using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted rows of the rules' part, headless on the Sim alone, nothing saved:
    /// menu OpenZ/V1.4/Vehicle Exit Jump: Stop Count (headless), the DV171 row's probe (Quick, Editor/OpenZParity.V14VehicleExit.cs), and
    /// off-project on the editor's Mono (the kit's tools/harness). The counted checks' game switches (OzV11Kit.GameSwitches: DV79 on, as at every
    /// start) with DV171 on, the DV69 calm start off and, as ZGame sets them at every start that is not 'Zod exact' (fix round 1), DV146 (the
    /// mech's hand gun), DV149 (its loss) and DV87 (a hatch driver fires his own weapon) ON; a plain 64 x 40-tile map, red the player, blue a CPU
    /// that gives no orders of its own; the enemies the vehicles fire at are dummies (they fire nothing, never move, never die).
    /// Every row needs its samples (a row with nothing measured FAILS):
    ///  B1  each of the four (light, medium, heavy tank, mech), its grunt driver: an attack-move past enemy dummies close to its way; once it has
    ///      fired and is driving (the mech: right after a tick in which its HAND GUN fired, so inside a reload window with the arm on a robot):
    ///      EXIT DRIVER. From that tick on: its shots (every weapon: the gun's Hitscan and MissileFired, the heavy's second barrel, the mech's
    ///      hand gun, a hatch driver's weapon, a bolt) = 0 for 10 s while the enemies stay in range; its move after the stop tick (px, the bar:
    ///      0.01 px, the rules have no brake); its facing (FaceX / FaceY) and the mech's hand-gun arm heading unchanged (the arm must be on a
    ///      robot at the stop: not NaN); no target and no order at the end of the EXIT's tick and at every tick after; neutral and empty.
    ///  B1p the three tanks with a PSYCHO driver (DV87: his own machine gun from the hatch): the same, the EXIT right after a tick with his
    ///      DriverShot (his weapon must have fired before).
    ///  B2  each of the four, its grunt driver hit by REAL enemy snipers (three squads of three; one squad at the light tank, whose hull would
    ///      die first) on CPU attack orders: first it attack-moves and fires (the mech: its hand gun too), then it DRIVES to and fro (a plain
    ///      move, given again at each end) across the snipers' line; the tick his driver is sniped must find it DRIVING (moving with a waypoint
    ///      at the start of that tick; the seeds 3 .. 14 are tried in order until one snipe lands mid-drive). Fix round 2 (review: the Sniped
    ///      event comes after every object's step): the vehicle is moved to the end of the world's object list (the server loop's order), so
    ///      it runs after the snipers in every tick and in the snipe's tick its whole step comes after the sniper's hit; that tick's events are split
    ///      at the sniper's own hit (the first Hitscan with its snipe flag on the vehicle): a shot of the vehicle after it counts as after; its
    ///      move, facing and arm are counted from the START of the snipe's tick: the same counts from there, for 10 s or until a robot climbs
    ///      into the emptied hull (the normal rule once nobody is inside; at least 1 s counted).
    ///  B2p the three tanks with a PSYCHO driver: the same (his weapon must have fired before the snipe).
    ///      Fix round 3 (review: the help call on an empty hull was never run): in B1 an enemy beside the emptied hull keeps shooting it (it
    ///      must lose health): its target and order ticks stay 0 all the same.
    ///  B3  (fix round 3, review: StunHold, Sim/ZObject.Electro.cs:194-216, fires BEFORE UnmannedHold) each of the four: an enemy Electrobot
    ///      stuns it while it drives and fires; once it has fired while stunned, EXIT DRIVER right after such a shot: the same counts.
    ///  B3s each of the four: the same stun, then its driver sniped by real enemy snipers while it is stunned (the seeds in turn as B2, the
    ///      vehicle after the snipers, the snipe's tick split at the sniper's own hit): the same counts from the snipe's tick.
    ///  H   each of the four: the driver let out is held as getting out for exactly ZObject.DriverOutHoldTicks (held at +hold-1, free at +hold)
    ///      and the hold is 1.0-1.5 s.
    ///  H5  (the Editor's menu only; fix round 3: not in DV171's quick probe, an INFO line there) each of the four: the Play check's MEASURED
    ///      touch (its last full report's J1 row, View/OzVehicleExitCheck.cs) within 0.1 s of DriverOutHoldTicks: no report or no J1 row for a
    ///      type = NOT MEASURED, which FAILS.
    /// </summary>
    public static class OzV14VehicleExitStopCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red, Cpu = Team.Blue;
        public static readonly string[] Types = { "light", ZMTank.Type, "heavy", ZMTank.Mech };
        static readonly string[] Tanks = { "light", ZMTank.Type, "heavy" };
        static string Name(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech" : t;
        /// <summary>The bar for the move after the stop tick (px): the rules have no brake, the vehicle's position does not change.</summary>
        public const double MoveBarPx = 0.01;
        /// <summary>The hold's limits (s): the spec's 1.0-1.5 s from the click to the touch; H5: the measured touch against the hold (s).</summary>
        public const double HoldMinSec = 1.0, HoldMaxSec = 1.5, TouchVsHoldSec = 0.1;
        /// <summary>B2: the seeds tried in order until the snipe lands while the vehicle drives.</summary>
        static readonly int[] SnipeSeeds = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };

        sealed class Scene { public ZWorld w; public int ox, oy; }
        const int GroundW = 64, GroundH = 40;
        static string groundMap;
        static Scene New(string set, int seed = 1)
        {
            var w = ZWorld.Create(set, groundMap ??= OzV11Kit.Plain(GroundW, GroundH), seed);
            w.NoGameEnd = true;
            w.Bots.Add(new ZBot(w, Cpu, seed + 101) { Ignored = true });
            w.Events.Clear();
            var s = new Scene { w = w, ox = GroundW * 8, oy = GroundH * 8 };
            Tick(s, 2, null);
            return s;
        }
        static void Tick(Scene s, int n, Action<ZEvent> each)
        {
            for (int i = 0; i < n; i++) { s.w.Tick(ZConst.TICK); if (each != null) foreach (var e in s.w.Events) each(e); s.w.Events.Clear(); }
        }
        static ZObject Robot(Scene s, string type, int cx, int cy, Team t, int n = 1) => s.w.CreateRobotGroup(type, cx - 8, cy - 8, t, n);
        /// <summary>An enemy dummy the vehicle fires at: a blue grunt that fires nothing, never moves and never dies (OzV11Kit's dummy).</summary>
        static ZObject Dummy(Scene s, int cx, int cy) { var d = Robot(s, "grunt", cx, cy, Cpu); d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = 1000000000; return d; }

        /// <summary>A crewed red vehicle of `type` at (x, y): its `driver` (a grunt, a psycho) got in by the player's ENTER order.</summary>
        static ZObject Crewed(Scene s, string type, int x, int y, string driver = "grunt")
        {
            var v = s.w.CreateObject("vehicle", type, x - 16, y - 16, Team.Neutral);
            var d = Robot(s, driver, x - 64, y, Me);
            OzV11Kit.PlayerEnter(s.w, d, v);
            for (int i = 0; i < 2000 && !(v.Owner == Me && v.Drivers.Count > 0); i++) Tick(s, 1, null);
            return v;
        }

        /// <summary>A shot of any weapon by v (the rules' events): the gun's hit roll and missile, the mech's hand gun, a hatch driver's own weapon, a bolt.</summary>
        static bool ShotBy(ZEvent e, ZObject v) => e.objId == v.Id && (e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired || e.type == ZEventType.MechHandGun
                                                                        || e.type == ZEventType.DriverShot || e.type == ZEventType.ElectroBolt || e.type == ZEventType.ElectroCharge);

        sealed class After
        {
            public long stopTick = -1; public int shotsBefore, shotsAfter, hgBefore, hgAfter, dsBefore, dsAfter, ticks, targetTicks, orderTicks, movingTicks; public double moved, x0, y0;
            public bool turned, armTurned, neutral, wasMoving, needHg, needDs, needMoving, needHit, hitFound; public string driver = "grunt", stop = "the EXIT";
            public float fx, fy; public double arm0 = double.NaN, arm1 = double.NaN; public string note = ""; public int recrewedAt = -1, seed = -1;
            /// <summary>Fix round 3 (review: the stunned fire path and the help call on an empty hull were never counted): B1: an enemy's hits on the
            /// empty hull after the stop (its health lost); B3: stunned at the stop, its shots while stunned, driving when the stun came.</summary>
            public bool needHullHit, needStun, wasStunned, drivingAtStun; public int hullLost, stunShots;
            /// <summary>One event of v before the stop (counted per weapon) / after it.</summary>
            public void Before(ZEvent e) { shotsBefore++; if (e.type == ZEventType.MechHandGun) hgBefore++; if (e.type == ZEventType.DriverShot) dsBefore++; }
            public void AfterShot(ZEvent e) { shotsAfter++; if (e.type == ZEventType.MechHandGun) hgAfter++; if (e.type == ZEventType.DriverShot) dsAfter++; }
        }

        /// <summary>Counts from the stop tick on, `seconds` long: shots by v, its move from where it stood, its facing and arm, its target and order.
        /// fromTickStart (fix round 2, B2): the caller set the baseline at the START of the stop's own tick (x0, y0, fx, fy, arm0) and that tick
        /// has run: its move, turn, target and order are counted first.</summary>
        static void Watch(Scene s, ZObject v, After a, double seconds, bool fromTickStart = false)
        {
            if (!fromTickStart) { a.x0 = v.PosX; a.y0 = v.PosY; a.fx = v.FaceX; a.fy = v.FaceY; a.arm0 = v.HandGunArmHeading; }
            else
            {   // the snipe's own tick, from its start: the vehicle ran after the sniper's hit
                a.ticks++;
                if (v.AttackObject != null) a.targetTicks++;
                if (v.Waypoints.Count > 0) a.orderTicks++;
                if (v.IsMoving) a.movingTicks++;
                double dx0 = v.PosX - a.x0, dy0 = v.PosY - a.y0; a.moved = Math.Max(a.moved, Math.Sqrt(dx0 * dx0 + dy0 * dy0));
                if (v.FaceX != a.fx || v.FaceY != a.fy) a.turned = true;
                double arm0 = v.HandGunArmHeading; if (!(double.IsNaN(arm0) && double.IsNaN(a.arm0)) && !(arm0 == a.arm0)) a.armTurned = true;
                a.arm1 = arm0;
            }
            int n = (int)Math.Round(seconds / ZConst.TICK);
            for (int i = 0; i < n; i++)
            {
                if (v.Owner != Team.Neutral || v.Drivers.Count > 0) { a.recrewedAt = a.ticks; break; }               // a robot climbed in (the normal rule once nobody is inside): a new driver, the count ends
                s.w.Tick(ZConst.TICK);
                foreach (var e in s.w.Events) if (ShotBy(e, v)) a.AfterShot(e);
                s.w.Events.Clear();
                a.ticks++;
                if (v.AttackObject != null) a.targetTicks++;
                if (v.Waypoints.Count > 0) a.orderTicks++;
                if (v.IsMoving) a.movingTicks++;
                double dx = v.PosX - a.x0, dy = v.PosY - a.y0; a.moved = Math.Max(a.moved, Math.Sqrt(dx * dx + dy * dy));
                if (v.FaceX != a.fx || v.FaceY != a.fy) a.turned = true;
                double arm = v.HandGunArmHeading;
                if (!(double.IsNaN(arm) && double.IsNaN(a.arm0)) && !(arm == a.arm0)) a.armTurned = true;
                a.arm1 = arm;
            }
            a.neutral = a.recrewedAt >= 0 || (v.Owner == Team.Neutral && v.Drivers.Count == 0);
        }

        /// <summary>Every sample a row needs (fix round 1: the mech's hand gun fired and its arm was on a robot at the stop; a psycho driver's own
        /// weapon fired; B2: the vehicle was driving at the snipe), then the counts after the stop.</summary>
        static bool Good(After a) => a.stopTick >= 0 && a.shotsBefore > 0 && a.shotsAfter == 0 && a.moved <= MoveBarPx && !a.turned && !a.armTurned && a.targetTicks == 0 && a.orderTicks == 0
                                     && a.movingTicks == 0 && a.neutral && a.ticks >= 100
                                     && (!a.needHg || (a.hgBefore > 0 && !double.IsNaN(a.arm0))) && (!a.needDs || a.dsBefore > 0) && (!a.needMoving || a.wasMoving) && (!a.needHit || a.hitFound)
                                     && (!a.needHullHit || a.hullLost > 0) && (!a.needStun || (a.wasStunned && a.stunShots > 0 && a.drivingAtStun));
        static string Says(After a) => $"stop tick {a.stopTick}; shots before {a.shotsBefore} (hand gun {a.hgBefore}, driver's weapon {a.dsBefore}), driving at the stop {a.wasMoving}; in the {a.ticks} ticks after: "
                                       + $"shots {a.shotsAfter} (hand gun {a.hgAfter}, driver's weapon {a.dsAfter}), moved {a.moved:0.000} px (bar {MoveBarPx}), "
                                       + $"facing turned {a.turned}, hand-gun arm turned {a.armTurned} ({(double.IsNaN(a.arm0) ? "none" : a.arm0.ToString("0.0"))} -> {(double.IsNaN(a.arm1) ? "none" : a.arm1.ToString("0.0"))}), "
                                       + $"ticks with a target {a.targetTicks}, with an order {a.orderTicks}, moving {a.movingTicks}; neutral and empty {a.neutral}"
                                       + $"{(a.recrewedAt >= 0 ? $" (a robot climbed in at +{a.recrewedAt} ticks: counted until then)" : "")}"
                                       + $"{(a.needHullHit ? $"; an enemy hit the empty hull for {a.hullLost} health (the help-call path)" : "")}"
                                       + $"{(a.needStun ? $"; stunned at the stop {a.wasStunned}, driving when the stun came {a.drivingAtStun}, its shots while stunned {a.stunShots}" : "")}{a.note}";
        static string Needs(After a) => (a.needHg ? "; needs: the hand gun fired and its arm on a robot at the stop" : "") + (a.needDs ? $"; needs: the {a.driver} driver's own weapon fired" : "")
                                        + (a.needMoving ? $"; needs: driving at {a.stop}" : "")                       // fix round 2 (review): B1 at the EXIT, B2 at the snipe
                                        + (a.needHullHit ? "; needs: the empty hull hit by an enemy" : "") + (a.needStun ? $"; needs: stunned at {a.stop}, fired while stunned, driving when stunned" : "");   // fix round 3

        // ------------------------------------------------------------------------------------------------ B1: EXIT while driving and firing
        static void B1(string set, string type, string driver, out After a)
        {
            a = new After { driver = driver, needHg = type == ZMTank.Mech, needDs = driver != "grunt", needMoving = true, stop = "the EXIT", needHullHit = true };
            var s = New(set);
            var v = Crewed(s, type, s.ox - 220, s.oy, driver);
            if (v.Owner != Me || v.DriverType != driver) { a.note = $"; the {driver} driver did NOT get in"; return; }
            for (int k = 0; k < 5; k++) Dummy(s, s.ox - 150 + 90 * k, s.oy + (k % 2 == 0 ? -30 : 30));     // along its way, 30 px off it: in the gun's, the hand gun's (120 px) and the hatch driver's reach as it passes
            int east = s.ox + 300, west = s.ox - 260;
            OzV11Kit.PlayerMove(s.w, v, east, s.oy, true);                                                     // an attack-move: it fires at them
            bool lastShot = false, lastHg = false, lastDs = false, driving = false;
            for (int i = 0; i < 9000; i++)
            {
                bool need = (!a.needHg || a.hgBefore > 0) && (!a.needDs || a.dsBefore > 0);
                if (a.shotsBefore > 0 && need && !driving) { driving = true; OzV11Kit.PlayerMove(s.w, v, v.CenterX < s.ox ? east : west, s.oy, false); }   // then a plain move on past them: it drives (and fires on the move)
                if (driving && v.IsMoving && v.Waypoints.Count > 0 && (a.needHg ? lastHg : a.needDs ? lastDs : lastShot)) break;   // right after a shot while it drives (the mech: its hand gun; a psycho driver: his burst)
                s.w.Tick(ZConst.TICK);
                lastShot = lastHg = lastDs = false;
                foreach (var e in s.w.Events) if (ShotBy(e, v)) { a.Before(e); lastShot = true; lastHg |= e.type == ZEventType.MechHandGun; lastDs |= e.type == ZEventType.DriverShot; }
                s.w.Events.Clear();
                if (v.Owner == Me && !v.IsMoving && v.Waypoints.Count == 0) OzV11Kit.PlayerMove(s.w, v, v.CenterX < s.ox ? east : west, s.oy, !driving);   // to and fro past them
            }
            a.wasMoving = v.IsMoving && v.Waypoints.Count > 0;
            bool ok = s.w.Eject(Me, v); s.w.Events.Clear();                                                   // the player's EXIT between two ticks (PlayerInput)
            if (!ok) { a.note = "; the EXIT was refused: " + (v.CanExitDriver(out string why) ? "?" : why); return; }
            a.stopTick = s.w.TickIndex;
            // fix round 3 (review: no enemy hit the empty hull, so the help-call path UnmannedHold drops never ran): an enemy beside it keeps
            // shooting the empty hull (damage 2 a hit; it never dies): its target ticks must stay 0
            var hitter = Robot(s, "grunt", v.CenterX + 36, v.CenterY + 36, Cpu); hitter.MoveSpeed = 0; hitter.Damage = 2; hitter.MaxHealth = hitter.Health = 1000000000;
            OzV11Kit.CpuAttack(s.w, hitter, v);
            int hp0 = v.Health;
            Watch(s, v, a, 10);
            a.hullLost = hp0 - v.Health;
        }

        // ------------------------------------------------------------------------------------------------ B3: stunned by an Electrobot, then emptied
        /// <summary>Fix round 3 (review: the stunned vehicle's fire path StunHold runs BEFORE UnmannedHold and was never counted): an enemy
        /// Electrobot (it never dies, its bolt 1 damage) stuns the vehicle while it drives and fires at dummies; once it has fired while stunned
        /// (StunHold's fire steps), EXIT DRIVER right after such a shot (B3), or its driver sniped by real enemy snipers while it is stunned (B3s,
        /// the seeds in turn as B2, the vehicle after the snipers in the object list, the snipe's tick split at the sniper's own hit). The same
        /// counts as B1 / B2 from the stop on; the Electrobot keeps bolting (the empty hull is hit, never stunned again).</summary>
        static void B3(string set, string type, bool snipe, out After a)
        {
            a = null; After last = null;
            foreach (int seed in snipe ? SnipeSeeds : new[] { 1 })
            {
                var t = B3Seed(set, type, snipe, seed);
                last = t;
                if (t.stopTick >= 0 && t.wasStunned) { a = t; break; }
            }
            a ??= last;
            if (snipe && !(a.stopTick >= 0 && a.wasStunned)) a.note += $"; no seed of {SnipeSeeds[0]}..{SnipeSeeds[SnipeSeeds.Length - 1]} had the snipe land while it was stunned";
        }

        static After B3Seed(string set, string type, bool snipe, int seed)
        {
            var a = new After { needHg = type == ZMTank.Mech, needStun = true, needHit = snipe, seed = seed, stop = snipe ? "the snipe" : "the EXIT" };
            var s = New(set, seed);
            var v = Crewed(s, type, s.ox - 220, s.oy);
            if (v.Owner != Me) { a.note = "; the driver did NOT get in"; return a; }
            for (int k = 0; k < 5; k++) Dummy(s, s.ox - 150 + 90 * k, s.oy + (k % 2 == 0 ? -30 : 30));
            int east = s.ox + 300, west = s.ox - 260;
            OzV11Kit.PlayerMove(s.w, v, east, s.oy, true);                                                     // an attack-move past the dummies: it drives and fires
            ZObject el = null; int elCharges = 0, elBolts = 0; double elNear = double.MaxValue;
            bool lastShot = false, driving = false;
            for (int i = 0; i < 9000; i++)
            {
                bool need = a.shotsBefore > 0 && (!a.needHg || a.hgBefore > 0);
                if (need && !driving) { driving = true; OzV11Kit.PlayerMove(s.w, v, v.CenterX < s.ox ? east : west, s.oy, false); }   // as B1: then a plain move on past them (it drives, it fires on the move; the dummies never die)
                if (el == null && driving && v.IsMoving && v.Waypoints.Count > 0)
                {   // the Electrobot standing beside its way ahead of it, at it at once (it drives past it inside the bolt's reach)
                    int ahead = v.Waypoints[0].x >= v.CenterX ? 70 : -70;
                    el = Robot(s, ZElectro.Type, v.CenterX + ahead, v.CenterY - 36, Cpu); el.Damage = 1; el.MoveSpeed = 0; el.MaxHealth = el.Health = 1000000000;
                    OzV11Kit.CpuAttack(s.w, el, v);
                }
                if (el != null && i % 50 == 0 && el.AttackObject != v && v.Owner == Me) OzV11Kit.CpuAttack(s.w, el, v);
                if (!snipe && v.Stunned && a.stunShots > 0 && lastShot) break;                                // B3: right after a shot while stunned
                if (snipe && v.Stunned && a.stunShots > 0) break;                                              // B3s: the snipers come now
                bool movingBefore = v.IsMoving && v.Waypoints.Count > 0, stunnedBefore = v.Stunned;
                s.w.Tick(ZConst.TICK);
                lastShot = false;
                foreach (var e in s.w.Events) if (ShotBy(e, v)) { a.Before(e); lastShot = true; if (stunnedBefore && v.Stunned) a.stunShots++; }
                if (el != null) foreach (var e in s.w.Events) if (e.objId == el.Id) { if (e.type == ZEventType.ElectroCharge) elCharges++; else if (e.type == ZEventType.ElectroBolt) elBolts++; }
                if (el != null) elNear = Math.Min(elNear, Math.Sqrt((double)(el.CenterX - v.CenterX) * (el.CenterX - v.CenterX) + (double)(el.CenterY - v.CenterY) * (el.CenterY - v.CenterY)));
                s.w.Events.Clear();
                if (!stunnedBefore && v.Stunned) a.drivingAtStun = movingBefore;
                if (v.Destroyed || v.KillAt >= 0) { a.note = $"; seed {seed}: the vehicle was destroyed before the stop"; return a; }
                if (!v.Stunned && v.Owner == Me && !v.IsMoving && v.Waypoints.Count == 0) OzV11Kit.PlayerMove(s.w, v, v.CenterX < s.ox ? east : west, s.oy, !driving);   // to and fro past them
            }
            string elSays = el == null ? "no Electrobot made" : $"the Electrobot ({el.Type}) charged {elCharges} times, bolted {elBolts}, came within {elNear:0} px, its target {(el.AttackObject != null ? "#" + el.AttackObject.Id : "none")}";
            if (!v.Stunned || a.stunShots == 0) { a.note = $"; seed {seed}: no stun with a shot while stunned in 90 s (stunned {v.Stunned}, its shots while stunned {a.stunShots}; {elSays})"; return a; }
            a.note = "; " + elSays;
            if (!snipe)
            {
                a.wasStunned = v.Stunned; long left = v.StunTicksLeft;
                bool ok = s.w.Eject(Me, v); s.w.Events.Clear();
                if (!ok) { a.note = "; the EXIT was refused: " + (v.CanExitDriver(out string why) ? "?" : why); return a; }
                a.stopTick = s.w.TickIndex; a.note = $"; EXIT while stunned ({left} ticks of the stun left, stunned after it {v.Stunned}){a.note}";
                Watch(s, v, a, 10);
                return a;
            }
            // B3s: real snipers at its driver while the Electrobot keeps it stunned (the vehicle after the snipers in the object list, as B2)
            var squads = new List<ZObject>();
            if (type == "light") squads.Add(Robot(s, "sniper", v.CenterX + 40, v.CenterY - 100, Cpu, 3));
            else { squads.Add(Robot(s, "sniper", v.CenterX + 40, v.CenterY - 100, Cpu, 3)); squads.Add(Robot(s, "sniper", v.CenterX - 40, v.CenterY - 104, Cpu, 3)); squads.Add(Robot(s, "sniper", v.CenterX, v.CenterY + 100, Cpu, 3)); }
            s.w.Objects.Remove(v); s.w.Objects.Add(v);
            long sniped = -1; int sniper = -1;
            for (int i = 0; i < 18000 && sniped < 0; i++)
            {
                if (i % 50 == 0)
                {
                    foreach (var sq in squads) foreach (var m in OzV11Kit.Squad(sq)) if (OzV11Kit.Alive(m) && m.AttackObject != v && v.Owner == Me) OzV11Kit.CpuAttack(s.w, m, v);
                    if (el.AttackObject != v && v.Owner == Me) OzV11Kit.CpuAttack(s.w, el, v);
                }
                bool stunnedBefore = v.Stunned;
                double bx = v.PosX, by = v.PosY, barm = v.HandGunArmHeading; float bfx = v.FaceX, bfy = v.FaceY;
                s.w.Tick(ZConst.TICK);
                var ev = s.w.Events; int hit = -1; bool snipedNow = false;
                foreach (var e in ev) if (e.type == ZEventType.Sniped && e.objId == v.Id) { snipedNow = sniped < 0; if (snipedNow) sniper = e.otherId; }
                if (snipedNow) for (int k = 0; k < ev.Count; k++) if (ev[k].type == ZEventType.Hitscan && ev[k].flag2 && ev[k].otherId == v.Id) { hit = k; break; }
                for (int k = 0; k < ev.Count; k++)
                    if (ShotBy(ev[k], v)) { if (snipedNow && (hit < 0 || k > hit)) a.AfterShot(ev[k]); else { a.Before(ev[k]); if (stunnedBefore) a.stunShots++; } }
                if (snipedNow)
                {
                    sniped = s.w.TickIndex; a.wasStunned = stunnedBefore; a.hitFound = hit >= 0;
                    a.x0 = bx; a.y0 = by; a.fx = bfx; a.fy = bfy; a.arm0 = barm;
                }
                s.w.Events.Clear();
                if (v.Destroyed || v.KillAt >= 0) break;
            }
            if (sniped < 0) { a.note = v.Destroyed || v.KillAt >= 0 ? $"; seed {seed}: the hull was destroyed before a snipe" : $"; seed {seed}: NO SNIPE in 180 s"; return a; }
            a.stopTick = sniped; a.note = $"; seed {seed}: sniped at tick {sniped} by #{sniper} while stunned {a.wasStunned}{a.note}";
            Watch(s, v, a, 10, true);
            return a;
        }

        // ------------------------------------------------------------------------------------------------ B2: the driver sniped by real enemy snipers while it drives
        static void B2(string set, string type, string driver, out After a)
        {
            a = null; After last = null;
            foreach (int seed in SnipeSeeds)
            {
                var t = B2Seed(set, type, driver, seed);
                last = t;
                if (t.stopTick >= 0 && t.wasMoving) { a = t; break; }
            }
            a ??= last;
            if (!(a.stopTick >= 0 && a.wasMoving)) a.note += $"; no seed of {SnipeSeeds[0]}..{SnipeSeeds[SnipeSeeds.Length - 1]} had the snipe land while it drove";
        }

        static After B2Seed(string set, string type, string driver, int seed)
        {
            var a = new After { driver = driver, needHg = type == ZMTank.Mech, needDs = driver != "grunt", needMoving = true, needHit = true, seed = seed, stop = "the snipe" };
            var s = New(set, seed);
            var v = Crewed(s, type, s.ox - 260, s.oy, driver);
            if (v.Owner != Me || v.DriverType != driver) { a.note = $"; the {driver} driver did NOT get in"; return a; }
            // first: it drives and fires (an attack-move past dummies; the mech's hand gun too)
            Dummy(s, s.ox - 160, s.oy - 40); Dummy(s, s.ox - 80, s.oy + 40);
            OzV11Kit.PlayerMove(s.w, v, s.ox - 20, s.oy, true);
            for (int i = 0; i < 3000 && !(a.shotsBefore > 0 && (!a.needHg || a.hgBefore > 0) && (!a.needDs || a.dsBefore > 0)); i++)
            {
                s.w.Tick(ZConst.TICK);
                foreach (var e in s.w.Events) if (ShotBy(e, v)) a.Before(e);
                s.w.Events.Clear();
                if (v.Owner == Me && !v.IsMoving && v.Waypoints.Count == 0) OzV11Kit.PlayerMove(s.w, v, v.CenterX < s.ox - 120 ? s.ox - 20 : s.ox - 260, s.oy, true);
            }
            // then: it drives to and fro across the snipers' line (a plain move: it does not halt to fire) while real snipers shoot at its driver
            var squads = new List<ZObject>();
            if (type == "light") squads.Add(Robot(s, "sniper", s.ox + 40, s.oy - 100, Cpu, 3));                  // one squad: the light tank's hull would die first under nine
            else { squads.Add(Robot(s, "sniper", s.ox + 40, s.oy - 100, Cpu, 3)); squads.Add(Robot(s, "sniper", s.ox - 40, s.oy - 104, Cpu, 3)); squads.Add(Robot(s, "sniper", s.ox, s.oy + 100, Cpu, 3)); }
            // fix round 2 (review: the Sniped event comes after every object's step): the vehicle moved to the end of the world's object list (the
            // server loop runs the objects in that order): it runs after the snipers in every tick, so in the snipe's tick its whole step comes
            // after the sniper's hit and is counted from that tick's start
            s.w.Objects.Remove(v); s.w.Objects.Add(v);
            int west = s.ox - 200, east = s.ox + 200;
            OzV11Kit.PlayerMove(s.w, v, east, s.oy, false);
            long sniped = -1; int sniper = -1;
            for (int i = 0; i < 18000 && sniped < 0; i++)
            {
                if (i % 50 == 0) foreach (var sq in squads) foreach (var m in OzV11Kit.Squad(sq)) if (OzV11Kit.Alive(m) && m.AttackObject != v && v.Owner == Me) OzV11Kit.CpuAttack(s.w, m, v);
                bool movingBefore = v.IsMoving && v.Waypoints.Count > 0;                                         // its state at the start of this tick
                double bx = v.PosX, by = v.PosY, barm = v.HandGunArmHeading; float bfx = v.FaceX, bfy = v.FaceY;
                s.w.Tick(ZConst.TICK);
                var ev = s.w.Events; int hit = -1; bool snipedNow = false;
                foreach (var e in ev) if (e.type == ZEventType.Sniped && e.objId == v.Id) { snipedNow = sniped < 0; if (snipedNow) sniper = e.otherId; }
                if (snipedNow) for (int k = 0; k < ev.Count; k++) if (ev[k].type == ZEventType.Hitscan && ev[k].flag2 && ev[k].otherId == v.Id) { hit = k; break; }   // the sniper's own hit
                for (int k = 0; k < ev.Count; k++)
                    if (ShotBy(ev[k], v)) { if (snipedNow && (hit < 0 || k > hit)) a.AfterShot(ev[k]); else a.Before(ev[k]); }   // a shot of its own after the sniper's hit in the snipe's own tick counts as after
                if (snipedNow)
                {
                    sniped = s.w.TickIndex; a.wasMoving = movingBefore; a.hitFound = hit >= 0;
                    a.x0 = bx; a.y0 = by; a.fx = bfx; a.fy = bfy; a.arm0 = barm;                                  // the baseline: the START of the snipe's tick
                    a.note = hit >= 0 ? $"; the sniper's hit is event {hit} of {ev.Count} in its tick, the vehicle's own events after it counted as after" : "; NO sniper's hit (Hitscan, snipe flag) found in the snipe's tick";
                }
                s.w.Events.Clear();
                if (v.Destroyed || v.KillAt >= 0) break;
                if (sniped < 0 && v.Owner == Me && !v.IsMoving && v.Waypoints.Count == 0) OzV11Kit.PlayerMove(s.w, v, v.CenterX < s.ox ? east : west, s.oy, false);   // keep it driving to and fro
            }
            if (sniped < 0) { a.note = v.Destroyed || v.KillAt >= 0 ? $"; seed {seed}: the hull was destroyed before a snipe" : $"; seed {seed}: NO SNIPE in 180 s"; return a; }
            a.stopTick = sniped; a.note = $"; seed {seed}: sniped at tick {sniped} by #{sniper} ({s.w.GetAny(sniper)?.Type}; the vehicle runs after the snipers in every tick: last in the object list){a.note}";
            // the tick of the snipe has run: its move, facing and arm counted from that tick's start, then every tick after
            Watch(s, v, a, 10, true);
            return a;
        }

        // ------------------------------------------------------------------------------------------------ H: the hold per type
        static void H(string set, string type, out int heldTicks, out bool exact)
        {
            heldTicks = -1; exact = false;
            var s = New(set);
            var v = Crewed(s, type, s.ox, s.oy);
            if (v.Owner != Me) return;
            Tick(s, 50, null);
            bool ok = s.w.Eject(Me, v); var r = s.w.Events.Where(e => e.type == ZEventType.Created && e.otherId == v.Id).Select(e => s.w.Get(e.objId)).FirstOrDefault(o => o != null); s.w.Events.Clear();
            if (!ok || r == null) return;
            int hold = ZObject.DriverOutHoldTicks(type); long t0 = s.w.TickIndex; bool heldLast = false;
            for (int i = 0; i < hold + 50; i++)
            {
                bool before = r.ExitWalking(s.w.Time);
                Tick(s, 1, null);
                if (i == hold - 2) heldLast = r.ExitWalking(s.w.Time);
                if (before && !r.ExitWalking(s.w.Time)) { heldTicks = (int)(s.w.TickIndex - t0); break; }
            }
            exact = heldLast && heldTicks == hold;
        }

        static IDisposable Rule() => new Hold();
        sealed class Hold : IDisposable
        {
            readonly bool ve = ZWorld.VehicleExitDeviation, calm = ZWorld.CalmStartDeviation, hg = ZWorld.MechHandGunDeviation, loss = ZWorld.MechGunLossDeviation, df = ZWorld.DriverFireDeviation;
            public Hold()
            {
                ZWorld.VehicleExitDeviation = true; ZWorld.CalmStartDeviation = false;
                ZWorld.MechHandGunDeviation = true; ZWorld.MechGunLossDeviation = true; ZWorld.DriverFireDeviation = true;   // fix round 1: as ZGame at every start that is not 'Zod exact' (View/ZGame.cs:161-163)
            }
            public void Dispose()
            {
                ZWorld.VehicleExitDeviation = ve; ZWorld.CalmStartDeviation = calm;
                ZWorld.MechHandGunDeviation = hg; ZWorld.MechGunLossDeviation = loss; ZWorld.DriverFireDeviation = df;
            }
        }

        /// <summary>The whole check (B1, B1p, B2, B2p, B3, B3s, H; in the Editor also H5).</summary>
        public static string Run(string settings) => Go(settings, "vehicle exit jump: stop", true);
        /// <summary>The Deviations row's probe: the same rows (they are quick: a few s of Sim time per scene); fix round 3 (review: the DV171 probe
        /// FAILED wherever the staging folder's Play check report is missing, the second PC and every later version): without H5 (an INFO line;
        /// H5 stays in the menu's full Run).</summary>
        public static string Quick(string settings) => Go(settings, "vehicle exit jump: stop quick", false);

        static string Go(string settings, string title, bool h5)
        {
            pass = fail = 0; sb = new StringBuilder();
            var t0 = DateTime.UtcNow;
            try
            {
                using (Rule())
                {
                    Info($"DV79 unmanned stop {ZWorld.UnmannedStopDeviation} (on at every start, View/ZGame.cs), DV171 {ZWorld.VehicleExitDeviation}, DV132's hold {ZWorld.ExitWalkHoldOn}, "
                         + $"DV146 the mech's hand gun {ZWorld.MechHandGunDeviation}, DV87 the hatch driver's weapon {ZWorld.DriverFireDeviation}; holds "
                         + string.Join(" / ", Types.Select(t => Name(t) + " " + ZObject.DriverOutHoldTicks(t))) + " ticks");
                    foreach (var type in Types)
                    {
                        B1(settings, type, "grunt", out var a);
                        Check(Good(a), $"B1 {Name(type)}: EXIT DRIVER right after a shot while it drives (it fired at enemy dummies on an attack-move, then drives on past them): from the EXIT's tick on {Says(a)}{Needs(a)}");
                    }
                    foreach (var type in Tanks)
                    {
                        B1(settings, type, "psycho", out var a);
                        Check(Good(a), $"B1p {Name(type)}, a psycho driver: EXIT DRIVER right after his own burst from the hatch while it drives and fires: from the EXIT's tick on {Says(a)}{Needs(a)}");
                    }
                    foreach (var type in Types)
                    {
                        B2(settings, type, "grunt", out var a);
                        Check(Good(a), $"B2 {Name(type)}: its driver sniped by real enemy snipers while it drives (it fired before): from the snipe's tick on {Says(a)}{Needs(a)}");
                    }
                    foreach (var type in Tanks)
                    {
                        B2(settings, type, "psycho", out var a);
                        Check(Good(a), $"B2p {Name(type)}, a psycho driver: sniped by real enemy snipers while it drives (he fired before): from the snipe's tick on {Says(a)}{Needs(a)}");
                    }
                    foreach (var type in Types)
                    {
                        B3(settings, type, false, out var a);
                        Check(Good(a), $"B3 {Name(type)}: stunned by an enemy Electrobot while it drove and fired, then EXIT DRIVER right after its shot while stunned (StunHold's fire path): from the EXIT's tick on {Says(a)}{Needs(a)}");
                    }
                    foreach (var type in Types)
                    {
                        B3(settings, type, true, out var a);
                        Check(Good(a), $"B3s {Name(type)}: stunned by an enemy Electrobot while it drove and fired, then its driver sniped by real enemy snipers while stunned: from the snipe's tick on {Says(a)}{Needs(a)}");
                    }
                    foreach (var type in Types)
                    {
                        H(settings, type, out int held, out bool exact);
                        int hold = ZObject.DriverOutHoldTicks(type); double sec = hold * ZConst.TICK;
                        Check(exact && sec >= HoldMinSec - 1e-9 && sec <= HoldMaxSec + 1e-9,
                              $"H {Name(type)}: the driver let out is held for {held} ticks (ZObject.DriverOutHoldTicks {hold} = {sec:0.00} s; 1.0-1.5 s): exact {exact}; the view lands his figure on that tick (H5: the Play check's measured touch)");
                    }
#if UNITY_EDITOR && !OPENZ_HARNESS
                    if (h5) PlayTouchRows();
                    else Info("H5 (the Play check's measured touch against the hold) is not in the quick probe (it reads the staging folder's Play check report): menu OpenZ/V1.4/Vehicle Exit Jump: Stop Count (headless) counts it");
#else
                    Info("H5 (the Play check's measured touch against the hold) is counted in the Editor only (menu Stop Count): it reads the Play check's last report" + (h5 ? "" : "; not in the quick probe"));
#endif
                }
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"run time {(DateTime.UtcNow - t0).TotalSeconds:0.0} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.4 {title}] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>H5: a J1 row's machine tag, written by View/OzVehicleExitCheck.cs (JumpVerdict): "[touch &lt;type&gt; &lt;s&gt; hold &lt;s&gt;]".</summary>
        public static bool ParseTouch(string line, out string type, out double touch, out double hold)
        {
            type = null; touch = hold = -1;
            var m = System.Text.RegularExpressions.Regex.Match(line ?? "", @"\[touch (\S+) (-?[0-9.]+) hold ([0-9.]+)\]");
            if (!m.Success) return false;
            type = m.Groups[1].Value;
            return double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out touch)
                   && double.TryParse(m.Groups[3].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out hold);
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        /// <summary>H5 (Editor only), counted: per type the Play check's last report (proof/play/play_check_report.txt), its J1 row's measured touch
        /// against DriverOutHoldTicks (within TouchVsHoldSec); no report or no J1 row for that type: NOT MEASURED (FAIL, never a PASS).</summary>
        static void PlayTouchRows()
        {
            var f = System.IO.Path.Combine(PlayFolder, "play_check_report.txt");
            var lines = System.IO.File.Exists(f) ? System.IO.File.ReadAllLines(f).Where(l => l.Contains(" J1 ")).ToList() : new List<string>();
            foreach (var type in Types)
            {
                double hold = ZObject.DriverOutHoldTicks(type) * ZConst.TICK; string row = null; double touch = -1;
                foreach (var l in lines) if (ParseTouch(l, out string ty, out double tt, out _) && ty == type) { row = l; touch = tt; }
                if (row == null) { Check(false, $"H5 {Name(type)}: NOT MEASURED: {(System.IO.File.Exists(f) ? "the last Play check report has no J1 row for it" : "no Play check report at " + f)} (run OpenZ/V1.4/Vehicle Exit Jump: Play Check)"); continue; }
                bool passRow = row.TrimStart().StartsWith("PASS");
                Check(touch >= 0 && Math.Abs(touch - hold) <= TouchVsHoldSec + 1e-9,
                      $"H5 {Name(type)}: the Play check's measured touch {touch:0.00} s after the click against the rules' hold {hold:0.00} s (within {TouchVsHoldSec:0.0} s); its J1 row {(passRow ? "PASS" : "FAIL")}");
            }
        }

        public static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_4/kits/vehicle_exit_jump3/proof/play"));   // vehicle_exit_jump3: the leap's Play check writes its pictures and report there (H5 reads it)

        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit Jump: Stop Count (headless)")]
        static void Menu() => OzV11Kit.RunMenu("vehicle exit jump: stop count", () => Run(OzV11Kit.SettingsText));

        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit Jump: Play Check (real mouse, a level playing)")]
        static void PlayMenu() => PlayStart(null);

        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit Jump: Owner Frames (a level playing)")]
        static void FramesMenu() => PlayStart("frames");

        /// <summary>Fix round 3 (review: Only set by hand was wiped by the menu): 14c alone, on level 2 (its own report file).</summary>
        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit Jump: Play Check 14c (level 2)")]
        static void Play14cMenu() => PlayStart("14c");

        [UnityEditor.MenuItem("OpenZ/V1.4/Vehicle Exit Jump: Play Check, last report")]
        static void PlayLast() => UnityEngine.Debug.Log(OzVehicleExitCheck.Running ? "[OpenZ V1.4 vehicle exit play] running" : OzVehicleExitCheck.Report);

        static void PlayStart(string only)
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.4 vehicle exit play] start a level in Play first (level 2, the normal mode), click the Game view once"); return; }
            OzVehicleExitCheck.Folder = PlayFolder; OzVehicleExitCheck.Only = only;
            OzVehicleExitCheck.Begin();
        }
#endif
    }
}
