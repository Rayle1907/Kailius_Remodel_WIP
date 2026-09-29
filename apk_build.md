# Android APK build

This is the current build, verification, and phone smoke-test procedure for the Unity project.

## Build contract

- Unity version: `6000.5.2f1`.
- Package ID: `com.kailius.gauntlet11`; scripting backend: IL2CPP; architecture: ARM64.
- `BuildGauntlet11` builds enabled Build Settings scenes with `Menu` first and `1_1Gauntlet` second. `BuildScene1Only` builds only `Scene_1`.
- For every Android scene except `Menu`, the scene-build callback activates and wires the configured mobile controls in build data. It does not save scene files or change movement speed and jump height.
- The callback requires one scene `Player` with `MoveByTouch` and enabled `PlayerCombat`, one `ControllesMobiles` hierarchy with joystick, Jump, Attack, Canvas and `GraphicRaycaster`, plus the player dust, row, menu, and stats objects. It creates an `EventSystem` or input module if absent. Bow is not required while its mechanic is disabled.
- A missing required object or component fails the build with its scene name and reason. Both build commands use `CleanBuildCache`, so builds may take longer.

For scene duplication details, see [duplication_instr.md](duplication_instr.md).

## Before building

1. Confirm the intended scenes are enabled in `ProjectSettings/EditorBuildSettings.asset`. The menu loads `1_1Gauntlet`.
2. Close another Unity Editor instance using this project; batch mode cannot open the same project concurrently.
3. Confirm the Android SDK and Unity Android build support are available.

## Build and verify

Run from PowerShell:

```powershell
$project = "F:\ITU\ITU 3rd sem\ProjectS\Kailius_WIP\Kailius"
$unity = "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe"
& $unity -batchmode -quit -projectPath $project -executeMethod CodexAndroidBuild.BuildGauntlet11 -logFile "$project\Logs\CodexAndroidBuild.log"
```

Verify the command exit code, a fresh APK, and one `ANDROID BUILD PREFLIGHT` entry per non-menu scene. The current Build Settings contain five non-menu scenes.

```powershell
Get-Item "$project\Builds\Android\Gauntlet1_1.apk" | Select-Object FullName,Length,LastWriteTime
Select-String "$project\Logs\CodexAndroidBuild.log" -Pattern 'ANDROID BUILD PREFLIGHT|Build completed|Build succeeded|Android build failed|Android build stopped|Exception|Error' | Select-Object -Last 80
```

Expected preflight format:

```text
ANDROID BUILD PREFLIGHT: 1_1Gauntlet mobile controls repaired in build data and input checks passed.
```

Check `git diff -- Assets/Scenes` after the build. The scene-build callback must not save or rewrite scene files. The build commands set Android Player Settings and may change `ProjectSettings`, so record those files before building and review any resulting settings diff separately.

## Install and smoke test

With a connected, authorized Android device:

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb devices
& $adb install -r "$project\Builds\Android\Gauntlet1_1.apk"
& $adb shell am force-stop com.kailius.gauntlet11
& $adb shell monkey -p com.kailius.gauntlet11 -c android.intent.category.LAUNCHER 1
& $adb shell pidof com.kailius.gauntlet11
```

Confirm `adb install` reports `Success`, the process stays alive, and Play/JUGAR opens `1_1Gauntlet`. Check the visible controls, then manually test joystick, jump, and melee attack. Use fresh `logcat` output for crashes or missing-reference errors. A running process alone does not prove that input works.

## Common failures

- **No fresh APK:** Read the current build log first. A second Unity Editor may have locked the project.
- **Build stops in a scene:** Fix the named missing object, component, or control in that source scene, then rebuild.
- **Play opens another scene:** Check `Assets/Scripts/Menu/Menu.cs` and `Assets/Scripts/Camera/BTNJugar.cs`.
- **Controls appear but do not respond:** Check the scene player references, `EventSystem`, input module, and UI raycasters.
- **APK will not install:** Confirm the target device accepts the ARM64 IL2CPP build.
