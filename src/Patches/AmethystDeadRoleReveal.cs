using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

// 身份显示功能
// 参考 BetterAmongUs PlayerInfoDisplay / RoleUtils.GetRoleInfo 可见性规则：
// - 自己始终可见（ShowSelfRole）
// - 内鬼队友可见（ShowImpostorTeammateRole）
// - 死后可见所有人（ShowRolesWhenDead，非守护天使）
internal static class AmethystDeadRoleReveal
{
    private static readonly Dictionary<byte, int> KillCounts = new Dictionary<byte, int>();

    internal static void ResetKills()
    {
        KillCounts.Clear();
    }

    internal static void AddKill(byte playerId)
    {
        KillCounts.TryGetValue(playerId, out int oldValue);
        KillCounts[playerId] = oldValue + 1;
    }

    internal static int GetKills(byte playerId)
    {
        return KillCounts.TryGetValue(playerId, out int value) ? value : 0;
    }

    // ========== 可见性判断（参考 BetterAmongUs RoleUtils.GetRoleInfo） ==========

    // 活着时是否应显示该玩家身份（头顶标签）
    internal static bool ShouldShowAlive(PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null) return false;

            AmongUsClient net = AmongUsClient.Instance;
            if (net == null || !net.IsGameStarted) return false;
            if (ShipStatus.Instance == null) return false;
            if (LobbyBehaviour.Instance != null) return false;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.IsDead) return false;

            // 自己
            if (target.PlayerId == local.PlayerId)
                return AmethystConfig.ShowSelfRole.Value;

            // 内鬼队友
            if (local.Data.Role != null && local.Data.Role.IsImpostor &&
                target.Data.Role != null && target.Data.Role.IsImpostor)
                return AmethystConfig.ShowImpostorTeammateRole.Value;

            return false;
        }
        catch
        {
            return false;
        }
    }

    // 死后是否应显示该玩家身份
    internal static bool ShouldReveal(PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null) return false;

            AmongUsClient net = AmongUsClient.Instance;
            if (net == null || !net.IsGameStarted) return false;
            if (ShipStatus.Instance == null) return false;
            if (LobbyBehaviour.Instance != null) return false;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return false;
            if (!local.Data.IsDead) return false;

            // ★ 第 199 轮：白名单 —— 只有**船员灵魂**才允许死亡后看到所有人身份。
            //   原来是 `role.Role != RoleTypes.GuardianAngel`（排除列表），
            //   于是 v19.0 新增的「网红 SpiritGuide」这种**能向活人传递信息**的
            //   死亡后职业也被算进去了，等于直接点名内鬼。
            //   统一走 `AmethystPlayerCacheStore.LocalIsCrewmateGhost`（每帧算一次），
            //   免得这里和另外两处判定再次跑偏。
            if (!AmethystPlayerCacheStore.LocalIsCrewmateGhost) return false;

            return AmethystConfig.ShowRolesWhenDead.Value;
        }
        catch
        {
            return false;
        }
    }

    // ========== 文本构建 ==========

    // 职业标签的着色 —— 返回**不带 `#` 的**六位十六进制（如 "FF4A62"）。
    //
    // ⚠ 第 172 轮（新功能）：改为优先使用**职业专属颜色**（AmethystRoleColors，参考 EndlessHostRoles）。
    //   以前一律 `role.TeamColor` —— 于是所有船员职业都是同一种浅青、所有内鬼职业都是同一种红，
    //   用户要的「大嗓门偏橘偏红」这类区分根本体现不出来。
    //   现在：职业配色开启 → 用职业自己的颜色；关闭 → 退回原来的阵营色（保持旧行为）。
    //
    // ⚠⚠ 修复「界面里直接显示 <color=##FF4A62> 这种原始标记」：
    //   所有调用点都写成 `<color=#{RoleHex(role)}>`（因为 `ColorUtility.ToHtmlStringRGB`
    //   返回的是**不带 #** 的 "8CFFFF"，历史上这里就是按"裸十六进制"约定用的）。
    //   而我第一版直接返回了 `AmethystRoleColors.HexOf(...)`，那个方法带 `#` ——
    //   于是拼成了 `##FF4A62`。TMP 解析不了这种非法颜色值，就把整段标记**当纯文本原样画出来**，
    //   正是截图里看到的那一行。
    //   现在这里**统一剥掉前导 `#`**，保证无论走哪条分支都满足"裸十六进制"的约定。
    private static string RoleHex(RoleBehaviour role)
    {
        try
        {
            if (AmethystRoleColors.Enabled && role != null)
                return StripHash(AmethystRoleColors.HexOf(role.Role));
        }
        catch { }
        try { return StripHash(ColorUtility.ToHtmlStringRGB(role.TeamColor)); }
        catch { return "FFFFFF"; }
    }

    // 去掉前导 '#'（TMP 的 `<color=#RRGGBB>` 由调用点补 '#'）。
    private static string StripHash(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return "FFFFFF";
        return hex[0] == '#' ? hex.Substring(1) : hex;
    }

    internal static string BuildAliveText(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null) return string.Empty;

            RoleBehaviour role = player.Data.Role;
            if (role == null) return string.Empty;

            string roleName = GetRoleLabel(role.Role, role.NiceName);
            if (string.IsNullOrEmpty(roleName)) return string.Empty;

            string color = RoleHex(role);

            if (role.IsImpostor)
            {
                int kills = GetKills(player.PlayerId);
                string killLabel = AmethystText.T("Kills", kills);
                return $"<color=#{color}>{roleName}</color> <color=#FFD24A>({killLabel})</color>";
            }

            if (player.Data.Tasks != null && player.Data.Tasks.Count > 0)
            {
                int completed = 0;
                foreach (var task in player.Data.Tasks)
                    if (task.Complete) completed++;
                return $"<color=#{color}>{roleName}</color> <color=#FFFF00>({completed}/{player.Data.Tasks.Count})</color>";
            }

            return $"<color=#{color}>{roleName}</color>";
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string BuildDeadWorldText(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null) return string.Empty;

            RoleBehaviour role = player.Data.Role;
            if (role == null) return string.Empty;

            string name = GetRoleLabel(role.Role, role.NiceName);
            if (string.IsNullOrEmpty(name)) return string.Empty;

            string roleColor = RoleHex(role);

            if (role.IsImpostor)
            {
                int kills = GetKills(player.PlayerId);
                string killLabel = AmethystText.T("Kills", kills);
                return $"<color=#{roleColor}>{name}</color> <color=#FFD24A>({killLabel})</color>";
            }

            if (player.Data.Tasks != null && player.Data.Tasks.Count > 0)
            {
                int completed = 0;
                foreach (var task in player.Data.Tasks)
                    if (task.Complete) completed++;
                return $"<color=#{roleColor}>{name}</color> <color=#FFFF00>({completed}/{player.Data.Tasks.Count})</color>";
            }

            return $"<color=#{roleColor}>{name}</color>";
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string BuildMeetingSuffix(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null) return string.Empty;

            RoleBehaviour role = player.Data.Role;
            if (role == null) return string.Empty;

            string label = GetRoleLabel(role.Role, role.NiceName);
            if (string.IsNullOrEmpty(label)) return string.Empty;

            string roleColor = RoleHex(role);
            string yellow = "#FFD24A";

            if (role.IsImpostor)
            {
                int kills = GetKills(player.PlayerId);
                string killLabel = AmethystText.T("Kills", kills);
                return $"<size=65%><color=#{roleColor}>【{label}】</color><color={yellow}>【{killLabel}】</color></size>";
            }

            if (player.Data.Tasks != null && player.Data.Tasks.Count > 0)
            {
                int completed = 0;
                foreach (var task in player.Data.Tasks)
                    if (task.Complete) completed++;
                return $"<size=65%><color=#{roleColor}>【{label}】</color><color=#FFFF00>【{completed}/{player.Data.Tasks.Count}】</color></size>";
            }

            return $"<size=65%><color=#{roleColor}>【{label}】</color></size>";
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string BuildChatTag(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null) return string.Empty;

            RoleBehaviour role = player.Data.Role;
            if (role == null) return string.Empty;

            string roleName = GetRoleLabel(role.Role, role.NiceName);
            if (string.IsNullOrEmpty(roleName)) return string.Empty;

            string color = RoleHex(role);

            if (role.IsImpostor)
            {
                int kills = GetKills(player.PlayerId);
                string killLabel = AmethystText.T("Kills", kills);
                return $"<color=#{color}>{roleName}</color> <color=#FFD24A>({killLabel})</color>";
            }

            if (player.Data.Tasks != null && player.Data.Tasks.Count > 0)
            {
                int completed = 0;
                foreach (var task in player.Data.Tasks)
                    if (task.Complete) completed++;
                return $"<color=#{color}>{roleName}</color> <color=#FFFF00>({completed}/{player.Data.Tasks.Count})</color>";
            }

            return $"<color=#{color}>{roleName}</color>";
        }
        catch
        {
            return string.Empty;
        }
    }

    // 职业显示名。
    //
    // ⚠ 第 172 轮修复「文本信息错误」：
    //   旧实现末尾是 `default: return fallback;` —— 而调用方传进来的 fallback 基本是
    //   `role.ToString()` / `b.NiceName`。一旦游戏更新加入**新职业**（枚举里我们没列到的），
    //   中文界面下就会直接显示英文枚举名（例如 "Noisemaker"），看起来就是「文本信息错误」。
    //
    //   现在改成三级兜底：
    //     ① 本地化表里查得到该 key → 用本地化名字（正常路径）；
    //     ② 查不到 → 尝试用游戏自己的 `RoleBehaviour.NiceName`（原版就是本地化的）；
    //     ③ 再兜不住 → 用去掉驼峰的空格化名字（仍比裸枚举名可读），绝不再原样吐英文枚举名。
    //
    //   `AmethystText.T` 在 key 缺失时会回退到 en_US，再缺失会**原样返回 key**，
    //   所以这里用「返回值是否等于 key」来判断"到底有没有翻译"。
    internal static string GetRoleLabel(RoleTypes role, string fallback)
    {
        string localized = LocalizedRoleName(role);
        if (!string.IsNullOrEmpty(localized)) return localized;

        // ② 游戏自带的 NiceName（原版按当前语言给出）。
        if (!string.IsNullOrEmpty(fallback) && !LooksLikeEnumName(fallback, role))
            return fallback;

        // ③ 把枚举名拆成可读形式（"GuardianAngel" → "Guardian Angel"）。
        string pretty = Prettify(role.ToString());
        return string.IsNullOrEmpty(pretty) ? role.ToString() : pretty;
    }

    // 查本地化职业名；没有翻译时返回 null（而不是把 key 当文本返回）。
    private static string LocalizedRoleName(RoleTypes role)
    {
        string key;
        switch (role)
        {
            case RoleTypes.Crewmate: key = "Crewmate"; break;
            case RoleTypes.Impostor: key = "Impostor"; break;
            case RoleTypes.Scientist: key = "Scientist"; break;
            case RoleTypes.Engineer: key = "Engineer"; break;
            case RoleTypes.GuardianAngel: key = "GuardianAngel"; break;
            case RoleTypes.Shapeshifter: key = "Shapeshifter"; break;
            case RoleTypes.CrewmateGhost: key = "CrewmateGhost"; break;
            case RoleTypes.ImpostorGhost: key = "ImpostorGhost"; break;
            case RoleTypes.Noisemaker: key = "Noisemaker"; break;
            case RoleTypes.Phantom: key = "Phantom"; break;
            case RoleTypes.Tracker: key = "Tracker"; break;
            case RoleTypes.Detective: key = "Detective"; break;
            case RoleTypes.Viper: key = "Viper"; break;
            case RoleTypes.Judge: key = "Judge"; break;
            // 第 187 轮：游戏 v19.0 新增的「网红」职业。
            // 漏了不会崩 —— default 分支会退回 "Spirit Guide" 这样拆词的英文名，
            // 但既然补了原生化名就一并登记，行为和其它职业保持一致。
            case RoleTypes.SpiritGuide: key = "SpiritGuide"; break;
            default: return null;   // 未收录的新职业：交给上面第 ② / ③ 级兜底
        }

        try
        {
            string text = AmethystText.T(key);
            // T() 查不到时会原样返回 key —— 那不代表"有翻译"。
            return string.IsNullOrEmpty(text) || text == key ? null : text;
        }
        catch { return null; }
    }

    // fallback 是不是"就是枚举名本身"（那样等于没本地化，应继续往下兜底）。
    private static bool LooksLikeEnumName(string value, RoleTypes role)
        => !string.IsNullOrEmpty(value) && string.Equals(value, role.ToString(), StringComparison.Ordinal);

    // "GuardianAngel" → "Guardian Angel"；已是正常文本时原样返回。
    private static string Prettify(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // 名字是多行时（含换行、或名字太长被自动折行），把身份标签再往上顶一点。
    //
    // ⚠ 性能（第 162 轮）：这里原来由两个 LateUpdate **每帧每名玩家**调用
    //   `nameText.ForceMeshUpdate(true, true)` —— 那是“强制重新解析文本 + 重建网格”，
    //   是 TMP 里最贵的调用之一，而目的只是为了读一个 `lineCount`。
    //   实际上 `textInfo.lineCount` 反映的就是**上一次**网格生成的结果，而名字文本由游戏自己更新，
    //   所以直接读即可，不需要我们再强制生成一遍
    //   （最坏情况是名字刚变的那一帧偏移量差一点，下一帧自纠，纯视觉且不可感知）。
    //
    // ⚠ 第 172 轮修复「信息鬼畜乱动」：
    //   旧实现用 `tag.transform.parent` 反查名字 TMP，但标签是**挂在名字对象下面**的
    //   （`Instantiate(source, names)`，names 就是 NameText_TMP），于是：
    //     · `parent` = 名字对象本身（它自己就是个 TMP）→ 读到的是**名字**没错，
    //     · 可 `Names` 下同时存在名字与我们的标签，`GetComponent<TextMeshPro>()` 在
    //       多个 TMP 并存时取到的**未必是名字那个**；
    //     · 一旦读到标签自身，`lineCount` 就由标签自己的文本（带 `<size=65%>` 富文本、
    //       且会随名字变化）决定 → 偏移量随之变化 → 标签位置上下弹跳 → 反过来又影响
    //       名字的布局/行数 —— 形成**互相驱动的抖动**，也就是用户看到的「鬼畜乱动」。
    //   现在改为**显式持有名字 TMP 引用**（由标签在创建时缓存），彻底断开这个回路。
    internal static float NameLineExtra(TextMeshPro nameText)
    {
        try
        {
            if (nameText == null || nameText.textInfo == null) return 0f;
            if (nameText.textInfo.lineCount > 1)
                return 0.16f * (nameText.textInfo.lineCount - 1);
        }
        catch { }
        return 0f;
    }

    internal static PlayerControl FindPlayer(byte playerId)
    {
        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return null;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl pc = all[i];
                if (pc != null && pc.PlayerId == playerId) return pc;
            }
        }
        catch { }

        return null;
    }
}

