using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static class AmethystColors
{
    
    private static readonly Dictionary<int, string> HexCache = new Dictionary<int, string>(32);
    private static readonly Dictionary<int, NameEntry> ColorNameCache = new Dictionary<int, NameEntry>(32);

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

    internal static bool SamePlayer(PlayerControl a, PlayerControl b)
    {
        if (a == null || b == null) return false;
        try { return a.PlayerId == b.PlayerId; } catch { }
        try { return a == b; } catch { return false; }
    }

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
