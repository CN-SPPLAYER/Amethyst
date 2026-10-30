using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

// ===== 性能/观感：启动时隐藏黑色控制台窗口（第 173 轮）=====
//
// 背景：这个 BepInEx 安装里 `BepInEx\config\BepInEx.cfg` 的 `[Logging.Console] Enabled = true`，
//       所以游戏一启动就会弹出一个**黑色控制台窗口**显示日志。用户要求把它藏掉。
//
// 做法（两条腿，任一生效即可）：
//   ① **Win32 隐藏**：拿到控制台窗口句柄 → `ShowWindow(SW_HIDE)`。
//      用 `GetConsoleWindow()` 取当前进程的控制台（BepInEx 是 AllocConsole 出来的，
//      所以属于本进程，能直接拿到）。
//   ② **失败兜底**：如果 ① 拿不到句柄（某些平台/注入方式），就什么都不做，
//      **绝不**尝试改 BepInEx 的配置文件 —— 那是用户环境，动它会牵一发动全身。
//
// ⚠ 为什么用 `GetConsoleWindow` 而不是按窗口标题找：
//     FindWindow 按标题匹配不可靠（标题会变、多开时可能命中别人的窗口），
//     而 `GetConsoleWindow` 精确等于"本进程的控制台"，语义上不会误伤。
//
// ⚠ 为什么不用 `FreeConsole`：那会**真的把控制台拆掉**，BepInEx 的日志输出会出问题；
//     `ShowWindow(SW_HIDE)` 只是隐藏，日志依旧在正常写，想看时仍可从别处取回。
internal static class AmethystConsoleHider
{
    private const int SW_HIDE = 0;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private static IntPtr _console;
    private static bool _tried;
    private static float _nextCheck;

    internal static bool Enabled => AmethystConfig.PerfHideConsole == null || AmethystConfig.PerfHideConsole.Value;

    // 启动时调用一次 + 由 AmethystHud 低频复检。
    //
    // 为什么要"复检"：BepInEx 的控制台**可能比我们晚创建**（插件 Load 时还在初始化），
    // 也可能被别的程序重新显示出来。低频（1 秒）复查一次成本可忽略，但能保证它不会
    // 在进游戏后又冒出来（用户反馈里最常见的就是"藏了但开局又弹出来"）。
    internal static void Tick()
    {
        if (!Enabled) return;

        try
        {
            float now = Time.unscaledTime;
            if (_tried && now < _nextCheck) return;
            _nextCheck = now + 1f;
            _tried = true;

            IntPtr hwnd = _console != IntPtr.Zero ? _console : GetConsoleWindow();
            if (hwnd == IntPtr.Zero) return;          // 没有控制台：无需处理
            _console = hwnd;

            if (IsWindowVisible(hwnd))
                ShowWindow(hwnd, SW_HIDE);
        }
        catch { }
    }
}
