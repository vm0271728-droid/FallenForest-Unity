# FALLEN FOREST — LATEST CANON ADDITIONS

This file records design decisions added after `FALLEN_FOREST_MASTER_PLAN.md` was created. A future continuation must read both files until these points are folded back into the master plan.

Last synchronized: 2026-08-17 09:25+ Europe/Moscow (+03:00).

## Canonical FPS / monster animation specification — NEW

A dedicated canonical implementation document now exists:

`FALLEN_FOREST_FPS_MONSTER_ANIMATION_TZ.md`

For first-person arms, flashlight presentation, document pickup, viewmodel/FOV rules, Boiled One gaze behaviour, Locust hiding/rage/locomotion and both player death animations, **that file is the newest detailed specification and overrides older conflicting wording in the master plan**.

Critical newest rules include:
- normal gameplay world FOV fixed at 75°; remove the old freely adjustable player FOV setting;
- hands/flashlight use a dedicated fixed-FOV viewmodel presentation, tuned around 60–62° and framed so no missing torso/arm cutoffs are visible;
- flashlight hand has controlled horizontal/vertical camera-turn lag, spring return, tiny overshoot and real beam lag because the light follows the flashlight rather than the camera;
- custom flashlight pickup, idle variants, walk/run motion and left-hand document pickup are required;
- Boiled is not humanoid: it has no normal shoulders or legs and only performs a tiny slow sway;
- Boiled gaze event must be blocked by foliage/branches/trees/rocks or other real visual occlusion;
- when Boiled is truly noticed, player movement is reduced by 67% rather than disabled, breathing accelerates, mild tinnitus builds, and Boiled disappears exactly while the eyes are fully closed;
- Locust is approximately 2.3× player height, has 2 far / 1 medium / 2 close hiding animations, and distance logic remains active even while it is hiding;
- approaching a hiding/retreating Locust can trigger Rage;
- in a close encounter, backing away to roughly 85% of the configured medium-distance threshold permits disengagement;
- Locust uses its very long arms as ground supports during chase locomotion instead of running like a scaled-up human;
- both Locust death sequences include explicit FPS-hand animation and a physically dropped, still-lit flashlight;
- rear death: player is pierced from behind, drops flashlight, grabs the piercing Locust hand/arm and loses grip as strength fades;
- front death: chest impalement knocks the player down, flashlight drops, player panics/flails defensively, strength rapidly fades, then Locust brings its head close while tinnitus/red fade leads to black.

## Document placement and presentation — fixed

- There are still exactly 10 collectible documents per run.
- The **documents themselves do not have a 45% spawn chance**. All required documents must spawn according to the run-generation rules.
- Documents should be distributed across the forest map, including dense grass areas, clearings, near trees/rocks and on uneven terrain, while remaining reachable and avoiding invalid geometry, overly steep slopes, map borders and blocked locations.
- Documents should **not be intentionally placed on trail centers**; the search should pull the player off the trails and into the forest.
- If a document is placed in dense grass, vegetation around the paper/folder should be locally reduced/cleared with a soft natural falloff so the document remains readable and the clearing looks visually intentional rather than like a square hole.

## Fireflies above documents — fixed clarification

- Each already-spawned document independently has a **45% chance to receive a firefly effect above it**.
- If the effect is selected, spawn **4–6 tiny fireflies** above/around that document.
- Fireflies must glow **very dimly**. They are atmospheric detail, not a bright quest marker.
- Their light must not reveal a document from far across the forest; they should become noticeable only at relatively close range.
- Motion should be slow, irregular and organic in a small volume over the document.
- Keep the effect mobile-friendly: tiny emissive particles/billboards with either no real Light components or extremely restrained lighting.

This clarification overrides any earlier wording that could be read as “the document itself has a 45% chance to appear.”

## Implementation staged after this clarification

Branch `feature/document-fireflies-grass-clearance` currently stages the code implementation of the rules above:

- `DocumentSpawner.cs` keeps generated documents off registered `TrailZone` volumes and still selects all 10 required slots.
- Each uncollected document gets a shader-driven `GrassExclusionEmitter`, so grass clears in a soft circular falloff even when the grass is mesh-batched.
- Each spawned document independently rolls the fixed **45% firefly chance**; a successful roll creates **4–6** very small dim fireflies.
- `DocumentFireflies.cs` was further reduced for Android: default visibility is only about 12.5m, the fireflies are tiny, use a subtle scale pulse, and **do not create a real point light by default**. A physical glow exists only as an optional extremely weak setting. This keeps them atmospheric rather than a quest beacon.
- `TrailZone.cs` exposes a smooth vegetation-density gradient: effectively no grass on the path volume, sparse grass beside it, then dense grass farther into the forest.
- `ForestScatterer.cs` staging raises the default grass target from 9000 to 16000 clumps, prevents trees from blocking registered trail volumes, and applies the trail density gradient while keeping mesh batching for Android.
- `ForestWindURP.shader` staging supports soft stochastic grass suppression around documents instead of a hard square hole.
- `TrailNetworkGenerator.cs` now creates a deterministic network of narrow terrain-following dirt trails. Catmull-Rom path ribbons follow the Terrain height and every path segment creates a `TrailZone`, so document exclusion, tree exclusion and grass thinning all use the same physical trail geometry.
- Trail generation cleanup was hardened: generated ribbons/zones live under one generated root and runtime mesh objects are explicitly cleaned before regeneration.

These changes are staged on a feature branch and are **not yet claimed compiled** until the Unity Android CI reaches a real project compile.

