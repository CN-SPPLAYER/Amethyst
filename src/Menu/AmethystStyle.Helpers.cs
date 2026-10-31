using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Amethyst;

internal static partial class AmethystStyle
{
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
