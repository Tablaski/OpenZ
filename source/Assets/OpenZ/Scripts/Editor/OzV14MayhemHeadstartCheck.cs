using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// <see cref="MenuPath"/>
    /// written to the Console and to Logs/OzV14MayhemHeadstartCheck.txt beside Assets. Nothing is saved: every PlayerPrefs key it sets is put back.
    ///  Edit mode or Play:
    ///   H1 the rule: with HEAD START saved ON, a MAYHEM match gets none (OpenZDifficulty.HeadStartFor), EASY / MEDIUM / HARD get it; saved OFF: none anywhere.
    ///   H2 the saved choice is kept: choosing MAYHEM leaves oz_headstart ON; back to HARD the rule gives it again.
    ///   H3 every start path reads the one rule: View/ZGame.cs has no head start block on the bare saved switch (all four read headStartNow =
    ///      HeadStartFor(ShownDifficulty)); the level-start screen's click and key H are refused on MAYHEM; the tooltip names MAYHEM.
    ///   P1 the testing panel's fog bar is not drawn (OzFogOfWar.PanelDrawn false; in Play no active CheatFogOfWar plate).
    ///   F1 the early band on a made-up reveal map: a unit 2 m outside explored ground is drawn early, one 5 m outside is held off, one inside explored is drawn.
    ///   K1 EvilZod's kill cap: 20 kills by him in 60 s of game time (evenly, and all in the first 2 s) -> at most 2 kill lines; a pause (game time standing
    ///      still) gives none; a new match's first kill speaks.
    ///  Play only:
    ///   M1 the level-start screen (the main menu, in Play): HEAD START ON saved; MAYHEM picked -> the row reads NOT ON MAYHEM / OFF, not live, a toggle
    ///      refused; back to HARD -> the saved ON shows again and the row is live. The difficulty and the switch put back.
    ///   M2 a level (in Play): on a MAYHEM match with HEAD START saved ON no head start vehicle was made (ZGame.HeadStartMade 0, refused logged); on another
    ///      difficulty with it ON the count is the map's (OpenZDifficulty.HeadStartCount). Run it on L02 and L17 on MAYHEM and on HARD. On MAYHEM with it
    ///      saved OFF the row is SKIP (nothing to refuse: save HEAD START ON and run again).
    ///   F2 the early band drawn (in Play, a MAYHEM match with the fog on): an other-side unit 1-3 m outside explored ground in the view (found, or one in
    ///      covered ground in the view placed there by revealing a small disc next to it). Over 12 frames of the player's camera: EarlyViews at least 1,
    ///      its renderers drawn (not forceRenderingOff) during the camera, their shadows Off during the camera and back to their own mode after it,
    ///      RestoreSurprises 0. FAIL when no such unit can be found or placed (never PASS without one).
    ///   K2 EvilZod's kill cap on the real path (in Play, this match): his kills EzKills, lines asked EzKillLinesAsked at most 1 + (kills - 1) / 3, no two
    ///      asks closer than 30 s of game time (EzKillAskTimes), asked + capped = kills. SKIP when he has made no kill yet.
    ///   K3 the kill line bag (in Play, the sound table loaded): 12 lines in a row, never the same line twice in a row while the event has 2 or more lines;
    ///      the live bag is put back afterwards (EzProofKillLines).
    /// </summary>
    public static class OzV14MayhemHeadstartCheck
    {
        public const string MenuPath = "OpenZ/V1.4/MAYHEM head start: Check (edit mode or Play)";
        static int pass, fail, skip; static StringBuilder sb;
        static void Row(string id, bool ok, string what) { if (ok) pass++; else fail++; Say((ok ? "PASS  " : "FAIL  ") + id + "  " + what); }
        static void Skip(string id, string what) { skip++; Say("SKIP  " + id + "  " + what); }
        static void Say(string s) { sb.AppendLine(s); Debug.Log("[OpenZ V1.4 MAYHEM head start] " + s); }

        static string Src(string rel) { var p = Path.Combine(Application.dataPath, "OpenZ/Scripts/" + rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }

        sealed class Prefs
        {
            readonly bool hadKey = PlayerPrefs.HasKey(OpenZDifficulty.Key), hadMay = PlayerPrefs.HasKey(OpenZDifficulty.MayhemKey), hadHead = PlayerPrefs.HasKey(OpenZDifficulty.HeadStartPref);
            readonly int key = PlayerPrefs.GetInt(OpenZDifficulty.Key, 0), may = PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0), head = PlayerPrefs.GetInt(OpenZDifficulty.HeadStartPref, 0);
            public void Back()
            {
                if (hadKey) PlayerPrefs.SetInt(OpenZDifficulty.Key, key); else PlayerPrefs.DeleteKey(OpenZDifficulty.Key);
                if (hadMay) PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, may); else PlayerPrefs.DeleteKey(OpenZDifficulty.MayhemKey);
                if (hadHead) PlayerPrefs.SetInt(OpenZDifficulty.HeadStartPref, head); else PlayerPrefs.DeleteKey(OpenZDifficulty.HeadStartPref);
                PlayerPrefs.Save();
            }
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            pass = fail = skip = 0; sb = new StringBuilder();
            Say($"1.4 kit mayhem_headstart check, {DateTime.Now:yyyy-MM-dd HH:mm:ss}, {(Application.isPlaying ? "in Play" : "edit mode")}");
            var keep = new Prefs();
            try { EditRows(); }
            finally { keep.Back(); }
            if (Application.isPlaying) { PlayRows(); return; }
            Finish();
        }

        static void EditRows()
        {
            // H1 / H2 the rule and the saved choice
            OpenZDifficulty.HeadStartToggle = true;
            bool may = !OpenZDifficulty.HeadStartFor(GameDifficulty.Mayhem), others = OpenZDifficulty.HeadStartFor(GameDifficulty.Easy) && OpenZDifficulty.HeadStartFor(GameDifficulty.Medium) && OpenZDifficulty.HeadStartFor(GameDifficulty.Hard);
            OpenZDifficulty.HeadStartToggle = false;
            bool offNone = !OpenZDifficulty.HeadStartFor(GameDifficulty.Easy) && !OpenZDifficulty.HeadStartFor(GameDifficulty.Hard) && !OpenZDifficulty.HeadStartFor(GameDifficulty.Mayhem);
            Row("H1", may && others && offNone, $"saved ON: MAYHEM gets none {may}, EASY / MEDIUM / HARD get it {others}; saved OFF: none anywhere {offNone}");
            OpenZDifficulty.HeadStartToggle = true;
            if (ZGame.I && ZGame.I.World != null) Skip("H2", "a match is running (choosing a difficulty would change its rules live): run it in edit mode or on the main menu");
            else
            {
                OpenZDifficulty.Choice = GameDifficulty.Mayhem;
                bool kept = OpenZDifficulty.HeadStartToggle && OpenZDifficulty.Choice == GameDifficulty.Mayhem && !OpenZDifficulty.HeadStartFor(OpenZDifficulty.Choice);
                OpenZDifficulty.Choice = GameDifficulty.Hard;
                bool back = OpenZDifficulty.HeadStartToggle && OpenZDifficulty.HeadStartFor(OpenZDifficulty.Choice);
                Row("H2", kept && back, $"MAYHEM chosen: the saved ON kept {kept} (none in the match); back to HARD: ON again {back}");
            }

            // H3 one rule for every start path
            string zg = Src("View/ZGame.cs"), mexe = Src("UI/OpenZMainMenu.MenuExe.cs"), menu = Src("UI/OpenZMainMenu.cs");
            int bare = 0, reads = 0;
            foreach (var line in zg.Split('\n'))
            {
                if (!line.Contains("&& level ==") && !line.Contains("OzVolcanic.Is(level) &&")) continue;
                if (line.Contains("OpenZDifficulty.HeadStartToggle")) bare++;
                if (line.Contains("headStartNow")) reads++;
            }
            bool rule = zg.Contains("bool headStartNow = OpenZDifficulty.HeadStartFor(ShownDifficulty);") && bare == 0 && reads == 4;
            bool screen = menu.Contains("if (OpenZDifficulty.Choice == GameDifficulty.Mayhem) { Sfx(\"ui_disabled\"); return; }") && mexe.Contains("headText.Set(OzShuttleStart.LockedRowMayhem)") && mexe.Contains("MAYHEM or OFF: none, the standard map start.");
            Row("H3", rule && screen, $"View/ZGame.cs: the four head start blocks read headStartNow ({reads} of 4), on the bare saved switch {bare}; the screen refuses the toggle on MAYHEM and shows \"{OzShuttleStart.LockedRowMayhem}\", the tooltip names MAYHEM {screen}");

            // P1 the testing panel's fog bar
            bool plate = Application.isPlaying && GameObject.Find("CheatFogOfWar") != null;
            Row("P1", !OzFogOfWar.PanelDrawn && !plate, $"the fog bar of the testing panel is not drawn: PanelDrawn {OzFogOfWar.PanelDrawn}{(Application.isPlaying ? $", an active CheatFogOfWar plate {plate}" : "")}");

            // F1 the early band on a made-up reveal map (1 m cells; an explored disc of 10 m round (100, 100) m)
            var m = new OzFowMask(200 * 8, 200 * 8);
            m.Stamp(100 * 8, 100 * 8, 10 * 8); m.ShowAll();
            int r = OzFogOfWar.EarlyDrawCells();                                                       // the fog's own band in cells (CellPx 8 = 1 m)
            // the explored edge (Shown >= 128) lies about 8.5 m from the middle (the 3-cell ramp from 10 m inwards)
            bool inside = m.ShownAtPx(104 * 8, 100 * 8) >= OzFowMask.Revealed;
            bool near = m.ShownAtPx(111 * 8, 100 * 8) < OzFowMask.Revealed && OzFogOfWar.NearRevealed(m, 111 * 8, 100 * 8, r);
            bool far = !OzFogOfWar.NearRevealed(m, 115 * 8, 100 * 8, r);
            Row("F1", OzFogOfWar.EarlyDrawM > 0f && r >= 1 && inside && near && far, $"band {OzFogOfWar.EarlyDrawM:0.0} m ({r} cells): explored ground drawn {inside}; 2-3 m outside it drawn early {near}; 6 m outside held off {far}");

            // K1 EvilZod's kill cap
            int Lines(IEnumerable<double> times, out string at)
            {
                double last = double.NegativeInfinity; int skipN = 0, n = 0; var s = new StringBuilder();
                foreach (var t in times) if (AudioDirector.EzKillGate(t, ref last, ref skipN)) { n++; s.Append(t.ToString("0.0")).Append(' '); }
                at = s.ToString().Trim(); return n;
            }
            var even = new List<double>(); for (int i = 0; i < 20; i++) even.Add(i * 3.0);
            var burst = new List<double>(); for (int i = 0; i < 20; i++) burst.Add(i * 0.1);
            var late = new List<double>(); for (int i = 0; i < 20; i++) late.Add(5.0 + i * 3.0);
            int a = Lines(even, out var ae), b = Lines(burst, out var be), c = Lines(late, out var ce);
            var paused = new List<double> { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 };                                // a pause: the game clock stands (any real time)
            int p = Lines(paused, out _);
            double l2 = double.NegativeInfinity; int s2 = 0; AudioDirector.EzKillGate(100, ref l2, ref s2); bool fresh = AudioDirector.EzKillGate(2, ref l2, ref s2);   // a new match (the clock from 0)
            Row("K1", a <= 2 && b <= 2 && c <= 2 && a >= 1 && p == 1 && fresh && AudioDirector.EzKillGapGameSec >= 30f && AudioDirector.EzKillEvery == 3,
                $"20 kills in 60 s: evenly {a} line(s) (at {ae} s), all in the first 2 s {b} (at {be} s), from 5 s {c} (at {ce} s); 6 kills while paused {p}; a new match's first kill speaks {fresh}; gap {AudioDirector.EzKillGapGameSec:0} s game time, then 1 kill in {AudioDirector.EzKillEvery}");
        }

        // ------------------------------------------------------------------ Play rows (stepped by the editor's update)
        static IEnumerator<float> routine; static double waitUntil;
        static void PlayRows()
        {
            routine = PlayRoutine(); waitUntil = 0;
            EditorApplication.update -= Step; EditorApplication.update += Step;
        }
        static void Step()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Step; Say("SKIP  the Play rows stopped: Play ended"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            bool more;
            try { more = routine.MoveNext(); }
            catch (Exception e) { more = false; Row("ERR", false, e.ToString()); }
            if (!more) { EditorApplication.update -= Step; Finish(); return; }
            waitUntil = EditorApplication.timeSinceStartup + routine.Current;
        }

        static IEnumerator<float> PlayRoutine()
        {
            var menu = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
            if (menu)
            {
                var keep = new Prefs();
                menu.ProofSelect(); yield return 1.2f;
                OpenZDifficulty.HeadStartToggle = true; OpenZDifficulty.Choice = GameDifficulty.Hard; yield return 0.4f;
                string hardText = menu.ProofHeadText; bool hardLive = menu.ProofHeadRowLive;
                OpenZDifficulty.Choice = GameDifficulty.Mayhem; yield return 0.4f;
                string mayText = menu.ProofHeadText; bool mayLive = menu.ProofHeadRowLive;
                menu.ProofToggleHeadStart(); yield return 0.25f;
                bool refused = OpenZDifficulty.HeadStartToggle;
                OpenZDifficulty.Choice = GameDifficulty.Hard; yield return 0.4f;
                string backText = menu.ProofHeadText; bool backLive = menu.ProofHeadRowLive;
                bool ok = hardLive && !hardText.StartsWith(OzShuttleStart.LockedRowMayhem, StringComparison.Ordinal)
                          && mayText.StartsWith(OzShuttleStart.LockedRowMayhem + " / ", StringComparison.Ordinal) && !mayLive && refused
                          && backLive && backText == hardText;
                Row("M1", ok, $"HEAD START saved ON: HARD \"{hardText}\" live {hardLive}; MAYHEM \"{mayText}\" live {mayLive}, a toggle refused (still ON) {refused}; back to HARD \"{backText}\" live {backLive}");
                keep.Back();
            }
            else Skip("M1", "no level-start screen (run it in Play on the main menu scene)");

            var g = ZGame.I;
            if (g && g.World != null)
            {
                int want = OpenZDifficulty.HeadStartCount(g.level);
                bool saved = OpenZDifficulty.HeadStartToggle;
                if (g.IsMayhem && !saved) Skip("M2", $"{g.level} MAYHEM with HEAD START saved OFF: nothing to refuse (made {ZGame.HeadStartMade}); save HEAD START ON on the level-start screen and run again");
                else if (g.IsMayhem) Row("M2", ZGame.HeadStartMade == 0 && ZGame.HeadStartRefusedMayhem,
                    $"{g.level} MAYHEM, HEAD START saved ON: head start vehicles made {ZGame.HeadStartMade} (want 0), refused and logged {ZGame.HeadStartRefusedMayhem}");
                else Row("M2", ZGame.HeadStartMade == (saved ? want : 0) && !ZGame.HeadStartRefusedMayhem,
                    $"{g.level} {OpenZDifficulty.Label(g.ShownDifficulty)}, HEAD START saved {(saved ? "ON" : "OFF")}: head start vehicles made {ZGame.HeadStartMade} (want {(saved ? want : 0)})");
            }
            else Skip("M2", "no level running (run it in Play on L02 and L17, on MAYHEM and on HARD, with HEAD START saved ON)");

            // F2 the early band drawn on real frames of the player's camera
            if (!(g && g.World != null)) Skip("F2", "no level running (run it in Play on a MAYHEM match, L02 or L17, with the fog faded in)");
            else if (!g.IsMayhem || !OzFogOfWar.Wanted || OzFogOfWar.Mask == null || OzFogOfWar.Strength < 0.999f)
                Skip("F2", $"{g.level}: the fog is not on and faded in (MAYHEM {g.IsMayhem}, fog wanted {OzFogOfWar.Wanted}, strength {OzFogOfWar.Strength:0.00}): run it on a MAYHEM match after the briefing");
            else
            {
                var mask = OzFogOfWar.Mask; int cells = OzFogOfWar.EarlyDrawCells(); var cam = F2Camera();
                UnitView pick = null; string how = "found";
                if (cam && cells >= 1)
                {
                    foreach (var v in g.LiveViewValues) if (F2Candidate(v, g, mask, cells, cam, true)) { pick = v; break; }
                    if (!pick)
                    {
                        // placed: a covered unit in the view gets a small explored disc (6 m) beside it whose explored edge (Shown >= Revealed, about 1.5 m inside
                        // the disc's rim: the 3-cell ramp) lies about 2 m from the unit's centre. The match's explored ground grows by that disc (a check run).
                        int tries = 0;
                        foreach (var v in g.LiveViewValues)
                        {
                            if (!F2Candidate(v, g, mask, cells, cam, false)) continue;
                            if (++tries > 3) break;                                             // at most 3 small discs revealed by the check
                            var o = v.Obj; int side = o.CenterX + 6 * 8 + 4 + 6 * 8 < mask.W * OzFowMask.CellPx ? 1 : -1;
                            mask.Stamp(o.CenterX + side * (6 * 8 + 4), o.CenterY, 6 * 8); mask.ShowAll();
                            if (F2Candidate(v, g, mask, cells, cam, true)) { pick = v; how = "placed (a 6 m explored disc beside it)"; break; }
                        }
                    }
                }
                if (!pick) Row("F2", false, $"{g.level}: no unit of another side in the view 1-{cells} m outside explored ground, and none in covered ground in the view to place there (camera {(cam ? cam.name : "none")}): drop an enemy or an empty vehicle in the fog in view with Alt+P and run again");
                else
                {
                    var o = pick.Obj;
                    f2R = new List<Renderer>(); pick.GetComponentsInChildren(true, f2R);
                    f2Own = new List<ShadowCastingMode>(f2R.Count); f2OwnOff = new List<bool>(f2R.Count);
                    foreach (var r in f2R) { f2Own.Add(r ? r.shadowCastingMode : ShadowCastingMode.Off); f2OwnOff.Add(!r || r.forceRenderingOff || !r.enabled); }
                    f2Frames = f2Band = f2Drawn = f2Off = f2Back = f2Early = 0; f2Obj = o; f2Cells = cells;
                    int surprises0 = OzFogOfWar.RestoreSurprises, shadowed = 0; for (int i = 0; i < f2R.Count; i++) if (!f2OwnOff[i] && f2Own[i] != ShadowCastingMode.Off) shadowed++;
                    int nR = f2R.Count;                                                                 // U1 (check only): F2Off() clears f2R before the row is written
                    try
                    {
                        RenderPipelineManager.beginCameraRendering += F2Begin; RenderPipelineManager.endCameraRendering += F2End;   // after the fog's own (it subscribed at its start): they see its BeginCamera's and EndCamera's result
                        double t0 = EditorApplication.timeSinceStartup;
                        while (f2Frames < 12 && EditorApplication.timeSinceStartup - t0 < 6.0) yield return 0.02f;
                    }
                    finally { F2Off(); }
                    int surprises = OzFogOfWar.RestoreSurprises - surprises0;
                    bool ok = f2Band >= 6 && f2Early == f2Band && f2Drawn == f2Band && f2Off == f2Band && f2Back == f2Frames && surprises == 0 && shadowed >= 1;
                    Row("F2", ok, $"{g.level} MAYHEM: {o.Type} #{o.Id} of {o.Owner} {how}, {nR} renderer(s), {shadowed} casting shadows; {f2Frames} camera frame(s), in the band (covered, within {cells} cells of explored) {f2Band} (at least 6): " +
                        $"EarlyViews >= 1 on {f2Early}, its renderers drawn on {f2Drawn}, its shadows Off during the camera on {f2Off}; after the camera its shadows back to its own on {f2Back} of {f2Frames}; RestoreSurprises {surprises}");
                }
            }

            // K2 EvilZod's kill cap on the real path (this match's counters)
            if (!(g && g.World != null)) Skip("K2", "no level running (run it in Play on a level where EvilZod fights)");
            else if (AudioDirector.EzKills == 0) Skip("K2", $"{g.level}: EvilZod has made no kill yet in this match: play on until he kills a few times and run again");
            else
            {
                int kills = AudioDirector.EzKills, asked = AudioDirector.EzKillLinesAsked, capped = AudioDirector.EzKillCapped;
                var at = AudioDirector.EzKillAskTimes; double minGap = double.PositiveInfinity;
                for (int i = 1; i < at.Count; i++) minGap = Math.Min(minGap, at[i] - at[i - 1]);
                int cap = 1 + (kills - 1) / AudioDirector.EzKillEvery;
                bool ok = asked >= 1 && asked <= cap && asked + capped == kills && (at.Count < 2 || minGap >= AudioDirector.EzKillGapGameSec - 1e-6);
                var ts = new StringBuilder(); foreach (var t in at) ts.Append(t.ToString("0.0")).Append(' ');
                Row("K2", ok, $"{g.level}: his kills {kills}, kill lines asked {asked} (at most 1 + ({kills} - 1) / {AudioDirector.EzKillEvery} = {cap}), kept quiet {capped}; asked at game time {ts.ToString().Trim()} s; " +
                    $"shortest gap {(at.Count < 2 ? "-" : minGap.ToString("0.0") + " s")} (at least {AudioDirector.EzKillGapGameSec:0} s)");
            }

            // K3 the kill line bag (the live bag put back afterwards)
            var ad = AudioDirector.I;
            int pool = ad ? ad.EzProofKillPool() : 0;
            if (ad && pool >= 1)
            {
                string prev = null; int twice = 0; var got = new List<string>();
                foreach (var l in ad.EzProofKillLines(12)) { got.Add(l ?? "(none)"); if (l != null && l == prev) twice++; prev = l; }
                Row("K3", pool < 2 || twice == 0, $"the kill event has {pool} distinct line(s); 12 in a row: the same line twice in a row {twice} time(s): {string.Join(" | ", got)}");
            }
            else Skip("K3", "no sound table loaded (run it in Play on a level)");
        }

        // ------------------------------------------------------------------ F2: the player's camera watched (render callbacks added after the fog's own)
        static List<Renderer> f2R; static List<ShadowCastingMode> f2Own; static List<bool> f2OwnOff; static ZObject f2Obj; static int f2Cells;
        static int f2Frames, f2Band, f2Drawn, f2Off, f2Back, f2Early;
        /// <summary>A camera the player plays through (as OzFogOfWar's own test: a game camera with an enabled RtsCamera, drawing to the screen or the main camera).</summary>
        static bool F2Player(Camera c) => c && c.cameraType == CameraType.Game && (c.targetTexture == null || c.CompareTag("MainCamera")) && c.TryGetComponent<RtsCamera>(out var r) && r.enabled;
        static Camera F2Camera() { foreach (var c in Camera.allCameras) if (F2Player(c)) return c; return null; }
        static bool F2InBand(ZObject o, OzFowMask m, int cells) => o != null && m.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed && OzFogOfWar.NearRevealed(m, o.CenterX, o.CenterY, cells);
        /// <summary>A unit of another side, alive, well inside the view; band true: in the early band now; false: in covered ground beyond it (to be placed).</summary>
        static bool F2Candidate(UnitView v, ZGame g, OzFowMask m, int cells, Camera cam, bool band)
        {
            var o = v ? v.Obj : null;
            if (o == null || o.Owner == g.player || !o.IsUnit || o.Destroyed || o.KillAt >= 0) return false;
            var p = cam.WorldToViewportPoint(v.transform.position);
            if (p.z <= 0f || p.x < 0.15f || p.x > 0.85f || p.y < 0.15f || p.y > 0.85f) return false;
            if (band) return F2InBand(o, m, cells);
            return m.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed && !OzFogOfWar.NearRevealed(m, o.CenterX, o.CenterY, cells);
        }
        static void F2Begin(ScriptableRenderContext ctx, Camera cam)
        {
            if (f2R == null || !F2Player(cam)) return;
            f2Frames++;
            var m = OzFogOfWar.Mask; if (m == null || !F2InBand(f2Obj, m, f2Cells)) return;
            f2Band++; if (OzFogOfWar.EarlyViews >= 1) f2Early++;
            bool drawn = true, off = true; int seen = 0;
            for (int i = 0; i < f2R.Count; i++)
            {
                var r = f2R[i]; if (!r || f2OwnOff[i]) continue;
                seen++; if (r.forceRenderingOff) drawn = false;
                if (f2Own[i] != ShadowCastingMode.Off && r.shadowCastingMode != ShadowCastingMode.Off) off = false;
            }
            if (drawn && seen > 0) f2Drawn++;
            if (off && seen > 0) f2Off++;
        }
        static void F2End(ScriptableRenderContext ctx, Camera cam)
        {
            if (f2R == null || !F2Player(cam)) return;
            bool back = true;
            for (int i = 0; i < f2R.Count; i++) { var r = f2R[i]; if (r && r.shadowCastingMode != f2Own[i]) back = false; }
            if (back) f2Back++;
        }
        static void F2Off() { RenderPipelineManager.beginCameraRendering -= F2Begin; RenderPipelineManager.endCameraRendering -= F2End; f2R = null; f2Obj = null; }

        static void Finish()
        {
            F2Off();                                                                                    // Play ended mid-row: the render callbacks taken off too
            Say($"[OpenZ V1.4 MAYHEM head start] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}{(skip > 0 ? $", {skip} skipped" : "")}");
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")); Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "OzV14MayhemHeadstartCheck.txt"), sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[OpenZ V1.4 MAYHEM head start] report not written: " + e.Message); }
        }
    }
}
