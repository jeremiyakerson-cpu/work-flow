using System;
using System.Collections.Generic;

namespace TowerDefense.Visuals.Pure
{
    /// <summary>Decoration types the map renderer knows how to draw.</summary>
    public enum DecorKind { Tree, Pine, Bush, Rock, Flowers, Stump, Grass }

    /// <summary>One placed decoration (world units).</summary>
    public struct DecorItem
    {
        public float X;
        public float Y;
        public DecorKind Kind;
        public float Scale;
        public int Variant;
        public bool FlipX;
        /// <summary>True when placed in the forest band outside the play area.</summary>
        public bool Border;
    }

    /// <summary>Rules for <see cref="DecorScatter"/>. All distances in world units.</summary>
    public sealed class ScatterSettings
    {
        public uint Seed = 1;
        public float WorldWidth = 32f;
        public float WorldHeight = 18f;
        /// <summary>Interior decor keeps this far from the play-area edge.</summary>
        public float EdgeMargin = 0.6f;
        /// <summary>Clearance from a path's centre line (half path width + gap).</summary>
        public float PathClearance = 1.25f;
        /// <summary>Clearance from a build-slot centre.</summary>
        public float SlotClearance = 1.2f;
        /// <summary>Clearance from markers (hero start, portals, gates).</summary>
        public float MarkerClearance = 1.4f;
        /// <summary>Minimum centre-to-centre spacing between items (scaled by their radii).</summary>
        public float MinSpacing = 0.35f;
        /// <summary>Spacing in the border band; negative lets canopies overlap into a forest.</summary>
        public float BorderSpacing = -0.3f;
        /// <summary>Interior items per square world unit.</summary>
        public float InteriorDensity = 0.07f;
        /// <summary>Width of the forest band drawn outside the play area.</summary>
        public float BorderWidth = 6f;
        /// <summary>Border items per square world unit.</summary>
        public float BorderDensity = 0.5f;
        /// <summary>Dart-throwing attempts per wanted item.</summary>
        public int AttemptsPerItem = 12;
    }

    /// <summary>
    /// Deterministic decoration placement (dart throwing on a spatial hash).
    /// Rules: nothing on or near a path, a build slot or a marker; interior
    /// items stay off the play-area edge; a dense tree border fills the area
    /// outside the play rect (visible on wide phones and when zoomed out);
    /// items never overlap. Same settings + inputs → identical output.
    /// </summary>
    public static class DecorScatter
    {
        /// <summary>Footprint radius of a kind at scale 1 (used for clearance and spacing).</summary>
        public static float Radius(DecorKind kind)
        {
            switch (kind)
            {
                case DecorKind.Tree: return 0.62f;
                case DecorKind.Pine: return 0.5f;
                case DecorKind.Bush: return 0.38f;
                case DecorKind.Rock: return 0.32f;
                case DecorKind.Flowers: return 0.22f;
                case DecorKind.Stump: return 0.26f;
                default: return 0.18f;
            }
        }

        public static List<DecorItem> Scatter(ScatterSettings s, IReadOnlyList<IReadOnlyList<Vec2f>> paths,
                                              IReadOnlyList<Vec2f> slots, IReadOnlyList<Vec2f> markers)
        {
            var rng = new DeterministicRandom(s.Seed);
            var items = new List<DecorItem>();
            var grid = new SpatialHash(2f);

            // Interior first (sparser, more variety), then the forest border.
            float interiorArea = MathF.Max(0f, (s.WorldWidth - 2f * s.EdgeMargin) * (s.WorldHeight - 2f * s.EdgeMargin));
            int interiorWanted = (int)(interiorArea * s.InteriorDensity);
            Place(s, rng, grid, items, paths, slots, markers, interiorWanted, border: false);

            float outerArea = (s.WorldWidth + 2f * s.BorderWidth) * (s.WorldHeight + 2f * s.BorderWidth) - s.WorldWidth * s.WorldHeight;
            int borderWanted = (int)(outerArea * s.BorderDensity);
            Place(s, rng, grid, items, paths, slots, markers, borderWanted, border: true);
            return items;
        }

