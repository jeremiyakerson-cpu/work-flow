# Difficulty modes

Every level (campaign and endless) is played on one of four difficulties,
picked in a modal right after the level is chosen in level select. Stars and
best waves are saved **per difficulty**.

| | Easy | Normal | Hard | Impossible |
|---|---|---|---|---|
| Enemy health (all enemies, summons included) | 0.70× | 1.00× | 1.35× | 1.70× |
| Extra boss health (on top of enemy health) | 0.90× (0.63× total) | 1.00× | 1.10× (≈1.49× total) | 1.20× (2.04× total) |
| Enemy speed | 0.90× | 1.00× | 1.10× | 1.20× |
| Kill reward | 1.15× | 1.00× | 0.90× | 0.80× |
| Starting gold | 1.30× | 1.00× | 0.85× | 0.75× |
| Lives | 1.5× | 1.0× | 0.5× | **always 1** |
| Early-call bonus | yes | yes | yes | **no** (calling early still works) |
| On a 20-life, 250-gold level | 30 lives, 325 gold | 20 lives, 250 gold | 10 lives, 213 gold | 1 life, 188 gold |
| Unlock | level unlocked | level unlocked | level unlocked | 3 stars on Hard **on that map** |

The table lives in one place: `Assets/Scripts/Core/DifficultyRules.cs`
(`Difficulty.Rules(mode)`), engine-free and unit-tested
(`Assets/Tests/EditMode/DifficultyRulesTests.cs`).

## Unlock rules

- Easy, Normal and Hard are open on every unlocked level.
- Impossible on a map needs **3 stars on Hard on that map**. Endless variants
  check their base campaign map (`CampaignProgression.IsDifficultyUnlocked`).
- The next campaign level and a map's endless variant unlock with at least one
  star on **any** difficulty.
- Picker: the last used difficulty is preselected (Impossible falls back to
  Hard on maps where it is still locked). Retry keeps the difficulty; Next keeps
  it too, clamped from Impossible to Hard if the next map hasn't unlocked it.

## How it plugs in

- `WaveManager.ApplyLevel(level, mode)` folds the rules into the wave curve
  (`DifficultyRules.ApplyTo(WaveCurve)`): health through `DifficultyMultiplier`,
  speed through `BaseSpeed`, rewards through the reference reward. Boss types get
  the extra boss health and the reward multiplier (`ApplyToBoss`). Because
  everything goes through the curve, plans, "next wave" previews and summons
  (`WaveManager.ScaledStats`) all agree. The endless speed cap (`maxSpeedScale`)
  limits growth only, so the multiplier still applies on late waves.
- `WaveManager.CallNextWaveEarly()` still skips the countdown on Impossible but
  pays 0; `EarlyCallBonusEnabled` / `EarlyCallBonusPreview` drive the button,
  which hides its bonus pill.
- `GameManager.Configure(baseGold, baseLives, mode)` scales the level's authored
  gold and lives (never below 1 life) and exposes `CurrentDifficulty`.
- The old `ApplyLevel(level)` / `Configure(gold, lives)` overloads mean Normal,
  and Normal is the identity, so existing balance and tests are unchanged.
- Stars still come from `StarRating` on lives kept vs. starting lives. With one
  life, surviving an Impossible level is always 3 stars.

## Saves (schema v3)

`LevelRecord.modes` holds best stars / best wave / completion per difficulty;
the record's top-level fields are the best across difficulties (what level
select, unlocks and the star total read). `SaveData.endlessBestWaves` tracks
the endless best per difficulty (`endlessBestWave` stays the overall best), and
`SettingsData.lastDifficulty` remembers the last pick. The v2 → v3 migration
moves every existing record and the endless best to **Normal**. `SaveService`:
`RecordLevelResult(id, mode, stars, wave)`, `GetBestStars(id, mode)`,
`GetBestWave(id, mode)`, `RecordEndlessResult(wave, mode)`,
`GetEndlessBestWave(mode)`, `TryGetHardestCompleted(id, out mode)`,
`LastDifficulty`. The difficulty-less overloads read the best across
difficulties and write Normal.

## Design rationale

- **Normal is today's game.** Every multiplier is relative to the authored
  level data, so the content team keeps balancing one version of each level and
  all four difficulties follow.
- **Easy is noticeably forgiving, not trivial.** 30% less health is roughly one
  tower tier of slack; the extra gold and lives let a new player recover from a
  bad build, and slightly slower enemies give the hero time to react. Bosses are
  softened a bit more (0.63× in total) because a boss leak is the most common
  first-time defeat.
- **Hard pushes on every axis a little rather than one axis a lot.** +35% health
  and +10% speed raise the damage you need by about half; 15% less starting gold
  and 10% less per kill tighten the build order; half the lives make leaks
  costly without making them instantly fatal, and the 3-star bar on 10 lives is
  "leak at most one".
- **Impossible is a mastery mode.** It adds more of the same (health, speed,
  economy) and two rule changes: a single life, so any leak ends the run, and no
  early-call gold, so you can't snowball by rushing waves. It is gated behind a
  3-star Hard clear of the same map, so a player only meets it on a map they
  have already mastered.
- Health, speed and economy compound: Hard enemies carry ≈1.49× the "effective
  threat" (health × speed) of Normal while the economy gives ≈0.87× the gold;
  Impossible ≈2.04× threat with ≈0.78× gold. Easy is ≈0.63× threat with ≈1.2×
  gold.

## Tuning

1. Edit the four rows in `Difficulty` (`DifficultyRules.cs`). Constructor order:
   enemy health, boss health (extra), speed, kill reward, starting gold,
   lives multiplier, fixed lives (0 = use the multiplier), early-call bonus.
2. Keep Easy < Normal < Hard < Impossible on every axis and Normal at exactly
   1.0 everywhere: `DifficultyRulesTests` fails otherwise.
3. Change the unlock with `Difficulty.ImpossibleUnlockStars` /
   `ImpossibleUnlockMode`; the picker, results hint and progression follow.
4. Run `Tools/check.sh`. Tune per level with `LevelData.difficultyMultiplier`
   (it stacks with the difficulty's health multiplier).
5. Watch the speed column: projectiles and the hero must still catch the
   fastest enemies at the endless speed cap times the Impossible multiplier.
