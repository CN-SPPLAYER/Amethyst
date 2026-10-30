using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

// 会议界面左上角显示“房主是谁”：房主皇冠图标 + 房主名字。
// 完全参考 BetterAmongUs（Patches\Gameplay\UI\MeetingHudPatch.cs + Utilities\GameObjectUtils.DestroyTextTranslators）：
//   1) 复用 ProceedButton 位为左上角房主标签，点亮 HostIcon 并用房主颜色染色；
//   2) 关键：移除 TextTranslatorTMP，否则它会把文本重写回“继续/Continue”。
internal static class AmethystShowHostMeeting
{
    internal static bool Enabled => AmethystConfig.ShowMeetingHost != null && AmethystConfig.ShowMeetingHost.Value;

    internal static bool Valid()
    {
        var net = AmongUsClient.Instance;
        return net != null && net.NetworkMode != NetworkModes.FreePlay;
    }

    // ⚠ 性能（第 169 轮）：下面这三个静态字段是本次优化的核心。
    //   本类的 `Apply` 原本被挂在 `MeetingHud.Update` 上**每帧**调用，而它内部要做
    //     · `GetComponentsInChildren<TextTranslatorTMP>(true)` —— 每次都分配一个数组
    //     · `GetComponentInChildren<TextMeshPro>()`       —— 遍历整棵子层级
    //     · `AmethystText.T("Host", name)`               —— 每次都格式化出一个新字符串
    //   会议期间 60fps 下每秒就是 60 次数组分配 + 60 个短命字符串，GC 压力很直观。
    //   现在把「查找 / 绑定」与「每帧兜底」拆开：
    //     · Apply  —— 只在 `MeetingHud.Start` 跑一次，做上面这些重活并把控件缓存下来
    //     · Ensure —— 每帧只做「比对房主名与屏幕文本」，无查找、无分配
    private static TextMeshPro _nameText;
    private static int _boundHud = -1;
    private static string _hostName;
    private static string _lastText;
    private static int _retryAt;

    internal static void Apply(MeetingHud hud)
    {
        try
        {
            if (!Enabled || !Valid()) return;
            if (hud == null || hud.ProceedButton == null || hud.HostIcon == null) return;

            var hostClient = AmongUsClient.Instance.GetHost();
            if (hostClient == null || hostClient.Character == null || hostClient.Character.Data == null) return;

            // TextTranslatorTMP 会把文本重写回「继续/Continue」，只需在绑定这一次清掉。
            DestroyTranslators(hud.ProceedButton.gameObject);

            int colorId = hostClient.Character.Data.DefaultOutfit != null ? hostClient.Character.Data.DefaultOutfit.ColorId : 0;
            if (colorId >= 0 && Palette.PlayerColors != null && colorId < Palette.PlayerColors.Length)
                PlayerMaterial.SetColors(colorId, hud.HostIcon);

            // 控件引用只在这里取一次并缓存；同时记下所属 MeetingHud 实例，
            // 换了一局 / 换了一个 hud 就重新绑定。
            _boundHud = hud.GetInstanceID();
            _nameText = hud.ProceedButton.gameObject.GetComponentInChildren<TextMeshPro>();
            _hostName = null;
            _lastText = null;

            if (_nameText != null)
            {
                string name = hostClient.Character.Data.PlayerName;
                string text = AmethystText.T("Host", name);
                if (_nameText.text != text) _nameText.text = text;
                _hostName = name;
                _lastText = text;
            }
        }
        catch { }
    }

    // ⚠ 性能（第 169 轮）：`MeetingHud.Update` 上的每帧兜底 —— 只比对、不查找、不分配。
    //   只有在「房主名变了」或「屏幕上的文本被别的东西改掉了」时才重新格式化并写回。
    internal static void Ensure(MeetingHud hud)
    {
        try
        {
            if (hud == null) return;

            if (_boundHud != hud.GetInstanceID() || _nameText == null)
            {
                // 尚未绑定成功（例如会议中途才打开本功能，或 Start 时 ProceedButton 还没就绪）。
                // 低频重试完整绑定，而不是每帧重跑 Apply。
                if (Time.frameCount >= _retryAt)
                {
                    _retryAt = Time.frameCount + 30;
                    Apply(hud);
                }
                return;
            }

            var hostClient = AmongUsClient.Instance.GetHost();
            if (hostClient == null || hostClient.Character == null || hostClient.Character.Data == null) return;

            string name = hostClient.Character.Data.PlayerName;
            // 常见路径：房主名没变、且屏幕上仍是我们上次写的那串 → 一次字符串比较后直接返回。
            if (string.Equals(_hostName, name) && string.Equals(_nameText.text, _lastText)) return;

            string text = AmethystText.T("Host", name);
            if (_nameText.text != text) _nameText.text = text;
            _hostName = name;
            _lastText = text;
        }
        catch { }
    }

    internal static void DestroyTranslators(GameObject root)
    {
        if (root == null) return;
        try
        {
            foreach (var t in root.GetComponentsInChildren<TextTranslatorTMP>(true))
                if (t != null) UnityEngine.Object.Destroy(t);
        }
        catch { }
    }
}

// 会议开始时：布局 ProceedButton 位 + 点亮 HostIcon + 写入房主名
[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystShowHostMeetingPatch
{
    private static void Postfix(MeetingHud __instance)
    {
        try
        {
            if (!AmethystShowHostMeeting.Enabled || !AmethystShowHostMeeting.Valid()) return;
            if (__instance == null) return;

            if (__instance.ProceedButton != null && __instance.HostIcon != null)
            {
                __instance.ProceedButton.gameObject.transform.localPosition = new Vector3(-2.5f, 2.2f, 0f);
                var sr = __instance.ProceedButton.gameObject.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;
                var pb = __instance.ProceedButton.GetComponent<PassiveButton>();
                if (pb != null) pb.enabled = false;
                __instance.HostIcon.enabled = true;
                __instance.HostIcon.gameObject.SetActive(true);
                __instance.ProceedButton.gameObject.SetActive(true);
            }

            AmethystShowHostMeeting.Apply(__instance);
        }
        catch { }
    }
}

// 会议期间每帧兜底：即使有其它组件尝试改写文本，也保证左上角仍是房主名。
// ⚠ 性能（第 169 轮）：这里改为调用廉价的 `Ensure`（只比对文本），
//   完整的 `Apply`（含 GetComponentsInChildren / GetComponentInChildren / 字符串格式化）
//   只在 `MeetingHud.Start` 以及绑定失败时的低频重试里跑。
[HarmonyPatch(typeof(MeetingHud), "Update")]
internal static class AmethystShowHostMeetingUpdatePatch
{
    private static void Postfix(MeetingHud __instance)
    {
        try
        {
            if (!AmethystShowHostMeeting.Enabled || !AmethystShowHostMeeting.Valid()) return;
            AmethystShowHostMeeting.Ensure(__instance);
        }
        catch { }
    }
}
