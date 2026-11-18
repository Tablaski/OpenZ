// OpenZ-VK (GPL-3.0). Original scenario, authored in native Zod map units.
const fs = require('node:fs');
const path = require('node:path');
const { convert } = require('./Convert-ShittyMapsV2.cjs');

const width = 84;
const height = 96;
const name = 'Chilly Willy';
const filename = 'vk_chilly_willy.map';
const id = 'SMChillyWillyV2';
const root = path.resolve(__dirname, '../shittymaps');
const palette = fs.readFileSync(path.join(root, 'Sources~/palettes/arctic.tileinfo'));
const tilesByClass = [[0, 1, 2, 3, 4, 5], [118, 138], [154, 155], [46, 47, 50]];
const ground = 0, road = 1, water = 2, cliff = 3;

function channels(x) {
    const bend = 47 - (x - 41) * .18 + Math.sin(x * .092) * 3.5;
    const island = Math.max(0, 1 - ((x - 43) / 40) ** 2);
    return [bend - island * 12, bend + island * 11];
}

const classes = Array.from({ length: width * height }, (_, i) => {
    const x = i % width, y = Math.floor(i / width);
    const [upper, lower] = channels(x);
    const bank = 2.1 + Math.sin(x * .22 + 2) * .53;
    const rim = 3.3 + Math.cos(y * .36) + Math.sin(y * .17 + x * .41) * 1.35;
    if (x < rim || x >= width - rim || y < 3 + Math.sin(x * .22) * 1.4 || y >= height - 4 - Math.cos(x * .29) * 1.8)
        return cliff;
    return Math.abs(y - upper) <= bank || Math.abs(y - lower) <= bank ? water : ground;
});
const at = (x, y) => classes[y * width + x];
const set = (x, y, value) => { classes[y * width + x] = value; };

function trace(points) {
    for (let i = 1; i < points.length; i++) {
        const [ax, ay] = points[i - 1], [bx, by] = points[i];
        const steps = Math.ceil(Math.hypot(bx - ax, by - ay) * 2);
        for (let q = 0; q <= steps; q++) {
            const x = Math.round(ax + (bx - ax) * q / steps);
            const y = Math.round(ay + (by - ay) * q / steps);
            for (let dx = -1; dx <= 1; dx++)
            for (let dy = -1; dy <= 1; dy++)
                if (at(x + dx, y + dy) === ground) set(x + dx, y + dy, road);
        }
    }
}

const objects = [];
const occupied = new Set();
function object(x, y, owner, type, variant, level = 0, links = 0, health = 100) {
    objects.push({ x, y, owner, type, variant, level, links, health });
}

function occupy(x, y, w, h) {
    for (let dy = 0; dy < h; dy++)
    for (let dx = 0; dx < w; dx++) occupied.add((y + dy) * width + x + dx);
}

function bridge(x, channel, health) {
    const center = channels(x + 1.5)[channel];
    const north = Math.floor(center) - 4;
    const length = 10;
    object(x, north, 0, 2, 6, 1, length - 5, health);
    for (let dy = 0; dy < length; dy++) {
        set(x + 1, north + dy, road);
        set(x + 2, north + dy, road);
    }
    occupy(x, north, 4, length);
    return { x: x + 2, north, south: north + length - 1 };
}

const upper = bridge(51, 0, 100);
const west = bridge(23, 1, 100);
const east = bridge(58, 1, 0);
trace([[69, 24], [69, 28], [61, 28], [upper.x, upper.north]]);
trace([[13, 21], [29, 21], [38, 24], [61, 28]]);
trace([[upper.x, upper.south], [51, 42], [45, 47], [32, 47], [west.x, west.north]]);
trace([[45, 47], [53, 49], [east.x, east.north]]);
trace([[west.x, west.south], [23, 73], [22, 85], [15, 85]]);
trace([[east.x, east.south], [60, 74], [53, 81], [32, 81], [22, 85]]);
trace([[32, 81], [37, 87], [65, 87], [73, 78]]);

const zones = [];
const columns = [5, 27, 56, 79], rows = [5, 31, 68, 91];
for (let row = 0; row < 3; row++)
for (let col = 0; col < 3; col++)
    zones.push({ x: columns[col], y: rows[row], w: columns[col + 1] - columns[col], h: rows[row + 1] - rows[row] });

function place(x, y, w, h, owner, type, variant, tech = 0) {
    const fits = (xx, yy) => {
        if (xx < 5 || yy < 5 || xx + w >= width - 4 || yy + h >= height - 4) return false;
        for (let dy = 0; dy < h; dy++)
        for (let dx = 0; dx < w; dx++) {
            if (at(xx + dx, yy + dy) !== ground || occupied.has((yy + dy) * width + xx + dx)) return false;
        }
        return true;
    };
    for (let radius = 0; radius <= 7; radius++)
    for (let dy = -radius; dy <= radius; dy++)
    for (let dx = -radius; dx <= radius; dx++) {
        if (Math.abs(dx) + Math.abs(dy) !== radius || !fits(x + dx, y + dy)) continue;
        object(x + dx, y + dy, owner, type, variant, tech);
        occupy(x + dx, y + dy, w, h);
        return;
    }
    throw Error('No dry footprint near ' + x + ',' + y);
}

