using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.4 speed-up 5, the counted check of the rules' neighbour grid (Sim/ZWorld.NeighbourGrid.cs): the same rules, bit for bit.
    /// Headless: no scene, no Play mode, nothing saved. It sets the rule switches a level start sets (View/ZGame.cs with the default
    /// options, both sides computers) itself and puts every switch back after, so it needs no Play start first.
    /// <list type="bullet">
    /// <item>G1-G6 the grid alone, on the Unit editor's map: a crowd of 300 units placed, walked by the rules, jumped by SetCords, put
    /// off the map, made and deleted (also by a check that edits Objects itself); after each step 400 questions at random points with
    /// every reach from -1 to 900 px, each answer compared with a walk of Objects (the same units, the same order).</item>
    /// <item>S1 whole computer matches (the levels, seeds, minutes asked), three runs each: the grid on with ZWorld.NbVerify (every
    /// question also compared with a walk of Objects: 0 differences), the grid off (the old loops), the grid on. The world's fingerprint
    /// after EVERY tick must be identical in all three from the first tick to the last, and the match must end the same.</item>
    /// <item>S2 the Unit editor's fight (the cap bench's armies and top-ups, as Editor/OzUeSameFight.cs places them): the same.</item>
    /// <item>T the rule time of the second and third runs (grid off, grid on; the first one warms the code up), for the report; not a pass bar.</item>
    /// </list>
    /// Menus: OpenZ/V1.4/Neighbour Grid Count (quick: L02 seed 1 for 3 min and the Unit editor 200 a side for 20 s; about 1-2 min) and
    /// (full: L02, L16, L17, L07 seeds 1-3 for 10 min and the Unit editor 200 a side for 60 s; about 10-20 min). The report goes to the
    /// </summary>
    public static class OzV14NeighbourGridCount
    {
        public static readonly string[] Levels = { "L02", "L16", "L17", "L07" };
        static string F(double v, string f = "0") => v.ToString(f, CultureInfo.InvariantCulture);
        static ulong Mix(ulong h, long v) { h ^= (ulong)v; h *= 1099511628211UL; h ^= h >> 31; return h; }

        // ------------------------------------------------------------------ switches
        /// <summary>The public static switches of the rules (ZWorld, ZPathing) and their values, to be put back after the check.</summary>
        static List<(FieldInfo f, object v)> Save()
        {
            var l = new List<(FieldInfo, object)>();
            foreach (var t in new[] { typeof(ZWorld), typeof(ZPathing) })
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                    if (!f.IsLiteral && !f.IsInitOnly && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string))) l.Add((f, f.GetValue(null)));
            return l;
        }

        static void Restore(List<(FieldInfo f, object v)> l) { foreach (var (f, v) in l) f.SetValue(null, v); }

        static void Set(Type t, string name, object v)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (f != null && !f.IsLiteral && !f.IsInitOnly) f.SetValue(null, v);
        }

        /// <summary>The switches View/ZGame.cs Awake sets at a level start with the default options (both sides computers; the Unit editor
        /// clears the computers and holds the calm start off itself), on top of OzV11Kit.GameSwitches: every deviation on, then the ones a
        /// default start leaves off. (ZWorld.HeavyTurretRockClearM, a number the view measures, keeps its headless default.)</summary>
        public static void PlaySwitches()
        {
            foreach (var f in typeof(ZWorld).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.FieldType == typeof(bool) && !f.IsLiteral && !f.IsInitOnly && f.Name.EndsWith("Deviation")) f.SetValue(null, true);
            ZWorld.ArmourChipDeviation = false;                                      // oz_armour_chip, default 0
            ZWorld.NoTankDuelVsJeep = true; ZWorld.ElectrobotAllLevels = false; ZPathing.VehicleFirst = true;
            ZWorld.L17CrewsDeviation = false;                                        // Easy only, never with both sides computers
            ZWorld.FortDoorGunDeviation = false;                                     // Hard only, never with both sides computers
            ZWorld.OvertakeDeviation = false;                                        // OPTIONS: fast units pass slow ones, off by default
            Set(typeof(ZWorld), "CustomStatsDeviation", false);                     // never in a computer match or the Unit editor
            Set(typeof(ZWorld), "HeroSquadsOffAsBlue", false);
            Set(typeof(ZWorld), "FriendsOverlap", false);                           // OPTIONS: friendly units overlap, off by default
            Set(typeof(ZWorld), "ShuttleBigCrashProofNext", false);
            Set(typeof(ZWorld), "L17StartShuttle", false);
            ZWorld.SetCrocodileRules(true, true, true, true, true);
            ZWorld.ShuttleOn = true; ZWorld.ShuttleTeam = Team.Red;
            ZWorld.NbVerify = false;
        }

        // ------------------------------------------------------------------ one run
        public sealed class Run
        {
            public string label, error; public double ms; public int ticks; public string end;
            public readonly List<ulong> hashes = new List<ulong>();
            public long queries, verifyFails; public string verifyFirst;
        }

        static ulong TickHash(ulong h, ZWorld w)
        {
            foreach (var e in w.Events) { h = Mix(h, (long)e.type); h = Mix(h, e.objId); h = Mix(h, e.otherId); h = Mix(h, e.x); h = Mix(h, e.y); h = Mix(h, BitConverter.SingleToInt32Bits(e.value)); }
            w.Events.Clear();
            h = OzV11StrikeCount.Fold(h, w);
            foreach (var o in w.Objects)
            {
                h = Mix(h, o.HoldPosition ? 1 : 0); h = Mix(h, o.AttackObject != null ? o.AttackObject.Id : -1); h = Mix(h, o.Destroyed ? 1 : 0);
                h = Mix(h, BitConverter.DoubleToInt64Bits(o.KillAt)); h = Mix(h, BitConverter.SingleToInt32Bits(o.Dx)); h = Mix(h, BitConverter.SingleToInt32Bits(o.Dy));
                h = Mix(h, o.Grenades); h = Mix(h, o.Drivers.Count);
            }
            foreach (var f in w.Flags) h = Mix(h, (long)f.Owner);
            foreach (var b in w.Bots) { h = Mix(h, b.Orders); h = Mix(h, b.Rounds); h = Mix(h, b.RejectedOrders); h = Mix(h, b.BuildChanges); }
            return h;
        }

        static Run Play(string label, ZWorld w, int maxTicks, double secs, Action<ZWorld> after)
        {
            var R = new Run { label = label };
            ulong h = 1469598103934665603UL; w.Events.Clear();
            while ((maxTicks > 0 ? R.ticks < maxTicks : w.Time < secs) && !w.GameOver)
            {
                long t0 = Stopwatch.GetTimestamp();
                w.Tick(ZConst.TICK);
                R.ms += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                R.ticks++;
                after?.Invoke(w);
                h = TickHash(h, w);
                R.hashes.Add(h);
            }
            int units = 0; foreach (var o in w.Objects) if (o.IsUnit && !o.Destroyed && o.KillAt < 0) units++;
            R.end = $"end {F(w.Time, "0.00")} s, over {w.GameOver}, winner {w.Winner}, objects {w.Objects.Count}, units {units}, rules {w.RulesFingerprint(1469598103934665603UL):x16}";
            R.queries = w.NbQueries; R.verifyFails = w.NbVerifyFails; R.verifyFirst = w.NbVerifyFirst;
            return R;
        }

        /// <summary>One computer match (both sides computers) with the grid on or off.</summary>
        public static Run Match(string settings, string map, string level, int seed, double seconds, bool grid, bool verify)
        {
            ZWorld.PerfNeighbourGrid = grid; ZWorld.NbVerify = verify;
            try
            {
                var w = ZWorld.Create(settings, map, seed, level == "L17");
                w.Bots.Add(new ZBot(w, Team.Blue, seed + 101)); w.Bots.Add(new ZBot(w, Team.Red, seed));
                return Play($"{level} seed {seed}", w, 0, seconds, null);
            }
            catch (Exception e) { return new Run { label = $"{level} seed {seed}", error = e.GetType().Name + ": " + e.Message }; }
            finally { ZWorld.PerfNeighbourGrid = true; ZWorld.NbVerify = false; }
        }

        // ------------------------------------------------------------------ the Unit editor's fight
        static readonly string[] Mix16 = { "grunt", "psycho", "light", "grunt", "sniper", "medium", "tough", "pyro", "jeep", "laser", "gatling", "heavy", "grunt", "psycho", "missile_launcher", "howitzer" };
        static readonly string[] Robots = { "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot" };
        static readonly string[] Guns = { "gatling", "gun", "howitzer", "missile_cannon" };

        /// <summary>The cap bench's army (OzUeSameFight.Army, View/OzUePlayground.Cap.cs BenchMix): groups in 18 rows 3 tiles apart, columns 3
        /// tiles apart from fromCol on, the front 5 tiles from the middle on side sign. Returns the next free column.</summary>
        public static int Army(ZWorld w, Team team, int sign, int units, int fromCol)
        {
            int cx = w.Map.width / 2, cy = w.Map.height / 2, made = 0, k = 0, row = 0, col = fromCol, wraps = 0;
            const int Rows = 18, Step = 3, Front = 5;
            while (made < units && k < 4000)
            {
                string id = Mix16[k++ % Mix16.Length];
                int tx = cx + sign * (Front + col * Step), ty = cy + (row - Rows / 2) * Step;
                if (++row >= Rows) { row = 0; col++; }
                if (tx < 2 || tx > w.Map.width - 4) { if (++wraps > 2) break; col = 0; row = 0; continue; }
                if (ty < 2 || ty > w.Map.height - 4) continue;
                ZObject o;
                if (Array.IndexOf(Robots, id) >= 0) o = w.CreateRobotGroup(id, tx * 16, ty * 16, team);
                else o = w.CreateObject(Array.IndexOf(Guns, id) >= 0 ? "cannon" : "vehicle", id, tx * 16, ty * 16, team);
                if (o == null) continue;
                made += 1 + o.Minions.Count;
            }
            return col + (row > 0 ? 1 : 0);
        }

        /// <summary>The Unit editor's fight: perSide a side placed as the bench does, no computer, no game end, a flag hold of 1 s, calm start
        /// off; once a second a side under 95 % of its number is topped up behind its last column (View/OzUeCapBench.cs).</summary>
        public static Run UeFight(string settings, string ueMap, int perSide, int ticks, bool grid, bool verify)
        {
            ZWorld.PerfNeighbourGrid = grid; ZWorld.NbVerify = verify;
            bool calm = ZWorld.CalmStartDeviation;
            try
            {
                ZWorld.CalmStartDeviation = false;
                var w = ZWorld.Create(settings, ueMap, 1, false);
                w.Bots.Clear(); w.NoGameEnd = true; w.FlagHoldSec = 1.0;
                int colB = Army(w, Team.Blue, -1, perSide, 0), colR = Army(w, Team.Red, 1, perSide, 0);
                return Play($"Unit editor {perSide} a side", w, ticks, 0, x =>
                {
                    if (x.TickIndex % 100 != 0) return;
                    int nb = 0, nr = 0;
                    foreach (var o in x.Objects) if (o.IsUnit && !o.Destroyed && o.KillAt < 0) { if (o.Owner == Team.Blue) nb++; else if (o.Owner == Team.Red) nr++; }
                    if (nb < perSide * 0.95) colB = Army(x, Team.Blue, -1, perSide - nb, colB);
                    if (nr < perSide * 0.95) colR = Army(x, Team.Red, 1, perSide - nr, colR);
                });
            }
            catch (Exception e) { return new Run { label = $"Unit editor {perSide} a side", error = e.GetType().Name + ": " + e.Message }; }
            finally { ZWorld.PerfNeighbourGrid = true; ZWorld.NbVerify = false; ZWorld.CalmStartDeviation = calm; }
        }

        // ------------------------------------------------------------------ G: the grid alone
        static int pass, fail;
        static void Row(StringBuilder sb, bool ok, string what) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS  " : "FAIL  ") + what); }

        /// <summary>400 questions at points of a fixed sequence, every reach -1 .. 900 px: the grid's answer against a walk of Objects.</summary>
        static (int same, int asked, int units, string first) Ask(ZWorld w, ref uint s)
        {
            int same = 0, asked = 0, units = 0; string first = null;
            int wp = w.Map.WidthPix, hp = w.Map.HeightPix;
            for (int q = 0; q < 400; q++)
            {
                s = s * 1664525u + 1013904223u; int x = (int)(s % (uint)(wp + 400)) - 200;
                s = s * 1664525u + 1013904223u; int y = (int)(s % (uint)(hp + 400)) - 200;
                s = s * 1664525u + 1013904223u; int r = q % 10 == 0 ? (int)(s % 902u) - 1 : (int)(s % 260u);
                var g = w.NearUnitsList(x, y, r);
                var walk = new List<ZObject>();
                foreach (var o in w.Objects) if (o.IsUnit && r >= 0 && Math.Abs(o.CenterX - x) <= r && Math.Abs(o.CenterY - y) <= r) walk.Add(o);
                asked++; units += walk.Count;
                bool ok = g != null && g.Count == walk.Count;
                for (int i = 0; ok && i < walk.Count; i++) ok = g[i] == walk[i];
                if (ok) same++;
                else if (first == null) first = $"({x}, {y}) r {r}: grid {(g == null ? "none" : g.Count.ToString())}, walk {walk.Count}";
            }
            return (same, asked, units, first);
        }

        public static string Geometry(string settings, string ueMap)
        {
            var sb = new StringBuilder();
            var saved = Save();
            var game = OzV11Kit.GameSwitches();
            try
            {
                PlaySwitches(); ZWorld.PerfNeighbourGrid = true; ZWorld.CalmStartDeviation = false;
                var w = ZWorld.Create(settings, ueMap, 1, false);
                w.Bots.Clear(); w.NoGameEnd = true;
                Army(w, Team.Blue, -1, 150, 0); Army(w, Team.Red, 1, 150, 0);
                uint s = 12345u;
                void Step(string what)
                {
                    var (same, asked, units, first) = Ask(w, ref s);
                    Row(sb, same == asked, $"{what}: {same} of {asked} answers the same as the walk ({units} units found in all){(first != null ? "; first different " + first : "")}");
                }
                Step("G1 the crowd as placed");
                for (int i = 0; i < 500; i++) w.Tick(ZConst.TICK);
                Step("G2 after 5 s of the fight (the rules' own steps and pushes)");
                int k = 0, mw = w.Map.WidthPix - 48, mh = w.Map.HeightPix - 48;
                foreach (var o in w.Objects)
                    if (o.IsUnit && k++ % 7 == 0) o.SetCords(Math.Max(16, Math.Min(mw, o.LocX + (k % 2 == 0 ? 333 : -251))), Math.Max(16, Math.Min(mh, o.LocY + (k % 3 == 0 ? 190 : -77))));
                Step("G3 every 7th unit jumped by SetCords (across the map)");
                var away = new List<(ZObject o, int x, int y)>();
                k = 0;
                foreach (var o in w.Objects) if (o.IsUnit && k++ % 11 == 0) away.Add((o, o.LocX, o.LocY));
                for (int i = 0; i < away.Count; i++) away[i].o.SetCords(i % 2 == 0 ? -300 - i : w.Map.WidthPix + 200 + i, i % 3 == 0 ? -150 : w.Map.HeightPix + 90 + i);
                Step("G3b every 11th unit put off the map (before every edge)");
                foreach (var (o, x, y) in away) o.SetCords(x, y);
                var some = w.Objects.FindAll(o => o.IsUnit && !o.IsMinion);
                for (int i = 0; i < some.Count; i += 9) w.RemoveNow(some[i]);
                Army(w, Team.Blue, -1, 40, 9); Army(w, Team.Red, 1, 40, 9);
                Step("G4 squads taken out (RemoveNow) and 80 units made");
                w.Objects.RemoveAll(o => o.IsUnit && o.Id % 5 == 0);
                Step("G5 a check edits Objects itself (every unit with an id divisible by 5 removed)");
                for (int i = 0; i < 300; i++) w.Tick(ZConst.TICK);
                Step("G6 3 s more of the fight");
                Row(sb, w.NbQueries > 0, $"G7 the rules asked the grid during the fight ({w.NbQueries} questions)");
            }
            catch (Exception e) { Row(sb, false, "G the check threw: " + e.GetType().Name + ": " + e.Message); }
            finally { game.Dispose(); Restore(saved); }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ S: same fights
        static void Compare(StringBuilder sb, string what, Run off, Run on, Run timeOn)
        {
            if (off.error != null || on.error != null || timeOn.error != null) { Row(sb, false, $"{what}: threw: {off.error ?? on.error ?? timeOn.error}"); return; }
            int first = -1;
            for (int i = 0; i < Math.Max(off.hashes.Count, Math.Max(on.hashes.Count, timeOn.hashes.Count)); i++)
                if (i >= off.hashes.Count || i >= on.hashes.Count || i >= timeOn.hashes.Count || off.hashes[i] != on.hashes[i] || timeOn.hashes[i] != off.hashes[i]) { first = i; break; }
            bool same = first < 0 && off.end == on.end && timeOn.end == on.end;
            Row(sb, same, $"{what}: {off.ticks} ticks, every tick's fingerprint {(same ? "identical" : "DIFFERENT from tick " + (first + 1))}; {on.end}");
            Row(sb, on.verifyFails == 0 && on.queries > 0, $"{what}: the grid answered {on.queries} questions, {on.verifyFails} differed from the walk{(on.verifyFirst != null ? " (first: " + on.verifyFirst + ")" : "")}");
            sb.AppendLine($"      T rule time: grid off {F(off.ms)} ms, grid on {F(timeOn.ms)} ms ({F(off.ms / Math.Max(1e-6, timeOn.ms), "0.00")}x), {F(off.ms / Math.Max(1, off.ticks), "0.000")} -> {F(timeOn.ms / Math.Max(1, timeOn.ticks), "0.000")} ms a tick");
        }

        public static string Fights(string settings, Func<string, string> map, string ueMap, string[] levels, int seeds, double minutes, int uePerSide, int ueTicks)
        {
            var sb = new StringBuilder();
            var saved = Save();
            var game = OzV11Kit.GameSwitches();
            try
            {
                foreach (var lv in levels)
                    for (int seed = 1; seed <= seeds; seed++)
                    {
                        string m = map(lv);
                        PlaySwitches(); var on = Match(settings, m, lv, seed, minutes * 60, true, true);       // first: the grid with every answer checked (and the warm-up)
                        PlaySwitches(); var off = Match(settings, m, lv, seed, minutes * 60, false, false);
                        PlaySwitches(); var t = Match(settings, m, lv, seed, minutes * 60, true, false);
                        Compare(sb, $"S1 {lv} seed {seed}, {F(minutes)} min, computers both sides", off, on, t);
                    }
                if (uePerSide > 0)
                {
                    PlaySwitches(); var on = UeFight(settings, ueMap, uePerSide, ueTicks, true, true);
                    PlaySwitches(); var off = UeFight(settings, ueMap, uePerSide, ueTicks, false, false);
                    PlaySwitches(); var t = UeFight(settings, ueMap, uePerSide, ueTicks, true, false);
                    Compare(sb, $"S2 the Unit editor's fight, {uePerSide} a side, {ueTicks} ticks, top-ups", off, on, t);
                }
            }
            finally { game.Dispose(); Restore(saved); }
            return sb.ToString();
        }

        /// <summary>The whole check: G1-G7, then S1 / S2. The first line: PASS or FAIL with the count.</summary>
        public static string Count(string settings, Func<string, string> map, string ueMap, string[] levels, int seeds, double minutes, int uePerSide, int ueTicks)
        {
            pass = fail = 0;
            var body = new StringBuilder();
            body.Append(Geometry(settings, ueMap));
            body.Append(Fights(settings, map, ueMap, levels, seeds, minutes, uePerSide, ueTicks));
            return $"{(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} neighbour grid count (1.4 speed-up 5): {string.Join(" ", levels)} seeds 1-{seeds}, {F(minutes)} min; the Unit editor {uePerSide} a side {ueTicks} ticks\n" + body;
        }

#if UNITY_EDITOR
        [MenuItem("OpenZ/V1.4/Neighbour Grid Count (quick, headless)")]
        public static void Quick() => Menu("count_quick.txt", new[] { "L02" }, 1, 3, 200, 2000);

        [MenuItem("OpenZ/V1.4/Neighbour Grid Count (full, headless)")]
        public static void Full() => Menu("count_full.txt", Levels, 3, 10, 200, 6000);

        static void Menu(string file, string[] levels, int seeds, double minutes, int ue, int ueTicks)
        {
            if (EditorApplication.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ NbGrid] leave Play first (edit mode, headless)"); return; }
            var st = Resources.Load<TextAsset>("OpenZ/default_settings");
            var uem = Resources.Load<TextAsset>("OpenZ/Maps/UE_grid");
            if (!st || !uem) { UnityEngine.Debug.LogError("[OpenZ NbGrid] Resources OpenZ/default_settings or OpenZ/Maps/UE_grid missing"); return; }
            string report = Count(st.text, lv => Resources.Load<TextAsset>($"OpenZ/Maps/{lv}_grid").text, uem.text, levels, seeds, minutes, ue, ueTicks);
            UnityEngine.Debug.Log("[OpenZ NbGrid] " + report);
            try
            {
                string d = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_4/kits/perf_sim_neighbour/proof"));
                System.IO.Directory.CreateDirectory(d);
                System.IO.File.WriteAllText(System.IO.Path.Combine(d, file), report);
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[OpenZ NbGrid] report not written: " + e.Message); }
        }
#endif
    }
}
