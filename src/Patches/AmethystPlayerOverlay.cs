using System;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

internal sealed class AmethystPlayerOverlay : MonoBehaviour
{
    private const float NameOffsetY = 0.30f;
    private const float ModTagOffsetY = 0.22f;

    private PlayerControl _player;

    private TextMeshPro _nameText;

    private TextMeshPro _modTag;
    private TextMeshPro _aliveTag;
    private TextMeshPro _deadTag;

    private int _createRetryAt;

    private string _wMod = string.Empty;
    private string _wAlive = string.Empty;
    private string _wDead = string.Empty;
    private bool _vMod, _vAlive, _vDead;
    private bool _hasPos;
    private float _lastLineExtra;

    public AmethystPlayerOverlay(IntPtr ptr) : base(ptr) { }

    internal void Init(PlayerControl player)
    {
        _player = player;
        EnsureTexts();
    }

    private void EnsureTexts()
    {
        
        if (_modTag != null && _aliveTag != null && _deadTag != null) return;
        if (_player == null) return;

        int frame = Time.frameCount;
        if (frame < _createRetryAt) return;
        _createRetryAt = frame + 30;

        try
        {
            Transform names = _player.gameObject.transform.Find("Names/NameText_TMP");
            if (names == null) return;

            TextMeshPro source = names.GetComponent<TextMeshPro>();
            if (source == null) return;

            _nameText = source;

            _modTag ??= MakeTag(source, names, "AmethystModClientTag", ModTagOffsetY, 1.4f, false);
            _aliveTag ??= MakeTag(source, names, "AmethystAliveRoleTag", NameOffsetY, 1.6f, true);
            _deadTag ??= MakeTag(source, names, "AmethystRoleTag", NameOffsetY, 1.6f, true);
        }
        catch { }
    }

    private static TextMeshPro MakeTag(TextMeshPro source, Transform parent, string tagName, float y, float size, bool roleStyle)
    {
        TextMeshPro t = UnityEngine.Object.Instantiate(source, parent);
        t.name = tagName;
        t.transform.localPosition = new Vector3(0f, y, 0f);
        t.transform.localScale = Vector3.one;
        t.fontSize = size;
        if (roleStyle)
        {
            t.fontSizeMin = size;
            t.fontSizeMax = size;
            t.enableAutoSizing = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.Center;
        }
        if (t.text != string.Empty) t.text = string.Empty;
        t.gameObject.SetActive(true);
        return t;
    }

    private void LateUpdate()
    {
        if (_player == null) return;

        int probe = AmethystPerfProbe.Begin(Section.S_Overlay);
        try { LateUpdateInner(); }
        finally { AmethystPerfProbe.End(probe, Section.S_Overlay); }
    }

    private void LateUpdateInner()
    {
        EnsureTexts();
        if (_modTag == null && _aliveTag == null && _deadTag == null) return;

        try
        {
            byte id;
            try { id = _player.PlayerId; } catch { return; }
            if (id >= 254) return;

            AmethystPlayerCacheStore.TryGet(id, out AmethystPlayerCache c);

            if (c != null) AmethystPlayerCacheStore.RefreshIfDirty(c);

            int stride = AmethystPerformance.RenderStride;
            if (stride > 1 && (Time.frameCount % stride) != (id % stride)) return;

            UpdateModTag(id);

            UpdateRoleTags(c, id);
        }
        catch { }
    }

    private void UpdateModTag(byte id)
    {
        if (_modTag == null) return;

        bool visible = AmethystPlayerCacheStore.InLobby;
        string want = string.Empty;

        if (visible)
        {
            bool isAme = AmethystModClientDetection.IsDetectedId(id);
            string otherMods = null;
            if (_player != null)
            {
                otherMods = AmethystKnownModDetection.GetDetectedModName(_player);
            }

            if (isAme)
            {
                string ver = AmethystModClientDetection.GetVersion(id);
                
                string line = string.IsNullOrEmpty(ver)
                    ? AmethystText.T("AmeUserTag")
                    : AmethystText.T("AmeUserVerTag", ver);
                if (!string.IsNullOrEmpty(otherMods))
                    want = $"<color=#b554ff>{line}</color> + <color=#ff4a62>[{otherMods}]</color>";
                else
                    want = $"<color=#b554ff>{line}</color>";
            }
            else if (!string.IsNullOrEmpty(otherMods))
            {
                want = $"<color=#ff4a62>[{otherMods}]</color>";
            }
            else
            {
                visible = false;
            }
        }

        if (!string.Equals(want, _wMod, StringComparison.Ordinal))
        {
            _wMod = want;
            try { _modTag.text = want; } catch { }
        }
        if (visible != _vMod)
        {
            _vMod = visible;
            try { _modTag.enabled = visible; } catch { }
        }
    }

