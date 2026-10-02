using System.Collections.Generic;
using TowerDefense.Content;
using TowerDefense.Input;
using TowerDefense.Levels;
using TowerDefense.Persistence;
using TowerDefense.Platform;
using TowerDefense.UI;
using TowerDefense.Visuals;
using UnityEngine;

namespace TowerDefense.App
{
    /// <summary>
    /// The shipping game's entry point and screen flow:
    /// boot -> main menu -> campaign / endless level select -> level -> results -> ...
    /// Wires the workstreams together: ContentCatalog (data), RuntimeTemplates /
    /// MapView / GameplayFx (visuals), GameManager / WaveManager (combat),
    /// GameHud / MenuScreens / TouchInputController (UI), SaveRuntime (progress).
    /// Platform services (save load, lifecycle, audio, haptics) start themselves
    /// in PlatformBootstrap before the first scene loads.
    /// Every level is built from code under one "[Level]" root and fully torn
    /// down before the next, so no scene reloads are involved.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        private const string FirstLevelTutorialId = "first_level";
        private const float BarricadeCooldown = 18f;
        private const float MinZoomSize = 4f;

        public static GameApp Instance { get; private set; }

        private ContentCatalog catalog;
        private CampaignProgression progression;
        private SaveService save;
        private UiSettingsAdapter settings;
        private Camera cam;

