using System;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Amethyst.Patches;

// ===== 蘑菇混合（The Fungle 的蘑菇混合破坏 MushroomMixup）期间隐藏名字 =====
//
// 用户反馈（自由模式实测）：使用蘑菇混合破坏后
//   ① 原版名字**没有**像正常对局那样被隐藏；
//   ② 名字颜色也**没有**渲染（看着像坏掉了）；破坏结束才恢复正常。
//
// ② 的成因很清楚：破坏期间游戏把玩家的外观类型设成
//   `PlayerOutfitType.MushroomMixup`（MalumMenu 也用这个值做判断），
//   而我们的头顶名字渲染把"非 Default 外观"当成变形中 → 整格跳过 ⇒ 名字没颜色。
//
// 参考实现：**BetterAmongUs 没有处理这一块**（它只有反作弊侧对 UpdateSystem 的校验，
//   名字显示完全交给原版）。真正做这件事的是 TownofNext / EndlessHostRoles：
//   在游戏的 `PlayerControl.MixUpOutfit`（混装开始）与 `PlayerControl.FixMixedUpOutfit`（复原）
//   上挂 Postfix，调用 `cosmetics.ToggleNameVisible(false / true)`。这里照这套来。
//
// 我们做成"两条腿"，避免任何一条漏掉：
//   1. 上述两个 Postfix —— 与游戏回调同步，第一时间隐藏 / 复原；
//   2. 每帧兜底 `Tick()`（由 AmethystNameRendering 调用）—— 覆盖自由模式、中途加入、
//      以及回调没走到的边角情况（例如破坏结束时某个玩家刚好不在场上）。
internal static class AmethystMushroomMixup
{
    private static bool _hidden;

    // 破坏是否进行中（与 AmethystRpcGuard 里同一套取法，不靠猜）
    internal static bool IsActive()
    {
        try
        {
            ShipStatus ship = ShipStatus.Instance;
            if (ship == null || ship.Systems == null) return false;
            if (ship.Systems.TryGetValue(SystemTypes.MushroomMixupSabotage, out var m) && m != null)
            {
                var s = m.TryCast<MushroomMixupSabotageSystem>();
                if (s != null && s.IsActive) return true;
            }
        }
        catch { }
        return false;
    }

    // ⚠ 第 179 轮复查：`IsActive()` 被**每帧调用两次** ——
    //   一次在 `Tick()` 里，一次在 `AmethystNameRendering.FlushNames()` 里。
    //   它内部要读 `ShipStatus.Instance` 并做一次 `Systems.TryGetValue` + `TryCast`，
    //   全是跨 IL2CPP 边界的原生调用，而破坏是否进行中**一帧之内不可能变**。
    //   这里按帧缓存，两个调用点共用一次读取。
    private static bool _activeCached;
    private static int _activeFrame = -1;

    internal static bool ActiveNow()
    {
        int frame = Time.frameCount;
        if (frame == _activeFrame) return _activeCached;
        _activeFrame = frame;
        _activeCached = IsActive();
        return _activeCached;
    }

    private static bool Alive(PlayerControl pc)
    {
        try { return pc != null && pc.Data != null && !pc.Data.IsDead; } catch { return false; }
    }

    internal static void HideName(PlayerControl pc)
    {
        if (!AmethystColors.NameColorsOn || !Alive(pc) || pc.cosmetics == null) return;
        try { pc.cosmetics.ToggleNameVisible(false); } catch { }
    }

    internal static void ShowName(PlayerControl pc)
    {
        if (!AmethystColors.NameColorsOn || !Alive(pc) || pc.cosmetics == null) return;
        try { pc.cosmetics.ToggleNameVisible(true); } catch { }
    }

    // 每帧兜底
    //
    // ⚠ 第 179 轮性能：旧实现在破坏**进行中的每一帧**都调用一次 `Apply(true)` ——
    //   而 `Apply` 会遍历全体玩家、对每名玩家读一次 `pc.cosmetics.isNameVisible`（原生读取）。
    //   也就是整段破坏期间每帧一次 O(n) 原生扫描，纯属浪费：名字只在"开始 / 结束"两个
    //   时刻需要翻转。现在改为**状态变化时才做**，另外每 20 帧补一次（覆盖中途加入的玩家）。
    private static int _lastApplyFrame = -999;

    internal static void Tick()
    {
        if (!AmethystColors.NameColorsOn) { _hidden = false; return; }

        bool active = ActiveNow();
        if (!active)
        {
            if (_hidden)
            {
                Apply(false);      // 破坏结束 → 复原（与 TownofNext 一样用 ToggleNameVisible(true)）
                _hidden = false;
                _lastApplyFrame = -999;
            }
            return;
        }

        // 进行中：只在开始那一帧、以及此后每 20 帧补一次（兼顾中途加入 / 复活）。
        int frame = Time.frameCount;
        bool first = !_hidden;
        if (first || frame - _lastApplyFrame >= 20)
        {
            _lastApplyFrame = frame;
            Apply(true);
            _hidden = true;
        }
    }

    private static void Apply(bool hide)
    {
        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl pc = all[i];
                if (pc == null || !Alive(pc) || pc.cosmetics == null) continue;

                // ❌ 预防 IL2CPP 本地异常跳床：在读取 isNameVisible 前，确保对象在托管内存中存活，且 cosmetics 未被解构。
                bool visible;
                try 
                { 
                    if (pc.gameObject == null || pc.cosmetics.gameObject == null) continue;
                    visible = pc.cosmetics.isNameVisible; 
                } 
                catch { continue; }

                if (hide && visible) HideName(pc);
                else if (!hide && !visible) ShowName(pc);
            }
        }
        catch { }
    }
}

// 混装开始（每个存活玩家各来一次）→ 隐藏名字
[HarmonyPatch(typeof(PlayerControl), "MixUpOutfit")]
internal static class AmethystMixUpOutfitPatch
{
    private static void Postfix(PlayerControl __instance) => AmethystMushroomMixup.HideName(__instance);
}

// 复原（破坏结束）→ 恢复名字
[HarmonyPatch(typeof(PlayerControl), "FixMixedUpOutfit")]
internal static class AmethystFixMixedUpOutfitPatch
{
    private static void Postfix(PlayerControl __instance) => AmethystMushroomMixup.ShowName(__instance);
}
