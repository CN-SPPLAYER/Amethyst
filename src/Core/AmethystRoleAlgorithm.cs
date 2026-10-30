using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppList = Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo>;

namespace Amethyst;

// 职业分配（开发者选项）。
//
// 参考：
//   · EHR    `Modules\Random\IRandom.cs`（Default / NetRandom / HashRandom / Xorshift / MersenneTwister）
//   · Onyx   `src\Host\NocturneForceRoles.cs`、`src\Patches\HnSPatches.cs`（躲猫猫）
//   · Elysium `cheats\EspAndRoleCheats.cs`
//
// 教训（上一版为什么会卡住/黑屏）：
//   参考模组在 `RoleManager.SelectRoles` 的 Prefix 里自行分配并 `return false` 跳过原版，
//   但那份代码还负责把角色 RPC 发出去。我们只调了 `AssignRolesForTeam` 就跳过，
//   客户端收不到角色 → 开始界面一直等 → 表现为「分配很久/卡住」。
//
// 现在的做法（不跳过原版，最稳）：
//   · 算法：只在分配前洗牌（原版照跑，RPC 照发）。
//   · 低人数生成内鬼：`SelectRoles` **Postfix** 里检查是否真的存在内鬼，若没有，
//     再随机挑一名玩家补成内鬼（补发 `RpcSetRole`）。原版流程完整，不会卡。
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

    // Fisher-Yates（参考 BetterAmongUs `ShuffleHelper`）。
    // ⚠ 必须先复制到 C# 托管 List 再重新 Clear/Add 回 IL2CPP List，
    // 绝不能在 IL2CPP List 上直接使用索引器 (players[i] = players[j]) 改写，
    // 否则会在 IL2CPP 原生指针迭代时引发 0xc0000005 内存冲突崩溃。
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

// 算法：普通模式分配前洗牌。
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
