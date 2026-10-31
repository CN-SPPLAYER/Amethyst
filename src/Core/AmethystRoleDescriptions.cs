using System;
using AmongUs.GameOptions;
using Amethyst;

namespace Amethyst;

internal static class AmethystRoleDescriptions
{
    
    internal static string Short(RoleTypes role)
        => Lookup("RoleInfo." + role.ToString());

    internal static string Long(RoleTypes role)
        => Lookup("RoleInfoLong." + role.ToString());

    private static string Lookup(string key)
    {
        try
        {
            string text = AmethystText.T(key);
            
            if (string.IsNullOrEmpty(text) || text == key) return string.Empty;
            return text;
        }
        catch { return string.Empty; }
    }

    internal static void VerifyCoverage()
    {
        try
        {
            var roles = (RoleTypes[])Enum.GetValues(typeof(RoleTypes));
            var missing = new System.Collections.Generic.List<string>();

            string original = null;
            try { original = AmethystConfig.Language.Value; } catch { }

            foreach (string lang in new[] { "zh", "en", "ru" })
            {
                try { AmethystConfig.Language.Value = lang; } catch { }

                foreach (RoleTypes role in roles)
                {
                    
                    bool isGhost;
                    try { isGhost = RoleManager.IsGhostRole(role); }
                    catch { isGhost = role == RoleTypes.CrewmateGhost || role == RoleTypes.ImpostorGhost; }

                    if (string.IsNullOrEmpty(Short(role)) && !isGhost)
                        missing.Add($"{lang}:RoleInfo.{role}");

                    if (string.IsNullOrEmpty(Long(role)) && !isGhost)
                        missing.Add($"{lang}:RoleInfoLong.{role}");
                }
            }

            if (original != null)
            {
                try { AmethystConfig.Language.Value = original; } catch { }
            }

            if (missing.Count > 0)
                AmethystPlugin.Logger?.LogWarning(
                    "[RoleInfo] 以下职业说明缺少本地化文本（请在 Resources/Lang/*.json 里补上）："
                    + string.Join(", ", missing));
            else
                AmethystPlugin.Logger?.LogInfo(
                    $"[RoleInfo] 本地化自检通过：3 种语言 × {roles.Length} 个职业的说明文本齐全。");
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[RoleInfo] 本地化自检失败: {ex.Message}");
        }
    }
}
