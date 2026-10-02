using System;
using System.Collections.Generic;

namespace TowerDefense.Persistence
{
    /// <summary>
    /// Repairs out-of-range or malformed values so the rest of the game can
    /// trust <see cref="SaveData"/> blindly. Never throws; never rejects a file
    /// just because one value is odd (a hand-edited 7-star level becomes 3).
    /// </summary>
    public static class SaveValidator
    {
        public const int MaxStars = 3;
        public const int MaxWave = 100000;
        public const float MinGameSpeed = 1f;
        public const float MaxGameSpeed = 3f;
        public const int MaxIdLength = 64;

        /// <summary>Sanitize in place and return the same instance (or a default if null).</summary>
        public static SaveData Sanitize(SaveData data)
        {
            if (data == null) return SaveData.CreateDefault();

            if (data.settings == null) data.settings = new SettingsData();
            if (data.stats == null) data.stats = new LifetimeStats();
            if (data.levels == null) data.levels = new List<LevelRecord>();
            if (data.tutorialsSeen == null) data.tutorialsSeen = new List<string>();

            SettingsData s = data.settings;
            s.musicVolume = ClampFinite(s.musicVolume, 0f, 1f, SettingsData.DefaultMusicVolume);
            s.sfxVolume = ClampFinite(s.sfxVolume, 0f, 1f, SettingsData.DefaultSfxVolume);
            s.lastGameSpeed = ClampFinite(s.lastGameSpeed, MinGameSpeed, MaxGameSpeed, SettingsData.DefaultGameSpeed);

            LifetimeStats st = data.stats;
            st.enemiesDefeated = Math.Max(0, st.enemiesDefeated);
            st.victories = Math.Max(0, st.victories);
            st.gamesPlayed = Math.Max(st.victories, st.gamesPlayed); // every victory was a game played

            data.endlessBestWave = Clamp(data.endlessBestWave, 0, MaxWave);
            if (data.lastSavedUnixSeconds < 0) data.lastSavedUnixSeconds = 0;

            data.levels = SanitizeLevels(data.levels);
            data.tutorialsSeen = SanitizeIds(data.tutorialsSeen);
            return data;
        }

        /// <summary>Clamp a float, replacing NaN/Infinity with <paramref name="fallback"/>.</summary>
        public static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>Trimmed id, or null when it is unusable.</summary>
        public static string CleanId(string id)
        {
            if (id == null) return null;
            id = id.Trim();
            if (id.Length == 0) return null;
            if (id.Length > MaxIdLength) id = id.Substring(0, MaxIdLength);
            return id;
        }

        private static List<LevelRecord> SanitizeLevels(List<LevelRecord> source)
        {
            var result = new List<LevelRecord>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                LevelRecord r = source[i];
                if (r == null) continue;
                string id = CleanId(r.levelId);
                if (id == null) continue;

                int stars = Clamp(r.bestStars, 0, MaxStars);
                int wave = Clamp(r.bestWave, 0, MaxWave);
                bool completed = r.completed || stars > 0;

                LevelRecord existing = null;
                for (int j = 0; j < result.Count; j++)
                    if (result[j].levelId == id) { existing = result[j]; break; }

                if (existing == null)
                {
                    result.Add(new LevelRecord { levelId = id, bestStars = stars, bestWave = wave, completed = completed });
                }
                else
                {
                    // Duplicate entries (hand edits, old bugs): keep the best of each.
                    existing.bestStars = Math.Max(existing.bestStars, stars);
                    existing.bestWave = Math.Max(existing.bestWave, wave);
                    existing.completed |= completed;
                }
            }
            return result;
        }

        private static List<string> SanitizeIds(List<string> source)
        {
            var result = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                string id = CleanId(source[i]);
                if (id != null && !result.Contains(id)) result.Add(id);
            }
            return result;
        }
    }
}
