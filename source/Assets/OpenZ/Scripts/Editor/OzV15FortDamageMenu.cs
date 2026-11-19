using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 fort_damage: the stage preview for the owner's approval (Play mode, any level with forts) and the fort health count (headless).
    /// The preview frames the fort with the normal RTS camera (pitch as set, 60 m up), sets its health 100 / 75 / 50 / 25 / 5 % in turn (the sim's
    /// own SetHealth: a test tool, never in a game), waits until the view has eased to the new health and the falling parts have landed, and
    /// saves one Game view frame per stage to &lt;project&gt;/Captures/FortDamage/ (outside Assets: nothing is imported). The sweep runs the health
    /// down from 100 % to 5 % over 40 s for an OBS clip of the stages easing into each other. Back to full health shows the reverse (the marks
    /// fade out; the fallen parts stay where they lie until they pop out).
    /// </summary>
    public static class OzV15FortDamageMenu
    {
        const string Root = "OpenZ/V1.5/Fort Damage/";
        static readonly float[] Stages = { 1.00f, 0.75f, 0.50f, 0.25f, 0.05f };
        const float Hold = 3.5f, SweepSeconds = 40f;

        static ZObject PickFort(bool mine)
        {
            var g = ZGame.I; if (!g || g.World == null) return null;
            return g.World.Objects.Where(o => o.IsFort && !o.Destroyed && (mine ? o.Owner == g.player : o.Owner != g.player && o.Owner != Team.Neutral)).OrderBy(o => o.Id).FirstOrDefault();
        }

        static bool Ready(out string why)
        {
            why = !Application.isPlaying ? "enter Play mode on a level first" : !ZGame.I || ZGame.I.World == null ? "no level running" : !OzFortDamage.I ? "the fort damage view is not running" : null;
            return why == null;
        }

        static void Frame(ZObject fort)
        {
            var cam = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
            if (!cam) return;
            var c = ZGame.I.CenterWorld(fort);
            cam.focus = new Vector3(c.x, 0f, c.z); cam.height = 60f;
        }

        static string Folder()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "FortDamage"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ------------------------------------------------------------------ the stage preview
        [MenuItem(Root + "Preview stages: enemy fort (Play, saves frames)")]
        static void PreviewEnemy() => Preview(false);
        [MenuItem(Root + "Preview stages: my fort (Play, saves frames)")]
        static void PreviewMine() => Preview(true);
        [MenuItem(Root + "Sweep enemy fort 100 -> 5 % over 40 s (Play, for an OBS clip)")]
        static void Sweep() => Run(false, true);
        [MenuItem(Root + "Enemy fort back to full health (Play)")]
        static void Heal() { if (!Ready(out var why)) { Debug.LogWarning("[OpenZ V1.5] fort damage: " + why); return; } var f = PickFort(false); if (f != null) { f.SetHealth(f.MaxHealth); Debug.Log($"[OpenZ V1.5] fort damage: {f.Type} #{f.Id} back to {f.Health}/{f.MaxHealth}"); } }
        [MenuItem(Root + "Enemy fort -10 % (Play)")]
        static void Step() { if (!Ready(out var why)) { Debug.LogWarning("[OpenZ V1.5] fort damage: " + why); return; } var f = PickFort(false); if (f != null) { f.SetHealth(Math.Max(1, f.Health - f.MaxHealth / 10)); Frame(f); Debug.Log($"[OpenZ V1.5] fort damage: {f.Type} #{f.Id} now {f.Health}/{f.MaxHealth}"); } }
        [MenuItem(Root + "Report (Play)")]
        static void Report() => Debug.Log("[OpenZ V1.5] " + OzFortDamage.Report());
        [MenuItem(Root + "Fort health count (headless)")]
        static void Count() => OzV11Kit.RunMenu("V1.5 fort health count (headless)", OzV15FortHealthCount.Run);

        static void Preview(bool mine) => Run(mine, false);

        static void Run(bool mine, bool sweep)
        {
            if (!Ready(out var why)) { Debug.LogWarning("[OpenZ V1.5] fort damage preview: " + why); return; }
            var fort = PickFort(mine);
            if (fort == null) { Debug.LogWarning("[OpenZ V1.5] fort damage preview: no standing fort of that side"); return; }
            EditorApplication.update -= Tick;
            job = new Job { fort = fort, sweep = sweep, folder = sweep ? null : Folder(), t0 = Time.realtimeSinceStartup, i = -1 };
            Frame(fort);
            fort.SetHealth(fort.MaxHealth);
            EditorApplication.update += Tick;
            Debug.Log($"[OpenZ V1.5] fort damage {(sweep ? "sweep" : "preview")}: {fort.Type} #{fort.Id} ({fort.Owner}) on {ZGame.I.level}" + (sweep ? $", 100 -> 5 % over {SweepSeconds:0} s" : $", frames to {job.folder}"));
        }

        sealed class Job { public ZObject fort; public bool sweep; public string folder; public float t0, waitFrom; public int i; public bool shot; public readonly List<string> lines = new List<string>(); }
        static Job job;

        static void Tick()
        {
            if (job == null || !Application.isPlaying || job.fort == null || job.fort.Destroyed || !OzFortDamage.I) { Stop("stopped (Play ended or the fort fell)"); return; }
            var f = job.fort; float now = Time.realtimeSinceStartup;
            if (job.sweep)
            {
                float k = Mathf.Clamp01((now - job.t0) / SweepSeconds);
                f.SetHealth(Mathf.Max(1, Mathf.RoundToInt(f.MaxHealth * Mathf.Lerp(1f, 0.05f, k))));
                if (k >= 1f) Stop("sweep done: " + OzFortDamage.Report());
                return;
            }
            if (job.i < 0) { job.i = 0; Set(f, Stages[0]); job.waitFrom = now; return; }
            float target = 1f - Stages[job.i];
            bool eased = Mathf.Abs(OzFortDamage.Shown(f.Id) - target) < 0.002f;
            if (!eased) { job.waitFrom = now; return; }
            if (!job.shot && now - job.waitFrom >= Hold)
            {   // one Game view frame of this stage (the normal RTS camera)
                string file = Path.Combine(job.folder, $"fort_{ZGame.I.level}_{f.Type}_{f.Id}_{Mathf.RoundToInt(Stages[job.i] * 100):000}pct.png");
                ScreenCapture.CaptureScreenshot(file, 1);
                job.lines.Add($"{Stages[job.i] * 100:0} %: {file}");
                job.shot = true; job.waitFrom = now;
                return;
            }
            if (job.shot && now - job.waitFrom >= 0.6f)
            {
                job.i++; job.shot = false;
                if (job.i >= Stages.Length) { Stop("preview done:\n  " + string.Join("\n  ", job.lines) + "\n" + OzFortDamage.Report()); return; }
                Set(f, Stages[job.i]); job.waitFrom = now;
            }
        }

        static void Set(ZObject f, float pct)
        {
            f.SetHealth(Mathf.Max(1, Mathf.RoundToInt(f.MaxHealth * pct)));
            Frame(f);
            Debug.Log($"[OpenZ V1.5] fort damage preview: {f.Type} #{f.Id} set to {pct * 100:0} % ({f.Health}/{f.MaxHealth})");
        }

        static void Stop(string why)
        {
            EditorApplication.update -= Tick;
            if (job != null) Debug.Log("[OpenZ V1.5] fort damage: " + why);
            job = null;
        }
    }
}
