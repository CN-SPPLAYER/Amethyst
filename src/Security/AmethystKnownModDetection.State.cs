using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static partial class AmethystKnownModDetection
{
    internal static void Reset()
    {
        DetectedMods.Clear();
        Claimed.Clear();
        UnknownHits.Clear();
        LoggedRaw.Clear();
    }

    internal static string GetDetectedModName(PlayerControl player)
    {
        if (player == null) return null;
        if (DetectedMods.TryGetValue(player.PlayerId, out var dict) && dict != null && dict.Count > 0)
        {
            var names = new List<string>(dict);
            return string.Join("+", names);
        }
        return null;
    }

    internal static void Observe(PlayerControl sender, byte callId, MessageReader reader)
    {
        try
        {
            if (AmethystConfig.ForeignModDetect == null || !AmethystConfig.ForeignModDetect.Value) return;
            if (AmongUsClient.Instance == null) return;
            if (sender == null || sender == PlayerControl.LocalPlayer) return;
            if (sender.Data == null) return;

            if (callId >= VanillaTop && LoggedRaw.Add(sender.PlayerId))
            {
                
                string who = SafeName(sender);
                AmethystPlugin.Logger?.LogInfo(
                    $"[ModDetect] 外来模组识别在跑：收到 {who} 的高 ID RPC {callId}（原版最大 {VanillaTop - 1}）");
            }

            CheckRpc(sender, callId, reader);
        }
        catch { }
    }
}
