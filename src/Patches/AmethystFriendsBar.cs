using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 好友条 / 好友码 UI —— 完全照抄 FinalSuspect 1.4 的 `Patches\System\AccountManagerPatch.cs`。
//
// ===== 参考原文（FS AccountManagerPatch.cs:6-24）=====
//     [HarmonyPatch(typeof(AccountTab), "Awake")]
//     public static class AwakeFriendCodeUIPatch
//     {
//         public static void Prefix()
//         {
//             var barSprit = GameObject.Find("BarSprite");
//             if (barSprit)
//                 barSprit.GetComponent<SpriteRenderer>().color = Color.clear;
//
//             if (FriendsButton != null) return;
//             FriendsButton = GameObject.Find("FriendsButton");
//             FriendsButton.transform.FindChild("Highlight").FindChild("NewRequestActive")
//                 .FindChild("Background").gameObject.GetComponent<SpriteRenderer>()
//                 .color = Color.white.AlphaMultiplied(0.3f);
//             FriendsButton.transform.FindChild("Inactive").FindChild("NewRequestInactive")
//                 .FindChild("Background").gameObject.GetComponent<SpriteRenderer>()
//                 .color = Color.white.AlphaMultiplied(0.3f);
//         }
//     }
//
//     [HarmonyPatch(typeof(AccountManager), "Awake")]
//     public static class AwakeAccountManager
//     {
//         public static void Prefix(AccountManager __instance) { RoleIllustrationManager.Create(__instance); }
//     }
//
// ===== 与我们第 189~195 轮那版的差异（都是我们自找的麻烦）=====
//
// ① 挂点是 **Prefix**，我们用的是 Postfix。`AccountTab.Awake` 期间子物体还没完全就绪，
//    FS 选 Prefix 是在原版 Awake **之前**就把样式改掉，避免首帧闪一下再变色。
//
// ② FS 只做三件事，全部是**改颜色**：
//        BarSprite 底板 → Color.clear（透明）
//        两个「新请求」角标底板 → Color.white × 0.3（压暗）
//    我们第 193 轮额外做了：关掉按钮的 `Shine` 子物体、统一按钮精灵、给好友按钮染紫。
//    那些是照搬主菜单大按钮的做法，但**好友按钮结构不同** —— 它的 Inactive/Highlight
//    子树里挂着 `NewRequestActive` 等逻辑节点，游戏靠它们的状态决定角标显隐。
//    我们动那些等于破坏它的状态机。这一轮全部去掉，只留 FS 的三步。
//
// ③ FS 用 `GameObject.Find("BarSprite")` / `Find("FriendsButton")` —— **裸名字**。
//
//  ★ 第 197 轮纠正（这里之前写着**反了**）：
//    本文件第 189/191 轮那版注释断言"`GameObject.Find` 是全场景按名字查找、
//    能命中嵌套对象，所以裸名字可用" —— **这个论断是错的**。
//    `GameObject.Find`（以及 `GameObject.Find(name)` 的单段形式）
//    **只匹配顶层对象**；要跨层级必须写成 `Find("A/B/C")` 从某个顶层对象出发。
//
//    BarSprite / FriendsButton 都位于 `AccountManager/AccountTab/GameHeader/BarSprite/...`
//    之下，不是顶层对象 ⇒ 裸名字**必然返回 null**。
//    这就是"角标压暗"长期是 no-op、以及日志里出现"看起来正常"的兜底值的原因
//    （项目文档 4128 / 4282 行已在 BarSprite、FriendsButton 上连续两轮实测纠正过这条）。
//
//    现状：改用下面的 `FindDeep()` —— 优先从 `AccountManager.Instance.transform`
//    走 `Transform.Find(相对路径)`，失败再退回 `GameObject.Find(全路径)` 兜底。
//    不再使用裸名字。
internal static class AmethystFriendsBar
{
    // FS 把找到的按钮存起来做"只处理一次"的守卫
    private static GameObject _friendsButton;
    private static bool _applied;
    private static bool _dumped;      // 第 199 轮：一次性层级转储，确认按钮结构用

