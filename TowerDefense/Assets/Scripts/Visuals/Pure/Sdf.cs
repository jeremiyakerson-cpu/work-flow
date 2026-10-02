using System;

namespace TowerDefense.Visuals.Pure
{
    /// <summary>
    /// Signed distance function: negative inside the shape, positive outside,
    /// in the canvas' normalized units. Combinators keep the "distance is a
    /// lower bound" property the rasterizer's block culling relies on.
    /// </summary>
    public delegate float Sdf(float x, float y);

    /// <summary>
    /// Engine-free signed distance shapes and operators (2D). Shapes named
    /// Unit* fit inside a radius-1 circle centred on the origin; position them
    /// with <see cref="Transform"/>.
    /// </summary>
    public static class SdfShapes
    {
        // ------------------------------------------------------------ primitives

        public static Sdf Circle(float cx, float cy, float r) =>
            (x, y) => MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        /// <summary>Ellipse (Quilez' gradient-normalised approximation, exact on the axes).</summary>
        public static Sdf Ellipse(float cx, float cy, float rx, float ry) => (x, y) =>
        {
            float px = x - cx, py = y - cy;
            float k0 = MathF.Sqrt((px / rx) * (px / rx) + (py / ry) * (py / ry));
            float k1 = MathF.Sqrt((px / (rx * rx)) * (px / (rx * rx)) + (py / (ry * ry)) * (py / (ry * ry)));
            if (k1 < 1e-6f) return -MathF.Min(rx, ry);
            return k0 * (k0 - 1f) / k1;
        };

        /// <summary>Axis-aligned box with half extents (hw, hh) and corner radius.</summary>
        public static Sdf RoundedBox(float cx, float cy, float hw, float hh, float radius) => (x, y) =>
        {
            float r = MathF.Min(radius, MathF.Min(hw, hh));
            float qx = MathF.Abs(x - cx) - hw + r;
            float qy = MathF.Abs(y - cy) - hh + r;
            float ox = MathF.Max(qx, 0f), oy = MathF.Max(qy, 0f);
            return MathF.Sqrt(ox * ox + oy * oy) + MathF.Min(MathF.Max(qx, qy), 0f) - r;
        };

        /// <summary>Line segment a-b thickened by radius r (rounded ends).</summary>
        public static Sdf Capsule(float ax, float ay, float bx, float by, float r) => (x, y) =>
            SegmentDistance(x, y, ax, ay, bx, by) - r;

        /// <summary>Open polyline stroke of radius r through the points.</summary>
        public static Sdf Stroke(Vec2f[] points, float r)
        {
            var pts = (Vec2f[])points.Clone();
            return (x, y) =>
            {
                if (pts.Length == 1) return MathF.Sqrt((x - pts[0].X) * (x - pts[0].X) + (y - pts[0].Y) * (y - pts[0].Y)) - r;
                float best = float.MaxValue;
                for (int i = 1; i < pts.Length; i++)
                {
                    float d = SegmentDistance(x, y, pts[i - 1].X, pts[i - 1].Y, pts[i].X, pts[i].Y);
                    if (d < best) best = d;
                }
                return best - r;
            };
        }

        /// <summary>Exact signed distance to a simple polygon (any winding).</summary>
        public static Sdf Polygon(params Vec2f[] vertices)
        {
            if (vertices == null || vertices.Length < 3) throw new ArgumentException("Polygon needs at least 3 vertices.");
            var v = (Vec2f[])vertices.Clone();
            return (x, y) => PolygonDistance(v, x, y);
        }

        public static Sdf RegularPolygon(float cx, float cy, float r, int sides, float rotation = 0f)
        {
            var v = new Vec2f[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = rotation + MathF.PI * 0.5f + i * 2f * MathF.PI / sides;
                v[i] = new Vec2f(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r);
            }
            return Polygon(v);
        }

