using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using UnityEngine;

namespace Amethyst;

public sealed partial class AmethystMusicPlayer
{

    [HideFromIl2Cpp]
    internal void DrawGui()
    {
        try { DrawGuiInner(); }
        catch { }
    }


    private void Build()
    {
        if (_built) return;
        _built = true;
        _title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * 1.2f), fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        _row = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * 1.2f), fontStyle = FontStyle.Normal, richText = true, wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
        _muted = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(11 * 1.2f), fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        _btn = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * 1.2f), fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
        
        _now = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * 1.2f), fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        AmethystFont.ApplyTo(_title);
        AmethystFont.ApplyTo(_row);
        AmethystFont.ApplyTo(_muted);
        AmethystFont.ApplyTo(_btn);
        AmethystFont.ApplyTo(_now);
    }


    private void DrawGuiInner()
    {
        if (!_visible) return;

        Build();
        AmethystPalette p = AmethystStyle.Current;
        float sc = Mathf.Clamp(Screen.height / 1080f, 1f, 2.4f);
        Matrix4x4 mtx = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));

        try
        {
            float sw = Screen.width / sc, sh = Screen.height / sc;
            _win.width = 430f;
            _win.height = 404f;
            _win.x = Mathf.Clamp(_win.x, 0f, Mathf.Max(0f, sw - _win.width));
            _win.y = Mathf.Clamp(_win.y, 0f, Mathf.Max(0f, sh - _win.height));

            float w = _win.width, h = _win.height;
            float x0 = _win.x + 12f;
            float iw = w - 24f;

            AmethystStyle.FillRounded(_win, A(p.Window, 0.97f), 12);
            AmethystStyle.StrokeRounded(_win, A(p.Accent, 0.35f), 12, 1);

            Rect head = new Rect(_win.x, _win.y, w, 30f);
            _title.normal.textColor = p.Text;
            GUI.Label(new Rect(x0, _win.y + 6f, w - 110f, 20f), AmethystText.T("MusicPlayer"), _title);
            
            AmethystStyle.Fill(new Rect(x0, _win.y + 28f, iw, 1f), A(p.Accent, 0.20f));

            Rect rfs = new Rect(_win.x + w - 76f, _win.y + 6f, 26f, 18f);
            if (Hover(rfs)) AmethystStyle.FillRounded(rfs, A(p.Accent, 0.25f), 5);
            AmethystIcons.Draw(AmethystIcon.Tune, new Rect(rfs.x + 6f, rfs.y + 2f, 14f, 14f), Hover(rfs) ? p.Text : p.Muted);
            if (GUI.Button(rfs, GUIContent.none, GUIStyle.none))
            {
                Scan();
                _index = Mathf.Clamp(_index, -1, Mathf.Max(0, _tracks.Count - 1));
                SetStatus(AmethystText.T("MusicRefreshed"), 2.5f);
            }

            Rect cls = new Rect(_win.x + w - 28f, _win.y + 6f, 20f, 17f);
            if (Hover(cls)) AmethystStyle.FillRounded(cls, A(new Color(0.9f, 0.3f, 0.3f), 0.25f), 6);
            AmethystIcons.Draw(AmethystIcon.Close, new Rect(cls.x + 3f, cls.y + 1.5f, 14f, 14f), new Color(0.95f, 0.5f, 0.5f));
            if (GUI.Button(cls, GUIContent.none, GUIStyle.none)) { _visible = false; return; }

            HandleDrag(head);

            float y = _win.y + 38f;
            
            _now.normal.textColor = p.Accent;
            GUI.Label(new Rect(x0, y, iw, 20f), StatusText(), _now);
            y += 22f;

            SeekBar(x0, y, iw, p);
            y += 30f;

            float bh = 28f, gap = 5f;
            float bwPrev = 70f, bwPlay = 88f, bwNext = 70f, bwStop = 56f, bwRep = 92f;
            float rowW = bwPrev + bwPlay + bwNext + bwStop + bwRep + gap * 4f;
            float bx = _win.x + (w - rowW) * 0.5f;
            if (Btn(bx, y, bwPrev, bh, AmethystText.T("MusicPrev"), p)) Prev();
            bx += bwPrev + gap;
            bool playing = _source != null && _source.clip != null && _source.isPlaying;
            if (Btn(bx, y, bwPlay, bh, playing ? AmethystText.T("MusicPause") : AmethystText.T("MusicPlay"), p, true)) PlayPause();
            bx += bwPlay + gap;
            if (Btn(bx, y, bwNext, bh, AmethystText.T("MusicNext"), p)) Next();
            bx += bwNext + gap;
            if (Btn(bx, y, bwStop, bh, AmethystText.T("MusicStop"), p)) Stop();
            bx += bwStop + gap;
            
            if (Btn(bx, y, bwRep, bh, RepeatLabel(), p)) CycleRepeat();
            y += bh + 12f;

            _muted.normal.textColor = p.Muted;
            GUI.Label(new Rect(x0, y, 44f, 20f), AmethystText.T("MusicVolume"), _muted);
            VolumeSlider(x0 + 46f, y, iw - 46f, p);
            y += 26f;

            _muted.normal.textColor = A(p.Text, 0.5f);
            GUI.Label(new Rect(x0, y, iw, 16f),
                AmethystText.T("MusicCountLabel", _tracks.Count) + "   ·   " + MusicDir, _muted);
            y += 20f;

            Rect body = new Rect(_win.x + 8f, y, w - 16f, h - (y - _win.y) - 8f);
            if (_names.Count == 0)
            {
                _muted.normal.textColor = A(p.Muted, 0.9f);
                GUI.Label(new Rect(body.x + 4f, body.y + 4f, body.width - 8f, 60f),
                    AmethystText.T("MusicEmpty"), _muted);
                return;
            }

            float rowH = 22f;
            float total = _names.Count * rowH;
            float maxScroll = Mathf.Max(0f, total - body.height);
            Event e = Event.current;
            if (e != null && e.type == EventType.ScrollWheel && body.Contains(e.mousePosition))
            {
                _scroll += e.delta.y * 18f;
                e.Use();
            }
            _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);

            GUI.BeginGroup(body);
            float ry = -_scroll;
            for (int i = 0; i < _names.Count; i++)
            {
                if (ry + rowH >= 0f && ry <= body.height)
                {
                    Rect rr = new Rect(0f, ry, body.width, rowH);
                    bool hov = e != null && rr.Contains(e.mousePosition);
                    bool cur = i == _index;
                    if (cur)
                    {
                        AmethystStyle.FillRounded(rr, A(p.Accent, 0.20f), 5);
                        AmethystStyle.FillRounded(new Rect(0f, ry + 5f, 3f, rowH - 10f), p.Accent, 1);
                    }
                    else if (hov) AmethystStyle.FillRounded(rr, A(p.Accent, 0.09f), 5);

                    _muted.normal.textColor = A(p.Text, cur ? 0.55f : 0.35f);
                    TextAnchor al = _muted.alignment;
                    _muted.alignment = TextAnchor.MiddleRight;
                    GUI.Label(new Rect(4f, ry, 22f, rowH), (i + 1).ToString("00"), _muted);
                    _muted.alignment = al;

                    _row.normal.textColor = cur ? p.Text : A(p.Text, 0.82f);
                    GUI.Label(new Rect(32f, ry, body.width - 40f, rowH), _names[i], _row);
                    if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) Play(i);
                }
                ry += rowH;
            }
            GUI.EndGroup();

            if (maxScroll > 1f)
            {
                float th = Mathf.Max(24f, body.height * (body.height / total));
                float ty = body.y + (body.height - th) * (_scroll / maxScroll);
                AmethystStyle.FillRounded(new Rect(body.xMax - 3f, ty, 3f, th), A(p.Accent, 0.5f), 1);
            }
        }
        finally { GUI.matrix = mtx; }
    }
}
