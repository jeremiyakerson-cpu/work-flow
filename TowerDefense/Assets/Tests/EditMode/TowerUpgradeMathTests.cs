using System;
using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    public class TowerUpgradeMathTests
    {
        private const float Eps = 1e-4f;

        // Mirrors TowerData's default field values.
        private static TowerUpgradeSpec DefaultTower()
        {
            var s = TowerUpgradeSpec.Neutral(range: 4f, fireRate: 1f, damage: 5f, baseCost: 50);
            s.level2DamageMult = 1.5f; s.level2RangeMult = 1.1f; s.level2FireRateMult = 1.15f; s.level2Cost = 60;
            s.level3DamageMult = 1.4f; s.level3RangeMult = 1.1f; s.level3FireRateMult = 1.1f; s.level3Cost = 90;
            s.pathADamageMult = 2f; s.pathARangeMult = 1.25f; s.pathACost = 150;
            s.pathBFireRateMult = 1.6f; s.pathBAppliesSlow = true; s.pathBCost = 140;
            return s;
        }

        // A poisoning splash tower (alchemist-like) to exercise effect growth.
        private static TowerUpgradeSpec PoisonSplash()
        {
            var s = DefaultTower();
            s.appliesPoison = true; s.poisonDps = 4f; s.splashRadius = 1f;
            s.level2PoisonMult = 1.5f; s.level2SplashBonus = 0.1f;
            s.level3PoisonMult = 1.5f; s.level3SplashBonus = 0.2f;
            s.pathAPoisonMult = 2f;
            s.pathBSplashBonus = 0.5f;
            return s;
        }

        [Test]
        public void Level1_IsBaseStats()
        {
            var st = TowerUpgradeMath.StatsAt(DefaultTower(), 1, UpgradeBranch.None);
            Assert.That(st.Damage, Is.EqualTo(5f));
            Assert.That(st.Range, Is.EqualTo(4f));
            Assert.That(st.FireRate, Is.EqualTo(1f));
            Assert.That(st.SplashRadius, Is.EqualTo(0f));
            Assert.That(st.PoisonDps, Is.EqualTo(0f));
            Assert.That(st.AppliesSlow, Is.False);
            Assert.That(st.Dps, Is.EqualTo(5f));
        }

        [Test]
        public void Level2_AppliesFirstLinearMultipliers()
        {
            var st = TowerUpgradeMath.StatsAt(DefaultTower(), 2, UpgradeBranch.None);
            Assert.That(st.Damage, Is.EqualTo(7.5f).Within(Eps));
            Assert.That(st.Range, Is.EqualTo(4.4f).Within(Eps));
            Assert.That(st.FireRate, Is.EqualTo(1.15f).Within(Eps));
        }

        [Test]
        public void Level3_StacksSecondLinearMultipliers()
        {
            var st = TowerUpgradeMath.StatsAt(DefaultTower(), 3, UpgradeBranch.None);
            Assert.That(st.Damage, Is.EqualTo(10.5f).Within(Eps));
            Assert.That(st.Range, Is.EqualTo(4.84f).Within(Eps));
            Assert.That(st.FireRate, Is.EqualTo(1.265f).Within(Eps));
            Assert.That(st.AppliesSlow, Is.False);
        }

        [Test]
        public void Specialization_StacksOnLevel3_PerBranch()
        {
            var a = TowerUpgradeMath.StatsAt(DefaultTower(), 4, UpgradeBranch.A);
            var b = TowerUpgradeMath.StatsAt(DefaultTower(), 4, UpgradeBranch.B);
            Assert.That(a.Damage, Is.EqualTo(21f).Within(Eps));
            Assert.That(a.Range, Is.EqualTo(6.05f).Within(Eps));
            Assert.That(a.FireRate, Is.EqualTo(1.265f).Within(Eps));
            Assert.That(a.AppliesSlow, Is.False);

            Assert.That(b.Damage, Is.EqualTo(10.5f).Within(Eps));
            Assert.That(b.Range, Is.EqualTo(4.84f).Within(Eps));
            Assert.That(b.FireRate, Is.EqualTo(2.024f).Within(Eps));
            Assert.That(b.AppliesSlow, Is.True, "branch B grants slow");
        }

        [Test]
        public void PoisonDps_GrowsWithEveryStep()
        {
            var s = PoisonSplash();
            Assert.That(TowerUpgradeMath.StatsAt(s, 1, UpgradeBranch.None).PoisonDps, Is.EqualTo(4f).Within(Eps));
            Assert.That(TowerUpgradeMath.StatsAt(s, 2, UpgradeBranch.None).PoisonDps, Is.EqualTo(6f).Within(Eps));
            Assert.That(TowerUpgradeMath.StatsAt(s, 3, UpgradeBranch.None).PoisonDps, Is.EqualTo(9f).Within(Eps));
            Assert.That(TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.A).PoisonDps, Is.EqualTo(18f).Within(Eps));
            Assert.That(TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.B).PoisonDps, Is.EqualTo(9f).Within(Eps));
            var l3 = TowerUpgradeMath.StatsAt(s, 3, UpgradeBranch.None);
            Assert.That(l3.TotalDps, Is.EqualTo(l3.Dps + 9f).Within(Eps));
        }

        [Test]
        public void PoisonDps_IsZero_WhenTheTowerDoesNotPoison()
        {
            var s = PoisonSplash();
            s.appliesPoison = false;
            var st = TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.A);
            Assert.That(st.PoisonDps, Is.EqualTo(0f));
            Assert.That(st.AppliesPoison, Is.False);
            Assert.That(st.TotalDps, Is.EqualTo(st.Dps));
        }

        [Test]
        public void SplashRadius_GrowsAdditively()
        {
            var s = PoisonSplash();
            Assert.That(TowerUpgradeMath.StatsAt(s, 1, UpgradeBranch.None).SplashRadius, Is.EqualTo(1f).Within(Eps));
            Assert.That(TowerUpgradeMath.StatsAt(s, 2, UpgradeBranch.None).SplashRadius, Is.EqualTo(1.1f).Within(Eps));
            Assert.That(TowerUpgradeMath.StatsAt(s, 3, UpgradeBranch.None).SplashRadius, Is.EqualTo(1.3f).Within(Eps));
            Assert.That(TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.A).SplashRadius, Is.EqualTo(1.3f).Within(Eps));
            Assert.That(TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.B).SplashRadius, Is.EqualTo(1.8f).Within(Eps));
        }

        [Test]
        public void SplashBonus_CanGrantSplashToASingleTargetTower()
        {
            var s = DefaultTower();
            s.pathBSplashBonus = 1.2f;
            Assert.That(TowerUpgradeMath.StatsAt(s, 3, UpgradeBranch.None).SplashRadius, Is.EqualTo(0f));
            Assert.That(TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.A).SplashRadius, Is.EqualTo(0f));
            Assert.That(TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.B).SplashRadius, Is.EqualTo(1.2f).Within(Eps));
        }

        [Test]
        public void Dps_StrictlyIncreases_L1_L2_L3_ThenEitherSpecialization()
        {
            var s = PoisonSplash();
            float l1 = TowerUpgradeMath.StatsAt(s, 1, UpgradeBranch.None).TotalDps;
            float l2 = TowerUpgradeMath.StatsAt(s, 2, UpgradeBranch.None).TotalDps;
            float l3 = TowerUpgradeMath.StatsAt(s, 3, UpgradeBranch.None).TotalDps;
            float a = TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.A).TotalDps;
            float b = TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.B).TotalDps;
            Assert.That(l2, Is.GreaterThan(l1));
            Assert.That(l3, Is.GreaterThan(l2));
            Assert.That(a, Is.GreaterThan(l3));
            Assert.That(b, Is.GreaterThan(l3));
        }

        [Test]
        public void NextUpgradeCost_IsLinearOnly()
        {
            var s = DefaultTower();
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 1, UpgradeBranch.None), Is.EqualTo(60));
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 2, UpgradeBranch.None), Is.EqualTo(90));
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 3, UpgradeBranch.None), Is.EqualTo(0), "specialize via BranchCost");
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 4, UpgradeBranch.A), Is.EqualTo(0), "capstone");
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 4, UpgradeBranch.B), Is.EqualTo(0), "capstone");
        }

        [Test]
        public void BranchCost_IsPerBranch_AndNoneThrows()
        {
            var s = DefaultTower();
            Assert.That(TowerUpgradeMath.BranchCost(s, UpgradeBranch.A), Is.EqualTo(150));
            Assert.That(TowerUpgradeMath.BranchCost(s, UpgradeBranch.B), Is.EqualTo(140));
            Assert.Throws<ArgumentException>(() => TowerUpgradeMath.BranchCost(s, UpgradeBranch.None));
        }

        [Test]
        public void TotalInvested_SumsEveryStep_PerBranch()
        {
            var s = DefaultTower();
            Assert.That(TowerUpgradeMath.TotalInvested(s, 1, UpgradeBranch.None), Is.EqualTo(50));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 2, UpgradeBranch.None), Is.EqualTo(110));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 3, UpgradeBranch.None), Is.EqualTo(200));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 4, UpgradeBranch.A), Is.EqualTo(350));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 4, UpgradeBranch.B), Is.EqualTo(340));
        }

        [Test]
        public void SellValue_IsFractionOfInvestment_RoundedHalfAwayFromZero()
        {
            var s = DefaultTower();
            Assert.That(TowerUpgradeMath.SellValue(s, 1, UpgradeBranch.None, 0.7f), Is.EqualTo(35));
            Assert.That(TowerUpgradeMath.SellValue(s, 2, UpgradeBranch.None, 0.7f), Is.EqualTo(77));
            Assert.That(TowerUpgradeMath.SellValue(s, 4, UpgradeBranch.A, 0.7f), Is.EqualTo(245));
            Assert.That(TowerUpgradeMath.SellValue(s, 4, UpgradeBranch.A, 1f), Is.EqualTo(350));

            // 245 x 0.7 = 171.5 -> 172 (half away from zero, from the exact decimal 0.7).
            s.pathACost = 95;
            Assert.That(TowerUpgradeMath.TotalInvested(s, 4, UpgradeBranch.A), Is.EqualTo(295));
            s.baseCost = 0; s.level2Cost = 60; s.level3Cost = 90; s.pathACost = 95; // 245 invested
            Assert.That(TowerUpgradeMath.SellValue(s, 4, UpgradeBranch.A, 0.7f), Is.EqualTo(172));
            // 15 x 0.5 = 7.5 -> 8
            var t = TowerUpgradeSpec.Neutral(1f, 1f, 1f, 15);
            Assert.That(TowerUpgradeMath.SellValue(t, 1, UpgradeBranch.None, 0.5f), Is.EqualTo(8));
        }

        [Test]
        public void InvalidStates_Throw()
        {
            var s = DefaultTower();
            Assert.Throws<ArgumentOutOfRangeException>(() => TowerUpgradeMath.StatsAt(s, 0, UpgradeBranch.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => TowerUpgradeMath.StatsAt(s, 5, UpgradeBranch.A));
            Assert.Throws<ArgumentException>(() => TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.None));
            Assert.Throws<ArgumentException>(() => TowerUpgradeMath.StatsAt(s, 3, UpgradeBranch.A));
            Assert.Throws<ArgumentException>(() => TowerUpgradeMath.StatsAt(s, 2, UpgradeBranch.B));
            Assert.Throws<ArgumentException>(() => TowerUpgradeMath.StatsAt(s, 4, (UpgradeBranch)7));
            Assert.Throws<ArgumentException>(() => TowerUpgradeMath.NextUpgradeCost(s, 3, UpgradeBranch.B));
            Assert.Throws<ArgumentException>(() => TowerUpgradeMath.TotalInvested(s, 4, UpgradeBranch.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => TowerUpgradeMath.SellValue(s, 9, UpgradeBranch.None, 0.7f));

            for (int level = 1; level <= 3; level++)
            {
                Assert.That(TowerUpgradeMath.IsValidState(level, UpgradeBranch.None), Is.True);
                Assert.That(TowerUpgradeMath.IsValidState(level, UpgradeBranch.A), Is.False);
                Assert.That(TowerUpgradeMath.IsValidState(level, UpgradeBranch.B), Is.False);
            }
            Assert.That(TowerUpgradeMath.IsValidState(4, UpgradeBranch.A), Is.True);
            Assert.That(TowerUpgradeMath.IsValidState(4, UpgradeBranch.B), Is.True);
            Assert.That(TowerUpgradeMath.IsValidState(4, UpgradeBranch.None), Is.False);
            Assert.That(TowerUpgradeMath.IsValidState(5, UpgradeBranch.A), Is.False);
            Assert.That(TowerUpgradeMath.MaxLinearLevel, Is.EqualTo(3));
            Assert.That(TowerUpgradeMath.MaxLevel, Is.EqualTo(4));
        }

        [Test]
        public void BrokenAssetValues_AreSanitised()
        {
            var s = PoisonSplash();
            s.level2DamageMult = 0f;            // treated as "no change"
            s.level3DamageMult = float.PositiveInfinity;
            s.pathADamageMult = float.NaN;
            s.level3RangeMult = -3f;
            s.level2PoisonMult = -1f;
            s.pathAPoisonMult = float.NaN;
            s.level2SplashBonus = -5f;          // splash never shrinks
            s.level3SplashBonus = float.NaN;
            s.level3Cost = -10;
            s.pathACost = -1;

            var st = TowerUpgradeMath.StatsAt(s, 4, UpgradeBranch.A);
            Assert.That(st.Damage, Is.EqualTo(5f).Within(Eps));
            Assert.That(st.Range, Is.EqualTo(4f * 1.1f * 1.25f).Within(Eps));
            Assert.That(st.PoisonDps, Is.EqualTo(4f * 1.5f).Within(Eps));
            Assert.That(st.SplashRadius, Is.EqualTo(1f).Within(Eps));
            Assert.That(TowerUpgradeMath.NextUpgradeCost(s, 2, UpgradeBranch.None), Is.EqualTo(0));
            Assert.That(TowerUpgradeMath.BranchCost(s, UpgradeBranch.A), Is.EqualTo(0));
            Assert.That(TowerUpgradeMath.TotalInvested(s, 4, UpgradeBranch.A), Is.EqualTo(110));

            var broken = TowerUpgradeSpec.Neutral(float.NaN, -1f, float.NegativeInfinity, -50);
            broken.splashRadius = float.NaN; broken.poisonDps = -3f; broken.appliesPoison = true;
            var b = TowerUpgradeMath.StatsAt(broken, 4, UpgradeBranch.B);
            Assert.That(b.Range, Is.EqualTo(0f));
            Assert.That(b.FireRate, Is.EqualTo(0f));
            Assert.That(b.Damage, Is.EqualTo(0f));
            Assert.That(b.SplashRadius, Is.EqualTo(0f));
            Assert.That(b.PoisonDps, Is.EqualTo(0f));
            Assert.That(TowerUpgradeMath.TotalInvested(broken, 1, UpgradeBranch.None), Is.EqualTo(0));
        }

        [Test]
        public void TotalInvested_DoesNotOverflow()
        {
            var s = TowerUpgradeSpec.Neutral(1f, 1f, 1f, int.MaxValue);
            s.level2Cost = int.MaxValue; s.level3Cost = int.MaxValue; s.pathBCost = int.MaxValue;
            Assert.That(TowerUpgradeMath.TotalInvested(s, 4, UpgradeBranch.B), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void NeutralSpec_KeepsStatsAcrossLevels()
        {
            var s = TowerUpgradeSpec.Neutral(3f, 2f, 10f, 70);
            s.appliesPoison = true; s.poisonDps = 2f; s.splashRadius = 0.5f;
            for (int level = 1; level <= 4; level++)
            {
                var st = TowerUpgradeMath.StatsAt(s, level, level == 4 ? UpgradeBranch.B : UpgradeBranch.None);
                Assert.That(st.Range, Is.EqualTo(3f));
                Assert.That(st.FireRate, Is.EqualTo(2f));
                Assert.That(st.Damage, Is.EqualTo(10f));
                Assert.That(st.PoisonDps, Is.EqualTo(2f));
                Assert.That(st.SplashRadius, Is.EqualTo(0.5f));
            }
        }
    }
}
