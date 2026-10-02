using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TowerDefense.Persistence;

namespace TowerDefense.PlatformTests
{
    [TestFixture]
    public class SaveMigrationTests
    {
        private string dir;
        private SaveFileStore store;

        [SetUp]
        public void SetUp()
        {
            dir = TempDir.Create();
            store = new SaveFileStore(dir, new SystemTextJsonSaveSerializer());
        }

        [TearDown]
        public void TearDown() => TempDir.Delete(dir);

        // The v1 prototype format: volumes in percent, endless record in the level list, no footer.
        private const string V1Json = @"{
  ""version"": 1,
  ""levels"": [
    { ""levelId"": ""meadow"", ""bestStars"": 3, ""bestWave"": 10, ""completed"": true },
    { ""levelId"": ""endless"", ""bestStars"": 0, ""bestWave"": 42, ""completed"": false }
  ],
  ""settings"": { ""musicVolume"": 80, ""sfxVolume"": 50, ""hapticsEnabled"": true, ""lastGameSpeed"": 2 },
  ""stats"": { ""enemiesDefeated"": 500, ""gamesPlayed"": 7, ""victories"": 3 }
}";

        [Test]
        public void V1File_MigratesToCurrent()
        {
            File.WriteAllText(store.MainPath, V1Json);
            SaveLoadResult r = store.Load();

            Assert.AreEqual(SaveLoadSource.Main, r.Source);
            Assert.AreEqual(1, r.OriginalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, r.Data.version);
            Assert.AreEqual(0.8f, r.Data.settings.musicVolume, 1e-6f);
            Assert.AreEqual(0.5f, r.Data.settings.sfxVolume, 1e-6f);
            Assert.AreEqual(42, r.Data.endlessBestWave);
            Assert.AreEqual(1, r.Data.levels.Count, "endless pseudo-level removed");
            Assert.AreEqual("meadow", r.Data.levels[0].levelId);
            Assert.AreEqual(500, r.Data.stats.enemiesDefeated);
        }

        [Test]
        public void UnversionedFile_IsTreatedAsV1()
        {
            File.WriteAllText(store.MainPath, "{ \"settings\": { \"musicVolume\": 30, \"sfxVolume\": 100 } }");
            SaveLoadResult r = store.Load();
            Assert.AreEqual(0, r.OriginalVersion);
            Assert.AreEqual(0.3f, r.Data.settings.musicVolume, 1e-6f);
            Assert.AreEqual(1f, r.Data.settings.sfxVolume, 1e-6f);
            Assert.AreEqual(SaveData.CurrentVersion, r.Data.version);
        }

