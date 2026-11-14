using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 2f Play proof (View/PlayerInput.ExitPick.cs: robots walking out of the shuttle are picked at their walking figures), level 16
    /// in Play (menu OpenZ/V1.2/Shuttle: 2f Walk-out Select Proof, Editor/OzExitPickMenu.cs). Set-up as K431's proof: the CPU teams removed,
    /// morning (day clock 0.10, held). Stages G (a grunt squad of 4) and H (ALAN &amp; BRAD, when on the level): the squad boards the level's
    /// shuttle, the ship flies to a free place, EXIT on the touchdown frame (the game's own PlayerInput.EjectUnit). While they walk out, with
    /// the game's own pick functions (PlayerInput.ProofClick = CollectClick as on a left release; PlayerInput.BoxFromHeldPress = the box):
    ///  a. each robot on the lower ramp (its figure past the hull, in sight from the RTS camera) and, when its walk on the ground is long
    ///     enough, on the ground walking on to its spot: a click on its figure, once with the switch off (as before: the cause) and 10 times
    ///     with it on (its squad every time, never the ship);
    ///  b. a box round the walking figures: the squad with the switch on (and what it took with the switch off);
    ///  c. then a move order to the squad (PlayerInput.OrderFor + ZWorld.SendWaypoints, as Flush sends it): taken at once; each robot still
    ///     getting out stays on its spot until rule DV132's out moment, keeps the order and then carries it out from there. Stage K (the G
    ///     squad once more) is the control: the same order given once every robot is out, for the leader's start time to compare with.
    /// Counted PASS / FAIL lines and pictures (the squad selected during the walk, normal RTS camera) in <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzExitPickProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "work", "staging")))
                    return System.IO.Path.Combine(d.FullName, "work", "staging", "v1_2_20261022", "shuttle_occupants");
            return System.IO.Path.Combine(Application.persistentDataPath, "shuttle_occupants");
        }
        public static void Run() { Report = "running"; new GameObject("OzExitPickProof").AddComponent<OzExitPickProof>(); }

        ZGame g; ZWorld W; PlayerInput input; RtsCamera rts; BoardingView bv; Team me = Team.Red;
        ZObject s; OzPlayableShuttle ship;
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;
        readonly List<int> ids = new List<int>();

        sealed class St
        {
            public string tag; public int size, clicks, oldMiss, oldSel, oldShip, newHit, newSel, newShip, shipUnder, rampClicks, groundClicks;
            public float minGap = 999f, maxGap, footR;
            public bool boxDone, boxOld, boxNew, boxShip; public int boxRobots, boxOldOver;
            public bool orderDone; public int orderHeads, orderTaken, tx, ty; public double orderAt = -1;
            public readonly HashSet<int> seen = new HashSet<int>(), clickedRamp = new HashSet<int>(), clickedGround = new HashSet<int>(), movedHeld = new HashSet<int>(), started = new HashSet<int>(), kept = new HashSet<int>(), arrived = new HashSet<int>();
            public readonly Dictionary<int, double> heldX = new Dictionary<int, double>(), heldY = new Dictionary<int, double>(), outAt = new Dictionary<int, double>(), lag = new Dictionary<int, double>(), arrivedAt = new Dictionary<int, double>();
            public double startLagMax, traceNext = -1; public bool control;
            public readonly StringBuilder trace = new StringBuilder();
            public readonly List<ZObject> heads = new List<ZObject>();
        }

        void Start()
        {
            g = ZGame.I; W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>(); rts = FindFirstObjectByType<RtsCamera>(); bv = BoardingView.I;
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            StartCoroutine(All());
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzExitPickProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        IEnumerator WaitFor(System.Func<bool> c, float max) { float t = 0f; while (t < max && !c()) { t += Time.deltaTime; yield return null; } }
        IEnumerator Wait(float s0) { float t = 0f; while (t < s0) { t += Time.deltaTime; yield return null; } }
        void Cam(Vector3 focus, float h) { if (!rts) return; rts.FreeLookOff(); rts.focus = new Vector3(focus.x, 0f, focus.z); rts.SetHeightImmediate(h); }
        Camera C => rts ? rts.GetComponent<Camera>() : Camera.main;
        void Select(params ZObject[] os) { input.Selection.Clear(); foreach (var o in os) if (o != null) input.Selection.Add(o); }
        void Shot(string name) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); Log("   picture " + name + ".png"); }
        UnitView V(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) ? v : null;
        static bool Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;
        static ZObject Head(ZObject o) => o.Leader != null && Alive(o.Leader) && o.Leader.Owner == o.Owner ? o.Leader : o;
        static string F(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        /// <summary>A free tile middle (the ship can land there now) between minR and maxR px from (cx, cy), the nearest first (K431's proof).</summary>
        int[] Free(int cx, int cy, int minR, int maxR)
        {
            long best = long.MaxValue; int[] r = null; int e = ZWorld.ShuttleEdgeTiles;
            for (int ty = e; ty < W.Map.height - e; ty++)
                for (int tx = e; tx < W.Map.width - e; tx++)
                {
                    int x = tx * 16 + 8, y = ty * 16 + 8; long d = (long)(x - cx) * (x - cx) + (long)(y - cy) * (y - cy);
                    if (d >= best || d < (long)minR * minR || d > (long)maxR * maxR) continue;
                    if (W.ShuttleNeverLands(x, y, s, out _) || !W.ShuttlePadFree(x, y, s, out _)) continue;
                    best = d; r = new[] { x, y };
                }
            return r;
        }

        /// <summary>a: a click on the walking figure, the switch off once (as before), then on 10 times.</summary>
        void ClickTest(St st, ZObject o, Vector3 fig, float gap, float r, bool ramp)
        {
            if (ramp) { st.clickedRamp.Add(o.Id); st.rampClicks++; } else { st.clickedGround.Add(o.Id); st.groundClicks++; }
            st.clicks++;
            st.minGap = Mathf.Min(st.minGap, gap); st.maxGap = Mathf.Max(st.maxGap, gap);
            var head = Head(o);
            Vector2 mp = C.WorldToScreenPoint(fig + Vector3.up * 0.9f * OzHeroSquadView.Tall(o));
            bool shipUnder = input.ScreenRect(s).Contains(mp); if (shipUnder) st.shipUnder++;
            PlayerInput.ExitPickOn = false;
            bool oldHit = input.ScreenRect(o).Contains(mp);
            input.ProofClick(mp);
            bool oldSel = input.Selection.Contains(head), oldShip = input.Selection.Contains(s);
            PlayerInput.ExitPickOn = true;
            if (!oldHit) st.oldMiss++; if (oldSel) st.oldSel++; if (oldShip) st.oldShip++;
            bool newHit = input.ScreenRect(o).Contains(mp); if (newHit) st.newHit++;
            int sel = 0, shipN = 0;
            for (int i = 0; i < 10; i++) { input.ProofClick(mp); if (input.Selection.Contains(head)) sel++; if (input.Selection.Contains(s)) shipN++; }
            st.newSel += sel; st.newShip += shipN;
            Log($"   {st.tag} click on {o.Type} #{o.Id}{(o.IsHero ? " (" + o.HeroMemberName + ")" : "")} walking out {(ramp ? "ON THE RAMP" : "ON THE GROUND")} ({F(r)} m from the ship's middle, the ramp foot {F(st.footR)} m), its figure {F(gap)} m from its spot, at screen ({mp.x:0},{mp.y:0}), the ship's frame under the pointer {shipUnder}: " +
                $"as before (switch off) its pick box {(oldHit ? "HIT" : "missed")}, the click selected its squad {oldSel}, the ship {oldShip}; now its pick box {(newHit ? "HIT" : "missed")}, its squad selected {sel} of 10 clicks, the ship {shipN}");
        }

        /// <summary>b: a box round the walking figures (their pick boxes now, 6 px more), the switch off once, then on.</summary>
        void BoxTest(St st, List<ZObject> away)
        {
            st.boxDone = true; st.boxRobots = away.Count;
            Rect box = default; bool first = true;
            foreach (var o in away) { var r = input.ScreenRect(o); box = first ? r : Rect.MinMaxRect(Mathf.Min(box.xMin, r.xMin), Mathf.Min(box.yMin, r.yMin), Mathf.Max(box.xMax, r.xMax), Mathf.Max(box.yMax, r.yMax)); first = false; }
            box = Rect.MinMaxRect(box.xMin - 6f, box.yMin - 6f, box.xMax + 6f, box.yMax + 6f);
            var heads = away.Select(Head).Distinct().ToList();
            PlayerInput.ExitPickOn = false;
            foreach (var o in away) if (input.ScreenRect(o).Overlaps(box)) st.boxOldOver++;
            input.BoxFromHeldPress(box.min, box.max, false, true);
            st.boxOld = heads.All(h => input.Selection.Contains(h));
            int oldCount = input.Selection.Count;
            PlayerInput.ExitPickOn = true;
            input.BoxFromHeldPress(box.min, box.max, false, true);
            st.boxNew = heads.All(h => input.Selection.Contains(h)); st.boxShip = input.Selection.Contains(s);
            Log($"   {st.tag} box ({box.xMin:0},{box.yMin:0})-({box.xMax:0},{box.yMax:0}) round {away.Count} walking figure(s) ({string.Join(", ", away.Select(o => "#" + o.Id))}): as before (switch off) " +
                $"{st.boxOldOver} of their old pick boxes in it, the squad taken {st.boxOld} ({oldCount} unit(s)); now the squad taken {st.boxNew} ({input.Selection.Count} unit(s): {string.Join(", ", input.Selection.Select(o => o.Type + " #" + o.Id))}), the ship too {st.boxShip}");
        }

        /// <summary>c: a move order to the squad getting out (as Flush sends it): mid-walk (G, H), or the control K right after the walks.</summary>
        void OrderTest(St st)
        {
            st.orderDone = true; st.orderAt = W.Time;
            var (bx, by) = s.ShuttleBackPoint(6);
            double dx = bx - s.CenterX, dy = by - s.CenterY, d = System.Math.Sqrt(dx * dx + dy * dy); if (d < 1) { dx = 1; dy = 0; d = 1; }
            int tx = (int)(s.CenterX + dx / d * 200) >> 4, ty = (int)(s.CenterY + dy / d * 200) >> 4;
            OzCheatSpawner.FreeTile(W, tx, ty, 1, true, out tx, out ty);
            st.tx = tx * 16 + 8; st.ty = ty * 16 + 8;
            st.heads.Clear();
            foreach (var id in st.seen) { var o = W.GetAny(id); if (!Alive(o)) continue; var h = Head(o); if (!st.heads.Contains(h)) st.heads.Add(h); }   // this squad (the box may have taken other units too)
            int heldN = 0;
            foreach (var h in st.heads)
            {
                st.orderHeads++;
                var wp = input.OrderFor(h, null, st.tx, st.ty);
                bool ok = W.SendWaypoints(me, h, new[] { wp });
                if (ok && h.Waypoints.Count > 0 && h.Waypoints[h.Waypoints.Count - 1].x == st.tx && h.Waypoints[h.Waypoints.Count - 1].y == st.ty) st.orderTaken++;
                foreach (var m in new[] { h }.Concat(h.Minions))
                    if (Alive(m) && (st.control || m.ExitWalking(W.Time))) { st.heldX[m.Id] = m.PosX; st.heldY[m.Id] = m.PosY; st.outAt[m.Id] = st.control ? W.Time : m.ExitOutAt; heldN++; }
            }
            Log($"   {st.tag} order {(st.control ? "right after the walks (control)" : "mid-walk")} at world time {F(W.Time)} s: move to ({st.tx},{st.ty}), {F(System.Math.Sqrt((st.tx - s.CenterX) * (double)(st.tx - s.CenterX) + (st.ty - s.CenterY) * (double)(st.ty - s.CenterY)) / 8.0)} m from the ship's middle, for {st.orderHeads} squad(s), taken {st.orderTaken}; " +
                (st.control ? $"{heldN} robot(s) out" : $"{heldN} robot(s) still getting out then (out at {string.Join(", ", st.outAt.Select(kv => "#" + kv.Key + " " + F(kv.Value)))})"));
        }

        /// <summary>c, every frame after the order: the robots getting out stand on their spots until their out moment, keep the order, then
        /// carry it out (started: off its spot towards the order's place; arrived: within 6 tiles of it). The leader's trace every 0.5 s.</summary>
        void OrderWatch(St st)
        {
            if (!st.orderDone) return;
            double tgx = st.tx, tgy = st.ty;
            foreach (var kv in st.outAt)
            {
                var o = W.GetAny(kv.Key); if (!Alive(o)) continue;
                double x0 = st.heldX[kv.Key], y0 = st.heldY[kv.Key];
                if (W.Time < kv.Value - 1e-6) { if (o.PosX != x0 || o.PosY != y0) st.movedHeld.Add(kv.Key); continue; }
                if (st.heads.Count > 0 && o == st.heads[0] && W.Time >= st.traceNext && W.Time <= kv.Value + 8.0)
                {
                    if (st.traceNext < 0) st.traceNext = kv.Value;
                    st.trace.Append($" +{F(W.Time - kv.Value)}s ({o.PosX:0},{o.PosY:0}) wps {o.Waypoints.Count}{(o.IsMoving ? " moving" : "")};");
                    st.traceNext += 0.5;
                }
                double d1 = System.Math.Sqrt((o.CenterX - tgx) * (o.CenterX - tgx) + (o.CenterY - tgy) * (o.CenterY - tgy));
                if (d1 <= 96 && !st.arrivedAt.ContainsKey(kv.Key)) { st.arrived.Add(kv.Key); st.arrivedAt[kv.Key] = W.Time - kv.Value; }
                if (st.started.Contains(kv.Key)) continue;
                if (o.Waypoints.Count > 0 || (o.Leader != null && o.Leader.Waypoints.Count > 0)) st.kept.Add(kv.Key);
                if (System.Math.Abs(o.PosX - x0) + System.Math.Abs(o.PosY - y0) >= 8) { st.started.Add(kv.Key); st.lag[kv.Key] = W.Time - kv.Value; st.startLagMax = System.Math.Max(st.startLagMax, W.Time - kv.Value); }   // 1 m off its spot (its way may first lead round something)
            }
        }

        IEnumerator Watch(St st, float maxSec)
        {
            float t = 0f, quiet = 0f, firstClickAt = -1f; bool any = false; int shotFrame = -1, shots = 0; string shotName = null;
            var away = new List<ZObject>();
            var mid = ZGame.PxToWorld(s.CenterX, s.CenterY);
            while (t < maxSec)
            {
                yield return new WaitForEndOfFrame();
                t += Time.deltaTime;
                if (shotName != null && Time.frameCount >= shotFrame) { Shot(shotName); shotName = null; }
                OrderWatch(st);
                bv.ExitFigureIds(ids);
                if (ids.Count == 0)
                {
                    if (any)
                    {
                        if (st.control && !st.orderDone) OrderTest(st);                                           // the control: the same order once every robot is out
                        quiet += Time.deltaTime;
                        bool done = st.orderDone && st.outAt.Keys.All(k => st.arrived.Contains(k) || !Alive(W.GetAny(k)));
                        if (shotName == null && ((quiet >= 1f && done) || quiet >= 30f)) break;
                    }
                    continue;
                }
                any = true; quiet = 0f; away.Clear();
                foreach (var id in ids)
                {
                    var o = W.GetAny(id); if (!Alive(o)) continue;
                    st.seen.Add(id);
                    if (BoardingView.ExitFigureState(o, out var fig) != 2) continue;
                    var v = V(o); float gap = v ? Flat(fig, v.transform.position) : 0f, r = Flat(fig, mid);
                    bool onRamp = r >= st.footR - 1f && r < st.footR - 0.2f, onGround = r >= st.footR + 0.5f && gap >= 2f;   // the ramp's lower half (past the hull); on the ground still 2 m or more from its spot (off its old pick box)
                    if ((onRamp || r >= st.footR + 0.5f) && gap >= 0.6f) away.Add(o);
                    if (shotName != null) continue;
                    bool ramp;
                    if (onRamp && !st.clickedRamp.Contains(id)) ramp = true;
                    else if (onGround && !st.clickedGround.Contains(id)) ramp = false;
                    else continue;
                    ClickTest(st, o, fig, gap, r, ramp);
                    if (firstClickAt < 0f) firstClickAt = t;
                    if ((ramp && shots == 0) || (!ramp && shots < 2))
                    {   // the first click on the ramp and the first on the ground: the squad selected while its robot walks out
                        shots = ramp ? 1 : 2; shotFrame = Time.frameCount + 2;
                        shotName = ramp ? $"walkout_select_{st.tag}1_clicked_while_walking" : $"walkout_select_{st.tag}1b_clicked_on_ground";   // 1 = on the ramp
                    }
                }
                if (!st.boxDone && firstClickAt >= 0f && shotName == null && st.groundClicks > 0 && t >= firstClickAt + 0.35f && (away.Count >= Mathf.Min(2, st.size) || (away.Count >= 1 && t >= firstClickAt + 2f)))
                {
                    BoxTest(st, away);
                    shotFrame = Time.frameCount + 2; shotName = $"walkout_select_{st.tag}2_box_while_walking";
                    if (!st.control) OrderTest(st);
                }
            }
            if (shotName != null) Shot(shotName);
            yield return null;
            foreach (var kv in st.outAt)
                Log($"   {st.tag} after the order #{kv.Key}{(st.heads.Count > 0 && st.heads[0].Id == kv.Key ? " (leader)" : "")}: {(st.control ? "ordered at" : "out at")} {F(kv.Value)} s, moved before it {st.movedHeld.Contains(kv.Key)}, kept the order {st.kept.Contains(kv.Key)}, started {(st.lag.TryGetValue(kv.Key, out var lg) ? "+" + F(lg) + " s" : "NO")}, at the order's place {(st.arrivedAt.TryGetValue(kv.Key, out var ar) ? "+" + F(ar) + " s" : "NO")}");
            Log($"   {st.tag} the leader from {(st.control ? "the order" : "its out moment")}:{st.trace}");
        }

        void Checks(St st)
        {
            Check($"{st.tag}a0 as before (switch off: the cause) the pick box of each walking robot stood on its hidden model's spot: the click on its figure missed it {st.oldMiss} of {st.clicks} times (figures {F(st.minGap)}-{F(st.maxGap)} m from their spots); the click took the ship {st.oldShip} times and the squad {st.oldSel} (only when the pointer happened to lie on a squad mate's empty spot)",
                  st.clicks > 0 && st.oldMiss == st.clicks);
            Check($"{st.tag}a1 now every robot was clicked on the lower ramp ({st.rampClicks} of {st.size}) and {st.groundClicks} on the ground walking on to its spot; the click picked its squad {st.newSel} of {10 * st.clicks} times (pick box on the figure {st.newHit} of {st.clicks}), never the ship ({st.newShip}; the ship's frame was under the pointer in {st.shipUnder} of the clicks)",
                  st.clicks > 0 && st.rampClicks == st.size && st.groundClicks > 0 && st.newHit == st.clicks && st.newSel == 10 * st.clicks && st.newShip == 0);
            Check($"{st.tag}b1 a box round {st.boxRobots} walking figure(s) took their squad (now {st.boxNew}; as before {st.boxOld}, {st.boxOldOver} of their old pick boxes in the box)",
                  st.boxDone && st.boxRobots > 0 && st.boxNew);
            Check($"{st.tag}c1 the move order {(st.control ? "given right after the walks (control)" : "given mid-walk")} was taken at once by {st.orderTaken} of {st.orderHeads} squad(s){(st.control ? "" : " (rule DV132: queued, as for the APC's passengers)")}",
                  st.orderDone && st.orderHeads > 0 && st.orderTaken == st.orderHeads);
            if (!st.control)
                Check($"{st.tag}c2 the {st.outAt.Count} robot(s) still getting out at the order stood on their spots until their out moment ({st.movedHeld.Count} moved before it) and still had the order then ({st.kept.Count})",
                      st.outAt.Count > 0 && st.movedHeld.Count == 0 && st.kept.Count == st.outAt.Count);
        }

        static double LeadLag(St st) => st != null && st.heads.Count > 0 && st.lag.TryGetValue(st.heads[0].Id, out var l) ? l : -1;

        /// <summary>c3: the squad carried the order out from its spots (every robot started and reached the order's place); its leader started
        /// no later than the control's (the same squad, the same order right after the walks) + 1 s, or within 1 s.</summary>
        void CarryCheck(St st, St control)
        {
            double lead = LeadLag(st), ctl = LeadLag(control);
            double limit = System.Math.Max(1.0, (ctl >= 0 ? ctl : 0) + 1.0);
            Check($"{st.tag}c3 then the squad carried it out from there: its leader started +{F(lead)} s after its out moment (the control K, the order given after the walks: +{F(ctl)} s; at most {F(limit)} s), every robot started ({st.started.Count} of {st.outAt.Count}, the last +{F(st.startLagMax)} s) and reached the order's place ({st.arrived.Count} of {st.outAt.Count})",
                  st.outAt.Count > 0 && lead >= 0 && lead <= limit && st.started.Count == st.outAt.Count && st.arrived.Count == st.outAt.Count);
        }

        IEnumerator Fly(int minR, int maxR)
        {
            var q = Free(s.CenterX, s.CenterY, minR, maxR);
            if (q == null) { Check("a landing place", false); yield break; }
            OzPlayableShuttle.ProofRoughNext = false;
            Select(s); W.SendWaypoints(me, s, new[] { new ZWaypoint { mode = WpMode.Move, x = q[0], y = q[1], playerGiven = true } });
            yield return WaitFor(() => s.ShuttleState == ZShuttleState.Descend, 60f);
            Cam(ship.transform.position, 45f);
            yield return WaitFor(() => s.ShuttleState != ZShuttleState.Descend, 30f);
            yield return WaitFor(() => s.ShuttleState == ZShuttleState.Landed, 10f);
        }

        St last;
        IEnumerator Stage(string tag, ZObject lead, float boardMax, bool control = false)
        {
            last = null;
            yield return WaitFor(() => s.ShuttleState == ZShuttleState.Landed && !s.ShuttleAirborne && s.ShuttleLevelled && s.ShuttleRampPhase == ZShuttleRamp.Shut && W.Time >= s.ShuttleRampHoldUntil, 30f);
            Select(lead);
            Cam(Vector3.Lerp(ship.transform.position, V(lead) ? V(lead).transform.position : ship.transform.position, 0.4f), 45f);
            W.SendWaypoints(me, lead, new[] { new ZWaypoint { mode = WpMode.Enter, x = s.CenterX, y = s.CenterY, refId = s.Id, playerGiven = true } });
            yield return WaitFor(() => s.Passengers.Count > 0, boardMax);
            yield return WaitFor(() => s.ShuttleRampPhase == ZShuttleRamp.Shut && W.Time >= s.ShuttleRampHoldUntil, 20f);
            int n = s.Passengers.Count;
            Check($"{tag}0 the squad boarded the shuttle ({n} aboard)", n > 0);
            if (n == 0) yield break;
            yield return Wait(0.5f);
            yield return Fly(320, 700);
            bool taken = input.EjectUnit(s);
            Log($"   {tag}: landed ({(s.ShuttleRoughLanding ? "a crash landing" : "a normal landing")}) at world time {F(W.Time)} s; EXIT taken {taken}");
            Check($"{tag}0 EXIT taken on the touchdown", taken);
            Select();
            Cam(ship.transform.position, 45f);
            var st = new St { tag = tag, size = n, control = control }; last = st;
            yield return WaitFor(() => s.Passengers.Count == 0, 15f);                                      // the rules let them out (the walks begin)
            var (fx, fy) = s.ShuttleBackPoint(0);                                                           // slot 0 = the ramp foot
            st.footR = Flat(ZGame.PxToWorld(fx, fy), ZGame.PxToWorld(s.CenterX, s.CenterY));
            yield return Watch(st, 40f);
            Log($"   {tag}: {st.seen.Count} robot(s) walked out; after the walks {input.Selection.Count} unit(s) selected ({string.Join(", ", input.Selection.Select(o => o.Type + " #" + o.Id))})");
            Checks(st);
        }

        IEnumerator All()
        {
            Application.targetFrameRate = 60;
            if (W == null || input == null || !bv) { Log("no game (level 16 in Play)"); fails++; Finish(); yield break; }
            if (!ZWorld.ShuttlePlayableDeviation || W.PlayableShuttle == null) { Log("no playable shuttle (level 16, the shuttle rule on, the option SHUTTLE on)"); fails++; Finish(); yield break; }
            me = g.player; s = W.PlayableShuttle; ship = OzPlayableShuttle.Of(s);
            if (!ship) { Log("the shuttle has no view"); fails++; Finish(); yield break; }
            Log($"1.2 item 2f walk-out select proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, world time {F(W.Time)} s; switches: ExitPickOn {PlayerInput.ExitPickOn}, K431 ExitRingsFollowOn {BoardingView.ExitRingsFollowOn}, DV132 ExitWalkHoldOn {ZWorld.ExitWalkHoldOn}");
            int bots = W.Bots.Count; W.Bots.Clear();
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            Log($"set-up: CPU removed {bots}, day 0.10 held");
            Check("E0 the 2f switch is on", PlayerInput.ExitPickOn);

            // ---- G: a grunt squad of 4
            yield return WaitFor(() => s.ShuttleState == ZShuttleState.Landed && !s.ShuttleAirborne && s.ShuttleLevelled && s.ShuttleRampPhase == ZShuttleRamp.Shut && W.Time >= s.ShuttleRampHoldUntil, 30f);
            var (bx, by) = s.ShuttleBackPoint(6);
            var lead = W.CreateRobotGroup("grunt", bx - 8, by - 8, me, 4);
            if (lead != null) { g.SpawnUnitView(lead); foreach (var m in lead.Minions) g.SpawnUnitView(m); }
            yield return Wait(0.5f);
            yield return Stage("G", lead, 60f);
            var stG = last;

            // ---- H: ALAN & BRAD
            St stH = null;
            var brad = W.Objects.FirstOrDefault(o => o.IsHero && o.HeroMemberName == "brad" && Alive(o) && o.Owner == me && !o.IsMinion);
            if (brad != null) { yield return Stage("H", brad, 90f); stH = last; }
            else Log("H skipped: no ALAN & BRAD on this level (hero squads off)");

            // ---- K: the control: the G squad again, the same order given once every robot is out
            St stK = null;
            var gLead = stG != null && stG.heads.Count > 0 ? stG.heads[0] : null;                             // the G squad as it came out (new robots)
            if (gLead != null && Alive(gLead)) { yield return Stage("K", gLead, 90f, true); stK = last; }
            else Log("K skipped: the G squad is gone");
            if (stG != null) CarryCheck(stG, stK);
            if (stH != null) CarryCheck(stH, stK);
            if (stK != null) Check($"Kc3 the control: the same squad, the order given right after the walks: its leader started +{F(LeadLag(stK))} s after the order, every robot started ({stK.started.Count} of {stK.outAt.Count}) and reached the order's place ({stK.arrived.Count} of {stK.outAt.Count})",
                                    stK.outAt.Count > 0 && LeadLag(stK) >= 0 && stK.started.Count == stK.outAt.Count && stK.arrived.Count == stK.outAt.Count);
            Finish();
        }

        void Finish()
        {
            OzPlayableShuttle.ProofRoughNext = false; PlayerInput.ExitPickOn = true;
            Log($"2f result: {passes} PASS, {fails} FAIL");
            Report = fails == 0 ? $"PASS ({passes})" : $"FAIL ({fails} of {passes + fails})";
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "walkout_select_proof.txt"), sb.ToString()); }
            catch (System.Exception ex) { Debug.LogWarning("[OzExitPickProof] " + ex.Message); }
            StopAllCoroutines();
            Destroy(gameObject);
        }
    }
}
