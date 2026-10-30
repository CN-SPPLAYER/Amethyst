using UnityEngine;

namespace Amethyst.Patches;

// ===== 性能：每秒更新调度器（第 180 轮重写）====//
//
// 【旧实现的问题】
//   `ShouldRun([CallerFilePath] string file, [CallerLineNumber] int line, object id)`
//   内部 `HashCode.Combine(file, line, identifier)` —— 而 `file` 是**编译期写死的
//   源文件绝对路径**（本工程约 55 个字符）。`HashCode.Combine` 走的是随机化的
//   Marvin 哈希，**每次调用都要把整条路径逐字符过一遍**。另外：
//     · `identifier` 声明为 `object` ⇒ 传 `byte`/`int` 时**每次装箱**；
//     · 每次调用一次 `Dictionary<int, Entry>.TryGetValue` + 一次索引器写入；
//     · `Time.realtimeSinceStartup` 是跨 IL2CPP 边界的原生读取。
//   这些代价与"要不要真的跑一次那段逻辑"完全无关 —— 绝大多数帧它只是返回 false。
//
// 【现在】
//   槽位在**编译期**用常量分配（`Slot.Notifier` 等），运行期只有：
//     一次位与取槽（`Slots` 取 2 的幂，`%` 退化成 `&`）+ 一次数组比较。
//   `Time.realtimeSinceStartup` 只在**槽位真的轮到自己**时才读（1/32 帧）。
//   零字符串、零哈希、零装箱、零字典。
//
// 【语义保持不变】（与 EHR `Modules\PerSecondUpdateScheduler.cs` 一致）
//   · 每个调用点被映射到固定的槽位，只有当前槽位才放行；
//   · 同一秒内同一个槽位最多跑一次（`LastSecond` 去重）。
//   效果不变：把"每帧都在算、但一秒算一次就够"的工作摊到 32 帧里分散执行，
//   帧时间变平滑，不再有周期性尖峰。
internal static class AmethystUpdateScheduler
{
    // 2 的幂，`%` 会被编译器优化成位与。
    internal const int Slots = 32;

    private static long _tick;
    private static readonly long[] LastSecond = new long[Slots];

    private static int CurrentSlot => (int)(_tick & (Slots - 1));

    /// <summary>由 <c>AmethystHud.Update</c> 每帧调用一次。</summary>
    internal static void OnTick() => _tick++;

    /// <summary>
    /// 当前是否轮到"这个调用点"更新。传入 <see cref="Slot"/> 里的编译期常量。
    /// </summary>
    internal static bool ShouldRun(int id)
    {
        int slot = id & (Slots - 1);
        if (slot != CurrentSlot) return false;

        long now = (long)Time.realtimeSinceStartup;
        if (LastSecond[slot] == now) return false;

        LastSecond[slot] = now;
        return true;
    }

    internal static void Reset()
    {
        _tick = 0;
        for (int i = 0; i < Slots; i++) LastSecond[i] = -1;
    }
}

// 调用点标识 —— **必须是编译期常量**，这样 `ShouldRun` 退化成一次位与。
//
// ⚠ 不同调用点必须拿到不同的低位组合，否则会互相抢槽位（表现为某个功能
//   莫名其妙变慢）。这里刻意取 0, 1, 4, 5, … —— 保证 0..31 内两两不冲突。
internal static class Slot
{
    internal const int Notifier = 0;
    internal const int ModStamp = 1;
    internal const int ExileName = 4;
    internal const int MeetingIntro = 5;
    internal const int Console = 6;
    internal const int RolePanel = 7;
}
