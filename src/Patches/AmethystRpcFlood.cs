using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

// RPC 洪水检测：统计每个客户端每秒发出的 RPC 数量。
// 只提醒、不拦截、不处罚——高频率发包也可能来自网络抖动或良性模组，
// 误判代价高于漏判，因此本模块永远不会丢弃消息或踢人。
internal static class AmethystRpcFlood
{
    private const float Window = 1f;
    private const float NotifyCooldown = 6f;

    private static readonly Dictionary<int, float> WindowStart = new Dictionary<int, float>();
    private static readonly Dictionary<int, int> WindowCount = new Dictionary<int, int>();
    private static readonly Dictionary<int, float> NotifiedAt = new Dictionary<int, float>();

    internal static void Reset()
    {
        WindowStart.Clear();
        WindowCount.Clear();
        NotifiedAt.Clear();
    }

    internal static void Observe(InnerNetClient net, int ownerId)
    {
    try
        {
            if (net == null || ownerId < 0 || ownerId == net.ClientId) return;

            float now = Time.unscaledTime;
            if (!WindowStart.TryGetValue(ownerId, out float start) || now - start >= Window)
            {
                WindowStart[ownerId] = now;
                WindowCount[ownerId] = 1;
                return;
            }

            int count = WindowCount.TryGetValue(ownerId, out int c) ? c + 1 : 1;
            WindowCount[ownerId] = count;

            int limit = 25;
            try { limit = Mathf.Clamp(AmethystConfig.RpcFloodThreshold.Value, 25, 500); } catch { }
            // 第 263 轮：原来是 `count != limit`，一旦某一秒跳过 25（例如 24→26）就永远不会触发。
            // 改为 `count < limit`，配合下面的 6 秒冷却避免刷屏。
            if (count < limit) return;

            if (NotifiedAt.TryGetValue(ownerId, out float t) && now - t < NotifyCooldown) return;
            NotifiedAt[ownerId] = now;

            string who = AmethystAccess.ClientName(net, ownerId);
            string whoShow = AmethystColors.Name(net, ownerId) ?? who;
            AmethystToast.Push(AmethystText.T("Guard"),
                AmethystText.T("AbnormalRPCRPCFloodS", whoShow, limit),
                5.0f, AmethystNotifyKind.Warning);
            AmethystEventLog.Add(who + " · " + AmethystText.T("AbnormalRPCRPCFloodSNotifyOnly", limit),
                AmethystNotifyKind.Warning, AmethystEventCat.Other);
        }
        catch { }
    }
}

// 洪水统计原本挂在 `InnerNetClient.HandleGameData` 的 Prefix 上，
// 会**独立复制并重解析每一个收包**（`MessageReader.Get` + 遍历全部子消息）。
// 现已并入 `Patches\AmethystGameDataScanner.cs` 的单遍扫描器 ——
// 现在一个包只被遍历一次，洪水计数顺带在同一趟里完成。
// 四项相关开关全关时，扫描器连包都不碰。
[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystRpcFloodResetPatch
{
    private static void Postfix() => AmethystRpcFlood.Reset();
}
