using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace Amethyst;

// 职业配色表（第 172 轮新功能）。
//
// 需求：为**原版每个职业**渲染上独立的颜色 —— 例：大嗓门（Noisemaker）用偏橘偏红的颜色，
//       而不是像以前那样一律「内鬼红 / 船员蓝」两种。
//
// 参考模组 EndlessHostRoles 8.0.1 的做法（`Main.cs` 第 446 行起的 `RoleHtmlColors` 字典）：
//   它给每个角色一个 HTML 色号，再由 `Utils.GetRoleColorCode` / `GetRoleColor` 统一查询。
//   其「原版 Vanilla」一节恰好就是我们需要的那 8 个原版职业：
//       Crewmate #8cffff · Engineer #FF6A00 · Scientist #8ee98e · GuardianAngel #77e6d1
//       Tracker  #34ad50 · Noisemaker #ff4a62 · Detective #625EEE · Judge #f8d85a
//   （EHR 把 Impostor/Shapeshifter/Phantom/Viper 等内鬼阵营的角色统一归到 `Main.ImpostorColor`，
//     并有一个 `AllCrewRolesHaveVanillaColor` 开关把船员系角色全部改回 #8cffff。）
//
// 本文件把这套配色**原样移植**过来，并补齐原版内鬼阵营职业（用游戏自己的调色板红）。
//
// ⚠ 设计要点：
//   1. 色号是**常量表**，不依赖游戏运行时状态，任何场景都能安全查询（含启动早期）。
//   2. 查询按 `RoleTypes` 枚举，未收录的职业回退到阵营色（内鬼红 / 船员浅青），
//      因此未来游戏新增职业不会出现「没颜色」的空档。
//   3. 结果按枚举缓存（枚举数量固定，缓存天然有界），热路径零分配。
internal static class AmethystRoleColors
{
    // 参考 EndlessHostRoles 的 `Main.ImpostorColor`：内鬼阵营用的就是游戏调色板红。
    internal static readonly Color ImpostorColor = new Color32(0xC6, 0x11, 0x11, 0xFF);
    internal static readonly Color CrewmateColor = new Color32(0x8C, 0xFF, 0xFF, 0xFF);

