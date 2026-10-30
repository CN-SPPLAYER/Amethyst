using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst;

// 模组客户端检测：通过 RPC ID 识别不同的模组/作弊菜单
// 参考 SickoMenu 的 SMAC 检测逻辑
internal static class AmethystKnownModDetection
{
    // 已知模组客户端的 RPC ID (byte 范围 0-255)
    internal static readonly HashSet<byte> SickoMenuRpcs = new HashSet<byte> { 250 }; // SickoMenu 使用 RPC 250
    internal static readonly HashSet<byte> BetterAmongUsRpcs = new HashSet<byte> { 151, 152 };
    internal static readonly HashSet<byte> HostGuardRpcs = new HashSet<byte> { 176 };
    internal static readonly HashSet<byte> ChocooMenuRpcs = new HashSet<byte> { 121 };
    internal static readonly HashSet<byte> SlopMenuCrewRpcs = new HashSet<byte> { 201, 202 };
    internal static readonly HashSet<byte> UnknownModRpcs = new HashSet<byte> { 103 };

    // ⚠ 第 219 轮 #3：补上 OnyxMenu `Security\ForeignMods.cs` 已知表里我们缺的条目。
    //   两边对同一个 callId 各自主张不同模组的，**一律保留我们原有的名字**：
    //     · 250 → 我们 SickoMenu / OnyxMenu KillNetwork
    //     · 103 → 我们 UnknownMod / OnyxMenu ASKIN
    //   这个冲突本身就说明"按 RPC ID 认模组"天生不可靠（ID 是各模组自己挑的，会撞车），
    //   所以下面还加了**通用启发式**：优先采信模组自己报上来的 <签名> <版本>。
    internal static readonly HashSet<byte> SickoMenuAltRpcs = new HashSet<byte> { 164 };
    internal static readonly HashSet<byte> BetterAmongUsAltRpcs = new HashSet<byte> { 150 };
    internal static readonly HashSet<byte> GaffMenuRpcs = new HashSet<byte> { 144, 145 };
    internal static readonly HashSet<byte> PolarClientRpcs = new HashSet<byte> { 195, 204 };
    internal static readonly HashSet<byte> BanModRpcs = new HashSet<byte> { 219, 240 };

    // 所有已知模组 RPC
    internal static readonly HashSet<byte> AllKnownModRpcs = new HashSet<byte>();

    // 已检测到的玩家：playerId -> 已检测到的 Mod 名称列表（带发现时间）
    private static readonly Dictionary<byte, Dictionary<string, float>> DetectedMods = new Dictionary<byte, Dictionary<string, float>>();

    static AmethystKnownModDetection()
    {
        foreach (var id in SickoMenuRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in BetterAmongUsRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in HostGuardRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in ChocooMenuRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in SlopMenuCrewRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in UnknownModRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in SickoMenuAltRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in BetterAmongUsAltRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in GaffMenuRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in PolarClientRpcs) AllKnownModRpcs.Add(id);
        foreach (var id in BanModRpcs) AllKnownModRpcs.Add(id);
    }

    internal static void Reset()
    {
        DetectedMods.Clear();
        Claimed.Clear();
        UnknownHits.Clear();
        LoggedRaw.Clear();
    }

    internal static string GetDetectedModName(PlayerControl player)
    {
        if (player == null) return null;
        if (DetectedMods.TryGetValue(player.PlayerId, out var dict) && dict != null && dict.Count > 0)
        {
            var names = new List<string>(dict.Keys);
            return string.Join("+", names);
        }
        return null;
    }

    // 通用启发式的门槛：原版 RpcCalls 都 < 90，≥ 90 基本可断定是模组自己挑的 ID。
    private const byte VanillaTop = 90;

    // "已经自称过名字的玩家" 与 "未知高 ID 的命中次数"，都随对局重置。
    private static readonly Dictionary<byte, string> Claimed = new Dictionary<byte, string>();
    private static readonly Dictionary<int, int> UnknownHits = new Dictionary<int, int>();

