using System;
using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    public class DifficultyRulesTests
    {
        private const int Seed = 4321;

        private static readonly DifficultyMode[] Ordered =
            { DifficultyMode.Easy, DifficultyMode.Normal, DifficultyMode.Hard, DifficultyMode.Impossible };

        private static List<EnemyTypeInfo> Types() => new List<EnemyTypeInfo>
        {
            new EnemyTypeInfo(10f, 2f, 5),
            new EnemyTypeInfo(20f, 1.5f, 8, unlockWave: 3),
            new EnemyTypeInfo(6f, 3.5f, 4, unlockWave: 2),
        };

        private static List<EnemyTypeInfo> Bosses() => new List<EnemyTypeInfo>
        {
            new EnemyTypeInfo(10f, 2f, 5, bossHealthMultiplier: 8f, bossRewardMultiplier: 20f),
        };

        private static WaveCurve CurveFor(DifficultyMode mode)
        {
            var curve = new WaveCurve { MaxSpeedScale = 3f, BossEveryNWaves = 5 };
            Difficulty.Rules(mode).ApplyTo(curve);
            return curve;
        }

        private static List<EnemyTypeInfo> BossesFor(DifficultyMode mode)
        {
            var list = Bosses();
            for (int i = 0; i < list.Count; i++) list[i] = Difficulty.Rules(mode).ApplyToBoss(list[i]);
            return list;
        }

        private static WavePlan PlanFor(DifficultyMode mode, int wave) =>
            WavePlanner.Plan(wave, CurveFor(mode), Types(), BossesFor(mode), 2, Seed);

        // ---------------------------------------------------------------- table shape

        [Test]
        public void EnumValues_AreStableForSaves()
        {
            Assert.That((int)DifficultyMode.Easy, Is.EqualTo(0));
            Assert.That((int)DifficultyMode.Normal, Is.EqualTo(1));
            Assert.That((int)DifficultyMode.Hard, Is.EqualTo(2));
            Assert.That((int)DifficultyMode.Impossible, Is.EqualTo(3));
            Assert.That(Difficulty.All, Is.EqualTo(Ordered));
        }

        [Test]
        public void Normal_IsTheIdentity()
        {
            DifficultyRules n = Difficulty.Rules(DifficultyMode.Normal);
            Assert.That(n.IsIdentity, Is.True);
            Assert.That(n.EnemyHealthMultiplier, Is.EqualTo(1f));
            Assert.That(n.BossHealthMultiplier, Is.EqualTo(1f));
            Assert.That(n.EnemySpeedMultiplier, Is.EqualTo(1f));
            Assert.That(n.KillRewardMultiplier, Is.EqualTo(1f));
            Assert.That(n.StartingGoldMultiplier, Is.EqualTo(1f));
            Assert.That(n.LivesMultiplier, Is.EqualTo(1f));
            Assert.That(n.FixedLives, Is.EqualTo(0));
            Assert.That(n.EarlyCallBonusEnabled, Is.True);
            foreach (int lives in new[] { 1, 7, 20, 33 }) Assert.That(n.ApplyLives(lives), Is.EqualTo(lives));
            foreach (int gold in new[] { 0, 1, 150, 275, 999 }) Assert.That(n.ApplyStartingGold(gold), Is.EqualTo(gold));
            Assert.That(n.EarlyCallBonus(4.2f, 2f), Is.EqualTo(EconomyRules.EarlyCallBonus(4.2f, 2f)));
        }

        [Test]
        public void Normal_LeavesTheCurveAndPlansUntouched()
        {
            var plain = new WaveCurve { MaxSpeedScale = 3f, BossEveryNWaves = 5 };
            WaveCurve normal = CurveFor(DifficultyMode.Normal);
            Assert.That(normal.DifficultyMultiplier, Is.EqualTo(plain.DifficultyMultiplier));
            Assert.That(normal.BaseSpeed, Is.EqualTo(plain.BaseSpeed));
            Assert.That(normal.ReferenceReward, Is.EqualTo(plain.ReferenceReward));

            for (int wave = 1; wave <= 12; wave++)
            {
                WavePlan a = WavePlanner.Plan(wave, plain, Types(), Bosses(), 2, Seed);
                WavePlan b = PlanFor(DifficultyMode.Normal, wave);
                Assert.That(b.Spawns.Count, Is.EqualTo(a.Spawns.Count));
                for (int i = 0; i < a.Spawns.Count; i++)
                {
                    Assert.That(b.Spawns[i].Health, Is.EqualTo(a.Spawns[i].Health));
                    Assert.That(b.Spawns[i].Speed, Is.EqualTo(a.Spawns[i].Speed));
                    Assert.That(b.Spawns[i].Reward, Is.EqualTo(a.Spawns[i].Reward));
                    Assert.That(b.Spawns[i].TypeIndex, Is.EqualTo(a.Spawns[i].TypeIndex));
                }
            }
        }

        // ---------------------------------------------------------------- monotonic Easy < Normal < Hard < Impossible

        private static void AssertStrictlyIncreasing(Func<DifficultyRules, double> axis, string name)
        {
            for (int i = 1; i < Ordered.Length; i++)
            {
                double prev = axis(Difficulty.Rules(Ordered[i - 1]));
                double cur = axis(Difficulty.Rules(Ordered[i]));
                Assert.That(cur, Is.GreaterThan(prev), $"{name}: {Ordered[i]} should be harder than {Ordered[i - 1]}");
            }
        }

        [Test]
        public void EveryAxis_GetsHarderWithEachMode()
        {
            AssertStrictlyIncreasing(r => r.EnemyHealthMultiplier, "enemy health");
            AssertStrictlyIncreasing(r => r.EffectiveBossHealthMultiplier, "boss health");
            AssertStrictlyIncreasing(r => r.EnemySpeedMultiplier, "enemy speed");
            AssertStrictlyIncreasing(r => -r.KillRewardMultiplier, "kill reward (less is harder)");
            AssertStrictlyIncreasing(r => -r.StartingGoldMultiplier, "starting gold (less is harder)");
            AssertStrictlyIncreasing(r => -r.ApplyLives(20), "lives on a 20-life level (fewer is harder)");
        }

        [Test]
        public void ResolvedValues_NeverGetEasierWithAHarderMode([Values(1, 2, 3, 5, 10, 15, 20, 25, 40)] int baseLives,
                                                                   [Values(0, 1, 100, 250, 400)] int baseGold)
        {
            for (int i = 1; i < Ordered.Length; i++)
            {
                DifficultyRules easier = Difficulty.Rules(Ordered[i - 1]);
                DifficultyRules harder = Difficulty.Rules(Ordered[i]);
                Assert.That(harder.ApplyLives(baseLives), Is.LessThanOrEqualTo(easier.ApplyLives(baseLives)));
                Assert.That(harder.ApplyStartingGold(baseGold), Is.LessThanOrEqualTo(easier.ApplyStartingGold(baseGold)));
                // A harder mode never re-enables a bonus an easier one disabled.
                Assert.That(!harder.EarlyCallBonusEnabled || easier.EarlyCallBonusEnabled, Is.True);
            }
        }

        [Test]
        public void Plans_ScaleMonotonically_ForEveryWave()
        {
            var totals = new long[Ordered.Length];
            for (int wave = 1; wave <= 30; wave++)
            {
                for (int m = 0; m < Ordered.Length; m++) totals[m] += PlanFor(Ordered[m], wave).TotalReward;
                for (int i = 1; i < Ordered.Length; i++)
                {
                    WavePlan easier = PlanFor(Ordered[i - 1], wave);
                    WavePlan harder = PlanFor(Ordered[i], wave);
                    // Same seed and types: same composition, only stats differ.
                    Assert.That(harder.Spawns.Count, Is.EqualTo(easier.Spawns.Count));
                    Assert.That(harder.IsBossWave, Is.EqualTo(easier.IsBossWave));
                    for (int s = 0; s < easier.Spawns.Count; s++)
                    {
                        Assert.That(harder.Spawns[s].TypeIndex, Is.EqualTo(easier.Spawns[s].TypeIndex));
                        Assert.That(harder.Spawns[s].Health, Is.GreaterThan(easier.Spawns[s].Health), $"wave {wave} health");
                        Assert.That(harder.Spawns[s].Speed, Is.GreaterThan(easier.Spawns[s].Speed), $"wave {wave} speed");
                        Assert.That(harder.Spawns[s].Reward, Is.LessThanOrEqualTo(easier.Spawns[s].Reward), $"wave {wave} reward");
                        Assert.That(harder.Spawns[s].Reward, Is.GreaterThanOrEqualTo(1), "every kill still pays");
                    }
                    // Whole-gold rounding can tie a single small wave; the run as a whole must pay less.
                    Assert.That(harder.TotalReward, Is.LessThanOrEqualTo(easier.TotalReward), $"wave {wave} total reward");
                }
            }
            for (int i = 1; i < Ordered.Length; i++)
                Assert.That(totals[i], Is.LessThan(totals[i - 1]), $"{Ordered[i]} pays less over 30 waves than {Ordered[i - 1]}");
        }

        [Test]
        public void Curve_ScalesHealthSpeedAndReward_ByTheTable()
        {
            var plain = new WaveCurve { MaxSpeedScale = 3f };
            WaveCurve hard = CurveFor(DifficultyMode.Hard);
            DifficultyRules r = Difficulty.Rules(DifficultyMode.Hard);
            for (int wave = 1; wave <= 40; wave += 3)
            {
                Assert.That(WavePlanner.HealthAt(hard, wave), Is.EqualTo(WavePlanner.HealthAt(plain, wave) * r.EnemyHealthMultiplier).Within(1e-3).Percent);
                // The endless speed cap limits growth only, so the multiplier survives late waves.
                Assert.That(WavePlanner.SpeedAt(hard, wave), Is.EqualTo(WavePlanner.SpeedAt(plain, wave) * r.EnemySpeedMultiplier).Within(1e-3).Percent);
            }
            // Wave 21 reference reward is 10 on Normal; 0.9x = 9 on Hard.
            WaveSpawn normalSpawn = WavePlanner.MakeSpawn(plain, Types(), 0, 21, 1f, 1f, 0f, 0);
            WaveSpawn hardSpawn = WavePlanner.MakeSpawn(hard, Types(), 0, 21, 1f, 1f, 0f, 0);
            Assert.That(normalSpawn.Reward, Is.EqualTo(10));
            Assert.That(hardSpawn.Reward, Is.EqualTo(9));
        }

        [Test]
        public void Bosses_GetTheExtraBossHealthMultiplier()
        {
            DifficultyMode[] modes = { DifficultyMode.Easy, DifficultyMode.Hard, DifficultyMode.Impossible };
            foreach (DifficultyMode mode in modes)
            {
                WavePlan normal = PlanFor(DifficultyMode.Normal, 5);
                WavePlan plan = PlanFor(mode, 5);
                Assert.That(plan.IsBossWave, Is.True);
                DifficultyRules r = Difficulty.Rules(mode);
                Assert.That(plan.Spawns[0].Health,
                            Is.EqualTo(normal.Spawns[0].Health * r.EffectiveBossHealthMultiplier).Within(1e-3).Percent, mode.ToString());
                Assert.That(plan.Spawns[0].Reward,
                            Is.EqualTo(normal.Spawns[0].Reward * r.KillRewardMultiplier).Within(1.0), mode.ToString());
            }
        }

        // ---------------------------------------------------------------- lives / gold / early call

        [Test]
        public void Lives_AreNeverZero([Values(-5, 0, 1, 2, 3, 20, 1000)] int baseLives)
        {
            foreach (DifficultyMode mode in Ordered)
                Assert.That(Difficulty.Rules(mode).ApplyLives(baseLives), Is.GreaterThanOrEqualTo(1), mode.ToString());
        }

        [Test]
        public void Impossible_HasOneLifeAndNoEarlyCallBonus()
        {
            DifficultyRules r = Difficulty.Rules(DifficultyMode.Impossible);
            foreach (int lives in new[] { 1, 10, 20, 50 }) Assert.That(r.ApplyLives(lives), Is.EqualTo(1));
            Assert.That(r.EarlyCallBonusEnabled, Is.False);
            Assert.That(r.EarlyCallBonus(5f, 2f), Is.EqualTo(0));
            Assert.That(Difficulty.Rules(DifficultyMode.Hard).EarlyCallBonus(5f, 2f), Is.EqualTo(10));
        }

        [Test]
        public void SuggestedValues_OnATwentyLifeLevel()
        {
            Assert.That(Difficulty.Rules(DifficultyMode.Easy).ApplyLives(20), Is.EqualTo(30));
            Assert.That(Difficulty.Rules(DifficultyMode.Normal).ApplyLives(20), Is.EqualTo(20));
            Assert.That(Difficulty.Rules(DifficultyMode.Hard).ApplyLives(20), Is.EqualTo(10));
            Assert.That(Difficulty.Rules(DifficultyMode.Impossible).ApplyLives(20), Is.EqualTo(1));
            Assert.That(Difficulty.Rules(DifficultyMode.Easy).ApplyStartingGold(250), Is.EqualTo(325));
            Assert.That(Difficulty.Rules(DifficultyMode.Hard).ApplyStartingGold(250), Is.EqualTo(213)); // 212.5 rounds up
        }

        [Test]
        public void ImpossibleRun_ThatSurvives_IsAlwaysThreeStars()
        {
            int lives = Difficulty.Rules(DifficultyMode.Impossible).ApplyLives(20);
            Assert.That(StarRating.Rate(lives, lives), Is.EqualTo(3));
        }

        [Test]
        public void InvalidMultipliers_FallBackToOne()
        {
            var r = new DifficultyRules(DifficultyMode.Hard, float.NaN, -1f, 0f, float.PositiveInfinity, -3f, float.NaN, -4, true);
            Assert.That(r.IsIdentity, Is.True);
            Assert.That(r.ApplyLives(20), Is.EqualTo(20));
        }

        // ---------------------------------------------------------------- unlocks

        [Test]
        public void Impossible_NeedsThreeStarsOnHard()
        {
            Assert.That(Difficulty.IsImpossibleUnlocked(0), Is.False);
            Assert.That(Difficulty.IsImpossibleUnlocked(2), Is.False);
            Assert.That(Difficulty.IsImpossibleUnlocked(3), Is.True);
            Assert.That(Difficulty.ImpossibleUnlockMode, Is.EqualTo(DifficultyMode.Hard));
            Assert.That(Difficulty.ImpossibleUnlockStars, Is.EqualTo(3));
        }

        [Test]
        public void IsUnlocked_FollowsTheLevelForRegularModes([Values(0, 1, 2, 3)] int hardStars)
        {
            foreach (DifficultyMode mode in new[] { DifficultyMode.Easy, DifficultyMode.Normal, DifficultyMode.Hard })
            {
                Assert.That(Difficulty.IsUnlocked(mode, true, hardStars), Is.True);
                Assert.That(Difficulty.IsUnlocked(mode, false, hardStars), Is.False);
            }
            Assert.That(Difficulty.IsUnlocked(DifficultyMode.Impossible, true, hardStars), Is.EqualTo(hardStars >= 3));
            Assert.That(Difficulty.IsUnlocked(DifficultyMode.Impossible, false, 3), Is.False, "a locked level locks every mode");
            Assert.That(Difficulty.IsUnlocked((DifficultyMode)42, true, 3), Is.False);
        }

        [Test]
        public void ClampToUnlocked_DropsImpossibleToHard()
        {
            Assert.That(Difficulty.ClampToUnlocked(DifficultyMode.Impossible, 2), Is.EqualTo(DifficultyMode.Hard));
            Assert.That(Difficulty.ClampToUnlocked(DifficultyMode.Impossible, 3), Is.EqualTo(DifficultyMode.Impossible));
            Assert.That(Difficulty.ClampToUnlocked(DifficultyMode.Easy, 0), Is.EqualTo(DifficultyMode.Easy));
            Assert.That(Difficulty.ClampToUnlocked((DifficultyMode)(-1), 0), Is.EqualTo(DifficultyMode.Normal));
        }

        [Test]
        public void FromInt_MapsUnknownValuesToTheFallback()
        {
            Assert.That(Difficulty.FromInt(2), Is.EqualTo(DifficultyMode.Hard));
            Assert.That(Difficulty.FromInt(9), Is.EqualTo(DifficultyMode.Normal));
            Assert.That(Difficulty.FromInt(-1, DifficultyMode.Easy), Is.EqualTo(DifficultyMode.Easy));
        }

        // ---------------------------------------------------------------- text

        [Test]
        public void EveryMode_HasANameDescriptionAndModifiers()
        {
            var names = new HashSet<string>();
            foreach (DifficultyMode mode in Ordered)
            {
                Assert.That(names.Add(Difficulty.DisplayName(mode)), Is.True);
                Assert.That(Difficulty.Description(mode), Is.Not.Empty);
                Assert.That(Difficulty.ModifierLines(mode, 20), Is.Not.Empty);
                Assert.That(Difficulty.ModifierSummary(mode, 20), Is.Not.Empty);
            }
        }

        [Test]
        public void ModifierText_ReadsLikeTheDesign()
        {
            Assert.That(Difficulty.ModifierSummary(DifficultyMode.Hard, 20), Is.EqualTo("Enemies +35% HP · 10 lives"));
            Assert.That(Difficulty.ModifierSummary(DifficultyMode.Easy, 20), Is.EqualTo("Enemies -30% HP · 30 lives"));
            Assert.That(Difficulty.ModifierSummary(DifficultyMode.Normal, 20), Is.EqualTo("Standard balance · 20 lives"));
            Assert.That(Difficulty.ModifierSummary(DifficultyMode.Impossible, 20),
                        Is.EqualTo("Enemies +70% HP · 1 life · no early-call bonus"));
            Assert.That(Difficulty.ModifierLines(DifficultyMode.Impossible, 20), Does.Contain("No early-call bonus"));
            Assert.That(Difficulty.Percent(1.35f), Is.EqualTo("+35%"));
            Assert.That(Difficulty.Percent(0.7f), Is.EqualTo("-30%"));
            Assert.That(Difficulty.ImpossibleUnlockHint, Is.EqualTo("3 stars on Hard to unlock"));
        }
    }
}
