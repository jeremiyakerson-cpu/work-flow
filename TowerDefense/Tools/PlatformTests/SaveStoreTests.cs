using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TowerDefense.Persistence;

namespace TowerDefense.PlatformTests
{
    [TestFixture]
    public class SaveStoreTests
    {
        private string dir;
        private FaultyFileSystem fs;
        private SaveFileStore store;
        private readonly List<string> logs = new List<string>();

        [SetUp]
        public void SetUp()
        {
            dir = TempDir.Create();
            fs = new FaultyFileSystem();
            logs.Clear();
            store = new SaveFileStore(dir, new SystemTextJsonSaveSerializer(), fs, "save.json", logs.Add);
        }

        [TearDown]
        public void TearDown() => TempDir.Delete(dir);

        private static SaveData Sample(int stars = 2, int wave = 12)
        {
            SaveData d = SaveData.CreateDefault();
            d.levels.Add(new LevelRecord { levelId = "meadow", bestStars = stars, bestWave = wave, completed = stars > 0 });
            d.endlessBestWave = 31;
            d.settings.musicVolume = 0.25f;
            d.settings.sfxVolume = 0.5f;
            d.settings.hapticsEnabled = false;
            d.settings.lastGameSpeed = 2f;
            d.stats.enemiesDefeated = 1234;
            d.stats.gamesPlayed = 9;
            d.stats.victories = 4;
            d.tutorialsSeen.Add("build");
            return d;
        }

        [Test]
        public void MissingFiles_LoadDefaults_WithoutCorruption()
        {
            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveLoadSource.Defaults, r.Source);
            Assert.IsFalse(r.CorruptionDetected);
            Assert.AreEqual(SaveData.CurrentVersion, r.Data.version);
            Assert.AreEqual(SettingsData.DefaultMusicVolume, r.Data.settings.musicVolume);
        }

