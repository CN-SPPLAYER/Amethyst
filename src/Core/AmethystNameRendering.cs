using System;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystNameRendering : MonoBehaviour
{
    public AmethystNameRendering(IntPtr ptr) : base(ptr) { }
    internal static int ProcessedFrame { get; private set; } = -1;

    private void LateUpdate()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_NameFlush);
    try { LateUpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_NameFlush); }
    }

    private void LateUpdateInner()
    {
            if (!AmethystColors.NameColorsOn) return;

            Patches.AmethystMushroomMixup.Tick();

            FlushNames();

            if (!Patches.AmethystRoleTagPicker.IsActive) ColorShifterMenu();
    
    }

    private static void FlushNames()
    {
        try
        {
            bool mixup = Patches.AmethystMushroomMixup.ActiveNow();
            var list = AmethystPlayerCacheStore.All;

            AmethystNameDiag.Tick(list, mixup);

            int frame = Time.frameCount;
            bool forceAllNames = AmethystPlayerCacheStore.LocalDead && !mixup
                                 && (InHideAndSeek() || frame % 10 == 0);

            for (int i = 0; i < list.Count; i++)
            {
                AmethystPlayerCache c = list[i];
                if (c == null) continue;

                AmethystPlayerCacheStore.RefreshIfDirty(c);
                AmethystColorBlindText.ApplyPending(c);

                TextMeshPro text = c.NameText;
                if (text == null) continue;

                bool textVisible = AmethystTextVisibility.For(c);
                if (!textVisible)
                {
                    try
                    {
                        if (text.enabled) text.enabled = false;
                        if (text.gameObject.activeSelf) text.gameObject.SetActive(false);
                    }
                    catch { }
                    continue;
                }

                if (forceAllNames)
                {
                    try
                    {
                        if (!text.enabled) text.enabled = true;
                        if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
                    }
                    catch { }
                }

                if (mixup)
                {
                    ApplyBodyColor(c, text);
                    continue;
                }

                if (c.Id == AmethystPlayerCacheStore.LocalId)
                {
                    
                    bool isVisible = textVisible;
                    try
                    {
                        if (text.enabled != isVisible) text.enabled = isVisible;
                        if (text.gameObject.activeSelf != isVisible) text.gameObject.SetActive(isVisible);
                    }
                    catch { }

                    if (isVisible) ApplyBodyColor(c, text);
                    continue;
                }

                if (c.IsShapeshifting || !c.OutfitIsDefault) continue;

                if (c.PendingNameText != null)
                {
                    try { text.text = c.PendingNameText; } catch { }
                    c.WrittenNameText = c.PendingNameText;
                    
                    c.WrittenForName = text;
                    c.PendingNameText = null;
                }
            }
            ProcessedFrame = Time.frameCount;
        }
        catch { }
    }

    private static void ApplyBodyColor(AmethystPlayerCache c, TextMeshPro text)
    {
        int id = c.ShownColorId;
        if (id < 0) return;

        try
        {
            Color target = AmethystColors.ColorOf(id);
            Color cur = text.color;
            target.a = cur.a;                       
            if (!SameRgb(cur, target)) text.color = target;
            c.WrittenVertexColorId = id;
            c.PendingVertexColorId = int.MinValue;
        }
        catch { }

        if (c.AppliedBodyWrapId != id)
        {
            string hex = AmethystColors.Hex(id);
            if (!string.IsNullOrEmpty(hex))
            {
                try
                {
                    string live = text.text;
                    string inner = UnwrapColor(live);
                    if (!string.IsNullOrEmpty(inner))
                    {
                        string want = $"<color={hex}>{inner}</color>";
                        if (live != want) text.text = want;
                        c.WrittenNameText = want;
                    }
                    c.AppliedBodyWrapId = id;
                }
                catch { }
            }
        }
    }

    private static bool SameRgb(Color a, Color b)
        => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

    private static int _hsFrame = -1;
    private static bool _hsCached;

    private static bool InHideAndSeek()
    {
        int frame = Time.frameCount;
        if (frame == _hsFrame) return _hsCached;
        _hsFrame = frame;
        try
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.IsHideAndSeek()) { _hsCached = true; return true; }

            GameOptionsManager gom = GameOptionsManager.Instance;
            var opts = gom != null ? gom.CurrentGameOptions : null;
            _hsCached = opts != null && (opts.GameMode is GameModes.HideNSeek or GameModes.SeekFools);
        }
        catch { _hsCached = false; }
        return _hsCached;
    }

    private static string UnwrapColor(string s)
    {
        const string Close = "</color>";
        if (string.IsNullOrEmpty(s)) return s;

        for (int guard = 0; guard < 4; guard++)
        {
            if (!s.StartsWith("<color=", StringComparison.Ordinal)) break;
            int gt = s.IndexOf('>');
            if (gt < 0 || !s.EndsWith(Close, StringComparison.Ordinal)) break;
            s = s.Substring(gt + 1, s.Length - gt - 1 - Close.Length);
        }
        return s;
    }

    private static ShapeshifterMinigame _shifterMenu;

    internal static void SetShifterMenu(ShapeshifterMinigame menu) => _shifterMenu = menu;

    private static ShapeshifterMinigame CurrentShifterMenu()
    {
        ShapeshifterMinigame cached = _shifterMenu;
        if (cached == null) return null;                 
        try
        {
            if (cached.gameObject.activeInHierarchy) return cached;
        }
        catch { }
        _shifterMenu = null;                              
        return null;
    }

    private static void ColorShifterMenu()
    {
        try
        {
            ShapeshifterMinigame menu = CurrentShifterMenu();
            if (menu == null || menu.potentialVictims == null) return;

            var list = AmethystPlayerCacheStore.All;
            if (list.Count == 0) return;

            for (int i = 0; i < menu.potentialVictims.Count; i++)
            {
                ShapeshifterPanel panel = menu.potentialVictims[i];
                if (panel == null || panel.NameText == null) continue;

                string shown = panel.NameText.text;
                if (string.IsNullOrEmpty(shown)) continue;

                for (int j = 0; j < list.Count; j++)
                {
                    AmethystPlayerCache c = list[j];
                    if (c == null || string.IsNullOrEmpty(c.Name)) continue;
                    if (shown.IndexOf(c.Name, StringComparison.Ordinal) < 0) continue;

                    if (c.IsShapeshifting || !c.OutfitIsDefault) break;

                    string hex = c.Id == AmethystPlayerCacheStore.LocalId
                        ? AmethystColors.Hex(c.ShownColorId)
                        : (AmethystPlayerCacheStore.LocalIsImp && c.IsImpostor && c.Id != AmethystPlayerCacheStore.LocalId
                            ? "#FF0000"
                            : AmethystColors.Hex(c.ColorId));
                    if (string.IsNullOrEmpty(hex)) break;

                    string want = $"<color={hex}>{c.Name}</color>";
                    if (shown != want) panel.NameText.text = want;
                    break;
                }
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ShapeshifterMinigame), "Begin")]
internal static class AmethystShifterMenuCachePatch
{
    private static void Postfix(ShapeshifterMinigame __instance)
        => AmethystNameRendering.SetShifterMenu(__instance);
}
