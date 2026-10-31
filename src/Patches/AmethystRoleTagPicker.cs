using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystRoleTagPicker
{
    private const string MenuName = "AmethystRoleTagMenu";

    internal const float MenuScale = 1.1f;

    private static bool _active;
    private static ShapeshifterMinigame _menu;
    private static byte _targetId = byte.MaxValue;
    private static List<AmethystRoleTagStore.RoleEntry> _roles;

    internal static bool IsActive => _active;

    private static void ResetState()
    {
        RestoreCards();
        _menu = null;
        _roles = null;
        _active = false;
        _targetId = byte.MaxValue;
    }

    internal static void Open(byte targetPlayerId)
    {
        if (!AmethystRoleTagStore.Enabled) return;

        if (AmethystRoleTagStore.IsDeadPlayer(targetPlayerId)) return;

        if (MeetingHud.Instance == null || MeetingHud.Instance.gameObject == null) return;

        if (_active && (_menu == null || _menu.gameObject == null)) ResetState();
        if (_active) return;

        try
        {
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return;   

            List<AmethystRoleTagStore.RoleEntry> roles = AmethystRoleTagStore.BuildRoleList();
            if (roles == null || roles.Count == 0) return;

            ShapeshifterMinigame prefab = GetShapeshifterMenuPrefab();
            if (prefab == null) return;

            Transform parent = null;
            if (Camera.main != null) parent = Camera.main.transform;
            else if (HudManager.Instance != null) parent = HudManager.Instance.transform;
            if (parent == null) return;

            _roles = roles;
            _targetId = targetPlayerId;
            _active = true;

            _menu = UnityEngine.Object.Instantiate(prefab, parent, false);
            if (_menu == null)
            {
                ResetState();
                return;
            }

            Vector3 baseScale = _menu.transform.localScale;
            _menu.gameObject.name = MenuName;
            _menu.transform.localPosition = new Vector3(0f, 0f, -50f);
            _menu.gameObject.AddComponent<AmethystRoleTagMenuScale>().Init(baseScale);
            _menu.Begin(null);

            MatchTopLayer(_menu.gameObject);
        }
        catch (Exception ex)
        {
            try { AmethystPlugin.Logger?.LogWarning($"RoleTag picker open failed: {ex.Message}"); } catch { }
            ResetState();
        }
    }

    private static ShapeshifterMinigame GetShapeshifterMenuPrefab()
    {
        try
        {
            var all = RoleManager.Instance != null ? RoleManager.Instance.AllRoles : null;
            if (all == null) return null;

            RoleBehaviour behaviour = null;
            for (int i = 0; i < all.Count; i++)
            {
                RoleBehaviour b = all[i];
                if (b != null && b.Role == RoleTypes.Shapeshifter)
                {
                    behaviour = b;
                    break;
                }
            }
            if (behaviour == null) return null;

            ShapeshifterRole shifter = behaviour.TryCast<ShapeshifterRole>();
            if (shifter == null) return null;

            Transform holder = GameData.Instance != null ? GameData.Instance.transform : null;
            ShapeshifterRole temp = UnityEngine.Object.Instantiate(shifter, holder);
            if (temp == null) return null;

            ShapeshifterMinigame menu = temp.ShapeshifterMenu;
            UnityEngine.Object.Destroy(temp.gameObject);
            return menu;
        }
        catch { return null; }
    }

    internal static void Populate(ShapeshifterMinigame menu)
    {
        if (menu == null) return;

        List<AmethystRoleTagStore.RoleEntry> roles = _roles;
        if (roles == null) return;

        NetworkedPlayerInfo localData = null;
        try { localData = PlayerControl.LocalPlayer != null ? PlayerControl.LocalPlayer.Data : null; } catch { }
        if (localData == null) return;

        menu.potentialVictims = new Il2CppSystem.Collections.Generic.List<ShapeshifterPanel>();
        var selectable = new Il2CppSystem.Collections.Generic.List<UiElement>();

        for (int i = 0; i < roles.Count; i++)
        {
            int index = i;
            float x = menu.XStart + (i % 3) * menu.XOffset;
            float y = menu.YStart + (i / 3) * menu.YOffset;

            ShapeshifterPanel panel = UnityEngine.Object.Instantiate(menu.PanelPrefab, menu.transform);
            if (panel == null) continue;

            panel.transform.localPosition = new Vector3(x, y, -1f);

            panel.SetPlayer(i, localData, (Il2CppSystem.Action)(() => OnChosen(index)));

            menu.potentialVictims.Add(panel);
            selectable.Add(panel.Button);
        }

        try
        {
            HideMeetingCards();   
                        ControllerManager.Instance.OpenOverlayMenu(MenuName, menu.BackButton, menu.DefaultButtonSelected, selectable, false);
        }
        catch { }
    }

    internal static void RenderPanel(ShapeshifterPanel panel, int index)
    {
        if (panel == null) return;

        List<AmethystRoleTagStore.RoleEntry> roles = _roles;
        if (roles == null || index < 0 || index >= roles.Count) return;

        AmethystRoleTagStore.RoleEntry role = roles[index];

        if (panel.NameText != null)
        {
            panel.NameText.text = role.Label;
            panel.NameText.color = role.Color;
        }

        if (panel.LevelNumberText != null) panel.LevelNumberText.text = string.Empty;
        if (panel.ColorBlindName != null) panel.ColorBlindName.text = string.Empty;

    }

    private static void OnChosen(int index)
    {
        try
        {
            List<AmethystRoleTagStore.RoleEntry> roles = _roles;
            if (roles != null && index >= 0 && index < roles.Count && _targetId != byte.MaxValue)
            {
                AmethystRoleTagStore.RoleEntry entry = roles[index];
                if (entry != null && entry.IsClear) AmethystRoleTagStore.Clear(_targetId);        
                else if (entry != null) AmethystRoleTagStore.Set(_targetId, entry.Label, entry.Color);
            }
        }
        catch { }
        Close();
    }

    private static readonly System.Collections.Generic.List<GameObject> _hidCards =
        new System.Collections.Generic.List<GameObject>();

    internal static void HideMeetingCards()
    {
        try
        {
            MeetingHud hud = MeetingHud.Instance;
            if (hud == null || hud.playerStates == null) return;

            for (int i = 0; i < hud.playerStates.Count; i++)
            {
                PlayerVoteArea pva = hud.playerStates[i];
                if (pva == null) continue;

                GameObject go = pva.gameObject;
                if (go == null || !go.activeSelf) continue;

                go.SetActive(false);
                if (!_hidCards.Contains(go)) _hidCards.Add(go);
            }
        }
        catch { }
    }

    internal static void RestoreCards()
    {
        try
        {
            for (int i = 0; i < _hidCards.Count; i++)
            {
                GameObject go = _hidCards[i];
                if (go != null && !go.activeSelf) go.SetActive(true);
            }
            _hidCards.Clear();
        }
        catch { }
    }
    internal static void Close()
    {
        ShapeshifterMinigame menu = _menu;
        ResetState();

        if (menu == null) return;

        try { menu.Close(); } catch { }
    }

    internal static void OnMenuDisabled(ShapeshifterMinigame menu)
    {
        if (menu == null) return;
        if (_menu != null && _menu != menu) return;   
        ResetState();
    }

    internal static void Cleanup() => Close();

    internal static void MatchTopLayer(GameObject root)
    {
        if (root == null) return;

        string refLayer = null;
        int refOrder = 0;
        int refLayerValue = int.MinValue;

        try
        {
            MeetingHud hud = MeetingHud.Instance;
            if (hud != null && hud.gameObject != null)
            {
                Renderer[] mrs = hud.gameObject.GetComponentsInChildren<Renderer>(true);
                if (mrs != null)
                {
                    for (int i = 0; i < mrs.Length; i++)
                    {
                        Renderer r = mrs[i];
                        if (r == null) continue;

                        int lv;
                        try { lv = SortingLayer.GetLayerValueFromName(r.sortingLayerName); }
                        catch { lv = 0; }

                        if (lv > refLayerValue || (lv == refLayerValue && r.sortingOrder > refOrder))
                        {
                            refLayerValue = lv;
                            refOrder = r.sortingOrder;
                            refLayer = r.sortingLayerName;
                        }
                    }
                }
            }
        }
        catch { }

        if (string.IsNullOrEmpty(refLayer))
        {
            try
            {
                MeetingHud hud = MeetingHud.Instance;
                if (hud != null && hud.playerStates != null)
                {
                    for (int i = 0; i < hud.playerStates.Count; i++)
                    {
                        PlayerVoteArea pva = hud.playerStates[i];
                        if (pva == null || pva.NameText == null) continue;

                        Renderer nr = pva.NameText.GetComponent<Renderer>();
                        if (nr == null) continue;

                        refLayer = nr.sortingLayerName;
                        refOrder = nr.sortingOrder;
                        break;
                    }
                }
            }
            catch { }
        }

        try
        {
            SortingLayer[] all = SortingLayer.layers;
            if (all != null && all.Length > 0)
            {
                string top = all[all.Length - 1].name;
                if (!string.IsNullOrEmpty(top)) refLayer = top;
            }
        }
        catch { }

        if (string.IsNullOrEmpty(refLayer)) return;   

        try
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            int min = int.MaxValue;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (r.sortingOrder < min) min = r.sortingOrder;
            }
            if (min == int.MaxValue) return;

            int target = Mathf.Max(refOrder + 500, 30000);
            if (target > 32767) target = 32767;

            int shift = target - min;
            if (shift < 0) shift = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                if (r.sortingLayerName != refLayer) r.sortingLayerName = refLayer;

                int want = r.sortingOrder + shift;
                if (want > 32767) want = 32767;
                if (r.sortingOrder != want) r.sortingOrder = want;
            }
        }
        catch { }
    }
}

