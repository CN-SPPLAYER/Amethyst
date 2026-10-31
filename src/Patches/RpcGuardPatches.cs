using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

internal static class AmethystRpcGuard
{
    
    internal static bool CallAllowed(byte callId) => AmethystConfig.CallAllowed(callId);
    private static readonly Dictionary<int, float> Hit = new Dictionary<int, float>();
    private static readonly Dictionary<string, float> Notif = new Dictionary<string, float>();
    private static readonly Dictionary<byte, float> TaskWindowStart = new Dictionary<byte, float>();
    private static readonly Dictionary<byte, int> TaskWindowCount = new Dictionary<byte, int>();
    
    internal static readonly Dictionary<byte, bool> HasSetLevel = new Dictionary<byte, bool>();
    internal static readonly Dictionary<byte, bool> HasSetName = new Dictionary<byte, bool>();
    private static float _meetingOpenedAt = -1f;
    private static bool _dropRpcManuallyDisabled;
    private static float _joinGraceUntil;
    
    private static float _shipAt = -1f;

    private static readonly Dictionary<byte, (int SystemType, float Time)> LastSabotage = new Dictionary<byte, (int, float)>();
    
    internal static readonly Dictionary<byte, int> DeadKillCount = new Dictionary<byte, int>();

    internal struct KillMark
    {
        public float At;
        public byte TargetId;
    }

    internal static readonly Dictionary<byte, KillMark> LastKillAt = new Dictionary<byte, KillMark>();

    private const float HideNSeekWindow = 0.01f;

    private static float _lastWindow = -1f;

    internal static float FastKillWindow
    {
        get
        {
            float user = 8f; 

            bool classic = IsClassicMode();
            float w = classic ? user : HideNSeekWindow; 

            if (Mathf.Abs(w - _lastWindow) > 0.0001f)
            {
                _lastWindow = w;
                try { AmethystPlugin.Logger?.LogInfo($"[FastKill] 阈值={w:0.###}s（{(classic ? "经典" : "躲猫猫")}）"); }
                catch { }
            }
            return w;
        }
    }

    internal static void NoteKill(PlayerControl killer, PlayerControl target)
    {
        try
        {
            if (killer == null || target == null) return;
            
            if (target.Data == null || !target.Data.IsDead) return;
            LastKillAt[killer.PlayerId] = new KillMark { At = Time.unscaledTime, TargetId = target.PlayerId };
        }
        catch { }
    }

    internal static bool InFastKillWindow(byte pid, PlayerControl target)
    {
        try
        {
            if (AmethystConfig.FastKillGuard != null && !AmethystConfig.FastKillGuard.Value) return false;

            float win = FastKillWindow;
            if (win <= 0f) return false;

            float cd = RealKillCooldown();
            if (cd >= 0f && cd <= win) return false;
            if (!LastKillAt.TryGetValue(pid, out KillMark m)) return false;

            if (target == null) return false;
            if (m.TargetId == target.PlayerId) return false;

            return Time.unscaledTime - m.At < win;
        }
        catch { return false; }
    }

    internal static bool IsClassicMode()
    {
        try
        {
            return GameOptionsManager.Instance != null &&
                   GameOptionsManager.Instance.CurrentGameOptions != null &&
                   GameOptionsManager.Instance.CurrentGameOptions.GameMode is GameModes.Normal or GameModes.NormalFools;
        }
        catch { return false; }
    }

    internal static float RealKillCooldown()
    {
        try
        {
            if (GameOptionsManager.Instance == null || GameOptionsManager.Instance.CurrentGameOptions == null) return -1f;
            return GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
        }
        catch { return -1f; }
    }
    
    internal static readonly Dictionary<byte, int> MajorSabotageByActor = new Dictionary<byte, int>();