// ========== 活着 / 死后 / 模组客户端 三个头顶标签 ==========
//
// 第 180 轮：这三个每玩家 MonoBehaviour 已**合并为一个** `AmethystPlayerOverlay`
// （见 `Patches\AmethystPlayerOverlay.cs`）。
//
// 合并原因：三者做的事高度同构（找 Names/NameText_TMP -> 克隆 TMP -> 每帧比较内容、
// 不同才写、可见性翻转才动 enabled、折行补偿变了才动 transform），却各自占一个
// LateUpdate —— 10 人局就是 30 个 MonoBehaviour 派发 + 30 次原生 PlayerId 读取 +
// 30 次字典查找。合并后只剩 10 个，判重与写入逻辑逐条保持不变。
//
// 挂载点也一并迁走：AmethystRoleTagAttachPatch -> AmethystPlayerOverlayAttachPatch。
//

// ========== 会议聊天气泡身份显示 ==========
// 注意：名字实际是在 ChatBubble.SetName 写入的；而 ChatController.SetChatBubbleName 可能通过
// WaitForNameToExist 协程稍后才调用 SetName，所以必须挂在 SetName 上，否则会被随后覆盖。
internal static class AmethystChatBubbleRole
{
    internal static void Apply(ChatBubble bubble, NetworkedPlayerInfo playerInfo, bool voted)
    {
        try
        {
            if (bubble == null || bubble.NameText == null) return;
            if (voted) return;
            if (playerInfo == null) return;

            bubble.NameText.richText = true;

            // 气泡旁的色盲颜色名同样可能在延迟回调后才写入，这里再兜底着色一次。
            if (AmethystConfig.ColorBlindText != null && AmethystConfig.ColorBlindText.Value
                && bubble.ColorBlindName != null)
            {
                int cid = playerInfo.DefaultOutfit != null ? playerInfo.DefaultOutfit.ColorId : 0;
                string cn = AmethystColors.ColoredColorName(cid);
                if (!string.IsNullOrEmpty(cn) && bubble.ColorBlindName.text != cn)
                {
                    bubble.ColorBlindName.richText = true;
                    bubble.ColorBlindName.text = cn;
                }
            }

            PlayerControl sourcePlayer = playerInfo.Object;
            string rawName = sourcePlayer != null && sourcePlayer.Data != null ? sourcePlayer.Data.PlayerName : null;
            bool colorOn = AmethystColors.NameColorsOn && sourcePlayer != null;
            // 聊天名用顶点色着色（对齐 FinalSuspect / EndlessHostRoles），避免依赖 TMP 富文本开关。
            if (colorOn && bubble.NameText != null)
                bubble.NameText.color = AmethystColors.DisplayColor(sourcePlayer);

            bool roleOn = AmethystConfig.ShowSelfRole.Value || AmethystConfig.ShowRolesWhenDead.Value
                || AmethystConfig.ShowImpostorTeammateRole.Value;

            // 未开启身份显示：仅按颜色渲染名字
            if (!roleOn)
            {
                if (colorOn && !string.IsNullOrEmpty(rawName)) bubble.NameText.SetText(rawName);
                return;
            }

            if (sourcePlayer == null) return;
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null) return;

            AmongUsClient net = AmongUsClient.Instance;
            if (net == null || !net.IsGameStarted) return;
            if (LobbyBehaviour.Instance != null) return;

            bool shouldShow = false;
            if (local.Data.IsDead)
            {
                // ★ 第 199 轮：同上，只有船员灵魂（不再是"非守护天使"）。
                if (AmethystConfig.ShowRolesWhenDead.Value &&
                    AmethystPlayerCacheStore.LocalIsCrewmateGhost)
                    shouldShow = true;
            }
            else
            {
                if (AmethystDeadRoleReveal.ShouldShowAlive(sourcePlayer))
                    shouldShow = true;
            }

            string tag = shouldShow ? AmethystDeadRoleReveal.BuildChatTag(sourcePlayer) : null;
            if (string.IsNullOrEmpty(tag))
            {
                if (colorOn && !string.IsNullOrEmpty(rawName)) bubble.NameText.SetText(rawName);
                return;
            }

            string formatted = $"<size=75%>{tag}</size>";
            if (sourcePlayer.PlayerId == local.PlayerId)
                bubble.NameText.SetText(formatted + " " + rawName);
            else
                bubble.NameText.SetText(rawName + " " + formatted);
        }
        catch { }
    }
}

