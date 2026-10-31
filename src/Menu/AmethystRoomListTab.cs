using System;
using UnityEngine;

namespace Amethyst;

public sealed partial class AmethystMenu
{
    private GUIStyle _roomCode;
    private GUIStyle _roomTag;
    private GUIStyle _roomHost;
    private GUIStyle _roomMeta;
    private bool _roomStyles;
    private string _roomConfirm = string.Empty;

    private string _filterMap = string.Empty;
    private string _filterRegion = string.Empty;
    private string _filterLang = string.Empty;

    private static readonly string[] MapFilters = { "", "Skeld", "Mira", "Polus", "Airship", "Fungle", "Submerged" };
    private static readonly string[] RegionFilters = { "", "Asia", "Europe", "NorthAmerica", "SouthAmerica", "Oceania", "Africa" };
    private static readonly string[] LangFilters = { "", "zh", "en", "ru", "ja", "ko", "other" };

    private const int RoomsTab = 4;

    private void SyncRoomListVisibility()
    {
        AmethystRoomService svc = AmethystRoomService.Instance;
        if (svc == null) return;

        bool visible = _open && _closeAt < 0f && !_collapsed && _tab == RoomsTab;
        svc.SetVisible(visible);
    }

    private void EnsureRoomStyles()
    {
        if (_roomStyles) return;
        AmethystPalette p = AmethystStyle.Current;
        _roomCode = Label(p.Accent, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
        _roomTag = Label(p.Muted, 11, FontStyle.Normal, TextAnchor.MiddleCenter);
        _roomHost = Label(p.Text, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
        _roomMeta = Label(p.Muted, 12, FontStyle.Normal, TextAnchor.MiddleRight);
        _roomCode.wordWrap = false;
        _roomTag.wordWrap = false;
        _roomHost.wordWrap = false;
        _roomMeta.wordWrap = false;
        _roomStyles = true;
    }

    private void DrawModRooms(float x, ref float y, float w)
    {
        Grp(-1);
        EnsureRoomStyles();

        AmethystPalette p = AmethystStyle.Current;
        AmethystRoomService svc = AmethystRoomService.Instance;

        if (svc == null)
        {
            DrawEmpty(new Rect(x, y, w, 190f), AmethystIcon.Rooms, AmethystText.T("RoomListUnavailable"));
            y += 210f;
            return;
        }

        if (AmethystConfig.RoomListEnabled != null && !AmethystConfig.RoomListEnabled.Value)
        {
            DrawEmpty(new Rect(x, y, w, 190f), AmethystIcon.Rooms, AmethystText.T("RoomListDisabled"));
            y += 210f;
            return;
        }

        if (Event.current != null && Event.current.type == EventType.Repaint)
            svc.EnsureFresh();

        var head = new Rect(x, y, w, 40f);
        AmethystStyle.FillRounded(head, A(p.Accent, 0.06f), 10);
        AmethystStyle.StrokeRounded(head, A(p.Accent, 0.18f), 10, 1);

        string status = svc.Loading
            ? AmethystText.T("RoomListLoading")
            : (svc.Failed && svc.Loaded
                ? AmethystText.T("RoomListFailed")
                : AmethystText.T("RoomListOnline", Math.Max(svc.OnlineCount, svc.Rooms.Count)));
        GUI.Label(new Rect(head.x + 12f, head.y, head.width - 210f, head.height), status, _rowInfo);

        int refresh = 8;
        try { refresh = Mathf.Clamp(AmethystConfig.RoomListRefreshSeconds.Value, 4, 60); } catch { }
        GUI.Label(new Rect(head.xMax - 196f, head.y, 96f, head.height),
            AmethystText.T("RoomListAuto", refresh), _roomMeta);

        if (SmallButton(new Rect(head.xMax - 94f, head.y + 8f, 82f, 24f),
                AmethystText.T("RoomListRefresh"), p.Accent))
        {
            _roomConfirm = string.Empty;
            svc.Refresh();
        }
        y += 50f;

        Toggle(x, ref y, w, AmethystText.T("RoomListReportOwnRoom"), AmethystConfig.RoomListReportOwnRoom);
        y += 4f;

        if (!string.IsNullOrEmpty(_roomConfirm))
        {
            var bar = new Rect(x, y, w, 52f);
            AmethystStyle.FillRounded(bar, new Color(1f, 0.72f, 0.35f, 0.10f), 9);
            AmethystStyle.StrokeRounded(bar, new Color(1f, 0.72f, 0.35f, 0.32f), 9, 1);
            GUI.Label(new Rect(bar.x + 12f, bar.y + 2f, bar.width - 24f, 22f),
                AmethystText.T("RoomListConfirmHint", _roomConfirm), _roomHost);

            if (SmallButton(new Rect(bar.xMax - 176f, bar.y + 24f, 76f, 22f),
                    AmethystText.T("RoomListCancel"), p.Muted))
                _roomConfirm = string.Empty;

            if (SmallButton(new Rect(bar.xMax - 94f, bar.y + 24f, 82f, 22f),
                    AmethystText.T("RoomListConfirmShort"), new Color(1f, 0.72f, 0.35f)))
            {
                string target = _roomConfirm;
                _roomConfirm = string.Empty;
                svc.Join(target);
                RequestClose();
            }

            y += 60f;
        }

        {
            float fx = x;
            float fy = y;
            fx = FilterChip(fx, fy, 104f, AmethystText.T("RoomListFilterMap"), MapFilters, ref _filterMap, MapLabel);
            fx = FilterChip(fx, fy, 122f, AmethystText.T("RoomListFilterRegion"), RegionFilters, ref _filterRegion, RegionLabel);
            FilterChip(fx, fy, 104f, AmethystText.T("RoomListFilterLang"), LangFilters, ref _filterLang, LangLabel);
            y += 32f;
        }

        if (!svc.Loaded && !svc.Failed)
        {
            DrawEmpty(new Rect(x, y, w, 190f), AmethystIcon.Rooms, AmethystText.T("RoomListLoading"));
            y += 210f;
            return;
        }

        if (svc.Failed && !svc.Loaded)
        {
            DrawEmpty(new Rect(x, y, w, 190f), AmethystIcon.Rooms, AmethystText.T("RoomListFailed"));
            if (SmallButton(new Rect(x + w * 0.5f - 46f, y + 150f, 92f, 26f),
                    AmethystText.T("RoomListRetry"), p.Accent))
                svc.Refresh();
            y += 210f;
            return;
        }

        if (svc.Rooms.Count == 0)
        {
            DrawEmpty(new Rect(x, y, w, 190f), AmethystIcon.Rooms, AmethystText.T("RoomListEmpty"));
            y += 210f;
            return;
        }

        int shownCount = 0;
        for (int i = 0; i < svc.Rooms.Count; i++)
            if (svc.Rooms[i] != null && MatchesFilters(svc.Rooms[i])) shownCount++;

        if (shownCount == 0)
        {
            DrawEmpty(new Rect(x, y, w, 190f), AmethystIcon.Rooms, AmethystText.T("RoomListNoMatch"));
            if (SmallButton(new Rect(x + w * 0.5f - 58f, y + 150f, 116f, 26f),
                    AmethystText.T("RoomListClearFilters"), p.Accent))
                ClearFilters();
            y += 210f;
            return;
        }

        for (int i = 0; i < svc.Rooms.Count; i++)
        {
            AmethystRoom room = svc.Rooms[i];
            if (room == null || !MatchesFilters(room)) continue;

            const float rowH = 62f;
            float e = Ent(0.02f + Mathf.Min(i, 8) * 0.02f, 0.25f);
            Color keepRow = GUI.color;
            GUI.color = new Color(keepRow.r, keepRow.g, keepRow.b, keepRow.a * e);
            if (!Cull(y, rowH))
                DrawRoomRow(new Rect(x, y, w, rowH), room, svc, p);
            GUI.color = keepRow;

            y += rowH + 8f;
        }
    }

    private void DrawRoomRow(Rect r, AmethystRoom room, AmethystRoomService svc, AmethystPalette p)
    {
        bool hover = r.Contains(M);
        bool pending = string.Equals(_roomConfirm, room.Code, StringComparison.Ordinal);
        _roomHost.normal.textColor = p.Text;
        _roomMeta.normal.textColor = p.Muted;
        AmethystStyle.FillRounded(r, p.Panel, 12);
        if (hover) AmethystStyle.FillRounded(r, A(Color.white, 0.04f), 12);
        AmethystStyle.StrokeRounded(r,
            pending ? new Color(1f, 0.72f, 0.35f, 0.72f) : (hover ? A(p.Accent, 0.30f) : A(Color.white, 0.07f)), 12, 1);

        const float joinW = 88f;
        float textW = r.width - joinW - 40f;

        _roomCode.normal.textColor = p.Accent;
        GUI.Label(new Rect(r.x + 14f, r.y + 5f, 104f, 24f), room.Code, _roomCode);

        float tagX = r.x + 122f;
        Color stateCol = room.InGame ? new Color(1f, 0.72f, 0.35f) : new Color(0.45f, 0.9f, 0.62f);
        RoomTag(new Rect(tagX, r.y + 8f, 56f, 20f),
            AmethystText.T(room.InGame ? "RoomListStateIngame" : "RoomListStateLobby"), stateCol);
        tagX += 62f;

        if (textW > 300f)
        {
            if (!string.IsNullOrEmpty(room.Map))
            {
                RoomTag(new Rect(tagX, r.y + 8f, 64f, 20f), room.Map, p.Muted);
                tagX += 70f;
            }

            if (!string.IsNullOrEmpty(room.Mode))
            {
                RoomTag(new Rect(tagX, r.y + 8f, 64f, 20f),
                    AmethystText.T(room.Mode == "hidenseek" ? "RoomListModeHideSeek" : "RoomListModeClassic"), p.Muted);
            }
        }

        string host = string.IsNullOrEmpty(room.Host) ? AmethystText.T("RoomListHostUnknown") : room.Host;
        GUI.Label(new Rect(r.x + 14f, r.y + 34f, textW * 0.55f, 20f), host, _roomHost);

        string ping = room.Ping > 0 ? room.Ping + "ms" : "--";
        string max = room.MaxPlayers > 0 ? room.MaxPlayers.ToString() : "--";
        string meta = AmethystText.T("RoomListMeta", room.Players, max, ping, RoomAge(room.Age));
        GUI.Label(new Rect(r.x + 14f + textW * 0.55f, r.y + 34f, textW * 0.45f, 20f), meta, _roomMeta);

        float bx = r.xMax - joinW - 12f;
        float by = r.y + 15f;

        if (svc.Joining && string.Equals(svc.JoiningCode, room.Code, StringComparison.Ordinal))
        {
            AmethystStyle.FillRounded(new Rect(bx, by, joinW, 32f), A(p.Accent, 0.16f), 8);
            _roomTag.normal.textColor = p.Accent;
            GUI.Label(new Rect(bx, by, joinW, 32f), AmethystText.T("RoomListJoining"), _roomTag);
            return;
        }

        bool joinable = !room.InGame && !room.IsFull;
        if (!joinable)
        {
            AmethystStyle.FillRounded(new Rect(bx, by, joinW, 32f), A(p.Muted, 0.10f), 8);
            _roomTag.normal.textColor = A(p.Muted, 0.85f);
            GUI.Label(new Rect(bx, by, joinW, 32f),
                AmethystText.T(room.IsFull ? "RoomListFull" : "RoomListInGame"), _roomTag);
            return;
        }

        if (SmallButton(new Rect(bx, by, joinW, 32f), AmethystText.T("RoomListJoin"), p.Accent))
            _roomConfirm = room.Code;
    }

    private void RoomTag(Rect r, string text, Color col)
    {
        AmethystStyle.FillRounded(r, A(col, 0.12f), 6);
        AmethystStyle.StrokeRounded(r, A(col, 0.30f), 6, 1);
        _roomTag.normal.textColor = col;
        GUI.Label(r, text, _roomTag);
    }

    private static string RoomAge(int age)
    {
        if (age < 5) return AmethystText.T("RoomListAgeNow");
        if (age < 60) return AmethystText.T("RoomListAgeSec", age);
        int m = age / 60;
        if (m < 60) return AmethystText.T("RoomListAgeMin", m);
        return AmethystText.T("RoomListAgeHour", m / 60);
    }

    private float FilterChip(float x, float y, float w, string title, string[] values, ref string value, Func<string, string> label)
    {
        string text = title + ": " + label(value);
        bool active = !string.IsNullOrEmpty(value);
        if (SmallButton(new Rect(x, y, w, 24f), text, active ? AmethystStyle.Current.Accent : AmethystStyle.Current.Muted))
        {
            int i = Array.IndexOf(values, value);
            if (i < 0) i = 0;
            value = values[(i + 1) % values.Length];
            _roomConfirm = string.Empty;
        }
        return x + w + 6f;
    }

    private static string MapLabel(string v)
    {
        return string.IsNullOrEmpty(v) ? AmethystText.T("RoomListAll") : v;
    }

    private static string RegionLabel(string v)
    {
        switch (v)
        {
            case "Asia": return AmethystText.T("RoomListRegionAsia");
            case "Europe": return AmethystText.T("RoomListRegionEurope");
            case "NorthAmerica": return AmethystText.T("RoomListRegionNA");
            case "SouthAmerica": return AmethystText.T("RoomListRegionSA");
            case "Oceania": return AmethystText.T("RoomListRegionOceania");
            case "Africa": return AmethystText.T("RoomListRegionAfrica");
            default: return AmethystText.T("RoomListAll");
        }
    }

    private static string LangLabel(string v)
    {
        switch (v)
        {
            case "zh": return AmethystText.T("RoomListLangZh");
            case "en": return AmethystText.T("RoomListLangEn");
            case "ru": return AmethystText.T("RoomListLangRu");
            case "ja": return AmethystText.T("RoomListLangJa");
            case "ko": return AmethystText.T("RoomListLangKo");
            case "other": return AmethystText.T("RoomListLangOther");
            default: return AmethystText.T("RoomListAll");
        }
    }

    private void ClearFilters()
    {
        _filterMap = string.Empty;
        _filterRegion = string.Empty;
        _filterLang = string.Empty;
    }

    private bool MatchesFilters(AmethystRoom r)
    {
        if (!string.IsNullOrEmpty(_filterMap) &&
            !string.Equals(NormMap(r.Map), _filterMap, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrEmpty(_filterRegion) &&
            !string.Equals(NormRegion(r.Region), _filterRegion, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrEmpty(_filterLang) &&
            !string.Equals(RoomLang(r), _filterLang, StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    private static string NormMap(string map)
    {
        if (string.IsNullOrEmpty(map)) return string.Empty;
        if (map.Equals("MiraHQ", StringComparison.OrdinalIgnoreCase)) return "Mira";
        if (map.Equals("Dleks", StringComparison.OrdinalIgnoreCase)) return "Skeld";
        return map;
    }

    private static string NormRegion(string region)
    {
        if (string.IsNullOrEmpty(region)) return string.Empty;
        return region.Replace(" ", string.Empty).Replace("-", string.Empty);
    }

    private static string RoomLang(AmethystRoom r)
    {
        string note = r.Note ?? string.Empty;
        int i = note.IndexOf("lang=", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return string.Empty;
        int j = i + 5;
        int k = j;
        while (k < note.Length && char.IsLetterOrDigit(note[k])) k++;
        return note.Substring(j, k - j).ToLowerInvariant();
    }
}
