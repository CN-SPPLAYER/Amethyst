using System;
using System.Security.Cryptography;
using System.Text;

namespace Amethyst;

internal static class AmethystRoomAuth
{
    internal static string Sign(string secret, string path, byte[] body, out string timestamp, out string nonce)
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        nonce = Guid.NewGuid().ToString("N");   

        string bodyHash = Sha256Hex(body);
        string payload = "POST\n" + (path ?? "/home/api/room") + "\n" + timestamp + "\n" + nonce + "\n" + bodyHash;

        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret ?? string.Empty)))
            return Hex(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    internal static string Sha256Hex(byte[] data)
    {
        using (var sha = SHA256.Create())
            return Hex(sha.ComputeHash(data ?? Array.Empty<byte>()));
    }

    private static string Hex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
