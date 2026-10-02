using System;

namespace TowerDefense.Persistence
{
    /// <summary>What changed when a level result was recorded (for "New best!" UI).</summary>
    public struct LevelResultOutcome
    {
        public int PreviousStars;
        public int PreviousBestWave;
        public bool NewBestStars;
        public bool NewBestWave;
        public bool FirstCompletion;
    }

    /// <summary>
    /// Game-facing save facade: owns the live <see cref="SaveData"/>, records
    /// results, and autosaves with a debounce. Engine-free; time comes from the
    /// injected clock and <see cref="Tick"/> is driven by a MonoBehaviour in
    /// Unity (SaveRuntime) - or by tests.
    ///
    /// Two kinds of changes:
    ///  - important (results, settings, tutorials): <see cref="MarkDirty"/>,
    ///    written <see cref="DebounceSeconds"/> after the last change (or at
    ///    most <see cref="MaxDelaySeconds"/> after the first one).
    ///  - cheap counters (enemies defeated): <see cref="MarkDirtyLazy"/>, written
    ///    with the next save or on <see cref="Flush"/> (app pause/quit), so a
    ///    busy fight does not touch the disk every few seconds.
    /// </summary>
    public sealed class SaveService
    {
        private readonly SaveFileStore store;
        private readonly Func<double> clock;
        private readonly Func<long> unixNow;

        private bool dirty;
        private bool lazyDirty;
        private double firstDirtyAt;
        private double lastChangeAt;

        public double DebounceSeconds { get; set; }
        public double MaxDelaySeconds { get; set; }

        public SaveData Data { get; private set; }
        public GameSettings Settings { get; }
        public SaveFileStore Store => store;
        public SaveLoadResult LastLoad { get; private set; }
        public bool IsLoaded { get; private set; }
        /// <summary>An autosave is scheduled.</summary>
        public bool IsDirty => dirty;
        /// <summary>Anything (scheduled or lazy) is not on disk yet.</summary>
        public bool HasUnsavedChanges => dirty || lazyDirty;
        public int SaveCount { get; private set; }

        /// <summary>Raised after Load() with the new data.</summary>
        public event Action<SaveData> Loaded;
        public event Action Saved;
        public event Action<string> SaveFailed;

        /// <param name="clock">Monotonic seconds (Unity: Time.realtimeSinceStartupAsDouble / unscaled time).</param>
        /// <param name="unixNow">UTC unix seconds for the informational timestamp; null = DateTime.UtcNow.</param>
        public SaveService(SaveFileStore store, Func<double> clock, double debounceSeconds = 1.5, double maxDelaySeconds = 10.0,
                           Func<long> unixNow = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.unixNow = unixNow ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            DebounceSeconds = debounceSeconds;
            MaxDelaySeconds = maxDelaySeconds;
            Data = SaveData.CreateDefault();
            Settings = new GameSettings(() => Data.settings, MarkDirty);
        }

        // ------------------------------------------------------------------ load / save

        /// <summary>Load from disk (recovering from corruption). Never throws.</summary>
        public SaveLoadResult Load()
        {
            SaveLoadResult result = store.Load();
            Data = result.Data ?? SaveData.CreateDefault();
            LastLoad = result;
            IsLoaded = true;
            dirty = false;
            lazyDirty = false;

            // Repair: recovered or migrated data is re-written right away so the
            // main file is valid and current again.
            bool repaired = result.Source == SaveLoadSource.Temp || result.Source == SaveLoadSource.Backup
                            || (result.Source == SaveLoadSource.Main && result.OriginalVersion < SaveData.CurrentVersion)
                            || (result.Source == SaveLoadSource.Defaults && result.CorruptionDetected);
            if (repaired) Save();

            Loaded?.Invoke(Data);
            Settings.NotifyAllChanged();
            return result;
        }

        /// <summary>Write now. Returns false on I/O failure (data stays dirty and is retried).</summary>
        public bool Save()
        {
            long previousStamp = Data.lastSavedUnixSeconds;
            Data.lastSavedUnixSeconds = unixNow();
            if (store.Save(Data))
            {
                dirty = false;
                lazyDirty = false;
                SaveCount++;
                Saved?.Invoke();
                return true;
            }
            Data.lastSavedUnixSeconds = previousStamp;
            dirty = true;
            double now = clock();
            firstDirtyAt = now;
            lastChangeAt = now; // back off one debounce interval before retrying
            SaveFailed?.Invoke(store.LastError);
            return false;
        }

        /// <summary>Save if anything changed (call on app pause / quit / level end).</summary>
        public bool Flush()
        {
            if (!HasUnsavedChanges) return true;
            return Save();
        }

