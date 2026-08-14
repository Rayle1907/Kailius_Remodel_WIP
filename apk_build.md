# APK Build Instructions

This is the detailed project-local memory for getting a duplicated gauntlet scene onto an Android phone without repeating the earlier failures.

Read this before building or installing a new gauntlet APK. The short checklist is in `gauntlet duplication instructions.md`; this file explains the full workflow and the failure modes we already hit.

## Known Working Setup

- Unity project: `F:\ITU\ITU 3rd sem\ProjectS\Kailius_WIP\Kailius`
- Unity editor: `C:\Program Files\Unity\Hub\Editor\2020.1.1f1\Editor\Unity.exe`
- Android package id: `com.kailius.gauntlet11`
- APK output: `F:\ITU\ITU 3rd sem\ProjectS\Kailius_WIP\Kailius\Builds\Android\Gauntlet1_1.apk`
- ADB: `$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe`
- Tested phone device id: `R5GL10FF4BL`
- Tested phone model: Samsung `SM_S948B`
- Working Android architecture: IL2CPP ARM64

Mono builds may install-fail on the Samsung phone with `INSTALL_FAILED_NO_MATCHING_ABIS`. Use IL2CPP ARM64 unless there is a clear reason to change it.

## Golden Rule

Do not assume a scene duplicate is complete because it looks complete in Unity.

Most of the earlier breakage came from duplicated scenes missing scene-local references, inactive mobile controls, stale Play targets, or objects still pointing to prefab/old-scene instances. Visual duplication is not enough. Always inspect the scene wiring before building.

## Correct Boot Flow

The Android test build should boot into the menu, then load the current gauntlet after pressing Play/JUGAR.

Known working flow:

1. First scene in build: `Assets/Scenes/Menu.unity`
2. Play/JUGAR loads: `1_1Gauntlet`
3. Gauntlet scene is included after the menu in the build scenes.

The menu loading code must not point to a random duplicate like `Scene1 copy`.

Files to check:

- `Assets/Scripts/Menu/Menu.cs`
- `Assets/Scripts/Camera/BTNJugar.cs`
- `Assets/Editor/CodexAndroidBuild.cs`

Known useful log message:

```text
Menu.PlayGame loading 1_1Gauntlet
```

If Play is pressed and this line does not appear, debug the menu button/load path first.

## Required Scene Wiring

Use `1_1Gauntlet` as the known Android-working reference scene.

Every new gauntlet duplicate needs these objects and references:

- `Player`
  - Has normal movement/combat scripts.
  - Has `MoveByTouch` for Android controls.
  - Has `Rigidbody2D`.
  - Has `PlayerCombat` and `Bow` if the mobile action buttons use them.

- `MoveByTouch` on `Player`
  - `ControlesMoviles` points to scene object `ControllesMobiles`.
  - `joystick` points to `ControllesMobiles/Fixed Joystick`.
  - `menu` points to `MenuIn-Game`.
  - `stats` points to `Estadisticas`.
  - `row` points to `Player/RowPoint`.
  - `dust` points to `Player/DustPS`.
  - Known working values: `moveSpeed = 10`, `jumpHeight = 22`.

- `ControllesMobiles`
  - Exists in the scene.
  - Is active for Android testing.
  - Contains joystick and action buttons.
  - Button scripts point to the scene `Player`, not a prefab asset or a player from another scene.

- Mobile buttons
  - Jump button has `ButtonJump` wired to `Player`.
  - Melee button has `ButtonAttack` wired to `Player`.
  - Bow/ranged button has `ButtonBow` wired to `Player`.

- `EventSystem`
  - Exists in the scene.
  - Has an input module such as `StandaloneInputModule`.
  - Without this, the buttons may be visible but taps will not work.

- HUD and menu objects
  - `MenuIn-Game` exists.
  - `Estadisticas` exists.
  - `Stats` references are checked.
  - `ScoreManager` references are checked.

Null-safety was added to some UI scripts, so missing UI references may no longer crash immediately. That does not mean the scene is correctly wired. Missing UI references can still make the HUD wrong or incomplete.

## Enemy Death Stability

Earlier symptoms:

- NPCs looped death animations.
- Dead enemies stayed around.
- Some enemies appeared as partial body/feet artifacts.

