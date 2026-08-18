# FALLEN FOREST — FULL CANONICAL TECHNICAL SPECIFICATION

Last updated: 2026-08-17.

This file is the single consolidated canonical specification for Fallen Forest. When this file conflicts with older design documents, this file takes precedence unless the user explicitly changes a rule later.

Repository: `vm0271728-droid/FallenForest-Unity`
Working integration branch: `integration/release-mainline`
Engine: Unity `6000.0.76f1` / Unity 6 URP
Primary platform: Android
Package: `com.fallenforest.horror`

---

## 1. Game identity

**Title:** Fallen Forest.

**Genre:** first-person atmospheric / survival / psychological horror.

**Core premise:** the player wakes alone in an almost completely dark conifer forest, finds a flashlight, explores the forest and collects exactly 10 documents while surviving recurring encounters with Locust, one rare Boiled One encounter and rare hallucination events. After document 10, the game transitions into the final Locust chase toward an old road and the ending sequence.

**Weapons:** none.

**Target playtime:** approximately 45–70 minutes.

The game must create tension through darkness, limited visibility, forest ambience, distance, uncertainty and observation rather than constant combat or constant jumpscares.

---

## 2. Technical platform

- Unity 6 / `6000.0.76f1`.
- URP.
- Input System.
- Android primary target.
- IL2CPP.
- ARM64.
- Minimum Android SDK 23.
- Landscape orientation.
- Package: `com.fallenforest.horror`.
- Planned release version: `1.0.0`.
- Version code: `10000`.
- APK output: `Builds/Android/Fallen_Forest_1.0.0.apk`.

APK size must come from real game content. Never pad the package with junk files. The target project may naturally become several hundred megabytes when the real models, textures, animation, audio and video content are included.

---

## 3. Overall visual direction

The target is a believable dark forest rather than a visibly placeholder mobile demo.

Required qualities:
- realistic proportions and materials;
- dark cold nighttime palette;
- physically believable tree bark, foliage, dirt, mud, needles and moss;
- PBR materials where the supplied content supports them;
- dense volumetric-looking atmosphere created with mobile-friendly methods;
- restrained fog;
- very limited artificial light;
- strong contrast between the flashlight beam and the forest outside it;
- no cartoon or arcade styling.

Post-processing may use restrained ACES/tonemapping, vignette, film grain, mild bloom and authored special effects. Permanent excessive chromatic aberration, blur or heavy glitch effects are not allowed.

---

## 4. World size and terrain

Approximate playable footprint: **720 × 720 meters**.

The terrain must not be flat.

Required relief:
- low and medium hills;
- natural depressions;
- shallow ravines;
- gradual rises and descents;
- occasional sharper terrain breaks where traversal remains comfortable;
- visible local ground undulation in flashlight lighting.

The initial wake-up area may be slightly flatter for playability, but it must still belong naturally to the surrounding terrain.

Trails must follow the final Terrain height instead of floating above a flat world.

Potential landmarks include:
- ravine;
- wet / swampy low area;
- clearing;
- hunting tower;
- large fallen tree;
- rock formation;
- old campsite or tent;
- dried stream;
- unusually dense forest section;
- old road used in the ending.

World boundaries must be visually hidden by forest, terrain and fog instead of obvious walls.

---

## 5. Trees — strict asset rule

**Only the tree models supplied by the user are allowed for the final forest.**

Do not manufacture primitive cylinders, temporary spruce meshes or unrelated replacement tree models for the final release forest.

The supplied tree packs must be divided into sensible near / mid / far use according to their available LODs and mesh density. High-detail models are for the near field and important silhouettes; lighter LODs/models are used farther away.

### Tree collision — mandatory

Every gameplay tree that has a solid trunk must have a proper collision shape around the **trunk**, not around the full canopy.

