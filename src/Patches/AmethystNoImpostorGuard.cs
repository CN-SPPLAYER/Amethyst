using HarmonyLib;
using InnerNet;

namespace Amethyst.Patches;

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
            if (AmethystRpcGuard.IsHS()) return;                 
            if (!AmethystRpcGuard.InMatch()) return;             

            float now = UnityEngine.Time.unscaledTime;
            if (now - _lastCheck < 0.5f) return;                 
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

            if (!_sawImpostor || alive <= 0) return;

            _ending = true;
            AmethystRpcGuard.NotifyOnce("no-impostor-end", AmethystText.T("NoImpostorEnd"));
            GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByVote, false);
        }
        catch { }
    }
}

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
