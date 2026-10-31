using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystDeadRoleReveal
{
    private static readonly Dictionary<byte, int> KillCounts = new Dictionary<byte, int>();

    internal static void ResetKills()
    {
        KillCounts.Clear();
    }

    internal static void AddKill(byte playerId)
    {
        KillCounts.TryGetValue(playerId, out int oldValue);
        KillCounts[playerId] = oldValue + 1;
    }

    internal static int GetKills(byte playerId)
    {
        return KillCounts.TryGetValue(playerId, out int value) ? value : 0;
    }

    internal static bool ShouldShowAlive(PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null) return false;

            AmongUsClient net = AmongUsClient.Instance;
            if (net == null || !net.IsGameStarted) return false;
            if (ShipStatus.Instance == null) return false;
            if (LobbyBehaviour.Instance != null) return false;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.IsDead) return false;

            if (target.PlayerId == local.PlayerId)
                return AmethystConfig.ShowSelfRole.Value;

            if (local.Data.Role != null && local.Data.Role.IsImpostor &&
                target.Data.Role != null && target.Data.Role.IsImpostor)
                return AmethystConfig.ShowImpostorTeammateRole.Value;

            return false;
        }
        catch
        {
            return false;
        }
    }

    internal static bool ShouldReveal(PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null) return false;

            AmongUsClient net = AmongUsClient.Instance;
            if (net == null || !net.IsGameStarted) return false;
            if (ShipStatus.Instance == null) return false;
            if (LobbyBehaviour.Instance != null) return false;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return false;
            if (!local.Data.IsDead) return false;

            if (!AmethystPlayerCacheStore.LocalIsCrewmateGhost) return false;

            return AmethystConfig.ShowRolesWhenDead.Value;
        }
        catch
        {
            return false;
        }
    }

    private static string RoleHex(RoleBehaviour role)
    {
        try
        {
            if (AmethystRoleColors.Enabled && role != null)
                return StripHash(AmethystRoleColors.HexOf(role.Role));
        }
        catch { }
        try { return StripHash(ColorUtility.ToHtmlStringRGB(role.TeamColor)); }
        catch { return "FFFFFF"; }
    }

    private static string StripHash(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return "FFFFFF";
        return hex[0] == '#' ? hex.Substring(1) : hex;
    }

    internal static string BuildAliveText(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null) return string.Empty;

            RoleBehaviour role = player.Data.Role;
            if (role == null) return string.Empty;

            string roleName = GetRoleLabel(role.Role, role.NiceName);
            if (string.IsNullOrEmpty(roleName)) return string.Empty;

            string color = RoleHex(role);

            if (role.IsImpostor)
            {
                int kills = GetKills(player.PlayerId);
                string killLabel = AmethystText.T("Kills", kills);
                return $"<color=#{color}>{roleName}</color> <color=#FFD24A>({killLabel})</color>";
            }

            if (player.Data.Tasks != null && player.Data.Tasks.Count > 0)
            {
                int completed = 0;
                foreach (var task in player.Data.Tasks)
                    if (task.Complete) completed++;
                return $"<color=#{color}>{roleName}</color> <color=#FFFF00>({completed}/{player.Data.Tasks.Count})</color>";
            }

            return $"<color=#{color}>{roleName}</color>";
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string BuildDeadWorldText(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null) return string.Empty;

            RoleBehaviour role = player.Data.Role;
            if (role == null) return string.Empty;

            string name = GetRoleLabel(role.Role, role.NiceName);
            if (string.IsNullOrEmpty(name)) return string.Empty;

            string roleColor = RoleHex(role);

            if (role.IsImpostor)
            {
                int kills = GetKills(player.PlayerId);
                string killLabel = AmethystText.T("Kills", kills);
                return $"<color=#{roleColor}>{name}</color> <color=#FFD24A>({killLabel})</color>";
            }

            if (player.Data.Tasks != null && player.Data.Tasks.Count > 0)
            {
                int completed = 0;
                foreach (var task in player.Data.Tasks)
                    if (task.Complete) completed++;
                return $"<color=#{roleColor}>{name}</color> <color=#FFFF00>({completed}/{player.Data.Tasks.Count})</color>";
            }

            return $"<color=#{roleColor}>{name}</color>";
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string BuildMeetingSuffix(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null) return string.Empty;

            RoleBehaviour role = player.Data.Role;
            if (role == null) return string.Empty;

            string label = GetRoleLabel(role.Role, role.NiceName);
            if (string.IsNullOrEmpty(label)) return string.Empty;

            string roleColor = RoleHex(role);
            string yellow = "#FFD24A";

            if (role.IsImpostor)
            {
                int kills = GetKills(player.PlayerId);
                string killLabel = AmethystText.T("Kills", kills);
                return $"<size=65%><color=#{roleColor}>【{label}】</color><color={yellow}>【{killLabel}】</color></size>";
            }

            if (player.Data.Tasks != null && player.Data.Tasks.Count > 0)
            {
                int completed = 0;
                foreach (var task in player.Data.Tasks)
                    if (task.Complete) completed++;
                return $"<size=65%><color=#{roleColor}>【{label}】</color><color=#FFFF00>【{completed}/{player.Data.Tasks.Count}】</color></size>";
            }

            return $"<size=65%><color=#{roleColor}>【{label}】</color></size>";
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string BuildChatTag(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null) return string.Empty;

            RoleBehaviour role = player.Data.Role;
            if (role == null) return string.Empty;

            string roleName = GetRoleLabel(role.Role, role.NiceName);
            if (string.IsNullOrEmpty(roleName)) return string.Empty;

            string color = RoleHex(role);

            if (role.IsImpostor)
            {
                int kills = GetKills(player.PlayerId);
                string killLabel = AmethystText.T("Kills", kills);
                return $"<color=#{color}>{roleName}</color> <color=#FFD24A>({killLabel})</color>";
            }

            if (player.Data.Tasks != null && player.Data.Tasks.Count > 0)
            {
                int completed = 0;
                foreach (var task in player.Data.Tasks)
                    if (task.Complete) completed++;
                return $"<color=#{color}>{roleName}</color> <color=#FFFF00>({completed}/{player.Data.Tasks.Count})</color>";
            }

            return $"<color=#{color}>{roleName}</color>";
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string GetRoleLabel(RoleTypes role, string fallback)
    {
        string localized = LocalizedRoleName(role);
        if (!string.IsNullOrEmpty(localized)) return localized;

        if (!string.IsNullOrEmpty(fallback) && !LooksLikeEnumName(fallback, role))
            return fallback;

        string pretty = Prettify(role.ToString());
        return string.IsNullOrEmpty(pretty) ? role.ToString() : pretty;
    }

    private static string LocalizedRoleName(RoleTypes role)
    {
        string key;
        switch (role)
        {
            case RoleTypes.Crewmate: key = "Crewmate"; break;
            case RoleTypes.Impostor: key = "Impostor"; break;
            case RoleTypes.Scientist: key = "Scientist"; break;
            case RoleTypes.Engineer: key = "Engineer"; break;
            case RoleTypes.GuardianAngel: key = "GuardianAngel"; break;
            case RoleTypes.Shapeshifter: key = "Shapeshifter"; break;
            case RoleTypes.CrewmateGhost: key = "CrewmateGhost"; break;
            case RoleTypes.ImpostorGhost: key = "ImpostorGhost"; break;
            case RoleTypes.Noisemaker: key = "Noisemaker"; break;
            case RoleTypes.Phantom: key = "Phantom"; break;
            case RoleTypes.Tracker: key = "Tracker"; break;
            case RoleTypes.Detective: key = "Detective"; break;
            case RoleTypes.Viper: key = "Viper"; break;
            case RoleTypes.Judge: key = "Judge"; break;
            
            case RoleTypes.SpiritGuide: key = "SpiritGuide"; break;
            default: return null;   
        }

        try
        {
            string text = AmethystText.T(key);
            
            return string.IsNullOrEmpty(text) || text == key ? null : text;
        }
        catch { return null; }
    }

    private static bool LooksLikeEnumName(string value, RoleTypes role)
        => !string.IsNullOrEmpty(value) && string.Equals(value, role.ToString(), StringComparison.Ordinal);

    private static string Prettify(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    internal static float NameLineExtra(TextMeshPro nameText)
    {
        try
        {
            if (nameText == null || nameText.textInfo == null) return 0f;
            if (nameText.textInfo.lineCount > 1)
                return 0.16f * (nameText.textInfo.lineCount - 1);
        }
        catch { }
        return 0f;
    }

    internal static PlayerControl FindPlayer(byte playerId)
    {
        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return null;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl pc = all[i];
                if (pc != null && pc.PlayerId == playerId) return pc;
            }
        }
        catch { }

        return null;
    }
}







