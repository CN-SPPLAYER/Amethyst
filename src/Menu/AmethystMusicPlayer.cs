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

internal enum MusicRepeatMode { Off, All, One }

public sealed partial class AmethystMusicPlayer : MonoBehaviour
{
    private static readonly string[] Exts = { "*.mp3", "*.wav", "*.ogg" };

    private const int MaxSeconds = 20 * 60;

    private AudioSource _source;
    private readonly List<string> _tracks = new List<string>();
    private readonly List<string> _names = new List<string>();

    private readonly Dictionary<string, Pcm> _cache = new Dictionary<string, Pcm>();
    private readonly object _gate = new object();

    private int _index = -1;
    private int _loadGen;
    private bool _loading;
    private bool _visible;
    private float _volume = 0.7f;
    private MusicRepeatMode _repeat = MusicRepeatMode.All;
    
    private bool _started;
    private bool _paused;
    private string _status = string.Empty;
    private float _statusUntil;

    private sealed class Pcm
    {
        public float[] Samples;
        public int Channels;
        public int Frequency;
    }

    private float[] _cur;
    private int _curChannels = 2;
    private int _curFreq = 44100;
    private int _curTotal;
    private volatile int _readPos;

    private Rect _win = new Rect(80f, 80f, 430f, 404f);
    private GUIStyle _title, _row, _muted, _btn, _now;
    private bool _built;
    private float _scroll;
    private bool _dragging;

    internal static AmethystMusicPlayer _self;
    internal static bool IsOpen => _self != null && _self._visible;

    internal bool IsActuallyPlaying
    {
        get
        {
            try
            {
                if (_source == null || _source.clip == null) return false;
                if (!_source.isPlaying) return false;
                return _source.volume > 0.0001f && _volume > 0.0001f;
            }
            catch { return false; }
        }
    }

    internal static string MusicDir => Path.Combine(AmethystPlatform.DataDir, "Music");

    private void Awake()
    {
        _self = this;

        try
        {
            float sx = AmethystConfig.MusicWinX.Value;
            float sy = AmethystConfig.MusicWinY.Value;
            if (sx >= 0f && sy >= 0f) _win = new Rect(sx, sy, _win.width, _win.height);
        }
        catch { }

        try
        {
            if (AmethystConfig.MusicVolume != null)
                _volume = Mathf.Clamp01(AmethystConfig.MusicVolume.Value);
        }
        catch { }

        try
        {
            if (AmethystConfig.MusicRepeat != null)
                _repeat = ParseRepeat(AmethystConfig.MusicRepeat.Value);
        }
        catch { }

        _source = gameObject.AddComponent<AudioSource>();
        
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.dopplerLevel = 0f;
        _source.bypassEffects = true;
        _source.bypassListenerEffects = true;
        _source.bypassReverbZones = true;
        _source.panStereo = 0f;
        _source.pitch = 1f;
        _source.volume = _volume;
        
        _source.loop = RepeatOne;
        Scan();
    }

    private void OnDestroy()
    {
        try { if (_self == this) _self = null; } catch { }
    }

