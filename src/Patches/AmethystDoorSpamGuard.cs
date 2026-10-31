using HarmonyLib;
using Hazel;
using InnerNet;

namespace Amethyst.Patches;

internal static class AmethystDoorSpamGuard
{
    private const float Window = 0.01f;
    private const int Limit = 2;      

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

            if (AmethystRpcGuard.InMeetingOrExileActive())
            {
                AmethystRpcGuard.NotifyOnce("door-in-meeting", AmethystText.T("DoorsInMeeting"));
                return false;
            }

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
