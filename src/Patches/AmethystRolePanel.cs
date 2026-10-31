using System;
using System.Text;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

internal static partial class AmethystRolePanel
{
    internal const string PanelName = "AmethystRolePanel";

    private static TaskPanelBehaviour _panel;    
    private static TaskPanelBehaviour _source;   
    private static TextMeshPro _tabText;         
    private static string _lastText;             

    private const float PosSmoothSpeed = 8f;     

    internal static bool Enabled { get; set; } = true;
    internal static bool ShowDeadRoles { get; set; }
    internal static bool ShowTeamRoles { get; set; }

    internal static void Tick()
    {
        try
        {
            if (!Enabled || !InMatch || IsHideAndSeekMode()) { Destroy(); return; }
            Ensure();
        }
        catch { }
    }

    private static bool InMatch
    {
        get
        {
            try
            {
                if (AmongUsClient.Instance == null || !AmongUsClient.Instance.IsGameStarted) return false;
                if (LobbyBehaviour.Instance != null) return false;
                if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return false;
                return true;
            }
            catch { return false; }
        }
    }

    private static bool IsHideAndSeekMode()
    {
        try
        {
            var options = GameOptionsManager.Instance?.CurrentGameOptions;
            if (options != null)
                return options.GameMode is GameModes.HideNSeek or GameModes.SeekFools;
            return GameManager.Instance != null && GameManager.Instance.IsHideAndSeek();
        }
        catch { return false; }
    }

    private static void Ensure()
    {
        try
        {
            if (_panel != null) return;

            HudManager hud = HudManager.Instance;
            if (hud == null || hud.TaskStuff == null) return;

            Transform root = hud.TaskStuff.transform;
            if (root == null) return;

            Transform t = root.FindChild("TaskPanel");
            if (t == null) return;

            _source = t.gameObject.GetComponent<TaskPanelBehaviour>();
            if (_source == null) return;

            GameObject clone = UnityEngine.Object.Instantiate(_source.gameObject, _source.transform.parent);
            if (clone == null) return;
            clone.name = PanelName;

            _panel = clone.GetComponent<TaskPanelBehaviour>();
            if (_panel == null)
            {
                UnityEngine.Object.Destroy(clone);
                return;
            }

            _panel.open = false;
            _panel.timer = 0f;

            try { DestroyTranslators(_panel.tab != null ? _panel.tab.gameObject : null); } catch { }

            _panel.transform.localPosition = _source.transform.localPosition - new Vector3(0f, 1f, 0f);

            _tabText = null;
            try
            {
                if (_panel.tab != null)
                    _tabText = _panel.tab.gameObject.GetComponentInChildren<TextMeshPro>();
            }
            catch { }
            if (_tabText == null)
            {
                try
                {
                    Transform tab = clone.transform.FindChild("Tab");
                    if (tab != null) _tabText = tab.gameObject.GetComponentInChildren<TextMeshPro>();
                }
                catch { }
            }

            _lastText = null;
        }
        catch { }
    }

    private static void DestroyTranslators(GameObject go)
    {
        if (go == null) return;
        try
        {
            foreach (Transform child in go.transform)
                DestroyTranslators(child.gameObject);

            var comps = go.GetComponents<MonoBehaviour>();
            if (comps != null)
            {
                for (int i = 0; i < comps.Length; i++)
                {
                    MonoBehaviour c = comps[i];
                    if (c != null && c.GetType().Name.Contains("Translator"))
                        UnityEngine.Object.Destroy(c);
                }
            }
        }
        catch { }
    }

    internal static void Destroy()
    {
        try
        {
            if (_panel != null && _panel.gameObject != null)
                UnityEngine.Object.Destroy(_panel.gameObject);
        }
        catch { }
        _panel = null;
        _source = null;
        _tabText = null;
        _lastText = null;
    }

    private const float MeetingOverlayZ = -20f;
    private static bool _meetingZAdjusted;

    internal static void ShowDuringMeeting()
    {
        try
        {
            if (IsHideAndSeekMode()) { Destroy(); return; }
            if (_panel == null || _panel.gameObject == null)
            {
                _meetingZAdjusted = false;
                return;
            }

            if (!_meetingZAdjusted)
            {
                _meetingZAdjusted = true;
                try
                {
                    Vector3 openPos = _panel.openPosition;
                    openPos.z = MeetingOverlayZ;
                    _panel.openPosition = openPos;
                }
                catch { }
            }

            if (!_panel.gameObject.activeSelf) _panel.gameObject.SetActive(true);
        }
        catch { }
    }

    private const float HideNSeekDropPixels = 140f;



