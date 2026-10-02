using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    public class DamageTableTests
    {
        [TestCase(DamageType.Physical, ArmorType.None, 1f)]
        [TestCase(DamageType.Physical, ArmorType.Light, 0.85f)]
        [TestCase(DamageType.Physical, ArmorType.Heavy, 0.55f)]
        [TestCase(DamageType.Magic, ArmorType.None, 1f)]
        [TestCase(DamageType.Magic, ArmorType.Light, 1f)]
        [TestCase(DamageType.Magic, ArmorType.Heavy, 1.25f)]
        [TestCase(DamageType.Poison, ArmorType.None, 1f)]
        [TestCase(DamageType.Poison, ArmorType.Light, 1f)]
        [TestCase(DamageType.Poison, ArmorType.Heavy, 1.25f)]
        public void DefaultTable_MatchesArmorTriangle(DamageType type, ArmorType armor, float expected)
        {
            Assert.That(DamageTable.Default.Multiplier(type, armor), Is.EqualTo(expected).Within(1e-6f));
        }

        [Test]
        public void Triangle_PhysicalWorstVsHeavy_MagicBestVsHeavy()
        {
            var t = DamageTable.Default;
            Assert.That(t.Multiplier(DamageType.Physical, ArmorType.Heavy),
                Is.LessThan(t.Multiplier(DamageType.Physical, ArmorType.Light)));
            Assert.That(t.Multiplier(DamageType.Physical, ArmorType.Light),
                Is.LessThan(t.Multiplier(DamageType.Physical, ArmorType.None)));
            Assert.That(t.Multiplier(DamageType.Magic, ArmorType.Heavy),
                Is.GreaterThan(t.Multiplier(DamageType.Physical, ArmorType.Heavy)));
        }

        [Test]
        public void EveryPair_IsPositiveAndFinite()
        {
            foreach (DamageType d in System.Enum.GetValues(typeof(DamageType)))
            foreach (ArmorType a in System.Enum.GetValues(typeof(ArmorType)))
            {
                float m = DamageTable.Default.Multiplier(d, a);
                Assert.That(m, Is.GreaterThan(0f));
                Assert.That(float.IsNaN(m) || float.IsInfinity(m), Is.False);
            }
        }

        [Test]
        public void Apply_ScalesDamage_AndRejectsInvalidAmounts()
        {
            var t = DamageTable.Default;
            Assert.That(t.Apply(100f, DamageType.Physical, ArmorType.Heavy), Is.EqualTo(55f).Within(1e-3f));
            Assert.That(t.Apply(0f, DamageType.Magic, ArmorType.None), Is.EqualTo(0f));
            Assert.That(t.Apply(-5f, DamageType.Magic, ArmorType.None), Is.EqualTo(0f));
            Assert.That(t.Apply(float.NaN, DamageType.Magic, ArmorType.None), Is.EqualTo(0f));
        }

        [Test]
        public void CustomTable_OverridesOnePair_WithoutTouchingDefault()
        {
            var t = DamageTable.CreateDefault();
            t.Set(DamageType.Magic, ArmorType.Heavy, 2f);
            t.Set(DamageType.Physical, ArmorType.None, -1f);
            Assert.That(t.Multiplier(DamageType.Magic, ArmorType.Heavy), Is.EqualTo(2f));
            Assert.That(t.Multiplier(DamageType.Physical, ArmorType.None), Is.EqualTo(0f));
            Assert.That(DamageTable.Default.Multiplier(DamageType.Magic, ArmorType.Heavy), Is.EqualTo(1.25f));
        }

        [Test]
        public void UnknownEnumValue_IsNeutral()
        {
            Assert.That(DamageTable.Default.Multiplier((DamageType)99, ArmorType.Heavy), Is.EqualTo(1f));
            Assert.That(new DamageTable().Multiplier(DamageType.Physical, ArmorType.Heavy), Is.EqualTo(1f));
        }
    }

    public class EconomyRulesTests
    {
        [TestCase(5, 1, 5)]
        [TestCase(5, 2, 5)]   // 5.25 -> 5
        [TestCase(5, 11, 8)]  // 7.5 -> 8 (half away from zero)
        [TestCase(5, 21, 10)]
        public void KillReward_GrowsLinearlyWithWave(int baseReward, int wave, int expected)
        {
            Assert.That(EconomyRules.KillReward(baseReward, wave, 0.05f), Is.EqualTo(expected));
        }

        [Test]
        public void KillReward_TypeRatio_AndMinimumOfOne()
        {
            Assert.That(EconomyRules.KillReward(5, 1, 0.05f, 2.0), Is.EqualTo(10));
            Assert.That(EconomyRules.KillReward(5, 1, 0.05f, 0.01), Is.EqualTo(1));
            Assert.That(EconomyRules.KillReward(0, 10, 0.05f), Is.EqualTo(0));
            Assert.That(EconomyRules.KillReward(5, 10, 0.05f, double.NaN), Is.EqualTo(0));
        }

        [Test]
        public void KillReward_IsMonotonicInWave_AndNeverOverflows()
        {
            int prev = 0;
            for (int w = 1; w <= 5000; w++)
            {
                int r = EconomyRules.KillReward(5, w, 0.05f);
                Assert.That(r, Is.GreaterThanOrEqualTo(prev));
                Assert.That(r, Is.GreaterThan(0));
                prev = r;
            }
            Assert.That(EconomyRules.KillReward(int.MaxValue, int.MaxValue, 100f, 1000.0), Is.EqualTo(EconomyRules.MaxGold));
        }

        [Test]
        public void KillReward_NegativeGrowth_IsTreatedAsFlat()
        {
            Assert.That(EconomyRules.KillReward(5, 50, -1f), Is.EqualTo(5));
        }

        [TestCase(5f, 2f, 10)]
        [TestCase(2.25f, 2f, 5)]  // 4.5 -> 5
        [TestCase(0f, 2f, 0)]
        [TestCase(-3f, 2f, 0)]
        [TestCase(5f, 0f, 0)]
        [TestCase(float.NaN, 2f, 0)]
        public void EarlyCallBonus(float seconds, float perSecond, int expected)
        {
            Assert.That(EconomyRules.EarlyCallBonus(seconds, perSecond), Is.EqualTo(expected));
        }

        [TestCase(100, 0.7f, 70)]
        [TestCase(125, 0.7f, 88)]  // 87.5 -> 88
        [TestCase(100, 1.5f, 100)] // clamped to full refund
        [TestCase(100, -1f, 0)]
        [TestCase(0, 0.7f, 0)]
        [TestCase(-50, 0.7f, 0)]
        public void SellRefund(int invested, float fraction, int expected)
        {
            Assert.That(EconomyRules.SellRefund(invested, fraction), Is.EqualTo(expected));
        }

        [Test]
        public void RoundGold_HandlesEdgeValues()
        {
            Assert.That(EconomyRules.RoundGold(double.NaN), Is.EqualTo(0));
            Assert.That(EconomyRules.RoundGold(double.PositiveInfinity), Is.EqualTo(EconomyRules.MaxGold));
            Assert.That(EconomyRules.RoundGold(-1.0), Is.EqualTo(0));
            Assert.That(EconomyRules.RoundGold(0.5), Is.EqualTo(1));
        }
    }

    public class StarRatingTests
    {
        [TestCase(20, 20, 3)]
        [TestCase(18, 20, 3)]
        [TestCase(17, 20, 2)]
        [TestCase(6, 20, 2)]
        [TestCase(5, 20, 1)]
        [TestCase(1, 20, 1)]
        [TestCase(0, 20, 0)]
        public void KingdomRushThresholds_On20Lives(int lives, int start, int stars)
        {
            Assert.That(StarRating.Rate(lives, start), Is.EqualTo(stars));
        }

        [Test]
        public void Loss_IsAlwaysZeroStars()
        {
            Assert.That(StarRating.Rate(20, 20, victory: false), Is.EqualTo(0));
        }

        [Test]
        public void PerfectRun_IsThreeStars_ForAnyStartingLives()
        {
            for (int start = 1; start <= 100; start++)
                Assert.That(StarRating.Rate(start, start), Is.EqualTo(3), $"start={start}");
        }

        [Test]
        public void Stars_AreMonotonicInLivesRemaining()
        {
            for (int start = 1; start <= 50; start++)
            {
                int prev = 0;
                for (int lives = 0; lives <= start; lives++)
                {
                    int s = StarRating.Rate(lives, start);
                    Assert.That(s, Is.GreaterThanOrEqualTo(prev));
                    Assert.That(s, Is.InRange(0, 3));
                    prev = s;
                }
            }
        }

        [Test]
        public void LivesAboveStart_AreClamped_AndZeroStartIsThreeStars()
        {
            Assert.That(StarRating.Rate(50, 20), Is.EqualTo(3));
            Assert.That(StarRating.Rate(1, 0), Is.EqualTo(3));
        }

        [Test]
        public void LivesNeeded_MatchesThresholds()
        {
            Assert.That(StarRating.LivesNeeded(20, StarRating.DefaultThreeStarFraction), Is.EqualTo(18));
            Assert.That(StarRating.LivesNeeded(20, StarRating.DefaultTwoStarFraction), Is.EqualTo(6));
            Assert.That(StarRating.LivesNeeded(10, 0.9f), Is.EqualTo(9));
            Assert.That(StarRating.LivesNeeded(1, 0.3f), Is.EqualTo(1));
            Assert.That(StarRating.LivesNeeded(0, 0.9f), Is.EqualTo(0));
        }

        [Test]
        public void CustomThresholds()
        {
            Assert.That(StarRating.Rate(10, 10, true, 1f, 0.5f), Is.EqualTo(3));
            Assert.That(StarRating.Rate(9, 10, true, 1f, 0.5f), Is.EqualTo(2));
            Assert.That(StarRating.Rate(4, 10, true, 1f, 0.5f), Is.EqualTo(1));
        }
    }
}
