using System.Diagnostics;
using UnityEngine;

namespace Amethyst;

// ===== 性能剖析探针（第 180 轮新增，默认关闭）====//
//
// 【为什么需要它】
//   本模组此前的性能问题全靠**读代码 + 推理**定位。这次抓到
//   「Prefix 跳过了游戏原版 FixedUpdate」是真凶，但用户随后反馈
//   **本地（FreePlay）模式下依然一卡一顿** —— 说明「网络包重复解析」
//   并不是那个症状的主因。此后不能再靠猜：必须拿到实测数据。
//
//   打开 `Performance.PerfProbe` 后，它按**每个每帧入口**统计耗时，
//   每 5 秒往 BepInEx 日志写一次汇总。跑一局就能直接看到
//   「这 60 毫秒里，谁吃了 40 毫秒」。
//
// 【它自己的成本】
//   每个被测段两次 `Stopwatch.GetTimestamp()`（读系统时钟，几十纳秒）。
//   默认**关闭**时 `Begin` 只读一个已缓存的 bool、`End` 见到 -1 直接返回。
//
// 【用法】
//   int p = AmethystPerfProbe.Begin(Section.S_Hud);
//   try { ...被测代码... }
//   finally { AmethystPerfProbe.End(p, Section.S_Hud); }
//   `Begin` 在关闭时返回 -1，所以调用点**不必写 if**。
internal static class AmethystPerfProbe
{
    internal const int Count = 24;

    private static readonly long[] Acc = new long[Count];      // 本帧
    private static readonly int[] Hits = new int[Count];
    private static readonly long[] Start = new long[Count];

    private static readonly long[] WinAcc = new long[Count];    // 汇总窗口
    private static readonly int[] WinHits = new int[Count];
    private static readonly long[] WinFrames = new long[Count];

    private static bool _on;
    private static int _onFrame = -1;
    private static float _nextLog;
    private static int _windowFrames;
    private static float _maxFrameMs;
    private static long _lastFrameStamp;

    internal static bool On
    {
        get
        {
            int frame = Time.frameCount;
            if (frame == _onFrame) return _on;
            _onFrame = frame;
            try { _on = AmethystConfig.PerfProbe != null && AmethystConfig.PerfProbe.Value; }
            catch { _on = false; }
            return _on;
        }
    }

    // ⚠ 这两处必须走 `On` **属性**（它会按帧刷新 `_on`），不能直接读字段。
    //   直接读 `_on` 的话，只有读属性才会刷新它 —— 而探针关闭时没人读属性，
    //   `_on` 会永远停在 false，表现为"开了开关却一条日志都没有"。
    internal static int Begin(int section)
    {
        if (!On || (uint)section >= Count) return -1;
        Start[section] = Stopwatch.GetTimestamp();
        return section;
    }

    internal static void End(int token, int section)
    {
        if (token < 0 || (uint)section >= Count) return;
        long dt = Stopwatch.GetTimestamp() - Start[section];
        if (dt < 0) return;
        Acc[section] += dt;
        Hits[section]++;
    }

    /// <summary>每帧由 <c>AmethystHud.Update</c> 调用一次。</summary>
    internal static void Flush()
    {
        if (!On) return;

        // 顺便量一下「本模组总共占了多少」和「这一帧本身有多长」——
        // 两者一比就知道是模组的问题还是游戏本身的问题。
        long now = Stopwatch.GetTimestamp();
        if (_lastFrameStamp != 0)
        {
            double ms = (now - _lastFrameStamp) / (double)Stopwatch.Frequency * 1000.0;
            if (ms > _maxFrameMs) _maxFrameMs = (float)ms;
        }
        _lastFrameStamp = now;

        _windowFrames++;
        for (int i = 0; i < Count; i++)
        {
            WinAcc[i] += Acc[i];
            WinHits[i] += Hits[i];
            Acc[i] = 0;
            Hits[i] = 0;
        }

        float t = Time.unscaledTime;
        if (t < _nextLog) return;
        _nextLog = t + 5f;

        Log();
        for (int i = 0; i < Count; i++) { WinAcc[i] = 0; WinHits[i] = 0; }
        _windowFrames = 0;
        _maxFrameMs = 0f;
    }

