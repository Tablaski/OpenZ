using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.2, level 16: the landmarks and rocks outside the map at the wide RTS zoom (the far pyramid two times smaller without
    /// its own big tree, the flume ends' rocks at the map's own sizes and seated, OzL16Outskirts.Props.cs): the Play check and its
    /// pictures. Menu OpenZ/V1.2/L16 outskirts props: check + pictures (Play), in Play on level 16 once the land is shown (the start
    /// OzL16Outskirts.PropsOn off and the level restarted):
    ///  - PROPS_&lt;round&gt;.txt: OutskirtsPropsCheck() (the counted check), TreeCheck() (the flume ends' rocks), RimFixCheck(),
    ///    RimSeatCheck(), Status();
    ///    widest 165 m: L16_props_pyramid_90m_&lt;round&gt;.png, _165m;
    ///  - the north flume ends' rocks (the floating rock) with the player's own camera: flume_left / flume_right at 120 and
    ///    165 m (the rocks near the picture's top): L16_props_flume_left_120m_&lt;round&gt;.png ...;
    ///  - the widest zoom on all four sides with the player's own camera (its limits hold): L16_props_widest_north_&lt;round&gt;.png,
    ///    _east, _south, _west.
    /// Editor only, changes nothing in the game (the camera is given back to the player at the end).
    /// </summary>
    public static class OzV12OutskirtsPropsShots
    {
        const string Tag = "[OzV12OutskirtsPropsShots]";
        static readonly Queue<System.Action> steps = new Queue<System.Action>();
        static double nextAt;

        static string Out
        {
            get { var p = Path.Combine(OpenZPaths.Records, "milestones", "l16_outskirts_props_v12"); Directory.CreateDirectory(p); return p; }
        }

        public static string Round => OzL16Outskirts.PropsOn ? "k52" : "before";

        [MenuItem("OpenZ/V1.2/L16 outskirts props: check + pictures (Play)")]
        public static void Menu() => Run();

        public static string Run()
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.level != OzL16Outskirts.Level) { Debug.LogWarning(Tag + " start Play on level 16 first"); return "not in Play on level 16"; }
            if (!OzL16Outskirts.I || OzL16Outskirts.BuildMs <= 0f || OzL16FlyoverFlora.LowLookOn) { Debug.LogWarning(Tag + " the land is not shown yet (wait for the start flyover to end)"); return "land not shown yet"; }
            var cam = Object.FindFirstObjectByType<RtsCamera>(); if (!cam) return "no RTS camera";
            string dir = Out, r = Round;
            string check = OzL16Outskirts.OutskirtsPropsCheck() + "\n" + OzL16Outskirts.TreeCheck() + "\n" + OzL16Outskirts.RimFixCheck() + "\n" + OzL16Outskirts.RimSeatCheck() + "\n" + OzL16Outskirts.Status() + "\n";
            File.WriteAllText(Path.Combine(dir, "PROPS_" + r + ".txt"), check);
            Debug.Log(Tag + " " + check);
            var log = new List<string> { "L16 outskirts props, round " + r + " | picture | camera" };
            steps.Clear();
            foreach (float h in new[] { 90f, 165f })
            {
                float hh = h;
                Shot(() => OzL16Outskirts.PropsView(hh), Path.Combine(dir, $"L16_props_pyramid_{hh:0}m_{r}.png"), log, $"the far pyramid at {hh:0} m, heading 65");
            }
            steps.Enqueue(() => { log.Add("camera back: " + OzL16Outskirts.PropsView(0f)); nextAt = EditorApplication.timeSinceStartup + 1.0; });
            foreach (var place in new[] { "flume_left", "flume_right" })
                foreach (float h in new[] { 120f, 165f })
                {
                    string pl = place; float hh = h;
                    Shot(() => OzL16Outskirts.Frame(pl, hh), Path.Combine(dir, $"L16_props_{pl}_{hh:0}m_{r}.png"), log, $"{pl} at {hh:0} m");
                }
            foreach (var side in new (string name, float x, float z)[] { ("north", 104f, -10f), ("east", 208f, -128f), ("south", 104f, -256f), ("west", 0f, -128f) })
            {
                var s = side;
                Shot(() => { cam.focus = new Vector3(s.x, 0f, s.z); cam.SetHeightImmediate(cam.maxHeight); return $"camera over ({cam.focus.x:0},{cam.focus.z:0}) at {cam.height:0} m (its limits hold)"; },
                     Path.Combine(dir, $"L16_props_widest_{s.name}_{r}.png"), log, $"widest zoom, {s.name} side");
            }
            steps.Enqueue(() =>
            {
                OzL16Outskirts.PropsView(0f);
                File.WriteAllLines(Path.Combine(dir, "SHOTS_" + r + ".txt"), log);
                Debug.Log(Tag + " done: " + dir);
            });
            nextAt = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return "running: " + dir;
        }

        static void Shot(System.Func<string> place, string path, List<string> log, string what)
        {
            steps.Enqueue(() => { log.Add(what + ": " + place()); nextAt = EditorApplication.timeSinceStartup + 2.5; });
            steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(path); log.Add(what + ": " + Path.GetFileName(path)); nextAt = EditorApplication.timeSinceStartup + 1.5; });
        }

        static void Tick()
        {
            if (!Application.isPlaying) { steps.Clear(); EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup < nextAt) return;
            if (steps.Count == 0) { EditorApplication.update -= Tick; return; }
            steps.Dequeue()();
        }
    }
}
