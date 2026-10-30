using System;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 主界面 EHR 风格改造（第 188 轮；第 189 轮按反馈删掉关闭键与印记缩放、恢复自由模式）。
//
// ===== 参考实现 =====
// EndlessHostRoles 8.0.2 `Patches\CredentialsPatch.cs:201-380` 的 `TitleLogoPatch`
// （注意：不是 `Patches\MainMenuManagerPatch.cs`，那个只加 GitHub/Discord/官网三个按钮）。
// EHR 的主界面风格**不是自己画一套 UI**，而是给游戏自带的**世界空间 Sprite 按钮**重新上色 + 重排：
//   ① 全部主按钮换成同一张小精灵（`quitButton` 的 active 精灵），去掉 Shine 高光，文字刷白
//   ② 三档配色分级（EHR：主 青蓝 / 次 青绿 / 三档 橄榄黄）
//   ③ 隐藏 Divider、LeftPanel、howToPlayButton、FreePlayButton
//   ④ RightPanel 染成粉色
//   ⑤ BottomButtonBounds 下移 0.1
//
// ===== 与 EHR 的差异 =====
//
// (1) **配色改成紫色系**（需求：保留我们的模组标识）。
//     EHR 那套青蓝/青绿/橄榄黄是冷蓝色，和我们的品牌紫（#6B2FBC，标识色 #b554ff）
//     放在一起会明显打架。所以**只搬结构，三档色相换成紫的梯度**：
//     亮紫 → 浅紫 → 暗紫，保留"主/次/末要"的分级关系与 0.8 的统一透明度。
//
// (2) **不 reparent、不 SetActive(false) 隐藏 LeftPanel**。
//     EHR 是 `LeftPanel.ForEachChild(ResetParent)` 把按钮先挪到面板外面、再把面板关掉。
//     本模组的 `AmethystMainMenuOptimize` 已经用「只关掉面板底板的 SpriteRenderer」达到同样观感，
//     而且那边有明确教训（见该文件 54-63 行）：**直接 Destroy 会被 SlicedAspectScaler 每帧访问
//     抛 NullReferenceException 刷屏**。改父级同样会打乱 AspectPosition / SlicedAspectScaler
//     的登记关系，风险远大于收益 —— 视觉一样，就用最省事的那条路。
//
// ===== 第 189 轮按反馈删掉的三项（都是"照搬 EHR 带来的问题"）=====
//
// A. **右面板关闭键**（EHR 的第 ⑥ 项）：用户反馈不需要，整块删除。
//    顺带记录为什么当初做得那么费劲（三次翻车），免得日后有人又照抄回来：
//      ① EHR 硬编码局部坐标 (-4.78, 1.3, 1) 是给它自己的 `RightPanel` 调的；
//         我们在 v19.0 捕获到的面板真名是 **`MaskedBlackScreen`**（`Find("RightPanel")`
//         找不到真正在平移的那个对象），且它 `localScale=9.16` 会把局部坐标放大 9 倍；
//         相机的可视世界范围只有 x ∈ [-5.46, 5.46]，按钮被放到 x=-26.45 → 飞出屏幕。
//      ② 改用"子物体边界并集"定位 → 被面板里一个 `Background 90.00x54.00`
//         的全屏背景撑爆，并集变成 (-34.68,-45.64)-(55.32,26.44)。
//      ③ 锚定到干净的"本地/在线"卡片后按钮出现，但面板 lossyScale 9.16
//         连带放大，0.8 世界单位被撑成 7.3 单位 ≈ 1300px，糊满屏幕。
//    —— 结论：**这个面板的坐标系和 EHR 假设的完全不是一回事**，硬搬必然要三次返工。
//
// B. **模组印记缩到 0.3**（EHR 的第 ① 项）：**与本模组自己的 `ModStampPatch` 冲突**。
//    `src\Patches\ModStampPatch.cs:50` 已经**每帧**把 `ModStamp` 的 localScale 写成 `0.25`，
//    我们又每秒写一次 `0.3` —— 两个模块抢同一个 Transform，
//    用户反馈的现象正是"右上角模组图标忽大忽小"。已整项删除，印记交给 ModStampPatch 独占。
//
// C. **隐藏 FreePlayButton**（EHR 的第 ③ 项）：用户反馈"自由模式怎么不见了"。
//    `MainMenuManager.freePlayButton` 就是主菜单的**自由模式**入口，属于常用功能，
//    不该为了对齐 EHR 的视觉而删掉。已恢复显示。
//    `howToPlayButton`（怎么玩，教学入口）仍然隐藏 —— 那才是真正与左面板重叠的孤儿按钮。
//
// ===== 开关 =====
// 独立开关 `Other.MainMenuStyle`（默认开）。关掉即停止干预。
internal static class AmethystMainMenuStyle
{
    internal static bool Enabled => AmethystConfig.MainMenuStyle != null && AmethystConfig.MainMenuStyle.Value;

