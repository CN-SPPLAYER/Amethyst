using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;


[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystRpcGuardJoinPatch
{
    private static void Postfix()
    {
        AmethystRpcGuard.MarkJoinGrace();
        AmethystRpcGuard.ResetRoundState();
    }
}
