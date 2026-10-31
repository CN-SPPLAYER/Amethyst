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

internal static class ChatBubbleCopyHandler
{
    private const float DoubleClickWindow = 0.38f;

    private static float lastClickAt = -10f;
    private static string lastClickText = string.Empty;
    private static float lastCopyAt = -10f;
    private static string lastCopiedText;

    internal static void Check(ChatController chat)
    {
        if (!AmethystConfig.BetterChat.Value || chat == null) return;
        bool right = Input.GetMouseButtonDown(1);
        if (!Input.GetMouseButtonDown(0) && !right) return;

        try
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector2 mouseScreen = Input.mousePosition;

            ChatBubble[] bubbles = ((Component)chat).GetComponentsInChildren<ChatBubble>(false);
            for (int i = bubbles.Length - 1; i >= 0; i--)
            {
                ChatBubble bubble = bubbles[i];
                if (bubble == null) continue;
                if (!HitsBubble(bubble, cam, mouseScreen)) continue;

                string text = right ? ReadName(bubble) : ReadText(bubble);
                if (string.IsNullOrEmpty(text)) continue;

                string key = (right ? "n|" : "m|") + text;
                float now = Time.unscaledTime;
                bool isDoubleClick = key == lastClickText && now - lastClickAt <= DoubleClickWindow;

                lastClickAt = now;
                lastClickText = key;

                if (!isDoubleClick) return;

                if (key == lastCopiedText && now - lastCopyAt < 2f) return;

                GUIUtility.systemCopyBuffer = text;
                lastCopyAt = now;
                lastCopiedText = key;
                lastClickAt = -10f;
                AmethystToast.Push(right ? AmethystText.T("NickCopied") : AmethystText.T("Copied"), 3.0f);
                return;
            }
        }
        catch { }
    }

    private static bool HitsBubble(ChatBubble bubble, Camera cam, Vector2 mouseScreen)
    {
        try
        {
            SpriteRenderer sr = ChatThemeStyler.GetBubbleBackground(bubble);
            if (sr == null) return false;
            Bounds b = sr.bounds;
            if (b.size.sqrMagnitude < 0.001f) return false;
            Vector3 smin = cam.WorldToScreenPoint(b.min);
            Vector3 smax = cam.WorldToScreenPoint(b.max);
            float x0 = Mathf.Min(smin.x, smax.x);
            float x1 = Mathf.Max(smin.x, smax.x);
            float y0 = Mathf.Min(smin.y, smax.y);
            float y1 = Mathf.Max(smin.y, smax.y);
            return mouseScreen.x >= x0 && mouseScreen.x <= x1 && mouseScreen.y >= y0 && mouseScreen.y <= y1;
        }
        catch { return false; }
    }

    private static string ReadText(ChatBubble bubble)
    {
        try
        {
            if (bubble.TextArea == null) return string.Empty;
            string raw = ((TMP_Text)bubble.TextArea).text ?? string.Empty;
            int cut = raw.IndexOf("\n<align", StringComparison.OrdinalIgnoreCase);
            if (cut >= 0) raw = raw.Substring(0, cut);
            return raw.Trim();
        }
        catch { return string.Empty; }
    }

    private static string ReadName(ChatBubble bubble)
    {
        try
        {
            if (bubble.NameText == null) return string.Empty;
            string raw = ((TMP_Text)bubble.NameText).text ?? string.Empty;
            int cut = raw.IndexOf("<size=", StringComparison.OrdinalIgnoreCase);
            if (cut > 0) raw = raw.Substring(0, cut);
            return AmethystNameColor.Strip(raw).Trim();
        }
        catch { return string.Empty; }
    }
}

internal static class ClipboardBridge
{
    private static bool _selectedAll;
    private static int _lastFrame = -1;

    internal static void Run(TextBoxTMP box)
    {
        if (!AmethystConfig.BetterChat.Value || box == null || !box.hasFocus) return;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool copy = ctrl && (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Insert));
        bool paste = (ctrl && Input.GetKeyDown(KeyCode.V)) || (shift && Input.GetKeyDown(KeyCode.Insert));
        bool cut = ctrl && Input.GetKeyDown(KeyCode.X);
        bool selectAll = ctrl && Input.GetKeyDown(KeyCode.A);
        if ((!copy && !paste && !cut && !selectAll) || _lastFrame == Time.frameCount) return;
        _lastFrame = Time.frameCount;

        if (selectAll)
        {
            _selectedAll = true;
        }
        else if (copy)
        {
            GUIUtility.systemCopyBuffer = box.text ?? string.Empty;
        }
        else if (paste)
        {
            string clip = GUIUtility.systemCopyBuffer;
            if (!string.IsNullOrEmpty(clip))
            {
                string cur = box.text ?? string.Empty;
                string result;
                if (_selectedAll) { result = clip; _selectedAll = false; }
#if ANDROID
                
                else { result = cur + clip; }
#else
                else { int at = Mathf.Clamp(box.caretPos, 0, cur.Length); result = cur.Insert(at, clip); }
#endif
                box.SetText(result, string.Empty);
            }
        }
        else if (cut)
        {
            GUIUtility.systemCopyBuffer = box.text ?? string.Empty;
            box.SetText(string.Empty, string.Empty);
            _selectedAll = false;
        }
    }

}

