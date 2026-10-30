using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 身份选择器：**照 MalumMenu 3.3.0 的方式调用游戏原版「变形者菜单」**。
//
// MalumMenu 的做法（`src\Utilities\PlayerPickMenu.cs` + `src\Patches\ShapeshifterMinigamePatches.cs`）：
//   1) 取 Shapeshifter 的 RoleBehaviour → `Cast<ShapeshifterRole>()` → 实例化 → 读 `.ShapeshifterMenu`
//      就拿到了变形者菜单的 prefab；
//   2) 把菜单实例化到 `Camera.main` 下、`localPosition = (0,0,-50)`，然后 `Begin(null)`；
//   3) 用 `ShapeshifterMinigame.Begin` 的 Prefix 接管内容 —— 自己造 `PanelPrefab` 面板、摆格子、
//      调 `SetPlayer(i, data, action)`，最后 `ControllerManager.Instance.OpenOverlayMenu(...)`，
//      并 `return false` 跳过原方法。
//
// 与 MalumMenu 的区别：它列的是**玩家**，这里列的是**身份**。
// 做法上**保留原版 SetPlayer 的渲染**（这样每个格子里有人物，符合用户要求），
// 再用 **Postfix** 把名字覆盖成身份名（内鬼红 / 船员蓝），并清掉等级与色盲色名。
internal static class AmethystRoleTagPicker
{
    private const string MenuName = "AmethystRoleTagMenu";

    // 菜单放大倍数：比原版大一圈，边缘就不会被会议 UI 遮到
    internal const float MenuScale = 1.1f;

    private static bool _active;
    private static ShapeshifterMinigame _menu;
    private static byte _targetId = byte.MaxValue;
    private static List<AmethystRoleTagStore.RoleEntry> _roles;

    internal static bool IsActive => _active;

    private static void ResetState()
    {
        RestoreCards();
        _menu = null;
        _roles = null;
        _active = false;
        _targetId = byte.MaxValue;
    }

    // ===== 打开 =====

    internal static void Open(byte targetPlayerId)
    {
        if (!AmethystRoleTagStore.Enabled) return;

        // ⚠ 死亡玩家不再允许标记（按钮已不生成，这里再挡一层：
        //   例如本局中途阵亡、或按钮被游戏重建的边界情况）。
        if (AmethystRoleTagStore.IsDeadPlayer(targetPlayerId)) return;

        // ⚠ 会议收尾保护：会议实例已经没了（或正在销毁）时不要再开选择器。
        //   否则选择器会把铭牌隐藏后，会议自己的 Update / Close 还会继续跑，
        //   原版 MeetingHud.Update 在铭牌被隐藏的状态下会空引用（用户日志里的闪退）。
        if (MeetingHud.Instance == null || MeetingHud.Instance.gameObject == null) return;

        // ⚠ 关键修复：用户点「X / 返回」时是**游戏自己**把菜单关掉的，我们的 Close() 不会被调用。
        // 若不在这里检测并复位，_active 会一直是 true —— 之后每次点 EditTag 都在下面直接 return，
        // 表现为「点过一次 X 之后就再也标记不了」。这里发现上次的菜单已经没了就复位。
        if (_active && (_menu == null || _menu.gameObject == null)) ResetState();
        if (_active) return;

        try
        {
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return;   // 面板要借本地玩家资料来渲染人物

            List<AmethystRoleTagStore.RoleEntry> roles = AmethystRoleTagStore.BuildRoleList();
            if (roles == null || roles.Count == 0) return;

            ShapeshifterMinigame prefab = GetShapeshifterMenuPrefab();
            if (prefab == null) return;

            Transform parent = null;
            if (Camera.main != null) parent = Camera.main.transform;
            else if (HudManager.Instance != null) parent = HudManager.Instance.transform;
            if (parent == null) return;

            _roles = roles;
            _targetId = targetPlayerId;
            _active = true;

            _menu = UnityEngine.Object.Instantiate(prefab, parent, false);
            if (_menu == null)
            {
                ResetState();
                return;
            }

            // 记住 prefab 自身的缩放，挂一个「每帧拉回目标缩放」的组件 ——
            // 只在 Open() 里赋值会被游戏后续的初始化覆盖，所以由组件每帧保持（目标是放大 1.1 倍）。
            Vector3 baseScale = _menu.transform.localScale;
            _menu.gameObject.name = MenuName;
            _menu.transform.localPosition = new Vector3(0f, 0f, -50f);
            _menu.gameObject.AddComponent<AmethystRoleTagMenuScale>().Init(baseScale);
            _menu.Begin(null);

            // Begin 里已经把面板建好了：把渲染器对齐到会议名字所在的图层并压过它
            MatchTopLayer(_menu.gameObject);
        }
        catch (Exception ex)
        {
            try { AmethystPlugin.Logger?.LogWarning($"RoleTag picker open failed: {ex.Message}"); } catch { }
            ResetState();
        }
    }

