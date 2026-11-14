using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// menu OpenZ/V1.3/EvilZod: Cone Count (headless, DV167), and off-project on plain .NET (the kit's tools/harness). One line per fact, [OpenZ V1.3 EvilZod cone] PASS n/n.
    ///  G  the cone test's own numbers (the 9 and 6 deg edges, ahead only);
    ///  C  one shot at a robot close in front: the robots behind it and beside it in the cone fall with it, in the same tick, each by its own distance (the damage
    ///     table of DV161), a real squad of three walking up to him, Alan and Brad; the switch off = DV161's single hit;
    ///  N  what the cone never hits: a robot out of the cone, behind him, past the pellets' reach, out of his range, behind a cliff, his own team, a vehicle;
    ///  D  a dash shot: its target the full number, the robot behind it by distance, the 6 deg cone;  K  the kills are his (veterancy), the deaths are relayed;
    ///  P  same scene = same events; no EvilZod in a level: the switch changes no world.
    /// </summary>
    public static class OzV13EvilConeCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        const string E = ZEvilZod.Type;
        const int X = 400, Y = 400;

        sealed class Scene
        {
            public ZWorld w; public ZObject e;
            public readonly List<(long t, ZEvent ev)> log = new List<(long, ZEvent)>();
            public void Run(int ticks)
            {
                for (int i = 0; i < ticks; i++)
                {
                    w.Tick(ZConst.TICK);
                    foreach (var x in w.Events) if (x.type >= ZEventType.EvilDashStart || x.type == ZEventType.Death) log.Add((w.TickIndex, x));
                    w.Events.Clear();
                }
            }
            public List<(long t, ZEvent ev)> All(ZEventType t) => log.Where(l => l.ev.type == t).ToList();
            public int Count(ZEventType t) => log.Count(l => l.ev.type == t);
            /// <summary>Runs until his first shot and 2 ticks more (at most `ticks`); the tick of that shot, -1 = none.</summary>
            public long FirstShot(int ticks = 400)
            {
                for (int i = 0; i < ticks; i++) { Run(1); var s = All(ZEventType.EvilShot); if (s.Count > 0) { Run(2); return s[0].t; } }
                return -1;
            }
        }

        static string Plain => OzV11Kit.Plain(200, 120);

        static string Painted(Func<int, int, int> cls)
        {
            int w = 200, h = 120;
            var c = new int[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) c[y * w + x] = cls(x, y);
            var s = new StringBuilder("{\"file\":\"v13_cone\",\"planet\":\"desert\",\"width\":" + w + ",\"height\":" + h + ",\"classes\":[");
            s.Append(string.Join(",", c)).Append("],\"tiles\":[").Append(string.Join(",", Enumerable.Repeat("0", w * h)));
            s.Append("],\"zones\":[{\"x\":0,\"y\":0,\"w\":" + w + ",\"h\":" + h + "}],\"objects\":[]}");
            return s.ToString();
        }

        static Scene New(string set, string map = null, int seed = 1)
        {
            var w = ZWorld.Create(set, map ?? Plain, seed);
            w.NoGameEnd = true; w.Events.Clear();
            return new Scene { w = w, e = OzV11Kit.Place(w, E, X, Y, Team.Blue) };
        }

        /// <summary>One robot (a squad of one) that stands still and does not fire back; bigHp: it lives through every shot, so the HP removed can be read.</summary>
        static ZObject Bot(Scene s, string type, int cx, int cy, bool bigHp = false, Team team = Team.Red)
        {
            var t = OzV11Kit.Place(s.w, type, cx, cy, team, ZBuildLists.IsRobot(type) ? 1 : -1);
            t.Damage = 0; t.MoveSpeed = 0;
            if (bigHp) { t.MaxHealth = 10000000; t.Health = 10000000; }
            return t;
        }

        /// <summary>An attack nobody ordered (a help call's kind): the plain shot by range, no dash.</summary>
        static void Unordered(Scene s, ZObject t) => s.e.Waypoints.Add(new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY });

        static int Hp(ZObject o) => o.MaxHealth - o.Health;
        static int D(ZObject a, ZObject b) => (int)Math.Sqrt((double)(a.CenterX - b.CenterX) * (a.CenterX - b.CenterX) + (double)(a.CenterY - b.CenterY) * (a.CenterY - b.CenterY));
        static bool Hit(Scene s, ZObject o) => s.All(ZEventType.EvilConeHit).Any(l => l.ev.otherId == o.Id);

        // ------------------------------------------------------------------------------------------------ the cone's numbers
        static void G()
        {
            int t9 = ZEvilCone.TanPermille, t6 = ZEvilCone.DashTanPermille;
            Check(t9 == (int)Math.Round(Math.Tan(9 * Math.PI / 180) * 1000) && t6 == (int)Math.Round(Math.Tan(6 * Math.PI / 180) * 1000) && ZEvilCone.PastPx == 64,
                  $"G1 the cone is the view's pellet cone: tan 9 deg x 1000 = {t9}, tan 6 deg x 1000 = {t6} (a dash shot); the reach 64 px past the target = the pellets' 8 m");
            Check(ZEvilCone.InCone(100, 0, 64, 16, 6, t9) && !ZEvilCone.InCone(100, 0, 64, 17, 6, t9) && ZEvilCone.InCone(100, 0, 64, -16, 6, t9) && !ZEvilCone.InCone(100, 0, 64, -17, 6, t9),
                  "G2 9 deg, a grunt (6 px) 64 px along the line: in at 16 px to either side (64 x 0.158 + 6 = 16.1), out at 17");
            Check(ZEvilCone.InCone(100, 0, 64, 12, 6, t6) && !ZEvilCone.InCone(100, 0, 64, 13, 6, t6), "G3 6 deg (a dash shot): in at 12 px (64 x 0.105 + 6 = 12.7), out at 13");
            Check(ZEvilCone.InCone(0, 100, -16, 64, 6, t9) && !ZEvilCone.InCone(0, 100, -17, 64, 6, t9) && ZEvilCone.InCone(-70, -70, -45, -45, 6, t9) && ZEvilCone.InCone(-70, -70, -50, -40, 6, t9) && !ZEvilCone.InCone(-70, -70, -60, -30, 6, t9),
                  "G4 the same edges on the other axes and on a diagonal");
            Check(!ZEvilCone.InCone(100, 0, -10, 0, 6, t9) && !ZEvilCone.InCone(100, 0, 0, 5, 6, t9) && !ZEvilCone.InCone(0, 0, 50, 0, 6, t9),
                  "G5 never behind him, never beside his own centre, never without an aim");
            Check(ZEvilCone.InCone(300, 40, 130, 20, 12, t9) && ZEvilCone.InCone(-300, 299, -120, 119, 9, t9), "G6 long aims (a building's centre far off) stay in whole numbers");
        }

        // ------------------------------------------------------------------------------------------------ one shot, the cone
        static void C(string set)
        {
            {   // a column of three grunts straight ahead
                var s = New(set); var a = Bot(s, "grunt", X + 40, Y); var b = Bot(s, "grunt", X + 56, Y); var c = Bot(s, "grunt", X + 72, Y);
                Unordered(s, a); long shot = s.FirstShot();
                var cone = s.All(ZEventType.EvilConeHit); var deaths = s.All(ZEventType.Death);
                Check(shot > 0 && s.Count(ZEventType.EvilShot) == 1 && a.Destroyed && b.Destroyed && c.Destroyed && cone.Count == 2 && cone.All(l => l.t == shot && l.ev.flag2 && l.ev.objId == s.e.Id)
                      && deaths.Count == 3 && deaths.All(l => l.t == shot && l.ev.otherId == s.e.Id),
                      $"C1 a column of three grunts at 40 / 56 / 72 px, his shot at the first: all three die in the same tick ({cone.Count} cone hits, {deaths.Count} deaths, killer him)");
                Check(cone.Select(l => l.ev.radius).OrderBy(r => r).SequenceEqual(new[] { 56, 72 }) && cone.All(l => (int)l.ev.value == a.MaxHealth), "C1 the cone events: 56 and 72 px, each the grunt's whole health");
                Check(s.e.Kills == 3 && s.w.EvilConeHits == 2 && s.w.EvilConeKills == 2 && s.w.EvilConeShots == 1 && s.e.EvilConeKills == 2, $"K1 the kills are his: {s.e.Kills} kills, the world's cone numbers 1 shot / 2 hits / 2 kills");
            }
            {   // the switch off: DV161's single hit
                bool was = ZWorld.EvilConeDeviation; ZWorld.EvilConeDeviation = false;
                try
                {
                    var s = New(set); var a = Bot(s, "grunt", X + 40, Y); var b = Bot(s, "grunt", X + 56, Y); var c = Bot(s, "grunt", X + 72, Y);
                    Unordered(s, a); s.FirstShot();
                    Check(a.Destroyed && !b.Destroyed && !c.Destroyed && b.Health == b.MaxHealth && c.Health == c.MaxHealth && s.Count(ZEventType.EvilConeHit) == 0,
                          "C2 the switch off: the same shot kills its target only (DV161 as it was), no cone event");
                }
                finally { ZWorld.EvilConeDeviation = was; }
            }
            {   // abreast: a squad's width in front of him
                var s = New(set); var a = Bot(s, "grunt", X + 48, Y); var l = Bot(s, "grunt", X + 48, Y - 12); var r = Bot(s, "grunt", X + 48, Y + 12);
                Unordered(s, a); s.FirstShot();
                Check(a.Destroyed && l.Destroyed && r.Destroyed, "C3 three grunts abreast at 6 m, 12 px apart (the traffic rules' closest): all three die by one shot");
            }
            {   // the damage by distance: the DV161 table on the robots behind
                var s = New(set); var t = Bot(s, "grunt", X + 64, Y, true);
                var b80 = Bot(s, "grunt", X + 80, Y, true); var b96 = Bot(s, "grunt", X + 96, Y + 3, true); var b112 = Bot(s, "grunt", X + 112, Y - 3, true);
                Unordered(s, t); s.FirstShot();
                int d96 = D(s.e, b96), d112 = D(s.e, b112);
                Check(Hp(t) == 4500 && Hp(b80) == 3472 && Hp(b96) == ZEvilZod.Hit(b96, d96, 120, false) && Hp(b112) == ZEvilZod.Hit(b112, d112, 120, false) && Hp(b96) == 2443 && Hp(b112) == 1415,
                      $"C4 by its own distance (DV161's table): target 64 px {Hp(t)}, 80 px {Hp(b80)}, 96 px {Hp(b96)}, 112 px {Hp(b112)} HP");
            }
            {   // a real red squad walks up to him and stops 7 m in front (his gun held for 9 s by the check, so they arrive and settle first)
                var s = New(set); var sq = OzV11Kit.Place(s.w, "grunt", X + 200, Y, Team.Red, 3);
                foreach (var m in OzV11Kit.Squad(sq)) m.Damage = 0;
                HoldGun(s.e, 900);
                OzV11Kit.PlayerMove(s.w, sq, X + 56, Y, false);
                s.Run(880);
                var mem = OzV11Kit.Squad(sq).ToList();
                string where = string.Join(", ", mem.Select(m => $"({m.CenterX - X}, {m.CenterY - Y})"));
                Unordered(s, mem.OrderBy(m => D(s.e, m)).First());
                s.FirstShot();
                int dead = mem.Count(m => m.Destroyed);
                Info($"C5 a red grunt squad walked up to 7 m in front of him and stood at {where} px from him");
                Check(dead == 3, $"C5 his first shot at the nearest of them kills {dead} of the 3");
            }
            {   // Alan and Brad
                bool plus = ZWorld.HeroPlusDeviation; ZWorld.HeroPlusDeviation = true;
                try
                {
                    var s = New(set); var h = s.w.CreateHeroSquad(ZHeroSquads.AlanBrad, Team.Red, new[] { 28, 25 });
                    var two = OzV11Kit.Squad(h).ToList(); foreach (var m in two) { m.Damage = 0; m.MoveSpeed = 0; }
                    s.Run(30);
                    Unordered(s, two.OrderBy(m => D(s.e, m)).First()); s.FirstShot();
                    Info($"C6 Alan and Brad at {string.Join(", ", two.Select(m => $"{m.HeroMemberName} ({m.CenterX - X}, {m.CenterY - Y}) {m.Health}/{m.MaxHealth}"))}");
                    Check(two.Count == 2 && two.All(m => m.Health < m.MaxHealth), $"C6 Alan and Brad together 7 m in front of him: one shot hits both ({two.Count(m => m.Destroyed)} of 2 killed)");
                }
                finally { ZWorld.HeroPlusDeviation = plus; }
            }
        }

        // ------------------------------------------------------------------------------------------------ what it never hits
        static void N(string set)
        {
            {
                var s = New(set); var t = Bot(s, "grunt", X + 48, Y, true);
                var side = Bot(s, "grunt", X + 30, Y + 30, true);          // 45 deg off his line
                var back = Bot(s, "grunt", X - 30, Y, true);               // behind him
                var edge = Bot(s, "grunt", X + 64, Y + 17, true);          // 1 px outside the 9 deg cone
                var past = Bot(s, "grunt", X + 116, Y - 6, true);          // 4 px past the pellets' reach (116 > 48 + 64), in his range
                var near = Bot(s, "grunt", X + 108, Y + 6, true);          // just inside it (108 px)
                var mate = Bot(s, "grunt", X + 64, Y - 4, true, Team.Blue);
                var tank = Bot(s, "light", X + 80, Y - 20, true);
                Unordered(s, t); s.FirstShot();
                Check(Hp(t) == 4500 && Hp(side) == 0 && Hp(back) == 0 && Hp(edge) == 0 && Hp(past) == 0 && Hp(near) > 0 && Hp(mate) == 0 && Hp(tank) == 0,
                      $"N1 untouched: 45 deg off ({Hp(side)}), behind him ({Hp(back)}), 1 px out of the cone ({Hp(edge)}), 4 px past the reach ({Hp(past)}), his own team ({Hp(mate)}), a light tank in the cone ({Hp(tank)}); hit: 4 px inside the reach ({Hp(near)})");
            }
            {   // out of his range: the target at 100 px, a robot behind it at 124 px
                var s = New(set); var t = Bot(s, "grunt", X + 100, Y, true); var far = Bot(s, "grunt", X + 124, Y, true);
                Unordered(s, t); s.FirstShot();
                Check(Hp(t) > 0 && Hp(far) == 0 && !s.e.WithinAttackRadius(far), $"N2 out of his range (124 px > 120): untouched, the target hit ({Hp(t)} HP)");
            }
            foreach (bool cliff in new[] { false, true })
            {   // a cliff between the target and the robot behind it
                var s = New(set, cliff ? Painted((x, y) => x == 31 && y >= 18 && y <= 32 ? (int)ZClass.Impassable : 0) : Plain);
                var t = Bot(s, "grunt", X + 64, Y, true); var hid = Bot(s, "grunt", X + 112, Y, true);
                Unordered(s, t); s.FirstShot();
                if (cliff) Check(Hp(t) == 4500 && Hp(hid) == 0, $"N3 a cliff between the target (64 px) and a robot at 112 px: it is untouched ({Hp(hid)} HP)");
                else Check(Hp(t) == 4500 && Hp(hid) == 1415, $"N3 the same without the cliff: hit ({Hp(hid)} HP)");
            }
        }

        // ------------------------------------------------------------------------------------------------ the dash shot
        static void Dash(string set)
        {
            var s = New(set); var t = Bot(s, "grunt", X + 160, Y, true); var b = Bot(s, "grunt", X + 174, Y, true); var w = Bot(s, "grunt", X + 172, Y + 15, true);
            OzV11Kit.PlayerAttack(s.w, s.e, t);
            long at = s.FirstShot(400);
            var shot = s.All(ZEventType.EvilShot).FirstOrDefault(); var cone = s.All(ZEventType.EvilConeHit).Where(l => l.t == at).ToList();
            int db = cone.Where(l => l.ev.otherId == b.Id).Select(l => l.ev.radius).FirstOrDefault();
            Check(at > 0 && s.Count(ZEventType.EvilDashEnd) >= 1 && shot.ev.flag && Hp(t) == 4500 && cone.Any(l => l.ev.otherId == b.Id && l.ev.flag) && Hp(b) == ZEvilZod.Hit(b, db, 120, false),
                  $"D1 a dash at a grunt: the dash shot's target the full number ({Hp(t)}), the grunt 14 px behind it by its own distance ({db} px, {Hp(b)} HP), same tick");
            Check(at > 0 && !cone.Any(l => l.ev.otherId == w.Id) && Hp(w) == 0, $"D2 a dash shot's cone is 6 deg: a grunt 15 px beside the line just behind the target is untouched ({Hp(w)} HP)");
        }

        /// <summary>Checks only: his gun waits until tick `until` (the private shot clock, as a plant after a dash holds it).</summary>
        static void HoldGun(ZObject e, long until) =>
            typeof(ZObject).GetField("evilShotTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(e, until);

        // ------------------------------------------------------------------------------------------------ the same world
        static string Print(Scene s) => string.Join(";", s.log.Select(l => $"{l.t}:{l.ev.type}:{l.ev.objId}:{l.ev.otherId}:{l.ev.value}:{l.ev.radius}:{l.ev.flag2}"));

        static void P(string set, Func<string, string> map)
        {
            {
                string one() { var s = New(set); var a = Bot(s, "grunt", X + 40, Y); Bot(s, "grunt", X + 56, Y); Bot(s, "grunt", X + 50, Y - 9); Unordered(s, a); s.Run(400); return Print(s); }
                string p1 = one(), p2 = one();
                Check(p1 == p2 && p1.Contains("EvilConeHit"), "P1 the same scene twice: the same events, ticks and HP");
            }
            if (map == null) return;
            foreach (var lv in new[] { "L02", "L07" })
            {
                string run(bool on)
                {
                    bool was = ZWorld.EvilConeDeviation; ZWorld.EvilConeDeviation = on;
                    try
                    {
                        var w = ZWorld.Create(set, map(lv), 3); w.Bots.Add(new ZBot(w, Team.Red, 3)); w.Bots.Add(new ZBot(w, Team.Blue, 104));
                        var p = new StringBuilder(); ulong h = 1469598103934665603UL;
                        for (int i = 1; i <= 6000; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (i % 100 == 0) h = w.RulesFingerprint(h); if (i % 1000 == 0) p.Append(' ').Append(h.ToString("x16")); }
                        return p.ToString();
                    }
                    finally { ZWorld.EvilConeDeviation = was; }
                }
                string off = run(false), on = run(true);
                Check(off == on && off.Length > 0, $"P2 {lv}: both computers 60 s, no EvilZod in the level: the switch off and on give the same world every 10 s");
            }
        }

        /// <summary>The whole check. Runs under OzV11Kit.GameSwitches.</summary>
        public static string Run(string settings, Func<string, string> map)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.EvilZodDeviation, cone = ZWorld.EvilConeDeviation;
            ZWorld.EvilZodDeviation = true; ZWorld.EvilConeDeviation = true;
            try { G(); C(settings); N(settings); Dash(settings); P(settings, map); }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " at " + (ex.StackTrace ?? "").Split('\n').FirstOrDefault()); }
            finally { ZWorld.EvilZodDeviation = was; ZWorld.EvilConeDeviation = cone; }
            sb.Append("[OpenZ V1.3 EvilZod cone] ").Append(fail == 0 ? "PASS" : "FAIL").Append(' ').Append(pass).Append('/').Append(pass + fail).AppendLine();
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: the cone's numbers and the column of three grunts (all three fall; the switch off: one).</summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.EvilZodDeviation, cone = ZWorld.EvilConeDeviation;
            ZWorld.EvilZodDeviation = true; ZWorld.EvilConeDeviation = true;
            try { G(); C1Only(settings); }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message); }
            finally { ZWorld.EvilZodDeviation = was; ZWorld.EvilConeDeviation = cone; }
            return sb.ToString();
        }

        static void C1Only(string set)
        {
            var s = New(set); var a = Bot(s, "grunt", X + 40, Y); var b = Bot(s, "grunt", X + 56, Y); var c = Bot(s, "grunt", X + 72, Y);
            Unordered(s, a); long shot = s.FirstShot();
            Check(shot > 0 && a.Destroyed && b.Destroyed && c.Destroyed && s.Count(ZEventType.EvilConeHit) == 2, "Q1 a column of three grunts at 40 / 56 / 72 px: one shot, all three fall");
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/V1.3/EvilZod: Cone Count (headless, DV167)")]
        static void Menu() => OzV11Kit.RunMenu("EvilZod: cone count (DV167)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));
#endif
    }
}
