using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

internal static class AmethystRpcGuard
{
    // 第 157 轮：把检测细化到"每个 RPC 调用"—— 该 RPC 的开关关掉时，这一个调用直接放行，
    // 其余 RPC 的防护不受影响。
    // ⚠ 第 180 轮性能重写：旧实现是
    //     string name = ((RpcCalls)callId).ToString();
    //     return AmethystConfig.RpcCallEnabled(name);
    // `Enum.ToString()` 要做反射式的名称查找，**每次都分配一个新的托管字符串**，
    // 而这个方法在 PlayerControl / PlayerPhysics / ShipStatus 三个 `HandleRpc`
    // 补丁里都是**第一句**——哪怕包下一行就被放行，这个字符串也照分配不误。
    // 10 人局每秒几百个包，累积起来是持续的 GC 压力（周期性尖峰的来源之一）。
    //
    // 现在改为 `AmethystConfig` 在 Bind 结束时建好的 **callId → ConfigEntry** 直查表：
    // 一次数组下标 + 一次属性读，零分配、零哈希、零字符串。
    internal static bool CallAllowed(byte callId) => AmethystConfig.CallAllowed(callId);
    private static readonly Dictionary<int, float> Hit = new Dictionary<int, float>();
    private static readonly Dictionary<string, float> Notif = new Dictionary<string, float>();
    private static readonly Dictionary<byte, float> TaskWindowStart = new Dictionary<byte, float>();
    private static readonly Dictionary<byte, int> TaskWindowCount = new Dictionary<byte, int>();
    // ⚠ 第 165 轮：这两个「是否已设置过」的表**不再参与判定**。
    //   原因见下方 SetLevel / SetName 分支的注释：大厅里合法重发（改等级、改名字、
    //   模组客户端同步自身等级/名字）会被数成「多次设置」而误报，
    //   参考模组（Hydra 的 anticheat/rpc/SetLevel.cs、TenkaiMenu 的 Anticheat.CheckLevel）
    //   都用「大厅合法 / 开局后即违规」的位置判定，而不是数次数。
    //   这里保留字段与 Clear 以维持既有状态结构（ResetRoundState 仍在清），
    //   日后若要按时间窗统计可直接复用。
    internal static readonly Dictionary<byte, bool> HasSetLevel = new Dictionary<byte, bool>();
    internal static readonly Dictionary<byte, bool> HasSetName = new Dictionary<byte, bool>();
    private static float _meetingOpenedAt = -1f;
    private static bool _dropRpcManuallyDisabled;
    private static float _joinGraceUntil;
    // 船舶（对局场景）首次出现时间：用于开局 3 秒宽限，避免角色/任务数据尚未就绪时的误判
    private static float _shipAt = -1f;

    // 快速破坏检测：记录每个玩家上次破坏的系统和时间
    private static readonly Dictionary<byte, (int SystemType, float Time)> LastSabotage = new Dictionary<byte, (int, float)>();
    // 反复击杀已死亡玩家的次数（按玩家）
    internal static readonly Dictionary<byte, int> DeadKillCount = new Dictionary<byte, int>();

    // ===== 第 199 轮：过快击杀（击杀冷却）=====
    //
    // 【为什么记录点要打在 `PlayerControl.MurderPlayer` 而不是 RPC 链路上】
    //
    //   一次**合法**击杀在网络上不是一个包，而是两个：
    //     ① 客户端 → 主机：`CmdCheckMurder`（callId = CheckMurder，"我能杀他吗"）
    //     ② 主机判定通过后广播：`RpcMurderPlayer`（callId = MurderPlayer）
    //   两者发送者都是凶手，间隔只有几毫秒。
    //   如果在 RPC 链路里做"第一次记时间、第二次报过快"，**每一次正常击杀都会误报**。
    //
    //   所以拆成两个点：
    //     · 记录 —— 打在 `PlayerControl.MurderPlayer`（游戏真正执行击杀的方法）。
    //       主机本地调用它，其他客户端通过 MurderPlayer RPC 也走到它，
    //       所以每台机器都能拿到准确时间戳。
    //     · 检测 —— 放在 HandleRpc 的 MurderPlayer/CheckMurder 分支。
    //   两个点互不干扰，一次合法击杀只会被记录一次。
    //
    // 【第 200 轮修正：高延迟下同一个目标的重复包不算作弊】
    //
    //   延迟高时内鬼连点三下击杀。主机还没回冷却，客户端就发出了 3 个
    //   `CheckMurder`，**目标都是同一个**。主机只接受第一个（只死一个人），
    //   后两个被游戏自身丢弃 —— 但本检测是按"发送者最近一次击杀时刻"算的，
    //   那两个包会落进 8 秒窗口 ⇒ **把正常玩家的连点误判成作弊**。
    //
    //   所以时间戳必须**连目标一起记**：窗口内**换了一个目标**才判；
    //   目标相同则视为"同一次击杀意图的重复包"，一律放行。
    //   （同目标重复点击本来就杀不死人，spam 由既有的 `RepeatKillDead` 规则兜底，
    //     不需要这条规则重复管。）
    //
    // 【为什么仍不会漏掉真作弊】
    //   真作弊要连杀两个人，第二个目标必然不同 ⇒ 会被判。
    //   只重复点同一个人并不能多杀人，游戏本来就只认一次。
    internal struct KillMark
    {
        public float At;
        public byte TargetId;
    }

    internal static readonly Dictionary<byte, KillMark> LastKillAt = new Dictionary<byte, KillMark>();

    // 第 203 轮：躲猫猫里内鬼的击杀冷却只有 **1 秒**，而这个窗口默认 8 秒
    // ⇒ 躲猫猫里每一次"冷却一好就连杀下一个"的**合法操作**都会被判成作弊
    //   （而该判定是 `hardDrop = true`，会无条件本地丢弃击杀包 + 可能踢/封）。
    //
    // 做法：阈值**跟着游戏模式走**，而不是"在躲猫猫里整条检测跳过"——
    //   经典（Normal / NormalFools） → 用户设置的值（默认 8s）
    //   躲猫猫（HideNSeek/SeekFools） → 0.01s
    // 0.01s 意味着"两次击杀必须发生在 10ms 内"才可能命中 ⇒ 实际上等价于关闭，
    // 但仍走**同一条检测通路**：模式切回来时窗口立刻恢复 8s，不需要任何状态复位。
    private const float HideNSeekWindow = 0.01f;

    private static float _lastWindow = -1f;

    internal static float FastKillWindow
    {
        get
        {
            float user = 8f; // 经典模式下固定写死为 8s

            bool classic = IsClassicMode();
            float w = classic ? user : HideNSeekWindow; // 躲猫猫模式保持 0.01s 不变

            // 模式切换时打一行，方便在日志里确认切换真的发生了（只在变化时打，不刷屏）
            if (Mathf.Abs(w - _lastWindow) > 0.0001f)
            {
                _lastWindow = w;
                try { AmethystPlugin.Logger?.LogInfo($"[FastKill] 阈值={w:0.###}s（{(classic ? "经典" : "躲猫猫")}）"); }
                catch { }
            }
            return w;
        }
    }

    /// <summary>记录一次**已生效**的击杀（由 `PlayerControl.MurderPlayer` 的 Postfix 调用）。</summary>
    internal static void NoteKill(PlayerControl killer, PlayerControl target)
    {
        try
        {
            if (killer == null || target == null) return;
            // 只认"真的打死了"：被护盾挡下 / 判定失败的不算，
            // 否则一次被挡的击杀会让玩家白等一个窗口。
            if (target.Data == null || !target.Data.IsDead) return;
            LastKillAt[killer.PlayerId] = new KillMark { At = Time.unscaledTime, TargetId = target.PlayerId };
        }
        catch { }
    }

    /// <summary>
    /// 该玩家是否在击杀冷却窗口内又去杀**另一个人**。
    /// 目标与上一次相同 ⇒ 放行（高延迟连点造成的重复包）。
    ///
    /// ⚠ 第 203 轮：**只在经典局生效**。躲猫猫（HideNSeek / SeekFools）里内鬼的击杀冷却
    ///   只有 **1 秒**，而本检测的窗口是 8 秒 ⇒ 躲猫猫里每一次"冷却一好就连杀下一个"的
    ///   **合法操作**都会落进窗口被判定成作弊；而这条判定是 `hardDrop = true`
    ///   （无条件本地丢弃击杀包）+ 后续可能警告/踢/封 ⇒ 纯误伤，且会让躲猫猫根本没法连杀。
    ///   第 203 轮的做法：**不跳过检测，而是把阈值改小** ——
    ///   躲猫猫用 0.01s、经典用用户设置值（默认 8s），跟着模式来回切。
    ///   详见 `FastKillWindow` 属性上方的注释。
    ///
    ///   另外还有第二道守卫：按这一局**真实生效的冷却**再判一次（见 `RealKillCooldown`）。
    ///   因为房主自定义冷却在经典局里也能调到 1s —— 只按模式挡，躲不掉这一种误伤。
    ///   冷却取法抄 BetterAmongUs 1.3.4 `Modules/AntiCheat/RPCHandlers/MurderPlayerHandler.cs`。
    /// </summary>
    internal static bool InFastKillWindow(byte pid, PlayerControl target)
    {
        try
        {
            if (AmethystConfig.FastKillGuard != null && !AmethystConfig.FastKillGuard.Value) return false;

            float win = FastKillWindow;
            if (win <= 0f) return false;

            // 第 203 轮第二道：按**这一局实际生效的冷却**再判一次。
            // 房主可以自定义击杀冷却（经典局里也能把 15~25s 调到 1s），那时窗口内的
            // 连杀同样是合法的。只在"真实冷却 > 我们的窗口"时才判 —— 即窗口内的击杀
            // 在正常规则下根本不可能发生。取不到真实值（-1）时**继续判**，
            // 因为经典局默认冷却 15~25s，远大于 8s 窗口。
            float cd = RealKillCooldown();
            if (cd >= 0f && cd <= win) return false;
            if (!LastKillAt.TryGetValue(pid, out KillMark m)) return false;

            // 目标读不出来时无法判断是否同一人 —— 保守起见不判作弊。
            if (target == null) return false;
            if (m.TargetId == target.PlayerId) return false;

            return Time.unscaledTime - m.At < win;
        }
        catch { return false; }
    }

