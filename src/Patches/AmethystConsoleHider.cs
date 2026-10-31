using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

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
            if (hwnd == IntPtr.Zero) return;          
            _console = hwnd;

            if (IsWindowVisible(hwnd))
                ShowWindow(hwnd, SW_HIDE);
        }
        catch { }
    }
}
