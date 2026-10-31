using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;


[HarmonyPatch(typeof(PlayerPhysics), "HandleRpc")]
internal static class AmethystRpcGuardVent
{
    private static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        bool isVent = callId == (byte)RpcCalls.EnterVent || callId == (byte)RpcCalls.ExitVent;
        bool isMove = callId == (byte)RpcCalls.ClimbLadder || callId == (byte)RpcCalls.UsePlatform;
        if (!isVent && !isMove) return true;
        if (isVent && (AmethystConfig.RpcGuardVent == null || !AmethystConfig.RpcGuardVent.Value)) return true;
        if (!AmethystRpcGuard.On() || __instance == null || reader == null) return true;
        if (callId != (byte)RpcCalls.EnterVent && callId != (byte)RpcCalls.ExitVent
            && callId != (byte)RpcCalls.ClimbLadder && callId != (byte)RpcCalls.UsePlatform) return true;

        PlayerControl actor = __instance.myPlayer;
        if (actor == null || actor == PlayerControl.LocalPlayer || actor.Data == null) return true;

        if (AmethystRpcGuard.StructureGuardOn && AmongUsClient.Instance != null
            && actor.OwnerId != AmongUsClient.Instance.HostId)
        {
            if (AmethystRpcGuard.IsWrongObject(AmethystRpcGuard.RpcObjectKind.Physics, callId))
            {
                AmethystRpcGuard.Flag(actor, AmethystText.T("RpcWrongObject"));
                return false;
            }
            
        }

        if (callId == (byte)RpcCalls.ClimbLadder || callId == (byte)RpcCalls.UsePlatform)
        {
            bool bad = false;
            string why = null;

            if (ShipStatus.Instance == null)
            {
                bad = true;
                why = AmethystText.T("MovementNoMap");
            }
            else if (callId == (byte)RpcCalls.ClimbLadder)
            {
                int map = AmethystRpcGuard.CurrentMapId();
                if (map != 4 && map != 5) { bad = true; why = AmethystText.T("LadderWrongMap"); }
                else if (actor.Data.IsDead) { bad = true; why = AmethystText.T("MovementWhileDead"); }
            }
            else
            {
                if (AmethystRpcGuard.CurrentMapId() != 4) { bad = true; why = AmethystText.T("PlatformWrongMap"); }
                else if (AmethystRpcGuard.IsHS()) { bad = true; why = AmethystText.T("PlatformInHS"); }
                else if (actor.Data.IsDead) { bad = true; why = AmethystText.T("MovementWhileDead"); }
            }

            if (bad)
            {
                AmethystRpcGuard.Flag(actor, why);
                return !AmethystRpcGuard.ShouldDropRpc();
            }
            return true;
        }

        if (AmethystRpcGuard.IsHS()) return true;
        
        if (!AmethystRpcGuard.VentRulesEnabled()) return true;

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        try
        {
            int ventId = reader.ReadPackedInt32();
            if (!AmethystRpcGuard.RealVent(ventId)) { cheat = true; reason = AmethystText.T("FakeVentId"); }
            else if (AmethystRpcGuard.MeetingGuardArmed())
            {
                
                cheat = true;
                reason = AmethystText.T("VentInMeeting");
            }
            else if (AmethystRpcGuard.ShipGraceOver() && AmethystRpcGuard.RoleReady(actor) && !actor.Data.Role.CanVent)
            { cheat = true; reason = AmethystText.T("VentWithoutPermission"); }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat)
        {
            AmethystRpcGuard.Flag(actor, reason);
            return !AmethystRpcGuard.ShouldDropRpc();
        }
        return true;
    }
}


