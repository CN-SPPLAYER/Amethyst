using System;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using InnerNet;

namespace Amethyst.Patches;

// ===== 网络包单遍扫描器（第 180 轮重写）====//
//
// 【为什么要重写】
//   此前有 **四个** 独立的 Harmony Prefix 挂在 `InnerNetClient.HandleGameData` 上：
//     ① AmethystRpcFloodPatch          —— 统计 RPC 洪水
//     ② AmethystModClientGameDataPatch—— 识别模组客户端握手
//     ③ AmethystRpcGuardGameData       —— 对局中换装 RPC 检测
//     ④ AmethystNetProtectGameDataPatch—— 大包 / 非法 tag 检测
//
//   每一个都**独立地** `MessageReader.Get(parentReader)` 复制整包、
//   `while` 遍历每一个子消息、每个子消息再 `ReadMessage()` + `Recycle()`。
//   ⇒ **收到一个包就被完整解析 3~4 遍**，其中 3 遍是纯浪费：
//      它们各自扫的是同一串子消息，只是各取各关心的一个字节。
//
//   这就是用户报告「站着不动帧率最高、开始移动帧率就掉」的直接原因：
//   Among Us 的位置同步（`CustomNetworkTransform` / `RpcCalls.SnapTo`）也是
//   GameData 包 —— 站着不动时一个位置包都没有，一旦移动，每个移动玩家
//   就有 ~20Hz 的包，我们这份"重复解析 ×3"的开销随之 ×10~20。
//
// 【现在】
//   只有一个 Prefix，**一趟**遍历同时完成上面四件事：
//     · 先做零成本的整包长度检查（超 MTU 直接丢）；
//     · 一次 `MessageReader.Get` + 一次 `while` 走完所有子消息；
//     · 在这一趟里顺手做 tag 合法性、RPC 洪水计数、模组握手识别、对局换装检测；
//     · 四项全关时**直接 return，一个字节都不碰**（此前每次收包都要白跑三趟）。
internal static class AmethystGameDataScanner
{
    // 服务端消息上限约 1201 B（官方）/1400 B（Nikocat 模组服）。
    // 一条 GameData 不应超过 MTU，超过即视作大消息攻击/异常。
    internal const int MaxGameDataLength = 1400;

    // ---- 本帧需要做哪几件事（位掩码）----
    private const int T_LARGE = 1;      // 整包长度
    private const int T_BAD_TAG = 2;    // 子消息 tag 合法性
    private const int T_FLOOD = 4;      // RPC 洪水计数
    private const int T_MODCLIENT = 8;  // 模组客户端握手
    private const int T_OUTFIT = 16;    // 对局中换装 RPC

    private const int T_NEEDS_WALK = T_BAD_TAG | T_FLOOD | T_MODCLIENT | T_OUTFIT;

    // 四项开关都是配置项，但**一包之内不可能变**——按「帧」缓存即可，
    // 避免每包都去读 4 次 ConfigEntry（那是跨边界调用）。
    private static int _taskFrame = -1;
    private static int _tasks;

    private static int Tasks(InnerNetClient net)
    {
        int frame = UnityEngine.Time.frameCount;
        if (frame == _taskFrame) return _tasks;
        _taskFrame = frame;

        int t = 0;
        try
        {
            // 防过载总开关：关掉时「大包 / 非法 tag / 加固整数」三项一起失效
            bool overloadOn = AmethystConfig.AntiOverloadEnabled == null || AmethystConfig.AntiOverloadEnabled.Value;

            if (overloadOn && AmethystConfig.BlockLargeGameMessages != null && AmethystConfig.BlockLargeGameMessages.Value)
                t |= T_LARGE;
            if (overloadOn && AmethystConfig.BlockInvalidGameDataTypes != null && AmethystConfig.BlockInvalidGameDataTypes.Value)
                t |= T_BAD_TAG;
        }
        catch { }

        try
        {
            if (AmethystConfig.RpcFloodDetect != null && AmethystConfig.RpcFloodDetect.Value
                && net != null && net.NetworkMode != NetworkModes.FreePlay)
                t |= T_FLOOD;
        }
        catch { }

        try
        {
            if (AmethystConfig.RpcGameDataCheck != null && AmethystConfig.RpcGameDataCheck.Value)
            {
                t |= T_MODCLIENT;
                if (AmethystRpcGuard.On() && AmethystRpcGuard.InMatch())
                    t |= T_OUTFIT;
            }
        }
        catch { }

        _tasks = t;
        return t;
    }

    internal static void Reset()
    {
        _taskFrame = -1;
        _tasks = 0;
    }

