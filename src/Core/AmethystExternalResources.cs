using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

namespace Amethyst
{
    /// <summary>
    /// 模组外部资源管理器：
    /// 将内嵌图片、文本等资产释放到外部目录 `Among Us/Amethyst/Resources/` 下。
    /// 
    /// 规则：
    /// 1. 启动时如果外部文件缺失任何一个，自动从程序集内嵌资源释放补齐（永不丢失基础资产）；
    /// 2. 玩家手动覆盖/修改了外部图片，直接加载玩家的新图片，绝不驳回修改；
    /// 3. 提供统一的资源字节/文件读取入口。
    /// </summary>
    internal static class AmethystExternalResources
    {
        private static string _resDir;
        internal static string ResDir => _resDir ??= Path.Combine(AmethystPlatform.DataDir, "Resources");

        private static bool _initialized;

        /// <summary>
        /// 确保外部资源目录健全：自动补齐所有缺失的内置资产。
        /// </summary>
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

                // 需要外部化的内置图片清单：相对路径 -> 内嵌资源全名
                // ⚠ 大厅图片（LobbyArt）已整体移除（第 240 轮：用户要求不再展示），
                //   因此清单里不再有它，也不会被释放到外部目录。
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

        /// <summary>
        /// 品牌资源迁移：如果外部文件仍是旧版内置资源，就用当前内置资源覆盖；
        /// 用户自己替换过的文件（hash 不同）保持不动。
        /// </summary>
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

        /// <summary>
        /// 获取外部图片字节（优先读取外部自定义文件；若不存在或读取失败，回退到内置资源）。
        /// </summary>
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

            // 回退到内置资源
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

        /// <summary>
        /// 创建 Sprite（优先外部文件，支持玩家任意替换图片）。
        /// </summary>
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
    }
}
