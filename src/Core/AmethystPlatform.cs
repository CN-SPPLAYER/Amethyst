using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Amethyst;

internal static class AmethystPlatform
{
    internal static bool IsAndroid
    {
        get
        {
            try { return Application.platform == RuntimePlatform.Android; }
            catch { return false; }
        }
    }

    internal static bool IsWindows
    {
        get
        {
            try
            {
                return Application.platform == RuntimePlatform.WindowsPlayer
                    || Application.platform == RuntimePlatform.WindowsEditor;
            }
            catch { return true; }
        }
    }

    internal static string DataRoot
    {
        get
        {
            if (IsAndroid)
            {
                try
                {
                    string star = Environment.GetEnvironmentVariable("STAR_DATA_PATH");
                    if (!string.IsNullOrEmpty(star)) return star;
                }
                catch { }

                try
                {
                    string pd = Application.persistentDataPath;
                    if (!string.IsNullOrEmpty(pd)) return pd;
                }
                catch { }

                try { return Path.GetTempPath(); } catch { return "."; }
            }

            try { return BepInEx.Paths.GameRootPath; } catch { return "."; }
        }
    }

    internal static string DataDir
    {
        get
        {
            try { return Path.Combine(DataRoot, "Amethyst"); } catch { return "Amethyst"; }
        }
    }

    internal static bool SupportsWindowsOnlyFeatures => !IsAndroid;

    private static bool _helperPathPrepared;

