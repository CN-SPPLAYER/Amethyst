using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 主界面展示图片轮换：在主菜单背后循环渐变平滑轮换多张大图。
//
// 图片来源：
//   - 图1: MainMenuArt.png (内置 Amethyst 主图)
//   - 图2: MainMenuArt2.png (BackThings.png)
//   - 图3: MainMenuArt3.png (backthingsred.png)
//
// 轮换与透明度逻辑：
//   - 当前展示图与下一张图叠加，上一张 alpha = 1 - t，下一张 alpha = t。
//   - 两张图片叠加后的综合透明度始终为 1.0 (100% 不透明)，绝不会露给主界面深色背景。
internal static class AmethystMainMenuArt
{
    internal static bool Enabled => AmethystConfig.MainMenuArt != null && AmethystConfig.MainMenuArt.Value;

    private static GameObject _artRoot;
    private static GameObject _artObj1;
    private static GameObject _artObj2;
    private static SpriteRenderer _sr1;
    private static SpriteRenderer _sr2;

    private static readonly Sprite[] Sprites = new Sprite[3];

    private const float PixelsPerUnit = 150f;
    private static readonly Vector3 Position = new Vector3(0f, 0f, 600f);

    // 轮换动画控制参数：每张图展示 6 秒，过渡渐变持续 0.8 秒
    private const float DisplayTime = 6f;
    private const float FadeTime = 0.8f;

    private static int _currentIndex = 0;
    private static float _timer = 0f;
    private static bool _isFading = false;

    internal static void Apply(MainMenuManager menu)
    {
        try
        {
            if (menu == null) return;

            if (!Enabled)
            {
                Destroy();
                return;
            }

            SetAmbienceVisible(false);

            if (_artRoot == null)
            {
                EnsureSpritesLoaded();
                if (Sprites[0] == null) return;

                _artRoot = new GameObject("AmethystMainMenuArtRoot");
                _artRoot.transform.position = Position;

                _artObj1 = new GameObject("ArtLayer1");
                _artObj1.transform.SetParent(_artRoot.transform, false);
                _sr1 = _artObj1.AddComponent<SpriteRenderer>();

                _artObj2 = new GameObject("ArtLayer2");
                _artObj2.transform.SetParent(_artRoot.transform, false);
                _sr2 = _artObj2.AddComponent<SpriteRenderer>();

                _currentIndex = 0;
                _timer = 0f;
                _isFading = false;

                _sr1.sprite = Sprites[0];
                _sr1.color = new Color(1f, 1f, 1f, 1f);
                _artObj1.transform.localPosition = Vector3.zero;

                _sr2.sprite = Sprites[1] ?? Sprites[0];
                _sr2.color = new Color(1f, 1f, 1f, 0f);
                _artObj2.transform.localPosition = new Vector3(0f, 0f, -0.1f);

                FitToScreen(_sr1, _artObj1);
                FitToScreen(_sr2, _artObj2);
            }

            UpdateRotation();
        }
        catch { }
    }

    private static void UpdateRotation()
    {
        if (_artRoot == null || _sr1 == null || _sr2 == null) return;

        _timer += Time.deltaTime;

        if (!_isFading)
        {
            if (_timer >= DisplayTime)
            {
                _timer = 0f;
                _isFading = true;

                int nextIndex = (_currentIndex + 1) % 3;
                if (Sprites[nextIndex] == null) nextIndex = 0;

                _sr2.sprite = Sprites[nextIndex];
                _sr2.color = new Color(1f, 1f, 1f, 0f);
                FitToScreen(_sr2, _artObj2);
            }
        }
        else
        {
            float t = Mathf.Clamp01(_timer / FadeTime);
            
            // 精确双层叠加：Layer1 (1 - t), Layer2 (t)，综合覆盖率恒定为 100%
            _sr1.color = new Color(1f, 1f, 1f, 1f - t);
            _sr2.color = new Color(1f, 1f, 1f, t);

            if (t >= 1f)
            {
                _currentIndex = (_currentIndex + 1) % 3;
                if (Sprites[_currentIndex] == null) _currentIndex = 0;

                _timer = 0f;
                _isFading = false;

                // 交换 Layer，准备下一次轮换
                _sr1.sprite = Sprites[_currentIndex];
                _sr1.color = new Color(1f, 1f, 1f, 1f);
                FitToScreen(_sr1, _artObj1);

                _sr2.color = new Color(1f, 1f, 1f, 0f);
            }
        }
    }

