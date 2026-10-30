using System.Globalization;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

// 主界面版本号显示：把游戏原生的一行（形如 `v19s (build num: 7489)`）
// 改写成「平台 + 版本 + 日期」格式，形如 `Steam v19.0 2026.9.29`。
//
// ===== 布局部分的关键教训（务必先读）=====
//
// 本轮之前为了"右下角显示模组标识"，先后试过五种做法，四次失败：
//
//  ① Destroy `AspectPosition` + 写一次坐标 → 原生版本号和标识**一起消失**
//     （`AspectPosition.Update` 才是钉边角的那个组件，删了就飞出屏幕）。
//  ② `Alignment = RightBottom` → 整行（含原生版本号）一起跑到右下角。
//  ③ 新建对象挂到 `VersionShower.transform.parent` → **什么也看不见**
//     （父级带 `RectMask2D` 会裁掉新对象；`AspectPosition` 在非全屏容器里
//      算的是容器相对坐标；主菜单重建时新对象被连带销毁）。
//  ④ 富文本 `<align="right">` → 无效（对象的 `TextAlignmentOptions` 覆盖它）。
//  ⑤ 自建覆盖 Canvas + 归一化锚点 → **定位成功**，实机确认能显示，
//     但用户认为不如原样好看 → 已按要求整体回退。
//
// ===== 现在的做法（只换内容，绝不碰布局）=====
//
// **只改 `VersionShower.text` 的字符串**，位置 / 字号 / 缩放 / 遮罩
// 全部交给游戏自己管 —— 这样就不可能再把原生版本号搞丢，
// 也不受主菜单重建与 `RectMask2D` 影响。
//
// ===== 数据来源（2026-10-16 修复：实机显示成了 `TESTNAME 2026.9.29 2404160.2404160.2404160`）=====
//
// 旧实现三处数据源在 v19 版游戏里全部失效，逐项修正：
//
//  1) 平台位取 `Constants.GetPlatformName()` —— 它在 PC 上返回的**就是占位串
//     `TESTNAME`**（联网层用它代表 PC 平台：参考模组 Hydra/Onyx 靠伪造该值伪装 PC 玩家，
//     本工程 `AmethystAccess` 的平台数据校验也以 `PlatformName == "TESTNAME"` 判定 PC）。
//     → 显示一律改走 **`Platforms` 枚举映射**（BetterAmongUs / EHR 做法；本工程
//       `LobbySearchPatches.GetPlatformName` 已有同一张表）。平台名 API 仅作末位兜底，
//       且必须过滤 `TESTNAME`。
//
//  2) 版本位取 `Application.version` —— v19 起游戏把它改成了**日期串** `2026.9.29`
//     （官方只在设置页/内部显示这种格式；主菜单显示的是 `19.x` 形式。
//      佐证：BetterAmongUs 的 `AppVersion => Application.version`，其支持列表就是 "2026.9.29"）。
//     → 人类版本号改用 `ReferenceDataManager.Instance.Refdata.userFacingVersion`
//       （BetterAmongUs `AmongUsVersion` / Hydra 同款来源，形如 `19.0`，显示时补 `v` 前缀）。
//
//  3) 日期位取 `Constants.GetVersionComponents(GetBroadcastVersion())` 的 (年,月,日) ——
//     该函数在新版里已不是"年.月.日"语义，拆出来的三个分量是同一个构建号
//     （实机显示 `2404160.2404160.2404160`，连 getter 都拿到同一个值）。
//     → 日期位优先取 `Application.version`（现在它本身就是 `2026.9.29`）；
//       仅当它不是日期格式（旧版游戏形如 `v19s`）才回落 `GetVersionComponents`，
//       并强制做 (2000..2100 / 1..12 / 1..31) 合理性校验：校验不过整段省略 ——
//       **宁可少显示，也绝不把垃圾数字放上去**。
//
// 兜底与刷新：`VersionShower.Start` 只跑一次，而主菜单对象每次进入都会重建；
// 另挂 `MainMenuManager.LateUpdate` 1 秒节流（与 `AmethystMainMenuStyle` 同做法），
// 保证重进主菜单、或 refdata 晚到时内容会被重新校正。全部数据都拿不到时返回 null
// → **保持游戏原生文本**，绝不写空、绝不写占位。
//
// 本文件另有一处取证日志：绑定对象时记录一次游戏**原生**文本
// （`[VersionLine] native=...`），以后核对"游戏自己写了什么"看这一行即可。

