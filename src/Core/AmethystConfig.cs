using System;
using BepInEx.Configuration;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal static partial class AmethystConfig
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

    private static readonly System.Collections.Generic.List<ConfigEntry<bool>> RpcCallTable =
        new System.Collections.Generic.List<ConfigEntry<bool>>(256);

    private static int[] RpcCallIndex;   

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
                if (idx[id] >= 0) continue;   

                idx[id] = RpcCallTable.Count;
                RpcCallTable.Add(RpcCallSwitches.TryGetValue(name, out ConfigEntry<bool> ce) ? ce : null);
            }

            RpcCallIndex = idx;
        }
        catch
        {
            RpcCallIndex = null;   
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

    internal static ConfigEntry<bool> FastKillGuard;
    internal static ConfigEntry<float> FastKillWindow;
    internal static ConfigEntry<bool> PlatformSpoofGuard;
    internal static ConfigEntry<bool> InvalidFcGuard;
    internal static ConfigEntry<bool> BotGuard;
    internal static ConfigEntry<int> CheaterActionThreshold;

    internal static ConfigEntry<bool> VoteKickProtect;
    internal static ConfigEntry<string> VoteKickAction;
    internal static ConfigEntry<bool> BlockDataFlood;
    internal static ConfigEntry<bool> ForeignModDetect;

    internal static ConfigEntry<bool> ForceDtls;                 
    internal static ConfigEntry<bool> BlockServerTeleports;      
    internal static ConfigEntry<bool> BlockUnauthorizedUpdates;  
    internal static ConfigEntry<bool> ReportDeadBodyCrashGuard;  
    internal static ConfigEntry<bool> AntiOverloadEnabled;       
    internal static ConfigEntry<bool> HardenPackedUInt;          
    internal static ConfigEntry<bool> AntiKickEnabled;           
    internal static ConfigEntry<bool> HostOnlyRpcKickGuard;      

    internal static ConfigEntry<bool> VentTpProtect;
    internal static ConfigEntry<bool> ZiplineProtect;

    internal static ConfigEntry<bool> CheckSabotageRepair;
    internal static ConfigEntry<float> SabotageRepairDistance;

    internal static ConfigEntry<bool> NoImpostorGameOver;
    
    internal static ConfigEntry<bool> DoorSpamGuard;

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

    internal static ConfigEntry<bool> BubbleAnim;
    
    internal static ConfigEntry<float> BubbleAnimSpeed;
    
    internal static ConfigEntry<bool> BubbleCascade;
    
    internal static ConfigEntry<bool> BubbleReflow;

    internal static ConfigEntry<bool> ChatHistory80;
    internal static ConfigEntry<bool> ForceShowStart;
    
    internal static ConfigEntry<float> AbilityUiScale;
    internal static ConfigEntry<bool> TopRightButtonGroup;
    
    internal static ConfigEntry<bool> ShowKeyGuide;
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

    internal static ConfigEntry<bool> RoomListEnabled;
    
    internal static ConfigEntry<bool> RoomListReportOwnRoom;
    internal static ConfigEntry<int> RoomListReportSeconds;
    internal static ConfigEntry<int> RoomListRefreshSeconds;
    internal static ConfigEntry<string> RoomListApiBase;
    
    internal static ConfigEntry<string> RoomListSecret;
    internal static ConfigEntry<bool> ModClientDetection;
    internal static ConfigEntry<bool> ShowRolesWhenDead;
    internal static ConfigEntry<bool> ShowSelfRole;
    internal static ConfigEntry<bool> ShowImpostorTeammateRole;

    internal static ConfigEntry<bool> RolePanel;
    internal static ConfigEntry<bool> ShowMeetingHost;
    internal static ConfigEntry<bool> ColorBlindText;
    
    internal static ConfigEntry<bool> MainMenuOptimize;
    internal static ConfigEntry<bool> MainMenuButtons;
    internal static ConfigEntry<bool> HoverThemeColor;   
    internal static ConfigEntry<bool> MainMenuArt;
    internal static ConfigEntry<bool> MainMenuStyle;   
    internal static ConfigEntry<bool> UnlockFriends;    
    internal static ConfigEntry<bool> UnlockFreeChat;  
    internal static ConfigEntry<bool> UnlockOnline;    
    internal static ConfigEntry<bool> UnlockGuestName; 
    
    internal static ConfigEntry<bool> ForceAdult;
    internal static ConfigEntry<bool> SnowEffect;
    internal static ConfigEntry<bool> RoleTag;
    internal static ConfigEntry<bool> RecapInfo;
    internal static ConfigEntry<bool> NameColors;
    
    internal static ConfigEntry<bool> RoleColors;

    internal static ConfigEntry<bool> FixLobbyMusic;
    
    internal static ConfigEntry<bool> MuteLobbyMusic;
    
    internal static ConfigEntry<bool> WinnerRoleReveal;
    
    internal static ConfigEntry<bool> LobbyPlayerInfo;
    
    internal static ConfigEntry<bool> KickSlowJoiners;
    internal static ConfigEntry<float> SlowJoinSeconds;

    internal static ConfigEntry<bool> PerfDontUpdateDead;
    internal static ConfigEntry<int> PerfDeadSkipFrames;
    internal static ConfigEntry<bool> PerfLowLoad;
    internal static ConfigEntry<bool> PerfHideConsole;
    internal static ConfigEntry<bool> PerfProbe;

    internal static ConfigEntry<int> RoleAlgorithm;

    internal static ConfigEntry<bool> DiagDisableFriendsBar;        
    internal static ConfigEntry<bool> DiagDisableMainMenuStyle;     

    internal static ConfigEntry<bool> MeetingTaskPanel;
    internal static ConfigEntry<bool> CooldownDecimals;
    internal static ConfigEntry<bool> ColorSnipe;
    internal static ConfigEntry<int> ColorSnipeId;
    internal static ConfigEntry<bool> AntiCheat;

    internal static ConfigEntry<string> Language;
    internal static ConfigEntry<KeyCode> MusicKey;
    
    internal static ConfigEntry<KeyCode> MusicPrevKey;
    internal static ConfigEntry<KeyCode> MusicNextKey;
    internal static ConfigEntry<KeyCode> MusicPlayPauseKey;
    
    internal static ConfigEntry<string> MusicRepeat;
    internal static ConfigEntry<bool> MusicMuteGameMusic;
    internal static ConfigEntry<bool> ScaleLobbyPane;
    internal static ConfigEntry<float> MusicWinX;
    internal static ConfigEntry<float> MusicWinY;
    internal static ConfigEntry<float> MusicVolume;

}
