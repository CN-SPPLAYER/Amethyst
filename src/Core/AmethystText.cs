using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Amethyst;

internal static class AmethystText
{
    private const string ResourcePrefix = "Amethyst.Resources.Lang.";

    private static readonly Dictionary<string, Dictionary<string, string>> Maps =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    private static bool _loaded;

    private static string _langRaw;
    private static bool _langRu;
    private static bool _langZh;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            Assembly asm = typeof(AmethystText).Assembly;

            foreach (string name in asm.GetManifestResourceNames())
            {
                if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                    !name.EndsWith(".json", StringComparison.Ordinal))
                    continue;

                string fileName = name.Substring(ResourcePrefix.Length);   
                string lang = fileName.Substring(0, fileName.Length - 5);  

                Dictionary<string, string> raw = ReadEmbedded(asm, name);
                if (raw == null) continue;

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in raw)
                {
                    if (kv.Key == "LanguageID" || kv.Key == "ModVersion") continue;
                    map[kv.Key] = kv.Value;
                }
                Maps[lang] = map;
            }

            if (Maps.Count == 0)
                AmethystPlugin.Logger?.LogError("[Lang] 未找到任何内嵌语言资源，界面文本将显示为键名。");

            VerifyPlaceholders();
            WarnAboutStaleExternalFolder();
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogError($"[Lang] 加载内嵌多语言异常: {ex}");
        }
    }

    private static Dictionary<string, string> ReadEmbedded(Assembly asm, string resourceName)
    {
        try
        {
            using Stream stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                AmethystPlugin.Logger?.LogWarning($"[Lang] 内嵌语言资源缺失: {resourceName}");
                return null;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[Lang] 解析内嵌语言资源 {resourceName} 失败: {ex.Message}");
            return null;
        }
    }

    private static void VerifyPlaceholders()
    {
        try
        {
            if (!Maps.TryGetValue("en_US", out var baseline) || baseline == null) return;

            foreach (var pair in Maps)
            {
                if (pair.Key == "en_US" || pair.Value == null) continue;

                List<string> missing = null;
                List<string> mismatch = null;

                foreach (var kv in baseline)
                {
                    if (!pair.Value.TryGetValue(kv.Key, out string val) || val == null)
                    {
                        (missing ??= new List<string>()).Add(kv.Key);
                        continue;
                    }

                    if (!string.Equals(PlaceholderSignature(val), PlaceholderSignature(kv.Value), StringComparison.Ordinal))
                        (mismatch ??= new List<string>()).Add(kv.Key);
                }

                if (missing != null)
                    AmethystPlugin.Logger?.LogWarning(
                        $"[Lang] {pair.Key} 缺少 {missing.Count} 个键：{string.Join(",", missing)}");

                if (mismatch != null)
                    AmethystPlugin.Logger?.LogWarning(
                        $"[Lang] {pair.Key} 有 {mismatch.Count} 个键的占位符与 en_US 不一致：{string.Join(",", mismatch)}");
            }
        }
        catch { }
    }

    private static void WarnAboutStaleExternalFolder()
    {
        try
        {
            string staleDir = Path.Combine(AmethystExternalResources.ResDir, "Lang");
            if (Directory.Exists(staleDir))
                AmethystPlugin.Logger?.LogInfo(
                    "[Lang] 语言文本已全部内嵌到 DLL，外部目录 Amethyst/Resources/Lang 不再被读取（可安全删除）。");
        }
        catch { }
    }

    private static string PlaceholderSignature(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        List<int> idx = null;
        for (int i = 0; i < s.Length - 1; i++)
        {
            if (s[i] != '{') continue;
            if (s[i + 1] == '{') { i++; continue; }   

            int j = i + 1, n = 0;
            bool any = false;
            while (j < s.Length && s[j] >= '0' && s[j] <= '9')
            {
                n = n * 10 + (s[j] - '0');
                j++;
                any = true;
            }

            if (!any) continue;

            if (j < s.Length && (s[j] == ':' || s[j] == ','))
            {
                int close = s.IndexOf('}', j);
                if (close < 0) continue;
                (idx ??= new List<int>()).Add(n);
                i = close;
                continue;
            }

            if (j < s.Length && s[j] == '}') { (idx ??= new List<int>()).Add(n); i = j; }
        }

        if (idx == null) return string.Empty;
        idx.Sort();

        var sb = new StringBuilder(idx.Count * 3);
        int last = -1;
        for (int i = 0; i < idx.Count; i++)
        {
            if (idx[i] == last) continue;
            last = idx[i];
            if (sb.Length > 0) sb.Append(',');
            sb.Append(last);
        }
        return sb.ToString();
    }

    private static void RefreshLang()
    {
        string v = AmethystConfig.Language != null ? AmethystConfig.Language.Value : "en";

        if (string.IsNullOrWhiteSpace(v) ||
            string.Equals(v.Trim(), AmethystLanguage.Auto, StringComparison.OrdinalIgnoreCase))
        {
            if (_autoStartedAt < 0f) _autoStartedAt = UnityEngine.Time.realtimeSinceStartup;

            string detected = AmethystLanguage.Detect();
            if (!string.IsNullOrEmpty(detected))
            {
                
                AmethystConfig.Language.Value = detected;
                v = detected;
            }
            else
            {
                
                bool ready = AmethystLanguage.ControllerReady();
                bool timedOut = _autoStartedAt >= 0f &&
                                UnityEngine.Time.realtimeSinceStartup - _autoStartedAt >= AutoDetectTimeout;

                if (ready || timedOut)
                {
                    AmethystConfig.Language.Value = "en";
                }

                v = "en";
            }
        }

        if (v != _langRaw)
        {
            _langRaw = v;
            AmethystNotifyText.Invalidate();
            string lower = v?.Trim().ToLowerInvariant() ?? "";
            _langRu = lower == "ru";
            _langZh = lower == "zh" || lower == "zh-cn" || lower == "zh-tw";
        }
    }

    internal static bool IsRussian
    {
        get { RefreshLang(); return _langRu; }
    }

    internal static bool IsChinese
    {
        get { RefreshLang(); return _langZh; }
    }

    private static string CurrentMapKey
    {
        get { RefreshLang(); return _langRu ? "ru_RU" : _langZh ? "zh_CN" : "en_US"; }
    }

    internal static string AllTextForPrewarm()
    {
        try
        {
            EnsureLoaded();
            if (Maps.TryGetValue(CurrentMapKey, out Dictionary<string, string> map) && map != null)
            {
                var sb = new StringBuilder(16 * 1024);
                foreach (string v in map.Values)
                {
                    if (string.IsNullOrEmpty(v)) continue;
                    sb.Append(v).Append('\n');
                }
                return sb.ToString();
            }
        }
        catch { }
        return string.Empty;
    }

    internal static string T(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        EnsureLoaded();
        string text = Lookup(key);
        if (args == null || args.Length == 0) return text;

        if (TryFormat(text, args, out string formatted)) return formatted;

        LogFormatMismatch(key, text);
        return BestEffortFormat(text, args);
    }

    private static bool TryFormat(string fmt, object[] args, out string result)
    {
        result = null;
        try { result = string.Format(fmt, args); return true; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static readonly HashSet<string> _formatWarned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static void LogFormatMismatch(string key, string fmt)
    {
        try
        {
            if (!_formatWarned.Add(key)) return;
            AmethystPlugin.Logger?.LogWarning(
                $"[Lang] 文本 '{key}' 的占位符与调用参数不匹配（\"{fmt}\"），已按尽力填充显示。");
        }
        catch { }
    }

    private static string BestEffortFormat(string fmt, object[] args)
    {
        if (string.IsNullOrEmpty(fmt)) return fmt ?? string.Empty;

        var sb = new StringBuilder(fmt.Length + 16);
        for (int i = 0; i < fmt.Length; i++)
        {
            char c = fmt[i];

            if (c == '}')
            {
                if (i + 1 < fmt.Length && fmt[i + 1] == '}') { sb.Append('}'); i++; }
                else sb.Append(c);
                continue;
            }

            if (c != '{') { sb.Append(c); continue; }

            if (i + 1 < fmt.Length && fmt[i + 1] == '{') { sb.Append('{'); i++; continue; }

            int j = i + 1, n = 0;
            bool any = false;
            while (j < fmt.Length && fmt[j] >= '0' && fmt[j] <= '9')
            {
                n = n * 10 + (fmt[j] - '0');
                j++;
                any = true;
            }

            if (!any) { sb.Append(c); continue; }

            if (j < fmt.Length && (fmt[j] == ':' || fmt[j] == ','))
            {
                int close = fmt.IndexOf('}', j);
                if (close < 0) { sb.Append(c); continue; }
                if (n >= 0 && n < args.Length) sb.Append(args[n]);
                i = close;
                continue;
            }

            if (j < fmt.Length && fmt[j] == '}')
            {
                if (n >= 0 && n < args.Length) sb.Append(args[n]);
                i = j;
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    internal static string LangName
    {
        get
        {
            RefreshLang();
            if (_langZh) return "中文";
            if (_langRu) return "Русский";
            return "English";
        }
    }

    internal static void Toggle()
    {
        RefreshLang();
        if (_langZh) AmethystConfig.Language.Value = "en";
        else if (_langRu) AmethystConfig.Language.Value = "zh";
        else AmethystConfig.Language.Value = "ru";
        _cacheLang = null;
    }

    private static readonly Dictionary<string, string> TextCache2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static string _cacheLang;
    
    private static float _autoStartedAt = -1f;
    private const float AutoDetectTimeout = 10f;

    private static string Lookup(string key)
    {
        string lang = CurrentMapKey;
        if (lang != _cacheLang)
        {
            TextCache2.Clear();
            _cacheLang = lang;
        }

        if (TextCache2.TryGetValue(key, out string cached)) return cached;

        string result = null;
        if (Maps.TryGetValue(lang, out var map) && map.TryGetValue(key, out string val))
        {
            result = val;
        }
        else if (Maps.TryGetValue("zh_CN", out var zh) && zh.TryGetValue(key, out string zhVal))
        {
            result = zhVal;
        }
        else if (Maps.TryGetValue("en_US", out var en) && en.TryGetValue(key, out string enVal))
        {
            result = enVal;
        }
        else
        {
            result = key;
        }

        TextCache2[key] = result;
        return result;
    }
}
