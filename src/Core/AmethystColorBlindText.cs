using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystColorBlindText : MonoBehaviour
{
    public AmethystColorBlindText(IntPtr ptr) : base(ptr) { }

    internal static bool On => AmethystConfig.ColorBlindText != null && AmethystConfig.ColorBlindText.Value;
    private void LateUpdate()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_NameFlush);
    try { LateUpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_NameFlush); }
    }

    private void LateUpdateInner()
    {
            if (!On || AmethystNameRendering.ProcessedFrame == Time.frameCount) return;

            try
            {
                var list = AmethystPlayerCacheStore.All;
                for (int i = 0; i < list.Count; i++)
                {
                    AmethystPlayerCache c = list[i];
                    if (c == null) continue;
                    ApplyPending(c);
                }
            }
            catch { }
    
    }

    internal static void ApplyPending(AmethystPlayerCache c)
    {
        if (c == null) return;

        TextMeshPro text = c.ColorBlindText;
        bool visible = AmethystTextVisibility.For(c);
        if (text != null)
        {
            try
            {
                if (text.enabled != visible) text.enabled = visible;
                if (text.gameObject.activeSelf != visible) text.gameObject.SetActive(visible);
            }
            catch { }
        }

        if (c.PendingColorBlindId == int.MinValue) return;
        c.PendingColorBlindId = int.MinValue;
        if (text == null) return;

        int id = c.ShownColorId >= 0 ? c.ShownColorId : c.ColorId;
        string want = AmethystColors.ColoredColorName(id);
        if (string.IsNullOrEmpty(want)) return;

        try
        {
            text.richText = true;
            if (text.text != want) text.text = want;
            if (text.enabled != visible) text.enabled = visible;
            if (text.gameObject.activeSelf != visible) text.gameObject.SetActive(visible);
            c.WrittenColorBlindId = id;
            c.WrittenForColorBlind = text;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(CosmeticsLayer), "GetColorBlindText")]
internal static class AmethystColorBlindTextPatch
{
    private static bool Prefix(CosmeticsLayer __instance, ref string __result)
    {
        try
        {
            if (__instance == null || !AmethystColorBlindText.On) return true;

            int colorId = 0;
            try { colorId = __instance.bodyMatProperties.ColorId; } catch { }

            string name = AmethystColors.ColoredColorName(colorId);
            if (!string.IsNullOrEmpty(name))
            {
                __result = name;
                return false;
            }
        }
        catch { }
        return true;
    }
}
