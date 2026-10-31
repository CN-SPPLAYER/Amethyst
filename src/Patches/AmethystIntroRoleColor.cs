using System;
using System.Reflection;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

internal static class AmethystIntroRoleColor
{
    private static bool _active;

    internal static bool Active => _active;
    internal static void Begin() => _active = true;
    internal static void End() => _active = false;

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

[HarmonyPatch]
internal static class AmethystShowRoleStateMachinePatch
{
    
    internal static MethodBase TargetMethod()
    {
        try
        {
            Type intro = typeof(IntroCutscene);
            foreach (Type nested in intro.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                
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

    private static void Postfix(object __instance, ref bool __result)
    {
        try
        {
            if (!__result) return;   

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

        try { return IntroCutscene.Instance; } catch { return null; }
    }
}

[HarmonyPatch(typeof(IntroCutscene), "OnDestroy")]
internal static class AmethystIntroRoleColorDestroyPatch
{
    private static void Postfix() => AmethystIntroRoleColor.End();
}