[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystRpcGuardShip
{
    private static bool Prefix(ShipStatus __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {

        if (AmethystConfig.RpcGuardShip == null || !AmethystConfig.RpcGuardShip.Value) return true;   
        if (!AmethystRpcGuard.On() || __instance == null || reader == null) return true;

        if (AmethystRpcGuard.StructureGuardOn)
        {
            if (AmethystRpcGuard.IsWrongObject(AmethystRpcGuard.RpcObjectKind.Ship, callId))
            {
                AmethystRpcGuard.NotifyOnce("rpc-wrong-object", AmethystText.T("RpcWrongObject"));
                return false;
            }
            
        }

        int pos = reader.Position;
        bool block = false;
        bool victimForced = false; 
        PlayerControl actor = null;
        string reason = null;
        try
        {
            
            if (AmethystRpcGuard.IsHS()) return true;

            if (callId == (byte)RpcCalls.CloseDoorsOfType)
            {
                
                if (AmethystRpcGuard.IsHS())
                {
                    block = true;
                    reason = AmethystText.T("DoorsInHS");
                }
                
                else if (AmethystRpcGuard.MeetingGuardArmed())
                {
                    block = true;
                    reason = AmethystText.T("DoorsInMeeting");
                }
            }
            else if (callId == (byte)RpcCalls.UpdateSystem)
            {
                int sys = reader.ReadByte();
                actor = reader.ReadNetObject<PlayerControl>();

                if (sys == (int)SystemTypes.Ventilation)
                {
                    
                    if (!AmethystRpcGuard.VentRulesEnabled()) return true;
                    reader.ReadUInt16();
                    int op = reader.Position < reader.Length ? reader.ReadByte() : -1;
                    int vid = reader.Position < reader.Length ? reader.ReadByte() : -1;

                    if (op == (int)VentilationSystem.Operation.Enter)
                    {
                        
                        if (AmethystRpcGuard.MeetingGuardArmed())
                        {
                            block = true;
                            reason = AmethystText.T("VentInMeeting");
                        }
                        else if (AmethystRpcGuard.ShipGraceOver())
                        {
                            if (!AmethystRpcGuard.RealVent(vid)) { block = true; reason = AmethystText.T("FakeVentId"); }
                            else if (actor != null && actor != PlayerControl.LocalPlayer
                                && AmethystRpcGuard.RoleReady(actor) && !actor.Data.Role.CanVent)
                            {
                                block = true;
                                reason = AmethystText.T("VentForAnother");
                            }
                        }
                    }
                    else if (op == (int)VentilationSystem.Operation.BootImpostors)
                    {
                        
                        if (AmethystVentTpProtect.IsLegitVentClean(vid, actor, true))
                        {
                            
                        }
                        else if (AmethystRpcGuard.ShipGraceOver())
                        {
                            if (!AmethystRpcGuard.RealVent(vid)) { block = true; reason = AmethystText.T("FakeVentId"); }
                            else if (actor == null || actor.Data == null || actor.Data.IsDead
                                || !AmethystRpcGuard.IsImp(actor))
                            {
                                block = true;
                                reason = AmethystText.T("ForcedVentEject");
                            }
                            else if (actor != PlayerControl.LocalPlayer
                                && AmethystRpcGuard.RoleReady(actor) && !actor.Data.Role.CanVent)
                            {
                                block = true;
                                reason = AmethystText.T("VentForAnother");
                            }
                        }
                    }
                    if (block) victimForced = true; 
                }
                else if (AmethystRpcGuard.SabSystems.Contains(sys))
                {
                    int amount = reader.Position < reader.Length ? reader.ReadByte() : 0;
                    CheckSabotage(__instance, actor, sys, amount, ref block, ref reason);
                }
                else if (sys == (int)SystemTypes.Sabotage && reader.Position < reader.Length)
                {
                    
                    int target = reader.ReadByte();
                    if (AmethystRpcGuard.SabSystems.Contains(target))
                    {
                        int amount = reader.Position < reader.Length ? reader.ReadByte() : 0;
                        CheckSabotage(__instance, actor, target, amount, ref block, ref reason);
                    }
                }
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (block)
        {
            
            if (victimForced) { AmethystRpcGuard.NotifyVentForce(actor, reason); return true; }

            if (AmethystRpcGuard.InJoinGrace() && (callId == (byte)RpcCalls.CloseDoorsOfType || callId == (byte)RpcCalls.UpdateSystem))
                return !AmethystRpcGuard.ShouldDropRpc();

            if (actor != null) AmethystRpcGuard.Flag(actor, reason);
            else AmethystRpcGuard.NotifyOnce("ship-nobody", reason);
            return !AmethystRpcGuard.ShouldDropRpc();
        }
        return true;
    }

    private static void CheckSabotage(ShipStatus ship, PlayerControl actor, int sys, int amount, ref bool block, ref string reason)
    {
        if (AmethystRpcGuard.MeetingGuardArmed())
        {
            block = true;
            reason = AmethystText.T("SabotageInMeeting");
        }
        else if (ship.Systems != null && !ship.Systems.ContainsKey((SystemTypes)sys))
        {
            block = true;
            reason = AmethystText.T("SabotageOffMap");
        }
        else if (actor != null && actor != PlayerControl.LocalPlayer && actor.Data != null
            && !actor.Data.IsDead && !actor.Data.IsIncomplete
            && !AmethystRpcGuard.IsImp(actor)
            && (sys == (int)SystemTypes.MushroomMixupSabotage || (amount & 0x80) != 0))
        {
            block = true;
            reason = AmethystText.T("SabotageByCrew");
        }
        else if (actor != null && AmethystRpcGuard.CheckRapidSabotage(actor.PlayerId, sys, out var rapidReason))
        {
            block = true;
            reason = rapidReason;
        }
        else if ((amount & 0x80) != 0 && AmethystRpcGuard.IsMajorSabotageSystem(sys) && actor != null)
        {
            
            bool active = AmethystRpcGuard.MajorSabotageActive();
            if (active && AmethystRpcGuard.MajorSabotageByActor.TryGetValue(actor.PlayerId, out int prev) && prev != sys)
            {
                block = true;
                reason = AmethystText.T("MultiSabotage");
            }
            else if (!active)
            {
                AmethystRpcGuard.MajorSabotageByActor.Clear();
            }
            AmethystRpcGuard.MajorSabotageByActor[actor.PlayerId] = sys;
        }
    }
}
