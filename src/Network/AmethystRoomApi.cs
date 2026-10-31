using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Amethyst;

internal sealed class AmethystRoom
{
    internal string Code = string.Empty;
    internal string Host = string.Empty;
    internal string State = "lobby";
    internal string Map = string.Empty;
    internal string Mode = "classic";
    internal int Players;
    internal int MaxPlayers;
    internal int Ping;
    internal string Region = string.Empty;
    internal string Role = "host";
    internal string Source = string.Empty;
    internal string SourceName = string.Empty;
    internal string Version = string.Empty;
    internal string Note = string.Empty;
    internal int Age;
    internal int Seen;

    internal bool InGame => string.Equals(State, "ingame", StringComparison.OrdinalIgnoreCase);
    internal bool IsFull => MaxPlayers > 0 && Players >= MaxPlayers;
}

internal static class AmethystRoomApi
{
    internal const string DefaultBase = "https://api2.elauk.top";

    internal static string BaseUrl
    {
        get
        {
            string v = null;
            try { v = AmethystConfig.RoomListApiBase != null ? AmethystConfig.RoomListApiBase.Value : null; }
            catch { }
            if (string.IsNullOrWhiteSpace(v)) return DefaultBase;
            return v.Trim().TrimEnd('/');
        }
    }

    internal static string RoomsUrl => BaseUrl + "/home/api/rooms";
    internal static string ReportUrl => BaseUrl + "/home/api/room";

    internal static string BuildReport(string code, string host, string state, string map, string mode,
        int players, int maxPlayers, int ping, string region, string note)
    {
        try
        {
            using (var ms = new MemoryStream(320))
            {
                using (var w = new Utf8JsonWriter(ms))
                {
                    w.WriteStartObject();
                    w.WriteString("code", code ?? string.Empty);
                    w.WriteString("host", host ?? string.Empty);
                    w.WriteString("state", string.Equals(state, "ingame", StringComparison.OrdinalIgnoreCase) ? "ingame" : "lobby");
                    w.WriteString("source", "amethyst");
                    w.WriteString("sourceName", "Amethyst");
                    w.WriteString("version", AmethystPlugin.PluginVersion ?? string.Empty);
                    if (!string.IsNullOrEmpty(map)) w.WriteString("map", map);
                    w.WriteString("mode", string.Equals(mode, "hidenseek", StringComparison.OrdinalIgnoreCase) ? "hidenseek" : "classic");
                    w.WriteNumber("players", Math.Max(0, players));
                    w.WriteNumber("maxPlayers", Math.Max(0, maxPlayers));
                    w.WriteNumber("ping", Math.Max(0, ping));
                    if (!string.IsNullOrEmpty(region)) w.WriteString("region", region);
                    w.WriteString("role", "host");
                    
                    if (!string.IsNullOrEmpty(note)) w.WriteString("note", note);
                    w.WriteEndObject();
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
        catch { return string.Empty; }
    }

    internal static string BuildClose(string code)
    {
        try
        {
            using (var ms = new MemoryStream(96))
            {
                using (var w = new Utf8JsonWriter(ms))
                {
                    w.WriteStartObject();
                    w.WriteString("code", code ?? string.Empty);
                    w.WriteString("action", "close");
                    w.WriteEndObject();
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
        catch { return string.Empty; }
    }

    internal static bool TryParse(string json, List<AmethystRoom> into, out int count, out string message)
    {
        count = 0;
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(json)) { message = "empty"; return false; }

        try
        {
            using (var doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) { message = "format"; return false; }

                if (root.TryGetProperty("ok", out JsonElement okEl) && okEl.ValueKind == JsonValueKind.False)
                {
                    message = Str(root, "message");
                    return false;
                }

                if (root.TryGetProperty("count", out JsonElement cEl) && cEl.ValueKind == JsonValueKind.Number)
                    cEl.TryGetInt32(out count);

                if (root.TryGetProperty("rooms", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in arr.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;

                        var r = new AmethystRoom
                        {
                            Code = Str(item, "code").ToUpperInvariant(),
                            Host = Str(item, "host"),
                            State = Str(item, "state"),
                            Map = Str(item, "map"),
                            Mode = Str(item, "mode"),
                            Region = Str(item, "region"),
                            Role = Str(item, "role"),
                            Source = Str(item, "source"),
                            SourceName = Str(item, "sourceName"),
                            Version = Str(item, "version"),
                            Note = Str(item, "note"),
                            Players = Int(item, "players"),
                            MaxPlayers = Int(item, "maxPlayers"),
                            Ping = Int(item, "ping"),
                            Age = Int(item, "age"),
                            Seen = Int(item, "seen")
                        };

                        if (r.Code.Length >= 4 && r.Code.Length <= 8) into.Add(r);
                    }
                }

                if (count <= 0) count = into.Count;
                return true;
            }
        }
        catch
        {
            message = "format";
            return false;
        }
    }

    private static string Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out JsonElement v)) return string.Empty;
        if (v.ValueKind == JsonValueKind.String) return v.GetString() ?? string.Empty;
        if (v.ValueKind == JsonValueKind.Number) return v.GetRawText();
        return string.Empty;
    }

    private static int Int(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out JsonElement v)) return 0;
        if (v.ValueKind == JsonValueKind.Number)
        {
            int i;
            return v.TryGetInt32(out i) ? i : 0;
        }
        if (v.ValueKind == JsonValueKind.String)
        {
            int i;
            return int.TryParse(v.GetString(), out i) ? i : 0;
        }
        return 0;
    }
}
