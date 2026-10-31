using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using UnityEngine;
using UnityEngine.Networking;
using Amethyst.Patches;

namespace Amethyst;

public sealed partial class AmethystRoomService
{

    private static string HostName(InnerNetClient net)
    {
        try
        {
            ClientData h = net.GetHost();
            if (h != null && !string.IsNullOrEmpty(h.PlayerName)) return h.PlayerName;
        }
        catch { }
        try
        {
            PlayerControl lp = PlayerControl.LocalPlayer;
            if (lp != null && lp.Data != null) return lp.Data.PlayerName ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }


    private static int PlayerCount()
    {
        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all != null) return all.Count;
        }
        catch { }
        return 0;
    }


    private static IGameOptions Options()
    {
        try
        {
            GameOptionsManager gom = GameOptionsManager.Instance;
            if (gom == null) return null;
            return gom.CurrentGameOptions;
        }
        catch { return null; }
    }


    private static int MaxPlayers()
    {
        try
        {
            IGameOptions o = Options();
            if (o != null) return o.MaxPlayers;
        }
        catch { }
        return 0;
    }


    private static string MapName()
    {
        try
        {
            IGameOptions o = Options();
            if (o == null) return string.Empty;
            switch ((int)o.MapId)
            {
                case 0: return "Skeld";
                case 1: return "Mira";
                case 2: return "Polus";
                case 3: return "Skeld";   
                case 4: return "Airship";
                case 5: return "Fungle";
                default: return string.Empty;
            }
        }
        catch { return string.Empty; }
    }


    private static string ModeName()
    {
        try
        {
            IGameOptions o = Options();
            if (o == null) return "classic";
            return (o.GameMode == GameModes.HideNSeek || o.GameMode == GameModes.SeekFools) ? "hidenseek" : "classic";
        }
        catch { return "classic"; }
    }


    private static string RegionName()
    {
        try
        {
            string r = AmethystServerInfo.RegionName();
            if (string.IsNullOrEmpty(r)) return string.Empty;
            switch (r)
            {
                case "North America":
                case "NA": return "NorthAmerica";
                case "South America":
                case "SA": return "SouthAmerica";
                case "Europe":
                case "EU": return "Europe";
                case "Asia":
                case "AS": return "Asia";
                case "Oceania":
                case "OC": return "Oceania";
                case "Africa":
                case "AF": return "Africa";
                default: return r;
            }
        }
        catch { return string.Empty; }
    }


    private static int PingOf(InnerNetClient net)
    {
        try { return net.Ping; } catch { return 0; }
    }


    private static string LanguageNote()
    {
        string code = LanguageCode();
        return string.IsNullOrEmpty(code) ? string.Empty : "lang=" + code;
    }


    private static string LanguageCode()
    {
        try
        {
            IGameOptions o = Options();
            if (o == null) return string.Empty;

            uint k = (uint)o.Keywords;
            if ((k & (uint)GameKeywords.SChinese) != 0) return "zh";
            if ((k & (uint)GameKeywords.TChinese) != 0) return "zh";
            if ((k & (uint)GameKeywords.English) != 0) return "en";
            if ((k & (uint)GameKeywords.Russian) != 0) return "ru";
            if ((k & (uint)GameKeywords.Japanese) != 0) return "ja";
            if ((k & (uint)GameKeywords.Korean) != 0) return "ko";
            if (k == 0) return HostLanguage();
            return "other";
        }
        catch { return string.Empty; }
    }


    private static string HostLanguage()
    {
        try
        {
            string lang = AmethystConfig.Language != null ? AmethystConfig.Language.Value : string.Empty;
            if (lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh";
            if (lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase)) return "ru";
            if (lang.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en";
            return "other";
        }
        catch { return string.Empty; }
    }
}
