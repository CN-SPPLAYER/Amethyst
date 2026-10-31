using System;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using InnerNet;

namespace Amethyst.Patches;

internal static class AmethystGameDataScanner
{
    
    internal const int MaxGameDataLength = 1400;

    private const int T_LARGE = 1;      
    private const int T_BAD_TAG = 2;    
    private const int T_FLOOD = 4;      
    private const int T_MODCLIENT = 8;  
    private const int T_OUTFIT = 16;    

    private const int T_NEEDS_WALK = T_BAD_TAG | T_FLOOD | T_MODCLIENT | T_OUTFIT;

    private static int _taskFrame = -1;
    private static int _tasks;

    private static int Tasks(InnerNetClient net)
    {
        int frame = UnityEngine.Time.frameCount;
        if (frame == _taskFrame) return _tasks;
        _taskFrame = frame;

        int t = 0;
        try
        {
            
            bool overloadOn = AmethystConfig.AntiOverloadEnabled == null || AmethystConfig.AntiOverloadEnabled.Value;

            if (overloadOn && AmethystConfig.BlockLargeGameMessages != null && AmethystConfig.BlockLargeGameMessages.Value)
                t |= T_LARGE;
            if (overloadOn && AmethystConfig.BlockInvalidGameDataTypes != null && AmethystConfig.BlockInvalidGameDataTypes.Value)
                t |= T_BAD_TAG;
        }
        catch { }

        try
        {
            if (AmethystConfig.RpcFloodDetect != null && AmethystConfig.RpcFloodDetect.Value
                && net != null && net.NetworkMode != NetworkModes.FreePlay)
                t |= T_FLOOD;
        }
        catch { }

        try
        {
            if (AmethystConfig.RpcGameDataCheck != null && AmethystConfig.RpcGameDataCheck.Value)
            {
                t |= T_MODCLIENT;
                if (AmethystRpcGuard.On() && AmethystRpcGuard.InMatch())
                    t |= T_OUTFIT;
            }
        }
        catch { }

        _tasks = t;
        return t;
    }

    internal static void Reset()
    {
        _taskFrame = -1;
        _tasks = 0;
    }

    [HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
    internal static class AmethystGameDataScannerResetPatch
    {
        private static void Postfix() => AmethystGameDataScanner.Reset();
    }

    [HarmonyPatch(typeof(InnerNetClient), "HandleGameData")]
    internal static class AmethystGameDataScannerPatch
    {
        private static bool Prefix(InnerNetClient __instance, MessageReader parentReader)
        {
            if (__instance == null || parentReader == null) return true;

            int probe = AmethystPerfProbe.Begin(Section.S_Other);
            try { return Scan(__instance, parentReader); }
            finally { AmethystPerfProbe.End(probe, Section.S_Other); }
        }

        private static bool Scan(InnerNetClient __instance, MessageReader parentReader)
        {
            int tasks = AmethystGameDataScanner.Tasks(__instance);
            if (tasks == 0) return true;    

            if ((tasks & T_NEEDS_WALK) == 0)
            {
                if (parentReader.Length > AmethystGameDataScanner.MaxGameDataLength)
                {
                    Drop(parentReader);
                    return false;
                }
                return true;
            }

            if ((tasks & T_LARGE) != 0 && parentReader.Length > AmethystGameDataScanner.MaxGameDataLength)
            {
                Drop(parentReader);
                return false;
            }

            int badTag = (tasks & T_BAD_TAG) != 0 ? 1 : 0;
            int flood = (tasks & T_FLOOD) != 0 ? 1 : 0;
            int modClient = (tasks & T_MODCLIENT) != 0 ? 1 : 0;
            int outfit = (tasks & T_OUTFIT) != 0 ? 1 : 0;

            int originalPosition = parentReader.Position;
            MessageReader copy = null;
            try
            {
                copy = MessageReader.Get(parentReader);
                while (copy.Position < copy.Length)
                {
                    MessageReader part = copy.ReadMessage();
                    if (part == null) break;
                    try
                    {
                        byte tag = (byte)part.Tag;

                        if (tag != 2)   
                        {
                            if (badTag != 0 && AmethystGameDataScannerTag.IsBadTag(tag))
                            {
                                Drop(parentReader);
                                return false;
                            }
                            continue;
                        }

                        uint netId = part.ReadPackedUInt32();
                        byte callId = part.ReadByte();

                        bool isCancelPet = callId == (byte)RpcCalls.CancelPet;

                        bool isOurMarker = false;
                        if (isCancelPet && (modClient != 0 || flood != 0)
                            && part.Length - part.Position >= 8)
                            isOurMarker = IsModMarker(part);

                        bool isOutfitCall = outfit != 0 && AmethystRpcGuard.IsOutfitCall(callId);
                        bool needObjHere = flood != 0
                            || (modClient != 0 && isCancelPet)
                            || isOutfitCall;

                        InnerNetObject obj = null;
                        if (needObjHere && __instance.allObjects != null)
                            __instance.allObjects.AllObjectsFast.TryGetValue(netId, out obj);

                        if (modClient != 0 && isCancelPet && obj != null)
                        {
                            PlayerControl sender = obj is PlayerPhysics physics ? physics.myPlayer : obj.TryCast<PlayerControl>();
                            if (sender != null && AmethystModClientDetection.HandlePacket(sender, part))
                                continue;   
                        }

                        if (flood != 0 && obj != null && !isOurMarker)
                            AmethystRpcFlood.Observe(__instance, obj.OwnerId);

                        if (isOutfitCall)
                        {
                            PlayerControl target = obj != null ? obj.TryCast<PlayerControl>() : null;
                            if (target != null && target != PlayerControl.LocalPlayer
                                && !AmethystRpcGuard.InJoinGrace())
                            {
                                AmethystRpcGuard.Flag(target, AmethystText.T("OutfitChangeInMatch"));
                                return true;   
                            }
                        }
                    }
                    catch { }
                    finally { try { part.Recycle(); } catch { } }
                }
            }
            catch { }
            finally
            {
                try { copy?.Recycle(); } catch { }
                try { parentReader.Position = originalPosition; } catch { }
            }

            return true;
        }

        private static bool IsModMarker(MessageReader part)
        {
            int pos = part.Position;
            try { return part.ReadString() == AmethystModClientDetection.Marker; }
            catch { return false; }
            finally { try { part.Position = pos; } catch { } }
        }

        private static void Drop(MessageReader reader)
        {
            try { reader.Recycle(); } catch { }
        }
    }
}

internal static class AmethystGameDataScannerTag
{
    internal static bool IsBadTag(byte tag)
    {
        try
        {
            GameDataTypes type = (GameDataTypes)tag;
            return type == GameDataTypes.Invalid || type == (GameDataTypes)3 || type > GameDataTypes.ReadyFlag;
        }
        catch { return false; }
    }
}
