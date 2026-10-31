using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using Amethyst.Patches;
using UnityEngine;

namespace Amethyst;

public sealed partial class AmethystMenu : MonoBehaviour
{
    private const int WindowId = 770731;
    private const float FullH = 610f;
    private const float MinW = 620f;
    private const float HeaderH = 64f;
    private const float SidebarW = 200f;
    private const float TabH = 44f;
    private const float RowH = 34f;
    private const float FooterH = 40f;

    private struct TabDef
    {
        public AmethystIcon Icon;
        public string Key;
        public TabDef(AmethystIcon icon, string key) { Icon = icon; Key = key; }
        public string Name => AmethystText.T(Key);
    }

    private static readonly TabDef[] Tabs =
    {
        new TabDef(AmethystIcon.Home, "TabHome"),
        new TabDef(AmethystIcon.Shield, "TabGuard"),
        new TabDef(AmethystIcon.Tune, "TabDev"),
        new TabDef(AmethystIcon.Star, "TabOther"),
        new TabDef(AmethystIcon.Rooms, "TabRooms"),
        new TabDef(AmethystIcon.People, "TabCredits"),
    };

    internal static bool Opened;
    internal static bool Typing;
    internal static bool ToggleRequest;

    private bool _open;
    private bool _collapsed;
    private int _tab;
    private int _slider;
    private float _menuScaleDraft;
    private bool _menuScaleDraftActive;
    private float _scroll;
    private float _scrollPending;
    private float _scrollGoal;
    private int _scrollFrame = -1;
    private float _maxScroll;
    private float _viewH;
    private bool _scrollGrab;
    private float _scrollLastY;
    private bool _scrollMoved;
    private float _h = FullH;
    private int _resize;
    
    private Rect _window = new Rect(300f, 110f, 940f, FullH);
    private bool _mobileCentered;

    private bool _dragging;
    private Vector2 _dragGrab;

    private bool _built;
    private GUIStyle _windowBg;
    private GUI.WindowFunction _winFn;
    private GUIStyle _brand;
    private GUIStyle _verPill;
    private GUIStyle _tabStyle;
    private GUIStyle _sectionTitle;
    private GUIStyle _cardTitle;
    private GUIStyle _rowLabel;
    private GUIStyle _rowLabelLine;
    private GUIStyle _value;
    private GUIStyle _valueC;
    private GUIStyle _muted;
    private GUIStyle _footer;
    private GUIStyle _btnLabel;
    private GUIStyle _centerMuted;
    private GUIStyle _arrow;
    private GUIStyle _subTabStyle;
    private GUIStyle _smallBtn;
    private GUIStyle _rowName;
    private GUIStyle _rowInfo;
    private GUIStyle _invisible;
    private GUIStyle _keyBadge;
    private GUIStyle _star;

    private string _textFocus;
    private bool _wasTyping;

    private bool _rebinding;
    private ConfigEntry<KeyCode> _rebindTarget;
    internal static bool Rebinding;
    private bool _menuScaleDragging;

    private float _closeAt = -1f;
    private float _fade = 1f;

    private const float OpenK = 260f, OpenC = 17.5f;
    private const float CloseK = 620f, CloseC = 52.3f;
    private float _ax;
    private float _av;
    private int _axFrame = -1;
    private int _press;
    private float _pressAt;

    private float _openAt = -1f;
    private float _enterAge = 99f;
    
    private int _openDraws;
    
    private float _enterClock;
    private float _tabClock;
    private float _titleClock;
    private int _clockFrame = -1;
    private bool _prewarmed;
    private string _titleNow = string.Empty;
    private string _titleOld;
    private float _titleAt = -10f;

    private float _tabAnimAt = -1f;
    
    private float _tabLag;
    private float _cascade = -100f;
    private int _cardIdx;
    private Color _cardOuter = Color.white;
    private bool _cardTintOn;
    
    private readonly int[] _subTab = new int[Tabs.Length + 1];
    private int _cardGrp = -1;
    private Rect _tabHi;
    private bool _tabHiInit;
    private readonly Dictionary<object, float> _pillAnim = new Dictionary<object, float>();
    private readonly Dictionary<object, float> _pillVel = new Dictionary<object, float>();

    private readonly Rect[] _subHi = new Rect[Tabs.Length + 1];
    private readonly bool[] _subHiInit = new bool[Tabs.Length + 1];

    private Font _lastBuiltFont;

    private static Vector2 M => Event.current != null ? Event.current.mousePosition : new Vector2(-1f, -1f);

