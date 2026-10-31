using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Amethyst.Patches;







internal static class ChatThemeStyler
{
    private struct IconState
    {
        internal Sprite Sprite;
        internal Color Color;
    }

    private static readonly Dictionary<int, IconState> originalIconStates = new Dictionary<int, IconState>();
    private static int lastChatId;
    private static string lastThemeId = string.Empty;
    private static bool lastEnabled;
    private static float nextInputRefreshAt;
    private static readonly Dictionary<int, SpriteRenderer> bubbleBackgrounds = new Dictionary<int, SpriteRenderer>();
    private static readonly Dictionary<int, Color> originalButtonBackgroundColors = new Dictionary<int, Color>();
    private static readonly Dictionary<int, Color> originalButtonIconColors = new Dictionary<int, Color>();
    private static readonly Dictionary<int, Color> originalButtonTextColors = new Dictionary<int, Color>();
    private static readonly Dictionary<int, Color> originalWindowColors = new Dictionary<int, Color>();

    internal static void RefreshInputs(ChatController chat, bool force = false)
    {
        if (chat == null) return;

        bool enabled = AmethystConfig.DarkChatTheme.Value;
        int chatId = chat.GetInstanceID();
        string themeId = AmethystStyle.Current.Id;

        if (!force && lastChatId == chatId && lastEnabled == enabled && string.Equals(lastThemeId, themeId, StringComparison.Ordinal) && Time.unscaledTime < nextInputRefreshAt)
        {
            return;
        }

        if (lastChatId != chatId)
        {
            originalIconStates.Clear();
            originalButtonBackgroundColors.Clear();
            originalButtonIconColors.Clear();
            originalButtonTextColors.Clear();
            originalWindowColors.Clear();
        }

        lastEnabled = enabled;
        lastChatId = chatId;
        lastThemeId = themeId;
        nextInputRefreshAt = Time.unscaledTime + 0.75f;

        if (enabled)
        {
            AmethystPalette palette = AmethystStyle.Current;
            Color background = Color.Lerp(new Color(0.018f, 0.020f, 0.030f, 1f), palette.Panel, 0.46f);
            Color textColor = DarkInputTextColor();
            ApplyFreeChatField(chat, background, textColor, true);
            ApplyQuickChatField(chat, background, textColor, true);
            ApplyWindow(chat);
        }
        else
        {
            Color background = Color.white;
            Color textColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            ApplyFreeChatField(chat, background, textColor, false);
            ApplyQuickChatField(chat, background, textColor, false);
            RestoreWindow(chat);
            RestoreInputButtons(chat);
            ApplyQuickChatIcons(chat, false);
            MatchBanMenuVisibilityPatch.RestoreAll();
        }
    }

    internal static void ApplyWindow(ChatController chat)
    {
        try
        {
            SpriteRenderer window = chat.backgroundImage;
            if (window == null)
            {
                return;
            }

            int id = window.GetInstanceID();
            if (!originalWindowColors.ContainsKey(id)) originalWindowColors[id] = window.color;
            Color tint = WindowTint(AmethystStyle.Current);
            if (window.color != tint)
            {
                window.color = tint;
            }
        }
        catch { }
    }

    private static Color WindowTint(AmethystPalette palette)
    {
        Color tint = Color.Lerp(new Color(0.018f, 0.020f, 0.030f, 1f), palette.Panel, 0.20f);
        tint.a = 0.92f;
        return tint;
    }

    internal static SpriteRenderer GetBubbleBackground(ChatBubble bubble)
    {
        if (bubble == null) return null;
        int id = bubble.GetInstanceID();
        if (bubbleBackgrounds.TryGetValue(id, out SpriteRenderer cached) && cached != null)
            return cached;

        Transform background = ((Component)bubble).transform.Find("Background");
        SpriteRenderer renderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
        if (renderer != null) bubbleBackgrounds[id] = renderer;
        return renderer;
    }

    private static Color DarkInputTextColor()
    {
        return Color.Lerp(AmethystStyle.Current.Text, Color.white, 0.10f);
    }

    internal static void HoldFreeChatColors(ChatController chat)
    {
        if (!AmethystConfig.DarkChatTheme.Value)
        {
            return;
        }

        try
        {
            if (chat == null || chat.freeChatField == null)
            {
                return;
            }

            TextBoxTMP textArea = chat.freeChatField.textArea;
            if (textArea == null)
            {
                return;
            }

            Color textColor = DarkInputTextColor();
            SetTextColor(textArea.outputText, textColor);
            SetTextColor(textArea.placeholderText, textColor);
        }
        catch { }
    }

