using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Sim/ZEvilZod.Face.cs, headless on the Sim alone, nothing saved: menu <see cref="MenuPath"/>, and off-project on plain .NET (the kit's tools/harness).
    /// Every shot of every EvilZod in every scene is logged: his heading, the target's bearing, the angle between, the ticks since that target was picked and since
    /// he last moved, the ticks the shot waited for his turn. The scenes (each one with the rule off = the 1.4 rules before the kit, then on):
    ///  guard     DV165's last defense at his spot by the blue fort, enemy squads from the front, both sides and behind;
    ///  appear    the retry help in front of the blue fort on L02, L07, L16, L17, red squads from every side;
    ///  autodash  1.4's auto dash at squads that outrange him, then his shots at the rest of the squad;
    ///  switch    rings of targets round him that die one per shot (the next one somewhere else), the player switching his target between far ones;
    ///  behind    he walks, stops, an enemy comes into range behind him; he fights one, a second one walks up behind him;
    ///  moving    squads that walk past him, round him;
    ///  stop      a walk under an order, enemies to the side, he fires once he stands;
    ///  order     the player's attack orders on targets beside and behind him (the dash cooling down: he walks, stops, fires; the dash ready: the dash shot);
    ///  red       the same scenes with EvilZod on the red side;
    ///  melee     random squads round him, random orders, 24 seeds (System.Random, outside the rules);
    ///  battle    L02, L07, L16, L17 with both computers and an EvilZod (the retry help) on the blue or the red side, 6 minutes each.
    /// Verdicts (rule on): no shot more than ZEvilFace.TolCenti / 100 deg off in any scene, at least MinPerScene shots in each and MinShots in all; every held
    /// shot waited no longer than its turn; the cycle between two shots unchanged; stationary targets: the same kills with the same hit points as with the rule off;
    /// the same scene twice the same; a level without him the same with the rule on and off; (fix round 1, F9) the rules' own note of the angle at each shot,
    /// which the Play check reads, within the tolerance at every shot. Rule off: the old fault counted (INFO lines with the numbers).
    /// </summary>
    public static class OzV14EvilFaceCount
    {
        public const string MenuPath = "OpenZ/V1.4/EvilZod: Face Count (headless, DV174)";
        public const int MinShots = 500, MinPerScene = 10;
        static int pass, fail; static StringBuilder sb; static string Set; static Func<string, string> Map;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        const string E = ZEvilZod.Type;

        /// <summary>One shot of his: the scene, the tick, who fired, at whom, his heading, the bearing, the angle (all hundredths of a degree), the ticks since the
        /// target was picked and since he last moved, the ticks it waited for the turn, the HP it took, the range (px), a dash shot; note = the rules' own note of the
        /// angle at that shot (ZObject.EvilFaceShotOff, fix round 1: what the Play check reads; -1 = no note).</summary>
        public sealed class Shot { public string scene; public long t; public int id, target, face, bearing, err, wait, hp, d, note = -1; public long sincePick, sinceMove; public bool dash; public Team team; }

        sealed class Track { public int target = -1; public long pick = -1, moved = -1; public int x, y; public readonly List<(long t, int face)> faces = new List<(long, int)>(); }

        sealed class Scene
        {
            public ZWorld w; public ZObject e; public string name;
            public readonly List<Shot> shots = new List<Shot>();
            readonly Dictionary<int, Track> tr = new Dictionary<int, Track>();
            public void Run(int ticks, Func<bool> stop = null)
            {
                for (int i = 0; i < ticks; i++)
                {
                    w.Tick(ZConst.TICK);
                    long now = w.TickIndex;
                    foreach (var x in w.Events)                                                 // first, against what was known before this tick: the target he held, his last step
                    {
                        if (x.type != ZEventType.EvilShot) continue;
                        var o = w.GetAny(x.objId); if (o == null || !o.IsEvilZod) continue;
                        tr.TryGetValue(o.Id, out var k);
                        int b = ZEvilFace.Bearing(x.x - o.CenterX, x.y - o.CenterY);
                        shots.Add(new Shot
                        {
                            scene = name, t = now, id = o.Id, target = x.otherId, face = o.EvilFacing, bearing = b, err = b < 0 ? 0 : Math.Abs(ZEvilFace.Diff(o.EvilFacing, b)),
                            wait = o.EvilFaceShotWaitTicks, hp = (int)x.value, d = x.radius, dash = x.flag, team = o.Owner, note = o.EvilFaceShotOff,
                            sincePick = k != null && k.target == x.otherId && k.pick >= 0 ? now - k.pick : 0,   // 0: picked in this very tick
                            sinceMove = k != null && k.moved >= 0 ? now - k.moved : now,
                        });
                    }
                    foreach (var o in w.Objects)
                    {
                        if (!o.IsEvilZod || o.Destroyed) continue;
                        if (!tr.TryGetValue(o.Id, out var k)) { k = new Track { x = o.CenterX, y = o.CenterY }; tr[o.Id] = k; }
                        int at = o.AttackObject != null && !o.AttackObject.Destroyed ? o.AttackObject.Id : -1;
                        if (at != k.target) { k.target = at; k.pick = now; }
                        if (o.CenterX != k.x || o.CenterY != k.y) { k.moved = now; k.x = o.CenterX; k.y = o.CenterY; }
                        k.faces.Add((now, o.EvilFacing)); if (k.faces.Count > 64) k.faces.RemoveAt(0);
                    }
                    w.Events.Clear();
                    if (stop != null && stop()) return;
                }
            }
            /// <summary>His heading at tick t (the value after that tick), -1 when not kept.</summary>
            public int FaceAt(int id, long t) { if (!tr.TryGetValue(id, out var k)) return -1; foreach (var f in k.faces) if (f.t == t) return f.face; return -1; }
        }

        static string Plain => OzV11Kit.Plain(200, 120);

        static Scene New(string name, int cx = 800, int cy = 900, Team team = Team.Blue, int seed = 1, string map = null)
        {
            var w = ZWorld.Create(Set, map ?? Plain, seed);
            w.NoGameEnd = true; w.Events.Clear();
            return new Scene { w = w, name = name, e = OzV11Kit.Place(w, E, cx, cy, team) };
        }

        static Team Foe(Team t) => t == Team.Blue ? Team.Red : Team.Blue;

        /// <summary>A squad of n of the other side at (cx, cy): its range, standing still, no damage, never dying, its attack order on him as asked.</summary>
        static List<ZObject> Squad(Scene s, string type, int cx, int cy, int n, Team team, int range = 0, bool still = false, bool harmless = true, bool tough = true, bool attack = true)
        {
            var lead = OzV11Kit.Place(s.w, type, cx, cy, team, n);
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

        /// <summary>A target of the other side that stands and does not fire: dies to one close shot unless bigHp.</summary>
        static ZObject Dummy(Scene s, int cx, int cy, Team team, bool bigHp = false)
        {
            var t = OzV11Kit.Place(s.w, "grunt", cx, cy, team, 1);
            t.Damage = 0; t.MoveSpeed = 0;
            if (bigHp) { t.MaxHealth = 10000000; t.Health = 10000000; }
            return t;
        }

        /// <summary>A point r px from (cx, cy) at deg degrees on the map (0 = east, 90 = south). A check's own float-free placement: whole px from a table of 16 directions.</summary>
        static void At(int cx, int cy, int r, int dir16, out int x, out int y)
        {
            int[] c = { 1000, 924, 707, 383, 0, -383, -707, -924, -1000, -924, -707, -383, 0, 383, 707, 924 };
            int k = ((dir16 % 16) + 16) % 16;
            x = cx + r * c[k] / 1000; y = cy + r * c[(k + 12) % 16] / 1000;
        }

        // ------------------------------------------------------------------------------------------------ the scenes
        static IEnumerable<Scene> Scenes(Team team)
        {
            string tag = team == Team.Blue ? "" : "red ";
            Team foe = Foe(team);
            // switch: a ring of dummies 50 px round him that each die to one shot; the creation order skips round the ring, so the next one is somewhere else
            foreach (int start in new[] { 0, 5, 11 })
            {
                var s = New(team == Team.Blue ? "switch" : "red", team: team);
                foreach (int k in new[] { 0, 7, 3, 12, 9, 1, 14, 5, 10, 2, 15, 6 }) { At(s.e.CenterX, s.e.CenterY, 50, k + start, out int x, out int y); Dummy(s, x, y, foe); }
                s.Run(3200);
                yield return s;
            }
            {   // switch: the player switches his target between far ones (big HP) every 3 s
                var s = New(team == Team.Blue ? "switch" : "red", team: team);
                var ts = new List<ZObject>();
                foreach (int k in new[] { 0, 6, 11, 3, 9, 14 }) { At(s.e.CenterX, s.e.CenterY, 100, k, out int x, out int y); ts.Add(Dummy(s, x, y, foe, true)); }
                s.e.Veteran = false;
                for (int i = 0; i < 12; i++) { OzV11Kit.PlayerAttack(s.w, s.e, ts[i % ts.Count]); s.Run(300); }
                yield return s;
            }
            // behind: he walks east, stops, an enemy comes into range behind him (west)
            foreach (int dir in new[] { 0, 4, 8, 12 })
            {
                var s = New(team == Team.Blue ? "behind" : "red", team: team);
                At(s.e.CenterX, s.e.CenterY, 120, dir, out int gx, out int gy);
                OzV11Kit.PlayerMove(s.w, s.e, gx, gy, false);
                s.Run(2500, () => !s.e.IsMoving && s.e.Waypoints.Count == 0 && s.w.TickIndex > 50);
                s.Run(30);
                At(s.e.CenterX, s.e.CenterY, 200, dir + 8, out int ex, out int ey);
                var sq = Squad(s, "grunt", ex, ey, 1, foe, still: false, attack: false);
                At(s.e.CenterX, s.e.CenterY, 90, dir + 8, out int mx, out int my);
                OzV11Kit.PlayerMove(s.w, sq[0], mx, my, false);
                s.Run(1400);
                yield return s;
            }
            {   // behind: he fights one in front, a second walks up behind him and the first one dies
                var s = New(team == Team.Blue ? "behind" : "red", team: team);
                var a = Dummy(s, s.e.CenterX + 100, s.e.CenterY, foe, true);
                s.Run(500);
                var b = Squad(s, "grunt", s.e.CenterX - 220, s.e.CenterY + 10, 1, foe, attack: false);
                OzV11Kit.PlayerMove(s.w, b[0], s.e.CenterX - 90, s.e.CenterY + 10, false);
                s.Run(600);
                a.SetHealth(0); s.w.UpdateObjectHealth(a);
                s.Run(1200);
                yield return s;
            }
            // moving: a squad that walks past him, one that walks round him
            foreach (int dir in new[] { 2, 10 })
            {
                var s = New(team == Team.Blue ? "moving" : "red", team: team);
                At(s.e.CenterX, s.e.CenterY, 220, dir, out int ax, out int ay); At(s.e.CenterX, s.e.CenterY, 220, dir + 8, out int bx, out int by);
                var sq = Squad(s, "grunt", ax + 40, ay, 3, foe, attack: false);
                for (int i = 0; i < 4; i++) { OzV11Kit.PlayerMove(s.w, sq[0], i % 2 == 0 ? bx + 40 : ax + 40, i % 2 == 0 ? by : ay, false); s.Run(1100); }
                yield return s;
            }
            {
                var s = New(team == Team.Blue ? "moving" : "red", team: team);
                var sq = Squad(s, "grunt", s.e.CenterX + 100, s.e.CenterY, 3, foe, attack: false);
                for (int i = 0; i < 16; i++) { At(s.e.CenterX, s.e.CenterY, 105, (i + 1) * 2, out int x, out int y); OzV11Kit.PlayerMove(s.w, sq[0], x, y, false); s.Run(260); }
                yield return s;
            }
            // stop: he walks under an order past enemies to the side, fires once he stands
            foreach (int side in new[] { 4, 12, 2, 14 })
            {
                var s = New(team == Team.Blue ? "stop" : "red", team: team);
                At(s.e.CenterX, s.e.CenterY, 100, side, out int ex, out int ey);
                Squad(s, "grunt", ex + 60, ey, 3, foe, still: true, attack: false);
                OzV11Kit.PlayerMove(s.w, s.e, s.e.CenterX + 150, s.e.CenterY, false);
                s.Run(2400);
                OzV11Kit.PlayerMove(s.w, s.e, s.e.CenterX - 150, s.e.CenterY, false);   // and back past them: he stands again, they are now on his other side
                s.Run(2400);
                yield return s;
            }
            // order: the player's attack orders on targets beside and behind him; the dash cooling down (he walks, stops, fires) and ready (the dash shot)
            foreach (bool ready in new[] { false, true })
            {
                var s = New(team == Team.Blue ? "order" : "red", team: team);
                var ts = new List<ZObject>();
                foreach (int k in new[] { 8, 4, 12, 6, 14 }) { At(s.e.CenterX, s.e.CenterY, 170, k, out int x, out int y); ts.Add(Dummy(s, x, y, foe, !ready)); }
                foreach (var t in ts)
                {
                    if (!OzV11Kit.Alive(t)) continue;
                    if (!ready && s.e.EvilDashReadyTick == 0) { var far = Dummy(s, s.e.CenterX, s.e.CenterY - 150, foe); OzV11Kit.PlayerAttack(s.w, s.e, far); s.Run(400); }   // spend the dash first
                    OzV11Kit.PlayerAttack(s.w, s.e, t); s.Run(ready ? 900 : 1300);
                }
                yield return s;
            }
            // autodash: a squad that outranges him (160 px) fires at him from 150 px, the dash ready: he dashes by himself, then fires at the rest
            foreach (int dir in new[] { 0, 5, 8, 13, 3, 10 })
            {
                var s = New(team == Team.Blue ? "autodash" : "red", team: team);
                At(s.e.CenterX, s.e.CenterY, 150, dir, out int x, out int y);
                var sq = Squad(s, "grunt", x, y, 3, foe, range: 160, still: true, harmless: false, tough: false);
                for (int i = 1; i < sq.Count; i++) { sq[i].MaxHealth = 10000000; sq[i].Health = 10000000; }   // the dash shot kills the one it lands at; he fires on at the others
                s.Run(2500);
                yield return s;
            }
        }

        /// <summary></summary>
        static IEnumerable<Scene> Guard()
        {
            string plain = OzV11Kit.Plain(80, 120, ("building:fort_front", 30, 8, "blue"), ("building:fort_back", 30, 100, "red"));
            var w = ZWorld.Create(Set, plain, 1); w.NoGameEnd = true; w.Events.Clear(); w.EvilLastDefenseTeam = Team.Blue;
            while (w.TickIndex < ZEvilAppear.LastDefenseTicks + 3000) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            var ez = w.Objects.FirstOrDefault(o => o.IsEvilZod && OzV11Kit.Alive(o));
            if (ez == null || !ez.EvilGuarding) { Check(false, "guard: the last defense did not come out on the plain map"); yield break; }
            var s = new Scene { w = w, e = ez, name = "guard" };
            int gx = ez.EvilGuardX, gy = ez.EvilGuardY;
            foreach (int dir in new[] { 4, 0, 8, 12, 2, 10 })
            {
                At(gx, gy, 130, dir, out int x, out int y);
                if (!w.Path.TilePassable(x >> 4, y >> 4, true)) continue;
                var sq = Squad(s, "grunt", x, y, 2, Team.Red, still: true, attack: false);
                s.Run(900);
                foreach (var r in sq) if (OzV11Kit.Alive(r)) { r.SetHealth(0); w.UpdateObjectHealth(r); }
                s.Run(200);
            }
            yield return s;
        }

        /// <summary>appear: the retry help in front of the blue fort, red squads from every side walk up to him.</summary>
        static IEnumerable<Scene> Appear()
        {
            foreach (var lv in new[] { "L02", "L07", "L16", "L17" })
            {
                var w = OzV13EvilAppearCount.Level(Set, Map, lv, 2, false, false);
                w.NoGameEnd = true;
                var e = w.AddEvilRetryHelp(Team.Blue);
                if (e == null) { Check(false, "appear " + lv + ": no retry help"); continue; }
                var s = new Scene { w = w, e = e, name = "appear" };
                foreach (int dir in new[] { 0, 4, 8, 12, 2, 6, 10, 14 })
                {
                    At(e.CenterX, e.CenterY, 120, dir, out int x, out int y);
                    if (x < 32 || y < 32 || !w.Path.TilePassable(x >> 4, y >> 4, true)) continue;
                    var sq = Squad(s, "grunt", x, y, 2, Team.Red, attack: true);
                    s.Run(900);
                    foreach (var r in sq) if (OzV11Kit.Alive(r)) { r.SetHealth(0); w.UpdateObjectHealth(r); }
                    s.Run(100);
                }
                yield return s;
            }
        }

        /// <summary>melee: random squads round him at random bearings and ranges, random walks of theirs and his; System.Random(seed), outside the rules.</summary>
        static IEnumerable<Scene> Melee(int seeds)
        {
            for (int seed = 1; seed <= seeds; seed++)
            {
                var rnd = new Random(seed * 7919);
                Team team = seed % 3 == 0 ? Team.Red : Team.Blue, foe = Foe(team);
                var s = New("melee", team: team, seed: seed);
                var squads = new List<List<ZObject>>();
                int n = 2 + rnd.Next(3);
                for (int i = 0; i < n; i++)
                {
                    At(s.e.CenterX, s.e.CenterY, 70 + rnd.Next(150), rnd.Next(16), out int x, out int y);
                    squads.Add(Squad(s, rnd.Next(4) == 0 ? "psycho" : "grunt", x, y, 1 + rnd.Next(3), foe, still: rnd.Next(3) == 0, tough: rnd.Next(2) == 0, attack: rnd.Next(2) == 0));
                }
                for (int step = 0; step < 6; step++)
                {
                    int what = rnd.Next(4);
                    if (what == 0) { At(s.e.CenterX, s.e.CenterY, 40 + rnd.Next(100), rnd.Next(16), out int x, out int y); OzV11Kit.PlayerMove(s.w, s.e, x, y, rnd.Next(2) == 0); }
                    else if (what == 1) { var sq = squads[rnd.Next(squads.Count)]; if (OzV11Kit.Alive(sq[0])) { At(s.e.CenterX, s.e.CenterY, 60 + rnd.Next(120), rnd.Next(16), out int x, out int y); OzV11Kit.PlayerMove(s.w, sq[0], x, y, false); } }
                    else if (what == 2) { var sq = squads[rnd.Next(squads.Count)]; var t = sq.FirstOrDefault(OzV11Kit.Alive); if (t != null) OzV11Kit.PlayerAttack(s.w, s.e, t); }
                    s.Run(400 + rnd.Next(400));
                }
                yield return s;
            }
        }

        /// <summary>battle: a level with both computers and an EvilZod on each side (the retry help), as a played match.</summary>
        static IEnumerable<Scene> Battle(int seconds)
        {
            foreach (var (lv, seed, team) in new[] { ("L02", 1, Team.Blue), ("L16", 2, Team.Red), ("L07", 3, Team.Blue), ("L17", 4, Team.Red) })
            {
                var w = OzV13EvilAppearCount.Level(Set, Map, lv, seed, true, true);
                var b = w.AddEvilRetryHelp(team);                                              // the retry help (once a world), in front of that side's fort
                if (b == null) { Check(false, "battle " + lv + ": no EvilZod made"); continue; }
                var s = new Scene { w = w, e = b, name = "battle" };
                s.Run(seconds * 100);
                yield return s;
            }
        }

        // ------------------------------------------------------------------------------------------------ the report
        static readonly int[] Bins = { 200, 500, 1000, 2000, 4500, 9000, 13500, 18001 };
        static string Hist(IEnumerable<Shot> shots)
        {
            var c = new int[Bins.Length]; int lo = 0;
            foreach (var s in shots) for (int i = 0; i < Bins.Length; i++) if (s.err < Bins[i]) { c[i]++; break; }
            var b = new StringBuilder();
            for (int i = 0; i < Bins.Length; i++) { b.Append(i == 0 ? "" : ", ").Append(lo / 100).Append('-').Append(Math.Min(180, Bins[i] / 100)).Append(" deg ").Append(c[i]); lo = Bins[i]; }
            return b.ToString();
        }

        static string Line(Shot s) => $"{s.scene} t{s.t} #{s.id} ({s.team}) at #{s.target} {s.d} px{(s.dash ? " dash" : "")}: heading {s.face / 100.0:0.0}, bearing {s.bearing / 100.0:0.0}, off {s.err / 100.0:0.0} deg; picked {s.sincePick} ticks before, moved {s.sinceMove} ticks before; waited {s.wait} ticks";

        /// <summary>Every scene once with the rule off and once on.</summary>
        static void All(bool full, out List<Shot> off, out List<Shot> on, out List<(Scene a, Scene b)> pairs)
        {
            off = new List<Shot>(); on = new List<Shot>(); pairs = new List<(Scene, Scene)>();
            bool was = ZWorld.EvilFaceFireDeviation;
            try
            {
                foreach (bool rule in new[] { false, true })
                {
                    ZWorld.EvilFaceFireDeviation = rule;
                    var list = new List<Scene>();
                    list.AddRange(Scenes(Team.Blue)); list.AddRange(Scenes(Team.Red));
                    if (full) { list.AddRange(Guard()); list.AddRange(Appear()); }
                    list.AddRange(Melee(full ? 24 : 6));
                    if (full) list.AddRange(Battle(360));
                    foreach (var s in list) (rule ? on : off).AddRange(s.shots);
                    if (!rule) foreach (var s in list) pairs.Add((s, null));
                    else for (int i = 0; i < list.Count && i < pairs.Count; i++) pairs[i] = (pairs[i].a, list[i]);
                }
            }
            finally { ZWorld.EvilFaceFireDeviation = was; }
        }

        static void Verdicts(bool full)
        {
            All(full, out var off, out var on, out var pairs);
            var names = (full ? new[] { "guard", "appear" } : new string[0]).Concat(new[] { "autodash", "switch", "behind", "moving", "stop", "order", "red", "melee" }).Concat(full ? new[] { "battle" } : new string[0]).ToArray();
            // before
            var badOff = off.Where(x => x.err > ZEvilFace.TolCenti).ToList();
            Info($"rule OFF (the 1.4 rules before this kit): {off.Count} shots, {badOff.Count} more than {ZEvilFace.TolCenti / 100} deg off his heading, the worst {(off.Count > 0 ? off.Max(x => x.err) / 100.0 : 0):0.0} deg; {Hist(off)}");
            foreach (var n in names) { var a = off.Where(x => x.scene == n).ToList(); Info($"  off {n}: {a.Count} shots, {a.Count(x => x.err > ZEvilFace.TolCenti)} over {ZEvilFace.TolCenti / 100} deg, worst {(a.Count > 0 ? a.Max(x => x.err) / 100.0 : 0):0.0} deg"); }
            if (badOff.Count > 0)
            {
                Info($"  off, the shots over {ZEvilFace.TolCenti / 100} deg: picked that tick {badOff.Count(x => x.sincePick == 0)}, 1-10 ticks before {badOff.Count(x => x.sincePick >= 1 && x.sincePick <= 10)}, later {badOff.Count(x => x.sincePick > 10)}; "
                     + $"standing 0-30 ticks {badOff.Count(x => x.sinceMove <= 30)}, longer {badOff.Count(x => x.sinceMove > 30)}");
                foreach (var x in badOff.OrderByDescending(x => x.err).Take(full ? 400 : 25)) Info("  OVER " + Line(x));
            }
            Check(badOff.Count > 0, $"the fault reproduced with the rule off: {badOff.Count} of {off.Count} shots left more than {ZEvilFace.TolCenti / 100} deg off his heading");
            // after
            var badOn = on.Where(x => x.err > ZEvilFace.TolCenti).ToList();
            Info($"rule ON (DV174): {on.Count} shots, {badOn.Count} more than {ZEvilFace.TolCenti / 100} deg off, the worst {(on.Count > 0 ? on.Max(x => x.err) / 100.0 : 0):0.0} deg; {Hist(on)}");
            foreach (var x in badOn.Take(25)) Info("  OVER " + Line(x));
            foreach (var n in names)
            {
                var a = on.Where(x => x.scene == n).ToList();
                Check(a.Count >= MinPerScene && a.All(x => x.err <= ZEvilFace.TolCenti),
                      $"F1 {n}: {a.Count} shots (at least {MinPerScene}), {a.Count(x => x.err > ZEvilFace.TolCenti)} more than {ZEvilFace.TolCenti / 100} deg off his heading (the worst {(a.Count > 0 ? a.Max(x => x.err) / 100.0 : 0):0.0} deg), {a.Count(x => x.wait > 0)} waited for the turn");
            }
            Check(on.Count >= MinShots && badOn.Count == 0, $"F2 every scene: {on.Count} shots (at least {MinShots}), {badOn.Count} more than {ZEvilFace.TolCenti / 100} deg off his heading");
            // fix round 1: the rules' own note at the shot (ZObject.EvilFaceShotOff, what the Play check reads in a level): over the tolerance with the rule off where the
            // check measured it, never with it on; the note against this check's own angle (the target's spot of the event, his spot after the tick)
            int noteOn = on.Count(x => x.note >= 0), noteOnBad = on.Count(x => x.note > ZEvilFace.TolCenti), noteOff = badOff.Count(x => x.note > ZEvilFace.TolCenti);
            int noteNear = on.Concat(off).Count(x => x.note >= 0 && Math.Abs(x.note - x.err) <= 100);
            Check(noteOn >= MinShots && noteOn == on.Count && noteOnBad == 0 && noteOff > 0,
                  $"F9 the rules' own note at each shot (the Play check's rows): rule on {noteOn} of {on.Count} shots noted (at least {MinShots}), {noteOnBad} over {ZEvilFace.TolCenti / 100} deg; "
                  + $"rule off {noteOff} of the {badOff.Count} shots measured over it noted over it too; within 1 deg of this check's angle in {noteNear} of {on.Count + off.Count}");
            // the turn itself: a held shot waits no longer than the turn from where he faced when it was ready (+1 tick: the turn comes at the start of a tick)
            int worst = 0; Shot worstShot = null; int held = 0;
            foreach (var p in pairs)
            {
                var sc = p.b; if (sc == null) continue;
                foreach (var x in sc.shots.Where(x => x.wait > 0))
                {
                    held++;
                    int f0 = sc.FaceAt(x.id, x.t - x.wait - 1); if (f0 < 0) continue;
                    int need = Math.Max(0, (Math.Abs(ZEvilFace.Diff(f0, x.bearing)) - ZEvilFace.TolCenti + ZEvilFace.TurnCentiPerTick - 1) / ZEvilFace.TurnCentiPerTick);
                    int extra = x.wait - need;
                    if (extra > worst) { worst = extra; worstShot = x; }
                }
            }
            var turns = on.Where(x => x.wait > 0).Select(x => x.wait).DefaultIfEmpty(0).ToList();
            Check(held > 0 && worst <= 2, $"F3 the turn itself: {held} shots waited for his turn, {turns.Max()} ticks at most ({turns.Max() * ZConst.TICK:0.00} s; 180 deg = {(18000 - ZEvilFace.TolCenti + ZEvilFace.TurnCentiPerTick - 1) / ZEvilFace.TurnCentiPerTick} ticks), "
                  + $"none longer than its turn from where he faced + {worst} ticks (a walking target){(worstShot != null && worst > 0 ? ": " + Line(worstShot) : "")}");
            // the cycle: two shots of his in a row, the second not held: CycleTicks apart (the reload starts at the shot, held or not)
            int pairsN = 0, wrong = 0; string wrongLine = "";
            foreach (var p in pairs)
            {
                var sc = p.b; if (sc == null) continue;
                foreach (var g in sc.shots.GroupBy(x => x.id))
                {
                    var l = g.ToList();
                    for (int i = 1; i < l.Count; i++)
                    {
                        if (l[i].wait > 0 || l[i].target != l[i - 1].target || l[i - 1].dash || l[i].sincePick < l[i].t - l[i - 1].t) continue;   // one engagement: the same target held since before the shot before
                        pairsN++; if (l[i].t - l[i - 1].t != ZEvilZod.CycleTicks) { wrong++; wrongLine = Line(l[i]) + $" ({l[i].t - l[i - 1].t} ticks after the one before)"; }
                    }
                }
            }
            Check(pairsN >= 50 && wrong == 0, $"F4 the fire rate: {pairsN} pairs of shots in a row at one target, the second not held: {pairsN - wrong} exactly {ZEvilZod.CycleTicks} ticks apart{(wrong > 0 ? ", " + wrong + " not: " + wrongLine : "")}");
            // stationary rings: the same kills with the same HP, rule on and off; only the ticks move
            int same = 0, rings = 0;
            foreach (var p in pairs)
            {
                if (p.a == null || p.b == null || p.a.name != "switch" || p.a.shots.Count == 0 || p.a.shots.Any(x => x.d > 60)) continue;
                rings++;
                string A = string.Join(";", p.a.shots.Select(x => x.target + ":" + x.hp + ":" + x.d)), B = string.Join(";", p.b.shots.Select(x => x.target + ":" + x.hp + ":" + x.d));
                if (A == B) same++;
            }
            Check(rings >= 3 && same == rings, $"F5 the rings of standing targets: {same} of {rings} scenes the same targets, in the same order, the same HP and range per shot with the rule on and off (only the ticks moved)");
        }

        static void Determinism()
        {
            string Once()
            {
                var s = New("det", seed: 9);
                foreach (int k in new[] { 0, 7, 3, 12, 9, 1 }) { At(s.e.CenterX, s.e.CenterY, 50, k, out int x, out int y); Dummy(s, x, y, Team.Red); }
                Squad(s, "grunt", s.e.CenterX - 120, s.e.CenterY + 80, 3, Team.Red, harmless: false, tough: false);
                s.Run(1500);
                return string.Join(";", s.shots.Select(x => x.t + "," + x.target + "," + x.face + "," + x.hp)) + "|" + s.w.Rng.Next(1000000);
            }
            string a = Once(), b = Once();
            Check(a == b && a.Length > 20, "F6 the same scene twice: the same shots, ticks, headings and random stream");
            bool was = ZWorld.EvilFaceFireDeviation;
            try
            {
                ZWorld.EvilFaceFireDeviation = false; string off = OzV13EvilZodCount.LevelPrint(Set, Map("L02"), 1, 30);
                ZWorld.EvilFaceFireDeviation = true; string on = OzV13EvilZodCount.LevelPrint(Set, Map("L02"), 1, 30);
                Check(off == on && off.Length > 0, "F7 L02: 30 s with both computers, no EvilZod: the object print the same with the rule on and off");
            }
            finally { ZWorld.EvilFaceFireDeviation = was; }
            int bad = 0;
            for (int dx = -40; dx <= 40; dx++) for (int dy = -40; dy <= 40; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    double tr = Math.Atan2(dy, dx) * 18000.0 / Math.PI; if (tr < 0) tr += 36000;
                    double e = Math.Abs(ZEvilFace.Diff(ZEvilFace.Bearing(dx, dy), (int)Math.Round(tr) % 36000));
                    if (e > 3) bad++;
                }
            Check(bad == 0 && ZEvilFace.Bearing(1, 0) == 0 && ZEvilFace.Bearing(0, 1) == 9000 && ZEvilFace.Bearing(-1, 0) == 18000 && ZEvilFace.Bearing(0, -1) == 27000 && ZEvilFace.Diff(35000, 1000) == 2000 && ZEvilFace.Diff(1000, 35000) == -2000,
                  $"F8 the whole-number bearing: every step of a 81 x 81 px square within 0.03 deg of the true angle ({bad} not); east 0, south 90, west 180, north 270; the shortest turn across north");
        }

        static string Wrap(Action body)
        {
            pass = fail = 0; sb = new StringBuilder();
            bool was = ZWorld.EvilZodDeviation, auto = ZWorld.EvilAutoDashDeviation, face = ZWorld.EvilFaceFireDeviation;
            ZWorld.EvilZodDeviation = true; ZWorld.EvilAutoDashDeviation = true; ZWorld.EvilFaceFireDeviation = true;   // as View/ZGame.cs: EvilZod on outside 'Zod exact'; the 1.4 switches at their defaults
            try { body(); }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " at " + (ex.StackTrace ?? "").Split('\n').FirstOrDefault()); }
            finally { ZWorld.EvilZodDeviation = was; ZWorld.EvilAutoDashDeviation = auto; ZWorld.EvilFaceFireDeviation = face; }
            return sb.ToString();
        }

        /// <summary>The whole check (every scene, the battles). Runs under OzV11Kit.GameSwitches.</summary>
        public static string Run(string settings, Func<string, string> map)
        {
            Set = settings; Map = map;
            string r = Wrap(() => { Verdicts(true); Determinism(); });
            return r + "[OpenZ V1.4 EvilZod face] " + (fail == 0 ? "PASS" : "FAIL") + " " + pass + "/" + (pass + fail) + "\n";
        }

        /// <summary>The Deviations row's probe (DV174): the scenes on the plain map (no level: no guard, no retry help, no battle), 6 melee seeds.</summary>
        public static string Quick(string settings, Func<string, string> map)
        {
            Set = settings; Map = map;
            return Wrap(() => Verdicts(false));
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem(MenuPath)]
        static void Menu() => OzV11Kit.RunMenu("EvilZod: face count (1.4, DV174)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));
#endif
    }
}
