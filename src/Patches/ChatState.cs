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

internal static class ChatState
{
    private static readonly List<string> sentMessages = new List<string>();
    internal static int HistoryIndex = -1;
    internal static string DraftBeforeHistory = "";
    internal static bool BrowsingHistory;

    internal static int Count => sentMessages.Count;

    internal static void Remember(string message)
    {
        bool isNewEntry = sentMessages.Count == 0 || sentMessages[sentMessages.Count - 1] != message;
        if (isNewEntry)
        {
            sentMessages.Add(message);
        }

        HistoryIndex = sentMessages.Count;
    }

    internal static string At(int index) => sentMessages[index];
}

