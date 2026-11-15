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
keys such as oz_flag_size (1/2/4), oz_ui_scale and the audio/camera settings stable.
The territory-border key retains 0=OFF and 1=x1 and now also accepts 2 and 4.
Already-saved choices are preserved. Microphone mode is saved; Tap to speak starts
with transmission stopped and requires a fresh tap after losing focus/connection.

OzForkPreferences tracks schema 1 and snapshots changed non-sensitive settings
on startup, every 30 seconds, focus loss and shutdown. Snapshots are stored in
Application.persistentDataPath/OpenZ-VK/preferences/schema-1, with current.json
and timestamped history. They are local history, not files shipped in a build.
Existing unknown PlayerPrefs keys are not changed. A lower-version player does not
lower the saved schema marker. No automatic reset or bulk restoration is performed.
Device choices are included only in local snapshots. External profile assets and
credentials keep their existing storage and are not included in snapshots. Future key migrations must explicitly preserve the user's choice and
extend the preference manifest. Physical capture/runtime persistence still await
user testing on a requested build.

## Options presentation

Only fork additions are violet (#D570FF), with a left accent stripe, and sort after
upstream options on each tab. The shared registry currently includes FORCE BAR,
FLAG SIZE, DOTTED TERRITORY BORDERS, TANK BEHAVIOR, ROBOT BEHAVIOR and CHAT SHORTCUT.
Microphone controls and VK lobby balance options also use this accent. New options
must be registered as fork additions rather than mixed into upstream rows.

## Territorial advantage and optional cap

Original OpenZ duration is base time × (1 − 0.5 × owned flag fraction), with the
existing building-damage factor. This is not a comparison with the other team's
flag count. The VK cap limits only the territorial reduction: min(original
reduction, chosen cap). OFF — ORIGINAL adds no cap. Choices are OFF, 40%, 30%, 25%
and 20%. For example, at 80% flag ownership, original production takes 60% of base
time; a 25% cap makes it take 75%. At 20% ownership, it still takes 90% in both
cases. Territorial advantage is never removed by switching the cap off.

## Optional fort loser bonus

OFF by default; selectable maximum reductions are 30%, 50% and 75%. It uses the
normal cumulative production seconds of surviving offensive mobile units. Robot
squads count once; cranes, unarmed units, turrets and buildings do not count. APCs
count because their crew can attack. Unit health does not discount normal value.

Let enemy lead = enemy value / own value − 1. The reduction is maximum ×
clamp((lead − 0.30) / 0.60, 0, 1). At the 50% maximum: +30% lead gives 0%, +50%
gives 16.7%, +70% gives 33.3%, and +90% or more gives 50%. Both empty gives zero;
only the weaker side can receive a bonus; an empty army against a nonempty army
receives the configured maximum. No territory threshold applies.

Only the fort benefits. The factor multiplies the normal territory/damage/handicap
duration, respecting the minimum build duration. Changes preserve the fraction of
work already completed. The force comparison bar shows the actual bonus with a
label/glow; it works in either unit-count or offensive-value display mode.

## Duel flow

Escape contains Restart level followed by Surrender, with no chat entry. Restart
asks the opponent and reloads the same map/rules after consent, using the normal
two-player loading barrier. Decline/expiry leaves the game running and notifies
both peers. Protocol version 2 rejects incompatible older peers.

Chat and the microphone menu sit to the right of the RVBG/1–0 command bar. Chat's
shortcut is bound in OPTIONS > GAME. Microphone offers Always on, Tap to speak
and Mute; the local meter shows post-gain input even while muted and sends no
muted audio. Shortcut/device/gain settings remain in microphone options.

At match end, both peers see the solo-style statistics and chart, using final
counters published by the host. Duel scores do not enter solo high-score tables.
Return to lobby waits for both players, preserving seats/map/rules and clearing
ready flags. Either player can leave instead. Handicap selection shows player
names. Lobby maps include L02/L07/L16/L17 plus their world names.
