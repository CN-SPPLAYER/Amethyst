using Hazel;
using HarmonyLib;
using InnerNet;

namespace Amethyst.Patches;

// 一组“网络层/防崩溃”防护，移植自 HydraMenu（modules/protections + network/DataHandler）。
// 均为独立开关、独立补丁，互不影响。

// ===== 已按需求移除（本轮）=====
// 本文件原有三项补丁，其中两项已整体删除（补丁类 + 配置项 + 菜单开关一并移除）：
//
//   ① AmethystBlockServerTeleportsPatch
//      `CustomNetworkTransform.HandleRpc` 前缀，丢弃发给本地玩家的 SnapTo RPC，
//      用于「阻止来自服务器的位置更新 / 防止服务器强制传送本地玩家」。
//      配置键 `Guard.BlockServerTeleports`。
//
//   ② AmethystHardenPackedUIntPatch
//      `MessageReader.ReadPackedUInt32` 前缀，用自实现的循环替换原版实现，
//      即「加固压缩整数解析」。
//      配置键 `Guard.HardenPackedUInt`（该实现直接顶替了全游戏的热路径读取）。
//
// 只留下下面这一项。

// ===== 抵御 VotingComplete 过载 =====
[HarmonyPatch(typeof(MeetingHud), "HandleRpc")]
internal static class AmethystVotingCompleteOverloadPatch
{
    private static bool Prefix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        // 防过载总开关
        if (AmethystConfig.AntiOverloadEnabled == null || !AmethystConfig.AntiOverloadEnabled.Value) return true;
        if (!AmethystConfig.BlockVotingCompleteOverload.Value) return true;
        if (callId != (byte)RpcCalls.VotingComplete || reader == null) return true;

        int pos = reader.Position;
        try
        {
            // 游戏会用该长度分配投票者数组；若极大则尝试分配超大内存
            int arrayLength = reader.ReadPackedInt32();
            if (arrayLength > 1024 || arrayLength > reader.BytesRemaining)
                return false; // 丢弃该 VotingComplete RPC
        }
        catch { return true; }
        finally { try { reader.Position = pos; } catch { } }

        return true;
    }
}

// 「阻止大型游戏消息」与「阻止无效 GameData 类型」已并入
// `Patches\AmethystGameDataScanner.cs` 的**单遍**扫描器 ——
// 旧实现在这里又独立复制并重解析了每一个收包，与另外三个 HandleGameData
// 补丁叠加后，一个包要被完整扫描 4 次。
// 需要遍历子消息时，现在统一由扫描器一趟走完。
