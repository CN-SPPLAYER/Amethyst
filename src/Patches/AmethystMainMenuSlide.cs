using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 主界面「点开始 → 右侧面板从右向左滑入」动画。
//
// 参考 FinalSuspect (`Patches\System\MainMenuManagerPatch.cs`) 的行为模型，
// 面板捕获方式沿用 BanMod (`gameModeButtons.transform.parent`)。
//
// 触发规则（与 FinalSuspect 完全一致）：
//   **滑入**：OpenGameModeMenu、**OpenAccountMenu**、**OpenCredits**
//            —— 「我的账号 / 制作人员」的内容也在右侧面板里，必须滑入而不是滑走。
//   **滑出**：MainMenuManager.Start、OptionsMenuBehaviour.Open、AnnouncementPopUp.Show
//            —— 打开设置/公告时滑出，避免面板停在原位遮挡弹窗。
//   另外仓库/商店（TransitionToInventory / TransitionToShop）也滑出，保证回来时能重新滑入。
//
// 动画：FinalSuspect 用 Vector3.Lerp(当前, 目标, Time.deltaTime * k)。
// 其 k 为 3/2（约 1 秒，偏慢）；这里按反馈显著加快到 12，约 0.25 秒完成。
// 该动画为内置行为、强制开启，不新增开关项。
internal static class AmethystMainMenuSlide
{
    private enum PanelState { Hidden, Showing, Visible, Hiding }

    private static GameObject _panel;
    private static Vector3 _shownPos;
    private static bool _captured;
    private static PanelState _state = PanelState.Hidden;

    // 「面板已在显示位时重播滑入」的冷却时间戳（防止被连续调用反复重置）
    private static float _lastReplay;

    // 上一次触发的入口（"gamemode" / "account" / "credits"）。
    // 用途：**同一个入口反复点不重播动画**（用户反馈：一直按「开始」会一遍遍滑入），
    // 而**换了入口**（制作人员 → 开始）时重播一次，观感与首次滑入一致。
    private static string _lastTrigger;

    // 面板隐藏位相对显示位的横向偏移（与 BanMod 一致）
    private const float HiddenOffsetX = 10f;

    // 位移速度：Lerp 系数。越大越快（最终约 0.25s 完成整段位移）。
    private const float Speed = 12f;

    private static Vector3 HiddenPos => _shownPos + new Vector3(HiddenOffsetX, 0f, 0f);

    // 滑入动画作用的那个面板本体。
    //
    // 第 188 轮新增：主界面样式模块（AmethystMainMenuStyle）要往**同一个**面板上挂关闭按钮。
    // 如果两边各自 `GameObject.Find("RightPanel")` / `gameModeButtons.transform.parent`，
    // 一旦找到的不是同一个对象，就会出现「面板滑走了、关闭键却留在原地」这种脏现象。
    // 这里以本模块捕获到的为准（它才是真正被平移的那个）。
    internal static GameObject Panel => _panel;

    // 是否已经成功捕获到面板。供 LateUpdate 的后备捕获判断。
    internal static bool Captured => _captured && _panel != null;

    // MainMenuManager.Start：捕获面板与显示位，并置于隐藏位。
    internal static void Capture(MainMenuManager menu)
    {
        try
        {
            if (menu == null) return;
            if (menu.gameModeButtons == null || menu.gameModeButtons.transform == null) return;

            Transform parent = menu.gameModeButtons.transform.parent;
            if (parent == null) return;

            _panel = parent.gameObject;
            if (_panel == null) return;

            _shownPos = _panel.transform.localPosition;
            _captured = true;
            _state = PanelState.Hidden;
            _panel.transform.localPosition = HiddenPos;
        }
        catch { }
    }

    // 每帧平滑移动（MainMenuManager.LateUpdate Postfix）。
    internal static void Animate()
    {
        try
        {
            if (!_captured || _panel == null) return;
            if (!_panel.activeInHierarchy) return;

            Vector3 target;
            if (_state == PanelState.Showing) target = _shownPos;
            else if (_state == PanelState.Hiding) target = HiddenPos;
            else return;

            Transform t = _panel.transform;

            // t 会被 Lerp 夹在 [0,1]；低帧率下也不会过冲。
            float k = Mathf.Clamp(Time.deltaTime * Speed, 0f, 1f);
            t.localPosition = Vector3.Lerp(t.localPosition, target, k);

            if (Vector3.Distance(t.localPosition, target) < 0.02f)
            {
                t.localPosition = target;
                _state = _state == PanelState.Showing ? PanelState.Visible : PanelState.Hidden;
            }
        }
        catch { }
    }

