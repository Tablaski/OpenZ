const fs = require('node:fs');
const path = require('node:path');
const { decodeMultiplayer } = require('./Convert-AiMaps.cjs');

const file = 'p03_bb_p03m01.map';
const source = process.argv[2];
if (!source) throw new Error('Usage: node tools/Convert-AiSkirmish.cjs <Zod Engine directory>');
const packageRoot = path.resolve(__dirname, '../aimaps');
const palette = fs.readFileSync(path.join(packageRoot, 'Sources~/palettes/desert.tileinfo'));
const input = fs.readFileSync(path.join(source, file));
const map = decodeMultiplayer(input, file, palette);
const owners = ['neutral', 'red', 'blue', 'green'];
const forts = map.objects.filter((object) => object.kind.startsWith('building:fort'));
if (map.planet !== 'desert' || forts.length !== 3 || ![1, 2, 3].every((owner) => forts.some((fort) => fort.owner === owner))) {
    throw new Error(`${file} does not have the expected three desert forts`);
}
for (const object of map.objects) {
    if (!owners[object.owner]) throw new Error(`Unsupported owner ${object.owner} in ${file}`);
    object.owner = owners[object.owner];
}
map.aiMap = true;
map.skirmish = true;
fs.writeFileSync(path.join(packageRoot, 'Sources~/maps', file), input);
fs.writeFileSync(path.join(packageRoot, 'Resources/OpenZ/Maps/SMP03M01_grid.json'), JSON.stringify(map) + '\n');
console.log(`${file}: ${map.width}×${map.height}, ${map.objects.length} objects, three starting forts`);
