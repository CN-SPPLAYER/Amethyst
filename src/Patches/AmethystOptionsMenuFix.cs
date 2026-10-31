using System;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(OptionsMenuBehaviour), "Open")]
internal static class AmethystOptionsMenuFixPatch
{
    public static void Prefix(OptionsMenuBehaviour __instance)
    {
        Repair(__instance);
    }

    public static Exception Finalizer(OptionsMenuBehaviour __instance, Exception __exception)
    {
        if (__exception == null) return null;

        try
        {
            AmethystPlugin.Logger?.LogWarning(
                $"OptionsMenuBehaviour.Open threw {__exception.GetType().Name}; applied repair. {__exception.Message}");
            Repair(__instance);
            SoftOpen(__instance);
        }
        catch { }

        return null;
    }

    private static void Repair(OptionsMenuBehaviour menu)
    {
        if (menu == null) return;

        try
        {
            if (menu.DefaultButtonSelected == null && menu.BackButton != null)
                menu.DefaultButtonSelected = menu.BackButton;
        }
        catch { }

        try
        {
            if (menu.ControllerSelectable == null)
                menu.ControllerSelectable = new Il2CppSystem.Collections.Generic.List<UiElement>();
        }
        catch { }

        try
        {
            if (menu.Tabs == null)
            {
                TabGroup[] tabs = menu.GetComponentsInChildren<TabGroup>(true);
                if (tabs != null && tabs.Length > 0)
                    menu.Tabs = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<TabGroup>(tabs);
            }
        }
        catch { }
    }

    private static void SoftOpen(OptionsMenuBehaviour menu)
    {
        if (menu == null) return;

        try { if (menu.gameObject != null) menu.gameObject.SetActive(true); } catch { }
        try { if (menu.Background != null && menu.Background.gameObject != null) menu.Background.gameObject.SetActive(true); } catch { }
        try { menu.ResetText(); } catch { }
        try { menu.UpdateButtons(); } catch { }
        try { menu.OpenTabGroup(0); } catch { }
        try { if (menu.MenuButton != null) menu.MenuButton.SelectButton(true); } catch { }
        try { menu.GrabControllerButtons(); } catch { }
    }
}
