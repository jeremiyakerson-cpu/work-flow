using System;
using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    public class TowerUpgradeMathTests
    {
        // Mirrors TowerData's default field values.
        private static TowerUpgradeSpec DefaultArcher()
        {
            var s = TowerUpgradeSpec.Neutral(range: 4f, fireRate: 1f, damage: 5f, baseCost: 50);
            s.level2DamageMult = 1.5f; s.level2RangeMult = 1.1f; s.level2FireRateMult = 1.15f; s.level2Cost = 40;
            s.pathADamageMult = 2f; s.pathARangeMult = 1.25f; s.pathACost = 80;
            s.pathBFireRateMult = 1.6f; s.pathBAppliesSlow = true; s.pathBCost = 90;
            s.level4DamageMult = 1.4f; s.level4FireRateMult = 1.2f; s.level4Cost = 140;
            return s;
        }

        [Test]
        public void Level1_IsBaseStats()
        {
            var st = TowerUpgradeMath.StatsAt(DefaultArcher(), 1, UpgradeBranch.None);
            Assert.That(st.Damage, Is.EqualTo(5f));
            Assert.That(st.Range, Is.EqualTo(4f));
            Assert.That(st.FireRate, Is.EqualTo(1f));
            Assert.That(st.AppliesSlow, Is.False);
            Assert.That(st.Dps, Is.EqualTo(5f));
        }

        [Test]
        public void Level2_AppliesLinearMultipliers()
        {
            var st = TowerUpgradeMath.StatsAt(DefaultArcher(), 2, UpgradeBranch.None);
            Assert.That(st.Damage, Is.EqualTo(7.5f).Within(1e-4f));
            Assert.That(st.Range, Is.EqualTo(4.4f).Within(1e-4f));
            Assert.That(st.FireRate, Is.EqualTo(1.15f).Within(1e-4f));
        }

        [Test]
        public void BranchA_DamageAndRange_BranchB_FireRateAndSlow()
        {
            var a = TowerUpgradeMath.StatsAt(DefaultArcher(), 3, UpgradeBranch.A);
            var b = TowerUpgradeMath.StatsAt(DefaultArcher(), 3, UpgradeBranch.B);
            Assert.That(a.Damage, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(a.Range, Is.EqualTo(5.5f).Within(1e-4f));
            Assert.That(a.FireRate, Is.EqualTo(1.15f).Within(1e-4f));
            Assert.That(a.AppliesSlow, Is.False);

            Assert.That(b.Damage, Is.EqualTo(7.5f).Within(1e-4f));
            Assert.That(b.Range, Is.EqualTo(4.4f).Within(1e-4f));
            Assert.That(b.FireRate, Is.EqualTo(1.84f).Within(1e-4f));
            Assert.That(b.AppliesSlow, Is.True);
        }

        [Test]
        public void Level4_StacksOnChosenBranch()
        {
            var a = TowerUpgradeMath.StatsAt(DefaultArcher(), 4, UpgradeBranch.A);
            var b = TowerUpgradeMath.StatsAt(DefaultArcher(), 4, UpgradeBranch.B);
            Assert.That(a.Damage, Is.EqualTo(21f).Within(1e-4f));
            Assert.That(a.FireRate, Is.EqualTo(1.38f).Within(1e-4f));
            Assert.That(b.FireRate, Is.EqualTo(1.84f * 1.2f).Within(1e-4f));
            Assert.That(b.AppliesSlow, Is.True, "branch B slow persists into level 4");
        }

        [Test]
        public void EveryUpgradeStep_NeverReducesDps_WithDefaultData()
        {
            var s = DefaultArcher();
            foreach (var branch in new[] { UpgradeBranch.A, UpgradeBranch.B })
            {
                float prev = 0f;
                for (int level = 1; level <= 4; level++)
                {
                    var st = TowerUpgradeMath.StatsAt(s, level, level > 2 ? branch : UpgradeBranch.None);
                    Assert.That(st.Dps, Is.GreaterThanOrEqualTo(prev));
                    prev = st.Dps;
                }
            }
        }

        [Test]
        public void NextUpgradeCost_PerLevel()
        {
            var s = DefaultArcher();
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 1, UpgradeBranch.None), Is.EqualTo(40));
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 2, UpgradeBranch.None), Is.EqualTo(80), "cheaper branch");
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 3, UpgradeBranch.A), Is.EqualTo(140));
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 4, UpgradeBranch.B), Is.EqualTo(0), "max level");
        }

        [Test]
        public void BranchCost_IsPerBranch()
        {
            var s = DefaultArcher();
            Assert.That(TowerUpgradeMath.BranchCost(s, UpgradeBranch.A), Is.EqualTo(80));
            Assert.That(TowerUpgradeMath.BranchCost(s, UpgradeBranch.B), Is.EqualTo(90));
            Assert.That(TowerUpgradeMath.BranchCost(s, UpgradeBranch.None), Is.EqualTo(80));
        }

        [Test]
        public void TotalInvested_SumsEveryStep_PerBranch()
        {
            var s = DefaultArcher();
            Assert.That(TowerUpgradeMath.TotalInvested(s, 1, UpgradeBranch.None), Is.EqualTo(50));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 2, UpgradeBranch.None), Is.EqualTo(90));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 3, UpgradeBranch.A), Is.EqualTo(170));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 3, UpgradeBranch.B), Is.EqualTo(180));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 4, UpgradeBranch.B), Is.EqualTo(320));
        }

        [Test]
        public void SellValue_IsFractionOfInvestment()
        {
            var s = DefaultArcher();
            Assert.That(TowerUpgradeMath.SellValue(s, 1, UpgradeBranch.None, 0.7f), Is.EqualTo(35));
            Assert.That(TowerUpgradeMath.SellValue(s, 4, UpgradeBranch.A, 0.7f), Is.EqualTo(217));
            Assert.That(TowerUpgradeMath.SellValue(s, 4, UpgradeBranch.A, 1f), Is.EqualTo(310));
        }

        [Test]
        public void InvalidStates_Throw()
        {
            var s = DefaultArcher();
            Assert.Throws<ArgumentOutOfRangeException>(() => TowerUpgradeMath.StatsAt(s, 0, UpgradeBranch.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => TowerUpgradeMath.StatsAt(s, 5, UpgradeBranch.A));
            Assert.Throws<ArgumentException>(() => TowerUpgradeMath.StatsAt(s, 3, UpgradeBranch.None));
            Assert.Throws<ArgumentException>(() => TowerUpgradeMath.StatsAt(s, 2, UpgradeBranch.A));
            Assert.That(TowerUpgradeMath.IsValidState(4, UpgradeBranch.B), Is.True);
            Assert.That(TowerUpgradeMath.IsValidState(1, UpgradeBranch.A), Is.False);
        }

        [Test]
        public void BrokenAssetValues_AreSanitised()
        {
            var s = DefaultArcher();
            s.level2DamageMult = 0f;            // treated as "no change"
            s.pathADamageMult = float.NaN;
            s.level4RangeMult = -3f;
            s.level4Cost = -10;
            var st = TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.A);
            Assert.That(st.Damage, Is.EqualTo(5f * 1.4f).Within(1e-4f));
            Assert.That(st.Range, Is.EqualTo(4f * 1.1f * 1.25f).Within(1e-4f));
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 3, UpgradeBranch.A), Is.EqualTo(0));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 4, UpgradeBranch.A), Is.EqualTo(170));
        }

        [Test]
        public void NeutralSpec_KeepsStatsAcrossLevels()
        {
            var s = TowerUpgradeSpec.Neutral(3f, 2f, 10f, 70);
            var st = TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.B);
            Assert.That(st.Range, Is.EqualTo(3f));
            Assert.That(st.FireRate, Is.EqualTo(2f));
            Assert.That(st.Damage, Is.EqualTo(10f));
        }
    }
}
