#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 9 counted Play check in the Unit editor (menu OpenZ/V1.3/Wrecks: Item 9 Unit Editor Clear Check; Play, the unit editor open):
    /// <see cref="Runs"/> runs of: CLEAR FIELD, six SAMPLE FIGHTs at once (OzUeCap.SampleFightsMax), the game at 4x for <see cref="FightSeconds"/>
    /// real seconds (the fights burn down: wrecks, flips, torn parts, wheels), a frame, the sunk count of the drawn vehicles and wrecks
    /// (as View/OzSunkWatch.cs counts them), then CLEAR FIELD and what is still drawn of units and wrecks (OzUePlayground.CountLeftAfterClear).
    /// Pass: every run 0 left after CLEAR FIELD, 0 failed clear passes, 0 sunk at the frame. The speed and the field are put back (cleared).
    /// Output: <see cref="Folder"/> (report.txt, a frame per run before and after the clear). Test only.
    /// </summary>
    public sealed class OzUeClearCheck : MonoBehaviour
    {
        public static int Runs = 3; public static float FightSeconds = 45f;
        public static string Report = "not run";
        public static string Folder => OzRelease.Milestones("v13_item09_ue_clear");
        static OzUeClearCheck run;
        public static bool Running => run;
        readonly StringBuilder sb = new StringBuilder();
        void Line(string t) { sb.AppendLine(t); Debug.Log("[OzUeClearCheck] " + t); }

        public static void Begin()
        {
            if (run) { Debug.LogWarning("[OzUeClearCheck] a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning("[OzUeClearCheck] enter Play and open the unit editor first"); return; }
            run = new GameObject("OzUe_ClearCheck").AddComponent<OzUeClearCheck>();
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);
            var ed = OzUnitEditor.I; var g = ed.Game; float speed0 = g.gameSpeed; bool paused0 = g.paused;
            int pass = 0, fail = 0, err0 = OzUePlayground.ClearErrors;
            for (int r = 1; r <= Runs; r++)
            {
                OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
                yield return new WaitForSecondsRealtime(0.5f);
                int made = 0;
                for (int i = 0; i < OzUeCap.SampleFightsMax; i++) { made += OzUePlayground.SampleFight() > 0 ? 1 : 0; yield return null; }
                g.paused = false; g.gameSpeed = 4f;
                float t = 0f;
                while (t < FightSeconds) { t += Time.unscaledDeltaTime; yield return null; }
                int sunk = SunkNow(g);
                Shot($"run{r}_a_before_clear");
                int flips = 0; foreach (var v in FindObjectsByType<UnitView>(FindObjectsSortMode.None)) if (v && v.gameObject.activeInHierarchy && v.Dying && v.FlippedWreck) flips++;
                OzUePlayground.ClearField();
                yield return null; yield return null;
                int left = OzUePlayground.CountLeftAfterClear(ed.World);
                Shot($"run{r}_b_after_clear");
                bool ok = left == 0 && OzUePlayground.ClearLeft == 0 && sunk == 0;
                if (ok) pass++; else fail++;
                Line($"{(ok ? "PASS" : "FAIL")} run {r}: {made} sample fights made, {FightSeconds:0} s at 4x, flipped wrecks drawn {flips}, sunk before the clear {sunk}; after CLEAR FIELD still drawn {left} (the clear's own count {OzUePlayground.ClearLeft}), stray views it hid so far {OzUePlayground.ClearStrayViews}");
            }
            g.gameSpeed = speed0; g.paused = paused0; OzUePlayground.ClearField();
            int errs = OzUePlayground.ClearErrors - err0;
            if (errs > 0) { fail++; Line($"FAIL CLEAR FIELD passes that failed: {errs}"); } else { pass++; Line("PASS CLEAR FIELD passes that failed: 0"); }
            Line($"lifted: hulls {WreckGroundRest.HullsLifted} (deepest {WreckGroundRest.DeepestHullM:0.00} m), pieces {WreckGroundRest.PiecesLifted}; playground ground deepened {WreckGroundRest.GroundDeepened}");
            Report = $"{pass} PASS, {fail} FAIL"; Line(Report);
            File.WriteAllText(Path.Combine(Folder, "report.txt"), sb.ToString());
            run = null; Destroy(gameObject);
        }

        /// <summary>The drawn vehicles and cannons (alive or wrecks, not sinking away on purpose) in the ground, by the sunk watch's own
        /// bounds and rule (View/OzSunkWatch.cs); every drawn view, also one the game's list no longer holds.</summary>
        static int SunkNow(ZGame g)
        {
            int n = 0;
            foreach (var v in FindObjectsByType<UnitView>(FindObjectsSortMode.None))
            {
                if (!v || !v.gameObject.activeInHierarchy || v.Obj == null || v.OnTower || v.WreckSinking) continue;
                if (v.Obj.Kind != ZKind.Vehicle && v.Obj.Kind != ZKind.Cannon) continue;
                if (OzWaterLanding.InWater(v.transform) || !OzSunkWatch.Bounds(v.transform, out var b)) continue;
                if (OzSunkWatch.Sunk(b, g.GroundY(b.center.x, b.center.z), out _)) n++;
            }
            return n;
        }

        void Shot(string name) { var p = Path.Combine(Folder, name + ".png"); ScreenCapture.CaptureScreenshot(p); Line("frame " + p); }
        void OnDestroy() { if (run == this) run = null; }
    }
}
#endif
