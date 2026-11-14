#if !OZ_RELEASE
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
    /// playing, the normal RTS camera, about 4 minutes. EvilZod on the human's team beside targets that do not fire back; the rules drive everything. Every volley
    /// his view draws is measured on its frame (OzEvilZodFx.FaceLog): the DRAWN gun's line against the line from his muzzle to the target (the line the pellets
    /// fly), and his body's front.
    ///  B  behind: he walks east, stands, a target appears behind him (west): he turns, then fires;
    ///  S  side: a second target to his north, the first one dies: he turns 90 deg, then fires;
    ///  R  a ring of six that die one per shot, each next one somewhere else round him;
    ///  M  a target that walks past him;
    ///  P  (fix round 1) right after a stop, four times: a plain move order past a standing target that ends about 133 deg off his walk line, so the rules pick it
    ///     and fire within ticks of his last step while the drawn legs still show the walk's tail;
    ///  D  (fix round 1) an ordered dash, the target steps off the dash line while he dashes (fix round 2: up to three tries, FAIL without a dash);
    ///  O  the player's attack order on a target behind him (the walk or the dash, the dash shot);
    ///  K  (fix round 2) a kill shot: the farther target, first in object order, dies to his second shot while a nearer enemy stands 18 / 28 deg to its side (the gun
    ///     must stay on the dying target until its volley is drawn: OzEvilZodBody.Target);
    ///  X  (fix round 1) out of a shuttle on the ground with an enemy in range to the side of the ramp (he is a new object: the rules' heading starts south).
    /// Bars: at least <see cref="MinShots"/> volleys drawn, every one with the drawn gun within OzEvilZodFx.FaceTolDeg of the target, none drawn at the cap
    /// (OzEvilZodFx.FaceWaitMax); and in the rules every shot within 10 deg of his heading, read from the rules' own note of each shot (ZObject.EvilFaceShotOff:
    /// the heading and bearing on the shot's tick, so a shot that killed its target is measured too), at least MinShots rows. The report and three frame sheets
    /// (scenes B, S and P1: every second frame from the target's appearance, or his last steps, to 0.3 s after the volley) go to <see cref="Folder"/>.
    /// Test only: the units it made stay.
    /// </summary>
    [DefaultExecutionOrder(1000)]   // after OzEvilZodFx (82): a volley drawn this frame is in FaceLog when this LateUpdate looks
    public sealed class OzEvilFacePlayCheck : MonoBehaviour
    {
        public static string Report = "idle";
        public const int MinShots = 12;
        ZGame g; ZWorld W; RtsCamera rts; Team me, foe; int pass, fail; ZObject ez;
        readonly StringBuilder log = new StringBuilder();
        readonly List<string> frames = new List<string>(); bool grabbing; int grabN; string grabTag; float grabStop = -1f;
        readonly List<string> simRows = new List<string>(); int simShots, simBad, simMeasured, lastShots, lastFaceShots;
        readonly HashSet<int> ids = new HashSet<int>();                                          // every EvilZod object of the check (X: a new one out of the shuttle)

        void Him(ZObject o) { ez = o; ids.Add(o.Id); lastFaceShots = o.EvilFaceShotCount; }

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_4", "kits", "evilzod_face_before_fire");
                if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.Combine(Application.persistentDataPath, "evilzod_face_play_check");
        }

        public static void Run() { Report = "running"; new GameObject("OzEvilFacePlayCheck").AddComponent<OzEvilFacePlayCheck>(); }

        void Start() { StartCoroutine(All()); }

        void Log(string s) { log.AppendLine(s); Debug.Log("[OpenZ EvilZod face] " + s); }
        void Check(string what, bool ok) { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }

        bool Walkable(int x, int y) => !W.Path.WithinImpassable(x - 8, y - 8, 16, 16, out _, out _, true) && !W.Map.LavaAt(x >> 4, y >> 4)
                                       && !W.UnitObjects.Any(o => !o.Destroyed && System.Math.Abs(o.CenterX - x) < 40 && System.Math.Abs(o.CenterY - y) < 40);

        /// <summary>A free disc of 150 px near the camera.</summary>
        bool FindSpot(out int x, out int y)
        {
            int fx = (int)(rts.focus.x * 8f), fy = (int)(-rts.focus.z * 8f);
            for (int r = 0; r < 1200; r += 32)
                for (int a = 0; a < 16; a++)
                {
                    x = fx + (int)(r * Mathf.Cos(a * 0.3927f)); y = fy + (int)(r * Mathf.Sin(a * 0.3927f));
                    bool ok = true;
                    for (int k = 0; ok && k < 16; k++) for (int rr = 0; ok && rr <= 150; rr += 25) ok = Walkable(x + (int)(rr * Mathf.Cos(k * 0.3927f)), y + (int)(rr * Mathf.Sin(k * 0.3927f)));
                    if (ok) return true;
                }
            x = y = 0; return false;
        }

        ZObject Target(int cx, int cy, bool bigHp = true)
        {
            var t = W.CreateRobotGroup("grunt", cx - 8, cy - 8, foe, 1);
            t.Damage = 0; t.MoveSpeed = 0;
            if (bigHp) { t.MaxHealth = 10000000; t.Health = 10000000; }
            g.SpawnUnitView(t);
            return t;
        }

        void Order(ZObject u, ZWaypoint wp) => W.SendWaypoints(u.Owner, u, new[] { wp });
        void Attack(ZObject u, ZObject t) => Order(u, new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY });
        static void Kill(ZWorld w, ZObject t) { if (t != null && !t.Destroyed) { t.SetHealth(0); w.UpdateObjectHealth(t); } }

        void Grab(string tag) { frames.Clear(); grabbing = true; grabN = 0; grabTag = tag; grabStop = -1f; }

        /// <summary>Every frame: the rules' shots (fix round 1: the rules' own note of each shot, his heading and the target's bearing on the shot's tick, so a shot
        /// that killed its target or a later tick of the frame changes nothing), the frame grab of a scene, its stop 0.3 s after the first volley.</summary>
        void LateUpdate()
        {
            if (W != null && ez != null && ez.EvilFaceShotCount != lastFaceShots)
            {
                int n = ez.EvilFaceShotCount - lastFaceShots; lastFaceShots = ez.EvilFaceShotCount; simShots += n; simMeasured++;
                int err = ez.EvilFaceShotOff, b = ez.EvilFaceShotBearing;
                if (err > ZEvilFace.TolCenti) simBad++;
                simRows.Add($"rules' shot {simShots}{(n > 1 ? $" ({n} shots this frame, the last one noted)" : "")}: heading {ez.EvilFaceShotHeading / 100f:0.0}, bearing {(b < 0 ? "- (on top of him)" : (b / 100f).ToString("0.0"))}, "
                            + $"{err / 100f:0.0} deg off, waited {ez.EvilFaceShotWaitTicks} ticks for the turn");
            }
            if (!grabbing) return;
            if (grabStop < 0f && OzEvilZodFx.FaceLog.Count > 0 && OzEvilZodFx.FaceLog[OzEvilZodFx.FaceLog.Count - 1].at >= Time.time - 0.0001f && grabN > 0) grabStop = Time.time + 0.3f / Mathf.Max(0.25f, g.gameSpeed);
            if (grabN++ % 2 == 0 && frames.Count < 36)
            {
                string p = Path.Combine(Folder(), grabTag + "_" + frames.Count.ToString("00") + ".png");
                ScreenCapture.CaptureScreenshot(p); frames.Add(p);
            }
            if (grabStop > 0f && Time.time >= grabStop) grabbing = false;
        }

        IEnumerator Sheet(string tag)
        {
            yield return WaitFor(() => !grabbing, 8f); grabbing = false;
            yield return OzProofPictures.AfterGrab();
            foreach (var f in frames) yield return OzProofPictures.Written(f, 3f);
            string outp = Path.Combine(Folder(), tag + "_frames.jpg");
            bool ok = OzProofPictures.Sheet(frames, 6, 480, outp);
            Check($"frame sheet {tag}: {frames.Count} frames (every second frame from the target's appearance to 0.3 s after the volley) -> {outp}", ok && frames.Count >= 4);
        }

        void Stage(string name, int from, int minShots)
        {
            var rows = OzEvilZodFx.FaceLog.Skip(from).Where(r => r.id == ez.Id).ToList();
            float worst = rows.Count > 0 ? rows.Max(r => r.gun) : 0f, body = rows.Count > 0 ? rows.Max(r => r.body) : 0f, wait = rows.Count > 0 ? rows.Max(r => r.wait) : 0f;
            Check($"{name}: {rows.Count} volleys drawn (at least {minShots}), the drawn gun at most {worst:0.0} deg off the line to the target (bar {OzEvilZodFx.FaceTolDeg:0}), his body at most {body:0.0} deg, "
                  + $"{rows.Count(r => r.held)} held for the gun, {rows.Count(r => r.timeout)} drawn at the cap, the longest hold {wait:0.000} s after the rules' shot",
                  rows.Count >= minShots && rows.All(r => r.gun <= OzEvilZodFx.FaceTolDeg && !r.timeout));
        }

        IEnumerator All()
        {
            g = ZGame.I; W = g ? g.World : null; rts = FindFirstObjectByType<RtsCamera>();
            if (W == null || !rts) { Log("no level in Play"); fail++; Finish(); yield break; }
            if (!ZWorld.EvilZodDeviation || !ZWorld.EvilFaceFireDeviation) { Log("EvilZod or his facing rule is switched off in this game"); fail++; Finish(); yield break; }
            Directory.CreateDirectory(Folder());
            me = g.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 30f);
            W.Bots.Clear(); OzEvilZodFx.FaceReset();
            Log($"level {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, game speed {g.gameSpeed:0.00} (the owner's clip: 1x), the view's gate {OzEvilZodFx.FaceTolDeg:0} deg, at most {OzEvilZodFx.FaceWaitMax:0.00} s");
            if (!FindSpot(out int x, out int y)) { Check("a free place near the camera", false); Finish(); yield break; }
            rts.focus = ZGame.PxToWorld(x, y); rts.height = 70f;                                  // the normal RTS height
            yield return Real(1.5f);
            Him(W.CreateRobotGroup(ZEvilZod.Type, x - 8, y - 8, me, 1)); var v = g.SpawnUnitView(ez);
            yield return Real(0.5f);
            Check("his view has the shot's effects (OzEvilZodFx: the prefab is his)", v && v.GetComponent<OzEvilZodFx>());
            lastShots = W.EvilShots;

            // ---- B: behind
            int n0 = OzEvilZodFx.FaceLog.Count;
            Order(ez, ZWaypoint.Move(x + 64, y, false));
            yield return WaitFor(() => !ez.IsMoving && ez.Waypoints.Count == 0, 15f);
            yield return Real(1f);
            Grab("behind");
            var tb = Target(ez.CenterX - 110, ez.CenterY);
            int s0 = W.EvilShots;
            yield return WaitFor(() => W.EvilShots - s0 >= 3, 20f);
            yield return Sheet("behind");
            Stage("B a target behind him (west, he faced east)", n0, 3);

            // ---- S: side
            n0 = OzEvilZodFx.FaceLog.Count;
            var ts = Target(ez.CenterX, ez.CenterY - 110);
            yield return Real(0.5f);
            Grab("side");
            Kill(W, tb); s0 = W.EvilShots;
            yield return WaitFor(() => W.EvilShots - s0 >= 3, 20f);
            yield return Sheet("side");
            Stage("S the first one dies, a second one to his north: he turns 90 deg", n0, 3);
            Kill(W, ts);
            yield return Real(2.5f);

            // ---- R: a ring of six that die one per shot
            n0 = OzEvilZodFx.FaceLog.Count; s0 = W.EvilShots;
            foreach (int k in new[] { 0, 7, 3, 12, 9, 1 }) Target(ez.CenterX + (int)(50 * Mathf.Cos(k * 0.3927f)), ez.CenterY + (int)(50 * Mathf.Sin(k * 0.3927f)), false);
            yield return WaitFor(() => W.EvilShots - s0 >= 6, 30f);
            yield return Real(1f);
            Stage("R a ring of six round him, one dies per shot, the next one elsewhere", n0, 5);

            // ---- M: a target that walks past him
            n0 = OzEvilZodFx.FaceLog.Count; s0 = W.EvilShots;
            var tm = W.CreateRobotGroup("grunt", ez.CenterX - 130 - 8, ez.CenterY + 90 - 8, foe, 1); tm.Damage = 0; tm.MaxHealth = 10000000; tm.Health = 10000000; g.SpawnUnitView(tm);
            Order(tm, ZWaypoint.Move(ez.CenterX + 130, ez.CenterY + 90, false));
            yield return WaitFor(() => W.EvilShots - s0 >= 3 || (tm.Waypoints.Count == 0 && !tm.IsMoving), 30f);
            yield return Real(1f);
            Stage("M a target walking past him", n0, 2);
            Kill(W, tm);
            yield return Real(2.5f);

            // ---- P (fix round 1): right after a stop. A plain move order 64 px east or west past a standing target that ends about 133 deg off his walk line,
            // 100 px away (in his range all the way: under a move order he does not fire); the rules pick it once he stands, while the drawn legs still walk
            for (int k = 0; k < 4; k++)
            {
                int dir = k % 2 == 0 ? 1 : -1, side = k < 2 ? 1 : -1;
                int sx = ez.CenterX + 64 * dir, sy = ez.CenterY;                                   // where the order stops him
                n0 = OzEvilZodFx.FaceLog.Count; s0 = W.EvilShots;
                var tp = Target(sx - 70 * dir, sy + 75 * side);
                Order(ez, ZWaypoint.Move(sx, sy, false));
                if (k == 0)
                {
                    yield return WaitFor(() => System.Math.Abs(ez.CenterX - sx) <= 6 || W.EvilShots != s0, 15f);   // his last steps on the sheet
                    Grab("stop");
                }
                yield return WaitFor(() => W.EvilShots - s0 >= 1, 15f);
                if (k == 0) yield return Sheet("stop"); else yield return Real(0.6f);
                Stage($"P{k + 1} right after a stop: a move order {(dir > 0 ? "east" : "west")}, the target {(side > 0 ? "south" : "north")}-{(dir > 0 ? "west" : "east")} of the stop, about 133 deg off his walk line", n0, 1);
                Kill(W, tp);
                yield return Real(2.5f);
            }

            // ---- D (fix round 1): an ordered dash (when it is ready), the target steps off the dash line while he dashes. Fix round 2: up to three tries until a
            // dash really starts (the cooldown, and EvilDashAllowed: his gun ready by the end of the windup), and no PASS without one
            bool dashed = false; ZObject td = null;
            for (int tryN = 0; tryN < 3 && !dashed; tryN++)
            {
                if (td != null) { Kill(W, td); yield return Real(2.5f); }
                n0 = OzEvilZodFx.FaceLog.Count;
                yield return WaitFor(() => ez.EvilDashReadyTick == 0, 30f);
                yield return Real(2.2f / Mathf.Max(0.25f, g.gameSpeed));                        // his reload (2 s) over too: EvilDashAllowed wants the gun ready
                s0 = W.EvilShots;
                td = W.CreateRobotGroup("grunt", ez.CenterX + 170 - 8, ez.CenterY - 8, foe, 1); td.Damage = 0; td.MaxHealth = 10000000; td.Health = 10000000; g.SpawnUnitView(td);
                Attack(ez, td);
                yield return WaitFor(() => ez.EvilDashing || W.EvilShots != s0, 5f);
                dashed = ez.EvilDashing;
                Log($"D try {tryN + 1}: {(dashed ? "the dash started; the target steps off its line" : "no dash")}");
            }
            Order(td, ZWaypoint.Move(td.CenterX, td.CenterY + 90, false));                       // off the line, sideways
            yield return WaitFor(() => W.EvilShots - s0 >= 2, 20f);
            yield return Real(1f);
            Check("D the dash started (at most three tries: without a dash the after-dash case is not measured)", dashed);
            if (dashed) Stage("D an ordered dash, the target steps off the dash line", n0, 2);
            Kill(W, td);
            yield return Real(2.5f);

            // ---- O: the player's order on a target behind him
            n0 = OzEvilZodFx.FaceLog.Count; s0 = W.EvilShots;
            var to = Target(ez.CenterX - 150, ez.CenterY + 20);
            Attack(ez, to);
            yield return WaitFor(() => W.EvilShots - s0 >= 2, 25f);
            yield return Real(1f);
            Stage("O the player's attack order on a target behind him", n0, 2);
            Kill(W, to);
            yield return Real(2.5f);

            // ---- K (fix round 2): a kill shot with a nearer enemy beside the dying target. A, the farther one, made first (the rules' passive pick is the first
            // enemy in range in object order), dies to one shot; B, nearer, stands 18 / 28 deg to its side, inside the 35 deg his upper body looks round for
            // (OzEvilZodBody alert). A survives the first shot (so the look round has surely found B), then has 1 HP: the second shot kills it. On the kill's
            // frame A is Destroyed already: the gun must stay on A until that volley is drawn
            for (int k = 0; k < 2; k++)
            {
                n0 = OzEvilZodFx.FaceLog.Count; s0 = W.EvilShots;
                float h = ez.EvilFacing / 100f * Mathf.Deg2Rad, side = (k == 0 ? 18f : -28f) * Mathf.Deg2Rad;      // his rules heading: 0 east, 90 south (map y down)
                var ka = Target(ez.CenterX + (int)(105f * Mathf.Cos(h)), ez.CenterY + (int)(105f * Mathf.Sin(h)));
                var kb = Target(ez.CenterX + (int)(60f * Mathf.Cos(h + side)), ez.CenterY + (int)(60f * Mathf.Sin(h + side)));
                yield return WaitFor(() => W.EvilShots - s0 >= 1, 20f);
                if (!ka.Destroyed) ka.Health = 1;                                                     // the next shot kills it
                yield return WaitFor(() => W.EvilShots - s0 >= 3, 20f);
                yield return Real(1f);
                var kr = OzEvilZodFx.FaceLog.Skip(n0).Where(r => r.id == ez.Id && r.target == ka.Id).ToList();
                Check($"K{k + 1} the kill shot on the farther target A (B nearer, {Mathf.Abs(side * Mathf.Rad2Deg):0} deg to its side): {kr.Count} volleys drawn at A (at least 2, the second the kill), A dead {ka.Destroyed}, "
                      + $"the drawn gun {(kr.Count > 0 ? kr.Max(r => r.gun) : 0f):0.0} deg off A (bar {OzEvilZodFx.FaceTolDeg:0}), {kr.Count(r => r.timeout)} at the cap",
                      kr.Count >= 2 && ka.Destroyed && kr.All(r => r.gun <= OzEvilZodFx.FaceTolDeg && !r.timeout));
                Stage($"K{k + 1} a kill shot with a nearer enemy beside the dying target, then the next volley at B", n0, 3);
                Kill(W, ka); Kill(W, kb);
                yield return Real(2.5f);
            }

            // ---- X (fix round 1): out of a shuttle on the ground, an enemy in range beside the ramp. He is a new object after the ride (the rules' heading starts
            // south, whatever way his drawn body comes down the ramp), and the rules hold him while the view walks him down (ExitWalkHold)
            if (!FindSpot(out int qx, out int qy)) Check("X a free place for the shuttle near the camera", false);
            else
            {
                var ship = W.CreatePlayableShuttle(qx, qy, me); g.SpawnUnitView(ship);
                var (fx, fy) = ship.ShuttleBackPoint(1);
                ez.SetCords(fx - 8, fy - 8 + 40);
                W.SendWaypoints(me, ez, new[] { new ZWaypoint { mode = WpMode.Enter, refId = ship.Id, x = ship.CenterX, y = ship.CenterY } });
                yield return WaitFor(() => ship.Passengers.Count == 1, 40f);
                bool boarded = ship.Passengers.Count == 1;
                yield return WaitFor(() => ship.CanEjectCrew(), 30f);
                yield return Real(1f);
                var (rx, ry) = ship.ShuttleBackPoint(0);                                              // the ramp's foot
                float ax = rx - ship.CenterX, ay = ry - ship.CenterY, al = Mathf.Max(1f, Mathf.Sqrt(ax * ax + ay * ay));
                var tx = Target(rx + (int)(-ay / al * 90f + ax / al * 20f), ry + (int)(ax / al * 90f + ay / al * 20f));   // 90 px to the side of the ramp, 20 px out
                int oldId = ez.Id; n0 = OzEvilZodFx.FaceLog.Count; s0 = W.EvilShots;
                bool out1 = boarded && W.Eject(me, ship);
                ZObject h = null;
                yield return WaitFor(() => (h = W.Objects.FirstOrDefault(o => o.IsEvilZod && !o.Destroyed && o.KillAt < 0 && o.Owner == me && o.Id != oldId)) != null, 20f);
                Check($"X he boarded the shuttle and came out again (boarded {boarded}, EXIT {out1}, out {h != null})", boarded && out1 && h != null);
                if (h != null)
                {
                    Him(h);
                    yield return WaitFor(() => W.EvilShots - s0 >= 2, 20f);
                    yield return Real(1f);
                    Stage("X out of a shuttle, an enemy in range beside the ramp", n0, 2);
                }
                Kill(W, tx);
            }

            // ---- all
            var all = OzEvilZodFx.FaceLog.Where(r => ids.Contains(r.id)).ToList();
            Check($"all: {all.Count} volleys drawn (at least {MinShots}), {all.Count(r => r.gun > OzEvilZodFx.FaceTolDeg)} with the drawn gun more than {OzEvilZodFx.FaceTolDeg:0} deg off, {all.Count(r => r.timeout)} drawn at the cap; the worst {(all.Count > 0 ? all.Max(r => r.gun) : 0f):0.0} deg",
                  all.Count >= MinShots && all.All(r => r.gun <= OzEvilZodFx.FaceTolDeg && !r.timeout));
            Check($"the rules: {simShots} shots, {simMeasured} measured from the rules' own note of the shot (at least {MinShots}), {simBad} more than {ZEvilFace.TolCenti / 100} deg off his heading",
                  simMeasured >= MinShots && simBad == 0);
            foreach (var r in all) Log($"  drawn: target #{r.target}, gun {r.gun:0.0} deg off, body {r.body:0.0} deg, {(r.held ? "held for the gun" : "not held")}, {r.wait:0.000} s after the rules' shot{(r.timeout ? ", AT THE CAP" : "")}");
            foreach (var r in simRows) Log("  " + r);
            Finish();
        }

        void Finish()
        {
            string res = $"[OpenZ V1.4 EvilZod face play] {(fail == 0 && pass > 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}";
            Log(res); Report = res;
            try { Directory.CreateDirectory(Folder()); File.WriteAllText(Path.Combine(Folder(), "play_check_report.txt"), log.ToString()); } catch { }
            if (fail == 0 && pass > 0) Debug.Log(res); else Debug.LogError(res);
            Destroy(gameObject);
        }
    }
}
#endif
