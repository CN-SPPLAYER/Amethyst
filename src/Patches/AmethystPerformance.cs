using UnityEngine;

namespace Amethyst.Patches;

// ===== 性能调度策略（第 180 轮重写）=====
//
// ⚠⚠⚠ 重大更正：本文件**不再挂任何 Harmony 补丁**。这与旧版完全不同。
//
// 【旧版做错了什么】
//   旧版在 `PlayerControl.FixedUpdate` 上挂 `Prefix` 并返回 false，想"跳过更新"。
//   但 Prefix 返回 false 跳过的是**游戏原版的方法体**，不是我们的工作 ——
//   我们本来就没在 `PlayerControl.FixedUpdate` 里做任何事。
//   `PlayerControl.FixedUpdate` 里跑的是游戏自己的逻辑
//   （技能 CD tick、变形时长、能力可用性判定、影子更新…；
//     dnSpy 核实本版本它是 `private void`，内含 `_FixedUpdate_b__0(IUsable j)` 那个
//     遍历所有 `IUsable` 能力的 lambda）。
//   于是：开了「低负载模式」且人数 > 8 时，**每帧只有 1 名玩家的原版 FixedUpdate 会执行**，
//   其余 N-1 名被整个跳过 —— 技能/变形会异常，而且轮转索引每帧推进、
//   每名玩家每 N 帧才被放行一次，表现出来正是用户报告的「走几步顿一下」。
//
// 【EHR 的真身（对照）】
//   `参考模组\EndlessHostRoles-8.0.1\Patches\PlayerControlPatch.cs` 第 1558 行：
//       //[HarmonyPatch(typeof(PlayerControl), "FixedUpdate")]
//   —— **Harmony 声明是被注释掉的**。`FixedUpdatePatch` 不是补丁，而是被
//   `Modules\FixedUpdateCaller.cs` 第 161 行手动调用的普通类：
//       FixedUpdatePatch.Postfix(pc, NonLowLoadPlayerIndex != playerIndex);
//   `lowLoad` 只是一个 bool 参数，在 `DoPostfix` 里用 `if (!lowLoad) { ... }`
//   决定**EHR 自己的逻辑**跑不跑。**原版 `PlayerControl.FixedUpdate` 永远正常执行。**
//   旧版把"低负载"误读成了"跳过原版方法体"，是移植时最关键的一处理解偏差。
//
// 【现在】
//   本文件退化为纯粹的**策略提供者**：只回答"我们自己的快照该多久刷一次、
//   每帧该处理几名玩家"，由 `AmethystPlayerCacheStore` 与
//   `AmethystPlayerOverlay` 消费。省下的是**我们自己的**开销，绝不碰游戏逻辑。
internal static class AmethystPerformance
{
    // ---- 快照刷新间隔（帧）----
    // 60 FPS 下 20 帧 ≈ 0.33 秒。名字 / 身份 / 任务进度这类字段延迟 0.33 秒刷新，
    // 人眼完全看不出来。
    internal const int CadenceNormal = 20;
    internal const int CadenceLowLoad = 40;   // 「低负载模式」开启时翻倍
    internal const int CadenceDeadMin = 20;   // 死人的刷新间隔下限

    // 每帧处理玩家数的分母（渲染轮转用）。1 = 每帧全处理。
    internal const int RenderStrideNormal = 1;
    internal const int RenderStrideLowLoad = 3;

    // 配置值每帧只读一次（`ConfigEntry.Value` 也要跨边界）。
    private static int _cfgFrame = -1;
    private static bool _lowLoad;
    private static bool _deadThrottle;
    private static int _deadSkip = 60;

    /// <summary>「低负载模式」：降低我们自己的快照频率与每帧处理量。</summary>
    internal static bool LowLoad
    {
        get { Refresh(); return _lowLoad; }
    }

    /// <summary>「不更新死亡玩家」：死人的快照用更长的间隔刷新。</summary>
    internal static bool DeadThrottle
    {
        get { Refresh(); return _deadThrottle; }
    }

    private static void Refresh()
    {
        int frame = Time.frameCount;
        if (frame == _cfgFrame) return;
        _cfgFrame = frame;

        try { _lowLoad = AmethystConfig.PerfLowLoad != null && AmethystConfig.PerfLowLoad.Value; }
        catch { _lowLoad = false; }

        try { _deadThrottle = AmethystConfig.PerfDontUpdateDead != null && AmethystConfig.PerfDontUpdateDead.Value; }
        catch { _deadThrottle = false; }

        try
        {
            int v = 60;
            if (AmethystConfig.PerfDeadSkipFrames != null)
                v = Mathf.Clamp(AmethystConfig.PerfDeadSkipFrames.Value, 2, 600);
            _deadSkip = v;
        }
        catch { _deadSkip = 60; }
    }

    /// <summary>一名玩家的快照刷新间隔（帧）。</summary>
    internal static int CadenceFor(bool dead)
    {
        Refresh();
        if (dead && _deadThrottle)
            return _deadSkip < CadenceDeadMin ? CadenceDeadMin : _deadSkip;
        return _lowLoad ? CadenceLowLoad : CadenceNormal;
    }

    /// <summary>每帧只处理 1/N 的玩家（N=1 表示全处理）。</summary>
    internal static int RenderStride
    {
        get { Refresh(); return _lowLoad ? RenderStrideLowLoad : RenderStrideNormal; }
    }

    internal static void Reset()
    {
        _cfgFrame = -1;
    }
}
