using System.Collections.Generic;
using AmongUs.GameOptions;
using BepInEx.Configuration;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystBetterPingState
{
    internal static int CurrentFps = 60;

    // 覆盖排序用的极高 sortingOrder —— 让这行字压在其它 UI 之上
    private const int TopSortingOrder = 32000;

    private static int _frames;
    private static float _accum;
    private static float _pingAt;
    private static int _ping = -1;
    private static string _lastText;

    // ⚠ 性能（第 162 轮）：这行字挂在 `PingTracker.Update` 的 Prefix 上（返回 false，完全接管原版），
    //   **每帧**都会跑。原来每帧都要 new List<string> + 两次插值 + string.Join，
    //   然后再拿去和 _lastText 比较 —— 比较发生在构建之后，等于每帧白分配一堆字符串。
    //   现在先比"会变的东西"（ping / fps / 语言 / 屏幕上当前显示的是不是我们写的那串），
    //   全都没变就直接返回，一个字符串都不拼。
    private static int _lastPing = int.MinValue;
    private static int _lastFps = int.MinValue;
    private static string _lastLang;
    private static int _lastMask = int.MinValue;
    private static string _lastServer;
    private static string _lastRoom;
    private static string _lastHost;

    private static int _aspectId;
    private static Vector3 _edge;

    internal static void Tick()
    {
        _frames++;
        _accum += Mathf.Max(Time.deltaTime, 0f);
        if (_accum >= 0.25f)
        {
            CurrentFps = Mathf.Clamp(Mathf.RoundToInt(_frames / Mathf.Max(_accum, 0.0001f)), 1, 999);
            _frames = 0;
            _accum = 0f;
        }
    }

    [HideFromIl2Cpp]
    internal static void Render(PingTracker tracker)
    {
        if (tracker == null || tracker.text == null) return;
        try
        {
            EnsureTopLayer(tracker);

            AspectPosition ap = tracker.GetComponent<AspectPosition>();
            if (ap != null)
            {
                int id = ap.GetInstanceID();
                if (id != _aspectId) { _aspectId = id; _edge = ap.DistanceFromEdge; }
                Vector3 want = _edge;
                // ⚠ 方向说明（这里踩过一次坑，别再搞反）：
                // 该显示锚在**底边**，`DistanceFromEdge.y` 是**离底边的距离** ——
                // 所以乘数**越大越靠上**、越小越靠下。
                // 上一轮把它减小到 0.55 想要「上移」，结果反而是下移；这里改成放大。
                want.y = _edge.y * 0.75f;
                want.x = _edge.x + 0.12f;
                if ((ap.DistanceFromEdge - want).sqrMagnitude > 1e-6f)
                {
                    ap.DistanceFromEdge = want;
                    ap.AdjustPosition();
                }
            }

            if (Time.unscaledTime - _pingAt >= 0.1f)
            {
                _pingAt = Time.unscaledTime;
                _ping = AmongUsClient.Instance == null ? -1 : ((InnerNetClient)AmongUsClient.Instance).Ping;
            }

            int fps = CurrentFps;
            TMP_Text target = tracker.text;

            // 语言会影响下面两个标签的文案，所以它也参与"变没变"的判断。
            string lang = null;
            try { if (AmethystConfig.Language != null) lang = AmethystConfig.Language.Value; } catch { }

            bool showLatency = On(AmethystConfig.PingShowLatency);
            bool showFps = On(AmethystConfig.PingShowFps);
            bool showServer = On(AmethystConfig.PingShowServer);
            bool showRoom = On(AmethystConfig.PingShowRoom);
            bool showHost = On(AmethystConfig.PingShowHost);

            string room = showRoom ? RoomInfo() : string.Empty;
            string host = showHost ? HostName() : string.Empty;            int mask = (showLatency ? 1 : 0) | (showFps ? 2 : 0) | (showServer ? 4 : 0) | (showRoom ? 8 : 0) | (showHost ? 16 : 0);

            string server = showServer ? AmethystServerInfo.RegionName() : string.Empty;

            // 会变的东西全都没变、而且屏幕上显示的正是我们上次写的那串 → 直接返回。
            if (_ping == _lastPing
                && fps == _lastFps
                && string.Equals(lang, _lastLang)
                && mask == _lastMask
                && string.Equals(server, _lastServer)
                && string.Equals(room, _lastRoom)
                && string.Equals(host, _lastHost)
                && string.Equals(target.text, _lastText))
                return;

            string ping = _ping < 0 ? "--" : _ping.ToString();
            string pc = _ping < 0 ? "9AA7B4" : _ping < 80 ? "7FEC9A" : _ping < 160 ? "EDE87A" : "F07070";
            string line1 = $"<color=#607888>{AmethystText.T("PingLatency")}:</color><mspace=0.66em><b><color=#{pc}>{ping}</color></b></mspace><color=#607888>ms</color>";
            string fc = fps >= 55 ? "7FEC9A" : fps >= 30 ? "EDE87A" : fps >= 20 ? "F5A84A" : "F07070";
            string line2 = $"<color=#607888>{AmethystText.T("PingFps")}:</color><mspace=0.66em><b><color=#{fc}>{fps}</color></b></mspace><color=#607888>fps</color>";

            // 服务器 / 区域（参考 EHR Utils.GetRegionName）。
            // ⚠ 不要套 `<mspace=0.66em>`：文本值（中文/字母）会与等宽格重叠。
            string region = server;
            string line3 = string.IsNullOrEmpty(region)
                ? string.Empty
                : $"<color=#607888>{AmethystText.T("PingServer")}:</color><b><color=#9FD3FF>{region}</color></b>";

            // 房间号（主题色）+（当前人数/总人数，按人数着色）；本地环境显示「本地」。
            string line4 = string.IsNullOrEmpty(room)
                ? string.Empty
                : $"<color=#607888>{AmethystText.T("PingRoom")}:</color>{room}";

            // 房主名字（按房主本人颜色着色）
            string line5 = string.IsNullOrEmpty(host)
                ? string.Empty
                : $"<color=#607888>{AmethystText.T("PingHost")}:</color>{host}";

            string text = "<size=82%>" + Join(line1, line2, line3, line4, line5, showLatency, showFps) + "</size>";

            _lastPing = _ping;
            _lastFps = fps;
            _lastLang = lang;
            _lastMask = mask;
            _lastServer = server;
            _lastRoom = room;
            _lastHost = host;
            if (text == _lastText) return;
            _lastText = text;

            target.richText = true;
            target.enableWordWrapping = false;
            target.alignment = TextAlignmentOptions.Center;
            target.overflowMode = TextOverflowModes.Overflow;
            target.text = text;
        }
        catch { }
    }

    private static bool On(ConfigEntry<bool> entry)
    {
        try { return entry == null || entry.Value; } catch { return false; }
    }

    private const string Sep = "   <color=#3B4E5C>|</color>   ";

    // 按启用情况拼装各项，未启用的直接不出现。
    private static string Join(string l1, string l2, string l3, string l4, string l5, bool showLatency, bool showFps)
    {
        var parts = new List<string>(5);
        if (showLatency) parts.Add(l1);
        if (showFps) parts.Add(l2);
        if (!string.IsNullOrEmpty(l3)) parts.Add(l3);
        if (!string.IsNullOrEmpty(l4)) parts.Add(l4);
        if (!string.IsNullOrEmpty(l5)) parts.Add(l5);
        return string.Join(Sep, parts);
    }

    // 房间：本地环境显示「本地」；线上显示 房号（主题色）+（当前人数/总人数）。
    // 人数按人数着色：当前人数 > 1 绿色，<= 1 红色。
    private static string RoomInfo()
    {
        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            if (net == null) return string.Empty;

            string accent = ColorUtility.ToHtmlStringRGB(AmethystStyle.Current.Accent);

            // 本地环境：直接显示「本地」，不显示问号房号。
            if (net.NetworkMode != NetworkModes.OnlineGame)
                return $"<b><color=#{accent}>{AmethystText.T("PingLocalRoom")}</color></b>";

            string code = string.Empty;
            try { code = GameCode.IntToGameName(((InnerNetClient)net).GameId); } catch { }
            if (string.IsNullOrEmpty(code)) code = "?";

            int cur = 0;
            try { var all = PlayerControl.AllPlayerControls; if (all != null) cur = all.Count; } catch { }

            int max = 0;
            try
            {
                GameOptionsManager gom = GameOptionsManager.Instance;
                if (gom != null && gom.CurrentGameOptions != null) max = gom.CurrentGameOptions.MaxPlayers;
            }
            catch { }

            string ratioColor = cur > 1 ? "#7FEC9A" : "#F07070";
            return $"<b><color=#{accent}>{code}</color></b> <b><color={ratioColor}>({cur}/{max})</color></b>";
        }
        catch { return string.Empty; }
    }

    // 房主名字：按房主本人的颜色着色。
    private static string HostName()
    {
        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            if (net == null) return string.Empty;
            ClientData host = ((InnerNetClient)net).GetHost();
            if (host == null) return string.Empty;

            string name = host.PlayerName;
            if (string.IsNullOrEmpty(name)) return string.Empty;

            int colorId = -1;
            try
            {
                if (host.Character != null && host.Character.Data != null && host.Character.Data.DefaultOutfit != null)
                    colorId = host.Character.Data.DefaultOutfit.ColorId;
            }
            catch { }

            string hex = colorId >= 0 ? AmethystColors.Hex(colorId) : null;
            if (string.IsNullOrEmpty(hex)) hex = "#9FD3FF";
            return $"<b><color={hex}>{name}</color></b>";
        }
        catch { return string.Empty; }
    }

    // 把延迟/帧率这行字提到**最顶层**，不被任何 UI 遮住。
    //
    // 做法：给它挂一个「覆盖排序」的子 Canvas ——
    // `overrideSorting = true` + 很高的 `sortingOrder`，同一画布层级下它就会排到最前。
    // （嵌套 Canvas 的排序只在父级是 Canvas 时生效；HUD 本身就是 Canvas ✓。
    //   纯渲染不需要 GraphicRaycaster，所以不挂。）
    private static void EnsureTopLayer(PingTracker tracker)
    {
        try
        {
            Canvas c = tracker.GetComponent<Canvas>();
            if (c == null) c = tracker.gameObject.AddComponent<Canvas>();

            if (!c.overrideSorting) c.overrideSorting = true;
            if (c.sortingOrder != TopSortingOrder) c.sortingOrder = TopSortingOrder;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PingTracker), "Update")]
internal static class AmethystBetterPingTrackerPatch
{
    public static bool Prefix(PingTracker __instance)
    {
        if (AmethystCompat.OtherPing) return true;   // 已有同功能的独立插件：让它的实现生效
        if (!AmethystConfig.BetterPingDisplay.Value) return true;
        AmethystBetterPingState.Render(__instance);
        return false;
    }
}
