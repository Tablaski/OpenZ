# Force comparison display

Options > Game > Force Bar selects Unit Count (default) or Military Value.
The preference is saved per player and applies immediately in the main menu
and in-game options. It never changes production or multiplayer rules.

Unit Count keeps the existing gauges and 70-unit limit. Military Value shows
the sum of normal construction seconds for surviving owned mobile units as
minutes:seconds (hours:minutes:seconds above an hour). Robot squads count once,
including partially depleted squads. Cannons, buildings, neutral and destroyed
units are excluded. Damage and production reductions do not change value.

The red/blue balance cursor shows each team's share of the combined selected
measure. Signed percentages use the smaller army as the reference: 1,300
versus 1,000 shows +30% and -30%. Two empty armies are equal; one empty army is
shown at the end of the scale with an infinite relative gap.

The HUD exposes `SetFortProductionBoost(team, reduction)` to show the actual
bonus supplied by the match policy as a restrained highlight and an in-bar
`LOOSER BONUS` label. Calm Mode keeps the highlight static. No toast or separate
overlay is created. The bonus policy is still being discussed; this UI revision
does not activate a production bonus or change any construction times.

The opt-in native two-player proof switches the saved display option through
the real GAME options row, captures both modes, and exercises the display hook
without changing production. It restores the user's preference afterwards.
