# Behavior laboratory

Open the Unit Editor and click **BEHAVIOR** at the bottom right. **TANK BEHAVIOR**
and **ROBOT BEHAVIOR** keep separate drafts. Their DODGING, MOVEMENT, BOOST and
COMBAT pages expose 72 parameters. Every parameter has detailed hover help and
shows its original OpenZ and Zod Classic reference values. Family-wide values
that differ by unit show a range; the tooltip lists each exact value.

## References and master

With no overrides, master 0 preserves the original OpenZ code path. Master 100
uses the classic Zod dodge semantics before fenio commit `37af18e`: tanks may
interrupt attack orders, engaged robots may not dodge, random defensive
directions and endurance-based sprint remain. Direct visual following removes
the added mechanical follower. This reference does not enable global Zod exact
mode or replace weapon statistics, collision/pathing, or other game systems.
The **Zod Local (idle only)** reference also prevents dodges during active orders.

Numeric parameters interpolate; discrete fields change at 50%. Parameters not
implemented in Zod are explicitly disabled, instantaneous or use the corresponding
native equivalent. A higher master is not a guaranteed higher survival rate.

Editing a field creates an override. Master changes preserve overrides. A unit
exception takes precedence over a family override; clicking OVERRIDE releases
that field. RESET OVERRIDES releases the family's exceptions. The scope control
cycles all units and individual types, including the mech separately from tanks.

## Live simulation and presets

Edits change existing and future units in the laboratory without respawning or
refilling endurance. Pause to edit before the next tick. Enabling physical limits
constrains real movement; visual steering fields affect presentation only.
Zero acceleration/braking time and zero turn rate mean instant, not stationary.
Braking affects arrival; explicit stop orders and blocked movement still stop
safely. Discrete profile switches reset movement controller state, not positions.

Retreat means a move away from an acquired enemy, not a new order button.
Retreat Dodge keeps native moving fire while allowing defensive escape.
Predictive dodging samples unblocked directions and estimates reachable positions
before the blast. It does not guarantee an escape or solve every simultaneous
threat optimally. DEBUG displays threat/dodge lines and refusal reasons.

SAVE updates the current custom preset; SAVE AS creates another named preset.
Draft autosave is separate from the saved library and next-match choices.
Saved presets freeze reference endpoints while preserving editable inheritance.
EXPORT/IMPORT exchange `.ozbehavior` files in the UnitEditor/BehaviorPresets
folder (FOLDER opens it). Invalid fields, foreign unit types and non-finite
numbers are rejected. Built-in references cannot be overwritten or deleted.

Choose **TANK BEHAVIOR** and **ROBOT BEHAVIOR** independently in **Options > Game**.
These choices apply at the next world creation, to both teams. Laboratory edits
do not silently change these selections. In multiplayer the host's profiles are
replicated to guests, including their presentation settings.

Existing unit-stat profiles still control health, weapons, normal speed and
maximum endurance. This feature does not replace the statistics editor.

## Build it yourself

Close Unity if this project is open, then double-click `BUILD-OPENZ.bat` in the
repository root. It finds the configured Unity editor and builds a Windows
release into `builds/OpenZ-Release`. Wait for SUCCESS. Start `OpenZ-Duel.exe`
there and keep the complete folder together when moving or sharing the game.
`SOURCE_VERSION.txt` identifies the source commit and any uncommitted files.
Errors are in `tests/Multiplayer/.cli/Unity-build-OpenZ-Release.log`.

This requires the complete existing local Unity project and its configured
Unity installation. The launcher does not download missing assets or licenses.

## Validation scope

The headless suite checks native equivalence, endpoint values, inheritance,
serialization, live order interruption, independence and replication. The native
Unit Editor proof opens all eight pages and exercises save/import/export and
the game dropdown. A fresh release player is required to validate runtime/UI.
Manual play remains necessary to judge animation quality, balance and whether a
preset is enjoyable. A saved-state combat replay/batch comparison interface is
not included in this implementation; existing pause/sample-fight controls remain.
