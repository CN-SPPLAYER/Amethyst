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
    // Use a vanilla RPC as the transport so the host relays it in non-host lobbies.
    // The marker makes the payload distinct from a real CancelPet call.
    private const RpcCalls TransportRpc = RpcCalls.CancelPet;
    internal const string Marker = "AMETHYST_MOD_CLIENT_V1";
    private const string Probe = "P";
    private const string Reply = "R";

    // 玩家 id -> 对方报上的 Amethyst 版本号（用于头顶 `Ame{Version} 用户` 标签）。
    // 自身也显示一条：显示自己的版本。
    private static readonly Dictionary<byte, string> Versions = new Dictionary<byte, string>();

    private static readonly HashSet<byte> Detected = new HashSet<byte>();

    // 第 180 轮：按玩家 id 判断是否要显示头顶标签。
    // 供 `AmethystPlayerOverlay` 调用（它每帧已经拿到了 id，
    // 不必再从 PlayerControl 反读一次 —— 那是跨 IL2CPP 边界的原生读取）。
    internal static bool IsDetectedId(byte id)
    {
        try
        {
            // 自己也显示：本机就是 Amethyst 客户端，开关开启时同样在头顶展示标签，而非只显示别人
            if (id == AmethystPlayerCacheStore.LocalId)
                return AmethystConfig.ModClientDetection != null && AmethystConfig.ModClientDetection.Value;
            return Detected.Contains(id);
        }
        catch { return false; }
    }

    // 取某名玩家报上的 Amethyst 版本号。
    //
    // ⚠ 自己这一端**不能**靠字典：本地玩家自己发出的 probe 会被 `HandlePacket`
    //   直接忽略（`sender == LocalPlayer`），所以 `Versions[LocalId]` 永远不会被网络填上。
    //   而且 `OnGameJoined` 可能早于 `LocalId` 就绪，靠那一刻注册也不可靠。
    //   ⇒ 自己直接返回本机插件版本，其它玩家才查字典。
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

// 头顶「♥ Amethyst ♥」标签已并入 `Patches\AmethystPlayerOverlay.cs`
// （第 180 轮：与活着/死后身份标签合并成一个组件，10 人局 30 个 LateUpdate → 10 个）。
// 本文件只保留握手协议与数据层。
