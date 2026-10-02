using System;
using System.Collections.Generic;

namespace TowerDefense.Core
{
    /// <summary>
    /// Engine-free description of one enemy type (EnemyData.ToTypeInfo()).
    /// Plans reference types by their index in the list passed to the planner.
    /// </summary>
    public struct EnemyTypeInfo
    {
        public float BaseHealth;
        public float BaseSpeed;
        public int BaseReward;
        /// <summary>First wave this type may appear in.</summary>
        public int UnlockWave;
        /// <summary>Relative pick chance among unlocked types. 0 (or less) = never picked.</summary>
        public float SpawnWeight;
        public bool IsFlying;
        /// <summary>Boss-pool entries: multiplies the wave's health curve.</summary>
        public float BossHealthMultiplier;
        /// <summary>Boss-pool entries: multiplies the wave's base reward.</summary>
        public float BossRewardMultiplier;

        public EnemyTypeInfo(float baseHealth, float baseSpeed, int baseReward, int unlockWave = 1, float spawnWeight = 1f,
                             bool isFlying = false, float bossHealthMultiplier = 8f, float bossRewardMultiplier = 20f)
        {
            BaseHealth = baseHealth;
            BaseSpeed = baseSpeed;
            BaseReward = baseReward;
            UnlockWave = unlockWave;
            SpawnWeight = spawnWeight;
            IsFlying = isFlying;
            BossHealthMultiplier = bossHealthMultiplier;
            BossRewardMultiplier = bossRewardMultiplier;
        }

        /// <summary>A placeholder for a missing (null) pool entry: never picked.</summary>
        public static EnemyTypeInfo Unavailable => new EnemyTypeInfo { UnlockWave = int.MaxValue, SpawnWeight = 0f };

        public bool IsPickable => SpawnWeight > 0f && !float.IsInfinity(SpawnWeight);
    }

    /// <summary>
    /// Difficulty curve parameters (mirrors WaveManager's Inspector fields).
    /// Defaults reproduce the original WaveManager math.
    /// </summary>
    public sealed class WaveCurve
    {
        public float BaseHealth = 10f;
        /// <summary>Multiplicative - compounds every wave.</summary>
        public float HealthGrowthPerWave = 1.12f;
        public float BaseSpeed = 2f;
        public float SpeedGrowthPerWave = 1.01f;
        /// <summary>Cap on the compounded speed scale (0 = uncapped). Keeps endless waves catchable by projectiles.</summary>
        public float MaxSpeedScale = 0f;
        public int BaseGoldReward = 5;
        /// <summary>Linear reward growth per wave (0.05 = +5% of base per wave).</summary>
        public float RewardGrowthPerWave = 0.05f;
        /// <summary>Per-level scale on the health curve (LevelData.difficultyMultiplier).</summary>
        public float DifficultyMultiplier = 1f;

        public int BaseEnemyCount = 6;
        public int EnemyCountGrowthPerWave = 1;
        /// <summary>Cap on enemies per regular wave (0 = uncapped). Health keeps compounding past the cap.</summary>
        public int MaxEnemiesPerWave = 0;
        public float SpawnInterval = 0.6f;

        public int BossEveryNWaves = 10;
        public float BossSpeedMultiplier = 0.6f;
        /// <summary>Regular enemies escorting a boss, as a fraction of a normal wave's count (0 = boss alone).</summary>
        public float BossEscortFraction = 0f;

        /// <summary>The "reference" enemy: a type with these base stats gets exactly the curve's values.</summary>
        public float ReferenceHealth = 10f;
        public float ReferenceSpeed = 2f;
        public float ReferenceReward = 5f;
    }

