using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;

using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.2/Flag Shots (this level, Play): in Play on a level, one neutral, one red and one blue flag (the nearest
    /// to the map's middle of each), the game's RTS camera at its normal 70 m height centred on each flag, at day (clock 0.08)
    /// and at night (clock 0.62, the clock frozen so the NIGHT &amp; RAIN option does not ease it back), plus one wide 140 m
    /// Editor only, changes nothing in the game.
    /// </summary>
    public static class OzV12FlagShots
    {
        const string Tag = "[OzV12FlagShots]";
        static readonly string Out = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/flags_ideas/current"));
        static readonly Queue<System.Action> steps = new Queue<System.Action>();
        static double nextAt;

        [MenuItem("OpenZ/V1.2/Flag Shots (this level, Play)")]
        public static void Run()
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null) { Debug.LogWarning(Tag + " start Play on a level first"); return; }
            var cam = Object.FindFirstObjectByType<RtsCamera>(); var dn = Object.FindFirstObjectByType<DayNight>();
            if (!cam || !dn) { Debug.LogWarning(Tag + " no RtsCamera / DayNight in this scene"); return; }
            Directory.CreateDirectory(Out);
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var w = ZGame.I.World;
            float mx = w.Map.WidthPix * 0.5f, my = w.Map.HeightPix * 0.5f;
            var picks = new List<(string name, ZObject f)>();
            foreach (var team in new[] { Team.Neutral, Team.Red, Team.Blue })
            {
                var f = w.Objects.Where(o => o.Kind == ZKind.MapItem && o.Type == "flag" && o.Owner == team)
                                 .OrderBy(o => (o.CenterX - mx) * (o.CenterX - mx) + (o.CenterY - my) * (o.CenterY - my)).FirstOrDefault();
                if (f != null) picks.Add((team.ToString().ToLowerInvariant(), f));
            }
            Debug.Log($"{Tag} {scene}: flags neutral/red/blue found: {string.Join(", ", picks.Select(p => p.name + " at " + p.f.CenterX / 16 + "," + p.f.CenterY / 16))}");
            steps.Clear();
            foreach (var (time, label) in new[] { (0.08f, "day"), (0.62f, "night") })
            {
                steps.Enqueue(() => { dn.running = false; NightLights.JumpTo(time); nextAt = EditorApplication.timeSinceStartup + 3.0; });
                foreach (var (name, f) in picks)
                {
                    var at = ZGame.PxToWorld(f.CenterX, f.CenterY);
                    steps.Enqueue(() => { cam.focus = new Vector3(at.x, 0f, at.z); cam.height = 70f; cam.ZoomTo(70f); nextAt = EditorApplication.timeSinceStartup + 2.5; });
                    string p = $"{Out}/{scene}_{name}_{label}.png";
                    steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(p); Debug.Log($"{Tag} picture {p} ({Screen.width}x{Screen.height}, clock {dn.timeOfDay:F3})"); nextAt = EditorApplication.timeSinceStartup + 1.0; });
                }
                if (picks.Count > 0)
                {
                    var c = ZGame.PxToWorld(picks.Average(p => p.f.CenterX), picks.Average(p => p.f.CenterY));
                    steps.Enqueue(() => { cam.focus = new Vector3(c.x, 0f, c.z); cam.height = 140f; cam.ZoomTo(140f); nextAt = EditorApplication.timeSinceStartup + 3.0; });
                    string pw = $"{Out}/{scene}_wide_{label}.png";
                    steps.Enqueue(() => { ScreenCapture.CaptureScreenshot(pw); Debug.Log($"{Tag} picture {pw}"); nextAt = EditorApplication.timeSinceStartup + 1.0; });
                }
            }
            steps.Enqueue(() => { cam.ZoomTo(70f); Debug.Log($"{Tag} {scene}: done"); });
            nextAt = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
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
