# Kingdom Defense — offline iOS tower defense (Unity 6)

Kingdom Rush–style 2D tower defense for iPhone and iPad. Fully offline: no
network, accounts, ads or tracking. The game runs with **zero art, audio,
prefab or scene assets**: sprites are drawn procedurally, sound effects and
music are synthesized, and content (5 towers, 10 enemies, 4 bosses, 5 maps +
endless variants) is built in code. Started from the 11 core scripts in the
Google Drive "Tower defense" folder (`Docs/SETUP_GUIDE.md`).

## Run it
1. Open this folder in **Unity 6000.0 LTS** (see `ProjectSettings/ProjectVersion.txt`).
2. Run **Tower Defense > iOS > Apply Project Settings** and **Create Bootstrap Scene**.
3. Press Play in `Assets/Scenes/Main.unity`. `App/GameApp.cs` boots the menu
   automatically. **Tower Defense > Sandbox > Play Sandbox** runs an
   autopiloted test map instead.
4. iOS build, signing and TestFlight: `Docs/IOS_BUILD.md`.

## Validate without Unity
```sh
Tools/check.sh          # compile all scripts against Unity stubs + run all .NET test suites
Tools/offline-audit.sh  # fail on any networking/tracking API
```
CI runs both (`.github/workflows/tower-defense.yml`).

## Layout
| Path | What |
|---|---|
| `Assets/Scripts/*.cs` | Original combat scripts (Tower, Enemy, WaveManager, Hero...) |
| `Assets/Scripts/Core` | Engine-free rules: damage, wave planner, upgrades, economy, stars |
| `Assets/Scripts/Content`, `Levels`, `Bosses` | Catalog, maps, progression, boss abilities |
| `Assets/Scripts/UI`, `Input` | Code-built uGUI HUD, radial menus, screens; touch/camera input |
| `Assets/Scripts/Visuals`, `Bootstrap` | Procedural art, runtime templates, map, FX; sandbox |
| `Assets/Scripts/Platform`, `Persistence`, `Audio` | Lifecycle, haptics, crash-safe saves, synthesized audio |
| `Assets/Scripts/App` | `GameApp`: menu → level select → level → results flow |
| `Docs/` | Architecture & ownership, content balance, iOS build guide |
