# Gauntlet Duplication Checklist

This is the project-local memory for duplicating gauntlet scenes without breaking Android boot, menu loading, mobile controls, or enemy cleanup.

## Known Good Flow

- Android app package: `com.kailius.gauntlet11`
- Boot scene: `Assets/Scenes/Menu.unity`
- Play button target scene: `1_1Gauntlet`
- Required build order for this test APK:
  - `Assets/Scenes/Menu.unity`
  - `Assets/Scenes/1_1Gauntlet.unity`
  - any later scenes needed by portals/progression

The menu scripts must load the gauntlet by scene name, not a stale duplicate scene:

- `Assets/Scripts/Menu/Menu.cs`
- `Assets/Scripts/Camera/BTNJugar.cs`

Both should route Play/JUGAR to `SceneManager.LoadScene("1_1Gauntlet")` for the current test build.

## Required Scene Pieces

Every duplicated gauntlet scene needs these pieces checked against a known working scene before building to Android:

- `Player`
  - Has the normal player movement/combat scripts used by the original scene.
  - Has `MoveByTouch` for Android controls.
  - `MoveByTouch.controls` points to `ControllesMobiles`.
  - `MoveByTouch.joystick` points to `ControllesMobiles/Fixed Joystick`.
  - `MoveByTouch.menuInGame` points to `MenuIn-Game`.
  - `MoveByTouch.stats` points to `Estadisticas`.
  - `MoveByTouch.rowPoint` points to `Player/RowPoint`.
  - `MoveByTouch.dust` points to `Player/DustPS`.
  - Typical working values from `1_1Gauntlet`: `moveSpeed = 10`, `jumpHeight = 22`.

- `ControllesMobiles`
  - Must exist in the scene.
  - Must be active for Android testing.
  - Must contain the fixed joystick and action buttons.
  - Jump, melee attack, and bow/ranged buttons must call methods on the scene `Player`, not on a prefab asset or old scene object.

- `EventSystem`
  - Must exist in the scene.
  - Must have a `StandaloneInputModule` or equivalent input module so UI buttons receive taps.

- `Canvas` / HUD objects
  - Health, power, score, coin, gem, star, damage, and defense UI references are easy to lose during duplication.
  - The current scripts have null-safety for missing UI refs, but missing refs still mean the HUD may not update correctly.
  - Check `Stats` on `Player`.
  - Check `ScoreManager` on the scene score object.

- `MenuIn-Game`
  - Must exist if `MoveByTouch` expects it.
  - Pause/settings buttons should point to scene objects, not prefab leftovers.

- `Estadisticas`
  - Must exist if `MoveByTouch` or stats UI expects it.

## Enemy Death Stability

The Android test scene previously showed enemies looping death animations and leaving partial body/feet artifacts. The safe assumptions for duplicated gauntlets are:

- Enemy death animation clips should not loop.
- `Enemy.Die()` should only run once per enemy.
- Dead enemies should stop physics movement.
- Dead enemies should not keep active hitboxes/colliders.
- Dead enemies should be destroyed from the root enemy object after `timeDestroy`.

Files currently patched for this:

- `Assets/Scripts/Enemies/Enemy.cs`
- enemy `*die.anim` clips under `Assets/Animations`

When adding or duplicating enemies, check new death clips for `m_LoopTime: 0`.

## Android Crash Checklist

Before sending a new gauntlet to the phone:

- Confirm the gauntlet scene is in build settings after `Menu`.
- Confirm Play/JUGAR loads the intended scene name.
- Confirm mobile controls are active and wired to the scene `Player`.
- Confirm there is exactly one working `EventSystem` after scene load.
- Confirm obvious missing scene references on `Stats`, `ScoreManager`, `MoveByTouch`, and enemy scripts.
- Build ARM64 IL2CPP for the Samsung phone. Mono builds may fail install with `INSTALL_FAILED_NO_MATCHING_ABIS`.

Smoke test after install:

1. Launch app.
2. Verify menu stays alive for at least 5 seconds.
3. Press JUGAR.
4. Verify log contains the intended scene load.
5. Verify process remains alive.
6. Take a screenshot and confirm the intended gauntlet is visible.
7. Test joystick, jump, melee, and bow buttons on the phone.
8. Kill a few enemies and confirm death animation plays once, no looping, no feet-only leftovers.

Useful adb checks:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" devices
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell pidof com.kailius.gauntlet11
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" logcat -d -t 3000 | Select-String -Pattern 'Menu.PlayGame|1_1Gauntlet|NullReference|MissingReference|Exception|AndroidRuntime|FATAL|SIGSEGV|Unity|Fatal signal|CRASH' -Context 3,12
```

## Honest Notes

- A scene that looks visually duplicated can still be broken if script references point to old objects, inactive prefabs, or prefab assets.
- Missing UI references may not crash anymore because of null checks, but they can still make the scene feel broken.
- If a new duplicated gauntlet crashes only on Android, use fresh logcat around the exact action. Do not trust old logs.
- For future gauntlets, compare the duplicate against `1_1Gauntlet` first, because that is the current known Android-working scene.

## Future Codex Workflow

When testing another gauntlet scene, read this file first, then inspect the new scene for the required objects and references before building. If the new scene is meant to replace the current test target, update the build script and menu load target together so the app boots Menu and Play opens the intended gauntlet.
