using System;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystMainMenuStyle
{
    internal static bool Enabled => AmethystConfig.MainMenuStyle != null && AmethystConfig.MainMenuStyle.Value;

    private static readonly Color32 TierPrimary   = new Color32(0xA8, 0x55, 0xF7, 0xCC); 
    private static readonly Color32 TierSecondary = new Color32(0xC4, 0x94, 0xFF, 0xCC); 
    private static readonly Color32 TierTertiary  = new Color32(0x7E, 0x57, 0xC2, 0xCC); 

    internal static Color32 TierPrimaryColor => TierPrimary;

    private static readonly Color32 PanelTint = new Color32(0xD8, 0xBE, 0xFF, 0xFF);

    private const float LeftPanelScale = 0.7f;
    private static Transform _scaledLeftPanel;
    private static Vector3 _leftPanelOriginalScale;

    private static Color _panelOriginalColor;
    private static bool _hasPanelOriginal;
    private static bool AppliedOnce;
    private static int _styledCount;
    private static bool _uniformFound;
    private static MainMenuManager _menu;

    internal static void Apply(MainMenuManager menu)
    {
        try
        {
            if (menu == null) return;
            _menu = menu;

            if (!Enabled) { Restore(); return; }

            bool firstRun = !AppliedOnce;
            AppliedOnce = true;

            ScaleLeftPanel(menu);
            StyleButtons(menu);
            HideLeftPanelPlate();
            KeepDivider();
            HideOrphanTutorials(menu);
            DecorateRightPanel();

            if (firstRun) Diagnose(menu);
        }
        catch { }
    }

    internal static void ApplyLive()
    {
        try
        {
            if (!Enabled) { Restore(); return; }
            MainMenuManager menu = _menu;
            if (menu == null)
            {
                menu = UnityEngine.Object.FindObjectOfType<MainMenuManager>();
                _menu = menu;
            }
            if (menu == null)
            {
                
                ResetRightPanel();
                return;
            }
            Apply(menu);
        }
        catch { }
    }

    private static void ScaleLeftPanel(MainMenuManager menu)
    {
        
        Transform panel = menu.playButton != null ? menu.playButton.transform.parent : null;
        while (panel != null && panel.name != "LeftPanel") panel = panel.parent;
        if (panel == null) return;

        if (_scaledLeftPanel != panel)
        {
            RestoreLeftPanelScale();
            _scaledLeftPanel = panel;
            _leftPanelOriginalScale = panel.localScale;
            panel.localScale = Vector3.one * LeftPanelScale;
            AmethystPlugin.Logger?.LogInfo($"[MainMenuStyle] LeftPanel scale={panel.localScale}");
        }

        panel.localScale = Vector3.one * LeftPanelScale;
    }

    private static void RestoreLeftPanelScale()
    {
        if (_scaledLeftPanel != null) _scaledLeftPanel.localScale = _leftPanelOriginalScale;
        _scaledLeftPanel = null;
    }

    private static void Diagnose(MainMenuManager menu)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[MainMenuStyle] ");
            sb.Append("按钮=").Append(_styledCount).Append('/').Append(8);
            sb.Append(" 统一精灵=").Append(_uniformFound ? "取到" : "退回各自精灵");
            sb.Append(" | 面板=").Append(AmethystMainMenuSlide.Panel != null
                ? AmethystMainMenuSlide.Panel.name : "未捕获");
            sb.Append(" 底色=").Append(_hasPanelOriginal ? "已染紫" : "无SpriteRenderer");
            sb.Append(" | 底部边界=未改动");
            sb.Append(" 分隔线=").Append(DividerState());
            sb.Append(" | 回读 开始=").Append(ReadColor(menu.playButton));
            sb.Append(" 新闻=").Append(ReadColor(menu.newsButton));
            sb.Append(" 退出=").Append(ReadColor(menu.quitButton));
            sb.Append(" | 入口 自由模式=").Append(State(menu.freePlayButton != null
                ? menu.freePlayButton.gameObject : null))
                .Append(" 怎么玩=").Append(State(menu.howToPlayButton != null
                    ? menu.howToPlayButton.gameObject : null));
            AmethystPlugin.Logger?.LogInfo(sb.ToString());
        }
        catch { }
    }

    private static string DividerState()
    {
        try
        {
            GameObject d = GameObject.Find("MainUI/AspectScaler/LeftPanel/Main Buttons/Divider");
            if (d == null)
            {
                
                d = GameObject.Find("Divider");
                if (d == null) return "未找到";
                return "顶层同名 显示=" + d.activeSelf;
            }

            var sr = d.GetComponent<SpriteRenderer>();
            return "显示=" + d.activeSelf + " sr=" + (sr == null ? "无" : sr.enabled.ToString());
        }
        catch { return "异常"; }
    }

    private static string State(GameObject go)
    {
        try { return go != null ? (go.activeSelf ? "显示" : "隐藏") : "无"; }
        catch { return "异常"; }
    }

    private static void StyleButtons(MainMenuManager menu)
    {
        try
        {
            
            Sprite uniform = null;
            try { uniform = menu.quitButton?.activeSprites?.GetComponent<SpriteRenderer>()?.sprite; } catch { }
            _uniformFound = uniform != null;
            int n = 0;

            n += Tint(menu.playButton, TierPrimary, uniform);
            n += Tint(menu.inventoryButton, TierPrimary, uniform);
            n += Tint(menu.shopButton, TierPrimary, uniform);

            n += Tint(menu.newsButton, TierSecondary, uniform);
            n += Tint(menu.myAccountButton, TierSecondary, uniform);
            n += Tint(menu.settingsButton, TierSecondary, uniform);

            n += Tint(menu.creditsButton, TierTertiary, uniform);
            n += Tint(menu.quitButton, TierTertiary, uniform);

            _styledCount = n;
        }
        catch { }
    }

    private static int Tint(PassiveButton button, Color32 color, Sprite uniform)
    {
        if (button == null) return 0;
        try
        {
            
            try { button.activeSprites?.transform?.Find("Shine")?.gameObject.SetActive(false); } catch { }
            try { button.inactiveSprites?.transform?.Find("Shine")?.gameObject.SetActive(false); } catch { }

            if (uniform != null)
            {
                try
                {
                    var a = button.activeSprites?.GetComponent<SpriteRenderer>();
                    if (a != null) a.sprite = uniform;
                    var i = button.inactiveSprites?.GetComponent<SpriteRenderer>();
                    if (i != null) i.sprite = uniform;
                }
                catch { }
            }

            try
            {
                var a = button.activeSprites?.GetComponent<SpriteRenderer>();
                if (a != null) a.color = color;
                var i = button.inactiveSprites?.GetComponent<SpriteRenderer>();
                if (i != null) i.color = color;
            }
            catch { }

            try { button.activeTextColor = Color.white; } catch { }
            try { button.inactiveTextColor = Color.white; } catch { }
            try { button.disabledTextColor = new Color(1f, 1f, 1f, 0.5f); } catch { }
            try { if (button.buttonText != null) button.buttonText.color = Color.white; } catch { }
            return 1;
        }
        catch { return 0; }
    }

    private static void HideLeftPanelPlate()
    {
        DisableRenderer("LeftPanel");
    }

    private static void KeepDivider()
    {
        try
        {
            
            GameObject d = GameObject.Find("MainUI/AspectScaler/LeftPanel/Main Buttons/Divider");
            if (d != null && !d.activeSelf) d.SetActive(true);
        }
        catch { }
    }

    private static void HideOrphanTutorials(MainMenuManager menu)
    {
        
    }

    private static void DecorateRightPanel()
    {
        GameObject panel = AmethystMainMenuSlide.Panel;
        if (panel == null) return;   

        try
        {
            var sr = panel.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (!_hasPanelOriginal) { _panelOriginalColor = sr.color; _hasPanelOriginal = true; }
                if (sr.color != PanelTint) sr.color = PanelTint;
            }
        }
        catch { }
    }

    private static void ResetRightPanel()
    {
        try
        {
            GameObject panel = AmethystMainMenuSlide.Panel;
            if (panel != null && _hasPanelOriginal)
            {
                var sr = panel.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = _panelOriginalColor;
            }
        }
        catch { }
    }

    internal static void Restore()
    {
        try { RestoreLeftPanelScale(); } catch { }
        try { ResetRightPanel(); } catch { }
    }

    private static void DisableRenderer(string path)
    {
        try
        {
            GameObject go = GameObject.Find(path);
            if (go == null) return;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || !sr.enabled) return;
            sr.enabled = false;
        }
        catch { }
    }

    private static string Fmt(float v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static string ReadColor(PassiveButton b)
    {
        try
        {
            var sr = b?.inactiveSprites?.GetComponent<SpriteRenderer>();
            if (sr == null) return "无renderer";
            var c = sr.color;
            return string.Format("#{0:X2}{1:X2}{2:X2}", Mathf.RoundToInt(c.r * 255f),
                Mathf.RoundToInt(c.g * 255f), Mathf.RoundToInt(c.b * 255f));
        }
        catch { return "异常"; }
    }
}

[HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class AmethystMainMenuStyleStartPatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        
        AmethystMainMenuStyle.Apply(__instance);
    }
}

[HarmonyPatch(typeof(MainMenuManager), "LateUpdate")]
internal static class AmethystMainMenuStyleUpdatePatch
{
    private static float _next;

    private static void Postfix()
    {
        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + 1f;
        AmethystMainMenuStyle.ApplyLive();
    }
}