Requirements:
- player cannot walk through the trunk;
- Locust/tree cover logic can rely on the trunk as real solid cover;
- branches and leaves must not create huge invisible collision walls;
- prefer simple capsule / cylinder / low-poly convex trunk colliders instead of MeshCollider on the entire tree;
- collider dimensions must visually match trunk width and usable trunk height;
- LOD switching must not cause collision to disappear or jump;
- tree collision must remain active at relevant gameplay distances even if the visual model changes LOD.

Where foliage needs to block a Boiled gaze check, use dedicated lightweight visibility occlusion volumes instead of turning the entire foliage mesh into expensive physical collision.

---

## 6. Grass and forest floor

The currently approved user grass remains the grass source. Do not replace it when integrating the supplied tree packs.

Away from trails the forest floor should feel dense and overgrown.

Ground dressing includes:
- dense grass;
- needles;
- dirt;
- moss;
- mud;
- twigs;
- small rocks;
- roots;
- fallen organic debris.

Wet low areas may be even denser.

### Trail vegetation gradient

- centre of trail: almost no grass;
- immediate edge: sparse grass;
- farther from edge: gradual density increase;
- deep forest: dense vegetation.

No hard rectangular or perfectly straight vegetation cutoffs.

---

## 7. Ambient darkness and flashlight exposure — new canonical rule

The game must **not** become literally pitch black when the flashlight is off. There is still natural night illumination from sky/moon/ambient contribution, but it is extremely weak.

### Flashlight OFF

The player should be able to barely perceive:
- nearby tree silhouettes;
- rough terrain shape;
- the closest vegetation masses;
- large nearby landmarks.

The image should remain frighteningly dark. Ambient lighting must never make the flashlight optional.

### Flashlight ON

When the flashlight is active, the visual system should mimic real eye/camera adaptation to the bright beam:
- the flashlight beam becomes the dominant exposure reference;
- weak ambient forest illumination outside the beam becomes much less visible;
- distant background may appear almost completely black while the bright beam is in view;
- ambient light does not need to be physically deleted; the perceived disappearance should come primarily from exposure / adaptation / contrast behaviour;
- transitions must be smooth enough not to look like the world light is toggled by a script.

Turning the flashlight off should allow the player's vision to slowly recover toward the weak natural-night baseline instead of snapping instantly to a brighter image.

This is a core atmosphere requirement.

---

## 8. Startup flow

Preferred flow:

`Splash / Loading -> Disclaimer -> Main Menu -> Loading -> Forest`

### Disclaimer

Warn about:
- jumpscares;
- sudden/loud sounds;
- flashing effects;
- photosensitive epilepsy risk.

Also recommend headphones for immersion.

### Loading screen

- dark forest background;
- large `FALLEN FOREST` title;
- localized `Loading...` / `Загрузка...` only;
- thin progress bar at the strict bottom edge;
- full screen width;
- no outline;
- tied to real scene loading progress, not fake random progress.

---

## 9. Main menu

Atmosphere:
- dark forest;
- subtle environmental movement;
- restrained fog / depth / parallax;
- premium minimal horror presentation;
- no obvious default Unity styling.

Main actions:
- PLAY;
- SETTINGS;
- CREDITS.

Android does not require QUIT as a prominent primary menu action.

PLAY is visually dominant.

Figma is the design source for final menu/HUD compositions; Unity implementation must follow the approved design while respecting Android safe areas and touch targets.

---

## 10. Localization

Default language: **English**.

Initial languages:
- English;
- Русский.

Selected language persists between launches.

Localization covers:
- startup warning;
- main menu;
- settings;
- loading;
- HUD;
- documents / prompts;
- death/continue UI;
- ending;
- credits.

Localization must be data-driven and extensible.

---

## 11. Opening sequence

1. Black screen.
2. Forest ambience and breathing.
3. Eyelids slowly open.
4. Camera is low near the ground.
5. Grass / clothes / terrain are close to camera.
6. Player begins to rise.
7. No exposition text.
8. A flashlight lies nearby with only a subtle visual cue.
9. Pickup is automatic; there is no interaction button.
10. The hand physically reaches for the flashlight.
11. The flashlight is lifted rather than teleported.
12. Thumb operates the power control.
13. Audible click.
14. Light turns on with a tiny believable delay.
15. Normal gameplay begins.

