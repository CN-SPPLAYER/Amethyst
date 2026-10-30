using HarmonyLib;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

// ===== 会议中显示任务面板（第 176 轮新功能）=====
//
// ===== 参考 AUnlocker 1.3.1 `src\Patches\OtherPatches.cs` 第 165~186 行 =====
//
//   原文（`HudManager_SetHudActive`）：
//       [HarmonyPatch(typeof(HudManager), "SetHudActive",
//                     typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool))]
//       public static void Postfix(HudManager __instance, RoleBehaviour role, bool isActive)
//       {
//           if (!AUnlocker.ShowTaskPanelInMeetings.Value) return;
//           if (!MeetingHud.Instance) return;
//
//           // Modify openPosition so the task panel appears on top of the meeting screen
//           var openPosition = __instance.TaskPanel.openPosition;
//           openPosition.z = -20f;
//           __instance.TaskPanel.openPosition = openPosition;
//
//           __instance.TaskPanel.gameObject.SetActive(true);
//       }
//
// 机制说明：
//   游戏在开会时会调用 `HudManager.SetHudActive(...)` 把 HUD 关掉 —— 任务面板随之被隐藏。
//   AUnlocker 的做法是挂这个方法的 Postfix，在**会议进行中**把任务面板重新激活。
//
//   关键点是那个 `openPosition.z = -20f`：
//   面板本身是用世界坐标/AspectPosition 定位的 3D 对象，会议界面是**另一套 Canvas**。
//   只 `SetActive(true)` 的话面板会被会议 UI **盖住**（看不到）。把 z 推到 -20
//   （相机朝 +z 看，所以更小的 z = 更靠近相机 = 画在更前面）就能让它压在会议界面之上。
//
// 与 AUnlocker 的差异（有意为之）：
//   · AUnlocker 只处理"任务面板"。我们额外确保**职业信息面板**也一起显示
//     （它是我们克隆出来的，同样会被 HUD 关闭波及）。
//   · `openPosition` 的 z 只改一次（用标志位），避免每帧重复写 Transform。
internal static class AmethystMeetingTaskPanel
{
    internal static bool Enabled =>
        AmethystConfig.MeetingTaskPanel != null && AmethystConfig.MeetingTaskPanel.Value;

    // 让面板压过会议 UI 的 z 值（AUnlocker 用 -20）。
    private const float OverlayZ = -20f;

    // 记录我们是否已经改过 z，避免重复写。
    private static bool _zAdjusted;

    internal static void Apply(HudManager hud)
    {
        if (!Enabled || hud == null) return;

        try
        {
            if (MeetingHud.Instance == null) { _zAdjusted = false; return; }

            TaskPanelBehaviour panel = hud.TaskPanel;
            if (panel == null) return;

            if (!_zAdjusted)
            {
                _zAdjusted = true;
                try
                {
                    Vector3 openPos = panel.openPosition;
                    openPos.z = OverlayZ;          // 压到会议界面之上
                    panel.openPosition = openPos;
                }
                catch { }
            }

            if (panel.gameObject != null && !panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);

            // 职业信息面板（我们自己克隆的）同样在会议里显示。
            AmethystRolePanel.ShowDuringMeeting();
        }
        catch { }
    }
}

// 会议开始时立刻应用一次（AUnlocker 挂在 SetHudActive 上，我们两条腿都走）。
[HarmonyPatch(typeof(HudManager), "SetHudActive",
    typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool))]
internal static class AmethystMeetingTaskPanelPatch
{
    private static void Postfix(HudManager __instance)
    {
        AmethystMeetingTaskPanel.Apply(__instance);
    }
}
