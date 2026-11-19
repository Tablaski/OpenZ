// OpenZ 1.5, level 18 (editor only): the extra Play proofs of level 18, counted from the running game; nothing in the game changes.
// Menu "OpenZ/V1.5/L18 Proofs/3 Extras (MainMenu, Play)": from the main menu, briefings off, red, every setting put back at the end:
//   A  MEDIUM + HEAD START on: the head start count, the start units with their tiles, cranes owned by a side; a night frame (light 0.62);
//      a red robot squad ordered over the river (seconds on water / deep water tiles, crossed or not, frames); wrecks on water tiles;
//      the standard WIN ending (OpenZVictory.DebugFortFalls(true)): frames over 14 s, the sounds logged after the fall.
//   B  MEDIUM: the standard LOSS ending (DebugFortFalls(false)): frames, sounds.
//   C  MAYHEM: the fog of war state (OzFogOfWar.GameplayView / GateReason) after the hand-back, frames.
//   D  the level-start flyover with briefings off (MEDIUM): Flying seen or not, its length, frames.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18ExtrasProof
    {
        static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/unity_pass_20261113_C")).Replace('\\', '/');
        static int T(int px) => px >> 4;
        static string Tile(ZObject o) => $"({T(o.CenterX)},{T(o.CenterY)})";
        static IEnumerator<double> run; static double waitUntil; static StringBuilder log; static readonly List<string> msgs = new List<string>();
        static int nErr, nWarn, nExc;
        static readonly Vector3 RedFort = new Vector3(124f, 0f, -166f);
        static double Now => EditorApplication.timeSinceStartup;
        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;
        static bool Briefing => ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive;
        static void Log(string s) { log.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); Debug.Log("[OzL18ExtrasProof] " + s); }
        static void Shot(string name) { ScreenCapture.CaptureScreenshot(Out + "/frames_extras/" + name + ".png"); }

        [MenuItem("OpenZ/V1.5/L18 Proofs/3 Extras (MainMenu, Play)", false, 7702)]
        static void Begin()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzL18ExtrasProof] enter Play on the MainMenu scene first"); return; }
            if (run != null) { Debug.LogWarning("[OzL18ExtrasProof] already running"); return; }
            Directory.CreateDirectory(Out + "/frames_extras"); log = new StringBuilder(); msgs.Clear(); nErr = nWarn = nExc = 0;
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
            run = Run(); waitUntil = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void OnLog(string text, string stack, LogType t)
        {
            if (text.StartsWith("[OzL18ExtrasProof]")) return;
            string first = text.Split('\n')[0]; if (first.Length > 300) first = first.Substring(0, 300);
            if (t == LogType.Error || t == LogType.Assert) { nErr++; msgs.Add("ERROR " + first); }
            else if (t == LogType.Exception) { nExc++; msgs.Add("EXCEPTION " + first + " | " + (stack ?? "").Split('\n')[0]); }
            else if (t == LogType.Warning) { nWarn++; msgs.Add("WARNING " + first); }
        }

        static void Tick()
        {
            if (run == null) { EditorApplication.update -= Tick; return; }
            if (!Application.isPlaying) { Log("Play left: stopped"); Done(); return; }
            if (Now < waitUntil) return;
            bool more;
            try { more = run.MoveNext(); } catch (Exception e) { Log("EXCEPTION in the proof " + e); more = false; }
            if (!more) { Done(); return; }
            waitUntil = Now + run.Current;
        }

        static void Done()
        {
            EditorApplication.update -= Tick; run = null; Application.logMessageReceived -= OnLog;
            log.AppendLine($"== log of the run: {nErr} error(s), {nExc} exception(s), {nWarn} warning(s); lines:");
            foreach (var m in msgs) log.AppendLine("  " + m);
            try { File.WriteAllText(Out + "/l18_extras.txt", log.ToString()); } catch (Exception) { }
            Debug.Log("[OzL18ExtrasProof] DONE " + Out + "/l18_extras.txt");
        }

        static IEnumerator<double> ToMenu()
        {
            if (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>())
            {
                OzScenes.MainMenu(); double tm = Now;
                while (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>() && Now - tm < 60) yield return 0.2;
                yield return 2.5;
            }
        }

        static IEnumerator<double> Deploy(string tag, GameDifficulty d, bool headStart)
        {
            var e = ToMenu(); while (e.MoveNext()) yield return e.Current;
            var m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>(); if (!m) { Log(tag + ": no main menu"); yield break; }
            int card = Array.FindIndex(OzLevelDef.All, x => x.Id == "L18");
            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 0); OzPlayBlue.Box = false; OzShuttleStart.On = false;
            OpenZDifficulty.Choice = d; OpenZDifficulty.HeadStartToggle = headStart;
            yield return 0.8; m.ProofSelect(); yield return 1.0; m.ProofFocus(card); yield return 0.8; m.ProofDeploy();
            double t0 = Now; while ((!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L18") && Now - t0 < 150) yield return 0.2;
            double tw = Now; bool sawFly = false; double flyFrom = -1, flyTo = -1; int fs = 0; double nextF = Now + 0.5;
            while (Now - tw < 60)
            {
                if (Flying) { if (!sawFly) { sawFly = true; flyFrom = Now - tw; } flyTo = Now - tw; if (tag == "D" && Now >= nextF && fs < 12) { Shot($"D_flyover_{fs:00}_{Now - tw:F1}s"); fs++; nextF = Now + 1.5; } }
                else if (Now - tw > 6 && !Briefing) break;
                yield return 0.1;
            }
            Log($"{tag}: level 18 loaded {(ZGame.I && ZGame.I.level == "L18")} after {tw - t0:F1} s, difficulty {OpenZDifficulty.Choice}, head start toggle {OpenZDifficulty.HeadStartToggle}; level-start flyover {(sawFly ? $"SEEN from {flyFrom:F1} s to {flyTo:F1} s ({fs} frames)" : "NOT seen in the first 6 s")}; briefing active {Briefing}");
        }

        static IEnumerator<double> Ending(string tag, bool win)
        {
            var w = ZGame.I.World; float s0 = Time.unscaledTime; double t0 = Now;
            int n = OpenZVictory.DebugFortFalls(win);
            Log($"{tag}: DebugFortFalls({win}) -> {n} fort part(s) set to fall");
            double[] at = { 0.5, 1.5, 3, 5, 8, 11, 14 }; int k = 0;
            while (Now - t0 < 16)
            {
                if (k < at.Length && Now - t0 >= at[k]) { Shot($"{tag}_{(win ? "win" : "loss")}_{at[k]:F1}s"); k++; }
                yield return 0.1;
            }
            Log($"{tag}: after 16 s: GameOver {w.GameOver}, winner {w.Winner}, end sequence running {OpenZVictory.EndSequenceRunning}, hold end screen {OpenZVictory.HoldEndScreen}");
            var snd = OzSoundLog.Since(s0);
            Log($"{tag}: {snd.Count} sounds logged since the fall; keys: {string.Join(", ", snd.GroupBy(x => x.key).Select(g => $"{g.Key} x{g.Count()}").Take(60))}");
            File.WriteAllText(Out + $"/sound_log_{tag}_{(win ? "win" : "loss")}.txt", OzSoundLog.Format(snd));
        }

        static IEnumerator<double> Run()
        {
            OzV15UeBlastsPalette.GameViewSize(1920, 1080);
            int prefBrief = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1); bool blueWas = OzPlayBlue.Box; bool shipWas = OzShuttleStart.On;
            var choiceWas = OpenZDifficulty.Choice; bool hsWas = OpenZDifficulty.HeadStartToggle;
            Log($"settings before: briefing {prefBrief}, PLAY AS BLUE {blueWas}, SHUTTLE box {shipWas}, difficulty {choiceWas}, head start {hsWas}");

            // ---------------- A: head start, night, wading, wrecks, win
            var e = Deploy("A", GameDifficulty.Medium, true); while (e.MoveNext()) yield return e.Current;
            if (ZGame.I && ZGame.I.level == "L18")
            {
                var w = ZGame.I.World; var me = ZGame.I.player; var rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
                Log($"A: HEAD START made {ZGame.HeadStartMade} (L18 list: {string.Join(", ", ZGame.HeadStartL18Types)}), refused for MAYHEM {ZGame.HeadStartRefusedMayhem}");
                foreach (var t in new[] { Team.Red, Team.Blue, Team.Neutral })
                {
                    var u = w.Objects.Where(o => o.Owner == t && o.IsUnit && !o.Destroyed && !o.IsMinion).GroupBy(o => o.Type).OrderBy(g => g.Key);
                    Log($"A: start units {t}: " + string.Join(", ", u.Select(g => $"{g.Key} x{g.Count()} [{string.Join(" ", g.Select(Tile))}]")));
                }
                Log($"A: cranes: " + string.Join(", ", w.Objects.Where(o => o.Type == "crane" && !o.Destroyed).Select(o => $"{o.Owner} {Tile(o)}")));
                Log($"A: big tanks: " + string.Join("; ", w.L18BigTanks.Select(o => $"{o.Type} #{o.Id} {o.Owner} {Tile(o)} drivers {o.Drivers.Count}")));
                if (rc) { rc.focus = RedFort; rc.height = 70f; }
                yield return 2.0; Shot("A_headstart_red_fort_day");
                var dn = UnityEngine.Object.FindFirstObjectByType<DayNight>();
                if (dn)
                {
                    float was = dn.timeOfDay; dn.timeOfDay = 0.62f; yield return 3.0; Shot("A_night_red_fort_0.62");
                    Log($"A: NIGHT: DayNight.timeOfDay {was:F2} -> 0.62, frame A_night_red_fort_0.62; back to {was:F2}");
                    dn.timeOfDay = was; yield return 1.0;
                }
                else Log("A: NIGHT: no DayNight in the scene");
                // wading: the red robot leader nearest the river, ordered due north over the water
                var rob = w.Objects.Where(o => o.Owner == me && o.IsRobot && !o.IsMinion && !o.Destroyed && !o.IsFortDefender).OrderBy(o => o.CenterY).FirstOrDefault();
                if (rob != null)
                {
                    int tx = T(rob.CenterX), ty = T(rob.CenterY), wetY = -1, dryY = -1;
                    for (int y = ty; y > 0; y--)
                    {
                        var c = w.Map.ClassAt(tx, y);
                        bool wet = c == ZClass.Water || c == ZClass.DeepWater;
                        if (wet && wetY < 0) wetY = y;
                        if (!wet && wetY >= 0 && c != ZClass.Impassable) { dryY = y; break; }
                    }
                    int goY = dryY > 2 ? dryY - 2 : ty - 20;
                    bool ok = w.SendWaypoints(me, rob, new[] { new ZWaypoint { mode = WpMode.Move, x = tx * 16 + 8, y = goY * 16 + 8, playerGiven = true } });
                    Log($"A: WADE: {rob.Type} #{rob.Id} squad leader at ({tx},{ty}) ordered north to ({tx},{goY}) (order {(ok ? "taken" : "REFUSED")}); first water tile north of it y {wetY}, dry shore y {dryY}");
                    double t0 = Now; double wetS = 0, deepS = 0, firstWet = -1; int wf = 0; int squad = w.Objects.Count(o => o.Leader == rob && !o.Destroyed);
                    while (Now - t0 < 120 && !rob.Destroyed)
                    {
                        var c = w.Map.ClassAt(T(rob.CenterX), T(rob.CenterY));
                        if (c == ZClass.Water) wetS += 0.2; if (c == ZClass.DeepWater) deepS += 0.2;
                        if ((c == ZClass.Water || c == ZClass.DeepWater) && firstWet < 0) firstWet = Now - t0;
                        if (rc) { rc.focus = new Vector3(rob.CenterX / 16f * 2f, 0f, -rob.CenterY / 16f * 2f); rc.height = 70f; }
                        if (firstWet >= 0 && wf < 3 && Now - t0 >= firstWet + 1.5 + 4 * wf) { Shot($"A_wading_{wf}_{Now - t0:F0}s"); wf++; }
                        if (dryY > 0 && T(rob.CenterY) <= goY + 1) break;
                        yield return 0.2;
                    }
                    Log($"A: WADE result after {Now - t0:F0} s: now at {Tile(rob)} (destroyed {rob.Destroyed}), on water tiles {wetS:F1} s, on deep water {deepS:F1} s, first wet at {firstWet:F1} s, crossed the water {(wetY > 0 && T(rob.CenterY) < wetY - 1 && dryY > 0 && T(rob.CenterY) <= dryY ? "YES" : "NO")}, squad members {squad} -> alive {w.Objects.Count(o => o.Leader == rob && !o.Destroyed)}, {wf} frames");
                }
                else Log("A: WADE: no red robot leader found");
                // wrecks on water tiles
                var wet2 = w.Objects.Where(o => !o.IsUnit && o.Kind == ZKind.MapItem && (w.Map.ClassAt(T(o.CenterX), T(o.CenterY)) == ZClass.Water || w.Map.ClassAt(T(o.CenterX), T(o.CenterY)) == ZClass.DeepWater)).GroupBy(o => o.Type).Select(g => $"{g.Key} x{g.Count()}");
                Log($"A: MAP ITEMS on water tiles: {string.Join(", ", wet2)}");
                var anyWreck = w.Objects.FirstOrDefault(o => o.Kind == ZKind.MapItem && o.Type.Contains("wreck") && w.Map.ClassAt(T(o.CenterX), T(o.CenterY)) != ZClass.Ground);
                if (anyWreck != null && rc) { rc.focus = new Vector3(anyWreck.CenterX / 16f * 2f, 0f, -anyWreck.CenterY / 16f * 2f); rc.height = 70f; yield return 2.0; Shot($"A_wreck_in_water_{anyWreck.Type}_{T(anyWreck.CenterX)}_{T(anyWreck.CenterY)}"); Log($"A: frame of {anyWreck.Type} #{anyWreck.Id} at {Tile(anyWreck)}"); }
                e = Ending("A", true); while (e.MoveNext()) yield return e.Current;
            }
            OzScenes.MainMenu(); yield return 4.0;

            // ---------------- B: loss
            e = Deploy("B", GameDifficulty.Medium, false); while (e.MoveNext()) yield return e.Current;
            if (ZGame.I && ZGame.I.level == "L18") { e = Ending("B", false); while (e.MoveNext()) yield return e.Current; }
            OzScenes.MainMenu(); yield return 4.0;

            // ---------------- C: MAYHEM fog
            e = Deploy("C", GameDifficulty.Mayhem, false); while (e.MoveNext()) yield return e.Current;
            if (ZGame.I && ZGame.I.level == "L18")
            {
                yield return 5.0;
                var rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
                Log($"C: MAYHEM chosen {OpenZDifficulty.MayhemChosen}; fog of war: gameplay view {OzFogOfWar.GameplayView}, gate reason '{OzFogOfWar.GateReason}', head start made {ZGame.HeadStartMade} (refused for MAYHEM {ZGame.HeadStartRefusedMayhem})");
                if (rc) { rc.focus = RedFort; rc.height = 70f; } yield return 2.0; Shot("C_mayhem_red_fort");
                if (rc) { rc.focus = new Vector3(124f, 0f, -120f); } yield return 2.0; Shot("C_mayhem_centre_bridges");
                if (rc) { rc.focus = new Vector3(124f, 0f, -78f); } yield return 2.0; Shot("C_mayhem_blue_fort");
            }
            OzScenes.MainMenu(); yield return 4.0;

            // ---------------- D: level-start flyover, briefings off
            e = Deploy("D", GameDifficulty.Medium, false); while (e.MoveNext()) yield return e.Current;
            OzScenes.MainMenu(); yield return 4.0;

            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, prefBrief); OzPlayBlue.Box = blueWas; OzShuttleStart.On = shipWas;
            OpenZDifficulty.Choice = choiceWas; OpenZDifficulty.HeadStartToggle = hsWas; PlayerPrefs.Save();
            Log($"settings put back: briefing {PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1)}, PLAY AS BLUE {OzPlayBlue.Box}, SHUTTLE box {OzShuttleStart.On}, difficulty {OpenZDifficulty.Choice}, head start {OpenZDifficulty.HeadStartToggle}");
        }
    }
}
