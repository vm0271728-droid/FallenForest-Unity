# Fallen Forest — final-stage status

## Completed in the final-stage pass
- Pinned a real Unity 6 LTS editor revision instead of a placeholder version string.
- Added a hard release-content validator: release APKs cannot contain placeholder Locust/Boiled geometry.
- Added GitHub Actions / GameCI Android APK pipeline and Git LFS configuration.
- Added high-quality mobile import policy (2K ASTC environment textures, streaming ambience, high-quality critical SFX).
- Added a unique generated Locust proximity sting and a separate Boiled trigger sting.
- Upgraded document folders from flat cubes to layered 3D folders with 2K cardboard/paper textures, normal detail, papers and metal clip.
- Upgraded flashlight pickup geometry while preserving the dim white outline requirement.
- Added hundreds of rocks, fallen logs, stumps and ground branches for forest breakup and physical navigation obstacles.
- Replaced the ending cube-car with a multi-part car, wheels, warm dynamic headlights and a dedicated passing-engine sound.
- Tightened rear-spawn interpretation: the whole rear hemisphere receives the exact 0.35 weight.
- Preserved only the two approved Locust screamer audio files; `amazing-grace-analog-horror.mp3` remains excluded.

## Remaining external release dependencies
1. Exact Locust 3D model file must be downloaded from its licensed source and imported into the prepared folder.
2. Exact Boiled One 3D model file must be downloaded from its licensed source and imported into the prepared folder.
3. The project must run once in Unity 6000.0.76f1 + Android Build Support (or in the included GameCI workflow) to import assets, generate scenes and compile IL2CPP Android code.

The release validator intentionally blocks the APK until items 1 and 2 are satisfied, so a fake/generic-monster "final" APK cannot accidentally be shipped.

## Repository / CI continuation
- Production repository initialized at `vm0271728-droid/FallenForest-Unity`.
- Android workflow updated to GameCI Unity Builder v5 with explicit credential and release-asset preflight checks.
- CI is configured to fail before Unity startup if either exact monster model, the Boiled video, or either approved Locust screamer is absent.
