using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Rewired;
using UnityEngine;

namespace Amethyst;

internal static class AmethystKeyGuide
{
    private const string GuideChildName = "HotKeyGuide";
    private const string TextChildName = "HotKeyText";

    private const string SheetTabSpace = "KeyBindCharacters0";   
    private const string SheetLetters = "KeyBindCharacters1";    
    private const string SheetFunction = "KeyBindCharacters2";   
    private const string SheetModifier = "KeyBindCharacters3";   
    private const string SheetMouse = "KeyBindCharacters4";      
    private const string SheetDigits = "KeyBindCharacters5";     
    private const string SheetBackground = "KeyBindBackground";  

    private const int GlyphWidth = 18;
    private const int GlyphHeight = 19;
    private const int BackgroundCell = 36;

    private static readonly Vector2 ButtonOffset = new Vector2(0.48f, 0.48f);
    private const float ButtonScale = 1.4285715f;

    private static readonly Vector2 SmallButtonOffset = new Vector2(0.12856665f, 0.12856665f);
    private const float SmallButtonScale = 1f;

    private const int ActionMapAndSabotage = 4;   
    private const int ActionUseAndPet = 6;        
    private const int ActionReport = 7;           
    private const int ActionKill = 8;             
    private const int ActionVent = 50;            
    private const int ActionAbility = 49;         

    private readonly struct Glyph
    {
        internal readonly string Sheet;
        internal readonly int Index;

        internal Glyph(string sheet, int index)
        {
            Sheet = sheet;
            Index = index;
        }
    }

    private struct Slot
    {
        internal GameObject Button;
        internal KeyCode Key;
        internal bool Small;
    }

    private static readonly Dictionary<KeyCode, Glyph> Glyphs = BuildGlyphs();

    private static readonly Dictionary<string, Sprite[]> SheetCache = new Dictionary<string, Sprite[]>(8);

    private static readonly List<Slot> Wanted = new List<Slot>(8);
    private static readonly List<Slot> Applied = new List<Slot>(8);

    private static readonly HashSet<KeyCode> ReportedUnsupported = new HashSet<KeyCode>();
    private static bool _reportedRewiredFailure;

    private static bool _reportedMissingBackground;

    private static HudManager _hud;

    private static Dictionary<KeyCode, Glyph> BuildGlyphs()
    {
        var map = new Dictionary<KeyCode, Glyph>(96);

        map[KeyCode.Tab] = new Glyph(SheetTabSpace, 0);
        map[KeyCode.Space] = new Glyph(SheetTabSpace, 1);
        map[KeyCode.Comma] = new Glyph(SheetTabSpace, 2);
        map[KeyCode.Period] = new Glyph(SheetTabSpace, 3);

        for (int i = 0; i < 26; i++)
            map[(KeyCode)((int)KeyCode.A + i)] = new Glyph(SheetLetters, i);

        for (int i = 0; i < 15; i++)
            map[(KeyCode)((int)KeyCode.F1 + i)] = new Glyph(SheetFunction, i);

        map[KeyCode.RightShift] = new Glyph(SheetModifier, 0);
        map[KeyCode.LeftShift] = new Glyph(SheetModifier, 1);
        map[KeyCode.RightControl] = new Glyph(SheetModifier, 2);
        map[KeyCode.LeftControl] = new Glyph(SheetModifier, 3);
        map[KeyCode.RightAlt] = new Glyph(SheetModifier, 4);
        map[KeyCode.LeftAlt] = new Glyph(SheetModifier, 5);

        for (int i = 0; i < 6; i++)
            map[(KeyCode)((int)KeyCode.Mouse0 + i)] = new Glyph(SheetMouse, i);

        for (int i = 0; i < 10; i++)
            map[(KeyCode)((int)KeyCode.Alpha0 + i)] = new Glyph(SheetDigits, i);

        return map;
    }

