// OpenZ (GPL-3.0) - version 1.5, kit player_reports_1111 (P1 quit hang, P2 start-up timing + P2b shortcut start, P3 SPACE = tactical pause in
// the controls lists, P8 the LOW menu camera):
// the counted check (edit mode, no level needed) and the batch run of the kit's copy proof.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Player;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.5/Player Reports 11 Nov: Check. Rows:
    ///  Q1-Q4 (P1): the main menu's QUIT goes through OzQuit.Request in a built game (the editor path unchanged); OzQuit hooks
    ///   Application.wantsToQuit (window close, Alt+F4, every Application.Quit) and never refuses; the watchdog (8 s, Player.log line
    ///   "[OpenZ] quit watchdog: forced exit after") and the heat guard's stop; the quit's start line "[OpenZ] quit: start".
    ///  T1-T2 (P2): the "[OpenZ Startup]" marks in OzMenuPerf (menu ready), OzShaderWarm (start / end), OzLevelPreview (flyby prepared),
    ///   OzLevelWarm (start / end); nothing of the game's runtime code writes to the temp folder outside the proof hooks.
    ///  C1-C6 (P3): the old fixed "Go to the last message" text is in no runtime script; OpenZPauseMenu has no static Keys table; the
    ///   pause menu / F1 table and the main menu's CONTROLS table built here, with the switch's state forced on and off (no saved setting
    ///   touched): the LAST MESSAGE row names the tactical pause while it has the key and the old words while not, the game speed row names
    ///   it while on; the gamepad card's L3 · R3 row is live; the README and CHANGES & UPDATES lines say what SPACE does with the switch off.
    /// <para><see cref="Batch"/> (-executeMethod): the check + the Windows 64 player script compiles (Mono, Mono OZ_RELEASE, IL2CPP
    /// OZ_RELEASE: the backend switched for the compile only and put back); the report Logs/player_reports_1111_check.txt; exit 0 = all PASS.</para>
    /// </summary>
    public static class OzV15PlayerReportsCheck
    {
        [MenuItem("OpenZ/V1.5/Player Reports 11 Nov: Check")]
        public static void Menu() { bool ok = Run(out var report); Debug.Log(report); EditorUtility.DisplayDialog("Player Reports 11 Nov", ok ? "PASS (details in the console)" : "FAIL (details in the console)", "OK"); }

        static string Src(string rel) { string p = Path.Combine(Application.dataPath, "OpenZ/Scripts", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
        static int Count(string s, string what) { int n = 0, i = 0; while (s.Length > 0 && (i = s.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; } return n; }

        public static bool Run(out string report)
        {
            var sb = new StringBuilder(); bool ok = true;
            void Row(string id, bool pass, string what) { ok &= pass; sb.AppendLine($"{(pass ? "PASS" : "FAIL")} {id} {what}"); }

            // ---- P1 the quit
            string menu = Src("UI/OpenZMainMenu.cs"), quit = Src("View/OzQuit.cs"), heat = Src("View/HeatGuardRuntime.cs");
            Row("Q1", Count(menu, "OzQuit.Request(\"main menu QUIT\")") == 1 && Count(menu, "UnityEditor.EditorApplication.isPlaying = false;") >= 1,
                "the main menu's QUIT: OzQuit.Request in a built game, the editor's Play stop as before");
            Row("Q5", Count(quit, "Application.quitting += Quitting;") == 1 && Count(quit, "[OpenZ] quit: the engine shuts down (Application.quitting;") == 1,
                "the engine's own quitting event writes \"[OpenZ] quit: the engine shuts down\" (a player's log shows the quit went past the game's part)");
            Row("Q2", Count(quit, "Application.wantsToQuit += WantsToQuit") == 1 && Count(quit, "return true;") >= 1 && Count(quit, "if (Application.isEditor) return;") >= 1,
                "OzQuit hooks Application.wantsToQuit in built games (window close, Alt+F4, Application.Quit) and never refuses a quit; nothing in the editor");
            Row("Q3", Math.Abs(OzQuit.WatchdogSeconds - 8f) < 0.01f && Count(quit, "[OpenZ] quit watchdog: forced exit after") >= 1 && Count(quit, "Debug.Log($\"[OpenZ] quit: start (") == 1
                      && Count(quit, "vp.Stop()") == 1 && Count(quit, "PlayerPrefs.Save()") == 1,
                $"the quit's start: video players stopped, sounds stopped, settings saved, the line \"[OpenZ] quit: start\"; the watchdog {OzQuit.WatchdogSeconds:0} s with its line, the Windows backstop {OzQuit.BackstopSeconds:0} s later");
            Row("Q4", Count(heat, "public static void StopPolling()") == 1 && Count(heat, "while (!stopping") == 2, "the heat guard's reading thread stops at the quit (no new reading, no nvidia-smi start while the game shuts down)");

            // ---- P2 the start-up timing
            Row("T1", Count(Src("View/OzMenuPerf.cs"), "OzBootTime.Once(\"menu\"") == 1 && Count(Src("View/OzShaderWarm.cs"), "OzBootTime.") == 2
                      && Count(Src("UI/OzLevelPreview.cs"), "OzBootTime.Once(\"flyby \"") == 1 && Count(Src("View/OzLevelWarm.cs"), "OzBootTime.Mark(") == 2,
                "the [OpenZ Startup] marks: game code starts, first scene, main menu ready, shader warm-up start / end, each menu flyby prepared, the loading-screen warm list start / end");
            var tempUsers = new List<string>();
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                string rel = f.Replace('\\', '/'); if (rel.Contains("/Editor/")) continue;
                string t = File.ReadAllText(f);
                if (t.Contains("GetTempPath()") || t.Contains("temporaryCachePath")) tempUsers.Add(Path.GetFileName(f));
            }
            string[] known = { "OzRelease.cs", "OzCrashLog.cs", "OzLevelPreview.cs", "OzScoresPopupProof.cs", "OzCloudShadowsCheck.cs", "OzDeathTrace.cs", "OzHighScoreSidesProof.cs", "OzUePopupCheck.cs", "OzUeProof.Plain.cs" };
            var unknown = tempUsers.FindAll(x => Array.IndexOf(known, x) < 0);
            sb.AppendLine("INFO T2 the temp folder in runtime code: proof hooks, OzRelease.DevRoot (release: proof hooks only), OzCrashLog (reads Unity's crash dumps) and "
                + "OzLevelPreview (a missing flyby's error path): nothing that runs once per version (" + tempUsers.Count + " files" + (unknown.Count > 0 ? "; NOT IN THE 11 NOV LIST, look at them: " + string.Join(", ", unknown) : "") + ")");

            // ---- P2b the shortcut start, P8 the LOW menu camera
            string boot = Src("View/OzBootTime.cs");
            Row("S1", OzBootTime.TitleIsLinkName == 0x800 && OzBootTime.PebParams == 0x20 && OzBootTime.ParamsWindowFlags == 0xA4
                      && Count(boot, "string shortcut = ShortcutStart();") == 1 && Count(boot, "Marshal.WriteInt32(pp, ParamsWindowFlags, flags & ~TitleIsLinkName);") == 1
                      && Count(boot, "\", started from a shortcut: \" + shortcut") == 1 && Count(boot, "[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]") == 1,
                "P2b: the game's first code (SubsystemRegistration) takes STARTF_TITLEISLINKNAME (0x800) out of the process's start-up parameters (x64 PEB +0x20 -> +0xA4): "
                + "a .lnk shortcut start then goes on as after a double-click on the exe; the \"game code starts\" line says \"started from a shortcut\"");
            string perf = Src("View/OzMenuPerf.cs");
            Row("L1", Count(perf, "LowTrim(MenuLight && lightApplied && OzQuality.Level == 2);") == 1 && Count(perf, "LowTrim(false);") == 1
                      && Count(perf, "d.requiresColorOption = CameraOverrideOption.Off; d.requiresDepthOption = CameraOverrideOption.Off;") == 1 && Count(perf, "menuCam.allowHDR = false;") == 1,
                "P8: on LOW only, the menu camera (draws no object) renders without HDR and without the depth / colour copies while the menu light is on; put back on another preset");

            // ---- P3 the controls lists
            var oldText = new List<string>();
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                string rel = f.Replace('\\', '/'); if (rel.Contains("/Editor/")) continue;
                if (File.ReadAllText(f).Contains("\"Go to the last message\"")) oldText.Add(Path.GetFileName(f));
            }
            Row("C1", oldText.Count == 0 && typeof(OpenZPauseMenu).GetField("Keys", BindingFlags.NonPublic | BindingFlags.Static) == null,
                "the old fixed \"SPACE: Go to the last message\" table is gone (OpenZPauseMenu.Keys removed: only the unused OpenZMainMenu.ControlKeys read it)" + (oldText.Count > 0 ? "; still in " + string.Join(", ", oldText) : ""));
            string table = Src("UI/OzControlsTable.cs");
            Row("C2", Count(table, "R(\"" + OzTacticalPause.CompactMessageRow + "\"") == 1 && Count(table, "R(OzBindings.DefOf(OzAction.LastMessage).label, A(OzAction.LastMessage))") == 1
                      && Count(Src("View/OzTacticalPause.cs"), "if (text != SpeedRow) return MessageRow(label, text);") == 1,
                "both CONTROLS tables hand their LAST MESSAGE row to OzTacticalPause.ControlsRow (the live text)");
            LiveTables(Row);
            string hudPad = Src("UI/OpenZHud.TouchKeys.cs");
            Row("C5", Count(hudPad, "(OpenZHud.PadR3Key, OpenZHud.PadR3Text)") == 1 && Count(hudPad, "PadR3Keep(id, rows[i].key,") == 1 && Count(hudPad, "if (on) PadR3Refresh();") == 1,
                $"the TOUCH AND GAMEPAD card: L3 · R3 = \"{OpenZHud.PadR3Text}\", \"{OpenZHud.PadR3TextPause}\" while the switch has the LAST MESSAGE key (R3 presses that key)");
            string readme = "";
            try { readme = (string)typeof(OpenZReleaseBuild).GetMethod("PlayerReadme", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { DateTime.Now }); } catch (Exception e) { readme = "(" + e.GetType().Name + ")"; }
            string changes = File.Exists(Path.Combine(Application.dataPath, "OpenZ/Resources/OpenZ/Text/changes_updates.json")) ? File.ReadAllText(Path.Combine(Application.dataPath, "OpenZ/Resources/OpenZ/Text/changes_updates.json")) : "";
            const string offWords = "Switched off, Space goes to the last message as before.";
            Row("C6", Count(readme, "press Space to freeze time") == 1 && Count(readme, offWords) == 1 && Count(changes, "press Space to freeze time") == 1 && Count(changes, offWords) == 1,
                "README (NEW IN 1.5) and CHANGES & UPDATES: SPACE = tactical pause, and with the switch off SPACE = the last message");
            Row("S2", Count(readme, "right-click OpenZ.exe > Send to > Desktop (create shortcut)") == 1 && Count(changes, "\"title\": \"Starting from a shortcut\"") == 1,
                "P2b: README START says how to make a desktop shortcut and never to copy OpenZ.exe alone; CHANGES & UPDATES (New in 1.5) has \"Starting from a shortcut\"");

            report = (ok ? "PASS" : "FAIL") + " player_reports_1111 check (P1 quit, P2 start-up timing + P2b shortcut start, P3 controls lists, P8 LOW menu camera)\n" + sb;
            return ok;
        }

        /// <summary>C3 / C4: the pause menu / F1 table and the main menu's table, built on a hidden canvas; the switch forced on / off through
        /// its cached value (OzTacticalPause.on: no PlayerPrefs written), the rows read, everything removed again.</summary>
        static void LiveTables(Action<string, bool, string> row)
        {
            var onField = typeof(OzTacticalPause).GetField("on", BindingFlags.NonPublic | BindingFlags.Static);
            object before = onField?.GetValue(null);
            GameObject root = null;
            try
            {
                root = new GameObject("OzPlayerReportsCheck", typeof(Canvas)) { hideFlags = HideFlags.HideAndDontSave };
                var compact = new GameObject("Compact", typeof(RectTransform)).GetComponent<RectTransform>(); compact.SetParent(root.transform, false);
                var full = new GameObject("Full", typeof(RectTransform)).GetComponent<RectTransform>(); full.SetParent(root.transform, false);
                OzControlsTable.BuildCompact(compact, OzLayout.PauseW, OzLayout.PauseH);
                OzControlsTable.BuildFull(full, null, null);
                string Read(RectTransform p, bool on, out string speed)
                {
                    onField.SetValue(null, on ? 1 : 0);
                    var m = p.GetComponentsInChildren<OzTacticalPause.MessageRowText>(true);
                    var s = p.GetComponentsInChildren<OzTacticalPause.RowText>(true);
                    foreach (var x in m) x.Refresh();
                    foreach (var x in s) x.Refresh();
                    speed = s.Length == 1 ? Text(s[0].gameObject) : "(" + s.Length + " game speed rows)";
                    return m.Length == 1 ? Text(m[0].gameObject) : "(" + m.Length + " last-message rows)";
                }
                bool shared = OzBindings.KeyOf(OzAction.TacticalPause) == OzBindings.KeyOf(OzAction.LastMessage);
                string cOn = Read(compact, true, out string csOn), cOff = Read(compact, false, out string csOff);
                string fOn = Read(full, true, out string fsOn), fOff = Read(full, false, out string fsOff);
                string key = OzBindings.Cap(OzAction.LastMessage);
                row("C3", shared && cOn == OzTacticalPause.CompactMessageRowOn && cOff == OzTacticalPause.CompactMessageRow && csOn == OzTacticalPause.SpeedRowOn && csOff == OzTacticalPause.SpeedRow,
                    $"pause menu / F1 table: on \"{cOn}\" + \"{csOn}\"; off \"{cOff}\" + \"{csOff}\" (the LAST MESSAGE and TACTICAL PAUSE keys {(shared ? "both " + key : "DIFFER")})");
                string fullOff = OzBindings.DefOf(OzAction.LastMessage).label;
                row("C4", shared && fOn == OzTacticalPause.FullMessageRowOn && fOff == fullOff && fsOn == OzTacticalPause.SpeedRowOn && fsOff == OzTacticalPause.SpeedRow,
                    $"main menu CONTROLS: on \"{fOn}\" [{key}] + \"{fsOn}\"; off \"{fOff}\" [{key}] + \"{fsOff}\"");
            }
            catch (Exception e) { row("C3", false, "the tables could not be built here: " + e.GetType().Name + " " + e.Message); }
            finally
            {
                if (onField != null) onField.SetValue(null, before ?? -1);
                if (root) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static string Text(GameObject go)
        {
            var tmp = go.GetComponent<TMPro.TMP_Text>(); if (tmp) return tmp.text;
            var leg = go.GetComponent<UnityEngine.UI.Text>(); return leg ? leg.text : "?";
        }

        /// <summary>-executeMethod OpenZ.EditorTools.OzV15PlayerReportsCheck.Batch: the check + the Windows 64 player script compiles.</summary>
        public static void Batch()
        {
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                ok &= Run(out var rep); sb.AppendLine(rep);
                var nb = NamedBuildTarget.Standalone;
                var backend = PlayerSettings.GetScriptingBackend(nb);
                foreach (var (name, rel, il2cpp) in new[] { ("Mono", false, false), ("Mono OZ_RELEASE", true, false), ("IL2CPP OZ_RELEASE (the release exe's flavour)", true, true) })
                {
                    var errs = new List<string>();
                    Application.LogCallback cb = (msg, st, type) => { if ((type == LogType.Error || type == LogType.Exception) && msg.Contains("error CS")) errs.Add(msg); };
                    Application.logMessageReceived += cb;
                    int asm = 0;
                    try
                    {
                        if (il2cpp) PlayerSettings.SetScriptingBackend(nb, ScriptingImplementation.IL2CPP);
                        var set = new ScriptCompilationSettings { target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.None };
                        if (rel) set.extraScriptingDefines = new[] { "OZ_RELEASE" };
                        var res = PlayerBuildInterface.CompilePlayerScripts(set, "Temp/oz_player_reports_" + (il2cpp ? "il2cpp" : rel ? "rel" : "plain"));
                        asm = res.assemblies != null ? res.assemblies.Count : 0;
                    }
                    finally
                    {
                        if (il2cpp) PlayerSettings.SetScriptingBackend(nb, backend);
                        Application.logMessageReceived -= cb;
                    }
                    bool good = errs.Count == 0 && asm > 0;
                    ok &= good;
                    sb.AppendLine($"{(good ? "PASS" : "FAIL")} player scripts Windows 64 {name}: {asm} assemblies, {errs.Count} errors");
                    foreach (var e in errs) sb.AppendLine("   " + e);
                }
                sb.AppendLine("the Standalone scripting backend after the compiles: " + PlayerSettings.GetScriptingBackend(nb) + " (before: " + backend + ")");
                ok &= PlayerSettings.GetScriptingBackend(nb) == backend;
            }
            catch (Exception e) { ok = false; sb.AppendLine("FAIL exception: " + e); }
            sb.Insert(0, (ok ? "PASS" : "FAIL") + " player_reports_1111 batch check\n");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/player_reports_1111_check.txt", sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
