using Hazel;
using HarmonyLib;
using InnerNet;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(MeetingHud), "HandleRpc")]
internal static class AmethystVotingCompleteOverloadPatch
{
    private static bool Prefix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        
        if (AmethystConfig.AntiOverloadEnabled == null || !AmethystConfig.AntiOverloadEnabled.Value) return true;
        if (!AmethystConfig.BlockVotingCompleteOverload.Value) return true;
        if (callId != (byte)RpcCalls.VotingComplete || reader == null) return true;

        int pos = reader.Position;
        try
        {
            
            int arrayLength = reader.ReadPackedInt32();
            if (arrayLength > 1024 || arrayLength > reader.BytesRemaining)
                return false; 
        }
        catch { return true; }
        finally { try { reader.Position = pos; } catch { } }

        return true;
    }
}
