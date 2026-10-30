using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(InnerNetClient), "CanBan")]
internal static class MatchCanBanPatch
{
    public static void Postfix(InnerNetClient __instance, ref bool __result)
    {
        if (ShouldUnlock(__instance))
        {
            __result = true;
        }
    }

    internal static bool ShouldUnlock(InnerNetClient client)
    {
        return AmethystConfig.UnlockMatchKickBan.Value
            && client != null
            && client.AmHost
            && ShipStatus.Instance != null;
    }
}

[HarmonyPatch(typeof(InnerNetClient), "CanKick")]
internal static class MatchCanKickPatch
{
    public static void Postfix(InnerNetClient __instance, ref bool __result)
    {
        if (MatchCanBanPatch.ShouldUnlock(__instance))
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(BanMenu), "SetVisible")]
internal static class MatchBanMenuVisibilityPatch
{
    private struct ButtonState
    {
        internal Sprite Sprite;
        internal Color Color;
    }

    private static readonly System.Collections.Generic.Dictionary<int, ButtonState> States = new System.Collections.Generic.Dictionary<int, ButtonState>();

    public static void Postfix(BanMenu __instance, bool show)
    {
        // Dark Mode for BanMenu
        try
        {
            if (__instance != null && show)
            {
                Transform tr = ((Component)__instance).transform;
                if (!AmethystConfig.DarkChatTheme.Value)
                {
                    RestoreAll();
                    return;
                }
                // Darken background using custom AmogusBan texture
                Transform bgTrans = tr.Find("Background");
                if (bgTrans != null)
                {
                    SpriteRenderer bgSr = bgTrans.GetComponent<SpriteRenderer>();
                    if (bgSr != null)
                    {
                        if (AmethystResources.AmogusBan != null)
                        {
                            bgSr.sprite = AmethystResources.AmogusBan;
                            bgSr.color = Color.white;
                        }
                        else
                        {
                            AmethystPalette palette = AmethystStyle.Current;
                            bgSr.color = Color.Lerp(new Color(0.018f, 0.020f, 0.030f, 0.90f), palette.Panel, 0.40f);
                        }
                    }
                }
                // Accent colors for Buttons
                if (__instance.BanButton != null)
                {
                    PassiveButton pb = __instance.BanButton.GetComponent<PassiveButton>();
                    if (pb != null)
                    {
                        pb.activeTextColor = AmethystStyle.Current.Accent;
                        pb.inactiveTextColor = Color.grey;
                    }
                    Transform banIcon = ((Component)__instance.BanButton).transform.Find("Icon");
                    if (banIcon != null)
                    {
                        SpriteRenderer banSr = banIcon.GetComponent<SpriteRenderer>();
                        if (banSr != null)
                        {
                            CacheSprite(banSr);
                            banSr.color = AmethystStyle.Current.Accent;
                            banSr.enabled = true;
                        }
                    }
                }
                if (__instance.KickButton != null)
                {
                    PassiveButton pb = __instance.KickButton.GetComponent<PassiveButton>();
                    if (pb != null)
                    {
                        pb.activeTextColor = AmethystStyle.Current.Accent;
                        pb.inactiveTextColor = Color.grey;
                    }
                    Transform kickIcon = ((Component)__instance.KickButton).transform.Find("Icon");
                    if (kickIcon != null)
                    {
                        SpriteRenderer kickSr = kickIcon.GetComponent<SpriteRenderer>();
                        if (kickSr != null)
                        {
                            CacheSprite(kickSr);
                            kickSr.color = AmethystStyle.Current.Accent;
                            kickSr.enabled = true;
                        }
                    }
                }
            }
            else if (!AmethystConfig.DarkChatTheme.Value)
            {
                RestoreAll();
            }
        }
        catch { }

        if (__instance == null || !show || AmongUsClient.Instance == null || !MatchCanBanPatch.ShouldUnlock((InnerNetClient)AmongUsClient.Instance))
        {
            return;
        }

        try
        {
            ((Component)__instance.BanButton).gameObject.SetActive(true);
            ((Component)__instance.KickButton).gameObject.SetActive(true);
        }
        catch (System.Exception error)
        {
            AmethystPlugin.Logger?.LogWarning((object)$"Match ban menu unlock failed: {error.Message}");
        }
    }

    internal static void RestoreAll()
    {
        try
        {
            BanMenu[] menus = Object.FindObjectsOfType<BanMenu>(true);
            for (int i = 0; i < menus.Length; i++) Restore(menus[i]);
        }
        catch { }
    }

    private static void Restore(BanMenu menu)
    {
        if (menu == null) return;
        RestoreButton(menu.BanButton != null ? ((Component)menu.BanButton).gameObject : null);
        RestoreButton(menu.KickButton != null ? ((Component)menu.KickButton).gameObject : null);
    }

    private static void RestoreButton(GameObject obj)
    {
        if (obj == null) return;

        SpriteRenderer icon = FindButtonIcon(obj);
        if (icon == null) return;
        if (States.TryGetValue(icon.GetInstanceID(), out ButtonState state))
        {
            icon.sprite = state.Sprite;
            icon.color = state.Color;
        }
        icon.enabled = true;
    }

    private static void CacheSprite(SpriteRenderer button)
    {
        if (button == null) return;
        GameObject obj = button.gameObject;
        if (obj == null) return;
        int id = button.GetInstanceID();
        if (States.ContainsKey(id)) return;
        States[id] = new ButtonState
        {
            Sprite = button.sprite,
            Color = button.color,
        };
    }

    private static SpriteRenderer FindButtonIcon(GameObject root)
    {
        if (root == null) return null;
        Transform icon = root.transform.Find("Icon");
        if (icon != null)
        {
            SpriteRenderer renderer = icon.GetComponent<SpriteRenderer>();
            if (renderer != null) return renderer;
        }

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].gameObject.name == "Icon")
                return renderers[i];
        }

        return null;
    }
}
