using System;
using System.Collections.Generic;

namespace Amethyst;

// 反作弊通知文案适配器（第 245 轮）。
//
// 背景：旧实现统一用 "{0} 使用外挂【{1}】"，而 {1} 是名词片段
// （如 "玩家多次破坏"），拼出来是 "SP_小紫 使用外挂【玩家多次破坏】" —— 不通顺。
// 但同一批片段还要被 "已警告 {0}【{1}】" / "已踢出 {0}【{1}】" 复用，
// 所以不能把片段改成句子。
//
// 做法：为每个事件新增 Notify_<Key> 完整句子模板，按**已本地化的片段文本**反查。
// 找不到时回退到通用模板，保证不会出现键名或空白。
internal static class AmethystNotifyText
{
    internal static readonly string[] Keys =
    {
        "MultiSabotage", "RapidSabotage", "SabotageByCrew", "SabotageInMeeting", "SabotageOffMap",
        "SabotageInHS", "DoorsInMeeting", "DoorsInHS_2", "KillByCrewmate", "KillImpostor",
        "DeadMurder", "RepeatKillDead", "FastKill", "TaskByImpostor", "TaskCompletionFlood",
        "TaskWithoutMap", "ExtraTask", "SetTasksInMatch", "SetLevelInMatch", "SetNameInMatch",
        "MultiSetName", "MultiSetLevel", "HTMLInName", "HostOnlyRPCByClient", "OutfitChangeInMatch",
        "GameRPCInLobby", "UnusedExiledRPC", "FakeVentId", "VentInMeeting", "VentWithoutPermission",
        "VentForAnother", "ForcedVentEject", "ReportInHS", "MeetingInHS", "VentingAsSeekerInHS",
        "VentAbuseInHS", "VoteByDead", "VoteOutsideMeeting", "ChatNoteOutsideMeeting",
        "ScanByImpostor", "ScanWithoutMap", "ScanWithoutTask", "ShapeshiftWithoutRole",
        "VanishAppearWithoutRole", "PlatformSpoof", "KnownCheatMenuSignature",
        "SpawnFloodTrimmed", "TrimmedDataFlood", "BlockedFakeMeeting", "VoteKickBlocked",
        "InvalidFC", "Bot", "Fortegreen"
    };

    private static Dictionary<string, string> _byReason;

    private static void Build()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < Keys.Length; i++)
        {
            string key = Keys[i];
            string reason, template;
            try
            {
                reason = AmethystText.T(key);
                template = AmethystText.T("Notify_" + key);
            }
            catch { continue; }

            if (string.IsNullOrEmpty(reason) || reason == key) continue;
            if (string.IsNullOrEmpty(template) || template == "Notify_" + key) continue;

            map[reason] = template;
        }

        _byReason = map;
    }

    internal static void Invalidate() => _byReason = null;

    // 按已本地化的 reason 片段生成一条完整通知正文。
    internal static string Body(string who, string reasonText)
    {
        try
        {
            if (_byReason == null) Build();
            if (!string.IsNullOrEmpty(reasonText) && _byReason.TryGetValue(reasonText, out string tpl))
                return string.Format(tpl, who);
        }
        catch { }

        return AmethystText.T("TriggeredProtection", who, reasonText);
    }

    // 没有明确作案者时使用（"有人……"）。
    internal static string BodyAnonymous(string reasonText)
        => Body(AmethystText.T("Someone"), reasonText);
}