[HarmonyPatch(typeof(VersionShower), "Start")]
internal static class AmethystVersionShowerPatch
{
    private static void Postfix(VersionShower __instance)
    {
        AmethystVersionLine.Bind(__instance);
    }
}

// 低频兜底：主菜单自身 LateUpdate，1 秒一次足够
//（进对局再返回时主菜单对象会换新；refdata 也可能晚于 Start 才就绪）。
[HarmonyPatch(typeof(MainMenuManager), "LateUpdate")]
internal static class AmethystVersionLineUpdatePatch
{
    private static float _next;

    private static void Postfix()
    {
        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + 1f;
        AmethystVersionLine.Refresh();
    }
}

internal static class AmethystVersionLine
{
    private static TMP_Text _text;
    private static string _applied;
    private static bool _logged;

    /// <summary>VersionShower.Start 时绑定文本对象，并写入一次。</summary>
    internal static void Bind(VersionShower shower)
    {
        try
        {
            if (shower == null) return;
            TMP_Text text = shower.text;
            if (text == null) return;

            _text = text;
            _applied = null;

            if (!_logged)
            {
                _logged = true;
                // 只记录一次原生文本：取证用，确认游戏自己写的是什么
                try { AmethystPlugin.Logger?.LogInfo($"[VersionLine] native=\"{text.text}\""); } catch { }
            }

            Apply();
        }
        catch { }
    }

    /// <summary>低频重校正（主菜单 LateUpdate，1 秒节流）。</summary>
    internal static void Refresh()
    {
        if (_text == null) return;
        Apply();
    }

    private static void Apply()
    {
        try
        {
            TMP_Text text = _text;
            if (text == null) return;

            string line = Build();
            if (string.IsNullOrEmpty(line)) return; // 数据不足 → 保持游戏原样
            if (_applied == line || text.text == line) { _applied = line; return; }

            text.text = line;
            text.ForceMeshUpdate();
            _applied = line;
            AmethystPlugin.Logger?.LogInfo($"[VersionLine] show=\"{line}\"");
        }
        catch { }
    }

