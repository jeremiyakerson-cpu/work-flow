using System;
using System.Collections.Generic;

namespace TowerDefense.Visuals.Pure
{
    /// <summary>Polyline helpers for drawing paths and placing markers along them.</summary>
    public static class PathGeometry
    {
        public static float Length(IReadOnlyList<Vec2f> points)
        {
            if (points == null) return 0f;
            float len = 0f;
            for (int i = 1; i < points.Count; i++) len += Vec2f.Distance(points[i - 1], points[i]);
            return len;
        }

        /// <summary>Shortest distance from p to the polyline (infinity for an empty one).</summary>
        public static float DistanceToPolyline(Vec2f p, IReadOnlyList<Vec2f> points)
        {
            if (points == null || points.Count == 0) return float.PositiveInfinity;
            if (points.Count == 1) return Vec2f.Distance(p, points[0]);
            float best = float.PositiveInfinity;
            for (int i = 1; i < points.Count; i++)
            {
                float d = SdfShapes.SegmentDistance(p.X, p.Y, points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Point at arc length <paramref name="distance"/> from the start (clamped to the ends).</summary>
        public static Vec2f PointAtDistance(IReadOnlyList<Vec2f> points, float distance)
        {
            if (points == null || points.Count == 0) return Vec2f.Zero;
            if (distance <= 0f || points.Count == 1) return points[0];
            for (int i = 1; i < points.Count; i++)
            {
                float seg = Vec2f.Distance(points[i - 1], points[i]);
                if (distance <= seg && seg > 0f) return Vec2f.Lerp(points[i - 1], points[i], distance / seg);
                distance -= seg;
            }
            return points[points.Count - 1];
        }

        /// <summary>Unit travel direction at arc length <paramref name="distance"/>.</summary>
        public static Vec2f DirectionAtDistance(IReadOnlyList<Vec2f> points, float distance)
        {
            if (points == null || points.Count < 2) return new Vec2f(1f, 0f);
            for (int i = 1; i < points.Count; i++)
            {
                float seg = Vec2f.Distance(points[i - 1], points[i]);
                if ((distance <= seg || i == points.Count - 1) && seg > 0f) return (points[i] - points[i - 1]).Normalized;
                distance -= seg;
            }
            return new Vec2f(1f, 0f);
        }

        /// <summary>
        /// First point along the path (from its start, or from its end when
        /// <paramref name="fromEnd"/>) lying inside the world rect shrunk by
        /// <paramref name="inset"/>. Spawn portals and exit gates sit there so they
        /// stay visible even when the path begins off-screen. Falls back to the
        /// endpoint if the path never enters the inset rect.
        /// </summary>
        public static Vec2f InsetEndpoint(IReadOnlyList<Vec2f> points, float worldW, float worldH, float inset, bool fromEnd)
        {
            if (points == null || points.Count == 0) return Vec2f.Zero;
            int n = points.Count;
            Vec2f Get(int k) => fromEnd ? points[n - 1 - k] : points[k];
            Vec2f first = Get(0);
            if (Inside(first, worldW, worldH, inset)) return first;
            const float step = 0.1f;
            for (int k = 1; k < n; k++)
            {
                Vec2f a = Get(k - 1), b = Get(k);
                float seg = Vec2f.Distance(a, b);
                int steps = Math.Max(1, (int)(seg / step));
                for (int s = 1; s <= steps; s++)
                {
                    Vec2f p = Vec2f.Lerp(a, b, (float)s / steps);
                    if (Inside(p, worldW, worldH, inset)) return p;
                }
            }
            return first;
        }

        public static bool Inside(Vec2f p, float worldW, float worldH, float inset) =>
            p.X >= inset && p.X <= worldW - inset && p.Y >= inset && p.Y <= worldH - inset;

        /// <summary>Signed distance from p to the rectangle [0,w]x[0,h] (negative inside).</summary>
        public static float RectSignedDistance(Vec2f p, float w, float h)
        {
            float qx = MathF.Abs(p.X - w * 0.5f) - w * 0.5f;
            float qy = MathF.Abs(p.Y - h * 0.5f) - h * 0.5f;
            float ox = MathF.Max(qx, 0f), oy = MathF.Max(qy, 0f);
            return MathF.Sqrt(ox * ox + oy * oy) + MathF.Min(MathF.Max(qx, qy), 0f);
        }
    }

    /// <summary>Orthographic camera framing math (pure, so it is testable for every device aspect).</summary>
    public static class CameraFit
    {
        /// <summary>
        /// Orthographic size (half the visible height) that shows the whole
        /// world rect on a screen of the given aspect (width / height), plus a
        /// fractional padding. Wider screens are height-bound, narrower ones
        /// (iPad 4:3) are width-bound.
        /// </summary>
        public static float OrthoSizeToFit(float worldWidth, float worldHeight, float aspect, float padding = 0f)
        {
            if (aspect <= 0f) aspect = 16f / 9f;
            float byHeight = worldHeight * 0.5f;
            float byWidth = worldWidth * 0.5f / aspect;
            return MathF.Max(byHeight, byWidth) * (1f + MathF.Max(0f, padding));
        }

        /// <summary>Orthographic size that fills the screen with world (crops the longer axis).</summary>
        public static float OrthoSizeToFill(float worldWidth, float worldHeight, float aspect)
        {
            if (aspect <= 0f) aspect = 16f / 9f;
            return MathF.Min(worldHeight * 0.5f, worldWidth * 0.5f / aspect);
        }

        /// <summary>Visible world width and height for an orthographic size and aspect.</summary>
        public static Vec2f VisibleSize(float orthoSize, float aspect) => new Vec2f(orthoSize * 2f * aspect, orthoSize * 2f);
    }

    /// <summary>Alpha profiles baked into small textures (path edges, vignette).</summary>
    public static class Profiles
    {
        /// <summary>
        /// Cross-section alpha for a line drawn with a stretched texture: v in
        /// [0,1] across the width, fully opaque in the middle, fading to 0 over
        /// <paramref name="fade"/> at each side (anti-aliased line edges).
        /// </summary>
        public static float LineEdge(float v, float fade)
        {
            float edge = MathF.Min(v, 1f - v);
            if (fade <= 0f) return edge >= 0f ? 1f : 0f;
            float t = Rgba.Clamp01(edge / fade);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Vignette darkness for a point at signed distance d from the play-area
        /// edge (negative inside): a faint inner falloff and a stronger fade
        /// outside, so the playable rect reads clearly on any screen shape.
        /// </summary>
        public static float Vignette(float d, float innerWidth = 3.5f, float outerWidth = 3f, float innerAlpha = 0.16f, float outerAlpha = 0.5f)
        {
            float inner = SmoothStep(-innerWidth, 0f, d) * innerAlpha;
            float outer = SmoothStep(0f, outerWidth, d) * (outerAlpha - innerAlpha);
            return Rgba.Clamp01(inner + outer);
        }

        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Rgba.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
