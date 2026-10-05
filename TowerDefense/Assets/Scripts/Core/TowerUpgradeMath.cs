using System;

namespace TowerDefense.Core
{
    /// <summary>KR-style specialization chosen at level 3 -> 4 (the capstone).</summary>
    public enum UpgradeBranch
    {
        None,
        A,
        B
    }

    /// <summary>
    /// Engine-free copy of a TowerData asset's numbers (TowerData.ToUpgradeSpec()).
    /// Level 1 = base stats; 1->2 and 2->3 are linear upgrades (no choice);
    /// 3->4 specializes into elite branch A or B. Nothing comes after level 4.
    /// Each step multiplies damage, range and fire rate, multiplies poison DPS
    /// and adds to the splash radius. Every step stacks on the previous ones.
    /// </summary>
    public struct TowerUpgradeSpec
    {
        public float range, fireRate, damage;
        public int baseCost;
        /// <summary>Base splash radius (0 = single target).</summary>
        public float splashRadius;
        /// <summary>Base poison damage per second (only used when appliesPoison).</summary>
        public float poisonDps;
        public bool appliesSlow, appliesPoison;

        public float level2DamageMult, level2RangeMult, level2FireRateMult;
        public float level2PoisonMult, level2SplashBonus;
        public int level2Cost;

        public float level3DamageMult, level3RangeMult, level3FireRateMult;
        public float level3PoisonMult, level3SplashBonus;
        public int level3Cost;

        public float pathADamageMult, pathARangeMult, pathAFireRateMult;
        public float pathAPoisonMult, pathASplashBonus;
        public bool pathAAppliesSlow;
        public int pathACost;

        public float pathBDamageMult, pathBRangeMult, pathBFireRateMult;
        public float pathBPoisonMult, pathBSplashBonus;
        public bool pathBAppliesSlow;
        public int pathBCost;

        /// <summary>Spec with every multiplier at 1, every bonus and cost 0 - fill in what you need.</summary>
        public static TowerUpgradeSpec Neutral(float range, float fireRate, float damage, int baseCost)
        {
            return new TowerUpgradeSpec
            {
                range = range, fireRate = fireRate, damage = damage, baseCost = baseCost,
                level2DamageMult = 1f, level2RangeMult = 1f, level2FireRateMult = 1f, level2PoisonMult = 1f,
                level3DamageMult = 1f, level3RangeMult = 1f, level3FireRateMult = 1f, level3PoisonMult = 1f,
                pathADamageMult = 1f, pathARangeMult = 1f, pathAFireRateMult = 1f, pathAPoisonMult = 1f,
                pathBDamageMult = 1f, pathBRangeMult = 1f, pathBFireRateMult = 1f, pathBPoisonMult = 1f,
            };
        }
    }

    /// <summary>Live combat stats of a tower at one (level, branch).</summary>
    public struct TowerStats
    {
        public float Range;
        public float FireRate;
        public float Damage;
        /// <summary>Splash radius of each hit (0 = single target).</summary>
        public float SplashRadius;
        /// <summary>Poison damage per second on each poisoned enemy (0 when the tower doesn't poison).</summary>
        public float PoisonDps;
        public bool AppliesSlow;
        public bool AppliesPoison;

        /// <summary>Direct-hit damage per second before resistances (Damage x FireRate).</summary>
        public float Dps => Damage * FireRate;
        /// <summary>Direct DPS plus poison DPS on one target (poison refreshes, never stacks).</summary>
        public float TotalDps => Dps + PoisonDps;
    }

    /// <summary>
    /// Upgrade math: stats, costs and total investment for a tower at
    /// (level, branch), computed from base stats each time (no drift from
    /// repeated in-place multiplication). Tower.cs is a thin wrapper over this.
    /// Valid states: levels 1..3 with no branch, level 4 with branch A or B.
    /// </summary>
    public static class TowerUpgradeMath
    {
        public const int MinLevel = 1;
        /// <summary>Last level reached by a plain (choice-free) upgrade.</summary>
        public const int MaxLinearLevel = 3;
        /// <summary>The specialized (elite) level: the capstone.</summary>
        public const int MaxLevel = 4;

        /// <summary>True when (level, branch) is a reachable tower state.</summary>
        public static bool IsValidState(int level, UpgradeBranch branch)
        {
            if (level < MinLevel || level > MaxLevel) return false;
            if (branch != UpgradeBranch.None && branch != UpgradeBranch.A && branch != UpgradeBranch.B) return false;
            return level == MaxLevel ? branch != UpgradeBranch.None : branch == UpgradeBranch.None;
        }

