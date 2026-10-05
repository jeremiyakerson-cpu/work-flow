# Tower Defense Core — Setup Guide

Eleven scripts: a data-driven, 2D, never-ending KR-style tower defense core.
Drop into `Assets/Scripts/` and wire up as below.

## Project setup (do this first)
1. New project with the **2D URP template**.
2. Package Manager: add **Cinemachine**, **TextMeshPro** (usually bundled),
   **Unity Splines** (optional, for curved paths). Add **DOTween** from the
   Asset Store for tweened animations.
3. Set up a **Sorting Layer** order: Background → Path → Towers → Enemies → UI.
4. Create an `"Enemy"` **Physics Layer** (Edit > Project Settings > Tags and
   Layers) - towers/heroes raycast against this specifically.

## Creating data assets (the new part)
Right-click in the Project window → **Create > Tower Defense > Tower Data**
or **Enemy Data**. One asset per tower/enemy type:

- **TowerData**: name, projectile prefab, base stats, on-hit effects, and
  every upgrade tier's multipliers (including both branch paths) — all
  editable in the Inspector, zero code changes to add a new tower.
- **EnemyData**: name, prefab reference, base stats, armor type, move type
  (ground/flying), and `unlockWave` (when it starts appearing). Mark
  `isBoss = true` and set the boss multipliers for boss-pool entries.

Then:
- Every **tower prefab** gets a `Tower.cs` + an assigned `TowerData` asset
  (or leave `data` empty on the prefab and assign it at spawn time via
  `TowerPlacement.TryBuild`, as the code now does).
- Every **enemy prefab** gets `Enemy.cs`, a `Collider2D` on layer `"Enemy"`,
  and gets referenced from an `EnemyData.prefab` field — WaveManager spawns
  enemies via `EnemyData`, not raw prefab lists anymore.

## Scene setup
1. **GameManager** → `GameManager.cs`.
2. **WaveManager** → `WaveManager.cs`. Assign `pathWaypoints`, `spawnPoint`,
   and drag your `EnemyData` assets into `enemyPool` / `bossPool`.
3. **Grid slots**: `TowerPlacement.cs` on each buildable spot, `Collider2D`
   (e.g. `BoxCollider2D`) required for tap/click detection.
4. **Hero prefab(s)**: `HeroUnit.cs` + `Rigidbody2D` (kinematic) + `Collider2D`.
5. **Barricade prefab**: `Barricade.cs` + a small trigger `Collider2D` sized
   to the path width.
6. **UI**: hook `GameManager` UnityEvents to HUD text. Tower shop buttons
   call `TowerShopUI.SelectTower(towerData, towerPrefab)`; upgrade buttons
   call `tower.Upgrade()` (levels 1→2→3) / `tower.ChooseBranch(path)` (specialization at level 3).

## Why ScriptableObjects matter here
Before: adding a tower meant a new prefab AND tuning fields scattered across
a `MonoBehaviour`. Now: `Tower.cs` is generic — it just reads whatever
`TowerData` asset is assigned. Same for `Enemy.cs`/`EnemyData`. Practically:
- Balance the whole game by editing assets, no recompiling.
- Non-programmers (or future rushed-you at 11pm) can add tower #7 by
  duplicating a `.asset` file and changing numbers.
- The KR branching-upgrade feel (`pathAName`/`pathBName`, separate stat
  multipliers per branch) is now literally visible and editable in one
  Inspector panel instead of buried in code.

## What makes this feel like Kingdom Rush (recap)
1. **Chokepoints** — `IBlockable` + `Enemy.TryGetBlocked()`: ground enemies
   stop and melee a Hero/Barricade instead of walking through.
2. **Armor triangle** — `ArmorType` × `DamageType` in `CombatEnums.cs`.
3. **Branching upgrades** — three linear levels, then `Tower.ChooseBranch()` specializes at level 3, data-defined.
4. **Respawning hero** — disables on death, returns after `respawnTime`.
5. **Status effects** — slow/poison, tactical layering on top of damage.

## Still to build (priority order)
- **Hand-authored multi-path levels** — the single biggest remaining gap.
  Endless mode can cycle a small set of hand-built maps while `WaveManager`
  keeps escalating the curve on top.
- **Boss move sets** — bosses are currently just a stat multiplier. Add an
  optional `EnemyAbility` component for charge attacks/summons/enrage.
- **Shop/upgrade UI** — wire real buttons to `TowerShopUI` and the tower
  upgrade methods; `TowerPlacement.OnMouseDown` is a placeholder flow.
- **Cooldown-bar UI pattern** shared by Hero abilities and future spells.

## The endless loop (unchanged)
`WaveManager.RunForever()` is a `while(true)` coroutine. Difficulty compounds
via `healthGrowthPerWave`, `enemyCountGrowthPerWave`, and periodic
`bossEveryNWaves` — tune those in the Inspector, not in code.
