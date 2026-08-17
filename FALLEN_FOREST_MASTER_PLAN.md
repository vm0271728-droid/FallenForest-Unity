# FALLEN FOREST — MASTER PLAN / CANONICAL HANDOFF

> **Canonical source of truth for continuing development in another chat.**
> Read this file before changing the project. Then inspect the latest GitHub branch heads and the newest GitHub Actions run, because CI status can change after this document was written.
>
> Last synchronized with the conversation: **2026-08-17 06:05 Europe/Moscow (+03:00)**.
>
> Repository: `vm0271728-droid/FallenForest-Unity`
> Default branch: `main`
> Unity: **6000.0.76f1 / Unity 6 URP**
> Android package: `com.fallenforest.horror`

---

## 0. NON-NEGOTIABLE CONTINUATION RULES

1. The end goal is a **real, installable Android APK**, not a design document and not a tiny fake demo.
2. Never present the old ~1.2 MB hand-built alpha APKs as the final game.
3. Final APK size must come from **real content only**. User expects roughly **400 MB minimum**, preferably **500–900 MB**, and >1 GB is acceptable only when justified by real models/textures/audio/video. **Never pad with junk.**
4. Target playtime: roughly **45–70 minutes / about one hour**.
5. User works mainly from an Android phone and may not have a PC available. Prefer GitHub/CI workflows and phone-friendly upload paths.
6. Do not claim a feature is compiled/tested if it has only been written to source. Be explicit about what is staged, merged, compiled, device-tested, or still pending.
7. When a GitHub Actions build is running on `main`, avoid commits under `Assets/**`, `Packages/**`, `ProjectSettings/**`, `Tools/**` or `.github/workflows/android-apk.yml` unless intentionally restarting it. The workflow uses `cancel-in-progress: true`.
8. Root documentation files are safe to change without triggering the Android workflow because the workflow path filter does not include root `*.md` files.
9. User has approved searching online for useful assets/sounds/models, but licensing must be checked and documented.
10. Current preferred creature models have **non-commercial restrictions**. Do not silently treat them as commercial-ready assets.
11. Future multiplayer is **LAN/Wi-Fi only**, 2–4 players, and must remain disabled in the current game until explicitly requested.
12. Preserve every fixed design decision in this document unless the user explicitly changes it later.

---

## 1. GAME IDENTITY

**Title:** `Fallen Forest`

**Genre:** first-person 3D atmospheric/survival horror.

**Core fantasy:** the player wakes alone in a nearly black, dense conifer forest with no exposition, finds a flashlight, searches for 10 documents, survives recurring encounters with the Locust and one rare Boiled One event, then escapes to an old road during a final chase.

**Weapons:** none.

**Primary useful light source:** flashlight.

**Tone:** oppressive darkness, realistic forest ambience, tension from limited visibility and enemy observation rather than constant combat.

---

## 2. TARGET PLATFORM / TECHNICAL BASE

- Unity 6, URP.
- Android primary target.
- IL2CPP.
- ARM64.
- Minimum Android SDK currently planned: 23.
- Landscape orientation intended.
- Input System package.
- Target package: `com.fallenforest.horror`.
- Current planned version in CI builder: `1.0.0`, version code `10000`.
- Build output path currently expected by CI: `Builds/Android/Fallen_Forest_1.0.0.apk`.

### Quality / rendering intent

- 2K runtime textures where practical for Android.
- ASTC compression; prior intent was roughly 4x4 for important faces/normals and 6x6 for broader environment textures.
- URP render scale around 1.
- 2x MSAA.
- Moderate mobile shadow distance (local full project used ~58m).
- Tonemapping/grade intent: ACES, vignette, film grain, restrained bloom, minimal chromatic aberration, SMAA/AA where performance allows.
- Adaptive shadow/LOD strategy for Android.

---

## 3. STARTUP FLOW / LOADING / WARNING

Preferred scene flow:

`Splash / Loading -> Disclaimer -> Main Menu -> Loading -> Forest`

### Loading screen — fixed design

- Dark atmospheric forest background.
- Large title: `FALLEN FOREST`.
- **Only** status text: `Загрузка...`
- Do **not** show `Подготовка леса...` or `Инициализация окружения...`.
- Thin modern progress bar:
  - strict bottom edge of screen;
  - full screen width;
  - no outline/border;
  - smooth, not segmented;
  - tied to real `AsyncOperation` progress, no fake random progress.
- Current controller attempts to load `Resources/UI/loading_forest`; if binary is absent it falls back to a dark background.

Committed on main before later CI fixes:
- `Assets/FallenForest/Scripts/UI/LoadingScreenController.cs`
- `Assets/FallenForest/Scripts/Core/SceneFlow.cs`

