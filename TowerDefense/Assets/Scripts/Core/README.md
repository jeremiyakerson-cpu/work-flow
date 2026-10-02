# TowerDefense.Core

Engine-free simulation logic (namespace `TowerDefense.Core`). The asmdef sets
`noEngineReferences: true`: nothing here may use `UnityEngine`. That keeps the
rules (damage, waves, economy, upgrades, ratings) unit-testable on plain .NET
via `Tools/check.sh`, and in Unity's Test Runner via `Assets/Tests/EditMode`.
MonoBehaviours in `Assets/Scripts` call into this layer; it never calls back.

| File | Rules |
|---|---|
| `CombatEnums.cs` | `DamageType`, `ArmorType`, `EnemyMoveType`, `TargetPriority` (global namespace) |
| `DamageTable.cs` | Armor triangle as a data table; `DamageTable.Default` is what `Enemy.TakeDamage` uses |
| `WavePlanner.cs` | Seeded, deterministic wave plans: curve scaling, counts, unlock gating, boss cadence, timings, paths |
| `TowerUpgradeMath.cs` | Tower stats / costs / investment at (level, branch) |
| `EconomyRules.cs` | Kill rewards, early-call bonus, sell refunds (whole gold, half-away-from-zero) |
| `StarRating.cs` | 0-3 stars from lives kept |
| `TargetSelector.cs` | Pure target choice per `TargetPriority` over `TargetCandidate` structs |
| `StatusEffects.cs` | `SlowEffect` / `PoisonEffect` stacking and ticking |
| `DeterministicRandom.cs` | SplitMix64; identical sequences on every platform |

Other asmdefs that use these types (including the global-namespace enums) must
reference `TowerDefense.Core`; `Assembly-CSharp` gets it automatically.
