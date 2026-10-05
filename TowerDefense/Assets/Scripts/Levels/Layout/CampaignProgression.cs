using System;
using System.Collections.Generic;
using TowerDefense.Content;
using TowerDefense.Core;

namespace TowerDefense.Levels
{
    /// <summary>
    /// Campaign unlock rules, independent of how stars are saved. Give it the
    /// level order and a star lookup (levelId -> 0..3) from the save system.
    ///  - The first level is always unlocked.
    ///  - Level N+1 unlocks once level N has at least RequiredStarsToAdvance stars (default 1).
    ///  - A level's endless variant ("id_endless") unlocks once that campaign level
    ///    has at least RequiredStarsForEndless stars (default 1, i.e. beaten once).
    ///  - Stars count on any difficulty for the two rules above.
    ///  - Difficulties: Easy/Normal/Hard are open on every unlocked level; Impossible
    ///    needs 3 stars on Hard on that map (see <see cref="IsDifficultyUnlocked"/>).
    /// </summary>
    public sealed class CampaignProgression
    {
        public const int MaxStarsPerLevel = 3;

        private readonly string[] order;
        private readonly Func<string, int> starLookup;
        private readonly Func<string, DifficultyMode, int> modeStarLookup;

        public int RequiredStarsToAdvance { get; }
        public int RequiredStarsForEndless { get; }

        /// <param name="starLookup">levelId -> best stars on any difficulty (unlocks the next level and endless).</param>
        public CampaignProgression(IReadOnlyList<string> levelOrder, Func<string, int> starLookup,
                                   int requiredStarsToAdvance = 1, int requiredStarsForEndless = 1)
            : this(levelOrder, starLookup, null, requiredStarsToAdvance, requiredStarsForEndless)
        {
        }

        /// <param name="starLookup">levelId -> best stars on any difficulty (unlocks the next level and endless).</param>
        /// <param name="modeStarLookup">
        /// (levelId, difficulty) -> stars on that difficulty (unlocks Impossible). Null = every
        /// result counts as Normal (a save from before difficulties).
        /// </param>
        public CampaignProgression(IReadOnlyList<string> levelOrder, Func<string, int> starLookup,
                                   Func<string, DifficultyMode, int> modeStarLookup,
                                   int requiredStarsToAdvance = 1, int requiredStarsForEndless = 1)
        {
            if (levelOrder == null) throw new ArgumentNullException(nameof(levelOrder));
            order = new string[levelOrder.Count];
            for (int i = 0; i < order.Length; i++) order[i] = levelOrder[i];
            this.starLookup = starLookup ?? (_ => 0);
            this.modeStarLookup = modeStarLookup;
            RequiredStarsToAdvance = Math.Max(0, requiredStarsToAdvance);
            RequiredStarsForEndless = Math.Max(0, requiredStarsForEndless);
        }

        public IReadOnlyList<string> Order => order;
        public int Count => order.Length;

        /// <summary>Stars for a campaign level, clamped to 0..3 (unknown ids give 0).</summary>
        public int Stars(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return 0;
            int s = starLookup(levelId);
            return s < 0 ? 0 : (s > MaxStarsPerLevel ? MaxStarsPerLevel : s);
        }

        /// <summary>
        /// Stars on one difficulty, clamped to 0..3. Endless ids read their base campaign map.
        /// Without a per-difficulty lookup every result counts as Normal.
        /// </summary>
        public int Stars(string levelId, DifficultyMode mode)
        {
            string id = ContentIds.BaseLevelId(levelId);
            if (string.IsNullOrEmpty(id)) return 0;
            int s = modeStarLookup != null ? modeStarLookup(id, mode) : (mode == DifficultyMode.Normal ? starLookup(id) : 0);
            return s < 0 ? 0 : (s > MaxStarsPerLevel ? MaxStarsPerLevel : s);
        }

        /// <summary>
        /// Can this level (campaign or endless id) be started on <paramref name="mode"/>?
        /// Easy/Normal/Hard whenever the level is unlocked; Impossible also needs
        /// 3 stars on Hard on the map (the base campaign map for endless variants).
        /// </summary>
        public bool IsDifficultyUnlocked(string levelId, DifficultyMode mode) =>
            Difficulty.IsUnlocked(mode, IsUnlocked(levelId), Stars(levelId, Difficulty.ImpossibleUnlockMode));

        /// <summary>The mode to start with: <paramref name="mode"/>, or Hard while Impossible is locked on this map.</summary>
        public DifficultyMode ClampDifficulty(string levelId, DifficultyMode mode) =>
            Difficulty.ClampToUnlocked(mode, Stars(levelId, Difficulty.ImpossibleUnlockMode));

        /// <summary>Position in the campaign, -1 if not a campaign level. Endless ids map to their base level.</summary>
        public int IndexOf(string levelId)
        {
            string id = ContentIds.BaseLevelId(levelId);
            return Array.IndexOf(order, id);
        }

        /// <summary>Is this campaign level (or endless variant id) playable?</summary>
        public bool IsUnlocked(string levelId)
        {
            if (ContentIds.IsEndlessId(levelId)) return IsEndlessUnlocked(ContentIds.BaseLevelId(levelId));
            int i = IndexOf(levelId);
            if (i < 0) return false;
            if (i == 0) return true;
            return Stars(order[i - 1]) >= RequiredStarsToAdvance && IsUnlocked(order[i - 1]);
        }

        /// <summary>Is the endless variant of this campaign level playable?</summary>
        public bool IsEndlessUnlocked(string campaignLevelId)
        {
            string id = ContentIds.BaseLevelId(campaignLevelId);
            return IndexOf(id) >= 0 && IsUnlocked(id) && Stars(id) >= Math.Max(1, RequiredStarsForEndless);
        }

        public bool IsCompleted(string levelId) => Stars(ContentIds.BaseLevelId(levelId)) > 0;

        /// <summary>Next campaign level id after this one, or null at the end.</summary>
        public string NextLevel(string levelId)
        {
            int i = IndexOf(levelId);
            return i >= 0 && i + 1 < order.Length ? order[i + 1] : null;
        }

        /// <summary>The level a "Continue" button should open: first unlocked level without stars, else the last one.</summary>
        public string CurrentLevel()
        {
            for (int i = 0; i < order.Length; i++)
                if (IsUnlocked(order[i]) && Stars(order[i]) == 0) return order[i];
            return order.Length > 0 ? order[order.Length - 1] : null;
        }

        public int UnlockedCount()
        {
            int n = 0;
            for (int i = 0; i < order.Length; i++)
                if (IsUnlocked(order[i])) n++;
            return n;
        }

        public int TotalStars()
        {
            int total = 0;
            for (int i = 0; i < order.Length; i++) total += Stars(order[i]);
            return total;
        }

        public int MaxStars => order.Length * MaxStarsPerLevel;

        /// <summary>Every campaign level has at least one star.</summary>
        public bool IsCampaignComplete()
        {
            if (order.Length == 0) return false;
            for (int i = 0; i < order.Length; i++)
                if (Stars(order[i]) <= 0) return false;
            return true;
        }
    }
}