        public static Sdf Star(float cx, float cy, float outer, float inner, int points, float rotation = 0f)
        {
            var v = new Vec2f[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float a = rotation + MathF.PI * 0.5f + i * MathF.PI / points;
                float r = (i & 1) == 0 ? outer : inner;
                v[i] = new Vec2f(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r);
            }
            return Polygon(v);
        }

        /// <summary>Half plane: inside where (x,y)·n &lt; offset.</summary>
        public static Sdf HalfPlane(float nx, float ny, float offset)
        {
            float len = MathF.Sqrt(nx * nx + ny * ny);
            nx /= len; ny /= len;
            return (x, y) => x * nx + y * ny - offset;
        }

        // ------------------------------------------------------------ named unit shapes

        public static Sdf UnitTriangle() => RegularPolygon(0f, -0.1f, 0.95f, 3);
        public static Sdf UnitDiamond() => Polygon(new Vec2f(0f, 1f), new Vec2f(0.7f, 0f), new Vec2f(0f, -1f), new Vec2f(-0.7f, 0f));
        public static Sdf UnitHexagon() => RegularPolygon(0f, 0f, 0.95f, 6, MathF.PI / 6f);
        public static Sdf UnitStar() => Star(0f, -0.05f, 1f, 0.45f, 5);
        public static Sdf UnitRing(float thickness = 0.18f) => Annulus(Circle(0f, 0f, 0.9f - thickness * 0.5f), thickness * 0.5f);

        /// <summary>Heater shield: flat top, curved sides meeting in a point.</summary>
        public static Sdf UnitShield()
        {
            Sdf top = RoundedBox(0f, 0.35f, 0.75f, 0.55f, 0.12f);
            Sdf bottom = Intersect(Circle(-0.55f, 0.15f, 1.3f), Circle(0.55f, 0.15f, 1.3f));
            Sdf lower = Intersect(bottom, HalfPlane(0f, 1f, 0.35f));
            return Intersect(Union(top, lower), HalfPlane(0f, -1f, 0.98f));
        }

        public static Sdf UnitCrown()
        {
            var band = RoundedBox(0f, -0.45f, 0.85f, 0.22f, 0.06f);
            var spikes = Polygon(
                new Vec2f(-0.85f, -0.3f), new Vec2f(-0.85f, 0.45f), new Vec2f(-0.45f, 0.05f),
                new Vec2f(0f, 0.65f), new Vec2f(0.45f, 0.05f), new Vec2f(0.85f, 0.45f), new Vec2f(0.85f, -0.3f));
            return Union(band, spikes, Circle(-0.85f, 0.5f, 0.12f), Circle(0f, 0.72f, 0.13f), Circle(0.85f, 0.5f, 0.12f));
        }

        /// <summary>Arrow pointing along +x.</summary>
        public static Sdf UnitArrow() => Union(
            RoundedBox(-0.25f, 0f, 0.6f, 0.16f, 0.08f),
            Polygon(new Vec2f(0.2f, 0.55f), new Vec2f(0.95f, 0f), new Vec2f(0.2f, -0.55f)));

        /// <summary>Droplet with its tip pointing up.</summary>
        public static Sdf UnitDroplet() => SmoothUnion(
            Circle(0f, -0.3f, 0.6f),
            Polygon(new Vec2f(0f, 0.95f), new Vec2f(0.45f, -0.1f), new Vec2f(-0.45f, -0.1f)), 0.15f);

        // ------------------------------------------------------------ operators

        public static Sdf Union(Sdf a, Sdf b) => (x, y) => MathF.Min(a(x, y), b(x, y));

