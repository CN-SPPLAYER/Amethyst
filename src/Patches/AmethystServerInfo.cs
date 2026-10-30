using System;
using AmongUs.Data;
using Hazel;
using InnerNet;
using UnityEngine;
using Amethyst;

namespace Amethyst.Patches;

// ===== 服务器 / 区域名称识别（第 176 轮新功能）=====
//
// 供「更好的延迟显示」第三项使用（延迟 | 帧率 | 服务器）。
//
// ===== 参考 EndlessHostRoles 8.0.1 `Modules\Utils.cs` 第 5028~5095 行 `GetRegionName()` =====
//
// EHR 的判定顺序（逐条对应，未作改动）：
//   ① 本地游戏（非线上模式）→ "Local Game"
//   ② `region.PingServer` 以 "among.us" 结尾 → 官方服，把全名缩成缩写：
//          "North America" → "NA"，"Europe" → "EU"，"Asia" → "AS"，其余保留原名
//   ③ 服务器 IP 含 "aumods.org" / "duikbo.at" → 官方模组服，按前缀给：
//          "au-eu" → "MEU"；"au-as" → "MAS"；其余 → "MNA"
//   ④ IP 含 "allofus" → AllOfUs 服，取 "AOU-" + 域名第一段（大写）
//   ⑤ 名字里含 "Niko" → 去掉 "233(" / "233 (" 这类噪音并补齐括号
//   ⑥ 其它情况 → 直接用 region.Name
//
// 这套判定的价值在于：原版只给你一个 `region.Name`（可能是 "North America" 这种长名，
// 也可能是一串杂乱的第三方服名），EHR 把它规范成 "NA / EU / AS / MEU / AOU-XXX" 这类
// 一眼能认出的短标记。
internal static class AmethystServerInfo
{
    // 缓存：区域名字不会每帧变，但 ping 显示每帧都在读它。
    // 用 1 秒的节流缓存，避免每帧都做 EndsWith/Contains 这一串字符串判定。
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

    // 逐条对应 EHR 的 GetRegionName，并**额外**把官方服细化到具体机房地区。
    //
    // ⚠ 关于"日本 / 美国 / 新加坡 / 香港"这类具体位置：
    //   游戏**不会**把机房城市名直接给你 —— `IRegionInfo.Name` 只有
    //   "North America" / "Europe" / "Asia" 这种大区名（而且是从服务器动态拉取的）。
    //   所以我分两层做：
    //     ① 先在 region.Name 里找已知地名关键字（有些第三方服名字里带城市）；
    //     ② 再看服务器 IP / FQDN 里的区域码（官方服会命中 "na"/"eu"/"as" 等）；
    //     ③ 都没有就退回 EHR 那套大区缩写（NA / EU / AS）。
    //   这样"能细化就细化，不能细化就老实显示大区"，**绝不编造**机房地名。
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

            // ① 本地游戏
            if (!ignoreNetworkMode)
            {
                AmongUsClient client = AmongUsClient.Instance;
                if (client != null && client.NetworkMode != NetworkModes.OnlineGame)
                    return AmethystText.T("ServerLocal");
            }

            // 收集"可用于判断地区"的所有线索：名字 + ping 主机 + 各服务器 IP/FQDN。
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

            // ② 已知第三方服 —— 同样**不做缩写**，按可读的全名本地化显示。
            //
            //   EHR 原来把它们缩成 MEU / MAS / MNA / AOU-XXX。
            //   按用户要求「不要简写」，这里改成完整可读的本地化名：
            //     aumods.org / duikbo.at  → 「模组服务器（欧洲/亚洲/北美）」
            //     allofus                 → 「AllOfUs 服」
            if (lowerIp.Contains("aumods.org") || lowerIp.Contains("duikbo.at"))
            {
                if (lowerIp.Contains("au-eu")) return AmethystText.T("ServerModEU");
                if (lowerIp.Contains("au-as")) return AmethystText.T("ServerModAS");
                return AmethystText.T("ServerModNA");
            }

            if (lowerIp.Contains("allofus"))
                return AmethystText.T("ServerAllOfUs");

            // ③ 具体机房地区：在名字 / 主机名 / IP 里找城市或国家关键字。
            //    命中就返回**本地化后的城市名**（如「日本」「新加坡」「香港」）。
            string city = MatchCity(lowerName, lowerPing, lowerIp);
            if (!string.IsNullOrEmpty(city)) return city;

            // ④ Niko 服的噪音清理（EHR 同款）
            if (name.IndexOf("Niko", StringComparison.OrdinalIgnoreCase) >= 0)
                name = name.Replace("233(", "-").Replace("233 (", "-").TrimEnd(')');

