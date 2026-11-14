#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 4 (rule DV155, Sim/ZWorld.RuinProduction.cs) Play frames: in a level in Play (full look, normal RTS camera), menu OpenZ/V1.3/Rules:
    /// Item 4 Destroyed Factory Frames. It takes the computer's first factory that is building, flies the camera to it, destroys it in the rules
    /// (SetHealth 0, as a kill does), runs the game at 4x until one whole build time after its rebuild, and saves full-screen frames: the blast
    /// (+0.5 s, +3 s), the ruin (+60 s, +300 s game time), 2 s before the rebuild, the rebuild, +2 s and +10 s after it. It counts the units that
    /// came out of it (want 0 while a ruin and 0 in the 2 s after the rebuild). Speed and camera are put back. Test only, nothing saved but the frames.
    /// Output: <see cref="Folder"/>.
    /// </summary>
    public sealed class OzRuinBuildWatch : MonoBehaviour
    {
        public static string Report = "idle";
        public static string Folder => OzRelease.Milestones("v13_item04_ruin_build");
        public static void Run() { Report = "running"; new GameObject("OzRuinBuildWatch").AddComponent<OzRuinBuildWatch>(); }

        readonly StringBuilder sb = new StringBuilder();
        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzRuinBuildWatch] " + t); }

        IEnumerator Start()
        {
            var g = ZGame.I; var W = g ? g.World : null;
            if (W == null) { Report = "no game"; Destroy(gameObject); yield break; }
            Directory.CreateDirectory(Folder);
            ZObject b = null;
            foreach (var o in W.Objects) if (o.IsProducer && !o.IsFort && o.Owner == Team.Blue && o.Building && !o.Destroyed) { b = o; break; }
            if (b == null) { Report = "FAIL no blue factory is building now (start later in the match)"; Log(Report); Destroy(gameObject); yield break; }
            var rts = FindFirstObjectByType<RtsCamera>(); var focus0 = rts ? rts.focus : Vector3.zero; float speed0 = g.gameSpeed;
            var at = g.CenterWorld(b);
            if (rts) rts.FlyTo(at);
            yield return new WaitForSecondsRealtime(2f);
            int outRuin = 0, outFirst2 = 0, outAfter = 0; double kill = W.Time, revived = -1, start = -1, span = -1;
            Log($"rule {(ZWorld.RuinNoBuildDeviation ? "on" : "off")}: {b.Type} #{b.Id} building {b.CurUnit} ({b.ProductionProgress * 100:0} % done), destroyed at {W.Time:0.0} s");
            b.SetHealth(0); W.UpdateObjectHealth(b);
            g.gameSpeed = 4f;
            var shots = new[] { (0.5, "a_blast"), (3.0, "b_blast_3s"), (60.0, "c_ruin_60s"), (300.0, "d_ruin_300s") };
            int next = 0; bool preShot = false, revShot = false, post2 = false, post10 = false;
            int built = b.BuiltCount + b.StoredCannons.Count;                                       // read every frame: the rules' own count (events may come several ticks a frame)
            while (true)
            {
                if (revived < 0 && !b.Destroyed) revived = W.Time;
                int now = b.BuiltCount + b.StoredCannons.Count;
                if (now > built)
                {
                    int n = now - built;
                    if (revived < 0) outRuin += n; else { outAfter += n; if (W.Time - revived <= 2.0) outFirst2 += n; }
                }
                built = now;
                if (revived >= 0 && start < 0 && b.Building) { start = b.BInit; span = b.BFinal - b.BInit; }
                double since = W.Time - kill;
                if (next < shots.Length && since >= shots[next].Item1) { Shot(shots[next].Item2); next++; }
                if (!preShot && b.DoAutoRepair && b.NextAutoRepairTime - W.Time <= 2.0) { preShot = true; Shot("e_rebuild_minus2s"); }
                if (revived >= 0 && !revShot) { revShot = true; Shot("f_rebuilt"); }
                if (revived >= 0 && !post2 && W.Time - revived >= 2) { post2 = true; Shot("g_rebuilt_plus2s"); }
                if (revived >= 0 && !post10 && W.Time - revived >= 10) { post10 = true; Shot("h_rebuilt_plus10s"); }
                bool done = revived >= 0 && post10 && (start < 0 ? W.Time - revived > 120 : W.Time > start + span + 2);
                if (done || since > 760 || W.GameOver) break;
                yield return null;
            }
            g.gameSpeed = speed0; if (rts) rts.FlyTo(focus0);
            bool pass = outRuin == 0 && outFirst2 == 0 && revived >= 0;
            Report = $"{(pass ? "PASS" : "FAIL")} units out while a ruin {outRuin}, in the 2 s after the rebuild {outFirst2}, after it {outAfter}; rebuilt {(revived < 0 ? "no" : (revived - kill).ToString("0.0") + " s after the blast")}; " +
                     $"next unit started {(start < 0 ? "-" : (start - revived).ToString("0.0") + " s after the rebuild")}, build time {span:0.0} s";
            Log(Report);
            File.WriteAllText(Path.Combine(Folder, "report.txt"), sb.ToString());
            Destroy(gameObject);
        }

        void Shot(string name)
        {
            var p = Path.Combine(Folder, name + ".png");
            ScreenCapture.CaptureScreenshot(p);
            Log("frame " + p);
        }
    }
}
#endif
