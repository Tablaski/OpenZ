// OpenZ (GPL-3.0) - version 1.4 kit fog_hover_leak: the counted checks of "what the fog hides is an empty spot" (no hover box, name, bar,
// squad frame, ENTER cursor or minimap dot on a unit, gun or building the fog hides). Editor only: nothing of it is in a build.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// name and health bar). Every reader asks <see cref="OzFogOfWar.Covers"/>, the fog's own test (a unit: the ground under its centre not
    /// explored, as the model hold-off HideCovered tests it; a building: its centre and four corners); false whenever the fog is not hiding.
    /// <list type="bullet">
    /// <item>Count (sources, no Play): the project's own sources, one row per reader: the overlay's hover, squad frame and squad bars, the
    /// always bars, the badges, the crate marks, the lava thermometers, the order / cursor hover, the fort gate zone, the minimap's dots, the
    /// cursor and the order sound (they read only the input's hover), the unit panel (only the overlay's hover member), the selection (drag
    /// box, double-click, select-all, groups: own units only), and that Covers is false whenever the fog is not hiding (outside MAYHEM).</item>
    /// <item>Play Count (in Play, a level as a human; MAYHEM, or any level: the fog is then switched on for this match the testing panel's
    /// way): every enemy and empty unit, gun and building the fog hides, the camera put over it (and back after), the pointer at 9 points of
    /// its pick box: the overlay's hover and the input's hover (with the fort gate) never answer a covered thing; the squads with members in
    /// the fog: no covered member in the frame or the bars; a lone covered robot: no frame (no thermometer); the minimap: dots = the units
    /// the fog does not hide (the radar rule as it is); a covered enemy fort's gate zone: never the fort. The control: visible enemies at
    /// their pick box's middle are hovered as today.</item>
    /// <item>Owner Frames (in Play, MAYHEM): the camera over a hidden enemy, 5 s for the pointer, a game frame; then a visible enemy, the same.</item>
    /// </list>
    /// </summary>
    public static class OzV14FogHoverLeakCount
    {
        const string Tag = "[OpenZ V1.4 fog hover leak]";
        static readonly string KitDir = Path.Combine(OpenZ.EditorTools.OpenZPaths.RepoRoot, "work", "staging", "v1_4", "kits", "fog_hover_leak", "proof", "play");   // repo-relative (public source: no machine paths)
        const float ProbeHeight = 70f;                                                   // the normal RTS height (m) the probes look from
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        static string Dir()
        {
            try { Directory.CreateDirectory(KitDir); return KitDir; }
            catch (Exception) { var d = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")); Directory.CreateDirectory(d); return d; }
        }

        static void Write(string file, string head)
        {
            string text = head + "\n" + sb + (fail == 0 ? "PASS" : "FAIL") + $" {pass}/{pass + fail}\n";
            string line = $"{Tag} {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} ({file})";
            try { var p = Path.Combine(Dir(), file); File.WriteAllText(p, text); line += " -> " + p.Replace("\\", "/"); }
            catch (Exception e) { line += " (report not written: " + e.Message + ")"; }
            if (fail == 0) Debug.Log(line + "\n" + text); else Debug.LogError(line + "\n" + text);
        }

        // ================================================================== sources (no Play)
        static string Src(string rel) { var p = Path.Combine(Application.dataPath, "OpenZ/Scripts", rel); return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : null; }

        /// <summary>The text of t from the first a to the next b after it (null when either is missing).</summary>
        static string Between(string t, string a, string b)
        {
            if (t == null) return null;
            int i = t.IndexOf(a, StringComparison.Ordinal); if (i < 0) return null;
            int j = t.IndexOf(b, i + a.Length, StringComparison.Ordinal); if (j < 0) return null;
            return t.Substring(i, j - i);
        }

        static bool Has(string t, string s) => t != null && t.IndexOf(s, StringComparison.Ordinal) >= 0;
        static int Count(string t, string s) { if (t == null) return -1; int n = 0, i = 0; while ((i = t.IndexOf(s, i, StringComparison.Ordinal)) >= 0) { n++; i += s.Length; } return n; }

        [MenuItem("OpenZ/V1.4/Fog Hover Leak: Count (sources, no Play)")]
        public static void CountSources()
        {
            pass = fail = 0; sb = new StringBuilder();
            string ov = Src("UI/OpenZUnitOverlay.cs"), th = Src("UI/OpenZUnitOverlay.Thermo.cs"), pin = Src("View/PlayerInput.cs"), gate = Src("View/PlayerInput.FortGate.cs"),
                   both = Src("View/PlayerInput.BothSides.cs"), mm = Src("UI/OpenZMinimap.cs"), cur = Src("UI/OpenZCursors.cs"), hud = Src("UI/OpenZHud.cs"),
                   aud = Src("View/AudioDirector.cs"), fog = Src("View/OzFogOfWar.cs"), ren = Src("View/OzFogOfWar.Render.cs"), fg = Src("View/OzFowGate.cs");
            const string Q = "OzFogOfWar.Covers(";
            // the overlay
            Check(Has(Between(ov, "ZObject HoverUnit(Camera cam, Vector2 mp)", "HoverMember = null;"), "if (OzFogOfWar.Covers(o)) continue;"),
                  "overlay hover (UI/OpenZUnitOverlay.cs HoverUnit): a covered unit is no hit (no box, name, bar; the unit panel's SQUAD MEMBER health reads this hover)");
            var ur = Between(ov, "bool UnitRect(Camera cam, ZObject u, out Rect r)", "foreach (var m in sqMembers)");
            Check(Has(ur, "!OzFogOfWar.Covers(u)) sqMembers.Add(u)") && Has(ur, "!OzFogOfWar.Covers(m)) sqMembers.Add(m)"),
                  "squad frame and far-member frames (UnitRect): covered members left out (also level 7's thermometers, which go through UnitRect)");
            Check(Has(th, "!UnitRect(cam, o, out var r)) continue;"), "lava thermometers (UI/OpenZUnitOverlay.Thermo.cs) skip a robot UnitRect gives no box");
            var ss = Between(ov, "bool SquadSplit(ZObject u)", "if (sqAll.Count < 2)");
            Check(Has(ss, "!OzFogOfWar.Covers(u)) sqAll.Add(u)") && Has(ss, "!OzFogOfWar.Covers(m)) sqAll.Add(m)"), "squad bars (SquadSplit / DrawSquadBars): covered members left out");
            Check(Has(Between(ov, "if (always)", "DrawLavaThermos("), "if (OzFogOfWar.Covers(o)) continue;"), "HEALTH BARS ON EVERY DAMAGED UNIT: a covered unit has none (fog_of_war)");
            Check(Has(Between(ov, "void DrawBadges(Camera cam, float sf)", "bool own = b.Owner == player;"), "if (OzFogOfWar.Covers(b)) continue;"), "production badges and level tags: none on a covered building (fog_of_war)");
            Check(Has(Between(ov, "if (itemMarkers)", "DrawOrderMarker("), "if (OzFogOfWar.Covers(o)) continue;"), "crate marks: none under the fog (fog_of_war)");
            Check(Has(ov, "ZObject hover = hud.PointerOverUi || PlayerInput.MatchDecided(W) ? null : HoverUnit(cam, hud.Pointer);"), "the overlay's hover box, name and bar come from HoverUnit alone");
            // the input: orders, cursor, sounds
            Check(Has(Between(pin, "ZObject HoverAt(Vector2 mp)", "return hu ?? h"), "if (OzFogOfWar.Covers(o)) continue;"), "order / cursor hover (View/PlayerInput.cs HoverAt): nothing covered is hovered or clicked (fog_of_war)");
            Check(Has(pin, "Hover = FortGateHover(HoverAt(mp), mp);"), "the input's hover = HoverAt, then the fort gate zone");
            Check(Has(Between(gate, "ZObject FortGateHover(ZObject h, Vector2 mp)", "bool FortEnterClick("), "if (!f.CanEnterFort(game.player) || OzFogOfWar.Covers(f)) continue;"),
                  "fort gate zone (View/PlayerInput.FortGate.cs): bare ground at a covered enemy fort's gate is not that fort (no ENTER cursor or ENTER_FORT order)");
            Check(Has(cur, "var h = input.Hover;") && !Has(cur, "W.Objects") && !Has(cur, "HoverMember"), "cursor (UI/OpenZCursors.cs, the attack cursor among them): reads only the input's hover");
            Check(Has(aud, "input.Hover != null && input.Hover.Owner != Player") && Has(cur, "OzSoundV3.CursorChanged(SoundName(previous), SoundName(c), hud.input.Hover)"),
                  "sounds (the order cue, the cursor-change sounds): read only the input's hover");
            Check(Has(Between(hud, "ZObject PanelHealthOf(ZObject u)", "CanvasGroup statsCg;"), "Overlay.HoverMember") && Count(hud, "HoverMember") == 1,
                  "unit panel: the only hover it reads is the overlay's hover member (covered never)");
            // selection: own units only, so nothing of another side can be picked by a box, a double-click, select-all or a group
            Check(Has(both, "OwnPickable(o, game.player)") && Has(pin, "static bool OwnPickable(ZObject o, Team me) => o.Owner == me"), "drag box and click select: own units only (Pickable / OwnPickable)");
            Check(Has(pin, "Selection.AddRange(W.OwnedSelectable(SideOf(pick))") && Has(pin, "Selection.AddRange(W.OwnedSelectable(game.player).Where(f));"), "double-click select-all-of-type and Ctrl select-all: own units only");
            Check(Has(pin, "Selection.RemoveAll(selPrune ??= o => o.Destroyed || !Mine(o.Owner)"), "the selection never holds another side's unit (pruned every frame)");
            // the minimap
            Check(Has(Between(mm, "HasRadar = radar;", "for (int i = bldUsed;"), "if (OzFogOfWar.Covers(o)) continue;"), "minimap (UI/OpenZMinimap.cs): no unit dot for a covered unit");
            // the fog's question: one test, off outside MAYHEM
            Check(Has(fog, "if (!hidingNow || o == null || o.Owner == playerNow) return false;"), "Covers is false whenever the fog is not hiding (every changed line does what it did)");
            Check(Has(fog, "if (forceSession == 0 && !game.IsMayhem) return"), "the fog runs in MAYHEM only (the testing panel aside)");
            Check(Has(Between(fog, "public static bool Covers(ZObject o)", "CoversTrue++;"), "if (m.ShownAtPx(o.CenterX, o.CenterY) >= OzFowMask.Revealed) return false;")
                  && Has(Between(ren, "void HideCovered(Camera cam)", "nv++;"), "if (mask.ShownAtPx(o.CenterX, o.CenterY) >= OzFowMask.Revealed) continue;"),
                  "a unit: Covers and the model hold-off (HideCovered) use the same test (the centre's cell, Revealed)");
            Check(Has(fg, "public static bool CoversAt(bool open, bool canDraw) => open && canDraw;") && Has(fg, "public static bool HoldsOff(bool open, bool canDraw) => open && canDraw;"),
                  "Covers answers at the same moments the models are held off (OzFowGate CoversAt = HoldsOff)");
            Info($"Covers asked: overlay {Count(ov, Q)}, input {Count(pin, Q)}, fort gate {Count(gate, Q)}, minimap {Count(mm, Q)}");
            Write("fog_hover_leak_count_sources.txt", $"{Tag} Count (sources, no Play), {DateTime.Now:dd MMM yyyy HH:mm}");
        }

        // ================================================================== Play
        static readonly BindingFlags BI = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly BindingFlags BS = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        static MethodInfo mHoverUnit, mUnitRect, mSquadSplit, mHoverProbe, mFortGateHover, mRefresh; static FieldInfo fSqMembers, fSqAll, fUnitUsed;

        static int Hooks()
        {
            var ot = typeof(OpenZUnitOverlay); var it = typeof(PlayerInput); var mt = typeof(OpenZMinimap);
            mHoverUnit = ot.GetMethod("HoverUnit", BI); mUnitRect = ot.GetMethod("UnitRect", BI); mSquadSplit = ot.GetMethod("SquadSplit", BI);
            fSqMembers = ot.GetField("sqMembers", BS); fSqAll = ot.GetField("sqAll", BS);
            mHoverProbe = it.GetMethod("HoverProbe", BI); mFortGateHover = it.GetMethod("FortGateHover", BI);
            mRefresh = mt.GetMethod("RefreshObjects", BI); fUnitUsed = mt.GetField("unitUsed", BI);
            int n = 0; foreach (var x in new MemberInfo[] { mHoverUnit, mUnitRect, mSquadSplit, fSqMembers, fSqAll, mHoverProbe, mFortGateHover, mRefresh, fUnitUsed }) if (x != null) n++;
            return n;
        }

        static Action step; static float waitUntil; static int waitFrames; static bool forcedOn;

        static void Pump()
        {
            if (!EditorApplication.isPlaying || step == null) { EditorApplication.update -= Pump; step = null; return; }
            try { step(); }
            catch (Exception e) { Debug.LogError($"{Tag} the check stopped: {e}"); EditorApplication.update -= Pump; step = null; }
        }

        static void Start(Action first) { step = first; EditorApplication.update -= Pump; EditorApplication.update += Pump; }
        static void Done() { step = null; EditorApplication.update -= Pump; }

        static bool Ready(out OpenZHud hud, out string why)
        {
            hud = OpenZHud.I; why = null;
            if (!EditorApplication.isPlaying) why = "press Play, start a level as a human, then run this again";
            else if (!hud || !hud.game || hud.game.World == null || !hud.input || !hud.rts || !hud.Overlay || !hud.Minimap) why = "no level with the HUD is playing";
            else if (!OzFogOfWar.I) why = "the level has no fog component";
            return why == null;
        }

        [MenuItem("OpenZ/V1.4/Fog Hover Leak: Play Count (in Play, a level as a human)")]
        public static void PlayCount()
        {
            if (!Ready(out var hud, out var why)) { Debug.LogWarning($"{Tag} {why}"); return; }
            if (step != null) { Debug.LogWarning($"{Tag} a run is going already"); return; }
            pass = fail = 0; sb = new StringBuilder(); forcedOn = false;
            int hooks = Hooks();
            Check(hooks == 9, $"the check's hooks found: {hooks}/9 (HoverUnit, UnitRect, SquadSplit, sqMembers, sqAll, HoverProbe, FortGateHover, RefreshObjects, unitUsed)");
            if (hooks != 9) { Write(ReportName(), Head()); return; }
            var W = hud.game.World;
            if (!OzFogOfWar.Hiding)
            {
                int asked = 0, yes = 0;
                foreach (var o in W.Objects) { asked++; if (OzFogOfWar.Covers(o)) yes++; }
                Check(yes == 0, $"the fog not hiding ({OzFogOfWar.GateReason ?? "-"}): Covers false for every object ({yes} of {asked} said yes): outside MAYHEM every changed line does what it did");
                if (!OzFogOfWar.Wanted)
                {
                    if (!OzFogOfWar.I.SetForced(true, out var refused)) { Info("the fog cannot be switched on here: " + refused); Write(ReportName(), Head()); return; }
                    forcedOn = true; Info("not MAYHEM: the fog switched ON for this match the testing panel's way (switched OFF again at the end)");
                }
            }
            else Info("the fog hides now (MAYHEM): the off row is the sources' count (Covers returns false first when the fog does not hide)");
            waitFrames = Time.frameCount + 600; waitUntil = Time.realtimeSinceStartup + 15f;
            Start(WaitHiding);
        }

        static string Head() => $"{Tag} Play Count, {DateTime.Now:dd MMM yyyy HH:mm}, scene {SceneManager.GetActiveScene().name}, player {(OpenZHud.I ? OpenZHud.I.Player.ToString() : "?")}, Game view {(OpenZHud.I && OpenZHud.I.rts ? OpenZHud.I.rts.GetComponent<Camera>().pixelWidth + "x" + OpenZHud.I.rts.GetComponent<Camera>().pixelHeight : "?")}";
        static string ReportName() => $"fog_hover_leak_play_{SceneManager.GetActiveScene().name}.txt";

        static void WaitHiding()
        {
            if (OzFogOfWar.Hiding) { step = null; Probe(); Done(); return; }
            if (Time.frameCount > waitFrames && Time.realtimeSinceStartup > waitUntil)
            {
                Check(false, $"the fog did not start hiding in 15 s ({OzFogOfWar.GateReason ?? "-"}): play the level (the RTS camera yours, no scene running) and run again");
                if (forcedOn) OzFogOfWar.I.SetForced(false, out _);
                Write(ReportName(), Head()); Done();
            }
        }

        static bool OnScreen(Camera cam, Vector2 p) => p.x >= 1f && p.y >= 1f && p.x <= cam.pixelWidth - 2 && p.y <= cam.pixelHeight - 2;

        static void Probe()
        {
            var hud = OpenZHud.I; var game = hud.game; var W = game.World; var me = hud.Player; var input = hud.input; var ov = hud.Overlay; var rts = hud.rts;
            var cam = rts.GetComponent<Camera>(); var mask = OzFogOfWar.Mask;
            Vector3 focus0 = rts.focus; float h0 = rts.height, t0 = rts.TargetHeight;
            var keepSel = new List<ZObject>(input.Selection);
            var covered = new List<ZObject>(); var visible = new List<ZObject>();
            foreach (var o in W.Objects)
            {
                if (o.Owner == me || o.Destroyed || o.KillAt >= 0 || !(o.IsUnit || (o.Kind == ZKind.Building && !o.IsBridge))) continue;
                if (OzFogOfWar.Covers(o)) covered.Add(o);
                else if (mask != null && mask.ShownAtPx(o.CenterX, o.CenterY) >= OzFowMask.Revealed && o.Owner != Team.Neutral) visible.Add(o);
            }
            int coveredUnits = 0; foreach (var o in covered) if (o.IsUnit) coveredUnits++;
            Info($"another side's units, guns and buildings: {covered.Count} hidden by the fog ({coveredUnits} units / guns), {visible.Count} enemies in explored ground");
            try
            {
                // ---- 1. the pointer on hidden things: the overlay's hover and the input's hover (orders, cursor, sounds; the fort gate)
                int probes = 0, targets = 0, off = 0, ovLeak = 0, inLeak = 0; var leakNames = new List<string>();
                int cap = 0;
                foreach (var o in covered)
                {
                    if (cap++ >= 80) break;
                    Aim(rts, game.CenterWorld(o));
                    var r = input.ScreenRect(o);
                    if (r.width <= 0f || !OnScreen(cam, r.center)) { off++; continue; }
                    targets++;
                    for (int gx = 1; gx <= 3; gx++)
                        for (int gy = 1; gy <= 3; gy++)
                        {
                            var mp = new Vector2(r.xMin + r.width * gx / 4f, r.yMin + r.height * gy / 4f);
                            if (!OnScreen(cam, mp)) continue;
                            probes++;
                            var hit = (ZObject)mHoverUnit.Invoke(ov, new object[] { cam, mp }); var pointed = ov.HoverMember ?? hit;
                            if (pointed != null && OzFogOfWar.Covers(pointed)) { ovLeak++; if (leakNames.Count < 8) leakNames.Add($"overlay {pointed.Type}#{pointed.Id}"); }
                            var ih = (ZObject)mHoverProbe.Invoke(input, new object[] { mp, false }); ih = (ZObject)mFortGateHover.Invoke(input, new object[] { ih, mp });
                            if (ih != null && OzFogOfWar.Covers(ih)) { inLeak++; if (leakNames.Count < 8) leakNames.Add($"input {ih.Type}#{ih.Id}"); }
                        }
                }
                Check(targets > 0, $"hidden things probed on screen: {targets} ({off} off the screen after the camera moved, e.g. under a map edge), {probes} pointer positions (9 a pick box)");
                Check(ovLeak == 0, $"overlay hover (box, name, bar, the unit panel's member health) on a hidden thing: {ovLeak} of {probes}" + (leakNames.Count > 0 ? " (" + string.Join(", ", leakNames) + ")" : ""));
                Check(inLeak == 0, $"input hover (orders, the attack / enter cursors, the cursor and order sounds, the fort gate) on a hidden thing: {inLeak} of {probes}");

                // ---- 2. the control: enemies in explored ground at their pick box's middle, as today
                int vT = 0, vOv = 0, vIn = 0, vUnits = 0; cap = 0;
                foreach (var o in visible)
                {
                    if (cap++ >= 40) break;
                    Aim(rts, game.CenterWorld(o));
                    var r = input.ScreenRect(o); if (r.width <= 0f || !OnScreen(cam, r.center)) continue;
                    vT++; if (o.IsUnit) vUnits++;
                    var hit = (ZObject)mHoverUnit.Invoke(ov, new object[] { cam, r.center }); var pointed = ov.HoverMember ?? hit;
                    if (o.IsUnit && pointed != null && (pointed == o || (pointed.Leader ?? pointed) == (o.Leader ?? o))) vOv++;
                    var ih = (ZObject)mHoverProbe.Invoke(input, new object[] { r.center, false }); ih = (ZObject)mFortGateHover.Invoke(input, new object[] { ih, r.center });
                    if (ih == o || (ih != null && o.IsUnit && (ih.Leader ?? ih) == (o.Leader ?? o))) vIn++;
                }
                if (vT == 0) Info("no enemy in explored ground on the map now: the control was not run (explore a little, or run it later in the match)");
                else Check((vUnits == 0 || vOv > 0) && vIn > 0, $"control, enemies in explored ground hovered as today at their pick box's middle: overlay {vOv} of {vUnits} units, input {vIn} of {vT} things (another unit in front may take a point)");

                // ---- 3. squads partly in the fog: no covered member in the frame, the far-member frames or the bars; a lone hidden robot: no frame
                int squads = 0, frameLeak = 0, barLeak = 0, whole = 0, wholeFramed = 0, lone = 0, loneFramed = 0;
                foreach (var u in W.Objects)
                {
                    if (u.Owner == me || u.Kind != ZKind.Robot || u.Destroyed || u.KillAt >= 0 || u.IsMinion) continue;
                    int all = 1, cov = OzFogOfWar.Covers(u) ? 1 : 0;
                    foreach (var m in u.Minions) if (m != null && !m.Destroyed && m.KillAt < 0) { all++; if (OzFogOfWar.Covers(m)) cov++; }
                    if (cov == 0) continue;
                    mUnitRect.Invoke(ov, new object[] { cam, u, null });
                    var list = (List<ZObject>)fSqMembers.GetValue(null);
                    int inFrame = 0; foreach (var m in list) if (OzFogOfWar.Covers(m)) frameLeak++; else inFrame++;
                    if (all == 1) { lone++; if (list.Count > 0) loneFramed++; continue; }
                    squads++;
                    if (cov == all) { whole++; if (list.Count > 0) wholeFramed++; }
                    mSquadSplit.Invoke(ov, new object[] { u });
                    foreach (var m in (List<ZObject>)fSqAll.GetValue(null)) if (OzFogOfWar.Covers(m)) barLeak++;
                }
                (typeof(OpenZUnitOverlay).GetField("farMembers", BS)?.GetValue(null) as List<ZObject>)?.Clear();   // review: UnitRect's far-member list left by the probe, so the next frame cannot draw it after a gun's or vehicle's early return
                Check(frameLeak == 0 && wholeFramed == 0 && loneFramed == 0, $"frames: covered members in a squad frame {frameLeak}; of {whole} squads wholly in the fog framed {wholeFramed}; of {lone} lone hidden robots framed {loneFramed} (no box, no lava thermometer)");
                Check(barLeak == 0, $"squad bars: covered members in the bars {barLeak} ({squads} enemy squads with members in the fog)");

                // ---- 4. the minimap's dots: the units the fog does not hide (the radar rule as it is)
                var mm = hud.Minimap; mRefresh.Invoke(mm, null);
                int used = (int)fUnitUsed.GetValue(mm), want = 0, coveredDots = 0; bool radar = OpenZMinimap.HasRadar; var sel = input.Selection;
                foreach (var o in W.Objects)
                {
                    if (!o.IsUnit || o.Destroyed || o.KillAt >= 0) continue;
                    if (!radar && o.Owner != me && o.Owner != Team.Neutral) continue;
                    if (OzFogOfWar.Covers(o)) { coveredDots++; continue; }
                    want++; if (o.Owner == me && sel.Contains(o.Leader ?? o)) want++;
                }
                Check(used == want, $"minimap: {used} unit marks drawn = {want} wanted (the units the fog does not hide + the selection rings; radar {(radar ? "yes" : "no")}); {coveredDots} hidden units got no dot");

                // ---- 5. the fort gate zone of a hidden enemy fort (a robot or vehicle of ours selected for it, put back after)
                ZObject mover = null;
                foreach (var o in W.Objects) if (o.Owner == me && !o.Destroyed && o.KillAt < 0 && (o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle) && o.CanMove && !o.IsMinion) { mover = o; break; }
                int forts = 0, gPts = 0, gLeak = 0, vForts = 0, vPts = 0, vHit = 0;
                if (mover != null)
                {
                    input.Selection.Clear(); input.Selection.Add(mover);
                    foreach (var f in W.Objects)
                    {
                        if (!f.IsFort || f.Destroyed || !f.CanEnterFort(me) || !f.GetCreationMovePoint(out int ex, out int ey)) continue;
                        bool cov = OzFogOfWar.Covers(f); if (cov) forts++; else vForts++;
                        Aim(rts, ZGame.PxToWorld(ex, ey));
                        for (int dx = -96; dx <= 96; dx += 12)
                            for (int dy = -96; dy <= 96; dy += 12)
                            {
                                int x = ex + dx, y = ey + dy; if (!PlayerInput.GroundGate(f, x, y)) continue;
                                var wp = ZGame.PxToWorld(x, y); wp.y = game.GroundY(wp.x, wp.z); if (float.IsNaN(wp.y)) continue;
                                var sp = cam.WorldToScreenPoint(wp); if (sp.z <= 0f || !OnScreen(cam, sp)) continue;
                                var ih = (ZObject)mHoverProbe.Invoke(input, new object[] { (Vector2)sp, false }); ih = (ZObject)mFortGateHover.Invoke(input, new object[] { ih, (Vector2)sp });
                                if (cov) { gPts++; if (ih == f) gLeak++; } else { vPts++; if (ih == f) vHit++; }
                            }
                    }
                }
                if (mover == null || forts == 0) Info($"fort gate: {(mover == null ? "no robot or vehicle of ours to select" : "no hidden enemy fort")} (the row needs a level with an enemy fort, e.g. L02, before its base is seen)");
                else Check(gLeak == 0, $"fort gate: {forts} hidden enemy fort(s), {gPts} points of their gate zones: the fort answered {gLeak} times (no ENTER cursor, no ENTER_FORT order)");
                if (vForts > 0) Info($"fort gate, forts in explored ground: {vHit} of {vPts} gate points answer the fort (as today)");
            }
            finally
            {
                input.Selection.Clear(); input.Selection.AddRange(keepSel);
                rts.focus = focus0; rts.SetHeightImmediate(h0); if (!Mathf.Approximately(t0, h0)) rts.ZoomTo(t0);
                if (forcedOn) { OzFogOfWar.I.SetForced(false, out _); Info("the fog switched OFF again for this match"); }
            }
            Write(ReportName(), Head());
        }

        static void Aim(RtsCamera rts, Vector3 at) { rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(ProbeHeight); }

        // ================================================================== the owner's frames
        static ZObject frameTarget; static int framePhase; static string frameDir; static readonly List<string> frameLog = new List<string>();
        static Vector3 frameFocus0; static float frameH0;
        static string frameShot; static float frameShotDeadline;                              // D2 check fix: the PNG asked for, still to be written

        [MenuItem("OpenZ/V1.4/Fog Hover Leak: Owner Frames (in Play, MAYHEM)")]
        public static void OwnerFrames()
        {
            if (!Ready(out var hud, out var why)) { Debug.LogWarning($"{Tag} {why}"); return; }
            if (step != null) { Debug.LogWarning($"{Tag} a run is going already"); return; }
            if (!OzFogOfWar.Hiding) { Debug.LogWarning($"{Tag} the fog is not hiding: play a MAYHEM level (the RTS camera yours), then run this again"); return; }
            frameDir = Path.Combine(Dir(), "owner_frames_" + SceneManager.GetActiveScene().name + "_" + DateTime.Now.ToString("HHmmss")); Directory.CreateDirectory(frameDir);
            frameLog.Clear(); framePhase = 0; frameFocus0 = hud.rts.focus; frameH0 = hud.rts.height; frameShot = null;   // D2 check fix: nothing pending at the start
            Start(FrameStep);
        }

        static ZObject Nearest(bool hidden)
        {
            var hud = OpenZHud.I; var W = hud.game.World; var f = hud.rts.focus; ZObject best = null; float bd = float.MaxValue; var mask = OzFogOfWar.Mask;
            foreach (var o in W.Objects)
            {
                if (o.Owner == hud.Player || o.Owner == Team.Neutral || !o.IsUnit || o.IsMinion || o.Destroyed || o.KillAt >= 0) continue;
                bool cov = OzFogOfWar.Covers(o);
                if (hidden != cov || (!hidden && (mask == null || mask.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed))) continue;
                var p = hud.game.CenterWorld(o); float d = (p.x - f.x) * (p.x - f.x) + (p.z - f.z) * (p.z - f.z);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        static void FrameStep()
        {
            var hud = OpenZHud.I; if (!hud || !hud.rts) { Done(); return; }
            if (frameShot != null)
            {   // D2 check fix: CaptureScreenshot writes at the end of a later frame, so the camera stays on the target until the PNG exists
                if (!File.Exists(frameShot) && Time.realtimeSinceStartup < frameShotDeadline) return;
                frameShot = null;
                if (framePhase > 3) { FrameEnd(); return; }
            }
            if (framePhase == 0 || framePhase == 2)
            {
                bool hidden = framePhase == 0;
                frameTarget = Nearest(hidden);
                if (frameTarget == null) { frameLog.Add($"no {(hidden ? "hidden" : "visible")} enemy unit now: frame {(hidden ? "A" : "B")} skipped"); framePhase += 2; if (framePhase > 2) FrameEnd(); return; }
                Aim(hud.rts, hud.game.CenterWorld(frameTarget));
                string what = hidden ? "A, a HIDDEN enemy" : "B, an enemy in explored ground";
                Debug.Log($"{Tag} frame {what}: {frameTarget.Type} #{frameTarget.Id} is in the middle of the screen. Put the pointer on it (the middle of the Game view) and keep it still: the frame is taken in 5 s.");
                waitUntil = Time.realtimeSinceStartup + 5f; framePhase++;
                return;
            }
            if (Time.realtimeSinceStartup < waitUntil) return;
            bool wasHidden = framePhase == 1;
            string file = Path.Combine(frameDir, wasHidden ? "A_hidden_enemy_under_the_pointer.png" : "B_visible_enemy_under_the_pointer.png");
            ScreenCapture.CaptureScreenshot(file);
            frameShot = file; frameShotDeadline = Time.realtimeSinceStartup + 3f;                // D2 check fix: wait for it before the next aim or the camera's put-back
            var ov = hud.Overlay;
            frameLog.Add($"frame {(wasHidden ? "A (hidden)" : "B (visible)")}: {frameTarget.Type} #{frameTarget.Id}, covered {OzFogOfWar.Covers(frameTarget)}; at the frame the overlay's hover was {(ov.HoverMember != null ? ov.HoverMember.Type + " #" + ov.HoverMember.Id : "nothing")}, "
                         + $"the hover bar showed {(OpenZUnitOverlay.HoverShownId >= 0 ? "#" + OpenZUnitOverlay.HoverShownId : "none")}, the input's hover {(hud.input.Hover != null ? hud.input.Hover.Type + " #" + hud.input.Hover.Id : "nothing")}, the cursor '{(hud.Cursors ? hud.Cursors.Current : "?")}' -> {file.Replace("\\", "/")}");
            framePhase++;
            if (framePhase > 3 && frameShot == null) FrameEnd();                                  // D2 check fix: with a PNG pending, the step above ends it once written
        }

        static void FrameEnd()
        {
            var hud = OpenZHud.I;
            if (hud && hud.rts) { hud.rts.focus = frameFocus0; hud.rts.SetHeightImmediate(frameH0); }
            Done();
            string text = $"{Tag} Owner Frames, {DateTime.Now:dd MMM yyyy HH:mm}, scene {SceneManager.GetActiveScene().name}\n" + string.Join("\n", frameLog) + "\n"
                          + "Wanted: A shows the cloud and NO box, name or bar, the plain move cursor (as on empty ground); B shows the enemy's box, name and bar (as today).\n";
            try { File.WriteAllText(Path.Combine(frameDir, "frames.txt"), text); } catch (Exception) { }
            Debug.Log(text);
        }
    }
}