    /// <summary>
    /// 当前是否**经典局**（Normal / NormalFools）。
    /// 抄自 BetterAmongUs 1.3.4 `Modules/GameState.IsNormalGame`。
    ///
    /// ⚠ 取不到游戏模式时返回 `false`（= 不检测）：这条检测的处罚是"无条件丢弃击杀包 + 踢/封"，
    ///   宁可漏放也不能误伤 —— 与本文件其它"读不到就放行"的保守口径一致。
    /// </summary>
    internal static bool IsClassicMode()
    {
        try
        {
            return GameOptionsManager.Instance != null &&
                   GameOptionsManager.Instance.CurrentGameOptions != null &&
                   GameOptionsManager.Instance.CurrentGameOptions.GameMode is GameModes.Normal or GameModes.NormalFools;
        }
        catch { return false; }
    }

    /// <summary>
    /// 当前这一局**实际生效**的击杀冷却（秒）。读不到返回 -1。
    /// 抄自 BetterAmongUs 1.3.4 `Modules/AntiCheat/RPCHandlers/MurderPlayerHandler.cs` 第 23 行的取法：
    ///     GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown)
    /// </summary>
    internal static float RealKillCooldown()
    {
        try
        {
            if (GameOptionsManager.Instance == null || GameOptionsManager.Instance.CurrentGameOptions == null) return -1f;
            return GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
        }
        catch { return -1f; }
    }
    // 每位玩家最近触发的重大破坏系统（用于“同一玩家多次破坏不同系统”检测）
    internal static readonly Dictionary<byte, int> MajorSabotageByActor = new Dictionary<byte, int>();

    // 这里刻意**不放**「已知作弊菜单 RPC 签名」表。
    // 按需求：反作弊系统**不检测、也不丢弃**任何人的模组 RPC
    // （硬编码的签名表容易误伤正常模组，且收益不确定）。
    // （本模组自己发的 RPC 是 `RpcCalls.CancelPet`（= 50，见 AmethystModClientDetection.TransportRpc），
    //   用于模组客户端识别握手。）

