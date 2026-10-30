using System;
using System.Collections.Generic;
using AmongUs.Data;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Amethyst.Patches;

// ===== 服装保存 / 快速切换 UI =====
//
// 说明：该功能在第三十五轮被整体移除（当时误判为「设置打不开」的元凶）；
// 后续已查明设置打不开的真正原因是原版 OptionsMenuBehaviour.Open() 抛 NRE（第三十六轮修复），
// 与本功能无关，故现在按需求恢复。恢复时保留了第三十四轮的两处加固：
//   1) PlayerTab.OnEnable 用 **Postfix**（绝不用 Prefix），保证原版初始化先跑完；
//   2) 模板prefab 跨场景存活（DontDestroyOnLoad(gameObject) + SetParent(null)）+ 真活校验。
//
// 另外吸取第四十轮的教训：**不再 Destroy 游戏对象**，
// 需要隐藏的图标/宠物一律用 SetActive(false)，避免组件仍持有引用导致每帧 NRE。

// 缓存仓库按钮模板（用于克隆出预设按钮）。
[HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class AmethystOutfitPrefabCachePatch
{
    internal static PassiveButton ButtonPrefab;

    private static void Postfix(MainMenuManager __instance)
    {
        // 无论配置开关如何都缓存模板，保证中途开启也能立即生效。
        if (__instance == null || __instance.inventoryButton == null) return;
        try
        {
            if (ButtonPrefab != null) return;

            // inventoryButton 属于主菜单场景；仓库页是另一个场景。
            // 必须把克隆体从场景层级中摘出并 DontDestroyOnLoad(gameObject)，
            // 否则会随主菜单场景卸载被销毁，之后在仓库页 Instantiate 会抛异常。
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

    // 仓库页注入：必须用 Postfix。仓库界面初始化完全依赖原版 OnEnable，
    // 若用 Prefix，前缀中任何一步抛异常都会让原版 OnEnable 不执行。
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

        // 模板必须是「真活对象」：跨场景后可能已随主菜单场景销毁，
        // 此时 C# 引用 != null 但底层已释放，Instantiate 会抛异常。
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

            // 回调需在 CreatePresetButton 之前构造，避免捕获变量未赋值告警
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
                    // 主菜单无 LocalPlayer：回退到 DataManager 刷新预览，
                    // 否则 UpdateFromLocalPlayer 抛 NRE 被吞、预览不动，看起来像“点了没反应”。
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

            // 隐藏模板自带的图标（不能用 Destroy：Unity 侧可能仍持有引用，会每帧 NRE）
            Deactivate(btn.transform.Find("Highlight/Icon"));
            Deactivate(btn.transform.Find("Inactive/Icon"));

            if (tab.PlayerPreview != null)
            {
                var clone = UnityEngine.Object.Instantiate(tab.PlayerPreview);
                clone.transform.SetParent(btn.transform);
                clone.transform.localPosition = new Vector3(-1.3f, 0.15f, -10f);
                clone.transform.localScale = Vector3.one * 0.45f;
                clone.ResetCosmetics();

                // 隐藏克隆预览里的宠物（同样不 Destroy）
                foreach (PetBehaviour pet in clone.GetComponentsInChildren<PetBehaviour>(true))
                    if (pet != null && pet.gameObject != null) pet.gameObject.SetActive(false);

                preview = clone;
            }

            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener(onClick);

            // 禁用翻译组件，防止游戏把我们的标签重写回“仓库/Inventory”
            foreach (var t in btn.GetComponentsInChildren<TextTranslatorTMP>(true))
                if (t != null) t.enabled = false;

            TextMeshPro textMesh = btn.GetComponentInChildren<TextMeshPro>();
            if (textMesh != null) textMesh.SetText(name);

            return btn;
        }
        catch { return null; }
    }

    // 每帧同步选中态与预览肤色。
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

    // 隐藏而不是销毁：避免残留引用被其它组件（如 AspectScaledAsset）每帧解引用而刷 NRE。
    private static void Deactivate(Transform t)
    {
        try
        {
            if (t != null && t.gameObject != null) t.gameObject.SetActive(false);
        }
        catch { }
    }
}
