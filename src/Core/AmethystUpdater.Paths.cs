using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Amethyst;

internal static partial class AmethystUpdater
{

    internal static void Reset()
    {
        State = Phase.Idle;
        RemoteVersion = string.Empty;
        DownloadUrl = string.Empty;
        ExpectedSha256 = string.Empty;
        StagedPath = string.Empty;
        DownloadViaApi = false;
        Error = string.Empty;
        Progress = 0f;
        NeedConfirm = false;
        NeedRestart = false;
    }


    private static string NormalizeDigest(string digest)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(digest)) return string.Empty;
            string s = digest.Trim();
            int colon = s.IndexOf(':');
            if (colon >= 0) s = s.Substring(colon + 1);
            return s.Trim().ToLowerInvariant();
        }
        catch { return string.Empty; }
    }


    private static bool DigestMatches(string path, string expected)
    {
        if (string.IsNullOrEmpty(expected)) return true;
        try
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return string.Equals(sb.ToString(), expected, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { return false; }
    }


    private static bool HasBatchUnsafe(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s)) return true;
            return s.IndexOf('%') >= 0 || s.IndexOf('"') >= 0
                || s.IndexOf('\r') >= 0 || s.IndexOf('\n') >= 0;
        }
        catch { return true; }
    }


    internal static void Fail(string reason)
    {
        State = Phase.Failed;
        Error = reason ?? string.Empty;
        NeedConfirm = false;
        if (!string.IsNullOrEmpty(Error))
        {
            AmethystToast.Push(AmethystText.T("CheckUpdate"), Error, 5f, AmethystNotifyKind.Warning);
        }
    }



    internal static string CurrentDllPath()
    {
        try
        {
            string loc = typeof(AmethystUpdater).Assembly.Location;
            if (!string.IsNullOrEmpty(loc) && File.Exists(loc)) return loc;
        }
        catch { }
        try
        {
            string dir = BepInEx.Paths.PluginPath;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                
                string stable = Path.Combine(dir, "Amethyst.dll");
                if (File.Exists(stable)) return stable;

                string best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (string file in Directory.GetFiles(dir, "Amethyst*.dll"))
                {
                    try
                    {
                        System.Reflection.AssemblyName an = System.Reflection.AssemblyName.GetAssemblyName(file);
                        if (!string.Equals(an.Name, "Amethyst", StringComparison.Ordinal)) continue;

                        DateTime t = File.GetLastWriteTimeUtc(file);
                        if (best == null || t > bestTime)
                        {
                            best = file;
                            bestTime = t;
                        }
                    }
                    catch { }
                }
                if (best != null) return best;
            }
        }
        catch { }
        return string.Empty;
    }




    private static bool SafeApiAssetUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps) return false;
            if (!string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)) return false;
            return uri.AbsolutePath.StartsWith("/repos/CN-SPPLAYER/Amethyst/releases/assets/",
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }


    private static bool SafeUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps) return false;

            if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;
            return uri.AbsolutePath.StartsWith("/CN-SPPLAYER/Amethyst/releases/download/",
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