---

## 12. Player controls

Touch layout:
- left side: floating joystick;
- right side: invisible look zone.

No permanent cluttered button grid.

Sensitivity:
- range approximately `0.3–2.5`;
- default `1.0`.

Camera shake:
- range `0–100`;
- default approximately `70`.

### FOV

Normal gameplay world FOV is fixed at **75°**.

There is **no user FOV setting**.

Temporary authored FOV changes are allowed for wake-up, Boiled, death/jumpscare and ending sequences.

---

## 13. Camera feel

Required:
- subtle breathing idle movement;
- human-feeling walking head movement;
- smoothing;
- mild roll / lean on turns and movement;
- authored shake when appropriate;
- cinematic overrides for scripted sequences.

The effect must remain comfortable on a phone and must not create excessive motion sickness.

---

## 14. FPS viewmodel and hands

Use the supplied first-person arms asset.

Hands and flashlight render on a dedicated viewmodel camera/layer.

Target viewmodel FOV: approximately **60–62°**.

Validate framing on common mobile ratios including 16:9, 18:9 and 20:9.

Never expose:
- missing torso;
- cut arm ends;
- empty space behind the rig.

Animation architecture should combine:
- skeletal animation;
- breathing/tension additive layer;
- procedural camera-turn lag;
- walk/run movement;
- interaction overrides;
- death overrides.

The flashlight is not a gun and must not behave like a generic FPS weapon.

---

## 15. Flashlight

Use the supplied flashlight model and its available PBR texture set.

The real gameplay Light follows the physical flashlight orientation rather than the camera directly.

### Turn inertia

When camera turns:
1. camera starts first;
2. forearm lags slightly;
3. wrist/flashlight lag a little more;
4. flashlight catches up;
5. tiny overshoot is allowed;
6. damping returns to neutral.

Horizontal lag is stronger than vertical lag.

Clamp offsets so touchscreen swipes never throw the viewmodel off-screen.

---

## 16. Flashlight idle / movement animation

Required at minimum:

### Base idle
- approximately 4–6 s seamless cycle;
- breathing;
- tiny wrist corrections;
- subtle grip tension changes;
- slight beam drift.

### Idle variant A — grip correction
- lower slightly;
- fingers relax sequentially;
- small rotation in palm;
- grip retightens;
- return to base.

### Idle variant B — tension
- hand drops a little;
- deeper breath;
- nervous wrist correction;
- grip tightens;
- return.

### Rare micro variant
- thumb/finger checks the flashlight body or switch without switching it off.

### Walking
Combine footsteps, breathing, turn lag, item inertia and slight asymmetry. Avoid a perfect repeating sine-wave weapon bob.

### Running
- flashlight moves slightly lower/closer to body;
- stronger but controlled motion;
- beam becomes less stable while remaining useful;
- stopping has inertial settling.

---

## 17. Flashlight pickup animation

Target duration: approximately **2.3–2.8 s**.

Sequence:
1. hand reaches toward real flashlight position;
2. first touch can nudge the object slightly;
3. fingers close around real geometry;
4. object lifts with visible weight;
5. wrist reacts to weight;
6. left hand briefly helps adjust grip if needed;
7. right hand rotates to gameplay pose;
8. thumb presses power control;
9. click plays;
10. light turns on with tiny delay;
11. blend to idle.

Do not teleport the object into the hand.

---

## 18. Documents

Every run contains **exactly 10 collectible documents**.

The system may choose them from a much larger deterministic candidate pool, around 160 candidates.

Valid placement:
- away from immediate start;
- reachable;
- not inside solid geometry;
- not on overly steep slopes;
- not at invalid map borders;
- not deliberately placed in trail centres;
- can appear in dense forest, grass, clearings, near trees or rocks and on uneven terrain.

