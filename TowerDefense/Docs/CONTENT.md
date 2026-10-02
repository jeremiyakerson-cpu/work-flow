# Content & Balance (workstream 2)

Everything here is built in code by `Content/DefaultContent.cs` (no `.asset`
files needed) and exposed through `TowerDefense.Content.ContentCatalog.Default`.
`Tower Defense/Content/Bake Default Content to Assets` writes the same objects
to `Assets/Content/Generated/` for Inspector tuning; load them with
`ContentCatalog.FromAsset(asset)` + `ContentCatalog.SetDefault(...)`.

## How WaveManager scales the numbers

- Wave curve health `h(w) = 10 × difficulty × 1.12^(w−1)`: 10 (w1), 15.7 (w5),
  27.7 (w10), 48.9 (w15), 86.1 (w20). Enemy count `6 + (w−1)`.
- An enemy gets `h(w) × baseHealth / 10`, speed `curve × baseSpeed / 2`,
  gold `curve × baseGoldReward / 5`. So `baseHealth` is "how many grunts
  of health" ×10, and 2.0 speed / 5 gold are the grunt baseline.
- Bosses get `h(w) × bossHealthMultiplier`, `0.6 ×` curve speed and
  `reward × bossGoldMultiplier` (their `baseHealth`/`baseSpeed` are unused).
- Armor: physical ×0.85 vs Light, ×0.55 vs Heavy; magic and poison ×1.25 vs Heavy.

## Towers

DPS is single-target, unupgraded → fully upgraded (branch A / B). Branch A
always raises damage+range, branch B fire rate (Tower.cs rules).

| id | Name | Type | Cost | Range | Rate | Dmg | DPS L1 → A4 / B4 | Branch A | Branch B | Role |
|---|---|---|---|---|---|---|---|---|---|---|
| archer | Archer Tower | Physical | 70 | 4.5 | 1.4 | 4 | 5.6 → 28 / 24 | Marksmen | Volley Rangers | Cheap, fast, hits air; poor vs Heavy |
| mage | Mage Tower | Magic | 100 | 4.0 | 0.6 | 11 | 6.6 → 34 / 31 | Archmage | Arcane Barrage | The Heavy-armor answer (×1.25) |
| artillery | Artillery | Physical, splash 1.5 | 120 | 4.2 | 0.4 | 14 | 5.6/target → 29 / 24 | Big Bertha | Mortar Battery | Packs; **ground only** |
| frost | Frost Spire | Magic, slow 0.55×1.6s | 90 | 3.8 | 1.0 | 2.5 | 2.5 → 15 / 12 | Glacier Spire | Blizzard | Force multiplier for every other tower |
| alchemist | Alchemist Lab | Poison, splash 1.0, 4 dps × 4s | 100 | 4.0 | 0.8 | 3 | 2.4 + 4 poison → 11.5 / 9.8 + 4 | Plague Doctor | Acid Rain (adds 0.75× slow) | Ignores armor, stops regeneration |

Upgrade costs (L2 / A / B / L4): archer 50/110/100/160, mage 70/150/140/220,
artillery 80/170/160/250, frost 60/120/120/180, alchemist 70/140/130/200.
A fully upgraded tower costs ~4–5× its base price, so a level's gold buys
a few maxed towers plus a spread of level-1/2 ones.

Design intent: every enemy archetype has a "right answer" and a "wrong
answer" tower. Archers are gold-efficient on swarms but hopeless on knights;
mages are the opposite. Artillery is the best pack-clearer but bats, wyverns
and the drake fly over its shells. Frost does little damage itself but every
slowed enemy spends longer in range of everything else. Poison ignores armor
and resets regeneration, so it is the troll counter.

Visual hints: `ContentVisualHints.TowerTint(tower)` (TowerData has no tint
field), `WantsProjectile(tower)` (artillery/alchemist need one for the
splash to land on impact; mage/archer read better with one).

## Enemies

| id | Name | HP× | Speed | Gold | Armor | Move | Unlock wave | Leak | Ability |
|---|---|---|---|---|---|---|---|---|---|
| grunt | Goblin | 1.0 | 2.0 | 5 | None | Ground | 1 | 1 | — |
| runner | Worg Rider | 0.7 | 3.4 | 5 | None | Ground | 2 | 1 | — |
| bat | Gloom Bat | 0.8 | 2.8 | 6 | None | Flying | 3 | 1 | — |
| brute | Orc Brute | 2.2 | 1.7 | 9 | Light | Ground | 4 | 2 | Berserk: <30% HP → 1.3× speed, 1.5× melee |
| shaman | Goblin Shaman | 1.2 | 1.9 | 10 | None | Ground | 5 | 1 | Healing Totem: 8% max HP to allies in 2.5u every 2.5s (not bosses) |
| knight | Dark Knight | 3.0 | 1.5 | 12 | Heavy | Ground | 6 | 2 | — |
| splitter | Bog Ooze | 1.8 | 1.6 | 8 | None | Ground | 7 | 1 | Burst: 3 slimelings at 30% of its max HP each |
| warder | Cultist Warder | 1.6 | 1.8 | 11 | Light | Ground | 8 | 1 | Dark Ward: immune 1.5s every 7s |
| troll | Cave Troll | 3.5 | 1.4 | 14 | Light | Ground | 9 | 2 | Troll Blood: 4%/s after 2s without damage |
| wyvern | Wyvern | 2.4 | 2.2 | 12 | Light | Flying | 10 | 2 | — |

