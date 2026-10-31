using HarmonyLib;
using UnityEngine;
using Amethyst.Patches;

namespace Amethyst;

internal static class AmethystAntiFakeMeeting
{
    private static float lastNote;

    internal static bool On => AmethystConfig.BlockFakeMeetings != null && AmethystConfig.BlockFakeMeetings.Value;

    internal static bool Illegal()
    {
        try
        {
            
            var lobby = LobbyBehaviour.Instance;
            bool inLobby = lobby != null && lobby.gameObject != null && lobby.gameObject.activeInHierarchy;
            return ShipStatus.Instance == null || inLobby;
        }
        catch { return false; }
    }

    internal static void Kill(MeetingHud hud)
    {
        try { if (hud != null) Object.Destroy(hud.gameObject); } catch { }

        if (Time.unscaledTime - lastNote < 1f) return;
        lastNote = Time.unscaledTime;
        AmethystSecurityNotify.Fire("BlockedFakeMeeting");
    }
}

[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystFakeMeetingStartPatch
{
    public static bool Prefix(MeetingHud __instance)
    {
        
        AmethystRpcGuard.MarkMeetingOpened();

        if (!AmethystAntiFakeMeeting.On || !AmethystAntiFakeMeeting.Illegal()) return true;
        AmethystAntiFakeMeeting.Kill(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), "Close")]
internal static class AmethystMeetingClosePatch
{
    public static void Postfix()
    {
        AmethystRpcGuard.ClearMeetingOpened();
        AmethystRpcGuard.ClearRapidSabotage();
    }
}
