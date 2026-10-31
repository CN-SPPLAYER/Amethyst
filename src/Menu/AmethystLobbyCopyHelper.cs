using System.Text;
using InnerNet;
using UnityEngine;
using AmongUs.GameOptions;
using Amethyst.Patches;

namespace Amethyst.Menu;

public static class AmethystLobbyCopyHelper
{
    public static void CopyRoomInfo()
    {
        if (AmongUsClient.Instance == null) return;

        var net = (InnerNetClient)AmongUsClient.Instance;
        int id = net.GameId;
        string code = GameCode.IntToGameName(id);

        if (string.IsNullOrEmpty(code))
        {
            AmethystToast.Push(AmethystText.T("LobbyCodeUnavailable"), 3.0f);
            return;
        }

        int cur = 0;
        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all != null) cur = all.Count;
        }
        catch { }

        int max = 0;
        GameModes mode = GameModes.Normal;
        GameKeywords langKeywords = GameKeywords.All;
        try
        {
            GameOptionsManager gom = GameOptionsManager.Instance;
            if (gom != null && gom.CurrentGameOptions != null)
            {
                max = gom.CurrentGameOptions.MaxPlayers;
                mode = gom.CurrentGameOptions.GameMode;
                langKeywords = gom.CurrentGameOptions.Keywords;
            }
        }
        catch { }

        string hostName = string.Empty;
        try
        {
            ClientData host = net.GetHost();
            if (host != null && !string.IsNullOrEmpty(host.PlayerName))
            {
                hostName = host.PlayerName;
            }
        }
        catch { }

        string serverName = string.Empty;
        try
        {
            serverName = AmethystServerInfo.RegionName();
        }
        catch { }

        string modeName = GetModeName(mode);
        string langName = GetLanguageName(langKeywords);

        var sb = new StringBuilder();
        sb.AppendLine($"{code}({cur}/{max})");
        sb.AppendLine($"{AmethystText.T("CopyRoomInfo_Mode")}:{modeName}");
        sb.AppendLine($"{AmethystText.T("CopyRoomInfo_Lang")}:{langName}");
        sb.AppendLine($"{AmethystText.T("CopyRoomInfo_Host")}:{hostName}");
        sb.Append($"{AmethystText.T("CopyRoomInfo_Server")}:{serverName}");

        string copyText = sb.ToString();
        try { GUIUtility.systemCopyBuffer = copyText; } catch { }

        AmethystToast.Push(AmethystText.T("CodeCopiedBB", code), 3.0f);
    }

    private static string GetModeName(GameModes mode)
    {
        bool hideSeek = mode is GameModes.HideNSeek or GameModes.SeekFools;
        return AmethystText.T(hideSeek ? "RoomModeHideNSeek" : "RoomModeNormal");
    }

    private static string GetLanguageName(GameKeywords kw)
    {
        
        if (kw == GameKeywords.All) return AmethystText.T("RoomLangAny");

        switch (kw)
        {
            case GameKeywords.English: return AmethystText.T("RoomLangEnglish");
            case GameKeywords.SpanishLA: return AmethystText.T("RoomLangSpanishLA");
            case GameKeywords.SpanishEU: return AmethystText.T("RoomLangSpanishEU");
            case GameKeywords.Brazilian: return AmethystText.T("RoomLangBrazilian");
            case GameKeywords.Portuguese: return AmethystText.T("RoomLangPortuguese");
            case GameKeywords.Korean: return AmethystText.T("RoomLangKorean");
            case GameKeywords.Russian: return AmethystText.T("RoomLangRussian");
            case GameKeywords.Dutch: return AmethystText.T("RoomLangDutch");
            case GameKeywords.Filipino: return AmethystText.T("RoomLangFilipino");
            case GameKeywords.French: return AmethystText.T("RoomLangFrench");
            case GameKeywords.German: return AmethystText.T("RoomLangGerman");
            case GameKeywords.Italian: return AmethystText.T("RoomLangItalian");
            case GameKeywords.Japanese: return AmethystText.T("RoomLangJapanese");
            case GameKeywords.Arabic: return AmethystText.T("RoomLangArabic");
            case GameKeywords.Polish: return AmethystText.T("RoomLangPolish");
            case GameKeywords.SChinese: return AmethystText.T("RoomLangSChinese");
            case GameKeywords.TChinese: return AmethystText.T("RoomLangTChinese");
            case GameKeywords.Irish: return AmethystText.T("RoomLangIrish");
            case GameKeywords.Other: return AmethystText.T("RoomLangOther");
        }

        if ((kw & GameKeywords.English) != 0) return AmethystText.T("RoomLangEnglish");
        if ((kw & GameKeywords.SpanishLA) != 0) return AmethystText.T("RoomLangSpanishLA");
        if ((kw & GameKeywords.Brazilian) != 0) return AmethystText.T("RoomLangBrazilian");
        if ((kw & GameKeywords.Portuguese) != 0) return AmethystText.T("RoomLangPortuguese");
        if ((kw & GameKeywords.Korean) != 0) return AmethystText.T("RoomLangKorean");
        if ((kw & GameKeywords.Russian) != 0) return AmethystText.T("RoomLangRussian");
        if ((kw & GameKeywords.Dutch) != 0) return AmethystText.T("RoomLangDutch");
        if ((kw & GameKeywords.Filipino) != 0) return AmethystText.T("RoomLangFilipino");
        if ((kw & GameKeywords.French) != 0) return AmethystText.T("RoomLangFrench");
        if ((kw & GameKeywords.German) != 0) return AmethystText.T("RoomLangGerman");
        if ((kw & GameKeywords.Italian) != 0) return AmethystText.T("RoomLangItalian");
        if ((kw & GameKeywords.Japanese) != 0) return AmethystText.T("RoomLangJapanese");
        if ((kw & GameKeywords.Arabic) != 0) return AmethystText.T("RoomLangArabic");
        if ((kw & GameKeywords.Polish) != 0) return AmethystText.T("RoomLangPolish");
        if ((kw & GameKeywords.SChinese) != 0) return AmethystText.T("RoomLangSChinese");
        if ((kw & GameKeywords.TChinese) != 0) return AmethystText.T("RoomLangTChinese");
        if ((kw & GameKeywords.Irish) != 0) return AmethystText.T("RoomLangIrish");
        if ((kw & GameKeywords.SpanishEU) != 0) return AmethystText.T("RoomLangSpanishEU");

        return AmethystText.T("RoomLangOther");
    }
}