    // 进房/开局时清一次缓存的任务位掩码（场景状态变了，开关组合要重新判定）。
    [HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
    internal static class AmethystGameDataScannerResetPatch
    {
        private static void Postfix() => AmethystGameDataScanner.Reset();
    }

    [HarmonyPatch(typeof(InnerNetClient), "HandleGameData")]
    internal static class AmethystGameDataScannerPatch
    {
        private static bool Prefix(InnerNetClient __instance, MessageReader parentReader)
        {
            if (__instance == null || parentReader == null) return true;

            int probe = AmethystPerfProbe.Begin(Section.S_Other);
            try { return Scan(__instance, parentReader); }
            finally { AmethystPerfProbe.End(probe, Section.S_Other); }
        }

        private static bool Scan(InnerNetClient __instance, MessageReader parentReader)
        {
            int tasks = AmethystGameDataScanner.Tasks(__instance);
            if (tasks == 0) return true;    // ← 四项全关：零成本

            // ---- 只开了"整包长度"时不必遍历子消息（最常见的最省情形）----
            if ((tasks & T_NEEDS_WALK) == 0)
            {
                if (parentReader.Length > AmethystGameDataScanner.MaxGameDataLength)
                {
                    Drop(parentReader);
                    return false;
                }
                return true;
            }

            if ((tasks & T_LARGE) != 0 && parentReader.Length > AmethystGameDataScanner.MaxGameDataLength)
            {
                Drop(parentReader);
                return false;
            }

            int badTag = (tasks & T_BAD_TAG) != 0 ? 1 : 0;
            int flood = (tasks & T_FLOOD) != 0 ? 1 : 0;
            int modClient = (tasks & T_MODCLIENT) != 0 ? 1 : 0;
            int outfit = (tasks & T_OUTFIT) != 0 ? 1 : 0;

            int originalPosition = parentReader.Position;
            MessageReader copy = null;
            try
            {
                copy = MessageReader.Get(parentReader);
                while (copy.Position < copy.Length)
                {
                    MessageReader part = copy.ReadMessage();
                    if (part == null) break;
                    try
                    {
                        byte tag = (byte)part.Tag;

                        if (tag != 2)   // GameDataTypes.RpcFlag
                        {
                            if (badTag != 0 && AmethystGameDataScannerTag.IsBadTag(tag))
                            {
                                Drop(parentReader);
                                return false;
                            }
                            continue;
                        }

                        // ---- 以下都是 RPC 子消息 ----
                        uint netId = part.ReadPackedUInt32();
                        byte callId = part.ReadByte();

                        // 模组客户端识别借用了原版 CancelPet 作传输。
                        bool isCancelPet = callId == (byte)RpcCalls.CancelPet;

                        // 是不是我们自己那包握手？读一次标记字符串（位置会还原）。
                        //
                        // ⚠ 注意：这一条**不能**只在 `RpcGameDataCheck`（模组检测）开启时才算 ——
                        //   旧实现里洪水统计有自己独立的一份 CancelPet/标记判断，
                        //   与模组检测的开关无关。若把它绑到 modClient 上，
                        //   用户关掉模组检测、只留洪水检测时，**我们自己的握手包会被算成洪水**。
                        bool isOurMarker = false;
                        if (isCancelPet && (modClient != 0 || flood != 0)
                            && part.Length - part.Position >= 8)
                            isOurMarker = IsModMarker(part);

                        // 只有真正需要的场景才去查对象表 —— 不是每个 RPC 都查。
                        //   · 洪水统计：任意 RPC 都要（按所有者归因）
                        //   · 模组握手：只可能是 CancelPet
                        //   · 换装检测：只可能是外观类 RPC
                        bool isOutfitCall = outfit != 0 && AmethystRpcGuard.IsOutfitCall(callId);
                        bool needObjHere = flood != 0
                            || (modClient != 0 && isCancelPet)
                            || isOutfitCall;

                        InnerNetObject obj = null;
                        if (needObjHere && __instance.allObjects != null)
                            __instance.allObjects.AllObjectsFast.TryGetValue(netId, out obj);

                        // ---- ① 模组客户端握手 ----
                        if (modClient != 0 && isCancelPet && obj != null)
                        {
                            PlayerControl sender = obj is PlayerPhysics physics ? physics.myPlayer : obj.TryCast<PlayerControl>();
                            if (sender != null && AmethystModClientDetection.HandlePacket(sender, part))
                                continue;   // 已消费（返回 true 表示这是我们的握手包）
                        }

                        // ---- ② RPC 洪水计数（排除我们自己的握手）----
                        if (flood != 0 && obj != null && !isOurMarker)
                            AmethystRpcFlood.Observe(__instance, obj.OwnerId);

                        // ---- ③ 对局中换装 RPC ----
                        if (isOutfitCall)
                        {
                            PlayerControl target = obj != null ? obj.TryCast<PlayerControl>() : null;
                            if (target != null && target != PlayerControl.LocalPlayer
                                && !AmethystRpcGuard.InJoinGrace())
                            {
                                AmethystRpcGuard.Flag(target, AmethystText.T("OutfitChangeInMatch"));
                                return true;   // 与旧实现一致：标记一次即结束本次扫描
                            }
                        }
                    }
                    catch { }
                    finally { try { part.Recycle(); } catch { } }
                }
            }
            catch { }
            finally
            {
                try { copy?.Recycle(); } catch { }
                try { parentReader.Position = originalPosition; } catch { }
            }

            return true;
        }

        // 只在确实要读字符串时才调用；读完会把位置还原。
        private static bool IsModMarker(MessageReader part)
        {
            int pos = part.Position;
            try { return part.ReadString() == AmethystModClientDetection.Marker; }
            catch { return false; }
            finally { try { part.Position = pos; } catch { } }
        }

        private static void Drop(MessageReader reader)
        {
            try { reader.Recycle(); } catch { }
        }
    }
}

// 与旧 `AmethystNetProtectGameDataPatch` 逐条对应的合法性判定。
internal static class AmethystGameDataScannerTag
{
    internal static bool IsBadTag(byte tag)
    {
        try
        {
            GameDataTypes type = (GameDataTypes)tag;
            return type == GameDataTypes.Invalid || type == (GameDataTypes)3 || type > GameDataTypes.ReadyFlag;
        }
        catch { return false; }
    }
}
