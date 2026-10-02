using UnityEditor;

namespace TowerDefense.Bootstrap.Editor
{
    /// <summary>
    /// Editor entry point for the visuals sandbox: "Tower Defense/Sandbox/Play Sandbox"
    /// enters play mode in the open scene and calls <see cref="SandboxBootstrap.Launch"/>
    /// once play mode has started. Use it in an empty scene (the sandbox makes its
    /// own camera, managers and map).
    /// </summary>
    [InitializeOnLoad]
    public static class SandboxMenu
    {
        private const string PendingKey = "TowerDefense.Sandbox.LaunchOnPlay";

        static SandboxMenu()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tower Defense/Sandbox/Play Sandbox")]
        private static void PlaySandbox()
        {
            if (EditorApplication.isPlaying)
            {
                SandboxBootstrap.Launch();
                return;
            }
            // Survives the domain reload on entering play mode.
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tower Defense/Sandbox/Restart Sandbox")]
        private static void RestartSandbox()
        {
            if (EditorApplication.isPlaying) SandboxBootstrap.Restart();
        }

        [MenuItem("Tower Defense/Sandbox/Restart Sandbox", true)]
        private static bool CanRestartSandbox() => EditorApplication.isPlaying;

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                SandboxBootstrap.Launch();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(PendingKey, false);
            }
        }
    }
}
