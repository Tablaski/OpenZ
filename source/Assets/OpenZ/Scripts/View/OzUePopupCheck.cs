#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 109 (editor only): the counted Play check of the Unit editor's simple windows, menu OpenZ/V1.2/Unit Editor/80 Popups Check
    /// (Editor/OzV12PopupsMenu.cs; Play, the unit editor open). Every action the old windows had is still reached, by real mouse and key
    /// events (routed to the game whatever window has focus, put back at the end), each press answered by a UI sound (the sound log), and
    /// every window is pictured, at 1920 x 1080 and at 1366 x 768 (the Game view set to each, put back at the end):
    ///  W1 LEAVE THE UNIT EDITOR? with a Custom change not saved: its title, the line "Your Custom stats are not saved.", STAY (ESC) | LEAVE,
    ///     SAVE &amp; LEAVE (main, ENTER), the field paused, the plate inside the screen, nothing cut (picture leave_unsaved);
    ///  A1 STAY by a click: closed, the fight runs again at its speed; Esc opens and closes it the same;
    ///  A2 SAVE &amp; LEAVE by a click: the saved profile and the file written, the toast SAVED with the file's name, it leaves (held);
    ///  W2 nothing to save: no line, STAY | LEAVE (main, ENTER) (picture leave_saved); Enter = LEAVE leaves at once (held), nothing saved;
    ///  A3 LEAVE by a click with a change not saved: no second window, the change dropped, it leaves (held);
    ///  A4 SAVE in the top bar: no window, the file written, the toast SAVED with the file's name (picture saved_toast), ui_confirm;
    ///  W3 LOAD A FILE: the file's row (name, day and time) picked and highlighted, the toggle off, OPEN FOLDER | LOAD (main, ENTER), the
    ///     close box (picture load); A5 OPEN FOLDER (counted, not handed to the file browser); A6 LOAD: the values, the field unchanged;
    ///  A7 LOAD with "Also load its units" turned on by a click (picture load_units): the field cleared and built again from the file;
    ///  A8 the close box closes LOAD;
    ///  W4 SHARE YOUR STATS (SHARE saves first): its line, DISCORD, COPY FILE PATH | OPEN FOLDER (main, ENTER), the close box (picture
    ///     share); A9 COPY FILE PATH: the path on the clipboard (put back after), the toast PATH COPIED with it; A10 DISCORD: the game's
    ///     Discord link (the links' proof switch: logged, not handed to the browser); A11 OPEN FOLDER (counted); Esc closes it;
    ///  W5 RESET ALL CUSTOM STATS? (RESET STATS in the top bar): its line, CANCEL (ESC) | RESET (main, ENTER) (picture reset); A12 Esc
    ///     cancels, nothing changes; A13 RESET by a click: every Custom value is OpenZ's, the field on OpenZ, the saved profile removed.
    /// Every save goes to the check's own folder (Folder/files_&lt;size&gt;, else the temporary cache), never to the player's Documents; the
    /// player's Custom values, the set in use, the saved profile, the play state and speed, the clipboard and the Game view size are put
    /// back. Hands off the mouse and the keyboard while it runs (about a minute). Folder (optional, set before Begin): report.txt,
    /// sound_log.txt and the pictures go there.
    /// </summary>
    public sealed class OzUePopupCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ PopupCheck]";
        public static string Report = "not run";
        /// <summary>Optional: a folder for report.txt, sound_log.txt and the pictures (null = the console only, the saves in the cache).</summary>
        public static string Folder;
        public static bool Running => run;
        static OzUePopupCheck run;
        int pass, fail;
        readonly StringBuilder lines = new StringBuilder();
        readonly List<string> pictures = new List<string>();
        Vector2 lastPos; bool lBtn;
        string size = "";

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUeShellRoot.I == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_PopupCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzUePopupCheck>();
        }

        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { Route(false); OzUeLeave.CheckHold = false; if (run == this) run = null; }

        // the editor gives mouse and key events to the game only while a Game view has focus: for the check's events the input goes to the
        // game whatever window has focus (no window focus needed), put back at the end
        bool routed; InputSettings.EditorInputBehaviorInPlayMode route0; InputSettings.BackgroundBehavior back0;
        void Route(bool on)
        {
            var s = InputSystem.settings;
            if (on && !routed) { routed = true; route0 = s.editorInputBehaviorInPlayMode; back0 = s.backgroundBehavior; s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
            else if (!on && routed) { routed = false; s.editorInputBehaviorInPlayMode = route0; s.backgroundBehavior = back0; }
        }

        // ------------------------------------------------------------------------------------------------ helpers
        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").Append("[").Append(size).Append("] ").AppendLine(what); }
        void Info(string what) { lines.Append("INFO  ").AppendLine(what); }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Seconds(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        void Move(Vector2 p)
        {
            var m = Mouse.current; if (m == null) return;
            var st = new MouseState { position = p, delta = p - lastPos };
            if (lBtn) st = st.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(m, st); lastPos = p;
        }

        IEnumerator Click(Vector2 p)
        {
            Move(p); yield return Frames(2);
            lBtn = true; Move(p); yield return Frames(2);
            lBtn = false; Move(p); yield return Frames(3);
        }

        static void Keys(params Key[] k) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); }
        static IEnumerator Tap(Key k) { Keys(k); yield return Frames(3); Keys(); yield return Frames(3); }

        /// <summary>The middle of a UI rect on the screen (as the Play proof's).</summary>
        static Vector2 Centre(RectTransform rt)
        {
            if (!rt) return Vector2.zero;
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return RectTransformUtility.WorldToScreenPoint(null, (c[0] + c[2]) * 0.5f);
        }

        /// <summary>A button of a window's row (not the close box, a row of LOAD's list or its toggle).</summary>
        static bool RowButton(RectTransform c) => c && c.Find("Bg") && c.Find("Content") && c.name != "Close" && c.name != "WithUnits" && !c.name.StartsWith("Row", StringComparison.Ordinal);

        /// <summary>A UI sound really played since t0 (the sound log: a ui_* event that was not dropped).</summary>
        static bool UiSound(float t0)
        {
            foreach (var e in OzSoundLog.Since(t0))
                if (e.key != null && e.key.StartsWith("ui_", StringComparison.Ordinal) && (e.outcome == null || !e.outcome.StartsWith("DROP", StringComparison.Ordinal))) return true;
            return false;
        }

        static int Count(float t0, string key)
        {
            int n = 0;
            foreach (var e in OzSoundLog.Since(t0)) if (e.key == key && (e.outcome == null || !e.outcome.StartsWith("DROP", StringComparison.Ordinal))) n++;
            return n;
        }

        /// <summary>A window's row buttons as shown, left to right, with the key on each cap: "STAY [ESC] | LEAVE | SAVE &amp; LEAVE [ENTER]*"
        /// (* = the highlighted main button).</summary>
        static string Row(RectTransform plate)
        {
            if (!plate || !plate.gameObject.activeInHierarchy) return "(not shown)";
            var bs = new List<(float x, string s)>();
            for (int i = 0; i < plate.childCount; i++)
            {
                var c = plate.GetChild(i) as RectTransform;
                if (!c || !c.gameObject.activeInHierarchy || !RowButton(c)) continue;
                var lt = c.Find("Content/Label"); var lm = lt ? lt.GetComponent<TextMeshProUGUI>() : null;
                var k = c.Find("Content/Key"); var km = k && k.gameObject.activeInHierarchy ? k.GetComponentInChildren<TextMeshProUGUI>() : null;
                var hb = HudOf(c);
                string s = (lm ? lm.text : c.name) + (km ? " [" + km.text + "]" : "") + (hb != null && hb.Style != HudButtonStyle.Normal ? "*" : "");
                bs.Add((c.position.x, s));
            }
            bs.Sort((a, b) => a.x.CompareTo(b.x));
            var sb = new StringBuilder();
            foreach (var b in bs) sb.Append(sb.Length > 0 ? " | " : "").Append(b.s);
            return sb.ToString();
        }

        static HudButton HudOf(RectTransform rt)
        {
            var sh = OzUeShellRoot.I; if (!sh) return null;
            foreach (var b in new[] { sh.Ask.stay, sh.Ask.noSave, sh.Ask.saveGo, sh.Ask.cancel, sh.Ask.resetNow, sh.Dialogs.discord, sh.Dialogs.copy, sh.Dialogs.folder, sh.Dialogs.loadFolder, sh.Dialogs.loadBtn })
                if (b != null && b.Rt == rt) return b;
            return null;
        }

        /// <summary>A window as shown: the plate inside the screen, every text of it on one line, not cut and above the row of buttons.
        /// words = its texts.</summary>
        static bool Fits(RectTransform plate, out string words)
        {
            words = "";
            if (!plate || !plate.gameObject.activeInHierarchy) { words = "(not shown)"; return false; }
            var c = new Vector3[4]; plate.GetWorldCorners(c);
            Vector2 lo = RectTransformUtility.WorldToScreenPoint(null, c[0]), hi = RectTransformUtility.WorldToScreenPoint(null, c[2]);
            bool ok = lo.x >= -0.5f && lo.y >= -0.5f && hi.x <= Screen.width + 0.5f && hi.y <= Screen.height + 0.5f;
            float rowTop = float.MaxValue;                                                                  // screen y (up): the highest top of the row's buttons
            for (int i = 0; i < plate.childCount; i++)
            {
                var ch = plate.GetChild(i) as RectTransform;
                if (!ch || !ch.gameObject.activeInHierarchy || !RowButton(ch)) continue;
                var bc = new Vector3[4]; ch.GetWorldCorners(bc);
                rowTop = Mathf.Min(rowTop, RectTransformUtility.WorldToScreenPoint(null, bc[1]).y);
            }
            var sb = new StringBuilder();
            for (int i = 0; i < plate.childCount; i++)
            {
                var ch = plate.GetChild(i) as RectTransform; if (!ch || !ch.gameObject.activeInHierarchy) continue;
                var tm = ch.GetComponent<TextMeshProUGUI>(); if (!tm || string.IsNullOrEmpty(tm.text)) continue;
                tm.ForceMeshUpdate();
                ok &= !tm.isTextTruncated && tm.textInfo.lineCount <= 1;
                float low = RectTransformUtility.WorldToScreenPoint(null, ch.TransformPoint(new Vector3(0f, tm.textBounds.min.y, 0f))).y;
                if (rowTop < float.MaxValue) ok &= low >= rowTop - 0.5f;
                sb.Append(sb.Length > 0 ? " / " : "").Append(tm.text);
            }
            words = sb.ToString();
            return ok;
        }

        IEnumerator Shot(string name)
        {
            if (string.IsNullOrEmpty(Folder)) yield break;
            Directory.CreateDirectory(Folder);
            string b = name + "_" + Screen.width + "x" + Screen.height, path = Path.Combine(Folder, b + ".png");
            int n = 1; while (File.Exists(path)) path = Path.Combine(Folder, b + "_" + (++n) + ".png");      // never over an older picture
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            float t0 = Time.realtimeSinceStartup;
            while (!File.Exists(path) && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            pictures.Add(path);
        }

        static Vector3 Focus(OzUnitEditor ed)
        {
            Vector3 f = ed && ed.Rts ? ed.Rts.focus : new Vector3(160, 0, -100);
            var map = ed && ed.World != null ? ed.World.Map : null;
            if (map != null) { f.x = Mathf.Clamp(f.x, 30f, map.width * 2f - 30f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 30f), -30f); }
            f.y = 0f; return f;
        }

        // ------------------------------------------------------------------------------------------------ the run
        IEnumerator Main()
        {
            Report = "running";
            Route(true);
            float tStart = Time.unscaledTime;
            var keep = OzUeSets.CustomToTree(); var set0 = OzUeSets.InUse; var profile0 = OzUeProfile.Take();
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed; bool hidden0 = OzUnitEditor.PanelsHidden;
            string folder0 = OzUeFiles.FolderOverride, clip0 = GUIUtility.systemCopyBuffer;
            bool noOpen0 = OzLinks.ProofNoOpen, noFolder0 = OzUeDialogs.ProofNoFolder;
            int view0 = OzFinalGateView.SelectedIndex();
            if (hidden0) OzUnitEditor.SetPanelsHidden(false);
            OzUePlace.Stop(); OzUePick.Unpick(); OzUePick.ClearInspect();
            OzUeLeave.CheckHold = true; OzLinks.ProofNoOpen = true; OzUeDialogs.ProofNoFolder = true;
            Exception crash = null;
            foreach (var (w, h) in new[] { (1920, 1080), (1366, 768) })
            {
                OzFinalGateView.SetFixed(w, h, "OpenZ " + w + "x" + h, out string info);
                float t0 = Time.realtimeSinceStartup;
                while ((Screen.width != w || Screen.height != h) && Time.realtimeSinceStartup - t0 < 5f) yield return null;
                yield return Seconds(0.8f);
                size = Screen.width + "x" + Screen.height;
                if (Screen.width != w || Screen.height != h) Check(false, "the Game view shows " + w + " x " + h + " (" + info + ")");
                string files = !string.IsNullOrEmpty(Folder) ? Path.Combine(Folder, "files_" + w + "x" + h) : Path.Combine(Application.temporaryCachePath, "OzUePopupCheck_" + w + "x" + h);
                Directory.CreateDirectory(files);
                OzUeFiles.FolderOverride = files;
                var round = Round(w == 1920);
                while (true)
                {
                    object cur;
                    try { if (!round.MoveNext()) break; cur = round.Current; }
                    catch (Exception e) { crash = e; break; }
                    yield return cur;
                }
                if (crash != null) { Check(false, "the check stopped on " + crash.GetType().Name + ": " + crash.Message); break; }
            }
            // put back
            size = "";
            var shell = OzUeShellRoot.I;
            if (shell) { shell.Dialogs.Close(false); while (shell.Ask.Open) shell.Ask.EscClose(); }
            OzUePlayground.ClearField();
            if (keep != null) OzUeSets.CustomFromTree(keep, out _);
            OzUeSets.Use(set0);
            OzUeProfile.Put(profile0);
            OzUeFiles.FolderOverride = folder0;
            GUIUtility.systemCopyBuffer = clip0;
            OzLinks.ProofNoOpen = noOpen0; OzUeDialogs.ProofNoFolder = noFolder0; OzUeLeave.CheckHold = false;
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            if (hidden0) OzUnitEditor.SetPanelsHidden(true);
            if (view0 >= 0) OzFinalGateView.Select(view0);
            yield return Frames(2);
            Finish(tStart);
        }

        /// <summary>One size: every window, every action.</summary>
        IEnumerator Round(bool first)
        {
            var ed = OzUnitEditor.I; var shell = OzUeShellRoot.I; var ask = shell.Ask; var dl = shell.Dialogs;
            var f = Focus(ed);
            OzUePlayground.ClearField(); yield return Frames(3);
            OzUeSets.Use(UeSet.Custom);
            OzUeFiles.Save(out _, out _); yield return Frames(2);                                              // a known start: the Custom stats saved, nothing unsaved
            OzUePlayground.SampleFight(); OzUeControls.SetSpeed(2f); OzUeControls.SetPlaying(true); yield return Seconds(0.8f);
            Move(new Vector2(Screen.width * 0.5f, Screen.height * 0.3f)); yield return Frames(3);

            // ---- W1 the leave question with a change not saved, A1 STAY
            double hp = OzUeSets.CustomValue("grunt", UeStat.Health);
            OzUeSets.SetCustom("grunt", UeStat.Health, hp + 9); yield return Frames(3);
            float t = Time.unscaledTime;
            yield return Click(Centre(shell.Top.menu.Rt)); yield return Seconds(0.4f);
            string row = Row(ask.LeaveRoot); bool fit = Fits(ask.LeaveRoot, out string words);
            bool w1 = ask.LeaveOpen && ask.Unsaved && !OzUeControls.Playing && OzUeLeave.LastStatus == OzUePlan.NotSaved && fit
                      && row == OzUePlan.Stay + " [" + OzUePlan.KeyEsc + "] | " + OzUePlan.NoSave + " | " + OzUePlan.SaveAndGo + " [" + OzUePlan.KeyEnter + "]*";
            Check(w1 && UiSound(t), "W1 MAIN MENU (a real click) asks \"" + words + "\" with " + row + " (* = the main button); the field paused (" + !OzUeControls.Playing + "); inside the screen, nothing cut (" + fit + "); a UI sound");
            yield return Shot("leave_unsaved");
            t = Time.unscaledTime;
            yield return Click(Centre(ask.stay.Rt)); yield return Frames(3);
            bool a1 = !ask.Open && OzUeControls.Playing && Mathf.Abs(OzUeControls.Speed - 2f) < 0.01f && Math.Abs(OzUeSets.CustomValue("grunt", UeStat.Health) - (hp + 9)) < 0.5;
            float te = Time.unscaledTime;
            yield return Tap(Key.Escape); bool escOpen = ask.LeaveOpen; yield return Tap(Key.Escape);
            bool escStay = escOpen && !ask.Open && OzUeControls.Playing;
            Check(a1 && escStay && UiSound(t) && UiSound(te), "A1 STAY (a click) closes it: the fight runs again at x2, the change is kept (" + a1 + "); Esc asks and Esc again stays (" + escStay + "); UI sounds");

            // ---- A2 SAVE & LEAVE
            int saves = OzUeFiles.Saves, held = OzUeLeave.Held;
            yield return Click(Centre(shell.Top.menu.Rt)); yield return Seconds(0.3f);
            t = Time.unscaledTime;
            yield return Click(Centre(ask.saveGo.Rt)); yield return Frames(4);
            bool a2 = OzUeFiles.Saves == saves + 1 && OzUeLeave.Held == held + 1 && !ask.Open && OzUeSets.UnsavedCount == 0 && File.Exists(OzUeFiles.FilePath)
                      && OzUeToast.Last == OzUePlan.SavedToast && OzUeToast.LastSub == OzUeFiles.FileName;
            Check(a2 && Count(t, "ui_confirm") >= 1, "A2 " + OzUePlan.SaveAndGo + " (a click): the saved profile and " + OzUeFiles.FileName + " written, the toast \"" + OzUeToast.Last + "  " + OzUeToast.LastSub
                  + "\", ui_confirm, then it leaves (held by the check: " + a2 + ")");

            // ---- W2 nothing to save: STAY | LEAVE, Enter = LEAVE
            yield return Click(Centre(shell.Top.menu.Rt)); yield return Seconds(0.4f);
            row = Row(ask.LeaveRoot); fit = Fits(ask.LeaveRoot, out words);
            bool w2 = ask.LeaveOpen && !ask.Unsaved && fit && (OzUeLeave.LastStatus ?? "") == "" && row == OzUePlan.Stay + " [" + OzUePlan.KeyEsc + "] | " + OzUePlan.NoSave + " [" + OzUePlan.KeyEnter + "]*";
            yield return Shot("leave_saved");
            saves = OzUeFiles.Saves; held = OzUeLeave.Held; int goes = OzUeLeave.Goes;
            t = Time.unscaledTime;
            yield return Tap(Key.Enter); yield return Frames(3);
            bool enterLeaves = OzUeLeave.Goes == goes + 1 && OzUeLeave.Held == held + 1 && !ask.Open && OzUeFiles.Saves == saves;
            Check(w2 && enterLeaves && UiSound(t), "W2 with nothing to save: \"" + words + "\", " + row + " (" + w2 + "); Enter = " + OzUePlan.NoSave + " leaves at once, nothing saved (held: " + enterLeaves + "); a UI sound");

            // ---- A3 LEAVE with a change not saved: no second window, the change dropped
            double hpSaved = OzUeSets.CustomValue("grunt", UeStat.Health);
            OzUeSets.SetCustom("grunt", UeStat.Health, hpSaved + 5); yield return Frames(3);
            yield return Click(Centre(shell.Top.menu.Rt)); yield return Seconds(0.3f);
            held = OzUeLeave.Held; int drops = OzUeLeave.DropsDone, opens = OzUeLeave.LeaveOpens;
            t = Time.unscaledTime;
            yield return Click(Centre(ask.noSave.Rt)); yield return Frames(4);
            bool a3 = OzUeLeave.DropsDone == drops + 1 && OzUeLeave.Held == held + 1 && !ask.Open && OzUeLeave.LeaveOpens == opens && OzUeSets.UnsavedCount == 0
                      && Math.Abs(OzUeSets.CustomValue("grunt", UeStat.Health) - hpSaved) < 0.5;
            Check(a3 && UiSound(t), "A3 " + OzUePlan.NoSave + " (a click) with a change not saved: no second window, the change dropped (the grunt's Custom health " + OzUeSets.CustomValue("grunt", UeStat.Health)
                  + " = the saved " + hpSaved + "), it leaves (held: " + a3 + "); a UI sound");

            // ---- A4 SAVE: no window, the toast with the file's name
            OzUeSets.SetCustom("grunt", UeStat.Health, hpSaved + 4); yield return Frames(3);
            saves = OzUeFiles.Saves; t = Time.unscaledTime;
            yield return Click(Centre(shell.Top.save.Rt)); yield return Frames(6);
            bool a4 = OzUeFiles.Saves == saves + 1 && !dl.Open && !ask.Open && OzUeSets.UnsavedCount == 0 && OzUeToast.Last == OzUePlan.SavedToast && OzUeToast.LastSub == OzUeFiles.FileName && OzUeToast.Visible;
            yield return Shot("saved_toast");
            Check(a4 && Count(t, "ui_confirm") >= 1, "A4 SAVE (a click): no window, the file written, the toast \"" + OzUeToast.Last + "\" with \"" + OzUeToast.LastSub + "\" small beside it (" + a4 + "), ui_confirm");

            // ---- W3 LOAD, A5 OPEN FOLDER, A6 LOAD
            OzUePlayground.ClearField(); yield return Frames(3);
            var g1 = OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-10f, 0f, 0f), 1); var l1 = OzUePlayground.Drop("light", Team.Blue, f + new Vector3(10f, 0f, 0f), 1);
            OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, f + new Vector3(0f, 0f, 8f), 1); yield return Frames(6);
            int red0 = OzUePlayground.CountUnits(Team.Red), blue0 = OzUePlayground.CountUnits(Team.Blue);
            yield return Click(Centre(shell.Top.save.Rt)); yield return Frames(4);                         // the file with this field
            OzUePlayground.ClearField(); yield return Frames(3);
            OzUeSets.SetCustom("grunt", UeStat.Health, hpSaved + 1); yield return Frames(3);                 // a value the LOAD takes back
            t = Time.unscaledTime;
            yield return Click(Centre(shell.Top.load.Rt)); yield return Seconds(0.4f);
            row = Row(dl.LoadRoot); fit = Fits(dl.LoadRoot, out words);
            bool picked = dl.LoadOpen && dl.FileCount >= 1 && dl.Selected == 0 && !dl.WithUnits && dl.CanUnits;
            bool w3 = picked && row == OzUeDialogs.FolderLabel + " | " + OzUeDialogs.LoadLabel + " [" + OzUePlan.KeyEnter + "]*" && dl.withUnits.B.Rt.gameObject.activeInHierarchy
                      && dl.LoadRoot.Find("Close") && dl.LoadRoot.Find("Close").gameObject.activeInHierarchy;
            yield return Shot("load");
            Check(w3 && UiSound(t), "W3 LOAD (a click): \"" + words + "\" with " + dl.FileCount + " file(s), the newest picked and highlighted, the toggle \"" + OzUeDialogs.WithUnitsLabel + "\" off, " + row + ", the close box (" + w3 + "); a UI sound");
            int fo = OzUeDialogs.FolderOpens; t = Time.unscaledTime;
            yield return Click(Centre(dl.loadFolder.Rt)); yield return Frames(3);
            Check(OzUeDialogs.FolderOpens == fo + 1 && dl.LoadOpen && UiSound(t), "A5 OPEN FOLDER in LOAD (a click): the folder opened (counted; the check does not hand it to the file browser), the window stays; a UI sound");
            int loads = OzUeFiles.Loads; t = Time.unscaledTime;
            yield return Click(Centre(dl.loadBtn.Rt)); yield return Frames(6);
            bool a6 = OzUeFiles.Loads == loads + 1 && !dl.Open && Math.Abs(OzUeSets.CustomValue("grunt", UeStat.Health) - (hpSaved + 4)) < 0.5 && OzUePlayground.CountUnits(Team.Red) == 0;
            Check(a6 && Count(t, "ui_confirm") >= 1, "A6 LOAD (a click), the toggle off: the file's values (the grunt's Custom health " + OzUeSets.CustomValue("grunt", UeStat.Health) + "), the field unchanged (empty), closed (" + a6 + "); ui_confirm");

            // ---- A7 LOAD with its units
            yield return Click(Centre(shell.Top.load.Rt)); yield return Seconds(0.3f);
            t = Time.unscaledTime;
            yield return Click(Centre(dl.withUnits.B.Rt)); yield return Frames(4);
            bool on = dl.WithUnits;
            bool toggleSound = UiSound(t);
            yield return Shot("load_units");
            loads = OzUeFiles.Loads;
            yield return Tap(Key.Enter); yield return Frames(10);
            int red1 = OzUePlayground.CountUnits(Team.Red), blue1 = OzUePlayground.CountUnits(Team.Blue);
            bool a7 = on && OzUeFiles.Loads == loads + 1 && !dl.Open && red1 == red0 && blue1 == blue0 && red0 > 0 && blue0 > 0 && OzUePlayground.OnField(OzUePlayground.Flag) == 1;
            Check(a7 && toggleSound, "A7 \"" + OzUeDialogs.WithUnitsLabel + "\" turned on by a click (" + on + ", its sound " + toggleSound + "), Enter = LOAD: the field built again from the file (red " + red1 + "/" + red0
                  + ", blue " + blue1 + "/" + blue0 + ", the flag) (" + a7 + ")");

            // ---- A8 the close box
            yield return Click(Centre(shell.Top.load.Rt)); yield return Seconds(0.3f);
            bool opened = dl.LoadOpen; t = Time.unscaledTime;
            yield return Click(Centre(dl.LoadRoot.Find("Close") as RectTransform)); yield return Frames(3);
            Check(opened && !dl.Open && Count(t, "ui_panel_close") >= 1, "A8 the close box (a click) closes LOAD with ui_panel_close");

            // ---- W4 SHARE, A9 COPY FILE PATH, A10 DISCORD, A11 OPEN FOLDER, Esc
            saves = OzUeFiles.Saves; t = Time.unscaledTime;
            yield return Click(Centre(shell.Top.share.Rt)); yield return Seconds(0.4f);
            row = Row(dl.ShareRoot); fit = Fits(dl.ShareRoot, out words);
            bool w4 = dl.ShareOpen && OzUeFiles.Saves == saves + 1 && fit && OzUeToast.Last == OzUePlan.SavedToast
                      && row == OzUeDialogs.DiscordLabel + " | " + OzUeDialogs.CopyLabel + " | " + OzUeDialogs.FolderLabel + " [" + OzUePlan.KeyEnter + "]*";
            yield return Shot("share");
            Check(w4 && UiSound(t), "W4 SHARE (a click) saves (the toast " + OzUeToast.Last + ") and shows \"" + words + "\" with " + row + " and the close box (" + w4 + "); a UI sound");
            int copies = OzUeDialogs.Copies; t = Time.unscaledTime;
            yield return Click(Centre(dl.copy.Rt)); yield return Frames(3);
            string copied = GUIUtility.systemCopyBuffer;
            Check(OzUeDialogs.Copies == copies + 1 && copied == OzUeFiles.Shown(OzUeFiles.FilePath) && OzUeToast.Last == "PATH COPIED" && OzUeToast.LastSub == copied && dl.ShareOpen && Count(t, "ui_confirm") >= 1,
                  "A9 COPY FILE PATH (a click): \"" + copied + "\" on the clipboard (put back at the end), the toast PATH COPIED with it, the window stays; ui_confirm");
            int opened0 = OzLinks.Opened; t = Time.unscaledTime;
            yield return Click(Centre(dl.discord.Rt)); yield return Frames(3);
            Check(OzLinks.Opened == opened0 + 1 && OzLinks.LastKey == OzLinks.Discord && OzLinks.LastFrom == OzUeDialogs.DiscordFrom && dl.ShareOpen && UiSound(t),
                  "A10 DISCORD (a click): the game's Discord link (" + OzLinks.Site(OzLinks.Discord) + "; the check logs it, not handed to the browser), the window stays; a UI sound");
            fo = OzUeDialogs.FolderOpens; t = Time.unscaledTime;
            yield return Click(Centre(dl.folder.Rt)); yield return Frames(3);
            bool a11 = OzUeDialogs.FolderOpens == fo + 1 && dl.ShareOpen && UiSound(t);
            t = Time.unscaledTime;
            yield return Tap(Key.Escape); yield return Frames(3);
            Check(a11 && !dl.Open && Count(t, "ui_panel_close") >= 1, "A11 OPEN FOLDER in SHARE (a click): counted, the window stays (" + a11 + "); Esc closes it with ui_panel_close");

            // ---- W5 RESET STATS, A12 Esc cancels, A13 RESET
            OzUeSets.SetCustom("light", UeStat.Damage, OzUeSets.CustomValue("light", UeStat.Damage) + 13); yield return Frames(3);
            int diff0 = OzUeSets.CustomDiffFromOpenZ; t = Time.unscaledTime;
            yield return Click(Centre(shell.Top.reset.Rt)); yield return Seconds(0.4f);
            row = Row(ask.ResetRoot); fit = Fits(ask.ResetRoot, out words);
            bool w5 = ask.ResetOpen && fit && row == OzUePlan.Cancel + " [" + OzUePlan.KeyEsc + "] | " + OzUePlan.ResetNow + " [" + OzUePlan.KeyEnter + "]*";
            yield return Shot("reset");
            Check(w5 && UiSound(t), "W5 RESET STATS (a click) asks \"" + words + "\" with " + row + " (" + w5 + "); a UI sound");
            t = Time.unscaledTime;
            yield return Tap(Key.Escape); yield return Frames(3);
            Check(!ask.Open && OzUeSets.CustomDiffFromOpenZ == diff0 && diff0 > 0 && OzUeProfile.Saved && UiSound(t), "A12 Esc cancels it: nothing changes (" + diff0 + " Custom values still differ from OpenZ, the profile still saved); a UI sound");
            yield return Click(Centre(shell.Top.reset.Rt)); yield return Seconds(0.3f);
            int done = OzUeLeave.ResetsDone; t = Time.unscaledTime;
            yield return Click(Centre(ask.resetNow.Rt)); yield return Frames(4);
            bool a13 = OzUeLeave.ResetsDone == done + 1 && OzUeSets.CustomDiffFromOpenZ == 0 && OzUeSets.InUse == UeSet.OpenZ && !OzUeProfile.Saved && !ask.Open && OzUeToast.Last == OzUePlan.ResetDone;
            Check(a13 && Count(t, "ui_confirm") >= 1, "A13 RESET (a click): every Custom value is OpenZ's, the field on OpenZ, the saved profile removed, the toast \"" + OzUeToast.Last + "\" (" + a13 + "); ui_confirm");
            OzUePlayground.ClearField(); yield return Frames(2);
        }

        void Finish(float tStart)
        {
            var sb = new StringBuilder();
            sb.Append(Tag).Append(" COUNTED CHECK 80 ").Append(fail == 0 ? "PASS" : "FAIL").Append(": ").Append(pass).Append(" of ").Append(pass + fail).Append(" checks").AppendLine();
            sb.Append(lines);
            if (pictures.Count > 0) sb.Append("pictures: ").AppendLine(string.Join(", ", pictures));
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder))
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(Path.Combine(Folder, "report.txt"), Report.Replace("\r\n", "\n"));
                    File.WriteAllText(Path.Combine(Folder, "sound_log.txt"), OzSoundLog.Format(OzSoundLog.Since(tStart)).Replace("\r\n", "\n"));
                }
                catch (Exception e) { Debug.LogWarning(Tag + " the report could not be written: " + e.Message); }
            }
            if (fail == 0) Debug.Log(Report); else Debug.LogError(Report);
            Destroy(gameObject);
        }
    }
}
#endif
