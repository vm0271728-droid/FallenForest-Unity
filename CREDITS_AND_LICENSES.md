# Fallen Forest — asset provenance

This file is kept with the project so a release build has a traceable asset provenance record.

## User-provided media
- App icon / creature reference image: supplied by the project owner in the development conversation.
- `jakes-screamer.mp3`: supplied by the project owner. It is reserved for Locust jumpscare variant A.
- `the-screamer-shared-between-mallie-and-jenny.mp3`: supplied by the project owner. It is reserved for a distinct Locust jumpscare variant B.
- `boiled_one_jumpscare.mp4`: supplied by the project owner and used only by the unique Boiled One event.
- `amazing-grace-analog-horror.mp3` is intentionally excluded from the project and release.

## CC0 menu / forest audio
The Android build fetches these directly from their OpenGameArt file URLs using `Tools/fetch_cc0_audio.py`:
- `creepy_forest_menu.ogg` — **Creepy Forest (F)**, Augmentality / Brandon Morris (submitted by HaelDB). The source page offers a CC0 licensing option. Used as the horror main-menu track.
- `forest_ambience_cc0.mp3` — **Forest Ambience**, TinyWorlds, CC0. Used as the continuous forest-bed layer.
- `ambient_horror_cc0.ogg` — **Ambient horror**, techiew, CC0. Used as a low horror/tension layer.

Source pages:
- https://opengameart.org/content/creepy-forest-f
- https://opengameart.org/content/forest-ambience
- https://opengameart.org/content/ambient-horror

## Environment textures
The forest-ground and pine-bark source texture sets were obtained from Poly Haven assets released under CC0. Keep `Assets/FallenForest/Art/Textures/LICENSE_POLYHAVEN.txt` with source provenance details.

## Exact Doctor Nowhere creature models — release slots
The final APK release gate requires exact imported 3D models in:
- `Assets/FallenForest/Art/Models/DoctorNowhere/Locust/`
- `Assets/FallenForest/Art/Models/DoctorNowhere/Boiled/`

Selected Locust source page:
- Sketchfab model `The Locust`, author `xerio3900`, page reports Creative Commons Attribution (CC BY), model UID `c5f117c974944afeac0c47cf903b6e80`.

Selected Boiled One source page:
- Sketchfab model `The boiled one horror game`, author `siren head fan` (`sirenheadfn`), page reports Creative Commons Attribution (CC BY), model UID `9c1c5c2bdbea453bad49f79d3279b9ff`.

Before public redistribution, retain the original downloaded license/readme files next to each imported model and verify the current source-page license. Character/IP rights are separate from a mesh uploader's asset license.

## Procedural project assets
Additional wind behaviour, camera systems, encounter logic, forest-event systems, mesh batching and gameplay code were generated specifically for the Fallen Forest project.