### Startup disclaimer — fixed requirement

Must warn about:
- jumpscares;
- sudden/loud sounds;
- flashing visual effects;
- photosensitive epilepsy risk.

Button: `ПРОДОЛЖИТЬ`

Recommended final copy:

```text
ПРЕДУПРЕЖДЕНИЕ

В игре присутствуют скримеры, резкие звуки, тревожные сцены
и мигающие визуальные эффекты.

Если у вас есть фоточувствительная эпилепсия или высокая
чувствительность к подобным эффектам, рекомендуется
воздержаться от игры.

Для лучшего погружения используйте наушники.
```

Current committed `StartupDisclaimer.cs` is close but does not yet include the headphones line and can be refined later.

---

## 4. MAIN MENU

Atmosphere:
- dark forest;
- subtle motion;
- no bright arcade styling.

Buttons:
- `ИГРАТЬ`
- `НАСТРОЙКИ`
- `ВЫЙТИ`

A creepy-face reference image supplied by the user was intended for icon/menu branding (`1000703013.jpg` in the working environment at the time). Binary may need re-upload in a future chat if not persisted in GitHub.

---

## 5. OPENING SEQUENCE

Fixed opening:

1. Black screen.
2. Forest ambience + breathing.
3. Eyelids slowly open.
4. Camera is low to the ground; blur / clothes / grass nearby.
5. Player rises.
6. No exposition text.
7. Flashlight lies nearby with a subtle dim white outline.
8. Flashlight is picked up automatically — **no interaction button**.
9. Click / light comes on.
10. Gameplay begins.

Relevant script already exists: `WakeUpSequence.cs`.

---

## 6. PLAYER CONTROLS / CAMERA

### Touch controls

- No permanent cluttered button grid.
- Left: floating joystick.
- Right half: invisible look area.
- Camera sensitivity range: about `0.3–2.5`, default `1.0`.
- FOV range: `60–100`, default `75`.
- Camera shake setting: `0–100`, default around `70`.
- Input System based.

### Camera feel

- walking head bob;
- smoothing;
- subtle roll on turning/strafe;
- breathing idle movement;
- shake system;
- wake-up cinematic offsets;
- cinematic FOV support;
- forced-look support staged in Boiled feature branch.

Current main file: `Assets/FallenForest/Scripts/Player/CameraMotion.cs`.

---

## 7. FOREST / WORLD DESIGN

Approximate intended playable footprint: around **720 x 720 meters**.

### Tree composition

- Spruce dominates: roughly **60–75%**.
- Pine secondary.
- Sparse birch.
- Dead trunks, stumps, fallen logs.
- Tree variation and lean.
- LODs.
- Existing procedural target discussed/implemented: around **3250 trees**.

### Ground dressing

- needles;
- dirt;
- moss;
- twigs;
- mud;
- small rocks;
- roots;
- trails.

### IMPORTANT: terrain must NOT be flat

Newest fixed user decision:

- The world must use a visibly **uneven terrain**, not a flat plane.
- Natural small/medium hills.
- Depressions and low areas.
- Ravines.
- Smooth rises and descents.
- Some sharper terrain breaks, but not so extreme that movement/camera becomes frustrating.
- Paths should follow/reflect terrain rather than float on a flat map.
- Small bumps and ground undulation should be visible in flashlight lighting.

### Landmarks / navigational anchors

Planned forest landmarks include:
- ravine;
- swamp/wet area;
- clearing;
- hunting tower;
- large fallen tree;
- rock formation;
- tent/campsite;
- old road;
- unusually dense forest section;
- dried stream.

Invisible world boundary should be visually masked by forest/terrain rather than obvious walls.

### Grass — newest fixed rule

Grass must look **very dense** away from trails.

- Forest floor: thick grass/vegetation, sometimes near knee height.
- Wet low areas: can be even denser.
- Near stones/roots/logs: irregular density, not uniform grid placement.
- Grass should move in wind.
- If performance allows, slight reaction/bending around the player.

**Trail density gradient:**
- center of trail: almost no grass / mostly dirt, needles, mud, small stones;
- immediate trail edges: sparse grass;
- farther from the trail: density ramps up gradually;
- no hard artificial line between path and forest.

Existing procedural world systems include `ForestScatterer` / spatial indexing work, but terrain/grass-density-by-path needs to be treated as an explicit final requirement and verified in the generated scene.

---

## 8. DOCUMENT OBJECTIVE / RUN STRUCTURE

Goal: collect **10 documents**.

- 10 active documents selected/placed from a much larger candidate pool (~160 discussed).
- Placement deterministic from a run seed.
- Avoid spawning too close to start.
- Respect spacing / slope / obstruction / tree clearance rules.
- Auto-pickup; no interaction button.
- HUD: `ДОКУМЕНТЫ X / 10`.
- Autosave every collected document.
- Death -> Continue restores same run/progress.

