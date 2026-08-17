# Fallen Forest — menu, localization and credits canon

Last synchronized: 2026-08-17.

This document records canonical product/UI decisions that must survive chat/session limits and be applied when the real Unity UI is integrated.

## Localization

- Default game language: **English**.
- Settings must contain a **Language** selector.
- Initial supported languages:
  - English — default;
  - Русский.
- The selected language must persist between launches.
- Localization applies to the entire player-facing game, including:
  - startup disclaimer;
  - main menu;
  - settings;
  - loading screens;
  - HUD;
  - document-related text;
  - death/continue UI;
  - ending/finale text;
  - credits/author attribution.
- Localization architecture must be data-driven/extensible so additional languages can be added later without rewriting UI logic.

## Main-menu author credit

The main menu must contain a small, readable author-credit block in a corner. It should not compete visually with the main menu buttons or destroy the dark forest atmosphere.

Canonical author lines:

English:
- **Idea by: Meric23**
- **Developed by: Meric23**

Russian:
- **Идея: Meric23**
- **Реализовал: Meric23**

`Meric23` is the project author credit and should be visually slightly more prominent than third-party asset attribution.

## Third-party / fan-content attribution

Fallen Forest is intended as a **free, non-commercial fan game**.

The menu/credits presentation must clearly preserve required attribution for third-party content used by the project. Current canonical entries include:

- The Locust / The Boiled One — characters by **Doctor Nowhere**.
- Locust 3D model — **Doumty**, CC BY-NC-ND 4.0.
- Boiled One 3D model — **MG Rips**, CC BY-NC 4.0.

A concise attribution may appear in the menu corner, while the complete list should be accessible through a dedicated **Credits / Авторы** view as the project accumulates additional music, ambience, SFX, models, textures, fonts, videos, or other licensed material.

The compact menu attribution must remain readable on mobile and respect Android safe areas/notches.

## UI behavior

- Credits should be available from the main menu without interfering with `Play`, `Settings`, or `Exit`.
- A small corner credit block may be tappable and open the full Credits screen.
- The full Credits screen should use localized labels but preserve creator names and license identifiers exactly.
- Do not silently remove attribution in release builds.

## Implementation timing

These rules are canonical now, but do **not** modify `Assets/**`, `Packages/**`, `ProjectSettings/**`, `Tools/**`, or the Android workflow while an important main CI build is active solely to implement these cosmetic/menu changes. Integrate them after the current compile/toolchain gate has completed, then verify the result in Unity and on Android.
