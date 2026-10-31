using System;
using System.Text.Json;

namespace Amethyst;

internal static class AmethystRelease
{
    internal const string ApiUrl = "https://api.github.com/repos/CN-SPPLAYER/Amethyst/releases/latest";

    internal struct Info
    {
        internal string Tag;        
        internal string Name;       
        internal string Version;    
        internal string AssetUrl;   
        internal string AssetApiUrl; 
        internal string AssetName;  
        internal string Digest;     
    }

    internal static bool TryParse(string json, out Info info)
    {
        info = default;
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return false;

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;

                info.Tag = root.TryGetProperty("tag_name", out JsonElement tag)
                    ? (tag.GetString() ?? string.Empty) : string.Empty;
                info.Name = root.TryGetProperty("name", out JsonElement name)
                    ? (name.GetString() ?? string.Empty) : string.Empty;

                info.Version = NormalizeVersion(info.Tag);
                if (string.IsNullOrEmpty(info.Version)) return false;

                if (root.TryGetProperty("assets", out JsonElement assets) &&
                    assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement a in assets.EnumerateArray())
                    {
                        if (a.ValueKind != JsonValueKind.Object) continue;

                        string an = a.TryGetProperty("name", out JsonElement n) ? (n.GetString() ?? string.Empty) : string.Empty;
                        string au = a.TryGetProperty("browser_download_url", out JsonElement u) ? (u.GetString() ?? string.Empty) : string.Empty;
                        string api = a.TryGetProperty("url", out JsonElement apiEl) ? (apiEl.GetString() ?? string.Empty) : string.Empty;
                        if (string.IsNullOrEmpty(au)) continue;

                        if (an.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            info.AssetName = an;
                            info.AssetUrl = au;
                            info.AssetApiUrl = api;
                            if (a.TryGetProperty("digest", out JsonElement dg) && dg.ValueKind == JsonValueKind.String)
                                info.Digest = dg.GetString() ?? string.Empty;
                            break;
                        }

                        if (string.IsNullOrEmpty(info.AssetUrl))
                        {
                            info.AssetName = an;
                            info.AssetUrl = au;
                            info.AssetApiUrl = api;
                        }
                    }
                }

                return true;
            }
        }
        catch { return false; }
    }

    internal static string NormalizeVersion(string tag)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
            string s = tag.Trim();

            int start = -1;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsDigit(s[i])) { start = i; break; }
            }
            if (start < 0) return string.Empty;

            int end = start;
            while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.')) end++;

            return s.Substring(start, end - start).Trim('.');
        }
        catch { return string.Empty; }
    }

    internal static int Compare(string localVersion, string remoteVersion)
    {
        try
        {
            if (Version.TryParse(localVersion, out Version lv) &&
                Version.TryParse(remoteVersion, out Version rv))
            {
                return rv.CompareTo(lv);
            }
        }
        catch { }
        return 0;
    }
}