    private static int _mobile = -1;
    private static bool Mobile
    {
        get
        {
            if (_mobile < 0) { try { _mobile = Application.isMobilePlatform ? 1 : 0; } catch { _mobile = 0; } }
            return _mobile == 1;
        }
    }

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_Menu);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_Menu); }
    }

    public void UpdateInner()
    {
            bool menuKey = AmethystConfig.MenuKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.MenuKey.Value);
            if (!_rebinding && menuKey)
            {
                if (_open && _closeAt < 0f) RequestClose();
                else Open();
            }
            if (ToggleRequest)
            {
                ToggleRequest = false;
                if (!_rebinding) { if (_open && _closeAt < 0f) RequestClose(); else Open(); }
            }
            if (!_rebinding && _open && _closeAt < 0f && Input.GetKeyDown(KeyCode.Escape))
                RequestClose();
            Opened = _open;

            bool typing = _open && _closeAt < 0f && _textFocus != null;
            Typing = typing;
            if (typing) SetMoveable(false);
            else if (_wasTyping) SetMoveable(true);
            _wasTyping = typing;
    
    }







    private static float SmoothSat(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    private float Ent(float delay, float len)
    {
        if (_enterAge > 50f) return 1f;
        float i = 1f - Mathf.Clamp01((_enterAge - delay) / len);
        return 1f - i * i * i;
    }

    private static int RectId(Rect r) => (Mathf.RoundToInt(r.x) * 73856093) ^ (Mathf.RoundToInt(r.y) * 19349663) ^ (Mathf.RoundToInt(r.width) * 83492791);

    private float PressK(int id)
    {
        if (_press != id) return 1f;
        float t = (Time.unscaledTime - _pressAt) / 0.13f;
        if (t >= 1f) { _press = 0; return 1f; }
        return Mathf.Lerp(0.97f, 1f, SmoothSat(t));
    }

    private void Pressed(Rect r, int id)
    {
        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown && r.Contains(e.mousePosition))
        {
            _press = id;
            _pressAt = Time.unscaledTime;
        }
    }

    private static void SetMoveable(bool value)
    {
        try { if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = value; }
        catch { }
    }

    [HideFromIl2Cpp]
    internal void DrawGui()
    {
        int __p = AmethystPerfProbe.Begin(Section.S_Menu);
        try { DrawGuiInner(); }
        finally { AmethystPerfProbe.End(__p, Section.S_Menu); }
    }

    private void DrawGuiInner()
    {
        SyncRoomListVisibility();
        if (!_open)
        {
            
            PrewarmIfNeeded();
            return;
        }
        Build();
        HandleRebindCapture();

        float baseS = Mobile
            ? Mathf.Clamp(Screen.height / 850f, 0.8f, 2.4f)
            : Mathf.Clamp(Screen.height / 1080f, 0.72f, 2.1f);
        float userS = AmethystConfig.MenuScale.Value;
        float s = Mathf.Clamp(baseS * userS, 0.5f, 3.2f);
        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float vw = Screen.width / s;
        float vh = Screen.height / s;

        float now = Time.unscaledTime;
        
        if (_openAt >= 0f && _openDraws == 0)
        {
            _openAt = now;
            _titleAt = now;
            _tabAnimAt = now;
        }
        _openDraws++;

        if (_clockFrame != Time.frameCount)
        {
            _clockFrame = Time.frameCount;
            float cdt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_openAt >= 0f) _enterClock += cdt;
            if (_tabAnimAt >= 0f) _tabClock += cdt;
            _titleClock += cdt;
        }

        _enterAge = _openAt >= 0f ? _enterClock : 99f;
        if (_openAt >= 0f && _enterAge >= 1f) _openAt = -1f;
        bool closing = _closeAt >= 0f;

        if (_axFrame != Time.frameCount)
        {
            _axFrame = Time.frameCount;
            float k = closing ? CloseK : OpenK;
            float c = closing ? CloseC : OpenC;
            float goal = closing ? 0f : 1f;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.05f) dt = 0.05f;
            int steps = dt > 0.02f ? 3 : 1;
            float hs = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                _av += (k * (goal - _ax) - c * _av) * hs;
                _ax += _av * hs;
            }
            if (_ax < 0f) _ax = 0f;
        }

        if (closing && (_ax <= 0.02f || now - _closeAt > 0.7f))
        {
            _open = false; _closeAt = -1f; _collapsed = false;
            _ax = 0f; _av = 0f;
            GUI.matrix = prevMatrix; return;
        }

        float lean = Mathf.Clamp01(_ax * 1.35f);
        float fade = SmoothSat(lean);
        float xs = Mathf.Min(_ax, 1.12f);               
        float veil = Mathf.Clamp01(_ax);                
        float lift = (1f - xs) * 26f;                   
        if (closing) lift = (1f - xs) * 12f;            
        
        float liftPx = Mathf.Round(lift);
        
        float f0 = Mathf.Clamp01(1f - _ax);
        float flare = closing ? 0f : fade * f0 * f0;
        _fade = fade;

        AmethystStyle.Fill(new Rect(0f, 0f, vw, vh), new Color(0.02f, 0.02f, 0.022f, 0.34f * veil));

        if (Mobile) CenterWindow(vw, vh);
        else
        {
            
            ClampWindow(vw, vh);
            if (fade >= 1f && AnimSettled) HandleResize(vw, vh);
        }

        HandleDrag(vw, vh);

        if (flare > 0.02f || fade > 0.02f)
        {
            
            const int reach = 40, bend = 18;
            float grow = reach + bend;
            
            float shell = closing ? 1f : 0.935f + 0.065f * xs;
            float gW = _window.width * shell;
            float gH = _window.height * shell;
            float gX = _window.center.x - gW * 0.5f;
            float gY = _window.center.y - gH * 0.5f;
            Rect halo = new Rect(gX - grow, gY - grow, gW + grow * 2f, gH + grow * 2f);
            if (fade > 0.02f)
            {
                float dY = _window.center.y + liftPx - gH * 0.5f;
                AmethystStyle.Halo(new Rect(gX - reach, dY - reach + 12f, gW + reach * 2f, gH + reach * 2f),
                    new Color(0f, 0f, 0f, 0.6f * fade), bend, reach);
            }
            if (flare > 0.02f)
                AmethystStyle.Halo(halo, A(AmethystStyle.Current.Accent, 0.36f * flare), bend, reach);
        }

        if (Mathf.Abs(liftPx) >= 1f)
            GUI.matrix = GUI.matrix * Matrix4x4.Translate(new Vector3(0f, liftPx, 0f));

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fade);

        if (_winFn == null) _winFn = (GUI.WindowFunction)DrawWindow;
        _window = GUI.Window(WindowId, _window, _winFn, string.Empty, _windowBg);

        GUI.color = prevColor;
        GUI.matrix = prevMatrix;
    }

    private const int RzL = 1, RzR = 2, RzT = 4, RzB = 8;

    private bool AnimSettled => _closeAt < 0f && _ax >= 0.97f;

    private void HandleResize(float vw, float vh)
    {
        Event e = Event.current;
        if (_collapsed || e == null) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector2 m = e.mousePosition;
            bool inX = m.x >= _window.x - 8f && m.x <= _window.xMax + 8f;
            bool inY = m.y >= _window.y - 8f && m.y <= _window.yMax + 8f;
            int dir = 0;
            if (inY && Mathf.Abs(m.x - _window.x) <= 8f) dir |= RzL;
            if (inY && Mathf.Abs(m.x - _window.xMax) <= 8f) dir |= RzR;
            if (inX && Mathf.Abs(m.y - _window.y) <= 8f) dir |= RzT;
            if (inX && Mathf.Abs(m.y - _window.yMax) <= 8f) dir |= RzB;
            if (dir != 0) { _resize = dir; e.Use(); }
        }
        else if (_resize != 0 && e.type == EventType.MouseDrag)
        {
            Vector2 m = e.mousePosition;
            if ((_resize & RzR) != 0) _window.width = Mathf.Clamp(m.x - _window.x, MinW, Mathf.Max(MinW, vw - _window.x));
            if ((_resize & RzB) != 0) _h = Mathf.Clamp(m.y - _window.y, FullH, Mathf.Max(FullH, vh - _window.y));
            if ((_resize & RzL) != 0)
            {
                float right = _window.x + _window.width;
                float nx = Mathf.Clamp(m.x, 0f, right - MinW);
                _window.x = nx;
                _window.width = right - nx;
            }
            if ((_resize & RzT) != 0)
            {
                float bottom = _window.y + _h;
                float ny = Mathf.Clamp(m.y, 0f, bottom - FullH);
                _window.y = ny;
                _h = bottom - ny;
            }
            e.Use();
        }
        else if (_resize != 0 && e.type == EventType.MouseUp)
        {
            _resize = 0;
            e.Use();
        }
    }

    private void CenterWindow(float vw, float vh)
    {
        float w = Mathf.Clamp(720f, MinW, vw);
        float bodyH = Mathf.Clamp(vh * 0.9f, Mathf.Min(320f, vh), FullH);
        _window.width = w;
        _h = bodyH;
        _window.height = _collapsed ? HeaderH : bodyH;
        if (!_mobileCentered)
        {
            _mobileCentered = true;
            _window.x = Mathf.Max(0f, (vw - w) * 0.5f);
            _window.y = Mathf.Max(0f, (vh - _window.height) * 0.5f);
        }
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, vw - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, vh - _window.height));
    }

    private void ClampWindow(float vw, float vh)
    {
        _window.width = Mathf.Clamp(_window.width, MinW, Mathf.Max(MinW, vw));
        _h = Mathf.Clamp(_h, FullH, Mathf.Max(FullH, vh));
        _window.height = _collapsed ? HeaderH : _h;
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, vw - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, vh - _window.height));
    }

    private void HandleDrag(float vw, float vh)
    {
        Event e = Event.current;
        if (e == null) return;

        if (_dragging && e.type != EventType.MouseUp && !Input.GetMouseButton(0))
        {
            _dragging = false;
            return;
        }

        if (!_dragging)
        {
            if (e.type != EventType.MouseDown || e.button != 0 || !AnimSettled) return;

            Vector2 m = e.mousePosition;
            float lx = m.x - _window.x;
            float ly = m.y - _window.y;

            if (lx < 0f || lx >= _window.width - 110f) return;
            if (ly < 0f || ly >= HeaderH) return;

            _dragging = true;
            
            _dragGrab = new Vector2(lx, ly);
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDrag)
        {
            Vector2 m = e.mousePosition;
            float maxX = Mathf.Max(0f, vw - _window.width);
            float maxY = Mathf.Max(0f, vh - _window.height);
            _window.x = Mathf.Clamp(m.x - _dragGrab.x, 0f, maxX);
            _window.y = Mathf.Clamp(m.y - _dragGrab.y, 0f, maxY);
            e.Use();
        }
        else if (e.type == EventType.MouseUp)
        {
            _dragging = false;
            e.Use();
        }
    }

    private void DrawWindow(int id)
    {
        AmethystPalette p = AmethystStyle.Current;
        float w = _window.width;
        float h = _window.height;
        GUI.color = new Color(1f, 1f, 1f, _fade);

        AmethystStyle.FillRounded(new Rect(0f, 0f, w, h), A(Color.white, 0.07f), 14);
        AmethystStyle.FillRounded(new Rect(1f, 1f, w - 2f, h - 2f),
            new Color(p.Window.r, p.Window.g, p.Window.b, 0.90f), 13);
        AmethystStyle.Fill(new Rect(14f, 1f, w - 28f, 1f), A(Color.white, 0.05f));

        DrawHeader(w, p);
        
        if (_collapsed) return;

        float sbTop = HeaderH + 8f;
        float sbBot = h - FooterH - 2f;
        AmethystStyle.FillRounded(new Rect(8f, sbTop, SidebarW - 14f, sbBot - sbTop), A(p.Panel, 0.55f), 10);
        AmethystStyle.Fill(new Rect(10f, sbTop + 1f, SidebarW - 18f, 1f), A(Color.white, 0.04f));
        AmethystStyle.Fill(new Rect(SidebarW - 2f, HeaderH + 12f, 1f, h - HeaderH - FooterH - 24f), A(Color.white, 0.06f));

        var navZone = new Rect(0f, HeaderH, SidebarW, h - HeaderH - FooterH);
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && navZone.Contains(Event.current.mousePosition))
        {
            SetTab(Mathf.Clamp(_tab + (Event.current.delta.y > 0f ? 1 : -1), 0, Tabs.Length));
            Event.current.Use();
        }

        float tabTop = HeaderH + 14f;
        float tabAvail = (h - FooterH - 2f) - tabTop - 6f;
        float tabStep = Mathf.Min(TabH + 4f, tabAvail / (Tabs.Length + 1));
        float th = Mathf.Min(TabH, tabStep - 3f);
        float settingsY = Mathf.Max(tabTop + Tabs.Length * tabStep, h - FooterH - th - 8f);
        var settingsRect = new Rect(14f, settingsY, SidebarW - 26f, th);
        Rect activeRect = _tab >= Tabs.Length ? settingsRect : new Rect(14f, tabTop + _tab * tabStep, SidebarW - 26f, th);
        if (!_tabHiInit) { _tabHi = activeRect; _tabHiInit = true; }
        if (Event.current != null && Event.current.type == EventType.Repaint)
            _tabHi = LerpRect(_tabHi, activeRect, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        {
            Color keepPill = GUI.color;
            GUI.color = new Color(keepPill.r, keepPill.g, keepPill.b, keepPill.a * Ent(0.22f, 0.3f));
            DrawTabHighlight(_tabHi);
            GUI.color = keepPill;
        }

        float ty = tabTop;
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tr = new Rect(14f, ty, SidebarW - 26f, th);
            float e = Ent(0.10f + i * 0.026f, 0.28f);
            float act = Mathf.Clamp01(1f - Mathf.Abs(tr.y - _tabHi.y) / Mathf.Max(1f, tabStep));
            Color keepTab = GUI.color;
            GUI.color = new Color(keepTab.r, keepTab.g, keepTab.b, keepTab.a * e);
            DrawTab(tr, Mathf.Round(-14f * (1f - e)), i, Tabs[i].Icon, Tabs[i].Name, act);
            GUI.color = keepTab;
            ty += tabStep;
        }
        {
            float e = Ent(0.10f + Tabs.Length * 0.026f, 0.28f);
            float act = Mathf.Clamp01(1f - Mathf.Abs(settingsRect.y - _tabHi.y) / Mathf.Max(1f, tabStep));
            Color keepTab = GUI.color;
            GUI.color = new Color(keepTab.r, keepTab.g, keepTab.b, keepTab.a * e);
            DrawTab(settingsRect, Mathf.Round(-14f * (1f - e)), Tabs.Length, AmethystIcon.Gear, AmethystText.T("Settings"), act);
            GUI.color = keepTab;
        }

        float nowT = Time.unscaledTime;
        string sec = _tab < Tabs.Length ? Tabs[_tab].Name : AmethystText.T("Settings");
        if (!string.Equals(sec, _titleNow))
        {
            _titleOld = _titleNow;
            _titleNow = sec;
            _titleAt = nowT;
            _titleClock = 0f;
        }
        
        float tw = Mathf.Clamp01(_titleClock / 0.26f);
        float ti = 1f - tw;
        float te = 1f - ti * ti * ti;
        float tent = Ent(0.16f, 0.32f);
        float toy = Mathf.Round((1f - tent) * 8f);
        float tx = SidebarW + 4f;
        float tyy = HeaderH + 4f;
        float tww = w - SidebarW - 56f;
        if (tw < 1f && !string.IsNullOrEmpty(_titleOld))
        {
            Color keep = GUI.color;
            GUI.color = new Color(keep.r, keep.g, keep.b, keep.a * (1f - te) * tent);
            _sectionTitle.normal.textColor = p.Muted;
            GUI.Label(new Rect(tx, tyy - Mathf.Round(7f * te) + toy, tww, 30f), _titleOld, _sectionTitle);
            GUI.color = keep;
        }
        {
            Color keep = GUI.color;
            GUI.color = new Color(keep.r, keep.g, keep.b, keep.a * te * tent);
            _sectionTitle.normal.textColor = p.Text;
            GUI.Label(new Rect(tx, tyy + Mathf.Round(7f * (1f - te)) + toy, tww, 30f), _titleNow, _sectionTitle);
            GUI.color = keep;
        }

        var area = new Rect(SidebarW, HeaderH + 40f, w - SidebarW - 16f, h - HeaderH - 40f - FooterH);
        if (Event.current != null && Event.current.type == EventType.Layout && _scrollFrame != Time.frameCount)
        {
            
            _scrollFrame = Time.frameCount;
            _scrollGoal = Mathf.Clamp(_scrollGoal + _scrollPending, 0f, _maxScroll);
            _scrollPending = 0f;
            if (Mathf.Abs(_scroll - _scrollGoal) > 0.4f)
                _scroll = Mathf.Lerp(_scroll, _scrollGoal, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            else
                _scroll = _scrollGoal;
        }
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && area.Contains(Event.current.mousePosition))
        {
            _scrollPending += Event.current.delta.y * 20f;
            Event.current.Use();
        }

        Event se = Event.current;
        if (se != null)
        {
            if (se.type == EventType.MouseDown)
            {
                _scrollGrab = area.Contains(se.mousePosition);
                _scrollLastY = se.mousePosition.y;
                _scrollMoved = false;
            }
            else if (se.type == EventType.MouseDrag && _scrollGrab && _slider == 0)
            {
                float dy = se.mousePosition.y - _scrollLastY;
                _scrollLastY = se.mousePosition.y;
                if (!_scrollMoved && Mathf.Abs(dy) > 6f) _scrollMoved = true;
                if (_scrollMoved) { _scrollPending -= dy; se.Use(); }
            }
            else if (se.type == EventType.MouseUp)
            {
                if (_scrollGrab && _scrollMoved) se.Use();
                _scrollGrab = false;
                _scrollMoved = false;
            }
        }

        _cascade = -100f;
        if (_tabAnimAt >= 0f)
        {
            float age = _tabClock - _tabLag;
            if (age >= 0.5f) _tabAnimAt = -1f;
            
            else _cascade = age;
        }
        _cardIdx = 0;
        float cAlpha = 1f;

        Color cPrev = GUI.color;
        GUI.color = new Color(cPrev.r, cPrev.g, cPrev.b, cPrev.a * cAlpha);
        GUI.BeginGroup(area);

        float x = 4f;
        float cx = x;
        float cw = area.width - 20f;
        float startY = 6f - _scroll;   
        float cy = startY;

        try
        {
            _inScroll = true;
            DrawStars(area.width, area.height);
            _viewH = area.height;
            var localArea = new Rect(0f, 0f, area.width, area.height);
            _cardGrp = -1;
            BeginCardGrid(x, startY, cw);   
            switch (_tab)
            {
                case 0: DrawHome(cx, ref cy, cw); break;
                case 1: DrawGuard(cx, ref cy, cw); break;
                case 2: DrawDevTab(cx, ref cy, cw); break;
                case 3: DrawOther(cx, ref cy, cw); break;
                case 4: DrawModRooms(cx, ref cy, cw); break;
                case 5: DrawCredits(cx, ref cy, cw); break;
                case 6: DrawSettings(cx, ref cy, cw); break;
                default: DrawEmpty(localArea, AmethystIcon.Star, AmethystText.T("SectionInProgress")); break;
            }
            _cardGrp = -1; 
        }
        catch (System.Exception ex)
        {
            
            try { AmethystPlugin.Logger?.LogWarning($"menu tab {_tab} draw failed: {ex}"); } catch { }
        }
        finally
        {
            _inScroll = false;
            
            if (_cardTintOn) { GUI.color = _cardOuter; _cardTintOn = false; }
            EndCardGrid(ref cy);   
            GUI.EndGroup();
            GUI.color = cPrev;
        }

        float contentH = cy - startY;
        float maxScroll = Mathf.Max(0f, contentH - (area.height - 12f));
        _maxScroll = maxScroll;
        if (maxScroll > 1f)
        {
            float trackH = area.height - 8f;
            float thumbH = Mathf.Max(28f, trackH * (area.height / Mathf.Max(contentH, 1f)));
            float thumbY = area.y + 4f + (trackH - thumbH) * (_scroll / maxScroll);
            AmethystStyle.FillRounded(new Rect(area.xMax - 5f, area.y + 4f, 3f, trackH), A(Color.white, 0.05f), 1);
            AmethystStyle.FillRounded(new Rect(area.xMax - 5f, thumbY, 3f, thumbH), A(p.Accent, 0.55f), 1);
        }

        float fy = h - FooterH + 4f;
        AmethystStyle.Fill(new Rect(SidebarW, h - FooterH, w - SidebarW - 16f, 1f), A(Color.white, 0.06f));
        string acc = Hex(p.Accent);
        string footerText =
            $"{AmethystText.T("Menu")} <color=#{acc}><b>{KeyName(AmethystConfig.MenuKey)}</b></color>     {AmethystText.T("LobbyCode")} <color=#{acc}><b>{KeyName(AmethystConfig.CopyCodeKey)}</b></color>     {AmethystText.T("Close")} <color=#{acc}><b>Esc</b></color>";
        LabelFade(new Rect(x, fy, cw, FooterH - 8f), footerText, _footer);

        Color grip = A(p.Muted, 0.5f);
        for (int gi = 0; gi < 3; gi++)
            for (int gj = 0; gj <= gi; gj++)
                AmethystStyle.Fill(new Rect(w - 8f - gj * 4f, h - 8f - gi * 4f, 2f, 2f), grip);

    }

    [HideFromIl2Cpp]
    private void DrawHeader(float w, AmethystPalette p)
    {
        
        float eBrand = Ent(0.04f, 0.3f);
        _brand.wordWrap = false;
        Color keepHdr = GUI.color;
        GUI.color = new Color(keepHdr.r, keepHdr.g, keepHdr.b, keepHdr.a * eBrand);
        float bx = Mathf.Round(20f - 10f * (1f - eBrand));
        AmethystLogo.Draw(new Rect(bx, 18f, 26f, 26f));
        var brandR = new Rect(bx + 32f, 15f, 220f, 34f);
        GUI.Label(brandR, "Amethyst", _brand);

        if (AmethystVersionChecker.UpdateAvailable)
        {
            Color ocUp = _verPill.normal.textColor;
            _verPill.normal.textColor = new Color(1f, 0.45f, 0.45f);
            GUI.Label(new Rect(250f, 22f, 180f, 20f), AmethystText.T("NewVersionAvailable"), _verPill);
            _verPill.normal.textColor = ocUp;
        }

        float eChip = Ent(0.10f, 0.3f);
        var ver = new Rect(w - 168f, 20f + Mathf.Round(-6f * (1f - eChip)), 62f, 24f);
        GUI.color = new Color(keepHdr.r, keepHdr.g, keepHdr.b, keepHdr.a * eChip);
        AmethystStyle.FillRounded(ver, A(p.Accent, 0.14f), 8);
        GUI.Label(ver, "v" + AmethystPlugin.PluginVersion, _verPill);
        GUI.color = keepHdr;

        var minB = new Rect(w - 96f, 20f, 26f, 24f);
        var closeB = new Rect(w - 62f, 20f, 26f, 24f);
        
        GUI.color = new Color(keepHdr.r, keepHdr.g, keepHdr.b, keepHdr.a * Ent(0.16f, 0.3f));
        if (WindowButton(minB, AmethystIcon.Minimize, p)) _collapsed = !_collapsed;
        if (WindowButton(closeB, AmethystIcon.Close, p)) RequestClose();
        GUI.color = keepHdr;

        AmethystStyle.Fill(new Rect(0f, HeaderH - 1f, w, 1f), A(p.Accent, 0.30f));
    }

    [HideFromIl2Cpp]
    private bool WindowButton(Rect r, AmethystIcon icon, AmethystPalette p)
    {
        bool hover = r.Contains(M);
        int id = RectId(r);
        Pressed(r, id);
        float k = PressK(id);

        Matrix4x4 m = GUI.matrix;
        if (k < 1f) GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
        if (hover) AmethystStyle.FillRounded(r, A(Color.white, 0.07f), 7);
        AmethystIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), hover ? p.Text : p.Muted);
        GUI.matrix = m;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    private void DrawTabHighlight(Rect r)
    {
        AmethystPalette p = AmethystStyle.Current;
        AmethystStyle.FillRounded(r, A(p.Accent, 0.18f), 11);
        AmethystStyle.StrokeRounded(r, A(p.Accent, 0.38f), 11, 1);
        AmethystStyle.Fill(new Rect(r.x + 12f, r.y + 1f, r.width - 24f, 1f), A(Color.white, 0.08f));
        AmethystStyle.FillRounded(new Rect(r.x + 3f, r.y + 8f, 3f, r.height - 16f), p.Accent, 2);
    }

    private void DrawTab(Rect r, float slide, int index, AmethystIcon icon, string label, float act)
    {
        AmethystPalette p = AmethystStyle.Current;
        bool hover = r.Contains(M);
        bool on = act > 0.5f;
        var d = new Rect(Mathf.Round(r.x + slide), r.y, r.width, r.height);

        if (hover && act < 0.999f)
        {
            AmethystStyle.FillRounded(d, A(Color.white, 0.05f * (1f - act)), 11);
            AmethystStyle.StrokeRounded(d, A(Color.white, 0.06f * (1f - act)), 11, 1);
        }

        Color baseInk = hover ? A(p.Text, 0.95f) : A(p.Text, 0.62f);
        Color ink = Color.Lerp(baseInk, p.Accent, act);
        
        AmethystIcons.Draw(icon, new Rect(d.x + 14f, d.y + d.height / 2f - 9f, 18f, 18f), ink);
        _tabStyle.normal.textColor = on ? p.Text : (hover ? A(p.Text, 0.92f) : A(p.Text, 0.66f));
        GUI.Label(new Rect(d.x + 44f, d.y, d.width - 48f, d.height), label, _tabStyle);
        if (GUI.Button(r, GUIContent.none, _invisible)) SetTab(index);
    }
