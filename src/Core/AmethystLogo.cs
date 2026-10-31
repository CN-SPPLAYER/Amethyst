using System;
using UnityEngine;

namespace Amethyst
{
    
    internal static class AmethystLogo
    {
        private static Texture2D _texture;
        private static GUIStyle _style;
        private static bool _attempted;

        internal static Texture2D Texture
        {
            get
            {
                if (_attempted) return _texture;
                _attempted = true;
                try
                {
                    byte[] data = AmethystExternalResources.LoadBytes(
                        "AmethystLogo.png",
                        "Amethyst.Resources.AmethystLogo.png");
                    if (data == null || data.Length == 0) return null;

                    var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (!ImageConversion.LoadImage(texture, data, true))
                    {
                        UnityEngine.Object.Destroy(texture);
                        return null;
                    }

                    texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    _texture = texture;
                }
                catch (Exception ex)
                {
                    AmethystPlugin.Logger?.LogWarning($"[Logo] 加载失败: {ex.Message}");
                }
                return _texture;
            }
        }

        internal static void Draw(Rect rect)
        {
            Texture2D texture = Texture;
            if (texture == null) return;

            if (_style == null) _style = new GUIStyle();
            _style.normal.background = texture;

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, prev.a);
            GUI.Box(rect, GUIContent.none, _style);
            GUI.color = prev;
        }
    }
}