    // 完全照 MalumMenu：Shapeshifter 的 RoleBehaviour → ShapeshifterRole → 实例化 → .ShapeshifterMenu
    private static ShapeshifterMinigame GetShapeshifterMenuPrefab()
    {
        try
        {
            var all = RoleManager.Instance != null ? RoleManager.Instance.AllRoles : null;
            if (all == null) return null;

            RoleBehaviour behaviour = null;
            for (int i = 0; i < all.Count; i++)
            {
                RoleBehaviour b = all[i];
                if (b != null && b.Role == RoleTypes.Shapeshifter)
                {
                    behaviour = b;
                    break;
                }
            }
            if (behaviour == null) return null;

            ShapeshifterRole shifter = behaviour.TryCast<ShapeshifterRole>();
            if (shifter == null) return null;

            Transform holder = GameData.Instance != null ? GameData.Instance.transform : null;
            ShapeshifterRole temp = UnityEngine.Object.Instantiate(shifter, holder);
            if (temp == null) return null;

            ShapeshifterMinigame menu = temp.ShapeshifterMenu;
            UnityEngine.Object.Destroy(temp.gameObject);
            return menu;
        }
        catch { return null; }
    }

    // ===== 由 Begin 的 Prefix 调用：用身份列表填满菜单 =====

    internal static void Populate(ShapeshifterMinigame menu)
    {
        if (menu == null) return;

        List<AmethystRoleTagStore.RoleEntry> roles = _roles;
        if (roles == null) return;

        // 借本地玩家的资料给 SetPlayer —— 原版会据此渲染出格子里的「人物」。
        // （我们随后在 Postfix 里只把名字换成身份名，人物保留。）
        NetworkedPlayerInfo localData = null;
        try { localData = PlayerControl.LocalPlayer != null ? PlayerControl.LocalPlayer.Data : null; } catch { }
        if (localData == null) return;

        menu.potentialVictims = new Il2CppSystem.Collections.Generic.List<ShapeshifterPanel>();
        var selectable = new Il2CppSystem.Collections.Generic.List<UiElement>();

        for (int i = 0; i < roles.Count; i++)
        {
            int index = i;
            float x = menu.XStart + (i % 3) * menu.XOffset;
            float y = menu.YStart + (i / 3) * menu.YOffset;

            ShapeshifterPanel panel = UnityEngine.Object.Instantiate(menu.PanelPrefab, menu.transform);
            if (panel == null) continue;

            panel.transform.localPosition = new Vector3(x, y, -1f);

            // 原版 SetPlayer 照常执行（渲染人物 + 名字），我们的 Postfix 再覆盖名字
            panel.SetPlayer(i, localData, (Il2CppSystem.Action)(() => OnChosen(index)));

            menu.potentialVictims.Add(panel);
            selectable.Add(panel.Button);
        }

        try
        {
            HideMeetingCards();   // 面板期间藏掉会议铭牌，避免被压住
                        ControllerManager.Instance.OpenOverlayMenu(MenuName, menu.BackButton, menu.DefaultButtonSelected, selectable, false);
        }
        catch { }
    }

    // ===== 由 SetPlayer 的 Postfix 调用：把名字换成身份 =====

