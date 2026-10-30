using System;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Amethyst;

// 兼容处理。
//
// 背景（来自用户日志）：
//   · 有人把**另一份 Amethyst**（`Amethyst1.1.2内测版热修复2.dll`）和本模组放在一起，
//     同一个插件 GUID 被加载两次 → `Assembly ... is not registered in il2cpp`。
//   · `ModManager.LateUpdate()`（ModExplorer 的 ModManager）持续抛
//     `NullReferenceException`，刷屏并可能最终闪退。
//   · 同时还装了与本模组功能重叠的独立插件（BetterPingDisplay / BetterCooldownDisplay /
//     Sabotage Cooldown Display / OutfitSaver / ForceShowStart / Unlock All Skins /
//     FPS & Network Optimizer）。
//
// 处理：
//   ① 检测重复的 Amethyst 程序集并写一条明确警告；
//   ② 给 ModExplorer 的 `ModManager.LateUpdate` 挂 Finalizer，吞掉其空引用异常，
//      避免刷屏与闪退（不改动对方代码）；
//   ③ 检测重叠插件，自动停用本模组中**重复的功能**，让对方的实现生效。
internal static class AmethystCompat
{
    internal static bool OtherPing { get; private set; }
    internal static bool OtherCooldown { get; private set; }
    internal static bool OtherSabotageCd { get; private set; }
    internal static bool OtherOutfitSaver { get; private set; }
    internal static bool OtherForceStart { get; private set; }
    internal static bool OtherUnlockSkins { get; private set; }
    internal static bool OtherFps { get; private set; }
    internal static bool DuplicateAmethyst { get; private set; }

    internal static void Init(Harmony harmony)
    {
        try
        {
            DuplicateAmethyst = HasDuplicateAmethyst();
            OtherPing = HasPlugin("BetterPingDisplay");
            OtherCooldown = HasPlugin("BetterCooldownDisplay");
            OtherSabotageCd = HasPlugin("Sabotage") && HasPlugin("Cooldown");
            OtherOutfitSaver = HasPlugin("OutfitSaver");
            OtherForceStart = HasPlugin("ForceShowStart");
            OtherUnlockSkins = HasPlugin("Unlock All Skins") || HasPlugin("UnlockAllSkins");
            OtherFps = HasPlugin("FPS & Network Optimizer") || HasPlugin("FPSOptimizer");

            if (DuplicateAmethyst)
            {
                AmethystPlugin.Logger?.LogWarning(
                    "[Compat] 检测到重复的 Amethyst 程序集（例如 Amethyst_1.1.6_Windows.dll 与 Amethyst.dll 并存）。" +
                    "请只保留一份 Amethyst 插件 DLL（Amethyst.dll 或 Amethyst_<版本>_Windows.dll），否则会出现重复补丁、卡顿甚至闪退。");
            }

            PatchModExplorer(harmony);

            AmethystPlugin.Logger?.LogInfo(
                $"[Compat] overlap: ping={OtherPing} cooldown={OtherCooldown} sabotageCd={OtherSabotageCd} " +
                $"outfit={OtherOutfitSaver} forceStart={OtherForceStart} unlockSkins={OtherUnlockSkins} fps={OtherFps}");
        }
        catch { }
    }

    // 给指定方法挂 Finalizer，吞掉异常（避免异常刷屏与写日志的开销）。
    private static void PatchSwallow(Harmony harmony, Type type, string method, string label)
    {
        try
        {
            if (type == null) return;
            MethodBase m = AccessTools.Method(type, method);
            if (m == null) return;

            MethodInfo finalizer = typeof(AmethystCompat).GetMethod(
                nameof(ModManagerFinalizer), BindingFlags.Static | BindingFlags.NonPublic);
            if (finalizer == null) return;

            harmony.Patch(m, finalizer: new HarmonyMethod(finalizer));
            AmethystPlugin.Logger?.LogInfo($"[Compat] {label} guarded.");
        }
        catch { }
    }

    // ===== ① 重复 Amethyst 检测 =====
    private static bool HasDuplicateAmethyst()
    {
        try
        {
            string self = typeof(AmethystPlugin).Assembly.GetName().Name ?? string.Empty;
            string selfLocation = string.Empty;
            try { selfLocation = typeof(AmethystPlugin).Assembly.Location ?? string.Empty; } catch { }

            // 本进程里加载的其它 Amethyst 程序集
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = asm.GetName().Name ?? string.Empty;
                if (n == self) continue;
                if (n.StartsWith("Amethyst", StringComparison.OrdinalIgnoreCase))
                {
                    string loc = string.Empty;
                    try { loc = asm.Location ?? string.Empty; } catch { }
                    if (!string.IsNullOrEmpty(selfLocation) && loc == selfLocation) continue;
                    return true;
                }
            }

            // BepInEx 加载列表里同一个 GUID 出现多次
            int count = 0;
            var chain = IL2CPPChainloader.Instance;
            if (chain != null && chain.Plugins != null)
            {
                foreach (var kv in chain.Plugins)
                {
                    try
                    {
                        string guid = kv.Value != null && kv.Value.Metadata != null ? kv.Value.Metadata.GUID : null;
                        if (guid == AmethystPlugin.PluginId) count++;
                    }
                    catch { }
                }
            }
            return count > 1;
        }
        catch { return false; }
    }

    // ===== ③ 重叠插件检测 =====
    private static bool HasPlugin(string needle)
    {
        try
        {
            if (string.IsNullOrEmpty(needle)) return false;
            var chain = IL2CPPChainloader.Instance;
            if (chain == null || chain.Plugins == null) return false;

            foreach (var kv in chain.Plugins)
            {
                try
                {
                    var md = kv.Value != null ? kv.Value.Metadata : null;
                    if (md == null) continue;
                    string name = md.Name ?? string.Empty;
                    string guid = md.GUID ?? string.Empty;
                    if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (guid.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    // ===== ② ModExplorer 的 ModManager.LateUpdate 空引用拦截 =====
    //
    // ModExplorer 不是本模组，改不了它的代码；这里挂一个 Harmony Finalizer，
    // 把它 `LateUpdate` 抛出的异常吞掉，避免每帧刷屏与潜在闪退。
    private static void PatchModExplorer(Harmony harmony)
    {
        try
        {
            Type t = AccessTools.TypeByName("ModManager");
            if (t == null) return;
            PatchSwallow(harmony, t, "LateUpdate", "ModExplorer ModManager.LateUpdate");
        }
        catch { }
    }

    // 返回 null = 吞掉异常。
    private static Exception ModManagerFinalizer(Exception __exception) => null;
}
