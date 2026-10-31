using System;
using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystJoinTimeout
{
    private sealed class Pending
    {
        internal int ClientId;
        internal float At;
    }

    private static readonly List<Pending> _pending = new List<Pending>();

    internal static bool Enabled => AmethystConfig.KickSlowJoiners != null && AmethystConfig.KickSlowJoiners.Value;

    internal static void Track(int clientId)
    {
        if (!Enabled) return;
        try
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].ClientId == clientId)
                {
                    _pending[i].At = Time.unscaledTime;
                    return;
                }
            }

            _pending.Add(new Pending { ClientId = clientId, At = Time.unscaledTime });
        }
        catch { }
    }

    internal static void Tick()
    {
        if (_pending.Count == 0) return;
        if (!Enabled) { _pending.Clear(); return; }

        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            if (net == null || !net.AmHost) { _pending.Clear(); return; }

            float wait = AmethystConfig.SlowJoinSeconds != null ? AmethystConfig.SlowJoinSeconds.Value : 5f;
            float now = Time.unscaledTime;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Pending p = _pending[i];
                ClientData client = FindClient(net, p.ClientId);

                if (client == null) { _pending.RemoveAt(i); continue; }

                bool ready = client.Character != null && DataReady(client.Character.Data);
                if (ready) { _pending.RemoveAt(i); continue; }

                if (now - p.At >= wait)
                {
                    _pending.RemoveAt(i);
                    Kick(net, client);
                }
            }
        }
        catch { }
    }

    private static bool DataReady(NetworkedPlayerInfo data)
    {
        try
        {
            if (data == null || data.IsIncomplete) return false;
            if (data.DefaultOutfit == null) return true;

            int c = data.DefaultOutfit.ColorId;
            return c >= 0 && (Palette.PlayerColors == null || c < Palette.PlayerColors.Length);
        }
        catch { return false; }
    }

    private static ClientData FindClient(AmongUsClient net, int id)
    {
        try
        {
            if (net.allClients == null) return null;
            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c != null && c.Id == id) return c;
            }
        }
        catch { }
        return null;
    }

    private static void Kick(AmongUsClient net, ClientData client)
    {
        try
        {
            if (client == null) return;
            string name = client.PlayerName;
            net.KickPlayer(client.Id, false);
            AmethystPlugin.Logger?.LogInfo($"[JoinGuard] Kicked {name} (id={client.Id}): player data was not ready in time.");
        }
        catch { }
    }
}

[HarmonyPatch(typeof(AmongUsClient), "OnPlayerJoined")]
internal static class AmethystJoinTimeoutTrackPatch
{
    private static void Postfix([HarmonyArgument(0)] ClientData client)
    {
        try { if (client != null) AmethystJoinTimeout.Track(client.Id); } catch { }
    }
}

internal sealed class AmethystJoinWatchdog : MonoBehaviour
{
    public AmethystJoinWatchdog(IntPtr ptr) : base(ptr) { }
    private void Update() => AmethystJoinTimeout.Tick();
}
