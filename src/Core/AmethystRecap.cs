using System;
using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using InnerNet;
using UnityEngine;

namespace Amethyst;

// 本局「复盘信息」的数据层。
//
// 信息获取、字段与排序参考 FinalSuspect：
//   - `DataHandling\FinalPlayerData.cs`：RoleWhenAlive / RoleAfterDeath / RealDeathReason / RealKiller /
//     ProcessInt / TotalTaskCount 等字段；SetRole 首次为「生前身份」，再次为「死后身份」。
//   - `DataHandling\FinalGameData\FinalGameData.cs`：`VanillaDeathReason { None, Exile, Kill, Disconnect }`。
//   - `Patches\Game_Vanilla\OutroPatch.cs`：`DidHumansWin = GameManager.Instance.DidHumansWin(
//     EndGameResult.CachedGameOverReason)`，据此显示「阵营获胜」。
//   - `Modules\Core\Game\Utils.cs` 的 `SummaryTexts`：每行 = 彩色名字 + 进度 + 死活 + 身份；
//     死因用 `GetVitalText`（击杀附凶手、放逐、掉线），身份若死后变化则 `生前 => 死后`。
//
// 排序：按 PlayerId 升序（等价于 FinalSuspect 的 AllPlayerData 顺序）。
internal enum RecapDeath { None, Kill, Exile, Disconnect }

internal sealed class RecapEntry
{
    internal byte PlayerId;
    internal string Name = string.Empty;
    internal int ColorId;
    internal bool Dead;
    internal bool Disconnected;
    internal RecapDeath Death;
    internal byte KillerId = byte.MaxValue;

    // 法官判决：该法官本局判决（overrule）的对象（参考 BetterAmongUs 的 ExtendedRoleInfo.Judged）
    internal byte JudgedId = byte.MaxValue;

    // 身份快照：生前 / 死后分开记录（FinalSuspect 的 RoleWhenAlive / RoleAfterDeath）
    internal string RoleAliveText = string.Empty;
    internal string RoleDeadText = string.Empty;
    internal bool Impostor;

    // 进度快照
    internal string ProgressText = string.Empty;
}

internal static class AmethystRecap
{
    private static readonly List<RecapEntry> _entries = new List<RecapEntry>();
    private static readonly Dictionary<byte, RecapEntry> _byId = new Dictionary<byte, RecapEntry>();

    // RoleTypes -> RoleBehaviour 查表（取本地化名称与阵营色），
    // 与 BetterAmongUs `Utilities\RoleUtils.cs` 的 roleLookup 做法一致（遍历 RoleManager.Instance.AllRoles）。
    private static Dictionary<RoleTypes, RoleBehaviour> _roleLookup;

    // 本局结果：ResultFaction「船员/伪装者」 + ResultReason（具体原因，可为空）
    internal static string ResultFaction = string.Empty;
    internal static string ResultReason = string.Empty;
    internal static bool Frozen;

    internal static bool HasData => _entries.Count > 0;

    // 独立开关（原先跟随「界面优化」总开关，第五十轮起单独开关）
    internal static bool FeatureOn => AmethystConfig.RecapInfo != null && AmethystConfig.RecapInfo.Value;

    // 是否处于「可以使用复盘」的场景。
    // 大厅和结算界面可用，对局中关闭。
    internal static EndGameManager EndScreen;
    internal static bool CanUse
    {
        get
        {
            try
            {
                if (!FeatureOn) return false;
                return LobbyBehaviour.Instance != null || EndScreen != null;
            }
            catch { return false; }
        }
    }

    internal static void Reset()
    {
        _entries.Clear();
        _byId.Clear();
        _roleLookup = null;
        ResultFaction = string.Empty;
        ResultReason = string.Empty;
        Frozen = false;
        EndScreen = null;
        AmethystRecapWindow.Hide();
    }

