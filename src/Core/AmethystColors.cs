using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Amethyst;

// 供通知使用的名字着色工具：把玩家名包成 <color=#RRGGBB>名字</color>。
// 仅用于“触发反作弊 / 进房 / 离开”等提示，不改变游戏内名字本身。
internal static class AmethystColors
{
    // ⚠ 性能（第 162 轮）：`Hex` / `ColoredColorName` 会被 **每帧 × 每名玩家** 调用
    //   （Core\AmethystNameRendering.RenderOverheadNames、
    //     Core\AmethystColorBlindText.LateUpdate、CosmeticsLayer.GetColorBlindText 补丁、
    //     Patches\AmethystNameColors 的 MeetingHud.Update 补丁…），
    //   原来每次都 string.Format + 插值 + ToUpper/Substring/ToLower —— 这是游戏内最主要的 GC 来源。
    //
    //   `Hex` 的结果**只由 colorId 决定**（调色板在运行时固定，且与语言无关），
    //   所以直接按 colorId 缓存；颜色数量固定，缓存天然有界（≤ Palette.PlayerColors 长度）。
    //   只缓存成功结果：解析失败（例如启动早期 Palette 还没就绪）不会写进缓存，下次仍会重试。
    private static readonly Dictionary<int, string> HexCache = new Dictionary<int, string>(32);
    private static readonly Dictionary<int, NameEntry> ColorNameCache = new Dictionary<int, NameEntry>(32);

    // 色盲颜色名的缓存条目：除了拼好的结果，还留着“当时用的原始名字”用于校验。
    private sealed class NameEntry
    {
        internal string Raw;
        internal string Result;
    }

    internal static string Hex(int colorId)
    {
        if (colorId < 0) return null;
        if (HexCache.TryGetValue(colorId, out string hit)) return hit;

        string made = null;
        try
        {
            var arr = Palette.PlayerColors;
            if (arr != null && colorId < arr.Length)
            {
                Color32 c = arr[colorId];
                made = string.Format("#{0:X2}{1:X2}{2:X2}", c.r, c.g, c.b);
            }
        }
        catch { }

        if (made != null) HexCache[colorId] = made;
        return made;
    }

    internal static string Name(string name, int colorId)
    {
        if (string.IsNullOrEmpty(name)) return name;
        string hex = Hex(colorId);
        return hex == null ? name : $"<color={hex}>{name}</color>";
    }

    // 颜色 id -> 实际 Color（供聊天气泡等用顶点色着色的场景）。
    internal static Color ColorOf(int colorId)
    {
        try
        {
            var arr = Palette.PlayerColors;
            if (arr != null && colorId >= 0 && colorId < arr.Length)
            {
                Color32 c = arr[colorId];
                return new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
            }
        }
        catch { }
        return Color.white;
    }

    // 「是不是同一个人」：**优先用 PlayerId 比较**。
    //
    // 为什么不直接用 `==` / `!=`：IL2CPP 的包装对象在不同调用路径下未必是同一个托管实例，
    // 一旦 `!=` 把"自己"误判成"不是自己"，本地玩家就会被套上「内鬼队友红」——
    // 表现就是用户反馈的「我变形之后名字变成纯红了」（实测像素 #FF0000）。
    internal static bool SamePlayer(PlayerControl a, PlayerControl b)
    {
        if (a == null || b == null) return false;
        try { return a.PlayerId == b.PlayerId; } catch { }
        try { return a == b; } catch { return false; }
    }

    // 显示色：自己与他人按各自颜色；本地为伪装者时队友显示为红色。
    internal static Color DisplayColor(PlayerControl pc)
    {
        try
        {
            if (pc == null || pc.Data == null) return Color.white;
            int id = pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local != null && !SamePlayer(local, pc) && local.Data != null && local.Data.Role != null)
            {
                if (Patches.AmethystRpcGuard.IsImp(local) && Patches.AmethystRpcGuard.IsImp(pc)) return Color.red;
            }
            return ColorOf(id);
        }
        catch { return Color.white; }
    }

    // 颜色名称（色盲文字）：首字母大写，并按该颜色着色
    //
    // ⚠ 性能（第 162 轮）：这个函数同样是“每帧 × 每名玩家”级别，原来每次要拼 4~6 个字符串。
    //   这里仍然**照常调用 `Palette.GetColorName`**（它是语言相关的，必须每次取最新值，
    //   这样中途切换语言不会读到过期结果），只在“原始名字和上次一样”时复用上一次拼好的结果 ——
    //   于是每帧只剩游戏自己那一次取名分配，我们这边的 ToUpper/Substring/ToLower/Format/插值全部省掉。
    //   返回值语义与原来完全一致（空名字 / 异常一律返回 string.Empty）。
    internal static string ColoredColorName(int colorId)
    {
        if (colorId < 0) return string.Empty;
        try
        {
            string n = Palette.GetColorName(colorId);
            if (string.IsNullOrEmpty(n)) return string.Empty;

            if (ColorNameCache.TryGetValue(colorId, out NameEntry cached) && cached != null
                && string.Equals(cached.Raw, n))
                return cached.Result;

            string cap = char.ToUpperInvariant(n[0]) + (n.Length > 1 ? n.Substring(1).ToLowerInvariant() : string.Empty);
            string hex = Hex(colorId);
            string made = hex == null ? cap : $"<color={hex}>{cap}</color>";

            ColorNameCache[colorId] = new NameEntry { Raw = n, Result = made };
            return made;
        }
        catch { return string.Empty; }
    }

    internal static int ColorId(ClientData c)
    {
        try
        {
            if (c == null) return 0;
            if (c.Character != null && c.Character.Data != null && c.Character.Data.DefaultOutfit != null)
                return c.Character.Data.DefaultOutfit.ColorId;
            return c.ColorId;
        }
        catch { return 0; }
    }

    internal static string Name(ClientData c)
    {
        if (c == null) return null;
        string name = !string.IsNullOrWhiteSpace(c.PlayerName) ? c.PlayerName.Trim() : ("#" + c.Id);
        return Name(name, ColorId(c));
    }

    internal static string Name(InnerNetClient net, int clientId)
    {
        try { return Name(AmethystAccess.FindClient(net, clientId)); }
        catch { return null; }
    }

    internal static bool NameColorsOn => AmethystConfig.NameColors != null && AmethystConfig.NameColors.Value;

    // 界面优化用：自己与他人名字按颜色渲染；若本地玩家是内鬼，则内鬼队友名字显示为红色。
    internal static string DisplayName(PlayerControl pc)
    {
        try
        {
            if (pc == null || pc.Data == null) return null;
            string name = pc.Data.PlayerName;
            if (!NameColorsOn || string.IsNullOrEmpty(name)) return name;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local != null && !SamePlayer(local, pc) && local.Data != null && local.Data.Role != null)
            {
                bool localImp = Amethyst.Patches.AmethystRpcGuard.IsImp(local);
                bool targetImp = Amethyst.Patches.AmethystRpcGuard.IsImp(pc);
                if (localImp && targetImp) return $"<color=#FF0000>{name}</color>";
            }

            int id = pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
            string hex = Hex(id);
            return hex == null ? name : $"<color={hex}>{name}</color>";
        }
        catch { return pc != null && pc.Data != null ? pc.Data.PlayerName : null; }
    }

    internal static string Name(PlayerControl pc)
    {
        try
        {
            if (pc == null || pc.Data == null) return null;
            int id = pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
            return Name(pc.Data.PlayerName, id);
        }
        catch { return pc != null && pc.Data != null ? pc.Data.PlayerName : null; }
    }
}
