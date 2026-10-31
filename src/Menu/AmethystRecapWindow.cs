using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystRecapWindow : MonoBehaviour
{
    private const float HeadH = 30f;
    private const float RowH = 20f;

    private const float AnimDuration = 0.42f;   
    private const float PanelW = 470f;          
    private const float PanelH = 620f;          
    private const float MarginX = 26f;          
    private const float MarginY = 84f;          
    private const float BackdropAlpha = 0.55f;  
    private const float RowSlide = 92f;         
    private const float RowStagger = 0.030f;    
    private const float RowReveal = 0.22f;      

    private Rect _win = new Rect(MarginX, MarginY, PanelW, PanelH);
    private float _anim;        
    private float _scroll;
    private bool _built;
    private GUIStyle _title, _row, _muted;

    private List<string> _cache = new List<string>();
    private float _cacheAt = -1f;

    private static bool _visible;

    internal static void ToggleVisible() => _visible = !_visible;

    internal static bool IsOpen => _visible;

    internal static void Hide() => _visible = false;

    internal static void ResetOnLobbyLeft() => _visible = false;

    private void Build()
    {
        if (_built) return;
        _built = true;
        _title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * 1.2f), fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        _row = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * 1.2f), fontStyle = FontStyle.Normal, richText = true, wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
        _muted = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * 1.2f), fontStyle = FontStyle.Normal, richText = true, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        AmethystFont.ApplyTo(_title);
        AmethystFont.ApplyTo(_row);
        AmethystFont.ApplyTo(_muted);
    }

    [HideFromIl2Cpp]
    internal void DrawGui()
    {
        int __p = AmethystPerfProbe.Begin(Section.S_Recap);
        try { DrawGuiInner(); }
        finally { AmethystPerfProbe.End(__p, Section.S_Recap); }
    }

    private void DrawGuiInner()
    {
        if (!AmethystRecap.CanUse) { Hide(); return; }

        Build();

        AmethystPalette p = AmethystStyle.Current;
        float sc = Mathf.Clamp(Screen.height / 1080f, 1f, 2.4f);
        Matrix4x4 mtx = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
        try
        {
            
            float dt = Time.unscaledDeltaTime;
            _anim = Mathf.MoveTowards(_anim, _visible ? 1f : 0f, dt / AnimDuration);
            float ease = Mathf.SmoothStep(0f, 1f, _anim);

            float sw = Screen.width / sc, sh = Screen.height / sc;
            Event e = Event.current;

            if (ease > 0.0001f)
                AmethystStyle.Fill(new Rect(0f, 0f, sw, sh), A(Color.black, BackdropAlpha * ease));

            if (!AmethystRecapButton.IsActive)
            {
                Rect toggle = new Rect(16f, 16f, 190f, 32f);
                AmethystStyle.FillRounded(toggle, A(p.Window, 0.95f), 8);
                AmethystStyle.StrokeRounded(toggle, A(p.Accent, Hover(toggle) ? 0.9f : 0.5f), 8, 1);
                _title.normal.textColor = p.Text;
                GUI.Label(new Rect(toggle.x + 12f, toggle.y + 6f, toggle.width - 24f, 20f),
                    AmethystText.T(_visible ? "RecapHide" : "RecapShow"), _title);
                if (GUI.Button(toggle, GUIContent.none, GUIStyle.none)) ToggleVisible();
            }

            if (ease <= 0.0001f) return;   

            float panelH = Mathf.Min(sh - MarginY * 2f, PanelH);
            float panelY = (sh - panelH) * 0.5f;
            float panelX = -PanelW + (MarginX + PanelW) * ease;
            _win = new Rect(panelX, panelY, PanelW, panelH);

            float w = _win.width, h = _win.height;

            AmethystStyle.FillRounded(_win, A(p.Window, 0.95f), 12);
            AmethystStyle.StrokeRounded(_win, A(p.Accent, 0.28f), 12, 1);
            AmethystStyle.Fill(new Rect(_win.x + 10f, _win.y + HeadH - 1f, w - 20f, 1f), A(p.Accent, 0.35f));

            AmethystStyle.FillRounded(new Rect(_win.x + 12f, _win.y + 9f, 3f, 12f), p.Accent, 1);
            _title.normal.textColor = p.Text;
            GUI.Label(new Rect(_win.x + 20f, _win.y + 5f, 200f, 20f), AmethystText.T("RecapInfo"), _title);

            Rect cpy = new Rect(_win.x + w - 80f, _win.y + 6f, 22f, 18f);
            Rect cls = new Rect(_win.x + w - 28f, _win.y + 6f, 22f, 18f);
            if (Hover(cpy)) AmethystStyle.FillRounded(cpy, A(p.Accent, 0.20f), 6);
            if (Hover(cls)) AmethystStyle.FillRounded(cls, A(new Color(0.9f, 0.3f, 0.3f), 0.25f), 6);
            Icon(cpy, AmethystIcon.Copy, Hover(cpy) ? p.Text : p.Muted);
            Icon(cls, AmethystIcon.Close, Hover(cls) ? new Color(0.95f, 0.5f, 0.5f) : p.Muted);
            if (GUI.Button(cpy, GUIContent.none, GUIStyle.none)) CopyAll();
            if (GUI.Button(cls, GUIContent.none, GUIStyle.none)) { _visible = false; return; }

            float top = _win.y + HeadH + 6f;
            string result = AmethystRecap.ResultText();
            if (!string.IsNullOrEmpty(result))
            {
                _muted.normal.textColor = A(p.Text, 0.95f);
                GUI.Label(new Rect(_win.x + 12f, top, w - 24f, 18f),
                    $"{AmethystText.T("RecapResult")}: {result}", _muted);
                top += 20f;
            }

            Rect body = new Rect(_win.x + 8f, top, w - 16f, h - (top - _win.y) - 10f);
            List<string> rows = CachedRows();

            if (rows.Count == 0)
            {
                _muted.normal.textColor = A(p.Muted, 0.85f);
                GUI.Label(new Rect(body.x + 4f, body.y + 4f, body.width - 8f, 20f), AmethystText.T("RecapNoData"), _muted);
            }
            else
            {
                float total = rows.Count * RowH;
                float maxScroll = Mathf.Max(0f, total - body.height);

                if (e != null && e.type == EventType.ScrollWheel && body.Contains(e.mousePosition))
                {
                    _scroll += e.delta.y * 18f;
                    e.Use();
                }
                _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);

                GUI.BeginGroup(body);
                Event ge = Event.current;
                float y = -_scroll;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (y + RowH >= 0f && y <= body.height)
                    {
                        
                        float rt = Mathf.Clamp01((ease - i * RowStagger) / RowReveal);
                        if (rt > 0f)
                        {
                            Rect rr = new Rect(0f, y, body.width, RowH);
                            bool hov = ge != null && rr.Contains(ge.mousePosition);
                            if (hov) AmethystStyle.Fill(rr, A(p.Accent, 0.10f * rt));
                            _row.normal.textColor = A(p.Text, rt);
                            
                            GUI.Label(new Rect(6f - (1f - rt) * RowSlide, y, body.width - 12f, RowH), rows[i], _row);
                        }
                    }
                    y += RowH;
                }
                GUI.EndGroup();

                if (maxScroll > 1f && total > 0f)
                {
                    float th = Mathf.Max(24f, body.height * (body.height / total));
                    float ty = body.y + (body.height - th) * (_scroll / maxScroll);
                    AmethystStyle.FillRounded(new Rect(body.xMax - 3f, ty, 3f, th), A(p.Accent, 0.5f), 1);
                }
            }
        }
        finally { GUI.matrix = mtx; }
    
    }

    [HideFromIl2Cpp]
    private List<string> CachedRows()
    {
        float now = Time.unscaledTime;
        if (now - _cacheAt < 0.25f) return _cache;
        _cacheAt = now;
        try { _cache = AmethystRecap.Rows(); } catch { }
        return _cache;
    }

    private static void CopyAll()
    {
        try
        {
            string text = AmethystRecap.PlainText();
            if (string.IsNullOrEmpty(text)) return;
            GUIUtility.systemCopyBuffer = text;
            AmethystToast.Push(AmethystText.T("Copied"), null, 3.0f, AmethystNotifyKind.Success);
        }
        catch { }
    }

    private static void Icon(Rect r, AmethystIcon icon, Color c)
        => AmethystIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), c);

    private static bool Hover(Rect r) => Event.current != null && r.Contains(Event.current.mousePosition);

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
