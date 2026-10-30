using HarmonyLib;
using InnerNet;

namespace Amethyst.Patches;

// ===== 会议后「没有内鬼」导致的黑屏 =====
//
// 场景：最后一个内鬼被放逐 / 掉线后，原版偶尔不会正常结算，而是停在
// `NoImpostorsPresent` 的黑屏上（BanMod 的 `HostMeetingBlackScreenGuard` 就是治这个）。
//
// BanMod 的做法是 patch `LogicGameFlow.GetPlayerCounts` 把计数改成安全值；
// 但它的 FFA / 零内鬼分支依赖 `GameModeType` / `Options.GameMode`，v19 **没有**这两个 API。
// 因此这里用等价且更直接的兜底：
//   房主端在 `CheckEndCriteria` 之后检查「本局曾经有内鬼、现在存活内鬼 = 0、
//   还有存活玩家、且不在会议/放逐/开场中」→ 直接以船员胜利结束。
//
// 只在本局**确实出现过内鬼**时才生效，避免把「自定义 0 内鬼模式」误结算。
internal static class AmethystNoImpostorGuard
{
    private static bool _sawImpostor;
    private static bool _ending;
    private static float _lastCheck;

    internal static void Reset()
    {
        _sawImpostor = false;
        _ending = false;
        _lastCheck = 0f;
    }

    internal static void Check()
    {
        try
        {
            if (_ending) return;
            if (AmethystConfig.NoImpostorGameOver == null || !AmethystConfig.NoImpostorGameOver.Value) return;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            if (AmethystRpcGuard.IsHS()) return;                 // 躲猫猫没有内鬼，不能套用
            if (!AmethystRpcGuard.InMatch()) return;             // 排除大厅/会议/放逐/开场

            float now = UnityEngine.Time.unscaledTime;
            if (now - _lastCheck < 0.5f) return;                 // 不需要每帧全量扫
            _lastCheck = now;

            int alive = 0;
            int imps = 0;

            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl pc = all[i];
                if (pc == null || pc.Data == null) continue;
                if (pc.Data.Disconnected || pc.Data.IsDead) continue;

                alive++;
                if (AmethystRpcGuard.IsImp(pc)) imps++;
            }

            if (imps > 0)
            {
                _sawImpostor = true;
                return;
            }

            // 本局从没出现过内鬼（自定义模式）或场上已无人：不处理
            if (!_sawImpostor || alive <= 0) return;

            _ending = true;
            AmethystRpcGuard.NotifyOnce("no-impostor-end", AmethystText.T("NoImpostorEnd"));
            GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByVote, false);
        }
        catch { }
    }
}

// 原版每次判定结束条件后跑一次；放在 Postfix，先让原版自己结算，
// 原版没结算（黑屏）时我们再兜底。
[HarmonyPatch(typeof(LogicGameFlowNormal), "CheckEndCriteria")]
internal static class AmethystNoImpostorCheckPatch
{
    private static void Postfix() => AmethystNoImpostorGuard.Check();
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystNoImpostorResetPatch
{
    private static void Postfix() => AmethystNoImpostorGuard.Reset();
}
