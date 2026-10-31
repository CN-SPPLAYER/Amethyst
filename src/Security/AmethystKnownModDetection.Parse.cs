using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static partial class AmethystKnownModDetection
{

    private static string PayloadName(PlayerControl sender, byte callId, MessageReader reader)
    {
        if (reader == null) return null;
        MessageReader copy = null;
        try
        {
            if (callId != 121 && callId != 212) return null;
            copy = MessageReader.Get(reader);
            if (callId == 121)
                return string.Equals(copy.ReadString(), "CHOCOO_PING", StringComparison.Ordinal) ? "ChocooMenu" : null;

            string info = copy.ReadString();
            return copy.ReadBoolean()
                ? (string.IsNullOrWhiteSpace(info) || info.Length > 32 ? "BanMod" : info)
                : null;
        }
        catch { return null; }
        finally { try { copy?.Recycle(); } catch { } }
    }


    private static string ClaimedName(PlayerControl sender, byte callId, MessageReader reader)
    {
        if (reader == null || callId < VanillaTop) return null;
        if (Claimed.ContainsKey(sender.PlayerId)) return null;   

        MessageReader copy = null;
        try
        {
            copy = MessageReader.Get(reader);

            string sig;
            byte claimed;
            string ver;
            try
            {
                sig = copy.ReadString();
                claimed = copy.ReadByte();
                ver = copy.ReadString();
            }
            catch { return null; }

            if (claimed != sender.PlayerId) return null;
            if (string.IsNullOrEmpty(sig) || sig.Length > 24) return null;
            if (ver == null || ver.Length > 16) return null;

            string name;
            if (sig.IndexOf("MMC", StringComparison.OrdinalIgnoreCase) >= 0)
                name = "ModMenuCrew " + ver;
            else if (LooksVersion(ver))
                name = sig + " " + ver;
            else
                return null;

            Claimed[sender.PlayerId] = name;
            return name;
        }
        catch { return null; }
        finally { try { copy?.Recycle(); } catch { } }
    }


    private static bool LooksVersion(string s)
    {
        if (s == null || s.Length < 3 || s.Length > 16) return false;

        bool digit = false, dot = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '0' && c <= '9') digit = true;
            else if (c == '.') dot = true;
            else if (!char.IsLetter(c) && c != '-' && c != '_') return false;
        }
        return digit && dot;
    }


    private static string TableName(byte callId)
    {
        if (KillNetworkRpcs.Contains(callId)) return "KillNetwork";
        if (SickoMenuAltRpcs.Contains(callId)) return "SickoMenu";
        if (BetterAmongUsRpcs.Contains(callId) || BetterAmongUsAltRpcs.Contains(callId)) return "BetterAmongUs";
        if (HostGuardRpcs.Contains(callId)) return "HostGuard";
        if (ChocooMenuRpcs.Contains(callId)) return "ChocooMenu";
        if (SlopMenuCrewRpcs.Contains(callId)) return "SlopMenuCrew";
        if (UnknownModRpcs.Contains(callId)) return "UnknownMod";
        if (callId == 169) return "UnknownMod";
        if (GaffMenuRpcs.Contains(callId)) return "Gaff Menu";
        if (PolarClientRpcs.Contains(callId)) return "Polar Client";
        if (BanModRpcs.Contains(callId)) return "BanMod";
        return null;
    }


    private static string UnknownName(byte pid, byte callId)
    {
        if (callId < VanillaTop) return null;

        int key = (pid << 8) | callId;
        UnknownHits.TryGetValue(key, out int hits);
        hits++;
        UnknownHits[key] = hits;

        return hits >= 2 ? AmethystText.T("UnknownMod") + " (" + callId + ")" : null;
    }


    private static string SafeName(PlayerControl p)
    {
        try { return AmethystColors.Name(p) ?? (p.Data != null ? p.Data.PlayerName : "#" + p.PlayerId); }
        catch { return "#" + (p != null ? p.PlayerId.ToString() : "?"); }
    }
}
