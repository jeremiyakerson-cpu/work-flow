using System;
using System.Collections.Generic;

namespace TowerDefense.Levels
{
    /// <summary>
    /// Engine-free 2D point (world units). Level layouts are authored with this
    /// so their geometry can be validated on plain .NET, outside Unity.
    /// </summary>
    [Serializable]
    public struct LayoutPoint
    {
        public float X;
        public float Y;

        public LayoutPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }

    /// <summary>
    /// Pure geometry helpers used by the level validator. No UnityEngine
    /// dependency, so they run in the .NET content tests with real numbers.
    /// </summary>
    public static class LevelGeometry
    {
        public static float Distance(LayoutPoint a, LayoutPoint b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Shortest distance from p to the segment a-b (handles a == b).</summary>
        public static float PointSegmentDistance(LayoutPoint p, LayoutPoint a, LayoutPoint b)
        {
            float abx = b.X - a.X, aby = b.Y - a.Y;
            float lenSq = abx * abx + aby * aby;
            if (lenSq <= 1e-12f) return Distance(p, a);
            float t = ((p.X - a.X) * abx + (p.Y - a.Y) * aby) / lenSq;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            var closest = new LayoutPoint(a.X + abx * t, a.Y + aby * t);
            return Distance(p, closest);
        }

        /// <summary>Shortest distance from p to any segment of the polyline. +Infinity if it has no segment.</summary>
        public static float DistanceToPolyline(LayoutPoint p, IReadOnlyList<LayoutPoint> polyline)
        {
            if (polyline == null || polyline.Count == 0) return float.PositiveInfinity;
            if (polyline.Count == 1) return Distance(p, polyline[0]);
            float best = float.PositiveInfinity;
            for (int i = 1; i < polyline.Count; i++)
            {
                float d = PointSegmentDistance(p, polyline[i - 1], polyline[i]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Shortest distance from p to any segment of any path.</summary>
        public static float DistanceToPaths(LayoutPoint p, IReadOnlyList<IReadOnlyList<LayoutPoint>> paths)
        {
            float best = float.PositiveInfinity;
            if (paths == null) return best;
            for (int i = 0; i < paths.Count; i++)
            {
                float d = DistanceToPolyline(p, paths[i]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Total length of a polyline.</summary>
        public static float PolylineLength(IReadOnlyList<LayoutPoint> polyline)
        {
            if (polyline == null) return 0f;
            float total = 0f;
            for (int i = 1; i < polyline.Count; i++) total += Distance(polyline[i - 1], polyline[i]);
            return total;
        }

        /// <summary>True if p lies inside [margin, size - margin] on both axes.</summary>
        public static bool InsideBounds(LayoutPoint p, float width, float height, float margin)
        {
            return p.X >= margin && p.X <= width - margin && p.Y >= margin && p.Y <= height - margin;
        }

        /// <summary>
        /// Signed distance from p to the nearest edge of the play area: positive
        /// inside, zero on the edge, negative outside.
        /// </summary>
        public static float SignedDistanceToEdge(LayoutPoint p, float width, float height)
        {
            float inside = Math.Min(Math.Min(p.X, width - p.X), Math.Min(p.Y, height - p.Y));
            if (inside >= 0f) return inside;
            // Outside: return minus the distance to the rectangle.
            float dx = p.X < 0f ? -p.X : (p.X > width ? p.X - width : 0f);
            float dy = p.Y < 0f ? -p.Y : (p.Y > height ? p.Y - height : 0f);
            return -(float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Distance from p to the play rectangle (0 if inside).</summary>
        public static float DistanceOutside(LayoutPoint p, float width, float height)
        {
            float s = SignedDistanceToEdge(p, width, height);
            return s < 0f ? -s : 0f;
        }
    }
}
