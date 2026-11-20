const fs = require('node:fs');
const path = require('node:path');
const { decodeMultiplayer } = require('./Convert-AiMaps.cjs');

const root = process.argv[2];
if (!root) throw new Error('Usage: node tools/Check-MultiplayerMaps.cjs <Zod Engine directory>');

const worlds = ['desert', 'volcanic', 'arctic', 'jungle', 'city'];
const capacities = [3, 4, 6, 8];
const counts = new Map(capacities.map((capacity) => [capacity, 0]));
let issues = 0;

for (const file of fs.readdirSync(root).sort()) {
    const match = /^p0([3468]).*\.map$/i.exec(file);
    if (!match) continue;
    const capacity = Number(match[1]);
    counts.set(capacity, counts.get(capacity) + 1);
    try {
        const bytes = fs.readFileSync(path.join(root, file));
        const world = worlds[bytes[58]];
        if (!world) throw new Error('Unknown terrain palette');
        const palette = fs.readFileSync(path.join(root, 'assets', 'planets', `${world}.tileinfo`));
        const map = decodeMultiplayer(bytes, file, palette);
        const forts = map.objects.filter((object) => object.kind.startsWith('building:fort'));
        const startingOwners = new Set(forts.map((fort) => fort.owner));
        const diagnostics = [];
        if (forts.length !== capacity || startingOwners.size !== capacity || startingOwners.has(0)) {
            diagnostics.push(`expected ${capacity} distinct owned forts, found ${forts.length} (owners: ${[...startingOwners].join(', ')})`);
        }
        for (const object of map.objects) {
            if (object.owner !== 0 && !startingOwners.has(object.owner)) {
                diagnostics.push(`${object.kind} at ${object.x},${object.y} belongs to owner ${object.owner} without a fort`);
            }
        }
        console.log(`${file}: ${map.width}×${map.height}, ${world}, ${forts.length} forts${diagnostics.length ? ' · REVIEW' : ''}`);
        for (const diagnostic of diagnostics) console.log(`  ${diagnostic}`);
        if (diagnostics.length) issues++;
    } catch (error) {
        console.error(`${file}: ${error.message}`);
        issues++;
    }
}

for (const [capacity, count] of counts) {
    if (count === 0) {
        console.error(`No P0${capacity} map files found`);
        issues++;
    }
}
console.log(`Inventory: ${[...counts].map(([capacity, count]) => `P0${capacity} ${count}`).join(', ')}. ${issues} entries require review.`);
process.exitCode = issues ? 1 : 0;
