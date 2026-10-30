using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using TMPro;
using UnityEngine;

namespace Amethyst;

// ===== 每名玩家的低频快照（第 179 轮性能重写的地基）=====
//
// 【为什么必须有这一层】
//
// IL2CPP 下读取任何**原生字段**都要跨托管/原生边界，而读 `string` 更狠 ——
// 它每次都会**分配一个新的托管字符串**。旧代码有 5~6 个组件在 `LateUpdate` 里
// 各自 `foreach (PlayerControl.AllPlayerControls)` 每帧全量读一遍：
//
//   Core\AmethystNameRendering       → pc.Data.PlayerName / pc.cosmetics.nameText / text.text
//   Core\AmethystColorBlindText      → pc.cosmetics.colorBlindText / bodyMatProperties.ColorId
//   Patches\AmethystDeadRoleReveal   → 2 个每玩家组件 × 每玩家 × (ShouldShowAlive + BuildXxxText)
//   Core\AmethystModClientDetection  → 每玩家每帧读 _text.text
//
// 10 人局 = 每帧几十次跨边界调用 + 几十次字符串分配 → GC 周期性尖峰 →
// 玩家体感就是"这模组卡"。而 EHR 有 450+ 职业却不卡，正是因为它**不这么做**：
// 它用 `PerSecondUpdateScheduler.ShouldRunUpdate(pc.PlayerId)` 把"每玩家"的工作
// 按帧错峰，任何一个玩家都不是每帧被处理。
//
// 【本层的做法】
//   ① 每名玩家每 `Cadence` 帧**错峰**刷新一次快照（`(frame + id) % Cadence`），
//      所以任何一帧只有约 n/Cadence 名玩家真的去读原生字段；
//   ② 快照里存的全是**托管字段**（string / int / bool / TMP 引用），
//      消费者每帧只做纯托管比较，稳态下**一次原生调用都不发、一个字符串都不分配**；
//   ③ "要不要改文本"这类需要读 `TMP.text`（会分配字符串）的判断，也全部放在
//      错峰刷新里做，结果写成 `Pending*`；每帧渲染循环只负责把待写值落下去。
//
// 这既保住了功能（名字颜色/身份/色盲文字全部照旧渲染），又把每帧成本从 O(n·重活)
// 降到接近 O(n·比较)。
internal sealed class AmethystPlayerCache
{
    internal PlayerControl Pc;
    internal byte Id;

    // ---- 低频刷新的原生字段快照 ----
    internal string Name = string.Empty;
    internal int ColorId = -1;          // 默认外观颜色（身份/色盲等按"本人颜色"的场合用）
    internal int ShownColorId = -1;     // 当前**身体实际显示**的颜色（变形/蘑菇混合后=实际色）
    internal bool Dead;
    internal bool IsImpostor;
    internal bool IsShapeshifting;
    internal bool InVent;
    internal bool OutfitIsDefault = true;
    internal int KillCount;
    internal int TasksDone;
    internal int TasksTotal;

    // ---- 直接缓存 TMP 引用：省掉每帧 `pc.cosmetics.xxx` 的包装器读取 ----
    internal TextMeshPro NameText;
    internal TextMeshPro ColorBlindText;

    // ---- 错峰刷新算出的"待写值"（null / int.MinValue 表示无待写）----
    internal string PendingNameText;
    internal int PendingVertexColorId = int.MinValue;

    // ===== 第 181 轮：脏标记 =====
    // 错峰刷新意味着每名玩家最多要等 `Cadence` 帧才被刷新一次。新玩家刚进场、
    // 或者谁刚变形完时，那段空窗期里快照仍是**进场前 / 变形前**的旧值 ——
    // 表现出来就是「刚进大厅时名字有一两帧没颜色」「变形后有一两帧什么都不渲染」。
    //
    // 这里给一条**旁路**：由事件（玩家 Awake / 变形 / 混装 / 换装 RPC）立刻置脏，
    // `RefreshStaggered` 见到脏的就**当帧刷新**，不再等槽位。
    internal bool Dirty;

    // 置脏后连续几次没等到 TMP 建好的重试计数（有上限，见 RefreshIfDirty）。
    internal int DirtyAttempts;

    // ---- 色盲文字待写值 ----
    internal int PendingColorBlindId = int.MinValue;

