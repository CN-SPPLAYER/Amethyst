using System;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystMushroomMixup
{
    private static bool _hidden;

    internal static bool IsActive()
    {
        try
        {
            ShipStatus ship = ShipStatus.Instance;
            if (ship == null || ship.Systems == null) return false;
            if (ship.Systems.TryGetValue(SystemTypes.MushroomMixupSabotage, out var m) && m != null)
            {
                var s = m.TryCast<MushroomMixupSabotageSystem>();
                if (s != null && s.IsActive) return true;
            }
        }
        catch { }
        return false;
    }

    private static bool _activeCached;
    private static int _activeFrame = -1;

    internal static bool ActiveNow()
    {
        int frame = Time.frameCount;
        if (frame == _activeFrame) return _activeCached;
        _activeFrame = frame;
        _activeCached = IsActive();
        return _activeCached;
    }

    private static bool Alive(PlayerControl pc)
    {
        try { return pc != null && pc.Data != null && !pc.Data.IsDead; } catch { return false; }
    }

    internal static void HideName(PlayerControl pc)
    {
        if (!AmethystColors.NameColorsOn || !Alive(pc) || pc.cosmetics == null) return;
        try { pc.cosmetics.ToggleNameVisible(false); } catch { }
    }

    internal static void ShowName(PlayerControl pc)
    {
        if (!AmethystColors.NameColorsOn || !Alive(pc) || pc.cosmetics == null) return;
        try { pc.cosmetics.ToggleNameVisible(true); } catch { }
    }

    private static int _lastApplyFrame = -999;

    internal static void Tick()
    {
        if (!AmethystColors.NameColorsOn) { _hidden = false; return; }

        bool active = ActiveNow();
        if (!active)
        {
            if (_hidden)
            {
                Apply(false);      
                _hidden = false;
                _lastApplyFrame = -999;
            }
            return;
        }

        int frame = Time.frameCount;
        bool first = !_hidden;
        if (first || frame - _lastApplyFrame >= 20)
        {
            _lastApplyFrame = frame;
            Apply(true);
            _hidden = true;
        }
    }

    private static void Apply(bool hide)
    {
        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl pc = all[i];
                if (pc == null || !Alive(pc) || pc.cosmetics == null) continue;

                bool visible;
                try 
                { 
                    if (pc.gameObject == null || pc.cosmetics.gameObject == null) continue;
                    visible = pc.cosmetics.isNameVisible; 
                } 
                catch { continue; }

                if (hide && visible) HideName(pc);
                else if (!hide && !visible) ShowName(pc);
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerControl), "MixUpOutfit")]
internal static class AmethystMixUpOutfitPatch
{
    private static void Postfix(PlayerControl __instance) => AmethystMushroomMixup.HideName(__instance);
}

[HarmonyPatch(typeof(PlayerControl), "FixMixedUpOutfit")]
internal static class AmethystFixMixedUpOutfitPatch
{
    private static void Postfix(PlayerControl __instance) => AmethystMushroomMixup.ShowName(__instance);
}