Known fixes/requirements:

- Enemy death animation clips must not loop.
- `Enemy.Die()` must run once per enemy.
- Dead enemies should stop physics motion.
- Dead enemies should disable their colliders/hitboxes.
- Dead enemies should be destroyed from the root enemy object after `timeDestroy`.

Files involved:

- `Assets/Scripts/Enemies/Enemy.cs`
- enemy death clips under `Assets/Animations`

When adding new enemies or new death animations, inspect the `.anim` file and confirm:

```text
m_LoopTime: 0
```

If death artifacts return, check this before changing unrelated systems.

## Before Building

1. Close the Unity editor for this project.
   - Batchmode Unity cannot open the project while another Unity instance has it open.
   - Failure signature:

```text
Fatal Error! It looks like another Unity instance is running with this project open.
Multiple Unity instances cannot open the same project.
```

2. Confirm the phone is connected:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" devices
```

Expected:

```text
R5GL10FF4BL    device
```

If it says `unauthorized`, unlock the phone and accept the RSA prompt. If no device appears, check USB mode, cable, USB debugging, and Samsung Auto Blocker.

3. Confirm the build script targets the intended scene.

The helper script is:

```text
Assets/Editor/CodexAndroidBuild.cs
```

Important settings:

```csharp
PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.kailius.gauntlet11");
PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
```

## Build Command

Run from the workspace:

```powershell
& "C:\Program Files\Unity\Hub\Editor\2020.1.1f1\Editor\Unity.exe" -batchmode -quit -projectPath "F:\ITU\ITU 3rd sem\ProjectS\Kailius_WIP\Kailius" -executeMethod CodexAndroidBuild.BuildGauntlet11 -logFile "F:\ITU\ITU 3rd sem\ProjectS\Kailius_WIP\Kailius\Logs\CodexAndroidBuild.log"
```

Unity may return control before the build is fully understood by Codex. Always verify the APK timestamp and log.

Check the APK:

```powershell
Get-Item -LiteralPath "F:\ITU\ITU 3rd sem\ProjectS\Kailius_WIP\Kailius\Builds\Android\Gauntlet1_1.apk" | Select-Object FullName,Length,LastWriteTime
```

Check build log:

```powershell
Select-String -LiteralPath "F:\ITU\ITU 3rd sem\ProjectS\Kailius_WIP\Kailius\Logs\CodexAndroidBuild.log" -Pattern "Build completed|Build succeeded|Android build failed|Exception|Error|Failed|Gradle|Total time|BuildPipeline" -Context 1,3 | Select-Object -Last 80
```

Unity licensing messages at the top of the log can look scary but may be harmless if the build continues and the APK timestamp updates. Do not call the build good until the APK timestamp changes.

## Install Command

Install the fresh APK:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" install -r "F:\ITU\ITU 3rd sem\ProjectS\Kailius_WIP\Kailius\Builds\Android\Gauntlet1_1.apk"
```

Expected:

```text
Performing Streamed Install
Success
```

Confirm the installed update time:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell dumpsys package com.kailius.gauntlet11 | Select-String -Pattern 'versionName|versionCode|firstInstallTime|lastUpdateTime'
```

## Launch and Smoke Test

Clear logs, force-stop, launch, wait, and check the process:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" logcat -c
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell am force-stop com.kailius.gauntlet11
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell monkey -p com.kailius.gauntlet11 -c android.intent.category.LAUNCHER 1
Start-Sleep -Seconds 5
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell pidof com.kailius.gauntlet11
```

If `pidof` returns a number, the process is alive.

Check whether the app is actually foreground/resumed:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell dumpsys activity activities | Select-String -Pattern 'mResumedActivity|topResumedActivity|com.kailius.gauntlet11' -Context 0,2
```

Take a screenshot:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell screencap -p /sdcard/gauntlet_smoke.png
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" pull /sdcard/gauntlet_smoke.png "F:\ITU\ITU 3rd sem\ProjectS\Kailius_WIP\gauntlet_smoke.png"
```

Do not rely only on process-alive checks. A screenshot catches wrong-scene and missing-controls mistakes quickly.

## Play Button Test

After launch:

