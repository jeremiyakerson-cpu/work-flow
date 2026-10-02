// Stub additions for workstream 4 (iOS platform, persistence, audio).
// Same rules as UnityEngine.Stubs.cs: partial types, real Unity 6 signatures only.
#pragma warning disable CS0067, CS1591
using System;

namespace UnityEngine
{
    public static partial class Time
    {
        public static double realtimeSinceStartupAsDouble => 0;
        public static double unscaledTimeAsDouble => 0;
    }

    public static partial class Application
    {
        public static bool isBatchMode => false;
    }

    public partial class AudioSource
    {
        public bool bypassEffects { get; set; }
        public bool bypassListenerEffects { get; set; }
        public bool bypassReverbZones { get; set; }
        public float panStereo { get; set; }
        public void PlayDelayed(float delay) { }
    }

    public partial class AudioClip
    {
        public bool LoadAudioData() => false;
    }

    public enum AudioSpeakerMode { Mono = 1, Stereo = 2, Quad = 3, Surround = 4, Mode5point1 = 5, Mode7point1 = 6, Prologic = 7 }

    public static partial class AudioSettings
    {
        public static int outputSampleRate => 0;
        public static AudioSpeakerMode speakerMode { get; set; }
        public static double dspTime => 0;
    }

    public static partial class QualitySettings
    {
        public static int vSyncCount { get; set; }
    }
}

namespace UnityEngine.iOS
{
    [Flags]
    public enum SystemGestureDeferMode { None = 0, TopEdge = 1, LeftEdge = 2, BottomEdge = 4, RightEdge = 8, All = 15 }

    public sealed partial class Device
    {
        public static SystemGestureDeferMode deferSystemGesturesMode { get; set; }
        public static bool hideHomeButton { get; set; }
        public static void SetNoBackupFlag(string path) { }
        public static void ResetNoBackupFlag(string path) { }
    }
}
