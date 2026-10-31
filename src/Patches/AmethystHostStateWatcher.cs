using UnityEngine;
using InnerNet;
using Amethyst;

namespace Amethyst.Patches;

internal sealed class AmethystHostStateWatcher : MonoBehaviour
{
    private bool _lastHost;
    private bool _initialized;

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_Other);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_Other); }
    }

    public void UpdateInner()
    {
            bool host = false;
            try
            {
                host = AmongUsClient.Instance != null && ((InnerNetClient)AmongUsClient.Instance).AmHost;
            }
            catch { }

            if (!_initialized)
            {
                _initialized = true;
                _lastHost = host;
            }

            if (host == _lastHost) return;
            _lastHost = host;

            AmethystRpcGuard.SyncDropRpcForHost(host);
            if (!host && AmethystConfig.DropRPC != null)
                AmethystConfig.DropRPC.Value = false;
    
    }
}
