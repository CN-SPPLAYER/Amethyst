using System;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst;

// 界面优化：把每位玩家头顶名字渲染为其游戏颜色（含自己）；若本地玩家是内鬼，则队友显示为红色。
// 自由模式 / 大厅 / 游戏内都生效。
//
// ===== 第 179 轮性能重写 =====
//
// 【旧实现的问题】
//   每个 LateUpdate 都 `foreach (PlayerControl.AllPlayerControls)` 全量走一遍，
//   并且**每名玩家每帧**都去读这些原生成员：
//       pc.cosmetics / pc.cosmetics.nameText / pc.Data / pc.Data.PlayerName
//       pc.shapeshiftTargetPlayerId / pc.CurrentOutfitType / pc.CurrentOutfit
//       text.text（★ 读 TMP.text 会**分配一个托管字符串**）
//   10 人局下每帧几十次跨 IL2CPP 边界调用 + 几十次字符串分配。
//   GC 每隔几秒被触发一次 → 体感就是"这模组卡"。
//
// 【现在】
//   所有"读原生 / 分配字符串 / 构建富文本"的工作都搬到
//   `AmethystPlayerCacheStore` 的**错峰刷新**里（每名玩家每 20 帧一次），
//   本组件的每帧循环只做**纯托管字段比较**：没有跨边界调用、没有分配，
//   只在"目标值与已写出值不同"时才碰一次 TMP。
//
// 【功能保持】
//   · 自己那一格仍然只改颜色、不改文本（变形时文本是伪装名，动了就把伪装抹掉）；
//   · 别人变形中仍然完全不碰（保留游戏自己的伪装显示）；
//   · 内鬼看队友仍然是纯红 #FF0000；
//   · 蘑菇混合破坏期间所有人按"当前身体色"上色（隐藏与否仍由 AmethystMushroomMixup 管）。
public sealed class AmethystNameRendering : MonoBehaviour
{
    public AmethystNameRendering(IntPtr ptr) : base(ptr) { }

