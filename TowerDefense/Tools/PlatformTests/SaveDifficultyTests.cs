using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TowerDefense.Core;
using TowerDefense.Persistence;

namespace TowerDefense.PlatformTests
{
    /// <summary>Schema v3: results per difficulty, last difficulty, per-difficulty endless bests.</summary>
    [TestFixture]
    public class SaveDifficultyTests
    {
        private string dir;
        private FakeClock clock;
        private SaveFileStore store;
        private SaveService service;

        [SetUp]
        public void SetUp()
        {
            dir = TempDir.Create();
            clock = new FakeClock();
            store = new SaveFileStore(dir, new SystemTextJsonSaveSerializer());
            service = new SaveService(store, clock.Get, unixNow: () => 1700000000);
            service.Load();
        }

        [TearDown]
        public void TearDown() => TempDir.Delete(dir);

        private SaveService Reloaded()
        {
            service.Flush();
            var fresh = new SaveService(new SaveFileStore(dir, new SystemTextJsonSaveSerializer()), clock.Get);
            fresh.Load();
            return fresh;
        }

        // The current v2 format, as written by the previous build.
        private const string V2Json = @"{
  ""version"": 2,
  ""levels"": [
    { ""levelId"": ""meadow"", ""bestStars"": 3, ""bestWave"": 10, ""completed"": true },
    { ""levelId"": ""crossroads"", ""bestStars"": 0, ""bestWave"": 6, ""completed"": false },
    { ""levelId"": ""meadow_endless"", ""bestStars"": 0, ""bestWave"": 27, ""completed"": false }
  ],
  ""endlessBestWave"": 27,
  ""settings"": { ""musicVolume"": 0.4, ""sfxVolume"": 0.9, ""hapticsEnabled"": false, ""lastGameSpeed"": 2 },
  ""stats"": { ""enemiesDefeated"": 900, ""gamesPlayed"": 12, ""victories"": 5 },
  ""tutorialsSeen"": [ ""first_level"" ],
  ""lastSavedUnixSeconds"": 1690000000
}";

        // ---------------------------------------------------------------- migration

        [Test]
        public void V2File_MigratesEveryRecordToNormal()
        {
            File.WriteAllText(store.MainPath, V2Json);
            SaveLoadResult r = store.Load();

            Assert.AreEqual(2, r.OriginalVersion);
            Assert.AreEqual(3, SaveData.CurrentVersion);
            Assert.AreEqual(SaveData.CurrentVersion, r.Data.version);

            LevelRecord meadow = r.Data.FindLevel("meadow");
            Assert.AreEqual(1, meadow.modes.Count);
            Assert.AreEqual((int)DifficultyMode.Normal, meadow.modes[0].mode);
            Assert.AreEqual(3, meadow.modes[0].bestStars);
            Assert.AreEqual(10, meadow.modes[0].bestWave);
            Assert.IsTrue(meadow.modes[0].completed);
            Assert.AreEqual(3, meadow.bestStars, "across-difficulty totals kept");

            LevelRecord cross = r.Data.FindLevel("crossroads");
            Assert.AreEqual(6, cross.FindMode(DifficultyMode.Normal).bestWave);
            Assert.IsFalse(cross.FindMode(DifficultyMode.Normal).completed);

            Assert.AreEqual(27, r.Data.FindLevel("meadow_endless").FindMode(DifficultyMode.Normal).bestWave);
            Assert.AreEqual(27, r.Data.endlessBestWave);
            Assert.AreEqual(1, r.Data.endlessBestWaves.Count);
            Assert.AreEqual((int)DifficultyMode.Normal, r.Data.endlessBestWaves[0].mode);
            Assert.AreEqual(27, r.Data.endlessBestWaves[0].bestWave);

            Assert.AreEqual((int)DifficultyMode.Normal, r.Data.settings.lastDifficulty);
            // Untouched data survives.
            Assert.AreEqual(0.4f, r.Data.settings.musicVolume, 1e-6f);
            Assert.AreEqual(2f, r.Data.settings.lastGameSpeed);
            Assert.AreEqual(900, r.Data.stats.enemiesDefeated);
            CollectionAssert.AreEqual(new[] { "first_level" }, r.Data.tutorialsSeen);
        }

