using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TowerDefense.Persistence;

namespace TowerDefense.PlatformTests
{
    [TestFixture]
    public class SaveServiceTests
    {
        private string dir;
        private FakeClock clock;
        private FaultyFileSystem fs;
        private SaveFileStore store;
        private SaveService service;

        [SetUp]
        public void SetUp()
        {
            dir = TempDir.Create();
            clock = new FakeClock();
            fs = new FaultyFileSystem();
            store = new SaveFileStore(dir, new SystemTextJsonSaveSerializer(), fs);
            service = new SaveService(store, clock.Get, debounceSeconds: 1.5, maxDelaySeconds: 10, unixNow: () => 1700000000);
            service.Load();
        }

        [TearDown]
        public void TearDown() => TempDir.Delete(dir);

        private SaveData Reload() => new SaveFileStore(dir, new SystemTextJsonSaveSerializer()).Load().Data;

        [Test]
        public void FreshInstall_LoadsDefaults_WithoutWriting()
        {
            Assert.AreEqual(SaveLoadSource.Defaults, service.LastLoad.Source);
            Assert.AreEqual(0, service.SaveCount);
            Assert.IsFalse(File.Exists(store.MainPath));
        }

        [Test]
        public void Debounce_WaitsForQuietPeriod()
        {
            service.RecordLevelResult("meadow", 2, 10);
            Assert.IsTrue(service.IsDirty);
            clock.Now = 1.0; service.Tick();
            Assert.AreEqual(0, service.SaveCount, "too early");
            clock.Now = 1.6; service.Tick();
            Assert.AreEqual(1, service.SaveCount);
            Assert.IsFalse(service.IsDirty);
            Assert.AreEqual(2, Reload().FindLevel("meadow").bestStars);
            Assert.AreEqual(1700000000, Reload().lastSavedUnixSeconds);
        }

        [Test]
        public void Debounce_ContinuousChanges_AreCappedByMaxDelay()
        {
            for (int i = 0; i <= 12; i++)
            {
                clock.Now = i; // a change every second never leaves a 1.5 s quiet period
                service.Settings.SetMusicVolume(i % 2 == 0 ? 0.2f : 0.3f);
                service.Tick();
            }
            Assert.AreEqual(1, service.SaveCount, "max delay forces exactly one write by t=10");
        }

        [Test]
        public void LazyStats_AreNotWrittenByTick_ButByFlush()
        {
            service.AddEnemiesDefeated(5);
            clock.Now = 100; service.Tick();
            Assert.AreEqual(0, service.SaveCount);
            Assert.IsTrue(service.HasUnsavedChanges);
            Assert.IsTrue(service.Flush());
            Assert.AreEqual(1, service.SaveCount);
            Assert.AreEqual(5, Reload().stats.enemiesDefeated);
            Assert.IsTrue(service.Flush(), "nothing to do is success");
            Assert.AreEqual(1, service.SaveCount);
        }

        [Test]
        public void EnemiesDefeated_SaturatesInsteadOfOverflowing()
        {
            service.Data.stats.enemiesDefeated = int.MaxValue - 1;
            service.AddEnemiesDefeated(10);
            Assert.AreEqual(int.MaxValue, service.Data.stats.enemiesDefeated);
        }

        [Test]
        public void RecordLevelResult_KeepsBests_AndCountsGames()
        {
            LevelResultOutcome first = service.RecordLevelResult("meadow", 2, 15);
            Assert.IsTrue(first.NewBestStars);
            Assert.IsTrue(first.NewBestWave);
            Assert.IsTrue(first.FirstCompletion);

            LevelResultOutcome worse = service.RecordLevelResult("meadow", 1, 9);
            Assert.IsFalse(worse.NewBestStars);
            Assert.IsFalse(worse.NewBestWave);
            Assert.IsFalse(worse.FirstCompletion);
            Assert.AreEqual(2, worse.PreviousStars);

            LevelResultOutcome loss = service.RecordLevelResult("canyon", 0, 7);
            Assert.IsFalse(loss.FirstCompletion);

            Assert.AreEqual(2, service.GetBestStars("meadow"));
            Assert.AreEqual(15, service.GetBestWave("meadow"));
            Assert.IsTrue(service.IsLevelCompleted("meadow"));
            Assert.IsFalse(service.IsLevelCompleted("canyon"));
            Assert.AreEqual(7, service.GetBestWave("canyon"));
            Assert.AreEqual(2, service.TotalStars);
            Assert.AreEqual(3, service.Data.stats.gamesPlayed);
            Assert.AreEqual(2, service.Data.stats.victories);
            Assert.AreEqual(0, service.GetBestStars("unknown"));
        }

        [Test]
        public void RecordLevelResult_ClampsInput()
        {
            service.RecordLevelResult("meadow", 12, -3);
            Assert.AreEqual(3, service.GetBestStars("meadow"));
            Assert.AreEqual(0, service.GetBestWave("meadow"));
        }

