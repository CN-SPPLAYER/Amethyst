using System;
using System.Linq;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

// ===== 「搜索游戏」房间列表：整体照抄 BetterAmongUs 1.3.4
//     src/Patches/Gameplay/Managers/FindAGameManagerClass/FindAGameManagerPatch.cs
//     src/Utilities/GameObjectUtils.cs（只用 SetUIColors / AddColor 两个扩展方法）
//
// 与 BAU 原文的差异**只有三类**，代码里每一处都标了 `← BAU 差异`：
//   ① 空引用保护。BAU 那几处 `transform.Find("Container")`、`container.gameListing`、
//      `GetComponent<ButtonRolloverHandler>()` 都没判空 —— 列表扩到 10 行之后必然有
//      "没填到数据"的行（`gameListing == null`），照抄必 NPE。
//   ② 取 `prefab = gameContainers[4]` 之前先判长度（BAU 是直接索引）。
//   ③ 三个 AMECE 自己的配置开关（ExpandLobbySearch / RoomSortByMaxPlayers / RoomInfoDisplay），
//      否则菜单里这三个开关会变成死选项。默认开启时行为与 BAU 一致。
//
// 除此之外**全部是 BAU 原值**，一个数都没改：
//   · 行 z 强行写成 25；克隆行世界坐标 `-0.75f * (i + 1)`；固定再克隆 5 个（共 10 行）
//   · `Scroller`：`MouseMustBeOverToScroll = true` / `allowY = true` / `ScrollWheelSpeed = 0.3`
//     / `SetYBoundsMin(0)` / `SetYBoundsMax(3.5)` / `ClickMask` = 面板上一个 100×100 的 BoxCollider2D
//   · 遮挡：`CutOffTop`，`localPosition(0,3,1)` + `localScale(1500,200,100)`（15×2 大黑块），
//     **不设 sortingOrder** —— BAU 就是靠"行 z=25 / 黑块 z=1"让黑块靠距离排序压住行
//   · 复位：`Scroller?.ScrollRelative(new(0f, -100f))`
//   · 行内信息：在 `Container` 下新建独立 TMP `TrueHostName_TMP`
//     （`AspectPosition` Center / anchorPoint(0.2,0.5) / DistanceFromEdge(10.9,-2.17,-2) / fontSize 3），
//     原版 `capacity` 的 "6/15" 那一行不动
//   · 按钮与行的染色：`SetUIColors` / `OverColor`，颜色用 Amethyst 主题色
//     （BAU 那边写死 `Color.green`）。⚠ BAU 的混合公式是**加法**，
//     `(sprite.color * 0.6f) + (color * 0.5f)` 在白底上必然发白 —— 换成紫色的结果是
//     (0.91, 0.82, 1.0) ≈ 还是白的。所以这里**直接赋主题色**，只沿用原 alpha。
//     主题色：`AmethystStyle.Current.Accent`（固定 #A06EFF）
[HarmonyPatch]
internal static class AmethystLobbySearchPatches
{
    public static Scroller Scroller;

