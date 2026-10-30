using BepInEx.Configuration;
using HarmonyLib;
using AmongUs.Data;

namespace Amethyst.Patches;

// 自由聊天解锁 + 强制成年。
//
// ===== 第 200 轮：**大幅收窄**（根因对比旧版本得出）=====
//
// 旧版本 `AMECE - 副本` 里**根本没有这个文件** —— 它从不碰账号系统，账号一切正常。
// 而把账号补丁从"静默失效"修成"真正生效"之后，出现了"返回主界面就一直卡在加载账号"。
// 原因在于原实现里有三项**会改写游戏在线流程**（不只是只读判断）：
//
//     IsAllowedOnline     Prefix  → canOnline = true          （强迫"允许联网"）
//     CanPlayOnline       Postfix → __result = true           （强迫"可以联网玩"）
//     InnerNetClient.JoinGame Prefix → LoginStatus = LoggedIn （直接写本地账号状态）
//
// 这三项会让游戏在主界面就尝试走在线/账号加载路径；在连不上官方服的环境里，
// 就会一直停在"正在加载账号"。**已全部删除。**
//
// ===== 现在只保留两项，都是"只读判断"层面的覆盖 =====
//   · `IsFreechatAllowed` → true     ：解决"登录成功但只能用快速聊天"。
//   · `isKWSMinor` 持续写回 false    ：即"强制成年"（`[Guest] ForceAdult`）。
//
// ⚠ 关于 `isKWSMinor`：互操作程序集里它是**字段直连的属性**（只有
//   `NativeFieldInfoPtr_isKWSMinor`，没有可钩的 `set_isKWSMinor`），所以只能自己反复写字段，
//   由 `AmethystHud` 每帧调用、内部 0.25 秒节流。
//
// 参考：OnyxMenu-1.2.2 `RemoveMinorLimits`、AUnlocker-1.3.1 `AccountPatches.cs`。
internal static class AmethystAccountUnlock
{
    internal static bool FreeChatEnabled => On(AmethystConfig.UnlockFreeChat);
    internal static bool ForceAdultEnabled => On(AmethystConfig.ForceAdult);

    private static bool On(ConfigEntry<bool> e)
    {
        try { return e == null || e.Value; } catch { return false; }
    }

    private static float _nextTick;

    /// <summary>"强制成年"的兜底断言。由 `AmethystHud` 每帧调用（内部 0.25 秒节流）。</summary>
    internal static void Tick()
    {
        if (!ForceAdultEnabled) return;
        try
        {
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextTick) return;
            _nextTick = now + 0.25f;

            EOSManager eos = EOSManager.Instance;
            if (eos == null) return;
            if (eos.isKWSMinor) eos.isKWSMinor = false;
        }
        catch { }
    }
}

// ===== 自由聊天（只读判断，不改游戏流程）=====
[HarmonyPatch(typeof(EOSManager), "IsFreechatAllowed")]
internal static class AmethystUnlockFreeChatPatch
{
    private static void Postfix(ref bool __result)
    {
        if (!AmethystAccountUnlock.FreeChatEnabled) return;
        if (!__result) __result = true;
    }
}
