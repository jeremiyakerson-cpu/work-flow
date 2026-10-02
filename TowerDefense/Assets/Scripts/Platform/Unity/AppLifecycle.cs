using System;
using TowerDefense.Persistence;
using UnityEngine;

namespace TowerDefense.Platform
{
    /// <summary>
    /// iOS app-lifecycle policy, on the persistent "[Platform]" object:
    ///  - 60 fps target, landscape-only autorotation, no running in background;
    ///  - backgrounding / focus loss (home swipe, call, Control Center): flush the
    ///    save and pause a running game. On return the game STAYS paused so the
    ///    player resumes deliberately from the pause menu;
    ///  - screen stays awake only while a game is actually running;
    ///  - edge swipes deferred only during gameplay (no accidental home swipes);
    ///  - low-memory warning: unload unused assets, collect, free haptic engines;
    ///  - gameplay haptics bound to whichever GameManager is current;
    ///  - the chosen game speed saved as Settings.LastGameSpeed (restoring it
    ///    when a level starts is up to the level flow).
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class AppLifecycle : MonoBehaviour
    {
        public const int TargetFrameRate = 60;

        public static AppLifecycle Instance { get; private set; }

        /// <summary>Raised when the app goes to background or loses focus (after the save flush).</summary>
        public static event Action Suspending;
        /// <summary>Raised when the app is active again. The game is still paused if it was running.</summary>
        public static event Action Resumed;

        /// <summary>True if the last suspension paused a running game (UI may show "Tap to resume").</summary>
        public bool PausedBySuspension { get; private set; }
        public bool IsInGameplay => inGameplay;

        private readonly GameplayHaptics gameplayHaptics = new GameplayHaptics();
        private GameManager speedSource;
        private bool inGameplay;
        private bool suspended;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            ApplyRuntimeSettings();
            Application.lowMemory += HandleLowMemory;
            ApplyGameplayState(false, force: true);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Application.lowMemory -= HandleLowMemory;
            gameplayHaptics.Unbind();
            TrackSpeed(null);
            Instance = null;
        }

        /// <summary>Frame rate, orientation and background behaviour. Safe to call again.</summary>
        public static void ApplyRuntimeSettings()
        {
            // iOS ignores vSyncCount; targetFrameRate drives CADisplayLink. 60 also on 120 Hz ProMotion
            // devices: plenty for this game and much kinder to battery and heat.
            Application.targetFrameRate = TargetFrameRate;
            Application.runInBackground = false;

            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        private void Update()
        {
            if (Instance != this) return; // duplicate pending destruction
            GameManager gm = GameManager.Instance;

            // Keep gameplay haptics bound to the current GameManager.
            if (gameplayHaptics.IsBound && gameplayHaptics.BoundGameManager == null) gameplayHaptics.Unbind();
            if (gm != null && (!gameplayHaptics.IsBound || !ReferenceEquals(gm, gameplayHaptics.BoundGameManager)))
                gameplayHaptics.BindToGameplay(gm, WaveManager.Instance);
            else if (gameplayHaptics.IsBound && !gameplayHaptics.HasWaveManager && WaveManager.Instance != null)
                gameplayHaptics.AttachWaveManager(WaveManager.Instance);

            if (!ReferenceEquals(gm, speedSource)) TrackSpeed(gm);

            bool running = gm != null && !gm.IsPaused && !gm.IsGameOver && !gm.IsVictory;
            if (running != inGameplay) ApplyGameplayState(running, force: false);
            if (running) PausedBySuspension = false;
        }

        private void ApplyGameplayState(bool running, bool force)
        {
            if (!force && running == inGameplay) return;
            inGameplay = running;
            // Never let the screen dim mid-wave; restore the user's auto-lock everywhere else.
            Screen.sleepTimeout = running ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
#if UNITY_IOS && !UNITY_EDITOR
            // First edge swipe only reveals the grabber while playing; menus behave normally.
            UnityEngine.iOS.Device.deferSystemGesturesMode = running
                ? UnityEngine.iOS.SystemGestureDeferMode.All
                : UnityEngine.iOS.SystemGestureDeferMode.None;
#endif
        }

        /// <summary>Remember the player's fast-forward choice (Settings.LastGameSpeed) whenever it changes.</summary>
        private void TrackSpeed(GameManager gm)
        {
            if (!ReferenceEquals(speedSource, null)) speedSource.SpeedChanged -= HandleSpeedChanged;
            speedSource = gm;
            if (gm != null) gm.SpeedChanged += HandleSpeedChanged;
        }

        private static void HandleSpeedChanged(float speed)
        {
            SaveRuntime.Service.Settings.SetLastGameSpeed(speed);
        }

        // iOS: pause(true) when the app is backgrounded; focus(false) also for
        // Control Center, Notification Center, incoming calls and Siri.
        private void OnApplicationPause(bool paused)
        {
            if (paused) Suspend();
            else Resume();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // In the Editor, clicking another window would pause the game every time.
            if (Application.isEditor) return;
            if (!hasFocus) Suspend();
            else Resume();
        }

        private void OnApplicationQuit()
        {
            SaveRuntime.Flush();
        }

        private void Suspend()
        {
            if (suspended) return;
            suspended = true;

            // Persist first: iOS may kill a backgrounded app without another callback.
            SaveRuntime.Flush();

            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.IsPaused && !gm.IsGameOver && !gm.IsVictory)
            {
                gm.Pause();
                PausedBySuspension = true;
            }
            ApplyGameplayState(false, force: false);
            Suspending?.Invoke();
        }

        private void Resume()
        {
            if (!suspended) return;
            suspended = false;
            // Deliberately do NOT resume the game: the player taps "Resume".
            Resumed?.Invoke();
        }

        private void HandleLowMemory()
        {
            Debug.LogWarning("[Platform] Low memory warning: unloading unused assets.");
            Resources.UnloadUnusedAssets();
            GC.Collect();
            Haptics.ReleaseNative();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Suspending = null;
            Resumed = null;
        }
    }
}
