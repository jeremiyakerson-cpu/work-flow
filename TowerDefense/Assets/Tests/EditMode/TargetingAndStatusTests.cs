using NUnit.Framework;
using TowerDefense.Core;

namespace TowerDefense.Tests
{
    public class TargetSelectorTests
    {
        // index: progress, remaining, health, distSqr, flying
        private static TargetCandidate[] Sample() => new[]
        {
            new TargetCandidate(10f, 30f, 50f, 9f, false),   // 0: mid-path, healthy, far
            new TargetCandidate(25f, 15f, 20f, 4f, false),   // 1: furthest along (First)
            new TargetCandidate(2f, 38f, 80f, 1f, false),    // 2: last, strongest, closest
            new TargetCandidate(15f, 25f, 5f, 16f, true),    // 3: flying, weakest
        };

        [TestCase(TargetPriority.First, 1)]
        [TestCase(TargetPriority.Last, 2)]
        [TestCase(TargetPriority.Strongest, 2)]
        [TestCase(TargetPriority.Weakest, 3)]
        [TestCase(TargetPriority.Closest, 2)]
        public void PicksPerPriority(TargetPriority p, int expected)
        {
            var c = Sample();
            Assert.That(TargetSelector.SelectIndex(c, c.Length, p), Is.EqualTo(expected));
        }

        [Test]
        public void GroundOnlyTower_SkipsFlyers()
        {
            var c = Sample();
            Assert.That(TargetSelector.SelectIndex(c, c.Length, TargetPriority.Weakest, canTargetGround: true, canTargetFlying: false),
                Is.EqualTo(1));
        }

        [Test]
        public void AirOnlyTower_OnlyFlyers()
        {
            var c = Sample();
            Assert.That(TargetSelector.SelectIndex(c, c.Length, TargetPriority.First, canTargetGround: false, canTargetFlying: true),
                Is.EqualTo(3));
        }

        [Test]
        public void NoTargetable_ReturnsMinusOne()
        {
            var c = Sample();
            Assert.That(TargetSelector.SelectIndex(c, c.Length, TargetPriority.First, false, false), Is.EqualTo(-1));
            Assert.That(TargetSelector.SelectIndex(c, 0, TargetPriority.First), Is.EqualTo(-1));
            Assert.That(TargetSelector.SelectIndex(null, 3, TargetPriority.First), Is.EqualTo(-1));
        }

        [Test]
        public void Count_LimitsTheScan_AndIsClampedToBuffer()
        {
            var c = Sample();
            Assert.That(TargetSelector.SelectIndex(c, 1, TargetPriority.First), Is.EqualTo(0));
            Assert.That(TargetSelector.SelectIndex(c, 99, TargetPriority.First), Is.EqualTo(1));
        }

        [Test]
        public void First_UsesRemainingDistance_AcrossPathsOfDifferentLength()
        {
            // Enemy 0 has walked further but sits on a longer path: enemy 1 is closer to the exit.
            var c = new[]
            {
                new TargetCandidate(40f, 20f, 10f, 1f, false),
                new TargetCandidate(30f, 5f, 10f, 1f, false),
            };
            Assert.That(TargetSelector.SelectIndex(c, 2, TargetPriority.First), Is.EqualTo(1));
            Assert.That(TargetSelector.SelectIndex(c, 2, TargetPriority.Last), Is.EqualTo(0));
        }

        [Test]
        public void Ties_BreakTowardsFirst_ThenLowerIndex()
        {
            var c = new[]
            {
                new TargetCandidate(5f, 20f, 10f, 4f, false),
                new TargetCandidate(9f, 16f, 10f, 4f, false),
                new TargetCandidate(9f, 16f, 10f, 4f, false),
            };
            Assert.That(TargetSelector.SelectIndex(c, 3, TargetPriority.Strongest), Is.EqualTo(1));
            Assert.That(TargetSelector.SelectIndex(c, 3, TargetPriority.Weakest), Is.EqualTo(1));
            Assert.That(TargetSelector.SelectIndex(c, 3, TargetPriority.Closest), Is.EqualTo(1));
            Assert.That(TargetSelector.SelectIndex(c, 3, TargetPriority.First), Is.EqualTo(1));
        }

