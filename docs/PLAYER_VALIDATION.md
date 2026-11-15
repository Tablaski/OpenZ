# Player validation record

## Requested release, November 15, 2026

Pending until the final release player is produced and the checks below run on
that same output folder:

- Behavior laboratory: separate tanks/robots, all four pages, live profiles,
  detailed tooltips and visible endpoint references, named save/import/export,
  next-match dropdown and clean return to main menu.
- Motion: custom movement affects simulation; visual follow stays coherent.
  Manual quality/balance assessment is still needed after automated checks.
- Live map readability: FLAG SIZE x1/x2/x4 and DOTTED TERRITORY BORDERS in both
  main-menu and in-match GAME options. Verify existing flags change live,
  original-size restoration, neutral grey / owner colours, enable/disable,
  and terrain/water alignment. Native checks run on the final combined release.
- Regression: BEHAVIOR entry must register as clickable HUD, not just be visible.
  The first native proof exercised profile APIs but missed the entry's absent
  HUD blocker; the user found it, the blocker was added, and the proof now
  verifies the real entry path before opening the pages.
- 1vs1 lobby: handicap beneficiary, shared/additional armies, build-time labels
  and independent handicap construction time; no layout overlap.
- Accumulated changes: Unit Editor entry repair, lobby/pickers, normal in-game
  options, microphone/chat, force-bar count and military-value display.
- Native multiplayer: four maps, guest orders/combat, voice transport, surrender
  and victory; host profile transmission. Internet/router/microphone listening
  checks are distinct from same-PC automated validation.

The release must disable development, script debugging and profiler connection.
Keep the entire output folder; source commits are not playable release downloads.
