using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.2, level 16 rounds 5d / 5e (the plants round the map "visually each same size as factory"): the Play check and its
    /// pictures. Menu OpenZ/V1.2/L16 rim sizes: check + pictures (Play), in Play on level 16 once the land is shown (the start flyover
    /// over). The round is read from the switches (r5b: OzL16Outskirts.RimFixOn off; r5e: on; r5efull: on without the cells' cost limit;
    /// r5d: on without the edge plants' new look; r4 / r5 as OzV12FarTreesShots names them) and named in every file
    ///  - CHECK_&lt;round&gt;.txt: Status(), TreeCheck(), FarLookCheck(), RimFixCheck() (the size audit: every crown outside the map and
    ///    every plant on the map's cliff tops and rims within 12 m of its line against the cap and a factory), the distance rule, the far
    ///    shadows, the low look;
    ///  - FARVIEWS_&lt;round&gt;.txt: the nine widest-zoom views of OzV12EdgeShots and three lower wide-overview views over the map
    ///    with the north rim just above the picture (116 / 125 / 140 m), per view the plant cells' far meshes the camera's view meets
    ///    and their triangles (counted from the meshes), the plants outside the map and the edge plants' stand-ins;
    ///  - the owner's picture's view (156 m over the west rim, focus 49.1 / -209.3; the widest zoom's side cut stands back for it) and
    ///    the same at 165 m, OWNER_VS_&lt;round&gt;.png: his picture (his old view and the round 5b view) beside the same part of the new
    ///    one at the same scale;
    ///  - the same place at 70 (focus 21 / -209.3: the west rim), 100, 113, 119 m (the plant cells change to their far meshes at 116 m:
    ///    the plants outside the map and the edge plants' stand-ins do not), at 70 m the west and the north rim (the map's own trees
    ///    beside the edge plants and the ones outside it, full size), the north side widest and at 165 m.
    /// Menu OpenZ/V1.2/L16 rim sizes: edge felling check (Play): three edge plants of the rules' felling list broken by the view (their
    /// own break): FELL_&lt;round&gt;.txt (their stand-ins tipped with them, the stumps cut from the stand-ins, the world's hash in the
    /// frame of the break and right after it), a 70 m picture over the first while it falls and after it.
    /// Menu OpenZ/V1.2/L16 rim sizes: flyover frames (Play): armed right after Play starts (or before an ending), a full-screen frame
    /// every 2.5 s while the low look holds, FLY_&lt;round&gt;_NN.png.
    /// Menu OpenZ/V1.2/L16 rim sizes: world hash 45-50 s (Play): from the level start, hands off: the world's hash at every tick between
    /// 45 and 50 s of the rules' time, WORLD_&lt;round&gt;.txt (the same lines in r5e and r5b: the kit changes nothing in the rules).
    /// Editor only, changes nothing in the game (the camera's limits are given back at the end).
    /// </summary>
    public static class OzV12RimSizesShots
    {
        const string Tag = "[OzV12RimSizesShots]";
        const string OwnerPictureName = "owner_factory_sized_trees_r5b.png";
        static string ownerPicture;
        /// <summary>
        /// (the kit's owner_refs; looked for once). Empty = not found (the pair is left out).</summary>
        static string OwnerPicture
        {
            get
            {
                if (ownerPicture != null) return ownerPicture;
                var near = Path.Combine(Out, "owner_refs", OwnerPictureName);
                if (File.Exists(near)) return ownerPicture = near;
                var staging = Path.Combine(OpenZPaths.RepoRoot, "work", "staging");
                try { var f = Directory.Exists(staging) ? Directory.GetFiles(staging, OwnerPictureName, SearchOption.AllDirectories) : new string[0]; ownerPicture = f.Length > 0 ? f[0] : ""; }
                catch (System.Exception) { ownerPicture = ""; }
                return ownerPicture;
            }
        }
        static readonly Queue<System.Action> steps = new Queue<System.Action>();
        static double nextAt;
        static readonly List<string> lines = new List<string>();

        static string Out
        {
            get { var p = Path.Combine(OpenZPaths.Records, "milestones", "l16_rim_sizes_v12"); Directory.CreateDirectory(p); return p; }
        }

        public static string Round => !OzL16Outskirts.TreesOn ? "r4" : !OzL16Outskirts.FarLookOn ? "r5" : !OzL16Outskirts.RimFixOn ? (OzL16Outskirts.FarLookMapTrees ? "r5bmap" : "r5b")
            : !OzL16Outskirts.RimEdgeOn ? (OzL16Outskirts.RimBudgetOn ? "r5d" : "r5dfull") : OzL16Outskirts.RimBudgetOn ? "r5e" : "r5efull";

        [MenuItem("OpenZ/V1.2/L16 rim sizes: check + pictures (Play)")]
        public static void Menu() => Run();

        public static string Run(string label = null)
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null || ZGame.I.level != OzL16Outskirts.Level) { Debug.LogWarning(Tag + " start Play on level 16 first"); return "not in Play on level 16"; }
            if (!OzL16Outskirts.I || OzL16Outskirts.BuildMs <= 0f || OzL16FlyoverFlora.LowLookOn) { Debug.LogWarning(Tag + " the land is not shown yet (wait for the start flyover to end)"); return "land not shown yet"; }
            var cam = Object.FindFirstObjectByType<RtsCamera>(); if (!cam) return "no RTS camera";
            string dir = Out, r = string.IsNullOrEmpty(label) ? Round : label;
            string check = OzL16Outskirts.Status() + "\n" + OzL16Outskirts.TreeCheck() + "\n" + OzL16Outskirts.FarLookCheck() + "\n" + OzL16Outskirts.RimFixCheck() + "\n" + OzL16DrawAhead.Status() + "\n" + OzL16FarShadows.Status() + "\n" + OzL16FlyoverFlora.Status() + "\n";
            File.WriteAllText(Path.Combine(dir, "CHECK_" + r + ".txt"), check);
            Debug.Log(Tag + " " + check);
            float W = ZGame.I.World.Map.WidthPix / 8f, H = ZGame.I.World.Map.HeightPix / 8f;
            var views = new (string name, float x, float z)[]
            {
                ("middle", W * 0.5f, -H * 0.5f), ("nw", 0f, 0f), ("ne", W, 0f), ("sw", 0f, -H), ("se", W, -H),
                ("n", W * 0.5f, 0f), ("s", W * 0.5f, -H), ("w", 0f, -H * 0.5f), ("e", W, -H * 0.5f),
            };
            steps.Clear(); lines.Clear();
            lines.Add($"L16 rim sizes, round {r} | widest height {cam.maxHeight} | view | focus | plant cells' far meshes in the view (counted from the meshes) | the plants outside the map and the edge plants' stand-ins");
            foreach (var v in views)
            {   // the same places as OzV12EdgeShots (the camera's own limits hold)
                steps.Enqueue(() => { cam.focus = new Vector3(v.x, 0f, v.z); cam.height = cam.maxHeight; cam.ZoomTo(cam.maxHeight); nextAt = EditorApplication.timeSinceStartup + 2.5; });
                steps.Enqueue(() => { lines.Add($"{v.name} | {cam.focus.x:0},{cam.focus.z:0} | {OzL16Outskirts.FarViewCount()} | {OzL16Outskirts.RimViewCount()}"); nextAt = EditorApplication.timeSinceStartup + 0.2; });
            }
            // the wide overview lower down, over the map with the north rim just above the picture (tools/culling_boxes.py), the wide
            // overview still on (it goes off only under 104 m)
            foreach (var v in new (string name, float h, float x, float z)[] { ("north_off_116", 116f, 84f, -61f), ("north_off_125", 125f, 90f, -66f), ("north_off_140", 140f, 83f, -74f) })
            {
                steps.Enqueue(() => { OzL16Outskirts.OwnerView(v.h, v.x, v.z); nextAt = EditorApplication.timeSinceStartup + 2.5; });
                steps.Enqueue(() => { lines.Add($"{v.name} | {cam.focus.x:0},{cam.focus.z:0} | {OzL16Outskirts.FarViewCount()} | {OzL16Outskirts.RimViewCount()}"); nextAt = EditorApplication.timeSinceStartup + 0.2; });
            }
            steps.Enqueue(() => { File.WriteAllLines(Path.Combine(dir, "FARVIEWS_" + r + ".txt"), lines); nextAt = 0; });
            // the owner's view and the same place nearer (the widest zoom's side cut stands back for these pictures)
            string owner = Path.Combine(dir, $"L16_rim_sizes_owner_view_156m_{r}.png");
            Shot(() => OzL16Outskirts.OwnerView(156f), owner, lines, "owner view 156 m");
            Shot(() => OzL16Outskirts.OwnerView(165f, 49.5f, -208.5f), Path.Combine(dir, $"L16_rim_sizes_owner_view_165m_{r}.png"), lines, "owner view 165 m");
            steps.Enqueue(() => { Pair(OwnerPicture, owner, Path.Combine(dir, $"OWNER_VS_{r}.png")); nextAt = 0; });
            Shot(() => OzL16Outskirts.OwnerView(70f, 21f, -209.3f), Path.Combine(dir, $"L16_rim_sizes_west_rim_70m_{r}.png"), lines, "west rim 70 m (focus 21 / -209.3)");
            foreach (float h in new[] { 100f, 113f, 119f })
            {
                float hh = h;
                Shot(() => OzL16Outskirts.OwnerView(hh), Path.Combine(dir, $"L16_rim_sizes_west_{hh:0}m_{r}.png"), lines, $"west {hh:0} m");
            }
            // the map's own trees beside the edge plants and the ones outside it at the normal zoom (full size), the north side wide
            Shot(() => OzL16Outskirts.OwnerView(70f, 22f, -150f), Path.Combine(dir, $"L16_rim_sizes_west_edge_70m_{r}.png"), lines, "west edge 70 m");
            Shot(() => OzL16Outskirts.OwnerView(70f, 104f, 4f), Path.Combine(dir, $"L16_rim_sizes_north_edge_70m_{r}.png"), lines, "north edge 70 m");
            Shot(() => OzL16Outskirts.OwnerView(165f, 104f, -40f), Path.Combine(dir, $"L16_rim_sizes_north_165m_{r}.png"), lines, "north 165 m (focus 104 / -40)");
            steps.Enqueue(() => { OzL16Outskirts.OwnerView(0f); OzL16Outskirts.Frame("north", cam.maxHeight); nextAt = EditorApplication.timeSinceStartup + 2.5; });
            steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"L16_rim_sizes_north_widest_{r}.png")); lines.Add($"north widest: {OzL16Outskirts.FarViewCount()} | {OzL16Outskirts.RimViewCount()}"); nextAt = EditorApplication.timeSinceStartup + 1.5; });
            steps.Enqueue(() =>
            {
                OzL16Outskirts.OwnerView(0f);
                File.WriteAllLines(Path.Combine(dir, "FARVIEWS_" + r + ".txt"), lines);
                Debug.Log(Tag + " done: " + dir);
            });
            nextAt = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return "running: " + dir;
        }

        [MenuItem("OpenZ/V1.2/L16 rim sizes: edge felling check (Play)")]
        public static void FellMenu() => Fell();

        public static string Fell()
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.level != OzL16Outskirts.Level || !OzL16Outskirts.I || OzL16FlyoverFlora.LowLookOn) { Debug.LogWarning(Tag + " Play on level 16, the land shown"); return "not ready"; }
            string dir = Out, r = Round; var log = new List<string>();
            steps.Clear();
            long before = OzL16Outskirts.RimWorldHash();
            string fell = OzL16Outskirts.RimEdgeFell(3);
            long same = OzL16Outskirts.RimWorldHash();
            log.Add(fell); log.Add($"world hash in the frame of the break: before {before}, after {same}: {(before == same ? "the same (the view's break changes nothing in the rules)" : "DIFFERENT")}");
            Debug.Log(Tag + " " + fell);
            var first = FirstFelled();
            if (first.HasValue)
            {
                var f = first.Value;
                steps.Enqueue(() => { OzL16Outskirts.OwnerView(70f, f.x, f.z - 20f); nextAt = EditorApplication.timeSinceStartup + 1.2; });
                steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"L16_rim_sizes_fell_falling_{r}.png")); nextAt = EditorApplication.timeSinceStartup + 5.5; });
                steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"L16_rim_sizes_fell_down_{r}.png")); nextAt = EditorApplication.timeSinceStartup + 0.5; });
            }
            else steps.Enqueue(() => { nextAt = EditorApplication.timeSinceStartup + 7; });
            steps.Enqueue(() =>
            {
                log.Add(OzL16Outskirts.RimEdgeFellResult());
                log.Add(OzTreeFall.LastLog);
                OzL16Outskirts.OwnerView(0f);
                File.WriteAllLines(Path.Combine(dir, "FELL_" + r + ".txt"), log);
                Debug.Log(Tag + " felling check done: " + string.Join(" | ", log));
            });
            nextAt = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return "running: " + dir;
        }

        static Vector3? FirstFelled()
        {
            var o = OzL16Outskirts.I; if (!o) return null;
            var root = GameObject.Find(OzL16Outskirts.FloraRoot); if (!root) return null;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf && mf.name.StartsWith("EDGE_") && mf.transform.parent && mf.transform.parent.GetComponent<DetailProp>() is DetailProp dp && dp.Broken) return mf.transform.position;
            return null;
        }

        static int flyN; static double flyNext; static bool flySeen;

        [MenuItem("OpenZ/V1.2/L16 rim sizes: flyover frames (Play)")]
        public static void FlyMenu()
        {   // arm it right after Play starts: a full-screen frame every 2.5 s while the start flyover's low look holds (FLY_<round>_NN.png)
            if (!Application.isPlaying) { Debug.LogWarning(Tag + " start Play on level 16 first, then arm this at once"); return; }
            flyN = 0; flyNext = 0; flySeen = false;
            EditorApplication.update -= FlyTick; EditorApplication.update += FlyTick;
            Debug.Log(Tag + " flyover frames armed");
        }

        static void FlyTick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= FlyTick; return; }
            bool low = OzL16FlyoverFlora.LowLookOn;
            if (low) flySeen = true;
            if (flySeen && !low) { EditorApplication.update -= FlyTick; Debug.Log(Tag + " flyover frames done: " + flyN); return; }
            if (!low || EditorApplication.timeSinceStartup < flyNext) return;
            flyNext = EditorApplication.timeSinceStartup + 2.5;
            ScreenCapture.CaptureScreenshot(Path.Combine(Out, $"FLY_{Round}_{flyN++:00}.png"));
        }

        static readonly List<string> hashLines = new List<string>();
        static double lastTime = -1;

        [MenuItem("OpenZ/V1.2/L16 rim sizes: world hash 45-50 s (Play)")]
        public static void HashMenu()
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null) { Debug.LogWarning(Tag + " start Play on level 16 first"); return; }
            hashLines.Clear(); lastTime = -1; hashLines.Add($"L16 world hash, round {Round}, rules' time | hash (every tick seen between 45 and 50 s; hands off from the level start)");
            EditorApplication.update -= HashTick; EditorApplication.update += HashTick;
        }

        static void HashTick()
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null) { EditorApplication.update -= HashTick; return; }
            double t = ZGame.I.World.Time;
            if (t >= 45.0 && t <= 50.0 && t != lastTime) { lastTime = t; hashLines.Add($"{t:0.000} | {OzL16Outskirts.RimWorldHash()}"); }
            if (t > 50.0)
            {
                EditorApplication.update -= HashTick;
                File.WriteAllLines(Path.Combine(Out, "WORLD_" + Round + ".txt"), hashLines);
                Debug.Log(Tag + " world hash 45-50 s written: " + hashLines.Count + " lines");
            }
        }

        static void Shot(System.Func<string> place, string path, List<string> log, string what)
        {
            steps.Enqueue(() => { log.Add(what + ": " + place()); nextAt = EditorApplication.timeSinceStartup + 2.5; });
            steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(path); log.Add(what + ": " + OzL16Outskirts.FarViewCount() + " | " + OzL16Outskirts.RimViewCount()); nextAt = EditorApplication.timeSinceStartup + 1.5; });
        }

        /// <summary>The owner's picture (his old view | the round 5b view: the left 1374 x 1446 of a 2560 x 1600 frame at 0.70) beside the
        /// same part of the new frame at the same scale.</summary>
        static void Pair(string a, string b, string outPath)
        {
            if (!File.Exists(a) || !File.Exists(b)) { Debug.LogWarning(Tag + " picture missing for the pair: " + (File.Exists(a) ? b : a)); return; }
            var ta = new Texture2D(2, 2); var tb = new Texture2D(2, 2);
            if (!ta.LoadImage(File.ReadAllBytes(a)) || !tb.LoadImage(File.ReadAllBytes(b))) { Debug.LogWarning(Tag + " a picture could not be read"); return; }
            int cw = Mathf.RoundToInt(tb.width * 1374f / 2560f), ch = Mathf.RoundToInt(tb.height * 1446f / 1600f), gap = 12;
            int ow = Mathf.RoundToInt(ta.height * (float)cw / ch);                                        // the new part at the owner's picture's height
            var o = new Texture2D(ta.width + gap + ow, ta.height, TextureFormat.RGB24, false);
            var fill = new Color32[o.width * o.height]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(20, 20, 20, 255);
            o.SetPixels32(fill);
            o.SetPixels(0, 0, ta.width, ta.height, ta.GetPixels());
            for (int y = 0; y < ta.height; y++)                                                            // textures count rows from the bottom: the frame's top rows
                for (int x = 0; x < ow; x++)
                {
                    float u = (x + 0.5f) / ow * cw, v = tb.height - ch + (y + 0.5f) / ta.height * ch;
                    o.SetPixel(ta.width + gap + x, y, tb.GetPixelBilinear(u / tb.width, v / tb.height));
                }
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
