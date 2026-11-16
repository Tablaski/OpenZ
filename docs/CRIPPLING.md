# Tank crippling and shortest hull turns (0.2.7)

Unit Editor > Dynamics > Tanks > CRIPPLING edits the selected family or tank
type live. SAVE AS stores a named profile; the host selects that profile under
DYNAMICS in the 1 vs 1 lobby. The guest can inspect it, and it is fixed for the
match, including restarts. Per-type exceptions take priority over family values.

## Health bands

The bands use the existing Unit Editor hover-card boundaries:

| Band | Remaining HP | Default retained speed | Extra acceleration | Shake | Rear smoke | Oil width / lifetime |
| --- | --- | --- | --- | --- | --- | --- |
| Green | >50% | 100% | 0 s | 0 degrees | 0 puffs/s | 0 m / 20 s |
| Yellow | >25% through 50% | 90% | 0.35 s | 0.8 degrees | 3 puffs/s | 0.16 m / 20 s |
| Red | <=25% | 80% | 0.8 s | 1.8 degrees | 6 puffs/s | 0.26 m / 30 s |

Custom damage movement and trails defaults to OFF. Turning it off retains the
native damage-speed rules (their 70%/40% thresholds are independent) and original
smoke/fire effects. The main game overlay still uses its upstream low-HP display;
the three band names correspond to the editor hover card. Original damage/fire
smoke is preserved when the new rear smoke is enabled or disabled.

Retained speed replaces the native damage speed factor, retains terrain/boost
factors and is effectively clamped to 0-100%. Zero stops real travel without
deleting orders. Extra acceleration time applies with physical limits OFF too;
with physical limits ON it adds to the configured acceleration time. Braking is
independent. Visual effects do not change combat positions or consume simulation
random numbers. Shake is measured in degrees and respects reduced motion.

Zero shake, smoke rate or oil width disables that effect. Oil trails start at
the hull rear only while moving, stop when stationary or held, and fade linearly
over their duration in simulation seconds. Pause freezes their fade. The scene
owns a pool of at most 512 segments and recycles the oldest marks under load.
Teleport gaps are discarded. Marks do not affect friction or damage.

## Impact penalties

Movement penalty on damaging hits is an independent switch, OFF by default.
Each tier has an actual HP-loss threshold, retained speed and duration. Defaults:
light 1 HP / 45% / 0.2 s; heavy 1000 HP / 2% / 0.6 s. Configure the heavy
threshold above the light one for two distinct tiers. Zero duration disables
a tier. Zero retained speed completely stops travel for the duration.

Heavy qualifying hits take priority. Repeated qualifying hits extend the end
time and retain the strongest active cap; they cannot weaken or shorten it.
The impact factor multiplies the current movement speed, including per-band
slowing when enabled. Hull turning and orders remain active. Acceleration rules
apply when the penalty expires. Friendly hits, HP editing, healing, misses and
zero HP loss do not trigger the penalty. Unknown/removed attackers are accepted
for explosions. This concerns hull HP, not driver health. Playground immortal
modes that suppress actual HP loss also suppress the impact penalty.

## Rotation and compatibility

Tank physical movement selects the nearest front/rear hull axis when reversing
is enabled: forward for offsets up to 90 degrees, reverse for larger offsets.
Rear movement retains reverse speed and delay. Brake-pivot-accelerate brakes
along the previous travel axis before reversing direction, then pivots toward
the chosen axis and accelerates. Exact side-on ties choose forward. Turning
reversing OFF explicitly retains forward-only physical movement. Direct and
legacy visual followers also choose the nearest tank axis.

Existing behavior enum IDs and the profile format stay unchanged; new keys are
appended. DamagedBoost moves from BOOST to CRIPPLING without changing its ID or
value. The profile drafts/library/selection preference keys remain unchanged.
Preference schema 3 snapshots their complete values under the existing local
OpenZ-VK/preferences folder; older histories and unknown keys remain untouched.
No personal settings, profile libraries or history are shipped in the release.

The changed replicated presentation schema requires both multiplayer players
to update together. Source checks do not certify the appearance or balance;
the persistent pending checklist is in PLAYER_VALIDATION.md.
