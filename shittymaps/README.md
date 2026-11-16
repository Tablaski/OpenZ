# OpenZ-VK Shitty Maps

This folder is a tracked local Unity package. A normal `git pull` retrieves the
map additions without downloading another full OpenZ resource archive.
`source/Packages/manifest.json` references it directly; there is no copy or
installation script, second Unity project, or Git LFS requirement.

It contains 60 converted variants from `D:\Zod Engine\p02*.map`, exposing 31 solo
choices and 44 converted 1v1 choices. The existing OpenZ levels 02, 07, 16 and 17
remain in the ordinary menu and are excluded from this conversion catalog.

- Solo: original numbered maps, split into **Z Original** (01–20) and
  **Z Expansion** (21–35). The absent originals for 21 and 27 use the user's
  approved, clearly labeled FIXED exception. Original and fixed Mayhem have
  separate identities, scenes and starting armies.
- Duel: exactly one preferred variant per numbered level, using FIXED/FDL when
  available; otherwise the original carries a warning pictogram. Non-numbered
  p02 maps are included in the **Multiplayer** tab. The warning means that no
  balancing variant was supplied, not that imbalance has been measured.
- Both modes provide world filters, numeric/name/world sorting and minimap
  gallery cards. Guests can inspect all cards; only the host applies a map.
- Arctic uses original snow/ice/water colors on OpenZ geometry and shared
  materials. No Zod artwork, models, audio or texture atlases are imported.

## Contents and sharing

`catalog.json` records the selected source filenames, source SHA-256 hashes,
worlds, sizes, variant policy and title-verification status. `Sources~/` preserves
the supplied native map files and terrain classification metadata for reproducible
conversion. `Resources/` contains decoded grids and generated symbolic minimaps.
`Scenes/` references the existing OpenZ prefabs. `Materials/` contains small shared
material definitions and two generated grain textures; terrain meshes regenerate
from the grids at scene loading and are never stored per map.

The initial folder is approximately 99 MiB on disk and 9 MiB with per-file gzip
compression (a size estimate, not a Git transfer guarantee). The shared OpenZ
models, shaders, textures, sounds and other base resources must already exist in
the receiving checkout. A supported freshly built player is required to play the
new catalog; pulling source data cannot upgrade an existing executable. This
folder is not itself a playable player release.

To regenerate decoded data from the supplied local files:

```powershell
node tools/Convert-ShittyMaps.cjs 'D:\Zod Engine'
```

Then run `OpenZ.EditorTools.OzShittyMapBake.All` in the project's Unity editor
(batch mode is supported). This authors scenes and previews and runs source
checks. It does not build or launch a game player. Existing generated scenes and
grids are tracked, so recipients do not need to regenerate them.

## Names and provenance

The header names in the supplied native maps are `clone_map`; they are not useful
display titles. Campaign titles and world ranges were checked online against:

- [The Bitmap Brothers' official single-player play guide](https://www.lamaisonbleue.net/infos/z/Z-Single_Player_Game_Guide.pdf), for missions 01–20.
- [ZZone's gallery](https://zzone.lewe.com/gallery.html), for all 35 mission/world identities.
- Z Wiki's [Desert](https://zzed.fandom.com/wiki/Desert),
  [Volcanic](https://zzed.fandom.com/wiki/Volcanic),
  [Arctic](https://zzed.fandom.com/wiki/Arctic) and
  [City](https://zzed.fandom.com/wiki/City) lists for extension titles.

Titles for extension levels 30, 31 and 32 could not be confirmed through the
accessible sources. They deliberately retain **Expansion 30/31/32**, as approved
by the user. Unverified custom-map titles are readable forms of their supplied
filenames, preserving the ability to identify the actual source. They are not
claimed to be historically verified titles.

Source maps originate from The Zod Engine by Michael Bok / Nighsoft and the
original Z layouts by The Bitmap Brothers. FIXED/FDL files are user-supplied
modifications. Preserve these credits, the repository licenses and upstream
attribution. No authorship of the original layouts is claimed by this fork.

## Validation

`BAKE-REPORT.txt` records every source map and generated object count.
`SOURCE-CHECKS.txt` records actual simulation source checks. Player menu,
rendering, navigation, bridge/destruction behavior, performance and two-PC match
validation remain pending a requested release build and manual testing. A scene
bake or successful C# compilation does not pass those player checks.
