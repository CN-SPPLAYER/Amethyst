using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst;

// 生成洪水(spawn flood)防护：作弊端在单帧内批量生成大量对象会导致卡死/崩溃。
// 参考 Nocturne(NocturneSpawnFlood)/Elysium 的防护思路：每帧最多处理 CapPerFrame 个生成，
// 超出部分直接丢弃（进房/开局/Lobby/对局有 5 秒宽限，正常流程不受影响）。
// 开关：Guard.SpawnFloodGuard（默认开，属于“缺失功能”补齐项——旧的 BlockFakeMeetings
// 描述中写了“拦截生成洪水”却从未实现）。
internal static class AmethystSpawnFlood
{
    private const int CapPerFrame = 200;
    private const float GraceSeconds = 5f;

    private static int _frame = -1;
    private static int _count;
    private static float _lastNote;
    private static float _graceUntil;

    internal static bool On
    {
        get
        {
            try { return AmethystConfig.SpawnFloodGuard != null && AmethystConfig.SpawnFloodGuard.Value; }
            catch { return false; }
        }
    }

    internal static void Grace()
    {
        _graceUntil = Time.unscaledTime + GraceSeconds;
    }

    internal static bool Allow()
    {
        if (Time.unscaledTime < _graceUntil) return true;
        int f = Time.frameCount;
        if (f != _frame)
        {
            _frame = f;
            _count = 0;
        }
        _count++;
        return _count <= CapPerFrame;
    }

    internal static Il2CppSystem.Collections.IEnumerator Drop()
    {
        if (Time.unscaledTime - _lastNote >= 1f)
        {
            _lastNote = Time.unscaledTime;
            AmethystSecurityNotify.Fire("SpawnFloodTrimmed");
        }
        return Empty().WrapToIl2Cpp();
    }

    private static IEnumerator Empty()
    {
        yield break;
    }
}

[HarmonyPatch(typeof(InnerNetClient), "CoHandleSpawn")]
internal static class AmethystSpawnFloodPatch
{
    public static bool Prefix(ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (!AmethystSpawnFlood.On || AmethystSpawnFlood.Allow()) return true;
        __result = AmethystSpawnFlood.Drop();
        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystSpawnFloodJoinPatch
{
    public static void Prefix() => AmethystSpawnFlood.Grace();
}

[HarmonyPatch(typeof(LobbyBehaviour), "Start")]
internal static class AmethystSpawnFloodLobbyPatch
{
    public static void Prefix() => AmethystSpawnFlood.Grace();
}

[HarmonyPatch(typeof(ShipStatus), "Start")]
internal static class AmethystSpawnFloodShipPatch
{
    public static void Prefix() => AmethystSpawnFlood.Grace();
}