    private static void SetTextColor(TMP_Text text, Color color)
    {
        if (text == null)
        {
            return;
        }

        Graphic graphic = (Graphic)text;
        if (graphic.color != color)
        {
            graphic.color = color;
        }
    }

    internal static void ApplyBubble(ChatBubble bubble)
    {
        if (bubble == null || !AmethystConfig.DarkChatTheme.Value)
        {
            return;
        }

        try
        {
            AmethystPalette palette = AmethystStyle.Current;
            SpriteRenderer background = GetBubbleBackground(bubble);

            if (background != null)
            {
                Color bubbleColor = Color.Lerp(new Color(0.018f, 0.020f, 0.030f, 0.84f), palette.Panel, 0.40f);
                if (background.color != bubbleColor)
                {
                    background.color = bubbleColor;
                }
            }

            if (bubble.MaskArea != null)
            {
                Color behind = WindowTint(palette);
                Color mask = bubble.MaskArea.color;
                behind.a = mask.a;
                if (mask != behind)
                {
                    bubble.MaskArea.color = behind;
                }
            }

            if (bubble.TextArea != null)
            {
                
                ((Graphic)bubble.TextArea).color = Color.white;
            }

            if (bubble.TextArea != null)
            {
                TMP_Text msgText = (TMP_Text)bubble.TextArea;
                msgText.enableWordWrapping = true;
                msgText.overflowMode = TextOverflowModes.Overflow;
            }
        }
        catch (Exception error)
        {
            AmethystPlugin.Logger?.LogWarning((object)$"Dark chat bubble styling failed: {error.Message}");
        }
    }

    private static void ApplyFreeChatField(ChatController chat, Color background, Color textColor, bool isDark)
    {
        try
        {
            if (chat.freeChatField == null)
            {
                return;
            }

            AbstractChatInputField field = (AbstractChatInputField)chat.freeChatField;
            if (field.background != null && field.background.color != background)
            {
                field.background.color = background;
            }

            TextBoxTMP textArea = chat.freeChatField.textArea;
            if (textArea != null)
            {
                
                if (textArea.Background != null && textArea.Background.color != background)
                {
                    textArea.Background.color = background;
                }

                SetTextColor(textArea.outputText, textColor);
                SetTextColor(textArea.placeholderText, textColor);
            }

            if (isDark && !AmethystConfig.BetterChat.Value)
            {
                SetTextColor(chat.freeChatField.charCountText, textColor);
            }

            ApplyInputButton(field.submitButton, background, textColor);
        }
        catch { }
    }

    private static void ApplyQuickChatField(ChatController chat, Color background, Color textColor, bool isDark)
    {
        try
        {
            if (chat.quickChatField == null)
            {
                ApplyQuickChatIcons(chat, isDark);
                return;
            }

            AbstractChatInputField field = (AbstractChatInputField)chat.quickChatField;
            if (field.background != null && field.background.color != background)
            {
                field.background.color = background;
            }

            SetTextColor(chat.quickChatField.text, textColor);
            SetTextColor(chat.quickChatField.placeholderText, textColor);
            
            ApplyInputButton(field.submitButton, background, textColor);
            ApplyInputButton(chat.quickChatField.clearButton, background, textColor);
            ApplyInputButton(chat.quickChatField.undoButton, background, textColor);

            ApplyQuickChatIcons(chat, isDark);
        }
        catch { }
    }

    private static void ApplyInputButton(ChatInputFieldButton button, Color background, Color textColor)
    {
        if (button == null)
        {
            return;
        }

        Color buttonBackground = Color.Lerp(background, textColor, 0.18f);
        CacheButtonSprites(button.backgroundSprites, originalButtonBackgroundColors);
        CacheButtonSprites(button.iconSprites, originalButtonIconColors);
        CacheButtonText(button.text);
        PaintSpriteTint(button.backgroundSprites, buttonBackground);
        PaintSpriteTint(button.iconSprites, textColor);
        SetTextColor(button.text, textColor);
    }

