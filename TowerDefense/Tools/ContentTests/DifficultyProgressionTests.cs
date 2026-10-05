using System;
using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Content;
using TowerDefense.Core;
using TowerDefense.Levels;

namespace TowerDefense.ContentTests
{
    /// <summary>Campaign unlocks with per-difficulty stars, and the Impossible unlock per map.</summary>
    public class DifficultyProgressionTests
    {
        // levelId -> stars per difficulty, like SaveService.GetBestStars(id, mode).
        private Dictionary<string, Dictionary<DifficultyMode, int>> stars;
        private CampaignProgression progression;

        private int Best(string id)
        {
            if (!stars.TryGetValue(id, out var byMode)) return 0;
            int best = 0;
            foreach (var kv in byMode) best = Math.Max(best, kv.Value);
            return best;
        }

        private int On(string id, DifficultyMode mode) =>
            stars.TryGetValue(id, out var byMode) && byMode.TryGetValue(mode, out int s) ? s : 0;

        private void Set(string id, DifficultyMode mode, int s)
        {
            if (!stars.TryGetValue(id, out var byMode)) stars[id] = byMode = new Dictionary<DifficultyMode, int>();
            byMode[mode] = s;
        }

        [SetUp]
        public void SetUp()
        {
            stars = new Dictionary<string, Dictionary<DifficultyMode, int>>();
            progression = new CampaignProgression(CampaignLayouts.CampaignOrder, Best, On);
        }

        private static readonly DifficultyMode[] Regular = { DifficultyMode.Easy, DifficultyMode.Normal, DifficultyMode.Hard };

        [Test]
        public void NextLevel_UnlocksWithAStarOnAnyDifficulty([Values(DifficultyMode.Easy, DifficultyMode.Normal,
                                                                      DifficultyMode.Hard, DifficultyMode.Impossible)] DifficultyMode mode)
        {
            Assert.That(progression.IsUnlocked(ContentIds.Crossroads), Is.False);
            Set(ContentIds.Meadow, mode, 1);
            Assert.That(progression.IsUnlocked(ContentIds.Crossroads), Is.True);
            Assert.That(progression.IsEndlessUnlocked(ContentIds.Meadow), Is.True);
            Assert.That(progression.TotalStars(), Is.EqualTo(1));
        }

        [Test]
        public void RegularModes_FollowTheLevelLock()
        {
            foreach (DifficultyMode mode in Regular)
            {
                Assert.That(progression.IsDifficultyUnlocked(ContentIds.Meadow, mode), Is.True, mode.ToString());
                Assert.That(progression.IsDifficultyUnlocked(ContentIds.Crossroads, mode), Is.False, mode.ToString());
                Assert.That(progression.IsDifficultyUnlocked(ContentIds.EndlessIdFor(ContentIds.Meadow), mode), Is.False,
                            "endless still needs a campaign star");
            }
            Set(ContentIds.Meadow, DifficultyMode.Easy, 1);
            foreach (DifficultyMode mode in Regular)
            {
                Assert.That(progression.IsDifficultyUnlocked(ContentIds.Crossroads, mode), Is.True, mode.ToString());
                Assert.That(progression.IsDifficultyUnlocked(ContentIds.EndlessIdFor(ContentIds.Meadow), mode), Is.True, mode.ToString());
            }
        }

        [Test]
        public void Impossible_NeedsThreeStarsOnHard_OnThatMap()
        {
            string meadow = ContentIds.Meadow;
            Assert.That(progression.IsDifficultyUnlocked(meadow, DifficultyMode.Impossible), Is.False);

            Set(meadow, DifficultyMode.Normal, 3);
            Set(meadow, DifficultyMode.Easy, 3);
            Assert.That(progression.IsDifficultyUnlocked(meadow, DifficultyMode.Impossible), Is.False, "only Hard counts");

            Set(meadow, DifficultyMode.Hard, 2);
            Assert.That(progression.IsDifficultyUnlocked(meadow, DifficultyMode.Impossible), Is.False);

            Set(meadow, DifficultyMode.Hard, 3);
            Assert.That(progression.IsDifficultyUnlocked(meadow, DifficultyMode.Impossible), Is.True);
            Assert.That(progression.IsDifficultyUnlocked(ContentIds.Crossroads, DifficultyMode.Impossible), Is.False,
                        "the unlock is per map");
        }