    internal static void PrepareMonoModHelperPath()
    {
        if (_helperPathPrepared) return;
        _helperPathPrepared = true;
        if (!IsAndroid) return;

        string chosen = null;
        string firstWritable = null;
        string lastTripleError = null;
        bool tripleOk = false;

        foreach (string candidate in HelperPathCandidates())
        {
            if (!TryPrepareWritable(candidate)) continue;
            if (firstWritable == null) firstWritable = candidate;

            ApplyAndroidHome(candidate);

            if (TryInitializePlatformTriple(out string tripleError))
            {
                chosen = candidate;
                tripleOk = true;
                break;
            }
            lastTripleError = tripleError;
        }

        if (chosen == null) chosen = firstWritable;

        if (string.IsNullOrEmpty(chosen))
        {
            try { AmethystPlugin.Logger?.LogWarning("[Android] MonoMod HelperDropPath: no writable candidate"); } catch { }
            return;
        }

        ApplyAndroidHome(chosen);

        try { Environment.SetEnvironmentVariable("MONOMOD_HelperDropPath", chosen); } catch { }
        try { Environment.SetEnvironmentVariable("MONOMOD_HELPER_DROP_PATH", chosen); } catch { }

        try
        {
            PropertyInfo cacheProp = typeof(BepInEx.Paths).GetProperty("CachePath", BindingFlags.Public | BindingFlags.Static);
            MethodInfo cacheSet = cacheProp?.GetSetMethod(true);
            if (cacheSet != null)
            {
                cacheSet.Invoke(null, new object[] { chosen });
            }
            else
            {
                FieldInfo cacheField = typeof(BepInEx.Paths).GetField(
                    "<CachePath>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                cacheField?.SetValue(null, chosen);
            }
        }
        catch { }

        int switchesFound = 0;
        try
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type switches = asm.GetType("MonoMod.Switches", false);
                if (switches == null) continue;

                MethodInfo set = switches.GetMethod(
                    "SetSwitchValue",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(object) },
                    null);
                if (set == null) continue;

                try
                {
                    set.Invoke(null, new object[] { "HelperDropPath", chosen });
                    set.Invoke(null, new object[] { "DMDDumpTo", chosen });
                    switchesFound++;
                }
                catch { }
            }
        }
        catch { }

        try
        {
            MethodInfo setData = typeof(AppContext).GetMethod(
                "SetData",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(object) },
                null);
            if (setData != null)
            {
                setData.Invoke(null, new object[] { "MonoMod.HelperDropPath", chosen });
                setData.Invoke(null, new object[] { "MonoMod.DMDDumpTo", chosen });
            }
        }
        catch { }

        try
        {
            string cachePath = string.Empty;
            try { cachePath = BepInEx.Paths.CachePath ?? string.Empty; } catch { }
            string tempPath = string.Empty;
            try { tempPath = Path.GetTempPath(); } catch { }
            string home = string.Empty;
            try { home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty; } catch { }
            string dataHome = string.Empty;
            try { dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? string.Empty; } catch { }
            string localAppData = string.Empty;
            try { localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } catch { }

            AmethystPlugin.Logger?.LogInfo(
                $"[Android] MonoMod HelperDropPath -> {chosen} switches={switchesFound} " +
                $"triple={(tripleOk ? "ok" : "failed:" + (lastTripleError ?? "?"))} " +
                $"home={home} dataHome={dataHome} localAppData={localAppData} " +
                $"cachePath={cachePath} tempPath={tempPath}");
        }
        catch { }
    }

    private static void ApplyAndroidHome(string baseDir)
    {
        try
        {
            string home = Path.Combine(baseDir, "home");
            string dataHome = Path.Combine(home, ".local", "share");
            string cacheHome = Path.Combine(home, ".cache");
            Directory.CreateDirectory(dataHome);
            Directory.CreateDirectory(cacheHome);

            Environment.SetEnvironmentVariable("HOME", home);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", dataHome);
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", cacheHome);
            Environment.SetEnvironmentVariable("TMPDIR", baseDir);
            Environment.SetEnvironmentVariable("TMP", baseDir);
            Environment.SetEnvironmentVariable("TEMP", baseDir);
        }
        catch { }
    }

    private static bool TryInitializePlatformTriple(out string error)
    {
        error = null;
        try
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type triple = asm.GetType("MonoMod.Core.Platforms.PlatformTriple", false);
                if (triple == null) continue;

                PropertyInfo current = triple.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                if (current == null) continue;

                current.GetValue(null);
                return true;
            }

            return true;
        }
        catch (Exception e)
        {
            Exception inner = e.InnerException ?? e;
            error = inner.GetType().Name + ": " + inner.Message;
            return false;
        }
    }

    private static string[] HelperPathCandidates()
    {
        
        string pkg = ProcessPackage();

        return new[]
        {
            "/data/.cache",                               
            SafeCombine("/data/user/0", pkg, "files"),    
            SafeCombine("/data/data", pkg, "files"),
            SafeCombine("/data/user/0", pkg, "cache"),    
            "/data/local/tmp",                            
            SafeApplicationPath(false),                   
            SafeCombine(DataDir, "tmp"),
            SafeCombine(SafeApplicationPath(true), "Amethyst", "tmp"),
        };
    }

    private static string ProcessPackage()
    {
        try
        {
            string cmdline = File.ReadAllText("/proc/self/cmdline");
            int nul = cmdline.IndexOf('\0');
            string cmd = nul >= 0 ? cmdline.Substring(0, nul) : cmdline;
            if (string.IsNullOrEmpty(cmd)) return null;

            int colon = cmd.IndexOf(':');
            if (colon > 0) cmd = cmd.Substring(0, colon);
            return cmd;
        }
        catch { return null; }
    }

    private static string SafeApplicationPath(bool persistent)
    {
        try
        {
            return persistent ? Application.persistentDataPath : Application.temporaryCachePath;
        }
        catch { return null; }
    }

    private static string SafeCombine(string first, params string[] rest)
    {
        try
        {
            if (string.IsNullOrEmpty(first)) return null;
            string path = first;
            foreach (string part in rest)
            {
                if (string.IsNullOrEmpty(path)) return null;
                path = Path.Combine(path, part);
            }
            return path;
        }
        catch { return null; }
    }

    private static bool TryPrepareWritable(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return false;
        try
        {
            Directory.CreateDirectory(dir);
            string probe = Path.Combine(dir, ".amethyst-write-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(probe, new byte[1]);
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}
