# GitHub Actions setup for Android APK

The repository uses GameCI to build the Unity project as an Android APK.

Required repository secrets:
- `UNITY_LICENSE` — activated Unity license text for the project editor version.
- `UNITY_EMAIL` — Unity account email.
- `UNITY_PASSWORD` — Unity account password used by GameCI activation.

The workflow intentionally performs a preflight check before starting the expensive Unity build. It also refuses to build if either exact creature model is missing, if the Boiled video is missing, or if the removed `amazing-grace-analog-horror.mp3` file returns.

Build output:
- GitHub Actions artifact: `Fallen-Forest-Android-APK`
- APK path inside the artifact: `Builds/Android/Fallen_Forest_1.0.0.apk`

Project editor version is pinned by `ProjectSettings/ProjectVersion.txt`.
