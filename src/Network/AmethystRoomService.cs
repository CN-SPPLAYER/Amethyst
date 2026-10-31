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

public sealed partial class AmethystRoomService : MonoBehaviour
{
    public AmethystRoomService(IntPtr ptr) : base(ptr) { }

    internal static AmethystRoomService Instance { get; private set; }

    private const int RequestTimeout = 10;

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
        
        float now = Time.unscaledTime;
        if (now < _nextPollAt) return;
        _nextPollAt = now + 1f;

        try { TickReport(); } catch { }
    }

    private void TickReport()
    {
        
        if (AmethystConfig.RoomListEnabled == null || !AmethystConfig.RoomListEnabled.Value)
        {
            if (_reported) CloseOwnRoom();
            return;
        }

        if (AmethystConfig.RoomListReportOwnRoom != null && !AmethystConfig.RoomListReportOwnRoom.Value)
        {
            
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



    private void FailJoin(string detailKey)
    {
        Joining = false;
        JoiningCode = string.Empty;
        AmethystToast.Push(AmethystText.T("RoomListJoinFailed"), AmethystText.T(detailKey), 3.5f, AmethystNotifyKind.Warning);
    }

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




    private static void LogWarn(string where, Exception ex)
    {
        try { AmethystPlugin.Logger?.LogWarning($"[RoomList] {where}: {ex.Message}"); } catch { }
    }
}