        [Test]
        public void CurrentVersion_IsNotMigratedAgain()
        {
            SaveData d = SaveData.CreateDefault();
            d.settings.musicVolume = 0.8f;
            store.Save(d);
            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveData.CurrentVersion, r.OriginalVersion);
            Assert.AreEqual(0.8f, r.Data.settings.musicVolume, 1e-6f);
        }

        [Test]
        public void MigratedSave_IsRewrittenAtCurrentVersion_OnServiceLoad()
        {
            File.WriteAllText(store.MainPath, V1Json);
            var clock = new FakeClock();
            var service = new SaveService(store, clock.Get);
            service.Load();
            Assert.AreEqual(1, service.SaveCount, "migrated data is written back immediately");
            SaveLoadResult again = store.Load();
            Assert.AreEqual(SaveData.CurrentVersion, again.OriginalVersion);
            Assert.AreEqual(0.8f, again.Data.settings.musicVolume, 1e-6f, "migration not applied twice");
        }

        [Test]
        public void FutureVersion_LoadsBestEffort()
        {
            File.WriteAllText(store.MainPath,
                "{\"version\": 99, \"endlessBestWave\": 5, \"someFutureField\": {\"x\": 1}}");
            SaveLoadResult r = null;
            Assert.DoesNotThrow(() => r = store.Load());
            Assert.AreEqual(SaveLoadSource.Main, r.Source);
            Assert.IsTrue(r.FromFutureVersion);
            Assert.AreEqual(5, r.Data.endlessBestWave);
        }

        [Test]
        public void Migrate_ReturnsOriginalVersion()
        {
            var d = new SaveData { version = 1 };
            Assert.AreEqual(1, SaveMigrator.Migrate(d));
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
        }
    }

    [TestFixture]
    public class SaveValidatorTests
    {
        [Test]
        public void ClampsAndRepairsInvalidValues()
        {
            var d = new SaveData
            {
                version = SaveData.CurrentVersion,
                levels = new List<LevelRecord>
                {
                    new LevelRecord { levelId = "  meadow ", bestStars = 7, bestWave = -4 },
                    null,
                    new LevelRecord { levelId = "", bestStars = 3 },
                    new LevelRecord { levelId = null, bestStars = 3 },
                    new LevelRecord { levelId = "meadow", bestStars = 1, bestWave = 20 },
                    new LevelRecord { levelId = "canyon", bestStars = -1, bestWave = 999999999 },
                },
                endlessBestWave = -10,
                settings = new SettingsData { musicVolume = float.NaN, sfxVolume = 4f, lastGameSpeed = float.PositiveInfinity },
                stats = new LifetimeStats { enemiesDefeated = -5, gamesPlayed = 2, victories = 6 },
                tutorialsSeen = new List<string> { "build", null, " build ", "", "hero" },
                lastSavedUnixSeconds = -1,
            };

            SaveValidator.Sanitize(d);

            Assert.AreEqual(2, d.levels.Count);
            LevelRecord meadow = d.FindLevel("meadow");
            Assert.AreEqual(3, meadow.bestStars, "stars clamped to 3, duplicates merged by max");
            Assert.AreEqual(20, meadow.bestWave);
            Assert.IsTrue(meadow.completed, "stars > 0 implies completed");
            LevelRecord canyon = d.FindLevel("canyon");
            Assert.AreEqual(0, canyon.bestStars);
            Assert.AreEqual(SaveValidator.MaxWave, canyon.bestWave);
            Assert.IsFalse(canyon.completed);

            Assert.AreEqual(0, d.endlessBestWave);
            Assert.AreEqual(SettingsData.DefaultMusicVolume, d.settings.musicVolume);
            Assert.AreEqual(1f, d.settings.sfxVolume);
            Assert.AreEqual(SettingsData.DefaultGameSpeed, d.settings.lastGameSpeed);
            Assert.AreEqual(0, d.stats.enemiesDefeated);
            Assert.AreEqual(6, d.stats.victories);
            Assert.AreEqual(6, d.stats.gamesPlayed, "every victory is a game played");
            CollectionAssert.AreEqual(new[] { "build", "hero" }, d.tutorialsSeen);
            Assert.AreEqual(0, d.lastSavedUnixSeconds);
        }

        [Test]
        public void NullSections_AreRecreated()
        {
            var d = new SaveData { levels = null, settings = null, stats = null, tutorialsSeen = null };
            SaveValidator.Sanitize(d);
            Assert.IsNotNull(d.levels);
            Assert.IsNotNull(d.settings);
            Assert.IsNotNull(d.stats);
            Assert.IsNotNull(d.tutorialsSeen);
        }

        [Test]
        public void NullData_GivesDefault()
        {
            Assert.IsNotNull(SaveValidator.Sanitize(null));
        }

        [Test]
        public void LongIds_AreTruncated()
        {
            string id = SaveValidator.CleanId(new string('x', 500));
            Assert.AreEqual(SaveValidator.MaxIdLength, id.Length);
        }

        [Test]
        public void InvalidValuesInFile_AreClampedOnLoad()
        {
            string dir = TempDir.Create();
            try
            {
                var store = new SaveFileStore(dir, new SystemTextJsonSaveSerializer());
                File.WriteAllText(store.MainPath,
                    "{\"version\":2,\"settings\":{\"musicVolume\":-3,\"sfxVolume\":\"NaN\",\"lastGameSpeed\":9},\"levels\":[{\"levelId\":\"a\",\"bestStars\":99}]}");
                SaveData d = store.Load().Data;
                Assert.AreEqual(0f, d.settings.musicVolume);
                Assert.AreEqual(SettingsData.DefaultSfxVolume, d.settings.sfxVolume);
                Assert.AreEqual(SaveValidator.MaxGameSpeed, d.settings.lastGameSpeed);
                Assert.AreEqual(3, d.levels[0].bestStars);
            }
            finally { TempDir.Delete(dir); }
        }
    }
}
