using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystMainMenuOptimize
{
    
    internal static bool Enabled => AmethystConfig.MainMenuOptimize != null && AmethystConfig.MainMenuOptimize.Value;

    private static readonly System.Collections.Generic.List<GameObject> _hidden = new System.Collections.Generic.List<GameObject>();
    private static readonly System.Collections.Generic.List<SpriteRenderer> _disabledRenderers = new System.Collections.Generic.List<SpriteRenderer>();
    private static MainMenuManager _menu;

    internal static void Tick(MainMenuManager menu)
    {
        try
        {
            if (!Enabled)
            {
                Restore();
                return;
            }
            if (menu == null) return;
            _menu = menu;

            HideObject("BackgroundTexture");

            HideRenderer("LeftPanel");
            ShowDivider();

            HideObject("WindowShine");
            HideRenderer("RightPanel");
            HideRenderer("MaskedBlackScreen");

            HideObject("MainUI/Tint");

            NormalizePlayButton(menu);

            if (AmethystConfig.DiagDisableFriendsBar == null || !AmethystConfig.DiagDisableFriendsBar.Value)
            {
                try { AmethystFriendsBar.TickLive(); } catch { }
            }
        }
        catch { }
    }

    internal static void TickLive()
    {
        try
        {
            MainMenuManager menu = _menu;
            if (menu == null)
            {
                menu = UnityEngine.Object.FindObjectOfType<MainMenuManager>();
                _menu = menu;
            }
            if (menu == null)
            {
                if (_hidden.Count > 0 || _disabledRenderers.Count > 0) Restore();
                return;
            }
            Tick(menu);
        }
        catch { }
    }

    private static void ShowDivider()
    {
        try
        {
            GameObject d = GameObject.Find("MainUI/AspectScaler/LeftPanel/Main Buttons/Divider");
            if (d != null && !d.activeSelf) d.SetActive(true);
        }
        catch { }
    }

    private static void NormalizePlayButton(MainMenuManager menu)
    {
        try
        {
            Transform t = menu.transform.Find("MainUI/AspectScaler/LeftPanel/Main Buttons/PlayButton/FontPlacer/Text_TMP");
            if (t == null) return;

            GameObject go = t.gameObject;
            if (go == null) return;

            var translator = go.GetComponent<TextTranslatorTMP>();
            if (translator != null && translator.enabled) translator.enabled = false;

            var text = go.GetComponent<TMPro.TextMeshPro>();
            if (text == null) return;

            string want = AmethystText.T("Play");
            if (!string.IsNullOrEmpty(want) && text.text != want) text.text = want;
        }
        catch { }
    }

    private static void HideFriendsBar()
    {
        
    }

    private static void HideObject(string path)
    {
        try
        {
            GameObject go = GameObject.Find(path);
            if (go == null || !go.activeSelf) return;
            go.SetActive(false);
            if (!_hidden.Contains(go)) _hidden.Add(go);
        }
        catch { }
    }

    private static void HideRenderer(string path)
    {
        try
        {
            GameObject go = GameObject.Find(path);
            if (go == null) return;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || !sr.enabled) return;
            sr.enabled = false;
            if (!_disabledRenderers.Contains(sr)) _disabledRenderers.Add(sr);
        }
        catch { }
    }

    internal static void Restore()
    {
        try
        {
            for (int i = 0; i < _hidden.Count; i++)
            {
                GameObject go = _hidden[i];
                if (go != null && !go.activeSelf) go.SetActive(true);
            }
            _hidden.Clear();

            for (int i = 0; i < _disabledRenderers.Count; i++)
            {
                SpriteRenderer sr = _disabledRenderers[i];
                if (sr != null) sr.enabled = true;
            }
            _disabledRenderers.Clear();
        }
        catch { }
    }
}

[HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class AmethystMainMenuOptimizeStartPatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        AmethystMainMenuOptimize.Tick(__instance);
    }
}
