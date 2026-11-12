# Windrunner production migration

- Read `Documentation/ProductionMigration.md` and `Documentation/Progress.md` before changes.
- Treat scene wiring and source as truth; the `7bfdfe5e` (misnamed `5J?`) commit is the user-confirmed gameplay-tested 5P baseline. Do not assume 5Q is installed.
- Preserve script GUIDs, serialized field names/types, tuned values and public facade APIs. Move assets only with their `.meta` files. Never bulk-reserialize scenes or vendor assets.
- Regular C# owns simulation and state; MonoBehaviours adapt lifecycle, physics and serialized references. Preserve update order, collision callbacks, scaled/unscaled clocks and feedback event ordering.
- Establish parity before consolidating movement, animation, input or time-scale writers. Similar names do not prove redundancy. Keep vendor code unchanged unless necessary and documented.
- Before Unity commands check for any running Unity Editor; never open this project concurrently. Use the matching Editor recorded in ProjectSettings.
- Run relevant EditMode/PlayMode tests, inspect logs and serialized diffs, then record exact results and a local checkpoint. Never claim unexecuted checks passed.
- Do not push, merge, rewrite history or delete irreplaceable assets. Keep outstanding manual gameplay checks explicit.
