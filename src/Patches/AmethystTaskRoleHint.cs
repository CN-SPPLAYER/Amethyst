using System;
using AmongUs.GameOptions;
using HarmonyLib;
using Amethyst;

namespace Amethyst.Patches;

internal static class AmethystTaskRoleHint
{
    internal static string Strip(string taskList)
    {
        if (string.IsNullOrEmpty(taskList)) return taskList;
        try
        {
            NetworkedPlayerInfo data = PlayerControl.LocalPlayer != null ? PlayerControl.LocalPlayer.Data : null;
            RoleBehaviour role = data != null ? data.Role : null;
            if (role == null) return taskList;

            TranslationController tc = TranslationController.Instance;
            if (tc == null) return taskList;

            string block = "\n" + role.NiceName + " " + tc.GetString(StringNames.RoleHint) + "\n" + role.BlurbMed;
            int at = taskList.IndexOf(block, StringComparison.Ordinal);
            return at < 0 ? taskList : taskList.Remove(at, block.Length);
        }
        catch { return taskList; }
    }
}

[HarmonyPatch(typeof(TaskPanelBehaviour), "SetTaskText")]
internal static class AmethystTaskRoleHintPatch
{
    private static void Postfix(TaskPanelBehaviour __instance, [HarmonyArgument(0)] string taskList)
    {
        try
        {
            if (__instance == null || __instance.taskText == null || __instance.gameObject == null) return;
            
            if (__instance.gameObject.name == AmethystRolePanel.PanelName) return;

            string text = AmethystTaskRoleHint.Strip(taskList);
            if (!ReferenceEquals(text, taskList) && __instance.taskText.text != text)
                __instance.taskText.text = text;
        }
        catch { }
    }
}
