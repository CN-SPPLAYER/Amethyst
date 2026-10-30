using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystMenuButton : MonoBehaviour
{
    private const float W = 140f, H = 44f;

    private GUIStyle _label;
    private float _pressAt = -1f;

    private static float _x = -1f, _y = -1f;
    private static bool _loaded;

    private bool _grabbing;
    private bool _dragging;
    private Vector2 _grab;
    private Vector2 _down;

    private static float _menuAt = -99f;
    private static bool _atMenu;
    private static MainMenuManager _menuMgr;

    // ⚠ 第 180 轮性能：旧实现每 0.4 秒 `FindObjectOfType<MainMenuManager>()` 一次。
    //   `FindObjectOfType` 是**全场景遍历**，主界面场景里有上千个对象（UI、活动立绘、
    //   角色模型…），每秒扫 2.5 次纯属浪费 —— 而且它绝大多数时候是"找得到但没变"。
    //   现在缓存实例并校验 `activeInHierarchy`：找到之后就一直复用，
    //   只有对象被销毁/失活时才在**下一个节流窗口**重新找一次。
    internal static bool AtMainMenu()
    {
        float t = Time.unscaledTime;

        // 已有有效引用 → 一次字段读就返回（绝大多数帧走这条）。
        if (_menuMgr != null)
        {
            try
            {
                if (_menuMgr.gameObject.activeInHierarchy) { _atMenu = true; return true; }
            }
            catch { }
            _menuMgr = null;   // 已销毁
        }

        if (t - _menuAt < 0.4f) return _atMenu;
        _menuAt = t;

        try
        {
            _menuMgr = Object.FindObjectOfType<MainMenuManager>();
            _atMenu = _menuMgr != null;
        }
        catch { _atMenu = false; }
        return _atMenu;
    }

    private static void EnsurePos()
    {
        if (_loaded) return;
        _loaded = true;
        _x = AmethystConfig.MenuButtonX.Value;
        _y = AmethystConfig.MenuButtonY.Value;
    }

    private static Rect Box()
    {
        EnsurePos();
        if (_x >= 0f && _y >= 0f) return new Rect(_x, _y, W, H);
        return new Rect(22f, HudManager.Instance == null ? 112f : 70f, W, H);
    }

    private static bool Visible()
    {
        if (AmethystMenu.Rebinding) return false;
        // Android 上屏幕菜单按钮是唯一可靠的入口，强制显示、不可关闭。
        if (!AmethystPlatform.IsAndroid && !AmethystConfig.MenuButton.Value) return false;
        return HudManager.Instance != null || AtMainMenu();
    }

    private static bool ReadPointer(out Vector2 gui, out bool down, out bool held, out bool up)
    {
        down = held = up = false;
        Vector2 sp = Input.mousePosition;
        bool has;
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            sp = t.position;
            down = t.phase == TouchPhase.Began;
            up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            held = down || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
            has = true;
        }
        else
        {
            down = Input.GetMouseButtonDown(0);
            up = Input.GetMouseButtonUp(0);
            held = Input.GetMouseButton(0);
            has = down || up || held;
        }
        gui = new Vector2(sp.x, Screen.height - sp.y);
        return has;
    }

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_MenuButton);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_MenuButton); }
    }

    public void UpdateInner()
    {
            if (!Visible()) { _grabbing = false; _dragging = false; return; }

            // 菜单打开时屏幕按钮直接不接收输入：菜单本身有右上角关闭、Esc 和菜单键。
            // 这样无论按钮和窗口是否重合、菜单是否缩放，都不会出现一次点击同时开关菜单。
            if (AmethystMenu.Opened) { _grabbing = false; _dragging = false; return; }

            ReadPointer(out Vector2 gui, out bool down, out bool held, out bool up);
            Rect box = Box();

            if (down && box.Contains(gui))
            {
                _grabbing = true;
                _dragging = false;
                _grab = gui - new Vector2(box.x, box.y);
                _down = gui;
                return;
            }

            if (!_grabbing) return;

            if (up)
            {
                if (!_dragging && box.Contains(gui)) { AmethystMenu.ToggleRequest = true; _pressAt = Time.unscaledTime; }
                else if (_dragging) { AmethystConfig.MenuButtonX.Value = _x; AmethystConfig.MenuButtonY.Value = _y; }
                _grabbing = false;
                _dragging = false;
                return;
            }

            if (!held) { _grabbing = false; _dragging = false; return; }

            if (!_dragging && (gui - _down).sqrMagnitude > 64f) _dragging = true;
            if (_dragging)
            {
                _x = Mathf.Clamp(gui.x - _grab.x, 2f, Screen.width - W - 2f);
                _y = Mathf.Clamp(gui.y - _grab.y, 2f, Screen.height - H - 2f);
            }
    
    }

    [HideFromIl2Cpp]
    internal void DrawGui()
    {
        int __p = AmethystPerfProbe.Begin(Section.S_MenuButtonDraw);
        try { DrawGuiInner(); }
        finally { AmethystPerfProbe.End(__p, Section.S_MenuButtonDraw); }
    }

    private void DrawGuiInner()
    {
        if (!Visible() || Event.current.type != EventType.Repaint) return;

        EnsureStyles();
        bool open = AmethystMenu.Opened;
        Rect r = Box();
        if (AmethystMenu.Opened) return;
        bool hover = _dragging || r.Contains(Event.current.mousePosition);
        bool active = open || _dragging;

        float press = 0f;
        if (_pressAt >= 0f)
        {
            press = 1f - Mathf.Clamp01((Time.unscaledTime - _pressAt) / 0.16f);
            if (press <= 0f) _pressAt = -1f;
        }

        // 参考 Toggle/Lock：纯文字、深色矩形、柔和投影 + 顶部高光，不再往按钮里贴一张图标。
        AmethystStyle.Halo(new Rect(r.x - 3f, r.y + 4f, r.width + 6f, r.height + 8f),
            A(Color.black, 0.55f), 4, 10);

        Color face = active
            ? new Color(0.10f, 0.105f, 0.12f, 0.97f)
            : new Color(0.045f, 0.047f, 0.057f, 0.96f);
        if (hover) face = new Color(face.r + 0.03f, face.g + 0.03f, face.b + 0.035f, 0.98f);
        if (press > 0f) face = Color.Lerp(face, Color.black, 0.25f * press);

        AmethystStyle.FillRounded(r, face, 4);
        AmethystStyle.Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, 1f), A(Color.white, 0.10f));
        AmethystStyle.Fill(new Rect(r.x + 1f, r.yMax - 2f, r.width - 2f, 1f), A(Color.black, 0.35f));

        string txt = AmethystText.T("Menu");
        var tr = new Rect(r.x, r.y, r.width, r.height);
        GUI.color = A(Color.black, 0.55f);
        GUI.Label(new Rect(tr.x, tr.y + 1f, tr.width, tr.height), txt, _label);
        GUI.color = Color.white;
        GUI.Label(tr, txt, _label);
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private void EnsureStyles()
    {
        if (_label != null) return;
        _label = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 17
        };
        AmethystFont.ApplyTo(_label);
        _label.normal.textColor = Color.white;
    }
}
