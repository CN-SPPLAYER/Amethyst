using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystProtToggles
{
    private static int _frame = -1;

    internal static bool Dtls;
    internal static bool BlockTeleports;
    internal static bool BlockUnauthorized;
    internal static bool HardenPackedUInt;
    internal static bool AntiOverload;
    internal static bool AntiKick;
    internal static bool HostOnlyRpcKick;

    internal static void Refresh()
    {
        int f = Time.frameCount;
        if (f == _frame) return;
        _frame = f;

        Dtls = On(AmethystConfig.ForceDtls);
        BlockTeleports = On(AmethystConfig.BlockServerTeleports);
        BlockUnauthorized = On(AmethystConfig.BlockUnauthorizedUpdates);
        HardenPackedUInt = On(AmethystConfig.HardenPackedUInt);
        AntiOverload = On(AmethystConfig.AntiOverloadEnabled);
        AntiKick = On(AmethystConfig.AntiKickEnabled);
        HostOnlyRpcKick = On(AmethystConfig.HostOnlyRpcKickGuard);
    }

    private static bool On(BepInEx.Configuration.ConfigEntry<bool> e)
        => e == null || e.Value;
}

[HarmonyPatch(typeof(InnerNetClient), "SetEndpoint")]
internal static class AmethystForceDtlsPatch
{
    private static void Prefix(ref bool dtls)
    {
        try
        {
            AmethystProtToggles.Refresh();
            if (AmethystProtToggles.Dtls) dtls = true;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(CustomNetworkTransform), "HandleRpc")]
internal static class AmethystBlockServerTeleportsPatch
{
    private static bool Prefix(CustomNetworkTransform __instance, byte callId)
    {
        try
        {
            if (callId != (byte)RpcCalls.SnapTo) return true;

            AmethystProtToggles.Refresh();
            if (!AmethystProtToggles.BlockTeleports) return true;

            return __instance == null || __instance.myPlayer != PlayerControl.LocalPlayer;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystBlockUnauthorizedUpdatesPatch
{
    private static bool Prefix(byte callId)
    {
        try
        {
            if (callId != (byte)RpcCalls.UpdateSystem && callId != (byte)RpcCalls.CloseDoorsOfType) return true;

            AmethystProtToggles.Refresh();
            if (!AmethystProtToggles.BlockUnauthorized) return true;

            return AmongUsClient.Instance == null || AmongUsClient.Instance.AmHost;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(MessageReader), "ReadPackedUInt32")]
internal static class AmethystHardenPackedUIntPatch
{
    private static bool Prefix(MessageReader __instance, ref uint __result)
    {
        try
        {
            AmethystProtToggles.Refresh();
            if (!AmethystProtToggles.AntiOverload || !AmethystProtToggles.HardenPackedUInt) return true;
        }
        catch { return true; }

        bool readMore = true;
        int shift = 0;
        uint output = 0;

        try
        {
            while (readMore)
            {
                if (__instance.BytesRemaining < 1) break;

                byte b = __instance.ReadByte();
                if (b >= 0x80)
                {
                    readMore = true;
                    b ^= 0x80;
                }
                else
                {
                    readMore = false;
                }

                if (shift < 32) output |= (uint)b << shift;
                shift += 7;
            }
        }
        catch
        {
            __result = 0;
            return false;
        }

        __result = output;
        return false;
    }
}

internal static class AmethystCrashGuardState
{
    private static bool _loading;

    internal static bool Enabled
        => AmethystConfig.ReportDeadBodyCrashGuard == null || AmethystConfig.ReportDeadBodyCrashGuard.Value;

    internal static void MarkLoading() => _loading = true;
    internal static void MarkLoaded() => _loading = false;
    internal static void Reset() => _loading = false;

    internal static bool ShouldBlock => Enabled && _loading;
}

[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystCrashGuardStartPatch
{
    private static void Prefix() => AmethystCrashGuardState.MarkLoading();
}

[HarmonyPatch(typeof(GameManager), "StartGame")]
internal static class AmethystCrashGuardLoadedPatch
{
    private static void Prefix() => AmethystCrashGuardState.MarkLoaded();
}

[HarmonyPatch(typeof(PlayerControl), "ReportDeadBody")]
internal static class AmethystCrashGuardReportPatch
{
    private static bool Prefix(PlayerControl __instance)
    {
        if (!AmethystCrashGuardState.ShouldBlock) return true;

        try
        {
            if (__instance != null && __instance != PlayerControl.LocalPlayer)
            {
                string name = AmethystColors.Name(__instance) ?? "?";
                AmethystSecurityNotify.Fire("TriggeredProtection", AmethystNotifyKind.Warning,
                    name, AmethystText.T("ReportDeadBodyCrashGuard"));
            }
        }
        catch { }

        AmethystPlugin.Logger?.LogWarning("[CrashGuard] blocked ReportDeadBody during match loading");
        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystCrashGuardResetPatch
{
    private static void Postfix() => AmethystCrashGuardState.Reset();
}

[HarmonyPatch(typeof(AmongUsClient), "OnGameEnd")]
internal static class AmethystCrashGuardEndPatch
{
    private static void Postfix() => AmethystCrashGuardState.Reset();
}