    [HideFromIl2Cpp]
    private void UpdateRoleTags(AmethystPlayerCache c, byte id)
    {
        string alive = c != null ? c.AliveText : string.Empty;
        string dead = c != null ? c.DeadText : string.Empty;

        string mark = AmethystRoleTagStore.Suffix(id);
        if (!string.IsNullOrEmpty(mark))
        {
            string m = mark.Trim();
            if (!string.IsNullOrEmpty(dead)) dead = dead + m;
            else if (!string.IsNullOrEmpty(alive)) alive = alive + m;
            else dead = m;
        }

        bool aliveVisible = !string.IsNullOrEmpty(alive);
        bool deadVisible = !string.IsNullOrEmpty(dead);

        if (_aliveTag != null && !string.Equals(alive, _wAlive, StringComparison.Ordinal))
        {
            _wAlive = alive;
            try { _aliveTag.text = alive; } catch { }
        }
        if (_deadTag != null && !string.Equals(dead, _wDead, StringComparison.Ordinal))
        {
            _wDead = dead;
            try { _deadTag.text = dead; } catch { }
        }

        if (aliveVisible != _vAlive)
        {
            _vAlive = aliveVisible;
            try { if (_aliveTag != null) _aliveTag.enabled = aliveVisible; } catch { }
        }
        if (deadVisible != _vDead)
        {
            _vDead = deadVisible;
            try { if (_deadTag != null) _deadTag.enabled = deadVisible; } catch { }
        }

        if (!aliveVisible && !deadVisible) return;

        float extra = c != null ? c.LineExtra : AmethystDeadRoleReveal.NameLineExtra(_nameText);
        if (_hasPos && extra == _lastLineExtra) return;

        _lastLineExtra = extra;
        _hasPos = true;
        float y = NameOffsetY + extra;
        try
        {
            if (_aliveTag != null) _aliveTag.transform.localPosition = new Vector3(0f, y, 0f);
            if (_deadTag != null) _deadTag.transform.localPosition = new Vector3(0f, y, 0f);
        }
        catch { }
    }

    private const string ModTagText = "<color=#b554ff>♥ Amethyst ♥</color>";

}

[HarmonyPatch(typeof(PlayerControl), "Awake")]
internal static class AmethystPlayerOverlayAttachPatch
{
    private static void Postfix(PlayerControl __instance)
    {
        try
        {
            if (__instance == null) return;

            AmethystPlayerCacheStore.OnPlayerAwake(__instance);

            if (__instance.gameObject.GetComponent<AmethystPlayerOverlay>() == null)
                __instance.gameObject.AddComponent<AmethystPlayerOverlay>().Init(__instance);
        }
        catch { }
    }
}

internal static class AmethystOutfitChangeWatcher
{
    
    [HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
    private static class RpcPatch
    {
        private static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId)
        {
            try
            {
                switch ((RpcCalls)callId)
                {
                    case RpcCalls.Shapeshift:
                    case RpcCalls.CheckShapeshift:
                    case RpcCalls.RejectShapeshift:
                    case RpcCalls.StartVanish:
                    case RpcCalls.StartAppear:
                    case RpcCalls.CheckVanish:
                    case RpcCalls.CheckAppear:
                    case RpcCalls.SetColor:
                    case RpcCalls.SetHatStr:
                    case RpcCalls.SetSkinStr:
                    case RpcCalls.SetVisorStr:
                    case RpcCalls.SetPetStr:
                    case RpcCalls.SetNamePlateStr:
                        AmethystPlayerCacheStore.MarkAllDirty();
                        break;
                }
            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(PlayerControl), "MixUpOutfit")]
    private static class MixUpPatch
    {
        private static void Postfix() => AmethystPlayerCacheStore.MarkAllDirty();
    }

    [HarmonyPatch(typeof(PlayerControl), "FixMixedUpOutfit")]
    private static class FixMixUpPatch
    {
        private static void Postfix() => AmethystPlayerCacheStore.MarkAllDirty();
    }
}

internal static class AmethystTextRebuildWatcher
{
    
    [HarmonyPatch(typeof(CosmeticsLayer), "SetName", new Type[] { typeof(string) })]
    private static class SetNamePatch
    {
        private static void Postfix() => AmethystPlayerCacheStore.MarkAllDirty();
    }

    [HarmonyPatch(typeof(CosmeticsLayer), "SetColorBlindColor")]
    private static class SetColorBlindPatch
    {
        private static void Postfix() => AmethystPlayerCacheStore.MarkAllDirty();
    }
}

internal static class AmethystLobbyNameWatcher
{
    [HarmonyPatch(typeof(AmongUsClient), "OnPlayerJoined")]
    private static class JoinedPatch
    {
        private static void Postfix()
        {
            AmethystPlayerCacheStore.MarkAllDirty();
            AmethystPlayerCacheStore.ForceResync();
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), "OnPlayerLeft")]
    private static class LeftPatch
    {
        private static void Postfix()
        {
            AmethystPlayerCacheStore.MarkAllDirty();
            AmethystPlayerCacheStore.ForceResync();
        }
    }
}
