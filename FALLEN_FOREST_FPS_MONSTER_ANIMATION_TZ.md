# Fallen Forest — canonical FPS / flashlight / document / monster animation specification

Last synchronized: 2026-08-17

This document is the canonical implementation specification for:
- first-person arms and flashlight presentation;
- flashlight pickup and idle behaviour;
- document pickup animation;
- camera/viewmodel FOV rules;
- Boiled One presentation and gaze event;
- Locust distance behaviour, hiding, rage, locomotion and kill animations;
- player hand animation during Locust deaths.

If older wording in `FALLEN_FOREST_MASTER_PLAN.md` conflicts with this file on these subsystems, this file is newer and takes precedence. `FALLEN_FOREST_LATEST_RULES.md` remains the global newest-rule document; future edits should keep the two synchronized.

---

## 1. Overall animation target

All first-person animation must feel cinematic, physical and human rather than like a generic FPS kit.

Required qualities:
- visible weight and inertia;
- no rigid attachment of hands to camera;
- no robotic linear movement;
- no obvious identical repeated cycles;
- smooth blending between idle/walk/run/pickup/death states;
- fingers should visibly establish and adjust grip where the rig allows it;
- procedural layers must never reveal that there is no full body behind the FPS arms;
- hand poses must be composed for mobile widescreen framing.

Do not make the flashlight behave like a weapon. It is a survival-horror prop held by a frightened ordinary person.

---

## 2. Camera and FOV

### World camera

The old freely adjustable FOV setting is removed from the player settings.

Default world FOV:
- `75°` fixed for normal gameplay.

Temporary scripted FOV changes are allowed only for authored sequences such as:
- wake-up;
- Boiled focus event;
- Locust kill/jumpscare;
- ending/cinematic beats.

### Viewmodel camera

Hands and flashlight render on a dedicated viewmodel layer/camera.

Target viewmodel FOV:
- approximately `60–62°`, fixed after visual tuning.

The viewmodel framing must be validated on common mobile aspect ratios including 16:9, 18:9 and 20:9.

No animation, sway, lag or screen ratio may reveal the cut ends of the arms or the empty space where a torso would be.

---

## 3. FPS arms

Use the supplied first-person arms asset as the base rig.

Required animation architecture:
- main skeletal Animator layer;
- additive breathing / subtle tension layer;
- procedural camera-turn lag layer;
- procedural walk/run sway layer;
- optional IK correction for document reach and grip;
- death animation override layer that disables normal idle/sway/lag while a Locust kill is playing.

The left hand is primarily used for document interaction while the right hand normally holds the flashlight.

---

## 4. Flashlight asset and material

Use the supplied flashlight FBX and its PBR textures:
- color/albedo;
- normal;
- AO;
- metallic;
- smoothness;
- emissive.

The flashlight is attached to a right-hand socket.

The actual gameplay light source follows the physical flashlight orientation rather than being rigidly aligned to the world camera. This is required so viewmodel lag affects the beam naturally.

---

## 5. Flashlight camera-turn lag / inertia

The flashlight hand must slightly lag behind camera rotation.

### Horizontal turns

When the player turns right:
1. the camera begins turning immediately;
2. the right arm and flashlight remain slightly behind;
3. the forearm starts catching up;
4. the wrist and flashlight lag fractionally behind the forearm;
5. the flashlight reaches the new orientation with a soft spring-like return;
6. a very small overshoot is allowed after a sharp stop;
7. damping settles the hand back into the normal pose.

Horizontal lag is stronger than vertical lag.

### Vertical turns

Vertical lag exists but is restrained so the flashlight remains useful and comfortable on a phone.

### Limits

- clamp maximum positional and rotational offset;
- rapid touchscreen swipes must never throw the hands outside the safe viewmodel frame;
- no jelly-like oscillation;
- the flashlight must remain usable as the player's primary light source.

### State scaling

- standing: subtle and accurate;
- walking: slightly more alive;
- running: stronger but controlled;
- sharp panic turns: noticeable lag and tiny overshoot, still within safe limits.

