using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.BuildTools
{
    /// <summary>
    /// Command-line / CI entry point for the iOS Xcode project:
    /// <code>
    /// Unity -batchmode -nographics -quit -projectPath TowerDefense -buildTarget iOS \
    ///       -executeMethod TowerDefense.BuildTools.BuildScript.BuildIOS \
    ///       -customBuildPath Builds/iOS [-buildNumber 42] [-developmentBuild]
    /// </code>
    /// Output path: -customBuildPath / -buildPath argument, else TD_IOS_BUILD_PATH, else Builds/iOS.
    /// Exits with code 0 on success and 1 on any failure (batch mode only).
    ///
    /// The game has a single empty scene; everything is created from code by
    /// [RuntimeInitializeOnLoadMethod] bootstraps. This script creates that scene
    /// (Assets/Scenes/Main.unity) if it does not exist yet and puts it first in
    /// the build settings.
    /// </summary>
    public static class BuildScript
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        public const string DefaultOutputPath = "Builds/iOS";

        [MenuItem("Tower Defense/iOS/Build Xcode Project", false, 20)]
        private static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildReport report = Build(ResolveOutputPath(), null, development: false);
            bool ok = report != null && report.summary.result == BuildResult.Succeeded;
            EditorUtility.DisplayDialog("iOS build", ok
                ? "Xcode project written to " + report.summary.outputPath
                : "Build failed - see the Console.", "OK");
        }

        [MenuItem("Tower Defense/iOS/Create Bootstrap Scene", false, 21)]
        private static void CreateSceneFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureBootstrapScene();
            Debug.Log("[Build] Bootstrap scene ready: " + MainScenePath);
        }

        /// <summary>CI entry (-executeMethod). Never returns normally in batch mode.</summary>
        public static void BuildIOS()
        {
            int exitCode = 1;
            try
            {
                string output = ResolveOutputPath();
                string buildNumber = GetArg("-buildNumber");
                bool development = HasArg("-developmentBuild") || IOSProjectSetup.Env("TD_DEVELOPMENT_BUILD") == "1";
                BuildReport report = Build(output, buildNumber, development);
                if (report != null && report.summary.result == BuildResult.Succeeded)
                {
                    Debug.Log("[Build] iOS Xcode project: " + report.summary.outputPath +
                              " (" + report.summary.totalTime + ", warnings: " + report.summary.totalWarnings + ")");
                    exitCode = 0;
                }
                else
                {
                    Debug.LogError("[Build] iOS build failed: " + (report != null ? report.summary.result.ToString() : "no report") +
                                   (report != null ? ", errors: " + report.summary.totalErrors : ""));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            else if (exitCode != 0) throw new UnityEditor.Build.BuildFailedException("iOS build failed");
        }

        /// <summary>Apply settings, make sure the scene exists, switch to iOS and build.</summary>
        public static BuildReport Build(string outputPath, string buildNumber, bool development)
        {
            if (string.IsNullOrEmpty(outputPath)) throw new ArgumentException("Output path required", nameof(outputPath));

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                // The Info.plist post-processor only compiles while iOS is the active
                // target (UNITY_IOS), and switching recompiles editor scripts after
                // this method returns. So switch now and ask for a second run.
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
                    throw new InvalidOperationException("Could not switch to the iOS build target. Is the iOS Build Support module installed?");
                throw new InvalidOperationException("Switched the active build target to iOS. Run the build again " +
                                                    "(command line: pass -buildTarget iOS so Unity starts on iOS).");
            }

            IOSProjectSetup.Apply(buildNumber);
            EnsureBootstrapScene();

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            Debug.Log("[Build] Building iOS to " + Path.GetFullPath(outputPath) + " with scenes: " + string.Join(", ", options.scenes));
            return BuildPipeline.BuildPlayer(options);
        }

        /// <summary>
        /// Create Assets/Scenes/Main.unity (an empty scene) if missing and make it
        /// the first enabled scene in Build Settings. Leaves other scenes in place.
        /// </summary>
        public static void EnsureBootstrapScene()
        {
            if (!File.Exists(MainScenePath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene, MainScenePath))
                    throw new IOException("Could not save " + MainScenePath);
                AssetDatabase.Refresh();
                Debug.Log("[Build] Created empty bootstrap scene " + MainScenePath);
            }

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(MainScenePath, true) };
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene s in existing)
                if (s != null && s.path != MainScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static string[] GetEnabledScenes()
        {
            var list = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s != null && s.enabled && File.Exists(s.path)) list.Add(s.path);
            return list.ToArray();
        }

        /// <summary>-customBuildPath (game-ci) or -buildPath, else TD_IOS_BUILD_PATH, else Builds/iOS.</summary>
        public static string ResolveOutputPath()
        {
            return IOSProjectSetup.FirstNonEmpty(GetArg("-customBuildPath"), GetArg("-buildPath"),
                                                 IOSProjectSetup.Env("TD_IOS_BUILD_PATH"), DefaultOutputPath);
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static bool HasArg(string name)
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
