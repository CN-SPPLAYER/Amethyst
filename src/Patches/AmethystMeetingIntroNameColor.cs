using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

// 会议名字着色：报告人栏 / 死亡栏 / 投票栏。
//
// 运行期实证（Player.log 的真实调用链）：
//   PlayerControl:StartMeeting(NetworkedPlayerInfo)
//     → PlayerVoteArea:SetCosmetics(NetworkedPlayerInfo)      ← 卡片在这里被写入名字/外观
//     → MeetingIntroAnimation:Init(NetworkedPlayerInfo, NetworkedPlayerInfo[])
//     → <CoIntro>d__57:MoveNext()
// 即：介绍画面的卡片（死亡卡片 + 报告人卡片）都是 PlayerVoteArea，
// 名字与颜色在 `SetCosmetics(playerInfo)` 里写入。
//
// ⚠ 关键教训（第五十七轮修复）：
//   开场介绍的 `MeetingIntro.deadCards` 里那些 PlayerVoteArea 的 **PlayerId 并不可靠**
//   （未被赋值，仍是默认值），因此**绝不能按 PlayerId 反查玩家**——
//   否则会查到 0 号玩家，表现就是「被报告的死者名字用了别人的颜色」。
//   介绍卡片唯一可靠的来源是 `SetCosmetics(playerInfo)` 传进来的那个 playerInfo；
//   这里把它算出的颜色**按卡片实例缓存**，每帧兜底只复用缓存，不再反查。
//   投票阶段（`MeetingHud.playerStates`）的 PlayerId 才是可靠的，那条路径照旧按 PlayerId 解析。
internal static class AmethystMeetingIntroNameColor
{
    // 卡片实例 -> 应显示的颜色（来自 SetCosmetics 的权威 playerInfo）
    private static readonly Dictionary<int, Color> CardColors = new Dictionary<int, Color>();

    private static bool On => AmethystColors.NameColorsOn;

    internal static bool TryGetCachedColor(int instanceId, out Color color)
    {
        return CardColors.TryGetValue(instanceId, out color);
    }

    // 权威路径：SetCosmetics(playerInfo) 带来了该卡片真正对应的玩家。
    // 计算颜色 → 缓存到该卡片实例 → 立即应用。
    internal static void ApplyFromInfo(PlayerVoteArea pv, NetworkedPlayerInfo info)
    {
        try
        {
            if (!On || pv == null || info == null) return;

            TextMeshPro nameText = pv.NameText;
            if (nameText == null) return;

            // 优先查找此 info 对应的实际 PlayerControl
            PlayerControl pc = AmethystMeetingNameColorPatch.FindPlayer(info.PlayerId);
            Color want;
            if (pc != null && pc.Data != null)
            {
                want = AmethystColors.DisplayColor(pc);
            }
            else
            {
                int cid = info.DefaultOutfit != null ? info.DefaultOutfit.ColorId : 0;
                want = AmethystColors.ColorOf(cid);
            }

            Apply(pv, nameText, want);
        }
        catch { }
    }

    // 投票阶段路径：playerStates 的 PlayerId 可靠，按它解析（保留伪装者队友红色规则）。
    internal static void ApplyByPlayerId(PlayerVoteArea pv)
    {
        try
        {
            if (!On || pv == null) return;

            TextMeshPro nameText = pv.NameText;
            if (nameText == null) return;

            // ⚠ 关键修复：若该卡片在 SetCosmetics 时已有权威缓存颜色，优先复用，
            // 避免开场/死者卡片 PlayerId 为 0 (未赋值) 时被误判为本地玩家(Player 0)而渲染成本地玩家颜色！
            if (CardColors.TryGetValue(pv.GetInstanceID(), out Color cached))
            {
                if (nameText.color != cached) nameText.color = cached;
                return;
            }

            PlayerControl pc = AmethystMeetingNameColorPatch.FindPlayer(pv.PlayerId.Value);
            if (pc == null || pc.Data == null) return;

            Apply(pv, nameText, AmethystColors.DisplayColor(pc));
        }
        catch { }
    }

    // 介绍卡片每帧兜底：**只复用缓存**，拿不到就跳过，绝不按 PlayerId 猜。
    internal static void ReapplyCached(PlayerVoteArea pv)
    {
        try
        {
            if (!On || pv == null) return;

            TextMeshPro nameText = pv.NameText;
            if (nameText == null) return;

            if (!CardColors.TryGetValue(pv.GetInstanceID(), out Color want)) return;
            if (nameText.color != want) nameText.color = want;
        }
        catch { }
    }

    private static void Apply(PlayerVoteArea pv, TextMeshPro nameText, Color want)
    {
        try { CardColors[pv.GetInstanceID()] = want; } catch { }
        if (nameText.color != want) nameText.color = want;
    }

    // 新会议开始时清掉上一轮的缓存（实例 ID 不再复用，留着只是白占内存）。
    internal static void ClearCache()
    {
        try { CardColors.Clear(); } catch { }
    }

    // 每帧兜底（由 AmethystHud 驱动）。
    internal static void Tick()
    {
        try
        {
            if (!On) return;

            MeetingHud hud = MeetingHud.Instance;
            if (hud == null) return;

            // 开场介绍的死亡卡片：只用缓存（PlayerId 不可靠，不能反查）
            MeetingIntroAnimation intro = hud.MeetingIntro;
            if (intro != null && intro.deadCards != null)
            {
                for (int i = 0; i < intro.deadCards.Count; i++)
                {
                    PlayerVoteArea pv = intro.deadCards[i];
                    if (pv != null) ReapplyCached(pv);
                }
            }

            // 投票阶段的玩家卡片：PlayerId 可靠
            var states = hud.playerStates;
            if (states != null)
            {
                for (int i = 0; i < states.Length; i++)
                {
                    PlayerVoteArea pv = states[i];
                    if (pv != null) ApplyByPlayerId(pv);
                }
            }
        }
        catch { }
    }
}

// 卡片写入自身数据（名字/外观）时立刻着色——这是介绍画面唯一权威的来源。
// Player.log 证实介绍阶段走的就是这条路：PlayerVoteArea:SetCosmetics。
[HarmonyPatch(typeof(PlayerVoteArea), "SetCosmetics")]
internal static class AmethystVoteAreaSetCosmeticsColorPatch
{
    private static void Postfix(PlayerVoteArea __instance, NetworkedPlayerInfo playerInfo)
    {
        AmethystMeetingIntroNameColor.ApplyFromInfo(__instance, playerInfo);
    }
}

// 报告人卡片被标记时着色一次（报告人卡片在 playerStates 内，PlayerId 可靠）。
[HarmonyPatch(typeof(PlayerVoteArea), "SetReporter")]
internal static class AmethystVoteAreaSetReporterColorPatch
{
    private static void Postfix(PlayerVoteArea __instance)
    {
        AmethystMeetingIntroNameColor.ApplyByPlayerId(__instance);
    }
}

// 新会议开始：清理上一轮的卡片颜色缓存。
[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystMeetingColorCacheResetPatch
{
    private static void Postfix()
    {
        AmethystMeetingIntroNameColor.ClearCache();
    }
}
