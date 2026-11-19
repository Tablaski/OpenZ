// OpenZ 1.5, level 18 (editor only, kit level18_rules/blue_start_v3, owner additions 48 point 4 and 49): game-camera frames of the BLUE start set
// v3 for the four option combinations (PLAY AS BLUE, MEDIUM: nothing / the SHUTTLE box / the HEAD START / both), the red fort with both options
// (red unchanged but for the head start's crane), one WIDE frame of the map showing where the EMPTY Mammoth stands and one frame at the Mammoth.
// Briefings off for every case (PlayerPrefs oz_zod_briefing 0); difficulty, head start, shuttle box, PLAY AS BLUE and the briefing pref are put
// back after. Nothing in the game changes: the camera keeps its pitch, only its focus and height move. For each picture a labels_<name>.json
// beside the PNG lists the screen place of every start unit worth naming (Camera.main.WorldToScreenPoint of its view): the kit's
// tools/label_frames.py draws the labels on the PICTURE afterwards (never in the game).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18BlueStartV3Frames
    {
        // name, PLAY AS BLUE, oz_difficulty (2 MEDIUM: the head start and the shuttle box both count), head start, shuttle box
        static readonly (string name, bool blue, int diff, bool head, bool ship)[] Cases =
        {
            ("blue_options_off", true, 2, false, false),      // PLAY AS BLUE, MEDIUM, head start OFF, shuttle box OFF (+ the wide map frame and the Mammoth frame)
            ("blue_shuttle", true, 2, false, true),           // PLAY AS BLUE, MEDIUM, head start OFF, shuttle box ON
            ("blue_headstart", true, 2, true, false),         // PLAY AS BLUE, MEDIUM, head start ON, shuttle box OFF
            ("blue_shuttle_headstart", true, 2, true, true),  // PLAY AS BLUE, MEDIUM, head start ON, shuttle box ON
            ("red_shuttle_headstart", false, 2, true, true),  // red player, MEDIUM, both ON: red unchanged, the head start without a crane
        };
        static IEnumerator<double> run; static double waitUntil; static string outDir; static StringBuilder log;
        /// <summary>The blue fort frame's focus: a little south of the fort's middle (124, -80) so the front squads by the river stay above the HUD.</summary>
        static readonly Vector3 BlueFocus = new Vector3(124f, 0f, -84f), RedFocus = new Vector3(124f, 0f, -164f);
        const float FortHeight = 75f, WideHeight = 190f, CloseHeight = 70f;

        [MenuItem("OpenZ/V1.5/L18 Briefings/8 Blue start set v3 frames (MainMenu, Play)", false, 7632)]
        static void Begin()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzL18BlueStartV3Frames] enter Play on the MainMenu scene first"); return; }
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/owner_frames/level18/_start_set_v3_png")).Replace('\\', '/');
            Directory.CreateDirectory(outDir); log = new StringBuilder();
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
            try { File.WriteAllText(Path.Combine(outDir, "start_set_v3_frames_log.txt"), log.ToString()); } catch (Exception) { }
            Debug.Log("[OzL18BlueStartV3Frames] DONE\n" + log);
        }

        static void Log(string s) { log.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); Debug.Log("[OzL18BlueStartV3Frames] " + s); }

        static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>A unit's place for its label: its view's place (a little over its middle), else the rules' middle on the ground.</summary>
        static Vector3 Where(ZObject o, float up)
        {
            if (ZGame.I && ZGame.I.Views.TryGetValue(o.Id, out var v) && v) return v.transform.position + Vector3.up * up;
            return new Vector3(o.CenterX / 8f, up, -o.CenterY / 8f);
        }

        /// <summary>The labels of one picture: every start unit worth naming that the camera sees (screen px, origin top left).</summary>
        static string Labels(string name, string file, ZWorld w, bool wide)
        {
            var cam = Camera.main; var sb = new StringBuilder(); int n = 0;
            sb.Append("{\"name\": \"").Append(name).Append("\", \"file\": \"").Append(Esc(Path.GetFileName(file))).Append("\", \"w\": ").Append(Screen.width).Append(", \"h\": ").Append(Screen.height).Append(", \"labels\": [");
            void Add(string text, Vector3 at)
            {
                if (!cam) return;
                var p = cam.WorldToScreenPoint(at);
                if (p.z <= 0 || p.x < 4 || p.y < 4 || p.x > Screen.width - 4 || p.y > Screen.height - 4) return;
                sb.Append(n++ == 0 ? "" : ", ").Append("{\"text\": \"").Append(Esc(text)).Append("\", \"x\": ").Append(F(p.x)).Append(", \"y\": ").Append(F(Screen.height - p.y)).Append("}");
            }
            int maxId = 0; foreach (var o in w.Objects) if (o.Source != null && o.Id > maxId) maxId = o.Id;   // objects made after the map's (the head start, the ship, the drivers)
            foreach (var o in w.Objects)
            {
                if (o.Destroyed) continue;
                if (o.IsShuttle) { Add(o.Owner == Team.Neutral ? "EMPTY SHUTTLE" : "SHUTTLE (option)", Where(o, 3f)); continue; }
                if (o.Kind == ZKind.Vehicle)
                {
                    string t = o.Type == ZBigTanks.Mammoth ? "MAMMOTH" : o.Type == ZBigTanks.Bizon ? "BIZON" : o.Type == "missile_launcher" ? "MLRS" : o.Type == "medium" ? "MECH" : o.Type.ToUpperInvariant() + (o.Type == "heavy" || o.Type == "light" ? " TANK" : "");
                    bool head = o.Source == null && !w.L18BigTanks.Contains(o);
                    if (o.Owner == Team.Neutral) { if (wide || ZBigTanks.Is(o.Type) || o.Type == "crane") Add("EMPTY " + t, Where(o, 2.5f)); continue; }
                    if (wide) continue;
                    Add(head ? "HEAD START: " + t : t + (o.Drivers.Count > 0 ? " (crewed)" : ""), Where(o, 2.5f));
                    continue;
                }
                if (wide) { if (o.IsFort && o.Type == "fort_front") Add("BLUE FORT", new Vector3(o.CenterX / 8f, 6f, -o.CenterY / 8f)); else if (o.IsFort) Add("RED FORT", new Vector3(o.CenterX / 8f, 6f, -o.CenterY / 8f)); continue; }
                if (o.Kind == ZKind.Robot && o.Leader == null && !o.IsFortDefender)
                    Add(o.Type.ToUpperInvariant() + (o.Minions.Count > 0 ? " SQUAD" : w.L18BigTankDrivers.Contains(o) ? " (the Mammoth's driver)" : ""), Where(o, 1.5f));
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static IEnumerator<double> Shot(RtsCamera rc, ZWorld w, string name, Vector3 focus, float height, bool wide)
        {
            rc.SetHeightImmediate(height); rc.focus = focus; yield return 1.4;
            string file = Path.Combine(outDir, $"start_set_v3_{name}.png").Replace('\\', '/');
            string labels = Labels(name, file, w, wide);
            ScreenCapture.CaptureScreenshot(file);
            File.WriteAllText(Path.Combine(outDir, $"labels_{name}.json"), labels);
            Log($"{name}: focus {rc.focus}, height {rc.height:F1} m, pitch {rc.pitch:F0}, sim time {w.Time:F1} -> {file}");
            yield return 0.8;
        }

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
                var w = ZGame.I.World; int ships = 0, vehicles = 0, cranesOwned = 0;
                foreach (var o in w.Objects) { if (o.Destroyed) continue; if (o.IsShuttle) ships++; else if (o.Kind == ZKind.Vehicle) { vehicles++; if (o.Type == "crane" && o.Owner != Team.Neutral) cranesOwned++; } }
                Log($"case {c.name}: level 18 loaded, human {ZGame.I.player}, PLAY AS BLUE {OzPlayBlue.Active}, difficulty pref {c.diff}, head start {c.head}, shuttle box {c.ship}: vehicles {vehicles}, ships {ships}, head start made {ZGame.HeadStartMade}, cranes owned by a side {cranesOwned} ");
                var rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
                if (!rc) { Log("case " + c.name + ": no RtsCamera"); continue; }
                var shot = Shot(rc, w, c.name + (c.blue ? "_blue_fort" : "_red_fort"), c.blue ? BlueFocus : RedFocus, FortHeight, false);
                while (shot.MoveNext()) yield return shot.Current;
                if (c.name == "blue_options_off")
                {
                    ZObject mam = null; foreach (var o in w.L18BigTanks) if (o.Type == ZBigTanks.Mammoth) mam = o;
                    var mf = mam != null ? new Vector3(mam.CenterX / 8f, 0f, -mam.CenterY / 8f) : new Vector3(18f, 0f, -42f);
                    shot = Shot(rc, w, "wide_map_empty_mammoth", (mf + new Vector3(124f, 0f, -80f)) * 0.5f + new Vector3(0f, 0f, -6f), WideHeight, true);
                    while (shot.MoveNext()) yield return shot.Current;
                    shot = Shot(rc, w, "empty_mammoth_close", mf + new Vector3(4f, 0f, -4f), CloseHeight, true);
                    while (shot.MoveNext()) yield return shot.Current;
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