    // 按 RoleTypes 取 RoleBehaviour（用于 NiceName 与阵营色）。
    //
    // ⚠ 注意：只有真正建出**非空**表时才写回缓存。
    // 若在 `RoleManager.Instance` 尚未就绪时调用，会建出一张空表；
    // 一旦把空表缓存下来，之后所有身份查表都会失败 ——
    // 表现为身份显示成原始枚举名（如 "Crewmate"）且阵营判定恒为 false
    // （伪装者会被当成船员去显示任务进度而不是击杀数），并且**整局都不会恢复**。
    private static RoleBehaviour LookupRole(RoleTypes type)
    {
        try
        {
            Dictionary<RoleTypes, RoleBehaviour> dict = _roleLookup;
            if (dict == null || dict.Count == 0)
            {
                dict = new Dictionary<RoleTypes, RoleBehaviour>();
                var all = RoleManager.Instance != null ? RoleManager.Instance.AllRoles : null;
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        RoleBehaviour r = all[i];
                        if (r != null) dict[r.Role] = r;
                    }
                }

                if (dict.Count == 0) return null;   // 还没就绪：不缓存空表，下次再试
                _roleLookup = dict;
            }

            return dict.TryGetValue(type, out RoleBehaviour b) ? b : null;
        }
        catch { return null; }
    }

    private static RecapEntry Get(byte id)
    {
        if (_byId.TryGetValue(id, out RecapEntry e)) return e;
        e = new RecapEntry { PlayerId = id };
        _byId[id] = e;
        _entries.Add(e);
        return e;
    }

    // ===== 事件记录 =====
    // 与 FinalSuspect 的 SetDeathReason 一致：死因只记录一次，先到先得，避免被后续事件覆盖。

    internal static void NoteKill(byte killerId, byte victimId)
    {
        if (victimId == killerId) return;
        try
        {
            RecapEntry v = Get(victimId);
            v.Dead = true;
            if (v.Death == RecapDeath.None)
            {
                v.Death = RecapDeath.Kill;
                v.KillerId = killerId;
            }
            // 凶手击杀数由 AmethystDeadRoleReveal 的既有补丁统计，这里不重复计数
        }
        catch { }
    }

    internal static void NoteExile(byte playerId)
    {
        try
        {
            RecapEntry e = Get(playerId);
            e.Dead = true;
            if (e.Death == RecapDeath.None) e.Death = RecapDeath.Exile;
        }
        catch { }
    }

    // 掉线 / 断开。
    //
    // ⚠ 关键：玩家断开后会被**移出 `PlayerControl.AllPlayerControls`**，
    // 之后 `Sync()` 再也遍历不到他 —— 所以这里必须把名字 / 颜色 / 身份**当场落定**，
    // 否则复盘里这一行既没有名字、也会因为从未被标记死亡而显示成「存活」。
    // 调用点是 `GameData.HandleDisconnect` 的 Prefix（此时 player.Data 仍然完整）。
    internal static void NoteDisconnect(NetworkedPlayerInfo data)
    {
        try
        {
            if (data == null) return;

            RecapEntry e = Get(data.PlayerId);
            if (e == null) return;

            if (!string.IsNullOrEmpty(data.PlayerName)) e.Name = data.PlayerName;
            try { if (data.DefaultOutfit != null) e.ColorId = data.DefaultOutfit.ColorId; } catch { }

            // 断开时按「当时是否已死」归类身份（活着就记生前身份）
            bool wasDead = false;
            try { wasDead = data.IsDead; } catch { }
            SnapshotRole(e, data, wasDead);

            e.Disconnected = true;
            e.Dead = true;
            if (e.Death == RecapDeath.None) e.Death = RecapDeath.Disconnect;
        }
        catch { }
    }

    // 法官判决（MeetingHud.SetJudgeOverrule）。
    // 参考 BetterAmongUs `Patches\Gameplay\RolePatch.cs`：同样挂钩该方法并把 targetPlayerId 记在法官身上。
    internal static void NoteJudge(byte judgeId, byte targetId)
    {
        try
        {
            if (judgeId == targetId) return;
            RecapEntry judge = Get(judgeId);
            judge.JudgedId = targetId;
        }
        catch { }
    }

    // ===== 逐帧快照（仅对局中调用）=====

    internal static void Sync()
    {
        if (Frozen) return;
        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl pc = all[i];
                if (pc == null || pc.isDummy) continue;
                NetworkedPlayerInfo data = pc.Data;
                if (data == null) continue;

                RecapEntry e = Get(pc.PlayerId);
                if (e == null) continue;

                try { if (!string.IsNullOrEmpty(data.PlayerName)) e.Name = data.PlayerName; } catch { }
                try { if (data.DefaultOutfit != null) e.ColorId = data.DefaultOutfit.ColorId; } catch { }

                bool dead = false;
                try { dead = data.IsDead; } catch { }
                if (dead) e.Dead = true;

                try
                {
                    if (data.Disconnected)
                    {
                        e.Disconnected = true;
                        e.Dead = true;
                        if (e.Death == RecapDeath.None) e.Death = RecapDeath.Disconnect;
                    }
                }
                catch { }

                SnapshotRole(e, data, dead);
            }
        }
        catch { }
    }

    // 身份与进度快照。
    //
    // 关键修正（第四十九轮）：**必须以 `NetworkedPlayerInfo.RoleType` 为准**，
    // 不能用 `Data.Role.Role`。`Data.Role` 是 RoleBehaviour 引用，在角色正式分配前
    // 可能仍是默认值（船员），此前据此记录会把伪装者记成「船员」
    // （实测：本机伪装者击杀两人后复盘显示为船员）。
    // BetterAmongUs `Utilities\RoleUtils.cs` 同样是「名称取自 RoleType、查表取行为」
    // （`playerData.RoleType.GetRoleName()` + `GetBehaviourPrefab()`）。
    //
    // 另外：**生前身份持续刷新**而不是只记第一次——首帧可能仍是未分配状态，
    // 只记第一次会把默认值永久锁死。死亡后则转入「死后身份」槽位，
    // 天然形成 FinalSuspect 的 `RoleWhenAlive => RoleAfterDeath`（船员死后 RoleType 变为
    // CrewmateGhost / ImpostorGhost）。
    // 灵魂职业判定结果缓存：`RoleManager.IsGhostRole` 是原生调用，
    // 而这里每名玩家每 20 帧都要判一次，按职业缓存后稳态下不再跨边界。
    private static readonly Dictionary<RoleTypes, bool> GhostCache = new Dictionary<RoleTypes, bool>();

    private static bool IsGhostRole(RoleTypes type)
    {
        if (GhostCache.TryGetValue(type, out bool cached)) return cached;
        bool ghost;
        try { ghost = RoleManager.IsGhostRole(type); }
        catch { ghost = type == RoleTypes.CrewmateGhost || type == RoleTypes.ImpostorGhost || type == RoleTypes.GuardianAngel; }
        GhostCache[type] = ghost;
        return ghost;
    }

    private static void SnapshotRole(RecapEntry e, NetworkedPlayerInfo data, bool dead)
    {
        try
        {
            RoleTypes type = data.RoleType;

            RoleBehaviour beh = LookupRole(type);
            string label = Patches.AmethystDeadRoleReveal.GetRoleLabel(type, beh != null ? beh.NiceName : null);
            if (string.IsNullOrEmpty(label)) label = type.ToString();

            Color team = beh != null ? beh.TeamColor : Color.white;
            string colored = $"<color=#{ColorUtility.ToHtmlStringRGB(team)}>{label}</color>";

            bool imp = beh != null && beh.IsImpostor;

            // 用**是否为灵魂职业**来判断，而不是只看 IsDead：
            // 玩家死亡后、灵魂职业切换前的那几帧 IsDead 已经是 true，
            // 若按 IsDead 记录，生前身份会落进「死后身份」槽位，
            // 结算时就会显示成灵魂职业。现在只要不是灵魂职业就一直记生前身份。
            bool ghost = IsGhostRole(type);

            if (!ghost)
            {
                e.RoleAliveText = colored;   // 持续刷新，避免把未分配时的默认值锁死
                if (imp) e.Impostor = true;
            }
            else
            {
                e.RoleDeadText = colored;    // 死后身份（幽灵角色）
                if (string.IsNullOrEmpty(e.RoleAliveText) && imp) e.Impostor = true;
            }

            // 进度：伪装者看击杀数，船员看任务完成度
            if (imp || e.Impostor)
            {
                int kills = Patches.AmethystDeadRoleReveal.GetKills(e.PlayerId);
                e.ProgressText = $"<color=#FFD24A>({AmethystText.T("Kills", kills)})</color>";
            }
            else
            {
                var tasks = data.Tasks;
                if (tasks != null && tasks.Count > 0)
                {
                    int done = 0;
                    for (int i = 0; i < tasks.Count; i++)
                    {
                        var t = tasks[i];
                        if (t != null && t.Complete) done++;
                    }
                    string col = done >= tasks.Count ? "#7BD88F" : "#E8C86A";
                    e.ProgressText = $"<color={col}>({done}/{tasks.Count})</color>";
                }
            }
        }
        catch { }
    }

    // ===== 结算 =====

    // 采集本局结果（阵营 + 具体原因）。
    //
    // 挂在 `EndGameManager.SetEverythingUp` 的 Postfix —— 与 BetterAmongUs `GameSummaryPatch` 同一挂钩点。
    // 之前是靠 0.5s 轮询 + `EndGameManager` 是否存在来判断的，有两个缺陷：
    //   1) 可能在 `EndGameResult.CachedGameOverReason` 赋值**之前**就抢先执行（EndGameManager 先出现，
    //      SetEverythingUp 在协程里更晚），从而读到默认值；
    //   2) `GameManager.Instance` 为 null 时原代码把 humansWin 当成 false，
    //      于是**把结果锁成「伪装者获胜」**且因 `string.IsNullOrEmpty(ResultFaction)` 的守卫永不纠正。
    // 现在：拿不到 GameManager 就直接不写，等下一次；结果只在成功计算后落定。
    internal static void CaptureResult()
    {
        try
        {
            if (!string.IsNullOrEmpty(ResultFaction)) return;

            GameOverReason reason = EndGameResult.CachedGameOverReason;
            GameManager gm = GameManager.Instance;
            if (gm == null) return;   // 拿不到就别写，避免锁死错误阵营

            bool humansWin = gm.DidHumansWin(reason);
            ResultFaction = AmethystText.T(humansWin ? "RecapCrewWin" : "RecapImpWin");

            string rk = ReasonKey(reason);
            if (!string.IsNullOrEmpty(rk)) ResultReason = AmethystText.T(rk);
        }
        catch { }
    }

    // 判断是否处于结算界面（EndGameManager 存在），并抓取房号。
    // 注意 EndGameManager 没有 Instance，与 AmethystAutoLobbyReturn 一样用 FindObjectOfType。
    // **结果采集不在这里**（见 CaptureResult，由 SetEverythingUp 触发）。
    internal static bool TryCaptureEndGame()
    {
        bool atEndGame = false;

        try
        {
            EndGameManager egm = UnityEngine.Object.FindObjectOfType<EndGameManager>();
            if (egm != null) atEndGame = true;
        }
        catch { }

        if (atEndGame) Frozen = true;
        return atEndGame;
    }

    // GameOverReason -> 本地化键。
    // 特例：ImpostorDisconnect / CrewmateDisconnect 是「全员断线」这种非正常结束，
    // 单独给出原因文案，而不是只提示笼统的胜负。
    private static string ReasonKey(GameOverReason r)
    {
        switch (r)
        {
            case GameOverReason.CrewmatesByVote: return "RecapReasonVoteOut";
            case GameOverReason.CrewmatesByTask: return "RecapReasonTasks";
            case GameOverReason.ImpostorsByVote: return "RecapReasonCrewVotedOut";
            case GameOverReason.ImpostorsByKill: return "RecapReasonKills";
            case GameOverReason.ImpostorsBySabotage: return "RecapReasonSabotage";
            case GameOverReason.ImpostorDisconnect: return "RecapReasonImpDisconnect";
            case GameOverReason.CrewmateDisconnect: return "RecapReasonCrewDisconnect";
            case GameOverReason.HideAndSeek_CrewmatesByTimer: return "RecapReasonHsTimer";
            case GameOverReason.HideAndSeek_ImpostorsByKills: return "RecapReasonHsKills";
            default: return null;
        }
    }

    // 组装后的结果文本（如「船员获胜（伪装者全部断线）」）
    internal static string ResultText()
    {
        if (string.IsNullOrEmpty(ResultFaction)) return string.Empty;
        if (string.IsNullOrEmpty(ResultReason)) return ResultFaction;
        return $"{ResultFaction}（{ResultReason}）";
    }

    internal static void Freeze() => Frozen = true;

    // ===== 展示 =====

    // 生成显示行。
    // 排序参考 BetterAmongUs `GameSummaryPatch.GetSortedPlayers`（第 171-179 行）：
    // 先按「是否掉线」，再按「是否死亡」，再让伪装者靠前；同一组内按 PlayerId 保持稳定。
    internal static List<string> Rows()
    {
        var list = new List<string>();
        try
        {
            _entries.Sort(CompareForDisplay);

            for (int i = 0; i < _entries.Count; i++)
            {
                RecapEntry e = _entries[i];
                if (e == null) continue;

                var sb = new StringBuilder();

                string name = string.IsNullOrEmpty(e.Name) ? ("#" + e.PlayerId) : e.Name;
                sb.Append(AmethystColors.Name(name, e.ColorId));

                // 第 207 轮（对照 EHR `Utils.SummaryTexts`）：排版改为
                //   「彩色名字 - 职业 进度 (死活/死因) 法官判决」
                // 名字与职业之间用暗色短横分隔，死活/死因用括号括起。
                sb.Append(" <color=#6E6E6E>-</color> ");

                string role = RoleText(e);
                if (!string.IsNullOrEmpty(role)) sb.Append(role);

                if (!string.IsNullOrEmpty(e.ProgressText)) sb.Append(' ').Append(e.ProgressText);

                string vital = VitalText(e);
                if (!string.IsNullOrEmpty(vital))
                    sb.Append(" <color=#6E6E6E>(").Append(vital).Append(")</color>");

                string judged = JudgedText(e);
                if (!string.IsNullOrEmpty(judged)) sb.Append("  ").Append(judged);

                list.Add(sb.ToString());
            }
        }
        catch { }
        return list;
    }

    // CachedWinners 决定获胜名单；名字和颜色联合匹配快照，避免同名串身份。
    internal static string WinnerRole(string name, int colorId, HashSet<byte> used)
    {
        foreach (RecapEntry e in _entries)
        {
            if (used.Contains(e.PlayerId) || e.ColorId != colorId || StripTags(e.Name) != StripTags(name)) continue;
            used.Add(e.PlayerId);
            return !string.IsNullOrEmpty(e.RoleAliveText) ? e.RoleAliveText : e.RoleDeadText;
        }
        return string.Empty;
    }

    // 身份：生前身份，若死后身份不同则追加「=> 死后身份」（FinalSuspect SummaryTexts 的写法）。
    private static string RoleText(RecapEntry e)
    {
        string alive = e.RoleAliveText;
        string dead = e.RoleDeadText;

        if (string.IsNullOrEmpty(alive)) return dead;           // 只有死后身份可用
        if (!e.Dead || string.IsNullOrEmpty(dead)) return alive; // 未死，或死后再没观察到新身份
        if (alive == dead) return alive;                         // 身份未变化，不重复

        return $"{alive}<color=#9A9A9A>=&gt;</color>{dead}";
    }

    // 排序比较器：掉线 / 死亡 / 阵营 分组，组内按 PlayerId 稳定。
    // 与 BetterAmongUs 的 `OrderBy(Disconnected).ThenBy(IsDead).ThenBy(!IsImpostor)` 等价
    // （伪装者靠前；未掉线、存活者排在前面）。
    private static int CompareForDisplay(RecapEntry a, RecapEntry b)
    {
        int c = a.Disconnected.CompareTo(b.Disconnected);
        if (c != 0) return c;
        c = a.Dead.CompareTo(b.Dead);
        if (c != 0) return c;
        c = b.Impostor.CompareTo(a.Impostor);
        if (c != 0) return c;
        return a.PlayerId.CompareTo(b.PlayerId);
    }

    // 死活 + 死因。参考 BetterAmongUs `GameSummaryPatch.BuildDeathReason`：
    // 它给每名玩家都标注状态（掉线 / 死亡 / 存活），这里同样保证每行都有状态标记。
    private static string VitalText(RecapEntry e)
    {
        try
        {
            switch (e.Death)
            {
                case RecapDeath.Kill:
                    {
                        string killer = e.KillerId != byte.MaxValue && _byId.TryGetValue(e.KillerId, out RecapEntry k) && k != null
                            ? AmethystColors.Name(k.Name, k.ColorId)
                            : "?";
                        return $"<color=#FF5A5A>{AmethystText.T("RecapKilledBy")}</color> {killer}";
                    }
                case RecapDeath.Exile:
                    return $"<color=#B554FF>{AmethystText.T("RecapExiled")}</color>";
                case RecapDeath.Disconnect:
                    return $"<color=#8A8A8A>{AmethystText.T("RecapDisconnected")}</color>";
                default:
                    // 死亡但未观测到具体原因（例如非主机端看不到击杀）
                    if (e.Dead) return $"<color=#FF8A8A>{AmethystText.T("RecapDead")}</color>";
                    return $"<color=#7BD88F>{AmethystText.T("RecapAlive")}</color>";
            }
        }
        catch { return string.Empty; }
    }

    // 法官判决：显示该法官本局判决的对象（参考 BetterAmongUs 的 ExtendedRoleInfo.Judged）
    private static string JudgedText(RecapEntry e)
    {
        try
        {
            if (e.JudgedId == byte.MaxValue) return string.Empty;

            string target = _byId.TryGetValue(e.JudgedId, out RecapEntry t) && t != null
                ? AmethystColors.Name(string.IsNullOrEmpty(t.Name) ? ("#" + t.PlayerId) : t.Name, t.ColorId)
                : ("#" + e.JudgedId);

            return $"<color=#E3D12B>{AmethystText.T("RecapJudge")}</color> → {target}";
        }
        catch { return string.Empty; }
    }

    // 纯文本版（供复制到剪贴板）
    internal static string PlainText()
    {
        var sb = new StringBuilder();
        string result = ResultText();
        if (!string.IsNullOrEmpty(result)) sb.Append(AmethystText.T("RecapResult")).Append(": ").Append(StripTags(result)).Append('\n');
        List<string> rows = Rows();
        for (int i = 0; i < rows.Count; i++)
            sb.Append(StripTags(rows[i])).Append('\n');

        return sb.ToString();
    }

    // 去掉富文本标签（<color=...> 等），用于复制纯文本
    private static string StripTags(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
        var sb = new StringBuilder(s.Length);
        bool inTag = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }
}
