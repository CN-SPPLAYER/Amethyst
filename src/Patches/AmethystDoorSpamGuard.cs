using HarmonyLib;
using Hazel;
using InnerNet;

namespace Amethyst.Patches;

// ===== 关门 RPC 防护 =====
//
// 两件事：
//   1) **会议中**：`CloseDoorsOfType` 一律硬丢（和躲猫猫 `AmethystHsSabotage` 一致）。
//      不受 `DropRPC` 开关影响，主机/客户端都丢 —— 否则只提示不拦，
//      用户看到的就是「检测到了但门还是关了」。
//   2) **0.01 秒内连关 ≥2 扇**：一键关门外挂。放行第 1 个，第 2 个起硬丢。
//      `Time.unscaledTime` 每帧只更新一次，同一帧的多个包时间差为 0，必然命中。
//
// `CloseDoorsOfType` 载荷只有 `SystemTypes`（1 字节），不含发送者，
// 因此只能按「本机收到的频率」判定；已存在的门状态不参与计数。
internal static class AmethystDoorSpamGuard
{
    private const float Window = 0.01f;
    private const int Limit = 2;      // 第 2 个起拦截

    private static float _windowStart = -1000f;
    private static int _count;

    internal static void Reset()
    {
        _windowStart = -1000f;
        _count = 0;
    }

    internal static bool Allow()
    {
        try
        {
            if (AmethystConfig.DoorSpamGuard == null || !AmethystConfig.DoorSpamGuard.Value) return true;

            // ① 会议/放逐流程中：一切关门 RPC 直接丢弃（所有端，硬丢）
            //    用 activeInHierarchy 判断，避免会议结束后单例残留导致永久禁门。
            if (AmethystRpcGuard.InMeetingOrExileActive())
            {
                AmethystRpcGuard.NotifyOnce("door-in-meeting", AmethystText.T("DoorsInMeeting"));
                return false;
            }

            // ② 0.01 秒内连关：第 1 个放行，第 2 个起硬丢
            float now = UnityEngine.Time.unscaledTime;

            if (now - _windowStart > Window)
            {
                _windowStart = now;
                _count = 1;
                return true;
            }

            _count++;
            if (_count < Limit) return true;

            AmethystRpcGuard.NotifyOnce("door-spam", AmethystText.T("DoorSpamBlocked"));
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystDoorSpamPatch
{
    private static bool Prefix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        bool isClose = callId == (byte)RpcCalls.CloseDoorsOfType;

        if (!isClose)
        {
            // 第二条门 RPC 通道：`UpdateSystem(SystemTypes.Doors, ...)`
            if (callId != (byte)RpcCalls.UpdateSystem || reader == null) return true;

            int pos = reader.Position;
            bool isDoorSystem = false;
            try
            {
                if (reader.BytesRemaining < 1) return true;
                isDoorSystem = reader.ReadByte() == (byte)SystemTypes.Doors;
            }
            catch { isDoorSystem = false; }
            finally { try { reader.Position = pos; } catch { } }

            if (!isDoorSystem) return true;
        }

        return AmethystDoorSpamGuard.Allow();
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystDoorSpamResetPatch
{
    private static void Postfix() => AmethystDoorSpamGuard.Reset();
}
