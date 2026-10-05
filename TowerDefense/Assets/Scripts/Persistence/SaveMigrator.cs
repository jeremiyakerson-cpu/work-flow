using System;
using System.Collections.Generic;
using TowerDefense.Core;

namespace TowerDefense.Persistence
{
    /// <summary>
    /// Forward-only schema migrations. Each step upgrades data from version N to
    /// N+1 in place; <see cref="Migrate"/> runs every step the file needs.
    ///
    /// Adding a schema change:
    ///   1. bump <see cref="SaveData.CurrentVersion"/>,
    ///   2. append a step to <see cref="Steps"/> (index = from-version - 1),
    ///   3. add a test feeding an old-format JSON through the store.
    /// Fields that only gain a default need no step: missing JSON keys keep the
    /// field initializer. Steps are for renamed/moved/re-scaled data.
    /// </summary>
    public static class SaveMigrator
    {
        /// <summary>Level id the v1 prototype used to store the endless record in the level list.</summary>
        public const string LegacyEndlessLevelId = "endless";

        /// <summary>Steps[i] upgrades version i+1 to i+2.</summary>
        private static readonly Action<SaveData>[] Steps =
        {
            MigrateV1ToV2,
            MigrateV2ToV3,
        };

        /// <summary>
        /// Upgrade <paramref name="data"/> to <see cref="SaveData.CurrentVersion"/>.
        /// Returns the version the data had before migrating (0/1 = v1).
        /// Data from a newer build (version above current) is left untouched.
        /// </summary>
        public static int Migrate(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int original = data.version;
            int v = data.version <= 0 ? 1 : data.version; // unversioned files are v1
            while (v < SaveData.CurrentVersion)
            {
                int index = v - 1;
                if (index < 0 || index >= Steps.Length)
                    throw new InvalidOperationException("No save migration from version " + v);
                Steps[index](data);
                v++;
            }
            if (data.version < SaveData.CurrentVersion) data.version = SaveData.CurrentVersion;
            return original;
        }

        /// <summary>
        /// v1 (prototype): volumes were stored as percentages 0..100 and the
        /// endless best wave lived in the level list under id "endless".
        /// v2: volumes are linear 0..1 and endless has its own field.
        /// </summary>
        private static void MigrateV1ToV2(SaveData data)
        {
            if (data.settings != null)
            {
                data.settings.musicVolume /= 100f;
                data.settings.sfxVolume /= 100f;
            }
            if (data.levels != null)
            {
                for (int i = data.levels.Count - 1; i >= 0; i--)
                {
                    LevelRecord r = data.levels[i];
                    if (r == null || r.levelId != LegacyEndlessLevelId) continue;
                    if (r.bestWave > data.endlessBestWave) data.endlessBestWave = r.bestWave;
                    data.levels.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// v2: one best stars / best wave per level and one endless best wave.
        /// v3: results per difficulty. Everything played before difficulties
        /// existed was Normal, so existing records move to Normal; the top-level
        /// fields stay as the across-difficulty bests. The last difficulty starts at Normal.
        /// </summary>
        private static void MigrateV2ToV3(SaveData data)
        {
            if (data.levels != null)
            {
                for (int i = 0; i < data.levels.Count; i++)
                {
                    LevelRecord r = data.levels[i];
                    if (r == null) continue;
                    if (r.modes == null) r.modes = new List<ModeRecord>();
                    if (r.modes.Count > 0) continue; // already per-difficulty (hand edit / partial write)
                    r.modes.Add(new ModeRecord
                    {
                        mode = (int)DifficultyMode.Normal,
                        bestStars = r.bestStars,
                        bestWave = r.bestWave,
                        completed = r.completed,
                    });
                }
            }
            if (data.endlessBestWaves == null) data.endlessBestWaves = new List<ModeWaveRecord>();
            if (data.endlessBestWaves.Count == 0 && data.endlessBestWave > 0)
                data.endlessBestWaves.Add(new ModeWaveRecord { mode = (int)DifficultyMode.Normal, bestWave = data.endlessBestWave });
            if (data.settings != null) data.settings.lastDifficulty = (int)DifficultyMode.Normal;
        }
    }
}
