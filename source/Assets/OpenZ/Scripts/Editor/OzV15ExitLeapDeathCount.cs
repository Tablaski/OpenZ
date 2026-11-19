using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit exit_leap_death, rule DV179 "thrown by the blast" (Sim/ZObject.DriverThrown.cs): the counted check, headless on the Sim alone, on
    /// a plain map (OzV11Kit.Plain), nothing saved: menu OpenZ/V1.5/Exit Leap Death: Count (headless, DV179). One line per fact,
    /// [OpenZ V1.5 exit leap death] PASS n/n. The check turns DV171 and DV179 on as View/ZGame.cs does at a start (and the DV69 calm start off),
    /// and puts them back. Red is the player; blue a CPU that gives no orders of its own.
    ///  T1 each of the four (light, medium, heavy tank, mech), a grunt driver let out by EXIT, the hull destroyed n ticks after the EXIT (n = 0,
    ///     1, 20, 40, 41: before his take-off): thrown (DriverThrownFromId = the hull), held DriverThrownTicks from the tick his hold saw it
    ///     (held at +259, free at +260), alive, never moved while held, a MOVE given at the EXIT carried out once he is free;
    ///  T2 the same, n = 42, 60, 100 (the window over): not thrown, free in the next tick (1.4's K3);
    ///  T3 a target while thrown: an enemy grunt beside him CanAttackObject = true at +1 and +259 of the thrown hold (DV171's own exception
    ///     carried on); DV132's shuttle / APC passengers are not touched (the rule reads DV171's own record only);
    ///  T4 DV179 off ('Zod exact' / the 1.4 rule): n = 0 and 20: not thrown, free in the next tick; n = 60 (after the window): DV179 on and
    ///     off give the same fingerprint 6 s later (the rule changes nothing outside its window);
    ///  T5 the same thrown scene twice: the same fingerprint.
    /// </summary>
    public static class OzV15ExitLeapDeathCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red, Cpu = Team.Blue;
        public static readonly string[] Types = { "light", ZMTank.Type, "heavy", ZMTank.Mech };
        static string Name(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech" : t;
        const long Thrown = ZObject.DriverThrownTicks, Window = ZObject.DriverThrownWindowTicks;

        sealed class Scene
        {
            public ZWorld w; public int ox, oy; public ulong hash = 1469598103934665603UL;
            void Fold(ZEvent e) { hash = (hash ^ (ulong)(long)e.type) * 1099511628211UL; hash = (hash ^ (ulong)(long)e.objId) * 1099511628211UL; hash = (hash ^ (ulong)(long)e.otherId) * 1099511628211UL; }
            public List<ZEvent> Take() { var l = new List<ZEvent>(w.Events); foreach (var e in l) Fold(e); w.Events.Clear(); return l; }
            public void Ticks(long n) { for (long i = 0; i < n; i++) { w.Tick(ZConst.TICK); foreach (var e in w.Events) Fold(e); w.Events.Clear(); } }
            public void Run(double seconds) => Ticks((long)Math.Round(seconds / ZConst.TICK));
            public ulong State() => w.RulesFingerprint(hash);
            public ZObject Robot(string type, int cx, int cy, Team t, int amount = 1) => w.CreateRobotGroup(type, cx - 8, cy - 8, t, amount);
            public ZObject Vehicle(string type, int cx, int cy, Team t) => w.CreateObject("vehicle", type, cx - 16, cy - 16, t);
            public bool Order(ZObject r, ZObject v, Team t, double secs = 20)
            {
                w.SendWaypoints(t, r, new[] { new ZWaypoint { mode = WpMode.Enter, refId = v.Id, x = v.CenterX, y = v.CenterY } });
                for (long i = 0, n = (long)Math.Round(secs / ZConst.TICK); i < n && !(v.Owner == t && v.Drivers.Count > 0); i++) Ticks(1);
                return v.Owner == t && v.Drivers.Count > 0;
            }
        }

        const int GroundW = 64, GroundH = 40;
        static string groundMap;
        static Scene New(string set, int seed = 1)
        {
            var w = ZWorld.Create(set, groundMap ??= OzV11Kit.Plain(GroundW, GroundH), seed);
            w.NoGameEnd = true;
            w.Bots.Add(new ZBot(w, Cpu, seed + 101) { Ignored = true });
            w.Events.Clear();
            var s = new Scene { w = w, ox = GroundW * 8, oy = GroundH * 8 };
            s.Ticks(2);
            return s;
        }

        sealed class Hold : IDisposable
        {
            readonly bool ve = ZWorld.VehicleExitDeviation, calm = ZWorld.CalmStartDeviation, thrown = ZWorld.DriverThrownDeviation, walk = ZWorld.ExitWalkHoldOn;
            public Hold(bool on) { ZWorld.VehicleExitDeviation = true; ZWorld.CalmStartDeviation = false; ZWorld.DriverThrownDeviation = on; ZWorld.ExitWalkHoldOn = true; }
            public void Dispose() { ZWorld.VehicleExitDeviation = ve; ZWorld.CalmStartDeviation = calm; ZWorld.DriverThrownDeviation = thrown; ZWorld.ExitWalkHoldOn = walk; }
        }

        /// <summary>One scene: a red `type` with a grunt driver (in by the player's ENTER order), EXIT, a MOVE for him at once, `n` ticks, the hull
        /// destroyed (between ticks, as a fatal hit is applied), then the next tick. Null: the EXIT was not taken.</summary>
        sealed class Result { public ZObject v, r; public bool thrownRec, heldAfter1; public double px, py; }
        static Result Exit(Scene s, string type, long n, bool move = true)
        {
            var v = s.Vehicle(type, s.ox, s.oy, Team.Neutral);
            var d = s.Robot("grunt", s.ox - 64, s.oy, Me);
            if (!s.Order(d, v, Me)) return null;
            s.Run(0.5);
            if (!s.w.Eject(Me, v)) return null;
            var ev = s.Take();
            var r = ev.Where(e => e.type == ZEventType.Created && e.otherId == v.Id).Select(e => s.w.Get(e.objId)).FirstOrDefault(o => o != null);
            if (r == null) return null;
            if (move) OzV11Kit.PlayerMove(s.w, r, r.CenterX + 160, r.CenterY, false);
            s.Ticks(n);
            v.SetHealth(0); s.w.UpdateObjectHealth(v); s.Take();
            double px = r.PosX, py = r.PosY;
            s.Ticks(1);
            return new Result { v = v, r = r, px = px, py = py, thrownRec = r.DriverThrownFromId == v.Id && r.DriverThrownAt >= 0, heldAfter1 = r.ExitWalking(s.w.Time) };
        }

        static double Moved(ZObject r, double px, double py) => Math.Sqrt((r.PosX - px) * (r.PosX - px) + (r.PosY - py) * (r.PosY - py));

        static void T1(string set)
        {
            foreach (var type in Types)
                foreach (long n in new long[] { 0, 1, 20, Window - 2, Window - 1 })
                {
                    var s = New(set);
                    var x = Exit(s, type, n);
                    if (x == null) { Check(false, $"T1 {Name(type)} n {n}: the EXIT was NOT taken"); continue; }
                    s.Ticks(Thrown - 1);                                                                 // +259 from the tick that saw the hull gone (Exit() already ran that tick)
                    bool held = x.r.ExitWalking(s.w.Time) && x.r.DriverThrownHeld, still = Moved(x.r, x.px, x.py) < 0.01, alive = OzV11Kit.Alive(x.r);
                    s.Ticks(1);
                    bool free = !x.r.ExitWalking(s.w.Time);
                    s.Run(0.5);                                                                          // his walk starts (before the turret debris lands, 3-4 s after the death)
                    bool walked = Moved(x.r, x.px, x.py) > 1;
                    Check(x.thrownRec && x.heldAfter1 && held && still && alive && free && walked,
                          $"T1 {Name(type)}, the hull destroyed {n} ticks after the EXIT (before his take-off at {Window}): thrown {x.thrownRec}, held in the next tick {x.heldAfter1}, "
                          + $"still held at +{Thrown - 1} {held} (moved {Moved(x.r, x.px, x.py):0.0} px while held: {still}), free at +{Thrown} {free}, alive {alive}, his MOVE done after it {walked}");
                }
        }

        static void T2(string set)
        {
            foreach (var type in new[] { "light", ZMTank.Mech })
                foreach (long n in new long[] { Window, 60, 100 })
                {
                    var s = New(set);
                    var x = Exit(s, type, n);
                    if (x == null) { Check(false, $"T2 {Name(type)} n {n}: the EXIT was NOT taken"); continue; }
                    Check(!x.thrownRec && !x.heldAfter1, $"T2 {Name(type)}, the hull destroyed {n} ticks after the EXIT (his take-off was at {Window}): not thrown {!x.thrownRec}, free in the next tick {!x.heldAfter1} (1.4)");
                }
        }

        static void T3(string set)
        {
            foreach (var type in new[] { "light", ZMTank.Mech })
            {
                var s = New(set);
                var x = Exit(s, type, 5, false);
                if (x == null) { Check(false, $"T3 {Name(type)}: the EXIT was NOT taken"); continue; }
                var foe = s.Robot("grunt", x.r.CenterX + 40, x.r.CenterY + 40, Cpu);
                bool a = foe.CanAttackObject(x.r) && x.r.DriverOutTarget;
                foe.KillAt = 0; foreach (var m in foe.Minions) if (m != null) m.KillAt = 0;      // asked once, then taken out (it would shoot him meanwhile)
                s.Ticks(Thrown - 3);
                var foe2 = s.Robot("grunt", x.r.CenterX + 40, x.r.CenterY - 40, Cpu);
                bool b = x.r.DriverThrownHeld && x.r.DriverOutTarget && foe2.CanAttackObject(x.r);
                Check(a && b, $"T3 {Name(type)}: the thrown driver is a target (an enemy grunt may pick him) at the hold's start {a} and at +{Thrown - 2} {b}");
            }
        }

        static void T4(string set)
        {
            ulong Fp(bool thrownOn, long n, out bool thrownRec, out bool held)
            {
                ulong f;
                using (new Hold(thrownOn))
                {
                    var s = New(set);
                    var x = Exit(s, "light", n);
                    thrownRec = x != null && x.thrownRec; held = x != null && x.heldAfter1;
                    s.Run(6);
                    f = s.State();
                }
                return f;
            }
            foreach (long n in new long[] { 0, 20 })
            {
                Fp(false, n, out bool tOff, out bool hOff);
                Check(!tOff && !hOff, $"T4 DV179 off, the hull destroyed {n} ticks after the EXIT: not thrown {!tOff}, free in the next tick {!hOff} (1.4)");
            }
            ulong on60 = Fp(true, 60, out bool t60, out _), off60 = Fp(false, 60, out _, out _);
            Check(!t60 && on60 == off60, $"T4 the hull destroyed 60 ticks after the EXIT (after the window): DV179 on and off give the same fingerprint 6 s later ({on60:X16} / {off60:X16}): nothing changes outside the window");
        }

        static void T5(string set)
        {
            ulong Once()
            {
                var s = New(set);
                Exit(s, "heavy", 10);
                s.Run(5);
                return s.State();
            }
            ulong a = Once(), b = Once();
            Check(a == b, $"T5 the same thrown scene twice: {a:X16} / {b:X16}");
        }

        public static string Run(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            var t0 = DateTime.UtcNow; int thrown0 = ZObject.DriversThrown;
            try
            {
                using (new Hold(true))
                {
                    Info($"the scenes: a plain {GroundW} x {GroundH}-tile map; DV171 {ZWorld.VehicleExitDeviation}, DV179 {ZWorld.DriverThrownDeviation}; window {Window} ticks, thrown hold {Thrown} ticks; "
                         + $"the 1.4 holds {string.Join(" / ", Types.Select(t => Name(t) + " " + ZObject.DriverOutHoldTicks(t)))} ticks");
                    T1(settings); T2(settings); T3(settings); T5(settings);
                }
                T4(settings);
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"drivers thrown in this run {ZObject.DriversThrown - thrown0}; run time {(DateTime.UtcNow - t0).TotalSeconds:0.0} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.5 exit leap death] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: the light tank and the mech, the hull destroyed 5 ticks after the EXIT: thrown and held for the
        /// thrown ticks, a target meanwhile; 60 ticks after: not thrown; DV179 off: not thrown.</summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                using (new Hold(true))
                {
                    foreach (var type in new[] { "light", ZMTank.Mech })
                    {
                        var s = New(settings);
                        var x = Exit(s, type, 5, false);
                        if (x == null) { Check(false, $"{Name(type)}: the EXIT was NOT taken"); continue; }
                        bool target = x.r.DriverOutTarget;
                        s.Ticks(Thrown - 1); bool held = x.r.ExitWalking(s.w.Time);   // +259 from the tick that saw the hull gone
                        s.Ticks(1); bool free = !x.r.ExitWalking(s.w.Time);
                        Check(x.thrownRec && x.heldAfter1 && held && free && target && OzV11Kit.Alive(x.r),
                              $"{Name(type)} destroyed 5 ticks after the EXIT: thrown {x.thrownRec}, held {Thrown} ticks ({held}, free then {free}), a target meanwhile {target}");
                        var s2 = New(settings);
                        var y = Exit(s2, type, 60, false);
                        Check(y != null && !y.thrownRec && !y.heldAfter1, $"{Name(type)} destroyed 60 ticks after the EXIT (after his take-off): not thrown, free at once");
                    }
                }
                using (new Hold(false))
                {
                    var s = New(settings);
                    var x = Exit(s, "light", 5, false);
                    Check(x != null && !x.thrownRec && !x.heldAfter1, "DV179 off: the hull destroyed 5 ticks after the EXIT frees him at once (1.4)");
                }
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.5 exit leap death quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.5/Exit Leap Death: Count (headless, DV179)")]
        static void Menu() => OzV11Kit.RunMenu("exit leap death: count (DV179)", () => Run(OzV11Kit.SettingsText));
#endif
    }
}