`SaveSystem` and `GameProgress` already exist in repo source.

---

## 9. LOCUST — RECURRING MONSTER

Role: recurring stalker / killer.

### Spawn / behavior

- Uses actual generated tree anchors where possible.
- Typical distance around 18–42m.
- Prefers hiding/peeking behind trees.
- Far flashlight exposure -> retreats/hides.
- Medium-range observation -> watches for **exactly ~4 seconds**, head tracks player.
- If allowed to get too close -> chase/attack.
- Warning distance around ~14m.
- Kill range around ~8.5m.
- Event chance decays as documents are collected using previously discussed form `(1 - 0.0285)^docs`.
- No routine random spawn after document 10; final chase takes over.

State intent:
- Hidden
- Peeking
- Observing
- Retreating
- Chasing
- Attacking

Existing `LocustAI.cs`, `MonsterDirector.cs`, `ForestSpatialIndex.cs`, `MonsterRegistry.cs` contain a substantial part of this logic.

### Locust fatal jumpscares — fixed

User explicitly required: if there are 2 audio screamers, there must be **2 different visual jumpscare patterns**.

1. `jakes-screamer.mp3`
   - centered brutal forward lunge;
   - cinematic FOV roughly ~52.

2. `the-screamer-shared-between-mallie-and-jenny.mp3`
   - different side/Bezier/sweep/snap attack composition.

`amazing-grace-analog-horror.mp3` is explicitly excluded/forbidden from the final release check.

---

## 10. BOILED ONE — RARE SPECIAL ENCOUNTER

Role: extremely rare, non-lethal psychological event.

### Spawn rules

- Roughly 5x rarer than Locust in prior weighting intent (`1` vs `2` style event weights was discussed; preserve rarity even if exact implementation is tuned later).
- Can appear around documents 2–8.
- Prefer open areas rather than tree cover.
- Only one spawn opportunity/event per run.
- **Encounter is consumed when Boiled spawns even if the player never sees it.**
- Slow head tracking.
- Does **not** kill the player.

### NEW fixed gaze-trigger sequence

Staged in branch: `feature/boiled-gaze-lock`.

Required behavior:

1. Boiled exists in the scene, slowly tracking player with head.
2. Merely shining the flashlight on it is not enough by itself.
3. If the player actually catches Boiled in the camera view / looks at it:
   - require direct visibility (no trigger through a tree/wall);
   - very short confirmation (~0.06s) to avoid one-frame false triggers.
4. Touch camera look is disabled.
5. Player movement is disabled for the forced encounter.
6. Camera automatically and smoothly locks onto **Boiled's head**.
7. Slight cinematic FOV narrowing and very subtle shake.
8. Player is forced to stare for **exactly 3 seconds**.
9. **The player falls**, not Boiled.
10. During the fall, camera continues trying to look at Boiled.
11. Eyelids close as camera drops toward ground.
12. Then fullscreen user-supplied Boiled video plays.
13. Then black.
14. Player wakes at the same location.
15. Boiled is gone.
16. This is not a death; no death screen.

Feature branch implementation touches:
- `BoiledOneEncounter.cs`
- `CameraMotion.cs`
- `WakeUpSequence.cs`
- persistent influence/glitch system below.

### Persistent Boiled influence — newest fixed rule

If the player actually completes/is affected by the Boiled encounter, then from that point until the end of the **current run**:

- small digital glitches occasionally appear on screen;
- short subtle horizontal strips;
- tiny block artifacts;
- rare mild RGB split/chromatic sliver;
- not constant and not visually overwhelming;
- mobile-friendly UI overlay rather than expensive full-screen postprocess is acceptable.

Current staged values in branch are roughly:
- intervals ~4.2–11.5s;
- burst duration ~0.055–0.18s;
- occasional paired microburst.

The influence flag is stored separately (`ff_boiled_influenced`) and survives `Continue` for the same run. It is cleared by starting a new run. Glitches should only display during forest gameplay, not in the main menu.

Staged file:
- `Assets/FallenForest/Scripts/UI/BoiledInfluenceGlitch.cs`

---

## 11. BOILED VIDEO / AUDIO

User supplied a Boiled jumpscare video archive earlier. Local extracted target used during development:

`Assets/FallenForest/Video/boiled_one_jumpscare.mp4`

Approx local size: ~8.8 MB.

The exact binary has **not** been committed to GitHub main at the time of this handoff.

`BoiledOneSequence.cs` already controls fullscreen video, blackout, same-position wakeup, and controls restoration.

---

## 12. SAVE / DEATH BEHAVIOR

### Locust death

