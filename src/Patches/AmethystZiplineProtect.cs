using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystZiplineProtect
{
    private static float _okUntil;
    private static float _lastToast;

    internal static bool On => AmethystConfig.ZiplineProtect != null && AmethystConfig.ZiplineProtect.Value;

    internal static void MarkOk()
    {
        _okUntil = Time.unscaledTime + 1.5f;
    }

    internal static bool IsForced(PlayerControl pc)
    {
        
        if (!AmethystColors.SamePlayer(pc, PlayerControl.LocalPlayer)) return false;
        if (Time.unscaledTime < _okUntil) return false;
        if (Time.unscaledTime - _lastToast >= 2f)
        {
            _lastToast = Time.unscaledTime;
            string who = AmethystColors.Name(pc) ?? Name(pc);
            AmethystToast.Push(AmethystText.T("Guard"),
                AmethystText.T("AbnormalRPCForcedZipline", who),
                5.0f, AmethystNotifyKind.Warning);
        }
        return true;
    }

    private static string Name(PlayerControl pc)
        => pc != null && pc.Data != null && !string.IsNullOrWhiteSpace(pc.Data.PlayerName) ? pc.Data.PlayerName : AmethystText.T("Player");
}

[HarmonyPatch(typeof(PlayerControl), "CmdCheckUseZipline")]
internal static class AmethystZiplineCmdPatch
{
    private static void Prefix(PlayerControl __instance)
    {
        if (AmethystZiplineProtect.On && __instance == PlayerControl.LocalPlayer) AmethystZiplineProtect.MarkOk();
    }
}

[HarmonyPatch(typeof(PlayerControl), "RpcUseZipline")]
internal static class AmethystZiplineUsePatch
{
    private static void Prefix(PlayerControl __instance)
    {
        if (AmethystZiplineProtect.On && __instance == PlayerControl.LocalPlayer) AmethystZiplineProtect.MarkOk();
    }
}

[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class AmethystZiplineRpcPatch
{
    private static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId)
    {
        try
        {
            if (!AmethystZiplineProtect.On || callId != (byte)RpcCalls.UseZipline) return true;
            return !AmethystZiplineProtect.IsForced(__instance);
        }
        catch { return true; }
    }
}
