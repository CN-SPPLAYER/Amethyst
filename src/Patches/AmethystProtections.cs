using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

// ===== 第 241 轮：按 Hydra 2.1.0 补齐的防护项 =====
//
// 参考实现：
//   `参考模组\Hydra-2.1.0\src\modules\protections\ForceDTLs.cs`
//   `...\BlockServerTeleports.cs`
//   `...\BlockUnauthorizedUpdates.cs`
//   `...\AntiOverload.cs`（HardenedReadPackedUInt）
//   `...\AntiCrash.cs`（ReportDeadBody）
//
// 每一项都是独立开关，互不影响；总开关（防过载 / 防踢）在各自补丁里另行判断。
//
// ⚠ 性能：`CustomNetworkTransform.HandleRpc` 是**位置同步热路径**（每名玩家约 20Hz），
//   `MessageReader.ReadPackedUInt32` 更是每个包都走。因此所有开关都按**帧**缓存，
//   一帧只读一次 `ConfigEntry`，而不是每次调用都读。

internal static class AmethystProtToggles
{
    private static int _frame = -1;

    internal static bool Dtls;
    internal static bool BlockTeleports;
    internal static bool BlockUnauthorized;
    internal static bool HardenPackedUInt;
    internal static bool AntiOverload;
    internal static bool AntiKick;
    internal static bool HostOnlyRpcKick;
    internal static bool BypassShapeRate;

    internal static void Refresh()
    {
        int f = Time.frameCount;
        if (f == _frame) return;
        _frame = f;

        Dtls = On(AmethystConfig.ForceDtls);
        BlockTeleports = On(AmethystConfig.BlockServerTeleports);
        BlockUnauthorized = On(AmethystConfig.BlockUnauthorizedUpdates);
        HardenPackedUInt = On(AmethystConfig.HardenPackedUInt);
        AntiOverload = On(AmethystConfig.AntiOverloadEnabled);
        AntiKick = On(AmethystConfig.AntiKickEnabled);
        HostOnlyRpcKick = On(AmethystConfig.HostOnlyRpcKickGuard);
        BypassShapeRate = On(AmethystConfig.BypassShapeshiftRateLimit);
    }

    private static bool On(BepInEx.Configuration.ConfigEntry<bool> e)
        => e == null || e.Value;
}

// ===== 强制 DTLS =====
// `InnerNetClient.SetEndpoint(string addr, ushort port, bool dtls)`：
// 把 dtls 参数强制改成 true，使网络数据走加密通道。
[HarmonyPatch(typeof(InnerNetClient), "SetEndpoint")]
internal static class AmethystForceDtlsPatch
{
    private static void Prefix(ref bool dtls)
    {
        try
        {
            AmethystProtToggles.Refresh();
            if (AmethystProtToggles.Dtls) dtls = true;
        }
        catch { }
    }
}

// ===== 拦截来自服务器的位置更新 =====
// `CustomNetworkTransform.HandleRpc` 收到 `SnapTo` 时，若目标是**本地玩家**则丢弃：
// 这样别人（或主机）无法把你的角色强行传送到别处。
//
// ⚠ 只拦本地玩家：其他玩家的位置更新必须放行，否则整局都看不到别人移动。
[HarmonyPatch(typeof(CustomNetworkTransform), "HandleRpc")]
internal static class AmethystBlockServerTeleportsPatch
{
    private static bool Prefix(CustomNetworkTransform __instance, byte callId)
    {
        try
        {
            if (callId != (byte)RpcCalls.SnapTo) return true;

            AmethystProtToggles.Refresh();
            if (!AmethystProtToggles.BlockTeleports) return true;

            return __instance == null || __instance.myPlayer != PlayerControl.LocalPlayer;
        }
        catch { return true; }
    }
}

