using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using UnityEngine;

namespace Amethyst;



internal static class AmethystAccess
{
    private static readonly List<AccessEntry> Bans = new List<AccessEntry>();
    private static readonly List<AccessEntry> CloudBans = new List<AccessEntry>();
    private static readonly Dictionary<int, float> ActedAt = new Dictionary<int, float>();
    private const float ActCooldown = 6f;
    private const float NotifyCooldown = 10f;
    private static readonly Dictionary<int, float> NotifiedAt = new Dictionary<int, float>();
    
    private static readonly HashSet<int> PuidMissingLogged = new HashSet<int>();
    private static bool OnNotifyCooldown(int id) => NotifiedAt.TryGetValue(id, out float t) && Time.realtimeSinceStartup - t < NotifyCooldown;
    private static void TouchNotify(int id) => NotifiedAt[id] = Time.realtimeSinceStartup;
    private static bool _loaded;

    private static string AmethystDir => AmethystPlatform.DataDir;
    internal static string Dir => AmethystDir;
    private static string BanTxt => Path.Combine(AmethystDir, "BanList.txt");
    private static string CloudBanTxt => Path.Combine(AmethystDir, "CloudBanList.txt");

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Parse(AmethystConfig.BanList?.Value, Bans);

        RefreshHeader(BanTxt, AmethystText.T("AmethystFormatNameFriendCodePUID"));

