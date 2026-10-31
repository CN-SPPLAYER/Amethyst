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


internal static class AmethystNoWinConditions
{
    internal static bool On
    {
        get
        {
            try
            {
                if (AmethystConfig.NoWinConditions == null || !AmethystConfig.NoWinConditions.Value) return false;
                return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            }
            catch { return false; }
        }
    }
}


[HarmonyPatch(typeof(LogicGameFlowNormal), "CheckEndCriteria")]
internal static class AmethystNoWinConditionsPatch
{
    public static bool Prefix() => !AmethystNoWinConditions.On;
}


[HarmonyPatch(typeof(LogicGameFlowHnS), "CheckEndCriteria")]
internal static class AmethystNoWinConditionsHnSPatch
{
    public static bool Prefix() => !AmethystNoWinConditions.On;
}


[HarmonyPatch(typeof(GameManager), "CheckTaskCompletion")]
internal static class AmethystNoWinConditionsTaskPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (!AmethystNoWinConditions.On) return true;
        __result = false;
        return false;
    }
}
