using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Check for the grunt in a tank's top hatch (View/BoardingView.HatchGrunt.cs, row 54). Edit: the table. Play: for 12 s every tank on
    /// the map whose hatch figure is up (pop 1) is read once per figure: a grunt in the light tank (and in the medium and heavy tank with
    /// HatchGruntMediumHeavy on) at the row's size x his size before (1 %), every other figure (any other robot type, a hero, a grunt in a
    /// tank whose row is off, any other vehicle's hatch) at x 1 (1 %). Read only: it places
    /// </summary>
    public static class OzHatchGruntCheck
    {
        static readonly Dictionary<string, string> rows = new Dictionary<string, string>();
        static readonly HashSet<string> seenTypes = new HashSet<string>();
        static double until; static int pass, fail; static StringBuilder sb;

        [MenuItem("OpenZ/Checks/V1.3/Hatch grunt (Edit: table; Play: every tank hatch figure for 12 s)")]
        static void Run()
        {
            sb = new StringBuilder("# Hatch grunt check (row 54)\n\n");
            sb.AppendLine($"switch HatchGruntOn {(BoardingView.HatchGruntOn ? "ON" : "OFF")}, medium and heavy tank rows (HatchGruntMediumHeavy) {(BoardingView.HatchGruntMediumHeavy ? "ON" : "OFF")}\n");
            sb.AppendLine("| tank | size x before | toward the turret front | along his facing | up (x his size) |");
            sb.AppendLine("|---|---|---|---|---|");
            bool ok = true;
            foreach (var t in new[] { "light", ZMTank.Type, "heavy" })
            {
                if (!BoardingView.HatchGrunt.TryGetValue(t, out var k)) { sb.AppendLine($"| {t} | missing | | | |"); ok = false; continue; }
                if (k.size < 1f || k.size > 1.2f || Mathf.Abs(k.fwd) > 0.2f || Mathf.Abs(k.face) > 0.2f || Mathf.Abs(k.up) > 0.2f) ok = false;
                sb.AppendLine($"| {t} | {k.size:0.000} | {k.fwd:+0.000;-0.000} | {k.face:+0.000;-0.000} | {k.up:+0.000;-0.000} |");
            }
            sb.AppendLine($"\nE1 three tank rows, sizes 1 to 1.2, shifts under 0.2 of his size: {(ok ? "PASS" : "FAIL")}");
            if (!Application.isPlaying || !ZGame.I || ZGame.I.World == null) { Say("(enter Play with tanks crewed by grunts and by other robot types, their hatch figures up)"); return; }
            rows.Clear(); seenTypes.Clear(); pass = fail = 0;
            sb.AppendLine("\n| tank | # | figure | size x before | want | verdict |");
            sb.AppendLine("|---|---|---|---|---|---|");
            until = EditorApplication.timeSinceStartup + 12.0;
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (!Application.isPlaying || !ZGame.I) { EditorApplication.update -= Poll; Say("Play ended before the end of the read"); return; }
            foreach (var kv in ZGame.I.Views)
            {
                var v = kv.Value; if (!v || v.Obj == null || !v.gameObject.activeInHierarchy) continue;
                if (!BoardingView.HatchGruntSample(v, out bool grunt, out string type, out float size, out float want, out float pop) || pop < 0.99f) continue;
                string key = v.Obj.Id + "|" + type;
                if (rows.ContainsKey(key)) continue;
                bool ok = Mathf.Abs(size / want - 1f) <= 0.01f;
                if (ok) pass++; else fail++;
                if (grunt && ok) seenTypes.Add(v.Obj.Type);
                rows[key] = $"| {v.Obj.Type} | {v.Obj.Id} | {type}{(grunt ? " (row 54)" : "")} | {size:0.000} | {want:0.000} | {(ok ? "PASS" : "FAIL")} |";
                sb.AppendLine(rows[key]);
            }
            if (EditorApplication.timeSinceStartup < until) return;
            EditorApplication.update -= Poll;
            bool all = seenTypes.Contains("light") && (!BoardingView.HatchGruntMediumHeavy || (seenTypes.Contains(ZMTank.Type) && seenTypes.Contains("heavy")));
            sb.AppendLine($"\nP1 every hatch figure read at its size: {(fail == 0 && pass > 0 ? "PASS" : "FAIL")} ({pass} pass, {fail} fail); a grunt read at his row's size in {(BoardingView.HatchGruntMediumHeavy ? "the light, medium and heavy tank" : "the light tank")}: {(all ? "PASS" : "NOT YET (" + string.Join(", ", seenTypes) + ")")}; frames placed by the row so far {BoardingView.HatchGruntFrames}");
            Say(null);
        }

        static void Say(string note)
        {
            if (note != null) sb.AppendLine("\n" + note);
            var text = sb.ToString();
            Debug.Log("[OpenZ hatch grunt] " + text);
            try { File.WriteAllText(Path.Combine(OpenZPaths.Records, "hatch_grunt_check.md"), text); } catch (System.Exception e) { Debug.LogWarning("[OpenZ hatch grunt] report not written: " + e.Message); }
        }
    }
}
