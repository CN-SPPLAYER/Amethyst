using HarmonyLib;
using InnerNet;

namespace Amethyst.Patches;

// 复盘信息：把对局事件记进 AmethystRecap。
// 数据来源与 FinalSuspect 对齐：击杀来自 MurderPlayer、放逐来自 ExileController.Begin；
// 每名玩家的身份/进度/掉线状态由 AmethystRecap.Sync() 逐帧快照。

// 击杀 → 记录受害者的死因与凶手
[HarmonyPatch(typeof(PlayerControl), "MurderPlayer")]
internal static class AmethystRecapKillPatch
{
    private static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
    {
        try
        {
            if (__instance == null || target == null) return;
            if ((resultFlags & MurderResultFlags.FailedError) != 0) return;
            if ((resultFlags & MurderResultFlags.FailedProtected) != 0) return;
            if (__instance.PlayerId == target.PlayerId) return;

            AmethystRecap.NoteKill(__instance.PlayerId, target.PlayerId);
        }
        catch { }
    }
}

// 放逐 → 记录被票出者。
// ExileController 没有 Init，入口是 Begin(InitProperties)；Postfix 时 initData 已赋值。
[HarmonyPatch(typeof(ExileController), "Begin")]
internal static class AmethystRecapExilePatch
{
    private static void Postfix(ExileController __instance)
    {
        try
        {
            if (__instance == null) return;
            NetworkedPlayerInfo info = __instance.initData != null ? __instance.initData.networkedPlayer : null;
            if (info == null) return;

            AmethystRecap.NoteExile(info.PlayerId);
        }
        catch { }
    }
}

// 法官判决 → 记录该法官判决（overrule）的对象。
// 参考 BetterAmongUs `Patches\Gameplay\RolePatch.cs`：同样挂钩 MeetingHud.SetJudgeOverrule
// （签名 PlayerId judgePlayerId, PlayerId targetPlayerId, ushort overruleNonce），
// 并把 targetPlayerId 记在法官身上。
[HarmonyPatch(typeof(MeetingHud), "SetJudgeOverrule")]
internal static class AmethystRecapJudgePatch
{
    private static void Postfix(PlayerId judgePlayerId, PlayerId targetPlayerId)
    {
        try
        {
            AmethystRecap.NoteJudge(judgePlayerId.Value, targetPlayerId.Value);
        }
        catch { }
    }
}

// 掉线 / 断开 → 记录为死亡原因「掉线」。
//
// 这是之前**遗漏**的一环：`AmethystRecap.NoteDisconnect` 写了却从没被调用，
// 而 `Sync()` 那条 `data.Disconnected` 分支也不可靠 —— 因为玩家断开后会立刻被移出
// `PlayerControl.AllPlayerControls`，逐帧遍历根本看不到 `Disconnected == true`。
// 结果就是掉线的人在复盘里一直显示「存活」。
// 这里挂在 `GameData.HandleDisconnect` 的 **Prefix**（此时 player.Data 仍然完整），
// 与 AmethystDisconnect 记录离开原因用的是同一个挂钩点。
[HarmonyPatch(typeof(GameData), "HandleDisconnect", new[] { typeof(PlayerControl), typeof(DisconnectReasons) })]
internal static class AmethystRecapDisconnectPatch
{
    private static void Prefix(PlayerControl player)
    {
        try
        {
            // 大厅里的进出不算「对局中掉线」，避免污染上一局的复盘
            if (LobbyBehaviour.Instance != null) return;
            if (player == null || player.Data == null) return;

            AmethystRecap.NoteDisconnect(player.Data);
        }
        catch { }
    }
}

// 结算界面就绪 → 采集结果 + 补一次快照并冻结。
//
// 挂钩 `EndGameManager.SetEverythingUp`（与 BetterAmongUs `GameSummaryPatch` 同一挂钩点）。
// 此时 `EndGameResult.CachedGameOverReason` 已经就绪，比之前的 0.5s 轮询可靠
// （EndGameManager 会先于 SetEverythingUp 出现，轮询可能抢在其之前读到默认原因）。
[HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
internal static class AmethystRecapEndGamePatch
{
    private static void Postfix(EndGameManager __instance)
    {
        try
        {
            AmethystRecap.Sync();
            AmethystRecap.CaptureResult();
            AmethystRecap.Freeze();
            AmethystRecap.EndScreen = __instance;
            AmethystRecapWindow.Hide();
        }
        catch { }
    }
}

// 每局开始 → 清空上一局复盘
[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystRecapResetPatch
{
    private static void Prefix()
    {
        AmethystRecap.Reset();
    }
}
