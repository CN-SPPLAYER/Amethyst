using System;
using AmongUs.Data;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 第 207 轮（从 EHR 提取）：大厅环境音量修复。
//
// 病根：会议开始 / 对局结束时，游戏会把环境音量压到 0（`ChangeAmbienceVolume(0f)`），
// 之后并不总会还原成玩家设置里的 `DataManager.Settings.Audio.AmbienceVolume`，
// 表现就是「开会 / 打完一局回到大厅后，背景音乐变得很小」。
//
// 参考 EHR：它在 `ExilePatch.WrapUpFinalizer` / `OnGameJoinedPatch` 里**强制**把
// 环境音量写回玩家设置值，并在 `LobbyBehaviour` 里发现大厅主题曲没在播时强制
// `CrossFadeSound("MapTheme", ...)` 补回来。这里同样强制还原音量 + 补播大厅主题曲。
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

    // 强制补播大厅主题曲（原版有时会把它停掉 / 不恢复）。
    internal static void EnsureLobbyTheme(LobbyBehaviour lobby)
    {
        if (!Enabled || lobby == null) return;

        // ❌ 新功能支持：如果开启了“关闭大厅音乐” (MuteLobbyMusic)，在这里直接阻止其播放并停掉音乐
        if (AmethystConfig.MuteLobbyMusic != null && AmethystConfig.MuteLobbyMusic.Value)
        {
            MuteNow(lobby);
            return;
        }

        // ⚠ 音乐播放器正在出声时也不要补播：否则「本模组刚把 MapTheme 静音 →
        //   这里又把它 CrossFade 补回来」，两者每帧互搏，表现为大厅音乐忽大忽小。
        //   播放器停掉后 AmethystGameMusicMute 会恢复原音量，这里下次进大厅再补播即可。
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

    // 强力立即停掉大厅音乐
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

    // 强力立即恢复播放大厅音乐（配合 UI 按钮切换时的实时反馈）
    internal static void UnmuteNow(LobbyBehaviour lobby)
    {
        try
        {
            if (lobby == null || lobby.MapTheme == null) return;
            SoundManager sm = SoundManager.Instance;
            if (sm == null) return;

            // 补播大厅主题曲
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

// 每帧兜底拦截：在游戏逻辑循环中，如果该功能被开启，每秒检查并阻止大厅环境音 MapTheme 播放
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

