# Airflow Prototype — Batch 2: Air Power + Momentum

Target: Unity 6.6 / 6000.6, URP, Input System.

## Upgrade your existing Batch 1 playground

1. Replace your `Assets/AirflowPrototype` folder with the one from this package,
   or merge/overwrite the changed files.
2. Let Unity compile.
3. Open the Batch 1 playground you were already testing.
4. Run:
   `Tools > Airflow Prototype > Batch 2 > Upgrade Current Playground`
5. Press Play.

If you prefer a clean scene:
`Tools > Airflow Prototype > Batch 2 > Create Fresh Batch 2 Playground`

## Controls

- WASD / Left Stick — Move
- Left Shift / Right Trigger — Air Dash
- Space / South Button — Jump
- Mouse / Right Stick — Camera

## What Batch 2 changes

### Air Power
Dash consumes a 0–100 resource.

For this batch only, Air Power regenerates automatically after a short delay.
That is a testing scaffold. Once Air Nodes exist, node hits become the important
recovery loop.

If you fully empty the meter, dash briefly locks out until some power returns.
This avoids ugly on/off stuttering at zero power.

### Air Dash
Dash is not a binary movement teleport.

Holding dash while moving raises:
- target speed
- acceleration

The motor still owns final velocity.

### Momentum tail
When dash is released at high speed, velocity is not clamped to normal run speed.
The excess speed decays using `Dash Release Deceleration`.

This is one of the most important Batch 2 tuning values.

### High-speed steering
At dash speed, the velocity direction bends toward input instead of snapping.
`Dash Steering Sharpness` determines how much "carving" the movement has.

### Feedback
- FOV now has room to react across the larger speed range.
- A presentation-only forward body lean grows above normal run speed.
- Debug HUD shows Air Power and dash state.

## First feel test

Do these before changing values:

1. Hold forward + dash from rest.
   - Does acceleration feel satisfying?
2. Release dash but keep holding forward.
   - Do you feel a short momentum tail?
3. Release all input at full dash speed.
   - Does the player glide too long, or stop too abruptly?
4. Hold dash and make a broad 90-degree turn.
   - Does the turn carve naturally?
5. Try rapid left/right steering at high speed.
   - It should not feel like a normal-speed controller simply running faster.
6. Dash into a jump.
   - Horizontal momentum should carry into the air.
7. Run, dash, release, dash again.
   - Does the rhythm make you want to repeat it?

## Important tuning values

`Dash Max Speed`
- top speed while air-dashing

`Dash Acceleration`
- how aggressively grounded dash reaches top speed

`Dash Air Acceleration`
- how much dash authority remains airborne

`Dash Release Deceleration`
- how quickly excess dash speed bleeds back toward ordinary speed
- LOWER = more flow / more slippery
- HIGHER = more controlled / less momentum

`Dash Steering Sharpness`
- how rapidly high-speed velocity bends toward new input
- LOWER = wide carving arcs
- HIGHER = responsive direction changes

`Dash Drain Per Second`
- how much Air Power sustained dash costs

`Passive Recovery Per Second`
- temporary test recovery; this becomes much less important after Air Nodes

`Fast Fov`
- camera speed presentation at high speed

`Dash Body Lean Degrees`
- purely visual

## What I need from your test

The useful feedback is comparative:

- dash acceleration: weak / good / too explosive
- max speed: too slow / good / uncontrollably fast
- momentum release: too sticky / good / too slippery
- high-speed turning: too rigid / good / too twitchy
- airborne carry: loses too much / good / carries too much
- Air Power duration: too short / good / too generous
- FOV response: too quiet / good / distracting

Most importantly:

**Does releasing dash while still moving make you immediately want to hit dash again?**

That rhythm is what the Air Nodes will plug into next.


## v2.1 braking update

Momentum now distinguishes player intent:

- Release Dash but keep holding movement:
  - excess speed coasts down gently using `Dash Release Deceleration`
- Release movement entirely while grounded:
  - the player brakes quickly using `Dash Brake Deceleration`
- Push against your current travel direction:
  - stronger braking uses `Dash Reverse Brake Deceleration`
- Release movement while airborne:
  - launch momentum is preserved, with only small `Airborne Momentum Decay`

This removes the "ice skating when I stop" behavior without destroying the
momentum tail that makes chaining movement satisfying.
