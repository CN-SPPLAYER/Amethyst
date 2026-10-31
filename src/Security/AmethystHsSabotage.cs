using Hazel;
using HarmonyLib;
using InnerNet;
using Amethyst.Patches;

namespace Amethyst;

internal static class AmethystHsSabotage
{
    internal static bool On()
    {
        try
        {
            if (AmongUsClient.Instance == null) return false;
            if (!AmethystRpcGuard.IsHS()) return false;
            bool hsToggle = AmethystConfig.HsSabotageGuard != null && AmethystConfig.HsSabotageGuard.Value;
            bool master = AmethystConfig.RpcGuard != null && AmethystConfig.RpcGuard.Value;
            return hsToggle || master;
        }
        catch { return false; }
    }
}

[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class AmethystHsSabotagePlayer
{
    private static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!AmethystHsSabotage.On() || __instance == null || __instance == PlayerControl.LocalPlayer || __instance.Data == null || reader == null) return true;

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        try
        {
            if (callId == (byte)RpcCalls.ReportDeadBody)
            {
                cheat = true;
                reason = AmethystText.T("ReportInHS");
            }
            
            else if (callId == (byte)RpcCalls.StartMeeting)
            {
                cheat = true;
                reason = AmethystText.T("MeetingInHS");
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat)
        {
            AmethystRpcGuard.Flag(__instance, reason);
            return !AmethystRpcGuard.ShouldDropRpc();
        }
        return true;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), "HandleRpc")]
internal static class AmethystHsSabotagePhysics
{
    private static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId)
    {
        if (!AmethystHsSabotage.On() || __instance == null) return true;
        if (callId != (byte)RpcCalls.EnterVent && callId != (byte)RpcCalls.ExitVent) return true;

        PlayerControl actor = __instance.myPlayer;
        if (actor == null || actor == PlayerControl.LocalPlayer || actor.Data == null) return true;

        try { if (actor.Data.IsDead) return true; } catch { }

        if (AmethystRpcGuard.IsImp(actor))
        {
            AmethystRpcGuard.Flag(actor,
                AmethystText.T("VentingAsSeekerInHS"));
            return !AmethystRpcGuard.ShouldDropRpc();
        }
        return true;
    }
}

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystHsSabotageShip
{
    private static bool Prefix(ShipStatus __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!AmethystHsSabotage.On() || __instance == null || reader == null) return true;

        int pos = reader.Position;
        bool block = false;
        PlayerControl actor = null;
        string reason = null;
        try
        {
            if (callId == (byte)RpcCalls.UpdateSystem)
            {
                int sys = reader.ReadByte();
                actor = reader.ReadNetObject<PlayerControl>();

                if (sys == (byte)SystemTypes.Ventilation)
                {
                    
                    reader.ReadUInt16();
                    if (reader.Position < reader.Length) reader.ReadByte();
                }
                else
                {
                    int target = sys;
                    if (sys == (byte)SystemTypes.Sabotage && reader.Position < reader.Length)
                        target = reader.ReadByte();

                    if (AmethystRpcGuard.SabSystems.Contains(target))
                    {
                        block = true;
                        reason = AmethystText.T("SabotageInHS");
                    }
                }
            }
            else if (callId == (byte)RpcCalls.CloseDoorsOfType)
            {
                if (AmethystRpcGuard.IsHS())
                {
                    block = true;
                    reason = AmethystText.T("DoorsInHS_2");
                }
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (block)
        {
            if (actor != null) AmethystRpcGuard.Flag(actor, reason);
            else AmethystRpcGuard.NotifyOnce("hs-nobody", reason);
            
            return false;
        }
        return true;
    }
}
