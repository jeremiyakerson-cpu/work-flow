using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    public class WavePlannerTests
    {
        private const int Seed = 1234;

        // grunt (ref), tank (2x hp, slow), runner (fast, frail), bat (flying, unlock 5), knight (unlock 10)
        private static List<EnemyTypeInfo> Types() => new List<EnemyTypeInfo>
        {
            new EnemyTypeInfo(10f, 2f, 5),
            new EnemyTypeInfo(20f, 1.5f, 8, unlockWave: 3),
            new EnemyTypeInfo(6f, 3.5f, 4, unlockWave: 2),
            new EnemyTypeInfo(8f, 2.5f, 6, unlockWave: 5, isFlying: true),
            new EnemyTypeInfo(40f, 1.2f, 15, unlockWave: 10),
        };

        private static List<EnemyTypeInfo> Bosses() => new List<EnemyTypeInfo>
        {
            new EnemyTypeInfo(10f, 2f, 5, bossHealthMultiplier: 8f, bossRewardMultiplier: 20f),
            new EnemyTypeInfo(10f, 2f, 5, unlockWave: 20, bossHealthMultiplier: 12f, bossRewardMultiplier: 30f),
        };

        private static WaveCurve Curve() => new WaveCurve { BossEveryNWaves = 10 };

        [Test]
        public void Wave1_ReferenceEnemy_GetsOriginalBaseStats()
        {
            var plan = WavePlanner.Plan(1, Curve(), new[] { new EnemyTypeInfo(10f, 2f, 5) }, null, 1, Seed);
            Assert.That(plan.Spawns.Count, Is.EqualTo(6));
            foreach (var s in plan.Spawns)
            {
                Assert.That(s.Health, Is.EqualTo(10f).Within(1e-4f));
                Assert.That(s.Speed, Is.EqualTo(2f).Within(1e-4f));
                Assert.That(s.Reward, Is.EqualTo(5));
                Assert.That(s.IsBoss, Is.False);
            }
        }

        [Test]
        public void Curve_MatchesOriginalWaveManagerFormula()
        {
            var c = Curve();
            c.DifficultyMultiplier = 1.5f;
            for (int w = 1; w <= 60; w++)
            {
                double h = 10.0 * 1.5 * System.Math.Pow(1.12, w - 1);
                double sp = 2.0 * System.Math.Pow(1.01, w - 1);
                Assert.That(WavePlanner.HealthAt(c, w), Is.EqualTo((float)h).Within(h * 1e-5));
                Assert.That(WavePlanner.SpeedAt(c, w), Is.EqualTo((float)sp).Within(sp * 1e-5));
                Assert.That(WavePlanner.EnemyCountAt(c, w), Is.EqualTo(6 + (w - 1)));
            }
        }

        [Test]
        public void PerTypeScaling_KeepsTanksTankier()
        {
            var types = Types();
            var c = Curve();
            var tank = WavePlanner.MakeSpawn(c, types, 1, 4, WavePlanner.HealthAt(c, 4), WavePlanner.SpeedAt(c, 4), 0f, 0);
            var grunt = WavePlanner.MakeSpawn(c, types, 0, 4, WavePlanner.HealthAt(c, 4), WavePlanner.SpeedAt(c, 4), 0f, 0);
            Assert.That(tank.Health, Is.EqualTo(grunt.Health * 2f).Within(1e-3f));
            Assert.That(tank.Speed, Is.EqualTo(grunt.Speed * 0.75f).Within(1e-4f));
            Assert.That(tank.Reward, Is.GreaterThan(grunt.Reward));
        }

        [Test]
        public void SameSeed_IsDeterministic_DifferentSeedsDiffer()
        {
            var types = Types();
            bool anyDifference = false;
            for (int w = 1; w <= 40; w++)
            {
                var a = WavePlanner.Plan(w, Curve(), types, Bosses(), 3, Seed);
                var b = WavePlanner.Plan(w, Curve(), types, Bosses(), 3, Seed);
                var other = WavePlanner.Plan(w, Curve(), types, Bosses(), 3, Seed + 1);
                Assert.That(a.Spawns.Count, Is.EqualTo(b.Spawns.Count));
                for (int i = 0; i < a.Spawns.Count; i++)
                {
                    Assert.That(a.Spawns[i].TypeIndex, Is.EqualTo(b.Spawns[i].TypeIndex));
                    Assert.That(a.Spawns[i].PathIndex, Is.EqualTo(b.Spawns[i].PathIndex));
                    Assert.That(a.Spawns[i].Health, Is.EqualTo(b.Spawns[i].Health));
                    Assert.That(a.Spawns[i].Delay, Is.EqualTo(b.Spawns[i].Delay));
                    if (i < other.Spawns.Count && other.Spawns[i].TypeIndex != a.Spawns[i].TypeIndex) anyDifference = true;
                }
            }
            Assert.That(anyDifference, Is.True, "a different seed should change composition somewhere");
        }

        [Test]
        public void PlanningOrder_DoesNotAffectResult()
        {
            var types = Types();
            var direct = WavePlanner.Plan(17, Curve(), types, Bosses(), 2, Seed);
            for (int w = 1; w < 17; w++) WavePlanner.Plan(w, Curve(), types, Bosses(), 2, Seed);
            var after = WavePlanner.Plan(17, Curve(), types, Bosses(), 2, Seed);
            Assert.That(after.TotalHealth, Is.EqualTo(direct.TotalHealth));
            for (int i = 0; i < direct.Spawns.Count; i++)
                Assert.That(after.Spawns[i].TypeIndex, Is.EqualTo(direct.Spawns[i].TypeIndex));
        }

        [Test]
        public void UnlockGating_NoTypeAppearsBeforeItsUnlockWave()
        {
            var types = Types();
            for (int seed = 0; seed < 20; seed++)
            for (int w = 1; w <= 30; w++)
            {
                var plan = WavePlanner.Plan(w, Curve(), types, Bosses(), 2, seed);
                foreach (var s in plan.Spawns)
                {
                    if (s.IsBoss) continue;
                    Assert.That(types[s.TypeIndex].UnlockWave, Is.LessThanOrEqualTo(w), $"seed {seed} wave {w}");
                }
            }
        }

        [Test]
        public void NewType_DebutsOnItsUnlockWave()
        {
            var types = Types();
            for (int seed = 0; seed < 25; seed++)
            {
                foreach (int unlock in new[] { 2, 3, 5 })
                {
                    var plan = WavePlanner.Plan(unlock, Curve(), types, Bosses(), 1, seed);
                    bool found = false;
                    foreach (var s in plan.Spawns) if (!s.IsBoss && types[s.TypeIndex].UnlockWave == unlock) found = true;
                    Assert.That(found, Is.True, $"seed {seed}: type unlocking at {unlock} missing");
                }
            }
        }

        [Test]
        public void NothingUnlocked_FallsBackToEarliestTypes()
        {
            var types = new List<EnemyTypeInfo> { new EnemyTypeInfo(10f, 2f, 5, unlockWave: 4), new EnemyTypeInfo(10f, 2f, 5, unlockWave: 9) };
            var plan = WavePlanner.Plan(1, Curve(), types, null, 1, Seed);
            Assert.That(plan.Spawns.Count, Is.EqualTo(6));
            foreach (var s in plan.Spawns) Assert.That(s.TypeIndex, Is.EqualTo(0));
        }

        [Test]
        public void UnavailableAndZeroWeightTypes_AreNeverPicked()
        {
            var types = new List<EnemyTypeInfo>
            {
                EnemyTypeInfo.Unavailable,
                new EnemyTypeInfo(10f, 2f, 5, spawnWeight: 0f),
                new EnemyTypeInfo(10f, 2f, 5),
            };
            for (int w = 1; w <= 20; w++)
                foreach (var s in WavePlanner.Plan(w, Curve(), types, null, 1, Seed).Spawns)
                    Assert.That(s.TypeIndex, Is.EqualTo(2));
        }

        [Test]
        public void SpawnWeights_ShapeTheMix()
        {
            var types = new List<EnemyTypeInfo> { new EnemyTypeInfo(10f, 2f, 5, spawnWeight: 9f), new EnemyTypeInfo(10f, 2f, 5, spawnWeight: 1f) };
            int heavy = 0, total = 0;
            for (int w = 1; w <= 60; w++)
                foreach (var s in WavePlanner.Plan(w, Curve(), types, null, 1, Seed).Spawns)
                {
                    total++;
                    if (s.TypeIndex == 0) heavy++;
                }
            double share = (double)heavy / total;
            Assert.That(share, Is.InRange(0.85, 0.95));
        }

        [Test]
        public void EmptyPools_GiveEmptyRegularWaves_AndNoBossWaves()
        {
            var plan = WavePlanner.Plan(10, Curve(), null, null, 1, Seed);
            Assert.That(plan.IsBossWave, Is.False);
            Assert.That(plan.Spawns.Count, Is.EqualTo(0));
            Assert.That(plan.Duration, Is.EqualTo(0f));
        }

        [Test]
        public void BossCadence_EveryNthWaveOnly()
        {
            var types = Types();
            for (int w = 1; w <= 100; w++)
            {
                var plan = WavePlanner.Plan(w, Curve(), types, Bosses(), 1, Seed);
                Assert.That(plan.IsBossWave, Is.EqualTo(w % 10 == 0), $"wave {w}");
                int bosses = 0;
                foreach (var s in plan.Spawns) if (s.IsBoss) bosses++;
                Assert.That(bosses, Is.EqualTo(plan.IsBossWave ? 1 : 0));
            }
        }

        [Test]
        public void IsBossWave_Rules()
        {
            Assert.That(WavePlanner.IsBossWave(10, 10, 1), Is.True);
            Assert.That(WavePlanner.IsBossWave(10, 10, 0), Is.False, "no bosses");
            Assert.That(WavePlanner.IsBossWave(10, 0, 3), Is.False, "cadence disabled");
            Assert.That(WavePlanner.IsBossWave(0, 5, 3), Is.False, "wave 0 never a boss");
            Assert.That(WavePlanner.IsBossWave(15, 5, 3), Is.True);
        }

        [Test]
        public void BossWave_DefaultIsBossAlone_WithOriginalStats()
        {
            var c = Curve();
            var plan = WavePlanner.Plan(10, c, Types(), Bosses(), 1, Seed);
            Assert.That(plan.Spawns.Count, Is.EqualTo(1));
            var boss = plan.Spawns[0];
            Assert.That(boss.IsBoss, Is.True);
            Assert.That(boss.TypeIndex, Is.EqualTo(0), "second boss unlocks at wave 20");
            Assert.That(boss.Health, Is.EqualTo(WavePlanner.HealthAt(c, 10) * 8f).Within(1e-2f));
            Assert.That(boss.Speed, Is.EqualTo(WavePlanner.SpeedAt(c, 10) * 0.6f).Within(1e-4f));
            Assert.That(boss.Reward, Is.EqualTo(EconomyRules.KillReward(5, 10, 0.05f) * 20));
        }

        [Test]
        public void BossWave_WithEscorts_BossFirstThenEscorts()
        {
            var c = Curve();
            c.BossEscortFraction = 0.5f;
            var plan = WavePlanner.Plan(10, c, Types(), Bosses(), 2, Seed);
            int expectedEscorts = (int)System.Math.Round(WavePlanner.EnemyCountAt(c, 10) * 0.5, System.MidpointRounding.AwayFromZero);
            Assert.That(plan.Spawns.Count, Is.EqualTo(1 + expectedEscorts));
            Assert.That(plan.Spawns[0].IsBoss, Is.True);
            Assert.That(plan.Spawns[1].Delay, Is.EqualTo(c.SpawnInterval * 2f).Within(1e-5f));
            for (int i = 1; i < plan.Spawns.Count; i++) Assert.That(plan.Spawns[i].IsBoss, Is.False);
        }

        [Test]
        public void DifficultyIsMonotonic_AndValuesStaySane_Across500Waves()
        {
            var types = Types();
            var c = Curve();
            c.MaxSpeedScale = 3f;
            float prevHealth = 0f, prevSpeed = 0f;
            int prevCount = 0;
            for (int w = 1; w <= 500; w++)
            {
                float h = WavePlanner.HealthAt(c, w);
                float sp = WavePlanner.SpeedAt(c, w);
                int n = WavePlanner.EnemyCountAt(c, w);
                Assert.That(h, Is.GreaterThanOrEqualTo(prevHealth));
                Assert.That(sp, Is.GreaterThanOrEqualTo(prevSpeed));
                Assert.That(n, Is.GreaterThanOrEqualTo(prevCount));
                Assert.That(sp, Is.LessThanOrEqualTo(2f * 3f + 1e-4f), "speed cap");
                prevHealth = h; prevSpeed = sp; prevCount = n;

                var plan = WavePlanner.Plan(w, c, types, Bosses(), 3, Seed);
                Assert.That(double.IsNaN(plan.TotalHealth) || double.IsInfinity(plan.TotalHealth), Is.False);
                Assert.That(plan.TotalReward, Is.GreaterThan(0));
                Assert.That(plan.Duration, Is.GreaterThanOrEqualTo(0f));
                foreach (var s in plan.Spawns)
                {
                    Assert.That(s.Health, Is.GreaterThan(0f).And.LessThanOrEqualTo(WavePlanner.MaxHealth));
                    Assert.That(float.IsNaN(s.Speed), Is.False);
                    Assert.That(s.Speed, Is.GreaterThan(0f));
                    Assert.That(s.Reward, Is.GreaterThan(0));
                    Assert.That(s.Delay, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(s.PathIndex, Is.InRange(0, 2));
                }
            }
        }

        [Test]
        public void TotalWaveHealth_TrendsUpward()
        {
            // Composition is random, so compare 5-wave windows rather than single waves.
            var types = Types();
            double Window(int start)
            {
                double sum = 0;
                for (int w = start; w < start + 5; w++)
                    if (w % 10 != 0) sum += WavePlanner.Plan(w, Curve(), types, Bosses(), 1, Seed).TotalHealth;
                return sum;
            }
            for (int start = 1; start <= 90; start += 5)
                Assert.That(Window(start + 5), Is.GreaterThan(Window(start)), $"window {start}");
        }

        [Test]
        public void HealthIsCapped_InVeryLateEndlessWaves()
        {
            var c = Curve();
            Assert.That(WavePlanner.HealthAt(c, 5000), Is.EqualTo(WavePlanner.MaxHealth));
            var plan = WavePlanner.Plan(5000, c, Types(), Bosses(), 1, Seed);
            foreach (var s in plan.Spawns)
            {
                Assert.That(float.IsInfinity(s.Health), Is.False);
                Assert.That(s.Health, Is.LessThanOrEqualTo(WavePlanner.MaxHealth));
            }
        }

        [Test]
        public void EnemyCount_CapAndFloor()
        {
            var c = Curve();
            c.MaxEnemiesPerWave = 25;
            Assert.That(WavePlanner.EnemyCountAt(c, 100), Is.EqualTo(25));
            c.BaseEnemyCount = 0; c.EnemyCountGrowthPerWave = -3; c.MaxEnemiesPerWave = 0;
            Assert.That(WavePlanner.EnemyCountAt(c, 50), Is.EqualTo(1));
        }

        [Test]
        public void SpawnTimings_FirstImmediateThenInterval()
        {
            var c = Curve();
            c.SpawnInterval = 0.75f;
            var plan = WavePlanner.Plan(3, c, Types(), null, 1, Seed);
            Assert.That(plan.Spawns[0].Delay, Is.EqualTo(0f));
            for (int i = 1; i < plan.Spawns.Count; i++) Assert.That(plan.Spawns[i].Delay, Is.EqualTo(0.75f));
            Assert.That(plan.Duration, Is.EqualTo(0.75f * (plan.Spawns.Count - 1)).Within(1e-4f));
        }

        [Test]
        public void Paths_AreAssignedRoundRobin_AndClampedToPathCount()
        {
            var plan = WavePlanner.Plan(4, Curve(), Types(), null, 3, Seed);
            for (int i = 0; i < plan.Spawns.Count; i++) Assert.That(plan.Spawns[i].PathIndex, Is.EqualTo(i % 3));
            var single = WavePlanner.Plan(4, Curve(), Types(), null, 0, Seed);
            foreach (var s in single.Spawns) Assert.That(s.PathIndex, Is.EqualTo(0));
        }

        [Test]
        public void BrokenCurveValues_NeverProduceNaNOrNegatives()
        {
            var c = new WaveCurve
            {
                BaseHealth = float.NaN, HealthGrowthPerWave = -2f, BaseSpeed = -1f, SpeedGrowthPerWave = float.NaN,
                DifficultyMultiplier = 0f, SpawnInterval = -1f, BaseGoldReward = -5, RewardGrowthPerWave = float.NaN,
                ReferenceHealth = 0f, ReferenceSpeed = float.NaN, ReferenceReward = -1f,
            };
            var types = new[] { new EnemyTypeInfo(float.NaN, -3f, -2) };
            for (int w = 1; w <= 20; w++)
            {
                var plan = WavePlanner.Plan(w, c, types, null, 1, Seed);
                foreach (var s in plan.Spawns)
                {
                    Assert.That(s.Health, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(s.Speed, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(s.Reward, Is.GreaterThanOrEqualTo(0));
                    Assert.That(s.Delay, Is.EqualTo(0f));
                }
            }
        }

        [Test]
        public void WaveNumberBelowOne_IsTreatedAsWaveOne()
        {
            var a = WavePlanner.Plan(0, Curve(), Types(), null, 1, Seed);
            var b = WavePlanner.Plan(1, Curve(), Types(), null, 1, Seed);
            Assert.That(a.WaveNumber, Is.EqualTo(1));
            Assert.That(a.TotalHealth, Is.EqualTo(b.TotalHealth));
        }

        [Test]
        public void NullCurve_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => WavePlanner.Plan(1, null, Types(), null, 1, Seed));
        }
    }
}
