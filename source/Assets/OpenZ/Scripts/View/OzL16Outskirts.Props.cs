// OpenZ (GPL-3.0)
//  - The far pyramid (L16_Structures/Surround SUR_Pyramid*, 92 m east of the map) stood at three times the size of the map's own big
//    pyramid: 48.6 m wide and 28.7 m tall, six factories wide. It is half that size now, made smaller about the middle of its base, so
//    its foot stays on its ground; it is done in the level's first frame, before the land under the landmarks (LandmarkRises) and the
//    start flyover's islands (OzL16FlyoverFlora.Isles.cs) are made from its box. The big leafy tree on its north-west corner is a part
//    of the pyramid's own model (its crown 13.5 m wide at that size, 6.8 m at half of it, over the 2.78 m crown cap of the plants round
//    the map): its two levels of detail are drawn with copies of the same model without that tree (Resources/OpenZ/L16Props, the
//    stone, the step plants and the vines kept).
//  - The land under every landmark part beyond the rim reaches under its whole foot (and a grid cell more), so no corner of a ruin
//    stands over a dip of the land (the land round the plateau rose only under the middle of each part).
//  - Round 4's rocks at the ends of the rim's waterfall beds (L16_Outskirts_Rock_*): the two at the north flumes were the level's wall
//    pieces (cliff faces made to stand against the rim's wall: a flat plain back, a grass top), 10 to 12 m tall, turned with their plain
//    back to the map; at the RTS zoom their far shape drew that back with the grass picture pulled down it in long streaks (the
//    owner's "floating rock"). Each is a pair of the level's own free-standing rock mass now (the cleanest far shape of the rock kit),
//    side by side across the wall piece's place so the flume's mouth stays closed, and every one of the rocks is at most as big as the
//    biggest of its kind on the map, upright as round 4 turned it, seated on the ground the camera sees (its lowest point a little under
//    the lowest land under it). The level's own rocks outside the map are only lowered where they stand over the ground.
// Drawn only: no rule reads any of it, nothing inside the map changes, nothing more is drawn (the pyramid draws fewer triangles).
// Switch: PropsOn (default true; read when the level starts). Play check: return OpenZ.OzL16Outskirts.OutskirtsPropsCheck();
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>Off = the landmarks and rocks outside the map as before (for a before / after look); set before the level starts.</summary>
        public static bool PropsOn = true;
        /// <summary></summary>
        public static float PyramidScale = 0.5f;
        /// <summary>Where the pyramid's levels of detail without its own tree are (Resources; the mesh's name + PropsNoTree).</summary>
        public const string PropsMeshes = "OpenZ/L16Props/", PropsNoTree = "_notree";
        /// <summary>A prop floats when its lowest point stands more than this over the ground the camera sees under it (m, as the low
        /// look's LandmarkCheck); a rock is seated this share of its height into the land (m, at least / at most); a part lower than
        /// PropsFlat (m) is a flat deck (the causeway's slabs over the swamp), not counted as floating.</summary>
        public static float PropsFloat = 0.3f, RockSinkK = 0.12f, RockSinkMin = 0.15f, RockSinkMax = 0.5f, PropsFlat = 1f;
        /// <summary>A wall piece more than PairOver times as wide as the rock mass in its place (round 4 set it across a flume's mouth) gets
        /// a second rock mass beside the first: each PairOffset of the mass's width to one side of the wall piece's place along its width,
        /// the second turned PairTurn degrees (it shows its other side); its name is the first's + PairTag.</summary>
        public static float PairOver = 1.5f, PairOffset = 0.45f, PairTurn = 160f;
        public const string PairTag = "_pair";
        /// <summary>Counters (this level): pyramids made smaller, their levels of detail drawn without the tree, landmark parts with land
        /// under their whole foot, round 4's wall pieces swapped for the rock mass, second rock masses beside them, rocks made smaller to
        /// the map's biggest of their kind, rocks seated, the level's own rocks outside the map lowered onto the ground.</summary>
        public static int PyramidsHalved, PyramidLodsCut, PyramidLods, PadsAdded, RocksSwapped, RocksPaired, RocksCapped, RocksSeated, SceneRocksLowered;
        public static string PropsWhy = "";

        sealed class PyramidRow { public Transform t; public Vector3 scale0, base0, size0; public int lods, cut; }
        readonly List<PyramidRow> pyramids = new List<PyramidRow>();
        readonly Dictionary<string, Vector2> rockKindMax = new Dictionary<string, Vector2>();     // the map's own rocks: per kind the biggest height, width
        readonly List<Transform> sceneRocksOut = new List<Transform>();
        float rockBuriedMost; string rockBuriedName = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void PropsResetStatics() { PropsOn = true; PyramidsHalved = PyramidLodsCut = PyramidLods = PadsAdded = RocksSwapped = RocksPaired = RocksCapped = RocksSeated = SceneRocksLowered = 0; PropsWhy = ""; }

        // ------------------------------------------------------------------ the far pyramid (Start, before the land and the islands)
        /// <summary>Start: every SUR_Pyramid* landmark at PyramidScale about the middle of its base, its levels of detail without its own tree.</summary>
        void PropsEarly()
        {
            PyramidsHalved = PyramidLodsCut = PyramidLods = PadsAdded = RocksSwapped = RocksPaired = RocksCapped = RocksSeated = SceneRocksLowered = 0; PropsWhy = "";
            pyramids.Clear();
            if (!PropsOn) { PropsWhy = "off (PropsOn false)"; return; }
            var st = GameObject.Find("L16_Structures"); var sur = st ? st.transform.Find("Surround") : null;
            if (!sur) { PropsWhy = "the landmarks (L16_Structures/Surround) were not found"; Debug.LogWarning("[OzL16Outskirts] " + PropsWhy); return; }
            foreach (Transform t in sur)
            {
                if (!t || !t.name.StartsWith("SUR_Pyramid")) continue;
                if (!PyramidBox(t, out Bounds b)) continue;
                var row = new PyramidRow { t = t, scale0 = t.localScale, base0 = new Vector3(b.center.x, b.min.y, b.center.z), size0 = b.size };
                t.position = row.base0 + (t.position - row.base0) * PyramidScale;                     // about the middle of its base: the foot stays
                t.localScale = t.localScale * PyramidScale;
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf ? mf.sharedMesh : null; if (!m) continue;
                    row.lods++;
                    var cut = Resources.Load<Mesh>(PropsMeshes + m.name + PropsNoTree);
                    if (!cut) continue;
                    Vector3 a = m.bounds.size, c = cut.bounds.size;
                    bool same = Mathf.Abs(a.x - c.x) <= 0.05f * a.x && Mathf.Abs(a.y - c.y) <= 0.05f * a.y && Mathf.Abs(a.z - c.z) <= 0.05f * a.z && (m.bounds.center - cut.bounds.center).magnitude <= 0.5f;
                    if (!same) { Debug.LogWarning("[OzL16Outskirts] the pyramid's copy without its tree does not fit its level of detail " + m.name + ": the tree stays there"); continue; }
                    mf.sharedMesh = cut; row.cut++;
                }
                PyramidsHalved++; PyramidLods += row.lods; PyramidLodsCut += row.cut;
                pyramids.Add(row);
            }
            if (PyramidsHalved == 0) PropsWhy = "no SUR_Pyramid landmark was found";
            else if (PyramidLodsCut < PyramidLods) Debug.LogWarning($"[OzL16Outskirts] the far pyramid's own tree is cut from {PyramidLodsCut} of its {PyramidLods} levels of detail (the copies under Resources/{PropsMeshes} were not found or do not fit)");
        }

        /// <summary>A landmark's box: its drawn levels of detail but LOD2 (the detailed shape stands on the ground), else all of them.</summary>
        static bool PyramidBox(Transform t, out Bounds b)
        {
            b = default; bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(false))
            {
                if (!r || !r.enabled || r.name == "LOD2") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any) return true;
            foreach (var r in t.GetComponentsInChildren<Renderer>(false))
            {
                if (!r) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        // ------------------------------------------------------------------ the land under the landmarks' whole foot (after LandmarkRises)
        /// <summary>BuildSliced, after LandmarkRises: the same parts (a SUR_ landmark's, not LOD2, beyond the rim, not down in the water),
        /// the land at their foot under the whole of it and one grid cell more (so each point of the drawn land under it reads that top).</summary>
        void PropsRises()
        {
            if (!PropsOn) return;
            var st = GameObject.Find("L16_Structures"); if (!st) return;
            float m = OzL16FlyoverFlora.GroundMargin, more = Cell * 1.42f;
            foreach (var r in st.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.transform.parent || !r.transform.parent.name.StartsWith("SUR_") || r.name == "LOD2") continue;   // LandmarkRises' own filter
                var b = r.bounds;
                if (Outside(b.center) <= m + 0.5f || b.min.y < waterY + 0.1f || b.size.y < PropsFlat) continue;      // the rim's floor, the water, a flat deck
                AddRise(b.center.x, b.center.z, b.min.y - 0.05f, Mathf.Sqrt(b.extents.x * b.extents.x + b.extents.z * b.extents.z) + more);
                PadsAdded++;
            }
        }

        // ------------------------------------------------------------------ the rocks (after PlaceFlumeRocks)
        static Renderer RockLod0(Transform t, out MeshFilter mf)
        {
            mf = null; Renderer first = null; MeshFilter firstMf = null;
            foreach (var f in t.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f || !f.sharedMesh) continue;
                var r = f.GetComponent<Renderer>(); if (!r) continue;
                if (f.name == "LOD0") { mf = f; return r; }
                if (!first) { first = r; firstMf = f; }
            }
            mf = firstMf; return first;
        }

        /// <summary>A rock's kind: its detailed shape's mesh name.</summary>
        static string RockKind(MeshFilter mf) => mf && mf.sharedMesh ? mf.sharedMesh.name : "";

        /// <summary>A rock kit's wall piece (made to stand against the rim's wall: its back is a plain cut face).</summary>
        static bool WallPiece(string kind) => kind.Contains("CliffFace") || kind.Contains("WaterfallFace");

        /// <summary>A rock's height and width (m): its detailed shape's own box through its scale (a turn about the upright aside).</summary>
        static void RockSize(MeshFilter mf, out float h, out float w)
        {
            var s = mf.transform.lossyScale; var z = mf.sharedMesh.bounds.size;
            h = z.y * Mathf.Abs(s.y); w = Mathf.Max(z.x * Mathf.Abs(s.x), z.z * Mathf.Abs(s.z));
        }

        /// <summary>The ground the steep camera sees at (x, z): the land's drawn triangles, the level's ground collider where it is higher
        /// (the plateau and its rim), and the outskirts' swamp water where a water cell is drawn.</summary>
        float PropsGround(float x, float z)
        {
            float g = Gen(x, z);
            if (GroundAt(x, z, out float y) && y > g) g = y;
            if (waterGo && gh != null && Outside(new Vector3(x, 0f, z)) > OzL16FlyoverFlora.GroundMargin + 8f)
            {
                int i = Mathf.FloorToInt((x - gx0) / Cell), j = Mathf.FloorToInt((z - gz0) / Cell);
                if (i >= 0 && j >= 0 && i + 1 < gnx && j + 1 < gnz)
                {
                    float lo = Mathf.Min(Mathf.Min(gh[j * gnx + i], gh[j * gnx + i + 1]), Mathf.Min(gh[(j + 1) * gnx + i], gh[(j + 1) * gnx + i + 1]));
                    if (lo < waterY + 0.02f && waterY > g) g = waterY;
                }
            }
            return g;
        }

        /// <summary>The ground under a box's foot: the lowest and the highest of 3 x 3 points over the middle share of its footprint.</summary>
        void FootGround(Bounds b, float share, out float lo, out float hi)
        {
            lo = float.MaxValue; hi = float.MinValue;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    float g = PropsGround(b.center.x + i * share * b.extents.x, b.center.z + j * share * b.extents.z);
                    lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g);
                }
        }

        /// <summary>The level's own rocks (L16_RimRocks, L16_FallRocks): per kind the biggest on the map, the ones outside the map, and
        /// the free-standing rock mass (the first CliffMass on the map).</summary>
        GameObject PropsScan()
        {
            rockKindMax.Clear(); sceneRocksOut.Clear();
            GameObject mass = null;
            foreach (var gname in new[] { "L16_RimRocks", "L16_FallRocks" })
            {
                var g = GameObject.Find(gname); if (!g) continue;
                foreach (Transform t in g.transform)
                {
                    var r = RockLod0(t, out MeshFilter mf); if (!r) continue;
                    string kind = RockKind(mf);
                    if (Outside(r.bounds.center) > 0f) { sceneRocksOut.Add(t); continue; }
                    RockSize(mf, out float h, out float w);
                    rockKindMax[kind] = rockKindMax.TryGetValue(kind, out Vector2 k) ? new Vector2(Mathf.Max(k.x, h), Mathf.Max(k.y, w)) : new Vector2(h, w);
                    if (!mass && kind.Contains("CliffMass")) mass = t.gameObject;                       // the level's free-standing rock mass
                }
            }
            return mass;
        }

        void PropsRocks()
        {
            rockBuriedMost = 0f; rockBuriedName = "";
            if (!PropsOn) return;
            var mass = PropsScan();
            var rocks = new List<Transform>(); var pairs = new List<Transform>();
            foreach (Transform c in transform) if (c && c.name.StartsWith(RockPrefix)) rocks.Add(c);
            foreach (var t0 in rocks)
            {
                var t = t0;
                var r = RockLod0(t, out MeshFilter mf); if (!r) continue;
                bool swapped = false; float wallW = 0f;
                if (WallPiece(RockKind(mf)) && mass)
                {   // a wall piece standing free with its plain back to the map: the level's rock mass in its place, turned as it was
                    RockSize(mf, out _, out wallW);                                                     // its own width: a second rock mass beside the first spans it
                    var old = t.gameObject; string nm = old.name; Vector3 at = old.transform.position; Quaternion turn = old.transform.rotation;
                    old.name = "k52 replaced " + nm; old.SetActive(false); old.transform.SetParent(null, true); Destroy(old);
                    var go = Instantiate(mass, transform); go.name = nm;
                    foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);              // drawn only, as round 4's
                    go.transform.SetPositionAndRotation(at, turn); go.transform.localScale = mass.transform.lossyScale;
                    go.SetActive(true);
                    t = go.transform; r = RockLod0(t, out mf); if (!r) continue;
                    swapped = true; RocksSwapped++;
                }
                string kind = RockKind(mf);
                RockSize(mf, out float h, out float w);
                if (rockKindMax.TryGetValue(kind, out Vector2 mx) && h > 0f && w > 0f)
                {   // at most the map's biggest of its kind (the rock mass in a wall piece's place: as big as that)
                    float f = Mathf.Min(mx.x / h, mx.y / w);
                    if (!swapped) f = Mathf.Min(1f, f);
                    if (Mathf.Abs(f - 1f) > 0.001f) { t.localScale *= f; w *= f; if (!swapped) RocksCapped++; }
                }
                if (swapped && w > 0f && wallW > PairOver * w) pairs.Add(PropsPair(t, w));            // the flume's mouth the wall piece closed stays closed
                Seat(t);
            }
            foreach (var t in pairs) Seat(t);
            void Seat(Transform t)
            {   // seated: its lowest point a little under the lowest ground under its foot
                var b = RockBox(t);
                FootGround(b, 0.6f, out float lo, out float hiG);
                float sink = Mathf.Clamp(RockSinkK * b.size.y, RockSinkMin, RockSinkMax);
                t.position += Vector3.up * (lo - sink - b.min.y);
                RocksSeated++;
                float buried = (hiG - lo + sink) / Mathf.Max(0.01f, b.size.y);
                if (buried > rockBuriedMost) { rockBuriedMost = buried; rockBuriedName = t.name; }
            }
            foreach (var t in sceneRocksOut)
            {   // the level's own rocks outside the map: only lowered where they stand over the ground
                var b = RockBox(t); if (b.size.y <= 0f) continue;
                FootGround(b, 0.6f, out float lo, out _);
                float gap = b.min.y - lo;
                if (gap <= PropsFloat) continue;
                t.position += Vector3.down * (gap + 0.05f); SceneRocksLowered++;
            }
            Debug.Log($"[OzL16Outskirts] props outside the map: the far pyramid at {PyramidScale:0.00} of its size ({PyramidsHalved} pyramids, its own tree cut from {PyramidLodsCut} of {PyramidLods} levels of detail), land under the whole foot of {PadsAdded} landmark parts, flume rocks: wall pieces swapped for the rock mass {RocksSwapped} (a second rock mass beside {RocksPaired} of them), made as small as the map's biggest of their kind {RocksCapped}, seated {RocksSeated}; the level's own rocks outside the map lowered onto the ground {SceneRocksLowered}");
        }

        /// <summary>A rock's box: its detailed shape's drawn box.</summary>
        static Bounds RockBox(Transform t)
        {
            var r = RockLod0(t, out _);
            return r ? r.bounds : new Bounds(t.position, Vector3.zero);
        }

        /// <summary>The rock mass in a wall piece's place is about half its width (round 4 set the wall piece across a flume's mouth): a
        /// second rock mass of the same size beside it, turned to show its other side, the pair across the wall piece's place along its
        /// width, so the mouth stays closed by rocks no bigger than the map's biggest of their kind. Seated by the caller.</summary>
        Transform PropsPair(Transform first, float width)
        {
            var right = first.rotation * Vector3.right; right.y = 0f;
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            float off = PairOffset * width;
            var go = Instantiate(first.gameObject, transform); go.name = first.name + PairTag;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);                  // drawn only, as round 4's
            go.transform.SetPositionAndRotation(first.position + right * off, Quaternion.AngleAxis(PairTurn, Vector3.up) * first.rotation);
            go.transform.localScale = first.localScale;
            first.position -= right * off;
            RocksPaired++;
            return go.transform;
        }

        // ------------------------------------------------------------------ Play check
        static string PropsSize(Bounds b) => $"{Mathf.Max(b.size.x, b.size.z):0.0} x {b.size.y:0.0} m ({Mathf.Max(b.size.x, b.size.z) / FactoryWidth:0.00} factories wide)";

        static bool Drawable(Renderer r) => r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy;

        /// <summary>Play check (the steep camera, the land shown): the far pyramid's size, its base on its ground, its tree; every landmark
        /// part, outskirts rock and level rock outside the map: floating (its lowest point over the ground the camera sees, 3 x 3 points
        /// over the middle of its foot), stretched (a wall piece with its plain back turned to the map), bigger than the map's biggest of its
        /// kind; their sizes against a factory (8 m).</summary>
        public static string OutskirtsPropsCheck()
        {
            var o = I; if (!o) return "OzL16Outskirts props: not on this level";
            if (!o.built) return "OzL16Outskirts props: the land is not built yet";
            if (OzL16FlyoverFlora.LowLookOn) return "OzL16Outskirts props: not judged in a low shot (the land stands back: OzL16FlyoverFlora.LandmarkCheck() judges the landmarks there)";
            int fail = 0; var sb = new StringBuilder(); var sizes = new StringBuilder();
            if (o.rockKindMax.Count == 0) o.PropsScan();                                                    // the switch off: the level's rocks as they are
            // the far pyramid
            int pyrOk = 0, pyrScale = 0; string pyrText = "";
            foreach (var p in o.pyramids)
            {
                if (!p.t) continue;
                float k = p.t.localScale.x / Mathf.Max(1e-6f, p.scale0.x);
                if (Mathf.Abs(k - PyramidScale) < 0.001f) pyrScale++;
                if (!PyramidBox(p.t, out Bounds b)) continue;
                o.FootGround(b, 0.9f, out float lo, out _);
                float gap = b.min.y - lo;
                float baseMoved = Mathf.Abs(b.min.y - p.base0.y) + new Vector2(b.center.x - p.base0.x, b.center.z - p.base0.z).magnitude;
                if (gap <= PropsFloat && baseMoved < 0.5f) pyrOk++;
                pyrText += $" {p.t.name} scale {k:0.000} of the level's: {Mathf.Max(p.size0.x, p.size0.z):0.0} x {p.size0.y:0.0} m -> {PropsSize(b)}, base moved {baseMoved:0.00} m, highest point of its base over its ground {gap:+0.00;-0.00} m, own tree cut from {p.cut} of {p.lods} levels of detail;";
            }
            int pyrTrees = 0, pyrTreesDrawn = 0;
            var flora = GameObject.Find(FloraRoot);
            if (flora) foreach (var r in flora.GetComponentsInChildren<Renderer>(true)) if (r && r.name.StartsWith("SUR_Pyr2Tree")) { pyrTrees++; if (Drawable(r)) pyrTreesDrawn++; }
            if (o.pyramids.Count == 0 && GameObject.Find("L16_Structures") is GameObject s0 && s0.transform.Find("Surround") is Transform s1)
                foreach (Transform t in s1) if (t && t.name.StartsWith("SUR_Pyramid") && PyramidBox(t, out Bounds b0)) pyrText += $" {t.name} not made smaller ({(PropsOn ? "not found at the start" : "PropsOn false")}): {PropsSize(b0)};";
            bool pyrPass = o.pyramids.Count > 0 && pyrScale == o.pyramids.Count && pyrOk == o.pyramids.Count && PyramidLodsCut == PyramidLods && PyramidLods > 0 && pyrTreesDrawn == 0;
            if (!pyrPass) fail++;
            sb.Append($" | PYRAMIDS {o.pyramids.Count}: {(pyrPass ? "ok" : "FAIL")} (scale {PyramidScale:0.0} {pyrScale} of {o.pyramids.Count}, on the ground {pyrOk};{pyrText} the old '_lite' trees that stood on its steps (SUR_Pyr2Tree_*) {pyrTrees}, drawn {pyrTreesDrawn})");
            // every prop outside the map
            int parts = 0, flat = 0, rocks = 0, scene = 0, floating = 0, stretched = 0, over = 0; float worst = float.MinValue, flatWorst = float.MinValue; string worstName = "", floatNames = "", overNames = "", stretchNames = "";
            void Judge(string name, Bounds b, float share, bool isFlat)
            {
                o.FootGround(b, share, out float lo, out _);
                float gap = b.min.y - lo;
                if (isFlat) { flat++; flatWorst = Mathf.Max(flatWorst, gap); return; }
                if (gap > worst) { worst = gap; worstName = name; }
                if (gap > PropsFloat) { floating++; if (floating <= 8) floatNames += $" {name} +{gap:0.00} m;"; }
            }
            var st = GameObject.Find("L16_Structures"); var sur = st ? st.transform.Find("Surround") : null;
            if (sur)
                foreach (Transform t in sur)
                {
                    if (!t) continue;
                    Bounds all = default; bool any = false;
                    foreach (var r in t.GetComponentsInChildren<Renderer>(false))
                    {
                        if (!Drawable(r)) continue;
                        if (!any) { all = r.bounds; any = true; } else all.Encapsulate(r.bounds);
                        if (o.Outside(r.bounds.center) <= OzL16FlyoverFlora.GroundMargin + 0.5f) continue;   // on the rim's own floor: the rim's checks
                        parts++;
                        Judge(t.name + "/" + r.name, r.bounds, 0.9f, r.bounds.size.y < PropsFlat);
                    }
                    if (any) sizes.Append($" {t.name} {PropsSize(all)};");
                }
            var mapMid = new Vector3(o.map.x * 0.5f, 0f, -o.map.y * 0.5f);
            void JudgeRock(Transform t, bool outskirts)
            {
                var r = RockLod0(t, out MeshFilter mf); if (!r || !mf) return;
                bool drawn = false; foreach (var rr in t.GetComponentsInChildren<Renderer>(true)) if (Drawable(rr)) { drawn = true; break; }
                if (!drawn) return;
                if (outskirts) rocks++; else scene++;
                Judge(t.name, r.bounds, 0.6f, false);
                string kind = RockKind(mf);
                if (WallPiece(kind))
                {
                    var back = mf.transform.forward; var to = mapMid - t.position;
                    if (back.x * to.x + back.z * to.z > 0f) { stretched++; stretchNames += $" {t.name} ({kind});"; }
                }
                RockSize(mf, out float h, out float w);
                if (o.rockKindMax.TryGetValue(kind, out Vector2 mx) && (h > mx.x * 1.01f || w > mx.y * 1.01f)) { over++; if (over <= 8) overNames += $" {t.name} {kind} {w:0.0} x {h:0.0} m (the map's biggest {mx.y:0.0} x {mx.x:0.0});"; }
            }
            foreach (Transform c in o.transform) if (c && c.name.StartsWith(RockPrefix)) JudgeRock(c, true);
            foreach (var t in o.sceneRocksOut) if (t) JudgeRock(t, false);
            if (floating > 0) fail++; if (stretched > 0) fail++; if (over > 0) fail++;
            sb.Append($" | PROPS outside the map {parts + rocks + scene} (landmark parts beyond the rim {parts}, outskirts rocks {rocks}, the level's own rocks outside the map {scene}): floating {floating}{(floating > 0 ? " (" + floatNames + ")" : "")} (highest foot over the ground the camera sees {worst:+0.00;-0.00} m at {worstName}, at most {PropsFloat:0.0}; flat deck parts {flat}, highest {(flat > 0 ? flatWorst.ToString("+0.00;-0.00") : "-")} m, not counted), stretched {stretched}{(stretched > 0 ? " (" + stretchNames + ")" : "")} (a wall piece with its plain back to the map), bigger than the map's biggest of their kind {over}{(over > 0 ? " (" + overNames + ")" : "")}");
            sb.Append($" | ROCKS: round 4's wall pieces swapped for the level's rock mass {RocksSwapped} (a second rock mass beside {RocksPaired} of them, across the flume's mouth), made as small as the map's biggest of their kind {RocksCapped}, seated {RocksSeated} (most buried: {o.rockBuriedName} {100f * o.rockBuriedMost:0} % of its height), the level's own lowered onto the ground {SceneRocksLowered}; land under the whole foot of {PadsAdded} landmark parts");
            var kinds = new StringBuilder(); foreach (var kv in o.rockKindMax) kinds.Append($" {kv.Key} {kv.Value.y:0.0} x {kv.Value.x:0.0} m;");
            sb.Append(" | the map's biggest rock of each kind:" + kinds);
            sb.Append(" | SIZES (a factory is 8 m wide):" + sizes);
            return $"OzL16Outskirts props{(PropsOn ? "" : " (PropsOn false: the props as before)")}: {(fail == 0 ? "PASS" : "FAIL")}" + sb;
        }

        /// <summary>Play helper for the pictures: the RTS camera over the far pyramid turned to it (heading, deg; the camera's own tilt),
        /// </summary>
        public static string PropsView(float height, float heading = 65f, float fx = 300f, float fz = -80f)
        {
            var o = I; var cam = o ? o.rts : null; if (!cam) return "no RTS camera";
            if (height <= 0f) { cam.yawHeld = false; cam.yaw = 0f; cam.unclamped = false; cam.Place(); return $"camera back: over ({cam.focus.x:0},{cam.focus.z:0}) at {cam.height:0} m, heading 0"; }
            cam.FreeLookOff(); cam.yawHeld = true; cam.yaw = heading; cam.unclamped = true; cam.focus = new Vector3(fx, 0f, fz); cam.SetHeightImmediate(height);
            return $"camera over ({cam.focus.x:0},{cam.focus.z:0}) at {cam.height:0} m, heading {heading:0} (limits off until PropsView(0))";
        }
    }
}
