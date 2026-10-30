using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Amethyst;

// 复盘信息面板：可开关的 IMGUI 面板，显示本局每名玩家的战绩。
//
// ⚠ 第 225 轮（按用户要求改版）：**不再是可拖动窗口**。
//   改为 EHR 那种表现：点「显示复盘信息」→ 整屏淡黑色底板淡入、面板**从屏幕左侧滑到停靠位**、
//   面板内的信息**逐条从左往右出现**；点「隐藏复盘信息」→ 原路滑回去、底板淡出。
//   参考 `EndlessHostRoles-8.0.2\Patches\OutroPatch.cs:677` 的 `SlideAndFadeIn(rect, text, delay)`
//   （每行从左侧向右归位 + 淡入，行间错开 0.15s）。
//
// 绘制风格与 AmethystEventLog 保持一致（同一套 AmethystStyle）。
public sealed class AmethystRecapWindow : MonoBehaviour
{
    private const float HeadH = 30f;
    private const float RowH = 20f;

    // ===== 第 225 轮：改成 EHR 那种「从左侧滑入 + 淡黑底板」，并且**不再可拖动** =====
    //   EHR 的原始做法（`EndlessHostRoles-8.0.2\Patches\OutroPatch.cs:677` 的 `SlideAndFadeIn`）：
    //   每一行从左边滑到位、同时淡入，行与行错开 0.15s。这里整块面板从左滑入，
    //   面板内的信息再**逐条从左往右出现**；点「隐藏复盘信息」就原路滑回去。
    private const float AnimDuration = 0.42f;   // 面板滑入/滑出总时长（秒）
    private const float PanelW = 470f;          // 面板宽
    private const float PanelH = 620f;          // 面板高上限（实际取 min(屏高-2*MarginY, 此值)）
    private const float MarginX = 26f;          // 停靠后距屏幕左边
    private const float MarginY = 84f;          // 上下留白
    private const float BackdropAlpha = 0.55f;  // 淡黑色底板的不透明度
    private const float RowSlide = 92f;         // 每行自己的"从左往右"滑入距离
    private const float RowStagger = 0.030f;    // 每行延迟（占动画进度的比例）
    private const float RowReveal = 0.22f;      // 单行展开所需进度

    // 每帧按动画现算（不再由用户拖动）
    private Rect _win = new Rect(MarginX, MarginY, PanelW, PanelH);
    private float _anim;        // 0 = 完全收起，1 = 完全展开
    private float _scroll;
    private bool _built;
    private GUIStyle _title, _row, _muted;

    private List<string> _cache = new List<string>();
    private float _cacheAt = -1f;

    // 默认收起，由左上角按钮或 F2 切换，大厅与结算共用同一状态。
    private static bool _visible;

    internal static void ToggleVisible() => _visible = !_visible;

    // 当前窗口是否处于打开状态（供外部判断要不要复位）。
    internal static bool IsOpen => _visible;

    // 关闭（供"离开大厅 / 进入对局"时复位使用）。
    internal static void Hide() => _visible = false;

    // 回到大厅时复位成关闭状态。
    //
    // 为什么要复位：如果玩家在上一局结算后让它开着、直接点了再来一局，
    // 那么**下一次**进大厅时它又会是开着的 —— 那就等于"自动打开"了。
    // 在"离开大厅"这个时机清掉，可以保证每次进大厅都是关闭的初始态。
    internal static void ResetOnLobbyLeft() => _visible = false;

