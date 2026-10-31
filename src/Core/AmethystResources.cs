using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Amethyst
{
    internal static class AmethystResources
    {
        private static Sprite _darkReport;
        private static Sprite _darkQuickChat;
        private static Sprite _darkKeyboard;
        private static Sprite _amogusBan;
        private static Sprite _modStamp;
        private static Sprite _editTag;

        internal static Sprite DarkReport => _darkReport ??= AmethystExternalResources.CreateSprite("DarkMode/DarkReport.png", "Amethyst.Resources.DarkMode.ImageResource.DarkReport.png");
        internal static Sprite DarkQuickChat => _darkQuickChat ??= AmethystExternalResources.CreateSprite("DarkMode/DarkQuickChat.png", "Amethyst.Resources.DarkMode.ImageResource.DarkQuickChat.png");
        internal static Sprite DarkKeyboard => _darkKeyboard ??= AmethystExternalResources.CreateSprite("DarkMode/DarkKeyboard.png", "Amethyst.Resources.DarkMode.ImageResource.DarkKeyboard.png");
        internal static Sprite AmogusBan => _amogusBan ??= AmethystExternalResources.CreateSprite("DarkMode/AmogusBan.png", "Amethyst.Resources.DarkMode.ImageResource.AmogusBan.png");
        internal static Sprite ModStamp => _modStamp ??= AmethystExternalResources.CreateSprite("ModStamp.png", "Amethyst.Resources.ModStamp.png");
        internal static Sprite EditTag => _editTag ??= AmethystExternalResources.CreateSprite("EditTag.png", "Amethyst.Resources.EditTag.png");

        private static Sprite LoadSprite(string resourceName)
        {
            const string resourcePrefix = "Amethyst.Resources.DarkMode.ImageResource.";
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string fullPath = resourcePrefix + resourceName;
                using Stream stream = assembly.GetManifestResourceStream(fullPath);
                if (stream == null || stream.Length <= 0 || stream.Length > int.MaxValue)
                {
                    AmethystPlugin.Logger?.LogError($"Resource stream is null for: {fullPath}");
                    return null;
                }

                using MemoryStream buffer = new MemoryStream((int)stream.Length);
                stream.CopyTo(buffer);
                byte[] data = buffer.ToArray();

                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, data, true))
                {
                    UnityEngine.Object.Destroy(texture);
                    AmethystPlugin.Logger?.LogError($"Image data could not be decoded for: {fullPath}");
                    return null;
                }

                texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                texture.filterMode = FilterMode.Bilinear;
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                return sprite;
            }
            catch (Exception ex)
            {
                AmethystPlugin.Logger?.LogError($"Failed to load embedded resource {resourceName}: {ex}");
            }
            return null;
        }

        private static Sprite[] LoadDividedSprites(string resourceName, int cellW, int cellH)
        {
            const string resourcePrefix = "Amethyst.Resources.";
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using Stream stream = assembly.GetManifestResourceStream(resourcePrefix + resourceName);
                if (stream == null || stream.Length <= 0 || stream.Length > int.MaxValue)
                {
                    AmethystPlugin.Logger?.LogError($"Resource stream is null for: {resourcePrefix}{resourceName}");
                    return null;
                }

                using MemoryStream buffer = new MemoryStream((int)stream.Length);
                stream.CopyTo(buffer);
                byte[] data = buffer.ToArray();

                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, data, true))
                {
                    UnityEngine.Object.Destroy(texture);
                    AmethystPlugin.Logger?.LogError($"Image data could not be decoded for: {resourcePrefix}{resourceName}");
                    return null;
                }

                texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                texture.filterMode = FilterMode.Bilinear;

                int cols = texture.width / cellW;
                int rows = texture.height / cellH;
                var sprites = new Sprite[cols * rows];
                for (int index = 0; index < sprites.Length; index++)
                {
                    int col = index % cols;
                    int row = index / cols;
                    float x = col * cellW;
                    float y = (rows - row - 1) * cellH;
                    Sprite sprite = Sprite.Create(texture, new Rect(x, y, cellW, cellH), new Vector2(0.5f, 0.5f), 100f);
                    sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                    sprites[index] = sprite;
                }

                return sprites;
            }
            catch (Exception ex)
            {
                AmethystPlugin.Logger?.LogError($"Failed to load divided sprite {resourceName}: {ex}");
            }
            return null;
        }

        private static Sprite LoadSpriteFromResource(string fullPath)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using Stream stream = assembly.GetManifestResourceStream(fullPath);
                if (stream == null || stream.Length <= 0 || stream.Length > int.MaxValue)
                {
                    AmethystPlugin.Logger?.LogError($"Resource stream is null for: {fullPath}");
                    return null;
                }

                using MemoryStream buffer = new MemoryStream((int)stream.Length);
                stream.CopyTo(buffer);
                byte[] data = buffer.ToArray();

                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, data, true))
                {
                    UnityEngine.Object.Destroy(texture);
                    AmethystPlugin.Logger?.LogError($"Image data could not be decoded for: {fullPath}");
                    return null;
                }

                texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                texture.filterMode = FilterMode.Bilinear;
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                return sprite;
            }
            catch (Exception ex)
            {
                AmethystPlugin.Logger?.LogError($"Failed to load embedded resource {fullPath}: {ex}");
            }
            return null;
        }
    }
}
