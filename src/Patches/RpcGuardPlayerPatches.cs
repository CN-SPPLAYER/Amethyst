using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;


[HarmonyPatch(typeof(PlayerControl), "MurderPlayer")]
internal static class AmethystRecordKillPatch
{
    private static void Postfix(PlayerControl __instance, PlayerControl target)
    {
        try { AmethystRpcGuard.NoteKill(__instance, target); }
        catch { }
    }
}


[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class AmethystRpcGuardPlayer
{
    private static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (AmethystConfig.RpcGuardPlayer == null || !AmethystConfig.RpcGuardPlayer.Value) return true;   
        if (!AmethystRpcGuard.CallAllowed(callId)) return true;   
        if (!AmethystRpcGuard.On() || __instance == null || __instance == PlayerControl.LocalPlayer || __instance.Data == null || reader == null) return true;

        if (AmethystRpcGuard.StructureGuardOn && AmongUsClient.Instance != null
            && __instance.OwnerId != AmongUsClient.Instance.HostId)
        {
            if (AmethystRpcGuard.IsWrongObject(AmethystRpcGuard.RpcObjectKind.Player, callId))
            {
                AmethystRpcGuard.Flag(__instance, AmethystText.T("RpcWrongObject"));
                return false;
            }
            
        }

        if (AmethystEarlyMeeting.ShouldBlock("rpc-prefix") &&
            (callId == (byte)RpcCalls.StartMeeting || callId == (byte)RpcCalls.ReportDeadBody))
        {
            AmethystEarlyMeeting.NotifyAttempt(__instance);
            return false;
        }

        if (AmethystRpcGuard.AmHost() && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay
            && (callId == (byte)RpcCalls.SetRole || callId == (byte)RpcCalls.SetInfected))
        {
            AmethystRpcGuard.NotifyOnce("host-authority-rpc", AmethystText.T("HostOnlyRPCByClient"));
            return false;
        }

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        bool drop = true;     
        bool punish = true;   
        bool hardDrop = false; 
        try
        {
            if (AmongUsClient.Instance == null || AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
            {
                return true;
            }
            
            if (AmethystRpcGuard.InLobby() && AmethystRpcGuard.IsLobbyGameRpc(callId))
            {
                cheat = true;
                hardDrop = true;
                reason = AmethystText.T("GameRPCInLobby");
            }
            if (callId == (byte)RpcCalls.SetScanner)
            {
                bool scanning = reader.ReadBoolean();
                if (scanning && ShipStatus.Instance == null)
                {
                    cheat = true;
                    reason = AmethystText.T("ScanWithoutMap");
                }
                else if (scanning && AmethystRpcGuard.IsImp(__instance))
                {
                    cheat = true;
                    reason = AmethystText.T("ScanByImpostor");
                }
                else if (scanning && !__instance.Data.IsDead && !__instance.Data.IsIncomplete
                    && AmethystRpcGuard.InGame() && AmethystRpcGuard.ShipGraceOver()
                    && !AmethystRpcGuard.HasTask(__instance, TaskTypes.SubmitScan))
                {
                    cheat = true;
                    reason = AmethystText.T("ScanWithoutTask");
                }
            }
            else if (callId == (byte)RpcCalls.Exiled)
            {
                
                cheat = true;
                reason = AmethystText.T("UnusedExiledRPC");
            }
            else if (callId == (byte)RpcCalls.ReportDeadBody)
            {
                if (AmethystRpcGuard.MeetingGuardArmed())
                {
                    cheat = true;
                    reason = AmethystText.T("ReportDuringMeeting");
                }
                else if (__instance.Data.IsDead)
                {
                    cheat = true;
                    reason = AmethystText.T("ReportWhileDead");
                }
                else if (__instance.inVent || __instance.shapeshifting
                    || __instance.onLadder || __instance.inMovingPlat
                    || (__instance.MyPhysics != null && __instance.MyPhysics.Animations != null
                        && __instance.MyPhysics.Animations.IsPlayingAnyLadderAnimation()))
                {
                    cheat = true;
                    reason = AmethystText.T("ReportWhileBusy");
                }
                else
                {
                    int rp = reader.Position;
                    NetworkedPlayerInfo target = null;
                    bool parsed = false;
                    try { target = reader.ReadNetObject<NetworkedPlayerInfo>(); parsed = true; }
                    catch { }
                    finally { try { reader.Position = rp; } catch { } }

                    if (parsed)
                    {
                        if (target != null)
                        {
                            if (!target.IsDead)
                            {
                                cheat = true;
                                reason = AmethystText.T("ReportNotDead");
                            }
                            else if (__instance.Data != null && target.PlayerId == __instance.Data.PlayerId)
                            {
                                cheat = true;
                                reason = AmethystText.T("ReportSelf");
                            }
                        }
                        else if (__instance.RemainingEmergencies <= 0)
                        {
                            cheat = true;
                            reason = AmethystText.T("NoEmergencies");
                        }
                    }
                }
            }
            else if (callId == (byte)RpcCalls.MurderPlayer || callId == (byte)RpcCalls.CheckMurder)
            {
                PlayerControl target = null;
                int mpos = reader.Position;
                try { target = reader.ReadNetObject<PlayerControl>(); }
                catch { }
                finally { try { reader.Position = mpos; } catch { } }

                if (AmethystRpcGuard.InGame() && AmethystRpcGuard.RoleReady(__instance))
                {
                    byte pid = __instance.PlayerId;
                    bool actorDead = __instance.Data != null && __instance.Data.IsDead;
                    bool targetDead = target != null && target.Data != null && target.Data.IsDead;
                    bool targetIsImp = target != null && AmethystRpcGuard.IsImp(target);

                    if (actorDead)
                    {
                        cheat = true;
                        reason = AmethystText.T("DeadMurder");
                    }
                    else if (!AmethystRpcGuard.IsImp(__instance))
                    {
                        cheat = true;
                        reason = AmethystText.T("KillByCrewmate");
                    }
                    else if (targetIsImp && target != __instance)
                    {
                        cheat = true;
                        reason = AmethystText.T("KillImpostor");
                    }
                    
                    else if (AmethystRpcGuard.InFastKillWindow(pid, target))
                    {
                        cheat = true;
                        reason = AmethystText.T("FastKill");
                        hardDrop = true;
                    }
                    else if (targetDead)
                    {
                        int count = AmethystRpcGuard.DeadKillCount.TryGetValue(pid, out int c) ? c + 1 : 1;
                        AmethystRpcGuard.DeadKillCount[pid] = count;
                        if (count >= 6)
                        {
                            cheat = true;
                            reason = AmethystText.T("RepeatKillDead");
                        }
                    }
                    else
                    {
                        AmethystRpcGuard.DeadKillCount[pid] = 0;
                    }
                }
            }
            else if (callId == (byte)RpcCalls.Shapeshift || callId == (byte)RpcCalls.CheckShapeshift || callId == (byte)RpcCalls.RejectShapeshift)
            {
                if (AmethystRpcGuard.InGame() && __instance.Data.Role != null && __instance.Data.Role.Role != RoleTypes.Shapeshifter)
                {
                    cheat = true;
                    reason = AmethystText.T("ShapeshiftWithoutRole");
                }
            }
            else if (callId == (byte)RpcCalls.StartVanish || callId == (byte)RpcCalls.StartAppear || callId == (byte)RpcCalls.CheckVanish || callId == (byte)RpcCalls.CheckAppear)
            {
                if (AmethystRpcGuard.InGame() && __instance.Data.Role != null && __instance.Data.Role.Role != RoleTypes.Phantom)
                {
                    cheat = true;
                    reason = AmethystText.T("VanishAppearWithoutRole");
                }
            }
            else if (callId == (byte)RpcCalls.ProtectPlayer || callId == (byte)RpcCalls.CheckProtect)
            {
                int start = reader.Position;
                try
                {
                    PlayerControl target = reader.ReadNetObject<PlayerControl>();
                    if (target == null)
                    {
                        cheat = true;
                        reason = AmethystText.T("ProtectOverloading");
                    }
                }
                catch
                {
                    cheat = true;
                    reason = AmethystText.T("ProtectOverloading");
                }
                finally { try { reader.Position = start; } catch { } }
            }
            else if (callId == (byte)RpcCalls.SendChatNote)
            {
                if (AmethystRpcGuard.InLobby() || AmethystRpcGuard.InGame())
                {
                    cheat = true;
                    reason = AmethystText.T("ChatNoteOutsideMeeting");
                }
            }
            else if (callId == (byte)RpcCalls.SetTasks)
            {
                drop = false; punish = false; 
                
                drop = false; punish = false;
                if (AmethystRpcGuard.InGame() && AmethystRpcGuard.ShipGraceOver())
                {
                    cheat = true;
                    reason = AmethystText.T("SetTasksInMatch");
                }
            }
            else if ((callId == (byte)RpcCalls.StartMeeting || callId == (byte)RpcCalls.ReportDeadBody)
                && AmethystEarlyMeeting.ShouldBlock("rpc"))
            {
                if (!AmethystRpcGuard.MeetingGuardArmed())
                {
                    cheat = true;
                    reason = AmethystText.T("MeetingBeforeLoad");
                    AmethystEarlyMeeting.NotifyAttempt(__instance);
                }
            }
            else if (callId == (byte)RpcCalls.CompleteTask)
            {
                drop = false; punish = false; 
                uint taskIndex = reader.ReadPackedUInt32();
                if (AmethystRpcGuard.TrackTaskRate(__instance.PlayerId))
                {
                    cheat = true;
                    reason = AmethystText.T("TaskCompletionFlood");
                }
                if (ShipStatus.Instance == null)
                {
                    cheat = true;
                    reason = AmethystText.T("TaskWithoutMap");
                }
                else if (AmethystRpcGuard.IsImp(__instance))
                {
                    cheat = true;
                    reason = AmethystText.T("TaskByImpostor");
                }
                else if (__instance.Data.Tasks == null || taskIndex + 1 > __instance.Data.Tasks.Count)
                {
                    cheat = true;
                    reason = AmethystText.T("ExtraTask");
                }
            }
            else if (callId == (byte)RpcCalls.SetLevel)
            {
                
                if (!AmethystRpcGuard.InJoinGrace()
                    && AmethystRpcGuard.InGame()
                    && AmethystRpcGuard.ShipGraceOver())
                {
                    cheat = true;
                    reason = AmethystText.T("SetLevelInMatch");
                }
            }
            else if (callId == (byte)RpcCalls.SetStartCounter)
            {
                int sp = reader.Position;
                int counter = -1;
                bool parsed = false;
                try { counter = reader.ReadPackedInt32(); parsed = true; }
                catch
                {
                    try { reader.Position = sp; counter = reader.ReadInt32(); parsed = true; } catch { }
                }
                finally { try { reader.Position = sp; } catch { } }

                if (parsed && AmongUsClient.Instance != null
                    && __instance.OwnerId != AmongUsClient.Instance.HostId
                    && counter != -1)
                {
                    cheat = true;
                    reason = AmethystText.T("InvalidStartCounter");
                }
            }
            else if (callId == (byte)RpcCalls.SyncSettings)
            {
                if (AmongUsClient.Instance != null && __instance.OwnerId != AmongUsClient.Instance.HostId)
                {
                    cheat = true;
                    reason = AmethystText.T("HostOnlyRPCByClient");
                }
            }
            else if (callId == (byte)RpcCalls.UsePlatform)
            {
                if (ShipStatus.Instance == null
                    || AmethystRpcGuard.CurrentMapId() != 4
                    || AmethystRpcGuard.IsHS()
                    || __instance.Data.IsDead)
                {
                    cheat = true;
                    reason = AmethystText.T("PlatformInvalid");
                }
            }
            else if (callId == (byte)RpcCalls.TriggerSpores || callId == (byte)RpcCalls.CheckSpore)
            {
                if (ShipStatus.Instance == null || AmethystRpcGuard.CurrentMapId() != 5)
                {
                    cheat = true;
                    reason = AmethystText.T("SporeWrongMap");
                }
            }
            else if (callId == (byte)RpcCalls.UseZipline)
            {
                if (ShipStatus.Instance == null
                    || AmethystRpcGuard.MeetingGuardArmed()
                    || __instance.Data.IsDead)
                {
                    cheat = true;
                    reason = AmethystText.T("ZiplineInvalid");
                }
            }
            else if (callId == (byte)RpcCalls.SetName || callId == (byte)RpcCalls.CheckName)
            {
                
                if (!AmethystRpcGuard.InJoinGrace()
                    && AmethystRpcGuard.InGame()
                    && AmethystRpcGuard.ShipGraceOver())
                {
                    cheat = true;
                    reason = AmethystText.T("SetNameInMatch");
                }
                int namePos = reader.Position;
                try
                {
                    if (callId == (byte)RpcCalls.SetName) reader.ReadUInt32();
                    string name = reader.ReadString();
                    if (!string.IsNullOrEmpty(name) && name.IndexOf('<') >= 0)
                    {
                        cheat = true;
                        reason = AmethystText.T("HTMLInName");
                    }
                }
                catch { }
                finally { try { reader.Position = namePos; } catch { } }
            }
            else if (callId == (byte)RpcCalls.ExtendLobbyTimer || callId == (byte)RpcCalls.CloseMeeting)
            {
                
                if (AmethystRpcGuard.AmHost() && __instance.OwnerId != AmongUsClient.Instance.HostId)
                {
                    cheat = true;
                    reason = AmethystText.T("HostOnlyRPCByClient");
                }
            }
            else if (AmethystRpcGuard.IsOutfitCall(callId) && AmethystRpcGuard.InGame() && !AmethystRpcGuard.InJoinGrace())
            {
                cheat = true;
                reason = AmethystText.T("OutfitChangeInMatch");
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat)
        {
            if (AmethystRpcGuard.InJoinGrace() && (callId == (byte)RpcCalls.Shapeshift || callId == (byte)RpcCalls.CheckShapeshift || callId == (byte)RpcCalls.RejectShapeshift || callId == (byte)RpcCalls.StartVanish || callId == (byte)RpcCalls.StartAppear || callId == (byte)RpcCalls.CheckVanish || callId == (byte)RpcCalls.CheckAppear || AmethystRpcGuard.IsOutfitCall(callId)))
                return !AmethystRpcGuard.ShouldDropRpc();

            AmethystRpcGuard.Flag(__instance, reason, punish);
            
            if (hardDrop) return false;
            
            return drop ? !AmethystRpcGuard.ShouldDropRpc() : true;
        }
        return true;
    }
}


[HarmonyPatch(typeof(NetworkedPlayerInfo), "HandleRpc")]
internal static class AmethystRpcGuardPlayerInfo
{
    private static bool Prefix([HarmonyArgument(0)] byte callId)
    {
        if (callId != (byte)RpcCalls.SetTasks) return true;
        if (!AmethystRpcGuard.CallAllowed(callId)) return true;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;
        AmethystRpcGuard.NotifyOnce("set-tasks-forged", AmethystText.T("HostOnlyRPCByClient"));
        return false;
    }
}
