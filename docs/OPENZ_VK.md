# OpenZ-VK identity, settings and balance rules

## Fork version

The initial independent fork version is **0.2.0**, defined once in
`source/Assets/OpenZ/Scripts/UI/OzForkVersion.cs`. This identifies the new source
feature set; it is not a newly compiled player. Upstream OpenZ retains its own
version and credits.

Every requested build is stamped by OzForkBuildStamp with the full Git commit,
a modified-tree flag and UTC timestamp in FORK_BUILD.json beside the executable.
The bottom-right watermark displays the fork version, short commit, modification
marker and build time in all scenes. Missing metadata is visibly UNSTAMPED BUILD;
editor source sessions say SOURCE. Keep the metadata with the entire player folder.
Two builds from the same source can be distinguished by their UTC timestamps.

Display branding is OpenZ-VK. Internal assets, namespaces, reference-stat labels
and upstream credits remain OpenZ. The persistent version watermark also identifies
loading, faction selection, paused and end-screen recordings.

## Preference continuity

Company/product remain OpenZ/OpenZ to retain existing PlayerPrefs and persistent
files. Moving or replacing a player folder does not change this identity. Keep
keys such as oz_ui_scale and the audio/camera settings stable. Fractional flag
sizes and radar intervals use new keys. Earlier radar intervals and the 1x and
2x flag choices carry forward. The former 4x flag choice is read as 3x, and the
legacy value remains available to earlier versions.
The territory-border key retains 0=OFF and 1=x1 and now also accepts 2 and 4.
Already-saved choices are preserved.

OzForkPreferences tracks schema 2 and snapshots changed non-sensitive settings
on startup, every 30 seconds, focus loss and shutdown. Snapshots are stored in
Application.persistentDataPath/OpenZ-VK/preferences/schema-2, with current.json
and timestamped history. They are local history, not files shipped in a build.
Existing unknown PlayerPrefs keys are not changed. A lower-version player does not
lower the saved schema marker. No automatic reset or bulk restoration is performed.
Device choices are included only in local snapshots. External profile assets and
credentials keep their existing storage and are not included in snapshots. Future key migrations must explicitly preserve the user's choice and
extend the preference manifest. Physical capture/runtime persistence still await
user testing on a requested build.

Schema 2 extends the manifest with both behavior drafts, the named profile library
and both selected profile IDs. Schema 1 history remains in place; existing values
are neither reset nor rewritten. New movement controls are appended to the behavior
schema, preserving legacy numeric setting IDs and named JSON fields. Profiles
without the new controls keep brake-pivot-accelerate disabled.

## Brake, pivot, then accelerate

In Unit Editor > Dynamics > Tanks, select ALL TANKS or an individual type and use
BRAKE > PIVOT > ACCELERATE on FEEL or MOVEMENT. This changes only eight movement
values: Physical ON, TurnFirst ON, PivotSpeed 0%, AlignmentTolerance 5 degrees,
TurnRate 720 degrees/s, TurnAcceleration 0, Acceleration 0.25 s and Braking 0.15 s.
It preserves dodge settings, master and existing type exceptions. SAVE AS stores a
named profile; select it in Options > Game to use it in matches.

With physical limits and TurnFirst enabled, a sharp change brakes along the old
hull axis first, then pivots at the chosen rate once at or below PivotSpeed, then
accelerates when within AlignmentTolerance. Zero PivotSpeed stops completely;
a small percentage allows creeping. While enabled this mode takes precedence over
free lateral movement, reversing and the older pivot/turn-moving switches.
Acceleration and braking remain independently editable. Explicit stop orders and
collisions retain their immediate safe stop. These controls also use the existing
robot-family and per-type inheritance, though robot visual facing remains separate.

Physical presentation extrapolates actual accepted movement, rather than the
commanded velocity, so a stationary pivot does not slide toward its target between
ticks. Live physical tuning retains the established heading. DEBUG ON reports
Braking, Pivoting, Accelerating or Driving plus actual simulation speed.

## Options presentation

Only fork additions are violet (#D570FF), with a left accent stripe, and sort after
upstream options on each tab. The shared registry currently includes FORCE BAR,
FLAG SIZE, DOTTED TERRITORY BORDERS, TANK BEHAVIOR, ROBOT BEHAVIOR and CHAT SHORTCUT.
The online lobby uses its orange and neutral presentation. Register new fork options in the shared registry.

## Territorial advantage and optional cap

Original OpenZ reduces build time by 50% times the fraction of flags owned.
The cap limits that reduction and defaults to 50%, which preserves the original
rule. The slider goes down to 10%. In progressive mode the leading army's cap
decreases linearly from 50% to the chosen setting at a +50% offensive production
value lead. The trailing army retains the original cap in progressive mode.
The signed army gap is visible in Offensive Units Production Value on the force bar.

## Optional fort construction speedup

The fort speedup ranges from Off to 500% of the opponent's effective territorial
speed bonus. For example, a 20% reduction in enemy build time gives the enemy
+25% speed. A full 500% setting grants the trailing fort +125% speed. The fort
uses whichever gives it the faster build time, its normal territorial bonus or
the catch-up boost. Building damage and Player Advantage still apply, and speed
changes preserve completed work.

The boost starts as soon as the army trails. Increasing progressively reaches
full strength at a −20% army gap. With progression disabled the configured boost
applies in full from the first deficit. When enabled, Wider Howitzer Spread
increases shell dispersion for ground and fort-mounted howitzers on every side.
## Duel flow

Escape contains Restart level followed by Surrender, with no chat entry. Restart
asks the opponent and reloads the same map/rules after consent, using the normal
two-player loading barrier. Decline/expiry leaves the game running and notifies
both peers. Both peers must run compatible builds.

Text chat sits to the right of the RVBG/1–0 command bar. Set its shortcut in Options > Game.

At match end, both peers see the solo-style statistics and chart, using final
counters published by the host. Duel scores do not enter solo high-score tables.
Return to lobby waits for both players, preserving seats/map/rules and clearing
ready flags. Either player can leave instead. Handicap selection shows player
names. Lobby maps include L02/L07/L16/L17 plus their world names.
