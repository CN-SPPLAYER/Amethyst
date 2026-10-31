using System.Collections.Generic;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystAbilityButtonScale
{
    private const float MinScale = 0.1f;
    private const float MaxScale = 1.5f;
    private const float DefaultScale = 0.85f;

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

    internal static void Tick()
    {
        HudManager hud = HudManager.Instance;
        if (hud == null) return;

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

    private static void Scan(HudManager hud)
    {
        try
        {
            if (_buttonsRoot == null) _buttonsRoot = hud.transform.Find("Buttons");

            int added = 0;

            foreach (ActionButton btn in hud.GetComponentsInChildren<ActionButton>(true))
            {
                if (btn == null) continue;
                Transform t = btn.transform;
                if (t == null) continue;
                if (IsUnderTopRight(t)) continue;

                int id = t.GetInstanceID();
                if (!ButtonIds.Add(id)) continue;      

                ButtonTs.Add(t);
                ButtonOrigScale.Add(t.localScale);
                added++;
            }

            if (_buttonsRoot != null)
            {
                
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
                _applied = float.NaN;   
                AmethystPlugin.Logger?.LogInfo(
                    $"[AbilityScale] +{added} new | total buttons={ButtonTs.Count}"
                    + $" aspects={Aspects.Count} grids={Grids.Count}"
                    + $" root={(_buttonsRoot != null ? _buttonsRoot.name : "null")}");
            }
        }
        catch { }
    }

    private static void Apply(float factor)
    {
        try
        {
            for (int i = 0; i < ButtonTs.Count; i++)
            {
                Transform t = ButtonTs[i];
                if (t == null) continue;   
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
