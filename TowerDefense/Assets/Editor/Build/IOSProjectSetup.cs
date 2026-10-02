using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TowerDefense.BuildTools
{
    /// <summary>
    /// Applies every iOS Player Setting the game relies on through the API, so
    /// ProjectSettings.asset never has to be hand-edited. Run from
    /// "Tower Defense > iOS > Apply Project Settings"; BuildScript.BuildIOS and
    /// every iOS build (IOSBuildPreprocess) call it too.
    ///
    /// Identity is only filled in while it still has Unity's defaults, so a real
    /// bundle id / team id set in the Inspector is never overwritten. CI can
    /// override with environment variables:
    ///   TD_BUNDLE_ID, TD_VERSION, TD_BUILD_NUMBER (or GITHUB_RUN_NUMBER), TD_APPLE_TEAM_ID.
    /// </summary>
    public static class IOSProjectSetup
    {
        /// <summary>
        /// PLACEHOLDER bundle identifier. Replace with your own reverse-DNS id
        /// (must match the App ID in your Apple Developer account) here, in
        /// Player Settings, or via TD_BUNDLE_ID. See Docs/IOS_BUILD.md.
        /// </summary>
        public const string BundleId = "com.yourcompany.towerdefense";
        public const string CompanyName = "YourCompany";
        public const string ProductName = "Tower Defense";
        /// <summary>Marketing version (CFBundleShortVersionString).</summary>
        public const string Version = "1.0.0";
        public const string MinimumIOSVersion = "15.0";
        /// <summary>PlayerSettings.SetArchitecture value for iOS: 1 = ARM64.</summary>
        public const int ArchitectureArm64 = 1;

        private static readonly NamedBuildTarget IOS = NamedBuildTarget.iOS;

        [MenuItem("Tower Defense/iOS/Apply Project Settings", false, 1)]
        private static void ApplyFromMenu()
        {
            Apply(null);
            Debug.Log("[Build] iOS project settings applied. Bundle id: " + PlayerSettings.GetApplicationIdentifier(IOS));
        }

        /// <summary>
        /// Apply all iOS settings. <paramref name="buildNumber"/> overrides CFBundleVersion when not empty.
        /// <paramref name="saveAssets"/> writes ProjectSettings.asset (skip it while a build is running).
        /// </summary>
        public static void Apply(string buildNumber, bool saveAssets = true)
        {
            ApplyIdentity(buildNumber);
            ApplyPlatform();
            EnsureLegacyInputHandling();
            if (saveAssets) AssetDatabase.SaveAssets();
        }

        private static void ApplyIdentity(string buildNumber)
        {
            if (IsUnityDefault(PlayerSettings.companyName, "DefaultCompany")) PlayerSettings.companyName = CompanyName;
            if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == "TowerDefense")
                PlayerSettings.productName = ProductName;

            string envId = Env("TD_BUNDLE_ID");
            string currentId = PlayerSettings.GetApplicationIdentifier(IOS);
            if (!string.IsNullOrEmpty(envId)) PlayerSettings.SetApplicationIdentifier(IOS, envId);
            else if (IsDefaultBundleId(currentId)) PlayerSettings.SetApplicationIdentifier(IOS, BundleId);

            string envVersion = Env("TD_VERSION");
            if (!string.IsNullOrEmpty(envVersion)) PlayerSettings.bundleVersion = envVersion;
            else if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = Version;

            string number = FirstNonEmpty(buildNumber, Env("TD_BUILD_NUMBER"), Env("GITHUB_RUN_NUMBER"));
            if (!string.IsNullOrEmpty(number)) PlayerSettings.iOS.buildNumber = number;
            else if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0")
                PlayerSettings.iOS.buildNumber = "1";

            string team = Env("TD_APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        }

        private static void ApplyPlatform()
        {
            // Landscape only (both sides), auto-rotating between them.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = true;

            // Devices / OS.
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = MinimumIOSVersion;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.requiresFullScreen = true;     // no iPad Split View / Slide Over
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.hideHomeButton = false;       // keep the indicator; we defer gestures instead
            SetDeferSystemGestures();

            // Code generation.
            PlayerSettings.SetScriptingBackend(IOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(IOS, ArchitectureArm64);
            PlayerSettings.SetApiCompatibilityLevel(IOS, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(IOS, ManagedStrippingLevel.Medium); // Assets/Plugins/link.xml keeps game code
            PlayerSettings.stripEngineCode = true;

            // Graphics: Metal is the only iOS API in Unity 6; "automatic" selects it.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, true);

            // Runtime behaviour (60 fps and screen sleep are set at runtime by AppLifecycle).
            PlayerSettings.runInBackground = false;
            PlayerSettings.iOS.appInBackgroundBehavior = iOSAppInBackgroundBehavior.Suspend;
            PlayerSettings.muteOtherAudioSources = false; // Ambient session: mixes with the user's music, obeys the silent switch
            PlayerSettings.accelerometerFrequency = 0;    // unused: don't wake the sensor
            PlayerSettings.iOS.showActivityIndicatorOnLoading = iOSShowActivityIndicatorOnLoading.DontShow;

            // Offline: no persistent Wi-Fi, no plain-HTTP exception in Info.plist.
            PlayerSettings.iOS.requiresPersistentWiFi = false;
            PlayerSettings.iOS.allowHTTPDownload = false;
        }

        /// <summary>
        /// PlayerSettings.iOS.deferSystemGesturesMode = SystemGestureDeferMode.All.
        /// Set via reflection so this file compiles against both Unity and the
        /// offline API stubs (whose declared property type differs).
        /// </summary>
        private static void SetDeferSystemGestures()
        {
            try
            {
                PropertyInfo prop = typeof(PlayerSettings.iOS).GetProperty("deferSystemGesturesMode", BindingFlags.Public | BindingFlags.Static);
                if (prop != null && prop.PropertyType.IsEnum)
                    prop.SetValue(null, Enum.ToObject(prop.PropertyType, 15)); // SystemGestureDeferMode.All
                else
                    Debug.LogWarning("[Build] PlayerSettings.iOS.deferSystemGesturesMode not found; set 'Defer system gestures on edges' to All manually.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build] Could not set deferSystemGesturesMode: " + e.Message);
            }
        }

        /// <summary>ProjectSettings.asset "activeInputHandler": 0 = Input Manager (Old), 1 = Input System (New), 2 = Both.</summary>
        public const int InputHandlerOld = 0;
        public const int InputHandlerNew = 1;

        /// <summary>
        /// The game reads touches through the legacy Input Manager
        /// (UnityEngine.Input + StandaloneInputModule). If the project is set to
        /// "Input System Package (New)" only, switch it to Old: with the new-only
        /// setting every UnityEngine.Input call throws. "Old" and "Both" are kept.
        /// There is no public PlayerSettings API for this, so it goes through the
        /// serialized PlayerSettings object. Unity applies it after an Editor restart.
        /// </summary>
        public static void EnsureLegacyInputHandling()
        {
            try
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0 || assets[0] == null) return;
                var so = new SerializedObject(assets[0]);
                SerializedProperty prop = so.FindProperty("activeInputHandler");
                if (prop == null || prop.intValue != InputHandlerNew) return;
                prop.intValue = InputHandlerOld;
                so.ApplyModifiedProperties();
                Debug.LogWarning("[Build] Active Input Handling was 'Input System Package (New)'; set to 'Input Manager (Old)' " +
                                 "because the game uses UnityEngine.Input. Restart the Editor for it to take effect.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build] Could not check Active Input Handling: " + e.Message +
                                 ". Set Player Settings > Other > Active Input Handling to 'Input Manager (Old)' or 'Both'.");
            }
        }

        private static bool IsUnityDefault(string value, string unityDefault) =>
            string.IsNullOrEmpty(value) || value == unityDefault;

        /// <summary>Empty, Unity's "com.DefaultCompany.X" / "com.Company.ProductName", or our placeholder.</summary>
        public static bool IsDefaultBundleId(string id)
        {
            return string.IsNullOrEmpty(id)
                   || id.StartsWith("com.DefaultCompany.", StringComparison.Ordinal)
                   || id == "com.Company.ProductName"
                   || id == BundleId;
        }

        internal static string Env(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        internal static string FirstNonEmpty(params string[] values)
        {
            foreach (string v in values) if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            return null;
        }
    }
}
