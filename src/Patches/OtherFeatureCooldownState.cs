using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using AmongUs.Data;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace Amethyst.Patches;


internal static class AmethystSabotageCooldownState
{
    internal static readonly Dictionary<SystemTypes, TextMeshPro> TimerTexts = new Dictionary<SystemTypes, TextMeshPro>();

    private static readonly Dictionary<Type, PropertyInfo> IsActiveProps = new Dictionary<Type, PropertyInfo>();
    private static readonly Dictionary<SystemTypes, bool> ActiveThisFrame = new Dictionary<SystemTypes, bool>();
    private static int _activeFrame = -1;

    private static PropertyInfo ResolveIsActive(Type type)
    {
        if (type == null) return null;
        if (IsActiveProps.TryGetValue(type, out PropertyInfo cached)) return cached;

        PropertyInfo prop = null;
        try { prop = type.GetProperty("IsActive"); } catch { prop = null; }
        IsActiveProps[type] = prop;
        return prop;
    }

    internal static bool IsActive(SystemTypes type)
    {
        try
        {
            
            int frame = Time.frameCount;
            if (frame != _activeFrame)
            {
                _activeFrame = frame;
                ActiveThisFrame.Clear();
            }
            else if (ActiveThisFrame.TryGetValue(type, out bool memo))
            {
                return memo;
            }

            bool result = false;
            if (ShipStatus.Instance != null
                && ShipStatus.Instance.Systems.TryGetValue(type, out ISystemType sys)
                && sys != null)
            {
                PropertyInfo prop = ResolveIsActive(sys.GetType());
                if (prop != null) result = prop.GetValue(sys) is bool value && value;
            }

            ActiveThisFrame[type] = result;
            return result;
        }
        catch { return false; }
    }
}


[HarmonyPatch(typeof(InfectedOverlay), "FixedUpdate")]
internal static class AmethystSabotageCooldownPatch
{
    public static void Postfix(InfectedOverlay __instance)
    {
        if (AmethystCompat.OtherSabotageCd) return;
        if (!AmethystConfig.SabotageCooldownDisplay.Value || __instance == null || __instance.sabSystem == null) return;
        try
        {
            float exact = __instance.sabSystem.PercentCool * (__instance.sabSystem.initialCooldown ? 10f : 30f);
            int remaining = Mathf.CeilToInt(exact);
            for (int i = 0; i < __instance.rooms.Count; i++)
            {
                MapRoom room = __instance.rooms[i];
                if (!room.special || room.special.transform == null) continue;
                if (!AmethystSabotageCooldownState.TimerTexts.TryGetValue(room.room, out TextMeshPro text) || text == null || text.transform.parent != room.special.transform)
                {
                    if (text != null) UnityEngine.Object.Destroy(text.gameObject);
                    if (HudManager.Instance == null || HudManager.Instance.KillButton == null || HudManager.Instance.KillButton.cooldownTimerText == null) continue;
                    text = UnityEngine.Object.Instantiate(HudManager.Instance.KillButton.cooldownTimerText, room.special.transform, true);
                    text.alignment = TextAlignmentOptions.Center;
                    text.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                    text.overflowMode = TextOverflowModes.Overflow;
                    text.enableWordWrapping = false;
                    text.color = Color.white;
                    text.fontSize = text.fontSizeMax = text.fontSizeMin = 2.5f;
                    text.sortingOrder = 100;
                    text.gameObject.SetActive(true);
                    AmethystSabotageCooldownState.TimerTexts[room.room] = text;
                }
                bool active = AmethystSabotageCooldownState.IsActive(room.room);

                bool otherActive = false;
                foreach (KeyValuePair<SystemTypes, TextMeshPro> timerEntry in AmethystSabotageCooldownState.TimerTexts)
                {
                    if (timerEntry.Key == room.room) continue;
                    if (AmethystSabotageCooldownState.IsActive(timerEntry.Key)) { otherActive = true; break; }
                }
                if (active || otherActive || __instance.DoorsPreventingSabotage)
                {
                    text.text = active && !__instance.DoorsPreventingSabotage ? "<b><#ff0000>鈻?/color></b>" : "<b><#ff0000>鈯?/color></b>";
                    text.enabled = true;
                }
                else if (remaining <= 0) text.enabled = false;
                else
                {
                    text.text = exact < 10f ? $"<b><#ffff00>{exact:F1}</color></b>" : $"<b><#ffff00>{remaining}</color></b>";
                    text.enabled = true;
                }
            }
        }
        catch { }
    }
}




[HarmonyPatch(typeof(ActionButton), "SetFillUp")]
internal static class AmethystDurationTextPatch
{
    
    internal static float LastFillAt;

    public static void Postfix(ActionButton __instance, float timer)
    {
        if (AmethystCompat.OtherCooldown) return;
        if (!(AmethystConfig.BetterCooldownDisplay.Value || AmethystConfig.CooldownDecimals.Value) || __instance == null || __instance.cooldownTimerText == null) return;
        try
        {
            LastFillAt = Time.realtimeSinceStartup;
            AmethystCooldownTextPatch.Format(__instance, timer);
        }
        catch { }
    }
}


internal static class AmethystCooldownColorState
{
    private static readonly HashSet<RoleTypes> ImpostorRoles = new HashSet<RoleTypes>
    {
        RoleTypes.Impostor, RoleTypes.ImpostorGhost, RoleTypes.Shapeshifter, RoleTypes.Phantom, RoleTypes.Viper
    };

    internal static void Tick(HudManager __instance)
    {
        if (AmethystCompat.OtherCooldown) return;
        if (!AmethystConfig.BetterCooldownDisplay.Value || __instance == null) return;
        try
        {
            Color color = PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null && PlayerControl.LocalPlayer.Data.Role != null && ImpostorRoles.Contains(PlayerControl.LocalPlayer.Data.Role.Role)
                ? new Color32(255, 90, 90, 255) : new Color32(140, 255, 255, 255);
            
            Apply(__instance.AbilityButton, color);
            Apply(__instance.SecondaryAbilityButton, color);
            Apply(__instance.KillButton, color);
            Apply(__instance.ImpostorVentButton, color);
        }
        catch { }
    }

    private static void Apply(ActionButton button, Color color)
    {
        if (button == null || button.cooldownTimerText == null) return;
        button.cooldownTimerText.color = color;
        
        if (!button.isCoolingDown && Time.realtimeSinceStartup - AmethystDurationTextPatch.LastFillAt > 0.5f)
            button.cooldownTimerText.text = string.Empty;
    }
}
