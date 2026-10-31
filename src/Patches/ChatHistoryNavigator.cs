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

internal static class ChatHistoryNavigator
{
    internal static void Handle(ChatController chat)
    {
        if (ChatState.Count == 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveBack(chat);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveForward(chat);
        }
    }

    private static void MoveBack(ChatController chat)
    {
        if (!ChatState.BrowsingHistory)
        {
            ChatState.DraftBeforeHistory = chat.freeChatField.textArea.text;
            ChatState.BrowsingHistory = true;
        }

        if (ChatState.HistoryIndex <= 0)
        {
            SoundManager.Instance.PlaySound(chat.warningSound, loop: false, 1f, (AudioMixerGroup)null);
            return;
        }

        ChatState.HistoryIndex = Mathf.Clamp(ChatState.HistoryIndex - 1, 0, ChatState.Count - 1);
        UseText(chat, ChatState.At(ChatState.HistoryIndex));
    }

    private static void MoveForward(ChatController chat)
    {
        ChatState.HistoryIndex += 1;
        if (ChatState.HistoryIndex < ChatState.Count)
        {
            UseText(chat, ChatState.At(ChatState.HistoryIndex));
            return;
        }

        UseText(chat, ChatState.DraftBeforeHistory);
        ChatState.BrowsingHistory = false;
    }

    private static void UseText(ChatController chat, string value)
    {
        chat.freeChatField.textArea.SetText(value, string.Empty);
    }
}