private bool _inScroll;

    private bool Cull(float y, float h) => _inScroll && (y + h < -4f || y > _viewH + 4f);

    private bool RowCull(ref float y, float h, float step)
    {
        if (!Cull(y, h)) return false;
        y += step;
        return true;
    }

    private readonly Dictionary<string, bool> _cardCollapsed = new Dictionary<string, bool>();
    private readonly HashSet<string> _knownCards = new HashSet<string>();

    private const float CardGapX = 12f;
    private const float CardGapY = 12f;
    private const float GridTwoColsMinW = 620f;   
    private const float FullSpanBodyH = 560f;     
    private int _cardCols = 1;
    private readonly float[] _cardColY = new float[2];
    private readonly float[] _cardColX = new float[2];
    private float _cardColW;
    private bool _cardGridInit;

    private void BeginCardGrid(float x, float startY, float w)
    {
        _cardCols = w >= GridTwoColsMinW ? 2 : 1;
        _cardColW = (w - CardGapX) * 0.5f;
        _cardColX[0] = x;
        _cardColX[1] = x + _cardColW + CardGapX;
        _cardColY[0] = startY;
        _cardColY[1] = startY;
        
        _cardGridInit = false;
    }

    private void EndCardGrid(ref float y)
    {
        if (_cardCols <= 1) return;
        float maxY = _cardColY[0] > _cardColY[1] ? _cardColY[0] : _cardColY[1];
        if (maxY > y) y = maxY;
    }

    private void Grp(int g) => _cardGrp = g;

    private Rect Card(float x, ref float y, float w, string title, float bodyH)
    {
        AmethystPalette p = AmethystStyle.Current;
        const float head = 44f;

        if (_cardTintOn) { GUI.color = _cardOuter; _cardTintOn = false; }

        if (_cardGrp >= 0 && _cardGrp != _subTab[SubTabSlot()])
            return new Rect(-20000f, -20000f, w - 36f, bodyH);

        if (!_knownCards.Contains(title)) _knownCards.Add(title);   
        _cardCollapsed.TryGetValue(title, out bool collapsed);
        float shownBody = collapsed ? 0f : bodyH;

        bool grid = _cardCols > 1;
        if (grid && !_cardGridInit)
        {
            _cardGridInit = true;
            _cardColY[0] = y;
            _cardColY[1] = y;
        }

        bool span = grid && shownBody >= FullSpanBodyH;      
        int col = 0;
        float cardX = x;
        float cardW = w;
        float cardY;
        if (grid && !span)
        {
            col = _cardColY[1] < _cardColY[0] ? 1 : 0;
            cardX = _cardColX[col];
            cardW = _cardColW;
            cardY = _cardColY[col];
        }
        else if (grid)
        {
            cardY = _cardColY[0] > _cardColY[1] ? _cardColY[0] : _cardColY[1];
        }
        else
        {
            cardY = y;
        }

        float cascadeRise = 0f;
        if (_cascade > -100f)
        {
            float e = SmoothSat((_cascade - Mathf.Min(_cardIdx, 6) * 0.035f) / 0.24f);
            cascadeRise = Mathf.Round((1f - e) * 12f);
            _cardOuter = GUI.color;
            GUI.color = new Color(_cardOuter.r, _cardOuter.g, _cardOuter.b, _cardOuter.a * e);
            _cardTintOn = true;
            _cardIdx++;
        }

        var card = new Rect(cardX, cardY + cascadeRise, cardW, head + shownBody + 14f);

        Event ce = Event.current;
        var headRect = new Rect(card.x, card.y, card.width, head);
        if (ce != null && ce.type == EventType.MouseDown && ce.button == 0 && headRect.Contains(ce.mousePosition))
        {
            collapsed = !collapsed;
            _cardCollapsed[title] = collapsed;
            ce.Use();
        }

        if (!Cull(card.y, card.height))
        {
            bool hover = card.Contains(M);
            AmethystStyle.FillRounded(card, new Color(p.Panel.r, p.Panel.g, p.Panel.b, 0.93f), 10);
            if (hover) AmethystStyle.FillRounded(card, A(Color.white, 0.03f), 10);
            AmethystStyle.StrokeRounded(card, hover ? A(p.Accent, 0.45f) : A(Color.white, 0.07f), 10, 1);
            AmethystStyle.Fill(new Rect(card.x + 12f, card.y + 1f, card.width - 24f, 1f), A(Color.white, 0.05f));

            AmethystStyle.FillRounded(new Rect(card.x + 18f, card.y + head / 2f - 6f, 2f, 12f), p.Accent, 1);
            GUI.Label(new Rect(card.x + 30f, card.y, card.width - 68f, head), title, _cardTitle);
            GUI.Label(new Rect(card.xMax - 34f, card.y, 22f, head), collapsed ? "▸" : "▾", _centerMuted);
            if (!collapsed) AmethystStyle.Fill(new Rect(card.x + 18f, card.y + head - 1f, card.width - 36f, 1f), A(Color.white, 0.05f));
        }

        float cardBottom = cardY + card.height;
        float nextY = cardBottom + (grid ? CardGapY : 14f);
        if (grid && span) { _cardColY[0] = nextY; _cardColY[1] = nextY; }
        else if (grid) _cardColY[col] = nextY;

        y = cardBottom + 14f;
        return collapsed
            ? new Rect(-20000f, -20000f, card.width - 36f, bodyH)
            : new Rect(card.x + 18f, card.y + head + 8f, card.width - 36f, bodyH);
    }

    private void Sub(float x, ref float y, float w, string title)
    {
        if (Cull(y, 26f)) { y += 26f; return; }
        AmethystPalette p = AmethystStyle.Current;
        AmethystStyle.Fill(new Rect(x, y + 21f, w, 1f), A(Color.white, 0.06f));
        AmethystStyle.FillRounded(new Rect(x, y + 6f, 2f, 12f), A(p.Accent, 0.75f), 1);
        GUI.Label(new Rect(x + 10f, y - 1f, w - 12f, 22f), title, _cardTitle);
        y += 26f;
    }

    private static readonly string[] KickBanVals = { "Kick", "Ban" };
    private static readonly string[] VoteVals = { "Null", "Warn", "Kick", "ErrorKick", "Ban" };
    private static string[] _kickBanDisp, _voteDisp;
    private static string _dispLang;

    private static void EnsureDisp()
    {
        string lang = AmethystText.IsRussian ? "ru" : (AmethystText.IsChinese ? "zh" : "en");
        if (_kickBanDisp != null && lang == _dispLang) return;
        _dispLang = lang;
        _kickBanDisp = new[] { AmethystText.T("Kick"), AmethystText.T("Ban") };
        _voteDisp = new[] { AmethystText.T("Null"), AmethystText.T("Warn"), AmethystText.T("Kick"), AmethystText.T("ErrorKick"), AmethystText.T("Ban") };
    }

    private static string[] KickBanDisp { get { EnsureDisp(); return _kickBanDisp; } }
    private static string[] VoteDisp { get { EnsureDisp(); return _voteDisp; } }

    private static readonly string[] GuardSubs = { "GuardSubLists", "GuardSubFilters", "GuardSubShield", "GuardSubPerRpc" };

    [HideFromIl2Cpp]
    
    private int SubTabSlot() => _tab >= 0 && _tab < _subTab.Length ? _tab : 0;

    [HideFromIl2Cpp]
    private int SubBar(float x, ref float y, float w, string[] keys)
    {
        string[] names = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++) names[i] = AmethystText.T(keys[i]);
        int slot = SubTabSlot();
        int cur = Mathf.Clamp(_subTab[slot], 0, names.Length - 1);
        _subTab[slot] = cur;

        AmethystPalette p = AmethystStyle.Current;
        int cols = names.Length <= 4 ? names.Length : 3;
        int rows = Mathf.CeilToInt(names.Length / (float)cols);
        float cellW = (w - (cols - 1) * 6f) / cols;

        var active = new Rect(x + (cur % cols) * (cellW + 6f), y + (cur / cols) * 32f, cellW, 28f);
        if (!_subHiInit[slot]) { _subHi[slot] = active; _subHiInit[slot] = true; }
        else if (Event.current != null && Event.current.type == EventType.Repaint)
            _subHi[slot] = LerpRect(_subHi[slot], active, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));

        for (int i = 0; i < names.Length; i++)
        {
            var r = new Rect(x + (i % cols) * (cellW + 6f), y + (i / cols) * 32f, cellW, 28f);
            if (Cull(r.y, r.height)) continue;
            AmethystStyle.FillRounded(r, A(Color.white, 0.045f), 8);
            AmethystStyle.StrokeRounded(r, A(Color.white, 0.07f), 8, 1);
        }

        if (!Cull(_subHi[slot].y, _subHi[slot].height))
        {
            AmethystStyle.FillRounded(_subHi[slot], A(p.Accent, 0.20f), 8);
            AmethystStyle.StrokeRounded(_subHi[slot], A(p.Accent, 0.65f), 8, 1);
        }

        for (int i = 0; i < names.Length; i++)
        {
            var r = new Rect(x + (i % cols) * (cellW + 6f), y + (i / cols) * 32f, cellW, 28f);
            if (Cull(r.y, r.height)) continue;
            bool on = cur == i;
            
            bool lit = _subHi[slot].Contains(new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f));
            _subTabStyle.normal.textColor = lit ? Color.Lerp(p.Accent, Color.white, 0.5f) : p.Muted;
            GUI.Label(r, names[i], _subTabStyle);
            if (GUI.Button(r, GUIContent.none, _invisible) && !on)
            {
                _subTab[slot] = i;
                _scroll = 0f;
                _scrollPending = 0f;
                _scrollGoal = 0f;
                
                _tabAnimAt = Time.unscaledTime;
                _tabLag = 0f;
            }
        }
        y += rows * 32f + 6f;
        return cur;
    }

    private void DrawGuard(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, GuardSubs);

        Grp(0);
        Rect b = Card(x, ref y, w, AmethystText.T("AccessLists"), 2f * RowH + 94f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("BanListKickOnJoin"), AmethystConfig.AccessBanEnabled);
        GUI.Label(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{AmethystText.T("Ban_2")}: <b>{AmethystAccess.BanCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 86f, by + 1f, 82f, 24f), AmethystText.T("CLRBAN"), new Color(0.9f, 0.36f, 0.36f))) AmethystAccess.ClearBans();
        by += 32f;
        if (SmallButton(new Rect(b.x + 2f, by, 112f, 24f), AmethystText.T("ImportTXT"), AmethystStyle.Current.Accent)) AmethystAccess.ImportTxt();
        GUI.Label(new Rect(b.x + 122f, by, b.width - 122f, 24f), "Among Us/Amethyst/BanList.txt", _muted);
        by += 32f;
        Toggle(b.x, ref by, b.width, AmethystText.T("CloudBanEnabled"), AmethystConfig.CloudBanEnabled);
        string cloudBtnText = AmethystAccess.IsFetchingCloudBans ? "..." : AmethystText.T("GetCloudBanList");
        if (SmallButton(new Rect(b.x + 2f, by, 136f, 24f), cloudBtnText, AmethystStyle.Current.Accent)) AmethystAccess.FetchCloudBans(this);
        GUI.Label(new Rect(b.x + 146f, by, b.width - 146f, 24f), $"{AmethystText.T("CloudBanList")}: <b>{AmethystAccess.CloudBanCount}</b>", _muted);
        by += 32f;

        Grp(0);
        ListCard(x, ref y, w, AmethystText.T("BanList"), AmethystAccess.BanEntries, AmethystAccess.RemoveBan);
        Grp(0);
        
        ListCard(x, ref y, w, AmethystText.T("CloudBanList"), AmethystAccess.CloudBanEntries, AmethystAccess.RemoveCloudBan);
        Grp(0);
        RecentCard(x, ref y, w);

        float lvlBody = 2f * RowH
            + (AmethystConfig.MinLevelEnabled.Value ? 50f + RowH : 0f)
            + (AmethystConfig.MaxLevelEnabled.Value ? 50f + RowH : 0f);
        Grp(1);
        b = Card(x, ref y, w, AmethystText.T("Level"), lvlBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ReactToLowLevel"), AmethystConfig.MinLevelEnabled);
        if (AmethystConfig.MinLevelEnabled.Value)
        {
            SliderInt(b.x, ref by, b.width, AmethystText.T("MinLevel"), AmethystConfig.MinLevel, 1, 500);
            ActionCycle(b.x, ref by, b.width, AmethystText.T("Action"), AmethystConfig.MinLevelAction, KickBanVals, KickBanDisp);
        }
        Toggle(b.x, ref by, b.width, AmethystText.T("ReactToHighLevel"), AmethystConfig.MaxLevelEnabled);
        if (AmethystConfig.MaxLevelEnabled.Value)
        {
            SliderInt(b.x, ref by, b.width, AmethystText.T("MaxLevel"), AmethystConfig.MaxLevel, 1, 999);
            ActionCycle(b.x, ref by, b.width, AmethystText.T("Action"), AmethystConfig.MaxLevelAction, KickBanVals, KickBanDisp);
        }

        Grp(2);
        
        int rowItemsCount = 31;
        float hostCardH = rowItemsCount * RowH + 50f + 50f + 3f * 26f;
        if (AmethystConfig.RpcGuardAction.Value == "Kick" || AmethystConfig.RpcGuardAction.Value == "Ban")
            hostCardH += 50f; 

        b = Card(x, ref y, w, AmethystText.T("RPCAnticheatHost"), hostCardH);
        by = b.y;
        bool beforeAc = AmethystConfig.AntiCheat.Value;
        Toggle(b.x, ref by, b.width, AmethystText.T("AntiCheatMaster"), AmethystConfig.AntiCheat);
        if (AmethystConfig.AntiCheat.Value != beforeAc) ApplyAntiCheat(AmethystConfig.AntiCheat.Value);
        HostAwareToggle(b.x, ref by, b.width, AmethystText.T("DropRPCsHost"), AmethystConfig.DropRPC);
        ActionCycle(b.x, ref by, b.width, AmethystText.T("ReactToCheater"), AmethystConfig.RpcGuardAction, VoteVals, VoteDisp);
        if (AmethystConfig.RpcGuardAction.Value == "Kick" || AmethystConfig.RpcGuardAction.Value == "Ban")
        {
            SliderInt(b.x, ref by, b.width, AmethystText.T("CheaterActionThreshold"), AmethystConfig.CheaterActionThreshold, 1, 8);
        }
        Toggle(b.x, ref by, b.width, AmethystText.T("RPCFloodDetectionNotifyOnly"), AmethystConfig.RpcFloodDetect);
        
        SliderInt(b.x, ref by, b.width, AmethystText.T("FloodThresholdSec"), AmethystConfig.RpcFloodThreshold, 25, 500);

        Sub(b.x, ref by, b.width, AmethystText.T("GuardGroupCheck"));
        Toggle(b.x, ref by, b.width, AmethystText.T("DetectModClients"), AmethystConfig.ModClientDetection);
        
        Toggle(b.x, ref by, b.width, AmethystText.T("ForeignModDetect"), AmethystConfig.ForeignModDetect);
        Toggle(b.x, ref by, b.width, AmethystText.T("BypassDisconnectPenalty"), AmethystConfig.BypassDisconnectPenalty);
        Toggle(b.x, ref by, b.width, AmethystText.T("DetectHostTempBan"), AmethystConfig.DetectHostTempBan);
        Toggle(b.x, ref by, b.width, AmethystText.T("FastKillGuard"), AmethystConfig.FastKillGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("PlatformSpoofGuard"), AmethystConfig.PlatformSpoofGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("InvalidFcGuard"), AmethystConfig.InvalidFcGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("BotGuard"), AmethystConfig.BotGuard);
        Sub(b.x, ref by, b.width, AmethystText.T("GuardGroupRules"));
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardBlockFakeMeetings"), AmethystConfig.BlockFakeMeetings);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardBlockEarlyMeetings"), AmethystConfig.BlockEarlyMeetings);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardHsSabotage"), AmethystConfig.HsSabotageGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardVentHost"), AmethystConfig.VentHostRules);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardVentClient"), AmethystConfig.VentClientRules);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardSpawnFlood"), AmethystConfig.SpawnFloodGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardVentTp"), AmethystConfig.VentTpProtect);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardZipline"), AmethystConfig.ZiplineProtect);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardSabotageRepair"), AmethystConfig.CheckSabotageRepair);
        Slider(b.x, ref by, b.width, AmethystText.T("GuardSabotageRepairDist"), AmethystConfig.SabotageRepairDistance, 0.5f, 10f, "0.0", 0.1f);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardNoImpostorEnd"), AmethystConfig.NoImpostorGameOver);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardDoorSpam"), AmethystConfig.DoorSpamGuard);
        
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardSecurityNotify"), AmethystConfig.SecurityNotify);

        Sub(b.x, ref by, b.width, AmethystText.T("GuardGroupRpc"));
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcPlayer"), AmethystConfig.RpcGuardPlayer);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcVent"), AmethystConfig.RpcGuardVent);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcShip"), AmethystConfig.RpcGuardShip);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcVoteBan"), AmethystConfig.RpcGuardVoteBan);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcGameData"), AmethystConfig.RpcGameDataCheck);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardUnknownRpc"), AmethystConfig.UnknownRpcGuard);
            
            Grp(2);
            Grp(3);   
        b = Card(x, ref y, w, AmethystText.T("GuardRpcPerCall"), AmethystConfig.RpcCallNames.Length * RowH + 24f);
            by = b.y;
            for (int ri = 0; ri < AmethystConfig.RpcCallNames.Length; ri++)
            {
                string rn = AmethystConfig.RpcCallNames[ri];
                if (AmethystConfig.RpcCallSwitches.TryGetValue(rn, out var rcfg) && rcfg != null)
                    
                    Toggle(b.x, ref by, b.width, AmethystText.T(rn), rcfg);
            }
        
        Grp(2);
        b = Card(x, ref y, w, AmethystText.T("AntiBan"), 2f * RowH + 42f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("AsHostPunishSender"), AmethystConfig.AntiBanHost);
        ActionCycle(b.x, ref by, b.width, AmethystText.T("Action_2"), AmethystConfig.AntiBanHostAction, VoteVals, VoteDisp);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), AmethystText.T("KillsTheCrashBanVentKickPacketOffHostItIsAlwaysO"), _muted);

        Grp(2);
        
         b = Card(x, ref y, w, AmethystText.T("NetworkProtection"), 4f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ForceDTLSEncryption"), AmethystConfig.ForceDtls);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockServerTeleports"), AmethystConfig.BlockServerTeleports);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockUnauthorizedUpdates"), AmethystConfig.BlockUnauthorizedUpdates);
        Toggle(b.x, ref by, b.width, AmethystText.T("ReportDeadBodyCrashGuard"), AmethystConfig.ReportDeadBodyCrashGuard);

        Grp(2);
        
        b = Card(x, ref y, w, AmethystText.T("AntiOverload"), 6f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ProtEnabled"), AmethystConfig.AntiOverloadEnabled);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockLargeGameMessages"), AmethystConfig.BlockLargeGameMessages);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockInvalidGameDataTypes"), AmethystConfig.BlockInvalidGameDataTypes);
        Toggle(b.x, ref by, b.width, AmethystText.T("HardenPackedUInt"), AmethystConfig.HardenPackedUInt);
        Toggle(b.x, ref by, b.width, AmethystText.T("VotingCompleteOverloadGuard"), AmethystConfig.BlockVotingCompleteOverload);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockDataFlood"), AmethystConfig.BlockDataFlood);

        Grp(2);
        
        b = Card(x, ref y, w, AmethystText.T("AntiKick"), 4f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ProtEnabled"), AmethystConfig.AntiKickEnabled);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockVoteKicks"), AmethystConfig.VoteKickProtect);
        ActionCycle(b.x, ref by, b.width, AmethystText.T("Action_2"), AmethystConfig.VoteKickAction, VoteVals, VoteDisp);
        Toggle(b.x, ref by, b.width, AmethystText.T("HostOnlyRpcKickGuard"), AmethystConfig.HostOnlyRpcKickGuard);

        Grp(1);
        b = Card(x, ref y, w, AmethystText.T("Colors"), RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("KickFortegreen"), AmethystConfig.KickFortegreen);

        Grp(1);
        b = Card(x, ref y, w, AmethystText.T("Moderation"), 2f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("KickBanInMatchHost"), AmethystConfig.UnlockMatchKickBan);
        Toggle(b.x, ref by, b.width, AmethystText.T("KickSlowJoiners"), AmethystConfig.KickSlowJoiners);
        
    }
    private void DrawDevTab(float x, ref float y, float w)
    {
        Grp(0);
        Rect b = Card(x, ref y, w, AmethystText.T("DevOptions"), 3f * RowH + 20f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("PerfProbe"), AmethystConfig.PerfProbe);
        Toggle(b.x, ref by, b.width, AmethystText.T("NoWinConditions"), AmethystConfig.NoWinConditions);

        if (AmethystConfig.RoleAlgorithm != null)
        {
            int cur = Mathf.Clamp(AmethystConfig.RoleAlgorithm.Value, 0, 3);
            CycleRow(b.x, ref by, b.width, AmethystText.T("RoleAlgorithm"), RoleAlgorithmName(cur),
                () => { AmethystConfig.RoleAlgorithm.Value = (cur + 1) % 4; });
        }
    }

    private static string RoleAlgorithmName(int mode)
    {
        return mode switch
        {
            1 => AmethystText.T("RoleAlgoSystem"),
            2 => AmethystText.T("RoleAlgoXorshift"),
            3 => AmethystText.T("RoleAlgoMT"),
            _ => AmethystText.T("RoleAlgoDefault"),
        };
    }

    private void RecentCard(float x, ref float y, float w)
    {
        IReadOnlyList<RecentEntry> recent = AmethystRecent.Entries;
        float body = 30f + (recent.Count > 0 ? recent.Count * 46f : 26f);
        Rect b = Card(x, ref y, w, $"{AmethystText.T("WhoLeft")} ({recent.Count})", body);
        float by = b.y;

        if (SmallButton(new Rect(b.x + b.width - 96f, by, 92f, 24f), AmethystText.T("CLEAR"), new Color(0.9f, 0.36f, 0.36f)))
            AmethystRecent.Clear();
        by += 30f;

        if (recent.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), AmethystText.T("Empty"), _muted);
            return;
        }

        for (int i = 0; i < recent.Count; i++)
        {
            RecentEntry e = recent[i];
            var r = new Rect(b.x, by, b.width, 44f);
            HoverFill(r);
            GUI.Label(new Rect(r.x + 8f, r.y + 1f, r.width - 152f, 22f), e.Title, _rowName);
            GUI.Label(new Rect(r.x + 8f, r.y + 21f, r.width - 152f, 20f), e.Info, _rowInfo);

            if (SmallButton(new Rect(r.xMax - 74f, r.y + 10f, 70f, 24f), AmethystText.T("Ban"), new Color(0.9f, 0.4f, 0.4f)))
                AmethystAccess.AddBan(e.Name, e.Fc, e.Puid);
            by += 46f;
        }
    }

    [HideFromIl2Cpp]
    
    private void DrawCredits(float x, ref float y, float w)
    {
        Grp(0);
        Rect head = Card(x, ref y, w, AmethystText.T("Credits"), RowH + 22f);
        GUI.Label(new Rect(head.x + 2f, head.y, head.width - 4f, 24f),
            AmethystText.T("CreditsThanks"), _muted);

        for (int g = 0; g < AmethystCredits.Groups.Length; g++)
        {
            CreditGroup grp = AmethystCredits.Groups[g];
            if (grp == null || grp.Members == null || grp.Members.Length == 0) continue;

            Grp(0);
            Rect b = Card(x, ref y, w, AmethystText.T(grp.TitleKey), grp.Members.Length * 30f + 4f);
            float by = b.y;
            for (int i = 0; i < grp.Members.Length; i++)
            {
                string member = grp.Members[i];
                if (string.IsNullOrEmpty(member)) continue;

                var r = new Rect(b.x, by, b.width, 28f);
                HoverFill(r);
                AmethystStyle.FillRounded(new Rect(r.x + 6f, r.y + 8f, 3f, 12f), AmethystStyle.Current.Accent, 1);
                GUI.Label(new Rect(r.x + 18f, r.y, r.width - 24f, r.height), member, _rowName);
                by += 30f;
            }
        }
    }

    [HideFromIl2Cpp]
    private void ListCard(float x, ref float y, float w, string title, IReadOnlyList<AccessEntry> entries, Action<string> onRemove)
    {
        float body = entries.Count > 0 ? entries.Count * 30f : 26f;
        Rect b = Card(x, ref y, w, $"{title} ({entries.Count})", body);
        float by = b.y;
        if (entries.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), AmethystText.T("Empty"), _muted);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AccessEntry e = entries[i];
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            string name = string.IsNullOrEmpty(e.Name) ? (string.IsNullOrEmpty(e.Code) ? AmethystText.T("Guest") : e.Code) : e.Name;
            float nameW = onRemove != null ? r.width - 50f : r.width - 12f;
            string subText = EntrySub(e);
            string rowText = string.IsNullOrEmpty(subText)
                ? $"<b>{name}</b>"
                : $"<b>{name}</b>  <color=#8A94AC><size=11>{subText}</size></color>";
            LabelFade(new Rect(r.x + 8f, r.y, nameW, r.height), rowText, _rowName);
            if (onRemove != null && SmallButton(new Rect(r.xMax - 36f, r.y + 3f, 32f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f)))
            {
                onRemove(string.IsNullOrEmpty(e.Code) ? e.Puid : e.Code);
                break;
            }
            by += 30f;
        }
    }

    private static string EntrySub(AccessEntry e)
    {
        string s = string.Empty;
        if (!string.IsNullOrEmpty(e.Code)) s = e.Code;
        if (!string.IsNullOrEmpty(e.Puid))
        {
            string pu = e.Puid.Length > 8 ? e.Puid.Substring(0, 8) + "…" : e.Puid;
            s = s.Length > 0 ? s + " · " + pu : pu;
        }
        return s;
    }

    private bool SmallButton(Rect r, string label, Color col)
    {
        if (Cull(r.y, r.height)) return false;
        bool hover = r.Contains(M);
        int id = RectId(r);
        Pressed(r, id);
        float k = PressK(id);

        Matrix4x4 m = GUI.matrix;
        if (k < 1f) GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
        AmethystStyle.FillRounded(r, hover ? A(col, 0.4f) : A(col, 0.2f), 7);
        AmethystStyle.Fill(new Rect(r.x + 4f, r.y + 2f, r.width - 8f, 1f), A(Color.white, 0.14f));
        _smallBtn.normal.textColor = Color.Lerp(col, Color.white, 0.55f);
        GUI.Label(r, label, _smallBtn);
        GUI.matrix = m;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    [HideFromIl2Cpp]
    private void ActionCycle(float x, ref float y, float w, string label, ConfigEntry<string> entry, string[] vals, string[] disp)
    {
        int i = Mathf.Max(0, Array.IndexOf(vals, entry.Value));
        CycleRow(x, ref y, w, label, disp[i], () => { entry.Value = vals[(i + 1) % vals.Length]; });
    }

    [HideFromIl2Cpp]
    private void ColorCycle(float x, ref float y, float w, string label, ConfigEntry<int> entry)
    {
        int max = 17;
        try { if (Palette.PlayerColors != null) max = Palette.PlayerColors.Length - 1; } catch { }
        int cur = Mathf.Clamp(entry.Value, 0, Mathf.Max(0, max));
        CycleRow(x, ref y, w, label, ColorName(cur),
            () => { entry.Value = (cur + 1) % (max + 1); },
            () => { entry.Value = (cur - 1 + max + 1) % (max + 1); });
    }

    private static string ColorName(int id)
    {
        try
        {
            string n = Palette.GetColorName(id);
            if (!string.IsNullOrEmpty(n)) return char.ToUpperInvariant(n[0]) + (n.Length > 1 ? n.Substring(1) : string.Empty);
        }
        catch { }
        return "#" + id;
    }

    private static InnerNetClient GuardNet()
    {
        try { return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance; }
        catch { return null; }
    }

    private static string ClientInfo(ClientData c)
    {
        string s = string.Empty;
        try
        {
            string lvl = Patches.AmethystJoinLevels.Display(c);
            if (lvl != "?") s = AmethystText.IsChinese ? lvl + "级" : AmethystText.T("Lvl") + lvl;
        }
        catch { }
        try
        {
            if (c.PlatformData != null)
            {
                string pl = Plat(c.PlatformData.Platform);
                if (pl.Length > 0) s = s.Length > 0 ? s + " · " + pl : pl;
            }
        }
        catch { }
        return s;
    }

    private static string Plat(Platforms p)
    {
        return (int)p switch
        {
            1 => "Epic",
            2 => "Steam",
            3 => "Mac",
            4 => "MS",
            5 => "Itch",
            6 => "iOS",
            7 => "Android",
            8 => "Switch",
            9 => "Xbox",
            10 => "PS",
            _ => string.Empty,
        };
    }

    private string _homeAbout, _homeImp;
    private float _homeAboutH, _homeImpH, _homeTextW = -1f;

    private void DrawHome(float x, ref float y, float w)
    {
        HomeAbout(x, ref y, w);
        HomeMoreInfo(x, ref y, w);
        HomeNotices(x, ref y, w);
    }

    private void HomeAbout(float x, ref float y, float w)
    {
        string about = AmethystText.T("BAmethystBAnAmongUsAnticheat");
        MeasureHome(about, w);
        Grp(0);
        
        Rect b = Card(x, ref y, w, AmethystText.T("About"), _homeAboutH + 2f * 30f + 8f);
        GUI.Label(new Rect(b.x, b.y, b.width, _homeAboutH), about, _rowLabel);
        float by = b.y + _homeAboutH + 8f;
        InfoRow(b.x, ref by, b.width, AmethystText.T("Version"), "v" + AmethystPlugin.PluginVersion);
        InfoRow(b.x, ref by, b.width, AmethystText.T("Author"), "一只屑小紫");
    }

    private void HomeMoreInfo(float x, ref float y, float w)
    {
        Grp(0);
        Rect b = Card(x, ref y, w, AmethystText.T("MoreInfo"), 4f * 30f + 8f);
        float by = b.y;
        InfoRow(b.x, ref by, b.width, AmethystText.T("System"), AmethystSysInfo.Os());
        InfoRow(b.x, ref by, b.width, AmethystText.T("GameVersion"), AmethystSysInfo.Game());
        InfoRow(b.x, ref by, b.width, "BepInEx", AmethystSysInfo.BepInExVersion());
        InfoRow(b.x, ref by, b.width, AmethystText.T("ModCount"), AmethystSysInfo.ModCount());
    }

    private void HomeNotices(float x, ref float y, float w)
    {
        string imp = AmethystText.T("ColorFFD166TheModMayConflictWithOtherModsDisable");
        Grp(0);
        Rect b = Card(x, ref y, w, AmethystText.T("Important"), _homeImpH);
        LabelFade(b, imp, _rowLabel);

        Grp(0);
        b = Card(x, ref y, w, AmethystText.T("Links"), 3f * RowH);
        float by = b.y;
        LinkRow(b.x, ref by, b.width, AmethystText.T("QQGroup"), "1031096791");
        LinkRow(b.x, ref by, b.width, "GitHub", "https://github.com/CN-SPPLAYER/Amethyst");
        string keyName = AmethystConfig.MenuKey.Value == KeyCode.None ? "—" : AmethystConfig.MenuKey.Value.ToString();
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), AmethystText.T("MenuOpenMenuKeyIsInSettingsKeys", keyName), _muted);
    }

    private void MeasureHome(string about, float w)
    {
        string imp = AmethystText.T("ColorFFD166TheModMayConflictWithOtherModsDisable");
        if (Mathf.Abs(w - _homeTextW) < 0.5f && ReferenceEquals(about, _homeAbout) && ReferenceEquals(imp, _homeImp)) return;
        _homeTextW = w; _homeAbout = about; _homeImp = imp;

        float narrow = (_cardCols > 1 ? _cardColW : w) - 36f;
        _homeAboutH = _rowLabel.CalcHeight(new GUIContent(about), narrow);
        _homeImpH = _rowLabel.CalcHeight(new GUIContent(imp), narrow);

        if (Mathf.Max(_homeAboutH, _homeImpH) >= FullSpanBodyH)
        {
            float wide = w - 36f;
            _homeAboutH = _rowLabel.CalcHeight(new GUIContent(about), wide);
            _homeImpH = _rowLabel.CalcHeight(new GUIContent(imp), wide);
        }
    }

    private void LinkRow(float x, ref float y, float w, string label, string url)
    {
        AmethystPalette p = AmethystStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        bool hover = r.Contains(M);
        AmethystStyle.FillRounded(r, hover ? A(p.Accent, 0.18f) : A(Color.white, 0.04f), 8);
        AmethystStyle.StrokeRounded(r, hover ? A(p.Accent, 0.5f) : A(Color.white, 0.08f), 8, 1);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width - 48f, r.height), label, _rowLabelLine);
        GUI.Label(new Rect(r.x + w - 40f, r.y, 34f, r.height), "↗", _value);
        if (!string.IsNullOrEmpty(url) && GUI.Button(r, GUIContent.none, _invisible))
            OpenLink(url);
        y += RowH;
    }

    private static readonly string[] OtherSubs = { "OtherSubPing", "OtherSubLobby", "OtherSubDisplay", "OtherSubAppearance", "OtherSubPerf" };

    private void DrawOther(float x, ref float y, float w)
    {
        Grp(-1);
        int sub = SubBar(x, ref y, w, OtherSubs);
        switch (sub)
        {
            case 0: DrawOtherPing(x, ref y, w); break;
            case 1: DrawOtherLobby(x, ref y, w); break;
            case 2: DrawOtherDisplay(x, ref y, w); break;
            case 3: DrawOtherAppearance(x, ref y, w); break;
            default: DrawOtherPerf(x, ref y, w); break;
        }
    }

    private void DrawOtherPing(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, AmethystText.T("OtherCooldown"), 4f * RowH + 4f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("BetterPingDisplay"), AmethystConfig.BetterPingDisplay);
        Toggle(b.x, ref by, b.width, AmethystText.T("BetterCooldownDisplay"), AmethystConfig.BetterCooldownDisplay);
        Toggle(b.x, ref by, b.width, AmethystText.T("CooldownDecimals"), AmethystConfig.CooldownDecimals);
        Toggle(b.x, ref by, b.width, AmethystText.T("SabotageCooldownDisplay"), AmethystConfig.SabotageCooldownDisplay);

        if (AmethystConfig.BetterPingDisplay != null && AmethystConfig.BetterPingDisplay.Value)
        {
            Rect pb = Card(x, ref y, w, AmethystText.T("BetterPingItems"), 5f * RowH + 4f);
            float pby = pb.y;
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingLatency"), AmethystConfig.PingShowLatency);
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingFps"), AmethystConfig.PingShowFps);
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingServer"), AmethystConfig.PingShowServer);
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingRoom"), AmethystConfig.PingShowRoom);
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingHost"), AmethystConfig.PingShowHost);
        }
    }

    private void DrawOtherLobby(float x, ref float y, float w)
    {
        
        Rect b = Card(x, ref y, w, AmethystText.T("OtherLobby"), 8f * RowH + 4f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ExpandLobbySearch"), AmethystConfig.ExpandLobbySearch);
        Toggle(b.x, ref by, b.width, AmethystText.T("RoomSortByMaxPlayersLabel"), AmethystConfig.RoomSortByMaxPlayers);
        Toggle(b.x, ref by, b.width, AmethystText.T("RoomInfoDisplay"), AmethystConfig.RoomInfoDisplay);
        Toggle(b.x, ref by, b.width, AmethystText.T("LobbyPlayerInfo"), AmethystConfig.LobbyPlayerInfo);
        Toggle(b.x, ref by, b.width, AmethystText.T("FixLobbyMusic"), AmethystConfig.FixLobbyMusic);
        Toggle(b.x, ref by, b.width, AmethystText.T("MuteLobbyMusic"), AmethystConfig.MuteLobbyMusic);
        Toggle(b.x, ref by, b.width, AmethystText.T("MusicMuteGameMusic"), AmethystConfig.MusicMuteGameMusic);
        Toggle(b.x, ref by, b.width, AmethystText.T("ScaleLobbyPane"), AmethystConfig.ScaleLobbyPane);

        b = Card(x, ref y, w, AmethystText.T("ColorSnipe"), 2f * RowH + 8f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ColorSnipe"), AmethystConfig.ColorSnipe);
        ColorCycle(b.x, ref by, b.width, AmethystText.T("ColorSnipeId"), AmethystConfig.ColorSnipeId);
    }

    private void DrawOtherDisplay(float x, ref float y, float w)
    {
        
        Rect b = Card(x, ref y, w, AmethystText.T("OtherColors"), 4f * RowH + 4f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ColorBlindText"), AmethystConfig.ColorBlindText);
        Toggle(b.x, ref by, b.width, AmethystText.T("NameColors"), AmethystConfig.NameColors);
        
        Toggle(b.x, ref by, b.width, AmethystText.T("RoleColors"), AmethystConfig.RoleColors);
        Toggle(b.x, ref by, b.width, AmethystText.T("HoverThemeColor"), AmethystConfig.HoverThemeColor);

        b = Card(x, ref y, w, AmethystText.T("OtherRoleInfo"), 8f * RowH + 4f);
        by = b.y;
        
        Toggle(b.x, ref by, b.width, AmethystText.T("MeetingTaskPanel"), AmethystConfig.MeetingTaskPanel);
        Toggle(b.x, ref by, b.width, AmethystText.T("ShowMeetingHost"), AmethystConfig.ShowMeetingHost);
        Toggle(b.x, ref by, b.width, AmethystText.T("ShowSelfRole"), AmethystConfig.ShowSelfRole);
        Toggle(b.x, ref by, b.width, AmethystText.T("ShowImpostorTeammateRole"), AmethystConfig.ShowImpostorTeammateRole);
        Toggle(b.x, ref by, b.width, AmethystText.T("ShowRolesWhenDead"), AmethystConfig.ShowRolesWhenDead);
        
        Toggle(b.x, ref by, b.width, AmethystText.T("RolePanel"), AmethystConfig.RolePanel);
        Toggle(b.x, ref by, b.width, AmethystText.T("RoleTag"), AmethystConfig.RoleTag);
        Toggle(b.x, ref by, b.width, AmethystText.T("WinnerRoleReveal"), AmethystConfig.WinnerRoleReveal);

        b = Card(x, ref y, w, AmethystText.T("Visual"), RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("SkipKillAnimation"), AmethystConfig.SkipKillAnimation);
    }

    private void DrawOtherAppearance(float x, ref float y, float w)
    {
        
        Rect b = Card(x, ref y, w, AmethystText.T("OtherMainMenu"), 7f * RowH + 4f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("UiTheme"), AmethystConfig.UiTheme);
        Toggle(b.x, ref by, b.width, AmethystText.T("MainMenuOptimize"), AmethystConfig.MainMenuOptimize);
        Toggle(b.x, ref by, b.width, AmethystText.T("MainMenuButtons"), AmethystConfig.MainMenuButtons);
        Toggle(b.x, ref by, b.width, AmethystText.T("MainMenuArt"), AmethystConfig.MainMenuArt);
        Toggle(b.x, ref by, b.width, AmethystText.T("SnowEffect"), AmethystConfig.SnowEffect);
        Toggle(b.x, ref by, b.width, AmethystText.T("OutfitSaver"), AmethystConfig.OutfitSaver);
        Toggle(b.x, ref by, b.width, AmethystText.T("RecapInfo"), AmethystConfig.RecapInfo);

        bool fpsOn = AmethystConfig.UnlockFps240 != null && AmethystConfig.UnlockFps240.Value;
        
        b = Card(x, ref y, w, AmethystText.T("DisplayRoles"), 5f * RowH + 20f + (fpsOn ? 50f : 0f) + 50f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("UnlockFps"), AmethystConfig.UnlockFps240);
        if (AmethystConfig.UnlockFps240 != null && AmethystConfig.UnlockFps240.Value)
            SliderInt(b.x, ref by, b.width, AmethystText.T("FpsLimit"), AmethystConfig.FpsLimit, 60, 240);
        Toggle(b.x, ref by, b.width, AmethystText.T("AutoReturnToLobby"), AmethystConfig.AutoReturnLobby);
        Toggle(b.x, ref by, b.width, AmethystText.T("UnlockAllCosmetics"), AmethystConfig.UnlockAllCosmetics);
        
        Slider(b.x, ref by, b.width, AmethystText.T("AbilityUiScale"), AmethystConfig.AbilityUiScale, 0.1f, 1.5f, "0.00", 0.01f);
        Toggle(b.x, ref by, b.width, AmethystText.T("TopRightButtonGroup"), AmethystConfig.TopRightButtonGroup);
        
        Toggle(b.x, ref by, b.width, AmethystText.T("ShowKeyGuide"), AmethystConfig.ShowKeyGuide);

        b = Card(x, ref y, w, AmethystText.T("VisualChat"), 4f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("Keep80ChatMessages"), AmethystConfig.ChatHistory80);
        Toggle(b.x, ref by, b.width, AmethystText.T("ForceShowChatOverlay"), AmethystConfig.VisualAlwaysShowChat);
        Toggle(b.x, ref by, b.width, AmethystText.T("BetterChatRicherInputCopyPaste"), AmethystConfig.BetterChat);
        Toggle(b.x, ref by, b.width, AmethystText.T("DarkChatThemeMatchingCurrentAccent"), AmethystConfig.DarkChatTheme);
    }

    private void DrawOtherPerf(float x, ref float y, float w)
    {
        
        bool deadOn = AmethystConfig.PerfDontUpdateDead != null && AmethystConfig.PerfDontUpdateDead.Value;
        Rect b = Card(x, ref y, w, AmethystText.T("PerfSection"), 4f * RowH + 20f + (deadOn ? 50f : 0f));
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("PerfDontUpdateDead"), AmethystConfig.PerfDontUpdateDead);
        if (deadOn)
            SliderInt(b.x, ref by, b.width, AmethystText.T("PerfDeadSkipFrames"), AmethystConfig.PerfDeadSkipFrames, 2, 300);
        Toggle(b.x, ref by, b.width, AmethystText.T("PerfLowLoad"), AmethystConfig.PerfLowLoad);
        Toggle(b.x, ref by, b.width, AmethystText.T("PerfHideConsole"), AmethystConfig.PerfHideConsole);
    }

    private static void ApplyAntiCheat(bool on)
    {
        AmethystConfig.RpcGuard.Value = on;
        AmethystConfig.VentTpProtect.Value = on;
        AmethystConfig.ZiplineProtect.Value = on;
        AmethystConfig.VentHostRules.Value = on;
        AmethystConfig.VentClientRules.Value = on;
        AmethystConfig.BlockEarlyMeetings.Value = on;
        AmethystConfig.HsSabotageGuard.Value = on;
        AmethystConfig.BlockFakeMeetings.Value = on;
        AmethystConfig.SpawnFloodGuard.Value = on;
    }

    private static readonly string[] SettingsSubs = { "SettingsSubInterface", "SettingsSubKeys" };
    private static readonly int[] SettingsCounts = { 0, 0 };

    private void DrawSettings(float x, ref float y, float w)
    {
        int sub = SubBar(x, ref y, w, SettingsSubs);
        SettingsCounts[0] = 0;
        SettingsCounts[1] = 0;
        switch (sub)
        {
            case 1: KeySettings(x, ref y, w); break;
            default: InterfaceSettings(x, ref y, w); break;
        }
    }

    private void InterfaceSettings(float x, ref float y, float w)
    {
        Grp(0);
        
        Rect b = Card(x, ref y, w, AmethystText.T("Interface"),
            RowH + 50f + 3f * RowH + 24f);
        float by = b.y;
        CycleRow(b.x, ref by, b.width, AmethystText.T("Language"), AmethystText.LangName, () => AmethystText.Toggle());
        Slider(b.x, ref by, b.width, AmethystText.T("MenuScale"), AmethystConfig.MenuScale, 0.7f, 1.8f, "0.00", 0.01f);
        Toggle(b.x, ref by, b.width, AmethystText.T("Toasts"), AmethystConfig.Toasts);
        Toggle(b.x, ref by, b.width, AmethystText.T("OnScreenMenuButton"), AmethystConfig.MenuButton);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockTelemetryData"), AmethystConfig.BlockTelemetry);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), AmethystText.T("AppliedOnGameStart"), _muted);
        SettingsCounts[0] = 1;
    }

    private void KeySettings(float x, ref float y, float w)
    {
        Grp(1);
        Rect b = Card(x, ref y, w, AmethystText.T("Hotkeys"), 9f * RowH + 22f);
        float by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 4f, 22f), AmethystText.T("ChangeTheTopLeftMenuOpenKeyHere"), _muted);
        by += 20f;
        KeyRow(b.x, ref by, b.width, AmethystText.T("OpenMenuKey"), AmethystConfig.MenuKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("LobbyCode"), AmethystConfig.CopyCodeKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("RecapShowKey"), AmethystConfig.RecapKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("MusicKey"), AmethystConfig.MusicKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("MusicPrevKey"), AmethystConfig.MusicPrevKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("MusicNextKey"), AmethystConfig.MusicNextKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("MusicPlayPauseKey"), AmethystConfig.MusicPlayPauseKey);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), AmethystText.T("ClickToSetTrashRMBToClearEscToCancel"), _muted);
        SettingsCounts[1] = 1;
    }

    private void DrawEmpty(Rect area, AmethystIcon icon, string msg)
    {
        AmethystPalette p = AmethystStyle.Current;
        float s = 66f;
        var ic = new Rect(area.center.x - s / 2f, area.y + area.height * 0.30f, s, s);
        AmethystStyle.FillRounded(new Rect(ic.x - 12f, ic.y - 12f, s + 24f, s + 24f), A(p.Accent, 0.06f), 22);
        AmethystIcons.Draw(icon, ic, A(p.Muted, 0.55f));
        GUI.Label(new Rect(area.x, ic.yMax + 16f, area.width, 24f), msg, _centerMuted);
    }

    [HideFromIl2Cpp]
    private void ActionRow(Rect body, AmethystIcon icon, string label, string btn, Action onClick)
    {
        AmethystPalette p = AmethystStyle.Current;
        var row = new Rect(body.x, body.y, body.width, 44f);
        var sq = new Rect(row.x, row.y + 2f, 40f, 40f);
        AmethystStyle.FillRounded(sq, A(p.Accent, 0.14f), 10);
        AmethystIcons.Draw(icon, new Rect(sq.x + 10f, sq.y + 10f, 20f, 20f), p.Accent);
        GUI.Label(new Rect(sq.xMax + 14f, row.y, row.width - 170f, 44f), label, _rowLabel);

        var bt = new Rect(row.xMax - 112f, row.y + 5f, 112f, 34f);
        bool hover = bt.Contains(M);
        AmethystStyle.FillRounded(bt, p.Accent, 9);
        AmethystStyle.FillRounded(new Rect(bt.x + 1.5f, bt.y + 1.5f, bt.width - 3f, bt.height - 3f), hover ? A(p.Accent, 0.30f) : p.Panel, 8);
        AmethystStyle.Fill(new Rect(bt.x + 8f, bt.y + 4f, bt.width - 16f, 1f), A(Color.white, 0.10f));
        GUI.Label(bt, btn, _btnLabel);
        if (GUI.Button(bt, GUIContent.none, _invisible)) onClick();
    }

    private void InfoRow(float x, ref float y, float w, string label, string value)
    {
        GUI.Label(new Rect(x + 2f, y, w * 0.5f, 28f), label, _muted);
        GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f - 2f, 28f), value, _value);
        y += 30f;
    }

    [HideFromIl2Cpp]
    private void Toggle(float x, ref float y, float w, string label, ConfigEntry<bool> entry)
    {
        if (entry == null) { y += RowH; return; }
        if (Cull(y, RowH)) { y += RowH; return; }
        var r = new Rect(x, y, w, RowH - 4f);
        bool forcedOn = AmethystPlatform.IsAndroid &&
                        string.Equals(entry.Definition.Key, "MenuButton", StringComparison.Ordinal);
        if (forcedOn && !entry.Value) entry.Value = true;

        if (!forcedOn) HoverFill(r);
        if (!forcedOn && GUI.Button(r, GUIContent.none, _invisible))
        {
            entry.Value = !entry.Value;
            
            if (string.Equals(entry.Definition.Key, "MuteLobbyMusic", StringComparison.Ordinal))
            {
                try
                {
                    LobbyBehaviour lobby = LobbyBehaviour.Instance;
                    if (lobby != null)
                    {
                        if (entry.Value) Patches.AmethystLobbyMusicFix.MuteNow(lobby);
                        else Patches.AmethystLobbyMusicFix.UnmuteNow(lobby);
                    }
                }
                catch { }
            }
        }

        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height),
            label, forcedOn ? _muted : _rowLabelLine);
        DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f),
            forcedOn ? true : entry.Value, entry, forcedOn);
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void HostAwareToggle(float x, ref float y, float w, string label, ConfigEntry<bool> entry)
    {
        if (entry == null) { y += RowH; return; }
        if (Cull(y, RowH)) { y += RowH; return; }
        var r = new Rect(x, y, w, RowH - 4f);
        bool host = false;
        try { host = AmongUsClient.Instance != null && ((InnerNetClient)AmongUsClient.Instance).AmHost; } catch { }
        if (!host)
        {
            if (entry.Value)
            {
                AmethystRpcGuard.SetDropRpcEnabled(false);
                entry.Value = false;
            }
            GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height), label, _muted);
            DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f), false, entry);
            y += RowH;
            return;
        }

        HoverFill(r);
        if (host)
        {
            if (GUI.Button(r, GUIContent.none, _invisible))
                AmethystRpcGuard.SetDropRpcEnabled(!entry.Value, true);
        }
        else
        {
            AmethystStyle.StrokeRounded(r, new Color(1f, 1f, 1f, 0.08f), 8, 1);
        }

        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height), label, _rowLabelLine);
        DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f), entry.Value, entry);
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void Slider(float x, ref float y, float w, string label, ConfigEntry<float> entry, float min, float max, string fmt, float step = 0f)
    {
        if (entry == null) { y += RowH; return; }
        if (Cull(y, 50f)) { y += 50f; return; }
        AmethystPalette p = AmethystStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabelLine);
        bool deferred = ReferenceEquals(entry, AmethystConfig.MenuScale);
        if (deferred && !_menuScaleDragging && !_menuScaleDraftActive) _menuScaleDraft = entry.Value;
        var labelRect = new Rect(x + w - 58f, y, 54f, 22f);
        y += 26f;

        Rect minus = default, plus = default, track;
        if (step > 0f)
        {
            const float arrowW = 24f;
            minus = new Rect(x + 8f, y - 3f, arrowW, 20f);
            plus = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
            track = new Rect(minus.xMax + 8f, y + 2f, plus.x - minus.xMax - 16f, 8f);
        }
        else
        {
            track = new Rect(x + 10f, y + 2f, w - 20f, 8f);
        }

        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x - 8f, track.y - 10f, track.width + 16f, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
        {
            _slider = id;
            if (deferred) { _menuScaleDraft = entry.Value; _menuScaleDraftActive = true; _menuScaleDragging = true; }
            e.Use();
        }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                float next = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                if (deferred) _menuScaleDraft = next;
                else entry.Value = next;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                if (deferred && _menuScaleDraftActive)
                {
                    entry.Value = Mathf.Clamp(_menuScaleDraft, min, max);
                    _menuScaleDraftActive = false;
                }
                if (deferred) _menuScaleDragging = false;
                _slider = 0;
                e.Use();
            }
        }

        if (deferred && _menuScaleDragging && !Input.GetMouseButton(0))
        {
            entry.Value = Mathf.Clamp(_menuScaleDraft, min, max);
            _menuScaleDraftActive = false;
            _menuScaleDragging = false;
            _slider = 0;
        }

        if (step > 0f)
        {
            float cur = deferred && _menuScaleDraftActive ? _menuScaleDraft : entry.Value;
            if (StepBtn(minus, "-", p)) CommitSlider(entry, deferred, Mathf.Clamp(cur - step, min, max));
            if (StepBtn(plus, "+", p)) CommitSlider(entry, deferred, Mathf.Clamp(cur + step, min, max));
        }

        float shown = deferred && _menuScaleDraftActive ? _menuScaleDraft : entry.Value;
        GUI.Label(labelRect, shown.ToString(fmt), _value);

        float t = Mathf.InverseLerp(min, max, shown);
        DrawSliderTrack(track, t);
        y += 24f;
    }

    private void CommitSlider(ConfigEntry<float> entry, bool deferred, float v)
    {
        if (deferred)
        {
            _menuScaleDraft = v;
            _menuScaleDraftActive = false;
            _menuScaleDragging = false;
            _slider = 0;
        }
        entry.Value = v;
    }

    private bool StepBtn(Rect r, string label, AmethystPalette p)
    {
        bool hov = r.Contains(M);
        AmethystStyle.FillRounded(r, hov ? p.ButtonHover : p.Button, 6);
        AmethystStyle.StrokeRounded(r, A(Color.white, hov ? 0.10f : 0.06f), 6, 1);

        Color prev = _arrow.normal.textColor;
        _arrow.normal.textColor = hov ? p.Text : A(p.Text, 0.72f);
        GUI.Label(r, label, _arrow);
        _arrow.normal.textColor = prev;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

[HideFromIl2Cpp]
    private void DrawSliderTrack(Rect track, float t)
    {
        AmethystPalette p = AmethystStyle.Current;
        t = Mathf.Clamp01(t);
        float kx = track.x + track.width * t;

        AmethystStyle.FillRounded(track, p.Button, 4);
        AmethystStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(6f, track.width * t), track.height), p.Accent, 4);

        float ky = track.y + track.height / 2f;
        AmethystStyle.FillRounded(new Rect(kx - 8f, ky - 7f, 16f, 16f), A(Color.black, 0.28f), 8);
        AmethystStyle.FillRounded(new Rect(kx - 7f, ky - 8f, 14f, 14f), Color.white, 7);
    }

    [HideFromIl2Cpp]
    private void SliderInt(float x, ref float y, float w, string label, ConfigEntry<int> entry, int min, int max)
    {
        if (entry == null) { y += RowH; return; }
        AmethystPalette p = AmethystStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabelLine);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value.ToString(), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "-")) entry.Value = Mathf.Clamp(entry.Value - 1, min, max);
        if (ArrowButton(rightBtn, "+")) entry.Value = Mathf.Clamp(entry.Value + 1, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width))), min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        DrawSliderTrack(track, t);
        y += 24f;
    }

    [HideFromIl2Cpp]
    private void FpsSlider(float x, ref float y, float w, string label, ConfigEntry<int> entry)
    {
        if (entry == null) { y += RowH; return; }
        const int min = 30, max = 300, step = 5;
        AmethystPalette p = AmethystStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabelLine);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value >= max ? "∞" : entry.Value.ToString(), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "-")) entry.Value = Mathf.Clamp(entry.Value - step, min, max);
        if (ArrowButton(rightBtn, "+")) entry.Value = Mathf.Clamp(entry.Value + step, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                int raw = Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width)));
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(raw / (float)step) * step, min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        DrawSliderTrack(track, t);
        y += 24f;
    }

    private bool ArrowButton(Rect r, string glyph)
        => StepBtn(r, glyph, AmethystStyle.Current);

