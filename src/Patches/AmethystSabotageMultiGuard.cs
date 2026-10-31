using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystSabotageMultiGuard
{
    
    private static float _lastStartAt = -999f;

    private static readonly Dictionary<int, float> LastRepair = new Dictionary<int, float>();

    private const float StartCooldown = 2.5f;
    private const float RepairCooldown = 0.25f;

    internal static void Reset()
    {
        _lastStartAt = -999f;
        LastRepair.Clear();
    }

    internal static bool IsGuardedSystem(SystemTypes s)
    {
        return s == SystemTypes.Sabotage
            || s == SystemTypes.Reactor
            || s == SystemTypes.Laboratory
            || s == SystemTypes.LifeSupp
            || s == SystemTypes.Comms
            || s == SystemTypes.HeliSabotage
            || s == SystemTypes.Electrical
            || s == SystemTypes.MushroomMixupSabotage;
    }

    internal static bool IsStart(SystemTypes system, int amount)
    {
        if (system == SystemTypes.Sabotage) return true;
        if (system == SystemTypes.MushroomMixupSabotage) return amount == 1;
        if (amount < 0) return false;
        return (amount & 0x80) != 0;
    }

    internal static bool AcceptStart()
    {
        float now = Time.realtimeSinceStartup;
        if (now - _lastStartAt < StartCooldown) return false;

        _lastStartAt = now;
        return true;
    }

    internal static bool AcceptRepair(int ownerId, SystemTypes system)
    {
        float now = Time.realtimeSinceStartup;
        int key = (ownerId << 16) | ((int)system & 0xFFFF);

        float last;
        if (LastRepair.TryGetValue(key, out last) && now - last < RepairCooldown)
            return false;

        LastRepair[key] = now;
        return true;
    }

    internal static bool AnyMajorSabotageActive()
    {
        try { return AmethystRpcGuard.MajorSabotageActive(); }
        catch { return false; }
    }
}

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystSabotageMultiGuardPatch
{
    private static bool Prefix(byte callId, MessageReader reader)
    {
        try
        {
            if (callId != (byte)RpcCalls.UpdateSystem || reader == null) return true;

            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

            int pos = reader.Position;
            try
            {
                if (reader.BytesRemaining < 2) return true;

                SystemTypes system = (SystemTypes)reader.ReadByte();
                if (!AmethystSabotageMultiGuard.IsGuardedSystem(system)) return true;

                PlayerControl sender = null;
                try { sender = reader.ReadNetObject<PlayerControl>(); }
                catch { sender = null; }

                if (sender == PlayerControl.LocalPlayer) return true;

                int ownerId = -1;
                try { if (sender != null) ownerId = sender.OwnerId; } catch { }

                int amount = -1;
                try
                {
                    if (reader.BytesRemaining >= 1) amount = reader.ReadByte();
                }
                catch { }

                bool isStart = AmethystSabotageMultiGuard.IsStart(system, amount);

                if (isStart)
                {
                    
                    if (AmethystSabotageMultiGuard.AnyMajorSabotageActive())
                    {
                        AmethystPlugin.Logger?.LogWarning(
                            "[SabotageGuard] blocked start " + system + " (another sabotage active)");
                        return false;
                    }

                    if (!AmethystSabotageMultiGuard.AcceptStart())
                    {
                        AmethystPlugin.Logger?.LogWarning(
                            "[SabotageGuard] blocked start " + system + " (start cooldown)");
                        return false;
                    }
                }
                else
                {
                    if (!AmethystSabotageMultiGuard.AcceptRepair(ownerId, system))
                    {
                        AmethystPlugin.Logger?.LogWarning(
                            "[SabotageGuard] blocked repair " + system + " (rate limited)");
                        return false;
                    }
                }
            }
            finally
            {
                try { reader.Position = pos; } catch { }
            }
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning("[SabotageGuard] parse error: " + ex.Message);
            return true;
        }

        return true;
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystSabotageMultiGuardResetPatch
{
    private static void Postfix() => AmethystSabotageMultiGuard.Reset();
}

[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystSabotageMultiGuardStartPatch
{
    private static void Prefix() => AmethystSabotageMultiGuard.Reset();
}