- Fade/black/death state.
- Options: Continue / Main menu.
- Continue restores saved document progress and location/state as designed.

### Boiled

- Never a normal death.
- Video -> black -> wake at same location -> continue play.

### New run

- clears run seed/progress/Boiled influence/current collected-doc mask etc.

---

## 13. FINAL CHASE / ENDING

Trigger: after document 10.

### Final chase numbers / intent

- Player speed becomes normal * **2.15**.
- Locust final chase speed should be around **97.5% of the player's final speed**, so it remains terrifyingly close but escape is possible if player keeps moving.
- Final Locust should not behave like routine random events.

### Boundary ending

- Can be reached from any side of the forest boundary.
- Transition should guide/shift player to old road ending rather than reveal a hard invisible wall.
- Forest audio hard-cuts when reaching road at the right cinematic moment.
- Player looks back.
- Player eventually sits/rests.
- Fade.
- `КОНЕЦ`.
- Return to menu.

### Important local-only finale improvement not yet on main

A local patch was prepared to:
- destroy/remove any remaining Locust instances once ending begins so player cannot die during ending cinematic;
- detach the acquired flashlight and leave it on the ground near the road, still lit and aimed back into the forest.

Files involved locally:
- `FlashlightController.cs` (`PlaceForEnding(...)`)
- `EndSequence.cs`

These changes need careful merge against current GitHub versions later.

---

## 14. FINALE PICKUP TRUCK

User supplied archive in the current conversation:

`pickup-truck.zip`

Archive contents inspected:
- `source/Pickup Afghanistan.fbx` — ~239,500 bytes
- `textures/Pickup_Afghanistan.png` — ~16,399,266 bytes

Total archive content ~16.6 MB.

This exact binary is currently conversation-local and may need to be re-uploaded or retrieved from the user's file library in another chat if it is not automatically mounted there.

### Truck visual lighting — fixed

- glowing/emissive front lamp material;
- two real forward headlights (Spot Lights or equivalent);
- warm/neutral white, not cartoon-yellow;
- visible illumination of road, grass and terrain;
- restrained volumetric/fog impression if performance allows;
- red rear/tail lights;
- headlights should be an important cinematic light source in the dark road scene.

### Truck driving physics — fixed, not optional

The truck **must not** simply translate on rails with frozen wheels.

Staged branch: `feature/finale-pickup-vehicle`

Script added:
`Assets/FallenForest/Scripts/Cinematics/CinematicPickupVehicle.cs`

Required behavior:

- `Rigidbody` vehicle body.
- Four `WheelCollider`s.
- Suspension with spring/damper/travel.
- Body pitch/roll response over uneven road.
- Anti-roll assistance.
- Front wheels visibly steer with route direction.
- Wheels visibly rotate according to physical wheel pose/speed.
- Driven wheels receive torque.
- Physical braking / deceleration near player.
- Route-following controller for the cinematic approach.
- Car should feel like it is actually driving over the road terrain.
- Model wheel transforms must be bound to wheel colliders after importing the user's FBX.

Current staged baseline values are only starting points and must be tuned to the actual pickup dimensions (wheel radius, mass, spring, damper, center of mass, torque, speed).

---

## 15. AUDIO

Desired soundscape:
- dense night forest ambience;
- wind;
- player footsteps;
- breathing;
- rustles / distant events;
- monster stings;
- Locust screamers;
- Boiled trigger sting;
- car pass / engine;
- hard silence transition at final road moment.

CC0 assets currently fetched by CI:
- `Assets/FallenForest/Audio/Menu/creepy_forest_menu.ogg`
- `Assets/FallenForest/Audio/Ambience/forest_ambience_cc0.mp3`
- `Assets/FallenForest/Audio/Ambience/ambient_horror_cc0.ogg`

Exact user Locust screamers expected by release validation:
- `Assets/FallenForest/Audio/Screamers/jakes-screamer.mp3`
- `Assets/FallenForest/Audio/Screamers/the-screamer-shared-between-mallie-and-jenny.mp3`

Generated local SFX that existed in the working environment but are not guaranteed on GitHub:
- `locust_near_sting.wav`
- `boiled_trigger_sting.wav`
- `car_pass_engine.wav`

Generated larger ambience existed locally as well; preserve only if useful and properly integrated.

---

## 16. MONSTER MODELS / CURRENT LICENSING

### Preferred Locust model

User-provided archive earlier:
`toe-locust-by-doumty.zip`

Contents included:
- `source/T_O_E Locust - By Doumty.fbx`
- `locust_basecolor_tex.png`
- `locust_fibers_tex.png`
- `locust_normal_tex.png`
- `locust_metallic_tex.png`
- `locust_roughness_tex.png`

