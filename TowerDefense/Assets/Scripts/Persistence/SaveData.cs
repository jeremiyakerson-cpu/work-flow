using System;
using System.Collections.Generic;

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
        public const int CurrentVersion = 2;

        /// <summary>Schema version of this data. 0 = file had no version (pre-versioned, treated as v1).</summary>
        public int version;

        public List<LevelRecord> levels = new List<LevelRecord>();
        public int endlessBestWave;
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

    /// <summary>Best results on one hand-authored level.</summary>
    [Serializable]
    public class LevelRecord
    {
        /// <summary>Matches LevelData.id (stable, never renamed).</summary>
        public string levelId;
        /// <summary>0-3. 0 means played but never won.</summary>
        public int bestStars;
        /// <summary>Furthest wave reached on this level.</summary>
        public int bestWave;
        public bool completed;
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
