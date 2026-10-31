using System;

namespace Amethyst;

internal static class AmethystLanguage
{
    internal const string Auto = "auto";

    internal static string Detect()
    {
        try
        {
            TranslationController tc = TranslationController.Instance;
            if (tc == null) return string.Empty;

            LanguageUnit unit = tc.currentLanguage;
            if (unit == null) return string.Empty;

            return Map(unit.languageID);
        }
        catch { return string.Empty; }
    }

    internal static bool ControllerReady()
    {
        try { return TranslationController.Instance != null; }
        catch { return false; }
    }

    private static string Map(SupportedLangs lang)
    {
        switch (lang)
        {
            case SupportedLangs.SChinese:
            case SupportedLangs.TChinese:
                return "zh";

            case SupportedLangs.Russian:
                return "ru";

            default:
                return "en";
        }
    }
}