---

## 6. Flashlight idle animations

### `Flashlight_Idle_Base`

Seamless base loop, approximately 4–6 seconds.

Includes:
- subtle breathing movement;
- tiny shoulder/forearm motion;
- minimal wrist correction;
- minute flashlight inertia;
- occasional small finger pressure changes.

The light beam should drift very slightly with the hand, never enough to frustrate navigation.

### `Flashlight_Idle_Variant_A` — grip adjustment

Approximately 1.5–2 seconds.

The character:
- lowers the flashlight slightly;
- loosens grip;
- repositions fingers sequentially rather than all at once;
- rotates the flashlight a little in the palm;
- tightens grip;
- returns to base idle.

### `Flashlight_Idle_Variant_B` — tension

Approximately 2–3 seconds.

The character:
- slowly lets the hand drop a little;
- takes a slightly deeper breath;
- makes a small nervous wrist correction;
- tightens the grip;
- raises the flashlight back into position.

Do not turn this into an exaggerated hand shake.

### `Flashlight_Idle_Variant_C` — rare micro-detail

Very rare.

The thumb runs over the flashlight body or briefly touches/checks the switch without turning the flashlight off.

The system should randomize long-idle variants so they do not trigger on a perfectly predictable interval.

---

## 7. Walking and running with flashlight

### Walking

Movement must combine:
- footsteps;
- breathing;
- camera turn lag;
- slight item inertia;
- minor step asymmetry.

Do not use a perfectly repeating sinusoidal weapon-bob pattern.

The flashlight has perceptible mass: wrist response should slightly trail the forearm.

### Running

During running:
- flashlight moves slightly lower and closer to the body;
- the right elbow moves more actively;
- the beam becomes less stable but remains useful;
- the left hand may occasionally enter the lower frame naturally;
- stopping from a run has soft inertial settling rather than an instant snap to idle.

---

## 8. Flashlight pickup animation

The flashlight is picked up automatically after the wake-up sequence; no interaction button.

Target duration:
- approximately `2.3–2.8 s`.

Sequence:
1. player is still low after waking;
2. right hand enters frame toward the flashlight;
3. first contact nudges/slides the flashlight slightly on the ground;
4. fingers close around the real geometry;
5. the flashlight lifts with visible weight;
6. wrist drops very slightly as the object leaves the ground;
7. left hand briefly assists a grip correction;
8. right hand rotates flashlight to gameplay position;
9. thumb physically presses the power control;
10. audible click;
11. light turns on with a tiny natural delay after the click;
12. blend into `Flashlight_Idle_Base`.

Do not teleport the flashlight into the hand.

---

## 9. Document model and pickup

Use the supplied folder/document asset as the final collectible visual after conversion/import into the Unity pipeline.

All 10 required documents must spawn every run. Fireflies remain a separate 45% per-document visual chance and are not part of the pickup animation itself.

Documents are auto-picked up without an interaction button.

### Main pickup animation

Target duration:
- approximately `2.0–2.7 s`.

Sequence:
1. right hand with flashlight moves slightly down/right while continuing to illuminate the document;
2. left hand reaches toward the real document position;
3. fingers catch an actual edge rather than the object's centre;
4. one side of the folder lifts first;
5. hand changes grip;
6. the full folder leaves the ground;
7. wrist reacts slightly to its weight;
8. folder comes briefly closer to the camera;
9. left hand lowers it out of frame;
10. collectible object is removed;
11. HUD updates to `ДОКУМЕНТЫ X / 10`;
12. autosave triggers;
13. right-hand flashlight returns to normal idle.

### Pickup variants

Use at least three visual grip variants across the 10 documents:
- Variant A: take by upper/side edge;
- Variant B: scoop from below and regrip after lift;
- Variant C: rare imperfect grip where the folder slips slightly and fingers correct it.

Variant C must stay subtle and rare, not comedic.

### IK / terrain correction

