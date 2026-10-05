using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Content;
using TowerDefense.Core;

namespace TowerDefense.ContentTests
{
    /// <summary>
    /// Data rules for the shipped towers (Content/TowerBalance.cs, the numbers
    /// DefaultContent copies into TowerData): KR-like cost curve, a visible power
    /// jump at every step, effect growth and the role identities.
    /// </summary>
    public class TowerBalanceTests
    {
        private static readonly UpgradeBranch[] Branches = { UpgradeBranch.A, UpgradeBranch.B };

        private static List<TowerBalanceEntry> Towers() => TowerBalance.Create();

        private static TowerBalanceEntry Find(string id) => Towers().Find(t => t.Id == id);

        private static TowerStats At(TowerBalanceEntry t, int level, UpgradeBranch b = UpgradeBranch.None) =>
            TowerUpgradeMath.StatsAt(t.Spec, level, b);

        [Test]
        public void ShipsTheFiveTowers_InShopOrder()
        {
            var ids = Towers().ConvertAll(t => t.Id);
            Assert.That(ids, Is.EqualTo(new[] { ContentIds.Archer, ContentIds.Mage, ContentIds.Artillery, ContentIds.Frost, ContentIds.Alchemist }));
        }

        [Test]
        public void CostCurve_EachLinearStepCostsMore_SpecializationIsMostExpensive()
        {
            foreach (var t in Towers())
            {
                var s = t.Spec;
                Assert.That(s.baseCost, Is.GreaterThan(0), t.Id);
                Assert.That(s.level2Cost, Is.GreaterThan(s.baseCost), t.Id + ": L2 > build");
                Assert.That(s.level3Cost, Is.GreaterThan(s.level2Cost), t.Id + ": L3 > L2");
                Assert.That(s.pathACost, Is.GreaterThan(s.level3Cost), t.Id + ": spec A > L3");
                Assert.That(s.pathBCost, Is.GreaterThan(s.level3Cost), t.Id + ": spec B > L3");
            }
        }

        [Test]
        public void TotalInvested_StaysAroundFiveToSixTimesTheBuildCost()
        {
            foreach (var t in Towers())
                foreach (var b in Branches)
                {
                    int total = TowerUpgradeMath.TotalInvested(t.Spec, 4, b);
                    Assert.That(total, Is.InRange(5 * t.Spec.baseCost, 7 * t.Spec.baseCost), t.Id + " " + b);
                }
        }

        [Test]
        public void Dps_StrictlyIncreases_L1_L2_L3_ThenEitherSpecialization_ForEveryTower()
        {
            foreach (var t in Towers())
            {
                TowerStats l1 = At(t, 1), l2 = At(t, 2), l3 = At(t, 3);
                Assert.That(l2.Dps, Is.GreaterThan(l1.Dps), t.Id + " L2 > L1");
                Assert.That(l3.Dps, Is.GreaterThan(l2.Dps), t.Id + " L3 > L2");
                Assert.That(l2.TotalDps, Is.GreaterThan(l1.TotalDps), t.Id + " total L2 > L1");
                Assert.That(l3.TotalDps, Is.GreaterThan(l2.TotalDps), t.Id + " total L3 > L2");
                foreach (var b in Branches)
                {
                    TowerStats spec = At(t, 4, b);
                    Assert.That(spec.Dps, Is.GreaterThan(l3.Dps), t.Id + " " + b + " > L3");
                    Assert.That(spec.TotalDps, Is.GreaterThan(l3.TotalDps), t.Id + " total " + b + " > L3");
                }
            }
        }

        [Test]
        public void EveryStep_IsAVisiblePowerJump()
        {
            foreach (var t in Towers())
            {
                float l1 = At(t, 1).TotalDps, l2 = At(t, 2).TotalDps, l3 = At(t, 3).TotalDps;
                Assert.That(l2 / l1, Is.GreaterThanOrEqualTo(1.35f), t.Id + " L1->L2");
                Assert.That(l3 / l2, Is.GreaterThanOrEqualTo(1.35f), t.Id + " L2->L3");
                foreach (var b in Branches)
                    Assert.That(At(t, 4, b).TotalDps / l3, Is.GreaterThanOrEqualTo(1.4f), t.Id + " L3->" + b);
            }
        }

