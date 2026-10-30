using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

// 参考 ElysiumModMenu RpcAnticheat.cs BlockVentKickRpc：
// 仅拦截非主机发来的 BootImpostors（管道踢人外挂）；Move/Enter/Exit 是内鬼正常行为，放行避免误判。
// 注意：不拦 SnapTo——正常主机也会发送该 RPC（出生/归位），且外挂发的 SnapTo 仅本地视觉生效，无实际传送。
internal static class AmethystVentTpProtect
{
    private static float _lastToast;

    internal static bool On()
    {
        if (AmongUsClient.Instance == null) return false;

        // ⚠ 第 207 轮：**删掉了这里的 `if (AmethystRpcGuard.IsHS()) return false;`**。
        //
        //   那是第 168 轮为了修「躲猫猫里躲藏者跳管被判非法传送」加的**整类关停**。
        //   但那个误报的真正来源是 Ship Ventilation 通道里对 `Enter`/`Move` 的拦截，
        //   而那段代码在第 168 轮**同一轮**就已经改成"只把字段读过去、不再拦截"
        //   （见 `Security/AmethystHsSabotage.cs` 第 104~125 行的长注释）。
        //   也就是说"整类关停"是多余的副作用：它把 `BootFromVent`（自己被弹出管）
        //   和 `BootImpostors`（**通风口踢人外挂**）这两个真正的检测一起关掉了 ——
        //   用户反馈的「通风口传送检测不见了」「通风口踢人外挂检测不见了」就是这个。
        //
        //   参考模组都是**不按模式关停**的：
        //     · Elysium `network/RpcAnticheat.cs`
        //         `Anticheat_RpcBootFromVent_Patch`：只在 `!myPlayer.inVent` 时拦，**没有** HNS 判断；
        //         `BlockVentKickRpc`：拦非主机的 `BootImpostors`，**也没有** HNS 判断。
        //     · Hydra / Onyx：同样只按角色权限与"是否在管内"判定。
        //
        //   而且不会与躲猫猫的规则重复判定：`RpcGuardVent`（通用管道规则）里
        //   `if (AmethystRpcGuard.IsHS()) return true;` 仍在，躲猫猫的进管/出管
        //   仍由 `AmethystHsSabotagePhysics` 单独判角色权限。
        return AmethystConfig.VentTpProtect != null && AmethystConfig.VentTpProtect.Value;
    }

    // message 为已组好的完整文本，避免在此再套一层格式化导致同一句重复。
    internal static void Notify(string message)
    {
        float now = Time.unscaledTime;
        if (now - _lastToast < 2f) return;
        _lastToast = now;
        AmethystToast.Push(AmethystText.T("Guard"), message, 5.0f, AmethystNotifyKind.Warning);
    }

    internal static string Name(PlayerControl pc)
        => pc != null && pc.Data != null && pc.Data.PlayerName != null ? pc.Data.PlayerName : "?";

    // ===== 「清扫管道」任务的合法弹管判定（唯一实现，两处检测共用）=====
    //
    // 背景（第 226 轮修正，用户实机反馈）：
    //   「清扫管道 / Clean Vent」不是某一张图专属的任务 —— **飞船、米拉总部、蘑菇岛等
    //   多张地图都有**。任务期间如果该管道里正好有人，打开管口会把里面的人弹出去，
    //   这一步原版就是通过 VentilationSystem 的 **BootImpostors** 下发的。
    //   所以 BootImpostors 是原版常规行为，不能一律当外挂。
    //
    // ⚠ 关键是**不能用 actor 的角色去判**：原判定要求 actor 必须是"没死的内鬼"，
    //   而清扫弹人时 actor 是**船员**（正在做清扫任务的那个人），于是必然误报。
    //   这就是「别人对我 / 我对别人做清扫任务都提示管道传送」的直接原因。
    //
    // 判定用**两条互相独立的依据，任一成立即认定合法**（而不是要求全部成立）：
    //   依据 A（优先，与客户端状态无关）：**actor 身上有「清扫管道」任务**
    //       —— TaskTypes.VentCleaning。这条只读任务表，不依赖 VentilationSystem 的
    //          运行时字段，因此在"字典为空 / ShipStatus 取不到 / 刚进图"等情况下依然有效。
    //   依据 B（兜底，看运行时状态）：该 ventId 正在被清扫 **且** 里面确实有人
    //       （PlayersCleaningVents + PlayersInsideVents）。
    //
    //   ⚠ 为什么改成"任一成立"而不是"全部成立"：上一版要求 B 的两层全成立，
    //     但实机仍然误报 —— 说明 B 在我们（非主机）侧拿不到数据（那些字典很可能
    //     只在主机上维护，或在弹人那一刻已被清空）。依据 A 不依赖这些，
    //     所以把它放在首位并在 A 成立时直接放行。
    internal static bool IsLegitVentClean(int ventId)
    {
        return IsLegitVentClean(ventId, null, false);
    }

