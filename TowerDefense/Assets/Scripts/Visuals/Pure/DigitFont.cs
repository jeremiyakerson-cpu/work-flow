using System;
using System.Collections.Generic;

namespace TowerDefense.Visuals.Pure
{
    /// <summary>
    /// Tiny stroke font for floating combat/gold numbers (no TextMeshPro, no
    /// font assets). Each glyph is a set of polylines in a 1 x 1.4 cell drawn
    /// as thick rounded strokes, so it rasterizes crisply with an outline.
    /// </summary>
    public static class DigitFont
    {
        public const string Characters = "0123456789+-";
        public const float CellWidth = 1f;
        public const float CellHeight = 1.4f;
        /// <summary>Horizontal advance between glyph centres, in cell widths.</summary>
        public const float Advance = 0.78f;
        public const float StrokeRadius = 0.13f;

        private static readonly Dictionary<char, Vec2f[][]> Glyphs = Build();

        public static bool Supports(char c) => Glyphs.ContainsKey(c);

        /// <summary>Polylines for a glyph (cell coordinates, origin bottom-left).</summary>
        public static Vec2f[][] Strokes(char c) =>
            Glyphs.TryGetValue(c, out var s) ? s : throw new ArgumentException($"Glyph '{c}' not in DigitFont.");

        /// <summary>Signed distance of the glyph strokes, centred on the cell centre.</summary>
        public static Sdf GlyphShape(char c, float scale)
        {
            var strokes = Strokes(c);
            var parts = new Sdf[strokes.Length];
            for (int i = 0; i < strokes.Length; i++)
            {
                var pts = new Vec2f[strokes[i].Length];
                for (int k = 0; k < pts.Length; k++)
                    pts[k] = new Vec2f((strokes[i][k].X - CellWidth * 0.5f) * scale, (strokes[i][k].Y - CellHeight * 0.5f) * scale);
                parts[i] = SdfShapes.Stroke(pts, StrokeRadius * scale);
            }
            return parts.Length == 1 ? parts[0] : SdfShapes.Union(parts);
        }

        /// <summary>
        /// Writes the x offset (in cell widths, centred on 0) of each glyph of
        /// <paramref name="text"/> into <paramref name="offsets"/>; returns the
        /// number of glyphs written. Unsupported characters are skipped.
        /// Allocation-free so it can run when a popup spawns mid-game.
        /// </summary>
        public static int Layout(string text, float[] offsets)
        {
            if (string.IsNullOrEmpty(text) || offsets == null) return 0;
            int count = 0;
            for (int i = 0; i < text.Length && count < offsets.Length; i++)
                if (Supports(text[i])) count++;
            float start = -(count - 1) * Advance * 0.5f;
            for (int i = 0; i < count; i++) offsets[i] = start + i * Advance;
            return count;
        }

        /// <summary>
        /// Allocation-free formatting of a signed integer with an optional
        /// leading '+', into a reusable char buffer. Returns the length.
        /// </summary>
        public static int Format(int value, bool plusSign, char[] buffer)
        {
            int len = 0;
            if (value < 0) buffer[len++] = '-';
            else if (plusSign) buffer[len++] = '+';
            long v = Math.Abs((long)value);
            int start = len;
            do
            {
                buffer[len++] = (char)('0' + (int)(v % 10));
                v /= 10;
            } while (v > 0 && len < buffer.Length);
            Array.Reverse(buffer, start, len - start);
            return len;
        }

        private static Dictionary<char, Vec2f[][]> Build()
        {
            Vec2f P(float x, float y) => new Vec2f(x, y);
            const float L = 0.22f, R = 0.78f, B = 0.22f, M = 0.7f, T = 1.18f;
            return new Dictionary<char, Vec2f[][]>
            {
                ['0'] = new[] { new[] { P(L, B), P(R, B), P(R, T), P(L, T), P(L, B) } },
                ['1'] = new[] { new[] { P(0.3f, 0.98f), P(0.52f, T), P(0.52f, B) }, new[] { P(0.3f, B), P(0.74f, B) } },
                ['2'] = new[] { new[] { P(L, T), P(R, T), P(R, M), P(L, M), P(L, B), P(R, B) } },
                ['3'] = new[] { new[] { P(L, T), P(R, T), P(R, B), P(L, B) }, new[] { P(0.36f, M), P(R, M) } },
                ['4'] = new[] { new[] { P(L, T), P(L, M), P(R, M) }, new[] { P(0.68f, T), P(0.68f, B) } },
                ['5'] = new[] { new[] { P(R, T), P(L, T), P(L, M), P(R, M), P(R, B), P(L, B) } },
                ['6'] = new[] { new[] { P(R, T), P(L, T), P(L, B), P(R, B), P(R, M), P(L, M) } },
                ['7'] = new[] { new[] { P(L, T), P(R, T), P(0.42f, B) } },
                ['8'] = new[] { new[] { P(L, B), P(R, B), P(R, T), P(L, T), P(L, B) }, new[] { P(L, M), P(R, M) } },
                ['9'] = new[] { new[] { P(R, M), P(L, M), P(L, T), P(R, T), P(R, B), P(L, B) } },
                ['+'] = new[] { new[] { P(0.2f, M), P(0.8f, M) }, new[] { P(0.5f, 0.4f), P(0.5f, 1.0f) } },
                ['-'] = new[] { new[] { P(0.24f, M), P(0.76f, M) } },
            };
        }
    }
}