            // ⑤ 官方服：按**完整地区名**显示，并按当前语言本地化。
            //
            // ⚠ 用户要求：不要简写（不要 NA/EU/AS），各语言按各语言显示 ——
            //   中文「亚洲」、英文「Asia」、俄文「Азия」。
            //
            //   实现上分两步：
            //     (a) 优先用**游戏自带的翻译**（`TranslationController.GetString`）——
            //         游戏内部就有 `StringNames.ServerNA/ServerEU/ServerAS/ServerSA`
            //         这几个键，跟着游戏语言走，最省事也最不容易翻错；
            //     (b) 拿不到就用我们自己的三语文本（Resources/Lang/*.json）。
            if (!string.IsNullOrEmpty(pingServer)
                && pingServer.EndsWith("among.us", StringComparison.Ordinal))
            {
                string full = OfficialRegionFullName(name);
                if (!string.IsNullOrEmpty(full)) return full;
                return name;
            }

            // ⑥ 兜底：原样显示 region.Name
            return name;
        }
        catch
        {
            try { return region != null ? region.Name : string.Empty; }
            catch { return string.Empty; }
        }
    }

    // 官方大区 -> 完整地区名（按当前语言）。
    //
    // 先试游戏自带翻译（StringNames.ServerNA/EU/AS/SA），失败再用我们自己的 JSON 文本。
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
                    // 游戏新增了别的大区：用它自己的原始名（不要缩写）。
                    return name;
            }
        }
        catch { return name; }
    }

    // 先问游戏要翻译，拿不到再查我们自己的语言文件。
    private static string GameOrOwn(StringNames gameKey, string ownKey)
    {
        // (a) 游戏自带翻译
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

        // (b) 我们自己的三语文本
        try
        {
            string own = AmethystText.T(ownKey);
            if (!string.IsNullOrEmpty(own) && own != ownKey) return own;
        }
        catch { }

        return null;
    }

    // 在名字 / ping 主机 / IP 里匹配具体地区关键字。
    //
    // 匹配顺序：**先城市、后国家**（"Tokyo" 比 "Japan" 更具体）。
    // 返回本地化文本，未命中返回 null。
    private static string MatchCity(string name, string ping, string ip)
    {
        // 依次尝试三个来源，任一命中即返回。
        foreach (string src in new[] { name, ping, ip })
        {
            if (string.IsNullOrEmpty(src)) continue;

            // —— 亚洲 ——
            if (Has(src, "hongkong", "hong kong", "hk-", ".hk")) return AmethystText.T("ServerHongKong");
            if (Has(src, "singapore", "sg-", ".sg")) return AmethystText.T("ServerSingapore");
            if (Has(src, "japan", "tokyo", "jp-", ".jp")) return AmethystText.T("ServerJapan");
            if (Has(src, "korea", "seoul", "kr-", ".kr")) return AmethystText.T("ServerKorea");
            if (Has(src, "india", "mumbai", "in-", ".in")) return AmethystText.T("ServerIndia");
            if (Has(src, "china", "beijing", "shanghai")) return AmethystText.T("ServerChina");
            if (Has(src, "taiwan", "taipei")) return AmethystText.T("ServerTaiwan");

            // —— 美洲 ——
            if (Has(src, "brazil", "sao paulo", "sa-east")) return AmethystText.T("ServerBrazil");
            if (Has(src, "us-west", "uswest", "california", "oregon", "los angeles", "san jose")) return AmethystText.T("ServerUSWest");
            if (Has(src, "us-east", "useast", "virginia", "ohio", "new york")) return AmethystText.T("ServerUSEast");
            if (Has(src, "canada", "toronto", "montreal")) return AmethystText.T("ServerCanada");

            // —— 欧洲 ——
            if (Has(src, "london", "uk-", ".uk")) return AmethystText.T("ServerUK");
            if (Has(src, "germany", "frankfurt", "berlin", "de-", ".de")) return AmethystText.T("ServerGermany");
            if (Has(src, "france", "paris", "fr-", ".fr")) return AmethystText.T("ServerFrance");
            if (Has(src, "russia", "moscow", "ru-", ".ru")) return AmethystText.T("ServerRussia");
            if (Has(src, "netherland", "amsterdam", "nl-", ".nl")) return AmethystText.T("ServerNetherlands");
            if (Has(src, "poland", "warsaw", "pl-", ".pl")) return AmethystText.T("ServerPoland");

            // —— 大洋洲 ——
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
