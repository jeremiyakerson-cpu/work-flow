using UnityEngine;

namespace TowerDefense.Persistence
{
    /// <summary>
    /// Unity entry point for saves. <see cref="Service"/> is created and loaded
    /// on first access (PlatformBootstrap touches it before the first scene),
    /// stored at Application.persistentDataPath/save.json - on iOS that is the
    /// app's Documents folder, included in iCloud/iTunes device backups.
    ///
    /// Usage: <c>SaveRuntime.Service.RecordLevelResult(level.id, stars, wave);</c>
    /// </summary>
    public static class SaveRuntime
    {
        private static SaveService service;

        /// <summary>The loaded save service (lazy, never null).</summary>
        public static SaveService Service
        {
            get
            {
                if (service == null) Initialize();
                return service;
            }
        }

        public static bool IsInitialized => service != null;

        /// <summary>Create and load the service. Safe to call more than once.</summary>
        public static SaveService Initialize()
        {
            if (service != null) return service;
            var store = new SaveFileStore(Application.persistentDataPath, new JsonUtilitySaveSerializer(),
                                          new PhysicalSaveFileSystem(), SaveFileStore.DefaultFileName,
                                          msg => Debug.LogWarning("[Save] " + msg));
            service = new SaveService(store, () => Time.realtimeSinceStartupAsDouble);
            SaveLoadResult r = service.Load();
            if (r.CorruptionDetected)
                Debug.LogWarning("[Save] Recovered from a corrupt save (source: " + r.Source + ").");
            return service;
        }

        /// <summary>Write pending changes now (app pause, quit, level end).</summary>
        public static void Flush()
        {
            if (service != null) service.Flush();
        }

        /// <summary>Tests / domain reload: drop the instance so the next access reloads from disk.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            service = null;
        }
    }
}
