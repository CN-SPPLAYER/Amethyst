using System;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 落雪效果（独立开关）。
//
// 参考 FinalSpectrum 的 `Patches\Game_Vanilla\ShipStatusPatch.cs` ——
// 它的做法是**改游戏已有的雪**：
//     var snow = DestroyableSingleton<SnowManager>.Instance.particles;
//     snow.startColor / startSpeed / startSize = ...;
// 但有两个问题，所以这里**不照搬**：
//   1) 游戏的 `SnowManager` **只在雪地图里才有**，主界面 / 大厅根本没有雪，照抄会「开了也看不到」；
//   2) 本工程引用的 `UnityEngine.ParticleSystemModule` 是 Il2CppInterop 的**裁剪版**，
//      `MainModule.startLifetime / startSpeed / startSize / gravityModifier`、
//      `ShapeModule.shapeType / scale` 这些成员**都不存在**（编译期就报 CS1061），
//      所以自建粒子系统的路走不通。
//
// 因此改为**精灵雪花**：自己造一批小雪花 SpriteRenderer，
// 挂在摄像机前的一个容器下，由组件每帧向下移动并循环回顶部。
//   - 不依赖粒子模块，纯 SpriteRenderer，工程里已有成熟用法；
//   - 任何场景（主界面 / 大厅 / 对局）都会下雪；
//   - 悬挂在摄像机下，所以雪始终**在你周围**；
//   - 雪花贴图是运行时生成的小白点（带柔和边缘），不需要额外资源。
internal static class AmethystSnow
{
    internal static bool Enabled => AmethystConfig.SnowEffect != null && AmethystConfig.SnowEffect.Value;

    private static GameObject _root;
    private static Camera _camera;      // 换场景后相机会变，需要重建

    internal const int FlakeCount = 150;

    // 雪花活动范围（摄像机前方的一个盒子）
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
                // 64×64 的柔和白点：中心不透明、边缘渐隐。
                // ⚠ 之前只有 8×8 —— 雪花在屏幕上是**被放大**显示的，所以糊得厉害；
                //   提高到 64×64 后边缘干净清晰。（PPU 仍取 n*12，世界尺寸保持不变。）
                const int n = 64;
                Texture2D tex = new Texture2D(n, n, TextureFormat.ARGB32, false);
                tex.wrapMode = TextureWrapMode.Clamp;      // 防止边缘采样到对侧，出现杂色
                tex.filterMode = FilterMode.Bilinear;
                float c = (n - 1) * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        // 边缘渐隐、中心几乎全不透。
                        // 之前用的是 a*a，整片雪都偏半透明 → 在浅色背景上看着「雪是暗的」。
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

    // 由主界面 / 对局的 Update 每帧调用（内部很廉价）
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

            if (_root != null && _camera == cam) return;   // 已就绪

            if (_root != null) Destroy();                  // 换场景 / 换摄像机 → 重建
            _camera = cam;

            _root = new GameObject("AmethystSnow");
            _root.transform.SetParent(cam.transform, false);
            // 站在「UI 画布平面」的**略靠后一点**：
            // 相机朝 +z 看，localPosition.z 越大越远 —— 所以放在 planeDistance 之后，
            // 雪花就在背景之上、但在开始按钮等 UI 之下，不会挡到按钮。
            _root.transform.localPosition = new Vector3(0f, 0f, ComputeZ(cam));

            _root.AddComponent<AmethystSnowFlakes>().Init();
        }
        catch { }
    }

    // 算出雪花容器该放在摄像机前方多远。
    //
    // 主菜单 UI 是画布（Canvas）渲染的：如果是 Screen Space - Camera，
    // 它就位于距相机 `planeDistance` 的平面上。把雪花放在**这个平面之后**
    // 就能保证「在背景之上、在按钮之下」，不会把开始按钮挡掉。
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

// 每帧把雪花往下推，掉出底部就回到顶部（并重新随机横向位置）
internal sealed class AmethystSnowFlakes : MonoBehaviour
{
    private Transform[] _flakes;
    private float[] _speed;
    private float[] _sway;      // 左右轻微摆动，避免整齐得像雨

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
                    // 远近不同的雪花透明度 / 大小都不同，看起来才有层次
                    // （alpha 下限由 0.35 提到 0.72 —— 太低的雪花看起来发灰发暗）
                    sr.color = new Color(1f, 1f, 1f, UnityEngine.Random.Range(0.72f, 1f));
                }

                // 按要求**不做缩放**，直接用贴图原始尺寸 —— 缩放会让雪花发虚。
                // （尺寸由贴图的 PPU 决定，见 FlakeSprite。）
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

                    // 掉出底部 → 回到顶部重新来
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

// 主界面里驱动（**只在主界面**下雪 —— 对局内不下雪，很多地图本身就自带雪）
[HarmonyPatch(typeof(MainMenuManager), "LateUpdate")]
internal static class AmethystSnowMainMenuPatch
{
    private static void Postfix()
    {
        try { AmethystSnow.Tick(); } catch { }
    }
}