    // ⚠ 第 199 轮**已撤销**：把底板换成主菜单的"统一小精灵"（`MinorActive`）。
    //
    //   实测数据：`旧高=0.620  新精灵高=0.440  补偿后 localScale.y=1.4091`。
    //   当时只按**高度**做了补偿，忘了宽度 —— 而这两张底板的**宽高比根本不同**：
    //     · 好友按钮 = 右上角账号栏里接近**正方形**的小图标按钮（高 0.62 ⇒ 宽也≈0.6）
    //     · MinorActive = 左面板那种**扁宽矩形**按钮（高 0.44，宽是高的 4~5 倍）
    //   同高补偿之后宽度仍是原来的 4~5 倍 ⇒ 按钮胀成一大条，
    //   连hover 判定用的碰撞盒范围都被撑大，用户反馈"一靠近就超级大"。
    //
    //   结论：**这两种形状不兼容，换精灵这条路走不通**。
    //   想让右上角真的变成左面板那种宽按钮，只能整体重做成一个带文字的
    //   新按钮对象（而不是换精灵），那是另一种改法，需要用户明确要。
    //
    //   保留的只有"上色"部分：`Highlight`/`Inactive` 两态底板染主题色
    //   + 角标 Background/Shine 压暗。这部分是安全的。

    internal static bool Enabled => AmethystConfig.MainMenuStyle != null && AmethystConfig.MainMenuStyle.Value;

    /// <summary>对齐 FS 的 `AwakeFriendCodeUIPatch`（Prefix）。</summary>
    internal static void Apply()
    {
        try
        {
            if (!Enabled) return;

            // ① 顶栏底板透明
            //    FS 用裸名字 `GameObject.Find("BarSprite")`，但裸名字**只匹配顶层对象**，
            //    而 BarSprite 实际位于 `AccountManager/AccountTab/GameHeader/BarSprite`
            //    （本文件第 48-51 行的注释推断"裸名字能找到嵌套对象"是**错的**，
            //      项目文档 4128 行已经实测纠正过）。
            //    这里改成"完整路径优先 + 裸名字兜底"，两条路都试。
            GameObject bar = FindDeep("AccountManager/AccountTab/GameHeader/BarSprite")
                             ?? GameObject.Find("BarSprite");
            if (bar != null)
            {
                var sr = bar.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = Color.clear;
            }

            // ②③ 角标压暗
            if (_friendsButton != null)
            {
                if (!_applied) { _applied = true; Log("底板透明 + 角标压暗（已处理）"); }
                return;
            }

            _friendsButton = FindFriendsButton();
            if (_friendsButton == null) return;   // 找不到就安静退出，下一次 Awake 再试

            // FS: Highlight/NewRequestActive/Background → Color.white.AlphaMultiplied(0.3f)
            // FS: Inactive/NewRequestInactive/Background  → 同上
            // 角标：底板 / 数量文字 / 高光 三处一起改成主题色（递归查找，不依赖具体层级）。
            DimBadge("Highlight", "NewRequestActive");
            DimBadge("Inactive", "NewRequestInactive");

            _applied = true;
            Log("底板透明 + 角标压暗（首次）");
        }
        catch { }
    }

    // ===== 第 199 轮：好友按钮染主题色 =====
    //
    // 做法与 `AmethystMainMenuStyle.Tint` 完全一致 —— 走 `PassiveButton` 的
    // `activeSprites` / `inactiveSprites` 两个 GameObject，取其 `SpriteRenderer`
    // 直接写 `color`。这样按钮的**形状/尺寸/高光**一概不动，只换颜色。
    //
    // ⚠ **必须反复断言，不能只染一次**。
    //   `AmethystMainMenuStyle.cs:188-191` 已经踩过这个坑并留下记录：
    //   游戏在按钮状态切换时会自己重设 `SpriteRenderer.color`，
    //   一次性标志会导致"染完又被刷回原版青绿"。所以本方法由
    //   `AmethystMainMenuOptimize.TickLive()`（主界面 0.5s 一次）驱动，反复上色。
    internal static void TickLive()
    {
        try
        {
            if (!Enabled) return;

            // ⚠ 第 200 轮：一次性部分（顶栏底板透明 + 角标压暗）改由这里驱动，
            //   因为两个 `Awake` 挂点都已删除（理由见文件末尾的注释）。
            //   这里运行时账号 UI 早已建好，`AccountManager.Instance` 一定是安全的。
            if (!_applied) Apply();

            GameObject btn = _friendsButton != null ? _friendsButton : FindFriendsButton();
            if (btn == null) return;
            _friendsButton = btn;

            PassiveButton pb = btn.GetComponent<PassiveButton>();
            if (pb == null) { DumpOnce(btn, "按钮上没有 PassiveButton"); return; }

            Color accent = ThemeAccent();

            foreach (var sprite in btn.GetComponentsInChildren<SpriteRenderer>(true))
                AmethystUiSpriteTone.Apply(sprite);
            DimBadge("Highlight", "NewRequestActive");
            DimBadge("Inactive", "NewRequestInactive");

            // 只上色，**不动精灵、不动 transform**。
            // 换统一底板精灵的方案已在上一轮实测失败并撤销，理由见字段区注释。
            //
            // 走 activeSprites / inactiveSprites —— 与主菜单按钮同一套。
            // 这两个节点就是按钮的两态底板（Among Us 原生结构）。
            int tinted = 0;
            tinted += TintState(pb.activeSprites, accent, "activeSprites");
            tinted += TintState(pb.inactiveSprites, accent, "inactiveSprites");

            // 兜底：万一这两个字段为空（结构随游戏版本变了），直接在
            // Highlight / Inactive 两个状态容器下找 Background 染。
            // 排除 NewRequest* 子树 —— 那些是角标，由 DimBadge 负责压暗。
            if (tinted == 0)
            {
                tinted += TintNamedChild(btn, "Highlight", accent);
                tinted += TintNamedChild(btn, "Inactive", accent);
            }

            DumpOnce(btn, tinted > 0
                ? $"已染主题色 {HexOf(accent)}：命中 {tinted} 个渲染器（底板精灵保持原版，未换）"
                : "没染上：activeSprites/inactiveSprites 与 Highlight/Inactive.Background 都没命中");
        }
        catch { }
    }