    /// <summary>One scheduled spawn inside a wave.</summary>
    public struct WaveSpawn
    {
        /// <summary>Index into the enemy-type list (or the boss list when IsBoss).</summary>
        public int TypeIndex;
        public bool IsBoss;
        public float Health;
        public float Speed;
        public int Reward;
        /// <summary>Seconds to wait after the previous spawn (0 for the first).</summary>
        public float Delay;
        /// <summary>Which path (0..pathCount-1) the enemy walks.</summary>
        public int PathIndex;
    }

    /// <summary>A fully resolved wave: who spawns, when, where and with which stats.</summary>
    public sealed class WavePlan
    {
        public int WaveNumber { get; internal set; }
        public bool IsBossWave { get; internal set; }
        /// <summary>Health curve value for the reference enemy this wave.</summary>
        public float HealthScale { get; internal set; }
        public float SpeedScale { get; internal set; }
        public IReadOnlyList<WaveSpawn> Spawns => spawns;
        /// <summary>Seconds from the first spawn to the last.</summary>
        public float Duration { get; internal set; }
        public double TotalHealth { get; internal set; }
        public long TotalReward { get; internal set; }

        internal readonly List<WaveSpawn> spawns = new List<WaveSpawn>();
    }

    /// <summary>
    /// Deterministic wave composition. The same (wave, curve, types, pathCount,
    /// seed) always produces the same plan, and each wave uses its own random
    /// stream, so planning wave 7 doesn't depend on whether waves 1-6 were
    /// planned (UI previews are safe). Stats never go NaN or negative; health
    /// is capped at <see cref="MaxHealth"/> so endless mode can't overflow.
    /// </summary>
    public static class WavePlanner
    {
        public const float MaxHealth = 1e9f;
        public const float MaxSpeed = 1000f;

        public static bool IsBossWave(int waveNumber, int bossEveryNWaves, int bossTypeCount) =>
            waveNumber > 0 && bossEveryNWaves > 0 && bossTypeCount > 0 && waveNumber % bossEveryNWaves == 0;

        /// <summary>Health for the reference enemy on this wave (difficulty multiplier included).</summary>
        public static float HealthAt(WaveCurve curve, int waveNumber)
        {
            double h = Positive(curve.BaseHealth) * Positive(curve.DifficultyMultiplier, 1.0) *
                       Math.Pow(Positive(curve.HealthGrowthPerWave, 1.0), WaveIndex(waveNumber));
            return ClampFloat(h, MaxHealth);
        }

        /// <summary>Speed multiplier from compounding growth, honouring MaxSpeedScale.</summary>
        public static float SpeedScaleAt(WaveCurve curve, int waveNumber)
        {
            double s = Math.Pow(Positive(curve.SpeedGrowthPerWave, 1.0), WaveIndex(waveNumber));
            if (curve.MaxSpeedScale > 0f && s > curve.MaxSpeedScale) s = curve.MaxSpeedScale;
            return ClampFloat(s, MaxSpeed);
        }

        public static float SpeedAt(WaveCurve curve, int waveNumber) =>
            ClampFloat(Positive(curve.BaseSpeed) * SpeedScaleAt(curve, waveNumber), MaxSpeed);

        /// <summary>Regular-wave enemy count: base + growth x (wave-1), at least 1, capped by MaxEnemiesPerWave.</summary>
        public static int EnemyCountAt(WaveCurve curve, int waveNumber)
        {
            long n = (long)curve.BaseEnemyCount + (long)Math.Max(0, curve.EnemyCountGrowthPerWave) * WaveIndex(waveNumber);
            if (curve.MaxEnemiesPerWave > 0 && n > curve.MaxEnemiesPerWave) n = curve.MaxEnemiesPerWave;
            return (int)Math.Max(1, Math.Min(n, 100_000));
        }

