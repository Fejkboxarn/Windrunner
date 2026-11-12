# Character Tuning Studio

Open **Tools > Windrunner > Character Tuning**. The window edits the existing module ScriptableObjects through isolated drafts; it is not a new gameplay configuration authority. Petra's binding preset is `Assets/AirflowPrototype/Generated/PetraCharacterTuning.asset`. Existing numerical values, scene overrides and profile sharing are preserved.

## Tune Petra

1. Open Batch1_Playground. Select **Airflow Player** in Hierarchy, then **Use selected player** in the window. Or drag that player root into Character and click **Load / discard drafts**. The camera modules are discovered from that player's explicit ViewCamera once when loaded.
2. Choose Movement, Jumping, Aerial, Air Dash, Air Sling, Air Cast, Camera, Animation, Resources, Feedback or Water. Search matches property names, tooltips, asset names and owners. The window shows exact asset paths and component fields that consume them. Hover a setting for the original tooltip, units and loaded baseline. Compound curves/lists remain editable through standard Unity property drawers.
3. Edits affect draft copies only. Use Unity Undo/Redo and **Compare: changed values only**. **Revert drafts** restores the loaded authored values; these are the actual scene's defaults, not the C# constructor defaults. **Validate** checks finite numbers, authored Min/Range constraints and camera/Sling ordering.
4. In Edit Mode, **Save changes to assets** explicitly saves modified module profiles. Unchanged saves do not rewrite files. Shared assets remain shared: duplicate profiles before making another character independent. External edits after loading cause Save to reject stale data; reload to resolve the conflict. Scene/prefab reference overrides are never silently replaced by saving scalar tuning.
5. To test, enter Play Mode, select/load the running player again, and enable **Live preview (safe modules only)**. Movement/jump/Dash/resources/camera values in AirflowMovementSettings, Sling, Cast, targeting/rewards and water profiles use isolated runtime drafts. Changes are visible through the same profile references gameplay reads. Another character still using the original asset is unaffected. Revert, disabling preview, closing the window or exiting Play restores bindings.
6. To keep Play Mode adjustments, click **Save changes to assets** before stopping. Saving is always explicit. Mode changes discard unsaved drafts; no automatic persistence or PlayerPrefs copy is performed. Animation graph topology, feedback/audio pools and other cached modules are intentionally restart-only: save, stop, then start Play again. Do not interpret a draft in a restart-only module as a live change.

## Future characters, presets and overrides

Start with the same production component composition and your compatible Humanoid rig. **Capture preset** saves a named index of component/path/property-to-profile bindings. It contains no duplicated gameplay rules. **Duplicate character profiles** creates a new preset plus independent copies of its module profiles, remapping nested profile references to the copied assets. Select the other character, choose the new preset, then **Apply preset** in Edit Mode. All bindings/type compatibility are checked before any modification; missing hierarchy paths are reported rather than guessed. Apply uses Undo and records prefab overrides; save the scene/prefab deliberately through Unity afterward.

Preset application is only a reference-wiring operation. Existing bindings remain unchanged until explicitly applied. Runtime scalar state, input rebinds, scene-node overrides, target health and transforms are not persisted by this tool. Node-specific perfect timing and rewards remain encounter overrides, not character defaults; character fallback values cannot override an explicitly configured node. Collision masks and rig/scene wiring are composition, not silently migrated tuning.

## Safety and limits

The window performs discovery and cloning on Load, never in a gameplay Update. It supports the existing logically separate module profiles rather than a new monolithic data model. Live preview has an explicit reviewed type allowlist; graph/pool rebuilding is not performed mid-ability. Source/profile missing-reference audits and production ownership tests complement the window's range validation; it is not a substitute for visual/audible acceptance.

Configuration tests cover exact clone parity, preserved shared references, draft isolation, Revert, invalid ranges, conflicting values, preset capture and no-op saves. Play Mode tests verify binding isolation across characters and restoration after preview. Missing authored clips remain safe animation fallbacks; no hidden legacy playback is needed to preview missing clips.
