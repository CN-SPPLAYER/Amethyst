using System;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystSabotageRepairGuard
{
    private static readonly Vector2[] None = Array.Empty<Vector2>();

    private static readonly Vector2[] ElecSkeld = { new Vector2(-10.05f, -10.24f) };
    private static readonly Vector2[] ElecMira = { new Vector2(14.74f, 21.08f) };
    private static readonly Vector2[] ElecPolus = { new Vector2(9.60f, -11.44f) };
    private static readonly Vector2[] ElecAirship =
    {
        new Vector2(-12.82f, -11.27f), new Vector2(30.66f, 2.08f), new Vector2(13.97f, 6.35f)
    };

    private static readonly Vector2[] O2Skeld = { new Vector2(6.52f, -6.61f), new Vector2(6.81f, -3.07f) };
    private static readonly Vector2[] O2Mira = { new Vector2(17.57f, 24.22f), new Vector2(4.03f, -0.63f) };

    private static readonly Vector2[] ReactorSkeld = { new Vector2(-21.28f, -1.69f), new Vector2(-21.28f, -8.62f) };
    private static readonly Vector2[] ReactorMira = { new Vector2(4.67f, 14.47f), new Vector2(0.24f, 14.49f) };
    private static readonly Vector2[] ReactorPolus = { new Vector2(24.40f, -3.05f), new Vector2(4.43f, -3.91f) };
    private static readonly Vector2[] ReactorAirship = { new Vector2(3.89f, 9.76f), new Vector2(11.49f, 6.35f) };
    private static readonly Vector2[] ReactorFungle = { new Vector2(20.95f, -6.01f), new Vector2(23.98f, -7.67f) };

    private static readonly Vector2[] CommsSkeld = { new Vector2(4.27f, -16.39f) };
    private static readonly Vector2[] CommsMira =
    {
        new Vector2(15.11f, 5.17f), new Vector2(13.79f, 18.93f),
        new Vector2(13.68f, 19.97f), new Vector2(14.46f, 19.24f)
    };
    private static readonly Vector2[] CommsPolus = { new Vector2(13.86f, -15.30f) };
    private static readonly Vector2[] CommsAirship = { new Vector2(-13.98f, 2.17f) };
    private static readonly Vector2[] CommsFungle = { new Vector2(24.57f, 13.76f), new Vector2(8.25f, 0.43f) };

    internal static bool On
    {
        get
        {
            try { return AmethystConfig.CheckSabotageRepair != null && AmethystConfig.CheckSabotageRepair.Value; }
            catch { return false; }
        }
    }

    internal static float Limit
    {
        get
        {
            try
            {
                float v = AmethystConfig.SabotageRepairDistance != null
                    ? AmethystConfig.SabotageRepairDistance.Value
                    : 3.5f;
                return Mathf.Clamp(v, 0.5f, 20f);
            }
            catch { return 3.5f; }
        }
    }

    internal static bool IsRepair(int sys, int amount)
    {
        switch ((SystemTypes)sys)
        {
            case SystemTypes.Electrical:
                return amount >= 0 && amount <= 4;

            case SystemTypes.LifeSupp:
            case SystemTypes.Reactor:
            case SystemTypes.Laboratory:
            case SystemTypes.HeliSabotage:
            case SystemTypes.Comms:
                return amount == 16 || amount == 17;

            default:
                return false;
        }
    }

    internal static Vector2[] Panels(int map, int sys)
    {
        switch ((SystemTypes)sys)
        {
            case SystemTypes.Electrical:
                switch (map)
                {
                    case 0: return ElecSkeld;
                    case 1: return ElecMira;
                    case 2: return ElecPolus;
                    case 4: return ElecAirship;
                    default: return None;   
                }

            case SystemTypes.LifeSupp:
                switch (map)
                {
                    case 0: return O2Skeld;
                    case 1: return O2Mira;
                    default: return None;   
                }

            case SystemTypes.Reactor:
            case SystemTypes.Laboratory:
            case SystemTypes.HeliSabotage:
                switch (map)
                {
                    case 0: return ReactorSkeld;
                    case 1: return ReactorMira;
                    case 2: return ReactorPolus;
                    case 4: return ReactorAirship;
                    case 5: return ReactorFungle;
                    default: return None;   
                }

            case SystemTypes.Comms:
                switch (map)
                {
                    case 0: return CommsSkeld;
                    case 1: return CommsMira;
                    case 2: return CommsPolus;
                    case 4: return CommsAirship;
                    case 5: return CommsFungle;
                    default: return None;   
                }

            default:
                return None;
        }
    }

    internal static bool NearConsole(int map, int sys, Vector2 pos, out float nearest)
    {
        nearest = float.MaxValue;
        Vector2[] list = Panels(map, sys);
        if (list == null || list.Length == 0) return true;   

        for (int i = 0; i < list.Length; i++)
        {
            float d = Vector2.Distance(pos, list[i]);
            if (d < nearest) nearest = d;
        }
        return nearest <= Limit;
    }
}

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystSabotageRepairGuardPatch
{
    private static bool Prefix(ShipStatus __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        try
        {
            if (!AmethystSabotageRepairGuard.On) return true;
            if (__instance == null || reader == null) return true;
            if (callId != (byte)RpcCalls.UpdateSystem) return true;
            
            if (!AmethystRpcGuard.AmHost()) return true;

            int pos = reader.Position;
            bool tooFar = false;
            PlayerControl actor = null;

            try
            {
                if (reader.BytesRemaining < 2) return true;

                int sys = reader.ReadByte();
                actor = reader.ReadNetObject<PlayerControl>();

                if (actor == null) return true;
                
                if (AmethystColors.SamePlayer(actor, PlayerControl.LocalPlayer)) return true;

                if (reader.BytesRemaining < 1) return true;
                int amount = reader.ReadByte();

                if (!AmethystSabotageRepairGuard.IsRepair(sys, amount)) return true;

                int map = AmethystRpcGuard.CurrentMapId();
                if (map < 0) return true;

                Vector2 at = actor.transform.position;
                tooFar = !AmethystSabotageRepairGuard.NearConsole(map, sys, at, out _);
            }
            finally
            {
                try { reader.Position = pos; } catch { }
            }

            if (tooFar)
            {
                
                AmethystRpcGuard.Flag(actor, AmethystText.T("SabotageRepairTooFar"), false);
                
                return !AmethystRpcGuard.ShouldDropRpc();
            }
        }
        catch (Exception ex)
        {
            try { AmethystPlugin.Logger?.LogWarning("[SabotageRepairGuard] " + ex.Message); } catch { }
        }

        return true;
    }
}
