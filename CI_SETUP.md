# GitHub Actions setup for Android APK

The repository uses GameCI to build the Unity 6 project as an Android APK.

## Unity Personal activation (current GameCI method)
GameCI's current GitHub activation guide uses a locally activated Unity Personal `.ulf` file rather than the old/deprecated request-activation-file workflow.

1. Install Unity Hub on any desktop computer and sign in to the Unity account used for CI.
2. In Unity Hub open **Preferences -> Licenses -> Add -> Get a free personal license**. Complete activation even if a Personal license already appears in the list; this ensures a `.ulf` file is created.
3. Locate `Unity_lic.ulf`:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
4. In this GitHub repository open **Settings -> Secrets and variables -> Actions** and create:
   - `UNITY_LICENSE` — paste the full text contents of `Unity_lic.ulf`.
   - `UNITY_EMAIL` — Unity account email.
   - `UNITY_PASSWORD` — Unity account password.

Do not commit a `.ulf`, Unity password, or any GitHub secret into the repository.

Official current GameCI activation guide: https://game.ci/docs/github/activation/

## Build pipeline
The workflow `.github/workflows/android-apk.yml`:
- downloads the vetted CC0 menu/forest ambience before Unity import;
- checks Unity credentials before spending runner time on a Unity image;
- refuses to build unless both exact creature model folders contain real models;
- requires both distinct user-provided Locust screamer tracks and the Boiled One MP4;
- explicitly rejects `amazing-grace-analog-horror.mp3`;
- runs the release content validator;
- builds ARM64 Android with IL2CPP;
- uploads `Builds/Android/Fallen_Forest_1.0.0.apk` as artifact `Fallen-Forest-Android-APK`.

Project editor version is pinned by `ProjectSettings/ProjectVersion.txt`.
