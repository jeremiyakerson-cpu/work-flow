using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TowerDefense.Content;
using TowerDefense.Levels;

namespace TowerDefense.ContentTests
{
    /// <summary>Each rule fires on a deliberately broken copy of a valid map.</summary>
    public class ValidatorTests
    {
        private static LayoutRules Rules() => new LayoutRules { KnownEnemyIds = ContentIds.Enemies, KnownBossIds = ContentIds.Bosses };

        private static List<string> Codes(LevelLayout l) =>
            LayoutValidator.Validate(l, Rules()).Select(i => i.Code).ToList();

        private static LevelLayout Valid() => CampaignLayouts.Meadow();

        [Test]
        public void ValidBaseline_HasNoIssues()
        {
            Assert.That(Codes(Valid()), Is.Empty);
        }

        [Test]
        public void SlotOnPath_IsAnError()
        {
            var l = Valid();
            l.BuildSlots[0] = new LayoutPoint(3f, 13.5f); // 0.5 from the y=13 road
            var issues = LayoutValidator.Validate(l, Rules());
            var issue = issues.Single(i => i.Code == "slot.on-path");
            Assert.That(issue.IsError);
            StringAssert.Contains("Slot 0", issue.Message);
            StringAssert.Contains("0.50", issue.Message);
        }

        [Test]
        public void SlotJustOutsideClearance_IsAccepted()
        {
            var l = Valid();
            l.BuildSlots[0] = new LayoutPoint(3f, 11.65f); // 1.35 from the road
            Assert.That(Codes(l), Does.Not.Contain("slot.on-path"));
        }

        [Test]
        public void OverlappingSlots_AreAnError()
        {
            var l = Valid();
            l.BuildSlots[1] = new LayoutPoint(l.BuildSlots[0].X + 0.5f, l.BuildSlots[0].Y - 0.5f);
            Assert.That(Codes(l), Does.Contain("slot.overlap"));
        }

        [Test]
        public void SlotOutsideMargin_IsAnError()
        {
            var l = Valid();
            l.BuildSlots[0] = new LayoutPoint(0.5f, 11f);
            Assert.That(Codes(l), Does.Contain("slot.out-of-bounds"));
        }

        [Test]
        public void SlotFarFromAnyPath_IsAWarning()
        {
            var l = Valid();
            l.BuildSlots[0] = new LayoutPoint(2f, 2f);
            var issue = LayoutValidator.Validate(l, Rules()).Single(i => i.Code == "slot.far-from-path");
            Assert.That(issue.Severity, Is.EqualTo(IssueSeverity.Warning));
        }

        [Test]
        public void SlotCount_OutsideRange_IsAnError()
        {
            var l = Valid();
            l.BuildSlots.RemoveRange(0, 5);
            Assert.That(Codes(l), Does.Contain("slot.count"));
        }

        [Test]
        public void HeroOffPath_IsAnError()
        {
            var l = Valid();
            l.HeroStart = new LayoutPoint(12f, 7f);
            Assert.That(Codes(l), Does.Contain("hero.off-path"));
        }

        [Test]
        public void PathStartingMidScreen_IsAnError()
        {
            var l = Valid();
            l.Paths[0][0] = new LayoutPoint(3f, 13f);
            Assert.That(Codes(l), Does.Contain("path.spawn-not-at-edge"));
        }

        [Test]
        public void PathEndingMidScreen_IsAnError()
        {
            var l = Valid();
            var p = l.Paths[0];
            p[p.Length - 1] = new LayoutPoint(30f, 7f);
            Assert.That(Codes(l), Does.Contain("path.exit-not-at-edge"));
        }

        [Test]
        public void PathLeavingTheScreenMidRoute_IsAnError()
        {
            var l = Valid();
            l.Paths[0][2] = new LayoutPoint(7f, -2f);
            Assert.That(Codes(l), Does.Contain("path.interior-outside"));
        }

        [Test]
        public void SpawnFarOffScreen_IsAnError()
        {
            var l = Valid();
            l.Paths[0][0] = new LayoutPoint(-6f, 13f);
            Assert.That(Codes(l), Does.Contain("path.far-outside"));
        }

        [Test]
        public void DegenerateSegment_IsAnError()
        {
            var l = Valid();
            var p = l.Paths[0].ToList();
            p.Insert(1, p[1]);
            l.Paths[0] = p.ToArray();
            Assert.That(Codes(l), Does.Contain("path.degenerate-segment"));
        }

        [Test]
        public void NoPaths_IsAnError()
        {
            var l = Valid();
            l.Paths.Clear();
            Assert.That(Codes(l), Does.Contain("path.none"));
        }

        [Test]
        public void BadEconomy_IsAnError()
        {
            var l = Valid();
            l.StartingGold = 0;
            l.StartingLives = -1;
            l.DifficultyMultiplier = 0f;
            l.WavesToWin = -3;
            var codes = Codes(l);
            Assert.That(codes, Does.Contain("rules.gold"));
            Assert.That(codes, Does.Contain("rules.lives"));
            Assert.That(codes, Does.Contain("rules.difficulty"));
            Assert.That(codes, Does.Contain("rules.waves"));
        }

        [Test]
        public void EmptyOrUnknownPools_AreErrors()
        {
            var l = Valid();
            l.EnemyPool.Clear();
            Assert.That(Codes(l), Does.Contain("pool.enemy-empty"));

            l = Valid();
            l.EnemyPool.Add("dragon_typo");
            l.BossPool.Add(ContentIds.Grunt);
            var codes = Codes(l);
            Assert.That(codes, Does.Contain("pool.enemy-unknown"));
            Assert.That(codes, Does.Contain("pool.boss-unknown"));

            l = Valid();
            l.EnemyPool.Add(ContentIds.Grunt);
            Assert.That(Codes(l), Does.Contain("pool.enemy-duplicate"));
        }

        [Test]
        public void UnreachableBossInterval_IsAWarning()
        {
            var l = Valid();
            l.BossEveryNWaves = 12; // 10-wave level
            Assert.That(Codes(l), Does.Contain("pool.boss-unreachable"));
        }

        [Test]
        public void DuplicateIds_AcrossLevels_AreAnError()
        {
            var issues = LayoutValidator.ValidateAll(new[] { Valid(), Valid() }, Rules());
            Assert.That(issues.Select(i => i.Code), Does.Contain("id.duplicate"));
            Assert.That(LayoutValidator.HasErrors(issues), Is.True);
        }

        [Test]
        public void IssueToString_IsReadable()
        {
            var i = new LevelIssue(IssueSeverity.Error, "meadow", "slot.on-path", "Slot 3 ...");
            Assert.That(i.ToString(), Is.EqualTo("[Error] meadow: slot.on-path - Slot 3 ..."));
        }
    }
}
