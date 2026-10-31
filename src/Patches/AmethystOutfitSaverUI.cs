using System;
using System.Collections.Generic;
using AmongUs.Data;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class AmethystOutfitPrefabCachePatch
{
    internal static PassiveButton ButtonPrefab;

    private static void Postfix(MainMenuManager __instance)
    {
        
        if (__instance == null || __instance.inventoryButton == null) return;
        try
        {
            if (ButtonPrefab != null) return;

            ButtonPrefab = UnityEngine.Object.Instantiate(__instance.inventoryButton);
            UnityEngine.Object.DontDestroyOnLoad(ButtonPrefab.gameObject);
            ButtonPrefab.transform.SetParent(null, false);
            ButtonPrefab.gameObject.SetActive(false);
        }
        catch { }
    }
}

[HarmonyPatch]
internal static class AmethystOutfitSaverUIPatch
{
    private static readonly List<PassiveButton> _presetButtons = new List<PassiveButton>();
    private static readonly List<PoolablePlayer> _presetPreviews = new List<PoolablePlayer>();
    private static PlayerTab _setupInstance;

    private static readonly string[] _namesRu = { "Основной", "Наряд 1", "Наряд 2", "Наряд 3", "Наряд 4", "Наряд 5" };
    private static readonly string[] _namesEn = { "Main", "Outfit 1", "Outfit 2", "Outfit 3", "Outfit 4", "Outfit 5" };
    private static readonly string[] _namesZh = { "主服装", "服装一", "服装二", "服装三", "服装四", "服装五" };

    private static string[] Names => AmethystText.IsRussian ? _namesRu : (AmethystText.IsChinese ? _namesZh : _namesEn);

    [HarmonyPatch(typeof(PlayerTab), "OnEnable")]
    [HarmonyPostfix]
    private static void PlayerTab_OnEnable_Postfix(PlayerTab __instance)
    {
        if (AmethystCompat.OtherOutfitSaver || !AmethystConfig.OutfitSaver.Value || __instance == null) return;
        try
        {
            if (_setupInstance == null || _presetButtons.Count == 0)
            {
                _setupInstance = __instance;
                SetupPresets(__instance);
            }
            else
            {
                var saver = AmethystOutfitSaver.Instance;
                if (saver == null) return;
                var presets = saver.Presets;
                if (presets == null) return;

                for (int i = 0; i < _presetPreviews.Count && i < 6 && i < presets.Count; i++)
                {
                    var preview = _presetPreviews[i];
                    if (preview == null) continue;
                    preview.UpdateFromPlayerOutfit(presets[i].ToPlayerOutfit(), PlayerMaterial.MaskType.None, false, true);
                }
            }
        }
        catch { }
    }

    private static void SetupPresets(PlayerTab tab)
    {
        var saver = AmethystOutfitSaver.Instance;
        if (saver == null) return;

        _presetButtons.Clear();
        _presetPreviews.Clear();

        PassiveButton prefab = AmethystOutfitPrefabCachePatch.ButtonPrefab;
        if (prefab == null || !prefab) return;
        if (prefab.gameObject == null || !prefab.gameObject) return;

        var presets = saver.Presets;
        if (presets == null) return;

        for (int i = 0; i <= 5; i++)
        {
            int idx = i;
            if (idx >= presets.Count) break;
            var data = presets[idx];

            Action onClick = () =>
            {
                if (!saver.CanInteract()) return;
                if (idx == saver.SelectedPreset) return;
                saver.ApplyPresetFromUI(idx);

                foreach (var b in _presetButtons)
                    if (b != null) b.SetPassiveButtonHoverStateInactive();

                var theBtn = idx < _presetButtons.Count ? _presetButtons[idx] : null;
                if (theBtn != null) theBtn.SetPassiveButtonHoverStateActive();

                try
                {
                    if (tab.PlayerPreview == null) { }
                    
                    else if (PlayerControl.LocalPlayer != null)
                        tab.PlayerPreview.UpdateFromLocalPlayer(PlayerMaterial.MaskType.None);
                    else
                        tab.PlayerPreview.UpdateFromDataManager(PlayerMaterial.MaskType.None);
                }
                catch { }
            };

            PassiveButton btn = CreatePresetButton(tab, Names[i], new Vector3(2.5f, 1.55f - idx * 0.45f, 0f), out PoolablePlayer preview, onClick);

            if (preview != null)
            {
                try
                {
                    preview.UpdateFromPlayerOutfit(data.ToPlayerOutfit(), PlayerMaterial.MaskType.None, false, true);
                    preview.ToggleName(false);
                    preview.transform.position += new Vector3(0f, 0f, -1f * idx);
                }
                catch { }
            }
            _presetPreviews.Add(preview);
            _presetButtons.Add(btn);
        }
    }

