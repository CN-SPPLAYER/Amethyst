using System;
using AmongUs.GameOptions;
using HarmonyLib;
using Amethyst;

namespace Amethyst.Patches;

// 原版会把「职业名 + 提示 + 职业说明」追加在任务列表末尾。不删掉的话，它会和我们自己的
// 「职业信息」面板显示同一段内容（EHR `HudPatch.cs` 第 1649 行同样把它删掉）。
//
// 任务文本本身**不再做任何着色**，原版给什么颜色就用什么颜色。
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
            // 只看**原版任务面板**；我们自己的克隆「职业信息」面板不碰。
            if (__instance.gameObject.name == AmethystRolePanel.PanelName) return;

            string text = AmethystTaskRoleHint.Strip(taskList);
            if (!ReferenceEquals(text, taskList) && __instance.taskText.text != text)
                __instance.taskText.text = text;
        }
        catch { }
    }
}
