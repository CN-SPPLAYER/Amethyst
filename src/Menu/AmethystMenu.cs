using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using Amethyst.Patches;
using UnityEngine;

namespace Amethyst;

public sealed partial class AmethystMenu : MonoBehaviour
{
    private const int WindowId = 770731;
    private const float FullH = 610f;
    private const float MinW = 620f;
    private const float HeaderH = 64f;
    private const float SidebarW = 200f;
    private const float TabH = 44f;
    private const float RowH = 34f;
    private const float FooterH = 40f;

    private struct TabDef
    {
        public AmethystIcon Icon;
        public string Key;
        public TabDef(AmethystIcon icon, string key) { Icon = icon; Key = key; }
        public string Name => AmethystText.T(Key);
    }

    private static readonly TabDef[] Tabs =
    {
        new TabDef(AmethystIcon.Home, "TabHome"),
        new TabDef(AmethystIcon.Shield, "TabGuard"),
        new TabDef(AmethystIcon.Tune, "TabDev"),
        new TabDef(AmethystIcon.Star, "TabOther"),
        new TabDef(AmethystIcon.Rooms, "TabRooms"),
        new TabDef(AmethystIcon.People, "TabCredits"),
    };

    internal static bool Opened;
    internal static bool Typing;
    internal static bool ToggleRequest;

    private bool _open;
    private bool _collapsed;
    private int _tab;
    private int _slider;
    private float _menuScaleDraft;
    private bool _menuScaleDraftActive;
    private float _scroll;
    private float _scrollPending;
    private float _scrollGoal;
    private int _scrollFrame = -1;
    private float _maxScroll;
    private float _viewH;
    private bool _scrollGrab;
    private float _scrollLastY;
    private bool _scrollMoved;
    private float _h = FullH;
    private int _resize;
    // 第 204 轮：默认窗口从 720 加宽到 940。
    // 原因：内容区宽度 = 窗口宽 − 侧栏 200 − 边距 36 = 704，减去 12 的列间距后
    //   每列 346（卡片内文 310，开关标签还有 236px），两列才放得下不挤。
    // 720 宽时内容区只有 484，分两列每列 236 → 标签只剩 126px，会折行/截断。
    // 屏幕不够宽时 `ClampWindow` 会自动夹回可视范围，用户也可以自己拖窄。
    private Rect _window = new Rect(300f, 110f, 940f, FullH);
    private bool _mobileCentered;

    // ⚠ 第 166/167 轮：自实现拖动所需的状态。
    //   `_dragGrab` = 按下瞬间鼠标在**窗口内**的本地偏移（= 鼠标 - 窗口左上角），
    //   整段拖动过程中**保持不变**；每帧用 `鼠标位置 - _dragGrab` 直接算出窗口位置，
    //   不做任何累加（累加才是边缘抖动 / 瞬移的根源）。
    private bool _dragging;
    private Vector2 _dragGrab;

    private bool _built;
    private GUIStyle _windowBg;
    private GUI.WindowFunction _winFn;
    private GUIStyle _brand;
    private GUIStyle _verPill;
    private GUIStyle _tabStyle;
    private GUIStyle _sectionTitle;
    private GUIStyle _cardTitle;
    private GUIStyle _rowLabel;
    private GUIStyle _rowLabelLine;
    private GUIStyle _value;
    private GUIStyle _valueC;
    private GUIStyle _muted;
    private GUIStyle _footer;
    private GUIStyle _btnLabel;
    private GUIStyle _centerMuted;
    private GUIStyle _arrow;
    private GUIStyle _subTabStyle;
    private GUIStyle _smallBtn;
    private GUIStyle _rowName;
    private GUIStyle _rowInfo;
    private GUIStyle _invisible;
    private GUIStyle _keyBadge;
    private GUIStyle _star;

    private string _textFocus;
    private bool _wasTyping;

    private bool _rebinding;
    private ConfigEntry<KeyCode> _rebindTarget;
    internal static bool Rebinding;
    private bool _menuScaleDragging;

    private float _closeAt = -1f;
    private float _fade = 1f;

    // ===== 开合弹簧（第 221 轮：照抄 Nocturne / OnyxMenu 1.2.5 的开合手感）=====
    // _ax = 打开程度 0..1（打开方向允许轻微过冲），_av = 速度。
    // 参数与参考实现同值：打开欠阻尼（落位带一次很轻的回弹），关闭近临界阻尼（干脆收走）。
    // 动画只体现在"画出来的那一帧"，布局 / 拖动 / 缩放 / 命中判定全部不受影响。
    private const float OpenK = 260f, OpenC = 17.5f;
    private const float CloseK = 620f, CloseC = 52.3f;
    private float _ax;
    private float _av;
    private int _axFrame = -1;
    private int _press;
    private float _pressAt;

    // ===== 入场错峰（第 248 轮，对齐 OnyxMenu 1.2.5）=====
    // _openAt = 本次打开的时间戳（-1 = 已经落定 / 无入场动画）；
    // _enterAge = 距离打开过去了多久，Ent() 用它算每段延迟后的缓出；
    // _titleNow/_titleOld/_titleAt = 分区标题的交叉淡入（旧标题下沉淡出、新标题上浮淡入）。
    private float _openAt = -1f;
    private float _enterAge = 99f;
    // 第 280 轮：本次打开已经绘制过几帧。
    // 首次打开菜单时 Build()/字体/贴图预热可能产生一个长帧；用墙钟算 _enterAge
    // 会把 1 秒入场直接吃掉，表现就是"启动后第一次打开菜单没有动画（旧观感）"。
    // 以第一次真正绘制为计时起点即可消除这个差异。
    private int _openDraws;
    // 第 282 轮：入场计时改为「累计的、限幅的帧时间」，不再用墙钟。
    // 首开/资源预热造成长帧时，墙钟会让 1 秒入场瞬间走完（看起来没有动画）；
    // 累计限幅帧时间保证动画至少按真实渲染帧播放完。
    private float _enterClock;
    private float _tabClock;
    private float _titleClock;
    private int _clockFrame = -1;
    private bool _prewarmed;
    private string _titleNow = string.Empty;
    private string _titleOld;
    private float _titleAt = -10f;

    private float _tabAnimAt = -1f;
    // 第 249 轮：卡片级联（cascade）——完全对齐 Onyx 的入场方式。
    // _tabLag：开菜单后延迟 0.12s 再开始级联；切页签为 0。
    // _cascade：级联已运行秒数（-100 = 关闭）；_cardIdx：当前页签第几张卡。
    private float _tabLag;
    private float _cascade = -100f;
    private int _cardIdx;
    private Color _cardOuter = Color.white;
    private bool _cardTintOn;
    // 每个页签各自的子标签选择。索引 = 页签下标；
    // **最后的「设置」齿轮页下标是 Tabs.Length**，所以数组必须比 Tabs.Length 多开 1 个。
    //
    // ⚠ 这里原来写死 new int[5]，而 Tabs.Length == 5、设置页的 _tab == 5 ——
    // DrawSettings 第一行的 SubBar() 访问 _subTab[5] 直接抛 IndexOutOfRangeException，
    // 整页在画出任何东西之前就中断，表现就是「模组菜单里的设置页一片空白、啥都没有」。
    private readonly int[] _subTab = new int[Tabs.Length + 1];
    private int _cardGrp = -1;
    private Rect _tabHi;
    private bool _tabHiInit;
    private readonly Dictionary<object, float> _pillAnim = new Dictionary<object, float>();
    private readonly Dictionary<object, float> _pillVel = new Dictionary<object, float>();

    // 子标签的滑动高亮（第 221 轮）：与主标签 `_tabHi` 相同的做法，每槽位一个矩形。
    // 索引 = 页签下标（与 `_subTab` 同尺寸）。
    private readonly Rect[] _subHi = new Rect[Tabs.Length + 1];
    private readonly bool[] _subHiInit = new bool[Tabs.Length + 1];

    // 第 248 轮：径向光晕贴图 `_flareTex` 已删除 —— 改由 `AmethystStyle.Halo`
    // 现场按圆角矩形 SDF 生成九宫格贴图（拉伸不变形）。
    private Font _lastBuiltFont;

    private static Vector2 M => Event.current != null ? Event.current.mousePosition : new Vector2(-1f, -1f);

