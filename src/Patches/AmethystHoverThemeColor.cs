using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(ButtonRolloverHandler))]
internal static class AmethystHoverThemeColorPatch
{
    private static bool Enabled =>
        AmethystConfig.HoverThemeColor != null && AmethystConfig.HoverThemeColor.Value;

    private static Color Theme
    {
        get { try { return AmethystStyle.Current.Accent; } catch { return Color.white; } }
    }

    private static bool IsGreenHighlight(Color c)
    {
        try { if (c == Palette.AcceptedGreen) return true; } catch { }
        if (c.r > 0.01f) return false;
        if (c.a < 0.99f) return false;
        return c.g > 0.99f && c.b >= -0.01f && c.b <= 0.18f;
    }

    [HarmonyPatch("DoMouseOver")]
    [HarmonyPrefix]
    private static void DoMouseOver_Prefix(ButtonRolloverHandler __instance)
    {
        if (!Enabled || __instance == null) return;
        try
        {
            if (IsGreenHighlight(__instance.OverColor))
                __instance.OverColor = Theme;
        }
        catch { }
    }

    [HarmonyPatch("ChangeOutColor")]
    [HarmonyPrefix]
    private static void ChangeOutColor_Prefix(ButtonRolloverHandler __instance, ref Color color)
    {
        if (!Enabled || __instance == null) return;
        try
        {
            if (IsGreenHighlight(color)) color = Theme;
        }
        catch { }
    }
}
