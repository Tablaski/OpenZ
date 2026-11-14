#if UNITY_EDITOR && !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 vehicle_exit_gun: the Play check of the kit's three items, on a level playing in the normal mode (EXIT DRIVER on), every row from
    /// real frames (read at the END of each frame: what is drawn) and the rules' own events; no row passes on 0 samples. Menu: OpenZ/V1.4/Vehicle
    /// Exit Gun: Play Check (a level playing) and ...: last report (Editor/OzV14VehicleExitGunMenu.cs); report and notes in Folder.
    /// The EXIT is the player's own: PlayerInput.EjectUnit (what the EXIT DRIVER tab calls). The level's own enemies are quieted while it runs
    /// (their Damage 0, put back at the end), the check's own enemies keep theirs.
    ///  G   each driver type (grunt, psycho, tough, sniper, pyro, laser: the robot types that drive; the laser's rig is noted: Humanoid or the
    ///      Generic laser rig) x each hatch vehicle (light, medium, heavy tank, mech): EXIT DRIVER, then every frame: his gun shown in his hand
    ///      (its object active, a renderer of it drawn, the gun under his right hand's bone and its grip within GripM of that bone) within
    ///      GunWithinSec of the touch (the first frame drawn landing) and in EVERY frame after it on the figure (no empty-hand frame, at least
    ///      MinFigureFrames frames), the gun-ready sound, and after the hand-over his own model's gun drawn and in his hand in every frame for
    ///      LiveSec (at least MinLiveFrames frames); no T-pose (his hips over his lower foot never more than 1.15 x his own model's standing
    ///      height of them, size for size); his gun in his hand in the first frame of his walk on.
    ///  G4  review 2 (one frame's step past the whole 0.47 s stand-up): FastRows drivers x hulls at FastSpeed (4x) game speed with every frame
    ///      from the EXIT to his walk on stalled to StallMs (Time.maximumDeltaTime 0.1 s: a 0.4 s step per frame), up to FastTries EXITs per
    ///      row until one landing's stand-up was stepped past in one frame (its record's gunLate; no pass without it); every try: the G row's
    ///      gun rows (at least MinFastFrames figure frames after the gun), his gun in his hand in the first frame of his walk on.
    ///  E   an enemy jeep and an enemy grunt squad on the computer's attack order on our light tank: once both fired at it, EXIT DRIVER: shots
    ///      STARTED at the emptied tank by them after the EXIT's tick (their Hitscan / MissileFired events on it) = 0 for EmptySec; what they do
    ///      instead is noted (another target or standing).
    ///  Es  the same with its crew sniped out by enemy sniper squads (the driver at 1 health): 0 shots at it after the snipe's tick.
    ///  W   wobble against death: our heavy and light tank shell two enemy grunt squads (one squad tough enough to live: the survivors); every
    ///      frame of every robot: a reaction and a death at once on one body = 0 frames, a frozen reaction's body moving (UnitView.ReactFrozenNow
    ///      in this and the last frame, its root moved more than StillM or turned more than StillDeg) = 0 frames, at least one death and at
    ///      least one survivor reacting (a hit that does not kill still wobbles); frozen / dropped / refused reactions (INFO).
    ///  Wm  review 2, the conflict itself: our MLRS fires on an enemy light tank; once its shot is in the air, enemy grunts (30 HP) are put
    ///      inside its splash but outside rocket 1's core (thrown by a ripple rocket's landing: BlastWave's hold, up to 2.5 s) and kept
    ///      wobbling (staggers, long knockdowns) until the shot kills them: reactions frozen at the death event and thrown >= 1, of them with a
    ///      hold for their ripple rocket >= 1, thrown in the death event's own frame while held = 0, thrown before the hold's moment (eventAt +
    ///      wave, 0.02 s grace) = 0, a frozen body moving = 0 frames, a reaction and a death on one body = 0 frames.
    /// </summary>
    public class OzVehicleExitGunCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.4 vehicle exit gun]";
        public static bool Running; public static string Report = "not run";
        public static string Folder;
        public static float GunWithinSec = 0.5f, GripM = 0.30f, LiveSec = 1.0f, EmptySec = 6f;
        public static int MinFigureFrames = 5, MinLiveFrames = 10;
        public static float FastSpeed = 4f, StillM = 0.005f, StillDeg = 0.5f; public static int StallMs = 110, FastTries = 4, MinFastFrames = 2;
        public static readonly string[,] FastRows = { { "grunt", "light" }, { "tough", "heavy" }, { "laser", ZMTank.Mech } };
        public static readonly string[] Drivers = { "grunt", "psycho", "tough", "sniper", "pyro", "laser" };
        public static readonly string[] Hulls = { "light", ZMTank.Type, "heavy", ZMTank.Mech };

        ZGame g; ZWorld W; PlayerInput input; Team me, foe; float speed0 = -1f;
        readonly StringBuilder sb = new StringBuilder(); int pass, total;
        readonly List<ZObject> made = new List<ZObject>();
        readonly Dictionary<ZObject, int> quieted = new Dictionary<ZObject, int>();
        readonly HashSet<long> used = new HashSet<long>();

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OZ_VehicleExitGunCheck").AddComponent<OzVehicleExitGunCheck>();
        }

        IEnumerator Start()
        {
            Running = true; Report = "running";
            try
            {
                g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>();
                var hud = OpenZHud.I;
                if (!g || W == null || !input || !hud) { Report = Tag + " FAIL: no level playing"; Debug.LogWarning(Report); yield break; }
                if (!ZWorld.VehicleExitDeviation) { Report = Tag + " FAIL: EXIT DRIVER is off in this session ('Zod exact'): start a level in the normal mode"; Debug.LogWarning(Report); yield break; }
                me = hud.Player; foe = me == Team.Red ? Team.Blue : Team.Red;
                if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
                StartCoroutine(Peace());
                yield return GunRows();
                yield return GunRowsFast();
                yield return EmptyRows(false);
                yield return EmptyRows(true);
                yield return WobbleRows();
                yield return WobbleRippleRows();
            }
            finally
            {
                foreach (var kv in quieted) if (kv.Key != null) kv.Key.Damage = kv.Value;
                if (g && speed0 >= 0f) g.gameSpeed = speed0;                                                   // G4 put the game speed back
                Report = $"{Tag} {(pass == total && total > 0 ? "PASS" : "FAIL")} {pass}/{total}\n" + sb;
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(Path.Combine(Folder, "gun_check_report.txt"), Report); } catch { } }
                Debug.Log(Report);
                Running = false; Destroy(gameObject);
            }
        }

        void Log(string what, bool ok, string detail) { total++; if (ok) pass++; sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {what}: {detail}"); }
        void Note(string s) => sb.AppendLine("      " + s);
        static IEnumerator Real(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator Peace()
        {
            while (Running && W != null)
            {
                foreach (var o in W.Objects) if (o.Owner != me && o.Owner != Team.Neutral && !o.Destroyed && o.Damage > 0 && !made.Contains(o) && !quieted.ContainsKey(o)) { quieted[o] = o.Damage; o.Damage = 0; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        // ------------------------------------------------------------------ units and places
        ZObject Make(string cat, string type, Team t, int x, int y, int amount = -1)
        {
            var o = cat == "robot" ? W.CreateRobotGroup(type, x, y, t, amount) : W.CreateObject(cat, type, x, y, t);
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            made.Add(o); made.AddRange(o.Minions);
            return o;
        }
        static void Tough(ZObject o) { o.MaxHealth = o.Health = 1000000000; foreach (var m in o.Minions) m.MaxHealth = m.Health = 1000000000; }

        /// <summary>A free w x h tile block (robot-passable, no object within 3 tiles) near the camera's focus, not used before; the camera flown there.</summary>
        IEnumerator Spot(int w, int h, int[] at)
        {
            at[0] = at[1] = -1;
            ClearMade();                                                                                      // U2 check fix 2: the earlier tries' units out first (run 1 on L17: 24 tanks + drivers left standing, every row from the medium tank's laser on 'no free ground near the camera')
            var rts = FindFirstObjectByType<RtsCamera>();
            var c = ZGame.WorldToPx(rts ? rts.focus : Vector3.zero); int fx = c.x >> 4, fy = c.y >> 4;
            for (int r = 0; r < 60 && at[0] < 0; r++)
                for (int dx = -r; dx <= r && at[0] < 0; dx++)
                    for (int dy = -r; dy <= r && at[0] < 0; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int bx = fx + dx * 2, by = fy + dy * 2;
                        if (used.Contains(((long)bx << 32) ^ (uint)by) || !Free(bx, by, w, h)) continue;
                        at[0] = bx; at[1] = by;
                    }
            if (at[0] < 0) yield break;
            for (int x = -2; x < w + 2; x++) for (int y = -2; y < h + 2; y++) used.Add(((long)(at[0] + x) << 32) ^ (uint)(at[1] + y));
            if (rts) { rts.FlyTo(ZGame.PxToWorld((at[0] + w / 2) * 16, (at[1] + h / 2) * 16)); yield return Real(1.2f); }
        }
        /// <summary>U2 check fix 2 (check only): every unit this check made so far taken out of the world (the game's RemoveNow, no death, as the
        /// Unit editor's CLEAR FIELD; OzFpsViewPlayCheck.Clean does the same) and its view dropped; the used blocks free again.</summary>
        void ClearMade()
        {
            if (W == null || made.Count == 0) return;
            var sel = input ? input.Selection : null;
            foreach (var o in made.ToList())
            {
                if (o == null) continue;
                var ids = new List<int> { o.Id }; foreach (var m in o.Minions) if (m != null) ids.Add(m.Id);
                if (sel != null) sel.RemoveAll(s => s != null && ids.Contains(s.Id));
                if (!o.Destroyed && W.Get(o.Id) != null) W.RemoveNow(o);
                foreach (var id in ids) if (g.Views.TryGetValue(id, out var v)) { g.Views.Remove(id); if (v) { v.gameObject.SetActive(false); Destroy(v.gameObject); } }
            }
            made.Clear(); used.Clear();
        }
        bool Free(int bx, int by, int w, int h)
        {
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) if (!W.Path.TilePassable(bx + x, by + y, true)) return false;
            int x0 = (bx - 3) * 16, y0 = (by - 3) * 16, x1 = (bx + w + 3) * 16, y1 = (by + h + 3) * 16;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX >= x0 && o.CenterX <= x1 && o.CenterY >= y0 && o.CenterY <= y1) return false;
            return true;
        }
        ZObject DriverOf(ZObject v) { foreach (var o in W.Objects) if (o.Kind == ZKind.Robot && o.DriverOutFromId == v.Id && !o.Destroyed) return o; return null; }

        static Transform RightHand(GameObject go)
        {
            var a = go ? go.GetComponentInChildren<Animator>() : null;
            if (a && a.isHuman) { var h = a.GetBoneTransform(HumanBodyBones.RightHand); if (h) return h; }
            if (!go) return null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "mixamorigRightHand" || t.name == "RightHand" || t.name.EndsWith("_RightHand")) return t;
            return null;
        }
        /// <summary>The gun drawn in his hand on `go`: its object active, a renderer of it on, under his right hand's bone (or within GripM of it).</summary>
        static bool GunInHand(GameObject go, out string why, out float grip)
        {
            why = ""; grip = -1f;
            var w = BoardingView.GunOf(go); if (!w) { why = "no gun object"; return false; }
            if (!w.gameObject.activeInHierarchy) { why = "gun object off"; return false; }
            bool drawn = false; foreach (var r in w.GetComponentsInChildren<Renderer>()) if (r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) { drawn = true; break; }
            if (!drawn) { why = "no gun renderer drawn"; return false; }
            var h = RightHand(go); if (!h) { why = "no right hand bone"; return false; }
            grip = Vector3.Distance(w.position, h.position) / Mathf.Max(0.2f, go.transform.lossyScale.y);
            if (!w.IsChildOf(h) && grip > GripM) { why = $"gun {grip:0.00} m from his hand"; return false; }
            return true;
        }

        // ------------------------------------------------------------------ G: the gun back in his hands
        IEnumerator GunRows()
        {
            foreach (var hull in Hulls)
                foreach (var driver in Drivers)
                {
                    string row = $"G {driver} out of the {Kind(hull)}";
                    var res = new GunWatch();
                    yield return GunTry(driver, hull, false, res);
                    if (res.setupFail != null) { Log(row, false, res.setupFail); continue; }
                    Log(row, res.ok, res.detail);
                }
        }

        /// <summary>G4 (review 2): one frame's step past his whole stand-up (4x game speed, long frames): the walk on never starts without his gun.</summary>
        IEnumerator GunRowsFast()
        {
            if (speed0 < 0f) speed0 = g.gameSpeed;
            try
            {
                for (int i = 0; i < FastRows.GetLength(0); i++)
                {
                    string driver = FastRows[i, 0], hull = FastRows[i, 1];
                    string row = $"G4 {driver} out of the {Kind(hull)} at {FastSpeed:0}x game speed, the jump's frames {StallMs} ms long";
                    int tries = 0, late = 0, bad = 0; var notes = new List<string>(); string fail = null;
                    while (tries < FastTries && late == 0)
                    {
                        tries++;
                        var res = new GunWatch();
                        yield return GunTry(driver, hull, true, res);
                        if (res.setupFail != null) { fail = res.setupFail; break; }
                        if (res.late) late++;
                        if (!res.ok) bad++;
                        notes.Add($"try {tries}: {(res.ok ? "ok" : "FAIL")}, the stand-up stepped past in one frame {res.late}; {res.detail}");
                    }
                    if (fail != null) { Log(row, false, fail); continue; }
                    Log(row, bad == 0 && late > 0, $"{tries} EXIT(s), of them with the whole stand-up stepped past in one frame {late} (want >= 1), failing {bad} (want 0)");
                    foreach (var n in notes) Note(n);
                }
            }
            finally { g.gameSpeed = speed0; }
        }

        class GunWatch { public bool ok, late; public string detail = "", setupFail; }

        IEnumerator GunTry(string driver, string hull, bool fast, GunWatch res)
        {
            if (fast) g.gameSpeed = speed0;
            var at = new int[2]; yield return Spot(8, 8, at);
            if (at[0] < 0) { res.setupFail = "no free ground near the camera"; yield break; }
            var v = Make("vehicle", hull, me, (at[0] + 3) * 16, (at[1] + 3) * 16);
            v.DriverType = driver; Tough(v);
            yield return Real(1.0f);
            if (!v.CanExitDriver(out string why)) { res.setupFail = "EXIT DRIVER cannot let him out: " + why; yield break; }
            if (fast) g.gameSpeed = FastSpeed;
            yield return new WaitForFixedUpdate();                                                      // U2 check fix: the EXIT before this frame's Update (as a real click: PlayerInput.Update), so BoardingView.Update (order 70) reads its Created events before CombatView.LateUpdate clears them; a coroutine's EjectUnit after Update left every jump unplanned ('no figure seen')
            bool ok = input.EjectUnit(v);
            if (!ok) { if (fast) g.gameSpeed = speed0; res.setupFail = "the EXIT was refused"; yield break; }
            yield return null;
            var r = DriverOf(v);
            if (r == null) { if (fast) g.gameSpeed = speed0; res.setupFail = "no driver out (DriverOutFromId)"; yield break; }
            made.Add(r);
            yield return WatchGun(r, fast, res);
            if (fast) g.gameSpeed = speed0;
        }

        IEnumerator WatchGun(ZObject r, bool fast, GunWatch res)
        {
            var eof = new WaitForEndOfFrame();
            float t0 = Time.time, touchT = -1f, gunT = -1f, endT = -1f; int figFrames = 0, figAfter = 0, emptyAfter = 0, liveFrames = 0, liveEmpty = 0, tpose = 0; string firstWhy = "", liveWhy = "", rig = "";
            float worstGrip = 0f; int walkFirst = -1; string walkWhy = "";                                      // review 2: his gun in his hand in the first frame of the walk on (1 yes, 0 no, -1 not seen)
            bool stall = fast;
            while (Time.time - t0 < 9f)
            {
                yield return eof;
                var fig = BoardingView.HatchFigureGo(r.Id, out string phase);
                if (stall && (phase == "walk" || Time.time - t0 > 4f || (touchT >= 0f && (fig == null || !fig.activeInHierarchy)))) stall = false;
                if (stall) System.Threading.Thread.Sleep(StallMs);                                              // G4: a long frame from the EXIT to his walk on (the next frame's step: Time.maximumDeltaTime x FastSpeed)
                if (fig && fig.activeInHierarchy)
                {
                    figFrames++;
                    if (rig == "") { var an = fig.GetComponentInChildren<Animator>(); rig = an ? (an.isHuman ? "Humanoid" : "Generic") : "no Animator"; }
                    if (touchT < 0f && (phase == "land" || phase == "walk")) touchT = Time.time;
                    if (touchT >= 0f)
                    {
                        bool inHand = GunInHand(fig, out string why, out float grip);
                        if (inHand && gunT < 0f) gunT = Time.time;
                        if (gunT >= 0f) { figAfter++; if (!inHand) { emptyAfter++; if (firstWhy == "") firstWhy = $"{phase}: {why}"; } else worstGrip = Mathf.Max(worstGrip, grip); }
                        if (phase == "walk" && walkFirst < 0) { walkFirst = inHand ? 1 : 0; if (!inHand) walkWhy = why; }
                        if (TPose(fig, r)) tpose++;
                    }
                    continue;
                }
                if (touchT >= 0f && (fig == null || !fig.activeInHierarchy))
                {   // handed over: his own model
                    if (fast && endT < 0f) g.gameSpeed = speed0;
                    if (endT < 0f) endT = Time.time;
                    g.Views.TryGetValue(r.Id, out var lv);
                    if (!lv || lv.Dying) { liveWhy = "his model gone / dying"; break; }
                    liveFrames++;
                    if (!GunInHand(lv.gameObject, out string why, out float grip)) { liveEmpty++; if (liveWhy == "") liveWhy = why; }
                    if (Time.time - endT >= LiveSec) break;
                }
            }
            BoardingView.HatchJumps.TryGetValue(r.Id, out var rec);
            float within = touchT >= 0f && gunT >= 0f ? gunT - touchT : -1f;
            res.late = rec != null && rec.gunLate;
            // U2 check fix 4 (check only): at 4x one stepped frame can take the figure past its stand-up AND its short walk on, so his own model takes
            // over in the next frame and no figure frame is left to show the gun (run 3: the Tough out of the heavy tank, the gun up 0.63 s after the
            // touch in the stepped frame, his model armed in all its 31 frames). That case passes when the record says the gun came up in that frame
            // (gunLate) and his model holds it from its first frame; any figure frame after the gun is still held to every rule below.
            bool fastHandOver = fast && figAfter == 0 && walkFirst < 0 && endT >= 0f && rec != null && rec.gunLate && rec.gunUpAfterTouch >= 0f;
            res.ok = touchT >= 0f && (fastHandOver || (gunT >= 0f && within <= GunWithinSec && figAfter >= (fast ? MinFastFrames : MinFigureFrames) && emptyAfter == 0 && walkFirst == 1))
                     && liveFrames >= MinLiveFrames && liveEmpty == 0 && tpose == 0 && rec != null && rec.gunSound && rec.handGunFound;
            res.detail = (fastHandOver ? "the stepped frame went past the walk on too: no figure frame after the gun, his model armed from its first frame; " : "") + $"{(rig == "" ? "no figure seen" : rig + " rig")}; touch seen {touchT >= 0f}, gun in his hand {within:0.00} s after the touch (want <= {GunWithinSec:0.0}); figure frames after it {figAfter} "
                         + $"(empty-hand {emptyAfter}{(firstWhy != "" ? ", first: " + firstWhy : "")}), the first walk frame: {(walkFirst == 1 ? "gun in hand" : walkFirst == 0 ? "NO GUN (" + walkWhy + ")" : "not seen")}, the gun's grip at most {worstGrip:0.00} m from his hand bone; T-pose frames {tpose}; "
                         + $"his model after the hand-over {liveFrames} frames (gun not in hand {liveEmpty}{(liveWhy != "" ? ": " + liveWhy : "")}); "
                         + (rec == null ? "no jump record" : $"record: the gun up {rec.gunUpAfterTouch:0.00} s after the touch ({(rec.gunName == "" ? "NO GUN on the figure" : rec.gunName)}){(rec.gunLate ? " in the frame that stepped past the stand-up" : "")}, the gun-ready sound {rec.gunSound}, "
                         + $"hand-over: his model's gun found {rec.handGunFound}, its object switched on there {rec.handObjOn}, renderers drawn {rec.handRendOn} / off {rec.handRendOff}");
        }
        /// <summary>A T-pose (U2 check fix 3, check only): both upper arms held straight out sideways (each along the hips' own left / right axis
        /// within 25 deg). Run 2 on L17 compared his hips' height with his HIDDEN live model's current pose instead: that model plays its carry /
        /// hold clip (tough_carry, psycho_hold: lower hips) while the figure is drawn, so the Tough's and the psycho's armed idle and walk read as a
        /// 'T-pose' at 1.17-1.21 (also with the size taken by leg length); their close frames show neither a T-pose nor a stiff stance
        /// (checks/u2/exit_frames: frames.txt + sheet_*.png).</summary>
        bool TPose(GameObject fig, ZObject r)
        {
            var a = fig.GetComponentInChildren<Animator>(); if (!a || !a.isHuman) return false;
            var ll = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var rl = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var lu = a.GetBoneTransform(HumanBodyBones.LeftUpperArm); var lf = a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var ru = a.GetBoneTransform(HumanBodyBones.RightUpperArm); var rf = a.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (!ll || !rl || !lu || !lf || !ru || !rf) return false;
            var right = rl.position - ll.position; if (right.sqrMagnitude < 1e-6f) return false; right.Normalize();
            float cos = Mathf.Cos(25f * Mathf.Deg2Rad);
            return Vector3.Dot((lf.position - lu.position).normalized, -right) > cos && Vector3.Dot((rf.position - ru.position).normalized, right) > cos;
        }

        // ------------------------------------------------------------------ E / Es: nobody shoots the emptied tank
        static bool ShotAt(ZEvent e, ZObject v) => e.otherId == v.Id && (e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired);
        void CpuAttack(ZObject u, ZObject t) => W.SendWaypoints(u.Owner, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, attackTo = true, playerGiven = true, x = t.LocX + 8, y = t.LocY + 8 } });

        IEnumerator EmptyRows(bool snipe)
        {
            string row = snipe ? "Es its crew sniped out while an enemy jeep and grunt squad shoot our light tank" : "E EXIT DRIVER while an enemy jeep and grunt squad shoot our light tank";
            var at = new int[2]; yield return Spot(18, 10, at);
            if (at[0] < 0) { Log(row, false, "no free ground near the camera"); yield break; }
            var v = Make("vehicle", "light", me, (at[0] + 2) * 16, (at[1] + 4) * 16); Tough(v);
            var foes = new List<ZObject>();
            var jeep = Make("vehicle", "jeep", foe, (at[0] + 12) * 16, (at[1] + 4) * 16); Tough(jeep); foes.Add(jeep);
            var squad = Make("robot", "grunt", foe, (at[0] + 10) * 16, (at[1] + 8) * 16, 3); Tough(squad); foes.Add(squad); foes.AddRange(squad.Minions);
            CpuAttack(jeep, v); CpuAttack(squad, v);
            bool snipersIn = false;                                                                           // U2 check fix 5 (check only): run 4 (UE) the snipers shot the driver out before the jeep or the squad fired once, so Es had no attack to stop; they now come once both fired
            long lastTick = -1; int jeepShots = 0, squadShots = 0, after = 0; bool emptied = false; long emptyTick = -1; float t0 = Time.time, tEmpty = -1f;
            var ids = new HashSet<int>(foes.Select(f => f.Id));
            while (Time.time - t0 < 60f)
            {
                yield return null;
                if (W.TickIndex == lastTick) continue;
                lastTick = W.TickIndex;
                foreach (var e in W.Events)
                {
                    if (!ShotAt(e, v) || !ids.Contains(e.objId)) continue;
                    if (emptied) after++;
                    else if (e.objId == jeep.Id) jeepShots++; else squadShots++;
                }
                if (snipe && !snipersIn && jeepShots > 0 && squadShots > 0)
                {
                    snipersIn = true; v.Drivers[0] = 1;
                    for (int k = 0; k < 3; k++) { var sn = Make("robot", "sniper", foe, (at[0] + 9 + k) * 16, (at[1] + 1) * 16, 3); Tough(sn); CpuAttack(sn, v); }
                }
                if (snipe && !emptied && W.Events.Any(e => e.type == ZEventType.Sniped && e.objId == v.Id)) { emptied = true; emptyTick = W.TickIndex; tEmpty = Time.time; }
                if (!snipe && !emptied && jeepShots > 0 && squadShots > 0 && v.CanExitDriver(out _))
                {
                    if (input.EjectUnit(v)) { emptied = true; emptyTick = W.TickIndex; tEmpty = Time.time; var d = DriverOf(v); if (d != null) made.Add(d); }
                }
                if (emptied && Time.time - tEmpty >= EmptySec) break;
            }
            int holders = 0; var doing = new List<string>();
            foreach (var f in foes)
            {
                if (f.AttackObject == v || f.Waypoints.Any(q => (q.mode == WpMode.Attack || q.mode == WpMode.Agro) && q.refId == v.Id)) holders++;
                doing.Add($"{f.Type} #{f.Id}: {(f.AttackObject != null ? $"target {f.AttackObject.Type} #{f.AttackObject.Id} {f.AttackObject.Owner}" : "no target")}");
            }
            bool ok = emptied && (jeepShots + squadShots) > 0 && after == 0 && holders == 0 && Time.time - tEmpty >= EmptySec - 0.1f;
            Log(row, ok, $"shots at it before: the jeep {jeepShots}, the squad {squadShots}; emptied {emptied} (tick {emptyTick}); shots STARTED at the empty tank by them in the next {(tEmpty >= 0f ? Time.time - tEmpty : 0f):0.0} s: {after} (want 0); "
                         + $"still on it at the end {holders}; they do: {string.Join("; ", doing)}");
        }

        // ------------------------------------------------------------------ W / Wm: the wobble never fights the death
        /// <summary>One end-of-frame look at the watched robots: a reaction and a death on one body; a frozen reaction's body moving (frozen
        /// in this and the last frame, its root moved more than StillM or turned more than StillDeg); survivors reacting; deaths drawn.</summary>
        class WobbleWatch
        {
            public int both, frozenMoving, frozenFrames, survivorReactFrames, deaths;
            public readonly HashSet<int> seenDead = new HashSet<int>();
            readonly Dictionary<int, (Vector3 p, Quaternion q)> lastFrozen = new Dictionary<int, (Vector3, Quaternion)>();
            public void Look(ZGame g, HashSet<int> lives, HashSet<int> dies)
            {
                var now = new HashSet<int>();
                foreach (var kv in g.Views)
                {
                    var uv = kv.Value; if (!uv || uv.Obj == null || uv.Obj.Kind != ZKind.Robot) continue;
                    if (!lives.Contains(kv.Key) && !dies.Contains(kv.Key)) continue;
                    if (uv.ReactAndDeathBoth) both++;
                    if (lives.Contains(kv.Key) && uv.ReactingAny && !uv.Dying) survivorReactFrames++;
                    if (dies.Contains(kv.Key) && uv.Dying && seenDead.Add(kv.Key)) deaths++;
                    if (uv.ReactFrozenNow && !uv.Dying)
                    {
                        frozenFrames++; now.Add(kv.Key);
                        var t = uv.transform;
                        if (lastFrozen.TryGetValue(kv.Key, out var was) && (Vector3.Distance(was.p, t.position) > StillM || Quaternion.Angle(was.q, t.rotation) > StillDeg)) frozenMoving++;
                        lastFrozen[kv.Key] = (t.position, t.rotation);
                    }
                }
                foreach (var id in lastFrozen.Keys.Where(k => !now.Contains(k)).ToList()) lastFrozen.Remove(id);
            }
        }

        IEnumerator WobbleRows()
        {
            string row = "W tank shells on two enemy grunt squads (one tough enough to live): the reaction and the death never drive one body";
            var at = new int[2]; yield return Spot(18, 12, at);
            if (at[0] < 0) { Log(row, false, "no free ground near the camera"); yield break; }
            var heavy = Make("vehicle", "heavy", me, (at[0] + 1) * 16, (at[1] + 3) * 16); Tough(heavy);
            var light = Make("vehicle", "light", me, (at[0] + 1) * 16, (at[1] + 8) * 16); Tough(light);
            var dying = Make("robot", "grunt", foe, (at[0] + 12) * 16, (at[1] + 4) * 16, 3); dying.Damage = 0; foreach (var m in dying.Minions) m.Damage = 0;
            var living = Make("robot", "grunt", foe, (at[0] + 13) * 16, (at[1] + 8) * 16, 3); Tough(living); living.Damage = 0; foreach (var m in living.Minions) m.Damage = 0;
            var lives = new HashSet<int>(new[] { living.Id }.Concat(living.Minions.Select(m => m.Id)));
            var dies = new HashSet<int>(new[] { dying.Id }.Concat(dying.Minions.Select(m => m.Id)));
            int fz0 = UnitView.DeathWinsFrozen, d0 = UnitView.DeathWinsDropped, f0 = UnitView.DeathWinsRefused, ff0 = UnitView.ReactFrozenFrames;
            CpuAttack(heavy, dying); CpuAttack(light, living);
            var eof = new WaitForEndOfFrame(); float t0 = Time.time; var ww = new WobbleWatch();
            while (Time.time - t0 < 25f)
            {
                yield return eof;
                ww.Look(g, lives, dies);
                if ((heavy.AttackObject == null && heavy.Waypoints.Count == 0) && ww.deaths >= dies.Count) CpuAttack(heavy, living);   // the dying squad is gone: shell the living one as well
                if (light.Waypoints.Count == 0) CpuAttack(light, living);
                if (ww.deaths >= dies.Count && Time.time - t0 > 12f) break;
            }
            bool ok = ww.both == 0 && ww.frozenMoving == 0 && ww.deaths > 0 && ww.survivorReactFrames > 0;
            Log(row, ok, $"frames with a reaction and a death on one body {ww.both} (want 0); frames a frozen reaction's body moved {ww.frozenMoving} (want 0) of {ww.frozenFrames} frozen; deaths drawn {ww.deaths} of {dies.Count}; "
                         + $"survivor frames with a reaction drawn {ww.survivorReactFrames} (want > 0: a hit that does not kill still wobbles)");
            Note($"INFO the death wins: reactions frozen at their death event {UnitView.DeathWinsFrozen - fz0} (frozen frames {UnitView.ReactFrozenFrames - ff0}), waiting reactions dropped {UnitView.DeathWinsDropped - d0}, refused on the dead {UnitView.DeathWinsRefused - f0}");
        }

        /// <summary>Wm (review 2): the conflict itself, in an MLRS ripple. Our MLRS fires on an enemy light tank; once its shot is queued, enemy
        /// grunts (30 HP) go inside its splash but outside rocket 1's core (as OzKC5bCheck places them), and each is kept reacting (half a long
        /// knockdown, half staggers started again as each ends) until the shot kills them: every death event then meets a running reaction.</summary>
        IEnumerator WobbleRippleRows()
        {
            string row = "Wm an MLRS salvo kills enemy grunts that are still wobbling from an earlier hit: the wobble frozen, the death thrown when its own rocket lands";
            var at = new int[2]; yield return Spot(26, 14, at);
            if (at[0] < 0) { Log(row, false, "no free ground near the camera"); yield break; }
            var mlrs = Make("vehicle", "missile_launcher", me, (at[0] + 1) * 16, (at[1] + 6) * 16); Tough(mlrs);
            var target = Make("vehicle", "light", foe, (at[0] + 10) * 16, (at[1] + 6) * 16); Tough(target); target.Damage = 0;   // U2 check fix 5 (check only): 9 tiles (144 px) off, inside the MLRS's 160 px reach (run 4: 18 tiles = 288 px, it drove off to close in and never fired in 12 s)
            int fz0 = UnitView.DeathWinsFrozen, th0 = UnitView.DeathWinsFrozenThrown, hd0 = UnitView.DeathWinsFrozenHeld, ea0 = UnitView.DeathWinsFrozenEarly, rp0 = BlastWave.ByRipple;
            CpuAttack(mlrs, target);
            ZMissile shot = null; float t0 = Time.time;
            while (shot == null && Time.time - t0 < 12f)
            {
                yield return null;
                foreach (var m in W.PendingMissiles) if (m.attackerId == mlrs.Id && !m.debris && !m.grenade) shot = m;
                foreach (var m in W.QueuedMissiles) if (m.attackerId == mlrs.Id && !m.debris && !m.grenade) shot = m;
            }
            if (shot == null) { Log(row, false, "the MLRS did not fire within 12 s"); yield break; }
            var dies = new HashSet<int>(); var views = new List<UnitView>();
            for (int k = 0; k < 16 && dies.Count < 8; k++)
            {
                float ang = k * 22.5f * Mathf.Deg2Rad, rr = (k & 1) == 0 ? 50f : Mathf.Max(52f, shot.radius - 16f);
                int cx = shot.x + (int)(Mathf.Cos(ang) * rr), cy = shot.y + (int)(Mathf.Sin(ang) * rr);
                if (rr > shot.radius - 12) continue;                                                            // the sim's splash kills it; outside rocket 1's core (>= 44 px)
                if (!W.Path.TilePassable(cx >> 4, cy >> 4, true)) continue;
                if (W.Objects.Any(o => o.IsUnit && !o.Destroyed && Mathf.Abs(o.CenterX - cx) < 20 && Mathf.Abs(o.CenterY - cy) < 20)) continue;
                var r = W.CreateObject("robot", "grunt", cx - 8, cy - 8, foe);
                g.SpawnUnitView(r); made.Add(r);
                r.Damage = 0; r.SetHealth(30); dies.Add(r.Id);
            }
            if (dies.Count == 0) { Log(row, false, $"no open spot inside the MLRS shot's splash ({shot.radius} px) outside rocket 1's core"); yield break; }
            var blast = ZGame.PxToWorld(shot.x, shot.y);
            var eof = new WaitForEndOfFrame(); var ww = new WobbleWatch(); var none = new HashSet<int>(); int n = 0;
            t0 = Time.time;
            while (Time.time - t0 < 14f)
            {
                yield return eof;
                foreach (var id in dies)
                {
                    if (!g.Views.TryGetValue(id, out var uv) || !uv || uv.Dying || uv.ReactingAny) continue;
                    var o = W.GetAny(id); if (o == null || o.Destroyed || o.KillAt >= 0) continue;
                    bool down = (id & 1) == 0;
                    uv.React(blast, down ? 3 : 2, (uint)id * 2654435761u + (uint)n++, 0f, down ? 6f : -1f);   // kept wobbling until the shot lands (a long knockdown or staggers)
                }
                ww.Look(g, none, dies);
                if (ww.deaths >= dies.Count && Time.time - t0 > 4f) break;
            }
            var log = UnitView.DeathWinsLog.Where(x => dies.Contains(x.id)).ToList();
            int held = log.Count(x => x.wave > 0f), early = log.Count(x => x.wave > 0f && x.thrownFrame == x.eventFrame), before = log.Count(x => x.wave > 0f && x.thrownAt + 0.02f < x.eventAt + x.wave);
            float minLate = log.Where(x => x.wave > 0f).Select(x => x.thrownAt - (x.eventAt + x.wave)).DefaultIfEmpty(0f).Min();
            bool ok = log.Count > 0 && held > 0 && early == 0 && before == 0 && ww.frozenMoving == 0 && ww.both == 0 && UnitView.DeathWinsFrozenEarly - ea0 == 0;
            Log(row, ok, $"grunts put in the splash {dies.Count}, deaths drawn {ww.deaths}; reactions frozen at the death event and thrown {log.Count} (want > 0), of them held for their ripple rocket {held} (want > 0); "
                         + $"thrown in the death event's own frame while held {early} (want 0), thrown before the hold's moment {before} (want 0; the earliest throw {minLate:+0.000;-0.000} s against it); "
                         + $"frames a frozen body moved {ww.frozenMoving} (want 0) of {ww.frozenFrames}; a reaction and a death on one body {ww.both} (want 0)");
            Note($"INFO counters in Wm: frozen {UnitView.DeathWinsFrozen - fz0}, thrown {UnitView.DeathWinsFrozenThrown - th0}, held {UnitView.DeathWinsFrozenHeld - hd0}; BlastWave ripple holds {BlastWave.ByRipple - rp0}; "
                 + string.Join(", ", log.Select(x => $"#{x.id} kind {x.kind}: wave {x.wave:0.00} s, thrown {x.thrownAt - x.eventAt:0.00} s after the event")));
        }

        static string Kind(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech" : t;
    }
}
#endif