    // 立即把面板钉到显示位（不做动画）。
    //
    // 用途：好友 UI 属于右侧面板这一套（FS 的 `HideRightPanel` 就同时收起
    // `AccountTab/AccountWindow`）。我们的滑入模块默认把面板停在**隐藏位**，
    // 如果好友 UI 渲染在这块面板里，就会出现"点了好友但界面在屏幕外"。
    // 打开好友前先把它钉回显示位，保证 UI 一定落在可见区域。
    internal static void EnsureShownNow()
    {
        try
        {
            if (!_captured || _panel == null) return;
            _panel.transform.localPosition = _shownPos;
            _state = PanelState.Visible;
        }
        catch { }
    }

    // 滑入（点开始 / 我的账号 / 制作人员）
    // `trigger` 用来区分入口：同一个入口反复点不重播，换入口才重播。
    internal static void Show(string trigger)
    {
        try
        {
            if (!_captured || _panel == null) return;

            bool sameAsLast = !string.IsNullOrEmpty(trigger) && trigger == _lastTrigger;

            if (_state == PanelState.Visible)
            {
                // 面板已经在显示位：
                //   - 同一个入口反复点（例如一直按「开始」）⇒ **不重播**，避免动画一遍遍滑入；
                //   - 换了入口（制作人员 → 开始 / 我的账号）⇒ 内容变了，重播一次滑入。
                if (sameAsLast) return;
                if (Time.realtimeSinceStartup - _lastReplay < 0.4f) return;
                _lastReplay = Time.realtimeSinceStartup;
                _lastTrigger = trigger;

                _panel.transform.localPosition = HiddenPos;
                _state = PanelState.Showing;
                return;
            }

            _lastTrigger = trigger;
            _state = PanelState.Showing;
        }
        catch { }
    }

    // 滑出（设置 / 公告 / 仓库 / 商店 / 进入主界面时）
    //
    // ⚠ 第 196 轮修正：这里**不再**顺带强关账号窗口。
    //   旧版把 `AccountWindow.SetActive(false)` 写在 Hide() 里，注释还写着
    //   "与 FinalSuspect 一致" —— 实际不一致。FS 的写法是把它放在一个**独立的**
    //   Prefix 里，只挂在三个明确事件上：
    //       MainMenuManager.Start / OptionsMenuBehaviour.Open / AnnouncementPopUp.Show
    //   差别很要紧：好友面板就挂在 `AccountTab/AccountWindow` 下面，
    //   我们在**每次滑出**（含进对局返回、仓库、商店）都强关它，
    //   等于把游戏刚打开的好友 UI 又按掉 —— 表现就是"点了没反应 / 突然刷一下又没了"。
    //   现在这一行搬回 FS 的独立钩子，只在那三个时机执行。
    internal static void Hide()
    {
        try
        {
            if (!_captured || _panel == null) return;
            if (_state == PanelState.Hidden) return;
            _state = PanelState.Hiding;
        }
        catch { }
    }

    // 收起账号窗口 —— 照抄 FinalSuspect `MainMenuManagerPatch.cs:33-42`
    //     AccountManager.Instance?.transform.FindChild("AccountTab/AccountWindow")?.gameObject.SetActive(false);
    //
    // ⚠ 第 197 轮：**默认不再执行**（见下方 `CloseAccountWindow` 的调用点说明）。
    //   保留方法体，但改由「只在明确需要腾地方的两个时机」调用，且加开关可关。
    internal static void CloseAccountWindow()
    {
        try
        {
            AccountManager.Instance?.transform
                ?.FindChild("AccountTab/AccountWindow")?.gameObject.SetActive(false);
        }
        catch { }
    }

    // 离开主界面时清理引用，防止场景切换后残留假活对象。
    internal static void Reset()
    {
        _captured = false;
        _panel = null;
        _state = PanelState.Hidden;
        _lastTrigger = null;
    }
}

[HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class AmethystMainMenuSlideStartPatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        AmethystMainMenuSlide.Reset();
        AmethystMainMenuSlide.Capture(__instance);
    }
}