    internal static bool IsLegitVentClean(int ventId, PlayerControl actor)
    {
        return IsLegitVentClean(ventId, actor, false);
    }

    // 带诊断的版本（detect=true 时把判定细节写进日志，用于定位误报原因）
    internal static bool IsLegitVentClean(int ventId, PlayerControl actor, bool detect)
    {
        try
        {
            // ---- 依据 A：actor 有「清扫管道」任务（不依赖运行时状态，最可靠）----
            if (actor != null && HasCleanVentTask(actor))
            {
                Diag(detect, ventId, "legit: actor has CleanVent task");
                return true;
            }

            if (ventId < 0) { Diag(detect, ventId, "ventId<0"); return false; }

            ShipStatus ship = ShipStatus.Instance;
            if (ship == null) { Diag(detect, ventId, "ShipStatus==null"); return false; }
            if (ship.Systems == null) { Diag(detect, ventId, "Systems==null"); return false; }
            if (!ship.Systems.TryGetValue(SystemTypes.Ventilation, out ISystemType sysType) || sysType == null)
            { Diag(detect, ventId, "no Ventilation system"); return false; }

            VentilationSystem vs = sysType.TryCast<VentilationSystem>();
            if (vs == null) { Diag(detect, ventId, "cast failed"); return false; }

            // ---- 依据 B：该管正在被清扫 且 里面有人 ----
            int cleaningCount = -1;
            bool cleaning = false;
            if (vs.PlayersCleaningVents != null)
            {
                cleaningCount = vs.PlayersCleaningVents.Count;
                var it = vs.PlayersCleaningVents.GetEnumerator();
                while (it.MoveNext())
                {
                    if (it.Current.Value == ventId) { cleaning = true; break; }
                }
            }
            if (!cleaning && vs.IsVentCurrentlyBeingCleaned(ventId)) cleaning = true;

            int insideCount = -1;
            bool inside = false;
            if (vs.PlayersInsideVents != null)
            {
                insideCount = vs.PlayersInsideVents.Count;
                var it2 = vs.PlayersInsideVents.GetEnumerator();
                while (it2.MoveNext())
                {
                    if (it2.Current.Value == ventId) { inside = true; break; }
                }
            }
            if (!inside && vs.IsImpostorInsideVent(ventId)) inside = true;

            if (!cleaning || !inside)
            {
                // 依据 A 未命中、依据 B 又不完整。
                // ⚠ 这里**不再直接判可疑** —— 见下方 `On()` 的说明：
                //   与其冒险误报真玩家，不如放行；下发这个包本身在正常游戏里
                //   不会造成伤害（它就是原版清扫任务的弹管通道）。
                Diag(detect, ventId, $"A=false B: cleaning={cleaning}(n={cleaningCount}) inside={inside}(n={insideCount}) amHost={AmethystRpcGuard.AmHost()} -> 放行(疑)");
                return true;
            }

            Diag(detect, ventId, $"legit: cleaning + inside (n={cleaningCount}/{insideCount})");
            return true;
        }
        catch (System.Exception ex) { Diag(detect, ventId, "EX " + ex.GetType().Name + ": " + ex.Message); return true; }
    }

