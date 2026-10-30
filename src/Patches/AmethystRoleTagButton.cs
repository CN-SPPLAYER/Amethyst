using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Amethyst.Patches;

// 会议里每名玩家头像旁的「EditTag」按钮。
//
// 参考 FinalSuspect 1.4 `Patches\System\MeetingButtonPatch.cs`：
//   克隆 `PlayerVoteArea.Buttons/CancelButton` 作为按钮，改名、定位到头像左侧，
//   把 sprite 换成 EditTag.png，然后给 PassiveButton.OnClick 挂上打开身份选择器的回调。
internal static class AmethystRoleTagButton
{
    private const string ButtonName = "AmethystRoleTagButton";

    // 图标缩放（之前偏大、边缘会被裁掉；想再调只动这个值）
    private const float TagScale = 0.81f;

    // 悬停高亮的颜色：深紫色 + 极低 alpha。
    // 这个高亮是**铺满按钮的遮罩**，alpha 一高就把图标本身（人物）整个盖住，
    // 所以这里只给近乎透明的一层，悬停时泛一点点紫。
    private static readonly Color HoverPurple = new Color32(107, 47, 188, 10);

    private static bool _loggedChildren;

    // 给高亮子物体上色，并把它换成**与图标同一张图**，这样形状和图标完全一致。
    // （原本用的是 CancelButton 的圆角按钮图，和方形图标对不上。）
    private static void SetHighlight(Transform root, string child, bool front, Sprite icon)
    {
        Transform t = root.FindChild(child);
        if (t == null) return;

        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = HoverPurple;
            if (icon != null) sr.sprite = icon;
        }

        // 与图标重合：图标就在按钮根对象上，所以高亮位置归中、缩放归 1
        t.localPosition = new Vector3(0f, 0f, front ? 1.4f : 0.5f);
        t.localScale = Vector3.one;
    }

    // 一次性把按钮的子物体名记进日志。万一悬停高亮仍不是预期效果，
    // 从 BepInEx 日志里就能看出到底是哪个子物体在渲染。
    private static void LogChildrenOnce(GameObject go)
    {
        if (_loggedChildren || go == null) return;
        _loggedChildren = true;
        try
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform c = go.transform.GetChild(i);
                if (c == null) continue;
                SpriteRenderer csr = c.GetComponent<SpriteRenderer>();
                sb.Append(c.name);
                if (csr != null) sb.Append("(a=").Append(csr.color.a.ToString("0.00")).Append(')');
                sb.Append(", ");
            }
            AmethystPlugin.Logger?.LogInfo($"[RoleTag] EditTag button children: {sb}");
        }
        catch { }
    }

    // 该会议格子的玩家是否不可标记（死亡 / 会议中离线）。
    //
    // `pva.AmDead` 是游戏给会议卡片维护的状态（阵亡 / 断线时都会被置位），
    // 这里再 OR 上 `AmethystRoleTagStore.IsDeadPlayer` 的权威判据
    // （`NetworkedPlayerInfo.Disconnected` / `IsDead` / 资料缺失），
    // 两者任一为真即视为不可标记 —— 宁可少一个按钮，也不给死人或离线者留标记。
    private static bool IsDeadCard(PlayerVoteArea pva)
    {
        try { if (pva.AmDead) return true; } catch { }
        return AmethystRoleTagStore.IsDeadPlayer(pva.PlayerId);
    }

    internal static void Create(MeetingHud hud)
    {
        if (!AmethystRoleTagStore.Enabled) return;
        if (hud == null || hud.playerStates == null) return;

        for (int i = 0; i < hud.playerStates.Count; i++)
        {
            PlayerVoteArea pva = hud.playerStates[i];
            if (pva == null || pva.transform == null) continue;

            // 排除自己：给自己打标记没有意义
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local != null && local.PlayerId == pva.PlayerId) continue;

            Transform existing = pva.transform.FindChild(ButtonName);

            // ⚠ 死亡玩家：**只保留活人**的标记按钮。
            //   已存在的按钮要销毁（死亡后若游戏重建过卡片，或本局中途阵亡，
            //   都可能残留一个按钮）。这段放在 `existing != null` 的提前返回之前，
            //   否则残留按钮永远清不掉。
            if (IsDeadCard(pva))
            {
                if (existing != null)
                {
                    try { UnityEngine.Object.Destroy(existing.gameObject); } catch { }
                }
                continue;
            }

            if (existing != null) continue;   // 已经有就不重复建

            try
            {
                Transform template = pva.Buttons != null ? pva.Buttons.transform.Find("CancelButton") : null;
                if (template == null) continue;

                GameObject go = UnityEngine.Object.Instantiate(template.gameObject, pva.transform);
                if (go == null) continue;

                go.name = ButtonName;
                go.transform.localPosition = new Vector3(-0.95f, 0.03f, -1.31f);

                // 图标之前偏大、边缘被裁掉，整体缩小一点
                go.transform.localScale = go.transform.localScale * TagScale;

                // 先把按钮自己的图换成 EditTag；高亮随后也用同一张图，形状才对得上
                Sprite tag = AmethystResources.EditTag;
                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                if (sr != null && tag != null) sr.sprite = tag;

                // 悬停高亮：深紫 + 极低 alpha，形状与图标一致。
                // 鼠标悬停与手柄导航用的是不同的高亮对象，三个名字都设一遍。
                SetHighlight(go.transform, "ControllerHighlight", true, tag);
                SetHighlight(go.transform, "Highlight", false, tag);
                SetHighlight(go.transform, "HoverHighlight", false, tag);

                LogChildrenOnce(go);

                // 注意：本游戏版本里 PlayerVoteArea 的字段是 PlayerId（不是 TargetPlayerId）
                byte target = pva.PlayerId;
                PassiveButton button = go.GetComponent<PassiveButton>();
                if (button != null)
                {
                    button.OnClick = new Button.ButtonClickedEvent();
                    button.OnClick.AddListener((Action)(() => AmethystRoleTagPicker.Open(target)));
                }
            }
            catch { }
        }
    }

    internal static void Clear(MeetingHud hud)
    {
        if (hud == null || hud.playerStates == null) return;

        for (int i = 0; i < hud.playerStates.Count; i++)
        {
            PlayerVoteArea pva = hud.playerStates[i];
            if (pva == null || pva.transform == null) continue;

            Transform t = pva.transform.FindChild(ButtonName);
            if (t != null)
            {
                try { UnityEngine.Object.Destroy(t.gameObject); } catch { }
            }
        }
    }

    // 显示标记：把标记后缀并入会议名字旁那行文本
    internal static string Suffix(byte playerId) => AmethystRoleTagStore.Suffix(playerId);
}

