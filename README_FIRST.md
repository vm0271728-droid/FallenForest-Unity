# Fallen Forest — Unity development start

This is the first real Unity source package for the Android horror game described in the chat.

## Implemented in code
- First-person Android control: floating joystick on the left half; right half camera look.
- Non-inverted camera.
- Camera physics: head bob, turn roll, smoothing, configurable shake.
- Settings: sensitivity 0.3x–2.5x, FOV 60–100, camera shake 0–100%.
- Flashlight pickup and flashlight ray detection.
- 10 randomly selected document folders from safe spawn points.
- Autosave after document pickup.
- Final run after document #10: player speed x2.15.
- Locust final chase target speed = 97.5% of the player's final speed.
- MonsterDirector spawn ring logic.
- Rear spawn weighting x0.35 (65% less frequent behind the player).
- Overall regular monster spawn probability decays by 2.85% multiplicatively per collected document.
- Locust base spawn ring 18–42 m.
- Boiled One base spawn ring 28–55 m and relative encounter weight 0.2 vs Locust (5x rarer).
- Boiled One may happen only once, does not chase, and is excluded from final chase.
- Boiled One flashlight event hooks: stand -> kneel/eyes closed animation triggers -> supplied video -> blackout -> wake at same position, no death.
- Locust behavior hooks: hidden/peek/observe 4s/retreat/attack/final chase.
- Supplied scream audio is wired for Locust death variants.
- Supplied Boiled One MP4 is wired to Unity VideoPlayer.
- Wake-up eyelid sequence.
- Invisible boundary and final escape-sequence framework: leave forest -> road -> passing car -> hard audio silence -> sit -> END.
- Procedural wind shader and player grass-bend shader globals.
- 520m terrain prototype with a dense placeholder conifer forest and fog.
- Main menu: PLAY / SETTINGS / EXIT and horror ambience.
- App icon asset prepared from the selected reference image.

## Art status
The included primitive trees/monsters are DEVELOPMENT PLACEHOLDERS only. They are generated automatically so every gameplay system can be tested before final production assets are imported.

The real Locust and Boiled One models were intentionally not scraped from Sketchfab. Their import slots and links are in:
`Assets/FallenForest/ThirdParty/DoctorNowhere/README_IMPORT_MODELS.txt`

The project already contains real 2K PBR source maps for forest ground and pine bark derived from CC0 Poly Haven assets.

## First open
When Unity finishes compiling the scripts, `FallenForestProjectBuilder` automatically creates:
- `Assets/FallenForest/Scenes/MainMenu.unity`
- `Assets/FallenForest/Scenes/Forest.unity`
- prototype prefabs/materials/terrain data
- Android player settings/build scene list

If auto-bootstrap was interrupted, run:
**Fallen Forest -> Rebuild Prototype Scenes**

To build on a machine with Unity Android Build Support installed:
**Fallen Forest -> Build Android APK**

## Recommended editor
Unity 6 LTS + Android Build Support + SDK/NDK/OpenJDK.

## Important
This environment does not currently contain a licensed Unity Editor, so this package could be authored here but the Unity APK could not be genuinely compiled/tested here. Do not mistake a non-Unity APK for a Unity build.
