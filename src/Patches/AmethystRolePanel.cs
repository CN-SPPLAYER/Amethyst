using System;
using System.Text;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

// 对局内「职业信息」面板。
//
// ===== 参考 EndlessHostRoles 8.0.1 `Patches\HudPatch.cs` 的 `TaskPanelBehaviourPatch` =====
//
// EHR 的结构（三段，缺一不可）：
//
//   ① `CreateRoleTab`（第 1083~1108 行）—— **创建**：
//        克隆 TaskPanel → 改名 "RolePanel" → `open = false`
//        → 干掉 Tab 上的 Translator（否则页签文字会被翻译器覆盖回「任务」）
//        → `localPosition = TaskPanel.localPosition - (0,1,0)`   ← 只在**这里**摆一次
//
//   ② `UpdateRoleTab`（第 1112~1129 行）—— **目标位置**：
//        `float y = TaskPanel.taskText.textBounds.size.y + 1;`
//        `float defaultPos = taskingGm ? 2f : 0.6f;`
//        targetClosed = (TaskPanel.closedPosition.x, ..., defaultPos)
//        targetOpen   = (TaskPanel.openPosition.x,   ..., defaultPos)
//        `panel.closedPosition = Lerp(panel.closedPosition, targetClosed, t)`   ← 只挪这两个"端点"
//        `panel.openPosition   = Lerp(panel.openPosition,   targetOpen,   t)`
//        注意：它**从不去改 panel.transform.localPosition** —— 位置由 ③ 算。
//
//   ③ Update 的 Prefix（第 1570~1632 行）—— **几何与最终位置**：
//        background.localScale = taskText.textBounds.size + (0.4, 0.3)
//        closedPosition.x = -background.sprite.bounds.size.x * background.localScale.x
//        timer 按 open 增减 → relativePos 用 SmoothStep 在 closed↔open 之间插值
//        transform.localPosition = AspectPosition.ComputePosition(LeftTop, relativePos)
//
// ===== 我前两版为什么都错 =====
//
//   · 位置错误：我在**自己的 Update 里**每帧把 `openPosition/closedPosition` 改写成
//     `_source.position - (0,1,0)`。但 ③ 又用 `background.sprite.bounds.size.x * scale.x`
//     去重算 `closedPosition.x` —— 两个写入源互相覆盖，位置就飘了。
//     EHR 只在 ① 摆一次 transform、在 ② 用 Lerp 逼近"目标端点"，绝不硬拽。
//
//   · 看不到文本：③ 里 `background.localScale` 取的是 `taskText.textBounds.size`。
//     textBounds 只有 TMP **排版完成后**才有效。我原来是"每 ≤1 秒才 SetTaskText 一次"
//     并且和 `_lastText` 比较后提前 return —— 于是 geometry 那一步读到的是**上一帧甚至空**的
//     textBounds，`scale` 直接变成 `Vector3.zero`（EHR 第 1596~1601 行的三元表达式），
//     背景被缩到 0 ⇒ 整块面板看不见。
//     修法：把 `SetTaskText` 放在 **Prefix 里、算几何之前**，每帧都走一遍（EHR 的做法）。
internal static class AmethystRolePanel
{
    internal const string PanelName = "AmethystRolePanel";

    private static TaskPanelBehaviour _panel;    // 我们克隆出来的职业面板
    private static TaskPanelBehaviour _source;   // 原版任务面板（模板 + 定位/尺寸基准）
    private static TextMeshPro _tabText;         // 页签文字（"职业信息"）
    private static string _lastText;             // 只在变化时写 TMP

    private const float PosSmoothSpeed = 8f;     // EHR 同款：越大越快

    internal static bool Enabled { get; set; } = true;
    internal static bool ShowDeadRoles { get; set; }
    internal static bool ShowTeamRoles { get; set; }

    // 由 AmethystHud 低频驱动：保证面板存在（创建只在需要时做一次）。
    internal static void Tick()
    {
        try
        {
            if (!Enabled || !InMatch || IsHideAndSeekMode()) { Destroy(); return; }
            Ensure();
        }
        catch { }
    }

    private static bool InMatch
    {
        get
        {
            try
            {
                if (AmongUsClient.Instance == null || !AmongUsClient.Instance.IsGameStarted) return false;
                if (LobbyBehaviour.Instance != null) return false;
                if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return false;
                return true;
            }
            catch { return false; }
        }
    }