    // ===== BAU 原文：src/Patches/Gameplay/Managers/FindAGameManagerClass/FindAGameManagerPatch.cs =====
    [HarmonyPatch(typeof(FindAGameManager), "Start")]
    [HarmonyPrefix]
    private static void FindAGameManager_Start_Prefix(FindAGameManager __instance)
    {
        if (AmethystConfig.ExpandLobbySearch == null || !AmethystConfig.ExpandLobbySearch.Value) return; // ← BAU 差异③

        try
        {
            // Apply custom UI colors to all Find Game menu buttons
            // 刷新、返回在 Start 后统一设置精灵和交互状态色。
            if (__instance.clearFilterButton != null) __instance.clearFilterButton.gameObject.SetUIColors("Disabled");
            if (__instance.serverButton != null) // ← BAU 差异①：BAU 四行都没判空
            {
                __instance.serverButton.gameObject.SetUIColors("Inactive", "Disabled", "Background");
                __instance.serverButton.activeTextColor = AmethystFindGameUiColors.Theme * 0.3f;   // BAU 原文是 Color.cyan * 0.3f
            }

            if (__instance.gameContainers == null || __instance.gameContainers.Length < 5) return; // ← BAU 差异②

            // Modify game container hover colors
            foreach (var con in __instance.gameContainers)
            {
                if (con == null) continue; // ← BAU 差异①
                var roll = con.GetComponent<ButtonRolloverHandler>();
                if (roll == null) continue; // ← BAU 差异①：BAU 下一行直接 roll.OverColor，会 NRE
                // BAU 原文是 (roll.OverColor * 0.6f) + (Color.green * 0.5f)，同样是加法混合
                // ⇒ 白底上会发白（见 AmethystFindGameUiColors.AddColor 的推导）。悬停色直接用主题色。
                roll.OverColor = AmethystFindGameUiColors.Theme;
            }

            // Create scrollable container for game listings
            var prefab = __instance.gameContainers[4];
            var list = new GameObject("GameListScroller");
            list.transform.SetParent(prefab.transform.parent);

            // Set up scroller component for vertical scrolling
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

            // Move existing game containers into scrollable list
            foreach (var con in __instance.gameContainers)
            {
                if (con == null) continue; // ← BAU 差异①
                con.transform.SetParent(list.transform);
                var oldPos = con.transform.position;
                con.transform.position = new Vector3(oldPos.x, oldPos.y, 25);
            }

            // Add more game containers to support longer server lists
            var oldGameContainers = __instance.gameContainers.ToList();

            for (int i = 0; i < 5; i++)
            {
                var GameContainer = UnityEngine.Object.Instantiate(prefab, list.transform);
                if (GameContainer == null) continue; // ← BAU 差异①
                var oldPos = GameContainer.transform.position;
                GameContainer.transform.position = new Vector3(oldPos.x, oldPos.y - 0.75f * (i + 1), 25);
                oldGameContainers.Add(GameContainer);
            }

            __instance.gameContainers = oldGameContainers.ToArray();

            // Create visual cutoff for scrolling content
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
        // Reset scroll position when refreshing game list
        Scroller?.ScrollRelative(new(0f, -100f));
    }

    [HarmonyPatch(typeof(FindAGameManager), "HandleList")]
    [HarmonyPostfix]
    private static void FindAGameManager_HandleList_Postfix(FindAGameManager __instance, HttpMatchmakerManager.FindGamesListFilteredResponse response)
    {
        if (__instance == null || __instance.gameContainers == null || response == null) return; // ← BAU 差异①
        if (AmethystConfig.ExpandLobbySearch == null || !AmethystConfig.ExpandLobbySearch.Value) return; // ← BAU 差异③

        try
        {
            // Clear existing containers before populating with new data
            __instance.ResetContainers();
            GameListing[] games = response.Games.ToArray();

            // Sort games: first by player count (highest first), then by host name
            if (AmethystConfig.RoomSortByMaxPlayers == null || AmethystConfig.RoomSortByMaxPlayers.Value) // ← BAU 差异③
            {
                // `Where(g => g != null)` 是 BAU 差异①：BAU 直接进 OrderByDescending，
                // 列表里混进一个 null 元素就会在取 PlayerCount 时 NRE。
                games = [.. games.Where(g => g != null).OrderByDescending(game => game.PlayerCount).ThenBy(game => game.TrueHostName)];
            }

            int gameNum = 0;
            int count = 0;

            // Populate visible containers with available games
            while (count < __instance.gameContainers.Length && count < games.Count())
            {
                if (games[count].Options != null)
                {
                    var con = __instance.gameContainers[gameNum];
                    if (con == null) break; // ← BAU 差异①：BAU 直接 con.gameObject
                    con.gameObject.SetActive(true);
                    con.SetGameListing(games[count]);
                    con.SetupGameInfo();
                    gameNum++;
                }
                count++;
            }

            // Add true host name and platform info to each game container.
            // 每行信息由 `GameContainer.SetupGameInfo` 的 Postfix 统一写入
            //（参考 AUnlocker / Elysium），这里只在填充后兜底跑一次。
            foreach (var container in __instance.gameContainers)
            {
                if (container == null) continue; // ← BAU 差异①
                WriteHostInfo(container);
            }
        }
        catch { }
    }

    // ===== 行内信息显示：完全照抄 BetterAmongUs 原版 =====
    //
    // BAU 的坐标是经过验证的正确值，直接使用：
    //   · 父节点：container.transform.Find("Container")（诊断日志确认它存在，@(1.09,2.66,0)）
    //   · 定位：AspectPosition.Alignment = Center
    //           anchorPoint = (0.2, 0.5)
    //           DistanceFromEdge = (10.9, -2.17, -2)
    //   · 字号：fontSize = 3f（BAU 原值）
    //   · 内容：{房主名}\n<size=65%>{平台} ({房号})
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
        // 完全照抄 BAU：新建 TMP 并用 AspectPosition 定位
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
        // 完全照抄 BAU
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
        // Create a black sprite to visually cut off scrolling content
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

    // 移植自 BetterAmongUs Utilities/Utils.GetPlatformName(Platforms)
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

// ===== 每行信息：挂在 `GameContainer.SetupGameInfo` 上 =====
//
// 参考 AUnlocker `GameContainer_SetupGameInfo` 与 Elysium `MoreLobbyInfoPatch`：
// 列表回调只在刷新时跑一次，而 `SetupGameInfo` 是**每一行被设置时**都会执行，
// 因此把信息挂在这里能保证所有房间行都拿到完整信息（主机名 / 平台 / 房号），
// 不会出现「部分房间信息不完整」。
[HarmonyPatch(typeof(GameContainer), "SetupGameInfo")]
internal static class AmethystGameContainerInfoPatch
{
    private static void Postfix(GameContainer __instance)
    {
        AmethystLobbySearchPatches.WriteHostInfo(__instance);
    }
}

// ===== 照抄 BetterAmongUs src/Utilities/GameObjectUtils.cs（只用这两个）
//
// BAU 版本里每次都先判 `BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_Theme)`
// —— 那是 BAU 和其它模组的握手标志，Amethyst 没有这套协议，去掉。
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

        // 染当前帧不够：鼠标移入/移出和刷新冷却结束会读取这些状态色。
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

    /// <summary>
    /// Amethyst 主题色（`AmethystStyle.Current.Accent`，固定 #A06EFF）。
    /// BAU 那边这个位置写死的是 `Color.green`（按钮/行悬停）和 `Color.cyan * 0.3f`（服务器按钮文字），
    /// 整套搬过来会变绿，和紫水晶主题冲突 —— 所以颜色改走主题色，结构一行不动。
    /// </summary>
    internal static Color Theme
    {
        get { try { return AmethystStyle.Current.Accent; } catch { return Color.white; } }
    }

    /// <summary>
    /// Sets UI colors for all SpriteRenderer components while avoiding specific GameObject names.
    /// </summary>
    internal static void SetUIColors(this GameObject go, params string[] avoidGoName)
    {
        go.SetUIColors(null, null, avoidGoName);
    }

    /// <summary>
    /// Sets UI colors for SpriteRenderer components with an optional check function.
    /// </summary>
    internal static void SetUIColors(this GameObject go, Func<SpriteRenderer, bool> check = null, params string[] avoidGoName)
    {
        go.SetUIColors(null, check, avoidGoName);
    }

    /// <summary>
    /// Sets UI colors for SpriteRenderer components with specific color and filtering options.
    /// </summary>
    internal static void SetUIColors(this GameObject go, Color? color = null, Func<SpriteRenderer, bool> check = null, params string[] avoidGoName)
    {
        if (go == null) return; // ← BAU 差异①
        var sprites = go.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sprite in sprites)
        {
            if (sprite == null) continue; // ← BAU 差异①
            if (avoidGoName.Any(name => sprite.gameObject.name == name)) continue;
            if (check == null || check(sprite))
                AddColor(sprite, color);
        }
    }

