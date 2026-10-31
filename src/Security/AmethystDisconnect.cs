using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static class AmethystDisconnect
{
    private static readonly Dictionary<int, DisconnectReasons> Reasons = new Dictionary<int, DisconnectReasons>();

    private struct OurAction
    {
        internal bool Ban;
        internal string Reason;
        internal string PlayerName;
        internal bool Shown;
        internal float At;
    }

    private static readonly Dictionary<int, OurAction> OurActions = new Dictionary<int, OurAction>();

    private static readonly HashSet<int> AnnouncedLeaves = new HashSet<int>();

    private const float ActionTtl = 90f;

    internal static void Record(PlayerControl player, DisconnectReasons reason)
    {
        try { if (player != null) Reasons[player.OwnerId] = reason; } catch { }
    }

    internal static void RecordById(int clientId, DisconnectReasons reason)
    {
        try { if (clientId >= 0) Reasons[clientId] = reason; } catch { }
    }

    internal static DisconnectReasons Get(int clientId)
        => Reasons.TryGetValue(clientId, out DisconnectReasons r) ? r : DisconnectReasons.Unknown;

    internal static void RecordAction(int clientId, bool ban, string reason, string playerName = null)
    {
        try
        {
            if (clientId < 0) return;
            PruneExpired();

            bool shown = PushCustomMessage(playerName, ban, reason);
            OurActions[clientId] = new OurAction
            {
                Ban = ban,
                Reason = reason ?? string.Empty,
                PlayerName = playerName ?? string.Empty,
                Shown = shown,
                At = Time.realtimeSinceStartup,
            };
        }
        catch { }
    }

    internal static bool TryGetAction(int clientId, out bool ban, out string reason)
    {
        if (OurActions.TryGetValue(clientId, out OurAction a)) { ban = a.Ban; reason = a.Reason; return true; }
        ban = false; reason = null; return false;
    }

    internal static bool TryConsumeActionByName(string playerName, out bool ban, out string reason, out bool shown)
    {
        ban = false; reason = null; shown = false;
        string want = (playerName ?? string.Empty).Trim();
        if (want.Length == 0) return false;

        int hit = FindByName(want);
        if (hit < 0) return false;

        OurAction a = OurActions[hit];
        OurActions.Remove(hit);   

        ban = a.Ban;
        reason = a.Reason;
        shown = a.Shown;
        return true;
    }

    private static int FindByName(string want)
    {
        foreach (var kv in OurActions)
        {
            string have = (kv.Value.PlayerName ?? string.Empty).Trim();
            if (have.Length == 0) continue;
            if (string.Equals(have, want, System.StringComparison.Ordinal)) return kv.Key;
        }
        
        foreach (var kv in OurActions)
        {
            string have = (kv.Value.PlayerName ?? string.Empty).Trim();
            if (have.Length == 0) continue;
            if (string.Equals(have, want, System.StringComparison.OrdinalIgnoreCase)) return kv.Key;
        }
        return -1;
    }

    private static void PruneExpired()
    {
        if (OurActions.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        List<int> dead = null;
        foreach (var kv in OurActions)
        {
            if (now - kv.Value.At <= ActionTtl) continue;
            (dead ??= new List<int>()).Add(kv.Key);
        }
        if (dead == null) return;
        for (int i = 0; i < dead.Count; i++) OurActions.Remove(dead[i]);
    }

    internal static bool MarkLeaveAnnounced(int clientId) => AnnouncedLeaves.Add(clientId);

    internal static bool PushCustomMessage(string playerName, bool ban, string reason)
    {
        try
        {
            string who = (playerName ?? string.Empty).Trim();
            string head = AmethystText.T(ban ? "BannedByUs" : "KickedByUs", reason ?? string.Empty);
            string full = who.Length == 0 ? head : $"{who} {head}";

            HudManager hud = HudManager.Instance;
            if (hud == null || hud.Notifier == null) return false;
            hud.Notifier.AddDisconnectMessage(full);
            return true;
        }
        catch { return false; }
    }

    internal static void Clear()
    {
        Reasons.Clear();
        OurActions.Clear();
        AnnouncedLeaves.Clear();
    }
}

[HarmonyPatch(typeof(GameData), "HandleDisconnect", new[] { typeof(PlayerControl), typeof(DisconnectReasons) })]
internal static class AmethystDisconnectHandlePatch
{
    private static void Prefix(PlayerControl player, DisconnectReasons reason) => AmethystDisconnect.Record(player, reason);
}

[HarmonyPatch(typeof(AmongUsClient), "OnPlayerLeft")]
internal static class AmethystDisconnectLeftPatch
{
    private static void Postfix(ClientData data, DisconnectReasons reason)
    {
        try { if (data != null) AmethystDisconnect.RecordById(data.Id, reason); } catch { }
    }
}

[HarmonyPatch(typeof(GameData), "ShowNotification")]
internal static class AmethystShowNotificationGuard
{
    
    static bool Prefix([HarmonyArgument(0)] string playerName, [HarmonyArgument(1)] DisconnectReasons reason)
    {
        try
        {
            
            if (AmethystDisconnect.TryConsumeActionByName(playerName, out bool ourBan, out string reasonText, out bool shown))
            {
                if (!shown) AmethystDisconnect.PushCustomMessage(playerName, ourBan, reasonText);
                return false;   
            }

            return true;
        }
        catch { return true; }
    }
}
