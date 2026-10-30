using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace Amethyst;

// 会议身份标记 —— 数据层。
//
// 功能：会议里每名玩家的头像旁有一个「EditTag」图标，点击后用**游戏原版变形者菜单**
// （ShapeshifterMinigame）列出当前版本可用身份，选中即把该身份标记到那名玩家身上；
// 标记随后显示在会议名字旁与游戏内头顶（本地标记，只有自己看得到）。
//
// 配色按要求：**内鬼红、船员蓝**；列表顺序：**内鬼在前、船员在后**。
//
// ⚠ 命名注意：`AmethystDeadRoleReveal.cs` 里已经有一个
//   `Amethyst.Patches.AmethystRoleTag : MonoBehaviour`（头顶标签组件）。
//   由于 Patches 命名空间下的代码会用「就近解析」，本类**不能**再叫 AmethystRoleTag，
//   否则在 `Amethyst.Patches` 里会把 `AmethystRoleTag.X` 解析到那个 MonoBehaviour 上（CS0426）。
//   故此处命名为 AmethystRoleTagStore。

internal sealed class RoleTagInfo
{
    internal string Label = string.Empty;
    internal Color Color = Color.white;
}

internal static class AmethystRoleTagStore
{
    internal static bool Enabled => AmethystConfig.RoleTag != null && AmethystConfig.RoleTag.Value;

    // 内鬼 = 红，船员 = 蓝（按要求；想改色调只动这两个常量即可）
    internal static readonly Color ImpostorColor = new Color32(255, 63, 63, 255);
    internal static readonly Color CrewmateColor = new Color32(92, 168, 255, 255);
    internal static readonly Color ClearColor = new Color32(185, 185, 185, 255);   // 「清除标记」项

    // 本地标记表：PlayerId -> 标记（只在本次对局内有效）
    private static readonly Dictionary<byte, RoleTagInfo> Tags = new Dictionary<byte, RoleTagInfo>();

    internal static void Reset()
    {
        Tags.Clear();
        SuffixCache.Clear();
        DeadCache.Clear();
        _deadProbeFrame = -999;
    }

    internal static void Set(byte playerId, string label, Color color)
    {
        Tags[playerId] = new RoleTagInfo { Label = label ?? string.Empty, Color = color };
        SuffixCache.Remove(playerId);   // 第 179 轮：让缓存的显示串失效
    }

    internal static void Clear(byte playerId)
    {
        Tags.Remove(playerId);
        SuffixCache.Remove(playerId);
    }

    internal static bool Has(byte playerId) => Tags.ContainsKey(playerId);

    // 玩家是否**不可标记**（死亡 / 会议中离线 / 资料缺失）。
    //
    // 用途：会议标记只对**在场的活人**有意义 —— 死人不会再被投票、也不会再行动；
    // 而会议中途离线的人，游戏同样会把他的卡片置为不可用状态。
    // 给他们留标记既没用又容易误导，所以标记内容与标记按钮都一并撤掉。
    //
    // 判据优先级：
    //   ① `GameData.GetPlayerById(id)` 的 `NetworkedPlayerInfo` —— **权威来源**：
    //        · `Disconnected`：会议中途离线（游戏 `MeetingHud.HandleDisconnectedPlayerVoteArea`
    //          处理的正是这种情况，卡片会变灰且不能投票）；
    //        · `IsDead`：阵亡；
    //        · `IsIncomplete`：资料尚未同步完整（刚加入），同样不可标记；
    //        · `info == null`：资料已被移除 ⇒ 不在场 ⇒ 不可标记。
    //   ② 回退到本地快照的 `Dead`（`GameData` 取不到时兜底）。
    //
    // ⚠ 为什么不能只靠快照：玩家离线后 `PlayerControl` 可能被销毁，
    //   `AmethystPlayerCacheStore.SyncPlayers` 会把他从表里清掉 ⇒ `TryGet` 失败
    //   ⇒ 旧实现返回 false ⇒ **标记和按钮反而留下来了**。这正是本次要堵的洞。
    internal static bool IsDeadPlayer(byte playerId)
    {
        try
        {
            GameData gd = GameData.Instance;
            if (gd != null)
            {
                NetworkedPlayerInfo info = gd.GetPlayerById(playerId);
                if (info == null) return true;          // 资料已移除 = 不在场
                if (info.Disconnected) return true;     // 会议中离线
                if (info.IsDead) return true;           // 阵亡
                if (info.IsIncomplete) return true;     // 资料未同步完整
                return false;
            }
        }
        catch { }

        try
        {
            if (AmethystPlayerCacheStore.TryGet(playerId, out AmethystPlayerCache c) && c != null)
                return c.Dead;
        }
        catch { }

        return false;
    }

