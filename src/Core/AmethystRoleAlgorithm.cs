using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppList = Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo>;

namespace Amethyst;

internal static class AmethystRoleAlgorithm
{
    internal static int Mode
    {
        get
        {
            try { return AmethystConfig.RoleAlgorithm != null ? AmethystConfig.RoleAlgorithm.Value : 0; }
            catch { return 0; }
        }
    }

    private interface IRng
    {
        int Next(int minValue, int maxValue);
    }

    private static IRng NewRng()
    {
        return Mode switch
        {
            2 => new Xorshift(),
            3 => new MersenneTwister(),
            _ => new SystemRng(),
        };
    }

    internal static void Shuffle(Il2CppList players)
    {
        if (players == null || Mode <= 0 || players.Count < 2) return;
        try
        {
            var temp = new List<NetworkedPlayerInfo>();
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null) temp.Add(p);
            }
            if (temp.Count < 2) return;

            IRng rng = NewRng();
            for (int i = temp.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                var t = temp[i];
                temp[i] = temp[j];
                temp[j] = t;
            }

            players.Clear();
            for (int i = 0; i < temp.Count; i++)
            {
                players.Add(temp[i]);
            }
        }
        catch { }
    }

    private static void Shuffle(List<PlayerControl> players)
    {
        if (players == null || players.Count < 2) return;
        IRng rng = NewRng();
        for (int i = players.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            if (i == j) continue;
            PlayerControl tmp = players[i];
            players[i] = players[j];
            players[j] = tmp;
        }
    }

    internal static bool IsImp(PlayerControl p)
    {
        try { return p != null && p.Data != null && p.Data.Role != null && p.Data.Role.IsImpostor; }
        catch { return false; }
    }

    private sealed class SystemRng : IRng
    {
        private readonly System.Random _rng = new System.Random();
        public int Next(int minValue, int maxValue) => maxValue <= minValue ? minValue : _rng.Next(minValue, maxValue);
    }

    private sealed class Xorshift : IRng
    {
        private uint _num = (uint)DateTime.UtcNow.Ticks;
        public int Next(int minValue, int maxValue)
        {
            if (minValue >= maxValue) return minValue;
            uint x = _num;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _num = x;
            return (int)(minValue + x % (uint)(maxValue - minValue));
        }
    }

    private sealed class MersenneTwister : IRng
    {
        private const int N = 624;
        private const int M = 397;
        private const uint MatrixA = 0x9908b0df;
        private const uint UpperMask = 0x80000000;
        private const uint LowerMask = 0x7fffffff;
        private const uint TemperingMaskB = 0x9d2c5680;
        private const uint TemperingMaskC = 0xefc60000;

        private readonly uint[] _mt = new uint[N];
        private readonly uint[] _mag01 = { 0x0, MatrixA };
        private short _mtItems;

        public MersenneTwister()
        {
            _mt[0] = (uint)DateTime.UtcNow.Ticks & 0xffffffffU;
            for (_mtItems = 1; _mtItems < N; _mtItems++)
                _mt[_mtItems] = (uint)(1812433253U * (_mt[_mtItems - 1] ^ _mt[_mtItems - 1] >> 30) + _mtItems);
        }

        private uint Next()
        {
            uint y;
            if (_mtItems >= N)
            {
                short kk = 0;
                for (; kk < N - M; ++kk)
                {
                    y = _mt[kk] & UpperMask | _mt[kk + 1] & LowerMask;
                    _mt[kk] = _mt[kk + M] ^ y >> 1 ^ _mag01[y & 0x1];
                }
                for (; kk < N - 1; ++kk)
                {
                    y = _mt[kk] & UpperMask | _mt[kk + 1] & LowerMask;
                    _mt[kk] = _mt[kk + (M - N)] ^ y >> 1 ^ _mag01[y & 0x1];
                }
                y = _mt[N - 1] & UpperMask | _mt[0] & LowerMask;
                _mt[N - 1] = _mt[M - 1] ^ y >> 1 ^ _mag01[y & 0x1];
                _mtItems = 0;
            }

            y = _mt[_mtItems++];
            y ^= y >> 11;
            y ^= y << 7 & TemperingMaskB;
            y ^= y << 15 & TemperingMaskC;
            y ^= y >> 18;
            return y;
        }

        public int Next(int minValue, int maxValue)
        {
            if (minValue >= maxValue) return minValue;
            return (int)(minValue + Next() % (uint)(maxValue - minValue));
        }
    }
}

[HarmonyPatch(typeof(LogicRoleSelectionNormal), "AssignRolesFromList")]
internal static class AmethystRoleAlgorithmShufflePatch
{
    private static void Prefix(Il2CppList players)
    {
        try
        {
            if (players == null) return;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            AmethystRoleAlgorithm.Shuffle(players);
        }
        catch { }
    }
}
