using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Amethyst;

// ===== 平台适配（第 275 轮）=====
//
// 目标：同一份 AnyCPU DLL 同时跑在 Windows（BepInEx）与 Android（Starlight）。
//
// 依据 Starlight 官方移植指南：
//   · `BepInEx.Paths.GameRootPath` 在 Android 上**不可写**，不能用来存数据；
//   · Starlight 提供环境变量 `STAR_DATA_PATH` 作为模组数据目录；
//   · 也推荐 `Application.persistentDataPath`。
//
// 因此所有需要**写盘**的路径统一走 `DataRoot`，只读路径（插件目录）保持原样。
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

    // 可写数据根目录。
    //   Android: STAR_DATA_PATH → Application.persistentDataPath → 临时目录
    //   Windows: BepInEx 的 GameRootPath（保持原有行为）
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

    // 本模组自己的数据子目录（历史、作弊记录、音乐等）
    internal static string DataDir
    {
        get
        {
            try { return Path.Combine(DataRoot, "Amethyst"); } catch { return "Amethyst"; }
        }
    }

    // Windows 专属能力：批处理替换/重启、NAudio(WinMM) 播放、控制台窗口
    internal static bool SupportsWindowsOnlyFeatures => !IsAndroid;

    // ===== Android / MonoMod 原生辅助库目录（第 276 轮）=====
    //
    // 真机日志：202 个补丁全部失败，内层异常都是
    //   UnauthorizedAccessException: Access to the path '/data/.cache' is denied.
    //
    // 原因是 Starlight 把 MonoMod 的 HelperDropPath 指到了 /data/.cache；
    // MonoMod 每次创建 detour 都要把原生异常辅助库（mm-exhelper.so）释放到该目录，
    // 目录不可写时每个 Harmony 补丁都会在建 detour 阶段失败 —— 与补丁签名无关。
    //
    // 这里在打补丁之前把 HelperDropPath 改到可写目录。依次尝试：
    //   1) /data/.cache（若能创建，保持 Starlight 的目录/命名空间）
    //   2) Application.temporaryCachePath（应用私有 cache，位于 /data 下）
    //   3) STAR_DATA_PATH/Amethyst/tmp（本模组已证明可写）
    //
    // 同时写环境变量与 AppContext（覆盖 MonoMod 尚未初始化的情况），
    // 再反射调用 MonoMod.Switches.SetSwitchValue（覆盖已经缓存的旧值）。
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

        // 确保最终选中的目录对应的环境变量已生效
        ApplyAndroidHome(chosen);

        // MonoMod 的 Switches 在静态构造时读取 MONOMOD_<开关名>，
        // 开关名是 HelperDropPath（不是 HELPER_DROP_PATH），两种写法都写一份。
        try { Environment.SetEnvironmentVariable("MONOMOD_HelperDropPath", chosen); } catch { }
        try { Environment.SetEnvironmentVariable("MONOMOD_HELPER_DROP_PATH", chosen); } catch { }

        // BepInEx 的缓存目录也可能被 Starlight 指到 /data/.cache；
        // CachePath 的 setter 是私有的，用反射写（再回退到 backing field）。
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

        // 若 Switches 已经初始化并缓存了 /data/.cache，必须用 SetSwitchValue 覆盖。
        // ⚠ 不要 break：0Harmony 可能内置一份 MonoMod.Utils，而 MonoMod.Core 用的是
        //    另一份独立程序集；两边的 Switches 静态字典互不相通，必须每一份都设置。
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

        // AppContext 是 MonoMod 的另一条回退路径（Switches.TryGetSwitchValue）。
        // AppContext.SetData 在 .NET 6 是 internal，用反射写。
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

    // 主动触发 MonoMod 的 PlatformTriple.Current（AndroidSystem..ctor 在这里跑），
    // 这样可以在打补丁前逐个候选验证目录是否真的可用。
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

            // 类型尚未加载：无法提前验证，交给第一次补丁。
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
        // /proc/self/cmdline 给出当前进程的包名（Starlight 的 dev.allofus.fusioncore），
        // 用它拼内部数据目录；这比 Application.identifier（游戏包名）更可靠。
        string pkg = ProcessPackage();

        return new[]
        {
            "/data/.cache",                               // Starlight 默认值；可写时保持其目录
            SafeCombine("/data/user/0", pkg, "files"),    // 应用内部 files（可写，最可能可 dlopen）
            SafeCombine("/data/data", pkg, "files"),
            SafeCombine("/data/user/0", pkg, "cache"),    // 应用内部 cache
            "/data/local/tmp",                            // shell 上下文
            SafeApplicationPath(false),                   // Unity cache（可能是外部存储）
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
