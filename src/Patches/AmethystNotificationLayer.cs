using UnityEngine;

namespace Amethyst.Patches;

// 提高原版左下角通知图层（参考 FinalSuspect：把 Notifier 的 AspectPosition 沿 Z 轴拉近）。
// 不 patch HudManager.Start —— 该补丁在部分构建下会让原生 Start 抛 NRE，
// 改为由 AmethystHud.Update 每帧探测 HudManager.Instance，首次可用时调整一次。
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
