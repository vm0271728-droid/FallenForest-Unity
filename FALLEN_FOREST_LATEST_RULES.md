# FALLEN FOREST — LATEST CANON ADDITIONS

This file records design decisions added after `FALLEN_FOREST_MASTER_PLAN.md` was created. A future continuation must read both files until these points are folded back into the master plan.

Last synchronized: 2026-08-17 06:25 Europe/Moscow (+03:00).

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
- Each spawned document independently rolls the fixed **45% firefly chance**; a successful roll creates **4–6** very small dim fireflies with a deliberately tiny local glow and distance culling.
- `TrailZone.cs` exposes a smooth vegetation-density gradient: effectively no grass on the path volume, sparse grass beside it, then dense grass farther into the forest.
- `ForestScatterer.cs` staging raises the default grass target from 9000 to 16000 clumps and applies the trail density gradient while keeping mesh batching for Android.
- `ForestWindURP.shader` staging supports soft stochastic grass suppression around documents instead of a hard square hole.

These changes are staged on a feature branch and are **not yet claimed compiled** until the Unity Android CI reaches a real project compile.

## Uneven terrain implementation staged

Branch `feature/world-terrain-relief` contains `TerrainReliefGenerator.cs`:

- deterministic layered broad/medium/fine terrain noise;
- natural depressions and shallow ridge structure;
- moderate vertical relief rather than a flat plane;
- a blended flatter opening area around the wake-up/start point so the beginning remains playable;
- intended generation/baking before release, not expensive per-frame terrain deformation.

This terrain branch is also staged and must be integrated into the real generated/committed Forest scene before being considered final.
