// OpenZ (GPL-3.0) - version 1.3, level 7, lava kit LV1: everything the surface needs that is not in OzLava.cs (LV_CONTRACT.md 3, 4, 5, 6). The fields, the 0.5 m drawn-lava grid every
// Asks, the border sheets, the lava clock, the ramp. View only: reads the map, writes no sim state, uses no random. Runs only where OzLava boots, a map with lava.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public partial class OzLava
    {
        /// <summary>The ramp as it must be for the game's tone curve. Scene values, not the picture's bytes: ACES turns 253 168 41 pale yellow, so the core stop that displays as the
        /// picture's core is 255 115 4 at gain 2 (proved by LV1/tools/inv_palette.py, set against captures in Play: change Gains and call OzLavaRampTable.Push).</summary>
        static readonly float[] LavaGains = { 1f, 1f, 1.2f, 1.15f, 1.25f };
        static readonly Color32 LavaCoreStop = new Color32(255, 92, 0, 255), LavaMoltenStop = new Color32(235, 38, 2, 255), LavaRedStop = new Color32(135, 18, 6, 255);   // deep red-orange, yellow only in the clumps

        const string PocketsRes = "OpenZ/Lava/L07_border_pockets";
        const int FirstBorderId = 6;
        // the drawn-lava grid, 0.5 m a cell (x -24 .., z 24 ..): map tiles + border pockets. Static so SurfaceAt asks one null test on every other level
        static sbyte[] drawn; static int gw, gh; static float gx0, gz1, gRcp; static float gLavaY;

        int[] fieldOf; int fieldCount;
        readonly Vector3[] fieldCentre = new Vector3[OzLavaIds.FieldSlots]; readonly int[] fieldTiles = new int[OzLavaIds.FieldSlots];
        readonly List<GameObject> borders = new List<GameObject>(); readonly List<Mesh> borderMeshes = new List<Mesh>();
        float clock, litePoll;

        // ------------------------------------------------------------------ what the other kits ask
        /// <summary>True where lava is DRAWN (map tiles + border sheets); y = its height. False at once with no OzLava.</summary>
        public static bool SurfaceAt(Vector3 p, out float y)
        {
            y = 0f;
            if (drawn == null || I == null) return false;                                                  // every other level stops at the first test, a plain static read
            if (!(p.x >= gx0 && p.z <= gz1 && p.x < gx0 + gw / gRcp && p.z > gz1 - gh / gRcp)) return false;   // (written so a NaN falls out too)
            int x = (int)((p.x - gx0) * gRcp), z = (int)((gz1 - p.z) * gRcp);
            if (x >= gw || z >= gh || drawn[z * gw + x] < 0) return false;
            y = gLavaY; return true;
        }

        public float Clock => clock;
        public int FieldCount => fieldCount;
        public int FieldOfTile(int tx, int ty) => map == null || fieldOf == null || tx < 0 || ty < 0 || tx >= map.width || ty >= map.height ? -1 : fieldOf[ty * map.width + tx];
        public int FieldAt(float x, float z)
        {
            if (drawn == null || !(x >= gx0 && z <= gz1 && x < gx0 + gw / gRcp && z > gz1 - gh / gRcp)) return -1;
            int cx = (int)((x - gx0) * gRcp), cz = (int)((gz1 - z) * gRcp);
            return cx >= gw || cz >= gh ? -1 : drawn[cz * gw + cx];
        }
        public Vector3 FieldCentre(int id) => id >= 0 && id < fieldCentre.Length ? fieldCentre[id] : Vector3.zero;
        public int FieldTiles(int id) => id >= 0 && id < fieldTiles.Length ? fieldTiles[id] : 0;

        // ------------------------------------------------------------------ the hooks
        partial void Lv1Boot()
        {
            OzLavaIds.Neutral();
            OzLavaRampTable.Stops[4] = LavaCoreStop; OzLavaRampTable.Stops[3] = LavaMoltenStop; OzLavaRampTable.Stops[2] = LavaRedStop;
            for (int i = 0; i < LavaGains.Length; i++) OzLavaRampTable.Gains[i] = LavaGains[i];
            OzLavaRampTable.Push();
            clock = 0f; litePoll = 0f;
            Shader.SetGlobalFloat(OzLavaIds.Lite, OzLavaFx.Lite ? 1f : 0f);
            FloodFields();
            BuildGrid();
            if (OzLavaFx.Off) return;
            BuildBorders();
        }

        partial void Lv1Tick(float dt, float t)
        {
            clock += dt;
            Shader.SetGlobalFloat(OzLavaIds.Clock, clock);
            if (t >= litePoll) { litePoll = t + 1f; Shader.SetGlobalFloat(OzLavaIds.Lite, OzLavaFx.Lite ? 1f : 0f); }
        }

        partial void Lv1Down()
        {
            foreach (var b in borders) if (b) Destroy(b);
            foreach (var m in borderMeshes) if (m) Destroy(m);
            borders.Clear(); borderMeshes.Clear(); drawn = null;
            OzLavaIds.ResetGlobals();
        }

        // ------------------------------------------------------------------ fields: 8-connected, biggest first (0-5 on this map); 6-9 the border sheets (north, east, south, west)
        void FloodFields()
        {
            int W = map.width, H = map.height; fieldOf = new int[W * H]; for (int i = 0; i < fieldOf.Length; i++) fieldOf[i] = -1;
            var found = new List<List<int>>(); var q = new Queue<int>();
            for (int s = 0; s < W * H; s++)
            {
                if (fieldOf[s] != -1 || map.lava[s] == 0) continue;
                var cells = new List<int>(); fieldOf[s] = -2; q.Enqueue(s);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(), cx = c % W, cy = c / W; cells.Add(c);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy; if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                            int ni = ny * W + nx; if (fieldOf[ni] != -1 || map.lava[ni] == 0) continue; fieldOf[ni] = -2; q.Enqueue(ni);
                        }
                }
                found.Add(cells);
            }
            found.Sort((a, b) => b.Count.CompareTo(a.Count));
            fieldCount = Mathf.Min(found.Count, FirstBorderId);
            for (int id = 0; id < found.Count; id++)
            {
                int slot = Mathf.Min(id, FirstBorderId - 1); double sx = 0, sz = 0;                       // (more fields than slots: the small ones share the last map slot)
                foreach (int c in found[id]) { fieldOf[c] = slot; sx += c % W * 2 + 1; sz += -(c / W * 2 + 1); }
                if (id < FirstBorderId) { fieldTiles[slot] = found[id].Count; fieldCentre[slot] = new Vector3((float)(sx / found[id].Count), lavaY, (float)(sz / found[id].Count)); }
            }
        }

        /// <summary>Vertex colour b of a sheet corner: the field of any lava tile round it (8-connected fields never share a corner).</summary>
        float CornerField(int cx, int cy)
        {
            for (int dy = -1; dy <= 0; dy++)
                for (int dx = -1; dx <= 0; dx++)
                {
                    int f = FieldOfTile(cx + dx, cy + dy);
                    if (f >= 0) return (f + 0.5f) / OzLavaIds.FieldSlots;
                }
            return 0.5f / OzLavaIds.FieldSlots;
        }

        // ------------------------------------------------------------------ the drawn grid: the pockets file, then the map's tiles over it
        void BuildGrid()
        {
            gx0 = -24f; gz1 = 24f; gRcp = 2f; gw = 432; gh = 536; gLavaY = lavaY;
            var ta = Resources.Load<TextAsset>(PocketsRes); byte[] bits = null;
            if (ta != null && ta.bytes.Length > 20)
            {
                var b = ta.bytes; gw = System.BitConverter.ToInt32(b, 0); gh = System.BitConverter.ToInt32(b, 4); gx0 = System.BitConverter.ToSingle(b, 8); gz1 = System.BitConverter.ToSingle(b, 12);
                gRcp = 1f / System.BitConverter.ToSingle(b, 16); bits = b;
            }
            else Debug.LogWarning("[OpenZ] lava: " + PocketsRes + " missing, no border lava");
            var g = new sbyte[gw * gh];
            for (int i = 0; i < g.Length; i++) g[i] = -1;
            float[] sum = new float[8]; int[] count = new int[4];                                         // x and z sums per border side, for FieldCentre
            if (bits != null)
                for (int z = 0; z < gh; z++)
                    for (int x = 0; x < gw; x++)
                    {
                        int k = z * gw + x;
                        if ((bits[20 + (k >> 3)] >> (k & 7) & 1) == 0) continue;
                        float wx = gx0 + (x + 0.5f) / gRcp, wz = gz1 - (z + 0.5f) / gRcp;
                        int side = SideOf(wx, wz); g[k] = (sbyte)(FirstBorderId + side);
                        sum[side * 2] += wx; sum[side * 2 + 1] += wz; count[side]++;
                    }
            for (int s = 0; s < 4; s++)
                if (count[s] > 0) { int id = FirstBorderId + s; fieldCentre[id] = new Vector3(sum[s * 2] / count[s], lavaY, sum[s * 2 + 1] / count[s]); fieldTiles[id] = count[s] / 16; }
            int cells = Mathf.RoundToInt(2f * gRcp);                                                      // a 2 m tile in grid cells
            int ox = Mathf.RoundToInt(-gx0 * gRcp), oz = Mathf.RoundToInt(gz1 * gRcp);
            for (int ty = 0; ty < map.height; ty++)
                for (int tx = 0; tx < map.width; tx++)
                {
                    int f = fieldOf[ty * map.width + tx]; if (f < 0) continue;
                    for (int z = 0; z < cells; z++)
                        for (int x = 0; x < cells; x++)
                        {
                            int px = ox + tx * cells + x, pz = oz + ty * cells + z;
                            if (px >= 0 && pz >= 0 && px < gw && pz < gh) g[pz * gw + px] = (sbyte)f;
                        }
                }
            drawn = g;
        }

        int SideOf(float x, float z)
        {
            float w = map.width * 2f, h = map.height * 2f, n = z, e = x - w, s = -h - z, a = -x;           // how far past each map edge; the largest names the side
            if (n >= e && n >= s && n >= a) return 0;
            if (e >= s && e >= a) return 1;
            return s >= a ? 2 : 3;
        }

        // ------------------------------------------------------------------ border sheets: one mesh per side on 1 m blocks, the same material, no wake. They replace the terrain lane's LAV_Border_nn
        void BuildBorders()
        {
            var mat = Resources.Load<Material>(MaterialRes); if (drawn == null || !mat) return;            // before the terrain's sheets go: never no sheet at all
            for (int i = 0; i < 32; i++) { var old = GameObject.Find("LAV_Border_" + i.ToString("00")); if (old) old.SetActive(false); }
            int step = Mathf.Max(1, Mathf.RoundToInt(gRcp));                                               // grid cells a block
            int bw = gw / step, bh = gh / step;
            var side = new sbyte[bw * bh];
            for (int bz = 0; bz < bh; bz++)
                for (int bx = 0; bx < bw; bx++)
                {
                    int best = -1, n = 0;
                    for (int z = 0; z < step; z++) for (int x = 0; x < step; x++) { int v = drawn[(bz * step + z) * gw + bx * step + x]; if (v >= FirstBorderId) { best = v; n++; } else if (v >= 0) { best = -1; n = 0; goto next; } }
                    next:
                    side[bz * bw + bx] = (sbyte)(n > 0 ? best : -1);                                       // any lava in the block: a pocket's outline must never show the pit bed
                }
            // one block more all round: the ground stands over the sheet there and hides it, as edgeGrow does on the map's sheet. Map tiles keep their own sheet
            var grown = (sbyte[])side.Clone();
            for (int bz = 0; bz < bh; bz++)
                for (int bx = 0; bx < bw; bx++)
                {
                    if (side[bz * bw + bx] >= 0 || TileLavaAt(bx, bz, step)) continue;
                    for (int dz = -1; dz <= 1 && grown[bz * bw + bx] < 0; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = bx + dx, nz = bz + dz;
                            if (nx >= 0 && nz >= 0 && nx < bw && nz < bh && side[nz * bw + nx] >= 0) { grown[bz * bw + bx] = side[nz * bw + nx]; break; }
                        }
                }
            side = grown;
            string[] names = { "LAV_Border_N", "LAV_Border_E", "LAV_Border_S", "LAV_Border_W" };
            for (int s = 0; s < 4; s++) BuildBorder(names[s], FirstBorderId + s, side, bw, bh, 1f * step / gRcp, mat);
        }

        bool TileLavaAt(int bx, int bz, int step)
        {
            for (int z = 0; z < step; z++) for (int x = 0; x < step; x++) { int v = drawn[(bz * step + z) * gw + bx * step + x]; if (v >= 0 && v < FirstBorderId) return true; }
            return false;
        }

        void BuildBorder(string name, int id, sbyte[] side, int bw, int bh, float size, Material mat)
        {
            var index = new Dictionary<int, int>(); var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            int Corner(int cx, int cy)
            {
                int key = cy * (bw + 1) + cx;
                if (index.TryGetValue(key, out int vi)) return vi;
                int inside = 0;
                for (int dy = -1; dy <= 0; dy++) for (int dx = -1; dx <= 0; dx++) { int x = cx + dx, y = cy + dy; if (x >= 0 && y >= 0 && x < bw && y < bh && side[y * bw + x] >= 0) inside++; }
                vi = verts.Count; verts.Add(new Vector3(gx0 + cx * size, lavaY, gz1 - cy * size));
                cols.Add(new Color(inside == 4 ? 1f : 0f, 0f, (id + 0.5f) / OzLavaIds.FieldSlots, 0f)); index[key] = vi; return vi;
            }
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    if (side[y * bw + x] != id) continue;
                    int v00 = Corner(x, y), v10 = Corner(x + 1, y), v01 = Corner(x, y + 1), v11 = Corner(x + 1, y + 1);
                    tris.Add(v00); tris.Add(v10); tris.Add(v11); tris.Add(v00); tris.Add(v11); tris.Add(v01);
                }
            if (tris.Count == 0) return;
            var mesh = new Mesh { name = "Oz" + name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            var n = new Vector3[verts.Count]; for (int i = 0; i < n.Length; i++) n[i] = Vector3.up; mesh.normals = n;
            mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(transform, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = true;
            borders.Add(go); borderMeshes.Add(mesh);
        }
    }
}
