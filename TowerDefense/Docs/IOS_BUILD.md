# iOS build, release and CI

How to open the Unity project, export the Xcode project, sign it, ship it to
TestFlight, and keep the game provably offline. Covers the platform layer
(`Assets/Scripts/Platform`, `Persistence`, `Audio`, `Assets/Plugins`,
`Assets/Editor/Build`) built by workstream 4.

## 1. Requirements

| What | Version |
|---|---|
| Unity | **6000.0.58f2** (Unity 6 LTS, pinned in `ProjectSettings/ProjectVersion.txt`; first 6000.0 patch with the CVE-2025-59489 fix). Any later 6000.0.x LTS also works: let Unity upgrade the version file and commit it. |
| Unity modules | **iOS Build Support** (Unity Hub > Installs > Add modules) |
| Mac + Xcode | The Xcode release App Store Connect currently requires (check Apple's "Upcoming requirements" page). Signing and upload need macOS. |
| Apple | An Apple Developer Program membership for device builds and TestFlight |
| .NET 8 SDK | Only for the offline checks in `Tools/` (no Unity needed) |

Render pipeline: **built-in**. URP needs editor-authored pipeline assets, so it
is a later, deliberate migration (Package Manager > URP, create a 2D Renderer
asset, assign it in Graphics and Quality settings, convert materials).

## 2. First open (once per clone of a fresh project)

The repo contains only the files that matter: `Assets/` scripts and plugins,
`Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt` and
`ProjectSettings/TagManager.asset` (layer 8 = `Enemy`). Unity generates the rest.

1. Unity Hub > **Add project from disk** > select `TowerDefense/`. Open with 6000.0.58f2.
2. Wait for the import. Unity creates `Library/` (ignored), every missing
   `ProjectSettings/*.asset`, `Packages/packages-lock.json` and a `.meta` file
   next to every asset.
3. Menu **Tower Defense > iOS > Apply Project Settings** (also applied
   automatically before every iOS build).
4. Menu **Tower Defense > iOS > Create Bootstrap Scene**: creates the empty
   `Assets/Scenes/Main.unity` and puts it first in Build Settings. The game
   builds itself from code at runtime (`[RuntimeInitializeOnLoadMethod]`).
5. **Active Input Handling** (Edit > Project Settings > Player > Other
   Settings) must be **Input Manager (Old)** or **Both**: the game reads
   touches through the legacy `UnityEngine.Input` + `StandaloneInputModule`.
   Step 3 switches a "New"-only project to Old (restart the Editor when it
   says so). Do not add `com.unity.inputsystem` to `Packages/manifest.json`.
6. **File > Build Profiles** (or Build Settings) > iOS > **Switch Platform**.
7. Press Play once: the console should show no errors; the platform services
   create `[Platform]` and `[Audio]` objects under DontDestroyOnLoad.
8. **Commit** the generated files so every machine (and CI) gets identical
   GUIDs and settings:
   - all `*.meta` files under `Assets/` (never let two people generate them separately),
   - `ProjectSettings/*.asset`, `ProjectSettings/ProjectVersion.txt`,
   - `Packages/packages-lock.json`,
   - `Assets/Scenes/Main.unity`.
   `TowerDefense/.gitignore` already excludes `Library/`, `Temp/`, `Logs/`,
   `UserSettings/`, `Builds/` and IDE files.

`Assets/Plugins/iOS/TDHaptics.mm` and `PrivacyInfo.xcprivacy` are recognised
as iOS-only plugins by folder convention; no inspector changes are needed.

## 3. Export the Xcode project

From the Editor: **Tower Defense > iOS > Build Xcode Project** (writes `Builds/iOS`).

From the command line (what CI runs):

```bash
"/Applications/Unity/Hub/Editor/6000.0.58f2/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath TowerDefense -buildTarget iOS \
  -executeMethod TowerDefense.BuildTools.BuildScript.BuildIOS \
  -customBuildPath Builds/iOS -buildNumber 42 -logFile -
```

- Always pass `-buildTarget iOS`: the Info.plist post-processor only compiles
  while iOS is the active target. If the editor is on another platform,
  `BuildIOS` switches it and fails with a message asking for a second run.
- Output path: `-customBuildPath` / `-buildPath`, else `TD_IOS_BUILD_PATH`, else `Builds/iOS`.
- `-developmentBuild` (or `TD_DEVELOPMENT_BUILD=1`) makes a development build.
- Exit code 0 = Xcode project written; 1 = any failure (details in the log).

What the build applies (`Assets/Editor/Build/IOSProjectSetup.cs`):

| Setting | Value |
|---|---|
| Bundle id | `com.yourcompany.towerdefense` (**placeholder**, see 4.1) |
| Version / build | `1.0.0` / `-buildNumber`, `TD_BUILD_NUMBER`, `GITHUB_RUN_NUMBER`, else `1` |
| Orientation | Landscape left + right only, auto-rotate between them |
| Devices / OS | iPhone + iPad, **iOS 15.0** minimum |
| Scripting | IL2CPP, ARM64, .NET Standard 2.1, managed stripping Medium (`Assets/Plugins/link.xml` keeps all game code), engine code stripping on |
| Graphics | Metal (automatic graphics API) |
| Screen | Requires full screen (no iPad multitasking), status bar hidden, edge gestures deferred (All) |
| Background | Does not run in background; suspends (`appInBackgroundBehavior = Suspend`) |
| Audio session | `muteOtherAudioSources = false` (Ambient: mixes with the player's music, obeys the silent switch) |
| Network | `requiresPersistentWiFi = false`, `allowHTTPDownload = false` |
| Input | Active Input Handling forced from "New" to "Old" if needed (legacy Input Manager) |

Runtime (`AppLifecycle`): 60 fps target, landscape lock, screen kept awake
and edge gestures deferred **only while a wave is running**, save flushed and
game paused when the app backgrounds or loses focus (it stays paused on
return), `Resources.UnloadUnusedAssets` on low-memory warnings.

Post-process (`IOSBuildProcessing.cs`) edits the exported `Info.plist`:
`ITSAppUsesNonExemptEncryption = false`, landscape-only
`UISupportedInterfaceOrientations` (and `~ipad`), `UIRequiresFullScreen`,
`UIStatusBarHidden`, `UIViewControllerBasedStatusBarAppearance = false`, and
removes tracking/networking/permission keys (`NSUserTrackingUsageDescription`,
`NSAppTransportSecurity`, `NSLocalNetworkUsageDescription`, `UIBackgroundModes`,
`SKAdNetworkItems`, ...). It also checks that a privacy manifest is present.

## 4. Xcode, signing and the device

1. Open `Builds/iOS/Unity-iPhone.xcodeproj` (no CocoaPods: there are no third-party SDKs).
2. Target **Unity-iPhone** > Signing & Capabilities: tick *Automatically manage
   signing*, choose your **Team**. (Or set `TD_APPLE_TEAM_ID` / Player Settings
   > iOS > Signing Team ID before exporting.)
3. Plug in a device, select it, Run. Release builds: Product > Archive.

### 4.1 Bundle identifier (do this before the first upload)

`com.yourcompany.towerdefense` is a placeholder and will be rejected by
signing. Pick your own reverse-DNS id, register it as an App ID in the Apple
Developer portal, then set it in **one** place:
- Player Settings > iOS > Bundle Identifier (kept: the setup script only
  replaces Unity defaults and the placeholder), or
- the `BundleId` constant in `Assets/Editor/Build/IOSProjectSetup.cs`, or
- `TD_BUNDLE_ID` in the environment (CI).

Never change it after release: saves live in that app's container.

## 5. TestFlight / App Store checklist

- [ ] **Bundle id, team, version, build number** set (build number must increase per upload).
- [ ] **App icon**: Player Settings > iOS > Icon. Provide a 1024x1024 PNG
      (no transparency, no rounded corners); Unity fills the size set.
- [ ] **Launch screen**: Player Settings > iOS > Splash Image > Launch Screen
      (a storyboard is mandatory; Unity's default solid-colour storyboard is
      acceptable; use a landscape image if you add one). The Unity splash is
      a separate, in-player screen.
- [ ] **Privacy manifest**: `Assets/Plugins/iOS/PrivacyInfo.xcprivacy`
      (no tracking, no tracking domains, no collected data; required-reason
      APIs: UserDefaults `CA92.1`, file timestamp `C617.1`). Unity 6 merges
      every `PrivacyInfo.xcprivacy` under `Assets/Plugins` into the
      UnityFramework manifest together with the engine's own declarations
      (supported since 2021.3.35f1 / 2022.3.18f1 / 2023.2.7f1). Verify after
      archiving: Xcode Organizer > right-click the archive > **Generate Privacy
      Report**. The build log warns if no manifest made it into the project.
- [ ] **App Privacy** in App Store Connect: "Data Not Collected".
      A **privacy policy URL** is still required for every app: a one-page
      statement that the game is offline and collects nothing is enough.
- [ ] **Export compliance**: answered automatically by
      `ITSAppUsesNonExemptEncryption = false` (no networking, no custom crypto).
- [ ] **Age rating** questionnaire: cartoon/fantasy violence (infrequent/mild)
      and nothing else; no user-generated content, no web access, no ads,
      no in-app purchases.
- [ ] **Screenshots** in landscape for the required iPhone and iPad sizes.
- [ ] Test on a real device: rotation (both landscapes), home-swipe during a
      wave (first swipe only shows the grabber), backgrounding mid-wave
      (game is paused on return, progress saved), silent switch (game audio
      muted, the player's music keeps playing), haptics toggle.
- [ ] Archive > **Distribute App** > App Store Connect > Upload; add testers in TestFlight.

## 6. How "fully offline" is guaranteed

1. **No networking code compiles**: `Packages/manifest.json` has no
   `com.unity.modules.unitywebrequest*`, analytics, ads, IAP, services,
   netcode or transport packages, so `UnityWebRequest` does not even exist.
2. **`Tools/offline-audit.sh`** (run in CI on every change) fails on any
   networking/tracking API under `Assets/`: `UnityWebRequest`, `WWW`,
   `System.Net`, `HttpClient`, `WebClient`, sockets, `WebSocket`, `Dns`,
   `Application.OpenURL`, advertising identifiers in C#; `NSURLSession`,
   `NSURLConnection`, CFNetwork, Network.framework, BSD sockets, web views,
   AdSupport and AppTrackingTransparency in native plugins; and on online
   packages in the manifest. `--self-test` proves each rule fires.
3. **Info.plist** is stripped of ATS exceptions, tracking and network keys
   at export; there is no ATT prompt (`NSUserTrackingUsageDescription` is removed).
4. **Privacy manifest** declares no tracking and no collected data.
5. **Everything is local**: saves at `Application.persistentDataPath`, audio
   synthesized at startup, art generated in code. Unity Services (Analytics,
   Cloud Diagnostics) must stay unlinked (Edit > Project Settings > Services).
   If your editor shows **Player Settings > Other > "Disable HW Statistics"**
   (Pro feature), tick it.

## 7. Save data

- File: `<persistentDataPath>/save.json` (iOS: the app's Documents folder,
  included in device backups), plus `save.json.bak` (previous save) and,
  only after a crash or corruption, `save.json.tmp` / `save.json.corrupt`.
- Format: JSON (JsonUtility) followed by a footer line
  `#td-save crc32=XXXXXXXX length=N`. A hand-edited file without the footer
  is accepted if it parses.
- Writes are atomic (temp file, fsync, rename over the old file with a
  backup). Loading tries main, temp, backup, then defaults; it never throws.
- Schema `version` 2 with forward migrations (`SaveMigrator`). To change the
  schema: bump `SaveData.CurrentVersion`, add a step, add a test.
- Debounced autosave (1.5 s after the last change, at most 10 s), plus a flush
  on background/quit. Kill counts are written lazily with the next save.
- Reset during development: delete the files, or call
  `SaveRuntime.Service.ResetProgress()` (keeps settings).

## 8. CI (`.github/workflows/tower-defense.yml`)

Runs on pull requests and pushes to `main` that touch `TowerDefense/**`:

- **checks** (always, no license): `Tools/check.sh` (stub compile of runtime
  and editor scripts + every engine-free suite: core, content, platform,
  visuals) and the offline audit with its self-test.
- **ios-gate** + **ios-build** (pushes to `main` and manual runs): exports the
  Xcode project with `game-ci/unity-builder@v4` on Linux and uploads it as an
  artifact. Signing and upload remain a Mac step. A job-level `if` cannot read
  secrets, so `ios-gate` checks them and sets an output; without secrets the
  build is skipped with a notice.

Secrets (repo Settings > Secrets and variables > Actions):

| Secret | Personal license | Pro/Plus license |
|---|---|---|
| `UNITY_LICENSE` | contents of the activated `.ulf` file (see game-ci "Activation") | optional |
| `UNITY_EMAIL` | Unity account email | Unity account email |
| `UNITY_PASSWORD` | Unity account password | Unity account password |
| `UNITY_SERIAL` | not used | serial key |

The game-ci Docker image for the pinned editor version must exist
(`unityci/editor:ubuntu-6000.0.58f2-ios-*`); when upgrading Unity, check that
the matching image is published.

## 9. Offline checks without Unity

```bash
cd TowerDefense
Tools/check.sh                                   # stub compile + all engine-free suites (incl. PlatformTests)
dotnet test Tools/PlatformTests/PlatformTests.csproj   # just save, audio, throttling, haptics gate
Tools/offline-audit.sh && Tools/offline-audit.sh --self-test
```

`Tools/PlatformTests` compiles only the engine-free files (everything outside
the `Unity/` subfolders of `Platform`, `Persistence` and `Audio`), so any
accidental `UnityEngine` dependency there breaks the build.

## 10. Troubleshooting

- **"Switched the active build target to iOS. Run the build again"**: expected
  the first time; pass `-buildTarget iOS` on the command line.
- **No sound in the Editor**: audio renders on a worker thread at startup
  (well under a second); requests before that are dropped. `AudioManager`
  adds an `AudioListener` if the scene has none.
- **Game pauses when the Editor loses focus**: only on device; focus loss is
  ignored in the Editor, backgrounding (pause) is not.
- **`InvalidOperationException: You are trying to read Input using the
  UnityEngine.Input class, but you have switched active Input handling to
  Input System package`**: set Active Input Handling to Old or Both (see 2.5).
- **Signing errors**: bundle id still the placeholder, or no team selected.
- **"Multiple commands produce PrivacyInfo.xcprivacy"**: a second manifest was
  added manually to the same target; keep only the one under `Assets/Plugins/iOS`.
