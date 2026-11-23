# AI Maps converter changelog

## V5 — five material studies on V4 geometry

Launch with `-oz-ai-maps-v5` to enable V5 on Desert (SM02), Molten Kombat (SM08), Chilly Willy (SM11), The Jungle (SM16) and The Car Park (SM17). Other maps continue to use V4. The V3 switch takes precedence.

- The approved direction for the revision is material detail on the existing OpenZ scene. V5 and V4 share vertices, triangle indices, the collider, shoreline shape, bridge decks, ramps and placements. The first V5 study (`a1e835ca`, refined in `d37fd441`) trialled higher exterior relief and procedural props. The revision retains that history and removes those effects from the active renderer.
- `OzAiTerrainV5.hlsl` layers metre-scale weathering, resolvable detail and bump normals over the official OpenZ shaders and textures. The gradient-based bump is applied in the lighting pass. The layer scales fade with the measured pixel footprint and leave road boundaries and markings to the existing masks. Terrain V5 and rock V5 material properties default to zero.
- Desert uses the native gravel and scrub regions to guide mineral stains, darker sand, wind patterns and sandstone grain. Volcanic uses angular scoria, grey ash, porous basalt and the original hot-tile mask. Small open scoria cones reference native rim tiles 260, 263, 267, 334, 354, 389, 406, 425 and 448. They are roughly 3–5 m across and 1–3 m high, with spacing, an irregular rim and an excavated-looking throat. They sit only on the impassable basalt perimeter, outside bridge clearance.
- Arctic uses 103 ground tiles from SM11 that depict fractured ice in the native atlas. The shader adds a thin reflective glaze with fine fractures, exposes gravel on existing roads and puts occasional narrow drifts over the road texture. The water shader adds a faint low haze while retaining its shoreline and tile classifications.
- Jungle uses the V4 shelf and basin relief. In SM16 the two central cuts receive damp silt on the existing basin floor and water shading near the actual bottom, to avoid apparent water shelves halfway up the cliffs. Water coverage remains driven by the native wet cells. The central road retains the V4 mesh and grade.
- City aligns chipped joints to the established 6.6 × 4.7 m staggered paving and limits coloured inlays to the original service-yard mask. Sewer outlets use native mouth art at tile IDs 240–247 and 264–267. SM17 has six south-facing mouths. They produce small rusty tubes and running effluent at the bank, with render meshes only and the bridge envelope excluded.
- `OzAiMapPreviewCapture.Run` with `-oz-preview-comparisons -oz-preview-v5-review` captures the border, terrain and a crossing in each example. Repeat with `-oz-ai-maps-v5` to compare from identical cameras. A geometry hash records actual Unity render and collision data for every scene. Keep the resulting captures outside tracked game assets.

## V4 — surface detail, clean crossings and continuous cliffs

V4 renders all five AI Map biomes when their existing scenes load in solo, Skirmish or online matches. Existing maps require no conversion or scene rebake. To add a new map, decode its native file and bake its scene with the V3 palette enabled. Pass `-oz-ai-maps-v3` to the same compiled game or Unity preview process to compare the earlier appearance.

The Arctic profile retains its snow, slate, bridge and open-water art kit, refining the gravel and snow detail, the frost transition on banks and the depth colours of meltwater. The 18 Arctic scenes use 39,825 native water cells and 54 native bridges. The matching local Zod/Fenio palette shows blue open water and snowy edges. The water-ring tiles are ripple artwork. The six Arctic prop sprites present in these maps depict structures, firs and snow-covered rocks, and their placements are on land. There were no native floating ice props or separate traversable frozen-water surfaces to reproduce. The Arctic profile preserves these native surface classes and passes through the same bridge-clearance logic as the other V4 profiles.

### Rendering stages

1. `OzConversionTerrainV2.Build` uses the existing decoded native grid, mesh topology, palette-role paint, scene objects, biome materials, water and lighting. V3 retains responsibility for the broad colour composition and palette classification.
2. `OzAiTerrainV4.StraightRidges` marks uninterrupted runs of at least five non-lava impassable tiles along either native grid axis. V4 samples these runs without the V3 coordinate warp so the remaining rock escarpments read as continuous. `OzJungleTerracesV4` takes precedence on jungle terrain with opposing cliff faces. Directional palette roles were checked against the official `L16_environment.fbx` collision mesh. At native row 47, the centre road is at about +1.2 m, while the left and right cuts reach about −4 m and −2.5 m. The converter looks for facing tiles over two to eleven native ground or water cells, reconstructs a recessed floor and a cliff lip, and raises an existing dry road between neighbouring cuts into a shelf. A short gap between known shelf samples is blended only along a continuous native road. Normal bridge clearance takes precedence. Adjacent visual objects keep their X/Z placements and are reseated in height where required.
3. Bridge envelopes use the native orientation and length, five tiles plus valid extensions. The protected core covers the four-tile deck and one metre beyond both edges. It fades over another 1.5 m at the sides and over one tile at either approach. Within the core, raised terrain falls below the water plane and clears the rails. Clearance takes priority over the ridge profile. Original bridge meshes, destruction stages, bank approaches and simulation geometry are retained. Molten Kombat (08), The Car Park (17) and Desert P03M01 cover lava, destroyed bridges and both orientations.
4. `OzAiTerrainV4.hlsl` adds asphalt aggregate, sand ripples, scoria, concrete pores or mossy soil detail in world space. The native road masks separate road and ground detail. Pixel-footprint filtering fades each scale at a distance. This keeps close detail independent of the resolution of the map-scale paint. The rock faces reuse the original artwork with a finer triplanar scale where supported. Native rock props retain their models and gameplay bindings.

### Compatibility and checks

- Keep the native `.map` bytes, decoded grids, tile classes, road and lava masks, objects, starting positions, territories, IDs and gameplay rules intact. Unity uses the terrain surface for selection and view height. Bridge decks and movement rules retain their existing simulation bindings.
- The shader property starts at zero on official materials, V2 and V3. V4 adds no per-map texture pack or duplicate scene.
- Headless checks cover native map loads, movement and bridge states as well as V4 bridge envelopes and ridge masks. The runtime checks are listed in [the player validation record](../docs/PLAYER_VALIDATION.md).
- The palette scan was checked against 17 jungle entries. It identifies raised road sections in The Jungle (16), both Expansion 31 layouts, Jungle 1 and Jungle1vs1. Other matching layouts can contain recessed ground without a raised road section.
- Catalogue videos and posters use `tools/Refresh-AiMapPreviews.ps1`. The tool renders the same enabled scenes as the game. It stages and decodes the new clips before updating the existing resource paths used by solo, the online lobby and local Skirmish. It records map-specific framing and cuts, avoids full-map views, and keeps the simulation paused. `-ComparisonsOnly` captures one 4K view per biome with matching camera coordinates for V3 and V4. Inspect both sides and approaches of each visible bridge in the resulting live captures.

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
