using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.2/L16 outer trees: check + pictures (Play), in Play on level 16 once the land is shown (the start flyover over):
    /// writes OzL16Outskirts.Status() and OzL16Outskirts.TreeCheck() to CHECK.txt, then takes pictures with the normal RTS camera
    /// (its own limits hold) over both north flume ends (100 m), the west side where the map's own trees stand beside the outer ones
    /// changes nothing in the game. The nine widest-zoom views with their render numbers: OzV12EdgeShots.Run(label).
    /// </summary>
    public static class OzV12OuterTreesShots
    {
        const string Tag = "[OzV12OuterTreesShots]";
        static readonly Queue<System.Action> steps = new Queue<System.Action>();
        static double nextAt;

        static string Out
        {
            get { var p = Path.Combine(OpenZPaths.Records, "milestones", "l16_outer_trees_v12"); Directory.CreateDirectory(p); return p; }
        }

        [MenuItem("OpenZ/V1.2/L16 outer trees: check + pictures (Play)")]
        public static void Menu() => Run();

        public static string Run()
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.level != OzL16Outskirts.Level) { Debug.LogWarning(Tag + " start Play on level 16 first"); return "not in Play on level 16"; }
            if (!OzL16Outskirts.I || OzL16Outskirts.BuildMs <= 0f) { Debug.LogWarning(Tag + " the land is not shown yet (wait for the start flyover to end)"); return "land not shown yet"; }
            var cam = Object.FindFirstObjectByType<RtsCamera>(); if (!cam) return "no RTS camera";
            string dir = Out, check = OzL16Outskirts.Status() + "\n" + OzL16Outskirts.TreeCheck() + "\n" + OzL16FlyoverFlora.Status() + "\n" + OzL16DrawAhead.Status() + "\n" + OzL16FarShadows.Status() + "\n";
            File.WriteAllText(Path.Combine(dir, "CHECK.txt"), check);
            Debug.Log(Tag + " " + check);
            var shots = new (string name, string where, float h)[]
            {
                ("flume_left_100m", "flume_left", 100f), ("flume_right_100m", "flume_right", 100f),
                ("west_80m", "west", 80f), ("north_80m", "north", 80f), ("north_widest", "north", cam.maxHeight),
            };
            steps.Clear();
            foreach (var s in shots)
            {
                steps.Enqueue(() => { OzL16Outskirts.Frame(s.where, s.h); nextAt = EditorApplication.timeSinceStartup + 2.5; });
                steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(Path.Combine(dir, "L16_outer_trees_" + s.name + ".png")); nextAt = EditorApplication.timeSinceStartup + 1.0; });
            }
            steps.Enqueue(() => Debug.Log(Tag + " done: " + dir));
            nextAt = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return "running: " + dir;
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