Minions (never in wave pools, only spawned by abilities; they still need a
prefab): slimeling (0.5 HP×, 2.4 speed), skeleton (0.9, Light), spiderling
(0.4, 3.0 speed). Summoned minions pay half gold so summoners can't be farmed.

Unlock waves introduce one new threat roughly every wave from 2 to 10, so the
tutorial (10 waves, pool of 4) sees grunts → runners → bats → brutes, and
later maps layer healers, armor, splitters, shields and regeneration on top.

## Bosses

| id | Name | HP mult | Gold mult | Armor | Move | Move set |
|---|---|---|---|---|---|---|
| warlord | Goblin Warlord | 14 | 15 | Light | Ground | War Charge (2.5× speed for 1.2s every 6s, waits while blocked) + Bloodlust (<40%: 1.4× speed, 2× melee, 1.5× attack rate) |
| necromancer | Necromancer | 12 | 15 | None | Ground | Raise Dead (3 skeletons every 8s, max 9 alive) + Bone Ward (immune 2s every 9s) |
| broodmother | Broodmother | 16 | 18 | Heavy | Ground | Lay Eggs (4 spiderlings every 7s, max 12) + Brood Burst (6 spiderlings at 5% of its max HP on death) |
| drake | Ember Drake | 18 | 20 | Heavy | Flying | Call the Swarm (2 bats every 10s, max 6) + Inferno (<50%: 1.35× speed) |

Each boss tests a different skill: the warlord punishes a thin hero block,
the necromancer needs splash for the adds and burst damage outside the ward,
the broodmother needs both magic (Heavy) and splash (swarm), and the drake
needs anti-air magic because artillery can't reach it.

## Abilities (Bosses/Abilities)

`EnrageAbility`, `ChargeAbility`, `SummonAbility`, `HealAuraAbility`,
`ShieldAbility`, `SplitOnDeathAbility`, `RegenerateAbility` — each a data
ScriptableObject plus a `…Behaviour` MonoBehaviour. All timers use scaled
`Time.deltaTime` (pause / fast-forward aware). Summons and splits use
`WaveManager.SpawnExtra` at the source's waypoint and position, so they count
toward the wave. Behaviours never undo stat changes on destroy (Enemy.Init
resets them), which keeps them safe with pooling. `EnemyAbilityBehaviour.AnyTriggered`
fires for VFX/audio; `IsActive` says whether a timed effect is running.

## Levels

World 32 × 18, (0,0) bottom-left. Slots sit 2 units beside a road (rule:
≥ 1.3 from every centreline, ≤ 3.2 from the nearest, ≥ 1.6 apart, ≥ 1 inside
the edges). Paths start just off-screen and leave through an exit edge.

| # | id | Name | Shape | Slots | Gold | Lives | Waves | Diff | Boss every | Bosses | Enemy pool |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | meadow | Greenleaf Meadow | 1 serpentine path (tutorial) | 12 | 260 | 20 | 10 | 0.85 | 10 | warlord | grunt, runner, bat, brute |
| 2 | crossroads | Miller's Crossroads | 2 west roads merging at a central crossing | 15 | 300 | 20 | 12 | 1.0 | 6 | warlord | + shaman, knight |
| 3 | frostfang | Frostfang Pass | 2 separate spawns (north gate, west valley), 2 exits | 16 | 340 | 20 | 14 | 1.05 | 7 | necromancer | + splitter, warder |
| 4 | marsh | Fenwick Fords | 3 paths (west, north, south) into one causeway | 16 | 380 | 20 | 15 | 1.1 | 5 | broodmother, necromancer | + troll |
| 5 | citadel | Shadow Citadel | finale: twin switchbacks merging at the gate | 16 | 450 | 15 | 20 | 1.2 | 5 | all four | all ten |

Hero starts on the key junction of each map (the merge point on 2/4/5).

### Endless mode

Every campaign map has an endless variant `"<id>_endless"` (e.g.
`meadow_endless`): same geometry, `wavesToWin = 0`, difficulty × 1.15,
starting gold × 1.2, the full enemy roster (still gated by `unlockWave`) and
all four bosses every 5 waves. A variant unlocks once its campaign map has at
least one star (`CampaignProgression.IsEndlessUnlocked`). UI suggestion: an
"Endless" toggle on each unlocked, completed map in the level select.

### Progression

`CampaignProgression(order, starLookup)`: level 1 always open; level N+1 opens
when level N has ≥ 1 star (configurable); stars clamp to 0..3; a stale save
with stars on a locked level can't skip the chain.

## Validation

- `Tools/ContentTests/run.sh` (plain .NET): geometry math, every rule of the
  layout validator, every campaign and endless map validated with zero
  errors and zero warnings, map-shape assertions, progression rules.
- In Unity: `Tower Defense/Content/Validate Levels`, or
  `LevelValidator.Validate(level, rules, requirePrefabs)` at runtime.