FBX detected as Kaydara FBX 7400. String inspection indicated animation stack markers, but do **not** overclaim full rig/animation quality until imported and checked in Unity.

License/source known during development:
- Sketchfab author: Doumty
- license: **CC BY-NC-ND**
- attribution required;
- non-commercial;
- no derivatives.

Therefore this model is safe only for an unchanged free/noncommercial prototype unless license permission changes. Replace for a commercial release.

### Preferred Boiled model

User-provided archive earlier:
`the-boiled-one-horror-game-boiled-one.zip`

Contains `source/BoiledOne.fbx` plus body/eyes/teeth/gums/details textures.

FBX detected as Kaydara FBX 7400.

License/source known during development:
- Sketchfab author: MG Rips
- license: **CC BY-NC**
- attribution required;
- non-commercial;
- derivatives/modification allowed under license but still non-commercial.

Replace/get permission for commercial final release.

### Credits file issue

`CREDITS_AND_LICENSES.md` was known to be stale and may still list earlier rejected/older model selections. Update it before a real release, but do not erase the actual restrictions above.

---

## 17. WORLD / PROCEDURAL SYSTEMS ALREADY DEVELOPED

Substantial code exists for:

### `ForestSpatialIndex.cs`
- spatial hash for generated trees;
- nearby tree cover/open-area queries;
- Locust can spawn using actual tree locations;
- Boiled can prefer open space.

### `ForestScatterer.cs`
Previously developed intent/implementation includes:
- deterministic seed;
- thousands of trees;
- dense grass batches/clumps;
- clustered noise distribution;
- spacing/start clearing;
- tree variation/lean;
- registration into spatial index.

### `MonsterDirector.cs`
- Locust tree anchors;
- Boiled open-area placement;
- spawn rings/distances;
- rear weighting;
- authored fallback points;
- Boiled run consumption rule.

### `MonsterRegistry.cs`
Uses active sets to avoid repeated expensive `FindObjects` scans where possible.

All of these need a real Unity compile/import pass before being called fully verified.

---

## 18. FUTURE LAN CO-OP — ARCHITECTURE ONLY, DO NOT ENABLE YET

User explicitly requested the possibility be reserved now but **not added to the playable game yet**.

Branch:
`future/lan-multiplayer-foundation`

Files:
- `Assets/FallenForest/Scripts/Networking/LanMultiplayerFoundation.cs`
- `LAN_MULTIPLAYER_PLAN.md`

### Fixed future multiplayer rule

- 2–4 players.
- **Local network / Wi-Fi only.**
- One player hosts.
- Others discover host by LAN broadcast or local IPv4 connection.
- No Internet matchmaking.
- No Relay.
- No cloud accounts.
- No public-IP direct play.
- No dedicated server requirement.

### Architecture reserved

- stable `PlayerId`;
- player roster abstraction instead of hardcoding one global player;
- shared document authority;
- monster target selector among players;
- transport adapter abstraction;
- LAN-only policy;
- session context that remains `SinglePlayer` today.

`LanOnlyNetworkPolicy.RuntimeEnabled` is intentionally false.

### Future co-op gameplay intent

- documents are shared team progress `10/10`;
- Locust can target different players;
- Boiled can become a player-specific manifestation while host records authoritative state;
- final chase can trigger globally after doc 10 while each client keeps own camera/UI presentation;
- one player's death should not automatically terminate the whole session unless design is changed later;
- LAN session state should not corrupt single-player saves.

Reserved default ports in planning:
- game: `7777`
- discovery: `47777`

Do not choose/install a networking SDK until actual LAN implementation starts.

---

## 19. CURRENT GIT BRANCH STRATEGY / STAGED FEATURES

### `main`
Current production/integration branch.

At time of this handoff, latest known main commit before this documentation commit was:
`0f0f84c0ba73349a51d82cf309401071c87d06f6`

Message:
`Work around Unity CLI Android NDK extraction on fresh runners`

### `feature/boiled-gaze-lock`
Contains:
- gaze-based trigger;
- camera lock to head;
- 3-second forced stare;
- player collapse/eyelid close;
- persistent Boiled influence flag;
- lightweight glitch overlay.

Known staged commits in this branch include:
- `a6c55b587f0774d2a4ca649c02cdd0b69b346c2a` — gaze lock/collapse foundation;
- later commits add save influence flag and glitch overlay.

Do not assume this is already in `main`.

### `feature/finale-pickup-vehicle`
Contains physics-driven cinematic pickup controller with WheelCollider suspension, steering, wheel spin and lights.

Known initial commit:
`0875668805a5fab6c62f35ed7ab20517e9538f85`

Do not assume this is already in `main`.

### `future/lan-multiplayer-foundation`
Architecture-only LAN reserve. Current game stays single-player.