    // 主题色。与 `AmethystStyle.Current.Accent` 同源（固定 #A06EFF），
    // 但这里自己取一遍，避免 Menu 层反向依赖 Patches 层。
    // ⚠ 第 200 轮：改为**与「开始」按钮同一个色值**（`AmethystMainMenuStyle.TierPrimary = #A855F7`）。
    //   用户明确要求好友按钮不要用模组 accent(#A06EFF)，要和起始按钮一致。
    private static Color ThemeAccent()
    {
        try { return AmethystMainMenuStyle.TierPrimaryColor; }
        catch { return new Color32(0xA8, 0x55, 0xF7, 0xFF); }
    }

    private static int TintState(GameObject state, Color c, string what)
    {
        try
        {
            if (state == null) return 0;
            var sr = state.GetComponent<SpriteRenderer>();
            if (sr == null) return 0;
            // 保留原透明度（alpha=0 的节点说明是被刻意关掉的，别点亮它）
            sr.color = new Color(c.r, c.g, c.b, sr.color.a);
            return 1;
        }
        catch { return 0; }
    }

    private static int TintNamedChild(GameObject root, string child, Color c)
    {
        try
        {
            Transform t = root.transform.FindChild(child);
            if (t == null) return 0;
            int n = 0;
            var srs = t.GetComponentsInChildren<SpriteRenderer>(true);
            if (srs == null) return 0;
            for (int i = 0; i < srs.Length; i++)
            {
                SpriteRenderer sr = srs[i];
                if (sr == null) continue;
                // 角标子树跳过
                bool inBadge = false;
                for (Transform p = sr.transform; p != null && p != t; p = p.parent)
                {
                    if (p.name.StartsWith("NewRequest", System.StringComparison.Ordinal)) { inBadge = true; break; }
                }
                if (inBadge) continue;

                sr.color = new Color(c.r, c.g, c.b, sr.color.a);
                n++;
            }
            return n;
        }
        catch { return 0; }
    }

    private static string HexOf(Color c)
    {
        int r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
        int g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
        int b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
        return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
    }

