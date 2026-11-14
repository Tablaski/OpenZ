#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 16 counted Play check (UnitView.BeginWreck + TeamPaint.FortPaletteBurns + WreckPieces.MeetStatics): menu OpenZ/V1.3/Wrecks:
    /// For each fort (red and blue) the camera flies over it (normal RTS camera). A level gun standing on one of its four tower corners dies
    /// first (measured as a tower wreck too); then, round by round, each corner takes each of the four types (from the fort's store, as the
    /// player places one) until every type has died on every corner; the guns of a round die together. Last, the same four types on open
    /// ground near the first fort, as before. Frames per round: alive, +1.5 s (burning on the pads), +6 s, +15 s. Counts, per tower gun:
    ///  W1  burnt: the fort cream the shader gets (_OZFortPaint) at +1.5 s is at most 0.5 of the living gun's (before the fix 1.0);
    ///  W1p no white left: in a render of the wreck alone (its own drawn parts on a spare layer, same camera and lights, no smoke, fire or fort
    ///      in front) a "white" pixel is one at least <see cref="WhiteLumShare"/> as bright as the living gun's paint (its 75th luminance
    ///      percentile) and at most <see cref="WhiteSatMax"/> saturated; at most <see cref="WhiteShareMax"/> of the wreck's pixels may be white
    ///      at +1.5 s (on its pad) and at +6 s (its pieces, its parts, its turret); the same wreck drawn with today's look (FortPaletteBurns
    ///      off for that one frame) is counted beside it;
    ///  W2  falls apart: its base broke on its pad when its turret flew (pieces of its baked fragments made; the rule's counters up);
    ///  W3  pieces fly and land: every piece of it moved at least 0.3 m and lies at rest at +15 s, on the ground or held by the fort or a rock
    ///      (OzRockBody), none more than 0.35 m in the air without support; its turret lies on the ground when it has landed;
    ///  W4  nothing falls through: no piece below the ground, none under a solid surface of the fort (dropped through the tower top into it);
    ///  W5  every action has its sound (OzSoundLog, per round): the tosses and the barrel whooshes (dst_turret_toss), the breaks and the clanks
    ///      (dst_debris_metal), the fire (dst_fire_loop, or the fire loop already running), a heavy landing clang (turret), no clip missing;
    ///  C   coverage: each type died on each corner of each fort;
    ///  G   the four types on the GROUND as today: fort palette and burn switch off, the shader's _OZFortPaint the same alive and dead, the
    ///      rule's tower counters and WreckPieces.SmallMetStatics not moved by them, the base broke exactly when today's seeded one in two says.
    ///  * W1p: the "spare" layer was layer 31, the highest unnamed one, which is BuildingBreaker.DebrisLayer: fort, building and prop rubble,
    ///    falling palms and other cosmetic debris are drawn on it, so whatever of it moved between the empty render and the wreck's render
    ///    counted as wreck pixels (cream rubble = white); and the empty render was taken frames before the wreck's, while the full-screen fog
    ///    pass (OpenZ Atmosphere, on every camera of the renderer) drifts. Now a layer that nothing in the scene uses (no renderer, no collider;
    ///    never the debris, walk, poster or crater layer), the empty render made by a second camera IN THE SAME FRAME as the wreck's, a control
    ///    pair with nothing moved to the layer before each moment (it must differ by 0 px), both with the game camera's post (a bare render
    ///    clipped fire-lit and highlit faces to white), and a wreck too small on screen to count is said so;
    ///  * W3 turret: it read every entry of each mesh's vertex list; GunChairDebris cuts the seat kit off the turret mesh by its triangles and
    ///    keeps the whole vertex list (the howitzer's cradle keeps its 20 dropped collar triangles' corners too), so the seat's unused corners
    ///    counted as a turret 0.3-0.7 m in the ground. Now the turret is read as it is drawn (the vertices its triangles use, as OzHeavyLanding
    ///    fits it; its renderers under the thrown turret, shown and active), 0.4 s and 1.2 s after its DRAWN landing (OzHeavyLanding put on it),
    ///    with the rules' blast time, the ground, terrain, crater dent and water there, and the old reading, printed beside;
    ///  * C: a team whose only units are its tower guns loses its fort the moment the last one dies (ZWorld.CheckNoUnitsDestroyFort, Zod), so
    ///    a cleared level ran one round a fort. Now such a team gets one held grunt (a keeper) first, and the line says why a fort's rounds ended;
    ///  * W4: a piece in water (the water landing rule lets it go under) or melting in lava is counted as such, not as fallen through;
    ///  * W5: the turret clang is played only on screen (UnitView.TossLanded): asked only when a turret landed on screen;
    ///  * the report and frames go to one folder per level (the runs overwrote each other).
    /// The units it placed, and the level's own tower guns, are destroyed by the check. Output: <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzTowerWreckCheck : MonoBehaviour
    {
        public static string Report = "idle";
        public static string Folder => Path.Combine(OzRelease.Milestones("v13_item16_tower_gun_wrecks"), SceneManager.GetActiveScene().name);
        public static void Run() { Report = "running"; new GameObject("OzTowerWreckCheck").AddComponent<OzTowerWreckCheck>(); }
        public static readonly string[] Types = { "howitzer", "gun", "gatling", "missile_cannon" };
        static readonly (int lx, int ly)[] Corners = { (16, 0), (112, 0), (16, 48), (112, 48) };   // Zod's fort turret spots (Sim/ZWorld.Rules.cs CannonNotPlacable)
        public static float WhiteLumShare = 0.6f, WhiteSatMax = 0.35f, WhiteShareMax = 0.02f;
        /// <summary>A wreck with fewer pixels than this in its alive render is too small on screen to count white pixels on.</summary>
        public static int MinPixels = 400;
        /// <summary>W3 turret: its lowest drawn point lies at most SunkMax under the ground (the landing puts it 0.05 m in) and at most AirMax over it.</summary>
        public static float SunkMax = 0.25f, AirMax = 0.6f;
        static readonly int FortPaintId = Shader.PropertyToID("_OZFortPaint");
        readonly StringBuilder sb = new StringBuilder(); int pass, fail;
        void Line(string t) { sb.AppendLine(t); Debug.Log("[OzTowerWreckCheck] " + t); }
        void Verdict(bool ok, string t) { if (ok) pass++; else fail++; Line((ok ? "PASS  " : "FAIL  ") + t); }
        static string F(float v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        static string G(float v) => float.IsNaN(v) ? "-" : float.IsInfinity(v) ? "(nothing of it drawn)" : v.ToString("+0.00;-0.00", System.Globalization.CultureInfo.InvariantCulture);

        sealed class Unit
        {
            public ZObject o; public UnitView v; public string type, tag; public bool ground, expectBreak, fireLoop;
            public readonly List<Renderer> rs = new List<Renderer>();
            public Transform turretRoot;
            public float creamAlive, creamDead, lumRef = 1f, landT = -1f, tossT = -1f, visLandT = -1f;
            public float gapLand = float.NaN, gapSettled = float.NaN, gapBlast = float.NaN, settledY = float.NaN, endY = float.NaN;
            public string landNote = "-", settledNote = "-", blastNote = "-";
            public bool landOnScreen, inLava, inWater; public Vector4[] fpAlive, fpDead; public ZMissile debris;
            public int pxAlive, whiteAlive, px15, white15, pxToday, whiteToday, px6, white6, baked, maskDirty;
            public readonly Dictionary<WreckPieces, (Vector3 first, float moved)> pieces = new Dictionary<WreckPieces, (Vector3, float)>();
        }

        ZGame g; ZWorld W; Camera maskCam, emptyCam; RenderTexture maskRt, emptyRt; Texture2D maskTex; int maskLayer = -1; string maskWhy = "-"; Color32[] empty, img;
        readonly List<ZObject> fortList = new List<ZObject>();
        static readonly Dictionary<Mesh, Vector3[]> usedCache = new Dictionary<Mesh, Vector3[]>();

        /// <summary>The fort whose footprint (its sim rectangle, 0.5 m in from each side; world x = px / 8, z = -py / 8) holds `at`, or null.</summary>
        ZObject FortOver(Vector3 at)
        {
            float px = at.x * 8f, py = -at.z * 8f;
            foreach (var f in fortList)
                if (px >= f.LocX + 4 && px <= f.LocX + f.WidthPix - 4 && py >= f.LocY + 4 && py <= f.LocY + f.HeightPix - 4) return f;
            return null;
        }

        IEnumerator Start()
        {
            g = ZGame.I; W = g ? g.World : null;
            if (W == null) { Report = "no game"; Destroy(gameObject); yield break; }
            Directory.CreateDirectory(Folder);
            usedCache.Clear();
            var rts = FindFirstObjectByType<RtsCamera>(); var focus0 = rts ? rts.focus : Vector3.zero;
            float speed0 = g.gameSpeed; g.gameSpeed = 1f;
            Line($"level {SceneManager.GetActiveScene().name}, game speed {speed0:0.##} (1 during the check), camera height {(rts ? rts.height.ToString("0") : "-")} m");
            MaskSetup();
            Line(maskLayer < 0 ? "no spare layer or camera for the wreck renders: W1p fails" : $"wreck renders on {maskWhy}, {maskRt.width} x {maskRt.height}");
            var forts = W.Objects.Where(o => o.IsFort && !o.Destroyed && o.Owner != Team.Neutral).ToList();
            fortList.Clear(); fortList.AddRange(W.Objects.Where(o => o.IsFort));
            Keepers(forts);
            foreach (var fort in forts)
            {
                string ftag = fort.Owner + " fort #" + fort.Id;
                if (rts) rts.FlyTo(g.CenterWorld(fort));
                yield return new WaitForSecondsRealtime(2f);
                var need = new HashSet<(int, string)>(); for (int c = 0; c < 4; c++) foreach (var t in Types) need.Add((c, t));
                string ended = "every type died on every corner";
                for (int round = 0; need.Count > 0; round++)
                {
                    if (round >= 7) { ended = "7 rounds were not enough"; break; }
                    if (fort.Destroyed) { ended = $"the fort was destroyed before round {round} ({(TeamHasOtherUnit(fort.Owner) ? "it still had other units" : "its team had no unit left but its tower guns: ZWorld.CheckNoUnitsDestroyFort")})"; break; }
                    var units = new List<Unit>();
                    for (int c = 0; c < 4; c++)
                    {
                        int tx = (fort.LocX + Corners[c].lx) / 16, ty = (fort.LocY + Corners[c].ly) / 16;
                        ZObject gun = W.Objects.FirstOrDefault(o => o.Kind == ZKind.Cannon && !o.Destroyed && o.LocX / 16 == tx && o.LocY / 16 == ty);
                        string why = "level gun";
                        if (gun == null)
                        {
                            string type = Types.FirstOrDefault(t => need.Contains((c, t))); if (type == null) continue;
                            for (float w = 0f; w < 12f && !W.CannonPlacable(fort, tx, ty); w += 0.5f) yield return new WaitForSeconds(0.5f);   // the last round's wreck leaves the rules
                            fort.StoredCannons.Add(type);
                            gun = W.PlaceCannon(fort.Owner, fort, tx, ty, fort.StoredCannons.Count - 1);
                            if (gun == null) { fort.StoredCannons.RemoveAt(fort.StoredCannons.Count - 1); Line($"{ftag} corner {c}: {type} not placeable"); continue; }
                            why = "placed";
                        }
                        need.Remove((c, gun.Type));
                        units.Add(new Unit { o = gun, type = gun.Type, tag = $"{ftag} r{round} corner {c} {gun.Type} ({why})" });
                    }
                    if (units.Count == 0) { ended = $"no gun could be placed in round {round}"; break; }
                    yield return Round(units, $"{fort.Owner}_r{round}", false);
                }
                Verdict(need.Count == 0, $"C {ftag}: every type died on every tower corner (left: {(need.Count == 0 ? "none" : string.Join(", ", need.Select(n => n.Item2 + " @ corner " + n.Item1)))}); rounds ended: {ended}");
            }
            var first = forts.FirstOrDefault();
            if (first != null)
            {   // the four types on the ground, as today
                var gu = new List<Unit>(); int tx0 = first.CenterX / 16, ty0 = first.CenterY / 16 + 14;
                foreach (var type in Types)
                {
                    if (!FreeTile(tx0, ty0, out int tx, out int ty)) { Line("ground: no free tile near the fort"); break; }
                    gu.Add(new Unit { o = W.CreateObject("cannon", type, tx * 16, ty * 16, first.Owner), type = type, ground = true, tag = $"ground {type}" });
                    tx0 += 4;
                }
                if (gu.Count > 0)
                {
                    if (rts) rts.FlyTo(g.CenterWorld(gu[gu.Count / 2].o));
                    yield return new WaitForSecondsRealtime(2f);
                    yield return Round(gu, "ground", true);
                }
            }
            if (rts) rts.FlyTo(focus0);
            g.gameSpeed = speed0;
            if (maskCam) Destroy(maskCam.gameObject);
            if (emptyCam) Destroy(emptyCam.gameObject);
            if (maskRt) maskRt.Release();
            if (emptyRt) emptyRt.Release();
            Report = $"{pass} PASS, {fail} FAIL"; Line(Report);
            File.WriteAllText(Path.Combine(Folder, "report.txt"), sb.ToString());
            Destroy(gameObject);
        }

        bool TeamHasOtherUnit(Team team) => W.Objects.Any(o => o.Owner == team && !o.Destroyed && o.KillAt < 0 && (o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle) && !o.IsFortDefender);

        /// <summary>A team with no robot or vehicle loses its fort when its last unit dies (ZWorld.CheckNoUnitsDestroyFort; guns count as units):
        /// on a cleared level the first round's deaths took the fort and its later rounds never ran. Such a team gets one grunt, held, far
        /// from the corners, that the rules cannot kill (test only; it stays when the check ends).</summary>
        void Keepers(List<ZObject> forts)
        {
            foreach (var team in forts.Select(f => f.Owner).Distinct().ToList())
            {
                if (TeamHasOtherUnit(team)) continue;
                var f = forts.First(x => x.Owner == team);
                if (!FreeTile(f.CenterX / 16, f.CenterY / 16 - 16, out int kx, out int ky) && !FreeTile(f.CenterX / 16, f.CenterY / 16 + 20, out kx, out ky)) { Line($"keeper: {team} has only its guns and no free tile for a keeper: its fort will fall with its last gun"); continue; }
                var k = W.CreateRobotGroup("grunt", kx * 16 + 8, ky * 16 + 8, team, 1);
                if (k == null) { Line($"keeper: {team}: no grunt made"); continue; }
                k.MaxHealth = k.Health = 10000000; W.SendHold(team, k); g.SpawnUnitView(k);
                Line($"keeper: {team} had no robot or vehicle (only its guns): grunt #{k.Id} held at tile ({kx}, {ky}), so the rules do not take its fort when its last tower gun dies");
            }
        }

        /// <summary>One round: the units are drawn, measured alive, destroyed together in the rules, followed for 15 s, then counted.</summary>
        IEnumerator Round(List<Unit> units, string name, bool ground)
        {
            foreach (var u in units) u.v = g.SpawnUnitView(u.o);
            for (float w = 0f; w < 5f && !ground && units.Any(u => u.v && !u.v.OnTower); w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(1f);
            foreach (var u in units.ToList())
                if (!u.v || u.v.OnTower == ground) { Verdict(false, $"{u.tag}: not drawn {(ground ? "on the ground" : "on its tower")}"); units.Remove(u); }
            if (units.Count == 0) yield break;
            foreach (var u in units)
            {
                foreach (var r in u.v.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.GetComponentInParent<UnitView>(true) == u.v && Drawn(r, true)) u.rs.Add(r);   // not a gunner riding on it
                u.turretRoot = u.v.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("TurretYaw"));   // the one UnitView tosses (its first "TurretYaw")
                var frag = u.v.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "OZ_WreckFragments"); u.baked = frag ? frag.childCount : 0;
                u.creamAlive = CreamLum(u.v, out u.fpAlive);
                u.expectBreak = WreckFx.CannonBreak && WreckFx.Chance(RobotDeathRule.Hash(u.o.Id, 0, 11u) >> 5, 2u);   // today's seeded one in two (a gun on the ground)
            }
            yield return Measure(units, 0);
            Shot(name + "_a_alive");
            int burnt0 = WreckFx.TowerWrecksBurnt, broken0 = WreckFx.TowerWrecksBroken, small0 = WreckPieces.SmallMetStatics;
            float s0 = Time.unscaledTime, t0 = Time.time;
            foreach (var u in units) { u.o.SetHealth(0); W.UpdateObjectHealth(u.o); }
            yield return null;
            foreach (var u in units) u.debris = FindDebris(u.o.Id);
            bool shot15 = false, shot6 = false;
            while (Time.time - t0 < 15f)
            {
                float t = Time.time - t0;
                var all = FindObjectsByType<WreckPieces>(FindObjectsSortMode.None);
                foreach (var u in units) Track(u, all, t);
                if (!shot15 && t >= 1.5f)
                {
                    shot15 = true;
                    var ad = AudioDirector.I;
                    foreach (var u in units) { u.creamDead = CreamLum(u.v, out u.fpDead); u.fireLoop = ad && (ad.LoopActive("pyro_burn") || ad.LoopActive("wreck_fire_" + u.o.Id)); }   // OpenZPyroFire's burn loop key / UnitView's own
                    yield return Measure(units, 1); Shot(name + "_b_burning_1_5s");
                }
                if (!shot6 && t >= 6f) { shot6 = true; yield return Measure(units, 2); Shot(name + "_c_6s"); }
                yield return null;
            }
            foreach (var u in units) if (u.turretRoot) u.endY = u.turretRoot.position.y;
            Shot(name + "_d_15s");
            int towerN = units.Count(u => !u.ground);
            if (!ground)
            {
                Verdict(WreckFx.TowerWrecksBurnt - burnt0 == towerN && WreckFx.TowerWrecksBroken - broken0 == towerN, $"W2 {name}: the rule burnt {WreckFx.TowerWrecksBurnt - burnt0} and broke {WreckFx.TowerWrecksBroken - broken0} of {towerN} tower guns; small pieces made solid {WreckPieces.SmallMetStatics - small0}");
                Sounds(name, s0, towerN, units.Any(u => u.fireLoop), units.Count(u => u.landOnScreen));
            }
            else
                Verdict(WreckFx.TowerWrecksBurnt == burnt0 && WreckFx.TowerWrecksBroken == broken0 && WreckPieces.SmallMetStatics == small0,
                    $"G {name}: the ground guns moved no tower counter (burnt {WreckFx.TowerWrecksBurnt - burnt0}, broken {WreckFx.TowerWrecksBroken - broken0}, small pieces made solid {WreckPieces.SmallMetStatics - small0})");
            foreach (var u in units) yield return Count(u);
        }

        /// <summary>Every frame: where each of its pieces went; its turret thrown (it leaves the view), its drawn landing (OzHeavyLanding put on
        /// it on the landing frame, UnitView.UpdateToss), the rules' debris blast; the turret read 0.4 s and 1.2 s after its drawn landing
        /// (without one: water, lava or the landing switched off, after the rules' blast) and 0.4 s after the blast.</summary>
        void Track(Unit u, WreckPieces[] all, float t)
        {
            foreach (var p in all)
            {
                if (!p || p.UnitId != u.o.Id) continue;
                var at = p.transform.position;
                if (!u.pieces.TryGetValue(p, out var e)) u.pieces[p] = (at, 0f);
                else u.pieces[p] = (e.first, Mathf.Max(e.moved, Vector3.Distance(e.first, at)));
            }
            if (u.ground) return;
            var tr = u.turretRoot;
            if (tr && u.tossT < 0f && tr.parent == null) u.tossT = t;
            if (tr && u.visLandT < 0f && u.tossT >= 0f && tr.GetComponent<OzHeavyLanding>()) { u.visLandT = t; u.landOnScreen = WreckFx.OnScreen(tr.position); }
            if (u.landT < 0f && u.debris != null && !Listed(u.debris)) u.landT = t;
            float judge = u.visLandT >= 0f ? u.visLandT : (u.landT >= 0f && t >= u.landT + 1.5f ? u.landT : -1f);
            if (judge >= 0f && float.IsNaN(u.gapLand) && t >= judge + 0.4f) u.gapLand = TurretGap(u, out u.landNote);
            if (judge >= 0f && float.IsNaN(u.gapSettled) && t >= judge + 1.2f) { u.gapSettled = TurretGap(u, out u.settledNote); u.settledY = tr ? tr.position.y : float.NaN; }
            if (u.landT >= 0f && float.IsNaN(u.gapBlast) && t >= u.landT + 0.4f) u.gapBlast = TurretGap(u, out u.blastNote);
        }

        /// <summary>The thrown turret's lowest DRAWN point over the ground under it: its mesh renderers under the turret (shown, active, not
        /// a wreck piece, not a glow), each by the vertices its triangles use (OzHeavyLanding fits the same ones, 0.05 m in). The old reading
        /// (every entry of each vertex list, unused ones too: GunChairDebris keeps the seat kit's corners in the turret mesh) is printed beside.</summary>
        float TurretGap(Unit u, out string note)
        {
            var root = u.turretRoot;
            if (!root) { note = "its turret is gone"; return float.PositiveInfinity; }
            if (OzLavaMelt.Busy(root)) { u.inLava = true; note = "melting in lava (the lava rule)"; return 0f; }
            float low = float.MaxValue, old = float.MaxValue; Vector3 at = default; string who = "-"; int n = 0, boxes = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.GetComponentInParent<WreckPieces>() || !Drawn(r, false)) continue;
                var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; if (!m) continue;
                var w = r.transform.localToWorldMatrix; n++;
                var pts = UsedVertices(m);
                if (pts == null) { boxes++; var b = r.bounds; if (b.min.y < low) { low = b.min.y; at = b.center; who = r.name + " (its box)"; } if (b.min.y < old) old = b.min.y; continue; }
                foreach (var v in pts) { var p = w.MultiplyPoint3x4(v); if (p.y < low) { low = p.y; at = p; who = r.name; } }
                foreach (var v in m.vertices) { float y = w.MultiplyPoint3x4(v).y; if (y < old) old = y; }
            }
            if (n == 0) { note = "nothing of it drawn"; return float.PositiveInfinity; }
            float gy = g.GroundY(at.x, at.z), ty = g.TerrainY(at.x, at.z), dent = CraterField.DentRender(at.x, at.z);
            bool water = OzWaterLanding.InWater(root) || OzWaterLanding.OpenWaterAt(at, out _);
            if (water) u.inWater = true;
            note = $"lowest drawn point on {who} at ({at.x:0.0}, {at.y:0.00}, {at.z:0.0}), ground {gy:0.00} (terrain {ty:0.00}, crater dent {dent:+0.00;-0.00}){(water ? ", in water" : "")}; every vertex-list entry (the old reading) {old - gy:+0.00;-0.00}{(boxes > 0 ? $"; {boxes} part(s) by box" : "")}";
            return low - gy;
        }

        /// <summary>A mesh's vertices that its index lists use (cached), null when it cannot be read.</summary>
        static Vector3[] UsedVertices(Mesh m)
        {
            if (usedCache.TryGetValue(m, out var c)) return c;
            Vector3[] res = null;
            if (m.isReadable)
            {
                var v = m.vertices; var seen = new HashSet<int>(); var list = new List<Vector3>();
                for (int s = 0; s < m.subMeshCount; s++)
                    foreach (int i in m.GetIndices(s))
                        if (i >= 0 && i < v.Length && seen.Add(i)) list.Add(v[i]);
                res = list.ToArray();
            }
            usedCache[m] = res;
            return res;
        }

        /// <summary>Per unit, per moment (0 alive, 1 +1.5 s, 2 +6 s): its wreck drawn alone, its pixels and its white pixels.</summary>
        IEnumerator Measure(List<Unit> units, int when)
        {
            if (maskLayer < 0) yield break;
            yield return MaskShot(null);
            Stats(2f, out int dirty, out _, null);                                                    // the control pair: nothing on the layer, so nothing may differ
            if (dirty > 0) Line($"the render layer {maskLayer} is not empty at moment {when}: {dirty} px of other objects (those wrecks are not measured)");
            foreach (var u in units)
            {
                u.maskDirty = Mathf.Max(u.maskDirty, dirty);
                var set = WreckSet(u);
                yield return MaskShot(set);
                if (when == 0)
                {
                    var lums = new List<float>(); Stats(2f, out u.pxAlive, out _, lums);
                    lums.Sort(); u.lumRef = lums.Count > 0 ? lums[(int)(lums.Count * 0.75f)] : 1f;
                    Stats(WhiteLumShare * u.lumRef, out _, out u.whiteAlive, null);
                }
                else if (when == 1)
                {
                    Stats(WhiteLumShare * u.lumRef, out u.px15, out u.white15, null);
                    var tp = u.v ? u.v.GetComponent<TeamPaint>() : null;
                    if (tp && tp.FortPaletteBurns)
                    {   // today's look of the same wreck at the same moment, for one frame
                        tp.FortPaletteBurns = false;
                        yield return MaskShot(set);
                        Stats(WhiteLumShare * u.lumRef, out u.pxToday, out u.whiteToday, null);
                        tp.FortPaletteBurns = true;
                    }
                    else { u.pxToday = u.px15; u.whiteToday = u.white15; }
                }
                else Stats(WhiteLumShare * u.lumRef, out u.px6, out u.white6, null);
            }
        }

        /// <summary>Everything drawn of the wreck now: its drawn parts of the start (base, turret, the base fragments once shown), the
        /// renderers under its view, and every wreck piece made from it (the gun's parts, the seat kit, the base pieces).</summary>
        List<Renderer> WreckSet(Unit u)
        {
            var set = new HashSet<Renderer>();
            foreach (var r in u.rs) if (Drawn(r, false)) set.Add(r);
            if (u.v) foreach (var r in u.v.GetComponentsInChildren<MeshRenderer>()) if (r.GetComponentInParent<UnitView>() == u.v && Drawn(r, false)) set.Add(r);
            foreach (var p in u.pieces.Keys) if (p) foreach (var r in p.GetComponentsInChildren<MeshRenderer>()) if (Drawn(r, false)) set.Add(r);
            return set.ToList();
        }

        static bool Drawn(Renderer r, bool any)
        {
            if (!r || !(r is MeshRenderer) || (!any && (!r.enabled || !r.gameObject.activeInHierarchy))) return false;
            var n = r.name.ToLowerInvariant(); if (n.Contains("shadow") || n.Contains("label")) return false;
            var m = r.sharedMaterial; return !(m && m.renderQueue >= 2450);                                   // glows, lamps, decals, flames: not the wreck's paint
        }

        IEnumerator Count(Unit u)
        {
            float sh15 = u.px15 > 0 ? (float)u.white15 / u.px15 : 1f, shT = u.pxToday > 0 ? (float)u.whiteToday / u.pxToday : 0f;
            float sh6 = u.px6 > 0 ? (float)u.white6 / u.px6 : 1f, shA = u.pxAlive > 0 ? (float)u.whiteAlive / u.pxAlive : 0f;
            int basePieces = u.pieces.Keys.Count(p => p && p.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("frag_")));
            if (u.ground)
            {
                var tp = u.v ? u.v.GetComponent<TeamPaint>() : null;
                bool same = u.fpAlive != null && u.fpDead != null && u.fpDead.All(d => u.fpAlive.Any(a => (a - d).sqrMagnitude < 1e-10f));
                Verdict(tp && !tp.FortPalette && !tp.FortPaletteBurns && same && (basePieces > 0) == u.expectBreak,
                    $"G {u.tag}: fort palette off {(tp ? !tp.FortPalette : false)}, burn switch off {(tp ? !tp.FortPaletteBurns : false)}, _OZFortPaint alive = dead {same}, base broke {basePieces > 0} (today's seeded rule: {u.expectBreak}); white share alive {F(shA)} of {u.pxAlive} px, +1.5 s {F(sh15)} of {u.px15} px, +6 s {F(sh6)} of {u.px6} px (as today; the frames for the eye)");
                yield break;
            }
            Verdict(u.creamAlive > 0f && u.creamDead <= u.creamAlive * 0.5f, $"W1 {u.tag}: fort cream luminance alive {F(u.creamAlive)}, dead +1.5 s {F(u.creamDead)} (want at most 0.5 x; the burn steps give 0.49 at once, 0.44 from 1.3 s, 0.36 at the end)");
            bool measurable = maskLayer >= 0 && u.pxAlive >= MinPixels && u.maskDirty == 0;
            Verdict(measurable && u.px15 > 0 && sh15 <= WhiteShareMax && sh6 <= WhiteShareMax,
                $"W1p {u.tag}: white pixels of the wreck +1.5 s {u.white15} of {u.px15} ({F(sh15)}; today's look {u.whiteToday} of {u.pxToday}, {F(shT)}), +6 s {u.white6} of {u.px6} ({F(sh6)}); alive {u.whiteAlive} of {u.pxAlive} (want at most {F(WhiteShareMax)})" +
                (u.pxAlive < MinPixels ? $"; too small on screen to count (alive {u.pxAlive} px, want {MinPixels}+): not measured, frame the fort closer" : "") +
                (u.maskDirty > 0 ? $"; the render layer was not empty ({u.maskDirty} px of other objects): not measured" : ""));
            Verdict(basePieces > 0, $"W2 {u.tag}: base pieces {basePieces} (baked fragments {u.baked}), all its pieces {u.pieces.Count}");
            int moved = 0, still = 0, floating = 0, sunk = 0, under = 0, sinking = 0, wet = 0, lava = 0; var notes = new List<string>(); var rest = new List<(WreckPieces p, Bounds b)>();
            foreach (var kv in u.pieces)
            {
                var p = kv.Key; if (!p) continue;
                if (p.Fading) { sinking++; continue; }                                                     // its life is over: it sinks by the wreck rules
                if (OzLavaMelt.Busy(p.transform)) { lava++; continue; }                                    // level 7: it melts into the lava (kit LV5)
                if (kv.Value.moved >= 0.3f) moved++; else notes.Add("did not fly: " + p.name);
                var rb = p.GetComponent<Rigidbody>(); var rs = p.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float gy = g.GroundY(b.center.x, b.center.z);
                if (!(rb && (rb.isKinematic || rb.IsSleeping()))) { still++; notes.Add($"still moving at {b.center:F1}"); continue; }
                if (b.max.y < gy - 0.05f)
                {
                    if (OzWaterLanding.InWater(p.transform)) { wet++; continue; }                         // the water landing rule: it lies under the surface or went under
                    sunk++; notes.Add($"below the ground at {b.center:F1}, {gy - b.min.y:0.00} m in, {b.size.y:0.00} m tall, terrain {g.TerrainY(b.center.x, b.center.z):0.00}, crater dent {CraterField.DentRender(b.center.x, b.center.z):+0.00;-0.00}, on a rock {OzRockBody.RestsOnRock(rb)}, kinematic {rb && rb.isKinematic}");
                    continue;
                }
                if (b.min.y - gy > 0.35f && !OzRockBody.RestsOnRock(rb)) { floating++; notes.Add($"in the air {b.min.y - gy:0.00} m at {b.center:F1}"); }
                rest.Add((p, b)); OzStaticSolid.ArmAround(b.center, 3f, 1f, 3f);
            }
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            var underNotes = new List<string>();
            foreach (var (p, b) in rest)
            {
                // (before: any rock-layer collider above counted, so a piece beside a rock overhang, a level-16 ruin or the roof gun was a false FAIL)
                if (!p) continue;
                var fo = FortOver(b.center); if (fo == null) continue;
                if (b.min.y - g.GroundY(b.center.x, b.center.z) > 1.5f) continue;                       // on the fort's roof or a tower top: not fallen through
                var froot = g.StaticViews.TryGetValue(fo.Id, out var fv) && fv ? fv.transform : null;
                foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 40f, b.center.z), Vector3.down, 39.9f, OzRockSolid.Mask, QueryTriggerInteraction.Ignore))
                    if (OzRockSolid.IsRock(h.collider) && !h.collider.transform.IsChildOf(p.transform) && (!froot || h.collider.transform.IsChildOf(froot)))
                    { under++; underNotes.Add($"under {h.collider.name} of fort #{fo.Id} at {b.center:F1}"); break; }
            }
            int live = u.pieces.Keys.Count(p => p && !p.Fading && !OzLavaMelt.Busy(p.transform));
            Verdict(live + sinking + lava > 0 && moved == live && still == 0 && floating == 0,
                $"W3 {u.tag}: pieces flew {moved} of {live} (and {sinking} already sinking by the wreck rules{(lava > 0 ? $", {lava} melting in lava" : "")}), at rest {live - still}, in the air without support {floating}{(notes.Count > 0 ? " (" + string.Join("; ", notes.Take(6)) + ")" : "")}");
            string landing = u.visLandT >= 0f ? $"landed (drawn) {F(u.visLandT)} s after the death, the rules' blast at {(u.landT >= 0f ? F(u.landT) : "-")} s, thrown at {(u.tossT >= 0f ? F(u.tossT) : "-")} s{(u.landOnScreen ? "" : ", off screen")}"
                        : u.landT >= 0f ? $"no drawn landing seen (no OzHeavyLanding on it{(u.inWater ? ": water" : "")}), judged after the rules' blast at {F(u.landT)} s" : "not seen landing";
            float sankAfter = !float.IsNaN(u.settledY) && !float.IsNaN(u.endY) ? u.settledY - u.endY : 0f;
            bool lies(float gap) => !float.IsNaN(gap) && !float.IsInfinity(gap) && gap >= -SunkMax && gap <= AirMax;
            bool turretOk = u.inLava || (u.inWater && !float.IsNaN(u.gapSettled)) || (lies(u.gapLand) && lies(u.gapSettled));
            Verdict(turretOk, $"W3 {u.tag}: its turret {landing}; its lowest drawn point +0.4 s {G(u.gapLand)} m over the ground, settled (+1.2 s) {G(u.gapSettled)} m (want -{SunkMax:0.00} .. +{AirMax:0.00}; the landing lays it 0.05 m in){(u.inLava ? "; in lava: it melts by the lava rule" : "")}{(u.inWater ? "; in water: the water landing rule" : "")}{(sankAfter > 0.1f ? $"; it sank {sankAfter:0.00} m by +15 s after it settled (the wreck fade)" : "")}; at the rules' blast +0.4 s {G(u.gapBlast)} m" +
                $" | +0.4 s: {u.landNote} | settled: {u.settledNote}");
            Verdict(sunk == 0 && under == 0, $"W4 {u.tag}: pieces below the ground {sunk}, at ground level inside the fort under its own parts (fell through the tower top into it) {under}{(wet > 0 ? $"; in water (the water landing rule) {wet}" : "")}{(lava > 0 ? $"; melting in lava {lava}" : "")}{(underNotes.Count > 0 ? " (" + string.Join("; ", underNotes.Take(6)) + ")" : "")}");
        }

        void Sounds(string name, float s0, int n, bool fireRunning, int landedOnScreen)
        {
            var es = OzSoundLog.Since(s0);
            bool NoClip(OzSoundLog.Entry e) => e.outcome == "DROP no event" || e.outcome == "DROP no clips loaded" || e.outcome == "DROP no clip names" || e.outcome == "DROP clip not loaded";
            int Asked(string key, string kind = null) => es.Count(e => e.key == key && !NoClip(e) && (kind == null || (e.outcome != null && e.outcome.StartsWith(kind))));
            var keys = new[] { "dst_turret_toss", "dst_debris_metal", "dst_fire_loop", "turret" };
            var missing = es.Where(e => keys.Contains(e.key) && NoClip(e)).Select(e => e.key).Distinct().ToList();
            int toss = Asked("dst_turret_toss"), metal = Asked("dst_debris_metal"), fire = Asked("dst_fire_loop", "LOOP"), clang = Asked("turret");
            Verdict(missing.Count == 0 && toss >= n && metal >= n && (fire >= 1 || fireRunning) && (clang >= 1 || landedOnScreen == 0),
                $"W5 {name}: sounds asked: turret toss / barrel whoosh {toss} (want {n}+), break snap and clanks {metal} (want {n}+), fire loop starts {fire} (running at +1.5 s {fireRunning}), heavy landing clang {clang} (want 1+ when a turret lands on screen: {landedOnScreen} did; UnitView.TossLanded plays it on screen only), clips missing {(missing.Count == 0 ? "none" : string.Join(", ", missing))}");
        }

        /// <summary>Mean luminance of the fort palette colour the shader gets on the view's own renderers (0 = none set), and each vector.</summary>
        static float CreamLum(UnitView v, out Vector4[] each)
        {
            var list = new List<Vector4>(); each = list.ToArray();
            if (!v) return 0f;
            var block = new MaterialPropertyBlock(); float sum = 0f; int n = 0;
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponentInParent<UnitView>() != v) continue;
                r.GetPropertyBlock(block, 0);
                var c = block.GetVector(FortPaintId); list.Add(c); if (c.w <= 0.001f) continue;
                sum += 0.2126f * c.x + 0.7152f * c.y + 0.0722f * c.z; n++;
            }
            each = list.ToArray();
            return n > 0 ? sum / n : 0f;
        }

        ZMissile FindDebris(int id)
        {
            foreach (var x in W.PendingMissiles) if (x.debris && x.attackerId == id) return x;
            foreach (var x in W.QueuedMissiles) if (x.debris && x.attackerId == id) return x;
            return null;
        }
        bool Listed(ZMissile m)
        {
            foreach (var x in W.PendingMissiles) if (x == m) return true;
            foreach (var x in W.QueuedMissiles) if (x == m) return true;
            return false;
        }

        // ------------------------------------------------------------------ the wreck drawn alone (a second camera, a layer nothing else uses)
        void MaskSetup()
        {
            var main = Camera.main; if (!main) return;
            maskLayer = SpareLayer(out maskWhy);
            if (maskLayer < 0) return;
            int w = Mathf.Max(64, main.pixelWidth), h = Mathf.Max(64, main.pixelHeight);
            maskRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); emptyRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            maskTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            empty = new Color32[w * h]; img = new Color32[w * h];
            maskCam = new GameObject("OzTowerWreckCheck_render").AddComponent<Camera>(); maskCam.enabled = false;
            emptyCam = new GameObject("OzTowerWreckCheck_render_empty").AddComponent<Camera>(); emptyCam.enabled = false;
            var md = main.GetUniversalAdditionalCameraData();
            foreach (var cam in new[] { maskCam, emptyCam })
            {   // the game camera's look (its post: tone mapping, grading), so "white" means white as seen; before, the bare render clipped
                // a fire-lit or highlit face to white that the game draws orange or grey. No anti-aliasing or dithering (both renders alike)
                var d = cam.GetUniversalAdditionalCameraData();
                d.renderPostProcessing = md.renderPostProcessing; d.volumeLayerMask = md.volumeLayerMask; d.volumeTrigger = md.volumeTrigger;
                d.renderShadows = md.renderShadows; d.antialiasing = AntialiasingMode.None; d.dithering = false; d.stopNaN = true;
            }
            maskWhy += md.renderPostProcessing ? ", with the game camera's post" : ", no post (the game camera has none)";
        }

        /// <summary>An unnamed layer with no renderer and no collider on it anywhere in the scene, and none the game takes for itself at run
        /// time: BuildingBreaker.DebrisLayer (31: rubble, palms, props, cans are drawn on it), ZGame.WalkLayer (30), OzFactionPosters and
        /// CraterField's isolation layer (27, 26). -1 = none.</summary>
        static int SpareLayer(out string why)
        {
            var used = new HashSet<int>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (r) used.Add(r.gameObject.layer);
            foreach (var c in FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c) used.Add(c.gameObject.layer);
            int debris = BuildingBreaker.DebrisLayer, walk = ZGame.WalkLayer;
            for (int i = 25; i >= 8; i--)
            {
                if (!string.IsNullOrEmpty(LayerMask.LayerToName(i)) || i == debris || i == walk || used.Contains(i)) continue;
                why = $"layer {i} (unnamed, nothing in the scene on it; the debris layer is {debris}, the walk layer {walk})";
                return i;
            }
            why = "-"; return -1;
        }

        /// <summary>One frame, two cameras at the game camera's place: `img` = the spare layer with `set` moved onto it (null = nothing),
        /// `empty` = no layer at all; both carry the same full-screen passes of that frame (the fog), so they differ only where the set is drawn.</summary>
        IEnumerator MaskShot(List<Renderer> set)
        {
            var main = Camera.main; if (!main || !maskCam || !emptyCam) yield break;
            foreach (var (cam, rt, mask) in new[] { (maskCam, maskRt, 1 << maskLayer), (emptyCam, emptyRt, 0) })
            {
                cam.CopyFrom(main); cam.cullingMask = mask; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f); cam.targetTexture = rt;
            }
            var keep = new List<(GameObject go, int layer)>();
            if (set != null) foreach (var r in set) if (r) { keep.Add((r.gameObject, r.gameObject.layer)); r.gameObject.layer = maskLayer; }
            maskCam.enabled = true; emptyCam.enabled = true;
            yield return new WaitForEndOfFrame();
            var was = RenderTexture.active;
            foreach (var (rt, into) in new[] { (maskRt, img), (emptyRt, empty) })
            {
                RenderTexture.active = rt;
                maskTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false); maskTex.Apply(false);
                maskTex.GetPixelData<Color32>(0).CopyTo(into);
            }
            RenderTexture.active = was; maskCam.enabled = false; emptyCam.enabled = false;
            foreach (var (go, layer) in keep) if (go) go.layer = layer;
        }

        /// <summary>The wreck's pixels (those that differ from the empty render) and its white ones (at least `whiteLum`, at most WhiteSatMax saturated).</summary>
        void Stats(float whiteLum, out int px, out int white, List<float> lums)
        {
            px = 0; white = 0;
            for (int i = 0; i < img.Length; i++)
            {
                var a = img[i]; var e = empty[i];
                if (System.Math.Abs(a.r - e.r) + System.Math.Abs(a.g - e.g) + System.Math.Abs(a.b - e.b) <= 12) continue;
                px++;
                float r = a.r / 255f, gg = a.g / 255f, b = a.b / 255f, lum = 0.2126f * Mathf.GammaToLinearSpace(r) + 0.7152f * Mathf.GammaToLinearSpace(gg) + 0.0722f * Mathf.GammaToLinearSpace(b);
                float mx = Mathf.Max(r, Mathf.Max(gg, b)), mn = Mathf.Min(r, Mathf.Min(gg, b)), sat = mx > 1e-3f ? (mx - mn) / mx : 0f;
                lums?.Add(lum);
                if (lum >= whiteLum && sat <= WhiteSatMax) white++;
            }
        }

        bool FreeTile(int tx0, int ty0, out int tx, out int ty)
        {
            for (int r = 0; r < 14; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        tx = tx0 + dx; ty = ty0 + dy;
                        if (tx < 2 || ty < 2 || tx > W.Map.width - 4 || ty > W.Map.height - 4) continue;
                        bool ok = true;
                        for (int j = 0; j < 2 && ok; j++) for (int i = 0; i < 2 && ok; i++) ok = W.Path.TilePassable(tx + i, ty + j, false);
                        if (!ok) continue;
                        foreach (var o in W.Objects) if (!o.Destroyed && System.Math.Abs((o.CenterX >> 4) - tx) < 3 && System.Math.Abs((o.CenterY >> 4) - ty) < 3) { ok = false; break; }
                        if (ok) return true;
                    }
            tx = ty = 0; return false;
        }

        void Shot(string name) { var p = Path.Combine(Folder, name + ".png"); ScreenCapture.CaptureScreenshot(p); Line("frame " + p); }
    }
}
#endif
