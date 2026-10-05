using System;
using System.Collections.Generic;
using TowerDefense.Core;

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
            if (!Difficulty.IsDefined(s.lastDifficulty)) s.lastDifficulty = (int)DifficultyMode.Normal;

            LifetimeStats st = data.stats;
            st.enemiesDefeated = Math.Max(0, st.enemiesDefeated);
            st.victories = Math.Max(0, st.victories);
            st.gamesPlayed = Math.Max(st.victories, st.gamesPlayed); // every victory was a game played

            data.endlessBestWave = Clamp(data.endlessBestWave, 0, MaxWave);
            data.endlessBestWaves = SanitizeEndless(data.endlessBestWaves, data.endlessBestWave);
            data.endlessBestWave = 0;
            for (int i = 0; i < data.endlessBestWaves.Count; i++)
                data.endlessBestWave = Math.Max(data.endlessBestWave, data.endlessBestWaves[i].bestWave);
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
                    existing = new LevelRecord { levelId = id };
                    result.Add(existing);
                }

                // Per-difficulty entries: drop unknown modes, clamp, merge duplicates (best of each).
                int modeStars = 0, modeWave = 0;
                bool modeCompleted = false;
                if (r.modes != null)
                {
                    for (int k = 0; k < r.modes.Count; k++)
                    {
                        ModeRecord m = r.modes[k];
                        if (m == null) continue;
                        int ms = Clamp(m.bestStars, 0, MaxStars);
                        int mw = Clamp(m.bestWave, 0, MaxWave);
                        bool mc = m.completed || ms > 0;
                        // Unknown modes (a newer build's file) still back the top-level bests,
                        // so they are not misattributed to Normal below; they are just not kept.
                        modeStars = Math.Max(modeStars, ms);
                        modeWave = Math.Max(modeWave, mw);
                        modeCompleted |= mc;
                        if (!Difficulty.IsDefined(m.mode)) continue;
                        Merge(existing.GetOrAddMode((DifficultyMode)m.mode), ms, mw, mc);
                    }
                }

                // Top-level bests not backed by any difficulty (pre-v3 data, hand edits,
                // a newer build's file) were earned before difficulties existed: Normal.
                // (Same rule as LevelRecord.FoldUnbackedIntoNormal, on the clamped values.)
                if (stars > modeStars || wave > modeWave || (completed && !modeCompleted))
                    Merge(existing.GetOrAddMode(DifficultyMode.Normal), stars, wave, completed);
            }
            for (int i = 0; i < result.Count; i++) result[i].RefreshTotals();
            return result;
        }

        private static void Merge(ModeRecord into, int stars, int wave, bool completed)
        {
            into.bestStars = Math.Max(into.bestStars, stars);
            into.bestWave = Math.Max(into.bestWave, wave);
            into.completed |= completed || stars > 0;
        }

        private static List<ModeWaveRecord> SanitizeEndless(List<ModeWaveRecord> source, int legacyBest)
        {
            var result = new List<ModeWaveRecord>(4);
            int best = 0;
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    ModeWaveRecord r = source[i];
                    if (r == null || !Difficulty.IsDefined(r.mode)) continue;
                    int wave = Clamp(r.bestWave, 0, MaxWave);
                    best = Math.Max(best, wave);
                    ModeWaveRecord existing = FindEndless(result, r.mode);
                    if (existing == null) result.Add(new ModeWaveRecord { mode = r.mode, bestWave = wave });
                    else existing.bestWave = Math.Max(existing.bestWave, wave);
                }
            }
            if (legacyBest > best)
            {
                ModeWaveRecord normal = FindEndless(result, (int)DifficultyMode.Normal);
                if (normal == null) result.Add(new ModeWaveRecord { mode = (int)DifficultyMode.Normal, bestWave = legacyBest });
                else normal.bestWave = Math.Max(normal.bestWave, legacyBest);
            }
            return result;
        }

        private static ModeWaveRecord FindEndless(List<ModeWaveRecord> list, int mode)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].mode == mode) return list[i];
            return null;
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
