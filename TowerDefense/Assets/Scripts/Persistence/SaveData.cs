using System;
using System.Collections.Generic;
using TowerDefense.Core;

namespace TowerDefense.Persistence
{
    /// <summary>
    /// Everything the game persists between launches. Plain serializable
    /// fields only (no properties, no dictionaries) so Unity's JsonUtility
    /// and System.Text.Json (tests) both round-trip it identically.
    /// Engine-free: never reference UnityEngine from this folder's root.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Schema version written by this build. Bump it and add a step to <see cref="SaveMigrator"/>.</summary>
        public const int CurrentVersion = 3;

        /// <summary>Schema version of this data. 0 = file had no version (pre-versioned, treated as v1).</summary>
        public int version;

        public List<LevelRecord> levels = new List<LevelRecord>();
        /// <summary>Best endless wave on any map and any difficulty (kept in sync with <see cref="endlessBestWaves"/>).</summary>
        public int endlessBestWave;
        /// <summary>v3: best endless wave on any map, per difficulty.</summary>
        public List<ModeWaveRecord> endlessBestWaves = new List<ModeWaveRecord>();
        public SettingsData settings = new SettingsData();
        public LifetimeStats stats = new LifetimeStats();
        /// <summary>Ids of tutorial hints already shown (e.g. "build", "upgrade", "hero").</summary>
        public List<string> tutorialsSeen = new List<string>();
        /// <summary>Informational only: UTC unix seconds of the last successful write.</summary>
        public long lastSavedUnixSeconds;

        /// <summary>A fresh save at the current schema version.</summary>
        public static SaveData CreateDefault()
        {
            return new SaveData { version = CurrentVersion };
        }

        /// <summary>Record for a level id, or null if the level was never finished.</summary>
        public LevelRecord FindLevel(string levelId)
        {
            if (levels == null || string.IsNullOrEmpty(levelId)) return null;
            for (int i = 0; i < levels.Count; i++)
            {
                LevelRecord r = levels[i];
                if (r != null && string.Equals(r.levelId, levelId, StringComparison.Ordinal)) return r;
            }
            return null;
        }
    }

    /// <summary>
    /// Best results on one hand-authored level. Since v3 the per-difficulty
    /// results in <see cref="modes"/> are the source of truth; the top-level
    /// fields are the best across all difficulties (what level select and unlocks
    /// read), recomputed by <see cref="SaveValidator"/> on load.
    /// </summary>
    [Serializable]
    public class LevelRecord
    {
        /// <summary>Matches LevelData.id (stable, never renamed).</summary>
        public string levelId;
        /// <summary>0-3, best on any difficulty. 0 means played but never won.</summary>
        public int bestStars;
        /// <summary>Furthest wave reached on this level on any difficulty.</summary>
        public int bestWave;
        /// <summary>Won at least once on any difficulty.</summary>
        public bool completed;
        /// <summary>v3: results per difficulty (at most one entry per mode).</summary>
        public List<ModeRecord> modes = new List<ModeRecord>();

        /// <summary>The entry for a difficulty, or null if never played on it.</summary>
        public ModeRecord FindMode(DifficultyMode mode)
        {
            if (modes == null) return null;
            for (int i = 0; i < modes.Count; i++)
                if (modes[i] != null && modes[i].mode == (int)mode) return modes[i];
            return null;
        }

        /// <summary>The entry for a difficulty, created on demand.</summary>
        public ModeRecord GetOrAddMode(DifficultyMode mode)
        {
            ModeRecord r = FindMode(mode);
            if (r != null) return r;
            if (modes == null) modes = new List<ModeRecord>();
            r = new ModeRecord { mode = (int)mode };
            modes.Add(r);
            return r;
        }

        /// <summary>
        /// Attribute top-level bests that no difficulty entry backs (pre-v3 data built in
        /// memory, hand edits) to Normal, so <see cref="RefreshTotals"/> never loses them.
        /// </summary>
        public void FoldUnbackedIntoNormal()
        {
            int stars = 0, wave = 0;
            bool done = false;
            if (modes != null)
            {
                for (int i = 0; i < modes.Count; i++)
                {
                    ModeRecord m = modes[i];
                    if (m == null) continue;
                    stars = Math.Max(stars, m.bestStars);
                    wave = Math.Max(wave, m.bestWave);
                    done |= m.completed;
                }
            }
            if (bestStars <= stars && bestWave <= wave && (!completed || done)) return;
            ModeRecord normal = GetOrAddMode(DifficultyMode.Normal);
            normal.bestStars = Math.Max(normal.bestStars, bestStars);
            normal.bestWave = Math.Max(normal.bestWave, bestWave);
            normal.completed |= completed || bestStars > 0;
        }

        /// <summary>Recompute the across-difficulty bests from <see cref="modes"/>.</summary>
        public void RefreshTotals()
        {
            int stars = 0, wave = 0;
            bool done = false;
            if (modes != null)
            {
                for (int i = 0; i < modes.Count; i++)
                {
                    ModeRecord m = modes[i];
                    if (m == null) continue;
                    if (m.bestStars > stars) stars = m.bestStars;
                    if (m.bestWave > wave) wave = m.bestWave;
                    done |= m.completed;
                }
            }
            bestStars = stars;
            bestWave = wave;
            completed = done;
        }
    }

    /// <summary>Best results on one level on one difficulty.</summary>
    [Serializable]
    public class ModeRecord
    {
        /// <summary>(int)DifficultyMode. Stored as an int so JsonUtility and System.Text.Json agree.</summary>
        public int mode = (int)DifficultyMode.Normal;
        public int bestStars;
        public int bestWave;
        public bool completed;

        public DifficultyMode GetMode() => Difficulty.FromInt(mode);
    }

    /// <summary>A best wave on one difficulty (endless records).</summary>
    [Serializable]
    public class ModeWaveRecord
    {
        /// <summary>(int)DifficultyMode.</summary>
        public int mode = (int)DifficultyMode.Normal;
        public int bestWave;

        public DifficultyMode GetMode() => Difficulty.FromInt(mode);
    }

    /// <summary>Player-facing options. Volumes are linear 0..1.</summary>
    [Serializable]
    public class SettingsData
    {
        public const float DefaultMusicVolume = 0.7f;
        public const float DefaultSfxVolume = 1f;
        public const float DefaultGameSpeed = 1f;

        public float musicVolume = DefaultMusicVolume;
        public float sfxVolume = DefaultSfxVolume;
        public bool hapticsEnabled = true;
        /// <summary>Last chosen fast-forward (1x..3x); restored when a level starts.</summary>
        public float lastGameSpeed = DefaultGameSpeed;
        /// <summary>v3: last difficulty picked, (int)DifficultyMode; preselected in the difficulty picker.</summary>
        public int lastDifficulty = (int)DifficultyMode.Normal;
    }

    /// <summary>Lifetime counters shown on a stats screen.</summary>
    [Serializable]
    public class LifetimeStats
    {
        public int enemiesDefeated;
        public int gamesPlayed;
        public int victories;
    }
}