        /// <summary>Schedule a debounced autosave.</summary>
        public void MarkDirty()
        {
            double now = clock();
            if (!dirty) firstDirtyAt = now;
            lastChangeAt = now;
            dirty = true;
        }

        /// <summary>Remember that something changed without scheduling a write.</summary>
        public void MarkDirtyLazy() => lazyDirty = true;

        /// <summary>Call every frame (or a few times a second). Writes when the debounce elapses.</summary>
        public void Tick()
        {
            if (!dirty) return;
            double now = clock();
            if (now - lastChangeAt >= DebounceSeconds || now - firstDirtyAt >= MaxDelaySeconds) Save();
        }

        // ------------------------------------------------------------------ progress

        /// <summary>
        /// Record a finished campaign level. Keeps the best stars and best wave,
        /// counts the game, and counts a victory when <paramref name="stars"/> &gt; 0.
        /// </summary>
        public LevelResultOutcome RecordLevelResult(string levelId, int stars, int wave)
        {
            var outcome = new LevelResultOutcome();
            string id = SaveValidator.CleanId(levelId);
            stars = SaveValidator.Clamp(stars, 0, SaveValidator.MaxStars);
            wave = SaveValidator.Clamp(wave, 0, SaveValidator.MaxWave);

            Data.stats.gamesPlayed++;
            if (stars > 0) Data.stats.victories++;

            if (id != null)
            {
                LevelRecord r = Data.FindLevel(id);
                if (r == null)
                {
                    r = new LevelRecord { levelId = id };
                    Data.levels.Add(r);
                }
                outcome.PreviousStars = r.bestStars;
                outcome.PreviousBestWave = r.bestWave;
                outcome.FirstCompletion = stars > 0 && !r.completed;
                if (stars > r.bestStars) { r.bestStars = stars; outcome.NewBestStars = true; }
                if (wave > r.bestWave) { r.bestWave = wave; outcome.NewBestWave = true; }
                if (stars > 0) r.completed = true;
            }
            MarkDirty();
            return outcome;
        }

        /// <summary>
        /// Record an endless run. Returns true on a new best wave. Pass countGame = false
        /// when RecordLevelResult already counted this run for its map.
        /// </summary>
        public bool RecordEndlessResult(int wave, bool countGame = true)
        {
            wave = SaveValidator.Clamp(wave, 0, SaveValidator.MaxWave);
            if (countGame) Data.stats.gamesPlayed++;
            bool best = wave > Data.endlessBestWave;
            if (best) Data.endlessBestWave = wave;
            MarkDirty();
            return best;
        }

        public int EndlessBestWave => Data.endlessBestWave;

        public int GetBestStars(string levelId)
        {
            LevelRecord r = Data.FindLevel(levelId);
            return r != null ? r.bestStars : 0;
        }

        public int GetBestWave(string levelId)
        {
            LevelRecord r = Data.FindLevel(levelId);
            return r != null ? r.bestWave : 0;
        }

        public bool IsLevelCompleted(string levelId)
        {
            LevelRecord r = Data.FindLevel(levelId);
            return r != null && r.completed;
        }

        /// <summary>Sum of best stars over all levels (for unlock gates).</summary>
        public int TotalStars
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Data.levels.Count; i++) total += Data.levels[i].bestStars;
                return total;
            }
        }

        // ------------------------------------------------------------------ stats / tutorials

        /// <summary>Count kills. Lazy: written with the next save or flush.</summary>
        public void AddEnemiesDefeated(int count)
        {
            if (count <= 0) return;
            long total = (long)Data.stats.enemiesDefeated + count;
            Data.stats.enemiesDefeated = total > int.MaxValue ? int.MaxValue : (int)total;
            MarkDirtyLazy();
        }

        public bool IsTutorialSeen(string tutorialId)
        {
            string id = SaveValidator.CleanId(tutorialId);
            return id != null && Data.tutorialsSeen.Contains(id);
        }

        /// <summary>Mark a tutorial hint as shown. Returns false if it already was.</summary>
        public bool MarkTutorialSeen(string tutorialId)
        {
            string id = SaveValidator.CleanId(tutorialId);
            if (id == null || Data.tutorialsSeen.Contains(id)) return false;
            Data.tutorialsSeen.Add(id);
            MarkDirty();
            return true;
        }

        /// <summary>Wipe progress, stats and tutorials; keeps settings. Saves immediately.</summary>
        public bool ResetProgress()
        {
            SettingsData keep = Data.settings;
            Data = SaveData.CreateDefault();
            Data.settings = keep;
            bool ok = Save();
            Loaded?.Invoke(Data);
            return ok;
        }
    }
}