---

## 20. CI / ANDROID BUILD STATUS — IMPORTANT BLOCKER AT HANDOFF

Workflow:
`.github/workflows/android-apk.yml`

Workflow name:
`Build Fallen Forest APK`

Unity secrets are configured as GitHub Actions secrets:
- `UNITY_USERNAME`
- `UNITY_PASSWORD`

Never ask user to paste the password into chat.

### What works in CI

- checkout/LFS step;
- CC0 audio download;
- secret presence verification;
- disk cleanup;
- official Unity CLI beta installer;
- Unity Editor `6000.0.76f1` installation.

### Latest known run at handoff

Run **#25**, run ID `31988273488`, finished **failure**.

Failure step:
`Install Android Build Support and child modules`

The Unity CLI beta successfully installed many child modules:
- OpenJDK 17.0.18+8;
- CMake 3.22.1;
- Android build tools 36.0.0;
- platform tools 36.0.0;
- SDK platforms 34/35/36;
- command-line tools 16.0;
- Android SDK & NDK Tools wrapper.

But it still failed on:
- `android`
- `android-ndk-r27c`

Exact recurring error:

```text
Could not find a part of the path
/home/runner/Unity/Hub/Editor/6000.0.76f1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK/android-ndk-r27c
```

Creating only the `NDK` parent directory did **not** solve it.

There was plenty of disk space (~94 GB free at failure), so this is not a disk-capacity issue.

### Next CI engineering task

Do not keep blindly retrying the same command. Investigate a more robust installation route for Android Build Support/NDK, for example:
- verify exact Unity CLI beta `install-modules` extraction behavior and expected folder semantics;
- test pre-creating the exact `NDK/android-ndk-r27c` path only if consistent with official installer behavior;
- consider using Unity Hub/headless module installer route instead of current beta CLI if available;
- consider a proven Unity CI image/action with Android module already present if licensing/Unity Personal activation remains compatible;
- validate NDK `source.properties` after install before moving on.

Once Android modules pass, next possible blockers are:
1. Unity Personal cloud activation;
2. project compile/import;
3. release-media/model preflight;
4. missing scenes in GitHub source;
5. actual Android player build.

---

## 21. KNOWN BUILD/PROJECT BLOCKERS AFTER TOOLCHAIN

Even after CI gets Android support installed, do **not** assume APK will build immediately.

Known issues:

1. Repo `FallenForestProjectBuilder.cs` on main is a small CI builder and currently expects scenes:
   - `Assets/FallenForest/Scenes/MainMenu.unity`
   - `Assets/FallenForest/Scenes/Forest.unity`
2. Those scenes were known to be absent from GitHub main at handoff.
3. A much larger local project builder existed in `/mnt/data/FallenForest_Unity_Full/...` and could generate scenes/materials/world, but it was not fully transferred to GitHub.
4. Exact creature FBXs/textures are not yet integrated into GitHub release paths.
5. Exact Locust screamers are not yet guaranteed in GitHub.
6. Exact Boiled MP4 is not yet guaranteed in GitHub.
7. Pickup-truck FBX/texture is not yet in GitHub.
8. Loading forest background binary is not yet guaranteed in GitHub.
9. No successful full Unity compile/import has yet validated all current C# source.
10. No final APK has been device-tested.

### Local project snapshots that existed during development

- `/mnt/data/FallenForest_Unity_FinalStage` (~122 MB)
- `/mnt/data/FallenForest_Unity_Full` (~100 MB)
- `/mnt/data/FallenForest_Unity_DevStart` (~95 MB)

These paths are session/container-local and may not exist in another chat. Treat GitHub as persistent source; if required binary/local-only content is gone, ask user to re-upload rather than pretending it exists.

---

## 22. RELEASE MEDIA PREFLIGHT CURRENTLY EXPECTED BY CI

The workflow checks for:

```text
Assets/FallenForest/Audio/Menu/creepy_forest_menu.ogg
Assets/FallenForest/Audio/Ambience/forest_ambience_cc0.mp3
Assets/FallenForest/Audio/Ambience/ambient_horror_cc0.ogg
Assets/FallenForest/Audio/Screamers/jakes-screamer.mp3
Assets/FallenForest/Audio/Screamers/the-screamer-shared-between-mallie-and-jenny.mp3
Assets/FallenForest/Video/boiled_one_jumpscare.mp4
Assets/FallenForest/Art/Models/DoctorNowhere/Locust/<3D file>
Assets/FallenForest/Art/Models/DoctorNowhere/Boiled/<3D file>
```

And rejects:

```text
Assets/FallenForest/Audio/Screamers/amazing-grace-analog-horror.mp3
```

The exact model directory naming (`DoctorNowhere`) is legacy and may be renamed later, but if renamed the workflow must be updated consistently.

