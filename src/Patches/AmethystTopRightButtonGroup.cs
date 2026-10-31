using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystTopRightButtonGroup
{
    private sealed class Item
    {
        internal Transform Transform;
        internal AspectPosition Aspect;
        internal bool AspectEnabled;
        internal Vector3 Position;
    }

    private sealed class HiddenItem
    {
        internal GameObject GameObject;
        internal bool Active;
    }

    private sealed class ZeroedItem
    {
        internal Transform Transform;
        internal Vector3 Position;
    }

    private const float Spacing = 0.55f;
    private const float MatchInfoOffsetY = 0.008f;
    private const float BaseEdgeX = 0.48f;
    private const float BaseEdgeY = 0.51f;
    private const float PanelScale = 0.551f;
    private const string PanelResource = "Amethyst.Resources.TopRightButtonHolder.png";

    private static readonly List<Item> Items = new List<Item>();
    private static readonly List<HiddenItem> Hidden = new List<HiddenItem>();
    private static readonly List<ZeroedItem> Zeroed = new List<ZeroedItem>();
    private static readonly List<SpriteRenderer> Disabled = new List<SpriteRenderer>();
    private static HudManager _hud;
    private static AspectPosition _holder;
    private static Transform _buttonsHolder;
    private static Transform _matchInfoTransform;
    private static SpriteRenderer _background;
    private static Sprite _panelSprite;
    private static float _panelHeight = 1.42f;
    private static AspectPosition _chatOriginalAspect;
    private static Vector3 _chatNotifyDotOriginal;
    private static Vector3 _friendsNotifOriginal;
    private static bool _hasFriendsNotif;
    private static bool _bound;
    private static float _nextScan;
    private static float _nextFind;

    internal static bool Enabled => AmethystConfig.TopRightButtonGroup == null || AmethystConfig.TopRightButtonGroup.Value;

    internal static bool TryGetRightEdge(out float x)
    {
        x = 0f;
        if (_background == null || _background.sprite == null) return false;
        x = _background.bounds.max.x;
        return true;
    }

    internal static void Tick()
    {
        HudManager hud = HudManager.Instance;
        if (hud == null)
        {
            if (_bound) Restore();
            return;
        }
        if (_hud != hud)
        {
            Restore();
            _hud = hud;
            _nextScan = 0f;
        }
        if (!Enabled)
        {
            if (_bound) Restore();
            return;
        }
        if (_bound) return;
        if (Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + 0.5f;
        Bind(hud);
    }

    internal static void LateTick()
    {
        if (!_bound || _buttonsHolder == null || _holder == null) return;

        EnsureMatchInfo();

        int count = 0;
        for (int i = 0; i < Items.Count; i++)
        {
            Item item = Items[i];
            if (item == null || item.Transform == null) continue;
            if (!item.Transform.gameObject.activeInHierarchy) continue;
            item.Transform.position = _buttonsHolder.TransformPoint(new Vector3(-Spacing * count, item.Transform == _matchInfoTransform ? MatchInfoOffsetY : 0f, 0f));
            count++;
        }

        if (_background == null || _background.sprite == null || count == 0) return;
        _background.transform.localPosition = new Vector3(-0.275f * (count - 1) + 0.008f, 0.0228f, 0.1f);
        _background.size = new Vector2(1.48f + 0.99818516f * (count - 1), _panelHeight);
    }

    private static void Bind(HudManager hud)
    {
        try
        {
            if (hud.UICamera == null) return;

            GameObject holderObject = new GameObject("AmethystTopRightButtons");
            holderObject.layer = hud.gameObject.layer;
            holderObject.transform.SetParent(hud.transform, false);

            _holder = holderObject.AddComponent<AspectPosition>();
            _holder.Alignment = AspectPosition.EdgeAlignments.RightTop;
            _holder.DistanceFromEdge = new Vector3(BaseEdgeX, BaseEdgeY, -400f);
            _holder.parentCam = hud.UICamera;
            _holder.updateAlways = true;
            _holder.AdjustPosition();

            GameObject buttonsObject = new GameObject("Holder");
            buttonsObject.layer = hud.gameObject.layer;
            _buttonsHolder = buttonsObject.transform;
            _buttonsHolder.SetParent(holderObject.transform, false);
            _buttonsHolder.localPosition = new Vector3(0f, 0f, -10f);

            List<Transform> buttons = Collect(hud);
            if (buttons.Count < 2)
            {
                UnityEngine.Object.Destroy(holderObject);
                _holder = null;
                _buttonsHolder = null;
                return;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                AddItem(buttons[i]);
            }

            ApplyChatDetail(hud);
            CreateBackground(holderObject);
            EnsureMatchInfo();

            _bound = true;
        }
        catch { Restore(); }
    }

    private static void AddItem(Transform transform)
    {
        if (transform == null) return;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i] != null && Items[i].Transform == transform) return;
        }

        Item item = new Item
        {
            Transform = transform,
            Aspect = transform.GetComponent<AspectPosition>(),
            Position = transform.position,
        };
        item.AspectEnabled = item.Aspect != null && item.Aspect.enabled;
        if (item.Aspect != null) item.Aspect.enabled = false;
        Items.Add(item);
        HidePanels(transform);
    }




    private static void CenterStateSprites(PassiveButton button)
    {
        if (button == null) return;
        try
        {
            if (button.activeSprites != null) KeepZeroed(button.activeSprites.transform);
            if (button.inactiveSprites != null) KeepZeroed(button.inactiveSprites.transform);
            if (button.selectedSprites != null) KeepZeroed(button.selectedSprites.transform);
            Collider2D collider = button.GetComponent<Collider2D>();
            if (collider != null) collider.offset = Vector2.zero;
        }
        catch { }
    }

    private static void KeepZeroed(Transform transform)
    {
        for (int i = 0; i < Zeroed.Count; i++)
        {
            if (Zeroed[i].Transform == transform) { transform.localPosition = Vector3.zero; return; }
        }
        Zeroed.Add(new ZeroedItem { Transform = transform, Position = transform.localPosition });
        transform.localPosition = Vector3.zero;
    }

    private static void EnsureMatchInfo()
    {
        if (!_bound) return;
        if (Time.unscaledTime < _nextFind) return;
        _nextFind = Time.unscaledTime + 1f;

        PassiveButton button = null;
        try { if (_hud != null) button = _hud.MatchInfoButton; } catch { }
        if (button == null) return;
        _matchInfoTransform = button.transform;
        AddItem(button.transform);
        CenterStateSprites(button);
    }

    private static void CreateBackground(GameObject holderObject)
    {
        Sprite sprite = PanelSprite();
        if (sprite == null) return;

        GameObject backgroundObject = new GameObject("background");
        backgroundObject.layer = holderObject.layer;
        backgroundObject.transform.SetParent(holderObject.transform, false);
        backgroundObject.transform.localPosition = new Vector3(0f, 0.0228f, 0.1f);
        backgroundObject.transform.localScale = new Vector3(PanelScale, PanelScale, 1f);

        _background = backgroundObject.AddComponent<SpriteRenderer>();
        _background.sprite = sprite;
        _background.drawMode = SpriteDrawMode.Sliced;
        _background.tileMode = SpriteTileMode.Continuous;
        _background.size = new Vector2(1.48f, sprite.bounds.size.y);
        _panelHeight = sprite.bounds.size.y;
    }

    private static Sprite PanelSprite()
    {
        if (_panelSprite != null) return _panelSprite;
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(PanelResource);
            if (stream == null || stream.Length <= 0) return null;

            using MemoryStream buffer = new MemoryStream((int)stream.Length);
            stream.CopyTo(buffer);
            byte[] data = buffer.ToArray();

            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!ImageConversion.LoadImage(texture, data, true))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
            texture.filterMode = FilterMode.Bilinear;

            _panelSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(50f, 50f, 50f, 50f));
            _panelSprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
        }
        catch { }
        return _panelSprite;
    }


    private static void HidePanels(Transform root)
    {
        try
        {
            SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                SpriteRenderer renderer = all[i];
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                string name = renderer.sprite.name;
                if (name.IndexOf("ButtonPanel", StringComparison.OrdinalIgnoreCase) < 0) continue;
                renderer.enabled = false;
                Disabled.Add(renderer);
            }

            Transform background = root.Find("background");
            if (background == null) background = root.Find("Background");
            if (background != null && background.gameObject.activeSelf)
            {
                Hidden.Add(new HiddenItem { GameObject = background.gameObject, Active = true });
                background.gameObject.SetActive(false);
            }
        }
        catch { }
    }

    private static void HideWidestPanel(Transform root)
    {
        try
        {
            SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer widest = null;
            SpriteRenderer second = null;
            float widestWidth = 0f;
            float secondWidth = 0f;

            for (int i = 0; i < all.Length; i++)
            {
                SpriteRenderer renderer = all[i];
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                float width = renderer.bounds.size.x;
                if (width > widestWidth)
                {
                    secondWidth = widestWidth;
                    second = widest;
                    widestWidth = width;
                    widest = renderer;
                }
                else if (width > secondWidth)
                {
                    secondWidth = width;
                    second = renderer;
                }
            }

            if (widest == null || widestWidth < 0.7f) return;
            if (second != null && widestWidth < secondWidth * 1.2f) return;

            widest.enabled = false;
            Disabled.Add(widest);
        }
        catch { }
    }

    private static void ApplyChatDetail(HudManager hud)
    {
        try
        {
            ChatController chat = hud.Chat;
            if (chat == null) return;

            _chatOriginalAspect = chat.chatButtonAspectPosition;
            chat.chatButtonAspectPosition = _holder;

            PassiveButton chatButton = chat.chatButton;
            if (chatButton != null)
            {
                HideWidestPanel(chatButton.transform);
                CenterStateSprites(chatButton);
            }

            if (chat.chatNotifyDot != null)
            {
                _chatNotifyDotOriginal = chat.chatNotifyDot.transform.localPosition;
                chat.chatNotifyDot.transform.localPosition = new Vector3(0.23f, 0.23f, -1f);
            }
        }
        catch { }

        try
        {
            FriendsListButton listButton = hud.GetComponentInChildren<FriendsListButton>(true);
            if (listButton != null && listButton.NotifCircle != null)
            {
                _friendsNotifOriginal = listButton.NotifCircle.transform.localPosition;
                _hasFriendsNotif = true;
                listButton.NotifCircle.transform.localPosition = new Vector3(0.6f, 0.95f, -1f);
            }
        }
        catch { }
    }

    private static List<Transform> Collect(HudManager hud)
    {
        List<Transform> list = new List<Transform>();
        Add(list, FriendsButton(hud));
        Add(list, hud.MapButton != null ? hud.MapButton.transform : null);
        try { Add(list, hud.SettingsButton != null ? hud.SettingsButton.transform : null); } catch { }
        try { Add(list, hud.Chat != null && hud.Chat.chatButton != null ? hud.Chat.chatButton.transform : null); } catch { }
        return list;
    }

    private static void Add(List<Transform> list, Transform transform)
    {
        if (transform == null) return;
        if (transform.GetComponent<PassiveButton>() == null)
        {
            PassiveButton button = transform.GetComponentInChildren<PassiveButton>(true);
            if (button == null) return;
            transform = button.transform;
        }
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == transform) return;
        }
        list.Add(transform);
    }

    private static Transform FriendsButton(HudManager hud)
    {
        try
        {
            FriendsListButton listButton = hud.GetComponentInChildren<FriendsListButton>(true);
            if (listButton != null && listButton.Button != null) return listButton.Button.transform;
        }
        catch { }
        GameObject friends = AmethystFriendsBar.FindFriendsButton();
        return friends != null ? friends.transform : null;
    }

    private static void Restore()
    {
        try
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Item item = Items[i];
                if (item == null || item.Transform == null) continue;
                item.Transform.position = item.Position;
                if (item.Aspect != null) item.Aspect.enabled = item.AspectEnabled;
            }
            for (int i = 0; i < Hidden.Count; i++)
            {
                if (Hidden[i] != null && Hidden[i].GameObject != null) Hidden[i].GameObject.SetActive(Hidden[i].Active);
            }
            for (int i = 0; i < Disabled.Count; i++)
            {
                if (Disabled[i] != null) Disabled[i].enabled = true;
            }
            for (int i = 0; i < Zeroed.Count; i++)
            {
                if (Zeroed[i] != null && Zeroed[i].Transform != null) Zeroed[i].Transform.localPosition = Zeroed[i].Position;
            }

            ChatController chat = _hud != null ? _hud.Chat : null;
            if (chat != null)
            {
                chat.chatButtonAspectPosition = _chatOriginalAspect;
                if (chat.chatNotifyDot != null) chat.chatNotifyDot.transform.localPosition = _chatNotifyDotOriginal;
                if (_hasFriendsNotif)
                {
                    FriendsListButton listButton = _hud.GetComponentInChildren<FriendsListButton>(true);
                    if (listButton != null && listButton.NotifCircle != null)
                        listButton.NotifCircle.transform.localPosition = _friendsNotifOriginal;
                }
            }

            if (_holder != null) UnityEngine.Object.Destroy(_holder.gameObject);
        }
        catch { }
        Items.Clear();
        Hidden.Clear();
        Disabled.Clear();
        Zeroed.Clear();
        _holder = null;
        _buttonsHolder = null;
        _matchInfoTransform = null;
        _background = null;
        _hud = null;
        _bound = false;
        _hasFriendsNotif = false;
        _nextFind = 0f;
    }
}