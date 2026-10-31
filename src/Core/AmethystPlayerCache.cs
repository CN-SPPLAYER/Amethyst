using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using TMPro;
using UnityEngine;

namespace Amethyst;



internal static class AmethystPlayerCacheStore
{
    
    internal const int Cadence = 20;

    private static readonly Dictionary<byte, AmethystPlayerCache> Map =
        new Dictionary<byte, AmethystPlayerCache>(24);
    private static readonly List<AmethystPlayerCache> List =
        new List<AmethystPlayerCache>(24);
    private static readonly HashSet<byte> Live = new HashSet<byte>();
    private static readonly List<byte> Gone = new List<byte>(8);

    private static int _frame;
    private static int _nextResync;

    internal static bool InGame;
    internal static bool InLobby;
    internal static bool LocalDead;
    internal static bool LocalIsImp;

    internal static bool LocalIsCrewmateGhost;
    internal static byte LocalId = 255;

    internal static bool NameColorsOn;
    internal static bool ColorBlindOn;
    internal static bool ShowSelfRole;
    internal static bool ShowImpTeammateRole;
    internal static bool ShowRolesWhenDead;

    internal static bool AliveTagsAllowed
        => InGame && !LocalDead && (ShowSelfRole || ShowImpTeammateRole);

    internal static bool DeadTagsAllowed
        => InGame && LocalDead && LocalIsCrewmateGhost && ShowRolesWhenDead;

    internal static List<AmethystPlayerCache> All => List;

    internal static bool TryGet(byte id, out AmethystPlayerCache cache)
        => Map.TryGetValue(id, out cache);

    internal static void RefreshIfDirty(AmethystPlayerCache c)
    {
        try
        {
            if (c == null || !c.Dirty) return;

            if (!HasNameText(c))
            {
                if (++c.DirtyAttempts >= MaxDirtyAttempts) c.Dirty = false;
                return;
            }

            c.Dirty = false;
            c.DirtyAttempts = 0;
            Refresh(c);
        }
        catch { c.Dirty = false; }
    }

    private const int MaxDirtyAttempts = 60;

    private const int MaxDataRetryFrames = 90;

    private static bool HasNameText(AmethystPlayerCache c)
    {
        try
        {
            PlayerControl pc = c.Pc;
            if (pc == null) return false;
            CosmeticsLayer cos = pc.cosmetics;
            return cos != null && cos.nameText != null;
        }
        catch { return false; }
    }

    internal static void MarkDirty(byte id)
    {
        try
        {
            if (Map.TryGetValue(id, out AmethystPlayerCache c) && c != null) c.Dirty = true;
        }
        catch { }
    }

    internal static void ForceResync()
    {
        try { _nextResync = 0; } catch { }
    }

    internal static void MarkAllDirty()
    {
        try
        {
            if (_dirtyAllFrame == Time.frameCount) return;
            _dirtyAllFrame = Time.frameCount;

            for (int i = 0; i < List.Count; i++)
            {
                AmethystPlayerCache c = List[i];
                if (c != null) c.Dirty = true;
            }
        }
        catch { }
    }

    private static int _dirtyAllFrame = -1;

    internal static void OnPlayerAwake(PlayerControl pc)
    {
        try
        {
            if (pc == null) return;
            byte id = pc.PlayerId;

            AmethystPlayerCache c;
            if (!Map.TryGetValue(id, out c) || c == null)
            {
                c = new AmethystPlayerCache { Pc = pc, Id = id, Dirty = true };
                Map[id] = c;
            }
            else
            {
                c.Pc = pc;
                c.Dirty = true;
            }

            if (!Live.Contains(id)) Live.Add(id);
            bool inList = false;
            for (int i = 0; i < List.Count; i++)
                if (List[i] == c) { inList = true; break; }
            if (!inList) List.Add(c);
        }
        catch { }
    }

    internal static void Reset()
    {
        Map.Clear();
        List.Clear();
        Live.Clear();
        Gone.Clear();
        _nextResync = 0;
    }

    internal static void Tick()
    {
        _frame++;
        int probe = AmethystPerfProbe.Begin(Section.S_Cache);
        try { ReadGlobals(); } catch { }
        try { SyncPlayers(); } catch { }
        try { RefreshStaggered(); } catch { }
        AmethystPerfProbe.End(probe, Section.S_Cache);
    }

    private static void ReadGlobals()
    {
        InGame = false;
        LocalDead = false;
        LocalIsImp = false;
        LocalIsCrewmateGhost = false;
        LocalId = 255;

        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            bool started = net != null && net.IsGameStarted;
            bool inLobbyScene = LobbyBehaviour.Instance != null;
            InLobby = net != null && !started;
            InGame = started && ShipStatus.Instance != null && !inLobbyScene;
        }
        catch { InGame = false; InLobby = false; }

