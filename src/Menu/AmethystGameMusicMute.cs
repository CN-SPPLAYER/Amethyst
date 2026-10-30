using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Amethyst;

// 播放器开始播放时，压掉游戏自己的音乐（大厅音乐 + 主界面音乐）。
//
// 为什么用「枚举 AudioSource + 按名字判断」而不是逐个去找游戏的播放入口：
//   游戏里音乐有好几个来源，而且不同场景不一样：
//     · 大厅：`SoundManager.CrossFadeSound("MapTheme", lobby.MapTheme, ...)`（AmethystLobbyMusicFix 已经在管这个）
//     · 主界面：不是 SoundManager 的命名音，直接挂在场景里的 AudioSource 上
//     · 开场/其他：还有 IntroMusic 之类
//   逐个对接每一种来源既脆弱又容易漏。这里改用「枚举 AudioSource + 按名字判断」
//   的做法：只认名字里带 music/theme/bgm/song/soundtrack/title 的 AudioSource。
//
// 关键点：**只静音，不改玩家设置**，也不动 "ambient/ambience"（环境音效不是音乐，
// 静掉会让游戏变得死寂，所以要特意排除）。
internal static class AmethystGameMusicMute
{
    private sealed class Saved
    {
        public AudioSource Source;
        public float Volume;
        public bool Mute;
    }

    private const float ScanInterval = 0.5f;

    private static readonly Dictionary<int, Saved> Muted = new Dictionary<int, Saved>();
    private static float _nextScan;

    // 播放器是否正在出声（播放中且音量 > 0）
    internal static bool WantMute
    {
        get
        {
            try
            {
                if (!AmethystConfig.MusicMuteGameMusic.Value) return false;

                AmethystMusicPlayer mp = AmethystMusicPlayer._self;
                if (mp == null || !mp.IsActuallyPlaying) return false;

                return true;
            }
            catch { return false; }
        }
    }

    internal static void Tick()
    {
        try
        {
            if (!WantMute)
            {
                RestoreAll();
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < _nextScan)
            {
                Reapply();
                return;
            }
            _nextScan = now + ScanInterval;

            var found = new HashSet<int>();
            AudioSource[] all = Object.FindObjectsOfType<AudioSource>(true);
            if (all == null) return;

            for (int i = 0; i < all.Length; i++)
            {
                AudioSource s = all[i];
                if (s == null || !IsGameMusic(s)) continue;

                int id = s.GetInstanceID();
                found.Add(id);

                if (!Muted.ContainsKey(id))
                    Muted[id] = new Saved { Source = s, Volume = s.volume, Mute = s.mute };

                s.mute = true;
                s.volume = 0f;
            }

            // 已经不在场的（换场景销毁）就清掉记录
            var stale = new List<int>();
            foreach (var kv in Muted)
                if (!found.Contains(kv.Key) || kv.Value.Source == null) stale.Add(kv.Key);
            foreach (int id in stale) Muted.Remove(id);
        }
        catch { }
    }

    // 两次扫描之间也要保持静音：游戏自己会把 volume 写回去
    private static void Reapply()
    {
        foreach (var kv in Muted)
        {
            AudioSource s = kv.Value.Source;
            if (s == null) continue;
            if (!s.mute || s.volume > 0.0001f)
            {
                s.mute = true;
                s.volume = 0f;
            }
        }
    }

    internal static void RestoreAll()
    {
        if (Muted.Count == 0) return;
        try
        {
            foreach (var kv in Muted)
            {
                AudioSource s = kv.Value.Source;
                if (s == null) continue;
                try
                {
                    s.volume = kv.Value.Volume;
                    s.mute = kv.Value.Mute;
                }
                catch { }
            }
        }
        catch { }
        finally { Muted.Clear(); }
    }

    // 判断一个 AudioSource 是不是"游戏音乐"
    private static bool IsGameMusic(AudioSource s)
    {
        string id = Identify(s);
        if (id.Length == 0) return false;

        // 环境音效/氛围音不算音乐 —— 静掉会让场景变得死寂
        if (id.Contains("ambient") || id.Contains("ambience")) return false;

        return id.Contains("music")
            || id.Contains("theme")
            || id.Contains("bgm")
            || id.Contains("song")
            || id.Contains("soundtrack")
            || id.Contains("title");
    }

    // 名字 + 音频片段名 + 最多 4 层父节点名，一起拿来判断
    private static string Identify(AudioSource s)
    {
        try
        {
            string name = ((Object)s).name ?? string.Empty;
            string clip = s.clip != null ? (((Object)s.clip).name ?? string.Empty) : string.Empty;
            string path = string.Empty;

            Transform t = s.transform;
            for (int depth = 0; t != null && depth < 4; depth++)
            {
                string n = ((Object)t).name;
                if (!string.IsNullOrEmpty(n)) path += " " + n;
                t = t.parent;
            }

            return (name + " " + clip + path).ToLowerInvariant();
        }
        catch { return string.Empty; }
    }
}
