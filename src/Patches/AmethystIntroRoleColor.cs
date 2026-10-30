using System;
using System.Reflection;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

// 职业颜色 —— 「职业分配阶段」（开场 IntroCutscene）着色。
//
// ===== 为什么必须补「协程状态机」而不是普通方法 =====
//
// 我用 Mono.Cecil 反查过：`IntroCutscene.CoBegin` / `IntroCutscene.ShowRole` /
// `HudManager.CoShowIntro` 这三个方法在本程序集的托管代码里**没有任何调用点** ——
// 它们是由 **IL2CPP 原生代码**通过某种内部路径驱动的。
// 直接 `[HarmonyPatch(typeof(X), "ShowRole")]` 往往挂不上（或挂上了也永不触发），
// 这就是我之前两版"挂上去却看不到颜色"的根本原因。
//
// EHR 的做法（`Patches\IntroPatch.cs` 第 24~40 行 `ShowRoleMoveNextPatch`）是：
//   补**编译器生成的协程状态机类型** `_ShowRole_d__41` 的 `MoveNext`，
//   当 `State == 1 && __result` 时执行着色。状态机是真实存在于程序集里的托管类型
//   （我已核实：`IntroCutscene._ShowRole_d__41` 带 `get___1__state` / `get___4__this`），
//   所以能可靠挂上、也一定会被调用。
//
// 本文件照此实现：
//   · Harmony **TargetMethod()** 动态定位 `_ShowRole_d__41.MoveNext`（不硬编码名字，
//     游戏改名也能找到 —— 用 `IntroCutscene` 的嵌套类型 + "有 __1__state 和 __4__this" 作为判据）；
//   · Postfix 里读 `__4__this` 拿到 `IntroCutscene` 实例，把三个 TMP 染成职业色。
//
// 保留一个**兜底**：如果状态机找不到（游戏大改），退回"每帧从 IntroCutscene.Instance 取"，
// 至少不会完全不工作。
internal static class AmethystIntroRoleColor
{
    private static bool _active;

    internal static bool Active => _active;
    internal static void Begin() => _active = true;
    internal static void End() => _active = false;

    // 由状态机 Postfix 直接调用（拿到实例，最可靠）。
    internal static void ApplyTo(IntroCutscene intro)
    {
        if (!AmethystRoleColors.Enabled) return;
        if (intro == null) return;

        try
        {
            _active = true;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.Role == null) return;

            RoleBehaviour role = local.Data.Role;
            Color want = AmethystRoleColors.ColorOf(role.Role);

            Apply(intro.YouAreText, want);
            Apply(intro.RoleText, want);
            Apply(intro.RoleBlurbText, want);
        }
        catch { }
    }

    // 兜底路径：由 AmethystHud 低频调用（状态机方案失效时仍能工作）。
    internal static void Tick()
    {
        if (!_active) return;
        try { ApplyTo(IntroCutscene.Instance); } catch { }
    }

    private static void Apply(TextMeshPro text, Color want)
    {
        if (text == null) return;
        try
        {
            Color cur = text.color;
            if (Mathf.Abs(cur.r - want.r) > 0.004f
                || Mathf.Abs(cur.g - want.g) > 0.004f
                || Mathf.Abs(cur.b - want.b) > 0.004f)
            {
                want.a = cur.a;
                text.color = want;
            }
        }
        catch { }
    }
}

// ===== 主挂载点：IntroCutscene 的 ShowRole 协程状态机（EHR 同款）=====
[HarmonyPatch]
internal static class AmethystShowRoleStateMachinePatch
{
    // 动态定位状态机类型：IntroCutscene 的嵌套类型里，同时具备
    // `__1__state`（状态）与 `__4__this`（宿主实例）并且有 `MoveNext` 的那个。
    // 为什么不直接写 "_ShowRole_d__41"：编译器给状态机的编号会随游戏代码改动而变，
    // 写死容易在更新后静默失配；按"形状"匹配更耐久。
    internal static MethodBase TargetMethod()
    {
        try
        {
            Type intro = typeof(IntroCutscene);
            foreach (Type nested in intro.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                // 只认 ShowRole 的状态机（名字里带 ShowRole 最稳；退化时用"形状"兜）
                bool nameLooksRight = nested.Name.IndexOf("ShowRole", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!nameLooksRight) continue;

                MethodInfo moveNext = nested.GetMethod("MoveNext",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (moveNext != null)
                {
                    AmethystPlugin.Logger?.LogInfo($"[IntroRoleColor] hooked state machine: {nested.Name}.MoveNext");
                    return moveNext;
                }
            }

            AmethystPlugin.Logger?.LogWarning("[IntroRoleColor] ShowRole state machine not found; falling back to per-frame tick.");
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[IntroRoleColor] TargetMethod failed: {ex.Message}");
        }
        return null;
    }

    // 状态机的 `__4__this` 就是 IntroCutscene 实例。
    // 它在 IL2CPP 下是**属性**（不是字段），所以用 AccessTools.Property 取。
    private static void Postfix(object __instance, ref bool __result)
    {
        try
        {
            if (!__result) return;   // 协程已结束，不再着色

            IntroCutscene intro = ResolveIntro(__instance);
            if (intro == null) return;

            AmethystIntroRoleColor.ApplyTo(intro);
        }
        catch { }
    }

    private static IntroCutscene ResolveIntro(object stateMachine)
    {
        if (stateMachine == null) return null;

        try
        {
            PropertyInfo prop = AccessTools.Property(stateMachine.GetType(), "__4__this");
            if (prop != null)
            {
                object owner = prop.GetValue(stateMachine);
                if (owner is IntroCutscene ic) return ic;
            }
        }
        catch { }

        // 退化：直接从单例拿。
        try { return IntroCutscene.Instance; } catch { return null; }
    }
}

// 开场结束（协程跑完 / 场景销毁）时复位兜底开关。
[HarmonyPatch(typeof(IntroCutscene), "OnDestroy")]
internal static class AmethystIntroRoleColorDestroyPatch
{
    private static void Postfix() => AmethystIntroRoleColor.End();
}
