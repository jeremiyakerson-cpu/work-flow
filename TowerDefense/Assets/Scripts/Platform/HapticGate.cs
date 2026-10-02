using System;

namespace TowerDefense.Platform
{
    /// <summary>The iOS feedback styles exposed by TDHaptics.mm.</summary>
    public enum HapticFeedback
    {
        /// <summary>UIImpactFeedbackGenerator light: taps, small confirmations.</summary>
        Light,
        /// <summary>UIImpactFeedbackGenerator medium: build, upgrade.</summary>
        Medium,
        /// <summary>UIImpactFeedbackGenerator heavy: boss arrives / dies.</summary>
        Heavy,
        /// <summary>UINotificationFeedbackGenerator success: victory.</summary>
        Success,
        /// <summary>UINotificationFeedbackGenerator warning: a life lost.</summary>
        Warning,
        /// <summary>UINotificationFeedbackGenerator error: defeat, cannot afford.</summary>
        Error,
        /// <summary>UISelectionFeedbackGenerator: picker / tower selection ticks.</summary>
        Selection,
    }

    /// <summary>
    /// Engine-free gate in front of the native haptics: the settings toggle plus
    /// rate limits (a global floor and a per-style interval) so a burst of leaks
    /// at 3x speed is one buzz, not a continuous rattle.
    /// </summary>
    public sealed class HapticGate
    {
        public const double GlobalMinInterval = 0.05;

        private readonly RateLimiter global = new RateLimiter(GlobalMinInterval);
        private readonly RateLimiter[] perStyle;

        public bool Enabled { get; set; } = true;

        public HapticGate()
        {
            int n = Enum.GetValues(typeof(HapticFeedback)).Length;
            perStyle = new RateLimiter[n];
            for (int i = 0; i < n; i++) perStyle[i] = new RateLimiter(IntervalFor((HapticFeedback)i));
        }

        /// <summary>Minimum seconds between two haptics of the same style.</summary>
        public static double IntervalFor(HapticFeedback style)
        {
            switch (style)
            {
                case HapticFeedback.Selection: return 0.05;
                case HapticFeedback.Light: return 0.08;
                case HapticFeedback.Medium: return 0.12;
                case HapticFeedback.Heavy: return 0.30;
                default: return 0.60; // notifications are long patterns; never stack them
            }
        }

        /// <summary>True if a haptic of <paramref name="style"/> may fire at <paramref name="now"/> (real seconds).</summary>
        public bool TryFire(HapticFeedback style, double now)
        {
            if (!Enabled) return false;
            RateLimiter own = perStyle[(int)style];
            // Check both before consuming either, so a rejected request does not burn a slot.
            if (!own.CanAcquire(now) || !global.CanAcquire(now)) return false;
            global.TryAcquire(now);
            own.TryAcquire(now);
            return true;
        }

        public void Reset()
        {
            global.Reset();
            for (int i = 0; i < perStyle.Length; i++) perStyle[i].Reset();
        }
    }
}
