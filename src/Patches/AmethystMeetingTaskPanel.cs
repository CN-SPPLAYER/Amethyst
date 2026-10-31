using HarmonyLib;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

internal static class AmethystMeetingTaskPanel
{
    internal static bool Enabled =>
        AmethystConfig.MeetingTaskPanel != null && AmethystConfig.MeetingTaskPanel.Value;

    private const float OverlayZ = -20f;

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
                    openPos.z = OverlayZ;          
                    panel.openPosition = openPos;
                }
                catch { }
            }

            if (panel.gameObject != null && !panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);

            AmethystRolePanel.ShowDuringMeeting();
        }
        catch { }
    }
}

[HarmonyPatch(typeof(HudManager), "SetHudActive",
    typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool))]
internal static class AmethystMeetingTaskPanelPatch
{
    private static void Postfix(HudManager __instance)
    {
        AmethystMeetingTaskPanel.Apply(__instance);
    }
}
