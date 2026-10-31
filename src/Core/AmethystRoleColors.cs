using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace Amethyst;

internal static class AmethystRoleColors
{
    
    internal static readonly Color ImpostorColor = new Color32(0xC6, 0x11, 0x11, 0xFF);
    internal static readonly Color CrewmateColor = new Color32(0x8C, 0xFF, 0xFF, 0xFF);

    internal static Color ColorOf(RoleTypes role)
    {
        switch (role)
        {
            
            case RoleTypes.Crewmate:      return Rgb(0x8C, 0xFF, 0xFF); 
            case RoleTypes.Engineer:      return Rgb(0xFF, 0x6A, 0x00); 
            case RoleTypes.Scientist:     return Rgb(0x8E, 0xE9, 0x8E); 
            case RoleTypes.GuardianAngel: return Rgb(0x77, 0xE6, 0xD1); 
            case RoleTypes.Tracker:       return Rgb(0x34, 0xAD, 0x50); 
            case RoleTypes.Noisemaker:    return Rgb(0xFF, 0x4A, 0x62); 
            case RoleTypes.Detective:     return Rgb(0x62, 0x5E, 0xEE); 
            case RoleTypes.Judge:         return Rgb(0xF8, 0xD8, 0x5A); 
            case RoleTypes.CrewmateGhost: return Rgb(0x8C, 0xFF, 0xFF); 

            case RoleTypes.SpiritGuide:   return Rgb(0xFF, 0x00, 0x66); 

            case RoleTypes.Impostor:      return ImpostorColor;
            case RoleTypes.Shapeshifter:  return ImpostorColor;
            case RoleTypes.Phantom:       return ImpostorColor;
            case RoleTypes.Viper:         return ImpostorColor;
            case RoleTypes.ImpostorGhost: return ImpostorColor;

            default:
                return IsImpostorRole(role) ? ImpostorColor : CrewmateColor;
        }
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

    private static readonly HashSet<RoleTypes> ImpostorRoles = new HashSet<RoleTypes>
    {
        RoleTypes.Impostor, RoleTypes.ImpostorGhost, RoleTypes.Shapeshifter,
        RoleTypes.Phantom, RoleTypes.Viper
    };

    internal static bool IsImpostorRole(RoleTypes role) => ImpostorRoles.Contains(role);

    internal static bool Enabled => AmethystConfig.RoleColors != null && AmethystConfig.RoleColors.Value;

    private static readonly Dictionary<RoleTypes, string> HexCache = new Dictionary<RoleTypes, string>();

    internal static string HexOf(RoleTypes role)
    {
        if (HexCache.TryGetValue(role, out string hit)) return hit;

        Color c = ColorOf(role);
        string made = string.Format("#{0:X2}{1:X2}{2:X2}",
            Mathf.RoundToInt(c.r * 255f), Mathf.RoundToInt(c.g * 255f), Mathf.RoundToInt(c.b * 255f));
        HexCache[role] = made;
        return made;
    }

    internal static string Colorize(RoleTypes role, string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (!Enabled) return text;
        return $"<color={HexOf(role)}>{text}</color>";
    }

    private static readonly RoleTypes[] ExplicitlyMapped =
    {
        RoleTypes.Crewmate, RoleTypes.Engineer, RoleTypes.Scientist, RoleTypes.GuardianAngel,
        RoleTypes.Tracker, RoleTypes.Noisemaker, RoleTypes.Detective, RoleTypes.Judge,
        RoleTypes.CrewmateGhost, RoleTypes.SpiritGuide,
        RoleTypes.Impostor, RoleTypes.Shapeshifter, RoleTypes.Phantom, RoleTypes.Viper,
        RoleTypes.ImpostorGhost,
    };

    internal static void VerifyCoverage()
    {
        try
        {
            var mapped = new System.Collections.Generic.HashSet<RoleTypes>(ExplicitlyMapped);
            var all = (RoleTypes[])Enum.GetValues(typeof(RoleTypes));

            var missing = new System.Collections.Generic.List<string>();
            foreach (RoleTypes role in all)
                if (!mapped.Contains(role)) missing.Add(role.ToString());

            var stray = new System.Collections.Generic.List<string>();
            var allSet = new System.Collections.Generic.HashSet<RoleTypes>(all);
            foreach (RoleTypes role in ExplicitlyMapped)
                if (!allSet.Contains(role)) stray.Add(role.ToString());

            if (missing.Count > 0)
                AmethystPlugin.Logger?.LogWarning(
                    "[RoleColors] 以下职业没有配到颜色，已回退为阵营色（请在 AmethystRoleColors.ColorOf 里补上 case）："
                    + string.Join(", ", missing));
            if (stray.Count > 0)
                AmethystPlugin.Logger?.LogWarning(
                    "[RoleColors] ExplicitlyMapped 里有已不存在的职业（游戏枚举变了？）："
                    + string.Join(", ", stray));
            if (missing.Count == 0 && stray.Count == 0)
                AmethystPlugin.Logger?.LogInfo(
                    $"[RoleColors] 覆盖自检通过：{all.Length} 个原版职业全部有配色。");
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[RoleColors] 覆盖自检失败: {ex.Message}");
        }
    }
}
