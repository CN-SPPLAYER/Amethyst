using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

internal static class ModStampState
{
    
    private static readonly Vector3 TargetScale = new Vector3(0.25f, 0.25f, 0.25f);

    private const float ShowRetryInterval = 0.5f;

    private static ModManager _mgr;
    private static SpriteRenderer _stamp;
    private static SpriteRenderer _styled;   
    private static float _nextShow;

    internal static void Tick()
    {
        try
        {
            
            if (_mgr == null)
            {
                try { _mgr = ModManager.Instance; }
                catch { _mgr = null; }
            }
            if (_mgr != null && _mgr.localCamera == null)
            {
                Camera cam = Camera.main;
                if (cam == null) cam = UnityEngine.Object.FindObjectOfType<Camera>();
                if (cam != null) _mgr.localCamera = cam;
            }

            SpriteRenderer stamp = Resolve();
            if (stamp == null) return;

            bool visible = IsEnabled(stamp);
            if (!visible && Time.unscaledTime >= _nextShow)
            {
                _nextShow = Time.unscaledTime + ShowRetryInterval;
                if (_mgr != null) { try { _mgr.ShowModStamp(); } catch { } }
                visible = IsEnabled(stamp);   
            }
            if (!visible) return;

            Sprite sprite = AmethystResources.ModStamp;
            if (sprite != null && stamp.sprite != sprite) stamp.sprite = sprite;

            _stamp = stamp;

            if (_styled == stamp) return;

            stamp.color = new Color(1f, 1f, 1f, 0.4f);
            stamp.transform.localScale = TargetScale;
            _styled = stamp;
        }
        catch { }
    }

    private static bool IsEnabled(SpriteRenderer sr)
    {
        try { return sr != null && ((Renderer)sr).enabled; }
        catch { return false; }
    }

    [HarmonyPatch(typeof(ModManager), "LateUpdate")]
    [HarmonyPostfix]
    private static void LateUpdate_Postfix(ModManager __instance)
    {
        try
        {
            if (_mgr == null) _mgr = __instance;
            Tick();
        }
        catch { }
    }

    private static SpriteRenderer Resolve()
    {
        try
        {
            if (_stamp != null) return _stamp;
            if (_mgr != null && _mgr.ModStamp != null) return _mgr.ModStamp;
        }
        catch { }

        try
        {
            GameObject go = GameObject.Find("ModStamp");
            if (go != null)
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) return sr;
            }
        }
        catch { }

        return null;
    }

    internal static void Reset()
    {
        _mgr = null;
        _stamp = null;
        _styled = null;
        _nextShow = 0f;
    }
}
