using System;
using TMPro;
using UnityEngine;

namespace Amethyst;

internal static class AmethystRecapButton
{
    private static readonly Vector3 Pos = new Vector3(-4.5f, 2.6f, -14f);
    private static readonly Vector2 BtnScale = new Vector2(1.5f, 0.5f);
    private const float FontSize = 2f;

    private static PassiveButton _base;        
    private static PassiveButton _live;        
    private static Transform _liveParent;
    private static bool _lastVisible;

    internal static bool IsActive
    {
        get { try { return _live != null && _live.gameObject != null; } catch { return false; } }
    }

    internal static void SetBase(PassiveButton src)
    {
        try
        {
            if (_base != null && _base.gameObject != null) return;
            if (src == null) return;

            var holder = new GameObject("AmethystRecapButtonHolder");
            UnityEngine.Object.DontDestroyOnLoad(holder);

            _base = UnityEngine.Object.Instantiate(src, holder.transform);
            _base.name = "AmethystRecapButtonBase";
            UnityEngine.Object.DontDestroyOnLoad(_base.gameObject);
            _base.gameObject.SetActive(false);

            var aspect = _base.GetComponent<AspectPosition>();
            if (aspect != null) UnityEngine.Object.Destroy(aspect);
            var col = _base.GetComponent<BoxCollider2D>();
            if (col != null) col.offset = Vector2.zero;
        }
        catch { }
    }

    internal static void Tick()
    {
        try
        {
            if (!AmethystRecap.CanUse) { DestroyLive(); return; }
            if (_base == null || _base.gameObject == null) return;   

            Transform parent = CurrentParent();
            if (parent == null) { DestroyLive(); return; }

            if (_live == null || _live.gameObject == null || _liveParent != parent)
            {
                DestroyLive();
                _live = Create(parent);
                _liveParent = parent;
                _lastVisible = AmethystRecapWindow.IsOpen;
                if (_live != null && _live.gameObject != null) _live.gameObject.SetActive(true);
                return;
            }

            if (_lastVisible != AmethystRecapWindow.IsOpen)
            {
                _lastVisible = AmethystRecapWindow.IsOpen;
                SetLabel(_live);
            }
        }
        catch { }
    }

    private static void DestroyLive()
    {
        try { if (_live != null && _live.gameObject != null) UnityEngine.Object.Destroy(_live.gameObject); } catch { }
        _live = null;
        _liveParent = null;
    }

    private static Transform CurrentParent()
    {
        try
        {
            if (LobbyBehaviour.Instance != null)
            {
                if (HudManager.Instance != null) return HudManager.Instance.transform;
                return LobbyBehaviour.Instance.transform;
            }
            if (AmethystRecap.EndScreen != null) return AmethystRecap.EndScreen.transform;
        }
        catch { }
        return null;
    }

    private static string Label() => AmethystText.T(AmethystRecapWindow.IsOpen ? "RecapHide" : "RecapShow");

    private static void SetLabel(PassiveButton b)
    {
        try
        {
            var t = b?.transform.Find("FontPlacer/Text_TMP")?.GetComponent<TMP_Text>();
            if (t != null) t.text = Label();
        }
        catch { }
    }

    private static PassiveButton Create(Transform parent)
    {
        PassiveButton b = null;
        try
        {
            b = UnityEngine.Object.Instantiate(_base, parent);
            b.name = "RecapToggleButton";

            var aspect = b.GetComponent<AspectPosition>();
            if (aspect != null) UnityEngine.Object.Destroy(aspect);
            b.transform.localPosition = Pos;

            b.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            b.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                try { AmethystRecapWindow.ToggleVisible(); } catch { }
                SetLabel(b);
            }));

            var text = b.transform.Find("FontPlacer/Text_TMP")?.GetComponent<TMP_Text>();
            if (text != null)
            {
                var translator = text.GetComponent<TextTranslatorTMP>();
                if (translator != null) UnityEngine.Object.Destroy(translator);

                text.fontSize = text.fontSizeMax = text.fontSizeMin = FontSize;
                text.enableWordWrapping = false;
                text.text = Label();

                Transform container = text.transform.parent;
                if (container != null)
                {
                    var ca = container.GetComponent<AspectPosition>();
                    if (ca != null) UnityEngine.Object.Destroy(ca);
                    Vector3 cp = container.localPosition;
                    container.localPosition = new Vector3(0f, cp.y, cp.z);
                }

                var ta = text.GetComponent<AspectPosition>();
                if (ta != null) UnityEngine.Object.Destroy(ta);
                Vector3 tp = text.transform.localPosition;
                text.transform.localPosition = new Vector3(0f, tp.y, tp.z);
                text.horizontalAlignment = HorizontalAlignmentOptions.Center;
            }

            Color acc = AmethystStyle.Current.Accent;
            var normalSprite = b.inactiveSprites?.GetComponent<SpriteRenderer>();
            var hoverSprite = b.activeSprites?.GetComponent<SpriteRenderer>();
            if (normalSprite != null) normalSprite.color = new Color(acc.r * 0.78f, acc.g * 0.78f, acc.b * 0.78f, 1f);
            if (hoverSprite != null) hoverSprite.color = new Color(acc.r, acc.g, acc.b, 1f);

            var col = b.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.offset = Vector2.zero;
                if (normalSprite != null && hoverSprite != null)
                    normalSprite.size = hoverSprite.size = col.size = BtnScale;
            }

            return b;
        }
        catch
        {
            try { if (b != null && b.gameObject != null) UnityEngine.Object.Destroy(b.gameObject); } catch { }
            return null;
        }
    }
}
