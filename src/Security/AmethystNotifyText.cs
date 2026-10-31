using System;
using System.Collections.Generic;

namespace Amethyst;

internal static class AmethystNotifyText
{
    internal static readonly string[] Keys =
    {
        "MultiSabotage", "RapidSabotage", "SabotageByCrew", "SabotageInMeeting", "SabotageOffMap",
        "SabotageInHS", "DoorsInMeeting", "DoorsInHS_2", "KillByCrewmate", "KillImpostor",
        "DeadMurder", "RepeatKillDead", "FastKill", "TaskByImpostor", "TaskCompletionFlood",
        "TaskWithoutMap", "ExtraTask", "SetTasksInMatch", "SetLevelInMatch", "SetNameInMatch",
        "MultiSetName", "MultiSetLevel", "HTMLInName", "HostOnlyRPCByClient", "OutfitChangeInMatch",
        "GameRPCInLobby", "UnusedExiledRPC", "FakeVentId", "VentInMeeting", "VentWithoutPermission",
        "VentForAnother", "ForcedVentEject", "ReportInHS", "MeetingInHS", "VentingAsSeekerInHS",
        "VentAbuseInHS", "VoteByDead", "VoteOutsideMeeting", "ChatNoteOutsideMeeting",
        "ScanByImpostor", "ScanWithoutMap", "ScanWithoutTask", "ShapeshiftWithoutRole",
        "VanishAppearWithoutRole", "PlatformSpoof", "KnownCheatMenuSignature",
        "SpawnFloodTrimmed", "TrimmedDataFlood", "BlockedFakeMeeting", "VoteKickBlocked",
        "InvalidFC", "Bot", "Fortegreen"
    };

    private static Dictionary<string, string> _byReason;

    private static void Build()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < Keys.Length; i++)
        {
            string key = Keys[i];
            string reason, template;
            try
            {
                reason = AmethystText.T(key);
                template = AmethystText.T("Notify_" + key);
            }
            catch { continue; }

            if (string.IsNullOrEmpty(reason) || reason == key) continue;
            if (string.IsNullOrEmpty(template) || template == "Notify_" + key) continue;

            map[reason] = template;
        }

        _byReason = map;
    }

    internal static void Invalidate() => _byReason = null;

    internal static string Body(string who, string reasonText)
    {
        try
        {
            if (_byReason == null) Build();
            if (!string.IsNullOrEmpty(reasonText) && _byReason.TryGetValue(reasonText, out string tpl))
                return string.Format(tpl, who);
        }
        catch { }

        return AmethystText.T("TriggeredProtection", who, reasonText);
    }

    internal static string BodyAnonymous(string reasonText)
        => Body(AmethystText.T("Someone"), reasonText);
}
