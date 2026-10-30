using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using UnityEngine;
using UnityEngine.Networking;
using Amethyst.Patches;

namespace Amethyst;

// 模组房间列表的运行时服务：
//   · 房主心跳上报（大厅 / 游戏中都上报，离开时发 close）
//   · 列表拉取与缓存（仅当菜单停留在该页签时按间隔刷新）
//   · 一键加入（先退出当前房间，再走游戏自带的"按代码加入"流程）
public sealed class AmethystRoomService : MonoBehaviour
{
    public AmethystRoomService(IntPtr ptr) : base(ptr) { }

    internal static AmethystRoomService Instance { get; private set; }

    private const int RequestTimeout = 10;

    // ---- 列表状态（UI 只读）----
    internal readonly List<AmethystRoom> Rooms = new List<AmethystRoom>();
    internal bool Loading;
    internal bool Loaded;
    internal bool Failed;
    internal string Error = string.Empty;
    internal float LastUpdated = -1f;
    internal int OnlineCount;
    internal bool Joining;
    internal string JoiningCode = string.Empty;

    private bool _getInFlight;
    private float _nextRefreshAt;
    private UnityWebRequest _fetchReq;
    private int _fetchGen;
    internal bool Visible { get; private set; }

    // ---- 上报状态 ----
    private bool _reported;
    private string _lastCode = string.Empty;
    private string _lastState = string.Empty;
    private float _nextReportAt;
    private bool _postInFlight;
    private bool _postWarned;
    private bool _closing;
    private float _nextPollAt;
    private float _backoff;
    private int _failStreak;
    private bool _authFailed;
    private bool _secretWarned;

    private void Awake() { Instance = this; }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        // 1 秒轮询一次：AllPlayerControls / GameOptions / allClients 不便宜，不能每帧读。
        float now = Time.unscaledTime;
        if (now < _nextPollAt) return;
        _nextPollAt = now + 1f;

