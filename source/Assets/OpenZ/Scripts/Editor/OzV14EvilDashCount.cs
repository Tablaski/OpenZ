using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// (Sim/ZEvilZod.AutoDash.cs), headless on the Sim alone, on plain maps and one level, nothing saved: menu <see cref="MenuPath"/>, and off-project on plain .NET
    /// (the kit's tools/harness). One line per fact, [OpenZ V1.4 EvilZod dash] PASS n/n. In a fresh editor the rules' static switches are off: it sets the ones a
    /// Play session has (ZWorld.EvilZodDeviation as View/ZGame.cs does, the auto dash's own switch) and puts them back.
    ///  C  the cooldown measured from the dash start to the ready event: 750 ticks (7.5 s), a veteran 650 (6.5 s); the circle beside his name half full half way;
    ///  A  under attack with every enemy out of his range and the dash ready: the dash starts by itself within 0.5 s of the hit (a grunt squad 150 px away, a sniper
    ///     squad); the same dash as an order's (windup, burst, plant, the shot at the full number) at the nearest enemy; the next enemy once the cooldown is over;
    ///     a player order in its windup cancels it and gives the cooldown back;
    ///  N  no auto dash: not under attack; guarding the blue fort as its last defense; a running player move order (and the move done: he dashes); an enemy in his
    ///     range (he fires; that enemy down: he dashes at the far one); lava or deep water on the line; the enemy past the dash range; the switch off;
    ///  D  the same scene twice: the same ticks, events and places, the same random stream; P  level 2 with both computers: the same world, the auto dash on and off.
    /// </summary>
    public static class OzV14EvilDashCount
    {
        public const string MenuPath = "OpenZ/V1.4/EvilZod: Dash Count (headless)";
        static int pass, fail; static StringBuilder sb; static string Set;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        const string E = ZEvilZod.Type;
        /// <summary>0.5 s: the longest wait from the hit (or the cooldown's end, or the end of the move) to the dash start that passes.</summary>
        const int Within = 50;

        sealed class Scene
        {
            public ZWorld w; public ZObject e;
            /// <summary>The tick his LastEnemyHit first moved (-1 = never), how many ticks it moved on.</summary>
            public long firstHit = -1; public int hitTicks; double seen = -100;
            public readonly List<(long t, ZEvent ev)> log = new List<(long, ZEvent)>();
            public void Run(int ticks, Func<bool> stop = null)
            {
                for (int i = 0; i < ticks; i++)
                {
                    w.Tick(ZConst.TICK);
                    foreach (var x in w.Events) if (x.type >= ZEventType.EvilDashStart) log.Add((w.TickIndex, x));
                    w.Events.Clear();
                    if (e.LastEnemyHit != seen) { seen = e.LastEnemyHit; hitTicks++; if (firstHit < 0) firstHit = w.TickIndex; }
                    if (stop != null && stop()) return;
                }
            }
            public int Count(ZEventType t) => log.Count(l => l.ev.type == t);
            public (long t, ZEvent ev) First(ZEventType t) => log.FirstOrDefault(l => l.ev.type == t);
            public List<(long t, ZEvent ev)> All(ZEventType t) => log.Where(l => l.ev.type == t).ToList();
            public int Dashes => Count(ZEventType.EvilDashStart);
            public int Shots => Count(ZEventType.EvilShot);
        }

        static string Plain => OzV11Kit.Plain(200, 120);

        /// <summary>A plain map with painted tile classes (and lava), as DV161's check paints them.</summary>
        static string Painted(Func<int, int, int> cls, Func<int, int, int> lava = null)
        {
            int w = 200, h = 120;
            var c = new int[w * h]; var l = new int[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { c[y * w + x] = cls(x, y); if (lava != null) l[y * w + x] = lava(x, y); }
            var s = new StringBuilder("{\"file\":\"v14_evil_dash\",\"planet\":\"desert\",\"width\":" + w + ",\"height\":" + h + ",\"classes\":[");
            s.Append(string.Join(",", c)).Append("],\"tiles\":[").Append(string.Join(",", Enumerable.Repeat("0", w * h)));
            s.Append("],\"zones\":[{\"x\":0,\"y\":0,\"w\":" + w + ",\"h\":" + h + "}],\"objects\":[]");
            if (lava != null) s.Append(",\"lava\":[").Append(string.Join(",", l)).Append("]");
            return s.Append("}").ToString();
        }

        static Scene New(string map = null, int cx = 400, int cy = 400, int seed = 1)
        {
            var w = ZWorld.Create(Set, map ?? Plain, seed);
            w.NoGameEnd = true; w.Events.Clear();
            return new Scene { w = w, e = OzV11Kit.Place(w, E, cx, cy, Team.Blue) };
        }

        /// <summary>A red squad of n at (cx, cy). range &gt; 0: every member's range set to it (an attacker that outranges him, as snipers, a heavy tank or a squad on
        /// high ground do); still: it does not move; harmless: it does no damage; tough: it does not die; attack: the red player's attack order on him.</summary>
        static List<ZObject> Squad(Scene s, string type, int cx, int cy, int n, int range = 0, bool still = false, bool harmless = false, bool tough = false, bool attack = true)
        {
            var lead = OzV11Kit.Place(s.w, type, cx, cy, Team.Red, n);
            var all = OzV11Kit.Squad(lead).ToList();
            foreach (var r in all)
            {
                if (range > 0) r.AttackRadius = range;
                if (still) r.MoveSpeed = 0;
                if (harmless) r.Damage = 0;
                if (tough) { r.MaxHealth = 10000000; r.Health = 10000000; }
            }
            if (attack) OzV11Kit.PlayerAttack(s.w, lead, s.e);
            return all;
        }

        /// <summary>A red grunt that stands and does not fire back (DV161's check's target).</summary>
        static ZObject Dummy(Scene s, int cx, int cy, bool bigHp = false)
        {
            var t = OzV11Kit.Place(s.w, "grunt", cx, cy, Team.Red, 1);
            t.Damage = 0; t.MoveSpeed = 0;
            if (bigHp) { t.MaxHealth = 10000000; t.Health = 10000000; }
            return t;
        }

        static double D(ZObject a, ZObject b) => OzV11Kit.Dist(a, b);
        static bool Alive(ZObject o) => OzV11Kit.Alive(o);

        /// <summary>The nearest live red unit to him now, and its distance.</summary>
        static ZObject Nearest(Scene s, out double d)
        {
            ZObject best = null; d = double.MaxValue;
            foreach (var o in s.w.Objects) if (o.IsUnit && o.Owner == Team.Red && Alive(o)) { double k = D(s.e, o); if (k < d) { d = k; best = o; } }
            return best;
        }

        // ------------------------------------------------------------------------------------------------ the cooldown
        static void C()
        {
            foreach (bool vet in new[] { false, true })
            {
                var s = New(); s.e.Veteran = vet;
                var t = Dummy(s, 560, 400);
                OzV11Kit.PlayerAttack(s.w, s.e, t);
                s.Run(2);
                var start = s.First(ZEventType.EvilDashStart);
                long ready = s.e.EvilDashReadyTick; int want = vet ? 650 : 750;
                s.Run((int)(start.t + want / 2 - s.w.TickIndex));
                double half = s.e.EvilDashCharge01;
                s.Run((int)(ready - s.w.TickIndex) + 5);
                var r = s.First(ZEventType.EvilDashReady);
                Check(s.Dashes == 1 && ready - start.t == want && r.t == ready && ZEvilZod.CooldownTicks == 750 && ZEvilZod.VeteranCooldownTicks == 650 && s.e.EvilCooldownTicks == want,
                      $"C1 {(vet ? "a veteran" : "normal")}: an ordered dash, the ready event {r.t - start.t} ticks after its start = {want * ZConst.TICK:0.0} s (1.4: twice as fast, {(vet ? "13" : "15")} s before)");
                Check(Math.Abs(half - 0.5) < 0.002, $"C2 {(vet ? "a veteran" : "normal")}: the circle beside his name half way through: {half:0.000} full (the HUD reads EvilDashCharge01)");
            }
        }

        // ------------------------------------------------------------------------------------------------ the auto dash
        /// <summary>A1's scene: a grunt squad 150 px east whose range is 160 px, standing, firing at him.</summary>
        static Scene A1Scene(int seed = 1)
        {
            var s = New(seed: seed);
            Squad(s, "grunt", 550, 400, 3, range: 160, still: true);
            return s;
        }

        static void A()
        {
            {
                var s = A1Scene();
                ZObject near = null; double nearD = 0; bool readyAtHit = false;
                s.Run(1500, () =>
                {
                    if (s.firstHit == s.w.TickIndex) readyAtHit = s.e.EvilDashReadyTick == 0;
                    if (near == null && s.Dashes > 0) near = Nearest(s, out nearD);
                    return s.Count(ZEventType.EvilShot) > 0;
                });
                var start = s.First(ZEventType.EvilDashStart); var go = s.First(ZEventType.EvilDashGo); var end = s.First(ZEventType.EvilDashEnd); var shot = s.First(ZEventType.EvilShot);
                long wait = start.t - s.firstHit;
                Check(s.firstHit > 0 && readyAtHit && s.Dashes == 1 && wait >= 0 && wait <= Within && start.ev.flag,
                      $"A1 a grunt squad 150 px away (range 160) hits him, the dash ready: he dashes by himself {wait} ticks ({wait * ZConst.TICK:0.00} s) after the first hit (tick {s.firstHit})");
                Check(near != null && start.ev.otherId == near.Id, $"A1 at the nearest enemy: #{start.ev.otherId} ({nearD:0} px), the nearest #{near?.Id}");
                Check(go.t - start.t == ZEvilZod.WindupTicks && end.ev.flag && end.t - go.t == start.ev.radius && shot.t - end.t == ZEvilZod.PlantTicks,
                      $"A1 the order's dash: windup {go.t - start.t} ticks, burst {end.t - go.t}, plant {shot.t - end.t}, then the shot");
                Check(shot.ev.otherId == start.ev.otherId && shot.ev.flag && shot.ev.flag2,
                      $"A1 the dash shot at that enemy (the full number's shot, flag dash): it took {(int)shot.ev.value} HP, all it had, killed");
                Check(s.e.EvilDashReadyTick - start.t == ZEvilZod.CooldownTicks && s.w.EvilAutoDashes == 1 && s.e.EvilAutoDashes == 1 && s.w.EvilDashes == 1,
                      $"A1 its cooldown {s.e.EvilDashReadyTick - start.t} ticks, as an ordered dash's; counted as an auto dash ({s.w.EvilAutoDashes})");
            }
            {   // real snipers, their own range 144
                var s = New();
                Squad(s, "sniper", 400, 540, 3);
                s.Run(1500, () => s.Dashes > 0);
                var start = s.First(ZEventType.EvilDashStart); long wait = start.t - s.firstHit;
                var t = s.w.Get(start.ev.otherId);
                Check(s.firstHit > 0 && s.Dashes == 1 && wait >= 0 && wait <= Within && t != null && t.Type == "sniper",
                      $"A2 a red sniper squad 140 px away (their own range 144) hits him: he dashes at a sniper {wait} ticks after the first hit");
            }
            {   // the next enemy: an ordered dash first, then a squad out of his reach fires at him; the cooldown over, he dashes at it by himself
                var s = New();
                var t = Dummy(s, 560, 400);
                var far = Squad(s, "grunt", 480, 590, 3, range: 200, still: true);
                OzV11Kit.PlayerAttack(s.w, s.e, t);
                s.Run(2);
                long ready = s.e.EvilDashReadyTick;
                s.Run(2000, () => s.Dashes > 1);
                var ds = s.All(ZEventType.EvilDashStart);
                var second = ds.Count > 1 ? ds[1] : default;
                var at = second.ev.otherId > 0 ? s.w.Get(second.ev.otherId) : null;
                Check(ds.Count == 2 && !ds[0].ev.flag && second.ev.flag && second.t >= ready && second.t - ready <= Within && at != null && far.Contains(at) && !Alive(t),
                      $"A3 an ordered dash, then a squad out of his reach (range 200) fires at him: once the cooldown is over (tick {ready}) he dashes at it by himself, {(ds.Count > 1 ? second.t - ready : -1)} ticks later");
                Check(s.firstHit > 0 && s.firstHit < ready && ds.Count(x => x.t < ready) == 1,
                      $"A3 under fire from tick {s.firstHit}, before the cooldown ended: no auto dash while it ran ({ds.Count(x => x.t < ready) - 1} besides the ordered one)");
            }
            {   // a player order in the windup
                var s = A1Scene();
                s.Run(1500, () => s.Dashes > 0);
                OzV11Kit.PlayerMove(s.w, s.e, 400, 300, false); s.Run(3);
                Check(s.Dashes == 1 && s.Count(ZEventType.EvilDashCancel) == 1 && s.Count(ZEventType.EvilDashGo) == 0 && s.e.EvilDashReadyTick == 0,
                      "A4 a player's move in the auto dash's windup: cancelled, the cooldown given back");
                s.Run(300);
                Check(s.Dashes == 1, $"A4 and no new auto dash while that move runs ({s.Dashes - 1})");
            }
        }

        // ------------------------------------------------------------------------------------------------ no auto dash
        static void N()
        {
            {
                var s = New();
                Squad(s, "grunt", 550, 400, 3, range: 160, still: true, harmless: true);
                s.Run(400);
                Check(s.Dashes == 0 && s.firstHit < 0 && s.e.EvilDashReadyTick == 0, $"N1 not under attack (the same squad does no damage): no dash in 4 s, the dash ready all along");
            }
            Guarding();
            {
                var s = New();
                var reds = Squad(s, "grunt", 550, 400, 3, range: 160);
                OzV11Kit.PlayerMove(s.w, s.e, 400, 1000, false);
                int hitsWalking = 0; bool allMove = true;
                s.Run(600, () => { allMove &= s.e.Waypoints.Count > 0 && s.e.Waypoints[0].playerGiven; return false; });
                hitsWalking = s.hitTicks;
                Check(s.Dashes == 0 && hitsWalking >= 2 && allMove, $"N3 a player's move order running, hit on {hitsWalking} ticks in 6 s by a squad that follows him: no dash");
            }
            {
                var s = New();
                Squad(s, "grunt", 550, 400, 3, range: 160);
                OzV11Kit.PlayerMove(s.w, s.e, 400, 440, false);
                long arrived = -1;
                s.Run(3000, () =>
                {
                    bool order = false; foreach (var w in s.e.Waypoints) order |= w.playerGiven;
                    if (arrived < 0 && !order) arrived = s.w.TickIndex;
                    return s.Dashes > 0;
                });
                var start = s.First(ZEventType.EvilDashStart);
                bool hitBefore = s.firstHit > 0 && s.firstHit < arrived;
                Check(arrived > 0 && hitBefore && s.Dashes == 1 && start.t >= arrived && start.t - arrived <= Within && start.ev.flag,
                      $"N3 a short player move under fire: no dash on the way; done at tick {arrived}, he dashes {(s.Dashes > 0 ? start.t - arrived : -1)} ticks later");
            }
            {
                var s = New();
                var near = Squad(s, "grunt", 500, 400, 3, tough: true);
                var far = Squad(s, "grunt", 400, 580, 3, range: 200, still: true);
                s.Run(400);
                Check(s.Dashes == 0 && s.Shots >= 1 && s.hitTicks >= 1 && s.e.EvilDashReadyTick == 0, $"N4 a squad in his range ({D(s.e, near[0]):0} px) and one far off, both firing: no dash, he fires ({s.Shots} shots)");
                foreach (var r in near) { r.SetHealth(0); s.w.UpdateObjectHealth(r); }
                long down = s.w.TickIndex;
                s.Run(400, () => s.Dashes > 0);
                var start = s.First(ZEventType.EvilDashStart); var at = s.w.Get(start.ev.otherId);
                Check(s.Dashes == 1 && start.t - down <= Within && at != null && far.Contains(at), $"N4 the near squad down: he dashes at the far one {(s.Dashes > 0 ? start.t - down : -1)} ticks later");
            }
            void Line(string name, string map)
            {
                var s = New(map);
                Squad(s, "grunt", 550, 400, 3, range: 160, still: true);
                s.Run(500);
                Check(s.Dashes == 0 && s.hitTicks >= 1, $"N5 {name} on the line, under fire ({s.hitTicks} hit ticks): no dash (the order's dash refuses that line too)");
            }
            Line("lava", Painted((x, y) => x == 29 ? (int)ZClass.Impassable : 0, (x, y) => x == 29 ? 2 : 0));
            Line("deep water", Painted((x, y) => x == 29 ? (int)ZClass.DeepWater : 0));
            {
                var s = New();
                Squad(s, "grunt", 630, 400, 3, range: 260, still: true);
                s.Run(500);
                Check(s.Dashes == 0 && s.hitTicks >= 1, $"N6 a squad 230 px away, past the dash range ({ZEvilZod.DashRangePx} px), under fire: no dash");
            }
            {
                bool was = ZWorld.EvilAutoDashDeviation; ZWorld.EvilAutoDashDeviation = false;
                try
                {
                    var s = A1Scene(); s.Run(500);
                    Check(s.Dashes == 0 && s.hitTicks >= 1, "N7 the switch off: A1's scene, no dash (DV161 round 2: on orders only)");
                }
                finally { ZWorld.EvilAutoDashDeviation = was; }
            }
        }

        /// <summary>N2: the blue fort's last defense (DV165) on a plain map with two forts; at his spot, a squad that outranges him fires at him.</summary>
        static void Guarding()
        {
            string plain = OzV11Kit.Plain(80, 120, ("building:fort_front", 30, 8, "blue"), ("building:fort_back", 30, 100, "red"));
            var w = ZWorld.Create(Set, plain, 1); w.NoGameEnd = true; w.Events.Clear(); w.EvilLastDefenseTeam = Team.Blue;
            while (w.TickIndex < ZEvilAppear.LastDefenseTicks + 3000) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            var ez = w.Objects.FirstOrDefault(o => o.IsEvilZod && Alive(o));
            if (ez == null || !ez.EvilGuarding) { Check(false, "N2 the last defense did not come out on the plain map"); return; }
            var s = new Scene { w = w, e = ez };
            int gx = ez.EvilGuardX, gy = ez.EvilGuardY;
            Squad(s, "grunt", gx, gy + 150, 3, range: 160, still: true);
            double off = 0;
            s.Run(600, () => { off = Math.Max(off, Math.Sqrt((double)(ez.CenterX - gx) * (ez.CenterX - gx) + (double)(ez.CenterY - gy) * (ez.CenterY - gy))); return false; });
            Check(ez.EvilGuarding && s.Dashes == 0 && s.hitTicks >= 1 && ez.EvilDashReadyTick == 0 && off <= ZEvilAppear.StepPx + ZEvilAppear.HomePx,
                  $"N2 guarding the blue fort, a squad 150 px from his spot fires at him ({s.hitTicks} hit ticks), the dash ready: no dash, he holds his spot ({off:0} px off it at most, {s.Shots} shots)");
        }

        // ------------------------------------------------------------------------------------------------ determinism, levels
        static string Print(Scene s)
        {
            var b = new StringBuilder();
            foreach (var l in s.log) b.Append(l.t).Append(':').Append((int)l.ev.type).Append(',').Append(l.ev.objId).Append(',').Append(l.ev.otherId).Append(',').Append(l.ev.x).Append(',').Append(l.ev.y).Append(';');
            foreach (var o in s.w.Objects) b.Append(o.Id).Append('@').Append(o.PosX).Append(',').Append(o.PosY).Append(',').Append(o.Health).Append(';');
            return b.Append('|').Append(s.w.Rng.Next(1000000)).ToString();
        }

        static void DP(Func<string, string> map)
        {
            string Once() { var s = A1Scene(7); s.Run(1200); return Print(s); }
            string a = Once(), b = Once();
            Check(a == b && a.Length > 40, "D1 A1's scene twice (seed 7, 12 s): the same ticks, events, places and random stream");
            bool was = ZWorld.EvilAutoDashDeviation;
            try
            {
                ZWorld.EvilAutoDashDeviation = false; string off = OzV13EvilZodCount.LevelPrint(Set, map("L02"), 1, 30);
                ZWorld.EvilAutoDashDeviation = true; string on = OzV13EvilZodCount.LevelPrint(Set, map("L02"), 1, 30);
                Check(off == on && off.Length > 0, "P1 L02: 30 s with both computers, no EvilZod: the object print the same with the auto dash on and off");
            }
            finally { ZWorld.EvilAutoDashDeviation = was; }
        }

        static string Wrap(Action body)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.EvilZodDeviation, auto = ZWorld.EvilAutoDashDeviation;
            ZWorld.EvilZodDeviation = true; ZWorld.EvilAutoDashDeviation = true;        // as View/ZGame.cs: EvilZod on outside 'Zod exact'; the auto dash's own switch on (its default)
            try { body(); }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " at " + (ex.StackTrace ?? "").Split('\n').FirstOrDefault()); }
            finally { ZWorld.EvilZodDeviation = was; ZWorld.EvilAutoDashDeviation = auto; }
            return sb.ToString();
        }

        /// <summary>The whole check. Runs under OzV11Kit.GameSwitches.</summary>
        public static string Run(string settings, Func<string, string> map)
        {
            Set = settings;
            string r = Wrap(() => { C(); A(); N(); DP(map); });
            return r + "[OpenZ V1.4 EvilZod dash] " + (fail == 0 ? "PASS" : "FAIL") + " " + pass + "/" + (pass + fail) + "\n";
        }

        /// <summary>The Deviations row's probe (DV161): the cooldown and A1.</summary>
        public static string Quick(string settings)
        {
            Set = settings;
            return Wrap(() => { C(); A1Quick(); });
        }

        static void A1Quick()
        {
            var s = A1Scene();
            s.Run(1500, () => s.Dashes > 0);
            var start = s.First(ZEventType.EvilDashStart); long wait = start.t - s.firstHit;
            Check(s.firstHit > 0 && s.Dashes == 1 && wait >= 0 && wait <= Within && start.ev.flag, $"A1 under fire from 150 px, the dash ready: he dashes by himself {wait} ticks after the first hit");
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem(MenuPath)]
        static void Menu() => OzV11Kit.RunMenu("EvilZod: dash count (1.4)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));
#endif
    }
}