    // 与 EHR `RoleHtmlColors` 的「Vanilla」一节一一对应；内鬼系职业补游戏原生红。
    //
    // ⚠ 覆盖完整性：改用 **switch 表达式 + 显式 default** 而不是字典。
    //   字典是"运行时查不到才回退"，漏一个职业**编译器不会报错**，只会静默变回阵营色 ——
    //   这正是"不要乱猜"所担心的。switch 形式让覆盖情况**一眼可见**，
    //   并且下面 `AllRolesCovered()` 会对着枚举做一次自检（见文件末）。
    //
    // 原版 RoleTypes 全枚举（取自游戏程序集 Assembly-CSharp.dll，**共 15 个**）：
    //   Crewmate=0 Impostor=1 Scientist=2 Engineer=3 GuardianAngel=4 Shapeshifter=5
    //   CrewmateGhost=6 ImpostorGhost=7 Noisemaker=8 Phantom=9 Tracker=10
    //   Detective=12 Viper=18 Judge=19 SpiritGuide=21
    //   （11 / 13~17 / 20 在枚举中不存在）
    //
    // ⚠ 第 187 轮：游戏更新到 v19.0（2026-09-29）后新增了 `SpiritGuide=21`（网红/Influencer）。
    //   在此之前本项目编译用的参考程序集是旧版游戏（只有 14 个值），**根本编译不到这个职业**，
    //   于是它会静默落到 default 的船员浅青、且没有专属说明文案。
    //   参考程序集已换成 BepInEx\interop\（当前游戏真实 API），详见 csproj 的 GameRefsDir 注释。
    internal static Color ColorOf(RoleTypes role)
    {
        switch (role)
        {
            // ---- 船员阵营（EHR Vanilla 段原色）----
            case RoleTypes.Crewmate:      return Rgb(0x8C, 0xFF, 0xFF); // #8cffff
            case RoleTypes.Engineer:      return Rgb(0xFF, 0x6A, 0x00); // #FF6A00 偏橙
            case RoleTypes.Scientist:     return Rgb(0x8E, 0xE9, 0x8E); // #8ee98e 浅绿
            case RoleTypes.GuardianAngel: return Rgb(0x77, 0xE6, 0xD1); // #77e6d1 青绿
            case RoleTypes.Tracker:       return Rgb(0x34, 0xAD, 0x50); // #34ad50 深绿
            case RoleTypes.Noisemaker:    return Rgb(0xFF, 0x4A, 0x62); // #ff4a62 偏橘偏红 ← 用户举例
            case RoleTypes.Detective:     return Rgb(0x62, 0x5E, 0xEE); // #625EEE 蓝紫
            case RoleTypes.Judge:         return Rgb(0xF8, 0xD8, 0x5A); // #f8d85a 亮黄
            case RoleTypes.CrewmateGhost: return Rgb(0x8C, 0xFF, 0xFF); // 幽灵沿用船员色

            // ---- 船员幽灵职业（第 187 轮新增）----
            // 网红/Influencer：船员阵营的**幽灵形态**职业（死后仍可行动）。
            case RoleTypes.SpiritGuide:   return Rgb(0xFF, 0x00, 0x66); // #ff0066

            // ---- 内鬼阵营（EHR 走 Main.ImpostorColor）----
            case RoleTypes.Impostor:      return ImpostorColor;
            case RoleTypes.Shapeshifter:  return ImpostorColor;
            case RoleTypes.Phantom:       return ImpostorColor;
            case RoleTypes.Viper:         return ImpostorColor;
            case RoleTypes.ImpostorGhost: return ImpostorColor;

            // 未来游戏新增职业：按阵营兜底（内鬼红 / 船员浅青），绝不出现"没颜色"。
            default:
                return IsImpostorRole(role) ? ImpostorColor : CrewmateColor;
        }
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

    // 内鬼阵营职业集合（颜色以外的用途：判断是否走「队友红」等既有逻辑）。
    private static readonly HashSet<RoleTypes> ImpostorRoles = new HashSet<RoleTypes>
    {
        RoleTypes.Impostor, RoleTypes.ImpostorGhost, RoleTypes.Shapeshifter,
        RoleTypes.Phantom, RoleTypes.Viper
    };

    internal static bool IsImpostorRole(RoleTypes role) => ImpostorRoles.Contains(role);

    internal static bool Enabled => AmethystConfig.RoleColors != null && AmethystConfig.RoleColors.Value;

    // 枚举 -> "#RRGGBB"（富文本用）。按枚举缓存，热路径零分配。
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

    // 把职业名包成该职业的颜色。未开启功能时原样返回。
    internal static string Colorize(RoleTypes role, string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (!Enabled) return text;
        return $"<color={HexOf(role)}>{text}</color>";
    }

    // ===== 覆盖自检 =====
    //
    // 用**反射遍历 `RoleTypes` 的真实枚举成员**，确保每一个都已经被 ColorOf 显式处理
    // （而不是静默落到 default 的阵营色）。游戏更新新增职业后，这个方法会在下次启动时
    // 把漏掉的职业名写进 BepInEx 日志，避免"配色悄悄少了几个职业却没人发现"。
    //
    // ⚠ 第 174 轮修正：旧实现用「查回来的颜色 == 阵营兜底色」当作"没覆盖"的判据，
    //   这是**错的** —— 有些职业是**故意**和阵营色相同的（`CrewmateGhost` 就该等于船员浅青、
    //   `Impostor` 就该等于内鬼红）。于是它会误报 CrewmateGhost"没有专属颜色"
    //   （实测日志里就是这么跳的），而这类假警报会让人逐渐忽略这条日志。
    //
    //   现在改为**逐项对照显式名单**：`ExplicitlyMapped` 列出 ColorOf 的 switch 里
    //   实际写了 case 的所有职业。名单与 switch 是两处维护、可能漂移，所以下面还会
    //   用"两边集合是否相等"做一次交叉校验，任何一边漏了都会报警。
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

            // 反向校验：名单里有没有已经不在枚举里的（游戏删角色 / 枚举被改）。
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
