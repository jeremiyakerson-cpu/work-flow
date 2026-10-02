using TowerDefense.Audio;
using TowerDefense.Persistence;
using TowerDefense.UI;

namespace TowerDefense.Platform
{
    /// <summary>
    /// Ready-made bridge from the UI's <see cref="IUiSettings"/> to the saved
    /// settings. Volume changes reach AudioManager and the haptics toggle
    /// reaches Haptics through the GameSettings change events.
    /// Usage: <c>model.settings = new UiSettingsAdapter(SaveRuntime.Service);</c>
    /// </summary>
    public sealed class UiSettingsAdapter : IUiSettings
    {
        private readonly SaveService save;

        public UiSettingsAdapter(SaveService save)
        {
            this.save = save ?? SaveRuntime.Service;
        }

        public float MusicVolume
        {
            get => save.Settings.GetMusicVolume();
            set => save.Settings.SetMusicVolume(value);
        }

        public float SfxVolume
        {
            get => save.Settings.GetSfxVolume();
            set => save.Settings.SetSfxVolume(value);
        }

        public bool HapticsEnabled
        {
            get => save.Settings.GetHapticsEnabled();
            set => save.Settings.SetHapticsEnabled(value);
        }

        /// <summary>Wipes stars, bests, stats and tutorials; keeps settings. Writes immediately.</summary>
        public void ResetProgress() => save.ResetProgress();

        /// <summary>Flush pending changes to disk (settings screen closed).</summary>
        public void Save() => save.Flush();
    }

    /// <summary>
    /// <see cref="IUiFeedback"/> implementation: click sound plus a light
    /// selection tick on every UI button. Usage: <c>UIRoot.Feedback = new UiFeedbackAdapter();</c>
    /// </summary>
    public sealed class UiFeedbackAdapter : IUiFeedback
    {
        public void OnButtonClick()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio != null) audio.PlayUiClick();
            Haptics.Play(HapticFeedback.Selection);
        }
    }
}
