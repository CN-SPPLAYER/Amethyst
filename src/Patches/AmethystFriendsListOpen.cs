using System;
using System.Text;
using HarmonyLib;
using Il2CppSystem.Collections;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace Amethyst.Patches;

// ===== 主界面点「好友」不出面板：定位 + 修复（第 199 轮）=====
//
// ── 证据（`BepInEx\LogOutput.log`，第 55~67 行）────────────────────────
//   [FriendDiag] OpenUi 进入 ... 允许好友=True | UI实例=有 activeSelf=False ...
//   [FriendDiag] FriendsListUI.Open 进入 ...
//   Exception: System.NullReferenceException
//     at FriendsListUI.Open ()
//   [FriendDiag] OpenUi 返回 ... activeSelf=False
//
// 结论（三条，逐条都有依据，不再是猜测）：
//
// ① **点击到达了游戏**。日志顺序是
//      OpenUi 进入 → FriendsListUI.Open 进入 → 异常 → OpenUi 返回 → 好友按钮被点击
//    最后一条是**我们**挂在 `PassiveButton.OnClick` 上的探针listener，
//    它排在游戏自己的 listener **之后**（UnityEvent 按注册顺序调用，
//    游戏的在 Inspector 里序列化、先注册）。
//    ⇒ 第 197 轮"点击没到达按钮"和第 198 轮"强行再 OpenUi 一次"两个判断都已被否证。
//    后者还让每次点击重复抛 3 次同一个原生异常（日志里每次点击三组 Exception）。
//
// ② **不是被面板藏住了**。转储显示父链 `FriendsListManager=True`、
//    `activeSelf=False`、`根=FriendsListManager`。
//    好友 UI 挂在**激活**的 FriendsListManager 下，就是它自己关着；
//    右面板在屏幕外这个假设不成立（第 198 轮据此加的"钉到可见位"属于无效动作）。
//
// ③ **`Open()` 抛异常的位置在 `gameObject.SetActive(true)` 之前**。
//    因为异常返回后 `activeSelf` 仍是 `False`——原版是走到那里才把面板点亮的。
//
// ── 原生代码定位（LibCpp2IL 反汇编 GameAssembly.dll）──────────────────────
// `FriendsListUI.Open` @ 0x18063F470，字段偏移已逐个核对：
//
//   0x18063F5B9  Component.get_gameObject      → 之后才判断 activeSelf
//   0x18063F5EA  String.op_Equality            → currentSceneName == "" 就直接 Close 返回
//   0x18063F603  GetComponentsInChildren<FriendsListBar>(true)  并 Destroy 旧 bar
//   0x18063F6E9  FriendsListUI.UpdateFriendCodeUI
//   0x18063F6F8  DestroyableSingleton<HudManager>.get_InstanceExists   ← 0x18340C1B8
//   0x18063F708  DestroyableSingleton<HudManager>.get_Instance         ← 0x18340C100
//   0x18063F716  mov rcx,[rax+0A0h]        ← **HudManager + 0xA0**
//   0x18063F728  ChatController.get_IsOpenOrPlaying
//   …
//   0x18063F862  GameObject.SetActive(true)   ← 面板到这里才被点亮
//
// `HudManager` 的字段表核对结果：**0xA0 = `Chat`**（ChatController）。
// 所以那段原生逻辑等价于：
//
//     if (HudManager.InstanceExists && HudManager.Instance.Chat.IsOpenOrPlaying)
//         return;                    // 聊天开着就别弹好友面板
//
// `HudManager.Instance.Chat` 为空时，IL2CPP 生成的那句 null 检查直接跳
// 0x18063FB10（`il2cpp_codegen_raise_null_reference_exception`）⇒ NullReferenceException。
//
// ── 本轮怎么修 ────────────────────────────────────────────────────────
// 原版这段代码**没有任何 null 保护**，我们不能改游戏，只能：
//   · Prefix  —— 若能预判（HudManager 存在但 Chat 为空），就自己把面板打开并
//                跳过原版，**根本不产生异常**；
//   · Finalizer —— 万一预判没覆盖（站点不是 Chat），接住 NRE 并把面板打开，
//                同时吞掉异常，避免每次点击刷 3 条 IL2CPP 栈。
//   · 一次性转储 —— 把原版 `Open()` 在 SetActive 之前会用到的每一个引用都打出来，
//     这样如果站点另有其人，日志能直接指出，不用再来回猜。
//
// ⚠ 仍未确认（置信度：中高）：**为什么主界面会有 HudManager 而 Chat 为空**。
//   已排除本模组的嫌疑——全仓库 16 处 `Object.Destroy` 逐个看过，动的是
//   MeetingHud / 特效贴图 / 角色面板 / 角色标签 / 雪花根节点 / 主菜单背景，
//   没有一处碰 HudManager 或 Chat；`AmethystAlwaysChatState` 只做 `SetActive`，
//   不销毁。所以更像是 v19.0 主菜单自带的状态问题（好友按钮在大厅里是好的，
//   因为大厅场景会正常装配 HudManager）。这一轮先让它能用，根因留待日志确认。
//
// ── 第 218 轮：找到"修好了却还是不生效"的原因（本文件此前根本没跑）────────────
// ⚠ 第 199 轮写完这个补丁后，**第 200 轮**为了定位另一个问题（主界面"一直加载账号"）
//   加了一组临时开关 `Diag.Disable{FriendsBar,FriendsListOpen,MainMenuStyle}`，
//   把本模块停在"关闭"状态做二分，事后忘了打开：
//     · `AmethystConfig` 里 `DisableFriendsListOpen` 默认值就是 **true**（= 停用本模块）；
//     · `AmethystPlugin.SkipDiagPatch()` 看到类名含 "FriendsListOpen" 就整类跳过；
//     · 实机 `BepInEx\config\amethyst.mod.cfg` 里也确实是 `DisableFriendsListOpen = true`；
//     · `LogOutput.log` 里**从来没有** `[FriendsOpen]` 行 —— 铁证：补丁一次都没执行。
//   所以"好友按钮点了不出面板"并不是第 199 轮的分析错了，而是**修复被开关关掉了**。
//
// 处置：删掉 `DisableFriendsListOpen` 这个开关（连同字段与 Bind），本模块无条件生效。
//   理由：它的两个补丁都只在**点好友按钮**时才触发，与启动/账号加载无关，
//   不可能参与"一直加载账号"；而第 198 轮那个真正会刷异常的"兜底强开 OpenUi()"
//   已在第 199 轮删除。留一个默认就会关掉修复的开关，本身就是这次的病根。
//
// 同时补了 `ManualOpen()` 的两处（见该方法内注释）：
//   ① 先销毁残留旧 bar（原版在抛点之前做过；Prefix 路径整段跳过原版，从没做过）；
//   ② 补 `UpdateFriendCodeUI()`（同上，在抛点之前）。
[HarmonyPatch(typeof(FriendsListUI), "Open")]
internal static class AmethystFriendsListOpenPatch
{
    // 诊断只打一次：原版每次点击抛 3 次异常，逐次转储会把日志淹掉。
    private static bool _dumped;