        [Test]
        public void NaNCandidate_IsNeverPreferred()
        {
            var c = new[]
            {
                new TargetCandidate(float.NaN, float.NaN, float.NaN, float.NaN, false),
                new TargetCandidate(1f, 10f, 3f, 2f, false),
            };
            foreach (TargetPriority p in System.Enum.GetValues(typeof(TargetPriority)))
                Assert.That(TargetSelector.SelectIndex(c, 2, p), Is.EqualTo(1), p.ToString());
        }

        [Test]
        public void SelectedCandidate_IsOptimal_OnRandomSets()
        {
            var rng = new DeterministicRandom(42);
            var c = new TargetCandidate[16];
            for (int round = 0; round < 200; round++)
            {
                int n = 1 + rng.Range(0, c.Length);
                for (int i = 0; i < n; i++)
                    c[i] = new TargetCandidate((float)rng.NextDouble() * 50f, (float)rng.NextDouble() * 50f,
                        (float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 25f, rng.Range(0, 4) == 0);

                foreach (TargetPriority p in System.Enum.GetValues(typeof(TargetPriority)))
                {
                    int best = TargetSelector.SelectIndex(c, n, p);
                    Assert.That(best, Is.InRange(0, n - 1));
                    for (int i = 0; i < n; i++)
                        Assert.That(TargetSelector.IsBetter(c[i], c[best], p), Is.False, $"{p}: {i} beats {best}");
                }
            }
        }
    }

