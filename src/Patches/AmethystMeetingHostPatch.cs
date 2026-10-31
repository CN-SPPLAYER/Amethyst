using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystShowHostMeeting
{
    internal static bool Enabled => AmethystConfig.ShowMeetingHost != null && AmethystConfig.ShowMeetingHost.Value;

    internal static bool Valid()
    {
        var net = AmongUsClient.Instance;
        return net != null && net.NetworkMode != NetworkModes.FreePlay;
    }

    private static TextMeshPro _nameText;
    private static int _boundHud = -1;
    private static string _hostName;
    private static string _lastText;
    private static int _retryAt;

    internal static void Apply(MeetingHud hud)
    {
        try
        {
            if (!Enabled || !Valid()) return;
            if (hud == null || hud.ProceedButton == null || hud.HostIcon == null) return;

            var hostClient = AmongUsClient.Instance.GetHost();
            if (hostClient == null || hostClient.Character == null || hostClient.Character.Data == null) return;

            DestroyTranslators(hud.ProceedButton.gameObject);

            int colorId = hostClient.Character.Data.DefaultOutfit != null ? hostClient.Character.Data.DefaultOutfit.ColorId : 0;
            if (colorId >= 0 && Palette.PlayerColors != null && colorId < Palette.PlayerColors.Length)
                PlayerMaterial.SetColors(colorId, hud.HostIcon);

            _boundHud = hud.GetInstanceID();
            _nameText = hud.ProceedButton.gameObject.GetComponentInChildren<TextMeshPro>();
            _hostName = null;
            _lastText = null;

            if (_nameText != null)
            {
                string name = hostClient.Character.Data.PlayerName;
                string text = AmethystText.T("Host", name);
                if (_nameText.text != text) _nameText.text = text;
                _hostName = name;
                _lastText = text;
            }
        }
        catch { }
    }

    internal static void Ensure(MeetingHud hud)
    {
        try
        {
            if (hud == null) return;

            if (_boundHud != hud.GetInstanceID() || _nameText == null)
            {
                
                if (Time.frameCount >= _retryAt)
                {
                    _retryAt = Time.frameCount + 30;
                    Apply(hud);
                }
                return;
            }

            var hostClient = AmongUsClient.Instance.GetHost();
            if (hostClient == null || hostClient.Character == null || hostClient.Character.Data == null) return;

            string name = hostClient.Character.Data.PlayerName;
            
            if (string.Equals(_hostName, name) && string.Equals(_nameText.text, _lastText)) return;

            string text = AmethystText.T("Host", name);
            if (_nameText.text != text) _nameText.text = text;
            _hostName = name;
            _lastText = text;
        }
        catch { }
    }

    internal static void DestroyTranslators(GameObject root)
    {
        if (root == null) return;
        try
        {
            foreach (var t in root.GetComponentsInChildren<TextTranslatorTMP>(true))
                if (t != null) UnityEngine.Object.Destroy(t);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystShowHostMeetingPatch
{
    private static void Postfix(MeetingHud __instance)
    {
        try
        {
            if (!AmethystShowHostMeeting.Enabled || !AmethystShowHostMeeting.Valid()) return;
            if (__instance == null) return;

            if (__instance.ProceedButton != null && __instance.HostIcon != null)
            {
                __instance.ProceedButton.gameObject.transform.localPosition = new Vector3(-2.5f, 2.2f, 0f);
                var sr = __instance.ProceedButton.gameObject.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;
                var pb = __instance.ProceedButton.GetComponent<PassiveButton>();
                if (pb != null) pb.enabled = false;
                __instance.HostIcon.enabled = true;
                __instance.HostIcon.gameObject.SetActive(true);
                __instance.ProceedButton.gameObject.SetActive(true);
            }

            AmethystShowHostMeeting.Apply(__instance);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(MeetingHud), "Update")]
internal static class AmethystShowHostMeetingUpdatePatch
{
    private static void Postfix(MeetingHud __instance)
    {
        try
        {
            if (!AmethystShowHostMeeting.Enabled || !AmethystShowHostMeeting.Valid()) return;
            AmethystShowHostMeeting.Ensure(__instance);
        }
        catch { }
    }
}
