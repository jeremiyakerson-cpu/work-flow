// Compile-only stand-ins for the UnityEditor API surface used by Assets/**/Editor.
// Same rules as UnityEngine.Stubs.cs: partial types, real Unity 6 signatures only.
#pragma warning disable CS0067, CS1591
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)] public partial class MenuItem : Attribute { public MenuItem(string itemName) { } public MenuItem(string itemName, bool isValidateFunction) { } public MenuItem(string itemName, bool isValidateFunction, int priority) { } }
    [AttributeUsage(AttributeTargets.Class)] public partial class CustomEditor : Attribute { public CustomEditor(Type inspectedType) { } public CustomEditor(Type inspectedType, bool editorForChildClasses) { } }
    [AttributeUsage(AttributeTargets.Class)] public partial class CanEditMultipleObjects : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public partial class InitializeOnLoadAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public partial class InitializeOnLoadMethodAttribute : Attribute { }
    public partial class Editor : ScriptableObject { public Object target { get; set; } public Object[] targets => null; public SerializedObject serializedObject => null; public virtual void OnInspectorGUI() { } public bool DrawDefaultInspector() => false; public void Repaint() { } }
    public partial class EditorWindow : ScriptableObject { public static T GetWindow<T>() where T : EditorWindow => null; public static T GetWindow<T>(string title) where T : EditorWindow => null; public void Show() { } public void Close() { } public void Repaint() { } public GUIContent titleContent { get; set; } }
    public partial class SerializedObject { public SerializedObject(Object obj) { } public SerializedProperty FindProperty(string propertyPath) => null; public bool ApplyModifiedProperties() => false; public void Update() { } }
    public partial class SerializedProperty { public int intValue { get; set; } public float floatValue { get; set; } public string stringValue { get; set; } public bool boolValue { get; set; } public Object objectReferenceValue { get; set; } public int arraySize { get; set; } public SerializedProperty GetArrayElementAtIndex(int index) => null; }

    public static partial class AssetDatabase
    {
        public static void CreateAsset(Object asset, string path) { } public static void AddObjectToAsset(Object objectToAdd, Object assetObject) { }
        public static void SaveAssets() { } public static void Refresh() { } public static void ImportAsset(string path) { }
        public static T LoadAssetAtPath<T>(string assetPath) where T : Object => null; public static Object LoadAssetAtPath(string assetPath, Type type) => null;
        public static string[] FindAssets(string filter) => null; public static string[] FindAssets(string filter, string[] searchInFolders) => null;
        public static string GUIDToAssetPath(string guid) => ""; public static string AssetPathToGUID(string path) => ""; public static string GetAssetPath(Object assetObject) => "";
        public static bool IsValidFolder(string path) => false; public static string CreateFolder(string parentFolder, string newFolderName) => "";
        public static bool DeleteAsset(string path) => false; public static bool CopyAsset(string path, string newPath) => false; public static string MoveAsset(string oldPath, string newPath) => "";
        public static void StartAssetEditing() { } public static void StopAssetEditing() { }
        public static string GenerateUniqueAssetPath(string path) => "";
    }
    public static partial class EditorUtility
    {
        public static void SetDirty(Object target) { } public static bool DisplayDialog(string title, string message, string ok) => false; public static bool DisplayDialog(string title, string message, string ok, string cancel) => false;
        public static void DisplayProgressBar(string title, string info, float progress) { } public static void ClearProgressBar() { } public static void FocusProjectWindow() { }
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) => ""; public static string OpenFolderPanel(string title, string folder, string defaultName) => "";
    }
    public static partial class EditorGUILayout
    {
        public static void LabelField(string label) { } public static void LabelField(string label, string label2) { } public static void HelpBox(string message, MessageType type) { } public static void Space() { }
        public static int IntField(string label, int value) => 0; public static float FloatField(string label, float value) => 0; public static string TextField(string label, string text) => ""; public static bool Toggle(string label, bool value) => false;
        public static Object ObjectField(string label, Object obj, Type objType, bool allowSceneObjects) => null; public static bool PropertyField(SerializedProperty property) => false;
        public static void BeginHorizontal() { } public static void EndHorizontal() { } public static void BeginVertical() { } public static void EndVertical() { }
    }
    public enum MessageType { None, Info, Warning, Error }
    public static partial class Selection { public static Object activeObject { get; set; } public static GameObject activeGameObject { get; set; } public static Object[] objects { get; set; } }
    public static partial class EditorApplication { public static bool isPlaying { get; set; } public static void Exit(int returnValue) { } public static event Action update; public static event Action<PlayModeStateChange> playModeStateChanged; }
    public enum PlayModeStateChange { EnteredEditMode, ExitingEditMode, EnteredPlayMode, ExitingPlayMode }
    public partial class EditorBuildSettingsScene { public EditorBuildSettingsScene(string path, bool enable) { } public string path { get; set; } public bool enabled { get; set; } }
    public static partial class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes { get; set; } }

    public enum BuildTarget { StandaloneOSX = 2, StandaloneWindows = 5, iOS = 9, Android = 13, StandaloneWindows64 = 19, WebGL = 20, StandaloneLinux64 = 24 }
    public enum BuildTargetGroup { Unknown = 0, Standalone = 1, iOS = 4, Android = 7, WebGL = 13 }
    [Flags] public enum BuildOptions { None = 0, Development = 1, AutoRunPlayer = 4, ShowBuiltPlayer = 8, AllowDebugging = 512, CompressWithLz4 = 0x40000, CompressWithLz4HC = 0x80000, StrictMode = 0x200000, SymlinkSources = 0x100 }
    public partial class BuildPlayerOptions { public string[] scenes { get; set; } public string locationPathName { get; set; } public BuildTarget target { get; set; } public BuildTargetGroup targetGroup { get; set; } public BuildOptions options { get; set; } }
    public static partial class BuildPipeline { public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions buildPlayerOptions) => null; public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(string[] levels, string locationPathName, BuildTarget target, BuildOptions options) => null; }
    public static partial class EditorUserBuildSettings { public static BuildTarget activeBuildTarget => default; public static bool SwitchActiveBuildTarget(BuildTargetGroup targetGroup, BuildTarget target) => false; public static bool development { get; set; } }

    public enum UIOrientation { Portrait = 0, PortraitUpsideDown = 1, LandscapeRight = 2, LandscapeLeft = 3, AutoRotation = 4 }
    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1 }
    public enum ApiCompatibilityLevel { NET_Standard_2_0 = 6, NET_Unity_4_8 = 3, NET_Standard = 6 }
    public enum ManagedStrippingLevel { Disabled = 0, Low = 1, Medium = 2, High = 3, Minimal = 4 }
    public enum iOSTargetDevice { iPhoneOnly = 0, iPadOnly = 1, iPhoneAndiPad = 2 }
    public enum iOSSdkVersion { DeviceSDK = 988, SimulatorSDK = 989 }
    public enum iOSStatusBarStyle { Default = 0, LightContent = 1, DarkContent = 2 }
    public enum iOSShowActivityIndicatorOnLoading { DontShow = -1 }
    public static partial class PlayerSettings
    {
        public static string companyName { get; set; } public static string productName { get; set; } public static string bundleVersion { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; } public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; } public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static bool statusBarHidden { get; set; } public static bool runInBackground { get; set; }
        public static void SetApplicationIdentifier(BuildTargetGroup targetGroup, string identifier) { } public static string GetApplicationIdentifier(BuildTargetGroup targetGroup) => "";
        public static void SetScriptingBackend(BuildTargetGroup targetGroup, ScriptingImplementation backend) { }
        public static void SetApiCompatibilityLevel(BuildTargetGroup buildTargetGroup, ApiCompatibilityLevel value) { }
        public static void SetManagedStrippingLevel(BuildTargetGroup targetGroup, ManagedStrippingLevel level) { }
        public static void SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget buildTarget, string defines) { } public static string GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget buildTarget) => "";
        public static bool stripEngineCode { get; set; }
        public static partial class iOS
        {
            public static string buildNumber { get; set; } public static string targetOSVersionString { get; set; } public static iOSTargetDevice targetDevice { get; set; }
            public static iOSSdkVersion sdkVersion { get; set; } public static bool requiresFullScreen { get; set; } public static bool hideHomeButton { get; set; }
            public static iOSStatusBarStyle statusBarStyle { get; set; } public static bool requiresPersistentWiFi { get; set; } public static string appleDeveloperTeamID { get; set; }
            public static bool appleEnableAutomaticSigning { get; set; } public static string cameraUsageDescription { get; set; } public static string locationUsageDescription { get; set; } public static string microphoneUsageDescription { get; set; }
            public static bool forceHardShadowsOnMetal { get; set; } public static bool allowHTTPDownload { get; set; } public static bool disableDepthAndStencilBuffers { get; set; }
            public static iOSShowActivityIndicatorOnLoading showActivityIndicatorOnLoading { get; set; } public static bool deferSystemGesturesMode { get; set; }
        }
    }
}

