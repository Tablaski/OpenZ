using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// OzV12EdgeShots.Run(label): in Play on a level, the RTS camera at its widest height over the middle, the four corners
    /// and the four edge middles; per view one picture plus the frame time (mean of 120 frames) and the editor's render
    /// Editor only, changes nothing in the game.
    /// </summary>
    public static class OzV12EdgeShots
    {
        const string Tag = "[OzV12EdgeShots]";
        static readonly string Out = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/l16_surround/shots"));
        static readonly Queue<System.Action> steps = new Queue<System.Action>();
        static double nextAt;
        static int frames; static float ms; static bool measuring;
        static readonly List<string> lines = new List<string>();

        public static void Run(string label)
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null) { Debug.LogWarning(Tag + " start Play on a level first"); return; }
            var cam = Object.FindFirstObjectByType<RtsCamera>();
            if (!cam) return;
            Directory.CreateDirectory(Out);
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            float W = ZGame.I.World.Map.WidthPix / 8f, H = ZGame.I.World.Map.HeightPix / 8f;
            var views = new (string name, float x, float z)[]
            {
                ("middle", W * 0.5f, -H * 0.5f), ("nw", 0f, 0f), ("ne", W, 0f), ("sw", 0f, -H), ("se", W, -H),
                ("n", W * 0.5f, 0f), ("s", W * 0.5f, -H), ("w", 0f, -H * 0.5f), ("e", W, -H * 0.5f),
            };
            lines.Clear(); lines.Add($"{scene} {label} | widest height {cam.maxHeight} | view | mean ms (120 frames) | batches | setpass | tris");
            steps.Clear();
            foreach (var v in views)
            {
                steps.Enqueue(() => { cam.focus = new Vector3(v.x, 0f, v.z); cam.height = cam.maxHeight; cam.ZoomTo(cam.maxHeight); nextAt = EditorApplication.timeSinceStartup + 2.5; });
                steps.Enqueue(() => { frames = 0; ms = 0f; measuring = true; nextAt = double.MaxValue; });
                steps.Enqueue(() =>
                {
                    string p = $"{Out}/{scene}_{label}_{v.name}.png";
                    ScreenCapture.CaptureScreenshot(p);
                    lines.Add($"{v.name} | focus {cam.focus.x:0},{cam.focus.z:0} | {ms / Mathf.Max(1, frames):0.00} | {UnityStats.batches} | {UnityStats.setPassCalls} | {UnityStats.triangles}");
                    nextAt = EditorApplication.timeSinceStartup + 1.0;
                });
            }
            steps.Enqueue(() => { File.WriteAllLines($"{Out}/EDGE_{scene}_{label}.txt", lines); Debug.Log($"{Tag} {scene} {label}: done\n" + string.Join("\n", lines)); });
            nextAt = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!Application.isPlaying) { steps.Clear(); EditorApplication.update -= Tick; return; }
            if (measuring)
            {
                frames++; ms += Time.unscaledDeltaTime * 1000f;
                if (frames >= 120) { measuring = false; nextAt = 0; }
                return;
            }
            if (EditorApplication.timeSinceStartup < nextAt) return;
            if (steps.Count == 0) { EditorApplication.update -= Tick; return; }
            steps.Dequeue()();
        }
    }
}