internal sealed class AmethystRoleTagMenuScale : MonoBehaviour
{
    private Vector3 _base = Vector3.one;
    private int _tick;

    public AmethystRoleTagMenuScale(IntPtr ptr) : base(ptr) { }

    internal void Init(Vector3 baseScale) => _base = baseScale;

    private void OnDestroy() { try { AmethystRoleTagPicker.RestoreCards(); } catch { } }

    private void LateUpdate()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_RoleTagMenu);
    try { LateUpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_RoleTagMenu); }
    }

    private void LateUpdateInner()
    {
            try
            {
                
                Vector3 want = _base * AmethystRoleTagPicker.MenuScale;
                if (transform.localScale != want) transform.localScale = want;

                _tick = _tick >= 20 ? 0 : _tick + 1;
                if (_tick == 0)
                {
                    
                    AmethystRoleTagPicker.MatchTopLayer(gameObject);
                }
            }
            catch { }
    
    }
}

[HarmonyPatch(typeof(ShapeshifterMinigame), "Begin")]
internal static class AmethystRoleTagMenuBeginPatch
{
    private static bool Prefix(ShapeshifterMinigame __instance)
    {
        if (!AmethystRoleTagPicker.IsActive) return true;
        try { AmethystRoleTagPicker.Populate(__instance); } catch { }
        return false;
    }
}

[HarmonyPatch(typeof(ShapeshifterPanel), "SetPlayer")]
internal static class AmethystRoleTagPanelPatch
{
    private static void Postfix(ShapeshifterPanel __instance, int index, NetworkedPlayerInfo playerInfo, Il2CppSystem.Action onShift)
    {
        if (!AmethystRoleTagPicker.IsActive) return;
        try { AmethystRoleTagPicker.RenderPanel(__instance, index); } catch { }
    }
}

[HarmonyPatch(typeof(ShapeshifterMinigame), "OnDisable")]
internal static class AmethystRoleTagMenuDisablePatch
{
    private static void Postfix(ShapeshifterMinigame __instance)
    {
        try { AmethystRoleTagPicker.OnMenuDisabled(__instance); } catch { }
    }
}

[HarmonyPatch(typeof(MeetingHud), "OnDestroy")]
internal static class AmethystRoleTagMenuCleanupPatch
{
    private static void Postfix() => AmethystRoleTagPicker.Cleanup();
}

[HarmonyPatch(typeof(MeetingHud), "Close")]
internal static class AmethystRoleTagMenuClosePatch
{
    private static void Postfix() => AmethystRoleTagPicker.Cleanup();
}