        [Test]
        public void EveryState_HasFinitePositiveStats_AndRangeNeverShrinks()
        {
            foreach (var t in Towers())
            {
                float prevRange = 0f;
                for (int level = 1; level <= 3; level++)
                {
                    TowerStats st = At(t, level);
                    AssertSane(t.Id, st);
                    Assert.That(st.Range, Is.GreaterThan(prevRange), t.Id + " range grows with level " + level);
                    prevRange = st.Range;
                }
                foreach (var b in Branches)
                {
                    TowerStats st = At(t, 4, b);
                    AssertSane(t.Id, st);
                    Assert.That(st.Range, Is.GreaterThanOrEqualTo(prevRange), t.Id + " " + b);
                }
            }
        }

        private static void AssertSane(string id, TowerStats st)
        {
            Assert.That(float.IsFinite(st.Damage) && st.Damage > 0f, Is.True, id + " damage");
            Assert.That(float.IsFinite(st.Range) && st.Range > 0f, Is.True, id + " range");
            Assert.That(float.IsFinite(st.FireRate) && st.FireRate > 0f, Is.True, id + " rate");
            Assert.That(st.Range, Is.LessThan(7f), id + " range stays below a third of the 18-unit map height");
        }

        [Test]
        public void SplashTowers_GrowTheirBlastEveryLinearLevel()
        {
            foreach (var id in new[] { ContentIds.Artillery, ContentIds.Alchemist })
            {
                var t = Find(id);
                Assert.That(At(t, 1).SplashRadius, Is.GreaterThan(0f), id);
                Assert.That(At(t, 2).SplashRadius, Is.GreaterThan(At(t, 1).SplashRadius), id + " L2");
                Assert.That(At(t, 3).SplashRadius, Is.GreaterThan(At(t, 2).SplashRadius), id + " L3");
                foreach (var b in Branches)
                    Assert.That(At(t, 4, b).SplashRadius, Is.GreaterThanOrEqualTo(At(t, 3).SplashRadius), id + " " + b);
            }
            var bertha = At(Find(ContentIds.Artillery), 4, UpgradeBranch.A);
            Assert.That(bertha.SplashRadius, Is.GreaterThan(At(Find(ContentIds.Artillery), 4, UpgradeBranch.B).SplashRadius),
                "Big Bertha has the biggest blast");
        }

        [Test]
        public void Alchemist_PoisonGrowsEveryStep_PlagueDoctorIsThePoisonSpecialist()
        {
            var t = Find(ContentIds.Alchemist);
            Assert.That(At(t, 1).PoisonDps, Is.GreaterThan(0f));
            Assert.That(At(t, 2).PoisonDps, Is.GreaterThan(At(t, 1).PoisonDps));
            Assert.That(At(t, 3).PoisonDps, Is.GreaterThan(At(t, 2).PoisonDps));
            Assert.That(At(t, 4, UpgradeBranch.B).PoisonDps, Is.GreaterThan(At(t, 3).PoisonDps));
            Assert.That(At(t, 4, UpgradeBranch.A).PoisonDps, Is.GreaterThanOrEqualTo(2f * At(t, 3).PoisonDps - 1e-3f));
            Assert.That(At(t, 4, UpgradeBranch.B).AppliesSlow, Is.True, "Acid Rain slows");
            Assert.That(At(t, 4, UpgradeBranch.A).AppliesSlow, Is.False);
        }

