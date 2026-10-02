namespace TowerDefense.Visuals.Pure
{
    /// <summary>
    /// Small, platform-stable PRNG (xorshift32) for deterministic decoration
    /// layouts: the same seed produces the same map on every device and run,
    /// unlike System.Random whose algorithm is not guaranteed across runtimes.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed;
            // Warm up so close seeds diverge quickly.
            for (int i = 0; i < 4; i++) NextUInt();
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform integer in [min, max).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min));
    }

    /// <summary>Stable string hashing (FNV-1a). string.GetHashCode is randomised per process on .NET.</summary>
    public static class StableHash
    {
        public static uint Fnv1a(string text)
        {
            uint hash = 2166136261u;
            if (text == null) return hash;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            return hash;
        }

        /// <summary>Integer lattice hash in [0,1) for value noise.</summary>
        public static float Lattice(int x, int y, uint seed)
        {
            uint h = seed ^ ((uint)x * 0x27D4EB2Du) ^ ((uint)y * 0x165667B1u);
            h ^= h >> 15;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }
    }

    /// <summary>Tileable value noise for seamless ground textures.</summary>
    public static class TileableNoise
    {
        /// <summary>
        /// Smooth value noise at (x, y) in lattice units, wrapping every
        /// <paramref name="period"/> cells so a texture built from it tiles.
        /// </summary>
        public static float Value(float x, float y, int period, uint seed)
        {
            int x0 = FloorToInt(x), y0 = FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            int ax = Wrap(x0, period), bx = Wrap(x0 + 1, period);
            int ay = Wrap(y0, period), by = Wrap(y0 + 1, period);
            float a = StableHash.Lattice(ax, ay, seed), b = StableHash.Lattice(bx, ay, seed);
            float c = StableHash.Lattice(ax, by, seed), d = StableHash.Lattice(bx, by, seed);
            float top = a + (b - a) * sx, bottom = c + (d - c) * sx;
            return top + (bottom - top) * sy;
        }

        /// <summary>Two-octave fractal noise in [0,1), tileable with the base period.</summary>
        public static float Fractal(float x, float y, int period, uint seed)
        {
            float n = Value(x, y, period, seed) * 0.65f;
            n += Value(x * 2f, y * 2f, period * 2, seed + 101u) * 0.35f;
            return n;
        }

        private static int FloorToInt(float v) => v >= 0f ? (int)v : (int)v - ((int)v == v ? 0 : 1);
        private static int Wrap(int v, int period) => ((v % period) + period) % period;
    }
}
