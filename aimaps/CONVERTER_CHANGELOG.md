# AI Maps converter changelog

## V4 — surface detail, clean crossings and continuous cliffs

V4 renders desert, volcanic, city and jungle AI Maps that already use the V3 palette. It applies in solo, Skirmish and online matches. Arctic keeps its existing art kit. Pass `-oz-ai-maps-v3` to the same compiled game or Unity preview process to compare the earlier appearance.

### Rendering stages

1. `OzConversionTerrainV2.Build` uses the existing decoded native grid, mesh topology, palette-role paint, scene objects, biome materials, water and lighting. V3 retains responsibility for the broad colour composition and palette classification.
2. `OzAiTerrainV4.StraightRidges` marks uninterrupted runs of at least five non-lava impassable tiles along either native grid axis. V4 samples these runs without the V3 coordinate warp. Strong samples converge to a steadier crest with a sharper shoulder. Native gaps, roads, water and lava remain outside the mask. Use the two longitudinal ridges of The Jungle (16) as the reference. Compare the native map and the cliff shapes and materials of the official `L16_Jungle` scene.
3. Bridge envelopes use the native orientation and length, five tiles plus valid extensions. The protected core covers the four-tile deck and one metre beyond both edges. It fades over another 1.5 m at the sides and over one tile at either approach. Within the core, raised terrain falls below the water plane and clears the rails. Clearance takes priority over the ridge profile. Original bridge meshes, destruction stages, bank approaches and simulation geometry are retained. Molten Kombat (08), The Car Park (17) and Desert P03M01 cover lava, destroyed bridges and both orientations.
4. `OzAiTerrainV4.hlsl` adds asphalt aggregate, sand ripples, scoria, concrete pores or mossy soil detail in world space. The native road masks separate road and ground detail. Pixel-footprint filtering fades each scale at a distance. This keeps close detail independent of the resolution of the map-scale paint. The rock faces reuse the original artwork with a finer triplanar scale where supported. Native rock props retain their models and gameplay bindings.

### Compatibility and checks

- Keep the native `.map` bytes, decoded grids, tile classes, road and lava masks, objects, starting positions, territories, IDs and gameplay rules intact. Unity uses the terrain surface for selection and view height. Bridge decks and movement rules retain their existing simulation bindings.
- The shader property starts at zero on official materials, V2 and V3. V4 adds no per-map texture pack or duplicate scene.
- Headless checks cover native map loads, movement and bridge states as well as V4 bridge envelopes and ridge masks. The runtime checks are listed in [the player validation record](../docs/PLAYER_VALIDATION.md).
- The comparison plates are target previews derived from real V3 captures. Use identical camera, lighting, zoom and resolution for future Unity captures. Inspect both sides and approaches of each visible bridge.

## V3 — second palette review

`Convert-AiMapsV2.cjs --upgrade-catalog` verifies existing layouts against the native records and adds AI environment metadata and volcanic lava classifications. `OzVolcanicConversionBake.UpgradeCatalog` upgrades existing scenes. `OzConversionTerrainV2.Palette.cs` maps native tile IDs to extra rendering roles. It uses native palette IDs as visual references and the installed OpenZ detail materials for rendering.

| Biome | V3 palette roles |
| --- | --- |
| Desert | Gravel fans, dry scrub, windblown shoulders and silt |
| Volcanic | Basalt shelves, rusty scoria, ash and scorched lava edges |
| City | Concrete service yards, staggered joints, rubble and graphite asphalt |
| Jungle | Mossy verges, exposed mud and wet beds |
| Arctic | Original fork Arctic art kit with snow, rock and meltwater |

## V2 — mapping native layouts to official 3D assets

V2 paired maps available in both forms. It compared the native Zod palette and object records with official `L02_Desert`, `L07_Volcanic`, `L16_Jungle` and `L17_CarPark`, then generalised the mappings to other native layouts. The reference profiles connect 2D material roles, roads, water, cliffs, rocks, bridges and objects to equivalents in the OpenZ art kit.

`Convert-AiMapsV2.cjs` adds visual lava identifiers to decoded maps. `OzVolcanicConversionBake.Profiles.cs` selects the scene, materials and prop kit. The editor positions scene objects by native coordinates. The terrain component rebuilds the continuous terrain and material masks at load time. The separate V2 scenes retain those reference mappings.

## Native input and catalogue

`Convert-AiMaps.cjs` decodes the Zod files with their planet `.tileinfo` records. [The map package](README.md) keeps the inputs and the catalogue. Each entry records an ID, source path, SHA-256, dimensions, object count and mode availability. The generated C# catalogue and Unity build settings register map scenes and previews. The catalogue can use separate IDs for original solo layouts and balanced duel layouts where both exist.

## Maintenance

Keep each converter version available. Add a separately identifiable version for new conversion rules. Update this changelog in the same change with the rationale, inputs, mappings, render stages and validation. Only overwrite a previous version at the maintainer's explicit request. Check unchanged native data, both bridge orientations and states, ridge continuity and original gaps at tactical zoom and in close 4K views.
