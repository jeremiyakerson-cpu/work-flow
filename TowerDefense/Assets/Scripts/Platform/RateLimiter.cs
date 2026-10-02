using System;

namespace TowerDefense.Platform
{
    /// <summary>Allows at most one event per <see cref="MinInterval"/> seconds of the supplied clock.</summary>
    public sealed class RateLimiter
    {
        private double last = double.NegativeInfinity;

        public double MinInterval { get; set; }

        public RateLimiter(double minInterval)
        {
            MinInterval = Math.Max(0.0, minInterval);
        }

        /// <summary>True if <see cref="TryAcquire"/> would succeed at <paramref name="now"/> (does not consume).</summary>
        public bool CanAcquire(double now) => now - last >= MinInterval;

        /// <summary>True (and consumes the slot) if enough time passed since the last accepted event.</summary>
        public bool TryAcquire(double now)
        {
            if (!CanAcquire(now)) return false;
            last = now;
            return true;
        }

        public void Reset() => last = double.NegativeInfinity;
    }
}