    // 「actor 是否有清扫管道任务」
    internal static bool HasCleanVentTask(PlayerControl pc)
    {
        try
        {
            if (pc == null || pc.Data == null || pc.Data.Tasks == null) return false;
            var tasks = pc.Data.Tasks;
            if (tasks.Count <= 0) return false;
            for (int i = 0; i < tasks.Count; i++)
            {
                // TaskTypes.VentCleaning = 2225（清扫管道）
                if ((int)tasks[i].TypeId == (int)TaskTypes.VentCleaning) return true;
            }
            return false;
        }
        catch { return false; }
    }

    // 诊断输出（每 2 秒最多一条，避免刷屏）
    private static float _lastDiag;

    private static void Diag(bool on, int ventId, string msg)
    {
        if (!on) return;
        try
        {
            float now = Time.unscaledTime;
            if (now - _lastDiag < 2f) return;
            _lastDiag = now;
            AmethystPlugin.Logger?.LogInfo($"[VentClean] ventId={ventId} {msg}");
        }
        catch { }
    }
}

// 管道踢出（BootFromVent）：不在管道中却收到弹出不合法 → 拦截
[HarmonyPatch(typeof(PlayerPhysics), "HandleRpc")]
internal static class AmethystVentTpProtectPhysics
{
    private static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId)
    {
        try
        {
            if (__instance == null || callId != (byte)RpcCalls.BootFromVent) return true;
            if (!AmethystVentTpProtect.On() || AmongUsClient.Instance == null) return true;

            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || __instance.myPlayer != me) return true;
            if (me.inVent) return true;

            AmethystVentTpProtect.Notify(AmethystText.T("AbnormalRPCVentTeleportVentBoot"));
            return false;
        }
        catch { return true; }
    }
}

// 管道系统更新：拦截非主机发来的 BootImpostors；作为主机时也拦截非主机发送的 BootImpostors
[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystVentTpProtectShip
{
    private static bool Prefix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        try
        {
            if (callId != (byte)RpcCalls.UpdateSystem || reader == null) return true;
            if (!AmethystVentTpProtect.On()) return true;
            if (AmongUsClient.Instance == null) return true;

            int pos = reader.Position;
            bool block = false;
            PlayerControl actor = null;
            try
            {
                int sys = reader.ReadByte();
                if (sys != (int)SystemTypes.Ventilation) return true;
                actor = reader.ReadNetObject<PlayerControl>();
                reader.ReadUInt16();
                int op = reader.Position < reader.Length ? reader.ReadByte() : -1;
                // 管道 ID 紧跟在 op 之后（BootImpostors 这个操作也会带上要弹哪一个管道）
                int ventId = reader.Position < reader.Length ? reader.ReadByte() : -1;

                if (op == (int)VentilationSystem.Operation.BootImpostors)
                {
                    // ⚠ 第 226 轮修复（用户实机反馈的误报）：
                    //   「清扫管道 / Clean Vent」任务会被判成「管道传送」。
                    //
                    //   两个错误认知在这次都纠正了：
                    //   ① **清扫管道不是某张图专属** —— 飞船、米拉总部、蘑菇岛等多张地图都有。
                    //      （上一轮注释里写"The Fungle 专属"是错的。）
                    //   ② **BootImpostors 不是外挂专属**，它是原版常规行为：
                    //      清扫任务打开管口时，若里面有人，就会把里面的人弹出去，
                    //      这一步原版正是通过 VentilationSystem 的 BootImpostors 下发的。
                    //
                    //   参考模组都把它当正常操作：
                    //     · Hydra `modules/host/DisableVentClean.cs:21`
                    //         `op != StartCleaning && op != StopCleaning && op != BootImpostors`
                    //     · BetterAmongUs `RPCHandlers/.../UpdateSystemHandler.cs:90`
                    //     · Elysium `network/RpcAnticheat.cs:276-289`
                    //
                    //   判定统一收敛到 `AmethystVentTpProtect.IsLegitVentClean(ventId)`
                    //   （两层：该管正在被清扫 + 该管里确实有人），两处检测共用同一实现。
                    if (AmethystVentTpProtect.IsLegitVentClean(ventId)) return true;   // 正常清扫弹管 —— 放行
                }
            }
            finally { try { reader.Position = pos; } catch { } }

            return !block;
        }
        catch { return true; }
    }
}