    // ---- 头顶身份标签的"名字折行补偿" ----
    //   `textInfo.lineCount` 是**原生读取**（跨 IL2CPP 边界），而两个头顶标签组件
    //   以前是**每帧每名玩家**各读一次（2N 次/帧）。名字折行只随名字长度变化，
    //   放进错峰刷新里算一次即可。
    internal float LineExtra;

    // ---- 错峰构建好的头顶身份文本（供每帧渲染直接读取，避免每帧拼富文本）----
    internal string AliveText = string.Empty;
    internal string DeadText = string.Empty;

    // ---- 错峰构建好的会议格子身份后缀（`BuildMeetingSuffix` 会遍历任务并拼富文本，
    //      绝不能每帧对每名玩家调用）----
    internal string MeetingText = string.Empty;

    // ---- 已经写出去的值，用于渲染循环里的"纯托管"判重（不读原生）----
    internal string WrittenNameText;
    internal int WrittenVertexColorId = int.MinValue;
    internal int WrittenColorBlindId = int.MinValue;

    // 自己那一格"富文本已包成哪个颜色" —— 只在颜色变化时才重新读/写一次 text.text。
    internal int AppliedBodyWrapId = int.MinValue;

    // ===== 第 184 轮：我们**把颜色写进了哪个 TMP 对象** =====
    //
    // 【为什么必须记这个 —— 这就是「大厅数据不同步时别人的名字不渲染」的根因之一】
    //   `c.NameText` 每次刷新都重新指向 `pc.cosmetics.nameText`。
    //   而游戏在**大厅数据同步 / 改名 / 换装**时会**重建这个 TMP 对象**（新实例）。
    //   我们的 `c.NameText` 于是指向了**新对象**，但 `c.WrittenNameText` 仍记着
    //   「我已经把这句话写过了」—— 那句话是写进**旧对象**的。
    //   ⇒ `if (c.WrittenNameText == want2) return;` 命中 → **永远不再往新对象写**。
    //   结果：新对象一直是游戏原版的纯白名字 ⇒ 看起来「不渲染」。
    //
    //   记下写入目标实例，只有「同一个实例 + 同一个值」才允许跳过。
    internal TextMeshPro WrittenForName;
    internal TextMeshPro WrittenForColorBlind;

    // `pc.Data` 连续拿不到的次数（大厅数据未同步时用来决定是否继续重试）。
    internal int DataMissingAttempts;
}

internal static class AmethystPlayerCacheStore
{
    // 每名玩家每隔这么多帧刷新一次快照。    // 50Hz 下 20 帧 ≈ 0.4 秒 —— 名字 / 身份 / 任务进度这类字段延迟 0.4 秒刷新
    // 人眼完全看不出来，但把每帧的跨边界读取降到了 1/20。
    internal const int Cadence = 20;

    private static readonly Dictionary<byte, AmethystPlayerCache> Map =
        new Dictionary<byte, AmethystPlayerCache>(24);
    private static readonly List<AmethystPlayerCache> List =
        new List<AmethystPlayerCache>(24);
    private static readonly HashSet<byte> Live = new HashSet<byte>();
    private static readonly List<byte> Gone = new List<byte>(8);

    private static int _frame;
    private static int _nextResync;

    // ===== 每帧刷新的**全局**状态 =====
    // 这些以前被 `ShouldShowAlive` / `ShouldReveal` 在"每玩家"级别反复读取
    // （AmongUsClient.Instance / ShipStatus.Instance / LobbyBehaviour.Instance × 每玩家）。
    // 现在一帧只读一次，所有判定退化成纯托管比较。
    internal static bool InGame;
    internal static bool InLobby;
    internal static bool LocalDead;
    internal static bool LocalIsImp;

    /// <summary>
    /// 本地玩家是否为**船员灵魂**（<c>RoleTypes.CrewmateGhost</c>）。
    ///
    /// ★ 第 199 轮：这是「死亡后看到所有人身份」的**唯一**准入条件。
    ///   原来写的是 `!LocalIsGuardianAngel`（只要不是守护天使就显示），
    ///   那是个**排除列表**，游戏每加一个死亡后职业就会漏一次：
    ///     · `GuardianAngel`（守护天使）—— 能给活人套盾，看到全部身份超模；
    ///     · `SpiritGuide`（网红，v19.0 新增，`RoleTypes.SpiritGuide = 21`）——
    ///       能用相机拍照并把消息发给**活着的船员**，等于直接点名内鬼，
    ///       比守护天使更超模。
    ///   改成**白名单**（只有船员灵魂）之后，新增任何死亡后职业都自动被排除，
    ///   不会再随着游戏更新漏判。
    /// </summary>
    internal static bool LocalIsCrewmateGhost;
    internal static byte LocalId = 255;

