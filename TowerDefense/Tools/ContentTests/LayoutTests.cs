using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TowerDefense.Content;
using TowerDefense.Levels;

namespace TowerDefense.ContentTests
{
    /// <summary>Runs the real validator over every authored map (campaign and endless).</summary>
    public class LayoutTests
    {
        private static LayoutRules StrictRules() => new LayoutRules
        {
            KnownEnemyIds = ContentIds.Enemies,
            KnownBossIds = ContentIds.Bosses,
        };

        private static IEnumerable<TestCaseData> AllLayouts()
        {
            foreach (var l in CampaignLayouts.CreateAll())
                yield return new TestCaseData(l).SetName("Layout_" + l.Id);
        }

        [TestCaseSource(nameof(AllLayouts))]
        public void EveryLayout_PassesValidationWithoutErrorsOrWarnings(LevelLayout level)
        {
            var issues = LayoutValidator.Validate(level, StrictRules());
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void AllLayouts_HaveUniqueIds()
        {
            var issues = LayoutValidator.ValidateAll(CampaignLayouts.CreateAll(), StrictRules());
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void Campaign_HasFiveOrMoreLevelsInDeclaredOrder()
        {
            var campaign = CampaignLayouts.CreateCampaign();
            Assert.That(campaign.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(campaign.Select(l => l.Id), Is.EqualTo(CampaignLayouts.CampaignOrder));
            Assert.That(campaign.All(l => l.WavesToWin > 0), "Campaign levels must have a win condition.");
        }

        [Test]
        public void Campaign_IsLandscape32x18()
        {
            foreach (var l in CampaignLayouts.CreateAll())
            {
                Assert.That(l.Width, Is.EqualTo(32f), l.Id);
                Assert.That(l.Height, Is.EqualTo(18f), l.Id);
            }
        }

        [Test]
        public void Tutorial_HasExactlyOnePath()
        {
            Assert.That(CampaignLayouts.Meadow().Paths.Count, Is.EqualTo(1));
        }

        [Test]
        public void Crossroads_TwoPathsWithDistinctSpawnsThatMerge()
        {
            var l = CampaignLayouts.Crossroads();
            Assert.That(l.Paths.Count, Is.EqualTo(2));
            Assert.That(LevelGeometry.Distance(l.Paths[0][0], l.Paths[1][0]), Is.GreaterThan(4f), "spawns should differ");
            Assert.That(SharedPoints(l.Paths[0], l.Paths[1]), Is.GreaterThanOrEqualTo(2), "paths should merge before the exit");
        }

        [Test]
        public void Frostfang_TwoSeparateSpawnsAndRoutes()
        {
            var l = CampaignLayouts.Frostfang();
            Assert.That(l.Paths.Count, Is.EqualTo(2));
            Assert.That(LevelGeometry.Distance(l.Paths[0][0], l.Paths[1][0]), Is.GreaterThan(4f));
            Assert.That(SharedPoints(l.Paths[0], l.Paths[1]), Is.EqualTo(0), "routes are independent");
            // Independent routes: no point of one path lies on the other.
            foreach (var p in l.Paths[0])
                Assert.That(LevelGeometry.DistanceToPolyline(p, l.Paths[1]), Is.GreaterThan(2f));
        }

        [Test]
        public void Marsh_HasThreePathsFromThreeSpawns()
        {
            var l = CampaignLayouts.Marsh();
            Assert.That(l.Paths.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                    Assert.That(LevelGeometry.Distance(l.Paths[i][0], l.Paths[j][0]), Is.GreaterThan(4f));
        }

        [Test]
        public void Finale_IsTheLongestAndHardest()
        {
            var campaign = CampaignLayouts.CreateCampaign();
            var finale = campaign.Last();
            Assert.That(finale.Id, Is.EqualTo(ContentIds.Citadel));
            Assert.That(finale.WavesToWin, Is.EqualTo(campaign.Max(l => l.WavesToWin)));
            Assert.That(finale.DifficultyMultiplier, Is.EqualTo(campaign.Max(l => l.DifficultyMultiplier)));
            Assert.That(finale.BossPool, Is.EquivalentTo(ContentIds.Bosses));
        }

        [Test]
        public void Campaign_RampsUpWavesAndDifficulty()
        {
            var campaign = CampaignLayouts.CreateCampaign();
            for (int i = 1; i < campaign.Count; i++)
            {
                Assert.That(campaign[i].WavesToWin, Is.GreaterThanOrEqualTo(campaign[i - 1].WavesToWin), campaign[i].Id);
                Assert.That(campaign[i].DifficultyMultiplier, Is.GreaterThanOrEqualTo(campaign[i - 1].DifficultyMultiplier), campaign[i].Id);
                Assert.That(campaign[i].EnemyPool.Count, Is.GreaterThanOrEqualTo(campaign[i - 1].EnemyPool.Count), campaign[i].Id);
            }
        }

        [Test]
        public void EveryCampaignLevel_HasABossWaveWithinItsWaves()
        {
            foreach (var l in CampaignLayouts.CreateCampaign())
            {
                Assert.That(l.BossPool, Is.Not.Empty, l.Id);
                Assert.That(l.BossEveryNWaves, Is.InRange(1, l.WavesToWin), l.Id);
            }
        }

        [Test]
        public void EndlessVariants_MirrorCampaignGeometry()
        {
            var campaign = CampaignLayouts.CreateCampaign();
            var endless = CampaignLayouts.CreateEndless();
            Assert.That(endless.Count, Is.EqualTo(campaign.Count));
            for (int i = 0; i < campaign.Count; i++)
            {
                var c = campaign[i];
                var e = endless[i];
                Assert.That(e.Id, Is.EqualTo(ContentIds.EndlessIdFor(c.Id)));
                Assert.That(e.BaseLevelId, Is.EqualTo(c.Id));
                Assert.That(e.IsEndless, Is.True);
                Assert.That(e.WavesToWin, Is.EqualTo(0));
                Assert.That(e.DifficultyMultiplier, Is.GreaterThan(c.DifficultyMultiplier));
                Assert.That(e.BuildSlots, Is.EqualTo(c.BuildSlots));
                Assert.That(e.Paths.Count, Is.EqualTo(c.Paths.Count));
                Assert.That(e.EnemyPool, Is.EquivalentTo(ContentIds.Enemies));
                Assert.That(e.BossPool, Is.EquivalentTo(ContentIds.Bosses));
            }
        }

        [Test]
        public void MakeEndless_DoesNotMutateCampaign()
        {
            var c = CampaignLayouts.Meadow();
            int pool = c.EnemyPool.Count;
            var e = CampaignLayouts.MakeEndless(c);
            e.BuildSlots.Clear();
            e.Paths[0][0] = new LayoutPoint(99, 99);
            Assert.That(c.WavesToWin, Is.EqualTo(10));
            Assert.That(c.EnemyPool.Count, Is.EqualTo(pool));
            Assert.That(c.BuildSlots.Count, Is.EqualTo(12));
            Assert.That(c.Paths[0][0].X, Is.EqualTo(-1f));
        }

        [Test]
        public void ContentIds_EndlessIdRoundTrip()
        {
            Assert.That(ContentIds.EndlessIdFor("meadow"), Is.EqualTo("meadow_endless"));
            Assert.That(ContentIds.IsEndlessId("meadow_endless"), Is.True);
            Assert.That(ContentIds.IsEndlessId("meadow"), Is.False);
            Assert.That(ContentIds.BaseLevelId("meadow_endless"), Is.EqualTo("meadow"));
            Assert.That(ContentIds.BaseLevelId("meadow"), Is.EqualTo("meadow"));
        }

        [Test]
        public void ContentIds_AreUniqueAcrossCategories()
        {
            var all = ContentIds.Towers.Concat(ContentIds.Enemies).Concat(ContentIds.Minions).Concat(ContentIds.Bosses).ToList();
            Assert.That(all.Distinct().Count(), Is.EqualTo(all.Count));
            var enemyLike = ContentIds.Enemies.Concat(ContentIds.Minions).Concat(ContentIds.Bosses).ToList();
            Assert.That(enemyLike.All(id => id == id.ToLowerInvariant() && !id.Contains(' ')));
        }

        private static int SharedPoints(LayoutPoint[] a, LayoutPoint[] b)
        {
            int n = 0;
            foreach (var p in a)
                foreach (var q in b)
                    if (LevelGeometry.Distance(p, q) < 1e-4f) { n++; break; }
            return n;
        }
    }
}
