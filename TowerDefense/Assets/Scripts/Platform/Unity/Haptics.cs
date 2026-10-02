using TowerDefense.Persistence;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace TowerDefense.Platform
{
    /// <summary>
    /// Taptic feedback. On an iOS device it calls Assets/Plugins/iOS/TDHaptics.mm;
    /// everywhere else (Editor, other platforms) every call is a no-op.
    /// Gated by the "Haptics" setting and rate-limited (<see cref="HapticGate"/>).
    ///
    /// Usage: <c>Haptics.Play(HapticFeedback.Medium);</c>
    /// </summary>
    public static class Haptics
    {
        private static readonly HapticGate Gate = new HapticGate();
        private static GameSettings boundSettings;

        /// <summary>Mirrors the settings toggle (kept in sync by <see cref="BindSettings"/>).</summary>
        public static bool Enabled
        {
            get => Gate.Enabled;
            set => Gate.Enabled = value;
        }

        /// <summary>True when calls reach the native plugin (iOS device builds).</summary>
        public static bool IsAvailable
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Follow the saved "haptics on" toggle.</summary>
        public static void BindSettings(GameSettings settings)
        {
            if (boundSettings != null) boundSettings.HapticsEnabledChanged -= HandleSetting;
            boundSettings = settings;
            if (settings == null) return;
            settings.HapticsEnabledChanged += HandleSetting;
            Enabled = settings.GetHapticsEnabled();
        }

        private static void HandleSetting(bool on) => Enabled = on;

        /// <summary>Play one haptic if enabled and not rate-limited. Returns true if it was sent.</summary>
        public static bool Play(HapticFeedback style)
        {
            if (!Gate.TryFire(style, Time.realtimeSinceStartupAsDouble)) return false;
#if UNITY_IOS && !UNITY_EDITOR
            switch (style)
            {
                case HapticFeedback.Light: TDHaptics_Impact(0); break;
                case HapticFeedback.Medium: TDHaptics_Impact(1); break;
                case HapticFeedback.Heavy: TDHaptics_Impact(2); break;
                case HapticFeedback.Success: TDHaptics_Notification(0); break;
                case HapticFeedback.Warning: TDHaptics_Notification(1); break;
                case HapticFeedback.Error: TDHaptics_Notification(2); break;
                case HapticFeedback.Selection: TDHaptics_Selection(); break;
            }
            return true;
#else
            return false;
#endif
        }

        /// <summary>Warm up the Taptic Engine before gameplay to cut first-hit latency.</summary>
        public static void Prepare()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Enabled) TDHaptics_Prepare();
#endif
        }

        /// <summary>Free the native generators (low memory / background).</summary>
        public static void ReleaseNative()
        {
#if UNITY_IOS && !UNITY_EDITOR
            TDHaptics_Release();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (boundSettings != null) boundSettings.HapticsEnabledChanged -= HandleSetting;
            boundSettings = null;
            Gate.Reset();
            Gate.Enabled = true;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void TDHaptics_Impact(int style);
        [DllImport("__Internal")] private static extern void TDHaptics_Notification(int type);
        [DllImport("__Internal")] private static extern void TDHaptics_Selection();
        [DllImport("__Internal")] private static extern void TDHaptics_Prepare();
        [DllImport("__Internal")] private static extern void TDHaptics_Release();
#endif
    }
}