        [Test]
        public void RoleIdentities_Hold()
        {
            var archer = Find(ContentIds.Archer);
            var mage = Find(ContentIds.Mage);
            var artillery = Find(ContentIds.Artillery);
            var frost = Find(ContentIds.Frost);
            var alchemist = Find(ContentIds.Alchemist);

            Assert.That(archer.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(archer.CanTargetFlying, Is.True);
            Assert.That(mage.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(artillery.CanTargetFlying, Is.False, "artillery is ground only");
            Assert.That(artillery.CanTargetGround, Is.True);
            Assert.That(artillery.Spec.splashRadius, Is.GreaterThan(0f));
            Assert.That(frost.Spec.appliesSlow, Is.True);
            Assert.That(frost.SlowMultiplier, Is.InRange(0.3f, 0.8f));
            Assert.That(alchemist.DamageType, Is.EqualTo(DamageType.Poison));
            Assert.That(alchemist.Spec.appliesPoison, Is.True);

            // Frost does little damage itself, at every level.
            for (int level = 1; level <= 3; level++)
                Assert.That(At(frost, level).Dps, Is.LessThan(At(archer, level).Dps), "frost L" + level);

            // The mage is the heavy-armor answer: beats the archer vs Heavy at every level and branch.
            var table = DamageTable.Default;
            float Heavy(TowerBalanceEntry t, int level, UpgradeBranch b) =>
                table.Apply(At(t, level, b).Dps, t.DamageType, ArmorType.Heavy);
            for (int level = 1; level <= 3; level++)
                Assert.That(Heavy(mage, level, UpgradeBranch.None), Is.GreaterThan(Heavy(archer, level, UpgradeBranch.None)), "L" + level);
            foreach (var b in Branches)
                foreach (var a in Branches)
                    Assert.That(Heavy(mage, 4, b), Is.GreaterThan(Heavy(archer, 4, a)), "mage " + b + " vs archer " + a);
        }

        [Test]
        public void Branches_HaveDistinctNamedEliteIdentities()
        {
            var names = new HashSet<string>();
            foreach (var t in Towers())
            {
                Assert.That(t.PathAName, Is.Not.Null.And.Not.Empty, t.Id);
                Assert.That(t.PathBName, Is.Not.Null.And.Not.Empty, t.Id);
                Assert.That(t.PathADescription, Is.Not.Null.And.Not.Empty, t.Id);
                Assert.That(t.PathBDescription, Is.Not.Null.And.Not.Empty, t.Id);
                Assert.That(t.PathADescription.Length, Is.LessThanOrEqualTo(80), t.Id + " A description is one line");
                Assert.That(t.PathBDescription.Length, Is.LessThanOrEqualTo(80), t.Id + " B description is one line");
                Assert.That(t.PathAName, Is.Not.EqualTo(t.PathBName), t.Id);
                Assert.That(names.Add(t.PathAName), Is.True, "duplicate elite name " + t.PathAName);
                Assert.That(names.Add(t.PathBName), Is.True, "duplicate elite name " + t.PathBName);

                // The two elites must play differently, not just be priced differently.
                TowerStats a = At(t, 4, UpgradeBranch.A), b = At(t, 4, UpgradeBranch.B);
                bool differs = a.Range != b.Range || a.FireRate != b.FireRate || a.SplashRadius != b.SplashRadius ||
                               a.PoisonDps != b.PoisonDps || a.AppliesSlow != b.AppliesSlow;
                Assert.That(differs, Is.True, t.Id + " branches differ in more than damage");
            }
        }

        [Test]
        public void BranchA_IsTheDamageAndRangeElite_BranchB_TheFireRateElite()
        {
            foreach (var t in Towers())
            {
                TowerStats l3 = At(t, 3), a = At(t, 4, UpgradeBranch.A), b = At(t, 4, UpgradeBranch.B);
                Assert.That(a.Range, Is.GreaterThan(l3.Range), t.Id + " A adds range");
                Assert.That(a.Damage, Is.GreaterThan(b.Damage), t.Id + " A hits harder per shot");
                Assert.That(b.FireRate, Is.GreaterThan(a.FireRate), t.Id + " B fires faster");
            }
        }
    }
}
