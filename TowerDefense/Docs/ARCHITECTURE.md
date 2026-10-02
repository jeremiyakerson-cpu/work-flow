# Tower Defense (iOS, offline) — Architecture & Ownership

Kingdom Rush–style 2D tower defense for iPhone/iPad. Fully offline: no network
calls, no accounts, no ads SDKs, no analytics. Built with **Unity 6 (6000.0 LTS)**,
2D, uGUI, C# 9.

The original 11 scripts from the Google Drive "Tower defense" folder are the
foundation (`Assets/Scripts/*.cs`, setup notes in `Docs/SETUP_GUIDE.md`).

## Guiding decisions

1. **Zero-asset runnable.** The game must boot and be playable with *no*
   hand-made art, audio, prefabs or scene YAML. Sprites are generated
   procedurally, SFX are synthesized, prefabs are runtime "templates", content
   (towers/enemies/levels) is created in code via `ScriptableObject.CreateInstance`.
   Real art/audio can later replace any piece without code changes.
2. **No hand-written scenes.** The app bootstraps from code
   (`[RuntimeInitializeOnLoadMethod]`) inside a single empty scene that the
   build script creates. Avoids fragile hand-authored `.unity`/`.meta` YAML.
3. **Engine-free core.** Pure rules (damage, wave plans, economy, upgrade math,
   star ratings, save-data migration) live in `Assets/Scripts/Core`
   (`TowerDefense.Core` asmdef, `noEngineReferences: true`) and are unit-tested
   on plain .NET and in Unity's Test Runner.
4. **Data-driven.** `TowerData`, `EnemyData`, `LevelData`, `EnemyAbilityData`
   ScriptableObjects. Balance by editing data, not code.

## Runtime templates (no prefabs)

A runtime template is a fully built GameObject (sprite, collider, component)
parented under an **inactive** root `"[Templates]"`. The template itself stays
`activeSelf = true`, so `Instantiate(template)` yields an *active* clone at the
scene root and `Awake` never runs on the template. `TowerData.towerPrefab`,
`TowerData.projectilePrefab` and `EnemyData.prefab` point at these templates.

## Layers & physics

- Layer **8 = "Enemy"** (in `ProjectSettings/TagManager.asset`). Towers/heroes
  query `LayerMask.GetMask("Enemy")`.
- Enemies: `CircleCollider2D` + kinematic `Rigidbody2D` on layer Enemy.
- Build slots: `BoxCollider2D` (or Circle) on Default layer; input controller
  finds them with `Physics2D.OverlapPoint`.
- World units: play area spans (0,0) → `LevelData.worldSize` (default 32×18,
  landscape 16:9). Orthographic camera fits this to any iPhone/iPad aspect.

## Public API contract between workstreams

These members exist now and must keep their signatures (add freely, don't
break). Everything is in the global namespace for the original scripts; new
code uses `TowerDefense.<Area>` namespaces.