        ImportFile(CloudBanTxt, CloudBans);
        RefreshHeader(CloudBanTxt, AmethystText.T("AmethystFormatNameFriendCodePUID"));
    }

    private static void Parse(string csv, List<AccessEntry> into)
    {
        into.Clear();
        if (string.IsNullOrWhiteSpace(csv)) return;
        foreach (string item in csv.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            string[] p = item.Split('|');
            string name, code, puid;
            if (p.Length == 1)
            {
                
                string v = p[0].Trim();
                name = string.Empty;
                bool isPuid = LooksLikePuid(v);
                code = isPuid ? string.Empty : v;
                puid = isPuid ? v : string.Empty;
            }
            else
            {
                name = p[0].Trim();
                code = p.Length >= 2 ? p[1].Trim() : string.Empty;
                puid = p.Length >= 3 ? p[2].Trim() : string.Empty;
            }
            if ((code.Length > 0 || puid.Length > 0) && !Has(into, code, puid))
                into.Add(new AccessEntry { Name = name, Code = code, Puid = puid });
        }
    }

    private static void Persist(BepInEx.Configuration.ConfigEntry<string> entry, List<AccessEntry> list, string txtPath)
    {
        if (entry != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(Esc(list[i].Name)).Append('|').Append(Esc(list[i].Code)).Append('|').Append(Esc(list[i].Puid));
            }
            entry.Value = sb.ToString();
        }

        WriteTxt(txtPath, list);
    }

    private static void WriteTxt(string path, List<AccessEntry> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sb = new StringBuilder();
            sb.AppendLine(AmethystText.T("AmethystFormatNameFriendCodePUID"));
            for (int i = 0; i < list.Count; i++)
                sb.Append(list[i].Name).Append(" | ").Append(list[i].Code).Append(" | ").Append(list[i].Puid).Append('\n');
            File.WriteAllText(path, sb.ToString());
        }
        catch { }
    }

    private static void RefreshHeader(string path, string header)
    {
        try
        {
            if (!File.Exists(path)) return;
            var lines = new List<string>(File.ReadAllLines(path));
            if (lines.Count > 0 && lines[0].StartsWith("# Amethyst", System.StringComparison.Ordinal) && lines[0] != header)
            {
                lines[0] = header;
                File.WriteAllText(path, string.Join("\n", lines) + "\n");
            }
        }
        catch { }
    }

    private static string Esc(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace(';', ' ').Replace('|', ' ').Trim();

    private static bool LooksLikePuid(string s)
    {
        if (s == null || s.Length != 32) return false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!hex) return false;
        }
        return true;
    }

    private static bool Has(List<AccessEntry> list, string code, string puid)
    {
        for (int i = 0; i < list.Count; i++)
        {
            AccessEntry e = list[i];
            bool hasCode = !string.IsNullOrEmpty(e.Code);

            if (hasCode && !string.IsNullOrEmpty(code))
            {
                if (string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase)) return true;
                continue;   
            }

            if (!string.IsNullOrEmpty(puid) && !string.IsNullOrEmpty(e.Puid)
                && string.Equals(e.Puid, puid, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool HasPuidOnly(List<AccessEntry> list)
    {
        for (int i = 0; i < list.Count; i++)
            if (string.IsNullOrEmpty(list[i].Code) && !string.IsNullOrEmpty(list[i].Puid)) return true;
        return false;
    }

    internal static int BanCount { get { EnsureLoaded(); return Bans.Count; } }
    internal static IReadOnlyList<AccessEntry> BanEntries { get { EnsureLoaded(); return Bans; } }

    internal static int CloudBanCount { get { EnsureLoaded(); return CloudBans.Count; } }
    internal static IReadOnlyList<AccessEntry> CloudBanEntries { get { EnsureLoaded(); return CloudBans; } }

    internal static bool IsBanned(string fc, string puid)
    {
        EnsureLoaded();
        if (AmethystConfig.AccessBanEnabled != null && AmethystConfig.AccessBanEnabled.Value && Has(Bans, fc, puid))
            return true;
        if (AmethystConfig.CloudBanEnabled != null && AmethystConfig.CloudBanEnabled.Value && Has(CloudBans, fc, puid))
            return true;
        return false;
    }

    internal static bool IsBotPlatform(string raw)
        => !string.IsNullOrEmpty(raw) && raw.IndexOf("bot", System.StringComparison.OrdinalIgnoreCase) >= 0;

    internal static void AddBan(string name, string fc, string puid)
    {
        EnsureLoaded();
        if ((string.IsNullOrWhiteSpace(fc) && string.IsNullOrWhiteSpace(puid)) || Has(Bans, fc, puid)) return;
        Bans.Add(new AccessEntry { Name = name ?? string.Empty, Code = (fc ?? string.Empty).Trim(), Puid = (puid ?? string.Empty).Trim() });
        Persist(AmethystConfig.BanList, Bans, BanTxt);
    }

    internal static void RemoveBan(string key) { EnsureLoaded(); if (RemoveByKey(Bans, key)) Persist(AmethystConfig.BanList, Bans, BanTxt); }

    internal static void RemoveCloudBan(string key)
    {
        EnsureLoaded();
        if (RemoveByKey(CloudBans, key)) WriteTxt(CloudBanTxt, CloudBans);
    }

    private static bool RemoveByKey(List<AccessEntry> list, string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        for (int i = list.Count - 1; i >= 0; i--)
            if (string.Equals(list[i].Code, key, StringComparison.OrdinalIgnoreCase) || string.Equals(list[i].Puid, key, StringComparison.OrdinalIgnoreCase))
            { list.RemoveAt(i); return true; }
        return false;
    }

    internal static void ClearBans() { EnsureLoaded(); Bans.Clear(); Persist(AmethystConfig.BanList, Bans, BanTxt); }

    internal static void ImportTxt()
    {
        EnsureLoaded();
        int b = ImportFile(BanTxt, Bans);
        Persist(AmethystConfig.BanList, Bans, BanTxt);
        AmethystToast.Push(AmethystText.T("ImportTXT"), AmethystText.T("ImportedBans", b), 3.0f, AmethystNotifyKind.Success);
    }

    private static bool _isFetchingCloudBans;
    internal static bool IsFetchingCloudBans => _isFetchingCloudBans;

    internal static void FetchCloudBans(MonoBehaviour runner)
    {
        if (_isFetchingCloudBans) return;
        EnsureLoaded();
        if (runner == null)
        {
            AmethystToast.Push(AmethystText.T("CloudBanList"), AmethystText.T("CloudBanFailed"), 3.0f, AmethystNotifyKind.Warning);
            return;
        }

        _isFetchingCloudBans = true;
        try
        {
            runner.StartCoroutine(CoFetchCloudBans().WrapToIl2Cpp());
        }
        catch (Exception ex)
        {
            _isFetchingCloudBans = false;
            AmethystPlugin.Logger?.LogError($"FetchCloudBans failed: {ex}");
            AmethystToast.Push(AmethystText.T("CloudBanList"), AmethystText.T("CloudBanFailed"), 3.0f, AmethystNotifyKind.Warning);
        }
    }

    private static System.Collections.IEnumerator CoFetchCloudBans()
    {
        UnityEngine.Networking.UnityWebRequest req = null;
        try
        {
            req = UnityEngine.Networking.UnityWebRequest.Get("https://api2.elauk.top/acban");
            if (req != null)
            {
                req.timeout = 10;
                req.SetRequestHeader("Accept", "application/json");
            }
        }
        catch { }

        if (req == null)
        {
            _isFetchingCloudBans = false;
            AmethystToast.Push(AmethystText.T("CloudBanList"), AmethystText.T("CloudBanFailed"), 3.0f, AmethystNotifyKind.Warning);
            yield break;
        }

        yield return req.SendWebRequest();

        bool success = false;
        int added = 0;
        int removed = 0;
        int total = 0;

        try
        {
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success && req.downloadHandler != null)
            {
                string json = req.downloadHandler.text;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var newCloudList = new List<AccessEntry>();
                    using (var doc = System.Text.Json.JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("bans", out var bansElement) && bansElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var item in bansElement.EnumerateArray())
                            {
                                string name = item.TryGetProperty("name", out var np) ? np.GetString() : string.Empty;
                                string fc = item.TryGetProperty("friendCode", out var fp) ? fp.GetString() : string.Empty;
                                string puid = item.TryGetProperty("puid", out var pp) ? pp.GetString() : string.Empty;

                                fc = (fc ?? string.Empty).Trim();
                                puid = (puid ?? string.Empty).Trim();
                                name = (name ?? string.Empty).Trim();

                                if ((fc.Length > 0 || puid.Length > 0) && !Has(newCloudList, fc, puid))
                                {
                                    newCloudList.Add(new AccessEntry { Name = name, Code = fc, Puid = puid });
                                }
                            }
                        }
                    }

                    for (int i = 0; i < newCloudList.Count; i++)
                    {
                        var e = newCloudList[i];
                        if (!Has(CloudBans, e.Code, e.Puid)) added++;
                    }

                    for (int i = 0; i < CloudBans.Count; i++)
                    {
                        var e = CloudBans[i];
                        if (!Has(newCloudList, e.Code, e.Puid)) removed++;
                    }

                    CloudBans.Clear();
                    CloudBans.AddRange(newCloudList);
                    total = CloudBans.Count;

                    WriteTxt(CloudBanTxt, CloudBans);
                    success = true;
                }
            }
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogError($"Parse cloud ban response failed: {ex}");
        }
        finally
        {
            _isFetchingCloudBans = false;
            try { req.Dispose(); } catch { }
        }

        if (success)
        {
            string msg = AmethystText.T("CloudBanUpdated", added, removed, total);
            AmethystToast.Push(AmethystText.T("CloudBanList"), msg, 5.0f, AmethystNotifyKind.Success);
            AmethystEventLog.Add(msg, AmethystNotifyKind.Success, AmethystEventCat.Other);
            AmethystPlugin.Logger?.LogInfo($"[CloudBan] {msg}");
        }
        else
        {
            AmethystToast.Push(AmethystText.T("CloudBanList"), AmethystText.T("CloudBanFailed"), 5.0f, AmethystNotifyKind.Warning);
        }
    }

    private static int ImportFile(string path, List<AccessEntry> list)
    {
        try
        {
            if (!File.Exists(path)) return list.Count;
            list.Clear();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split('|');
                string name, fc, puid;
                if (p.Length == 1)
                {
                    
                    string v = p[0].Trim();
                    name = string.Empty;
                    if (LooksLikePuid(v)) { fc = string.Empty; puid = v; }
                    else { fc = v; puid = string.Empty; }
                }
                else { name = p[0].Trim(); fc = p.Length >= 2 ? p[1].Trim() : string.Empty; puid = p.Length >= 3 ? p[2].Trim() : string.Empty; }
                if ((fc.Length > 0 || puid.Length > 0) && !Has(list, fc, puid))
                    list.Add(new AccessEntry { Name = name, Code = fc, Puid = puid });
            }
        }
        catch { }
        return list.Count;
    }

    internal static void Kick(InnerNetClient net, int clientId, bool ban, bool force = false)
    {
        try
        {
            if (net == null || !net.AmHost) return;
            if (clientId < 0 || clientId == net.ClientId || clientId == net.HostId) return;
            if (!force && OnCooldown(clientId)) return;
            Touch(clientId);
            net.KickPlayer(clientId, ban);
        }
        catch { }
    }

    private static MethodInfo _sendLateRejection;

    internal static void ErrorKick(InnerNetClient net, int clientId, bool force = false)
    {
        try
        {
            if (net == null || !net.AmHost) return;
            if (clientId < 0 || clientId == net.ClientId || clientId == net.HostId) return;
            if (!force && OnCooldown(clientId)) return;
            Touch(clientId);

            if (net.GameState == InnerNetClient.GameStates.Started)
            {
                if (_sendLateRejection == null)
                {
                    _sendLateRejection = typeof(InnerNetClient).GetMethod(
                        "SendLateRejection",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (_sendLateRejection != null)
                {
                    _sendLateRejection.Invoke(net, new object[] { clientId, DisconnectReasons.ClientTimeout });
                    return;
                }
            }

            net.KickPlayer(clientId, false);
        }
        catch
        {
            try { net?.KickPlayer(clientId, false); } catch { }
        }
    }

    private static readonly Dictionary<int, int> ViolationCounts = new Dictionary<int, int>();

    internal static void Act(InnerNetClient net, int clientId, string action, string who, string reason, bool silentToast = false, bool bypassThreshold = false)
    {
        string whoShow = AmethystColors.Name(net, clientId) ?? who;
        string act = (action ?? "Null").Trim().ToLowerInvariant();

        string plainName = StripColor(whoShow);

        if (act == "kick" || act == "ban" || act == "errorkick")
        {
            if (!bypassThreshold)
            {
                int targetThreshold = 1;
                try { if (AmethystConfig.CheaterActionThreshold != null) targetThreshold = Mathf.Clamp(AmethystConfig.CheaterActionThreshold.Value, 1, 8); } catch { }

                ViolationCounts.TryGetValue(clientId, out int currentCount);
                currentCount++;
                ViolationCounts[clientId] = currentCount;

                if (currentCount < targetThreshold)
                {
                    
                    if (!silentToast) AmethystToast.Push(AmethystText.T("Protection"), AmethystText.T("Warned", whoShow, $"{reason} ({currentCount}/{targetThreshold})"), 5.0f, AmethystNotifyKind.Warning);
                    return;
                }
            }
            else
            {
                
                ViolationCounts.TryGetValue(clientId, out int cur);
                ViolationCounts[clientId] = cur + 1;
            }
        }

        switch (act)
        {
            case "warn":
                if (!silentToast) AmethystToast.Push(AmethystText.T("Protection"), AmethystText.T("Warned", whoShow, reason), 5.0f, AmethystNotifyKind.Warning);
                break;
            case "kick":
                AmethystDisconnect.RecordAction(clientId, false, reason, plainName);
                
                Kick(net, clientId, false, bypassThreshold);
                if (!silentToast) AmethystToast.Push(AmethystText.T("Kick_2"), AmethystText.T("Kicked", whoShow, reason), 5.0f, AmethystNotifyKind.Warning);
                break;
            case "errorkick":
                AmethystDisconnect.RecordAction(clientId, false, reason, plainName);
                ErrorKick(net, clientId, bypassThreshold);
                if (!silentToast) AmethystToast.Push(AmethystText.T("ErrorKick"), AmethystText.T("Kicked", whoShow, reason), 5.0f, AmethystNotifyKind.Warning);
                break;
            case "ban":
                AmethystDisconnect.RecordAction(clientId, true, reason, plainName);
                Kick(net, clientId, true, bypassThreshold);
                if (!silentToast) AmethystToast.Push(AmethystText.T("Ban_2"), AmethystText.T("Banned", whoShow, reason), 5.0f, AmethystNotifyKind.Danger);
                break;
        }
    }

    internal static void BanClient(InnerNetClient net, ClientData c)
    {
        if (c == null) return;
        AddBan(SafeName(c), SafeFc(c), SafePuid(c));
        AmethystDisconnect.RecordAction(c.Id, true, AmethystText.T("BanList"), SafeName(c));
        Kick(net, c.Id, true);
        AmethystToast.Push(AmethystText.T("BanList"), Show(c), 5.0f, AmethystNotifyKind.Danger);
    }

    private static string Show(ClientData c) => AmethystColors.Name(c) ?? SafeName(c);

    private static string StripColor(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return (s ?? string.Empty).Trim();
        try
        {
            var sb = new System.Text.StringBuilder(s.Length);
            int at = 0;
            while (at < s.Length)
            {
                int open = s.IndexOf('<', at);
                if (open < 0) { sb.Append(s, at, s.Length - at); break; }
                if (open > at) sb.Append(s, at, open - at);
                int close = s.IndexOf('>', open);
                if (close < 0) break;
                at = close + 1;
            }
            return sb.ToString().Trim();
        }
        catch { return (s ?? string.Empty).Trim(); }
    }

    private static bool IsValidFriendCode(string fc)
    {
        if (string.IsNullOrWhiteSpace(fc)) return false;
        int sep = fc.LastIndexOf('#');
        if (sep <= 0 || fc.Length - sep - 1 != 4) return false;
        for (int i = sep + 1; i < fc.Length; i++)
            if (!char.IsDigit(fc[i])) return false;
        return true;
    }

    private static bool _platformDiagLogged;

    private static bool IsGenericPlatformName(string name)
        => string.Equals(name, "TESTNAME", System.StringComparison.Ordinal);

    private static bool IsPlatformDataValid(ClientData client)
    {
        try
        {
            if (client == null || client.PlatformData == null) return true;
            var pd = client.PlatformData;
            string pname = (pd.PlatformName ?? string.Empty).Trim();
            ulong xuid = pd.XboxPlatformId;
            ulong psid = pd.PsnPlatformId;

            switch ((int)pd.Platform)
            {
                case (int)Platforms.StandaloneEpicPC:
                case (int)Platforms.StandaloneSteamPC:
                case (int)Platforms.StandaloneMac:
                case (int)Platforms.StandaloneItch:
                case (int)Platforms.IPhone:
                case (int)Platforms.Android:
                    return IsGenericPlatformName(pname) && xuid == 0 && psid == 0;

                case (int)Platforms.StandaloneWin10:
                    return IsGenericPlatformName(pname) && xuid != 0 && psid == 0;

                case (int)Platforms.Xbox:
                    return !IsGenericPlatformName(pname)
                        && pname.Length >= 1 && pname.Length <= 16
                        && xuid != 0 && psid == 0;

                case (int)Platforms.Playstation:
                    return !IsGenericPlatformName(pname) && xuid == 0 && psid != 0;

                case (int)Platforms.Switch:
                    return !IsGenericPlatformName(pname) && xuid == 0 && psid == 0;

                case 112:
                    return xuid == 0 && psid == 0;

                case 255:
                    try
                    {
                        if (AmongUsClient.Instance != null && AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame)
                            return true;
                    }
                    catch { }
                    return xuid == 0 && psid == 0;

                default:
                    if (!_platformDiagLogged)
                    {
                        _platformDiagLogged = true;
                        AmethystPlugin.Logger?.LogWarning("[PlatformGuard] unknown platform tag " + (int)pd.Platform + " (name '" + pname + "'), skipped");
                    }
                    return true;
            }
        }
        catch (System.Exception ex)
        {
            if (!_platformDiagLogged)
            {
                _platformDiagLogged = true;
                AmethystPlugin.Logger?.LogWarning("[PlatformGuard] validation error: " + ex.GetType().Name + ": " + ex.Message);
            }
            return true;
        }
    }

    internal static void Enforce(InnerNetClient net, ClientData client)
    {
        try
        {
            if (net == null || !net.AmHost || client == null) return;
            if (client.Id < 0 || client.Id == net.ClientId || client.Id == net.HostId) return;

            string fc = SafeFc(client);
            string puid = SafePuid(client);
            if (OnCooldown(client.Id)) return;

            try
            {
                bool puidOnly = (AmethystConfig.AccessBanEnabled != null && AmethystConfig.AccessBanEnabled.Value && HasPuidOnly(Bans))
                             || (AmethystConfig.CloudBanEnabled != null && AmethystConfig.CloudBanEnabled.Value && HasPuidOnly(CloudBans));
                if (puidOnly && string.IsNullOrEmpty(puid) && PuidMissingLogged.Add(client.Id))
                    AmethystPlugin.Logger?.LogWarning($"[BanList] client {client.Id} ({SafeName(client)}) has no PUID; PUID-only entries cannot match");
            }
            catch { }

            if (AmethystConfig.InvalidFcGuard != null && AmethystConfig.InvalidFcGuard.Value
                && !string.IsNullOrWhiteSpace(fc) && !IsValidFriendCode(fc)
                && !OnNotifyCooldown(client.Id))
            {
                TouchNotify(client.Id);
                AmethystToast.Push(AmethystText.T("Guard"), AmethystNotifyText.Body(Show(client), AmethystText.T("InvalidFC")), 5.0f, AmethystNotifyKind.Warning);
                AmethystEventLog.Add($"{AmethystText.T("InvalidFC")}: {SafeName(client)}", AmethystNotifyKind.Warning, AmethystEventCat.Other);
            }

            if (AmethystConfig.PlatformSpoofGuard != null && AmethystConfig.PlatformSpoofGuard.Value
                && !IsPlatformDataValid(client) && !OnNotifyCooldown(client.Id))
            {
                TouchNotify(client.Id);
                AmethystToast.Push(AmethystText.T("Guard"), AmethystNotifyText.Body(Show(client), AmethystText.T("PlatformSpoof")), 5.0f, AmethystNotifyKind.Warning);
                AmethystEventLog.Add($"{AmethystText.T("PlatformSpoof")}: {SafeName(client)}", AmethystNotifyKind.Warning, AmethystEventCat.Other);
            }

            if (IsBanned(fc, puid))
            {
                AmethystDisconnect.RecordAction(client.Id, true, AmethystText.T("BanList"), StripColor(Show(client)));
                Kick(net, client.Id, true, true);
                AmethystToast.Push(AmethystText.T("BanList"), Show(client), 5.0f, AmethystNotifyKind.Danger);
                return;
            }

            if (AmethystConfig.BotGuard != null && AmethystConfig.BotGuard.Value && IsBotPlatform(SafePlatform(client)))
            {
                AmethystDisconnect.RecordAction(client.Id, true, AmethystText.T("Bot"), StripColor(Show(client)));
                Kick(net, client.Id, true, true);
                AmethystToast.Push(AmethystText.T("Guard"), AmethystNotifyText.Body(Show(client), AmethystText.T("Bot")), 5.0f, AmethystNotifyKind.Danger);
                return;
            }

            if (TryLevel(client, out int lvl))
            {
                if (AmethystConfig.MinLevelEnabled.Value && lvl < AmethystConfig.MinLevel.Value)
                {
                    Touch(client.Id);
                    Act(net, client.Id, AmethystConfig.MinLevelAction.Value, Show(client), AmethystText.T("LvlLow", lvl, AmethystConfig.MinLevel.Value), false, true);
                    return;
                }

                if (AmethystConfig.MaxLevelEnabled.Value && lvl > AmethystConfig.MaxLevel.Value)
                {
                    Touch(client.Id);
                    Act(net, client.Id, AmethystConfig.MaxLevelAction.Value, Show(client), AmethystText.T("LvlHigh", lvl, AmethystConfig.MaxLevel.Value), false, true);
                }
            }
        }
        catch { }
    }

    private static bool OnCooldown(int id) => ActedAt.TryGetValue(id, out float t) && Time.realtimeSinceStartup - t < ActCooldown;
    private static void Touch(int id) => ActedAt[id] = Time.realtimeSinceStartup;

    internal static ClientData FindClient(InnerNetClient net, int clientId)
    {
        try
        {
            if (net == null || net.allClients == null) return null;
            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c != null && c.Id == clientId) return c;
            }
        }
        catch { }
        return null;
    }

    internal static string ClientName(InnerNetClient net, int clientId)
    {
        ClientData c = FindClient(net, clientId);
        return c != null ? SafeName(c) : "#" + clientId;
    }

    internal static string PlatformOf(ClientData c)
    {
        try
        {
            if (c == null || c.PlatformData == null) return "Unknown";
            return (int)c.PlatformData.Platform switch
            {
                1 => "Epic",
                2 => "Steam",
                3 => "Mac",
                4 => "MS Store",
                5 => "Itch.io",
                6 => "iOS",
                7 => "Android",
                8 => "Switch",
                9 => "Xbox",
                10 => "PS",
                112 => "Starlight",
                _ => "Unknown",
            };
        }
        catch { return "Unknown"; }
    }

    internal static string SafeName(ClientData c)
    {
        try { if (c != null && !string.IsNullOrWhiteSpace(c.PlayerName)) return c.PlayerName.Trim(); }
        catch { }
        return c != null ? "#" + c.Id : "?";
    }

    private static string SafeFc(ClientData c)
    {
        try { return (c.FriendCode ?? string.Empty).Trim(); }
        catch { return string.Empty; }
    }

    private static string SafePuid(ClientData c)
    {
        try { return (c.ProductUserId ?? string.Empty).Trim(); }
        catch { return string.Empty; }
    }

    internal static string SafePlatform(ClientData c)
    {
        try { return c != null && c.PlatformData != null ? (c.PlatformData.PlatformName ?? string.Empty).Trim() : string.Empty; }
        catch { return string.Empty; }
    }

    private static bool TryLevel(ClientData c, out int level)
    {
        return Patches.AmethystJoinLevels.TryLevel(c != null ? c.Id : -1, c != null ? c.Character : null, out level);
    }
}
