using System;
using System.Linq;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

[HarmonyPatch]
internal static class AmethystLobbySearchPatches
{
    public static Scroller Scroller;

    [HarmonyPatch(typeof(FindAGameManager), "Start")]
    [HarmonyPrefix]
    private static void FindAGameManager_Start_Prefix(FindAGameManager __instance)
    {
        if (AmethystConfig.ExpandLobbySearch == null || !AmethystConfig.ExpandLobbySearch.Value) return; 

        try
        {
            
            if (__instance.clearFilterButton != null) __instance.clearFilterButton.gameObject.SetUIColors("Disabled");
            if (__instance.serverButton != null) 
            {
                __instance.serverButton.gameObject.SetUIColors("Inactive", "Disabled", "Background");
                __instance.serverButton.activeTextColor = AmethystFindGameUiColors.Theme * 0.3f;   
            }

            if (__instance.gameContainers == null || __instance.gameContainers.Length < 5) return; 

            foreach (var con in __instance.gameContainers)
            {
                if (con == null) continue; 
                var roll = con.GetComponent<ButtonRolloverHandler>();
                if (roll == null) continue; 
                
                roll.OverColor = AmethystFindGameUiColors.Theme;
            }

            var prefab = __instance.gameContainers[4];
            var list = new GameObject("GameListScroller");
            list.transform.SetParent(prefab.transform.parent);

            Scroller = list.AddComponent<Scroller>();
            Scroller.Inner = list.transform;
            Scroller.MouseMustBeOverToScroll = true;
            var box = prefab.transform.parent.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(100f, 100f);
            Scroller.ClickMask = box;
            Scroller.ScrollWheelSpeed = 0.3f;
            Scroller.SetYBoundsMin(0f);
            Scroller.SetYBoundsMax(3.5f);
            Scroller.allowY = true;

            foreach (var con in __instance.gameContainers)
            {
                if (con == null) continue; 
                con.transform.SetParent(list.transform);
                var oldPos = con.transform.position;
                con.transform.position = new Vector3(oldPos.x, oldPos.y, 25);
            }

            var oldGameContainers = __instance.gameContainers.ToList();

            for (int i = 0; i < 5; i++)
            {
                var GameContainer = UnityEngine.Object.Instantiate(prefab, list.transform);
                if (GameContainer == null) continue; 
                var oldPos = GameContainer.transform.position;
                GameContainer.transform.position = new Vector3(oldPos.x, oldPos.y - 0.75f * (i + 1), 25);
                oldGameContainers.Add(GameContainer);
            }

            __instance.gameContainers = oldGameContainers.ToArray();

            var cutOffTop = CreateBlackSquareSprite();
            cutOffTop.transform.SetParent(list.transform.parent);
            cutOffTop.transform.localPosition = new Vector3(0, 3, 1);
            cutOffTop.transform.localScale = new Vector3(1500, 200, 100);
        }
        catch { }
    }

    [HarmonyPatch(typeof(FindAGameManager), "Start")]
    [HarmonyPostfix]
    private static void ApplyNavigationColors(FindAGameManager __instance)
    {
        if (AmethystConfig.ExpandLobbySearch == null || !AmethystConfig.ExpandLobbySearch.Value) return;
        AmethystFindGameUiColors.StyleNavigationButton(__instance.refreshButton);
        if (__instance.BackButton != null)
            AmethystFindGameUiColors.StyleNavigationButton(__instance.BackButton.GetComponent<PassiveButton>());
    }

    [HarmonyPatch(typeof(FindAGameManager), "RefreshList")]
    [HarmonyPostfix]
    private static void FindAGameManager_RefreshList_Postfix(FindAGameManager __instance)
    {
        
        Scroller?.ScrollRelative(new(0f, -100f));
    }