    internal static void Tick()
    {
        try
        {
            HudManager hud = HudManager.Instance;
            if (hud == null) return;              

            if (hud != _hud)
            {
                
                _hud = hud;
                Applied.Clear();
                Wanted.Clear();
            }

            if (!IsEnabled)
            {
                if (Applied.Count == 0) return;
                UnmountAll();      
                return;
            }

            Collect(hud, Wanted);
            if (SameLayout(Applied, Wanted)) return;

            Rebuild();
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[KeyGuide] 挂载角标失败: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsEnabled
    {
        get
        {
            try
            {
                var entry = AmethystConfig.ShowKeyGuide;
                return entry == null || entry.Value;
            }
            catch
            {
                return true;
            }
        }
    }

    private static void Collect(HudManager hud, List<Slot> into)
    {
        into.Clear();
        Add(into, hud.KillButton, ActionKill, small: false);
        Add(into, hud.UseButton, ActionUseAndPet, small: false);
        Add(into, hud.PetButton, ActionUseAndPet, small: false);
        Add(into, hud.SabotageButton, ActionMapAndSabotage, small: false);
        Add(into, hud.ImpostorVentButton, ActionVent, small: false);
        Add(into, hud.ReportButton, ActionReport, small: false);
        Add(into, hud.MapButton, ActionMapAndSabotage, small: true);   
        Add(into, hud.AbilityButton, ActionAbility, small: false);     
    }

    private static void Add(List<Slot> into, Component button, int actionId, bool small)
    {
        if (button == null) return;             
        GameObject go = button.gameObject;
        if (go == null) return;

        into.Add(new Slot { Button = go, Key = ResolveKeyCode(actionId), Small = small });
    }

    private static KeyCode ResolveKeyCode(int actionId)
    {
        try
        {
            KeyboardMap map = ReInput.mapping.GetKeyboardMapInstanceSavedOrDefault(0, 0, 0);
            if (map == null) return KeyCode.None;

            Il2CppReferenceArray<ActionElementMap> buttonMaps = map.GetButtonMapsWithAction(actionId);
            if (buttonMaps == null || buttonMaps.Length == 0) return KeyCode.None;

            ActionElementMap first = buttonMaps[0];
            return first == null ? KeyCode.None : first.keyCode;
        }
        catch (Exception ex)
        {
            if (!_reportedRewiredFailure)
            {
                _reportedRewiredFailure = true;
                AmethystPlugin.Logger?.LogWarning(
                    $"[KeyGuide] 读取 Rewired 键位失败，本次不画角标: {ex.GetType().Name}: {ex.Message}");
            }
            return KeyCode.None;
        }
    }

    private static bool SameLayout(List<Slot> applied, List<Slot> wanted)
    {
        if (applied.Count != wanted.Count) return false;

        for (int i = 0; i < applied.Count; i++)
        {
            if (applied[i].Button != wanted[i].Button) return false;
            if (applied[i].Key != wanted[i].Key) return false;
            if (applied[i].Small != wanted[i].Small) return false;
        }

        return true;
    }

    private static void Rebuild()
    {
        UnmountAll();

        Sprite background = null;
        int mounted = 0;

        for (int i = 0; i < Wanted.Count; i++)
        {
            Slot slot = Wanted[i];
            if (slot.Button == null) continue;

            if (!Glyphs.TryGetValue(slot.Key, out Glyph glyph))
            {
                
                if (slot.Key != KeyCode.None) ReportUnsupported(slot.Key);
                continue;
            }

            if (background == null) background = LoadSheet(SheetBackground, BackgroundCell, BackgroundCell)?[0];

            if (background == null)
            {
                ReportMissingBackground();
                continue;
            }

            Sprite glyphSprite = GlyphSprite(glyph);
            if (glyphSprite == null) continue;

            if (Mount(slot, background, glyphSprite)) mounted++;
        }

        Applied.Clear();
        Applied.AddRange(Wanted);

        AmethystPlugin.Logger?.LogInfo($"[KeyGuide] 角标已重建: {mounted}/{Wanted.Count} 个按钮");
    }

    private static void UnmountAll()
    {
        for (int i = 0; i < Applied.Count; i++)
            RemoveGuides(Applied[i].Button);

        Applied.Clear();
    }

    private static void RemoveGuides(GameObject button)
    {
        if (button == null) return;

        try
        {
            Transform root = button.transform;
            if (root == null) return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null || child.name != GuideChildName) continue;

                GameObject go = child.gameObject;
                
                if (go != null) go.SetActive(false);
                UnityEngine.Object.Destroy(go);
            }
        }
        catch { }
    }

    private static bool Mount(Slot slot, Sprite background, Sprite glyph)
    {
        GameObject button = slot.Button;
        if (button == null) return false;

        try
        {
            RemoveGuides(button);   

            Vector2 offset = slot.Small ? SmallButtonOffset : ButtonOffset;
            float scale = slot.Small ? SmallButtonScale : ButtonScale;

            var guide = new GameObject(GuideChildName);
            guide.transform.SetParent(button.transform, false);
            guide.layer = button.layer;                                    
            guide.transform.localPosition = new Vector3(offset.x, offset.y, -10f);
            guide.transform.localScale = new Vector3(scale, scale, 1f);

            var backRenderer = guide.AddComponent<SpriteRenderer>();
            backRenderer.sprite = background;                              

            var text = new GameObject(TextChildName);
            text.transform.SetParent(guide.transform, false);
            text.layer = button.layer;
            var textRenderer = text.AddComponent<SpriteRenderer>();
            textRenderer.sprite = glyph;
            text.transform.localPosition = new Vector3(0f, 0f, -1f);

            return true;
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[KeyGuide] 挂载角标失败: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static Sprite[] LoadSheet(string name, int cellWidth, int cellHeight)
    {
        if (SheetCache.TryGetValue(name, out Sprite[] cached) && cached != null) return cached;

        Sprite[] sprites = AmethystExternalResources.CreateSlicedSprites(
            "KeyGuide/" + name + ".png",
            "Amethyst.Resources.KeyGuide." + name + ".png",
            cellWidth, cellHeight);

        if (sprites != null) SheetCache[name] = sprites;
        return sprites;
    }

    private static Sprite GlyphSprite(Glyph glyph)
    {
        Sprite[] sheet = LoadSheet(glyph.Sheet, GlyphWidth, GlyphHeight);
        if (sheet == null || glyph.Index < 0 || glyph.Index >= sheet.Length) return null;
        return sheet[glyph.Index];
    }

    private static void ReportUnsupported(KeyCode key)
    {
        if (!ReportedUnsupported.Add(key)) return;
        AmethystPlugin.Logger?.LogInfo($"[KeyGuide] 字形表里没有这个键，不画角标: {key}");
    }

    private static void ReportMissingBackground()
    {
        if (_reportedMissingBackground) return;
        _reportedMissingBackground = true;
        AmethystPlugin.Logger?.LogWarning(
            $"[KeyGuide] 底片图集缺失（{SheetBackground}），本次不画角标: 外部目录与内置副本都没读到");
    }
}
