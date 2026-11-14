using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.4, REPEAT QUEUE, rule DV169 (Sim/ZObject.RepeatQueue.cs): the counted check, headless on the Sim alone, on the real level 2 map,
    /// nothing saved: menu OpenZ/V1.4/Repeat Queue: Count (headless, DV169), and off-project on plain .NET (the kit's tools/harness). One line
    /// per fact, [OpenZ V1.4 repeat queue] PASS n/n. The check turns the rule's switch on as View/ZGame.cs does at a level start (a fresh
    /// editor has it off until a Play start) on top of the counted checks' usual game switches. The scenes: level 2, red is the player (no
    /// CPU), blue a CPU that gives no orders; every unit of the map off it at the first tick, no game end, every other producer stopped, so
    /// only the building under test builds. Red's build-list bonus (the difficulty bonus a human gets) is 2 unless a scene says otherwise.
    ///  G  the switches' and the building's defaults;
    ///  L  REPEAT QUEUE on: grunt, light tank, medium tank built in a loop for 3 rounds, the queue turning after every unit; with REPEAT off
    ///     the same; a list of one keeps building its unit with REPEAT off;
    ///  O  REPEAT QUEUE off with the rule's switch on: today's queue exactly (the same units, ticks and events as with the switch off), with
    ///     REPEAT on (the last unit repeats) and off (idle after the queue);
    ///  R  a queue entry removed by the player leaves the loop for good; STOP clears it all and the next list loops again;
    ///  U  an entry the building can no longer build is skipped and dropped, the loop goes on; none left = idle;
    ///  C  at the unit cap the loop waits, nothing is skipped, and goes on in order once there is room;
    ///  K  a captured factory has REPEAT QUEUE off and its queue no longer turns;
    ///  B  the computer: a CPU match never has a building with REPEAT QUEUE on; another team's command is refused;
    ///  X  the rule off ('Zod exact'), or without DV21's queue: the command is refused and nothing loops;
    ///  D  the same scene twice = the same ticks and events.
    /// </summary>
    public static class OzV14RepeatQueueCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red;

        sealed class Scene
        {
            public ZWorld w; public ZObject b;
            public readonly List<string> built = new List<string>();
            public readonly List<long> builtTick = new List<long>();
            public readonly List<string> queueAfter = new List<string>();
            public ulong hash = 1469598103934665603UL; public long tick;
            public double Seconds => tick * ZConst.TICK;
            /// <summary>Runs ticks (stop: after each tick). Records the building's output (type, tick, the list right after) and an event hash.</summary>
            public void Run(double seconds, Func<bool> stop = null)
            {
                int n = (int)Math.Round(seconds / ZConst.TICK);
                for (int i = 0; i < n; i++)
                {
                    w.Tick(ZConst.TICK); tick++;
                    foreach (var e in w.Events)
                    {
                        hash = (hash ^ (ulong)(long)e.type) * 1099511628211UL; hash = (hash ^ (ulong)(long)e.objId) * 1099511628211UL; hash = (hash ^ (ulong)(long)e.otherId) * 1099511628211UL;
                        if (e.type == ZEventType.UnitBuilt && e.otherId == b.Id) { built.Add(w.Get(e.objId)?.Type ?? "?"); builtTick.Add(tick); queueAfter.Add(List); }
                        if (e.type == ZEventType.CannonStored && e.objId == b.Id) { built.Add("cannon"); builtTick.Add(tick); queueAfter.Add(List); }
                    }
                    w.Events.Clear();
                    if (stop != null && stop()) break;
                }
            }
            /// <summary>Runs until the building has built `n` units in all (at most `seconds`).</summary>
            public void RunBuilt(int n, double seconds) => Run(seconds, () => built.Count >= n);
            public string Order(int from = 0) => string.Join(",", built.Skip(from));
            /// <summary>The unit in production, then the queue: "light | medium,grunt" (idle: "idle | ").</summary>
            public string List => (b.Building && b.CurUnit != null ? b.CurUnit : "idle") + " | " + string.Join(",", b.Queue);
            public void Orders(params string[] units)
            {
                w.StartBuilding(Me, b, units[0]);
                for (int i = 1; i < units.Length; i++) w.AddBuildingQueue(Me, b, units[i]);
            }
        }

        /// <summary>Level 2 with red as the player; `type` = the red building under test ("fort" or a type).</summary>
        static Scene New(string set, string map, int bonus = 2, string type = "fort", int seed = 1)
        {
            var w = ZWorld.Create(set, map, seed);
            w.NoGameEnd = true;
            w.Bots.Add(new ZBot(w, Team.Blue, seed + 101) { Ignored = true });
            foreach (var o in w.Objects) if (o.IsUnit && o.Owner != Team.Neutral) o.KillAt = 0;          // off the map at the first tick
            w.TechBonus[(int)Me] = bonus;
            var b = type == "fort" ? w.Objects.First(o => o.IsFort && o.Owner == Me) : w.Objects.First(o => o.Type == type && o.Owner == Me);
            foreach (var o in w.Objects) if (o.IsProducer) o.StopBuildingProduction();
            return new Scene { w = w, b = b };
        }

        static string Rounds(string[] list, int rounds) => string.Join(",", Enumerable.Range(0, rounds).SelectMany(_ => list));

        // ------------------------------------------------------------------------------------------------ G
        static void G(string set, string map)
        {
            var s = New(set, map);
            Check(ZWorld.RepeatQueueDeviation && ZWorld.FifoQueueDeviation, $"G1 the check runs with the game's switches: DV169 {ZWorld.RepeatQueueDeviation}, DV21 {ZWorld.FifoQueueDeviation} (View/ZGame.cs sets DV169 at every start but 'Zod exact')");
            Check(s.w.Objects.Where(o => o.IsProducer).All(o => !o.RepeatQueue) && s.b.RepeatProduction,
                  $"G2 every producer of the level starts with REPEAT QUEUE off ({s.w.Objects.Count(o => o.IsProducer)} producers) and REPEAT on as before; the queue holds {ZObject.MAX_QUEUE_ITEMS}");
        }

        // ------------------------------------------------------------------------------------------------ L
        static void L(string set, string map)
        {
            var list = new[] { "grunt", "light", "medium" };
            var s = New(set, map); s.Orders(list);
            bool cmd = s.w.SetBuildingRepeatQueue(Me, s.b, true);
            s.RunBuilt(9, 2400);
            Check(cmd && s.Order() == Rounds(list, 3), $"L1 REPEAT QUEUE on, grunt then light tank and medium tank queued: built {s.Order()} in {s.Seconds:0} s (3 rounds), turns {s.w.RepeatQueueTurns}");
            Check(s.queueAfter.Count >= 3 && s.queueAfter[0] == "light | medium,grunt" && s.queueAfter[1] == "medium | grunt,light" && s.queueAfter[2] == "grunt | light,medium",
                  $"L2 the list after each unit: {string.Join("; ", s.queueAfter.Take(3))} (the finished unit goes to the end)");
            Check(s.w.RepeatQueueTurns == 9 && s.w.RepeatQueueSkips == 0 && s.b.Queue.Count == 2, $"L3 9 units sent back to the end, 0 skipped, the list still 3 long ({s.List})");

            var r = New(set, map); r.Orders(list); r.w.SetBuildingRepeat(Me, r.b, false); r.w.SetBuildingRepeatQueue(Me, r.b, true);
            r.RunBuilt(6, 1700);
            Check(r.Order() == Rounds(list, 2), $"L4 the same with REPEAT off: built {r.Order()} (REPEAT does not matter while the queue loops)");

            var one = New(set, map); one.Orders("grunt"); one.w.SetBuildingRepeat(Me, one.b, false); one.w.SetBuildingRepeatQueue(Me, one.b, true);
            one.RunBuilt(3, 400);
            Check(one.Order() == "grunt,grunt,grunt" && one.b.Building, $"L5 a list of one with REPEAT off and REPEAT QUEUE on: built {one.Order()}, still building {one.List}");
        }

        // ------------------------------------------------------------------------------------------------ O
        static void O(string set, string map)
        {
            var list = new[] { "grunt", "light", "medium" };
            string Today(bool rule, bool repeat, out ulong hash, out string after)
            {
                bool was = ZWorld.RepeatQueueDeviation; ZWorld.RepeatQueueDeviation = rule;
                try
                {
                    var s = New(set, map); s.Orders(list); if (!repeat) s.w.SetBuildingRepeat(Me, s.b, false);
                    s.Run(repeat ? 1300 : 900);
                    hash = s.hash; after = s.List + " turns " + s.w.RepeatQueueTurns;
                    return s.Order() + " @ " + string.Join(",", s.builtTick);
                }
                finally { ZWorld.RepeatQueueDeviation = was; }
            }
            string onR = Today(true, true, out var hOn, out var aOn), offR = Today(false, true, out var hOff, out var aOff);
            Check(onR == offR && hOn == hOff && onR.StartsWith("grunt,light,medium,medium"), $"O1 REPEAT QUEUE off, REPEAT on: {onR.Split('@')[0].Trim()} (the last repeats); with the rule's switch on and off the same units, ticks and events ({hOn:X16}); {aOn}");
            string on2 = Today(true, false, out var h2On, out var a2On), off2 = Today(false, false, out var h2Off, out _);
            Check(on2 == off2 && h2On == h2Off && on2.StartsWith("grunt,light,medium @") && a2On.StartsWith("idle | "), $"O2 REPEAT QUEUE off, REPEAT off: {on2.Split('@')[0].Trim()}, then {a2On} (idle after the queue), the same with the switch off");
        }

        // ------------------------------------------------------------------------------------------------ R
        static void R(string set, string map)
        {
            var s = New(set, map); s.Orders("grunt", "light", "medium"); s.w.SetBuildingRepeatQueue(Me, s.b, true);
            s.RunBuilt(1, 400);
            string before = s.List;
            int i = s.b.Queue.IndexOf("grunt");
            bool removed = i >= 0 && s.w.CancelBuildingQueue(Me, s.b, i, "grunt");
            string cut = s.List;
            s.RunBuilt(5, 1500);
            Check(removed && before == "light | medium,grunt" && s.Order() == "grunt,light,medium,light,medium" && !s.b.Queue.Contains("grunt") && s.b.CurUnit != "grunt",
                  $"R1 the grunt removed from the queue ({before} -> {cut}): built {s.Order()}, the loop goes on without it ({s.List})");

            var t = New(set, map); t.Orders("grunt", "jeep", "crane"); t.w.SetBuildingRepeatQueue(Me, t.b, true);
            t.RunBuilt(2, 400);
            bool stop = t.w.StopBuilding(Me, t.b); int n = t.built.Count;
            t.Run(300);
            bool idle = !t.b.Building && t.b.CurUnit == null && t.b.Queue.Count == 0 && t.built.Count == n;
            t.Orders("grunt", "jeep");
            t.RunBuilt(n + 3, 400);
            Check(stop && idle && t.b.RepeatQueue && t.Order(n) == "grunt,jeep,grunt", $"R2 STOP: queue cleared, nothing built in 300 s, the switch stays on; the next list (grunt, jeep) loops: {t.Order(n)}");
        }

        // ------------------------------------------------------------------------------------------------ U
        static void U(string set, string map)
        {
            var s = New(set, map); s.Orders("grunt", "light", "medium"); s.w.SetBuildingRepeatQueue(Me, s.b, true);
            s.RunBuilt(1, 400);                                                                           // now: light in production, medium, grunt queued
            s.w.TechBonus[(int)Me] = 1;                                                                   // level 1: no medium tank any more
            bool gone = !ZBuildLists.InList(s.b.Type, s.b.BuildLevel, "medium") && ZBuildLists.InList(s.b.Type, s.b.BuildLevel, "light");
            s.RunBuilt(6, 1500);
            Check(gone && s.Order() == "grunt,light,grunt,light,grunt,light" && s.w.RepeatQueueSkips == 1 && !s.b.Queue.Contains("medium"),
                  $"U1 the medium tank leaves the build list (level 2 -> 1): skipped and dropped once, the loop goes on: built {s.Order()}, skips {s.w.RepeatQueueSkips} ({s.List})");

            var a = New(set, map); a.Orders("light", "medium"); a.w.SetBuildingRepeatQueue(Me, a.b, true);
            a.w.TechBonus[(int)Me] = 0;                                                                   // level 0: neither
            a.Run(700);
            Check(a.Order() == "light" && !a.b.Building && a.b.CurUnit == null && a.b.Queue.Count == 0 && a.w.RepeatQueueSkips == 2,
                  $"U2 none of the list buildable any more: the unit in production comes out ({a.Order()}), then both skipped (skips {a.w.RepeatQueueSkips}) and the building idle ({a.List})");
        }

        // ------------------------------------------------------------------------------------------------ C
        static void C(string set, string map)
        {
            var s = New(set, map, 0); s.Orders("grunt", "jeep"); s.w.SetBuildingRepeatQueue(Me, s.b, true);
            s.RunBuilt(1, 400);
            var extra = new List<ZObject>();
            int k = 0;
            while (s.w.UnitCount(Me) < ZWorld.MAX_UNITS_PER_TEAM)
            {   // guns of the player's, well away from the building: they never move, they count for the cap
                var g = OzV11Kit.Place(s.w, "gatling", 64 + 40 * (k % 10), 64 + 40 * (k / 10), Me); extra.Add(g); k++;
            }
            double due = s.b.BFinal - s.w.Time;
            string held = s.List; int n = s.built.Count;
            s.Run(due + 60);
            bool waited = s.built.Count == n && s.List == held && s.w.UnitLimitReached(Me);
            for (int i = 0; i < 10; i++) extra[i].KillAt = s.w.Time;
            s.RunBuilt(n + 3, 500);
            Check(waited && s.Order() == "grunt,jeep,grunt,jeep" && s.w.RepeatQueueSkips == 0,
                  $"C1 at the unit cap ({ZWorld.MAX_UNITS_PER_TEAM}) the loop waits {due + 60:0} s with {held} and builds nothing; room again: built {s.Order()}, nothing skipped");
        }

        // ------------------------------------------------------------------------------------------------ K
        static void K(string set, string map)
        {
            var s = New(set, map, 2, "robot_factory");
            var flag = s.w.Flags.FirstOrDefault(f => f.LinkedObjects.Contains(s.b));
            string how = "red's own robot factory";
            if (flag == null)
            {   // red's robot factory stands in its fort's zone (no flag): red first takes a flag zone with a robot factory in it
                flag = s.w.Flags.FirstOrDefault(f => f.LinkedObjects.Any(o => o.Type == "robot_factory"));
                if (flag == null) { Check(false, "K1 no flag zone with a robot factory on level 2"); return; }
                s.w.AwardZone(flag, Me);
                foreach (var o in flag.LinkedObjects) if (o.IsProducer) o.StopBuildingProduction();
                s.b = flag.LinkedObjects.First(o => o.Type == "robot_factory"); how = $"the robot factory #{s.b.Id} of flag #{flag.Id}, taken by red first";
            }
            s.Orders("grunt", "psycho"); s.w.SetBuildingRepeatQueue(Me, s.b, true);
            s.RunBuilt(2, 400);
            int turns = s.w.RepeatQueueTurns;
            s.w.AwardZone(flag, Team.Blue);
            bool off = s.b.Owner == Team.Blue && !s.b.RepeatQueue;
            s.Run(400);
            Check(off && s.w.RepeatQueueTurns == turns && s.b.Queue.Count == 0, $"K1 blue takes {how} with REPEAT QUEUE on: off at once, its queue no longer turns (turns {turns} -> {s.w.RepeatQueueTurns}, the list {s.List}, built {s.Order()})");
        }

        // ------------------------------------------------------------------------------------------------ B
        static void B(string set, string map)
        {
            var w = ZWorld.Create(set, map, 3);
            w.Bots.Add(new ZBot(w, Team.Blue, 104)); w.Bots.Add(new ZBot(w, Team.Red, 3));
            int seen = 0, producing = 0;
            for (int i = 0; i < 30000; i++)
            {
                w.Tick(ZConst.TICK); w.Events.Clear();
                if (i % 100 == 0) foreach (var o in w.Objects) if (o.IsProducer) { if (o.RepeatQueue) seen++; if (o.Building) producing++; }
            }
            Check(seen == 0 && w.RepeatQueueTurns == 0 && producing > 0, $"B1 computer against computer on level 2, 300 s, the switch on: never a building with REPEAT QUEUE on (seen {seen}), turns {w.RepeatQueueTurns}, the buildings producing ({producing} looks)");
            var s = New(set, map);
            var blueFort = s.w.Objects.First(o => o.IsFort && o.Owner == Team.Blue);
            bool refused = !s.w.SetBuildingRepeatQueue(Team.Blue, s.b, true) && !s.w.SetBuildingRepeatQueue(Me, blueFort, true) && !s.b.RepeatQueue && !blueFort.RepeatQueue;
            Check(refused, "B2 a team's command on a building it does not own is refused (blue on red's fort, red on blue's fort)");
        }

        // ------------------------------------------------------------------------------------------------ X
        static void X(string set, string map)
        {
            bool was = ZWorld.RepeatQueueDeviation;
            try
            {
                ZWorld.RepeatQueueDeviation = false;
                var s = New(set, map); s.Orders("grunt", "jeep");
                bool refused = !s.w.SetBuildingRepeatQueue(Me, s.b, true);
                s.b.RepeatQueue = true;                                                                   // even forced on, the rule is off
                s.RunBuilt(4, 700);
                Check(refused && s.Order() == "grunt,jeep,jeep,jeep" && s.w.RepeatQueueTurns == 0, $"X1 the rule off ('Zod exact'): the command refused; a building forced on builds today's {s.Order()}");
            }
            finally { ZWorld.RepeatQueueDeviation = was; }
            bool fifo = ZWorld.FifoQueueDeviation;
            try
            {
                ZWorld.FifoQueueDeviation = false;
                var s = New(set, map); s.Orders("grunt");
                Check(!s.w.SetBuildingRepeatQueue(Me, s.b, true) && !s.b.RepeatQueue, "X2 without DV21's queue (Zod's own queue) the command is refused");
            }
            finally { ZWorld.FifoQueueDeviation = fifo; }
        }

        // ------------------------------------------------------------------------------------------------ D
        static void D(string set, string map)
        {
            var runs = new List<string>();
            for (int k = 0; k < 2; k++)
            {
                var s = New(set, map); s.Orders("grunt", "light", "medium"); s.w.SetBuildingRepeatQueue(Me, s.b, true);
                s.Run(700);
                runs.Add($"{s.Order()} @ {string.Join(",", s.builtTick)} events {s.hash:X16} rand {s.w.Rng.Next()}");
            }
            Check(runs[0] == runs[1], "D1 the same scene twice (700 s, REPEAT QUEUE on): the same units, ticks and events: " + runs[0]);
        }

        static IDisposable Rule() => new Hold();
        sealed class Hold : IDisposable
        {
            readonly bool rq = ZWorld.RepeatQueueDeviation, fifo = ZWorld.FifoQueueDeviation;
            public Hold() { ZWorld.RepeatQueueDeviation = true; ZWorld.FifoQueueDeviation = true; }   // as View/ZGame.cs at a start (DV21 is a code switch, on)
            public void Dispose() { ZWorld.RepeatQueueDeviation = rq; ZWorld.FifoQueueDeviation = fifo; }
        }

        public static string Run(string settings, Func<string, string> map)
        {
            pass = fail = 0; sb = new StringBuilder();
            string l02 = map("L02");
            var t0 = DateTime.UtcNow;
            try
            {
                using (Rule()) { G(settings, l02); L(settings, l02); O(settings, l02); R(settings, l02); U(settings, l02); C(settings, l02); K(settings, l02); B(settings, l02); X(settings, l02); D(settings, l02); }
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"run time {(DateTime.UtcNow - t0).TotalSeconds:0.0} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.4 repeat queue] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe on a quiet level 2: two rounds of grunt, jeep, crane with REPEAT QUEUE on; off = today's order; a
        /// removed entry leaves the loop.</summary>
        public static string Quick(string settings, string l02)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                using (Rule())
                {
                    var list = new[] { "grunt", "jeep", "crane" };
                    var s = New(settings, l02, 0); s.Orders(list); s.w.SetBuildingRepeatQueue(Me, s.b, true);
                    s.RunBuilt(6, 900);
                    Check(s.Order() == Rounds(list, 2), $"REPEAT QUEUE on: built {s.Order()} (the list loops)");
                    var o = New(settings, l02, 0); o.Orders(list);
                    o.RunBuilt(4, 900);
                    Check(o.Order() == "grunt,jeep,crane,crane" && o.w.RepeatQueueTurns == 0, $"REPEAT QUEUE off: built {o.Order()} (today's queue: the last repeats)");
                    var r = New(settings, l02, 0); r.Orders(list); r.w.SetBuildingRepeatQueue(Me, r.b, true);
                    r.RunBuilt(1, 300);
                    int i = r.b.Queue.IndexOf("grunt"); bool removed = i >= 0 && r.w.CancelBuildingQueue(Me, r.b, i, "grunt");
                    r.RunBuilt(4, 600);
                    Check(removed && r.Order() == "grunt,jeep,crane,jeep", $"a grunt removed from the loop: built {r.Order()}");
                }
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.4 repeat queue quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.4/Repeat Queue: Count (headless, DV169)")]
        static void Menu() => OzV11Kit.RunMenu("repeat queue: count (DV169)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));

        [UnityEditor.MenuItem("OpenZ/V1.4/Repeat Switch: Panel Layout Check (Play, a level playing)")]
        static void LayoutMenu()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.4 repeat switch layout] start a level in Play first, in the normal mode (the player's fort standing)"); return; }
            OzRepeatQueueCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_4/kits/queue_repeat_switch/proof/play"));
            OzRepeatQueueCheck.Begin();
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/Repeat Switch: Panel Layout Check, last report")]
        static void LayoutLast() => UnityEngine.Debug.Log(OzRepeatQueueCheck.Running ? "[OpenZ V1.4 repeat switch layout] running" : OzRepeatQueueCheck.Report);
#endif
    }
}
