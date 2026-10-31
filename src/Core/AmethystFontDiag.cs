using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Amethyst
{
    internal static class AmethystFontDiag
    {
        internal static void Run()
        {
            try
            {
                var names = Font.GetOSInstalledFontNames();
                AmethystPlugin.Logger?.LogInfo($"[FontDiag] Total OS Installed Fonts: {names?.Length ?? 0}");
                if (names != null)
                {
                    var matches = names.Where(n => n.IndexOf("Smiley", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("得意", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    AmethystPlugin.Logger?.LogInfo($"[FontDiag] Smiley matches in OSInstalledFontNames: {string.Join(", ", matches)}");
                    
                    AmethystPlugin.Logger?.LogInfo($"[FontDiag] Sample OS Fonts: {string.Join(", ", names.Take(10))}");
                }
            }
            catch (Exception ex)
            {
                AmethystPlugin.Logger?.LogError($"[FontDiag] Exception: {ex}");
            }
        }
    }
}