    // ===== 功能开关（每帧读一次 ConfigEntry.Value）=====
    internal static bool NameColorsOn;
    internal static bool ColorBlindOn;
    internal static bool ShowSelfRole;
    internal static bool ShowImpTeammateRole;
    internal static bool ShowRolesWhenDead;

    // 头顶身份标签是否可以显示（活着 / 死后两条线）。
    internal static bool AliveTagsAllowed
        => InGame && !LocalDead && (ShowSelfRole || ShowImpTeammateRole);

    /// <summary>死亡后是否可以把**所有人**的身份显示出来 —— 只对船员灵魂开放（第 199 轮）。</summary>
    internal static bool DeadTagsAllowed
        => InGame && LocalDead && LocalIsCrewmateGhost && ShowRolesWhenDead;

    internal static List<AmethystPlayerCache> All => List;

    internal static bool TryGet(byte id, out AmethystPlayerCache cache)
        => Map.TryGetValue(id, out cache);

    // ===== 第 182 轮：就地刷新 =====
    //
    // `Tick()` 在 `AmethystHud.Update` 里跑，而 Unity 不保证不同 MonoBehaviour 的
    // Update 顺序 —— 游戏完全可能在**我们之后**才创建玩家。
    // 那一帧的 LateUpdate 就会拿到一份还没算过的空快照：
    //   · 色盲文字已经被 `GetColorBlindText` 补丁当场染色（游戏调用即生效）；
    //   · 名字的 `PendingNameText` 还是 null，要等下一帧 `Tick()` 才写。
    // ⇒ 两者不同步整整一帧（用户实测能看到）。
    //
    // 解法：让**写的地方**看到脏就当场刷新。第一个写的人负责刷，
    // 后面写的人直接复用同一份结果（`Dirty` 已被清掉，不会重复计算）。
    // 脏是罕见状态，所以这个检查在稳态下就是一次 bool 判断。
    //
    // ⚠ 第 182 轮补正：早先这里是「先清 Dirty 再 Refresh」，但
    //   `PlayerControl.Awake` 触发时 `pc.cosmetics` 通常**还是 null** ——
    //   刷新拿不到 `nameText`，却把 Dirty 消费掉了，于是又要干等 20 帧的错峰槽位。
    //   那正是用户看到的「进大厅后还有几帧没渲染好」。
    //   现在：刷新后若仍拿不到 TMP 引用，就**保留脏标记**继续等（有次数上限）。
    internal static void RefreshIfDirty(AmethystPlayerCache c)
    {
        try
        {
            if (c == null || !c.Dirty) return;

            // 便宜的前置检查：TMP 还没建好就只重试，不做全量刷新。
            if (!HasNameText(c))
            {
                if (++c.DirtyAttempts >= MaxDirtyAttempts) c.Dirty = false;
                return;
            }

            c.Dirty = false;
            c.DirtyAttempts = 0;
            Refresh(c);
        }
        catch { c.Dirty = false; }
    }

    // Dirty 连续多少次拿不到 TMP 建好的就放弃（之后交给错峰路径兜底）。
    private const int MaxDirtyAttempts = 60;

    // `pc.Data` 连续拿不到多少次就停止重试（之后交给错峰路径兜底）。
    // 大厅里玩家刚进场 / 数据尚未同步时 `pc.Data` 会短暂为 null。
    private const int MaxDataRetryFrames = 90;

    private static bool HasNameText(AmethystPlayerCache c)
    {
        try
        {
            PlayerControl pc = c.Pc;
            if (pc == null) return false;
            CosmeticsLayer cos = pc.cosmetics;
            return cos != null && cos.nameText != null;
        }
        catch { return false; }
    }


    // ===== 第 181 轮：事件驱动的"立即刷新"旁路 =====
    //
    // 错峰刷新（每 N 帧一次）在稳态下很省，但**状态突变后的那一个窗口期**里
    // 快照是旧值：名字颜色/顶点色/色盲文字会短暂缺失或用错颜色。
    // 由「谁发生了变化」这个事件来触发一次立即刷新，比把刷新频率整体拉高划算得多。

