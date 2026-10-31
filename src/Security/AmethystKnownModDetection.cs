using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static partial class AmethystKnownModDetection
{
    
    internal static readonly HashSet<byte> KillNetworkRpcs = new HashSet<byte> { 250 }; 
    internal static readonly HashSet<byte> BetterAmongUsRpcs = new HashSet<byte> { 151, 152 };
    internal static readonly HashSet<byte> HostGuardRpcs = new HashSet<byte> { 176 };
    internal static readonly HashSet<byte> ChocooMenuRpcs = new HashSet<byte> { 121 };
    internal static readonly HashSet<byte> SlopMenuCrewRpcs = new HashSet<byte> { 201, 202 };
    internal static readonly HashSet<byte> UnknownModRpcs = new HashSet<byte> { 103 };

    internal static readonly HashSet<byte> SickoMenuAltRpcs = new HashSet<byte> { 164 };
    internal static readonly HashSet<byte> BetterAmongUsAltRpcs = new HashSet<byte> { 150 };
    internal static readonly HashSet<byte> GaffMenuRpcs = new HashSet<byte> { 144, 145 };
    internal static readonly HashSet<byte> PolarClientRpcs = new HashSet<byte> { 195, 204 };
    internal static readonly HashSet<byte> BanModRpcs = new HashSet<byte> { 219, 240, 212 };

    internal static readonly HashSet<byte> AllKnownModRpcs = new HashSet<byte>();

    private static readonly Dictionary<byte, HashSet<string>> DetectedMods = new Dictionary<byte, HashSet<string>>();

    static AmethystKnownModDetection()
    {
        foreach (var id in KillNetworkRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in BetterAmongUsRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in HostGuardRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in ChocooMenuRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in SlopMenuCrewRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in UnknownModRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in SickoMenuAltRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in BetterAmongUsAltRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in GaffMenuRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in PolarClientRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in BanModRpcs) AllKnownModRpcs.Add(id);
    }





    private const byte VanillaTop = 90;

    private static readonly Dictionary<byte, string> Claimed = new Dictionary<byte, string>();
    private static readonly Dictionary<int, int> UnknownHits = new Dictionary<int, int>();

    private static readonly HashSet<byte> LoggedRaw = new HashSet<byte>();




    internal static void CheckRpc(PlayerControl sender, byte callId, MessageReader reader)
    {
        if (sender == null || sender == PlayerControl.LocalPlayer) return;
        if (sender.Data == null) return;

        if (callId == 242) return;

        string modName = PayloadName(sender, callId, reader);
        if (modName == null) modName = ClaimedName(sender, callId, reader);
        if (modName == null) modName = TableName(callId);
        if (modName == null) modName = UnknownName(sender.PlayerId, callId);
        if (modName == null) return;

        if (!DetectedMods.TryGetValue(sender.PlayerId, out var playerMods) || playerMods == null)
        {
            playerMods = new HashSet<string>(StringComparer.Ordinal);
            DetectedMods[sender.PlayerId] = playerMods;
        }

        if (!playerMods.Add(modName)) return;

        string playerName = AmethystColors.Name(sender) ?? (sender.Data != null ? sender.Data.PlayerName : $"#{sender.OwnerId}");
        AmethystToast.Push(
            AmethystText.T("ModClient"),
            AmethystText.T("Using", playerName, modName),
            5.0f, AmethystNotifyKind.Warning);

        AmethystPlugin.Logger?.LogInfo($"[ModDetect] {playerName} detected using {modName} (RPC {callId})");
    }





}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static partial class AmethystKnownModDetectionResetPatch
{
    private static void Postfix() => AmethystKnownModDetection.Reset();
}

[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class AmethystKnownModDetectionPatch
{
    private static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
        => AmethystKnownModDetection.Observe(__instance, callId, reader);
}

[HarmonyPatch(typeof(PlayerPhysics), "HandleRpc")]
internal static class AmethystKnownModDetectionPhysicsPatch
{
    private static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        PlayerControl sender = __instance != null ? __instance.myPlayer : null;
        AmethystKnownModDetection.Observe(sender, callId, reader);
    }
}

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystKnownModDetectionShipPatch
{
    private static void Prefix(ShipStatus __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!AmethystConfig.RpcGuard.Value || AmongUsClient.Instance == null) return;
        
    }
}

internal static class AmethystFriendCodeCheck
{
    private static readonly HashSet<string> BlockedFriendCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    internal static void Reset()
    {
        BlockedFriendCodes.Clear();
    }

    internal static bool IsBlocked(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode)) return false;
        return BlockedFriendCodes.Contains(friendCode.Trim().ToLowerInvariant());
    }

    internal static void AddBlocked(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode)) return;
        BlockedFriendCodes.Add(friendCode.Trim().ToLowerInvariant());
    }

    internal static void LoadFromConfig()
    {
        Reset();
        if (AmethystConfig.AccessBanEnabled != null && AmethystConfig.AccessBanEnabled.Value && AmethystConfig.BanList?.Value != null)
        {
            foreach (var item in AmethystConfig.BanList.Value.Split(';'))
            {
                var parts = item.Split('|');
                if (parts.Length >= 2)
                {
                    string fc = parts[1].Trim();
                    if (!string.IsNullOrWhiteSpace(fc))
                        AddBlocked(fc);
                }
            }
        }

        if (AmethystConfig.CloudBanEnabled != null && AmethystConfig.CloudBanEnabled.Value)
        {
            var cloudEntries = AmethystAccess.CloudBanEntries;
            if (cloudEntries != null)
            {
                for (int i = 0; i < cloudEntries.Count; i++)
                {
                    var e = cloudEntries[i];
                    if (!string.IsNullOrWhiteSpace(e.Code))
                        AddBlocked(e.Code);
                }
            }
        }
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnPlayerJoined")]
internal static class AmethystFriendCodeCheckPatch
{
    private static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData data)
    {
        bool banEnabled = (AmethystConfig.AccessBanEnabled != null && AmethystConfig.AccessBanEnabled.Value)
            || (AmethystConfig.CloudBanEnabled != null && AmethystConfig.CloudBanEnabled.Value);
        if (!banEnabled || data == null) return;
        AmethystFriendCodeCheck.LoadFromConfig();

        string fc = data.FriendCode;
        if (AmethystFriendCodeCheck.IsBlocked(fc))
        {
            string playerName = AmethystColors.Name(data.Character) ?? data.PlayerName;
            AmethystToast.Push(
                AmethystText.T("BanList"),
                AmethystText.T("FriendCodeIsBlocked", fc, playerName),
                5.0f, AmethystNotifyKind.Danger);

            if (__instance != null && __instance.AmHost)
            {
                AmethystAccess.Kick((InnerNetClient)__instance, data.Id, true, true);
            }
        }
    }
}
