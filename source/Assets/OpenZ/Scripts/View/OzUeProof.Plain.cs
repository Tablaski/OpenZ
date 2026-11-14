using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenZ.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the Unit editor's Play proof, View/OzUeProof.cs): the checks of the test round's changes. At the first visit the loading
    /// screen's picture; at 1920 x 1080 (after item 9):
    ///  11 Esc with nothing open asks LEAVE THE UNIT EDITOR? and pauses the field, Esc again = STAY (the fight runs again at its speed; a
    ///     paused field stays paused); Esc closes one thing at a time (the help card, the unit card, the TABLE, then the question); MAIN MENU
    ///     asks the same: with a Custom change not saved its one line says so and it offers STAY | LEAVE, SAVE &amp; LEAVE (the main button,
    ///     ENTER), no reset; LEAVE drops the change and goes at once, no second question; SAVE &amp; LEAVE writes the saved profile and the
    ///     file; Enter = SAVE &amp; LEAVE; with nothing to save: no line, STAY | LEAVE, Enter = LEAVE goes at once (the proof holds every
    ///     leave);
    ///  13 the obstacle's, the flag's and an empty tank's words: none while the pointer is away (the hit obstacle's bar only), two lines while
    ///     it is on them;
    ///  14 SAMPLE FIGHT on free ground (never on units, the obstacle or the flag; spread on the grid), refused with its words at the most;
    ///  15 the unit cap: red to the cap, the next drop refused in words with ui_error, the counts plate "300/300", the field's total, LOAD
    ///     WITH UNITS of a file with more units stops at the cap and says how many were not placed;
    ///  16 the stats a unit runs: its name label "... · CUSTOM" / "... · ZODENGINE" and the hover card "Custom stats: every LASER here runs
    ///     them.", a unit the set does not list "OPENZ (NOT IN Z-WIKI)";
    ///  17 RESET STATS left of SAVE: asks first (Esc cancels, nothing changes), then every Custom value is OpenZ's, the field uses OpenZ and
    ///     the saved profile is gone; with nothing to reset it only says so;
    ///  18 the STATS box's line on which stats are used follows the set; the SHARE window says OpenZ sends nothing; the help card names the
    ///     level screen;
    ///  19 a Custom value typed for the jeep while OpenZ is in use: the field switches to Custom, every jeep on the field (red, blue and an
    ///     empty one) and one placed after take it at once, the toast says so; the squad size counts for squads placed after; the line by
    ///     the Custom column and the hover card name every jeep.
    /// At every picture size: the leave question in its two forms and the reset confirm, the obstacle's words, a unit's stats label, the line by the Custom
    /// column (pictures). The player's settings are put back (Restore), the saved Custom profile as before the run (PlainPutProfile).
    /// </summary>
    public static partial class OzUeProof
    {
        // ================================================================== the saved Custom profile before / after the run
        static OzUeProfile.Snap profile0;

        static void PlainKeepProfile()
        {
            profile0 = OzUeProfile.Take();
            OzUeLoadingPicture.ProofHoldNext = true;                                                           // the first visit's loading screen stays up for its picture (12)
        }

        static void PlainPutProfile()
        {
            OzUeLoadingPicture.ProofHoldNext = false;
            if (profile0 == null) return;
            OzUeProfile.Put(profile0);
            Info("the saved Custom profile is as before the run (" + (profile0.had ? "one was saved" : "none was saved") + ")");
            profile0 = null;
        }

        /// <summary>The folder a leave's SAVE &amp; LEAVE writes to when the run has none yet (the first visit).</summary>
        static string PlainLeaveFolder()
        {
            string p = System.IO.Path.Combine(dir ?? Application.temporaryCachePath, "files_leave");
            try { Directory.CreateDirectory(p); } catch (Exception e) { Info("the leave's folder could not be made: " + e.Message); }
            return p;
        }

        // ================================================================== 12 the loading picture (the first visit)
        static IEnumerator LoadingPicture()
        {
            int start = OpenZMainMenu.UnitEditorStarts;                                                        // the click came just before this step
            yield return WaitFor(() => OzUeLoadingPicture.AttachedFor == start, 2f);                           // put on in the click's own frame
            bool att = lastWait, held = OzUeLoadingPicture.HeldFor == start;
            yield return WaitFor(() => OzUeLoadingPicture.OnScreen(out _), 1.5f);                              // the loading screen's fade-in (0.3 s)
            bool on0 = OzUeLoadingPicture.OnScreen(out string state);
            yield return Shot("loading");
            bool on1 = OzUeLoadingPicture.OnScreen(out _);                                                     // still up once the picture is written: it is in the picture
            OzUeLoadingPicture.ProofHoldNext = false;
            var uv = OzUeLoadingPicture.LastUv; var sz = OzUeLoadingPicture.LastSize;
            float screen = (float)Screen.width / Mathf.Max(1, Screen.height), shown = sz.y > 0f && uv.height > 0f ? sz.x * uv.width / (sz.y * uv.height) : 0f;
            bool flat = att && Mathf.Abs(shown / screen - 1f) < 0.002f && uv.width <= 1.0001f && uv.height <= 1.0001f;
            Check(att && on0 && on1 && Mathf.Approximately(sz.x, 2560f) && Mathf.Approximately(sz.y, 1440f) && flat,
                  "12 the Unit editor's loading screen shows its picture (" + (att ? "on it" : "NOT on it") + "; " + state + "; still shown when the picture was written: " + on1
                  + " (the proof holds this loading screen " + F0(OzUeLoadingPicture.ProofHoldS) + " s: " + held + "); " + F0(sz.x) + " x " + F0(sz.y) + ", the part shown "
                  + shown.ToString("0.###", CultureInfo.InvariantCulture) + " wide for the screen's " + screen.ToString("0.###", CultureInfo.InvariantCulture) + ": cropped, never stretched: " + flat + ")");
        }

        // ================================================================== 11-19 at 1920 x 1080
        static IEnumerator PlainChecks()
        {
            var shell = OzUeShellRoot.I;
            if (!OzUnitEditor.UiBuilt || !shell || shell.Ask == null) { Check(false, "11 the leave question is built"); yield break; }
            var ask = shell.Ask;
            yield return Tidy();
            OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField(); yield return 2;

            // ---------------------------------------------------------------- 11 Esc and the leave question
            OzUeControls.SetSpeed(2f); OzUeControls.SetPlaying(true); yield return 3;
            int opens = OzUeLeave.LeaveOpens;
            yield return Tap(Key.Escape);
            bool open1 = ask.LeaveOpen, paused1 = !OzUeControls.Playing;
            yield return Tap(Key.Escape);
            bool closed1 = !ask.Open, running1 = OzUeControls.Playing, speed1 = Mathf.Abs(OzUeControls.Speed - 2f) < 0.01f;
            Check(open1 && paused1 && closed1 && running1 && speed1 && OzUeLeave.LeaveOpens == opens + 1,
                  "11 Esc with nothing open asks " + OzUePlan.LeaveTitle + " (" + (open1 ? "shown" : "NOT shown") + ") and the field stands still while it shows (" + paused1
                  + "); Esc again = STAY: closed (" + closed1 + "), the fight runs again (" + running1 + ") at x2 as before (" + speed1 + ")");
            OzUeControls.SetPlaying(false); yield return 3;
            yield return Tap(Key.Escape);
            bool open2 = ask.LeaveOpen;
            yield return Click(Centre(ask.stay.Rt)); yield return 2;
            bool closed2 = !ask.Open, still2 = !OzUeControls.Playing;
            Check(open2 && closed2 && still2, "11 with the field paused: Esc asks (" + open2 + "), a click on STAY closes it (" + closed2 + ") and the field stays paused (" + still2 + ")");
            OzUeControls.SetPlaying(true); OzUeControls.SetSpeed(1f); yield return 3;
            OzUeStats.SetTable(true); OzUeStats.OpenCard("grunt", OzUeKeys.ShownBlue); shell.Help.Show(shell.Top.HelpRect, OzUnitEditor.CanvasSize); yield return 3;
            bool all3 = shell.Help.Open && OzUeStats.CardOpen && OzUeStats.TableOpen, order = all3;
            yield return Tap(Key.Escape); order &= !shell.Help.Open && OzUeStats.CardOpen && OzUeStats.TableOpen && !ask.Open;
            yield return Tap(Key.Escape); order &= !OzUeStats.CardOpen && OzUeStats.TableOpen && !ask.Open;
            yield return Tap(Key.Escape); order &= !OzUeStats.TableOpen && !ask.Open;
            yield return Tap(Key.Escape); order &= ask.LeaveOpen;
            yield return Tap(Key.Escape); order &= !ask.Open && OzUeControls.Playing;
            Check(order, "11 Esc closes one thing at a time, the top one first: the help card, the unit card, the TABLE (all three open: " + all3 + "), then it asks " + OzUePlan.LeaveTitle + "; Esc in the question = STAY");

            // a known start: the Custom stats saved (the profile = them), then one grunt value changed and not saved
            var keep = OzUeSets.CustomToTree(); var set0x = OzUeSets.InUse;
            OzUeSets.Use(UeSet.Custom);
            bool savedFirst = OzUeFiles.Save(out _, out _) && OzUeProfile.Saved && OzUeSets.UnsavedCount == 0;
            double hpSaved = OzUeSets.CustomValue("grunt", UeStat.Health);
            OzUeSets.SetCustom("grunt", UeStat.Health, hpSaved + 7); yield return 2;
            yield return Click(Centre(shell.Top.menu.Rt)); yield return 0.3f;
            bool same = ask.LeaveOpen && ask.Unsaved;
            string status = StripTags(OzUeLeave.LastStatus ?? "");
            string words = PlainPlate(ask.LeaveRoot, out bool fit);
            string three = PlainButtons(ask.LeaveRoot);
            Check(same && fit && savedFirst && status == OzUePlan.NotSaved && three == OzUePlan.Stay + " | " + OzUePlan.NoSave + " | " + OzUePlan.SaveAndGo,
                  "11 a real click on MAIN MENU asks the same question; with one grunt value changed after a SAVE its one line says \"" + status + "\" and it offers " + three + "; " + words);
            bool main = ask.saveGo.Style == HudButtonStyle.Primary && PlainCap(ask.saveGo) == OzUePlan.KeyEnter && PlainCap(ask.stay) == OzUePlan.KeyEsc && PlainCap(ask.noSave) == null
                        && ask.noSave.Style == HudButtonStyle.Normal;
            bool noReset = !PlainHasText(ask.LeaveRoot, "RESET") && !PlainHasText(ask.LeaveRoot, "DEFAULT");
            Check(main && noReset, "11 " + OzUePlan.SaveAndGo + " is the main button (highlighted, " + PlainCap(ask.saveGo) + " on its cap), " + OzUePlan.Stay + " has " + PlainCap(ask.stay) + " (" + main
                  + "); no reset in the question: RESET STATS stays in the top bar (" + noReset + ")");
            OzUeLeave.ProofHold = true;
            int held = OzUeLeave.Held, drops = OzUeLeave.DropsDone, lo0 = OzUeLeave.LeaveOpens;
            yield return Click(Centre(ask.noSave.Rt)); yield return 3;
            bool dropped = OzUeLeave.DropsDone == drops + 1 && OzUeLeave.Held == held + 1 && !ask.Open && OzUnitEditor.Active && OzUeLeave.LeaveOpens == lo0
                           && Math.Abs(OzUeSets.CustomValue("grunt", UeStat.Health) - hpSaved) < 0.5 && OzUeSets.UnsavedCount == 0;
            Check(dropped, "11 " + OzUePlan.NoSave + " with a change not saved goes at once, no second question: the change is dropped (the grunt's Custom health is the saved "
                  + OzUeLiveStats.Format(UeStat.Health, hpSaved) + " again, nothing unsaved) and it goes (held by the proof: " + dropped + ")");
            var f = Focus();
            OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-10f, 0f, 0f), 1); OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(10f, 0f, 0f), 1); yield return 2;
            OzUeSets.SetCustom("light", UeStat.Damage, OzUeSets.CustomValue("light", UeStat.Damage) + 11); yield return 2;
            double dmg = OzUeSets.CustomValue("light", UeStat.Damage);
            int saves = OzUeFiles.Saves, pw = OzUeProfile.Writes; held = OzUeLeave.Held;
            yield return Click(Centre(shell.Top.menu.Rt)); yield return 0.3f;
            yield return Click(Centre(ask.saveGo.Rt)); yield return 3;
            var pv = OzUeProfile.Values();
            bool inProfile = pv.TryGetValue("light", out var lv) && Math.Abs(lv[(int)UeStat.Damage] - dmg) < 1e-9;
            bool saved = OzUeFiles.Saves == saves + 1 && OzUeProfile.Writes == pw + 1 && File.Exists(OzUeFiles.FilePath) && inProfile && OzUeSets.UnsavedCount == 0;
            bool held1 = OzUeLeave.Held == held + 1 && OzUnitEditor.Active && !ask.Open;
            int onField = saved ? OzUeFiles.Info(OzUeFiles.FilePath).onField : 0;
            Check(saved && held1 && onField >= 2, "11 " + OzUePlan.SaveAndGo + " keeps the Custom stats as the saved profile the levels read (" + OzUeProfile.KeyValues + ": the light tank's Custom damage "
                  + OzUeLiveStats.Format(UeStat.Damage, dmg) + " in it: " + inProfile + "), writes " + OzUeFiles.FileName + " (" + onField + " on its field) and then goes (held by the proof: " + held1 + ")");
            OzUeSets.SetCustom("light", UeStat.Damage, dmg + 1); yield return 2;
            yield return Click(Centre(shell.Top.menu.Rt)); yield return 2;
            saves = OzUeFiles.Saves;
            yield return Tap(Key.Enter); yield return 2;
            bool went = OzUeLeave.Held == held + 2 && !ask.Open && OzUeLeave.LastChoice == OzUePlan.SaveAndGo && OzUeFiles.Saves == saves + 1 && OzUnitEditor.Active && OzUeSets.UnsavedCount == 0;
            Check(went, "11 Enter in the question with a change not saved = " + OzUePlan.SaveAndGo + " (held by the proof: " + went + ", the last choice " + OzUeLeave.LastChoice + ")");
            int goes = OzUeLeave.Goes;
            yield return Click(Centre(shell.Top.menu.Rt)); yield return 0.3f;
            bool savedForm = ask.LeaveOpen && !ask.Unsaved && PlainButtons(ask.LeaveRoot) == OzUePlan.Stay + " | " + OzUePlan.NoSave && ask.noSave.Style == HudButtonStyle.Primary
                             && PlainCap(ask.noSave) == OzUePlan.KeyEnter && (OzUeLeave.LastStatus ?? "") == "";
            string savedWords = PlainPlate(ask.LeaveRoot, out bool sfit);
            saves = OzUeFiles.Saves;
            yield return Tap(Key.Enter); yield return 2;
            bool straight = OzUeLeave.Goes == goes + 1 && !ask.Open && OzUeLeave.Held == held + 3 && OzUeFiles.Saves == saves && OzUeLeave.LastChoice == OzUePlan.NoSave;
            Check(savedForm && sfit && straight, "11 with nothing to save the question has no line and offers " + OzUePlan.Stay + " | " + OzUePlan.NoSave + " (the main button, ENTER: " + savedForm + "; " + savedWords
                  + "); Enter = " + OzUePlan.NoSave + " goes at once, nothing saved (held by the proof: " + straight + ")");
            OzUeLeave.ProofHold = false;

            // ---------------------------------------------------------------- 13 the words over the obstacle, the flag, an empty tank
            OzUePlayground.ClearField(); OzUeControls.SetPlaying(false); yield return 2;
            // on the free field, under no panel: the field takes no hover through a panel (the pointer there is on the panel)
            var ob = Leader(OzUePlayground.Drop(OzUePlayground.Obstacle, Team.Neutral, PlainFree(0.5f, 0.66f, f + new Vector3(0f, 0f, 10f)), 1));
            var fl = Leader(OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, PlainFree(0.28f, 0.4f, f + new Vector3(-16f, 0f, -6f)), 1));
            Vector3 emAt = PlainFree(0.72f, 0.4f, f + new Vector3(16f, 0f, -6f));
            var em = Leader(OzUePlayground.Drop("light", Team.Neutral, emAt, 1));
            if (ob != null) ob.Health = Mathf.Min(ob.MaxHealth, 5405);                                           // as the picture: hit
            yield return Park(); yield return 4;
            bool quiet = ob != null && OzUeMarks.ObstacleWords == 0 && OzUeMarks.FlagWords == 0 && OzUeMarks.EmptyWords == 0;
            yield return Shot("words_quiet");
            bool two = false, flag2 = false, empty2 = false;
            if (ob != null) { Move(ToScreen(Pos(ob) + Vector3.up * 1.5f)); yield return 5; two = OzUeMarks.ObstacleWords == 2 && OzUeMarks.LastObstacleTop == "OBSTACLE  " + ob.Health + " / " + ob.MaxHealth + " HP"; yield return Shot("words_obstacle"); }
            if (fl != null) { Move(ToScreen(Pos(fl) + Vector3.up * 0.6f)); yield return 5; flag2 = OzUeMarks.FlagWords == 2 && (OzUeMarks.LastFlagTop ?? "").StartsWith("FLAG  ", StringComparison.Ordinal); }
            if (em != null) { Move(UnitScreen(em)); yield return 5; empty2 = OzUeMarks.EmptyWords == 2; yield return Shot("words_empty"); } // where the game picks it (its drawn model)
            Check(quiet && two && flag2 && empty2, "13 the words in two lines only while the pointer is on the object: a hit obstacle with the pointer away shows its bar only, no words (" + quiet
                  + "); on it \"" + OzUeMarks.LastObstacleTop + "\" over \"" + OzUePlan.ObstacleHint + "\" (" + two + "); the flag \"" + OzUeMarks.LastFlagTop + "\" over \"" + OzUePlan.FlagHint
                  + "\" (" + flag2 + "); an empty light tank \"" + (OzUeMarks.LastEmptyTop ?? "") + "\" over \"" + OzUePlan.EmptyHint + "\" (" + empty2 + ")");
            if (em != null) OzUePlayground.Remove(em);
            var hud = OzUnitEditor.I ? OzUnitEditor.I.Hud : null;
            var hw = new List<string>(OzUePlayground.Vehicles); hw.AddRange(OzUePlayground.Guns);
            int hwOk = 0; var hwBad = new StringBuilder();
            foreach (var id in hw)
            {
                var o = Leader(OzUePlayground.Drop(id, Team.Neutral, emAt, 1)); yield return 3;                // one at a time on the tank's spot
                bool ok = false, hwOnField = false;
                if (o != null)
                {
                    OzUeMarks.LastEmptyTop = null;
                    Move(UnitScreen(o)); yield return 5;
                    hwOnField = hud && !hud.PointerOverUi;
                    ok = hwOnField && OzUeMarks.EmptyWords == 2 && OzUeMarks.LastEmptyTop == "EMPTY " + OzUePlayground.FullName(id) + "  " + Mathf.Max(0, o.Health) + " / " + o.MaxHealth + " HP";
                    OzUePlayground.Remove(o); yield return 2;
                }
                if (ok) hwOk++;
                else hwBad.Append(hwBad.Length > 0 ? ", " : "").Append(id).Append(o == null ? " (not placed)" : !hwOnField ? " (the pointer on a panel)" : " (\"" + (OzUeMarks.LastEmptyTop ?? "no words") + "\")");
            }
            Check(hwOk == hw.Count, "13 every empty vehicle and gun shows its two lines while the pointer is on it (" + hwOk + " of " + hw.Count + "): \"EMPTY <NAME>  <hp> / <max> HP\" over \""
                  + OzUePlan.EmptyHint + "\"" + (hwBad.Length > 0 ? "; not: " + hwBad : ""));

            // ---------------------------------------------------------------- 14 SAMPLE FIGHT on free ground, at most a few at once
            OzUePlayground.ClearField(); yield return 2;
            OzUePlayground.Drop(OzUePlayground.Obstacle, Team.Neutral, f, 1); OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, f + new Vector3(0f, 0f, -10f), 1); yield return 2;
            var centres = new List<Vector3>(); int fights = 0, refusedAt = -1; string refusal = null; bool soundOk = true, apart = true, offObjects = true;
            for (int k = 0; k < OzUeCap.SampleFightsMax + 2; k++)
            {
                float t0 = Time.unscaledTime;
                int made = OzUePlayground.SampleFight(); yield return 2;
                if (made <= 0) { refusedAt = k; refusal = OzUePlayground.LastSampleWhy; soundOk &= ErrorSince(t0); break; }
                fights++;
                var c = OzUePlayground.LastSampleCentre;
                foreach (var o in centres) apart &= (o - c).magnitude >= OzUePlan.SampleStep * 0.9f;
                centres.Add(c);
            }
            foreach (var o in OzUePlayground.Placed())
            {
                if (!o.IsUnit) continue;
                foreach (var x in OzUePlayground.Placed())
                    if ((OzUePlayground.IsObstacle(x) || OzUePlayground.IsFlag(x)) && (Pos(o) - Pos(x)).magnitude < (OzUePlayground.IsObstacle(x) ? 4.2f : 1.4f)) offObjects = false;
            }
            var rts0 = OzUnitEditor.I.Rts; Vector3 focus0 = rts0 ? rts0.focus : f; float h0 = rts0 ? rts0.height : 70f;
            OzUeControls.Fit(); yield return 1.2f;                                                           // Home: the whole field in view for the picture
            yield return Shot("sample_fights");
            if (rts0) { rts0.focus = focus0; rts0.SetHeightImmediate(h0); }                                   // the view as it was
            yield return 0.5f;
            string want = OzUeCap.SampleFightsMax + " SAMPLE FIGHTS ARE ON THE FIELD";
            Check(fights == OzUeCap.SampleFightsMax && refusedAt == fights && refusal != null && refusal.StartsWith(want, StringComparison.Ordinal) && soundOk && apart && offObjects,
                  "14 SAMPLE FIGHT on free ground: " + fights + " fights, each on its own ground (the centres a grid step apart: " + apart + "), none on the obstacle or the flag (" + offObjects
                  + "); the next click refused (\"" + refusal + "\", ui_error: " + soundOk + ")");
            OzUePlayground.ClearField(); yield return 2;
            int again = OzUePlayground.SampleFight(); yield return 2;
            Check(again > 0 && OzUePlayground.SampleFightsOnField == 1, "14 CLEAR FIELD frees the fights: SAMPLE FIGHT places one again (" + again + " groups)");

            // ---------------------------------------------------------------- 15 the unit cap
            OzUePlayground.ClearField(); OzUeControls.SetPlaying(false); OzUeSets.Use(UeSet.OpenZ); yield return 2;    // OpenZ's squads (pyro 4): 75 squads = the cap exactly
            yield return PlainFill(OzUeKeys.ShownRed, f, -1f);
            int red = OzUePlayground.CountUnits(OzUeKeys.ShownRed);
            float te = Time.unscaledTime;
            var refused = OzUePlayground.Drop("pyro", OzUeKeys.ShownRed, f + new Vector3(0f, 0f, 30f), 5); yield return 2;
            bool words1 = !refused.ok && refused.why == OzUeCap.FullSide(OzUeKeys.ShownRed) && ErrorSince(te) && OzUePlayground.CountUnits(OzUeKeys.ShownRed) == red;
            yield return 0.7f;                                                                                // the counts plate counts twice a second
            string plate = PlainCounts();
            yield return Shot("cap_full");
            Check(red == OzUeCap.PerSide && words1 && plate.Contains(OzUeCap.PerSide + "/" + OzUeCap.PerSide),
                  "15 " + OzUeUi.TeamWord(OzUeKeys.ShownRed) + " filled to " + red + " units (the cap " + OzUeCap.PerSide + "); the next drop places nothing and says \"" + refused.why + "\" with ui_error ("
                  + words1 + "); the counts plate shows \"" + plate + "\"");
            yield return PlainFill(OzUeKeys.ShownBlue, f, 1f);
            te = Time.unscaledTime;
            var jeep = OzUePlayground.Drop("jeep", Team.Neutral, f + new Vector3(0f, 0f, -30f), 1); yield return 2;
            bool total = OzUeCap.All(true) == OzUeCap.Total && !jeep.ok && jeep.why == OzUeCap.FullAll && ErrorSince(te);
            Check(total, "15 both sides full: " + OzUeCap.All(true) + " units in all; an empty jeep is refused too (\"" + jeep.why + "\")");
            string capFile = null; int placed = 0; string loadMsg = null;
            if (OzUeFiles.Save(out string sp, out _))
            {
                try
                {
                    if (OzUeJson.TryParse(File.ReadAllText(sp), out object tree, out _) && OzUeJson.Get(OzUeJson.Get(tree, "field"), "items") is List<object> items)
                    {
                        items.AddRange(new List<object>(items));                                             // twice the units the cap allows
                        capFile = Path.Combine(Path.GetDirectoryName(sp), "cap_test.json");
                        File.WriteAllText(capFile, OzUeJson.Write(tree, true));
                    }
                }
                catch (Exception e) { Info("the cap test file could not be made: " + e.Message); }
            }
            if (capFile != null) { OzUeFiles.Load(capFile, true, out loadMsg); yield return 3; placed = OzUeCap.All(true); }
            Check(capFile != null && placed <= OzUeCap.Total && OzUePlayground.CountUnits(Team.Red) <= OzUeCap.PerSide && OzUePlayground.CountUnits(Team.Blue) <= OzUeCap.PerSide
                  && loadMsg != null && loadMsg.Contains("NOT PLACED"), "15 LOAD WITH UNITS of a file with twice the units stops at the cap (" + placed + " units on the field) and says so: \"" + loadMsg + "\"");
            OzUePlayground.ClearField(); yield return 3;

            // ---------------------------------------------------------------- 16 which stats a unit runs: its label, the hover card
            var laser = Leader(OzUePlayground.Drop("laser", OzUeKeys.ShownRed, f + new Vector3(0f, 0f, 4f), 1));
            var electro = Leader(OzUePlayground.Drop(ZElectro.Type, OzUeKeys.ShownRed, f + new Vector3(12f, 0f, 4f), 1));
            OzUeSets.Use(UeSet.Custom); yield return 3;
            string l1 = null, card1 = null, l2 = null, l3 = null;
            if (laser != null)
            {
                OzUePick.Pick(new List<ZObject> { laser }, false); yield return Park(); yield return 4;
                l1 = OzUeMarks.LastUnitLabel;
                Move(UnitScreen(laser)); yield return 0.5f;
                card1 = PlainCardFoot();
                yield return Shot("unit_stats");
                OzUeSets.Use(UeSet.ZodEngine); yield return Park(); yield return 4;
                l2 = OzUeMarks.LastUnitLabel;
            }
            if (electro != null) { OzUeSets.Use(UeSet.ZWiki); OzUePick.Pick(new List<ZObject> { electro }, false); yield return Park(); yield return 4; l3 = OzUeMarks.LastUnitLabel; }
            if (electro == null) Info("16 no Electrobot on the field (the game plays the original Z rules?): the not-listed unit's words were not read");
            bool tags = Has(l1, "·  CUSTOM") && Has(card1, "Custom stats: every LASER here runs them.") && Has(l2, "·  ZODENGINE") && (electro == null || Has(l3, "OPENZ (NOT IN Z-WIKI)"));
            Check(tags, "16 the stats a unit runs: the picked laser's label \"" + StripTags(l1 ?? "") + "\", its hover card \"" + StripTags(card1 ?? "") + "\"; with ZodEngine \"" + StripTags(l2 ?? "")
                        + "\"; an Electrobot under Z-Wiki \"" + StripTags(l3 ?? "") + "\"");
            OzUePick.Unpick(); OzUePlayground.ClearField(); OzUeSets.Use(UeSet.OpenZ); yield return 2;

            // ---------------------------------------------------------------- 17 RESET STATS
            var rr = CanvasRect(shell.Top.reset.Rt); var sv = CanvasRect(shell.Top.save.Rt);
            OzUeSets.Use(UeSet.Custom);
            double hp0 = OzUeSets.CustomValue("grunt", UeStat.Health);
            OzUeSets.SetCustom("grunt", UeStat.Health, hp0 * 1.5); OzUeSets.SetCustom("light", UeStat.Damage, OzUeSets.CustomValue("light", UeStat.Damage) * 0.8);
            OzUeFiles.Save(out _, out _);                                                                      // a saved profile for the reset to remove
            yield return 1.4f;                                                                                // the working copy's autosave (a second after the change)
            string prefs = PlayerPrefs.GetString(OzUeSets.KeyCustom, ""), want1 = "grunt." + OzUeLiveStats.Def(UeStat.Health).key + "=" + OzUeSets.CustomValue("grunt", UeStat.Health).ToString("R", CultureInfo.InvariantCulture);
            bool kept = prefs.Contains(want1) && OzUeSets.CustomDiffFromOpenZ >= 2 && OzUeProfile.Saved;
            yield return Click(Centre(shell.Top.reset.Rt)); yield return 2;
            bool asks = ask.ResetOpen && !ask.LeaveOpen;
            yield return Tap(Key.Escape); yield return 2;
            bool unchanged = !ask.Open && OzUeSets.CustomDiffFromOpenZ >= 2 && OzUeSets.InUse == UeSet.Custom && OzUeProfile.Saved;
            Check(rr.xMax + OzUePlan.ResetGap - sv.x < 1f && rr.xMax < sv.x && kept && asks && unchanged, "17 RESET STATS stands left of SAVE in the top bar (" + R(rr) + ", SAVE " + R(sv) + "); the changed Custom values are in the working copy and a profile is saved ("
                  + kept + "); a click asks " + OzUePlan.ResetTitle + " (" + asks + "); Esc cancels and nothing changes (" + unchanged + ")");
            yield return Click(Centre(shell.Top.reset.Rt)); yield return 0.3f;
            yield return Shot("reset_stats_confirm");
            int done = OzUeLeave.ResetsDone; float tr = Time.unscaledTime;
            yield return Click(Centre(ask.resetNow.Rt)); yield return 3;
            bool reset = OzUeLeave.ResetsDone == done + 1 && OzUeSets.CustomDiffFromOpenZ == 0 && OzUeSets.InUse == UeSet.OpenZ && !OzUeProfile.Saved && !ask.Open && UiSoundSince(tr);
            tr = Time.unscaledTime;
            yield return Click(Centre(shell.Top.reset.Rt)); yield return 3;
            bool nothing = !ask.Open && OzUeToast.Last == OzUePlan.NothingToReset && UiSoundSince(tr);
            OzUePick.InspectType("grunt", OzUeKeys.ShownBlue); yield return 3;
            string resetWord = PlainInspectorReset();
            Check(reset && nothing && (resetWord == "Reset unit" || resetWord == "Reset"), "17 RESET STATS in the confirm: every Custom value is OpenZ's again, the field uses OpenZ and the saved profile is gone (" + reset
                  + "); clicked again with nothing to reset it only says \"" + OzUeToast.Last + "\" (" + nothing + "); the stats box's own button reads \"" + resetWord + "\" (its tip names RESET STATS)");
            OzUePick.ClearInspect(); yield return 2;

            // ---------------------------------------------------------------- 18 which stats the levels use
            var box = OzUeStatsRoot.I != null && OzUeStatsRoot.I.Shell != null ? OzUeStatsRoot.I.Shell.Box : null;
            var scope = box ? box.Find("Scope") : null; var tm = scope ? scope.GetComponent<TextMeshProUGUI>() : null;
            OzUeSets.Use(UeSet.ZodEngine); yield return 0.4f;
            string s1 = tm ? StripTags(tm.text) : null;
            OzUeSets.Use(UeSet.OpenZ); yield return 0.4f;
            string s2 = tm ? StripTags(tm.text) : null;
            bool cut = tm && (tm.isTextTruncated || tm.preferredWidth > tm.rectTransform.rect.width + 1.5f);
            shell.Dialogs.OpenShare(OzUnitEditor.CanvasSize); yield return 0.3f;
            bool share = PlainHasText(shell.Dialogs.ShareRoot, "OpenZ sends nothing") && PlainHasText(shell.Dialogs.ShareRoot, "friends");
            shell.Dialogs.Close(false);
            shell.Help.Show(shell.Top.HelpRect, OzUnitEditor.CanvasSize); yield return 0.3f;
            bool help = PlainHasText(shell.Help.Root, OzUePlan.HelpStats) && PlainHasText(shell.Help.Root, OzUePlan.HelpEsc);
            yield return Shot("help_stats");
            shell.Help.Close(false); yield return 2;
            Check(Has(s1, "ZodEngine") && Has(s1, "Levels") && Has(s2, "OpenZ") && !cut && share && help, "18 which stats are used: the STATS box's line \"" + s1 + "\" (ZodEngine in use), \"" + s2
                  + "\" (OpenZ), not cut (" + !cut + "); the SHARE window says OpenZ sends nothing and that you send it to friends yourself (" + share + "); the help card's line under the keys and its Esc row (" + help + ")");

            // ---------------------------------------------------------------- 19 a Custom change reaches every unit of the type at once
            OzUePlayground.ClearField(); OzUeControls.SetPlaying(false); OzUeSets.Use(UeSet.OpenZ); yield return 2;
            var jr = Leader(OzUePlayground.Drop("jeep", Team.Red, f + new Vector3(-14f, 0f, 0f), 1));
            var jb = Leader(OzUePlayground.Drop("jeep", Team.Blue, f + new Vector3(14f, 0f, 0f), 1));
            var jn = Leader(OzUePlayground.Drop("jeep", Team.Neutral, f + new Vector3(0f, 0f, 12f), 1));
            OzUePick.InspectType("jeep", OzUeKeys.ShownBlue); yield return 4;
            int sw0 = OzUeSets.SwitchesToCustom;
            var hb = CustomBox("Health"); bool typing = false;
            if (hb)
            {
                yield return Click(Centre(hb)); yield return 3;
                typing = OzUeHooks.Typing;
                foreach (var ch in "2000") { InputSystem.QueueTextEvent(Keyboard.current, ch); yield return 1; }
                yield return Tap(Key.Enter); yield return 4;
            }
            else Info("19 the inspector's Custom Health box of the jeep was not found");
            string toast = OzUeToast.Last;
            yield return Shot("custom_edit");
            var jl = Leader(OzUePlayground.Drop("jeep", Team.Red, f + new Vector3(0f, 0f, -12f), 1)); yield return 3;   // one placed after the change
            bool every = jr != null && jb != null && jn != null && jl != null && jr.MaxHealth == 2000 && jb.MaxHealth == 2000 && jn.MaxHealth == 2000 && jl.MaxHealth == 2000;
            bool switched = OzUeSets.InUse == UeSet.Custom && OzUeSets.SwitchesToCustom == sw0 + 1;
            string wantToast = "EVERY JEEP, BOTH TEAMS: CUSTOM HEALTH 2000 HP" + OzUePlan.FieldNowCustom.ToUpperInvariant();
            Check(typing && every && switched && toast == wantToast, "19 2000 typed into the jeep's Custom Health while OpenZ was in use (the box took the keyboard: " + typing + "): the field switched to Custom ("
                  + switched + "); the red, the blue and the empty jeep on the field and a red one placed after have " + (jr != null ? jr.MaxHealth : -1) + " / " + (jb != null ? jb.MaxHealth : -1) + " / "
                  + (jn != null ? jn.MaxHealth : -1) + " / " + (jl != null ? jl.MaxHealth : -1) + " HP (" + every + "); the toast \"" + toast + "\"");
            string note = StripTags(OzUeInspector.LastNote ?? ""); bool noteCut = true, noteOver = true;
            var insp = box ? PlainFind(box, "Inspector") : null;
            var noteRt = insp ? PlainFind(insp, "CustomNote") : null; var footRt = insp ? PlainFind(insp, "Foot") : null;
            if (noteRt) { var nt = noteRt.GetComponent<TextMeshProUGUI>(); noteCut = nt == null || nt.isTextTruncated; var nr = CanvasRect(noteRt); noteOver = footRt && nr.y + (nt ? nt.preferredHeight : nr.height) > CanvasRect(footRt).y + 0.5f; }
            int sq0 = OzUePlayground.SquadSize("grunt");
            var g0 = Leader(OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(-8f, 0f, -20f), 1)); yield return 2;
            OzUeSets.SetCustom("grunt", UeStat.Squad, sq0 + 1); yield return 2;
            var g1 = Leader(OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(8f, 0f, -20f), 1)); yield return 2;
            int n0 = g0 != null ? 1 + g0.Minions.Count : -1, n1 = g1 != null ? 1 + g1.Minions.Count : -1;
            string sqToast = OzUeSets.EditToast("grunt", UeStat.Squad, false);
            OzUePick.Unpick(); OzUePick.ClearInspect(); yield return 2;
            ZObject jh = jb; string foot = null;
            if (jh != null) { yield return Park(); Move(UnitScreen(jh)); yield return 0.5f; foot = PlainCardFoot(); }
            Check(n0 == sq0 && n1 == sq0 + 1 && sqToast.StartsWith("NEW GRUNT SQUADS, BOTH TEAMS: " + (sq0 + 1) + " ROBOTS", StringComparison.Ordinal) && OzUeInspector.NoteLines <= 2 && note.Contains("every JEEP here, both teams")
                  && !noteCut && !noteOver && Has(foot, "every JEEP here runs them"),
                  "19 the squad size counts for squads placed after it (a grunt squad of " + n0 + " placed before stays, one placed after has " + n1 + "; \"" + sqToast + "\"); the line by the Custom column \""
                  + note + "\" (" + OzUeInspector.NoteLines + " lines, not cut: " + !noteCut + ", over the foot: " + !noteOver + "); the jeep's hover card \"" + StripTags(foot ?? "") + "\"");
            OzUePlayground.ClearField();
            if (keep != null) OzUeSets.CustomFromTree(keep, out _);
            OzUeSets.Use(set0x); yield return 2;
            OzUeLeave.ProofHold = false;
            Restore(); yield return 3;
        }

        // ================================================================== the pictures at every size
        static IEnumerator PlainPictures()
        {
            var shell = OzUeShellRoot.I; if (!shell || shell.Ask == null) yield break;
            var ask = shell.Ask; var f = Focus();
            var keep = OzUeSets.CustomToTree(); var set0x = OzUeSets.InUse;
            OzUePlayground.ClearField(); OzUeControls.SetPlaying(true); yield return 2;
            OzUePlayground.SampleFight(); yield return 1.0f;
            yield return Park();
            OzUeSets.SetCustom("grunt", UeStat.Health, OzUeSets.CustomValue("grunt", UeStat.Health) + 3); yield return 2;    // a change not saved: the question's line and SAVE & LEAVE
            yield return Click(Centre(shell.Top.menu.Rt)); yield return 0.4f;
            bool shown = ask.LeaveOpen && ask.Unsaved, paused = !OzUeControls.Playing;
            string words = PlainPlate(ask.LeaveRoot, out bool fit);
            yield return Shot("leave_question");
            yield return Tap(Key.Escape); yield return 2;
            bool back = !ask.Open && OzUeControls.Playing;
            yield return Click(Centre(shell.Top.reset.Rt)); yield return 0.4f;
            bool conf = ask.ResetOpen;
            string cw = PlainPlate(ask.ResetRoot, out bool cfit);
            yield return Shot("reset_confirm");
            yield return Tap(Key.Escape); yield return 2;
            bool cancelled = !ask.Open && OzUeSets.UnsavedCount > 0;
            OzUeSets.DropUnsaved(); yield return 2;                                                             // nothing to save: the question's other form
            yield return Click(Centre(shell.Top.menu.Rt)); yield return 0.4f;
            bool other = ask.LeaveOpen && !ask.Unsaved;
            string ow = PlainPlate(ask.LeaveRoot, out bool ofit);
            yield return Shot("leave_question_saved");
            yield return Tap(Key.Escape); yield return 2;
            Check(shown && paused && fit && back && conf && cfit && cancelled && other && ofit && !ask.Open && OzUeControls.Playing, "11 the leave question at this size (the field paused: " + paused + "): " + words
                  + "; Esc: back to the running fight (" + back + "); the RESET STATS confirm: " + cw + ", Esc cancels it (" + cancelled + "); with nothing to save: " + ow);
            OzUePlayground.ClearField(); OzUeControls.SetPlaying(false); yield return 2;
            var ob = Leader(OzUePlayground.Drop(OzUePlayground.Obstacle, Team.Neutral, f + new Vector3(0f, 0f, 8f), 1));
            var laser = Leader(OzUePlayground.Drop("laser", OzUeKeys.ShownRed, f + new Vector3(-14f, 0f, -4f), 1));
            if (ob != null) ob.Health = Mathf.Min(ob.MaxHealth, 5405);
            yield return Park(); yield return 4;
            bool quiet = OzUeMarks.ObstacleWords == 0;
            yield return Shot("obstacle_quiet");
            bool two = false;
            if (ob != null) { Move(ToScreen(Pos(ob) + Vector3.up * 1.5f)); yield return 5; two = OzUeMarks.ObstacleWords == 2; yield return Shot("obstacle_words"); }
            string tag = null;
            if (laser != null)
            {
                OzUeSets.Use(UeSet.Custom); OzUeMarks.LastUnitLabel = null;
                OzUePick.Pick(new List<ZObject> { laser }, false); yield return Park(); yield return 4;        // the pointer away: the picked laser's own label shows (a hovered unit shows the hover card instead)
                tag = OzUeMarks.LastUnitLabel;
                yield return Shot("unit_label");
                Move(UnitScreen(laser)); yield return 0.5f;
                yield return Shot("unit_stats");                                                               // the hover card's foot: the stats it runs
                OzUePick.Unpick();
            }
            Check(quiet && two && Has(tag, "·  CUSTOM"), "13 the obstacle's words at this size: none with the pointer away (" + quiet + "), two lines on it (" + two + "); 16 the picked laser's label \""
                  + StripTags(tag ?? "") + "\"");
            yield return 2;                                                                                    // the unpick above reaches the inspector first (OzUePick.Reconcile): else it shuts the jeep's stats again
            OzUePick.InspectType("jeep", OzUeKeys.ShownBlue); yield return 4;
            yield return Park();
            bool jeepShown = OzUePick.Kind == UeInspect.Type && OzUePick.InspectedId == "jeep";
            var box = OzUeStatsRoot.I != null && OzUeStatsRoot.I.Shell != null ? OzUeStatsRoot.I.Shell.Box : null;
            var insp = box ? PlainFind(box, "Inspector") : null;
            var noteRt = insp ? PlainFind(insp, "CustomNote") : null; var footRt = insp ? PlainFind(insp, "Foot") : null;
            bool noteOk = false; string nt = StripTags(OzUeInspector.LastNote ?? ""); int tmpLines = -1;
            if (noteRt && footRt)
            {
                var tmn = noteRt.GetComponent<TextMeshProUGUI>(); var nr = CanvasRect(noteRt);
                if (tmn != null) { tmn.ForceMeshUpdate(); tmpLines = tmn.textInfo.lineCount; }                 // TextMeshPro's own wrap of the line
                noteOk = jeepShown && tmn != null && !tmn.isTextTruncated && tmpLines >= 1 && tmpLines <= 2 && nr.y + tmn.preferredHeight <= CanvasRect(footRt).y + 0.5f && OzUeInspector.NoteLines <= 2;
            }
            yield return Shot("inspector_note");
            Check(noteOk, "19 the line by the Custom column at this size (the jeep's stats shown: " + jeepShown + "): \"" + nt + "\" in " + OzUeInspector.NoteLines + " line(s) (TextMeshPro: "
                  + tmpLines + "), over the foot, not cut (" + noteOk + ")");
            OzUePick.ClearInspect();
            OzUePlayground.ClearField(); OzUeControls.SetPlaying(true);
            if (keep != null) OzUeSets.CustomFromTree(keep, out _);
            OzUeSets.Use(set0x); yield return 2;
        }

        // ================================================================== helpers
        /// <summary>One side filled to the cap with x5 pyro squads (4 robots each), one drop a frame, on the given half of the field.</summary>
        static IEnumerator PlainFill(Team team, Vector3 f, float side)
        {
            for (int k = 0; k < 40 && OzUePlayground.CountUnits(team) < OzUeCap.PerSide; k++)
            {
                var p = f + new Vector3(side * (18f + (k % 5) * 14f), 0f, -40f + (k / 5) * 11f);
                OzUePlayground.Drop("pyro", team, p, 5);
                yield return 1;
            }
            yield return 2;
        }

        /// <summary>A refusal's sound: ui_error in the sound log since t0.</summary>
        static bool ErrorSince(float t0)
        {
            foreach (var e in OzSoundLog.Since(t0)) if (e.key == "ui_error" && (e.outcome == null || !e.outcome.StartsWith("DROP", StringComparison.Ordinal))) return true;
            foreach (var e in OzSoundLog.Since(t0)) if (e.key == "ui_error") return true;                     // played in a frame that had a UI sound already
            return false;
        }

        /// <summary>The counts plate's words (UE_Counts), as shown.</summary>
        static string PlainCounts()
        {
            var plate = Named("UE_Counts", false); if (!plate) return "";
            var sb = new StringBuilder();
            foreach (var t in plate.GetComponentsInChildren<TextMeshProUGUI>(false)) sb.Append(StripTags(t.text)).Append(' ');
            return sb.ToString().Trim();
        }

        static string PlainCardFoot()
        {
            var card = Named("OzUeHoverCard", false); if (!card) return null;
            var foot = card.Find("Foot"); var t = foot ? foot.GetComponent<TextMeshProUGUI>() : null;
            return t ? t.text : null;
        }

        /// <summary>The ground point under a spot of the free field (fractions of the screen part no panel covers, from the bottom left),
        /// else the fallback.</summary>
        static Vector3 PlainFree(float fx, float fy, Vector3 fallback)
        {
            var ed = OzUnitEditor.I; var ff = OzUeInsets.FreeFieldScreen;
            if (!ed || ff.width < 4f || ff.height < 4f) return fallback;
            return OzUePlaceDriver.FloorPoint(ed, new Vector2(ff.x + ff.width * fx, ff.y + ff.height * fy), out var at) ? at : fallback;
        }

        /// <summary>The first active child of `root` with that name (any depth).</summary>
        static RectTransform PlainFind(RectTransform root, string name)
        {
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(false)) if (rt.name == name) return rt;
            return null;
        }

        static string PlainInspectorReset()
        {
            var box = OzUeStatsRoot.I != null && OzUeStatsRoot.I.Shell != null ? OzUeStatsRoot.I.Shell.Box : null;
            if (!box) return null;
            foreach (var rt in box.GetComponentsInChildren<RectTransform>(false))
                if (rt.name == "Reset" && rt.parent && rt.parent.name == "Foot") { var t = rt.GetComponentInChildren<TextMeshProUGUI>(); return t ? StripTags(t.text) : null; }
            return null;
        }

        static bool PlainHasText(RectTransform root, string part)
        {
            if (!root) return false;
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(false)) if (StripTags(t.text).IndexOf(StripTags(part), StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>A window's plate as shown (1.2 item 109: the game-style windows, UI/OzUeWindow.cs): its stencil, title and line, its
        /// buttons (one row inside the plate, none over another, the texts over the row, none cut), the plate inside the screen. The close
        /// box, LOAD's rows and its toggle are not row buttons. fit = all of it holds.</summary>
        static string PlainPlate(RectTransform plate, out bool fit)
        {
            fit = false;
            if (!plate || !plate.gameObject.activeInHierarchy) return "not shown";
            var pr = CanvasRect(plate); var cs = OzUnitEditor.CanvasSize;
            var btns = new List<(string label, Rect r)>(); var texts = new List<(string t, Rect r, TextMeshProUGUI tm)>();
            for (int i = 0; i < plate.childCount; i++)
            {
                var c = plate.GetChild(i) as RectTransform; if (!c || !c.gameObject.activeInHierarchy) continue;
                if (c.Find("Bg") && c.Find("Content"))
                {
                    if (!PlainRowButton(c)) continue;
                    var lt = c.Find("Content/Label"); var lm = lt ? lt.GetComponent<TextMeshProUGUI>() : null;
                    btns.Add((lm ? StripTags(lm.text) : c.name, CanvasRect(c))); continue;
                }
                var tm = c.GetComponent<TextMeshProUGUI>();
                if (tm && !string.IsNullOrEmpty(tm.text)) texts.Add((StripTags(tm.text), CanvasRect(c), tm));
            }
            bool ok = pr.x >= -0.5f && pr.y >= -0.5f && pr.xMax <= cs.x + 0.5f && pr.yMax <= cs.y + 0.5f && btns.Count >= 2;
            float rowY = btns.Count > 0 ? btns[0].r.y : 0f;
            for (int i = 0; i < btns.Count; i++)
            {
                var b = btns[i].r;
                ok &= b.x >= pr.x - 0.5f && b.xMax <= pr.xMax + 0.5f && b.yMax <= pr.yMax + 0.5f && Mathf.Abs(b.y - rowY) < 1f;
                for (int j = i + 1; j < btns.Count; j++) { var o = btns[j].r; ok &= Mathf.Min(b.xMax, o.xMax) - Mathf.Max(b.x, o.x) <= 0.5f; }
            }
            var sb = new StringBuilder();
            foreach (var t in texts)
            {
                if (t.r.y < rowY) ok &= t.r.y + t.tm.preferredHeight <= rowY + 0.5f;
                ok &= !t.tm.isTextTruncated;
                if (sb.Length < 300) sb.Append(sb.Length > 0 ? " / " : "").Append(t.t.Length > 52 ? t.t.Substring(0, 52) + "..." : t.t);
            }
            sb.Append("; buttons");
            foreach (var b in btns) sb.Append(' ').Append(b.label).Append(" |");
            sb.Append(" in one row inside the plate " + R(pr));
            fit = ok;
            return sb.ToString();
        }

        /// <summary>A button of a window's row (not the close box, a row of LOAD's list or its toggle).</summary>
        static bool PlainRowButton(RectTransform c) => c.name != "Close" && c.name != "WithUnits" && !c.name.StartsWith("Row", StringComparison.Ordinal);

        /// <summary>The labels of a window's row buttons as shown, left to right: "STAY | LEAVE | SAVE &amp; LEAVE".</summary>
        static string PlainButtons(RectTransform plate)
        {
            if (!plate || !plate.gameObject.activeInHierarchy) return "";
            var bs = new List<(float x, string label)>();
            for (int i = 0; i < plate.childCount; i++)
            {
                var c = plate.GetChild(i) as RectTransform; if (!c || !c.gameObject.activeInHierarchy || !c.Find("Bg") || !c.Find("Content") || !PlainRowButton(c)) continue;
                var lt = c.Find("Content/Label"); var lm = lt ? lt.GetComponent<TextMeshProUGUI>() : null;
                bs.Add((CanvasRect(c).x, lm ? StripTags(lm.text) : c.name));
            }
            bs.Sort((a, b) => a.x.CompareTo(b.x));
            var sb = new StringBuilder();
            foreach (var b in bs) sb.Append(sb.Length > 0 ? " | " : "").Append(b.label);
            return sb.ToString();
        }

        /// <summary>The key on a button's cap as shown (null when it has none showing).</summary>
        static string PlainCap(HudButton b)
        {
            var k = b != null && b.Content ? b.Content.Find("Key") : null;
            if (!k || !k.gameObject.activeInHierarchy) return null;
            var t = k.GetComponentInChildren<TextMeshProUGUI>();
            return t ? StripTags(t.text) : null;
        }

        static bool Has(string s, string part) => s != null && StripTags(s).IndexOf(part, StringComparison.Ordinal) >= 0;
    }
}