    private static readonly HashSet<byte> LobbyGameRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.MurderPlayer, (byte)RpcCalls.CheckMurder,
        (byte)RpcCalls.ReportDeadBody, (byte)RpcCalls.StartMeeting,
        (byte)RpcCalls.EnterVent, (byte)RpcCalls.ExitVent,
        (byte)RpcCalls.Shapeshift, (byte)RpcCalls.CheckShapeshift, (byte)RpcCalls.RejectShapeshift,
        (byte)RpcCalls.StartVanish, (byte)RpcCalls.StartAppear, (byte)RpcCalls.CheckVanish, (byte)RpcCalls.CheckAppear,
        (byte)RpcCalls.ProtectPlayer, (byte)RpcCalls.CheckProtect,
        (byte)RpcCalls.CompleteTask, (byte)RpcCalls.PlayAnimation
    };

    internal static bool IsLobbyGameRpc(byte callId) => LobbyGameRpcs.Contains(callId);

    internal static bool InLobby()
    {
        try
        {
            return AmongUsClient.Instance != null
                && !AmongUsClient.Instance.IsGameStarted
                && Alive(LobbyBehaviour.Instance);
        }
        catch { return false; }
    }

    internal static readonly HashSet<int> SabSystems = new HashSet<int>
    {
        (int)SystemTypes.Reactor, (int)SystemTypes.LifeSupp, (int)SystemTypes.Comms,
        (int)SystemTypes.HeliSabotage, (int)SystemTypes.Laboratory, (int)SystemTypes.MushroomMixupSabotage,
        (int)SystemTypes.Electrical
    };

    internal static bool On()
    {
        if (!AmethystConfig.RpcGuard.Value || AmongUsClient.Instance == null) return false;
        return true;
    }

    internal static bool AmHost()
    {
        try { return ((InnerNetClient)AmongUsClient.Instance)?.AmHost == true; }
        catch { return false; }
    }

    internal static bool ShouldDropRpc()
    {
        try
        {
            if (AmethystConfig.DropRPC == null) return false;
            return AmHost() && AmethystConfig.DropRPC.Value;
        }
        catch { return false; }
    }

    internal static void SetDropRpcEnabled(bool enabled, bool manualChange = false)
    {
        try
        {
            if (AmethystConfig.DropRPC != null) AmethystConfig.DropRPC.Value = enabled;
            if (manualChange) _dropRpcManuallyDisabled = !enabled;
            if (enabled) _dropRpcManuallyDisabled = false;
        }
        catch { }
    }

    internal static void SyncDropRpcForHost(bool host)
    {
        try
        {
            if (AmethystConfig.DropRPC == null) return;
            if (!host)
            {
                AmethystConfig.DropRPC.Value = false;
                return;
            }

            if (!_dropRpcManuallyDisabled)
                AmethystConfig.DropRPC.Value = true;
        }
        catch { }
    }

    internal static bool InMatch()
    {
        try
        {
            return AmongUsClient.Instance != null && AmongUsClient.Instance.IsGameStarted
                && ShipStatus.Instance != null
                && !Alive(LobbyBehaviour.Instance) && !Alive(IntroCutscene.Instance)
                && !MeetingActive() && !ExileActive();
        }
        catch { return false; }
    }

    private static bool Alive(UnityEngine.Object o)
    {
        try
        {
            if (o == null) return false;                 
            var c = o as UnityEngine.Component;
            return c != null && c.gameObject != null && c.gameObject.activeInHierarchy;
        }
        catch { return false; }
    }

    internal static bool MeetingActive() => Alive(MeetingHud.Instance);
    internal static bool ExileActive() => Alive(ExileController.Instance);
    internal static bool InMeetingOrExileActive() => MeetingActive() || ExileActive();

    internal static bool InGame()
    {
        try
        {
            return AmongUsClient.Instance != null && AmongUsClient.Instance.IsGameStarted
                && ShipStatus.Instance != null
                && !Alive(LobbyBehaviour.Instance) && !Alive(IntroCutscene.Instance);
        }
        catch { return false; }
    }

    internal static bool IsImp(PlayerControl pc)
    {
        try { return pc != null && pc.Data != null && pc.Data.Role != null && pc.Data.Role.IsImpostor; }
        catch { return false; }
    }

    internal static bool IsHS()
    {
        try { return GameManager.Instance != null && GameManager.Instance.IsHideAndSeek(); }
        catch { return false; }
    }

    internal static bool VentRulesEnabled()
    {
        try
        {
            var cfg = AmHost() ? AmethystConfig.VentHostRules : AmethystConfig.VentClientRules;
            return cfg == null || cfg.Value;
        }
        catch { return false; }
    }

    internal static bool ShipGraceOver()
    {
        try
        {
            ShipStatus s = ShipStatus.Instance;
            if (s == null) { _shipAt = -1f; return false; }
            if (_shipAt < 0f) _shipAt = Time.realtimeSinceStartup;
            return Time.realtimeSinceStartup - _shipAt >= 3f;
        }
        catch { return false; }
    }

    internal static bool RoleReady(PlayerControl pc)
    {
        try
        {
            return pc != null && pc.Data != null && !pc.Data.IsIncomplete && pc.Data.Role != null;
        }
        catch { return false; }
    }

    internal static bool IsOutfitCall(byte callId)
    {
        return callId == (byte)RpcCalls.CheckName || callId == (byte)RpcCalls.SetName
            || callId == (byte)RpcCalls.SetColor || callId == (byte)RpcCalls.SetHatStr
            || callId == (byte)RpcCalls.SetSkinStr || callId == (byte)RpcCalls.SetPetStr
            || callId == (byte)RpcCalls.SetVisorStr || callId == (byte)RpcCalls.SetNamePlateStr;
    }

    internal enum RpcObjectKind { Player, Physics, Ship, VoteBan, Meeting }

    internal static bool StructureGuardOn =>
        AmethystConfig.UnknownRpcGuard != null && AmethystConfig.UnknownRpcGuard.Value;

    private static readonly HashSet<byte> PlayerKeyRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.MurderPlayer, (byte)RpcCalls.CheckMurder, (byte)RpcCalls.ReportDeadBody,
        (byte)RpcCalls.StartMeeting, (byte)RpcCalls.SetName, (byte)RpcCalls.CheckName,
        (byte)RpcCalls.SetColor, (byte)RpcCalls.CheckColor, (byte)RpcCalls.SetLevel,
        (byte)RpcCalls.SetTasks, (byte)RpcCalls.SetRole, (byte)RpcCalls.SetInfected,
        (byte)RpcCalls.SyncSettings, (byte)RpcCalls.SetStartCounter, (byte)RpcCalls.UseZipline,
        (byte)RpcCalls.Shapeshift, (byte)RpcCalls.CheckShapeshift, (byte)RpcCalls.RejectShapeshift,
        (byte)RpcCalls.StartVanish, (byte)RpcCalls.StartAppear, (byte)RpcCalls.CheckVanish,
        (byte)RpcCalls.CheckAppear, (byte)RpcCalls.ProtectPlayer, (byte)RpcCalls.CheckProtect
    };

    private static readonly HashSet<byte> PhysicsKeyRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.EnterVent, (byte)RpcCalls.ExitVent, (byte)RpcCalls.ClimbLadder
    };

    private static readonly HashSet<byte> ShipKeyRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.UpdateSystem, (byte)RpcCalls.CloseDoorsOfType
    };

    private static readonly HashSet<byte> VoteKeyRpcs = new HashSet<byte> { (byte)RpcCalls.AddVote };

    private static readonly HashSet<byte> MeetingKeyRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.CastVote, (byte)RpcCalls.ClearVote, (byte)RpcCalls.VotingComplete
    };

    internal static bool IsWrongObject(RpcObjectKind kind, byte callId)
    {
        switch (kind)
        {
            case RpcObjectKind.Player:
                return PhysicsKeyRpcs.Contains(callId) || ShipKeyRpcs.Contains(callId)
                    || VoteKeyRpcs.Contains(callId) || MeetingKeyRpcs.Contains(callId);

            case RpcObjectKind.Physics:
                return PlayerKeyRpcs.Contains(callId) || ShipKeyRpcs.Contains(callId)
                    || VoteKeyRpcs.Contains(callId) || MeetingKeyRpcs.Contains(callId);

            case RpcObjectKind.Ship:
                return PlayerKeyRpcs.Contains(callId) || PhysicsKeyRpcs.Contains(callId)
                    || VoteKeyRpcs.Contains(callId) || MeetingKeyRpcs.Contains(callId);

            case RpcObjectKind.VoteBan:
                return PlayerKeyRpcs.Contains(callId) || PhysicsKeyRpcs.Contains(callId)
                    || ShipKeyRpcs.Contains(callId) || MeetingKeyRpcs.Contains(callId);

            case RpcObjectKind.Meeting:
                return PlayerKeyRpcs.Contains(callId) || PhysicsKeyRpcs.Contains(callId)
                    || ShipKeyRpcs.Contains(callId) || VoteKeyRpcs.Contains(callId);
        }
        return false;
    }

    internal static int CurrentMapId()
    {
        try
        {
            GameOptionsManager gom = GameOptionsManager.Instance;
            if (gom == null) return -1;
            var o = gom.CurrentGameOptions;
            return o != null ? o.MapId : -1;
        }
        catch { return -1; }
    }

    internal static bool InMeetingOrExile()
    {
        
        return InMeetingOrExileActive();
    }

    internal static bool MeetingGuardArmed()
    {
        try
        {
            if (!AmethystRpcGuard.MeetingActive()) return false;
            float openedAt = _meetingOpenedAt;
            if (openedAt < 0f) return false;
            return Time.unscaledTime - openedAt >= 2f;
        }
        catch { return false; }
    }

    internal static void MarkMeetingOpened()
    {
        _meetingOpenedAt = Time.unscaledTime;
    }

    internal static void ClearMeetingOpened()
    {
        _meetingOpenedAt = -1f;
    }

    internal static bool MajorSabotageActive()
    {
        try
        {
            ShipStatus ship = ShipStatus.Instance;
            if (ship == null || ship.Systems == null) return false;

            if (ship.Systems.TryGetValue(SystemTypes.Electrical, out var e) && e != null)
            { var s = e.TryCast<SwitchSystem>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.Reactor, out var r) && r != null)
            { var s = r.TryCast<ReactorSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.Laboratory, out var lab) && lab != null)
            { var s = lab.TryCast<ReactorSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.LifeSupp, out var ls) && ls != null)
            { var s = ls.TryCast<LifeSuppSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.Comms, out var c) && c != null)
            { var s = c.TryCast<HudOverrideSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.Comms, out var c2) && c2 != null)
            { var s = c2.TryCast<HqHudSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.MushroomMixupSabotage, out var m) && m != null)
            { var s = m.TryCast<MushroomMixupSabotageSystem>(); if (s != null && s.IsActive) return true; }

            return false;
        }
        catch { return false; }
    }

    internal static bool IsMajorSabotageSystem(int sys)
    {
        switch ((SystemTypes)sys)
        {
            case SystemTypes.Electrical:
            case SystemTypes.Reactor:
            case SystemTypes.Laboratory:
            case SystemTypes.LifeSupp:
            case SystemTypes.Comms:
            case SystemTypes.MushroomMixupSabotage:
                return true;
            default:
                return false;
        }
    }

    internal static bool CheckRapidSabotage(byte playerId, int systemType, out string reason)
    {
        reason = null;
        if (systemType == (int)SystemTypes.Doors) return false; 

        float now = Time.realtimeSinceStartup;
        if (LastSabotage.TryGetValue(playerId, out var last))
        {
            if (now - last.Time < 0.1f && last.SystemType != systemType)
            {
                reason = AmethystText.T("RapidSabotage");
                return true;
            }
        }
        LastSabotage[playerId] = (systemType, now);
        return false;
    }

    internal static void ClearRapidSabotage()
    {
        LastSabotage.Clear();
    }

    internal static void ResetRoundState()
    {
        HasSetLevel.Clear();
        HasSetName.Clear();
        TaskWindowStart.Clear();
        TaskWindowCount.Clear();
        LastSabotage.Clear();
        DeadKillCount.Clear();
        LastKillAt.Clear();   
        MajorSabotageByActor.Clear();
        
    }

    internal static void MarkJoinGrace()
    {
        _joinGraceUntil = Time.realtimeSinceStartup + 10f;
    }

    internal static bool InJoinGrace()
    {
        try { return Time.realtimeSinceStartup < _joinGraceUntil; }
        catch { return false; }
    }

    internal static bool RealVent(int id)
    {
        try
        {
            ShipStatus s = ShipStatus.Instance;
            if (s == null || s.AllVents == null) return true;
            foreach (Vent v in s.AllVents) if (v != null && v.Id == id) return true;
            return false;
        }
        catch { return true; }
    }

    internal static bool HasTask(PlayerControl pc, TaskTypes taskType)
    {
        try
        {
            if (pc == null) return true;                            
            NetworkedPlayerInfo data = pc.Data;
            if (data == null || data.Tasks == null) return true;    
            var tasks = data.Tasks;
            if (tasks.Count <= 0) return true;                      

            int target = (int)taskType;
            for (int i = 0; i < tasks.Count; i++)
            {
                
                if ((int)tasks[i].TypeId == target) return true;
            }
            return false;                                           
        }
        catch { return true; }   
    }

    internal static bool TrackTaskRate(byte playerId)
    {
        try
        {
            float now = Time.realtimeSinceStartup;
            if (!TaskWindowStart.TryGetValue(playerId, out float start) || now - start >= 2f)
            {
                TaskWindowStart[playerId] = now;
                TaskWindowCount[playerId] = 1;
                return false;
            }

            int count = TaskWindowCount.TryGetValue(playerId, out int existing) ? existing + 1 : 1;
            TaskWindowCount[playerId] = count;
            return count > 3;
        }
        catch { return false; }
    }

    internal static void Flag(PlayerControl actor, string reason, bool punish = true)
    {
        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null) return;
            int cid = actor != null ? actor.OwnerId : -1;
            string who = actor != null ? AmethystAccess.ClientName(net, cid) : AmethystText.T("Unknown");
            string whoShow = actor != null ? (AmethystColors.Name(actor) ?? who) : who;
            string msg = AmethystNotifyText.Body(whoShow, reason);
            if (cid < 0 || cid == net.ClientId) return;

            float now = Time.realtimeSinceStartup;
            if (Hit.TryGetValue(cid, out float t) && now - t < 6f) return;
            Hit[cid] = now;

            string playerId = cid.ToString();
            if (actor != null && actor.Data != null)
            {
                try { playerId = actor.PlayerId.ToString(); } catch { }
            }
            AmethystPlugin.LogCheat(who, playerId, reason);

            try
            {
                InnerNet.ClientData cd = AmethystAccess.FindClient(net, cid);
                AmethystHistory.Cheat(
                    who,
                    cd != null ? (cd.FriendCode ?? string.Empty) : string.Empty,
                    cd != null ? (cd.ProductUserId ?? string.Empty) : string.Empty,
                    AmethystAccess.PlatformOf(cd),
                    reason);
            }
            catch { }

            AmethystToast.Push(AmethystText.T("Guard"), msg, 5.0f, AmethystNotifyKind.Warning);
            AmethystEventLog.Add(who + " · " + msg, AmethystNotifyKind.Warning, AmethystEventCat.Other);

            if (!AmHost()) return;
            if (cid == net.HostId) return;
            if (punish)
                AmethystAccess.Act(net, cid, AmethystConfig.RpcGuardAction.Value, who, reason, true);
        }
        catch { }
    }

    internal static void NotifyOnce(string key, string reason, float cooldown = 4f)
    {
        try
        {
            if (Notif.TryGetValue(key, out float t) && Time.realtimeSinceStartup - t < cooldown) return;
            Notif[key] = Time.realtimeSinceStartup;
            AmethystToast.Push(AmethystText.T("Guard"), AmethystNotifyText.BodyAnonymous(reason), 5.0f, AmethystNotifyKind.Warning);
        }
        catch { }
    }

    internal static void NotifyVentForce(PlayerControl victim, string detail)
    {
        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null) return;
            int cid = victim != null ? victim.OwnerId : -1;

            float now = Time.realtimeSinceStartup;
            if (cid >= 0 && Hit.TryGetValue(cid, out float t) && now - t < 6f) return;
            if (cid >= 0) Hit[cid] = now;

            string name = victim != null
                ? AmethystAccess.ClientName(net, cid)
                : AmethystText.T("Unknown");
            string nameShow = victim != null ? (AmethystColors.Name(victim) ?? name) : name;
            string msg = AmethystText.T("VentTeleportForcedVentBlockedTarget", nameShow);
            string full = string.IsNullOrEmpty(detail) ? msg : $"{msg} · {detail}";
            AmethystToast.Push(AmethystText.T("Guard"), full, 5.0f, AmethystNotifyKind.Warning);
            AmethystEventLog.Add(name + " · " + full, AmethystNotifyKind.Warning, AmethystEventCat.Other);
            if (cid >= 0 && cid != net.ClientId && cid != net.HostId)
            {
                string playerId = (victim != null && victim.Data != null) ? victim.PlayerId.ToString() : cid.ToString();
                AmethystPlugin.LogCheat(name, playerId, string.IsNullOrEmpty(detail) ? "vent force" : detail);

                try
                {
                    InnerNet.ClientData cd = AmethystAccess.FindClient(net, cid);
                    AmethystHistory.Cheat(
                        name,
                        cd != null ? (cd.FriendCode ?? string.Empty) : string.Empty,
                        cd != null ? (cd.ProductUserId ?? string.Empty) : string.Empty,
                        AmethystAccess.PlatformOf(cd),
                        string.IsNullOrEmpty(detail) ? "vent force" : detail);
                }
                catch { }
            }
        }
        catch { }
    }
}









