using System;
using System.Text;
using HarmonyLib;
using Il2CppSystem.Collections;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(FriendsListUI), "Open")]
internal static class AmethystFriendsListOpenPatch
{
    
    private static bool _dumped;

    private static bool Prefix(FriendsListUI __instance)
    {
        try
        {
            if (__instance == null) return true;
            if (!VanillaWouldThrow()) return true;

            DumpOnce(__instance, "预判命中：HudManager.Instance.Chat 为空，走我们自己的打开流程");
            ManualOpen(__instance);
            return false;   
        }
        catch
        {
            return true;
        }
    }

    private static Exception Finalizer(FriendsListUI __instance, Exception __exception)
    {
        if (!(__exception is NullReferenceException)) return __exception;

        try
        {
            if (__instance == null) return __exception;

            DumpOnce(__instance, "兜底命中：原版 Open() 抛 NullReferenceException，已代为打开");
            ManualOpen(__instance);
            return null;
        }
        catch
        {
            return __exception;
        }
    }

    private static bool VanillaWouldThrow()
    {
        try
        {
            HudManager hud = HudManager.Instance;
            return hud != null && hud.Chat == null;
        }
        catch
        {
            return false;
        }
    }

    private static void ManualOpen(FriendsListUI ui)
    {
        
        DestroyBars<FriendsListBar>(ui);
        DestroyBars<LobbyPlayerBar>(ui);

        try { ui.friendBars = new List<FriendsListBar>(); } catch { }
        try { ui.lobbyBars = new List<LobbyPlayerBar>(); } catch { }
        try { ui.notifBars = new List<FriendsListBar>(); } catch { }
        try { ui.platformFriendBars = new List<FriendsListBar>(); } catch { }

        try { ui.UpdateFriendCodeUI(); } catch { }

        GameObject go = null;
        try
        {
            go = ui.gameObject;
            if (go != null && !go.activeSelf) go.SetActive(true);
        }
        catch { }

        try { if (ui.LobbyPlayersTab != null) ui.LobbyPlayersTab.SetActive(true); } catch { }
        try { if (ui.LobbyPlayersInactiveTab != null) ui.LobbyPlayersInactiveTab.SetActive(false); } catch { }
        try { ui.OpenTab(0); } catch { }

        try
        {
            FriendsListManager mgr = FriendsListManager.Instance;
            if (mgr != null) ui.StartCoroutine(mgr.RefreshFriendsList(null, true));
        }
        catch { }

        try
        {
            ControllerManager cm = ControllerManager.Instance;
            if (cm != null)
            {
                cm.OpenOverlayMenu(go != null ? go.name : string.Empty,
                                   ui.BackButton, ui.DefaultButtonSelected, ui.ControllerSelectable);
            }
        }
        catch { }
    }

    private static void DestroyBars<T>(FriendsListUI ui) where T : Component
    {
        try
        {
            T[] bars = ui.GetComponentsInChildren<T>(true);
            if (bars == null) return;

            for (int i = 0; i < bars.Length; i++)
            {
                try
                {
                    if (bars[i] != null && bars[i].gameObject != null)
                        UnityEngine.Object.Destroy(bars[i].gameObject);
                }
                catch { }
            }
        }
        catch { }
    }

    private static void DumpOnce(FriendsListUI ui, string tag)
    {
        if (_dumped) return;
        _dumped = true;

        try
        {
            var sb = new StringBuilder();
            sb.Append("[FriendsOpen] ").Append(tag);

            try
            {
                sb.Append(" | 场景=").Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
            catch { sb.Append(" | 场景=?"); }

            try
            {
                HudManager hud = HudManager.Instance;
                sb.Append(" | HudManager=").Append(hud == null ? "null" : "有");
                sb.Append(" HudManager.Chat=").Append(hud == null ? "-" : (hud.Chat == null ? "null" : "有"));
            }
            catch (Exception ex) { sb.Append(" | HudManager=异常(").Append(ex.GetType().Name).Append(")"); }

            try { sb.Append(" | TranslationController=").Append(TranslationController.Instance == null ? "null" : "有"); }
            catch { sb.Append(" | TranslationController=?"); }

            try { sb.Append(" | ControllerManager=").Append(ControllerManager.Instance == null ? "null" : "有"); }
            catch { sb.Append(" | ControllerManager=?"); }

            if (ui != null)
            {
                sb.Append(" | guestAccountWarnings=").Append(ui.guestAccountWarnings == null ? "null" : "有");
                sb.Append(" ViewRequestsButton=").Append(ui.ViewRequestsButton == null ? "null" : "有");
                sb.Append(" LobbyPlayersTab=").Append(ui.LobbyPlayersTab == null ? "null" : "有");
                sb.Append(" LobbyPlayersInactiveTab=").Append(ui.LobbyPlayersInactiveTab == null ? "null" : "有");
                sb.Append(" BackButton=").Append(ui.BackButton == null ? "null" : "有");
                sb.Append(" DefaultButtonSelected=").Append(ui.DefaultButtonSelected == null ? "null" : "有");
                sb.Append(" ControllerSelectable=").Append(ui.ControllerSelectable == null ? "null" : "有");

                try
                {
                    GameObject g = ui.gameObject;
                    sb.Append(" | 本体 activeSelf=").Append(g == null ? "?" : g.activeSelf.ToString());
                    sb.Append(" activeInHierarchy=").Append(g == null ? "?" : g.activeInHierarchy.ToString());
                    sb.Append(" 父=").Append(ui.transform.parent == null ? "(root)" : ui.transform.parent.name);
                }
                catch { }
            }

            AmethystPlugin.Logger?.LogInfo(sb.ToString());
        }
        catch (Exception ex)
        {
            try { AmethystPlugin.Logger?.LogWarning("[FriendsOpen] 转储失败: " + ex.Message); }
            catch { }
        }
    }
}

[HarmonyPatch(typeof(FriendsListManager), "OpenUi")]
internal static class AmethystFriendsListOpenUiShowPanelPatch
{
    private static void Prefix()
    {
        try { AmethystMainMenuSlide.EnsureShownNow(); } catch { }
    }
}