        [Test]
        public void Endless_ImpossibleChecksTheBaseCampaignMap()
        {
            string endless = ContentIds.EndlessIdFor(ContentIds.Meadow);
            Set(ContentIds.Meadow, DifficultyMode.Hard, 2);
            Set(endless, DifficultyMode.Hard, 3); // stars on the endless id itself never count
            Assert.That(progression.IsDifficultyUnlocked(endless, DifficultyMode.Impossible), Is.False);

            Set(ContentIds.Meadow, DifficultyMode.Hard, 3);
            Assert.That(progression.IsDifficultyUnlocked(endless, DifficultyMode.Impossible), Is.True);
            Assert.That(progression.Stars(endless, DifficultyMode.Hard), Is.EqualTo(3));
        }

        [Test]
        public void HardStarsOnALockedLevel_DoNotUnlockImpossible()
        {
            // Stale/hand-edited save: 3 stars on Hard on level 3, nothing before it.
            Set(ContentIds.Frostfang, DifficultyMode.Hard, 3);
            Assert.That(progression.IsDifficultyUnlocked(ContentIds.Frostfang, DifficultyMode.Impossible), Is.False);
            Assert.That(progression.IsDifficultyUnlocked(ContentIds.Frostfang, DifficultyMode.Hard), Is.False);
        }

        [Test]
        public void ClampDifficulty_DropsImpossibleToHardWhileLocked()
        {
            Set(ContentIds.Meadow, DifficultyMode.Hard, 3);
            Assert.That(progression.ClampDifficulty(ContentIds.Meadow, DifficultyMode.Impossible), Is.EqualTo(DifficultyMode.Impossible));
            Assert.That(progression.ClampDifficulty(ContentIds.Crossroads, DifficultyMode.Impossible), Is.EqualTo(DifficultyMode.Hard),
                        "next level: keep the mode only if it is open there");
            Assert.That(progression.ClampDifficulty(ContentIds.Crossroads, DifficultyMode.Easy), Is.EqualTo(DifficultyMode.Easy));
        }

        [Test]
        public void PerModeStars_AreClamped()
        {
            Set(ContentIds.Meadow, DifficultyMode.Hard, 9);
            Set(ContentIds.Meadow, DifficultyMode.Easy, -4);
            Assert.That(progression.Stars(ContentIds.Meadow, DifficultyMode.Hard), Is.EqualTo(3));
            Assert.That(progression.Stars(ContentIds.Meadow, DifficultyMode.Easy), Is.EqualTo(0));
            Assert.That(progression.Stars(null, DifficultyMode.Hard), Is.EqualTo(0));
        }

        [Test]
        public void UnknownLevelsAndModes_AreLocked()
        {
            Assert.That(progression.IsDifficultyUnlocked("nope", DifficultyMode.Easy), Is.False);
            Assert.That(progression.IsDifficultyUnlocked(null, DifficultyMode.Normal), Is.False);
            Assert.That(progression.IsDifficultyUnlocked(ContentIds.Meadow, (DifficultyMode)9), Is.False);
        }

        [Test]
        public void WithoutAPerModeLookup_EverythingCountsAsNormal()
        {
            var legacy = new CampaignProgression(CampaignLayouts.CampaignOrder, id => id == ContentIds.Meadow ? 3 : 0);
            Assert.That(legacy.Stars(ContentIds.Meadow, DifficultyMode.Normal), Is.EqualTo(3));
            Assert.That(legacy.Stars(ContentIds.Meadow, DifficultyMode.Hard), Is.EqualTo(0));
            Assert.That(legacy.IsDifficultyUnlocked(ContentIds.Meadow, DifficultyMode.Hard), Is.True);
            Assert.That(legacy.IsDifficultyUnlocked(ContentIds.Meadow, DifficultyMode.Impossible), Is.False);
            Assert.That(legacy.IsUnlocked(ContentIds.Crossroads), Is.True);
        }
    }
}
