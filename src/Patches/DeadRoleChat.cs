using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;


internal static class AmethystChatBubbleRole
{
    internal static void Apply(ChatBubble bubble, NetworkedPlayerInfo playerInfo, bool voted)
    {
        try
        {
            if (bubble == null || bubble.NameText == null) return;
            if (voted) return;
            if (playerInfo == null) return;

            bubble.NameText.richText = true;

            if (AmethystConfig.ColorBlindText != null && AmethystConfig.ColorBlindText.Value
                && bubble.ColorBlindName != null)
            {
                int cid = playerInfo.DefaultOutfit != null ? playerInfo.DefaultOutfit.ColorId : 0;
                string cn = AmethystColors.ColoredColorName(cid);
                if (!string.IsNullOrEmpty(cn) && bubble.ColorBlindName.text != cn)
                {
                    bubble.ColorBlindName.richText = true;
                    bubble.ColorBlindName.text = cn;
                }
            }

            PlayerControl sourcePlayer = playerInfo.Object;
            string rawName = sourcePlayer != null && sourcePlayer.Data != null ? sourcePlayer.Data.PlayerName : null;
            bool colorOn = AmethystColors.NameColorsOn && sourcePlayer != null;
            
            if (colorOn && bubble.NameText != null)
                bubble.NameText.color = AmethystColors.DisplayColor(sourcePlayer);

            bool roleOn = AmethystConfig.ShowSelfRole.Value || AmethystConfig.ShowRolesWhenDead.Value
                || AmethystConfig.ShowImpostorTeammateRole.Value;

            if (!roleOn)
            {
                if (colorOn && !string.IsNullOrEmpty(rawName)) bubble.NameText.SetText(rawName);
                return;
            }

            if (sourcePlayer == null) return;
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null) return;

            AmongUsClient net = AmongUsClient.Instance;
            if (net == null || !net.IsGameStarted) return;
            if (LobbyBehaviour.Instance != null) return;

            bool shouldShow = false;
            if (local.Data.IsDead)
            {
                
                if (AmethystConfig.ShowRolesWhenDead.Value &&
                    AmethystPlayerCacheStore.LocalIsCrewmateGhost)
                    shouldShow = true;
            }
            else
            {
                if (AmethystDeadRoleReveal.ShouldShowAlive(sourcePlayer))
                    shouldShow = true;
            }

            string tag = shouldShow ? AmethystDeadRoleReveal.BuildChatTag(sourcePlayer) : null;
            if (string.IsNullOrEmpty(tag))
            {
                if (colorOn && !string.IsNullOrEmpty(rawName)) bubble.NameText.SetText(rawName);
                return;
            }

            string formatted = $"<size=75%>{tag}</size>";
            if (sourcePlayer.PlayerId == local.PlayerId)
                bubble.NameText.SetText(formatted + " " + rawName);
            else
                bubble.NameText.SetText(rawName + " " + formatted);
        }
        catch { }
    }
}


[HarmonyPatch(typeof(ChatBubble), "SetName")]
internal static class AmethystChatBubbleRolePatch
{
    private static void Postfix(ChatBubble __instance, bool voted)
    {
        AmethystChatBubbleRole.Apply(__instance, __instance != null ? __instance.playerInfo : null, voted);
    }
}


[HarmonyPatch(typeof(ChatController), "SetChatBubbleName")]
internal static class AmethystChatBubbleRoleEarlyPatch
{
    private static void Postfix(ChatBubble bubble, NetworkedPlayerInfo playerInfo, bool didVote)
    {
        AmethystChatBubbleRole.Apply(bubble, playerInfo, didVote);
    }
}
