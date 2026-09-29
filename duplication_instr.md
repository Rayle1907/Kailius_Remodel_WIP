# Duplicating a gauntlet scene

Use `Assets/Scenes/1_1Gauntlet.unity` as the current reference scene. Before building, confirm the duplicate has:

- One active scene `Player` with its normal movement and combat components, plus configured `MoveByTouch`.
- `MoveByTouch` references to its scene `ControllesMobiles`, joystick, `MenuIn-Game`, `Estadisticas`, `Player/RowPoint`, and `Player/DustPS`. Preserve the scene's movement and jump values.
- A `ControllesMobiles` hierarchy with Canvas, `GraphicRaycaster`, joystick, Jump, and Attack controls. Their handlers must target the player in this scene. Bow is currently disabled and is not a build requirement.
- One working `EventSystem` and input module; the Android build callback can add these to build data if absent.
- Working HUD references on `Stats` and `ScoreManager`. Build validation of mobile controls does not validate the whole HUD.

Add the duplicate to Build Settings when it should ship. If it replaces the current Play destination, update both menu load paths and check the menu still loads the intended scene. The Android callback repairs controls only in build data and fails with a scene-specific message when a required part is missing; it does not save the scene.

For enemy death behavior, confirm new death animation clips do not loop and the enemy root is destroyed after death. For the APK build, log checks, install, and phone test, follow [apk_build.md](apk_build.md).