Documents should pull the player away from trails.

### Local grass clearance

Dense grass around a document must thin naturally with a soft falloff so the document remains readable. No square holes.

### Fireflies

Every spawned document independently has a **45% chance** of a firefly effect.

When selected:
- 4–6 tiny fireflies;
- dim emissive glow;
- slow irregular motion;
- short practical visibility distance;
- no bright quest-beacon appearance;
- no real PointLight by default.

---

## 19. Document pickup

Automatic; no interaction button.

Target duration: approximately **2.0–2.7 s**.

Sequence:
1. right hand with flashlight moves slightly down/right but keeps the object lit;
2. left hand reaches to actual document anchor;
3. fingers catch a real edge;
4. one side lifts first;
5. regrip;
6. full folder/document leaves ground;
7. wrist reacts to weight;
8. object briefly comes closer to camera;
9. left hand lowers it out of frame;
10. world collectible disappears;
11. HUD updates;
12. autosave triggers;
13. flashlight returns to normal presentation.

Use at least three grip variants across the ten pickups, including a rare subtle imperfect grip correction.

Short interaction IK may correct reach on uneven terrain.

---

## 20. HUD

Minimal permanent HUD.

Primary counter:
- English: `DOCUMENTS X / 10`;
- Russian: `ДОКУМЕНТЫ X / 10`.

No minimap, weapon HUD or cluttered inventory grid.

---

## 21. Locust — identity

Recurring primary monster.

Approximate height: **2.3× player height**.

Key traits:
- extremely long arms;
- huge scale;
- heavy centre of mass;
- stalking / hiding behaviour;
- arm-supported chase locomotion.

Do not animate it as a simply scaled-up human.

---

## 22. Locust encounter logic

Typical appearance distance: approximately **18–42 m**.

It should prefer trees/cover and partial exposure.

At long distance, strong flashlight exposure can encourage retreat/hiding.

At medium distance, it may observe the player for roughly **4 seconds** with head tracking before retreat or escalation.

Distance logic stays active even during hiding animations.

No normal random Locust encounter should begin after document 10; final chase logic takes over.

---

## 23. Locust hiding animation set

Minimum five visually distinct authored variants:

- `Locust_FarHide_A`;
- `Locust_FarHide_B`;
- `Locust_MediumHide`;
- `Locust_CloseHide_A`;
- `Locust_CloseHide_B`.

They must differ in silhouette, timing and visible body parts, not only playback speed.

Close trees may be too small to hide the whole creature; partial limbs/body remain visible naturally.

---

## 24. Locust distance, retreat and Rage

Hiding is not an invulnerable animation lock.

If the player advances aggressively while Locust is retreating, the retreat may abort and Locust enters **Rage**.

In Rage:
- it fixes on the player;
- body shifts forward;
- long arms prepare as support limbs;
- chase begins with little hesitation;
- it does not immediately return to passive hiding.

For a close encounter, backing away is the intended survival response.

If `M` is the configured medium-distance threshold, safe retreat is approximately:

`0.85 × M`

After reaching that separation, Locust may disengage instead of killing the player.

---

## 25. Locust chase locomotion

Locust must not perform a normal tall-human sprint.

Its long arms are functional locomotion supports.

Desired movement:
1. torso pitches forward;
2. one long arm reaches toward ground;
3. arm contacts and takes part of the load;
4. lower body drives mass forward;
5. opposite arm cycles forward;
6. next support contact occurs;
7. body surges between support points.

The result is an unnatural hybrid between running and quadrupedal support.

Synchronize heavy hand/limb contacts with sound. Avoid obvious hand/foot sliding.

---

## 26. Locust rear death

Distinct death sequence.

