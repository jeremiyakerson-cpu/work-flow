using System;

namespace TowerDefense.Core
{
    /// <summary>KR-style specialization branch chosen at level 2 -> 3.</summary>
    public enum UpgradeBranch
    {
        None,
        A,
        B
    }

    /// <summary>
    /// Engine-free copy of a TowerData asset's numbers (TowerData.ToUpgradeSpec()).
    /// Level 1 = base stats; 1->2 linear; 2->3 branch A or B; 3->4 final tier.
    /// </summary>
    public struct TowerUpgradeSpec
    {
        public float range, fireRate, damage;
        public int baseCost;
        public bool appliesSlow, appliesPoison;

        public float level2DamageMult, level2RangeMult, level2FireRateMult;
        public int level2Cost;

        public float pathADamageMult, pathARangeMult, pathAFireRateMult;
        public bool pathAAppliesSlow;
        public int pathACost;

        public float pathBDamageMult, pathBRangeMult, pathBFireRateMult;
        public bool pathBAppliesSlow;
        public int pathBCost;

        public float level4DamageMult, level4RangeMult, level4FireRateMult;
        public int level4Cost;

        /// <summary>Spec with every multiplier at 1 and every cost 0 - fill in what you need.</summary>
        public static TowerUpgradeSpec Neutral(float range, float fireRate, float damage, int baseCost)
        {
            return new TowerUpgradeSpec
            {
                range = range, fireRate = fireRate, damage = damage, baseCost = baseCost,
                level2DamageMult = 1f, level2RangeMult = 1f, level2FireRateMult = 1f,
                pathADamageMult = 1f, pathARangeMult = 1f, pathAFireRateMult = 1f,
                pathBDamageMult = 1f, pathBRangeMult = 1f, pathBFireRateMult = 1f,
                level4DamageMult = 1f, level4RangeMult = 1f, level4FireRateMult = 1f,
            };
        }
    }

    /// <summary>Live combat stats of a tower at one (level, branch).</summary>
    public struct TowerStats
    {
        public float Range;
        public float FireRate;
        public float Damage;
        public bool AppliesSlow;
        public bool AppliesPoison;

        /// <summary>Damage per second before resistances (Damage x FireRate).</summary>
        public float Dps => Damage * FireRate;
    }

    /// <summary>
    /// Upgrade math: stats, next cost and total investment for a tower at
    /// (level, branch), computed from base stats each time (no drift from
    /// repeated in-place multiplication). Tower.cs is a thin wrapper over this.
    /// </summary>
    public static class TowerUpgradeMath
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 4;
        /// <summary>Level at which the branch choice is made (2 -> 3).</summary>
        public const int BranchLevel = 2;

        /// <summary>True when (level, branch) is a reachable tower state.</summary>
        public static bool IsValidState(int level, UpgradeBranch branch)
        {
            if (level < MinLevel || level > MaxLevel) return false;
            bool needsBranch = level > BranchLevel;
            return needsBranch ? branch != UpgradeBranch.None : branch == UpgradeBranch.None;
        }

        /// <summary>Stats at (level, branch). Throws for unreachable states (e.g. level 3 without a branch).</summary>
        public static TowerStats StatsAt(in TowerUpgradeSpec spec, int level, UpgradeBranch branch)
        {
            Validate(level, branch);
            double dmg = Positive(spec.damage), rng = Positive(spec.range), rate = Positive(spec.fireRate);
            bool slow = spec.appliesSlow;

            if (level >= 2)
            {
                dmg *= Mult(spec.level2DamageMult);
                rng *= Mult(spec.level2RangeMult);
                rate *= Mult(spec.level2FireRateMult);
            }
            if (level >= 3)
            {
                if (branch == UpgradeBranch.A)
                {
                    dmg *= Mult(spec.pathADamageMult);
                    rng *= Mult(spec.pathARangeMult);
                    rate *= Mult(spec.pathAFireRateMult);
                    slow |= spec.pathAAppliesSlow;
                }
                else
                {
                    dmg *= Mult(spec.pathBDamageMult);
                    rng *= Mult(spec.pathBRangeMult);
                    rate *= Mult(spec.pathBFireRateMult);
                    slow |= spec.pathBAppliesSlow;
                }
            }
            if (level >= 4)
            {
                dmg *= Mult(spec.level4DamageMult);
                rng *= Mult(spec.level4RangeMult);
                rate *= Mult(spec.level4FireRateMult);
            }

            return new TowerStats
            {
                Damage = (float)dmg,
                Range = (float)rng,
                FireRate = (float)rate,
                AppliesSlow = slow,
                AppliesPoison = spec.appliesPoison,
            };
        }

        /// <summary>Cost of a specific branch at the level-2 choice.</summary>
        public static int BranchCost(in TowerUpgradeSpec spec, UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.A: return Cost(spec.pathACost);
                case UpgradeBranch.B: return Cost(spec.pathBCost);
                default: return Math.Min(Cost(spec.pathACost), Cost(spec.pathBCost));
            }
        }

        /// <summary>
        /// Gold for the next step from (level, branch). At level 2 it is the cheaper
        /// branch (the "from" price shown before a branch is picked). 0 at max level.
        /// </summary>
        public static int NextUpgradeCost(in TowerUpgradeSpec spec, int level, UpgradeBranch branch)
        {
            Validate(level, branch);
            switch (level)
            {
                case 1: return Cost(spec.level2Cost);
                case 2: return BranchCost(spec, UpgradeBranch.None);
                case 3: return Cost(spec.level4Cost);
                default: return 0;
            }
        }

        /// <summary>Everything spent to reach (level, branch), including the build cost.</summary>
        public static int TotalInvested(in TowerUpgradeSpec spec, int level, UpgradeBranch branch)
        {
            Validate(level, branch);
            long total = Cost(spec.baseCost);
            if (level >= 2) total += Cost(spec.level2Cost);
            if (level >= 3) total += BranchCost(spec, branch);
            if (level >= 4) total += Cost(spec.level4Cost);
            return (int)Math.Min(total, int.MaxValue);
        }

        /// <summary>Refund for selling a tower at (level, branch).</summary>
        public static int SellValue(in TowerUpgradeSpec spec, int level, UpgradeBranch branch, float refundFraction)
        {
            return EconomyRules.SellRefund(TotalInvested(spec, level, branch), refundFraction);
        }

        private static void Validate(int level, UpgradeBranch branch)
        {
            if (level < MinLevel || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Tower level must be 1..4.");
            if (!IsValidState(level, branch))
                throw new ArgumentException($"Branch {branch} is not valid at level {level}.", nameof(branch));
        }

        // Multipliers must be positive; a zero/negative/NaN entry in an asset is treated as "no change".
        private static double Mult(float m) => m > 0f && !float.IsInfinity(m) ? m : 1.0;
        private static double Positive(float v) => v > 0f && !float.IsInfinity(v) ? v : 0.0;
        private static int Cost(int c) => c > 0 ? c : 0;
    }
}
