using NUnit.Framework;
using TowerDefense.Platform;

namespace TowerDefense.PlatformTests
{
    [TestFixture]
    public class HapticGateTests
    {
        [Test]
        public void RateLimiter_EnforcesInterval()
        {
            var r = new RateLimiter(0.1);
            Assert.IsTrue(r.TryAcquire(0));
            Assert.IsFalse(r.TryAcquire(0.05));
            Assert.IsTrue(r.CanAcquire(0.1));
            Assert.IsTrue(r.TryAcquire(0.1));
            r.Reset();
            Assert.IsTrue(r.TryAcquire(0.11));
        }

        [Test]
        public void Disabled_BlocksEverything()
        {
            var g = new HapticGate { Enabled = false };
            Assert.IsFalse(g.TryFire(HapticFeedback.Heavy, 10));
            g.Enabled = true;
            Assert.IsTrue(g.TryFire(HapticFeedback.Heavy, 10));
        }

        [Test]
        public void BurstOfLeaks_IsOneBuzzPerInterval()
        {
            var g = new HapticGate();
            int fired = 0;
            for (int i = 0; i < 100; i++) // 100 leaks within one real second
                if (g.TryFire(HapticFeedback.Warning, i * 0.01)) fired++;
            Assert.AreEqual((int)System.Math.Ceiling(1.0 / HapticGate.IntervalFor(HapticFeedback.Warning)), fired);
        }

        [Test]
        public void GlobalFloor_AppliesAcrossStyles_WithoutBurningPerStyleSlots()
        {
            var g = new HapticGate();
            Assert.IsTrue(g.TryFire(HapticFeedback.Heavy, 1.0));
            Assert.IsFalse(g.TryFire(HapticFeedback.Light, 1.01), "global floor");
            // The rejected Light did not consume its own slot:
            Assert.IsTrue(g.TryFire(HapticFeedback.Light, 1.0 + HapticGate.GlobalMinInterval));
        }

        [Test]
        public void EveryStyleHasAnInterval()
        {
            foreach (HapticFeedback s in System.Enum.GetValues(typeof(HapticFeedback)))
                Assert.Greater(HapticGate.IntervalFor(s), 0.0, s.ToString());
        }
    }
}
