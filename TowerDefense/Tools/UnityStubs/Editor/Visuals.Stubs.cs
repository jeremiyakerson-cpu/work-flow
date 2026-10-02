// Visuals workstream additions to the compile-only UnityEditor stubs.
// Real Unity 6 signatures only.
#pragma warning disable CS1591

namespace UnityEditor
{
    public partial class SessionState
    {
        public static void SetBool(string key, bool value) { }
        public static bool GetBool(string key, bool defaultValue) => defaultValue;
        public static void EraseBool(string key) { }
    }
}
