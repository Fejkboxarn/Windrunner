# Dagger Arsenal

Use the MainMenu scene and a new, disposable playthrough for acceptance. New development slots start with Training Daggers equipped and own Farseer's Daggers. Opening World_Zone01 or StartingWorld directly in the Editor uses a temporary session. Use a named slot to verify persistence.

## In-game acceptance

1. Open `Assets/WindrunnerFrontend/Scenes/MainMenu.unity`, press Play, choose **New game**, then **Start a new playthrough**. At the Zone 01 spawn, check the neutral pair on the character and its HUD icon.
2. Press **I** or **D-pad right**. The full-screen Arsenal pauses gameplay. Check that the portrait includes the head, feet and both hands. Select **Rotate character** and verify that only the portrait rotates.
3. Use the mouse, arrow keys, or controller to select **Farseer's Daggers**. Check the cyan blades, Second Wind description and cooldown. Training should still be marked equipped.
4. Choose **Equip**, then **Back**. Inspect both hands in the world while walking and turning the camera. Check the cyan HUD icon. Try activating at full AirPower. The meter should stay full and the cooldown should stay available.
5. Hold the normal Dash control (Left Shift or right trigger) until some AirPower is spent, then release it.
6. Press **R** or **D-pad left** once. Try once on the ground and again on a later cooldown while airborne. Look for one brief cyan burst.
7. Check that the existing AirPower meter reaches its configured maximum. Depleting it completely before activation should also make Dash available again.
8. The HUD should show about 30 seconds remaining. Another press should produce neither a refill nor a second burst. Pause for several seconds and check that the countdown holds.
9. Open **Pause → Dagger Arsenal**, select **Training Daggers**, equip them and resume. Check both hands, the HUD icon and the absence of an active ability.
10. Immediately equip Farseer's again. The timer should have lost only the intervening gameplay time. Choose **Unequip** to check that the two models and HUD disappear. Re-equipping must preserve the timer.
11. With time still remaining, choose **Pause → Save game**, save to the current slot, then return to the title. Choose **Continue**. Check the pair and timer. A separate new slot should start with Training and an independent cooldown.
12. Return to gameplay. Check movement, jump, Dash, Sling, Cast, camera and portable animation. Open **Pause → Settings → Controls**, rebind DaggerAbility to an unused input, then use the new binding after spending AirPower. Repeat menu navigation on the controller, disconnect it during cooldown, reconnect and make a fresh press. Check focus, blade alignment during fast animation, HUD legibility and burst visibility at your usual resolution.

## Authoring

Open **Tools → Windrunner → Dagger Arsenal**. Select a pair to edit its asset in the Inspector. Choose **Create pair in this catalog** to register a new definition with a stable ID. Keep released pair and effect IDs stable across renames. Set the display name, description, icon, accent color, two render-only model prefabs and hand-relative position/rotation offsets. Use a short, non-looping particle prefab for the optional activation burst. The bundled visuals are placeholder geometry.

Assign an existing active effect, leave it empty for a basic pair, or create a **Windrunner → Daggers → Second Wind** asset through the Project window Create menu. Configure its stable ID, display text, cooldown, restoration fraction and full-meter policy. A fraction of 1 restores up to the current AirPower maximum. A shared active effect shares its timer across pairs. Passive entries are optional. Neither development pair has a passive effect.

**Edit catalog and starting ownership** opens the registry, development starting list, initial pair and portrait camera distance/key-light intensity. To grant a future pair through progression, register it, set its unlock content ID to the existing reward entitlement and leave it out of starting ownership. A saved entitlement grants ownership once. Updating starting ownership affects uninitialized slots. The authoring window reports invalid IDs, missing visuals and incompatible effect wiring. During Play Mode, drag the world player's DaggerPlayer into **Play Mode player** and use **Equip on test player** for an owned selection. In a named slot this operation saves the equip.

## State and lifecycle

DaggerEquipment owns equip, rejection, activation and cooldown rules. The session's ProgressionService writes the optional loadout into the existing playthrough record. DaggerPlayer adapts the centralized input, hand attachments, HUD, effect lifetimes and scaled gameplay clock. Second Wind uses AirPower.Restore. New effects extend the active/passive definitions without changing the motor. Passive implementations return a disposable modifier or subscription.

Cooldown begins with a successful commit and activation. All active timers tick on allowed scaled gameplay time, including while the related pair is unequipped. Pause, full hit-stop, gameplay input blocks and offline time freeze progress. Partial slowdown uses the reduced game delta. Equip and activation write immediately. Normal saves, autosaves and orderly exit capture remaining time. Abrupt termination can restore an earlier, longer timer. A missing definition clears its equip while preserving ownership IDs and remaining timers.

The portrait snapshots posed mesh geometry and uses the character's hand sockets. It renders on selection or rotation and releases its meshes and render target when closed. It has no independent gameplay or audio listener. The Movement Gym's historical speed experiment is accessible to its Editor physics fixture only. The production ability has no speed multiplier or continuous drain.