    private static bool IsHideAndSeekMode()
    {
        try
        {
            var options = GameOptionsManager.Instance?.CurrentGameOptions;
            if (options != null)
                return options.GameMode is GameModes.HideNSeek or GameModes.SeekFools;
            return GameManager.Instance != null && GameManager.Instance.IsHideAndSeek();
        }
        catch { return false; }
    }

    // ===== ① 创建（对应 EHR CreateRoleTab）=====
    private static void Ensure()
    {
        try
        {
            if (_panel != null) return;

            HudManager hud = HudManager.Instance;
            if (hud == null || hud.TaskStuff == null) return;

            Transform root = hud.TaskStuff.transform;
            if (root == null) return;

            Transform t = root.FindChild("TaskPanel");
            if (t == null) return;

            _source = t.gameObject.GetComponent<TaskPanelBehaviour>();
            if (_source == null) return;

            GameObject clone = UnityEngine.Object.Instantiate(_source.gameObject, _source.transform.parent);
            if (clone == null) return;
            clone.name = PanelName;

            _panel = clone.GetComponent<TaskPanelBehaviour>();
            if (_panel == null)
            {
                UnityEngine.Object.Destroy(clone);
                return;
            }

            _panel.open = false;
            _panel.timer = 0f;

            // 干掉页签上的 Translator —— 否则它会按 StringNames.Tasks 把文字刷回「任务」，
            // 我们写的「职业信息」会被立刻覆盖（EHR 第 1101~1102 行同款处理）。
            try { DestroyTranslators(_panel.tab != null ? _panel.tab.gameObject : null); } catch { }

            // ⚠ 只在创建时摆一次（EHR 第 1104 行）。之后就交给 UpdateRoleTab 的 Lerp
            //   和 Prefix 的 ComputePosition，绝不再硬拽。
            _panel.transform.localPosition = _source.transform.localPosition - new Vector3(0f, 1f, 0f);

            // 页签文字引用
            _tabText = null;
            try
            {
                if (_panel.tab != null)
                    _tabText = _panel.tab.gameObject.GetComponentInChildren<TextMeshPro>();
            }
            catch { }
            if (_tabText == null)
            {
                try
                {
                    Transform tab = clone.transform.FindChild("Tab");
                    if (tab != null) _tabText = tab.gameObject.GetComponentInChildren<TextMeshPro>();
                }
                catch { }
            }

            _lastText = null;
        }
        catch { }
    }

    // 递归销毁 Translator（参照 EHR `ObjectHelper.DestroyTranslator`）。
    private static void DestroyTranslators(GameObject go)
    {
        if (go == null) return;
        try
        {
            foreach (Transform child in go.transform)
                DestroyTranslators(child.gameObject);

            var comps = go.GetComponents<MonoBehaviour>();
            if (comps != null)
            {
                for (int i = 0; i < comps.Length; i++)
                {
                    MonoBehaviour c = comps[i];
                    if (c != null && c.GetType().Name.Contains("Translator"))
                        UnityEngine.Object.Destroy(c);
                }
            }
        }
        catch { }
    }

    internal static void Destroy()
    {
        try
        {
            if (_panel != null && _panel.gameObject != null)
                UnityEngine.Object.Destroy(_panel.gameObject);
        }
        catch { }
        _panel = null;
        _source = null;
        _tabText = null;
        _lastText = null;
    }

    // 会议期间保持可见（由 AmethystMeetingTaskPanel 调用）。
    //
    // 游戏开会时会把整个 HUD 关掉，我们的克隆面板也会被一起隐藏。
    // 这里把面板重新激活、并把 z 推前（压过会议界面）—— 与任务面板同样的处理。
    private const float MeetingOverlayZ = -20f;
    private static bool _meetingZAdjusted;

    internal static void ShowDuringMeeting()
    {
        try
        {
            if (IsHideAndSeekMode()) { Destroy(); return; }
            if (_panel == null || _panel.gameObject == null)
            {
                _meetingZAdjusted = false;
                return;
            }

            if (!_meetingZAdjusted)
            {
                _meetingZAdjusted = true;
                try
                {
                    Vector3 openPos = _panel.openPosition;
                    openPos.z = MeetingOverlayZ;
                    _panel.openPosition = openPos;
                }
                catch { }
            }

            if (!_panel.gameObject.activeSelf) _panel.gameObject.SetActive(true);
        }
        catch { }
    }