    [HarmonyPatch(typeof(FindAGameManager), "HandleList")]
    [HarmonyPostfix]
    private static void FindAGameManager_HandleList_Postfix(FindAGameManager __instance, HttpMatchmakerManager.FindGamesListFilteredResponse response)
    {
        if (__instance == null || __instance.gameContainers == null || response == null) return; 
        if (AmethystConfig.ExpandLobbySearch == null || !AmethystConfig.ExpandLobbySearch.Value) return; 

        try
        {
            
            __instance.ResetContainers();
            GameListing[] games = response.Games.ToArray();

            if (AmethystConfig.RoomSortByMaxPlayers == null || AmethystConfig.RoomSortByMaxPlayers.Value) 
            {
                
                games = [.. games.Where(g => g != null).OrderByDescending(game => game.PlayerCount).ThenBy(game => game.TrueHostName)];
            }

            int gameNum = 0;
            int count = 0;

            while (count < __instance.gameContainers.Length && count < games.Count())
            {
                if (games[count].Options != null)
                {
                    var con = __instance.gameContainers[gameNum];
                    if (con == null) break; 
                    con.gameObject.SetActive(true);
                    con.SetGameListing(games[count]);
                    con.SetupGameInfo();
                    gameNum++;
                }
                count++;
            }

            foreach (var container in __instance.gameContainers)
            {
                if (container == null) continue; 
                WriteHostInfo(container);
            }
        }
        catch { }
    }

    internal static void WriteHostInfo(GameContainer container)
    {
        if (container == null) return;
        if (AmethystConfig.RoomInfoDisplay != null && !AmethystConfig.RoomInfoDisplay.Value) return;

        try
        {
            Transform child = container.transform.Find("Container");
            if (child == null) return;

            Transform tmproObject = child.Find("TrueHostName_TMP");
            TMP_Text tmpro = tmproObject != null ? tmproObject.GetComponent<TextMeshPro>() : null;
            if (tmpro == null) tmpro = CreateNewTextMeshPro(child);
            if (tmpro == null) return;

            tmpro.font = container.capacity != null ? container.capacity.font : tmpro.font;
            tmpro.fontSize = 3f;
            tmpro.text = FormatGameInfoText(container.gameListing);
            tmpro.gameObject.SetActive(true);
        }
        catch { }
    }

    private static TMP_Text CreateNewTextMeshPro(Transform parent)
    {
        
        var tmproObject = new GameObject("TrueHostName_TMP").transform;
        tmproObject.SetParent(parent, true);

        var aspectPos = tmproObject.gameObject.AddComponent<AspectPosition>();
        aspectPos.Alignment = AspectPosition.EdgeAlignments.Center;
        aspectPos.anchorPoint = new Vector2(0.2f, 0.5f);
        aspectPos.DistanceFromEdge = new Vector3(10.9f, -2.17f, -2f);
        aspectPos.AdjustPosition();

        return tmproObject.gameObject.AddComponent<TextMeshPro>();
    }

    private static string FormatGameInfoText(GameListing listing)
    {
        
        if (listing == null) return string.Empty;
        string hostStr = !string.IsNullOrEmpty(listing.TrueHostName) ? listing.TrueHostName : listing.HostName;
        if (string.IsNullOrWhiteSpace(hostStr)) hostStr = "Unknown";
        hostStr = hostStr.Replace("<", "\u2039").Replace(">", "\u203A");

        string platform = GetPlatformName(listing.Platform);
        if (string.IsNullOrEmpty(platform)) platform = "Unknown";

        string code;
        try { code = GameCode.IntToGameName(listing.GameId); } catch { code = string.Empty; }
        if (string.IsNullOrWhiteSpace(code)) code = "Unknown";

        return $"{hostStr}{Environment.NewLine}<size=65%>{platform} ({code})";
    }