        /// <summary>
        /// Build the plan for one wave. enemyTypes/bossTypes may be null or empty;
        /// a regular wave with no pickable type comes back with no spawns.
        /// </summary>
        public static WavePlan Plan(int waveNumber, WaveCurve curve, IReadOnlyList<EnemyTypeInfo> enemyTypes,
                                    IReadOnlyList<EnemyTypeInfo> bossTypes, int pathCount, int seed)
        {
            if (curve == null) throw new ArgumentNullException(nameof(curve));
            int wave = Math.Max(1, waveNumber);
            int paths = Math.Max(1, pathCount);
            var rng = new DeterministicRandom(DeterministicRandom.Combine(seed, wave));

            float health = HealthAt(curve, wave);
            float speed = SpeedAt(curve, wave);
            float interval = curve.SpawnInterval > 0f && !float.IsInfinity(curve.SpawnInterval) ? curve.SpawnInterval : 0f;

            var plan = new WavePlan
            {
                WaveNumber = wave,
                HealthScale = health,
                SpeedScale = SpeedScaleAt(curve, wave),
                IsBossWave = IsBossWave(wave, curve.BossEveryNWaves, CountPickable(bossTypes)),
            };

            int regularCount;
            int pathCursor = 0;
            if (plan.IsBossWave)
            {
                int bossIndex = PickWeighted(bossTypes, wave, rng, ignoreUnlock: false);
                EnemyTypeInfo boss = bossTypes[bossIndex];
                int waveReward = EconomyRules.KillReward(curve.BaseGoldReward, wave, curve.RewardGrowthPerWave);
                plan.spawns.Add(new WaveSpawn
                {
                    TypeIndex = bossIndex,
                    IsBoss = true,
                    Health = ClampFloat((double)health * Positive(boss.BossHealthMultiplier, 1.0), MaxHealth),
                    Speed = ClampFloat((double)speed * Positive(curve.BossSpeedMultiplier, 1.0), MaxSpeed),
                    Reward = Math.Max(1, EconomyRules.RoundGold(waveReward * Positive(boss.BossRewardMultiplier, 1.0))),
                    Delay = 0f,
                    PathIndex = rng.Range(0, paths),
                });
                pathCursor = plan.spawns[0].PathIndex + 1;
                double escort = EnemyCountAt(curve, wave) * Positive(curve.BossEscortFraction);
                regularCount = (int)Math.Round(Math.Min(escort, 100_000), MidpointRounding.AwayFromZero);
            }
            else
            {
                regularCount = EnemyCountAt(curve, wave);
            }

            if (regularCount > 0 && CountPickable(enemyTypes) > 0)
            {
                int first = plan.spawns.Count;
                for (int i = 0; i < regularCount; i++)
                {
                    int typeIndex = PickWeighted(enemyTypes, wave, rng, ignoreUnlock: false);
                    float delay = plan.spawns.Count == 0 ? 0f : (i == 0 ? interval * 2f : interval);
                    plan.spawns.Add(MakeSpawn(curve, enemyTypes, typeIndex, wave, health, speed, delay, (pathCursor + i) % paths));
                }
                GuaranteeNewTypes(plan, curve, enemyTypes, wave, health, speed, first);
            }

            foreach (var s in plan.spawns)
            {
                plan.Duration += s.Delay;
                plan.TotalHealth += s.Health;
                plan.TotalReward += s.Reward;
            }
            return plan;
        }

        /// <summary>Stats for one regular enemy of a type on a wave (used by plans and by summons).</summary>
        public static WaveSpawn MakeSpawn(WaveCurve curve, IReadOnlyList<EnemyTypeInfo> types, int typeIndex, int waveNumber,
                                          float waveHealth, float waveSpeed, float delay, int pathIndex)
        {
            EnemyTypeInfo t = types[typeIndex];
            return new WaveSpawn
            {
                TypeIndex = typeIndex,
                IsBoss = false,
                Health = ClampFloat(waveHealth * Ratio(t.BaseHealth, curve.ReferenceHealth), MaxHealth),
                Speed = ClampFloat(waveSpeed * Ratio(t.BaseSpeed, curve.ReferenceSpeed), MaxSpeed),
                Reward = EconomyRules.KillReward(curve.BaseGoldReward, waveNumber, curve.RewardGrowthPerWave,
                                                 Ratio(t.BaseReward, curve.ReferenceReward)),
                Delay = delay,
                PathIndex = pathIndex,
            };
        }

