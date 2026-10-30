using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst;

// 记录每位玩家断开时的原因（参考 BetterAmongUs：GameData.HandleDisconnect + DisconnectReasons），
// 供离开提示显示更准确的文案。以 ClientId（OwnerId）为键，与 JoinDetector 的 client.Id 对应。
internal static class AmethystDisconnect
{
    private static readonly Dictionary<int, DisconnectReasons> Reasons = new Dictionary<int, DisconnectReasons>();

    // 本反作弊（Amethyst）自己执行的踢/封记录：clientId -> 记录。
    // PlayerName 用于在 GameData.ShowNotification（只拿得到 playerName，拿不到 clientId）时匹配；
    // Shown 标记「带原因的文案是否已经推过」，保证一次踢/封只显示一次；At 用于 TTL 清理防泄漏。
    private struct OurAction
    {
        internal bool Ban;
        internal string Reason;
        internal string PlayerName;
        internal bool Shown;
        internal float At;
    }

    private static readonly Dictionary<int, OurAction> OurActions = new Dictionary<int, OurAction>();

    // 已经提示过离开的 clientId，避免同一玩家被重复提示。
    private static readonly HashSet<int> AnnouncedLeaves = new HashSet<int>();

    /// <summary>记录保留时长（秒）。ShowNotification 未在此时限内到达也不会永久占用内存。</summary>
    private const float ActionTtl = 90f;

    internal static void Record(PlayerControl player, DisconnectReasons reason)
    {
        try { if (player != null) Reasons[player.OwnerId] = reason; } catch { }
    }

    // 直接按 clientId 记录（AmongUsClient.OnPlayerLeft 提供 ClientData.Id）
    internal static void RecordById(int clientId, DisconnectReasons reason)
    {
        try { if (clientId >= 0) Reasons[clientId] = reason; } catch { }
    }

    internal static DisconnectReasons Get(int clientId)
        => Reasons.TryGetValue(clientId, out DisconnectReasons r) ? r : DisconnectReasons.Unknown;

    // 记录一次「我们踢/封了某人」，并**立刻**把带原因的文案推到左下角。
    //
    // 为什么要立刻推：原版 GameData.ShowNotification 对 Kick/Ban 只给一句不带原因的提示，
    // 而它何时被调用、玩家名是否与我们记录的一致都不完全可控；立刻推一次可以保证原因一定可见。
    // ShowNotification 那边只负责**抑制**原版那条，避免两条并存（见 AmethystShowNotificationGuard）。
    internal static void RecordAction(int clientId, bool ban, string reason, string playerName = null)
    {
        try
        {
            if (clientId < 0) return;
            PruneExpired();

            bool shown = PushCustomMessage(playerName, ban, reason);
            OurActions[clientId] = new OurAction
            {
                Ban = ban,
                Reason = reason ?? string.Empty,
                PlayerName = playerName ?? string.Empty,
                Shown = shown,
                At = Time.realtimeSinceStartup,
            };
        }
        catch { }
    }

    internal static bool TryGetAction(int clientId, out bool ban, out string reason)
    {
        if (OurActions.TryGetValue(clientId, out OurAction a)) { ban = a.Ban; reason = a.Reason; return true; }
        ban = false; reason = null; return false;
    }

    // 按玩家名找到我们踢/封的记录，并**消费**它（移除）。
    // shown=true 表示带原因的文案已经推过，调用方只需抑制原版、不必再推一次。
    internal static bool TryConsumeActionByName(string playerName, out bool ban, out string reason, out bool shown)
    {
        ban = false; reason = null; shown = false;
        string want = (playerName ?? string.Empty).Trim();
        if (want.Length == 0) return false;

        int hit = FindByName(want);
        if (hit < 0) return false;

        OurAction a = OurActions[hit];
        OurActions.Remove(hit);   // 一次性消费，天然防重 + 自动清理

        ban = a.Ban;
        reason = a.Reason;
        shown = a.Shown;
        return true;
    }

    private static int FindByName(string want)
    {
        foreach (var kv in OurActions)
        {
            string have = (kv.Value.PlayerName ?? string.Empty).Trim();
            if (have.Length == 0) continue;
            if (string.Equals(have, want, System.StringComparison.Ordinal)) return kv.Key;
        }
        // 名字大小写不一致时再退一步（游戏内名字大小写稳定，这里只是保险）。
        foreach (var kv in OurActions)
        {
            string have = (kv.Value.PlayerName ?? string.Empty).Trim();
            if (have.Length == 0) continue;
            if (string.Equals(have, want, System.StringComparison.OrdinalIgnoreCase)) return kv.Key;
        }
        return -1;
    }

