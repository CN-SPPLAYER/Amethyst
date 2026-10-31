using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystMeetingIntroNameColor
{
    
    private static readonly Dictionary<int, Color> CardColors = new Dictionary<int, Color>();

    private static bool On => AmethystColors.NameColorsOn;

    internal static bool TryGetCachedColor(int instanceId, out Color color)
    {
        return CardColors.TryGetValue(instanceId, out color);
    }

    internal static void ApplyFromInfo(PlayerVoteArea pv, NetworkedPlayerInfo info)
    {
        try
        {
            if (!On || pv == null || info == null) return;

            TextMeshPro nameText = pv.NameText;
            if (nameText == null) return;

            PlayerControl pc = AmethystMeetingNameColorPatch.FindPlayer(info.PlayerId);
            Color want;
            if (pc != null && pc.Data != null)
            {
                want = AmethystColors.DisplayColor(pc);
            }
            else
            {
                int cid = info.DefaultOutfit != null ? info.DefaultOutfit.ColorId : 0;
                want = AmethystColors.ColorOf(cid);
            }

            Apply(pv, nameText, want);
        }
        catch { }
    }

    internal static void ApplyByPlayerId(PlayerVoteArea pv)
    {
        try
        {
            if (!On || pv == null) return;

            TextMeshPro nameText = pv.NameText;
            if (nameText == null) return;

            if (CardColors.TryGetValue(pv.GetInstanceID(), out Color cached))
            {
                if (nameText.color != cached) nameText.color = cached;
                return;
            }

            PlayerControl pc = AmethystMeetingNameColorPatch.FindPlayer(pv.PlayerId.Value);
            if (pc == null || pc.Data == null) return;

            Apply(pv, nameText, AmethystColors.DisplayColor(pc));
        }
        catch { }
    }

    internal static void ReapplyCached(PlayerVoteArea pv)
    {
        try
        {
            if (!On || pv == null) return;

            TextMeshPro nameText = pv.NameText;
            if (nameText == null) return;

            if (!CardColors.TryGetValue(pv.GetInstanceID(), out Color want)) return;
            if (nameText.color != want) nameText.color = want;
        }
        catch { }
    }

    private static void Apply(PlayerVoteArea pv, TextMeshPro nameText, Color want)
    {
        try { CardColors[pv.GetInstanceID()] = want; } catch { }
        if (nameText.color != want) nameText.color = want;
    }

    internal static void ClearCache()
    {
        try { CardColors.Clear(); } catch { }
    }

    internal static void Tick()
    {
        try
        {
            if (!On) return;

            MeetingHud hud = MeetingHud.Instance;
            if (hud == null) return;

            MeetingIntroAnimation intro = hud.MeetingIntro;
            if (intro != null && intro.deadCards != null)
            {
                for (int i = 0; i < intro.deadCards.Count; i++)
                {
                    PlayerVoteArea pv = intro.deadCards[i];
                    if (pv != null) ReapplyCached(pv);
                }
            }

            var states = hud.playerStates;
            if (states != null)
            {
                for (int i = 0; i < states.Length; i++)
                {
                    PlayerVoteArea pv = states[i];
                    if (pv != null) ApplyByPlayerId(pv);
                }
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerVoteArea), "SetCosmetics")]
internal static class AmethystVoteAreaSetCosmeticsColorPatch
{
    private static void Postfix(PlayerVoteArea __instance, NetworkedPlayerInfo playerInfo)
    {
        AmethystMeetingIntroNameColor.ApplyFromInfo(__instance, playerInfo);
    }
}

[HarmonyPatch(typeof(PlayerVoteArea), "SetReporter")]
internal static class AmethystVoteAreaSetReporterColorPatch
{
    private static void Postfix(PlayerVoteArea __instance)
    {
        AmethystMeetingIntroNameColor.ApplyByPlayerId(__instance);
    }
}

[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystMeetingColorCacheResetPatch
{
    private static void Prefix()
    {
        AmethystMeetingIntroNameColor.ClearCache();
    }
}