    // ── ① 预判 + 接管 ────────────────────────────────────────────────
    private static bool Prefix(FriendsListUI __instance)
    {
        try
        {
            if (__instance == null) return true;
            if (!VanillaWouldThrow()) return true;

            DumpOnce(__instance, "预判命中：HudManager.Instance.Chat 为空，走我们自己的打开流程");
            ManualOpen(__instance);
            return false;   // 跳过原版 Open，不产生原生异常
        }
        catch
        {
            return true;
        }
    }

    // ── ② 兜底：接住预判没覆盖到的 NRE ────────────────────────────────
    //
    // 返回 null = 吞掉异常；返回 __exception = 原样抛回。
    // 只有 NullReferenceException 才接管，其它异常一律放行（别把别的问题捂住）。
    private static Exception Finalizer(FriendsListUI __instance, Exception __exception)
    {
        if (!(__exception is NullReferenceException)) return __exception;

        try
        {
            if (__instance == null) return __exception;

            DumpOnce(__instance, "兜底命中：原版 Open() 抛 NullReferenceException，已代为打开");
            ManualOpen(__instance);
            return null;
        }
        catch
        {
            return __exception;
        }
    }

    // 原版 Open() 里 `HudManager.Instance.Chat.IsOpenOrPlaying` 那一步会炸的条件。
    // 逐字对应反汇编：InstanceExists 为真 ⇒ 取 Instance ⇒ 取 Instance+0xA0（Chat）。
    private static bool VanillaWouldThrow()
    {
        try
        {
            HudManager hud = HudManager.Instance;
            return hud != null && hud.Chat == null;
        }
        catch
        {
            return false;
        }
    }