        [Test]
        public void V2File_ThroughTheService_ReadsAsNormalAndIsRewrittenAsV3()
        {
            File.WriteAllText(store.MainPath, V2Json);
            var s = new SaveService(new SaveFileStore(dir, new SystemTextJsonSaveSerializer()), clock.Get);
            s.Load();

            Assert.AreEqual(3, s.GetBestStars("meadow"));
            Assert.AreEqual(3, s.GetBestStars("meadow", DifficultyMode.Normal));
            Assert.AreEqual(0, s.GetBestStars("meadow", DifficultyMode.Hard));
            Assert.AreEqual(0, s.GetBestStars("meadow", DifficultyMode.Easy));
            Assert.AreEqual(27, s.GetBestWave("meadow_endless", DifficultyMode.Normal));
            Assert.AreEqual(27, s.GetEndlessBestWave(DifficultyMode.Normal));
            Assert.AreEqual(0, s.GetEndlessBestWave(DifficultyMode.Hard));
            Assert.AreEqual(DifficultyMode.Normal, s.LastDifficulty);
            Assert.IsTrue(s.TryGetHardestCompleted("meadow", out DifficultyMode hardest));
            Assert.AreEqual(DifficultyMode.Normal, hardest);

            Assert.AreEqual(1, s.SaveCount, "migrated data is rewritten right away");
            SaveLoadResult again = new SaveFileStore(dir, new SystemTextJsonSaveSerializer()).Load();
            Assert.AreEqual(SaveData.CurrentVersion, again.OriginalVersion);
            Assert.AreEqual(3, again.Data.FindLevel("meadow").FindMode(DifficultyMode.Normal).bestStars);
        }

        [Test]
        public void V1File_MigratesAllTheWayToPerDifficulty()
        {
            File.WriteAllText(store.MainPath, @"{ ""version"": 1,
  ""levels"": [ { ""levelId"": ""meadow"", ""bestStars"": 2, ""bestWave"": 10, ""completed"": true },
                { ""levelId"": ""endless"", ""bestWave"": 42 } ],
  ""settings"": { ""musicVolume"": 80, ""sfxVolume"": 50 } }");
            SaveLoadResult r = store.Load();
            Assert.AreEqual(2, r.Data.FindLevel("meadow").FindMode(DifficultyMode.Normal).bestStars);
            Assert.AreEqual(42, r.Data.endlessBestWave);
            Assert.AreEqual(42, r.Data.endlessBestWaves[0].bestWave);
            Assert.AreEqual((int)DifficultyMode.Normal, r.Data.endlessBestWaves[0].mode);
        }

        [Test]
        public void V3File_IsNotMigratedAgain_AndKeepsEveryMode()
        {
            service.RecordLevelResult("meadow", DifficultyMode.Hard, 2, 12);
            service.RecordLevelResult("meadow", DifficultyMode.Easy, 3, 12);
            SaveService fresh = Reloaded();
            Assert.AreEqual(SaveData.CurrentVersion, fresh.LastLoad.OriginalVersion);
            Assert.AreEqual(2, fresh.GetBestStars("meadow", DifficultyMode.Hard));
            Assert.AreEqual(3, fresh.GetBestStars("meadow", DifficultyMode.Easy));
            Assert.AreEqual(0, fresh.GetBestStars("meadow", DifficultyMode.Normal));
            Assert.AreEqual(2, fresh.Data.FindLevel("meadow").modes.Count, "no phantom Normal entry");
        }

        // ---------------------------------------------------------------- per-mode records