Documents can lie in grass, on uneven terrain or near rocks/trees.

The pickup system should correct shoulder/elbow/wrist reach toward the actual document anchor so the hand does not grab empty air.

After a confirmed grip, the folder may be temporarily parented to a hand socket for the remainder of the animation.

---

## 10. Boiled One — form and scale

Boiled One is approximately `1.5×` player height.

Important anatomy rule:
- it has no normal shoulders;
- it has no legs;
- do not animate it as a humanoid;
- it is essentially a vertical piece of flesh.

Its only baseline body animation is a very small, slow, unsettling sway.

### `Boiled_IdleSway`

- tiny lateral movement;
- slight lean;
- slow irregular timing;
- no normal breathing cycle;
- no walking;
- no weight-shift animation based on human anatomy.

The movement should be subtle enough that at first the player might mistake it for a static shape.

---

## 11. Boiled visibility and gaze trigger

The Boiled event must only trigger if the player actually sees it.

A camera direction match alone is not enough.

If visual obstruction exists between the camera and Boiled, including:
- foliage;
- dense leaves;
- branches;
- tree trunk;
- rock;
- another solid visual blocker,

then:
- camera does not focus;
- no gaze event;
- no screamer/event escalation;
- the player remains safe from that trigger.

Use real line-of-sight logic. Because leaf cards may not have suitable physical colliders, dense foliage should participate in the visibility test via dedicated inexpensive vision-occlusion volumes or equivalent robust logic.

A short confirmation window around the previously specified ~0.06 s is acceptable after unobstructed visibility is established.

---

## 12. Boiled focus event

When the player truly notices Boiled:

1. camera begins a smooth focus toward Boiled;
2. camera look control is temporarily taken over by the event;
3. player movement is **not** disabled completely;
4. movement speed receives `-67%`, leaving `33%` normal movement speed;
5. breathing becomes progressively faster;
6. a restrained tinnitus/ringing tone gradually appears;
7. ambient forest sound may be slightly attenuated;
8. scripted focus continues toward eye closure;
9. eyelids close fully;
10. the instant the eyes are fully closed, Boiled disappears/despawns;
11. the player must never visually witness the disappearance itself.

The existing psychological sequence/video may continue after full eye closure if used by the current implementation, but Boiled must already be gone before the eyes reopen.

The fear escalation should feel progressive:
- initially only slightly faster breathing;
- then shorter, more stressed breaths;
- ringing becomes more noticeable toward eye closure;
- avoid painfully loud high frequencies.

---

## 13. Locust — scale and animation identity

Locust is approximately `2.3×` player height.

Its animation must reflect:
- very long arms;
- huge scale;
- unusual centre of mass;
- substantial weight.

Do not simply scale up a normal human animation.

---

## 14. Locust hiding animation set

Minimum authored stalking/hiding set:

### Far distance — two variants

`Locust_FarHide_A`
- most of body behind a tree;
- only part of head/shoulder/body visible;
- slow withdrawal behind the tree after detection/light exposure;
- different body parts disappear at slightly different times because of scale.

`Locust_FarHide_B`
- more unusual silhouette;
- may initially show only upper head or an offset portion of the body;
- slow peek or lean;
- distinct timing and shape from A;
- retreats behind cover in a clearly different way.

### Medium distance — one primary variant

`Locust_MediumHide`
- Locust is more exposed than at far range;
- after being noticed it **hides slowly** rather than instantly disappearing;
- head and body withdraw progressively;
- distance logic remains live throughout the hiding animation.

### Close distance — two variants

`Locust_CloseHide_A`
- nearby tree is too small to fully conceal the enormous creature;
- player can see portions of limbs/body/head;
- if player retreats successfully, Locust can complete its withdrawal.

`Locust_CloseHide_B`
- more aggressive and asymmetrical close peek;
- head/limb presentation differs clearly from A;
- may transition directly into rage/chase if player approaches.

All five stalk/hide animations must be visually distinct, not simple retimes of the same clip.

