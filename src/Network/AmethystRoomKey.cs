using System;
using System.Text;

namespace Amethyst;

internal static class AmethystRoomKey
{
    private const string Encoded = "DK5N5RH9RjqzYPMstFs71mL7c/gq/AJAuUHmGrN7NP04lHbne9VKJoBV6Q==";

    private static readonly byte[] Mask =
    {
        0x5A, 0xC3, 0x27, 0x91, 0x4E, 0xB8, 0x33, 0x6D,
        0xF1, 0x0C, 0x8A, 0x55, 0xD7, 0x1F, 0x6B, 0xA4
    };

    internal static string Default
    {
        get
        {
            try
            {
                byte[] data = Convert.FromBase64String(Encoded);
                for (int i = 0; i < data.Length; i++)
                    data[i] ^= Mask[i % Mask.Length];
                return Encoding.UTF8.GetString(data);
            }
            catch { return string.Empty; }
        }
    }
}
