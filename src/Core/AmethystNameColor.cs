using System.Text;

namespace Amethyst;

internal static class AmethystNameColor
{
    private static readonly StringBuilder StripBuilder = new StringBuilder(64);

    internal static string Strip(string value)
    {
        if (string.IsNullOrEmpty(value) || (value.IndexOf('<') < 0 && value.IndexOf('&') < 0))
            return value ?? string.Empty;

        StripBuilder.Length = 0;
        int i = 0;
        while (i < value.Length)
        {
            char ch = value[i];
            if (ch == '<')
            {
                int close = value.IndexOf('>', i + 1);
                if (close >= 0) { i = close + 1; continue; }
            }
            StripBuilder.Append(ch);
            i++;
        }
        StripBuilder.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        return StripBuilder.ToString();
    }
}