    private static SpriteRenderer CreateBlackSquareSprite()
    {
        
        var square = new GameObject("CutOffTop");
        var renderer = square.AddComponent<SpriteRenderer>();
        Texture2D texture = new(100, 100);
        Color[] pixels = texture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.black;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        renderer.sprite = sprite;
        square.transform.localScale = new Vector3(100, 100, 1);
        return renderer;
    }

    private static string GetPlatformName(Platforms platform)
    {
        return platform switch
        {
            Platforms.StandaloneSteamPC => "Steam",
            Platforms.StandaloneEpicPC => "Epic Games",
            Platforms.StandaloneWin10 => "Microsoft Store",
            Platforms.StandaloneMac => "Mac OS",
            Platforms.StandaloneItch => "Itch.io",
            Platforms.Xbox => "Xbox",
            Platforms.Playstation => "Playstation",
            Platforms.Switch => "Switch",
            Platforms.Android => "Android",
            Platforms.IPhone => "IPhone",
            Platforms.Unknown => "None",
            _ => string.Empty,
        };
    }
}

[HarmonyPatch(typeof(GameContainer), "SetupGameInfo")]
internal static class AmethystGameContainerInfoPatch
{
    private static void Postfix(GameContainer __instance)
    {
        AmethystLobbySearchPatches.WriteHostInfo(__instance);
    }
}

internal static class AmethystFindGameUiColors
{
    internal static void StyleNavigationButton(PassiveButton button)
    {
        if (button == null) return;
        foreach (var sprite in button.GetComponentsInChildren<SpriteRenderer>(true))
            AmethystUiSpriteTone.Apply(sprite);
        Color normal = Color.Lerp(Theme, Color.black, 0.25f);
        Color hover = Color.Lerp(Theme, Color.white, 0.2f);
        Color disabled = new Color(0.32f, 0.30f, 0.36f, 1f);
        button.gameObject.SetUIColors(normal);
        if (button.activeSprites != null) button.activeSprites.SetUIColors(hover);
        if (button.selectedSprites != null) button.selectedSprites.SetUIColors(hover);
        if (button.onClickSprites != null) button.onClickSprites.SetUIColors(Theme);
        if (button.disabledSprites != null) button.disabledSprites.SetUIColors(disabled);

        foreach (var roll in button.GetComponentsInChildren<ButtonRolloverHandler>(true))
        {
            roll.UseObjectsOutColor = false;
            roll.OutColor = normal;
            roll.OverColor = hover;
            roll.UnselectedColor = disabled;
            if (roll.Target != null) roll.Target.color = normal;
            if (roll.Target2 != null) roll.Target2.color = normal;
        }
        button.activeTextColor = Color.white;
        button.inactiveTextColor = Color.white;
        button.selectedTextColor = Color.white;
        button.selectedInactiveTextColor = Color.white;
        button.disabledTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        if (button.buttonText != null) button.buttonText.color = Color.white;
    }

    internal static Color Theme
    {
        get { try { return AmethystStyle.Current.Accent; } catch { return Color.white; } }
    }

    internal static void SetUIColors(this GameObject go, params string[] avoidGoName)
    {
        go.SetUIColors(null, null, avoidGoName);
    }

    internal static void SetUIColors(this GameObject go, Func<SpriteRenderer, bool> check = null, params string[] avoidGoName)
    {
        go.SetUIColors(null, check, avoidGoName);
    }

    internal static void SetUIColors(this GameObject go, Color? color = null, Func<SpriteRenderer, bool> check = null, params string[] avoidGoName)
    {
        if (go == null) return; 
        var sprites = go.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sprite in sprites)
        {
            if (sprite == null) continue; 
            if (avoidGoName.Any(name => sprite.gameObject.name == name)) continue;
            if (check == null || check(sprite))
                AddColor(sprite, color);
        }
    }

    internal static void AddColor(SpriteRenderer sprite, Color? color = null)
    {
        if (sprite == null) return; 

        Color c = color ?? Theme;
        sprite.color = new Color(c.r, c.g, c.b, sprite.color.a);
    }
}
