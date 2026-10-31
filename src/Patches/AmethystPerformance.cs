using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystPerformance
{
    
    internal const int CadenceNormal = 20;
    internal const int CadenceLowLoad = 40;   
    internal const int CadenceDeadMin = 20;   

    internal const int RenderStrideNormal = 1;
    internal const int RenderStrideLowLoad = 3;

    private static int _cfgFrame = -1;
    private static bool _lowLoad;
    private static bool _deadThrottle;
    private static int _deadSkip = 60;

    internal static bool LowLoad
    {
        get { Refresh(); return _lowLoad; }
    }

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

    internal static int CadenceFor(bool dead)
    {
        Refresh();
        if (dead && _deadThrottle)
            return _deadSkip < CadenceDeadMin ? CadenceDeadMin : _deadSkip;
        return _lowLoad ? CadenceLowLoad : CadenceNormal;
    }

    internal static int RenderStride
    {
        get { Refresh(); return _lowLoad ? RenderStrideLowLoad : RenderStrideNormal; }
    }

    internal static void Reset()
    {
        _cfgFrame = -1;
    }
}