    private static void Log()
    {
        try
        {
            if (AmethystPlugin.Logger == null || _windowFrames <= 0) return;

            double freq = Stopwatch.Frequency;
            double frames = _windowFrames;
            double fps = frames / 5.0;

            var sb = new System.Text.StringBuilder(512);
            sb.Append("[PerfProbe] ").Append((int)frames).Append("f ")
              .Append(fps.ToString("F0")).Append("fps peak=").Append(_maxFrameMs.ToString("F1")).Append("ms | ");

            double totalMs = 0;
            for (int i = 0; i < Count; i++) totalMs += WinAcc[i];
            totalMs = totalMs / freq * 1000.0 / frames;
            sb.Append("TOTAL=").Append(totalMs.ToString("F3")).Append("ms/f | ");

            for (int i = 0; i < Count; i++)
            {
                if (WinHits[i] == 0) continue;
                double perFrame = WinAcc[i] / freq * 1000.0 / frames;
                double perCall = WinAcc[i] / freq * 1000.0 / WinHits[i];
                sb.Append(Names[i]).Append('=')
                  .Append(perFrame.ToString("F3")).Append('/')
                  .Append(perCall.ToString("F4"))
                  .Append("ms x").Append(WinHits[i] / _windowFrames)
                  .Append("  ");
            }

            AmethystPlugin.Logger.LogInfo(sb.ToString());
        }
        catch { }
    }

    private static readonly string[] Names =
    {
        "Hud",          // 0  AmethystHud.Update
        "Cache",        // 1  玩家快照（错峰刷新）
        "NameFlush",    // 2  名字 / 色盲文字 / 变形者菜单
        "Overlay",      // 3  每玩家头顶覆盖层（数量 = 玩家数）
        "MeetingInfo",  // 4  会议格子身份
        "RoleTagMenu",  // 5  身份标记选择器
        "OnGui",        // 6  IMGUI 总入口
        "Menu",         // 7  模组菜单
        "MenuButton",   // 8  悬浮按钮
        "Toast",        // 9  模组通知
        "EventLog",     // 10 事件日志窗口
        "Recap",        // 11 复盘窗口
        "OutfitSaver",  // 12 服装保存
        "AutoLobby",    // 13 自动返回大厅
        "JoinDetect",   // 14 进出房检测 + 历史记录
        "AccessGuard",  // 15 访问守卫轮询
        "Rebrand",      // 16 品牌替换（每帧 Tick）
        "MeetingTask",  // 17 会议任务面板
        "CooldownUI",   // 18 冷却/聊天 UI 状态
        "Other",        // 19 其它零散
        "BtnDraw",      // 20 悬浮按钮绘制
        "SplashDraw",   // 21 启动界面绘制
        "ChatMain",     // 22 ChatController.Update 主补丁（输入策略/历史/主题/复制/气泡动画）
        "ChatAux"       // 23 ChatController.Update 上另外三个 Postfix + SetVisible 前缀
    };
}

internal static class Section
{
    internal const int S_Hud = 0;
    internal const int S_Cache = 1;
    internal const int S_NameFlush = 2;
    internal const int S_Overlay = 3;
    internal const int S_MeetingInfo = 4;
    internal const int S_RoleTagMenu = 5;
    internal const int S_OnGui = 6;
    internal const int S_Menu = 7;
    internal const int S_MenuButton = 8;
    internal const int S_Toast = 9;
    internal const int S_EventLog = 10;
    internal const int S_Recap = 11;
    internal const int S_OutfitSaver = 12;
    internal const int S_AutoLobby = 13;
    internal const int S_JoinDetect = 14;
    internal const int S_AccessGuard = 15;
    internal const int S_Rebrand = 16;
    internal const int S_MeetingTask = 17;
    internal const int S_CooldownUI = 18;
    internal const int S_Other = 19;
    internal const int S_MenuButtonDraw = 20;
    internal const int S_Splash = 21;
    internal const int S_ChatMain = 22;
    internal const int S_ChatAux = 23;
}
