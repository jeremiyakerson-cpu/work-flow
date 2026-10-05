using System;
using TowerDefense.Core;

namespace TowerDefense.Persistence
{
    /// <summary>
    /// Settings adapter for UI and systems. Reads/writes the live
    /// <see cref="SettingsData"/> inside the current save, clamps input, raises
    /// a change event only when a value actually changes, and schedules a
    /// debounced autosave. UI defines its own settings interface; integration
    /// adapts it to these get/set methods and events.
    /// </summary>
    public sealed class GameSettings
    {
        private readonly Func<SettingsData> source;
        private readonly Action markDirty;

        public event Action<float> MusicVolumeChanged;
        public event Action<float> SfxVolumeChanged;
        public event Action<bool> HapticsEnabledChanged;
        public event Action<float> LastGameSpeedChanged;
        public event Action<DifficultyMode> LastDifficultyChanged;
        /// <summary>Any setting changed (also raised once after a load replaces all values).</summary>
        public event Action Changed;

        /// <param name="source">Returns the settings object to edit (the save may be reloaded, so it is fetched each time).</param>
        /// <param name="markDirty">Called after every change to schedule an autosave.</param>
        public GameSettings(Func<SettingsData> source, Action markDirty)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.markDirty = markDirty;
        }

        private SettingsData S => source() ?? new SettingsData();

        public float MusicVolume { get => GetMusicVolume(); set => SetMusicVolume(value); }
        public float SfxVolume { get => GetSfxVolume(); set => SetSfxVolume(value); }
        public bool HapticsEnabled { get => GetHapticsEnabled(); set => SetHapticsEnabled(value); }
        public float LastGameSpeed { get => GetLastGameSpeed(); set => SetLastGameSpeed(value); }
        public DifficultyMode LastDifficulty { get => GetLastDifficulty(); set => SetLastDifficulty(value); }

        /// <summary>Last difficulty picked (unknown saved values read as Normal).</summary>
        public DifficultyMode GetLastDifficulty() => Difficulty.FromInt(S.lastDifficulty);

        public void SetLastDifficulty(DifficultyMode value)
        {
            SettingsData s = S;
            if (!Difficulty.IsDefined(value) || (int)value == s.lastDifficulty) return;
            s.lastDifficulty = (int)value;
            LastDifficultyChanged?.Invoke(value);
            Commit();
        }

        /// <summary>Linear 0..1.</summary>
        public float GetMusicVolume() => S.musicVolume;
        public float GetSfxVolume() => S.sfxVolume;
        public bool GetHapticsEnabled() => S.hapticsEnabled;
        /// <summary>1..3 (x speed).</summary>
        public float GetLastGameSpeed() => S.lastGameSpeed;

        public void SetMusicVolume(float value)
        {
            SettingsData s = S;
            float v = SaveValidator.ClampFinite(value, 0f, 1f, s.musicVolume);
            if (v == s.musicVolume) return;
            s.musicVolume = v;
            MusicVolumeChanged?.Invoke(v);
            Commit();
        }

        public void SetSfxVolume(float value)
        {
            SettingsData s = S;
            float v = SaveValidator.ClampFinite(value, 0f, 1f, s.sfxVolume);
            if (v == s.sfxVolume) return;
            s.sfxVolume = v;
            SfxVolumeChanged?.Invoke(v);
            Commit();
        }

        public void SetHapticsEnabled(bool value)
        {
            SettingsData s = S;
            if (value == s.hapticsEnabled) return;
            s.hapticsEnabled = value;
            HapticsEnabledChanged?.Invoke(value);
            Commit();
        }

        public void SetLastGameSpeed(float value)
        {
            SettingsData s = S;
            float v = SaveValidator.ClampFinite(value, SaveValidator.MinGameSpeed, SaveValidator.MaxGameSpeed, s.lastGameSpeed);
            if (v == s.lastGameSpeed) return;
            s.lastGameSpeed = v;
            LastGameSpeedChanged?.Invoke(v);
            Commit();
        }

        /// <summary>Raise every change event with the current values (after a load).</summary>
        public void NotifyAllChanged()
        {
            SettingsData s = S;
            MusicVolumeChanged?.Invoke(s.musicVolume);
            SfxVolumeChanged?.Invoke(s.sfxVolume);
            HapticsEnabledChanged?.Invoke(s.hapticsEnabled);
            LastGameSpeedChanged?.Invoke(s.lastGameSpeed);
            LastDifficultyChanged?.Invoke(Difficulty.FromInt(s.lastDifficulty));
            Changed?.Invoke();
        }

        private void Commit()
        {
            Changed?.Invoke();
            markDirty?.Invoke();
        }
    }
}