- Locust pierces player from behind with its pointed hand/arm.
- Camera receives a hard physical shock.
- Player drops the flashlight physically.
- Flashlight remains ON.
- Dropped flashlight can bounce, roll and sweep the scene with its beam.
- Both player hands grab the piercing Locust limb and try to pull it free.
- Hands tremble and weaken.
- One hand slips first, then the other.
- Hands finally drop out of frame.
- Red pulsing vignette grows.
- Breathing breaks down.
- Image fades to black.
- Death UI only appears after black.

---

## 27. Locust front death

Completely different from the rear sequence.

1. Locust lunges from front and pierces the chest.
2. Flashlight drops physically and remains ON.
3. Player is forced down toward the ground.
4. Camera loses horizon with controlled authored roll.
5. Both hands rise defensively and panic/flail intentionally.
6. Strength rapidly fades; movements shrink.
7. One arm drops before the other.
8. Locust brings its huge head rapidly close to camera.
9. Tinnitus rises and forest ambience is heavily suppressed.
10. Red vignette intensifies.
11. Image progresses red -> dark red -> near black -> black.
12. Death UI appears only after black.

---

## 28. Locust screamers

The two approved Locust screamer audio files must remain mapped to **two visually different death/jumpscare sequences**.

Never reuse one death animation with only different audio.

`amazing-grace-analog-horror.mp3` is forbidden and must remain excluded from release assets.

---

## 29. Boiled One — identity

Rare psychological entity, approximately **1.5× player height**.

It is not a normal humanoid:
- no normal shoulders;
- no normal legs;
- essentially a vertical irregular piece of flesh;
- no walking animation;
- no human breathing / weight shifting.

Baseline animation is only a tiny, slow, irregular sway.

Boiled is roughly five times rarer than Locust and may appear around documents 2–8. Maximum one encounter opportunity per run.

---

## 30. Boiled visibility and gaze trigger

The event triggers only when the player truly sees Boiled.

A camera-angle test alone is insufficient.

Visibility must be blocked by real visual obstruction such as:
- tree trunk;
- rock;
- dense branch/foliage region;
- other valid solid occluder.

For foliage use inexpensive dedicated visibility occluders if necessary instead of expensive ray-mesh testing across thousands of leaves.

A short visibility confirmation around ~0.06 s can prevent one-frame false triggers.

---

## 31. Boiled focus event

When genuinely noticed:
1. camera smoothly begins focusing toward Boiled;
2. player look is temporarily overridden;
3. movement is **not** fully disabled;
4. movement speed is reduced by 67%, leaving 33% normal speed;
5. breathing progressively accelerates;
6. restrained tinnitus gradually appears;
7. forest ambience may be slightly attenuated;
8. scripted focus continues toward eye closure;
9. eyelids close fully;
10. at the exact moment the eyes are fully closed, Boiled disappears/despawns;
11. player never sees the disappearance itself.

The approved psychological video sequence may then continue, followed by black and waking at the same location.

This is not a normal death.

---

## 32. Persistent Boiled influence

After the player completes the Boiled encounter, rare subtle digital anomalies may appear until the end of the current run:
- short horizontal strips;
- tiny block artifacts;
- occasional mild RGB split/sliver;
- brief microbursts.

The effect must be rare, subtle and mobile-friendly.

It survives Continue within the same run and clears on a new run.

---

## 33. New hallucination entity — White Eyes

A new non-physical hallucination is part of the canonical design.

### Presentation

While the player is moving forward through the forest, a pair of **two white eyes** may appear somewhere far ahead.

The eyes:
- are only two bright white eye shapes / points;
- have no visible body;
- face/look toward the player;
- exist at a distant position in the player's forward view;
- disappear quickly;
- disappear by simply vanishing, with **no dissolve animation**, no smoke and no body reveal;
- do not attack the player;
- do not start a chase;
- are intended purely as a hallucination / paranoia event.

### Timing

After an appearance, the next eligible appearance interval is randomly selected between **5 and 8 minutes**.

The exact moment inside that range must be random so the player cannot predict it.

