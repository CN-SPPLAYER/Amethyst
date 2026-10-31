using BepInEx.Configuration;
using HarmonyLib;
using AmongUs.Data;

namespace Amethyst.Patches;

internal static class AmethystAccountUnlock
{
    internal static bool FreeChatEnabled => On(AmethystConfig.UnlockFreeChat);
    internal static bool ForceAdultEnabled => On(AmethystConfig.ForceAdult);

    private static bool On(ConfigEntry<bool> e)
    {
        try { return e == null || e.Value; } catch { return false; }
    }

    private static float _nextTick;

    internal static void Tick()
    {
        if (!ForceAdultEnabled) return;
        try
        {
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextTick) return;
            _nextTick = now + 0.25f;

            EOSManager eos = EOSManager.Instance;
            if (eos == null) return;
            if (eos.isKWSMinor) eos.isKWSMinor = false;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(EOSManager), "IsFreechatAllowed")]
internal static class AmethystUnlockFreeChatPatch
{
    private static void Postfix(ref bool __result)
    {
        if (!AmethystAccountUnlock.FreeChatEnabled) return;
        if (!__result) __result = true;
    }
}