// 名字真正写入气泡的地方（含 WaitForNameToExist 延迟回调路径）。
[HarmonyPatch(typeof(ChatBubble), "SetName")]
internal static class AmethystChatBubbleRolePatch
{
    private static void Postfix(ChatBubble __instance, bool voted)
    {
        AmethystChatBubbleRole.Apply(__instance, __instance != null ? __instance.playerInfo : null, voted);
    }
}

// 即时路径的兜底：部分版本名字在 SetChatBubbleName 当场就写好。
[HarmonyPatch(typeof(ChatController), "SetChatBubbleName")]
internal static class AmethystChatBubbleRoleEarlyPatch
{
    private static void Postfix(ChatBubble bubble, NetworkedPlayerInfo playerInfo, bool didVote)
    {
        AmethystChatBubbleRole.Apply(bubble, playerInfo, didVote);
    }
}

// ========== 会议界面身份显示 ==========
internal sealed class AmethystMeetingInfoDisplay : MonoBehaviour
{
    private PlayerVoteArea _pva;
    private TextMeshPro _nameText;
    private TextMeshPro _infoText;
    private Vector3 _namePos;
    private Vector3 _infoPos;
    private string _lastInfoText = string.Empty;
    // 第 172 轮：文字"有无"的状态位，避免每帧按旧快照强行摆位置（与游戏互相拉扯 → 抖动）。
    private bool _lastHasInfo;

