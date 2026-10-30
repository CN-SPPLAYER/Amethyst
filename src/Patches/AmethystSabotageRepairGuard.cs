using System;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

// ===== 破坏修复位置校验（防「隔空修复」外挂）=====
//
// 原版修复破坏必须站在控制台旁：客户端只有靠近控制台才能打开面板并发出
// `UpdateSystem(系统, 修复值)`。但这条 RPC **不携带任何位置信息**，外挂可以
// 从地图另一头直接发修复值，瞬间把破坏修好。主机侧只能拿「包里 actor 的当前坐标」
// 去和控制台坐标比距离。
//
// 参考实现：`参考模组\BanMod-BanMod_3.9.8\Modules\AntiCheat.cs`
//   · 840-903 行：修复动作的 (system, amount) 判据
//   · 994-1125 行：每张地图的控制台坐标
//   该版本与本模组的目标版本枚举值逐个对上（SystemTypes: Reactor=3 / Electrical=7 /
//   LifeSupp=8 / Comms=14 / Laboratory=21 / MushroomMixup=57 / HeliSabotage=58；
//   MapNames: Skeld=0 … Fungle=5），因此坐标可直接沿用。
//
// ⚠ 与 BanMod 的关键差异（**故意**）：
//   BanMod 在「这张图没有坐标表」时返回 false，等于判定作弊 —— 于是 Polus/Airship/
//   Fungle 的氧气修复、Dleks 的电力修复会被**全部误判**。本实现把「没有坐标表」
//   视为「无法判定 → 放行」，宁可漏判也绝不误伤正常玩家。
//
// amount 判据（见 BanMod 同位置 + 其 FixSabotage 实际发送的值）：
//   Electrical  : 0~4      = 拨动第 N 个灯光开关（修复灯光的唯一客户端动作）
//   LifeSupp    : 16 / 17  = 氧气修复（低 2 位是控制台编号）
//   Reactor     : 16 / 17  = 反应堆修复
//   Laboratory  : 16 / 17  = Polus 实验室（反应堆类）
//   HeliSabotage: 16 / 17  = Airship 直升机（反应堆类）
//   Comms       : 16 / 17  = 通讯修复
//   开始破坏一律带 0x80 位（>=128），不会命中上面的判据，所以本补丁不碰开始破坏。
internal static class AmethystSabotageRepairGuard
{
    private static readonly Vector2[] None = Array.Empty<Vector2>();

    // Electrical：灯光开关面板
    private static readonly Vector2[] ElecSkeld = { new Vector2(-10.05f, -10.24f) };
    private static readonly Vector2[] ElecMira = { new Vector2(14.74f, 21.08f) };
    private static readonly Vector2[] ElecPolus = { new Vector2(9.60f, -11.44f) };
    private static readonly Vector2[] ElecAirship =
    {
        new Vector2(-12.82f, -11.27f), new Vector2(30.66f, 2.08f), new Vector2(13.97f, 6.35f)
    };

    // LifeSupp：氧气控制台
    private static readonly Vector2[] O2Skeld = { new Vector2(6.52f, -6.61f), new Vector2(6.81f, -3.07f) };
    private static readonly Vector2[] O2Mira = { new Vector2(17.57f, 24.22f), new Vector2(4.03f, -0.63f) };

    // Reactor / Laboratory / HeliSabotage：三系统共用一张「反应堆类」表
    private static readonly Vector2[] ReactorSkeld = { new Vector2(-21.28f, -1.69f), new Vector2(-21.28f, -8.62f) };
    private static readonly Vector2[] ReactorMira = { new Vector2(4.67f, 14.47f), new Vector2(0.24f, 14.49f) };
    private static readonly Vector2[] ReactorPolus = { new Vector2(24.40f, -3.05f), new Vector2(4.43f, -3.91f) };
    private static readonly Vector2[] ReactorAirship = { new Vector2(3.89f, 9.76f), new Vector2(11.49f, 6.35f) };
    private static readonly Vector2[] ReactorFungle = { new Vector2(20.95f, -6.01f), new Vector2(23.98f, -7.67f) };

    // Comms：通讯控制台
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

    // 该 (system, amount) 是不是「修复」动作（开始破坏带 0x80 位，不会命中）
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

    // 控制台坐标表：map = GameOptions.MapId（0 Skeld / 1 MiraHQ / 2 Polus / 3 Dleks / 4 Airship / 5 Fungle）
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
                    default: return None;   // Dleks(3) / Fungle(5)：暂无坐标表 → 放行
                }

            case SystemTypes.LifeSupp:
                switch (map)
                {
                    case 0: return O2Skeld;
                    case 1: return O2Mira;
                    default: return None;   // Polus / Dleks / Airship / Fungle：暂无坐标表 → 放行
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
                    default: return None;   // Dleks(3) → 放行
                }

            case SystemTypes.Comms:
                switch (map)
                {
                    case 0: return CommsSkeld;
                    case 1: return CommsMira;
                    case 2: return CommsPolus;
                    case 4: return CommsAirship;
                    case 5: return CommsFungle;
                    default: return None;   // Dleks(3) → 放行
                }

            default:
                return None;
        }
    }

    // true = 合法（或无法判定），false = 距离异常
    internal static bool NearConsole(int map, int sys, Vector2 pos, out float nearest)
    {
        nearest = float.MaxValue;
        Vector2[] list = Panels(map, sys);
        if (list == null || list.Length == 0) return true;   // 没有坐标表 → 无法判定 → 放行

        for (int i = 0; i < list.Length; i++)
        {
            float d = Vector2.Distance(pos, list[i]);
            if (d < nearest) nearest = d;
        }
        return nearest <= Limit;
    }
}

// 只读包、不改包：读完把 reader.Position 还原，交回原版继续处理。
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
            // ⚠ 只在房主端判定：客户端收到的 UpdateSystem 可能是房主中继的，
            //   包里的 actor 不一定是真正的修复者，按它算距离会误判。
            if (!AmethystRpcGuard.AmHost()) return true;

            int pos = reader.Position;
            bool tooFar = false;
            PlayerControl actor = null;

            try
            {
                if (reader.BytesRemaining < 2) return true;

                int sys = reader.ReadByte();
                actor = reader.ReadNetObject<PlayerControl>();

                // 拿不到发送者：无法判定，放行
                if (actor == null) return true;
                // 本机自己的合法操作直接放行（IL2CPP 包装对象的 == 不可靠，用 PlayerId 比）
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
                // 位置类判定受延迟影响（主机看到的坐标可能落后于玩家真实位置），
                // 因此只记录 + 提示，不进入踢/封处罚链。
                AmethystRpcGuard.Flag(actor, AmethystText.T("SabotageRepairTooFar"), false);
                // 与 AmethystRpcGuardShip 一致：只有主机且开了 DropRPC 才真正丢弃，
                // 非主机丢弃会造成「本地破坏没修好、其他人已修好」的不同步。
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
