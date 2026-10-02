using System;
using System.Collections.Generic;

namespace TowerDefense.UI
{
    /// <summary>Main menu content and actions. Null actions hide their button.</summary>
    public sealed class MainMenuCallbacks
    {
        public string title = "Kingdom Defense";
        public string subtitle = "Hold the line";
        /// <summary>Small footer text (e.g. version).</summary>
        public string footer;
        public Action onPlayCampaign;
        public Action onEndless;
        /// <summary>Custom settings action; if null and <see cref="settings"/> is set, the built-in settings screen opens.</summary>
        public Action onSettings;
        public IUiSettings settings;
    }

    /// <summary>One card in the level select grid.</summary>
    public sealed class LevelSelectEntry
    {
        public string id;
        public string title;
        /// <summary>Optional second line (difficulty, theme...).</summary>
        public string subtitle;
        /// <summary>0..3 earned stars (campaign levels).</summary>
        public int stars;
        public bool locked;
        public bool isEndless;
        /// <summary>Best wave reached (endless entries).</summary>
        public int bestWave;
        /// <summary>Free slot for the caller (e.g. the LevelData).</summary>
        public object userData;
    }

    /// <summary>Pause menu actions. onResume is required; null restart/quit hide their buttons.</summary>
    public sealed class PauseMenuCallbacks
    {
        public Action onResume;
        public Action onRestart;
        public Action onQuitToMenu;
        public IUiSettings settings;
        /// <summary>Ask "are you sure?" before restart / quit.</summary>
        public bool confirmDestructive = true;
    }

    /// <summary>What the victory / defeat screen shows.</summary>
    public sealed class ResultsData
    {
        public bool victory;
        public string levelTitle;
        /// <summary>0..3, victory only.</summary>
        public int stars;
        /// <summary>Wave reached (defeat) or cleared (victory).</summary>
        public int wave;
        /// <summary>0 = endless.</summary>
        public int wavesToWin;
        public int livesLeft;
        public int startingLives;
        public bool isEndless;
        public int bestWave;
        public bool isNewBest;
        /// <summary>Optional extra rows, e.g. ("Gold earned", "1,240").</summary>
        public IReadOnlyList<KeyValuePair<string, string>> extraStats;
    }

    /// <summary>Results screen actions. Null hides the button (e.g. no next level).</summary>
    public sealed class ResultsCallbacks
    {
        public Action onNext;
        public Action onRetry;
        public Action onMenu;
    }
}
