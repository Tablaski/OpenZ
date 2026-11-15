# Unit testing laboratory

In the Unit Editor, click **UNIT TESTING** at the bottom right, or choose the
tab beside BEHAVIOR. It uses the same native right-hand sidebar as STATS.
Hover controls for detailed explanations. These rules belong to this playground
world; they do not enter saved combat profiles or ordinary matches.

RED and BLUE each have independent immortality, circular damage and reference
HP controls. The HP slider and numeric field cover 1-100% of each unit's own
maximum health, rounded to the nearest integer HP with a one-HP minimum.
Changing the value immediately resets current living units and their hidden
damage pool; future units inherit it. Kill/death counts are preserved.

| Immortal | Circular damage | Effect |
| --- | --- | --- |
| OFF | Either | Units start at the selected HP and take ordinary lethal damage. |
| ON | OFF | Actual HP remain pinned at the selected percentage. A hidden pool records the damage and deaths that would have occurred. |
| ON | ON | HP decrease normally. Each lethal hit records one virtual death and restores exactly the selected percentage. Overkill is discarded. |

Immortal units stay in place, keep their orders and target, and avoid destruction,
leader succession and removal. Their crews are protected against sniper defeat;
crew defeat still records a virtual kill/death. Testing covers owned robots,
vehicles and cannons, not buildings, neutral objects or terrain. Robot squad
members count individually.

**Show damage** displays landed HP losses above units, including theoretical
losses for invulnerable units (`*`) and identified crew damage. Misses do not
produce damage numbers. **Show kill and death counts** displays large trophy
and skull counters. Virtual defeats credit the responsible enemy attacker;
environmental/self-inflicted defeats have no credited killer. Counts accumulate
even with the display disabled, survive HP resets and disappear with removed
units. Ordinary mortal deaths retain their counters briefly above the wreck.

Display elements are native canvas text/vector shapes with no new asset dependency.
Events and floating-number pools have bounded queues. Visual clarity, overlap
and readability still require validation in a freshly built release player.
