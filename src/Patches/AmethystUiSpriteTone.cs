using System.Collections.Generic;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystUiSpriteTone
{
    private static readonly Dictionary<int, Sprite> Sprites = new();
    private static readonly HashSet<int> Generated = new();

    internal static void Apply(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return;
        Sprite source = renderer.sprite;
        int id = source.GetInstanceID();
        if (Generated.Contains(id)) return;
        if (!Sprites.TryGetValue(id, out Sprite result) || result == null)
        {
            Texture2D texture = source.texture;
            Rect rect = source.textureRect;
            RenderTexture previous = RenderTexture.active;
            RenderTexture temp = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            Texture2D copy = null;
            try
            {
                Graphics.Blit(texture, temp);
                RenderTexture.active = temp;
                copy = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(rect, 0, 0);
                var pixels = copy.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    float value = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    pixels[i] = new Color(value, value, value, c.a);
                }
                copy.SetPixels(pixels);
                copy.Apply(false, true);
                result = Sprite.Create(copy, new Rect(0, 0, copy.width, copy.height),
                    new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height),
                    source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
                result.name = source.name + "_AmethystNeutral";
                Sprites[id] = result;
                Generated.Add(result.GetInstanceID());
            }
            catch
            {
                if (copy != null) Object.Destroy(copy);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
            }
        }
        renderer.sprite = result;
    }
}