    // ===== 三档紫（对应 EHR 的 青蓝 / 青绿 / 橄榄黄）=====
    // alpha 统一 0.8 —— 和 EHR 一致：全不透明会显得很厚重，半透明才透出背景图。
    private static readonly Color32 TierPrimary   = new Color32(0xA8, 0x55, 0xF7, 0xCC); // 亮紫 #A855F7
    private static readonly Color32 TierSecondary = new Color32(0xC4, 0x94, 0xFF, 0xCC); // 浅紫 #C494FF
    private static readonly Color32 TierTertiary  = new Color32(0x7E, 0x57, 0xC2, 0xCC); // 暗紫 #7E57C2

    // 供 `AmethystFriendsBar` 复用：与「开始」按钮**完全相同**的色值。
    // 用户要求好友按钮及好友申请角标都用这一档，而不是模组 accent(#A06EFF)。
    internal static Color32 TierPrimaryColor => TierPrimary;

    // 右侧面板底色（EHR 用粉色 rgba(255,199,230)，这里换成淡紫）
    private static readonly Color32 PanelTint = new Color32(0xD8, 0xBE, 0xFF, 0xFF);

    // EHR TitleLogoPatch：左侧主菜单面板 localScale = (0.7, 0.7, 0.7)。
    private const float LeftPanelScale = 0.7f;
    private static Transform _scaledLeftPanel;
    private static Vector3 _leftPanelOriginalScale;

    private static Color _panelOriginalColor;
    private static bool _hasPanelOriginal;
    private static bool AppliedOnce;
    private static int _styledCount;
    private static bool _uniformFound;

    /// <summary>主菜单创建时应用一次。</summary>
    internal static void Apply(MainMenuManager menu)
    {
        try
        {
            if (menu == null) return;

            if (!Enabled) { Restore(); return; }

            bool firstRun = !AppliedOnce;
            AppliedOnce = true;

            ScaleLeftPanel(menu);
            StyleButtons(menu);
            HideLeftPanelPlate();
            KeepDivider();
            HideOrphanTutorials(menu);
            DecorateRightPanel();

            if (firstRun) Diagnose(menu);
        }
        catch { }
    }

    /// <summary>低频重入：游戏可能重建这些对象，样式得跟上；每次进来都能重新应用。</summary>
    internal static void ApplyLive()
    {
        try
        {
            if (!Enabled) { Restore(); return; }
            MainMenuManager menu = UnityEngine.Object.FindObjectOfType<MainMenuManager>();
            if (menu == null)
            {
                // 不在主界面了：把面板底色收掉，避免下次进来时状态是脏的
                ResetRightPanel();
                return;
            }
            Apply(menu);
        }
        catch { }
    }

