using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Amethyst;

internal sealed class AmethystPalette
{
    internal AmethystPalette(string id, string name, Color window, Color panel, Color accent, Color accentSoft,
        Color button, Color buttonHover, Color text, Color muted)
    {
        Id = id;
        Name = name;
        Window = window;
        Panel = panel;
        Accent = accent;
        AccentSoft = accentSoft;
        Button = button;
        ButtonHover = buttonHover;
        Text = text;
        Muted = muted;
    }

    internal string Id { get; }
    internal string Name { get; }
    internal Color Window { get; }
    internal Color Panel { get; }
    internal Color Accent { get; }
    internal Color AccentSoft { get; }
    internal Color Button { get; }
    internal Color ButtonHover { get; }
    internal Color Text { get; }
    internal Color Muted { get; }
}

internal static class AmethystStyle
{

    private static Color Soft(Color a) => new Color(a.r * 0.34f, a.g * 0.34f, a.b * 0.34f, 1f);

    private static AmethystPalette Theme(string name, Color accent) =>
        new AmethystPalette(name.ToLowerInvariant(), name,
            Rgb(10, 11, 15),      // window #0A0B0F
            Rgb(18, 20, 26),      // panel  #12141A
            accent,
            Soft(accent),
            Rgb(31, 34, 42),      // button
            Rgb(45, 49, 60),      // button hover
            Rgb(240, 241, 245),   // text
            Rgb(150, 155, 168));  // muted

    // ===== 主题已固定 =====
    // 按要求**移除主题选项**，强制使用 **#A06EFF**（即原来列表里的 Violet 色号）。
    // `AmethystPalette` / `Current` 这套 API 保留不变，所以其它所有界面代码无需改动，
    // 只是 Current 永远返回同一个调色板，不再读配置、也不再随玩家选择变化。
    private static readonly AmethystPalette Forced =
        Theme("Amethyst", Rgb(160, 110, 255));      // #A06EFF

    private static Texture2D _white;
    private static GUIStyle _fill;
    private static GUIStyle _texStyle;
    private static readonly Dictionary<int, Texture2D> RoundedTex = new Dictionary<int, Texture2D>();
    private static readonly Dictionary<int, GUIStyle> RoundedStyles = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle> StrokeStyles = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle> HaloStyles = new Dictionary<int, GUIStyle>();

    [HideFromIl2Cpp]
    internal static AmethystPalette Current => Forced;

    internal static Texture2D White
    {
        get
        {
            if (_white == null) _white = Solid(Color.white);
            return _white;
        }
    }

    internal static Texture2D Solid(Color c)
    {

        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp
        };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    internal static void ClampWindow(ref Rect win, float sw, float sh, float minW, float minH)
    {
        win.width = Mathf.Clamp(win.width, minW, sw);
        win.height = Mathf.Clamp(win.height, minH, sh);
        win.x = Mathf.Clamp(win.x, 0f, sw - win.width);
        win.y = Mathf.Clamp(win.y, 0f, sh - win.height);
    }

    internal static void Fill(Rect r, Color c)
    {
        if (_fill == null)
        {
            _fill = new GUIStyle();
            _fill.normal.background = White;
        }

        Color prev = GUI.color;
        GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
        GUI.Box(r, GUIContent.none, _fill);
        GUI.color = prev;
    }

    internal static void FillRaw(Rect r)
    {
        if (_fill == null) { _fill = new GUIStyle(); _fill.normal.background = White; }
        GUI.Box(r, GUIContent.none, _fill);
    }

    internal static void FillRounded(Rect r, Color c, int radius)
    {
        GUIStyle st = RoundedStyle(radius);
        Color prev = GUI.color;
        GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
        GUI.Box(r, GUIContent.none, st);
        GUI.color = prev;
    }

    internal static GUIStyle RoundedStyle(int radius)
    {
        radius = Mathf.Clamp(radius, 2, 40);
        if (RoundedStyles.TryGetValue(radius, out GUIStyle s) && s != null) return s;

        s = new GUIStyle();
        s.normal.background = RoundedTexture(radius);
        s.border = Offset(radius, radius, radius, radius);
        RoundedStyles[radius] = s;
        return s;
    }

