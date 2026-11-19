using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): the Play proof of ARCHITECTURE 10.2 / DESIGN_SPEC section 12, started from the
    /// MainMenu scene (menu OpenZ/V1.2/Unit Editor/3 Play Proof (pictures), Editor/OzUeProofMenu.cs). Real input events where a click is
    /// the point (InputSystem.QueueStateEvent / QueueTextEvent, as OzCheatSpawner's self test), the modules' facades otherwise. Items:
    ///  1 the main menu shows UNIT EDITOR right under PLAY (its row above the footer); a click loads the playground; MAIN MENU (with the
    ///    confirm) returns. The statics are read here (after one visit) and again after the last visit: they must be equal.
    ///  2 at 1920 x 1080: the screen map within 2 u of spec section 1 for E001, E010, E021, E030, E040, E050; the dock in every tab one
    ///    row with the same rect. 3 at every picture size: no text under 11 u, none cut or spilling out of its box, no panel over another.
    ///  4 the OpenZ column = the game's own rows (health, build time, squad); the inspector shows each of the 20 units.
    ///  5 the set switch changes a live red and a live blue grunt's max HP and damage, OpenZ brings the exact numbers back; a Custom
    ///    grunt Health typed through the number box (click, type, Enter) changes the grunts on the field.
    ///  6 both teams by mouse: red picked + right-click a blue unit = attack; blue moves; a flag taken by red then blue, every change at
    ///    least 1 s after the last; a grunt climbs into an empty light tank; Ctrl + right-click destroys an obstacle and its tiles open;
    ///    and its release is the click (the picked squad moves there).
    ///  7 SAMPLE FIGHT for 3 game minutes at x4: no CPU commander (World.Bots 0), no game end; one side alone: no game end.
    ///  8 SAVE writes the file (FolderOverride = the proof's folder) with no window, the toast SAVED and the file's name; LOAD reads it back
    ///    (values equal); LOAD with its toggle "Also load its units" builds the field again (counts equal); SHARE saves and shows its window;
    ///    its DISCORD opens the game's Discord link (key discord, from unit_editor_share; the links' proof switch: logged, not handed to the
    ///    browser) with the click sound and the window stays open; COPY FILE PATH copies the path, OPEN FOLDER is counted (not opened), Esc
    ///    closes it and the folder holds nothing new; the IL of the editor's code calls no network API.
    ///  9 every visible control of the panels clicked once: each answers with a sound (the sound log).
    /// 10 pictures at 1920 x 1080, 1366 x 768 and 2560 x 1600 (list, inspector, the picked unit, balance, table, card, help, the 10 backgrounds,
    ///    share, share with OPEN DISCORD hovered (its tooltip), LOAD picker, paused, neutral, flag captured, obstacle hit and destroyed, turned camera, drop preview x5 with every
    ///    model counted, hover card),
    ///    0 console errors in the whole run, the statics restored. tools/p4_review_page.py puts each picture beside its prototype shot.
    /// The player's editor settings (set in use, Custom values, team, count, speed, sound, floor style and options) are put back.
    /// </summary>
    public static partial class OzUeProof
    {
        const string Tag = "[OpenZ UnitEditor proof]";
        public static string Report = "not run";
        /// <summary>Where checks.txt and the pictures go; empty = the kit's proof folder (k6_unit_editor/proof of the newest 1.2 stage).</summary>
        public static string Folder;
        public static bool Running => runner;
        static OzUeProofRunner runner;
        static int ok, bad, pictures, errors, firstErrors;
        static readonly StringBuilder lines = new StringBuilder(), errorLines = new StringBuilder();
        static string dir, picDir, filesDir, size = "";
        static Dictionary<string, string> statics0;
        static readonly List<string> held = new List<string>();
        static readonly HashSet<string> taken = new HashSet<string>();
        static bool delivered = true, lastWait;
        static Vector2? found;
#if UNITY_EDITOR
        static int gameView0 = -1;                                                                          // the Game view's own size, back at the end
#endif
        static Vector2 lastPos; static bool lBtn, rBtn, mBtn;
        static float maxDt; const float MaxProofFrame = 2f;
        /// <summary>The runner, every frame: the longest frame of a picture set (a stalled frame fails its size's check).</summary>
        internal static void NoteFrame(float dt) { if (dt > maxDt) maxDt = dt; }

        // the player's editor settings, put back at the end
        static UeSet set0; static object custom0; static Team team0; static int count0; static float speed0; static bool sound0, lines0, labels0, motion0, kept; static int style0; static string tab0;

        /// <summary>Starts the proof (Play mode, the MainMenu scene).</summary>
        public static void Begin()
        {
            if (runner) { Debug.LogWarning(Tag + " a run is still going"); return; }
            ok = bad = pictures = errors = firstErrors = 0; lines.Clear(); errorLines.Clear(); held.Clear(); taken.Clear(); statics0 = null; delivered = true; kept = false; Report = "running";
            dir = OutDir(); picDir = System.IO.Path.Combine(dir, "pictures");
            Directory.CreateDirectory(picDir);
            var go = new GameObject("OzUe_Proof"); UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<OzUeProofRunner>();
            Application.logMessageReceived += OnLog;
            runner.Push(Main());
            Debug.Log(Tag + " started: pictures and checks go to " + dir);
        }

        static string OutDir()
        {
            if (!string.IsNullOrEmpty(Folder)) return System.IO.Path.GetFullPath(Folder);
#if !OZ_RELEASE
            try
            {
                string staging = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging"));
                if (Directory.Exists(staging))
                {
                    var ds = Directory.GetDirectories(staging, "v1_2*");
                    Array.Sort(ds, StringComparer.Ordinal);
                    for (int i = ds.Length - 1; i >= 0; i--) if (Directory.Exists(System.IO.Path.Combine(ds[i], "k6_unit_editor"))) return System.IO.Path.Combine(ds[i], "k6_unit_editor", "proof");
                }
            }
            catch (Exception) { }
#endif
            return System.IO.Path.Combine(Application.persistentDataPath, "UnitEditorProof");
        }

        static void OnLog(string cond, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (cond != null && cond.StartsWith(Tag, StringComparison.Ordinal)) return;
            errors++;
            if (firstErrors++ < 20) errorLines.AppendLine("  " + type + ": " + (cond ?? "").Split('\n')[0]);
        }

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else bad++;
            lines.Append(pass ? "PASS  " : "FAIL  ").Append(size.Length > 0 ? "[" + size + "] " : "").AppendLine(what);
        }

        static void Info(string what) { lines.Append("INFO  ").AppendLine(what); }

        /// <summary>Stops a run by hand (menu 3b): the report says so, the settings are put back.</summary>
        public static void Abort()
        {
            if (!runner) { Debug.Log(Tag + " no run is going"); return; }
            Check(false, "the proof was stopped by hand");
            Finish();
        }

        internal static void Crashed(Exception e)
        {
            Check(false, "the proof stopped on an exception: " + e.GetType().Name + ": " + e.Message);
            Finish();
        }

        static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            try { Restore(); } catch (Exception e) { Info("the settings could not all be put back: " + e.Message); }
            PlainPutProfile();                                                                              // the saved Custom profile as before the run (its SAVE checks and leaves wrote it)
            OzUeFiles.FolderOverride = null;
            if (errors > 0) lines.Append("INFO  the first console errors:\n").Append(errorLines);
            bool pass = bad == 0 && delivered;
            Report = Tag + " " + (pass ? "PASS" : "FAIL") + " " + ok + "/" + (ok + bad) + " checks, " + pictures + " pictures, " + errors + " console errors -> " + dir;
            try { File.WriteAllText(System.IO.Path.Combine(dir, "checks.txt"), Report + "\n\n" + lines); } catch (Exception e) { Debug.LogWarning(Tag + " checks.txt not written: " + e.Message); }
            if (pass) Debug.Log(Report); else Debug.LogError(Report);
            Debug.Log(lines.ToString());
#if UNITY_EDITOR
            if (gameView0 >= 0) OzFinalGateView.Select(gameView0);