    // 手工复现原版 Open() 在 SetActive(true) 之后真正要紧的那几步。
    //
    // 顺序照抄反汇编（0x18063F842 之后）：
    //   viewingAllFriends = true → SetActive(true) → 标签页可见性 → OpenTab
    //   → RefreshFriendsList 协程 → OpenOverlayMenu（手柄/ESC 返回要用它）
    // 其中 SetActive 之前的三步（UpdateFriendCodeUI、bar 列表重建、currentSceneName）
    // 原版**已经跑完了**——我们是在它抛异常之后接手的，所以不重复。
    private static void ManualOpen(FriendsListUI ui)
    {
        // ★ 第 218 轮：顺序改成**照抄原版 Open()**，两条接管路径才都正确：
        //   清旧 bar → 四个列表置空 → UpdateFriendCodeUI → SetActive(true) → 标签页/刷新/覆盖层
        //   反汇编原序：0x18063F603（销毁旧 bar）→ 0x18063F6E9（UpdateFriendCodeUI）
        //   →〔原版在这里抛 NRE〕→ 0x18063F770（四个列表 = new List<>()）→ 0x18063F862（SetActive）

        // ① 清掉面板里残留的旧 bar。
        //   走 Finalizer（原版已跑过这一步）时是空操作；
        //   走 Prefix（原版整段被我们跳过）时**必须补** —— 否则只把列表换成空表、
        //   却不销毁列表里那些 GameObject，下一次 RefreshFriendsList 会再建一批 bar，
        //   面板就出现重复/叠影条目。
        DestroyBars<FriendsListBar>(ui);
        DestroyBars<LobbyPlayerBar>(ui);

        // ② 四个列表换成新的空表。
        //   反汇编里原版是"先 Destroy 旧 bar，再把列表置空"；走 Finalizer 时列表还攥着
        //   已销毁对象的引用，不清掉后续 RefreshFriendsList 会继续往里加，面板会越开越乱。
        try { ui.friendBars = new List<FriendsListBar>(); } catch { }
        try { ui.lobbyBars = new List<LobbyPlayerBar>(); } catch { }
        try { ui.notifBars = new List<FriendsListBar>(); } catch { }
        try { ui.platformFriendBars = new List<FriendsListBar>(); } catch { }

        // ③ 刷新好友码文本（原版 0x18063F6E9，在抛点**之前**；Prefix 路径没跑过，补一次）。
        //    重复调用是幂等的，只刷新文本。
        try { ui.UpdateFriendCodeUI(); } catch { }

        // ④ 点亮面板。
        GameObject go = null;
        try
        {
            go = ui.gameObject;
            if (go != null && !go.activeSelf) go.SetActive(true);
        }
        catch { }

        // 原版：currentSceneName != "OnlineGame" 时走这一支（主界面即是如此）。
        try { if (ui.LobbyPlayersTab != null) ui.LobbyPlayersTab.SetActive(true); } catch { }
        try { if (ui.LobbyPlayersInactiveTab != null) ui.LobbyPlayersInactiveTab.SetActive(false); } catch { }
        try { ui.OpenTab(0); } catch { }

        // 原版在异常点之前已经 Destroy 掉了旧的 FriendsListBar 并把四个
        // bar 列表重建成空表，所以必须重新拉一次，否则面板是空的。
        try
        {
            FriendsListManager mgr = FriendsListManager.Instance;
            if (mgr != null) ui.StartCoroutine(mgr.RefreshFriendsList(null, true));
        }
        catch { }

        // 打开覆盖层菜单：没有它手柄导航和 ESC 返回都失效。
        try
        {
            ControllerManager cm = ControllerManager.Instance;
            if (cm != null)
            {
                cm.OpenOverlayMenu(go != null ? go.name : string.Empty,
                                   ui.BackButton, ui.DefaultButtonSelected, ui.ControllerSelectable);
            }
        }
        catch { }
    }

