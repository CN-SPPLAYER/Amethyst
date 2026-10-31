using System;
using System.Text;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;
namespace Amethyst.Patches;

internal static partial class AmethystRolePanel
{
    private static Vector3 Mul(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

    private static string RoleColored(PlayerControl pc)
    {
        try
        {
            if (pc.Data == null || pc.Data.Role == null) return null;
            RoleTypes role = pc.Data.Role.Role;
            string label = AmethystDeadRoleReveal.GetRoleLabel(role, role.ToString());
            if (string.IsNullOrEmpty(label)) return null;
            return AmethystRoleColors.Colorize(role, label);
        }
        catch { return null; }
    }
    private static void AppendSelfBlock(StringBuilder sb, PlayerControl local)
    {
        try
        {
            RoleBehaviour role = local.Data != null ? local.Data.Role : null;
            if (role == null) return;

            RoleTypes type = role.Role;
            string label = AmethystDeadRoleReveal.GetRoleLabel(type, role.NiceName);
            if (string.IsNullOrEmpty(label)) label = type.ToString();

            sb.Append("<b>").Append(AmethystRoleColors.Colorize(type, label)).Append("</b>");

            string tag = AmethystRoleDescriptions.Short(type);
            if (!string.IsNullOrEmpty(tag)) sb.Append('\n').Append(tag);

            string longText = AmethystRoleDescriptions.Long(type);
            if (!string.IsNullOrEmpty(longText))
                sb.Append("\n<size=80%>").Append(longText).Append("</size>");
        }
        catch { }
    }

    private static void AppendLine(StringBuilder sb, PlayerControl pc, string prefix)
    {
        try
        {
            string name = AmethystColors.Name(pc) ?? (pc.Data != null ? pc.Data.PlayerName : "?");
            string role = RoleColored(pc);
            if (string.IsNullOrEmpty(role)) return;

            sb.Append("<b>");
            if (!string.IsNullOrEmpty(prefix)) sb.Append(prefix).Append(' ');
            sb.Append(name).Append("</b>  ").Append(role);

            if (pc.Data != null && pc.Data.IsDead) sb.Append("  ").Append(AmethystText.T("DeadTag"));
            sb.Append('\n');
        }
        catch { }
    }

    private static string Build()
    {
        StringBuilder sb = new StringBuilder();
        try
        {
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return string.Empty;

            AppendSelfBlock(sb, local);

            bool dead = false;
            try { dead = local.Data.IsDead; } catch { }

            bool showAll = false;
            try
            {
                
                showAll = dead && ShowDeadRoles && AmethystPlayerCacheStore.LocalIsCrewmateGhost;
            }
            catch { }

            bool showTeam = false;
            try { showTeam = !dead && ShowTeamRoles && AmethystRpcGuard.IsImp(local); } catch { }

            if (!showAll && !showTeam) return sb.ToString().TrimEnd();

            var others = new System.Collections.Generic.List<PlayerControl>();
            var all = PlayerControl.AllPlayerControls;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    PlayerControl pc = all[i];
                    if (pc == null || pc == local || pc.Data == null) continue;

                    bool teammate = false;
                    try { teammate = showTeam && AmethystRpcGuard.IsImp(pc); } catch { }
                    if (showAll || teammate) others.Add(pc);
                }
            }

            if (others.Count > 0)
            {
                sb.Append('\n');
                sb.Append("<size=80%>").Append(AmethystText.T("RolePanelOthers")).Append("</size>\n");
                for (int i = 0; i < others.Count; i++)
                    AppendLine(sb, others[i], null);
            }
        }
        catch { }
        return sb.ToString().TrimEnd();
    }



    private static float DropUnits()
    {
        try
        {
            if (!AmethystRpcGuard.IsHS()) return 0f;
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic || cam.orthographicSize <= 0f) return 0f;
            return HideNSeekDropPixels * (2f * cam.orthographicSize) / Mathf.Max(1f, (float)Screen.height);
        }
        catch { return 0f; }
    }
}
