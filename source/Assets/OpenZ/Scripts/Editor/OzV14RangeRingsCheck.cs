#if !OPENZ_HARNESS
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
    /// <summary>
    /// 1.4 range_rings_option: the counted check of OPTIONS RANGE RINGS ON SELECTED UNITS (UI/OzRangeRingsOption.cs, View/AttackRangeRings.KeepOption.cs).
    /// Menu OpenZ/V1.4/Range Rings: Count (Play, a level playing). It pauses the battle (the rings run on real time, the units stand still), picks up
    /// to 3 of the player's units that have a range, and reads the real rings through AttackRangeRings' check hooks while real time passes:
    ///  P1 FADE (the default) = today: the rings show at full strength in the hold, are gone at the end of burst + hold + fade, never held.
    ///  P2 ALWAYS keeps them: from 2 s to 10 s after the pick every look finds the rings at full strength.
    ///  P3 switching back to FADE restores: the rings still shown fade (not cut) and are gone after the fade.
    ///  P4 ALWAYS, the units leave the selection: the rings are gone within the 0.2 s drop fade (+ frames).
    ///  P5 the switch to ALWAYS while the units are still selected (their rings had faded): the rings come back with their burst and stay.
    ///  P6 the default with nothing saved is FADE; the row is on GAME under HEALTH BARS; its tooltip; both menus build the row.
    ///  P7 no rule change: no Sim file reads the option.
    /// It puts back the saved option, the selection and the pause. One line "[OpenZ V1.4 range rings] PASS n/n", also written to
    /// </summary>
    public static class OzV14RangeRingsCheck
    {
        const string Tag = "[OpenZ V1.4 range rings]";
        static readonly string Dir = Path.Combine(OpenZPaths.RepoRoot, "work", "staging", "v1_4", "kits", "range_rings_option", "proof", "play");   // repo-relative (public source: no machine paths)
        public static string Report = "not run";

        static int ok, bad; static readonly StringBuilder notes = new StringBuilder();
        static void Check(bool pass, string what) { if (pass) ok++; else bad++; notes.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); }
        static void Info(string what) => notes.Append("\n  [info] ").Append(what);

        static string Src(string rel) { var p = Path.Combine(Application.dataPath, "OpenZ/Scripts", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
        static int Count(string t, string w) { int n = 0; for (int i = t.IndexOf(w, StringComparison.Ordinal); i >= 0; i = t.IndexOf(w, i + w.Length, StringComparison.Ordinal)) n++; return n; }

        // ---- the run (EditorApplication.update)
        static int phase; static double t0; static bool running;
        static ZGame game; static PlayerInput input; static AttackRangeRings rr;
        static readonly List<ZObject> picks = new List<ZObject>(), keepSel = new List<ZObject>();
        static bool keepPaused, keepAlways, hadPref; static int prefValue;
        static float expand, hold, fade, drop, full;
        // per phase counters
        static int looks, misses, shownLate, heldSeen, cutAtSwitch, fadingSeen; static float firstAfter = -1f; static double lastShown, backAt;
        static int rebuildsAtStart;

        [MenuItem("OpenZ/V1.4/Range Rings: Count (Play, a level playing)")]
        public static void Run()
        {
            if (running) { Debug.LogWarning(Tag + " already running"); return; }
            ok = 0; bad = 0; notes.Length = 0;
            if (!EditorApplication.isPlaying) { Fail("start a level in Play first"); return; }
            game = ZGame.I; input = UnityEngine.Object.FindFirstObjectByType<PlayerInput>(); rr = AttackRangeRings.I;
            if (!game || game.World == null || !input || !rr) { Fail("no level playing (ZGame, PlayerInput, AttackRangeRings)"); return; }
            rr.ProofTiming(out expand, out hold, out fade, out drop, out full);
            Info($"ring timing: burst {expand:0.00} s, hold {hold:0.00} s, fade {fade:0.00} s (all {expand + hold + fade:0.00} s), drop fade {drop:0.00} s, full alpha {full:0.00}");
            picks.Clear();
            var seen = new HashSet<string>();
            foreach (var o in game.World.OwnedSelectable(game.player))
                if (o.AttackRadius > 0 && o.IsUnit && seen.Add(o.Type)) { picks.Add(o); if (picks.Count == 3) break; }
            if (picks.Count == 0) { Fail("the player has no unit with a range in this level"); return; }
            Info("picked: " + string.Join(", ", picks.Select(o => o.Type + " #" + o.Id)));
            keepSel.Clear(); keepSel.AddRange(input.Selection);
            keepPaused = game.paused; game.paused = true;
            hadPref = PlayerPrefs.HasKey(OzRangeRingsOption.Pref); prefValue = PlayerPrefs.GetInt(OzRangeRingsOption.Pref, 0); keepAlways = OzRangeRingsOption.Always;
            rebuildsAtStart = rr.ProofKeptRebuilds;
            running = true; phase = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Begin(1);
        }

        static void Fail(string why) { bad++; notes.Append("\n  [FAIL] ").Append(why); End(); }

        static void Pick() { input.Selection.Clear(); input.UePick(picks, false); }

        static void Begin(int p)
        {
            phase = p; t0 = EditorApplication.timeSinceStartup;
            looks = misses = shownLate = heldSeen = cutAtSwitch = fadingSeen = 0; firstAfter = -1f; lastShown = -1; backAt = -1;
            switch (p)
            {
                case 1: OzRangeRingsOption.Always = false; Pick(); break;
                case 2: OzRangeRingsOption.Always = true; Pick(); break;
                case 3: OzRangeRingsOption.Always = false; break;                                                    // straight after P2: the rings are held
                case 4: OzRangeRingsOption.Always = true; Pick(); break;
                case 5: OzRangeRingsOption.Always = false; Pick(); break;
            }
        }

        static void Tick()
        {
            if (!running) return;
            try
            {
                if (!EditorApplication.isPlaying || !rr || !game) { bad++; notes.Append("\n  [FAIL] Play stopped during the check"); Finish(); return; }
                double t = EditorApplication.timeSinceStartup - t0;
                rr.ProofRingAlpha(out float min, out float max, out int sel, out int heldN);
                int n = rr.ProofRingCount;
                float life = expand + hold + fade;
                switch (phase)
                {
                    case 1:
                        if (heldN > 0) heldSeen++;
                        if (t >= 0.7 && t <= expand + hold - 0.15) { looks++; if (sel == 0 || max < full - 0.02f) misses++; }
                        if (n > 0) lastShown = t;
                        if (t >= life + 0.25 && n > 0) shownLate++;
                        if (t >= life + 0.8)
                        {
                            Check(looks > 0 && misses == 0, $"P1 FADE: in the hold the rings show at full strength ({looks - misses}/{looks} looks)");
                            Check(lastShown > expand + hold && lastShown <= life + 0.25, $"P1 FADE: the last look with a ring {lastShown:0.00} s after the pick (the end of the fade: {life:0.00} s)");
                            Check(shownLate == 0, $"P1 FADE: no ring from {life + 0.25:0.00} s on ({shownLate} looks with one)");
                            Check(heldSeen == 0, $"P1 FADE: no ring is held ({heldSeen} looks with one)");
                            Begin(2);
                        }
                        break;
                    case 2:
                        if (t >= 2.0) { looks++; if (sel == 0 || min < full - 0.001f) misses++; }
                        if (t >= 10.0)
                        {
                            Check(looks > 0 && misses == 0, $"P2 ALWAYS: from 2 s to 10 s after the pick every look finds the rings at full strength ({looks - misses}/{looks})");
                            Check(heldN > 0 && heldN == n, $"P2 ALWAYS: every ring is held ({heldN}/{n})");
                            Begin(3);
                        }
                        break;
                    case 3:
                        if (firstAfter < 0f && t > 0.0) { firstAfter = max; if (n == 0 || max < 0.5f) cutAtSwitch++; }
                        if (t >= 0.25 && t <= 0.4 && n > 0 && max < full - 0.05f && max > 0f) fadingSeen++;
                        if (t >= fade + 0.2 && n > 0) shownLate++;
                        if (t >= fade + 0.6)
                        {
                            Check(cutAtSwitch == 0, $"P3 back to FADE: the rings are not cut at the switch (first look: alpha {firstAfter:0.00})");
                            Check(fadingSeen > 0, $"P3 back to FADE: they fade ({fadingSeen} looks between 0.25 and 0.4 s with a ring fading)");
                            Check(shownLate == 0, $"P3 back to FADE: gone {fade + 0.2:0.00} s after the switch ({shownLate} looks with one)");
                            Begin(4);
                        }
                        break;
                    case 4:
                        if (t >= 3.0 && backAt < 0) { Check(heldN > 0 && sel > 0, $"P4 ALWAYS: held 3 s after the pick ({heldN} held)"); input.Selection.Clear(); backAt = t; }
                        if (backAt >= 0 && t >= backAt + drop + 0.2 && n > 0) shownLate++;
                        if (backAt >= 0 && t >= backAt + 1.0)
                        {
                            Check(shownLate == 0, $"P4 ALWAYS, the units leave the selection: the rings are gone after the {drop:0.00} s drop fade ({shownLate} looks with one)");
                            Begin(5);
                        }
                        break;
                    case 5:
                        if (t >= 3.0 && backAt < 0) { Check(n == 0, $"P5 FADE: the rings have faded 3 s after the pick ({n} shown)"); OzRangeRingsOption.Always = true; backAt = t; }
                        if (backAt >= 0 && lastShown < 0 && n > 0) lastShown = t - backAt;
                        if (backAt >= 0 && t >= backAt + 3.5)
                        {
                            Check(lastShown >= 0 && lastShown <= 0.8, $"P5 the switch to ALWAYS with the units selected: the rings come back ({(lastShown < 0 ? "never" : lastShown.ToString("0.00") + " s")} after the switch)");
                            Check(heldN > 0 && sel > 0 && min >= full - 0.001f, $"P5 and stay, held at full strength ({heldN} held, alpha {min:0.00})");
                            Statics();
                            Finish();
                        }
                        break;
                }
            }
            catch (Exception e) { bad++; notes.Append("\n  [FAIL] the check threw: " + e.Message); Finish(); }
        }

        static void Statics()
        {
            // P6 the default and the row
            PlayerPrefs.DeleteKey(OzRangeRingsOption.Pref); OzRangeRingsOption.Reload();
            Check(!OzRangeRingsOption.Always && OzRangeRingsOption.Name == "FADE", "P6 nothing saved: FADE (" + OzRangeRingsOption.Name + ")");
            Check(OzSettingTips.TabOf(OzRangeRingsOption.Label) == OzSettingTips.TabGame, "P6 the row is on the GAME tab");
            Check(OzSettingTips.TabOrder(OzRangeRingsOption.Label) == OzSettingTips.TabOrder("HEALTH BARS ON EVERY DAMAGED UNIT") + 1, "P6 directly under HEALTH BARS ON EVERY DAMAGED UNIT");
            Check(OzSettingTips.For(OzRangeRingsOption.Label) == OzRangeRingsOption.Tip, "P6 its tooltip");
            Check(Count(Src("UI/OpenZMainMenu.MenuExe.cs"), "OzRangeRingsOption.Label") == 1 && Count(Src("UI/OpenZPauseMenu.cs"), "OzRangeRingsOption.Label") == 2,
                  "P6 both menus build the row (main menu 1 row, pause menu its row + its value line)");
            // P7 no rule change
            int simReaders = 0;
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts/Sim"), "*.cs", SearchOption.AllDirectories))
            { var s = File.ReadAllText(f); if (s.Contains("OzRangeRingsOption") || s.Contains(OzRangeRingsOption.Pref) || s.Contains("OzRingHold")) simReaders++; }
            Check(simReaders == 0, $"P7 no rule change: Sim files reading the option {simReaders}");
            Info($"ALWAYS made the rings again settled {rr.ProofKeptRebuilds - rebuildsAtStart} time(s) during the check (the battle paused: 0 expected)");
        }

        static void Finish()
        {
            running = false; EditorApplication.update -= Tick;
            try
            {
                if (hadPref) PlayerPrefs.SetInt(OzRangeRingsOption.Pref, prefValue); else PlayerPrefs.DeleteKey(OzRangeRingsOption.Pref);
                PlayerPrefs.Save(); OzRangeRingsOption.Reload();
                Info($"put back: the option {(OzRangeRingsOption.Always == keepAlways ? "as it was" : "DIFFERENT")} ({OzRangeRingsOption.Name}), the selection ({keepSel.Count}), the pause ({keepPaused})");
                if (input) { input.Selection.Clear(); input.Selection.AddRange(keepSel.Where(o => o != null && !o.Destroyed)); }
                if (game) game.paused = keepPaused;
            }
            catch (Exception e) { bad++; notes.Append("\n  [FAIL] putting back threw: " + e.Message); }
            End();
        }

        static void End()
        {
            string level = ZGame.I && ZGame.I.World != null && ZGame.I.World.Map != null ? Path.GetFileNameWithoutExtension(ZGame.I.World.Map.file) : "no level";
            Report = $"{Tag} {level}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{notes}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, "range_rings_check.txt"), DateTime.Now.ToString("dd MMM yyyy HH:mm") + "\n" + Report + "\n"); }
            catch (Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
        }
    }
}
#endif
