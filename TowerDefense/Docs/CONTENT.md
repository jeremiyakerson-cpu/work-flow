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

The numbers live in the engine-free table `Content/TowerBalance.cs`;
`DefaultContent` copies each entry into a `TowerData`
(`TowerData.ApplyUpgradeSpec`). `Tools/ContentTests/TowerBalanceTests.cs`
checks the rules below against those exact numbers.

### Upgrade model

KR style: **build (L1) → L2 → L3 → specialization (L4)**.

- L1 → L2 and L2 → L3 are plain upgrades with no choice (`Tower.Upgrade()`).
  Each one multiplies damage, range and fire rate. Poison towers also multiply
  `poisonDps` (`levelNPoisonMult`), and splash towers add to `splashRadius`
  (`levelNSplashBonus`, in world units).
- At L3 the player picks one of two named elite variants
  (`Tower.ChooseBranch(PathA|PathB)`). The branch applies its own multipliers
  and bonuses on top of L3 and may grant slow (`pathXAppliesSlow`) or splash
  (a splash bonus on a single-target tower). This is the capstone: nothing
  comes after it.
- Branch A is always the **damage and range** elite (bigger hits, longer
  reach). Branch B is the **fire-rate and utility** elite (faster, plus area,
  slow or poison spread).
- Cost curve: build < L2 < L3 < specialization. Each step costs more than the
  last, and the specialization is the most expensive. A specialized tower
  costs about 5.6–6× its build price in total. Selling refunds 70% of
  everything invested.
- Power curve: about ×1.55 total DPS per linear step, then ×1.4–2.3 for the
  specialization (tests require ≥ ×1.35 and ≥ ×1.4).

### Base stats (L1)

| id | Name | Type | Build | Range | Rate | Dmg | DPS | Extras | Role |
|---|---|---|---|---|---|---|---|---|---|
| archer | Archer Tower | Physical | 70 | 4.5 | 1.4 | 4 | 5.6 | — | Cheap, fast, hits air; poor vs Heavy |
| mage | Mage Tower | Magic | 100 | 4.0 | 0.6 | 11 | 6.6 | — | The Heavy-armor answer (×1.25) |
| artillery | Artillery | Physical | 120 | 4.2 | 0.4 | 14 | 5.6/target | splash 1.5, **ground only** | Pack clearer |
| frost | Frost Spire | Magic | 90 | 3.8 | 1.0 | 2.5 | 2.5 | slow 0.55× for 1.6s | Force multiplier for every other tower |
| alchemist | Alchemist Lab | Poison | 100 | 4.0 | 0.8 | 3 | 2.4 + 4 poison | splash 1.0, poison 4s | Ignores armor, stops regeneration |

### Upgrade costs

| id | L2 | L3 | Spec A | Spec B | Total (A / B) |
|---|---|---|---|---|---|
| archer | 80 | 110 | 160 | 150 | 420 / 410 |
| mage | 110 | 150 | 220 | 210 | 580 / 570 |
| artillery | 130 | 180 | 260 | 250 | 690 / 680 |
| frost | 100 | 130 | 190 | 180 | 510 / 500 |
| alchemist | 110 | 150 | 220 | 210 | 580 / 570 |

### Stats per level

Single-target DPS = damage × rate, before resistances. "+p" is poison DPS on
one target (poison refreshes and never stacks).

| id | L1 | L2 | L3 | Spec A | Spec B |
|---|---|---|---|---|---|
| archer | 4 dmg, 1.4/s, r4.5: **5.6** | 5.4, 1.61/s, r4.86: **8.7** | 7.3, 1.85/s, r5.25: **13.5** | 14.6, 1.85/s, r6.3: **27.0** | 7.3, 3.43/s, r5.25: **25.0** |
| mage | 11, 0.6/s, r4.0: **6.6** | 15.4, 0.66/s, r4.32: **10.2** | 21.6, 0.73/s, r4.67: **15.7** | 43.1, 0.73/s, r5.37: **31.3** | 21.6, 1.38/s, r4.67: **29.7** |
| artillery | 14, 0.4/s, r4.2, splash 1.5: **5.6** | 19.6, 0.44/s, r4.45, splash 1.65: **8.6** | 27.4, 0.48/s, r4.72, splash 1.8: **13.3** | 54.9, 0.48/s, r5.19, splash 2.4: **26.6** | 27.4, 0.87/s, r4.72, splash 1.9: **23.9** |
| frost | 2.5, 1.0/s, r3.8: **2.5** | 3.5, 1.1/s, r4.1: **3.9** | 4.9, 1.21/s, r4.43: **5.9** | 10.8, 1.21/s, r5.32: **13.0** | 5.4, 1.57/s, r4.43, splash 1.2: **8.5** |
| alchemist | 3, 0.8/s, r4.0, splash 1.0: **2.4 +4p** | 4.1, 0.88/s, r4.32, splash 1.1: **3.6 +5.6p** | 5.5, 0.97/s, r4.67, splash 1.2: **5.3 +7.8p** | 7.1, 0.97/s, r5.13, splash 1.2: **6.9 +15.7p** | 5.5, 1.45/s, r4.67, splash 1.7, slows: **7.9 +11.0p** |

### Specializations (L4 elites)

| Tower | A (damage/range) | B (rate/utility) |
|---|---|---|
| Archer | **Marksmen**: longbow snipers, heavy arrows (×2 dmg) and the longest reach (×1.2 range) | **Volley Rangers**: rapid volleys (×1.85 rate) that shred swarms and flyers |
| Mage | **Archmage**: devastating blasts (×2 dmg, ×1.15 range); melts armored knights | **Arcane Barrage**: a torrent of rapid bolts (×1.9 rate) |
| Artillery | **Big Bertha**: colossal shells (×2 dmg, ×1.1 range) and a huge blast (+0.6 splash) | **Mortar Battery**: rapid barrage (×1.8 rate, +0.1 splash) that pins packs |
| Frost | **Glacier Spire**: long-range ice lances (×2.2 dmg, ×1.2 range) | **Blizzard**: gains a 1.2 area (splash slow) with ×1.3 rate and ×1.1 dmg; slows whole groups |
| Alchemist | **Plague Doctor**: poison ×2, ×1.3 impact dmg, ×1.1 range | **Acid Rain**: wide splashes (+0.5), ×1.5 rate, poison ×1.4, adds a 0.75× slow |

Design intent: every enemy archetype has a "right answer" and a "wrong
answer" tower. Archers are gold-efficient on swarms but hopeless on knights;
mages are the opposite (a mage beats an archer against Heavy armor at every
level and in every branch pairing, which is tested). Artillery is the best
pack-clearer, but bats, wyverns and the drake fly over its shells. Frost does
little damage itself (less than an archer at every linear level), but every
slowed enemy spends longer in range of everything else, and Blizzard spreads
that slow over an area. Poison ignores armor and resets regeneration, so it
is the troll counter, and its DPS now grows with every upgrade.

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
  errors and zero warnings, map-shape assertions, progression rules, and the
  tower balance rules (cost curve, strictly rising DPS L1 < L2 < L3 < either
  specialization, power jump per step, poison/splash growth, role identities,
  distinct elite names).
- In Unity: `Tower Defense/Content/Validate Levels`, or
  `LevelValidator.Validate(level, rules, requirePrefabs)` at runtime.
