using System;
using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal enum RecapDeath { None, Kill, Exile, Disconnect }

internal sealed class RecapEntry
{
    internal byte PlayerId;
    internal string Name = string.Empty;
    internal int ColorId;
    internal bool Dead;
    internal bool Disconnected;
    internal RecapDeath Death;
    internal byte KillerId = byte.MaxValue;

    internal byte JudgedId = byte.MaxValue;

    internal string RoleAliveText = string.Empty;
    internal string RoleDeadText = string.Empty;
    internal bool Impostor;

    internal string ProgressText = string.Empty;
}

internal static partial class AmethystRecap
{
    private static readonly List<RecapEntry> _entries = new List<RecapEntry>();
    private static readonly Dictionary<byte, RecapEntry> _byId = new Dictionary<byte, RecapEntry>();

    private static Dictionary<RoleTypes, RoleBehaviour> _roleLookup;

    internal static string ResultFaction = string.Empty;
    internal static string ResultReason = string.Empty;
    internal static bool Frozen;

    internal static bool HasData => _entries.Count > 0;

    internal static bool FeatureOn => AmethystConfig.RecapInfo != null && AmethystConfig.RecapInfo.Value;

    internal static EndGameManager EndScreen;
    internal static bool CanUse
    {
        get
        {
            try
            {
                if (!FeatureOn) return false;
                return LobbyBehaviour.Instance != null || EndScreen != null;
            }
            catch { return false; }
        }
    }

    internal static void Reset()
    {
        _entries.Clear();
        _byId.Clear();
        _roleLookup = null;
        ResultFaction = string.Empty;
        ResultReason = string.Empty;
        Frozen = false;
        EndScreen = null;
        AmethystRecapWindow.Hide();
    }

    private static RoleBehaviour LookupRole(RoleTypes type)
    {
        try
        {
            Dictionary<RoleTypes, RoleBehaviour> dict = _roleLookup;
            if (dict == null || dict.Count == 0)
            {
                dict = new Dictionary<RoleTypes, RoleBehaviour>();
                var all = RoleManager.Instance != null ? RoleManager.Instance.AllRoles : null;
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        RoleBehaviour r = all[i];
                        if (r != null) dict[r.Role] = r;
                    }
                }

                if (dict.Count == 0) return null;   
                _roleLookup = dict;
            }