    // ⚠ 第 220 轮：诊断 —— 每个玩家只打**一次**"确实收到了高 ID RPC"的日志。
    //   目的：用户问"外来模组识别到底有没有在工作"，而"没识别出来"有两种完全不同的原因：
    //     ① 补丁压根没被调用（挂错方法 / 开关关着 / 被别的 Prefix 短路）；
    //     ② 补丁跑了，但这个 RPC 既不在已知表里、也不自称名字。
    //   这一行能直接把 ① 和 ② 分开 —— 有这行 = 补丁在跑；没有 = 补丁没跑到。
    private static readonly HashSet<byte> LoggedRaw = new HashSet<byte>();

    // 两个 `HandleRpc` 补丁共用的入口：门控 → 诊断 → 三档识别。
    internal static void Observe(PlayerControl sender, byte callId, MessageReader reader)
    {
        try
        {
            if (AmethystConfig.ForeignModDetect == null || !AmethystConfig.ForeignModDetect.Value) return;
            if (AmongUsClient.Instance == null) return;
            if (sender == null || sender == PlayerControl.LocalPlayer) return;
            if (sender.Data == null) return;

            if (callId >= VanillaTop && LoggedRaw.Add(sender.PlayerId))
            {
                // 只在第一次真的需要打日志时才算名字（避免每个包都去取颜色/名字）
                string who = SafeName(sender);
                AmethystPlugin.Logger?.LogInfo(
                    $"[ModDetect] 外来模组识别在跑：收到 {who} 的高 ID RPC {callId}（原版最大 {VanillaTop - 1}）");
            }

            CheckRpc(sender, callId, reader);
        }
        catch { }
    }

    private static string SafeName(PlayerControl p)
    {
        try { return AmethystColors.Name(p) ?? (p.Data != null ? p.Data.PlayerName : "#" + p.PlayerId); }
        catch { return "#" + (p != null ? p.PlayerId.ToString() : "?"); }
    }

    // ===== 第 219 轮 #3：三档识别（移植 OnyxMenu `Security\ForeignMods.cs` 的思路）=====
    //
    //   ① **模组自称**（最可信）——RPC ≥ 90 时按约定读 `(签名, 自己的 playerId, 版本号)`；
    //      自己报的名字不会撞车，所以优先采信。
    //   ② **固定 ID 表**（其次）——就是我们原有那 6 条 + 本轮从 OnyxMenu 补的 5 条。
    //   ③ **未知高 ID 兜底**（最后）——同一个 (玩家, callId) 出现 2 次 ⇒ 报"未知模组 (callId)"。
    //      只知道"他发了原版不可能有的 RPC"，不知道是哪个模组，但已足够提示房主。
    internal static void CheckRpc(PlayerControl sender, byte callId, MessageReader reader)
    {
        if (sender == null || sender == PlayerControl.LocalPlayer) return;
        if (sender.Data == null) return;

        string modName = ClaimedName(sender, callId, reader);
        if (modName == null) modName = TableName(callId);
        if (modName == null) modName = UnknownName(sender.PlayerId, callId);
        if (modName == null) return;

        if (!DetectedMods.TryGetValue(sender.PlayerId, out var playerMods) || playerMods == null)
        {
            playerMods = new Dictionary<string, float>();
            DetectedMods[sender.PlayerId] = playerMods;
        }

        // 去重：同一玩家短时间内相同 Mod 不重复弹 Toast
        if (playerMods.TryGetValue(modName, out float lastTime) && Time.realtimeSinceStartup - lastTime < 5f) return;

        playerMods[modName] = Time.realtimeSinceStartup;

        string playerName = AmethystColors.Name(sender) ?? (sender.Data != null ? sender.Data.PlayerName : $"#{sender.OwnerId}");
        AmethystToast.Push(
            AmethystText.T("ModClient"),
            AmethystText.T("Using", playerName, modName),
            5.0f, AmethystNotifyKind.Warning);

        AmethystPlugin.Logger?.LogInfo($"[ModDetect] {playerName} detected using {modName} (RPC {callId})");
    }

