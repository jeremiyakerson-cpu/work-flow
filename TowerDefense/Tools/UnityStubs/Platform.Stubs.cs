// Stub additions for workstream 4 (iOS platform, persistence, audio).
// Same rules as UnityEngine.Stubs.cs: partial types, real Unity 6 signatures only.
#pragma warning disable CS0067, CS1591
using System;

namespace UnityEngine
{
    public static partial class Time
    {
        public static double realtimeSinceStartupAsDouble => 0;
    }

    public static partial class Application
    {
        public static bool isBatchMode => false;
    }
}

namespace UnityEngine.iOS
{
    [Flags]
    public enum SystemGestureDeferMode { None = 0, TopEdge = 1, LeftEdge = 2, BottomEdge = 4, RightEdge = 8, All = 15 }

    public sealed partial class Device
    {
        public static SystemGestureDeferMode deferSystemGesturesMode { get; set; }
    }
}
