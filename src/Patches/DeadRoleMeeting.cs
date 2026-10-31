using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;


internal sealed class AmethystMeetingInfoDisplay : MonoBehaviour
{
    private PlayerVoteArea _pva;
    private TextMeshPro _nameText;
    private TextMeshPro _infoText;
    private Vector3 _namePos;
    private Vector3 _infoPos;
    private string _lastInfoText = string.Empty;
    
    private bool _lastHasInfo;

    public AmethystMeetingInfoDisplay(IntPtr ptr) : base(ptr) { }

    internal void Init(PlayerVoteArea pva)
    {
        _pva = pva;
        if (_pva == null || _pva.NameText == null) return;

        _nameText = _pva.NameText;
        _namePos = _nameText.transform.localPosition;
        _infoText = InstantiatePlayerInfoText("AmethystMeetingInfo_TMP", new Vector3(0f, 0.16f, 0f), _pva.transform);
        _infoPos = _infoText.transform.localPosition;
        _infoText.fontSize = 1.4f;
        _infoText.fontSizeMin = 1.4f;
        _infoText.fontSizeMax = 1.4f;
    }

    private TextMeshPro InstantiatePlayerInfoText(string name, Vector3 positionOffset, Transform parent)
    {
        TextMeshPro newTextObject = UnityEngine.Object.Instantiate(_nameText, parent);
        newTextObject.name = name;
        newTextObject.transform.localPosition = _namePos + positionOffset;
        newTextObject.transform.localScale = Vector3.one;
        newTextObject.text = string.Empty;
        newTextObject.gameObject.SetActive(true);
        return newTextObject;
    }

    private void LateUpdate()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_MeetingInfo);
    try { LateUpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_MeetingInfo); }
    }

    private void LateUpdateInner()
    {
            if (_pva == null || _nameText == null || _infoText == null)
                return;

            try
            {
                byte pid = _pva.PlayerId;

                string nextText = string.Empty;
                if (AmethystPlayerCacheStore.TryGet(pid, out AmethystPlayerCache c) && c != null)
                    nextText = c.MeetingText;

                string mark = _pva.AmDead ? string.Empty : AmethystRoleTagStore.Suffix(pid);
                if (!string.IsNullOrEmpty(mark))
                    nextText = string.IsNullOrEmpty(nextText) ? mark.Trim() : nextText + mark;

                if (!string.Equals(nextText, _lastInfoText, StringComparison.Ordinal))
                {
                    _infoText.SetText(nextText);
                    _lastInfoText = nextText;
                }

                bool hasInfo = !string.IsNullOrEmpty(_lastInfoText);
                if (hasInfo != _lastHasInfo)
                {
                    _lastHasInfo = hasInfo;
                    _infoText.transform.localPosition = hasInfo ? _infoPos : _namePos + new Vector3(0f, 0.16f, 0f);
                    _infoText.enabled = hasInfo;
                }
            }
            catch { }
    
    }
}


[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class AmethystMeetingInfoDisplayAttachPatch
{
    private static void Postfix(MeetingHud __instance)
    {
        try
        {
            if (__instance == null || __instance.playerStates == null) return;

            foreach (PlayerVoteArea pva in __instance.playerStates)
            {
                if (pva == null) continue;
                if (pva.gameObject.GetComponent<AmethystMeetingInfoDisplay>() == null)
                    pva.gameObject.AddComponent<AmethystMeetingInfoDisplay>().Init(pva);
            }
        }
        catch { }
    }
}