    internal static void Destroy()
    {
        try
        {
            if (_artRoot != null) UnityEngine.Object.Destroy(_artRoot);
        }
        catch { }
        _artRoot = null;
        _artObj1 = null;
        _artObj2 = null;
        _sr1 = null;
        _sr2 = null;

        SetAmbienceVisible(true);
    }

    private static GameObject _ambience;
    private static bool _ambienceHidden;
    private static float _nextFind;

    internal static void SetAmbienceVisible(bool visible)
    {
        try
        {
            if (visible)
            {
                if (_ambienceHidden && _ambience != null) _ambience.SetActive(true);
                _ambience = null;
                _ambienceHidden = false;
                return;
            }

            if (_ambienceHidden && _ambience == null)
            {
                _ambienceHidden = false;
            }

            if (_ambienceHidden) return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextFind) return;
            _nextFind = now + 1f;

            _ambience = GameObject.Find("Ambience");
            if (_ambience == null) return;

            _ambience.SetActive(false);
            _ambienceHidden = true;
        }
        catch { }
    }

    private static void FitToScreen(SpriteRenderer sr, GameObject targetObj)
    {
        try
        {
            Camera cam = Camera.main;
            if (cam == null || sr == null || sr.sprite == null || targetObj == null) return;

            float dist = Mathf.Abs(Position.z - cam.transform.position.z);

            float worldH, worldW;
            if (cam.orthographic)
            {
                worldH = cam.orthographicSize * 2f;
                worldW = worldH * cam.aspect;
            }
            else
            {
                worldH = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                worldW = worldH * cam.aspect;
            }

            Vector3 size = sr.sprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f) return;

            float k = Mathf.Max(worldW / size.x, worldH / size.y);
            targetObj.transform.localScale = new Vector3(k, k, 1f);
        }
        catch { }
    }

    private static void EnsureSpritesLoaded()
    {
        if (Sprites[0] != null) return;

        Sprites[0] = AmethystExternalResources.CreateSprite("MainMenuArt.png", "Amethyst.Resources.MainMenuArt.png");
        Sprites[1] = AmethystExternalResources.CreateSprite("MainMenuArt2.png", "Amethyst.Resources.MainMenuArt2.png");
        Sprites[2] = AmethystExternalResources.CreateSprite("MainMenuArt3.png", "Amethyst.Resources.MainMenuArt3.png");
    }

    private static Sprite LoadSprite(string resourceName)
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream s = asm.GetManifestResourceStream(resourceName);
            if (s == null || s.Length <= 0 || s.Length > int.MaxValue) return null;

            using MemoryStream buffer = new MemoryStream((int)s.Length);
            s.CopyTo(buffer);
            byte[] data = buffer.ToArray();

            Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!ImageConversion.LoadImage(tex, data, false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            tex.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
            return sprite;
        }
        catch { return null; }
    }
}

// 进入主界面时应用
[HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class AmethystMainMenuArtStartPatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        AmethystMainMenuArt.Apply(__instance);
    }
}

// 低频兜底（借用主菜单自身的 LateUpdate）：中途开关无需重进主界面即可生效。
// Apply() 内部在「已创建 / 已关闭」时都是廉价返回，所以每帧调用没有负担。
[HarmonyPatch(typeof(MainMenuManager), "LateUpdate")]
internal static class AmethystMainMenuArtUpdatePatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        try { AmethystMainMenuArt.Apply(__instance); } catch { }
    }
}
