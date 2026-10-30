using Hazel;
using HarmonyLib;
using InnerNet;
using Amethyst.Patches;

namespace Amethyst;

// H&S（Hide & Seek）模式专属防护：报告 / 关门 / 破坏 / 寻猎者进管。
// 开关：Guard.HsSabotageGuard（可单独开启）；开启 RpcGuard 主开关时同样生效（保持旧行为）。
// 通用 RPC 守卫在 H&S 中会让路（见 AmethystRpcGuardShip/Vent 的 IsHS 分支），
// 避免同一事件被两套规则重复判定或归类错误。
internal static class AmethystHsSabotage
{
    internal static bool On()
    {
        try
        {
            if (AmongUsClient.Instance == null) return false;
            if (!AmethystRpcGuard.IsHS()) return false;
            bool hsToggle = AmethystConfig.HsSabotageGuard != null && AmethystConfig.HsSabotageGuard.Value;
            bool master = AmethystConfig.RpcGuard != null && AmethystConfig.RpcGuard.Value;
            return hsToggle || master;
        }
        catch { return false; }
    }
}

[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class AmethystHsSabotagePlayer
{
    private static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!AmethystHsSabotage.On() || __instance == null || __instance == PlayerControl.LocalPlayer || __instance.Data == null || reader == null) return true;

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        try
        {
            if (callId == (byte)RpcCalls.ReportDeadBody)
            {
                cheat = true;
                reason = AmethystText.T("ReportInHS");
            }
            // ⚠ 第 207 轮：**紧急会议按钮那条路之前完全没拦**。
            //   躲猫猫本身**没有任何会议**（没有紧急会议、没有可报告的尸体），
            //   所以 `StartMeeting` 和 `ReportDeadBody` 在躲猫猫里都是非法 RPC。
            //   原来只拦了后者 ⇒ 用"紧急会议"外挂的玩家在躲猫猫里**完全检测不到**
            //   （用户反馈：「躲猫猫中不允许开会，那个检测不见了」）。
            //
            //   参考模组的做法：
            //     · BetterAmongUs `Modules/AntiCheat/RPCHandlers/ReportDeadBodyHandler.cs` 第 27 行
            //       `... || GameState.IsHideNSeek || ...` → 躲猫猫里直接 CancelAsHost 拦掉报告；
            //       另外单独注册了 `StartMeetingHandler`（第 6~9 行）。
            //     · Elysium `network/RpcAnticheat.cs` 第 371~372 行把 `ReportDeadBody` 与
            //       `StartMeeting` 并列进同一组一起判（第 410、494 行同款并列写法）。
            //   ⇒ 两条路都补上，与参考模组的并列写法一致。
            else if (callId == (byte)RpcCalls.StartMeeting)
            {
                cheat = true;
                reason = AmethystText.T("MeetingInHS");
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat)
        {
            AmethystRpcGuard.Flag(__instance, reason);
            return !AmethystRpcGuard.ShouldDropRpc();
        }
        return true;
    }
}

// H&S 中寻猎者（Seeker）进管：进出管 RPC 走 PlayerPhysics.HandleRpc（旧代码错误地挂在
// PlayerControl.HandleRpc 上导致从未生效——本类为修复后的实现）。
[HarmonyPatch(typeof(PlayerPhysics), "HandleRpc")]
internal static class AmethystHsSabotagePhysics
{
    private static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId)
    {
        if (!AmethystHsSabotage.On() || __instance == null) return true;
        if (callId != (byte)RpcCalls.EnterVent && callId != (byte)RpcCalls.ExitVent) return true;

        PlayerControl actor = __instance.myPlayer;
        if (actor == null || actor == PlayerControl.LocalPlayer || actor.Data == null) return true;

        // ⚠ 第 168 轮：死亡后的鬼魂可以自由进出管道（正常游戏机制），不能判作弊。
        //   Hydra 的 CanVent 分支有同样的 IsDead 保护，这里补齐。
        try { if (actor.Data.IsDead) return true; } catch { }

        if (AmethystRpcGuard.IsImp(actor))
        {
            AmethystRpcGuard.Flag(actor,
                AmethystText.T("VentingAsSeekerInHS"));
            return !AmethystRpcGuard.ShouldDropRpc();
        }
        return true;
    }
}

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystHsSabotageShip
{
    private static bool Prefix(ShipStatus __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!AmethystHsSabotage.On() || __instance == null || reader == null) return true;

        int pos = reader.Position;
        bool block = false;
        PlayerControl actor = null;
        string reason = null;
        try
        {
            if (callId == (byte)RpcCalls.UpdateSystem)
            {
                int sys = reader.ReadByte();
                actor = reader.ReadNetObject<PlayerControl>();

                if (sys == (byte)SystemTypes.Ventilation)
                {
                    // ⚠ 第 168 轮修复（用户反馈：躲猫猫里「躲藏者跳管」被判「非法进管/传送」）：
                    //
                    //   这里原来把 Enter / Move / BootImpostors 一律按「非本机 actor」拦截：
                    //       if ((op == Enter || op == Move || op == BootImpostors)
                    //           && actor != null && !actor.AmOwner && actor.Data != null)
                    //           { block = true; reason = VentAbuseInHS; }
                    //
                    //   但主机在玩家进管后，**正是通过这个通道**把管道状态广播同步给所有客户端；
                    //   非主机端收到的每个 Enter/Move 都是主机发来的合法同步 ——
                    //   `!actor.AmOwner` 在非主机端恒为真（actor 是那个进管的玩家，不是本机），
                    //   于是躲猫猫里任何人一进管道都会被判「非法进管/传送」，
                    //   并把 actor 当作弊者上报 / 惩罚。这就是误报根源。
                    //
                    //   参考实现（都只在个人通道判定，Ship 通道不碰 Enter/Move）：
                    //     · Hydra   src/anticheat/rpc/EnterVent.cs / ExitVent.cs
                    //     · Elysium network/RpcAnticheat.cs  BlockVentKickRpc（只处理 BootImpostors）
                    //     · Onyx    src/Patches/NocturneVentTpProtect.cs（只处理 BootFromVent）
                    //
                    //   因此这里不再拦截 Ventilation 通道，交由：
                    //     · AmethystHsSabotagePhysics —— 个人 EnterVent/ExitVent 通道的角色权限判定
                    //     · AmethystVentTpProtect     —— 普通模式下的强制踢管防护（H&S 中已自动关闭）
                    //   仅把 reader 指针跳过该分支的字段，避免影响后续分支的读取位置。
                    reader.ReadUInt16();
                    if (reader.Position < reader.Length) reader.ReadByte();
                }
                else
                {
                    int target = sys;
                    if (sys == (byte)SystemTypes.Sabotage && reader.Position < reader.Length)
                        target = reader.ReadByte();

                    if (AmethystRpcGuard.SabSystems.Contains(target))
                    {
                        block = true;
                        reason = AmethystText.T("SabotageInHS");
                    }
                }
            }
            else if (callId == (byte)RpcCalls.CloseDoorsOfType)
            {
                if (AmethystRpcGuard.IsHS())
                {
                    block = true;
                    reason = AmethystText.T("DoorsInHS_2");
                }
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (block)
        {
            if (actor != null) AmethystRpcGuard.Flag(actor, reason);
            else AmethystRpcGuard.NotifyOnce("hs-nobody", reason);
            // 关键修复：命中后真正丢弃该 UpdateSystem/CloseDoors RPC，使其在（主机）不生效，
            // 而不再只提示不拦截。
            return false;
        }
        return true;
    }
}
