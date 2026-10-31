using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystNotifierLayer
{
    private static bool _done;

    internal static void Apply()
    {
        if (_done) return;
        try
        {
            HudManager hud = HudManager.Instance;
            if (hud == null || hud.Notifier == null) return;
            var ap = hud.Notifier.GetComponent<AspectPosition>();
            if (ap != null) ap.DistanceFromEdge += Vector3.back * 900f;
            _done = true;
        }
        catch { }
    }
}
