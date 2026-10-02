using TowerDefense.Audio;
using TowerDefense.Persistence;
using TowerDefense.UI;
using UnityEngine;

namespace TowerDefense.Platform
{
    /// <summary>
    /// Starts the platform services before the first scene loads, so they exist
    /// no matter how the game scene is assembled:
    ///   1. loads the save (SaveRuntime),
    ///   2. creates "[Platform]" (DontDestroyOnLoad) with AppLifecycle + SaveAutosaveDriver,
    ///   3. binds Haptics and the AudioManager ("[Audio]") to the saved settings,
    ///   4. installs UiFeedbackAdapter as UIRoot.Feedback (click sound + tick) if none is set.
    /// AudioManager and the gameplay haptics then bind themselves to
    /// GameManager.Instance whenever one appears.
    ///
    /// Integration can call <see cref="Initialize"/> explicitly (idempotent).
    /// Define TD_NO_PLATFORM_AUTOBOOT to opt out of the automatic start.
    /// </summary>
    public static class PlatformBootstrap
    {
        public static GameObject Root { get; private set; }
        public static bool IsInitialized => Root != null;

#if !TD_NO_PLATFORM_AUTOBOOT
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize() => Initialize();
#endif

        /// <summary>Create the platform services once. Returns the persistent root object.</summary>
        public static GameObject Initialize()
        {
            if (Root != null) return Root;

            AppLifecycle.ApplyRuntimeSettings();
            SaveService save = SaveRuntime.Initialize();

            Root = new GameObject("[Platform]");
            Object.DontDestroyOnLoad(Root);
            Root.AddComponent<AppLifecycle>();
            Root.AddComponent<SaveAutosaveDriver>();

            Haptics.BindSettings(save.Settings);

            AudioManager audio = AudioManager.EnsureExists();
            audio.BindSettings(save.Settings);

            // Button click sound + selection haptic, unless integration installed its own.
            if (UIRoot.Feedback == null) UIRoot.Feedback = new UiFeedbackAdapter();
            return Root;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Root = null;
        }
    }
}
