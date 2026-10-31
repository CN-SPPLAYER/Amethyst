using Amethyst.Patches;
using InnerNet;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystHud : MonoBehaviour
{
    
    private float _mainMenuNext;
    private float _perfNext;   
    
    private float _recapNext;
    
    private float _recapSyncNext;
    
    private float _keyGuideNext;

    public void Update()
    {
        int probe = AmethystPerfProbe.Begin(Section.S_Hud);
        try { UpdateInner(); }
        finally { AmethystPerfProbe.End(probe, Section.S_Hud); }

        try { AmethystPerfProbe.Flush(); } catch { }
    }

    private void UpdateInner()
    {
        
        try { AmethystUpdateScheduler.OnTick(); } catch { }

        try { Patches.AmethystAccountUnlock.Tick(); } catch { }

        try { AmethystPlayerCacheStore.Tick(); } catch { }

        try
        {
            if (AmethystConfig.BetterPingDisplay != null && AmethystConfig.BetterPingDisplay.Value)
                AmethystBetterPingState.Tick();
        }
        catch { }

        if (AmethystConfig.CopyCodeKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.CopyCodeKey.Value))
            CopyLobbyCode();

        if (Time.unscaledTime >= _perfNext)
        {
            _perfNext = Time.unscaledTime + 0.1f;

            if (AmethystUpdateScheduler.ShouldRun(Slot.Notifier)) { try { AmethystNotifierLayer.Apply(); } catch { } }
            
            try { Patches.ModStampState.Tick(); } catch { }

            try { Patches.AmethystAbilityButtonScale.Tick(); } catch { }

            if (Time.unscaledTime >= _keyGuideNext)
            {
                _keyGuideNext = Time.unscaledTime + 0.5f;
                try { AmethystKeyGuide.Tick(); } catch (System.Exception e) { AmethystPlugin.Logger?.LogWarning("[KeyGuide] " + e.Message); }
            }

            if (AmethystUpdateScheduler.ShouldRun(Slot.ExileName)) { try { AmethystExileName.Tick(); } catch { } }
            if (AmethystUpdateScheduler.ShouldRun(Slot.MeetingIntro)) { try { AmethystMeetingIntroNameColor.Tick(); } catch { } }

            try { AmethystIntroRoleColor.Tick(); } catch { }

            if (AmethystUpdateScheduler.ShouldRun(Slot.Console)) { try { AmethystConsoleHider.Tick(); } catch { } }

            if (AmethystUpdateScheduler.ShouldRun(Slot.RolePanel))
            {
                try
                {
                    Amethyst.Patches.AmethystRolePanel.Enabled = AmethystConfig.RolePanel == null || AmethystConfig.RolePanel.Value;
                    Amethyst.Patches.AmethystRolePanel.ShowDeadRoles = AmethystConfig.ShowRolesWhenDead != null && AmethystConfig.ShowRolesWhenDead.Value;
                    Amethyst.Patches.AmethystRolePanel.ShowTeamRoles = AmethystConfig.ShowImpostorTeammateRole != null && AmethystConfig.ShowImpostorTeammateRole.Value;
                    Amethyst.Patches.AmethystRolePanel.Tick();
                }
                catch { }
            }
        }

        if (Time.unscaledTime >= _mainMenuNext)
        {
            _mainMenuNext = Time.unscaledTime + 0.5f;
            try { AmethystMainMenuOptimize.TickLive(); } catch { }
            try { Patches.AmethystMainMenuButtons.TickLive(); } catch { }
            try { Patches.AmethystUiTheme.TickLive(); } catch { }
        }

        try
        {
            if (!AmethystRecap.CanUse && AmethystRecapWindow.IsOpen)
                AmethystRecapWindow.Hide();
        }
        catch { }

        try { AmethystRecapButton.Tick(); } catch { }
        try { Patches.AmethystTopRightButtonGroup.Tick(); } catch { }

        try
        {
            if (AmethystConfig.RecapKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.RecapKey.Value))
            {
                if (AmethystRecap.CanUse)
                    AmethystRecapWindow.ToggleVisible();
            }
        }
        catch { }

        if (Time.unscaledTime >= _recapSyncNext)
        {
            _recapSyncNext = Time.unscaledTime + 0.1f;
            try
            {
                AmongUsClient net = AmongUsClient.Instance;
                if (net != null && net.IsGameStarted && LobbyBehaviour.Instance == null)
                    AmethystRecap.Sync();
            }
            catch { }
        }

        if (Time.unscaledTime >= _recapNext)
        {
            _recapNext = Time.unscaledTime + 0.5f;
            try
            {
                if (AmethystRecap.TryCaptureEndGame())
                {
                    AmethystRecap.Sync();

                }
            }
            catch { }
        }

        HudManager hud = HudManager.Instance;
        if (hud != null)
        {
            try { AmethystCooldownColorState.Tick(hud); } catch { }
            try { AmethystAlwaysChatState.Tick(hud); } catch { }
            
            try { AmethystMeetingTaskPanel.Apply(hud); } catch { }
        }
    }

    public void LateUpdate()
    {
        try { Patches.AmethystTopRightButtonGroup.LateTick(); } catch { }

        if (AmethystCompat.OtherFps) return;
        if (AmethystConfig.UnlockFps240 == null || !AmethystConfig.UnlockFps240.Value) return;
        int cap = 60;
        try { if (AmethystConfig.FpsLimit != null) cap = Mathf.Clamp(AmethystConfig.FpsLimit.Value, 60, 240); } catch { }
        if (QualitySettings.vSyncCount != 0 || Application.targetFrameRate != cap)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = cap;
        }
    }

    private static void CopyLobbyCode()
    {
        Menu.AmethystLobbyCopyHelper.CopyRoomInfo();
    }
}
