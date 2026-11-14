using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 8 : menu OpenZ/V1.3/Unit Editor/8b Same
    /// Fight Check (edit mode). The Unit editor's fight without the editor: a fresh world on the playground's own grid
    /// (Resources/OpenZ/Maps/UE_grid, default_settings, seed 1, no CPU, no game end, a flag hold of 1 s, calm start off: as OzUnitEditor
    /// sets its world), the bench's mixed armies (View/OzUePlayground.Cap.cs BenchMix order, 18 rows, 6 m columns, the fronts 20 m apart)
    /// placed through the Sim calls the dock uses, then Ticks fixed 10 ms rule ticks, and a fingerprint of every object (type, owner, HP,
    /// position, alive) in the world's own order, plus the world clock and the live counts. Run A twice (the check itself must repeat:
    /// A == A'), then run B with every registered speed-up switch ON (<see cref="Switches"/>: the sim speed-ups of item 25 add theirs;
    /// item 8's own speed-up, the field's hover cull, is view only and cannot reach the rules). PASS = A == A' == B. Writes
    /// same_fight.txt into the item's proof folder. Edit mode only (no Play, no views); about 30-60 s at 100 a side.
    /// </summary>
    public static class OzUeSameFight
    {
        /// <summary>Speed-up switches to compare (name, set on / off). Item 25 registers its sim switches here; none = A == A' only.</summary>
        public static readonly List<(string name, Action<bool> set)> Switches = new List<(string, Action<bool>)>();
        public static int PerSide = 100, Ticks = 3000;
        static readonly string[] Mix = { "grunt", "psycho", "light", "grunt", "sniper", "medium", "tough", "pyro", "jeep", "laser", "gatling", "heavy", "grunt", "psycho", "missile_launcher", "howitzer" };   // OzUePlayground.BenchMix

        [MenuItem("OpenZ/V1.3/Unit Editor/8b Same Fight Check (edit mode)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ SameFight] leave Play first (edit mode, headless)"); return; }
            var log = new StringBuilder();
            AutoRegister(log);
            bool calm0 = ZWorld.CalmStartDeviation;
            try
            {
                ZWorld.CalmStartDeviation = false;                                                               // as the Unit editor holds it
                foreach (var s in Switches) s.set(false);
                string a = Fight(out string an, out double ams), a2 = Fight(out _, out double a2ms);
                log.AppendLine($"A  (speed-ups off): {an}, {ams:0} ms");
                log.AppendLine($"A' (speed-ups off): {(a2 == a ? "the same" : "DIFFERENT")}, {a2ms:0} ms");
                string b = a; double bms = 0;
                if (Switches.Count > 0)
                {
                    foreach (var s in Switches) s.set(true);
                    b = Fight(out string bn, out bms);
                    foreach (var s in Switches) s.set(false);
                    log.AppendLine($"B  (speed-ups on: {string.Join(", ", Switches.ConvertAll(s => s.name))}): {(b == a ? "the same" : "DIFFERENT: " + bn)}, {bms:0} ms ({(ams > 0 ? (bms / ams * 100.0).ToString("0") : "-")} % of A's time)");
                }
                else log.AppendLine("B  not run: no speed-up switch registered (OzUeSameFight.Switches); item 8's hover cull is view only (see the bench's same-hover line)");
                bool pass = a == a2 && b == a;
                log.Insert(0, (pass ? "PASS" : "FAIL") + $" same fight: {PerSide} a side, {Ticks} ticks ({Ticks / 100.0:0} s of game time), fingerprint {a.Substring(0, Math.Min(16, a.Length))}\n");
            }
            catch (Exception e) { log.Insert(0, "FAIL the check threw: " + e.GetType().Name + ": " + e.Message + "\n"); }
            finally { ZWorld.CalmStartDeviation = calm0; foreach (var s in Switches) { try { s.set(false); } catch (Exception) { } } restore?.Invoke(); restore = null; }
            string text = "[OpenZ SameFight] " + log;
            Debug.Log(text);
            try
            {
                string d = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/solutions/item08_ue_unit_cap/proof"));
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d, "same_fight.txt"), text);
            }
            catch (Exception e) { Debug.LogWarning("[OpenZ SameFight] same_fight.txt not written: " + e.Message); }
        }

        /// <summary>Item 25's sim speed-up switch (Sim/ZWorld.PerfRules.cs ZWorld.PerfFastRules, item25_perf_sim) when that package is in:
        /// found by name, so this check compiles with or without it. Its saved value comes back after the check.</summary>
        static void AutoRegister(StringBuilder log)
        {
            var f = typeof(ZWorld).GetField("PerfFastRules", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (f == null || f.FieldType != typeof(bool)) return;
            bool was = (bool)f.GetValue(null);
            restore = () => f.SetValue(null, was);
            if (!Switches.Exists(s => s.name == "ZWorld.PerfFastRules")) Switches.Add(("ZWorld.PerfFastRules", on => f.SetValue(null, on)));
            log.AppendLine("item 25's ZWorld.PerfFastRules found (it was " + was + ", put back after): off for A / A', on for B");
        }
        static Action restore;

        /// <summary>One fight: the fingerprint (hex), its words (clock, live units a side), the time it took.</summary>
        static string Fight(out string words, out double ms)
        {
            var st = Resources.Load<TextAsset>("OpenZ/default_settings");
            var map = Resources.Load<TextAsset>("OpenZ/Maps/UE_grid");
            if (!st || !map) throw new Exception("Resources OpenZ/default_settings or OpenZ/Maps/UE_grid missing");
            var w = ZWorld.Create(st.text, map.text, 1, false);
            w.Bots.Clear(); w.NoGameEnd = true; w.FlagHoldSec = 1.0;
            Army(w, Team.Blue, -1); Army(w, Team.Red, 1);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Ticks; i++) w.Tick(ZConst.TICK);
            ms = sw.Elapsed.TotalMilliseconds;
            ulong h = 1469598103934665603UL;
            void Mix64(long v) { unchecked { for (int k = 0; k < 8; k++) { h ^= (byte)(v >> (8 * k)); h *= 1099511628211UL; } } }
            void MixS(string s) { if (s == null) { Mix64(-1); return; } foreach (char c in s) Mix64(c); }
            int blue = 0, red = 0;
            foreach (var o in w.Objects)
            {
                MixS(o.Type); Mix64((int)o.Owner); Mix64(o.Health); Mix64(o.LocX); Mix64(o.LocY); Mix64(o.Destroyed ? 1 : 0); Mix64(o.KillAt >= 0 ? 1 : 0);
                if (o.IsUnit && !o.Destroyed && o.KillAt < 0) { if (o.Owner == Team.Blue) blue++; else if (o.Owner == Team.Red) red++; }
            }
            Mix64(w.Objects.Count); Mix64((long)Math.Round(w.Time * 1000.0));
            words = $"clock {w.Time:0.00} s, {w.Objects.Count} objects, live units blue {blue} / red {red}";
            return h.ToString("x16");
        }

        /// <summary>The bench's army: groups of Mix in 18 rows 3 tiles apart, columns 3 tiles apart, the front 5 tiles from the middle on side sign.</summary>
        static void Army(ZWorld w, Team team, int sign)
        {
            int cx = w.Map.width / 2, cy = w.Map.height / 2, made = 0, k = 0, row = 0, col = 0;
            const int Rows = 18, Step = 3, Front = 5;
            while (made < PerSide && k < 4000)
            {
                string id = Mix[k++ % Mix.Length];
                int tx = cx + sign * (Front + col * Step), ty = cy + (row - Rows / 2) * Step;
                if (++row >= Rows) { row = 0; col++; }
                if (tx < 2 || ty < 2 || tx > w.Map.width - 4 || ty > w.Map.height - 4) continue;
                ZObject o;
                if (Array.IndexOf(OzUePlayground.Robots, id) >= 0) o = w.CreateRobotGroup(id, tx * 16, ty * 16, team);
                else o = w.CreateObject(Array.IndexOf(OzUePlayground.Guns, id) >= 0 ? "cannon" : "vehicle", id, tx * 16, ty * 16, team);
                if (o == null) continue;
                made += 1 + o.Minions.Count;
            }
        }
    }
}