    // 销毁面板下（含未激活）所有指定类型的 bar —— 照抄原版 Open() 在 SetActive 之前的清理。
    private static void DestroyBars<T>(FriendsListUI ui) where T : Component
    {
        try
        {
            T[] bars = ui.GetComponentsInChildren<T>(true);
            if (bars == null) return;

            for (int i = 0; i < bars.Length; i++)
            {
                try
                {
                    if (bars[i] != null && bars[i].gameObject != null)
                        UnityEngine.Object.Destroy(bars[i].gameObject);
                }
                catch { }
            }
        }
        catch { }
    }

    // 一次性把原版 Open() 在 SetActive(true) 之前会用到的引用全部打出来。
    //
    // 站点一共 16 个 IL2CPP 空检查抛点，真正可达的是下面这几个。
    // 如果下一轮日志显示 Chat 不为空，那就是别的原因，这里能直接看出来。
    private static void DumpOnce(FriendsListUI ui, string tag)
    {
        if (_dumped) return;
        _dumped = true;

        try
        {
            var sb = new StringBuilder();
            sb.Append("[FriendsOpen] ").Append(tag);

            try
            {
                sb.Append(" | 场景=").Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
            catch { sb.Append(" | 场景=?"); }

            try
            {
                HudManager hud = HudManager.Instance;
                sb.Append(" | HudManager=").Append(hud == null ? "null" : "有");
                sb.Append(" HudManager.Chat=").Append(hud == null ? "-" : (hud.Chat == null ? "null" : "有"));
            }
            catch (Exception ex) { sb.Append(" | HudManager=异常(").Append(ex.GetType().Name).Append(")"); }

            try { sb.Append(" | TranslationController=").Append(TranslationController.Instance == null ? "null" : "有"); }
            catch { sb.Append(" | TranslationController=?"); }

            try { sb.Append(" | ControllerManager=").Append(ControllerManager.Instance == null ? "null" : "有"); }
            catch { sb.Append(" | ControllerManager=?"); }

            if (ui != null)
            {
                sb.Append(" | guestAccountWarnings=").Append(ui.guestAccountWarnings == null ? "null" : "有");
                sb.Append(" ViewRequestsButton=").Append(ui.ViewRequestsButton == null ? "null" : "有");
                sb.Append(" LobbyPlayersTab=").Append(ui.LobbyPlayersTab == null ? "null" : "有");
                sb.Append(" LobbyPlayersInactiveTab=").Append(ui.LobbyPlayersInactiveTab == null ? "null" : "有");
                sb.Append(" BackButton=").Append(ui.BackButton == null ? "null" : "有");
                sb.Append(" DefaultButtonSelected=").Append(ui.DefaultButtonSelected == null ? "null" : "有");
                sb.Append(" ControllerSelectable=").Append(ui.ControllerSelectable == null ? "null" : "有");

                try
                {
                    GameObject g = ui.gameObject;
                    sb.Append(" | 本体 activeSelf=").Append(g == null ? "?" : g.activeSelf.ToString());
                    sb.Append(" activeInHierarchy=").Append(g == null ? "?" : g.activeInHierarchy.ToString());
                    sb.Append(" 父=").Append(ui.transform.parent == null ? "(root)" : ui.transform.parent.name);
                }
                catch { }
            }

            AmethystPlugin.Logger?.LogInfo(sb.ToString());
        }
        catch (Exception ex)
        {
            try { AmethystPlugin.Logger?.LogWarning("[FriendsOpen] 转储失败: " + ex.Message); }
            catch { }
        }
    }
}

// 打开好友 UI 时把右侧面板钉到可见位。
//
// ⚠ 第 199 轮更正：第 198 轮把这条当成"面板在屏幕外导致看不见"的**修复**，
//   但日志已证明父链完整（FriendsListManager=True、根=FriendsListManager），
//   它不是病因。这里保留只是因为好友按钮本身长在右面板上，打开它时
//   顺手把面板显示出来在交互上说得通；**不要再把它当成修复手段**。
[HarmonyPatch(typeof(FriendsListManager), "OpenUi")]
internal static class AmethystFriendsListOpenUiShowPanelPatch
{
    private static void Prefix()
    {
        try { AmethystMainMenuSlide.EnsureShownNow(); } catch { }
    }
}