    /// <summary>
    /// Adds a color tint to a SpriteRenderer using a blend formula.
    /// </summary>
    internal static void AddColor(SpriteRenderer sprite, Color? color = null)
    {
        if (sprite == null) return; // ← BAU 差异①

        // BAU 原文（加法混合）：
        //     sprite.color = (sprite.color * 0.6f) + ((Color)color * 0.5f);   color ??= Color.green
        //
        // ⚠ 这一句在**白底**上必然发白：加法里那个 0.6 的底会把每个通道都抬起来。
        //   配 BAU 的绿：(0.6, 1.1, 0.6) → 淡绿，看得出来；
        //   配我们的紫 #A06EFF：(0.6+0.31, 0.6+0.22, 0.6+0.50) = (0.91, 0.82, 1.0)
        //   —— 三通道都贴到 1，等于"还是白的"，这就是看着只变淡、没变成紫色的原因。
        //
        // 所以这里**直接用主题色**（乘法交给精灵自己的贴图去做明暗），不再做加法。
        // alpha 沿用原值：BAU 那句也会把 alpha 一起改掉，这里不跟着改，
        // 免得本来半透明的精灵（比如高亮遮罩）被拉成不透明。
        Color c = color ?? Theme;
        sprite.color = new Color(c.r, c.g, c.b, sprite.color.a);
    }
}