// 会议开始时建按钮
[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystRoleTagButtonStartPatch
{
    private static void Postfix(MeetingHud __instance) => AmethystRoleTagButton.Create(__instance);
}

// 会议期间低频兜底：游戏可能重建 playerStates，这里保证按钮一直在
[HarmonyPatch(typeof(MeetingHud), "Update")]
internal static class AmethystRoleTagButtonUpdatePatch
{
    private static int _tick;

    private static void Postfix(MeetingHud __instance)
    {
        if (!AmethystRoleTagStore.Enabled) return;

        _tick = _tick >= 30 ? 0 : _tick + 1;
        if (_tick != 0) return;

        AmethystRoleTagButton.Create(__instance);
    }
}

// 有玩家离开（含**会议中途离线**）时：立刻失效死亡/离线缓存并重扫一次按钮。
//
// ⚠ 为什么需要这个即时路径：
//   `MeetingHud.Update` 的兜底是每 30 帧（≈0.6s）一次。会议里有人掉线时，
//   他的标记与 EditTag 按钮会在这 0.6s 内继续显示 —— 用户会以为功能没生效。
//   这里在离开事件当帧就重扫：`IsDeadCard` 会通过 `NetworkedPlayerInfo.Disconnected`
//   认出他，随即销毁按钮；`Suffix()` 也会返回空串让标记消失。
//
// ⚠ 参数里**不声明** `ClientData`：该类型在 Harmony 的 IL 编译阶段容易报错
//   （见 `AmethystPlayerOverlay.cs` 里 `AmethystLobbyNameWatcher.LeftPatch` 的说明）。
[HarmonyPatch(typeof(AmongUsClient), "OnPlayerLeft")]
internal static class AmethystRoleTagButtonLeftPatch
{
    private static void Postfix()
    {
        try
        {
            AmethystRoleTagStore.InvalidateDeadCache();
            AmethystRoleTagStore.InvalidateSuffix();

            MeetingHud hud = MeetingHud.Instance;
            if (hud != null) AmethystRoleTagButton.Create(hud);
        }
        catch { }
    }
}