    // 显示层用：形如 " <color=#FF3F3F>[伪装者]</color>"；没有标记时返回空串。
    //
    // ⚠ 第 179 轮性能：本方法由**每名玩家**的两个头顶标签组件**每帧**调用（共 2N 次/帧），
    //   外加会议里每个格子每帧一次。旧实现每次都：读 `LobbyBehaviour.Instance`（原生）、
    //   查字典、再 `string.Format` 拼一个新的富文本 —— 也就是每帧 2N 次字符串分配，
    //   而且**标记几乎从不变化**。
    //
    //   现在改为按玩家 id **缓存拼好的字符串**：只有标记被设置/清除时才失效重算。
    //   同时把「是否在大厅」这个原生判定节流到每 10 帧一次（它是场景级状态）。
    private static readonly Dictionary<byte, string> SuffixCache = new Dictionary<byte, string>();

    // 死亡 / 离线判定的按帧缓存。
    //
    // `Suffix()` 每帧会被「会议每格」+「头顶每名玩家」各调一次（≤30 次/帧），
    // 而 `IsDeadPlayer` 要跨 IL2CPP 边界读 `GameData.GetPlayerById`。
    // 这里每 10 帧（≈0.2s）重建一次结果表：死亡/离线是低频事件，
    // 0.2s 的延迟看不出来，但原生调用从每帧 30 次降到每 10 帧 ≤15 次。
    private static readonly Dictionary<byte, bool> DeadCache = new Dictionary<byte, bool>();
    private static int _deadProbeFrame = -999;

    private static bool IsDeadCached(byte playerId)
    {
        int frame = Time.frameCount;
        if (frame - _deadProbeFrame >= 10)
        {
            _deadProbeFrame = frame;
            DeadCache.Clear();
        }

        if (DeadCache.TryGetValue(playerId, out bool dead)) return dead;

        dead = IsDeadPlayer(playerId);
        DeadCache[playerId] = dead;
        return dead;
    }

    private static int _lobbyProbeFrame = -999;
    private static bool _inLobby;

    internal static void InvalidateSuffix() => SuffixCache.Clear();

    /// <summary>
    /// 让「死亡 / 离线」判定缓存立即失效。
    /// 由「玩家离开」这类事件调用，使标记与按钮在**当帧**就消失，而不必等 10 帧节流。
    /// </summary>
    internal static void InvalidateDeadCache()
    {
        DeadCache.Clear();
        _deadProbeFrame = -999;
    }

    private static bool InLobbyNow()
    {
        int frame = Time.frameCount;
        if (frame - _lobbyProbeFrame >= 10)
        {
            _lobbyProbeFrame = frame;
            try { _inLobby = LobbyBehaviour.Instance != null; } catch { _inLobby = false; }
        }
        return _inLobby;
    }