        [Test]
        public void Results_AreIsolatedPerDifficulty()
        {
            LevelResultOutcome hard = service.RecordLevelResult("meadow", DifficultyMode.Hard, 2, 15);
            Assert.AreEqual(DifficultyMode.Hard, hard.Mode);
            Assert.IsTrue(hard.NewBestStars);
            Assert.IsTrue(hard.FirstCompletion);
            Assert.IsTrue(hard.FirstCompletionAnyMode);

            LevelResultOutcome easy = service.RecordLevelResult("meadow", DifficultyMode.Easy, 1, 15);
            Assert.IsTrue(easy.NewBestStars, "best is per difficulty: 1 star beats Easy's 0");
            Assert.IsTrue(easy.FirstCompletion);
            Assert.IsFalse(easy.FirstCompletionAnyMode);
            Assert.AreEqual(0, easy.PreviousStars);

            Assert.AreEqual(2, service.GetBestStars("meadow", DifficultyMode.Hard));
            Assert.AreEqual(1, service.GetBestStars("meadow", DifficultyMode.Easy));
            Assert.AreEqual(0, service.GetBestStars("meadow", DifficultyMode.Normal));
            Assert.AreEqual(0, service.GetBestStars("meadow", DifficultyMode.Impossible));
            Assert.IsTrue(service.IsLevelCompleted("meadow", DifficultyMode.Hard));
            Assert.IsFalse(service.IsLevelCompleted("meadow", DifficultyMode.Normal));

            LevelResultOutcome worse = service.RecordLevelResult("meadow", DifficultyMode.Hard, 1, 20);
            Assert.IsFalse(worse.NewBestStars);
            Assert.IsTrue(worse.NewBestWave);
            Assert.AreEqual(2, worse.PreviousStars);
            Assert.AreEqual(2, service.GetBestStars("meadow", DifficultyMode.Hard));
            Assert.AreEqual(20, service.GetBestWave("meadow", DifficultyMode.Hard));
            Assert.AreEqual(15, service.GetBestWave("meadow", DifficultyMode.Easy));
        }

        [Test]
        public void DifficultylessReads_AreTheMaximumAcrossModes()
        {
            service.RecordLevelResult("meadow", DifficultyMode.Easy, 3, 10);
            service.RecordLevelResult("meadow", DifficultyMode.Impossible, 0, 14);
            service.RecordLevelResult("meadow", DifficultyMode.Hard, 1, 10);
            Assert.AreEqual(3, service.GetBestStars("meadow"));
            Assert.AreEqual(14, service.GetBestWave("meadow"));
            Assert.IsTrue(service.IsLevelCompleted("meadow"));
            Assert.AreEqual(3, service.TotalStars, "totals count each level once, at its best");

            Assert.IsTrue(service.TryGetHardestCompleted("meadow", out DifficultyMode hardest));
            Assert.AreEqual(DifficultyMode.Hard, hardest, "an Impossible loss doesn't count as cleared");
            Assert.IsFalse(service.TryGetHardestCompleted("crossroads", out _));
        }

        [Test]
        public void DifficultylessWrites_AreNormal()
        {
            service.RecordLevelResult("meadow", 2, 9);
            Assert.AreEqual(2, service.GetBestStars("meadow", DifficultyMode.Normal));
            Assert.AreEqual(0, service.GetBestStars("meadow", DifficultyMode.Hard));
            Assert.IsTrue(service.RecordEndlessResult(30));
            Assert.AreEqual(30, service.GetEndlessBestWave(DifficultyMode.Normal));
            Assert.AreEqual(30, service.EndlessBestWave);
        }

        [Test]
        public void UnknownDifficulty_IsRecordedAsNormal()
        {
            service.RecordLevelResult("meadow", (DifficultyMode)17, 2, 9);
            Assert.AreEqual(2, service.GetBestStars("meadow", DifficultyMode.Normal));
            Assert.AreEqual(1, service.Data.FindLevel("meadow").modes.Count);
        }