    public AmethystMeetingInfoDisplay(IntPtr ptr) : base(ptr) { }

    internal void Init(PlayerVoteArea pva)
    {
        _pva = pva;
        if (_pva == null || _pva.NameText == null) return;

        _nameText = _pva.NameText;
        _namePos = _nameText.transform.localPosition;
        _infoText = InstantiatePlayerInfoText("AmethystMeetingInfo_TMP", new Vector3(0f, 0.16f, 0f), _pva.transform);
        _infoPos = _infoText.transform.localPosition;
        _infoText.fontSize = 1.4f;
        _infoText.fontSizeMin = 1.4f;
        _infoText.fontSizeMax = 1.4f;
    }

    private TextMeshPro InstantiatePlayerInfoText(string name, Vector3 positionOffset, Transform parent)
    {
        TextMeshPro newTextObject = UnityEngine.Object.Instantiate(_nameText, parent);
        newTextObject.name = name;
        newTextObject.transform.localPosition = _namePos + positionOffset;
        newTextObject.transform.localScale = Vector3.one;
        newTextObject.text = string.Empty;
        newTextObject.gameObject.SetActive(true);
        return newTextObject;
    }

    // ⚠ 第 179 轮性能重写：
    //
    //   旧实现每 5 帧对**每个会议格子**做一次：
    //       PlayerControl.LocalPlayer / local.Data.IsDead / local.Data.Role   ← 原生读取 ×3
    //       FindPlayer(pva.PlayerId)      ← 遍历全体玩家（原生 List）
    //       BuildMeetingSuffix(player)    ← 读 role + 遍历 Tasks + 拼富文本（★ 分配）
    //   10 人会议 = 每 5 帧 10 次遍历 + 10 次富文本构建。
    //
    //   现在：身份文本由 `AmethystPlayerCacheStore` 按 20 帧错峰算好（快照里的 `MeetingText`），
    //   本组件只做「查表 → 与上次写入值比较 → 不同才写」，且**不再有 5 帧节流** ——
    //   因为单帧成本已经降到一个字典查找 + 一次字符串引用比较。
    private void LateUpdate()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_MeetingInfo);
    try { LateUpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_MeetingInfo); }
    }

    private void LateUpdateInner()
    {
            if (_pva == null || _nameText == null || _infoText == null)
                return;

            try
            {
                byte pid = _pva.PlayerId;

                // ---- ① 身份后缀：来自错峰快照（零原生读取）----
                string nextText = string.Empty;
                if (AmethystPlayerCacheStore.TryGet(pid, out AmethystPlayerCache c) && c != null)
                    nextText = c.MeetingText;

                // 会议身份标记：与上面的身份显示**彼此独立** —— 只要有标记就显示，
                // 不受 ShowSelfRole / ShowRolesWhenDead 等开关影响（本地标记，只有自己看得到）。
                //
                // ⚠ 不可标记者（阵亡 / 会议中离线 / 资料缺失）不显示标记：
                //   · 这里先用 `PlayerVoteArea.AmDead`（游戏卡片级状态，最即时）；
                //   · `Suffix()` 内部还会用 `NetworkedPlayerInfo.Disconnected / IsDead`
                //     再判一次（离线玩家 `AmDead` 未必被置位，必须靠 `Disconnected` 兜住）。
                string mark = _pva.AmDead ? string.Empty : AmethystRoleTagStore.Suffix(pid);
                if (!string.IsNullOrEmpty(mark))
                    nextText = string.IsNullOrEmpty(nextText) ? mark.Trim() : nextText + mark;

                if (!string.Equals(nextText, _lastInfoText, StringComparison.Ordinal))
                {
                    _infoText.SetText(nextText);
                    _lastInfoText = nextText;
                }

                // ⚠ 第 172 轮修复「信息鬼畜乱动」：
                //   旧实现在这里**每帧无条件**写一次 `localPosition` —— 即使文本一个字都没变。
                //   而 `_infoPos` / `_namePos` 是 Init 时抓的快照，会议格子在玩家说话、
                //   票数变化、头像动画时会被游戏改动位置，于是我们每帧把文字按**旧快照**强行摆回去，
                //   游戏又把格子挪走 —— 两者每帧互相拉扯，表现为会议里那行身份文字不断跳动。
                //   现在：只在「有无信息」这一状态**发生变化**时才摆一次位置。
                bool hasInfo = !string.IsNullOrEmpty(_lastInfoText);
                if (hasInfo != _lastHasInfo)
                {
                    _lastHasInfo = hasInfo;
                    _infoText.transform.localPosition = hasInfo ? _infoPos : _namePos + new Vector3(0f, 0.16f, 0f);
                    _infoText.enabled = hasInfo;
                }
            }
            catch { }
    
    }
}

