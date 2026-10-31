using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace Amethyst;

internal sealed class RoleTagInfo
{
    internal string Label = string.Empty;
    internal Color Color = Color.white;
}

internal static class AmethystRoleTagStore
{
    internal static bool Enabled => AmethystConfig.RoleTag != null && AmethystConfig.RoleTag.Value;

    internal static readonly Color ImpostorColor = new Color32(255, 63, 63, 255);
    internal static readonly Color CrewmateColor = new Color32(92, 168, 255, 255);
    internal static readonly Color ClearColor = new Color32(185, 185, 185, 255);   

    private static readonly Dictionary<byte, RoleTagInfo> Tags = new Dictionary<byte, RoleTagInfo>();

    internal static void Reset()
    {
        Tags.Clear();
        SuffixCache.Clear();
        DeadCache.Clear();
        _deadProbeFrame = -999;
    }

    internal static void Set(byte playerId, string label, Color color)
    {
        Tags[playerId] = new RoleTagInfo { Label = label ?? string.Empty, Color = color };
        SuffixCache.Remove(playerId);   
    }

    internal static void Clear(byte playerId)
    {
        Tags.Remove(playerId);
        SuffixCache.Remove(playerId);
    }

    internal static bool Has(byte playerId) => Tags.ContainsKey(playerId);

    internal static bool IsDeadPlayer(byte playerId)
    {
        try
        {
            GameData gd = GameData.Instance;
            if (gd != null)
            {
                NetworkedPlayerInfo info = gd.GetPlayerById(playerId);
                if (info == null) return true;          
                if (info.Disconnected) return true;     
                if (info.IsDead) return true;           
                if (info.IsIncomplete) return true;     
                return false;
            }
        }
        catch { }

        try
        {
            if (AmethystPlayerCacheStore.TryGet(playerId, out AmethystPlayerCache c) && c != null)
                return c.Dead;
        }
        catch { }

        return false;
    }

    private static readonly Dictionary<byte, string> SuffixCache = new Dictionary<byte, string>();

    private static readonly Dictionary<byte, bool> DeadCache = new Dictionary<byte, bool>();
    private static int _deadProbeFrame = -999;

    private static bool IsDeadCached(byte playerId)
    {
        int frame = Time.frameCount;
        if (frame - _deadProbeFrame >= 10)
        {
            _deadProbeFrame = frame;
            DeadCache.Clear();
        }

        if (DeadCache.TryGetValue(playerId, out bool dead)) return dead;

        dead = IsDeadPlayer(playerId);
        DeadCache[playerId] = dead;
        return dead;
    }

    private static int _lobbyProbeFrame = -999;
    private static bool _inLobby;

    internal static void InvalidateSuffix() => SuffixCache.Clear();

    internal static void InvalidateDeadCache()
    {
        DeadCache.Clear();
        _deadProbeFrame = -999;
    }

    private static bool InLobbyNow()
    {
        int frame = Time.frameCount;
        if (frame - _lobbyProbeFrame >= 10)
        {
            _lobbyProbeFrame = frame;
            try { _inLobby = LobbyBehaviour.Instance != null; } catch { _inLobby = false; }
        }
        return _inLobby;
    }

    internal static string Suffix(byte playerId)
    {
        if (!Enabled) return string.Empty;

        if (InLobbyNow()) return string.Empty;

        if (IsDeadCached(playerId)) return string.Empty;

        if (SuffixCache.TryGetValue(playerId, out string hit)) return hit;

        string made = string.Empty;
        if (Tags.TryGetValue(playerId, out RoleTagInfo info) && info != null && !string.IsNullOrEmpty(info.Label))
            made = $" <color=#{ColorUtility.ToHtmlStringRGB(info.Color)}>[{info.Label}]</color>";

        SuffixCache[playerId] = made;
        return made;
    }

    internal sealed class RoleEntry
    {
        internal RoleTypes Role;
        internal RoleBehaviour Behaviour;
        internal string Label = string.Empty;
        internal Color Color = Color.white;
        internal bool Impostor;
        internal bool IsClear;   
    }

    private static bool SkipRole(RoleTypes r)
    {
        try { return RoleManager.IsGhostRole(r); }
        catch { return r == RoleTypes.ImpostorGhost || r == RoleTypes.CrewmateGhost || r == RoleTypes.GuardianAngel; }
    }

    internal static List<RoleEntry> BuildRoleList()
    {
        var list = new List<RoleEntry>();
        try
        {
            var all = RoleManager.Instance != null ? RoleManager.Instance.AllRoles : null;
            if (all == null) return list;

            for (int i = 0; i < all.Count; i++)
            {
                RoleBehaviour b = all[i];
                if (b == null) continue;
                if (SkipRole(b.Role)) continue;

                bool imp = false;
                try { imp = b.IsImpostor; } catch { }

                string label = string.Empty;
                try { label = Patches.AmethystDeadRoleReveal.GetRoleLabel(b.Role, b.NiceName); } catch { }
                if (string.IsNullOrEmpty(label)) label = b.Role.ToString();

                list.Add(new RoleEntry
                {
                    Role = b.Role,
                    Behaviour = b,
                    Label = label,
                    Impostor = imp,
                    Color = imp ? ImpostorColor : CrewmateColor
                });
            }

            list.Sort((x, y) => y.Impostor.CompareTo(x.Impostor));

            list.Add(new RoleEntry
            {
                Role = RoleTypes.Crewmate,
                Label = AmethystText.T("RoleTagClear"),
                Color = ClearColor,
                Impostor = false,
                IsClear = true
            });
        }
        catch { }
        return list;
    }
}

[HarmonyLib.HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystRoleTagResetPatch
{
    private static void Prefix() => AmethystRoleTagStore.Reset();
}

[HarmonyLib.HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
internal static class AmethystRoleTagEndGameResetPatch
{
    private static void Prefix() => AmethystRoleTagStore.Reset();
}
