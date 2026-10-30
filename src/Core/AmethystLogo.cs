using System;
using UnityEngine;

namespace Amethyst
{
    /// <summary>
    /// Amethyst 品牌水晶标志（第 290 轮）。
    /// 图标是程序化生成的紫水晶晶簇，和游戏内官方紫水晶装扮同色系；
    /// 玩家可以用 `Amethyst/Resources/AmethystLogo.png` 覆盖它。
    /// </summary>
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

            // 第 291 轮：不能直接用 GUI.DrawTexture。
            // Windows IL2CPP 下它会触发 `Method unstripping failed`，异常从 Draw 冒到
            // DrawHeader，导致整个菜单只画完背景就中断（用户看到的全黑空窗口）。
            // 改用和 AmethystIcons 相同的 GUIStyle + GUI.Box 路径，已在 IL2CPP 验证可用。
            if (_style == null) _style = new GUIStyle();
            _style.normal.background = texture;

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, prev.a);
            GUI.Box(rect, GUIContent.none, _style);
            GUI.color = prev;
        }
    }
}