    /// <summary>拼「平台 + 版本 + 日期」；关键项全缺时返回 null（保持原生文本）。</summary>
    private static string Build()
    {
        string platform = Platform();
        string version = Version();
        string date = Date();

        var sb = new StringBuilder(48);
        if (!string.IsNullOrEmpty(platform)) sb.Append(platform);
        if (!string.IsNullOrEmpty(version))
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(version);
        }
        // 版本与日期相同时（例如旧版两边都拿到 `2026.9.29`）只显示一次
        if (!string.IsNullOrEmpty(date) && !string.Equals(date, version, System.StringComparison.OrdinalIgnoreCase))
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(date);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    // 平台名：`Platforms` 枚举映射优先 —— 唯一在所有平台上都正确的显示来源。
    private static string Platform()
    {
        try
        {
            try
            {
                Platforms p = Platforms.Unknown;
                var pd = Constants.GetPlatformData();
                if (pd != null) p = pd.Platform;
                else p = Constants.GetPlatformType();

                string name = MapPlatform(p);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch { }

            // 末位兜底：游戏自己的平台名 API（务必过滤 PC 占位串 TESTNAME）
            try { string s = Constants.GetCurrentPlatformName(); if (NameOk(s)) return s.Trim(); } catch { }
            try { string s = Constants.GetPlatformName(); if (NameOk(s)) return s.Trim(); } catch { }
            try { string s = Constants.GetPurchasingPlatformType(); if (NameOk(s)) return s.Trim(); } catch { }
            return null;
        }
        catch { return null; }
    }

    private static bool NameOk(string s)
        => !string.IsNullOrWhiteSpace(s)
           && !s.Trim().Equals("TESTNAME", System.StringComparison.OrdinalIgnoreCase);

    // 与 `LobbySearchPatches.GetPlatformName` 保持同一张表（均移植自 BetterAmongUs）
    private static string MapPlatform(Platforms platform)
    {
        return platform switch
        {
            Platforms.StandaloneSteamPC => "Steam",
            Platforms.StandaloneEpicPC => "Epic Games",
            Platforms.StandaloneWin10 => "Microsoft Store",
            Platforms.StandaloneMac => "Mac OS",
            Platforms.StandaloneItch => "Itch.io",
            Platforms.Xbox => "Xbox",
            Platforms.Playstation => "Playstation",
            Platforms.Switch => "Switch",
            Platforms.Android => "Android",
            Platforms.IPhone => "IPhone",
            (Platforms)112 => "Starlight", // 参考模组（BAU / EHR）约定的自定义平台
            _ => null,
        };
    }

    // 版本号：refdata 的 `userFacingVersion`（`19.0` → 显示 `v19.0`）优先；
    // 旧版游戏回落到 `Application.version`（当时形如 `v19s`，且不是日期时才可用）。
    private static string Version()
    {
        try
        {
            try
            {
                var rd = ReferenceDataManager.Instance?.Refdata;
                string v = rd?.userFacingVersion;
                if (!string.IsNullOrWhiteSpace(v))
                {
                    v = v.Trim();
                    return (v[0] == 'v' || v[0] == 'V') ? v : "v" + v;
                }
            }
            catch { }

            try
            {
                string v = Application.version;
                if (!string.IsNullOrWhiteSpace(v))
                {
                    v = v.Trim();
                    if (!LooksLikeDate(v)) return StripBuildSuffix(v);
                }
            }
            catch { }

            return null;
        }
        catch { return null; }
    }

    // 剥掉结尾的 build 后缀（`v19s` → `v19`、`v19e` → `v19`）
    private static string StripBuildSuffix(string v)
    {
        int end = v.Length;
        while (end > 1 && !char.IsDigit(v[end - 1])) end--;
        return end == 0 ? null : v.Substring(0, end);
    }

    // 日期：新版 `Application.version` 本身就是日期串；旧版回落 GetVersionComponents，
    // 且**必须通过合理性校验**，校验不过返回 null（绝不显示 `2404160.…` 这种垃圾）。
    private static string Date()
    {
        try
        {
            try
            {
                string v = Application.version;
                if (!string.IsNullOrWhiteSpace(v) && LooksLikeDate(v.Trim())) return v.Trim();
            }
            catch { }

            try
            {
                int broadcast = Constants.GetBroadcastVersion();
                var t = Constants.GetVersionComponents(broadcast);

                int year = t.Item1, month = t.Item2, day = t.Item3;
                if (year < 2000 || year > 2100) return null;
                if (month < 1 || month > 12) return null;
                if (day < 1 || day > 31) return null;

                return year.ToString(CultureInfo.InvariantCulture) + "."
                     + month.ToString(CultureInfo.InvariantCulture) + "."
                     + day.ToString(CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
        catch { return null; }
    }

    // `2026.9.29` / `2026.09.29` 这类 yyyy.m.d（游戏 v19 起用它当内部版本号）
    private static bool LooksLikeDate(string s)
    {
        try
        {
            string[] parts = s.Split('.');
            if (parts.Length != 3) return false;
            if (parts[0].Length != 4) return false;
            if (parts[1].Length < 1 || parts[1].Length > 2) return false;
            if (parts[2].Length < 1 || parts[2].Length > 2) return false;

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < parts[i].Length; j++)
                    if (!char.IsDigit(parts[i][j])) return false;

            int month = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int day = int.Parse(parts[2], CultureInfo.InvariantCulture);
            return month >= 1 && month <= 12 && day >= 1 && day <= 31;
        }
        catch { return false; }
    }
}

// 旧类型保留占位：右下角标识功能已按用户要求整体回退（详见文件头 ⑤ 的记录）。
internal static class AmethystVersionTag
{
    internal const string TagText = "Amethyst\u00AE2026";

    /// <summary>空实现：右下角标识已回退，不再绘制任何模组内容。</summary>
    internal static void Tick() { }
}
