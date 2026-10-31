using System;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystColorSnipe : MonoBehaviour
{
    public AmethystColorSnipe(IntPtr ptr) : base(ptr) { }

    private float _next;
    private int _petApplied = -1;

    internal static bool On => AmethystConfig.ColorSnipe != null && AmethystConfig.ColorSnipe.Value;

    internal static int Max()
    {
        try { if (Palette.PlayerColors != null) return Mathf.Max(0, Palette.PlayerColors.Length - 1); }
        catch { }
        return 17;
    }

    private static bool Taken(int id, PlayerControl self)
    {
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl p = e.Current;
                if (p == null || p == self || p.Data == null || p.Data.Disconnected) continue;
                if (p.Data.DefaultOutfit != null && p.Data.DefaultOutfit.ColorId == id) return true;
            }
        }
        catch { }
        return false;
    }

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_Other);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_Other); }
    }

    public void UpdateInner()
    {
            if (!On || LobbyBehaviour.Instance == null) return;

            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || me.Data == null || me.Data.DefaultOutfit == null) return;
            if (Time.time < _next) return;
            _next = Time.time + 0.25f;

            int want = Mathf.Clamp(AmethystConfig.ColorSnipeId != null ? AmethystConfig.ColorSnipeId.Value : 0, 0, Max());
            if (Taken(want, me)) return;

            if (me.Data.DefaultOutfit.ColorId != want)
            {
                try { me.CmdCheckColor((byte)want); } catch { }
                _petApplied = -1;
            }

            if (_petApplied != want)
            {
                try
                {
                    PetBehaviour pet = me.GetPet();
                    if (pet != null)
                    {
                        pet.SetCrewmateColor(want);
                        _petApplied = want;
                    }
                }
                catch { }
            }
    }
}