        [Test]
        public void ThreeStarsOnHard_ReportsImpossibleUnlockOnce()
        {
            Assert.IsFalse(service.RecordLevelResult("meadow", DifficultyMode.Hard, 2, 10).UnlockedImpossible);
            Assert.IsFalse(service.RecordLevelResult("meadow", DifficultyMode.Normal, 3, 10).UnlockedImpossible, "Normal doesn't count");
            Assert.IsTrue(service.RecordLevelResult("meadow", DifficultyMode.Hard, 3, 10).UnlockedImpossible);
            Assert.IsFalse(service.RecordLevelResult("meadow", DifficultyMode.Hard, 3, 10).UnlockedImpossible, "already unlocked");
        }

        [Test]
        public void Endless_BestWaveIsTrackedPerDifficulty()
        {
            service.RecordLevelResult("meadow_endless", DifficultyMode.Hard, 0, 18);
            Assert.IsTrue(service.RecordEndlessResult(18, DifficultyMode.Hard, countGame: false));
            Assert.IsTrue(service.RecordEndlessResult(25, DifficultyMode.Easy));
            Assert.IsFalse(service.RecordEndlessResult(12, DifficultyMode.Hard));
            Assert.IsTrue(service.RecordEndlessResult(12, DifficultyMode.Impossible), "first Impossible run is a best");

            Assert.AreEqual(18, service.GetEndlessBestWave(DifficultyMode.Hard));
            Assert.AreEqual(25, service.GetEndlessBestWave(DifficultyMode.Easy));
            Assert.AreEqual(12, service.GetEndlessBestWave(DifficultyMode.Impossible));
            Assert.AreEqual(0, service.GetEndlessBestWave(DifficultyMode.Normal));
            Assert.AreEqual(25, service.EndlessBestWave, "global best is across difficulties");
            Assert.AreEqual(18, service.GetBestWave("meadow_endless", DifficultyMode.Hard));
            Assert.AreEqual(0, service.GetBestWave("meadow_endless", DifficultyMode.Easy));

            SaveService fresh = Reloaded();
            Assert.AreEqual(18, fresh.GetEndlessBestWave(DifficultyMode.Hard));
            Assert.AreEqual(25, fresh.EndlessBestWave);
        }

        [Test]
        public void LastDifficulty_IsRememberedAcrossLaunches()
        {
            int changes = 0;
            service.Settings.LastDifficultyChanged += _ => changes++;
            service.LastDifficulty = DifficultyMode.Hard;
            service.LastDifficulty = DifficultyMode.Hard; // no-op
            service.Settings.SetLastDifficulty((DifficultyMode)99); // ignored
            Assert.AreEqual(1, changes);
            Assert.IsTrue(service.IsDirty);
            Assert.AreEqual(DifficultyMode.Hard, Reloaded().LastDifficulty);
        }

        [Test]
        public void ResetProgress_ClearsEveryDifficulty_ButKeepsTheLastPick()
        {
            service.RecordLevelResult("meadow", DifficultyMode.Hard, 3, 10);
            service.RecordEndlessResult(9, DifficultyMode.Hard);
            service.LastDifficulty = DifficultyMode.Easy;
            service.ResetProgress();
            Assert.AreEqual(0, service.GetBestStars("meadow", DifficultyMode.Hard));
            Assert.AreEqual(0, service.GetEndlessBestWave(DifficultyMode.Hard));
            Assert.AreEqual(0, service.EndlessBestWave);
            Assert.AreEqual(DifficultyMode.Easy, service.LastDifficulty, "a setting, not progress");
        }

        // ---------------------------------------------------------------- clamping / corruption