    /// <summary>标记某名玩家的快照已过期，下一帧立即重刷。</summary>
    internal static void MarkDirty(byte id)
    {
        try
        {
            if (Map.TryGetValue(id, out AmethystPlayerCache c) && c != null) c.Dirty = true;
        }
        catch { }
    }

    /// <summary>立刻要求下一帧重建"在场玩家"列表（不再等错峰轮询）。</summary>
    internal static void ForceResync()
    {
        try { _nextResync = 0; } catch { }
    }

    /// <summary>标记全体。变形 / 混装复原 / 换装这类**会同时影响多人外观**的事件用它。
    /// 一局里只发生几次，代价是几次全量刷新（10 人局约 0.5 ms），完全可以接受。
    ///
    /// 同一帧内重复调用只生效一次 —— 进房时游戏会给每名玩家各调一次
    /// `CosmeticsLayer.SetName`，没有这个合并就是 10×10 次刷新。
    /// </summary>
    internal static void MarkAllDirty()
    {
        try
        {
            if (_dirtyAllFrame == Time.frameCount) return;
            _dirtyAllFrame = Time.frameCount;

            for (int i = 0; i < List.Count; i++)
            {
                AmethystPlayerCache c = List[i];
                if (c != null) c.Dirty = true;
            }
        }
        catch { }
    }

    private static int _dirtyAllFrame = -1;

    /// <summary>
    /// 玩家对象刚被创建（`PlayerControl.Awake`）时立刻建表并置脏。
    /// 这样不用等最多 20 帧的 `SyncPlayers` 轮询，进大厅的第一帧就有名字。
    /// </summary>
    internal static void OnPlayerAwake(PlayerControl pc)
    {
        try
        {
            if (pc == null) return;
            byte id = pc.PlayerId;

            AmethystPlayerCache c;
            if (!Map.TryGetValue(id, out c) || c == null)
            {
                c = new AmethystPlayerCache { Pc = pc, Id = id, Dirty = true };
                Map[id] = c;
            }
            else
            {
                c.Pc = pc;
                c.Dirty = true;
            }

            // 立刻放进在场列表，省掉等下一次 SyncPlayers
            if (!Live.Contains(id)) Live.Add(id);
            bool inList = false;
            for (int i = 0; i < List.Count; i++)
                if (List[i] == c) { inList = true; break; }
            if (!inList) List.Add(c);
        }
        catch { }
    }

    internal static void Reset()
    {
        Map.Clear();
        List.Clear();
        Live.Clear();
        Gone.Clear();
        _nextResync = 0;
    }

    // 由 AmethystHud.Update 每帧调用一次（必须在所有 LateUpdate 消费者之前）。
    internal static void Tick()
    {
        _frame++;
        int probe = AmethystPerfProbe.Begin(Section.S_Cache);
        try { ReadGlobals(); } catch { }
        try { SyncPlayers(); } catch { }
        try { RefreshStaggered(); } catch { }
        AmethystPerfProbe.End(probe, Section.S_Cache);
    }

    // ---------- 全局状态 ----------
    private static void ReadGlobals()
    {
        InGame = false;
        LocalDead = false;
        LocalIsImp = false;
        LocalIsCrewmateGhost = false;
        LocalId = 255;

        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            bool started = net != null && net.IsGameStarted;
            bool inLobbyScene = LobbyBehaviour.Instance != null;
            InLobby = net != null && !started;
            InGame = started && ShipStatus.Instance != null && !inLobbyScene;
        }
        catch { InGame = false; InLobby = false; }

