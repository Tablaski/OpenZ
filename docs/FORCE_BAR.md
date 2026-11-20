# Force comparison display

Options > Game > Force Bar selects Unit Count or Offensive Units Production Value.
The multiplayer preference defaults to Offensive Units Production Value and is
saved separately from the solo preference. Changing the display does not change
the army score used by multiplayer balancing rules.
Unit Count keeps the existing gauges and 70-unit limit. Offensive Units
Production Value shows the sum of normal construction seconds for surviving
owned offensive mobile units as
a numeric score. Robot squads count once,
including partially depleted squads. Cranes, unarmed mobile units, cannons,
buildings, neutral and destroyed units are excluded. Armed APCs count because
their crew can fire. Damage and production reductions do not change value.

The red/blue balance cursor shows each team's share of the combined selected
measure. Signed percentages use the smaller army as the reference: 1,300
versus 1,000 shows +30% and -30%. Two empty armies are equal; one empty army is
shown at the end of the scale with an infinite relative gap.

The force bar shows the relative army gap used by both progressive multiplayer
rules. The leader reaches the selected territory cap at a +50% lead. The trailing
fort reaches its full configured boost at a −20% gap. An active fort boost is
shown next to the army score.
The opt-in native two-player proof switches the saved display option through
the real GAME options row, captures both modes, and exercises the display hook
without changing production. It restores the user's preference afterwards.