    private void LateUpdate()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_NameFlush);
    try { LateUpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_NameFlush); }
    }

    private void LateUpdateInner()
    {
            if (!AmethystColors.NameColorsOn) return;

            // 蘑菇混合破坏：原版在正常对局里会隐藏名字，**自由模式下不会** ——
            // 由我们隐藏 / 复原（见 AmethystMushroomMixup，参考 TownofNext 的写法）。
            Patches.AmethystMushroomMixup.Tick();

            FlushNames();

            // 我们自己借变形者菜单做「身份标记」时**必须跳过** ——
            // 那里名字要显示成身份名（内鬼/船员…），不是玩家名。
            if (!Patches.AmethystRoleTagPicker.IsActive) ColorShifterMenu();
    
    }

    // ===== 把错峰算好的待写值落下去 =====
    //
    // ⚠ 本循环里**没有**任何 `pc.Data` / `pc.cosmetics` / `text.text` 读取。
    //   所有判断都用快照上的托管字段（int / string 引用比较）。
    //   写入本身仍然要做（那是最终目的），但只在值真的变化时发生。
    private static void FlushNames()
    {
        try
        {
            bool mixup = Patches.AmethystMushroomMixup.ActiveNow();
            var list = AmethystPlayerCacheStore.All;

            AmethystNameDiag.Tick(list, mixup);

            // ===== 第 226 轮：死亡后强制显示所有人姓名（躲猫猫会隐藏名字）=====
            //
            // 用户反馈：「房主在躲猫猫里开启隐藏名称后，我作为船员死亡，
            //   部分幽灵名字一闪一闪的 —— 显示一下又不见了，又有一部分幽灵一直显示」。
            //
            // 【成因】旧代码把这条断言压成了 `(frame % 10 == 0)`：每 10 帧才把名字的
            //   `enabled / gameObject.activeSelf` 顶回可见一次，其余 9 帧不管。
            //   而原版在躲猫猫里（`BoolOptionNames.ShowCrewmateNames` 关掉时）是**每帧**
            //   把该藏的名字藏回去的（`CosmeticsLayer.ToggleNameVisible(false)` →
            //   `nameText.gameObject.SetActive(false)`）。
            //   ⇒ 10 帧里我们只赢 1 帧，渲染出来就是 **6 Hz 的一闪一闪**。
            //   而原版**本来就没藏**的那些名字（自己的名字、以及按原版逐人规则不需要藏的格）
            //   一直可见 ⇒ 与"又有一部分幽灵显示的"完全对得上。
            //
            // 【为什么低频断言会输、每帧断言就能赢】症状本身就是证据：既然我们每 10 帧的
            //   写入能"显示一下"，就说明**渲染前最后写的那个是我们**（原版藏名字发生在我们
            //   LateUpdate 之前，否则我们写的那一帧也该看不见）。把断言提到每帧＝每帧都由
            //   我们收尾 ⇒ 稳定压住，而不会反过来变成"永远看不见"。
            //
            // 【开销】只有「躲猫猫 + 本地玩家已死」才每帧断言（每名玩家 1~2 次 bool 读，
            //   且 `enabled` 已是目标值时一次写入都不发生）；其余情况保持原来的每 10 帧
            //   低频兜底 ⇒ 经典局 / 活着时的开销与旧版逐字相同。
            int frame = Time.frameCount;
            bool forceAllNames = AmethystPlayerCacheStore.LocalDead && !mixup
                                 && (InHideAndSeek() || frame % 10 == 0);

            for (int i = 0; i < list.Count; i++)
            {
                AmethystPlayerCache c = list[i];
                if (c == null) continue;

                // 第 182 轮：脏的**当场**刷新。`Tick()` 在 Update 阶段跑，
                // 而游戏可能在我们之后才创建玩家 —— 那一帧若不在这里补刷，
                // 色盲文字（被 GetColorBlindText 补丁当场染色）就有了、名字还没有。
                AmethystPlayerCacheStore.RefreshIfDirty(c);

                TextMeshPro text = c.NameText;
                if (text == null) continue;

                // 本地玩家**死亡后**强制显示所有人名称（躲猫猫会隐藏名字）；
                // 未死亡前，别人的名字按游戏设置，只有自己始终可见。
                // ⚠ 第 226 轮：躲猫猫里这个断言是**每帧**执行的（原因见本函数开头），
                //   其余模式才是原来的每 10 帧低频兜底。
                if (forceAllNames)
                {
                    try
                    {
                        if (!text.enabled) text.enabled = true;
                        if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
                    }
                    catch { }
                }

                // ---- ⓪ 蘑菇混合破坏期间：**所有人**都按"当前身体颜色"上色 ----
                // 名字本身是否可见由 `AmethystMushroomMixup` 统一管（破坏期间隐藏）。
                if (mixup)
                {
                    ApplyBodyColor(c, text);
                    continue;
                }

                // ---- ① 本地玩家自己：只改颜色，不动文本 ----
                if (c.Id == AmethystPlayerCacheStore.LocalId)
                {
                    // 自己的名字在管道之外始终可见；某些模式（躲猫猫）会隐藏名字，
                    // 强制显示自己（别人仍按游戏设置）。但在管道内（InVent）必须隐藏，否则会暴露。
                    bool isVisible = !c.InVent;
                    try
                    {
                        if (text.enabled != isVisible) text.enabled = isVisible;
                        if (text.gameObject.activeSelf != isVisible) text.gameObject.SetActive(isVisible);
                    }
                    catch { }

                    if (isVisible) ApplyBodyColor(c, text);
                    continue;
                }

                // ---- ② 别人 ----
                // 变形中不碰（保留游戏自己的伪装显示）——判断来自快照，不读原生。
                if (c.IsShapeshifting || !c.OutfitIsDefault) continue;

                if (c.PendingNameText != null)
                {
                    try { text.text = c.PendingNameText; } catch { }
                    c.WrittenNameText = c.PendingNameText;
                    // 第 184 轮：记下「写进了哪个 TMP 实例」。
                    // 游戏重建 TMP 后，靠这一项发现「值没变但对象换了」，从而重新写一次。
                    c.WrittenForName = text;
                    c.PendingNameText = null;
                }
            }
        }
        catch { }
    }

    // 姓名颜色 = **当前身体实际显示的颜色**（自己、以及蘑菇混合破坏期间的每个人）。
    //
    // 顶点色 + 富文本两条都写：
    //   · 顶点色：游戏若随后把 text 内容重写成纯名字，颜色也还是对的；
    //   · 富文本：把**当前显示的名字原样**包成该颜色（不替换名字内容）。
    private static void ApplyBodyColor(AmethystPlayerCache c, TextMeshPro text)
    {
        int id = c.ShownColorId;
        if (id < 0) return;

        // ① 顶点色
        //
        // ⚠ 第 181 轮：以前只在 `WrittenVertexColorId != id`（即"我们认为还没写过"）时
        //   才去碰 TMP。但 `WrittenVertexColorId` 记的是**我们的意图**，不是 TMP 的实际状态 ——
        //   变形时游戏会重写这个文本组件（连同颜色），我们却因为"以为写过了"而不再管，
        //   表现出来就是「变形后有一两帧名字没有颜色」。
        //
        //   现在每帧**读一次实际值再比对**：`TMP.color` 是值类型读取，不分配字符串，
        //   每名玩家每帧一次，开销可忽略；不一致就补写，于是天然自愈。
        try
        {
            Color target = AmethystColors.ColorOf(id);
            Color cur = text.color;
            target.a = cur.a;                       // 保留原透明度（幽灵 / 淡入淡出）
            if (!SameRgb(cur, target)) text.color = target;
            c.WrittenVertexColorId = id;
            c.PendingVertexColorId = int.MinValue;
        }
        catch { }

        // ② 富文本：把**当前显示的名字原样**包成该颜色。
        //
        // ⚠ 第 179 轮：守卫条件只能是 `AppliedBodyWrapId != id`。
        //   之前写成 `WrittenNameText == null || AppliedBodyWrapId != id` —— 而破坏期间
        //   名字是空的（`AmethystMushroomMixup` 把名字隐藏了），于是 `inner` 为空、
        //   `WrittenNameText` 永远不会被赋值，条件**每帧都成立** ⇒ 每帧每名玩家读一次
        //   `text.text`（★ 分配托管字符串）。正好把这一轮要消灭的开销又原样搬了回来。
        if (c.AppliedBodyWrapId != id)
        {
            string hex = AmethystColors.Hex(id);
            if (!string.IsNullOrEmpty(hex))
            {
                try
                {
                    string live = text.text;
                    string inner = UnwrapColor(live);
                    if (!string.IsNullOrEmpty(inner))
                    {
                        string want = $"<color={hex}>{inner}</color>";
                        if (live != want) text.text = want;
                        c.WrittenNameText = want;
                    }
                    c.AppliedBodyWrapId = id;
                }
                catch { }
            }
        }
    }

    private static bool SameRgb(Color a, Color b)
        => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

    // ===== 当前是不是躲猫猫（HideNSeek / SeekFools）=====
    //
    // 只有躲猫猫会在 `Show Crewmate Names` 关掉时**每帧**藏名字，也就是唯一需要我们
    // 每帧顶回来的模式（见 `FlushNames` 里的第 226 轮说明）。
    //
    // 每帧缓存一次（整帧一次原生调用）：判定与 `AmethystRolePanel.IsHideAndSeekMode()` /
    // `AmethystRpcGuard.IsHS()` 一致 —— `GameManager.IsHideAndSeek()` 优先，
    // 再退到 `CurrentGameOptions.GameMode`（`SeekFools` 这种变体也覆盖到）。
    private static int _hsFrame = -1;
    private static bool _hsCached;

    private static bool InHideAndSeek()
    {
        int frame = Time.frameCount;
        if (frame == _hsFrame) return _hsCached;
        _hsFrame = frame;
        try
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.IsHideAndSeek()) { _hsCached = true; return true; }

            GameOptionsManager gom = GameOptionsManager.Instance;
            var opts = gom != null ? gom.CurrentGameOptions : null;
            _hsCached = opts != null && (opts.GameMode is GameModes.HideNSeek or GameModes.SeekFools);
        }
        catch { _hsCached = false; }
        return _hsCached;
    }

    // 去掉我们自己包上去的 <color=...>...</color>，避免每帧层层嵌套
    private static string UnwrapColor(string s)
    {
        const string Close = "</color>";
        if (string.IsNullOrEmpty(s)) return s;

        for (int guard = 0; guard < 4; guard++)
        {
            if (!s.StartsWith("<color=", StringComparison.Ordinal)) break;
            int gt = s.IndexOf('>');
            if (gt < 0 || !s.EndsWith(Close, StringComparison.Ordinal)) break;
            s = s.Substring(gt + 1, s.Length - gt - 1 - Close.Length);
        }
        return s;
    }

    // ===== 变形者菜单里的名字 =====
    //
    // 菜单里的名字是 `ShapeshifterPanel.NameText`，与头顶那套（`cosmetics.nameText`）完全独立。
    //
    // ⚠ 这一段**不能**走 AmethystPlayerCache：面板列表与玩家列表的下标并不一一对应，
    //   而且菜单只在变形者打开时存在（极短时间）。所以这里保留原逻辑，但：
    //     · 只在菜单**激活**时才跑（缓存 + 激活校验 + 0.25s 限流重搜）；
    //     · 菜单没开时整个函数立即返回 —— 绝大多数时间开销为 0。
    private static ShapeshifterMinigame _shifterMenu;

    // 由 `AmethystShifterMenuCachePatch`（挂在 `ShapeshifterMinigame.Begin` 的 Postfix）
    // 在游戏**自己打开**菜单的那一刻把实例交过来。
    //
    // ⚠ 第 180 轮性能：旧实现是每 0.25 秒 `FindObjectOfType<ShapeshifterMinigame>()`。
    //   `FindObjectOfType` 是**全场景遍历**，而菜单绝大多数时间根本不存在 →
    //   这个调用永远返回 null，等于每秒白跑 4 次全场景扫描，永远找不到任何东西。
    //   现在改成事件驱动：游戏打开菜单时我们被动拿到引用，平时开销为 0。
    internal static void SetShifterMenu(ShapeshifterMinigame menu) => _shifterMenu = menu;

    private static ShapeshifterMinigame CurrentShifterMenu()
    {
        ShapeshifterMinigame cached = _shifterMenu;
        if (cached == null) return null;                 // 常态：一次字段读，结束
        try
        {
            if (cached.gameObject.activeInHierarchy) return cached;
        }
        catch { }
        _shifterMenu = null;                              // 对象已被销毁
        return null;
    }

    private static void ColorShifterMenu()
    {
        try
        {
            ShapeshifterMinigame menu = CurrentShifterMenu();
            if (menu == null || menu.potentialVictims == null) return;

            var list = AmethystPlayerCacheStore.All;
            if (list.Count == 0) return;

            for (int i = 0; i < menu.potentialVictims.Count; i++)
            {
                ShapeshifterPanel panel = menu.potentialVictims[i];
                if (panel == null || panel.NameText == null) continue;

                // 面板本身不直接持有玩家引用，用「面板上的名字」反查对应玩家。
                // 面板显示的就是玩家当前名字，所以按名字匹配是可靠的。
                string shown = panel.NameText.text;
                if (string.IsNullOrEmpty(shown)) continue;

                for (int j = 0; j < list.Count; j++)
                {
                    AmethystPlayerCache c = list[j];
                    if (c == null || string.IsNullOrEmpty(c.Name)) continue;
                    if (shown.IndexOf(c.Name, StringComparison.Ordinal) < 0) continue;

                    // 变形中的人不套用（与头顶一致）——来自快照
                    if (c.IsShapeshifting || !c.OutfitIsDefault) break;

                    string hex = c.Id == AmethystPlayerCacheStore.LocalId
                        ? AmethystColors.Hex(c.ShownColorId)
                        : (AmethystPlayerCacheStore.LocalIsImp && c.IsImpostor && c.Id != AmethystPlayerCacheStore.LocalId
                            ? "#FF0000"
                            : AmethystColors.Hex(c.ColorId));
                    if (string.IsNullOrEmpty(hex)) break;

                    string want = $"<color={hex}>{c.Name}</color>";
                    if (shown != want) panel.NameText.text = want;
                    break;
                }
            }
        }
        catch { }
    }
}

// 变形者菜单的实例由事件驱动缓存（第 180 轮，替代每 0.25 秒一次的
// `FindObjectOfType` 全场景扫描）。`Begin(PlayerTask)` 已用 dnSpy 核实存在。
// 用 `[HarmonyPatch(Type, string)]`（与本工程其它补丁一致），不要写 `Harmony.Patch` ——
// 本文件 `using HarmonyLib;` 之后裸写 `Harmony` 会被就近解析成 `HarmonyLib.Harmony` 那个**类**。
[HarmonyPatch(typeof(ShapeshifterMinigame), "Begin")]
internal static class AmethystShifterMenuCachePatch
{
    private static void Postfix(ShapeshifterMinigame __instance)
        => AmethystNameRendering.SetShifterMenu(__instance);
}