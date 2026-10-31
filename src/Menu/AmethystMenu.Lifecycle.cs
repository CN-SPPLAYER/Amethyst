using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using Amethyst.Patches;
using UnityEngine;

namespace Amethyst;

public sealed partial class AmethystMenu
{
    private void Open()
    {
        
        bool resume = _open;
        if (!resume)
        {
            _ax = 0f;
            _av = 0f;
            _axFrame = -1;
            try { AmethystPlugin.Logger?.LogInfo($"[Menu] open prewarmed={_prewarmed}"); } catch { }
        }
        _open = true;
        _closeAt = -1f;
        
        _openAt = resume ? -1f : Time.unscaledTime;
        _enterAge = 0f;
        _openDraws = 0;
        _enterClock = 0f;
        _tabClock = 0f;
        _titleClock = 0f;
        _titleAt = Time.unscaledTime;
        
        _tabAnimAt = _openAt;
        _tabLag = 0.12f;
        _roomConfirm = string.Empty;
        _mobileCentered = false;
        try { Input.simulateMouseWithTouches = true; } catch { }
        SyncRoomListVisibility();
    }

    private void RequestClose()
    {
        if (_open && _closeAt < 0f) _closeAt = Time.unscaledTime;
        SyncRoomListVisibility();
    }

    private void SetTab(int nt)
    {
        if (nt == _tab) return;
        _tab = nt;
        _tabAnimAt = Time.unscaledTime;
        _tabLag = 0f;
        _tabClock = 0f;
        _titleClock = 0f;
        _scroll = 0f;
        _scrollPending = 0f;
        _scrollGoal = 0f;
        _roomConfirm = string.Empty;
        SyncRoomListVisibility();
    }
}