        try { TickReport(); } catch { }
    }

    // ================= 房主上报 =================

    private void TickReport()
    {
        // 两个独立开关（第 272 轮）：
        //   RoomListEnabled       = 是否启用「房间列表」这个功能（关掉连拉取都不做）
        //   RoomListReportOwnRoom = 是否把自己的房间上报（隐私开关，默认关）
        // 之前两者被绑在一起，导致「只想看列表、不想被公开」的用户无法关闭上报。
        if (AmethystConfig.RoomListEnabled == null || !AmethystConfig.RoomListEnabled.Value)
        {
            if (_reported) CloseOwnRoom();
            return;
        }

        if (AmethystConfig.RoomListReportOwnRoom != null && !AmethystConfig.RoomListReportOwnRoom.Value)
        {
            // 关掉上报时，如果之前已经报过，主动从列表里撤下，避免残留 90 秒
            if (_reported) CloseOwnRoom();
            return;
        }

        AmongUsClient ac = AmongUsClient.Instance;
        InnerNetClient net = ac as InnerNetClient;
        if (ac == null || net == null)
        {
            if (_reported) CloseOwnRoom();
            return;
        }

        bool host = false;
        int gameId = 0;
        try
        {
            host = net.AmHost;
            gameId = net.GameId;
        }
        catch { }

        if (!host || gameId == 0)
        {
            if (_reported) CloseOwnRoom();
            return;
        }

        string code = string.Empty;
        try { code = GameCode.IntToGameName(gameId); } catch { }
        if (string.IsNullOrEmpty(code) || code.Length < 4 || code.Length > 8) return;

        if (Secret().Length == 0)
        {
            if (!_secretWarned)
            {
                _secretWarned = true;
                AmethystPlugin.Logger?.LogWarning("[RoomList] no secret configured; reporting disabled (set [RoomList] Secret)");
            }
            _reported = false;
            return;
        }

        if (_authFailed) return;

        string state = "lobby";
        try { state = net.IsGameStarted ? "ingame" : "lobby"; } catch { }

        bool changed = !string.Equals(code, _lastCode, StringComparison.Ordinal)
                    || !string.Equals(state, _lastState, StringComparison.Ordinal);
        float now = Time.unscaledTime;
        if (now < _nextReportAt && (_backoff > 0f || !changed)) return;
        if (_postInFlight) return;

        int interval = 12;
        try { interval = Mathf.Clamp(AmethystConfig.RoomListReportSeconds.Value, 8, 60); } catch { }

        _lastCode = code;
        _lastState = state;

        string json = AmethystRoomApi.BuildReport(
            code, HostName(net), state, MapName(), ModeName(),
            PlayerCount(), MaxPlayers(), PingOf(net), RegionName(), LanguageNote());
        if (string.IsNullOrEmpty(json)) return;

        // 签名和上传必须用**同一份字节**。
        byte[] body = Encoding.UTF8.GetBytes(json);
        Post(AmethystRoomApi.ReportUrl, ReportPath(), body, ok =>
        {
            if (ok)
            {
                _reported = true;
                _failStreak = 0;
                _backoff = 0f;
                _nextReportAt = Time.unscaledTime + interval;
            }
            else
            {
                // 失败退避：3s → 6s → 12s → 24s → 48s → 60s
                _failStreak++;
                float b = _failStreak <= 1 ? 3f : Mathf.Min(60f, 3f * Mathf.Pow(2f, _failStreak - 1));
                _backoff = b;
                _nextReportAt = Time.unscaledTime + b;
            }
        });
    }

    private void CloseOwnRoom()
    {
        string code = _lastCode;
        _reported = false;
        _lastCode = string.Empty;
        _lastState = string.Empty;
        _nextReportAt = 0f;
        _backoff = 0f;
        _failStreak = 0;

        if (string.IsNullOrEmpty(code) || _closing) return;
        if (_authFailed || Secret().Length == 0) return;

        string json = AmethystRoomApi.BuildClose(code);
        if (string.IsNullOrEmpty(json)) return;

        _closing = true;
        byte[] body = Encoding.UTF8.GetBytes(json);
        if (!Post(AmethystRoomApi.ReportUrl, ReportPath(), body, ok => _closing = false))
            _closing = false;
    }

    private static string HostName(InnerNetClient net)
    {
        try
        {
            ClientData h = net.GetHost();
            if (h != null && !string.IsNullOrEmpty(h.PlayerName)) return h.PlayerName;
        }
        catch { }
        try
        {
            PlayerControl lp = PlayerControl.LocalPlayer;
            if (lp != null && lp.Data != null) return lp.Data.PlayerName ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    private static int PlayerCount()
    {
        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all != null) return all.Count;
        }
        catch { }
        return 0;
    }

    private static IGameOptions Options()
    {
        try
        {
            GameOptionsManager gom = GameOptionsManager.Instance;
            if (gom == null) return null;
            return gom.CurrentGameOptions;
        }
        catch { return null; }
    }

    private static int MaxPlayers()
    {
        try
        {
            IGameOptions o = Options();
            if (o != null) return o.MaxPlayers;
        }
        catch { }
        return 0;
    }

    private static string MapName()
    {
        try
        {
            IGameOptions o = Options();
            if (o == null) return string.Empty;
            switch ((int)o.MapId)
            {
                case 0: return "Skeld";
                case 1: return "Mira";
                case 2: return "Polus";
                case 3: return "Skeld";   // Dleks（镜像 Skeld）按 Skeld 处理
                case 4: return "Airship";
                case 5: return "Fungle";
                default: return string.Empty;
            }
        }
        catch { return string.Empty; }
    }

    private static string ModeName()
    {
        try
        {
            IGameOptions o = Options();
            if (o == null) return "classic";
            return (o.GameMode == GameModes.HideNSeek || o.GameMode == GameModes.SeekFools) ? "hidenseek" : "classic";
        }
        catch { return "classic"; }
    }

    private static string RegionName()
    {
        try
        {
            string r = AmethystServerInfo.RegionName();
            if (string.IsNullOrEmpty(r)) return string.Empty;
            switch (r)
            {
                case "North America":
                case "NA": return "NorthAmerica";
                case "South America":
                case "SA": return "SouthAmerica";
                case "Europe":
                case "EU": return "Europe";
                case "Asia":
                case "AS": return "Asia";
                case "Oceania":
                case "OC": return "Oceania";
                case "Africa":
                case "AF": return "Africa";
                default: return r;
            }
        }
        catch { return string.Empty; }
    }

    private static int PingOf(InnerNetClient net)
    {
        try { return net.Ping; } catch { return 0; }
    }

    // 房间语言标记：优先用大厅的语言筛选位（GameKeywords），
    // 未设置（All）时退回房主自己的模组语言；服务端没有语言字段，
    // 因此写进会被原样回传的 note（"lang=xx"），客户端据此筛选。
    private static string LanguageNote()
    {
        string code = LanguageCode();
        return string.IsNullOrEmpty(code) ? string.Empty : "lang=" + code;
    }

    private static string LanguageCode()
    {
        try
        {
            IGameOptions o = Options();
            if (o == null) return string.Empty;

            uint k = (uint)o.Keywords;
            if ((k & (uint)GameKeywords.SChinese) != 0) return "zh";
            if ((k & (uint)GameKeywords.TChinese) != 0) return "zh";
            if ((k & (uint)GameKeywords.English) != 0) return "en";
            if ((k & (uint)GameKeywords.Russian) != 0) return "ru";
            if ((k & (uint)GameKeywords.Japanese) != 0) return "ja";
            if ((k & (uint)GameKeywords.Korean) != 0) return "ko";
            if (k == 0) return HostLanguage();
            return "other";
        }
        catch { return string.Empty; }
    }

    private static string HostLanguage()
    {
        try
        {
            string lang = AmethystConfig.Language != null ? AmethystConfig.Language.Value : string.Empty;
            if (lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh";
            if (lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase)) return "ru";
            if (lang.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en";
            return "other";
        }
        catch { return string.Empty; }
    }

    // ================= 列表拉取 =================

    // 只有「菜单打开 + 当前页签是房间列表 + 未折叠」时才允许拉取。
    // 离开页签或关闭菜单时立刻作废并中止在途请求，后台不再刷新。
    internal void SetVisible(bool visible)
    {
        if (Visible == visible) return;
        Visible = visible;

        if (!visible)
        {
            _fetchGen++;
            try { _fetchReq?.Abort(); } catch { }
            _fetchReq = null;
            _getInFlight = false;
            Loading = false;
            return;
        }

        EnsureFresh();
    }

    internal void EnsureFresh()
    {
        if (!Visible) return;
        if (AmethystConfig.RoomListEnabled == null || !AmethystConfig.RoomListEnabled.Value) return;
        if (_getInFlight) return;

        float now = Time.unscaledTime;
        if (!Loaded && !Failed) { Refresh(); return; }
        if (now >= _nextRefreshAt) Refresh();
    }

    internal void Refresh()
    {
        if (!Visible || _getInFlight) return;

        int secs = 8;
        try { secs = Mathf.Clamp(AmethystConfig.RoomListRefreshSeconds.Value, 4, 60); } catch { }

        _nextRefreshAt = Time.unscaledTime + secs;
        _getInFlight = true;
        Loading = true;
        Error = string.Empty;

        int gen = ++_fetchGen;
        try { StartCoroutine(CoFetch(gen).WrapToIl2Cpp()); }
        catch (Exception ex)
        {
            _getInFlight = false;
            Loading = false;
            Failed = true;
            Error = "start";
            LogWarn("fetch start", ex);
        }
    }

    private IEnumerator CoFetch(int gen)
    {
        if (gen != _fetchGen) yield break;

        string url = AmethystRoomApi.RoomsUrl + "?_=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        UnityWebRequest req = null;
        try
        {
            req = UnityWebRequest.Get(url);
            if (req != null)
            {
                req.timeout = RequestTimeout;
                req.SetRequestHeader("Accept", "application/json");
            }
        }
        catch { }

        if (req == null)
        {
            if (gen == _fetchGen)
            {
                _getInFlight = false;
                Loading = false;
                Failed = true;
                Error = "request";
            }
            yield break;
        }

        _fetchReq = req;
        yield return req.SendWebRequest();

        // 已离开页签/关闭菜单：丢弃结果，不覆盖新请求的状态。
        if (gen != _fetchGen)
        {
            if (_fetchReq == req) _fetchReq = null;
            try { req.Dispose(); } catch { }
            yield break;
        }

        bool ok = false;
        try
        {
            if (req.result == UnityWebRequest.Result.Success && req.downloadHandler != null)
            {
                var parsed = new List<AmethystRoom>(16);
                int count;
                string msg;
                if (AmethystRoomApi.TryParse(req.downloadHandler.text, parsed, out count, out msg))
                {
                    Rooms.Clear();
                    Rooms.AddRange(parsed);
                    OnlineCount = count > 0 ? count : Rooms.Count;
                    Loaded = true;
                    Failed = false;
                    Error = string.Empty;
                    LastUpdated = Time.unscaledTime;
                    ok = true;
                }
                else
                {
                    Error = string.IsNullOrEmpty(msg) ? "format" : msg;
                }
            }
            else if (req.responseCode == 429)
            {
                Error = "too_frequent";
                _nextRefreshAt = Time.unscaledTime + 20f;
            }
            else
            {
                Error = "network";
            }
        }
        catch { Error = "exception"; }
        finally
        {
            try { req.Dispose(); } catch { }
            if (_fetchReq == req) _fetchReq = null;
            if (!ok) Failed = true;
            Loading = false;
            _getInFlight = false;
        }
    }

    // ================= 一键加入 =================

    internal void Join(string code)
    {
        if (Joining) return;

        code = Normalize(code);
        if (!IsValidCode(code))
        {
            AmethystToast.Push(AmethystText.T("RoomListInvalidCode"), 3f);
            return;
        }

        Joining = true;
        JoiningCode = code;

        try { StartCoroutine(CoJoin(code).WrapToIl2Cpp()); }
        catch (Exception ex)
        {
            Joining = false;
            JoiningCode = string.Empty;
            LogWarn("join start", ex);
            AmethystToast.Push(AmethystText.T("RoomListJoinFailed"), AmethystText.T("RoomListJoinFailedDetail"), 3.5f, AmethystNotifyKind.Warning);
        }
    }

    internal static string Normalize(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        var sb = new StringBuilder(8);
        foreach (char raw in code.Trim().ToUpperInvariant())
        {
            char ch = raw;
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
        }
        return sb.ToString();
    }

    internal static bool IsValidCode(string code) => code != null && code.Length >= 4 && code.Length <= 8;

    private IEnumerator CoJoin(string code)
    {
        AmongUsClient ac = AmongUsClient.Instance;
        InnerNetClient net = ac as InnerNetClient;
        if (ac == null || net == null)
        {
            FailJoin("RoomListJoinFailedDetail");
            yield break;
        }

        bool inGame = false;
        try { inGame = net.GameState != InnerNetClient.GameStates.NotJoined || net.GameId != 0; } catch { }

        if (inGame)
        {
            try { ac.ExitGame(DisconnectReasons.ExitGame); }
            catch { }

            float wait = 0f;
            while (wait < 5f)
            {
                wait += Time.unscaledDeltaTime;
                bool left = false;
                try { left = net.GameState == InnerNetClient.GameStates.NotJoined; }
                catch { left = true; }
                if (left) break;
                yield return null;
            }
            yield return null;

            // ExitGame 之后单例可能被重建，重新取一次，避免拿已销毁的引用去加入。
            ac = AmongUsClient.Instance;
            net = ac as InnerNetClient;
            if (ac == null || net == null)
            {
                FailJoin("RoomListJoinFailedDetail");
                yield break;
            }
        }

        int id = 0;
        try { id = GameCode.GameNameToInt(code); } catch { }
        if (id == 0)
        {
            FailJoin("RoomListInvalidCode");
            yield break;
        }

        bool started = false;

        // 首选游戏自带的"按代码加入"入口（含加载遮罩、匹配服务、失败提示）。
        try
        {
            PSManager ps = PSManager.Instance;
            if (ps != null)
            {
                ps.JoinGameFromCode(code);
                started = true;
            }
        }
        catch (Exception ex) { LogWarn("PSManager join", ex); }

        // 兜底：直接启动 AmongUsClient 的按代码查找并加入协程。
        if (!started)
        {
            try
            {
                var routine = ac.CoFindGameInfoFromCodeAndJoin(id);
                if (routine != null)
                {
                    ac.StartCoroutine(routine);
                    started = true;
                }
            }
            catch (Exception ex) { LogWarn("join coroutine", ex); }
        }

        if (!started)
        {
            FailJoin("RoomListJoinFailedDetail");
            yield break;
        }

        // 等到真正进入连接阶段为止（最多 8s）。固定 2s 就解锁会让玩家在匹配中途重复点加入。
        float hold = 0f;
        bool entered = false;
        while (hold < 8f)
        {
            hold += Time.unscaledDeltaTime;
            try { entered = net.GameState != InnerNetClient.GameStates.NotJoined; } catch { }
            if (entered) break;
            yield return null;
        }
        if (!entered)
            AmethystPlugin.Logger?.LogWarning($"[RoomList] join did not start within 8s: {code}");

        Joining = false;
        JoiningCode = string.Empty;
    }

    private void FailJoin(string detailKey)
    {
        Joining = false;
        JoiningCode = string.Empty;
        AmethystToast.Push(AmethystText.T("RoomListJoinFailed"), AmethystText.T(detailKey), 3.5f, AmethystNotifyKind.Warning);
    }

    // ================= HTTP POST =================

    private bool Post(string url, string path, byte[] body, Action<bool> done)
    {
        if (_postInFlight || body == null || body.Length == 0) return false;

        _postInFlight = true;
        try
        {
            StartCoroutine(CoPost(url, path, body, done).WrapToIl2Cpp());
            return true;
        }
        catch (Exception ex)
        {
            _postInFlight = false;
            LogWarn("post start", ex);
            done?.Invoke(false);
            return false;
        }
    }

    private IEnumerator CoPost(string url, string path, byte[] body, Action<bool> done)
    {
        UnityWebRequest req = null;
        bool ok = false;

        try
        {
            string secret = Secret();
            if (secret.Length > 0)
            {
                string ts, nonce;
                string sig = AmethystRoomAuth.Sign(secret, path, body, out ts, out nonce);

                req = new UnityWebRequest(url, "POST");
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Accept", "application/json");
                req.SetRequestHeader("X-Ace-Timestamp", ts);
                req.SetRequestHeader("X-Ace-Nonce", nonce);
                req.SetRequestHeader("X-Ace-Signature", sig);
                req.timeout = RequestTimeout;
            }
        }
        catch (Exception ex) { LogWarn("post build", ex); }

        if (req == null)
        {
            _postInFlight = false;
            done?.Invoke(false);
            yield break;
        }

        yield return req.SendWebRequest();

        try
        {
            ok = req.result == UnityWebRequest.Result.Success;
            int status = (int)req.responseCode;

            string resp = string.Empty;
            try { if (req.downloadHandler != null) resp = req.downloadHandler.text ?? string.Empty; } catch { }

            if (ok)
            {
                _postWarned = false;
            }
            else if (status == 401)
            {
                if (!_authFailed)
                {
                    _authFailed = true;
                    AmethystPlugin.Logger?.LogError($"[RoomList] signature rejected (401); check [RoomList] Secret. body={Trim(resp, 200)}");
                }
            }
            else if (!_postWarned)
            {
                _postWarned = true;
                AmethystPlugin.Logger?.LogWarning($"[RoomList] report failed: {status} {req.error} body={Trim(resp, 200)}");
            }
        }
        catch { }
        finally
        {
            try { req.Dispose(); } catch { }
            _postInFlight = false;
            done?.Invoke(ok);
        }
    }

    private static string Secret()
    {
        try
        {
            string s = AmethystConfig.RoomListSecret != null ? AmethystConfig.RoomListSecret.Value : null;
            if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        }
        catch { }

        // 配置为空时使用编译内置密钥。
        return AmethystRoomKey.Default;
    }

    private static string ReportPath()
    {
        try { return new Uri(AmethystRoomApi.ReportUrl).AbsolutePath; }
        catch { return "/home/api/room"; }
    }

    private static string Trim(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    private static void LogWarn(string where, Exception ex)
    {
        try { AmethystPlugin.Logger?.LogWarning($"[RoomList] {where}: {ex.Message}"); } catch { }
    }
}
