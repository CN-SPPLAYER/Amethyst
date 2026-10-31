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
        
        bool android = AmethystPlatform.IsAndroid;

        if (!android)
        {
            AmethystDependencies.Setup();
        }

        Logger = Log;
        if (!android) AmethystDependencies.FlushLog();

        AmethystPlatform.PrepareMonoModHelperPath();

        Logger.LogInfo($"[Platform] {Application.platform} dataRoot={AmethystPlatform.DataRoot}");

        AmethystExternalResources.EnsureInitialized(); 
        
        AmethystConfig.Bind(Config);
        if (android && !AmethystConfig.MenuButton.Value)
            AmethystConfig.MenuButton.Value = true;
        
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

        AmethystRoleColors.VerifyCoverage();

        AmethystRoleDescriptions.VerifyCoverage();

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
        
        AddComponent<AmethystGuiHost>();
        AddComponent<Patches.AmethystHostStateWatcher>();
        AddComponent<Patches.AmethystAccessGuard>();
        AddComponent<AmethystAutoLobbyReturn>();
        if (!android) AddComponent<AmethystMusicPlayer>();   
        AddComponent<AmethystRoomService>();
        if (!android)
        {
            
            AddComponent<AmethystUpdaterHost>();
            AddComponent<AmethystUpdateDialog>();
        }
        
        AddComponent<Patches.AmethystJoinWatchdog>();

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

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
                
                if (!_firstPatchFailureLogged)
                {
                    _firstPatchFailureLogged = true;
                    try { Logger.LogWarning("First patch failure detail:\n" + ex); } catch { }
                }

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

    private static bool SkipDiagPatch(Type type)
    {
        try
        {
            string n = type != null ? (type.FullName ?? string.Empty) : string.Empty;

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
        
        CrashReportHandler.enableCaptureExceptions = false;
#endif
    }
}
