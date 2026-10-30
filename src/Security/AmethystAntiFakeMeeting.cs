using HarmonyLib;
using UnityEngine;
using Amethyst.Patches;

namespace Amethyst;

internal static class AmethystAntiFakeMeeting
{
    private static float lastNote;

    // 独立开关：Guard.BlockFakeMeetings（旧的实现错误地挂在 RpcGuard 主开关上，
    // 导致关掉 RpcGuard 后该防护无法独立启用）
    internal static bool On => AmethystConfig.BlockFakeMeetings != null && AmethystConfig.BlockFakeMeetings.Value;

    internal static bool Illegal()
    {
        try
        {
            // 第 265 轮：`LobbyBehaviour.Instance` 可能残留（禁用未销毁），
            // 用「是否真正激活」判断，否则会把所有正常会议当成伪造会议销毁。
            var lobby = LobbyBehaviour.Instance;
            bool inLobby = lobby != null && lobby.gameObject != null && lobby.gameObject.activeInHierarchy;
            return ShipStatus.Instance == null || inLobby;
        }
        catch { return false; }
    }

    internal static void Kill(MeetingHud hud)
    {
        try { if (hud != null) Object.Destroy(hud.gameObject); } catch { }

        if (Time.unscaledTime - lastNote < 1f) return;
        lastNote = Time.unscaledTime;
        AmethystSecurityNotify.Fire("BlockedFakeMeeting");
    }
}

[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystFakeMeetingStartPatch
{
    public static bool Prefix(MeetingHud __instance)
    {
        // 无论正常会议还是伪造会议，都标记会议开始时间（用于会议后破坏检测）
        AmethystRpcGuard.MarkMeetingOpened();

        if (!AmethystAntiFakeMeeting.On || !AmethystAntiFakeMeeting.Illegal()) return true;
        AmethystAntiFakeMeeting.Kill(__instance);
        return false;
    }
}

// 会议结束时清除破坏检测状态（参考 BetterAmongUs / TownofNext）
[HarmonyPatch(typeof(MeetingHud), "Close")]
internal static class AmethystMeetingClosePatch
{
    public static void Postfix()
    {
        AmethystRpcGuard.ClearMeetingOpened();
        AmethystRpcGuard.ClearRapidSabotage();
    }
}
