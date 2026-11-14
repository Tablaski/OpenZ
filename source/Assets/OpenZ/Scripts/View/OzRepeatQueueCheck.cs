#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// in Play with a level running in the normal mode (menu OpenZ/V1.4/Repeat Switch: Panel Layout Check (Play, a level playing)). Editor only.
    /// <list type="number">
    /// <item>The player's fort gets a full menu: a unit in production and 5 queued (from its own build list), the menu opened on it.</item>
    /// <item>For every Game view size of <see cref="Screens"/> (1280x720, 1920x1080, 2560x1440, 3840x2160; set through the editor's Game view
    /// sizes and put back), every UI SCALE preset (OzText.ScalePresets, 90..160 %) and every answer of the switch (OFF, LAST, ALL; the fort's
    /// flags set as the tiles set them): the menu's parts as drawn (OpenZProductionWindow.LayoutProbe: buttons, slots, slot tags, the REPEAT
    /// caption, the three tiles with their glyphs, words and the LAST "1", the badge, the return rail, the clear-rally X, and the drawn glyphs of
    /// every text), then counted: overlaps between a part this kit placed or made and any other part (a part and what it belongs to excepted);
    /// each kit part at least <see cref="EdgeU"/> u inside the menu's frame; each word inside what it belongs to (a button's words
    /// <see cref="WordsU"/> u from its left and right); the whole menu on the screen; the states: exactly the right tile lit, ALL live (DV169
    /// on), the caption and the tiles shown, 1.3's REPEAT hidden, the badge on the last slot and its REPEAT tag for LAST only, the rail for ALL
    /// only, the X with its tooltip; and the places (<see cref="Places"/>): every part of the switch against its own number in
    /// OzRepeatSwitchLayout (window units, x from the menu's left, y down from the QUEUE row's top), within <see cref="SizeTolU"/>: the caption
    /// at 305, the tiles 43 x 52 at 305 / 350 / 395, y 16, their glyphs 20 u at the tile's x + 11.5, y 21, their words centred at the tile's
    /// x + 21.5, y 57 at full size, the LAST "1" centred on its glyph (371.5, 31), the badge 13 u (shade 15 u) centred at the last filled slot's
    /// x + 45, y 22, the rail's five pieces (down from the last filled slot's centre at y 68, along y 73, up to the first slot's centre at y 69,
    /// the arrow head) at OzRepeatSwitchLayout.RailWidth (2 u, at least 2 px; drawn at least 2 px thick), the hint at x 70, SET RALLY 208 x 32
    /// at 15 and the X 32 x 32 at 229 (y 82), its glyph 16 u in the middle.</item>
    /// <item>Extra layouts at 1920x1080, 100 %: a queue of 2 with LAST and with ALL (the marks follow the last filled slot); the REPEAT QUEUE rule
    /// off for a moment (ALL disabled, its tooltip); a rally point set (the X live) and none (the X greyed); the 'Zod exact' row
    /// (OpenZProductionWindow.ProofZodExact: 1.3's QUEUE row at 1.3's places, the kit's bottom row); the 1.3 look for the before picture.</item>
    /// <item>The hint's texts beside the switch measured with the game's text engine at their own size: each inside its box.</item>
    /// <item>The clicks as a player makes them: the check's own mouse (a device of its own, OzRepeatSwitchMouse; the editor's mouse is off
    /// meanwhile and put back) moved over the middle of a tile, pressed and let go, so OpenZHud's own rule decides (an enabled button: its
    /// OnClick or OnRightClick; a disabled one is never clicked: ui_disabled for a left click, nothing for a right click). 15 left clicks through
    /// every change between OFF, LAST and ALL, each with REPEAT on and off before it (among them ALL -> LAST with REPEAT already on, which sends
    /// only REPEAT QUEUE off), a click on the lit tile of each answer; 6 right clicks (the same picks); with the REPEAT QUEUE rule off a left
    /// and a right click on the disabled ALL; the X live (a rally point set: the click clears it) and greyed (ui_disabled). After each: the tile
    /// was under the pointer, the fort's flags (OFF = REPEAT off, REPEAT QUEUE off; LAST = REPEAT on, REPEAT QUEUE off; ALL = REPEAT QUEUE on,
    /// REPEAT as it was; the lit or a disabled tile = nothing changes), the lit tile, and the cues asked for in the sound log (ui_toggle_on for
    /// LAST and ALL, ui_toggle_off for OFF, none for the lit tile, ui_disabled for a left click on a disabled button). Then PickRepeat's own
    /// second guard (ALL with the rule off, called directly, as no click reaches it): ui_error, nothing changes.</item>
    /// <item>Pictures: the menu cut out at every size at 100 % in each answer, the extremes 1280x720 at 160 % and 3840x2160 at 90 %, the extra
    /// proof/play (set by the menu).</item>
    /// </list>
    /// PASS: every layout counted, 0 overlaps, every part inside, every state right, every part at its number, every hint text inside its box,
    /// every click made and right. The Game view must be in front (the check's mouse reaches the game only then; it says so when it does not).
    /// The fort's production, its rally points, the rule switch, the UI scale, the Game view size and the editor's mouse are put back at the
    /// end (the unit in production restarts its clock).
    /// </summary>
    public sealed class OzRepeatQueueCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.4 repeat switch layout]";
        public static string Report = "not run", Folder;
        public static bool Running;
        public static readonly Vector2Int[] Screens = { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160) };
        static readonly int[] Modes = { OpenZProductionWindow.RepeatOff, OpenZProductionWindow.RepeatLast, OpenZProductionWindow.RepeatAll };
        /// <summary>Kit parts stay this far (window units) inside the menu's outer edge (the frame's border is about 6 u).</summary>
        public const float EdgeU = 6f;
        /// <summary>A button's words stay this far (window units) inside the button, left and right.</summary>
        public const float WordsU = OzRepeatQueueCheckMargins.WordsU;
        /// <summary>Two parts overlap when their rectangles share more than this (screen pixels each way): anti-aliased edges are not overlaps.</summary>
        public const float TouchPx = 0.5f;
        /// <summary>A box measured in window units matches its number within this.</summary>
        public const float SizeTolU = 0.6f;
        /// <summary>TextMeshPro's drawn text box (textBounds) as the game draws it (measured in the repeat_queue kit's Play run of 5 Nov 2026,
        /// e.g. QUEUE 14.0..63.9 x -2.6..10.7 at size 11): it starts at the text's own x, runs on by one letter spacing after the last letter, is
        /// 1.209 em tall and centred on the caps' middle (0.3635 em under a top-aligned text's y).</summary>
        public const float TextEm = 1.209f, CapMidEm = 0.3635f;
        /// <summary>The drawn size of every text of the switch: OzText's minimum (the caption 9, the hint 8 and the words 11 all draw at 11).</summary>
        public const float TextSize = 11f;
        /// <summary>The queue hint's letter spacing (UI/OpenZProductionWindow.cs: 8 -> 11, +0.14) for 1.3's right-aligned hint.</summary>
        public const float HintTracking = 0.14f;

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OZ_RepeatSwitchCheck").AddComponent<OzRepeatQueueCheck>();
        }

        IEnumerator Start()
        {
            Running = true;
            try { yield return Main(); }
            finally { Running = false; Destroy(gameObject); }
        }

        sealed class Saved { public string cur; public List<string> queue = new List<string>(); public List<ZWaypoint> rally = new List<ZWaypoint>(); public bool building, repeat, repeatQueue; }

        ZWorld W; Team me; ZObject fort; OpenZProductionWindow pw;
        int layouts, overlaps, outside, words, offScreen, states, places, sizeMiss, clicks, clickBad;
        Mouse mouse, sysMouse;
        /// <summary>The player's clicks of the test (tile, right button), from LAST lit: 15 left clicks through every change, each of ALL -> LAST /
        /// OFF with REPEAT on and with REPEAT off before it, and the lit tile of each answer; then 6 right clicks (every change once, the lit tile).</summary>
        static readonly (int mode, bool right)[] ClickSeq =
        {
            (2, false), (1, false), (0, false), (2, false), (1, false), (2, false), (0, false), (2, false), (0, false), (1, false), (1, false), (2, false), (2, false), (0, false), (0, false),
            (2, true), (0, true), (1, true), (2, true), (1, true), (1, true),
        };
        /// <summary>Clicks made through the check's own mouse: the sequence, the two on the disabled ALL, the two on the X.</summary>
        static int ClicksWanted => ClickSeq.Length + 4;
        /// <summary>The cues of the switch and of OpenZHud's disabled-button rule, read back from the sound log.</summary>
        static readonly string[] CueKeys = { "ui_toggle_on", "ui_toggle_off", "ui_disabled", "ui_error" };
        readonly List<string> lines = new List<string>();
        readonly List<(string file, string what)> pics = new List<(string file, string what)>();
        string first;

        IEnumerator Main()
        {
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); var hud = FindFirstObjectByType<OpenZHud>();
            W = g ? g.World : null;
            if (!g || W == null || !hud || hud.Production == null) { Report = Tag + " FAIL: no level playing (ZGame, OpenZHud)"; Debug.LogWarning(Report); yield break; }
            pw = hud.Production;
            if (!ZWorld.FifoQueueDeviation || pw.ZodExactRow) { Report = Tag + " FAIL: 'Zod exact' (or no DV21) in this session: the switch is not shown; start a level in the normal mode"; Debug.LogWarning(Report); yield break; }
            if (!ZWorld.RepeatQueueDeviation) { Report = Tag + " FAIL: the REPEAT QUEUE rule (DV169) is off in this session: start a level in the normal mode"; Debug.LogWarning(Report); yield break; }
            me = hud.Player;
            fort = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == me && !o.Destroyed) ?? W.Objects.FirstOrDefault(o => o.IsProducer && o.Owner == me && !o.Destroyed);
            if (fort == null) { Report = Tag + " FAIL: the player has no fort or factory standing"; Debug.LogWarning(Report); yield break; }

            // the fort's production and rally points as they are, put back at the end
            var saved = new Saved { cur = fort.CurUnit, building = fort.Building, repeat = fort.RepeatProduction, repeatQueue = fort.RepeatQueue };
            saved.queue.AddRange(fort.Queue); saved.rally.AddRange(fort.RallyPoints);
            float uiSaved = OzText.UiScale; int viewSaved = OzFinalGateView.SelectedIndex(); bool ruleSaved = ZWorld.RepeatQueueDeviation;
            int extras = 0, extraBad = 0, hintBad = 0;
            List<string> list = null;
            try
            {
                list = ZBuildLists.For(fort.Type, fort.BuildLevel).Where(u => !ZBuildLists.IsCannon(u)).ToList();
                Fill(list, ZObject.MAX_QUEUE_ITEMS);
                W.SetRallyPoints(me, fort, new ZWaypoint[0]);
                pw.Open(fort);
                yield return new WaitForSecondsRealtime(0.4f);
                if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);

                // 1. every screen x every UI scale x OFF / LAST / ALL
                foreach (var scr in Screens)
                {
                    OzFinalGateView.SetFixed(scr.x, scr.y, "OpenZ check " + scr.x + "x" + scr.y, out string info);
                    yield return new WaitForSecondsRealtime(0.5f);
                    if (Screen.width != scr.x || Screen.height != scr.y) { sizeMiss++; lines.Add($"SIZE  wanted {scr.x}x{scr.y}, the Game view is {Screen.width}x{Screen.height} ({info})"); }
                    foreach (var s in OzText.ScalePresets)
                    {
                        OzText.UiScale = s; hud.ApplyUiScale();
                        foreach (var mode in Modes)
                        {
                            SetMode(mode);
                            if (!pw.IsOpen) pw.Open(fort);
                            pw.ProofRefresh();
                            for (int f = 0; f < 4; f++) yield return null;
                            yield return new WaitForSecondsRealtime(0.15f);                                // the menu's 10 Hz texts
                            bool picture = Mathf.Approximately(s, 1f) || (scr.x == 1280 && Mathf.Approximately(s, 1.6f)) || (scr.x == 3840 && Mathf.Approximately(s, 0.9f));
                            string at = $"{Screen.width}x{Screen.height} UI {Mathf.RoundToInt(s * 100)} % {OpenZProductionWindow.RepeatWords[mode]}";
                            bool ok = Layout(at, mode, ZObject.MAX_QUEUE_ITEMS, true);
                            layouts++;
                            if (picture) yield return Picture($"menu_{Screen.width}x{Screen.height}_ui{Mathf.RoundToInt(s * 100)}_repeat_{OpenZProductionWindow.RepeatWords[mode].ToLowerInvariant()}.png", at + (ok ? "" : " (FAIL)"));
                        }
                    }
                }

                // 2. extra layouts at 1920x1080, 100 %
                OzFinalGateView.SetFixed(1920, 1080, "OpenZ check 1920x1080", out _);
                OzText.UiScale = 1f; hud.ApplyUiScale();
                yield return new WaitForSecondsRealtime(0.5f);
                foreach (var mode in new[] { OpenZProductionWindow.RepeatLast, OpenZProductionWindow.RepeatAll })
                {   // a queue of 2: the marks follow the last filled slot
                    Fill(list, 2); SetMode(mode); pw.ProofRefresh();
                    yield return Settle();
                    extras++; if (!Layout($"1920x1080 UI 100 % {OpenZProductionWindow.RepeatWords[mode]}, a queue of 2", mode, 2, true)) extraBad++;
                    yield return Picture($"extra_queue2_repeat_{OpenZProductionWindow.RepeatWords[mode].ToLowerInvariant()}.png", $"a queue of 2, {OpenZProductionWindow.RepeatWords[mode]} lit: the mark on slot 2");
                }
                Fill(list, ZObject.MAX_QUEUE_ITEMS);
                {   // the REPEAT QUEUE rule off for a moment: ALL disabled with its tooltip, LAST lit
                    SetMode(OpenZProductionWindow.RepeatLast);
                    ZWorld.RepeatQueueDeviation = false; pw.ProofRefresh();
                    yield return Settle();
                    extras++;
                    bool ok = Layout("1920x1080 UI 100 %, the REPEAT QUEUE rule off", OpenZProductionWindow.RepeatLast, ZObject.MAX_QUEUE_ITEMS, false);
                    string tip = pw.RepeatTileTip(OpenZProductionWindow.RepeatAll);
                    if (tip != OpenZProductionWindow.RepeatAllOffTip) { ok = false; states++; lines.Add("FAIL  the rule off: the ALL tooltip is '" + tip + "'"); }
                    yield return Picture("extra_rule_dv169_off.png", "the REPEAT QUEUE rule (DV169) off: ALL disabled");
                    if (!ok) extraBad++;                                                                   // its clicks: part 4, with the check's own mouse
                    ZWorld.RepeatQueueDeviation = ruleSaved; pw.ProofRefresh();
                    yield return Settle();
                }
                {   // a rally point: the X live; none: greyed (Refresh's rule, the same button)
                    W.SetRallyPoints(me, fort, new[] { RallyPoint() });
                    yield return Settle();
                    extras++;
                    bool ok = Layout("1920x1080 UI 100 % LAST, a rally point set", OpenZProductionWindow.RepeatLast, ZObject.MAX_QUEUE_ITEMS, true);
                    if (!pw.ClearLive) { ok = false; states++; lines.Add("FAIL  a rally point set: the X is not live"); }
                    if (pw.QueueHintShown != OpenZProductionWindow.HintPlain) { ok = false; states++; lines.Add("FAIL  a rally point set: the hint reads '" + pw.QueueHintShown + "'"); }
                    if (!ok) extraBad++;
                    yield return Picture("extra_rally_set_x_live.png", "a rally point set: the clear-rally X live");
                    W.SetRallyPoints(me, fort, new ZWaypoint[0]);
                    yield return Settle();
                    if (pw.ClearLive) { extraBad++; states++; lines.Add("FAIL  no rally point: the X is live"); }
                }
                {   // 'Zod exact': 1.3's QUEUE row with the kit's bottom row (LAST: REPEAT on, so 1.3's glyph shows too)
                    SetMode(OpenZProductionWindow.RepeatLast);
                    pw.ProofZodExact(true);
                    yield return Settle();
                    extras++;
                    var parts = pw.LayoutProbe();
                    var r = Count(parts, pw.UnitPx, out int ov, out int outs, out int wd, out bool onScreen);
                    bool st = !pw.RepeatSwitchShown && pw.ClassicRowShown && pw.ClearIsX && !pw.RepeatBadgeShown && !pw.RepeatRailShown;
                    string where = ClassicPlaces(parts);
                    overlaps += ov; outside += outs; words += wd; if (!onScreen) offScreen++; if (!st) states++; if (where != null) places++;
                    bool ok = ov + outs + wd == 0 && onScreen && st && where == null; if (!ok) extraBad++;
                    lines.Add($"{(ok ? "OK   " : "FAIL ")} 1920x1080 UI 100 % 'Zod exact' row: {parts.Count} parts, overlaps {ov}, kit parts outside {outs}, words outside {wd}, "
                              + $"states {(st ? "right (1.3's REPEAT row, the X)" : "WRONG")}, places {(where == null ? "right (1.3's glyph 311 / 30, REPEAT 98 x 32 at 340, the hint to 438)" : "WRONG: " + where)}{r}");
                    yield return Picture("extra_zod_exact_row.png", "'Zod exact': 1.3's QUEUE row (REPEAT ON / OFF), the kit's bottom row");
                    pw.ProofZodExact(false);
                    yield return Settle();
                }

                // 3. the hint's texts at their own size, in their box
                {
                    var hw = pw.HintWidths(out float box);
                    foreach (var (text, width) in hw)
                    {
                        bool fits = width <= box + 0.01f;
                        if (!fits) hintBad++;
                        lines.Add($"{(fits ? "OK   " : "FAIL ")} hint '{text}': {width:0.0} u at its own size in {box:0} u");
                    }
                }

                // 4. the clicks as a player makes them: the check's own mouse over the middle of a tile, pressed and let go, so OpenZHud's own
                //    rule decides (an enabled button: its OnClick / OnRightClick and the HUD's click sound; a disabled one is never clicked: a left
                //    click plays ui_disabled, a right click nothing). After each: the flags, the lit tile, the cues in the sound log.
                SetMode(OpenZProductionWindow.RepeatLast); pw.ProofRefresh();
                yield return Settle();
                const int cLast = OpenZProductionWindow.RepeatLast, cAll = OpenZProductionWindow.RepeatAll;
                Devices(true);
                yield return MouseAt(pw.RepeatTileCentre(OpenZProductionWindow.RepeatOff));
                if (!pw.RepeatTileHovered(OpenZProductionWindow.RepeatOff))
                { clickBad++; lines.Add("FAIL  the check's own mouse does not reach the game (the Game view must be in front): no click tested"); }
                else
                {
                    foreach (var (mode, right) in ClickSeq) yield return Click(mode, right, null);
                    {   // the REPEAT QUEUE rule off for a moment: ALL disabled, LAST lit; a left and a right click on ALL change nothing
                        SetMode(cLast); ZWorld.RepeatQueueDeviation = false; pw.ProofRefresh();
                        yield return Settle();
                        yield return Click(cAll, false, "the REPEAT QUEUE rule off, ALL disabled");
                        yield return Click(cAll, true, "the REPEAT QUEUE rule off, ALL disabled");
                        bool r0 = fort.RepeatProduction, q0 = fort.RepeatQueue; float t0 = Time.unscaledTime;
                        pw.ProofPick(cAll);                                                                    // PickRepeat's own second guard: no click reaches it
                        yield return Settle();
                        var heard = Heard(t0);
                        bool guard = fort.RepeatProduction == r0 && fort.RepeatQueue == q0 && pw.RepeatLitShown == cLast && heard.Count == 1 && heard.Contains("ui_error");
                        if (!guard) clickBad++;
                        lines.Add($"{(guard ? "OK   " : "FAIL ")} the rule off, PickRepeat(ALL) called directly (its second guard; no click reaches it): cues {CueList(heard)} (want ui_error), "
                                  + $"REPEAT {OnOff(fort.RepeatProduction)}, REPEAT QUEUE {OnOff(fort.RepeatQueue)}, lit {Word(pw.RepeatLitShown)} (want nothing changed: on / off, LAST)");
                        ZWorld.RepeatQueueDeviation = ruleSaved; pw.ProofRefresh();
                        yield return Settle();
                    }
                    {   // the clear-rally X: a rally point set, a click on the X clears it; then greyed, a click plays ui_disabled and changes nothing
                        W.SetRallyPoints(me, fort, new[] { RallyPoint() });
                        yield return Settle();
                        for (int k = 0; k < 2; k++)
                        {
                            bool live0 = pw.ClearLive; int n0 = fort.RallyPoints.Count;
                            yield return MouseAt(pw.ClearCentre);
                            bool hov = pw.ClearHovered; float t0 = Time.unscaledTime;
                            yield return MouseClick(pw.ClearCentre, false);
                            yield return Settle();
                            var heard = Heard(t0);
                            bool ok = hov && fort.RallyPoints.Count == 0 && !pw.ClearLive && (k == 0 ? live0 && n0 == 1 && !heard.Contains("ui_disabled") : !live0 && heard.Contains("ui_disabled"));
                            clicks++; if (!ok) clickBad++;
                            lines.Add($"{(ok ? "OK   " : "FAIL ")} click the X {(k == 0 ? "with a rally point set (live)" : "with none (greyed)")}: under the pointer {(hov ? "yes" : "NO")}, rally points {n0} -> "
                                      + $"{fort.RallyPoints.Count}, the X {(pw.ClearLive ? "live" : "greyed")}, cues {CueList(heard)} (want {(k == 0 ? "0 points left, greyed" : "ui_disabled, nothing changed")})");
                        }
                    }
                }
                Devices(false);

                // 5. before (1.3) and after at 1920x1080, 100 %, LAST lit for the after picture
                if (!string.IsNullOrEmpty(Folder))
                {
                    SetMode(OpenZProductionWindow.RepeatLast);
                    pw.ProofClassic(true);
                    yield return Settle();
                    yield return Picture("before_1_3_menu_1920x1080.png", "BEFORE (1.3): REPEAT ON / OFF at the end of the queue row, STOP, CLEAR and SET RALLY in the bottom row");
                    pw.ProofClassic(false);
                    yield return Settle();
                    yield return Picture("after_1_4_menu_1920x1080.png", "AFTER (1.4): the REPEAT switch, LAST lit" + (pw.StopInHeader ? ", STOP beside the progress bar" : "") + ", SET RALLY and the clear-rally X");
                    yield return new WaitForEndOfFrame();
                    try { var tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(Folder, "after_1_4_full_frame_1920x1080.png"), tex.EncodeToPNG()); Destroy(tex); pics.Add(("after_1_4_full_frame_1920x1080.png", "the whole frame")); }
                    catch (System.Exception ex) { lines.Add("picture failed: " + ex.Message); }
                }
            }
            finally
            {
                // put everything back
                Devices(false);                                                                            // the editor's own mouse again
                ZWorld.RepeatQueueDeviation = ruleSaved;
                W.StopBuilding(me, fort);
                if (saved.cur != null) W.StartBuilding(me, fort, saved.cur);
                foreach (var q in saved.queue) W.AddBuildingQueue(me, fort, q);
                if (!saved.building) W.StopBuilding(me, fort);
                W.SetBuildingRepeat(me, fort, saved.repeat);
                W.SetBuildingRepeatQueue(me, fort, saved.repeatQueue);
                W.SetRallyPoints(me, fort, saved.rally);
                pw.ProofZodExact(false);
                pw.ProofClassic(false);
                OzText.UiScale = uiSaved; hud.ApplyUiScale();
                OzFinalGateView.Select(viewSaved);
            }

            int want = Screens.Length * OzText.ScalePresets.Length * Modes.Length;
            bool pass = layouts == want && extras == 5 && extraBad == 0 && overlaps == 0 && outside == 0 && words == 0 && offScreen == 0 && states == 0 && places == 0 && sizeMiss == 0 && hintBad == 0
                        && clickBad == 0 && clicks == ClicksWanted;
            var sb = new StringBuilder();
            sb.Append($"{Tag} {(pass ? "PASS" : "FAIL")} {layouts}/{want} layouts ({Screens.Length} screens x {OzText.ScalePresets.Length} UI scales x OFF / LAST / ALL) + {extras - extraBad}/5 extra layouts, "
                      + $"{g.level} {fort.Type} #{fort.Id}, STOP {(pw.StopInHeader ? "beside the progress bar" : "in the bottom row (STOP move not in)")}: overlaps {overlaps}, kit parts outside the frame {outside}, "
                      + $"words outside their part {words}, menu off the screen {offScreen}, wrong states {states}, parts off their numbers {places}, screen sizes not reached {sizeMiss}, hint texts too wide {hintBad}, wrong clicks {clickBad} "
                      + $"({clicks}/{ClicksWanted} clicks through the check's own mouse); pictures {pics.Count}");
            Report = sb.ToString();
            Debug.Log(Report + "\n" + string.Join("\n", lines));
            if (!string.IsNullOrEmpty(Folder))
            {
                try
                {
                    File.WriteAllText(Path.Combine(Folder, "layout_report.txt"), System.DateTime.Now.ToString("dd MMM yyyy HH:mm") + "\n" + Report + "\n" + string.Join("\n", lines) + "\n\nthe parts of the first layout:\n" + first + "\n");
                    var html = new StringBuilder("<!doctype html><meta charset=\"utf-8\"><title>REPEAT switch layout</title><body style=\"background:#1b1d1a;color:#ddd;font:14px sans-serif\">");
                    html.Append("<h2>Build menu: the REPEAT switch (OFF / LAST / ALL)" + (pw.StopInHeader ? ", STOP beside the progress bar" : "") + ", the clear-rally X</h2><p>" + System.Net.WebUtility.HtmlEncode(Report) + "</p>");
                    foreach (var (file, what) in pics) html.Append($"<h3>{System.Net.WebUtility.HtmlEncode(what)}</h3><img src=\"{file}\" style=\"max-width:100%\">");
                    File.WriteAllText(Path.Combine(Folder, "index.html"), html.Append("</body>").ToString());
                }
                catch (System.Exception ex) { Debug.LogWarning(Tag + " report not written: " + ex.Message); }
            }
        }

        /// <summary>The fort builds list[0] with `n` queued after it (from its own build list).</summary>
        void Fill(List<string> list, int n)
        {
            W.StopBuilding(me, fort);
            W.StartBuilding(me, fort, list[0]);
            for (int i = 0; i < n; i++) W.AddBuildingQueue(me, fort, list[(i + 1) % list.Count]);
        }

        /// <summary>The fort's flags as the tile sets them (OFF: REPEAT off, REPEAT QUEUE off; LAST: REPEAT on, REPEAT QUEUE off; ALL: REPEAT QUEUE on).</summary>
        void SetMode(int mode)
        {
            if (mode == OpenZProductionWindow.RepeatAll) { W.SetBuildingRepeat(me, fort, true); W.SetBuildingRepeatQueue(me, fort, true); return; }
            W.SetBuildingRepeat(me, fort, mode == OpenZProductionWindow.RepeatLast);
            W.SetBuildingRepeatQueue(me, fort, false);
        }

        IEnumerator Settle()
        {
            pw.ProofRefresh();
            for (int f = 0; f < 4; f++) yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
        }

        /// <summary>One player's click on tile `mode` (left, or right), through OpenZHud, and what it must do: an enabled tile picks its answer
        /// (the lit one: nothing), a disabled one changes nothing (a left click: ui_disabled). One line in the report.</summary>
        IEnumerator Click(int mode, bool right, string note)
        {
            const int cOff = OpenZProductionWindow.RepeatOff, cLast = OpenZProductionWindow.RepeatLast, cAll = OpenZProductionWindow.RepeatAll;
            bool r0 = fort.RepeatProduction, q0 = fort.RepeatQueue, live = pw.RepeatTileLive(mode); int lit0 = pw.RepeatLitShown;
            bool same = !live || mode == lit0;                                                             // a disabled or the lit tile: nothing changes
            bool wantR = same ? r0 : mode == cAll ? r0 : mode == cLast;                                    // ALL leaves REPEAT as it is
            bool wantQ = same ? q0 : mode == cAll;
            int wantLit = same ? lit0 : mode;
            string cue = !live ? (right ? null : "ui_disabled") : mode == lit0 ? null : mode == cOff ? "ui_toggle_off" : "ui_toggle_on";
            var p = pw.RepeatTileCentre(mode);
            yield return MouseAt(p);
            bool hov = pw.RepeatTileHovered(mode); float t0 = Time.unscaledTime;
            yield return MouseClick(p, right);
            yield return Settle();
            var heard = Heard(t0);
            bool flags = fort.RepeatProduction == wantR && fort.RepeatQueue == wantQ, lit = pw.RepeatLitShown == wantLit;
            bool sound = cue == null ? heard.Count == 0 : heard.Count == 1 && heard.Contains(cue);
            bool ok = hov && flags && lit && sound;
            clicks++; if (!ok) clickBad++;
            lines.Add($"{(ok ? "OK   " : "FAIL ")} {(right ? "right" : "left")} click {OpenZProductionWindow.RepeatWords[mode]}{(note != null ? " (" + note + ")" : "")} with {Word(lit0)} lit "
                      + $"(REPEAT {OnOff(r0)}, REPEAT QUEUE {OnOff(q0)}){(live && mode == lit0 ? ", the lit tile" : "")}: under the pointer {(hov ? "yes" : "NO")}, "
                      + $"REPEAT {OnOff(fort.RepeatProduction)}, REPEAT QUEUE {OnOff(fort.RepeatQueue)} (want {OnOff(wantR)} / {OnOff(wantQ)}), lit {Word(pw.RepeatLitShown)} "
                      + $"(want {Word(wantLit)}), cues {CueList(heard)} (want {cue ?? "none"})");
        }

        /// <summary>The check's own mouse (OzRepeatSwitchMouse) on, the editor's mouse off meanwhile; off: the editor's mouse back (also at the
        /// end, when a step threw). The HUD reads Mouse.current, so it sees only the check's mouse while it is on.</summary>
        void Devices(bool on)
        {
            if (on && mouse == null)
            {
                sysMouse = Mouse.current;
                mouse = InputSystem.AddDevice<Mouse>("OzRepeatSwitchMouse");
                if (sysMouse != null && sysMouse != mouse) InputSystem.DisableDevice(sysMouse);
                mouse.MakeCurrent();
            }
            else if (!on && mouse != null)
            {
                if (sysMouse != null) { InputSystem.EnableDevice(sysMouse); sysMouse.MakeCurrent(); }
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                mouse = null; sysMouse = null;
            }
        }

        /// <summary>The check's mouse: buttons 0 none, 1 left, 2 right, at p (screen pixels), as a device reports it.</summary>
        void MouseEv(Vector2 p, int buttons)
        {
            if (mouse == null) return;
            var st = new MouseState { position = p };
            if ((buttons & 1) != 0) st = st.WithButton(MouseButton.Left);
            if ((buttons & 2) != 0) st = st.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, st);
        }

        /// <summary>The pointer moved to p and held there a few frames (OpenZHud's hover follows).</summary>
        IEnumerator MouseAt(Vector2 p) { MouseEv(p, 0); for (int f = 0; f < 4; f++) yield return null; }

        /// <summary>A press and a release at p (left, or right), a few frames each, as a hand makes them.</summary>
        IEnumerator MouseClick(Vector2 p, bool right)
        {
            MouseEv(p, right ? 2 : 1); for (int f = 0; f < 3; f++) yield return null;
            MouseEv(p, 0); for (int f = 0; f < 3; f++) yield return null;
        }

        /// <summary>The cues of <see cref="CueKeys"/> asked for since t0 (the sound log; played or dropped, the code asked for them).</summary>
        static HashSet<string> Heard(float t0) =>
            new HashSet<string>(OzSoundLog.Since(t0).Where(e => e.key != null && System.Array.IndexOf(CueKeys, e.key) >= 0).Select(e => e.key));
        static string CueList(HashSet<string> h) => h.Count == 0 ? "none" : string.Join(" + ", h.OrderBy(x => x));
        static string OnOff(bool on) => on ? "on" : "off";
        static string Word(int lit) => lit >= 0 && lit < OpenZProductionWindow.RepeatWords.Length ? OpenZProductionWindow.RepeatWords[lit] : lit.ToString();
        /// <summary>One rally point in front of the fort (a MOVE waypoint, as the player's).</summary>
        ZWaypoint RallyPoint() => new ZWaypoint { mode = WpMode.Move, refId = -1, x = fort.CenterX, y = fort.CenterY + 96, attackTo = true };

        IEnumerator Picture(string file, string what)
        {
            if (string.IsNullOrEmpty(Folder)) yield break;
            yield return new WaitForEndOfFrame();
            if (Cut(pw.LayoutProbe(), Path.Combine(Folder, file), pw.UnitPx)) pics.Add((file, what));
        }

        /// <summary>One counted layout: the counts, the states of the switch in `mode` with `n` filled slots, and every part's place.</summary>
        bool Layout(string at, int mode, int n, bool allLive)
        {
            var parts = pw.LayoutProbe();
            var r = Count(parts, pw.UnitPx, out int ov, out int outs, out int wd, out bool onScreen);
            overlaps += ov; outside += outs; words += wd; if (!onScreen) offScreen++;
            string why = States(parts, mode, n, allLive);
            bool st = why == null; if (!st) states++;
            string where = Places(parts, mode, n);
            bool pl = where == null; if (!pl) places++;
            bool ok = ov + outs + wd == 0 && onScreen && st && pl;
            lines.Add($"{(ok ? "OK   " : "FAIL ")} {at}: {parts.Count} parts, menu {UnitW(parts, "window")}, {pw.UnitPx:0.000} px/u, overlaps {ov}, kit parts outside {outs}, "
                      + $"words outside {wd}, on screen {(onScreen ? "yes" : "NO")}, states {(st ? "right" : "WRONG: " + why)}, places {(pl ? "right" : "WRONG: " + where)}{r}");
            if (first == null) first = Dump(parts, pw.UnitPx);
            return ok;
        }

        /// <summary>The switch's states in `mode`, null when all are right.</summary>
        string States(List<OpenZProductionWindow.LayoutPart> parts, int mode, int n, bool allLive)
        {
            var bad = new List<string>();
            if (!pw.RepeatSwitchShown) bad.Add("the switch hidden");
            if (pw.ClassicRowShown) bad.Add("1.3's REPEAT shown");
            if (pw.RepeatLitShown != mode) bad.Add("lit " + pw.RepeatLitShown + " (want " + mode + ")");
            if (pw.RepeatTileLive(OpenZProductionWindow.RepeatAll) != allLive) bad.Add("ALL " + (allLive ? "disabled" : "live"));
            if (!pw.RepeatTileLive(OpenZProductionWindow.RepeatOff) || !pw.RepeatTileLive(OpenZProductionWindow.RepeatLast)) bad.Add("OFF / LAST disabled");
            if (!parts.Any(p => p.name == "REPEAT caption")) bad.Add("no caption");
            foreach (var w in OpenZProductionWindow.RepeatWords)
                if (!parts.Any(p => p.name == "REPEAT " + w + " tile") || !parts.Any(p => p.name == w + " word") || !parts.Any(p => p.name == w + " glyph")) bad.Add("tile " + w + " incomplete");
            bool lastLit = mode == OpenZProductionWindow.RepeatLast, allLit = mode == OpenZProductionWindow.RepeatAll;
            if (pw.RepeatBadgeShown != (lastLit && n > 0)) bad.Add("badge " + (pw.RepeatBadgeShown ? "shown" : "hidden"));
            if (lastLit && pw.RepeatBadgeSlot != n - 1) bad.Add("badge on slot " + (pw.RepeatBadgeSlot + 1) + " (want " + n + ")");
            if (pw.RepeatRailShown != (allLit && n >= 2)) bad.Add("rail " + (pw.RepeatRailShown ? "shown" : "hidden"));
            string tag = pw.SlotTag(n - 1);
            if (n >= 2 && (tag == "REPEAT") != lastLit) bad.Add("slot " + n + " tag '" + tag + "'");
            if (pw.SlotTag(0) != "NEXT") bad.Add("slot 1 tag '" + pw.SlotTag(0) + "'");
            if (!pw.ClearIsX) bad.Add("CLEAR not the X");
            if (pw.ClearTip != OpenZProductionWindow.ClearRallyTip) bad.Add("X tooltip");
            return bad.Count == 0 ? null : string.Join(", ", bad);
        }

        /// <summary>Every part of the switch against its own number in OzRepeatSwitchLayout (window units: x from the menu's left, y down from the
        /// QUEUE row's top), null when all are right: the caption, the hint, the three tiles, their glyphs and words, the LAST "1", the badge and
        /// its shade on the last filled slot (LAST), the rail's five pieces (ALL), SET RALLY, the X and its glyph. Boxes within SizeTolU; texts by
        /// TextMeshPro's drawn box (<see cref="TextEm"/>): where it starts (left-aligned), ends (right-aligned) or its middle (centred: half a
        /// letter spacing right of the given x), its caps' middle, and its height at the full size (a shrunk text fails).</summary>
        string Places(List<OpenZProductionWindow.LayoutPart> parts, int mode, int n)
        {
            var at = new Where(parts, pw.UnitPx, pw.QueueRowScreen);
            float qy = OzLayout.ProdQueueSlotY, g = OzRepeatSwitchLayout.GlyphSize;
            at.TextL("REPEAT caption", OzRepeatSwitchLayout.CaptionX, 0f);
            at.TextL("queue hint", OzRepeatSwitchLayout.HintX, 0f);
            for (int i = 0; i < 3; i++)
            {
                string w = OpenZProductionWindow.RepeatWords[i]; var t = OzRepeatSwitchLayout.Tile(i);
                at.Box("REPEAT " + w + " tile", t.x, t.y, t.w, t.h);
                at.Box(w + " glyph", t.x + (t.w - g) * 0.5f, t.y + OzRepeatSwitchLayout.GlyphMidY - g * 0.5f, g, g);
                at.TextC(w + " word", t.x + t.w * 0.5f, t.y + OzRepeatSwitchLayout.WordMidY, OzRepeatSwitchLayout.WordTracking);
            }
            var tl = OzRepeatSwitchLayout.Tile(OpenZProductionWindow.RepeatLast);
            at.TextC("LAST 1", tl.x + tl.w * 0.5f, tl.y + OzRepeatSwitchLayout.GlyphMidY, 0f);
            if (mode == OpenZProductionWindow.RepeatLast && n > 0)
            {   // the badge: centred 45 u right of and 6 u under the last filled slot's top-left
                float cx = OzRepeatSwitchLayout.SlotX(n - 1) + OzRepeatSwitchLayout.BadgeDX, cy = qy + OzRepeatSwitchLayout.BadgeDY;
                float s = OzRepeatSwitchLayout.ShadeSize, k = OzRepeatSwitchLayout.BadgeSize;
                at.Box("repeat badge shade", cx - s * 0.5f, cy - s * 0.5f, s, s);
                at.Box("repeat badge", cx - k * 0.5f, cy - k * 0.5f, k, k);
            }
            if (mode == OpenZProductionWindow.RepeatAll && n >= 2)
            {   // the rail: down from the last filled slot's centre, along under the slots, up to the first slot's centre, the arrow head; 2 u
                // thick, never under RailMinPx pixels (OzRepeatSwitchLayout.RailWidth at the menu's measured pixels per unit)
                var rail = pw.RepeatRailScreen();
                float px = pw.UnitPx, h = OzLayout.ProdQueueSize * 0.5f, w = OzRepeatSwitchLayout.RailWidth(px), hw = w * 0.5f, y = OzRepeatSwitchLayout.RailY;
                float xa = OzRepeatSwitchLayout.SlotX(n - 1) + h, xb = OzRepeatSwitchLayout.SlotX(0) + h;
                float top = OzRepeatSwitchLayout.RailTopRight, topL = OzRepeatSwitchLayout.RailTopLeft, hx = OzRepeatSwitchLayout.HeadDX, hy = OzRepeatSwitchLayout.HeadDY;
                float tip = OzRepeatSwitchLayout.HeadY(w);
                var want = new[] { new Rect(xa - hw, top, w, y + hw - top), new Rect(xb - hw, y - hw, xa - xb + w, w), new Rect(xb - hw, topL, w, y + hw - topL),
                                   SegBox(new Vector2(xb - hx, tip + hy), new Vector2(xb, tip), w), SegBox(new Vector2(xb, tip), new Vector2(xb + hx, tip + hy), w) };
                string[] names = { "rail down from slot " + n, "rail along", "rail up to slot 1", "rail arrow head (left stroke)", "rail arrow head (right stroke)" };
                if (Mathf.Abs(pw.RepeatRailWidth - w) > 0.05f) at.bad.Add($"rail laid out {pw.RepeatRailWidth:0.00} u thick (want {w:0.00} u: 2 u, at least {OzRepeatSwitchLayout.RailMinPx:0} px at {px:0.000} px a unit)");
                if (rail.Count != want.Length) at.bad.Add("rail " + rail.Count + " pieces (want " + want.Length + ")");
                else
                {
                    for (int i = 0; i < want.Length; i++) at.BoxR(names[i], rail[i], want[i].x, want[i].y, want[i].width, want[i].height);
                    if (rail[0].width < OzRepeatSwitchLayout.RailMinPx - 0.05f || rail[1].height < OzRepeatSwitchLayout.RailMinPx - 0.05f)
                        at.bad.Add($"rail drawn {rail[0].width:0.00} / {rail[1].height:0.00} px thick (want at least {OzRepeatSwitchLayout.RailMinPx:0} px)");
                }
            }
            BottomRow(at);
            return at.Result;
        }

        /// <summary>'Zod exact': 1.3's QUEUE row at 1.3's places (OzLayout: the glyph 24 u at 311 / 30, REPEAT ON / OFF 98 x 32 at 340 in the
        /// slots' middle, the hint right-aligned at 438) and the kit's bottom row; null when all are right.</summary>
        string ClassicPlaces(List<OpenZProductionWindow.LayoutPart> parts)
        {
            var at = new Where(parts, pw.UnitPx, pw.QueueRowScreen);
            at.Box("repeat glyph (1.3)", OzLayout.ProdRepeatX, OzLayout.ProdRepeatDY, 24, 24);
            at.Box("REPEAT (1.3)", OzLayout.ProdRepeatBtnX, OzLayout.ProdQueueSlotY + (OzLayout.ProdQueueSize - OzLayout.ProdActionH) * 0.5f, OzLayout.ProdRepeatBtnW, OzLayout.ProdActionH);
            at.TextR("queue hint", OzLayout.ProdHintRX, 0f, HintTracking);
            BottomRow(at);
            return at.Result;
        }

        /// <summary>The bottom row, ProdActionDY under the QUEUE row's top: SET RALLY 208 x 32 at 15, the X 32 x 32 at 229, its glyph 16 u in its middle.</summary>
        static void BottomRow(Where at)
        {
            float dy = OzLayout.ProdActionDY, cg = OzRepeatSwitchLayout.ClearGlyph; var ra = OzRepeatSwitchLayout.Rally; var cx = OzRepeatSwitchLayout.ClearX;
            at.Box("SET RALLY", ra.x, dy + ra.y, ra.w, ra.h);
            at.Box("CLEAR RALLY X", cx.x, dy + cx.y, cx.w, cx.h);
            at.Box("CLEAR RALLY X glyph", cx.x + (cx.w - cg) * 0.5f, dy + cx.y + (cx.h - cg) * 0.5f, cg, cg);
        }

        /// <summary>The box a straight piece from a to b, w thick, covers (as OpenZProductionWindow.Seg turns it; window units, y down).</summary>
        static Rect SegBox(Vector2 a, Vector2 b, float w)
        {
            var d = b - a; float len = d.magnitude; var u = d / len; var c = (a + b) * 0.5f;
            float ex = Mathf.Abs(u.x) * len * 0.5f + Mathf.Abs(u.y) * w * 0.5f, ey = Mathf.Abs(u.y) * len * 0.5f + Mathf.Abs(u.x) * w * 0.5f;
            return Rect.MinMaxRect(c.x - ex, c.y - ey, c.x + ex, c.y + ey);
        }

        /// <summary>Parts measured against their numbers: window units, x from the menu's left, y down from the QUEUE row's top.</summary>
        sealed class Where
        {
            readonly List<OpenZProductionWindow.LayoutPart> parts; readonly float px; readonly Rect win, row;
            public readonly List<string> bad = new List<string>();
            public Where(List<OpenZProductionWindow.LayoutPart> parts, float px, Rect row)
            {
                this.parts = parts; this.px = px; this.row = row; win = parts.First(p => p.name == "window").r;
                if (row.height <= 0f) bad.Add("no QUEUE row");
            }
            public string Result => bad.Count == 0 ? null : string.Join(", ", bad);
            Rect U(Rect r) => Rect.MinMaxRect((r.xMin - win.xMin) / px, (row.yMax - r.yMax) / px, (r.xMax - win.xMin) / px, (row.yMax - r.yMin) / px);

            bool Find(string name, out Rect screen)
            {
                var p = parts.FirstOrDefault(q => q.name == name); screen = p.r;
                if (p.name == null) { bad.Add(name + " missing"); return false; }
                return true;
            }

            public void Box(string name, float x, float y, float w, float h) { if (Find(name, out var r)) BoxR(name, r, x, y, w, h); }

            public void BoxR(string name, Rect screen, float x, float y, float w, float h)
            {
                var u = U(screen);
                if (Mathf.Abs(u.xMin - x) > SizeTolU || Mathf.Abs(u.yMin - y) > SizeTolU || Mathf.Abs(u.width - w) > SizeTolU || Mathf.Abs(u.height - h) > SizeTolU)
                    bad.Add($"{name} at {u.xMin:0.0},{u.yMin:0.0} {u.width:0.0} x {u.height:0.0} u (want {x:0.0},{y:0.0} {w:0.0} x {h:0.0})");
            }

            /// <summary>A left-aligned text at x, its caps' top at y.</summary>
            public void TextL(string name, float x, float y) => Text(name, 'L', x, y + CapMidEm * TextSize);
            /// <summary>A right-aligned text ending at x (its drawn box runs on by one letter spacing), its caps' top at y.</summary>
            public void TextR(string name, float x, float y, float tracking) => Text(name, 'R', x + tracking * TextSize, y + CapMidEm * TextSize);
            /// <summary>A text centred on x (its drawn box sits half a letter spacing to the right), its caps' middle at mid.</summary>
            public void TextC(string name, float x, float mid, float tracking) => Text(name, 'C', x + tracking * TextSize * 0.5f, mid);

            void Text(string name, char how, float x, float mid)
            {
                if (!Find(name, out var r)) return;
                var u = U(r); float ux = how == 'L' ? u.xMin : how == 'R' ? u.xMax : u.center.x, h = TextEm * TextSize;
                if (Mathf.Abs(ux - x) > SizeTolU || Mathf.Abs(u.center.y - mid) > SizeTolU || Mathf.Abs(u.height - h) > SizeTolU)
                    bad.Add($"{name} {(how == 'L' ? "starts" : how == 'R' ? "ends" : "centred")} at x {ux:0.0}, caps' middle y {u.center.y:0.0}, {u.height:0.0} u tall "
                            + $"(want {x:0.0}, {mid:0.0}, {h:0.0}: size {TextSize:0})");
            }
        }

        /// <summary>The counts of one layout. Overlaps: a kit part against any other part (not the window, not a part it belongs to or that
        /// belongs to it). Outside: a kit part less than EdgeU u inside the menu. Words: a part that belongs to another one and is not inside it
        /// (its margin u from the left and right; parts drawn on top of another by design are not checked). Returns the first few findings.</summary>
        static string Count(List<OpenZProductionWindow.LayoutPart> parts, float px, out int overlaps, out int outside, out int words, out bool onScreen)
        {
            overlaps = outside = words = 0;
            var sb = new StringBuilder();
            var byName = new Dictionary<string, OpenZProductionWindow.LayoutPart>();
            foreach (var p in parts) byName[p.name] = p;
            var win = byName["window"].r;
            onScreen = win.xMin >= -0.5f && win.yMin >= -0.5f && win.xMax <= Screen.width + 0.5f && win.yMax <= Screen.height + 0.5f;
            var inner = Rect.MinMaxRect(win.xMin + EdgeU * px, win.yMin + EdgeU * px, win.xMax - EdgeU * px, win.yMax - EdgeU * px);
            for (int i = 0; i < parts.Count; i++)
            {
                var a = parts[i]; if (a.name == "window") continue;
                if (a.kit && !Inside(a.r, inner, 0f)) { outside++; if (sb.Length < 600) sb.Append($" | {a.name} outside the frame's inner edge: {R(a.r)} in {R(inner)}"); }
                if (a.owner != null && !a.onTop && byName.TryGetValue(a.owner, out var own))
                {
                    var box = Rect.MinMaxRect(own.r.xMin + a.margin * px, own.r.yMin, own.r.xMax - a.margin * px, own.r.yMax);
                    if (!Inside(a.r, box, TouchPx)) { words++; if (sb.Length < 600) sb.Append($" | {a.name} not inside {a.owner} ({a.margin:0} u each side): {R(a.r)} in {R(own.r)}"); }
                }
                for (int j = i + 1; j < parts.Count; j++)
                {
                    var b = parts[j]; if (b.name == "window") continue;
                    if (!a.kit && !b.kit) continue;
                    if (Owns(byName, a.name, b) || Owns(byName, b.name, a)) continue;
                    if (Shared(a.r, b.r) > TouchPx) { overlaps++; if (sb.Length < 600) sb.Append($" | {a.name} {R(a.r)} overlaps {b.name} {R(b.r)}"); }
                }
            }
            return sb.ToString();
        }

        /// <summary>`p` belongs to `name` (directly or through what it belongs to).</summary>
        static bool Owns(Dictionary<string, OpenZProductionWindow.LayoutPart> byName, string name, OpenZProductionWindow.LayoutPart p)
        {
            for (int k = 0; k < 8 && p.owner != null; k++)
            {
                if (p.owner == name) return true;
                if (!byName.TryGetValue(p.owner, out p)) return false;
            }
            return false;
        }

        static bool Inside(Rect a, Rect box, float tol) => a.xMin >= box.xMin - tol && a.yMin >= box.yMin - tol && a.xMax <= box.xMax + tol && a.yMax <= box.yMax + tol;
        static float Shared(Rect a, Rect b) => Mathf.Min(Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
        static string R(Rect r) => $"({r.xMin:0},{r.yMin:0} {r.width:0}x{r.height:0})";
        static string UnitW(List<OpenZProductionWindow.LayoutPart> parts, string name) { var p = parts.First(x => x.name == name); return $"{p.r.width:0}x{p.r.height:0} px"; }

        /// <summary>Every part in window units from the menu's top-left (the report's last block).</summary>
        static string Dump(List<OpenZProductionWindow.LayoutPart> parts, float px)
        {
            var win = parts.First(p => p.name == "window").r; var sb = new StringBuilder();
            foreach (var p in parts)
                sb.AppendLine($"  {p.name,-28} x {(p.r.xMin - win.xMin) / px,6:0.0} .. {(p.r.xMax - win.xMin) / px,6:0.0}   y {(win.yMax - p.r.yMax) / px,6:0.0} .. {(win.yMax - p.r.yMin) / px,6:0.0}"
                              + $"{(p.kit ? "   (kit)" : "")}{(p.text ? "   (drawn text)" : "")}{(p.owner != null ? "   in " + p.owner : "")}");
            return sb.ToString();
        }

        /// <summary>The menu cut out of the frame (12 u round it) as a picture.</summary>
        static bool Cut(List<OpenZProductionWindow.LayoutPart> parts, string file, float px)
        {
            try
            {
                var w = parts.First(p => p.name == "window").r; float m = 12f * px;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(w.xMin - m), 0, Screen.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(w.yMin - m), 0, Screen.height - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(w.xMax + m), x0 + 1, Screen.width), y1 = Mathf.Clamp(Mathf.CeilToInt(w.yMax + m), y0 + 1, Screen.height);
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                var cut = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGB24, false);
                cut.SetPixels(tex.GetPixels(x0, y0, x1 - x0, y1 - y0)); cut.Apply();
                File.WriteAllBytes(file, cut.EncodeToPNG());
                Destroy(tex); Destroy(cut);
                return true;
            }
            catch (System.Exception ex) { Debug.LogWarning(Tag + " picture failed: " + ex.Message); return false; }
        }
    }
}
#endif
