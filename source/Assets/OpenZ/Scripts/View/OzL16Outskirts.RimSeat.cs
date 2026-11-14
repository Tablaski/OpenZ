// OpenZ (GPL-3.0) - Level 16, round 5e's fixes at the map's edge (the WIN ending showed a row of leaf crowns hanging in the sky over the
// north cliff top; the start flyover showed tall thin trees on the east crest that went away in the middle of its first shot):
//  - SEATS: some of the map's own small plants on its north cliff tops (ferns, bushes, bananas of the plant cells along the north line, a
//    few by the east line) stand up to 6 m over the ground under them: they were set on a ground that is no longer there. Seen from above
//    they read as plants on the cliff top; the old rim jungle stood behind them in a low shot, so their bare undersides did not show.
//    With the old rim trees gone they hang in the sky of the WIN ending. Each is seated on what is under its stem (the level's drawn
//    ground, a rock or a ruin; measured off the engine, every row checked again here): its lowest point a little into the surface; its
//    run of points in its cell's joined far mesh moves down with it (every other point in place, proven off the engine), so the far
//    view shows it where the near one does. A drawn-only change: no rule reads these plants (they are not trees of the rules).
//  - THE GONE CELLS' RIM TREES: round 4 switches some plant cells beyond the rim off when the land is shown. Before that (the start
//    flyover) their trees within GroundMargin m outside the map stood in the low look: the old rim jungle's simplified trees, the tall
//    thin ones on the east crest, gone a few seconds into the shot when the land came. They are held off from the level's first frame
//    (as round 5e holds off the other old rim trees), so the flyover shows from its first frame what it shows after the land comes.
//  - THE GROVES' FEET: a new grove plant's foot RimFootIn under the lowest land round its stem (the Play check's own ring of points),
//    never more than FootMax under the land at its middle; a small plant that would hang over a dip is not placed.
// Switch: RimSeatOn (default true; read when the level starts; round 5e off: none of it). Play check: return
// OpenZ.OzL16Outskirts.RimSeatCheck(); (also the SEATS part of RimFixCheck).
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>Round 5e's fixes: the map plants that stand in the air seated, the gone cells' rim trees held off before the land is
        /// shown. Set before the level starts.</summary>
        public static bool RimSeatOn = true;
        /// <summary>Counters: map plants seated, cells whose far mesh moved their runs, rows refused, old rim trees of the gone cells held
        /// off before the land is shown, edge plants that are full models under the cap already (they need no stand-in).</summary>
        public static int RimSeated, RimSeatCells, RimSeatRefused, RimGoneHeld, RimEdgeFine;
        public static string RimSeatWhy = "", RimEdgeFineNames = "";
        /// <summary>A new grove plant's foot this far under the lowest land round its stem (m).</summary>
        public const float RimFootIn = 0.02f;
        /// <summary>The AIR test: a plant whose foot stands within AirBand m inside the map line and whose lowest point stands more than
        /// AirOver m over the level's ground collider round its stem (the highest of it at the stem and on a ring AirRing m out: a plant on
        /// a cliff's edge stands on the edge, not over the cliff's foot) is in the air, unless it is one of the seats or the data knows what
        /// holds it (RimAirHeld: a rock, a ruin or the rim's drawn turf, none of them in the collider).</summary>
        public const float AirBand = 15f, AirOver = 1f, AirRing = 0.5f;

        sealed class RimSeatRow { public string cell, plant; public float y0, y1; public int points, triangles, start, count; }
        sealed class RimSeatDone { public RimSeatRow row; public Transform plant; public MeshFilter far; public Mesh was; public Vector3 dLocal; }

        readonly List<RimSeatDone> rimSeats = new List<RimSeatDone>();
        readonly List<(MeshFilter mf, Mesh was, Mesh made)> rimSeatMeshes = new List<(MeshFilter, Mesh, Mesh)>();
        readonly List<Renderer> rimGone = new List<Renderer>();
        readonly List<Transform> rimEdgeFine = new List<Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRimSeatStatics()
        {   // every Play session: the switch on, the counters at 0
            RimSeatOn = true; RimSeated = RimSeatCells = RimSeatRefused = RimGoneHeld = RimEdgeFine = 0; RimSeatWhy = RimEdgeFineNames = "";
        }

        /// <summary>RimEarly, first: this level's counters and lists.</summary>
        void RimSeatReset()
        {
            RimSeated = RimSeatCells = RimSeatRefused = RimGoneHeld = 0; RimSeatWhy = "";
            rimSeats.Clear(); rimSeatMeshes.Clear(); rimGone.Clear();
        }

        /// <summary>RimEdgeReset: the edge plants that need no stand-in, counted again for this level.</summary>
        void RimEdgeFineReset() { RimEdgeFine = 0; RimEdgeFineNames = ""; rimEdgeFine.Clear(); }

        /// <summary>RimEdgeSwap: an edge plant that is a full model under the cap already is drawn as it is (it needs no stand-in).</summary>
        void RimEdgeFineAdd(Transform t)
        {
            RimEdgeFine++; rimEdgeFine.Add(t);
            if (RimEdgeFineNames.Length < 300) RimEdgeFineNames += (RimEdgeFineNames.Length > 0 ? ", " : "") + t.name;
        }

        /// <summary>RimEdgeCheck: the edge plants that need no stand-in, each drawn as it is, a full model (no '_lite' or far shape), its
        /// true crown at most RimCap. Returns how many are not; why: the first of them.</summary>
        int RimEdgeFineBad(out string why)
        {
            int bad = 0; why = "";
            foreach (var t in rimEdgeFine)
            {
                var r = t ? t.GetComponent<Renderer>() : null; var mf = t ? t.GetComponent<MeshFilter>() : null;
                bool fine = r && mf && mf.sharedMesh && RimDrawn(r) && FarKindOf(mf.sharedMesh) < 0 && !mf.sharedMesh.name.Contains("_lite") && TrueWidth(t, mf.sharedMesh) <= RimCap + 0.005f;
                if (!fine) { bad++; if (why.Length == 0) why = t ? t.name : "a plant that is gone"; }
            }
            return bad;
        }

        void RefuseSeat(string why) { RimSeatRefused++; if (RimSeatWhy.Length < 600) RimSeatWhy += (RimSeatWhy.Length > 0 ? "; " : "") + why; }

        // ------------------------------------------------------------------ the gone cells' rim trees (RimEarly)
        /// <summary>The plants of the cells round 4 switches off with the land that a low shot shows before that: within GroundMargin m
        /// outside the map (the low look's own rule: OzL16FlyoverFlora hides the farther ones). Held off (Renderer.forceRenderingOff; when
        /// the land is shown round 4 switches them off for good).</summary>
        void RimGoneHold(HashSet<Transform> gone)
        {
            if (!RimSeatOn || !RimThinOn) return;
            float m = OzL16FlyoverFlora.GroundMargin;
            foreach (var cell in gone)
            {
                if (!cell) continue;
                foreach (var r in cell.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r || r.name == "FAR" || r.forceRenderingOff) continue;
                    float o = Outside(r.bounds.center); if (o <= 0f || o > m) continue;
                    r.forceRenderingOff = true; rimGone.Add(r); RimGoneHeld++;
                }
            }
        }

        // ------------------------------------------------------------------ the seats (RimEarly)
        /// <summary>Each row checked against the level (its cell's far mesh the one the data knows, the plant where the data has it, its run
        /// its own: every point on its own shape's box, widened 0.75 m; its new foot not deep under the level's ground collider): the
        /// plant moves down to its seat, its run with it in a copy of the cell's far mesh. A cell with a row that does not check out is
        /// left exactly as it is.</summary>
        void RimSeatMake(GameObject root)
        {
            if (!RimSeatOn) return;
            var byCell = new Dictionary<string, List<RimSeatRow>>(); var order = new List<string>();
            foreach (var row in RimSeatRows)
            {
                if (!byCell.TryGetValue(row.cell, out var l)) { byCell[row.cell] = l = new List<RimSeatRow>(); order.Add(row.cell); }
                l.Add(row);
            }
            foreach (var cellName in order)
            {
                var rows = byCell[cellName];
                var cellT = root.transform.Find(cellName); if (!cellT) { RefuseSeat(cellName + " is not in the level"); continue; }
                var farT = cellT.Find("FAR"); var mf = farT ? farT.GetComponent<MeshFilter>() : null; var src = mf ? mf.sharedMesh : null;
                if (!src || !src.isReadable || src.subMeshCount != 1 || src.vertexCount != rows[0].points || Tris(src) != rows[0].triangles) { RefuseSeat(cellName + ": its far mesh is not the one the data knows"); continue; }
                var sv = new List<Vector3>(); src.GetVertices(sv);
                var plants = new List<Transform>(); string why = null;
                foreach (var row in rows)
                {
                    var t = FindDeep(cellT, row.plant);
                    var pmf = t ? t.GetComponent<MeshFilter>() : null;
                    if (!t || !pmf || !pmf.sharedMesh || plants.Contains(t)) { why = row.plant + " is not in its cell"; break; }
                    if (Mathf.Abs(t.position.y - row.y0) > 0.005f) { why = row.plant + $" stands at {t.position.y:0.000} m, the data has {row.y0:0.000} m"; break; }
                    if (row.points != sv.Count || row.start < 0 || row.count <= 0 || row.start + row.count > sv.Count) { why = row.plant + ": its run is not in the far mesh"; break; }
                    var box = WorldBox(pmf.sharedMesh.bounds, t.localToWorldMatrix); box.Expand(1.5f);
                    for (int j = row.start; j < row.start + row.count && why == null; j++) if (!box.Contains(farT.TransformPoint(sv[j]))) why = row.plant + ": its far run is not its own";
                    if (why != null) break;
                    if (GroundAt(t.position.x, t.position.z, out float g) && row.y1 < g - 0.5f) { why = row.plant + ": its seat would lie deep under the level's ground"; break; }
                    plants.Add(t);
                }
                if (why != null) { RefuseSeat(why); continue; }
                var made = Instantiate(src); made.name = src.name + " (seats)";
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i]; var t = plants[i]; float dy = row.y1 - row.y0;
                    var d = farT.InverseTransformVector(new Vector3(0f, dy, 0f));
                    for (int j = row.start; j < row.start + row.count; j++) sv[j] += d;
                    var p = t.position; t.position = new Vector3(p.x, row.y1, p.z);
                    rimSeats.Add(new RimSeatDone { row = row, plant = t, far = mf, was = src, dLocal = d }); RimSeated++;
                }
                made.SetVertices(sv); made.RecalculateBounds(); made.UploadMeshData(false);                // readable: round 5e's own cut of an edge plant's run reads it (RimEdgeFarCut)
                mf.sharedMesh = made; rimSeatMeshes.Add((mf, src, made)); RimSeatCells++;
            }
        }

        /// <summary>RimUndoAll: the seats and the held trees as the level had them.</summary>
        void RimSeatUndo()
        {
            foreach (var s in rimSeats) if (s.plant) { var p = s.plant.position; s.plant.position = new Vector3(p.x, s.row.y0, p.z); }
            foreach (var (mf, was, made) in rimSeatMeshes) { if (mf && mf.sharedMesh == made) mf.sharedMesh = was; if (made) Destroy(made); }
            foreach (var r in rimGone) if (r) r.forceRenderingOff = false;
            rimSeats.Clear(); rimSeatMeshes.Clear(); rimGone.Clear(); RimSeated = RimSeatCells = RimGoneHeld = 0;
        }

        // ------------------------------------------------------------------ the groves' feet (GroveUnder)
        /// <summary>A grove plant's foot at (p.x, p.y): RimFootIn under the lowest land round its stem (Under, 64 points: the Play check's
        /// own ring), never more than FootMax under the land at its middle. A tree keeps round 5's own place where that would sink it
        /// deeper (round 5's place stands on the land: TreeCheck); a small plant that would hang over a dip is not placed (false).</summary>
        bool RimGroveFoot(Vector2 p, ref float y, bool tree)
        {
            float h = Gen(p.x, p.y), lo = Under(p.x, p.y, 64), want = Mathf.Min(y, lo - RimFootIn);
            if (h - want <= FootMax) { y = want; return true; }
            if (tree) { y = Mathf.Min(y, Mathf.Max(want, h - FootMax)); return true; }
            if (h - FootMax > lo) return false;
            y = h - FootMax; return true;
        }

        // ------------------------------------------------------------------ status and the Play check
        static string RimSeatStatus() => !RimSeatOn ? ", seats off" :
            $", seats {RimSeated} of {RimSeatRows.Length} map plants on the ground under them (far meshes {RimSeatCells}, refused {RimSeatRefused}), old rim trees of the gone cells held off {RimGoneHeld}";

        /// <summary>Play check, at any moment (also in the start flyover and the endings): every seat in its place (at its data's height,
        /// drawn, its run moved in its cell's far mesh as it moved), no row refused; the AIR test over every plant drawn within AirBand m
        /// inside the map line; the gone cells' rim trees not drawn.</summary>
        public static string RimSeatCheck()
        {
            var o = I; if (!o) return "OzL16Outskirts: not on this level";
            if (!RimFixOn || !TreesOn || !FarLookOn || !RimSeatOn) return "SEATS: off";
            return o.RimSeatCheck(out _);
        }

        string RimSeatCheck(out bool ok)
        {
            ok = true;
            if (!RimSeatOn) return "SEATS: off (RimSeatOn false)";
            var root = GameObject.Find(FloraRoot); if (!root) { ok = false; return "SEATS: no " + FloraRoot; }
            int placed = 0, drawn = 0, runOk = 0; string bad = "";
            var cur = new Dictionary<Mesh, List<Vector3>>(); var was = new Dictionary<Mesh, List<Vector3>>();
            List<Vector3> Points(Dictionary<Mesh, List<Vector3>> d, Mesh m)
            {
                if (!m || !m.isReadable) return null;
                if (!d.TryGetValue(m, out var l)) { l = new List<Vector3>(); m.GetVertices(l); d[m] = l; }
                return l;
            }
            var seatSet = new HashSet<Transform>();
            foreach (var s in rimSeats)
            {
                if (!s.plant) { if (bad.Length == 0) bad = s.row.plant + " is gone"; continue; }
                seatSet.Add(s.plant);
                if (Mathf.Abs(s.plant.position.y - s.row.y1) <= 0.001f) placed++; else if (bad.Length == 0) bad = s.row.plant + " is not on its seat";
                bool on = RimDrawn(s.plant.GetComponent<Renderer>()) || (rimEdgeBy.TryGetValue(s.plant, out var ep) && ep.modelR && RimDrawn(ep.modelR));   // an edge plant: its stand-in draws it (also before the land is shown)
                if (on) drawn++; else if (bad.Length == 0) bad = s.row.plant + " is not drawn";
                var a = Points(cur, s.far ? s.far.sharedMesh : null); var b = Points(was, s.was); bool moved = a != null && b != null && a.Count == b.Count;
                for (int j = s.row.start; moved && j < s.row.start + s.row.count; j++) if ((a[j] - b[j] - s.dLocal).sqrMagnitude > 1e-6f) moved = false;
                if (moved) runOk++; else if (bad.Length == 0) bad = s.row.plant + ": its far run did not move with it";
            }
            // the AIR test: every plant drawn now with its foot within AirBand m inside the map line (no water plant, hanging vine, log,
            // rock or mound, no simplified tree; the edge plants' stand-ins: the EDGE part)
            var held = new HashSet<string>(RimAirHeld); int looked = 0, seatHigh = 0, heldN = 0, air = 0; string airName = "";
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (!mf || !mf.sharedMesh || mf.name == "FAR") continue;
                var r = mf.GetComponent<Renderer>(); if (!RimDrawn(r)) continue;
                var t = mf.transform; var p = t.position;
                if (Outside(p) > 0f) continue;
                float din = Mathf.Min(Mathf.Min(p.x, map.x - p.x), Mathf.Min(-p.z, map.y + p.z)); if (din > AirBand) continue;
                string kind = RimBaseName(mf.sharedMesh);
                if (System.Array.IndexOf(RimNoCrown, kind) >= 0 || kind == "FL_VineCurtainA" || kind.StartsWith("FL_Reeds", System.StringComparison.Ordinal) || kind.Contains("_lite")) continue;
                if (rimEdgeBy.ContainsKey(t) || (t.parent && rimEdgeBy.ContainsKey(t.parent)) || OzTreeFallDown(t)) continue;
                looked++;
                if (!AirGround(p.x, p.z, out float g)) continue;
                float low = WorldBox(mf.sharedMesh.bounds, t.localToWorldMatrix).min.y;
                if (low - g <= AirOver) continue;
                if (seatSet.Contains(t)) { seatHigh++; continue; }                                         // seated on a rock, a ruin or the rim's drawn turf
                if (held.Contains(t.name)) { heldN++; continue; }
                air++; if (airName.Length < 200) airName += (airName.Length > 0 ? ", " : "") + $"{t.name} +{low - g:0.0} m";
            }
            int goneOn = 0; foreach (var r in rimGone) if (RimDrawn(r)) goneOn++;
            if (RimSeatRefused > 0 || placed != RimSeatRows.Length || drawn != RimSeatRows.Length || runOk != RimSeatRows.Length || air > 0 || goneOn > 0) ok = false;
            return $"SEATS (the map's plants that stood in the air): {(ok ? "PASS" : "FAIL")} | seated {placed} of {RimSeatRows.Length} (drawn {drawn}, far runs moved with them {runOk}, cells {RimSeatCells}), refused {RimSeatRefused}{(RimSeatWhy.Length > 0 ? " (" + RimSeatWhy + ")" : "")}{(bad.Length > 0 ? " (" + bad + ")" : "")}" +
                   $" | AIR (plants within {AirBand:0} m inside the line, lowest point more than {AirOver:0.0} m over the level's ground collider, its highest within {AirRing:0.0} m of the stem): {air} in the air{(air > 0 ? " (" + airName + ")" : "")} of {looked} looked at; on a rock, a ruin or the rim's drawn turf the collider leaves out: seats {seatHigh}, known {heldN} of {RimAirHeld.Length}" +
                   $" | old rim trees of the gone cells held off {RimGoneHeld}, drawn {goneOn}";
        }

        /// <summary>The AIR test's ground under a stem: the highest hit of the level's ground collider at (x, z) and on 8 points AirRing m
        /// round it (a single ray at a cliff's edge finds the cliff's foot under a plant that stands on the edge).</summary>
        static bool AirGround(float x, float z, out float g)
        {
            bool any = GroundAt(x, z, out g);
            for (int k = 0; k < 8; k++)
            {
                float a = k * 0.25f * Mathf.PI;
                if (GroundAt(x + Mathf.Cos(a) * AirRing, z + Mathf.Sin(a) * AirRing, out float h) && (!any || h > g)) { g = h; any = true; }
            }
            return any;
        }

        /// <summary>The seats, made off the engine (the level's drawn ground, collider, rocks and ruins under each stem; each plant's run of
        /// points in its cell's far mesh found and proven with round 5b's own method): cell, plant, its height now and on its seat (m,
        /// world), its cell's far mesh (points, triangles), its run (first point, points).</summary>
        static readonly RimSeatRow[] RimSeatRows =
        {
            new RimSeatRow { cell = "P_0_-1", plant = "PLT_BananaPlantA_0025", y0 = 6.5785f, y1 = 5.7882f, points = 8589, triangles = 5187, start = 671, count = 671 },
            new RimSeatRow { cell = "P_0_-1", plant = "PLT_BananaPlantA_0038", y0 = 6.8061f, y1 = 6.2296f, points = 8589, triangles = 5187, start = 2013, count = 671 },
            new RimSeatRow { cell = "P_0_-1", plant = "PLT_BushBroadleafA_0039", y0 = 6.8989f, y1 = 6.3434f, points = 8589, triangles = 5187, start = 4494, count = 156 },
            new RimSeatRow { cell = "P_0_-1", plant = "PLT_BushBroadleafA_0045", y0 = 6.5680f, y1 = 5.8718f, points = 8589, triangles = 5187, start = 5274, count = 156 },
            new RimSeatRow { cell = "P_0_-1", plant = "PLT_BushBroadleafA_0060", y0 = 6.9399f, y1 = 5.9503f, points = 8589, triangles = 5187, start = 5898, count = 156 },
            new RimSeatRow { cell = "P_0_-1", plant = "PLT_FernClusterA_0074", y0 = 8.3802f, y1 = 5.3796f, points = 8589, triangles = 5187, start = 7770, count = 117 },
            new RimSeatRow { cell = "P_1_-1", plant = "PLT_BushBroadleafA_0053", y0 = 8.3938f, y1 = 5.4061f, points = 5375, triangles = 3168, start = 2684, count = 156 },
            new RimSeatRow { cell = "P_1_-1", plant = "PLT_FernClusterA_0070", y0 = 8.3087f, y1 = 5.2156f, points = 5375, triangles = 3168, start = 2957, count = 117 },
            new RimSeatRow { cell = "P_1_-1", plant = "PLT_FernClusterA_0076", y0 = 8.1880f, y1 = 5.7529f, points = 5375, triangles = 3168, start = 3074, count = 117 },
            new RimSeatRow { cell = "P_1_-1", plant = "PLT_WallFall_f5_FernClus", y0 = 8.6959f, y1 = 5.7087f, points = 5375, triangles = 3168, start = 4673, count = 117 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BananaPlantA_0021", y0 = 9.5047f, y1 = 6.0309f, points = 8444, triangles = 5470, start = 0, count = 671 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BananaPlantA_0027", y0 = 8.4625f, y1 = 6.1453f, points = 8444, triangles = 5470, start = 671, count = 671 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BananaPlantA_0034", y0 = 10.0168f, y1 = 5.1486f, points = 8444, triangles = 5470, start = 2013, count = 671 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BananaPlantA_0037", y0 = 9.5121f, y1 = 4.9884f, points = 8444, triangles = 5470, start = 2684, count = 671 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BushBroadleafA_0034", y0 = 8.2048f, y1 = 5.9713f, points = 8444, triangles = 5470, start = 4338, count = 156 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BushBroadleafA_0035", y0 = 8.8824f, y1 = 6.2605f, points = 8444, triangles = 5470, start = 4494, count = 156 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BushBroadleafA_0036", y0 = 8.8655f, y1 = 5.2167f, points = 8444, triangles = 5470, start = 4650, count = 156 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BushBroadleafA_0046", y0 = 8.0277f, y1 = 7.0535f, points = 8444, triangles = 5470, start = 4806, count = 156 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BushBroadleafA_0047", y0 = 9.6682f, y1 = 5.2800f, points = 8444, triangles = 5470, start = 4962, count = 156 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_BushBroadleafA_0048", y0 = 10.1706f, y1 = 4.7058f, points = 8444, triangles = 5470, start = 5118, count = 156 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_FernClusterA_0055", y0 = 8.4953f, y1 = 6.1280f, points = 8444, triangles = 5470, start = 5820, count = 117 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_FernClusterA_0056", y0 = 8.1078f, y1 = 5.9625f, points = 8444, triangles = 5470, start = 5937, count = 117 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_FernClusterA_0057", y0 = 9.8535f, y1 = 4.9982f, points = 8444, triangles = 5470, start = 6054, count = 117 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_FernClusterA_0058", y0 = 10.1146f, y1 = 4.7429f, points = 8444, triangles = 5470, start = 6171, count = 117 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_FernClusterA_0067", y0 = 9.3953f, y1 = 5.4287f, points = 8444, triangles = 5470, start = 6288, count = 117 },
            new RimSeatRow { cell = "P_2_-1", plant = "PLT_FernClusterA_0068", y0 = 10.0894f, y1 = 4.9675f, points = 8444, triangles = 5470, start = 6405, count = 117 },
            new RimSeatRow { cell = "P_3_-1", plant = "PLT_BananaPlantA_0022", y0 = 10.2918f, y1 = 4.2872f, points = 1841, triangles = 1127, start = 0, count = 671 },
            new RimSeatRow { cell = "P_3_-1", plant = "PLT_BushBroadleafA_0037", y0 = 7.1052f, y1 = 5.2217f, points = 1841, triangles = 1127, start = 671, count = 156 },
            new RimSeatRow { cell = "P_3_-1", plant = "PLT_BushBroadleafA_0050", y0 = 8.7648f, y1 = 5.1100f, points = 1841, triangles = 1127, start = 983, count = 156 },
            new RimSeatRow { cell = "P_3_-1", plant = "PLT_FernClusterA_0059", y0 = 10.0844f, y1 = 4.2787f, points = 1841, triangles = 1127, start = 1373, count = 117 },
            new RimSeatRow { cell = "P_3_-1", plant = "PLT_FernClusterA_0060", y0 = 9.6166f, y1 = 4.2630f, points = 1841, triangles = 1127, start = 1490, count = 117 },
            new RimSeatRow { cell = "P_3_-1", plant = "PLT_FernClusterA_0061", y0 = 5.4556f, y1 = 4.9444f, points = 1841, triangles = 1127, start = 1607, count = 117 },
            new RimSeatRow { cell = "P_3_-1", plant = "PLT_FernClusterA_0075", y0 = 9.5260f, y1 = 5.1172f, points = 1841, triangles = 1127, start = 1724, count = 117 },
            new RimSeatRow { cell = "P_4_-1", plant = "PLT_BananaPlantA_0028", y0 = 8.9126f, y1 = 2.8638f, points = 4992, triangles = 3381, start = 0, count = 671 },
            new RimSeatRow { cell = "P_4_-1", plant = "PLT_BananaPlantA_0029", y0 = 5.9123f, y1 = 1.0420f, points = 4992, triangles = 3381, start = 671, count = 671 },
            new RimSeatRow { cell = "P_4_-1", plant = "PLT_BushBroadleafA_0054", y0 = 4.2977f, y1 = 1.1781f, points = 4992, triangles = 3381, start = 1498, count = 156 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_BushBroadleafA_0038", y0 = 4.2267f, y1 = 2.0609f, points = 3399, triangles = 2197, start = 671, count = 156 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_BushBroadleafA_0051", y0 = 6.3114f, y1 = 3.5882f, points = 3399, triangles = 2197, start = 827, count = 156 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_BushBroadleafA_0055", y0 = 6.2891f, y1 = 3.6039f, points = 3399, triangles = 2197, start = 983, count = 156 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_FernClusterA_0062", y0 = 4.8498f, y1 = 1.0974f, points = 3399, triangles = 2197, start = 1256, count = 117 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_FernClusterA_0063", y0 = 6.4358f, y1 = 3.6760f, points = 3399, triangles = 2197, start = 1373, count = 117 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_FernClusterA_0073", y0 = 6.3776f, y1 = 3.4246f, points = 3399, triangles = 2197, start = 1490, count = 117 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_WallFall_f6_FernClus", y0 = 3.9000f, y1 = 2.4710f, points = 3399, triangles = 2197, start = 2613, count = 117 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_WallFall_f6_FernClus.001", y0 = 4.6223f, y1 = 3.4050f, points = 3399, triangles = 2197, start = 2730, count = 117 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_WallFall_f6_FernClus.002", y0 = 3.3495f, y1 = 2.3494f, points = 3399, triangles = 2197, start = 2847, count = 117 },
            new RimSeatRow { cell = "P_5_-1", plant = "PLT_WallFall_f6_FernClus.003", y0 = 4.6030f, y1 = 3.1975f, points = 3399, triangles = 2197, start = 2964, count = 117 },
            new RimSeatRow { cell = "P_6_-1", plant = "PLT_BananaPlantA_0030", y0 = 8.8708f, y1 = 3.7276f, points = 2208, triangles = 1397, start = 0, count = 671 },
            new RimSeatRow { cell = "P_6_-1", plant = "PLT_BushBroadleafA_0042", y0 = 7.6052f, y1 = 3.8250f, points = 2208, triangles = 1397, start = 671, count = 156 },
            new RimSeatRow { cell = "P_6_-1", plant = "PLT_BushBroadleafA_0056", y0 = 7.1298f, y1 = 3.8065f, points = 2208, triangles = 1397, start = 827, count = 156 },
            new RimSeatRow { cell = "P_6_-1", plant = "PLT_BushBroadleafA_0062", y0 = 8.6109f, y1 = 3.6257f, points = 2208, triangles = 1397, start = 983, count = 156 },
            new RimSeatRow { cell = "P_6_-1", plant = "PLT_BushBroadleafA_0063", y0 = 8.6830f, y1 = 3.6690f, points = 2208, triangles = 1397, start = 1139, count = 156 },
            new RimSeatRow { cell = "P_6_-1", plant = "PLT_FernClusterA_0077", y0 = 7.6936f, y1 = 3.9081f, points = 2208, triangles = 1397, start = 1295, count = 117 },
            new RimSeatRow { cell = "P_6_-1", plant = "PLT_FernClusterA_0081", y0 = 8.6795f, y1 = 3.7520f, points = 2208, triangles = 1397, start = 1412, count = 117 },
            new RimSeatRow { cell = "P_6_-2", plant = "PLT_FernClusterA_0108", y0 = 3.8237f, y1 = 1.7380f, points = 773, triangles = 547, start = 312, count = 117 },
            new RimSeatRow { cell = "P_6_-5", plant = "PLT_BushBroadleafA_0115", y0 = 1.1402f, y1 = 0.1087f, points = 4957, triangles = 2757, start = 4026, count = 156 },
        };

        /// <summary>The plants within AirBand m inside the map line whose lowest point stands more than 0.8 m over the level's ground
        /// collider (its highest within AirRing m of the stem) because a rock, a ruin or the rim's drawn turf holds them (off the engine:
        /// each within 0.25 m of what holds it).</summary>
        static readonly string[] RimAirHeld =
        {
            "PLT_FernClusterA_0072",
            "PLT_FernClusterA_0071",
            "PLT_BushBroadleafA_0082",
            "PLT_FernClusterA_0194",
            "PLT_BananaPlantA_0086",
            "PLT_FernClusterA_0143",
            "PLT_FernClusterA_0145",
            "PLT_FernClusterA_0216",
            "PLT_BananaPlantA_0033",
        };
    }
}
