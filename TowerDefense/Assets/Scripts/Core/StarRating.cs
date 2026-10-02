using System;

namespace TowerDefense.Core
{
    /// <summary>
    /// Kingdom Rush-style level result: 3 stars for losing (almost) nothing,
    /// 2 for a solid hold, 1 for scraping through, 0 for a loss.
    /// Defaults match KR on 20 lives: 18+ = 3 stars, 6+ = 2 stars.
    /// </summary>
    public static class StarRating
    {
        public const float DefaultThreeStarFraction = 0.9f;
        public const float DefaultTwoStarFraction = 0.3f;

        /// <summary>
        /// Stars for a finished level. victory = false always yields 0.
        /// A threshold fraction f of startingLives means "at least ceil(f x startingLives)
        /// lives left", so a perfect run always earns 3 stars even on 1-life levels.
        /// </summary>
        public static int Rate(int livesRemaining, int startingLives, bool victory = true,
                               float threeStarFraction = DefaultThreeStarFraction,
                               float twoStarFraction = DefaultTwoStarFraction)
        {
            if (!victory || livesRemaining <= 0) return 0;
            if (startingLives <= 0) return 3;
            int lives = Math.Min(livesRemaining, startingLives);
            if (lives >= LivesNeeded(startingLives, threeStarFraction)) return 3;
            if (lives >= LivesNeeded(startingLives, twoStarFraction)) return 2;
            return 1;
        }

        /// <summary>Minimum lives remaining for a threshold (for "keep 18 lives for 3 stars" UI hints).</summary>
        public static int LivesNeeded(int startingLives, float fraction)
        {
            if (startingLives <= 0) return 0;
            double f = fraction > 0f ? Math.Min(1.0, fraction) : 0.0;
            // Small epsilon so 0.9 * 20 = 18.000000002 doesn't ceil to 19.
            int needed = (int)Math.Ceiling(startingLives * f - 1e-6);
            return Math.Max(1, Math.Min(startingLives, needed));
        }
    }
}
