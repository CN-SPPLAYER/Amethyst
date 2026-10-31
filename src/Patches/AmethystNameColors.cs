using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(ChatBubble), "SetColorblindText")]
internal static class AmethystChatBubbleColorBlindPatch
{
    private static void Postfix(ChatBubble __instance)
    {
        Apply(__instance, __instance != null ? __instance.playerInfo : null);
    }

    internal static void Apply(ChatBubble bubble, NetworkedPlayerInfo info)
    {
        try
        {
            if (bubble == null || bubble.ColorBlindName == null) return;
            if (AmethystConfig.ColorBlindText == null || !AmethystConfig.ColorBlindText.Value) return;
            if (info == null) return;

            int cid = 0;
            try { cid = info.DefaultOutfit != null ? info.DefaultOutfit.ColorId : 0; } catch { }

            string cn = AmethystColors.ColoredColorName(cid);
            if (!string.IsNullOrEmpty(cn) && bubble.ColorBlindName.text != cn)
            {
                bubble.ColorBlindName.richText = true;
                bubble.ColorBlindName.text = cn;
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChatController), "SetChatBubbleName")]
internal static class AmethystChatBubbleColorBlindSyncPatch
{
    private static void Postfix(ChatBubble bubble, NetworkedPlayerInfo playerInfo)
    {
        AmethystChatBubbleColorBlindPatch.Apply(bubble, playerInfo);
    }
}

[HarmonyPatch(typeof(MeetingHud), "Update")]
internal static class AmethystMeetingNameColorPatch
{
    private static readonly PlayerControl[] ById = new PlayerControl[256];
    private static int _tick;
    private static int _playersSignature;

    private static void Postfix(MeetingHud __instance)
    {
        if (__instance == null) return;
        bool nameOn = AmethystColors.NameColorsOn;
        bool cbOn = AmethystConfig.ColorBlindText != null && AmethystConfig.ColorBlindText.Value;
        if (!nameOn && !cbOn) return;

        _tick = _tick >= 5 ? 0 : _tick + 1;
        if (_tick != 0) return;

        try
        {
            var states = __instance.playerStates;
            if (states == null) return;

            var all = PlayerControl.AllPlayerControls;
            int signature = 17;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    PlayerControl p = all[i];
                    if (p != null) signature = signature * 31 + p.PlayerId * 397 + p.GetInstanceID();
                }
            }
            if (_playersSignature != signature)
            {
                Array.Clear(ById, 0, ById.Length);
                _playersSignature = signature;
                if (all == null) return;
                for (int i = 0; i < all.Count; i++)
                {
                    PlayerControl p = all[i];
                    if (p == null) continue;
                    int id = p.PlayerId;
                    if (id >= 0 && id < ById.Length) ById[id] = p;
                }
            }

            for (int i = 0; i < states.Count; i++)
            {
                PlayerVoteArea pv = states[i];
                if (pv == null) continue;

                if (AmethystMeetingIntroNameColor.TryGetCachedColor(pv.GetInstanceID(), out Color cachedColor))
                {
                    if (nameOn && pv.NameText != null && pv.NameText.color != cachedColor)
                        pv.NameText.color = cachedColor;
                    continue;
                }

                int pid = pv.PlayerId.Value;
                if (pid < 0 || pid >= ById.Length) continue;
                PlayerControl pc = ById[pid];
                if (pc == null || pc.Data == null) continue;

                if (cbOn && pv.ColorBlindName != null)
                {
                    int cid = pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
                    string cn = AmethystColors.ColoredColorName(cid);
                    if (!string.IsNullOrEmpty(cn) && pv.ColorBlindName.text != cn) pv.ColorBlindName.text = cn;
                }

                if (nameOn && pv.NameText != null)
                {
                    Color want = AmethystColors.DisplayColor(pc);
                    if (pv.NameText.color != want) pv.NameText.color = want;
                }
            }
        }
        catch { }
    }

    internal static PlayerControl FindPlayer(byte id)
    {
        try
        {
            var all = PlayerControl.AllPlayerControls;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl p = all[i];
                if (p != null && p.PlayerId == id) return p;
            }
        }
        catch { }
        return null;
    }
}

[HarmonyPatch(typeof(PlayerVoteArea), "SetDead")]
internal static class AmethystVoteAreaDeadNamePatch
{
    private static void Postfix(PlayerVoteArea __instance)
    {
        try
        {
            if (__instance == null || __instance.NameText == null) return;
            if (!AmethystColors.NameColorsOn) return;

            PlayerControl pc = AmethystMeetingNameColorPatch.FindPlayer(__instance.PlayerId.Value);
            if (pc == null || pc.Data == null) return;
            __instance.NameText.color = AmethystColors.DisplayColor(pc);
        }
        catch { }
    }
}