    public class StatusEffectTests
    {
        [Test]
        public void Slow_DefaultIsInactive()
        {
            var s = default(SlowEffect);
            Assert.That(s.IsActive, Is.False);
            Assert.That(s.CurrentMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void Slow_AppliesAndExpires()
        {
            var s = SlowEffect.None;
            Assert.That(s.Apply(0.5f, 1f), Is.True);
            Assert.That(s.CurrentMultiplier, Is.EqualTo(0.5f));
            s.Tick(0.6f);
            Assert.That(s.IsActive, Is.True);
            s.Tick(0.6f);
            Assert.That(s.IsActive, Is.False);
            Assert.That(s.CurrentMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void Slow_StrongerReplaces_WeakerIgnored_EqualRefreshes()
        {
            var s = SlowEffect.None;
            s.Apply(0.6f, 2f);
            Assert.That(s.Apply(0.8f, 10f), Is.False, "weaker slow must not extend a stronger one");
            Assert.That(s.TimeRemaining, Is.EqualTo(2f));

            Assert.That(s.Apply(0.4f, 1f), Is.True);
            Assert.That(s.Multiplier, Is.EqualTo(0.4f));
            Assert.That(s.TimeRemaining, Is.EqualTo(1f));

            Assert.That(s.Apply(0.4f, 3f), Is.True);
            Assert.That(s.TimeRemaining, Is.EqualTo(3f));
            Assert.That(s.Apply(0.4f, 0.5f), Is.False, "equal slow keeps the longer timer");
        }

        [Test]
        public void Slow_WeakerApplies_OnceStrongerExpired()
        {
            var s = SlowEffect.None;
            s.Apply(0.3f, 0.5f);
            s.Tick(1f);
            Assert.That(s.Apply(0.8f, 1f), Is.True);
            Assert.That(s.CurrentMultiplier, Is.EqualTo(0.8f));
        }

        [Test]
        public void Slow_RejectsInvalidInput_AndClampsBelowZero()
        {
            var s = SlowEffect.None;
            Assert.That(s.Apply(1f, 2f), Is.False);
            Assert.That(s.Apply(1.5f, 2f), Is.False);
            Assert.That(s.Apply(0.5f, 0f), Is.False);
            Assert.That(s.Apply(float.NaN, 2f), Is.False);
            Assert.That(s.Apply(-1f, 2f), Is.True);
            Assert.That(s.CurrentMultiplier, Is.EqualTo(0f));
        }

        [Test]
        public void Poison_TotalDamage_IsDpsTimesDuration_RegardlessOfFrameRate()
        {
            foreach (float dt in new[] { 1f / 30f, 1f / 60f, 0.25f, 0.7f })
            {
                var p = new PoisonEffect();
                p.Apply(4f, 3f);
                float total = 0f;
                for (int i = 0; i < 1000 && p.IsActive; i++) total += p.Tick(dt);
                Assert.That(total, Is.EqualTo(12f).Within(1e-3f), $"dt={dt}");
                Assert.That(p.IsActive, Is.False);
                Assert.That(p.Tick(dt), Is.EqualTo(0f));
            }
        }

        [Test]
        public void Poison_StrongerReplaces_WeakerIgnored_EqualRefreshes()
        {
            var p = new PoisonEffect();
            p.Apply(2f, 3f);
            Assert.That(p.Apply(1f, 10f), Is.False);
            Assert.That(p.TimeRemaining, Is.EqualTo(3f));
            Assert.That(p.Apply(5f, 1f), Is.True);
            Assert.That(p.DamagePerSecond, Is.EqualTo(5f));
            Assert.That(p.TimeRemaining, Is.EqualTo(1f));
            Assert.That(p.Apply(5f, 2f), Is.True);
            Assert.That(p.TimeRemaining, Is.EqualTo(2f));
        }

        [Test]
        public void Poison_RejectsInvalidInput_AndClears()
        {
            var p = new PoisonEffect();
            Assert.That(p.Apply(0f, 3f), Is.False);
            Assert.That(p.Apply(3f, -1f), Is.False);
            Assert.That(p.Apply(float.NaN, 3f), Is.False);
            Assert.That(p.Tick(-1f), Is.EqualTo(0f));
            p.Apply(3f, 3f);
            Assert.That(p.Tick(float.NaN), Is.EqualTo(0f));
            p.Clear();
            Assert.That(p.IsActive, Is.False);
        }
    }

    public class DeterministicRandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new DeterministicRandom(123);
            var b = new DeterministicRandom(123);
            for (int i = 0; i < 100; i++) Assert.That(a.NextULong(), Is.EqualTo(b.NextULong()));
        }

        [Test]
        public void KnownFirstValue_PinsTheAlgorithm()
        {
            // SplitMix64 reference: seed 0 -> 0xE220A8397B1DCDAF.
            Assert.That(new DeterministicRandom(0).NextULong(), Is.EqualTo(0xE220A8397B1DCDAFUL));
        }

        [Test]
        public void Range_StaysInBounds_AndHitsEveryValue()
        {
            var r = new DeterministicRandom(7);
            var seen = new bool[5];
            for (int i = 0; i < 1000; i++)
            {
                int v = r.Range(-2, 3);
                Assert.That(v, Is.InRange(-2, 2));
                seen[v + 2] = true;
            }
            Assert.That(seen, Is.All.True);
            Assert.That(r.Range(4, 4), Is.EqualTo(4));
            Assert.That(r.Range(int.MinValue, int.MaxValue), Is.InRange(int.MinValue, int.MaxValue - 1));
        }

        [Test]
        public void NextDouble_InUnitInterval()
        {
            var r = new DeterministicRandom(99);
            for (int i = 0; i < 1000; i++) Assert.That(r.NextDouble(), Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
        }

        [Test]
        public void Combine_SeparatesStreams()
        {
            Assert.That(DeterministicRandom.Combine(1, 2), Is.Not.EqualTo(DeterministicRandom.Combine(2, 1)));
            Assert.That(DeterministicRandom.Combine(1, 2), Is.EqualTo(DeterministicRandom.Combine(1, 2)));
        }
    }
}
