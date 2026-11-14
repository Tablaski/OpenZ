using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.2/L16 far tree look: check + pictures (Play), in Play on level 16 once the land is shown (the start flyover over).
    /// The round is read from the switches (r4: OzL16Outskirts.TreesOn off; r5: FarLookOn off; r5b: both on; r5bmap: also
    /// FarLookMapTrees on) and named in every file:
    ///  - CHECK_&lt;round&gt;.txt: OzL16Outskirts.Status(), TreeCheck(), FarLookCheck(), the distance rule and the far shadows;
    ///  - the nine widest-zoom views of OzV12EdgeShots (same places): per view the plant cells' far meshes the camera's view meets and
    ///    their triangles (OzL16Outskirts.FarViewCount: counted from the meshes, not the editor's render counters, so units and effects
    ///    do not move them) in FARVIEWS_&lt;round&gt;.txt;
    ///  - the north side at the widest zoom (as round 5's picture L16_outer_trees_north_widest.png) and NORTH_RIM_r5_vs_&lt;round&gt;.png: the
    ///    upper part of round 5's picture above the same part of the new one;
    ///  - the zoom from 70 m to the widest over the north rim (70, 100, 110, 113, 119, 130, 165 m; the plant cells change to their far
    ///    meshes at OzL16DrawAhead.FarAbove + FarBand = 116 m) and ZOOM_113_vs_119_&lt;round&gt;.png: the last near and the first far picture.
    /// </summary>
    public static class OzV12FarTreesShots
    {
        const string Tag = "[OzV12FarTreesShots]";
        static readonly Queue<System.Action> steps = new Queue<System.Action>();
        static double nextAt;
        static readonly List<string> lines = new List<string>();

        static string Out
        {
            get { var p = Path.Combine(OpenZPaths.Records, "milestones", "l16_far_trees_v12"); Directory.CreateDirectory(p); return p; }
        }

        static string OldNorth => Path.Combine(OpenZPaths.Records, "milestones", "l16_outer_trees_v12", "L16_outer_trees_north_widest.png");

        public static string Round => !OzL16Outskirts.TreesOn ? "r4" : !OzL16Outskirts.FarLookOn ? "r5" : OzL16Outskirts.FarLookMapTrees ? "r5bmap" : "r5b";

        [MenuItem("OpenZ/V1.2/L16 far tree look: check + pictures (Play)")]
        public static void Menu() => Run();

        public static string Run(string label = null)
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null || ZGame.I.level != OzL16Outskirts.Level) { Debug.LogWarning(Tag + " start Play on level 16 first"); return "not in Play on level 16"; }
            if (!OzL16Outskirts.I || OzL16Outskirts.BuildMs <= 0f) { Debug.LogWarning(Tag + " the land is not shown yet (wait for the start flyover to end)"); return "land not shown yet"; }
            var cam = Object.FindFirstObjectByType<RtsCamera>(); if (!cam) return "no RTS camera";
            string dir = Out, r = string.IsNullOrEmpty(label) ? Round : label;
            string check = OzL16Outskirts.Status() + "\n" + OzL16Outskirts.TreeCheck() + "\n" + OzL16Outskirts.FarLookCheck() + "\n" + OzL16DrawAhead.Status() + "\n" + OzL16FarShadows.Status() + "\n";
            File.WriteAllText(Path.Combine(dir, "CHECK_" + r + ".txt"), check);
            Debug.Log(Tag + " " + check);
            float W = ZGame.I.World.Map.WidthPix / 8f, H = ZGame.I.World.Map.HeightPix / 8f;
            var views = new (string name, float x, float z)[]
            {
                ("middle", W * 0.5f, -H * 0.5f), ("nw", 0f, 0f), ("ne", W, 0f), ("sw", 0f, -H), ("se", W, -H),
                ("n", W * 0.5f, 0f), ("s", W * 0.5f, -H), ("w", 0f, -H * 0.5f), ("e", W, -H * 0.5f),
            };
            steps.Clear(); lines.Clear();
            lines.Add($"L16 far tree look, round {r} | widest height {cam.maxHeight} | view | focus | plant cells' far meshes in the view (counted from the meshes)");
            foreach (var v in views)
            {   // the same places as OzV12EdgeShots (the camera's own limits hold)
                steps.Enqueue(() => { cam.focus = new Vector3(v.x, 0f, v.z); cam.height = cam.maxHeight; cam.ZoomTo(cam.maxHeight); nextAt = EditorApplication.timeSinceStartup + 2.5; });
                steps.Enqueue(() => { lines.Add($"{v.name} | {cam.focus.x:0},{cam.focus.z:0} | {OzL16Outskirts.FarViewCount()}"); nextAt = EditorApplication.timeSinceStartup + 0.2; });
            }
            steps.Enqueue(() => { File.WriteAllLines(Path.Combine(dir, "FARVIEWS_" + r + ".txt"), lines); Debug.Log(Tag + " " + string.Join("\n", lines)); nextAt = 0; });
            // the north side at the widest zoom, beside round 5's picture
            string north = Path.Combine(dir, "L16_far_trees_north_widest_" + r + ".png");
            steps.Enqueue(() => { OzL16Outskirts.Frame("north", cam.maxHeight); nextAt = EditorApplication.timeSinceStartup + 2.5; });
            steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(north); nextAt = EditorApplication.timeSinceStartup + 1.5; });
            steps.Enqueue(() => { Pair(OldNorth, north, Path.Combine(dir, "NORTH_RIM_r5_vs_" + r + ".png"), 0.42f); nextAt = 0; });
            // the zoom over the north rim: no change in the picture at the switch to the far meshes
            foreach (float h in new[] { 70f, 100f, 110f, 113f, 119f, 130f, 165f })
            {
                float hh = Mathf.Min(h, cam.maxHeight);
                steps.Enqueue(() => { OzL16Outskirts.Frame("north", hh); nextAt = EditorApplication.timeSinceStartup + 2.5; });
                steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"L16_far_trees_zoom_{hh:0}m_{r}.png")); lines.Add($"zoom {hh:0} m: {OzL16Outskirts.FarViewCount()}"); nextAt = EditorApplication.timeSinceStartup + 1.5; });
            }
            steps.Enqueue(() =>
            {
                Pair(Path.Combine(dir, $"L16_far_trees_zoom_113m_{r}.png"), Path.Combine(dir, $"L16_far_trees_zoom_119m_{r}.png"), Path.Combine(dir, $"ZOOM_113_vs_119_{r}.png"), 0.5f);
                File.WriteAllLines(Path.Combine(dir, "FARVIEWS_" + r + ".txt"), lines);
                Debug.Log(Tag + " done: " + dir);
            });
            nextAt = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return "running: " + dir;
        }

        /// <summary>The upper part (share of the height) of picture a above the same part of picture b, in one picture.</summary>
        static void Pair(string a, string b, string outPath, float share)
        {
            if (!File.Exists(a) || !File.Exists(b)) { Debug.LogWarning(Tag + " picture missing for the pair: " + (File.Exists(a) ? b : a)); return; }
            var ta = new Texture2D(2, 2); var tb = new Texture2D(2, 2);
            if (!ta.LoadImage(File.ReadAllBytes(a)) || !tb.LoadImage(File.ReadAllBytes(b))) { Debug.LogWarning(Tag + " a picture could not be read"); return; }
            int w = Mathf.Max(ta.width, tb.width), ha = Mathf.RoundToInt(ta.height * share), hb = Mathf.RoundToInt(tb.height * share), gap = 8;
            var o = new Texture2D(w, ha + hb + gap, TextureFormat.RGB24, false);
            var fill = new Color32[w * (ha + hb + gap)]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(20, 20, 20, 255);
            o.SetPixels32(fill);
            o.SetPixels(0, hb + gap, ta.width, ha, ta.GetPixels(0, ta.height - ha, ta.width, ha));      // a on top (textures count rows from the bottom)
            o.SetPixels(0, 0, tb.width, hb, tb.GetPixels(0, tb.height - hb, tb.width, hb));
            o.Apply();
            File.WriteAllBytes(outPath, o.EncodeToPNG());
            Object.DestroyImmediate(ta); Object.DestroyImmediate(tb); Object.DestroyImmediate(o);
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
