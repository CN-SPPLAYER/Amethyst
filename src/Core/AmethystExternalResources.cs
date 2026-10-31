using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

namespace Amethyst
{
    
    internal static class AmethystExternalResources
    {
        
        private const int MaxSliceCount = 512;

        private static string _resDir;
        internal static string ResDir => _resDir ??= Path.Combine(AmethystPlatform.DataDir, "Resources");

        private static bool _initialized;

        internal static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                if (!Directory.Exists(ResDir))
                {
                    Directory.CreateDirectory(ResDir);
                }

                var assets = new (string relativePath, string resName)[]
                {
                    ("DarkMode/DarkReport.png", "Amethyst.Resources.DarkMode.ImageResource.DarkReport.png"),
                    ("DarkMode/DarkQuickChat.png", "Amethyst.Resources.DarkMode.ImageResource.DarkQuickChat.png"),
                    ("DarkMode/DarkKeyboard.png", "Amethyst.Resources.DarkMode.ImageResource.DarkKeyboard.png"),
                    ("DarkMode/AmogusBan.png", "Amethyst.Resources.DarkMode.ImageResource.AmogusBan.png"),
                    ("ModStamp.png", "Amethyst.Resources.ModStamp.png"),
                    ("EditTag.png", "Amethyst.Resources.EditTag.png"),
                    ("AmethystLogo.png", "Amethyst.Resources.AmethystLogo.png"),
                    ("MainMenuArt.png", "Amethyst.Resources.MainMenuArt.png"),
                    ("MainMenuArt2.png", "Amethyst.Resources.MainMenuArt2.png"),
                    ("MainMenuArt3.png", "Amethyst.Resources.MainMenuArt3.png"),

                    ("KeyGuide/KeyBindBackground.png", "Amethyst.Resources.KeyGuide.KeyBindBackground.png"),
                    ("KeyGuide/KeyBindCharacters0.png", "Amethyst.Resources.KeyGuide.KeyBindCharacters0.png"),
                    ("KeyGuide/KeyBindCharacters1.png", "Amethyst.Resources.KeyGuide.KeyBindCharacters1.png"),
                    ("KeyGuide/KeyBindCharacters2.png", "Amethyst.Resources.KeyGuide.KeyBindCharacters2.png"),
                    ("KeyGuide/KeyBindCharacters3.png", "Amethyst.Resources.KeyGuide.KeyBindCharacters3.png"),
                    ("KeyGuide/KeyBindCharacters4.png", "Amethyst.Resources.KeyGuide.KeyBindCharacters4.png"),
                    ("KeyGuide/KeyBindCharacters5.png", "Amethyst.Resources.KeyGuide.KeyBindCharacters5.png"),
                    ("KeyGuide/KeyBindOption.png", "Amethyst.Resources.KeyGuide.KeyBindOption.png"),
                };

                Assembly asm = typeof(AmethystExternalResources).Assembly;
                foreach (var (relPath, resName) in assets)
                {
                    string targetFile = Path.Combine(ResDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(targetFile))
                    {
                        try
                        {
                            string dir = Path.GetDirectoryName(targetFile);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            using Stream stream = asm.GetManifestResourceStream(resName);
                            if (stream != null)
                            {
                                using var fs = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                                stream.CopyTo(fs);
                                AmethystPlugin.Logger?.LogInfo($"[Resources] 外部文件缺失已自动补齐: {relPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            AmethystPlugin.Logger?.LogWarning($"[Resources] 补齐外部资源失败 {relPath}: {ex.Message}");
                        }
                    }
                }
                RefreshBrandAsset(asm, "AmethystLogo.png", "Amethyst.Resources.AmethystLogo.png",
                    "9E75E41642E46B16157F9C16CCBA037FD125BDF2F8BED2EB1FA1D9D88165ABDA");
            }
            catch (Exception ex)
            {
                AmethystPlugin.Logger?.LogError($"[Resources] 初始化外部资源目录异常: {ex}");
            }
        }

