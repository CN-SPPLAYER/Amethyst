using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

// 聊天气泡的“色盲文字”（颜色名称）按玩家颜色渲染。
// 注意：ChatBubble 的颜色名称在独立的 ColorBlindName 上（会议投票区是 PlayerVoteArea.ColorBlindName），
// 与 NameText 不是同一个控件，之前只处理了会议投票区，聊天气泡因此没有颜色。
[HarmonyPatch(typeof(ChatBubble), "SetColorblindText")]
internal static class AmethystChatBubbleColorBlindPatch
{
    private static void Postfix(ChatBubble __instance)
    {
        Apply(__instance, __instance != null ? __instance.playerInfo : null);
    }

    internal static void Apply(ChatBubble bubble, NetworkedPlayerInfo info)
    {
        try
        {
            if (bubble == null || bubble.ColorBlindName == null) return;
            if (AmethystConfig.ColorBlindText == null || !AmethystConfig.ColorBlindText.Value) return;
            if (info == null) return;

            int cid = 0;
            try { cid = info.DefaultOutfit != null ? info.DefaultOutfit.ColorId : 0; } catch { }

            string cn = AmethystColors.ColoredColorName(cid);
            if (!string.IsNullOrEmpty(cn) && bubble.ColorBlindName.text != cn)
            {
                bubble.ColorBlindName.richText = true;
                bubble.ColorBlindName.text = cn;
            }
        }
        catch { }
    }
}

// 兜底：SetColorblindText 可能在 playerInfo 赋值前调用，这里在设置气泡名字时再同步一次。
[HarmonyPatch(typeof(ChatController), "SetChatBubbleName")]
internal static class AmethystChatBubbleColorBlindSyncPatch
{
    private static void Postfix(ChatBubble bubble, NetworkedPlayerInfo playerInfo)
    {
        AmethystChatBubbleColorBlindPatch.Apply(bubble, playerInfo);
    }
}

// 界面优化：会议中玩家名字按颜色渲染（内鬼队友红色）。参考聊天气泡同一套规则。
[HarmonyPatch(typeof(MeetingHud), "Update")]
internal static class AmethystMeetingNameColorPatch
{
    private static readonly PlayerControl[] ById = new PlayerControl[256];
    private static int _tick;

    private static void Postfix(MeetingHud __instance)
    {
        if (__instance == null) return;
        bool nameOn = AmethystColors.NameColorsOn;
        bool cbOn = AmethystConfig.ColorBlindText != null && AmethystConfig.ColorBlindText.Value;
        if (!nameOn && !cbOn) return;

        // 降低 MeetingHud.Update 中名字与色盲刷新的频率（每 5 帧一次，足够平滑且极省）
        _tick = _tick >= 5 ? 0 : _tick + 1;
        if (_tick != 0) return;

        try
        {
            var states = __instance.playerStates;
            if (states == null) return;

            Array.Clear(ById, 0, ById.Length);
            var all = PlayerControl.AllPlayerControls;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    PlayerControl p = all[i];
                    if (p == null) continue;
                    int id = p.PlayerId;
                    if (id >= 0 && id < ById.Length) ById[id] = p;
                }
            }

            for (int i = 0; i < states.Count; i++)
            {
                PlayerVoteArea pv = states[i];
                if (pv == null) continue;

                // ⚠ 关键修复：介绍画面 / 死者展示卡片 (MeetingIntroAnimation) 中的卡片 PlayerId 可能未被正确赋值 (默认 0)，
                // 若被误判为本地玩家 PlayerId，会错渲染成本地玩家的配色。如果卡片在 SetCosmetics 阶段已有缓存，跳过全量反查。
                if (AmethystMeetingIntroNameColor.TryGetCachedColor(pv.GetInstanceID(), out Color cachedColor))
                {
                    if (nameOn && pv.NameText != null && pv.NameText.color != cachedColor)
                        pv.NameText.color = cachedColor;
                    continue;
                }

                int pid = pv.PlayerId.Value;
                if (pid < 0 || pid >= ById.Length) continue;
                PlayerControl pc = ById[pid];
                if (pc == null || pc.Data == null) continue;

                // 会议中的色盲文字（颜色名称）按其颜色渲染
                if (cbOn && pv.ColorBlindName != null)
                {
                    int cid = pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
                    string cn = AmethystColors.ColoredColorName(cid);
                    if (!string.IsNullOrEmpty(cn) && pv.ColorBlindName.text != cn) pv.ColorBlindName.text = cn;
                }

                // 名字按颜色渲染（用顶点色，避免依赖 TMP 富文本开关）
                // ⚠ 只在颜色真的变了才写：`TMP_Text.color` 的 setter 会把顶点数据标脏。
                if (nameOn && pv.NameText != null)
                {
                    Color want = AmethystColors.DisplayColor(pc);
                    if (pv.NameText.color != want) pv.NameText.color = want;
                }
            }
        }
        catch { }
    }

    internal static PlayerControl FindPlayer(byte id)
    {
        try
        {
            var all = PlayerControl.AllPlayerControls;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl p = all[i];
                if (p != null && p.PlayerId == id) return p;
            }
        }
        catch { }
        return null;
    }
}

// 会议里玩家“死亡/存活”状态刷新时，兜底把名字按玩家颜色渲染一次。
[HarmonyPatch(typeof(PlayerVoteArea), "SetDead")]
internal static class AmethystVoteAreaDeadNamePatch
{
    private static void Postfix(PlayerVoteArea __instance)
    {
        try
        {
            if (__instance == null || __instance.NameText == null) return;
            if (!AmethystColors.NameColorsOn) return;

            PlayerControl pc = AmethystMeetingNameColorPatch.FindPlayer(__instance.PlayerId.Value);
            if (pc == null || pc.Data == null) return;
            __instance.NameText.color = AmethystColors.DisplayColor(pc);
        }
        catch { }
    }
}