    // 一次性把好友按钮整棵子树的「名字 / 有无 SpriteRenderer / 当前颜色」打出来。
    //
    // 为什么必须打：第 189~197 轮在这个按钮上翻过好几次车，根因都是
    // **凭注释猜结构**，而 `GameObject.Find` 裸名字只匹配顶层对象这件事
    // 又踩过一次。有了层级实况，下次不用再猜。
    private static void DumpOnce(GameObject btn, string verdict)
    {
        if (_dumped) return;
        _dumped = true;
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[FriendsBar] 按钮染色：").Append(verdict);
            sb.Append(" | 层级:");
            Walk(btn.transform, sb, 0);
            AmethystPlugin.Logger?.LogInfo(sb.ToString());
        }
        catch { }
    }

    private static void Walk(Transform t, System.Text.StringBuilder sb, int depth)
    {
        if (t == null || depth > 4) return;
        for (int i = 0; i < depth; i++) sb.Append(' ');
        string srTxt;
        try
        {
            var sr = t.GetComponent<SpriteRenderer>();
            srTxt = sr == null ? "-" : HexOf(sr.color) + "/a" + sr.color.a.ToString("F2");
        }
        catch { srTxt = "!"; }
        sb.Append('[').Append(t.name).Append(' ').Append(srTxt).Append(']');
        int n = t.childCount;
        for (int i = 0; i < n; i++)
        {
            try { Walk(t.GetChild(i), sb, depth + 1); }
            catch { }
        }
    }

    // 查找「好友」按钮（供本类与 `AmethystFriendsListOpen` 共用同一套可靠查找）。
    //
    // 为什么必须共用：第 197 轮之前这里用裸名字 `GameObject.Find("FriendsButton")`，
    // 而 `GameObject.Find` **只匹配顶层对象** ⇒ 永远返回 null（项目文档 4282 行已实测）。
    // 把查找集中成一个方法，避免两处各写一份、再次跑偏。
    internal static GameObject FindFriendsButton()
    {
        try
        {
            return FindDeep("AccountManager/AccountTab/GameHeader/BarSprite/FriendsButton")
                   ?? GameObject.Find("FriendsButton");
        }
        catch { return null; }
    }

    // 查找支持路径的对象。
    //
    // ⚠ 为什么不能只用 `GameObject.Find`：
    //   `GameObject.Find("A/B/C")` 只能解析**从顶层对象出发**的路径；
    //   而 `A` 本身若不是顶层对象（例如挂在 `AccountManager` 这个 DontDestroyOnLoad
    //   对象之下），整条路径就必然返回 null。
    //   本项目的历史坑（文档 4128 / 4282 行）：BarSprite、FriendsButton
    //   连续两轮都因为裸名字返回 null，日志里却打出"看起来正常"的兜底值。
    //
    // 正确做法：先按 `AccountManager.Instance`（这是 `DestroyableSingleton`，
    // 一定拿得到）的 transform 走 `Transform.Find(相对路径)`，
    // 失败再退回 `GameObject.Find` 全路径兜底。
    private static GameObject FindDeep(string path)
    {
        try
        {
            // 主路径：从 AccountManager 的 transform 出发按相对路径找。
            // `AccountManager` 是 DestroyableSingleton，账号 UI 都挂在它下面。
            AccountManager mgr = AccountManager.Instance;
            if (mgr != null && mgr.transform != null)
            {
                Transform t = mgr.transform.Find(path);
                if (t != null) return t.gameObject;

                // `AccountTab` 也可能直接被实例化到别的层级，退一层再试
                string trimmed = path.StartsWith("AccountManager/", System.StringComparison.Ordinal)
                    ? path.Substring("AccountManager/".Length)
                    : path;
                t = mgr.transform.Find(trimmed);
                if (t != null) return t.gameObject;
            }

            // 兜底：全场景按路径查找（对顶层对象有效）
            return GameObject.Find(path);
        }
        catch { return null; }
    }

    // 角标染色：底板 + 数量文字 + 高光。
    //
    // ⚠ 第 200 轮：**改成整棵子树递归查找**。
    //   原来用 `Transform.FindChild` 找**直接子节点**（`b.FindChild("Shrine")`），
    //   但实测 `Shrine` 并不在 `NewRequestActive`/`NewRequestInactive` 的直接子级
    //   （一次性层级转储里把它印在同一行，是因为日志里过滤掉了缩进，看不出层级）。
    //   结果就是底板改到了、`Shrine` 那个青蓝色 (#93F4E2) 一直没改到。
    private static void DimBadge(string state, string badge)
    {
        try
        {
            Transform t = _friendsButton.transform;
            Transform s = FindDescendant(t, state);
            if (s == null) return;
            Transform b = FindDescendant(s, badge);
            if (b == null) return;

            Color accent = ThemeAccent();

            // ① 底板
            Transform bg = FindDescendant(b, "Background");
            if (bg != null)
            {
                var sr = bg.gameObject.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(accent.r, accent.g, accent.b, sr.color.a);
            }

            // ② 数量文字（原来偏蓝，按用户要求改成主题色）
            Transform tx = FindDescendant(b, "Text_TMP");
            if (tx != null)
            {
                var tmp = tx.gameObject.GetComponent<TMPro.TextMeshPro>();
                if (tmp != null) tmp.color = Color.white;
            }

            // ③ 高光：两态里名字不同，Highlight 侧 "Shine"、Inactive 侧 "Shrine"
            Transform sh = FindDescendant(b, "Shine", "Shrine");
            if (sh != null)
            {
                var sr = sh.gameObject.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.18f);
            }
        }
        catch { }
    }

    // 在子树里按名字递归查找（先序）。找不到返回 null。
    private static Transform FindDescendant(Transform root, params string[] names)
    {
        try
        {
            if (root == null) return null;
            int n = root.childCount;
            for (int i = 0; i < n; i++)
            {
                Transform c = root.GetChild(i);
                if (c == null) continue;

                for (int k = 0; k < names.Length; k++)
                {
                    if (string.Equals(c.name, names[k], System.StringComparison.Ordinal)) return c;
                }

                Transform d = FindDescendant(c, names);
                if (d != null) return d;
            }
        }
        catch { }
        return null;
    }

    private static void Log(string what)
    {
        try
        {
            // ⚠ 这里必须用与 Apply() **同一个**查找方式（FindDeep），
            //   否则日志读的是裸名字（返回 null）⇒ 永远打"未找到"，
            //   和实际状态对不上。这正是项目文档 4282 行记的教训：
            //   日志里凡是从 null 分支兜底出来的值，比不打日志更危险。
            bool barOk = FindDeep("AccountManager/AccountTab/GameHeader/BarSprite") != null;
            AmethystPlugin.Logger?.LogInfo(
                $"[FriendsBar] {what}（FinalSuspect AwakeFriendCodeUIPatch 做法）：" +
                $"底板={(barOk ? "已透明" : "未找到")} " +
                $"按钮={(_friendsButton != null ? "已处理" : "未找到")}");
        }
        catch { }
    }
}

