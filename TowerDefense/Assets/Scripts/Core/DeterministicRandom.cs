using System;

namespace TowerDefense.Core
{
    /// <summary>
    /// Small seeded PRNG (SplitMix64). Unlike System.Random its sequence is
    /// specified here, so a seed yields the same waves in the Editor, in the
    /// .NET test run and in an IL2CPP iOS build.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong state;

        public DeterministicRandom(ulong seed)
        {
            state = seed;
        }

        /// <summary>Stable seed for one independent stream, e.g. (run seed, wave number).</summary>
        public static ulong Combine(int seed, int stream)
        {
            ulong s = ((ulong)(uint)seed << 32) | (uint)stream;
            return Mix(s ^ 0xD1B54A32D192ED03UL);
        }

        public ulong NextULong()
        {
            state += 0x9E3779B97F4A7C15UL;
            return Mix(state);
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Uniform int in [minInclusive, maxExclusive). Returns minInclusive for an empty range.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextULong() % span));
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
