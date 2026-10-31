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

internal static class ChatInputPolicy
{
    private const float VanillaSafeChatCooldownSeconds = 3.15f;

    private const float ChatGapSeconds = 3f;
    private const int FreeInRow = 6;
    private const float IdleResetSeconds = 5f;

    private static int sentInRow;
    private static float lastSentAt = -99f;

    internal static bool CanSendNow()
    {
        float since = Time.time - lastSentAt;
        if (since >= IdleResetSeconds) sentInRow = 0;
        if (sentInRow < FreeInRow) return true;
        return since >= ChatGapSeconds;
    }

    internal static void NoteSent()
    {
        if (Time.time - lastSentAt >= IdleResetSeconds) sentInRow = 0;
        sentInRow++;
        lastSentAt = Time.time;
    }

    internal static void TuneBubbleCache(ChatController chat)
    {
        if (chat == null || chat.chatBubblePool == null) return;
        chat.chatBubblePool.poolSize = AmethystConfig.ChatHistory80.Value ? 80 : 20;
        chat.chatBubblePool.ReclaimOldest();
    }

    internal static void ApplyTypingRules(ChatController chat)
    {
        TextBoxTMP input = chat.freeChatField.textArea;

        if (AmethystConfig.BetterChat.Value)
        {
            AllowExtendedInput(input, 120);
        }
    }

    internal static bool CanSendEnhancedChat(ChatController chat, out float waitSeconds)
    {
        waitSeconds = 0f;
        if (chat == null)
        {
            return true;
        }

        if (CanSendNow()) return true;
        waitSeconds = ChatGapSeconds - (Time.time - lastSentAt);
        return waitSeconds <= 0f;
    }

    internal static void AllowExtendedInput(TextBoxTMP input, int limit)
    {
        input.AllowSymbols = true;
        input.AllowEmail = true;
        input.allowAllCharacters = true;
        input.characterLimit = limit;
    }

    internal static void DrawCount(FreeChatInputField field, int warningAt, int dangerAt)
    {
        if (field == null || field.textArea == null || field.charCountText == null)
        {
            return;
        }

        int length = field.textArea.text.Length;
        int limit = field.textArea.characterLimit;
        TMP_Text counter = field.charCountText;
        counter.enableWordWrapping = false;
        counter.overflowMode = TextOverflowModes.Overflow;
        counter.SetText($"{length}/{limit}", true);
        ((Graphic)counter).color = CounterColor(length, warningAt, dangerAt);
    }

    internal static bool TryPrepareOutgoing(ChatController chat, out string message, out bool changed)
    {
        message = string.Empty;
        changed = false;
        if (chat == null || chat.freeChatField == null || chat.freeChatField.textArea == null)
        {
            return false;
        }

        string original = chat.freeChatField.textArea.text ?? string.Empty;
        message = SanitizeOutgoing(original);
        if (!AmethystConfig.BetterChat.Value)
        {
            int limit = Mathf.Max(1, chat.freeChatField.textArea.characterLimit);
            if (message.Length > limit)
            {
                message = message.Substring(0, limit).Trim();
            }
        }

        changed = !string.Equals(original, message, StringComparison.Ordinal);
        return !string.IsNullOrWhiteSpace(message);
    }

    private static Color CounterColor(int currentLength, int warningAt, int dangerAt)
    {
        if (currentLength >= dangerAt)
        {
            return Color.red;
        }

        if (currentLength >= warningAt)
        {
            return new Color(1f, 0.84f, 0.20f, 1f);
        }

        return AmethystConfig.DarkChatTheme.Value ? Color.Lerp(AmethystStyle.Current.Text, Color.white, 0.20f) : Color.black;
    }

    private static string SanitizeOutgoing(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string source = value.Replace("\r", string.Empty).Replace("\0", string.Empty);
        System.Text.StringBuilder builder = new System.Text.StringBuilder(source.Length);
        bool lastWasSpace = false;
        for (int i = 0; i < source.Length; i++)
        {
            char ch = source[i];
            if (char.IsControl(ch) && ch != '\n' && ch != '\t')
            {
                continue;
            }

            if (ch == '\t')
            {
                ch = ' ';
            }

            if (ch == ' ')
            {
                if (lastWasSpace)
                {
                    continue;
                }

                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString().Trim();
    }
}

