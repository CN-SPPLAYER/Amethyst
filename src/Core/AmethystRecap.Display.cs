using System;
using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static partial class AmethystRecap
{

    private static string ReasonKey(GameOverReason r)
    {
        switch (r)
        {
            case GameOverReason.CrewmatesByVote: return "RecapReasonVoteOut";
            case GameOverReason.CrewmatesByTask: return "RecapReasonTasks";
            case GameOverReason.ImpostorsByVote: return "RecapReasonCrewVotedOut";
            case GameOverReason.ImpostorsByKill: return "RecapReasonKills";
            case GameOverReason.ImpostorsBySabotage: return "RecapReasonSabotage";
            case GameOverReason.ImpostorDisconnect: return "RecapReasonImpDisconnect";
            case GameOverReason.CrewmateDisconnect: return "RecapReasonCrewDisconnect";
            case GameOverReason.HideAndSeek_CrewmatesByTimer: return "RecapReasonHsTimer";
            case GameOverReason.HideAndSeek_ImpostorsByKills: return "RecapReasonHsKills";
            default: return null;
        }
    }


    internal static string ResultText()
    {
        if (string.IsNullOrEmpty(ResultFaction)) return string.Empty;
        if (string.IsNullOrEmpty(ResultReason)) return ResultFaction;
        return $"{ResultFaction}（{ResultReason}）";
    }


    internal static List<string> Rows()
    {
        var list = new List<string>();
        try
        {
            _entries.Sort(CompareForDisplay);

            for (int i = 0; i < _entries.Count; i++)
            {
                RecapEntry e = _entries[i];
                if (e == null) continue;

                var sb = new StringBuilder();

                string name = string.IsNullOrEmpty(e.Name) ? ("#" + e.PlayerId) : e.Name;
                sb.Append(AmethystColors.Name(name, e.ColorId));

                sb.Append(" <color=#6E6E6E>-</color> ");

                string role = RoleText(e);
                if (!string.IsNullOrEmpty(role)) sb.Append(role);

                if (!string.IsNullOrEmpty(e.ProgressText)) sb.Append(' ').Append(e.ProgressText);

                string vital = VitalText(e);
                if (!string.IsNullOrEmpty(vital))
                    sb.Append(" <color=#6E6E6E>(").Append(vital).Append(")</color>");

                string judged = JudgedText(e);
                if (!string.IsNullOrEmpty(judged)) sb.Append("  ").Append(judged);

                list.Add(sb.ToString());
            }
        }
        catch { }
        return list;
    }


    internal static string WinnerRole(string name, int colorId, HashSet<byte> used)
    {
        foreach (RecapEntry e in _entries)
        {
            if (used.Contains(e.PlayerId) || e.ColorId != colorId || StripTags(e.Name) != StripTags(name)) continue;
            used.Add(e.PlayerId);
            return !string.IsNullOrEmpty(e.RoleAliveText) ? e.RoleAliveText : e.RoleDeadText;
        }
        return string.Empty;
    }


    private static string RoleText(RecapEntry e)
    {
        string alive = e.RoleAliveText;
        string dead = e.RoleDeadText;

        if (string.IsNullOrEmpty(alive)) return dead;           
        if (!e.Dead || string.IsNullOrEmpty(dead)) return alive; 
        if (alive == dead) return alive;                         

        return $"{alive}<color=#9A9A9A>=&gt;</color>{dead}";
    }


    private static int CompareForDisplay(RecapEntry a, RecapEntry b)
    {
        int c = a.Disconnected.CompareTo(b.Disconnected);
        if (c != 0) return c;
        c = a.Dead.CompareTo(b.Dead);
        if (c != 0) return c;
        c = b.Impostor.CompareTo(a.Impostor);
        if (c != 0) return c;
        return a.PlayerId.CompareTo(b.PlayerId);
    }


    private static string VitalText(RecapEntry e)
    {
        try
        {
            switch (e.Death)
            {
                case RecapDeath.Kill:
                    {
                        string killer = e.KillerId != byte.MaxValue && _byId.TryGetValue(e.KillerId, out RecapEntry k) && k != null
                            ? AmethystColors.Name(k.Name, k.ColorId)
                            : "?";
                        return $"<color=#FF5A5A>{AmethystText.T("RecapKilledBy")}</color> {killer}";
                    }
                case RecapDeath.Exile:
                    return $"<color=#B554FF>{AmethystText.T("RecapExiled")}</color>";
                case RecapDeath.Disconnect:
                    return $"<color=#8A8A8A>{AmethystText.T("RecapDisconnected")}</color>";
                default:
                    
                    if (e.Dead) return $"<color=#FF8A8A>{AmethystText.T("RecapDead")}</color>";
                    return $"<color=#7BD88F>{AmethystText.T("RecapAlive")}</color>";
            }
        }
        catch { return string.Empty; }
    }


    private static string JudgedText(RecapEntry e)
    {
        try
        {
            if (e.JudgedId == byte.MaxValue) return string.Empty;

            string target = _byId.TryGetValue(e.JudgedId, out RecapEntry t) && t != null
                ? AmethystColors.Name(string.IsNullOrEmpty(t.Name) ? ("#" + t.PlayerId) : t.Name, t.ColorId)
                : ("#" + e.JudgedId);

            return $"<color=#E3D12B>{AmethystText.T("RecapJudge")}</color> → {target}";
        }
        catch { return string.Empty; }
    }


    internal static string PlainText()
    {
        var sb = new StringBuilder();
        string result = ResultText();
        if (!string.IsNullOrEmpty(result)) sb.Append(AmethystText.T("RecapResult")).Append(": ").Append(StripTags(result)).Append('\n');
        List<string> rows = Rows();
        for (int i = 0; i < rows.Count; i++)
            sb.Append(StripTags(rows[i])).Append('\n');

        return sb.ToString();
    }


    private static string StripTags(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
        var sb = new StringBuilder(s.Length);
        bool inTag = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }
}
