# Fallen Forest Monster T3 Implementation

## Boiled One
- Non humanoid movement.
- Only slow organic sway.
- Visibility requires real line of sight.
- Foliage, branches, rocks and trees block the gaze event.
- On detection: camera focus, movement reduced to 33%, breathing and tinnitus increase, eyes close and creature disappears exactly during full eye closure.

## Locust
- 2.3x player scale target.
- Five authored hiding animations:
  - FarHide_A
  - FarHide_B
  - MediumHide
  - CloseHide_A
  - CloseHide_B
- Distance checks remain active during hiding.
- Approaching during retreat triggers rage.
- Chase locomotion uses arm-supported movement, not human running.

## Required animation clips

Boiled:
- Idle_SlowSway
- Focus_Event
- EyeClosure_Disappear

Locust:
- Far_Peek_A
- Far_Peek_B
- Medium_Retreat
- Close_Retreat_A
- Close_Retreat_B
- Rage_Transition
- ArmSupported_Chase
- Death_Front
- Death_Back

## Wind system

Forest ambience requires:
- procedural wind variation;
- grass movement;
- tree foliage sway;
- stronger gust moments;
- mobile friendly implementation through shaders/vertex animation where possible.

This file defines implementation targets; final visual animation clips require the actual creature rigs/assets.