---

## 23. ASSET / GIT LFS NOTES

`.gitattributes` is configured to LFS common binary art/audio formats including FBX/OBJ/BLEND/WAV/MP3/OGG/MP4/PNG/JPG/etc.

This means large exact user binaries should preferably be added through a proper Git/LFS-capable path. The current connector can manipulate GitHub text well, but huge base64 transfers are impractical and should not be faked.

For phone-only workflow, practical options are:
- user uploads binaries to GitHub via mobile/browser if manageable;
- CI downloads publicly licensed assets from stable sources;
- if a service requires an API token, store token as GitHub secret rather than chat plaintext.

---

## 24. PERFORMANCE GOALS FOR ANDROID

Because the scene is dense, final integration must be performance-aware:

- grass batching/chunking;
- tree LODs;
- spatial indexing rather than global searches;
- conservative dynamic shadow distance;
- occlusion/frustum strategies where appropriate;
- avoid expensive full-screen effects for the persistent Boiled glitch;
- ASTC compressed textures;
- avoid keeping 4K originals resident when 2K runtime import is sufficient;
- pool/reuse effects where useful;
- profile on real Android hardware before calling release final.

Do not reduce the forest to a sparse empty scene merely to hit FPS; preserve the user's dense-horror look and optimize intelligently.

---

## 25. CONTENT SIZE PHILOSOPHY

User strongly rejected tiny demos and expects the finished game to feel like a substantial real project.

Desired final installed/APK scale:
- minimum expectation ~400 MB;
- ideal ~500–900 MB;
- >1 GB acceptable if real content warrants it.

Valid reasons for size:
- real 2K textures;
- multiple tree/grass/environment assets;
- monster models and textures;
- audio ambience and SFX;
- jumpscare videos;
- final truck model/texture;
- actual scene/world data.

Invalid reason:
- dummy filler / random bytes / padding.

---

## 26. ORDER OF DEVELOPMENT FROM THIS HANDOFF

Recommended next sequence:

1. **Fix Android toolchain installation** in GitHub Actions until AndroidPlayer + NDK + SDK + JDK validation passes.
2. Run Unity activation step and fix only the real next error.
3. Reach first genuine project compile/import; repair all C# compile errors.
4. Transfer/adapt scene-generation logic or create committed `MainMenu.unity` + `Forest.unity` so the CI builder has real scenes.
5. Merge `feature/boiled-gaze-lock` after compile review.
6. Merge `feature/finale-pickup-vehicle` after compile review.
7. Integrate pickup model and bind four wheel transforms/colliders, tune suspension to actual dimensions.
8. Ensure terrain is genuinely uneven and visually natural.
9. Implement/verify dense grass with path-distance density falloff.
10. Integrate exact preferred Locust/Boiled models with correct materials and documented licenses.
11. Integrate exact two Locust screamers and two distinct visual attack patterns.
12. Integrate Boiled video and persistent influence glitch.
13. Refine loading background/disclaimer/menu assets.
14. Merge local finale improvements: remove Locust during ending, leave lit flashlight on road pointing into forest.
15. Complete road/car cinematic with headlights, tail lights, vehicle suspension/steering/spin.
16. Run release preflight.
17. Build first real Android APK.
18. Download workflow artifact, extract actual `.apk`, do not give user only the artifact ZIP if they asked for APK.
19. Device test: boot, touch controls, save/continue, monster triggers, video playback, performance, final chase/end scene.
20. Optimize, fix bugs, rebuild until release-quality.

---

## 27. ACCEPTANCE CHECKLIST — GAMEPLAY

Do not call Fallen Forest finished until all of these are true:

- [ ] App launches on Android.
- [ ] Warning screen displays and can continue.
- [ ] Main menu works.
- [ ] Loading screen uses real async progress and bottom full-width bar.
- [ ] Opening wake-up sequence works.
- [ ] Flashlight auto-pickup works.
- [ ] Touch joystick and camera look work.
- [ ] Terrain is clearly uneven, not flat.
- [ ] Forest is dense and dark.
- [ ] Grass is genuinely thick away from paths.
- [ ] Grass density falls off near paths and is minimal on path center.
- [ ] 10 documents can be collected and HUD updates.
- [ ] Save/Continue restores run.
- [ ] Locust hides/peeks/observes/retreats/chases as intended.
- [ ] Two Locust audio files map to two visibly different jumpscare patterns.
- [ ] Boiled spawns rarely and only once per run.
- [ ] Boiled gaze trigger requires actual visible look.
- [ ] Camera locks onto Boiled head.
- [ ] Forced stare lasts ~3 sec.
- [ ] Player falls/eyes close while still looking toward Boiled.
- [ ] Boiled fullscreen video plays.
- [ ] Player wakes same place and Boiled is gone.
- [ ] Boiled encounter does not count as death.
- [ ] Persistent small glitches appear afterward until run end/continue.
- [ ] Glitches do not appear merely because Boiled spawned unseen.
- [ ] Document 10 starts final run.
- [ ] Player final speed is 2.15x normal.
- [ ] Final Locust speed around 97.5% of player's final speed.
- [ ] Ending cannot be interrupted by an unintended Locust death.
- [ ] Old road finale works.
- [ ] User pickup truck model is used.
- [ ] Truck headlights illuminate terrain.
- [ ] Tail lights work.
- [ ] Truck uses real Rigidbody/WheelCollider physics.
- [ ] Suspension visibly compresses/rebounds over road unevenness.
- [ ] Front wheels visibly steer.
- [ ] Wheels visibly spin.
- [ ] Truck approaches/brakes naturally near player.
- [ ] Flashlight can be left lit on road aimed toward forest in ending.
- [ ] `КОНЕЦ` and return to menu work.

---

## 28. ACCEPTANCE CHECKLIST — TECHNICAL / RELEASE

- [ ] Current project compiles in Unity 6000.0.76f1.
- [ ] Android Build Support fully installed in CI.
- [ ] NDK contains valid `source.properties`.
- [ ] Unity Personal activation works on runner.
- [ ] MainMenu and Forest scenes exist in GitHub/CI checkout.
- [ ] All final release media exists.
- [ ] All used licenses documented.
- [ ] No forbidden `amazing-grace` screamer included.
- [ ] IL2CPP ARM64 APK builds.
- [ ] Actual APK artifact uploads.
- [ ] APK installs on a physical Android device.
- [ ] No missing pink materials / broken shaders.
- [ ] No runtime exceptions on start.
- [ ] Video works on Android codec stack.
- [ ] Touch input works after cutscenes.
- [ ] Save/Continue works after app restart.
- [ ] Stable performance in dense grass/forest.
- [ ] Build size reflects real content, no filler.

---

## 29. THINGS THAT ARE NOT FINISHED / MUST NOT BE OVERCLAIMED

At this handoff, the following are **not proven final**:

- Android APK build pipeline is still blocked at NDK installation.
- No successful full project compile/import of all newest code.
- Preferred Locust/Boiled binary models not yet integrated into main release paths.
- Pickup-truck binary not yet integrated into GitHub.
- Exact Boiled MP4 not yet integrated into GitHub release checkout.
- Exact Locust screamers not yet guaranteed in GitHub release checkout.
- Loading forest background binary not yet guaranteed.
- Main GitHub builder expects scenes that are absent.
- Local giant scene builder was not fully transferred.
- Terrain/grass final art pass is not complete.
- Boiled gaze/glitch feature is staged on a feature branch, not necessarily merged.
- Truck physics controller is staged on a feature branch, not necessarily merged.
- Finale flashlight/Locust cleanup patch is local-only and not merged.
- Device performance/testing is not done.
- Current creature licenses are not commercial-ready.
- Multiplayer is architecture-only and intentionally disabled.

---

## 30. QUICK INSTRUCTION FOR A NEW CHAT

If the user says something like **“продолжай Fallen Forest”** in another chat:

1. Connect to GitHub repo `vm0271728-droid/FallenForest-Unity`.
2. Read this entire file first: `FALLEN_FOREST_MASTER_PLAN.md`.
3. Inspect latest `main` commit and branch heads:
   - `feature/boiled-gaze-lock`
   - `feature/finale-pickup-vehicle`
   - `future/lan-multiplayer-foundation`
4. Inspect newest GitHub Actions run; do not assume run #25 is still current.
5. If latest build failed, fetch exact job logs and fix the concrete failure rather than guessing.
6. Preserve all fixed game-design decisions in this file.
7. If a required user binary is missing in the new chat/container, search the user's connected file library if available; otherwise ask for that specific binary to be re-uploaded. Never claim to have it if it is not accessible.
8. Continue toward the real Android APK, not another placeholder demo.

---

## 31. USER'S LATEST DESIGN EMPHASIS

The most recent explicit points to protect are:

- terrain must visibly be **not flat**;
- grass must be **very dense** in forest and clearly **sparser near paths**;
- Boiled influence must leave small glitches until the end of that run;
- Boiled gaze scene must seize camera and stare at the head for 3 sec before the player collapses;
- finale truck must have real headlights and tail lights;
- truck approach must have actual suspension, steering wheel angle and wheel rotation;
- future co-op is LAN-only and not playable yet;
- the entire plan must remain in this canonical file so development can continue in another chat.

---

**END OF CANONICAL HANDOFF**