    // ===== ① 模组自称 =====
    // 约定（OnyxMenu 观察到的多个模组共同做法）：RPC 体 = string 签名 / byte 自己的 playerId / string 版本。
    // 校验三点才算数：playerId 对得上、签名不空且 ≤24、版本像版本号（含数字和点）。
    // ⚠ 读完**必须**还原 reader 位置：这个 reader 后面还要交给游戏自己的 HandleRpc 用。
    //   这里用 `MessageReader.Get(reader)` 拿一份副本，既不动原 reader 的位置，也不用手工回滚。
    private static string ClaimedName(PlayerControl sender, byte callId, MessageReader reader)
    {
        if (reader == null || callId < VanillaTop) return null;
        if (Claimed.ContainsKey(sender.PlayerId)) return null;   // 已经认出来了就别再解析

        MessageReader copy = null;
        try
        {
            copy = MessageReader.Get(reader);

            string sig;
            byte claimed;
            string ver;
            try
            {
                sig = copy.ReadString();
                claimed = copy.ReadByte();
                ver = copy.ReadString();
            }
            catch { return null; }

            if (claimed != sender.PlayerId) return null;
            if (string.IsNullOrEmpty(sig) || sig.Length > 24) return null;
            if (ver == null || ver.Length > 16) return null;

            string name;
            if (sig.IndexOf("MMC", StringComparison.OrdinalIgnoreCase) >= 0)
                name = "ModMenuCrew " + ver;
            else if (LooksVersion(ver))
                name = sig + " " + ver;
            else
                return null;

            Claimed[sender.PlayerId] = name;
            return name;
        }
        catch { return null; }
        finally { try { copy?.Recycle(); } catch { } }
    }

    // 版本号长得像 "1.2.3" / "v2.0" / "0.5.0-beta"：含数字、含点，其余只允许字母/数字/点/横线/下划线。
    private static bool LooksVersion(string s)
    {
        if (s == null || s.Length < 3 || s.Length > 16) return false;

        bool digit = false, dot = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '0' && c <= '9') digit = true;
            else if (c == '.') dot = true;
            else if (!char.IsLetter(c) && c != '-' && c != '_') return false;
        }
        return digit && dot;
    }

    // ===== ② 固定 ID 表 =====
    private static string TableName(byte callId)
    {
        if (SickoMenuRpcs.Contains(callId) || SickoMenuAltRpcs.Contains(callId)) return "SickoMenu";
        if (BetterAmongUsRpcs.Contains(callId) || BetterAmongUsAltRpcs.Contains(callId)) return "BetterAmongUs";
        if (HostGuardRpcs.Contains(callId)) return "HostGuard";
        if (ChocooMenuRpcs.Contains(callId)) return "ChocooMenu";
        if (SlopMenuCrewRpcs.Contains(callId)) return "SlopMenuCrew";
        if (UnknownModRpcs.Contains(callId)) return "UnknownMod";
        if (GaffMenuRpcs.Contains(callId)) return "Gaff Menu";
        if (PolarClientRpcs.Contains(callId)) return "Polar Client";
        if (BanModRpcs.Contains(callId)) return "BanMod";
        return null;
    }

    // ===== ③ 未知高 ID 兜底 =====
    // 见两次才报：一次可能是误读/半包，两次基本可以确定对方在发这个 ID。
    private static string UnknownName(byte pid, byte callId)
    {
        if (callId < VanillaTop) return null;

        int key = (pid << 8) | callId;
        UnknownHits.TryGetValue(key, out int hits);
        hits++;
        UnknownHits[key] = hits;

        return hits >= 2 ? AmethystText.T("UnknownMod") + " (" + callId + ")" : null;
    }
}