    internal static void RenderPanel(ShapeshifterPanel panel, int index)
    {
        if (panel == null) return;

        List<AmethystRoleTagStore.RoleEntry> roles = _roles;
        if (roles == null || index < 0 || index >= roles.Count) return;

        AmethystRoleTagStore.RoleEntry role = roles[index];

        // 只改这一行的名字与颜色；**保留人物**（用户要求看到人物），也不动背景与排版
        if (panel.NameText != null)
        {
            panel.NameText.text = role.Label;
            panel.NameText.color = role.Color;
        }

        // 等级与色盲色名是「玩家」信息，身份没有，清掉避免误导
        if (panel.LevelNumberText != null) panel.LevelNumberText.text = string.Empty;
        if (panel.ColorBlindName != null) panel.ColorBlindName.text = string.Empty;

        // 遮罩层**不要动**：原版 `SetPlayer` 用的是 `index + 2`，而原版变形者菜单本身
        // 就要容纳最多 15 名玩家（遮罩层 2..16），所以格子多是没问题的。
        // 上一版我"统一压回 2"是错的 —— 多个 PoolablePlayer 共用同一遮罩层会互相干扰。
    }

    // ===== 选中 / 关闭 =====

    private static void OnChosen(int index)
    {
        try
        {
            List<AmethystRoleTagStore.RoleEntry> roles = _roles;
            if (roles != null && index >= 0 && index < roles.Count && _targetId != byte.MaxValue)
            {
                AmethystRoleTagStore.RoleEntry entry = roles[index];
                if (entry != null && entry.IsClear) AmethystRoleTagStore.Clear(_targetId);        // 清除标记
                else if (entry != null) AmethystRoleTagStore.Set(_targetId, entry.Label, entry.Color);
            }
        }
        catch { }
        Close();
    }

    // ===== 第 147 轮：面板打开期间**临时隐藏会议铭牌** =====
    // 前几轮用 Sorting Layer / order 都没能彻底解决"死了人之后铭牌压住面板"，
    // 所以改用最直接、必然有效的办法：面板打开时把 `MeetingHud.playerStates`（12 个铭牌）隐藏，
    // 关闭时**保证恢复**（Close / ResetState / OnMenuDisabled / OnDestroy 四路都恢复）。
    private static readonly System.Collections.Generic.List<GameObject> _hidCards =
        new System.Collections.Generic.List<GameObject>();

    internal static void HideMeetingCards()
    {
        try
        {
            MeetingHud hud = MeetingHud.Instance;
            if (hud == null || hud.playerStates == null) return;

            for (int i = 0; i < hud.playerStates.Count; i++)
            {
                PlayerVoteArea pva = hud.playerStates[i];
                if (pva == null) continue;

                GameObject go = pva.gameObject;
                if (go == null || !go.activeSelf) continue;

                go.SetActive(false);
                if (!_hidCards.Contains(go)) _hidCards.Add(go);
            }
        }
        catch { }
    }

    internal static void RestoreCards()
    {
        try
        {
            for (int i = 0; i < _hidCards.Count; i++)
            {
                GameObject go = _hidCards[i];
                if (go != null && !go.activeSelf) go.SetActive(true);
            }
            _hidCards.Clear();
        }
        catch { }
    }
    internal static void Close()
    {
        ShapeshifterMinigame menu = _menu;
        ResetState();

        if (menu == null) return;

        // ⚠ 只调 Close()，**不要**紧接着 Destroy(menu.gameObject)！
        //
        // Minigame.Close() 会走它自己的收尾协程（CoStartClose / CoDestroySelf），
        // 其中包含把 ControllerManager 的遮罩菜单关掉、以及销毁自身。
        // 之前我在 Close() 之后立刻 Destroy，把那条协程打断了 →
        // 遮罩菜单一直处于打开状态，之后**所有点击都被它吞掉**。
        // MalumMenu 的做法也只是调 Close()，这里照做。
        try { menu.Close(); } catch { }
    }

    // 菜单被禁用（包括玩家点 X / 返回键让游戏自己关掉）时复位状态。
    // 这是「点过 X 之后就再也标记不了」的根治点。
    internal static void OnMenuDisabled(ShapeshifterMinigame menu)
    {
        if (menu == null) return;
        if (_menu != null && _menu != menu) return;   // 不是我们开的那一个，忽略
        ResetState();
    }

    // 会议结束/关闭时兜底清理，避免菜单残留
    internal static void Cleanup() => Close();

