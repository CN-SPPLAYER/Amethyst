using System;
using System.Collections.Generic;
using HarmonyLib;
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

internal static class ChatInputPolicy
{
    private const float VanillaSafeChatCooldownSeconds = 3.15f;

    private const float ChatGapSeconds = 3f;
    private const int FreeInRow = 6;
    private const float IdleResetSeconds = 5f;

    private static int sentInRow;
    private static float lastSentAt = -99f;

    private static bool CanSendNow()
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
        }

        lastEnabled = enabled;
        lastChatId = chatId;
        lastThemeId = themeId;
        nextInputRefreshAt = Time.unscaledTime + 0.75f;

        if (enabled)
        {
            AmethystPalette palette = AmethystStyle.Current;
            Color background = Color.Lerp(new Color(0.018f, 0.020f, 0.030f, 1f), palette.Panel, 0.46f);
            Color textColor = Color.Lerp(palette.Text, Color.white, 0.10f);
            ApplyFreeChatField(chat, background, textColor);
            ApplyQuickChatField(chat, background, textColor, true);
        }
        else
        {
            // Reset to default vanilla colors (white background, dark grey text)
            Color background = Color.white;
            Color textColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            ApplyFreeChatField(chat, background, textColor);
            ApplyQuickChatField(chat, background, textColor, false);
            MatchBanMenuVisibilityPatch.RestoreAll();
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
            Color bubbleColor = Color.Lerp(new Color(0.018f, 0.020f, 0.030f, 0.84f), palette.Panel, 0.40f);
            Transform background = ((Component)bubble).transform.Find("Background");
            if (background != null)
            {
                SpriteRenderer renderer = ((Component)background).GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = bubbleColor;
                }
            }

            if (bubble.TextArea != null)
            {
                ((Graphic)bubble.TextArea).color = Color.Lerp(palette.Text, Color.white, 0.12f);
            }

            if (bubble.NameText != null)
            {
                // Leave the author name color to the game.
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

    private static void ApplyFreeChatField(ChatController chat, Color background, Color textColor)
    {
        try
        {
            if (chat.freeChatField == null)
            {
                return;
            }

            AbstractChatInputField field = (AbstractChatInputField)chat.freeChatField;
            if (field.background != null)
            {
                field.background.color = background;
            }

            TextBoxTMP textArea = chat.freeChatField.textArea;
            if (textArea != null && textArea.outputText != null)
            {
                ((Graphic)textArea.outputText).color = textColor;
            }
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
            if (field.background != null)
            {
                field.background.color = background;
            }

            if (chat.quickChatField.text != null)
            {
                ((Graphic)chat.quickChatField.text).color = textColor;
            }

            ApplyQuickChatIcons(chat, isDark);
        }
        catch { }
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
            Transform bg = ((Component)bubble).transform.Find("Background");
            if (bg == null) return false;
            SpriteRenderer sr = ((Component)bg).GetComponent<SpriteRenderer>();
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
                // 安卓版 TextBoxTMP 没有公开的 caretPos（v19a），无法取到光标位置；
                // 退化为末尾追加。选中全部时仍走上面的整段替换。
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

internal static class ChatBubbleAnimations
{
    private const float Duration = 1.15f;

    private class Entry
    {
        public float StartTime;
        public Transform Root;
        public Vector3 TargetScale;
        public SpriteRenderer Bg;
        public Color BgColor;
        public TMP_Text NameText;
        public Color NameColor;
        public TMP_Text MsgText;
        public Color MsgColor;
    }

    private static readonly Dictionary<int, Entry> _active = new Dictionary<int, Entry>();

    internal static void Register(ChatBubble bubble)
    {
        if (!AmethystConfig.BetterChat.Value) return;
        try
        {
            Transform root = ((Component)bubble).transform;

            SpriteRenderer bg = null;
            Transform bgT = root.Find("Background");
            if (bgT != null) bg = ((Component)bgT).GetComponent<SpriteRenderer>();

            TMP_Text nameText = bubble.NameText != null ? (TMP_Text)bubble.NameText : null;
            TMP_Text msgText = bubble.TextArea != null ? (TMP_Text)bubble.TextArea : null;

            var entry = new Entry
            {
                StartTime = Time.unscaledTime,
                Root = root,
                TargetScale = root.localScale,
                Bg = bg,
                BgColor = bg != null ? bg.color : Color.white,
                NameText = nameText,
                NameColor = nameText != null ? nameText.color : Color.white,
                MsgText = msgText,
                MsgColor = msgText != null ? msgText.color : Color.white,
            };

            _active[((Component)bubble).gameObject.GetInstanceID()] = entry;
            ApplyEntry(entry, 0f);
        }
        catch { }
    }

    private static readonly List<int> _done = new List<int>();

    internal static void Tick()
    {
        if (_active.Count == 0) return;
        float now = Time.unscaledTime;
        _done.Clear();

        foreach (var kvp in _active)
        {
            Entry e = kvp.Value;
            float t = Mathf.Clamp01((now - e.StartTime) / Duration);
            try { ApplyEntry(e, t); }
            catch { _done.Add(kvp.Key); continue; }
            if (t >= 1f) _done.Add(kvp.Key);
        }

        for (int i = 0; i < _done.Count; i++) _active.Remove(_done[i]);
    }

    private static float EaseAlpha(float t) => 1f - Mathf.Pow(1f - t, 3f);

    private static float EaseScale(float t)
    {
        float delayed = Mathf.Clamp01((t - 0.04f) / 0.96f);
        return 1f - Mathf.Pow(1f - delayed, 8f);
    }

    private static void ApplyEntry(Entry e, float t)
    {
        float alpha = EaseAlpha(t);
        float scale = EaseScale(t);
        float s = Mathf.Lerp(0.92f, 1f, scale);
        e.Root.localScale = new Vector3(e.TargetScale.x * s, e.TargetScale.y * s, e.TargetScale.z);

        if (e.Bg != null)
        {
            Color c = e.BgColor; c.a = e.BgColor.a * alpha;
            e.Bg.color = c;
        }
        if (e.NameText != null)
        {
            Color c = e.NameColor; c.a = e.NameColor.a * alpha;
            e.NameText.color = c;
        }
        if (e.MsgText != null)
        {
            Color c = e.MsgColor; c.a = e.MsgColor.a * alpha;
            e.MsgText.color = c;
        }
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
        // 第 199 轮：加探针。`ChatController.Update` 是**每帧**入口，
        // 而本补丁是四个同类补丁里最重的一个（五段调用）。
        // 用户反馈"打开聊天后更卡"，此前却完全没有这一段的归因数据，
        // 只能靠读代码猜 —— 这一轮改成先量再改。
        int p = AmethystPerfProbe.Begin(Section.S_ChatMain);
        try
        {
            if (__instance == null) return;
            ChatInputPolicy.ApplyTypingRules(__instance);
            ChatHistoryNavigator.Handle(__instance);
            ChatThemeStyler.RefreshInputs(__instance);
            ChatBubbleCopyHandler.Check(__instance);
            ChatBubbleAnimations.Tick();
        }
        catch { }
        finally { AmethystPerfProbe.End(p, Section.S_ChatMain); }
    }
}

[HarmonyPatch(typeof(ChatBubble), "SetText")]
internal static class DarkChatBubblePatch
{
    public static void Postfix(ChatBubble __instance)
    {
        ChatThemeStyler.ApplyBubble(__instance);
        ChatBubbleAnimations.Register(__instance);
    }
}

[HarmonyPatch(typeof(ChatBubble), "SetText")]
internal static class ChatBubbleTextGuardPatch
{
    public static void Prefix([HarmonyArgument(0)] ref string chatText)
    {
        if (string.IsNullOrEmpty(chatText)) return;
    }
}

[HarmonyPatch(typeof(FreeChatInputField), "UpdateCharCount")]
internal static class ChatCounterPatch
{
    public static void Postfix(FreeChatInputField __instance)
    {
        if (AmethystConfig.BetterChat.Value)
        {
            ChatInputPolicy.DrawCount(__instance, int.MaxValue, int.MaxValue);
            return;
        }

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
        return true;
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

internal static class ChatBubbleSenderDecorator
{
    internal static void Apply(ChatBubble bubble)
    {
        // Keep the chat author name untouched so the game renders it natively.
        return;
    }

    private static string TrimRawPlatformName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        int pipe = raw.IndexOf('|');
        if (pipe > 0) raw = raw.Substring(0, pipe).TrimEnd();

        int authorIdx = FindAuthorSeparator(raw);
        if (authorIdx > 0) raw = raw.Substring(0, authorIdx).TrimEnd();

        if (raw.Length > 24) raw = raw.Substring(0, 24).TrimEnd() + "…";
        return raw;
    }

    private static int FindAuthorSeparator(string raw)
    {
        string[] separators = { " by ", " от ", " - ", " — ", " – ", " :: " };
        int earliest = -1;
        for (int i = 0; i < separators.Length; i++)
        {
            int idx = raw.IndexOf(separators[i], StringComparison.OrdinalIgnoreCase);
            if (idx > 0 && (earliest < 0 || idx < earliest))
                earliest = idx;
        }
        return earliest;
    }

    private static string BubblePlatformLabel(Platforms platform)
    {
        return (int)platform switch
        {
            1 => "Epic",
            2 => "Steam",
            3 => "Mac",
            4 => "MS Store",
            5 => "Itch.io",
            6 => "iOS",
            7 => "Android",
            8 => "Switch",
            9 => "Xbox",
            10 => "PS",
            _ => platform.ToString(),
        };
    }
}

[HarmonyPatch(typeof(ChatBubble), "SetName")]
internal static class ChatBubbleSenderInfoPatch
{
    public static void Postfix(ChatBubble __instance)
    {
        ChatBubbleSenderDecorator.Apply(__instance);
    }
}

// ===== 强制背景色，防止悬停或移开时变回原色 =====
[HarmonyPatch(typeof(ChatController), "Update")]
internal static class ChatControllerUpdateHoverFixPatch
{
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(ChatController __instance)
    {
        // 第 199 轮：与其余三个 ChatController.Update 补丁合并计量（见 ChatRuntimePatch 的说明）。
        int p = AmethystPerfProbe.Begin(Section.S_ChatAux);
        try
        {
            if (__instance == null) return;
            if (AmethystConfig.DarkChatTheme.Value)
            {
                AmethystPalette palette = AmethystStyle.Current;
                Color background = Color.Lerp(new Color(0.018f, 0.020f, 0.030f, 1f), palette.Panel, 0.46f);
                // ⚠ 性能（第 169 轮）：本补丁挂在 `ChatController.Update` 上（每帧）。
                //   原来每帧都无条件写两次背景色 —— `Graphic.color` 的 setter 会走
                //   `SetVerticesDirty`，即使颜色一模一样也会把 UI 标脏、触发重建。
                //   这里改成「和当前值不同才写」：既保留了「悬停/移开后强制改回」的语义
                //   （游戏改动了我们仍然会纠正回来），又不再每帧白白弄脏 UI。
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
            }
        }
        catch { }
        finally { AmethystPerfProbe.End(p, Section.S_ChatAux); }
    }
}
