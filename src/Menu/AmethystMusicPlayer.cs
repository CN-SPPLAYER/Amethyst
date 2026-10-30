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

// 循环模式：
//   Off = 顺序播放，整张歌单放完就停（不回到第一首）
//   All = 列表循环，最后一首放完接回第一首
//   One = 单曲循环，当前这首反复播放
internal enum MusicRepeatMode { Off, All, One }

// 音乐播放器：扫描「游戏目录\Amethyst\Music」下的音频文件并在游戏内播放。
//
// ⚠ 为什么不用 UnityWebRequestMultimedia.GetAudioClip（重要教训）：
//   第一版就是用它，结果**加载半天也没声音**。翻 LogOutput.log 才发现：
//     MissingMethodException: 'Void DownloadHandlerAudioClip..ctor(String, AudioType)'
//       at UnityWebRequestMultimedia.GetAudioClip(String, AudioType)
//   游戏的 IL2CPP 产物把 DownloadHandlerAudioClip 的构造函数**裁掉了**，
//   这个 API 在游戏里根本拿不到 AudioClip —— 不是代码写错，是这条路被封死了。
//   所以改为：C# 侧解码出 float[] PCM，再用 `AudioClip.Create(..., stream: true, 回调)` 交给 Unity。
//
// ⚠ 解码工作放在 ThreadPool 上做：一首 5 分钟的歌解码要几百毫秒~几秒，
//   放在主线程会直接卡住游戏。
//
// 解码依赖 NAudio（MP3/WAV）与 NVorbis（OGG），均为 MIT 许可的第三方开源库，
// 许可证全文见 Amethyst\libs\THIRD-PARTY-NOTICES.txt。
public sealed class AmethystMusicPlayer : MonoBehaviour
{
    private static readonly string[] Exts = { "*.mp3", "*.wav", "*.ogg" };

    // 单曲时长上限（秒）。解码结果是整首歌的 float[]：立体声 48kHz 下每秒约 384 KB，
    // 4 分钟约 92 MB。设 20 分钟上限 ≈ 460 MB 封顶，避免超长文件吃光内存。
    private const int MaxSeconds = 20 * 60;

    private AudioSource _source;
    private readonly List<string> _tracks = new List<string>();
    private readonly List<string> _names = new List<string>();

    // 解码结果缓存（路径 -> PCM）。整首歌解码后是很大的 float[]，
    // 所以只保留当前曲，切歌时丢掉旧的。
    private readonly Dictionary<string, Pcm> _cache = new Dictionary<string, Pcm>();
    private readonly object _gate = new object();

    private int _index = -1;
    private int _loadGen;
    private bool _loading;
    private bool _visible;
    private float _volume = 0.7f;
    private MusicRepeatMode _repeat = MusicRepeatMode.All;
    // 区分「暂停」与「自然播完」：两者都是 isPlaying=false + time≈0，
    // 只看这两点会把"刚起播就按暂停"误判成播完、直接跳下一首。
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

    // 当前正在播放的 PCM（供 PCM 读取回调取用）
    private float[] _cur;
    private int _curChannels = 2;
    private int _curFreq = 44100;
    private int _curTotal;
    private volatile int _readPos;

    // 说明：**没有** `_reachedEnd` 这类"我们自己的曲尾标记"。
    // 曲尾由 Unity 自己判断（`_source.loop` 为 false 时它会停止并把 time 归零），
    // 原因见 `PcmReader` 的注释：Unity 会预读约 0.5s 缓冲，
    // 任何"按 `_readPos` 判曲尾"的做法都会提前 0.5s 掐断曲尾。

    private Rect _win = new Rect(80f, 80f, 430f, 404f);
    private GUIStyle _title, _row, _muted, _btn, _now;
    private bool _built;
    private float _scroll;
    private bool _dragging;

    internal static AmethystMusicPlayer _self;
    internal static bool IsOpen => _self != null && _self._visible;

    // 播放器此刻是否真的在出声（供 AmethystGameMusicMute 判断要不要压掉游戏音乐）。
    // 条件：有 clip、没被暂停、音量不为 0。
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

    // 音频目录：与封禁名单同级 —— <游戏目录>\Amethyst\Music
    internal static string MusicDir => Path.Combine(AmethystPlatform.DataDir, "Music");

