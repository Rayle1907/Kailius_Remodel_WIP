# Personal iPad build

This build is for direct installation and testing. It exports a development Xcode project and uses Unity Analytics' development environment.

## One-time setup

1. Install Unity `6000.5.2f1` with **iOS Build Support** in Unity Hub.
2. Install full Xcode, open it once to accept its license and install components, then select it in Xcode's Command Line Tools settings. This Mac runs macOS 26.3; the current App Store Xcode requires 26.6. Download a compatible Xcode release from [Apple Developer Downloads](https://developer.apple.com/download/all/) after signing in. Apple's [compatibility table](https://developer.apple.com/xcode/system-requirements/) lists Xcode 26.6 as supporting macOS 26.2 through 26.x. Check the iPad's iPadOS version against the chosen Xcode's device-support range.
3. Connect the iPad to the Mac, trust the Mac, and enable Developer Mode on the iPad.
4. Sign in to Xcode with an Apple Account. A Personal Team can install directly on your own device. Free signing expires after seven days, requiring a rebuild and reinstall.

## Export and install

1. Open the project in Unity `6000.5.2f1`. Run `CodexIOSBuild.BuildForDevice` from the Editor or batch mode. The Xcode project is written to `Builds/iOS/Gauntlet`.
2. Open `Builds/iOS/Gauntlet/Unity-iPhone.xcodeproj` in Xcode.
3. For the `Unity-iPhone` target, choose your Personal Team and enable automatic signing. If `com.kailius.gauntlet11` is unavailable for that team, choose a unique identifier once and keep it stable for later installs. Do not commit signing credentials.
4. Select the connected iPad and run the app. Unity's iOS build targets both iPhone and iPad, allows both landscape directions, and disables portrait.

The export includes all enabled Build Settings scenes. `Menu` and `1_1Gauntlet` must be enabled first; later enabled levels follow. The mobile build preflight checks every non-menu scene and repairs touch controls in build data without saving source scenes.

## Device checks

- Play each scene and test movement with jump and attack at the same time, pause, retry, transitions, and revive offers.
- Rotate to each landscape direction in the menu and during play. Check that buttons, HUD, and dialogs stay visible and portrait never appears.
- Lock and unlock the iPad, switch apps, force-close and reopen, then try offline play. Returning from active play should show the existing pause menu.
- Open **PRIVACY** in the main or in-game pause menu. Test analytics consent, withdrawal, and deletion. Verify development events only after consent.
- Check sustained frame rate, memory, and heat on the iPad before changing quality settings. The iOS default uses the existing mobile quality preset.
- Review `git diff -- Assets/Scenes` after exporting; the build should not change scene files.

An iPhone test and the final device target decision remain for later. Public release work—App Store metadata, distribution signing, privacy disclosures, screenshots, and broader device testing—is separate.
