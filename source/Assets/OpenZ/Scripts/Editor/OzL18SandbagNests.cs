// OpenZ (GPL-3.0)
// Editor only. Level 18's twelve Zod map objects 14 get a sandbag position from the approved SandbagsV2 kit instead of the
// pale textured cube of kit l18_street_r3 (L18S_ConcreteBlock), which read as a placeholder box with a face.
//   What they are: map items of the original Z map (p02_bb_orig18.map, Resources/OpenZ/Maps/L18_grid.json, "map_item:map_object14"), Zod's
//   destroyable cover block (refs/zod_engine/assets/other/map_items/map_object14.png: a grey cracked block). The Sim keeps them as they are:
//   each blocks its tile (ZWorld SetMapImpassables) and direct fire (ZSight, city map objects but 11 / 12 / 13), explosives wear it down
//   (map_item_health), its death throws Zod's debris missile. They stand beside the flags, the neutral factories, the gun posts and the two
//   north bridgeheads: a sandbag position is what a city at war puts there. Only the look changes: no Sim file, no map, no place, no rule.
//   The nest: a front wall of 2 x 2 bags (SB2_Seg_I2, 1.54 m) and two side walls running back from its ends (open at the back), drawn x0.95:
//   1.46 x 1.90 m inside the 2 m Zod tile, 0.58 m high; Damaged = SB2_Seg_D2, Rubble = SB2_Seg_R2 (each wall's spill side outward), LOD0 +
//   LOD1 like the level 17 walls (OpenZSandbagsV2), level 17's material M_SandbagsV2_L17 (not changed), no collider. The front faces the other
//   side of the river: a nest in the north (blue) half faces south, one in the south (red) half faces north (the two bridgeheads face the
//   bridge they guard); each sits on the lowest ground under its footprint. View/OzSandbagNest.cs draws the stages and the break.
// Menus: OpenZ/V1.5/L18 placeholder boxes r6/1 (open level 18 scene: builds the 12 nests, saves the scene), /2 checks the open level 18 scene
// (no write) into Logs/l18_placeholder_boxes_r6_check.txt. OzStreetR3.Blocks() (Street furniture r3 menu 1) builds the nests too, so the cube
// can never come back from that menu.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18SandbagNests
    {
        public const string NestName = "SandbagNest", Tag = "[OpenZ] L18 sandbag nests r6";
        public const string KitFbx = "Assets/OpenZ/Models/Sandbags/V2/SandbagsV2_Kit.fbx", Lod1Dir = "Assets/OpenZ/Models/Sandbags/V2/LOD1";
        public const string MatPath = "Assets/OpenZ/Materials/Env/M_SandbagsV2_L17.mat";
        /// <summary>The kit drawn at x0.95: footprint 1.46 x 1.90 m inside the 2 m Zod tile.</summary>
        public const float Scale = 0.95f;
        /// <summary>Level 18's Zod map objects 14 (L18_grid.json); level 17 has 22 of the same, NOT touched by this kit.</summary>
        public const int Want = 12, L17Count = 22;
        const float Lod0Height = 0.18f, CullHeight = 0.01f, MaxSpread = 0.06f;
        static readonly string[] Stages = { "Intact", "Damaged", "Rubble" };
        static readonly string[] Walls = { "Front", "Left", "Right" };
        static readonly string[] IntactPieces = { "SB2_Seg_I2_v0", "SB2_Seg_I2_v1", "SB2_Seg_I2_v2" };
        const string DamagedPiece = "SB2_Seg_D2", RubblePiece = "SB2_Seg_R2";
        static readonly string[] Bags = { "SB2_Bag_A", "SB2_Bag_B", "SB2_Bag_C" };

        [System.Serializable] class MapObj { public int x, y; public string kind; }
        [System.Serializable] class MapDoc { public int width, height; public MapObj[] objects; }

        static MapDoc Map(string level)
        {
            var ta = Resources.Load<TextAsset>($"OpenZ/Maps/{level}_grid");
            return ta ? JsonUtility.FromJson<MapDoc>(ta.text) : null;
        }

        static bool Tile(string name, out int tx, out int ty)
        {   // map_object14_<x>_<y>
            tx = ty = -1;
            var p = name.Split('_');
            return p.Length == 4 && int.TryParse(p[2], out tx) && int.TryParse(p[3], out ty);
        }

        /// <summary>The front's world yaw: a nest in the north (blue) half of the map faces south (world -z, yaw 180), one in the south (red)
        /// half faces north (+z, yaw 0). Level 18's river runs east-west through the middle (tile rows 53-66 of 120).</summary>
        public static float FrontYaw(int ty, int mapHeight) => ty < mapHeight / 2 ? 180f : 0f;

        // ------------------------------------------------------------------ menus
        [MenuItem("OpenZ/V1.5/L18 placeholder boxes r6/1 Sandbag nests on the 12 Zod map objects 14 (open level 18 scene, saves it)", false, 7795)]
        public static string BuildMenu()
        {
            var s = Build(true);
            if (s.StartsWith("FAILED")) Debug.LogError(Tag + ": " + s); else Debug.Log(Tag + ": " + s + " Then menu 2 (check).");
            return s;
        }

        [MenuItem("OpenZ/V1.5/L18 placeholder boxes r6/2 Check the open level 18 scene (no write)", false, 7796)]
        public static string CheckMenu()
        {
            var s = Check();
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")); Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "l18_placeholder_boxes_r6_check.txt"), s.Replace(" | ", "\n") + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning(Tag + ": check file not written: " + e.Message); }
            if (s.StartsWith("PASS")) Debug.Log(Tag + " check: " + s); else Debug.LogError(Tag + " check: " + s);
            return s;
        }

        // ------------------------------------------------------------------ build
        /// <summary>Builds the nest on every map_object14 of the open level 18 scene (idempotent: the r3 concrete block, any Unity cube of
        /// Z_city_block and a nest made before go first). save = save the scene (OzStreetR3's menu 1 saves itself).</summary>
        public static string Build(bool save)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (Application.isPlaying || OzCity.LevelOfScene(scene.name) != OzCity.BridgeGame) return "FAILED: open the level 18 scene, out of Play (" + scene.name + ")";
            var root = GameObject.Find("ZodObjects");
            if (!root) return "FAILED: no ZodObjects in the open scene";
            var kit = KitMeshes();
            foreach (var n in IntactPieces.Concat(new[] { DamagedPiece, RubblePiece, "SB2_Seg_IC_v0" }).Concat(Bags))
                if (!kit.ContainsKey(n)) return $"FAILED: {KitFbx} has no {n} (nothing changed)";
            if (!kit["SB2_Seg_IC_v0"].isReadable) return "FAILED: the sandbag kit is not readable (menu OpenZ/Sandbags/V2 Build both levels imports it) (nothing changed)";
            int turn = KitTurn(kit["SB2_Seg_IC_v0"], out var turnWhy);
            if (turn < 0) return "FAILED: " + turnWhy + " (nothing changed)";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (!mat) return "FAILED: " + MatPath + " missing (nothing changed)";
            var map = Map(OzCity.BridgeGame);
            if (map == null || map.height <= 0) return "FAILED: Resources/OpenZ/Maps/L18_grid.json unreadable (nothing changed)";
            var targets = root.transform.Cast<Transform>().Where(t => t.name.StartsWith("map_object14_")).OrderBy(t => t.name, System.StringComparer.Ordinal).ToList();
            if (targets.Count != Want) return $"FAILED: {targets.Count} map_object14 under ZodObjects, {Want} expected (nothing changed)";
            var ground = GameObject.Find("COL_Terrain")?.GetComponent<Collider>();
            var bags = Bags.Select(n => kit[n]).ToArray();
            var ib = kit[IntactPieces[0]].bounds;
            float L = ib.size.x, D = ib.size.z, H = (L + D) * 0.5f;                       // front wall length, wall depth, half the footprint depth
            int k = 0, oldBlocks = 0, cubes = 0, again = 0, noGround = 0, lod1Missing = 0; float worstSpread = 0f; long tris0 = 0;
            var sb = new StringBuilder();
            foreach (var t in targets)
            {
                if (!Tile(t.name, out int tx, out int ty)) return "FAILED: " + t.name + " is not map_object14_<x>_<y> (the scene is half done: run menu 1 again)";
                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    var c = t.GetChild(i);
                    if (c.name == OzStreetR3.BlockName) { Object.DestroyImmediate(c.gameObject); oldBlocks++; continue; }
                    if (c.name == NestName) { Object.DestroyImmediate(c.gameObject); again++; continue; }
                    var mf = c.GetComponent<MeshFilter>();
                    if (mf && mf.sharedMesh && mf.sharedMesh.name == "Cube") { Object.DestroyImmediate(c.gameObject); cubes++; }
                }
                float yaw = FrontYaw(ty, map.height);
                var nest = new GameObject(NestName);
                nest.transform.SetParent(t, false);
                nest.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                nest.transform.localScale = Vector3.one * (Scale / Mathf.Max(1e-4f, t.lossyScale.x));
                // the lowest ground under the footprint (4 corners + centre): nothing floats; a spread over 6 cm is reported
                var c0 = new Vector3(t.position.x, t.position.y, t.position.z);
                float lo = float.MaxValue, hi = float.MinValue; int hits = 0;
                foreach (var q in new[] { new Vector2(0, 0), new Vector2(-L / 2, -H), new Vector2(L / 2, -H), new Vector2(-L / 2, H), new Vector2(L / 2, H) })
                {
                    var w = c0 + nest.transform.rotation * new Vector3(q.x * Scale, 0f, q.y * Scale);
                    if (ground && ground.Raycast(new Ray(new Vector3(w.x, c0.y + 60f, w.z), Vector3.down), out var hit, 200f)) { lo = Mathf.Min(lo, hit.point.y); hi = Mathf.Max(hi, hit.point.y); hits++; }
                }
                if (hits == 0) { noGround++; lo = hi = c0.y; }
                worstSpread = Mathf.Max(worstSpread, hi - lo);
                nest.transform.position = new Vector3(c0.x, lo, c0.z);
                var stages = new GameObject[3];
                for (int s = 0; s < 3; s++)
                {
                    var st = new GameObject(Stages[s]);
                    st.transform.SetParent(nest.transform, false);
                    stages[s] = st;
                    for (int wv = 0; wv < 3; wv++)
                    {
                        string piece = s == 0 ? IntactPieces[(k + wv) % 3] : s == 1 ? DamagedPiece : RubblePiece;
                        var m0 = kit[piece]; var m1 = Lod1(piece); if (!m1) lod1Missing++;
                        // nest-local (kit metres, +z = the front): the wall's inner face midpoint and its outward side
                        Vector3 inner, outward;
                        if (wv == 0) { inner = new Vector3(0f, 0f, H - D); outward = Vector3.forward; }
                        else { float sx = wv == 1 ? -1f : 1f; inner = new Vector3(sx * (L / 2 - D), 0f, H - D - L / 2); outward = new Vector3(sx, 0f, 0f); }
                        // the spill side (Blender -Y = Unity -z, +z after the importer's measured 180) outward; an intact wall is symmetric: every
                        // second one turned 180 deg for variety (OpenZSandbagsV2's rule for straight walls)
                        float th = Mathf.Atan2(-outward.x, -outward.z) * Mathf.Rad2Deg + (turn == 180 ? 180f : 0f) + (s == 0 && (k + wv) % 2 == 1 ? 180f : 0f);
                        var rot = Quaternion.Euler(0f, th, 0f);
                        var md = Quaternion.Inverse(rot) * outward;                                 // the outward side in the mesh's own frame
                        float zi = md.z > 0f ? m0.bounds.min.z : m0.bounds.max.z;                   // the mesh's inner face
                        var w = new GameObject(Walls[wv]);
                        w.transform.SetParent(st.transform, false);
                        w.transform.localRotation = rot;
                        w.transform.localPosition = inner - rot * new Vector3(m0.bounds.center.x, 0f, zi);
                        var r0 = Part(w, "LOD0", m0, mat);
                        var lods = new List<LOD> { new LOD(m1 ? Lod0Height : CullHeight, new Renderer[] { r0 }) };
                        if (m1) lods.Add(new LOD(CullHeight, new Renderer[] { Part(w, "LOD1", m1, mat) }));
                        var lg = w.AddComponent<LODGroup>();
                        lg.fadeMode = LODFadeMode.None; lg.SetLODs(lods.ToArray()); lg.RecalculateBounds();
                        if (s == 0) tris0 += Tris(m0);
                    }
                    if (s > 0) st.SetActive(false);
                }
                var comp = nest.AddComponent<OzSandbagNest>();
                comp.intact = stages[0]; comp.damaged = stages[1]; comp.rubble = stages[2]; comp.bagMeshes = bags; comp.material = mat;
                sb.Append($"{t.name} at ({c0.x:0.#}, {c0.z:0.#}) front {(yaw == 0f ? "north" : "south")}, ground {lo:0.000}{(hits > 0 ? $" (spread {(hi - lo) * 100f:0.#} cm)" : " (NO GROUND HIT)")}; ");
                k++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            if (worstSpread > MaxSpread || noGround > 0) Debug.LogWarning($"{Tag}: ground under a nest spreads {worstSpread * 100f:0.#} cm (rule 6 cm) or was not hit ({noGround}): look at those nests on a close frame");
            return $"{k} sandbag nests built ({oldBlocks} concrete blocks and {cubes} Unity cubes out{(again > 0 ? $", {again} nests made before: redone" : "")}), importer turn {turn} deg ({turnWhy}), " +
                   $"intact tris {tris0:N0} (all 12), LOD1 missing {lod1Missing}, worst ground spread {worstSpread * 100f:0.#} cm, no ground hit {noGround}{(save ? ", scene saved" : "")}. {sb}";
        }

        // ------------------------------------------------------------------ check (no write)
        public static string Check()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (Application.isPlaying || OzCity.LevelOfScene(scene.name) != OzCity.BridgeGame) return "FAIL: open the level 18 scene, out of Play (" + scene.name + ")";
            var rows = new List<string>(); int pass = 0, n = 0;
            void Row(bool ok, string what) { n++; if (ok) pass++; rows.Add((ok ? "PASS " : "FAIL ") + what); }
            var map = Map(OzCity.BridgeGame);
            var root = GameObject.Find("ZodObjects");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            Row(map != null && root && mat, "level 18 map, ZodObjects and M_SandbagsV2_L17 found");
            if (map == null || !root || !mat) return "FAIL " + pass + "/" + n + ": " + string.Join(" | ", rows);
            var inMap = (map.objects ?? new MapObj[0]).Where(o => o.kind == "map_item:map_object14").Select(o => $"map_object14_{o.x}_{o.y}").OrderBy(s => s, System.StringComparer.Ordinal).ToList();
            var targets = root.transform.Cast<Transform>().Where(t => t.name.StartsWith("map_object14_")).OrderBy(t => t.name, System.StringComparer.Ordinal).ToList();
            Row(inMap.Count == Want && targets.Select(t => t.name).SequenceEqual(inMap), $"Zod map objects 14: {targets.Count} in the scene, {inMap.Count} in L18_grid.json, the same names ({Want})");
            var ground = GameObject.Find("COL_Terrain")?.GetComponent<Collider>();
            int nests = 0, oldBlocks = 0, cubes = 0, wrongMat = 0, colliders = 0, badStages = 0, badPlace = 0, badFront = 0, outTile = 0, floatOrSunk = 0, badLink = 0;
            float hMin = float.MaxValue, hMax = 0f, worstGap = 0f;
            foreach (var t in targets)
            {
                Tile(t.name, out int tx, out int ty);
                if (new Vector2(t.position.x - (tx * 2 + 1), t.position.z + (ty * 2 + 1)).magnitude > 0.01f) badPlace++;   // the Zod object's place is unchanged
                oldBlocks += t.GetComponentsInChildren<Transform>(true).Count(c => c.name == OzStreetR3.BlockName);
                cubes += t.GetComponentsInChildren<MeshFilter>(true).Count(m => m.sharedMesh && m.sharedMesh.name == "Cube");
                colliders += t.GetComponentsInChildren<Collider>(true).Length;
                wrongMat += t.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.sharedMaterial != mat);
                var nt = t.Find(NestName); var nc = nt ? nt.GetComponent<OzSandbagNest>() : null;
                if (!nt || !nc) continue;
                nests++;
                bool stagesOk = nc.intact && nc.damaged && nc.rubble && nc.intact.activeSelf && !nc.damaged.activeSelf && !nc.rubble.activeSelf && nc.material == mat
                                && nc.bagMeshes != null && nc.bagMeshes.Length == 3 && nc.bagMeshes.All(bm => bm)
                                && new[] { nc.intact, nc.damaged, nc.rubble }.All(sg => sg.transform.parent == nt && sg.GetComponentsInChildren<LODGroup>(true).Length == 3);
                if (!stagesOk) badStages++;
                if (Vector3.Dot(nt.forward, new Vector3(0f, 0f, FrontYaw(ty, map.height) == 0f ? 1f : -1f)) < 0.999f) badFront++;
                if (!nc.intact) continue;
                var b = Box(nc.intact.transform);
                float cx = tx * 2 + 1, cz = -(ty * 2 + 1);
                if (b.min.x < cx - 1.02f || b.max.x > cx + 1.02f || b.min.z < cz - 1.02f || b.max.z > cz + 1.02f) outTile++;
                hMin = Mathf.Min(hMin, b.size.y); hMax = Mathf.Max(hMax, b.size.y);
                if (ground && ground.Raycast(new Ray(new Vector3(b.center.x, b.max.y + 60f, b.center.z), Vector3.down), out var hit, 200f))
                {
                    float gap = b.min.y - hit.point.y; worstGap = Mathf.Max(worstGap, Mathf.Abs(gap));
                    if (gap > 0.02f || gap < -0.08f) floatOrSunk++;
                }
                if (nc.simId >= 0) badLink++;                                                    // bound only in Play (ZGame.StaticViews)
            }
            Row(nests == Want, $"sandbag nests (SandbagNest + OzSandbagNest): {nests} of {Want}");
            Row(oldBlocks == 0, $"r3 concrete blocks (L18S_ConcreteBlock) left: {oldBlocks}");
            Row(cubes == 0, $"Unity cubes (Z_city_block's placeholder) left on them: {cubes}");
            Row(wrongMat == 0, $"renderers on them not wearing M_SandbagsV2_L17: {wrongMat}");
            Row(colliders == 0, $"colliders on them (view only): {colliders}");
            Row(badStages == 0, $"nests with Intact (on) / Damaged / Rubble (off), 3 walls each with LOD0 + LOD1, bags + material set: {nests - badStages} of {nests}");
            Row(badPlace == 0, $"Zod objects off their map tile centre (must be 0: places unchanged): {badPlace}");
            Row(badFront == 0, $"fronts facing the other side of the river (north half south, south half north): {nests - badFront} of {nests}");
            Row(outTile == 0, $"intact nests inside their 2 m Zod tile (+2 cm): {nests - outTile} of {nests}");
            Row(floatOrSunk == 0, $"intact nests on the ground (bottom 0..8 cm into it, never above): worst {worstGap * 100f:0.#} cm, bad {floatOrSunk}");
            Row(hMin >= 0.45f && hMax <= 0.75f, $"intact nest height {hMin:0.00}..{hMax:0.00} m (0.45..0.75: the kit's 2-row walls x0.95)");
            Row(badLink == 0, $"nests with a Play-time Zod id saved into the scene: {badLink}");
            int other = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
                if (mf.sharedMesh && mf.sharedMesh.name == "Cube" && mf.GetComponent<MeshRenderer>() && mf.GetComponent<MeshRenderer>().enabled && TopName(root.transform, mf.transform).StartsWith("map_object")) other++;
            Row(other == 0, $"visible flat Unity cubes left among all the Zod map objects (placeholder boxes): {other}");
            var l17 = Map(OzCity.CarPark);
            int l17n = l17?.objects?.Count(o => o.kind == "map_item:map_object14") ?? -1;
            rows.Add($"INFO level 17 has {l17n} Zod map objects 14 (Z_city_block, two dark Unity cubes each), NOT changed by this kit ({L17Count} expected)");
            return $"{(pass == n ? "PASS" : "FAIL")} {pass}/{n}: " + string.Join(" | ", rows);
        }

        static string TopName(Transform root, Transform t) { while (t.parent && t.parent != root) t = t.parent; return t.name; }

        static Bounds Box(Transform t)
        {
            var b = new Bounds(t.position, Vector3.zero); bool any = false;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || mf.name != "LOD0") continue;
                var mb = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        // ------------------------------------------------------------------ the kit (as OpenZSandbagsV2 reads it)
        static Dictionary<string, Mesh> KitMeshes()
        {
            var d = new Dictionary<string, Mesh>();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(KitFbx);
            if (go)
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh && !d.ContainsKey(Clean(mf.name))) d[Clean(mf.name)] = mf.sharedMesh;
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(KitFbx).OfType<Mesh>())
                if (!d.ContainsKey(Clean(m.name))) d[Clean(m.name)] = m;
            return d;
        }

        static Mesh Lod1(string piece)
        {
            string p = $"{Lod1Dir}/{piece}_LOD1.fbx";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            var mf = go ? go.GetComponentInChildren<MeshFilter>(true) : null;
            return mf ? mf.sharedMesh : AssetDatabase.LoadAllAssetsAtPath(p).OfType<Mesh>().FirstOrDefault();
        }

        static string Clean(string n)
        {
            int k = n.IndexOf("__", System.StringComparison.Ordinal); if (k >= 0) n = n.Substring(0, k);
            k = n.IndexOf('.'); if (k >= 0) n = n.Substring(0, k);
            return n;
        }

        /// <summary>= OpenZSandbagsV2.KitTurn: the importer's turn about Y (0 or 180) measured on SB2_Seg_IC_v0's corner arm, never assumed.</summary>
        static int KitTurn(Mesh ic, out string why)
        {
            why = "";
            var v = ic.vertices; double ax = 0, az = 0; int n = 0;
            foreach (var p in v) if (Mathf.Abs(p.z) > 0.32f) { ax += p.x; az += p.z; n++; }
            if (n < 20) { why = $"SB2_Seg_IC_v0 has no corner arm ({n} arm vertices)"; return -1; }
            ax /= n; az /= n;
            why = $"IC arm at local ({ax:F2}, {az:F2})";
            if (ax < -0.3 && az > 0.2) return 0;
            if (ax > 0.3 && az < -0.2) return 180;
            why += ": a mirrored import (neither the Blender frame nor a 180 deg turn)";
            return -1;
        }

        static MeshRenderer Part(GameObject parent, string name, Mesh mesh, Material mat)
        {   // = OpenZSandbagsV2.Part
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; mr.receiveShadows = true;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            GameObjectUtility.SetStaticEditorFlags(go, 0);                                       // the stages swap at run time: never static-batched
            return mr;
        }

        static long Tris(Mesh m) { long t = 0; for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3; return t; }
    }
}
