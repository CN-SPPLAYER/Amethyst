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



[HarmonyPatch]
internal static class AmethystForceShowStartPatches
{
    private static PassiveButton _cancelButton;
    private static GameStartManager _owner;

    private static void EnsureCancelButton(GameStartManager gs)
    {
        if (_cancelButton != null && _owner == gs) return;
        if (gs == null || gs.StartButton == null) return;
        try
        {
            _cancelButton = UnityEngine.Object.Instantiate(gs.StartButton, gs.transform);
            _cancelButton.name = "AmethystForceShowStartCancel";
            _cancelButton.gameObject.SetActive(false);

            TextMeshPro label = _cancelButton.GetComponentInChildren<TextMeshPro>();
            if (label != null)
            {
                DestroyTranslators(label.gameObject);
                label.text = string.Empty;
            }

            SpriteRenderer inactive = _cancelButton.inactiveSprites.GetComponent<SpriteRenderer>();
            SpriteRenderer active = _cancelButton.activeSprites.GetComponent<SpriteRenderer>();
            if (inactive != null) inactive.color = new Color(0.8f, 0f, 0f, 1f);
            if (active != null) active.color = Color.red;
            Transform shine = _cancelButton.inactiveSprites.transform.Find("Shine");
            if (shine != null) shine.gameObject.SetActive(false);

            _cancelButton.activeTextColor = _cancelButton.inactiveTextColor = Color.white;
            _cancelButton.OnClick = new Button.ButtonClickedEvent();
            _cancelButton.OnClick.AddListener((Action)gs.ResetStartState);
            _owner = gs;
        }
        catch { }
    }

    private static void DestroyTranslators(GameObject go)
    {
        if (go == null) return;
        foreach (Transform child in go.transform)
            DestroyTranslators(child.gameObject);
        var comps = go.GetComponents<MonoBehaviour>();
        foreach (var c in comps)
        {
            if (c != null && c.GetType().Name.Contains("Translator"))
                UnityEngine.Object.Destroy(c);
        }
    }

    [HarmonyPatch(typeof(GameStartManager), "Start")]
    [HarmonyPostfix]
    private static void StartPostfix(GameStartManager __instance)
    {
        
        EnsureCancelButton(__instance);
        DumpLobbyStructure(__instance);
        
        try { ApplyLobbyPaneScale(__instance); } catch { }
    }