        /// <summary>True when an item of the given kind/scale may sit at (x, y). Exposed for tests.</summary>
        public static bool IsClear(ScatterSettings s, float x, float y, float radius, bool border,
                                   IReadOnlyList<IReadOnlyList<Vec2f>> paths, IReadOnlyList<Vec2f> slots, IReadOnlyList<Vec2f> markers)
        {
            var p = new Vec2f(x, y);
            if (border)
            {
                // Outside the play rect, inside the band.
                float d = PathGeometry.RectSignedDistance(p, s.WorldWidth, s.WorldHeight);
                if (d < radius * 0.5f || d > s.BorderWidth) return false;
            }
            else
            {
                float m = s.EdgeMargin + radius;
                if (x < m || x > s.WorldWidth - m || y < m || y > s.WorldHeight - m) return false;
            }

            if (paths != null)
                for (int i = 0; i < paths.Count; i++)
                    if (PathGeometry.DistanceToPolyline(p, paths[i]) < s.PathClearance + radius) return false;
            if (slots != null)
                for (int i = 0; i < slots.Count; i++)
                    if (Vec2f.Distance(p, slots[i]) < s.SlotClearance + radius) return false;
            if (markers != null)
                for (int i = 0; i < markers.Count; i++)
                    if (Vec2f.Distance(p, markers[i]) < s.MarkerClearance + radius) return false;
            return true;
        }

        private static void Place(ScatterSettings s, DeterministicRandom rng, SpatialHash grid, List<DecorItem> items,
                                  IReadOnlyList<IReadOnlyList<Vec2f>> paths, IReadOnlyList<Vec2f> slots, IReadOnlyList<Vec2f> markers,
                                  int wanted, bool border)
        {
            int placed = 0;
            int attempts = wanted * Math.Max(1, s.AttemptsPerItem);
            float minX = border ? -s.BorderWidth : 0f, maxX = border ? s.WorldWidth + s.BorderWidth : s.WorldWidth;
            float minY = border ? -s.BorderWidth : 0f, maxY = border ? s.WorldHeight + s.BorderWidth : s.WorldHeight;

            for (int a = 0; a < attempts && placed < wanted; a++)
            {
                float x = rng.Range(minX, maxX);
                float y = rng.Range(minY, maxY);
                DecorKind kind = PickKind(rng, border);
                float scale = border ? rng.Range(0.95f, 1.35f) : rng.Range(0.8f, 1.15f);
                float radius = Radius(kind) * scale;

                if (!IsClear(s, x, y, radius, border, paths, slots, markers)) continue;
                if (grid.Overlaps(x, y, radius, border ? s.BorderSpacing : s.MinSpacing)) continue;

                var item = new DecorItem
                {
                    X = x,
                    Y = y,
                    Kind = kind,
                    Scale = scale,
                    Variant = rng.Range(0, 4),
                    FlipX = rng.NextFloat() < 0.5f,
                    Border = border,
                };
                items.Add(item);
                grid.Add(x, y, radius);
                placed++;
            }
        }

        private static DecorKind PickKind(DeterministicRandom rng, bool border)
        {
            float r = rng.NextFloat();
            if (border)
            {
                if (r < 0.5f) return DecorKind.Tree;
                if (r < 0.85f) return DecorKind.Pine;
                if (r < 0.95f) return DecorKind.Bush;
                return DecorKind.Rock;
            }
            if (r < 0.16f) return DecorKind.Tree;
            if (r < 0.24f) return DecorKind.Pine;
            if (r < 0.42f) return DecorKind.Bush;
            if (r < 0.56f) return DecorKind.Rock;
            if (r < 0.74f) return DecorKind.Flowers;
            if (r < 0.80f) return DecorKind.Stump;
            return DecorKind.Grass;
        }

        /// <summary>Uniform-grid spatial hash of placed circles.</summary>
        private sealed class SpatialHash
        {
            private readonly float cell;
            private readonly Dictionary<long, List<(float x, float y, float r)>> cells = new Dictionary<long, List<(float, float, float)>>();
            private float maxRadius;

            public SpatialHash(float cellSize) { cell = cellSize; }

            private static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

            public void Add(float x, float y, float r)
            {
                long k = Key((int)MathF.Floor(x / cell), (int)MathF.Floor(y / cell));
                if (!cells.TryGetValue(k, out var list)) cells[k] = list = new List<(float, float, float)>();
                list.Add((x, y, r));
                if (r > maxRadius) maxRadius = r;
            }

            public bool Overlaps(float x, float y, float r, float spacing)
            {
                float reach = r + maxRadius + spacing;
                int x0 = (int)MathF.Floor((x - reach) / cell), x1 = (int)MathF.Floor((x + reach) / cell);
                int y0 = (int)MathF.Floor((y - reach) / cell), y1 = (int)MathF.Floor((y + reach) / cell);
                for (int cx = x0; cx <= x1; cx++)
                    for (int cy = y0; cy <= y1; cy++)
                    {
                        if (!cells.TryGetValue(Key(cx, cy), out var list)) continue;
                        foreach (var c in list)
                        {
                            float min = r + c.r + spacing;
                            float dx = c.x - x, dy = c.y - y;
                            if (dx * dx + dy * dy < min * min) return true;
                        }
                    }
                return false;
            }
        }
    }
}