---

## 15. Locust distance logic during hiding

Hiding is not a safe animation lock.

Distance is recalculated continuously even while Locust is retreating behind a tree.

### Medium-distance behaviour

When Locust has been noticed at medium range:
- it begins to hide slowly;
- if the player keeps or increases distance, Locust can leave safely;
- if the player runs toward Locust during this retreat, the retreat can abort and Locust enters rage.

### Close encounter escape rule

If the player has already noticed a close Locust, the only reliable way to avoid the kill is to back away and create distance.

Define:
- `M = medium-distance threshold`
- safe retreat distance = `0.85 × M`

Once the player increases separation to at least approximately 85% of the medium-distance threshold, Locust may disengage/leave instead of killing.

If the player moves too close during the retreat/hide sequence, Locust can enter rage.

The exact numeric medium threshold remains a gameplay tuning value, but this 85% rule is part of the behaviour specification.

---

## 16. Locust rage

`Rage` is triggered when the player violates the creature's retreat space, including:
- aggressively reducing distance during a hiding/retreat animation;
- approaching too close in a close encounter;
- otherwise crossing the configured rage threshold.

In rage:
- hiding is aborted;
- Locust fixes on the player;
- body weight shifts forward;
- hands/arms prepare for ground-assisted locomotion;
- chase begins with little or no additional hesitation;
- it should not immediately return to passive hiding.

---

## 17. Locust locomotion — arm-supported run

This is a key animation requirement.

Because Locust is huge and has very long arms, it **uses its arms as supports during fast running**.

The chase must not look like a tall humanoid sprint.

Desired motion:
1. torso pitches forward;
2. one long arm reaches toward/contacts the ground;
3. that arm takes part of the body's load;
4. lower body drives the mass forward;
5. opposite arm cycles forward;
6. next ground support contact occurs;
7. the body surges between these support points.

The result should feel like an unnatural hybrid of a run and quadrupedal support.

Requirements:
- hands/arms are functional locomotion limbs, not decorative swinging props;
- heavy contact sounds synchronize with ground support;
- nearby strong contacts may produce a very restrained camera response;
- limb animation must avoid obvious foot/hand sliding;
- large-scale inertia must remain visible even at chase speed.

---

## 18. Locust kill from behind

This is a distinct death sequence.

### Attack

- Locust attacks from behind with its pointed/sharp hand;
- the hand pierces the player;
- the player receives an immediate physical camera shock.

### Player hands

- right hand loses the flashlight;
- flashlight physically falls to the ground rather than disappearing;
- flashlight remains ON;
- both player hands instinctively grab the Locust hand/arm that has pierced them;
- fingers clamp around it and try to pull it free;
- hands tremble under strain;
- grip weakens as the player loses strength;
- one hand begins slipping first;
- the second follows;
- hands finally drop out of frame.

### Flashlight after drop

The dropped flashlight should retain physics for the remainder of the death:
- bounce/roll/rotate naturally depending on terrain;
- its beam sweeps across grass/trees/Locust as it settles;
- it remains visible until the screen is nearly or fully dark.

### Screen/audio

- red pulsing vignette begins after the impalement;
- low impact/heartbeat response;
- breathing breaks down;
- image progressively darkens;
- red peripheral pulse remains visible while consciousness fades;
- complete black precedes the death-state UI.

Do not jump immediately to `Game Over`.

---

## 19. Locust kill from the front

This is completely different from the rear kill.

### Stage 1 — chest impalement

- Locust lunges from the front;
- pointed hand pierces the player's chest;
- camera receives a hard physical hit;
- right hand drops the flashlight immediately.

The flashlight again becomes a live physical object, remains ON and can illuminate the scene from the ground.

### Stage 2 — player collapse

- Locust uses the impaling force to knock/force the player to the ground;
- camera loses horizon in a controlled way;
- camera drops toward ground level with authored roll;
- red vignette begins.

### Player hands during front death

