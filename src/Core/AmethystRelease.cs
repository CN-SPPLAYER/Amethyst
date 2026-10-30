using System;
using System.Text.Json;

namespace Amethyst;

// ===== GitHub Release 解析（第 267 轮）=====
//
// 为什么单独抽出来：
//   `AmethystUpdater`（主界面「检查更新」按钮）与 `AmethystVersionChecker`（启动检查）
//   现在都从 **GitHub Releases API** 取版本，不应各写一份 JSON 解析与版本归一化。
//
// 为什么不再依赖 `Version.txt`：
//   `Version.txt` 是仓库里的一个普通文件，发布 release 时不会自动更新；
//   一旦忘记改，启动检查就会一直显示旧版本。Releases API 直接读发布记录，
//   与「按格式发 release」这一实际流程一致。
internal static class AmethystRelease
{
    internal const string ApiUrl = "https://api.github.com/repos/CN-SPPLAYER/Amethyst/releases/latest";

    internal struct Info
    {
        internal string Tag;        // tag_name，如 Amethyst_v1.1.7
        internal string Name;       // release 标题，如 Amethyst_v1.1.7hotfix1
        internal string Version;    // 归一化后的数字版本，如 1.1.7
        internal string AssetUrl;   // .dll 资产的 browser_download_url
        internal string AssetApiUrl; // .dll 资产的 api url（api.github.com/.../releases/assets/{id}）
        internal string AssetName;  // 资产文件名
        internal string Digest;     // 可选：sha256:...
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

                        // 没有 .dll 时先记住第一个可下载资产，便于报「没有 DLL」而不是「没有资产」
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

    // "Amethyst_v1.1.7" / "v1.1.7" / "1.1.7" / "Amethyst_v1.1.7hotfix2" → "1.1.7"
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

    // 返回值：>0 远程更新；0 相同；<0 本地更新。任一侧解析失败返回 0（按「无法判定」处理）。
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