// ===== ⚠⚠ 第 200 轮：**两个 Awake 挂点都已删除，不要再加回来** =====
//
// 原来这里有两个（都指向 `AmethystFriendsBar.Apply()`）：
//
//     [HarmonyPatch(typeof(AccountTab),      "Awake")]      Prefix
//     [HarmonyPatch(typeof(AccountManager),  "Awake")]  Prefix
//
// 而 `Apply()` 内部的 `FindDeep()` 第一句就是 `AccountManager.Instance`。
// 这两个 Awake 都发生在 `AccountManager` **自身的初始化期间**，
// 此时回头访问它的单例，会打断账号管理器的初始化 ——
// 实测症状：主界面一直是"正在加载账号"（账号名 XXXXXXXXXX、等级是个黑圆），
// 点返回主界面后尤其明显。
//
// 参考实现 FinalSuspect 之所以能用，是因为它的 `AwakeFriendCodeUIPatch` 只改颜色、
// 且**不访问 `AccountManager.Instance`**（它用裸名字 `GameObject.Find`）；
// 我们在 `AwakeAccountManager` 那个补丁上改成 `__instance`、从不碰 `Instance` 也是一样道理。
// 而本文件为了修"裸名字只匹配顶层对象"的坑，改用了 `AccountManager.Instance` 走相对路径 ——
// 于是把 FS 那套挂点变得不安全了。
//
// 现在的做法：**不在 Awake 里做任何事**，全部交给
// `AmethystMainMenuOptimize.TickLive()`（主界面 0.5s 一次）驱动
// → `TickLive()` 里已加 `if (!_applied) Apply();`。

// ⚠⚠ 第 200 轮：**这个挂点已被删除，不要再加回来。**
//
//   原来这里还有一个：
//       [HarmonyPatch(typeof(AccountManager), "Awake")]
//       internal static class AmethystAccountManagerAwakePatch { Prefix → AmethystFriendsBar.Apply(); }
//
//   而 `Apply()` 内部 `FindDeep()` 的第一句是 `AccountManager.Instance` ——
//   等于**在 `AccountManager.Awake` 还没跑完时回头访问它自己的单例**。
//   实测后果：`AccountManager` 的初始化被这段代码打断，账号 UI 永远停在
//   "正在加载账号"（主界面账号名显示 XXXXXXXXXX、等级是个黑圆）。
//
//   参考实现 FinalSuspect 只挂 `AccountTab.Awake`，且在 `AccountManager.Awake` 上的补丁
//   用的是 `__instance`、从不访问 `Instance`。我们照着它去掉这个多余挂点即可。

// 第 199 轮：主题色的**反复断言**由 `AmethystMainMenuOptimize.TickLive()` 驱动
// （主界面 0.5s 一次，由 `AmethystHud.Update` 调用），见该文件里的调用点。
// 这里不再单独挂 Harmony 补丁 —— `MainMenuManager.Start` 早于 `AccountTab.Awake`，
// 一次性挂在 Start 上按钮还没建好，等于没挂。