    private static int _mobile = -1;
    private static bool Mobile
    {
        get
        {
            if (_mobile < 0) { try { _mobile = Application.isMobilePlatform ? 1 : 0; } catch { _mobile = 0; } }
            return _mobile == 1;
        }
    }

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_Menu);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_Menu); }
    }

    public void UpdateInner()
    {
            bool menuKey = AmethystConfig.MenuKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.MenuKey.Value);
            if (!_rebinding && menuKey)
            {
                if (_open && _closeAt < 0f) RequestClose();
                else Open();
            }
            if (ToggleRequest)
            {
                ToggleRequest = false;
                if (!_rebinding) { if (_open && _closeAt < 0f) RequestClose(); else Open(); }
            }
            if (!_rebinding && _open && _closeAt < 0f && Input.GetKeyDown(KeyCode.Escape))
                RequestClose();
            Opened = _open;

            bool typing = _open && _closeAt < 0f && _textFocus != null;
            Typing = typing;
            if (typing) SetMoveable(false);
            else if (_wasTyping) SetMoveable(true);
            _wasTyping = typing;
    
    }

    private void Open()
    {
        // 照抄 OM（NocturneMenu.Open）：
        // 若当前已经处于开启状态（例如开着时再次被触发），保留当前弹簧进度（resume）；
        // 否则**弹簧完全归零**：打开瞬间 _ax = 0、位移 26px，下一帧平滑升起。
        // 这彻底解决了"打开瞬间的撕裂感" —— 之前不复位，关闭到一半再次打开时
        // 带着旧的残余速度与位置，或者第一帧初值不是 0，画面会在第一帧闪一下。
        bool resume = _open;
        if (!resume)
        {
            _ax = 0f;
            _av = 0f;
            _axFrame = -1;
            try { AmethystPlugin.Logger?.LogInfo($"[Menu] open prewarmed={_prewarmed}"); } catch { }
        }
        _open = true;
        _closeAt = -1f;
        // 第 248 轮：入场错峰从"真正打开"这一刻开始计时（resume 时不动画，直接落定）。
        _openAt = resume ? -1f : Time.unscaledTime;
        _enterAge = 0f;
        _openDraws = 0;
        _enterClock = 0f;
        _tabClock = 0f;
        _titleClock = 0f;
        _titleAt = Time.unscaledTime;
        // 与 Onyx `NocturneMenu.Open` 一致：开菜单也触发卡片级联，且延迟 0.12s 起手。
        _tabAnimAt = _openAt;
        _tabLag = 0.12f;
        _roomConfirm = string.Empty;
        _mobileCentered = false;
        try { Input.simulateMouseWithTouches = true; } catch { }
        SyncRoomListVisibility();
    }

    private void RequestClose()
    {
        if (_open && _closeAt < 0f) _closeAt = Time.unscaledTime;
        SyncRoomListVisibility();
    }

    private void SetTab(int nt)
    {
        if (nt == _tab) return;
        _tab = nt;
        _tabAnimAt = Time.unscaledTime;
        _tabLag = 0f;
        _tabClock = 0f;
        _titleClock = 0f;
        _scroll = 0f;
        _scrollPending = 0f;
        _scrollGoal = 0f;
        _roomConfirm = string.Empty;
        SyncRoomListVisibility();
    }

    private static float SmoothSat(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    // 入场错峰：delay 秒后才开始、len 秒内三次缓出到 1。
    // _enterAge > 50 表示"不在入场中"（99 是停机值），直接返回 1，稳态零开销。
    private float Ent(float delay, float len)
    {
        if (_enterAge > 50f) return 1f;
        float i = 1f - Mathf.Clamp01((_enterAge - delay) / len);
        return 1f - i * i * i;
    }

    private static int RectId(Rect r) => (Mathf.RoundToInt(r.x) * 73856093) ^ (Mathf.RoundToInt(r.y) * 19349663) ^ (Mathf.RoundToInt(r.width) * 83492791);

    private float PressK(int id)
    {
        if (_press != id) return 1f;
        float t = (Time.unscaledTime - _pressAt) / 0.13f;
        if (t >= 1f) { _press = 0; return 1f; }
        return Mathf.Lerp(0.97f, 1f, SmoothSat(t));
    }

    private void Pressed(Rect r, int id)
    {
        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown && r.Contains(e.mousePosition))
        {
            _press = id;
            _pressAt = Time.unscaledTime;
        }
    }

    private static void SetMoveable(bool value)
    {
        try { if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = value; }
        catch { }
    }

    [HideFromIl2Cpp]
    internal void DrawGui()
    {
        int __p = AmethystPerfProbe.Begin(Section.S_Menu);
        try { DrawGuiInner(); }
        finally { AmethystPerfProbe.End(__p, Section.S_Menu); }
    }

    private void DrawGuiInner()
    {
        SyncRoomListVisibility();
        if (!_open)
        {
            // 第 282 轮：主界面空闲时预生成样式/贴图/字形，避免第一次打开时卡一帧。
            PrewarmIfNeeded();
            return;
        }
        Build();
        HandleRebindCapture();

        float baseS = Mobile
            ? Mathf.Clamp(Screen.height / 850f, 0.8f, 2.4f)
            : Mathf.Clamp(Screen.height / 1080f, 0.72f, 2.1f);
        float userS = AmethystConfig.MenuScale.Value;
        float s = Mathf.Clamp(baseS * userS, 0.5f, 3.2f);
        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float vw = Screen.width / s;
        float vh = Screen.height / s;

        float now = Time.unscaledTime;
        // 第 280 轮：以本次打开后的第一次绘制作为入场计时起点。
        // 第一次打开时 Build()/字体/贴图预热可能耗时数百毫秒；若用墙钟，
        // _enterAge 会直接超过 1 秒，Ent()/卡片级联全部落定 —— 看起来就是旧版（没有动画）。
        // 在第一次绘制时把三个时间戳挪到当前帧，动画就能完整播放。
        if (_openAt >= 0f && _openDraws == 0)
        {
            _openAt = now;
            _titleAt = now;
            _tabAnimAt = now;
        }
        _openDraws++;

        // 第 282 轮：所有入场时钟每帧只推进一次。
        // OnGUI 同一帧会跑 Layout / Repaint / 鼠标等多个事件，直接累加会跑快 2~3 倍。
        if (_clockFrame != Time.frameCount)
        {
            _clockFrame = Time.frameCount;
            float cdt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_openAt >= 0f) _enterClock += cdt;
            if (_tabAnimAt >= 0f) _tabClock += cdt;
            _titleClock += cdt;
        }

        _enterAge = _openAt >= 0f ? _enterClock : 99f;
        if (_openAt >= 0f && _enterAge >= 1f) _openAt = -1f;
        bool closing = _closeAt >= 0f;

        // ===== 开合弹簧（第 221 轮，照抄 Nocturne / OnyxMenu 1.2.5）=====
        // 旧实现是两条固定时长的补间（开 0.26s / 关 0.2s），只有透明度在动，落位很"平"。
        // 现在改成参考实现的弹簧-阻尼模型：打开时窗口下移 26px 向上落位 + 淡入 + 开场光晕，
        // 带一次很轻的过冲，收尾自然；关闭时快速下坠淡出。
        //
        // ⚠ 实机反馈修正：**不再缩放窗口内容**。上一版把整个窗口从 93.5% 放大，
        //   字号随之短暂变化（"字忽大忽小"）。OM 那边也只缩放"外壳"、正文不缩放；
        //   我们这里外壳与正文在同一组里，索性整体不做缩放 —— 只保留位移 + 淡入 + 光晕。
        //
        // 细节（与参考实现一致的部分）：
        //   · **每帧只推进一次**（用 `Time.frameCount` 判重）—— OnGUI 同一帧会跑多个事件
        //     （Layout / MouseDown / Repaint…），不判重会把弹簧在同帧积分多次；
        //     也不再用"只在 Repaint 推进"，那会让同一帧内"画的位置"和"点的位置"差一帧
        //     （开合过程中点按钮/拖动都会有最多一帧的偏移）。
        //   · 帧时长 > 0.02s 时拆 3 个子步，掉帧 / 卡顿后不会发散；
        //   · lean（淡入进度）比 _ax 跑得快（×1.35）：颜色先出来，位移再慢慢落位。
        if (_axFrame != Time.frameCount)
        {
            _axFrame = Time.frameCount;
            float k = closing ? CloseK : OpenK;
            float c = closing ? CloseC : OpenC;
            float goal = closing ? 0f : 1f;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.05f) dt = 0.05f;
            int steps = dt > 0.02f ? 3 : 1;
            float hs = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                _av += (k * (goal - _ax) - c * _av) * hs;
                _ax += _av * hs;
            }
            if (_ax < 0f) _ax = 0f;
        }

        // 收尾判定同参考实现：贴近 0 或超过 0.7s 保险时间就彻底结束
        if (closing && (_ax <= 0.02f || now - _closeAt > 0.7f))
        {
            _open = false; _closeAt = -1f; _collapsed = false;
            _ax = 0f; _av = 0f;
            GUI.matrix = prevMatrix; return;
        }

        float lean = Mathf.Clamp01(_ax * 1.35f);
        float fade = SmoothSat(lean);
        float xs = Mathf.Min(_ax, 1.12f);               // 过冲上限 12%
        float veil = Mathf.Clamp01(_ax);                // 背景遮罩用线性值（同参考实现）
        float lift = (1f - xs) * 26f;                   // 起始下移 26px，随后向上落位
        if (closing) lift = (1f - xs) * 12f;            // 关闭只下坠
        // Onyx 用的是 `shown.m_YMin += Ui.Round(lift)`：位移必须落在整像素上。
        // 小数位移会让字形在亚像素位置反复重采样 —— 实机表现就是打开时"文字像素偏移/发虚"。
        float liftPx = Mathf.Round(lift);
        // 开场光晕：**跟着淡入一起出现**，不再抢在窗口之前。
        // 旧写法在开菜单的第一帧就把光晕打满（那时窗口还是全透明、遮罩也还没上），
        // 实机表现就是"先闪一团紫、窗口才出来" —— 即反馈里的"打开瞬间的撕裂感"。
        float f0 = Mathf.Clamp01(1f - _ax);
        float flare = closing ? 0f : fade * f0 * f0;
        _fade = fade;

        AmethystStyle.Fill(new Rect(0f, 0f, vw, vh), new Color(0.02f, 0.02f, 0.022f, 0.34f * veil));

        if (Mobile) CenterWindow(vw, vh);
        else
        {
            // 第 167 轮：无条件裁剪。拖动（HandleDrag）夹取的是**同一组边界**，
            //   两边结果幂等，不会再像 `GUI.DragWindow` 那样互相覆盖。
            ClampWindow(vw, vh);
            if (fade >= 1f && AnimSettled) HandleResize(vw, vh);
        }

        // ⚠ 第 167 轮（最终位置）：拖动必须在 `GUI.Window` **之前**处理。
        //   之前失败的两个原因都已定位：
        //     1) 放在 `GUI.Window` **之后**：窗口内任何 `GUI.Button` 在 MouseDown 时都会
        //        `Event.Use()`，事件类型变成 `Used`。标题栏上一点就可能已被消费，
        //        `e.type == MouseDown` 不成立 → **完全拖不动**；
        //        剩下能触发的只有边缘 8px 的 HandleResize → 表现就是「只会拉长窗口」。
        //     2) 放在窗口回调**内部**：回调结束后 `GUI.Window` 会把窗口矩形作为返回值
        //        赋回 `_window`，回调里改的位置同帧被覆盖 → 同样拖不动。
        //   放在这里则两全：`_window` 先改后画（`GUI.Window` 原样返回传入的矩形），
        //   而事件此时还没被任何控件消费。
        //   HandleResize 在上一行先跑：鼠标落在边缘 8px 内时它已 `Use()` 掉事件，
        //   这里就看不到 MouseDown —— 缩放优先于拖动，正是想要的优先级。
        HandleDrag(vw, vh);

        // 开场光晕 / 窗口投影（第 248 轮：改成 OnyxMenu 的圆角矩形 SDF 光晕）
        // 旧实现是一张 128² 的**径向**白色贴图 `StretchToFill` 铺到窗口矩形上，
        // 拉伸后必然变成"椭圆一团"，在宽窗口上尤其明显（这就是实机看到的"紫色椭圆"）。
        // 现在走 `AmethystStyle.Halo`：贴图本身按**圆角矩形距离场**生成，九宫格拉伸，
        // 无论窗口多宽多高，光晕都始终贴着圆角外缘，形状不会变形。
        if (flare > 0.02f || fade > 0.02f)
        {
            // Onyx 几何：纹理边长 = (Reach+Bend)*2+2，九宫格 border = Reach+Bend。
            // 调用方必须外扩 (Reach+grow)，核心圆角矩形才正好落在窗口边缘；
            // 只外扩 grow 会让亮核缩进窗口 40px 并被窗口盖住，光晕几乎不可见。
            const int reach = 40, bend = 18;
            float grow = reach + bend;
            // Onyx：只有"外壳"（投影 / 光晕）从 93.5% 绽放到 100%，正文完全不缩放。
            // 投影跟着窗口的 lift 走，光晕用窗口原位（与 Onyx 完全一致）。
            float shell = closing ? 1f : 0.935f + 0.065f * xs;
            float gW = _window.width * shell;
            float gH = _window.height * shell;
            float gX = _window.center.x - gW * 0.5f;
            float gY = _window.center.y - gH * 0.5f;
            Rect halo = new Rect(gX - grow, gY - grow, gW + grow * 2f, gH + grow * 2f);
            if (fade > 0.02f)
            {
                float dY = _window.center.y + liftPx - gH * 0.5f;
                AmethystStyle.Halo(new Rect(gX - reach, dY - reach + 12f, gW + reach * 2f, gH + reach * 2f),
                    new Color(0f, 0f, 0f, 0.6f * fade), bend, reach);
            }
            if (flare > 0.02f)
                AmethystStyle.Halo(halo, A(AmethystStyle.Current.Accent, 0.36f * flare), bend, reach);
        }

        // 动画位移：只做"下移回位"（不改尺寸、不改 _window）——
        // 字号恒定，布局 / 拖动 / 命中判定全部照旧；结束处由 `GUI.matrix = prevMatrix` 恢复。
        if (Mathf.Abs(liftPx) >= 1f)
            GUI.matrix = GUI.matrix * Matrix4x4.Translate(new Vector3(0f, liftPx, 0f));

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fade);

        if (_winFn == null) _winFn = (GUI.WindowFunction)DrawWindow;
        _window = GUI.Window(WindowId, _window, _winFn, string.Empty, _windowBg);

        GUI.color = prevColor;
        GUI.matrix = prevMatrix;
    }

    private const int RzL = 1, RzR = 2, RzT = 4, RzB = 8;

    // 动画是否已完全落定。开合过程中**暂缓"按下即拖"**：
    // 动画期间画面经过了位移/缩放，而 HandleDrag/HandleResize 是拿鼠标和脚本坐标系的
    // _window 直接比，不拦住的话"刚打开就拖"会带着最多 26px 的偏移起拖（窗口瞬跳一下）。
    // 阈值取 0.97 而不是 0.999：弹簧收尾的回摆会短暂跌破 0.999（会来回翻），
    // 而 0.97 之后的残差是 (1-0.97)*26 ≈ 0.8px 的位移 + 0.2% 的缩放 —— 肉眼不可见。
    // 缩放原本的门控是 `fade >= 1f`，这里再与落定状态相与。
    private bool AnimSettled => _closeAt < 0f && _ax >= 0.97f;

    private void HandleResize(float vw, float vh)
    {
        Event e = Event.current;
        if (_collapsed || e == null) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector2 m = e.mousePosition;
            bool inX = m.x >= _window.x - 8f && m.x <= _window.xMax + 8f;
            bool inY = m.y >= _window.y - 8f && m.y <= _window.yMax + 8f;
            int dir = 0;
            if (inY && Mathf.Abs(m.x - _window.x) <= 8f) dir |= RzL;
            if (inY && Mathf.Abs(m.x - _window.xMax) <= 8f) dir |= RzR;
            if (inX && Mathf.Abs(m.y - _window.y) <= 8f) dir |= RzT;
            if (inX && Mathf.Abs(m.y - _window.yMax) <= 8f) dir |= RzB;
            if (dir != 0) { _resize = dir; e.Use(); }
        }
        else if (_resize != 0 && e.type == EventType.MouseDrag)
        {
            Vector2 m = e.mousePosition;
            if ((_resize & RzR) != 0) _window.width = Mathf.Clamp(m.x - _window.x, MinW, Mathf.Max(MinW, vw - _window.x));
            if ((_resize & RzB) != 0) _h = Mathf.Clamp(m.y - _window.y, FullH, Mathf.Max(FullH, vh - _window.y));
            if ((_resize & RzL) != 0)
            {
                float right = _window.x + _window.width;
                float nx = Mathf.Clamp(m.x, 0f, right - MinW);
                _window.x = nx;
                _window.width = right - nx;
            }
            if ((_resize & RzT) != 0)
            {
                float bottom = _window.y + _h;
                float ny = Mathf.Clamp(m.y, 0f, bottom - FullH);
                _window.y = ny;
                _h = bottom - ny;
            }
            e.Use();
        }
        else if (_resize != 0 && e.type == EventType.MouseUp)
        {
            _resize = 0;
            e.Use();
        }
    }

    private void CenterWindow(float vw, float vh)
    {
        float w = Mathf.Clamp(720f, MinW, vw);
        float bodyH = Mathf.Clamp(vh * 0.9f, Mathf.Min(320f, vh), FullH);
        _window.width = w;
        _h = bodyH;
        _window.height = _collapsed ? HeaderH : bodyH;
        if (!_mobileCentered)
        {
            _mobileCentered = true;
            _window.x = Mathf.Max(0f, (vw - w) * 0.5f);
            _window.y = Mathf.Max(0f, (vh - _window.height) * 0.5f);
        }
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, vw - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, vh - _window.height));
    }

    private void ClampWindow(float vw, float vh)
    {
        _window.width = Mathf.Clamp(_window.width, MinW, Mathf.Max(MinW, vw));
        _h = Mathf.Clamp(_h, FullH, Mathf.Max(FullH, vh));
        _window.height = _collapsed ? HeaderH : _h;
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, vw - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, vh - _window.height));
    }