// 进新房间时清掉检测状态（原来 `AmethystKnownModDetection.Reset()` 定义了却**没有调用方**，
// 检测结果会跨局残留；本轮补上这个 Postfix，顺带清掉新增的两张表）。
[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystKnownModDetectionResetPatch
{
    private static void Postfix() => AmethystKnownModDetection.Reset();
}

// 在 PlayerControl.HandleRpc 中检测模组客户端
[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class AmethystKnownModDetectionPatch
{
    private static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
        => AmethystKnownModDetection.Observe(__instance, callId, reader);
}

// ⚠ 第 220 轮补齐：**PlayerPhysics.HandleRpc 也必须挂**。
//
//   很多模组（**包括本模组自己的握手**）是把自定义 RPC 发到 **PlayerPhysics 的 netId** 上的：
//   `AmethystModClientDetection.SendProbe()` 用的就是 `LocalPlayer.MyPhysics.NetId`。
//   这类包只会走 `PlayerPhysics.HandleRpc`，**根本不经过 `PlayerControl.HandleRpc`** ——
//   所以第 219 轮只挂 PlayerControl 时，凡是学这套写法的外来模组一个都认不出来。
//
//   参考：本模组 `AmethystModClientDetection.PhysicsPatch`（收自己的握手）、
//         `AmethystRpcGuardVent`、`AmethystHsSabotage` 也都挂在 PlayerPhysics 上。
//   （OnyxMenu 的 ForeignMods 同样只挂了 PlayerControl —— 这是它的一个覆盖盲区，我们补上。）
[HarmonyPatch(typeof(PlayerPhysics), "HandleRpc")]
internal static class AmethystKnownModDetectionPhysicsPatch
{
    private static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        PlayerControl sender = __instance != null ? __instance.myPlayer : null;
        AmethystKnownModDetection.Observe(sender, callId, reader);
    }
}

// 在 ShipStatus.HandleRpc 中检测模组客户端
[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystKnownModDetectionShipPatch
{
    private static void Prefix(ShipStatus __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!AmethystConfig.RpcGuard.Value || AmongUsClient.Instance == null) return;
        // ShipStatus RPC 中没有 sender 信息，跳过
    }
}

// 好友代码黑名单检测
internal static class AmethystFriendCodeCheck
{
    private static readonly HashSet<string> BlockedFriendCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    internal static void Reset()
    {
        BlockedFriendCodes.Clear();
    }

    internal static bool IsBlocked(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode)) return false;
        return BlockedFriendCodes.Contains(friendCode.Trim().ToLowerInvariant());
    }

    internal static void AddBlocked(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode)) return;
        BlockedFriendCodes.Add(friendCode.Trim().ToLowerInvariant());
    }

    // 从配置文件和云端名单加载封禁列表
    internal static void LoadFromConfig()
    {
        Reset();
        if (AmethystConfig.AccessBanEnabled != null && AmethystConfig.AccessBanEnabled.Value && AmethystConfig.BanList?.Value != null)
        {
            foreach (var item in AmethystConfig.BanList.Value.Split(';'))
            {
                var parts = item.Split('|');
                if (parts.Length >= 2)
                {
                    string fc = parts[1].Trim();
                    if (!string.IsNullOrWhiteSpace(fc))
                        AddBlocked(fc);
                }
            }
        }

        if (AmethystConfig.CloudBanEnabled != null && AmethystConfig.CloudBanEnabled.Value)
        {
            var cloudEntries = AmethystAccess.CloudBanEntries;
            if (cloudEntries != null)
            {
                for (int i = 0; i < cloudEntries.Count; i++)
                {
                    var e = cloudEntries[i];
                    if (!string.IsNullOrWhiteSpace(e.Code))
                        AddBlocked(e.Code);
                }
            }
        }
    }
}

// 进房时检查好友代码
[HarmonyPatch(typeof(AmongUsClient), "OnPlayerJoined")]
internal static class AmethystFriendCodeCheckPatch
{
    private static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData data)
    {
        bool banEnabled = (AmethystConfig.AccessBanEnabled != null && AmethystConfig.AccessBanEnabled.Value)
            || (AmethystConfig.CloudBanEnabled != null && AmethystConfig.CloudBanEnabled.Value);
        if (!banEnabled || data == null) return;
        AmethystFriendCodeCheck.LoadFromConfig();

        string fc = data.FriendCode;
        if (AmethystFriendCodeCheck.IsBlocked(fc))
        {
            string playerName = AmethystColors.Name(data.Character) ?? data.PlayerName;
            AmethystToast.Push(
                AmethystText.T("BanList"),
                AmethystText.T("FriendCodeIsBlocked", fc, playerName),
                5.0f, AmethystNotifyKind.Danger);

            if (__instance != null && __instance.AmHost)
            {
                AmethystAccess.Kick((InnerNetClient)__instance, data.Id, true, true);
            }
        }
    }
}
