// OpenZ (GPL-3.0) - Level 16: ground under the landmarks beyond the rim in a low shot.
// The level's landmarks beyond the rim (L16_Structures/Surround SUR_*: the far pyramid, the idols, the causeway, the river gate) stand
// on the land round the plateau (OzL16Outskirts: the lowland at their feet, raised under each part). A LOW shot (the mission-start
// flyover until its last shot, an ending's scene, a picture with ForceOn) does not draw that land: the far water of the low look
// (OzL16FlyoverFlora.FarLand.cs) lies at FarWaterY there, 3 to 7 m under their feet, so the far pyramid of the dawn shot hung in the
// air over it. So while the low-shot look is on, every landmark part beyond the rim that does not stand down in the water already gets
// an island of land under it: a flat top over its box (a little into its foot, so no slit shows under it), falling away into the far
// water as the land round the plateau falls from a raised foot, its shore a little uneven; drawn with the rim floor's own material
// (the jungle floor round the map). Made once from the parts' boxes when the low look first comes on, drawn only with the far land:
// no rule reads it, it has no collider, the steep camera never draws it.
// Play check: return OpenZ.OzL16FlyoverFlora.LandmarkCheck();
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL16FlyoverFlora
    {
        /// <summary>Off = the low shots without the islands (for a before / after look); set before the low look first comes on.</summary>
        public static bool Isles = true;
        /// <summary>The islands: their grid (m), the flat margin round a part's box (m), how fast the land falls away from it (m per m, as
        /// the land round the plateau falls from a raised foot) and how uneven that fall is (a share), how far the top stands into the foot
        /// of a part at least IsleTallPart high (m) or under a flat one (m: the causeway's slabs), how far under the far water the shore's
        /// edge ends (m).</summary>
        public static float IsleCell = 2f, IslePad = 2f, IsleSlope = 0.32f, IsleWobble = 0.35f, IsleSink = 0.3f, IsleUnder = 0.1f, IsleTallPart = 1f, IsleBelow = 0.6f;
        /// <summary>LandmarkCheck: a landmark part whose foot stands more than this over the ground the low look draws under it hangs (m).</summary>
        public static float IsleHang = 0.3f;
        /// <summary>Counters: landmark parts with an island under them, the islands' triangles.</summary>
        public static int IsleParts, IsleTriangles;

        struct IsleBox { public float x0, z0, x1, z1, top; }
        readonly List<IsleBox> isleBoxes = new List<IsleBox>();
        readonly Dictionary<long, int> isleVid = new Dictionary<long, int>();
        readonly HashSet<long> isleCells = new HashSet<long>();
        readonly List<Vector3> isleV = new List<Vector3>();
        GameObject isleGo; Mesh isleMesh; bool isleTried;
        static readonly RaycastHit[] isleHits = new RaycastHit[32];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void IslesResetStatics() { Isles = true; IsleParts = IsleTriangles = 0; }

        static long IsleKey(int i, int j) => ((long)i << 32) ^ (uint)j;

        /// <summary>A drawn part of a landmark beyond the rim (the filter of OzL16Outskirts.LandmarkRises: its parent is a SUR_ landmark).</summary>
        static bool IsleLandmark(Renderer r) => r && r.enabled && r.transform.parent && r.transform.parent.name.StartsWith("SUR_");

        /// <summary>How uneven the islands' fall is at (x, z): -1..1, slow (a few tens of metres).</summary>
        static float IsleWob(float x, float z) => 0.6f * Mathf.Sin(0.21f * x + 1.3f) * Mathf.Sin(0.17f * z + 0.4f) + 0.4f * Mathf.Sin(0.11f * (x + z) + 2.1f);

        /// <summary>The islands' height at (x, z): the highest of every part's flat top falling away from its box, never under the shore's edge.</summary>
        float IsleH(float x, float z)
        {
            float h = FarWaterY - IsleBelow, k = IsleSlope * (1f + IsleWobble * IsleWob(x, z));
            foreach (var b in isleBoxes)
            {
                float dx = Mathf.Max(Mathf.Max(b.x0 - x, 0f), x - b.x1), dz = Mathf.Max(Mathf.Max(b.z0 - z, 0f), z - b.z1);
                float t = b.top - k * Mathf.Sqrt(dx * dx + dz * dz);
                if (t > h) h = t;
            }
            return h;
        }

        /// <summary>ShowFar: the islands go in and out with the far land (made the first time they are wanted).</summary>
        void IslesShow(bool show)
        {
            if (!show) { if (isleGo) isleGo.SetActive(false); return; }
            if (!Isles) return;
            if (!isleTried) { isleTried = true; BuildIsles(); }
            if (isleGo) isleGo.SetActive(true);
        }

        void BuildIsles()
        {
            IsleParts = IsleTriangles = 0; isleBoxes.Clear(); isleVid.Clear(); isleCells.Clear(); isleV.Clear();
            var st = GameObject.Find("L16_Structures");
            var rf = GameObject.Find("RIM2_Floor"); var rr = rf ? rf.GetComponent<Renderer>() : null;
            if (!st || !rr || !rr.sharedMaterial) { Debug.LogWarning("[OpenZ L16 flyover flora] the landmarks (L16_Structures) or the rim floor (RIM2_Floor) were not found: no islands under the landmarks in the low shots"); return; }
            if (map == Vector2.zero) { var rts = FindFirstObjectByType<RtsCamera>(); map = rts ? rts.mapSizeMeters : new Vector2(208f, 256f); }
            float bottom = FarWaterY - IsleBelow;
            foreach (var r in st.GetComponentsInChildren<Renderer>(false))
            {
                if (!IsleLandmark(r)) continue;
                var b = r.bounds;
                if (Outside(b.center) <= GroundMargin + 0.5f || b.min.y < FarWaterY + 0.1f) continue;     // on the rim's own floor, or down in the water already
                float top = b.min.y + (b.size.y >= IsleTallPart ? IsleSink : -IsleUnder);
                isleBoxes.Add(new IsleBox { x0 = b.min.x - IslePad, z0 = b.min.z - IslePad, x1 = b.max.x + IslePad, z1 = b.max.z + IslePad, top = top });
            }
            IsleParts = isleBoxes.Count;
            if (IsleParts == 0) return;
            // the cells: every square within reach of a part's box, none on the plateau (the far water's own hole), none wholly under the water
            float px0 = -GroundMargin, px1 = map.x + GroundMargin, pz0 = -map.y - GroundMargin, pz1 = GroundMargin, c = IsleCell;
            var want = new List<Vector2Int>(); var seen = new HashSet<long>();
            foreach (var b in isleBoxes)
            {
                float reach = (b.top - bottom) / Mathf.Max(0.01f, IsleSlope * (1f - IsleWobble));
                int i0 = Mathf.FloorToInt((b.x0 - reach) / c), i1 = Mathf.CeilToInt((b.x1 + reach) / c), j0 = Mathf.FloorToInt((b.z0 - reach) / c), j1 = Mathf.CeilToInt((b.z1 + reach) / c);
                for (int i = i0; i < i1; i++)
                    for (int j = j0; j < j1; j++)
                        if (seen.Add(IsleKey(i, j))) want.Add(new Vector2Int(i, j));
            }
            var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
            float tile = Mathf.Max(0.1f, OzL16Outskirts.RimTile);
            var gh = new Dictionary<long, float>();
            float G(int i, int j)                                                                          // the height at a grid point (each one worked out once)
            {
                long k = IsleKey(i, j);
                if (!gh.TryGetValue(k, out float h)) { h = IsleH(i * c, j * c); gh[k] = h; }
                return h;
            }
            int V(int i, int j)
            {
                long k = IsleKey(i, j);
                if (isleVid.TryGetValue(k, out int at)) return at;
                float x = i * c, z = j * c; at = isleV.Count; isleVid[k] = at;
                isleV.Add(new Vector3(x, G(i, j), z));
                nrm.Add(new Vector3(G(i - 1, j) - G(i + 1, j), 2f * c, G(i, j - 1) - G(i, j + 1)).normalized);
                uv.Add(new Vector2(x / tile, z / tile)); col.Add(Color.white);                              // by world position, as the rim floor
                return at;
            }
            foreach (var q in want)
            {
                float cx = (q.x + 0.5f) * c, cz = (q.y + 0.5f) * c;
                if (cx > px0 && cx < px1 && cz > pz0 && cz < pz1) continue;
                float hi = Mathf.Max(Mathf.Max(G(q.x, q.y), G(q.x + 1, q.y)), Mathf.Max(G(q.x + 1, q.y + 1), G(q.x, q.y + 1)));
                if (hi <= bottom + 0.01f) continue;
                int a = V(q.x, q.y), b = V(q.x + 1, q.y), cc = V(q.x + 1, q.y + 1), d = V(q.x, q.y + 1);
                tri.Add(a); tri.Add(d); tri.Add(cc); tri.Add(a); tri.Add(cc); tri.Add(b);                         // clockwise from above
                isleCells.Add(IsleKey(q.x, q.y));
            }
            if (tri.Count == 0) return;
            isleMesh = new Mesh { name = "L16_FarIsles", indexFormat = isleV.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            isleMesh.SetVertices(isleV); isleMesh.SetNormals(nrm); isleMesh.SetUVs(0, uv); isleMesh.SetColors(col); isleMesh.SetTriangles(tri, 0); isleMesh.RecalculateBounds();
            isleGo = new GameObject("L16_FarIsles"); isleGo.transform.SetParent(transform, false);
            isleGo.AddComponent<MeshFilter>().sharedMesh = isleMesh;
            var mr = isleGo.AddComponent<MeshRenderer>(); mr.sharedMaterial = rr.sharedMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = true;
            isleGo.SetActive(false);
            IsleTriangles = tri.Count / 3;
            Debug.Log($"[OpenZ L16 flyover flora] low shot: islands under {IsleParts} landmark parts beyond the rim ({IsleTriangles} triangles, {isleCells.Count} cells of {IsleCell:F0} m, the rim floor's material)");
        }

        /// <summary>The ground a low shot draws at (x, z): on the plateau its own ground (the level's ground collider, the highest hit); beyond
        /// it the island drawn there (its own mesh points), else the far water.</summary>
        float LowGround(float x, float z, out string what)
        {
            if (x > -GroundMargin && x < map.x + GroundMargin && z > -map.y - GroundMargin && z < GroundMargin)
            {
                float y = float.MinValue; int k = Physics.RaycastNonAlloc(new Vector3(x, 400f, z), Vector3.down, isleHits, 800f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < k; i++) if (isleHits[i].collider && isleHits[i].collider.name.StartsWith("COL_Terrain") && isleHits[i].point.y > y) y = isleHits[i].point.y;
                what = "the plateau"; return y > float.MinValue ? y : FarWaterY;
            }
            int ci = Mathf.FloorToInt(x / IsleCell), cj = Mathf.FloorToInt(z / IsleCell);
            if (isleCells.Contains(IsleKey(ci, cj)))
            {
                float u = x / IsleCell - ci, w = z / IsleCell - cj;
                float h00 = isleV[isleVid[IsleKey(ci, cj)]].y, h10 = isleV[isleVid[IsleKey(ci + 1, cj)]].y, h11 = isleV[isleVid[IsleKey(ci + 1, cj + 1)]].y, h01 = isleV[isleVid[IsleKey(ci, cj + 1)]].y;
                what = "an island"; return (1f - u) * (1f - w) * h00 + u * (1f - w) * h10 + u * w * h11 + (1f - u) * w * h01;
            }
            what = "the far water"; return FarWaterY;
        }

        void IslesDestroy() { if (isleMesh) Destroy(isleMesh); isleMesh = null; }

        static string IslesStatus() => !I ? "" : $", islands under the landmarks beyond the rim {(I.isleGo ? (I.isleGo.activeSelf ? "drawn" : "built, not drawn") : I.isleTried ? "none made" : "not made yet")} ({IsleParts} parts, {IsleTriangles} triangles)";

        /// <summary>Play check (any moment; judged while the low look is on): every landmark part beyond the rim against the ground the low shot
        /// draws under its foot (nine points of its box: corners, side middles, middle), and the outskirts' flume rocks drawn.</summary>
        public static string LandmarkCheck()
        {
            if (!I) return "landmark check: not in this scene (level 16 only)";
            var st = GameObject.Find("L16_Structures");
            if (!st) return "landmark check: the landmarks (L16_Structures) were not found";
            if (I.map == Vector2.zero) { var rts = FindFirstObjectByType<RtsCamera>(); I.map = rts ? rts.mapSizeMeters : new Vector2(208f, 256f); }
            bool low = LowLookOn, drawn = I.isleGo && I.isleGo.activeInHierarchy;
            int parts = 0, water = 0, onIsle = 0, onPlateau = 0, hang = 0; float worstAll = float.MinValue; var names = new StringBuilder();
            foreach (var r in st.GetComponentsInChildren<Renderer>(false))
            {
                if (!IsleLandmark(r)) continue;
                var b = r.bounds;
                if (I.Outside(b.center) <= GroundMargin + 0.5f) continue;
                parts++;
                if (b.min.y < FarWaterY + 0.1f) { water++; continue; }
                float worst = float.MinValue; bool plateau = false;
                for (int a = 0; a < 3; a++)
                    for (int c = 0; c < 3; c++)
                    {
                        float g = I.LowGround(Mathf.Lerp(b.min.x, b.max.x, a * 0.5f), Mathf.Lerp(b.min.z, b.max.z, c * 0.5f), out string what);
                        if (!drawn && what == "an island") g = FarWaterY;                                       // built but not drawn now: nothing under it
                        if (what == "the plateau") plateau = true;
                        worst = Mathf.Max(worst, b.min.y - g);
                    }
                worstAll = Mathf.Max(worstAll, worst);
                if (worst > IsleHang)
                {
                    hang++;
                    if (hang <= 6) names.Append(names.Length > 0 ? ", " : "").Append(r.transform.parent.name).Append('/').Append(r.name).Append(" +").Append(worst.ToString("F1")).Append(" m");
                }
                else if (plateau) onPlateau++; else onIsle++;
            }
            OzL16Outskirts.LowRockCount(out int rocks, out int rocksDrawn);
            bool ok = low && hang == 0 && rocksDrawn == 0 && (drawn || parts == water);
            string head = !low ? "landmark check: not judged (the low look is off: the steep camera draws the land round the plateau)" : ok ? "landmark check: PASS" : "landmark check: FAIL";
            return head + $" | low look {(low ? "ON" : "off")}, islands {(I.isleGo ? (drawn ? "drawn" : "built, not drawn") : "not made yet")} ({IsleParts} parts, {IsleTriangles} triangles)"
                 + $" | landmark parts beyond the rim {parts}: down in the water {water}, standing on an island {onIsle}, on the plateau's ground and an island {onPlateau}, over the far water with no ground under them {hang}"
                 + (names.Length > 0 ? " (" + names + ")" : "") + $" (highest foot over its ground {(parts > water ? worstAll.ToString("+0.00;-0.00") : "-")} m, at most {IsleHang:F1})"
                 + $" | outskirts flume rocks {rocks}: drawn {rocksDrawn}{(low ? ", held off with their land " + (rocks - rocksDrawn) : " (their land is drawn)")}";
        }
    }
}