    private static void UpdateTargetPositions(TaskPanelBehaviour panel)
    {
        try
        {
            if (panel == null || _source == null) return;

            float y = 0f;
            try { y = _source.taskText.textBounds.size.y + 1f; } catch { }

            bool taskingGm = IsTaskingGameMode();
            float defaultPos = taskingGm ? 2f : 0.6f;
            float drop = DropUnits();

            Vector3 targetClosed = new Vector3(
                _source.closedPosition.x,
                (taskingGm && _source.open ? y + 0.2f : defaultPos) + drop,
                _source.closedPosition.z);

            Vector3 targetOpen = new Vector3(
                _source.openPosition.x,
                (taskingGm && _source.open ? y : defaultPos) + drop,
                _source.openPosition.z);

            float t = 1f - Mathf.Exp(-PosSmoothSpeed * Time.deltaTime);

            panel.closedPosition = Vector3.Lerp(panel.closedPosition, targetClosed, t);
            panel.openPosition = Vector3.Lerp(panel.openPosition, targetOpen, t);
        }
        catch { }
    }

    private static bool IsTaskingGameMode()
    {
        try { if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek()) return true; } catch { }

        try
        {
            GameOptionsManager gom = GameOptionsManager.Instance;
            if (gom != null && gom.CurrentGameOptions != null)
            {
                GameModes mode = gom.CurrentGameOptions.GameMode;
                return mode is GameModes.Normal or GameModes.NormalFools
                    or GameModes.HideNSeek or GameModes.SeekFools;
            }
        }
        catch { }

        try
        {
            NetworkedPlayerInfo data = PlayerControl.LocalPlayer != null ? PlayerControl.LocalPlayer.Data : null;
            return data != null && data.Tasks != null && data.Tasks.Count > 0;
        }
        catch { return false; }
    }

    internal static void PortUpdate(TaskPanelBehaviour panel)
    {
        try
        {
            if (IsHideAndSeekMode()) { Destroy(); return; }
            if (panel == null || panel.taskText == null || panel.background == null || panel.tab == null) return;

            UpdateTextNow(panel);

            Transform bg = panel.background.transform;
            Vector3 extents = panel.background.sprite.bounds.extents;
            Vector3 tabExtents = panel.tab.sprite.bounds.extents;

            Vector3 size = panel.taskText.textBounds.size;
            bg.localScale = size.x > 0f
                ? new Vector3(size.x + 0.4f, size.y + 0.3f, 1f)
                : Vector3.zero;

            Vector3 bgPos = extents;
            bgPos.y = -bgPos.y;
            bgPos = Mul(bgPos, bg.localScale);
            panel.background.transform.localPosition = bgPos;

            Vector3 tabPos = Mul(tabExtents, panel.tab.transform.localScale);
            tabPos.y = -tabPos.y;
            tabPos.x += bgPos.x * 2f;
            panel.tab.transform.localPosition = tabPos;

            if (GameManager.Instance == null) return;

            UpdateTargetPositions(panel);

            panel.closedPosition = new Vector3(
                -panel.background.sprite.bounds.size.x * panel.background.transform.localScale.x,
                panel.closedPosition.y,
                panel.closedPosition.z);

            panel.timer = panel.open
                ? Mathf.Min(1f, panel.timer + (Time.deltaTime / panel.animationTimeSeconds))
                : Mathf.Max(0f, panel.timer - (Time.deltaTime / panel.animationTimeSeconds));

            Vector3 relativePos = new Vector3(
                Mathf.SmoothStep(panel.closedPosition.x, panel.openPosition.x, panel.timer),
                Mathf.SmoothStep(panel.closedPosition.y, panel.openPosition.y, panel.timer),
                panel.openPosition.z);

            panel.transform.localPosition = AspectPosition.ComputePosition(
                AspectPosition.EdgeAlignments.LeftTop, relativePos);
        }
        catch { }
    }

    private static void UpdateTextNow(TaskPanelBehaviour panel)
    {
        try
        {
            string title = AmethystText.T("RolePanel");
            if (_tabText != null && _tabText.text != title) _tabText.text = title;

            string body = Build();
            if (body == _lastText) return;
            _lastText = body;

            panel.SetTaskText(body);
        }
        catch { }
    }







}

[HarmonyPatch(typeof(TaskPanelBehaviour), "Update")]
internal static class AmethystRolePanelUpdatePatch
{
    private static bool Prefix(TaskPanelBehaviour __instance)
    {
        try
        {
            if (__instance == null || __instance.gameObject == null) return true;
            if (__instance.gameObject.name != AmethystRolePanel.PanelName) return true;

            AmethystRolePanel.PortUpdate(__instance);
            return false;
        }
        catch { return true; }
    }
}