[HideFromIl2Cpp]
    private void CycleRow(float x, ref float y, float w, string label, string valueText, Action onClick, Action onLeft = null)
    {
        if (Cull(y, RowH)) { y += RowH; return; }
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.45f, r.height), label, _rowLabelLine);

        if (onLeft != null)
        {
            
            const float aw = 22f;    
            
            float maxValW = r.width * 0.45f; 
            float valW = 40f;
            try
            {
                if (_value != null)
                {
                    valW = Mathf.Clamp(_valueC.CalcSize(new GUIContent(valueText)).x + 16f, 36f, maxValW);
                }
            }
            catch { }

            var rb = new Rect(r.xMax - aw - 4f, r.y + 2f, aw, r.height - 4f); 
            var lb = new Rect(rb.x - valW - aw, rb.y, aw, rb.height);         
            
            if (ArrowButton(lb, "◂")) onLeft();
            if (ArrowButton(rb, "▸")) onClick();
            
            GUI.Label(new Rect(lb.xMax, r.y, valW, r.height), valueText, _valueC);
        }
        else
        {
            if (GUI.Button(r, GUIContent.none, _invisible)) onClick();
            GUI.Label(new Rect(r.x + r.width - 168f, r.y, 164f, r.height), valueText + "   ▸", _value);
        }
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void KeyRow(float x, ref float y, float w, string label, ConfigEntry<KeyCode> entry)
    {
        if (entry == null) { y += RowH; return; }
        if (Cull(y, RowH)) { y += RowH; return; }
        AmethystPalette p = AmethystStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.5f, r.height), label, _rowLabelLine);

        bool listening = _rebinding && ReferenceEquals(_rebindTarget, entry);
        bool isMenuKey = ReferenceEquals(entry, AmethystConfig.MenuKey);
        float bw = listening ? 132f : 96f;
        var badge = new Rect(r.xMax - bw - 4f, r.y + (r.height - 24f) / 2f, bw, 24f);
        AmethystStyle.FillRounded(badge, listening ? A(p.Accent, 0.85f) : A(Color.white, 0.06f), 7);
        AmethystStyle.StrokeRounded(badge, listening ? A(p.Accent, 0.9f) : A(Color.white, 0.10f), 7, 1);
        _keyBadge.normal.textColor = listening ? Color.white : p.Text;
        string txt = listening ? AmethystText.T("PressAKey") : (entry.Value == KeyCode.None ? "—" : entry.Value.ToString());
        GUI.Label(badge, txt, _keyBadge);

        Event e = Event.current;

        if (!listening && !isMenuKey)
        {
            var del = new Rect(badge.x - 26f, badge.y, 22f, 24f);
            bool dh = e != null && del.Contains(e.mousePosition);
            AmethystStyle.FillRounded(del, dh ? A(new Color(0.9f, 0.3f, 0.3f), 0.30f) : A(Color.white, 0.06f), 6);
            AmethystIcons.Draw(AmethystIcon.Trash, new Rect(del.x + 5f, del.y + 5f, 14f, 14f), dh ? new Color(0.95f, 0.5f, 0.5f) : A(p.Muted, 0.9f));
            if (GUI.Button(del, GUIContent.none, _invisible))
            {
                entry.Value = KeyCode.None;
                _rebinding = false; _rebindTarget = null; Rebinding = false;
            }
        }

        bool rightClear = !isMenuKey && e != null && e.type == EventType.MouseDown && e.button == 1 && badge.Contains(e.mousePosition);
        bool clicked = GUI.Button(badge, GUIContent.none, _invisible);
        if (rightClear)
        {
            entry.Value = KeyCode.None;
            _rebinding = false; _rebindTarget = null; Rebinding = false;
            e.Use();
        }
        else if (clicked)
        {
            if (listening) { _rebinding = false; _rebindTarget = null; }
            else { _rebinding = true; _rebindTarget = entry; }
            Rebinding = _rebinding;
        }
        y += RowH;
    }

    private void HandleRebindCapture()
    {
        Rebinding = _rebinding;
        if (!_rebinding || _rebindTarget == null) return;
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        KeyCode kc = e.keyCode;
        if (kc == KeyCode.Escape) { _rebinding = false; _rebindTarget = null; }
        else if (kc != KeyCode.None) { _rebindTarget.Value = kc; _rebinding = false; _rebindTarget = null; }
        Rebinding = _rebinding;
        e.Use();
    }

    [HideFromIl2Cpp]
    private void DrawPill(Rect track, bool on, object key, bool disabled = false)
    {
        AmethystPalette p = AmethystStyle.Current;
        int r = Mathf.RoundToInt(track.height / 2f);

        if (disabled)
        {
            AmethystStyle.FillRounded(track, A(p.Button, 0.40f), r);
            AmethystStyle.StrokeRounded(track, A(Color.white, 0.03f), r, 1);
            float dk = track.height - 6f;
            int dkr = Mathf.RoundToInt(dk / 2f);
            AmethystStyle.FillRounded(new Rect(track.xMax - dk - 3f, track.y + 3f, dk, dk),
                A(p.Muted, 0.40f), dkr);
            return;
        }

        float target = on ? 1f : 0f;
        float a = target, v = 0f;
        if (key != null)
        {
            if (!_pillAnim.TryGetValue(key, out a)) a = target;
            _pillVel.TryGetValue(key, out v);
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                v += (-300f * (a - target) - 24f * v) * dt;
                a += v * dt;
                _pillAnim[key] = a;
                _pillVel[key] = v;
            }
        }
        float e = Mathf.Clamp01(a);
        float kpos = Mathf.Clamp(a, -0.12f, 1.12f);

        if (e > 0.01f)
            AmethystStyle.FillRounded(track, Color.Lerp(p.Button, p.Accent, e), r);
        else
            AmethystStyle.FillRounded(track, p.Button, r);
        if (e < 0.99f)
            AmethystStyle.StrokeRounded(track, A(Color.white, 0.06f * (1f - e)), r, 1);

        float knob = track.height - 6f;
        float kx = Mathf.Lerp(track.x + 3f, track.xMax - knob - 3f, kpos);
        int kr = Mathf.RoundToInt(knob / 2f);
        AmethystStyle.FillRounded(new Rect(kx, track.y + 4f, knob, knob), A(Color.black, 0.22f), kr);
        AmethystStyle.FillRounded(new Rect(kx, track.y + 3f, knob, knob), Color.Lerp(p.Muted, Color.white, e), kr);
    }

    private static float Frac(float v) => v - Mathf.Floor(v);

    private void DrawStars(float w, float h)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        float t = Time.unscaledTime;
        for (int i = 0; i < 26; i++)
        {
            float fx = Frac(i * 0.61803399f + 0.11f);
            float fy = Frac(i * 0.75487767f + 0.29f);
            float drift = Frac(fy + 0.02f * t * (0.4f + fx));
            float sz = 1.3f + 1.7f * fy;
            float tw = 0.5f + 0.5f * Mathf.Sin(t * (0.7f + fx) + i);
            Color col = (i % 5 == 0) ? AmethystStyle.Current.Accent : Color.white;
            AmethystStyle.FillRounded(new Rect(fx * w, drift * h, sz, sz), A(col, 0.05f + 0.06f * tw), 1);
        }
    }

    private void HoverFill(Rect r)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        if (!r.Contains(Event.current.mousePosition)) return;
        AmethystPalette p = AmethystStyle.Current;
        AmethystStyle.FillRounded(r, A(p.Accent, 0.06f), 8);
        AmethystStyle.FillRounded(new Rect(r.x, r.y, r.width * 0.42f, r.height), A(p.Accent, 0.05f), 8);
        AmethystStyle.FillRounded(new Rect(r.x + 1f, r.y + 4f, 3f, r.height - 8f), p.Accent, 2);
    }

    private string CustomText(Rect r, string text, string key)
    {
        AmethystPalette p = AmethystStyle.Current;
        bool focused = _textFocus == key;
        AmethystStyle.FillRounded(r, p.Button, 7);
        AmethystStyle.StrokeRounded(r, focused ? A(p.Accent, 0.8f) : A(Color.white, 0.08f), 7, 1);

        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown)
            _textFocus = r.Contains(e.mousePosition) ? key : (_textFocus == key ? null : _textFocus);

        if (focused && e != null && e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Backspace) { if (text.Length > 0) text = text.Substring(0, text.Length - 1); e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { _textFocus = null; e.Use(); }
            else if (e.character != '\0' && !char.IsControl(e.character) && text.Length < 24) { text += e.character; e.Use(); }
        }

        bool caret = focused && Time.unscaledTime % 1f < 0.5f;
        GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), text + (caret ? "|" : ""), _rowLabel);
        return text;
    }

    private static void OpenLink(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try { GUIUtility.systemCopyBuffer = url; } catch { }
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try { Application.OpenURL(url); } catch { }
        }
        AmethystToast.Push(AmethystText.T("Link"), AmethystText.T("CopiedToClipboard"), 3.0f, AmethystNotifyKind.Info);
    }

    private static string KeyName(ConfigEntry<KeyCode> key) => key != null ? key.Value.ToString() : "?";
    private static string KeyDisp(ConfigEntry<KeyCode> key) => key == null || key.Value == KeyCode.None ? "—" : key.Value.ToString();
    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static Rect LerpRect(Rect a, Rect b, float t) =>
        new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));

    private static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }

    private void PrewarmIfNeeded()
    {
        if (_prewarmed) return;
        if (!AmethystMenuButton.AtMainMenu()) return;

        _prewarmed = true;
        try { Build(); } catch { _built = false; }
        try { PrewarmFonts(); } catch { }
        try { AmethystStyle.PrewarmTextures(); } catch { }
        try { AmethystPlugin.Logger?.LogInfo("[Menu] prewarm done"); } catch { }
    }

    private void PrewarmFonts()
    {
        string all = AmethystText.AllTextForPrewarm();
        if (string.IsNullOrEmpty(all)) return;

        var seen = new HashSet<string>();
        WarmUnique(_brand, all, seen); WarmUnique(_verPill, all, seen); WarmUnique(_cardTitle, all, seen);
        WarmUnique(_rowLabel, all, seen); WarmUnique(_rowLabelLine, all, seen); WarmUnique(_value, all, seen);
        WarmUnique(_valueC, all, seen); WarmUnique(_muted, all, seen); WarmUnique(_footer, all, seen);
        WarmUnique(_btnLabel, all, seen); WarmUnique(_centerMuted, all, seen); WarmUnique(_arrow, all, seen);
        WarmUnique(_subTabStyle, all, seen); WarmUnique(_smallBtn, all, seen); WarmUnique(_rowName, all, seen);
        WarmUnique(_rowInfo, all, seen); WarmUnique(_tabStyle, all, seen); WarmUnique(_sectionTitle, all, seen);
        WarmUnique(_keyBadge, all, seen); WarmUnique(_star, all, seen);
    }

    private static void WarmUnique(GUIStyle style, string text, HashSet<string> seen)
    {
        if (style == null || string.IsNullOrEmpty(text)) return;
        try
        {
            Font font = style.font != null ? style.font : GUI.skin.font;
            if (font == null) return;

            string key = font.GetInstanceID() + "|" + style.fontSize + "|" + (int)style.fontStyle;
            if (!seen.Add(key)) return;
            font.RequestCharactersInTexture(text, style.fontSize, style.fontStyle);
        }
        catch { }
    }

    private void Build()
    {
        var currentFont = AmethystFont.GetFont();
        
        if (_built && currentFont == _lastBuiltFont) return;
        _built = true;
        _lastBuiltFont = currentFont;
        AmethystPalette p = AmethystStyle.Current;

        _windowBg = new GUIStyle();
        _invisible = new GUIStyle();

        _brand = Label(p.Text, Mathf.RoundToInt(25 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _verPill = Label(p.Accent, Mathf.RoundToInt(12 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _cardTitle = Label(p.Text, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
        _rowLabel = Label(p.Text, Mathf.RoundToInt(15 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowLabel.wordWrap = true;
        _rowLabelLine = Label(p.Text, Mathf.RoundToInt(15 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowLabelLine.wordWrap = false;
        _rowLabelLine.clipping = TextClipping.Clip;
        _value = Label(p.Accent, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleRight);
        _valueC = Label(p.Accent, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _muted = Label(p.Muted, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _footer = Label(p.Muted, Mathf.RoundToInt(12 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _btnLabel = Label(p.Accent, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _centerMuted = Label(A(p.Muted, 0.8f), Mathf.RoundToInt(14 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter, customFont: false); 
        _arrow = Label(p.Accent, Mathf.RoundToInt(15 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter, customFont: false); 
        _subTabStyle = Label(p.Muted, Mathf.RoundToInt(12 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _smallBtn = Label(Color.white, Mathf.RoundToInt(11 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter, customFont: false); 
        _rowName = Label(p.Text, Mathf.RoundToInt(14 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowName.wordWrap = false;
        _rowName.clipping = TextClipping.Clip;
        _rowInfo = Label(p.Muted, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowInfo.wordWrap = false;
        _rowInfo.clipping = TextClipping.Clip;
        _tabStyle = Label(p.Muted, Mathf.RoundToInt(14 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        
        _sectionTitle = Label(p.Text, Mathf.RoundToInt(20 * 1.2f), FontStyle.Bold, TextAnchor.MiddleLeft);
        _keyBadge = Label(p.Text, Mathf.RoundToInt(12 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _star = Label(p.Accent, Mathf.RoundToInt(17 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter, customFont: false);
    }

    private static readonly Regex ColorTagRx = new Regex("<color=#([0-9A-Fa-f]{6})>", RegexOptions.Compiled);

    private void LabelFade(Rect r, string text, GUIStyle style)
    {
        if (string.IsNullOrEmpty(text)) return;

        float a = GUI.color.a;
        if (a >= 0.999f)
        {
            GUI.Label(r, text, style);
            return;
        }

        bool any = false;
        string faded = ColorTagRx.Replace(text, m =>
        {
            any = true;
            return "<color=#" + m.Groups[1].Value + AlphaHex(a) + ">";
        });

        if (!any)
        {
            GUI.Label(r, text, style);
            return;
        }

        GUI.Label(r, faded, style);
    }

    private static string AlphaHex(float a)
    {
        int v = Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
        return v.ToString("X2");
    }

    private static GUIStyle Label(Color color, int size, FontStyle style, TextAnchor anchor, bool customFont = true)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = style,
            alignment = anchor,
            richText = true
        };
        if (customFont)
        {
            AmethystFont.ApplyTo(s);
        }
        else
        {
            
            s.font = null;
        }
        s.normal.textColor = color;
        s.hover.textColor = color;
        s.active.textColor = color;
        s.focused.textColor = color;
        s.onNormal.textColor = color;
        s.onHover.textColor = color;
        s.onActive.textColor = color;
        s.onFocused.textColor = color;
        s.padding = AmethystStyle.Offset(0, 0, 0, 0);
        return s;
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
}