    internal static string Suffix(byte playerId)
    {
        if (!Enabled) return string.Empty;

        // 大厅里一律不显示。
        // 标记本应在「对局结束」时就清零，这里再兜一层 ——
        // 头顶标记是挂在 PlayerControl 上的，而大厅里的玩家同样有 PlayerControl，
        // 一旦清零时机没赶上，回到大厅就会看到上一局的标记。
        if (InLobbyNow()) return string.Empty;

        // ⚠ 不可标记者（阵亡 / 会议中离线 / 资料缺失）不显示标记。
        //   走按帧缓存的判定，避免每帧对每名玩家做跨 IL2CPP 查询。
        if (IsDeadCached(playerId)) return string.Empty;

        if (SuffixCache.TryGetValue(playerId, out string hit)) return hit;

        string made = string.Empty;
        if (Tags.TryGetValue(playerId, out RoleTagInfo info) && info != null && !string.IsNullOrEmpty(info.Label))
            made = $" <color=#{ColorUtility.ToHtmlStringRGB(info.Color)}>[{info.Label}]</color>";

        SuffixCache[playerId] = made;
        return made;
    }

    // ===== 可选身份列表 =====

    internal sealed class RoleEntry
    {
        internal RoleTypes Role;
        internal RoleBehaviour Behaviour;
        internal string Label = string.Empty;
        internal Color Color = Color.white;
        internal bool Impostor;
        internal bool IsClear;   // 末尾的「清除标记」项
    }

    // 不需要出现在标记列表里的身份：所有**灵魂职业**。
    //
    // ⚠ 不能再手写名单 —— 旧实现写死了「伪装者灵魂 / 船员灵魂 / 守护天使」，
    //   游戏 v19.0 新增「网红 SpiritGuide」后就漏了它，导致灵魂职业出现在标记列表里。
    //   改用游戏自己的 `RoleManager.IsGhostRole`（参考 BetterAmongUs `Utilities\RoleUtils.cs` 第 49 行）。
    private static bool SkipRole(RoleTypes r)
    {
        try { return RoleManager.IsGhostRole(r); }
        catch { return r == RoleTypes.ImpostorGhost || r == RoleTypes.CrewmateGhost || r == RoleTypes.GuardianAngel; }
    }

    // 从 RoleManager 取「当前版本可用身份」，内鬼在前、船员在后。
    internal static List<RoleEntry> BuildRoleList()
    {
        var list = new List<RoleEntry>();
        try
        {
            var all = RoleManager.Instance != null ? RoleManager.Instance.AllRoles : null;
            if (all == null) return list;

            for (int i = 0; i < all.Count; i++)
            {
                RoleBehaviour b = all[i];
                if (b == null) continue;
                if (SkipRole(b.Role)) continue;

                bool imp = false;
                try { imp = b.IsImpostor; } catch { }

                string label = string.Empty;
                try { label = Patches.AmethystDeadRoleReveal.GetRoleLabel(b.Role, b.NiceName); } catch { }
                if (string.IsNullOrEmpty(label)) label = b.Role.ToString();

                list.Add(new RoleEntry
                {
                    Role = b.Role,
                    Behaviour = b,
                    Label = label,
                    Impostor = imp,
                    Color = imp ? ImpostorColor : CrewmateColor
                });
            }

            // 内鬼在前、船员在后。
            // 注意 List.Sort 是**不稳定**排序，所以「清除标记」不能混在列表里参与排序，
            // 必须在排序完成之后追加到末尾。
            list.Sort((x, y) => y.Impostor.CompareTo(x.Impostor));

            list.Add(new RoleEntry
            {
                Role = RoleTypes.Crewmate,
                Label = AmethystText.T("RoleTagClear"),
                Color = ClearColor,
                Impostor = false,
                IsClear = true
            });
        }
        catch { }
        return list;
    }
}

// 每局开始时清空上一局的标记
[HarmonyLib.HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystRoleTagResetPatch
{
    private static void Prefix() => AmethystRoleTagStore.Reset();
}

// 对局结束（结算界面就绪）时**立即清零**。
// 之前只在下一局 CoStartGame 时清零，于是「结束游戏 → 回到大厅」这段时间里
// 上一局的标记还在，而大厅玩家同样带 PlayerControl，头顶就会继续显示 —— 用户看到的正是这个。
[HarmonyLib.HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
internal static class AmethystRoleTagEndGameResetPatch
{
    private static void Prefix() => AmethystRoleTagStore.Reset();
}
