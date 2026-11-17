# Dagger Arsenal

Use the MainMenu scene and a new, disposable playthrough for acceptance. New development slots start with Training Daggers equipped and own Farseer's Daggers. Opening World_Zone01 or StartingWorld directly in the Editor uses a temporary session. Use a named slot to verify persistence.

## In-game acceptance

1. Open `Assets/WindrunnerFrontend/Scenes/MainMenu.unity`, press Play, choose **New game**, then **Start a new playthrough**. At the Zone 01 spawn, check the neutral pair on the character and its HUD icon.
2. Press **I** or **D-pad right**. The full-screen Arsenal pauses gameplay. Check that the portrait includes the head, feet and both hands. Select **Rotate character** and verify that only the portrait rotates.
3. Select **Farseer's Daggers**, choose **Equip**, then **Back**. Look for the smaller cyan **SECOND WIND 0 / 50** bar just above the main AirPower meter. Inspect the hand models while moving the camera.
4. With main AirPower full, earn an Air Node return by hitting a node with Air Cast, or destroy a Sling Node and wait for its return stream. Check that the reserve increases on delivery. Another reward at 50/50 should leave it at 50.
5. Press **R** or **D-pad left** while the main bar is full. The reserve should stay charged. Spend a small amount with Dash and release Dash. The reserve should show **READY**.
6. Press the ability once. Look for one cyan transfer burst. The main meter accepts up to its missing capacity and the reserve empties. For example, releasing 50 with only 10 missing returns 10. A second press should have no effect until more overflow is earned.
7. Bank more overflow, equip Training Daggers and finish a reward. The reserve bar should disappear. Equip Farseer's again and check that the same amount returns. Repeat during a jump to check airborne activation.
8. Save a named playthrough while charged, return to the title, and choose **Continue**. Check the reserve, then start a separate new slot and check that it starts empty. Pause, exercise controller navigation and verify the transfer only fires on a fresh gameplay press. Judge HUD spacing and brightness at your usual resolution.

## Authoring

Open **Tools → Windrunner → Dagger Arsenal**. Select a pair to edit its asset in the Inspector. Choose **Create pair in this catalog** to register a new definition with a stable ID. Keep released pair and effect IDs stable across renames. Set the display name, description, icon, accent color, two render-only model prefabs and hand-relative position/rotation offsets. Use a short, non-looping particle prefab for the optional activation burst. The bundled visuals are placeholder geometry.

Assign an existing active effect, leave it empty for a basic pair, or create a **Windrunner → Daggers → Second Wind** asset through the Project window Create menu. Configure its stable ID, reserve capacity, eligible reward sources, storage ratio, transfer efficiency, meter color, optional fill sprite and optional charge burst. The stock profile banks up to 50 at 1:1 and transfers at 100% efficiency. The HUD brightens briefly on charge. The pair's activation VFX plays on successful release. Every successful release spends the entire bank, including energy the main meter cannot accept. Passive entries are optional. Neither development pair has a passive effect.

**Edit catalog and starting ownership** opens the registry, development starting list, initial pair and portrait camera distance/key-light intensity. To grant a future pair through progression, register it, set its unlock content ID to the existing reward entitlement and leave it out of starting ownership. A saved entitlement grants ownership once. Updating starting ownership affects uninitialized slots. The authoring window reports invalid IDs, missing visuals and incompatible effect wiring. During Play Mode, drag the world player's DaggerPlayer into **Play Mode player** and use **Equip on test player** for an owned selection. In a named slot this operation saves the equip.

## State and lifecycle

AirPower grants the original reward once and measures the excess against current main capacity. Only the Air Node hit and Sling Node death return streams submit eligible rewards. Passive recovery, capacity reconciliation, debug refills and releases bypass the overflow path. DaggerEquipment applies the equipped profile to that excess and commits the resulting balance through the session's existing progression store. The balance belongs to the pair ID within that slot. It survives swapping, pausing and offline time.

Older cooldown-based saves start with zero reserve. An installed reserve definition retires its old cooldown record without awarding energy. A missing definition clears an unavailable equip and retains its records for later installation. Lowering capacity clamps the stored balance. A full main meter, empty reserve or blocked gameplay rejects release.

The portrait snapshots posed mesh geometry and uses the character's hand sockets. It renders on selection or rotation and releases its meshes and render target when closed. It has no independent gameplay or audio listener. The Movement Gym's historical speed experiment is accessible to its Editor physics fixture only. The production ability has no speed multiplier or continuous drain.
