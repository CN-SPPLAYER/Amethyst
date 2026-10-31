using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace Amethyst;

internal sealed class KnownPlayer
{
    internal string Name = string.Empty;
    internal string Fc = string.Empty;
    internal string Puid = string.Empty;
    internal string Platform = "?";
    internal string Level = "?";
    internal string LastSeen = string.Empty;
}

internal static class AmethystHistory
{
    private static readonly object Gate = new object();

    private static readonly Dictionary<string, KnownPlayer> Index =
        new Dictionary<string, KnownPlayer>(StringComparer.OrdinalIgnoreCase);
    private static bool _indexLoaded;

    private static string Dir => AmethystPlatform.DataDir;

    internal static string PlayerTxt => Path.Combine(Dir, "PlayerHistory.txt");
    internal static string CheatTxt => Path.Combine(Dir, "CheatHistory.txt");

    private const string PlayerHeader = "# Amethyst 玩家历史 / Player history  ——  时间 | 事件 | 名字 | 好友码 | PUID | 平台 | 等级";
    private const string CheatHeader = "# Amethyst 作弊历史 / Cheat history  ——  时间 | 名字 | 好友码 | PUID | 平台 | 原因";

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

    internal static string LastKnownName(string fc, string puid)
    {
        KnownPlayer k = Lookup(fc, puid);
        return k != null ? (k.Name ?? string.Empty) : string.Empty;
    }

    internal static void Player(string action, string name, string fc, string puid, string platform, string level)
    {
        try
        {
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(fc) && string.IsNullOrEmpty(puid)) return;

            EnsureIndex();

            string time = Now();
            Append(PlayerTxt, PlayerHeader,
                $"{time} | {Clean(action)} | {Clean(name)} | {Clean(fc)} | {Clean(puid)} | {Clean(platform)} | {Clean(level)}");

            try
            {
                lock (Gate) { Remember(fc, puid, name, platform, level, time); }
            }
            catch { }
        }
        catch { }
    }

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

    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\r", " ").Replace("\n", " ").Replace("|", "/").Trim();
    }
}
