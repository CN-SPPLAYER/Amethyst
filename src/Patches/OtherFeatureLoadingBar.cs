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


[HarmonyPatch(typeof(LoadingBarManager), "SetLoadingPercent")]
internal static class AmethystLoadingBarManagerSetPatch
{
    [HarmonyPrefix]
    private static bool Prefix(float percent)
    {
        
        if (!AmethystBetterLoadingPatch.AllowLoadingBarUpdate)
            AmethystBetterLoadingPatch.NoteGamePercent(percent);

        return !AmethystBetterLoadingPatch.Active || AmethystBetterLoadingPatch.AllowLoadingBarUpdate;
    }
}


[HarmonyPatch(typeof(LoadingBarManager), "ToggleLoadingBar")]
internal static class AmethystLoadingBarManagerTogglePatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return !AmethystBetterLoadingPatch.Active;
    }
}