    private void Awake()
    {
        _self = this;

        // 恢复上次的窗口位置（-1 表示还没保存过，用默认位置）。
        // 这里只读原始值，真正的越界钳制在 DrawGuiInner 每帧做 —— 因为屏幕尺寸
        // 要等 IMGUI 阶段才知道，而且在缩放矩阵下算才准。
        try
        {
            float sx = AmethystConfig.MusicWinX.Value;
            float sy = AmethystConfig.MusicWinY.Value;
            if (sx >= 0f && sy >= 0f) _win = new Rect(sx, sy, _win.width, _win.height);
        }
        catch { }

        // 恢复上次音量
        try
        {
            if (AmethystConfig.MusicVolume != null)
                _volume = Mathf.Clamp01(AmethystConfig.MusicVolume.Value);
        }
        catch { }

        // 恢复上次循环模式
        try
        {
            if (AmethystConfig.MusicRepeat != null)
                _repeat = ParseRepeat(AmethystConfig.MusicRepeat.Value);
        }
        catch { }

        _source = gameObject.AddComponent<AudioSource>();
        // 2D 播放：不做空间衰减、不进混响，否则音乐会跟着角色位置变声。
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.dopplerLevel = 0f;
        _source.bypassEffects = true;
        _source.bypassListenerEffects = true;
        _source.bypassReverbZones = true;
        _source.panStereo = 0f;
        _source.pitch = 1f;
        _source.volume = _volume;
        // 循环 / 曲尾交给 Unity（见 PlayPcm 的说明）
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
            // 播放器一出声就压掉游戏自己的音乐（大厅音乐 / 主界面音乐），停播即恢复。
            try { AmethystGameMusicMute.Tick(); } catch { }

            if (AmethystConfig.MusicKey.Value != KeyCode.None
                && Input.GetKeyDown(AmethystConfig.MusicKey.Value)
                && GUIUtility.keyboardControl == 0)
            {
                _visible = !_visible;
                if (_visible) Scan();
            }

            if (!_visible) return;

            // 播放器打开时才接管这些键，且必须没有输入框聚焦（聊天打字不触发）
            if (GUIUtility.keyboardControl == 0)
            {
                if (AmethystConfig.MusicNextKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.MusicNextKey.Value)) Next();
                else if (AmethystConfig.MusicPrevKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.MusicPrevKey.Value)) Prev();
                else if (AmethystConfig.MusicPlayPauseKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.MusicPlayPauseKey.Value)) PlayPause();
            }

            // 一首放完：Unity 对 stream clip 会在片尾自动停止并把 time 归零。
            // 单曲循环时 `_source.loop` 为 true，根本不会走到这里。
            // `_paused` 用来排除"手动暂停"，否则刚起播就暂停会被当成播完。
            //
            // ⚠ 第 236 轮：**回到这个判据**（不再用自建 `_reachedEnd`）。
            //   Unity 会预读约 0.5s 缓冲，`_readPos` 越过曲尾时"听到的"还没到片尾，
            //   按 `_readPos` 判曲尾会提前 0.5s 掐断。让 Unity 自己报告停止才是准的
            //   （参考 OnyxMenu `NocturneMusicPlayer` 的做法）。
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

    // ===== 扫描 =====
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

    // 顶部状态行文案。
    //   · 有临时状态（加载中 / 加载失败 / 已刷新…）时先显示它；
    //   · 否则：正在播放 → "正在播放 + 歌名"；有选中但暂停 → "已暂停 + 歌名"；
    //     都没有 → 空（列表里也没歌时由列表区的提示兜底）。
    //
    // ⚠ 曾经的 BUG：起播时用 SetStatus("正在播放") 当临时状态，而临时状态会**整行顶掉**，
    //   于是播起来之后顶部只显示「正在播放」、看不到歌名（暂停后临时状态过期才显出歌名）。
    //   现在起播**不再写临时状态**，这行完全由 _index + isPlaying 现算，歌名一定在。
    private string StatusText()
    {
        if (Time.unscaledTime < _statusUntil && !string.IsNullOrEmpty(_status)) return _status;
        return TrackLine();
    }

    // "正在播放 歌名" / "已暂停 歌名"；没有选中曲目时返回空
    private string TrackLine()
    {
        string name = _index >= 0 && _index < _names.Count ? _names[_index] : string.Empty;
        if (string.IsNullOrEmpty(name)) return string.Empty;

        bool playing = _source != null && _source.clip != null && _source.isPlaying;
        return (playing ? AmethystText.T("MusicNowPlaying") : AmethystText.T("MusicPaused")) + " " + name;
    }

    // ===== 传输控制 =====
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
        // 加载提示带上歌名，避免解码期间顶部只剩「加载中…」看不出在加载哪首
        SetStatus(AmethystText.T("MusicLoading") + " " + Path.GetFileNameWithoutExtension(path), 15f);

        Pcm pcm = null;
        string error = null;
        bool done = false;

        // 解码放线程池：避免大文件把游戏主线程卡住
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
        if (gen != _loadGen) yield break;   // 已经切到别的歌了，丢弃这次结果

        if (pcm == null)
        {
            AmethystPlugin.Logger?.LogWarning($"[Music] decode failed {Path.GetFileName(path)}: {error}");
            SetStatus(error == "too long" ? AmethystText.T("MusicTooLong") : AmethystText.T("MusicLoadFailed"), 4f);
            yield break;
        }

        lock (_gate)
        {
            // 只缓存当前这一首，避免 float[] 长期占内存
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

        // 统一用设备输出采样率，避免 Unity 重采样导致变调
        int rate = AudioSettings.outputSampleRate;

        if (ext == ".ogg")
        {
            using var vr = new NVorbis.VorbisReader(path);
            // 解码出来是整首歌的 float[]：4 分钟立体声约 92 MB。
            // 加个上限，避免一个超长文件把内存吃光（这里取 20 分钟）。
            if (vr.TotalTime.TotalSeconds > MaxSeconds)
                throw new InvalidOperationException("too long");
            channels = vr.Channels;
            samples = Drain(vr, rate);
        }
        else
        {
            // mp3 -> Mp3FileReader；wav -> WaveFileReader
            using WaveStream reader = ext == ".mp3"
                ? (WaveStream)new Mp3FileReader(path)
                : new WaveFileReader(path);

            if (reader.TotalTime.TotalSeconds > MaxSeconds)
                throw new InvalidOperationException("too long");

            channels = reader.WaveFormat.Channels;
            ISampleProvider prov = reader.ToSampleProvider();
            // WdlResamplingSampleProvider：纯托管重采样，不依赖 Windows Media Foundation
            // （MediaFoundationResampler 在 IL2CPP 上不一定可用）。
            if (reader.WaveFormat.SampleRate != rate)
                prov = new WdlResamplingSampleProvider(prov, rate);
            samples = Drain(prov);
        }

        return new Pcm { Samples = samples, Channels = channels, Frequency = rate };
    }

    // 把 ISampleProvider 全部读出来（先攒到 List 再合并，避免预知长度）
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
        // NVorbis 不带重采样，采样率不同就做一次简单的线性插值
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
            // 换曲前销毁旧 clip，否则 AudioClip 会一直堆积
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

            // stream: true —— 不把整首歌拷进 Unity，靠回调按需喂 PCM
            AudioClip clip = AudioClip.Create(
                Path.GetFileNameWithoutExtension(_tracks[_index]),
                _curTotal, _curChannels, _curFreq, true,
                (Action<Il2CppStructArray<float>>)PcmReader,
                (Action<int>)PcmSetPos);

            _source.clip = clip;

            // ⚠ 第 236 轮：**回到 `_source.loop = RepeatOne`**。
            //
            //   第 233 轮为了消除 seek 竞态，曾把它钉死为 true 并用自建 `_reachedEnd`
            //   判曲尾。但 Unity 会**预读约 0.5s 缓冲**：`_readPos` 越过曲尾时，
            //   实际听到的声音还差约 0.5s 才到片尾 ⇒ 自建曲尾标记会把每首歌的
            //   最后 0.5s 掐掉。
            //
            //   参考 OnyxMenu `NocturneMusicPlayer`：它让 Unity 自己负责循环与曲尾
            //   （`_source.loop = _loop`，并在 `PcmSetPositionCallback` 里把
            //   `_rawReadPos = newFrame` 同步回去）。本实现照此：
            //     · 循环 / 曲尾：交给 Unity（`loop` + `Update` 里的 isPlaying 判据）；
            //     · `PcmSetPos` 恢复为"接收 Unity 的位置"（帧 → 交错样本）；
            //     · seek 时用 `Stop()+Play()` 清掉预读缓冲，避免残留旧音频。
            _source.loop = RepeatOne;
            _source.volume = _volume;
            // 不写 `_source.time`：新 clip 的播放头本来就是 0，写它只会多触发一次
            // 异步位置回调。
            _source.Play();
            _started = true;
            _paused = false;
            // 换曲时清掉可能残留的拖动状态，避免进度条一直显示"预览位置"
            _seekDragging = false;
            _seekTarget = 0f;
            // 这里**不要**再 SetStatus("正在播放")：那会把整行顶掉，导致看不到歌名。
            // 顶部那行由 StatusText()/TrackLine() 现算，起播后自然变成「正在播放 + 歌名」。
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[Music] play failed: {ex.Message}");
            SetStatus(AmethystText.T("MusicLoadFailed"), 3f);
        }
    }

    // Unity 每次需要一段 PCM 时调用：从 _cur 的 _readPos 处填充 data。
    //
    // ⚠ 单位必须严格区分（第 232 轮修复）：
    //     `_cur`        = 交错样本数组，长度 = 帧数 × 声道数
    //     `_readPos`    = **交错样本**下标（直接索引 `_cur`）
    //     `_curTotal`   = **帧数**（`_cur.Length / channels`），只用于 AudioClip.Create 的 lengthSamples
    //
    //   旧实现用 `_readPos >= _curTotal` 判断回绕 —— 拿"交错样本下标"去比"帧数"，
    //   立体声下差 2 倍 ⇒ 单曲循环在**歌曲一半**处就跳回开头：
    //   先"提前重复一次"，再放一半又"重新播放一遍"（用户实测到的正是这个）。
    //
    // ⚠ 第 236 轮：`_readPos` **只是缓存/镜像**，真正的播放头由 Unity 掌握。
    //
    //   Unity 在需要数据时回调本方法，并在循环 / seek 时通过 `PcmSetPos` 告知新位置。
    //   因此这里**不做任何取模/回绕判断** —— 回绕由 Unity 负责（`_source.loop`），
    //   我们越界就读静音即可。这样循环点与 Unity 的播放头严格一致，
    //   不会出现"数据先回绕、声音还没到片尾"的错位。
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

    // Unity 的 PCM 位置回调（`AudioClip.Create` 的 pcmSetPositionCallback）。
    //
    // ⚠ 第 236 轮：**恢复写 `_readPos`**，与参考实现一致
    //   （OnyxMenu `NocturneMusicPlayer.PcmSetPositionCallback`：
    //     `_rawReadPos = Math.Max(0, newFrame);`）。
    //
    //   第 233 轮把它留空，是为了避开"`AudioSource.time` setter 之后异步回调旧位置"
    //   的竞态。那个竞态的根因其实在**我们自己**：`ApplySeek` 同时写了
    //   `_source.time` 和 `_readPos`，两个写入源互相覆盖。
    //   现在 `ApplySeek` 只写 `_source.time`（唯一入口），由 Unity 回调本方法同步
    //   `_readPos` —— 单一真相，不再有竞态，也不需要 `Stop()+Play()` 强刷。
    //
    //   注意单位：Unity 传的是**帧**，`_readPos` 是**交错样本**下标。
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

    // --- 循环模式 ---
    // One = 单曲循环；All = 列表循环；Off = 播完即停。
    // 三者的区别只体现在 `PcmReader` 的回绕处理与 `Update` 的曲尾动作，
    // **不再**映射到 Unity 的 `AudioSource.loop`（该值恒为 true，见 PlayPcm）。
    private bool RepeatOne => _repeat == MusicRepeatMode.One;

    private static MusicRepeatMode ParseRepeat(string v)
    {
        if (string.Equals(v, "One", StringComparison.OrdinalIgnoreCase)) return MusicRepeatMode.One;
        if (string.Equals(v, "Off", StringComparison.OrdinalIgnoreCase)) return MusicRepeatMode.Off;
        return MusicRepeatMode.All; // "All" 或任何未知值都当列表循环
    }

    // 点击循环按钮在 Off <-> All <-> One 之间循环
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
        // 循环 / 曲尾由 Unity 负责（`loop` 为 false 时它会在片尾停止并归零）。
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

    // ===== 界面 =====
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
        // 「正在播放 + 歌名」用独立样式：比列表行更大更粗，作为窗口主标题
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

            // 标题栏：按住可拖动整窗口
            Rect head = new Rect(_win.x, _win.y, w, 30f);
            _title.normal.textColor = p.Text;
            GUI.Label(new Rect(x0, _win.y + 6f, w - 110f, 20f), AmethystText.T("MusicPlayer"), _title);
            // 顶栏分隔线，让标题区更像一条真正的标题栏
            AmethystStyle.Fill(new Rect(x0, _win.y + 28f, iw, 1f), A(p.Accent, 0.20f));

            // 刷新按钮：重新检测文件夹（游戏中加歌后点它）
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
            // 正在播放 / 状态行
            _now.normal.textColor = p.Accent;
            GUI.Label(new Rect(x0, y, iw, 20f), StatusText(), _now);
            y += 22f;

            // 进度条（可拖动定位）
            SeekBar(x0, y, iw, p);
            y += 30f;

            // 传输按钮（居中排列，播放键用主题色实心，视觉重心明确）
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
            // 循环模式：点一次切一档（单曲 → 列表 → 播完即停）
            if (Btn(bx, y, bwRep, bh, RepeatLabel(), p)) CycleRepeat();
            y += bh + 12f;

            // 音量（滑块 + 两端 −/+ 细调）
            _muted.normal.textColor = p.Muted;
            GUI.Label(new Rect(x0, y, 44f, 20f), AmethystText.T("MusicVolume"), _muted);
            VolumeSlider(x0 + 46f, y, iw - 46f, p);
            y += 26f;

            // 文件夹提示压成一行：曲目数 + 具体路径
            _muted.normal.textColor = A(p.Text, 0.5f);
            GUI.Label(new Rect(x0, y, iw, 16f),
                AmethystText.T("MusicCountLabel", _tracks.Count) + "   ·   " + MusicDir, _muted);
            y += 20f;

            // 播放列表
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

                    // 序号列（右对齐、压暗），让长歌名有个稳定的左边距
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

    // 标题栏拖动（IMGUI 每帧事件驱动：按下记偏移，拖动更新，松开结束）
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
            // 拖完就记住位置，下次打开还在原处
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

    // 音量滑块：版式照抄主界面菜单的 `AmethystMenu.Slider`
    // （圆角轨道 + 主题色已填充段 + 光晕圆钮 + 白色内钮），
    // 而不是 IMGUI 默认的 `GUI.HorizontalSlider` 灰条。
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

    // 松开滑块时把音量写进配置，下次启动还是这个音量
    private void SaveVolume()
    {
        try { if (AmethystConfig.MusicVolume != null) AmethystConfig.MusicVolume.Value = _volume; }
        catch { }
    }

    private bool _volDragging;
    private bool _seekDragging;

    // 拖动中的目标位置（秒）。拖动期间只更新这个预览值，**不碰音频**；
    // 松开鼠标才真正定位（见 SeekBar / ApplySeek）。
    private float _seekTarget;

    // 进度条：按下 → 记录目标位置（只预览）；拖动 → 更新目标位置（仍不定位）；
    // 松开 → 一次性 ApplySeek。
    //
    // ⚠ 第 232 轮：旧实现在 `MouseDrag` 里**每帧**写 `_source.time`。
    //   对流式 clip（`AudioClip.Create(stream: true)`）来说，写 `time` 会让 Unity
    //   立刻 flush 缓冲区并从新位置重新拉 PCM，每帧一次 ⇒ 持续爆音/卡顿，
    //   而且拖动途中经过曲尾附近还会触发循环回绕。现在改为松手才应用。
    private void SeekBar(float x, float y, float w, AmethystPalette p)
    {
        float dur = (_source != null && _source.clip != null) ? _source.clip.length : 0f;

        // 当前播放位置：读 Unity 的 `_source.time`。
        // （第 236 轮回到"Unity 是唯一播放头"之后，`time` 与音频数据严格一致。）
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
                // 松手才应用；即使鼠标移出窗口，也会在这里收尾（不再检查命中区域），
                // 避免 `_seekDragging` 卡住导致下一次点击行为异常。
                ApplySeek(_seekTarget, dur);
                _seekDragging = false;
                e.Use();
            }
        }

        // 拖动中显示预览位置，让滑块跟着鼠标走（而不是被实际播放位置拉回去）
        float shown = _seekDragging ? _seekTarget : cur;
        float t = dur > 0.001f ? Mathf.Clamp01(shown / dur) : 0f;
        float kx = track.x + track.width * t;
        AmethystStyle.FillRounded(track, p.Button, 4);
        AmethystStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        AmethystStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        AmethystStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);

        // 时间标签放在轨道下方，避免和上方「正在播放」那行叠在一起
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

    // 真正应用定位（只在鼠标松开时调用）。
    //
    // ⚠ 第 236 轮：**只写 `_source.time`**（唯一入口），与参考实现一致
    //   （OnyxMenu `NocturneMusicPlayer` 的 seek 就是 `_source.time = r.Seek * _cTotal`）。
    //
    //   为什么不再写 `_readPos`：
    //     `_readPos` 现在是 Unity 播放头的**镜像**，由 `PcmSetPos` 回调维护。
    //     若我们在这里也写一份，就又有两个写入源 —— 那正是第 232/233 轮
    //     "先播 0.5 秒再退回重播"的根因。
    //     写 `_source.time` 后 Unity 会自己调用 `PcmSetPos(新帧)`，
    //     `_readPos` 随之同步，落点精确且只有一条路径。
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

    // 细调按钮（− / +），和主菜单里的同款
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
