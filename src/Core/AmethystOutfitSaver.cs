using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AmongUs.Data;
using BepInEx;
using UnityEngine;

namespace Amethyst;

internal sealed class AmethystOutfitData
{
    public string HatId = HatData.EmptyId;
    public string PetId = PetData.EmptyId;
    public string SkinId = SkinData.EmptyId;
    public string VisorId = VisorData.EmptyId;
    public string NamePlateId = NamePlateData.EmptyId;

    internal NetworkedPlayerInfo.PlayerOutfit ToPlayerOutfit()
    {
        return new NetworkedPlayerInfo.PlayerOutfit
        {
            HatId = HatId,
            PetId = PetId,
            SkinId = SkinId,
            VisorId = VisorId,
            NamePlateId = NamePlateId,
            ColorId = DataManager.Player != null && DataManager.Player.Customization != null ? DataManager.Player.Customization.Color : 0
        };
    }
}

internal sealed class AmethystOutfitStore
{
    public int Selected = 0;
    public List<AmethystOutfitData> Presets = new List<AmethystOutfitData>();
}

public sealed class AmethystOutfitSaver : MonoBehaviour
{
    private readonly List<AmethystOutfitData> _presets = new List<AmethystOutfitData>();
    private int _selected;
    private bool _loaded;
    private bool _subscribed;
    private bool _ignoreChange;
    private float _cooldown;
    private System.Action _saveHandler;

    private string FilePath => Path.Combine(Paths.ConfigPath, "Amethyst", "outfits.json");

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal List<AmethystOutfitData> Presets
    {
        get { EnsureLoaded(); return _presets; }
    }

    internal int SelectedPreset
    {
        get { EnsureLoaded(); return _selected; }
        set { EnsureLoaded(); _selected = Mathf.Clamp(value, 0, _presets.Count - 1); SaveFile(); }
    }

    internal bool CanInteract() => _cooldown <= 0f;

    private void Awake()
    {
        _instance = this;
        Load();
    }