    // 把菜单下所有渲染器抬到**会议 HUD 里最上层的那个渲染器之上**，
    // 从而压过会议铭牌（卡片美术 + 名字 + 按钮）。
    //
    // ⚠ 第 131 轮修正（用户反馈"部分会议铭牌被遮挡"又出现）：
    //   原实现拿 `PlayerVoteArea.NameText`（**名字文字**）当参照，但它并不是会议里最高的东西 ——
    //   盖住我们格子的是铭牌的**卡片美术**，它可能在**另一个 Sorting Layer**、或同层但 order 更高。
    //   （图层顺序优先于同层 order，所以只比 NameText 必然漏。）
    //   现在：遍历整个 `MeetingHud`，取 (图层值, order) **最大**的那个渲染器当参照，
    //   把我们所有渲染器都放到**同一个图层**，并把最小 order 抬到它之上。
    internal static void MatchTopLayer(GameObject root)
    {
        if (root == null) return;

        // 1) 找会议 HUD 里"最上面"的渲染器
        string refLayer = null;
        int refOrder = 0;
        int refLayerValue = int.MinValue;

        try
        {
            MeetingHud hud = MeetingHud.Instance;
            if (hud != null && hud.gameObject != null)
            {
                Renderer[] mrs = hud.gameObject.GetComponentsInChildren<Renderer>(true);
                if (mrs != null)
                {
                    for (int i = 0; i < mrs.Length; i++)
                    {
                        Renderer r = mrs[i];
                        if (r == null) continue;

                        int lv;
                        try { lv = SortingLayer.GetLayerValueFromName(r.sortingLayerName); }
                        catch { lv = 0; }

                        if (lv > refLayerValue || (lv == refLayerValue && r.sortingOrder > refOrder))
                        {
                            refLayerValue = lv;
                            refOrder = r.sortingOrder;
                            refLayer = r.sortingLayerName;
                        }
                    }
                }
            }
        }
        catch { }

        // 兜底：拿不到就退回"按会议名字文字"的旧参照
        if (string.IsNullOrEmpty(refLayer))
        {
            try
            {
                MeetingHud hud = MeetingHud.Instance;
                if (hud != null && hud.playerStates != null)
                {
                    for (int i = 0; i < hud.playerStates.Count; i++)
                    {
                        PlayerVoteArea pva = hud.playerStates[i];
                        if (pva == null || pva.NameText == null) continue;

                        Renderer nr = pva.NameText.GetComponent<Renderer>();
                        if (nr == null) continue;

                        refLayer = nr.sortingLayerName;
                        refOrder = nr.sortingOrder;
                        break;
                    }
                }
            }
            catch { }
        }

        // ⚠ 第 146 轮（用户建议"直接把图层改成最高"）：
        // 之前是"对齐到会议 HUD 里最上层的那个渲染器"，但死亡铭牌/卡片可能来自**另一个层级组合**，
        // 每格渲染器的相对高低还是会被压住。现在直接把我们的所有渲染器放到
        // **SortingLayer.layers 里的最后一个（= 最高图层）**，再配 30000+ 的 order，
        // 不再依赖会议那边是几层。
        try
        {
            SortingLayer[] all = SortingLayer.layers;
            if (all != null && all.Length > 0)
            {
                string top = all[all.Length - 1].name;
                if (!string.IsNullOrEmpty(top)) refLayer = top;
            }
        }
        catch { }

        if (string.IsNullOrEmpty(refLayer)) return;   // 拿不到参照就不动，避免把菜单搞乱

        try
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            // ⚠ **整体平移**而不是统一赋值：统一赋值会把菜单内部层次（底板/人物/文字）压平
            //   （曾导致"12 个格子只有 3 个有人物"）。
            int min = int.MaxValue;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (r.sortingOrder < min) min = r.sortingOrder;
            }
            if (min == int.MaxValue) return;

            // 目标：同图层 + 远高于会议最上层（再留 500 余量，并用 30000 兜底抬高）
            int target = Mathf.Max(refOrder + 500, 30000);
            if (target > 32767) target = 32767;

            // ⚠ 第 145 轮修正：这里**不能因为"已经在上面了"就 return**。
            // 原因（用户复现）：开局正常，**死了几个人之后**游戏会把死亡铭牌的渲染器换到
            // **更高的 Sorting Layer**（图层优先于同层 order），而我们的 order 早已是 30000+，
            // 于是 shift <= 0 直接 return ⇒ 图层永远纠正不回来 ⇒ 铭牌又压住面板。
            // 现在：shift 只夹到 0，循环每次都跑，**图层每帧都重新对齐**。
            int shift = target - min;
            if (shift < 0) shift = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                if (r.sortingLayerName != refLayer) r.sortingLayerName = refLayer;