    // 大厅中不可能出现的“对局类”RPC（参考 Elysium lobbyGameRpcs）：
    // 有人在未开局时伪造击杀/会议/变形/进管等 RPC，通常用于炸房/干扰。
    private static readonly HashSet<byte> LobbyGameRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.MurderPlayer, (byte)RpcCalls.CheckMurder,
        (byte)RpcCalls.ReportDeadBody, (byte)RpcCalls.StartMeeting,
        (byte)RpcCalls.EnterVent, (byte)RpcCalls.ExitVent,
        (byte)RpcCalls.Shapeshift, (byte)RpcCalls.CheckShapeshift, (byte)RpcCalls.RejectShapeshift,
        (byte)RpcCalls.StartVanish, (byte)RpcCalls.StartAppear, (byte)RpcCalls.CheckVanish, (byte)RpcCalls.CheckAppear,
        (byte)RpcCalls.ProtectPlayer, (byte)RpcCalls.CheckProtect,
        (byte)RpcCalls.CompleteTask, (byte)RpcCalls.PlayAnimation
    };

    internal static bool IsLobbyGameRpc(byte callId) => LobbyGameRpcs.Contains(callId);

    internal static bool InLobby()
    {
        try
        {
            return AmongUsClient.Instance != null
                && !AmongUsClient.Instance.IsGameStarted
                && Alive(LobbyBehaviour.Instance);
        }
        catch { return false; }
    }

    internal static readonly HashSet<int> SabSystems = new HashSet<int>
    {
        (int)SystemTypes.Reactor, (int)SystemTypes.LifeSupp, (int)SystemTypes.Comms,
        (int)SystemTypes.HeliSabotage, (int)SystemTypes.Laboratory, (int)SystemTypes.MushroomMixupSabotage,
        (int)SystemTypes.Electrical
    };

    internal static bool On()
    {
        if (!AmethystConfig.RpcGuard.Value || AmongUsClient.Instance == null) return false;
        return true;
    }

    internal static bool AmHost()
    {
        try { return ((InnerNetClient)AmongUsClient.Instance)?.AmHost == true; }
        catch { return false; }
    }

    internal static bool ShouldDropRpc()
    {
        try
        {
            if (AmethystConfig.DropRPC == null) return false;
            return AmHost() && AmethystConfig.DropRPC.Value;
        }
        catch { return false; }
    }

    internal static void SetDropRpcEnabled(bool enabled, bool manualChange = false)
    {
        try
        {
            if (AmethystConfig.DropRPC != null) AmethystConfig.DropRPC.Value = enabled;
            if (manualChange) _dropRpcManuallyDisabled = !enabled;
            if (enabled) _dropRpcManuallyDisabled = false;
        }
        catch { }
    }

    internal static void SyncDropRpcForHost(bool host)
    {
        try
        {
            if (AmethystConfig.DropRPC == null) return;
            if (!host)
            {
                AmethystConfig.DropRPC.Value = false;
                return;
            }

            if (!_dropRpcManuallyDisabled)
                AmethystConfig.DropRPC.Value = true;
        }
        catch { }
    }

    internal static bool InMatch()
    {
        try
        {
            return AmongUsClient.Instance != null && AmongUsClient.Instance.IsGameStarted
                && ShipStatus.Instance != null
                && !Alive(LobbyBehaviour.Instance) && !Alive(IntroCutscene.Instance)
                && !MeetingActive() && !ExileActive();
        }
        catch { return false; }
    }

    // ⚠ 第 265 轮：不能用 `X.Instance == null` 判断。
    //   会议/放逐/开场结束后对象可能只是被 SetActive(false) 而没有销毁，
    //   `Instance` 会一直非 null，导致 `InMatch()` / `InGame()` 永久为 false ——
    //   所有依赖它们的检测在第一次会议后全部失效。
    //   这里统一改为「对象存在且真正激活」。
    private static bool Alive(UnityEngine.Object o)
    {
        try
        {
            if (o == null) return false;                 // Unity 的伪 null：已销毁
            var c = o as UnityEngine.Component;
            return c != null && c.gameObject != null && c.gameObject.activeInHierarchy;
        }
        catch { return false; }
    }

    internal static bool MeetingActive() => Alive(MeetingHud.Instance);
    internal static bool ExileActive() => Alive(ExileController.Instance);
    internal static bool InMeetingOrExileActive() => MeetingActive() || ExileActive();

    // 游戏进行中（含会议/放逐），仅排除大厅与开场。用于"会议中也不可触发"的强规则（如击杀）。
    internal static bool InGame()
    {
        try
        {
            return AmongUsClient.Instance != null && AmongUsClient.Instance.IsGameStarted
                && ShipStatus.Instance != null
                && !Alive(LobbyBehaviour.Instance) && !Alive(IntroCutscene.Instance);
        }
        catch { return false; }
    }

    internal static bool IsImp(PlayerControl pc)
    {
        try { return pc != null && pc.Data != null && pc.Data.Role != null && pc.Data.Role.IsImpostor; }
        catch { return false; }
    }

    internal static bool IsHS()
    {
        try { return GameManager.Instance != null && GameManager.Instance.IsHideAndSeek(); }
        catch { return false; }
    }

    // 管道(vent)检测的主机/非主机开关：分别由 VentHostRules / VentClientRules 控制
    internal static bool VentRulesEnabled()
    {
        try
        {
            var cfg = AmHost() ? AmethystConfig.VentHostRules : AmethystConfig.VentClientRules;
            return cfg == null || cfg.Value;
        }
        catch { return false; }
    }

    // 对局场景出现 3 秒宽限：角色/任务/管道数据在开局瞬间可能尚未就绪，
    // 宽限期内跳过依赖这些数据的规则（会议/破坏等强规则不受影响）。
    internal static bool ShipGraceOver()
    {
        try
        {
            ShipStatus s = ShipStatus.Instance;
            if (s == null) { _shipAt = -1f; return false; }
            if (_shipAt < 0f) _shipAt = Time.realtimeSinceStartup;
            return Time.realtimeSinceStartup - _shipAt >= 3f;
        }
        catch { return false; }
    }

    // 玩家数据是否完整可信（角色已同步、未死亡、未处于加载中）
    internal static bool RoleReady(PlayerControl pc)
    {
        try
        {
            return pc != null && pc.Data != null && !pc.Data.IsIncomplete && pc.Data.Role != null;
        }
        catch { return false; }
    }

    internal static bool IsOutfitCall(byte callId)
    {
        return callId == (byte)RpcCalls.CheckName || callId == (byte)RpcCalls.SetName
            || callId == (byte)RpcCalls.SetColor || callId == (byte)RpcCalls.SetHatStr
            || callId == (byte)RpcCalls.SetSkinStr || callId == (byte)RpcCalls.SetPetStr
            || callId == (byte)RpcCalls.SetVisorStr || callId == (byte)RpcCalls.SetNamePlateStr;
    }

    // ===== 第 261 轮：RPC 结构校验（网络对象归属）=====
    // 第 263 轮：未知 RPC **不再拦截/提示** —— 游戏与模组更新会引入新 RPC ID，
    // 按枚举白名单丢弃会误伤正常玩家。这里只保留「RPC 发错网络对象」的判定。

    internal enum RpcObjectKind { Player, Physics, Ship, VoteBan, Meeting }

    internal static bool StructureGuardOn =>
        AmethystConfig.UnknownRpcGuard != null && AmethystConfig.UnknownRpcGuard.Value;

    private static readonly HashSet<byte> PlayerKeyRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.MurderPlayer, (byte)RpcCalls.CheckMurder, (byte)RpcCalls.ReportDeadBody,
        (byte)RpcCalls.StartMeeting, (byte)RpcCalls.SetName, (byte)RpcCalls.CheckName,
        (byte)RpcCalls.SetColor, (byte)RpcCalls.CheckColor, (byte)RpcCalls.SetLevel,
        (byte)RpcCalls.SetTasks, (byte)RpcCalls.SetRole, (byte)RpcCalls.SetInfected,
        (byte)RpcCalls.SyncSettings, (byte)RpcCalls.SetStartCounter, (byte)RpcCalls.UseZipline,
        (byte)RpcCalls.Shapeshift, (byte)RpcCalls.CheckShapeshift, (byte)RpcCalls.RejectShapeshift,
        (byte)RpcCalls.StartVanish, (byte)RpcCalls.StartAppear, (byte)RpcCalls.CheckVanish,
        (byte)RpcCalls.CheckAppear, (byte)RpcCalls.ProtectPlayer, (byte)RpcCalls.CheckProtect
    };

    private static readonly HashSet<byte> PhysicsKeyRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.EnterVent, (byte)RpcCalls.ExitVent, (byte)RpcCalls.ClimbLadder
    };

    private static readonly HashSet<byte> ShipKeyRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.UpdateSystem, (byte)RpcCalls.CloseDoorsOfType
    };

    private static readonly HashSet<byte> VoteKeyRpcs = new HashSet<byte> { (byte)RpcCalls.AddVote };

    private static readonly HashSet<byte> MeetingKeyRpcs = new HashSet<byte>
    {
        (byte)RpcCalls.CastVote, (byte)RpcCalls.ClearVote, (byte)RpcCalls.VotingComplete
    };

    internal static bool IsWrongObject(RpcObjectKind kind, byte callId)
    {
        switch (kind)
        {
            case RpcObjectKind.Player:
                return PhysicsKeyRpcs.Contains(callId) || ShipKeyRpcs.Contains(callId)
                    || VoteKeyRpcs.Contains(callId) || MeetingKeyRpcs.Contains(callId);

            case RpcObjectKind.Physics:
                return PlayerKeyRpcs.Contains(callId) || ShipKeyRpcs.Contains(callId)
                    || VoteKeyRpcs.Contains(callId) || MeetingKeyRpcs.Contains(callId);

            case RpcObjectKind.Ship:
                return PlayerKeyRpcs.Contains(callId) || PhysicsKeyRpcs.Contains(callId)
                    || VoteKeyRpcs.Contains(callId) || MeetingKeyRpcs.Contains(callId);

            case RpcObjectKind.VoteBan:
                return PlayerKeyRpcs.Contains(callId) || PhysicsKeyRpcs.Contains(callId)
                    || ShipKeyRpcs.Contains(callId) || MeetingKeyRpcs.Contains(callId);

            case RpcObjectKind.Meeting:
                return PlayerKeyRpcs.Contains(callId) || PhysicsKeyRpcs.Contains(callId)
                    || ShipKeyRpcs.Contains(callId) || VoteKeyRpcs.Contains(callId);
        }
        return false;
    }

    internal static int CurrentMapId()
    {
        try
        {
            GameOptionsManager gom = GameOptionsManager.Instance;
            if (gom == null) return -1;
            var o = gom.CurrentGameOptions;
            return o != null ? o.MapId : -1;
        }
        catch { return -1; }
    }

    internal static bool InMeetingOrExile()
    {
        // 第 265 轮：改为「真正激活」判断，避免单例残留导致永久为 true。
        return InMeetingOrExileActive();
    }

    internal static bool MeetingGuardArmed()
    {
        try
        {
            if (!AmethystRpcGuard.MeetingActive()) return false;
            float openedAt = _meetingOpenedAt;
            if (openedAt < 0f) return false;
            return Time.unscaledTime - openedAt >= 1f;
        }
        catch { return false; }
    }

    internal static void MarkMeetingOpened()
    {
        _meetingOpenedAt = Time.unscaledTime;
    }

    internal static void ClearMeetingOpened()
    {
        _meetingOpenedAt = -1f;
    }

    // 场上是否已存在重大破坏（电力/反应堆/氧气/通讯/蘑菇）
    internal static bool MajorSabotageActive()
    {
        try
        {
            ShipStatus ship = ShipStatus.Instance;
            if (ship == null || ship.Systems == null) return false;

            if (ship.Systems.TryGetValue(SystemTypes.Electrical, out var e) && e != null)
            { var s = e.TryCast<SwitchSystem>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.Reactor, out var r) && r != null)
            { var s = r.TryCast<ReactorSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.Laboratory, out var lab) && lab != null)
            { var s = lab.TryCast<ReactorSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.LifeSupp, out var ls) && ls != null)
            { var s = ls.TryCast<LifeSuppSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.Comms, out var c) && c != null)
            { var s = c.TryCast<HudOverrideSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.Comms, out var c2) && c2 != null)
            { var s = c2.TryCast<HqHudSystemType>(); if (s != null && s.IsActive) return true; }
            if (ship.Systems.TryGetValue(SystemTypes.MushroomMixupSabotage, out var m) && m != null)
            { var s = m.TryCast<MushroomMixupSabotageSystem>(); if (s != null && s.IsActive) return true; }

            return false;
        }
        catch { return false; }
    }

    // 是否为“重大破坏”系统（电力/反应堆/氧气/通讯/蘑菇）
    internal static bool IsMajorSabotageSystem(int sys)
    {
        switch ((SystemTypes)sys)
        {
            case SystemTypes.Electrical:
            case SystemTypes.Reactor:
            case SystemTypes.Laboratory:
            case SystemTypes.LifeSupp:
            case SystemTypes.Comms:
            case SystemTypes.MushroomMixupSabotage:
                return true;
            default:
                return false;
        }
    }

    // 检测快速破坏：0.1秒内两次不同的破坏（不包括关门）
    internal static bool CheckRapidSabotage(byte playerId, int systemType, out string reason)
    {
        reason = null;
        if (systemType == (int)SystemTypes.Doors) return false; // 排除关门

        float now = Time.realtimeSinceStartup;
        if (LastSabotage.TryGetValue(playerId, out var last))
        {
            if (now - last.Time < 0.1f && last.SystemType != systemType)
            {
                reason = AmethystText.T("RapidSabotage");
                return true;
            }
        }
        LastSabotage[playerId] = (systemType, now);
        return false;
    }

    // 清除快速破坏检测记录
    internal static void ClearRapidSabotage()
    {
        LastSabotage.Clear();
    }

    internal static void ResetRoundState()
    {
        HasSetLevel.Clear();
        HasSetName.Clear();
        TaskWindowStart.Clear();
        TaskWindowCount.Clear();
        LastSabotage.Clear();
        DeadKillCount.Clear();
        LastKillAt.Clear();   // 第 199 轮：换局/重开必须清空，否则新一局开局就落在旧窗口里
        MajorSabotageByActor.Clear();
        // (265) Do NOT clear _meetingOpenedAt here: ResetRoundState is called from
        // MeetingHud.OnDisable, which may fire early; clearing it would disable all
        // meeting-time detections for the whole meeting. It is cleared by MeetingHud.Close.
        // 注意：_shipAt 不在此处清零——开局宽限只在对局首次出现 ShipStatus 时生效一次，
        // 会议结束不应重新触发 3 秒宽限（否则任务/扫描类检测窗口会被反复压缩）。
        // _shipAt 在 ShipGraceOver() 检测到 ShipStatus 消失时自动清零（对局结束/换图）。
    }

    internal static void MarkJoinGrace()
    {
        _joinGraceUntil = Time.realtimeSinceStartup + 10f;
    }

    internal static bool InJoinGrace()
    {
        try { return Time.realtimeSinceStartup < _joinGraceUntil; }
        catch { return false; }
    }

    internal static bool RealVent(int id)
    {
        try
        {
            ShipStatus s = ShipStatus.Instance;
            if (s == null || s.AllVents == null) return true;
            foreach (Vent v in s.AllVents) if (v != null && v.Id == id) return true;
            return false;
        }
        catch { return true; }
    }

    // 玩家是否被分配了指定类型的任务。
    // **返回 true = “有该任务，或数据不足以判断” → 调用方的 `!HasTask(...)` 会放行。**
    //
    // ⚠ 第 165 轮（修复「无扫描任务却扫描」误报的**根因**）：
    //   这里原来用**反射**去找 `TaskType` / `Type` 属性或字段。但本作（IL2CPP）的
    //   `NetworkedPlayerInfo.TaskInfo` 上**根本没有** `TaskType` / `Type` 这两个成员。
    //   已用编译探针实测确认：
    //       task.TypeId   → 可读（是任务类型，byte）
    //       task.Id       → 可读（是 uint 的任务实例 id，不是类型）
    //       task.TaskType → error CS1061（该类型不含此成员）
    //       task.TaskId   → error CS1061（该类型不含此成员）
    //   于是两次反射都取到 null、`value` 恒为 null，循环永远不 `return true`，
    //   本方法**恒返回 false**。
    //   后果：`SetScanner` 那条「无扫描任务却扫描」的判定 `!HasTask(..., SubmitScan)`
    //   恒为真 —— 连**真正在做医疗扫描**的船员都会被判成作弊。这就是误报来源。
    //   现在改为直接读强类型字段 `task.TypeId`，不再有任何反射。
    internal static bool HasTask(PlayerControl pc, TaskTypes taskType)
    {
        try
        {
            if (pc == null) return true;                            // 连玩家都没有 → 不判定
            NetworkedPlayerInfo data = pc.Data;
            if (data == null || data.Tasks == null) return true;    // 任务数据未同步 → 不判定
            var tasks = data.Tasks;
            if (tasks.Count <= 0) return true;                      // 任务表为空（尚未分发）→ 不判定

            int target = (int)taskType;
            for (int i = 0; i < tasks.Count; i++)
            {
                // TaskInfo 是值类型，不存在 null 元素，直接取值比较。
                if ((int)tasks[i].TypeId == target) return true;
            }
            return false;                                           // 任务表可用，且确实没有该类型任务
        }
        catch { return true; }   // 任何异常都放行：宁可漏判，不可误报
    }

    internal static bool TrackTaskRate(byte playerId)
    {
        try
        {
            float now = Time.realtimeSinceStartup;
            if (!TaskWindowStart.TryGetValue(playerId, out float start) || now - start >= 2f)
            {
                TaskWindowStart[playerId] = now;
                TaskWindowCount[playerId] = 1;
                return false;
            }

            int count = TaskWindowCount.TryGetValue(playerId, out int existing) ? existing + 1 : 1;
            TaskWindowCount[playerId] = count;
            return count > 3;
        }
        catch { return false; }
    }

    internal static void Flag(PlayerControl actor, string reason, bool punish = true)
    {
        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null) return;
            int cid = actor != null ? actor.OwnerId : -1;
            string who = actor != null ? AmethystAccess.ClientName(net, cid) : AmethystText.T("Unknown");
            string whoShow = actor != null ? (AmethystColors.Name(actor) ?? who) : who;
            string msg = AmethystNotifyText.Body(whoShow, reason);
            if (cid < 0 || cid == net.ClientId) return;

            float now = Time.realtimeSinceStartup;
            if (Hit.TryGetValue(cid, out float t) && now - t < 6f) return;
            Hit[cid] = now;

            string playerId = cid.ToString();
            if (actor != null && actor.Data != null)
            {
                try { playerId = actor.PlayerId.ToString(); } catch { }
            }
            AmethystPlugin.LogCheat(who, playerId, reason);

            // 作弊历史（强制开启，只写文件，与封禁名单同目录）
            try
            {
                InnerNet.ClientData cd = AmethystAccess.FindClient(net, cid);
                AmethystHistory.Cheat(
                    who,
                    cd != null ? (cd.FriendCode ?? string.Empty) : string.Empty,
                    cd != null ? (cd.ProductUserId ?? string.Empty) : string.Empty,
                    AmethystAccess.PlatformOf(cd),
                    reason);
            }
            catch { }

            AmethystToast.Push(AmethystText.T("Guard"), msg, 5.0f, AmethystNotifyKind.Warning);
            AmethystEventLog.Add(who + " · " + msg, AmethystNotifyKind.Warning, AmethystEventCat.Other);

            if (!AmHost()) return;
            if (cid == net.HostId) return;
            if (punish)
                AmethystAccess.Act(net, cid, AmethystConfig.RpcGuardAction.Value, who, reason, true);
        }
        catch { }
    }

    internal static void NotifyOnce(string key, string reason, float cooldown = 4f)
    {
        try
        {
            if (Notif.TryGetValue(key, out float t) && Time.realtimeSinceStartup - t < cooldown) return;
            Notif[key] = Time.realtimeSinceStartup;
            AmethystToast.Push(AmethystText.T("Guard"), AmethystNotifyText.BodyAnonymous(reason), 5.0f, AmethystNotifyKind.Warning);
        }
        catch { }
    }

    // 透传/强制进管类（Ventilation Enter/BootImpostors）：包里的 actor 是“被强传/顶出的受害者”，
    // 并非作弊者（真凶是发这个包的那个客户端，但 ShipStatus.HandleRpc 拿不到发送者）。
    // 因此与 Flag 不同：不做惩罚，也不用“XX触发防护”的指责口吻，仅中性描述收到的强制进管、把 actor 标注为“目标”。
    internal static void NotifyVentForce(PlayerControl victim, string detail)
    {
        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null) return;
            int cid = victim != null ? victim.OwnerId : -1;

            float now = Time.realtimeSinceStartup;
            if (cid >= 0 && Hit.TryGetValue(cid, out float t) && now - t < 6f) return;
            if (cid >= 0) Hit[cid] = now;

            string name = victim != null
                ? AmethystAccess.ClientName(net, cid)
                : AmethystText.T("Unknown");
            string nameShow = victim != null ? (AmethystColors.Name(victim) ?? name) : name;
            string msg = AmethystText.T("VentTeleportForcedVentBlockedTarget", nameShow);
            string full = string.IsNullOrEmpty(detail) ? msg : $"{msg} · {detail}";
            AmethystToast.Push(AmethystText.T("Guard"), full, 5.0f, AmethystNotifyKind.Warning);
            AmethystEventLog.Add(name + " · " + full, AmethystNotifyKind.Warning, AmethystEventCat.Other);
            if (cid >= 0 && cid != net.ClientId && cid != net.HostId)
            {
                string playerId = (victim != null && victim.Data != null) ? victim.PlayerId.ToString() : cid.ToString();
                AmethystPlugin.LogCheat(name, playerId, string.IsNullOrEmpty(detail) ? "vent force" : detail);

                try
                {
                    InnerNet.ClientData cd = AmethystAccess.FindClient(net, cid);
                    AmethystHistory.Cheat(
                        name,
                        cd != null ? (cd.FriendCode ?? string.Empty) : string.Empty,
                        cd != null ? (cd.ProductUserId ?? string.Empty) : string.Empty,
                        AmethystAccess.PlatformOf(cd),
                        string.IsNullOrEmpty(detail) ? "vent force" : detail);
                }
                catch { }
            }
        }
        catch { }
    }
}