    private void Update()
    {
        try
        {
            
            try { AmethystGameMusicMute.Tick(); } catch { }

            if (AmethystConfig.MusicKey.Value != KeyCode.None
                && Input.GetKeyDown(AmethystConfig.MusicKey.Value)
                && GUIUtility.keyboardControl == 0)
            {
                _visible = !_visible;
                if (_visible) Scan();
            }

            if (!_visible) return;

            if (GUIUtility.keyboardControl == 0)
            {
                if (AmethystConfig.MusicNextKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.MusicNextKey.Value)) Next();
                else if (AmethystConfig.MusicPrevKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.MusicPrevKey.Value)) Prev();
                else if (AmethystConfig.MusicPlayPauseKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.MusicPlayPauseKey.Value)) PlayPause();
            }

            if (_started && !_paused && !_loading && _source != null && _source.clip != null
                && !_source.isPlaying && !_source.loop && _source.time <= 0.01f)
            {
                bool last = _index >= _tracks.Count - 1;
                if (_repeat == MusicRepeatMode.All || !last) Next();
                else
                {
                    Stop();
                    SetStatus(AmethystText.T("MusicFinished"), 3f);
                }
            }
        }
        catch { }
    }

    [HideFromIl2Cpp]
    internal void Scan()
    {
        _tracks.Clear();
        _names.Clear();

        string dir = MusicDir;
        try
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                SetStatus(AmethystText.T("MusicFolderCreated"), 4f);
                return;
            }

            foreach (string ext in Exts)
            {
                try
                {
                    foreach (string f in Directory.GetFiles(dir, ext))
                        _tracks.Add(f);
                }
                catch { }
            }

            _tracks.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string t in _tracks) _names.Add(Path.GetFileNameWithoutExtension(t));
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[Music] scan failed: {ex.Message}");
        }
    }

    private void SetStatus(string s, float life)
    {
        _status = s ?? string.Empty;
        _statusUntil = Time.unscaledTime + life;
    }

    private string StatusText()
    {
        if (Time.unscaledTime < _statusUntil && !string.IsNullOrEmpty(_status)) return _status;
        return TrackLine();
    }

    private string TrackLine()
    {
        string name = _index >= 0 && _index < _names.Count ? _names[_index] : string.Empty;
        if (string.IsNullOrEmpty(name)) return string.Empty;

        bool playing = _source != null && _source.clip != null && _source.isPlaying;
        return (playing ? AmethystText.T("MusicNowPlaying") : AmethystText.T("MusicPaused")) + " " + name;
    }

    internal void Play(int index)
    {
        if (index < 0 || index >= _tracks.Count) return;
        _index = index;
        string path = _tracks[index];

        lock (_gate)
        {
            if (_cache.TryGetValue(path, out Pcm p) && p != null)
            {
                PlayPcm(p);
                return;
            }
        }

        _loadGen++;
        try { StartCoroutine(LoadAndPlay(path, _loadGen).WrapToIl2Cpp()); }
        catch { }
    }

    [HideFromIl2Cpp]
    private IEnumerator LoadAndPlay(string path, int gen)
    {
        _loading = true;
        
        SetStatus(AmethystText.T("MusicLoading") + " " + Path.GetFileNameWithoutExtension(path), 15f);

        Pcm pcm = null;
        string error = null;
        bool done = false;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                Pcm p = Decode(path);
                if (gen == _loadGen) pcm = p;
            }
            catch (Exception ex) { error = ex.Message; }
            finally { done = true; }
        });

        while (!done) yield return null;

        _loading = false;
        if (gen != _loadGen) yield break;   

        if (pcm == null)
        {
            AmethystPlugin.Logger?.LogWarning($"[Music] decode failed {Path.GetFileName(path)}: {error}");
            SetStatus(error == "too long" ? AmethystText.T("MusicTooLong") : AmethystText.T("MusicLoadFailed"), 4f);
            yield break;
        }

        lock (_gate)
        {
            
            _cache.Clear();
            _cache[path] = pcm;
        }

        PlayPcm(pcm);
    }

    private static Pcm Decode(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        float[] samples;
        int channels;

        int rate = AudioSettings.outputSampleRate;

        if (ext == ".ogg")
        {
            using var vr = new NVorbis.VorbisReader(path);
            
            if (vr.TotalTime.TotalSeconds > MaxSeconds)
                throw new InvalidOperationException("too long");
            channels = vr.Channels;
            samples = Drain(vr, rate);
        }
        else
        {
            
            using WaveStream reader = ext == ".mp3"
                ? (WaveStream)new Mp3FileReader(path)
                : new WaveFileReader(path);

            if (reader.TotalTime.TotalSeconds > MaxSeconds)
                throw new InvalidOperationException("too long");

            channels = reader.WaveFormat.Channels;
            ISampleProvider prov = reader.ToSampleProvider();
            
            if (reader.WaveFormat.SampleRate != rate)
                prov = new WdlResamplingSampleProvider(prov, rate);
            samples = Drain(prov);
        }

        return new Pcm { Samples = samples, Channels = channels, Frequency = rate };
    }

    private static float[] Drain(ISampleProvider prov)
    {
        var buf = new List<float>();
        float[] chunk = new float[1 << 16];
        int n;
        while ((n = prov.Read(chunk, 0, chunk.Length)) > 0)
        {
            for (int i = 0; i < n; i++) buf.Add(chunk[i]);
        }
        return buf.ToArray();
    }

    private static float[] Drain(NVorbis.VorbisReader vr, int rate)
    {
        var buf = new List<float>();
        float[] chunk = new float[1 << 16];
        int n;
        while ((n = vr.ReadSamples(chunk, 0, chunk.Length)) > 0)
        {
            for (int i = 0; i < n; i++) buf.Add(chunk[i]);
        }
        float[] s = buf.ToArray();
        
        if (vr.SampleRate != rate && s.Length > 0 && vr.SampleRate > 0)
            s = ResampleLinear(s, vr.SampleRate, rate);
        return s;
    }

    private static float[] ResampleLinear(float[] src, int from, int to)
    {
        if (from == to) return src;
        int outLen = (int)((long)src.Length * to / from);
        var dst = new float[outLen];
        double step = (double)from / to;
        for (int i = 0; i < outLen; i++)
        {
            double pos = i * step;
            int i0 = (int)pos;
            int i1 = i0 + 1 < src.Length ? i0 + 1 : src.Length - 1;
            float frac = (float)(pos - i0);
            dst[i] = src[i0] + (src[i1] - src[i0]) * frac;
        }
        return dst;
    }

    private void PlayPcm(Pcm p)
    {
        if (_source == null || p == null || p.Samples == null) return;
        try
        {
            
            _source.Stop();
            if (_source.clip != null)
            {
                try { UnityEngine.Object.Destroy(_source.clip); } catch { }
                _source.clip = null;
            }

            _cur = p.Samples;
            _curChannels = p.Channels;
            _curFreq = p.Frequency;
            _curTotal = p.Samples.Length / p.Channels;
            _readPos = 0;

            AudioClip clip = AudioClip.Create(
                Path.GetFileNameWithoutExtension(_tracks[_index]),
                _curTotal, _curChannels, _curFreq, true,
                (Action<Il2CppStructArray<float>>)PcmReader,
                (Action<int>)PcmSetPos);

            _source.clip = clip;

            _source.loop = RepeatOne;
            _source.volume = _volume;
            
            _source.Play();
            _started = true;
            _paused = false;
            
            _seekDragging = false;
            _seekTarget = 0f;
            
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[Music] play failed: {ex.Message}");
            SetStatus(AmethystText.T("MusicLoadFailed"), 3f);
        }
    }

    private void PcmReader(Il2CppStructArray<float> data)
    {
        try
        {
            int n = data.Length;
            float[] cur = _cur;
            if (cur == null || cur.Length == 0)
            {
                for (int i = 0; i < n; i++) data[i] = 0f;
                return;
            }

            int len = cur.Length;
            int pos = _readPos;

            for (int i = 0; i < n; i++)
            {
                int s = pos + i;
                data[i] = s >= 0 && s < len ? cur[s] : 0f;
            }

            _readPos = pos + n;
        }
        catch { }
    }

    private void PcmSetPos(int frame)
    {
        try
        {
            int ch = Mathf.Max(1, _curChannels);
            int len = _cur != null ? _cur.Length : 0;
            _readPos = Mathf.Clamp(frame * ch, 0, Mathf.Max(0, len));
        }
        catch { }
    }

    internal void PlayPause()
    {
        if (_source == null) return;
        try
        {
            if (_source.isPlaying) { _source.Pause(); _paused = true; return; }
            if (_source.clip != null) { _source.UnPause(); _paused = false; return; }
            Play(_index >= 0 ? _index : 0);
        }
        catch { }
    }

    internal void Stop()
    {
        try
        {
            if (_source == null) return;
            _source.Stop();
            _started = false;
            _paused = false;
            if (_source.clip != null)
            {
                try { UnityEngine.Object.Destroy(_source.clip); } catch { }
                _source.clip = null;
            }
        }
        catch { }
    }

    internal void Next()
    {
        if (_tracks.Count == 0) return;
        Play((_index + 1) % _tracks.Count);
    }

    internal void Prev()
    {
        if (_tracks.Count == 0) return;
        Play(_index <= 0 ? _tracks.Count - 1 : _index - 1);
    }

    private bool RepeatOne => _repeat == MusicRepeatMode.One;

    private static MusicRepeatMode ParseRepeat(string v)
    {
        if (string.Equals(v, "One", StringComparison.OrdinalIgnoreCase)) return MusicRepeatMode.One;
        if (string.Equals(v, "Off", StringComparison.OrdinalIgnoreCase)) return MusicRepeatMode.Off;
        return MusicRepeatMode.All; 
    }

    private void CycleRepeat()
    {
        _repeat = _repeat == MusicRepeatMode.All ? MusicRepeatMode.One
                 : _repeat == MusicRepeatMode.One ? MusicRepeatMode.Off
                 : MusicRepeatMode.All;
        try
        {
            if (AmethystConfig.MusicRepeat != null)
                AmethystConfig.MusicRepeat.Value = _repeat.ToString();
        }
        catch { }
        
        if (_source != null && _source.clip != null) _source.loop = RepeatOne;
        SetStatus(RepeatLabel(), 1.5f);
    }

    private string RepeatLabel()
    {
        switch (_repeat)
        {
            case MusicRepeatMode.One: return AmethystText.T("MusicLoopOne");
            case MusicRepeatMode.All: return AmethystText.T("MusicLoopAll");
            default: return AmethystText.T("MusicLoopOff");
        }
    }




    private void HandleDrag(Rect head)
    {
        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.MouseDown && head.Contains(e.mousePosition))
        {
            _dragging = true;
            _dragOff = new Vector2(e.mousePosition.x - _win.x, e.mousePosition.y - _win.y);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && _dragging)
        {
            _win.x = e.mousePosition.x - _dragOff.x;
            _win.y = e.mousePosition.y - _dragOff.y;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && _dragging)
        {
            _dragging = false;
            
            try
            {
                AmethystConfig.MusicWinX.Value = _win.x;
                AmethystConfig.MusicWinY.Value = _win.y;
            }
            catch { }
            e.Use();
        }
    }

    private Vector2 _dragOff;

    private void VolumeSlider(float x, float y, float w, AmethystPalette p)
    {
        const float valueW = 40f;
        Rect minus = new Rect(x, y - 2f, 18f, 20f);
        Rect plus = new Rect(x + w - valueW - 24f, y - 2f, 18f, 20f);
        Rect track = new Rect(minus.xMax + 6f, y + 5f, plus.x - minus.xMax - 12f, 8f);
        Rect hit = new Rect(track.x - 8f, track.y - 9f, track.width + 16f, 26f);
        Event e = Event.current;

        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
        {
            _volDragging = true;
            e.Use();
        }
        if (_volDragging && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                ApplyVolume(Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _volDragging = false;
                SaveVolume();
                e.Use();
            }
        }

        if (StepBtn(minus, "-", p)) { ApplyVolume(_volume - 0.01f); SaveVolume(); }
        if (StepBtn(plus, "+", p)) { ApplyVolume(_volume + 0.01f); SaveVolume(); }

        float t = Mathf.Clamp01(_volume);
        float kx = track.x + track.width * t;
        AmethystStyle.FillRounded(track, p.Button, 4);
        AmethystStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        AmethystStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        AmethystStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);

        _muted.normal.textColor = p.Text;
        TextAnchor al = _muted.alignment;
        _muted.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(x + w - valueW, y - 1f, valueW, 20f), Mathf.RoundToInt(_volume * 100f) + "%", _muted);
        _muted.alignment = al;
    }

    private void ApplyVolume(float v)
    {
        _volume = Mathf.Clamp01(v);
        try { if (_source != null) _source.volume = _volume; } catch { }
    }

    private void SaveVolume()
    {
        try { if (AmethystConfig.MusicVolume != null) AmethystConfig.MusicVolume.Value = _volume; }
        catch { }
    }

    private bool _volDragging;
    private bool _seekDragging;

    private float _seekTarget;

    private void SeekBar(float x, float y, float w, AmethystPalette p)
    {
        float dur = (_source != null && _source.clip != null) ? _source.clip.length : 0f;

        float cur = (_source != null && _source.clip != null) ? _source.time : 0f;
        Rect track = new Rect(x, y + 2f, w, 8f);
        Rect hit = new Rect(track.x - 8f, track.y - 8f, track.width + 16f, 22f);
        Event e = Event.current;

        if (e != null)
        {
            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition) && dur > 0.001f)
            {
                _seekDragging = true;
                _seekTarget = PositionToSeconds(track, e.mousePosition.x, dur);
                e.Use();
            }
            else if (_seekDragging && e.type == EventType.MouseDrag)
            {
                _seekTarget = PositionToSeconds(track, e.mousePosition.x, dur);
                e.Use();
            }
            else if (_seekDragging && (e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow))
            {
                
                ApplySeek(_seekTarget, dur);
                _seekDragging = false;
                e.Use();
            }
        }

        float shown = _seekDragging ? _seekTarget : cur;
        float t = dur > 0.001f ? Mathf.Clamp01(shown / dur) : 0f;
        float kx = track.x + track.width * t;
        AmethystStyle.FillRounded(track, p.Button, 4);
        AmethystStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        AmethystStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        AmethystStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);

        _muted.normal.textColor = A(p.Text, 0.45f);
        TextAnchor al = _muted.alignment;
        _muted.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(track.x, track.y + 10f, 60f, 14f), FmtTime(shown), _muted);
        _muted.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(track.xMax - 60f, track.y + 10f, 60f, 14f), FmtTime(dur), _muted);
        _muted.alignment = al;
    }

    private static float PositionToSeconds(Rect track, float mouseX, float dur)
        => Mathf.Clamp01((mouseX - track.x) / track.width) * dur;

    private void ApplySeek(float seconds, float dur)
    {
        if (dur <= 0.001f || _source == null || _source.clip == null) return;
        try
        {
            float target = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, dur - 0.05f));
            _source.time = target;
        }
        catch { }
    }

    private static string FmtTime(float sec)
    {
        if (sec < 0f || float.IsNaN(sec) || float.IsInfinity(sec)) sec = 0f;
        int s = Mathf.FloorToInt(sec);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    private bool StepBtn(Rect r, string label, AmethystPalette p)
    {
        bool hov = Hover(r);
        AmethystStyle.FillRounded(r, hov ? A(p.ButtonHover, 0.95f) : A(p.Button, 0.85f), 5);
        AmethystStyle.StrokeRounded(r, A(p.Accent, hov ? 0.7f : 0.3f), 5, 1);
        _btn.normal.textColor = p.Text;
        GUI.Label(r, label, _btn);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    private bool Btn(float x, float y, float w, float h, string label, AmethystPalette p, bool accent = false)
    {
        Rect r = new Rect(x, y, w, h);
        bool hov = Hover(r);
        if (accent)
            AmethystStyle.FillRounded(r, A(p.Accent, hov ? 0.92f : 0.85f), 7);
        else
            AmethystStyle.FillRounded(r, hov ? A(p.ButtonHover, 0.95f) : A(p.Button, 0.9f), 6);
        AmethystStyle.StrokeRounded(r, A(p.Accent, hov ? 0.8f : 0.35f), 6, 1);
        _btn.normal.textColor = accent ? Color.white : p.Text;
        GUI.Label(r, label, _btn);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    private static bool Hover(Rect r) => Event.current != null && r.Contains(Event.current.mousePosition);

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
