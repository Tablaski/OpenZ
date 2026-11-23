# OpenZ-VK AI Maps

This folder is a tracked local Unity package. A normal `git pull` retrieves the
map additions without downloading another full OpenZ resource archive.
`source/Packages/manifest.json` references it directly; there is no copy or
installation script, second Unity project, or Git LFS requirement.

The catalog offers 49 solo and 48 AI map choices for 1v1. Levels 02, 07, 16, 17 and 18 have V3 conversions in AI Maps alongside their official versions in the OpenZ campaign and lobby gallery.

- Solo groups the converted maps under Z Original, Z Expansion and Zod Engine.
  It uses the OpenZ mission carousel with a looping 3D preview and minimap on the selected map.
  Other cards show the full map as a still. All AI cards carry the AI stamp.
- The 1v1 gallery provides world filters, numeric/name/world sorting, a looping
  video for the selected map and a larger tactical minimap. Guests can inspect
  maps while the host applies the shared selection.
- Arctic uses original snow, rock and water materials. Other planet profiles use
  the installed OpenZ detail textures and procedural paint.

## Contents and sharing

`catalog.json` records the selected source filenames, source SHA-256 hashes,
worlds, sizes, variant policy and title-verification status. `Sources~/` preserves
the supplied native map files and terrain classification metadata for reproducible
conversion. `Resources/` contains decoded grids, tactical minimaps and short preview videos.
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
node tools/Convert-AiMaps.cjs 'D:\Zod Engine'
```

Then run `OpenZ.EditorTools.OzAiMapBake.All` in the project's Unity editor
(batch mode is supported). This authors scenes and previews and runs source
checks. It does not build or launch a game player. Existing generated scenes and
grids are tracked, so recipients do not need to regenerate them.

## V2 planet profiles

Run the V2 converter on the native maps, then run **OpenZ → AI Maps → Bake V2 reference maps**. The editor method is
`OpenZ.EditorTools.OzVolcanicConversionBake.ReferenceMaps`.

```powershell
node tools/Convert-AiMapsV2.cjs 'D:\Zod Engine\p02_bb_orig16.map' SM16V2
node tools/Convert-AiMapsV2.cjs 'D:\Zod Engine\p02_bb_orig17.map' SM17V2
node tools/Convert-AiMapsV2.cjs 'D:\Zod Engine\p02_bb_orig02.map' SM02V2
node tools/Convert-AiMapsV2.cjs 'D:\Zod Engine\p02_bb_orig07.map' SM07V2
node tools/Convert-AiMapsV2.cjs 'D:\Zod Engine\p02_bb_orig08.map' SM08V2
```

Each profile was reconciled with its OpenZ reference grid, the Zod editor
palette and the Fenio fork's `palette_tile_info`. The converter keeps native
tiles, travel classes, zone rectangles and object records. The volcanic lava
lookup also uses the animation links to identify the hot pool frames.

| Profile | Reusable OpenZ art |
| --- | --- |
| Jungle | L16 grass, mud, road and moss layers, swamp material, rock slices, trees and plank bridge |
| City | L17 street detail and road markings shader, concrete and cliff materials, harbour water, street furniture, city RockZ and bridge stages |
| Desert | L02 sand and asphalt details, sandstone, river material, palms, desert RockZ, grenade crates and bridges |
| Volcanic | L07 ash and asphalt details, triplanar cliffs, animated lava, volcanoes, volcanic RockZ and crystals |
| Arctic | Original snow, slate textures, destructible rock meshes, frost-covered steel bridge stages and cold lighting, plus OpenZ water ripples |

The same authoring code derives roads, banks, water surfaces, bridge spans
and collider geometry from each supplied layout. The scenes reference the
installed OpenZ assets and fit authored scenery to each converted layout.

To try another layout on a supported planet, use a new `SM...V2` ID and
call `OzVolcanicConversionBake.Bake(id)` in the editor. The current catalog
registers the reference maps and the additional Molten Kombat trial.

## V3 palette pass

The playable catalog uses V3. The four prototype conversions of official OpenZ missions are kept as editor references. The Arctic maps use the original Arctic art kit.
The shared Molten Kombat entry covers both solo and 1v1.

```powershell
node tools/Convert-AiMapsV2.cjs --upgrade-catalog
```

Then run **OpenZ → AI Maps → Upgrade the existing catalog to V3**.
The batch command matches the existing scenes by ID and verifies the recorded
source hashes and native records before updating the volcanic lava fields.
The editor upgrades the art in each existing scene in place.

V3 reads the decoded palette tile IDs and blends additional material roles into
the OpenZ ground shaders. The Fenio atlas was a visual reference. The conversion
uses identifiers and class metadata, with new procedural paint and the installed
OpenZ detail textures. No atlas pixels are included in the map package.

| Profile | Additional material roles |
| --- | --- |
| Desert | Gravel fans, dry scrub, windblown shoulders and silt |
| Volcanic | Basalt shelves, rusty scoria, ash and scorched lava edges |
| Jungle | Mossy verges, exposed mud and darker wet beds |
| City | Concrete service yards with staggered joints, rubble and graphite asphalt |

For a custom layout, pass a new `SM...V3` ID to `Convert-AiMapsV2.cjs` and
then to `OzVolcanicConversionBake.Bake(id)`. Use a separate `SM...V2` ID for the
V2 look. The two identifiers decode identical native gameplay data.

## Chilly Willy V2

`node tools/Make-ChillyWillyV2.cjs` writes the original `vk_chilly_willy.map`
and its decoded grid. Its two channels, bridge sites, territories and starting
armies are authored in the generator. Chilly Willy (level 11) informed the choice
of terrain roles. The Arctic palette metadata provides native tile semantics.

The **Bake Chilly Willy** editor command generates the scene and preview.
The **Rebuild original Arctic art** command recreates the Arctic materials,
textures, rock meshes and bridge prefabs stored in `Art/Arctic/`. A normal
checkout has the generated assets ready for Unity.

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
original Z layouts by The Bitmap Brothers. Preserve these credits, the repository licenses and upstream
attribution. No authorship of the original layouts is claimed by this fork.

The orbital backgrounds in `Resources/OpenZ/AiMaps/Loading/` adapt the OpenZ
intro's `IC_Planet_BaseColor.png` with new surface detail, biome colours and
lighting. Credit OpenZ for the original texture. These backgrounds are shared
under [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/).

## Validation

`BAKE-REPORT.txt` records every source map and generated object count.
`SOURCE-CHECKS.txt` records actual simulation source checks. Player menu,
rendering, navigation, bridge/destruction behavior, performance and two-PC match
validation remain pending a requested release build and manual testing. A scene
bake or successful C# compilation does not pass those player checks.