        [Test]
        public void SaveThenLoad_RoundTripsEveryField()
        {
            Assert.IsTrue(store.Save(Sample()));
            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveLoadSource.Main, r.Source);
            SaveData d = r.Data;
            Assert.AreEqual(1, d.levels.Count);
            Assert.AreEqual("meadow", d.levels[0].levelId);
            Assert.AreEqual(2, d.levels[0].bestStars);
            Assert.AreEqual(12, d.levels[0].bestWave);
            Assert.IsTrue(d.levels[0].completed);
            Assert.AreEqual(31, d.endlessBestWave);
            Assert.AreEqual(0.25f, d.settings.musicVolume);
            Assert.AreEqual(0.5f, d.settings.sfxVolume);
            Assert.IsFalse(d.settings.hapticsEnabled);
            Assert.AreEqual(2f, d.settings.lastGameSpeed);
            Assert.AreEqual(1234, d.stats.enemiesDefeated);
            Assert.AreEqual(9, d.stats.gamesPlayed);
            Assert.AreEqual(4, d.stats.victories);
            CollectionAssert.AreEqual(new[] { "build" }, d.tutorialsSeen);
        }

        [Test]
        public void SecondSave_KeepsPreviousFileAsBackup()
        {
            store.Save(Sample(stars: 1));
            store.Save(Sample(stars: 3));
            Assert.IsTrue(File.Exists(store.BackupPath));
            Assert.IsFalse(File.Exists(store.TempPath), "temp file must not linger after a good save");
            Assert.IsTrue(SaveFileStore.TryDecode(File.ReadAllText(store.BackupPath), out string json, out _));
            StringAssert.Contains("\"bestStars\": 1", json);
            Assert.AreEqual(3, store.Load().Data.levels[0].bestStars);
        }

        [Test]
        public void GarbageMainFile_FallsBackToBackup_AndQuarantinesMain()
        {
            store.Save(Sample(stars: 1));
            store.Save(Sample(stars: 2));
            File.WriteAllText(store.MainPath, "{ this is not json ]");

            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveLoadSource.Backup, r.Source);
            Assert.IsTrue(r.CorruptionDetected);
            Assert.AreEqual(1, r.Data.levels[0].bestStars);
            Assert.IsTrue(File.Exists(store.CorruptPath), "corrupt file kept aside for diagnosis");
            Assert.IsFalse(File.Exists(store.MainPath));
        }

        [Test]
        public void TruncatedMainFile_IsDetected()
        {
            store.Save(Sample(stars: 1));
            store.Save(Sample(stars: 2));
            string text = File.ReadAllText(store.MainPath);
            File.WriteAllText(store.MainPath, text.Substring(0, text.Length - 12)); // lose the end of the footer

            SaveLoadResult r = store.Load();
            Assert.IsTrue(r.CorruptionDetected);
            Assert.AreEqual(SaveLoadSource.Backup, r.Source);
        }

        [Test]
        public void TruncatedInsideJson_IsDetected()
        {
            store.Save(Sample());
            string text = File.ReadAllText(store.MainPath);
            File.WriteAllText(store.MainPath, text.Substring(0, text.Length / 2));
            SaveLoadResult r = store.Load();
            Assert.IsTrue(r.CorruptionDetected);
            Assert.AreEqual(SaveLoadSource.Defaults, r.Source);
        }

        [Test]
        public void BitFlipThatStillParses_FailsChecksum()
        {
            store.Save(Sample(stars: 1));
            store.Save(Sample(stars: 2));
            string text = File.ReadAllText(store.MainPath);
            File.WriteAllText(store.MainPath, text.Replace("\"bestStars\": 2", "\"bestStars\": 3"));

            SaveLoadResult r = store.Load();
            Assert.IsTrue(r.CorruptionDetected);
            Assert.AreEqual(SaveLoadSource.Backup, r.Source);
            Assert.AreEqual(1, r.Data.levels[0].bestStars);
        }

        [TestCase("")]
        [TestCase("   \n")]
        [TestCase("null")]
        [TestCase("[1,2,3]")]
        [TestCase("\0\0\0\0")]
        public void UnusableMainAndNoBackup_GivesDefaults_NeverThrows(string content)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(store.MainPath, content);
            SaveLoadResult r = null;
            Assert.DoesNotThrow(() => r = store.Load());
            Assert.AreEqual(SaveLoadSource.Defaults, r.Source);
            Assert.IsTrue(r.CorruptionDetected);
            Assert.IsNotNull(r.Data.settings);
        }

        [Test]
        public void MainAndBackupBothCorrupt_GivesDefaults()
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(store.MainPath, "garbage");
            File.WriteAllText(store.BackupPath, "{\"version\": ");
            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveLoadSource.Defaults, r.Source);
            Assert.IsTrue(r.CorruptionDetected);
        }

        [Test]
        public void CrashMidTempWrite_KeepsOldSave_AndCleansPartialTemp()
        {
            store.Save(Sample(stars: 1));
            fs.Crash = CrashPoint.MidTempWrite;
            Assert.IsFalse(store.Save(Sample(stars: 3)));
            Assert.IsTrue(File.Exists(store.TempPath), "the simulated crash leaves a partial temp file");

            fs.Crash = CrashPoint.None;
            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveLoadSource.Main, r.Source);
            Assert.AreEqual(1, r.Data.levels[0].bestStars);
            Assert.IsFalse(r.CorruptionDetected);
            Assert.IsFalse(File.Exists(store.TempPath));
        }

        [Test]
        public void CrashMidFirstEverWrite_GivesDefaults()
        {
            fs.Crash = CrashPoint.MidTempWrite;
            Assert.IsFalse(store.Save(Sample()));
            fs.Crash = CrashPoint.None;
            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveLoadSource.Defaults, r.Source);
            Assert.IsFalse(File.Exists(store.TempPath), "partial temp must be discarded");
        }

        [Test]
        public void CrashBeforeReplace_LoadsIntactMain()
        {
            store.Save(Sample(stars: 1));
            fs.Crash = CrashPoint.BeforeReplace;
            Assert.IsFalse(store.Save(Sample(stars: 3)));
            fs.Crash = CrashPoint.None;

            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveLoadSource.Main, r.Source);
            Assert.AreEqual(1, r.Data.levels[0].bestStars);
        }

        [Test]
        public void CrashBetweenDeleteAndMove_PromotesCompleteTemp()
        {
            store.Save(Sample(stars: 1));
            fs.Crash = CrashPoint.AfterDeleteBeforeMove;
            Assert.IsFalse(store.Save(Sample(stars: 3)));
            Assert.IsFalse(File.Exists(store.MainPath));
            fs.Crash = CrashPoint.None;

            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveLoadSource.Temp, r.Source);
            Assert.AreEqual(3, r.Data.levels[0].bestStars, "the newest complete write wins");
            Assert.IsTrue(File.Exists(store.MainPath), "temp promoted to main");
            Assert.IsFalse(File.Exists(store.TempPath));
        }

        [Test]
        public void WriteFailure_ReturnsFalse_DoesNotThrow_AndLogs()
        {
            fs.Crash = CrashPoint.WriteThrows;
            bool ok = true;
            Assert.DoesNotThrow(() => ok = store.Save(Sample()));
            Assert.IsFalse(ok);
            StringAssert.Contains("No space left", store.LastError);
            Assert.IsNotEmpty(logs);
        }

        [Test]
        public void HandEditedFileWithoutFooter_IsAccepted()
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(store.MainPath,
                "{\"version\":2,\"levels\":[{\"levelId\":\"canyon\",\"bestStars\":3,\"bestWave\":15,\"completed\":true}]}");
            SaveLoadResult r = store.Load();
            Assert.AreEqual(SaveLoadSource.Main, r.Source);
            Assert.AreEqual("canyon", r.Data.levels[0].levelId);
            Assert.AreEqual(SettingsData.DefaultSfxVolume, r.Data.settings.sfxVolume, "missing keys keep defaults");
        }

        [Test]
        public void Footer_RoundTripsAndRejectsTampering()
        {
            string encoded = SaveFileStore.Encode("{\"a\":\"é\"}"); // non-ASCII: length is in UTF-8 bytes
            Assert.IsTrue(SaveFileStore.TryDecode(encoded, out string json, out _));
            Assert.AreEqual("{\"a\":\"é\"}", json);
            Assert.IsFalse(SaveFileStore.TryDecode(encoded.Replace("é", "e"), out _, out string reason));
            Assert.IsNotNull(reason);
            Assert.IsFalse(SaveFileStore.TryDecode("{}\n#td-save crc32=zz", out _, out _));
        }

        [Test]
        public void Crc32_MatchesKnownVector()
        {
            // The standard check value for "123456789".
            Assert.AreEqual(0xCBF43926u, Crc32.Compute(System.Text.Encoding.ASCII.GetBytes("123456789")));
        }

        [Test]
        public void SaveCreatesMissingDirectory()
        {
            string nested = Path.Combine(dir, "a", "b");
            var s = new SaveFileStore(nested, new SystemTextJsonSaveSerializer());
            Assert.IsTrue(s.Save(Sample()));
            Assert.AreEqual(SaveLoadSource.Main, s.Load().Source);
        }
    }
}