## Uneven terrain implementation staged

Branch `feature/world-terrain-relief` contains `TerrainReliefGenerator.cs`:

- deterministic layered broad/medium/fine terrain noise;
- natural depressions and shallow ridge structure;
- moderate vertical relief rather than a flat plane;
- a blended flatter opening area around the wake-up/start point so the beginning remains playable;
- intended generation/baking before release, not expensive per-frame terrain deformation.

This terrain branch is also staged and must be integrated into the real generated/committed Forest scene before being considered final.

## Ordered world-generation integration — staged

Branch `feature/world-generation-integration` was created from the document/trail branch and also contains the uneven terrain generator.

It adds `WorldGenerationCoordinator.cs` with an early execution order and an explicit final-world sequence:

`terrain relief -> terrain-following trails -> trees + dense grass -> normal DocumentSpawner Start`

The purpose is to ensure trail meshes and their exclusion zones are based on the final uneven terrain, forest objects are generated around those trails, and document candidate validation samples the finished forest rather than stale flat-world positions. This branch is the preferred place to consolidate the terrain/trail/forest work before it is eventually compile-tested and merged.

## Finale safety and flashlight presentation — staged

Branch `feature/finale-safety-flashlight` contains two fixed ending improvements:

- `EndSequence.cs` removes any remaining active Locust instances once the boundary ending cinematic has actually begun, preventing an unintended fatal attack after player controls are taken away.
- `FlashlightController.cs` can detach the acquired flashlight during the road ending, leave it switched on on the ground and aim its beam back toward the forest.

These changes are staged, not yet compile-proven.

## Final pickup / vehicle implementation — staged

Branch `feature/finale-pickup-vehicle` contains `CinematicPickupVehicle.cs` with the required Rigidbody + WheelCollider cinematic driving foundation:

- physical wheel suspension;
- spring/damper travel;
- anti-roll assistance;
- driven-wheel torque;
- physical braking;
- visible wheel spin;
- visible front-wheel steering;
- headlights and tail-light hooks;
- automatic approach route for the road ending.

The exact user-supplied `Pickup Afghanistan.fbx` was inspected. It imports as a merged mesh rather than four ready-made wheel transforms. Branch `feature/pickup-wheel-split` therefore stages `PickupWheelMeshSplitter.cs`, which analyzes disconnected mesh islands, extracts the four wheel meshes into independent visual transforms, creates four matching WheelColliders, estimates wheel radius, adds a Rigidbody/body collider and wires the result into `CinematicPickupVehicle`. This is specifically so the final truck can show real suspension movement, steering and wheel rotation even though the source FBX did not expose separate wheel nodes.

## Exact user archive import path — staged

Branch `feature/user-archive-import` provides a phone-friendly persistent workflow for exact user media:

- `Tools/import_user_archives.py`
- `ThirdParty/UserUploads/README.md`
- `FinalCreaturePrefabBuilder.cs`

Recognized original ZIP archives include the user's Locust, Boiled, pickup, Boiled screamer video and Locust screamer archives. The importer copies only the approved two Locust screamer MP3 files and deliberately excludes/removes `amazing-grace-analog-horror.mp3` from release paths.

The creature builder creates gameplay prefabs from the exact imported FBX models rather than silently substituting placeholder monsters. Current model license restrictions remain unchanged: Locust is CC BY-NC-ND and Boiled is CC BY-NC, so they are not commercial-release-ready without permission/replacement.

## Startup warning polish — staged

Branch `feature/ui-startup-polish` updates the warning copy to the intended final text and includes the line recommending headphones for immersion. It keeps the warning about jumpscares, sudden sounds, flashing effects and photosensitive epilepsy risk.

## Current Android CI strategy — active

The earlier standalone experimental Unity CLI path repeatedly failed installing Unity 6 Android NDK r27c. `buildalon/unity-setup@v2` was also tested but incorrectly reported that Unity Hub was absent on the 2026 Ubuntu runner even after Unity Hub had installed successfully.

Direct Linux Hub CLI under `xvfb-run` is still the active strategy:
- install `unityhub` + `xvfb`;
- use Linux syntax `unityhub --headless ...`;
- set install path under `$HOME/Unity/Hub/Editor`;
- install Unity `6000.0.76f1` changeset `6f7f9e1c9e8a`;
- validate Editor executable, Android editor extension, SDK platform-tools, NDK `source.properties` and OpenJDK before activation/compile.

GitHub Actions run #30 (`31992485117`) exposed a specific hang: Unity Hub reached an interactive prompt asking whether to install child module `android-open-jdk-17.0.18+8`. Because GitHub Actions has no interactive stdin response, the install waited indefinitely. The DBus warnings printed before that prompt were not the actual blocker.

The user manually cancelled that stuck attempt. A re-run was started, then main commit `1115e8b256703054f12a7e76768e1d2d2237cab3` fixed the workflow to use the documented noninteractive Android-parent form:

`--module android --childModules`

This should install the Android SDK/NDK/OpenJDK child modules without prompting. The workflow retains `cancel-in-progress: true`, so the workflow-changing commit should supersede the redundant rerun. Do not assume success in another chat: fetch the newest Actions run and inspect the actual steps first.

## Current integration rule

Do not merge the staged feature branches into `main` merely to make progress appear larger. First get the CI toolchain to a genuine Unity project compile. Then merge/test feature branches in controlled groups so actual compiler/runtime failures can be attributed and fixed instead of hiding several independent regressions in one build.
