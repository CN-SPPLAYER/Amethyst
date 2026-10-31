using System;
using BepInEx.Configuration;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static partial class AmethystConfig
{
    
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
        
        RpcFloodThreshold = config.Bind("Guard", "RpcFloodThreshold", 25, "RPCs per second from one client before a suspicion toast.");
        
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
        
        SabotageRepairDistance = config.Bind("Guard", "SabotageRepairDistance", 3.5f, "Max distance (world units) from a console for a valid repair.");

        NoImpostorGameOver = config.Bind("Guard", "NoImpostorGameOver", true, "End the round as a crew win when every impostor is gone, instead of showing the black screen.");
        DoorSpamGuard = config.Bind("Guard", "DoorSpamGuard", true, "Allow only one CloseDoorsOfType per 0.01s burst; block the rest (one-key close-all cheat).");

        BlockLargeGameMessages = config.Bind("Guard", "BlockLargeGameMessages", true, "Drop oversized GameData packets (>1400 B) to stop large-message crash/overload attacks.");
        BlockInvalidGameDataTypes = config.Bind("Guard", "BlockInvalidGameDataTypes", true, "Drop GameData sub-messages with an invalid/malformed type tag.");
        BlockVotingCompleteOverload = config.Bind("Guard", "BlockVotingCompleteOverload", true, "Guard MeetingHud.VotingComplete against an oversized voter-array (memory-allocation) overload.");

        VoteKickProtect = config.Bind("Guard", "VoteKickProtect", true,
            "As HOST: block vote-kicks in the lobby (drop the vote, and optionally punish the voter). Off-host this can do nothing - only the host owns the vote count.");
        VoteKickAction = config.Bind("Guard", "VoteKickAction", "Kick",
            "Action on the vote-kicker: Null, Warn, Kick, Ban. Kick/Ban still respect CheaterActionThreshold.");

        BlockDataFlood = config.Bind("Guard", "BlockDataFlood", true,
            "Drop phantom/oversized DATA object streams: a DATA message whose object id stays unknown for >3s, or more than 40 DATA messages per second (object-flood freeze protection).");

        ForeignModDetect = config.Bind("Guard", "ForeignModDetect", true,
            "Identify other mods: known RPC-id table + generic heuristic (RPC id >= 90 announcing a signature/version) + unknown-high-RPC fallback.");

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
        EventConsole = config.Bind("Events", "EventConsole", false, "Show the event console window.");
        EventFilterMask = config.Bind("Events", "EventFilterMask", 127, "Event filter bitmask for the console.");
        SecurityNotify = config.Bind("Events", "SecurityNotify", true, "Notify on security actions (ban, kick, etc.).");

        UnlockFps240 = config.Bind("QoL", "UnlockFps240", false, "Unlock FPS (off = 60 FPS). When on, the cap is configurable in the menu (60-240).");
        FpsLimit = config.Bind("QoL", "FpsLimit", 240, "Frame rate cap used when FPS unlock is on (60-240).");
        AutoReturnLobby = config.Bind("QoL", "AutoReturnLobby", true, "Auto-return to the lobby from the post-match screen.");
        UnlockMatchKickBan = config.Bind("Guard", "UnlockMatchKickBan", true, "As host, allow kick/ban during the match.");
        BlockEarlyMeetings = config.Bind("Guard", "BlockEarlyMeetings", true, "Block meetings and reports during the first 15 seconds after host starts the game (including countdown).");
        NoWinConditions = config.Bind("Host", "NoWinConditions", false, "Disable win-condition checks (the game won't end by itself).");
        BetterChat = config.Bind("Chat", "BetterChat", true, "Better chat: richer input, copy/paste, bubble animation.");
        DarkChatTheme = config.Bind("Chat", "DarkChatTheme", true, "Dark chat theme matching the mod colors.");
        VisualAlwaysShowChat = config.Bind("Chat", "VisualAlwaysShowChat", false, "Always show chat overlay.");
        BubbleAnim = config.Bind("Chat", "BubbleAnim", true, "Chat bubble animation: slide/rise/pop in, avatar bounce, staggered name and text, background flash and fade.");
        BubbleAnimSpeed = config.Bind("Chat", "BubbleAnimSpeed", 1.0f, "Chat bubble animation speed, range 0.5 - 2.0. Default 1.0.");
        BubbleCascade = config.Bind("Chat", "BubbleCascade", true, "Replay the newest bubbles, one after another, when the chat window opens.");
        BubbleReflow = config.Bind("Chat", "BubbleReflow", true, "Glide bubbles into place when the chat list re-aligns instead of snapping.");

        ChatHistory80 = config.Bind("Other", "ChatHistory80", true, "Keep up to 80 chat messages.");
        ForceShowStart = config.Bind("Other", "ForceShowStart", true, "Show the start button with one player.");
        AbilityUiScale = config.Bind("Other", "AbilityUiScale", 0.85f,
            "Scale of the in-game action buttons (Use / Kill / Report / Ability / Vent / Sabotage), range 0.1 - 1.5. Default 0.85.");
        
        ShowKeyGuide = config.Bind("Other", "ShowKeyGuide", true,
            "Show the bound key mapping on each action button (Kill / Use / Pet / Sabotage / Vent / Report / Map). Keys are read from the game's own key bindings.");
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
        
        RoomListEnabled = config.Bind("RoomList", "Enabled", true, "Mod room list: show the public room list in the menu. Turning this off hides the tab and stops all room-list traffic.");
        
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
        TopRightButtonGroup = config.Bind("Other", "TopRightButtonGroup", true, "Merge the top-right HUD buttons into one panel.");
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

        PerfDontUpdateDead = config.Bind("Performance", "DontUpdateDeadPlayers", true,
            "Performance: skip most FixedUpdate frames for DEAD players (reference: EndlessHostRoles DontUpdateDeadPlayers). Biggest single win in full games. Slight cost: dead players' overlays refresh a bit slower.");
        PerfDeadSkipFrames = config.Bind("Performance", "DeadSkipFrames", 60,
            "Performance: how many FixedUpdate frames to skip for a dead player (EndlessHostRoles uses 60; 150 = deeper).");
        PerfDeadSkipFrames.Value = Mathf.Clamp(PerfDeadSkipFrames.Value, 2, 600);
        PerfLowLoad = config.Bind("Performance", "LowLoadMode", true,
            "Performance: round-robin player updates under load -- only one player is fully updated per frame, the rest are skipped (reference: EndlessHostRoles LowLoadMode).");
        PerfHideConsole = config.Bind("Performance", "HideConsole", true,
            "Performance: hide the black console window at startup.");

        PerfProbe = config.Bind("Performance", "PerfProbe", false,
            "Performance probe: measure per-subsystem frame cost and write a summary to the BepInEx log every few seconds. Use it when the game stutters.");
        PerfProbe.Value = false;

        RoleAlgorithm = config.Bind("Developer", "RoleAlgorithm", 0,
            "Role assignment algorithm: 0=Default (game's own), 1=System random, 2=Xorshift, 3=Mersenne Twister. Host only; shuffles the player list before the game assigns roles.");

        DiagDisableFriendsBar = config.Bind("Diag", "DisableFriendsBar", false,
            "TEMP diagnostic: disable the FriendsBar module (also AmethystAccountManagerAwakePatch).");
        DiagDisableMainMenuStyle = config.Bind("Diag", "DisableMainMenuStyle", false,
            "TEMP diagnostic: disable the MainMenuStyle module.");

        MeetingTaskPanel = config.Bind("Other", "MeetingTaskPanel", true,
            "Show the task panel (your task list) during meetings (reference: AUnlocker). The panel is raised above the meeting UI.");
        CooldownDecimals = config.Bind("Other", "CooldownDecimals", true, "Show one decimal place on ability cooldown/duration when it is within 10 seconds.");
        ColorSnipe = config.Bind("Other", "ColorSnipe", false, "Lobby only: automatically claim the desired color when it becomes free.");
        ColorSnipeId = config.Bind("Other", "ColorSnipeId", 0, "Desired color id for color snipe (0-17).");
        AntiCheat = config.Bind("Guard", "AntiCheat", true, "Master switch: enable all anti-cheat detections at once (excludes DropRPC, RPC flood, vent-kick).");

        Language = config.Bind("General", "Language", "auto", "Language: auto, en, ru, zh. 'auto' follows the game language on first launch, then the value is saved and used afterwards.");
        
        BuildRpcCallTable();
    }
}
