using System.Collections.Generic;
using TowerDefense.Visuals;
using UnityEngine;

namespace TowerDefense.Bootstrap
{
    /// <summary>
    /// Self-contained playable scene built entirely from code with
    /// <see cref="SandboxContent"/> (sandbox data, not the real catalog):
    /// camera, GameManager, WaveManager, map, build slots, hero, FX and a few
    /// pre-built towers, plus an <see cref="SandboxAutoPilot"/> that keeps
    /// building/upgrading so the whole loop is visible.
    /// <para>Never runs in the shipping game by itself. Start it with
    /// <see cref="Launch"/> (integration code or the editor menu
    /// "Tower Defense/Sandbox/Play Sandbox"), or define the scripting symbol
    /// TD_SANDBOX to auto-launch after the first scene loads.</para>
    /// </summary>
    public sealed class SandboxBootstrap : MonoBehaviour
    {
        /// <summary>The running sandbox, or null.</summary>
        public static SandboxBootstrap Current { get; private set; }

        public SandboxContent Content { get; private set; }
        public MapView Map { get; private set; }
        public GameManager Game { get; private set; }
        public WaveManager Waves { get; private set; }
        public HeroUnit Hero { get; private set; }
        public IReadOnlyList<TowerPlacement> Slots => slots;

        private readonly List<TowerPlacement> slots = new List<TowerPlacement>();
        private Camera createdCamera;

#if TD_SANDBOX
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoLaunch() => Launch();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        /// <summary>Build and start the sandbox (idempotent: returns the running one).</summary>
        public static SandboxBootstrap Launch()
        {
            if (Current != null) return Current;
            var root = new GameObject("[Sandbox]");
            var boot = root.AddComponent<SandboxBootstrap>();
            Current = boot;
            boot.Build();
            return boot;
        }

        /// <summary>Stop waves, unbind FX and destroy everything the sandbox created.</summary>
        public static void Teardown()
        {
            if (Current == null) return;
            SandboxBootstrap boot = Current;
            Current = null;
            boot.TeardownInternal();
        }

        /// <summary>Teardown followed by a fresh Launch (used after game over).</summary>
        public static SandboxBootstrap Restart()
        {
            Teardown();
            return Launch();
        }

        private void Build()
        {
            Content = SandboxContent.Create();
            LevelData level = Content.Level;

            Camera cam = Camera.main;
            if (cam == null) cam = createdCamera = CameraSetup.EnsureMainCamera();
            CameraSetup.Frame(cam, level.worldSize);

            RuntimeTemplates.Initialize();
            RuntimeTemplates.AssignTowerPrefabs(Content.Towers);
            RuntimeTemplates.AssignEnemyPrefabs(Content.AllEnemies);

            Map = MapView.Build(level, transform);
            GameplayFx.Bind(transform);

            // Managers: AddComponent runs Awake immediately (sets Instance). WaveManager.Start
            // runs next frame, so autoStart is switched off before it can fire.
            Game = new GameObject("GameManager").AddComponent<GameManager>();
            Game.transform.SetParent(transform, false);
            Waves = new GameObject("WaveManager").AddComponent<WaveManager>();
            Waves.transform.SetParent(transform, false);
            Waves.autoStart = false;
            Waves.delayBetweenWaves = 4f;
            Waves.spawnInterval = 0.7f;
            Waves.ApplyLevel(level);

            Transform slotRoot = VisualBuilder.Child(transform, "Slots", Vector3.zero);
            slots.AddRange(RuntimeTemplates.SpawnSlots(level, slotRoot));

            // Pre-build a few towers so there is action from the first wave.
            int prebuildCost = 0;
            foreach (var (slot, tower) in SandboxContent.PrebuiltTowers)
                if (slot < slots.Count && tower < Content.Towers.Count) prebuildCost += Content.Towers[tower].baseCost;
            Game.Configure(level.startingGold + prebuildCost, level.startingLives);
            foreach (var (slot, tower) in SandboxContent.PrebuiltTowers)
                if (slot < slots.Count && tower < Content.Towers.Count) slots[slot].TryBuild(Content.Towers[tower]);

            Hero = RuntimeTemplates.SpawnHero(level.heroStart, transform);
            Hero.heroName = "Sir Sandbox";

            gameObject.AddComponent<SandboxAutoPilot>().Setup(this);
            Waves.StartWaves();
        }

        private void TeardownInternal()
        {
            GameplayFx.Unbind(); // first, so nothing below spawns effects
            if (Waves != null)
            {
                Waves.StopWaves();
                Waves.ClearEnemies();
            }

            // Projectiles/barricades live at the scene root (pooled); destroy the active ones.
            foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
            foreach (var b in FindObjectsByType<Barricade>(FindObjectsSortMode.None)) Destroy(b.gameObject);

            Destroy(gameObject); // map, slots + towers, hero, managers, FX pool
            if (createdCamera != null) Destroy(createdCamera.gameObject);
            RuntimeTemplates.Dispose(); // also clears parked pool instances of the templates
            Content?.Destroy();
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
