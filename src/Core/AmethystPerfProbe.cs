using System.Diagnostics;
using UnityEngine;

namespace Amethyst;

internal static class AmethystPerfProbe
{
    internal const int Count = 24;

    private static readonly long[] Acc = new long[Count];      
    private static readonly int[] Hits = new int[Count];
    private static readonly long[] Start = new long[Count];

    private static readonly long[] WinAcc = new long[Count];    
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

    internal static void Flush()
    {
        if (!On) return;

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
        "Hud",          
        "Cache",        
        "NameFlush",    
        "Overlay",      
        "MeetingInfo",  
        "RoleTagMenu",  
        "OnGui",        
        "Menu",         
        "MenuButton",   
        "Toast",        
        "EventLog",     
        "Recap",        
        "OutfitSaver",  
        "AutoLobby",    
        "JoinDetect",   
        "AccessGuard",  
        "Rebrand",      
        "MeetingTask",  
        "CooldownUI",   
        "Other",        
        "BtnDraw",      
        "SplashDraw",   
        "ChatMain",     
        "ChatAux"       
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