    private static void PruneExpired()
    {
        if (OurActions.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        List<int> dead = null;
        foreach (var kv in OurActions)
        {
            if (now - kv.Value.At <= ActionTtl) continue;
            (dead ??= new List<int>()).Add(kv.Key);
        }
        if (dead == null) return;
        for (int i = 0; i < dead.Count; i++) OurActions.Remove(dead[i]);
    }

    // 返回 true 表示这是第一次为该玩家提示离开。
    internal static bool MarkLeaveAnnounced(int clientId) => AnnouncedLeaves.Add(clientId);

    // 把「某人因某原因被踢/封」推到游戏左下角通知栏（原版 Notifier）。
    // 返回 true 表示成功推送；HUD 尚未就绪时返回 false，交由 ShowNotification 补推。
    internal static bool PushCustomMessage(string playerName, bool ban, string reason)
    {
        try
        {
            string who = (playerName ?? string.Empty).Trim();
            string head = AmethystText.T(ban ? "BannedByUs" : "KickedByUs", reason ?? string.Empty);
            string full = who.Length == 0 ? head : $"{who} {head}";

            HudManager hud = HudManager.Instance;
            if (hud == null || hud.Notifier == null) return false;
            hud.Notifier.AddDisconnectMessage(full);
            return true;
        }
        catch { return false; }
    }

    internal static void Clear()
    {
        Reasons.Clear();
        OurActions.Clear();
        AnnouncedLeaves.Clear();
    }
}

[HarmonyPatch(typeof(GameData), "HandleDisconnect", new[] { typeof(PlayerControl), typeof(DisconnectReasons) })]
internal static class AmethystDisconnectHandlePatch
{
    private static void Prefix(PlayerControl player, DisconnectReasons reason) => AmethystDisconnect.Record(player, reason);
}

// 补充来源：AmongUsClient.OnPlayerLeft 对每一次离开都会带上原因，
// 且通常先于 AmethystJoinDetector 的轮询触发（轮询才是发通知的时机），
// 因此这里记录能提高「正常离开」拿到的原因准确率（尤其大厅里 GameData 可能尚未建立的情况）。
// 参考 BetterAmongUs 的 PlayerJoinAndLeftPatch 同样同时挂钩 OnPlayerLeft 与 HandleDisconnect。
[HarmonyPatch(typeof(AmongUsClient), "OnPlayerLeft")]
internal static class AmethystDisconnectLeftPatch
{
    private static void Postfix(ClientData data, DisconnectReasons reason)
    {
        try { if (data != null) AmethystDisconnect.RecordById(data.Id, reason); } catch { }
    }
}

// ⚠ 第 227 轮新增：拦截 GameData.ShowNotification，用带原因的自定义文案替换原版的「玩家已离开游戏」。
//
// 游戏在处理断开时呼叫 GameData.ShowNotification(playerName, reason) 把一条左下角消息弹出来；
// 对 Kick/Ban，它仅显示一个不带动因的「被踢出/已离开游戏」，我们的踢人原因无法传达。
// 方案（参考 BetterAmongUs PlayerJoinAndLeftPatch + Hydra AccurateDisconnectReason）：
//   ① Prefix 拦掉原版（return false）；
//   ② 若该玩家刚被我们踢/封（RecordAction 命中），推送「名字 —— 原因」；
//   ③ 否则放行原版（return true），正常离开保持原样。
[HarmonyPatch(typeof(GameData), "ShowNotification")]
internal static class AmethystShowNotificationGuard
{
    // 签名已由反编译确认：GameData.ShowNotification(string playerName, DisconnectReasons reason)
    static bool Prefix([HarmonyArgument(0)] string playerName, [HarmonyArgument(1)] DisconnectReasons reason)
    {
        try
        {
            // 我们踢/封的玩家：抑制原版那条不带原因的「已离开游戏」。
            // 带原因的文案在 RecordAction 里已经立刻推过；若当时 HUD 尚未就绪（shown=false），这里补推一次。
            if (AmethystDisconnect.TryConsumeActionByName(playerName, out bool ourBan, out string reasonText, out bool shown))
            {
                if (!shown) AmethystDisconnect.PushCustomMessage(playerName, ourBan, reasonText);
                return false;   // 拦截原版
            }

            // 不是我们踢/封的（正常离开、掉线、官方封禁等）：原版文案保持不动。
            return true;
        }
        catch { return true; }
    }
}

// ⚠ 第 116 轮按需求：**移除"显示平台及等级"功能**（JoinDetect 开关已删），
//   玩家正常进/离开完全交还给**原版通知** —— 但第 227 轮**恢复**对 ShowNotification 的拦截，
//   仅用于把我们踢/封显示为含原因的消息。正常离开仍走原版。
