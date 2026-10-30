using System.Collections.Generic;
using UnityEngine;

namespace Amethyst.Patches;

// ===== 缩放技能 / 动作按钮（HUD 右下角那一排）=====
//
// 需求：菜单里用滑块拖动调整，范围 0.1 ~ 1.5，默认 0.85，功能默认开启。
//
// 参考实现：MiraAPI 0.5.0 —— Patches/HudManagerPatches.cs 的 `ResizeUI(float scaleFactor)`
//   （参考模组\MiraAPI-0.5.0\MiraAPI\Patches\HudManagerPatches.cs:46-119）
//
// 它做三件事：
//   ① `HudManager/Buttons` 下所有 `AspectPosition`：`DistanceFromEdge *= scaleFactor`
//      —— 让按钮离屏幕边缘的间距一起缩，否则按钮变小但仍顶在原来的角落位置；
//      跳过 `TopRight`（地图 / 破坏 / 设置那些右上角图标，不属于这排技能键）。
//   ② `HudManager` 下所有 `ActionButton`（使用 / 击杀 / 报告 / 技能 / 通风 / 破坏）：
//      `transform.localScale *= scaleFactor`。
//   ③ `Buttons` 下的 `GridArrange`：`CellSize` 一起缩，并 `ArrangeChilds()` 重排，
//      否则按钮变小了但格子间距还是旧的，会挤在一起或散开。
//
// ⚠ 与原实现的两处关键差异（别照抄那两个 `*=`）：
//
//   ① 原实现只在设置**变更时**调用一次，所以直接 `*=` 没问题。
//      我们这边是**滑块连续拖动**，同一个值会被反复应用 —— 若沿用 `*=`，
//      每次都在上次结果上再乘一次，数值会指数级跑飞（拖一下就缩到看不见）。
//      → 改为**幂等**：登记每个对象的原始值（origScale / origEdge / origCell），
//        之后每次都写 `orig * factor` 绝对值，调用多少次结果都一样。
//
//   ② 原实现是一次性遍历。但角色专属技能键是在**对局开始后**才出现的，
//      一次性抓取会漏掉它们，导致「普通按钮缩了、技能键没缩」。
//      而如果简单地在后来「重抓一遍」，此时对象**已经被缩过**，
//      把当前值当原始值会越缩越小（第二次抓取就把 0.7 当成 1.0 了）。
//      → 改为**增量登记**：用 InstanceID 去重，只登记还没见过的对象
//        （新出现的对象必然是未被我们碰过的原始状态），已登记的永不重抓。
//
// 性能：增量扫描 0.5s 一次（HUD 层级很小，开销可忽略）；
//      缩放值本身只在**变化时**才写一次，稳态下每帧只有一次浮点比较。
internal static class AmethystAbilityButtonScale
{
    private const float MinScale = 0.1f;
    private const float MaxScale = 1.5f;
    private const float DefaultScale = 0.85f;

    /// <summary>增量扫描间隔（秒）。用于发现对局开始后才出现的技能键。</summary>
    private const float RescanInterval = 0.5f;

    private static HudManager _hud;
    private static Transform _buttonsRoot;
    private static float _nextScan;
    private static float _applied = float.NaN;

    private static readonly List<Transform> ButtonTs = new List<Transform>();
    private static readonly List<Vector3> ButtonOrigScale = new List<Vector3>();
    private static readonly HashSet<int> ButtonIds = new HashSet<int>();

    private static readonly List<AspectPosition> Aspects = new List<AspectPosition>();
    private static readonly List<Vector3> AspectOrigEdge = new List<Vector3>();
    private static readonly HashSet<int> AspectIds = new HashSet<int>();

    private static readonly List<GridArrange> Grids = new List<GridArrange>();
    private static readonly List<Vector2> GridOrigCell = new List<Vector2>();
    private static readonly HashSet<int> GridIds = new HashSet<int>();

    /// <summary>当前配置的缩放值（已做范围钳制）。配置缺失时回落到默认 0.85。</summary>
    internal static float CurrentScale
    {
        get
        {
            try
            {
                var e = AmethystConfig.AbilityUiScale;
                if (e != null) return Mathf.Clamp(e.Value, MinScale, MaxScale);
            }
            catch { }
            return DefaultScale;
        }
    }

    /// <summary>每帧由 AmethystHud 调用。</summary>
    internal static void Tick()
    {
        HudManager hud = HudManager.Instance;
        if (hud == null) return;

        // HudManager 换实例（重进对局 / 回主菜单再进）：旧引用全失效，清空重新登记。
        if (_hud != hud)
        {
            _hud = hud;
            Reset();
            _nextScan = 0f;
        }

        float now = Time.unscaledTime;
        if (now >= _nextScan)
        {
            _nextScan = now + RescanInterval;
            Scan(hud);
        }

        float want = CurrentScale;
        if (!float.IsNaN(_applied) && Mathf.Abs(want - _applied) < 0.0005f) return;

        _applied = want;
        Apply(want);
    }

