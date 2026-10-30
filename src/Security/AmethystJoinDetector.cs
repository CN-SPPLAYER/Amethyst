using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystJoinDetector : MonoBehaviour
{
    private const float ScanInterval = 0.5f;
    private const float PendingTimeout = 8f;

    private static readonly string[] SusTokens =
    {
        "menu", "mod", "cheat", "hack", "inject", "trainer", "aimbot", "godmode",
        "esp", "exploit", "njord", "meow", "sus", "aura", "xenon", "nexus",
    };

    private readonly HashSet<int> _known = new HashSet<int>();
    private readonly Dictionary<int, float> _pending = new Dictionary<int, float>();
    private readonly List<int> _live = new List<int>();
    private readonly List<int> _stale = new List<int>();
    private readonly Dictionary<int, string> _names = new Dictionary<int, string>();
    private readonly Dictionary<int, int> _colors = new Dictionary<int, int>();
    private readonly Dictionary<int, RecentEntry> _seen = new Dictionary<int, RecentEntry>();
    private bool _primed;
    private float _next;

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_JoinDetect);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_JoinDetect); }
    }

    public void UpdateInner()
    {
            if (Time.realtimeSinceStartup < _next) return;
            _next = Time.realtimeSinceStartup + ScanInterval;
            Scan();
    
    }

    private void Forget()
    {
        if (_known.Count == 0 && _pending.Count == 0 && _names.Count == 0 && _seen.Count == 0 && !_primed) return;
        _known.Clear();
        _pending.Clear();
        _names.Clear();
        _colors.Clear();
        _seen.Clear();
        AmethystDisconnect.Clear();
        _primed = false;
    }

    private void Scan()
    {
        InnerNetClient net = Net();
        if (net == null || net.allClients == null)
        {
            Forget();
            return;
        }

        float now = Time.realtimeSinceStartup;
        bool priming = !_primed;
        _live.Clear();

        var cursor = net.allClients.GetEnumerator();
        while (cursor.MoveNext())
        {
            ClientData client = cursor.Current;
            if (client == null || client.Id < 0) continue;
            _live.Add(client.Id);
            // 只在解析到**有效**名字时才更新缓存：玩家大退/断线时 Character 会先变 null，
            // 若用空值或 "TESTNAME" 覆盖，离开提示就会显示成占位名。
            string resolved = ResolveName(client);
            // 拿不到实时名字时，**读取玩家历史**回填上一次已知的名字
            // （参考 OnyxMenu 的 NocturneNameHistory：按好友码回查历史昵称）。
            if (string.IsNullOrEmpty(resolved))
            {
                try
                {
                    string f = string.Empty, p = string.Empty;
                    try { f = client.FriendCode ?? string.Empty; } catch { }
                    try { p = client.ProductUserId ?? string.Empty; } catch { }
                    if (f.Length > 0 || p.Length > 0) resolved = AmethystHistory.LastKnownName(f, p);
                }
                catch { }
            }
            if (!string.IsNullOrEmpty(resolved)) _names[client.Id] = resolved;
            else if (!_names.ContainsKey(client.Id)) _names[client.Id] = AmethystText.T("Player_2");
            try { _colors[client.Id] = AmethystColors.ColorId(client); } catch { }
            if (client.Id != net.ClientId && (!_seen.TryGetValue(client.Id, out RecentEntry rec) || rec.Level == "?"))
                _seen[client.Id] = Snapshot(client, _names[client.Id]);

            if (priming) { _known.Add(client.Id); continue; }
            if (_known.Contains(client.Id)) continue;
            if (client.Id == net.ClientId) { _known.Add(client.Id); continue; }

            if (!_pending.ContainsKey(client.Id)) _pending[client.Id] = now;
            bool hasChar = client.Character != null && client.Character.Data != null;
            // 等待玩家信息（角色/名字）就绪后再提示，避免用未加载的数据生成异常文案
            string readyName = ResolveName(client);
            bool hasName = !string.IsNullOrEmpty(readyName) && readyName != "?" && !readyName.StartsWith("#");
            if ((hasChar && hasName) || now - _pending[client.Id] >= PendingTimeout)
            {
                AnnounceJoin(client);
                _known.Add(client.Id);
                _pending.Remove(client.Id);
            }
        }

        if (priming) _primed = true;
        PruneDeparted(_live.Contains(net.ClientId));
    }

    private void PruneDeparted(bool selfPresent)
    {
        _stale.Clear();
        foreach (int id in _known)
            if (!_live.Contains(id)) _stale.Add(id);
        for (int i = 0; i < _stale.Count; i++)
        {
            int id = _stale[i];
            if (selfPresent) { AnnounceLeave(id); Remember(id); }
            _known.Remove(id);
            _names.Remove(id);
            _colors.Remove(id);
            _seen.Remove(id);
        }

        _stale.Clear();
        foreach (var pair in _pending)
            if (!_live.Contains(pair.Key)) _stale.Add(pair.Key);
        for (int i = 0; i < _stale.Count; i++) _pending.Remove(_stale[i]);
    }

    private static RecentEntry Snapshot(ClientData client, string knownName)
    {
        var e = new RecentEntry
        {
            Name = string.IsNullOrEmpty(knownName) ? AmethystText.T("Player_2") : knownName,
            Level = ResolveLevel(client),
            Platform = "?",
            Raw = string.Empty,
            Fc = string.Empty,
            Puid = string.Empty,
        };

        try
        {
            if (client.PlatformData != null)
            {
                e.Platform = PlatformLabel(client.PlatformData.Platform);
                e.Raw = CleanRaw(client.PlatformData.PlatformName);
            }
        }
        catch { }
        try { e.Fc = client.FriendCode ?? string.Empty; } catch { }
        try { e.Puid = client.ProductUserId ?? string.Empty; } catch { }
        return e;
    }

    private void Remember(int id)
    {
        if (!_seen.TryGetValue(id, out RecentEntry e)) return;
        if (_names.TryGetValue(id, out string n)) e.Name = n;
        e.Left = System.DateTime.Now.ToString("HH:mm");
        AmethystRecent.Push(e);
    }

    private static void AnnounceJoin(ClientData client)
    {
        string name = ResolveName(client);
        string level = ResolveLevel(client);

        string platform = "?";
        string raw = string.Empty;
        try
        {
            if (client.PlatformData != null)
            {
                platform = PlatformLabel(client.PlatformData.Platform);
                raw = CleanRaw(client.PlatformData.PlatformName);
            }
        }
        catch { }

        bool sus = IsSuspicious(raw);
        string levelStr = AmethystText.IsChinese ? $"{level}级" : $"{AmethystText.T("Lv")}{level}";
        string tail = $"{levelStr} · {platform}";
        if (raw.Length > 0) tail += $" · {raw}";

        bool known = false;

        string fc = string.Empty, puid = string.Empty;
        try { fc = client.FriendCode ?? string.Empty; } catch { }
        try { puid = client.ProductUserId ?? string.Empty; } catch { }
        string extra = string.Empty;
        if (fc.Length > 0) extra += " · FC " + fc;
        if (puid.Length > 0) extra += " · PUID " + puid;

        AmethystNotifyKind kind = sus ? AmethystNotifyKind.Danger : known ? AmethystNotifyKind.Warning : AmethystNotifyKind.Info;
        string mark = sus ? "⚠ " : known ? "☺ ＋ " : "＋ ";
        string nameShow = AmethystColors.Name(client) ?? name;
        // ⚠ 第 116 轮：**不再发进房提示**（"显示平台及等级"功能已移除）——
        //   玩家正常进/离开完全交给原版通知；这里只保留事件日志与玩家历史。
        AmethystEventLog.Add(mark + nameShow + " " + AmethystText.T("Joined") + " · " + tail + extra, kind, AmethystEventCat.Join);

        // 玩家历史（强制开启，只写文件）：进入
        AmethystHistory.Player(
            AmethystText.T("HistoryJoin"),
            name,
            fc,
            puid,
            platform,
            level);
    }

    private void AnnounceLeave(int id)
    {
        if (!AmethystDisconnect.MarkLeaveAnnounced(id)) return;
        string name = _names.TryGetValue(id, out string n) ? n : AmethystText.T("Player_2");
        string nameShow = _colors.TryGetValue(id, out int cid) ? AmethystColors.Name(name, cid) : name;
        // 本反作弊执行过踢/封时，单独标明（区别于其他反作弊，如树懒）
        string reason = AmethystDisconnect.TryGetAction(id, out bool ourBan, out string ourReason)
            ? AmethystText.T(ourBan ? "BannedByUs" : "KickedByUs", ourReason ?? string.Empty)
            : ReasonText(AmethystDisconnect.Get(id));
        // ⚠ 第 116 轮：**不再发离开提示** —— 玩家正常离开保持原版通知，这里只记事件日志与历史。
        AmethystEventLog.Add("－ " + nameShow + " " + reason, AmethystNotifyKind.Warning, AmethystEventCat.Join);

        // 玩家历史（强制开启，只写文件）：离开
        // 离开时 ClientData 已经/即将消失，信息取自进入时缓存下来的 RecentEntry。
        string level = "?", platform = "?", fc = string.Empty, puid = string.Empty;
        if (_seen.TryGetValue(id, out RecentEntry e) && e != null)
        {
            if (!string.IsNullOrEmpty(e.Level)) level = e.Level;
            if (!string.IsNullOrEmpty(e.Platform)) platform = e.Platform;
            fc = e.Fc ?? string.Empty;
            puid = e.Puid ?? string.Empty;
            if (!string.IsNullOrEmpty(e.Name) && (string.IsNullOrEmpty(name) || name == AmethystText.T("Player_2")))
                name = e.Name;
        }

        // 仍然只有通用称呼时，读取玩家历史回填（大退场景常常走到这里）
        if ((string.IsNullOrEmpty(name) || name == AmethystText.T("Player_2")) && (fc.Length > 0 || puid.Length > 0))
        {
            try
            {
                string hist = AmethystHistory.LastKnownName(fc, puid);
                if (!string.IsNullOrEmpty(hist)) name = hist;
            }
            catch { }
        }

        AmethystHistory.Player(AmethystText.T("HistoryLeave"), name, fc, puid, platform, level);
    }

    // 依据游戏断开原因给出更准确的提示。
    //
    // 参考 BetterAmongUs `Patches\Gameplay\Player\PlayerJoinAndLeftPatch.cs` 的 `BetterShowNotification`
    // （第 164-190 行）：它显式列出 ExitGame / ClientTimeout / Kicked / Banned / Hacking / Error / Unknown，
    // 而**其余一切原因都归到「离开」**（default → DisconnectReason.Left）。
    //
    // 之前的 bug：本机 `DisconnectReasons` 枚举里**正常退出走的是 `IntentionalLeaving`（=208）**，
    // 既不是 ExitGame 也不是其它已列出的值，于是落到 default 被显示成「断开连接（原因未知）」，
    // 这正是「玩家正常离开却提示原因未知」的成因。现补上该分支，并让 default 与 BetterAmongUs 一致归为「离开」。
    private static string ReasonText(DisconnectReasons reason)
    {
        switch (reason)
        {
            case DisconnectReasons.ExitGame:
            case DisconnectReasons.IntentionalLeaving:
                return AmethystText.T("DisconnectLeft");
            case DisconnectReasons.ClientTimeout:
                return AmethystText.T("DisconnectTimeout");
            case DisconnectReasons.Kicked:
                return AmethystText.T("DisconnectKicked");
            case DisconnectReasons.Banned:
                return AmethystText.T("DisconnectBanned");
            case DisconnectReasons.Hacking:
                return AmethystText.T("DisconnectHacking");
            case DisconnectReasons.Error:
                return AmethystText.T("DisconnectError");
            case DisconnectReasons.Unknown:
                return AmethystText.T("DisconnectUnknown");
            default:
                // 与 BetterAmongUs 一致：未特别列出的原因一律按「离开游戏」处理，
                // 避免把正常离开误报成「原因未知」。
                return AmethystText.T("DisconnectLeft");
        }
    }

    private static string ResolveName(ClientData client)
    {
        try
        {
            if (client.Character != null && client.Character.Data != null && !string.IsNullOrWhiteSpace(client.Character.Data.PlayerName))
                return Trim(client.Character.Data.PlayerName, 22);
        }
        catch { }

        // 平台名只作为**最后**的兜底，且必须排除占位值（如 "TESTNAME"）。
        // 玩家直接大退时 Character 会先变为 null，此前会把 "TESTNAME" 当成玩家名显示出来。
        try
        {
            if (client.PlatformData != null)
            {
                string pn = client.PlatformData.PlatformName;
                if (!string.IsNullOrWhiteSpace(pn) && !IsPlaceholderName(pn))
                    return Trim(pn, 22);
            }
        }
        catch { }

        // 返回空串：调用方会保留上一次已知的有效名字，而不是用占位值覆盖。
        return string.Empty;
    }

    // 占位 / 无意义的名字，不能当作玩家名使用
    private static bool IsPlaceholderName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        string v = s.Trim();
        return v.Equals("TESTNAME", System.StringComparison.OrdinalIgnoreCase)
            || v.Equals("TEST", System.StringComparison.OrdinalIgnoreCase)
            || v.Equals("UNKNOWN", System.StringComparison.OrdinalIgnoreCase)
            || v.Equals("?", System.StringComparison.Ordinal);
    }

    private static string ResolveLevel(ClientData client)
    {
        return Patches.AmethystJoinLevels.Display(client);
    }

    private static string CleanRaw(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string raw = value.Replace("\r", " ").Replace("\n", " ").Trim();

        int pipe = raw.IndexOf('|');
        if (pipe > 0) raw = raw.Substring(0, pipe).TrimEnd();

        if (raw.Equals("TESTNAME", System.StringComparison.OrdinalIgnoreCase)) return string.Empty;

        return Trim(raw, 24);
    }

    private static bool IsSuspicious(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        string lower = raw.ToLowerInvariant();
        for (int i = 0; i < SusTokens.Length; i++)
            if (lower.Contains(SusTokens[i])) return true;
        return false;
    }

    private static string Trim(string value, int max)
    {
        string clean = value.Trim();
        return clean.Length <= max ? clean : clean.Substring(0, max - 1).TrimEnd() + "…";
    }

    private static InnerNetClient Net()
    {
        try { return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance; }
        catch { return null; }
    }

    private static string PlatformLabel(Platforms platform)
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
            112 => "Starlight",
            _ => "Unknown",
        };
    }
}