    private void Start()
    {
        TrySubscribe();
    }

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_OutfitSaver);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_OutfitSaver); }
    }

    public void UpdateInner()
    {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
            if (!_subscribed) TrySubscribe();
    
    }

    private static AmongUs.Data.Player.PlayerCustomizationData Customization
    {
        get
        {
            try { return DataManager.Player != null ? DataManager.Player.Customization : null; }
            catch { return null; }
        }
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;
        AmongUs.Data.Player.PlayerCustomizationData cust = Customization;
        if (cust == null) return;
        try
        {
            _saveHandler = SaveToSelected;
            cust.OnHatChanged += _saveHandler;
            cust.OnPetChanged += _saveHandler;
            cust.OnSkinChanged += _saveHandler;
            cust.OnVisorChanged += _saveHandler;
            cust.OnNamePlateChanged += _saveHandler;
            _subscribed = true;
            FindCurrentPreset();
            SaveToSelected();
        }
        catch { }
    }

    private void SaveToSelected()
    {
        if (_ignoreChange) return;
        AmongUs.Data.Player.PlayerCustomizationData cust = Customization;
        if (cust == null) return;
        try
        {
            EnsureLoaded();
            AmethystOutfitData d = _presets[_selected];
            d.HatId = Norm(cust.Hat);
            d.PetId = Norm(cust.Pet);
            d.SkinId = Norm(cust.Skin);
            d.VisorId = Norm(cust.Visor);
            d.NamePlateId = Norm(cust.NamePlate);
            SaveFile();
        }
        catch { }
    }

    private static string Norm(string id) => string.IsNullOrEmpty(id) ? HatData.EmptyId : id;

    private static string NormField(string id, string emptyId)
    {
        if (string.IsNullOrEmpty(id)) return emptyId;
        if (id.StartsWith("hat_", StringComparison.OrdinalIgnoreCase)) return emptyId;
        return id;
    }

    private void FindCurrentPreset()
    {
        AmongUs.Data.Player.PlayerCustomizationData cust = Customization;
        if (cust == null) return;
        EnsureLoaded();
        for (int i = 0; i < _presets.Count; i++)
        {
            var d = _presets[i];
            if (d.HatId == cust.Hat && d.PetId == cust.Pet && d.SkinId == cust.Skin
                && d.VisorId == cust.Visor && d.NamePlateId == cust.NamePlate)
            {
                _selected = i;
                return;
            }
        }
        _selected = 0;
    }

    internal void ApplyPresetFromUI(int index)
    {
        if (_cooldown > 0f) return;
        _cooldown = 0.5f;
        EnsureLoaded();
        _selected = Mathf.Clamp(index, 0, _presets.Count - 1);

        AmethystOutfitData data = _presets[_selected];
        ValidateOutfit(data);

        AmongUs.Data.Player.PlayerCustomizationData cust = Customization;
        if (cust != null)
        {
            _ignoreChange = true;
            try
            {
                cust.Hat = data.HatId;
                cust.Pet = data.PetId;
                cust.Skin = data.SkinId;
                cust.Visor = data.VisorId;
                cust.NamePlate = data.NamePlateId;
            }
            catch { }
            _ignoreChange = false;
        }

        PlayerControl lp = PlayerControl.LocalPlayer;
        if (lp != null)
        {
            try
            {
                lp.RpcSetHat(data.HatId);
                lp.RpcSetPet(data.PetId);
                lp.RpcSetSkin(data.SkinId);
                lp.RpcSetVisor(data.VisorId);
                lp.RpcSetNamePlate(data.NamePlateId);
            }
            catch { }
        }

        SaveFile();
    }

    private static void ValidateOutfit(AmethystOutfitData d)
    {
        try
        {
            HatManager hm = HatManager.Instance;
            if (hm == null) return;
            if (!hm.GetUnlockedHats().Any(item => item.ProductId == d.HatId)) d.HatId = HatData.EmptyId;
            if (!hm.GetUnlockedPets().Any(item => item.ProductId == d.PetId)) d.PetId = PetData.EmptyId;
            if (!hm.GetUnlockedSkins().Any(item => item.ProductId == d.SkinId)) d.SkinId = SkinData.EmptyId;
            if (!hm.GetUnlockedVisors().Any(item => item.ProductId == d.VisorId)) d.VisorId = VisorData.EmptyId;
            if (!hm.GetUnlockedNamePlates().Any(item => item.ProductId == d.NamePlateId)) d.NamePlateId = NamePlateData.EmptyId;
        }
        catch { }
    }

    private void EnsureLoaded()
    {
        if (!_loaded) Load();
    }

    private void Load()
    {
        _loaded = true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                try
                {
                    var store = JsonSerializer.Deserialize<AmethystOutfitStore>(json, new JsonSerializerOptions { IncludeFields = true });
                    if (store?.Presets != null)
                    {
                        _selected = Mathf.Clamp(store.Selected, 0, 5);
                        foreach (var d in store.Presets)
                        {
                            AmethystOutfitData newItem = new AmethystOutfitData();
                            newItem.HatId = Norm(d.HatId);
                            newItem.PetId = NormField(d.PetId, PetData.EmptyId);
                            newItem.SkinId = NormField(d.SkinId, SkinData.EmptyId);
                            newItem.VisorId = NormField(d.VisorId, VisorData.EmptyId);
                            newItem.NamePlateId = NormField(d.NamePlateId, NamePlateData.EmptyId);
                            _presets.Add(newItem);
                        }
                    }
                }
                catch
                {
                    var loaded = JsonSerializer.Deserialize<List<AmethystOutfitData>>(json, new JsonSerializerOptions { IncludeFields = true });
                    if (loaded != null)
                    {
                        foreach (var d in loaded)
                        {
                            AmethystOutfitData newItem = new AmethystOutfitData();
                            newItem.HatId = Norm(d.HatId);
                            newItem.PetId = NormField(d.PetId, PetData.EmptyId);
                            newItem.SkinId = NormField(d.SkinId, SkinData.EmptyId);
                            newItem.VisorId = NormField(d.VisorId, VisorData.EmptyId);
                            newItem.NamePlateId = NormField(d.NamePlateId, NamePlateData.EmptyId);
                            _presets.Add(newItem);
                        }
                    }
                }
            }
        }
        catch { }
        while (_presets.Count < 6) _presets.Add(new AmethystOutfitData());
        if (_presets.Count > 6) _presets.RemoveRange(6, _presets.Count - 6);
    }

    private void SaveFile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var store = new AmethystOutfitStore
            {
                Selected = Mathf.Clamp(_selected, 0, Math.Max(0, _presets.Count - 1)),
                Presets = _presets
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        }
        catch { }
    }

    internal static AmethystOutfitSaver Instance
    {
        get
        {
            if (_instance != null) return _instance;

            float t = Time.realtimeSinceStartup;
            if (t < _nextProbeAt) return null;
            _nextProbeAt = t + 1f;

            try { _instance = GameObject.FindObjectOfType<AmethystOutfitSaver>(); }
            catch { _instance = null; }
            return _instance;
        }
    }

    private static AmethystOutfitSaver _instance;
    private static float _nextProbeAt = -1f;
}
