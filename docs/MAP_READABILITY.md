# Live map-readability options

Both the main menu and the current match expose these preferences in OPTIONS >
GAME. They apply immediately and are saved per installation. Each multiplayer
player chooses their own presentation; no simulation or network rules change.

- **FLAG SIZE**: 1x (original), 1.5x, 2x, 2.5x or 3x. Scales the visible flag and pole at the
  same ground/roof anchor. Includes territory, fort and newly placed laboratory
  flags. Capture distances and ownership do not change. Flag motion, capture
  animations and night effects remain attached; ground capture rings keep their
  original size.
- **DOTTED TERRITORY BORDERS**: Choose Off, x1, x2 or x4. Round dots sit just
  inside each territory. A light rim and dark outer edge keep team colours
  distinct across bright and dark terrain. Neutral territory uses dark grey
  on Arctic maps, white on City maps and light grey elsewhere. Colours follow
  capture and visual team-colour changes. Marks follow ground and shallow water
  and skip impassable tiles and buildings.
- **MINIMAP RADARS**: On by default. Visible intact radars get an outline. A
  white ring expands and fades around active friendly radars, and their markers
  light up white during the sweep. White brackets identify the emitting radar
  on the battlefield. Reduced Motion keeps the radar outlines on the minimap.
  This presentation option works with any Radar Loss Consequence setting.
- **MINIMAP RADAR SWEEP INTERVAL**: Choose 1.5, 3, 5 or 10 seconds between sweeps
  from your active radars. The default is 3 seconds.

The reference is local Zod Engine's `ZMap::SetupAllZoneInfo` and `DoZoneEffects`
in `src/zmap.cpp`: owner-coloured markers along passable perimeter tiles, with
separate water markers. OpenZ-VK renders these as 3D ground dots. The optional
persistent border display and capture flashes are separate effects.

Native validation is included in the requested release's multiplayer proof:
both option menus, flag cycles while options are open, border enable/disable,
owner/neutral colours and screenshots. Terrain alignment, readability at extreme
zoom and large fort-flag occlusion still deserve manual play checks.
