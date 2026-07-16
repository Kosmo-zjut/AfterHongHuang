using System;
using System.Collections.Generic;

/// <summary>
/// xoshiro256** 伪随机数生成器，使用 SplitMix64 初始化四个 64 位状态。
/// 算法状态完全由输入 seed 决定，不依赖平台内置随机实现或时间。
/// </summary>
public sealed class StableRandom
{
    private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15UL;
    private ulong _splitMixState;
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public StableRandom(ulong seed)
    {
        _splitMixState = seed;
        _s0 = NextSplitMix64();
        _s1 = NextSplitMix64();
        _s2 = NextSplitMix64();
        _s3 = NextSplitMix64();
        if ((_s0 | _s1 | _s2 | _s3) == 0)
            _s0 = 1;
    }

    public ulong NextUInt64()
    {
        ulong result = RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);
        return result;
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));

        ulong range = (ulong)((long)maxExclusive - minInclusive);
        ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
        ulong sample;
        do
        {
            sample = NextUInt64();
        } while (sample >= limit);

        return minInclusive + (int)(sample % range);
    }

    public bool NextBool() => (NextUInt64() & 1UL) != 0;

    /// <summary>Fisher-Yates 洗牌，所有交换索引来自本实例，调用方可复现。</summary>
    public void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private ulong NextSplitMix64()
    {
        ulong z = (_splitMixState += SplitMixIncrement);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong RotateLeft(ulong value, int shift)
    {
        return (value << shift) | (value >> (64 - shift));
    }
}