    private static bool _diagDone;
    private static void DumpLobbyStructure(GameStartManager gs)
    {
        if (_diagDone || gs == null) return;
        _diagDone = true;
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[LobbyDiag] GameStartManager structure:");

            LobbyInfoPane lip = gs.LobbyInfoPane;
            sb.AppendLine($"  gs.LobbyInfoPane: {(lip == null ? "NULL" : lip.gameObject.name + " | parent=" + (lip.transform.parent != null ? lip.transform.parent.name : "null") + " | children=" + lip.transform.childCount)}");

            try
            {
                LobbyInfoPane inst = LobbyInfoPane.Instance;
                sb.AppendLine($"  LobbyInfoPane.Instance: {(inst == null ? "NULL" : inst.gameObject.name + " | parent=" + (inst.transform.parent != null ? inst.transform.parent.name : "null") + " | children=" + inst.transform.childCount)}");
            }
            catch (Exception e) { sb.AppendLine($"  LobbyInfoPane.Instance: EXCEPTION {e.GetType().Name}"); }

            var hip = gs.HostInfoPanel;
            sb.AppendLine($"  gs.HostInfoPanel: {(hip == null ? "NULL" : hip.gameObject.name)}");

            if (lip != null)
            {
                var bg = lip.InfoPaneBackground;
                sb.AppendLine($"  InfoPaneBackground: {(bg == null ? "NULL" : "parent=" + (bg.transform.parent != null ? bg.transform.parent.name : "null"))}");
                var copy = lip.CopyCodeText;
                sb.AppendLine($"  CopyCodeText: {(copy == null ? "NULL" : "parent=" + copy.transform.parent.name)}");
            }

            sb.AppendLine($"  gs.EditButton: {(gs.EditButton == null ? "NULL" : "parent=" + gs.EditButton.transform.parent.name)}");
            sb.AppendLine($"  gs.HostViewButton: {(gs.HostViewButton == null ? "NULL" : "parent=" + gs.HostViewButton.transform.parent.name)}");
            sb.AppendLine($"  gs.ClientViewButton: {(gs.ClientViewButton == null ? "NULL" : "parent=" + gs.ClientViewButton.transform.parent.name)}");
            sb.AppendLine($"  gs.GameRoomButton: {(gs.GameRoomButton == null ? "NULL" : "parent=" + gs.GameRoomButton.transform.parent.name)}");
            sb.AppendLine($"  gs.PlayerCounter: {(gs.PlayerCounter == null ? "NULL" : "parent=" + gs.PlayerCounter.transform.parent.name)}");
            sb.AppendLine($"  gs.MapImage: {(gs.MapImage == null ? "NULL" : "parent=" + gs.MapImage.transform.parent.name)}");

            if (gs.StartButton != null)
            {
                var ap = gs.StartButton.GetComponent<AspectPosition>();
                string apDesc = ap == null ? "NONE" : "y=" + ap.DistanceFromEdge.y + " z=" + ap.DistanceFromEdge.z;
                sb.AppendLine($"  StartButton: AP={apDesc} | localPos=" + gs.StartButton.transform.localPosition
                    + " | world=" + gs.StartButton.transform.position
                    + " | parent=" + (gs.StartButton.transform.parent != null ? gs.StartButton.transform.parent.name : "null")
                    + " | parentWorldPos=" + (gs.StartButton.transform.parent != null ? gs.StartButton.transform.parent.position.ToString() : "null")
                    + " | parentScale=" + (gs.StartButton.transform.parent != null ? gs.StartButton.transform.parent.lossyScale.ToString() : "null"));
            }

            sb.AppendLine("  --- pane root detail ---");
            Transform pane = null;
            try { var i2 = LobbyInfoPane.Instance; if (i2 != null) pane = i2.transform; } catch { }
            if (pane == null && gs.LobbyInfoPane != null) pane = gs.LobbyInfoPane.transform;
            if (pane != null)
            {
                var ap2 = pane.GetComponent<AspectPosition>();
                sb.AppendLine($"  pane: name={pane.name} localPos={pane.localPosition} world={pane.position} localScale={pane.localScale} lossyScale={pane.lossyScale}"
                    + $" AP={(ap2 == null ? "NONE" : "anch=" + ap2.Alignment + " y=" + ap2.DistanceFromEdge.y + " x=" + ap2.DistanceFromEdge.x)}"
                    + $" parent={pane.parent?.name}");
                for (int i = 0; i < pane.childCount; i++)
                {
                    var c = pane.GetChild(i);
                    sb.AppendLine($"    child[{i}] {c.name} localPos={c.localPosition} world={c.position} localScale={c.localScale} children={c.childCount}");
                }
            }

            sb.AppendLine("  --- AspectSize chain (visual root) ---");
            Transform vis = gs.LobbyInfoPane != null && gs.LobbyInfoPane.InfoPaneBackground != null
                ? gs.LobbyInfoPane.InfoPaneBackground.transform : null;
            if (vis != null)
            {
                Transform cur = vis;
                int depth = 0;
                while (cur != null && depth < 8)
                {
                    var apx = cur.GetComponent<AspectPosition>();
                    var asz = cur.GetComponent<AspectSize>();
                    sb.AppendLine($"    [{depth}] {cur.name} localPos={cur.localPosition} world={cur.position} localScale={cur.localScale} lossy={cur.lossyScale}"
                        + $" AP={(apx == null ? "-" : apx.Alignment + "@" + apx.DistanceFromEdge.ToString())}"
                        + $" AspectSize={(asz == null ? "-" : "present")}"
                        + $" children={cur.childCount}");
                    cur = cur.parent;
                    depth++;
                }
            }

            sb.AppendLine("  --- countdown / top buttons ---");
            var gst = gs.GameStartText;
            if (gst != null)
            {
                var ap3 = gst.GetComponent<AspectPosition>();
                sb.AppendLine($"  GameStartText: localPos={gst.transform.localPosition} world={gst.transform.position} parent={gst.transform.parent?.name}"
                    + $" AP={(ap3 == null ? "NONE" : "anch=" + ap3.Alignment + " y=" + ap3.DistanceFromEdge.y + " x=" + ap3.DistanceFromEdge.x)} text={gst.text}");
            }
            if (gs.GameStartTextParent != null)
            {
                sb.AppendLine($"  GameStartTextParent: localPos={gs.GameStartTextParent.transform.localPosition} world={gs.GameStartTextParent.transform.position}");
            }
            if (gs.EditButton != null)
            {
                var p = gs.EditButton.transform.parent;
                while (p != null)
                {
                    if (p.name.Contains("Host Buttons") || p.name.Contains("AspectSize") || p.name.ToLower().Contains("hud"))
                    {
                        sb.AppendLine($"  EditButton ancestor: {p.name} world={p.position} localPos={p.localPosition}");
                        var apa = p.GetComponent<AspectPosition>();
                        if (apa != null) sb.AppendLine($"    {p.name} AP: anch={apa.Alignment} y={apa.DistanceFromEdge.y} x={apa.DistanceFromEdge.x}");
                    }
                    p = p.parent;
                }
            }

            AmethystPlugin.Logger.LogInfo(sb.ToString());
        }
        catch { }
    }

    private static void ApplyStartButtonLayout(PassiveButton btn, GameStartManager gs)
    {
        
    }

    private const float GameStartBlockLocalY = -2.61f;
    private static GameStartManager _gsbOwner;
    private static Transform _gsbRoot;
    private static bool _gsbLogged;

    private static void ApplyGameStartBlockLayout(GameStartManager gs)
    {
        if (gs == null) return;
        Transform root = gs.transform;
        if (root == null) return;

        if (_gsbOwner != gs || _gsbRoot != root)
        {
            _gsbOwner = gs;
            _gsbRoot = root;
            _gsbLogged = false;
            AmethystPlugin.Logger.LogInfo(
                $"[LobbyBlock] root={root.name} origLocalY={root.localPosition.y} world={root.position}");
        }

        Vector3 lp = root.localPosition;
        if (Mathf.Abs(lp.y - GameStartBlockLocalY) > 0.001f)
        {
            lp.y = GameStartBlockLocalY;
            root.localPosition = lp;
        }

        if (!_gsbLogged)
        {
            _gsbLogged = true;
            PassiveButton btn = gs.StartButton;
            AmethystPlugin.Logger.LogInfo(
                $"[LobbyBlock] applied localY={GameStartBlockLocalY} btnWorldY={(btn != null ? btn.transform.position.y : float.NaN)}");
        }
    }

    private static void ApplyLocalLabelLayout(GameStartManager gs)
    {
        
    }

    private const float PaneScale = 0.7f;
    private const float PaneShiftLeftX = 0.02f;   

    private static LobbyInfoPane _paneLip;
    private static Vector3 _paneOrigScale = Vector3.one;
    private static float _paneOrigEdgeX = float.NaN;
    private static float _paneAlignDelta;
    private static bool _paneApplied;
    private static readonly List<Behaviour> _paneDisabled = new List<Behaviour>();

    private static void ApplyLobbyPaneScale(GameStartManager gs)
    {
        LobbyInfoPane lip = null;
        try { if (gs != null) lip = gs.LobbyInfoPane; } catch { }
        if (lip == null) { try { lip = LobbyInfoPane.Instance; } catch { } }
        ApplyLobbyPaneScale(lip);
    }

    internal static void ApplyLobbyPaneScale(LobbyInfoPane lip)
    {
        bool on = AmethystConfig.ScaleLobbyPane != null && AmethystConfig.ScaleLobbyPane.Value;

        if (lip == null) return;

        if (_paneLip != null && _paneLip != lip)
        {
            RestoreLobbyPane();
        }

        if (_paneLip != lip)
        {
            _paneLip = lip;
            _paneApplied = false;
            _paneAlignDelta = 0f;
            _paneOrigScale = lip.transform.localScale;
            var ap0 = lip.GetComponent<AspectPosition>();
            if (ap0 != null) _paneOrigEdgeX = ap0.DistanceFromEdge.x;

        }

        if (on)
        {
            
            if (!_paneApplied)
            {
                _paneApplied = true;
                _paneDisabled.Clear();
                foreach (var ap in lip.GetComponentsInChildren<AspectPosition>(true))
                {
                    if (ap == null || !ap.enabled) continue;
                    if (ap.gameObject == lip.gameObject) continue; 
                    ap.enabled = false;
                    _paneDisabled.Add(ap);
                }
                foreach (var asz in lip.GetComponentsInChildren<AspectSize>(true))
                {
                    if (asz == null || !asz.enabled) continue;
                    if (asz.gameObject == lip.gameObject) continue;
                    asz.enabled = false;
                    _paneDisabled.Add(asz);
                }
            }

            var wantScale = _paneOrigScale * PaneScale;
            if ((lip.transform.localScale - wantScale).sqrMagnitude > 1e-6f)
                lip.transform.localScale = wantScale;

            var paneAp = lip.GetComponent<AspectPosition>();
            if (paneAp != null && !float.IsNaN(_paneOrigEdgeX))
            {
                float wantX = _paneOrigEdgeX + PaneShiftLeftX + _paneAlignDelta;
                if (Mathf.Abs(paneAp.DistanceFromEdge.x - wantX) > 0.002f)
                {
                    var v = paneAp.DistanceFromEdge;
                    v.x = wantX;
                    paneAp.DistanceFromEdge = v;
                    paneAp.AdjustPosition();
                }

                if (AmethystTopRightButtonGroup.TryGetRightEdge(out float groupRight))
                {
                    float paneRight = PaneRightEdge(lip);
                    if (!float.IsNaN(paneRight))
                    {
                        float delta = paneRight - groupRight;
                        if (Mathf.Abs(delta) > 0.003f)
                        {
                            _paneAlignDelta += delta;
                            var v2 = paneAp.DistanceFromEdge;
                            v2.x += delta;
                            paneAp.DistanceFromEdge = v2;
                            paneAp.AdjustPosition();
                        }
                    }
                }
            }
        }
        else if (_paneApplied)
        {
            RestoreLobbyPane();
        }
    }

    private static float PaneRightEdge(LobbyInfoPane lip)
    {
        try
        {
            if (lip.InfoPaneBackground != null)
            {
                var background = lip.InfoPaneBackground.GetComponent<SpriteRenderer>();
                if (background != null && background.sprite != null) return background.bounds.max.x;
            }
            float max = float.NaN;
            var all = lip.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var renderer = all[i];
                if (renderer == null || renderer.sprite == null || !renderer.enabled) continue;
                float right = renderer.bounds.max.x;
                if (float.IsNaN(max) || right > max) max = right;
            }
            return max;
        }
        catch { return float.NaN; }
    }

    private static void RestoreLobbyPane()
    {
        if (_paneLip == null) return;
        try
        {
            _paneLip.transform.localScale = _paneOrigScale;

            var ap = _paneLip.GetComponent<AspectPosition>();
            if (ap != null && !float.IsNaN(_paneOrigEdgeX))
            {
                var v = ap.DistanceFromEdge;
                v.x = _paneOrigEdgeX;
                ap.DistanceFromEdge = v;
                ap.AdjustPosition();
            }

            for (int i = 0; i < _paneDisabled.Count; i++)
                if (_paneDisabled[i] != null) _paneDisabled[i].enabled = true;
            _paneDisabled.Clear();
            _paneApplied = false;
            _paneAlignDelta = 0f;
        }
        catch { }
    }

    private static string GetGameString(StringNames name)
    {
        try
        {
            if (TranslationController.Instance != null)
                return TranslationController.Instance.GetString(name);
        }
        catch { }
        return string.Empty;
    }

    [HarmonyPatch(typeof(GameStartManager), "Update")]
    [HarmonyPrefix]
    private static void StartManagerUpdatePrefix(GameStartManager __instance)
    {
        if (__instance == null) return;
        try { __instance.MinPlayers = 1; } catch { }
    }

    [HarmonyPatch(typeof(GameStartManager), "Update")]
    [HarmonyPostfix]
    private static void StartManagerUpdatePostfix(GameStartManager __instance)
    {
        if (__instance == null) return;
        try
        {
            
            try { ApplyLobbyPaneScale(__instance); } catch { }

            try { ApplyGameStartBlockLayout(__instance); } catch { }

            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                
                __instance.StartButton.gameObject.SetActive(false);
                return;
            }

            __instance.GameStartTextParent.SetActive(false);
            __instance.StartButton.gameObject.SetActive(true);

            if (__instance.startState == GameStartManager.StartingStates.Countdown)
            {
                __instance.StartButton.buttonText.text =
                    string.Format("{0}: {1}", GetGameString(StringNames.Cancel), (int)__instance.countDownTimer + 1);
            }
            else
            {
                __instance.StartButton.buttonText.text = GetGameString(StringNames.StartLabel);
            }

            ApplyStartButtonLayout(__instance.StartButton, __instance);
            ApplyLocalLabelLayout(__instance);
        }
        catch { }
    }

    [HarmonyPatch(typeof(GameStartManager), "BeginGame")]
    [HarmonyPrefix]
    private static bool StartManagerBeginGamePrefix(GameStartManager __instance)
    {
        if (__instance == null) return true;
        
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;
        if (__instance.startState != GameStartManager.StartingStates.Countdown) return true;
        try
        {
            if (SoundManager.Instance != null && __instance.gameStartSound != null)
                SoundManager.Instance.StopSound(__instance.gameStartSound);
            __instance.ResetStartState();
        }
        catch { }
        return false;
    }

    [HarmonyPatch(typeof(GameStartManager), "ResetStartState")]
    [HarmonyPrefix]
    private static void ResetPrefix(GameStartManager __instance)
    {
        if (_cancelButton == null) return;
        try
        {
            _cancelButton.gameObject.SetActive(false);
            if (__instance.StartButton != null) __instance.StartButton.gameObject.SetActive(true);
            if (__instance.startState == GameStartManager.StartingStates.Countdown && SoundManager.Instance != null)
                SoundManager.Instance.StopSound(__instance.gameStartSound);
        }
        catch { }
    }
}
