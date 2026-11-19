// OpenZ 1.5, level 18 (editor only, kit level18_start_robots): game-camera frames of each fort's start set, taken the moment the game hands
// back after the RED briefing, after the BLUE briefing (PLAY AS BLUE) and with the briefings OFF (PlayerPrefs oz_zod_briefing 0, restored after).
// Menu OpenZ/V1.5/L18 Briefings/6 Start set frames (MainMenu, Play). Nothing in the game changes; the camera keeps the height the game gives it
// off Unity), a log beside them.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18StartSetFrames
    {
        static readonly string[] Cases = { "red", "blue", "off" };
        static IEnumerator<double> run; static double waitUntil; static string outDir; static System.Text.StringBuilder log;
        static readonly Vector3 BlueFort = new Vector3(124f, 0f, -78f), RedFort = new Vector3(124f, 0f, -166f);

        [MenuItem("OpenZ/V1.5/L18 Briefings/6 Start set frames (MainMenu, Play)", false, 7630)]
        static void Begin()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzL18StartSetFrames] enter Play on the MainMenu scene first"); return; }
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/owner_frames/level18/_start_set_png")).Replace('\\', '/');
            Directory.CreateDirectory(outDir); log = new System.Text.StringBuilder();
            run = Run(); waitUntil = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (run == null) { EditorApplication.update -= Tick; return; }
            if (!Application.isPlaying) { Log("Play left: stopped"); Done(); return; }
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            bool more;
            try { more = run.MoveNext(); } catch (Exception e) { Log("EXCEPTION " + e); more = false; }
            if (!more) { Done(); return; }
            waitUntil = EditorApplication.timeSinceStartup + run.Current;
        }

        static void Done()
        {
            EditorApplication.update -= Tick; run = null;
            try { File.WriteAllText(Path.Combine(outDir, "start_set_frames_log.txt"), log.ToString()); } catch (Exception) { }
            Debug.Log("[OzL18StartSetFrames] DONE\n" + log);
        }

        static void Log(string s) { log.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); Debug.Log("[OzL18StartSetFrames] " + s); }
        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;

        static IEnumerator<double> Run()
        {
            int prefWas = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1); bool boxWas = OzPlayBlue.Box;
            foreach (var c in Cases)
            {
                if (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>())
                {
                    OzScenes.MainMenu(); double tm = EditorApplication.timeSinceStartup;
                    while (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>() && EditorApplication.timeSinceStartup - tm < 40) yield return 0.2;
                    yield return 2.0;
                }
                var m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
                if (!m) { Log("case " + c + ": no main menu, stopped"); break; }
                PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, c == "off" ? 0 : 1);
                OzPlayBlue.Box = c == "blue";
                int card = Array.FindIndex(OzLevelDef.All, d => d.Id == "L18");
                yield return 0.8; m.ProofSelect(); yield return 0.8; m.ProofFocus(card); yield return 0.6; m.ProofDeploy();
                double t0 = EditorApplication.timeSinceStartup;
                while ((!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L18") && EditorApplication.timeSinceStartup - t0 < 120) yield return 0.2;
                if (!ZGame.I || ZGame.I.World == null) { Log("case " + c + ": level 18 did not load"); continue; }
                Log($"case {c}: level 18 loaded, human {ZGame.I.player}, PLAY AS BLUE {OzPlayBlue.Active}");
                bool blue = c == "blue";
                if (c != "off")
                {
                    double tb = EditorApplication.timeSinceStartup;
                    while (!(blue ? OzBlueCameo.BriefingActive : ZodBriefing.BriefingActive) && EditorApplication.timeSinceStartup - tb < 25) yield return 0.1;
                    Log($"case {c}: briefing started {(blue ? OzBlueCameo.BriefingActive : ZodBriefing.BriefingActive)} after {EditorApplication.timeSinceStartup - tb:F1} s");
                }
                double tw = EditorApplication.timeSinceStartup;
                while ((ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive || Flying) && EditorApplication.timeSinceStartup - tw < 120) yield return 0.1;
                Log($"case {c}: hand-back after {EditorApplication.timeSinceStartup - tw:F1} s (briefing {(ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive ? "STILL ACTIVE" : "over")}, flyover {(Flying ? "STILL FLYING" : "landed")})");
                yield return 0.5;
                var rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
                if (!rc) { Log("case " + c + ": no RtsCamera"); continue; }
                Log($"case {c}: game camera at hand-back: focus {rc.focus}, height {rc.height:F1} m, pitch {rc.pitch:F0}, sim time {ZGame.I.World.Time:F1}");
                var own = ZGame.I.player == Team.Blue ? BlueFort : RedFort; var other = own == BlueFort ? RedFort : BlueFort;
                foreach (var f in new[] { own, other })
                {
                    string which = f == BlueFort ? "blue_fort" : "red_fort";
                    rc.focus = f; yield return 1.2;
                    string file = Path.Combine(outDir, $"start_set_{(c == "off" ? "briefings_off" : "after_" + c + "_briefing")}_{which}.png").Replace('\\', '/');
                    ScreenCapture.CaptureScreenshot(file);
                    Log($"case {c}: {which}: focus {rc.focus}, height {rc.height:F1} m, sim time {ZGame.I.World.Time:F1} -> {file}");
                    yield return 0.8;
                }
                OzScenes.MainMenu(); yield return 3.0;
            }
            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, prefWas); OzPlayBlue.Box = boxWas; PlayerPrefs.Save();
            Log($"restored: oz_zod_briefing {prefWas}, PLAY AS BLUE box {boxWas}");
        }
    }
}
