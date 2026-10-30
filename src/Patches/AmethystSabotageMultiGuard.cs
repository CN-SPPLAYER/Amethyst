using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

// ===== 多重破坏 / 快速切换破坏防护（第 244 轮修正）=====
//
// 【两次失败的真实原因】
//   ① 旧实现「先写时间戳、再判定」：被拦下的那次也写了时间戳，
//      于是通讯的第 1 次被拦后状态被污染，第 2 次刚好在冷却过期时放行。
//   ② 日志里出现 “owner -99”：reader.ReadNetObject<PlayerControl>() 拿不到发送者，
//      所有玩家都挤在 -99 这一个键上，按玩家归属的限速表实际失效。
//
// 【现在的做法（不依赖发送者）】
//   1. 硬判据：场上已有重大破坏时，任何新的「开始破坏」一律丢弃。
//      这是原版自身规则，与外挂发送间隔无关，连点无法绕过。
//   2. 兜底判据：全局「开始破坏」冷却 2.5s（原版破坏冷却约 30s，不会误伤）。
//      用于同一批消息里连续到达、第 1 个还未来得及应用的情况。
//   3. 修复 / 开关灯不走长冷却，只做 0.25s 的同系统防刷。
//   4. 所有校验通过后才提交状态（绝不污染状态机）。
//   5. 全部读取先查 BytesRemaining；解析失败一律放行交回原版，避免误吞合法包。
//
// ⚠ 只在主机侧生效：客户端收到的是主机广播的合法破坏，客户端没有丢弃权。
internal static class AmethystSabotageMultiGuard
{
    // 全局最近一次「开始破坏」被接受的时间
    private static float _lastStartAt = -999f;

    // (ownerId 或 -1) << 16 | systemType -> 最近一次修复/开关被接受的时间
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

    // 先校验，全部通过后才提交。
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

            // 只在主机侧拦截
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

            int pos = reader.Position;
            try
            {
                if (reader.BytesRemaining < 2) return true;

                SystemTypes system = (SystemTypes)reader.ReadByte();
                if (!AmethystSabotageMultiGuard.IsGuardedSystem(system)) return true;

                // 发送者仅用于日志与按玩家的修复防刷；拿不到也不影响核心判定。
                PlayerControl sender = null;
                try { sender = reader.ReadNetObject<PlayerControl>(); }
                catch { sender = null; }

                // 主机自己的合法操作直接放行
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
                    // ① 场上已有重大破坏：新的开始破坏非法
                    if (AmethystSabotageMultiGuard.AnyMajorSabotageActive())
                    {
                        AmethystPlugin.Logger?.LogWarning(
                            "[SabotageGuard] blocked start " + system + " (another sabotage active)");
                        return false;
                    }

                    // ② 全局开始破坏冷却（同一批包连发时兜底）
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