    private static void Reset()
    {
        ButtonTs.Clear(); ButtonOrigScale.Clear(); ButtonIds.Clear();
        Aspects.Clear(); AspectOrigEdge.Clear(); AspectIds.Clear();
        Grids.Clear(); GridOrigCell.Clear(); GridIds.Clear();
        _buttonsRoot = null;
        _applied = float.NaN;
    }

    /// <summary>判断对象是否位于 Buttons/TopRight 分支下（地图 / 破坏 / 设置等右上角图标）。</summary>
    private static bool IsUnderTopRight(Transform t)
    {
        try
        {
            for (int depth = 0; depth < 8 && t != null; depth++)
            {
                if (t.name == "TopRight") return true;
                if (_buttonsRoot != null && t == _buttonsRoot) break;
                t = t.parent;
            }
        }
        catch { }
        return false;
    }

    /// <summary>增量登记：只处理「还没见过」的对象，并顺手剔除已销毁的。</summary>
    private static void Scan(HudManager hud)
    {
        try
        {
            if (_buttonsRoot == null) _buttonsRoot = hud.transform.Find("Buttons");

            int added = 0;

            // ① 动作按钮（使用 / 击杀 / 报告 / 技能 / 通风 …）
            //    ⚠ 排除 TopRight 分支：地图 / 破坏 / 设置那几个是右上角图标，
            //      不属于这排技能键，跟着缩会让右上角整块变小。
            foreach (ActionButton btn in hud.GetComponentsInChildren<ActionButton>(true))
            {
                if (btn == null) continue;
                Transform t = btn.transform;
                if (t == null) continue;
                if (IsUnderTopRight(t)) continue;

                int id = t.GetInstanceID();
                if (!ButtonIds.Add(id)) continue;      // 已登记过，跳过（绝不重抓原始值）

                ButtonTs.Add(t);
                ButtonOrigScale.Add(t.localScale);
                added++;
            }

            if (_buttonsRoot != null)
            {
                // ② 边距：跳过 TopRight（地图 / 破坏 / 设置等右上角图标，不在这排里）
                foreach (AspectPosition ap in _buttonsRoot.GetComponentsInChildren<AspectPosition>(true))
                {
                    if (ap == null) continue;
                    Transform parent = ap.transform.parent;
                    if (parent != null && parent.name == "TopRight") continue;
                    if (ap.gameObject.name.Contains("TopRight")) continue;

                    int id = ap.GetInstanceID();
                    if (!AspectIds.Add(id)) continue;

                    Aspects.Add(ap);
                    AspectOrigEdge.Add(ap.DistanceFromEdge);
                    added++;
                }

                // ③ 格子尺寸：按钮按 GridArrange 排，格子不跟着缩会挤/散
                foreach (GridArrange g in _buttonsRoot.GetComponentsInChildren<GridArrange>(true))
                {
                    if (g == null) continue;
                    if (g.gameObject.name.Contains("TopRight")) continue;

                    int id = g.GetInstanceID();
                    if (!GridIds.Add(id)) continue;

                    Grids.Add(g);
                    GridOrigCell.Add(g.CellSize);
                    added++;
                }
            }

            if (added > 0)
            {
                _applied = float.NaN;   // 有新对象 → 强制重写一次
                AmethystPlugin.Logger?.LogInfo(
                    $"[AbilityScale] +{added} new | total buttons={ButtonTs.Count}"
                    + $" aspects={Aspects.Count} grids={Grids.Count}"
                    + $" root={(_buttonsRoot != null ? _buttonsRoot.name : "null")}");
            }
        }
        catch { }
    }

    /// <summary>把缩放值写下去。全部按「原始值 × 倍数」写绝对值，重复调用结果一致。</summary>
    private static void Apply(float factor)
    {
        try
        {
            for (int i = 0; i < ButtonTs.Count; i++)
            {
                Transform t = ButtonTs[i];
                if (t == null) continue;   // 已销毁：留着无害，下次 Reset 会清
                t.localScale = ButtonOrigScale[i] * factor;
            }

            for (int i = 0; i < Aspects.Count; i++)
            {
                AspectPosition ap = Aspects[i];
                if (ap == null) continue;
                ap.DistanceFromEdge = AspectOrigEdge[i] * factor;
                try { ap.AdjustPosition(); } catch { }
            }

            for (int i = 0; i < Grids.Count; i++)
            {
                GridArrange g = Grids[i];
                if (g == null) continue;
                g.CellSize = GridOrigCell[i] * factor;
                try { if (g.isActiveAndEnabled) g.ArrangeChilds(); } catch { }
            }
        }
        catch { }
    }
}
