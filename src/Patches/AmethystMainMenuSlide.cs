using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystMainMenuSlide
{
    private enum PanelState { Hidden, Showing, Visible, Hiding }

    private static GameObject _panel;
    private static Vector3 _shownPos;
    private static bool _captured;
    private static PanelState _state = PanelState.Hidden;

    private static float _lastReplay;

    private static string _lastTrigger;

    private const float HiddenOffsetX = 10f;

    private const float Speed = 12f;

    private static Vector3 HiddenPos => _shownPos + new Vector3(HiddenOffsetX, 0f, 0f);

    internal static GameObject Panel => _panel;

    internal static bool Captured => _captured && _panel != null;

    internal static void Capture(MainMenuManager menu)
    {
        try
        {
            if (menu == null) return;
            if (menu.gameModeButtons == null || menu.gameModeButtons.transform == null) return;

            Transform parent = menu.gameModeButtons.transform.parent;
            if (parent == null) return;

            _panel = parent.gameObject;
            if (_panel == null) return;

            _shownPos = _panel.transform.localPosition;
            _captured = true;
            _state = PanelState.Hidden;
            _panel.transform.localPosition = HiddenPos;
        }
        catch { }
    }

    internal static void Animate()
    {
        try
        {
            if (!_captured || _panel == null) return;
            if (!_panel.activeInHierarchy) return;

            Vector3 target;
            if (_state == PanelState.Showing) target = _shownPos;
            else if (_state == PanelState.Hiding) target = HiddenPos;
            else return;

            Transform t = _panel.transform;

            float k = Mathf.Clamp(Time.deltaTime * Speed, 0f, 1f);
            t.localPosition = Vector3.Lerp(t.localPosition, target, k);

            if (Vector3.Distance(t.localPosition, target) < 0.02f)
            {
                t.localPosition = target;
                _state = _state == PanelState.Showing ? PanelState.Visible : PanelState.Hidden;
            }
        }
        catch { }
    }

    internal static void EnsureShownNow()
    {
        try
        {
            if (!_captured || _panel == null) return;
            _panel.transform.localPosition = _shownPos;
            _state = PanelState.Visible;
        }
        catch { }
    }

    internal static void Show(string trigger)
    {
        try
        {
            if (!_captured || _panel == null) return;

            bool sameAsLast = !string.IsNullOrEmpty(trigger) && trigger == _lastTrigger;

            if (_state == PanelState.Visible)
            {
                
                if (sameAsLast) return;
                if (Time.realtimeSinceStartup - _lastReplay < 0.4f) return;
                _lastReplay = Time.realtimeSinceStartup;
                _lastTrigger = trigger;

                _panel.transform.localPosition = HiddenPos;
                _state = PanelState.Showing;
                return;
            }

            _lastTrigger = trigger;
            _state = PanelState.Showing;
        }
        catch { }
    }

    internal static void Hide()
    {
        try
        {
            if (!_captured || _panel == null) return;
            if (_state == PanelState.Hidden) return;
            _state = PanelState.Hiding;
        }
        catch { }
    }

    internal static void CloseAccountWindow()
    {
        try
        {
            AccountManager.Instance?.transform
                ?.FindChild("AccountTab/AccountWindow")?.gameObject.SetActive(false);
        }
        catch { }
    }

    internal static void Reset()
    {
        _captured = false;
        _panel = null;
        _state = PanelState.Hidden;
        _lastTrigger = null;
    }
}

[HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class AmethystMainMenuSlideStartPatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        AmethystMainMenuSlide.Reset();
        AmethystMainMenuSlide.Capture(__instance);
    }
}

[HarmonyPatch(typeof(MainMenuManager), "LateUpdate")]
internal static class AmethystMainMenuSlideUpdatePatch
{
    private static void Postfix(MainMenuManager __instance)
    {
        if (!AmethystMainMenuSlide.Captured)
            AmethystMainMenuSlide.Capture(__instance);

        AmethystMainMenuSlide.Animate();
    }
}

[HarmonyPatch(typeof(MainMenuManager), "OpenGameModeMenu")]
internal static class AmethystMainMenuSlideGameModePatch
{
    private static void Prefix() => AmethystMainMenuSlide.Show("gamemode");
}

[HarmonyPatch(typeof(MainMenuManager), "OpenAccountMenu")]
internal static class AmethystMainMenuSlideAccountPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Show("account");
}

[HarmonyPatch(typeof(MainMenuManager), "OpenCredits")]
internal static class AmethystMainMenuSlideCreditsPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Show("credits");
}

[HarmonyPatch(typeof(OptionsMenuBehaviour), "Open")]
[HarmonyPatch(typeof(AnnouncementPopUp), "Show")]
[HarmonyPriority(Priority.Last)]
internal static class AmethystCloseAccountWindowPatch
{
    private static void Prefix() => AmethystMainMenuSlide.CloseAccountWindow();
}

[HarmonyPatch(typeof(OptionsMenuBehaviour), "Open")]
internal static class AmethystMainMenuSlideSettingsPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Hide();
}

[HarmonyPatch(typeof(AnnouncementPopUp), "Show")]
internal static class AmethystMainMenuSlideAnnouncementPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Hide();
}

[HarmonyPatch(typeof(MainMenuManager), "TransitionToInventory")]
internal static class AmethystMainMenuSlideInventoryPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Hide();
}

[HarmonyPatch(typeof(MainMenuManager), "TransitionToShop")]
internal static class AmethystMainMenuSlideShopPatch
{
    private static void Prefix() => AmethystMainMenuSlide.Hide();
}
