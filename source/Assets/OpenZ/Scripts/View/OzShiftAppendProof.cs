using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 9 Play proof (rule DV135, View/OzShiftAppend.cs: a Shift queue is added to the order the units already have), on any level in
    /// Play with free flags (level 2 recommended; menu OpenZ/V1.2/Orders: 9 Shift Adds Proof (Play), Editor/OzShiftAppendMenu.cs). Set-up:
    /// the CPU teams removed, the other teams hold their fire (OzProofHoldFire), the player's other units hold position (none of them takes
    /// a flag by itself during the walks), morning (day clock 0.10, held). A selection of a grunt squad,
    /// two jeeps and a light tank is made beside the player's fort. Every order goes through PlayerInput's own path: OzShuttleQueueInput.Click,
    /// the flag's waypoint of a right-click (PlayerInput.OrderFor = BuildWaypoint with the flag under the pointer) into PlayerInput's own
    /// queue, then its Flush (by reflection), as the plain click does at once and the Shift queue when Shift goes up.
    /// <list type="bullet">
    /// <item>A, the case reported: a plain click on flag 1; while the units are on their way, a Shift click on flag 2. The Shift preview is
    /// shown first (the queue drawn without being sent, the K410 proof's way): its line goes on from flag 1. Then the lists sent, and the
    /// units take flag 1, then flag 2.</item>
    /// <item>B: from standing, Shift clicks on flag 3 and flag 4: [flag 3, flag 4], taken in that order.</item>
    /// <item>C to G, the lists only: two plain clicks (the second replaces), the rule off (the cause: [flag 2] only), movement-layer moves in
    /// front (not copied), a first move that cannot be overwritten (kept once), a patrol (replaced).</item>
    /// <item>H, the minimap and the real Shift key: a plain click on flag 1, then the Shift key held (the keyboard's own state, as
    /// View/InputSelfTest.cs holds it), the minimap's right-click order at flag 2 (OpenZMinimap.Order), Shift let go: PlayerInput sends
    /// the queue itself and the minimap's point goes after the order to flag 1.</item>
    /// </list>
    /// Counted PASS / FAIL lines and pictures at the normal RTS camera (70 m, full screen) in <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzShiftAppendProof : MonoBehaviour
    {
        public static string Report = "idle";
        /// <summary></summary>
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = System.IO.Path.Combine(d.FullName, "work", "staging");
                if (!System.IO.Directory.Exists(staging)) continue;
                foreach (var stage in System.IO.Directory.GetDirectories(staging))
                {
                    var kit = System.IO.Path.Combine(stage, "k9_shift_append");
                    if (System.IO.Directory.Exists(kit)) return System.IO.Path.Combine(kit, "proof");
                }
            }
            return System.IO.Path.Combine(Application.persistentDataPath, "k9_shift_append");
        }
        public static void Run()
        {
            Report = "running";
#if UNITY_EDITOR
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) UnityEditor.EditorWindow.FocusWindowIfItsOpen(gv);   // case H's Shift key reaches the game only with the Game view in focus
#endif
            new GameObject("OzShiftAppendProof").AddComponent<OzShiftAppendProof>();
        }

        /// <summary>The normal RTS camera height of the pictures (the game's start height).</summary>
        public const float CamHeight = 70f;
        /// <summary>A unit took its way to a flag when it came this close (px) to its own place there, or 80 px to the flag.</summary>
        const int SpotPx = 72, NearPx = 80;
        const float WalkMax = 150f;

        ZGame g; ZWorld W; PlayerInput input; RtsCamera rts; Team me = Team.Red;
        Dictionary<ZObject, List<ZWaypoint>> pending; System.Reflection.MethodInfo flush, setSelectTime;
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;
        Dictionary<ZObject, List<ZWaypoint>> shown; bool keepShown;
        readonly List<ZObject> sel = new List<ZObject>();
        ZObject f1, f2, f3, f4;

        void Start()
        {
            g = ZGame.I; W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>(); rts = FindFirstObjectByType<RtsCamera>();
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var pf = typeof(PlayerInput).GetField("pending", F);
            if (input && pf != null) pending = pf.GetValue(input) as Dictionary<ZObject, List<ZWaypoint>>;
            flush = typeof(PlayerInput).GetMethod("Flush", F, null, new[] { typeof(bool), typeof(Vector2) }, null);
            var st = typeof(PlayerInput).GetProperty("SelectTime"); setSelectTime = st != null ? st.GetSetMethod(true) : null;
            StartCoroutine(Unshow());
            StartCoroutine(All());
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzShiftAppendProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        IEnumerator WaitFor(System.Func<bool> c, float max) { float t = 0f; while (t < max && !c()) { t += Time.deltaTime; yield return null; } }
        IEnumerator Wait(float s0) { float t = 0f; while (t < s0) { t += Time.deltaTime; yield return null; } }
        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        void Cam(Vector3 focus, float h) { if (!rts) return; rts.FreeLookOff(); rts.focus = new Vector3(focus.x, 0f, focus.z); rts.SetHeightImmediate(h); }
        Vector3 Wp(int x, int y) => ZGame.PxToWorld(x, y);
        Vector3 Mid(params Vector3[] ps) { var s = Vector3.zero; foreach (var p in ps) s += p; return s / Mathf.Max(1, ps.Length); }
        Vector3 GroupAt() => Mid(sel.Where(Alive).Select(u => g.CenterWorld(u)).ToArray());
        IEnumerator Shot(string name) { yield return Frames(3); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); Log("   picture " + name + ".png"); yield return Frames(2); }
        static bool Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;
        static double D(int ax, int ay, int bx, int by) => System.Math.Sqrt((double)(ax - bx) * (ax - bx) + (double)(ay - by) * (ay - by));
        static double D(ZObject a, ZObject b) => D(a.CenterX, a.CenterY, b.CenterX, b.CenterY);
        static double LineDist(ZObject p, int ax, int ay, int bx, int by)
        {
            double dx = bx - ax, dy = by - ay, l = System.Math.Sqrt(dx * dx + dy * dy); if (l < 1) return D(p.CenterX, p.CenterY, ax, ay);
            return System.Math.Abs((p.CenterX - ax) * dy - (p.CenterY - ay) * dx) / l;
        }
        static string F(double v) => v < 0 ? "never" : v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s";
        string Tok(ZWaypoint w) => w.mode == WpMode.ForceMove ? "forced" : w.refId < -1 ? "helper" : w.refId == f1?.Id ? "flag1" : w.refId == f2?.Id ? "flag2" : w.refId == f3?.Id ? "flag3" : w.refId == f4?.Id ? "flag4" : "point";
        string List(ZObject u) => "[" + string.Join(", ", u.Waypoints.Select(Tok)) + "]";
        string Lists() => string.Join(" ", sel.Select(u => u.Type + " #" + u.Id + " " + List(u)));
        bool ListIs(params string[] want) => sel.All(u => u.Waypoints.Select(Tok).SequenceEqual(want));

        /// <summary>The Shift preview shown without being sent (the K410 proof's way): put into PlayerInput's queue after its Update (the overlay
        /// draws it in this frame's LateUpdate) and taken out at the end of the frame, so PlayerInput never sends it.</summary>
        void LateUpdate()
        {
            if (keepShown && setSelectTime != null && input) setSelectTime.Invoke(input, new object[] { Time.unscaledTime });
            if (shown != null && pending != null) foreach (var kv in shown) pending[kv.Key] = kv.Value;
        }
        IEnumerator Unshow()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (shown != null && pending != null) foreach (var kv in shown) if (pending.TryGetValue(kv.Key, out var l) && l == kv.Value) pending.Remove(kv.Key);
            }
        }
        void OnDestroy() { if (shown != null && pending != null) foreach (var kv in shown) if (pending.TryGetValue(kv.Key, out var l) && l == kv.Value) pending.Remove(kv.Key); shown = null; keepShown = false; }

        // ------------------------------------------------------------------ PlayerInput's own order path
        /// <summary>A right-click on a flag as PlayerInput.Update gives it: the queue's Click (first = the queue was empty), then the flag's
        /// waypoint for every selected unit into the queue.</summary>
        void Click(ZObject flag, bool shift)
        {
            OzShuttleQueueInput.Click(pending.Count == 0, shift);
            foreach (var u in input.Selection)
            {
                if (!pending.TryGetValue(u, out var l)) pending[u] = l = new List<ZWaypoint>();
                l.Add(input.OrderFor(u, flag, flag.CenterX, flag.CenterY));
            }
        }
        /// <summary>PlayerInput.Flush: at once after a plain click, when Shift goes up after a Shift queue (Z not held).</summary>
        void Send() => flush.Invoke(input, new object[] { false, Vector2.zero });
        void Order(ZObject flag, bool shift) { Click(flag, shift); Send(); }
        void Show(params ZObject[] flags)
        {
            var d = new Dictionary<ZObject, List<ZWaypoint>>();
            foreach (var u in sel) d[u] = flags.Select(fl => input.OrderFor(u, fl, fl.CenterX, fl.CenterY)).ToList();
            shown = d;
        }

        // ------------------------------------------------------------------ the set-up
        bool Layout(out int sx, out int sy)
        {
            sx = sy = 0;
            var home = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == me && !o.Destroyed) ?? W.Objects.FirstOrDefault(o => o.Kind == ZKind.Building && o.Owner == me && !o.Destroyed)
                       ?? W.Objects.FirstOrDefault(o => o.IsUnit && o.Owner == me && !o.Destroyed);
            if (home == null) { Log("no own fort, building or unit"); return false; }
            var enemies = W.Objects.Where(o => o.IsUnit && o.Owner != me && o.Owner != Team.Neutral && !o.Destroyed).ToList();
            var free = W.Flags.Where(f => f.Owner == Team.Neutral && enemies.All(e => D(e, f) > 384)).OrderBy(f => f.Id).ToList();
            if (free.Count < 4) { Log($"only {free.Count} free flags away from the enemy (4 wanted; level 2 recommended)"); return false; }
            double cx = free.Average(f => (double)f.CenterX), cy = free.Average(f => (double)f.CenterY), dl = D(home.CenterX, home.CenterY, (int)cx, (int)cy);
            sx = home.CenterX + (int)((cx - home.CenterX) / System.Math.Max(1, dl) * 160); sy = home.CenterY + (int)((cy - home.CenterY) / System.Math.Max(1, dl) * 160);
            int qx = sx, qy = sy;
            double best = double.MaxValue;
            foreach (var a in free) foreach (var b in free)
            {
                if (a == b) continue;
                double s1 = D(qx, qy, a.CenterX, a.CenterY), s2 = D(qx, qy, b.CenterX, b.CenterY), ab = D(a, b);
                if (s1 < 160 || s1 > 560 || ab < 160 || ab > 640 || s2 <= s1 || LineDist(a, qx, qy, b.CenterX, b.CenterY) < 128) continue;
                if (s1 + ab < best) { best = s1 + ab; f1 = a; f2 = b; }
            }
            if (f1 == null) { Log("no flag pair for case A (flag 1 10-35 tiles from the start, flag 2 10-40 tiles beyond it, flag 1 8 tiles or more off the straight way to flag 2)"); return false; }
            best = double.MaxValue;
            foreach (var a in free) foreach (var b in free)
            {
                if (a == b || a == f1 || a == f2 || b == f1 || b == f2) continue;
                double s1 = D(f2, a), ab = D(a, b);
                if (s1 < 160 || s1 > 640 || ab < 160 || ab > 640 || LineDist(a, f2.CenterX, f2.CenterY, b.CenterX, b.CenterY) < 96) continue;
                if (s1 + ab < best) { best = s1 + ab; f3 = a; f4 = b; }
            }
            if (f3 == null) Log("no flag pair for case B beyond flag 2 (B is then not run)");
            Log($"layout: start ({sx},{sy}) px beside the {home.Type} #{home.Id}; flag 1 #{f1.Id} tile ({f1.LocX >> 4},{f1.LocY >> 4}) {D(qx, qy, f1.CenterX, f1.CenterY):0} px from the start, flag 2 #{f2.Id} tile ({f2.LocX >> 4},{f2.LocY >> 4}) {D(f1, f2):0} px beyond it " +
                $"(flag 1 {LineDist(f1, qx, qy, f2.CenterX, f2.CenterY):0} px off the straight way to flag 2)" +
                (f3 != null ? $"; flag 3 #{f3.Id} tile ({f3.LocX >> 4},{f3.LocY >> 4}) {D(f2, f3):0} px from flag 2, flag 4 #{f4.Id} tile ({f4.LocX >> 4},{f4.LocY >> 4}) {D(f3, f4):0} px beyond it" : ""));
            return true;
        }

        void Spawn(int sx, int sy)
        {
            int tx = sx >> 4, ty = sy >> 4;
            OzCheatSpawner.FreeTile(W, tx - 2, ty - 2, 1, true, out int gx, out int gy);
            var lead = W.CreateRobotGroup("grunt", gx * 16, gy * 16, me, 4);
            if (lead != null) { g.SpawnUnitView(lead); foreach (var m in lead.Minions) g.SpawnUnitView(m); sel.Add(lead); }
            foreach (var (dx, dy, type) in new[] { (2, -2, "jeep"), (2, 1, "jeep"), (-1, 2, "light") })
            {
                OzCheatSpawner.FreeTile(W, tx + dx, ty + dy, 2, false, out int vx, out int vy);
                var v = W.CreateObject("vehicle", type, vx * 16, vy * 16, me);
                if (v != null) { g.SpawnUnitView(v); sel.Add(v); }
            }
            Log($"selection: {string.Join(", ", sel.Select(u => u.Type + " #" + u.Id + (u.Minions.Count > 0 ? " (+" + u.Minions.Count + ")" : "")))}");
        }

        void Select() { input.Selection.Clear(); input.Selection.AddRange(sel.Where(Alive)); }

        // ------------------------------------------------------------------ the walks: A and B
        /// <summary>Watches the walk until flag b is ours: when each flag turned ours, whether every unit came to its own place at flag a (or the
        /// flag) before flag b was taken; pictures when the flags are taken.</summary>
        IEnumerator Walk(string tag, ZObject a, ZObject b, Dictionary<ZObject, (int x, int y)> spotA, System.Action<double, double, int> done)
        {
            double ta = a.Owner == me ? 0 : -1, tb = -1, t0 = W.Time; float t = 0f; bool shotA = false;
            var reached = new HashSet<ZObject>();
            while (t < WalkMax)
            {
                yield return null; t += Time.deltaTime;
                if (ta < 0 && a.Owner == me) ta = W.Time - t0;
                if (tb < 0 && b.Owner == me) tb = W.Time - t0;
                if (tb < 0)
                    foreach (var u in sel)
                        if (Alive(u) && (D(u.CenterX, u.CenterY, spotA[u].x, spotA[u].y) <= SpotPx || D(u, a) <= NearPx)) reached.Add(u);
                if (ta >= 0 && !shotA) { shotA = true; Cam(Mid(Wp(a.CenterX, a.CenterY), GroupAt()), CamHeight); yield return Shot($"k9_{tag}4_flag_{(tag == "A" ? 1 : 3)}_taken_on_to_the_next_70m"); }
                if (tb >= 0) { Cam(Mid(Wp(b.CenterX, b.CenterY), GroupAt()), CamHeight); yield return Shot($"k9_{tag}5_flag_{(tag == "A" ? 2 : 4)}_taken_70m"); break; }
            }
            done(ta, tb, reached.Count);
        }

        IEnumerator CaseA()
        {
            Select(); keepShown = true;
            Cam(Mid(GroupAt(), Wp(f1.CenterX, f1.CenterY)), CamHeight);
            Order(f1, false);                                                                                 // the plain click on flag 1
            Check($"A0 the plain click on flag 1: every unit's list [flag1] ({Lists()})", ListIs("flag1"));
            yield return Wait(1.0f);
            yield return Shot("k9_A1_plain_order_to_flag_1_70m");
            yield return Wait(1.5f);
            // each unit's flag 1 waypoint as it is now (a group order's own spot; an attack-move may have put a target of its own in front)
            var before = sel.ToDictionary(u => u, u => u.Waypoints.FirstOrDefault(q => q.refId == f1.Id && q.playerGiven));
            var spot = sel.ToDictionary(u => u, u => before[u].playerGiven && before[u].refId == f1.Id ? (x: before[u].x, y: before[u].y) : (x: f1.CenterX, y: f1.CenterY));
            int onWay = sel.Count(u => D(u.CenterX, u.CenterY, spot[u].x, spot[u].y) > SpotPx);
            Check($"A1 at the Shift click every unit is still on its way to flag 1 ({onWay} of {sel.Count} more than {SpotPx} px from their places there)", onWay == sel.Count);
            // the Shift preview: the queue drawn while Shift is held, not sent
            Show(f2);
            int fromA = 0;
            foreach (var u in sel) if (OzShiftAppend.PreviewFrom(u, out int px, out int py) && px == spot[u].x && py == spot[u].y) fromA++;
            Cam(Mid(GroupAt(), Wp(f1.CenterX, f1.CenterY), Wp(f2.CenterX, f2.CenterY)), CamHeight);
            yield return Shot("k9_A2_shift_preview_flag_1_then_flag_2_70m");
            shown = null; yield return Frames(2);
            Check($"A2 the Shift preview's line goes on from each unit's place at flag 1 (the end of its order): {fromA} of {sel.Count}; the drawn queue was not sent (lists still {Lists()})", fromA == sel.Count && ListIs("flag1"));
            // the Shift click on flag 2, then Shift up: the queue is sent
            int app0 = OzShiftAppend.Appended;
            Order(f2, true);
            bool same = sel.All(u => u.Waypoints.Count == 2 && before[u].playerGiven && before[u].refId == f1.Id && u.Waypoints[0].Equals(before[u]));
            Check($"A3 the Shift click on flag 2 sent [flag1, flag2] to every unit, its flag 1 waypoint the same as before the click (no new start, no point doubled): {Lists()}; same first waypoint {same}; queues put in front {OzShiftAppend.Appended - app0}",
                  ListIs("flag1", "flag2") && same && OzShiftAppend.Appended - app0 == sel.Count);
            yield return Wait(0.6f);
            yield return Shot("k9_A3_sent_route_flag_1_then_flag_2_70m");
            double ta = -1, tb = -1; int reached = 0;
            yield return Walk("A", f1, f2, spot, (x, y, n) => { ta = x; tb = y; reached = n; });
            Check($"A4 the units took flag 1 ({F(ta)} after the Shift click), then flag 2 ({F(tb)}); every unit came to its place at flag 1 before flag 2 was taken: {reached} of {sel.Count}",
                  ta >= 0 && tb >= 0 && ta < tb && reached == sel.Count);
            keepShown = false;
        }

        IEnumerator CaseB()
        {
            if (f3 == null) { Check("B no flag pair for case B on this level (level 2 has one)", false); yield break; }
            yield return WaitFor(() => sel.All(u => !Alive(u) || u.Waypoints.Count == 0), 20f);         // the last ones arrive at flag 2
            int walking = sel.Count(u => Alive(u) && u.Waypoints.Count > 0);
            if (walking > 0) { Log($"   B: {walking} unit(s) still on their way at flag 2 after 20 s: STOP (the player's own key) so all stand"); foreach (var u in sel) W.SendStop(me, u); }
            yield return Wait(1.0f);
            Select(); keepShown = true;
            Show(f3, f4);
            int fromUnit = sel.Count(u => !OzShiftAppend.PreviewFrom(u, out _, out _));
            Cam(Mid(GroupAt(), Wp(f3.CenterX, f3.CenterY), Wp(f4.CenterX, f4.CenterY)), CamHeight);
            yield return Shot("k9_B1_shift_preview_flag_3_then_flag_4_70m");
            shown = null; yield return Frames(2);
            Check($"B0 standing (no order left), the Shift preview's line starts at each unit: {fromUnit} of {sel.Count}", fromUnit == sel.Count);
            Click(f3, true); Click(f4, true); Send();                                                          // two Shift clicks, then Shift up
            Check($"B1 two Shift clicks from standing sent [flag3, flag4] (nothing put in front): {Lists()}", ListIs("flag3", "flag4"));
            var spot = sel.ToDictionary(u => u, u => (x: f3.CenterX, y: f3.CenterY));
            double ta = -1, tb = -1; int reached = 0;
            yield return Walk("B", f3, f4, spot, (x, y, n) => { ta = x; tb = y; reached = n; });
            Check($"B2 the units took flag 3 ({F(ta)}), then flag 4 ({F(tb)}); every unit came to flag 3 before flag 4 was taken: {reached} of {sel.Count}", ta >= 0 && tb >= 0 && ta < tb && reached == sel.Count);
            keepShown = false;
        }

        // ------------------------------------------------------------------ C to G: the lists
        IEnumerator Lists2()
        {
            Select();
            // C: two plain clicks
            Order(f1, false); yield return Wait(0.5f); Order(f2, false);
            Check($"C two plain clicks: the second replaces the first, as before: {Lists()}", ListIs("flag2"));
            // D: the rule off = the cause
            ZWorld.ShiftAppendDeviation = false;
            Order(f1, false); yield return Wait(0.5f); Order(f2, true);
            Check($"D the rule off (Zod, the cause): a plain click on flag 1, then a Shift click on flag 2 sends flag 2 only: {Lists()}", ListIs("flag2"));
            ZWorld.ShiftAppendDeviation = true;
            // E: movement-layer moves in front at the Shift click
            Order(f1, false); yield return Wait(0.5f);
            var a = sel[0]; a.Waypoints.Insert(0, ZWaypoint.Move(a.CenterX + 16, a.CenterY, false, ZPathing.RefStepAside));
            var b = sel[1]; var bo = ZWaypoint.Move(b.CenterX - 16, b.CenterY, false, ZPathing.RefBackOff); bo.playerGiven = true; b.Waypoints.Insert(0, bo);
            Order(f2, true);
            Check($"E a step aside (squad) and a back-off with the player's mark (jeep) in front at the Shift click are not copied: {Lists()}", ListIs("flag1", "flag2"));
            // F: a first move that cannot be overwritten
            Order(f1, false); yield return Wait(0.5f);
            var j = sel[1]; j.Waypoints.Insert(0, new ZWaypoint { mode = WpMode.ForceMove, refId = -1, x = j.CenterX + 24, y = j.CenterY - 24, playerGiven = true });
            Order(f2, true);
            var jt = j.Waypoints.Select(Tok).ToList();
            Check($"F a first move that cannot be overwritten (jeep) stays once, not doubled: jeep {List(j)}", jt.SequenceEqual(new[] { "forced", "flag1", "flag2" }));
            // G: a patrol (the jeep's forced move of F first ends by itself: SEND_WAYPOINTS keeps it until then)
            yield return WaitFor(() => j.Waypoints.All(q => q.mode != WpMode.ForceMove), 6f);
            foreach (var u in sel) W.SendPatrol(me, u, u.CenterX, u.CenterY + 96);
            yield return Wait(0.5f);
            Order(f2, true);
            Check($"G a patrol is replaced by the Shift queue, as before (a patrol never ends): {Lists()}; still patrolling {sel.Count(u => u.Patrolling)}", ListIs("flag2") && sel.All(u => !u.Patrolling));
            foreach (var u in sel) W.SendStop(me, u);
        }

        // ------------------------------------------------------------------ H: a Shift click on the minimap, the real Shift key
        /// <summary>H: a plain click on flag 1; the Shift key held through the keyboard's own state; the minimap's right-click order at flag 2
        /// (OpenZMinimap.Order, its own path, by reflection); Shift let go: PlayerInput's Update sends the queue (not this proof).</summary>
        IEnumerator CaseH()
        {
            var mm = FindFirstObjectByType<OpenZMinimap>();
            var order = typeof(OpenZMinimap).GetMethod("Order", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                                                       new[] { typeof(Vector2Int), typeof(Keyboard), typeof(Vector2) }, null);
            if (!mm || order == null || Keyboard.current == null) { Check("H the minimap's order path (OpenZMinimap.Order) and a keyboard found", false); yield break; }
            Select();
            Order(f1, false); yield return Wait(0.5f);                                                         // the plain click on flag 1 (the map's path)
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.LeftShift)); yield return Frames(2);   // Shift held
            string threw = null;
            try { order.Invoke(mm, new object[] { new Vector2Int(f2.CenterX, f2.CenterY), Keyboard.current, Vector2.zero }); }   // the minimap's right-click at flag 2
            catch (System.Exception ex) { var e = ex.InnerException ?? ex; threw = e.GetType().Name + ": " + e.Message; }
            if (threw != null)
            {   // never leave the Shift key held (PlayerInput would keep the queue for good)
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                Check("H0 the minimap's order (OpenZMinimap.Order) threw " + threw, false); yield break;
            }
            yield return Frames(3);
            int queued = sel.Count(u => pending.TryGetValue(u, out var l) && l.Count == 1);
            bool held = Keyboard.current.shiftKey.isPressed;
            if (!held) Log("   H: the Shift key event did not reach the game (the Game view without focus?): click the Game view's title tab, then run the proof again");
            Check($"H0 Shift held: the minimap's Shift click is queued, not sent (Shift held {held}; queued {queued} of {sel.Count}; lists {Lists()})", held && queued == sel.Count && ListIs("flag1"));
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); yield return Frames(3);               // Shift let go: PlayerInput sends the queue itself
            Check($"H1 Shift up: PlayerInput sent the queue and the minimap's point went after the order to flag 1: {Lists()} (queue left {pending.Count})", pending.Count == 0 && ListIs("flag1", "point"));
            foreach (var u in sel) W.SendStop(me, u);
        }

        IEnumerator All()
        {
            Application.targetFrameRate = 60;
            if (W == null || input == null) { Log("no game (a level in Play)"); fails++; Finish(); yield break; }
            if (pending == null || flush == null || setSelectTime == null) { Log("PlayerInput's queue, Flush or SelectTime not found (PlayerInput.pending / Flush(bool, Vector2) / SelectTime)"); fails++; Finish(); yield break; }
            me = g.player;
            Log($"1.2 item 9 Shift adds proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, world time {W.Time:0.0} s");
            Check("E0 the rule's switch is on (ZWorld.ShiftAppendDeviation)", ZWorld.ShiftAppendDeviation);
            int bots = W.Bots.Count; W.Bots.Clear();
            OzProofHoldFire.Attach(gameObject, W, null, me, false, true, true, Log);
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            int held = 0;                                                                                     // the player's other units hold (no flag taken by them on their own during the walks)
            foreach (var o in W.Objects.ToList())
                if (o.Owner == me && o.IsMobile && !o.IsMinion && !o.IsShuttle && Alive(o) && W.SendHold(me, o)) held++;
            Log($"set-up: CPU removed {bots}, day 0.10 held, the player's other units hold position ({held})");
            if (!Layout(out int sx, out int sy)) { Check("A0 a flag layout on this level", false); Finish(); yield break; }
            Spawn(sx, sy);
            if (sel.Count < 4) { Check("A0 the selection was made (a squad, two jeeps, a light tank)", false); Finish(); yield break; }
            yield return Wait(1.0f);
            yield return CaseA();
            yield return CaseB();
            yield return Lists2();
            yield return CaseH();
            Finish();
        }

        void Finish()
        {
            ZWorld.ShiftAppendDeviation = true; shown = null; keepShown = false;
            Log($"item 9 result: {passes} PASS, {fails} FAIL");
            Report = fails == 0 ? $"PASS ({passes})" : $"FAIL ({fails} of {passes + fails})";
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "shift_append_proof.txt"), sb.ToString()); }
            catch (System.Exception ex) { Debug.LogWarning("[OzShiftAppendProof] " + ex.Message); }
            StopAllCoroutines();
            Destroy(gameObject);
        }
    }
}
