using System;
using System.IO;
using UnityEngine;

namespace Amethyst
{
    internal static class AmethystFontTest
    {
        internal static void InspectFont(Font f, string tag)
        {
            if (f == null)
            {
                AmethystPlugin.Logger?.LogInfo($"[FontInspect] {tag}: null");
                return;
            }
            try
            {
                AmethystPlugin.Logger?.LogInfo($"[FontInspect] {tag}: name={f.name}, dynamic={f.dynamic}, fontSize={f.fontSize}, mat={f.material?.name}, shader={f.material?.shader?.name}, tex={f.material?.mainTexture != null}");
            }
            catch (Exception ex)
            {
                AmethystPlugin.Logger?.LogError($"[FontInspect] {tag} ex: {ex.Message}");
            }
        }
    }
}
