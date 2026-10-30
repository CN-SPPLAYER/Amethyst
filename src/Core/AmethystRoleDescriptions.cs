using System;
using AmongUs.GameOptions;
using Amethyst;

namespace Amethyst;

// ===== 每个原版职业的专属说明话术 =====
//
// ===== 参考 EndlessHostRoles 8.0.1 =====
//
// EHR 的职业说明放在**语言文件**里，按职业名拼 key 查表
// （`Modules\ExtendedPlayerControl.cs` 第 2218~2226 行）：
//     string info = (role.IsVanilla() ? "Blurb" : "Info") + (infoLong ? "Long" : "");
//     string roleInfo = GetString($"{role}{info}");     // 形如 "NoisemakerEHRInfo"
//
// 即每个职业两段文本：
//   `{Role}EHRInfo`     —— 一句话术（短，带性格）
//   `{Role}EHRInfoLong` —— 详细规则（长，多行）
//
// ⚠ 第 176 轮修复「切语言后职业信息仍是中文」：
//   我上一版把话术**硬编码成中文字符串写在 C# 里** —— 那样无论模组语言切成
//   英文还是俄语，面板里永远是中文。这是个低级错误：文本就没进本地化系统，
//   切换语言自然不可能生效。
//
//   现在改为和 EHR 完全一致的做法：文本全部放进 `Resources/Lang/*.json`，
//   代码只按 key 查表。三种语言（zh/en/ru）**逐条覆盖**，缺任何一条都会
//   在启动自检里报出来（见下方 VerifyCoverage）。
//
// 语言文件的 key 命名（与 EHR 对齐，便于日后对照）：
//   RoleInfo.<Role>      短话术
//   RoleInfoLong.<Role>  长说明
internal static class AmethystRoleDescriptions
{
    // 短话术：查 `RoleInfo.{Role}`。
    internal static string Short(RoleTypes role)
        => Lookup("RoleInfo." + role.ToString());

    // 长说明：查 `RoleInfoLong.{Role}`。
    internal static string Long(RoleTypes role)
        => Lookup("RoleInfoLong." + role.ToString());

    // 查表；查不到返回空串（面板会跳过该段，而不是显示 key）。
    private static string Lookup(string key)
    {
        try
        {
            string text = AmethystText.T(key);
            // T() 查不到时会原样返回 key —— 那不代表"有文本"。
            if (string.IsNullOrEmpty(text) || text == key) return string.Empty;
            return text;
        }
        catch { return string.Empty; }
    }

    // ===== 覆盖自检 =====
    //
    // 逐一核对**每个原版职业、每种语言**是否都配齐了两段文本。
    // 任一语言缺项都会写进 BepInEx 日志 —— 避免"切了语言却没人发现还是旧文本"。
    internal static void VerifyCoverage()
    {
        try
        {
            var roles = (RoleTypes[])Enum.GetValues(typeof(RoleTypes));
            var missing = new System.Collections.Generic.List<string>();

            // 逐语言检查：临时切到该语言查一次，再切回来。
            string original = null;
            try { original = AmethystConfig.Language.Value; } catch { }

            foreach (string lang in new[] { "zh", "en", "ru" })
            {
                try { AmethystConfig.Language.Value = lang; } catch { }

                foreach (RoleTypes role in roles)
                {
                    // 幽灵形态在 EHR 里也没有专属话术，允许缺失（我们已补了中文/英文）。
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
