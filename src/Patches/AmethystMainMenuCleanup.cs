using System.Globalization;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(VersionShower), "Start")]
internal static class AmethystVersionShowerPatch
{
    private static void Postfix(VersionShower __instance)
    {
        AmethystVersionLine.Bind(__instance);
    }
}

[HarmonyPatch(typeof(MainMenuManager), "LateUpdate")]
internal static class AmethystVersionLineUpdatePatch
{
    private static float _next;

    private static void Postfix()
    {
        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + 1f;
        AmethystVersionLine.Refresh();
    }
}

internal static class AmethystVersionLine
{
    private static TMP_Text _text;
    private static string _applied;
    private static bool _logged;

    internal static void Bind(VersionShower shower)
    {
        try
        {
            if (shower == null) return;
            TMP_Text text = shower.text;
            if (text == null) return;

            _text = text;
            _applied = null;

            if (!_logged)
            {
                _logged = true;
                
                try { AmethystPlugin.Logger?.LogInfo($"[VersionLine] native=\"{text.text}\""); } catch { }
            }

            Apply();
        }
        catch { }
    }

    internal static void Refresh()
    {
        if (_text == null) return;
        Apply();
    }

    private static void Apply()
    {
        try
        {
            TMP_Text text = _text;
            if (text == null) return;

            string line = Build();
            if (string.IsNullOrEmpty(line)) return; 
            if (_applied == line || text.text == line) { _applied = line; return; }

            text.text = line;
            text.ForceMeshUpdate();
            _applied = line;
            AmethystPlugin.Logger?.LogInfo($"[VersionLine] show=\"{line}\"");
        }
        catch { }
    }

    private static string Build()
    {
        string platform = Platform();
        string version = Version();
        string date = Date();

        var sb = new StringBuilder(48);
        if (!string.IsNullOrEmpty(platform)) sb.Append(platform);
        if (!string.IsNullOrEmpty(version))
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(version);
        }
        
        if (!string.IsNullOrEmpty(date) && !string.Equals(date, version, System.StringComparison.OrdinalIgnoreCase))
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(date);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static string Platform()
    {
        try
        {
            try
            {
                Platforms p = Platforms.Unknown;
                var pd = Constants.GetPlatformData();
                if (pd != null) p = pd.Platform;
                else p = Constants.GetPlatformType();

                string name = MapPlatform(p);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch { }

            try { string s = Constants.GetCurrentPlatformName(); if (NameOk(s)) return s.Trim(); } catch { }
            try { string s = Constants.GetPlatformName(); if (NameOk(s)) return s.Trim(); } catch { }
            try { string s = Constants.GetPurchasingPlatformType(); if (NameOk(s)) return s.Trim(); } catch { }
            return null;
        }
        catch { return null; }
    }

    private static bool NameOk(string s)
        => !string.IsNullOrWhiteSpace(s)
           && !s.Trim().Equals("TESTNAME", System.StringComparison.OrdinalIgnoreCase);

    private static string MapPlatform(Platforms platform)
    {
        return platform switch
        {
            Platforms.StandaloneSteamPC => "Steam",
            Platforms.StandaloneEpicPC => "Epic Games",
            Platforms.StandaloneWin10 => "Microsoft Store",
            Platforms.StandaloneMac => "Mac OS",
            Platforms.StandaloneItch => "Itch.io",
            Platforms.Xbox => "Xbox",
            Platforms.Playstation => "Playstation",
            Platforms.Switch => "Switch",
            Platforms.Android => "Android",
            Platforms.IPhone => "IPhone",
            (Platforms)112 => "Starlight", 
            _ => null,
        };
    }

    private static string Version()
    {
        try
        {
            try
            {
                var rd = ReferenceDataManager.Instance?.Refdata;
                string v = rd?.userFacingVersion;
                if (!string.IsNullOrWhiteSpace(v))
                {
                    v = v.Trim();
                    return (v[0] == 'v' || v[0] == 'V') ? v : "v" + v;
                }
            }
            catch { }

            try
            {
                string v = Application.version;
                if (!string.IsNullOrWhiteSpace(v))
                {
                    v = v.Trim();
                    if (!LooksLikeDate(v)) return StripBuildSuffix(v);
                }
            }
            catch { }

            return null;
        }
        catch { return null; }
    }

    private static string StripBuildSuffix(string v)
    {
        int end = v.Length;
        while (end > 1 && !char.IsDigit(v[end - 1])) end--;
        return end == 0 ? null : v.Substring(0, end);
    }

    private static string Date()
    {
        try
        {
            try
            {
                string v = Application.version;
                if (!string.IsNullOrWhiteSpace(v) && LooksLikeDate(v.Trim())) return v.Trim();
            }
            catch { }

            try
            {
                int broadcast = Constants.GetBroadcastVersion();
                var t = Constants.GetVersionComponents(broadcast);

                int year = t.Item1, month = t.Item2, day = t.Item3;
                if (year < 2000 || year > 2100) return null;
                if (month < 1 || month > 12) return null;
                if (day < 1 || day > 31) return null;

                return year.ToString(CultureInfo.InvariantCulture) + "."
                     + month.ToString(CultureInfo.InvariantCulture) + "."
                     + day.ToString(CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
        catch { return null; }
    }

    private static bool LooksLikeDate(string s)
    {
        try
        {
            string[] parts = s.Split('.');
            if (parts.Length != 3) return false;
            if (parts[0].Length != 4) return false;
            if (parts[1].Length < 1 || parts[1].Length > 2) return false;
            if (parts[2].Length < 1 || parts[2].Length > 2) return false;

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < parts[i].Length; j++)
                    if (!char.IsDigit(parts[i][j])) return false;

            int month = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int day = int.Parse(parts[2], CultureInfo.InvariantCulture);
            return month >= 1 && month <= 12 && day >= 1 && day <= 31;
        }
        catch { return false; }
    }
}

internal static class AmethystVersionTag
{
    internal const string TagText = "Amethyst\u00AE2026";

    internal static void Tick() { }
}
