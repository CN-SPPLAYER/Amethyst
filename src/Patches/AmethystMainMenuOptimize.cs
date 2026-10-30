using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 界面优化 · 主界面优化。
//
// 参考 AmongUsRevamped 2.1.0 的 `Patches\CredentialsPatch.cs`（MainMenuManagerStartPatch），
// 对原版主界面做「减法」精简：隐藏遮挡元素、去掉多余装饰，让主界面更干净。
//
// 与参考实现的差异（按要求）：
//   **不移除 Among Us 标志**（原版 `LOGO-AU`）。参考模组会隐藏它以换成自己的模组图标，
//   本模组没有独立图标，所以保留原版标志。
//
// 说明：
//   - 全部操作都是「把已有对象隐藏/禁用」，属于可逆的视觉精简，不改游戏逻辑；
//   - 每帧幂等（对象可能被游戏重建），但只在开启开关时执行；
//   - 关闭开关后不再干预，重新打开主界面即恢复原版外观。
internal static class AmethystMainMenuOptimize
{
    // 独立开关（原先跟随「界面优化」总开关，第五十轮起单独开关）
    internal static bool Enabled => AmethystConfig.MainMenuOptimize != null && AmethystConfig.MainMenuOptimize.Value;

    // 记录我们禁用过的对象，便于关闭开关时还原。
    private static readonly System.Collections.Generic.List<GameObject> _hidden = new System.Collections.Generic.List<GameObject>();
    private static readonly System.Collections.Generic.List<SpriteRenderer> _disabledRenderers = new System.Collections.Generic.List<SpriteRenderer>();

    internal static void Tick(MainMenuManager menu)
    {
        try
        {
            if (!Enabled)
            {
                Restore();
                return;
            }
            if (menu == null) return;

            // 主界面背景贴图：去掉杂色背景，让纯色底更干净
            HideObject("BackgroundTexture");

            // 左侧面板底板：精简掉厚重底框
            //
            // ⚠ 第 190 轮：**分隔线（Divider）不再隐藏**。
            //   这里原来有一处 `HideObject(".../Main Buttons/Divider")`，
            //   加上 `AmethystMainMenuStyle` 里的另一处，两条路径执行时机不同
            //   （本文件走 Start 的 Postfix，那个模块走 LateUpdate 的 1 秒节流），
            //   而游戏在 Start 之后会重建左面板层级 → 新建的 Divider 先显示、
            //   最多 1 秒后才被节流兜底关掉，表现为"闪一下就没"。
            //   现改为两处都不隐藏，横线常驻。
            HideRenderer("LeftPanel");
            ShowDivider();

            // 右侧面板与遮罩、窗户高光：主界面不需要它们占据视觉
            HideObject("WindowShine");
            HideRenderer("RightPanel");
            HideRenderer("MaskedBlackScreen");

            // 全屏色调遮罩
            HideObject("MainUI/Tint");

            // 好友按钮的「新请求」角标：隐藏而不是销毁！
            //
            // 重要教训：参考模组 (AmongUsRevamped) 这里用的是 Object.Destroy，但直接 Destroy 会炸：
            // 该 Background 是一个 SpriteRenderer，且被 AspectScaledAsset 登记在
            // SlicedAspectScaler 的列表里。对象被销毁后引用变成「假活」，
            // SlicedAspectScaler.Update() 每帧访问它的 .size 就会抛
            //   NullReferenceException at UnityEngine.SpriteRenderer.get_size()
            //   at AspectScaledAsset.ScaleObject() → SlicedAspectScaler.Update()
            // 造成每帧刷屏（实测单次会话 1600+ 次）。
            // 改为 SetActive(false)：视觉上同样不显示，但对象仍然存活，引用有效，不会刷错。
            // 好友按钮的「新请求」角标：隐藏而不是销毁。
            //
            // ⚠ 第 191 轮：**这两条也一并撤销**。
            //   它们是 `FriendsButton/Highlight/NewRequestActive/{Text_TMP,Background}`，
            //   属于**按钮自身的子物体**。隐藏后虽然不影响点击，但用户反馈
            //   "好友打不开面板" 时，这是同一区域里唯一还在动 `SetActive(false)` 的东西，
            //   排查时容易被误判成元凶，所以改成统一交给 `AmethystFriendsBar` 用
            //   **改颜色**的方式处理（角标压到 30% 透明度）—— 不动激活状态，行为更干净。
            //
            // 好友面板本体（FriendsListUI）也恢复显示，见 HideFriendsBar()。

            // 开始按钮文案本地化为「开始 / Start」，并禁用翻译组件防止被改回
            NormalizePlayButton(menu);

            // 开始按钮文案本地化为「开始 / Start」，并禁用翻译组件防止被改回
            NormalizePlayButton(menu);

            // 第 199 轮：好友按钮（账号页右上角）染主题色。
            //
            // 放在这里是因为它和本方法是同一类活：**游戏会自己把
            // SpriteRenderer.color 刷回原版**，所以必须低频反复断言，
            // 不能只在 Awake 染一次（`AmethystMainMenuStyle.cs:188-191`
            // 已经在主菜单按钮上踩过这个坑并留了记录）。
            // 本方法由 `AmethystHud.Update` 每 0.5s 调一次，不必再加新计时器。
            if (AmethystConfig.DiagDisableFriendsBar == null || !AmethystConfig.DiagDisableFriendsBar.Value)
            {
                try { AmethystFriendsBar.TickLive(); } catch { }
            }
        }
        catch { }
    }

