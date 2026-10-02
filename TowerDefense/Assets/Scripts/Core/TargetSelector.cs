namespace TowerDefense.Core
{
    /// <summary>One enemy in range, flattened for target selection.</summary>
    public struct TargetCandidate
    {
        /// <summary>World distance travelled along its path (Enemy.PathProgress).</summary>
        public float Progress;
        /// <summary>World distance left to the exit. Lower = more dangerous.</summary>
        public float RemainingDistance;
        public float Health;
        /// <summary>Squared distance from the tower (squared avoids a sqrt per candidate).</summary>
        public float DistanceSqr;
        public bool IsFlying;

        public TargetCandidate(float progress, float remainingDistance, float health, float distanceSqr, bool isFlying)
        {
            Progress = progress;
            RemainingDistance = remainingDistance;
            Health = health;
            DistanceSqr = distanceSqr;
            IsFlying = isFlying;
        }
    }

    /// <summary>
    /// Picks which candidate a tower shoots, per TargetPriority. Pure and
    /// allocation-free: callers pass a reused buffer and a count.
    /// "First" means closest to the exit (least RemainingDistance), so it stays
    /// correct on maps with several paths of different lengths. Ties on any
    /// priority fall back to "First", then to the lower index (stable).
    /// </summary>
    public static class TargetSelector
    {
        /// <summary>Index of the best candidate in [0, count), or -1 if none is targetable.</summary>
        public static int SelectIndex(TargetCandidate[] candidates, int count, TargetPriority priority,
                                      bool canTargetGround = true, bool canTargetFlying = true)
        {
            if (candidates == null) return -1;
            if (count > candidates.Length) count = candidates.Length;

            int best = -1;
            for (int i = 0; i < count; i++)
            {
                ref readonly TargetCandidate c = ref candidates[i];
                if (c.IsFlying ? !canTargetFlying : !canTargetGround) continue;
                if (best < 0 || IsBetter(in c, in candidates[best], priority)) best = i;
            }
            return best;
        }

        /// <summary>True if a should be preferred over b under this priority.</summary>
        public static bool IsBetter(in TargetCandidate a, in TargetCandidate b, TargetPriority priority)
        {
            int cmp;
            switch (priority)
            {
                case TargetPriority.Last: cmp = HigherWins(a.RemainingDistance, b.RemainingDistance); break; // furthest from exit
                case TargetPriority.Strongest: cmp = HigherWins(a.Health, b.Health); break;
                case TargetPriority.Weakest: cmp = LowerWins(a.Health, b.Health); break;
                case TargetPriority.Closest: cmp = LowerWins(a.DistanceSqr, b.DistanceSqr); break;
                default: cmp = 0; break;
            }
            if (cmp != 0) return cmp > 0;
            return IsFurtherAlong(in a, in b);
        }

        private static bool IsFurtherAlong(in TargetCandidate a, in TargetCandidate b)
        {
            int cmp = LowerWins(a.RemainingDistance, b.RemainingDistance);
            if (cmp == 0) cmp = HigherWins(a.Progress, b.Progress);
            return cmp > 0;
        }

        // >0 when x wins. NaN always loses so a corrupted candidate is never preferred.
        private static int HigherWins(float x, float y)
        {
            int n = NaNRule(x, y);
            if (n != 2) return n;
            return x > y ? 1 : (x < y ? -1 : 0);
        }

        private static int LowerWins(float x, float y)
        {
            int n = NaNRule(x, y);
            if (n != 2) return n;
            return x < y ? 1 : (x > y ? -1 : 0);
        }

        // 2 = neither is NaN, compare normally.
        private static int NaNRule(float x, float y)
        {
            bool xn = float.IsNaN(x), yn = float.IsNaN(y);
            if (!xn && !yn) return 2;
            return xn == yn ? 0 : (xn ? -1 : 1);
        }
    }
}
