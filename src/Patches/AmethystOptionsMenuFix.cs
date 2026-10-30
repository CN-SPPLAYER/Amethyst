using System;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 主界面「设置」打不开的修复。
//
// 现象：点击设置（齿轮）后设置弹窗根本不出现（点「开始」正常）。Player.log 栈：
//   NullReferenceException: Object reference not set to an instance of an object.
//     at OptionsMenuBehaviour.Open ()
//     at UnityEngine.Events.InvokableCall.Invoke (System.Object[] args)
//     at UnityEngine.Events.UnityEvent.Invoke ()
//     at PassiveButton.ReceiveClickUp ()
//     at PassiveButtonManager.Update ()
// 说明点击事件已被接收，但 OptionsMenuBehaviour.Open() 自身在第一条指令就抛异常，UI 因此不显示。
//
// 原因：OptionsMenuBehaviour 的部分字段（Tabs / ControllerSelectable / DefaultButtonSelected）
// 为 null，Open() 直接解引用即抛异常。这是已知问题——参考模组 ElysiumMenu 的
// `menu\MainMenuOptionsFix.cs` 就是专门为同一个 Open() NRE 写的修复，做法与此处一致。
//
// 修复：
//   1) Prefix 在 Open 之前补齐这些字段，让原版 Open() 能正常跑完；
//   2) 万一仍然抛异常，用 Finalizer 吞掉异常并手动「软打开」设置面板，
//      保证设置界面无论如何都能打开，而不是卡在 NRE。
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

        // 原版 Open() 抛异常时面板不会显示。这里补齐字段并按原版流程手动软打开，然后吞掉异常。
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

    // 补齐 Open() 会解引用、但可能为 null 的字段。
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

    // 原版 Open() 的正常收尾动作；在异常兜底路径里手动执行一遍，使面板真正显示出来。
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