        private static void RefreshBrandAsset(Assembly asm, string relPath, string resName, string oldHash)
        {
            try
            {
                string target = Path.Combine(ResDir, relPath);
                if (!File.Exists(target)) return;

                byte[] current = File.ReadAllBytes(target);
                using (var sha = SHA256.Create())
                {
                    string hash = Convert.ToHexString(sha.ComputeHash(current));
                    if (!string.Equals(hash, oldHash, StringComparison.OrdinalIgnoreCase)) return;
                }

                using Stream src = asm.GetManifestResourceStream(resName);
                if (src == null) return;

                using var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read);
                src.CopyTo(fs);
                AmethystPlugin.Logger?.LogInfo($"[Resources] 品牌资源已更新: {relPath}");
            }
            catch (Exception ex)
            {
                AmethystPlugin.Logger?.LogWarning($"[Resources] 更新品牌资源失败 {relPath}: {ex.Message}");
            }
        }

        internal static byte[] LoadBytes(string relPath, string embeddedFallbackName)
        {
            EnsureInitialized();

            string externalPath = Path.Combine(ResDir, relPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(externalPath))
            {
                try
                {
                    return File.ReadAllBytes(externalPath);
                }
                catch (Exception ex)
                {
                    AmethystPlugin.Logger?.LogWarning($"[Resources] 读取外部文件失败 {relPath}，回退到内置: {ex.Message}");
                }
            }

            try
            {
                Assembly asm = typeof(AmethystExternalResources).Assembly;
                using Stream s = asm.GetManifestResourceStream(embeddedFallbackName);
                if (s != null)
                {
                    using var ms = new MemoryStream((int)s.Length);
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch { }

            return null;
        }

        internal static Sprite CreateSprite(string relPath, string embeddedFallbackName, float pixelsPerUnit = 100f)
        {
            byte[] data = LoadBytes(relPath, embeddedFallbackName);
            if (data == null || data.Length == 0) return null;

            return CreateSpriteFromBytes(data, pixelsPerUnit, relPath);
        }

        private static Sprite CreateSpriteFromBytes(byte[] data, float pixelsPerUnit, string tag)
        {
            if (data == null || data.Length == 0) return null;

            try
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, data, true))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                texture.filterMode = FilterMode.Bilinear;
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
                sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                return sprite;
            }
            catch (Exception ex)
            {
                AmethystPlugin.Logger?.LogError($"[Resources] 解码 Sprite 失败 {tag}: {ex.Message}");
                return null;
            }
        }

        internal static Sprite[] CreateSlicedSprites(string relPath, string embeddedFallbackName, int cellWidth, int cellHeight, float pixelsPerUnit = 100f)
        {
            if (cellWidth <= 0 || cellHeight <= 0) return null;

            byte[] data = LoadBytes(relPath, embeddedFallbackName);
            if (data == null || data.Length == 0) return null;

            Texture2D texture = null;
            try
            {
                
                texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, data, true))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                texture.filterMode = FilterMode.Bilinear;

                int cols = texture.width / cellWidth;
                int rows = texture.height / cellHeight;
                if (cols <= 0 || rows <= 0)
                {
                    AmethystPlugin.Logger?.LogWarning(
                        $"[Resources] 图片尺寸 {texture.width}x{texture.height} 装不下 {cellWidth}x{cellHeight} 的格子: {relPath}");
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                long cellCount = (long)cols * rows;
                if (cellCount > MaxSliceCount)
                {
                    AmethystPlugin.Logger?.LogWarning(
                        $"[Resources] 图片 {relPath} 尺寸 {texture.width}x{texture.height} 按 {cellWidth}x{cellHeight} 切成 {cols}x{rows} = {cellCount} 格，超过上限 {MaxSliceCount}，拒绝切片");
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                if (texture.width != cols * cellWidth || texture.height != rows * cellHeight)
                {
                    AmethystPlugin.Logger?.LogWarning(
                        $"[Resources] 图片 {relPath} 尺寸 {texture.width}x{texture.height} 不是格子 {cellWidth}x{cellHeight} 的整数倍（{cols}x{rows} 格），切片可能错位");
                }

                var sprites = new Sprite[cols * rows];
                var pivot = new Vector2(0.5f, 0.5f);
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        var rect = new Rect(col * cellWidth, (rows - 1 - row) * cellHeight, cellWidth, cellHeight);
                        Sprite sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit);
                        sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                        sprites[row * cols + col] = sprite;
                    }
                }

                return sprites;
            }
            catch (Exception ex)
            {
                
                if (texture != null) UnityEngine.Object.Destroy(texture);
                AmethystPlugin.Logger?.LogError($"[Resources] 切分图片失败 {relPath}: {ex.Message}");
                return null;
            }
        }
    }
}