| Type | Contract |
|---|---|
| `GameManager` | `Instance`, `Gold`, `Lives`, `StartingLives`, `IsGameOver`, `IsVictory`, `IsPaused`, `GameSpeed`; `Configure(gold,lives)`, `AddGold`, `CanAfford`, `SpendGold`, `DamageBase`, `TriggerVictory`, `RestartGame`, `Pause/Resume/TogglePause`, `SetGameSpeed`; C# events `GoldChanged, LivesChanged, WaveStarted, WaveCleared, GameOverTriggered, VictoryTriggered, PausedChanged, SpeedChanged` (+ original UnityEvents) |
| `WaveManager` | `Instance`, `ApplyLevel(LevelData)`, `SetPaths(...)`, `Paths`, `StartWaves/StopWaves`, `autoStart`, `wavesToWin`, `CurrentWave`, `WaveInProgress`, `IsRunning`, `EnemiesAlive`, `TimeUntilNextWave`, `IsCountingDown`, `CallNextWaveEarly()`, `IsBossWave(n)`, `SpawnExtra(...)`, events `WaveSpawning(int,bool)`, `EnemySpawned(Enemy)` |
| `Enemy` | `Init(EnemyData, IReadOnlyList<Vector3> path, health, speed, reward, startWaypointIndex=0, startPosition=null)`, `Data`, `IsDead`, `IsBoss`, `CurrentHealth`, `maxHealth`, `HealthPercent()`, `PathProgress`, `Path`, `WaypointIndex`, `IsBlocked`, `IsFlying`, `SpeedMultiplier`, `Invulnerable`, `Heal`, `TakeDamage`, `ApplySlow/ApplyPoison`, `IsSlowed/IsPoisoned`, `TryGetBlocked/ReleaseFromBlock`; events `Damaged, Died, Leaked`, static `AnyDamaged, AnyDied, AnyLeaked` |
| `Tower` | `Init(TowerData)`, `data`, `Range/FireRate/Damage`, `targetPriority`, `upgradeLevel`, `chosenPath`, `CanUpgrade()`, `NextUpgradeCost()`, `BranchCost(path)`, `Upgrade()`, `ChooseBranch(path)`, `UpgradeFinal()`, `SellValue()`, `TotalInvested`, `ApplyHit(Enemy)`; events `Upgraded`, static `AnyFired, AnyUpgraded` |
| `TowerPlacement` | `TryBuild(TowerData)`, `TryBuild(TowerData, GameObject)`, `GetBuiltTower()`, `IsOccupied`, `Sell()`, `HandleTap()`; static events `SlotTapped, AnySlotChanged` |
| `HeroUnit` | `MoveTo(pos)` (walks), `PlaceAt(pos)` (instant), `UseAbility()`, `IsAbilityReady()`, `AbilityReadyPercent`, `RespawnPercent`, `HealthPercent()`, `IsDead`, `IsMoving`, `heroName`, `abilityName`; `protected virtual PerformAbility()`; events `Died, Respawned, AbilityUsed` |
| `Barricade` | `IBlockable`, `maxHealth`, `HealthPercent()` |
| `TowerData` | original fields + `id`, `description`, `towerPrefab`, `canTargetGround`, `canTargetFlying`, `splashRadius` |
| `EnemyData` | original fields + `id`, `description`, `tint`, `visualScale`, `abilities` (`List<EnemyAbilityData>`) |
| `EnemyAbilityData` / `EnemyAbilityBehaviour` | `Attach(Enemy)` adds a behaviour; `Bind(Enemy, data)`. Enemy destroys behaviours on despawn |
| `LevelData` | `id, displayName, description, worldSize, paths (List<PathDefinition>{points}), buildSlots, heroStart, startingGold, startingLives, wavesToWin (0=endless), difficultyMultiplier, enemyPool, bossPool, bossEveryNWaves, groundColor, pathColor, accentColor` |

## Folder ownership (parallel workstreams)

Each workstream edits **only** its own paths. Need something from another
area? Code against the contract above; if a member is missing, note it in your
final report rather than editing a file you don't own.

| Workstream | Owns |
|---|---|
| 1. Core & Combat | `Assets/Scripts/*.cs` (the original scripts), `Assets/Scripts/Core/**`, `Assets/Scripts/Pooling/**`, `Assets/Tests/EditMode/**` |
| 2. Levels, Content & Bosses | `Assets/Scripts/Levels/**`, `Assets/Scripts/Bosses/**`, `Assets/Scripts/Content/**`, `Assets/Editor/Content/**` |
| 3. UI & Input | `Assets/Scripts/UI/**`, `Assets/Scripts/Input/**` |
| 4. iOS Platform, Save & Audio | `ProjectSettings/**`, `Packages/**`, `Assets/Scripts/Platform/**`, `Assets/Scripts/Persistence/**`, `Assets/Scripts/Audio/**`, `Assets/Plugins/**`, `Assets/Editor/Build/**`, `.github/workflows/tower-defense.yml` (repo root) |
| 5. Visuals & Scene Assembly | `Assets/Scripts/Visuals/**`, `Assets/Scripts/Bootstrap/**` |
| Integration (after merge) | `Assets/Scripts/App/**` — app flow wiring all of the above |

Stub additions (`Tools/UnityStubs`): add missing Unity API members in a new
file `Tools/UnityStubs/<Area>.Stubs.cs` (or `Editor/<Area>.Stubs.cs`) using
`partial` types. Only real Unity 6 signatures. Never edit the shared stub files.

## Rules for all code

- C# 9 (Unity 6). No `record` structs, no file-scoped namespaces, no global usings.
- `Tools/check.sh` must pass before every commit (runtime + editor compile
  against stubs, core NUnit tests).
- No per-frame allocations in `Update` hot paths (no LINQ, no `new List` per
  frame, cache `LayerMask.GetMask`, prefer `OverlapCircle` with a
  `ContactFilter2D` + reused buffer where it matters).
- `UnityEngine.Random` vs `System.Random`: always qualify if both are in scope.
- Inside any `namespace TowerDefense.*`, `Input` resolves to the `TowerDefense.Input`
  namespace: write `UnityEngine.Input.GetTouch(...)`, never bare `Input.`.
- Input uses the legacy Input Manager ("Active Input Handling" = Old or Both).
- Pooled objects (enemies, projectiles) are reused: never hold an `Enemy` across
  frames without also storing and comparing its `SpawnId`.
- Respect `Time.timeScale` for gameplay; UI animations use unscaled time.
- iOS: landscape only, safe-area aware UI, 60 fps target, no network.
- Keep comment density/style of the original scripts: `/// <summary>` on
  public types and non-obvious members, short inline comments.
