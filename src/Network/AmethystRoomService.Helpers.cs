using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using UnityEngine;
using UnityEngine.Networking;
using Amethyst.Patches;

namespace Amethyst;

public sealed partial class AmethystRoomService
{

    internal static string Normalize(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        var sb = new StringBuilder(8);
        foreach (char raw in code.Trim().ToUpperInvariant())
        {
            char ch = raw;
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
        }
        return sb.ToString();
    }


    internal static bool IsValidCode(string code) => code != null && code.Length >= 4 && code.Length <= 8;

    private IEnumerator CoJoin(string code)
    {
        AmongUsClient ac = AmongUsClient.Instance;
        InnerNetClient net = ac as InnerNetClient;
        if (ac == null || net == null)
        {
            FailJoin("RoomListJoinFailedDetail");
            yield break;
        }

        bool inGame = false;
        try { inGame = net.GameState != InnerNetClient.GameStates.NotJoined || net.GameId != 0; } catch { }

        if (inGame)
        {
            try { ac.ExitGame(DisconnectReasons.ExitGame); }
            catch { }

            float wait = 0f;
            while (wait < 5f)
            {
                wait += Time.unscaledDeltaTime;
                bool left = false;
                try { left = net.GameState == InnerNetClient.GameStates.NotJoined; }
                catch { left = true; }
                if (left) break;
                yield return null;
            }
            yield return null;

            ac = AmongUsClient.Instance;
            net = ac as InnerNetClient;
            if (ac == null || net == null)
            {
                FailJoin("RoomListJoinFailedDetail");
                yield break;
            }
        }

        int id = 0;
        try { id = GameCode.GameNameToInt(code); } catch { }
        if (id == 0)
        {
            FailJoin("RoomListInvalidCode");
            yield break;
        }

        bool started = false;

        try
        {
            PSManager ps = PSManager.Instance;
            if (ps != null)
            {
                ps.JoinGameFromCode(code);
                started = true;
            }
        }
        catch (Exception ex) { LogWarn("PSManager join", ex); }

        if (!started)
        {
            try
            {
                var routine = ac.CoFindGameInfoFromCodeAndJoin(id);
                if (routine != null)
                {
                    ac.StartCoroutine(routine);
                    started = true;
                }
            }
            catch (Exception ex) { LogWarn("join coroutine", ex); }
        }

        if (!started)
        {
            FailJoin("RoomListJoinFailedDetail");
            yield break;
        }

        float hold = 0f;
        bool entered = false;
        while (hold < 8f)
        {
            hold += Time.unscaledDeltaTime;
            try { entered = net.GameState != InnerNetClient.GameStates.NotJoined; } catch { }
            if (entered) break;
            yield return null;
        }
        if (!entered)
            AmethystPlugin.Logger?.LogWarning($"[RoomList] join did not start within 8s: {code}");

        Joining = false;
        JoiningCode = string.Empty;
    }


    private static string Secret()
    {
        try
        {
            string s = AmethystConfig.RoomListSecret != null ? AmethystConfig.RoomListSecret.Value : null;
            if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        }
        catch { }

        return AmethystRoomKey.Default;
    }


    private static string ReportPath()
    {
        try { return new Uri(AmethystRoomApi.ReportUrl).AbsolutePath; }
        catch { return "/home/api/room"; }
    }


    private static string Trim(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}
