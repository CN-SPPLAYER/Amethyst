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






[HarmonyPatch(typeof(AmongUs.Data.Player.PlayerBanData), nameof(AmongUs.Data.Player.PlayerBanData.BanPoints), MethodType.Setter)]
internal static class AmethystBypassDisconnectPenaltyPatch
{
    private static readonly System.Collections.Generic.List<float> StartGameTimestamps = new System.Collections.Generic.List<float>();
    private static float _lastWarningTime = -1f;

    internal static bool CheckHostRapidStart()
    {
        try
        {
            bool preventOn = AmethystConfig.BypassDisconnectPenalty != null && AmethystConfig.BypassDisconnectPenalty.Value;
            bool detectOn = AmethystConfig.DetectHostTempBan != null && AmethystConfig.DetectHostTempBan.Value;
            if (!preventOn && !detectOn) return false;
            if (AmongUsClient.Instance == null || AmongUsClient.Instance.AmHost) return false; 

            float now = Time.realtimeSinceStartup;
            StartGameTimestamps.Add(now);

            while (StartGameTimestamps.Count > 0 && now - StartGameTimestamps[0] > 0.1f)
            {
                StartGameTimestamps.RemoveAt(0);
            }

            int countInWindow = 0;
            for (int i = 0; i < StartGameTimestamps.Count; i++)
            {
                if (now - StartGameTimestamps[i] <= 0.05f) countInWindow++;
            }

            if (countInWindow >= 3)
            {
                
                if (detectOn && now - _lastWarningTime > 5f)
                {
                    _lastWarningTime = now;
                    string msg = AmethystText.T("TempBanAllDetected");
                    AmethystToast.Push(AmethystText.T("Guard"), msg, 10.0f, AmethystNotifyKind.Danger);
                    AmethystEventLog.Add(msg, AmethystNotifyKind.Danger, AmethystEventCat.Other);
                    AmethystPlugin.Logger?.LogWarning($"[AntiCheat] {msg} (Rapid StartGame: {countInWindow} in 0.05s)");
                }
                return preventOn; 
            }
        }
        catch { }
        return false;
    }

    public static bool Prefix(ref float value)
    {
        if (!AmethystConfig.BypassDisconnectPenalty.Value) return true;
        try
        {
            if (!(bool)(UnityEngine.Object)AmongUsClient.Instance
                || AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
                return true;
        }
        catch { return true; }

        value = 0f;
        return true;
    }
}
