using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Amethyst;

internal enum AmethystEventCat { Kill, Meeting, Sabotage, Vent, Role, Report, Join, Other }

public sealed class AmethystEventLog : MonoBehaviour
{
    private sealed class Row { public string Clock; public string Text; public AmethystNotifyKind Kind; public AmethystEventCat Cat; public float H; }

    private const int Cap = 150;
    private const float HeadH = 30f;
    private const float RowH = 18f;

    private static float _lastTextW = -1f;

    private static readonly List<Row> _rows = new List<Row>(Cap);
    private static bool ConsoleOn => AmethystConfig.EventConsole.Value;
    private static bool _stick = true;

    internal static void Add(string text, AmethystNotifyKind kind) => Add(text, kind, AmethystEventCat.Other);

    internal static void Add(string text, AmethystNotifyKind kind, AmethystEventCat cat)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (cat != AmethystEventCat.Other)
        {
            int mask = AmethystConfig.EventFilterMask.Value;
            if ((mask & (1 << (int)cat)) == 0) return;
        }
        while (_rows.Count >= Cap) _rows.RemoveAt(0);
        _rows.Add(new Row { Clock = DateTime.Now.ToString("HH:mm:ss"), Text = text, Kind = kind, Cat = cat });
        _stick = true;
    }

    private Rect _win = new Rect(24f, 130f, 430f, 366f);
    private float _scroll;
    private bool _drag;
    private Vector2 _grab;
    private bool _built;
    private GUIStyle _clock, _row, _tabS;
    private readonly GUIContent _gc = new GUIContent();

    private float _measuredAt;

    [HideFromIl2Cpp]
    private float RowHeight(Row r, float textW)
    {
        if (r.H > 0f) return r.H;
        _gc.text = r.Text;
        r.H = Mathf.Max(RowH, _row.CalcHeight(_gc, textW) + 2f);
        return r.H;
    }

    private void Remeasure(float textW)
    {
        if (Mathf.Abs(textW - _measuredAt) < 0.5f) return;
        _measuredAt = textW;
        
        if (textW != _lastTextW) { _lastTextW = textW; for (int i = 0; i < _rows.Count; i++) _rows[i].H = 0f; }
    }

    [HideFromIl2Cpp]
    internal void DrawGui()
    {
        int __p = AmethystPerfProbe.Begin(Section.S_EventLog);
        try { DrawGuiInner(); }
        finally { AmethystPerfProbe.End(__p, Section.S_EventLog); }
    }

    private void DrawGuiInner()
    {
        if (!ConsoleOn) return;
        Build();

        float sc = Mathf.Clamp(Screen.height / 1080f, 1f, 2.4f);
        Matrix4x4 mtx = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
        try
        {
        float sw = Screen.width / sc, sh = Screen.height / sc;
        Event e = Event.current;
        AmethystStyle.ClampWindow(ref _win, sw, sh, 300f, 150f);

        AmethystPalette p = AmethystStyle.Current;
        float w = _win.width, h = _win.height;

        AmethystStyle.FillRounded(_win, A(p.Window, 0.95f), 12);
        AmethystStyle.StrokeRounded(_win, A(p.Accent, 0.28f), 12, 1);
        AmethystStyle.Fill(new Rect(_win.x + 10f, _win.y + HeadH - 1f, w - 20f, 1f), A(p.Accent, 0.35f));

        AmethystStyle.FillRounded(new Rect(_win.x + 12f, _win.y + 9f, 3f, 12f), p.Accent, 1);
        GUI.Label(new Rect(_win.x + 20f, _win.y + 5f, 120f, 20f), AmethystText.T("EVENTS"), _tabS);

        List<Row> list = _rows;

        _clock.normal.textColor = p.Muted;
        GUI.Label(new Rect(_win.x + w - 150f, _win.y + 7f, 56f, 16f), list.Count + "/" + Cap, _clock);

        Rect cpy = new Rect(_win.x + w - 80f, _win.y + 6f, 22f, 18f);
        Rect clr = new Rect(_win.x + w - 54f, _win.y + 6f, 22f, 18f);
        Rect cls = new Rect(_win.x + w - 28f, _win.y + 6f, 22f, 18f);
        if (Hover(cpy)) AmethystStyle.FillRounded(cpy, A(p.Accent, 0.20f), 6);
        if (Hover(clr)) AmethystStyle.FillRounded(clr, A(Color.white, 0.07f), 6);
        if (Hover(cls)) AmethystStyle.FillRounded(cls, A(new Color(0.9f, 0.3f, 0.3f), 0.25f), 6);
        Icon(cpy, AmethystIcon.Copy, Hover(cpy) ? p.Text : p.Muted);
        Icon(clr, AmethystIcon.Trash, Hover(clr) ? p.Text : p.Muted);
        Icon(cls, AmethystIcon.Close, Hover(cls) ? new Color(0.95f, 0.5f, 0.5f) : p.Muted);
        if (GUI.Button(cpy, GUIContent.none, GUIStyle.none)) CopyAll(list);
        if (GUI.Button(clr, GUIContent.none, GUIStyle.none)) list.Clear();
        if (GUI.Button(cls, GUIContent.none, GUIStyle.none)) { AmethystConfig.EventConsole.Value = false; return; }

        float filterH = 42f;
        string[] fkeys = { "EventFilterKill", "EventFilterMeet", "EventFilterSabo", "EventFilterVent", "EventFilterRole", "EventFilterRep", "EventFilterJoin" };
        int cols = 4;
        float gap = 4f;
        float fw = (w - 12f - (cols - 1) * gap) / cols;
        int mask = AmethystConfig.EventFilterMask.Value;
        for (int i = 0; i < fkeys.Length; i++)
        {
            Rect fr = new Rect(_win.x + 6f + (i % cols) * (fw + gap), _win.y + HeadH + 4f + (i / cols) * 20f, fw, 18f);
            bool fon = (mask & (1 << i)) != 0;
            AmethystStyle.FillRounded(fr, fon ? A(p.Accent, 0.22f) : A(Color.white, 0.05f), 5);
            _tabS.normal.textColor = fon ? p.Accent : A(p.Muted, 0.8f);
            GUI.Label(fr, AmethystText.T(fkeys[i]), _tabS);
            if (GUI.Button(fr, GUIContent.none, GUIStyle.none))
                AmethystConfig.EventFilterMask.Value = mask ^ (1 << i);
        }

        Rect body = new Rect(_win.x + 6f, _win.y + HeadH + 4f + filterH, w - 12f, h - HeadH - 10f - filterH);
        if (e != null && e.type == EventType.ScrollWheel && body.Contains(e.mousePosition))
        {
            _scroll += e.delta.y * 18f;
            _stick = false;
            e.Use();
        }

        float textW = body.width - 66f;
        Remeasure(textW);
        float total = 0f;
        for (int i = 0; i < list.Count; i++) total += RowHeight(list[i], textW);

        float maxScroll = Mathf.Max(0f, total - body.height);
        if (_stick) _scroll = maxScroll;
        _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);
        if (_scroll >= maxScroll - 1f) _stick = true;

        GUI.BeginGroup(body);
        Event ge = Event.current;
        float y = -_scroll;
        for (int i = 0; i < list.Count; i++)
        {
            Row r = list[i];
            float rh = RowHeight(r, textW);
            if (y + rh >= 0f && y <= body.height)
            {
                Rect rr = new Rect(0f, y, body.width, rh);
                bool hov = ge != null && rr.Contains(ge.mousePosition);
                if (hov) AmethystStyle.Fill(rr, A(p.Accent, 0.10f));
                _clock.normal.textColor = A(p.Muted, 0.8f);
                GUI.Label(new Rect(2f, y + 1f, 58f, RowH), r.Clock, _clock);
                _row.normal.textColor = KindColor(r.Kind, p);
                GUI.Label(new Rect(62f, y + 1f, textW, rh), r.Text, _row);
                if (hov && ge.type == EventType.MouseDown && ge.button == 0)
                {
                    Copy(r.Clock + "  " + r.Text);
                    ge.Use();
                }
            }
            y += rh;
        }
        GUI.EndGroup();

        if (maxScroll > 1f && total > 0f)
        {
            float th = Mathf.Max(24f, body.height * (body.height / total));
            float ty = body.y + (body.height - th) * (_scroll / maxScroll);
            AmethystStyle.FillRounded(new Rect(body.xMax - 3f, ty, 3f, th), A(p.Accent, 0.5f), 1);
        }

        Drag(e, new Rect(_win.x + 108f, _win.y, Mathf.Max(0f, w - 108f - 66f), HeadH));
        }
        finally { GUI.matrix = mtx; }
    
    }

    private void Drag(Event e, Rect head)
    {
        if (e == null) return;
        if (e.type == EventType.MouseDown && e.button == 0 && head.Contains(e.mousePosition))
        {
            _drag = true;
            _grab = e.mousePosition - new Vector2(_win.x, _win.y);
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseDrag)
        {
            _win.x = e.mousePosition.x - _grab.x;
            _win.y = e.mousePosition.y - _grab.y;
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseUp) _drag = false;
    }

    private static void CopyAll(List<Row> list)
    {
        if (list.Count == 0) return;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++)
            sb.Append(list[i].Clock).Append("  ").Append(list[i].Text).Append('\n');
        Copy(sb.ToString());
    }

    private static void Copy(string s)
    {
        try { GUIUtility.systemCopyBuffer = s; } catch { }
        AmethystToast.Push(AmethystText.T("Copied"), null, 3.0f, AmethystNotifyKind.Success);
    }

    private static void Icon(Rect r, AmethystIcon icon, Color c) => AmethystIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), c);

    private static bool Hover(Rect r) => Event.current != null && r.Contains(Event.current.mousePosition);

    private static Color KindColor(AmethystNotifyKind k, AmethystPalette p) => k switch
    {
        AmethystNotifyKind.Success => new Color(0.32f, 0.85f, 0.46f),
        AmethystNotifyKind.Danger => new Color(0.93f, 0.36f, 0.36f),
        AmethystNotifyKind.Warning => new Color(0.96f, 0.76f, 0.20f),
        _ => Color.Lerp(p.Text, p.Accent, 0.55f),
    };

    private void Build()
    {
        if (_built) return;
        _built = true;
        _clock = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
        _row = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft };
        _tabS = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        AmethystFont.ApplyTo(_clock);
        AmethystFont.ApplyTo(_row);
        AmethystFont.ApplyTo(_tabS);
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
