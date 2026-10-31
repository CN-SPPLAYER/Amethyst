using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Amethyst.Patches;


[HarmonyPatch(typeof(ChatController), "AlignAllBubbles")]
internal static class ChatAlignPatch
{
    public static void Prefix(ChatController __instance)
    {
        ChatBubbleAnimations.Before(__instance);
    }

    public static void Postfix()
    {
        
        try
        {
            ChatBubbleAnimations.After();
        }
        catch (Exception ex)
        {
            ChatBubbleAnimations.ClearSeen();
            AmethystPlugin.Logger?.LogWarning("[Bubble] Align 回流失败: " + ex.Message);
        }
    }
}


[HarmonyPatch(typeof(ChatController), "Toggle")]
internal static class ChatOpenAnimationPatch
{
    public static void Prefix(ChatController __instance, out bool __state)
    {
        __state = __instance != null && __instance.IsOpenOrOpening;
    }

    public static void Postfix(ChatController __instance, bool __state)
    {
        if (__state || __instance == null || !__instance.IsOpenOrOpening)
        {
            return;
        }

        ChatBubbleAnimations.Cascade(__instance);
    }
}


[HarmonyPatch(typeof(ChatBubble), "SetText")]
[HarmonyPriority(Priority.Low)]
internal static class ChatBubbleAnimationPatch
{
    public static void Postfix(ChatBubble __instance)
    {
        
        try
        {
            ChatBubbleAnimations.Register(__instance);
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning("[Bubble] SetText 入场登记失败: " + ex.Message);
        }
    }
}