// ===== 拦截未授权的系统更新 =====
// `UpdateSystem` / `CloseDoorsOfType` 只应由主机下发。
// 非主机（普通客户端）收到它们一律丢弃，防止作弊者制造本地状态不同步。
//
// ⚠ 第 241 轮：按用户确认，`CloseDoorsOfType`（关门）**属于**本项防护范围，
//   与 Hydra `BlockUnauthorizedUpdates` 的行为保持一致。
//   注意 `ShipStatus.HandleRpc` 没有 sender 参数，因此只能按「我是不是主机」判断。
[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystBlockUnauthorizedUpdatesPatch
{
    private static bool Prefix(byte callId)
    {
        try
        {
            if (callId != (byte)RpcCalls.UpdateSystem && callId != (byte)RpcCalls.CloseDoorsOfType) return true;

            AmethystProtToggles.Refresh();
            if (!AmethystProtToggles.BlockUnauthorized) return true;

            return AmongUsClient.Instance == null || AmongUsClient.Instance.AmHost;
        }
        catch { return true; }
    }
}

// ===== 加固压缩整数（packed uint）反序列化 =====//
// 原版 `MessageReader.ReadPackedUInt32` 在缓冲区被截断时会继续读越界字节。
// 这里用自实现替换：每读一字节前先检查 `BytesRemaining`，读完即停。
//
// ⚠ 这是**全游戏热路径**（每个包都走）。只读一次按帧缓存的开关，
//   命中后直接 `return false` 并给出 `__result`，不做任何分配。
[HarmonyPatch(typeof(MessageReader), "ReadPackedUInt32")]
internal static class AmethystHardenPackedUIntPatch
{
    private static bool Prefix(MessageReader __instance, ref uint __result)
    {
        try
        {
            AmethystProtToggles.Refresh();
            if (!AmethystProtToggles.AntiOverload || !AmethystProtToggles.HardenPackedUInt) return true;
        }
        catch { return true; }

        bool readMore = true;
        int shift = 0;
        uint output = 0;

        // 第 263 轮：手工读包必须整体兜底。
        // 日志里出现过 `InvalidDataException: Read length is longer than message length`
        // 从 `ReadPackedInt32 → ReadPackedUInt32` 一路穿到原生层 —— 就是这里读越界抛出的。
        // 包本身已损坏（通常来自伪造客户端），给出安全值即可，绝不能让异常穿出去。
        try
        {
            while (readMore)
            {
                if (__instance.BytesRemaining < 1) break;

                byte b = __instance.ReadByte();
                if (b >= 0x80)
                {
                    readMore = true;
                    b ^= 0x80;
                }
                else
                {
                    readMore = false;
                }

                // 与原版一致：最多只贡献 5 个字节（shift 28 之后不再左移）
                if (shift < 32) output |= (uint)b << shift;
                shift += 7;
            }
        }
        catch
        {
            __result = 0;
            return false;
        }

        __result = output;
        return false;
    }
}

// ===== 绕过变形 RPC 的频率限制 =====
//
// Hydra 的做法（`BypassShapeshiftRatelimits.cs`）：
//   开局时挑选一名随机玩家，克隆当前游戏选项、把 `ShapeshifterCooldown` 设为 0，
//   再用 `GameOptions.SendGameOptionsToClient` 发给该玩家。
//
// ⚠ 该实现无法直接移植：v19 的 `Assembly-CSharp.dll` 里**不存在**
//   `GameOptions.CreateCloneOptions` 与 `GameOptions.SendGameOptionsToClient`
//   （已用反编译全文检索确认）。因此这里采用等效机制：
//   每帧把**本地玩家自己**的 `ShapeshifterRole.cooldownSecondsRemaining` 清零。
//
//   说明：
//     · 只处理本地玩家自己的角色实例，不影响别人的冷却显示；
//     · 主机自己是变形者时立刻生效（主机的判定就是权威）；
//     · 非主机清零本地冷却后，主机仍会用自己的副本校验 ——
//       这是原版机制决定的，与 Hydra 的“发给单个玩家”效果一致。
[HarmonyPatch(typeof(ShapeshifterRole), "FixedUpdate")]
internal static class AmethystBypassShapeshiftRatePatch
{
    private static void Postfix(ShapeshifterRole __instance)
    {
        try
        {
            if (__instance == null) return;

            AmethystProtToggles.Refresh();
            if (!AmethystProtToggles.BypassShapeRate) return;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return;
            if (local.Data.Role != __instance) return;   // 只处理本地玩家自己的角色

            __instance.cooldownSecondsRemaining = 0f;
        }
        catch { }
    }
}

// ===== 防护 ReportDeadBody 房间崩溃漏洞 =====
//
// 漏洞原理（Hydra `AntiCrash.cs` 注释）：
//   加载界面阶段玩家尚未分配任务，`ReportDeadBody` 里的"任务是否全部完成"检查会返回 true
//   ⇒ 主机调用 EndGame ⇒ Among Us 匹配服务器把**整个房间删除**，而不是把人送回大厅。
//
// 判据：`CoStartGame`（开始加载）→ 标记 loading；`GameManager.StartGame`（角色分配完成、
//   可以正常玩了）→ 清除标记。加载窗口内到达的 ReportDeadBody 一律丢弃。
internal static class AmethystCrashGuardState
{
    private static bool _loading;

    internal static bool Enabled
        => AmethystConfig.ReportDeadBodyCrashGuard == null || AmethystConfig.ReportDeadBodyCrashGuard.Value;

    internal static void MarkLoading() => _loading = true;
    internal static void MarkLoaded() => _loading = false;
    internal static void Reset() => _loading = false;

    internal static bool ShouldBlock => Enabled && _loading;
}

[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystCrashGuardStartPatch
{
    private static void Prefix() => AmethystCrashGuardState.MarkLoading();
}

[HarmonyPatch(typeof(GameManager), "StartGame")]
internal static class AmethystCrashGuardLoadedPatch
{
    private static void Prefix() => AmethystCrashGuardState.MarkLoaded();
}

[HarmonyPatch(typeof(PlayerControl), "ReportDeadBody")]
internal static class AmethystCrashGuardReportPatch
{
    private static bool Prefix(PlayerControl __instance)
    {
        if (!AmethystCrashGuardState.ShouldBlock) return true;

        try
        {
            if (__instance != null && __instance != PlayerControl.LocalPlayer)
            {
                string name = AmethystColors.Name(__instance) ?? "?";
                AmethystSecurityNotify.Fire("TriggeredProtection", AmethystNotifyKind.Warning,
                    name, AmethystText.T("ReportDeadBodyCrashGuard"));
            }
        }
        catch { }

        AmethystPlugin.Logger?.LogWarning("[CrashGuard] blocked ReportDeadBody during match loading");
        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystCrashGuardResetPatch
{
    private static void Postfix() => AmethystCrashGuardState.Reset();
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameEnd")]
internal static class AmethystCrashGuardEndPatch
{
    private static void Postfix() => AmethystCrashGuardState.Reset();
}


