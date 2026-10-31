using HarmonyLib;
using InnerNet;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(PlayerControl), "MurderPlayer")]
internal static class AmethystRecapKillPatch
{
    private static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
    {
        try
        {
            if (__instance == null || target == null) return;
            if ((resultFlags & MurderResultFlags.FailedError) != 0) return;
            if ((resultFlags & MurderResultFlags.FailedProtected) != 0) return;
            if (__instance.PlayerId == target.PlayerId) return;

            AmethystRecap.NoteKill(__instance.PlayerId, target.PlayerId);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ExileController), "Begin")]
internal static class AmethystRecapExilePatch
{
    private static void Postfix(ExileController __instance)
    {
        try
        {
            if (__instance == null) return;
            NetworkedPlayerInfo info = __instance.initData != null ? __instance.initData.networkedPlayer : null;
            if (info == null) return;

            AmethystRecap.NoteExile(info.PlayerId);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(MeetingHud), "SetJudgeOverrule")]
internal static class AmethystRecapJudgePatch
{
    private static void Postfix(PlayerId judgePlayerId, PlayerId targetPlayerId)
    {
        try
        {
            AmethystRecap.NoteJudge(judgePlayerId.Value, targetPlayerId.Value);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(GameData), "HandleDisconnect", new[] { typeof(PlayerControl), typeof(DisconnectReasons) })]
internal static class AmethystRecapDisconnectPatch
{
    private static void Prefix(PlayerControl player)
    {
        try
        {
            
            if (LobbyBehaviour.Instance != null) return;
            if (player == null || player.Data == null) return;

            AmethystRecap.NoteDisconnect(player.Data);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
internal static class AmethystRecapEndGamePatch
{
    private static void Postfix(EndGameManager __instance)
    {
        try
        {
            AmethystRecap.Sync();
            AmethystRecap.CaptureResult();
            AmethystRecap.Freeze();
            AmethystRecap.EndScreen = __instance;
            AmethystRecapWindow.Hide();
        }
        catch { }
    }
}

[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystRecapResetPatch
{
    private static void Prefix()
    {
        AmethystRecap.Reset();
    }
}
