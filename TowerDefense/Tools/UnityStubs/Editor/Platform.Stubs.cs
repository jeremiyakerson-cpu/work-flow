// UnityEditor stub additions for workstream 4 (build tooling).
// Same rules as UnityEditor.Stubs.cs: partial types, real Unity 6 signatures only.
#pragma warning disable CS0067, CS1591
using UnityEngine.SceneManagement;

namespace UnityEditor
{
    public static partial class AssetDatabase
    {
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string assetPath) => null;
    }

    public enum iOSAppInBackgroundBehavior { Custom = -1, Suspend = 0, [System.Obsolete("Exit is no longer supported on iOS", true)] Exit = 1 }
    public enum InsecureHttpOption { NotAllowed = 0, DevelopmentOnly = 1, AlwaysAllowed = 2 }

    public static partial class PlayerSettings
    {
        public static bool muteOtherAudioSources { get; set; }
        public static InsecureHttpOption insecureHttpOption { get; set; }
        public static ScriptingImplementation GetScriptingBackend(UnityEditor.Build.NamedBuildTarget buildTarget) => default;
        public static int accelerometerFrequency { get; set; }
        public static bool useAnimatedAutorotation { get; set; }
        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget buildTarget, string identifier) { }
        public static string GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget buildTarget) => "";
        public static void SetScriptingBackend(UnityEditor.Build.NamedBuildTarget buildTarget, ScriptingImplementation backend) { }
        public static void SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget buildTarget, ManagedStrippingLevel level) { }
        public static void SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget buildTarget, ApiCompatibilityLevel value) { }
        public static void SetArchitecture(UnityEditor.Build.NamedBuildTarget buildTarget, int architecture) { }
        public static void SetUseDefaultGraphicsAPIs(BuildTarget platform, bool automatic) { }

        public static partial class iOS
        {
            public static iOSAppInBackgroundBehavior appInBackgroundBehavior { get; set; }
        }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene = 0, DefaultGameObjects = 1 }
    public enum NewSceneMode { Single = 0, Additive = 1 }
    public enum OpenSceneMode { Single = 0, Additive = 1, AdditiveWithoutLoading = 2 }

    // Real type: "public sealed class EditorSceneManager : SceneManager"; the shared SceneManager stub is static, so no base here.
    public static partial class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode = NewSceneMode.Single) => default;
        public static Scene OpenScene(string scenePath, OpenSceneMode mode = OpenSceneMode.Single) => default;
        public static bool SaveScene(Scene scene, string dstScenePath = "", bool saveAsCopy = false) => false;
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => false;
    }
}
