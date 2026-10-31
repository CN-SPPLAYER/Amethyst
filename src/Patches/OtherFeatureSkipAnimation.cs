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


[HarmonyPatch(typeof(KillOverlay), "ShowKillAnimation", new[] { typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo) })]
internal static class AmethystSkipKillAnimationInfoPatch
{
    public static bool Prefix() => !AmethystConfig.SkipKillAnimation.Value;
}


[HarmonyPatch(typeof(KillOverlay), "ShowKillAnimation", new[] { typeof(OverlayKillAnimation), typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo) })]
internal static class AmethystSkipKillAnimationOverlayPatch
{
    public static bool Prefix() => !AmethystConfig.SkipKillAnimation.Value;
}


[HarmonyPatch(typeof(KillOverlay), "ShowKillAnimation", new[] { typeof(OverlayKillAnimation), typeof(KillOverlayInitData) })]
internal static class AmethystSkipKillAnimationInitPatch
{
    public static bool Prefix() => !AmethystConfig.SkipKillAnimation.Value;
}
