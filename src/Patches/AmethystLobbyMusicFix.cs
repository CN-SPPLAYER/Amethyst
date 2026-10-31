using System;
using AmongUs.Data;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystLobbyMusicFix
{
    internal static bool Enabled => AmethystConfig.FixLobbyMusic != null && AmethystConfig.FixLobbyMusic.Value;

    internal static void Restore()
    {
        if (!Enabled) return;
        try
        {
            SoundManager sm = SoundManager.Instance;
            if (sm == null) return;

            float volume = 0.5f;
            try
            {
                if (DataManager.Settings != null && DataManager.Settings.Audio != null)
                    volume = DataManager.Settings.Audio.AmbienceVolume;
            }
            catch { }

            sm.ChangeAmbienceVolume(volume);
        }
        catch { }
    }

    internal static void EnsureLobbyTheme(LobbyBehaviour lobby)
    {
        if (!Enabled || lobby == null) return;

        if (AmethystConfig.MuteLobbyMusic != null && AmethystConfig.MuteLobbyMusic.Value)
        {
            MuteNow(lobby);
            return;
        }

        if (AmethystGameMusicMute.WantMute) return;

        try
        {
            SoundManager sm = SoundManager.Instance;
            if (sm == null || sm.soundPlayers == null) return;

            bool playing = false;
            var e = sm.soundPlayers.GetEnumerator();
            while (e.MoveNext())
            {
                ISoundPlayer sp = e.Current;
                if (sp != null && string.Equals(sp.Name, "MapTheme", StringComparison.Ordinal)) { playing = true; break; }
            }

            if (!playing) sm.CrossFadeSound("MapTheme", lobby.MapTheme, 0.5f);
        }
        catch { }
    }

    internal static void MuteNow(LobbyBehaviour lobby)
    {
        try
        {
            SoundManager sm = SoundManager.Instance;
            if (sm == null) return;
            sm.StopNamedSound("MapTheme");
            if (lobby != null && lobby.MapTheme != null)
            {
                sm.StopSound(lobby.MapTheme);
            }
        }
        catch { }
    }

    internal static void UnmuteNow(LobbyBehaviour lobby)
    {
        try
        {
            if (lobby == null || lobby.MapTheme == null) return;
            SoundManager sm = SoundManager.Instance;
            if (sm == null) return;

            sm.CrossFadeSound("MapTheme", lobby.MapTheme, 0.5f, 1.5f);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), "Start")]
internal static class AmethystLobbyMusicStartPatch
{
    private static void Postfix(LobbyBehaviour __instance)
    {
        if (AmethystConfig.MuteLobbyMusic != null && AmethystConfig.MuteLobbyMusic.Value)
        {
            AmethystLobbyMusicFix.MuteNow(__instance);
            return;
        }
        AmethystLobbyMusicFix.Restore();
        AmethystLobbyMusicFix.EnsureLobbyTheme(__instance);
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), "Update")]
internal static class AmethystLobbyMusicUpdatePatch
{
    private static float _nextCheck;

    private static void Postfix(LobbyBehaviour __instance)
    {
        if (AmethystConfig.MuteLobbyMusic == null || !AmethystConfig.MuteLobbyMusic.Value) return;
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.5f;

        try
        {
            AmethystLobbyMusicFix.MuteNow(__instance);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ExileController), "WrapUp")]
internal static class AmethystLobbyMusicExilePatch
{
    private static void Postfix() => AmethystLobbyMusicFix.Restore();
}

[HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
internal static class AmethystLobbyMusicEndPatch
{
    private static void Postfix() => AmethystLobbyMusicFix.Restore();
}
