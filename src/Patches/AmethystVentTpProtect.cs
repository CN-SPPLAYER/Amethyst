using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystVentTpProtect
{
    private static float _lastToast;

    internal static bool On()
    {
        if (AmongUsClient.Instance == null) return false;

        return AmethystConfig.VentTpProtect != null && AmethystConfig.VentTpProtect.Value;
    }

    internal static void Notify(string message)
    {
        float now = Time.unscaledTime;
        if (now - _lastToast < 2f) return;
        _lastToast = now;
        AmethystToast.Push(AmethystText.T("Guard"), message, 5.0f, AmethystNotifyKind.Warning);
    }

    internal static string Name(PlayerControl pc)
        => pc != null && pc.Data != null && pc.Data.PlayerName != null ? pc.Data.PlayerName : "?";

    internal static bool IsLegitVentClean(int ventId)
    {
        return IsLegitVentClean(ventId, null, false);
    }

    internal static bool IsLegitVentClean(int ventId, PlayerControl actor)
    {
        return IsLegitVentClean(ventId, actor, false);
    }

    internal static bool IsLegitVentClean(int ventId, PlayerControl actor, bool detect)
    {
        try
        {
            
            if (actor != null && HasCleanVentTask(actor))
            {
                Diag(detect, ventId, "legit: actor has CleanVent task");
                return true;
            }

            if (ventId < 0) { Diag(detect, ventId, "ventId<0"); return false; }

            ShipStatus ship = ShipStatus.Instance;
            if (ship == null) { Diag(detect, ventId, "ShipStatus==null"); return false; }
            if (ship.Systems == null) { Diag(detect, ventId, "Systems==null"); return false; }
            if (!ship.Systems.TryGetValue(SystemTypes.Ventilation, out ISystemType sysType) || sysType == null)
            { Diag(detect, ventId, "no Ventilation system"); return false; }

            VentilationSystem vs = sysType.TryCast<VentilationSystem>();
            if (vs == null) { Diag(detect, ventId, "cast failed"); return false; }

            int cleaningCount = -1;
            bool cleaning = false;
            if (vs.PlayersCleaningVents != null)
            {
                cleaningCount = vs.PlayersCleaningVents.Count;
                var it = vs.PlayersCleaningVents.GetEnumerator();
                while (it.MoveNext())
                {
                    if (it.Current.Value == ventId) { cleaning = true; break; }
                }
            }
            if (!cleaning && vs.IsVentCurrentlyBeingCleaned(ventId)) cleaning = true;

            int insideCount = -1;
            bool inside = false;
            if (vs.PlayersInsideVents != null)
            {
                insideCount = vs.PlayersInsideVents.Count;
                var it2 = vs.PlayersInsideVents.GetEnumerator();
                while (it2.MoveNext())
                {
                    if (it2.Current.Value == ventId) { inside = true; break; }
                }
            }
            if (!inside && vs.IsImpostorInsideVent(ventId)) inside = true;

            if (!cleaning || !inside)
            {
                
                Diag(detect, ventId, $"A=false B: cleaning={cleaning}(n={cleaningCount}) inside={inside}(n={insideCount}) amHost={AmethystRpcGuard.AmHost()} -> 放行(疑)");
                return true;
            }

            Diag(detect, ventId, $"legit: cleaning + inside (n={cleaningCount}/{insideCount})");
            return true;
        }
        catch (System.Exception ex) { Diag(detect, ventId, "EX " + ex.GetType().Name + ": " + ex.Message); return true; }
    }

    internal static bool HasCleanVentTask(PlayerControl pc)
    {
        try
        {
            if (pc == null || pc.Data == null || pc.Data.Tasks == null) return false;
            var tasks = pc.Data.Tasks;
            if (tasks.Count <= 0) return false;
            for (int i = 0; i < tasks.Count; i++)
            {
                
                if ((int)tasks[i].TypeId == (int)TaskTypes.VentCleaning) return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static float _lastDiag;

    private static void Diag(bool on, int ventId, string msg)
    {
        if (!on) return;
        try
        {
            float now = Time.unscaledTime;
            if (now - _lastDiag < 2f) return;
            _lastDiag = now;
            AmethystPlugin.Logger?.LogInfo($"[VentClean] ventId={ventId} {msg}");
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), "HandleRpc")]
internal static class AmethystVentTpProtectPhysics
{
    private static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId)
    {
        try
        {
            if (__instance == null || callId != (byte)RpcCalls.BootFromVent) return true;
            if (!AmethystVentTpProtect.On() || AmongUsClient.Instance == null) return true;

            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || __instance.myPlayer != me) return true;
            if (me.inVent) return true;

            AmethystVentTpProtect.Notify(AmethystText.T("AbnormalRPCVentTeleportVentBoot"));
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystVentTpProtectShip
{
    private static bool Prefix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        try
        {
            if (callId != (byte)RpcCalls.UpdateSystem || reader == null) return true;
            if (!AmethystVentTpProtect.On()) return true;
            if (AmongUsClient.Instance == null) return true;

            int pos = reader.Position;
            bool block = false;
            PlayerControl actor = null;
            try
            {
                int sys = reader.ReadByte();
                if (sys != (int)SystemTypes.Ventilation) return true;
                actor = reader.ReadNetObject<PlayerControl>();
                reader.ReadUInt16();
                int op = reader.Position < reader.Length ? reader.ReadByte() : -1;
                
                int ventId = reader.Position < reader.Length ? reader.ReadByte() : -1;

                if (op == (int)VentilationSystem.Operation.BootImpostors)
                {
                    
                    if (AmethystVentTpProtect.IsLegitVentClean(ventId)) return true;   
                }
            }
            finally { try { reader.Position = pos; } catch { } }

            return !block;
        }
        catch { return true; }
    }
}
