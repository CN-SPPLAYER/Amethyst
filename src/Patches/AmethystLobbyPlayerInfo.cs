using System;
using AmongUs.Data;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

// 第 207 轮（参考 BetterAmongUs `MonoScripts\PlayerInfoDisplay.cs`，加入我们自己的信息）：
// 大厅中在每名玩家的名字**上方**显示一行扩展信息：
//   第一行：平台 + 等级（平台灰色、等级黄色）
//   第二行：好友码（正常青色；缺失/无效标红并写「无好友码」）
//
// 实现方式与 BAU 相同：克隆原版 `Names/NameText_TMP`，挂到同一个父节点下。
// 每名玩家每 15 帧刷新一次（10 人局 ≈ 每帧 0.7 次原生读取），并且只在文本变化时才写 TMP。
// 仅大厅显示；离开大厅自动隐藏。
internal sealed class AmethystLobbyPlayerInfo : MonoBehaviour
{
    private const float OffsetY = 0.52f;

    private PlayerControl _player;
    private TextMeshPro _info;
    private TextMeshPro _nameText;

    private string _written = string.Empty;
    private bool _visible;
    private int _nextAt;
    private int _createRetryAt;

    public AmethystLobbyPlayerInfo(IntPtr ptr) : base(ptr) { }

    internal void Init(PlayerControl player)
    {
        _player = player;
        Ensure();
    }

    private void Ensure()
    {
        if (_info != null) return;
        if (_player == null) return;

        int frame = Time.frameCount;
        if (frame < _createRetryAt) return;
        _createRetryAt = frame + 30;

        try
        {
            Transform names = _player.gameObject.transform.Find("Names/NameText_TMP");
            if (names == null) return;

            TextMeshPro source = names.GetComponent<TextMeshPro>();
            if (source == null) return;

            _nameText = source;

            TextMeshPro t = UnityEngine.Object.Instantiate(source, names);
            t.name = "AmethystLobbyInfo";
            t.transform.localPosition = new Vector3(0f, OffsetY, 0f);
            t.transform.localScale = Vector3.one;
            t.fontSize = 1.25f;
            t.fontSizeMin = 1.25f;
            t.fontSizeMax = 1.25f;
            t.enableAutoSizing = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = true;
            if (t.text != string.Empty) t.text = string.Empty;
            t.gameObject.SetActive(true);

            _info = t;
        }
        catch { }
    }

    private void LateUpdate()
    {
        if (_player == null) return;

        Ensure();
        if (_info == null) return;
        if (Time.frameCount < _nextAt) return;
        _nextAt = Time.frameCount + 15;

        try
        {
            bool on = AmethystConfig.LobbyPlayerInfo != null && AmethystConfig.LobbyPlayerInfo.Value;
            if (!on || !AmethystPlayerCacheStore.InLobby) { Set(string.Empty); return; }

            // ❌ 高危闪退风险防御：绝不直接去读频繁重组的 pc.Data（Disconnected 后会瞬置 null 或抛异常导致 0xC0000005）
            // 应该通过极度安全的托管错峰快照读取。
            if (!AmethystPlayerCacheStore.TryGet(_player.PlayerId, out AmethystPlayerCache cache) || cache == null)
            {
                Set(string.Empty);
                return;
            }

            // 校验玩家原生数据状态和离线状态（cache.Pc 的安全检测）
            if (cache.Pc == null || cache.Pc.gameObject == null)
            {
                Set(string.Empty);
                return;
            }

            ClientData c = FindClient(_player.OwnerId);
            if (c == null) { Set(string.Empty); return; }

            string platform = AmethystAccess.PlatformOf(c);
            string level = AmethystJoinLevels.Display(c);

            // 直播主模式（游戏自带设置，参考 BAU `PlayerInfoDisplay`）：
            // 隐藏平台信息，并把好友码打码，避免直播时泄露。
            bool streamer = false;
            try
            {
                streamer = DataManager.Settings != null
                    && DataManager.Settings.Gameplay != null
                    && DataManager.Settings.Gameplay.StreamerMode;
            }
            catch { }

            string platText = (string.IsNullOrEmpty(platform) || platform == "Unknown")
                ? string.Empty
                : $"<color=#9E9E9E>{platform}</color>";
            if (streamer && platText.Length > 0)
                platText = $"<color=#9E9E9E>{AmethystText.T("PlayerPlatformHidden")}</color>";
            string lvlText = (string.IsNullOrEmpty(level) || level == "?")
                ? string.Empty
                : $"<color=#FFD829>Lv {level}</color>";

            string line1 = JoinNonEmpty(platText, lvlText);

            string fc = SafeFc(c);
            if (streamer && !string.IsNullOrWhiteSpace(fc)) fc = new string('*', fc.Length);
            string line2;
            if (string.IsNullOrWhiteSpace(fc))
                line2 = $"<color=#FF5252>{AmethystText.T("NoFriendCode")}</color>";
            else
                line2 = $"<color=#00F7FF>{fc}</color>";

            Set(string.IsNullOrEmpty(line1) ? line2 : line1 + "\n" + line2);
        }
        catch { }
    }

    private void Set(string s)
    {
        if (_info == null) return;

        if (!string.Equals(s, _written, StringComparison.Ordinal))
        {
            _written = s;
            try { _info.text = s; } catch { }
        }

        bool vis = !string.IsNullOrEmpty(s);
        if (vis != _visible)
        {
            _visible = vis;
            try { _info.enabled = vis; } catch { }
        }
    }

    private static string JoinNonEmpty(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b;
        if (string.IsNullOrEmpty(b)) return a;
        return a + "  " + b;
    }

    private static string SafeFc(ClientData c)
    {
        try { return c != null ? (c.FriendCode ?? string.Empty) : string.Empty; }
        catch { return string.Empty; }
    }

    private static ClientData FindClient(int ownerId)
    {
        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            if (net == null || net.allClients == null) return null;

            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c != null && c.Id == ownerId) return c;
            }
        }
        catch { }
        return null;
    }
}

[HarmonyPatch(typeof(PlayerControl), "Awake")]
internal static class AmethystLobbyInfoAttachPatch
{
    private static void Postfix(PlayerControl __instance)
    {
        try
        {
            if (__instance == null) return;
            if (__instance.gameObject.GetComponent<AmethystLobbyPlayerInfo>() == null)
                __instance.gameObject.AddComponent<AmethystLobbyPlayerInfo>().Init(__instance);
        }
        catch { }
    }
}