namespace UnityEditor.Build
{
    public partial struct NamedBuildTarget { public static readonly NamedBuildTarget iOS; public static readonly NamedBuildTarget Standalone; public string TargetName => ""; public static NamedBuildTarget FromBuildTargetGroup(BuildTargetGroup buildTargetGroup) => default; }
    public interface IOrderedCallback { int callbackOrder { get; } }
    public interface IPreprocessBuildWithReport : IOrderedCallback { void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report); }
    public interface IPostprocessBuildWithReport : IOrderedCallback { void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report); }
    public partial class BuildFailedException : Exception { public BuildFailedException(string message) : base(message) { } }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown = 0, Succeeded = 1, Failed = 2, Cancelled = 3 }
    public partial class BuildReport { public BuildSummary summary => default; }
    public partial struct BuildSummary { public BuildResult result => default; public BuildTarget platform => default; public string outputPath => ""; public ulong totalSize => 0; public int totalErrors => 0; public int totalWarnings => 0; public TimeSpan totalTime => default; }
}

namespace UnityEditor.Callbacks
{
    [AttributeUsage(AttributeTargets.Method)] public partial class PostProcessBuildAttribute : Attribute { public PostProcessBuildAttribute() { } public PostProcessBuildAttribute(int callbackOrder) { } }
    [AttributeUsage(AttributeTargets.Method)] public partial class DidReloadScripts : Attribute { public DidReloadScripts() { } public DidReloadScripts(int callbackOrder) { } }
}

