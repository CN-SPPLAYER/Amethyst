using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;


[HarmonyPatch(typeof(VoteBanSystem), "HandleRpc")]
internal static class AmethystVoteBanProtect
{
    private static bool Prefix(VoteBanSystem __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (AmethystConfig.RpcGuardVoteBan == null || !AmethystConfig.RpcGuardVoteBan.Value) return true;   
        if (!AmethystRpcGuard.On() || __instance == null || reader == null) return true;
        if (callId != (byte)RpcCalls.AddVote)
        {
            
            if (AmethystRpcGuard.StructureGuardOn
                && AmethystRpcGuard.IsWrongObject(AmethystRpcGuard.RpcObjectKind.VoteBan, callId))
            {
                AmethystRpcGuard.NotifyOnce("rpc-wrong-object", AmethystText.T("RpcWrongObject"));
                return false;
            }
            return true;
        }

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        PlayerControl voter = null;
        try
        {
            int sourceId = reader.ReadInt32();
            int targetId = reader.ReadInt32();

            ClientData sourceClient = AmongUsClient.Instance.FindClientById(sourceId);
            if (sourceClient != null && sourceClient.Character != null)
                voter = sourceClient.Character;

            if (voter != null && voter.Data != null && voter.Data.IsDead)
            {
                cheat = true;
                reason = AmethystText.T("VoteByDead");
            }
            
            else if (!AmethystRpcGuard.MeetingGuardArmed())
            {
                cheat = true;
                reason = AmethystText.T("VoteOutsideMeeting");
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat)
        {
            string who = voter != null && voter.Data != null ? voter.Data.PlayerName : AmethystText.T("Unknown");
            int cid = voter != null ? voter.OwnerId : -1;
            if (AmethystRpcGuard.AmHost() && cid >= 0 && cid != AmongUsClient.Instance.HostId)
            {
                
                AmethystRpcGuard.Flag(voter, reason);
                return false;
            }
            else
            {
                
                AmethystRpcGuard.NotifyOnce("vote-cheat-" + reason, who + " " + reason);
            }
        }
        return true;
    }
}


[HarmonyPatch(typeof(MeetingHud), "HandleRpc")]
internal static class AmethystRpcGuardMeeting
{
    private static bool Prefix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        
        if (callId == (byte)RpcCalls.VotingComplete)
        {
            if (!AmethystRpcGuard.CallAllowed(callId)) return true;
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                AmethystRpcGuard.NotifyOnce("voting-complete-forged", AmethystText.T("HostOnlyRPCByClient"));
                return false;
            }
            return true;
        }

        if (!AmethystRpcGuard.StructureGuardOn) return true;
        if (callId != (byte)RpcCalls.CastVote && callId != (byte)RpcCalls.ClearVote) return true;

        if (!AmethystRpcGuard.MeetingGuardArmed())
        {
            AmethystRpcGuard.NotifyOnce("vote-outside-meeting", AmethystText.T("VoteOutsideMeeting"));
            return false;
        }
        return true;
    }
}


[HarmonyPatch(typeof(MeetingHud), "OnDisable")]
internal static class AmethystMeetingEndPatch
{
    private static void Postfix()
    {
        
        AmethystRpcGuard.ClearRapidSabotage();
        AmethystRpcGuard.ResetRoundState();
        try
        {
            AmethystPlugin.Logger?.LogInfo(
                "[Guard] meeting hud disabled: meetingActive=" + AmethystRpcGuard.MeetingActive()
                + " exileActive=" + AmethystRpcGuard.ExileActive()
                + " inMatch=" + AmethystRpcGuard.InMatch());
        }
        catch { }
    }
}