        /// <summary>
        /// Weighted pick among types unlocked by this wave. If none is unlocked yet,
        /// the earliest-unlocking types are used so a wave is never silently empty.
        /// Returns -1 only when no type is pickable at all.
        /// </summary>
        public static int PickWeighted(IReadOnlyList<EnemyTypeInfo> types, int waveNumber, DeterministicRandom rng, bool ignoreUnlock)
        {
            if (types == null || types.Count == 0) return -1;
            int gate = ignoreUnlock ? int.MaxValue : waveNumber;
            double total = TotalWeight(types, gate);
            if (total <= 0.0)
            {
                gate = EarliestUnlock(types);
                total = TotalWeight(types, gate);
                if (total <= 0.0) return -1;
            }

            double roll = rng.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < types.Count; i++)
            {
                if (!Eligible(types[i], gate)) continue;
                last = i;
                roll -= types[i].SpawnWeight;
                if (roll < 0.0) return i;
            }
            return last; // floating-point remainder
        }

        // A type unlocking exactly this wave shows up at least once (its debut), at the end of the wave.
        private static void GuaranteeNewTypes(WavePlan plan, WaveCurve curve, IReadOnlyList<EnemyTypeInfo> types, int wave,
                                              float health, float speed, int first)
        {
            if (wave <= 1) return;
            int slot = plan.spawns.Count - 1;
            for (int t = 0; t < types.Count && slot >= first; t++)
            {
                if (!types[t].IsPickable || types[t].UnlockWave != wave) continue;
                bool present = false;
                for (int i = first; i < plan.spawns.Count; i++)
                    if (plan.spawns[i].TypeIndex == t) { present = true; break; }
                if (present) continue;

                // Never overwrite another debuting type's only appearance.
                while (slot >= first && types[plan.spawns[slot].TypeIndex].UnlockWave == wave) slot--;
                if (slot < first) return;
                WaveSpawn old = plan.spawns[slot];
                plan.spawns[slot] = MakeSpawn(curve, types, t, wave, health, speed, old.Delay, old.PathIndex);
                slot--;
            }
        }

        private static bool Eligible(in EnemyTypeInfo t, int gate) => t.IsPickable && t.UnlockWave <= gate;

        private static double TotalWeight(IReadOnlyList<EnemyTypeInfo> types, int gate)
        {
            double sum = 0.0;
            for (int i = 0; i < types.Count; i++)
                if (Eligible(types[i], gate)) sum += types[i].SpawnWeight;
            return sum;
        }

        private static int EarliestUnlock(IReadOnlyList<EnemyTypeInfo> types)
        {
            int min = int.MaxValue;
            for (int i = 0; i < types.Count; i++)
                if (types[i].IsPickable && types[i].UnlockWave < min) min = types[i].UnlockWave;
            return min;
        }

        private static int CountPickable(IReadOnlyList<EnemyTypeInfo> types)
        {
            if (types == null) return 0;
            int n = 0;
            for (int i = 0; i < types.Count; i++) if (types[i].IsPickable) n++;
            return n;
        }

        private static int WaveIndex(int waveNumber) => Math.Max(0, waveNumber - 1);

        // Type stat relative to the reference enemy; missing/invalid stats count as "reference".
        private static double Ratio(double value, double reference) =>
            value > 0.0 && reference > 0.0 && !double.IsInfinity(value) ? value / reference : 1.0;

        private static double Positive(double v, double fallback = 0.0) =>
            v > 0.0 && !double.IsInfinity(v) ? v : fallback;

        private static float ClampFloat(double v, float max)
        {
            if (!(v > 0.0)) return 0f;
            return v >= max ? max : (float)v;
        }
    }
}
