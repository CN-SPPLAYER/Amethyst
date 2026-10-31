using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using AmongUs.Data;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace Amethyst.Patches;


[HarmonyPatch(typeof(AmongUs.Data.Player.PlayerBanData), nameof(AmongUs.Data.Player.PlayerBanData.BanMinutesLeft), MethodType.Getter)]
internal static class AmethystBypassDisconnectPenaltyMinutesLeftPatch
{
    private static void Postfix(AmongUs.Data.Player.PlayerBanData __instance, ref int __result)
    {
        if (!AmethystConfig.BypassDisconnectPenalty.Value) return;
        try { __instance.banPoints = 0f; } catch { }
        __result = 0;
    }
}


[HarmonyPatch(typeof(AmongUs.Data.Player.PlayerBanData), nameof(AmongUs.Data.Player.PlayerBanData.BanMinutes), MethodType.Getter)]
internal static class AmethystBypassDisconnectPenaltyMinutesPatch
{
    private static void Postfix(AmongUs.Data.Player.PlayerBanData __instance, ref float __result)
    {
        if (!AmethystConfig.BypassDisconnectPenalty.Value) return;
        try { __instance.banPoints = 0f; } catch { }
        __result = 0f;
    }
}


[HarmonyPatch(typeof(AmongUs.Data.Player.PlayerBanData), nameof(AmongUs.Data.Player.PlayerBanData.IsBanned), MethodType.Getter)]
internal static class AmethystBypassDisconnectPenaltyIsBannedPatch
{
    private static void Postfix(AmongUs.Data.Player.PlayerBanData __instance, ref bool __result)
    {
        if (!AmethystConfig.BypassDisconnectPenalty.Value) return;
        try { __instance.banPoints = 0f; } catch { }
        __result = false;
    }
}


[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystBlockRapidStartGamePatch
{
    private static float _lastGameStartTime = -1f;

    [HarmonyPrefix]
    private static bool Prefix()
    {
        
        if (AmethystBypassDisconnectPenaltyPatch.CheckHostRapidStart())
        {
            return false; 
        }

        try
        {
            if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost)
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastGameStartTime < 2f)
                {
                    
                    return false;
                }
                _lastGameStartTime = now;
            }
        }
        catch { }

        return true;
    }
}