    private static void ScaleLeftPanel(MainMenuManager menu)
    {
        // 从当前菜单按钮向上定位，避免匹配到其他界面的同名对象。
        Transform panel = menu.playButton != null ? menu.playButton.transform.parent : null;
        while (panel != null && panel.name != "LeftPanel") panel = panel.parent;
        if (panel == null) return;

        if (_scaledLeftPanel != panel)
        {
            RestoreLeftPanelScale();
            _scaledLeftPanel = panel;
            _leftPanelOriginalScale = panel.localScale;
            panel.localScale = Vector3.one * LeftPanelScale;
            AmethystPlugin.Logger?.LogInfo($"[MainMenuStyle] LeftPanel scale={panel.localScale}");
        }

        // 使用绝对值，低频重入不会累乘缩小；子按钮、文字和碰撞体同步缩放。
        panel.localScale = Vector3.one * LeftPanelScale;
    }

    private static void RestoreLeftPanelScale()
    {
        if (_scaledLeftPanel != null) _scaledLeftPanel.localScale = _leftPanelOriginalScale;
        _scaledLeftPanel = null;
    }

    // ===== 启动诊断 =====
    //
    // 主界面是**世界空间 Sprite UI**，不写日志就只能靠肉眼判断有没有生效。
    // 每次进入主菜单打一行，把"哪些对象找到了、面板到底是什么"讲清楚。
    //
    // ★ 关键：一定要把**写完之后**真正读回来的值打出来。
    //   只报"我做了什么"是不够的 —— 第 188 轮就因为只报"上色 8 个"而全绿，
    //   屏幕却是原版青绿（游戏在 Start 之后自己把颜色刷了回去）。
    //   读回值才能分清"写失败"和"写对了但被覆盖"。
    private static void Diagnose(MainMenuManager menu)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[MainMenuStyle] ");
            sb.Append("按钮=").Append(_styledCount).Append('/').Append(8);
            sb.Append(" 统一精灵=").Append(_uniformFound ? "取到" : "退回各自精灵");
            sb.Append(" | 面板=").Append(AmethystMainMenuSlide.Panel != null
                ? AmethystMainMenuSlide.Panel.name : "未捕获");
            sb.Append(" 底色=").Append(_hasPanelOriginal ? "已染紫" : "无SpriteRenderer");
            sb.Append(" | 底部边界=未改动");
            sb.Append(" 分隔线=").Append(DividerState());
            sb.Append(" | 回读 开始=").Append(ReadColor(menu.playButton));
            sb.Append(" 新闻=").Append(ReadColor(menu.newsButton));
            sb.Append(" 退出=").Append(ReadColor(menu.quitButton));
            sb.Append(" | 入口 自由模式=").Append(State(menu.freePlayButton != null
                ? menu.freePlayButton.gameObject : null))
                .Append(" 怎么玩=").Append(State(menu.howToPlayButton != null
                    ? menu.howToPlayButton.gameObject : null));
            AmethystPlugin.Logger?.LogInfo(sb.ToString());
        }
        catch { }
    }

    // 分隔线的真实状态（名字 / 激活 / 渲染器开关）。
    //
    // ⚠ 第 190 轮：这条曾经"显示一两帧然后消失"，光看日志完全看不出来 ——
    //   日志只报"我做了什么"，而这里要报的是**对象此刻的真实状态**。
    //   顺带把 gameObject 的名字也打出来，避免以后又出现"找不到目标还以为成功了"。
    private static string DividerState()
    {
        try
        {
            GameObject d = GameObject.Find("MainUI/AspectScaler/LeftPanel/Main Buttons/Divider");
            if (d == null)
            {
                // 路径没命中就退回裸名字（裸名字只匹配顶层对象，两者不是一回事）
                d = GameObject.Find("Divider");
                if (d == null) return "未找到";
                return "顶层同名 显示=" + d.activeSelf;
            }

            var sr = d.GetComponent<SpriteRenderer>();
            return "显示=" + d.activeSelf + " sr=" + (sr == null ? "无" : sr.enabled.ToString());
        }
        catch { return "异常"; }
    }

    private static string State(GameObject go)
    {
        try { return go != null ? (go.activeSelf ? "显示" : "隐藏") : "无"; }
        catch { return "异常"; }
    }

    // ===== 按钮：三档上色 + 统一小精灵 + 去高光 + 白字 =====
    //
    // ⚠ **不能只上色一次**。最初这里有个 `_buttonsStyled` 一次性标志，
    //   只在 `MainMenuManager.Start` 之后染一次；实机截图发现按钮仍是原版青绿色 ——
    //   游戏在 `Start` 之后还会自己把按钮颜色刷回去（开子菜单 / 切按钮状态时
    //   重设 SpriteRenderer.color），而每秒跑的 ApplyLive 看到标志为 true 就整段跳过，
    //   等于"上色完再也没补过"。现在改成**每次进来都重新断言**。
    //
    // ⚠ v1.1.6 回退说明（重要）：
    //   本轮为了做"悬停边缘高光"试过**不再套 uniform 精灵**，结果实机截图显示
    //   各按钮保留了**自己的原版贴图**——而那张贴图本身带底色（仓库/商店偏蓝），
    //   于是出现"开始是紫的、仓库/商店是蓝的"的花脸，观感更差，已按要求回退。
    //   结论：`uniform` 是**保证按钮颜色一致**的必要手段，不能去掉。
    //   悬停高光这件事另想办法，但优先级低于"按钮颜色必须统一"。
    private static void StyleButtons(MainMenuManager menu)
    {
        try
        {
            // 统一小精灵：以「退出」按钮的 active 精灵为基准（EHR 的 minorActiveSprite）。
            // 取不到就退回各按钮自己的精灵 —— 只上色不改形状，比硬套一个错尺寸安全。
            Sprite uniform = null;
            try { uniform = menu.quitButton?.activeSprites?.GetComponent<SpriteRenderer>()?.sprite; } catch { }
            _uniformFound = uniform != null;
            int n = 0;

            n += Tint(menu.playButton, TierPrimary, uniform);
            n += Tint(menu.inventoryButton, TierPrimary, uniform);
            n += Tint(menu.shopButton, TierPrimary, uniform);

            n += Tint(menu.newsButton, TierSecondary, uniform);
            n += Tint(menu.myAccountButton, TierSecondary, uniform);
            n += Tint(menu.settingsButton, TierSecondary, uniform);

            n += Tint(menu.creditsButton, TierTertiary, uniform);
            n += Tint(menu.quitButton, TierTertiary, uniform);

            _styledCount = n;
        }
        catch { }
    }

    private static int Tint(PassiveButton button, Color32 color, Sprite uniform)
    {
        if (button == null) return 0;
        try
        {
            // 去高光：原版按钮的 Shine 子物体在选中时会闪，与"扁平统一"的观感冲突。
            try { button.activeSprites?.transform?.Find("Shine")?.gameObject.SetActive(false); } catch { }
            try { button.inactiveSprites?.transform?.Find("Shine")?.gameObject.SetActive(false); } catch { }

            if (uniform != null)
            {
                try
                {
                    var a = button.activeSprites?.GetComponent<SpriteRenderer>();
                    if (a != null) a.sprite = uniform;
                    var i = button.inactiveSprites?.GetComponent<SpriteRenderer>();
                    if (i != null) i.sprite = uniform;
                }
                catch { }
            }

            try
            {
                var a = button.activeSprites?.GetComponent<SpriteRenderer>();
                if (a != null) a.color = color;
                var i = button.inactiveSprites?.GetComponent<SpriteRenderer>();
                if (i != null) i.color = color;
            }
            catch { }

            // 文字：active / inactive / disabled 三态全刷白（EHR 只刷了前两个）
            try { button.activeTextColor = Color.white; } catch { }
            try { button.inactiveTextColor = Color.white; } catch { }
            try { button.disabledTextColor = new Color(1f, 1f, 1f, 0.5f); } catch { }
            try { if (button.buttonText != null) button.buttonText.color = Color.white; } catch { }
            return 1;
        }
        catch { return 0; }
    }

    // ===== 左面板底板（**不含分隔线**）=====
    //
    // 只关底板的 SpriteRenderer，**不动层级、不 SetActive**（理由见文件头差异 (2)）。
    // 面板里装着全部按钮，关掉整块会让按钮一起消失。
    //
    // ⚠ 第 190 轮：**分隔线（Divider）不再隐藏**。
    //   用户反馈"那条横线改为不消失，现在还是会一点开始就大概显示一两帧然后就消失"。
    //   现象与本轮排查结论一致，见下方长注释。
    private static void HideLeftPanelPlate()
    {
        DisableRenderer("LeftPanel");
    }

    // ===== 分隔线：第 190 轮改为**保留** =====
    //
    // 背景（这一条折腾了两轮才找准）：
    //   EHR 的第 ④ 项是 `GameObject.Find("Divider")?.SetActive(false)`。
    //   本模组第 189 轮照搬了它（两处：裸名字 + 完整路径），
    //   加上 `AmethystMainMenuOptimize.cs:44` **早就有一处**在隐藏同一条线。
    //
    // 为什么会出现"显示一两帧然后消失"这种闪烁：
    //   两条隐藏路径的**执行时机不同** ——
    //   `AmethystMainMenuOptimize.Tick` 走 `MainMenuManager.Start` 的 Postfix（第 44 行），
    //   而本模块走 `MainMenuManager.LateUpdate` 的 1 秒节流兜底。
    //   游戏在 `Start` 之后还会重建一次左面板层级（按钮/分隔线都是新建的对象），
    //   新对象 `activeSelf` 默认为 true → **先显示出来**，
    //   最多 1 秒后被我们的节流兜底 `SetActive(false)` → **再消失**。
    //   这就是用户看到的"闪一下就没"。
    //
    // 现在两处都不再隐藏它，横线常驻。
    // 底板（LeftPanel 那个大圆角矩形）仍然隐藏 —— 那是"去掉厚重底框"，
    // 与横线是两回事，保留。
    private static void KeepDivider()
    {
        try
        {
            // 已经被人隐藏过（同一进程内跑过旧逻辑）就恢复显示
            GameObject d = GameObject.Find("MainUI/AspectScaler/LeftPanel/Main Buttons/Divider");
            if (d != null && !d.activeSelf) d.SetActive(true);
        }
        catch { }
    }

    // 第 189 轮：**一个模式区的按钮都不藏了**。
    //
    // 背景（两次做错、两次回退）：
    //   ① 第 188 轮照搬 EHR 藏了 `howToPlayButton`（怎么玩/玩法介绍）和
    //      `freePlayButton`（自由模式）。用户反馈"自由模式怎么不见了"。
    //   ② 第 189 轮先只恢复了 freePlayButton、继续藏 howToPlayButton，
    //      用户反馈"把自由模式旁边那个玩法介绍不要隐藏了，感觉没有这个怪怪的、缺一块"。
    //
    // 也就是说：EHR 藏这两个按钮的前提是**它自己重排了整块模式区**（LeftPanel 缩到 0.7
    // 且把按钮 reparent 到面板外），而我们走的是"只关底板 SpriteRenderer"的路线，
    // 布局还是原版的 —— 藏掉任何一个都会在那一行留下空缺。
    //
    // 教训：隐藏类改动必须**连着布局改动一起搬**，单独搬就是留残缺。
    // 这里保留方法名与调用点（结构照 EHR 的清单），但实现为空操作并说明原因，
    // 免得日后又有人照 EHR 把它们加回来。
    private static void HideOrphanTutorials(MainMenuManager menu)
    {
        // 故意留空：见上方说明。自由模式与玩法介绍都必须显示。
    }

    // ===== 右面板：染成淡紫 =====
    private static void DecorateRightPanel()
    {
        GameObject panel = AmethystMainMenuSlide.Panel;
        if (panel == null) return;   // 滑入模块还没捕获到 —— 下一轮再试

        try
        {
            var sr = panel.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (!_hasPanelOriginal) { _panelOriginalColor = sr.color; _hasPanelOriginal = true; }
                if (sr.color != PanelTint) sr.color = PanelTint;
            }
        }
        catch { }
    }

    // 离开主界面时把面板底色收掉（下次进来会重新染）
    private static void ResetRightPanel()
    {
        try
        {
            GameObject panel = AmethystMainMenuSlide.Panel;
            if (panel != null && _hasPanelOriginal)
            {
                var sr = panel.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = _panelOriginalColor;
            }
        }
        catch { }
    }

    // ===== 底部按钮边界：第 189 轮**已删除** EHR 的「下移 0.1」=====
    //
    // EHR 的第 ⑤ 项是 `BottomButtonBounds.transform.localPosition -= (0, 0.1, 0)`。
    // 本轮实测这**两项投诉都出自它**：
    //   ① 用户反馈"点开始之后会有一条横线会突然消失"
    //   ② 用户反馈"自由模式怎么不见了"（日志里 freePlayButton.activeSelf 仍是 true，
    //      说明它没被 SetActive 隐藏，而是被这个位移推出了屏幕外）
    // 截图佐证：右面板滑入后「练习模式」那张卡压在红字上、且被屏幕下边缘切掉一半，
    // 而它正是那一行里的成员。
    //
    // 教训：EHR 的位移量是按**它自己的面板布局**调的，我们的面板在 v19.0 里
    // 结构和尺寸都不同（见文件头 A 里的实测数据），照搬位移量必然错位。
    // 整项删除，不做"改成多少"的猜测。

    // 还原（关开关时）
    //
    // ⚠ 只还原有原始快照的改动：右面板底色、左面板缩放。
    //   按钮配色 / 面板底板**故意不还原** —— 原版颜色没有可靠来源
    //   （取样时对象可能已被别的模块改过），硬还原反而会破坏别人的设置。
    //   需要恢复原版外观时，重进一次主界面即可（游戏会重建对象）。
    internal static void Restore()
    {
        try { RestoreLeftPanelScale(); } catch { }
        try { ResetRightPanel(); } catch { }
    }

    private static void DisableRenderer(string path)
    {
        try
        {
            GameObject go = GameObject.Find(path);
            if (go == null) return;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || !sr.enabled) return;
            sr.enabled = false;
        }
        catch { }
    }

    private static string Fmt(float v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static string ReadColor(PassiveButton b)
    {
        try
        {
            var sr = b?.inactiveSprites?.GetComponent<SpriteRenderer>();
            if (sr == null) return "无renderer";
            var c = sr.color;
            return string.Format("#{0:X2}{1:X2}{2:X2}", Mathf.RoundToInt(c.r * 255f),
                Mathf.RoundToInt(c.g * 255f), Mathf.RoundToInt(c.b * 255f));
        }
        catch { return "异常"; }
    }
}


// ===== Hook =====

[HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class AmethystMainMenuStyleStartPatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        // 滑入模块的 Postfix 已经先跑过（面板已捕获），这里能直接拿到它
        AmethystMainMenuStyle.Apply(__instance);
    }
}

// 低频兜底：主菜单自身 LateUpdate，1 秒一次足够（按钮/面板不会被频繁重建，
// 但进对局再返回时主菜单对象会换新，靠这个把样式补回去）。
[HarmonyPatch(typeof(MainMenuManager), "LateUpdate")]
internal static class AmethystMainMenuStyleUpdatePatch
{
    private static float _next;

    private static void Postfix()
    {
        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + 1f;
        AmethystMainMenuStyle.ApplyLive();
    }
}