        [Test]
        public void RecordEndlessResult_TracksBest()
        {
            Assert.IsTrue(service.RecordEndlessResult(20));
            Assert.IsFalse(service.RecordEndlessResult(12));
            Assert.AreEqual(20, service.EndlessBestWave);
            Assert.AreEqual(2, service.Data.stats.gamesPlayed);
            Assert.AreEqual(0, service.Data.stats.victories);
        }

        [Test]
        public void EndlessRun_RecordedPerMapAndGlobally_CountsOneGame()
        {
            service.RecordLevelResult("meadow_endless", 0, 17);
            Assert.IsTrue(service.RecordEndlessResult(17, countGame: false));
            Assert.AreEqual(17, service.GetBestWave("meadow_endless"));
            Assert.AreEqual(17, service.EndlessBestWave);
            Assert.AreEqual(1, service.Data.stats.gamesPlayed);
        }

        [Test]
        public void Tutorials_AreRememberedOnce()
        {
            Assert.IsFalse(service.IsTutorialSeen("build"));
            Assert.IsTrue(service.MarkTutorialSeen("build"));
            Assert.IsFalse(service.MarkTutorialSeen("build"));
            Assert.IsFalse(service.MarkTutorialSeen("  "));
            service.Flush();
            CollectionAssert.AreEqual(new[] { "build" }, Reload().tutorialsSeen);
        }

        [Test]
        public void FailedSave_StaysDirty_RaisesEvent_AndRetries()
        {
            string error = null;
            service.SaveFailed += e => error = e;
            fs.Crash = CrashPoint.WriteThrows;
            service.RecordLevelResult("meadow", 3, 10);
            clock.Now = 2; service.Tick();
            Assert.IsNotNull(error);
            Assert.IsTrue(service.IsDirty);

            fs.Crash = CrashPoint.None;
            clock.Now = 2.5; service.Tick();
            Assert.AreEqual(0, service.SaveCount, "backs off one debounce interval");
            clock.Now = 4; service.Tick();
            Assert.AreEqual(1, service.SaveCount);
            Assert.AreEqual(3, Reload().FindLevel("meadow").bestStars);
        }

        [Test]
        public void LoadFromBackup_RepairsMainImmediately()
        {
            service.RecordLevelResult("meadow", 1, 5); service.Save();
            service.RecordLevelResult("meadow", 2, 6); service.Save();
            File.WriteAllText(store.MainPath, "corrupt!");

            var fresh = new SaveService(store, clock.Get);
            SaveLoadResult r = fresh.Load();
            Assert.AreEqual(SaveLoadSource.Backup, r.Source);
            Assert.AreEqual(1, fresh.SaveCount);
            Assert.AreEqual(SaveLoadSource.Main, store.Load().Source);
            Assert.AreEqual(1, fresh.GetBestStars("meadow"));
        }

        [Test]
        public void ResetProgress_KeepsSettings()
        {
            service.Settings.SetSfxVolume(0.3f);
            service.RecordLevelResult("meadow", 3, 10);
            service.MarkTutorialSeen("build");
            Assert.IsTrue(service.ResetProgress());
            Assert.AreEqual(0, service.TotalStars);
            Assert.IsFalse(service.IsTutorialSeen("build"));
            Assert.AreEqual(0.3f, service.Settings.GetSfxVolume());
            Assert.AreEqual(0.3f, Reload().settings.sfxVolume);
        }

        [Test]
        public void Settings_ClampNotifyOnlyOnChange_AndScheduleSave()
        {
            var music = new List<float>();
            int changed = 0;
            service.Settings.MusicVolumeChanged += music.Add;
            service.Settings.Changed += () => changed++;

            service.Settings.SetMusicVolume(1.7f);
            service.Settings.SetMusicVolume(1f);       // same after clamp: no event
            service.Settings.SetMusicVolume(float.NaN); // ignored
            service.Settings.MusicVolume = 0.4f;
            CollectionAssert.AreEqual(new[] { 1f, 0.4f }, music);
            Assert.AreEqual(2, changed);
            Assert.IsTrue(service.IsDirty);

            service.Settings.SetLastGameSpeed(10f);
            Assert.AreEqual(SaveValidator.MaxGameSpeed, service.Settings.GetLastGameSpeed());
            service.Settings.SetLastGameSpeed(0.1f);
            Assert.AreEqual(SaveValidator.MinGameSpeed, service.Settings.GetLastGameSpeed());

            bool? haptics = null;
            service.Settings.HapticsEnabledChanged += v => haptics = v;
            service.Settings.SetHapticsEnabled(true); // already true
            Assert.IsNull(haptics);
            service.Settings.HapticsEnabled = false;
            Assert.AreEqual(false, haptics);
        }

        [Test]
        public void Load_NotifiesSettingsListeners()
        {
            service.Settings.SetSfxVolume(0.2f);
            service.Flush();

            var fresh = new SaveService(store, clock.Get);
            float sfx = -1;
            fresh.Settings.SfxVolumeChanged += v => sfx = v;
            SaveData loaded = null;
            fresh.Loaded += d => loaded = d;
            fresh.Load();
            Assert.AreEqual(0.2f, sfx);
            Assert.AreSame(fresh.Data, loaded);
        }
    }
}