            return dict.TryGetValue(type, out RoleBehaviour b) ? b : null;
        }
        catch { return null; }
    }

    private static RecapEntry Get(byte id)
    {
        if (_byId.TryGetValue(id, out RecapEntry e)) return e;
        e = new RecapEntry { PlayerId = id };
        _byId[id] = e;
        _entries.Add(e);
        return e;
    }

    internal static void NoteKill(byte killerId, byte victimId)
    {
        if (victimId == killerId) return;
        try
        {
            RecapEntry v = Get(victimId);
            v.Dead = true;
            if (v.Death == RecapDeath.None)
            {
                v.Death = RecapDeath.Kill;
                v.KillerId = killerId;
            }
            
        }
        catch { }
    }

    internal static void NoteExile(byte playerId)
    {
        try
        {
            RecapEntry e = Get(playerId);
            e.Dead = true;
            if (e.Death == RecapDeath.None) e.Death = RecapDeath.Exile;
        }
        catch { }
    }

    internal static void NoteDisconnect(NetworkedPlayerInfo data)
    {
        try
        {
            if (data == null) return;

            RecapEntry e = Get(data.PlayerId);
            if (e == null) return;

            if (!string.IsNullOrEmpty(data.PlayerName)) e.Name = data.PlayerName;
            try { if (data.DefaultOutfit != null) e.ColorId = data.DefaultOutfit.ColorId; } catch { }

            bool wasDead = false;
            try { wasDead = data.IsDead; } catch { }
            SnapshotRole(e, data, wasDead);

            e.Disconnected = true;
            e.Dead = true;
            if (e.Death == RecapDeath.None) e.Death = RecapDeath.Disconnect;
        }
        catch { }
    }

    internal static void NoteJudge(byte judgeId, byte targetId)
    {
        try
        {
            if (judgeId == targetId) return;
            RecapEntry judge = Get(judgeId);
            judge.JudgedId = targetId;
        }
        catch { }
    }

    internal static void Sync()
    {
        if (Frozen) return;
        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl pc = all[i];
                if (pc == null || pc.isDummy) continue;
                NetworkedPlayerInfo data = pc.Data;
                if (data == null) continue;

                RecapEntry e = Get(pc.PlayerId);
                if (e == null) continue;

                try { if (!string.IsNullOrEmpty(data.PlayerName)) e.Name = data.PlayerName; } catch { }
                try { if (data.DefaultOutfit != null) e.ColorId = data.DefaultOutfit.ColorId; } catch { }

                bool dead = false;
                try { dead = data.IsDead; } catch { }
                if (dead) e.Dead = true;

                try
                {
                    if (data.Disconnected)
                    {
                        e.Disconnected = true;
                        e.Dead = true;
                        if (e.Death == RecapDeath.None) e.Death = RecapDeath.Disconnect;
                    }
                }
                catch { }

                SnapshotRole(e, data, dead);
            }
        }
        catch { }
    }

    private static readonly Dictionary<RoleTypes, bool> GhostCache = new Dictionary<RoleTypes, bool>();

    private static bool IsGhostRole(RoleTypes type)
    {
        if (GhostCache.TryGetValue(type, out bool cached)) return cached;
        bool ghost;
        try { ghost = RoleManager.IsGhostRole(type); }
        catch { ghost = type == RoleTypes.CrewmateGhost || type == RoleTypes.ImpostorGhost || type == RoleTypes.GuardianAngel; }
        GhostCache[type] = ghost;
        return ghost;
    }

    private static void SnapshotRole(RecapEntry e, NetworkedPlayerInfo data, bool dead)
    {
        try
        {
            RoleTypes type = data.RoleType;

            RoleBehaviour beh = LookupRole(type);
            string label = Patches.AmethystDeadRoleReveal.GetRoleLabel(type, beh != null ? beh.NiceName : null);
            if (string.IsNullOrEmpty(label)) label = type.ToString();

            Color team = beh != null ? beh.TeamColor : Color.white;
            string colored = $"<color=#{ColorUtility.ToHtmlStringRGB(team)}>{label}</color>";

            bool imp = beh != null && beh.IsImpostor;

            bool ghost = IsGhostRole(type);

            if (!ghost)
            {
                e.RoleAliveText = colored;   
                if (imp) e.Impostor = true;
            }
            else
            {
                e.RoleDeadText = colored;    
                if (string.IsNullOrEmpty(e.RoleAliveText) && imp) e.Impostor = true;
            }

            if (imp || e.Impostor)
            {
                int kills = Patches.AmethystDeadRoleReveal.GetKills(e.PlayerId);
                e.ProgressText = $"<color=#FFD24A>({AmethystText.T("Kills", kills)})</color>";
            }
            else
            {
                var tasks = data.Tasks;
                if (tasks != null && tasks.Count > 0)
                {
                    int done = 0;
                    for (int i = 0; i < tasks.Count; i++)
                    {
                        var t = tasks[i];
                        if (t != null && t.Complete) done++;
                    }
                    string col = done >= tasks.Count ? "#7BD88F" : "#E8C86A";
                    e.ProgressText = $"<color={col}>({done}/{tasks.Count})</color>";
                }
            }
        }
        catch { }
    }

    internal static void CaptureResult()
    {
        try
        {
            if (!string.IsNullOrEmpty(ResultFaction)) return;

            GameOverReason reason = EndGameResult.CachedGameOverReason;
            GameManager gm = GameManager.Instance;
            if (gm == null) return;   

            bool humansWin = gm.DidHumansWin(reason);
            ResultFaction = AmethystText.T(humansWin ? "RecapCrewWin" : "RecapImpWin");

            string rk = ReasonKey(reason);
            if (!string.IsNullOrEmpty(rk)) ResultReason = AmethystText.T(rk);
        }
        catch { }
    }

    internal static bool TryCaptureEndGame()
    {
        bool atEndGame = false;

        try
        {
            EndGameManager egm = UnityEngine.Object.FindObjectOfType<EndGameManager>();
            if (egm != null) atEndGame = true;
        }
        catch { }

        if (atEndGame) Frozen = true;
        return atEndGame;
    }



    internal static void Freeze() => Frozen = true;








}
