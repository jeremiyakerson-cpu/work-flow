using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense.Content;
using TowerDefense.Levels;

namespace TowerDefense.ContentTests
{
    public class ProgressionTests
    {
        private Dictionary<string, int> stars;
        private CampaignProgression progression;

        [SetUp]
        public void SetUp()
        {
            stars = new Dictionary<string, int>();
            progression = new CampaignProgression(CampaignLayouts.CampaignOrder,
                id => stars.TryGetValue(id, out int s) ? s : 0);
        }

        [Test]
        public void FreshSave_OnlyFirstLevelUnlocked()
        {
            Assert.That(progression.IsUnlocked(ContentIds.Meadow), Is.True);
            Assert.That(progression.IsUnlocked(ContentIds.Crossroads), Is.False);
            Assert.That(progression.UnlockedCount(), Is.EqualTo(1));
            Assert.That(progression.CurrentLevel(), Is.EqualTo(ContentIds.Meadow));
            Assert.That(progression.IsEndlessUnlocked(ContentIds.Meadow), Is.False);
        }

        [Test]
        public void OneStar_UnlocksNextLevelAndEndless()
        {
            stars[ContentIds.Meadow] = 1;
            Assert.That(progression.IsUnlocked(ContentIds.Crossroads), Is.True);
            Assert.That(progression.IsUnlocked(ContentIds.Frostfang), Is.False);
            Assert.That(progression.IsEndlessUnlocked(ContentIds.Meadow), Is.True);
            Assert.That(progression.IsUnlocked(ContentIds.EndlessIdFor(ContentIds.Meadow)), Is.True);
            Assert.That(progression.IsUnlocked(ContentIds.EndlessIdFor(ContentIds.Crossroads)), Is.False);
            Assert.That(progression.CurrentLevel(), Is.EqualTo(ContentIds.Crossroads));
        }

        [Test]
        public void StarsOnALockedLevel_DoNotSkipTheChain()
        {
            // A stale/hand-edited save: stars on level 3 but not on level 1.
            stars[ContentIds.Frostfang] = 3;
            Assert.That(progression.IsUnlocked(ContentIds.Marsh), Is.False);
            Assert.That(progression.IsEndlessUnlocked(ContentIds.Frostfang), Is.False);
        }

        [Test]
        public void StarsAreClampedAndTotalled()
        {
            stars[ContentIds.Meadow] = 7;
            stars[ContentIds.Crossroads] = -2;
            Assert.That(progression.Stars(ContentIds.Meadow), Is.EqualTo(3));
            Assert.That(progression.Stars(ContentIds.Crossroads), Is.EqualTo(0));
            Assert.That(progression.TotalStars(), Is.EqualTo(3));
            Assert.That(progression.MaxStars, Is.EqualTo(CampaignLayouts.CampaignOrder.Length * 3));
        }

        [Test]
        public void FullCampaign_IsComplete()
        {
            foreach (var id in CampaignLayouts.CampaignOrder) stars[id] = 2;
            Assert.That(progression.IsCampaignComplete(), Is.True);
            Assert.That(progression.UnlockedCount(), Is.EqualTo(CampaignLayouts.CampaignOrder.Length));
            Assert.That(progression.CurrentLevel(), Is.EqualTo(ContentIds.Citadel));
            Assert.That(progression.NextLevel(ContentIds.Citadel), Is.Null);
            Assert.That(progression.NextLevel(ContentIds.Meadow), Is.EqualTo(ContentIds.Crossroads));
        }

        [Test]
        public void UnknownIds_AreLocked()
        {
            Assert.That(progression.IsUnlocked("nope"), Is.False);
            Assert.That(progression.IsUnlocked(null), Is.False);
            Assert.That(progression.IndexOf("nope"), Is.EqualTo(-1));
        }

        [Test]
        public void CustomThresholds_AreRespected()
        {
            var strict = new CampaignProgression(CampaignLayouts.CampaignOrder,
                id => stars.TryGetValue(id, out int s) ? s : 0, requiredStarsToAdvance: 2, requiredStarsForEndless: 3);
            stars[ContentIds.Meadow] = 1;
            Assert.That(strict.IsUnlocked(ContentIds.Crossroads), Is.False);
            stars[ContentIds.Meadow] = 2;
            Assert.That(strict.IsUnlocked(ContentIds.Crossroads), Is.True);
            Assert.That(strict.IsEndlessUnlocked(ContentIds.Meadow), Is.False);
            stars[ContentIds.Meadow] = 3;
            Assert.That(strict.IsEndlessUnlocked(ContentIds.Meadow), Is.True);
        }

        [Test]
        public void NullStarLookup_TreatsEverythingAsUnplayed()
        {
            var p = new CampaignProgression(CampaignLayouts.CampaignOrder, null);
            Assert.That(p.UnlockedCount(), Is.EqualTo(1));
        }
    }
}
