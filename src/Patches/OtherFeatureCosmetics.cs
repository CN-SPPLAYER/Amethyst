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


[HarmonyPatch(typeof(HatManager), "Initialize")]
internal static class AmethystUnlockCosmeticsPatch
{
    public static void Postfix(HatManager __instance)
    {
        if (AmethystCompat.OtherUnlockSkins || !AmethystConfig.UnlockAllCosmetics.Value || __instance == null) return;
        try
        {
            foreach (var bundle in __instance.allBundles) bundle.Free = true;
            foreach (var featuredBundle in __instance.allFeaturedBundles) featuredBundle.Free = true;
            foreach (var featuredCube in __instance.allFeaturedCubes) featuredCube.Free = true;
            foreach (var featuredItem in __instance.allFeaturedItems) featuredItem.Free = true;
            foreach (var hat in __instance.allHats) hat.Free = true;
            foreach (var nameplate in __instance.allNamePlates) nameplate.Free = true;
            foreach (var pet in __instance.allPets) pet.Free = true;
            foreach (var skin in __instance.allSkins) skin.Free = true;
            foreach (var starBundle in __instance.allStarBundles) starBundle.price = 0;
            foreach (var visor in __instance.allVisors) visor.Free = true;
        }
        catch { }
    }
}


[HarmonyPatch(typeof(PlayerPurchasesData), "GetPurchase")]
internal static class AmethystUnlockCosmeticsPurchasePatch
{
    
    public static bool Prefix(ref bool __result)
    {
        if (AmethystCompat.OtherUnlockSkins || !AmethystConfig.UnlockAllCosmetics.Value) return true;
        __result = true;
        return false;
    }
}