// ⚠ 第 166/167 轮：自实现的窗口拖动（替换 `GUI.DragWindow`）。
    //
    // 为什么要换掉 `GUI.DragWindow`：
    //   它把「抓取点相对窗口的偏移」存在自己的内部状态里，每帧用 `鼠标位置 - 偏移`
    //   算新位置，**完全不理会外部的裁剪**。而 DrawGui 每帧都会 `ClampWindow`
    //   把窗口夹回可视范围，于是两者互相覆盖同一份位置：
    //     · 拖到最下边缘时：DragWindow 算出的 y 继续增大，ClampWindow 又把它夹回来，
    //       两股力每帧对撞 → 窗口在边缘**抖动 / 掉帧**；
    //     · 继续往左下或右下拖时：DragWindow 内部累积的 y 偏移已经远大于实际位置，
    //       一旦 x 变化，它就用那个错误的累积值重算整个位置 → 窗口**瞬移**。
    //
    // 自实现后位置只有一个来源（**无累加**）：
    //   按下时记下抓取偏移（鼠标 − 窗口左上角），全程不变；
    //   每帧 `鼠标 − 抓取偏移` 得到目标位置，再夹到可视范围。
    //   夹取只改窗口位置、**不污染抓取偏移**，所以顶到边缘后继续左右拖，
    //   另一个轴会稳稳停在边缘，不会再跳。
    //
    // 调用位置：DrawGui 里 `GUI.Window` 之**前**（原因见那里的注释）。
    private void HandleDrag(float vw, float vh)
    {
        Event e = Event.current;
        if (e == null) return;

        // 兜底：鼠标在游戏窗口外松开时收不到 MouseUp，用输入状态解除拖动，避免"粘住"。
        if (_dragging && e.type != EventType.MouseUp && !Input.GetMouseButton(0))
        {
            _dragging = false;
            return;
        }

        if (!_dragging)
        {
            if (e.type != EventType.MouseDown || e.button != 0 || !AnimSettled) return;

            // 这里是 `GUI.Window` 之后，`mousePosition` 与 `_window` 属于**同一套 GUI 坐标**，
            // 所以先换算成窗口本地坐标，再判断「是不是按在标题栏上」。
            // （第 166 轮的错误就是拿整屏坐标直接和 HeaderH 这种**本地**尺寸比 —— 永远不成立。）
            Vector2 m = e.mousePosition;
            float lx = m.x - _window.x;
            float ly = m.y - _window.y;

            // 右上一小块留给最小化 / 关闭按钮。
            // 按钮自己会 `Event.Use()` 消费掉按下（事件类型变成 Used），因此点在按钮上时
            // 根本走不到这里；这个范围判断只是双保险。
            if (lx < 0f || lx >= _window.width - 110f) return;
            if (ly < 0f || ly >= HeaderH) return;

            _dragging = true;
            // 抓取点 = 按下瞬间鼠标在窗口内的**本地**偏移（上面的 lx / ly 已经算好）。
            // 之后每帧都用 `鼠标 - 抓取点` **直接**算出窗口位置，不做任何累加；
            // 夹取只作用于算出来的结果，**不污染** _dragGrab ——
            // 这正是「顶到边缘后继续往左下/右下拖不会瞬移」的关键。
            _dragGrab = new Vector2(lx, ly);
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDrag)
        {
            Vector2 m = e.mousePosition;
            float maxX = Mathf.Max(0f, vw - _window.width);
            float maxY = Mathf.Max(0f, vh - _window.height);
            _window.x = Mathf.Clamp(m.x - _dragGrab.x, 0f, maxX);
            _window.y = Mathf.Clamp(m.y - _dragGrab.y, 0f, maxY);
            e.Use();
        }
        else if (e.type == EventType.MouseUp)
        {
            _dragging = false;
            e.Use();
        }
    }

    // ⚠ 第 167 轮（修正）：拖动**不能**放在这个回调里。
    //   DrawGui 里是 `_window = GUI.Window(WindowId, _window, _winFn, ...)` ——
    //   `GUI.Window` 调用完本回调后，会把**它自己**的窗口矩形作为返回值赋回 `_window`，
    //   于是回调内部对 `_window` 的修改会在同一帧被覆盖掉，表现就是「怎么拖都不动」。
    //   所以这里只负责画；拖动统一由 DrawGui 在 `GUI.Window` 返回**之后**执行（见 HandleDrag）。
    private void DrawWindow(int id)
    {
        AmethystPalette p = AmethystStyle.Current;
        float w = _window.width;
        float h = _window.height;
        GUI.color = new Color(1f, 1f, 1f, _fade);

        // 第 289 轮：窗口描边改为「外层 1px 填充 + 内层窗口填充」。
        // 旧 StrokeRounded 的 9-slice 1px 边在圆角处会产生忽粗忽细的锯齿（用户反馈的“边缘怪怪的”）。
        AmethystStyle.FillRounded(new Rect(0f, 0f, w, h), A(Color.white, 0.07f), 14);
        AmethystStyle.FillRounded(new Rect(1f, 1f, w - 2f, h - 2f),
            new Color(p.Window.r, p.Window.g, p.Window.b, 0.90f), 13);
        AmethystStyle.Fill(new Rect(14f, 1f, w - 28f, 1f), A(Color.white, 0.05f));

        DrawHeader(w, p);
        // 折叠状态下整个窗口就是标题栏，拖动由 DrawGui 里的 HandleDrag 统一处理
        // （第 166 轮：这里原来调用 GUI.DragWindow）。
        if (_collapsed) return;

        float sbTop = HeaderH + 8f;
        float sbBot = h - FooterH - 2f;
        AmethystStyle.FillRounded(new Rect(8f, sbTop, SidebarW - 14f, sbBot - sbTop), A(p.Panel, 0.55f), 10);
        AmethystStyle.Fill(new Rect(10f, sbTop + 1f, SidebarW - 18f, 1f), A(Color.white, 0.04f));
        AmethystStyle.Fill(new Rect(SidebarW - 2f, HeaderH + 12f, 1f, h - HeaderH - FooterH - 24f), A(Color.white, 0.06f));

        var navZone = new Rect(0f, HeaderH, SidebarW, h - HeaderH - FooterH);
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && navZone.Contains(Event.current.mousePosition))
        {
            SetTab(Mathf.Clamp(_tab + (Event.current.delta.y > 0f ? 1 : -1), 0, Tabs.Length));
            Event.current.Use();
        }

        float tabTop = HeaderH + 14f;
        float tabAvail = (h - FooterH - 2f) - tabTop - 6f;
        float tabStep = Mathf.Min(TabH + 4f, tabAvail / (Tabs.Length + 1));
        float th = Mathf.Min(TabH, tabStep - 3f);
        float settingsY = Mathf.Max(tabTop + Tabs.Length * tabStep, h - FooterH - th - 8f);
        var settingsRect = new Rect(14f, settingsY, SidebarW - 26f, th);
        Rect activeRect = _tab >= Tabs.Length ? settingsRect : new Rect(14f, tabTop + _tab * tabStep, SidebarW - 26f, th);
        if (!_tabHiInit) { _tabHi = activeRect; _tabHiInit = true; }
        if (Event.current != null && Event.current.type == EventType.Repaint)
            _tabHi = LerpRect(_tabHi, activeRect, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        {
            Color keepPill = GUI.color;
            GUI.color = new Color(keepPill.r, keepPill.g, keepPill.b, keepPill.a * Ent(0.22f, 0.3f));
            DrawTabHighlight(_tabHi);
            GUI.color = keepPill;
        }

        // 第 250 轮：照抄 Onyx `RailItem` —— 每行从左侧 14px 滑入、0.026s 错峰，
        // act 由行中心到滑块中心的距离得出（0..1），滑块滑动时颜色/底色跟着过渡。
        float ty = tabTop;
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tr = new Rect(14f, ty, SidebarW - 26f, th);
            float e = Ent(0.10f + i * 0.026f, 0.28f);
            float act = Mathf.Clamp01(1f - Mathf.Abs(tr.y - _tabHi.y) / Mathf.Max(1f, tabStep));
            Color keepTab = GUI.color;
            GUI.color = new Color(keepTab.r, keepTab.g, keepTab.b, keepTab.a * e);
            DrawTab(tr, Mathf.Round(-14f * (1f - e)), i, Tabs[i].Icon, Tabs[i].Name, act);
            GUI.color = keepTab;
            ty += tabStep;
        }
        {
            float e = Ent(0.10f + Tabs.Length * 0.026f, 0.28f);
            float act = Mathf.Clamp01(1f - Mathf.Abs(settingsRect.y - _tabHi.y) / Mathf.Max(1f, tabStep));
            Color keepTab = GUI.color;
            GUI.color = new Color(keepTab.r, keepTab.g, keepTab.b, keepTab.a * e);
            DrawTab(settingsRect, Mathf.Round(-14f * (1f - e)), Tabs.Length, AmethystIcon.Gear, AmethystText.T("Settings"), act);
            GUI.color = keepTab;
        }

        // ===== 分区标题：交叉淡入 + 纵向滑动（第 248 轮，取 OM 的 0.26s 节奏）=====
        // 页签变了才启动一次过渡：旧标题上移 7px 淡出，新标题从下方 7px 浮上来淡入，
        // 全程套一层 `Ent(0.16,0.32)` 上浮 8px，所以"开菜单"和"切页签"都有同一套落位感。
        // 宽度取 `w - SidebarW - 56`（右侧留出滚动条与窗口边距），文本不会压到内容区。
        float nowT = Time.unscaledTime;
        string sec = _tab < Tabs.Length ? Tabs[_tab].Name : AmethystText.T("Settings");
        if (!string.Equals(sec, _titleNow))
        {
            _titleOld = _titleNow;
            _titleNow = sec;
            _titleAt = nowT;
            _titleClock = 0f;
        }
        // 第 282 轮：标题交叉淡入同样用累计限幅帧时间。
        float tw = Mathf.Clamp01(_titleClock / 0.26f);
        float ti = 1f - tw;
        float te = 1f - ti * ti * ti;
        float tent = Ent(0.16f, 0.32f);
        float toy = Mathf.Round((1f - tent) * 8f);
        float tx = SidebarW + 4f;
        float tyy = HeaderH + 4f;
        float tww = w - SidebarW - 56f;
        if (tw < 1f && !string.IsNullOrEmpty(_titleOld))
        {
            Color keep = GUI.color;
            GUI.color = new Color(keep.r, keep.g, keep.b, keep.a * (1f - te) * tent);
            _sectionTitle.normal.textColor = p.Muted;
            GUI.Label(new Rect(tx, tyy - Mathf.Round(7f * te) + toy, tww, 30f), _titleOld, _sectionTitle);
            GUI.color = keep;
        }
        {
            Color keep = GUI.color;
            GUI.color = new Color(keep.r, keep.g, keep.b, keep.a * te * tent);
            _sectionTitle.normal.textColor = p.Text;
            GUI.Label(new Rect(tx, tyy + Mathf.Round(7f * (1f - te)) + toy, tww, 30f), _titleNow, _sectionTitle);
            GUI.color = keep;
        }

        var area = new Rect(SidebarW, HeaderH + 40f, w - SidebarW - 16f, h - HeaderH - 40f - FooterH);
        if (Event.current != null && Event.current.type == EventType.Layout && _scrollFrame != Time.frameCount)
        {
            // 第 250 轮：滚轮 / 拖动改成 Onyx 的指数平滑（exp -18），不再"一格一跳"。
            _scrollFrame = Time.frameCount;
            _scrollGoal = Mathf.Clamp(_scrollGoal + _scrollPending, 0f, _maxScroll);
            _scrollPending = 0f;
            if (Mathf.Abs(_scroll - _scrollGoal) > 0.4f)
                _scroll = Mathf.Lerp(_scroll, _scrollGoal, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            else
                _scroll = _scrollGoal;
        }
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && area.Contains(Event.current.mousePosition))
        {
            _scrollPending += Event.current.delta.y * 20f;
            Event.current.Use();
        }

        Event se = Event.current;
        if (se != null)
        {
            if (se.type == EventType.MouseDown)
            {
                _scrollGrab = area.Contains(se.mousePosition);
                _scrollLastY = se.mousePosition.y;
                _scrollMoved = false;
            }
            else if (se.type == EventType.MouseDrag && _scrollGrab && _slider == 0)
            {
                float dy = se.mousePosition.y - _scrollLastY;
                _scrollLastY = se.mousePosition.y;
                if (!_scrollMoved && Mathf.Abs(dy) > 6f) _scrollMoved = true;
                if (_scrollMoved) { _scrollPending -= dy; se.Use(); }
            }
            else if (se.type == EventType.MouseUp)
            {
                if (_scrollGrab && _scrollMoved) se.Use();
                _scrollGrab = false;
                _scrollMoved = false;
            }
        }

        // ===== 第 249 轮：完全对齐 Onyx 的卡片级联（cascade）=====
        // 旧实现是整页一起下移 30px 再上浮；Onyx 是每张卡各自错峰 0.035s、上浮 12px（取整）。
        // Onyx 不做“整页一起上浮”：每张卡按 0.035s 错峰、上浮 12px（取整）并淡入，
        // 分区标题（Sub）保持原样立即出现。开菜单延迟 0.12s，切页签立即开始。
        _cascade = -100f;
        if (_tabAnimAt >= 0f)
        {
            float age = _tabClock - _tabLag;
            if (age >= 0.5f) _tabAnimAt = -1f;
            // 负值也要赋值：Onyx 用负数让卡片在 tabLag 期间保持 alpha=0 等待，
            // 否则开菜单前 0.12s 卡片会先全亮再突然消失重放。
            else _cascade = age;
        }
        _cardIdx = 0;
        float cAlpha = 1f;

        Color cPrev = GUI.color;
        GUI.color = new Color(cPrev.r, cPrev.g, cPrev.b, cPrev.a * cAlpha);
        GUI.BeginGroup(area);

        // 这几个局部变量在 try 之后（滚动条计算与页脚）仍要使用，
        // 因此必须声明在 try 之外，否则会超出作用域。
        float x = 4f;
        float cx = x;
        float cw = area.width - 20f;
        float startY = 6f - _scroll;   // 内容起始 Y（级联上浮由每张卡自己处理）
        float cy = startY;

        try
        {
            _inScroll = true;
            DrawStars(area.width, area.height);
            _viewH = area.height;
            var localArea = new Rect(0f, 0f, area.width, area.height);
            _cardGrp = -1;
            BeginCardGrid(x, startY, cw);   // 第 204 轮：卡片两列瀑布（宽度不够时自动退回单列）
            switch (_tab)
            {
                case 0: DrawHome(cx, ref cy, cw); break;
                case 1: DrawGuard(cx, ref cy, cw); break;
                case 2: DrawDevTab(cx, ref cy, cw); break;
                case 3: DrawOther(cx, ref cy, cw); break;
                case 4: DrawModRooms(cx, ref cy, cw); break;
                case 5: DrawCredits(cx, ref cy, cw); break;
                case 6: DrawSettings(cx, ref cy, cw); break;
                default: DrawEmpty(localArea, AmethystIcon.Star, AmethystText.T("SectionInProgress")); break;
            }
            _cardGrp = -1; // 恢复全放行，避免任何未配对的 Grp 泄漏到页脚计算
        }
        catch (System.Exception ex)
        {
            // 单个页签画崩不能拖垮整个菜单：记日志并保证下面的 GUI.EndGroup() 一定会执行。
            // （之前「设置」页的 IndexOutOfRangeException 就是在这里抛出的 ——
            //   异常跳过了 EndGroup，IMGUI 组状态被破坏。）
            try { AmethystPlugin.Logger?.LogWarning($"menu tab {_tab} draw failed: {ex}"); } catch { }
        }
        finally
        {
            _inScroll = false;
            // 第 249 轮：卡片级联的逐卡 alpha 必须在 EndGroup 前还原，否则会漏到页脚 / 滚动条。
            if (_cardTintOn) { GUI.color = _cardOuter; _cardTintOn = false; }
            EndCardGrid(ref cy);   // 第 204 轮：滚动高度取两列里更高的那列（必须在 EndGroup 之前，cy 是外层局部变量）
            GUI.EndGroup();
            GUI.color = cPrev;
        }

        float contentH = cy - startY;
        float maxScroll = Mathf.Max(0f, contentH - (area.height - 12f));
        _maxScroll = maxScroll;
        if (maxScroll > 1f)
        {
            float trackH = area.height - 8f;
            float thumbH = Mathf.Max(28f, trackH * (area.height / Mathf.Max(contentH, 1f)));
            float thumbY = area.y + 4f + (trackH - thumbH) * (_scroll / maxScroll);
            AmethystStyle.FillRounded(new Rect(area.xMax - 5f, area.y + 4f, 3f, trackH), A(Color.white, 0.05f), 1);
            AmethystStyle.FillRounded(new Rect(area.xMax - 5f, thumbY, 3f, thumbH), A(p.Accent, 0.55f), 1);
        }

        float fy = h - FooterH + 4f;
        AmethystStyle.Fill(new Rect(SidebarW, h - FooterH, w - SidebarW - 16f, 1f), A(Color.white, 0.06f));
        string acc = Hex(p.Accent);
        string footerText =
            $"{AmethystText.T("Menu")} <color=#{acc}><b>{KeyName(AmethystConfig.MenuKey)}</b></color>     {AmethystText.T("LobbyCode")} <color=#{acc}><b>{KeyName(AmethystConfig.CopyCodeKey)}</b></color>     {AmethystText.T("Close")} <color=#{acc}><b>Esc</b></color>";
        LabelFade(new Rect(x, fy, cw, FooterH - 8f), footerText, _footer);

        Color grip = A(p.Muted, 0.5f);
        for (int gi = 0; gi < 3; gi++)
            for (int gj = 0; gj <= gi; gj++)
                AmethystStyle.Fill(new Rect(w - 8f - gj * 4f, h - 8f - gi * 4f, 2f, 2f), grip);

        // ⚠ 第 166/167 轮：这里原来调用 `GUI.DragWindow(new Rect(0f, 0f, w, HeaderH))`。
        //   它和 DrawGui 每帧执行的 ClampWindow 会互相覆盖同一份位置，
        //   正是「拖到最下边缘后左右拖会瞬移」与边缘抖动的根源 ——
        //   详见 HandleDrag 的注释。
        //   拖动现在由 DrawWindow 末尾的 HandleDrag 在窗口内部统一处理（本地坐标）。
    }

    [HideFromIl2Cpp]
    private void DrawHeader(float w, AmethystPalette p)
    {
        // 第 249 轮：文字入场与 Onyx 一致 —— 品牌从左侧 10px 处滑入并淡入（0.04s 起手）。
        float eBrand = Ent(0.04f, 0.3f);
        _brand.wordWrap = false;
        Color keepHdr = GUI.color;
        GUI.color = new Color(keepHdr.r, keepHdr.g, keepHdr.b, keepHdr.a * eBrand);
        float bx = Mathf.Round(20f - 10f * (1f - eBrand));
        AmethystLogo.Draw(new Rect(bx, 18f, 26f, 26f));
        var brandR = new Rect(bx + 32f, 15f, 220f, 34f);
        GUI.Label(brandR, "Amethyst", _brand);

        if (AmethystVersionChecker.UpdateAvailable)
        {
            Color ocUp = _verPill.normal.textColor;
            _verPill.normal.textColor = new Color(1f, 0.45f, 0.45f);
            GUI.Label(new Rect(250f, 22f, 180f, 20f), AmethystText.T("NewVersionAvailable"), _verPill);
            _verPill.normal.textColor = ocUp;
        }

        // 版本号从下 6px 浮入（Onyx 的 `Ent(0.10, 0.3)` 同值）。
        float eChip = Ent(0.10f, 0.3f);
        var ver = new Rect(w - 168f, 20f + Mathf.Round(-6f * (1f - eChip)), 62f, 24f);
        GUI.color = new Color(keepHdr.r, keepHdr.g, keepHdr.b, keepHdr.a * eChip);
        AmethystStyle.FillRounded(ver, A(p.Accent, 0.14f), 8);
        GUI.Label(ver, "v" + AmethystPlugin.PluginVersion, _verPill);
        GUI.color = keepHdr;

        var minB = new Rect(w - 96f, 20f, 26f, 24f);
        var closeB = new Rect(w - 62f, 20f, 26f, 24f);
        // 与 Onyx 第 1023 行同值：右上角按钮 0.16s 起手、0.30s 落位。
        GUI.color = new Color(keepHdr.r, keepHdr.g, keepHdr.b, keepHdr.a * Ent(0.16f, 0.3f));
        if (WindowButton(minB, AmethystIcon.Minimize, p)) _collapsed = !_collapsed;
        if (WindowButton(closeB, AmethystIcon.Close, p)) RequestClose();
        GUI.color = keepHdr;

        AmethystStyle.Fill(new Rect(0f, HeaderH - 1f, w, 1f), A(p.Accent, 0.30f));
    }

    [HideFromIl2Cpp]
    private bool WindowButton(Rect r, AmethystIcon icon, AmethystPalette p)
    {
        bool hover = r.Contains(M);
        int id = RectId(r);
        Pressed(r, id);
        float k = PressK(id);

        Matrix4x4 m = GUI.matrix;
        if (k < 1f) GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
        if (hover) AmethystStyle.FillRounded(r, A(Color.white, 0.07f), 7);
        AmethystIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), hover ? p.Text : p.Muted);
        GUI.matrix = m;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    private void DrawTabHighlight(Rect r)
    {
        AmethystPalette p = AmethystStyle.Current;
        AmethystStyle.FillRounded(r, A(p.Accent, 0.18f), 11);
        AmethystStyle.StrokeRounded(r, A(p.Accent, 0.38f), 11, 1);
        AmethystStyle.Fill(new Rect(r.x + 12f, r.y + 1f, r.width - 24f, 1f), A(Color.white, 0.08f));
        AmethystStyle.FillRounded(new Rect(r.x + 3f, r.y + 8f, 3f, r.height - 16f), p.Accent, 2);
    }

    // 第 250 轮：完全照抄 Onyx `RailItem` 的三件事 ——
    //   · 入场：整行从左侧 14px 滑入（位移取整）+ 淡入，逐项 0.026s 错峰；
    //   · 选中态不是布尔值，而是"行中心与滑块中心的距离" act（0..1），
    //     滑块滑动时图标/文字/悬停底色连续过渡，而不是瞬间切换；
    //   · 命中判定始终用最终位置 r，滑动过程中不会点空。
    private void DrawTab(Rect r, float slide, int index, AmethystIcon icon, string label, float act)
    {
        AmethystPalette p = AmethystStyle.Current;
        bool hover = r.Contains(M);
        bool on = act > 0.5f;
        var d = new Rect(Mathf.Round(r.x + slide), r.y, r.width, r.height);

        if (hover && act < 0.999f)
        {
            AmethystStyle.FillRounded(d, A(Color.white, 0.05f * (1f - act)), 11);
            AmethystStyle.StrokeRounded(d, A(Color.white, 0.06f * (1f - act)), 11, 1);
        }

        Color baseInk = hover ? A(p.Text, 0.95f) : A(p.Text, 0.62f);
        Color ink = Color.Lerp(baseInk, p.Accent, act);
        // 第 289 轮：删除图标后面的 28×28 紫色圆斑。
        // 选中态已经有 DrawTabHighlight 的整行胶囊滑块，再叠一个圆斑就是用户说的“文字旁边那个图像好丑”。
        AmethystIcons.Draw(icon, new Rect(d.x + 14f, d.y + d.height / 2f - 9f, 18f, 18f), ink);
        _tabStyle.normal.textColor = on ? p.Text : (hover ? A(p.Text, 0.92f) : A(p.Text, 0.66f));
        GUI.Label(new Rect(d.x + 44f, d.y, d.width - 48f, d.height), label, _tabStyle);
        if (GUI.Button(r, GUIContent.none, _invisible)) SetTab(index);
    }