    private void Build()
    {
        if (_built) return;
        _built = true;
        _title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * 1.2f), fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        _row = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * 1.2f), fontStyle = FontStyle.Normal, richText = true, wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
        _muted = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * 1.2f), fontStyle = FontStyle.Normal, richText = true, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        AmethystFont.ApplyTo(_title);
        AmethystFont.ApplyTo(_row);
        AmethystFont.ApplyTo(_muted);
    }

    [HideFromIl2Cpp]
    internal void DrawGui()
    {
        int __p = AmethystPerfProbe.Begin(Section.S_Recap);
        try { DrawGuiInner(); }
        finally { AmethystPerfProbe.End(__p, Section.S_Recap); }
    }

    private void DrawGuiInner()
    {
        if (!AmethystRecap.CanUse) { Hide(); return; }

        Build();

        AmethystPalette p = AmethystStyle.Current;
        float sc = Mathf.Clamp(Screen.height / 1080f, 1f, 2.4f);
        Matrix4x4 mtx = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
        try
        {
            // ---- 动画推进：_visible 为真滑入、为假滑回（点「隐藏」就是原路返回）----
            float dt = Time.unscaledDeltaTime;
            _anim = Mathf.MoveTowards(_anim, _visible ? 1f : 0f, dt / AnimDuration);
            float ease = Mathf.SmoothStep(0f, 1f, _anim);

            float sw = Screen.width / sc, sh = Screen.height / sc;
            Event e = Event.current;

            // ---- 淡黑色底板：整屏，随动画淡入淡出 ----
            if (ease > 0.0001f)
                AmethystStyle.Fill(new Rect(0f, 0f, sw, sh), A(Color.black, BackdropAlpha * ease));

            // ⚠ 第 221 轮：默认改用**游戏原生精灵按钮**（EHR `OutroPatch.cs:708` 同款，见 `AmethystRecapButton`）。
            //   只有当原生按钮没能建立时（拿不到模板按钮 / 当前场景不可用）才画这个 IMGUI 兜底版 ——
            //   保证玩家永远不会因为克隆失败而彻底没有这个按钮。
            //   ⚠ 第 225 轮：必须画在**底板之上**，否则面板一打开就被底板盖住、点不到了。
            if (!AmethystRecapButton.IsActive)
            {
                Rect toggle = new Rect(16f, 16f, 190f, 32f);
                AmethystStyle.FillRounded(toggle, A(p.Window, 0.95f), 8);
                AmethystStyle.StrokeRounded(toggle, A(p.Accent, Hover(toggle) ? 0.9f : 0.5f), 8, 1);
                _title.normal.textColor = p.Text;
                GUI.Label(new Rect(toggle.x + 12f, toggle.y + 6f, toggle.width - 24f, 20f),
                    AmethystText.T(_visible ? "RecapHide" : "RecapShow"), _title);
                if (GUI.Button(toggle, GUIContent.none, GUIStyle.none)) ToggleVisible();
            }

            if (ease <= 0.0001f) return;   // 完全收起：连面板都不画

            // ---- 面板：从屏幕左侧外面滑到停靠位（位置固定，不再可拖动）----
            float panelH = Mathf.Min(sh - MarginY * 2f, PanelH);
            float panelY = (sh - panelH) * 0.5f;
            float panelX = -PanelW + (MarginX + PanelW) * ease;
            _win = new Rect(panelX, panelY, PanelW, panelH);

            float w = _win.width, h = _win.height;

            AmethystStyle.FillRounded(_win, A(p.Window, 0.95f), 12);
            AmethystStyle.StrokeRounded(_win, A(p.Accent, 0.28f), 12, 1);
            AmethystStyle.Fill(new Rect(_win.x + 10f, _win.y + HeadH - 1f, w - 20f, 1f), A(p.Accent, 0.35f));

            AmethystStyle.FillRounded(new Rect(_win.x + 12f, _win.y + 9f, 3f, 12f), p.Accent, 1);
            _title.normal.textColor = p.Text;
            GUI.Label(new Rect(_win.x + 20f, _win.y + 5f, 200f, 20f), AmethystText.T("RecapInfo"), _title);

            // 复制 / 关闭
            Rect cpy = new Rect(_win.x + w - 80f, _win.y + 6f, 22f, 18f);
            Rect cls = new Rect(_win.x + w - 28f, _win.y + 6f, 22f, 18f);
            if (Hover(cpy)) AmethystStyle.FillRounded(cpy, A(p.Accent, 0.20f), 6);
            if (Hover(cls)) AmethystStyle.FillRounded(cls, A(new Color(0.9f, 0.3f, 0.3f), 0.25f), 6);
            Icon(cpy, AmethystIcon.Copy, Hover(cpy) ? p.Text : p.Muted);
            Icon(cls, AmethystIcon.Close, Hover(cls) ? new Color(0.95f, 0.5f, 0.5f) : p.Muted);
            if (GUI.Button(cpy, GUIContent.none, GUIStyle.none)) CopyAll();
            if (GUI.Button(cls, GUIContent.none, GUIStyle.none)) { _visible = false; return; }

            // 结果行：显示哪个阵营获胜 + 具体原因（如「伪装者全部断线」）
            float top = _win.y + HeadH + 6f;
            string result = AmethystRecap.ResultText();
            if (!string.IsNullOrEmpty(result))
            {
                _muted.normal.textColor = A(p.Text, 0.95f);
                GUI.Label(new Rect(_win.x + 12f, top, w - 24f, 18f),
                    $"{AmethystText.T("RecapResult")}: {result}", _muted);
                top += 20f;
            }

            Rect body = new Rect(_win.x + 8f, top, w - 16f, h - (top - _win.y) - 10f);
            List<string> rows = CachedRows();

            if (rows.Count == 0)
            {
                _muted.normal.textColor = A(p.Muted, 0.85f);
                GUI.Label(new Rect(body.x + 4f, body.y + 4f, body.width - 8f, 20f), AmethystText.T("RecapNoData"), _muted);
            }
            else
            {
                float total = rows.Count * RowH;
                float maxScroll = Mathf.Max(0f, total - body.height);

                if (e != null && e.type == EventType.ScrollWheel && body.Contains(e.mousePosition))
                {
                    _scroll += e.delta.y * 18f;
                    e.Use();
                }
                _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);

                GUI.BeginGroup(body);
                Event ge = Event.current;
                float y = -_scroll;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (y + RowH >= 0f && y <= body.height)
                    {
                        // ★ 第 225 轮：每行**逐条从左往右出现**（对应 EHR 的 SlideAndFadeIn + 0.15s 错开）。
                        //   进度 rt 由面板动画进度 ease 推出来：第 i 行比前一行晚 RowStagger。
                        float rt = Mathf.Clamp01((ease - i * RowStagger) / RowReveal);
                        if (rt > 0f)
                        {
                            Rect rr = new Rect(0f, y, body.width, RowH);
                            bool hov = ge != null && rr.Contains(ge.mousePosition);
                            if (hov) AmethystStyle.Fill(rr, A(p.Accent, 0.10f * rt));
                            _row.normal.textColor = A(p.Text, rt);
                            // 没到位之前位于最终位置**左侧** RowSlide 处，随 rt 归零 = 从左往右滑到位
                            // （EHR 的 SlideAndFadeIn 也是"起于左侧、向右归位"；方向别搞反）
                            GUI.Label(new Rect(6f - (1f - rt) * RowSlide, y, body.width - 12f, RowH), rows[i], _row);
                        }
                    }
                    y += RowH;
                }
                GUI.EndGroup();

                if (maxScroll > 1f && total > 0f)
                {
                    float th = Mathf.Max(24f, body.height * (body.height / total));
                    float ty = body.y + (body.height - th) * (_scroll / maxScroll);
                    AmethystStyle.FillRounded(new Rect(body.xMax - 3f, ty, 3f, th), A(p.Accent, 0.5f), 1);
                }
            }
        }
        finally { GUI.matrix = mtx; }
    
    }

    // 行内容每 0.25s 重算一次，避免每帧拼接富文本
    [HideFromIl2Cpp]
    private List<string> CachedRows()
    {
        float now = Time.unscaledTime;
        if (now - _cacheAt < 0.25f) return _cache;
        _cacheAt = now;
        try { _cache = AmethystRecap.Rows(); } catch { }
        return _cache;
    }

    private static void CopyAll()
    {
        try
        {
            string text = AmethystRecap.PlainText();
            if (string.IsNullOrEmpty(text)) return;
            GUIUtility.systemCopyBuffer = text;
            AmethystToast.Push(AmethystText.T("Copied"), null, 3.0f, AmethystNotifyKind.Success);
        }
        catch { }
    }

    private static void Icon(Rect r, AmethystIcon icon, Color c)
        => AmethystIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), c);

    private static bool Hover(Rect r) => Event.current != null && r.Contains(Event.current.mousePosition);

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