        /// <summary>Stats at (level, branch). Throws for unreachable states (e.g. level 4 without a branch).</summary>
        public static TowerStats StatsAt(in TowerUpgradeSpec spec, int level, UpgradeBranch branch)
        {
            Validate(level, branch);
            double dmg = Positive(spec.damage), rng = Positive(spec.range), rate = Positive(spec.fireRate);
            double splash = Positive(spec.splashRadius);
            double poison = spec.appliesPoison ? Positive(spec.poisonDps) : 0.0;
            bool slow = spec.appliesSlow;

            if (level >= 2)
                Apply(ref dmg, ref rng, ref rate, ref poison, ref splash,
                      spec.level2DamageMult, spec.level2RangeMult, spec.level2FireRateMult, spec.level2PoisonMult, spec.level2SplashBonus);
            if (level >= 3)
                Apply(ref dmg, ref rng, ref rate, ref poison, ref splash,
                      spec.level3DamageMult, spec.level3RangeMult, spec.level3FireRateMult, spec.level3PoisonMult, spec.level3SplashBonus);
            if (level >= 4)
            {
                if (branch == UpgradeBranch.A)
                {
                    Apply(ref dmg, ref rng, ref rate, ref poison, ref splash,
                          spec.pathADamageMult, spec.pathARangeMult, spec.pathAFireRateMult, spec.pathAPoisonMult, spec.pathASplashBonus);
                    slow |= spec.pathAAppliesSlow;
                }
                else
                {
                    Apply(ref dmg, ref rng, ref rate, ref poison, ref splash,
                          spec.pathBDamageMult, spec.pathBRangeMult, spec.pathBFireRateMult, spec.pathBPoisonMult, spec.pathBSplashBonus);
                    slow |= spec.pathBAppliesSlow;
                }
            }

            return new TowerStats
            {
                Damage = (float)dmg,
                Range = (float)rng,
                FireRate = (float)rate,
                SplashRadius = (float)splash,
                PoisonDps = (float)poison,
                AppliesSlow = slow,
                AppliesPoison = spec.appliesPoison,
            };
        }

        /// <summary>Gold for the plain upgrade from <paramref name="level"/> (1 or 2). 0 at level 3 (specialize instead) and 4.</summary>
        public static int NextUpgradeCost(in TowerUpgradeSpec spec, int level, UpgradeBranch branch)
        {
            Validate(level, branch);
            switch (level)
            {
                case 1: return Cost(spec.level2Cost);
                case 2: return Cost(spec.level3Cost);
                default: return 0;
            }
        }

        /// <summary>Gold to specialize into <paramref name="branch"/> at level 3. Throws for UpgradeBranch.None.</summary>
        public static int BranchCost(in TowerUpgradeSpec spec, UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.A: return Cost(spec.pathACost);
                case UpgradeBranch.B: return Cost(spec.pathBCost);
                default: throw new ArgumentException($"No specialization cost for branch {branch}.", nameof(branch));
            }
        }

        /// <summary>Everything spent to reach (level, branch), including the build cost.</summary>
        public static int TotalInvested(in TowerUpgradeSpec spec, int level, UpgradeBranch branch)
        {
            Validate(level, branch);
            long total = Cost(spec.baseCost);
            if (level >= 2) total += Cost(spec.level2Cost);
            if (level >= 3) total += Cost(spec.level3Cost);
            if (level >= 4) total += BranchCost(spec, branch);
            return (int)Math.Min(total, int.MaxValue);
        }

        /// <summary>Refund for selling a tower at (level, branch).</summary>
        public static int SellValue(in TowerUpgradeSpec spec, int level, UpgradeBranch branch, float refundFraction)
        {
            return EconomyRules.SellRefund(TotalInvested(spec, level, branch), refundFraction);
        }

        private static void Apply(ref double dmg, ref double rng, ref double rate, ref double poison, ref double splash,
                                  float dmgMult, float rangeMult, float rateMult, float poisonMult, float splashBonus)
        {
            dmg *= Mult(dmgMult);
            rng *= Mult(rangeMult);
            rate *= Mult(rateMult);
            poison *= Mult(poisonMult);
            splash += Positive(splashBonus);
        }

        private static void Validate(int level, UpgradeBranch branch)
        {
            if (level < MinLevel || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Tower level must be 1..4.");
            if (!IsValidState(level, branch))
                throw new ArgumentException($"Branch {branch} is not valid at level {level}.", nameof(branch));
        }

        // Multipliers must be positive; a zero/negative/NaN/infinite entry in an asset is treated as "no change".
        private static double Mult(float m) => m > 0f && !float.IsInfinity(m) ? m : 1.0;
        // Base values and splash bonuses never go negative (NaN/infinite -> 0).
        private static double Positive(float v) => v > 0f && !float.IsInfinity(v) ? v : 0.0;
        private static int Cost(int c) => c > 0 ? c : 0;
    }
}
