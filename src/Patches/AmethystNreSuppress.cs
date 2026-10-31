using System;
using HarmonyLib;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(HudManager), "Update")]
internal static class AmethystNreSuppressHudUpdate
{
    private static Exception Finalizer(Exception __exception) => null;
}

[HarmonyPatch(typeof(HudManager), "Start")]
internal static class AmethystNreSuppressHudStart
{
    private static Exception Finalizer(Exception __exception) => null;
}

[HarmonyPatch(typeof(MeetingHud), "Update")]
internal static class AmethystNreSuppressMeetingUpdate
{
    private static Exception Finalizer(Exception __exception) => null;
}