1. Ask the user to press Play/JUGAR manually.
2. Immediately check process and logs.

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell pidof com.kailius.gauntlet11
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" logcat -d -t 3000 | Select-String -Pattern 'Menu.PlayGame|1_1Gauntlet|NullReference|MissingReference|IndexOutOfRange|Argument|Exception|AndroidRuntime|FATAL|SIGSEGV|Unity|Fatal signal|CRASH|LoadScene' -Context 3,12
```

Expected:

- Process is still alive.
- Log contains the intended scene load.
- No `NullReference`, `FATAL`, `SIGSEGV`, or Android crash around the press.

Then take another screenshot and confirm:

- Correct gauntlet loaded.
- Joystick is visible.
- Jump/attack/bow buttons are visible.
- HUD is visible and reasonable.

## Common Failures and What They Mean

### App installs but will not launch

Check logcat for Android fatal/native crash. If a development build crashes at startup but a release build does not, do not assume the game scene is the cause. Development/debugging flags can change startup behavior.

### Play opens the wrong scene

Likely cause: menu script or button target still points to a stale duplicate scene.

Check:

- `Menu.cs`
- `BTNJugar.cs`
- scene list in `CodexAndroidBuild.cs`

### Controls are visible but do not work

Likely causes:

- Missing `EventSystem`.
- Buttons point to the wrong player.
- `MoveByTouch` refs point to old/prefab objects.
- `ControllesMobiles` exists but is inactive.

Compare against `1_1Gauntlet`.

### No joystick/buttons

Likely causes:

- `ControllesMobiles` was not duplicated.
- It exists but is inactive.
- Canvas/control hierarchy is missing from the duplicate scene.

### NPC death loops or partial dead sprites remain

Likely causes:

- Death animation loops.
- `Enemy.Die()` is triggered repeatedly.
- Colliders/physics/scripts remain active after death.
- Root enemy object is not destroyed.

Check `Enemy.cs` and death animation `m_LoopTime`.

### Unity batch build exits immediately and APK is old

Likely cause: another Unity editor has the project open, or Unity failed before building.

Check:

```powershell
Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,StartTime,Path
```

Then read:

```text
Logs/CodexAndroidBuild.log
```

### `adb devices` shows no device

Check:

- Phone is unlocked.
- USB debugging is enabled.
- USB mode is File Transfer / Android Auto, not charge-only.
- Samsung Auto Blocker is not blocking USB commands.
- Cable supports data.

### `adb devices` shows unauthorized

Revoke and re-accept debugging authorization:

- Phone: Developer options -> Revoke USB debugging authorizations.
- Unplug/replug.
- Accept the RSA prompt.

## Future Codex Procedure

When the user says they updated the game and wants it on the phone:

1. Read this file and `gauntlet duplication instructions.md`.
2. Confirm the intended target scene if it changed.
3. Check `CodexAndroidBuild.cs` still builds the intended scenes.
4. Confirm phone with `adb devices`.
5. Ensure Unity editor is closed.
6. Build with `CodexAndroidBuild.BuildGauntlet11`.
7. Verify APK timestamp changed.
8. Install with `adb install -r`.
9. Launch and verify process is alive.
10. Check install metadata `lastUpdateTime`.
11. Take screenshot.
12. Have the user press Play/JUGAR.
13. Check logs and screenshot after scene load.
14. Report honestly what was verified and what still needs manual gameplay testing.

## What Was Actually Verified Last Time

The last successful update did the following:

- Built a fresh APK.
- Installed successfully via `adb install -r`.
- Confirmed package `lastUpdateTime`.
- Launched the app.
- Confirmed the process stayed alive.
- Confirmed the Unity activity was resumed/focused.
- Captured a screenshot showing gameplay with mobile controls visible.

Manual gameplay still matters after that. The user should test:

- Joystick movement.
- Jump button.
- Melee attack button.
- Bow/ranged button.
- Menu/settings button.
- Enemy death behavior.
- Scene progression/portals.

## Be Honest

If the phone is disconnected, unauthorized, blocked by Auto Blocker, or Unity is open and locking the project, say so directly. Do not pretend the phone was updated unless:

- The APK timestamp changed.
- `adb install -r` returned `Success`.
- Package `lastUpdateTime` updated.
- The launched app process stayed alive.
