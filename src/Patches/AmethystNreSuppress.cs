using System;
using HarmonyLib;

namespace Amethyst.Patches;

// 这些原生方法在部分环境下（x86 IL2CPP / 特定游戏状态）自身会抛 NullReferenceException，
// 与本模组逻辑无关，但会经 Unity 日志转发到 BepInEx 控制台疯狂刷屏。
// 参考 Onyx 的做法：只挂一个 Harmony Finalizer，捕获并吞掉异常，阻止刷屏且不影响其它逻辑。
[HarmonyPatch(typeof(HudManager), "Update")]
internal static class AmethystNreSuppressHudUpdate
{
    private static Exception Finalizer(Exception __exception) => null;
}

[HarmonyPatch(typeof(HudManager), "Start")]
internal static class AmethystNreSuppressHudStart
{
    private static Exception Finalizer(Exception __exception) => null;
}

// ⚠ 会议身份标记（RoleTag）闪退修复（第 201 轮）：
//   在会议里点「EditTag」打开身份选择器时，我们会临时隐藏 `MeetingHud.playerStates`
//   这排会议铭牌。这个瞬间（以及会议收尾）原版 `MeetingHud.Update` 会抛
//   NullReferenceException —— 用户日志里的形态是：
//       [Error:Il2CppInterop] During invoking native->managed trampoline
//       System.NullReferenceException ...
//         at MeetingHud.Update () ...
//         at DMD<MeetingHud::Update>(MeetingHud this)
//   该异常每触发一次就会打断当帧会议逻辑并刷屏，积累后表现为会议界面卡死 / 游戏闪退。
//
//   参考 BetterAmongUs `MeetingHudPatch.MeetingHud_Update_Prefix`（注释即 "Fix null exception"）
//   的处理思路，以及本文件对 `HudManager.Update` 的既有做法：挂一个 Finalizer 捕获并吞掉。
//   Finalizer 只在真有异常时生效，正常帧的会议逻辑完全不受影响（不会像 Prefix 那样整帧跳过）。
[HarmonyPatch(typeof(MeetingHud), "Update")]
internal static class AmethystNreSuppressMeetingUpdate
{
    private static Exception Finalizer(Exception __exception) => null;
}