        public static Sdf Union(params Sdf[] shapes)
        {
            var s = (Sdf[])shapes.Clone();
            return (x, y) =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < s.Length; i++) { float v = s[i](x, y); if (v < d) d = v; }
                return d;
            };
        }

        public static Sdf Intersect(Sdf a, Sdf b) => (x, y) => MathF.Max(a(x, y), b(x, y));

        /// <summary>a minus b.</summary>
        public static Sdf Subtract(Sdf a, Sdf b) => (x, y) => MathF.Max(a(x, y), -b(x, y));

        /// <summary>Polynomial smooth minimum with blend radius k.</summary>
        public static Sdf SmoothUnion(Sdf a, Sdf b, float k) => (x, y) =>
        {
            float da = a(x, y), db = b(x, y);
            float h = Math.Clamp(0.5f + 0.5f * (db - da) / k, 0f, 1f);
            return db + (da - db) * h - k * h * (1f - h);
        };

        /// <summary>Grow (r &gt; 0) or shrink the shape, rounding convex corners.</summary>
        public static Sdf Round(Sdf a, float r) => (x, y) => a(x, y) - r;

        /// <summary>Hollow band of half-thickness t centred on the shape's edge.</summary>
        public static Sdf Annulus(Sdf a, float t) => (x, y) => MathF.Abs(a(x, y)) - t;

        public static Sdf Translate(Sdf a, float dx, float dy) => (x, y) => a(x - dx, y - dy);

        /// <summary>Uniform scale about the origin (distance stays exact).</summary>
        public static Sdf Scale(Sdf a, float s) => (x, y) => a(x / s, y / s) * s;

        /// <summary>Rotate counter-clockwise by radians about the origin.</summary>
        public static Sdf Rotate(Sdf a, float radians)
        {
            float c = MathF.Cos(-radians), s = MathF.Sin(-radians);
            return (x, y) => a(x * c - y * s, x * s + y * c);
        }

        /// <summary>Scale, rotate, then translate a unit shape.</summary>
        public static Sdf Transform(Sdf unit, float cx, float cy, float scale, float rotation = 0f)
        {
            Sdf s = scale == 1f ? unit : Scale(unit, scale);
            if (rotation != 0f) s = Rotate(s, rotation);
            return (cx == 0f && cy == 0f) ? s : Translate(s, cx, cy);
        }

        // ------------------------------------------------------------ distance helpers

        public static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float ex = bx - ax, ey = by - ay;
            float wx = px - ax, wy = py - ay;
            float len2 = ex * ex + ey * ey;
            float t = len2 > 1e-12f ? Math.Clamp((wx * ex + wy * ey) / len2, 0f, 1f) : 0f;
            float dx = wx - ex * t, dy = wy - ey * t;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public static float PolygonDistance(Vec2f[] v, float px, float py)
        {
            float dx0 = px - v[0].X, dy0 = py - v[0].Y;
            float d = dx0 * dx0 + dy0 * dy0;
            float s = 1f;
            int n = v.Length;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float ex = v[j].X - v[i].X, ey = v[j].Y - v[i].Y;
                float wx = px - v[i].X, wy = py - v[i].Y;
                float len2 = ex * ex + ey * ey;
                float t = len2 > 1e-12f ? Math.Clamp((wx * ex + wy * ey) / len2, 0f, 1f) : 0f;
                float bx = wx - ex * t, by = wy - ey * t;
                d = MathF.Min(d, bx * bx + by * by);
                bool c1 = py >= v[i].Y, c2 = py < v[j].Y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * MathF.Sqrt(d);
        }
    }

    /// <summary>Distance-to-coverage conversion used for anti-aliasing.</summary>
    public static class Coverage
    {
        /// <summary>
        /// Fraction of a pixel covered by a shape whose edge is at signed distance
        /// d, for a filter of width aa (one pixel for crisp AA, wider for softness).
        /// 1 deep inside, 0 far outside, 0.5 on the edge.
        /// </summary>
        public static float FromDistance(float d, float aa)
        {
            if (aa <= 1e-6f) return d <= 0f ? 1f : 0f;
            float t = 0.5f - d / aa;
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t); // smoothstep keeps soft edges soft without banding
        }
    }
}
