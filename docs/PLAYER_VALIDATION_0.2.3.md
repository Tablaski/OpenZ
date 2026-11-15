# OpenZ-VK 0.2.3 player validation

Pending manual validation on the next requested release player:

- Dotted territory borders: x1 has one quarter of the previous radius and diameter; x2 and x4 retain their relative multipliers. Verify live switching and visibility on land and water. Spacing is unchanged.
- Offensive units production value in the top bar: show rounded whole-number scores without a time format or unit suffix (90 instead of 1:30 and 3600 instead of 1:00:00). Production-value calculations, percentages, balance cursor and unit-count mode are unchanged.
- Existing border-size and force-bar preferences are preserved.

No player build or gameplay/rendering checks were requested or performed.

Completed source checks: all Unity runtime scripts compile (existing warnings only); Git diff whitespace check passed.
