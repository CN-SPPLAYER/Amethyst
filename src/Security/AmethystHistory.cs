using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace Amethyst;

// 从玩家历史里查到的已知信息
internal sealed class KnownPlayer
{
    internal string Name = string.Empty;
    internal string Fc = string.Empty;
    internal string Puid = string.Empty;
    internal string Platform = "?";
    internal string Level = "?";
    internal string LastSeen = string.Empty;
}

// ===== 历史记录（玩家历史 + 作弊历史）=====
//
// 按要求：
//   - **强制开启**，没有开关，也不出现在任何 UI 里，只写文件；
//   - 文件与封禁名单同级目录：`<游戏根目录>/Amethyst/`（即 AmethystAccess 的 AmethystDir）；
//   - 玩家历史：每次**进入 / 离开**都追加一条（名字 / 好友码 / PUID / 平台 / 等级）；
//   - 作弊历史：记录触发反作弊的玩家（名字 / 好友码 / PUID / 平台 / 原因）。
//
// 另参考 OnyxMenu `Player\NocturneNameHistory.cs` 的做法，给玩家历史加了**回读**能力：
//   启动后惰性把 PlayerHistory.txt 解析进内存索引（按好友码 / PUID 建键，保留最新一条），
//   之后每次写入同步更新索引。这样在「玩家大退、Character 已为 null」这类拿不到实时名字的场景，
//   可以**读取玩家历史**回填上一次已知的名字 —— 这正是该文件存在的意义。
internal static class AmethystHistory
{
    private static readonly object Gate = new object();

    // 好友码 / PUID -> 最近一次已知信息
    private static readonly Dictionary<string, KnownPlayer> Index =
        new Dictionary<string, KnownPlayer>(StringComparer.OrdinalIgnoreCase);
    private static bool _indexLoaded;

    private static string Dir => AmethystPlatform.DataDir;

    internal static string PlayerTxt => Path.Combine(Dir, "PlayerHistory.txt");
    internal static string CheatTxt => Path.Combine(Dir, "CheatHistory.txt");

    private const string PlayerHeader = "# Amethyst 玩家历史 / Player history  ——  时间 | 事件 | 名字 | 好友码 | PUID | 平台 | 等级";
    private const string CheatHeader = "# Amethyst 作弊历史 / Cheat history  ——  时间 | 名字 | 好友码 | PUID | 平台 | 原因";

    // ===== 回读：按好友码 / PUID 查询最近一次已知信息 =====
    internal static KnownPlayer Lookup(string fc, string puid)
    {
        try
        {
            EnsureIndex();
            if (!string.IsNullOrEmpty(fc) && Index.TryGetValue("fc:" + fc, out KnownPlayer a)) return a;
            if (!string.IsNullOrEmpty(puid) && Index.TryGetValue("puid:" + puid, out KnownPlayer b)) return b;
        }
        catch { }
        return null;
    }

    // 便捷：取历史里记录过的名字（拿不到就返回空串）
    internal static string LastKnownName(string fc, string puid)
    {
        KnownPlayer k = Lookup(fc, puid);
        return k != null ? (k.Name ?? string.Empty) : string.Empty;
    }

    // ===== 写入：玩家进入 / 离开 =====
    internal static void Player(string action, string name, string fc, string puid, string platform, string level)
    {
        try
        {
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(fc) && string.IsNullOrEmpty(puid)) return;

            // 必须**先**把已有历史读进索引：否则首次写入会把 _indexLoaded 置为 true，
            // 导致老会话的历史永远不会被加载，回读就查不到以前见过的人。
            EnsureIndex();

            string time = Now();
            Append(PlayerTxt, PlayerHeader,
                $"{time} | {Clean(action)} | {Clean(name)} | {Clean(fc)} | {Clean(puid)} | {Clean(platform)} | {Clean(level)}");

            // 同步更新内存索引（供后续回读，避免重复解析整个文件）
            try
            {
                lock (Gate) { Remember(fc, puid, name, platform, level, time); }
            }
            catch { }
        }
        catch { }
    }

    // ===== 写入：触发反作弊 =====
    internal static void Cheat(string name, string fc, string puid, string platform, string reason)
    {
        try
        {
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(fc) && string.IsNullOrEmpty(puid)) return;

            Append(CheatTxt, CheatHeader,
                $"{Now()} | {Clean(name)} | {Clean(fc)} | {Clean(puid)} | {Clean(platform)} | {Clean(reason)}");
        }
        catch { }
    }

    // ===== 索引 =====

    private static void EnsureIndex()
    {
        lock (Gate)
        {
            if (_indexLoaded) return;
            _indexLoaded = true;

            try
            {
                if (!File.Exists(PlayerTxt)) return;
                foreach (string raw in File.ReadAllLines(PlayerTxt, Encoding.UTF8))
                {
                    string line = raw;
                    if (string.IsNullOrWhiteSpace(line) || line[0] == '#') continue;

                    string[] p = line.Split('|');
                    if (p.Length < 5) continue;

                    string time = p[0].Trim();
                    string name = p.Length >= 3 ? p[2].Trim() : string.Empty;
                    string fc = p.Length >= 4 ? p[3].Trim() : string.Empty;
                    string puid = p.Length >= 5 ? p[4].Trim() : string.Empty;
                    string platform = p.Length >= 6 ? p[5].Trim() : "?";
                    string level = p.Length >= 7 ? p[6].Trim() : "?";

                    Remember(fc, puid, name, platform, level, time);
                }
            }
            catch { }
        }
    }

    // 同一键保留**最新**一条（文件是追加式，后写覆盖先写）
    private static void Remember(string fc, string puid, string name, string platform, string level, string time)
    {
        void Put(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!Index.TryGetValue(key, out KnownPlayer k))
            {
                k = new KnownPlayer();
                Index[key] = k;
            }
            if (!string.IsNullOrEmpty(name)) k.Name = name;
            if (!string.IsNullOrEmpty(fc)) k.Fc = fc;
            if (!string.IsNullOrEmpty(puid)) k.Puid = puid;
            if (!string.IsNullOrEmpty(platform) && platform != "?") k.Platform = platform;
            if (!string.IsNullOrEmpty(level) && level != "?") k.Level = level;
            k.LastSeen = time ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(fc)) Put("fc:" + fc);
        if (!string.IsNullOrEmpty(puid)) Put("puid:" + puid);
    }

    // ===== 底层写入 =====

    private static void Append(string path, string header, string line)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                if (!File.Exists(path))
                    File.WriteAllText(path, header + "\n", new UTF8Encoding(false));

                File.AppendAllText(path, line + "\n", new UTF8Encoding(false));
            }
        }
        catch { }
    }

    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    // 清理换行与分隔符，保证一行一条记录、便于后续检索
    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\r", " ").Replace("\n", " ").Replace("|", "/").Trim();
    }
}