    private static void CacheButtonSprites(Il2CppReferenceArray<SpriteRenderer> sprites, Dictionary<int, Color> cache)
    {
        if (sprites == null) return;
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite != null && !cache.ContainsKey(sprite.GetInstanceID())) cache[sprite.GetInstanceID()] = sprite.color;
        }
    }

    private static void CacheButtonText(TMP_Text text)
    {
        if (text == null) return;
        int id = text.GetInstanceID();
        if (!originalButtonTextColors.ContainsKey(id)) originalButtonTextColors[id] = ((Graphic)text).color;
    }

    private static void RestoreInputButtons(ChatController chat)
    {
        try
        {
            RestoreInputButton(chat.freeChatField != null ? ((AbstractChatInputField)chat.freeChatField).submitButton : null);
            RestoreInputButton(chat.quickChatField != null ? ((AbstractChatInputField)chat.quickChatField).submitButton : null);
            RestoreInputButton(chat.quickChatField != null ? chat.quickChatField.clearButton : null);
            RestoreInputButton(chat.quickChatField != null ? chat.quickChatField.undoButton : null);
        }
        catch { }
    }

    private static void RestoreInputButton(ChatInputFieldButton button)
    {
        if (button == null) return;
        RestoreButtonSprites(button.backgroundSprites, originalButtonBackgroundColors);
        RestoreButtonSprites(button.iconSprites, originalButtonIconColors);
        if (button.text != null && originalButtonTextColors.TryGetValue(button.text.GetInstanceID(), out Color textColor))
            ((Graphic)button.text).color = textColor;
    }

    private static void RestoreButtonSprites(Il2CppReferenceArray<SpriteRenderer> sprites, Dictionary<int, Color> cache)
    {
        if (sprites == null) return;
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite != null && cache.TryGetValue(sprite.GetInstanceID(), out Color color)) sprite.color = color;
        }
    }

    private static void RestoreWindow(ChatController chat)
    {
        try
        {
            SpriteRenderer window = chat != null ? chat.backgroundImage : null;
            if (window != null && originalWindowColors.TryGetValue(window.GetInstanceID(), out Color color)) window.color = color;
        }
        catch { }
    }

    private static void PaintSpriteTint(Il2CppReferenceArray<SpriteRenderer> sprites, Color color)
    {
        if (sprites == null)
        {
            return;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null)
            {
                continue;
            }

            Color tint = new Color(color.r, color.g, color.b, sprite.color.a);
            if (sprite.color != tint)
            {
                sprite.color = tint;
            }
        }
    }

    private static void ApplyQuickChatIcons(ChatController chat, bool isDark)
    {
        if (isDark)
        {
            SetIcon(chat.quickChatButton?.gameObject, "QuickChatIcon", AmethystResources.DarkQuickChat);
            SetIcon(chat.banButton?.MenuButton?.gameObject, "OpenBanMenuIcon", AmethystResources.DarkReport);
            SetIcon(chat.openKeyboardButton?.gameObject, "OpenKeyboardIcon", AmethystResources.DarkKeyboard);
        }
        else
        {
            RestoreIcon(chat.quickChatButton?.gameObject, "QuickChatIcon");
            RestoreIcon(chat.banButton?.MenuButton?.gameObject, "OpenBanMenuIcon");
            RestoreIcon(chat.openKeyboardButton?.gameObject, "OpenKeyboardIcon");
        }
    }

    private static void SetIcon(GameObject root, string name, Sprite sprite)
    {
        SpriteRenderer renderer = FindIcon(root, name);
        if (renderer == null) return;
        int id = renderer.GetInstanceID();
        if (!originalIconStates.ContainsKey(id))
        {
            originalIconStates[id] = new IconState
            {
                Sprite = renderer.sprite,
                Color = renderer.color,
            };
        }
        if (sprite != null)
        {
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.enabled = true;
        }
    }

    private static void RestoreIcon(GameObject root, string name)
    {
        SpriteRenderer renderer = FindIcon(root, name);
        if (renderer == null) return;
        if (originalIconStates.TryGetValue(renderer.GetInstanceID(), out IconState state))
        {
            if (state.Sprite != null) renderer.sprite = state.Sprite;
            renderer.color = state.Color;
            renderer.enabled = true;
        }
    }

    private static SpriteRenderer FindIcon(GameObject root, string name)
    {
        if (root == null) return null;
        SpriteRenderer own = root.GetComponent<SpriteRenderer>();
        if (own != null && root.name == name) return own;
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].gameObject.name == name) return renderers[i];
        return null;
    }
}





[HarmonyPatch(typeof(ChatController), "Awake")]
internal static class ChatPoolPatch
{
    public static void Postfix(ChatController __instance)
    {
        try
        {
            if (__instance == null) return;
            ChatInputPolicy.TuneBubbleCache(__instance);
            ChatThemeStyler.RefreshInputs(__instance, true);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChatController), "Update")]
internal static class ChatRuntimePatch
{
    public static void Postfix(ChatController __instance)
    {
        
        int p = AmethystPerfProbe.Begin(Section.S_ChatMain);
        try
        {
            if (__instance == null) return;
            ChatInputPolicy.ApplyTypingRules(__instance);
            ChatHistoryNavigator.Handle(__instance);
            ChatThemeStyler.RefreshInputs(__instance);
            ChatBubbleCopyHandler.Check(__instance);
            ChatBubbleAnimations.Tick(__instance);
        }
        catch { }
        finally { AmethystPerfProbe.End(p, Section.S_ChatMain); }
    }
}

