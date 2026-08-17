# Fallen Forest — user upload drop folder

This folder is intentionally designed for phone-friendly uploads of the **original ZIP archives** supplied by the project owner. `Tools/import_user_archives.py` extracts the approved contents into Unity asset paths during development/CI.

Recognized archive names:

- `toe-locust-by-doumty.zip`
- `the-boiled-one-horror-game-boiled-one.zip`
- `pickup-truck.zip`
- `Видео для скримера вареного.zip` (aliases: `boiled-jumpscare.zip`, `boiled_one_jumpscare.zip`)
- `скримеры.zip` (aliases: `locust-screamers.zip`, `screamers.zip`)

The importer copies only the two approved Locust screamer MP3s. `amazing-grace-analog-horror.mp3` is deliberately excluded and deleted from the release path if present.

## Licensing note

The current Locust and Boiled model archives are suitable only for the currently planned free/non-commercial prototype unless separate permission is obtained or the models are replaced:

- Locust by Doumty: CC BY-NC-ND (non-commercial, no derivatives).
- Boiled model by MG Rips: CC BY-NC (non-commercial).

Do not remove these restrictions from release documentation.
