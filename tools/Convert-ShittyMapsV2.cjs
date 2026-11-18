// OpenZ-VK (GPL-3.0). Keep native gameplay data and add biome-specific rendering semantics.
const fs = require('node:fs');
const path = require('node:path');
const { decode, sha } = require('./Convert-ShittyMaps.cjs');

const packageRoot = path.resolve(__dirname, '../shittymaps');
const palettes = ['desert', 'volcanic', 'arctic', 'jungle', 'city'];
const supported = new Set(['desert', 'volcanic', 'jungle', 'city']);

// Static lava IDs reconciled against L07_grid and the Zod editor palette.
const crust = new Set([5, 6, 25, 26, 46, 66, 86, 97, 98, 99, 106, 119, 141, 142, 160, 161, 162, 181, 182]);
// Zod's palette links these frames through next_tile_in_effect. Include the
// start frames and phases used by other native volcanic layouts.
const hot = new Set([376, 377, 378, 396, 397, 398, 416, 417, 418, 436, 437, 438]);

function convert(bytes, filename, palette) {
    const map = decode(bytes, filename, palette);
    if (!supported.has(map.planet)) throw Error('No V2 asset mapping for ' + map.planet);
    if (map.planet !== 'volcanic') return map;
    map.lava = map.tiles.map((id, index) => {
        if (map.classes[index] !== 3) return 0;
        return hot.has(id) ? 2 : crust.has(id) ? 1 : 0;
    });
    // An empty lava plane would still activate the OpenZ lava subsystem.
    if (!map.lava.some(Boolean)) delete map.lava;
    return map;
}

function run(source, id) {
    if (!source || !/^SM[A-Za-z0-9_]+V2$/.test(id ?? ''))
        throw Error('Usage: node tools/Convert-ShittyMapsV2.cjs <native.map> <SM...V2>');
    const filename = path.basename(source);
    const bytes = fs.readFileSync(source);
    const world = palettes[bytes[58]];
    if (!supported.has(world)) throw Error('No V2 asset mapping for ' + (world ?? filename));
    const palette = fs.readFileSync(path.join(packageRoot, 'Sources~/palettes', world + '.tileinfo'));
    const map = convert(bytes, filename, palette);
    const grid = path.join(packageRoot, 'Resources/OpenZ/Maps', id + '_grid.json');
    const native = path.join(packageRoot, 'Sources~/maps', filename);
    if (fs.existsSync(native) && sha(fs.readFileSync(native)) !== sha(bytes))
        throw Error('A different source map already uses ' + filename);
    fs.writeFileSync(native, bytes);
    fs.writeFileSync(grid, JSON.stringify(map) + '\n');
    console.log(`${id}: ${map.width} x ${map.height}, ${map.objects.length} objects, ${map.lava?.filter(Boolean).length ?? 0} lava tiles, SHA-256 ${sha(bytes)}`);
}

module.exports = { convert };
if (require.main === module) run(process.argv[2], process.argv[3]);