place(64, 9, 10, 11, 2, 2, 0);
place(9, 75, 10, 11, 1, 2, 1);
for (const [x, y, owner, type] of [
    [11, 12, 0, 5], [36, 12, 2, 4], [71, 25, 2, 5],
    [20, 42, 0, 4], [38, 51, 0, 2], [62, 39, 0, 3],
    [13, 68, 1, 4], [49, 73, 0, 5], [64, 76, 0, 4]
]) place(x, y, type === 3 ? 5 : 4, type === 2 ? 3 : type === 3 ? 4 : 5, owner, 2, type, 1);

for (const [x, y, owner] of [[20, 19, 0], [43, 16, 0], [18, 43, 0], [46, 43, 0], [69, 48, 0], [43, 85, 0], [73, 83, 0]])
    place(x, y, 1, 1, owner, 7, 0);
for (const [x, y, owner, type, variant] of [
    [60, 16, 2, 5, 1], [65, 23, 2, 5, 3], [76, 22, 2, 4, 1],
    [20, 75, 1, 5, 0], [10, 72, 1, 5, 3], [20, 87, 1, 4, 1],
    [36, 42, 0, 4, 0], [49, 54, 0, 4, 6], [62, 45, 0, 3, 1],
    [15, 17, 0, 7, 2], [29, 45, 0, 7, 2], [54, 48, 0, 7, 2], [39, 79, 0, 7, 2],
    [32, 42, 0, 7, 4], [47, 38, 0, 7, 4], [72, 68, 0, 7, 4]
]) place(x, y, type === 3 || type === 4 ? 2 : 1, type === 3 || type === 4 ? 2 : 1, owner, type, variant);

// Loose transverse scree, interspersed with passes along the marked roads.
const ridges = [[15, 31, 28, 26], [31, 35, 44, 32], [60, 36, 73, 33],
    [12, 60, 21, 63], [30, 57, 47, 55], [65, 59, 74, 62], [27, 71, 41, 76],
    [63, 70, 72, 74], [10, 24, 10, 34], [74, 55, 77, 70]];
for (const [ax, ay, bx, by] of ridges) {
    const span = Math.max(Math.abs(bx - ax), Math.abs(by - ay));
    for (let i = 0; i <= span; i++) {
        const x = Math.round(ax + (bx - ax) * i / span);
        const y = Math.round(ay + (by - ay) * i / span) + (i % 5 === 0 ? 1 : 0);
        if (at(x, y) !== ground || occupied.has(y * width + x)) continue;
        object(x, y - 2, 0, 7, 1);
        occupy(x, y, 1, 1);
    }
}

const bytes = Buffer.alloc(62 + zones.length * 8 + objects.length * 16 + classes.length * 2);
bytes.writeUInt16LE(width, 0);
bytes.writeUInt16LE(height, 2);
bytes.write(name, 4, 'ascii');
bytes[54] = 2;
bytes.writeUInt16LE(objects.length, 56);
bytes[58] = 2;
bytes.writeUInt16LE(zones.length, 60);
let offset = 62;
for (const { x, y, w, h } of zones) {
    for (const n of [x, y, w, h]) { bytes.writeUInt16LE(n, offset); offset += 2; }
}
for (const obj of objects) {
    bytes.writeUInt16LE(obj.x, offset);
    bytes.writeUInt16LE(obj.y, offset + 2);
    bytes[offset + 4] = obj.owner;
    bytes[offset + 5] = obj.type;
    bytes[offset + 6] = obj.variant;
    bytes[offset + 7] = obj.level;
    bytes.writeUInt16LE(obj.links, offset + 8);
    bytes.writeInt32LE(obj.health, offset + 12);
    offset += 16;
}
for (let i = 0; i < classes.length; i++, offset += 2) {
    const variants = tilesByClass[classes[i]];
    bytes.writeUInt16LE(variants[((i * 89) ^ (i >>> 2)) % variants.length], offset);
}

const decoded = convert(bytes, filename, palette);
if (decoded.classes.some((c, i) => c !== classes[i])) throw Error('Arctic palette no longer matches the scenario terrain roles');
fs.writeFileSync(path.join(root, 'Sources~/maps', filename), bytes);
fs.writeFileSync(path.join(root, 'Resources/OpenZ/Maps', id + '_grid.json'), JSON.stringify(decoded) + '\n');
console.log(`${name}: ${width} × ${height}, ${objects.length} objects, ${zones.length} territories`);