    // 供 AmethystHud 低频调用：在主界面时应用优化，离开主界面或关闭开关时还原。
    // 这样「中途开关」无需重进主界面即可生效。
    internal static void TickLive()
    {
        try
        {
            MainMenuManager menu = UnityEngine.Object.FindObjectOfType<MainMenuManager>();
            if (menu == null)
            {
                if (_hidden.Count > 0 || _disabledRenderers.Count > 0) Restore();
                return;
            }
            Tick(menu);
        }
        catch { }
    }

    // 确保左面板里的分隔线处于显示状态。
    //
    // 第 190 轮：分隔线**不再隐藏**（用户反馈"闪一下就没"很不美观）。
    // 这里显式 SetActive(true) 而不是"什么都不做"，是为了在同一进程内
    // 抵掉旧版本残留的隐藏记录 —— 比如热重载后 _hidden 里还留着它。
    private static void ShowDivider()
    {
        try
        {
            GameObject d = GameObject.Find("MainUI/AspectScaler/LeftPanel/Main Buttons/Divider");
            if (d != null && !d.activeSelf) d.SetActive(true);
        }
        catch { }
    }

    // 把 Play 按钮的文字固定为“开始”（跟随语言），避免原版把文案翻译成别的词。
    private static void NormalizePlayButton(MainMenuManager menu)
    {
        try
        {
            Transform t = menu.transform.Find("MainUI/AspectScaler/LeftPanel/Main Buttons/PlayButton/FontPlacer/Text_TMP");
            if (t == null) return;

            GameObject go = t.gameObject;
            if (go == null) return;

            var translator = go.GetComponent<TextTranslatorTMP>();
            if (translator != null && translator.enabled) translator.enabled = false;

            var text = go.GetComponent<TMPro.TextMeshPro>();
            if (text == null) return;

            string want = AmethystText.T("Play");
            if (!string.IsNullOrEmpty(want) && text.text != want) text.text = want;
        }
        catch { }
    }

    // 第 191 轮：**不再隐藏 FriendsListUI**。
    //
    // ⚠ 这里原来有一处 `go.SetActive(false)`，把**整个好友面板本体**关掉了 ——
    //   而它不是"背景板"，而是点「好友」后弹出的**整个面板**
    //   （探针确认 FriendsListUI 上挂着 FriendArea / AddFriendObjects / FriendScroller /
    //     BlockedArea / FriendCodeText / BackButton 等全部内容）。
    //   用户反馈的现象正是"右上角好友打不开面板"：对象被关掉后，
    //   游戏的 `FriendsManager.Open()` 走的是 `SetActive(true)` 之外的路径（或被我们的
    //   1 秒兜底再次关掉），于是点多少次都弹不出来。
    //
    //   这条隐藏是第 100 多轮加的（当时误以为它是"主界面底部那条好友背景板"），
    //   与 `AmethystMainMenuStyle` 的好友条改造目标直接冲突。
    //   现整项删除，好友面板恢复原样；顶栏的观感仍由 AmethystFriendsBar 负责
    //   （底板透明 + 按钮上色），那两项只改颜色、不动激活状态。
    private static void HideFriendsBar()
    {
        // 故意留空：见上方说明。好友面板必须能正常打开和关闭。
        //
        // ⚠ 第 192 轮：这里原先还有一段"兜底恢复显示"
        //   （`if (!ui.gameObject.activeSelf) ui.gameObject.SetActive(true);`），
        //   犯了和 `AmethystFriendsBar` 里同一个错误 —— 面板的关闭正是靠 SetActive(false)
        //   实现的，每秒置 true 会让好友菜单不断自己弹开、且关不掉。已删除。
        //
        // 现在这个方法**完全什么都不做**：面板的开关状态由游戏自己掌管。
    }

    private static void HideObject(string path)
    {
        try
        {
            GameObject go = GameObject.Find(path);
            if (go == null || !go.activeSelf) return;
            go.SetActive(false);
            if (!_hidden.Contains(go)) _hidden.Add(go);
        }
        catch { }
    }

    private static void HideRenderer(string path)
    {
        try
        {
            GameObject go = GameObject.Find(path);
            if (go == null) return;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || !sr.enabled) return;
            sr.enabled = false;
            if (!_disabledRenderers.Contains(sr)) _disabledRenderers.Add(sr);
        }
        catch { }
    }

    // 关闭开关时把隐藏过的对象恢复显示。
    internal static void Restore()
    {
        try
        {
            for (int i = 0; i < _hidden.Count; i++)
            {
                GameObject go = _hidden[i];
                if (go != null && !go.activeSelf) go.SetActive(true);
            }
            _hidden.Clear();

            for (int i = 0; i < _disabledRenderers.Count; i++)
            {
                SpriteRenderer sr = _disabledRenderers[i];
                if (sr != null) sr.enabled = true;
            }
            _disabledRenderers.Clear();
        }
        catch { }
    }
}

// 主界面每次进入时应用一次（MainMenuManager.Start）。
[HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class AmethystMainMenuOptimizeStartPatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        AmethystMainMenuOptimize.Tick(__instance);
    }
}
