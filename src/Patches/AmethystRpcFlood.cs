using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

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

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystRpcFloodResetPatch
{
    private static void Postfix() => AmethystRpcFlood.Reset();
}
