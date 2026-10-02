using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Input
{
    /// <summary>
    /// Allocation-free geometry helpers for validating taps against enemy paths
    /// (hero move orders, spell placement). Works in the XY plane.
    /// </summary>
    public static class PathUtility
    {
        /// <summary>Closest point to <paramref name="p"/> on segment a-b (XY plane).</summary>
        public static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            float abx = b.x - a.x, aby = b.y - a.y;
            float lenSq = abx * abx + aby * aby;
            if (lenSq < 1e-8f) return a;
            float t = ((p.x - a.x) * abx + (p.y - a.y) * aby) / lenSq;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return new Vector2(a.x + abx * t, a.y + aby * t);
        }

        /// <summary>Distance from <paramref name="p"/> to segment a-b.</summary>
        public static float DistanceToSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 c = ClosestPointOnSegment(a, b, p);
            float dx = p.x - c.x, dy = p.y - c.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Projects <paramref name="point"/> onto the nearest path segment. Returns
        /// true when that segment lies within <paramref name="maxDistance"/>.
        /// </summary>
        public static bool TryProjectOntoPaths(IReadOnlyList<IReadOnlyList<Vector3>> paths, Vector2 point,
                                               float maxDistance, out Vector3 closest)
        {
            closest = new Vector3(point.x, point.y, 0f);
            if (paths == null) return false;

            float bestSq = maxDistance * maxDistance;
            bool found = false;
            for (int i = 0; i < paths.Count; i++)
            {
                IReadOnlyList<Vector3> path = paths[i];
                if (path == null) continue;
                for (int s = 0; s + 1 < path.Count; s++)
                {
                    Vector3 a3 = path[s], b3 = path[s + 1];
                    Vector2 c = ClosestPointOnSegment(new Vector2(a3.x, a3.y), new Vector2(b3.x, b3.y), point);
                    float dx = point.x - c.x, dy = point.y - c.y;
                    float dSq = dx * dx + dy * dy;
                    if (dSq <= bestSq)
                    {
                        bestSq = dSq;
                        closest = new Vector3(c.x, c.y, 0f);
                        found = true;
                    }
                }
            }
            return found;
        }
    }
}
