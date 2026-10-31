using System;
using AmongUs.Data;
using Hazel;
using InnerNet;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

internal static class AmethystServerInfo
{
    
    private static string _cached = string.Empty;
    private static float _nextAt;

    internal static string RegionName()
    {
        try
        {
            string lang = AmethystConfig.Language != null ? AmethystConfig.Language.Value : string.Empty;
            float now = Time.unscaledTime;
            if (now < _nextAt && string.Equals(lang, _cachedLang)) return _cached;
            _nextAt = now + 0.5f;
            _cachedLang = lang;

            _cached = Resolve(null, false);
            return _cached;
        }
        catch { return string.Empty; }
    }
    private static string _cachedLang = string.Empty;

    private static string Resolve(IRegionInfo region, bool ignoreNetworkMode)
    {
        try
        {
            if (region == null)
            {
                ServerManager sm = ServerManager.Instance;
                if (sm == null) return string.Empty;
                region = sm.CurrentRegion;
            }
            if (region == null) return string.Empty;

            string name = region.Name ?? string.Empty;

            if (!ignoreNetworkMode)
            {
                AmongUsClient client = AmongUsClient.Instance;
                if (client != null && client.NetworkMode != NetworkModes.OnlineGame)
                    return AmethystText.T("ServerLocal");
            }

            string pingServer = null;
            try { pingServer = region.PingServer; } catch { }

            string ip = string.Empty;
            try
            {
                var servers = region.Servers;
                if (servers != null && servers.Length > 0 && servers[0] != null)
                    ip = servers[0].Ip ?? string.Empty;
            }
            catch { }

            string lowerName = name.ToLowerInvariant();
            string lowerPing = (pingServer ?? string.Empty).ToLowerInvariant();
            string lowerIp = ip.ToLowerInvariant();

            if (lowerIp.Contains("aumods.org") || lowerIp.Contains("duikbo.at"))
            {
                if (lowerIp.Contains("au-eu")) return AmethystText.T("ServerModEU");
                if (lowerIp.Contains("au-as")) return AmethystText.T("ServerModAS");
                return AmethystText.T("ServerModNA");
            }

            if (lowerIp.Contains("allofus"))
                return AmethystText.T("ServerAllOfUs");

            string city = MatchCity(lowerName, lowerPing, lowerIp);
            if (!string.IsNullOrEmpty(city)) return city;

            if (name.IndexOf("Niko", StringComparison.OrdinalIgnoreCase) >= 0)
                name = name.Replace("233(", "-").Replace("233 (", "-").TrimEnd(')');

            if (!string.IsNullOrEmpty(pingServer)
                && pingServer.EndsWith("among.us", StringComparison.Ordinal))
            {
                string full = OfficialRegionFullName(name);
                if (!string.IsNullOrEmpty(full)) return full;
                return name;
            }

            return name;
        }
        catch
        {
            try { return region != null ? region.Name : string.Empty; }
            catch { return string.Empty; }
        }
    }

    private static string OfficialRegionFullName(string name)
    {
        try
        {
            switch (name)
            {
                case "North America":
                    return GameOrOwn(StringNames.ServerNA, "ServerRegionNA");
                case "Europe":
                    return GameOrOwn(StringNames.ServerEU, "ServerRegionEU");
                case "Asia":
                    return GameOrOwn(StringNames.ServerAS, "ServerRegionAS");
                case "South America":
                    return GameOrOwn(StringNames.ServerSA, "ServerRegionSA");
                default:
                    
                    return name;
            }
        }
        catch { return name; }
    }

    private static string GameOrOwn(StringNames gameKey, string ownKey)
    {
        
        try
        {
            TranslationController tc = TranslationController.Instance;
            if (tc != null)
            {
                string s = tc.GetString(gameKey, (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>)null);
                if (!string.IsNullOrEmpty(s) && s != gameKey.ToString()) return s;
            }
        }
        catch { }

        try
        {
            string own = AmethystText.T(ownKey);
            if (!string.IsNullOrEmpty(own) && own != ownKey) return own;
        }
        catch { }

        return null;
    }

    private static string MatchCity(string name, string ping, string ip)
    {
        
        foreach (string src in new[] { name, ping, ip })
        {
            if (string.IsNullOrEmpty(src)) continue;

            if (Has(src, "hongkong", "hong kong", "hk-", ".hk")) return AmethystText.T("ServerHongKong");
            if (Has(src, "singapore", "sg-", ".sg")) return AmethystText.T("ServerSingapore");
            if (Has(src, "japan", "tokyo", "jp-", ".jp")) return AmethystText.T("ServerJapan");
            if (Has(src, "korea", "seoul", "kr-", ".kr")) return AmethystText.T("ServerKorea");
            if (Has(src, "india", "mumbai", "in-", ".in")) return AmethystText.T("ServerIndia");
            if (Has(src, "china", "beijing", "shanghai")) return AmethystText.T("ServerChina");
            if (Has(src, "taiwan", "taipei")) return AmethystText.T("ServerTaiwan");

            if (Has(src, "brazil", "sao paulo", "sa-east")) return AmethystText.T("ServerBrazil");
            if (Has(src, "us-west", "uswest", "california", "oregon", "los angeles", "san jose")) return AmethystText.T("ServerUSWest");
            if (Has(src, "us-east", "useast", "virginia", "ohio", "new york")) return AmethystText.T("ServerUSEast");
            if (Has(src, "canada", "toronto", "montreal")) return AmethystText.T("ServerCanada");

            if (Has(src, "london", "uk-", ".uk")) return AmethystText.T("ServerUK");
            if (Has(src, "germany", "frankfurt", "berlin", "de-", ".de")) return AmethystText.T("ServerGermany");
            if (Has(src, "france", "paris", "fr-", ".fr")) return AmethystText.T("ServerFrance");
            if (Has(src, "russia", "moscow", "ru-", ".ru")) return AmethystText.T("ServerRussia");
            if (Has(src, "netherland", "amsterdam", "nl-", ".nl")) return AmethystText.T("ServerNetherlands");
            if (Has(src, "poland", "warsaw", "pl-", ".pl")) return AmethystText.T("ServerPoland");

            if (Has(src, "australia", "sydney", "au-", ".au")) return AmethystText.T("ServerAustralia");
        }
        return null;
    }

    private static bool Has(string src, params string[] needles)
    {
        for (int i = 0; i < needles.Length; i++)
            if (src.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
        return false;
    }
}
