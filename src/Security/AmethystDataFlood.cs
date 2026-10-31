using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static class AmethystDataFlood
{
    private const float PhantomAfter = 3f;   
    private const int PerSec = 40;           
    private const int MaxTracked = 1024;     

    private static readonly Dictionary<uint, (float first, float last)> Seen =
        new Dictionary<uint, (float, float)>();
    private static readonly List<uint> Stale = new List<uint>();

    private static Il2CppSystem.Collections.IEnumerator _skip;
    private static float _windowAt;
    private static float _sweepAt;
    private static float _graceUntil;
    private static float _lastNote;
    private static int _hits;

    internal static bool On => AmethystConfig.BlockDataFlood != null && AmethystConfig.BlockDataFlood.Value;

    internal static void Reset()
    {
        Seen.Clear();
        _hits = 0;
        _graceUntil = Time.unscaledTime + 5f;   
    }

    internal static bool Check(InnerNetClient net, MessageReader reader)
    {
        if (reader == null || reader.Tag != 1) return false;   

        int pos = reader.Position;
        uint id;
        try { id = reader.ReadPackedUInt32(); }
        catch { try { reader.Position = pos; } catch { } return true; }   
        try { reader.Position = pos; } catch { }

        if (id == 0) return true;

        try
        {
            if (net != null && net.allObjects != null && net.allObjects.AllObjectsFast != null
                && net.allObjects.AllObjectsFast.ContainsKey(id))
                return false;
            if (net != null && net.DestroyedObjects != null && net.DestroyedObjects.Contains(id))
                return false;
        }
        catch { }

        float now = Time.unscaledTime;
        if (now >= _sweepAt && Seen.Count > 0) Sweep(net, now);

        if (Seen.TryGetValue(id, out var t))
        {
            Seen[id] = (t.first, now);
            
            if (now - t.first > PhantomAfter) return true;
        }
        else
        {
            if (Seen.Count >= MaxTracked) return true;
            Seen[id] = (now, now);
        }

        if (now - _windowAt >= 1f)
        {
            _windowAt = now;
            _hits = 0;
        }
        return ++_hits > PerSec && now >= _graceUntil;
    }

    internal static Il2CppSystem.Collections.IEnumerator Skip()
    {
        if (Time.unscaledTime - _lastNote >= 1f)
        {
            _lastNote = Time.unscaledTime;
            try { AmethystSecurityNotify.Fire("TrimmedDataFlood"); } catch { }
        }
        return _skip ??= Empty().WrapToIl2Cpp();
    }

    private static void Sweep(InnerNetClient net, float now)
    {
        _sweepAt = now + 2f;
        Stale.Clear();
        foreach (var kv in Seen)
        {
            bool known = false;
            try
            {
                known = net != null && net.allObjects != null && net.allObjects.AllObjectsFast != null
                        && net.allObjects.AllObjectsFast.ContainsKey(kv.Key);
            }
            catch { }
            if (now - kv.Value.last > 10f || known) Stale.Add(kv.Key);
        }
        for (int i = 0; i < Stale.Count; i++) Seen.Remove(Stale[i]);
    }

    private static IEnumerator Empty()
    {
        yield break;
    }
}

[HarmonyPatch(typeof(InnerNetClient), "HandleGameDataInner")]
internal static class AmethystDataFloodPatch
{
    public static bool Prefix(InnerNetClient __instance, MessageReader reader,
                             ref Il2CppSystem.Collections.IEnumerator __result)
    {
        try
        {
            if (!AmethystDataFlood.On) return true;
            if (reader == null || !AmethystDataFlood.Check(__instance, reader)) return true;

            __result = AmethystDataFlood.Skip();
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystDataFloodJoinPatch
{
    public static void Prefix() => AmethystDataFlood.Reset();
}