        [Test]
        public void Validator_ClampsMergesAndDropsBadModeEntries()
        {
            var d = new SaveData
            {
                version = SaveData.CurrentVersion,
                levels = new List<LevelRecord>
                {
                    new LevelRecord
                    {
                        levelId = "meadow", bestStars = 0, bestWave = 0,
                        modes = new List<ModeRecord>
                        {
                            new ModeRecord { mode = (int)DifficultyMode.Hard, bestStars = 9, bestWave = -3 },
                            null,
                            new ModeRecord { mode = 42, bestStars = 3, bestWave = 99 },  // unknown difficulty
                            new ModeRecord { mode = -1, bestStars = 3 },
                            new ModeRecord { mode = (int)DifficultyMode.Hard, bestStars = 1, bestWave = 30 }, // duplicate
                            new ModeRecord { mode = (int)DifficultyMode.Easy, bestStars = 0, bestWave = 999999999 },
                        },
                    },
                    // Duplicate level record: modes merge too.
                    new LevelRecord
                    {
                        levelId = " meadow ",
                        modes = new List<ModeRecord> { new ModeRecord { mode = (int)DifficultyMode.Impossible, bestStars = 2, bestWave = 5 } },
                    },
                    // Inconsistent v3 record: totals higher than any mode -> attributed to Normal.
                    new LevelRecord
                    {
                        levelId = "canyon", bestStars = 2, bestWave = 8, completed = true,
                        modes = new List<ModeRecord> { new ModeRecord { mode = (int)DifficultyMode.Easy, bestStars = 1, bestWave = 4 } },
                    },
                    // Null modes list (JsonUtility / hand edit).
                    new LevelRecord { levelId = "marsh", bestStars = 1, bestWave = 3, modes = null },
                },
                endlessBestWave = 7,
                endlessBestWaves = new List<ModeWaveRecord>
                {
                    new ModeWaveRecord { mode = (int)DifficultyMode.Hard, bestWave = 40 },
                    new ModeWaveRecord { mode = (int)DifficultyMode.Hard, bestWave = 12 },
                    new ModeWaveRecord { mode = 8, bestWave = 500 },
                    null,
                    new ModeWaveRecord { mode = (int)DifficultyMode.Easy, bestWave = -2 },
                },
                settings = new SettingsData { lastDifficulty = 12 },
            };

            SaveValidator.Sanitize(d);

            Assert.AreEqual(3, d.levels.Count);
            LevelRecord meadow = d.FindLevel("meadow");
            Assert.AreEqual(3, meadow.modes.Count, "Hard, Easy, Impossible; unknown modes dropped");
            ModeRecord hard = meadow.FindMode(DifficultyMode.Hard);
            Assert.AreEqual(3, hard.bestStars, "clamped and merged by max");
            Assert.AreEqual(30, hard.bestWave);
            Assert.IsTrue(hard.completed, "stars imply completed");
            Assert.AreEqual(SaveValidator.MaxWave, meadow.FindMode(DifficultyMode.Easy).bestWave);
            Assert.IsFalse(meadow.FindMode(DifficultyMode.Easy).completed);
            Assert.AreEqual(2, meadow.FindMode(DifficultyMode.Impossible).bestStars);
            Assert.IsNull(meadow.FindMode(DifficultyMode.Normal));
            Assert.AreEqual(3, meadow.bestStars, "totals recomputed from modes");
            Assert.AreEqual(SaveValidator.MaxWave, meadow.bestWave);
            Assert.IsTrue(meadow.completed);

            LevelRecord canyon = d.FindLevel("canyon");
            Assert.AreEqual(2, canyon.FindMode(DifficultyMode.Normal).bestStars);
            Assert.AreEqual(8, canyon.FindMode(DifficultyMode.Normal).bestWave);
            Assert.AreEqual(1, canyon.FindMode(DifficultyMode.Easy).bestStars);
            Assert.AreEqual(2, canyon.bestStars);

            LevelRecord marsh = d.FindLevel("marsh");
            Assert.AreEqual(1, marsh.FindMode(DifficultyMode.Normal).bestStars);

            Assert.AreEqual(2, d.endlessBestWaves.Count);
            Assert.AreEqual(40, d.endlessBestWaves.Find(e => e.mode == (int)DifficultyMode.Hard).bestWave);
            Assert.AreEqual(0, d.endlessBestWaves.Find(e => e.mode == (int)DifficultyMode.Easy).bestWave);
            Assert.AreEqual(40, d.endlessBestWave, "global best recomputed as the maximum");

            Assert.AreEqual((int)DifficultyMode.Normal, d.settings.lastDifficulty);
        }

