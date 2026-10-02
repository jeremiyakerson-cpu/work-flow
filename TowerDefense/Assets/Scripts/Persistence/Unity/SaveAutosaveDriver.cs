using UnityEngine;

namespace TowerDefense.Persistence
{
    /// <summary>
    /// Drives the debounced autosave and counts kills for lifetime stats.
    /// Lives on the persistent "[Platform]" object created by PlatformBootstrap.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class SaveAutosaveDriver : MonoBehaviour
    {
        private void OnEnable()
        {
            Enemy.AnyDied += HandleEnemyDied;
        }

        private void OnDisable()
        {
            Enemy.AnyDied -= HandleEnemyDied;
        }

        private void Update()
        {
            if (SaveRuntime.IsInitialized) SaveRuntime.Service.Tick();
        }

        private void OnApplicationQuit()
        {
            SaveRuntime.Flush();
        }

        private static void HandleEnemyDied(Enemy e)
        {
            SaveRuntime.Service.AddEnemiesDefeated(1);
        }
    }
}
