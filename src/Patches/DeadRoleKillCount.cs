using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;


[HarmonyPatch(typeof(PlayerControl), "MurderPlayer")]
internal static class AmethystKillCountPatch
{
    private static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
    {
        try
        {
            if (__instance == null || target == null) return;
            if ((resultFlags & MurderResultFlags.FailedError) != 0) return;
            if ((resultFlags & MurderResultFlags.FailedProtected) != 0) return;
            if (__instance.PlayerId == target.PlayerId) return;

            AmethystDeadRoleReveal.AddKill(__instance.PlayerId);
        }
        catch { }
    }
}


[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystKillCountResetPatch
{
    private static void Prefix()
    {
        AmethystDeadRoleReveal.ResetKills();
    }
}
