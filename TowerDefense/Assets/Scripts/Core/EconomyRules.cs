using System;

namespace TowerDefense.Core
{
    /// <summary>
    /// Gold rules: kill rewards per wave, early-call bonus, sell refunds.
    /// All results are non-negative whole gold. Rounding is half-away-from-zero
    /// (12.5 -> 13), which is what players expect from a price tag.
    /// </summary>
    public static class EconomyRules
    {
        /// <summary>Largest gold value any rule returns, so compounding endless waves never overflow.</summary>
        public const int MaxGold = 1_000_000_000;

        /// <summary>Round to whole gold, half away from zero, clamped to [0, MaxGold]. NaN -> 0.</summary>
        public static int RoundGold(double value)
        {
            if (!(value > 0.0)) return 0;
            if (value >= MaxGold) return MaxGold;
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Wave-scaled reward multiplier: grows linearly by growthPerWave each wave
        /// (wave 1 = 1x, wave 21 at 0.05 = 2x). Negative growth is treated as 0.
        /// </summary>
        public static double RewardScale(int waveNumber, float growthPerWave)
        {
            int w = Math.Max(1, waveNumber);
            double g = growthPerWave > 0f ? Exact(growthPerWave) : 0.0;
            return 1.0 + g * (w - 1);
        }

        /// <summary>
        /// Gold for killing one enemy: the wave curve's base reward x RewardScale,
        /// x typeRatio (an enemy type's own reward relative to the reference type).
        /// Always at least 1 gold for a positive base, so every kill pays something.
        /// </summary>
        public static int KillReward(int baseReward, int waveNumber, float growthPerWave, double typeRatio = 1.0)
        {
            if (baseReward <= 0 || !(typeRatio > 0.0)) return 0;
            int waveReward = RoundGold(baseReward * RewardScale(waveNumber, growthPerWave));
            return Math.Max(1, RoundGold(waveReward * typeRatio));
        }

        /// <summary>
        /// Bonus for calling the next wave early: gold per second of countdown
        /// skipped. 0 when nothing is skipped.
        /// </summary>
        public static int EarlyCallBonus(float secondsRemaining, float goldPerSecond)
        {
            if (!(secondsRemaining > 0f) || !(goldPerSecond > 0f)) return 0;
            return RoundGold(Exact(secondsRemaining) * Exact(goldPerSecond));
        }

        /// <summary>Refund when selling: refundFraction (clamped 0..1) of everything invested.</summary>
        public static int SellRefund(int totalInvested, float refundFraction)
        {
            if (totalInvested <= 0) return 0;
            double f = refundFraction > 0f ? Math.Min(1.0, Exact(refundFraction)) : 0.0;
            return RoundGold(totalInvested * f);
        }

        /// <summary>
        /// Float -> double as the designer typed it: 0.7f becomes 0.7, not 0.69999998,
        /// so 125 x 0.7 rounds to 88 instead of 87.
        /// </summary>
        public static double Exact(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1e27f) return value;
            return (double)(decimal)value;
        }
    }
}
