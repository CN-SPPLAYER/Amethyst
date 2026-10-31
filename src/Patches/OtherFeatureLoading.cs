using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using AmongUs.Data;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace Amethyst.Patches;









[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystBetterLoadingPatch
{
    
    private const float MaxLoadingSeconds = 30f;

    private const float SmoothRate = 75f;

    private const float ClientMapRampSeconds = 8.5f;

    private static bool _running;
    private static bool _allowLoadingBarUpdate;
    private static bool _hidden;
    private static float _loadingStartedAt;
    private static float _shownPercent; 
    private static float _gamePercent = -1f; 
    private static float _mapWaitStart = -1f; 
    private static GameObject _loadingBar; 
    internal static bool Active => _running;
    internal static bool AllowLoadingBarUpdate => _allowLoadingBarUpdate;

    internal static void NoteGamePercent(float percent)
    {
        try
        {
            if (float.IsNaN(percent) || percent < 0f) return;
            if (percent > 100f) percent = 100f;
            if (percent > _gamePercent) _gamePercent = percent;   
        }
        catch { }
    }

    internal static void HideBar()
    {
        _hidden = true;
        try { if (_loadingBar != null) _loadingBar.SetActive(false); } catch { }
    }

    [HarmonyPostfix]
    private static void Postfix(AmongUsClient __instance)
    {
        if (__instance == null) return;
        
        if (_running)
        {
            _running = false;
            _hidden = false;
            _loadingBar = null;
        }
        _gamePercent = -1f;
        _mapWaitStart = -1f;
        try { __instance.StartCoroutine(CoLoading().WrapToIl2Cpp()); }
        catch { }
    }

    [HarmonyPatch(typeof(AmongUsClient), "ExitGame")]
    internal static class AmethystExitGamePatch
    {
        [HarmonyPostfix]
        private static void Postfix() => HideBar();
    }

    private static IEnumerator CoLoading()
    {
        if (_running) yield break;
        _running = true;
        _hidden = false;
        _loadingStartedAt = Time.realtimeSinceStartup;
        _shownPercent = 0f;

        try
        {
            if (LoadingBarManager.Instance == null || LoadingBarManager.Instance.loadingBar == null) yield break;

            int quickCheckFrames = 10;
            for (int i = 0; i < quickCheckFrames; i++)
            {
                if (_hidden || IsGameGone() || IntroCutscene.Instance != null) yield break;
                yield return null;
            }

            _loadingBar = LoadingBarManager.Instance.loadingBar.gameObject;
            _loadingBar.SetActive(true);

            bool completed = false;
            while (true)
            {
                if (_hidden || IntroCutscene.Instance != null) break; 
                if (AmongUsClient.Instance == null || LoadingBarManager.Instance == null || LoadingBarManager.Instance.loadingBar == null)
                    yield break;

                if (IsGameGone()) yield break;

                if (IsLoadingFinished())
                {
                    completed = true;
                    break;
                }

                if (Time.realtimeSinceStartup - _loadingStartedAt >= MaxLoadingSeconds) break;

                BuildStatus(out float target, out string text);
                AdvanceProgress(target);
                SetLoadingPercent(_shownPercent, text);
                yield return null;
            }

            if (completed && !_hidden && IntroCutscene.Instance == null)
            {
                
                string done = AmethystText.T("LoadingComplete");
                float t0 = Time.realtimeSinceStartup;
                while (_shownPercent < 100f && Time.realtimeSinceStartup - t0 < 0.5f)
                {
                    if (_hidden || IntroCutscene.Instance != null) break;
                    AdvanceProgress(100f, 180f); 
                    SetLoadingPercent(_shownPercent, done);
                    yield return null;
                }
                if (!_hidden && IntroCutscene.Instance == null)
                {
                    _shownPercent = 100f;
                    SetLoadingPercent(100f, done);
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }
        finally
        {
            
            try { if (_loadingBar != null) _loadingBar.SetActive(false); } catch { }
            _running = false;
            _hidden = false;
            _loadingBar = null;
            _gamePercent = -1f;
            _mapWaitStart = -1f;
        }
    }

    private static void AdvanceProgress(float target, float rate = SmoothRate)
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f || dt > 0.25f) dt = 0.016f;
        float next = Mathf.MoveTowards(_shownPercent, target, rate * dt);
        if (next < _shownPercent) next = _shownPercent; 
        _shownPercent = next;
    }

    private static void BuildStatus(out float percent, out string text)
    {
        percent = 10f;
        text = AmethystText.T("StartingGame");

        AmongUsClient client = AmongUsClient.Instance;
        if (client == null) return;

        if (client.GameState != InnerNetClient.GameStates.Started) return;

        if (LobbyBehaviour.Instance != null)
        {
            text = AmethystText.T("EnteringLobby");
            percent = 22f;
            return;
        }

        bool clientMapWait = ShipStatus.Instance == null
            && !client.ShipLoadingAsyncHandle.IsValid()
            && !client.AmHost;

        if (clientMapWait)
        {
            if (_mapWaitStart < 0f) _mapWaitStart = Time.realtimeSinceStartup;
        }
        else
        {
            _mapWaitStart = -1f;   
        }

        if (ShipStatus.Instance == null || client.ShipLoadingAsyncHandle.IsValid())
        {
            bool asyncLoading = client.ShipLoadingAsyncHandle.IsValid();
            if (asyncLoading)
            {
                float p = 0f;
                try { p = client.ShipLoadingAsyncHandle.PercentComplete; } catch { }
                if (float.IsNaN(p) || p < 0f) p = 0f;
                if (p > 1f) p = 1f;
                percent = 30f + 40f * p;
                text = AmethystText.T("LoadingShipPercent", Mathf.RoundToInt(p * 100f));
            }
            else if (!client.AmHost)
            {
                
                float elapsed = _mapWaitStart >= 0f ? Time.realtimeSinceStartup - _mapWaitStart : 0f;
                float estimate = 30f + 38f * Mathf.Clamp01(elapsed / ClientMapRampSeconds);
                float fromGame = _gamePercent >= 0f ? 30f + 38f * Mathf.Clamp01(_gamePercent / 100f) : 0f;
                percent = Mathf.Max(estimate, fromGame);
                
                text = AmethystText.T("LoadingShipPercent", Mathf.RoundToInt((percent - 30f) / 40f * 100f));
            }
            else
            {
                text = AmethystText.T("SpawningShip");
                percent = 30f;
            }
            return;
        }

        CountClients(out int readyClients, out int totalClients);
        if (totalClients > 0 && readyClients < totalClients)
        {
            text = AmethystText.T("WaitingForOtherPlayers", readyClients, totalClients);
            percent = 72f + 12f * readyClients / totalClients;
            return;
        }

        CountRoles(out int assignedPlayers, out int totalPlayers);
        if (totalPlayers > 0 && assignedPlayers < totalPlayers)
        {
            if (assignedPlayers == 0)
            {
                
                text = client.AmHost
                    ? AmethystText.T("AssigningRoles")
                    : AmethystText.T("WaitingForOtherPlayers_2");
                percent = 85f;
                return;
            }

            text = AmethystText.T("AssigningRoles_2", assignedPlayers, totalPlayers);
            percent = 85f + 12f * assignedPlayers / totalPlayers;
            return;
        }

        if (IntroCutscene.Instance != null)
        {
            text = AmethystText.T("PlayingIntro");
            percent = 97f;
            return;
        }

        text = AmethystText.T("AlmostReady");
        percent = 97f;
    }

    private static bool IsGameGone()
    {
        try
        {
            AmongUsClient client = AmongUsClient.Instance;
            if (client == null) return true;
            return client.GameState == InnerNetClient.GameStates.Ended
                || client.GameState == InnerNetClient.GameStates.NotJoined;
        }
        catch { return true; }
    }

    private static bool IsLoadingFinished()
    {
        try
        {
            AmongUsClient client = AmongUsClient.Instance;
            if (client == null) return true;
            if (client.GameState != InnerNetClient.GameStates.Started) return false;

            if (IntroCutscene.Instance != null) return false;

            if (ShipStatus.Instance == null || client.ShipLoadingAsyncHandle.IsValid()) return false;

            CountRoles(out int assignedPlayers, out int totalPlayers);
            if (totalPlayers > 0 && assignedPlayers < totalPlayers) return false;

            CountClients(out int readyClients, out int totalClients);
            if (totalClients > 0 && readyClients < totalClients) return false;

            if (totalPlayers <= 0 && totalClients <= 0)
                return Time.realtimeSinceStartup - _loadingStartedAt >= 3f;
            return true;
        }
        catch { return true; }
    }

    private static void CountClients(out int readyClients, out int totalClients)
    {
        readyClients = 0;
        totalClients = 0;

        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            var clients = net != null ? net.allClients : null;
            if (clients == null) return;

            int localId = net.ClientId;
            for (int i = 0; i < clients.Count; i++)
            {
                ClientData client = clients[i];
                if (client == null || client.Character == null) continue;
                totalClients++;

                if (client.IsReady || client.Id == localId) readyClients++;
            }
        }
        catch
        {
            readyClients = 0;
            totalClients = 0;
        }
    }

    private static void CountRoles(out int assignedPlayers, out int totalPlayers)
    {
        assignedPlayers = 0;
        totalPlayers = 0;

        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl pc = all[i];
                if (pc == null || pc.isDummy) continue;
                totalPlayers++;
                if (pc.roleAssigned) assignedPlayers++;
            }
        }
        catch
        {
            assignedPlayers = 0;
            totalPlayers = 0;
        }
    }

    private static void SetLoadingPercent(float percent, string text)
    {
        try
        {
            var bar = LoadingBarManager.Instance != null ? LoadingBarManager.Instance.loadingBar : null;
            if (bar == null) return;
            _allowLoadingBarUpdate = true;
            try
            {
                bar.SetLoadingPercent(percent, StringNames.None);
            }
            finally
            {
                _allowLoadingBarUpdate = false;
            }
            if (bar.loadingText != null)
            {
                bar.loadingText.gameObject.SetActive(true);
                bar.loadingText.enabled = true;
                bar.loadingText.SetText(text);
            }
        }
        catch { }
    }
}