namespace UnityEditor.iOS.Xcode
{
    public partial class PBXProject
    {
        public static string GetPBXProjectPath(string buildPath) => "";
        public void ReadFromFile(string path) { } public void ReadFromString(string src) { } public void WriteToFile(string path) { } public string WriteToString() => "";
        public string GetUnityMainTargetGuid() => ""; public string GetUnityFrameworkTargetGuid() => ""; public string ProjectGuid() => "";
        public void AddFrameworkToProject(string targetGuid, string framework, bool weak) { }
        public void SetBuildProperty(string targetGuid, string name, string value) { } public void AddBuildProperty(string targetGuid, string name, string value) { }
        public string AddFile(string path, string projectPath) => ""; public void AddFileToBuild(string targetGuid, string fileGuid) { }
        public string AddFile(string path, string projectPath, PBXSourceTree sourceTree) => "";
        public void AddCapability(string targetGuid, PBXCapabilityType capability) { }
    }
    public enum PBXSourceTree { Absolute, Group, Source, Build, Developer, Sdk }
    public partial class PBXCapabilityType { public static readonly PBXCapabilityType GameCenter; public static readonly PBXCapabilityType InAppPurchase; public static readonly PBXCapabilityType PushNotifications; public static readonly PBXCapabilityType BackgroundModes; }
    public partial class PlistDocument { public PlistElementDict root { get; set; } public void ReadFromFile(string path) { } public void ReadFromString(string text) { } public void WriteToFile(string path) { } public string WriteToString() => ""; }
    public partial class PlistElement { public string AsString() => ""; public bool AsBoolean() => false; public int AsInteger() => 0; public PlistElementArray AsArray() => null; public PlistElementDict AsDict() => null; }
    public partial class PlistElementDict : PlistElement
    {
        public PlistElement this[string key] { get => null; set { } } public IDictionary<string, PlistElement> values => null;
        public void SetString(string key, string val) { } public void SetBoolean(string key, bool val) { } public void SetInteger(string key, int val) { } public void SetReal(string key, float val) { }
        public PlistElementArray CreateArray(string key) => null; public PlistElementDict CreateDict(string key) => null; public void Remove(string key) { }
    }
    public partial class PlistElementArray : PlistElement { public List<PlistElement> values { get; set; } public void AddString(string val) { } public void AddBoolean(bool val) { } public void AddInteger(int val) { } public PlistElementDict AddDict() => null; public PlistElementArray AddArray() => null; }
    public partial class ProjectCapabilityManager { public ProjectCapabilityManager(string pbxProjectPath, string entitlementFilePath, string targetName = null, string targetGuid = null) { } public void WriteToFile() { } public void AddGameCenter() { } }
}

namespace UnityEngine
{
    public partial class GUIContent { public GUIContent() { } public GUIContent(string text) { } public string text { get; set; } }
}
