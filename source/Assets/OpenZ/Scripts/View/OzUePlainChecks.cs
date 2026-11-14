using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs SelfCheck, menu OpenZ/V1.2/Unit Editor/2 Counted Check and the Play proof): the counted lines of the
    /// test round's changes, on paper: the unit cap and its words, the top bar with RESET STATS, the editor's windows (1.2 item 109: the
    /// leave question in its two forms, the reset confirm, LOAD and SHARE) at every design size x UI SCALE step (with TextMeshPro's
    /// widths), the two-line words over the obstacle, the flag and empty
    /// hardware, the stats box's line on which stats are used, the inspector's line by the Custom column, the toasts of a Custom change,
    /// the saved Custom profile's format (the one the levels read), the loading picture, SAMPLE FIGHT's spots, and the words that say which
    /// stats the levels use. Twelve lines. Nothing here writes the game's settings store.
    /// </summary>
    internal static class OzUePlainChecks
    {
        internal static int SelfCheck(StringBuilder log)
        {
            int f = 0;
            GameObject tmp = null;
            try
            {
                tmp = new GameObject("OzUe_PlainCheck", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                var rt = (RectTransform)tmp.transform; rt.sizeDelta = new Vector2(1920, 1080);
                var probe = Hud.Label(rt, "Probe", "", 12, OzUi.Ink, 0f, OzStyle.Plain);
                OzUePlan.Measure m = (t, size, k) => { probe.FontSize = size; probe.Tracking = k; return probe.WidthAt(t); };
                f += Cap(log);
                f += TopBar(log);
                f += Dialogs(log, new OzUeLeave(rt, null, null), new OzUeDialogs(rt, null));
                f += Words(log);
                f += Scope(log, m);
                f += Note(log, m);
                f += Toasts(log, m);
                f += Profile(log);
                f += Loading(log);
                f += Sample(log);
                f += Texts(log);
            }
            catch (Exception e) { f += OzUnitEditor.Line(log, false, "P4 the test round's checks ran without an exception: " + e.GetType().Name + ": " + e.Message); }
            finally
            {
                if (tmp) { if (Application.isPlaying) UnityEngine.Object.Destroy(tmp); else UnityEngine.Object.DestroyImmediate(tmp); }
                OzUeShell.DisposeMeasure();
            }
            return f;
        }

        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string Pct(float s) => Mathf.RoundToInt(s * 100f).ToString(CultureInfo.InvariantCulture) + " %";

        // ------------------------------------------------------------------ 1 the cap
        static int Cap(StringBuilder log)
        {
            int ps = OzUeCap.PerSide, all = OzUeCap.Total, mx = OzUeCap.SampleFightsMax;
            string near = OzUeCap.CountText(ps * 4 / 5), under = OzUeCap.CountText(ps * 4 / 5 - 1);
            bool ok = ps >= 200 && ps <= 1000 && all >= ps && all <= 2 * ps && mx == Math.Max(1, ps / 30)
                      && OzUeCap.FullSide(Team.Red).EndsWith(" IS FULL: " + ps + " UNITS, THE EDITOR'S LIMIT", StringComparison.Ordinal)
                      && OzUeCap.FullAll == "THE FIELD IS FULL: " + all + " UNITS, THE EDITOR'S LIMIT" && near == (ps * 4 / 5) + "/" + ps && under.IndexOf('/') < 0;
            return OzUnitEditor.Line(log, ok, "P1 the unit cap: " + ps + " a side, " + all + " in all (provisional: menu 5 measures 100 .. 400 a side); SAMPLE FIGHT at most " + mx
                                              + " on the field at once; refusals in plain words (\"" + OzUeCap.FullSide(Team.Red) + "\", \"" + OzUeCap.FullAll + "\"); the counts plate shows \""
                                              + near + "\" from 80 % of the cap on (\"" + under + "\" under it)");
        }

        // ------------------------------------------------------------------ 2 the top bar
        static int TopBar(StringBuilder log)
        {
            int n = 0, bad = 0; string first = null; UeR at = default; float least = float.MaxValue;
            foreach (var sc in OzUeLayoutGrid.Screens)
                foreach (float s in OzText.ScalePresets)
                {
                    var cs = OzUnitEditor.CanvasFor(sc.w, sc.h, s);
                    var p = OzUePlan.TopBar(cs.x, cs.y, OzUeShellRoot.Compact(cs), OzUeTopBar.Crumb, OzUeTopBar.CrumbLong, OzUeShell.TextW);
                    n++;
                    if (!OzUePlan.TopOk(p, out string why)) { bad++; if (first == null) first = sc.w + "x" + sc.h + " at " + Pct(s) + ": " + why; }
                    least = Mathf.Min(least, p.reset.x - (p.crumbX + p.crumbW));
                    if (sc.w == 1920 && sc.h == 1080 && Mathf.Abs(s - 1f) < 0.001f) at = p.reset;
                }
            return OzUnitEditor.Line(log, bad == 0, "P4 the top bar with RESET STATS at the 7 design sizes x the " + OzText.ScalePresets.Length + " UI SCALE steps (" + n + " layouts): RESET STATS "
                                                     + F(OzUePlan.ResetGap) + " u left of SAVE, inside the bar, no button over another, the crumb " + F(OzUePlan.Gap) + " u or more before it (the least "
                                                     + F(least) + " u)" + (bad > 0 ? "; " + bad + " bad, the first " + first : "; at 1920 x 1080 RESET STATS " + at));
        }

        // ------------------------------------------------------------------ 3 / 4 the editor's windows (1.2 item 109: game-style, UI/OzUeWinPlan.cs)
        static int Dialogs(StringBuilder log, OzUeLeave lv, OzUeDialogs dl)
        {
            int f = 0;
            int a = Win(lv.LeavePlan, out string fa, out string ta), b = Win(lv.LeaveSavedPlan, out string fb, out string tb);
            bool forms = lv.LeavePlan.hasLine && !lv.LeaveSavedPlan.hasLine && lv.LeavePlan.buttons.Length == 3 && lv.LeaveSavedPlan.buttons.Length == 2
                         && lv.LeavePlan.main == 2 && lv.LeaveSavedPlan.main == 1;
            f += OzUnitEditor.Line(log, a == 0 && b == 0 && forms && Sizes(), "P4 " + OzUePlan.LeaveTitle + " with Custom changes not saved (\"" + OzUePlan.NotSaved + "\", "
                                                                     + OzUeWinPlan.Describe(lv.LeavePlan) + ") and with none (no line, " + OzUeWinPlan.Describe(lv.LeaveSavedPlan) + ") at the 7 design sizes x the "
                                                                     + OzText.ScalePresets.Length + " UI SCALE steps: full size, 16 u inside the canvas, the title and the line on one line, the buttons in one row "
                                                                     + "inside the plate, none over another, no text under " + F(OzText.MinSize) + " u; the tightest " + (ta ?? tb)
                                                                     + (a + b > 0 ? "; " + (a + b) + " bad, the first " + (fa ?? fb) : ""));
            var l1 = dl.LoadPlan; var l6 = dl.LoadPlanMax;
            int c = Win(lv.ResetPlan, out string fc, out _), d = Win(l6, out string fd, out _), e = Win(dl.SharePlan, out string fe, out _), g = Win(l1, out string fg, out _);
            f += OzUnitEditor.Line(log, c + d + e + g == 0 && dl.SharePlan.hasLine && lv.ResetPlan.hasLine && l6.rows == OzUeWinPlan.MaxRows,
                                   "P4 " + OzUePlan.ResetTitle + " (\"" + OzUePlan.ResetLine + "\", " + OzUeWinPlan.Describe(lv.ResetPlan) + "), " + OzUeDialogs.LoadTitle + " with 1 and "
                                   + OzUeWinPlan.MaxRows + " rows and its toggle (" + OzUeWinPlan.Describe(l1) + " / " + F(l6.h) + " u tall) and " + OzUeDialogs.ShareTitle + " (\"" + OzUeDialogs.ShareLine
                                   + "\", " + OzUeWinPlan.Describe(dl.SharePlan) + ") at the 7 design sizes x every UI SCALE step: full size, 16 u inside the canvas, the buttons in one row inside "
                                   + "the plate, none over another, the texts, the list and the toggle over the row" + (c + d + e + g > 0 ? ": " + (c + d + e + g) + " bad layout(s), the first " + (fc ?? fd ?? fe ?? fg) : ""));
            return f;
        }

        /// <summary>The windows' text sizes: none under the game's floor (11 u).</summary>
        static bool Sizes() => OzUeWinPlan.StencilSize >= OzText.MinSize && OzUeWinPlan.LineSize >= OzText.MinSize && OzUeWinPlan.CapSize >= OzText.MinSize && OzUeWinPlan.BtnSize >= OzText.MinSize;

        /// <summary>One window's plan at every design size x UI SCALE step: the bad layouts; first = the first problem, tight = the tightest.</summary>
        static int Win(OzUeWinPlan.Win d, out string first, out string tight)
        {
            int bad = 0; first = null; tight = null; float least = float.MaxValue;
            foreach (var sc in OzUeLayoutGrid.Screens)
                foreach (float s in OzText.ScalePresets)
                {
                    var cs = OzUnitEditor.CanvasFor(sc.w, sc.h, s);
                    if (!OzUeWinPlan.WinOk(d, cs.x, cs.y, out string why)) { bad++; if (first == null) first = sc.w + "x" + sc.h + " at " + Pct(s) + ": " + why; }
                    var r = OzUePlan.Place(cs.x, cs.y, d.w, d.h, out _);
                    float spare = Mathf.Min(r.x, Mathf.Min(r.y, cs.y - r.B));
                    if (spare < least) { least = spare; tight = sc.w + "x" + sc.h + " at " + Pct(s) + " (canvas " + F(cs.x) + " x " + F(cs.y) + "), " + F(least) + " u to spare"; }
                }
            return bad;
        }

        // ------------------------------------------------------------------ 5 the two-line words
        static int Words(StringBuilder log)
        {
            bool a = OzUePlan.WordsOk(OzUePlan.ObstacleWordsUp, OzUePlan.ObstacleBarUp, OzUePlan.ObstacleBarH, out string wa);
            bool b = OzUePlan.WordsOk(OzUePlan.FlagWordsUp, 0f, 0f, out string wb);
            bool c = OzUePlan.WordsOk(OzUePlan.EmptyWordsUp, 8f, 3f, out string wc);
            return OzUnitEditor.Line(log, a && b && c, "P4 the field's words in two lines, shown while pointed at or picked: the obstacle \"OBSTACLE  5405 / 10000 HP\" over \"" + OzUePlan.ObstacleHint
                                                       + "\" (its bar alone when hit), the flag \"FLAG  neutral\" over \"" + OzUePlan.FlagHint + "\", empty hardware over \"" + OzUePlan.EmptyHint
                                                       + "\": the lines " + F(OzUePlan.WordsLine) + " u apart, their letters clear of each other and of the bar" + (a && b && c ? "" : " (" + (wa ?? wb ?? wc) + ")"));
        }

        // ------------------------------------------------------------------ 6 the line under the set switch
        static int Scope(StringBuilder log, OzUePlan.Measure m)
        {
            int n = 0, longs = 0; bool ok = true; float least = float.MaxValue; string tight = "";
            foreach (var cs in UeStatsChecks.Sizes)
            {
                var L = UeBoxLayout.For(cs, UeStatsChecks.InsetTop, UeStatsChecks.InsetBottom);
                float size = L.compact ? OzUePlan.ScopeSizeCompact : OzUePlan.ScopeSize, width = L.w - 22f;
                ok &= OzUePlan.CellH(L.compact) + 14f <= L.bodyTop - L.setTop + 0.01f;                                   // the cells and the line's band above the body, as before
                foreach (var set in OzUeSets.All)
                {
                    string t = OzUePlan.ScopeFor(OzUeSets.Name(set), width, size, m, out bool fits);
                    n++; ok &= fits; if (t == OzUePlan.ScopeLong(OzUeSets.Name(set))) longs++;
                    float spare = width - m(t, size, 0f);
                    if (spare < least) { least = spare; tight = OzUeSets.Name(set) + " in the " + F(L.w) + " u box"; }
                }
            }
            return OzUnitEditor.Line(log, ok, "P3 the line under the set switch (\"" + OzUePlan.ScopeLong("Custom") + "\", shorter in a narrow box) fits the STATS box at its 4 sizes for each of the 4 sets ("
                                              + n + "): the longest words in " + longs + ", the tightest " + tight + " with " + F(least) + " u to spare; the cells " + F(OzUePlan.CellH(false)) + " u (compact "
                                              + F(OzUePlan.CellH(true)) + "), the body where it was");
        }

        // ------------------------------------------------------------------ 7 the inspector's line by the Custom column
        static int Note(StringBuilder log, OzUePlan.Measure m)
        {
            int n = 0, longs = 0, bad = 0; string first = null, widest = null; float minW = float.MaxValue;
            foreach (var cs in UeStatsChecks.Sizes)
            {
                var L = UeBoxLayout.For(cs, UeStatsChecks.InsetTop, UeStatsChecks.InsetBottom);
                float width = L.w - 20f;
                foreach (var id in OzUeLiveStats.Units)
                {
                    string name = OzUePlayground.FullName(id);
                    string t = OzUePlan.NoteFor(name, width, L.compact, m, out int lines);
                    n++; if (t == OzUePlan.NoteLong(name)) longs++;
                    if (lines > 2) { bad++; if (first == null) first = name + " in the " + F(L.w) + " u box: " + lines + " lines"; }
                    if (width < minW) { minW = width; widest = name; }
                }
            }
            float band = OzUePlan.NoteH(false), bandC = OzUePlan.NoteH(true);
            return OzUnitEditor.Line(log, bad == 0 && OzUePlan.NoteSizeCompact >= OzText.MinSize, "P3 the line by the Custom column (\"" + OzUePlan.NoteLong("JEEP") + "\") in two lines at most for each of the "
                                              + OzUeLiveStats.Units.Length + " units at the STATS box's 4 sizes (" + n + "): the longest words in " + longs + ", a shorter wording in the others; its band "
                                              + F(band) + " u (compact " + F(bandC) + ") over the foot" + (bad > 0 ? "; " + bad + " too long, the first " + first : ""));
        }

        // ------------------------------------------------------------------ 8 the toasts of a Custom change and of SAVE
        static int Toasts(StringBuilder log, OzUePlan.Measure m)
        {
            float cw = float.MaxValue; string at = "";
            foreach (var sc in OzUeLayoutGrid.Screens)
                foreach (float s in OzText.ScalePresets) { var c = OzUnitEditor.CanvasFor(sc.w, sc.h, s); if (c.x < cw) { cw = c.x; at = sc.w + "x" + sc.h + " at " + Pct(s); } }
            float room = cw - 32f; int n = 0, bad = 0; float widest = 0f; string wt = "", first = null;
            bool sw = OzUeSets.EditSwitched;
            try
            {
                OzUeSets.EditSwitched = true;                                                                     // the longest: with " · FIELD NOW ON CUSTOM"
                foreach (var id in OzUeLiveStats.Units)
                    for (int i = 0; i < (int)UeStat.Dps; i++)
                    {
                        var st = (UeStat)i;
                        if (!OzUeSets.Editable(id, st)) continue;
                        foreach (bool back in new[] { false, true })
                        {
                            string t = OzUeSets.EditToast(id, st, back);
                            float w = Mathf.Ceil(m(t, 12f, 0.12f) + 2f * OzUeToast.PadX + 2f);
                            n++;
                            if (w > widest) { widest = w; wt = t; }
                            if (w > room) { bad++; if (first == null) first = t; }
                        }
                    }
                foreach (var (t, sub) in new (string t, string sub)[] { (OzUePlan.SavedToast, OzUeFiles.FileName), ("PATH COPIED", OzUeFiles.Shown(OzUeFiles.FilePath)), (OzUePlan.ResetDone, null),
                                                                      (OzUePlan.NothingToReset, null), (OzUeSets.UnitToast("missile_cannon", "CUSTOM = ZODENGINE VALUES"), null) })
                {
                    float w = Mathf.Ceil(m(t, 12f, 0.12f) + 2f * OzUeToast.PadX + 2f);
                    if (sub != null) w = Mathf.Ceil(w + OzUeToast.SubGap + m(sub, OzUeToast.SubSize, 0f));             // 1.2 item 109: the small part beside the caps
                    n++; if (w > widest) { widest = w; wt = t + (sub != null ? "  " + sub : ""); }
                    if (w > room) { bad++; if (first == null) first = t; }
                }
            }
            finally { OzUeSets.EditSwitched = sw; }
            return OzUnitEditor.Line(log, bad == 0, "P4 the toasts of a Custom change (every unit x every editable stat, typed and put back, with \"" + OzUePlan.FieldNowCustom.Trim() + "\"), of SAVE (with "
                                                     + "the file's name), of COPY FILE PATH (with the path) and of RESET STATS (" + n + ") fit one line on the narrowest canvas (" + at + ", " + F(cw) + " u): the widest "
                                                     + F(widest) + " u, \"" + wt + "\"" + (bad > 0 ? "; " + bad + " too wide, the first \"" + first + "\"" : ""));
        }

        // ------------------------------------------------------------------ 9 the saved Custom profile's format (the levels read it)
        static int Profile(StringBuilder log)
        {
            var src = new Dictionary<string, double[]>(StringComparer.Ordinal);
            int values = 0;
            foreach (var id in OzUeLiveStats.Units)
            {
                var a = new double[OzUeLiveStats.StatCount];
                for (int i = 0; i < a.Length; i++) a[i] = double.NaN;
                for (int i = 0; i < (int)UeStat.Dps; i++)
                {
                    var st = (UeStat)i;
                    if (!OzUeSets.Editable(id, st)) continue;
                    var o = OzUeSets.Cell(id, st, UeSet.OpenZ);
                    if (!o.HasNumber) continue;
                    a[i] = OzUeLiveStats.Limit(id, st, o.value * (i % 3 == 0 ? 1.37 : 1.0) + (i == (int)UeStat.Reload ? 0.123456789 : 0.0));   // some values changed, one with many decimals
                    values++;
                }
                src[id] = a;
            }
            string text = OzUeProfile.Format(src);
            var back = OzUeProfile.Parse(text);
            int same = 0, wrong = 0;
            foreach (var kv in src)
                for (int i = 0; i < (int)UeStat.Dps; i++)
                {
                    if (double.IsNaN(kv.Value[i])) continue;
                    if (back.TryGetValue(kv.Key, out var b) && b[i].Equals(kv.Value[i])) same++; else wrong++;
                }
            var junk = OzUeProfile.Parse("nobody.hp=5;grunt.nokey=3;grunt.hp=NaN;grunt.hp=1e400;grunt.hp=;=4;grunt.dps=9;jeep.hp=-50;grunt.squad=3");
            bool junkOk = !junk.ContainsKey("nobody") && junk.TryGetValue("grunt", out var g) && g[(int)UeStat.Squad] == 3 && double.IsNaN(g[(int)UeStat.Health]) && double.IsNaN(g[(int)UeStat.Dps])
                          && junk.TryGetValue("jeep", out var j) && j[(int)UeStat.Health] >= OzUeLiveStats.Def(UeStat.Health).min;
            bool keys = OzUeProfile.KeyValues == "oz_ue_profile" && OzUeProfile.KeyName == "oz_ue_profile_name" && OzUeProfile.KeyBase == "oz_ue_profile_base" && OzUeProfile.KeyChanged == "oz_ue_profile_changed";
            return OzUnitEditor.Line(log, values > 0 && wrong == 0 && same == values && junkOk && keys && OzUeProfile.Parse("").Count == 0,
                                     "P1 the saved Custom profile the levels read (PlayerPrefs " + OzUeProfile.KeyValues + " / _name / _base / _changed; written by SAVE, removed by RESET STATS): " + values
                                     + " values of the " + OzUeLiveStats.Units.Length + " units written and read back exactly (" + same + " same, " + wrong + " wrong; " + text.Length + " characters); "
                                     + "unknown units, keys, NaN, overflow and empty values are skipped, values kept inside their limits (" + junkOk + "); this check writes nothing");
        }

        // ------------------------------------------------------------------ 10 the loading picture
        static int Loading(StringBuilder log)
        {
            var t = Resources.Load<Texture2D>(OzUeLoadingPicture.Path);
            bool ok = t != null && t.width == 2560 && t.height == 1440 && t.mipmapCount > 1;
            if (t != null)
                foreach (var a in new[] { 16f / 9f, 16f / 10f, 4f / 3f, 21f / 9f, 32f / 9f, 9f / 16f })
                {
                    var uv = OzLevelPreview.Cover((float)t.width / t.height, a);
                    float shown = t.width * uv.width / (t.height * uv.height);
                    ok &= uv.width <= 1.0001f && uv.height <= 1.0001f && uv.x >= -0.0001f && uv.y >= -0.0001f && Mathf.Abs(shown / a - 1f) < 0.001f;
                }
            return OzUnitEditor.Line(log, ok, "P4 the Unit editor's loading picture Resources/" + OzUeLoadingPicture.Path + ": " + (t ? t.width + " x " + t.height + ", " + t.mipmapCount + " mip levels, " + t.format : "missing")
                                              + "; on 16:9, 16:10, 4:3, 21:9, 32:9 and 9:16 screens it covers the screen cropped, never stretched");
        }

        // ------------------------------------------------------------------ 11 SAMPLE FIGHT on paper
        static int Sample(StringBuilder log)
        {
            bool known = true;
            foreach (var id in OzUePlan.SampleBlue) known &= OzUePlayground.KindOf(id) != UeKind.None;
            foreach (var id in OzUePlan.SampleRed) known &= OzUePlayground.KindOf(id) != UeKind.None;
            bool fits = OzUePlan.SampleFits(id => OzUePlayground.KindOf(id) == UeKind.Robot, out string ext);
            return OzUnitEditor.Line(log, known && fits && OzUePlan.SampleBlue.Length == 6 && OzUePlan.SampleRed.Length == 6,
                                     "P1 SAMPLE FIGHT on paper: 6 blue + 6 red groups as before, their spots with their room in one " + F(OzUePlan.SampleStep) + " m cell (" + ext
                                     + "), so fights on the grid of free ground never touch; at most " + OzUeCap.SampleFightsMax + " on the field at once");
        }

        // ------------------------------------------------------------------ 12 the words on which stats the levels use, and the windows' short lines
        static int Texts(StringBuilder log)
        {
            bool esc = false;
            for (int i = 0; i < OzUeHelpCard.Keys.GetLength(0); i++) if (OzUeHelpCard.Keys[i, 0] == "Esc") esc = OzUeHelpCard.Keys[i, 1] == OzUePlan.HelpEsc;
            string[] levels = { OzUePlan.ScopeLong("Custom"), OzUePlan.ScopeTip, OzUeTopBar.SaveTip, OzUeTopBar.ResetTip, OzUePlan.HelpStats, OzUePlan.NoteLong("JEEP") };
            bool all = true; int old = 0;
            foreach (var s in levels) { all &= Has(s, "level"); if (Has(s, "always OpenZ") || Has(s, "Campaign")) old++; }
            bool pick = Has(OzUePlan.ScopeTip, "level screen") && Has(OzUeTopBar.SaveTip, "level screen") && Has(OzUePlan.NoteLong("JEEP"), "level screen") && Has(OzUeTopBar.ResetTip, "levels play OpenZ")
                        && Has(OzUeTopBar.MenuTip, "ESC") && Has(OzUePlan.ScopeTip, "RESET STATS") && Has(OzUePlan.NoteLong("JEEP"), "every JEEP here, both teams");
            bool brief = OzUePlan.NotSaved.Length <= 40 && OzUePlan.ResetLine.Length <= 50 && OzUeDialogs.ShareLine.Length <= 80 && Has(OzUeDialogs.ShareLine, "OpenZ sends nothing");
            return OzUnitEditor.Line(log, esc && all && pick && old == 0 && brief, "P4 the words on which stats are used: the STATS box's line and its tip, the SAVE and RESET STATS tips, the help card "
                                                      + "and the line by the Custom column say the levels play OpenZ unless Custom is picked on the level screen (" + levels.Length + " texts, none says \"always OpenZ\" "
                                                      + "any more: " + (old == 0) + "), a Custom change reaches every unit of the type, both teams, and how to reset; the help card's Esc: \"" + OzUePlan.HelpEsc
                                                      + "\"; MAIN MENU's tip names Esc; the windows say one short line at most (\"" + OzUePlan.NotSaved + "\", \"" + OzUePlan.ResetLine + "\", \""
                                                      + OzUeDialogs.ShareLine + "\": " + brief + ")");
        }

        static bool Has(string s, string part) => s != null && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
