# Live map-readability options

Both the main menu and the current match expose these preferences in OPTIONS >
GAME. They apply immediately and are saved per installation. Each multiplayer
player chooses their own presentation; no simulation or network rules change.

- **FLAG SIZE**: x1 (original), x2 or x4. Scales the visible flag and pole at the
  same ground/roof anchor. Includes territory, fort and newly placed laboratory
  flags. Capture distances and ownership do not change. Flag motion, capture
  animations and night effects remain attached; ground capture rings keep their
  original size.
- **DOTTED TERRITORY BORDERS**: OFF by default. Thin, softly edged dashes,
  0.18 metres wide, about 0.8 metres long every 2 metres, drawn just inside each
  territory. Adjacent territories can show their own colours independently.
  Unowned territory uses grey. Colours follow capture and visual team-colour
  changes. Marks follow terrain/water and skip impassable walls and buildings.
- **MINIMAP RADARS**: On by default. Visible intact radars get an outline. A
  ring expands and fades around active friendly radars. Reduced Motion keeps
  the outlines and hides the sweep. The ring marks the radar's position.
  Fog rules and detection ranges remain unchanged.

The reference is local Zod Engine's `ZMap::SetupAllZoneInfo` and `DoZoneEffects`
in `src/zmap.cpp`: owner-coloured markers along passable perimeter tiles, with
separate water markers. OpenZ renders these as static 3D ground dashes rather
than importing sprites or changing map resources. Existing capture flashes are
separate from the optional persistent border display.

Native validation is included in the requested release's multiplayer proof:
both option menus, flag cycles while options are open, border enable/disable,
owner/neutral colours and screenshots. Terrain alignment, readability at extreme
zoom and large fort-flag occlusion still deserve manual play checks.
