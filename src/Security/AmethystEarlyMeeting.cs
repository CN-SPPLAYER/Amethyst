using HarmonyLib;
using Hazel;
using UnityEngine;
using Amethyst.Patches;

namespace Amethyst;

internal static class AmethystEarlyMeeting
{
    private static bool roundBlockArmed;
    private static float shhhBlockUntil = -1f;
    private static float _lastNotify;

    internal static bool On => AmethystConfig.BlockEarlyMeetings != null && AmethystConfig.BlockEarlyMeetings.Value;

    internal static void MarkGameStart()
    {
        
        bool wasArmed = roundBlockArmed;
        roundBlockArmed = true;
        shhhBlockUntil = -1f;

        if (!wasArmed)
            AmethystPlugin.Logger?.LogInfo("[MeetingGuard] Host click detected. Waiting for SHHHH to unblock.");
    }

    internal static void Reset()
    {
        roundBlockArmed = false;
        shhhBlockUntil = -1f;
    }

    internal static void MarkShhhSeen()
    {
        if (AmongUsClient.Instance == null) return;
        bool changed = !roundBlockArmed || shhhBlockUntil < 0f;
        roundBlockArmed = true;
        shhhBlockUntil = Time.unscaledTime + 12f;

        if (changed)
            AmethystPlugin.Logger?.LogInfo($"[MeetingGuard] SHHHH seen. Meetings blocked for 12s until: {shhhBlockUntil}");
    }

    internal static bool IsMeetingDelayActive()
    {
        return AmethystRpcGuard.MeetingGuardArmed();
    }

    internal static void NotifyAttempt(PlayerControl who)
    {
        try
        {
            if (who == null) return;
            string name = AmethystColors.Name(who) ?? (who.Data != null && who.Data.PlayerName != null ? who.Data.PlayerName : "?");
            float now = Time.unscaledTime;
            if (now - _lastNotify < 2f) return;
            _lastNotify = now;
            AmethystToast.Push(AmethystText.T("Guard"),
                AmethystText.T("TriggeredProtectionMeetingDetection", name),
                5.0f, AmethystNotifyKind.Warning);
        }
        catch { }
    }

    private static bool Blocked()
    {
        try
        {
            if (AmongUsClient.Instance == null) return false;
            if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay) return false;
            if (!roundBlockArmed) return false;
            if (shhhBlockUntil < 0f) return true;
            return Time.unscaledTime < shhhBlockUntil;
        }
        catch { return true; }
    }

    internal static bool ShouldBlock(string src)
    {
        if (!On) return false;
        return Blocked();
    }

    internal static bool ShouldBlockButton(string src)
    {
        if (!On) return false;
        return Blocked();
    }
}

[HarmonyPatch(typeof(ShhhBehaviour), "PlayAnimation")]
internal static class AmethystEarlyMeetingShhhPatch
{
    public static void Prefix()
    {
        AmethystEarlyMeeting.MarkShhhSeen();
        
        try { AmethystBetterLoadingPatch.HideBar(); } catch { }
    }
}

[HarmonyPatch(typeof(GameStartManager), "BeginGame")]
internal static class AmethystEarlyMeetingGameStartPatch
{
    public static void Prefix() => AmethystEarlyMeeting.MarkGameStart();
}

[HarmonyPatch(typeof(PlayerControl), "StartMeeting")]
internal static class AmethystEarlyMeetingStartPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        if (!AmethystEarlyMeeting.ShouldBlock("start")) return true;
        if (__instance != null && __instance != PlayerControl.LocalPlayer)
            AmethystEarlyMeeting.NotifyAttempt(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), "RpcStartMeeting")]
internal static class AmethystEarlyMeetingRpcStartPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        if (!AmethystEarlyMeeting.ShouldBlock("rpc-start")) return true;
        if (__instance != null && __instance != PlayerControl.LocalPlayer)
            AmethystEarlyMeeting.NotifyAttempt(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), "ReportDeadBody")]
internal static class AmethystEarlyMeetingReportBodyPatch
{
    public static bool Prefix(PlayerControl __instance, NetworkedPlayerInfo target)
    {
        if (!AmethystEarlyMeeting.ShouldBlock("report-body")) return true;
        if (__instance != null && __instance != PlayerControl.LocalPlayer)
            AmethystEarlyMeeting.NotifyAttempt(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(MeetingRoomManager), "AssignSelf")]
internal static class AmethystEarlyMeetingAssignPatch
{
    public static bool Prefix(PlayerControl reporter)
    {
        if (!AmethystEarlyMeeting.On || !AmethystEarlyMeeting.ShouldBlock("assign")) return true;
        AmethystEarlyMeeting.NotifyAttempt(reporter);
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), "CmdReportDeadBody")]
internal static class AmethystEarlyMeetingCmdReportPatch
{
    public static bool Prefix(NetworkedPlayerInfo target)
    {
        if (target == null && AmethystEarlyMeeting.ShouldBlock("report")) return false;
        return true;
    }
}

[HarmonyPatch(typeof(EmergencyMinigame), "CallMeeting")]
internal static class AmethystEarlyMeetingCallPatch
{
    public static bool Prefix(EmergencyMinigame __instance)
    {
        if (!AmethystEarlyMeeting.ShouldBlockButton("button-call")) return true;
        try { __instance?.Close(); } catch { }
        return false;
    }
}

[HarmonyPatch(typeof(EmergencyMinigame), "Begin")]
internal static class AmethystEarlyMeetingBeginPatch
{
    public static bool Prefix(EmergencyMinigame __instance)
    {
        if (!AmethystEarlyMeeting.ShouldBlockButton("button")) return true;
        try
        {
            if (__instance != null && __instance.gameObject != null)
                __instance.gameObject.SetActive(false);
        }
        catch { }
        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystEarlyMeetingJoinPatch
{
    public static void Postfix()
    {
        AmethystEarlyMeeting.Reset();
        AmethystRpcGuard.ClearMeetingOpened();
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameEnd")]
internal static class AmethystEarlyMeetingEndPatch
{
    public static void Postfix()
    {
        AmethystEarlyMeeting.Reset();
        AmethystRpcGuard.ClearMeetingOpened();
    }
}