[HarmonyPatch(typeof(ChatController), "GetPooledBubble")]
internal static class ChatPooledBubbleThemePatch
{
    private static void Postfix(ref ChatBubble __result)
    {
        try
        {
            ChatThemeStyler.ApplyBubble(__result);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChatBubble), "SetText")]
internal static class DarkChatBubblePatch
{
    public static void Postfix(ChatBubble __instance)
    {
        ChatThemeStyler.ApplyBubble(__instance);
    }
}

[HarmonyPatch(typeof(ChatBubble), "SetText")]
[HarmonyPriority(Priority.Last)]
internal static class DarkChatBodyWhitePatch
{
    public static void Postfix(ChatBubble __instance)
    {
        if (!AmethystConfig.DarkChatTheme.Value)
        {
            return;
        }

        if (__instance == null)
        {
            return;
        }

        try
        {
            TextMeshPro textArea = __instance.TextArea;
            if (textArea == null)
            {
                return;
            }

            Graphic graphic = (Graphic)textArea;
            Color color = graphic.color;
            if (color.r == 1f && color.g == 1f && color.b == 1f)
            {
                return;
            }

            color.r = 1f;
            color.g = 1f;
            color.b = 1f;
            graphic.color = color;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(FreeChatInputField), "UpdateCharCount")]
internal static class ChatCounterPatch
{
    public static void Postfix(FreeChatInputField __instance)
    {
        if (AmethystConfig.BetterChat.Value)
        {
            ChatInputPolicy.DrawCount(__instance, 90, 120);
        }
    }
}

[HarmonyPatch(typeof(ChatController), "SendFreeChat")]
internal static class FreeChatSendPatch
{
    public static bool Prefix(ChatController __instance)
    {
        
        if (!AmethystConfig.BetterChat.Value)
        {
            return true;
        }

        if (ChatInputPolicy.CanSendNow())
        {
            return true;
        }

        AmethystToast.Push(
            AmethystText.IsChinese ? "聊天：发送过快，请稍候" : "Chat: too fast, wait a moment",
            2.0f);
        return false;
    }

    public static void Postfix(ChatController __instance)
    {
        ChatThemeStyler.HoldFreeChatColors(__instance);
    }
}

[HarmonyPatch(typeof(TextBoxTMP), "Start")]
internal static class TextBoxRulesPatch
{
    public static void Postfix(TextBoxTMP __instance)
    {
        if (!AmethystConfig.BetterChat.Value)
        {
            return;
        }

        ChatInputPolicy.AllowExtendedInput(__instance, __instance.characterLimit);
    }
}

[HarmonyPatch(typeof(TextBoxTMP), "Update")]
internal static class ClipboardPatch
{
    public static void Postfix(TextBoxTMP __instance)
    {
        ClipboardBridge.Run(__instance);
    }
}

[HarmonyPatch(typeof(PlayerControl), "RpcSendChat")]
internal static class ChatSendHistoryPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] string chatText, bool __result)
    {
        if (!__result || string.IsNullOrWhiteSpace(chatText))
        {
            return;
        }

        if (__instance == null || __instance != PlayerControl.LocalPlayer)
        {
            return;
        }

        ChatState.Remember(chatText);
        
        ChatInputPolicy.NoteSent();
    }
}

[HarmonyPatch(typeof(ChatController), "Update")]
internal static class ChatControllerUpdateHoverFixPatch
{
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(ChatController __instance)
    {
        
        int p = AmethystPerfProbe.Begin(Section.S_ChatAux);
        try
        {
            if (__instance == null) return;
            if (AmethystConfig.DarkChatTheme.Value)
            {
                AmethystPalette palette = AmethystStyle.Current;
                Color background = Color.Lerp(new Color(0.018f, 0.020f, 0.030f, 1f), palette.Panel, 0.46f);
                
                if (__instance.freeChatField != null && __instance.freeChatField.background != null
                    && __instance.freeChatField.background.color != background)
                {
                    __instance.freeChatField.background.color = background;
                }
                if (__instance.quickChatField != null && __instance.quickChatField.background != null
                    && __instance.quickChatField.background.color != background)
                {
                    __instance.quickChatField.background.color = background;
                }

                ChatThemeStyler.HoldFreeChatColors(__instance);

                ChatThemeStyler.ApplyWindow(__instance);
            }
        }
        catch { }
        finally { AmethystPerfProbe.End(p, Section.S_ChatAux); }
    }
}
