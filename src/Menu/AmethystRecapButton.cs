using System;
using TMPro;
using UnityEngine;

namespace Amethyst;

// ===== 复盘「显示 / 隐藏信息」按钮 —— 改成游戏原生精灵按钮（第 221 轮）=====
//
// 需求：把原来画在屏幕左上角的 IMGUI 圆角按钮，换成**和 EHR 相同的 UI**，
//       但颜色仍用我们的主题色。
//
// 【照抄的是哪个 EHR 实现】
//   `EndlessHostRoles-8.0.2\Patches\OutroPatch.cs:708` 的 `ResultsToggleButton`：
//     位置 `(-4.5f, 2.6f, -14f)`、`Scale = new(1.5f, 0.5f)`、`FontSize = 2f`、
//     标签在 `ShowResults` / `HideResults` 之间切换、设置 `Main.ShowResult` 后刷新标签。
//   它用的 `SimpleButton`（`Modules\SimpleButton.cs`）本质就是：
//     **克隆一个原版 PassiveButton → 去掉 AspectPosition → 自设 localPosition / 颜色 / 缩放 / 字号 / 标签**。
//   这套构造与本模组 `AmethystMainMenuButtons.CreateButton` 完全同源，所以直接复用那份写法。
//
// 【与 EHR 唯一的不同】颜色：EHR 是蓝(0,165,255)→青(0,255,255)；
//   这里换成 `AmethystStyle.Current.Accent`（暗一档作常态、本体作悬停），即"我们的主题色"。
//
// ⚠ 取不到模板按钮时（例如玩家从没进过主菜单就直连大厅）本模块**静默不生效**，
//   由 `AmethystRecapWindow` 回退画原来的 IMGUI 按钮 —— 不能让用户因为克隆失败而彻底没有这个按钮。
internal static class AmethystRecapButton
{
    private static readonly Vector3 Pos = new Vector3(-4.5f, 2.6f, -14f);
    private static readonly Vector2 BtnScale = new Vector2(1.5f, 0.5f);
    private const float FontSize = 2f;

    private static PassiveButton _base;        // 常驻模板（DontDestroyOnLoad）
    private static PassiveButton _live;        // 当前场景里的实例
    private static Transform _liveParent;
    private static bool _lastVisible;

    // 原生按钮是否已经接管（`AmethystRecapWindow` 用它决定要不要画 IMGUI 回退按钮）
    internal static bool IsActive
    {
        get { try { return _live != null && _live.gameObject != null; } catch { return false; } }
    }

    // 从主菜单按钮抓一个模板。时机与 `AmethystMainMenuButtons` 相同（`MainMenuManager.Start`）。
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

    // 每帧由 `AmethystHud` 调用：保证当前场景有按钮，并同步标签。
    internal static void Tick()
    {
        try
        {
            if (!AmethystRecap.CanUse) { DestroyLive(); return; }
            if (_base == null || _base.gameObject == null) return;   // 没模板 ⇒ 交给 IMGUI 回退

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

    // 大厅 → 挂在 HudManager 下（拿不到就退回 LobbyBehaviour）；
    // 结算 → 挂在 EndGameManager 下（与 EHR 的父节点一致）。
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

                // 居中：容器与文本的 x 归零（与 `AmethystMainMenuButtons` 的写法一致）
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

            // 颜色：我们的主题色（暗一档常态 / 本体悬停），替代 EHR 的蓝→青
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