        // Current level session
        private LevelData level;
        private GameObject levelRoot;
        private MapView map;
        private GameManager game;
        private WaveManager waves;
        private HeroUnit hero;
        private GameHud hud;
        private TouchInputController input;
        private bool resultShown;
        private bool endlessRecorded;
        private int lastEndlessWaves;

#if !TD_SANDBOX && !TD_NO_APP_AUTOBOOT
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot() => Boot();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>Create the app (idempotent). Runs automatically unless TD_SANDBOX or TD_NO_APP_AUTOBOOT is defined.</summary>
        public static GameApp Boot()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[GameApp]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameApp>();
            Instance.Initialize();
            return Instance;
        }

        private void Initialize()
        {
            PlatformBootstrap.Initialize();
            save = SaveRuntime.Service;
            settings = new UiSettingsAdapter(save);

            cam = CameraSetup.EnsureMainCamera();
            DontDestroyOnLoad(cam.gameObject);
            UIRoot.Create();

            catalog = ContentCatalog.Default;
            progression = catalog.CreateProgression(id => save.GetBestStars(id));

            RuntimeTemplates.Initialize();
            RuntimeTemplates.AssignTowerPrefabs(catalog.Towers);
            RuntimeTemplates.AssignEnemyPrefabs(catalog.AllEnemies);

            // Catch authoring mistakes (slot on a path, missing prefab...) loudly in development builds.
            if (Debug.isDebugBuild) LevelValidator.Log(catalog.ValidateLevels(requirePrefabs: true), "GameApp");

            ShowMainMenu();
        }

        private void OnDestroy()
        {
            TeardownLevel();
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ menus

        private void ShowMainMenu()
        {
            MenuScreens.CloseAll();
            MenuScreens.ShowMainMenu(new MainMenuCallbacks
            {
                title = "Kingdom Defense",
                subtitle = "Hold the line",
                footer = $"{progression.TotalStars()} / {progression.MaxStars} stars",
                onPlayCampaign = ShowCampaignSelect,
                onEndless = ShowEndlessSelect,
                // Rebuild the menu on close so the star total reflects a progress reset.
                onSettings = () => MenuScreens.ShowSettings(settings, ShowMainMenu),
                settings = settings,
            });
        }

        private void ShowCampaignSelect()
        {
            var entries = new List<LevelSelectEntry>();
            foreach (LevelData l in catalog.CampaignLevels)
            {
                entries.Add(new LevelSelectEntry
                {
                    id = l.id,
                    title = l.displayName,
                    subtitle = $"{l.wavesToWin} waves",
                    stars = save.GetBestStars(l.id),
                    locked = !progression.IsUnlocked(l.id),
                    userData = l,
                });
            }
            MenuScreens.ShowLevelSelect(entries, OnLevelPicked, ShowMainMenu, "Campaign");
        }

        private void ShowEndlessSelect()
        {
            var entries = new List<LevelSelectEntry>();
            foreach (LevelData l in catalog.EndlessLevels)
            {
                string baseId = ContentIds.BaseLevelId(l.id);
                LevelData baseLevel = catalog.GetLevel(baseId);
                bool locked = !progression.IsEndlessUnlocked(baseId);
                entries.Add(new LevelSelectEntry
                {
                    id = l.id,
                    title = baseLevel != null ? baseLevel.displayName : l.displayName,
                    subtitle = locked ? "Earn a star in the campaign to unlock" : "Survive as long as you can",
                    isEndless = true,
                    bestWave = save.GetBestWave(l.id),
                    locked = locked,
                    userData = l,
                });
            }
            MenuScreens.ShowLevelSelect(entries, OnLevelPicked, ShowMainMenu, "Endless");
        }

        private void OnLevelPicked(LevelSelectEntry entry)
        {
            if (entry.locked)
            {
                if (UIRoot.Instance != null) UIRoot.Instance.ShowToast(entry.isEndless ? "Earn a star on this map first" : "Finish the previous level first");
                return;
            }
            if (entry.userData is LevelData picked) StartLevel(picked);
        }

        // ------------------------------------------------------------------ level session

        private void StartLevel(LevelData data)
        {
            TeardownLevel();
            MenuScreens.CloseAll();
            level = data;
            resultShown = false;
            endlessRecorded = false;
            lastEndlessWaves = 0;

            levelRoot = new GameObject("[Level] " + data.id);
            Transform root = levelRoot.transform;

            map = MapView.Build(data, root);
            CameraSetup.Frame(cam, data.worldSize);

            // AddComponent runs Awake immediately (sets Instance); autoStart off so
            // waves begin only once everything below is in place.
            game = new GameObject("GameManager").AddComponent<GameManager>();
            game.transform.SetParent(root, false);
            waves = new GameObject("WaveManager").AddComponent<WaveManager>();
            waves.transform.SetParent(root, false);
            waves.autoStart = false;
            waves.ApplyLevel(data);
            game.Configure(data.startingGold, data.startingLives);
            GameplayFx.Bind(root); // after WaveManager exists, so FX attach to this level's waves

            RuntimeTemplates.SpawnSlots(data, root);
            hero = RuntimeTemplates.SpawnHero(data.heroStart, root);

            input = TouchInputController.Instance != null ? TouchInputController.Instance : TouchInputController.Create(cam);
            float fitSize = CameraSetup.OrthoSizeToFit(data.worldSize, cam.aspect);
            input.SetBounds(new Rect(Vector2.zero, data.worldSize), Mathf.Min(MinZoomSize, fitSize), fitSize);
            input.SetHero(hero);

            bool firstLevel = catalog.CampaignLevels.Count > 0 && catalog.CampaignLevels[0] == data;
            hud = GameHud.Bind(game, waves, hero, catalog.Towers, new HudOptions
            {
                input = input,
                settings = settings,
                spells = new[] { BarricadeSpell(root) },
                onRestart = () => StartLevel(data),
                onQuitToMenu = QuitToMenu,
                showTutorial = firstLevel && !save.IsTutorialSeen(FirstLevelTutorialId),
                onTutorialFinished = () => save.MarkTutorialSeen(FirstLevelTutorialId),
            });

            game.VictoryTriggered += OnVictory;
            game.GameOverTriggered += OnDefeat;
            game.SetGameSpeed(save.Settings.LastGameSpeed);
            waves.StartWaves();
        }

        private SpellDefinition BarricadeSpell(Transform root)
        {
            // Cast through RuntimeTemplates (template left null) so barricades live under the
            // level root and are destroyed with it.
            SpellDefinition spell = SpellDefinition.Barricade(null, BarricadeCooldown);
            spell.cast = position => RuntimeTemplates.SpawnBarricade(position, root);
            return spell;
        }

        private void OnVictory()
        {
            if (resultShown || game == null) return;
            resultShown = true;
            MarkTutorialSeenIfFirstLevel();
            int stars = game.Stars;
            LevelResultOutcome outcome = save.RecordLevelResult(level.id, stars, waves.CurrentWave);
            save.Flush();

            LevelData next = NextCampaignLevel(level);
            MenuScreens.ShowVictory(new ResultsData
            {
                victory = true,
                levelTitle = level.displayName,
                stars = stars,
                wave = waves.CurrentWave,
                wavesToWin = level.wavesToWin,
                livesLeft = game.Lives,
                startingLives = game.StartingLives,
                isNewBest = outcome.NewBestStars,
            }, new ResultsCallbacks
            {
                onNext = next != null && progression.IsUnlocked(next.id) ? () => StartLevel(next) : (System.Action)null,
                onRetry = () => StartLevel(level),
                onMenu = QuitToMenu,
            });
        }

        private void OnDefeat()
        {
            if (resultShown || game == null) return;
            resultShown = true;
            MarkTutorialSeenIfFirstLevel();
            bool endless = level.IsEndless;
            int previousBest = save.GetBestWave(level.id);
            int wave = endless ? RecordEndlessRun() : waves.CurrentWave;
            if (!endless) save.RecordLevelResult(level.id, 0, wave);
            save.Flush();

            MenuScreens.ShowDefeat(new ResultsData
            {
                victory = false,
                levelTitle = level.displayName,
                stars = 0,
                wave = wave,
                wavesToWin = level.wavesToWin,
                livesLeft = 0,
                startingLives = game.StartingLives,
                isEndless = endless,
                bestWave = Mathf.Max(previousBest, wave),
                isNewBest = endless && wave > previousBest,
            }, new ResultsCallbacks
            {
                onRetry = () => StartLevel(level),
                onMenu = QuitToMenu,
            });
        }

        /// <summary>
        /// Save an endless run once (defeat, restart or quit) and return waves survived:
        /// the wave in progress when the run ended doesn't count.
        /// </summary>
        private int RecordEndlessRun()
        {
            if (endlessRecorded || waves == null) return lastEndlessWaves;
            endlessRecorded = true;
            bool waveUnfinished = waves.WaveInProgress || waves.EnemiesAlive > 0;
            lastEndlessWaves = Mathf.Max(0, waveUnfinished ? waves.CurrentWave - 1 : waves.CurrentWave);
            save.RecordLevelResult(level.id, 0, lastEndlessWaves);
            save.RecordEndlessResult(lastEndlessWaves, countGame: false);
            return lastEndlessWaves;
        }

        private void MarkTutorialSeenIfFirstLevel()
        {
            if (catalog.CampaignLevels.Count > 0 && catalog.CampaignLevels[0] == level)
                save.MarkTutorialSeen(FirstLevelTutorialId);
        }

        private LevelData NextCampaignLevel(LevelData current)
        {
            if (current == null || current.IsEndless) return null;
            int index = catalog.CampaignIndexOf(current.id);
            return index >= 0 ? catalog.GetCampaignLevel(index + 1) : null;
        }

        private void QuitToMenu()
        {
            TeardownLevel();
            ShowMainMenu();
        }

        /// <summary>Destroy everything the current level created. Safe to call when no level is loaded.</summary>
        private void TeardownLevel()
        {
            // An endless run abandoned from the pause menu still counts toward the best wave.
            if (level != null && level.IsEndless && waves != null && waves.CurrentWave > 0) RecordEndlessRun();
            if (game != null)
            {
                game.VictoryTriggered -= OnVictory;
                game.GameOverTriggered -= OnDefeat;
            }
            if (hud != null) hud.Unbind();
            if (input != null) input.ResetLevelState();
            MenuScreens.CloseAll();
            GameplayFx.Unbind(); // before despawns, so clearing the field spawns no effects

            if (waves != null)
            {
                waves.StopWaves();
                waves.ClearEnemies();
            }
            // Projectiles are pooled at the scene root; recycle or destroy the live ones.
            foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                TowerDefense.Pooling.GameObjectPool.Despawn(p.gameObject);

            if (map != null) map.Clear();
            if (levelRoot != null) Destroy(levelRoot);

            levelRoot = null;
            map = null;
            game = null;
            waves = null;
            hero = null;
            hud = null;
            level = null;
            Time.timeScale = 1f;
            if (save != null) save.Flush();
        }
    }
}
