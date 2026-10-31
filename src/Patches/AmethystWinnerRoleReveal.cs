using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystWinnerRoleReveal
{
    internal static bool Enabled => AmethystConfig.WinnerRoleReveal != null && AmethystConfig.WinnerRoleReveal.Value;

    private const int MaxWaitFrames = 30;

    internal static IEnumerator CoReveal(EndGameManager egm)
    {
        yield return null;
        if (egm == null || !Enabled) yield break;

        var winners = EndGameResult.CachedWinners;
        if (winners == null || winners.Count == 0) yield break;

        PoolablePlayer[] players = null;
        for (int frame = 0; frame < MaxWaitFrames; frame++)
        {
            players = GetPlayers(egm);
            if (players != null && players.Length >= winners.Count) break;
            yield return null;
        }

        players = GetPlayers(egm);
        if (players == null || players.Length == 0) yield break;

        try { ApplyAll(players, winners); } catch { }
    }

    private static PoolablePlayer[] GetPlayers(EndGameManager egm)
    {
        try { return egm != null ? egm.GetComponentsInChildren<PoolablePlayer>() : null; }
        catch { return null; }
    }

    private static void ApplyAll(PoolablePlayer[] players, Il2CppSystem.Collections.Generic.List<CachedPlayerData> winners)
    {
        var usedPlayers = new HashSet<int>();
        for (int i = 0; i < winners.Count; i++)
        {
            PoolablePlayer player = FindPlayer(players, winners, i, usedPlayers);
            if (player == null || player.cosmetics == null || player.cosmetics.nameText == null) continue;
            if (player.transform.Find("AmethystWinnerLabel") != null) continue;

            CachedPlayerData data = winners[i];
            var source = player.cosmetics.nameText;

            var used = new HashSet<byte>();
            string role = AmethystRecap.WinnerRole(data.PlayerName, data.Outfit.ColorId, used);

            int colorId = data.Outfit != null ? data.Outfit.ColorId : -1;
            string coloredName = AmethystColors.Name(data.PlayerName, colorId);

            var label = Object.Instantiate(source, player.transform);
            label.name = "AmethystWinnerLabel";
            label.gameObject.SetActive(true);
            label.enabled = true;
            label.text = coloredName + (string.IsNullOrEmpty(role) ? string.Empty : "\n" + role);
            label.color = Color.white;
            label.alpha = 1f;
            label.richText = true;
            label.alignment = TextAlignmentOptions.Top;

            var body = player.cosmetics.currentBodySprite.BodySprite;
            Bounds bounds = body.bounds;
            label.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.08f, source.transform.position.z);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);

            source.gameObject.SetActive(false);
        }
    }

    private static PoolablePlayer FindPlayer(PoolablePlayer[] players, Il2CppSystem.Collections.Generic.List<CachedPlayerData> winners, int index, HashSet<int> used)
    {
        CachedPlayerData data = winners[index];

        int wantColor = -1;
        try { if (data.Outfit != null) wantColor = data.Outfit.ColorId; } catch { }

        if (wantColor >= 0)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (used.Contains(i)) continue;
                if (AvatarColor(players[i]) == wantColor) { used.Add(i); return players[i]; }
            }
        }

        string want = Plain(data.PlayerName);
        for (int i = 0; i < players.Length; i++)
        {
            if (used.Contains(i)) continue;
            if (Plain(NameOf(players[i])) == want) { used.Add(i); return players[i]; }
        }

        if (index < players.Length && !used.Contains(index))
        {
            used.Add(index);
            return players[index];
        }
        return null;
    }

    private static int AvatarColor(PoolablePlayer player)
    {
        try
        {
            if (player == null || player.cosmetics == null) return -1;
            return player.cosmetics.bodyMatProperties.ColorId;
        }
        catch { return -1; }
    }

    private static string NameOf(PoolablePlayer player)
    {
        try
        {
            if (player == null || player.cosmetics == null || player.cosmetics.nameText == null) return string.Empty;
            return player.cosmetics.nameText.text ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string Plain(string text) => System.Text.RegularExpressions.Regex.Replace(text ?? string.Empty, "<[^>]*>", string.Empty).Trim();
}

[HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
internal static class AmethystWinnerRolePatch
{
    private static void Postfix(EndGameManager __instance)
    {
        if (!AmethystWinnerRoleReveal.Enabled || __instance == null) return;
        __instance.StartCoroutine(AmethystWinnerRoleReveal.CoReveal(__instance).WrapToIl2Cpp());
    }
}
