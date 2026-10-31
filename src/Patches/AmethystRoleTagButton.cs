using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Amethyst.Patches;

internal static class AmethystRoleTagButton
{
    private const string ButtonName = "AmethystRoleTagButton";

    private const float TagScale = 0.81f;

    private static readonly Color HoverPurple = new Color32(107, 47, 188, 10);

    private static bool _loggedChildren;

    private static void SetHighlight(Transform root, string child, bool front, Sprite icon)
    {
        Transform t = root.FindChild(child);
        if (t == null) return;

        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = HoverPurple;
            if (icon != null) sr.sprite = icon;
        }

        t.localPosition = new Vector3(0f, 0f, front ? 1.4f : 0.5f);
        t.localScale = Vector3.one;
    }

    private static void LogChildrenOnce(GameObject go)
    {
        if (_loggedChildren || go == null) return;
        _loggedChildren = true;
        try
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform c = go.transform.GetChild(i);
                if (c == null) continue;
                SpriteRenderer csr = c.GetComponent<SpriteRenderer>();
                sb.Append(c.name);
                if (csr != null) sb.Append("(a=").Append(csr.color.a.ToString("0.00")).Append(')');
                sb.Append(", ");
            }
            AmethystPlugin.Logger?.LogInfo($"[RoleTag] EditTag button children: {sb}");
        }
        catch { }
    }

    private static bool IsDeadCard(PlayerVoteArea pva)
    {
        try { if (pva.AmDead) return true; } catch { }
        return AmethystRoleTagStore.IsDeadPlayer(pva.PlayerId);
    }

    internal static void Create(MeetingHud hud)
    {
        if (!AmethystRoleTagStore.Enabled) return;
        if (hud == null || hud.playerStates == null) return;

        for (int i = 0; i < hud.playerStates.Count; i++)
        {
            PlayerVoteArea pva = hud.playerStates[i];
            if (pva == null || pva.transform == null) continue;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local != null && local.PlayerId == pva.PlayerId) continue;

            Transform existing = pva.transform.FindChild(ButtonName);

            if (IsDeadCard(pva))
            {
                if (existing != null)
                {
                    try { UnityEngine.Object.Destroy(existing.gameObject); } catch { }
                }
                continue;
            }

            if (existing != null) continue;   

            try
            {
                Transform template = pva.Buttons != null ? pva.Buttons.transform.Find("CancelButton") : null;
                if (template == null) continue;

                GameObject go = UnityEngine.Object.Instantiate(template.gameObject, pva.transform);
                if (go == null) continue;

                go.name = ButtonName;
                go.transform.localPosition = new Vector3(-0.95f, 0.03f, -1.31f);

                go.transform.localScale = go.transform.localScale * TagScale;

                Sprite tag = AmethystResources.EditTag;
                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                if (sr != null && tag != null) sr.sprite = tag;

                SetHighlight(go.transform, "ControllerHighlight", true, tag);
                SetHighlight(go.transform, "Highlight", false, tag);
                SetHighlight(go.transform, "HoverHighlight", false, tag);

                LogChildrenOnce(go);

                byte target = pva.PlayerId;
                PassiveButton button = go.GetComponent<PassiveButton>();
                if (button != null)
                {
                    button.OnClick = new Button.ButtonClickedEvent();
                    button.OnClick.AddListener((Action)(() => AmethystRoleTagPicker.Open(target)));
                }
            }
            catch { }
        }
    }

    internal static void Clear(MeetingHud hud)
    {
        if (hud == null || hud.playerStates == null) return;

        for (int i = 0; i < hud.playerStates.Count; i++)
        {
            PlayerVoteArea pva = hud.playerStates[i];
            if (pva == null || pva.transform == null) continue;

            Transform t = pva.transform.FindChild(ButtonName);
            if (t != null)
            {
                try { UnityEngine.Object.Destroy(t.gameObject); } catch { }
            }
        }
    }

    internal static string Suffix(byte playerId) => AmethystRoleTagStore.Suffix(playerId);
}

[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystRoleTagButtonStartPatch
{
    private static void Postfix(MeetingHud __instance) => AmethystRoleTagButton.Create(__instance);
}

[HarmonyPatch(typeof(MeetingHud), "Update")]
internal static class AmethystRoleTagButtonUpdatePatch
{
    private static int _tick;

    private static void Postfix(MeetingHud __instance)
    {
        if (!AmethystRoleTagStore.Enabled) return;

        _tick = _tick >= 30 ? 0 : _tick + 1;
        if (_tick != 0) return;

        AmethystRoleTagButton.Create(__instance);
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnPlayerLeft")]
internal static class AmethystRoleTagButtonLeftPatch
{
    private static void Postfix()
    {
        try
        {
            AmethystRoleTagStore.InvalidateDeadCache();
            AmethystRoleTagStore.InvalidateSuffix();

            MeetingHud hud = MeetingHud.Instance;
            if (hud != null) AmethystRoleTagButton.Create(hud);
        }
        catch { }
    }
}
