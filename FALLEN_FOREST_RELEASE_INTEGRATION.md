# Fallen Forest — Release Integration Pass

Status: active integration branch `integration/release-content-pass`.

## Canonical source archive

The user supplied one canonical archive named `все нужное.zip` (about 85.8 MB). This archive is the preferred source for the current release integration pass and supersedes using the same files as separate chat uploads.

It contains nine nested packages:

1. `grass.zip`
2. `first-person-arms.zip`
3. `flashlight.zip`
4. `document-file-folder-1.zip`
5. `pickup-truck.zip`
6. `toe-locust-by-doumty.zip`
7. `the-boiled-one-horror-game-boiled-one.zip`
8. Boiled One screamer video archive
9. screamer audio archive

`Tools/import_all_needed_archive.py` is the deterministic unpacker for this package. It copies only the approved Locust screamers and explicitly excludes `amazing-grace-analog-horror.mp3`.

## Grass rule

The user explicitly confirmed that the supplied Grass FBX contains three area variants:

- large area;
- small area;
- smallest area.

Do not assign the size class by filename guesses. `FallenForestUserContentIntegrator` instantiates the FBX, measures actual renderer XZ footprint, sorts the mesh variants, and builds:

- `UserGrass_Large.prefab`
- `UserGrass_Small.prefab`
- `UserGrass_Tiny.prefab`

The largest real user grass prefab also replaces the old generated `RuntimeGrass.prefab` path before scene assembly, so the release scene cannot silently fall back to crossed-quad placeholder grass once the user source asset is present. The generated Forest scene is patched to give `ForestScatterer` all three real grass variants.

Usage intent:

- Large: broad forest coverage away from precision boundaries.
- Small: local clearings, trail shoulders and object neighborhoods.
- Tiny: precision placement near documents, rocks, trees and trail/exclusion boundaries.

Existing trail and document grass-exclusion logic remains authoritative: grass must not cover the center of paths or clip through document pickup areas.

## Canonical imported paths

The importer normalizes the archive into these source roots:

- `Assets/FallenForest/Art/Vegetation/UserGrass`
- `Assets/FallenForest/Art/Viewmodel/Arms`
- `Assets/FallenForest/Art/Viewmodel/Flashlight`
- `Assets/FallenForest/Art/Documents/UserDocument`
- `Assets/FallenForest/Art/Vehicles/Pickup`
- `Assets/FallenForest/Art/Models/DoctorNowhere/Locust`
- `Assets/FallenForest/Art/Models/DoctorNowhere/Boiled`
- `Assets/FallenForest/Audio/Screamers`
- `Assets/FallenForest/Video/boiled_one_jumpscare.mp4`

## Integration order

1. Preserve the proven Unity 6 CI fixes: Unity Hub child modules, licensing path, collab-proxy 2.10.2, Terrain Physics module and Unity 6 AudioImporter API.
2. Import the canonical user archive without placeholders.
3. Build the three real grass prefabs.
4. Build exact Locust and Boiled One gameplay prefabs from the user FBX models.
5. Generate/patch MainMenu and Forest scenes using the real runtime world systems.
6. Integrate the exact arms, flashlight, document and pickup assets.
7. Reconcile menu/localization/credits with `FALLEN_FOREST_MENU_LOCALIZATION_CREDITS.md` and remove the old user FOV setting.
8. Run a static dependency/release validation pass before touching `main`.
9. Merge one controlled integration batch to `main` and let Actions perform the first full Android IL2CPP build attempt.
10. If CI fails, inspect the full failing job log before any next change.

## Release truth

This branch is integration work, not a claim that the final APK is complete. The last proven `main` result compiled all C# successfully on Unity 6, but release media/content validation stopped the Android build before `BuildPipeline.BuildPlayer`. The next main-branch run should happen only after the deterministic release blockers are closed as a batch.
