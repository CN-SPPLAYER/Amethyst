using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static class AmethystSpawnFlood
{
    private const int CapPerFrame = 200;
    private const float GraceSeconds = 5f;

    private static int _frame = -1;
    private static int _count;
    private static float _lastNote;
    private static float _graceUntil;

    internal static bool On
    {
        get
        {
            try { return AmethystConfig.SpawnFloodGuard != null && AmethystConfig.SpawnFloodGuard.Value; }
            catch { return false; }
        }
    }

    internal static void Grace()
    {
        _graceUntil = Time.unscaledTime + GraceSeconds;
    }

    internal static bool Allow()
    {
        if (Time.unscaledTime < _graceUntil) return true;
        int f = Time.frameCount;
        if (f != _frame)
        {
            _frame = f;
            _count = 0;
        }
        _count++;
        return _count <= CapPerFrame;
    }

    internal static Il2CppSystem.Collections.IEnumerator Drop()
    {
        if (Time.unscaledTime - _lastNote >= 1f)
        {
            _lastNote = Time.unscaledTime;
            AmethystSecurityNotify.Fire("SpawnFloodTrimmed");
        }
        return Empty().WrapToIl2Cpp();
    }

    private static IEnumerator Empty()
    {
        yield break;
    }
}

[HarmonyPatch(typeof(InnerNetClient), "CoHandleSpawn")]
internal static class AmethystSpawnFloodPatch
{
    public static bool Prefix(ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (!AmethystSpawnFlood.On || AmethystSpawnFlood.Allow()) return true;
        __result = AmethystSpawnFlood.Drop();
        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystSpawnFloodJoinPatch
{
    public static void Prefix() => AmethystSpawnFlood.Grace();
}

[HarmonyPatch(typeof(LobbyBehaviour), "Start")]
internal static class AmethystSpawnFloodLobbyPatch
{
    public static void Prefix() => AmethystSpawnFlood.Grace();
}

[HarmonyPatch(typeof(ShipStatus), "Start")]
internal static class AmethystSpawnFloodShipPatch
{
    public static void Prefix() => AmethystSpawnFlood.Grace();
}