        [Test]
        public void Validator_FoldsAnUnbackedLegacyEndlessBestIntoNormal()
        {
            var d = new SaveData { version = SaveData.CurrentVersion, endlessBestWave = 50, endlessBestWaves = null };
            SaveValidator.Sanitize(d);
            Assert.AreEqual(1, d.endlessBestWaves.Count);
            Assert.AreEqual((int)DifficultyMode.Normal, d.endlessBestWaves[0].mode);
            Assert.AreEqual(50, d.endlessBestWave);
        }

        [Test]
        public void HandEditedV3File_WithGarbageModes_LoadsAndIsRepaired()
        {
            File.WriteAllText(store.MainPath, @"{ ""version"": 3,
  ""levels"": [ { ""levelId"": ""meadow"", ""modes"": [ { ""mode"": 2, ""bestStars"": 77, ""bestWave"": 5 }, { ""mode"": 9, ""bestStars"": 3 } ] } ],
  ""endlessBestWaves"": [ { ""mode"": 3, ""bestWave"": 11 } ],
  ""settings"": { ""lastDifficulty"": -4 } }");
            var s = new SaveService(new SaveFileStore(dir, new SystemTextJsonSaveSerializer()), clock.Get);
            s.Load();
            Assert.AreEqual(SaveLoadSource.Main, s.LastLoad.Source);
            Assert.AreEqual(3, s.GetBestStars("meadow", DifficultyMode.Hard));
            Assert.AreEqual(3, s.GetBestStars("meadow"));
            Assert.AreEqual(11, s.GetEndlessBestWave(DifficultyMode.Impossible));
            Assert.AreEqual(11, s.EndlessBestWave);
            Assert.AreEqual(DifficultyMode.Normal, s.LastDifficulty);
        }

        [Test]
        public void CorruptV3File_FallsBackToTheBackup_WithItsDifficulties()
        {
            service.RecordLevelResult("meadow", DifficultyMode.Hard, 3, 10);
            service.Save();
            service.RecordLevelResult("meadow", DifficultyMode.Easy, 1, 10);
            service.Save(); // .bak now holds the Hard-only save
            File.WriteAllText(store.MainPath, "{ \"version\": 3, \"levels\": [ { \"levelId\": "); // truncated

            var s = new SaveService(new SaveFileStore(dir, new SystemTextJsonSaveSerializer()), clock.Get);
            s.Load();
            Assert.AreEqual(SaveLoadSource.Backup, s.LastLoad.Source);
            Assert.IsTrue(s.LastLoad.CorruptionDetected);
            Assert.AreEqual(3, s.GetBestStars("meadow", DifficultyMode.Hard));
            Assert.AreEqual(0, s.GetBestStars("meadow", DifficultyMode.Easy));
        }

        [Test]
        public void InMemoryLegacyRecord_IsNotLostWhenADifficultyResultIsAdded()
        {
            service.Data.levels.Add(new LevelRecord { levelId = "meadow", bestStars = 2, bestWave = 9, completed = true });
            service.RecordLevelResult("meadow", DifficultyMode.Hard, 1, 4);
            Assert.AreEqual(2, service.GetBestStars("meadow"));
            Assert.AreEqual(2, service.GetBestStars("meadow", DifficultyMode.Normal));
            Assert.AreEqual(1, service.GetBestStars("meadow", DifficultyMode.Hard));
        }
    }
}