private bool _inScroll;

    private bool Cull(float y, float h) => _inScroll && (y + h < -4f || y > _viewH + 4f);

    private bool RowCull(ref float y, float h, float step)
    {
        if (!Cull(y, h)) return false;
        y += step;
        return true;
    }

    private readonly Dictionary<string, bool> _cardCollapsed = new Dictionary<string, bool>();
    private readonly HashSet<string> _knownCards = new HashSet<string>();

    // ===== 第 204 轮：卡片**多列**排版（参考 ElysiumModMenu 的 block 布局）=====
    //
    // 问题：以前所有卡片都"一个接一个往下堆"（`y = card.yMax + 14f`），
    //   「防作弊」页光开关就三十几行 ⇒ 一根又长又窄的竖条，很不好看。
    //
    // 做法（抄 ElysiumModMenu `uimenu/SelfChatCompactTab.cs` 的思路：
    //   它是 `columns = contentWidth >= 560f ? 3 : 2` 然后把若干 block 并排放）：
    //   · 内容够宽 → 分两列，每张卡放进**当前较矮的那一列**（最短列优先），
    //     两列高度自然平衡；不够宽 → 退回单列，行为与旧版完全一致。
    //   · body 特别高的卡（逐 RPC 开关那种 30+ 行）**跨整行**，
    //     否则会出现一根又窄又高的柱子、另一列全空着。
    private const float CardGapX = 12f;
    private const float CardGapY = 12f;
    private const float GridTwoColsMinW = 620f;   // 内容宽度达到这个值才分两列
    private const float FullSpanBodyH = 560f;     // body 超过这个高度就跨整行
    private int _cardCols = 1;
    private readonly float[] _cardColY = new float[2];
    private readonly float[] _cardColX = new float[2];
    private float _cardColW;
    private bool _cardGridInit;

    private void BeginCardGrid(float x, float startY, float w)
    {
        _cardCols = w >= GridTwoColsMinW ? 2 : 1;
        _cardColW = (w - CardGapX) * 0.5f;
        _cardColX[0] = x;
        _cardColX[1] = x + _cardColW + CardGapX;
        _cardColY[0] = startY;
        _cardColY[1] = startY;
        // ⚠ 两列的**起始高度不在这里定**：页签开头还有 `SubBar`（子标签栏）等
        //   会先推进调用方的 y。真正的高度由 `Card` 第一次被调用时的 y 决定，
        //   否则第一张卡会压在子标签栏上面。见 Card 里的 `_cardGridInit`。
        _cardGridInit = false;
    }

    // 网格模式下滚动高度要按**两列里更高的一列**算，否则下面那列会被滚动条截掉。
    private void EndCardGrid(ref float y)
    {
        if (_cardCols <= 1) return;
        float maxY = _cardColY[0] > _cardColY[1] ? _cardColY[0] : _cardColY[1];
        if (maxY > y) y = maxY;
    }

    private void Grp(int g) => _cardGrp = g;

    private Rect Card(float x, ref float y, float w, string title, float bodyH)
    {
        AmethystPalette p = AmethystStyle.Current;
        const float head = 44f;

        // 第 249 轮：上一张卡的级联 alpha 在这里还原 —— 卡片正文由调用方在本次返回之后绘制。
        if (_cardTintOn) { GUI.color = _cardOuter; _cardTintOn = false; }

        if (_cardGrp >= 0 && _cardGrp != _subTab[SubTabSlot()])
            return new Rect(-20000f, -20000f, w - 36f, bodyH);

        if (!_knownCards.Contains(title)) _knownCards.Add(title);   // 抄 OnyxMenu：去重，避免每帧重复添加
        _cardCollapsed.TryGetValue(title, out bool collapsed);
        float shownBody = collapsed ? 0f : bodyH;

        // ---- 第 204 轮：两列瀑布式落位 ----
        // 单列时 `cardX/cardY` 就是调用方给的 x/y，行为与旧版一字不差；
        // 两列时忽略调用方的 y，改用"较矮那一列"的当前底边。
        //
        // ⚠ 两列的起始高度用**第一次调用 Card 时的 y**，而不是 BeginCardGrid 里的 startY：
        //   页签开头 `SubBar(...)` 会把 y 往下推（子标签栏），用 startY 会让第一张卡压上去。
        bool grid = _cardCols > 1;
        if (grid && !_cardGridInit)
        {
            _cardGridInit = true;
            _cardColY[0] = y;
            _cardColY[1] = y;
        }

        bool span = grid && shownBody >= FullSpanBodyH;      // 跨整行
        int col = 0;
        float cardX = x;
        float cardW = w;
        float cardY;
        if (grid && !span)
        {
            col = _cardColY[1] < _cardColY[0] ? 1 : 0;
            cardX = _cardColX[col];
            cardW = _cardColW;
            cardY = _cardColY[col];
        }
        else if (grid)
        {
            cardY = _cardColY[0] > _cardColY[1] ? _cardColY[0] : _cardColY[1];
        }
        else
        {
            cardY = y;
        }

        // 第 249 轮：Onyx 卡片级联 —— 每张卡错峰 0.035s（索引封顶 6），
        // 上浮 12px 且位移取整，整张卡（含正文文字）一起淡入。
        float cascadeRise = 0f;
        if (_cascade > -100f)
        {
            float e = SmoothSat((_cascade - Mathf.Min(_cardIdx, 6) * 0.035f) / 0.24f);
            cascadeRise = Mathf.Round((1f - e) * 12f);
            _cardOuter = GUI.color;
            GUI.color = new Color(_cardOuter.r, _cardOuter.g, _cardOuter.b, _cardOuter.a * e);
            _cardTintOn = true;
            _cardIdx++;
        }

        var card = new Rect(cardX, cardY + cascadeRise, cardW, head + shownBody + 14f);

        Event ce = Event.current;
        var headRect = new Rect(card.x, card.y, card.width, head);
        if (ce != null && ce.type == EventType.MouseDown && ce.button == 0 && headRect.Contains(ce.mousePosition))
        {
            collapsed = !collapsed;
            _cardCollapsed[title] = collapsed;
            ce.Use();
        }

        if (!Cull(card.y, card.height))
        {
            bool hover = card.Contains(M);
            AmethystStyle.FillRounded(card, new Color(p.Panel.r, p.Panel.g, p.Panel.b, 0.93f), 10);
            if (hover) AmethystStyle.FillRounded(card, A(Color.white, 0.03f), 10);
            AmethystStyle.StrokeRounded(card, hover ? A(p.Accent, 0.45f) : A(Color.white, 0.07f), 10, 1);
            AmethystStyle.Fill(new Rect(card.x + 12f, card.y + 1f, card.width - 24f, 1f), A(Color.white, 0.05f));

            AmethystStyle.FillRounded(new Rect(card.x + 18f, card.y + head / 2f - 6f, 2f, 12f), p.Accent, 1);
            GUI.Label(new Rect(card.x + 30f, card.y, card.width - 68f, head), title, _cardTitle);
            GUI.Label(new Rect(card.xMax - 34f, card.y, 22f, head), collapsed ? "▸" : "▾", _centerMuted);
            if (!collapsed) AmethystStyle.Fill(new Rect(card.x + 18f, card.y + head - 1f, card.width - 36f, 1f), A(Color.white, 0.05f));
        }

        // 推进落位游标：两列各推自己那根；跨整行的把两列一起推到同一高度。
        // 布局游标用**未上浮**的底边：卡片只在绘制时上浮，避免级联期间整页跟着抖。
        float cardBottom = cardY + card.height;
        float nextY = cardBottom + (grid ? CardGapY : 14f);
        if (grid && span) { _cardColY[0] = nextY; _cardColY[1] = nextY; }
        else if (grid) _cardColY[col] = nextY;

        y = cardBottom + 14f;
        return collapsed
            ? new Rect(-20000f, -20000f, card.width - 36f, bodyH)
            : new Rect(card.x + 18f, card.y + head + 8f, card.width - 36f, bodyH);
    }

    private void Sub(float x, ref float y, float w, string title)
    {
        if (Cull(y, 26f)) { y += 26f; return; }
        AmethystPalette p = AmethystStyle.Current;
        AmethystStyle.Fill(new Rect(x, y + 21f, w, 1f), A(Color.white, 0.06f));
        AmethystStyle.FillRounded(new Rect(x, y + 6f, 2f, 12f), A(p.Accent, 0.75f), 1);
        GUI.Label(new Rect(x + 10f, y - 1f, w - 12f, 22f), title, _cardTitle);
        y += 26f;
    }

    private static readonly string[] KickBanVals = { "Kick", "Ban" };
    private static readonly string[] VoteVals = { "Null", "Warn", "Kick", "ErrorKick", "Ban" };
    private static string[] _kickBanDisp, _voteDisp;
    private static string _dispLang;

    private static void EnsureDisp()
    {
        string lang = AmethystText.IsRussian ? "ru" : (AmethystText.IsChinese ? "zh" : "en");
        if (_kickBanDisp != null && lang == _dispLang) return;
        _dispLang = lang;
        _kickBanDisp = new[] { AmethystText.T("Kick"), AmethystText.T("Ban") };
        _voteDisp = new[] { AmethystText.T("Null"), AmethystText.T("Warn"), AmethystText.T("Kick"), AmethystText.T("ErrorKick"), AmethystText.T("Ban") };
    }

    private static string[] KickBanDisp { get { EnsureDisp(); return _kickBanDisp; } }
    private static string[] VoteDisp { get { EnsureDisp(); return _voteDisp; } }

    private static readonly string[] GuardSubs = { "GuardSubLists", "GuardSubFilters", "GuardSubShield", "GuardSubPerRpc" };

    [HideFromIl2Cpp]
    // 当前页签的子标签槽位；越界时退化为 0，避免任何索引问题把整页画崩。
    private int SubTabSlot() => _tab >= 0 && _tab < _subTab.Length ? _tab : 0;

    [HideFromIl2Cpp]
    private int SubBar(float x, ref float y, float w, string[] keys)
    {
        string[] names = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++) names[i] = AmethystText.T(keys[i]);
        int slot = SubTabSlot();
        int cur = Mathf.Clamp(_subTab[slot], 0, names.Length - 1);
        _subTab[slot] = cur;

        AmethystPalette p = AmethystStyle.Current;
        int cols = names.Length <= 4 ? names.Length : 3;
        int rows = Mathf.CeilToInt(names.Length / (float)cols);
        float cellW = (w - (cols - 1) * 6f) / cols;

        // ===== 滑动高亮（第 221 轮，参考 OM 的子标签）=====
        // 旧实现是"每一格自己亮/灭"，切换时高亮直接跳过去；现在改成一块会滑动的高亮：
        // 目标 = 当前子标签的格子，用与主标签 `_tabHi` 相同的指数趋近逼近，切换时平滑滑过去。
        // 每个页签槽位各记一份位置（`_subHi[slot]`），切回来时不会从头再滑一遍。
        var active = new Rect(x + (cur % cols) * (cellW + 6f), y + (cur / cols) * 32f, cellW, 28f);
        if (!_subHiInit[slot]) { _subHi[slot] = active; _subHiInit[slot] = true; }
        else if (Event.current != null && Event.current.type == EventType.Repaint)
            _subHi[slot] = LerpRect(_subHi[slot], active, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));

        // 1) 每格的基础底（未选中样式）
        for (int i = 0; i < names.Length; i++)
        {
            var r = new Rect(x + (i % cols) * (cellW + 6f), y + (i / cols) * 32f, cellW, 28f);
            if (Cull(r.y, r.height)) continue;
            AmethystStyle.FillRounded(r, A(Color.white, 0.045f), 8);
            AmethystStyle.StrokeRounded(r, A(Color.white, 0.07f), 8, 1);
        }

        // 2) 滑动高亮（压在标签下面）
        if (!Cull(_subHi[slot].y, _subHi[slot].height))
        {
            AmethystStyle.FillRounded(_subHi[slot], A(p.Accent, 0.20f), 8);
            AmethystStyle.StrokeRounded(_subHi[slot], A(p.Accent, 0.65f), 8, 1);
        }

        // 3) 标签 + 点击
        for (int i = 0; i < names.Length; i++)
        {
            var r = new Rect(x + (i % cols) * (cellW + 6f), y + (i / cols) * 32f, cellW, 28f);
            if (Cull(r.y, r.height)) continue;
            bool on = cur == i;
            // Onyx 的做法：滑块中心盖住哪个格子，哪个格子的文字才变亮。
            // 这样滑块滑过去时文字是"被点亮"的，而不是瞬间跳色。
            bool lit = _subHi[slot].Contains(new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f));
            _subTabStyle.normal.textColor = lit ? Color.Lerp(p.Accent, Color.white, 0.5f) : p.Muted;
            GUI.Label(r, names[i], _subTabStyle);
            if (GUI.Button(r, GUIContent.none, _invisible) && !on)
            {
                _subTab[slot] = i;
                _scroll = 0f;
                _scrollPending = 0f;
                _scrollGoal = 0f;
                // 与 Onyx 一致：切子标签也重放一次卡片级联。
                _tabAnimAt = Time.unscaledTime;
                _tabLag = 0f;
            }
        }
        y += rows * 32f + 6f;
        return cur;
    }

    private void DrawGuard(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, GuardSubs);

        Grp(0);
        Rect b = Card(x, ref y, w, AmethystText.T("AccessLists"), 2f * RowH + 94f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("BanListKickOnJoin"), AmethystConfig.AccessBanEnabled);
        GUI.Label(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{AmethystText.T("Ban_2")}: <b>{AmethystAccess.BanCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 86f, by + 1f, 82f, 24f), AmethystText.T("CLRBAN"), new Color(0.9f, 0.36f, 0.36f))) AmethystAccess.ClearBans();
        by += 32f;
        if (SmallButton(new Rect(b.x + 2f, by, 112f, 24f), AmethystText.T("ImportTXT"), AmethystStyle.Current.Accent)) AmethystAccess.ImportTxt();
        GUI.Label(new Rect(b.x + 122f, by, b.width - 122f, 24f), "Among Us/Amethyst/BanList.txt", _muted);
        by += 32f;
        Toggle(b.x, ref by, b.width, AmethystText.T("CloudBanEnabled"), AmethystConfig.CloudBanEnabled);
        string cloudBtnText = AmethystAccess.IsFetchingCloudBans ? "..." : AmethystText.T("GetCloudBanList");
        if (SmallButton(new Rect(b.x + 2f, by, 136f, 24f), cloudBtnText, AmethystStyle.Current.Accent)) AmethystAccess.FetchCloudBans(this);
        GUI.Label(new Rect(b.x + 146f, by, b.width - 146f, 24f), $"{AmethystText.T("CloudBanList")}: <b>{AmethystAccess.CloudBanCount}</b>", _muted);
        by += 32f;

        Grp(0);
        ListCard(x, ref y, w, AmethystText.T("BanList"), AmethystAccess.BanEntries, AmethystAccess.RemoveBan);
        Grp(0);
        // 第 215 轮：云端名单同样每行带 ✕，可手动删除不需要的条目。
        // 删除只改本地 CloudBanList.txt —— 再次「获取云端封禁名单」仍以服务器数据为准（整体覆盖）。
        ListCard(x, ref y, w, AmethystText.T("CloudBanList"), AmethystAccess.CloudBanEntries, AmethystAccess.RemoveCloudBan);
        Grp(0);
        RecentCard(x, ref y, w);

        float lvlBody = 2f * RowH
            + (AmethystConfig.MinLevelEnabled.Value ? 50f + RowH : 0f)
            + (AmethystConfig.MaxLevelEnabled.Value ? 50f + RowH : 0f);
        Grp(1);
        b = Card(x, ref y, w, AmethystText.T("Level"), lvlBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ReactToLowLevel"), AmethystConfig.MinLevelEnabled);
        if (AmethystConfig.MinLevelEnabled.Value)
        {
            SliderInt(b.x, ref by, b.width, AmethystText.T("MinLevel"), AmethystConfig.MinLevel, 1, 500);
            ActionCycle(b.x, ref by, b.width, AmethystText.T("Action"), AmethystConfig.MinLevelAction, KickBanVals, KickBanDisp);
        }
        Toggle(b.x, ref by, b.width, AmethystText.T("ReactToHighLevel"), AmethystConfig.MaxLevelEnabled);
        if (AmethystConfig.MaxLevelEnabled.Value)
        {
            SliderInt(b.x, ref by, b.width, AmethystText.T("MaxLevel"), AmethystConfig.MaxLevel, 1, 999);
            ActionCycle(b.x, ref by, b.width, AmethystText.T("Action"), AmethystConfig.MaxLevelAction, KickBanVals, KickBanDisp);
        }

        Grp(2);
        // ⚠ 第 165 轮：本卡片高度 +50f —— 「洪水阈值/秒」滑块（SliderInt 占 26+24 = 50px）
        //   从下面那张「逐个 RPC 开关」卡片**之外**挪进了这里。
        //   原先它被写在 36 项开关之后，但 `by` 早已越过 `Card(...)` 返回的 body 区域底部，
        //   而 SliderInt 是不做 Cull 的 —— 于是滑块被画到卡片下方、压在其它 UI 上，
        //   就是用户看到的「洪水阈值卡在下面 UI 上、位置错误」。
        // 严格精确计算卡片高度（每行 RowH=24f，外加滑块展开与卡片内部留白）
        // 统计卡片内所有项目：
        // 1. AntiCheatMaster (Toggle) -> 1
        // 2. DropRPCsHost (HostAwareToggle) -> 1
        // 3. ReactToCheater (ActionCycle) -> 1
        // 4. CheaterActionThreshold (SliderInt, 动态展开) -> 50f
        // 5. RPCFloodDetectionNotifyOnly (Toggle) -> 1
        // 6. FloodThresholdSec (SliderInt) -> 50f
        // 7-28. 其它各类独立防护 Toggle -> 22项（含防止+检测两个独立开关）
        // 总计：26 项单行 (Toggle / CycleRow) + 洪水滑块 (50f) + 缓冲留白 (50f)
        // ⚠ 第 219 轮：新增「识别外来模组」一项 ⇒ 25 → 26，注释里的区间同步改。
        int rowItemsCount = 31;
        float hostCardH = rowItemsCount * RowH + 50f + 50f + 3f * 26f;
        if (AmethystConfig.RpcGuardAction.Value == "Kick" || AmethystConfig.RpcGuardAction.Value == "Ban")
            hostCardH += 50f; // 处罚门槛滑块展开(+50f)

        b = Card(x, ref y, w, AmethystText.T("RPCAnticheatHost"), hostCardH);
        by = b.y;
        bool beforeAc = AmethystConfig.AntiCheat.Value;
        Toggle(b.x, ref by, b.width, AmethystText.T("AntiCheatMaster"), AmethystConfig.AntiCheat);
        if (AmethystConfig.AntiCheat.Value != beforeAc) ApplyAntiCheat(AmethystConfig.AntiCheat.Value);
        HostAwareToggle(b.x, ref by, b.width, AmethystText.T("DropRPCsHost"), AmethystConfig.DropRPC);
        ActionCycle(b.x, ref by, b.width, AmethystText.T("ReactToCheater"), AmethystConfig.RpcGuardAction, VoteVals, VoteDisp);
        if (AmethystConfig.RpcGuardAction.Value == "Kick" || AmethystConfig.RpcGuardAction.Value == "Ban")
        {
            SliderInt(b.x, ref by, b.width, AmethystText.T("CheaterActionThreshold"), AmethystConfig.CheaterActionThreshold, 1, 8);
        }
        Toggle(b.x, ref by, b.width, AmethystText.T("RPCFloodDetectionNotifyOnly"), AmethystConfig.RpcFloodDetect);
        // 第 165 轮：洪水阈值紧跟它自己的开关，保证画在本卡片 body 之内（不再溢出到卡片外）。
        SliderInt(b.x, ref by, b.width, AmethystText.T("FloodThresholdSec"), AmethystConfig.RpcFloodThreshold, 25, 500);

        Sub(b.x, ref by, b.width, AmethystText.T("GuardGroupCheck"));
        Toggle(b.x, ref by, b.width, AmethystText.T("DetectModClients"), AmethystConfig.ModClientDetection);
        // 第 219 轮 #3：识别**别人**用的模组（已知 ID 表 + 通用启发式）。
        //   与上面那条不同：上面那条只认「同为 Amethyst 的客户端」，这条认的是各种外来模组。
        Toggle(b.x, ref by, b.width, AmethystText.T("ForeignModDetect"), AmethystConfig.ForeignModDetect);
        Toggle(b.x, ref by, b.width, AmethystText.T("BypassDisconnectPenalty"), AmethystConfig.BypassDisconnectPenalty);
        Toggle(b.x, ref by, b.width, AmethystText.T("DetectHostTempBan"), AmethystConfig.DetectHostTempBan);
        Toggle(b.x, ref by, b.width, AmethystText.T("FastKillGuard"), AmethystConfig.FastKillGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("PlatformSpoofGuard"), AmethystConfig.PlatformSpoofGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("InvalidFcGuard"), AmethystConfig.InvalidFcGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("BotGuard"), AmethystConfig.BotGuard);
        Sub(b.x, ref by, b.width, AmethystText.T("GuardGroupRules"));
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardBlockFakeMeetings"), AmethystConfig.BlockFakeMeetings);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardBlockEarlyMeetings"), AmethystConfig.BlockEarlyMeetings);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardHsSabotage"), AmethystConfig.HsSabotageGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardVentHost"), AmethystConfig.VentHostRules);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardVentClient"), AmethystConfig.VentClientRules);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardSpawnFlood"), AmethystConfig.SpawnFloodGuard);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardVentTp"), AmethystConfig.VentTpProtect);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardZipline"), AmethystConfig.ZiplineProtect);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardSabotageRepair"), AmethystConfig.CheckSabotageRepair);
        Slider(b.x, ref by, b.width, AmethystText.T("GuardSabotageRepairDist"), AmethystConfig.SabotageRepairDistance, 0.5f, 10f, "0.0", 0.1f);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardNoImpostorEnd"), AmethystConfig.NoImpostorGameOver);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardDoorSpam"), AmethystConfig.DoorSpamGuard);
        // 阻止遥测只保留「设置 → 界面」里的一个开关（原来这里与 BlockTelemetryData 重复）。
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardSecurityNotify"), AmethystConfig.SecurityNotify);

        Sub(b.x, ref by, b.width, AmethystText.T("GuardGroupRpc"));
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcPlayer"), AmethystConfig.RpcGuardPlayer);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcVent"), AmethystConfig.RpcGuardVent);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcShip"), AmethystConfig.RpcGuardShip);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcVoteBan"), AmethystConfig.RpcGuardVoteBan);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardRpcGameData"), AmethystConfig.RpcGameDataCheck);
        Toggle(b.x, ref by, b.width, AmethystText.T("GuardUnknownRpc"), AmethystConfig.UnknownRpcGuard);
            // ===== 逐 RPC 开关（36 项，第 158 轮）=====
            Grp(2);
            Grp(3);   // 逐 RPC 开关放到独立页签
        b = Card(x, ref y, w, AmethystText.T("GuardRpcPerCall"), AmethystConfig.RpcCallNames.Length * RowH + 24f);
            by = b.y;
            for (int ri = 0; ri < AmethystConfig.RpcCallNames.Length; ri++)
            {
                string rn = AmethystConfig.RpcCallNames[ri];
                if (AmethystConfig.RpcCallSwitches.TryGetValue(rn, out var rcfg) && rcfg != null)
                    // ⚠ 第 164 轮：这里原来直接把 RPC 名（AddVote / Shapeshift / …）当标签用，
                    //   于是菜单里那 36 行全是英文 key，中文/俄语用户看到的也是一堆英文。
                    //   三份 lang 已补齐这 36 个 key，这里走 T() 取当前语言文案。
                    Toggle(b.x, ref by, b.width, AmethystText.T(rn), rcfg);
            }
        // （第 165 轮：「洪水阈值/秒」滑块原先在这里 —— 位置在 `Card(...)` 返回的 body 之外，
        //   会溢出并压在下方 UI 上；现已上移到 RPC 反作弊卡片里、紧跟在洪水检测开关之后。）

        Grp(2);
        b = Card(x, ref y, w, AmethystText.T("AntiBan"), 2f * RowH + 42f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("AsHostPunishSender"), AmethystConfig.AntiBanHost);
        ActionCycle(b.x, ref by, b.width, AmethystText.T("Action_2"), AmethystConfig.AntiBanHostAction, VoteVals, VoteDisp);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), AmethystText.T("KillsTheCrashBanVentKickPacketOffHostItIsAlwaysO"), _muted);

        Grp(2);
        // 第 241 轮：网络防护（按 Hydra 2.1.0 补齐）
        //   强制 DTLS / 拦截服务器位置更新 / 拦截未授权系统更新 / 防护 ReportDeadBody 崩溃
        b = Card(x, ref y, w, AmethystText.T("NetworkProtection"), 5f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ForceDTLSEncryption"), AmethystConfig.ForceDtls);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockServerTeleports"), AmethystConfig.BlockServerTeleports);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockUnauthorizedUpdates"), AmethystConfig.BlockUnauthorizedUpdates);
        Toggle(b.x, ref by, b.width, AmethystText.T("BypassShapeshiftRateLimit"), AmethystConfig.BypassShapeshiftRateLimit);
        Toggle(b.x, ref by, b.width, AmethystText.T("ReportDeadBodyCrashGuard"), AmethystConfig.ReportDeadBodyCrashGuard);

        Grp(2);
        // 第 241 轮：防过载（总开关 + 四项子功能）
        b = Card(x, ref y, w, AmethystText.T("AntiOverload"), 6f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ProtEnabled"), AmethystConfig.AntiOverloadEnabled);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockLargeGameMessages"), AmethystConfig.BlockLargeGameMessages);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockInvalidGameDataTypes"), AmethystConfig.BlockInvalidGameDataTypes);
        Toggle(b.x, ref by, b.width, AmethystText.T("HardenPackedUInt"), AmethystConfig.HardenPackedUInt);
        Toggle(b.x, ref by, b.width, AmethystText.T("VotingCompleteOverloadGuard"), AmethystConfig.BlockVotingCompleteOverload);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockDataFlood"), AmethystConfig.BlockDataFlood);

        Grp(2);
        // 第 241 轮：防踢（总开关 + 投票踢人 + 仅房主 RPC 踢人）
        b = Card(x, ref y, w, AmethystText.T("AntiKick"), 4f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ProtEnabled"), AmethystConfig.AntiKickEnabled);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockVoteKicks"), AmethystConfig.VoteKickProtect);
        ActionCycle(b.x, ref by, b.width, AmethystText.T("Action_2"), AmethystConfig.VoteKickAction, VoteVals, VoteDisp);
        Toggle(b.x, ref by, b.width, AmethystText.T("HostOnlyRpcKickGuard"), AmethystConfig.HostOnlyRpcKickGuard);

        Grp(1);
        b = Card(x, ref y, w, AmethystText.T("Colors"), RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("KickFortegreen"), AmethystConfig.KickFortegreen);

        Grp(1);
        b = Card(x, ref y, w, AmethystText.T("Moderation"), 2f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("KickBanInMatchHost"), AmethystConfig.UnlockMatchKickBan);
        Toggle(b.x, ref by, b.width, AmethystText.T("KickSlowJoiners"), AmethystConfig.KickSlowJoiners);
        // ⚠ "显示平台及等级"（JoinDetect）已按要求移除：模组不再自己发进/离提示，
        //    玩家正常进/离开完全走原版通知。
    }
    private void DrawDevTab(float x, ref float y, float w)
    {
        Grp(0);
        Rect b = Card(x, ref y, w, AmethystText.T("DevOptions"), 3f * RowH + 20f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("PerfProbe"), AmethystConfig.PerfProbe);
        Toggle(b.x, ref by, b.width, AmethystText.T("NoWinConditions"), AmethystConfig.NoWinConditions);

        // 职业分配算法：点击循环切换 0..3（默认 / System / Xorshift / Mersenne Twister）。
        if (AmethystConfig.RoleAlgorithm != null)
        {
            int cur = Mathf.Clamp(AmethystConfig.RoleAlgorithm.Value, 0, 3);
            CycleRow(b.x, ref by, b.width, AmethystText.T("RoleAlgorithm"), RoleAlgorithmName(cur),
                () => { AmethystConfig.RoleAlgorithm.Value = (cur + 1) % 4; });
        }
    }

    private static string RoleAlgorithmName(int mode)
    {
        return mode switch
        {
            1 => AmethystText.T("RoleAlgoSystem"),
            2 => AmethystText.T("RoleAlgoXorshift"),
            3 => AmethystText.T("RoleAlgoMT"),
            _ => AmethystText.T("RoleAlgoDefault"),
        };
    }

    private void RecentCard(float x, ref float y, float w)
    {
        IReadOnlyList<RecentEntry> recent = AmethystRecent.Entries;
        float body = 30f + (recent.Count > 0 ? recent.Count * 46f : 26f);
        Rect b = Card(x, ref y, w, $"{AmethystText.T("WhoLeft")} ({recent.Count})", body);
        float by = b.y;

        if (SmallButton(new Rect(b.x + b.width - 96f, by, 92f, 24f), AmethystText.T("CLEAR"), new Color(0.9f, 0.36f, 0.36f)))
            AmethystRecent.Clear();
        by += 30f;

        if (recent.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), AmethystText.T("Empty"), _muted);
            return;
        }

        for (int i = 0; i < recent.Count; i++)
        {
            RecentEntry e = recent[i];
            var r = new Rect(b.x, by, b.width, 44f);
            HoverFill(r);
            GUI.Label(new Rect(r.x + 8f, r.y + 1f, r.width - 152f, 22f), e.Title, _rowName);
            GUI.Label(new Rect(r.x + 8f, r.y + 21f, r.width - 152f, 20f), e.Info, _rowInfo);

            if (SmallButton(new Rect(r.xMax - 74f, r.y + 10f, 70f, 24f), AmethystText.T("Ban"), new Color(0.9f, 0.4f, 0.4f)))
                AmethystAccess.AddBan(e.Name, e.Fc, e.Puid);
            by += 46f;
        }
    }

    [HideFromIl2Cpp]
    // ===== 制作人员名单 =====
    // 内容硬编码在 `Core\AmethystCredits.cs`，不读外部文件、也没有配置项。
    private void DrawCredits(float x, ref float y, float w)
    {
        Grp(0);
        Rect head = Card(x, ref y, w, AmethystText.T("Credits"), RowH + 22f);
        GUI.Label(new Rect(head.x + 2f, head.y, head.width - 4f, 24f),
            AmethystText.T("CreditsThanks"), _muted);

        for (int g = 0; g < AmethystCredits.Groups.Length; g++)
        {
            CreditGroup grp = AmethystCredits.Groups[g];
            if (grp == null || grp.Members == null || grp.Members.Length == 0) continue;

            Grp(0);
            Rect b = Card(x, ref y, w, AmethystText.T(grp.TitleKey), grp.Members.Length * 30f + 4f);
            float by = b.y;
            for (int i = 0; i < grp.Members.Length; i++)
            {
                string member = grp.Members[i];
                if (string.IsNullOrEmpty(member)) continue;

                var r = new Rect(b.x, by, b.width, 28f);
                HoverFill(r);
                AmethystStyle.FillRounded(new Rect(r.x + 6f, r.y + 8f, 3f, 12f), AmethystStyle.Current.Accent, 1);
                GUI.Label(new Rect(r.x + 18f, r.y, r.width - 24f, r.height), member, _rowName);
                by += 30f;
            }
        }
    }

    [HideFromIl2Cpp]
    private void ListCard(float x, ref float y, float w, string title, IReadOnlyList<AccessEntry> entries, Action<string> onRemove)
    {
        float body = entries.Count > 0 ? entries.Count * 30f : 26f;
        Rect b = Card(x, ref y, w, $"{title} ({entries.Count})", body);
        float by = b.y;
        if (entries.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), AmethystText.T("Empty"), _muted);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AccessEntry e = entries[i];
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            string name = string.IsNullOrEmpty(e.Name) ? (string.IsNullOrEmpty(e.Code) ? AmethystText.T("Guest") : e.Code) : e.Name;
            float nameW = onRemove != null ? r.width - 50f : r.width - 12f;
            string subText = EntrySub(e);
            string rowText = string.IsNullOrEmpty(subText)
                ? $"<b>{name}</b>"
                : $"<b>{name}</b>  <color=#8A94AC><size=11>{subText}</size></color>";
            LabelFade(new Rect(r.x + 8f, r.y, nameW, r.height), rowText, _rowName);
            if (onRemove != null && SmallButton(new Rect(r.xMax - 36f, r.y + 3f, 32f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f)))
            {
                onRemove(string.IsNullOrEmpty(e.Code) ? e.Puid : e.Code);
                break;
            }
            by += 30f;
        }
    }

    private static string EntrySub(AccessEntry e)
    {
        string s = string.Empty;
        if (!string.IsNullOrEmpty(e.Code)) s = e.Code;
        if (!string.IsNullOrEmpty(e.Puid))
        {
            string pu = e.Puid.Length > 8 ? e.Puid.Substring(0, 8) + "…" : e.Puid;
            s = s.Length > 0 ? s + " · " + pu : pu;
        }
        return s;
    }

    private bool SmallButton(Rect r, string label, Color col)
    {
        if (Cull(r.y, r.height)) return false;
        bool hover = r.Contains(M);
        int id = RectId(r);
        Pressed(r, id);
        float k = PressK(id);

        Matrix4x4 m = GUI.matrix;
        if (k < 1f) GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
        AmethystStyle.FillRounded(r, hover ? A(col, 0.4f) : A(col, 0.2f), 7);
        AmethystStyle.Fill(new Rect(r.x + 4f, r.y + 2f, r.width - 8f, 1f), A(Color.white, 0.14f));
        _smallBtn.normal.textColor = Color.Lerp(col, Color.white, 0.55f);
        GUI.Label(r, label, _smallBtn);
        GUI.matrix = m;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    [HideFromIl2Cpp]
    private void ActionCycle(float x, ref float y, float w, string label, ConfigEntry<string> entry, string[] vals, string[] disp)
    {
        int i = Mathf.Max(0, Array.IndexOf(vals, entry.Value));
        CycleRow(x, ref y, w, label, disp[i], () => { entry.Value = vals[(i + 1) % vals.Length]; });
    }

    // 颜色循环：显示颜色名（本地化），而不是序号
    [HideFromIl2Cpp]
    private void ColorCycle(float x, ref float y, float w, string label, ConfigEntry<int> entry)
    {
        int max = 17;
        try { if (Palette.PlayerColors != null) max = Palette.PlayerColors.Length - 1; } catch { }
        int cur = Mathf.Clamp(entry.Value, 0, Mathf.Max(0, max));
        CycleRow(x, ref y, w, label, ColorName(cur),
            () => { entry.Value = (cur + 1) % (max + 1); },
            () => { entry.Value = (cur - 1 + max + 1) % (max + 1); });
    }

    private static string ColorName(int id)
    {
        try
        {
            string n = Palette.GetColorName(id);
            if (!string.IsNullOrEmpty(n)) return char.ToUpperInvariant(n[0]) + (n.Length > 1 ? n.Substring(1) : string.Empty);
        }
        catch { }
        return "#" + id;
    }

    private static InnerNetClient GuardNet()
    {
        try { return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance; }
        catch { return null; }
    }

    private static string ClientInfo(ClientData c)
    {
        string s = string.Empty;
        try
        {
            string lvl = Patches.AmethystJoinLevels.Display(c);
            if (lvl != "?") s = AmethystText.IsChinese ? lvl + "级" : AmethystText.T("Lvl") + lvl;
        }
        catch { }
        try
        {
            if (c.PlatformData != null)
            {
                string pl = Plat(c.PlatformData.Platform);
                if (pl.Length > 0) s = s.Length > 0 ? s + " · " + pl : pl;
            }
        }
        catch { }
        return s;
    }

    private static string Plat(Platforms p)
    {
        return (int)p switch
        {
            1 => "Epic",
            2 => "Steam",
            3 => "Mac",
            4 => "MS",
            5 => "Itch",
            6 => "iOS",
            7 => "Android",
            8 => "Switch",
            9 => "Xbox",
            10 => "PS",
            _ => string.Empty,
        };
    }

    private string _homeAbout, _homeImp;
    private float _homeAboutH, _homeImpH, _homeTextW = -1f;

    private void DrawHome(float x, ref float y, float w)
    {
        HomeAbout(x, ref y, w);
        HomeMoreInfo(x, ref y, w);
        HomeNotices(x, ref y, w);
    }

    private void HomeAbout(float x, ref float y, float w)
    {
        string about = AmethystText.T("BAmethystBAnAmongUsAnticheat");
        MeasureHome(about, w);
        Grp(0);
        // 「关于」只放**模组自己的**信息：版本 / 作者
        Rect b = Card(x, ref y, w, AmethystText.T("About"), _homeAboutH + 2f * 30f + 8f);
        GUI.Label(new Rect(b.x, b.y, b.width, _homeAboutH), about, _rowLabel);
        float by = b.y + _homeAboutH + 8f;
        InfoRow(b.x, ref by, b.width, AmethystText.T("Version"), "v" + AmethystPlugin.PluginVersion);
        InfoRow(b.x, ref by, b.width, AmethystText.T("Author"), "一只屑小紫");
    }

    // 运行环境信息**单独一张卡**。
    // （之前把它们塞进了「关于」卡里 —— 那卡是讲模组自身的，游戏版本 / 系统放里面确实不合适。）
    private void HomeMoreInfo(float x, ref float y, float w)
    {
        Grp(0);
        Rect b = Card(x, ref y, w, AmethystText.T("MoreInfo"), 4f * 30f + 8f);
        float by = b.y;
        InfoRow(b.x, ref by, b.width, AmethystText.T("System"), AmethystSysInfo.Os());
        InfoRow(b.x, ref by, b.width, AmethystText.T("GameVersion"), AmethystSysInfo.Game());
        InfoRow(b.x, ref by, b.width, "BepInEx", AmethystSysInfo.BepInExVersion());
        InfoRow(b.x, ref by, b.width, AmethystText.T("ModCount"), AmethystSysInfo.ModCount());
    }

    // 重要提示 + 链接（原本挂在「关于」方法尾部，现在独立成一个方法）
    private void HomeNotices(float x, ref float y, float w)
    {
        string imp = AmethystText.T("ColorFFD166TheModMayConflictWithOtherModsDisable");
        Grp(0);
        Rect b = Card(x, ref y, w, AmethystText.T("Important"), _homeImpH);
        LabelFade(b, imp, _rowLabel);

        Grp(0);
        b = Card(x, ref y, w, AmethystText.T("Links"), 3f * RowH);
        float by = b.y;
        LinkRow(b.x, ref by, b.width, AmethystText.T("QQGroup"), "1031096791");
        LinkRow(b.x, ref by, b.width, "GitHub", "https://github.com/CN-SPPLAYER/Amethyst");
        string keyName = AmethystConfig.MenuKey.Value == KeyCode.None ? "—" : AmethystConfig.MenuKey.Value.ToString();
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), AmethystText.T("MenuOpenMenuKeyIsInSettingsKeys", keyName), _muted);
    }

    private void MeasureHome(string about, float w)
    {
        string imp = AmethystText.T("ColorFFD166TheModMayConflictWithOtherModsDisable");
        if (Mathf.Abs(w - _homeTextW) < 0.5f && ReferenceEquals(about, _homeAbout) && ReferenceEquals(imp, _homeImp)) return;
        _homeTextW = w; _homeAbout = about; _homeImp = imp;

        // ⚠ 第 205 轮（修「重要提示」那几行字叠在一起）：
        //   两列模式下卡片只有**半宽**（`_cardColW`），而原来这里一直按**满宽** `w - 36`
        //   算 `CalcHeight`。宽度小 ⇒ 折行数变多 ⇒ 实际需要的高度比算出来的大，
        //   于是文字溢出卡片底部、压到下一张卡上（用户报的"字重在一起"）。
        //
        //   所以按"这张卡**实际会用的宽度**"来算：
        //     · 两列且不跨整行 → `_cardColW - 36`
        //     · 单列或跨整行   → `w - 36`
        //   跨整行与否取决于高度本身，是个循环依赖；这里先按窄宽算，
        //   只有当它高到会跨整行时才按满宽重算（这两个短文本实际上永远不跨）。
        float narrow = (_cardCols > 1 ? _cardColW : w) - 36f;
        _homeAboutH = _rowLabel.CalcHeight(new GUIContent(about), narrow);
        _homeImpH = _rowLabel.CalcHeight(new GUIContent(imp), narrow);

        if (Mathf.Max(_homeAboutH, _homeImpH) >= FullSpanBodyH)
        {
            float wide = w - 36f;
            _homeAboutH = _rowLabel.CalcHeight(new GUIContent(about), wide);
            _homeImpH = _rowLabel.CalcHeight(new GUIContent(imp), wide);
        }
    }

    private void LinkRow(float x, ref float y, float w, string label, string url)
    {
        AmethystPalette p = AmethystStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        bool hover = r.Contains(M);
        AmethystStyle.FillRounded(r, hover ? A(p.Accent, 0.18f) : A(Color.white, 0.04f), 8);
        AmethystStyle.StrokeRounded(r, hover ? A(p.Accent, 0.5f) : A(Color.white, 0.08f), 8, 1);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width - 48f, r.height), label, _rowLabelLine);
        GUI.Label(new Rect(r.x + w - 40f, r.y, 34f, r.height), "↗", _value);
        if (!string.IsNullOrEmpty(url) && GUI.Button(r, GUIContent.none, _invisible))
            OpenLink(url);
        y += RowH;
    }

    // ===== 其他功能：子标签（第 221 轮）=====
    // 原来这一页把所有卡片堆在同一屏（第 206 轮只做过卡内分组），太长也不好找。
    // 现在参考 OM 的整理方式拆成 5 个子标签（复用 Guard / Settings 已有的 SubBar 机制，
    // 子标签的滑动高亮、切换时"从下往上"入场都自动生效）：
    //   冷却与延迟 / 大厅与房间 / 显示与角色 / 界面与外观 / 性能
    // 开关本身一个没动，只是按主题分页。
    private static readonly string[] OtherSubs = { "OtherSubPing", "OtherSubLobby", "OtherSubDisplay", "OtherSubAppearance", "OtherSubPerf" };

    private void DrawOther(float x, ref float y, float w)
    {
        Grp(-1);
        int sub = SubBar(x, ref y, w, OtherSubs);
        switch (sub)
        {
            case 0: DrawOtherPing(x, ref y, w); break;
            case 1: DrawOtherLobby(x, ref y, w); break;
            case 2: DrawOtherDisplay(x, ref y, w); break;
            case 3: DrawOtherAppearance(x, ref y, w); break;
            default: DrawOtherPerf(x, ref y, w); break;
        }
    }

    // ----- 冷却与延迟 -----
    private void DrawOtherPing(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, AmethystText.T("OtherCooldown"), 4f * RowH + 4f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("BetterPingDisplay"), AmethystConfig.BetterPingDisplay);
        Toggle(b.x, ref by, b.width, AmethystText.T("BetterCooldownDisplay"), AmethystConfig.BetterCooldownDisplay);
        Toggle(b.x, ref by, b.width, AmethystText.T("CooldownDecimals"), AmethystConfig.CooldownDecimals);
        Toggle(b.x, ref by, b.width, AmethystText.T("SabotageCooldownDisplay"), AmethystConfig.SabotageCooldownDisplay);

        // 更好的延迟显示：仅在开启后出现，逐项选择要显示的内容。
        if (AmethystConfig.BetterPingDisplay != null && AmethystConfig.BetterPingDisplay.Value)
        {
            Rect pb = Card(x, ref y, w, AmethystText.T("BetterPingItems"), 5f * RowH + 4f);
            float pby = pb.y;
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingLatency"), AmethystConfig.PingShowLatency);
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingFps"), AmethystConfig.PingShowFps);
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingServer"), AmethystConfig.PingShowServer);
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingRoom"), AmethystConfig.PingShowRoom);
            Toggle(pb.x, ref pby, pb.width, AmethystText.T("PingHost"), AmethystConfig.PingShowHost);
        }
    }

    // ----- 大厅与房间 -----
    private void DrawOtherLobby(float x, ref float y, float w)
    {
        // ⚠ 第 206 轮：`RoomSortByMaxPlayers` 以前有配置项但**菜单里没有开关**，这里补上。
        Rect b = Card(x, ref y, w, AmethystText.T("OtherLobby"), 8f * RowH + 4f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ExpandLobbySearch"), AmethystConfig.ExpandLobbySearch);
        Toggle(b.x, ref by, b.width, AmethystText.T("RoomSortByMaxPlayersLabel"), AmethystConfig.RoomSortByMaxPlayers);
        Toggle(b.x, ref by, b.width, AmethystText.T("RoomInfoDisplay"), AmethystConfig.RoomInfoDisplay);
        Toggle(b.x, ref by, b.width, AmethystText.T("LobbyPlayerInfo"), AmethystConfig.LobbyPlayerInfo);
        Toggle(b.x, ref by, b.width, AmethystText.T("FixLobbyMusic"), AmethystConfig.FixLobbyMusic);
        Toggle(b.x, ref by, b.width, AmethystText.T("MuteLobbyMusic"), AmethystConfig.MuteLobbyMusic);
        Toggle(b.x, ref by, b.width, AmethystText.T("MusicMuteGameMusic"), AmethystConfig.MusicMuteGameMusic);
        Toggle(b.x, ref by, b.width, AmethystText.T("ScaleLobbyPane"), AmethystConfig.ScaleLobbyPane);

        // 抢占颜色（仅大厅）
        b = Card(x, ref y, w, AmethystText.T("ColorSnipe"), 2f * RowH + 8f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ColorSnipe"), AmethystConfig.ColorSnipe);
        ColorCycle(b.x, ref by, b.width, AmethystText.T("ColorSnipeId"), AmethystConfig.ColorSnipeId);
    }

    // ----- 显示与角色 -----
    private void DrawOtherDisplay(float x, ref float y, float w)
    {
        // ===== 颜色与可读性 =====
        Rect b = Card(x, ref y, w, AmethystText.T("OtherColors"), 4f * RowH + 4f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("ColorBlindText"), AmethystConfig.ColorBlindText);
        Toggle(b.x, ref by, b.width, AmethystText.T("NameColors"), AmethystConfig.NameColors);
        // ⚠ 第 172 轮：职业颜色（为原版每个职业渲染独立颜色，参考 EndlessHostRoles）
        Toggle(b.x, ref by, b.width, AmethystText.T("RoleColors"), AmethystConfig.RoleColors);
        Toggle(b.x, ref by, b.width, AmethystText.T("HoverThemeColor"), AmethystConfig.HoverThemeColor);

        // ===== 职业信息 =====
        b = Card(x, ref y, w, AmethystText.T("OtherRoleInfo"), 8f * RowH + 4f);
        by = b.y;
        // ⚠ 第 176 轮：会议中显示任务面板（参考 AUnlocker）
        Toggle(b.x, ref by, b.width, AmethystText.T("MeetingTaskPanel"), AmethystConfig.MeetingTaskPanel);
        Toggle(b.x, ref by, b.width, AmethystText.T("ShowMeetingHost"), AmethystConfig.ShowMeetingHost);
        Toggle(b.x, ref by, b.width, AmethystText.T("ShowSelfRole"), AmethystConfig.ShowSelfRole);
        Toggle(b.x, ref by, b.width, AmethystText.T("ShowImpostorTeammateRole"), AmethystConfig.ShowImpostorTeammateRole);
        Toggle(b.x, ref by, b.width, AmethystText.T("ShowRolesWhenDead"), AmethystConfig.ShowRolesWhenDead);
        // ⚠ 第 171 轮：职业信息面板开关（对局内多一个可折叠的职业页签）
        Toggle(b.x, ref by, b.width, AmethystText.T("RolePanel"), AmethystConfig.RolePanel);
        Toggle(b.x, ref by, b.width, AmethystText.T("RoleTag"), AmethystConfig.RoleTag);
        Toggle(b.x, ref by, b.width, AmethystText.T("WinnerRoleReveal"), AmethystConfig.WinnerRoleReveal);

        // 视觉：跳过击杀动画**不属于界面优化**，单独归类
        b = Card(x, ref y, w, AmethystText.T("Visual"), RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("SkipKillAnimation"), AmethystConfig.SkipKillAnimation);
    }

    // ----- 界面与外观 -----
    private void DrawOtherAppearance(float x, ref float y, float w)
    {
        // ===== 主菜单与外观 =====
        // ⚠ 「强制显示开始按钮」开关已移除：该功能改为**硬编码强制生效**
        //   （位置与显示逻辑见 Patches/OtherFeaturePatches.cs 的 ApplyGameStartBlockLayout），
        //   不再由配置控制，所以菜单里不该再留一个点了没用的开关。
        Rect b = Card(x, ref y, w, AmethystText.T("OtherMainMenu"), 7f * RowH + 4f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("UiTheme"), AmethystConfig.UiTheme);
        Toggle(b.x, ref by, b.width, AmethystText.T("MainMenuOptimize"), AmethystConfig.MainMenuOptimize);
        Toggle(b.x, ref by, b.width, AmethystText.T("MainMenuButtons"), AmethystConfig.MainMenuButtons);
        Toggle(b.x, ref by, b.width, AmethystText.T("MainMenuArt"), AmethystConfig.MainMenuArt);
        Toggle(b.x, ref by, b.width, AmethystText.T("SnowEffect"), AmethystConfig.SnowEffect);
        Toggle(b.x, ref by, b.width, AmethystText.T("OutfitSaver"), AmethystConfig.OutfitSaver);
        Toggle(b.x, ref by, b.width, AmethystText.T("RecapInfo"), AmethystConfig.RecapInfo);

        // 显示与身份（不随界面优化联动，各自独立开关）
        bool fpsOn = AmethystConfig.UnlockFps240 != null && AmethystConfig.UnlockFps240.Value;
        // 开启帧率解锁时多一行滑块（SliderInt 占 50px），卡片高度要跟着变；
        // 「技能 UI 缩放」也是滑块，同样占 50px。
        b = Card(x, ref y, w, AmethystText.T("DisplayRoles"), 3f * RowH + 20f + (fpsOn ? 50f : 0f) + 50f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("UnlockFps"), AmethystConfig.UnlockFps240);
        if (AmethystConfig.UnlockFps240 != null && AmethystConfig.UnlockFps240.Value)
            SliderInt(b.x, ref by, b.width, AmethystText.T("FpsLimit"), AmethystConfig.FpsLimit, 60, 240);
        Toggle(b.x, ref by, b.width, AmethystText.T("AutoReturnToLobby"), AmethystConfig.AutoReturnLobby);
        Toggle(b.x, ref by, b.width, AmethystText.T("UnlockAllCosmetics"), AmethystConfig.UnlockAllCosmetics);
        // 技能 / 使用 / 击杀 / 报告等动作按钮的缩放（0.1 ~ 1.5，默认 0.85）
        Slider(b.x, ref by, b.width, AmethystText.T("AbilityUiScale"), AmethystConfig.AbilityUiScale, 0.1f, 1.5f, "0.00", 0.01f);

        // 聊天（暗黑模式 / 聊天优化保留在此）
        b = Card(x, ref y, w, AmethystText.T("VisualChat"), 4f * RowH + 20f);
        by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("Keep80ChatMessages"), AmethystConfig.ChatHistory80);
        Toggle(b.x, ref by, b.width, AmethystText.T("ForceShowChatOverlay"), AmethystConfig.VisualAlwaysShowChat);
        Toggle(b.x, ref by, b.width, AmethystText.T("BetterChatRicherInputCopyPaste"), AmethystConfig.BetterChat);
        Toggle(b.x, ref by, b.width, AmethystText.T("DarkChatThemeMatchingCurrentAccent"), AmethystConfig.DarkChatTheme);
    }

    // ----- 性能 -----
    private void DrawOtherPerf(float x, ref float y, float w)
    {
        // ⚠ 第 173 轮：性能分区（参考 EndlessHostRoles 的 SystemSettings，把影响帧数的开关集中到一处）。
        //   「不更新死亡玩家」在开启时多一行跳帧数滑块，卡片高度要跟着变。
        bool deadOn = AmethystConfig.PerfDontUpdateDead != null && AmethystConfig.PerfDontUpdateDead.Value;
        Rect b = Card(x, ref y, w, AmethystText.T("PerfSection"), 4f * RowH + 20f + (deadOn ? 50f : 0f));
        float by = b.y;
        Toggle(b.x, ref by, b.width, AmethystText.T("PerfDontUpdateDead"), AmethystConfig.PerfDontUpdateDead);
        if (deadOn)
            SliderInt(b.x, ref by, b.width, AmethystText.T("PerfDeadSkipFrames"), AmethystConfig.PerfDeadSkipFrames, 2, 300);
        Toggle(b.x, ref by, b.width, AmethystText.T("PerfLowLoad"), AmethystConfig.PerfLowLoad);
        Toggle(b.x, ref by, b.width, AmethystText.T("PerfHideConsole"), AmethystConfig.PerfHideConsole);
    }

    // 一键反作弊：整合除 丢弃RPC / RPC洪水 / 防管道踢人漏洞 之外的反作弊功能
    private static void ApplyAntiCheat(bool on)
    {
        AmethystConfig.RpcGuard.Value = on;
        AmethystConfig.VentTpProtect.Value = on;
        AmethystConfig.ZiplineProtect.Value = on;
        AmethystConfig.VentHostRules.Value = on;
        AmethystConfig.VentClientRules.Value = on;
        AmethystConfig.BlockEarlyMeetings.Value = on;
        AmethystConfig.HsSabotageGuard.Value = on;
        AmethystConfig.BlockFakeMeetings.Value = on;
        AmethystConfig.SpawnFloodGuard.Value = on;
    }

    // 设置子标签：仅保留 界面 与 按键。原 显示 / 其他 已删除，其内容（通知、屏幕菜单按钮、阻止遥测）并入 界面。
    private static readonly string[] SettingsSubs = { "SettingsSubInterface", "SettingsSubKeys" };
    private static readonly int[] SettingsCounts = { 0, 0 };

    private void DrawSettings(float x, ref float y, float w)
    {
        int sub = SubBar(x, ref y, w, SettingsSubs);
        SettingsCounts[0] = 0;
        SettingsCounts[1] = 0;
        switch (sub)
        {
            case 1: KeySettings(x, ref y, w); break;
            default: InterfaceSettings(x, ref y, w); break;
        }
    }

    private void InterfaceSettings(float x, ref float y, float w)
    {
        Grp(0);
        // 界面 + 原 显示/其他 子标签的内容（通知、屏幕菜单按钮、阻止遥测）合并于此。
        // 主题选择器已移除（主题强制为 #A06EFF），所以这里不再需要主题网格的高度。
        Rect b = Card(x, ref y, w, AmethystText.T("Interface"),
            RowH + 50f + 3f * RowH + 24f);
        float by = b.y;
        CycleRow(b.x, ref by, b.width, AmethystText.T("Language"), AmethystText.LangName, () => AmethystText.Toggle());
        Slider(b.x, ref by, b.width, AmethystText.T("MenuScale"), AmethystConfig.MenuScale, 0.7f, 1.8f, "0.00", 0.01f);
        Toggle(b.x, ref by, b.width, AmethystText.T("Toasts"), AmethystConfig.Toasts);
        Toggle(b.x, ref by, b.width, AmethystText.T("OnScreenMenuButton"), AmethystConfig.MenuButton);
        Toggle(b.x, ref by, b.width, AmethystText.T("BlockTelemetryData"), AmethystConfig.BlockTelemetry);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), AmethystText.T("AppliedOnGameStart"), _muted);
        SettingsCounts[0] = 1;
    }

    private void KeySettings(float x, ref float y, float w)
    {
        Grp(1);
        Rect b = Card(x, ref y, w, AmethystText.T("Hotkeys"), 9f * RowH + 22f);
        float by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 4f, 22f), AmethystText.T("ChangeTheTopLeftMenuOpenKeyHere"), _muted);
        by += 20f;
        KeyRow(b.x, ref by, b.width, AmethystText.T("OpenMenuKey"), AmethystConfig.MenuKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("LobbyCode"), AmethystConfig.CopyCodeKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("RecapShowKey"), AmethystConfig.RecapKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("MusicKey"), AmethystConfig.MusicKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("MusicPrevKey"), AmethystConfig.MusicPrevKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("MusicNextKey"), AmethystConfig.MusicNextKey);
        KeyRow(b.x, ref by, b.width, AmethystText.T("MusicPlayPauseKey"), AmethystConfig.MusicPlayPauseKey);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), AmethystText.T("ClickToSetTrashRMBToClearEscToCancel"), _muted);
        SettingsCounts[1] = 1;
    }

    private void DrawEmpty(Rect area, AmethystIcon icon, string msg)
    {
        AmethystPalette p = AmethystStyle.Current;
        float s = 66f;
        var ic = new Rect(area.center.x - s / 2f, area.y + area.height * 0.30f, s, s);
        AmethystStyle.FillRounded(new Rect(ic.x - 12f, ic.y - 12f, s + 24f, s + 24f), A(p.Accent, 0.06f), 22);
        AmethystIcons.Draw(icon, ic, A(p.Muted, 0.55f));
        GUI.Label(new Rect(area.x, ic.yMax + 16f, area.width, 24f), msg, _centerMuted);
    }

    [HideFromIl2Cpp]
    private void ActionRow(Rect body, AmethystIcon icon, string label, string btn, Action onClick)
    {
        AmethystPalette p = AmethystStyle.Current;
        var row = new Rect(body.x, body.y, body.width, 44f);
        var sq = new Rect(row.x, row.y + 2f, 40f, 40f);
        AmethystStyle.FillRounded(sq, A(p.Accent, 0.14f), 10);
        AmethystIcons.Draw(icon, new Rect(sq.x + 10f, sq.y + 10f, 20f, 20f), p.Accent);
        GUI.Label(new Rect(sq.xMax + 14f, row.y, row.width - 170f, 44f), label, _rowLabel);

        var bt = new Rect(row.xMax - 112f, row.y + 5f, 112f, 34f);
        bool hover = bt.Contains(M);
        AmethystStyle.FillRounded(bt, p.Accent, 9);
        AmethystStyle.FillRounded(new Rect(bt.x + 1.5f, bt.y + 1.5f, bt.width - 3f, bt.height - 3f), hover ? A(p.Accent, 0.30f) : p.Panel, 8);
        AmethystStyle.Fill(new Rect(bt.x + 8f, bt.y + 4f, bt.width - 16f, 1f), A(Color.white, 0.10f));
        GUI.Label(bt, btn, _btnLabel);
        if (GUI.Button(bt, GUIContent.none, _invisible)) onClick();
    }

    private void InfoRow(float x, ref float y, float w, string label, string value)
    {
        GUI.Label(new Rect(x + 2f, y, w * 0.5f, 28f), label, _muted);
        GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f - 2f, 28f), value, _value);
        y += 30f;
    }

    [HideFromIl2Cpp]
    private void Toggle(float x, ref float y, float w, string label, ConfigEntry<bool> entry)
    {
        if (entry == null) { y += RowH; return; }
        if (Cull(y, RowH)) { y += RowH; return; }
        var r = new Rect(x, y, w, RowH - 4f);
        bool forcedOn = AmethystPlatform.IsAndroid &&
                        string.Equals(entry.Definition.Key, "MenuButton", StringComparison.Ordinal);
        if (forcedOn && !entry.Value) entry.Value = true;

        if (!forcedOn) HoverFill(r);
        if (!forcedOn && GUI.Button(r, GUIContent.none, _invisible))
        {
            entry.Value = !entry.Value;
            
            // 实时联动：如果点击的是“关闭大厅音乐” (MuteLobbyMusic)，在 UI 点击的瞬间立即执行开播或停播，不需要等切换场景！
            if (string.Equals(entry.Definition.Key, "MuteLobbyMusic", StringComparison.Ordinal))
            {
                try
                {
                    LobbyBehaviour lobby = LobbyBehaviour.Instance;
                    if (lobby != null)
                    {
                        if (entry.Value) Patches.AmethystLobbyMusicFix.MuteNow(lobby);
                        else Patches.AmethystLobbyMusicFix.UnmuteNow(lobby);
                    }
                }
                catch { }
            }
        }

        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height),
            label, forcedOn ? _muted : _rowLabelLine);
        DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f),
            forcedOn ? true : entry.Value, entry, forcedOn);
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void HostAwareToggle(float x, ref float y, float w, string label, ConfigEntry<bool> entry)
    {
        if (entry == null) { y += RowH; return; }
        if (Cull(y, RowH)) { y += RowH; return; }
        var r = new Rect(x, y, w, RowH - 4f);
        bool host = false;
        try { host = AmongUsClient.Instance != null && ((InnerNetClient)AmongUsClient.Instance).AmHost; } catch { }
        if (!host)
        {
            if (entry.Value)
            {
                AmethystRpcGuard.SetDropRpcEnabled(false);
                entry.Value = false;
            }
            GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height), label, _muted);
            DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f), false, entry);
            y += RowH;
            return;
        }

        HoverFill(r);
        if (host)
        {
            if (GUI.Button(r, GUIContent.none, _invisible))
                AmethystRpcGuard.SetDropRpcEnabled(!entry.Value, true);
        }
        else
        {
            AmethystStyle.StrokeRounded(r, new Color(1f, 1f, 1f, 0.08f), 8, 1);
        }

        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height), label, _rowLabelLine);
        DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f), entry.Value, entry);
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void Slider(float x, ref float y, float w, string label, ConfigEntry<float> entry, float min, float max, string fmt, float step = 0f)
    {
        if (entry == null) { y += RowH; return; }
        if (Cull(y, 50f)) { y += 50f; return; }
        AmethystPalette p = AmethystStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabelLine);
        bool deferred = ReferenceEquals(entry, AmethystConfig.MenuScale);
        if (deferred && !_menuScaleDragging && !_menuScaleDraftActive) _menuScaleDraft = entry.Value;
        var labelRect = new Rect(x + w - 58f, y, 54f, 22f);
        y += 26f;

        // step > 0 时在滑条两端各加一个「-」「+」按钮，用来逐格细调
        Rect minus = default, plus = default, track;
        if (step > 0f)
        {
            const float arrowW = 24f;
            minus = new Rect(x + 8f, y - 3f, arrowW, 20f);
            plus = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
            track = new Rect(minus.xMax + 8f, y + 2f, plus.x - minus.xMax - 16f, 8f);
        }
        else
        {
            track = new Rect(x + 10f, y + 2f, w - 20f, 8f);
        }

        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x - 8f, track.y - 10f, track.width + 16f, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
        {
            _slider = id;
            if (deferred) { _menuScaleDraft = entry.Value; _menuScaleDraftActive = true; _menuScaleDragging = true; }
            e.Use();
        }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                float next = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                if (deferred) _menuScaleDraft = next;
                else entry.Value = next;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                if (deferred && _menuScaleDraftActive)
                {
                    entry.Value = Mathf.Clamp(_menuScaleDraft, min, max);
                    _menuScaleDraftActive = false;
                }
                if (deferred) _menuScaleDragging = false;
                _slider = 0;
                e.Use();
            }
        }

        if (deferred && _menuScaleDragging && !Input.GetMouseButton(0))
        {
            entry.Value = Mathf.Clamp(_menuScaleDraft, min, max);
            _menuScaleDraftActive = false;
            _menuScaleDragging = false;
            _slider = 0;
        }

        if (step > 0f)
        {
            float cur = deferred && _menuScaleDraftActive ? _menuScaleDraft : entry.Value;
            if (StepBtn(minus, "-", p)) CommitSlider(entry, deferred, Mathf.Clamp(cur - step, min, max));
            if (StepBtn(plus, "+", p)) CommitSlider(entry, deferred, Mathf.Clamp(cur + step, min, max));
        }

        float shown = deferred && _menuScaleDraftActive ? _menuScaleDraft : entry.Value;
        GUI.Label(labelRect, shown.ToString(fmt), _value);

        float t = Mathf.InverseLerp(min, max, shown);
        DrawSliderTrack(track, t);
        y += 24f;
    }

    // 细调按钮点击后立即提交；MenuScale 走 draft 的话要顺手把 draft 状态清掉
    private void CommitSlider(ConfigEntry<float> entry, bool deferred, float v)
    {
        if (deferred)
        {
            _menuScaleDraft = v;
            _menuScaleDraftActive = false;
            _menuScaleDragging = false;
            _slider = 0;
        }
        entry.Value = v;
    }

    private bool StepBtn(Rect r, string label, AmethystPalette p)
    {
        bool hov = r.Contains(M);
        AmethystStyle.FillRounded(r, hov ? p.ButtonHover : p.Button, 6);
        AmethystStyle.StrokeRounded(r, A(Color.white, hov ? 0.10f : 0.06f), 6, 1);

        Color prev = _arrow.normal.textColor;
        _arrow.normal.textColor = hov ? p.Text : A(p.Text, 0.72f);
        GUI.Label(r, label, _arrow);
        _arrow.normal.textColor = prev;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

[HideFromIl2Cpp]
    private void DrawSliderTrack(Rect track, float t)
    {
        AmethystPalette p = AmethystStyle.Current;
        t = Mathf.Clamp01(t);
        float kx = track.x + track.width * t;

        AmethystStyle.FillRounded(track, p.Button, 4);
        AmethystStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(6f, track.width * t), track.height), p.Accent, 4);

        float ky = track.y + track.height / 2f;
        AmethystStyle.FillRounded(new Rect(kx - 8f, ky - 7f, 16f, 16f), A(Color.black, 0.28f), 8);
        AmethystStyle.FillRounded(new Rect(kx - 7f, ky - 8f, 14f, 14f), Color.white, 7);
    }

    [HideFromIl2Cpp]
    private void SliderInt(float x, ref float y, float w, string label, ConfigEntry<int> entry, int min, int max)
    {
        if (entry == null) { y += RowH; return; }
        AmethystPalette p = AmethystStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabelLine);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value.ToString(), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "-")) entry.Value = Mathf.Clamp(entry.Value - 1, min, max);
        if (ArrowButton(rightBtn, "+")) entry.Value = Mathf.Clamp(entry.Value + 1, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width))), min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        DrawSliderTrack(track, t);
        y += 24f;
    }

    [HideFromIl2Cpp]
    private void FpsSlider(float x, ref float y, float w, string label, ConfigEntry<int> entry)
    {
        if (entry == null) { y += RowH; return; }
        const int min = 30, max = 300, step = 5;
        AmethystPalette p = AmethystStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabelLine);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value >= max ? "∞" : entry.Value.ToString(), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "-")) entry.Value = Mathf.Clamp(entry.Value - step, min, max);
        if (ArrowButton(rightBtn, "+")) entry.Value = Mathf.Clamp(entry.Value + step, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                int raw = Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width)));
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(raw / (float)step) * step, min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        DrawSliderTrack(track, t);
        y += 24f;
    }

    private bool ArrowButton(Rect r, string glyph)
        => StepBtn(r, glyph, AmethystStyle.Current);

    // 主题色板（ThemeCols / ThemeGridHeight / ThemeSwatches）与 SwW/SwH/SwGap 已随
    // 「移除主题选项」一并删除 —— 主题现在固定为 #A06EFF。