        try
        {
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local != null && local.Data != null)
            {
                LocalId = local.PlayerId;
                LocalDead = local.Data.IsDead;
                RoleBehaviour r = local.Data.Role;
                if (r != null)
                {
                    LocalIsImp = r.IsImpostor;
                    LocalIsCrewmateGhost = r.Role == RoleTypes.CrewmateGhost;
                }
            }
        }
        catch { }

        try { NameColorsOn = AmethystConfig.NameColors != null && AmethystConfig.NameColors.Value; } catch { }
        try { ColorBlindOn = AmethystConfig.ColorBlindText != null && AmethystConfig.ColorBlindText.Value; } catch { }
        try { ShowSelfRole = AmethystConfig.ShowSelfRole != null && AmethystConfig.ShowSelfRole.Value; } catch { }
        try { ShowImpTeammateRole = AmethystConfig.ShowImpostorTeammateRole != null && AmethystConfig.ShowImpostorTeammateRole.Value; } catch { }
        try { ShowRolesWhenDead = AmethystConfig.ShowRolesWhenDead != null && AmethystConfig.ShowRolesWhenDead.Value; } catch { }
    }

    // ---------- 重建"当前在场玩家"列表 ----------
    // 每 Cadence 帧做一次（O(n)），足以跟上进出房间；期间快照按 id 查表，稳定。
    private static void SyncPlayers()
    {
        if (_frame < _nextResync) return;
        _nextResync = _frame + Cadence;

        var all = PlayerControl.AllPlayerControls;
        if (all == null)
        {
            if (List.Count > 0) Reset();
            return;
        }

        int n = all.Count;
        List.Clear();
        Live.Clear();

        for (int i = 0; i < n; i++)
        {
            PlayerControl pc;
            try { pc = all[i]; } catch { pc = null; }
            if (pc == null) continue;

            byte id;
            try { id = pc.PlayerId; } catch { continue; }

            if (!Map.TryGetValue(id, out AmethystPlayerCache c) || c == null)
            {
                c = new AmethystPlayerCache { Dirty = true };   // 第 181 轮：首次建立即刷新
                Map[id] = c;
            }
            c.Pc = pc;
            c.Id = id;
            List.Add(c);
            Live.Add(id);
        }

        // 清掉已经离开的玩家（避免 Map 无限增长 / 残留脏快照）
        if (Map.Count > Live.Count)
        {
            Gone.Clear();
            foreach (var kv in Map)
                if (!Live.Contains(kv.Key)) Gone.Add(kv.Key);
            for (int i = 0; i < Gone.Count; i++) Map.Remove(Gone[i]);
        }
    }

    // ---------- 错峰刷新 ----------
    //
    // ⚠ 第 180 轮：间隔不再是写死的 `Cadence`，而是按「这名玩家是不是死的」+
    //   「低负载模式有没有开」由 `AmethystPerformance.CadenceFor` 决定
    //   （配置值在那一层每帧只读一次）。
    private static void RefreshStaggered()
    {
        for (int i = 0; i < List.Count; i++)
        {
            AmethystPlayerCache c = List[i];
            if (c == null || c.Pc == null) continue;

            // 第 181 轮：被事件置脏的**当帧就刷**，不等错峰槽位。
            if (c.Dirty) { c.Dirty = false; Refresh(c); continue; }

            int cadence = Patches.AmethystPerformance.CadenceFor(c.Dead);
            if ((_frame + c.Id) % cadence != 0) continue;
            Refresh(c);
        }
    }

    private static void Refresh(AmethystPlayerCache c)
    {
        PlayerControl pc = c.Pc;
        if (pc == null) { c.NameText = null; c.ColorBlindText = null; return; }

        // ---- 缓存 TMP 引用（这样消费者每帧不再碰 pc.cosmetics）----
        try
        {
            CosmeticsLayer cos = pc.cosmetics;
            if (cos != null)
            {
                c.NameText = cos.nameText;
                c.ColorBlindText = cos.colorBlindText;
            }
            else
            {
                c.NameText = null;
                c.ColorBlindText = null;
            }
        }
        catch { }

        NetworkedPlayerInfo data = null;
        try { data = pc.Data; } catch { }

        if (data == null)
        {
            // ⚠ 第 184 轮：大厅数据未同步时 `pc.Data` 会短暂为 null。
            //   旧实现在这里直接 `return`，于是 `c.Name` 保持空字符串，
            //   `ComputeNameWrite` 里的 `IsNullOrEmpty(c.Name)` 直接返回 ——
            //   **这个名字在整个同步期间都拿不到颜色**。
            //   现在保持脏标记，下一帧继续重试（有次数上限，不会无限刷）。
            c.DataMissingAttempts++;
            if (c.DataMissingAttempts < MaxDataRetryFrames) c.Dirty = true;
            return;
        }
        c.DataMissingAttempts = 0;

        try { c.Name = data.PlayerName ?? string.Empty; } catch { c.Name = string.Empty; }

        int defColor = -1;
        try { if (data.DefaultOutfit != null) defColor = data.DefaultOutfit.ColorId; } catch { }

        // 第 184 轮：`DefaultOutfit` 为 null 是**大厅数据未同步时的常态**
        // （实测抓到 colorId=-1、TMP 里是游戏自己的 `???` 占位符）。
        // 那时若直接放弃，这个玩家在整个同步期间都拿不到颜色。
        // 退回从 cosmetics 的本体材质读颜色 —— 外观通常比 DefaultOutfit 先到。
        if (defColor < 0)
        {
            try
            {
                CosmeticsLayer cos2 = pc.cosmetics;
                if (cos2 != null)
                {
                    // `bodyMatProperties` 是 **值类型**（PlayerMaterial.Properties），
                    // 不能写 `!= null`（CS0019）。取不到就让外层 catch 兜住。
                    int c2 = cos2.bodyMatProperties.ColorId;
                    if (c2 >= 0) defColor = c2;
                }
            }
            catch { }
        }
        c.ColorId = defColor;

        try { c.Dead = data.IsDead; } catch { c.Dead = false; }
        try { c.InVent = pc.inVent; } catch { c.InVent = false; }

        RoleBehaviour role = null;
        try { role = data.Role; } catch { }
        bool imp = false;
        try { if (role != null) imp = role.IsImpostor; } catch { }
        c.IsImpostor = imp;

        c.KillCount = Amethyst.Patches.AmethystDeadRoleReveal.GetKills(c.Id);

        // ---- 任务进度：只在真的要用时才遍历（原生 List 遍历不便宜）----
        c.TasksDone = 0;
        c.TasksTotal = 0;
        if (!imp)
        {
            try
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
                    c.TasksDone = done;
                    c.TasksTotal = tasks.Count;
                }
            }
            catch { }
        }

        // ---- 变形判定 + 当前"身体色" ----
        bool shifting = false;
        int target = -1;
        try
        {
            target = pc.shapeshiftTargetPlayerId;
            if (target >= 0 && target != 255) shifting = true;
        }
        catch { }

        bool outfitDefault = true;
        try { outfitDefault = pc.CurrentOutfitType == PlayerOutfitType.Default; } catch { }
        c.OutfitIsDefault = outfitDefault;

        // 兜底：变形会把身体换成目标外观，"当前外观色 ≠ 默认色"也算伪装
        if (!shifting)
        {
            try
            {
                int curId = pc.CurrentOutfit != null ? pc.CurrentOutfit.ColorId : defColor;
                if (defColor >= 0 && curId != defColor) shifting = true;
            }
            catch { }
        }
        c.IsShapeshifting = shifting;

        int shown = defColor;
        if (target >= 0 && target != 255)
        {
            if (TryGet((byte)target, out AmethystPlayerCache tc) && tc != null && tc.ColorId >= 0)
                shown = tc.ColorId;
        }
        c.ShownColorId = shown >= 0 ? shown : defColor;

        // ---- 名字渲染：在这里算"该显示什么"，并直接读一次 TMP 判断要不要写 ----
        // 读 `TMP.text` 会分配字符串，所以**只在这个错峰路径里读**（每人每 20 帧一次）。
        // ---- 头顶身份标签的折行补偿：原生读取，放在错峰路径里算一次 ----
        try { c.LineExtra = Amethyst.Patches.AmethystDeadRoleReveal.NameLineExtra(c.NameText); } catch { }

        try { ComputeNameWrite(c); } catch { }
        try { ComputeColorBlindWrite(c); } catch { }
        try { ComputeIdentityTexts(c, pc, role, imp); } catch { }
    }

    // 头顶身份标签 / 会议格子身份后缀：低频构建一次，供每帧渲染直接读取。
    //
    // ⚠ 这两个字符串以前是在**每个玩家各自的 LateUpdate 里每帧**拼出来的：
    //   `BuildAliveText` / `BuildDeadWorldText` / `BuildMeetingSuffix` 都要读 role、
    //   遍历 Tasks（原生 List）、再做多次字符串插值。10 人局 = 每帧 20~30 次分配，
    //   而它们的输入（职业、击杀数、任务进度）其实**每秒变一次都算快**。
    private static void ComputeIdentityTexts(AmethystPlayerCache c, PlayerControl pc, RoleBehaviour role, bool imp)
    {
        c.AliveText = string.Empty;
        c.DeadText = string.Empty;
        c.MeetingText = string.Empty;

        if (role == null) return;

        bool isSelf = c.Id == LocalId;
        bool selfAliveTag = InGame && !c.Dead && isSelf && ShowSelfRole;
        bool impTeamTag = InGame && !LocalDead && LocalIsImp && imp && !isSelf && ShowImpTeammateRole;
        bool selfDeadTag = InGame && c.Dead && isSelf && ShowSelfRole;
        bool deadShowAllTag = DeadTagsAllowed;

        if (selfAliveTag || impTeamTag)
        {
            try { c.AliveText = Amethyst.Patches.AmethystDeadRoleReveal.BuildAliveText(pc) ?? string.Empty; }
            catch { c.AliveText = string.Empty; }
        }

        if (selfDeadTag || deadShowAllTag)
        {
            try { c.DeadText = Amethyst.Patches.AmethystDeadRoleReveal.BuildDeadWorldText(pc) ?? string.Empty; }
            catch { c.DeadText = string.Empty; }
        }

        // 会议格子：可见性规则与头顶一致（活着/死了看自己、内鬼看队友、死后船员看所有人）。
        if (selfAliveTag || impTeamTag || selfDeadTag || deadShowAllTag)
        {
            try { c.MeetingText = Amethyst.Patches.AmethystDeadRoleReveal.BuildMeetingSuffix(pc) ?? string.Empty; }
            catch { c.MeetingText = string.Empty; }
        }
    }

    // 名字渲染的"待写值"计算。
    //
    // 自己：**不改文本内容**（变形时那是伪装名），只把顶点色改成当前身体色。
    // 别人：包一层 <color>；内鬼队友（本地为内鬼时）用纯红。
    private static void ComputeNameWrite(AmethystPlayerCache c)
    {
        if (!NameColorsOn || c.NameText == null) return;

        TextMeshPro text = c.NameText;

        if (c.Id == LocalId)
        {
            // 顶点色路径：读 Color 是纯值类型，不分配字符串。
            int want = c.ShownColorId;
            if (want < 0) return;

            Color target;
            try { target = AmethystColors.ColorOf(want); } catch { return; }

            Color cur;
            try { cur = text.color; } catch { return; }
            target.a = cur.a;   // 保留原透明度（幽灵 / 淡入淡出）

            if (!SameRgb(cur, target) || c.WrittenVertexColorId != want)
                c.PendingVertexColorId = want;
            return;
        }

        // 别人：变形中不碰（保留游戏自己的伪装显示）
        if (c.IsShapeshifting || !c.OutfitIsDefault) return;
        if (string.IsNullOrEmpty(c.Name)) return;

        string hex;
        if (LocalIsImp && c.IsImpostor && c.Id != LocalId)
            hex = "#FF0000";
        else
            hex = AmethystColors.Hex(c.ColorId);
        if (string.IsNullOrEmpty(hex)) return;

        string want2 = $"<color={hex}>{c.Name}</color>";

        // 必须比较 TMP 的**实际文本**再决定是否重写。
        // 第 212 轮：原来这里有一句「同实例 + 同值就 return」，不读实际文本。
        // 游戏进大厅 / 重建名字时会把同一个 TMP 重写成纯白名字，于是我们
        // 以为写过了、永远不再补写 ⇒ 很多人的名字失去颜色。
        string live = null;
        try { live = text.text; } catch { }
        if (live == want2)
        {
            c.WrittenNameText = want2;   // 已经是目标值：记下来，之后不再读
            c.WrittenForName = text;
            return;
        }

        c.PendingNameText = want2;
    }

    // 色盲文字（玩家脚下的颜色名）待写值计算。
    private static void ComputeColorBlindWrite(AmethystPlayerCache c)
    {
        if (!ColorBlindOn || c.ColorBlindText == null) return;

        int wantId = c.ShownColorId >= 0 ? c.ShownColorId : c.ColorId;
        if (wantId < 0) return;

        string want = AmethystColors.ColoredColorName(wantId);
        if (string.IsNullOrEmpty(want)) return;

        // 同名字那条：必须比较实际文本，否则游戏重写后会一直跳过（色盲文字不渲染）。
        string live = null;
        try { live = c.ColorBlindText.text; } catch { }
        if (live == want)
        {
            c.WrittenColorBlindId = wantId;
            c.WrittenForColorBlind = c.ColorBlindText;
            return;
        }

        c.PendingColorBlindId = wantId;
    }

    private static bool SameRgb(Color a, Color b)
        => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
}