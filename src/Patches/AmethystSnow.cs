using System;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystSnow
{
    internal static bool Enabled => AmethystConfig.SnowEffect != null && AmethystConfig.SnowEffect.Value;

    private static GameObject _root;
    private static Camera _camera;      

    internal const int FlakeCount = 150;

    internal const float AreaWidth = 16f;
    internal const float AreaHeight = 10f;
    internal const float AheadOfCamera = 7f;

    private static Sprite _flake;

    internal static Sprite FlakeSprite
    {
        get
        {
            if (_flake != null) return _flake;
            try
            {
                
                const int n = 64;
                Texture2D tex = new Texture2D(n, n, TextureFormat.ARGB32, false);
                tex.wrapMode = TextureWrapMode.Clamp;      
                tex.filterMode = FilterMode.Bilinear;
                float c = (n - 1) * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        
                        float a = Mathf.Clamp01((1f - d) * 1.6f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                tex.hideFlags |= HideFlags.HideAndDontSave;

                _flake = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n * 12f);
                _flake.hideFlags |= HideFlags.HideAndDontSave;
            }
            catch { }
            return _flake;
        }
    }

    internal static void Tick()
    {
        try
        {
            if (!Enabled)
            {
                Destroy();
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            if (_root != null && _camera == cam) return;   

            if (_root != null) Destroy();                  
            _camera = cam;

            _root = new GameObject("AmethystSnow");
            _root.transform.SetParent(cam.transform, false);
            
            _root.transform.localPosition = new Vector3(0f, 0f, ComputeZ(cam));

            _root.AddComponent<AmethystSnowFlakes>().Init();
        }
        catch { }
    }

    private static float ComputeZ(Camera cam)
    {
        try
        {
            Canvas canvas = cam.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = UnityEngine.Object.FindObjectOfType<Canvas>();

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                float d = canvas.planeDistance + 0.5f;
                if (d > 1f) return d;
            }
        }
        catch { }
        return AheadOfCamera;
    }

    internal static void Destroy()
    {
        try
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
        catch { }
        _root = null;
        _camera = null;
    }
}

internal sealed class AmethystSnowFlakes : MonoBehaviour
{
    private Transform[] _flakes;
    private float[] _speed;
    private float[] _sway;      

    public AmethystSnowFlakes(IntPtr ptr) : base(ptr) { }

    internal void Init()
    {
        try
        {
            int count = AmethystSnow.FlakeCount;
            _flakes = new Transform[count];
            _speed = new float[count];
            _sway = new float[count];

            Sprite sprite = AmethystSnow.FlakeSprite;

            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("flake");
                go.transform.SetParent(transform, false);

                if (sprite != null)
                {
                    SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    
                    sr.color = new Color(1f, 1f, 1f, UnityEngine.Random.Range(0.72f, 1f));
                }

                go.transform.localScale = Vector3.one;
                go.transform.localPosition = RandomSpot();

                _flakes[i] = go.transform;
                _speed[i] = UnityEngine.Random.Range(0.7f, 2.2f);
                _sway[i] = UnityEngine.Random.Range(-0.25f, 0.25f);
            }
        }
        catch { }
    }

    private static Vector3 RandomSpot()
    {
        return new Vector3(
            UnityEngine.Random.Range(-AmethystSnow.AreaWidth * 0.5f, AmethystSnow.AreaWidth * 0.5f),
            UnityEngine.Random.Range(-AmethystSnow.AreaHeight * 0.5f, AmethystSnow.AreaHeight * 0.5f),
            UnityEngine.Random.Range(-2f, 2f));
    }

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_Other);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_Other); }
    }

    public void UpdateInner()
    {
            if (_flakes == null) return;

            try
            {
                float dt = Time.deltaTime;
                float half = AmethystSnow.AreaHeight * 0.5f;
                float halfW = AmethystSnow.AreaWidth * 0.5f;

                for (int i = 0; i < _flakes.Length; i++)
                {
                    Transform t = _flakes[i];
                    if (t == null) continue;

                    Vector3 p = t.localPosition;
                    p.y -= _speed[i] * dt;
                    p.x += _sway[i] * dt;

                    if (p.y < -half)
                    {
                        p.y = half;
                        p.x = UnityEngine.Random.Range(-halfW, halfW);
                    }
                    else if (p.x < -halfW) p.x = halfW;
                    else if (p.x > halfW) p.x = -halfW;

                    t.localPosition = p;
                }
            }
            catch { }
    
    }
}

[HarmonyPatch(typeof(MainMenuManager), "LateUpdate")]
internal static class AmethystSnowMainMenuPatch
{
    private static void Postfix()
    {
        try { AmethystSnow.Tick(); } catch { }
    }
}