#endif
            if (runner) { runner.Stop(); UnityEngine.Object.Destroy(runner.gameObject); }
            runner = null;
        }

        // ================================================================== the run
        static IEnumerator Main()
        {
            PlainKeepProfile();                                                                             // the saved Custom profile as it was: put back when the run ends (View/OzUeProof.Plain.cs)
            if (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>() && OzUnitEditor.Active)
            {
                Info("started in the playground: it leaves to the main menu first");
                yield return Leave(false);
            }
            if (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>()) { Check(false, "1 the proof starts in the MainMenu scene (open it, enter Play, run the menu again)"); Finish(); yield break; }
#if UNITY_EDITOR
            gameView0 = OzFinalGateView.SelectedIndex();
#endif
            yield return SetSize(1920, 1080);
            yield return InputCheck();
            if (!delivered) { Finish(); yield break; }

            // ---------------------------------------------------------------- 1: the item, a first visit, back
            yield return EnterFromMenu(true);
            if (!OzUnitEditor.UiBuilt) { Finish(); yield break; }
            yield return 1.0f;
            foreach (var n in OzUeStatics.Names) held.Add(n);
            Info("the editor holds " + held.Count + " statics: " + string.Join(", ", held));
            yield return Leave(true);
            statics0 = Statics();

            // ---------------------------------------------------------------- the full visit
            yield return EnterFromMenu(false);
            if (!OzUnitEditor.UiBuilt) { Finish(); yield break; }
            yield return 1.5f;
            Keep();
            int fn = 1; while (Directory.Exists(System.IO.Path.Combine(dir, "files_" + fn))) fn++;
            filesDir = System.IO.Path.Combine(dir, "files_" + fn);
            Directory.CreateDirectory(filesDir);
            OzUeFiles.FolderOverride = filesDir;                                                              // every SAVE / LOAD / SHARE of the run stays in the proof's folder
            var sc = new StringBuilder(); int scFails = -1;
            try { scFails = OzUnitEditor.SelfCheck(sc); } catch (Exception e) { sc.AppendLine("SelfCheck threw " + e.Message); }
            try { File.WriteAllText(System.IO.Path.Combine(dir, "selfcheck.txt"), sc.ToString()); } catch (Exception) { }
            Check(scFails == 0, "9.2.3 OzUnitEditor.SelfCheck (the four modules' layouts at the 6 canvas sizes): " + scFails + " FAIL (selfcheck.txt)");
            size = "1920x1080";
            yield return ScreenMap();
            Audit();
            yield return Stats();
            yield return SetsDriveTheFight();
            yield return Orders();
            yield return SampleFight();
            yield return Files();
            yield return EveryControl();
            yield return PlainChecks();                                                                     // the test round: Esc, the leave question, the words, SAMPLE FIGHT, the cap, RESET STATS (View/OzUeProof.Plain.cs)
            yield return PictureSet();
            yield return SetSize(1366, 768);
            size = "1366x768";
            yield return 1.0f;
            Audit();
            yield return PictureSet();
            yield return SetSize(2560, 1600);                                                             // the owner's 16:10 screen (canvas 1728 x 1080)
            size = OzProofWords.Screen1610;
            yield return 1.0f;
            Audit();
            yield return PictureSet();
            size = "";
#if UNITY_EDITOR
            if (gameView0 >= 0) { OzFinalGateView.Select(gameView0); yield return 1.0f; }
#endif
            Restore();
            yield return 0.5f;
            yield return Leave(true);
            var statics1 = Statics();
            CompareStatics(statics0, statics1);
            Check(Mathf.Approximately(Time.timeScale, 1f), "10 the time scale is 1 again in the main menu (" + Time.timeScale + ")");
            Check(errors == 0, "10 0 console errors or exceptions in the whole run (" + errors + ")");
            Finish();
        }

        // ================================================================== 1 main menu, entering, leaving
        static IEnumerator EnterFromMenu(bool first)
        {
            var menu = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
            if (!menu) { Check(false, "1 the main menu is up"); yield break; }
            float t0 = Time.realtimeSinceStartup;
            while (!menu.ProofOnMainList && Time.realtimeSinceStartup - t0 < 5f) yield return 1;
            yield return 0.8f;                                                                              // the menu's intro (the item list fades in)
            bool has = menu.ProofUnitEditorItem(out var at, out int idx, out int play, out float bottom);
            if (first)
            {
                Check(has && play >= 0 && idx == play + 1, "1 the main menu shows UNIT EDITOR right under PLAY (item " + idx + ", PLAY " + play + ")");
                Check(has && bottom > 0f && bottom <= OzMenuLayout.FooterY - 16f, "1 its row ends at y " + bottom + ", above the footer lines (" + (OzMenuLayout.FooterY - 16f) + ")");
                yield return Shot("main_menu");
            }
            if (!has) yield break;
            int starts = OpenZMainMenu.UnitEditorStarts;
            t0 = Time.realtimeSinceStartup;
            yield return Click(at);
            if (first) Check(OpenZMainMenu.UnitEditorStarts == starts + 1, "1 a real click on UNIT EDITOR starts the playground's loading");
            if (first) yield return LoadingPicture();                                                       // its loading screen shows the playground's picture (View/OzUeProof.Plain.cs)
            yield return WaitFor(() => OzUnitEditor.UiBuilt && SceneManager.GetActiveScene().name == OzUnitEditor.SceneName, 45f);
            if (first) Check(lastWait, "1 the playground loads (scene " + OzUnitEditor.SceneName + ", the editor's UI built) in " + (Time.realtimeSinceStartup - t0).ToString("0.0", CultureInfo.InvariantCulture) + " s");
            else if (!lastWait) Check(false, "1 the playground loads a second time");
        }

        static IEnumerator Leave(bool count)
        {
            var shell = OzUeShellRoot.I;
            if (!shell || shell.Top == null) { if (count) Check(false, "1 MAIN MENU: the shell is up"); yield break; }
            int asked = OzUeShellRoot.LeaveAsked;
            yield return Click(Centre(shell.Top.menu.Rt));
            yield return WaitFor(() => shell && shell.Ask != null && shell.Ask.LeaveOpen, 3f);
            if (count) Check(OzUeShellRoot.LeaveAsked == asked + 1 && lastWait, "1 a real click on MAIN MENU asks first (" + OzUePlan.LeaveTitle + " with " + OzUePlan.Stay + " / " + OzUePlan.NoSave
                                                                                 + (shell.Ask.Unsaved ? " / " + OzUePlan.SaveAndGo : "") + ")");
            OzUeLeave.ProofHold = false;
            string folder0 = OzUeFiles.FolderOverride;
            if (string.IsNullOrEmpty(folder0)) OzUeFiles.FolderOverride = PlainLeaveFolder();                // Enter = the main button (SAVE & LEAVE with changes not saved): its file stays in the proof's folder
            yield return Tap(Key.Enter);
            if (shell && shell.Ask != null && shell.Ask.LeaveOpen) { shell.Ask.ProofEnter(); Info("the leave question took the proof's answer (the Enter key did not arrive)"); }
            yield return WaitFor(() => !OzUnitEditor.Active && UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>() != null, 30f);
            if (string.IsNullOrEmpty(folder0)) OzUeFiles.FolderOverride = folder0;
            if (count) Check(lastWait, "1 MAIN MENU returns to the main menu");
            yield return 0.5f;
        }

        static IEnumerator InputCheck()
        {
            var p = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Move(p); yield return 3;
            var m = Mouse.current;
            delivered = m != null && (m.position.ReadValue() - p).sqrMagnitude < 4f;
            Check(delivered, "the proof's input events reach the game (else: click into the Game view once and run it again)");
        }

        // ================================================================== 2 / 3 the screen map and the rect audit
        /// <summary>1.5 ue_bar_r2 (tracker A13): the E040 rect the screen map wants at 1920 x 1080: the laid dock's own width when it leaves the
        /// zoom control its place beside it, centred, y 996, 72 u high (else the old sum, which then fails).</summary>
        static Rect DockSpec(OzUeBottomLayout.Dock d, float platCells)
        {
            float w = d != null && d.plate.w <= 1920f - 2f * OzUeBottomLayout.Gap + 0.5f && d.tile >= OzUeBottomLayout.TileMin ? d.plate.w : 1219f + platCells;   // 1.5 kit ue_bottom_left_r10 (A36): no zoom beside it: the laid width inside the canvas
            return new Rect((1920f - w) * 0.5f, 996, w, 72);
        }

        static IEnumerator ScreenMap()
        {
            var cs = OzUnitEditor.CanvasSize;
            if (Mathf.Abs(cs.x - 1920f) > 1f || Mathf.Abs(cs.y - 1080f) > 1f) { Check(false, "2 the screen map needs a 1920 x 1080 canvas (it is " + cs.x + " x " + cs.y + ": UI SCALE 100 %?)"); yield break; }
            OzUePick.ClearInspect(); yield return 3;
            var dockLaid = OzUeControlsRoot.I != null && OzUeControlsRoot.I.Laid != null ? OzUeControlsRoot.I.Laid.dock : null;
            float platCells = dockLaid != null ? dockLaid.platMidTile.w + dockLaid.platHighTile.w + 2f * dockLaid.tileGap + dockLaid.boxTile.w + dockLaid.tileGap : 0f;   // 1.5 ue_boxes: + the BOX cell   // 1.3: the two ramp platforms' cells, as their labels measure
            var want = new (string id, Rect r)[] { ("E001 top bar", new Rect(0, 0, 1920, 40)), ("E010 counts plate", new Rect(12, 52, 143, 36)), ("E021 play bar", new Rect(424, 950, 638, 38)),
                                                    /* 1.5 kit ue_bottom_left_r10 (A36): no E030 zoom control in the band (the BACKGROUND popover's VIEW row) */ ("E040 dock", DockSpec(dockLaid, platCells)) /* 1.5 ue_bar_r2: centred at y 996, 72 u high, at most 1920 - 2 x (12 + the zoom + 12) wide (the zoom beside it); was the spec's 437,996 1046x72 + every added cell */, ("E050 stats box", new Rect(1486, 52, 422, 860)) };
            var panels = Panels();
            RectTransform dock = null;
            foreach (var w in want)
            {
                int best = -1; float bestD = float.MaxValue;
                for (int i = 0; i < panels.Count; i++) { var r = panels[i].r; float d = Mathf.Abs(r.x - w.r.x) + Mathf.Abs(r.y - w.r.y) + Mathf.Abs(r.width - w.r.width) + Mathf.Abs(r.height - w.r.height); if (d < bestD) { bestD = d; best = i; } }
                if (best < 0) { Check(false, "2 " + w.id + ": no panel on the screen"); continue; }
                var f = panels[best];
                bool within = Mathf.Abs(f.r.x - w.r.x) <= 2f && Mathf.Abs(f.r.y - w.r.y) <= 2f && Mathf.Abs(f.r.width - w.r.width) <= 2f && Mathf.Abs(f.r.height - w.r.height) <= 2f;
                Check(within, "2 " + w.id + " " + R(f.r) + " (" + f.name + ") within 2 u of the spec's " + R(w.r));
                if (w.id.StartsWith("E040", StringComparison.Ordinal)) dock = f.rt;
            }
            if (!dock) yield break;
            string[] tabs = { "ROBOTS", "VEHICLES", "GUNS", "SPECIAL" };
            Rect r0 = default; bool same = true, any = false; var seen = new StringBuilder();
            foreach (var t in tabs)
            {
                var label = FindText(OzUnitEditor.Layer(UeLayer.Panels), t, dock);
                if (!label) { seen.Append(t).Append(": no tab; "); same = false; continue; }
                yield return Click(Centre(label));
                yield return 4;
                var r = CanvasRect(dock);
                if (!any) { r0 = r; any = true; }
                same &= Mathf.Abs(r.x - r0.x) < 0.6f && Mathf.Abs(r.width - r0.width) < 0.6f && Mathf.Abs(r.height - r0.height) < 0.6f && Mathf.Abs(r.y - r0.y) < 0.6f;
                same &= Mathf.Abs(r.height - 72f) <= 2f;
                seen.Append(t).Append(' ').Append(R(r)).Append("; ");
            }
            Check(any && same, "2 the dock is one row (72 u) with the same rect in all 4 tabs: " + seen);
        }

        /// <summary>3: no text under 11 u, none cut or spilling out of its box, no panel over another panel.</summary>
        static void Audit()
        {
            var ed = OzUnitEditor.I; if (!ed || !ed.UiRoot) { Check(false, "3 the editor's UI root"); return; }
            int small = 0, cut = 0, n = 0; var sb = new StringBuilder();
            var field = OzUnitEditor.Layer(UeLayer.Field);
            foreach (var t in ed.UiRoot.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text) || t.color.a < 0.05f || Hidden(t.rectTransform)) continue;
                if (field && t.rectTransform.IsChildOf(field)) continue;                                   // world labels move with the field
                n++;
                if (t.fontSize < OzText.MinSize - 0.05f) { small++; if (small <= 8) sb.Append(" small: \"").Append(Short(t.text)).Append("\" ").Append(t.fontSize.ToString("0.0", CultureInfo.InvariantCulture)).Append(';'); }
                bool truncated = t.isTextTruncated || (t.textWrappingMode == TextWrappingModes.NoWrap && t.preferredWidth > t.rectTransform.rect.width + 1.5f);
                if (truncated) { cut++; if (cut <= 8) sb.Append(" cut: \"").Append(Short(t.text)).Append("\" ").Append(t.preferredWidth.ToString("0", CultureInfo.InvariantCulture)).Append('>').Append(t.rectTransform.rect.width.ToString("0", CultureInfo.InvariantCulture)).Append(';'); }
            }
            Check(small == 0, "3 no text under 11 u (" + n + " texts)" + (small > 0 ? ":" + sb : ""));
            Check(cut == 0, "3 no text cut or wider than its box (" + n + " texts)" + (cut > 0 ? ":" + sb : ""));
            var ps = Panels(); int over = 0; var ob = new StringBuilder();
            for (int i = 0; i < ps.Count; i++)
                for (int j = i + 1; j < ps.Count; j++)
                {
                    var a = ps[i].r; var b = ps[j].r;
                    float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.x, b.x), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.y, b.y);
                    if (w > 1f && h > 1f) { over++; if (over <= 6) ob.Append(' ').Append(ps[i].name).Append(" x ").Append(ps[j].name).Append(';'); }
                }
            Check(over == 0, "3 no panel overlaps another (" + ps.Count + " panels)" + (over > 0 ? ":" + ob : ""));
        }

        // ================================================================== 4 / 5 stats
        static IEnumerator Stats()
        {
            var W = OzUnitEditor.I.World;
            OzUeSets.Use(UeSet.OpenZ); yield return 3;
            int n = 0, good = 0; var sb = new StringBuilder();
            foreach (var id in Concat(OzUePlayground.Robots, OzUePlayground.Vehicles, OzUePlayground.Guns))
            {
                var u = W.S.Unit(id); if (u == null) { sb.Append(' ').Append(id).Append(": no row;"); continue; }
                n++;
                var hp = OzUeSets.Cell(id, UeStat.Health, UeSet.OpenZ); var bt = OzUeSets.Cell(id, UeStat.Build, UeSet.OpenZ); var sq = OzUeSets.Cell(id, UeStat.Squad, UeSet.OpenZ);
                bool robot = Array.IndexOf(OzUePlayground.Robots, id) >= 0;
                bool same = hp.HasNumber && Math.Abs(hp.value - u.MaxHealth) < 0.5 && bt.HasNumber && Math.Abs(bt.value - u.build_time) < 0.5 && (!robot || (sq.HasNumber && Math.Abs(sq.value - u.group_amount) < 0.5));
                if (same) good++; else sb.Append(' ').Append(id).Append(": HP ").Append(hp.value).Append(" / ").Append(u.MaxHealth).Append(", build ").Append(bt.value).Append(" / ").Append(u.build_time).Append(';');
            }
            Check(n == 21 /* 1.5 DV180 / DV181: + the Mammoth and the Bizon */ && good == n, "4 the OpenZ column = the game's own rows (health, build time, squad) for " + good + " of " + n + " units" + (good < n ? ":" + sb : ""));   // DV145: + the medium tank (7 robots, 8 vehicles, 4 guns)
            bool extra = OzUeSets.Cell(OzUePlayground.Heroes, UeStat.Health, UeSet.OpenZ).HasNumber && OzUeSets.Cell(OzUePlayground.Shuttle, UeStat.Health, UeSet.OpenZ).HasNumber;
            Check(extra, "4 the OpenZ column has the heroes' and the shuttle's health (live)");
            int shown = 0; var miss = new StringBuilder();
            foreach (var id in OzUePlayground.Units)
            {
                OzUePick.InspectType(id, OzUeKeys.ShownBlue); yield return 2;
                if (OzUePick.Kind == UeInspect.Type && OzUePick.InspectedId == id) shown++; else miss.Append(' ').Append(id);
            }
            Check(shown == OzUePlayground.Units.Length, "4 the inspector shows each of the " + OzUePlayground.Units.Length + " units (" + shown + ")" + (miss.Length > 0 ? ", not:" + miss : ""));
            OzUePick.ClearInspect(); yield return 2;
        }

        static IEnumerator SetsDriveTheFight()
        {
            OzUePlayground.ClearField(); OzUeControls.SetPlaying(false); OzUeSets.Use(UeSet.OpenZ); yield return 3;
            var f = Focus();
            var dr = OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-25, 0, 0), 1);
            var db = OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(25, 0, 0), 1);
            yield return 3;
            ZObject r = Leader(dr), b = Leader(db);
            if (r == null || b == null) { Check(false, "5 a red and a blue grunt drop (" + dr.why + " / " + db.why + ")"); yield break; }
            int hp0 = r.MaxHealth, dm0 = r.Damage;
            OzUeSets.Use(UeSet.ZWiki); yield return 4;
            var wh = OzUeSets.Cell("grunt", UeStat.Health, UeSet.ZWiki); var wd = OzUeSets.Cell("grunt", UeStat.Damage, UeSet.ZWiki);
            bool changed = wh.HasNumber && wd.HasNumber && Math.Abs(r.MaxHealth - wh.value) <= 1 && Math.Abs(b.MaxHealth - wh.value) <= 1 && Math.Abs(r.Damage - wd.value) <= 1 && Math.Abs(b.Damage - wd.value) <= 1;
            Check(changed, "5 Z-Wiki in use: a live red and a live blue grunt take max HP " + r.MaxHealth + " / " + b.MaxHealth + " and damage " + r.Damage + " / " + b.Damage + " (the set's " + wh.value + " / " + wd.value + "; OpenZ " + hp0 + " / " + dm0 + ")");
            OzUeSets.Use(UeSet.OpenZ); yield return 4;
            Check(r.MaxHealth == hp0 && r.Damage == dm0 && b.MaxHealth == hp0 && b.Damage == dm0, "5 OpenZ again: exactly " + hp0 + " HP and " + dm0 + " damage on both");
            // a Custom value typed through the number box
            var keep = OzUeSets.CustomToTree();
            OzUeSets.Use(UeSet.Custom);
            OzUePick.InspectType("grunt", OzUeKeys.ShownBlue); yield return 4;
            var box = CustomBox("Health");
            if (!box) Check(false, "5 the inspector's Custom Health box was found (the row \"Health\" under the column \"Custom\")");
            else
            {
                yield return Click(Centre(box)); yield return 3;
                bool typing = OzUeHooks.Typing;
                foreach (var ch in "1300") { InputSystem.QueueTextEvent(Keyboard.current, ch); yield return 1; }
                yield return Tap(Key.Enter); yield return 4;
                var cv = OzUeSets.Cell("grunt", UeStat.Health, UeSet.Custom);
                Check(typing && cv.HasNumber && Math.Abs(cv.value - 1300) < 0.5 && Math.Abs(r.MaxHealth - 1300) <= 1 && Math.Abs(b.MaxHealth - 1300) <= 1,
                      "5 a click on the Custom Health box (it took the keyboard: " + typing + "), 1300 typed, Enter: Custom = " + cv.value + ", the grunts on the field " + r.MaxHealth + " / " + b.MaxHealth + " HP");
            }
            if (keep != null) OzUeSets.CustomFromTree(keep, out _);
            OzUeSets.Use(UeSet.OpenZ); yield return 2;
            OzUePlayground.ClearField(); yield return 2;
        }

        // ================================================================== 6 orders for both teams, by mouse
        static IEnumerator Orders()
        {
            var ed = OzUnitEditor.I; var W = ed.World; var f = Focus();
            OzUeControls.SetSpeed(2f);
            // a, b, g, h: attack, move, pick by right-click, right drag
            OzUePlayground.ClearField(); OzUeControls.SetPlaying(false); OzUePick.Unpick(); yield return 2;
            var red = Leader(OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-12, 0, 0), 1));
            var blue = Leader(OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(12, 0, 0), 1));
            yield return 6;
            if (red == null || blue == null) { Check(false, "6 a red and a blue squad drop"); yield break; }
            yield return Click(UnitScreen(red)); yield return 2;
            Check(Has(OzUePick.Picked, red), "6 a left click on the red squad picks it");
            yield return Click(UnitScreen(blue), true); yield return 3;
            Check(Attacks(red, blue), "6 red picked + right-click on the blue squad: red attacks it (waypoint " + Wp(red) + ")");
            yield return Click(UnitScreen(blue)); yield return 2;
            var spot = f + new Vector3(12, 0, -10);
            yield return Click(ToScreen(spot), true); yield return 3;
            Check(MovesNear(blue, spot, 8f), "6 blue picked + right-click on the ground: blue moves there (waypoint " + Wp(blue) + ")");
            OzUePick.Unpick(); yield return 2;
            int redWps = red.Waypoints.Count;
            yield return Click(UnitScreen(red), true); yield return 3;
            Check(Has(OzUePick.Picked, red) && red.Waypoints.Count == redWps, "6 nothing picked: a right-click on the red squad picks it (and gives no order)");
            var cam = ed.Rts; Vector3 focus0 = cam ? cam.focus : Vector3.zero;
            var from = ToScreen(f + new Vector3(0, 0, -14)); var to = from + new Vector2(-140f, 30f);
            Vector3 toGround = Vector3.zero; bool toOk = OzUeCamera.I && OzUeCamera.I.Ground(to, out toGround);
            yield return DragButton(from, to, 1, 8);
            yield return 6;
            float moved = cam ? (cam.focus - focus0).magnitude : 0f;
            Check(moved < 0.5f && toOk && MovesNear(red, toGround, 8f), "6 a right drag (140 px) leaves the view where it was (" + moved.ToString("0.0", CultureInfo.InvariantCulture)
                  + " m; owner item 227: the right button never moves the view here) and its release is the click: the picked red squad moves there (waypoint " + Wp(red) + ")");
            OzUeControls.CentreOn(f); yield return 0.8f;

            // c: the flag, taken by red then blue, 1 s between changes at least
            OzUePlayground.ClearField(); OzUePick.Unpick(); yield return 2;
            red = Leader(OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-10, 0, 4), 1));
            blue = Leader(OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(10, 0, 4), 1));
            var flag = Leader(OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, f + new Vector3(0, 0, -6), 1));
            yield return 6;
            if (red == null || blue == null || flag == null) Check(false, "6 the flag test drops (red, blue, a neutral flag)");
            else
            {
                var changes = new List<double>(); Team last = flag.Owner;
                yield return Click(UnitScreen(red)); yield return Click(ToScreen(Pos(flag) + Vector3.up * 0.8f), true);
                OzUeControls.SetPlaying(true);
                float t0 = Time.realtimeSinceStartup;
                while (flag.Owner != Team.Red && Time.realtimeSinceStartup - t0 < 40f) { if (flag.Owner != last) { last = flag.Owner; changes.Add(W.Time); } yield return 1; }
                bool redTook = flag.Owner == Team.Red; if (redTook && last != Team.Red) { last = Team.Red; changes.Add(W.Time); }
                OzUeControls.SetPlaying(false);
                yield return Shot("flag_captured");
                yield return Click(UnitScreen(blue)); yield return Click(ToScreen(Pos(flag) + Vector3.up * 0.8f), true);
                OzUeControls.SetPlaying(true);
                t0 = Time.realtimeSinceStartup;
                while (flag.Owner != Team.Blue && Time.realtimeSinceStartup - t0 < 60f) { if (flag.Owner != last) { last = flag.Owner; changes.Add(W.Time); } yield return 1; }
                bool blueTook = flag.Owner == Team.Blue; if (blueTook && last != Team.Blue) { last = Team.Blue; changes.Add(W.Time); }
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 3f) { if (flag.Owner != last) { last = flag.Owner; changes.Add(W.Time); } yield return 1; }   // a few seconds with both squads at it
                OzUeControls.SetPlaying(false);
                double minGap = double.MaxValue; for (int i = 1; i < changes.Count; i++) minGap = Math.Min(minGap, changes[i] - changes[i - 1]);
                Check(redTook && blueTook, "6 the flag: taken by red (right-click on it with red picked), then by blue");
                Check(changes.Count < 2 || minGap >= 0.99, "6 the flag changed owner " + changes.Count + " times, never sooner than 1 s after the last change (shortest " + (changes.Count < 2 ? "-" : minGap.ToString("0.00", CultureInfo.InvariantCulture) + " s") + ")");
            }

            // c2: a GRENADES crate dropped from the dock's extra, picked up by a red grunt squad walking over it (the game's rule): the
            // crate goes with its pop and pickup sound, the squad's grenades go up, the field counts no crate after
            OzUePlayground.ClearField(); OzUePick.Unpick(); yield return 2;
            red = Leader(OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-8, 0, 0), 1));
            var crate = Leader(OzUePlayground.Drop(OzUePlayground.Grenades, Team.Neutral, f + new Vector3(4, 0, 0), 1));
            yield return 6;
            if (red == null || crate == null) Check(false, "6 the grenade crate test drops (a red squad, a GRENADES crate)");
            else
            {
                bool viewed = OzUeCrateViews.LevelView(OzUnitEditor.I.Game, crate);                            // the levels' crate view (ZGame.StaticViews, View/OzUeCrateViews.cs)
                int inBox = crate.Grenades, g0 = SquadGrenades(red), pops0 = GrenadeCrateV2.Pops, placed0 = OzUePlayground.OnField(OzUePlayground.Grenades);
                Check(viewed && inBox > 0 && placed0 == 1 && OzUePlayground.IdOf(crate) == OzUePlayground.Grenades,
                      "6 GRENADES drops the game's grenade crate with the levels' view (" + viewed + "), " + inBox + " grenades in it, 1 on the field (" + placed0 + ")");
                yield return Shot("grenades_crate_dropped");
                float heard0 = Time.unscaledTime;                                                              // the crate's sounds from the order on (OzSoundLog)
                yield return Click(UnitScreen(red)); yield return Click(ToScreen(Pos(crate) + Vector3.up * 0.4f), true);
                OzUeControls.SetPlaying(true);
                yield return WaitFor(() => crate.Destroyed || crate.KillAt >= 0 || SquadGrenades(red) > g0, 40f);
                bool took = lastWait;
                yield return 1.5f;
                OzUeControls.SetPlaying(false);
                int g1 = SquadGrenades(red), placed1 = OzUePlayground.OnField(OzUePlayground.Grenades);
                int lidHeard = OzUeCrateViews.Played(heard0, "crate_lid"), popHeard = OzUeCrateViews.Played(heard0, "crate_pickup"),
                    wrongHeard = OzUeCrateViews.Played(heard0, "rock") + OzUeCrateViews.Played(heard0, "pickup") + OzUeCrateViews.Played(heard0, "explosion");
                Check(took && g1 > g0 && placed1 == 0 && GrenadeCrateV2.Pops > pops0 && lidHeard == 1 && popHeard == 1 && wrongHeard == 0,
                      "6 a red squad sent onto the crate picks it up: grenades " + g0 + " -> " + g1 + ", the crate gone (on the field " + placed1 + "), its pop with the pickup sound " + (GrenadeCrateV2.Pops - pops0) + "x; "
                      + "the sound log: the lid " + lidHeard + "x, the ammo pickup " + popHeard + "x, a rock sound, the old fallback pickup or a crate blast " + wrongHeard + "x");
                yield return Shot("grenades_picked_up");
            }

            // d: a grunt climbs into an empty light tank
            OzUePlayground.ClearField(); OzUePick.Unpick(); yield return 2;
            blue = Leader(OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(-8, 0, 0), 1));
            var tank = Leader(OzUePlayground.Drop("light", Team.Neutral, f + new Vector3(8, 0, 0), 1));
            yield return 6;
            if (blue == null || tank == null) Check(false, "6 a blue squad and an empty light tank drop");
            else
            {
                Check(OzUePlayground.IsEmptyHardware(tank), "6 a light tank dropped under NEUTRAL stands empty");
                yield return Click(UnitScreen(blue)); yield return Click(ToScreen(Pos(tank) + Vector3.up * 1.2f), true);
                OzUeControls.SetPlaying(true);
                yield return WaitFor(() => tank.Owner == Team.Blue, 40f);
                OzUeControls.SetPlaying(false);
                Check(lastWait, "6 blue picked + right-click on the empty light tank: a grunt climbs in, the tank is blue");
            }

            // e: Ctrl + right-click destroys an obstacle, its tiles are passable again (the 1920 pictures of the hit and the end)
            yield return ObstacleAttack(true);

            // f: a squad behind an obstacle holds its fire
            OzUePlayground.ClearField(); OzUePick.Unpick(); yield return 2;
            var ob = Leader(OzUePlayground.Drop(OzUePlayground.Obstacle, Team.Neutral, f, 1));
            red = Leader(OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-6.5f, 0, 0), 1));
            blue = Leader(OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(6.5f, 0, 0), 1));
            yield return 6;
            if (ob == null || red == null || blue == null) Check(false, "6 the line-of-sight test drops (an obstacle between a red and a blue squad)");
            else
            {
                OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
                int blockedFrames = 0, hits = 0; int hp = SquadHp(red) + SquadHp(blue);
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 6f)
                {
                    yield return 1;
                    int now = SquadHp(red) + SquadHp(blue);
                    if (AllBlocked(W, red, blue)) { blockedFrames++; if (now < hp) hits++; }
                    hp = now;
                }
                OzUeControls.SetPlaying(false);
                Check(blockedFrames > 30 && hits == 0, "6 a squad behind the obstacle holds its fire: " + hits + " hits in " + blockedFrames + " frames with every line blocked");
            }
            OzUeControls.SetSpeed(1f);
            OzUePlayground.ClearField(); OzUePick.Unpick(); yield return 2;
        }

        static IEnumerator ObstacleAttack(bool count)
        {
            var W = OzUnitEditor.I.World; var f = Focus();
            OzUePlayground.ClearField(); OzUePick.Unpick(); OzUeControls.SetPlaying(false); yield return 2;
            var ob = Leader(OzUePlayground.Drop(OzUePlayground.Obstacle, Team.Neutral, f + new Vector3(6, 0, 0), 1));
            var heavy = Leader(OzUePlayground.Drop("heavy", Team.Red, f + new Vector3(-14, 0, 0), 1));
            yield return 6;
            if (ob == null || heavy == null) { if (count) Check(false, "6 an obstacle and a red heavy tank drop"); yield break; }
            int tx = ob.LocX >> 4, ty = ob.LocY >> 4, tw = Mathf.Max(1, ob.WidthPix >> 4), th = Mathf.Max(1, ob.HeightPix >> 4);
            bool blocked = !W.Path.TilePassable(tx + tw / 2, ty + th / 2, true);
            yield return Click(UnitScreen(heavy)); yield return 2;
            yield return Click(ToScreen(Pos(ob) + Vector3.up * 1.5f), true, Key.LeftCtrl); yield return 3;
            OzUeControls.SetSpeed(4f); OzUeControls.SetPlaying(true);
            bool hitShot = false;
            float t0 = Time.realtimeSinceStartup;
            while (!Gone(ob) && Time.realtimeSinceStartup - t0 < 60f)
            {
                if (!hitShot && ob.Health < ob.MaxHealth) { hitShot = true; OzUeControls.SetPlaying(false); yield return Shot("obstacle_hit"); OzUeControls.SetPlaying(true); }
                yield return 1;
            }
            bool gone = Gone(ob);
            yield return 0.4f;
            OzUeControls.SetPlaying(false);
            yield return Shot("obstacle_destroyed");
            bool open = true;
            for (int y = ty; y < ty + th; y++) for (int x = tx; x < tx + tw; x++) open &= W.Path.TilePassable(x, y, true);
            if (count) Check(blocked && gone && open, "6 Ctrl + right-click with a red heavy tank picked destroys the obstacle (its tiles were blocked: " + blocked + ", destroyed: " + gone + ", all " + tw * th + " tiles passable again: " + open + ")");
            OzUeControls.SetSpeed(1f);
        }

        // ================================================================== 7 no CPU commander, no game end
        static IEnumerator SampleFight()
        {
            var W = OzUnitEditor.I.World;
            OzUePlayground.ClearField(); yield return 2;
            int groups = OzUePlayground.SampleFight();
            OzUeControls.SetSpeed(4f); OzUeControls.SetPlaying(true);
            int botFrames = 0, endFrames = 0; double start = W.Time;
            float t0 = Time.realtimeSinceStartup;
            bool shot = false;
            while (W.Time - start < 180.0 && Time.realtimeSinceStartup - t0 < 120f)
            {
                if (W.Bots.Count != 0) botFrames++;
                if (W.GameOver) endFrames++;
                if (!shot && W.Time - start > 6.0) { shot = true; yield return Shot("sample_fight"); }
                yield return 1;
            }
            double played = W.Time - start;
            Check(groups > 0 && played >= 179.0 && botFrames == 0 && endFrames == 0, "7 SAMPLE FIGHT (" + groups + " groups) for " + played.ToString("0", CultureInfo.InvariantCulture) + " game s at x4: no CPU commander (World.Bots 0 in every frame), no game end");
            OzUeControls.SetSpeed(1f);
            OzUePlayground.ClearField(); yield return 2;
            OzUePlayground.Drop("grunt", Team.Red, Focus(), 1);
            t0 = Time.realtimeSinceStartup; double s2 = W.Time;
            while (W.Time - s2 < 4.0 && Time.realtimeSinceStartup - t0 < 10f) yield return 1;
            Check(!W.GameOver && W.Time - s2 >= 3.9, "7 one side alone on the field for 4 s: no game end");
            OzUeControls.SetPlaying(false);
            OzUePlayground.ClearField(); yield return 2;
        }

        // ================================================================== 8 files
        static IEnumerator Files()
        {
            var shell = OzUeShellRoot.I; if (!shell) { Check(false, "8 the shell"); yield break; }
            string folder = filesDir;
            OzUeFiles.FolderOverride = folder;
            string file = System.IO.Path.Combine(folder, OzUeFiles.FileName);
            int saves = OzUeFiles.Saves;
            yield return Click(Centre(shell.Top.save.Rt)); yield return 4;
            bool written = OzUeFiles.Saves == saves + 1 && File.Exists(file);
            object tree = written ? OzUeJson.Parse(File.ReadAllText(file, Encoding.UTF8)) : null;
            bool savedToast = OzUeToast.Last == OzUePlan.SavedToast && OzUeToast.LastSub == OzUeFiles.FileName && !shell.Dialogs.Open && !shell.Ask.Open;
            Check(written && savedToast && OzUeJson.Str(tree, "format", null) == OzUeFiles.Format && OzUeJson.Num(tree, "version", 0) == OzUeFiles.Version && OzUeJson.Get(tree, "units") is Dictionary<string, object>,
                  "8 a real click on SAVE writes " + OzUeFiles.FileName + " (format, version 1, the units block) with no window, the toast \"" + OzUeToast.Last + "  " + OzUeToast.LastSub + "\" (" + savedToast + ")");
            string savedUnits = OzUeJson.Write(OzUeJson.Get(tree, "units"), false);
            var keep = OzUeSets.CustomToTree();
            var moved = OzUeJson.Parse(OzUeJson.Write(keep, false)) as Dictionary<string, object>;                 // a deep copy with one Custom value moved
            if (moved != null && OzUeJson.Get(moved, "units") is Dictionary<string, object> us && us.TryGetValue("grunt", out var g) && g is Dictionary<string, object> gd)
            { gd["hp"] = OzUeJson.Num(gd, "hp", 1081) + 7; OzUeSets.CustomFromTree(moved, out _); }
            yield return 2;
            yield return Click(Centre(shell.Top.load.Rt)); yield return 4;
            Check(shell.Dialogs.LoadOpen && shell.Dialogs.FileCount >= 1, "8 a real click on LOAD opens the LOAD picker (" + shell.Dialogs.FileCount + " file(s))");
            yield return Shot("load_picker");
            int loads = OzUeFiles.Loads;
            shell.Dialogs.Select(0);
            yield return Click(Centre(shell.Dialogs.loadBtn.Rt)); yield return 4;
            string nowUnits = OzUeJson.Write(OzUeJson.Get(OzUeSets.CustomToTree(), "units"), false);
            Check(OzUeFiles.Loads == loads + 1 && !shell.Dialogs.Open && nowUnits == savedUnits && OzUeSets.InUse == UeSet.Custom,
                  "8 LOAD reads the file back: the Custom values equal the saved ones, the playground uses Custom");
            // LOAD with its units (1.2 item 109: the toggle "Also load its units" instead of a second LOAD button)
            var f = Focus();
            OzUePlayground.ClearField(); yield return 2;
            OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-10, 0, 0), 1); OzUePlayground.Drop("light", Team.Blue, f + new Vector3(10, 0, 0), 1);
            OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, f + new Vector3(0, 0, 8), 1); OzUePlayground.Drop(OzUePlayground.Obstacle, Team.Neutral, f + new Vector3(0, 0, -10), 1);
            OzUePlayground.Drop(OzUePlayground.Grenades, Team.Neutral, f + new Vector3(8, 0, 8), 1);
            yield return 6;
            int r0 = OzUePlayground.CountUnits(Team.Red), b0 = OzUePlayground.CountUnits(Team.Blue), p0 = Count(OzUePlayground.Placed());
            int crates0 = OzUePlayground.OnField(OzUePlayground.Grenades);
            yield return Click(Centre(shell.Top.save.Rt)); yield return 4;
            OzUePlayground.ClearField(); yield return 4;
            yield return Click(Centre(shell.Top.load.Rt)); yield return 4;
            shell.Dialogs.Select(0);
            bool unitsOn = shell.Dialogs.CanUnits && !shell.Dialogs.WithUnits;                                 // off at every open
            yield return Click(Centre(shell.Dialogs.withUnits.B.Rt)); yield return 3;
            unitsOn &= shell.Dialogs.WithUnits;
            yield return Click(Centre(shell.Dialogs.loadBtn.Rt)); yield return 8;
            int r1 = OzUePlayground.CountUnits(Team.Red), b1 = OzUePlayground.CountUnits(Team.Blue), p1 = Count(OzUePlayground.Placed());
            Check(unitsOn && r1 == r0 && b1 == b0 && p1 == p0, "8 LOAD with its toggle \"" + OzUeDialogs.WithUnitsLabel + "\" turned on by a real click (" + unitsOn + ") builds the field again: red " + r1 + "/" + r0
                  + ", blue " + b1 + "/" + b0 + ", placed " + p1 + "/" + p0);
            int crates1 = OzUePlayground.OnField(OzUePlayground.Grenades);
            Check(crates0 == 1 && crates1 == crates0, "8 SAVE / LOAD with its units keeps the grenade crate (" + crates1 + "/" + crates0 + ", the file's crates list)");
            // SHARE
            var before = Directory.GetFiles(folder);
            int shares = OzUeDialogs.ShareOpens;
            yield return Click(Centre(shell.Top.share.Rt)); yield return 4;
            Check(shell.Dialogs.ShareOpen && OzUeDialogs.ShareOpens == shares + 1 && OzUeToast.Last == OzUePlan.SavedToast, "8 a real click on SHARE saves (the toast \"" + OzUeToast.Last + "  " + OzUeToast.LastSub
                  + "\") and shows the SHARE window");
            yield return Shot("share");
            // DISCORD (1.2 item 30): a real click; the links' proof switch logs the address instead of handing it to the browser
            bool noOpen0 = OzLinks.ProofNoOpen; OzLinks.ProofNoOpen = true;
            int opened0 = OzLinks.Opened, snd0 = OzSoundLog.Count; float tDiscord = Time.unscaledTime;
            yield return Click(Centre(shell.Dialogs.discord.Rt)); yield return 3;
            OzLinks.ProofNoOpen = noOpen0;
            bool discordSound = OzSoundLog.Since(tDiscord).Count > 0 || OzSoundLog.Count != snd0;
            string discordUrl = OzLinks.Get(OzLinks.Discord);
            Check(OzLinks.Opened == opened0 + 1 && OzLinks.LastKey == OzLinks.Discord && OzLinks.LastFrom == OzUeDialogs.DiscordFrom && !string.IsNullOrEmpty(discordUrl)
                  && OzLinks.LastOpened == discordUrl && shell.Dialogs.ShareOpen && discordSound,
                  "8 a real click on DISCORD opens the game's Discord link (key " + OzLinks.LastKey + ", from " + OzLinks.LastFrom + ", " + OzLinks.Site(OzLinks.Discord)
                  + "; proof: logged, not handed to the browser) with the click sound (" + discordSound + "); the window stays open (" + shell.Dialogs.ShareOpen + ")");
            // COPY FILE PATH and OPEN FOLDER (the proof's switch: counted, not handed to the file browser), then Esc closes it; the player's
            // clipboard is put back
            string clip0 = GUIUtility.systemCopyBuffer; int copies = OzUeDialogs.Copies, folders = OzUeDialogs.FolderOpens; bool noFolder0 = OzUeDialogs.ProofNoFolder;
            OzUeDialogs.ProofNoFolder = true;
            yield return Click(Centre(shell.Dialogs.copy.Rt)); yield return 3;
            string copied = GUIUtility.systemCopyBuffer;
            yield return Click(Centre(shell.Dialogs.folder.Rt)); yield return 3;
            OzUeDialogs.ProofNoFolder = noFolder0;
            bool stayed = shell.Dialogs.ShareOpen;
            yield return Tap(Key.Escape); yield return 3;
            GUIUtility.systemCopyBuffer = clip0;
            var after = Directory.GetFiles(folder);
            Check(OzUeDialogs.Copies == copies + 1 && copied == OzUeFiles.Shown(OzUeFiles.FilePath) && OzUeDialogs.FolderOpens == folders + 1 && stayed && !shell.Dialogs.Open && after.Length == before.Length,
                  "8 COPY FILE PATH copies \"" + copied + "\", OPEN FOLDER opens the folder (counted; the proof does not hand it to the file browser), the window stays open for both (" + stayed
                  + "); Esc closes it; the folder holds " + after.Length + " file(s) as before: SHARE wrote the file again and nothing else");
            int calls = NetworkCalls(out int methods, out string what);
            if (methods == 0) Info("8 the code's IL cannot be read here: the no-network check is the kit's code grep only");
            else Check(calls == 0, "8 nothing is sent: the IL of the editor's own code (" + methods + " methods of its OzUe types) calls no network API (System.Net, UnityEngine.Networking)" + (calls > 0 ? ":" + what : ""));
            if (keep != null) OzUeSets.CustomFromTree(keep, out _);
            OzUePlayground.ClearField(); yield return 2;
        }

        // ================================================================== 9 every visible control once
        static IEnumerator EveryControl()
        {
            var ed = OzUnitEditor.I; var hud = ed.Hud;
            var panels = OzUnitEditor.Layer(UeLayer.Panels);
            yield return Tidy();                                                                            // the panels as every spot is read: the STATS tab, the unit list
            var spots = new List<(string path, Vector2 at, RectTransform rt)>();
            foreach (var rt in panels.GetComponentsInChildren<RectTransform>(false))
            {
                if (!rt.Find("Bg") || !rt.Find("Content") || Hidden(rt)) continue;
                var r = CanvasRect(rt); if (r.width < 6f || r.height < 6f) continue;
                spots.Add((NodePath(rt, panels), Centre(rt), rt));
            }
            int clicked = 0, sounded = 0, covered = 0, zones = 0; var silent = new StringBuilder(); var names = new HashSet<string>();
            foreach (var s in spots)
            {
                if (!s.rt || !s.rt.gameObject.activeInHierarchy) continue;                                  // its view is not shown now
                Move(s.at); yield return 3;
                var b = hud.HotButton;
                if (b == null || !b.Alive || !b.Interactable) continue;
                if (b.Rt != s.rt) { covered++; continue; }                                                  // another control lies over its centre: that one has its own spot
                if (b == OzUeShellRoot.I.Top.menu) continue;                                                // leaving is item 1's
                if (!IsControl(b)) { zones++; continue; }                                                   // a swallow zone over a panel's head or foot: no control
                if (!names.Add(s.path)) continue;
                string label = b.Label != null ? b.Label.text : "";
                float t = Time.unscaledTime;
                yield return Click(s.at); yield return 3;
                clicked++;
                if (UiSoundSince(t)) sounded++;
                else if (silent.Length < 600) silent.Append(' ').Append(string.IsNullOrEmpty(label) ? s.path : label + " (" + s.path + ")").Append(';');
                if (label == "PIN") { yield return Click(s.at); yield return 3; }                            // back to pinned
                yield return Tidy();
            }
            Info("9 spots left out: " + covered + " under another control at their centre (clicked as that control), " + zones + " swallow zones of panel heads and feet (no control: they keep clicks off the rows under them)");
            Check(clicked > 10 && sounded == clicked, "9 every visible control of the panels clicked once (" + clicked + "): " + sounded + " answered with a UI sound that played" + (sounded < clicked ? "; silent:" + silent : ""));
            Restore(); yield return 3;
        }

        /// <summary>A control the player sees and clicks: it has a click handler, a kit button face or a tooltip (a swallow zone over a
        /// panel's head or foot has none of them: it only keeps the clicks off the rows under it).</summary>
        static bool IsControl(HudButton b) => b.OnClick != null || b.OnRightClick != null || (b.Bg && b.Bg.enabled) || !string.IsNullOrEmpty(b.Tooltip);

        /// <summary>A UI sound really played since t0 (the sound log: a ui_* event that was not dropped; a dropped one is not heard, a
        /// world sound of the field does not count).</summary>
        static bool UiSoundSince(float t0)
        {
            foreach (var e in OzSoundLog.Since(t0))
                if (e.key != null && e.key.StartsWith("ui_", StringComparison.Ordinal) && (e.outcome == null || !e.outcome.StartsWith("DROP", StringComparison.Ordinal))) return true;
            return false;
        }

        /// <summary>After a click: whatever it opened is closed, the panels are back.</summary>
        static IEnumerator Tidy()
        {
            for (int i = 0; i < 4 && (OzUeHooks.AnyModal || OzUePlace.Busy || OzUeStats.TableOpen || OzUeStats.CardOpen || (OzUeShellRoot.I && OzUeShellRoot.I.Help.Open)); i++) yield return Tap(Key.Escape);
            if (OzUnitEditor.PanelsHidden) OzUnitEditor.SetPanelsHidden(false);
            if (!OzUeStats.BoxOpen) OzUeStats.ToggleBox();
            if (!OzUeControls.DockOpen) OzUeControls.ToggleDock();
            if (!OzUeControls.Playing) OzUeControls.SetPlaying(true);
            if (OzUeStats.BalanceOpen) OzUeStats.SetTab(OzUeStats.Tab.Stats);                              // back to the STATS tab and the unit list: a row click
            if (OzUePick.Kind != UeInspect.None) OzUePick.ClearInspect();                                 // showed the inspector, whose zones lay under the next spots
            yield return 2;
        }

        // ================================================================== 10 pictures
        static IEnumerator PictureSet()
        {
            var ed = OzUnitEditor.I; var shell = OzUeShellRoot.I; var f = Focus();
            maxDt = 0f;                                                                                     // the longest frame of this size's pictures
            OzUeControls.CompassHome(); OzUePick.Unpick(); OzUePick.ClearInspect();
            OzUePlayground.ClearField(); OzUeControls.SetPlaying(true); yield return 2;
            OzUePlayground.SampleFight(); yield return 4.0f;
            yield return Park();                                                                            // no tooltip, no hover card in the pictures
            yield return Shot("list");
            OzUePick.InspectType("grunt", OzUeKeys.ShownBlue); yield return 0.4f;
            yield return Shot("inspector");
            // a blue grunt squad for unit_picked and hover_card: one of the fight's when it left one, else one placed beside the fight
            ZObject unit = BlueGrunt(); bool placedOne = false;
            if (unit == null) { unit = PlaceBlueGrunt(); placedOne = unit != null; yield return 0.6f; if (Gone(unit)) { unit = PlaceBlueGrunt(); yield return 0.3f; } }
            Check(unit != null && !Gone(unit), "10 a blue grunt squad on the field for unit_picked and hover_card (" + (unit == null ? "none" : placedOne ? "the fight left none: one placed beside it" : "one of the fight's") + ")");
            if (unit != null && !Gone(unit)) { yield return Click(UnitScreen(unit)); yield return Park(); yield return 0.4f; yield return Shot("unit_picked"); }
            if (!OzUeStats.BalanceOpen) OzUeStats.ToggleBalance();
            yield return 0.4f; yield return Shot("balance");
            if (OzUeStats.BalanceOpen) OzUeStats.ToggleBalance();
            if (!OzUeStats.TableOpen) OzUeStats.ToggleTable();
            yield return 0.4f; yield return Shot("table");
            if (OzUeStats.TableOpen) OzUeStats.ToggleTable();
            OzUeStats.OpenCard("grunt", OzUeKeys.ShownBlue); yield return 0.4f;
            yield return Shot("card");
            yield return CardPictureClick();                                                               // a plain click on its picture: the UI click (View/OzUeProof.Polish.cs)
            OzUeStats.CloseCard();
            yield return Click(Centre(shell.Top.help.Rt)); yield return 0.3f;
            Check(shell.Help.Open, "10 a real click on \"?\" opens the help card");
            yield return Shot("help");
            yield return Click(Centre(shell.Top.help.Rt)); yield return 0.2f;
            Check(!shell.Help.Open, "10 \"?\" again closes it");
            // the background popover, one picture per floor style
            yield return FindByTip("BACKGROUND", 400f, 200f);
            bool popover = found.HasValue;
            if (popover) { yield return Click(found.Value); yield return 0.4f; yield return Park(); }   // the gear's tooltip off the popover
            Check(popover, "10 the gear (tooltip \"Background ...\") was found and clicked");
            int style = OzUeBackdrop.Style;
            for (int i = 0; i < OzUeBackdrop.StyleCount; i++) { OzUeBackdrop.SetStyle(i); yield return 0.5f; yield return Shot("background_" + (i + 1).ToString("00", CultureInfo.InvariantCulture)); }
            OzUeBackdrop.SetStyle(style);
            if (popover) yield return Tap(Key.Escape);
            shell.Dialogs.OpenShare(OzUnitEditor.CanvasSize); yield return 0.3f; yield return Shot("share");
            var tipBack = lastPos; Move(Centre(shell.Dialogs.discord.Rt)); yield return 0.9f;                  // OPEN DISCORD hovered: its tooltip (0.35 s)
            yield return Shot("share_discord"); Move(tipBack); yield return 2;
            shell.Dialogs.Close(false);
            shell.Dialogs.OpenLoad(OzUnitEditor.CanvasSize); yield return 0.3f; yield return Shot("load_picker"); shell.Dialogs.Close(false);
            OzUeControls.SetPlaying(false); yield return 0.3f; yield return Shot("paused"); OzUeControls.SetPlaying(true);
            var team = OzUeControls.Team; OzUeControls.SetTeam(Team.Neutral); yield return 0.4f; yield return Shot("neutral"); OzUeControls.SetTeam(team);
            // the turned and tilted camera: a real middle-button drag
            var c = new Vector2(Screen.width * 0.4f, Screen.height * 0.5f);
            float yaw0 = ed.Rts ? ed.Rts.yaw : 0f;
            yield return DragButton(c, c + new Vector2(180f, -60f), 2, 10); yield return 0.3f;
            float turned = ed.Rts ? Mathf.Abs(Mathf.DeltaAngle(ed.Rts.yaw, yaw0)) : 0f;
            Check(turned > 5f, "10 a middle-button drag turns the camera (" + turned.ToString("0", CultureInfo.InvariantCulture) + " degrees)");
            yield return Shot("turned");
            OzUeControls.CompassHome(); yield return 1.0f;
            // the drop preview of five squads (armed as a click on its dock tile arms it: with the dock's team, E041 / E183; the mouse over the field)
            var dockTeam = OzUeControls.Team; OzUeControls.SetTeam(OzUeKeys.ShownRed);
            OzUeControls.SetCount(5); OzUePlace.ToggleArm("grunt", OzUeControls.Team);
            Move(ToScreen(f + new Vector3(0, 0, 6))); yield return 0.3f;
            int wantModels = 5 * Mathf.Max(1, OzUePlayground.SquadSize("grunt"));
            Check(OzUePlaceDriver.ModelsShown == wantModels, "10 the drop preview x5 shows every robot of the 5 squads as its team's see-through ghost on its own ring (" + OzUePlaceDriver.ModelsShown + " of " + wantModels + ")");
            Check(OzUePlaceDriver.WordsOver ? OzUePlaceDriver.WordsLow >= OzUePlaceDriver.PreviewHigh + 7.5f : OzUePlaceDriver.WordsTop <= OzUePlaceDriver.PreviewLow - 7.5f,
                  "10 the drop's words lie on their plates " + (OzUePlaceDriver.WordsOver ? "over" : "under") + " the whole preview, never on its models (plates y " + F0(OzUePlaceDriver.WordsLow) + " .. "
                  + F0(OzUePlaceDriver.WordsTop) + " u, the preview y " + F0(OzUePlaceDriver.PreviewLow) + " .. " + F0(OzUePlaceDriver.PreviewHigh) + " u)");
            yield return Shot("drop_preview_x5");
            yield return DropPreviewChecks();                                                              // the arm bar's team, every part a ghost, the picture's pixels, the words off the range ring
            OzUePlace.Stop(); OzUeControls.SetCount(1); OzUeControls.SetTeam(dockTeam);
            yield return GhostPictures();                                                                  // x1 / x3 / x5, a vehicle, a gun, an empty vehicle on a quiet field (the presets at 1920)
            // the hover card over a blue grunt (placed again when the fight took the squad meanwhile), the picture taken while it shows
            yield return HoverCardPicture(unit);
            // the flag and the obstacle at this size (item 6 took them at 1920)
            if (!taken.Contains("flag_captured_" + Screen.width)) { OzUePlayground.ClearField(); yield return 2; yield return FlagPicture(); }
            if (!taken.Contains("obstacle_destroyed_" + Screen.width)) yield return ObstacleAttack(false);
            OzUePlayground.ClearField(); OzUePick.Unpick(); OzUePick.ClearInspect(); yield return 2;
            yield return UiScaleSteps();                                                                   // no panel over another at every UI SCALE step of this size
            yield return PlainPictures();                                                                  // the leave question, the reset confirm, the obstacle's words, a unit's stats label (View/OzUeProof.Plain.cs)
            Check(maxDt < MaxProofFrame, "10 no frame of this size's pictures took " + MaxProofFrame.ToString("0", CultureInfo.InvariantCulture) + " s or more (the longest "
                  + maxDt.ToString("0.00", CultureInfo.InvariantCulture) + " s; menu 4, the frame-cost check, measures the frames closely)");
        }

        static IEnumerator FlagPicture()
        {
            var f = Focus();
            var flag = Leader(OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, f, 1));
            var red = Leader(OzUePlayground.Drop("grunt", OzUeKeys.ShownRed, f + new Vector3(-4, 0, 0), 1));
            yield return 4;
            if (flag == null || red == null) yield break;
            OzUePick.Pick(new List<ZObject> { red }, false); yield return 2;
            yield return Click(ToScreen(Pos(flag) + Vector3.up * 0.8f), true);
            OzUeControls.SetSpeed(2f); OzUeControls.SetPlaying(true);
            yield return WaitFor(() => flag.Owner != Team.Neutral, 30f);
            yield return 0.4f;
            yield return Shot("flag_captured");
            OzUeControls.SetSpeed(1f);
        }

        // ================================================================== settings kept and put back
        static void Keep()
        {
            set0 = OzUeSets.InUse; custom0 = OzUeSets.CustomToTree(); team0 = OzUeControls.Team; count0 = OzUeControls.Count; speed0 = OzUeControls.Speed;
            sound0 = OzUeControls.SoundOn; style0 = OzUeBackdrop.Style; lines0 = OzUeBackdrop.FloorLines; labels0 = OzUeBackdrop.NameLabels; motion0 = OzUeBackdrop.Motion; tab0 = OzUeControls.Tab;
            kept = true;
        }

        static void Restore()
        {
            RestorePolish();                                                                               // the UI scale and the graphics preset the polish checks change
            if (!kept || !OzUnitEditor.UiBuilt) return;
            OzUePlace.Stop();
            if (custom0 != null) OzUeSets.CustomFromTree(custom0, out _);
            OzUeSets.Use(set0);
            OzUeControls.SetTeam(team0); OzUeControls.SetCount(count0); OzUeControls.SetSpeed(speed0); OzUeControls.SetSound(sound0); OzUeControls.SetPlaying(true);
            OzUeBackdrop.SetStyle(style0); OzUeBackdrop.SetFloorLines(lines0); OzUeBackdrop.SetNameLabels(labels0); OzUeBackdrop.SetMotion(motion0);
            if (OzUnitEditor.PanelsHidden) OzUnitEditor.SetPanelsHidden(false);
            if (OzUeControls.Tab != tab0) Info("the dock's tab was " + tab0 + " before the proof (it is " + OzUeControls.Tab + " now: no facade sets it, one click on its tab brings it back)");
        }

        // ================================================================== statics before / after
        static Dictionary<string, string> Statics()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            var asm = typeof(ZWorld).Assembly;
            var types = new List<Type> { typeof(ZWorld), typeof(ZObject), typeof(RtsCamera), typeof(CombatAudio) };
            foreach (var n in new[] { "OpenZ.Sim.ZHeroBoost", "OpenZ.Sim.ZHeroSquads", "OpenZ.Sim.ZElectro" }) { var t = asm.GetType(n); if (t != null) types.Add(t); }
            foreach (var t in types)
                foreach (var fi in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (fi.IsLiteral) continue;
                    var ft = fi.FieldType; object v;
                    try { v = fi.GetValue(null); } catch (Exception) { continue; }
                    string s = null;
                    if (ft == typeof(bool) || ft == typeof(float) || ft == typeof(double) || ft.IsEnum || ft == typeof(string)) s = Convert.ToString(v, CultureInfo.InvariantCulture);
                    else if (v is IDictionary dict)
                    {
                        var keys = new List<string>();
                        foreach (DictionaryEntry e in dict) keys.Add(Convert.ToString(e.Key, CultureInfo.InvariantCulture) + "=" + Convert.ToString(e.Value, CultureInfo.InvariantCulture));
                        keys.Sort(StringComparer.Ordinal); s = string.Join(";", keys);
                    }
                    if (s != null) d[t.Name + "." + fi.Name] = s;
                }
            d["Time.timeScale"] = Time.timeScale.ToString("R", CultureInfo.InvariantCulture);
            d["AudioListener.volume"] = AudioListener.volume.ToString("R", CultureInfo.InvariantCulture);
            d["AudioListener.pause"] = AudioListener.pause.ToString();
            return d;
        }

        static void CompareStatics(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a == null || b == null) { Check(false, "10 the statics were read before and after"); return; }
            var heldDiff = new StringBuilder(); var other = new StringBuilder(); int nHeld = 0, nOther = 0;
            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var v) || v == kv.Value) continue;
                string member = kv.Key.Substring(kv.Key.IndexOf('.') + 1);
                bool isHeld = kv.Key.StartsWith("Time.", StringComparison.Ordinal) || kv.Key.StartsWith("AudioListener.", StringComparison.Ordinal);
                foreach (var h in held) if (h.IndexOf(member, StringComparison.Ordinal) >= 0) { isHeld = true; break; }
                var sb = isHeld ? heldDiff : other;
                if (isHeld) nHeld++; else nOther++;
                if (sb.Length < 900) sb.Append(' ').Append(kv.Key).Append(": ").Append(Short(kv.Value)).Append(" -> ").Append(Short(v)).Append(';');
            }
            Check(nHeld == 0, "10 leaving restores every static the editor holds (" + held.Count + " held; " + a.Count + " statics of ZWorld, ZObject, RtsCamera, CombatAudio, the hero tables, the time scale and the listener compared after the first and the last visit)" + (nHeld > 0 ? ":" + heldDiff : ""));
            if (nOther > 0) Info("statics that differ but are not held by the editor (" + nOther + "):" + other);
        }

        // ================================================================== small helpers
        static IEnumerator SetSize(int w, int h)
        {
#if UNITY_EDITOR
            OzFinalGateView.SetFixed(w, h, "OpenZ " + w + "x" + h, out string info);
            float t0 = Time.realtimeSinceStartup;
            while ((Screen.width != w || Screen.height != h) && Time.realtimeSinceStartup - t0 < 5f) yield return 1;
            yield return 0.6f;
            if (Screen.width != w || Screen.height != h) Check(false, "the Game view shows " + w + " x " + h + " (" + info + "; it shows " + Screen.width + " x " + Screen.height + ")");
            else Info("Game view " + w + " x " + h + " (" + info + ")");
#else
            if (Screen.width != w || Screen.height != h) Info("pictures at " + Screen.width + " x " + Screen.height + " (a player build cannot set the size " + w + " x " + h + ")");
            yield return 1;
#endif
        }

        static IEnumerator Shot(string name)
        {
            taken.Add(name + "_" + Screen.width);
            string baseName = name + "_" + Screen.width + "x" + Screen.height, path = System.IO.Path.Combine(picDir, baseName + ".png");
            int n = 1; while (File.Exists(path)) path = System.IO.Path.Combine(picDir, baseName + "_" + (++n) + ".png");   // never over an older picture
            yield return 1;
            ScreenCapture.CaptureScreenshot(path);
            float t0 = Time.realtimeSinceStartup;
            while (!File.Exists(path) && Time.realtimeSinceStartup - t0 < 4f) yield return 1;
            if (File.Exists(path)) { pictures++; lastShot = path; }
            else { lastShot = null; Check(false, "10 the picture " + System.IO.Path.GetFileName(path) + " was written"); }
            yield return 2;
        }

        static IEnumerator WaitFor(Func<bool> cond, float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                bool c = false; try { c = cond(); } catch (Exception) { }
                if (c) { lastWait = true; yield break; }
                if (Time.realtimeSinceStartup - t0 > seconds) { lastWait = false; yield break; }
                yield return 1;
            }
        }

        static void Move(Vector2 p, Vector2 delta = default)
        {
            var m = Mouse.current; if (m == null) return;
            var st = new MouseState { position = p, delta = delta };
            if (lBtn) st = st.WithButton(MouseButton.Left);
            if (rBtn) st = st.WithButton(MouseButton.Right);
            if (mBtn) st = st.WithButton(MouseButton.Middle);
            InputSystem.QueueStateEvent(m, st); lastPos = p;
        }

        static void Keys(params Key[] k) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); }

        static IEnumerator Tap(params Key[] k) { Keys(k); yield return 3; Keys(); yield return 3; }

        static IEnumerator Click(Vector2 p, bool right = false, params Key[] hold)
        {
            bool keys = hold != null && hold.Length > 0;
            if (keys) Keys(hold);
            Move(p, p - lastPos); yield return 2;
            if (right) rBtn = true; else lBtn = true;
            Move(p); yield return 2;
            if (right) rBtn = false; else lBtn = false;
            Move(p); yield return 2;
            if (keys) { Keys(); yield return 1; }
        }

        /// <summary>A drag with button 0 left / 1 right / 2 middle, in `steps` moves (the mouse delta goes with every step).</summary>
        static IEnumerator DragButton(Vector2 a, Vector2 b, int button, int steps)
        {
            Move(a, a - lastPos); yield return 2;
            if (button == 0) lBtn = true; else if (button == 1) rBtn = true; else mBtn = true;
            Move(a); yield return 2;
            Vector2 prev = a;
            for (int i = 1; i <= steps; i++) { var p = Vector2.Lerp(a, b, i / (float)steps); Move(p, p - prev); prev = p; yield return 1; }
            yield return 1;
            lBtn = rBtn = mBtn = false;
            Move(b); yield return 3;
        }

        /// <summary>Hovers the panels' buttons in the given top-left corner (canvas u) until the HUD's hot button has a tooltip that starts with `title`.</summary>
        static IEnumerator FindByTip(string title, float maxX, float maxY)
        {
            found = null;
            var hud = OzUnitEditor.I.Hud; var panels = OzUnitEditor.Layer(UeLayer.Panels);
            var spots = new List<Vector2>();
            foreach (var rt in panels.GetComponentsInChildren<RectTransform>(false))
            {
                if (!rt.Find("Bg") || !rt.Find("Content") || Hidden(rt)) continue;
                var r = CanvasRect(rt); if (r.x > maxX || r.y > maxY) continue;
                spots.Add(Centre(rt));
            }
            foreach (var at in spots)
            {
                Move(at); yield return 3;
                var b = hud.HotButton;
                if (b != null && !string.IsNullOrEmpty(b.Tooltip) && b.Tooltip.StartsWith(title, StringComparison.OrdinalIgnoreCase)) { found = at; yield break; }
            }
        }

        static Vector2 Centre(RectTransform rt)
        {
            if (!rt) return Vector2.zero;
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return RectTransformUtility.WorldToScreenPoint(null, (c[0] + c[2]) * 0.5f);
        }

        /// <summary>A rect in canvas units, top-left origin (as the spec writes them).</summary>
        static Rect CanvasRect(RectTransform rt)
        {
            var hud = OzUnitEditor.I ? OzUnitEditor.I.Hud : null; if (!hud || !rt) return default;
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            Vector2 lo = hud.ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(null, c[0])), hi = hud.ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(null, c[2]));
            float ch = OzUnitEditor.CanvasSize.y;
            return new Rect(lo.x, ch - hi.y, hi.x - lo.x, hi.y - lo.y);
        }

        static List<(string name, Rect r, RectTransform rt)> Panels()
        {
            var l = new List<(string, Rect, RectTransform)>();
            var p = OzUnitEditor.Layer(UeLayer.Panels); if (!p) return l;
            var cs = OzUnitEditor.CanvasSize;
            for (int i = 0; i < p.childCount; i++)
            {
                var rt = p.GetChild(i) as RectTransform;
                if (!rt || !rt.gameObject.activeInHierarchy || Hidden(rt)) continue;
                var r = CanvasRect(rt); if (r.width < 4f || r.height < 4f) continue;
                // a module's holder (it fills the whole layer and draws nothing itself: the stats module's OzUeStatsPanels) is no panel: its
                // own panels are (the STATS box, its rail); the hover card follows the mouse over everything as a tooltip does: left out
                if (!rt.GetComponent<UnityEngine.UI.Graphic>() && Mathf.Abs(r.width - cs.x) < 2f && Mathf.Abs(r.height - cs.y) < 2f)
                {
                    for (int k = 0; k < rt.childCount; k++)
                    {
                        var c = rt.GetChild(k) as RectTransform;
                        if (!c || !c.gameObject.activeInHierarchy || Hidden(c) || c.name == "OzUeHoverCard") continue;
                        var cr = CanvasRect(c); if (cr.width < 4f || cr.height < 4f) continue;
                        l.Add((rt.name + "/" + c.name, cr, c));
                    }
                    continue;
                }
                l.Add((rt.name, r, rt));
            }
            return l;
        }

        /// <summary>Faded out (a CanvasGroup on the way up at alpha under 0.2).</summary>
        static bool Hidden(Transform t)
        {
            for (var x = t; x; x = x.parent) { var g = x.GetComponent<CanvasGroup>(); if (g && g.alpha < 0.2f) return true; }
            return false;
        }

        static RectTransform FindText(RectTransform under, string exact, RectTransform inside)
        {
            if (!under) return null;
            foreach (var t in under.GetComponentsInChildren<TextMeshProUGUI>(false))
                if (t.isActiveAndEnabled && t.text == exact && (!inside || t.rectTransform.IsChildOf(inside))) return t.rectTransform;
            return null;
        }

        /// <summary>The inspector's Custom number box in the row of `stat`: the number text under the column head "Custom".</summary>
        static RectTransform CustomBox(string stat)
        {
            var panels = OzUnitEditor.Layer(UeLayer.Panels); if (!panels) return null;
            var all = panels.GetComponentsInChildren<TextMeshProUGUI>(false);
            TextMeshProUGUI row = null; float cw = OzUnitEditor.CanvasSize.x;
            foreach (var t in all) if (t.isActiveAndEnabled && (t.text ?? "").StartsWith(stat, StringComparison.Ordinal) && CanvasRect(t.rectTransform).x > cw * 0.6f) { row = t; break; }
            if (!row) return null;
            var rr = CanvasRect(row.rectTransform);
            TextMeshProUGUI head = null; float best = float.MaxValue;
            foreach (var t in all)
            {
                if (!t.isActiveAndEnabled || t.text != "Custom") continue;
                var r = CanvasRect(t.rectTransform); float dy = rr.center.y - r.center.y;
                if (dy > 0f && dy < best) { best = dy; head = t; }
            }
            if (!head) return null;
            float hx = CanvasRect(head.rectTransform).center.x;
            RectTransform box = null; float bx = float.MaxValue;
            foreach (var t in all)
            {
                if (!t.isActiveAndEnabled || t == row) continue;
                var r = CanvasRect(t.rectTransform);
                if (Mathf.Abs(r.center.y - rr.center.y) > 7f) continue;
                if (!double.TryParse(StripTags(t.text), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) continue;
                float d = Mathf.Abs(r.center.x - hx);
                if (d < bx && d < 45f) { bx = d; box = t.rectTransform; }
            }
            return box;
        }

        static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s ?? "";
            var sb = new StringBuilder(); bool tag = false;
            foreach (var c in s) { if (c == '<') tag = true; else if (c == '>') tag = false; else if (!tag) sb.Append(c); }
            return sb.ToString().Trim();
        }

        /// <summary>8: every call, new, field and type token in the IL of the editor's own types (OzUe*, OzUnitEditor; their lambdas and
        /// iterators included) resolved, and its type's namespace read: none may be System.Net (sockets, web requests, HTTP) or
        /// UnityEngine.Networking. Returns the hits; methods = the method bodies read (0 where the runtime keeps no IL).</summary>
        static int NetworkCalls(out int methods, out string what)
        {
            int hits = 0; methods = 0; var sb = new StringBuilder();
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            Type[] types;
            try { types = typeof(OzUeProof).Assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = Array.FindAll(e.Types, x => x != null); }
            foreach (var t in types)
            {
                string n = t.FullName ?? "";
                if (!n.StartsWith("OpenZ.OzUe", StringComparison.Ordinal) && !n.StartsWith("OpenZ.OzUnitEditor", StringComparison.Ordinal)) continue;
                var targs = t.IsGenericType ? t.GetGenericArguments() : null;
                foreach (var mb in Bodies(t, all))
                {
                    MethodBody body; try { body = mb.GetMethodBody(); } catch (Exception) { continue; }
                    var il = body != null ? body.GetILAsByteArray() : null; if (il == null) continue;
                    methods++;
                    var margs = mb is MethodInfo mi && mi.IsGenericMethod ? mi.GetGenericArguments() : null;
                    for (int i = 0; i + 4 < il.Length; i++)
                    {
                        if (Array.IndexOf(TokenOps, il[i]) < 0) continue;
                        int tok = BitConverter.ToInt32(il, i + 1), table = (tok >> 24) & 0xFF;
                        if (table != 0x01 && table != 0x02 && table != 0x04 && table != 0x06 && table != 0x0A && table != 0x1B && table != 0x2B) continue;
                        MemberInfo r; try { r = mb.Module.ResolveMember(tok, targs, margs); } catch (Exception) { continue; }
                        var dt = r as Type ?? r?.DeclaringType; if (dt == null) continue;
                        string ns = dt.Namespace ?? "";
                        if (!ns.StartsWith("System.Net", StringComparison.Ordinal) && !ns.StartsWith("UnityEngine.Networking", StringComparison.Ordinal)) continue;
                        hits++;
                        if (sb.Length < 300) sb.Append(' ').Append(t.Name).Append('.').Append(mb.Name).Append(" -> ").Append(dt.FullName).Append(';');
                    }
                }
            }
            what = sb.ToString(); return hits;
        }

        static readonly byte[] TokenOps = { 0x28, 0x6F, 0x73, 0x7B, 0x7D, 0x7E, 0x80, 0xD0, 0x74, 0x75, 0x8C, 0x8D, 0xA5 };   // call callvirt newobj ldfld stfld ldsfld stsfld ldtoken castclass isinst box newarr unbox.any
        static IEnumerable<MethodBase> Bodies(Type t, BindingFlags f) { foreach (var m in t.GetMethods(f)) yield return m; foreach (var c in t.GetConstructors(f)) yield return c; }

        /// <summary>A live blue grunt squad's leader on the field (the side drawn blue), else null.</summary>
        static ZObject BlueGrunt()
        {
            foreach (var o in OzUePlayground.Placed()) if (o.IsUnit && !o.IsMinion && !Gone(o) && o.Owner == OzUeKeys.ShownBlue && o.Type == "grunt") return o;
            return null;
        }

        /// <summary>One blue grunt squad on the floor under the free field's left, near part: on the screen at every size, away from the
        /// middle where SAMPLE FIGHT stands (for the pictures' few seconds).</summary>
        static ZObject PlaceBlueGrunt()
        {
            var ed = OzUnitEditor.I; var ff = OzUeInsets.FreeFieldScreen;
            if (ff.width < 4f || ff.height < 4f) ff = new Rect(0f, 0f, Screen.width, Screen.height);
            if (!OzUePlaceDriver.FloorPoint(ed, new Vector2(ff.x + ff.width * 0.1f, ff.y + ff.height * 0.3f), out var at)) at = Focus();
            return Leader(OzUePlayground.Drop("grunt", OzUeKeys.ShownBlue, at, 1));
        }

        /// <summary>The pointer to a quiet spot for a picture: over the field, on no panel and no button, no unit under it (no tooltip, no
        /// hover card, no hovered unit's ring in the picture).</summary>
        static IEnumerator Park()
        {
            var hud = OzUnitEditor.I ? OzUnitEditor.I.Hud : null; if (!hud) yield break;
            var ff = OzUeInsets.FreeFieldScreen;
            if (ff.width < 4f || ff.height < 4f) ff = new Rect(0f, 0f, Screen.width, Screen.height);
            var tries = new[] { new Vector2(0.5f, 0.93f), new Vector2(0.25f, 0.9f), new Vector2(0.75f, 0.9f), new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.5f),
                                new Vector2(0.5f, 0.07f), new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.1f) };
            foreach (var k in tries)
            {
                var p = new Vector2(ff.x + ff.width * k.x, ff.y + ff.height * k.y);
                Move(p, p - lastPos); yield return 3;
                if (hud.HotButton == null && !hud.PointerOverUi && OzUePick.Hover == null) yield break;
            }
            Info("no quiet spot for the pointer at " + size + " (a picture may show a tooltip or a hover card)");
        }

        static string F0(float v) => v.ToString("0", CultureInfo.InvariantCulture);

        static Vector3 Focus()
        {
            var ed = OzUnitEditor.I;
            Vector3 f = ed && ed.Rts ? ed.Rts.focus : new Vector3(160, 0, -100);
            var map = ed && ed.World != null ? ed.World.Map : null;
            if (map != null) { f.x = Mathf.Clamp(f.x, 30f, map.width * 2f - 30f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 30f), -30f); }
            f.y = 0f; return f;
        }

        static Vector2 ToScreen(Vector3 world) { var cam = OzUeDraw.Cam; return cam ? (Vector2)cam.WorldToScreenPoint(world) : Vector2.zero; }

        static Vector2 UnitScreen(ZObject o)
        {
            var ed = OzUnitEditor.I;
            if (ed && ed.Input) { var r = ed.Input.ScreenRect(o); if (r.width > 0f) return r.center; }
            return ToScreen(Pos(o) + Vector3.up * 0.9f);
        }

        static Vector3 Pos(ZObject o) => OzUeMarks.Pos(OzUnitEditor.I.Game, o);
        static ZObject Leader(UeDrop d) => d.ok && d.made != null && d.made.Count > 0 ? d.made[0] : null;
        /// <summary>The grenades a squad carries (leader and members).</summary>
        static int SquadGrenades(ZObject o)
        {
            if (o == null) return 0;
            var lead = o.Leader ?? o; int n = lead.Grenades;
            foreach (var m in lead.Minions) if (m != null) n += m.Grenades;
            return n;
        }
        static bool Gone(ZObject o) => o == null || o.Destroyed || o.KillAt >= 0;
        static bool Has(IReadOnlyList<ZObject> l, ZObject o) { if (l == null || o == null) return false; for (int i = 0; i < l.Count; i++) if (l[i] == o || (l[i] != null && l[i].Leader == o)) return true; return false; }

        static bool Attacks(ZObject u, ZObject target)
        {
            foreach (var w in u.Waypoints) { if (w.mode != WpMode.Attack) continue; var t = OzUnitEditor.I.World.Get(w.refId); if (t == target || (t != null && (t.Leader == target || target.Minions.Contains(t)))) return true; }
            return false;
        }

        static bool MovesNear(ZObject u, Vector3 world, float metres)
        {
            var px = ZGame.WorldToPx(world);
            foreach (var w in u.Waypoints) if ((w.mode == WpMode.Move || w.mode == WpMode.ForceMove) && Mathf.Abs(w.x - px.x) <= metres * 8f && Mathf.Abs(w.y - px.y) <= metres * 8f) return true;
            return false;
        }

        static string Wp(ZObject u)
        {
            if (u == null || u.Waypoints.Count == 0) return "none";
            var w = u.Waypoints[0]; return w.mode + " " + w.refId + " @" + w.x + "," + w.y + " x" + u.Waypoints.Count;
        }

        static int SquadHp(ZObject leader) { if (leader == null) return 0; int s = Mathf.Max(0, leader.Health); foreach (var m in leader.Minions) if (m != null) s += Mathf.Max(0, m.Health); return s; }

        static bool AllBlocked(ZWorld W, ZObject red, ZObject blue)
        {
            var a = Members(red); var b = Members(blue); bool any = false;
            foreach (var x in a) foreach (var y in b)
                {
                    long dx = x.CenterX - y.CenterX, dy = x.CenterY - y.CenterY, r = Math.Max(x.AttackRadius, y.AttackRadius);
                    if (dx * dx + dy * dy > r * r) continue;
                    any = true;
                    if (!W.EngageBarrierBetween(x.CenterX, x.CenterY, y.CenterX, y.CenterY)) return false;
                }
            return any;
        }

        static List<ZObject> Members(ZObject leader) { var l = new List<ZObject>(); if (leader == null) return l; if (!Gone(leader)) l.Add(leader); foreach (var m in leader.Minions) if (!Gone(m)) l.Add(m); return l; }
        static int Count(IEnumerable<ZObject> e) { int n = 0; foreach (var _ in e) n++; return n; }
        static IEnumerable<string> Concat(params string[][] a) { foreach (var x in a) foreach (var y in x) yield return y; }
        static string R(Rect r) => r.x.ToString("0.0", CultureInfo.InvariantCulture) + "," + r.y.ToString("0.0", CultureInfo.InvariantCulture) + " " + r.width.ToString("0.0", CultureInfo.InvariantCulture) + "x" + r.height.ToString("0.0", CultureInfo.InvariantCulture);
        static string Short(string s) { s = StripTags(s); return s.Length > 40 ? s.Substring(0, 40) + "..." : s; }
        static string NodePath(Transform t, Transform root) { var sb = new StringBuilder(t.name); for (var x = t.parent; x && x != root; x = x.parent) sb.Insert(0, x.name + "/"); return sb.ToString(); }
    }

    /// <summary>Steps the proof once a frame and survives the scene loads (DontDestroyOnLoad). A step yields null / an int (frames),
    /// a float (seconds of real time: the game may be paused) or another IEnumerator (run first).</summary>
    internal sealed class OzUeProofRunner : MonoBehaviour
    {
        readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        int waitFrame; float waitUntil; bool stopped;
        internal void Push(IEnumerator e) { stack.Push(e); }
        internal void Stop() { stopped = true; stack.Clear(); }

        void Update()
        {
            if (stopped) return;
            OzUeProof.NoteFrame(Time.unscaledDeltaTime);
            if (stack.Count == 0) { stopped = true; return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (Exception e) { Debug.LogException(e); stopped = true; stack.Clear(); OzUeProof.Crashed(e); return; }
            if (stopped) return;                                                                           // the step finished the proof (Finish stops the runner)
            if (!more) { stack.Pop(); return; }
            var c = top.Current;
            if (c is IEnumerator child) stack.Push(child);
            else if (c is float s) waitUntil = Time.realtimeSinceStartup + s;
            else waitFrame = Time.frameCount + (c is int n ? Mathf.Max(1, n) : 1);
        }
    }
}