// ===== 第 199 轮：过快击杀 —— 记录点 =====
//
// 挂在**游戏真正执行击杀**的 `PlayerControl.MurderPlayer` 上，而不是 RPC 链路。
// 原因见 `AmethystRpcGuard.LastKillAt` 上方的长注释（一��合法击杀会产生
// CheckMurder + MurderPlayer 两个包，在 RPC 链路里计数必然误报）。
//
// `PlayerControl.MurderPlayer(PlayerControl target, MurderResultFlags resultFlags)`
// 在主机上是本地调用，在其他客户端上由 MurderPlayer RPC 走到 —— 所以
// 每一台机器都能拿到准确的"上一次击杀时刻"。
[HarmonyPatch(typeof(PlayerControl), "MurderPlayer")]
internal static class AmethystRecordKillPatch
{
    private static void Postfix(PlayerControl __instance, PlayerControl target)
    {
        try { AmethystRpcGuard.NoteKill(__instance, target); }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class AmethystRpcGuardPlayer
{
    private static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (AmethystConfig.RpcGuardPlayer == null || !AmethystConfig.RpcGuardPlayer.Value) return true;   // 独立开关（第 154 轮）
        if (!AmethystRpcGuard.CallAllowed(callId)) return true;   // 该 RPC 自己的开关（第 157 轮）
        if (!AmethystRpcGuard.On() || __instance == null || __instance == PlayerControl.LocalPlayer || __instance.Data == null || reader == null) return true;

        // 第 261 轮：未知 RPC ID / 网络对象不匹配（只针对非房主发送者）。
        if (AmethystRpcGuard.StructureGuardOn && AmongUsClient.Instance != null
            && __instance.OwnerId != AmongUsClient.Instance.HostId)
        {
            if (AmethystRpcGuard.IsWrongObject(AmethystRpcGuard.RpcObjectKind.Player, callId))
            {
                AmethystRpcGuard.Flag(__instance, AmethystText.T("RpcWrongObject"));
                return false;
            }
            // 第 263 轮：未知 RPC 不能拦截 —— 游戏/模组更新会引入新 RPC ID，
            // 拦截 + 提示会误伤正常玩家。只做对象归属检查，不丢未知 RPC，不再提示。
        }

        if (AmethystEarlyMeeting.ShouldBlock("rpc-prefix") &&
            (callId == (byte)RpcCalls.StartMeeting || callId == (byte)RpcCalls.ReportDeadBody))
        {
            AmethystEarlyMeeting.NotifyAttempt(__instance);
            return false;
        }

        // 第 263 轮：房主权威 RPC —— 房主只会**发出**，不会从网络收到自己的广播；
        // 房主端收到即客户端伪造。
        // ⚠ 这些 RPC 的网络对象是「目标玩家」，不是发送者：绝不能按 OwnerId 归因/处罚，
        //   否则作弊者可以把伪造包挂在别人身上，让无辜玩家被记录/踢出。
        if (AmethystRpcGuard.AmHost() && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay
            && (callId == (byte)RpcCalls.SetRole || callId == (byte)RpcCalls.SetInfected))
        {
            AmethystRpcGuard.NotifyOnce("host-authority-rpc", AmethystText.T("HostOnlyRPCByClient"));
            return false;
        }

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        bool drop = true;     // 任务类RPC不丢弃
        bool punish = true;   // 任务类RPC仅提醒，不处罚
        bool hardDrop = false; // 需要无条件丢弃（如大厅中的伪造对局RPC）
        try
        {
            if (AmongUsClient.Instance == null || AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
            {
                return true;
            }
            // 大厅中伪造对局类 RPC（未开局时发击杀/会议/变形/进管等）= 炸房/骚扰；所有端都直接丢弃
            if (AmethystRpcGuard.InLobby() && AmethystRpcGuard.IsLobbyGameRpc(callId))
            {
                cheat = true;
                hardDrop = true;
                reason = AmethystText.T("GameRPCInLobby");
            }
            if (callId == (byte)RpcCalls.SetScanner)
            {
                bool scanning = reader.ReadBoolean();
                if (scanning && ShipStatus.Instance == null)
                {
                    cheat = true;
                    reason = AmethystText.T("ScanWithoutMap");
                }
                else if (scanning && AmethystRpcGuard.IsImp(__instance))
                {
                    cheat = true;
                    reason = AmethystText.T("ScanByImpostor");
                }
                else if (scanning && !__instance.Data.IsDead && !__instance.Data.IsIncomplete
                    && AmethystRpcGuard.InMatch() && AmethystRpcGuard.ShipGraceOver()
                    && !AmethystRpcGuard.HasTask(__instance, TaskTypes.SubmitScan))
                {
                    cheat = true;
                    reason = AmethystText.T("ScanWithoutTask");
                }
            }
            else if (callId == (byte)RpcCalls.Exiled)
            {
                // Exiled RPC 在游戏中已废弃，任何发送均属作弊
                cheat = true;
                reason = AmethystText.T("UnusedExiledRPC");
            }
            else if (callId == (byte)RpcCalls.ReportDeadBody)
            {
                if (AmethystRpcGuard.MeetingActive())
                {
                    cheat = true;
                    reason = AmethystText.T("ReportDuringMeeting");
                }
                else if (__instance.Data.IsDead)
                {
                    cheat = true;
                    reason = AmethystText.T("ReportWhileDead");
                }
                else if (__instance.inVent || __instance.shapeshifting
                    || __instance.onLadder || __instance.inMovingPlat
                    || (__instance.MyPhysics != null && __instance.MyPhysics.Animations != null
                        && __instance.MyPhysics.Animations.IsPlayingAnyLadderAnimation()))
                {
                    cheat = true;
                    reason = AmethystText.T("ReportWhileBusy");
                }
                else
                {
                    int rp = reader.Position;
                    NetworkedPlayerInfo target = null;
                    bool parsed = false;
                    try { target = reader.ReadNetObject<NetworkedPlayerInfo>(); parsed = true; }
                    catch { }
                    finally { try { reader.Position = rp; } catch { } }

                    if (parsed)
                    {
                        if (target != null)
                        {
                            if (!target.IsDead)
                            {
                                cheat = true;
                                reason = AmethystText.T("ReportNotDead");
                            }
                            else if (__instance.Data != null && target.PlayerId == __instance.Data.PlayerId)
                            {
                                cheat = true;
                                reason = AmethystText.T("ReportSelf");
                            }
                        }
                        else if (__instance.RemainingEmergencies <= 0)
                        {
                            cheat = true;
                            reason = AmethystText.T("NoEmergencies");
                        }
                    }
                }
            }
            else if (callId == (byte)RpcCalls.MurderPlayer || callId == (byte)RpcCalls.CheckMurder)
            {
                PlayerControl target = null;
                int mpos = reader.Position;
                try { target = reader.ReadNetObject<PlayerControl>(); }
                catch { }
                finally { try { reader.Position = mpos; } catch { } }

                if (AmethystRpcGuard.InGame() && AmethystRpcGuard.RoleReady(__instance))
                {
                    byte pid = __instance.PlayerId;
                    bool actorDead = __instance.Data != null && __instance.Data.IsDead;
                    bool targetDead = target != null && target.Data != null && target.Data.IsDead;
                    bool targetIsImp = target != null && AmethystRpcGuard.IsImp(target);

                    if (actorDead)
                    {
                        cheat = true;
                        reason = AmethystText.T("DeadMurder");
                    }
                    else if (!AmethystRpcGuard.IsImp(__instance))
                    {
                        cheat = true;
                        reason = AmethystText.T("KillByCrewmate");
                    }
                    else if (targetIsImp && target != __instance)
                    {
                        cheat = true;
                        reason = AmethystText.T("KillImpostor");
                    }
                    // ★ 第 199/200 轮：过快击杀（击杀冷却）。
                    //   放在 targetIsImp 之后 —— 前面几条是"更根本"的违规
                    //   （已死 / 非内鬼 / 杀队友），让它们保留各自的原因文案。
                    //   hardDrop = true ⇒ 无条件本地丢弃这个包，第二次击杀不会生效；
                    //   处罚（警告/踢/封）仍由 RpcGuardAction 决定，见 Flag()。
                    //
                    //   第 200 轮：**只对"换了一个目标"生效**。
                    //   高延迟下连点三下会发出 3 个指向同一目标的包，
                    //   主机只认第一个，后两个是同一次意图的重复包，不该判作弊。
                    else if (AmethystRpcGuard.InFastKillWindow(pid, target))
                    {
                        cheat = true;
                        reason = AmethystText.T("FastKill");
                        hardDrop = true;
                    }
                    else if (targetDead)
                    {
                        int count = AmethystRpcGuard.DeadKillCount.TryGetValue(pid, out int c) ? c + 1 : 1;
                        AmethystRpcGuard.DeadKillCount[pid] = count;
                        if (count >= 6)
                        {
                            cheat = true;
                            reason = AmethystText.T("RepeatKillDead");
                        }
                    }
                    else
                    {
                        AmethystRpcGuard.DeadKillCount[pid] = 0;
                    }
                }
            }
            else if (callId == (byte)RpcCalls.Shapeshift || callId == (byte)RpcCalls.CheckShapeshift || callId == (byte)RpcCalls.RejectShapeshift)
            {
                if (AmethystRpcGuard.InMatch() && __instance.Data.Role != null && __instance.Data.Role.Role != RoleTypes.Shapeshifter)
                {
                    cheat = true;
                    reason = AmethystText.T("ShapeshiftWithoutRole");
                }
            }
            else if (callId == (byte)RpcCalls.StartVanish || callId == (byte)RpcCalls.StartAppear || callId == (byte)RpcCalls.CheckVanish || callId == (byte)RpcCalls.CheckAppear)
            {
                if (AmethystRpcGuard.InMatch() && __instance.Data.Role != null && __instance.Data.Role.Role != RoleTypes.Phantom)
                {
                    cheat = true;
                    reason = AmethystText.T("VanishAppearWithoutRole");
                }
            }
            else if (callId == (byte)RpcCalls.ProtectPlayer || callId == (byte)RpcCalls.CheckProtect)
            {
                int start = reader.Position;
                try
                {
                    PlayerControl target = reader.ReadNetObject<PlayerControl>();
                    if (target == null)
                    {
                        cheat = true;
                        reason = AmethystText.T("ProtectOverloading");
                    }
                }
                catch
                {
                    cheat = true;
                    reason = AmethystText.T("ProtectOverloading");
                }
                finally { try { reader.Position = start; } catch { } }
            }
            else if (callId == (byte)RpcCalls.SendChatNote)
            {
                if (AmethystRpcGuard.InLobby() || AmethystRpcGuard.InMatch())
                {
                    cheat = true;
                    reason = AmethystText.T("ChatNoteOutsideMeeting");
                }
            }
            else if (callId == (byte)RpcCalls.SetTasks)
            {
                drop = false; punish = false; // 任务RPC：不丢弃、仅提醒
                // 开局宽限内（角色/任务数据可能尚未就绪）正常开局分发任务不算作弊
                drop = false; punish = false;
                if (AmethystRpcGuard.InMatch() && AmethystRpcGuard.ShipGraceOver())
                {
                    cheat = true;
                    reason = AmethystText.T("SetTasksInMatch");
                }
            }
            else if ((callId == (byte)RpcCalls.StartMeeting || callId == (byte)RpcCalls.ReportDeadBody)
                && AmethystEarlyMeeting.ShouldBlock("rpc"))
            {
                if (!AmethystRpcGuard.MeetingGuardArmed())
                {
                    cheat = true;
                    reason = AmethystText.T("MeetingBeforeLoad");
                    AmethystEarlyMeeting.NotifyAttempt(__instance);
                }
            }
            else if (callId == (byte)RpcCalls.CompleteTask)
            {
                drop = false; punish = false; // 任务RPC：不丢弃、仅提醒（避免误丢正常完成任务）
                uint taskIndex = reader.ReadPackedUInt32();
                if (AmethystRpcGuard.TrackTaskRate(__instance.PlayerId))
                {
                    cheat = true;
                    reason = AmethystText.T("TaskCompletionFlood");
                }
                if (ShipStatus.Instance == null)
                {
                    cheat = true;
                    reason = AmethystText.T("TaskWithoutMap");
                }
                else if (AmethystRpcGuard.IsImp(__instance))
                {
                    cheat = true;
                    reason = AmethystText.T("TaskByImpostor");
                }
                else if (__instance.Data.Tasks == null || taskIndex + 1 > __instance.Data.Tasks.Count)
                {
                    cheat = true;
                    reason = AmethystText.T("ExtraTask");
                }
            }
            else if (callId == (byte)RpcCalls.SetLevel)
            {
                // ⚠ 第 165 轮（修复「多次设置等级」误报的**根因**）：
                //   原来只要玩家在 join-grace(10s) 之后再发一次 SetLevel，就判「多次设置等级」。
                //   但 SetLevel 在**大厅里是可以合法重发的**：
                //     · 玩家在大厅改了等级 / 又改回来（原版设置界面本身就会重发）；
                //     · 模组客户端在大厅同步自己的等级（Hydra 的 SpoofLevel、Tenkai 的 spoofLevel
                //       开关一次就发一次 RpcSetLevel，关掉时再发一次真实等级）；
                //     · 玩家重进房间 / 换账号后重新同步。
                //   这些全是合法行为，却被当成了作弊 —— 这就是误报来源。
                //   参考模组 Hydra（anticheat/rpc/SetLevel.cs）的判定是：
                //     「SetLevel 只允许在**大厅加入时**发送；一旦 ShipStatus 存在（已开局）即违规」，
                //     它**不数次数**。TenkaiMenu 的同类判定还额外排除本地游戏。
                //   这里改用同样的语义：大厅一律放行，开局后才判定。
                if (!AmethystRpcGuard.InJoinGrace()
                    && AmethystRpcGuard.InGame()
                    && AmethystRpcGuard.ShipGraceOver())
                {
                    cheat = true;
                    reason = AmethystText.T("SetLevelInMatch");
                }
            }
            else if (callId == (byte)RpcCalls.SetStartCounter)
            {
                int sp = reader.Position;
                int counter = -1;
                bool parsed = false;
                try { counter = reader.ReadPackedInt32(); parsed = true; }
                catch
                {
                    try { reader.Position = sp; counter = reader.ReadInt32(); parsed = true; } catch { }
                }
                finally { try { reader.Position = sp; } catch { } }

                // 非房主只允许发送 -1（Hydra 同规则）；其它值 = 伪造开始倒计时。
                if (parsed && AmongUsClient.Instance != null
                    && __instance.OwnerId != AmongUsClient.Instance.HostId
                    && counter != -1)
                {
                    cheat = true;
                    reason = AmethystText.T("InvalidStartCounter");
                }
            }
            else if (callId == (byte)RpcCalls.SyncSettings)
            {
                if (AmongUsClient.Instance != null && __instance.OwnerId != AmongUsClient.Instance.HostId)
                {
                    cheat = true;
                    reason = AmethystText.T("HostOnlyRPCByClient");
                }
            }
            else if (callId == (byte)RpcCalls.UsePlatform)
            {
                if (ShipStatus.Instance == null
                    || AmethystRpcGuard.CurrentMapId() != 4
                    || AmethystRpcGuard.IsHS()
                    || __instance.Data.IsDead)
                {
                    cheat = true;
                    reason = AmethystText.T("PlatformInvalid");
                }
            }
            else if (callId == (byte)RpcCalls.TriggerSpores || callId == (byte)RpcCalls.CheckSpore)
            {
                if (ShipStatus.Instance == null || AmethystRpcGuard.CurrentMapId() != 5)
                {
                    cheat = true;
                    reason = AmethystText.T("SporeWrongMap");
                }
            }
            else if (callId == (byte)RpcCalls.UseZipline)
            {
                if (ShipStatus.Instance == null
                    || AmethystRpcGuard.MeetingActive()
                    || __instance.Data.IsDead)
                {
                    cheat = true;
                    reason = AmethystText.T("ZiplineInvalid");
                }
            }
            else if (callId == (byte)RpcCalls.SetName || callId == (byte)RpcCalls.CheckName)
            {
                // ⚠ 第 165 轮：与 SetLevel 同理 —— 大厅里改名是合法且**可以多次**的
                //   （反复调名字、换装后客户端同步、模组改名）。
                //   原来只要 join-grace 过了再改一次就判「多次设置名称」，同样是误报源。
                //   现在只在对局中判定；大厅内的多次改名放行，
                //   刷屏风险由 RPC 洪水检测（AmethystRpcFlood）兜底。
                if (!AmethystRpcGuard.InJoinGrace()
                    && AmethystRpcGuard.InGame()
                    && AmethystRpcGuard.ShipGraceOver())
                {
                    cheat = true;
                    reason = AmethystText.T("SetNameInMatch");
                }
                int namePos = reader.Position;
                try
                {
                    if (callId == (byte)RpcCalls.SetName) reader.ReadUInt32();
                    string name = reader.ReadString();
                    if (!string.IsNullOrEmpty(name) && name.IndexOf('<') >= 0)
                    {
                        cheat = true;
                        reason = AmethystText.T("HTMLInName");
                    }
                }
                catch { }
                finally { try { reader.Position = namePos; } catch { } }
            }
            else if (callId == (byte)RpcCalls.ExtendLobbyTimer || callId == (byte)RpcCalls.CloseMeeting)
            {
                // 这类“房主专属RPC”只有主机拥有权威判定：非主机收到的是主机合法中继的
                // RPC（无法区分发送者），判定“客户端发送房主专属RPC”会造成误报，因此仅在主机端检测。
                if (AmethystRpcGuard.AmHost() && __instance.OwnerId != AmongUsClient.Instance.HostId)
                {
                    cheat = true;
                    reason = AmethystText.T("HostOnlyRPCByClient");
                }
            }
            else if (AmethystRpcGuard.IsOutfitCall(callId) && AmethystRpcGuard.InMatch() && !AmethystRpcGuard.InJoinGrace())
            {
                cheat = true;
                reason = AmethystText.T("OutfitChangeInMatch");
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat)
        {
            if (AmethystRpcGuard.InJoinGrace() && (callId == (byte)RpcCalls.Shapeshift || callId == (byte)RpcCalls.CheckShapeshift || callId == (byte)RpcCalls.RejectShapeshift || callId == (byte)RpcCalls.StartVanish || callId == (byte)RpcCalls.StartAppear || callId == (byte)RpcCalls.CheckVanish || callId == (byte)RpcCalls.CheckAppear || AmethystRpcGuard.IsOutfitCall(callId)))
                return !AmethystRpcGuard.ShouldDropRpc();

            AmethystRpcGuard.Flag(__instance, reason, punish);
            // 大厅伪造对局RPC：无条件本地丢弃，防止炸房效果落地
            if (hardDrop) return false;
            // 任务类RPC不丢弃，正常放行；其余按 DropRPC 设置决定是否丢弃。
            return drop ? !AmethystRpcGuard.ShouldDropRpc() : true;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), "HandleRpc")]
internal static class AmethystRpcGuardVent
{
    private static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        bool isVent = callId == (byte)RpcCalls.EnterVent || callId == (byte)RpcCalls.ExitVent;
        bool isMove = callId == (byte)RpcCalls.ClimbLadder || callId == (byte)RpcCalls.UsePlatform;
        if (!isVent && !isMove) return true;
        if (isVent && (AmethystConfig.RpcGuardVent == null || !AmethystConfig.RpcGuardVent.Value)) return true;
        if (!AmethystRpcGuard.On() || __instance == null || reader == null) return true;
        if (callId != (byte)RpcCalls.EnterVent && callId != (byte)RpcCalls.ExitVent
            && callId != (byte)RpcCalls.ClimbLadder && callId != (byte)RpcCalls.UsePlatform) return true;

        PlayerControl actor = __instance.myPlayer;
        if (actor == null || actor == PlayerControl.LocalPlayer || actor.Data == null) return true;

        if (AmethystRpcGuard.StructureGuardOn && AmongUsClient.Instance != null
            && actor.OwnerId != AmongUsClient.Instance.HostId)
        {
            if (AmethystRpcGuard.IsWrongObject(AmethystRpcGuard.RpcObjectKind.Physics, callId))
            {
                AmethystRpcGuard.Flag(actor, AmethystText.T("RpcWrongObject"));
                return false;
            }
            // 第 263 轮：未知 RPC 不丢，只做对象归属判定。
        }

        if (callId == (byte)RpcCalls.ClimbLadder || callId == (byte)RpcCalls.UsePlatform)
        {
            bool bad = false;
            string why = null;

            if (ShipStatus.Instance == null)
            {
                bad = true;
                why = AmethystText.T("MovementNoMap");
            }
            else if (callId == (byte)RpcCalls.ClimbLadder)
            {
                int map = AmethystRpcGuard.CurrentMapId();
                if (map != 4 && map != 5) { bad = true; why = AmethystText.T("LadderWrongMap"); }
                else if (actor.Data.IsDead) { bad = true; why = AmethystText.T("MovementWhileDead"); }
            }
            else
            {
                if (AmethystRpcGuard.CurrentMapId() != 4) { bad = true; why = AmethystText.T("PlatformWrongMap"); }
                else if (AmethystRpcGuard.IsHS()) { bad = true; why = AmethystText.T("PlatformInHS"); }
                else if (actor.Data.IsDead) { bad = true; why = AmethystText.T("MovementWhileDead"); }
            }

            if (bad)
            {
                AmethystRpcGuard.Flag(actor, why);
                return !AmethystRpcGuard.ShouldDropRpc();
            }
            return true;
        }

        // H&S 模式的管道规则由 AmethystHsSabotage 模块统一处理（可单独开关），避免双重判定/误报
        if (AmethystRpcGuard.IsHS()) return true;
        // 异常管道检测主/非主机独立开关
        if (!AmethystRpcGuard.VentRulesEnabled()) return true;

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        try
        {
            int ventId = reader.ReadPackedInt32();
            if (!AmethystRpcGuard.RealVent(ventId)) { cheat = true; reason = AmethystText.T("FakeVentId"); }
            else if (AmethystRpcGuard.MeetingGuardArmed())
            {
                // 会议期间非本机进管：主机不会替玩家发 EnterVent，开会时的出管是走 Ship Ventilation
                // 通道踢出，而非个人 EnterVent RPC。故会议期间出现进管 RPC = 作弊。
                cheat = true;
                reason = AmethystText.T("VentInMeeting");
            }
            else if (AmethystRpcGuard.ShipGraceOver() && AmethystRpcGuard.RoleReady(actor) && !actor.Data.Role.CanVent)
            { cheat = true; reason = AmethystText.T("VentWithoutPermission"); }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat)
        {
            AmethystRpcGuard.Flag(actor, reason);
            return !AmethystRpcGuard.ShouldDropRpc();
        }
        return true;
    }
}

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class AmethystRpcGuardShip
{
    private static bool Prefix(ShipStatus __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {

        if (AmethystConfig.RpcGuardShip == null || !AmethystConfig.RpcGuardShip.Value) return true;   // 独立开关（第 154 轮）
        if (!AmethystRpcGuard.On() || __instance == null || reader == null) return true;

        if (AmethystRpcGuard.StructureGuardOn)
        {
            if (AmethystRpcGuard.IsWrongObject(AmethystRpcGuard.RpcObjectKind.Ship, callId))
            {
                AmethystRpcGuard.NotifyOnce("rpc-wrong-object", AmethystText.T("RpcWrongObject"));
                return false;
            }
            // 第 263 轮：未知 RPC 不丢，只做对象归属判定。
        }

        int pos = reader.Position;
        bool block = false;
        bool victimForced = false; // Ventilation 类命中：actor 是被强传/顶出的受害者，非作弊者
        PlayerControl actor = null;
        string reason = null;
        try
        {
            // H&S 模式：关门/破坏/报告/寻猎者进管统一交给 AmethystHsSabotage 处理
            // （可在菜单单独开关），避免本通用守卫双重判定与错误归类导致的误报。
            if (AmethystRpcGuard.IsHS()) return true;

            if (callId == (byte)RpcCalls.CloseDoorsOfType)
            {
                // CloseDoorsOfType 不携带发送者对象，无法判定发送者身份。
                // H&S 模式中关门无任何合法用途，直接拦截（参考 Hydra CloseDoorsOfType）。
                if (AmethystRpcGuard.IsHS())
                {
                    block = true;
                    reason = AmethystText.T("DoorsInHS");
                }
                // 会议中关门：强规则
                else if (AmethystRpcGuard.MeetingGuardArmed())
                {
                    block = true;
                    reason = AmethystText.T("DoorsInMeeting");
                }
            }
            else if (callId == (byte)RpcCalls.UpdateSystem)
            {
                int sys = reader.ReadByte();
                actor = reader.ReadNetObject<PlayerControl>();


                if (sys == (int)SystemTypes.Ventilation)
                {
                    // 异常管道检测（ShipStatus 路径）：主机/非主机独立开关
                    if (!AmethystRpcGuard.VentRulesEnabled()) return true;
                    reader.ReadUInt16();
                    int op = reader.Position < reader.Length ? reader.ReadByte() : -1;
                    int vid = reader.Position < reader.Length ? reader.ReadByte() : -1;

                    if (op == (int)VentilationSystem.Operation.Enter)
                    {
                        // 会议中进管：会议期间进管一律拦截（强规则，不受宽限影响）
                        if (AmethystRpcGuard.MeetingGuardArmed())
                        {
                            block = true;
                            reason = AmethystText.T("VentInMeeting");
                        }
                        else if (AmethystRpcGuard.ShipGraceOver())
                        {
                            if (!AmethystRpcGuard.RealVent(vid)) { block = true; reason = AmethystText.T("FakeVentId"); }
                            else if (actor != null && actor != PlayerControl.LocalPlayer
                                && AmethystRpcGuard.RoleReady(actor) && !actor.Data.Role.CanVent)
                            {
                                block = true;
                                reason = AmethystText.T("VentForAnother");
                            }
                        }
                    }
                    else if (op == (int)VentilationSystem.Operation.BootImpostors)
                    {
                        // ⚠ 第 226 轮修复（用户实机反馈：**这里才是真正在误报的地方**）：
                        //   旧判定要求 `actor` 必须是「没死的内鬼」，否则判 ForcedVentEject。
                        //   但原版「清扫管道」任务打开管口把人弹出时，
                        //   actor 是**正在做清扫任务的船员**，不是内鬼 —— 于是必然误报，
                        //   表现就是「别人对我 / 我对别人做清扫任务都提示管道传送」，
                        //   并且因为我们把包拦掉了，导致**客户端之间不同步**。
                        //
                        //   清扫管道不是某张图专属（飞船 / 米拉总部 / 蘑菇岛 等多图都有），
                        //   BootImpostors 也不是外挂专属 —— 它是原版常规行为。
                        //
                        //   现在先放行「合法清扫弹管」：
                        //   判定用 `IsLegitVentClean(ventId)`（该管正在被清扫 + 该管里确实有人），
                        //   与 `AmethystVentTpProtect` 共用同一实现，避免两处判定不一致。
                        if (AmethystVentTpProtect.IsLegitVentClean(vid, actor, true))
                        {
                            // 合法清扫弹管：什么都不做，交给游戏正常处理
                        }
                        else if (AmethystRpcGuard.ShipGraceOver())
                        {
                            if (!AmethystRpcGuard.RealVent(vid)) { block = true; reason = AmethystText.T("FakeVentId"); }
                            else if (actor == null || actor.Data == null || actor.Data.IsDead
                                || !AmethystRpcGuard.IsImp(actor))
                            {
                                block = true;
                                reason = AmethystText.T("ForcedVentEject");
                            }
                            else if (actor != PlayerControl.LocalPlayer
                                && AmethystRpcGuard.RoleReady(actor) && !actor.Data.Role.CanVent)
                            {
                                block = true;
                                reason = AmethystText.T("VentForAnother");
                            }
                        }
                    }
                    if (block) victimForced = true; // Ventilation：actor 是被强传/顶出的受害者 → 中性提示、不惩罚
                }
                else if (AmethystRpcGuard.SabSystems.Contains(sys))
                {
                    int amount = reader.Position < reader.Length ? reader.ReadByte() : 0;
                    CheckSabotage(__instance, actor, sys, amount, ref block, ref reason);
                }
                else if (sys == (int)SystemTypes.Sabotage && reader.Position < reader.Length)
                {
                    // 客户端破坏请求的包装层：system=Sabotage → target=实际系统
                    int target = reader.ReadByte();
                    if (AmethystRpcGuard.SabSystems.Contains(target))
                    {
                        int amount = reader.Position < reader.Length ? reader.ReadByte() : 0;
                        CheckSabotage(__instance, actor, target, amount, ref block, ref reason);
                    }
                }
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (block)
        {
            // ⚠ 第 226 轮：管道的 BootImpostors 属于「客户端状态同步」类 RPC，
            //   **绝不能丢弃** —— 丢弃后本机会保留"还在管道里"的旧状态，
            //   而其他客户端都按主机同步把人弹出了，于是本机与全场不同步
            //   （用户实测反馈的"导致游戏不同步"就是这个）。
            //   所以这一类只做**提示**，把包照常交给游戏处理。
            //   本来就是 victimForced（受害者是被顶出的人，不是作弊者），不惩罚也不拦截是合理的。
            if (victimForced) { AmethystRpcGuard.NotifyVentForce(actor, reason); return true; }

            if (AmethystRpcGuard.InJoinGrace() && (callId == (byte)RpcCalls.CloseDoorsOfType || callId == (byte)RpcCalls.UpdateSystem))
                return !AmethystRpcGuard.ShouldDropRpc();

            if (actor != null) AmethystRpcGuard.Flag(actor, reason);
            else AmethystRpcGuard.NotifyOnce("ship-nobody", reason);
            return !AmethystRpcGuard.ShouldDropRpc();
        }
        return true;
    }

    // 破坏类检测统一入口（主分支与 Sabotage 包装分支共用），按固定顺序判定：
    // 会议中破坏 → 地图外破坏 → 快速破坏(刷屏) → 平民破坏
    private static void CheckSabotage(ShipStatus ship, PlayerControl actor, int sys, int amount, ref bool block, ref string reason)
    {
        if (AmethystRpcGuard.MeetingGuardArmed())
        {
            block = true;
            reason = AmethystText.T("SabotageInMeeting");
        }
        else if (ship.Systems != null && !ship.Systems.ContainsKey((SystemTypes)sys))
        {
            block = true;
            reason = AmethystText.T("SabotageOffMap");
        }
        else if (actor != null && actor != PlayerControl.LocalPlayer && actor.Data != null
            && !actor.Data.IsDead && !actor.Data.IsIncomplete
            && !AmethystRpcGuard.IsImp(actor)
            && (sys == (int)SystemTypes.MushroomMixupSabotage || (amount & 0x80) != 0))
        {
            block = true;
            reason = AmethystText.T("SabotageByCrew");
        }
        else if (actor != null && AmethystRpcGuard.CheckRapidSabotage(actor.PlayerId, sys, out var rapidReason))
        {
            block = true;
            reason = rapidReason;
        }
        else if ((amount & 0x80) != 0 && AmethystRpcGuard.IsMajorSabotageSystem(sys) && actor != null)
        {
            // 仅当“同一个玩家”在已有重大破坏未结束时，尝试触发“另一个不同”的重大破坏才算多次破坏。
            // 不同玩家各点各的、或同一玩家反复点同一个破坏（实际只生效一次）都不算。
            bool active = AmethystRpcGuard.MajorSabotageActive();
            if (active && AmethystRpcGuard.MajorSabotageByActor.TryGetValue(actor.PlayerId, out int prev) && prev != sys)
            {
                block = true;
                reason = AmethystText.T("MultiSabotage");
            }
            else if (!active)
            {
                AmethystRpcGuard.MajorSabotageByActor.Clear();
            }
            AmethystRpcGuard.MajorSabotageByActor[actor.PlayerId] = sys;
        }
    }
}

// 投票踢人系统（VoteBanSystem）：检测会议外的恶意投票、死人投票等。
// 此检测需要主机权限才能实际拦截投票，非主机仅做通知。
[HarmonyPatch(typeof(VoteBanSystem), "HandleRpc")]
internal static class AmethystVoteBanProtect
{
    private static bool Prefix(VoteBanSystem __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (AmethystConfig.RpcGuardVoteBan == null || !AmethystConfig.RpcGuardVoteBan.Value) return true;   // 独立开关（第 154 轮）
        if (!AmethystRpcGuard.On() || __instance == null || reader == null) return true;
        if (callId != (byte)RpcCalls.AddVote)
        {
            // 第 263 轮：未知 RPC 不丢，只做对象归属判定。
            if (AmethystRpcGuard.StructureGuardOn
                && AmethystRpcGuard.IsWrongObject(AmethystRpcGuard.RpcObjectKind.VoteBan, callId))
            {
                AmethystRpcGuard.NotifyOnce("rpc-wrong-object", AmethystText.T("RpcWrongObject"));
                return false;
            }
            return true;
        }

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        PlayerControl voter = null;
        try
        {
            int sourceId = reader.ReadInt32();
            int targetId = reader.ReadInt32();

            // 查找投票者
            ClientData sourceClient = AmongUsClient.Instance.FindClientById(sourceId);
            if (sourceClient != null && sourceClient.Character != null)
                voter = sourceClient.Character;

            // 死人不能投票
            if (voter != null && voter.Data != null && voter.Data.IsDead)
            {
                cheat = true;
                reason = AmethystText.T("VoteByDead");
            }
            // 会议外不能投票
            else if (!AmethystRpcGuard.MeetingActive())
            {
                cheat = true;
                reason = AmethystText.T("VoteOutsideMeeting");
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat)
        {
            string who = voter != null && voter.Data != null ? voter.Data.PlayerName : AmethystText.T("Unknown");
            int cid = voter != null ? voter.OwnerId : -1;
            if (AmethystRpcGuard.AmHost() && cid >= 0 && cid != AmongUsClient.Instance.HostId)
            {
                // 主机端：直接丢弃作弊投票
                AmethystRpcGuard.Flag(voter, reason);
                return false;
            }
            else
            {
                // 非主机：仅通知（无权丢弃）
                AmethystRpcGuard.NotifyOnce("vote-cheat-" + reason, who + " " + reason);
            }
        }
        return true;
    }
}

// 「模组客户端握手」与「对局中换装 RPC」两个原本各自挂在
// `InnerNetClient.HandleGameData` 的 Prefix 上、各自独立复制并重解析每一个收包。
// 现已并入 `Patches\AmethystGameDataScanner.cs` 的**单遍**扫描器：
// 一个包只遍历一次，这两件事在同一趟里顺手完成。

[HarmonyPatch(typeof(MeetingHud), "HandleRpc")]
internal static class AmethystRpcGuardMeeting
{
    private static bool Prefix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        // 第 263 轮：VotingComplete 是房主权威 RPC。房主只会发出、不会从网络收到，
        // 因此房主端收到 VotingComplete = 客户端伪造计票结果。
        if (callId == (byte)RpcCalls.VotingComplete)
        {
            if (!AmethystRpcGuard.CallAllowed(callId)) return true;
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                AmethystRpcGuard.NotifyOnce("voting-complete-forged", AmethystText.T("HostOnlyRPCByClient"));
                return false;
            }
            return true;
        }

        if (!AmethystRpcGuard.StructureGuardOn) return true;
        if (callId != (byte)RpcCalls.CastVote && callId != (byte)RpcCalls.ClearVote) return true;

        // 投票只能在会议中存在；大厅/会议结束后的 CastVote/ClearVote 一律丢弃。
        if (!AmethystRpcGuard.MeetingActive())
        {
            AmethystRpcGuard.NotifyOnce("vote-outside-meeting", AmethystText.T("VoteOutsideMeeting"));
            return false;
        }
        return true;
    }
}

/// <summary>
/// 第 263 轮：`SetTasks` 走 `NetworkedPlayerInfo.HandleRpc`（不是 PlayerControl）。
/// 房主只会发出、不会从网络收到；房主端收到 = 客户端伪造任务。
/// </summary>
[HarmonyPatch(typeof(NetworkedPlayerInfo), "HandleRpc")]
internal static class AmethystRpcGuardPlayerInfo
{
    private static bool Prefix([HarmonyArgument(0)] byte callId)
    {
        if (callId != (byte)RpcCalls.SetTasks) return true;
        if (!AmethystRpcGuard.CallAllowed(callId)) return true;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;
        AmethystRpcGuard.NotifyOnce("set-tasks-forged", AmethystText.T("HostOnlyRPCByClient"));
        return false;
    }
}

/// <summary>第 261 轮：加入游戏时标记宽错宽限 + 重置本局状态。</summary>
[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class AmethystRpcGuardJoinPatch
{
    private static void Postfix()
    {
        AmethystRpcGuard.MarkJoinGrace();
        AmethystRpcGuard.ResetRoundState();
    }
}

// 会议结束时清除检测状态
[HarmonyPatch(typeof(MeetingHud), "OnDisable")]
internal static class AmethystMeetingEndPatch
{
    private static void Postfix()
    {
        // 第 265 轮：OnDisable 可能在会议开始时也触发，**不要**在这里清会议时间戳，
        // 否则整场会议的检测会失效。时间戳由 MeetingHud.Close 清。
        AmethystRpcGuard.ClearRapidSabotage();
        AmethystRpcGuard.ResetRoundState();
        try
        {
            AmethystPlugin.Logger?.LogInfo(
                "[Guard] meeting hud disabled: meetingActive=" + AmethystRpcGuard.MeetingActive()
                + " exileActive=" + AmethystRpcGuard.ExileActive()
                + " inMatch=" + AmethystRpcGuard.InMatch());
        }
        catch { }
    }
}
