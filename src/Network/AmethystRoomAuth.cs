using System;
using System.Security.Cryptography;
using System.Text;

namespace Amethyst;

// 房间上报的 HMAC 签名（2026-10 新鉴权）。
//
// 签名串（五行，用 \n 连接）：
//   POST
//   /home/api/room
//   <unix 秒>
//   <nonce>
//   <sha256(body) 小写 hex>
//
// signature = HMAC-SHA256(secret, 签名串) 的小写 hex。
// 请求头：X-Ace-Timestamp / X-Ace-Nonce / X-Ace-Signature。
//
// 关键：哈希必须针对**实际发送出去的 body 字节**，所以调用方先 ToBytes，
// 同一个 byte[] 既用于 Sign 也用于 UploadHandlerRaw。
internal static class AmethystRoomAuth
{
    internal static string Sign(string secret, string path, byte[] body, out string timestamp, out string nonce)
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        nonce = Guid.NewGuid().ToString("N");   // 32 位 hex，每次请求都不同

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
