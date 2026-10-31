using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystUpdateScheduler
{
    
    internal const int Slots = 32;

    private static long _tick;
    private static readonly long[] LastSecond = new long[Slots];

    private static int CurrentSlot => (int)(_tick & (Slots - 1));

    internal static void OnTick() => _tick++;

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

internal static class Slot
{
    internal const int Notifier = 0;
    internal const int ModStamp = 1;
    internal const int ExileName = 4;
    internal const int MeetingIntro = 5;
    internal const int Console = 6;
    internal const int RolePanel = 7;
}
