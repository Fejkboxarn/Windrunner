# Airflow Prototype — Batch 1: Locomotion Foundation

Target: Unity 6.6 (6000.6), URP, Input System package.

## What this batch contains

- Camera-relative third-person locomotion
- Authored acceleration and deceleration
- Responsive character facing
- Jump with:
  - coyote time
  - jump buffering
  - stronger falling gravity
- Air steering
- Custom orbit camera
- Camera collision
- Speed-driven FOV feedback
- Runtime debug HUD
- One-click Editor setup for a greybox playground

There is intentionally **no dash and no Air Node system yet**. Batch 1 has one job:
make ordinary running, turning, jumping, and landing feel trustworthy before momentum
systems amplify them.

## Install

1. Extract the `AirflowPrototype` folder from the zip.
2. Drag that folder into your Unity project's `Assets` folder.
3. Let Unity compile.
4. In Unity, choose:
   `Tools > Airflow Prototype > Batch 1 > Create Playground Scene`
5. Save your currently open scene if Unity asks.
6. Press Play.

The setup command creates:
`Assets/AirflowPrototype/Generated/Batch1_Playground.unity`

It also creates:
`Assets/AirflowPrototype/Generated/Batch1_MovementSettings.asset`

That settings asset is the tuning surface for this batch.

## Controls

- WASD / Arrow Keys / Left Stick: move
- Mouse / Right Stick: camera
- Space / Gamepad South: jump
- Escape: release cursor
- Left click: capture cursor again

## First tuning test

Do not judge this by whether it merely "works."

Play for 3–5 minutes and deliberately test:

1. Accelerate from rest.
   - Does the player feel eager to move, or sluggish?
2. Release movement at full speed.
   - Does stopping feel crisp enough without feeling robotic?
3. Make repeated 90-degree turns.
   - Does the character obey you without visually snapping?
4. Run directly into the wall and slide along it.
   - Does collision destroy control?
5. Jump just after leaving the edge of a platform.
   - Coyote time should make the jump still happen.
6. Press jump just before landing.
   - The buffered jump should fire as you touch down.
7. Change direction in midair.
   - There should be control, but less authority than on the ground.
8. Watch the camera while reaching top speed.
   - FOV should widen subtly, not scream "speed effect."

## Initial values worth changing

Select:
`Tools > Airflow Prototype > Batch 1 > Select Movement Settings`

Start with these if something feels wrong:

- `Max Ground Speed`
  - overall baseline pace
- `Ground Acceleration`
  - how eagerly movement begins / redirects
- `Ground Deceleration`
  - how quickly input release bleeds speed
- `Turn Sharpness`
  - visual facing response
- `Air Acceleration`
  - midair authority
- `Jump Height`
  - jump arc height
- `Gravity`
  - overall jump time / weight
- `Fall Gravity Multiplier`
  - how decisively the character comes back down
- `Coyote Time`
  - edge forgiveness
- `Jump Buffer Time`
  - landing-jump forgiveness
- `Base Fov` / `Fast Fov`
  - baseline speed presentation

## Feedback I need from you after testing

The most useful report is qualitative. For example:

- acceleration: too slow / good / too instant
- stopping: too slippery / good / too harsh
- turning: too heavy / good / too twitchy
- jump rise: too floaty / good / too abrupt
- fall: too floaty / good / too heavy
- air control: too weak / good / too strong
- camera: too loose / good / too rigid
- overall: "I want to keep moving" or "movement still feels functional rather than fun"

You can also send the settings values you changed.

## Architecture note

`PlayerInputReader`
    -> input intent

`PlayerMotor`
    -> gameplay movement state

`PlayerOrbitCamera`
    -> camera behavior

`PlayerSpeedFeedback`
    -> presentation driven from real player speed

`PlayerDebugHUD`
    -> prototype diagnostics

`AirflowMovementSettings`
    -> central tuning profile

Gameplay and presentation are already separated. Batch 2 can therefore add Air Power,
dash acceleration, momentum retention, and stronger speed feedback without turning the
motor into a camera/VFX/audio script.

## Input System note

For portability, Batch 1 creates its Input Actions in code rather than requiring a
specific `.inputactions` asset. It still uses `UnityEngine.InputSystem`.

When the prototype's control scheme stabilizes, `PlayerInputReader` can become an
adapter around your project's shared InputActionAsset without changing `PlayerMotor`.
