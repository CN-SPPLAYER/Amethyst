using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using AmongUs.Data;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace Amethyst.Patches;


[HarmonyPatch(typeof(ChatController), "Awake")]
internal static class AmethystChatHistoryPatch
{
    public static void Postfix(ChatController __instance)
    {
        if (!AmethystConfig.ChatHistory80.Value || __instance == null || __instance.chatBubblePool == null) return;
        try
        {
            __instance.chatBubblePool.poolSize = 80;
            __instance.chatBubblePool.ReclaimOldest();
        }
        catch { }
    }
}


[HarmonyPatch]
internal static class AmethystChatVisibilityState
{
    internal static bool ShouldShow()
    {
        try
        {
            if (AmethystConfig.VisualAlwaysShowChat.Value) return true;
            if (HudManager.Instance == null) return false;
            if (LobbyBehaviour.Instance != null || MeetingHud.Instance != null || ExileController.Instance != null || IntroCutscene.Instance != null)
                return true;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return false;
            return local.Data.IsDead;
        }
        catch { return false; }
    }
}


[HarmonyPatch(typeof(ChatController), "SetVisible")]
internal static class AmethystAlwaysChatVisiblePatch
{
    public static void Prefix(ref bool visible)
    {
        visible = AmethystChatVisibilityState.ShouldShow();
    }
}


[HarmonyPatch(typeof(MatchInfoHudButton), "Update")]
internal static class AmethystChatOverlapFixPatch
{
    public static bool Prefix(MatchInfoHudButton __instance)
    {
        try
        {
            if (__instance == null || __instance.aspectPosition == null) return true;

            if (AmethystChatVisibilityState.ShouldShow())
                __instance.aspectPosition.DistanceFromEdge = MatchInfoHudButton.adjustedDistanceFromEdge;
            else
                __instance.aspectPosition.DistanceFromEdge = MatchInfoHudButton.defaultDistanceFromEdge;

            return false;
        }
        catch { return true; }
    }
}


internal static class AmethystAlwaysChatState
{
    private static int _chatId = -1;
    private static bool _active;
    private static bool _hasState;

    internal static void Tick(HudManager __instance)
    {
        try
        {
            if (__instance == null || __instance.Chat == null) return;
            GameObject chat = ((Component)__instance.Chat).gameObject;
            int id = chat.GetInstanceID();
            bool active = AmethystChatVisibilityState.ShouldShow();
            if (_hasState && _chatId == id && _active == active) return;
            _chatId = id;
            _active = active;
            _hasState = true;
            if (chat.activeSelf != active) chat.SetActive(active);
        }
        catch { }
    }
}
