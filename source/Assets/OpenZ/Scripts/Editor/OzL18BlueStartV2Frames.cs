// OpenZ 1.5, level 18 (editor only, kit level18_rules/blue_start_v2, owner addition 43): game-camera frames of the BLUE start set v2 with the
// player's options off and with the SHUTTLE box + HEAD START on, and of the red fort to show red unchanged. Briefings off for every case
// (PlayerPrefs oz_zod_briefing 0); difficulty, head start, shuttle box, PLAY AS BLUE and the briefing pref are put back after.
// Menu OpenZ/V1.5/L18 Briefings/7 Blue start set v2 frames (MainMenu, Play). Nothing in the game changes; the camera keeps the height the game
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18BlueStartV2Frames
    {
        // name, PLAY AS BLUE, oz_difficulty (1 EASY, 2 MEDIUM, 0 HARD), head start, shuttle box
        static readonly (string name, bool blue, int diff, bool head, bool ship)[] Cases =
        {
            ("options_off", false, 0, false, false),          // HARD as red: the computer's blue fort, no head start, no ship (both forts)
            ("blue_options_off", true, 2, false, false),      // PLAY AS BLUE, MEDIUM, head start OFF, shuttle box OFF
            ("blue_shuttle_headstart", true, 2, true, true),  // PLAY AS BLUE, MEDIUM, head start ON, shuttle box ON
            ("red_shuttle_headstart", false, 2, true, true),  // red player, MEDIUM, head start ON, shuttle box ON (red unchanged)
        };
        static IEnumerator<double> run; static double waitUntil; static string outDir; static System.Text.StringBuilder log;
        static readonly Vector3 BlueFort = new Vector3(124f, 0f, -80f), RedFort = new Vector3(124f, 0f, -166f);

        [MenuItem("OpenZ/V1.5/L18 Briefings/7 Blue start set v2 frames (MainMenu, Play)", false, 7631)]
        static void Begin()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzL18BlueStartV2Frames] enter Play on the MainMenu scene first"); return; }
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/owner_frames/level18/_start_set_v2_png")).Replace('\\', '/');
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
            try { File.WriteAllText(Path.Combine(outDir, "start_set_v2_frames_log.txt"), log.ToString()); } catch (Exception) { }
            Debug.Log("[OzL18BlueStartV2Frames] DONE\n" + log);
        }

        static void Log(string s) { log.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); Debug.Log("[OzL18BlueStartV2Frames] " + s); }

        static IEnumerator<double> Run()
        {
            int prefWas = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1); bool boxWas = OzPlayBlue.Box;
            int diffWas = PlayerPrefs.GetInt(OpenZDifficulty.Key, 2), mayWas = PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0);
            int headWas = PlayerPrefs.GetInt(OpenZDifficulty.HeadStartPref, 0), shipWas = PlayerPrefs.GetInt(OzShuttleStart.Pref, 1);
            foreach (var c in Cases)
            {
                if (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>())
                {
                    OzScenes.MainMenu(); double tm = EditorApplication.timeSinceStartup;
                    while (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>() && EditorApplication.timeSinceStartup - tm < 40) yield return 0.2;
                    yield return 2.0;
                }
                var m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
                if (!m) { Log("case " + c.name + ": no main menu, stopped"); break; }
                PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 0);
                PlayerPrefs.SetInt(OpenZDifficulty.Key, c.diff); PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, 0);
                PlayerPrefs.SetInt(OpenZDifficulty.HeadStartPref, c.head ? 1 : 0); PlayerPrefs.SetInt(OzShuttleStart.Pref, c.ship ? 1 : 0); PlayerPrefs.Save();
                OzPlayBlue.Box = c.blue;
                int card = Array.FindIndex(OzLevelDef.All, d => d.Id == "L18");
                yield return 0.8; m.ProofSelect(); yield return 0.8; m.ProofFocus(card); yield return 0.6; m.ProofDeploy();
                double t0 = EditorApplication.timeSinceStartup;
                while ((!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L18") && EditorApplication.timeSinceStartup - t0 < 120) yield return 0.2;
                if (!ZGame.I || ZGame.I.World == null) { Log("case " + c.name + ": level 18 did not load"); continue; }
                yield return 1.0;
                var w = ZGame.I.World; int ships = 0, vehicles = 0;
                foreach (var o in w.Objects) { if (o.Destroyed) continue; if (o.IsShuttle) ships++; else if (o.Kind == ZKind.Vehicle) vehicles++; }
                Log($"case {c.name}: level 18 loaded, human {ZGame.I.player}, PLAY AS BLUE {OzPlayBlue.Active}, difficulty pref {c.diff}, head start {c.head}, shuttle box {c.ship}: vehicles {vehicles}, ships {ships}, head start made {ZGame.HeadStartMade}");
                var rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
                if (!rc) { Log("case " + c.name + ": no RtsCamera"); continue; }
                var forts = c.name == "options_off" ? new[] { BlueFort, RedFort } : c.blue ? new[] { BlueFort } : new[] { RedFort };
                foreach (var f in forts)
                {
                    string which = f == BlueFort ? "blue_fort" : "red_fort";
                    rc.focus = f; yield return 1.2;
                    string file = Path.Combine(outDir, $"start_set_v2_{c.name}_{which}.png").Replace('\\', '/');
                    ScreenCapture.CaptureScreenshot(file);
                    Log($"case {c.name}: {which}: focus {rc.focus}, height {rc.height:F1} m, pitch {rc.pitch:F0}, sim time {w.Time:F1} -> {file}");
                    yield return 0.8;
                }
                OzScenes.MainMenu(); yield return 3.0;
            }
            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, prefWas); OzPlayBlue.Box = boxWas;
            PlayerPrefs.SetInt(OpenZDifficulty.Key, diffWas); PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, mayWas);
            PlayerPrefs.SetInt(OpenZDifficulty.HeadStartPref, headWas); PlayerPrefs.SetInt(OzShuttleStart.Pref, shipWas); PlayerPrefs.Save();
            Log($"restored: oz_zod_briefing {prefWas}, PLAY AS BLUE box {boxWas}, difficulty {diffWas}, mayhem {mayWas}, head start {headWas}, shuttle box {shipWas}");
        }
    }
}
