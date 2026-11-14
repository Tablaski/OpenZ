using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Check for the jeep driver's seat (View/BoardingView.JeepSeat.cs). Edit: the table (every share at most 1, the sizes against
    /// before). Play: one red jeep per robot type, crewed by that type, in a row near the camera; after 2 s each seated driver is read
    /// at the end of a frame: his hips in the DriverSeat anchor's space against the table's shift (1.5 cm) and his drawn size against the
    /// anchor's against the table's (1 %). Test tool only: the jeeps stay on the field for the frames and the drive and turn rows.
    /// </summary>
    public static class OzJeepSeatCheck
    {
        static readonly string[] Types = { "grunt", "psycho", "tough", "pyro", "sniper", "laser", "electrobot" };
        static readonly List<(string type, ZObject jeep)> placed = new List<(string, ZObject)>();
        static double readAt;

        [MenuItem("OpenZ/Checks/V1.3/Jeep driver seat (Edit: table; Play: one jeep per robot type, hips and size)")]
        static void Run()
        {
            var sb = new StringBuilder("# Jeep driver seat check\n\n");
            sb.AppendLine($"switch JeepSeatBack {(BoardingView.JeepSeatBack ? "ON" : "OFF")}, crewScale gain {BoardingView.JeepGrowBack:0.000} (x 0.62)\n");
            sb.AppendLine("| type | share | size against before (jeep at 92 %) | back m | up m |");
            sb.AppendLine("|---|---|---|---|---|");
            bool ok = true;
            foreach (var t in Types)
            {
                if (!BoardingView.JeepSeat.TryGetValue(t, out var k)) { sb.AppendLine($"| {t} | missing | | | |"); ok = false; continue; }
                float before = t == "electrobot" ? 0.81f : 1f;
                float world = BoardingView.JeepGrowBack * k.fit * 0.92f / before;
                if (k.fit <= 0f || k.fit > 1f) ok = false;
                sb.AppendLine($"| {t} | {k.fit:0.000} | {world:0.000} | {k.back:0.000} | {k.up:+0.000;-0.000} |");
            }
            sb.AppendLine($"\nE1 every robot type has a row, every share in (0, 1]: {(ok ? "PASS" : "FAIL")}");
            if (!Application.isPlaying || !ZGame.I || ZGame.I.World == null) { Say(sb, "(enter Play on L02 for the seated drivers)"); return; }
            var g = ZGame.I; var W = g.World;
            var rc = Object.FindFirstObjectByType<RtsCamera>();
            var px = ZGame.WorldToPx(rc ? rc.focus : Vector3.zero);
            int cx = px.x >> 4, cy = px.y >> 4, n = 0;
            placed.Clear();
            for (int ring = 0; ring < 40 && n < Types.Length; ring++)
                for (int i = -ring; i <= ring && n < Types.Length; i++)
                {
                    int tx = cx + i * 3, ty = cy + ring * 3;
                    if (!Free(W, tx, ty)) continue;
                    var o = W.CreateObject("vehicle", "jeep", tx * 16, ty * 16, Team.Red);
                    if (o == null) continue;
                    o.DriverType = Types[n]; g.SpawnUnitView(o);
                    placed.Add((Types[n], o)); n++;
                }
            sb.AppendLine($"\nPlay: {placed.Count} jeeps placed, read in 2 s");
            pending = sb.ToString();
            readAt = EditorApplication.timeSinceStartup + 2.0;
            EditorApplication.update -= Read; EditorApplication.update += Read;
        }

        static string pending;

        static bool Free(ZWorld W, int tx, int ty)
        {
            for (int j = -1; j <= 2; j++) for (int i = -1; i <= 2; i++) if (!W.Path.TilePassable(tx + i, ty + j, false)) return false;
            foreach (var o in W.Objects) if (!o.Destroyed && Mathf.Abs((o.CenterX >> 4) - tx) < 3 && Mathf.Abs((o.CenterY >> 4) - ty) < 3) return false;
            return true;
        }

        static void Read()
        {
            if (EditorApplication.timeSinceStartup < readAt) return;
            EditorApplication.update -= Read;
            var sb = new StringBuilder(pending ?? "");
            if (!Application.isPlaying || !ZGame.I) { Say(sb, "Play ended before the read"); return; }
            sb.AppendLine("\n| type | jeep | hips in seat (back, up) m | want | size / seat | want | verdict |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            int pass = 0;
            foreach (var (type, o) in placed)
            {
                if (!ZGame.I.Views.TryGetValue(o.Id, out var v) || !v || !BoardingView.JeepSeatSample(v, out var drawn, out var hips, out var want, out var size, out var wantSize))
                { sb.AppendLine($"| {type} | #{o.Id} | no seated driver drawn | | | | FAIL |"); continue; }
                bool ok = drawn == type && Mathf.Abs(hips.x) <= 0.015f && Mathf.Abs(hips.y - want.y) <= 0.015f && Mathf.Abs(hips.z - want.z) <= 0.015f && Mathf.Abs(size / wantSize - 1f) <= 0.01f;
                if (ok) pass++;
                sb.AppendLine($"| {drawn} | #{o.Id} | ({hips.z:0.000}, {hips.y:+0.000;-0.000}) x {hips.x:+0.000;-0.000} | ({want.z:0.000}, {want.y:+0.000;-0.000}) | {size:0.0000} | {wantSize:0.0000} | {(ok ? "PASS" : "FAIL")} |");
            }
            sb.AppendLine($"\nP1 every robot type seated where the table puts him, at its size: {(pass == Types.Length ? "PASS" : "FAIL")} ({pass}/{Types.Length}); shifted seat poses drawn so far {BoardingView.JeepSeatShifts}");
            Say(sb, null);
        }

        static void Say(StringBuilder sb, string note)
        {
            if (note != null) sb.AppendLine("\n" + note);
            var text = sb.ToString();
            Debug.Log("[OpenZ jeep seat] " + text);
            try { File.WriteAllText(Path.Combine(OpenZPaths.Records, "jeep_seat_check.md"), text); } catch (System.Exception e) { Debug.LogWarning("[OpenZ jeep seat] report not written: " + e.Message); }
        }
    }
}
