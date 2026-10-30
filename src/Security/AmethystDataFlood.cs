using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Amethyst;

// ===== 第 219 轮 #2：幻影 DATA 对象洪水拦截（移植自 OnyxMenu 1.2.3 `Security\DataFlood.cs`）=====
//
// 【它防的是什么】不是 RPC 洪水（那个有 `AmethystRpcFlood`），也不是大包（`AmethystGameDataScanner`），
//   而是**对象数据流**：攻击者持续发送 `tag = 1`（GameDataTypes.DataFlag）的子消息，
//   里面填**一个根本不存在的 netId**。原版每收到一条就要去对象表里找、找不到就排队等 spawn，
//   发得足够快就会把对象表/等待队列撑爆 ⇒ 卡死或崩溃。
//
// 【三条判据】完全照抄参考实现：
//   ① 这个 netId 在对象表里已有、或已被销毁 ⇒ 正常包，放行；
//   ② 同一个未知 netId 第一次出现开始计时，**超过 3 秒**仍未解析出来 ⇒ 判定为幻影，丢弃；
//   ③ 1 秒窗口内 DATA 子消息 **超过 40 条**（且过了进房 5 秒宽限）⇒ 丢弃并提示。
//   另有 1024 条跟踪上限，超过直接丢弃，避免它自己把内存吃掉。
//
// 【为什么这次可以单独挂一个 Prefix、不会重演"一个包扫 4 遍"的旧问题】
//   旧的性能事故是**四个 Prefix 都挂在 `InnerNetClient.HandleGameData`（父包）上**，
//   各自 `MessageReader.Get` 复制整包再遍历一遍子消息 ⇒ 一个包被完整解析 3~4 遍（见
//   `AmethystGameDataScanner.cs` 顶部的事故记录）。
//   本条挂在 **`HandleGameDataInner`（单条子消息）** 上，是**另一个方法**，由游戏自己对每条子消息调用一次；
//   我们只读开头的 tag + 一个 PackedUInt32，读完立刻还原位置 —— **不复制整包、不遍历子消息**，
//   与父包那一趟扫描不叠加。
internal static class AmethystDataFlood
{
    private const float PhantomAfter = 3f;   // 未知 id 存活多久算幻影
    private const int PerSec = 40;           // 每秒 DATA 条数上限
    private const int MaxTracked = 1024;     // 跟踪表上限（防它自己吃内存）

    private static readonly Dictionary<uint, (float first, float last)> Seen =
        new Dictionary<uint, (float, float)>();
    private static readonly List<uint> Stale = new List<uint>();

    private static Il2CppSystem.Collections.IEnumerator _skip;
    private static float _windowAt;
    private static float _sweepAt;
    private static float _graceUntil;
    private static float _lastNote;
    private static int _hits;

    internal static bool On => AmethystConfig.BlockDataFlood != null && AmethystConfig.BlockDataFlood.Value;

    internal static void Reset()
    {
        Seen.Clear();
        _hits = 0;
        _graceUntil = Time.unscaledTime + 5f;   // 进房/开局 5 秒宽限：对象还在陆续 spawn
    }

    // 返回 true = 这一条 DATA 子消息应当被丢弃。
    internal static bool Check(InnerNetClient net, MessageReader reader)
    {
        if (reader == null || reader.Tag != 1) return false;   // 只认 DataFlag

        int pos = reader.Position;
        uint id;
        try { id = reader.ReadPackedUInt32(); }
        catch { try { reader.Position = pos; } catch { } return true; }   // 连 id 都读不出来 ⇒ 丢掉
        try { reader.Position = pos; } catch { }

        if (id == 0) return true;

        // ① 已知对象 / 已销毁对象 ⇒ 正常
        try
        {
            if (net != null && net.allObjects != null && net.allObjects.AllObjectsFast != null
                && net.allObjects.AllObjectsFast.ContainsKey(id))
                return false;
            if (net != null && net.DestroyedObjects != null && net.DestroyedObjects.Contains(id))
                return false;
        }
        catch { }

        float now = Time.unscaledTime;
        if (now >= _sweepAt && Seen.Count > 0) Sweep(net, now);

        if (Seen.TryGetValue(id, out var t))
        {
            Seen[id] = (t.first, now);
            // ② 这个 id 已经"未知"了超过 PhantomAfter ⇒ 幻影
            if (now - t.first > PhantomAfter) return true;
        }
        else
        {
            if (Seen.Count >= MaxTracked) return true;
            Seen[id] = (now, now);
        }

        // ③ 每秒条数上限
        if (now - _windowAt >= 1f)
        {
            _windowAt = now;
            _hits = 0;
        }
        return ++_hits > PerSec && now >= _graceUntil;
    }

    // 丢弃时给游戏一个空协程（原版把返回的 IEnumerator 当协程跑，给 null 会炸）。
    internal static Il2CppSystem.Collections.IEnumerator Skip()
    {
        if (Time.unscaledTime - _lastNote >= 1f)
        {
            _lastNote = Time.unscaledTime;
            try { AmethystSecurityNotify.Fire("TrimmedDataFlood"); } catch { }
        }
        return _skip ??= Empty().WrapToIl2Cpp();
    }

    private static void Sweep(InnerNetClient net, float now)
    {
        _sweepAt = now + 2f;
        Stale.Clear();
        foreach (var kv in Seen)
        {
            bool known = false;
            try
            {
                known = net != null && net.allObjects != null && net.allObjects.AllObjectsFast != null
                        && net.allObjects.AllObjectsFast.ContainsKey(kv.Key);
            }
            catch { }
            if (now - kv.Value.last > 10f || known) Stale.Add(kv.Key);
        }
        for (int i = 0; i < Stale.Count; i++) Seen.Remove(Stale[i]);
    }

    private static IEnumerator Empty()
    {
        yield break;
    }
}

[HarmonyPatch(typeof(InnerNetClient), "HandleGameDataInner")]
internal static class AmethystDataFloodPatch
{
    public static bool Prefix(InnerNetClient __instance, MessageReader reader,
                             ref Il2CppSystem.Collections.IEnumerator __result)
    {
        try
        {
            if (!AmethystDataFlood.On) return true;
            if (reader == null || !AmethystDataFlood.Check(__instance, reader)) return true;

            __result = AmethystDataFlood.Skip();
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystDataFloodJoinPatch
{
    public static void Prefix() => AmethystDataFlood.Reset();
}
