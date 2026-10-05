using System;

namespace TowerDefense.Core
{
    /// <summary>
    /// Difficulty picked per level before it starts. Stored as its integer value
    /// in saves, so never reorder or renumber these.
    /// </summary>
    public enum DifficultyMode
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        /// <summary>Unlocked per map by earning 3 stars on Hard there. 1 life, no early-call bonus.</summary>
        Impossible = 3,
    }

    /// <summary>
    /// How one difficulty modifies a level. Every multiplier is relative to the
    /// level's authored (Normal) balance, so Normal is the identity everywhere and
    /// changing a level's data rebalances every difficulty at once.
    /// Immutable; get instances from <see cref="Difficulty.Rules"/>.
    /// </summary>
    public sealed class DifficultyRules
    {
        public DifficultyMode Mode { get; }
        /// <summary>Scales the wave health curve (every enemy, summons and bosses).</summary>
        public float EnemyHealthMultiplier { get; }
        /// <summary>Extra health scale applied to bosses on top of <see cref="EnemyHealthMultiplier"/>.</summary>
        public float BossHealthMultiplier { get; }
        /// <summary>Scales the wave speed curve (the endless speed cap still applies to growth only).</summary>
        public float EnemySpeedMultiplier { get; }
        /// <summary>Scales every kill reward (regular, summoned and boss).</summary>
        public float KillRewardMultiplier { get; }
        /// <summary>Scales the level's starting gold.</summary>
        public float StartingGoldMultiplier { get; }
        /// <summary>Scales the level's starting lives (ignored when <see cref="FixedLives"/> &gt; 0).</summary>
        public float LivesMultiplier { get; }
        /// <summary>When &gt; 0, every level starts with exactly this many lives.</summary>
        public int FixedLives { get; }
        /// <summary>Calling a wave early pays gold for the skipped countdown. The call itself always works.</summary>
        public bool EarlyCallBonusEnabled { get; }

        public DifficultyRules(DifficultyMode mode, float enemyHealthMultiplier, float bossHealthMultiplier,
                               float enemySpeedMultiplier, float killRewardMultiplier, float startingGoldMultiplier,
                               float livesMultiplier, int fixedLives, bool earlyCallBonusEnabled)
        {
            Mode = mode;
            EnemyHealthMultiplier = Sane(enemyHealthMultiplier);
            BossHealthMultiplier = Sane(bossHealthMultiplier);
            EnemySpeedMultiplier = Sane(enemySpeedMultiplier);
            KillRewardMultiplier = Sane(killRewardMultiplier);
            StartingGoldMultiplier = Sane(startingGoldMultiplier);
            LivesMultiplier = Sane(livesMultiplier);
            FixedLives = Math.Max(0, fixedLives);
            EarlyCallBonusEnabled = earlyCallBonusEnabled;
        }

        /// <summary>Total health scale a boss gets (enemy x boss multiplier).</summary>
        public float EffectiveBossHealthMultiplier => EnemyHealthMultiplier * BossHealthMultiplier;

        /// <summary>True when this mode changes nothing (Normal).</summary>
        public bool IsIdentity =>
            EnemyHealthMultiplier == 1f && BossHealthMultiplier == 1f && EnemySpeedMultiplier == 1f &&
            KillRewardMultiplier == 1f && StartingGoldMultiplier == 1f && LivesMultiplier == 1f &&
            FixedLives == 0 && EarlyCallBonusEnabled;

        /// <summary>Starting lives for a level authored with <paramref name="baseLives"/>. Never below 1.</summary>
        public int ApplyLives(int baseLives)
        {
            if (FixedLives > 0) return FixedLives;
            int lives = baseLives > 0 ? EconomyRules.RoundGold(baseLives * EconomyRules.Exact(LivesMultiplier)) : 1;
            return Math.Max(1, lives);
        }

        /// <summary>Starting gold for a level authored with <paramref name="baseGold"/> (whole gold, never negative).</summary>
        public int ApplyStartingGold(int baseGold) =>
            baseGold > 0 ? EconomyRules.RoundGold(baseGold * EconomyRules.Exact(StartingGoldMultiplier)) : 0;

        /// <summary>
        /// Early-call bonus under these rules: the normal bonus when enabled, else 0.
        /// </summary>
        public int EarlyCallBonus(float secondsRemaining, float goldPerSecond) =>
            EarlyCallBonusEnabled ? EconomyRules.EarlyCallBonus(secondsRemaining, goldPerSecond) : 0;

        /// <summary>
        /// Fold these rules into a wave curve (in place) so plans, previews and
        /// summons (WaveManager.ScaledStats) all scale identically:
        /// health via DifficultyMultiplier, speed via BaseSpeed and rewards via the
        /// reference reward (a type's reward is BaseReward / ReferenceReward x wave reward).
        /// Call once on a freshly built curve; Normal leaves it untouched.
        /// </summary>
        public void ApplyTo(WaveCurve curve)
        {
            if (curve == null) throw new ArgumentNullException(nameof(curve));
            if (EnemyHealthMultiplier != 1f) curve.DifficultyMultiplier = Scale(curve.DifficultyMultiplier, EnemyHealthMultiplier);
            if (EnemySpeedMultiplier != 1f) curve.BaseSpeed = Scale(curve.BaseSpeed, EnemySpeedMultiplier);
            if (KillRewardMultiplier != 1f && curve.ReferenceReward > 0f)
                curve.ReferenceReward = (float)(EconomyRules.Exact(curve.ReferenceReward) / EconomyRules.Exact(KillRewardMultiplier));
        }

        /// <summary>
        /// Adjust one boss-pool type (in place of the original) for these rules:
        /// boss health and boss reward multipliers. Regular types need nothing (the curve covers them).
        /// </summary>
        public EnemyTypeInfo ApplyToBoss(EnemyTypeInfo boss)
        {
            if (BossHealthMultiplier != 1f) boss.BossHealthMultiplier = Scale(boss.BossHealthMultiplier, BossHealthMultiplier);
            if (KillRewardMultiplier != 1f) boss.BossRewardMultiplier = Scale(boss.BossRewardMultiplier, KillRewardMultiplier);
            return boss;
        }

        private static float Scale(float value, float multiplier) =>
            (float)(EconomyRules.Exact(value) * EconomyRules.Exact(multiplier));

        private static float Sane(float v) => v > 0f && !float.IsInfinity(v) ? v : 1f;
    }

    /// <summary>
    /// The difficulty table, names, descriptions and unlock rules. Tune the
    /// numbers here (Docs/DIFFICULTY.md explains the rationale); keep the order
    /// Easy &lt; Normal &lt; Hard &lt; Impossible on every axis (tests enforce it).
    /// </summary>
    public static class Difficulty
    {
        /// <summary>Stars needed on <see cref="ImpossibleUnlockMode"/> on a map to unlock Impossible there.</summary>
        public const int ImpossibleUnlockStars = 3;
        public const DifficultyMode ImpossibleUnlockMode = DifficultyMode.Hard;
        public const DifficultyMode Default = DifficultyMode.Normal;

        /// <summary>Every mode, easiest first.</summary>
        public static readonly DifficultyMode[] All =
            { DifficultyMode.Easy, DifficultyMode.Normal, DifficultyMode.Hard, DifficultyMode.Impossible };

        //                                                 mode                       hp     boss   speed  reward gold   lives  fixed early
        private static readonly DifficultyRules EasyRules = new DifficultyRules(DifficultyMode.Easy, 0.70f, 0.90f, 0.90f, 1.15f, 1.30f, 1.50f, 0, true);
        private static readonly DifficultyRules NormalRules = new DifficultyRules(DifficultyMode.Normal, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 0, true);
        private static readonly DifficultyRules HardRules = new DifficultyRules(DifficultyMode.Hard, 1.35f, 1.10f, 1.10f, 0.90f, 0.85f, 0.50f, 0, true);
        private static readonly DifficultyRules ImpossibleRules = new DifficultyRules(DifficultyMode.Impossible, 1.70f, 1.20f, 1.20f, 0.80f, 0.75f, 1.00f, 1, false);

        /// <summary>Rules for a mode (unknown values fall back to Normal).</summary>
        public static DifficultyRules Rules(DifficultyMode mode)
        {
            switch (mode)
            {
                case DifficultyMode.Easy: return EasyRules;
                case DifficultyMode.Hard: return HardRules;
                case DifficultyMode.Impossible: return ImpossibleRules;
                default: return NormalRules;
            }
        }

        public static bool IsDefined(int value) => value >= (int)DifficultyMode.Easy && value <= (int)DifficultyMode.Impossible;

        /// <summary>Saved integer to a mode; anything unknown becomes <paramref name="fallback"/>.</summary>
        public static DifficultyMode FromInt(int value, DifficultyMode fallback = Default) =>
            IsDefined(value) ? (DifficultyMode)value : fallback;

        public static bool IsDefined(DifficultyMode mode) => IsDefined((int)mode);

        public static string DisplayName(DifficultyMode mode)
        {
            switch (mode)
            {
                case DifficultyMode.Easy: return "Easy";
                case DifficultyMode.Hard: return "Hard";
                case DifficultyMode.Impossible: return "Impossible";
                default: return "Normal";
            }
        }

        /// <summary>One-line flavour description for the picker.</summary>
        public static string Description(DifficultyMode mode)
        {
            switch (mode)
            {
                case DifficultyMode.Easy: return "A relaxed march: weaker foes, a fuller purse and extra lives.";
                case DifficultyMode.Hard: return "Tougher, faster foes, a thinner purse and half the lives.";
                case DifficultyMode.Impossible: return "One leak and it's over. No early-call gold. Good luck.";
                default: return "The kingdom as intended. A fair, steady challenge.";
            }
        }

        /// <summary>
        /// Modifiers as short lines for a picker card, e.g. "+35% enemy HP", "10 lives".
        /// <paramref name="baseLives"/> is the level's authored lives (0 = show the lives multiplier instead).
        /// </summary>
        public static string[] ModifierLines(DifficultyMode mode, int baseLives = 0)
        {
            DifficultyRules r = Rules(mode);
            var lines = new System.Collections.Generic.List<string>(6);
            if (r.IsIdentity)
            {
                lines.Add("Standard balance");
                lines.Add(LivesLine(r, baseLives));
                return lines.ToArray();
            }
            lines.Add(Percent(r.EnemyHealthMultiplier) + " enemy HP");
            if (r.EnemySpeedMultiplier != 1f) lines.Add(Percent(r.EnemySpeedMultiplier) + " enemy speed");
            lines.Add(LivesLine(r, baseLives));
            if (r.StartingGoldMultiplier != 1f) lines.Add(Percent(r.StartingGoldMultiplier) + " starting gold");
            if (!r.EarlyCallBonusEnabled) lines.Add("No early-call bonus");
            return lines.ToArray();
        }

        /// <summary>
        /// One compact line, e.g. "Enemies +35% HP · 10 lives" (HUD, results).
        /// <paramref name="baseLives"/> is the level's authored lives (0 = show the lives multiplier instead).
        /// </summary>
        public static string ModifierSummary(DifficultyMode mode, int baseLives = 0)
        {
            DifficultyRules r = Rules(mode);
            string lives = LivesLine(r, baseLives);
            if (r.IsIdentity) return "Standard balance · " + lives;
            string text = "Enemies " + Percent(r.EnemyHealthMultiplier) + " HP · " + lives;
            if (!r.EarlyCallBonusEnabled) text += " · no early-call bonus";
            return text;
        }

        private static string LivesLine(DifficultyRules r, int baseLives)
        {
            if (baseLives > 0 || r.FixedLives > 0) return LivesText(r.ApplyLives(Math.Max(1, baseLives)));
            return r.LivesMultiplier == 1f ? "Standard lives" : Percent(r.LivesMultiplier) + " lives";
        }

        /// <summary>"+35%", "-30%", "±0%" style change from a multiplier.</summary>
        public static string Percent(float multiplier)
        {
            int pct = (int)Math.Round((EconomyRules.Exact(multiplier) - 1.0) * 100.0, MidpointRounding.AwayFromZero);
            if (pct == 0) return "±0%";
            return (pct > 0 ? "+" : "-") + Math.Abs(pct) + "%";
        }

        private static string LivesText(int lives) => lives == 1 ? "1 life" : lives + " lives";

        // ---------------------------------------------------------------- unlocks

        /// <summary>Impossible on a map needs <see cref="ImpossibleUnlockStars"/> stars on Hard on that map.</summary>
        public static bool IsImpossibleUnlocked(int hardStarsOnMap) => hardStarsOnMap >= ImpossibleUnlockStars;

        /// <summary>
        /// Can <paramref name="mode"/> be played on a map? Easy/Normal/Hard whenever the
        /// level is unlocked; Impossible additionally needs 3 stars on Hard on that map.
        /// </summary>
        public static bool IsUnlocked(DifficultyMode mode, bool levelUnlocked, int hardStarsOnMap)
        {
            if (!levelUnlocked || !IsDefined(mode)) return false;
            return mode != DifficultyMode.Impossible || IsImpossibleUnlocked(hardStarsOnMap);
        }

        /// <summary>The mode to actually start: Impossible falls back to Hard while it is locked on this map.</summary>
        public static DifficultyMode ClampToUnlocked(DifficultyMode mode, int hardStarsOnMap)
        {
            if (!IsDefined(mode)) return Default;
            if (mode == DifficultyMode.Impossible && !IsImpossibleUnlocked(hardStarsOnMap)) return DifficultyMode.Hard;
            return mode;
        }

        /// <summary>
        /// Lock hint for Impossible: "3 stars on Hard to unlock". Spelled out rather than
        /// "3★" because the built-in legacy UI font may not carry the star glyph.
        /// </summary>
        public static string ImpossibleUnlockHint =>
            ImpossibleUnlockStars + " stars on " + DisplayName(ImpossibleUnlockMode) + " to unlock";
    }
}
