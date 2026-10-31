using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Amethyst;

public enum AmethystNotifyKind { Info, Success, Warning, Danger }

public sealed class AmethystToast : MonoBehaviour
{
    private sealed class Entry
    {
        internal string Title;
        internal string Detail;
        internal float CreatedAt;
        internal float Duration;
        internal AmethystNotifyKind Kind;
        internal int Count;
    }

    private const int MaxEntries = 4;
    private const float Width = 360f;
    private const float Top = 84f;
    private const float Gap = 8f;
    private const float SlideInDistance = 56f;
    private const float SlideInSeconds = 0.30f;

    private static readonly List<Entry> Entries = new List<Entry>(MaxEntries);
    private static string _lastVanillaText;
    private static float _lastVanillaAt;
    private static bool _layerRaised;

    internal static bool AllowVanillaNotify;

    private static void EnsureNotifierLayer(NotificationPopper notifier)
    {
        if (_layerRaised || notifier == null) return;
        try
        {
            Canvas canvas = notifier.GetComponent<Canvas>() ?? notifier.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32000;
            }
            _layerRaised = true;
        }
        catch { }
    }

    private GUIStyle _title;
    private GUIStyle _detail;

    internal static void Push(string text, float life = 3.0f)
    {
        Push(text, null, life, AmethystNotifyKind.Info);
    }

    internal static void Push(string title, string detail, float life, AmethystNotifyKind kind)
    {
        if (!AmethystConfig.Toasts.Value) return;

        string t = Clean(title, 120);
        string d = Clean(detail, 160);
        if (string.IsNullOrEmpty(t) && string.IsNullOrEmpty(d)) return;

        while (Entries.Count >= MaxEntries) Entries.RemoveAt(0);

        float now = Time.unscaledTime;
        for (int i = 0; i < Entries.Count; i++)
        {
            Entry ex = Entries[i];
            if (ex == null || ex.Title != t || ex.Detail != d) continue;
            if (now - ex.CreatedAt > ex.Duration) continue;
            ex.CreatedAt = now;
            ex.Duration = Mathf.Clamp(life, 1.25f, 15f);
            ex.Count++;
            return;
        }

        Entries.Add(new Entry
        {
            Title = t,
            Detail = d,
            CreatedAt = now,
            Duration = Mathf.Clamp(life, 1.25f, 15f),
            Kind = kind,
            Count = 1,
        });
    }

    internal static void PushVanilla(string title, string detail, AmethystNotifyKind kind = AmethystNotifyKind.Info)
    {
        string t = Clean(title, 120);
        string d = Clean(detail, 160);
        if (string.IsNullOrEmpty(t) && string.IsNullOrEmpty(d)) return;
        if (!AmethystConfig.Toasts.Value) return;

        string text = string.IsNullOrEmpty(d) ? t : (string.IsNullOrEmpty(t) ? d : t + ": " + d);
        if (TryVanillaNotify(text)) return;

        Push(t, d, 3.6f, kind);
    }

    private static bool TryVanillaNotify(string text)
    {
        try
        {
            float now = Time.unscaledTime;
            if (text == _lastVanillaText && now - _lastVanillaAt < 1.5f) return true;
            HudManager hud = HudManager.Instance;
            if (hud == null || hud.Notifier == null) return false;
            EnsureNotifierLayer(hud.Notifier);
            _lastVanillaText = text;
            _lastVanillaAt = now;
            AllowVanillaNotify = true;
            try { hud.Notifier.AddDisconnectMessage(text); }
            finally { AllowVanillaNotify = false; }
            return true;
        }
        catch { return false; }
    }

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_Toast);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_Toast); }
    }

    public void UpdateInner()
    {
            float now = Time.unscaledTime;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i] == null || now - Entries[i].CreatedAt > Entries[i].Duration)
                    Entries.RemoveAt(i);
            }
    
    }

    [HideFromIl2Cpp]
    internal void DrawGui()
    {
        if (Entries.Count == 0) return;   
        int __p = AmethystPerfProbe.Begin(Section.S_Toast);
        try { DrawGuiInner(); }
        finally { AmethystPerfProbe.End(__p, Section.S_Toast); }
    }

    private void DrawGuiInner()
    {
        EnsureStyles();
        if (_title == null) return;   

        int prevDepth = GUI.depth;
        Color prevColor = GUI.color;
        Color prevContent = GUI.contentColor;
        GUI.depth = -12000;
        float now = Time.unscaledTime;
        float width = Mathf.Min(Width, Screen.width - 24f);
        float x = (Screen.width - width) * 0.5f;
        float y = Top;

        try
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry e = Entries[i];
                if (e == null) continue;

                float age = now - e.CreatedAt;
                float alpha = FadeAlpha(age, e.Duration);
                float height = string.IsNullOrEmpty(e.Detail) ? 58f : 74f;

                float slideT = EaseOutCubic(age / SlideInSeconds);
                float slideY = y - (1f - slideT) * SlideInDistance;
                float scale = Mathf.Lerp(0.96f, 1f, slideT);
                float sw = width * scale;
                float sh = height * scale;
                float dx = x + (width - sw) * 0.5f;
                float dy = slideY + (height - sh) * 0.5f;

                DrawCard(new Rect(dx, dy, sw, sh), e, alpha, now);
                y += height + Gap;
            }
        }
        finally
        {
            GUI.color = prevColor;
            GUI.contentColor = prevContent;
            GUI.depth = prevDepth;
        }
    }

    [HideFromIl2Cpp]
    private void DrawCard(Rect rect, Entry e, float alpha, float now)
    {
        AmethystPalette p = AmethystStyle.Current;
        const int r = 12;

        Color bar = e.Kind switch
        {
            AmethystNotifyKind.Success => new Color(0.22f, 0.82f, 0.38f),
            AmethystNotifyKind.Danger => new Color(0.90f, 0.28f, 0.28f),
            AmethystNotifyKind.Warning => new Color(0.95f, 0.72f, 0.10f),
            _ => p.Accent,
        };

        Color card = p.Panel;
        card.a = 0.98f * alpha;
        AmethystStyle.FillRounded(rect, card, r);
        AmethystStyle.StrokeRounded(rect, A(Color.white, 0.08f * alpha), r, 1);

        AmethystStyle.FillRounded(
            new Rect(rect.x + 1f, rect.y + 10f, 3f, Mathf.Max(4f, rect.height - 20f)),
            A(bar, 0.90f * alpha), 2);

        GUI.color = Color.white;
        GUI.contentColor = Color.white;

        float textX = rect.x + 18f;
        float textW = Mathf.Max(16f, rect.xMax - 40f - textX);
        float titleY = rect.y + (string.IsNullOrEmpty(e.Detail) ? 14f : 9f);

        _title.normal.textColor = new Color(1f, 1f, 1f, alpha);
        string title = e.Count > 1 ? e.Title + " ×" + e.Count : e.Title;
        GUI.Label(new Rect(textX, titleY, textW, 22f), title, _title);

        if (!string.IsNullOrEmpty(e.Detail))
        {
            _detail.normal.textColor = A(Color.Lerp(p.Muted, Color.white, 0.70f), alpha);
            GUI.Label(new Rect(textX, rect.y + 34f, textW, 20f), e.Detail, _detail);
        }

        float trackX = rect.x + 14f;
        float trackY = rect.yMax - 8f;
        float trackW = rect.width - 28f;
        AmethystStyle.FillRounded(new Rect(trackX, trackY, trackW, 2f), A(Color.white, 0.10f * alpha), 1);
        float fillW = trackW * (1f - Mathf.Clamp01((now - e.CreatedAt) / e.Duration));
        if (fillW > 1f)
            AmethystStyle.FillRounded(new Rect(trackX, trackY, fillW, 2f), A(bar, 0.95f * alpha), 1);
    }

    private static string StripTags(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
        try
        {
            var sb = new System.Text.StringBuilder(s.Length);
            int at = 0;
            while (at < s.Length)
            {
                int open = s.IndexOf('<', at);
                if (open < 0) { sb.Append(s, at, s.Length - at); break; }
                if (open > at) sb.Append(s, at, open - at);
                int close = s.IndexOf('>', open);
                if (close < 0) break;
                at = close + 1;
            }
            return sb.ToString();
        }
        catch { return s; }
    }

    private void EnsureStyles()
    {
        if (_title != null && _detail != null) return;

        try
        {
            GUIStyle baseLabel = null;
            try { if (GUI.skin != null) baseLabel = GUI.skin.label; } catch { }
            if (baseLabel == null) baseLabel = new GUIStyle();

            _title = new GUIStyle(baseLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = false,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft,
            };
            AmethystFont.ApplyTo(_title);

            _detail = new GUIStyle(baseLabel)
            {
                fontSize = 13,
                richText = true,
                wordWrap = false,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft,
            };
            AmethystFont.ApplyTo(_detail);
        }
        catch
        {
            _title = null;
            _detail = null;
        }
    }

    private static float FadeAlpha(float age, float duration)
    {
        float fadeIn = Mathf.Clamp01(age / 0.22f);
        float fadeOut = Mathf.Clamp01((duration - age) / 0.45f);
        return Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static string Clean(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string s = value.Replace("\r", " ").Replace("\n", " ").Trim();

        s = StripTags(s);
        if (string.IsNullOrEmpty(s)) return string.Empty;

        if (s.Length <= max) return s;
        int cut = Math.Max(0, max - 1);
        if (cut > 0 && char.IsHighSurrogate(s[cut - 1])) cut--; 
        return s.Substring(0, cut) + "…";
    }
}