    private static PassiveButton CreatePresetButton(PlayerTab tab, string name, Vector3 pos, out PoolablePlayer preview, Action onClick)
    {
        preview = null;

        PassiveButton prefab = AmethystOutfitPrefabCachePatch.ButtonPrefab;
        if (prefab == null || !prefab || tab == null) return null;

        try
        {
            var btn = UnityEngine.Object.Instantiate(prefab, tab.transform);
            if (btn == null) return null;

            btn.gameObject.SetActive(true);
            SetLayersBtn(btn.gameObject, "UI");
            btn.transform.localPosition = pos;
            btn.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            Deactivate(btn.transform.Find("Highlight/Icon"));
            Deactivate(btn.transform.Find("Inactive/Icon"));

            if (tab.PlayerPreview != null)
            {
                var clone = UnityEngine.Object.Instantiate(tab.PlayerPreview);
                clone.transform.SetParent(btn.transform);
                clone.transform.localPosition = new Vector3(-1.3f, 0.15f, -10f);
                clone.transform.localScale = Vector3.one * 0.45f;
                clone.ResetCosmetics();

                foreach (PetBehaviour pet in clone.GetComponentsInChildren<PetBehaviour>(true))
                    if (pet != null && pet.gameObject != null) pet.gameObject.SetActive(false);

                preview = clone;
            }

            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener(onClick);

            foreach (var t in btn.GetComponentsInChildren<TextTranslatorTMP>(true))
                if (t != null) t.enabled = false;

            TextMeshPro textMesh = btn.GetComponentInChildren<TextMeshPro>();
            if (textMesh != null) textMesh.SetText(name);

            return btn;
        }
        catch { return null; }
    }

    [HarmonyPatch(typeof(PlayerTab), "Update")]
    [HarmonyPrefix]
    private static void PlayerTab_Update_Prefix()
    {
        if (AmethystCompat.OtherOutfitSaver || !AmethystConfig.OutfitSaver.Value) return;
        try
        {
            var saver = AmethystOutfitSaver.Instance;
            if (saver == null) return;
            int sel = saver.SelectedPreset;

            for (int i = 0; i < _presetButtons.Count && i < 6; i++)
            {
                var btn = _presetButtons[i];
                if (btn == null) continue;
                if (i == sel)
                    btn.SetPassiveButtonHoverStateActive();
                else
                    btn.SetPassiveButtonHoverStateInactive();

                if (i >= _presetPreviews.Count) continue;
                var p = _presetPreviews[i];
                if (p == null) continue;

                try
                {
                    if (DataManager.Player != null && DataManager.Player.Customization != null)
                    {
                        if (p.Cosmetics.ColorId != DataManager.Player.Customization.Color)
                            p.SetBodyColor(DataManager.Player.Customization.Color);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static void SetLayersBtn(GameObject go, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1) return;
        Transform[] all = go.GetComponentsInChildren<Transform>(true);
        foreach (Transform c in all) c.gameObject.layer = layer;
    }

    private static void Deactivate(Transform t)
    {
        try
        {
            if (t != null && t.gameObject != null) t.gameObject.SetActive(false);
        }
        catch { }
    }
}