                int want = r.sortingOrder + shift;
                if (want > 32767) want = 32767;
                if (r.sortingOrder != want) r.sortingOrder = want;
            }
        }
        catch { }
    }
}

// 保持变形者菜单的**原本大小**，并每帧兜住渲染层级 / 会议名字隐藏。
// 只在 Open() 里赋值会被游戏自己的初始化流程覆盖，所以用组件每帧复位一次，直到菜单被销毁。
internal sealed class AmethystRoleTagMenuScale : MonoBehaviour
{
    private Vector3 _base = Vector3.one;
    private int _tick;

    public AmethystRoleTagMenuScale(IntPtr ptr) : base(ptr) { }

    internal void Init(Vector3 baseScale) => _base = baseScale;

    private void OnDestroy() { try { AmethystRoleTagPicker.RestoreCards(); } catch { } }


    
    private void LateUpdate()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_RoleTagMenu);
    try { LateUpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_RoleTagMenu); }
    }

    private void LateUpdateInner()
    {
            try
            {
                // 按要求放大 1.1 倍：比原版大一圈，边缘就不会被会议 UI 遮到。
                // 只在 Open() 里赋值会被游戏自身的初始化覆盖，所以每帧拉回目标值。
                Vector3 want = _base * AmethystRoleTagPicker.MenuScale;
                if (transform.localScale != want) transform.localScale = want;

                _tick = _tick >= 20 ? 0 : _tick + 1;
                if (_tick == 0)
                {
                    // 面板是在 Begin 里动态创建的，可能有漏网的渲染器
                    AmethystRoleTagPicker.MatchTopLayer(gameObject);
                }
            }
            catch { }
    
    }
}

// 接管变形者菜单的内容
[HarmonyPatch(typeof(ShapeshifterMinigame), "Begin")]
internal static class AmethystRoleTagMenuBeginPatch
{
    private static bool Prefix(ShapeshifterMinigame __instance)
    {
        if (!AmethystRoleTagPicker.IsActive) return true;
        try { AmethystRoleTagPicker.Populate(__instance); } catch { }
        return false;
    }
}

// 原版渲染完（人物 + 玩家名）之后，把名字覆盖成身份名
// 参数名必须与原方法一致，Harmony 按名字绑定
[HarmonyPatch(typeof(ShapeshifterPanel), "SetPlayer")]
internal static class AmethystRoleTagPanelPatch
{
    private static void Postfix(ShapeshifterPanel __instance, int index, NetworkedPlayerInfo playerInfo, Il2CppSystem.Action onShift)
    {
        if (!AmethystRoleTagPicker.IsActive) return;
        try { AmethystRoleTagPicker.RenderPanel(__instance, index); } catch { }
    }
}

// 菜单被关掉（含玩家点 X / 返回键）时复位状态，否则 _active 永远为 true、再也打不开
[HarmonyPatch(typeof(ShapeshifterMinigame), "OnDisable")]
internal static class AmethystRoleTagMenuDisablePatch
{
    private static void Postfix(ShapeshifterMinigame __instance)
    {
        try { AmethystRoleTagPicker.OnMenuDisabled(__instance); } catch { }
    }
}

// 会议关闭时清理
[HarmonyPatch(typeof(MeetingHud), "OnDestroy")]
internal static class AmethystRoleTagMenuCleanupPatch
{
    private static void Postfix() => AmethystRoleTagPicker.Cleanup();
}

// ⚠ 第 201 轮闪退修复：会议一开始收尾就**立刻**恢复铭牌、关掉选择器，
//   不要拖到 MeetingHud.OnDestroy（那时游戏收尾逻辑已经在跑，铭牌若仍隐藏着，
//   原版 MeetingHud.Update 会在这一两帧里空引用 → 闪退）。
//   Close 比 OnDestroy 早，且是会议结束的正式入口；这里做一次幂等清理。
[HarmonyPatch(typeof(MeetingHud), "Close")]
internal static class AmethystRoleTagMenuClosePatch
{
    private static void Postfix() => AmethystRoleTagPicker.Cleanup();
}
