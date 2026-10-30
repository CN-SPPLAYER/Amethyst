using System;
using BepInEx.Configuration;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static class AmethystConfig
{
    internal static ConfigEntry<KeyCode> MenuKey;
    internal static ConfigEntry<KeyCode> CopyCodeKey;
    internal static ConfigEntry<KeyCode> RecapKey;

    internal static ConfigEntry<bool> Toasts;
    internal static ConfigEntry<bool> MenuButton;
    internal static ConfigEntry<float> MenuButtonX;
    internal static ConfigEntry<float> MenuButtonY;

    internal static ConfigEntry<float> MenuScale;

    internal static ConfigEntry<bool> BlockTelemetry;

    // JoinDetect（显示平台及等级）已按要求移除：模组不再自己发进/离提示，改由原版通知负责。

    internal static ConfigEntry<bool> AccessBanEnabled;
    internal static ConfigEntry<bool> CloudBanEnabled;
    internal static ConfigEntry<string> BanList;
    internal static ConfigEntry<bool> MinLevelEnabled;
    internal static ConfigEntry<int> MinLevel;
    internal static ConfigEntry<string> MinLevelAction;
    internal static ConfigEntry<bool> MaxLevelEnabled;
    internal static ConfigEntry<int> MaxLevel;
    internal static ConfigEntry<string> MaxLevelAction;
    internal static ConfigEntry<bool> RpcGuard;
    internal static ConfigEntry<bool> DropRPC;
    internal static ConfigEntry<string> RpcGuardAction;
    internal static ConfigEntry<bool> RpcFloodDetect;
    internal static ConfigEntry<bool> RpcGuardPlayer;
    internal static ConfigEntry<bool> UnknownRpcGuard;
    // ===== 第 157 轮：RPC 检测细化到"每个 RPC 调用一个开关" =====
    internal static readonly string[] RpcCallNames =
    {
        "AddVote", "CancelPet", "CheckAppear", "CheckMurder", "CheckName", "CheckProtect",
        "CheckShapeshift", "CheckVanish", "CheckMurderPlayer", "CloseDoorsOfType", "CloseMeeting",
        "CompleteTask", "EnterVent", "Exiled", "ExitVent", "ExtendLobbyTimer", "MurderPlayer",
        "PlayAnimation", "ProtectPlayer", "RejectShapeshift", "ReportDeadBody", "SendChatNote",
        "SetColor", "SetHatStr", "SetLevel", "SetName", "SetNamePlateStr", "SetPetStr", "SetScanner",
        "SetSkinStr", "SetTasks", "SetVisorStr", "Shapeshift", "StartAppear", "StartMeeting",
        "StartVanish", "UpdateSystem"
    };

    internal static readonly System.Collections.Generic.Dictionary<string, ConfigEntry<bool>> RpcCallSwitches =
        new System.Collections.Generic.Dictionary<string, ConfigEntry<bool>>();

    internal static bool RpcCallEnabled(string name)
    {
        try
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (RpcCallSwitches.TryGetValue(name, out ConfigEntry<bool> e) && e != null) return e.Value;
            return true;
        }
        catch { return true; }
    }

    // ===== 第 180 轮性能：callId → ConfigEntry 的直查表 =====
    //
    // 旧路径 `RpcCallEnabled(((RpcCalls)callId).ToString())` 每包都要：
    //   ① `Enum.ToString()` —— 反射式名称查找 + **分配一个新字符串**；
    //   ② `Dictionary<string, …>.TryGetValue` —— 又一次字符串哈希。
    // 而它出现在 `PlayerControl` / `PlayerPhysics` / `ShipStatus` 三个
    // `HandleRpc` 补丁的第一行，即"绝大多数包都会被放行"的那条路径上。
    //
    // 现在 Bind 结束时把 256 个槽位按 callId 填好对应的 `ConfigEntry` 引用，
    // 运行期只做一次数组下标。表里存的是**引用**而不是 bool 快照，
    // 所以用户在菜单/配置文件里改开关后依然即时生效。
    private static readonly System.Collections.Generic.List<ConfigEntry<bool>> RpcCallTable =
        new System.Collections.Generic.List<ConfigEntry<bool>>(256);

    private static int[] RpcCallIndex;   // callId → RpcCallTable 下标，-1 = 不在表内

    internal static bool CallAllowed(byte callId)
    {
        try
        {
            int[] idx = RpcCallIndex;
            if (idx == null) return true;
            int i = idx[callId];
            if (i < 0) return true;
            ConfigEntry<bool> e = RpcCallTable[i];
            return e == null || e.Value;
        }
        catch { return true; }
    }

    private static void BuildRpcCallTable()
    {
        try
        {
            int[] idx = new int[256];
            for (int i = 0; i < 256; i++) idx[i] = -1;

            RpcCallTable.Clear();
            for (int i = 0; i < RpcCallNames.Length; i++)
            {
                string name = RpcCallNames[i];
                if (string.IsNullOrEmpty(name)) continue;
                if (!Enum.IsDefined(typeof(RpcCalls), name)) continue;

                byte id = (byte)Enum.Parse(typeof(RpcCalls), name);
                if (idx[id] >= 0) continue;   // 同名重复，保留先出现的那个

                idx[id] = RpcCallTable.Count;
                RpcCallTable.Add(RpcCallSwitches.TryGetValue(name, out ConfigEntry<bool> ce) ? ce : null);
            }

            RpcCallIndex = idx;
        }
        catch
        {
            RpcCallIndex = null;   // 建表失败就退回"全部放行"，不影响功能
        }
    }
    internal static ConfigEntry<bool> RpcGuardVent;
    internal static ConfigEntry<bool> RpcGuardShip;
    internal static ConfigEntry<bool> RpcGuardVoteBan;
    internal static ConfigEntry<bool> RpcGameDataCheck;
    internal static ConfigEntry<int> RpcFloodThreshold;
    internal static ConfigEntry<bool> AntiBanHost;
    internal static ConfigEntry<string> AntiBanHostAction;
    internal static ConfigEntry<bool> KickFortegreen;
    internal static ConfigEntry<bool> BlockFakeMeetings;
    internal static ConfigEntry<bool> HsSabotageGuard;
    internal static ConfigEntry<bool> VentHostRules;
    internal static ConfigEntry<bool> VentClientRules;
    internal static ConfigEntry<bool> SpawnFloodGuard;

    // 第 199 轮：过快击杀（击杀冷却）。
    // 秒数；设为 0 即关闭。原版击杀冷却是 15~25s，所以 8s 不会误伤正常玩家。
    // 第 203 轮：**这个值只在经典局（Normal / NormalFools）使用**；
    //   躲猫猫（HideNSeek / SeekFools）里内鬼的击杀冷却只有 1s，8s 窗口会把每一次
    //   合法连杀都判成作弊，所以那里阈值被强制改成 **0.01s**（见 RpcGuardPatches.FastKillWindow）。
    //   切回经典会自动恢复成这里的值。
    internal static ConfigEntry<bool> FastKillGuard;
    internal static ConfigEntry<float> FastKillWindow;
    internal static ConfigEntry<bool> PlatformSpoofGuard;
    internal static ConfigEntry<bool> InvalidFcGuard;
    internal static ConfigEntry<bool> BotGuard;
    internal static ConfigEntry<int> CheaterActionThreshold;

    // ⚠ 第 219 轮：按用户挑选，从 OnyxMenu(Nocturne) 1.2.3 移植的三项反作弊功能。
    //   #1 大厅投票踢人拦截（OnyxMenu `Guard.VoteKickProtect` / `Patches\AccessGuardPatches.cs`）
    //   #2 幻影 DATA 对象洪水拦截（OnyxMenu `Guard.BlockDataFlood` / `Security\DataFlood.cs`）
    //   #3 外来模组通用识别（OnyxMenu `Security\ForeignMods.cs` 的启发式，扩到既有已知表上）
    internal static ConfigEntry<bool> VoteKickProtect;
    internal static ConfigEntry<string> VoteKickAction;
    internal static ConfigEntry<bool> BlockDataFlood;
    internal static ConfigEntry<bool> ForeignModDetect;

    // ===== 第 241 轮：按 Hydra 2.1.0 补齐的防护项 =====
    // 参考：`参考模组\Hydra-2.1.0\src\modules\protections\*`
    internal static ConfigEntry<bool> ForceDtls;                 // 强制 DTLS
    internal static ConfigEntry<bool> BlockServerTeleports;      // 拦截服务器位置更新（SnapTo）
    internal static ConfigEntry<bool> BlockUnauthorizedUpdates;  // 拦截非主机的 UpdateSystem / CloseDoorsOfType
    internal static ConfigEntry<bool> ReportDeadBodyCrashGuard;  // 防护 ReportDeadBody 加载期崩溃漏洞
    internal static ConfigEntry<bool> AntiOverloadEnabled;       // 防过载：总开关
    internal static ConfigEntry<bool> HardenPackedUInt;          // 加固压缩整数反序列化
    internal static ConfigEntry<bool> AntiKickEnabled;           // 防踢：总开关
    internal static ConfigEntry<bool> HostOnlyRpcKickGuard;      // 防护仅房主 RPC 踢人漏洞
    internal static ConfigEntry<bool> BypassShapeshiftRateLimit; // 绕过变形 RPC 的频率限制


    internal static ConfigEntry<bool> VentTpProtect;
    internal static ConfigEntry<bool> ZiplineProtect;

    // 破坏修复位置校验（防「隔空修复」）：修复类 UpdateSystem 必须站在控制台旁
    internal static ConfigEntry<bool> CheckSabotageRepair;
    internal static ConfigEntry<float> SabotageRepairDistance;

    // 会议后没有内鬼 → 自动结算，避免 NoImpostorsPresent 黑屏
    internal static ConfigEntry<bool> NoImpostorGameOver;
    // 0.01 秒内连关多扇门（一键关门外挂）→ 只放行第一扇
    internal static ConfigEntry<bool> DoorSpamGuard;

    // 网络/防崩溃防护（放反作弊页·防护）
    internal static ConfigEntry<bool> BlockLargeGameMessages;
    internal static ConfigEntry<bool> BlockInvalidGameDataTypes;
    internal static ConfigEntry<bool> BlockVotingCompleteOverload;

    internal static ConfigEntry<bool> EventConsole;
    internal static ConfigEntry<int> EventFilterMask;
    internal static ConfigEntry<bool> SecurityNotify;

    internal static ConfigEntry<bool> UnlockFps240;
    internal static ConfigEntry<int> FpsLimit;
    internal static ConfigEntry<bool> AutoReturnLobby;
    internal static ConfigEntry<bool> UnlockMatchKickBan;
    internal static ConfigEntry<bool> BlockEarlyMeetings;
    internal static ConfigEntry<bool> NoWinConditions;
    internal static ConfigEntry<bool> BetterChat;
    internal static ConfigEntry<bool> DarkChatTheme;
    internal static ConfigEntry<bool> VisualAlwaysShowChat;

    internal static ConfigEntry<bool> ChatHistory80;
    internal static ConfigEntry<bool> ForceShowStart;
    /// <summary>技能/使用/击杀/报告等动作按钮的缩放倍数（0.1 ~ 1.5，默认 0.85）。</summary>
    internal static ConfigEntry<float> AbilityUiScale;
    internal static ConfigEntry<bool> SkipKillAnimation;
    internal static ConfigEntry<bool> SabotageCooldownDisplay;
    internal static ConfigEntry<bool> OutfitSaver;
    internal static ConfigEntry<bool> BetterPingDisplay;
    internal static ConfigEntry<bool> PingShowLatency;
    internal static ConfigEntry<bool> PingShowFps;
    internal static ConfigEntry<bool> PingShowServer;
    internal static ConfigEntry<bool> PingShowRoom;
    internal static ConfigEntry<bool> PingShowHost;
    internal static ConfigEntry<bool> UiTheme;
    internal static ConfigEntry<bool> BetterCooldownDisplay;
    internal static ConfigEntry<bool> UnlockAllCosmetics;
    internal static ConfigEntry<bool> BypassDisconnectPenalty;
    internal static ConfigEntry<bool> DetectHostTempBan;
    internal static ConfigEntry<bool> ExpandLobbySearch;
    internal static ConfigEntry<bool> RoomInfoDisplay;
    internal static ConfigEntry<bool> RoomSortByMaxPlayers;

    // 模组房间列表（独立功能：房主上报 + 菜单浏览 + 一键加入）
    internal static ConfigEntry<bool> RoomListEnabled;
    // 是否把自己的房间上报到公共列表（隐私开关）
    internal static ConfigEntry<bool> RoomListReportOwnRoom;
    internal static ConfigEntry<int> RoomListReportSeconds;
    internal static ConfigEntry<int> RoomListRefreshSeconds;
    internal static ConfigEntry<string> RoomListApiBase;
    // 房间上报的 HMAC 密钥：只从本机配置读取，不写进源码/仓库。
    internal static ConfigEntry<string> RoomListSecret;
    internal static ConfigEntry<bool> ModClientDetection;
    internal static ConfigEntry<bool> ShowRolesWhenDead;
    internal static ConfigEntry<bool> ShowSelfRole;
    internal static ConfigEntry<bool> ShowImpostorTeammateRole;

    // ⚠ 第 171 轮（新功能）：对局内「职业信息」面板（像原版任务面板那样点一下收起/展开）。
    internal static ConfigEntry<bool> RolePanel;
    internal static ConfigEntry<bool> ShowMeetingHost;
    internal static ConfigEntry<bool> ColorBlindText;
    // 说明：原先的「界面优化」总开关（UiOptimization）已取消——
    // 现在界面优化下的每个小功能都是独立开关（见下方 Bind 中的 Other 段）。
    internal static ConfigEntry<bool> MainMenuOptimize;
    internal static ConfigEntry<bool> MainMenuButtons;
    internal static ConfigEntry<bool> HoverThemeColor;   // 鼠标悬停可交互按钮 → 悬停态与边框染成主题色
    internal static ConfigEntry<bool> MainMenuArt;
    internal static ConfigEntry<bool> MainMenuStyle;   // 主界面 EHR 风格：三档紫按钮 / 模组印记缩小 / 右面板关闭键
    internal static ConfigEntry<bool> UnlockFriends;    // 访客解锁：好友列表（第 195 轮，根因是 EOSManager.IsFriendsListAllowed 返回 false）
    internal static ConfigEntry<bool> UnlockFreeChat;  // 访客解锁：自由聊天
    internal static ConfigEntry<bool> UnlockOnline;    // 访客解锁：联网 / 未成年限制 / 加入公共房间
    internal static ConfigEntry<bool> UnlockGuestName; // 访客解锁：自定义昵称
    // ⚠ 第 200 轮（新功能）：强制成年 —— 把账号的 KWS 未成年标记强制清掉。
    //   背景：账号被标成 KWS 未成年后 `EOSManager.IsFreechatAllowed()` 会变 false，
    //   表现就是"登录成功但只能用快速聊天"。OnyxMenu 的 RemoveMinorLimits 是同类功能。
    internal static ConfigEntry<bool> ForceAdult;
    internal static ConfigEntry<bool> SnowEffect;
    internal static ConfigEntry<bool> RoleTag;
    internal static ConfigEntry<bool> RecapInfo;
    internal static ConfigEntry<bool> NameColors;
    // ⚠ 第 172 轮（新功能）：职业颜色 —— 为**原版每个职业**渲染独立颜色
    // （参考 EndlessHostRoles 的 RoleHtmlColors：大嗓门 #ff4a62 偏橘偏红、工程师 #FF6A00 等）。
    internal static ConfigEntry<bool> RoleColors;

    // ===== 第 207 轮（从 EHR 提取的功能）=====
    // ① 大厅环境音量修复：会议 / 对局结束后原版把环境音量压到 0 后不总是还原，
    //    表现就是回到大厅后背景音乐很小。
    internal static ConfigEntry<bool> FixLobbyMusic;
    // 关闭大厅音乐
    internal static ConfigEntry<bool> MuteLobbyMusic;
    // ② 胜利者身份揭示：结算界面在每个获胜玩家名字下显示其职业。
    internal static ConfigEntry<bool> WinnerRoleReveal;
    // ③ 大厅显示他人信息：在每名玩家头顶显示平台 / 等级 / 好友码。
    internal static ConfigEntry<bool> LobbyPlayerInfo;
    // ④ 踢出加入缓慢的玩家（仅主机）：加入后超过 N 秒数据仍未就绪就踢出。
    internal static ConfigEntry<bool> KickSlowJoiners;
    internal static ConfigEntry<float> SlowJoinSeconds;

    // ⚠ 第 173 轮（新功能）：性能分区。
    //
    // 参考 EndlessHostRoles 8.0.1 的 `SystemSettings` 一组开关：
    //   · `DontUpdateDeadPlayers`（其 OptionHolder.cs 第 1734 行，默认 **true**）
    //     —— 挂在 `PlayerControl.FixedUpdate` 上，对**已死亡**的玩家做跳帧节流
    //        （其 PlayerControlPatch.cs 第 1612~1619 行，buffer 默认 60、DeepLowLoad 时 150）。
    //   · `LowLoadMode`（第 1727 行，默认 true）/ `DeepLowLoad`（第 1731 行，默认 false）
    //     —— 配合 `Modules\FixedUpdateCaller.cs` 的**轮询调度**：每帧只完整跑一名玩家的
    //        Postfix、其余玩家走 lowLoad 分支（见其 FixedUpdatePatch.Postfix(pc, lowLoad)）。
    //
    // 这里按同样的思路做三件事（都能各自独立开关）：
    //   1. DontUpdateDeadPlayers —— 死人不更新（收益最大）
    //   2. LowLoadMode          —— 低负载轮询（人多时按帧轮流更新）
    //   3. HideConsole          —— 启动时隐藏黑色控制台窗口
    internal static ConfigEntry<bool> PerfDontUpdateDead;
    internal static ConfigEntry<int> PerfDeadSkipFrames;
    internal static ConfigEntry<bool> PerfLowLoad;
    internal static ConfigEntry<bool> PerfHideConsole;
    internal static ConfigEntry<bool> PerfProbe;

    // 职业分配算法（开发者选项）：0=默认 1=System 2=Xorshift 3=MersenneTwister
    internal static ConfigEntry<int> RoleAlgorithm;

    // ⚠ 第 200 轮（临时定位用）：两个"主界面/账号相关"模块的独立开关。
    //   这两个模块（AmethystFriendsBar / AmethystMainMenuStyle）
    //   在旧版可用快照里都不存在，且都会直接改动主界面与账号相关对象。
    //
    //   ⚠ 第 218 轮：同组的第三个开关 `Diag.DisableFriendsListOpen` **已删除**。
    //     它当年被二分定位留在"关闭"状态、事后忘了打开（默认值就是 true），
    //     导致第 199 轮写好的「好友按钮点了不出面板」修复一直没生效 ——
    //     实机 cfg 里 `DisableFriendsListOpen = true`，日志里从无 `[FriendsOpen]` 行。
    //     `AmethystFriendsListOpen` 的两个补丁都只在**点好友按钮**时才触发，
    //     与启动/账号加载无关，不该被这个开关关掉，故不再保留开关。
    internal static ConfigEntry<bool> DiagDisableFriendsBar;        // 默认 false（打开）
    internal static ConfigEntry<bool> DiagDisableMainMenuStyle;     // 默认 false（打开）

    // ⚠ 第 176 轮：会议中显示任务面板（参考 AUnlocker 的 ShowTaskPanelInMeetings）。
    internal static ConfigEntry<bool> MeetingTaskPanel;
    internal static ConfigEntry<bool> CooldownDecimals;
    internal static ConfigEntry<bool> ColorSnipe;
    internal static ConfigEntry<int> ColorSnipeId;
    internal static ConfigEntry<bool> AntiCheat;

    internal static ConfigEntry<string> Language;
    internal static ConfigEntry<KeyCode> MusicKey;
    // 播放器内快捷键（仅在播放器窗口打开时生效，和 MusicKey 一样可在 设置→快捷键 里改）
    internal static ConfigEntry<KeyCode> MusicPrevKey;
    internal static ConfigEntry<KeyCode> MusicNextKey;
    internal static ConfigEntry<KeyCode> MusicPlayPauseKey;
    // 循环模式：Off = 顺序播放（放完即停）、All = 列表循环、One = 单曲循环
    internal static ConfigEntry<string> MusicRepeat;
    internal static ConfigEntry<bool> MusicMuteGameMusic;
    internal static ConfigEntry<bool> ScaleLobbyPane;
    internal static ConfigEntry<float> MusicWinX;
    internal static ConfigEntry<float> MusicWinY;
    internal static ConfigEntry<float> MusicVolume;
    // ThemeIndex 已移除：主题固定为 #A06EFF（见 AmethystStyle.Current）

    internal static void Bind(ConfigFile config)
    {
        MenuKey = config.Bind("Keys", "MenuKey", KeyCode.Insert, "Open/close the menu.");
        if (MenuKey.Value == KeyCode.None) MenuKey.Value = KeyCode.Insert;
        CopyCodeKey = config.Bind("Keys", "CopyCodeKey", KeyCode.F6, "Copy the lobby code.");
        RecapKey = config.Bind("Keys", "RecapKey", KeyCode.F2, "Show/hide the recap info window.");
        MusicKey = config.Bind("Keys", "MusicKey", KeyCode.M, "Open/close the music player.");
        MusicPrevKey = config.Bind("Keys", "MusicPrevKey", KeyCode.LeftBracket, "Previous track (held while the player is open).");
        MusicNextKey = config.Bind("Keys", "MusicNextKey", KeyCode.RightBracket, "Next track (held while the player is open).");
        MusicPlayPauseKey = config.Bind("Keys", "MusicPlayPauseKey", KeyCode.P, "Play/pause (held while the player is open).");
        MusicMuteGameMusic = config.Bind("Music", "MuteGameMusic", true,
            "While the music player is playing, mute the game's own lobby / main-menu music.");
        ScaleLobbyPane = config.Bind("Other", "ScaleLobbyPane", true, "Scale the lobby room settings panel to 0.7x.");
        // 播放器窗口位置：拖动结束后写回，下次打开还在原位（-1 = 用默认位置）
        MusicWinX = config.Bind("Music", "WinX", -1f, "Saved X of the music player window (-1 = default).");
        MusicWinY = config.Bind("Music", "WinY", -1f, "Saved Y of the music player window (-1 = default).");
        MusicVolume = config.Bind("Music", "Volume", 0.7f, "Music player volume (0..1).");
        MusicRepeat = config.Bind("Music", "Repeat", "All",
            "Repeat mode: One = repeat current track, All = loop the playlist, Off = play the list once then stop.");

        Toasts = config.Bind("QoL", "Toasts", true, "Pop-up notifications.");
        MenuButton = config.Bind("QoL", "MenuButton", true, "On-screen button under the Amethyst badge to open the menu (touch-friendly).");
        MenuButtonX = config.Bind("QoL", "MenuButtonX", -1f, "Saved X of the on-screen menu button (-1 = auto).");
        MenuButtonY = config.Bind("QoL", "MenuButtonY", -1f, "Saved Y of the on-screen menu button (-1 = auto).");

        MenuScale = config.Bind("QoL", "MenuScale", 1f, "Menu scale multiplier.");

        BlockTelemetry = config.Bind("Privacy", "BlockTelemetry", true, "Disable analytics, crash reports and performance reporting.");

        // JoinDetect（"显示平台及等级 / 原始平台名"）已移除，不再绑定。

        AccessBanEnabled = config.Bind("Guard", "AccessBanEnabled", true, "Kick/ban players from the ban list on join.");
        CloudBanEnabled = config.Bind("Guard", "CloudBanEnabled", true, "Kick/ban players from the cloud ban list on join.");
        BanList = config.Bind("Guard", "BanList", "", "Ban list (FriendCode, comma-separated).");
        MinLevelEnabled = config.Bind("Guard", "MinLevelEnabled", false, "React to players below the minimum level.");
        MinLevel = config.Bind("Guard", "MinLevel", 1, "Minimum level.");
        MinLevel.Value = Mathf.Clamp(MinLevel.Value, 1, 500);
        MinLevelAction = config.Bind("Guard", "MinLevelAction", "Kick", "Action on low level: Kick, Ban.");
        MaxLevelEnabled = config.Bind("Guard", "MaxLevelEnabled", false, "React to players above the maximum level.");
        MaxLevel = config.Bind("Guard", "MaxLevel", 500, "Maximum level.");
        MaxLevel.Value = Mathf.Clamp(MaxLevel.Value, 1, 999);
        MaxLevelAction = config.Bind("Guard", "MaxLevelAction", "Kick", "Action on high level: Kick, Ban.");
        RpcGuard = config.Bind("Guard", "RpcGuard", true, "Detect impossible RPCs by role/map (sabotage as crew, illegal vent, etc.).");
        DropRPC = config.Bind("Guard", "DropRPC", true, "Drop blocked RPCs when you are host. Off-host this is forced off.");
        RpcGuardAction = config.Bind("Guard", "RpcGuardAction", "Kick", "Action on the cheater: Null, Warn, Kick, Ban.");
        RpcFloodDetect = config.Bind("Guard", "RpcFloodDetect", true, "Notify-only RPC flood detection (never blocks or kicks).");
        RpcGuardPlayer = config.Bind("Guard", "RpcGuardPlayer", true, "Notify-only RPC flood detection (never blocks or kicks).");
        UnknownRpcGuard = config.Bind("Guard", "UnknownRpcGuard", true, "Reject RPCs delivered to the wrong net object. Unknown RPC IDs are always allowed (game updates add new IDs).");
        foreach (string rpcName in RpcCallNames)
            RpcCallSwitches[rpcName] = config.Bind("AntiCheat.RPC", "Guard " + rpcName, true,
                "单独开关某一个 RPC 的防护（默认全部开启）");
        RpcGuardVent = config.Bind("Guard", "RpcGuardVent", true, "Notify-only RPC flood detection (never blocks or kicks).");
        RpcGuardShip = config.Bind("Guard", "RpcGuardShip", true, "Notify-only RPC flood detection (never blocks or kicks).");
        RpcGuardVoteBan = config.Bind("Guard", "RpcGuardVoteBan", true, "Notify-only RPC flood detection (never blocks or kicks).");
        RpcGameDataCheck = config.Bind("Guard", "RpcGameDataCheck", true, "Notify-only RPC flood detection (never blocks or kicks).");
        // ⚠ 第 220 轮：默认值 20 → **35**（用户要求）。
        //   注意 BepInEx 只在**键不存在**时用默认值；老玩家的 .cfg 里已经写着 20，改默认值**不会**改到他们，
        //   需要手动调滑块或删掉这一行。（本次已同步把实机 cfg 的 20 改成 35。）
        RpcFloodThreshold = config.Bind("Guard", "RpcFloodThreshold", 25, "RPCs per second from one client before a suspicion toast.");
        // 第 263 轮：按用户要求把「最低限值」定为 25，避免设置过低导致误报。
        RpcFloodThreshold.Value = Mathf.Clamp(RpcFloodThreshold.Value, 25, 500);
        AntiBanHost = config.Bind("Guard", "AntiBanHost", true, "As host, punish the sender of the ban/crash exploit.");
        AntiBanHostAction = config.Bind("Guard", "AntiBanHostAction", "Ban", "Action on the sender: Null, Warn, Kick, Ban.");
        KickFortegreen = config.Bind("Guard", "KickFortegreen", true, "Host: kick players with the Fortegreen color.");
        BlockFakeMeetings = config.Bind("Guard", "BlockFakeMeetings", true, "Block fake meeting UI in the lobby (spoofed MeetingHud).");
        HsSabotageGuard = config.Bind("Guard", "HsSabotageGuard", true, "Hide & Seek rules: report, doors, seeker venting, sabotage. Works without the main RPC guard.");
        VentHostRules = config.Bind("Guard", "VentHostRules", true, "As HOST: detect abnormal vent/pipe use (vent in meeting, vent without vent-right, fake vent id, venting on another player's behalf). Needs RpcGuard.");
        VentClientRules = config.Bind("Guard", "VentClientRules", true, "As NON-HOST: also run the abnormal vent/pipe detection (local notice only, cannot drop or punish from the server side).");
        SpawnFloodGuard = config.Bind("Guard", "SpawnFloodGuard", true, "Trim a spawn flood (many spawns on one frame) to stop freezes/crashes. 5s grace after join/game start.");
        FastKillGuard = config.Bind("Guard", "FastKillGuard", true, "Enable fast kill detection (kill cooldown violation).");
        FastKillWindow = config.Bind("Guard", "FastKillWindow", 8f, "Seconds after a completed kill during which the same player may not kill a DIFFERENT player. Repeated attempts on the same target are ignored (high-latency double clicks). A kill on a different target inside the window is dropped and flagged. 0 disables. This value is used in CLASSIC modes only; in Hide N Seek the threshold is forced to 0.01s (impostor kill cooldown there is 1s) and switches back automatically.");
        PlatformSpoofGuard = config.Bind("Guard", "PlatformSpoofGuard", false, "Detect invalid/spoofed platform data on player join.");
        InvalidFcGuard = config.Bind("Guard", "InvalidFcGuard", false, "Detect invalid friend code format on player join.");
        BotGuard = config.Bind("Guard", "BotGuard", false, "Detect bot platform signatures on player join.");
        CheaterActionThreshold = config.Bind("Guard", "CheaterActionThreshold", 3, "How many violations by the same player before kick/ban action is triggered (1-8).");
        CheaterActionThreshold.Value = Mathf.Clamp(CheaterActionThreshold.Value, 1, 8);



        VentTpProtect = config.Bind("Guard", "VentTpProtect", true, "Block forced vent teleport/boot aimed at you.");
        ZiplineProtect = config.Bind("Guard", "ZiplineProtect", true, "Block forced zipline rides aimed at you.");

        CheckSabotageRepair = config.Bind("Guard", "CheckSabotageRepair", true, "Validate that sabotage repairs come from a player standing near the console.");
        // 3.5 而不是 BanMod 的 2.0~3.4：主机看到的坐标会因延迟略微滞后，
        // 阈值过紧会把「刚跑到控制台就修」的正常操作判成隔空修复。
        SabotageRepairDistance = config.Bind("Guard", "SabotageRepairDistance", 3.5f, "Max distance (world units) from a console for a valid repair.");

        NoImpostorGameOver = config.Bind("Guard", "NoImpostorGameOver", true, "End the round as a crew win when every impostor is gone, instead of showing the black screen.");
        DoorSpamGuard = config.Bind("Guard", "DoorSpamGuard", true, "Allow only one CloseDoorsOfType per 0.01s burst; block the rest (one-key close-all cheat).");

        BlockLargeGameMessages = config.Bind("Guard", "BlockLargeGameMessages", true, "Drop oversized GameData packets (>1400 B) to stop large-message crash/overload attacks.");
        BlockInvalidGameDataTypes = config.Bind("Guard", "BlockInvalidGameDataTypes", true, "Drop GameData sub-messages with an invalid/malformed type tag.");
        BlockVotingCompleteOverload = config.Bind("Guard", "BlockVotingCompleteOverload", true, "Guard MeetingHud.VotingComplete against an oversized voter-array (memory-allocation) overload.");

        // ===== 第 219 轮：从 OnyxMenu(Nocturne) 1.2.3 移植的三项 =====
        // #1 大厅投票踢人拦截。OnyxMenu 的做法：`VoteBanSystem.AddVote` Prefix，主机可拦下**任何**
        //    投票踢人并惩罚投票者。我们原有的 `AmethystVoteBanProtect` 只标记**非法票**
        //    （死人投票 / 会议外投票），合法的大厅投票踢人照常放行 —— 这条补的就是"整类拦截"。
        VoteKickProtect = config.Bind("Guard", "VoteKickProtect", true,
            "As HOST: block vote-kicks in the lobby (drop the vote, and optionally punish the voter). Off-host this can do nothing - only the host owns the vote count.");
        VoteKickAction = config.Bind("Guard", "VoteKickAction", "Kick",
            "Action on the vote-kicker: Null, Warn, Kick, Ban. Kick/Ban still respect CheaterActionThreshold.");

        // #2 幻影 DATA 对象洪水。OnyxMenu 的做法：`InnerNetClient.HandleGameDataInner` Prefix，
        //    跟踪 tag=1(DATA) 子消息里"未知且不在 DestroyedObjects"的 netId。
        BlockDataFlood = config.Bind("Guard", "BlockDataFlood", true,
            "Drop phantom/oversized DATA object streams: a DATA message whose object id stays unknown for >3s, or more than 40 DATA messages per second (object-flood freeze protection).");

        // #3 外来模组通用识别。我们原本只有一张 6 条的固定 RPC ID 表；
        //    OnyxMenu 除了更大的表，还有"通用启发式"（RPC>=90 且自称 <签名> <版本>）。
        ForeignModDetect = config.Bind("Guard", "ForeignModDetect", true,
            "Identify other mods: known RPC-id table + generic heuristic (RPC id >= 90 announcing a signature/version) + unknown-high-RPC fallback.");

        // ===== 第 241 轮：按 Hydra 2.1.0 补齐的防护项 =====
        ForceDtls = config.Bind("Guard", "ForceDtls", true,
            "Force DTLS so network traffic is encrypted (InnerNetClient.SetEndpoint).");
        BlockServerTeleports = config.Bind("Guard", "BlockServerTeleports", true,
            "Block position updates (SnapTo RPC) that the server/host sends for YOUR player, so nobody can teleport you.");
        BlockUnauthorizedUpdates = config.Bind("Guard", "BlockUnauthorizedUpdates", true,
            "As a client: drop UpdateSystem / CloseDoorsOfType RPCs. Only the host may send these; otherwise a cheater can desync your game state.");
        ReportDeadBodyCrashGuard = config.Bind("Guard", "ReportDeadBodyCrashGuard", true,
            "Block ReportDeadBody while the match is still loading. Sending it there makes the host end the game and the lobby gets deleted.");

        AntiOverloadEnabled = config.Bind("Guard", "AntiOverloadEnabled", true,
            "Anti-overload: master switch for the four protections below.");
        HardenPackedUInt = config.Bind("Guard", "HardenPackedUInt", true,
            "Use a hardened packed-uint deserializer that stops at the end of the buffer instead of reading past it.");

        AntiKickEnabled = config.Bind("Guard", "AntiKickEnabled", true,
            "Anti-kick: master switch for the two protections below.");
        HostOnlyRpcKickGuard = config.Bind("Guard", "HostOnlyRpcKickGuard", true,
            "Block the host-only RPC kick exploit (forged Ventilation UpdateSystem aimed at you).");
        BypassShapeshiftRateLimit = config.Bind("Guard", "BypassShapeshiftRateLimit", true,
            "Bypass the Shapeshift cooldown/rate limit for YOUR OWN role (sets the local cooldown to zero). The host remains authoritative, so this is most useful when you are the host.");

        BypassShapeshiftRateLimit = config.Bind("Guard", "BypassShapeshiftRateLimit", true,
            "Bypass the Shapeshift rate limit: keep your own Shapeshifter cooldown at zero so the shift is always available. Works authoritatively when you are the host.");

        EventConsole = config.Bind("Events", "EventConsole", false, "Show the event console window.");
        EventFilterMask = config.Bind("Events", "EventFilterMask", 127, "Event filter bitmask for the console.");
        SecurityNotify = config.Bind("Events", "SecurityNotify", true, "Notify on security actions (ban, kick, etc.).");

        // ⚠ 性能（第 162 轮）：默认值由 true 改为 **false**。
        //   本模组有大量"每帧"开销（名字着色、色盲文字、冷却文字、ping 显示、雪花…），
        //   一旦解锁帧率并关掉垂直同步，这些开销的执行频率会变成 60 FPS 时的最高 4 倍，
        //   帧时间方差被完全暴露 —— 体感就是明显卡顿。原版 60 FPS + 垂直同步能把这些吸收掉。
        //   想要高帧率的用户仍然可以在菜单里打开（打开后上限可在 60~240 之间调）。
        //   注意：已经生成过配置文件的用户不会被这一改动影响，需要自己在菜单/配置里关掉。
        UnlockFps240 = config.Bind("QoL", "UnlockFps240", false, "Unlock FPS (off = 60 FPS). When on, the cap is configurable in the menu (60-240).");
        FpsLimit = config.Bind("QoL", "FpsLimit", 240, "Frame rate cap used when FPS unlock is on (60-240).");
        AutoReturnLobby = config.Bind("QoL", "AutoReturnLobby", true, "Auto-return to the lobby from the post-match screen.");
        UnlockMatchKickBan = config.Bind("Guard", "UnlockMatchKickBan", true, "As host, allow kick/ban during the match.");
        BlockEarlyMeetings = config.Bind("Guard", "BlockEarlyMeetings", true, "Block meetings and reports during the first 15 seconds after host starts the game (including countdown).");
        NoWinConditions = config.Bind("Host", "NoWinConditions", false, "Disable win-condition checks (the game won't end by itself).");
        BetterChat = config.Bind("Chat", "BetterChat", true, "Better chat: richer input, copy/paste, bubble animation.");
        DarkChatTheme = config.Bind("Chat", "DarkChatTheme", true, "Dark chat theme matching the mod colors.");
        VisualAlwaysShowChat = config.Bind("Chat", "VisualAlwaysShowChat", false, "Always show chat overlay.");

        ChatHistory80 = config.Bind("Other", "ChatHistory80", true, "Keep up to 80 chat messages.");
        ForceShowStart = config.Bind("Other", "ForceShowStart", true, "Show the start button with one player.");
        AbilityUiScale = config.Bind("Other", "AbilityUiScale", 0.85f,
            "Scale of the in-game action buttons (Use / Kill / Report / Ability / Vent / Sabotage), range 0.1 - 1.5. Default 0.85.");
        SkipKillAnimation = config.Bind("Other", "SkipKillAnimation", false, "Skip kill and death animations.");
        SabotageCooldownDisplay = config.Bind("Other", "SabotageCooldownDisplay", true, "Show sabotage cooldowns on the map.");
        OutfitSaver = config.Bind("Other", "OutfitSaver", true, "Enable outfit saving and quick outfit switching.");
        BetterPingDisplay = config.Bind("Other", "BetterPingDisplay", true, "Show the improved ping display.");
        PingShowLatency = config.Bind("Other", "PingShowLatency", true, "Better ping display: show latency.");
        PingShowFps = config.Bind("Other", "PingShowFps", true, "Better ping display: show FPS.");
        PingShowServer = config.Bind("Other", "PingShowServer", true, "Better ping display: show the server/region.");
        PingShowRoom = config.Bind("Other", "PingShowRoom", true, "Better ping display: show the room code and player count.");
        PingShowHost = config.Bind("Other", "PingShowHost", true, "Better ping display: show the host's name.");
        UiTheme = config.Bind("Other", "UiTheme", true, "Render the game's UI buttons in the mod theme colour.");
        BetterCooldownDisplay = config.Bind("Other", "BetterCooldownDisplay", true, "Use improved cooldown colors and values.");
        UnlockAllCosmetics = config.Bind("Other", "UnlockAllCosmetics", false, "Unlock all cosmetics (hats, skins, pets, visors, nameplates).");
        BypassDisconnectPenalty = config.Bind("Guard", "BypassDisconnectPenalty", true, "Prevent host temporary account ban exploit: drop rapid StartGame packets (3+ within 0.05s) and clear ban points.");
        DetectHostTempBan = config.Bind("Guard", "DetectHostTempBan", true, "Detect host temporary account ban exploit and show a warning notification.");
        ExpandLobbySearch = config.Bind("Other", "ExpandLobbySearch", true, "Find Game: show up to 10 lobbies with a scrollable list (applies after reopening the Find Game screen).");
        RoomInfoDisplay = config.Bind("Other", "RoomInfoDisplay", true, "Find Game: show host name, host platform and room code on each lobby row.");
        RoomSortByMaxPlayers = config.Bind("Other", "RoomSortByMaxPlayers", true, "Find Game: sort lobby rows by max players (largest first), then by current players and host name (reference: BetterAmongUs 1.3.4 row sorting).");
        // 模组房间列表：房主每 30s 向 api2.elauk.top 上报一次（90s 无心跳自动过期），
        // 任何装了 AME 的玩家都能在菜单里看到并一键加入。
        RoomListEnabled = config.Bind("RoomList", "Enabled", true, "Mod room list: show the public room list in the menu. Turning this off hides the tab and stops all room-list traffic.");
        // 默认 true：保持「房主房间自动出现在列表里」的原有体验；
        // 不想公开的房主可以在房间列表页签顶部单独关闭上报。
        RoomListReportOwnRoom = config.Bind("RoomList", "ReportOwnRoom", true,
            "Privacy: report your own lobby to the public room list while you are the host. On by default; turn it off to browse the list without publishing your room.");
        RoomListReportSeconds = config.Bind("RoomList", "ReportSeconds", 12, "Heartbeat interval in seconds (8-60). The API expires rooms after 90 seconds without an update.");
        RoomListRefreshSeconds = config.Bind("RoomList", "RefreshSeconds", 8, "How often the room list refreshes while its tab is open (4-60).");
        RoomListApiBase = config.Bind("RoomList", "ApiBase", "https://api2.elauk.top", "Room list API base URL. Change this only if you host your own mirror.");
        RoomListSecret = config.Bind("RoomList", "Secret", "", "HMAC secret for POST /home/api/room (provided privately by the room-list operator). Leave empty to disable reporting.");
        ModClientDetection = config.Bind("Other", "ModClientDetection", true, "Detect compatible mod clients. Other mod clients may also see that you use this feature.");
        ShowRolesWhenDead = config.Bind("Other", "ShowRolesWhenDead", true, "After you die as a Crewmate Ghost, show everyone's role above their name (impostors also show kill count). Not shown for other dead roles (Guardian Angel, Spirit Guide, Impostor Ghost) - they can still act on the living.");
        ShowSelfRole = config.Bind("Other", "ShowSelfRole", true, "Show your own role above your name while alive (crew shows task count, impostor shows kill count).");
        ShowImpostorTeammateRole = config.Bind("Other", "ShowImpostorTeammateRole", true, "Show impostor teammates' role and kill count above their name. Impostors only.");
        RolePanel = config.Bind("Other", "RolePanel", true, "In-game role info panel: a second task-like panel showing roles; click its tab to collapse/expand.");
        ShowMeetingHost = config.Bind("Other", "ShowMeetingHost", true, "In a meeting, show who the host is (host icon + name, top-left).");
        ColorBlindText = config.Bind("Other", "ColorBlindText", true, "Show the color name (color-blind text) next to each player, colored by their color (reference: BetterAmongUs).");
        MainMenuOptimize = config.Bind("Other", "MainMenuOptimize", true, "Main menu optimization: hide the clutter (background texture, panel fill, window shine, tint, friend-request badge) while keeping the Among Us logo.");
        MainMenuButtons = config.Bind("Other", "MainMenuButtons", true, "Main menu links: show GitHub, QQ Group, Report Cheater and Check Update buttons on main menu.");
        HoverThemeColor = config.Bind("Other", "HoverThemeColor", true, "Button hover theme color: tint a button's hover sprites, border and text with the current accent color (reference: FinalSuspect).");
        MainMenuArt = config.Bind("Other", "MainMenuArt", true, "Main menu image: draw a custom picture behind the main menu (built-in art, embedded in the DLL).");
        MainMenuStyle = config.Bind("Other", "MainMenuStyle", true, "Main menu EHR-style restyle: recolor the menu buttons into three purple tiers, hide the left panel plate, make the friends bar transparent and tint the Friends button (reference: EndlessHostRoles 8.0.2 TitleLogoPatch / FinalSuspect AccountManagerPatch).");
        UnlockFriends = config.Bind("Guest", "UnlockFriends", true, "Guest unlock: allow the friends list (removes EOSManager.IsFriendsListAllowed gate; reference AUnlocker / Onyx AccountPatches).");
        UnlockFreeChat = config.Bind("Guest", "UnlockFreeChat", true, "Guest unlock: allow free chat instead of quick chat only (EOSManager.IsFreechatAllowed).");
        UnlockOnline = config.Bind("Guest", "UnlockOnline", true, "Guest unlock: allow online play and joining public lobbies; clears the minor/waiting gate (EOSManager.IsMinorOrWaiting / IsAllowedOnline, AccountManager.CanPlayOnline).");
        UnlockGuestName = config.Bind("Guest", "UnlockGuestName", true, "Guest unlock: allow setting a custom name as a guest (FullAccount.CanSetCustomName).");
        ForceAdult = config.Bind("Guest", "ForceAdult", true, "Force the account to be treated as an ADULT: keep EOSManager.isKWSMinor = false so minor/waiting limits never kick in (fixes 'free chat disabled / login looks lost' on minor-flagged accounts). Enforced 4x/second. Reference: OnyxMenu RemoveMinorLimits.");
        SnowEffect = config.Bind("Other", "SnowEffect", true, "Snow effect: make snow fall around the camera in every scene (main menu, lobby and in-game).");
        RoleTag = config.Bind("Other", "RoleTag", true, "Meeting role tag: an EditTag button next to each player in the meeting. Click it to pick a role from the game's shapeshifter menu and mark that player; the mark shows in the meeting and above their name in game (local only; impostors red, crewmates blue).");
        RecapInfo = config.Bind("Other", "RecapInfo", true, "Recap info window: view roles, kills/tasks, death causes and results in the lobby or results screen. Toggle with the top-left button or recap hotkey.");
        NameColors = config.Bind("Other", "NameColors", true, "Render player names (self and others) in their color; if you are an impostor, teammates are shown in red.");
        RoleColors = config.Bind("Other", "RoleColors", true, "Role colors: render each VANILLA role in its own color (reference: EndlessHostRoles). E.g. Noisemaker orange-red, Engineer orange, Scientist light green, instead of the flat impostor-red/crewmate-blue.");

        // ===== 第 207 轮：从 EHR 提取的功能 =====
        FixLobbyMusic = config.Bind("Other", "FixLobbyMusic", true,
            "Restore the lobby ambient/music volume after meetings and games, so it no longer stays quiet after entering a game.");
        MuteLobbyMusic = config.Bind("Other", "MuteLobbyMusic", false,
            "Lobby only: mute the lobby background music.");
        WinnerRoleReveal = config.Bind("Other", "WinnerRoleReveal", true,
            "On the game-over screen, show the winning players' role under their name.");
        LobbyPlayerInfo = config.Bind("Other", "LobbyPlayerInfo", true,
            "In the lobby, show each player's platform / level / friend code above their name.");
        KickSlowJoiners = config.Bind("Guard", "KickSlowJoiners", true,
            "As host: kick players whose player data is still not ready a few seconds after joining (slow or incomplete join).");
        SlowJoinSeconds = config.Bind("Guard", "SlowJoinSeconds", 5f,
            "Seconds to wait for a joining player's data before kicking them (KickSlowJoiners).");
        SlowJoinSeconds.Value = Mathf.Clamp(SlowJoinSeconds.Value, 1f, 30f);

        // ===== 性能（第 173 轮新分区）=====
        // 参考 EndlessHostRoles 的 SystemSettings：DontUpdateDeadPlayers(默认true) /
        // LowLoadMode(默认true) / DeepLowLoad(默认false)。
        // 全部默认开启 —— 用户明确要求「优先考虑性能、修复卡顿」。
        PerfDontUpdateDead = config.Bind("Performance", "DontUpdateDeadPlayers", true,
            "Performance: skip most FixedUpdate frames for DEAD players (reference: EndlessHostRoles DontUpdateDeadPlayers). Biggest single win in full games. Slight cost: dead players' overlays refresh a bit slower.");
        PerfDeadSkipFrames = config.Bind("Performance", "DeadSkipFrames", 60,
            "Performance: how many FixedUpdate frames to skip for a dead player (EndlessHostRoles uses 60; 150 = deeper).");
        PerfDeadSkipFrames.Value = Mathf.Clamp(PerfDeadSkipFrames.Value, 2, 600);
        PerfLowLoad = config.Bind("Performance", "LowLoadMode", true,
            "Performance: round-robin player updates under load -- only one player is fully updated per frame, the rest are skipped (reference: EndlessHostRoles LowLoadMode).");
        PerfHideConsole = config.Bind("Performance", "HideConsole", true,
            "Performance: hide the black console window at startup.");

        // 开发者/调试探针：默认关闭，防止每秒打日志/日志膨胀
        PerfProbe = config.Bind("Performance", "PerfProbe", false,
            "Performance probe: measure per-subsystem frame cost and write a summary to the BepInEx log every few seconds. Use it when the game stutters.");
        PerfProbe.Value = false;

        // 职业分配算法（开发者）：替换分配身份时使用的随机数算法。
        // 参考 EHR 的 RoleAssigningAlgorithm（Default / NetRandom / Xorshift / MersenneTwister）。
        RoleAlgorithm = config.Bind("Developer", "RoleAlgorithm", 0,
            "Role assignment algorithm: 0=Default (game's own), 1=System random, 2=Xorshift, 3=Mersenne Twister. Host only; shuffles the player list before the game assigns roles.");

        // 临时定位开关（逐个）：true = 停用对应模块。
        // （第 218 轮删掉了 `DisableFriendsListOpen` —— 它的默认值 true 把第 199 轮的
        //   好友面板修复一直关着，详见上面字段区的注释与 `AmethystFriendsListOpen.cs`。）
        DiagDisableFriendsBar = config.Bind("Diag", "DisableFriendsBar", false,
            "TEMP diagnostic: disable the FriendsBar module (also AmethystAccountManagerAwakePatch).");
        DiagDisableMainMenuStyle = config.Bind("Diag", "DisableMainMenuStyle", false,
            "TEMP diagnostic: disable the MainMenuStyle module.");

        // 第 176 轮：会议中显示任务面板（参考 AUnlocker 1.3.1 ShowTaskPanelInMeetings）。
        // ⚠ 按用户要求：**界面优化类功能一律默认开启**，玩家需要时再手动关。
        MeetingTaskPanel = config.Bind("Other", "MeetingTaskPanel", true,
            "Show the task panel (your task list) during meetings (reference: AUnlocker). The panel is raised above the meeting UI.");
        CooldownDecimals = config.Bind("Other", "CooldownDecimals", true, "Show one decimal place on ability cooldown/duration when it is within 10 seconds.");
        ColorSnipe = config.Bind("Other", "ColorSnipe", false, "Lobby only: automatically claim the desired color when it becomes free.");
        ColorSnipeId = config.Bind("Other", "ColorSnipeId", 0, "Desired color id for color snipe (0-17).");
        AntiCheat = config.Bind("Guard", "AntiCheat", true, "Master switch: enable all anti-cheat detections at once (excludes DropRPC, RPC flood, vent-kick).");

        Language = config.Bind("General", "Language", "auto", "Language: auto, en, ru, zh. 'auto' follows the game language on first launch, then the value is saved and used afterwards.");
        // 主题选择已移除（固定 #A06EFF），不再绑定 ThemeIndex；老配置文件里的该项会被忽略。

        // 所有 RpcCallSwitches 都已绑定完毕 → 建 callId 直查表（运行期零字符串）。
        BuildRpcCallTable();
    }
}
