namespace TowerDefense.UI
{
    /// <summary>
    /// Settings the UI edits. Implemented by the persistence/audio layer; the UI
    /// reads current values when the screen opens and writes on every change.
    /// </summary>
    public interface IUiSettings
    {
        /// <summary>0..1</summary>
        float MusicVolume { get; set; }
        /// <summary>0..1</summary>
        float SfxVolume { get; set; }
        bool HapticsEnabled { get; set; }
        /// <summary>Wipe campaign progress (called after the player confirms).</summary>
        void ResetProgress();
        /// <summary>Called when the settings screen closes (flush to disk).</summary>
        void Save();
    }

    /// <summary>Optional hook for click sounds / haptics on every UI button (set UIRoot.Feedback).</summary>
    public interface IUiFeedback
    {
        void OnButtonClick();
    }
}