// 第 280 轮：启动首轮的后备捕获。
// MainMenuManager.Start 的 Postfix 执行时，gameModeButtons 可能还没由 RunStartUp 建好，
// 于是 Capture 早退，第一次点「开始」就没有滑入动画（后续进入主菜单才正常）。
// LateUpdate 每帧都会跑；未捕获时用当前实例补捕获一次，位置取面板当前的显示位。
// 已捕获时只是一次布尔判断，零开销。
[HarmonyPatch(typeof(MainMenuManager), "LateUpdate")]
internal static class AmethystMainMenuSlideUpdatePatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        if (!AmethystMainMenuSlide.Captured)
            AmethystMainMenuSlide.Capture(__instance);

        AmethystMainMenuSlide.Animate();
    }
}

// ===== 滑入：点开始 / 我的账号 / 制作人员 =====
[HarmonyPatch(typeof(MainMenuManager), "OpenGameModeMenu")]
internal static class AmethystMainMenuSlideGameModePatch
{
    private static void Prefix() => AmethystMainMenuSlide.Show("gamemode");
}

[HarmonyPatch(typeof(MainMenuManager), "OpenAccountMenu")]
internal static class AmethystMainMenuSlideAccountPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Show("account");
}

[HarmonyPatch(typeof(MainMenuManager), "OpenCredits")]
internal static class AmethystMainMenuSlideCreditsPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Show("credits");
}

// ===== 滑出：设置 / 公告 / 仓库 / 商店 =====

// 收起账号窗口 —— 照抄 FinalSuspect `MainMenuManagerPatch.cs:33-42` 的触发集，
// 但**去掉了 `MainMenuManager.Start`**（第 197 轮修正，见下）。
//
// ===== 为什么必须去掉 `MainMenuManager.Start` =====
//
// 需求反馈：「点击好友按钮后不出好友 UI」。
//
// `AccountManager/AccountTab/AccountWindow` 是**账号页那一整块窗口**，
// 而右上角「好友」按钮弹出的 `FriendsListUI` 是它的**同级/子级 UI**
// （`FriendsListManager.UiPrefab` 在 `OpenUi()` 时实例化，见 `FriendsListManager.Ui/UiPrefab`）。
//
// 问题出在**时机**上：
//   · `MainMenuManager.Start` 在**每次进入主菜单**时都会跑 —— 包括从对局返回、
//     从仓库/商店返回、从设置返回；
//   · 我们在它的 Prefix 里把 `AccountWindow` 置为 `SetActive(false)`；
//   · 而用户点「好友」的瞬间，`FriendsListManager.OpenUi()` 才去实例化/激活 UI，
//     若此时 `AccountWindow` 刚被我们关掉，新 UI 就落在一个**非激活的父链**上 ——
//     Unity 不会报错，但界面**不会显示**，表现就是"点了没反应"。
//
// FS / EHR 之所以没事：它们把 `HideRightPanel` 的触发集严格限制在
// **`OptionsMenuBehaviour.Open` / `AnnouncementPopUp.Show`** 这两个"需要弹窗腾地方"的时机，
// 并且用 `[HarmonyPriority(Priority.Last)]` 保证在别人之后执行；
// 同时它们**另有** `ShowRightPanelImmediately()` 在需要时把面板重新打开。
// 我们缺的正是这一对"关了就有人负责开"的配对 —— 于是关掉之后再没人开。
//
// 第 197 轮的处理：
//   1. **移除 `MainMenuManager.Start`** —— 进主菜单不再动账号窗口（这是"点了没反应"的直接原因）；
//   2. 补上 `[HarmonyPriority(Priority.Last)]`，与 FS/EHR 对齐执行顺序；
//   3. 只保留 `OptionsMenuBehaviour.Open` / `AnnouncementPopUp.Show` 两个时机
//      （设置/公告确实需要腾地方，此时用户不可能同时在点好友）。
[HarmonyPatch(typeof(OptionsMenuBehaviour), "Open")]
[HarmonyPatch(typeof(AnnouncementPopUp), "Show")]
[HarmonyPriority(Priority.Last)]
internal static class AmethystCloseAccountWindowPatch
{
    private static void Prefix() => AmethystMainMenuSlide.CloseAccountWindow();
}

[HarmonyPatch(typeof(OptionsMenuBehaviour), "Open")]
internal static class AmethystMainMenuSlideSettingsPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Hide();
}

[HarmonyPatch(typeof(AnnouncementPopUp), "Show")]
internal static class AmethystMainMenuSlideAnnouncementPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Hide();
}

[HarmonyPatch(typeof(MainMenuManager), "TransitionToInventory")]
internal static class AmethystMainMenuSlideInventoryPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Hide();
}

[HarmonyPatch(typeof(MainMenuManager), "TransitionToShop")]
internal static class AmethystMainMenuSlideShopPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Hide();
}
