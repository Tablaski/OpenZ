#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit level18_rules: the parity rows DV197 (the two river stumps), DV198 (the Mammoth and the Bizon in level 18's start set),
    /// DV199 (the computer and the big tanks) and DV200 (the optional shuttle on level 18), each measured by its part of the counted check
    /// (OzV15L18RulesCount) on level 18's own map. Every row is level 18's or a big tank's: the other levels, the bot matches and the parity's other
    /// probes are as before (the parity report's Zod rows and every other row's probe hold them off).</summary>
    public static partial class OpenZParity
    {
        static void AddL18RulesDeviations(List<Deviation> list)
        {
            string map = null;
            try { map = Resources.Load<TextAsset>("OpenZ/Maps/L18_grid")?.text; } catch (Exception) { }
            (string cell, bool pass) Probe(string part)
            {
                if (string.IsNullOrEmpty(map)) return ("no level 18 map (Resources/OpenZ/Maps/L18_grid)", false);
                try { string r; using (OzV11Kit.GameSwitches()) r = OzV15L18RulesCount.Run(SettingsText, map, part); return (OzV11Kit.VerdictCell(r, out bool p) + " (the whole check: menu OpenZ/Checks/V1.5/L18 rules (no Play))", p); }
                catch (Exception ex) { return ("the probe threw " + ex.GetType().Name + ": " + ex.Message, false); }
            }
            var a = Probe("DV197");
            list.Add(new Deviation
            {
                id = "DV197", change = "level 18: the two half-sunk skyscraper stumps (fixes_r5: moved to the map edge, view only; the row closes NO tile)",
                zod = "Z's level 18 has open shallow water in both pools (robots wade everywhere)",
                openz = "owner addition 51: the two stumps stand at the edge of the map (outer water by the west and east borders), 25 % smaller, as view-only scenery: the tile set is EMPTY (ZWorld.L18StumpTiles = {}), no tile is closed, the pools are open water as in Zod, every wade path as before the stumps; the switch stays (no effect)",
                measured = a.cell, toggle = "ZWorld.L18StumpsDeviation (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off); the view shows the stumps only while the rule has them (View/OzL18Rules.cs)",
                status = a.pass ? DevActive : DevCheck,
            });
            var b = Probe("DV198");
            list.Add(new Deviation
            {
                id = "DV198", change = "level 18: blue's medium tank (map tile 8, 20: the top-left, far from both forts) is an EMPTY MAMMOTH, the map's RIGHT bottom neutral crane (tile 83, 110: far from both forts) an EMPTY BIZON (placed on tile 78, 110: the red box, on the bottom road in front of the robot factory, along the road, off the crossing: kit l18_map_fixes_r3), that crane gone; no side starts with a crane",
                zod = "Z's level 18 start set: blue's medium tank crewed by a grunt (zvehicle.cpp:155-168); the neutral crane at (83, 110) (four neutral cranes in the file)",
                openz = "the same map entries made as the big tanks (DV180 / DV181) in the same place and id, EMPTY and neutral (anybody may take them; a psycho takes the seat), each replaced tank's driver (a grunt of its side) made after every other start object: red's by the red fort (tile 51, 88; red keeps its own crewed medium tank), blue's by the blue fort (both big tanks stand far away, really empty: the Mammoth in the top-left, blue_start_v3; the Bizon on the right bottom crane's spot, bizon_far_r3: the scene's crane there is swapped for the Bizon's model at the start); the level 18 head start (the player's) holds no crane, the map's neutral cranes are the ones to find; every other object as before; only while DV180 / DV181 are on",
                measured = b.cell, toggle = "ZWorld.L18BigTanksDeviation (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off); ZWorld.L18BigTanksEmpty (a constant switch, default true: false = the crewed variant, each its side's own tank with a psycho driver)",
                status = b.pass ? DevActive : DevCheck,
            });
            var c = Probe("DV199");
            list.Add(new Deviation
            {
                id = "DV199", change = "the computer and the big tanks: a computer's Mammoth or Bizon is sent only where it can drive now",
                zod = "Z's computer pairs a vehicle with a target by the regions of the map file (bridge tiles count as road, water closed): with the bridges down it sends a tank at targets it cannot reach and the tank stands at the bank; Z has no big tank",
                openz = "for a computer's MAMMOTH or BIZON (only) the region test of its order round, its repair pick (DV137) and its end-game round (DV60) asks the pathing layer's exact reachability on the tank's own grid as it is now (the Bizon wades, bridges as they stand): a target counts when a spot it can stand on lies within 2 tiles of a flag / repair entrance, the fort's half size + 2, or its range of a unit; everything else of the computer stays Zod's (it takes empty big tanks as any empty vehicle, never builds one, the ram and the crush run by themselves)",
                measured = c.cell, toggle = "ZWorld.BotBigTanksDeviation (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off)",
                status = c.pass ? DevActive : DevCheck,
            });
            var d = Probe("DV200");
            list.Add(new Deviation
            {
                id = "DV200", change = "level 18: the SHUTTLE box of the battlefield screen",
                zod = "Z's level 18 has no shuttle",
                openz = "EASY / MEDIUM with the box ON: the player's crewed shuttle by his fort (red: tile (" + ZWorld.L18RedFortShuttleSpot[0] + ", " + ZWorld.L18RedFortShuttleSpot[1] + "), blue: (" + ZWorld.L18BlueFortShuttleSpot[0] + ", " + ZWorld.L18BlueFortShuttleSpot[1] + ")), the level's playable shuttle with every shuttle rule; HARD, MAYHEM or OFF: none; the computer none",
                measured = d.cell, toggle = "ZWorld.L18ShuttleDeviation (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off) + ZWorld.L18StartShuttle (the box's answer, UI/OzShuttleStart.L18StartAtStart)",
                status = d.pass ? DevActive : DevCheck,
            });
        }
    }

    public static class OzV15L18RulesMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/L18 rules (no Play)")]
        static void Check()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] L18 rules: leave Play mode first (the check sets the simulation's static switches)"); return; }
            Debug.Log("[OpenZ V1.5] L18 rules (DV197-DV200): " + Report());
        }

        static string Report()
        {
            var st = Resources.Load<TextAsset>("OpenZ/default_settings"); var map = Resources.Load<TextAsset>("OpenZ/Maps/L18_grid");
            if (!st || !map) return "FAIL  no settings or no level 18 map in Resources";
            var night = Resources.Load<TextAsset>("OpenZ/Night/L18_night");   // the night posts: the parking keep-out discs ZGame sets on level 18
            string r; using (OzV11Kit.GameSwitches()) r = OzV15L18RulesCount.Run(st.text, map.text, "all", night ? night.text : null);
            int p = 0, f = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
            return $"{p} PASS, {f} FAIL\n{r}";
        }

        /// <summary>Batch (the kit's copy proof): -executeMethod OpenZ.EditorTools.OzV15L18RulesMenu.BatchProof: the counted check and the four parity
        /// rows into Logs/level18_rules_batch.txt; exit 0 = every line PASS and the four rows ACTIVE ("BATCH PROOF OK").</summary>
        public static void BatchProof()
        {
            int code = 1; var sb = new System.Text.StringBuilder();
            try
            {
                string r = Report(); sb.AppendLine(r);
                try { sb.AppendLine(DrawnSizes(true)); } catch (Exception ex) { sb.AppendLine("SIZE EXCEPTION " + ex.Message); }
                var rows = OpenZParity.L18RulesRowsForBatch();
                sb.AppendLine(rows.text);
                bool ok = r.Contains(" PASS, 0 FAIL") && !r.Contains("FAIL  ") && rows.active == 4;
                if (Environment.GetEnvironmentVariable("OZ_L18_SCENE_MOVE") == "1")
                {   // the kit's copy proof with a copy of P's level 18 scene: menu 12 on it (not saved), then the check must PASS
                    const string scenePath = "Assets/OpenZ/Scenes/L18_BridgeGame.unity";
                    if (!File.Exists(scenePath)) { sb.AppendLine("SCENE FAIL no " + scenePath + " in this project"); ok = false; }
                    else
                    {
                        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                        SceneStartVehicles(out bool before);
                        string mv = MoveSceneVehicles();
                        SceneStartVehicles(out bool after);
                        sb.AppendLine("SCENE before menu 12: " + (before ? "PASS" : "FAIL (expected: the scene still has the old places)"));
                        sb.AppendLine("SCENE menu 12: " + mv);
                        sb.AppendLine("SCENE after menu 12 (not saved): " + (after ? "PASS" : "FAIL"));
                        ok &= after;
                    }
                }
                sb.AppendLine(ok ? "BATCH PROOF OK" : "BATCH PROOF FAILED");
                code = ok ? 0 : 1;
            }
            catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex); }
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/level18_rules_batch.txt"), sb.ToString());
            Debug.Log("[OpenZ V1.5] L18 rules batch: " + sb);
            EditorApplication.Exit(code);
        }

        /// <summary>The types of level 18's start sets (the map's tanks, the cranes, the big tanks, the head start's light tank / jeep / crane).</summary>
        static readonly string[] SizeTypes = { "light", "medium", "heavy", ZBigTanks.Mammoth, ZBigTanks.Bizon, "crane", "jeep", "apc", "missile_launcher" };

        /// <summary>The drawn size of every start vehicle model the game can draw (the prefab ZGame would use, and its v1 / Resources twin),
        /// measured from the model itself: each prefab put at the origin unturned (a red unit's start turn; blue's is the same box turned half
        /// round), the union of its enabled mesh renderers (LOD 0 only) in metres. "hull" = every part but the turret / barrel / gun parts,
        /// "all" = the whole model. One line per prefab: SIZE type prefab all x0 x1 z0 z1 y1 hull x0 x1 z0 z1 (the table in
        /// Editor/OzV15L18RulesCount.cs DrawnHull is these numbers).</summary>
        public static string DrawnSizes(bool parts = false)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var type in SizeTypes)
            {
                string key = "Z_" + type;
                GameObject v1 = null;
                foreach (var g in AssetDatabase.FindAssets(key + " t:Prefab"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    if (Path.GetFileNameWithoutExtension(path) == key) { v1 = AssetDatabase.LoadAssetAtPath<GameObject>(path); break; }
                }
                var drawn = OzV2Units.PrefabFor(key, OzHeavyTank.PrefabFor(key, v1));
                var seen = new List<GameObject>();
                foreach (var pf in new[] { drawn, v1 })
                {
                    if (!pf || seen.Contains(pf)) continue; seen.Add(pf);
                    sb.AppendLine(MeasureLine(type, pf, pf == drawn ? "drawn" : "v1", parts));
                }
                if (seen.Count == 0) sb.AppendLine($"SIZE {type} none: no prefab {key}");
            }
            return sb.ToString();
        }

        static string MeasureLine(string type, GameObject prefab, string which, bool parts)
        {
            var go = (GameObject)UnityEngine.Object.Instantiate(prefab);
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var skip = new HashSet<Renderer>();
                foreach (var lg in go.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = lg.GetLODs();
                    for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r) skip.Add(r);
                }
                Bounds all = default, hull = default; bool a = false, h = false; int n = 0;
                var pl = new System.Text.StringBuilder();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || !r.gameObject.activeInHierarchy || skip.Contains(r)) continue;
                    var b = r.bounds; n++;
                    string path = r.transform.name; for (var p = r.transform.parent; p && p != go.transform; p = p.parent) path = p.name + "/" + path;
                    string low = path.ToLowerInvariant();
                    bool top = low.Contains("turret") || low.Contains("barrel") || low.Contains("cannon") || low.Contains("muzzle") || low.Contains("mantlet") || low.Contains("gun") || low.Contains("antenna") || low.Contains("launcher") || low.Contains("rocket");
                    if (!a) { all = b; a = true; } else all.Encapsulate(b);
                    if (!top) { if (!h) { hull = b; h = true; } else hull.Encapsulate(b); }
                    if (parts) pl.AppendLine($"  part {path}{(top ? " (top)" : "")}: x {b.min.x:0.00}..{b.max.x:0.00} z {b.min.z:0.00}..{b.max.z:0.00} y {b.min.y:0.00}..{b.max.y:0.00}");
                }
                if (!h) hull = all;
                return $"SIZE {type} {which} {prefab.name} renderers {n} all x {all.min.x:0.00} {all.max.x:0.00} z {all.min.z:0.00} {all.max.z:0.00} y1 {all.max.y:0.00} hull x {hull.min.x:0.00} {hull.max.x:0.00} z {hull.min.z:0.00} {hull.max.z:0.00}"
                       + (parts ? "\n" + pl.ToString().TrimEnd() : "");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>Menu 11 (any scene, no Play): the drawn sizes into the console (the parts too).</summary>
        [MenuItem("OpenZ/L18 Bridge Game/11 Start vehicle sizes (no Play)", false, 7711)]
        static void SizesMenu() => Debug.Log("[OpenZ] L18 start vehicle sizes (m, unturned):\n" + DrawnSizes(true));

        // ------------------------------------------------------------------ owner addition 7 point I: the scene's start vehicles follow the map file
        /// <summary>The map entries this kit moved in Resources/OpenZ/Maps/L18_grid.json: type, old tile, new tile (the scene names "type_x_y").</summary>
        public static readonly (string type, int ox, int oy, int nx, int ny)[] SceneMoves = { ("heavy", 53, 30, 53, 35), ("light", 53, 35, 70, 36), ("medium", 53, 30, 8, 20), ("medium", 56, 80, 53, 88) };
        /// <summary></summary>
        public static readonly (string type, int ox, int oy, int nx, int ny)[] SceneRobotMoves = { ("psycho", 56, 45, 48, 40), ("pyro", 66, 47, 55, 50), ("laser", 69, 45, 70, 50), ("sniper", 62, 32, 62, 30) };

        /// <summary>Menu 12 (the open level 18 scene, not in Play): each scene start tank the kit moved in the map file (ZodObjects/heavy_53_33,
        /// ZodObjects/medium_56_80) is renamed to its new map tile and moved there (Unity x = tx * 2 + 2, z = -(ty * 2 + 2): the middle of its 2 x 2
        /// tiles, as the level build placed every map vehicle), its turn and team kept, its height set on the ground under the new place (a ray down
        /// on the scene's colliders, the same height over the ground as before); then <see cref="SceneStartVehicles"/> lists every map vehicle
        /// against the scene. Without it the big tank gets a fresh view and the scene's old tank stays where it stood as a dead model (memory
        /// openz-map-change-needs-scene). Run once, look at the two places, save the scene.</summary>
        [MenuItem("OpenZ/L18 Bridge Game/12 Start vehicles: scene to the map (open scene)", false, 7712)]
        static void SceneMovesMenu() => Debug.Log("[OpenZ] L18 start vehicles, the scene to the map:\n" + MoveSceneVehicles());

        /// <summary>Menu 13 (the open level 18 scene): the check alone, nothing changed.</summary>
        [MenuItem("OpenZ/L18 Bridge Game/13 Start vehicles: check the scene (open scene)", false, 7713)]
        static void SceneCheckMenu() => Debug.Log("[OpenZ] L18 start vehicles in the scene: " + SceneStartVehicles(out _));

        /// <summary>Menu 16 (blue_start_v2, owner addition 43; the open level 18 scene, not in Play): every scene robot of each squad in
        /// <see cref="SceneRobotMoves"/> (ZodObjects "type_x_y" or "type_x_y_member") is renamed to the squad's new map tile and moved by the same
        /// tile offset (2 m a tile), its turn, team and order kept, the same height over the ground; a squad already moved is reported, never moved twice.</summary>
        [MenuItem("OpenZ/L18 Bridge Game/16 Start robots: blue squads to the map (open scene)", false, 7716)]
        static void SceneRobotMovesMenu() => Debug.Log("[OpenZ] L18 start robots, the scene to the map (blue_start_v2): " + System.Environment.NewLine + MoveSceneRobots());

        /// <summary>Menu 17 (bizon_far_r3, the owner's picture of 13 Nov; the open level 18 scene, not in Play; nothing changed): the EMPTY Bizon is made
        /// from the map's right bottom neutral crane (83, 110), so the scene needs NO new object: its crane ZodObjects/crane_83_110 is swapped for the
        /// Bizon's model at the start (ZGame.BindViews: OzL18Rules.SceneAlias, OzV2Units.SceneView: the same place, turn, scale and paint). This
        /// prints the scene crane's place / turn / paint, red's scene medium (ZodObjects/medium_53_88: red's own crewed medium again, red paint) and
        /// <see cref="SceneStartVehicles"/>. PASS = crane_83_110 on its tiles (Unity x 168, z -222) with neutral paint, medium_53_88 red.</summary>
        [MenuItem("OpenZ/L18 Bridge Game/17 Bizon on the right bottom crane: check the scene (open scene)", false, 7717)]
        static void SceneBizonCraneMenu() => Debug.Log("[OpenZ] L18 the Bizon on the right bottom crane (bizon_far_r3):\n" + SceneBizonCrane());

        public static string SceneBizonCrane()
        {
            if (Application.isPlaying) return "leave Play mode first";
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith(OzCity.BridgeGame)) return "open the level 18 scene first (" + scene.name + ")";
            var root = GameObject.Find("ZodObjects");
            if (!root) return "no ZodObjects in the scene";
            var sb = new System.Text.StringBuilder(); bool ok = true;
            int Paint(Transform t) { foreach (var mb in t.GetComponents<MonoBehaviour>()) if (mb && mb.GetType().Name == "TeamPaint") { var p = new SerializedObject(mb).FindProperty("team"); if (p != null) return p.intValue; } return -1; }
            var sp = ZWorld.L18BigTankSpots[1];
            var cr = root.transform.Find($"{sp.from}_{sp.tx}_{sp.ty}");
            var want = new Vector3(sp.tx * 2 + 2, 0, -(sp.ty * 2 + 2));
            if (!cr) { ok = false; sb.AppendLine($"  FAIL no ZodObjects/{sp.from}_{sp.tx}_{sp.ty}: the Bizon would get a fresh view (never invisible) but no scene place"); }
            else
            {
                var d = cr.position - want; d.y = 0; int tp = Paint(cr);
                bool good = d.magnitude <= 0.3f && tp == 0;
                ok &= good;
                sb.AppendLine($"  {(good ? "PASS" : "FAIL")} {cr.name}: ({cr.position.x:0.00}, {cr.position.y:0.00}, {cr.position.z:0.00}), {d.magnitude:0.00} m off its tiles, turn {cr.eulerAngles.y:0}, scale {cr.localScale.x:0.##}, TeamPaint {tp} (0 = neutral grey): at the start it is drawn as the EMPTY Bizon on this place and turn");
            }
            var md = root.transform.Find("medium_53_88");
            if (!md) { ok = false; sb.AppendLine("  FAIL no ZodObjects/medium_53_88 (red's own crewed medium)"); }
            else { int tp = Paint(md); bool good = tp == (int)Team.Red; ok &= good; sb.AppendLine($"  {(good ? "PASS" : "FAIL")} medium_53_88: ({md.position.x:0.00}, {md.position.z:0.00}), TeamPaint {tp} ({(int)Team.Red} = red): red's own crewed medium tank again, no longer swapped"); }
            sb.AppendLine("  " + SceneStartVehicles(out bool vok));
            ok &= vok;
            sb.Append(ok ? "PASS nothing to change in the scene: look at the bottom-right (x 168, z -222) and at the red fort, no save needed" : "FAIL see the lines above");
            return sb.ToString();
        }

        public static string MoveSceneRobots()
        {
            if (Application.isPlaying) return "leave Play mode first";
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith(OzCity.BridgeGame)) return "open the level 18 scene first (" + scene.name + ")";
            var root = GameObject.Find("ZodObjects");
            if (!root) return "no ZodObjects in the scene";
            var sb = new System.Text.StringBuilder(); int moved = 0, already = 0, fails = 0;
            foreach (var mv in SceneRobotMoves)
            {
                string from = $"{mv.type}_{mv.ox}_{mv.oy}", to = $"{mv.type}_{mv.nx}_{mv.ny}";
                var list = new List<Transform>(); int there = 0;
                foreach (Transform t in root.transform)
                {
                    if (t.name == from || t.name.StartsWith(from + "_")) list.Add(t);
                    else if (t.name == to || t.name.StartsWith(to + "_")) there++;
                }
                if (list.Count == 0) { if (there > 0) { already += there; sb.AppendLine($"  {to}: already moved ({there} robots)"); } else { fails++; sb.AppendLine($"  FAIL neither {from} nor {to} under ZodObjects"); } continue; }
                if (there > 0) { fails++; sb.AppendLine($"  FAIL both {from} ({list.Count}) and {to} ({there}) under ZodObjects: nothing changed"); continue; }
                var d = new Vector3((mv.nx - mv.ox) * 2f, 0f, -(mv.ny - mv.oy) * 2f);
                foreach (var t in list)
                {
                    Undo.RecordObject(t, "L18 start robot to the map"); Undo.RecordObject(t.gameObject, "L18 start robot to the map");
                    var p0 = t.position; float above = 0f;
                    if (Ground(p0, out float g0)) above = p0.y - g0;
                    var p1 = p0 + d;
                    if (Ground(p1, out float g1)) p1.y = g1 + above;
                    string n0 = t.name; t.position = p1; t.gameObject.name = to + n0.Substring(from.Length); moved++;
                    sb.AppendLine($"  {n0} -> {t.name}: ({p0.x:0.00}, {p0.y:0.00}, {p0.z:0.00}) -> ({p1.x:0.00}, {p1.y:0.00}, {p1.z:0.00})");
                }
            }
            if (moved > 0) EditorSceneManager.MarkSceneDirty(scene);
            sb.Append($"{(fails == 0 ? "PASS" : "FAIL")} {moved} robots moved, {already} already in place, {fails} fails{(moved > 0 ? ": save the scene" : "")}");
            return sb.ToString();
        }

        public static string MoveSceneVehicles()
        {
            if (Application.isPlaying) return "leave Play mode first";
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith(OzCity.BridgeGame)) return "open the level 18 scene first (" + scene.name + ")";
            var root = GameObject.Find("ZodObjects");
            if (!root) return "no ZodObjects in the scene";
            var sb = new System.Text.StringBuilder(); int moved = 0;
            foreach (var mv in SceneMoves)
            {
                string from = $"{mv.type}_{mv.ox}_{mv.oy}", to = $"{mv.type}_{mv.nx}_{mv.ny}";
                var t = root.transform.Find(from); var already = root.transform.Find(to);
                if (!t) { sb.AppendLine(already ? $"  {to}: already moved (no {from})" : $"  FAIL neither {from} nor {to} under ZodObjects"); continue; }
                if (already) { sb.AppendLine($"  FAIL both {from} and {to} under ZodObjects: nothing changed (take the stale one out by hand)"); continue; }
                Undo.RecordObject(t, "L18 start vehicle to the map"); Undo.RecordObject(t.gameObject, "L18 start vehicle to the map");
                var p0 = t.position; float above = 0f;
                if (Ground(p0, out float g0)) above = p0.y - g0;
                var p1 = new Vector3(mv.nx * 2 + 2, p0.y, -(mv.ny * 2 + 2));
                if (Ground(p1, out float g1)) p1.y = g1 + above;
                t.position = p1; t.gameObject.name = to; moved++;
                sb.AppendLine($"  {from} -> {to}: ({p0.x:0.00}, {p0.y:0.00}, {p0.z:0.00}) -> ({p1.x:0.00}, {p1.y:0.00}, {p1.z:0.00}), turn {t.eulerAngles.y:0}");
            }
            if (moved > 0) EditorSceneManager.MarkSceneDirty(scene);
            sb.AppendLine(SceneStartVehicles(out bool ok));
            sb.Append(moved > 0 ? $"{moved} moved: look at both places at the RTS camera, then save the scene" : "nothing moved");
            return sb.ToString();
        }

        static bool Ground(Vector3 at, out float y)
        {
            y = 0f; float best = float.MinValue; bool hit = false;
            foreach (var h in Physics.RaycastAll(new Vector3(at.x, at.y + 60f, at.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<UnitView>() || IsUnder(h.collider.transform, "ZodObjects")) continue;   // the ground, never a unit
                if (h.point.y > best) { best = h.point.y; hit = true; }
            }
            if (hit) y = best;
            return hit;
        }

        static bool IsUnder(Transform t, string name) { for (; t; t = t.parent) if (t.name == name) return true; return false; }

        /// <summary>Every vehicle of level 18's map file against the open scene: ZodObjects holds "type_x_y" at the middle of its 2 x 2 tiles (0.3 m),
        /// and no ZodObjects vehicle object is left whose name has no map entry (a stale old tank). ok = all held.</summary>
        public static string SceneStartVehicles(out bool ok)
        {
            ok = false;
            var root = GameObject.Find("ZodObjects"); var map = Resources.Load<TextAsset>("OpenZ/Maps/L18_grid");
            if (!root || !map) return "FAIL no ZodObjects or no level 18 map";
            var m = ZMapData.FromJson(map.text);
            var want = new HashSet<string>(); var sb = new System.Text.StringBuilder(); int good = 0, bad = 0;
            foreach (var o in m.objects)
            {
                if (o.Category != "vehicle") continue;
                string n = $"{o.Type}_{o.x}_{o.y}"; want.Add(n);
                var t = root.transform.Find(n);
                var c = new Vector3(o.x * 2 + 2, 0, -(o.y * 2 + 2));
                if (!t) { bad++; sb.Append($" MISSING {n};"); continue; }
                var d = t.position - c; d.y = 0;
                if (d.magnitude > 0.3f) { bad++; sb.Append($" {n} stands {d.magnitude:0.00} m off its tiles;"); continue; }
                good++;
            }
            string[] vehicleTypes = { "light", "medium", "heavy", "crane", "jeep", "apc", "missile_launcher", "mtank", ZBigTanks.Mammoth, ZBigTanks.Bizon };
            foreach (Transform t in root.transform)
            {
                var parts = t.name.Split('_'); if (parts.Length < 3) continue;
                string type = string.Join("_", parts, 0, parts.Length - 2);
                if (Array.IndexOf(vehicleTypes, type) >= 0 && int.TryParse(parts[parts.Length - 2], out _) && int.TryParse(parts[parts.Length - 1], out _) && !want.Contains(t.name)) { bad++; sb.Append($" STALE {t.name} (no map entry: a dead model);"); }
            }
            ok = bad == 0;
            return $"{(ok ? "PASS" : "FAIL")} {good} of {want.Count} map vehicles in place in the scene{(bad > 0 ? ";" + sb : "")}";
        }

        // ------------------------------------------------------------------ DV197: the two river stumps in the scene
        [Serializable] class Doc { public RiverRuins river_ruins; }
        [Serializable] class RiverRuins { public Item[] items; }
        [Serializable] class Item { public string id, piece; public int[] tile; public float tilt_deg, tilt_heading_deg, unity_yaw_deg, scale, px, pz; }   // fixes_r5: scale, px / pz (Unity m; 0 = from the tile)

        /// <summary>Menu 10: the two river stumps of kit level18_assets' placement file (river_ruins R1, R2) as static scene objects under
        /// Environment/L18_RiverStumps, each named "&lt;id&gt;_&lt;piece&gt;", on the water line over its pivot tile (Unity x = tx * 2 + 1, z = -(ty * 2 + 1)),
        /// with the piece's own LOD group and materials (the surroundings' placer, OzL18Assets). View only: View/OzL18Rules.cs shows each one only
        /// while the rules closed its tiles (DV197). Run it on the open level 18 scene, then save the scene.</summary>
        [MenuItem("OpenZ/L18 Bridge Game/10 River stumps (DV197, open scene)", false, 7710)]
        static void StumpsMenu() => Debug.Log("[OpenZ] L18 river stumps: " + Stumps());

        public static string Stumps()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith(OzCity.BridgeGame)) return "open the level 18 scene first (" + scene.name + ")";
            const string json = "Assets/OpenZ/Models/L18/Ruins/L18_ruins_placement.json";
            if (!File.Exists(json)) return "missing " + json;
            var doc = JsonUtility.FromJson<Doc>(File.ReadAllText(json));
            if (doc?.river_ruins?.items == null || doc.river_ruins.items.Length == 0) return "no river_ruins in " + json;
            var piece = typeof(OzL18Assets).GetMethod("Piece", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (piece == null) return "OzL18Assets.Piece is not there (kit level18_assets)";
            var env = GameObject.Find("Environment");
            if (!env) return "no Environment in the scene";
            var old = env.transform.Find(OzL18Rules.StumpsRoot); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(OzL18Rules.StumpsRoot).transform; root.SetParent(env.transform, false);
            float water = -0.8f; int n = 0, tris = 0; var why = new System.Text.StringBuilder();
            foreach (var it in doc.river_ruins.items)
            {
                if (it.tile == null || it.tile.Length < 2) { why.Append(it.id + " no tile; "); continue; }
                var host = new GameObject($"{it.id}_{it.piece}").transform; host.SetParent(root, false);
                var dir = new Vector3(Mathf.Cos(it.tilt_heading_deg * Mathf.Deg2Rad), 0, Mathf.Sin(it.tilt_heading_deg * Mathf.Deg2Rad));
                var tilt = Mathf.Abs(it.tilt_deg) > 0.01f ? Quaternion.AngleAxis(it.tilt_deg, Vector3.Cross(Vector3.up, dir)) : Quaternion.identity;
                host.SetPositionAndRotation(new Vector3(it.px != 0f ? it.px : it.tile[0] * 2 + 1, water, it.pz != 0f ? it.pz : -(it.tile[1] * 2 + 1)), tilt * Quaternion.Euler(0, it.unity_yaw_deg, 0));
                host.localScale = Vector3.one * (it.scale > 0f ? it.scale : 1f);
                var args = new object[] { it.piece, host, false, 0 };
                var p = piece.Invoke(null, args) as GameObject;
                if (!p) { why.Append(it.id + " model missing; "); UnityEngine.Object.DestroyImmediate(host.gameObject); continue; }
                tris += (int)args[3];
                p.transform.localPosition = Vector3.zero; p.transform.localRotation = Quaternion.identity; p.transform.localScale = Vector3.one;
                host.gameObject.isStatic = true; n++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            return $"{n} of {doc.river_ruins.items.Length} river stumps placed under Environment/{OzL18Rules.StumpsRoot} ({tris} triangles at the nearest level){(why.Length > 0 ? "; " + why : "")}; save the scene";
        }
    }

    public static partial class OpenZParity
    {
        /// <summary>The four rows alone (the kit's batch proof): their text and how many are ACTIVE.</summary>
        internal static (string text, int active) L18RulesRowsForBatch()
        {
            var list = new List<Deviation>();
            AddL18RulesDeviations(list);
            var sb = new System.Text.StringBuilder(); int act = 0;
            foreach (var d in list) { if (d.status == DevActive) act++; sb.AppendLine($"{d.id} row: {(d.status == DevActive ? "ACTIVE" : "CHECK")}; measured: {d.measured}"); }
            return (sb.ToString(), act);
        }
    }
}
#endif
