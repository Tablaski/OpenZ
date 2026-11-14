#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// normal RTS camera, hands off the keyboard and the mouse (the check presses its own keys and moves its own mouse: two test devices made current
    /// for its run), about 5 minutes; it ends the match (its last row), so run it on a level that may be lost to a test. For every type (six robots, five vehicles, two guns) a new own unit at a free place near the camera, selected alone:
    ///  enter  Alt+F: the view is on, the camera at the eye (height over the ground in the type's band), every model renderer of the unit held off and
    ///         none of it drawn, a copy of its gun drawn and its line within 12 deg of the view's centre, the HUD hidden, the cursor locked and hidden,
    ///         field of view and near clip the view's;
    ///  look   400 px of mouse to the right = 60 deg (the free look's rate), 2000 px up = clamped at +60, 5000 px down = clamped at -60;
    ///  walk   W held 2 s (vehicles 3 s: they turn onto the line first): move orders sent (fix round 3: 1 .. 1 + walked px / 24), the unit moved at least 1 m the way the view looks, the RTS camera's
    ///         focus on the unit (it follows the unit, never scrolled by the keys) and the bound scroll key read as not held; released: one stop order, the unit's waypoints empty within 2 s; D held
    ///         1.5 s (vehicles 3 s): it moves at least 0.4 m to the view's right; a gun: no order, it stays;
    ///  leave  Esc (the pause menu stays shut): the view off, the RTS camera back on the unit with the old height, tilt, field of view and near clip, the
    ///         cursor free and shown, the HUD and the selection rings as before, every held-off renderer given back, the gun copy gone, and W scrolls
    ///         the RTS camera again.
    /// Then the other leave paths, each on a new unit: Alt+F again, the unit dies, the pause menu opens (the HUD stays hidden under it and comes back
    /// when it closes), a grunt gets into an own APC, a light tank's driver gets out, the level ends (OzFpsView.CheckEndsMatch stands in for it), and
    /// (fix round 1) Esc right after a fly-to began (the RTS camera stays on the unit); and the refusals (nothing, two units, an enemy unit selected,
    /// a gun being placed, an open question: no view, the refusal counted with its reason). Fix round 1, the MAYHEM fog: the fog forced on and the map
    /// covered again (test only), a grunt looks across at ground not explored yet: the fog's opacity picture (debug 1) full there (the ground, 3 m over
    /// it, an enemy building on it when one stands in sight) and clear on the explored ground in front; pictures fps_fog.png and fps_fog_op.png.
    /// Fix round 3: every own robot comes as its game squad (group_amount; the dummies are one robot); a robot's eye must be its head bone; W sends
    /// about one move order per 2 tiles (1 .. 1 + walked px / 24), D and (robots) S at least one, and nothing of a unit (its own squad mates too)
    /// is drawn within 1 m of the eye on any frame while W, D, S are held; fire on the move (a grunt squad, a light tank): its own shots while W is
    /// held straight past an enemy dummy in range at least 70 % of the rate standing still; and last, the level ends for real (the enemy fort
    /// destroyed) while a grunt is in the view: it leaves with "the level ends", the RTS camera keeps following OpenZEnding's target, glides toward
    /// the fort, stays driven up to the orbit round the fort and the orbit runs (the ending's own camActive, orbitT, camTarget; no release).
    /// No PASS without its samples; errors and exceptions logged during the run fail it. Report and a picture sheet of every type's first-person
    /// frame: <see cref="Folder"/>. Test only: the units it made stay, and so does the fog's reveal map of the fog row.
    /// fps_view_fix (7 Nov 2026): (c) what the camera draws is read inside its pictures (OzFpsView.AtRender: the view holds renderers off only
    /// there), between pictures the view holds none, and every model renderer of the unit and its crew figure is compared before the entry and
    /// after the leave, renderer by renderer (a change only where the game itself made it); (b) every vehicle and gun's copy is its barrel only
    /// (the parts kept and left out), at the lower middle (its box on the screen), its own pixels' share and brightness against the grunt's and
    /// the sniper's copies; (d) the medium tank too, and drive while firing for every vehicle type: the drawn unit and its sim point at its
    /// normal speed, no attack order in front of the walk; (a) the MAYHEM fog judged against the RTS camera's own fog picture, plus a whole-frame
    /// row on the fog's own distance; check faults: the game's own grab of a neutral vehicle after the stop (DV38 / DV84) is named and stopped,
    /// the focus row compares with the focus the view itself set (not a 5 cm bar), W must move the unit the way the view looks.
    /// </summary>
    public sealed partial class OzFpsViewPlayCheck : MonoBehaviour                                     // fps_view_sky: partial (the short check: OzFpsViewPlayCheck.Sky.cs)
    {
        public static string Report = "idle";
        static readonly string[] Robots = { "grunt", "sniper", "psycho", "tough", "pyro", "laser" }, Vehicles = { "light", "jeep", "medium", "mtank", "heavy", "apc" }, Guns = { "gatling", "gun" };   // fps_view_fix: + the medium tank (mtank; "medium" is the mech)
        /// <summary></summary>
        static readonly string[] GunMust = { "grunt", "sniper", "psycho", "tough", "pyro", "laser", "light", "jeep", "medium", "mtank", "heavy", "apc", "gatling", "gun" };   // fps_view_fix (b): every type
        ZGame g; ZWorld W; RtsCamera rts; PlayerInput pi; OpenZHud hud; Camera cam; Team me, foe; int pass, fail, errors;
        readonly List<string> errorLines = new List<string>();
        Keyboard proofKb, realKb; Mouse proofMouse, realMouse;
        readonly StringBuilder log = new StringBuilder(); readonly List<string> shots = new List<string>();
        readonly List<ZObject> made = new List<ZObject>();   // E2a (6 Nov 2026): the units this check made, taken out of the world before the next test's place (L02 has one free 15 m disc)

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_4", "kits", "fps_view_sky");   // fps_view_sky: this kit's proof/play (was fps_view_fix's)
                if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.Combine(Application.persistentDataPath, "fps_view_play_check");
        }

        public static void Run() { Report = "running"; new GameObject("OzFpsViewPlayCheck").AddComponent<OzFpsViewPlayCheck>(); }

        void Start() { Application.logMessageReceived += OnLog; StartCoroutine(glowRun ? GlowAll() : skyRun ? SkyAll() : All()); }   // fps_view_sky: the short check (OzFpsViewPlayCheck.Sky.cs); fps_glow: the glow check (OzFpsViewPlayCheck.Glow.cs)
        void OnDestroy() { Application.logMessageReceived -= OnLog; Devices(false); OzFpsView.CheckEndsMatch = false; OzFpsView.AtRender = null; }
        void OnLog(string msg, string st, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { errors++; if (errorLines.Count < 6) errorLines.Add(msg.Split('\n')[0]); } }

        void Log(string s) { log.AppendLine(s); Debug.Log("[OpenZ FPS check] " + s); }
        void Check(string what, bool ok) { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }
        /// <summary>Waits s real seconds, calling each() every frame (fix round 2: the tracks loop and the lid sampled while keys are held).</summary>
        static IEnumerator Sample(float s, System.Action each) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) { each(); yield return null; } }

        // ------------------------------------------------------------------ the test keyboard and mouse
        void Devices(bool on)
        {
            if (on)
            {
                realKb = Keyboard.current; realMouse = Mouse.current;
                proofKb = InputSystem.AddDevice<Keyboard>("OzFpsProofKeyboard"); proofKb.MakeCurrent();
                proofMouse = InputSystem.AddDevice<Mouse>("OzFpsProofMouse"); proofMouse.MakeCurrent();
                return;
            }
            if (proofKb != null) { if (proofKb.added) InputSystem.RemoveDevice(proofKb); proofKb = null; }
            if (proofMouse != null) { if (proofMouse.added) InputSystem.RemoveDevice(proofMouse); proofMouse = null; }
            if (realKb != null && realKb.added) realKb.MakeCurrent(); if (realMouse != null && realMouse.added) realMouse.MakeCurrent();
            realKb = null; realMouse = null;
        }
        void Keys(params Key[] k) { if (proofKb != null) InputSystem.QueueStateEvent(proofKb, new KeyboardState(k)); }
        IEnumerator Tap(params Key[] k) { Keys(k); yield return null; yield return null; Keys(); yield return null; }
        IEnumerator MouseBy(Vector2 d)
        {
            var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            InputSystem.QueueStateEvent(proofMouse, new MouseState { position = c, delta = d }); yield return null;
            InputSystem.QueueStateEvent(proofMouse, new MouseState { position = c, delta = Vector2.zero }); yield return null; yield return null;
        }

        // ------------------------------------------------------------------ places and units
        bool Walkable(int x, int y) => !W.Path.WithinImpassable(x - 8, y - 8, 16, 16, out _, out _, true) && !W.Map.LavaAt(x >> 4, y >> 4)
                                       && (ignoreUnits || !W.UnitObjects.Any(o => !o.Destroyed && System.Math.Abs(o.CenterX - x) < 48 && System.Math.Abs(o.CenterY - y) < 48));
        bool ignoreUnits;   // E3: FindSpot's last vehicle pass (the level's units on the only vehicle-ground place are then taken out)

        /// <summary>A free disc of 120 px near the camera (room to walk in every direction). fps_view_fix fix round 1 (the review's (d) light tank):
        /// a vehicle's or a gun's test (vehicle true) gets VEHICLE ground only: a 32 px box free on the vehicle grid at every sample point of the
        /// disc, never the robot ground alone. E2a's spot tested robot ground only, and L17's streets and car park are mostly ground a robot
        /// walks and a vehicle does not (pavements, verges): a tank made there stood across the edge of it, and every move order into the closed
        /// side ended at once with Zod's own terrain block (ZObject.ProcessMoveOrKillWP: "blocked by terrain ... again: Zod KillWP", the same for
        /// a right-click; measured on P's own Sim, kits/fps_view_fix/tools/harness sweep and probe, proof/proof_measure.txt): 0.13 m in 3 s,
        /// waypoints 0, for EVERY vehicle type on about half of E2a's places and directions. The neutral-free pass stays a preference; L17 has no
        /// place 340 px from every neutral vehicle and gun, so there it always falls back.</summary>
        bool FindSpot(out int x, out int y, bool vehicle = false)
        {
            Clean();                                                                                          // E2a: the earlier tests' units out first (see Clean)
            awayFromFoe = true;                                                                               // E3 (7 Nov 2026, check only): run 1 put the sniper 38 m from an enemy howitzer (L17), which killed it in the view and every later row failed
            if (Spot(out x, out y, true, vehicle) || Spot(out x, out y, false, vehicle)) { awayFromFoe = false; return true; }
            awayFromFoe = false;
            if (Spot(out x, out y, true, vehicle) || Spot(out x, out y, false, vehicle)) return true;      // fps_view_fix: first away from neutral vehicles and guns (the game's close grab, 220 px)
            if (vehicle)
            {   // E3 (check only): L17 has one cluster of vehicle ground (by (368, 720)), next to the enemy factory; run 2 found the enemy's captured medium tank #3 parked on it and every vehicle row had no place. The level's own units standing on that place are taken out (the game's RemoveNow, as Clean), named in an INFO line.
                ignoreUnits = true; bool got = Spot(out x, out y, false, true); ignoreUnits = false;
                if (got)
                {
                    int cx = x, cy = y; var names = new List<string>();
                    foreach (var o in W.UnitObjects.ToList())
                        if (o != null && !o.Destroyed && System.Math.Abs(o.CenterX - cx) < 176 && System.Math.Abs(o.CenterY - cy) < 176)
                        {
                            var ids = new List<int> { o.Id }; foreach (var m in o.Minions) if (m != null) ids.Add(m.Id);
                            names.Add($"{o.Type} #{o.Id} {o.Owner}"); pi.Selection.RemoveAll(s => s != null && ids.Contains(s.Id)); W.RemoveNow(o);
                            foreach (var id in ids) if (g.Views.TryGetValue(id, out var v)) { g.Views.Remove(id); if (v) { v.gameObject.SetActive(false); Destroy(v.gameObject); } }
                        }
                    Log($"INFO the only vehicle-ground place ({cx},{cy}) px was taken by the level's own units: taken out by the check: {(names.Count > 0 ? string.Join(", ", names) : "none")}");
                    return true;
                }
            }
            foreach (int rs in new[] { 72, 48 })
            {   // U1 (7 Nov 2026, check only): L02 has no 120 px vehicle-ground disc at all (largest 72 px) and its one 120 px robot disc fills with the CPU's start squads within seconds: a smaller free disc is taken (the sky check's rows do not drive far)
                spotR = rs; bool got = Spot(out x, out y, true, vehicle) || Spot(out x, out y, false, vehicle); spotR = 120;
                if (got) { Log($"INFO no 120 px free disc near the camera here: a {rs} px one at ({x},{y}) px (U1, check only)"); return true; }
            }
            LastSpotWhy = vehicle ? "no place of vehicle ground (a 32 px box free on the vehicle grid) 120 px round near the camera" : "no free place near the camera";
            return false;
        }
        /// <summary>fps_view_fix fix round 1: why the last FindSpot found nothing (for the failing row's text).</summary>
        string LastSpotWhy = "";

        /// <summary>fps_view_fix: no neutral vehicle or gun within the game's close grab (World.HeldVehicleReach 220 px) + 120 px of (x, y).</summary>
        bool NoFoeNear(int x, int y) => !W.Objects.Any(o => o != null && !o.Destroyed && o.Owner == foe && o.Damage > 0 && o.Kind != ZKind.MapItem
                                                         && System.Math.Abs(o.CenterX - x) < o.AttackRadius + 320 && System.Math.Abs(o.CenterY - y) < o.AttackRadius + 320);
        /// <summary>E3 (check only): FindSpot's first passes also keep an armed enemy's attack radius + 320 px (20 m) away from the place.</summary>
        bool awayFromFoe;
        bool NoNeutralNear(int x, int y) => !W.Objects.Any(o => o != null && !o.Destroyed && o.Owner == Team.Neutral && (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon)
                                                              && System.Math.Abs(o.CenterX - x) < 340 && System.Math.Abs(o.CenterY - y) < 340);

        /// <summary>fps_view_fix fix round 1: a sample point of the disc: robot ground (Walkable), and for a vehicle's test also a 32 px box free on
        /// the vehicle grid (the ground the vehicle itself drives on).</summary>
        bool Free2(bool vehicle, int x, int y) => Walkable(x, y) && (!vehicle || !W.Path.WithinImpassable(x - 16, y - 16, 32, 32, out _, out _, false));

        int spotR = 120;   // U1 (check only): the free disc's radius in px (FindSpot's last passes try 72, then 48)
        bool Spot(out int x, out int y, bool awayFromNeutral, bool vehicle)
        {
            int fx = (int)(rts.focus.x * 8f), fy = (int)(-rts.focus.z * 8f), mw = (int)(rts.mapSizeMeters.x * 8f), mh = (int)(rts.mapSizeMeters.y * 8f), edge = 320;   // 40 m off the map edges: the RTS camera's own limits never move its focus there
            for (int r = 0; r < 1600; r += 32)
                for (int a = 0; a < 16; a++)
                {
                    x = fx + (int)(r * Mathf.Cos(a * 0.3927f)); y = fy + (int)(r * Mathf.Sin(a * 0.3927f));
                    bool ok = x > edge && y > edge && x < mw - edge && y < mh - edge && (!awayFromNeutral || NoNeutralNear(x, y)) && (!awayFromFoe || NoFoeNear(x, y));
                    for (int k = 0; ok && k < 16; k++) for (int rr = 0; ok && rr <= spotR; rr += 24) ok = Free2(vehicle, x + (int)(rr * Mathf.Cos(k * 0.3927f)), y + (int)(rr * Mathf.Sin(k * 0.3927f)));
                    if (ok) return true;
                }
            // E2a: the rings above step 32 px out and 22.5 deg round, so far out they pass a small free disc by (L02 has one); every 16 px, nearest first
            var cand = new List<Vector2Int>();
            for (int gx = edge + 16; gx < mw - edge; gx += 16) for (int gy = edge + 16; gy < mh - edge; gy += 16) cand.Add(new Vector2Int(gx, gy));
            cand.Sort((p, q) => ((long)(p.x - fx) * (p.x - fx) + (long)(p.y - fy) * (p.y - fy)).CompareTo((long)(q.x - fx) * (q.x - fx) + (long)(q.y - fy) * (q.y - fy)));
            foreach (var c in cand)
            {
                bool ok = (!awayFromNeutral || NoNeutralNear(c.x, c.y)) && (!awayFromFoe || NoFoeNear(c.x, c.y));
                for (int k = 0; ok && k < 16; k++) for (int rr = 0; ok && rr <= spotR; rr += 24) ok = Free2(vehicle, c.x + (int)(rr * Mathf.Cos(k * 0.3927f)), c.y + (int)(rr * Mathf.Sin(k * 0.3927f)));
                if (ok) { x = c.x; y = c.y; return true; }
            }
            x = y = 0; return false;
        }

        /// <summary>E2a (6 Nov 2026, check only): every unit this check made so far is taken out of the world (the game's RemoveNow, no death,
        /// as the Unit editor's CLEAR FIELD) and its view dropped, so each test finds the free place again (L02 has a single 15 m free disc
        /// inside its 40 m margin). Called first in every FindSpot; the test before it has left the view by then.</summary>
        void Clean()
        {
            var all = new List<ZObject>(made);
            if (startIds != null) foreach (var o in W.UnitObjects.ToList()) if (o != null && !startIds.Contains(o.Id)) all.Add(o);   // also a crew that got out, a squad mate left by a dead leader
            foreach (var o in all)
            {
                if (o == null) continue;
                var ids = new List<int> { o.Id }; foreach (var m in o.Minions) if (m != null) ids.Add(m.Id);
                pi.Selection.RemoveAll(s => s != null && ids.Contains(s.Id));
                W.RemoveNow(o);
                foreach (var id in ids)
                    if (g.Views.TryGetValue(id, out var v)) { g.Views.Remove(id); if (v) { v.gameObject.SetActive(false); Destroy(v.gameObject); } }
            }
            made.Clear(); noGrab = null;
        }
        HashSet<int> startIds;   // E2a: the level's own units when the check began (W.Bots cleared: nothing else makes units during the run)

        /// <summary>A new unit at (x, y). Fix round 3: a robot comes as its game squad (amount -1: default_settings group_amount, grunt 3, tough 2,
        /// pyro 4 ...), every member with its view, as in the game; amount 1 only for the dummy enemies (one robot that never fires).</summary>
        ZObject Make(string cat, string type, int x, int y, Team t, int amount = -1)
        {
            var o = cat == "robot" ? W.CreateRobotGroup(type, x - 8, y - 8, t, amount) : W.CreateObject(cat, type, x - 16, y - 16, t);
            if (o != null) { g.SpawnUnitView(o); foreach (var m in o.Minions) if (m != null) g.SpawnUnitView(m); made.Add(o); }   // E2a: kept for Clean
            return o;
        }

        void Select(params ZObject[] us) { pi.Selection.Clear(); foreach (var u in us) if (u != null) pi.Selection.Add(u); }
        static void Kill(ZWorld w, ZObject t) { if (t != null && !t.Destroyed) { t.SetHealth(0); w.UpdateObjectHealth(t); } }
        UnitView ViewOf(ZObject u) => u != null && g.Views.TryGetValue(u.Id, out var v) ? v : null;

        static int DrawnModel(UnitView v)
        {
            if (!v) return 0; int n = 0;
            foreach (var r in v.GetComponentsInChildren<Renderer>(true))
                if ((r is MeshRenderer || r is SkinnedMeshRenderer) && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff) n++;
            return n;
        }
        /// <summary>Fix round 2: model renderers of units and crew figures (under any unit view or BoardingView's OZ_BoardingCrew) drawn with their
        /// bounds within r m of the camera, the gun copy (under the camera) aside. The camera must never stand in a robot or a hull.</summary>
        int NearDrawn(float r, out string names)
        {
            var c = cam.transform.position; int n = 0; var sb = new StringBuilder();
            void Look(Transform root)
            {
                foreach (var x in root.GetComponentsInChildren<Renderer>(false))
                    if ((x is MeshRenderer || x is SkinnedMeshRenderer) && x.enabled && !x.forceRenderingOff && !x.transform.IsChildOf(cam.transform) && x.bounds.SqrDistance(c) < r * r)
                    { n++; if (sb.Length < 240) sb.Append(x.name).Append(' '); }
            }
            foreach (var kv in g.Views) if (kv.Value && (kv.Value.transform.position - c).sqrMagnitude < 144f) Look(kv.Value.transform);   // fix round 3 (sampled every frame of a walk): roots within 12 m, every hull with a part within 1 m among them
            var box = BoardingView.CrewBox; if (box) Look(box);
            names = sb.Length > 0 ? sb.ToString().Trim() : "-"; return n;
        }

        static int HeldOff(UnitView v) { if (!v) return 0; int n = 0; foreach (var r in v.GetComponentsInChildren<Renderer>(true)) if ((r is MeshRenderer || r is SkinnedMeshRenderer) && r.forceRenderingOff) n++; return n; }
        static string HeldOffNames(UnitView v) { if (!v) return "-"; var s = new List<string>(); foreach (var r in v.GetComponentsInChildren<Renderer>(true)) if ((r is MeshRenderer || r is SkinnedMeshRenderer) && r.forceRenderingOff) s.Add(r.name + (r.gameObject.activeInHierarchy ? "" : " (inactive)")); return s.Count == 0 ? "-" : string.Join(", ", s); }   // E2a: which ones (report only)

        // ------------------------------------------------------------------ fps_view_fix (c): what the camera draws, read inside its picture
        // The view holds the model, the crew figure and the units by the eye off only while its camera draws (OzFpsView.AtRender is called
        // inside that picture, right after the hold). Outside the picture every renderer is as the game set it, so "drawn" is read there.
        UnitView rWatch; int rFrames, rDrawnMax, rNearMax; string rNearNames = "-";
        void OnRender(Camera c)
        {
            if (c != cam) return;
            rFrames++;
            if (rWatch) rDrawnMax = Mathf.Max(rDrawnMax, DrawnModel(rWatch));
            int n = NearDrawn(1f, out string nm); if (n > rNearMax) { rNearMax = n; rNearNames = nm; }
        }
        void Watch(UnitView v) { rWatch = v; rFrames = 0; rDrawnMax = 0; rNearMax = 0; rNearNames = "-"; }

        /// <summary>fps_view_fix (c): every model renderer of the unit and of its crew figure, with its state (held off by forceRenderingOff, enabled,
        /// active); the check compares the state before the entry with the state after the leave, renderer by renderer.</summary>
        Dictionary<Renderer, (bool off, bool en, bool act)> States(ZObject u, UnitView v)
        {
            var d = new Dictionary<Renderer, (bool, bool, bool)>();
            void Add(Transform t) { if (!t) return; foreach (var r in t.GetComponentsInChildren<Renderer>(true)) if (r is MeshRenderer || r is SkinnedMeshRenderer) d[r] = (r.forceRenderingOff, r.enabled, r.gameObject.activeInHierarchy); }
            if (v) Add(v.transform);
            var crew = u != null ? BoardingView.CrewFigure(u.Id) : null; if (crew) Add(crew.transform);
            return d;
        }

        /// <summary>fps_view_fix (c): a renderer the game itself holds off now: a BS01 type kit's back part (pack, drum, tanks, cape, radio) while its
        /// robot sits in a jeep or a gun (BS01TypeKit hides them with forceRenderingOff while seated_driver / seated_gunner plays).</summary>
        static bool GameHides(Renderer r)
        {
            var kit = r ? r.GetComponentInParent<BS01TypeKit>(true) : null; if (!kit) return false;
            bool inBack = false; foreach (var b in kit.BackParts) if (b == r) { inBack = true; break; }
            var f = typeof(BS01TypeKit).GetField("seated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return inBack && f != null && (bool)f.GetValue(kit) && r.forceRenderingOff;
        }

        /// <summary>fps_view_fix (c): the renderers whose state after the leave is not the state before the entry, each explained (the game changed it:
        /// the view found it held off by other code in its last picture, or the BS01 kit's seated rule holds it) or not.</summary>
        string Compare(Dictionary<Renderer, (bool off, bool en, bool act)> before, Dictionary<Renderer, (bool off, bool en, bool act)> after, out int same, out int game, out int bad)
        {
            same = game = bad = 0; var gsb = new StringBuilder(); var bsb = new StringBuilder();
            foreach (var kv in before)
            {
                var r = kv.Key; if (!r || !after.TryGetValue(r, out var a)) continue;                            // gone with its unit (a crew that got out)
                if (a.Equals(kv.Value)) { same++; continue; }
                // the view writes forceRenderingOff only (inside its pictures): an enabled / active change is the game's (BoardingView, a part shown or hidden);
                // held off after and drawn before only where the game holds it off (found off by other code in the view's last picture, or the BS01 kit)
                bool explained = a.off == kv.Value.off || !a.off || OzFpsView.LastForeignOff.Contains(r) || GameHides(r);
                string s = $"{r.name} off {kv.Value.off}->{a.off} on {kv.Value.en}->{a.en} active {kv.Value.act}->{a.act}{(GameHides(r) ? " (BS01 kit: seated)" : OzFpsView.LastForeignOff.Contains(r) ? " (held off by other code in the view's last picture)" : "")}";
                if (explained) { game++; if (gsb.Length < 300) gsb.Append(s).Append("; "); } else { bad++; if (bsb.Length < 300) bsb.Append(s).Append("; "); }
            }
            return $"game {(gsb.Length > 0 ? gsb.ToString().Trim() : "-")} | unexplained {(bsb.Length > 0 ? bsb.ToString().Trim() : "-")}";
        }

        // ------------------------------------------------------------------ fps_view_fix (b): the gun copy on the screen
        /// <summary>The copy's box on the screen (viewport 0..1, y up) from its renderers' bounds (8 corners each).</summary>
        Rect GunBox(OzFpsView fv)
        {
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            foreach (var r in fv.GunRenderers)
            {
                if (!r) continue; var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var v = cam.WorldToViewportPoint(p); if (v.z <= 0f) continue;
                    x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
                }
            }
            if (x1 < x0) return Rect.zero;
            x0 = Mathf.Clamp01(x0); y0 = Mathf.Clamp01(y0); x1 = Mathf.Clamp01(x1); y1 = Mathf.Clamp01(y1);
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        /// <summary>The copy's own pixels: the frame with the copy and the next frame with the copy held off (the check's own test, one frame),
        /// differing inside its box: their share of the screen and their mean brightness (Rec. 709 luma of the screen's values).</summary>
        IEnumerator GunPixels(OzFpsView fv, System.Action<float, float, int> done)
        {
            var box = GunBox(fv);
            yield return new WaitForEndOfFrame(); var a = ScreenCapture.CaptureScreenshotAsTexture();
            foreach (var r in fv.GunRenderers) if (r) r.forceRenderingOff = true;
            yield return null; yield return new WaitForEndOfFrame(); var b = ScreenCapture.CaptureScreenshotAsTexture();
            foreach (var r in fv.GunRenderers) if (r) r.forceRenderingOff = false;
            int w = a.width, h = a.height, n = 0; double lum = 0; var xs = new List<int>(); var ys = new List<int>();   // E3: the copy's own pixels' box (gunPixBox)
            int ix0 = Mathf.Max(0, (int)(box.xMin * w) - 8), ix1 = Mathf.Min(w - 1, (int)(box.xMax * w) + 8), iy0 = Mathf.Max(0, (int)(box.yMin * h) - 8), iy1 = Mathf.Min(h - 1, (int)(box.yMax * h) + 8);
            for (int y = iy0; y <= iy1; y += 2)
                for (int x = ix0; x <= ix1; x += 2)
                {
                    var ca = a.GetPixel(x, y); var cb = b.GetPixel(x, y);
                    if (Mathf.Abs(ca.r - cb.r) + Mathf.Abs(ca.g - cb.g) + Mathf.Abs(ca.b - cb.b) > 0.06f) { n++; lum += 0.2126f * ca.r + 0.7152f * ca.g + 0.0722f * ca.b; xs.Add(x); ys.Add(y); }
                }
            gunPixBox = Rect.zero;
            if (xs.Count > 20) { xs.Sort(); ys.Sort(); int lo = xs.Count / 200, hi = xs.Count - 1 - xs.Count / 200; gunPixBox = Rect.MinMaxRect(xs[lo] / (float)w, ys[lo] / (float)h, xs[hi] / (float)w, ys[hi] / (float)h); }   // 0.5 % at each end left out (a muzzle flash, a unit passing)
            Destroy(a); Destroy(b);
            done(n * 4f / Mathf.Max(1, w * h), n > 0 ? (float)(lum / n) : 0f, n);
        }
        readonly Dictionary<string, float> gunLuma = new Dictionary<string, float>();
        /// <summary>E3 (check only): the gun copy's box from its own pixels (GunPixels). Run 3: the box from the renderers' world bounds came out 0.37 - 0.43
        /// wide for narrow barrels (the corners of a slanted barrel's axis-aligned box near the eye spread far on the screen) and 0 .. 1 for the gatling
        /// (its mesh carries the whole turret's stale box, README); the pictures show a narrow barrel at the lower middle.</summary>
        Rect gunPixBox;

        /// <summary>fps_view_fix fix round 1 (the review's (c): the rows above read the renderers' flags, which the view itself sets; the PC, Low and
        /// Medium URP assets have the GPU Resident Drawer on, and a flag set inside a picture might not reach the drawer's instances). Here the hold
        /// is read by PIXELS: the game still (paused, time scale 0, no anti-aliasing history, as Editor/OzV13PerfViewCheck.cs's drawer A-B), the
        /// view looking 60 deg down at its own body or hull. A = the view's picture as it is (twice: A' gives the picture's own noise); B = the same
        /// picture with the hold lifted (OzFpsView.CheckDrawOwn: the model drawn, so the test sees where it is); C = the same picture with the
        /// renderers the hold took switched off for real (Renderer.enabled false, set before the frame, no render-time callback; given back
        /// exactly). Pass: A differs from C no more than A from A' (+ 0.05 % of the screen), and B differs from C on at least 0.5 % (the model is
        /// in the picture). Pictures: proof/play/fps_hull_[type]_A.png and _B.png.</summary>
        IEnumerator HullPixels(string type, OzFpsView fv)
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            string drawer = urp ? urp.gpuResidentDrawerMode.ToString() : "?";
            var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); var aa0 = cd ? cd.antialiasing : UnityEngine.Rendering.Universal.AntialiasingMode.None;
            bool paused0 = g.paused; float ts0 = Time.timeScale;
            g.paused = true; Time.timeScale = 0f; if (cd) cd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            yield return Frames(6);
            Texture2D a = null, a2 = null, bTex = null, c = null;
            yield return new WaitForEndOfFrame(); a = ScreenCapture.CaptureScreenshotAsTexture();
            yield return Frames(2); yield return new WaitForEndOfFrame(); a2 = ScreenCapture.CaptureScreenshotAsTexture();
            var held = new List<Renderer>(); fv.HeldInLastPicture(held);
            Vector3 camP = cam.transform.position; Quaternion camR = cam.transform.rotation;                 // E3: A's camera pose, kept for the C picture (below)
            OzFpsView.CheckDrawOwn = true; yield return Frames(2); yield return new WaitForEndOfFrame(); bTex = ScreenCapture.CaptureScreenshotAsTexture(); OzFpsView.CheckDrawOwn = false;
            // E3 (check only): run 2's C pictures were the same scene shifted by a few pixels (the difference only on edges, no body shape): with
            // every renderer of the robot switched off its Animator culls (CullUpdateTransforms), the head bone the eye sits on stops, and the view
            // moved. Their Animators always animate for the C frames (game still: the same pose), put back after.
            var anims = new Dictionary<Animator, AnimatorCullingMode>(); foreach (var r in held) { var an = r ? r.GetComponentInParent<Animator>() : null; if (an && !anims.ContainsKey(an)) { anims[an] = an.cullingMode; an.cullingMode = AnimatorCullingMode.AlwaysAnimate; } }
            // E3 run 3: still shifted with the Animators animating (the eye's own path, not the bones' culling, moves with the renderers off), so the
            // C picture is drawn from A's camera pose exactly (set at the start of each picture of this camera, test only, taken off after C)
            System.Action<UnityEngine.Rendering.ScriptableRenderContext, Camera> pin = (ctx, cc) => { if (cc == cam) cc.transform.SetPositionAndRotation(camP, camR); };
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += pin;
            var was = new List<bool>(held.Count); foreach (var r in held) { was.Add(r.enabled); r.enabled = false; }
            yield return Frames(2); yield return new WaitForEndOfFrame(); c = ScreenCapture.CaptureScreenshotAsTexture();
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= pin;
            for (int i = 0; i < held.Count; i++) if (held[i]) held[i].enabled = was[i];
            foreach (var kv in anims) if (kv.Key) kv.Key.cullingMode = kv.Value;
            if (cd) cd.antialiasing = aa0; Time.timeScale = ts0; g.paused = paused0;
            float aa = DifferShift(a, a2), ac = DifferShift(a, c), bc = Differ(bTex, c);                 // E3: A against A' and C read with a 1 px shift allowed (run 4: with the camera pinned the robots' C still differed on edges only, 0.4 - 2.2 %, no body shape: a sub-pixel move)
            try { File.WriteAllBytes(Path.Combine(Folder(), "fps_hull_" + type + "_A.png"), a.EncodeToPNG()); File.WriteAllBytes(Path.Combine(Folder(), "fps_hull_" + type + "_B.png"), bTex.EncodeToPNG()); File.WriteAllBytes(Path.Combine(Folder(), "fps_hull_" + type + "_C.png"), c.EncodeToPNG()); } catch (IOException) { }   // E3: C saved too
            Destroy(a); Destroy(a2); Destroy(bTex); Destroy(c);
            yield return Frames(2);
            string row = $"{type}: by PIXELS its own model is not drawn (the game still, the view {fv.Pitch:0} deg down, GPU Resident Drawer {drawer}): the view's picture against the same picture with the {held.Count} held renderers switched off for real {ac * 100f:0.000} % of the screen differs (its own noise {aa * 100f:0.000} %; at most noise + 0.05 %); with the hold lifted the model shows on {bc * 100f:0.00} % (at least 0.5 %: the test sees it)";
            if (bc >= 0.005f) { hullSeen.Add(type); Check(row, held.Count > 0 && ac <= aa + 0.0005f); }
            else Log("INFO " + row + ": the model is not in this picture, so this type's pixels prove nothing either way (the must-list row after all types)");
        }
        /// <summary>fps_view_fix fix round 1: the types whose model the pixel row saw in the picture (the hold then proven on them).</summary>
        readonly HashSet<string> hullSeen = new HashSet<string>();
        /// <summary>The pixel row must see these types' models: two robots (skinned) and every vehicle (mesh renderers, the drawer's kind).</summary>
        static readonly string[] HullMust = { "grunt", "sniper", "light", "jeep", "medium", "mtank", "heavy", "apc" };

        /// <summary>E3 (check only): as Differ, but a pixel of p counts only when no pixel of q within 1 px matches it (a sub-pixel shift of the same
        /// picture reads 0; the model's own shape, many pixels wide, still counts).</summary>
        static float DifferShift(Texture2D p, Texture2D q)
        {
            if (!p || !q || p.width != q.width || p.height != q.height) return 1f;
            var x = p.GetPixels32(); var y = q.GetPixels32(); int w = p.width, h = p.height, n = 0, all = 0;
            for (int j = 1; j < h - 1; j += 2)
                for (int i = 1; i < w - 1; i += 2)
                {
                    var s = x[j * w + i]; all++; bool match = false;
                    for (int dj = -1; dj <= 1 && !match; dj++) for (int di = -1; di <= 1 && !match; di++) { var d = y[(j + dj) * w + i + di]; match = System.Math.Abs(s.r - d.r) + System.Math.Abs(s.g - d.g) + System.Math.Abs(s.b - d.b) <= 15; }
                    if (!match) n++;
                }
            return all > 0 ? (float)n / all : 1f;
        }
        /// <summary>Share of the screen (sampled every 2nd pixel each way) where two grabs differ by more than 0.06 summed over r, g, b.</summary>
        static float Differ(Texture2D p, Texture2D q)
        {
            if (!p || !q || p.width != q.width || p.height != q.height) return 1f;
            var x = p.GetPixels32(); var y = q.GetPixels32(); int w = p.width, h = p.height, n = 0, all = 0;
            for (int j = 0; j < h; j += 2)
                for (int i = 0; i < w; i += 2)
                {
                    var s = x[j * w + i]; var d = y[j * w + i]; all++;
                    if (System.Math.Abs(s.r - d.r) + System.Math.Abs(s.g - d.g) + System.Math.Abs(s.b - d.b) > 15) n++;
                }
            return all > 0 ? (float)n / all : 1f;
        }

        struct Before { public float height, pitch, fov, near; public bool hud, sel, rings; public int heldOff; }
        AttackRangeRings Rings() { var r = pi.GetComponent<AttackRangeRings>(); return r ? r : AttackRangeRings.I; }
        Before Snap(UnitView v)
        {
            var so = pi.GetComponent<SelectionOverlay>(); var ar = Rings();
            return new Before { height = rts.height, pitch = rts.pitch, fov = cam.fieldOfView, near = cam.nearClipPlane, hud = hud && hud.hudVisible, sel = so && so.enabled, rings = ar && ar.enabled, heldOff = HeldOff(v) };
        }

        // ------------------------------------------------------------------ one type
        IEnumerator Enter(string tag, ZObject u, Before b)
        {
            Select(u); yield return Frames(2);
            if (pi.Placing != null) { Log($"INFO {tag}: a gun placing was left open before the entry (from {pi.Placing.Type} #{pi.Placing.Id}, stored {string.Join(",", pi.Placing.StoredCannons)}): cancelled by the check (E3: a row before it failed)"); pi.CancelPlace(); yield return Frames(2); }
            int e0 = OzFpsView.Enters;
            yield return Tap(Key.LeftAlt, Key.F); yield return Frames(3);
            Check($"{tag}: Alt+F with it selected alone: the view is on ({OzFpsView.Enters - e0} entry; refusal: {(OzFpsView.On ? "-" : OzFpsView.LastRefusal)})", OzFpsView.On && OzFpsView.Enters == e0 + 1 && OzFpsView.I && OzFpsView.I.Unit == u);
        }

        IEnumerator Type(string cat, string type)
        {
            if (!FindSpot(out int x, out int y, cat != "robot")) { Check($"{type}: a free place near the camera ({LastSpotWhy})", false); yield break; }
            Log($"INFO {type}: the place ({x},{y}) px, {(cat != "robot" ? "vehicle ground (a 32 px box free on the vehicle grid at every point 120 px round)" : "robot ground")}{(NoNeutralNear(x, y) ? ", no neutral vehicle or gun within 340 px" : ", a neutral vehicle or gun within 340 px (none farther on this map)")}");
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(0.4f);
            var u = Make(cat, type, x, y, me); if (cat == "robot") { noGrab = u; grabsStopped = 0; } yield return Real(1.2f);   // E3: the game's close grab of a neutral vehicle stopped while the robot is tested (run 2: L17's places are all within 340 px of one)
            var v = ViewOf(u);
            if (u == null || !v) { Check(type + ": made with its view", false); yield break; }
            var b = Snap(v); var before = States(u, v);
            yield return Enter(type, u, b);
            var fv = OzFpsView.I; if (!OzFpsView.On || !fv) yield break;
            bool robot = cat == "robot", gun = cat == "cannon";
            // ---- the eye, the model, the gun, the screen
            yield return Real(0.3f);
            var cp = cam.transform.position; float hgt = cp.y - g.GroundY(cp.x, cp.z);
            float lo = robot ? 0.95f : 1.2f, hi = robot ? (type == "tough" ? 3.6f : 3.0f) : 7f;
            bool headEye = fv.EyeName.EndsWith("(Head)");                                                  // fix round 3: a robot's eye is its head bone (the Top fallback also lands in the band)
            Check($"{type}: the camera at the eye {fv.EyeName}{(robot ? " (a robot: the head bone wanted, " + (headEye ? "found" : "NOT found") + ")" : "")}: {hgt:0.00} m over the ground (band {lo:0.0} .. {hi:0.0})",
                  hgt >= lo && hgt <= hi && (!robot || headEye));
            if (robot) Log($"INFO {type}: a squad of {1 + u.Minions.Count} (the game's group_amount), {fv.UnitsHeld} unit view(s) by the eye held off now");
            // fps_view_fix (c): read inside the camera's pictures (the only time the view holds anything off) and between them
            Watch(v); int heldBetween = 0, betweenFrames = 0;
            yield return Sample(0.25f, () => { betweenFrames++; heldBetween = Mathf.Max(heldBetween, OzFpsView.HeldNow); });
            Check($"{type}: its own model held off while the camera draws: {fv.HiddenCount} renderers held in the last picture, at most {rDrawnMax} of the unit drawn in {rFrames} pictures; between pictures the view holds {heldBetween} (on {betweenFrames} frames: every renderer as the game set it)",
                  fv.HiddenCount > 0 && rFrames >= 3 && rDrawnMax == 0 && heldBetween == 0 && betweenFrames > 0);
            // fix round 2: the crew figure BoardingView keeps outside the unit's view (the jeep's seated driver; the light tank's hatch commander, the lid
            // forced open here: test only, as the idle rest or a fight opens it) is held off, and nothing of a unit or a figure is drawn within 1 m of the eye
            if (type == "light") { yield return Sample(1.6f, () => u.LidOpen = true); }                     // lid 0.4 s + the commander's rise 0.2 s at game speed, with room
            if (type == "jeep" || type == "light")
                Check($"{type}: its crew figure ({(type == "jeep" ? "the seated driver" : "the hatch commander, lid open " + u.LidOpen)}) up {fv.CrewFigureUp} and held off while the camera draws: {fv.CrewHeld} renderers in the last picture, {fv.FiguresHeld} figure(s) held",
                      fv.CrewFigureUp && fv.CrewHeld > 0);
            Watch(v); yield return Frames(3);
            Check($"{type}: no part of a unit or a crew figure drawn within 1 m of the eye, the gun copy aside: at most {rNearMax} in {rFrames} pictures ({rNearNames})", rFrames >= 2 && rNearMax == 0);
            if (type == "light")
            {
                Watch(v); yield return Sample(0.4f, () => u.LidOpen = true);
                Check($"light: still none within 1 m 0.4 s later, the commander up {fv.CrewFigureUp}: at most {rNearMax} in {rFrames} pictures ({rNearNames})", rFrames >= 2 && rNearMax == 0 && fv.CrewFigureUp);
                u.LidOpen = false;
            }
            rWatch = null;
            float gunOff = fv.GunParts > 0 ? Vector3.Angle(fv.GunAxisWorld, cam.transform.forward) : 999f;
            bool must = GunMust.Contains(type);
            if (fv.GunParts > 0 || must) Check($"{type}: its gun copy drawn ({fv.GunParts} parts of {fv.GunRootName}{(fv.GunWhy.Length > 0 ? ", " + fv.GunWhy : "")}), its line {gunOff:0.0} deg off the view's centre", fv.GunParts > 0 && gunOff <= 12f);
            else Log($"INFO {type}: no gun copy ({fv.GunWhy}); not one of the types that must have it");
            if (fv.GunParts > 0)
            {   // fps_view_fix (b): what the copy took, where it sits on the screen, how much of it, how bright
                var box = GunBox(fv); float area = 0f, luma = 0f; int px = 0;
                yield return GunPixels(fv, (ar, lu, n) => { area = ar; luma = lu; px = n; });
                if (fv.GunSight && gunPixBox.width > 0f) { Log($"INFO {type}: the barrel copy's box by its renderers' bounds x {box.xMin:0.00} .. {box.xMax:0.00}, y {box.yMin:0.00} .. {box.yMax:0.00}; by its own pixels x {gunPixBox.xMin:0.00} .. {gunPixBox.xMax:0.00}, y {gunPixBox.yMin:0.00} .. {gunPixBox.yMax:0.00} (the row reads the pixels: E3)"); box = gunPixBox; }
                gunLuma[type] = luma;
                Log($"INFO {type}: gun copy parts kept: {fv.GunKept}; left out: {(fv.GunDropped.Length > 0 ? fv.GunDropped : "-")}");
                if (fv.GunSight)
                {
                    bool thin = !fv.GunWhy.StartsWith("no thin");
                    Check($"{type}: the gun copy is the barrel only, like a tank sight: kept {fv.GunKept}; left out {(fv.GunDropped.Length > 0 ? fv.GunDropped : "-")} (every kept part at least {OzFpsView.BarrelThin:0.0} x longer than thick)", thin && fv.GunKept.Length > 0);
                    Check($"{type}: the barrel copy at the lower middle: its box x {box.xMin:0.00} .. {box.xMax:0.00}, y {box.yMin:0.00} .. {box.yMax:0.00} of the screen (centre x 0.38 .. 0.62, from the bottom edge, top at most 0.42, at most 0.36 wide); its own pixels {area * 100f:0.0} % of the screen (at most 6 %), brightness {luma:0.00} ({px} samples)",
                          box.width > 0f && box.center.x >= 0.38f && box.center.x <= 0.62f && box.yMin <= 0.03f && box.yMax <= 0.42f && box.width <= 0.36f && area <= 0.06f && px > 0);
                }
                else Check($"{type}: the robot's gun copy in the lower right as before: its box x {box.xMin:0.00} .. {box.xMax:0.00}, y {box.yMin:0.00} .. {box.yMax:0.00}; its own pixels {area * 100f:0.0} % of the screen, brightness {luma:0.00} ({px} samples)",
                           box.width > 0f && box.center.x >= 0.5f && box.yMin <= (type == "tough" ? 0.12f : 0.05f) && px > 0);   // E3: the tough's launcher sits on its shoulder (run 3: from 0.09 of the height; robots' copies are built as before)
            }
            var arIn = Rings();
            Check($"{type}: the HUD hidden ({(b.hud ? "it was shown" : "it was hidden already")}), the range rings off ({(arIn ? (arIn.enabled ? "ON" : "off") : "none on the input")}), the cursor locked and hidden, field of view {cam.fieldOfView:0} near {cam.nearClipPlane:0.00}",
                  (hud == null || !hud.hudVisible) && !(arIn && arIn.enabled) && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible && Mathf.Abs(cam.fieldOfView - OzFpsView.FovDeg) < 0.01f && Mathf.Abs(cam.nearClipPlane - OzFpsView.NearClip) < 0.001f);
            int gf = OzFpsView.GuiFrames; yield return Frames(3);
            Check($"{type}: the crosshair drawn ({OzFpsView.GuiFrames - gf} frames of 3)", OzFpsView.GuiFrames - gf >= 2);
            string shot = Path.Combine(Folder(), "fps_" + type + ".png"); ScreenCapture.CaptureScreenshot(shot); shots.Add(shot);
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(shot, 3f);
            // ---- mouse look
            float y0 = fv.Yaw;
            yield return MouseBy(new Vector2(400f, 0f));
            float dy = Mathf.DeltaAngle(y0, fv.Yaw), camYaw = Mathf.DeltaAngle(fv.Yaw, cam.transform.eulerAngles.y);
            Check($"{type}: 400 px right = {dy:0.0} deg of turn (want {400f * RtsCamera.FreeLookSpeed:0.0}), the camera with it ({camYaw:0.00} off)", Mathf.Abs(dy - 400f * RtsCamera.FreeLookSpeed) < 0.6f && Mathf.Abs(camYaw) < 0.5f);
            yield return MouseBy(new Vector2(0f, 2000f));
            float up = fv.Pitch, camUp = -Mathf.DeltaAngle(0f, cam.transform.eulerAngles.x);
            yield return MouseBy(new Vector2(0f, -5000f));
            float down = fv.Pitch;
            Check($"{type}: look up clamps at {up:0.0} (camera {camUp:0.0}), down at {down:0.0} (want {OzFpsView.PitchMax:0} / {OzFpsView.PitchMin:0})", Mathf.Abs(up - OzFpsView.PitchMax) < 0.01f && Mathf.Abs(camUp - OzFpsView.PitchMax) < 0.5f && Mathf.Abs(down - OzFpsView.PitchMin) < 0.01f);
            yield return HullPixels(type, fv);                                                               // fps_view_fix fix round 1: the hold read by pixels, the view 60 deg down
            yield return MouseBy(new Vector2(0f, 400f));                                                     // -60 + 60: level again
            // ---- walk
            Key fwdKey = OzBindings.KeyOf(OzAction.ScrollUp), rightKey = OzBindings.KeyOf(OzAction.ScrollRight), backKey = OzBindings.KeyOf(OzAction.ScrollDown);
            var look = Quaternion.Euler(0f, fv.Yaw, 0f) * Vector3.forward; var right = Quaternion.Euler(0f, fv.Yaw, 0f) * Vector3.right;
            Vector3 p0 = v.transform.position; int m0 = OzFpsView.MoveOrders, s0 = OzFpsView.StopOrders;
            float snd0 = Time.unscaledTime; bool surfOn = false; var adir = AudioDirector.I; string surfKey = "surf:" + u.Id;   // fix round 2: its own walk / tracks sounds
            Watch(null);                                                                                     // fix round 3: nothing drawn within 1 m of the eye while it walks (its own squad round it), read in every picture
            System.Action surf = () => { surfOn |= adir && adir.LoopActive(surfKey); };
            Keys(fwdKey); yield return Sample(0.3f, surf);
            bool silent = !OzBindings.Held(OzAction.ScrollUp);
            float wSec = gun ? 1f : robot ? 2f : 4f;                                                         // fps_view_fix: a vehicle turns its hull onto the look first (the look was turned 60 deg above)
            yield return Sample(wSec - 0.3f, surf);
            Keys(); yield return Frames(3);
            var moved = v.transform.position - p0; moved.y = 0f; float along = Vector3.Dot(moved, look);
            float dirOff = moved.magnitude > 0.2f ? Vector3.Angle(moved, look) : 999f;
            // fps_view_fix (check fault 'focus on the unit', E2a: 0.050 - 0.062 m against a 0.05 m bar = one frame of walking at the editor's frame
            // rate): the focus must be exactly what the view itself set in its last LateUpdate (the keys never scrolled it), and that was the unit
            var vp = v.transform.position; float focusGap = new Vector2(rts.focus.x - OzFpsView.LastFocusSet.x, rts.focus.z - OzFpsView.LastFocusSet.z).magnitude;
            bool camStill = focusGap < 0.001f && OzFpsView.LastFocusFrame >= Time.frameCount - 1;
            float unitGap = new Vector2(rts.focus.x - vp.x, rts.focus.z - vp.z).magnitude;
            if (gun)
                Check($"{type}: W on a gun: no move order ({OzFpsView.MoveOrders - m0}), it stayed ({moved.magnitude:0.00} m), the RTS focus the view's own {camStill} ({focusGap:0.000} m), the scroll key silent {silent}", OzFpsView.MoveOrders == m0 && moved.magnitude < 0.2f && camStill && silent);
            else
            {
                float want = robot ? 1f : 1.5f; int orders = OzFpsView.MoveOrders - m0, most = 1 + Mathf.FloorToInt(moved.magnitude * 8f / 24f);   // fix round 3: about one order per 2 tiles of a straight walk (not 4 a second)
                Check($"{type}: W held {wSec:0} s: {orders} move order(s) (1 .. {most}: one per 2 tiles, the order under way holds), {along:0.00} m the way it looks (at least {want:0.0}), {dirOff:0} deg off the look (at most 35: W is forward in the view), the RTS focus the view's own {camStill} ({focusGap:0.000} m; {unitGap:0.000} m from the unit = its last frame's travel), the scroll key silent {silent}",
                      orders >= 1 && orders <= most && along >= want && dirOff <= 35f && camStill && silent);
                yield return WaitFor(() => u.Waypoints.Count == 0 || GameGrab(u), 2f);
                Check($"{type}: released: {OzFpsView.StopOrders - s0} stop order, waypoints left {u.Waypoints.Count}{WpText(u)}", OzFpsView.StopOrders - s0 == 1 && (u.Waypoints.Count == 0 || GameGrab(u)));
                yield return Real(0.6f);
                if (GameGrab(u)) { pi.FpsStop(u); yield return Frames(2); }                         // the game's own grab of a neutral vehicle stopped again (test only): the D row starts from standing
                if (!robot)
                {   // E3 (check only): L17's vehicle ground round the place is about 15 m across; after W (6 - 9 m on) the D point lay off it and the jeep's path went round (-6.4 m), the medium tank stood. The vehicle is driven back to the place by a plain game order first (the view stays on, its look unchanged).
                    W.SendWaypoints(me, u, new[] { ZWaypoint.Move(x, y, false) }); yield return Real(0.3f);
                    yield return WaitFor(() => u.Waypoints.Count == 0 && !u.IsMoving, 8f); yield return Real(0.3f);
                    Log($"INFO {type}: driven back to its place for the D row: {Mathf.Sqrt((u.CenterX - x) * (u.CenterX - x) + (u.CenterY - y) * (u.CenterY - y)) / 8f:0.0} m from it");
                }
                p0 = v.transform.position; m0 = OzFpsView.MoveOrders;
                Keys(rightKey); yield return Sample(robot ? 1.5f : 3f, surf); Keys(); yield return Frames(3);
                var side = v.transform.position - p0; side.y = 0f;
                Check($"{type}: D held {(robot ? 1.5f : 3f):0.0} s: {OzFpsView.MoveOrders - m0} move order(s), {Vector3.Dot(side, right):0.00} m to the view's right", OzFpsView.MoveOrders - m0 >= 1 && Vector3.Dot(side, right) >= 0.4f);
                yield return WaitFor(() => u.Waypoints.Count == 0 || GameGrab(u), 2f);
                if (robot)
                {   // fix round 3: S walks the robot back into its own squad (they stand behind and round it)
                    yield return Real(0.4f);
                    if (GameGrab(u)) { pi.FpsStop(u); yield return Frames(2); }
                    p0 = v.transform.position; m0 = OzFpsView.MoveOrders;
                    Keys(backKey); yield return Sample(1.5f, surf); Keys(); yield return Frames(3);
                    var back = v.transform.position - p0; back.y = 0f;
                    Check($"{type}: S held 1.5 s: {OzFpsView.MoveOrders - m0} move order(s), {-Vector3.Dot(back, look):0.00} m back", OzFpsView.MoveOrders - m0 >= 1 && -Vector3.Dot(back, look) >= 0.3f);
                    yield return WaitFor(() => u.Waypoints.Count == 0 || GameGrab(u), 2f);
                }
                Check($"{type}: nothing of a unit (its own squad of {1 + u.Minions.Count} too) or a crew figure drawn within 1 m of the eye while W, D{(robot ? ", S" : "")} were held: at most {rNearMax} in {rFrames} pictures ({rNearNames}); {fv.UnitsHeld} unit view(s) held off now, {OzFpsView.UnitsTaken} taken since the Play started",
                      rFrames > 0 && rNearMax == 0);
                if (type == "grunt")
                {   // fix round 2: its own steps under the eye are heard (AudioDirector: the walk marked heard by the view's orders, the step gates take its feet as in view)
                    var es = OzSoundLog.Since(snd0);
                    int steps = es.Count(e => e.key != null && e.key.StartsWith("rob_step") && e.outcome != null && e.outcome.StartsWith("PLAY"));
                    int dropped = es.Count(e => e.key != null && (e.key.StartsWith("rob_step") || e.key == "rob_servo" || e.key == "rob_run_start") && e.outcome == "DROP off screen");
                    int march = es.Count(e => e.key == "rob_squad_march" && e.outcome != null && e.outcome.StartsWith("LOOP"));   // E2a: a squad of 3 or more walks to ONE march loop, not single steps (AudioDirector.UpdateRobots: 'never 4 x footsteps'); fix round 3 made the grunt a squad of 3
                    Check($"grunt: its own steps heard while W, D and S were held: {steps} step sounds played, {march} squad march loop start(s) (its squad of {1 + u.Minions.Count}), {dropped} of its walk sounds dropped off screen", (steps >= 1 || march >= 1) && dropped == 0);
                }
                if (type == "light")
                    Check($"light: its own tracks loop ({surfKey}) played while W and D were held: {surfOn}", surfOn);
            }
            if (type == "grunt") yield return OwnShot(u, v, fv);                                           // fix round 2: its own shot heard with the target behind the eye
            // ---- Esc
            bool pauseBefore = hud && hud.Pause && hud.Pause.IsOpen;
            yield return Tap(Key.Escape); yield return Frames(3);
            Check($"{type}: Esc leaves ({OzFpsView.LastLeave}), the pause menu stays shut", !OzFpsView.On && OzFpsView.LastLeave == "Esc" && !pauseBefore && !(hud && hud.Pause && hud.Pause.IsOpen));
            yield return Restored(type, u, b, true, true, before);
        }

        /// <summary>fps_view_fix (check fault 'waypoints left 1', E2a: the first one mode Enter of the neutral light tank #349 on L17): the game's own grab
        /// of a neutral vehicle or gun close by (DV38 / DV84: a unit held by the player still takes a vehicle within World.HeldVehicleReach, 220 px)
        /// after the stop; the X key does the same. Not the view's.</summary>
        /// <summary>E3 (check only): run 2's fog grunt took a neutral vehicle by the game's close grab during the RTS camera's readings (L17 has no
        /// place 340 px from every neutral vehicle), the view left ("the unit got in") and the row's Esc then opened the pause menu, which the ending
        /// row's Alt+F could not pass. While set, the grab of this unit is stopped in every frame (test only, as the release row does).</summary>
        ZObject noGrab;
        void Update() { if (noGrab != null && W != null && !noGrab.Destroyed && GameGrab(noGrab)) { pi.FpsStop(noGrab); grabsStopped++; } }
        int grabsStopped;
        bool GameGrab(ZObject u)
        {
            if (u.Waypoints.Count == 0) return false;
            var w = u.Waypoints[0]; var t = W.Get(w.refId);
            return w.mode == WpMode.Enter && !w.playerGiven && t != null && t.Owner == Team.Neutral && (t.Kind == ZKind.Vehicle || t.Kind == ZKind.Cannon);
        }

        string WpText(ZObject u)
        {
            if (u.Waypoints.Count == 0) return "";
            var w = u.Waypoints.First(); var t = W.Get(w.refId);
            return $" (the first one mode {w.mode}, target #{w.refId}{(t != null ? " " + t.Type + " " + t.Owner : "")}, attackTo {w.attackTo}, given by the player {w.playerGiven}{(GameGrab(u) ? ": the game's own grab of a neutral vehicle close by (DV38 / DV84), as after the X key" : "")})";
        }

        /// <summary>Fix round 2: an enemy dummy 10 m off (in the grunt's 120 px; it never fires: test only), the view turned 120 deg away from it. The
        /// grunt turns and fires by itself; its own shots (the sound log rows of the robot shot keys at the listener's distance from its muzzle, 1.6 m
        /// either way: other units stand 15 m or more away) are played, none "DROP off screen", "DROP too far off screen" or "PLAY distant".</summary>
        IEnumerator OwnShot(ZObject u, UnitView v, OzFpsView fv)
        {
            ZObject e = null;
            for (int a = 0; a < 8 && e == null; a++)
            {
                int ex = u.CenterX + (int)(80f * Mathf.Cos(a * 0.785f)), ey = u.CenterY + (int)(80f * Mathf.Sin(a * 0.785f));
                if (Walkable(ex, ey)) e = Make("robot", "grunt", ex, ey, foe, 1);
            }
            if (e == null) { Check("grunt shot: an enemy dummy 10 m from the grunt", false); yield break; }
            e.Damage = 0; e.AttackRadius = 0; e.MaxHealth = 10000000; e.Health = 10000000;               // test only: it takes the shots and never fires
            yield return Real(0.8f);
            var ev = ViewOf(e); var ep = ev ? ev.transform.position : ZGame.PxToWorld(e.CenterX, e.CenterY);
            var d = ep - cam.transform.position; float k = RtsCamera.FreeLookSpeed;
            fv.LookBy(new Vector2(Mathf.DeltaAngle(fv.Yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 120f) / k, -fv.Pitch / k));   // the enemy 120 deg off the look, level
            yield return Frames(3);
            float off = Vector3.Angle(new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z), new Vector3(d.x, 0f, d.z));
            AudioListener lis = null; foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.isActiveAndEnabled) { lis = l; break; }
            float t0 = Time.unscaledTime, fire0 = v.LastFireAt, dSum = 0f; int fires = 0, dn = 0;
            yield return Sample(6f, () =>
            {
                if (v.LastFireAt != fire0) { fire0 = v.LastFireAt; fires++; }
                if (lis) { dSum += Vector3.Distance(lis.transform.position, v.transform.position + Vector3.up * 1.1f); dn++; }
            });
            float dOwn = dn > 0 ? dSum / dn : -1f;
            var shotKeys = new[] { "bolt", "rifle", "burst", "sniper" };
            var mine = OzSoundLog.Since(t0).Where(x => x.src == "CA" && shotKeys.Contains(x.key) && dOwn > 0f && Mathf.Abs(x.dist - dOwn) < 1.6f).ToList();
            int played = mine.Count(x => x.outcome != null && x.outcome.StartsWith("PLAY") && !x.outcome.StartsWith("PLAY distant"));
            int lost = mine.Count(x => x.outcome == "DROP off screen" || x.outcome == "DROP too far off screen" || (x.outcome != null && x.outcome.StartsWith("PLAY distant")));
            Check($"grunt shot: the enemy {off:0} deg off the look; {fires} shots of its own; {played} of its shot sounds played in full, {lost} dropped or distant (listener {dOwn:0.0} m from its muzzle)",
                  off > 100f && fires >= 1 && played >= 1 && lost == 0);
            Kill(W, e); yield return Real(0.3f);
        }

        /// <summary>Fix round 3: how long the fire on the move row counts the unit's own shots standing still and walking (real s, game speed 1).</summary>
        const float FireSeconds = 6f;

        /// <summary>Fix round 3, fps_view_fix (d): drive / walk while firing. A new own unit (a grunt squad; every vehicle type: light, medium tank,
        /// heavy, mech, jeep, APC) in the view, an enemy dummy (one robot, never fires: test only) 4 m ahead of it and 7 m to the side of the line it
        /// will drive. Its own shots are counted standing still for FireSeconds s (from its first shot), then while W is held straight past the dummy
        /// for FireSeconds s (or until it drove 160 px): the DRAWN unit and its sim point drive at its normal speed (Zod's move_speed), no ATTACK or
        /// AGRO waypoint ever stands in front of the walk (the attack order that stopped it, fps_view_fix d), it keeps shooting (at least 60 % of the
        /// standing rate where it fired 4 or more times standing; the jeep's own rate falls on the move in the rules: 35 of 54 on the plain sim),
        /// about one move order per 2 tiles (each one drops its target, DV16).</summary>
        IEnumerator FireWalk(string cat, string type)
        {
            string tag = type + " fire on the move";
            if (!FindSpot(out int x, out int y, cat != "robot")) { Check($"{tag}: a free place near the camera ({LastSpotWhy})", false); yield break; }
            Log($"INFO {tag}: the place ({x},{y}) px, {(cat != "robot" ? "vehicle ground (a 32 px box free on the vehicle grid at every point 120 px round)" : "robot ground")}{(NoNeutralNear(x, y) ? ", no neutral vehicle or gun within 340 px" : ", a neutral vehicle or gun within 340 px (none farther on this map)")}");
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(0.4f);
            var u = Make(cat, type, x, y, me); yield return Real(1.2f);
            var v = ViewOf(u); if (u == null || !v) { Check(tag + ": made with its view", false); yield break; }
            var b = Snap(v);
            yield return Enter(tag, u, b);
            var fv = OzFpsView.I; if (!OzFpsView.On || !fv) yield break;
            fv.LookBy(new Vector2(0f, -fv.Pitch / RtsCamera.FreeLookSpeed)); yield return Frames(2);          // level; the walk goes the way the view looks
            var look = Quaternion.Euler(0f, fv.Yaw, 0f) * Vector3.forward; var right = Quaternion.Euler(0f, fv.Yaw, 0f) * Vector3.right;
            ZObject e = null;
            foreach (float sd in new[] { 56f, -56f })
            {
                int ex = u.CenterX + Mathf.RoundToInt(look.x * 32f + right.x * sd), ey = u.CenterY - Mathf.RoundToInt(look.z * 32f + right.z * sd);   // Zod px: y runs south (world -z)
                if (Walkable(ex, ey)) { e = Make("robot", "grunt", ex, ey, foe, 1); break; }
            }
            if (e == null) { Check(tag + ": an enemy dummy beside the walk line", false); yield return Tap(Key.Escape); yield return Frames(3); yield break; }
            e.Damage = 0; e.AttackRadius = 0; e.MaxHealth = 10000000; e.Health = 10000000;                   // test only: it takes the shots and never fires
            yield return Real(0.8f);
            int range = u.AttackRadius; float fire0 = v.LastFireAt;
            // standing still in the view: its own shots from its first one
            yield return WaitFor(() => v.LastFireAt != fire0, 5f);
            bool opened = v.LastFireAt != fire0; int stand = 0; fire0 = v.LastFireAt;
            yield return Sample(FireSeconds, () => { if (v.LastFireAt != fire0) { fire0 = v.LastFireAt; stand++; } });
            // W held: straight on past the dummy
            Vector3 p0 = v.transform.position; int sx0 = u.CenterX, sy0 = u.CenterY, m0 = OzFpsView.MoveOrders, walk = 0, frames = 0, outFrames = 0, attackFrames = 0; string attackSeen = "-";
            Keys(OzBindings.KeyOf(OzAction.ScrollUp));
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < FireSeconds && Vector3.Dot(v.transform.position - p0, look) * 8f < 160f)
            {
                if (v.LastFireAt != fire0) { fire0 = v.LastFireAt; walk++; }
                frames++; long dx = u.CenterX - e.CenterX, dy = u.CenterY - e.CenterY; if (dx * dx + dy * dy > (long)range * range) outFrames++;
                if (OzFpsView.MoveOrders > m0 && Time.unscaledTime - t0 > 0.15f && u.Waypoints.Count > 0 && (u.Waypoints[0].mode == WpMode.Attack || u.Waypoints[0].mode == WpMode.Agro)) { attackFrames++; attackSeen = u.Waypoints[0].mode + " #" + u.Waypoints[0].refId; }   // E3: not before the first move order reached the sim (run 2: the standing attack on the dummy seen on the W row's first frame)
                yield return null;
            }
            float walkT = Mathf.Max(0.1f, Time.unscaledTime - t0); int orders = OzFpsView.MoveOrders - m0;
            Keys(); yield return Frames(3);
            var moved = v.transform.position - p0; moved.y = 0f; int most = 1 + Mathf.FloorToInt(moved.magnitude * 8f / 24f);
            float drawn = Vector3.Dot(moved, look), sim = ((u.CenterX - sx0) * look.x - (u.CenterY - sy0) * look.z) / 8f;
            float nominal = u.MoveSpeed / 8f * Mathf.Max(0.25f, g.gameSpeed) * walkT;                       // Zod's move_speed (px/s), 8 px a metre
            float standRate = stand / FireSeconds, walkRate = walk / walkT, share = standRate > 0f ? walkRate / standRate : 0f, outShare = frames > 0 ? (float)outFrames / frames : 1f;
            bool rateOk = stand >= 4 ? share >= 0.6f : walk >= 1;
            Check($"{type}: drive / walk while firing: W held {walkT:0.0} s past the enemy: the DRAWN unit {drawn:0.00} m and its sim point {sim:0.00} m the way it looks, its normal speed {nominal:0.00} m (drawn at least 60 %, sim at least 80 %); an ATTACK / AGRO waypoint in front on {attackFrames} of {frames} frames ({attackSeen}; none wanted); {orders} move order(s) (1 .. {most})",
                  opened && drawn >= 0.6f * nominal && sim >= 0.8f * nominal && attackFrames == 0 && orders >= 1 && orders <= most);
            Check($"{type}: it keeps firing on the way: standing still {stand} shots of its own in {FireSeconds:0} s (its first shot {(opened ? "seen" : "NEVER came")}), walking {walk} in {walkT:0.0} s: {share * 100f:0} % of the standing rate (at least 60 % when it fired 4 or more standing, else at least one shot); the enemy out of its {range} px on {outShare * 100f:0} % of the frames",
                  opened && rateOk);
            yield return WaitFor(() => u.Waypoints.Count == 0, 2f);
            Kill(W, e); yield return Real(0.3f);
            yield return Tap(Key.Escape); yield return Frames(3);
            Check($"{type} fire on the move: Esc leaves ({OzFpsView.LastLeave})", !OzFpsView.On && OzFpsView.LastLeave == "Esc");
        }

        /// <summary>Fix round 3 (the review's major fault): the level ends for real while the view is on (the enemy fort destroyed, as a robot walking
        /// into it does: ZWorld eliminates the team, OpenZEnding begins and takes the camera). The view leaves with "the level ends", the RTS camera's
        /// follow stays the ending's target, the camera glides toward the fort, and the ending's own state shows its camera driven all the way into
        /// its orbit round the fort (never released before it) and the orbit running. Test only and last: the match is over after it.</summary>
        IEnumerator EndingRow()
        {
            const string tag = "level end for real";
            ZObject fort = null; foreach (var o in W.Objects) if (o != null && !o.Destroyed && o.IsFort && o.Owner == foe) { fort = o; break; }
            var en = OpenZEnding.I;
            const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var fActive = typeof(OpenZEnding).GetField("camActive", bf); var fOrbit = typeof(OpenZEnding).GetField("orbitT", bf); var fTarget = typeof(OpenZEnding).GetField("camTarget", bf);
            if (fort == null || !en || !OpenZEnding.Enabled || !OpenZEnding.DriveCamera || fActive == null || fOrbit == null || fTarget == null)
            { Check($"{tag}: an enemy fort {fort != null}, the ending {(bool)en} (on {OpenZEnding.Enabled}, drives the camera {OpenZEnding.DriveCamera}), its camActive / orbitT / camTarget read {fActive != null} {fOrbit != null} {fTarget != null}", false); yield break; }
            if (!FindSpot(out int x, out int y)) { Check(tag + ": a free place near the camera", false); yield break; }
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(0.4f);
            var u = Make("robot", "grunt", x, y, me); yield return Real(1.2f);
            var v = ViewOf(u); if (u == null || !v) { Check(tag + ": a grunt made with its view", false); yield break; }
            var b = Snap(v);
            yield return Enter(tag, u, b);
            if (!OzFpsView.On) yield break;
            yield return Real(0.5f);
            var fc = g.CenterWorld(fort); var fortFrame = new Vector3(fc.x, 0f, fc.z + OpenZEnding.CamNorth);   // where OpenZEnding's glide ends
            int l0 = OzFpsView.Leaves; float fall = Time.unscaledTime;
            Kill(W, fort);                                                                                    // test only: the enemy fort destroyed, its team eliminated
            yield return WaitFor(() => !OzFpsView.On, 5f); yield return Frames(3);
            var tgt = fTarget.GetValue(en) as Transform; bool active = (bool)fActive.GetValue(en), follows = tgt && rts.follow == tgt;
            Check($"{tag}: the view left (\"{OzFpsView.LastLeave}\"), the ending running {OpenZEnding.Running}, its camera driven {active}, the RTS camera follows its target {follows} ({(rts.follow ? rts.follow.name : "none")}), the leave kept the camera {OzFpsView.LastLeaveKeptCamera}",
                  !OzFpsView.On && OzFpsView.Leaves == l0 + 1 && OzFpsView.LastLeave == "the level ends" && OpenZEnding.Running && active && follows && OzFpsView.LastLeaveKeptCamera);
            // the screen given back as on every leave (the focus, height and tilt are the ending's from here on: not compared)
            var ar = Rings();
            Check($"{tag}: back: the cursor free and shown, the gun copy gone {!cam.transform.Find("OzFpsGun")}, field of view {cam.fieldOfView:0.0} near {cam.nearClipPlane:0.00} (were {b.fov:0.0} {b.near:0.00}), the range rings {(ar && ar.enabled)} (were {b.rings}), {OzFpsView.LastRestored} renderers given back, {OzFpsView.LastStillOff} still off",
                  Cursor.lockState != CursorLockMode.Locked && Cursor.visible && !cam.transform.Find("OzFpsGun") && Mathf.Abs(cam.fieldOfView - b.fov) < 0.001f && Mathf.Abs(cam.nearClipPlane - b.near) < 0.0001f
                  && (ar && ar.enabled) == b.rings && OzFpsView.LastRestored > 0 && OzFpsView.LastStillOff == 0);
            float d0 = new Vector2(rts.focus.x - fortFrame.x, rts.focus.z - fortFrame.z).magnitude;
            yield return Real(1f);
            float d1 = new Vector2(rts.focus.x - fortFrame.x, rts.focus.z - fortFrame.z).magnitude;
            Check($"{tag}: the camera glides to the fort: {d0:0.0} m from its frame, 1 s later {d1:0.0} m", d1 < d0 - 1f || d1 < 2f);
            int freeFrames = 0; bool orbit = false;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 40f)
            {
                if (!(bool)fActive.GetValue(en) || rts.follow != tgt) freeFrames++;                       // released (ReleaseCamera) or the follow lost before the orbit
                if ((float)fOrbit.GetValue(en) >= 0f) { orbit = true; break; }
                yield return null;
            }
            Check($"{tag}: the orbit round the fort began {(orbit ? (Time.unscaledTime - fall).ToString("0.0") + " s after the fall" : "NEVER (40 s)")}, the camera driven and following the ending's target on every frame before it (released on {freeFrames})", orbit && freeFrames == 0);
            if (!orbit) yield break;
            float o1 = (float)fOrbit.GetValue(en), y1 = rts.yaw; yield return Real(1f);
            float o2 = (float)fOrbit.GetValue(en), y2 = rts.yaw; bool active2 = (bool)fActive.GetValue(en), follows2 = rts.follow == tgt;
            Check($"{tag}: the orbit runs: {o1:0.00} -> {o2:0.00} s, the camera's yaw {y1:0.00} -> {y2:0.00} deg, still driven {active2} and following the target {follows2} (no release)",
                  o2 > o1 + 0.5f && Mathf.Abs(Mathf.DeltaAngle(y1, y2)) > 0.05f && active2 && follows2);
        }

        /// <summary>Fix round 2: the CONTROLS HELP card (its own canvas) open when Alt+F goes in is closed by the entry, with its own close sound.</summary>
        IEnumerator HelpRow()
        {
            if (!hud || !hud.Help) { Check("CONTROLS HELP: the card on the HUD", false); yield break; }
            if (!FindSpot(out int x, out int y)) { Check("CONTROLS HELP: a free place near the camera", false); yield break; }
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(0.4f);
            var u = Make("robot", "grunt", x, y, me); yield return Real(1.2f);
            var v = ViewOf(u); if (u == null || !v) { Check("CONTROLS HELP: a grunt made with its view", false); yield break; }
            var b = Snap(v);
            hud.Help.Toggle(true); yield return Frames(3);
            bool was = hud.Help.IsOpen; int h0 = OzFpsView.HelpClosed;
            yield return Enter("CONTROLS HELP", u, b);
            Check($"CONTROLS HELP: open before {was}, closed by the entry {!hud.Help.IsOpen} ({OzFpsView.HelpClosed - h0} closed by the view)", was && OzFpsView.On && !hud.Help.IsOpen && OzFpsView.HelpClosed == h0 + 1);
            yield return Tap(Key.Escape); yield return Frames(3);
            Check($"CONTROLS HELP: Esc leaves the view ({OzFpsView.LastLeave}), the card stays shut {!hud.Help.IsOpen}", !OzFpsView.On && !hud.Help.IsOpen);
        }

        /// <summary>Every leave: the RTS camera, the screen and the keys as before the view. fps_view_fix (c): every model renderer of the unit and its
        /// crew figure in the state it had before the entry, renderer by renderer (a change only where the game itself changed it meanwhile: the
        /// view found it held off by other code in its last picture, or the BS01 kit holds a seated robot's pack off), and the view holds none.</summary>
        IEnumerator Restored(string tag, ZObject u, Before b, bool keys, bool hudCheck = true, Dictionary<Renderer, (bool off, bool en, bool act)> before = null)
        {
            var v = ViewOf(u); var at = OzFpsView.LastLeavePos;
            var fd = new Vector2(rts.focus.x - at.x, rts.focus.z - at.z).magnitude;
            float tilt = Mathf.DeltaAngle(cam.transform.eulerAngles.x, rts.Tilt);
            bool gunGone = !cam.transform.Find("OzFpsGun");
            var so = pi.GetComponent<SelectionOverlay>(); var ar = Rings();
            bool alive = u != null && !u.Destroyed && u.KillAt < 0 && v && !v.Dying;
            Check($"{tag}: back: focus {fd:0.00} m from the unit, height {rts.height:0.00} (was {b.height:0.00}), tilt {cam.transform.eulerAngles.x:0.0} ({tilt:0.00} off the RTS tilt), field of view {cam.fieldOfView:0.0} near {cam.nearClipPlane:0.00} (were {b.fov:0.0} {b.near:0.00})",
                  fd < 1.5f && Mathf.Abs(rts.height - b.height) < 0.01f && Mathf.Abs(rts.pitch - b.pitch) < 0.001f && Mathf.Abs(tilt) < 0.5f && Mathf.Abs(cam.fieldOfView - b.fov) < 0.001f && Mathf.Abs(cam.nearClipPlane - b.near) < 0.0001f);
            string cmp = "-"; int same = 0, game = 0, bad = 0;
            if (alive && before != null) cmp = Compare(before, States(u, v), out same, out game, out bad);
            Check($"{tag}: back: the cursor free and shown, the gun copy gone {gunGone}, the selection rings {(so && so.enabled)} (were {b.sel}), the range rings {(ar && ar.enabled)} (were {b.rings}), {OzFpsView.LastRestored} renderers given back at the last picture's end, the view holds {OzFpsView.HeldNow} now ({OzFpsView.LastStillOff} still off){(alive ? (before != null ? $"; every renderer before / after: {same} the same, {game} changed by the game, {bad} not explained ({cmp})" : $", the model's held-off count {HeldOff(v)} (was {b.heldOff})") : " (the unit is gone)")}",
                  Cursor.lockState != CursorLockMode.Locked && Cursor.visible && gunGone && (so && so.enabled) == b.sel && (ar && ar.enabled) == b.rings && (OzFpsView.LastRestored > 0 || !alive) && OzFpsView.LastStillOff == 0 && OzFpsView.HeldNow == 0
                  && (!alive || (before != null ? bad == 0 && same > 0 : HeldOff(v) == b.heldOff)));
            if (hudCheck) Check($"{tag}: back: the HUD {(hud && hud.hudVisible ? "shown" : "hidden")} as before ({(b.hud ? "shown" : "hidden")})", hud == null || hud.hudVisible == b.hud);
            if (!keys) yield break;
            var f0 = rts.focus; Keys(OzBindings.KeyOf(OzAction.ScrollUp)); yield return Real(0.4f);
            bool held = OzBindings.Held(OzAction.ScrollUp); Keys(); yield return Frames(2);
            float went = rts.focus.z - f0.z;
            Check($"{tag}: back: the scroll key reads held {held}, W moved the RTS camera {went:0.00} m north", held && went > 0.3f);
        }

        // ------------------------------------------------------------------ the other leave paths
        IEnumerator Other(string tag, string cat, string type, System.Func<ZObject, IEnumerator> leave, string want, bool keys, bool flyFirst = false)
        {
            if (!FindSpot(out int x, out int y, cat != "robot")) { Check($"{tag}: a free place near the camera ({LastSpotWhy})", false); yield break; }
            Log($"INFO {tag}: the place ({x},{y}) px, {(cat != "robot" ? "vehicle ground (a 32 px box free on the vehicle grid at every point 120 px round)" : "robot ground")}{(NoNeutralNear(x, y) ? ", no neutral vehicle or gun within 340 px" : ", a neutral vehicle or gun within 340 px (none farther on this map)")}");
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(0.4f);
            var u = Make(cat, type, x, y, me); yield return Real(1.2f);
            var v = ViewOf(u); if (u == null || !v) { Check(tag + ": made with its view", false); yield break; }
            var b = Snap(v); var before = States(u, v);
            if (flyFirst) { rts.FlyTo(rts.focus + new Vector3(70f, 0f, 40f)); yield return null; }             // fix round 1: a fly-to under way when the view goes in (Space, a minimap click)
            yield return Enter(tag, u, b);
            if (!OzFpsView.On) yield break;
            yield return Real(flyFirst ? 0.2f : 0.5f);
            int l0 = OzFpsView.Leaves;
            yield return leave(u);
            yield return WaitFor(() => !OzFpsView.On, 20f); yield return Frames(3);
            Check($"{tag}: the view left: \"{OzFpsView.LastLeave}\" (want \"{want}\")", !OzFpsView.On && OzFpsView.Leaves == l0 + 1 && OzFpsView.LastLeave.StartsWith(want));
            if (flyFirst) { yield return Real(1.2f); Check($"{tag}: no fly-to goes on after the leave (flying {rts.Flying})", !rts.Flying); }   // inside the 3 s FlyTimeout: a fly-to left running would have moved the focus off the unit by now
            yield return Restored(tag, u, b, keys, tag != "pause menu", before);
        }

        IEnumerator All()
        {
            g = ZGame.I; W = g ? g.World : null; pi = FindFirstObjectByType<PlayerInput>(); rts = pi ? pi.cam : null; hud = OpenZHud.I; cam = rts ? rts.GetComponent<Camera>() : null;
            if (W == null || !rts || !pi || !cam) { Log("no level in Play"); fail++; Finish(); yield break; }
            Directory.CreateDirectory(Folder());
            me = g.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 40f);
            W.Bots.Clear();
            startIds = new HashSet<int>(W.UnitObjects.Where(o => o != null).Select(o => o.Id));   // E2a: see Clean
            Log($"level {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, game speed {g.gameSpeed:0.00}, RTS height {rts.height:0.0}, field of view {cam.fieldOfView:0}, keys {OzBindings.Cap(OzAction.ScrollUp)} {OzBindings.Cap(OzAction.ScrollLeft)} {OzBindings.Cap(OzAction.ScrollDown)} {OzBindings.Cap(OzAction.ScrollRight)}");
            yield return WaitFor(() => Application.isFocused, 10f);
            Check($"the Unity window has the focus (fix round 2: the view locks the cursor and takes the mouse only with the focus): {Application.isFocused}", Application.isFocused);
            Devices(true);
            OzFpsView.AtRender = OnRender;                                                                   // fps_view_fix (c): what the camera draws, read inside its pictures
            if (OzFpsView.On && OzFpsView.I) OzFpsView.I.Leave("the check starts");
            // ---- refusals
            int r0 = OzFpsView.Refusals;
            Select(); yield return Tap(Key.LeftAlt, Key.F); yield return Frames(2);
            bool none = !OzFpsView.On;
            if (FindSpot(out int rx, out int ry))
            {
                rts.focus = ZGame.PxToWorld(rx, ry); yield return Real(0.3f);
                var a = Make("robot", "grunt", rx, ry, me); var c = Make("robot", "grunt", rx + 40, ry, me); var e = Make("robot", "grunt", rx, ry + 40, foe, 1);
                if (e != null) { e.Damage = 0; e.MaxHealth = 10000000; e.Health = 10000000; }
                yield return Real(1f);
                string whyNone = OzFpsView.LastRefusal;
                Select(a, c); yield return Tap(Key.LeftAlt, Key.F); yield return Frames(2);
                bool two = !OzFpsView.On; string whyTwo = OzFpsView.LastRefusal;
                Select(e); yield return Tap(Key.LeftAlt, Key.F); yield return Frames(2);
                bool enemy = !OzFpsView.On; string whyEnemy = OzFpsView.LastRefusal;                     // PlayerInput takes an enemy out of the selection before the hook: "nothing selected" (the "not an own unit" line is the second guard)
                Select(a); yield return Tap(Key.F); yield return Frames(2);
                bool plainF = !OzFpsView.On;
                Check($"refused: nothing selected {none} (\"{whyNone}\"), two units {two} (\"{whyTwo}\"), an enemy unit {enemy} (\"{whyEnemy}\": PlayerInput prunes it from the selection first); {OzFpsView.Refusals - r0} refusals; a plain F does nothing {plainF}",
                      none && two && enemy && plainF && OzFpsView.Refusals - r0 == 3 && whyNone == "nothing selected" && whyTwo.Contains("units selected") && (whyEnemy == "nothing selected" || whyEnemy == "not an own unit"));
                // fix round 1: an open question, a gun being placed
                int r1 = OzFpsView.Refusals;
                Select(a); OzConfirm.Ask("FPS CHECK", "the FPS view check (answered at once)", () => { }, s => { }); yield return Frames(2);
                yield return Tap(Key.LeftAlt, Key.F); yield return Frames(2);
                bool asked = OzConfirm.IsOpen, qRef = !OzFpsView.On; string whyQ = OzFpsView.LastRefusal;
                OzConfirm.ProofAnswer(false); yield return Frames(3);
                Check($"refused: an open question {qRef} (\"{whyQ}\"; the question was open {asked})", asked && qRef && OzFpsView.Refusals == r1 + 1 && whyQ == "a question is open");
                ZObject home = null; foreach (var o in W.Objects) if (o != null && !o.Destroyed && o.Kind == ZKind.Building && o.Owner == me) { home = o; break; }
                if (home != null)
                {
                    int r2 = OzFpsView.Refusals; bool added = home.StoredCannons.Count == 0; if (added) home.StoredCannons.Add("gatling");   // test only, taken out again below
                    Select(a); pi.BeginPlaceCannon(home); yield return Frames(2);
                    bool placing = pi.Placing != null;
                    yield return Tap(Key.LeftAlt, Key.F); yield return Frames(2);
                    bool pRef = !OzFpsView.On; string whyP = OzFpsView.LastRefusal;
                    pi.CancelPlace(); if (added) home.StoredCannons.RemoveAt(home.StoredCannons.Count - 1); yield return Frames(2);
                    Check($"refused: a gun being placed {pRef} (\"{whyP}\"; placing {placing}, from {home.Type} #{home.Id})", placing && pRef && OzFpsView.Refusals == r2 + 1 && whyP == "a gun is being placed");
                }
                else Log("INFO no own building on the map: the \"gun being placed\" refusal not played (the line is in OzFpsView.Refuse)");
                Kill(W, e); Select();
            }
            else Check("refusals: a free place near the camera", false);
            // ---- every type
            foreach (var t in Robots) yield return Type("robot", t);
            foreach (var t in Vehicles) yield return Type("vehicle", t);
            foreach (var t in Guns) yield return Type("cannon", t);
            // fps_view_fix (b): lit like the grunt's and the sniper's copies (their own pixels' brightness on the screen; the light tank's was a dark shape)
            if (gunLuma.TryGetValue("grunt", out float lg) && gunLuma.TryGetValue("sniper", out float ls) && lg > 0f && ls > 0f)
            {
                float refL = 0.5f * (lg + ls); var dark = new List<string>(); var all = new StringBuilder();
                foreach (var t in Vehicles.Concat(Guns)) { if (!gunLuma.TryGetValue(t, out float l)) { dark.Add(t + " (no copy measured)"); continue; } all.Append($" {t} {l:0.00}"); if (l < 0.55f * refL) dark.Add(t); }
                Check($"the vehicle and gun copies lit like the robots': brightness of their own pixels{all} against the grunt's {lg:0.00} and the sniper's {ls:0.00} (at least 55 % of their mean {refL:0.00}); too dark: {(dark.Count > 0 ? string.Join(", ", dark) : "none")}", dark.Count == 0);
            }
            else Check("the vehicle and gun copies lit like the robots': the grunt's and the sniper's copies measured", false);
            // fps_view_fix fix round 1 (c): the hold proven by pixels on the robots and on every vehicle
            var hullMiss = HullMust.Where(t => !hullSeen.Contains(t)).ToList();
            Check($"its own model not drawn, by PIXELS, on every type that must show it at 60 deg down: seen and judged on {string.Join(", ", hullSeen)}; not seen: {(hullMiss.Count > 0 ? string.Join(", ", hullMiss) : "none")} (wanted none of {string.Join(", ", HullMust)})", hullMiss.Count == 0);
            // ---- fix round 3: fire on the move (a straight walk keeps the target: one move order per 2 tiles, each drops it, DV16)
            yield return FireWalk("robot", "grunt");
            foreach (var t in Vehicles) yield return FireWalk("vehicle", t);                                // fps_view_fix (d): every vehicle type drives while it fires
            // ---- the other leave paths
            yield return Other("Alt+F again", "robot", "grunt", u => Tap(Key.LeftAlt, Key.F), "Alt+F again", true);
            yield return Other("the unit dies", "robot", "sniper", u => { Kill(W, u); return Frames(1); }, "the unit died", true);
            yield return Other("pause menu", "robot", "psycho", PauseLeave, "the pause menu is open", true);
            yield return Other("gets in", "robot", "grunt", GetIn, "the unit got in", true);
            yield return Other("crew out", "vehicle", "light", u => { W.Eject(me, u); return Frames(1); }, "the crew got out", true);
            yield return Other("level end", "robot", "tough", u => { OzFpsView.CheckEndsMatch = true; return Frames(2); }, "the level ends", false);
            OzFpsView.CheckEndsMatch = false;
            yield return Other("Esc after a fly-to", "robot", "grunt", u => Tap(Key.Escape), "Esc", true, true);
            yield return HelpRow();                                                                          // fix round 2
            // ---- fix round 1: the MAYHEM fog from the ground
            yield return FogRow();
            // ---- fix round 3, last (it ends the match for real): the level's end while the view is on leaves the camera to the ending
            yield return EndingRow();
            Devices(false);
            // ---- the picture sheet, the errors
            string sheet = Path.Combine(Folder(), "fps_types.jpg");
            bool ok = shots.Count > 0 && OzProofPictures.Sheet(shots, 4, 480, sheet);
            Check($"picture sheet of {shots.Count} first-person frames -> {sheet}", ok && shots.Count == Robots.Length + Vehicles.Length + Guns.Length);
            Check($"no error or exception logged during the run ({errors}{(errorLines.Count > 0 ? ": " + string.Join(" | ", errorLines) : "")})", errors == 0);
            Finish();
        }

        // ------------------------------------------------------------------ fix round 1, fps_view_fix (a): the MAYHEM fog from the ground
        /// <summary>fps_view_fix (a): the view must hide exactly what the fog hides from the RTS camera. The fog forced on and the map covered again
        /// (test only), a new grunt (its sight opened). Ground points and enemy buildings are first read with the RTS camera itself (its fog's
        /// opacity picture, debug 1, post effects off, the camera over each one at its normal height and tilt): what the RTS camera covers (0.9 or
        /// more) must read 0.9 or more in the view too (E2a chose them by the reveal map alone: the fog's field treats a narrow gap not explored yet
        /// as explored, for the RTS camera as well). In each sampled view frame every pixel whose own surface stands 1.5 m or more inside the covered
        /// ground (the fog's own signed distance, debug 2's green) is closed (0.85 or more) on 98 % of them, the deck closing it 1 m past the line
        /// as the RTS camera's deck does; explored surfaces 6 m or more from the line read 0.1 or less on 95 %. Pictures fps_fog.png,
        /// fps_fog_op.png (the view) and fps_fog_rts_op.png (the RTS camera).</summary>
        IEnumerator FogRow()
        {
            var f = OzFogOfWar.I;
            if (!f) { Check("MAYHEM fog: the fog of war's component in this level", false); yield break; }
            bool wasWanted = OzFogOfWar.Wanted;
            if (!wasWanted && !f.SetForced(true, out string refused)) { Check($"MAYHEM fog: the fog forced on for the check (refused: {refused})", false); yield break; }
            f.CoverAgain();                                                                                   // test only: the map covered again, opened round the player's side: ground not explored yet near the camera
            yield return Real(OzFogOfWar.FadeInSeconds + 1f);
            var mask = OzFogOfWar.Mask;
            var ucd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mask == null || !OzFogOfWar.Mat || !ucd) { Check($"MAYHEM fog: on with its reveal map {mask != null}, material {(bool)OzFogOfWar.Mat}, camera data {(bool)ucd}", false); f.SetForced(wasWanted, out _); yield break; }
            if (!FindSpot(out int x, out int y)) { Check("MAYHEM fog: a free place near the camera", false); f.SetForced(wasWanted, out _); yield break; }
            var home = ZGame.PxToWorld(x, y);
            rts.focus = home; yield return Real(0.4f);
            var u = Make("robot", "grunt", x, y, me); noGrab = u; grabsStopped = 0; yield return Real(2.5f);   // its own sight opens round it (the reveal eases in); E3: its close grab stopped (see noGrab)
            var v = ViewOf(u); if (u == null || !v) { noGrab = null; Check("MAYHEM fog: a grunt made with its view", false); f.SetForced(wasWanted, out _); yield break; }
            // the eye the view will have (a grunt's head, about 1.8 m over its feet) for the line-of-sight tests
            var eye = v.transform.position + Vector3.up * 1.8f; int mw = (int)(rts.mapSizeMeters.x * 8f), mh = (int)(rts.mapSizeMeters.y * 8f);
            var cands = new List<(float yaw, Vector3 p, float r0)>();
            for (int a = 0; a < 36 && cands.Count < 8; a++)
            {
                float ang = a * 10f * Mathf.Deg2Rad; var d = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));     // world: yaw a * 10 deg
                float r0 = -1f;
                for (float r = 4f; r < 220f; r += 2f)
                {
                    float wx = eye.x + d.x * r, wz = eye.z + d.z * r; int px = (int)(wx * 8f), py = (int)(-wz * 8f);
                    if (px < 24 || py < 24 || px > mw - 24 || py > mh - 24) break;
                    if (mask.ShownAtPx(px, py) < OzFowMask.Revealed) { r0 = r; break; }
                }
                if (r0 < 0f) continue;
                var gp = eye + d * (r0 + 30f); gp.y = g.GroundY(gp.x, gp.z);
                if (!Clear(eye, gp + Vector3.up * 1f) || !Clear(eye, gp + Vector3.up * 3f)) continue;
                cands.Add((a * 10f, gp, r0)); a += 2;                                                            // directions 30 deg apart at least
            }
            var blds = new List<(ZObject o, Vector3 p)>();
            foreach (var o in W.Objects)
            {
                if (o == null || o.Destroyed || o.Kind != ZKind.Building || o.Owner == me || mask.ShownAtPx(o.CenterX, o.CenterY) >= OzFowMask.Revealed) continue;
                var c = ZGame.PxToWorld(o.CenterX, o.CenterY); c.y = g.GroundY(c.x, c.z) + 2f;
                if ((c - eye).magnitude > 200f || !Clear(eye, c)) continue;
                blds.Add((o, c)); if (blds.Count >= 6) break;
            }
            // the RTS camera's own view of each (its fog's opacity, debug 1): what it covers
            var targets = new List<(float yaw, Vector3 p, float r0, float rts)>(); var bTargets = new List<(ZObject o, Vector3 p, float rts)>();
            bool post0 = ucd.renderPostProcessing; bool rtsShot = false;
            foreach (var c in cands)
            {
                rts.focus = new Vector3(c.p.x, 0f, c.p.z); yield return Real(0.3f);
                OzFogOfWar.DebugMode = 1; ucd.renderPostProcessing = false; yield return Frames(4);
                if (!rtsShot) { string op = Path.Combine(Folder(), "fps_fog_rts_op.png"); ScreenCapture.CaptureScreenshot(op); yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(op, 3f); rtsShot = true; }
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture(); float o1 = Opacity(tex, cam.WorldToScreenPoint(c.p + Vector3.up * 1f)); Destroy(tex);
                OzFogOfWar.DebugMode = 0; ucd.renderPostProcessing = post0;
                if (o1 >= 0.9f && targets.Count < 3) targets.Add((c.yaw, c.p, c.r0, o1));
                else Log($"INFO MAYHEM fog: the RTS camera at {c.yaw:0} deg, {c.r0 + 30f:0} m from the grunt (the reveal map: not explored from {c.r0:0} m): opacity {o1:0.00}{(o1 < 0.9f ? " (the RTS camera shows it: not a target)" : "")}");
            }
            foreach (var bb in blds)
            {
                rts.focus = new Vector3(bb.p.x, 0f, bb.p.z); yield return Real(0.3f);
                OzFogOfWar.DebugMode = 1; ucd.renderPostProcessing = false; yield return Frames(4);
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture(); float o1 = Opacity(tex, cam.WorldToScreenPoint(bb.p)); Destroy(tex);
                OzFogOfWar.DebugMode = 0; ucd.renderPostProcessing = post0;
                bTargets.Add((bb.o, bb.p, o1));
            }
            rts.focus = home; yield return Real(0.4f);
            var b = Snap(v);
            yield return Enter("MAYHEM fog", u, b);
            var fv = OzFpsView.I; if (!OzFpsView.On || !fv) { noGrab = null; f.SetForced(wasWanted, out _); yield break; }
            yield return Frames(3);
            var fu = OzFpsView.FogUnderNow;
            Check($"MAYHEM fog: the fog's under-the-sheets wall on ({fu.x:0} {fu.y:0} m {fu.z:0} m)", fu.x > 0.5f && fu.y > 10f && fu.z > 50f);
            int samples = 0; bool allCovered = true; var lines = new StringBuilder(); bool shotDone = false;
            int deepN = 0, deepOpen = 0, clearN = 0, clearShut = 0;
            foreach (var tg in targets)
            {
                yield return Aim(fv, tg.p + Vector3.up * 1f, -8f);                                         // the target 8 deg left of the crosshair (clear of it and of the gun copy)
                if (!shotDone)
                {
                    string col = Path.Combine(Folder(), "fps_fog.png"); ScreenCapture.CaptureScreenshot(col);
                    yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(col, 3f);
                }
                OzFogOfWar.DebugMode = 1; ucd.renderPostProcessing = false; yield return Frames(4);
                if (!shotDone)
                {
                    string op = Path.Combine(Folder(), "fps_fog_op.png"); ScreenCapture.CaptureScreenshot(op);
                    yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(op, 3f); shotDone = true;
                }
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                float g1 = Opacity(tex, cam.WorldToScreenPoint(tg.p + Vector3.up * 1f)), g3 = Opacity(tex, cam.WorldToScreenPoint(tg.p + Vector3.up * 3f));
                var near = eye + (tg.p - eye).normalized * 6f; near.y = g.GroundY(near.x, near.z);
                float n0 = Opacity(tex, cam.WorldToScreenPoint(near));
                Destroy(tex);
                OzFogOfWar.DebugMode = 2; yield return Frames(4);                                              // debug 2: red the opacity, green the surface's own signed distance (0.5 + d / 64)
                yield return new WaitForEndOfFrame();
                var t2 = ScreenCapture.CaptureScreenshotAsTexture(); WholeFrame(t2, ref deepN, ref deepOpen, ref clearN, ref clearShut); Destroy(t2);
                OzFogOfWar.DebugMode = 0; ucd.renderPostProcessing = post0; yield return Frames(2);
                bool ok = g1 >= 0.9f && g3 >= 0.9f && n0 >= 0f && n0 <= 0.1f;
                samples++; allCovered &= ok;
                lines.Append($" [{tg.yaw:0} deg: covered for the RTS camera ({tg.rts:0.00}) at {tg.r0 + 30f:0} m: the view's ground +1 m {g1:0.00}, +3 m {g3:0.00}; explored 6 m ahead {n0:0.00}]");
            }
            Check($"MAYHEM fog: ground the RTS camera covers reads full cover in the view too (opacity 0.9 or more), explored ground in front clear (0.1 or less), {samples} direction(s) (2 or more wanted):{lines}",
                  samples >= 2 && allCovered);
            Check($"MAYHEM fog: in the {samples} view frame(s) every pixel 1.5 m or more inside the covered ground (the fog's own distance) is closed: {deepN - deepOpen} of {deepN} at 0.85 or more (98 % wanted); explored surfaces 6 m or more from the line clear: {clearN - clearShut} of {clearN} at 0.1 or less (95 % wanted)",
                  deepN >= 200 && deepOpen <= 0.02f * deepN && clearN >= 200 && clearShut <= 0.05f * clearN);
            // enemy buildings in ground not explored yet: what the RTS camera covers, the view covers
            int bCovered = 0; bool bOk = true; var bl = new StringBuilder();
            foreach (var bt in bTargets)
            {
                yield return Aim(fv, bt.p, -8f);
                OzFogOfWar.DebugMode = 2; ucd.renderPostProcessing = false; yield return Frames(4);
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture(); var sp = cam.WorldToScreenPoint(bt.p);
                float ob = Opacity(tex, sp), dField = Channel(tex, sp, 1); Destroy(tex);
                OzFogOfWar.DebugMode = 0; ucd.renderPostProcessing = post0; yield return Frames(2);
                float dm = dField >= 0f ? (dField - 0.5f) * 64f : -99f;
                bl.Append($" [{bt.o.Type} #{bt.o.Id} {(bt.p - eye).magnitude:0} m: the RTS camera {bt.rts:0.00}, the view {ob:0.00}, its surface {dm:0.0} m inside the covered ground]");
                if (bt.rts >= 0.9f) { bCovered++; bOk &= ob >= 0.9f; }
            }
            if (bCovered > 0) Check($"MAYHEM fog: every enemy building the RTS camera covers is covered in the view too (0.9 or more at its middle): {bCovered} of {bTargets.Count} covered for the RTS camera:{bl}", bOk);
            else Log($"INFO MAYHEM fog: no enemy building in ground not explored yet within 200 m in clear sight that the RTS camera covers ({bTargets.Count} looked at:{bl}); the ground and whole-frame rows above cover what stands on it");
            bool onAtEnd = OzFpsView.On;                                                                     // E3: Esc only while the view is on (with it off, Esc opens the pause menu)
            if (onAtEnd) { yield return Tap(Key.Escape); yield return Frames(3); }
            noGrab = null;
            if (grabsStopped > 0) Log($"INFO MAYHEM fog: the game's close grab of a neutral vehicle by the fog grunt stopped {grabsStopped} time(s) (test only)");
            var fu2 = OzFpsView.FogUnderNow;
            Check($"MAYHEM fog: left: the wall off again ({fu2.x:0}), the view off {!OzFpsView.On}; the view was on to the end of the row {onAtEnd} (its leave: {OzFpsView.LastLeave})", fu2 == Vector4.zero && !OzFpsView.On && onAtEnd && OzFpsView.LastLeave == "Esc");
            OzFogOfWar.DebugMode = 0; f.SetForced(wasWanted, out _); yield return Real(0.6f);
        }

        /// <summary>fps_view_fix (a): one debug 2 frame: per pixel (every 4th) the opacity (red) and the surface's own signed distance (green, 0.5 + d / 64;
        /// the sky and d of 30 m or more left out, they read 1).</summary>
        static void WholeFrame(Texture2D t, ref int deepN, ref int deepOpen, ref int clearN, ref int clearShut)
        {
            bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int y = 8; y < t.height - 8; y += 4)
                for (int x = 8; x < t.width - 8; x += 4)
                {
                    var c = t.GetPixel(x, y); float r = lin ? Mathf.GammaToLinearSpace(c.r) : c.r, gg = lin ? Mathf.GammaToLinearSpace(c.g) : c.g;
                    if (gg >= 0.97f || gg <= 0.03f) continue;
                    float d = (gg - 0.5f) * 64f;
                    if (d >= 1.5f) { deepN++; if (r < 0.85f) deepOpen++; }
                    else if (d <= -6f) { clearN++; if (r > 0.1f) clearShut++; }
                }
        }

        /// <summary>One channel (linear) as the median of a 7 x 7 block at a screen point; -1 off the screen.</summary>
        static float Channel(Texture2D tex, Vector3 sp, int ch)
        {
            if (!tex || sp.z <= 0f) return -1f;
            int cx = Mathf.RoundToInt(sp.x * tex.width / Mathf.Max(1, Screen.width)), cy = Mathf.RoundToInt(sp.y * tex.height / Mathf.Max(1, Screen.height));
            if (cx < 4 || cy < 4 || cx >= tex.width - 4 || cy >= tex.height - 4) return -1f;
            var vals = new List<float>(49); bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int j = -3; j <= 3; j++) for (int i = -3; i <= 3; i++) { var c = tex.GetPixel(cx + i, cy + j); float r = ch == 0 ? c.r : ch == 1 ? c.g : c.b; vals.Add(lin ? Mathf.GammaToLinearSpace(r) : r); }
            vals.Sort(); return vals[24];
        }

        /// <summary>The line from a to b clear over the ground (the walk ground, 0.25 m).</summary>
        bool Clear(Vector3 a, Vector3 b)
        {
            for (int i = 1; i < 48; i++) { var p = Vector3.Lerp(a, b, i / 48f); if (p.y < g.GroundY(p.x, p.z) + 0.25f) return false; }
            return true;
        }

        /// <summary>Turns the view so the world point p sits `side` deg to the right of the crosshair (negative: left), on its height.</summary>
        IEnumerator Aim(OzFpsView fv, Vector3 p, float side)
        {
            var d = p - cam.transform.position; float yawT = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - side, pitchT = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            float k = RtsCamera.FreeLookSpeed;
            fv.LookBy(new Vector2(Mathf.DeltaAngle(fv.Yaw, yawT) / k, (pitchT - fv.Pitch) / k));
            yield return Frames(4);
        }

        /// <summary>The fog's opacity (debug 1: grey = 1 - transmittance, linear) as the median of a 7 x 7 block at a screen point; -1 off the screen.</summary>
        static float Opacity(Texture2D tex, Vector3 sp)
        {
            if (!tex || sp.z <= 0f) return -1f;
            int cx = Mathf.RoundToInt(sp.x * tex.width / Mathf.Max(1, Screen.width)), cy = Mathf.RoundToInt(sp.y * tex.height / Mathf.Max(1, Screen.height));
            if (cx < 4 || cy < 4 || cx >= tex.width - 4 || cy >= tex.height - 4) return -1f;
            var vals = new List<float>(49);
            bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;                                // the screen is sRGB: back to the shader's linear opacity
            for (int j = -3; j <= 3; j++) for (int i = -3; i <= 3; i++) { float r = tex.GetPixel(cx + i, cy + j).r; vals.Add(lin ? Mathf.GammaToLinearSpace(r) : r); }
            vals.Sort(); return vals[24];
        }

        IEnumerator PauseLeave(ZObject u)
        {
            hud.Pause.Open(); yield return Frames(3);
            bool hidden = !hud.hudVisible;
            Check($"pause menu: open, the HUD stays hidden under it {hidden}", hud.Pause.IsOpen && hidden);
            yield return Real(0.5f);
            hud.Pause.Close(); yield return Real(0.8f);
            Check($"pause menu: closed, the HUD back {hud.hudVisible}", !hud.Pause.IsOpen && hud.hudVisible);
        }

        IEnumerator GetIn(ZObject u)
        {
            var apc = W.CreateObject("vehicle", "apc", u.CenterX + 40 - 16, u.CenterY - 16, me); g.SpawnUnitView(apc);
            yield return Real(0.8f);
            W.SendWaypoints(me, u, new[] { new ZWaypoint { mode = WpMode.Enter, refId = apc.Id, x = apc.CenterX, y = apc.CenterY } });
        }

        void Finish()
        {
            Devices(false);
            Report = $"{(fail == 0 && pass > 0 ? "PASS" : "FAIL")} {pass} passed, {fail} failed";
            Log(Report);
            try { File.WriteAllText(glowRun ? Path.Combine(GlowFolder(), "report_glow.txt") : Path.Combine(Folder(), skyRun ? "report_sky.txt" : "report.txt"), log.ToString()); } catch (IOException) { }   // fps_view_sky: the short check's own report; fps_glow: kits/fps_glow/proof/play
            Destroy(gameObject);
        }
    }
}
#endif
