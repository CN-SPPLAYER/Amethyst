using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Amethyst;

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

            var stale = new List<int>();
            foreach (var kv in Muted)
                if (!found.Contains(kv.Key) || kv.Value.Source == null) stale.Add(kv.Key);
            foreach (int id in stale) Muted.Remove(id);
        }
        catch { }
    }

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

    private static bool IsGameMusic(AudioSource s)
    {
        string id = Identify(s);
        if (id.Length == 0) return false;

        if (id.Contains("ambient") || id.Contains("ambience")) return false;

        return id.Contains("music")
            || id.Contains("theme")
            || id.Contains("bgm")
            || id.Contains("song")
            || id.Contains("soundtrack")
            || id.Contains("title");
    }

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
