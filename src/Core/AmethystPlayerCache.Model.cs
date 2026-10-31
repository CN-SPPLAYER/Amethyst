using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using TMPro;
using UnityEngine;

namespace Amethyst;

internal sealed class AmethystPlayerCache
{
    internal PlayerControl Pc;
    internal byte Id;

    internal string Name = string.Empty;
    internal int ColorId = -1;          
    internal int ShownColorId = -1;     
    internal bool Dead;
    internal bool IsImpostor;
    internal bool IsShapeshifting;
    internal bool InVent;
    internal bool OutfitIsDefault = true;
    internal int KillCount;
    internal int TasksDone;
    internal int TasksTotal;

    internal TextMeshPro NameText;
    internal TextMeshPro ColorBlindText;

    internal string PendingNameText;
    internal int PendingVertexColorId = int.MinValue;

    internal bool Dirty;

    internal int DirtyAttempts;

    internal int PendingColorBlindId = int.MinValue;

    internal float LineExtra;

    internal string AliveText = string.Empty;
    internal string DeadText = string.Empty;

    internal string MeetingText = string.Empty;

    internal string WrittenNameText;
    internal int WrittenVertexColorId = int.MinValue;
    internal int WrittenColorBlindId = int.MinValue;

    internal int AppliedBodyWrapId = int.MinValue;

    internal TextMeshPro WrittenForName;
    internal TextMeshPro WrittenForColorBlind;

    internal int DataMissingAttempts;
}