    // ===== ② 目标位置（对应 EHR UpdateRoleTab）=====
    //
    // EHR 把面板放在任务面板**下面**：任务面板展开时，y 随任务文本高度下移，
    // 任务面板收起时回到固定位置（0.6，或非任务模式的 2）。
    // 这里只更新 `closedPosition` / `openPosition` 两个"端点"，用 Lerp 平滑逼近 ——
    // transform 本身由 ③ 的 ComputePosition 决定，不在这里碰。
    // 躲猫猫里职业面板整体再往下推这么多屏幕像素（经典模式不推）。
    private const float HideNSeekDropPixels = 140f;

    private static float DropUnits()
    {
        try
        {
            if (!AmethystRpcGuard.IsHS()) return 0f;
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic || cam.orthographicSize <= 0f) return 0f;
            return HideNSeekDropPixels * (2f * cam.orthographicSize) / Mathf.Max(1f, (float)Screen.height);
        }
        catch { return 0f; }
    }

    private static void UpdateTargetPositions(TaskPanelBehaviour panel)
    {
        try
        {
            if (panel == null || _source == null) return;

            float y = 0f;
            try { y = _source.taskText.textBounds.size.y + 1f; } catch { }

            bool taskingGm = IsTaskingGameMode();
            float defaultPos = taskingGm ? 2f : 0.6f;
            float drop = DropUnits();

            Vector3 targetClosed = new Vector3(
                _source.closedPosition.x,
                (taskingGm && _source.open ? y + 0.2f : defaultPos) + drop,
                _source.closedPosition.z);

            Vector3 targetOpen = new Vector3(
                _source.openPosition.x,
                (taskingGm && _source.open ? y : defaultPos) + drop,
                _source.openPosition.z);

            float t = 1f - Mathf.Exp(-PosSmoothSpeed * Time.deltaTime);

            panel.closedPosition = Vector3.Lerp(panel.closedPosition, targetClosed, t);
            panel.openPosition = Vector3.Lerp(panel.openPosition, targetOpen, t);
        }
        catch { }
    }

    // 本局是否**显示任务面板**（对齐 EHR `Utils.IsTaskingGameMode`）。
    //
    // ⚠ 不能用"本地玩家有没有任务"来判断：内鬼、躲猫猫里的躲藏者/寻猎者都没有任务，
    //   会被判成"非任务模式"，职业面板就落到任务面板**同一行**上，两块面板叠在一起。
    private static bool IsTaskingGameMode()
    {
        try { if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek()) return true; } catch { }

        try
        {
            GameOptionsManager gom = GameOptionsManager.Instance;
            if (gom != null && gom.CurrentGameOptions != null)
            {
                GameModes mode = gom.CurrentGameOptions.GameMode;
                return mode is GameModes.Normal or GameModes.NormalFools
                    or GameModes.HideNSeek or GameModes.SeekFools;
            }
        }
        catch { }

        try
        {
            NetworkedPlayerInfo data = PlayerControl.LocalPlayer != null ? PlayerControl.LocalPlayer.Data : null;
            return data != null && data.Tasks != null && data.Tasks.Count > 0;
        }
        catch { return false; }
    }

    // ===== ③ 面板 Update 的完全接管 =====
    //
    // 逐行对应 EHR 第 1592~1632 行。顺序很重要：
    //   **先** SetTaskText（让 textBounds 变有效）→ 再算 background.localScale → 再算位置。
    internal static void PortUpdate(TaskPanelBehaviour panel)
    {
        try
        {
            if (IsHideAndSeekMode()) { Destroy(); return; }
            if (panel == null || panel.taskText == null || panel.background == null || panel.tab == null) return;

            // ---- 0) 先写文本：textBounds 必须在本帧排版完成后才对 geometry 有效 ----
            UpdateTextNow(panel);

            // ---- 1) 背景尺寸跟随文本（EHR 第 1592~1601 行）----
            Transform bg = panel.background.transform;
            Vector3 extents = panel.background.sprite.bounds.extents;
            Vector3 tabExtents = panel.tab.sprite.bounds.extents;

            Vector3 size = panel.taskText.textBounds.size;
            bg.localScale = size.x > 0f
                ? new Vector3(size.x + 0.4f, size.y + 0.3f, 1f)
                : Vector3.zero;

            Vector3 bgPos = extents;
            bgPos.y = -bgPos.y;
            bgPos = Mul(bgPos, bg.localScale);
            panel.background.transform.localPosition = bgPos;

            // ---- 2) 页签贴到背景左缘（EHR 第 1607~1610 行）----
            Vector3 tabPos = Mul(tabExtents, panel.tab.transform.localScale);
            tabPos.y = -tabPos.y;
            tabPos.x += bgPos.x * 2f;
            panel.tab.transform.localPosition = tabPos;

            if (GameManager.Instance == null) return;

            // ---- 3) 端点目标位置（对应 EHR UpdateRoleTab）----
            UpdateTargetPositions(panel);

            // ---- 4) 收起位置随背景宽度变化（EHR 第 1614~1618 行）----
            panel.closedPosition = new Vector3(
                -panel.background.sprite.bounds.size.x * panel.background.transform.localScale.x,
                panel.closedPosition.y,
                panel.closedPosition.z);

            // ---- 5) 开合动画（EHR 第 1620~1630 行）----
            panel.timer = panel.open
                ? Mathf.Min(1f, panel.timer + (Time.deltaTime / panel.animationTimeSeconds))
                : Mathf.Max(0f, panel.timer - (Time.deltaTime / panel.animationTimeSeconds));

            Vector3 relativePos = new Vector3(
                Mathf.SmoothStep(panel.closedPosition.x, panel.openPosition.x, panel.timer),
                Mathf.SmoothStep(panel.closedPosition.y, panel.openPosition.y, panel.timer),
                panel.openPosition.z);

            panel.transform.localPosition = AspectPosition.ComputePosition(
                AspectPosition.EdgeAlignments.LeftTop, relativePos);
        }
        catch { }
    }

    // 写正文：每帧调用，但只在**内容变化**时才真的写 TMP（避免无谓的网格重建）。
    private static void UpdateTextNow(TaskPanelBehaviour panel)
    {
        try
        {
            string title = AmethystText.T("RolePanel");
            if (_tabText != null && _tabText.text != title) _tabText.text = title;

            string body = Build();
            if (body == _lastText) return;
            _lastText = body;

            panel.SetTaskText(body);
        }
        catch { }
    }

    private static Vector3 Mul(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

    // 组装正文。
    //
    // ⚠ 第 175 轮修复「只显示一个职业、然后就没内容了」：
    //   旧实现只做了「自己一行 + （死后/内鬼才加别人）」——活着当船员时**就只有一行**
    //   光秃秃的职业名，既没有说明也没有别人，看起来就是"没内容"。
    //
    //   EHR 的面板长这样（`Patches\HudPatch.cs` 第 1184~1200 行）：
    //       <b>职业名:</b>\r\n
    //       职业说明（一长段）
    //   即：**职业 + 该职业的专属话术**才是主体内容。别人只是附加信息。
    //
    //   现在对齐 EHR：
    //     · 正文主体 = 自己的职业 + 职业说明（短句 "话术" + 长说明 "规则"）；
    //     · 别人仍然遵守可见性规则（死后全显示 / 活着的内鬼看队友），
    //       每人一行「名字 职业（职业色）」。
    //
    // 可见性仍然保守：绝不因为"想看内容"就把别人的职业暴露给船员（那是透视挂）。
    private static string Build()
    {
        StringBuilder sb = new StringBuilder();
        try
        {
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return string.Empty;

            // ---- ① 自己的职业：名称 + 专属说明（EHR 的主体内容）----
            AppendSelfBlock(sb, local);

            bool dead = false;
            try { dead = local.Data.IsDead; } catch { }

            bool showAll = false;
            try
            {
                // ★ 第 199 轮：白名单 —— 只有**船员灵魂**才列出所有人身份。
                //   原来写的是"不是守护天使"，那是排除列表，v19.0 新增的
                //   「网红 SpiritGuide」（能拍照发消息给活人）会被漏进来。
                showAll = dead && ShowDeadRoles && AmethystPlayerCacheStore.LocalIsCrewmateGhost;
            }
            catch { }

            bool showTeam = false;
            try { showTeam = !dead && ShowTeamRoles && AmethystRpcGuard.IsImp(local); } catch { }

            if (!showAll && !showTeam) return sb.ToString().TrimEnd();

            // ---- ② 别人（仅在有权知道时）----
            var others = new System.Collections.Generic.List<PlayerControl>();
            var all = PlayerControl.AllPlayerControls;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    PlayerControl pc = all[i];
                    if (pc == null || pc == local || pc.Data == null) continue;

                    bool teammate = false;
                    try { teammate = showTeam && AmethystRpcGuard.IsImp(pc); } catch { }
                    if (showAll || teammate) others.Add(pc);
                }
            }

            if (others.Count > 0)
            {
                sb.Append('\n');
                sb.Append("<size=80%>").Append(AmethystText.T("RolePanelOthers")).Append("</size>\n");
                for (int i = 0; i < others.Count; i++)
                    AppendLine(sb, others[i], null);
            }
        }
        catch { }
        return sb.ToString().TrimEnd();
    }

    // 自己那一段：职业名（职业色，加粗）+ 专属话术 + 长说明。
    //
    // 结构照 EHR：
    //     <b><color=职业色>职业名</color>:</b>
    //     话术短句
    //     <size=80%>详细规则（多行）</size>
    private static void AppendSelfBlock(StringBuilder sb, PlayerControl local)
    {
        try
        {
            RoleBehaviour role = local.Data != null ? local.Data.Role : null;
            if (role == null) return;

            RoleTypes type = role.Role;
            string label = AmethystDeadRoleReveal.GetRoleLabel(type, role.NiceName);
            if (string.IsNullOrEmpty(label)) label = type.ToString();

            // 职业名（职业色）
            sb.Append("<b>").Append(AmethystRoleColors.Colorize(type, label)).Append("</b>");

            string tag = AmethystRoleDescriptions.Short(type);
            if (!string.IsNullOrEmpty(tag)) sb.Append('\n').Append(tag);

            string longText = AmethystRoleDescriptions.Long(type);
            if (!string.IsNullOrEmpty(longText))
                sb.Append("\n<size=80%>").Append(longText).Append("</size>");
        }
        catch { }
    }

    private static void AppendLine(StringBuilder sb, PlayerControl pc, string prefix)
    {
        try
        {
            string name = AmethystColors.Name(pc) ?? (pc.Data != null ? pc.Data.PlayerName : "?");
            string role = RoleColored(pc);
            if (string.IsNullOrEmpty(role)) return;

            sb.Append("<b>");
            if (!string.IsNullOrEmpty(prefix)) sb.Append(prefix).Append(' ');
            sb.Append(name).Append("</b>  ").Append(role);

            if (pc.Data != null && pc.Data.IsDead) sb.Append("  ").Append(AmethystText.T("DeadTag"));
            sb.Append('\n');
        }
        catch { }
    }

    private static string RoleColored(PlayerControl pc)
    {
        try
        {
            if (pc.Data == null || pc.Data.Role == null) return null;
            RoleTypes role = pc.Data.Role.Role;
            string label = AmethystDeadRoleReveal.GetRoleLabel(role, role.ToString());
            if (string.IsNullOrEmpty(label)) return null;
            return AmethystRoleColors.Colorize(role, label);
        }
        catch { return null; }
    }
}

// 接管克隆面板的 Update（EHR 的同一个挂载点、同一个语义）。
//
// 原版 Update 会重建任务文本、重算 background 尺寸、按端点插值位置 ——
// 三件事都会与我们的写入打架，所以对**我们自己的面板**一律 return false。
[HarmonyPatch(typeof(TaskPanelBehaviour), "Update")]
internal static class AmethystRolePanelUpdatePatch
{
    private static bool Prefix(TaskPanelBehaviour __instance)
    {
        try
        {
            if (__instance == null || __instance.gameObject == null) return true;
            if (__instance.gameObject.name != AmethystRolePanel.PanelName) return true;

            AmethystRolePanel.PortUpdate(__instance);
            return false;
        }
        catch { return true; }
    }
}
