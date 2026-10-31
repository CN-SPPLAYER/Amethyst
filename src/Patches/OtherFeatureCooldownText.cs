using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace Amethyst.Patches;

[HarmonyPatch(typeof(ActionButton), "SetCoolDown")]
internal static class AmethystCooldownTextPatch
{
    public static void Postfix(ActionButton __instance, float timer)
    {
        if (AmethystCompat.OtherCooldown) return;
        if (!(AmethystConfig.BetterCooldownDisplay.Value || AmethystConfig.CooldownDecimals.Value) || __instance == null || __instance.cooldownTimerText == null) return;
        Format(__instance, timer);
    }

    private static readonly Dictionary<int, string> TextCache = new Dictionary<int, string>(160);

    private static string TextOf(int key)
    {
        if (key <= 0) return string.Empty;
        if (TextCache.TryGetValue(key, out string hit) && hit != null) return hit;

        string made = key >= 2000 ? (key - 2000).ToString() : ((key - 1000) / 10f).ToString("0.0");
        TextCache[key] = made;
        return made;
    }

    internal static void Format(ActionButton button, float timer)
    {
        try
        {
            if (button == null || button.cooldownTimerText == null) return;

            int key;
            if (timer < 0.1f) key = 0;
            else if (timer > 10f) key = 2000 + Mathf.Clamp(Mathf.CeilToInt(timer), 11, 9999);
            else key = 1000 + Mathf.Clamp(Mathf.FloorToInt(timer * 10f), 1, 99);

            string want = TextOf(key);
            if (button.cooldownTimerText.text != want) button.cooldownTimerText.text = want;
        }
        catch { }
    }
}