The hand animation is panicked rather than focused on pulling the Locust hand out.

Sequence:
- both hands rise defensively into frame;
- player flails / reaches / tries to push Locust away;
- movements are intentional panic, not ragdoll noise;
- strength rapidly disappears;
- movement amplitude decreases;
- elbows sink;
- fingers lose the ability to clench strongly;
- one arm drops to chest/ground;
- the other remains raised for a moment longer;
- final hand falls away as the player weakens.

### Stage 3 — head approach

After the player is down:
- brief micro-pause;
- Locust brings its enormous head rapidly toward the camera;
- head fills most of the frame;
- this movement synchronizes with the assigned front screamer audio.

### Stage 4 — tinnitus / red fade

During the close head/screamer moment:
- tinnitus/ringing appears;
- forest ambience is heavily suppressed;
- red vignette strengthens;
- image becomes progressively redder and darker;
- mild blur / contrast loss may be used;
- final progression: red -> dark red -> near black -> black;
- only after black does the death-state UI take over.

---

## 20. Two Locust screamer identities

The two supplied Locust screamer audios must remain mapped to visually different deaths/jumpscares.

- one is the frontal chest-impalement / fall / head-to-camera sequence;
- one is the rear impalement sequence.

Do not reuse one animation with only different audio.

If existing branch naming or previous staging associates the exact MP3s differently, preserve the exact approved audio files while adapting the final visual mapping consistently in code and documentation.

The forbidden `amazing-grace-analog-horror.mp3` remains excluded from release.

---

## 21. Animation state override rules

During authored pickup/death/focus sequences:
- normal procedural layers must be selectively disabled or reduced so they do not corrupt the authored pose;
- death animations fully override standard flashlight idle/walk/lag layers;
- document pickup may keep a restrained camera-turn component if it does not break hand-to-object alignment;
- Boiled focus overrides camera look but preserves 33% movement speed as specified above.

---

## 22. Mobile performance and quality constraints

The target is Android, so quality must be achieved without unnecessarily expensive runtime systems.

Preferred approach:
- skeletal clips plus lightweight procedural offsets;
- cheap IK only during short interactions;
- avoid multiple unnecessary Animator graphs for the same viewmodel;
- no real-time expensive foliage ray-mesh tests over thousands of leaves; use robust simplified visibility blockers;
- dropped flashlight physics only needs full simulation during short authored moments;
- monster contact animation should use authored/IK-assisted points rather than high-cost full-body physical simulation.

Do not make animations visually cheap solely for performance; optimize implementation instead.

---

## 23. Acceptance criteria

The implementation is not considered complete until all of the following are true:

- changing/turning camera never exposes missing torso/arm cutoffs;
- flashlight visibly lags behind camera turns with controlled spring return;
- flashlight beam follows the flashlight, not a camera-locked direction;
- base idle + at least two meaningful long-idle variants are present;
- flashlight pickup is a real hand/object interaction;
- document pickup uses the left hand and preserves right-hand flashlight presentation;
- multiple document grip variants exist;
- document pickup aligns to uneven terrain without obvious air-grabbing;
- Boiled only sways subtly and is never animated as a humanoid;
- foliage can block Boiled gaze activation;
- Boiled focus applies -67% movement speed rather than full immobilization;
- breathing and mild tinnitus escalate until eye closure;
- Boiled disappears exactly while the player's eyes are fully closed;
- Locust has 2 far, 1 medium and 2 close hiding animations;
- Locust distance logic remains live while it is hiding;
- approaching during retreat can trigger rage;
- successful close retreat is possible when distance grows to the safe threshold;
- Locust uses its long arms as physical support during chase locomotion;
- rear death includes flashlight drop + hands grabbing the piercing limb;
- front death includes flashlight drop + panicked defensive hands that lose strength;
- both death sequences use red pulsing vignette and fade to black before death UI;
- front and rear deaths are unmistakably different;
- final behaviour is compile-tested in the actual Unity project and device-tested on Android before being called verified.