The system should not fire the hallucination constantly just because the interval elapsed; the appearance should wait for a reasonable forward-travel moment when the player can actually notice something ahead.

### Behaviour constraints

- no physical collider is required for the eyes themselves;
- no pathfinding or monster AI is required;
- the eyes should not leave a corpse/object after disappearing;
- disappearance is abrupt and fast;
- the event must not replace or consume Locust/Boiled encounters;
- it should be cheap enough for Android.

The precise eye separation, distance and visible duration are implementation tuning values, but the canonical behaviour above must remain intact.

---

## 34. Audio direction

Sound is a primary horror tool.

Layered forest ambience may include:
- wind;
- branches;
- distant vegetation movement;
- insects;
- occasional birds;
- rare unexplained distant sounds.

Player audio:
- footsteps by surface;
- breathing;
- stressed breathing;
- clothing movement;
- hand/item interaction.

Monster audio:
- Locust distant movement and heavy arm contacts;
- chase presence;
- approved distinct death screamers;
- Boiled tinnitus / ambience suppression after detection.

Do not use constant music to announce every monster event.

---

## 35. Save / run state

Autosave after every collected document.

Persist as required:
- run seed;
- collected documents;
- progress;
- current-run Boiled influence;
- settings;
- selected language.

Continue restores the current run.

New Game clears run-specific state and starts a new run.

---

## 36. Final chase

Triggered after document 10.

During final chase:
- player final movement speed = normal speed × **2.15**;
- Locust final chase speed ≈ **97.5% of the player's final speed**;
- the creature remains frighteningly close but escape remains mathematically possible with correct movement;
- normal random monster event logic is replaced by the authored finale.

The chase leads toward the old-road ending.

---

## 37. Final flashlight presentation

During the ending sequence the acquired flashlight may detach physically from the player.

Requirements:
- remains switched ON;
- can land on the ground naturally;
- its beam should ideally point back toward the forest as part of the final composition.

---

## 38. Final pickup / truck

The ending vehicle must use a real physics-based cinematic foundation, not simply slide as a static mesh.

Use:
- Rigidbody;
- WheelColliders;
- suspension spring/damper;
- anti-roll assistance;
- driven-wheel torque;
- braking;
- visible wheel rotation;
- front steering visuals;
- headlights / tail-light hooks.

The supplied merged pickup model may require wheel geometry extraction into separate visual wheel transforms.

---

## 39. Ending safety

Once the real ending cinematic begins:
- remaining Locust gameplay instances must no longer be able to kill the player;
- player cannot receive an accidental normal death during the authored ending;
- transition ends in localized finale presentation and return/continuation to menu as designed.

---

## 40. Credits

Canonical author credit:

English:
- `Idea by: Meric23`
- `Developed by: Meric23`

Russian:
- `Идея: Meric23`
- `Реализовал: Meric23`

Preserve required third-party attributions, including current creature creators/model sources and license identifiers.

Current Locust and Boiled model licenses contain non-commercial restrictions, so the project remains a free/non-commercial fan game unless permission is obtained or the assets are replaced with suitable commercial alternatives.

---

## 41. Android optimization

Quality should be preserved through technical optimization rather than deleting the atmosphere.

Use where appropriate:
- LODs;
- frustum/distance culling;
- GPU instancing / SRP Batcher;
- ASTC texture compression;
- sensible shadow distance;
- mesh batching for dense grass;
- limited real-time Light count;
- object pooling;
- inexpensive visibility occluders;
- throttled monster AI updates where safe;
- short-lived physics for authored flashlight drops;
- skeletal animation + lightweight procedural offsets;
- short interaction IK instead of constant expensive IK.

Avoid expensive whole-canopy MeshColliders and unnecessary full-body runtime physics for monsters.

---

## 42. Performance priorities

Primary order:
1. no crashes;
2. stable frametime;
3. no major hitching/spikes;
4. preserve horror readability and darkness;
5. raise visual quality within device budget.