    private static Texture2D RoundedTexture(int radius)
    {
        if (RoundedTex.TryGetValue(radius, out Texture2D t) && t != null) return t;

        int size = radius * 2 + 2;
        t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sx = x + 0.5f;
                float sy = y + 0.5f;
                float dx = sx < radius ? radius - sx : (sx > size - radius ? sx - (size - radius) : 0f);
                float dy = sy < radius ? radius - sy : (sy > size - radius ? sy - (size - radius) : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - dist + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        t.SetPixels32(px);
        t.Apply();
        RoundedTex[radius] = t;
        return t;
    }

    internal static void StrokeRounded(Rect r, Color c, int radius, int thickness)
    {
        radius = Mathf.Clamp(radius, 3, 40);
        thickness = Mathf.Clamp(thickness, 1, 6);
        int key = radius * 16 + thickness;
        if (!StrokeStyles.TryGetValue(key, out GUIStyle st) || st == null)
        {
            st = new GUIStyle();
            st.normal.background = BuildStroke(radius, thickness);
            st.border = Offset(radius, radius, radius, radius);
            StrokeStyles[key] = st;
        }

        Color prev = GUI.color;
        GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
        GUI.Box(r, GUIContent.none, st);
        GUI.color = prev;
    }

    private static Texture2D BuildStroke(int radius, int thickness)
    {
        int size = radius * 2 + 2;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float c = size / 2f;
        float ext = size / 2f - radius;
        float half = thickness * 0.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - c) - ext;
                float qy = Mathf.Abs(y + 0.5f - c) - ext;
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                float sd = outside + inside - radius;
                float a = Mathf.Clamp01(half - Mathf.Abs(sd) + 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    // 圆角矩形 SDF 光晕：与 OnyxMenu `Skin.BuildDrop/Glow` 同构。
    // reach = 向外扩散的距离；radius = 圆角半径。九宫格拉伸，任意窗口尺寸都不会变形。
    internal static void Halo(Rect r, Color c, int radius, int reach)
    {
        radius = Mathf.Clamp(radius, 4, 40);
        reach = Mathf.Clamp(reach, 8, 80);
        int key = radius * 128 + reach;
        if (!HaloStyles.TryGetValue(key, out GUIStyle st) || st == null)
        {
            st = BuildHalo(radius, reach);
            HaloStyles[key] = st;
        }
        Color prev = GUI.color;
        GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
        GUI.Box(r, GUIContent.none, st);
        GUI.color = prev;
    }

    private static GUIStyle BuildHalo(int radius, int reach)
    {
        int n = (reach + radius) * 2 + 2;
        int core = n - reach * 2;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = RoundRectSd(x + 0.5f - reach, y + 0.5f - reach, core, core, radius);
                float t = Mathf.Clamp01(1f - d / reach);
                t = t * t * (3f - 2f * t);
                float a = t * Mathf.Sqrt(t);
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        }

        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        tex.SetPixels32(px);
        tex.Apply();

        var st = new GUIStyle();
        st.normal.background = tex;
        st.border = Offset(reach + radius, reach + radius, reach + radius, reach + radius);
        return st;
    }

    private static float RoundRectSd(float x, float y, float w, float h, float rad)
    {
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - rad);
        float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - rad);
        float ox = Mathf.Max(qx, 0f);
        float oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;
    }

    internal static void DrawTex(Rect r, Texture2D tex)
    {
        if (tex == null) return;
        if (_texStyle == null) _texStyle = new GUIStyle();
        _texStyle.normal.background = tex;
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, prev.a);
        GUI.Box(r, GUIContent.none, _texStyle);
        GUI.color = prev;
    }

    internal static Texture2D BuildGradient(int w, int h, Color top, Color bottom, int radius)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            Color row = Color.Lerp(top, bottom, (float)y / Mathf.Max(1, h - 1));
            for (int x = 0; x < w; x++)
            {
                float dx = x < radius ? radius - x : (x > w - radius ? x - (w - radius) : 0f);
                float dy = y < radius ? radius - y : (y > h - radius ? y - (h - radius) : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - dist + 0.5f);
                px[y * w + x] = new Color(row.r, row.g, row.b, a);
            }
        }

        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    internal static Texture2D BuildVFade(int h, Color color, float topAlpha, float bottomAlpha)
    {
        var t = new Texture2D(1, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[h];
        for (int y = 0; y < h; y++)
        {
            float a = Mathf.Lerp(topAlpha, bottomAlpha, (float)y / Mathf.Max(1, h - 1));
            px[y] = new Color(color.r, color.g, color.b, a);
        }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    internal static Texture2D BuildDisc(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[size * size];
        float cc = (size - 1) * 0.5f, rad = cc - 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cc, dy = y - cc;
                float a = Mathf.Clamp01(rad - Mathf.Sqrt(dx * dx + dy * dy) + 0.75f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    internal static Texture2D BuildGlow(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[size * size];
        float cc = (size - 1) * 0.5f, rad = cc;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cc, dy = y - cc;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / rad);
                a = a * a * a;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    // 第 282 轮：主界面空闲时预生成菜单会用到的圆角/描边/光晕贴图，
    // 避免第一次打开菜单时在主线程上集中创建而造成掉帧。
    internal static void PrewarmTextures()
    {
        try
        {
            int[] rounded = { 2, 7, 8, 11, 12, 14, 16, 22 };
            foreach (int r in rounded) RoundedStyle(r);

            int[] strokes = { 8, 11, 12, 16 };
            foreach (int r in strokes)
            {
                int key = r * 16 + 1;
                if (!StrokeStyles.TryGetValue(key, out GUIStyle st) || st == null)
                    StrokeStyles[key] = BuildStrokeStyle(r, 1);
            }

            const int haloKey = 18 * 128 + 40;
            if (!HaloStyles.TryGetValue(haloKey, out GUIStyle hs) || hs == null)
                HaloStyles[haloKey] = BuildHalo(18, 40);
        }
        catch { }
    }

    private static GUIStyle BuildStrokeStyle(int radius, int thickness)
    {
        var st = new GUIStyle();
        st.normal.background = BuildStroke(radius, thickness);
        st.border = Offset(radius, radius, radius, radius);
        return st;
    }

    internal static RectOffset Offset(int l, int r, int t, int b)
    {
        var o = new RectOffset();
        o.left = l;
        o.right = r;
        o.top = t;
        o.bottom = b;
        return o;
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
}