// ========== 击杀统计 ==========
[HarmonyPatch(typeof(PlayerControl), "MurderPlayer")]
internal static class AmethystKillCountPatch
{
    private static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
    {
        try
        {
            if (__instance == null || target == null) return;
            if ((resultFlags & MurderResultFlags.FailedError) != 0) return;
            if ((resultFlags & MurderResultFlags.FailedProtected) != 0) return;
            if (__instance.PlayerId == target.PlayerId) return;

            AmethystDeadRoleReveal.AddKill(__instance.PlayerId);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystKillCountResetPatch
{
    private static void Prefix()
    {
        AmethystDeadRoleReveal.ResetKills();
    }
}

// ========== 组件挂载 ==========
// 每玩家组件的挂载已迁到 `Patches\AmethystPlayerOverlay.cs`
// （`AmethystPlayerOverlayAttachPatch`）—— 第 180 轮把「模组客户端标签 /
// 活着身份标签 / 死后身份标签」三个 MonoBehaviour 合并成了一个。

[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystMeetingInfoDisplayAttachPatch
{
    private static void Postfix(MeetingHud __instance)
    {
        try
        {
            if (__instance == null || __instance.playerStates == null) return;

            foreach (PlayerVoteArea pva in __instance.playerStates)
            {
                if (pva == null) continue;
                if (pva.gameObject.GetComponent<AmethystMeetingInfoDisplay>() == null)
                    pva.gameObject.AddComponent<AmethystMeetingInfoDisplay>().Init(pva);
            }
        }
        catch { }
    }
}
