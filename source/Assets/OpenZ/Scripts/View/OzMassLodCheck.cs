#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 "Mass battles faster" (editor only): the counted Play check of View/OzMassLod.cs, menu OpenZ/V1.3/Unit Editor/M Mass Battles
    /// Check (Play, the unit editor open; Editor/OzMassLodMenu.cs). The switching rules are fed a frame time (OzMassLod.TestFrame), the
    /// units are real (the cap bench's armies, View/OzUePlayground.Cap.cs BenchArmy):
    ///  M1 option OFF, 300 units, slow frames (40 ms) for 6 s: never in (the default changes nothing);
    ///  M2 option ON, 60 units, slow frames for 6 s: never in (the owner's 70-unit floor);
    ///  M3 option ON, 300 units, slow frames: in after 2 .. 4 s;
    ///  M4 smooth frames (10 ms): out after 5 .. 7 s;
    ///  M5 in again, then the field cut to 60 units: out within 1 s;
    ///  M6 while it is out, OzMassLod.SkipLegIk is false for every robot view (nothing simpler);
    ///  M7 the real gain: 200 a side at x1, 15 s with the lighter look held in and 15 s held out (OzMassLod.Force): average fps, main thread
    ///     ms, robots whose foot IK rested (report only; the bench decides further pieces).
    /// The option's saved value, the field, the speed and PLAY / PAUSE are put back. Folder (optional): report.txt.
    /// </summary>
    public sealed class OzMassLodCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ MassLodCheck]";
        public static string Report = "not run", Folder;
        public static bool Running => run;
        static OzMassLodCheck run;
        int pass, fail; readonly StringBuilder lines = new StringBuilder();

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_MassLodCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzMassLodCheck>();
        }

        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { if (run == this) run = null; OzMassLod.TestFrame = null; OzMassLod.Force = null; }
        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static IEnumerator Seconds(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        static void Field(int perSide)
        {
            OzUePlayground.ClearField();
            var w = OzUnitEditor.I.World;
            Vector3 c = new Vector3(w.Map.width, 0f, -w.Map.height);
            OzUeCap.Bypass = true;
            OzUePlayground.BenchArmy(Team.Blue, perSide, c, Vector3.right, Vector3.forward, -1f, 0, out _);
            OzUePlayground.BenchArmy(Team.Red, perSide, c, Vector3.right, Vector3.forward, 1f, 0, out _);
            OzUeCap.Bypass = false;
        }

        /// <summary>Seconds until Lighter == want (feeding the given frame time), at most limit.</summary>
        static IEnumerator Until(bool want, float frame, float limit, float[] took)
        {
            OzMassLod.TestFrame = frame;
            float t0 = Time.realtimeSinceStartup; took[0] = -1f;
            while (Time.realtimeSinceStartup - t0 < limit) { if (OzMassLod.Lighter == want) { took[0] = Time.realtimeSinceStartup - t0; yield break; } yield return null; }
        }

        IEnumerator Main()
        {
            Report = "running";
            bool on0 = OzMassLod.On, playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed;
            OzUePlace.Stop(); OzUePick.ClearInspect();
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            var took = new float[1];
            float slow = OzMassLod.RefFrame * 2.4f, smooth = OzMassLod.RefFrame * 0.6f;                         // review: relative to the frame-rate limit (60 fps: 40 / 10 ms; 30 fps: 80 / 20 ms)
            lines.Append("INFO  the frame-rate limit's frame ").Append((OzMassLod.RefFrame * 1000f).ToString("0.0")).Append(" ms: slow frames fed ").Append((slow * 1000f).ToString("0.0"))
                 .Append(" ms, smooth ").Append((smooth * 1000f).ToString("0.0")).AppendLine(" ms");

            OzMassLod.On = false; Field(150); yield return Seconds(1f);
            yield return Until(true, slow, 6f, took);
            Check(took[0] < 0f && !OzMassLod.Lighter, "M1 option OFF, " + OzMassLod.LastUnits + " units, slow frames for 6 s: lighter look never in");

            OzMassLod.On = true; Field(30); yield return Seconds(1f);
            yield return Until(true, slow, 6f, took);
            Check(took[0] < 0f && !OzMassLod.Lighter && OzMassLod.LastUnits <= OzMassLod.UnitFloor, "M2 option ON, " + OzMassLod.LastUnits + " units (70 or fewer), slow frames for 6 s: never in");

            OzMassLod.TestFrame = smooth; Field(150); yield return Seconds(2f);                                // review: smooth frames while the field fills, so M3 times the slow stretch from its own start
            yield return Until(true, slow, 8f, took);
            Check(took[0] >= 2f && took[0] <= 4f, "M3 option ON, " + OzMassLod.LastUnits + " units, slow frames: in after " + took[0].ToString("0.0") + " s (want 2 .. 4)");

            yield return Until(false, smooth, 10f, took);
            Check(took[0] >= 5f && took[0] <= 7f, "M4 smooth frames: out after " + took[0].ToString("0.0") + " s (want 5 .. 7)");

            int skipping = 0;
            var g = OzUnitEditor.I.Game;
            foreach (var kv in g.Views) if (kv.Value && OzMassLod.SkipLegIk(kv.Value)) skipping++;
            Check(skipping == 0, "M6 while out: " + skipping + " robot views would rest their foot IK (want 0)");

            yield return Until(true, slow, 8f, took);
            Field(30);
            yield return Until(false, slow, 3f, took);
            Check(took[0] >= 0f && took[0] <= 1f, "M5 in again, then the field cut to " + OzMassLod.LastUnits + " units: out after " + took[0].ToString("0.00") + " s (want 1 or less)");
            OzMassLod.TestFrame = null;

            // M7 the real gain (report only)
            Field(200); OzUeControls.Fit(); yield return Seconds(4f);
            var fpsIn = new float[2]; var fpsOut = new float[2];
            OzMassLod.Force = true; yield return Measure(15f, fpsIn);
            OzMassLod.Force = false; yield return Measure(15f, fpsOut);
            OzMassLod.Force = null;
            lines.Append("INFO  M7 200 a side at x1: lighter look in ").Append(fpsIn[0].ToString("0.0")).Append(" fps (main ").Append(fpsIn[1].ToString("0.0")).Append(" ms), out ")
                 .Append(fpsOut[0].ToString("0.0")).Append(" fps (main ").Append(fpsOut[1].ToString("0.0")).AppendLine(" ms)");

            OzUePlayground.ClearField();
            OzMassLod.On = on0; OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            Report = Tag + " " + (fail == 0 ? "PASS" : "FAIL") + " " + pass + " PASS " + fail + " FAIL";
            string all = Report + "\n" + lines;
            Debug.Log(all);
            if (!string.IsNullOrEmpty(Folder)) { try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), all); } catch (System.Exception e) { Debug.LogWarning(Tag + " " + e.Message); } }
            Destroy(gameObject);
        }

        static IEnumerator Measure(float sec, float[] into)
        {
            var ft = new FrameTiming[1]; int n = 0; float sum = 0f; double main = 0; int mn = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < sec)
            {
                yield return null;
                n++; sum += Time.unscaledDeltaTime;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, ft) > 0 && ft[0].cpuMainThreadFrameTime > 0.0) { main += ft[0].cpuMainThreadFrameTime; mn++; }
            }
            into[0] = sum > 0f ? n / sum : 0f; into[1] = mn > 0 ? (float)(main / mn) : -1f;
        }
    }
}
#endif