        try
        {
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local != null && local.Data != null)
            {
                LocalId = local.PlayerId;
                LocalDead = local.Data.IsDead;
                RoleBehaviour r = local.Data.Role;
                if (r != null)
                {
                    LocalIsImp = r.IsImpostor;
                    LocalIsCrewmateGhost = r.Role == RoleTypes.CrewmateGhost;
                }
            }
        }
        catch { }

        try { NameColorsOn = AmethystConfig.NameColors != null && AmethystConfig.NameColors.Value; } catch { }
        try { ColorBlindOn = AmethystConfig.ColorBlindText != null && AmethystConfig.ColorBlindText.Value; } catch { }
        try { ShowSelfRole = AmethystConfig.ShowSelfRole != null && AmethystConfig.ShowSelfRole.Value; } catch { }
        try { ShowImpTeammateRole = AmethystConfig.ShowImpostorTeammateRole != null && AmethystConfig.ShowImpostorTeammateRole.Value; } catch { }
        try { ShowRolesWhenDead = AmethystConfig.ShowRolesWhenDead != null && AmethystConfig.ShowRolesWhenDead.Value; } catch { }
    }

    private static void SyncPlayers()
    {
        if (_frame < _nextResync) return;
        _nextResync = _frame + Cadence;

        var all = PlayerControl.AllPlayerControls;
        if (all == null)
        {
            if (List.Count > 0) Reset();
            return;
        }

        int n = all.Count;
        List.Clear();
        Live.Clear();

        for (int i = 0; i < n; i++)
        {
            PlayerControl pc;
            try { pc = all[i]; } catch { pc = null; }
            if (pc == null) continue;

            byte id;
            try { id = pc.PlayerId; } catch { continue; }

            if (!Map.TryGetValue(id, out AmethystPlayerCache c) || c == null)
            {
                c = new AmethystPlayerCache { Dirty = true };   
                Map[id] = c;
            }
            c.Pc = pc;
            c.Id = id;
            List.Add(c);
            Live.Add(id);
        }

        if (Map.Count > Live.Count)
        {
            Gone.Clear();
            foreach (var kv in Map)
                if (!Live.Contains(kv.Key)) Gone.Add(kv.Key);
            for (int i = 0; i < Gone.Count; i++) Map.Remove(Gone[i]);
        }
    }

    private static void RefreshStaggered()
    {
        for (int i = 0; i < List.Count; i++)
        {
            AmethystPlayerCache c = List[i];
            if (c == null || c.Pc == null) continue;

            if (c.Dirty) { c.Dirty = false; Refresh(c); continue; }

            int cadence = Patches.AmethystPerformance.CadenceFor(c.Dead);
            if ((_frame + c.Id) % cadence != 0) continue;
            Refresh(c);
        }
    }

    private static void Refresh(AmethystPlayerCache c)
    {
        PlayerControl pc = c.Pc;
        if (pc == null) { c.NameText = null; c.ColorBlindText = null; return; }

        try
        {
            CosmeticsLayer cos = pc.cosmetics;
            if (cos != null)
            {
                c.NameText = cos.nameText;
                c.ColorBlindText = cos.colorBlindText;
            }
            else
            {
                c.NameText = null;
                c.ColorBlindText = null;
            }
        }
        catch { }

        NetworkedPlayerInfo data = null;
        try { data = pc.Data; } catch { }

        if (data == null)
        {
            
            c.DataMissingAttempts++;
            if (c.DataMissingAttempts < MaxDataRetryFrames) c.Dirty = true;
            return;
        }
        c.DataMissingAttempts = 0;

        try { c.Name = data.PlayerName ?? string.Empty; } catch { c.Name = string.Empty; }

        int defColor = -1;
        try { if (data.DefaultOutfit != null) defColor = data.DefaultOutfit.ColorId; } catch { }

        if (defColor < 0)
        {
            try
            {
                CosmeticsLayer cos2 = pc.cosmetics;
                if (cos2 != null)
                {
                    
                    int c2 = cos2.bodyMatProperties.ColorId;
                    if (c2 >= 0) defColor = c2;
                }
            }
            catch { }
        }
        c.ColorId = defColor;

        try { c.Dead = data.IsDead; } catch { c.Dead = false; }
        try { c.InVent = pc.inVent; } catch { c.InVent = false; }

        RoleBehaviour role = null;
        try { role = data.Role; } catch { }
        bool imp = false;
        try { if (role != null) imp = role.IsImpostor; } catch { }
        c.IsImpostor = imp;

        c.KillCount = Amethyst.Patches.AmethystDeadRoleReveal.GetKills(c.Id);

        c.TasksDone = 0;
        c.TasksTotal = 0;
        if (!imp)
        {
            try
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
                    c.TasksDone = done;
                    c.TasksTotal = tasks.Count;
                }
            }
            catch { }
        }

        bool shifting = false;
        int target = -1;
        try
        {
            target = pc.shapeshiftTargetPlayerId;
            if (target >= 0 && target != 255) shifting = true;
        }
        catch { }

        bool outfitDefault = true;
        try { outfitDefault = pc.CurrentOutfitType == PlayerOutfitType.Default; } catch { }
        c.OutfitIsDefault = outfitDefault;

        if (!shifting)
        {
            try
            {
                int curId = pc.CurrentOutfit != null ? pc.CurrentOutfit.ColorId : defColor;
                if (defColor >= 0 && curId != defColor) shifting = true;
            }
            catch { }
        }
        c.IsShapeshifting = shifting;

        int shown = defColor;
        if (target >= 0 && target != 255)
        {
            if (TryGet((byte)target, out AmethystPlayerCache tc) && tc != null && tc.ColorId >= 0)
                shown = tc.ColorId;
        }
        c.ShownColorId = shown >= 0 ? shown : defColor;

        try { c.LineExtra = Amethyst.Patches.AmethystDeadRoleReveal.NameLineExtra(c.NameText); } catch { }

        try { ComputeNameWrite(c); } catch { }
        try { ComputeColorBlindWrite(c); } catch { }
        try { ComputeIdentityTexts(c, pc, role, imp); } catch { }
    }

    private static void ComputeIdentityTexts(AmethystPlayerCache c, PlayerControl pc, RoleBehaviour role, bool imp)
    {
        c.AliveText = string.Empty;
        c.DeadText = string.Empty;
        c.MeetingText = string.Empty;

        if (role == null) return;

        bool isSelf = c.Id == LocalId;
        bool selfAliveTag = InGame && !c.Dead && isSelf && ShowSelfRole;
        bool impTeamTag = InGame && !LocalDead && LocalIsImp && imp && !isSelf && ShowImpTeammateRole;
        bool selfDeadTag = InGame && c.Dead && isSelf && ShowSelfRole;
        bool deadShowAllTag = DeadTagsAllowed;

        if (selfAliveTag || impTeamTag)
        {
            try { c.AliveText = Amethyst.Patches.AmethystDeadRoleReveal.BuildAliveText(pc) ?? string.Empty; }
            catch { c.AliveText = string.Empty; }
        }

        if (selfDeadTag || deadShowAllTag)
        {
            try { c.DeadText = Amethyst.Patches.AmethystDeadRoleReveal.BuildDeadWorldText(pc) ?? string.Empty; }
            catch { c.DeadText = string.Empty; }
        }

        if (selfAliveTag || impTeamTag || selfDeadTag || deadShowAllTag)
        {
            try { c.MeetingText = Amethyst.Patches.AmethystDeadRoleReveal.BuildMeetingSuffix(pc) ?? string.Empty; }
            catch { c.MeetingText = string.Empty; }
        }
    }

    private static void ComputeNameWrite(AmethystPlayerCache c)
    {
        if (!NameColorsOn || c.NameText == null) return;

        TextMeshPro text = c.NameText;

        if (c.Id == LocalId)
        {
            
            int want = c.ShownColorId;
            if (want < 0) return;

            Color target;
            try { target = AmethystColors.ColorOf(want); } catch { return; }

            Color cur;
            try { cur = text.color; } catch { return; }
            target.a = cur.a;   

            if (!SameRgb(cur, target) || c.WrittenVertexColorId != want)
                c.PendingVertexColorId = want;
            return;
        }

        if (c.IsShapeshifting || !c.OutfitIsDefault) return;
        if (string.IsNullOrEmpty(c.Name)) return;

        string hex;
        if (LocalIsImp && c.IsImpostor && c.Id != LocalId)
            hex = "#FF0000";
        else
            hex = AmethystColors.Hex(c.ColorId);
        if (string.IsNullOrEmpty(hex)) return;

        string want2 = $"<color={hex}>{c.Name}</color>";

        string live = null;
        try { live = text.text; } catch { }
        if (live == want2)
        {
            c.WrittenNameText = want2;   
            c.WrittenForName = text;
            return;
        }

        c.PendingNameText = want2;
    }

    private static void ComputeColorBlindWrite(AmethystPlayerCache c)
    {
        if (!ColorBlindOn || c.ColorBlindText == null) return;

        int wantId = c.ShownColorId >= 0 ? c.ShownColorId : c.ColorId;
        if (wantId < 0) return;

        string want = AmethystColors.ColoredColorName(wantId);
        if (string.IsNullOrEmpty(want)) return;

        string live = null;
        try { live = c.ColorBlindText.text; } catch { }
        if (live == want)
        {
            c.WrittenColorBlindId = wantId;
            c.WrittenForColorBlind = c.ColorBlindText;
            return;
        }

        c.PendingColorBlindId = wantId;
    }

    private static bool SameRgb(Color a, Color b)
        => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
}