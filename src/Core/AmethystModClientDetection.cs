using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Hazel;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Amethyst;

internal static class AmethystModClientDetection
{
    
    private const RpcCalls TransportRpc = RpcCalls.CancelPet;
    internal const string Marker = "AMETHYST_MOD_CLIENT_V1";
    private const string Probe = "P";
    private const string Reply = "R";

    private static readonly Dictionary<byte, string> Versions = new Dictionary<byte, string>();

    private static readonly HashSet<byte> Detected = new HashSet<byte>();

    internal static bool IsDetectedId(byte id)
    {
        try
        {
            
            if (id == AmethystPlayerCacheStore.LocalId)
                return AmethystConfig.ModClientDetection != null && AmethystConfig.ModClientDetection.Value;
            return Detected.Contains(id);
        }
        catch { return false; }
    }

    internal static string GetVersion(byte id)
    {
        try
        {
            if (id == AmethystPlayerCacheStore.LocalId) return AmethystPlugin.PluginVersion;
            if (Versions.TryGetValue(id, out string v)) return v;
        }
        catch { }
        return null;
    }

    internal static void Reset()
    {
        Detected.Clear();
        Versions.Clear();
    }

    internal static void SendProbe()
    {
        if (!AmethystConfig.ModClientDetection.Value || PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null) return;
        try
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.MyPhysics.NetId, (byte)TransportRpc, SendOption.Reliable, -1);
            writer.Write(Marker);
            writer.Write(Probe);
            writer.Write(PlayerControl.LocalPlayer.PlayerId);
            writer.Write(AmethystPlugin.PluginVersion);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        catch { }
    }

    private static void SendReply(PlayerControl target)
    {
        if (target == null || PlayerControl.LocalPlayer == null) return;
        try
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.MyPhysics.NetId, (byte)TransportRpc, SendOption.Reliable, target.OwnerId);
            writer.Write(Marker);
            writer.Write(Reply);
            writer.Write(PlayerControl.LocalPlayer.PlayerId);
            writer.Write(AmethystPlugin.PluginVersion);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        catch { }
    }

    internal static bool HandlePacket(PlayerControl sender, MessageReader reader)
    {
        if (sender == null || reader == null || sender == PlayerControl.LocalPlayer) return false;
        int position = reader.Position;
        try
        {
            if (reader.ReadString() != Marker) { reader.Position = position; return false; }
            string kind = reader.ReadString();
            byte claimedId = reader.ReadByte();
            string version = reader.ReadString();
            if (claimedId != sender.PlayerId) return true;
            Detected.Add(sender.PlayerId);
            try { Versions[sender.PlayerId] = version; } catch { }
            if (kind == Probe && AmethystConfig.ModClientDetection.Value) SendReply(sender);
            return true;
        }
        catch
        {
            try { reader.Position = position; } catch { }
            return false;
        }
    }

    internal static IEnumerator ProbeLoop()
    {
        while (true)
        {
            if (AmethystConfig.ModClientDetection.Value && AmongUsClient.Instance != null && PlayerControl.LocalPlayer != null)
                SendProbe();
            yield return new WaitForSeconds(8f);
        }
    }

    [HarmonyPatch(typeof(PlayerPhysics), "HandleRpc")]
    private static class PhysicsPatch
    {
        private static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
        {
            if (callId != (byte)TransportRpc) return true;
            PlayerControl sender = __instance != null ? __instance.myPlayer : null;
            return !HandlePacket(sender, reader);
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
    private static class JoinedPatch
    {
        private static void Postfix(AmongUsClient __instance)
        {
            Reset();
            try { __instance.StartCoroutine(ProbeLoop().WrapToIl2Cpp()); } catch { }
        }
    }
}
