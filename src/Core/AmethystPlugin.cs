using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.Analytics;
#if !ANDROID
using UnityEngine.CrashReportHandler;
#endif

namespace Amethyst;

#if !ANDROID
[BepInProcess("Among Us.exe")]
#endif
[BepInPlugin(PluginId, PluginName, PluginVersion)]
public sealed class AmethystPlugin : BasePlugin
{
    public const string PluginId = "amethyst.mod";
    public const string PluginName = "Amethyst";
    public const string PluginVersion = VersionInfo.PluginVersion;

    internal static ManualLogSource Logger { get; private set; }

    internal static void LogCheat(string name, string playerId, string reason)
    {
        try
        {
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Logger?.LogInfo($"[CheatLog] {time} | Name: {name} | PlayerId: {playerId} | Reason: {reason}");
        }
        catch { }
    }

    private readonly Harmony _harmony = new Harmony(PluginId);
    private static bool _firstPatchFailureLogged;

    public override void Load()
    {
        // 第 275 轮：Android（Starlight）适配。
        //
        // 平台差异（见 Starlight 官方移植指南）：
        //   · BepInEx.Paths.GameRootPath 在 Android 上不可写 → 数据统一走 AmethystPlatform.DataRoot；
        //   · Android 没有 cmd.exe / Among Us.exe 重启流程 → 自动更新在 Android 上禁用；
        //   · NAudio(WinMM) 是 Windows 专属 → 音乐播放器在 Android 上不挂载；
        //   · 控制台窗口不存在 → 隐藏控制台只在 Windows 执行。
        bool android = AmethystPlatform.IsAndroid;

        if (!android)
        {
            AmethystDependencies.Setup();
        }

        Logger = Log;
        if (!android) AmethystDependencies.FlushLog();

        // 第 276 轮：必须在任何 Harmony 补丁之前执行。
        // Starlight 的 MonoMod HelperDropPath 指向 /data/.cache，部分设备不可写，
        // 会导致每个补丁在建 detour 时 UnauthorizedAccessException。
        AmethystPlatform.PrepareMonoModHelperPath();

        Logger.LogInfo($"[Platform] {Application.platform} dataRoot={AmethystPlatform.DataRoot}");

        AmethystExternalResources.EnsureInitialized(); // 确保外部资源健全（缺失自动补全）
        // 第 259 轮：不再应用自定义字体（避免部分用户电脑字体注册失败）。
        AmethystConfig.Bind(Config);
        if (android && !AmethystConfig.MenuButton.Value)
            AmethystConfig.MenuButton.Value = true;
        // 第 180 轮：原先这里注册了 4 个每玩家组件
        //（AmethystModClientTag / AmethystRoleTag / AmethystAliveRoleTag / AmethystMeetingInfoDisplay），
        // 现已把前三个合并为单个 `AmethystPlayerOverlay`；会议那个载体是 PlayerVoteArea，
        // 不是 PlayerControl，仍需单独注册。
        RegisterInIl2Cpp<Patches.AmethystPlayerOverlay>();
        RegisterInIl2Cpp<Patches.AmethystRoleTagMenuScale>();
        RegisterInIl2Cpp<Patches.AmethystSnowFlakes>();
        RegisterInIl2Cpp<Patches.AmethystMeetingInfoDisplay>();
        RegisterInIl2Cpp<Patches.AmethystJoinWatchdog>();
        RegisterInIl2Cpp<Patches.AmethystLobbyPlayerInfo>();
        RegisterInIl2Cpp<AmethystOutfitSaver>();
        RegisterInIl2Cpp<AmethystColorBlindText>();
        RegisterInIl2Cpp<AmethystColorSnipe>();
        RegisterInIl2Cpp<AmethystNameRendering>();
        RegisterInIl2Cpp<AmethystVersionChecker>();
        RegisterInIl2Cpp<AmethystTextShine>();
        RegisterInIl2Cpp<AmethystRecapWindow>();
        RegisterInIl2Cpp<AmethystMenu>();
        RegisterInIl2Cpp<AmethystHud>();
        RegisterInIl2Cpp<AmethystToast>();
        RegisterInIl2Cpp<AmethystMenuButton>();
        RegisterInIl2Cpp<AmethystJoinDetector>();
        RegisterInIl2Cpp<AmethystJoinLogger>();
        RegisterInIl2Cpp<AmethystEventLog>();
        RegisterInIl2Cpp<AmethystGuiHost>();
        RegisterInIl2Cpp<Patches.AmethystHostStateWatcher>();
        RegisterInIl2Cpp<Patches.AmethystAccessGuard>();
        RegisterInIl2Cpp<AmethystAutoLobbyReturn>();
        RegisterInIl2Cpp<AmethystMusicPlayer>();
        RegisterInIl2Cpp<AmethystRoomService>();
        RegisterInIl2Cpp<AmethystUpdaterHost>();
        RegisterInIl2Cpp<AmethystUpdateDialog>();

        InstallHarmonyXNoiseFilter();
        PatchAllResilient();
        AmethystCompat.Init(_harmony);
        ApplyTelemetryPreference();

        // 第 172 轮：职业配色覆盖自检 —— 对着游戏程序集的 RoleTypes 枚举逐个核对，
        // 漏掉的职业会写进日志（游戏更新新增职业时能第一时间发现）。
        AmethystRoleColors.VerifyCoverage();

        // 第 176 轮：职业说明的**本地化**自检 —— 逐一核对 3 种语言 × 全部职业，
        // 缺哪条就写进日志（上一版话术硬编码中文，切语言不生效就是这个原因）。
        AmethystRoleDescriptions.VerifyCoverage();

        // 第 173 轮：启动时立刻尝试隐藏黑色控制台窗口（此后由 AmethystHud 低频复检）。
        // Android 没有控制台窗口，跳过。
        if (!android)
        {
            try { Patches.AmethystConsoleHider.Tick(); } catch { }
        }

        AddComponent<AmethystMenu>();
        AddComponent<AmethystHud>();
        AddComponent<AmethystColorBlindText>();
        AddComponent<AmethystColorSnipe>();
        AddComponent<AmethystNameRendering>();
        AddComponent<AmethystVersionChecker>();
        if (!AmethystCompat.OtherOutfitSaver) AddComponent<AmethystOutfitSaver>();
        AddComponent<AmethystToast>();
        AddComponent<AmethystMenuButton>();
        AddComponent<AmethystJoinDetector>();
        AddComponent<AmethystJoinLogger>();
        AddComponent<AmethystEventLog>();
        AddComponent<AmethystRecapWindow>();
        // ⚠ 第 214 轮：启动动画已按要求彻底移除（`AmethystSplashScreen` 文件已删除，
        //   `AmethystStartupGatePatch` 恢复完全放行），游戏直接按原版流程进入主界面。
        AddComponent<AmethystGuiHost>();
        AddComponent<Patches.AmethystHostStateWatcher>();
        AddComponent<Patches.AmethystAccessGuard>();
        AddComponent<AmethystAutoLobbyReturn>();
        if (!android) AddComponent<AmethystMusicPlayer>();   // NAudio/WinMM 仅 Windows
        AddComponent<AmethystRoomService>();
        if (!android)
        {
            // 自动更新依赖 cmd.exe 与「重启 Among Us.exe」，Android 上没有这套流程
            AddComponent<AmethystUpdaterHost>();
            AddComponent<AmethystUpdateDialog>();
        }
        // 第 207 轮（EHR 提取）：驱动「踢出加入缓慢玩家」看门狗。
        AddComponent<Patches.AmethystJoinWatchdog>();

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    // 容错注册 Il2Cpp 类型：普通数据类（如 AmethystOutfitData，非组件）无法被注入，
    // 若整体失败会让 ClassInjector 抛异常导致插件加载中断。逐类型 catch，保证单点失败不影响加载。
    private static void RegisterInIl2Cpp<T>() where T : class
    {
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        }
        catch (Exception ex)
        {
            Logger?.LogWarning($"Skipped il2cpp type registration {typeof(T).FullName}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void InstallHarmonyXNoiseFilter()
    {
        try
        {
            var listeners = BepInEx.Logging.Logger.Listeners;
            var toWrap = new List<ILogListener>(listeners);
            var collection = (ICollection<ILogListener>)listeners;
            foreach (var listener in toWrap)
            {
                collection.Remove(listener);
                collection.Add(new HarmonyXNoiseFilter(listener));
            }
        }
        catch
        {
        }
    }

    private sealed class HarmonyXNoiseFilter : ILogListener
    {
        private readonly ILogListener _inner;

        internal HarmonyXNoiseFilter(ILogListener inner) => _inner = inner;

        public LogLevel LogLevelFilter => _inner.LogLevelFilter;

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if (eventArgs.Level == LogLevel.Warning && eventArgs.Source?.SourceName == "HarmonyX")
            {
                return;
            }

            _inner.LogEvent(sender, eventArgs);
        }

        public void Dispose() => _inner.Dispose();
    }

    private void PatchAllResilient()
    {
        // ponytail: PatchAll throws on the first patch whose target method can't be resolved,
        // aborting the whole plugin load (which previously killed the menu). Patch per-type and
        // skip individual failures so one broken target can't take the mod down.
        var types = SafeGetTypes();
        foreach (var type in types)
        {
            try
            {
                if (SkipDiagPatch(type)) continue;
                _harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception ex)
            {
                // 第 278 轮：第一个失败补丁打印完整异常（含 inner 堆栈），
                // 用于确认 /data/.cache 到底是哪个方法写出来的。
                if (!_firstPatchFailureLogged)
                {
                    _firstPatchFailureLogged = true;
                    try { Logger.LogWarning("First patch failure detail:\n" + ex); } catch { }
                }

                // 第 275 轮：把内层异常一起写出来。
                // Android/Starlight 上曾出现 203 条只写「Patching exception」的日志，
                // 内层原因（方法不存在 / IL 编译失败 / 签名不匹配）完全看不到，无法定位。
                string inner = string.Empty;
                try
                {
                    if (ex.InnerException != null)
                        inner = " | inner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message;
                }
                catch { }

                Logger.LogWarning($"Skipped patch on {type.FullName}: {ex.GetType().Name}: {ex.Message}{inner}");
            }
        }
    }

    // 临时定位开关：按配置**逐个**跳过"主界面/账号"三个模块的补丁类。
    private static bool SkipDiagPatch(Type type)
    {
        try
        {
            string n = type != null ? (type.FullName ?? string.Empty) : string.Empty;

            // ⚠ `AmethystAccountManagerAwakePatch` 必须算进 FriendsBar 组：它写在 AmethystFriendsBar.cs 里，
            //   但类名不含 "FriendsBar"（它钩的是 AccountManager.Awake），漏掉它这一组就关不干净。
            //
            // ⚠ 第 218 轮：原来这里还有一条 `FriendsListOpen` 分支，对应 `Diag.DisableFriendsListOpen`
            //   （默认 **true**）。那个默认值把第 199 轮写好的「好友按钮点了不出面板」修复
            //   一直关着，实机日志里从没出现过 `[FriendsOpen]`。分支与配置项已删除，本模块无条件生效。
            if (n.Contains("FriendsBar") || n.Contains("AccountManagerAwake"))
                return AmethystConfig.DiagDisableFriendsBar != null && AmethystConfig.DiagDisableFriendsBar.Value;

            if (n.Contains("MainMenuStyle"))
                return AmethystConfig.DiagDisableMainMenuStyle != null && AmethystConfig.DiagDisableMainMenuStyle.Value;

            return false;
        }
        catch { return false; }
    }

    private static Type[] SafeGetTypes()
    {
        try
        {
            return typeof(AmethystPlugin).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).ToArray();
        }
    }

    private static void ApplyTelemetryPreference()
    {
        if (!AmethystConfig.BlockTelemetry.Value || Application.platform == RuntimePlatform.Android)
        {
            return;
        }

        Analytics.enabled = false;
        Analytics.deviceStatsEnabled = false;
        Analytics.initializeOnStartup = false;
        Analytics.limitUserTracking = true;
        PerformanceReporting.enabled = false;
#if !ANDROID
        // 安卓版没有 CrashReportHandler 模块（Unity 移动端不走该 API）
        CrashReportHandler.enableCaptureExceptions = false;
#endif
    }
}