[HideFromIl2Cpp]
    private void CycleRow(float x, ref float y, float w, string label, string valueText, Action onClick, Action onLeft = null)
    {
        if (Cull(y, RowH)) { y += RowH; return; }
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.45f, r.height), label, _rowLabelLine);

        if (onLeft != null)
        {
            // 左右轮调：在右侧提供 [ ◂  值  ▸ ] 的经典精美布局
            const float aw = 22f;    // 缩短箭头按钮宽度到 22px，更加精致
            
            // 🚀 智能自适应改进：动态测量文本在屏幕上的实际像素宽度，消除多余大片空白
            float maxValW = r.width * 0.45f; // 限制值最大占整行宽度的 45%
            float valW = 40f;
            try
            {
                if (_value != null)
                {
                    valW = Mathf.Clamp(_valueC.CalcSize(new GUIContent(valueText)).x + 16f, 36f, maxValW);
                }
            }
            catch { }

            var rb = new Rect(r.xMax - aw - 4f, r.y + 2f, aw, r.height - 4f); // 右按钮靠最右
            var lb = new Rect(rb.x - valW - aw, rb.y, aw, rb.height);         // 左按钮根据实际文本宽度 valW 弹性靠左
            
            if (ArrowButton(lb, "◂")) onLeft();
            if (ArrowButton(rb, "▸")) onClick();
            
            // 颜色名/数值完美且精致地贴合在 [ ◂ ] 和 [ ▸ ] 两个按钮正中间！
            GUI.Label(new Rect(lb.xMax, r.y, valW, r.height), valueText, _valueC);
        }
        else
        {
            if (GUI.Button(r, GUIContent.none, _invisible)) onClick();
            GUI.Label(new Rect(r.x + r.width - 168f, r.y, 164f, r.height), valueText + "   ▸", _value);
        }
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void KeyRow(float x, ref float y, float w, string label, ConfigEntry<KeyCode> entry)
    {
        if (entry == null) { y += RowH; return; }
        if (Cull(y, RowH)) { y += RowH; return; }
        AmethystPalette p = AmethystStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.5f, r.height), label, _rowLabelLine);

        bool listening = _rebinding && ReferenceEquals(_rebindTarget, entry);
        bool isMenuKey = ReferenceEquals(entry, AmethystConfig.MenuKey);
        float bw = listening ? 132f : 96f;
        var badge = new Rect(r.xMax - bw - 4f, r.y + (r.height - 24f) / 2f, bw, 24f);
        AmethystStyle.FillRounded(badge, listening ? A(p.Accent, 0.85f) : A(Color.white, 0.06f), 7);
        AmethystStyle.StrokeRounded(badge, listening ? A(p.Accent, 0.9f) : A(Color.white, 0.10f), 7, 1);
        _keyBadge.normal.textColor = listening ? Color.white : p.Text;
        string txt = listening ? AmethystText.T("PressAKey") : (entry.Value == KeyCode.None ? "—" : entry.Value.ToString());
        GUI.Label(badge, txt, _keyBadge);

        Event e = Event.current;

        if (!listening && !isMenuKey)
        {
            var del = new Rect(badge.x - 26f, badge.y, 22f, 24f);
            bool dh = e != null && del.Contains(e.mousePosition);
            AmethystStyle.FillRounded(del, dh ? A(new Color(0.9f, 0.3f, 0.3f), 0.30f) : A(Color.white, 0.06f), 6);
            AmethystIcons.Draw(AmethystIcon.Trash, new Rect(del.x + 5f, del.y + 5f, 14f, 14f), dh ? new Color(0.95f, 0.5f, 0.5f) : A(p.Muted, 0.9f));
            if (GUI.Button(del, GUIContent.none, _invisible))
            {
                entry.Value = KeyCode.None;
                _rebinding = false; _rebindTarget = null; Rebinding = false;
            }
        }

        bool rightClear = !isMenuKey && e != null && e.type == EventType.MouseDown && e.button == 1 && badge.Contains(e.mousePosition);
        bool clicked = GUI.Button(badge, GUIContent.none, _invisible);
        if (rightClear)
        {
            entry.Value = KeyCode.None;
            _rebinding = false; _rebindTarget = null; Rebinding = false;
            e.Use();
        }
        else if (clicked)
        {
            if (listening) { _rebinding = false; _rebindTarget = null; }
            else { _rebinding = true; _rebindTarget = entry; }
            Rebinding = _rebinding;
        }
        y += RowH;
    }

    private void HandleRebindCapture()
    {
        Rebinding = _rebinding;
        if (!_rebinding || _rebindTarget == null) return;
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        KeyCode kc = e.keyCode;
        if (kc == KeyCode.Escape) { _rebinding = false; _rebindTarget = null; }
        else if (kc != KeyCode.None) { _rebindTarget.Value = kc; _rebinding = false; _rebindTarget = null; }
        Rebinding = _rebinding;
        e.Use();
    }

    [HideFromIl2Cpp]
    private void DrawPill(Rect track, bool on, object key, bool disabled = false)
    {
        AmethystPalette p = AmethystStyle.Current;
        int r = Mathf.RoundToInt(track.height / 2f);

        if (disabled)
        {
            AmethystStyle.FillRounded(track, A(p.Button, 0.40f), r);
            AmethystStyle.StrokeRounded(track, A(Color.white, 0.03f), r, 1);
            float dk = track.height - 6f;
            int dkr = Mathf.RoundToInt(dk / 2f);
            AmethystStyle.FillRounded(new Rect(track.xMax - dk - 3f, track.y + 3f, dk, dk),
                A(p.Muted, 0.40f), dkr);
            return;
        }

        float target = on ? 1f : 0f;
        float a = target, v = 0f;
        if (key != null)
        {
            if (!_pillAnim.TryGetValue(key, out a)) a = target;
            _pillVel.TryGetValue(key, out v);
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                v += (-300f * (a - target) - 24f * v) * dt;
                a += v * dt;
                _pillAnim[key] = a;
                _pillVel[key] = v;
            }
        }
        float e = Mathf.Clamp01(a);
        float kpos = Mathf.Clamp(a, -0.12f, 1.12f);

        if (e > 0.01f)
            AmethystStyle.FillRounded(track, Color.Lerp(p.Button, p.Accent, e), r);
        else
            AmethystStyle.FillRounded(track, p.Button, r);
        if (e < 0.99f)
            AmethystStyle.StrokeRounded(track, A(Color.white, 0.06f * (1f - e)), r, 1);

        float knob = track.height - 6f;
        float kx = Mathf.Lerp(track.x + 3f, track.xMax - knob - 3f, kpos);
        int kr = Mathf.RoundToInt(knob / 2f);
        AmethystStyle.FillRounded(new Rect(kx, track.y + 4f, knob, knob), A(Color.black, 0.22f), kr);
        AmethystStyle.FillRounded(new Rect(kx, track.y + 3f, knob, knob), Color.Lerp(p.Muted, Color.white, e), kr);
    }

    private static float Frac(float v) => v - Mathf.Floor(v);

    private void DrawStars(float w, float h)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        float t = Time.unscaledTime;
        for (int i = 0; i < 26; i++)
        {
            float fx = Frac(i * 0.61803399f + 0.11f);
            float fy = Frac(i * 0.75487767f + 0.29f);
            float drift = Frac(fy + 0.02f * t * (0.4f + fx));
            float sz = 1.3f + 1.7f * fy;
            float tw = 0.5f + 0.5f * Mathf.Sin(t * (0.7f + fx) + i);
            Color col = (i % 5 == 0) ? AmethystStyle.Current.Accent : Color.white;
            AmethystStyle.FillRounded(new Rect(fx * w, drift * h, sz, sz), A(col, 0.05f + 0.06f * tw), 1);
        }
    }

    private void HoverFill(Rect r)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        if (!r.Contains(Event.current.mousePosition)) return;
        AmethystPalette p = AmethystStyle.Current;
        AmethystStyle.FillRounded(r, A(p.Accent, 0.06f), 8);
        AmethystStyle.FillRounded(new Rect(r.x, r.y, r.width * 0.42f, r.height), A(p.Accent, 0.05f), 8);
        AmethystStyle.FillRounded(new Rect(r.x + 1f, r.y + 4f, 3f, r.height - 8f), p.Accent, 2);
    }

    private string CustomText(Rect r, string text, string key)
    {
        AmethystPalette p = AmethystStyle.Current;
        bool focused = _textFocus == key;
        AmethystStyle.FillRounded(r, p.Button, 7);
        AmethystStyle.StrokeRounded(r, focused ? A(p.Accent, 0.8f) : A(Color.white, 0.08f), 7, 1);

        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown)
            _textFocus = r.Contains(e.mousePosition) ? key : (_textFocus == key ? null : _textFocus);

        if (focused && e != null && e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Backspace) { if (text.Length > 0) text = text.Substring(0, text.Length - 1); e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { _textFocus = null; e.Use(); }
            else if (e.character != '\0' && !char.IsControl(e.character) && text.Length < 24) { text += e.character; e.Use(); }
        }

        bool caret = focused && Time.unscaledTime % 1f < 0.5f;
        GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), text + (caret ? "|" : ""), _rowLabel);
        return text;
    }

    private static void OpenLink(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try { GUIUtility.systemCopyBuffer = url; } catch { }
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try { Application.OpenURL(url); } catch { }
        }
        AmethystToast.Push(AmethystText.T("Link"), AmethystText.T("CopiedToClipboard"), 3.0f, AmethystNotifyKind.Info);
    }

    private static string KeyName(ConfigEntry<KeyCode> key) => key != null ? key.Value.ToString() : "?";
    private static string KeyDisp(ConfigEntry<KeyCode> key) => key == null || key.Value == KeyCode.None ? "—" : key.Value.ToString();
    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static Rect LerpRect(Rect a, Rect b, float t) =>
        new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));

    private static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }

    // ===== 第 284 轮：首次打开前的同步预热 =====
    // 分帧预热保证不了「打开前完成」——日志里 prewarmed=False 连续出现就是证据。
    // 改为主界面第一次 OnGUI 一次性做完；虽然会把开销放在主界面，
    // 但用户打开菜单时一定已经就绪。
    private void PrewarmIfNeeded()
    {
        if (_prewarmed) return;
        if (!AmethystMenuButton.AtMainMenu()) return;

        _prewarmed = true;
        try { Build(); } catch { _built = false; }
        try { PrewarmFonts(); } catch { }
        try { AmethystStyle.PrewarmTextures(); } catch { }
        try { AmethystPlugin.Logger?.LogInfo("[Menu] prewarm done"); } catch { }
    }

    private void PrewarmFonts()
    {
        string all = AmethystText.AllTextForPrewarm();
        if (string.IsNullOrEmpty(all)) return;

        var seen = new HashSet<string>();
        WarmUnique(_brand, all, seen); WarmUnique(_verPill, all, seen); WarmUnique(_cardTitle, all, seen);
        WarmUnique(_rowLabel, all, seen); WarmUnique(_rowLabelLine, all, seen); WarmUnique(_value, all, seen);
        WarmUnique(_valueC, all, seen); WarmUnique(_muted, all, seen); WarmUnique(_footer, all, seen);
        WarmUnique(_btnLabel, all, seen); WarmUnique(_centerMuted, all, seen); WarmUnique(_arrow, all, seen);
        WarmUnique(_subTabStyle, all, seen); WarmUnique(_smallBtn, all, seen); WarmUnique(_rowName, all, seen);
        WarmUnique(_rowInfo, all, seen); WarmUnique(_tabStyle, all, seen); WarmUnique(_sectionTitle, all, seen);
        WarmUnique(_keyBadge, all, seen); WarmUnique(_star, all, seen);
    }

    private static void WarmUnique(GUIStyle style, string text, HashSet<string> seen)
    {
        if (style == null || string.IsNullOrEmpty(text)) return;
        try
        {
            Font font = style.font != null ? style.font : GUI.skin.font;
            if (font == null) return;

            string key = font.GetInstanceID() + "|" + style.fontSize + "|" + (int)style.fontStyle;
            if (!seen.Add(key)) return;
            font.RequestCharactersInTexture(text, style.fontSize, style.fontStyle);
        }
        catch { }
    }

    private void Build()
    {
        var currentFont = AmethystFont.GetFont();
        // 允许热更新字体重新构建样式
        if (_built && currentFont == _lastBuiltFont) return;
        _built = true;
        _lastBuiltFont = currentFont;
        AmethystPalette p = AmethystStyle.Current;

        _windowBg = new GUIStyle();
        _invisible = new GUIStyle();

        _brand = Label(p.Text, Mathf.RoundToInt(25 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _verPill = Label(p.Accent, Mathf.RoundToInt(12 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _cardTitle = Label(p.Text, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
        _rowLabel = Label(p.Text, Mathf.RoundToInt(15 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowLabel.wordWrap = true;
        _rowLabelLine = Label(p.Text, Mathf.RoundToInt(15 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowLabelLine.wordWrap = false;
        _rowLabelLine.clipping = TextClipping.Clip;
        _value = Label(p.Accent, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleRight);
        _valueC = Label(p.Accent, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _muted = Label(p.Muted, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _footer = Label(p.Muted, Mathf.RoundToInt(12 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _btnLabel = Label(p.Accent, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _centerMuted = Label(A(p.Muted, 0.8f), Mathf.RoundToInt(14 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter, customFont: false); // 几何折叠符号 ▸ ▾ 走原字体
        _arrow = Label(p.Accent, Mathf.RoundToInt(15 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter, customFont: false); // 箭头 ◂ ▸ 走原字体
        _subTabStyle = Label(p.Muted, Mathf.RoundToInt(12 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _smallBtn = Label(Color.white, Mathf.RoundToInt(11 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter, customFont: false); // 删除等符号 ✕ 走原字体
        _rowName = Label(p.Text, Mathf.RoundToInt(14 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowName.wordWrap = false;
        _rowName.clipping = TextClipping.Clip;
        _rowInfo = Label(p.Muted, Mathf.RoundToInt(13 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowInfo.wordWrap = false;
        _rowInfo.clipping = TextClipping.Clip;
        _tabStyle = Label(p.Muted, Mathf.RoundToInt(14 * 1.2f), FontStyle.Normal, TextAnchor.MiddleLeft);
        // 第 248 轮：主区分区标题（内容区顶部那条 20pt 粗体），配合交叉淡入 + 上浮落位
        _sectionTitle = Label(p.Text, Mathf.RoundToInt(20 * 1.2f), FontStyle.Bold, TextAnchor.MiddleLeft);
        _keyBadge = Label(p.Text, Mathf.RoundToInt(12 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter);
        _star = Label(p.Accent, Mathf.RoundToInt(17 * 1.2f), FontStyle.Normal, TextAnchor.MiddleCenter, customFont: false);
    }

    // 富文本 <color=#RRGGBB> 不带头 alpha，Unity 会按不透明渲染：
    // 关闭菜单时这些彩色文字不会跟随 GUI.color 一起淡出，而是瞬间消失。
    // 绘制时把当前 GUI.color.a 注入颜色标签，并临时把 GUI.color.a 置 1，
    // 让最终 alpha 只由标签控制，与其他文字同步淡出。
    private static readonly Regex ColorTagRx = new Regex("<color=#([0-9A-Fa-f]{6})>", RegexOptions.Compiled);

    private void LabelFade(Rect r, string text, GUIStyle style)
    {
        if (string.IsNullOrEmpty(text)) return;

        float a = GUI.color.a;
        if (a >= 0.999f)
        {
            GUI.Label(r, text, style);
            return;
        }

        bool any = false;
        string faded = ColorTagRx.Replace(text, m =>
        {
            any = true;
            return "<color=#" + m.Groups[1].Value + AlphaHex(a) + ">";
        });

        if (!any)
        {
            GUI.Label(r, text, style);
            return;
        }

        GUI.Label(r, faded, style);
    }

    private static string AlphaHex(float a)
    {
        int v = Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
        return v.ToString("X2");
    }

    private static GUIStyle Label(Color color, int size, FontStyle style, TextAnchor anchor, bool customFont = true)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = style,
            alignment = anchor,
            richText = true
        };
        if (customFont)
        {
            AmethystFont.ApplyTo(s);
        }
        else
        {
            // 明确剥离自定义字体，强制用系统原生默认字体
            s.font = null;
        }
        s.normal.textColor = color;
        s.hover.textColor = color;
        s.active.textColor = color;
        s.focused.textColor = color;
        s.onNormal.textColor = color;
        s.onHover.textColor = color;
        s.onActive.textColor = color;
        s.onFocused.textColor = color;
        s.padding = AmethystStyle.Offset(0, 0, 0, 0);
        return s;
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
}
