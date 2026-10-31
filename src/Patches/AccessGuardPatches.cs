using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(AmongUsClient), "OnPlayerJoined")]
internal static class AmethystAccessJoinPatch
{
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData client)
        => AmethystAccess.Enforce((InnerNetClient)__instance, client);
}

[HarmonyPatch(typeof(InnerNetClient), "KickPlayer")]
internal static class AmethystKickSelfGuardPatch
{
    public static bool Prefix(InnerNetClient __instance, int clientId)
    {
        if (__instance == null || !__instance.AmHost) return true;
        return clientId != __instance.ClientId && clientId != __instance.HostId;
    }
}

[HarmonyPatch(typeof(VoteBanSystem), "AddVote")]
internal static class AmethystVoteKickProtectPatch
{
    public static bool Prefix([HarmonyArgument(0)] int srcClient, [HarmonyArgument(1)] int clientId)
    {
        try
        {
            if (AmethystConfig.AntiKickEnabled == null || !AmethystConfig.AntiKickEnabled.Value) return true;
            if (AmethystConfig.VoteKickProtect == null || !AmethystConfig.VoteKickProtect.Value) return true;
            if (AmongUsClient.Instance == null) return true;

            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null || !net.AmHost) return true;

            string action = AmethystConfig.VoteKickAction != null ? AmethystConfig.VoteKickAction.Value : "Null";
            if (!string.Equals(action, "Null", System.StringComparison.OrdinalIgnoreCase))
            {
                
                AmethystAccess.Act(
                    net, srcClient, action,
                    AmethystAccess.ClientName(net, srcClient),
                    AmethystText.T("VoteKickAttempt", AmethystAccess.ClientName(net, clientId)));
            }
            else
            {
                AmethystSecurityNotify.Fire("VoteKickBlocked", AmethystNotifyKind.Warning,
                    AmethystAccess.ClientName(net, srcClient), AmethystAccess.ClientName(net, clientId));
            }

            return false;   
        }
        catch { return true; }
    }
}

public sealed class AmethystAccessGuard : MonoBehaviour
{
    private float _next;

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_AccessGuard);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_AccessGuard); }
    }

    public void UpdateInner()
    {
            if (Time.realtimeSinceStartup < _next) return;
            _next = Time.realtimeSinceStartup + 0.6f;

            if (AmongUsClient.Instance == null || LobbyBehaviour.Instance == null) return;
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (!net.AmHost || net.allClients == null) return;
            bool fg = AmethystConfig.KickFortegreen.Value;
            bool banEnabled = (AmethystConfig.AccessBanEnabled != null && AmethystConfig.AccessBanEnabled.Value)
                || (AmethystConfig.CloudBanEnabled != null && AmethystConfig.CloudBanEnabled.Value);
            if (!fg && !banEnabled
                && !AmethystConfig.MinLevelEnabled.Value && !AmethystConfig.MaxLevelEnabled.Value) return;

            try
            {
                var e = net.allClients.GetEnumerator();
                while (e.MoveNext())
                {
                    ClientData c = e.Current;
                    if (c == null) continue;
                    if (fg && c.Id != net.ClientId && c.Id != net.HostId && IsFortegreen(c))
                    {
                        AmethystDisconnect.RecordAction(c.Id, false, AmethystText.T("Fortegreen"), AmethystAccess.SafeName(c));
                        AmethystAccess.Kick(net, c.Id, false);
                        AmethystToast.Push(AmethystText.T("Guard"), AmethystNotifyText.Body(AmethystColors.Name(c) ?? AmethystAccess.SafeName(c), AmethystText.T("Fortegreen")), 5.0f, AmethystNotifyKind.Warning);
                        continue;
                    }
                    AmethystAccess.Enforce(net, c);
                }
            }
            catch { }
    
    }

    private static bool IsFortegreen(ClientData c)
    {
        try { return c.Character != null && c.Character.CurrentOutfit != null && c.Character.CurrentOutfit.ColorId == 18; }
        catch { return false; }
    }
}
