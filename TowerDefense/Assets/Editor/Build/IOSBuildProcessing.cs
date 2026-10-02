using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using UnityEngine;

namespace TowerDefense.BuildTools
{
    /// <summary>
    /// Re-applies the iOS Player Settings before every iOS build, including
    /// builds started from File > Build Settings, so a forgotten menu click
    /// cannot ship portrait or Mono.
    /// </summary>
    public sealed class IOSBuildPreprocess : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            IOSProjectSetup.Apply(null, saveAssets: false);
        }
    }

#if UNITY_IOS
    // UnityEditor.iOS.Xcode exists only with iOS Build Support installed, and
    // UNITY_IOS is defined in the Editor while iOS is the active build target.
    // Always build with "-buildTarget iOS" (BuildScript warns otherwise).

    /// <summary>
    /// Finalizes the generated Xcode project's Info.plist for an offline,
    /// landscape-only, full-screen game and checks the privacy manifest.
    /// </summary>
    public static class IOSPostProcessBuild
    {
        /// <summary>Keys that must never ship: tracking, networking, permissions this game does not use.</summary>
        public static readonly string[] ForbiddenKeys =
        {
            "NSUserTrackingUsageDescription",   // ATT prompt
            "NSAppTransportSecurity",           // HTTP exceptions (we make no requests at all)
            "NSLocalNetworkUsageDescription",
            "NSBonjourServices",
            "NSLocationWhenInUseUsageDescription",
            "NSLocationAlwaysAndWhenInUseUsageDescription",
            "NSCameraUsageDescription",
            "NSMicrophoneUsageDescription",
            "NSPhotoLibraryUsageDescription",
            "UIBackgroundModes",                // no background audio/fetch/location
            "SKAdNetworkItems",                 // ad attribution
            "GADApplicationIdentifier",         // ad SDK
            "LSApplicationQueriesSchemes",
        };

        private static readonly string[] Landscape =
        {
            "UIInterfaceOrientationLandscapeLeft",
            "UIInterfaceOrientationLandscapeRight",
        };

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogError("[Build] Info.plist not found at " + plistPath);
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict root = plist.root;

            // Export compliance: no encryption beyond what iOS itself provides (no networking at all).
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            // Landscape only, iPhone and iPad.
            SetStringArray(root, "UISupportedInterfaceOrientations", Landscape);
            SetStringArray(root, "UISupportedInterfaceOrientations~ipad", Landscape);
            root.SetBoolean("UIRequiresFullScreen", true);

            // Status bar hidden everywhere, including the launch screen.
            root.SetBoolean("UIStatusBarHidden", true);
            root.SetBoolean("UIViewControllerBasedStatusBarAppearance", false);

            int removed = 0;
            foreach (string key in ForbiddenKeys)
                if (root.values.Remove(key)) removed++;
            if (removed > 0) Debug.Log("[Build] Removed " + removed + " tracking/network/permission keys from Info.plist.");

            plist.WriteToFile(plistPath);
            CheckPrivacyManifest(pathToBuiltProject);
            Debug.Log("[Build] Info.plist finalized: landscape-only, full screen, status bar hidden, ITSAppUsesNonExemptEncryption=false.");
        }

        private static void SetStringArray(PlistElementDict root, string key, string[] items)
        {
            root.values.Remove(key);
            PlistElementArray array = root.CreateArray(key);
            foreach (string item in items) array.AddString(item);
        }

        /// <summary>
        /// Unity (2022.3.18+ / Unity 6) merges every PrivacyInfo.xcprivacy found
        /// under Assets/Plugins into the UnityFramework privacy manifest. Warn
        /// loudly if the exported project has none, since App Store Connect
        /// rejects uploads without one.
        /// </summary>
        private static void CheckPrivacyManifest(string projectPath)
        {
            try
            {
                string[] manifests = Directory.GetFiles(projectPath, "PrivacyInfo.xcprivacy", SearchOption.AllDirectories);
                bool declaresNoTracking = false;
                foreach (string m in manifests)
                {
                    string text = File.ReadAllText(m);
                    if (text.Contains("NSPrivacyTracking")) declaresNoTracking = true;
                }
                if (manifests.Length == 0 || !declaresNoTracking)
                    Debug.LogWarning("[Build] No privacy manifest found in the Xcode project. Add Assets/Plugins/iOS/PrivacyInfo.xcprivacy " +
                                     "to the UnityFramework target (see Docs/IOS_BUILD.md) before uploading.");
                else
                    Debug.Log("[Build] Privacy manifest present (" + manifests.Length + " file(s)).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build] Could not verify the privacy manifest: " + e.Message);
            }
        }
    }
#endif
}