Stress-test:
- dense user tree forest;
- trunk colliders;
- dense grass;
- flashlight + shadows;
- multiple LOD transitions;
- active Locust animation/AI;
- Boiled sequence;
- White Eyes hallucination;
- final chase and vehicle.

---

## 43. Development and verification rules

GitHub is the source of truth for code and project state.

Do not call a feature finished because source code exists.

Required progression for important systems:

`source -> compile -> build -> APK -> real Android device test`

For GitHub Actions failures:
1. inspect the complete failing job log;
2. capture the exact first meaningful compiler/runtime error;
3. identify root cause;
4. make the smallest targeted fix;
5. push a new commit;
6. let the push create a fresh run;
7. verify the new run before declaring success.

Linear is used to track bugs, CI regressions, technical debt and implementation tasks. Notion is used as project documentation/GDD. Figma is used for menu/UI design source.

---

## 44. Animation acceptance criteria

Do not call the animation layer complete until all are true:
- viewmodel never exposes missing torso/arm cutoffs;
- flashlight visibly lags camera turns and settles naturally;
- beam follows flashlight orientation;
- base idle and meaningful idle variants exist;
- flashlight pickup is a physical hand/object interaction;
- document pickup uses left hand and real document position;
- multiple document grip variants exist;
- document pickup works on uneven terrain;
- Boiled never moves like a humanoid;
- foliage/trunks can block Boiled gaze;
- Boiled focus leaves 33% player movement speed;
- Boiled disappears while eyes are fully closed;
- Locust has 2 far, 1 medium and 2 close hide variants;
- hiding distance logic stays active;
- approaching retreating Locust can cause Rage;
- safe retreat remains possible;
- Locust chase uses long arms as ground supports;
- front and rear death sequences are unmistakably different;
- both deaths physically drop the still-lit flashlight;
- both deaths fade to black before death UI.

---

## 45. Forest / lighting acceptance criteria

The forest is not release-ready until:
- only the user's supplied tree models are used in the final forest;
- every relevant solid trunk has a correctly sized trunk collider;
- foliage does not create giant invisible player blockers;
- grass remains the approved existing grass source;
- terrain is visibly uneven;
- trails follow terrain and have natural vegetation falloff;
- flashlight OFF still leaves extremely faint natural night visibility;
- flashlight ON causes weak ambient surroundings to visually fall away through exposure/adaptation rather than an obvious global light toggle;
- switching flashlight state does not produce ugly exposure pops;
- tree LOD changes do not break collision.

---

## 46. White Eyes hallucination acceptance criteria

The hallucination is complete when:
- it consists only of two white watching eyes with no visible body;
- it appears far ahead during suitable forward travel moments;
- appearance intervals are randomized between 5 and 8 minutes;
- it disappears quickly by abruptly vanishing;
- no dissolve/smoke/spawn body is shown;
- it never attacks or chases;
- it does not interfere with Locust or Boiled encounter state;
- it remains cheap on Android;
- it has been observed correctly in a real device playtest.

---

## 47. Definition of a finished Fallen Forest build

A green CI build alone is not a finished game.

A release candidate requires, at minimum:
- finished menu, settings, credits and localization;
- startup warning and loading flow;
- complete wake-up and flashlight pickup;
- final FPS arms/viewmodel presentation;
- real flashlight materials and motion;
- uneven Terrain with working TerrainCollider;
- only user-supplied tree models with correct trunk collision and LOD behaviour;
- approved dense grass and trails;
- weak natural night ambient + flashlight exposure adaptation;
- all 10 documents every run;
- autosave/Continue;
- document pickup animation and fireflies;
- complete Locust AI/animation/death set;
- complete Boiled encounter and persistent influence;
- White Eyes hallucination system;
- finished audio pass;
- final chase;
- physical ending truck;
- ending sequence;
- acceptable Android performance;
- installable APK;
- full start-to-ending smoke test on a real Android device;
- no unresolved critical blocker.
