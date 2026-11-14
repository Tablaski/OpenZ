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
    /// 1.4 rally_minimap, the Play check with real clicks (menu OpenZ/V1.4/Rally Minimap: Play Check (a level playing, real clicks)). Editor only.
    /// A level playing in the normal mode, the player's fort standing, the Game view in front. For each Game view size of <see cref="Screens"/>
    /// (1920x1080, 1366x768; set through the editor's Game view sizes and put back), through the check's own mouse and keyboard (devices of their
    /// own; the editor's are off meanwhile and put back), so OpenZHud, the minimap and the rally tool decide as for a player:
    /// <list type="number">
    /// <item>The tool not armed (as 1.4): a left click on the minimap flies the camera there and sets nothing; a right click with nothing selected
    /// sets nothing.</item>
    /// <item>SET RALLY clicked in the fort's build menu (the menu closes, the tool armed, the minimap back). The pointer over the minimap: the
    /// map's armed cursor (move), the ghost mark drawn.</item>
    /// <item>Armed, a left click on the minimap: the fort's rally point there (within one screen pixel of the picture), the camera does not fly,
    /// the tool done, the toast RALLY POINT SET; its rally cues (sound log: every key with "rally" or "capture" in it and the RALLY toasts) the
    /// same as a left click of the armed tool on the ground of the map (made too, as the reference).</item>
    /// <item>Armed, a right click on the minimap with nothing selected: the point there, the same cues.</item>
    /// <item>Armed, one own unit selected, a right click on the minimap: that unit's move order there (as on the map), the rally points unchanged,
    /// the tool still armed.</item>
    /// <item>Armed, Shift held, three left clicks on the minimap: three unsent points (the minimap draws them), the rally points unchanged; Shift
    /// let go: the route of three, the toast RALLY ROUTE SET · 3 POINTS, the chained clicks' rally cue (ui_rally_point); a Shift route of two on the map
    /// gives the same cues.</item>
    /// <item>The tool not armed again: a left click flies, a right click with nothing selected sets nothing (nothing else changed).</item>
    /// </list>
    /// Frames for the owner (full frame and the minimap cut out, x2) at both sizes: armed with the pointer over the minimap, a Shift route being
    /// Put back at the end: the fort's rally points, the selection, the Game view size, the editor's mouse and keyboard. The one own unit ordered
    /// in step 5 keeps that order (the level is a test session).
    /// </summary>
    public sealed class OzRallyMinimapCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.4 rally minimap play]";
        public static string Report = "not run", Folder;
        public static bool Running;
        const float ToastCalmWait = 6.3f;                                                       // D2 check fix: OpenZHud.ToastCalm (6 s) + a margin, real time
        public static readonly Vector2Int[] Screens = { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) };

        OpenZHud hud; ZGame game; ZWorld W; Team me; ZObject fort; OpenZProductionWindow pw;
        Mouse mouse, sysMouse; Keyboard keys, sysKeys; bool shiftDown; Vector2 mousePos;
        int ok, bad;
        readonly List<string> lines = new List<string>();
        readonly List<string> pics = new List<string>();

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OZ_RallyMinimapCheck").AddComponent<OzRallyMinimapCheck>();
        }

        IEnumerator Start()
        {
            Running = true;
            try { yield return Main(); }
            finally { Devices(false); Running = false; Destroy(gameObject); }
        }

        void Check(bool good, string what) { if (good) ok++; else bad++; lines.Add((good ? "OK    " : "FAIL  ") + what); }

        IEnumerator Main()
        {
            game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); hud = FindFirstObjectByType<OpenZHud>();
            W = game ? game.World : null;
            if (!game || W == null || !hud || !hud.Production || !hud.Rally || !hud.Minimap) { Report = Tag + " FAIL: no level playing (ZGame, OpenZHud with its build menu, rally tool and minimap)"; Debug.LogWarning(Report); yield break; }
            pw = hud.Production; me = hud.Player;
            fort = W.Objects.FirstOrDefault(o => !o.Destroyed && o.Owner == me && o.IsFort && o.IsProducer) ?? W.Objects.FirstOrDefault(o => !o.Destroyed && o.Owner == me && o.IsProducer);
            if (fort == null) { Report = Tag + " FAIL: the player owns no fort or factory"; Debug.LogWarning(Report); yield break; }
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            var savedRally = fort.RallyPoints.ToList();
            var savedSel = hud.input.Selection.ToList();
            int savedView = OzFinalGateView.SelectedIndex();
            lines.Add($"{Tag} {System.DateTime.Now:dd MMM yyyy HH:mm}, level {game.name}, team {me}, the rally factory {fort.Type} #{fort.Id}");
            try
            {
                Devices(true);
                foreach (var scr in Screens)
                {
                    OzFinalGateView.SetFixed(scr.x, scr.y, "OpenZ check " + scr.x + "x" + scr.y, out string info);
                    yield return new WaitForSecondsRealtime(0.8f);
                    Check(Screen.width == scr.x && Screen.height == scr.y, $"Game view {Screen.width}x{Screen.height} (want {scr.x}x{scr.y}; {info})");
                    yield return OneScreen(scr);
                }
            }
            finally
            {
                if (shiftDown) KeyEv(false);
                hud.Rally.Disarm(); if (pw.IsOpen) pw.Close();
                hud.input.Selection.Clear(); hud.input.Selection.AddRange(savedSel.Where(o => o != null && !o.Destroyed));
                if (!fort.Destroyed) W.SetRallyPoints(me, fort, savedRally);
                OzFinalGateView.Select(savedView);
                Devices(false);
            }
            bool pass = bad == 0 && ok > 0;
            var sb = new StringBuilder();
            sb.AppendLine($"{Tag} {(pass ? "PASS" : "FAIL")}: {ok} OK, {bad} FAIL ({Screens.Length} screens)");
            foreach (var l in lines) sb.AppendLine(l);
            if (pics.Count > 0) { sb.AppendLine("frames for the owner:"); foreach (var p in pics) sb.AppendLine("  " + p); }
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder)) File.WriteAllText(Path.Combine(Folder, "report.txt"), Report);
            if (pass) Debug.Log(Report); else Debug.LogWarning(Report);
        }

        IEnumerator OneScreen(Vector2Int scr)
        {
            string at = scr.x + "x" + scr.y;
            yield return ResetState();
            int wpx = W.Map.WidthPix, hpx = W.Map.HeightPix;
            Vector2Int Clamp(int x, int y) => new Vector2Int(Mathf.Clamp(x, 48, wpx - 48), Mathf.Clamp(y, 48, hpx - 48));
            int fx = fort.CenterX, fy = fort.CenterY;
            var A = Clamp(fx + (fx < wpx / 2 ? 160 : -160), fy + (fy < hpx / 2 ? 120 : -120));
            var B = Clamp(fx + (fx < wpx / 2 ? 300 : -300), fy + (fy < hpx / 2 ? 40 : -40));
            var C = Clamp(fx + (fx < wpx / 2 ? 220 : -220), fy + (fy < hpx / 2 ? 260 : -260));
            var F = Clamp(fx < wpx / 2 ? wpx - 64 : 64, fy < hpx / 2 ? hpx - 64 : 64);           // far from the fort: the fly is plain to see
            float tol = TolPx();
            lines.Add($"-- {at}: points A {A}, B {B}, C {C}, far {F} (Zod px), minimap {MiniRect()} (screen px), tolerance {tol:0.#} Zod px (one screen pixel of the picture + 2)");

            // 1. the tool not armed: 1.4 as it was
            var cam0 = CamPos(); float t0 = Time.unscaledTime;
            yield return Click(Mini(F), false); yield return new WaitForSecondsRealtime(0.9f);
            Check(Vector3.Distance(cam0, CamPos()) > 2f && fort.RallyPoints.Count == 0 && RallyCues(t0).Count == 0,
                  $"{at} not armed, left click on the minimap: the camera flew {Vector3.Distance(cam0, CamPos()):0.0} m, rally points {fort.RallyPoints.Count} (want 0), rally cues {List(RallyCues(t0))}");
            t0 = Time.unscaledTime;
            yield return Click(Mini(B), true); yield return new WaitForSecondsRealtime(0.3f);
            Check(fort.RallyPoints.Count == 0 && RallyCues(t0).Count == 0, $"{at} not armed, nothing selected, right click on the minimap: rally points {fort.RallyPoints.Count} (want 0), rally cues {List(RallyCues(t0))}");

            // 2. SET RALLY clicked, the pointer over the minimap
            yield return ArmByClick(at);
            yield return MoveTo(Mini(A)); for (int f = 0; f < 4; f++) yield return null;
            Check(hud.Cursors && hud.Cursors.Current == "move" && hud.Minimap.RallyGhostShown,
                  $"{at} armed, the pointer over the minimap: cursor {(hud.Cursors ? hud.Cursors.Current : "none")} (want move, the map's armed cursor), ghost mark {(hud.Minimap.RallyGhostShown ? "drawn" : "NOT drawn")}");
            yield return Picture("owner_rally_minimap_armed_" + at);

            // 3. armed, left click on the minimap
            cam0 = CamPos(); t0 = Time.unscaledTime;
            yield return Click(Mini(A), false); yield return new WaitForSecondsRealtime(0.6f);
            var cuesMini = RallyCues(t0);
            Check(Points(A, tol) && !hud.Rally.Armed && Vector3.Distance(cam0, CamPos()) < 0.3f && HasToast(t0, "RALLY POINT SET"),
                  $"{at} armed, left click on the minimap: rally {Pts()} (want {A}), tool {(hud.Rally.Armed ? "STILL armed" : "done")}, camera moved {Vector3.Distance(cam0, CamPos()):0.00} m (want none), cues {List(cuesMini)}");

            // the reference: the armed tool's left click on the ground of the map
            yield return new WaitForSecondsRealtime(ToastCalmWait);                             // D2 check fix: the HUD shows one toast text at most once per 6 s (OpenZHud.ToastCalm)
            yield return ArmByClick(at);
            if (Ground(out var g, out var gpx))
            {
                t0 = Time.unscaledTime;
                yield return Click(g, false); yield return new WaitForSecondsRealtime(0.6f);
                var cuesMap = RallyCues(t0);
                Check(Points(gpx, 2f) && !hud.Rally.Armed, $"{at} the reference, armed, left click on the map at {g}: rally {Pts()} (want {gpx}), cues {List(cuesMap)}");
                Check(cuesMap.SetEquals(cuesMini), $"{at} the minimap's left click gives the map's cues: {List(cuesMini)} / {List(cuesMap)}");

                // 4. armed, nothing selected, right click on the minimap
                yield return new WaitForSecondsRealtime(ToastCalmWait);                         // D2 check fix: past the toast calm again, so RALLY POINT SET can show
                yield return ArmByClick(at);
                t0 = Time.unscaledTime;
                yield return Click(Mini(B), true); yield return new WaitForSecondsRealtime(0.6f);
                var cuesRight = RallyCues(t0);
                Check(Points(B, tol) && !hud.Rally.Armed && HasToast(t0, "RALLY POINT SET") && cuesRight.SetEquals(cuesMap),
                      $"{at} armed, nothing selected, right click on the minimap: rally {Pts()} (want {B}), tool {(hud.Rally.Armed ? "STILL armed" : "done")}, cues {List(cuesRight)} (the map's {List(cuesMap)})");
            }
            else Check(false, $"{at} no screen point on the ground of the map outside the HUD for the reference click");

            // 5. armed, one own unit selected, right click on the minimap: its order, the tool stays armed
            yield return ArmByClick(at);
            var u = W.Objects.FirstOrDefault(o => !o.Destroyed && o.Owner == me && o.Selectable && o.CanMove && !o.IsShuttle && !o.IsMinion && o.Kind == ZKind.Vehicle)
                 ?? W.Objects.FirstOrDefault(o => !o.Destroyed && o.Owner == me && o.Selectable && o.CanMove && !o.IsShuttle && !o.IsMinion);
            if (u != null)
            {
                var before = Pts();
                hud.input.Selection.Clear(); hud.input.Selection.Add(u);
                yield return null;
                yield return Click(Mini(C), true); yield return new WaitForSecondsRealtime(0.4f);
                ZWaypoint? wp = null;                                                             // its move order: a waypoint at C (the sim may have put path points in front)
                foreach (var w in u.Waypoints) if (Mathf.Abs(w.x - C.x) <= tol && Mathf.Abs(w.y - C.y) <= tol) wp = w;
                if (!wp.HasValue && u.Waypoints.Count > 0) wp = u.Waypoints[u.Waypoints.Count - 1];
                bool near = wp.HasValue && Mathf.Abs(wp.Value.x - C.x) <= tol && Mathf.Abs(wp.Value.y - C.y) <= tol;
                Check(near && Pts() == before && hud.Rally.Armed,
                      $"{at} armed, {u.Type} #{u.Id} selected, right click on the minimap: its order {(wp.HasValue ? "(" + wp.Value.x + "," + wp.Value.y + ")" : "none")} (want {C}), rally {Pts()} (want {before}, unchanged), tool {(hud.Rally.Armed ? "armed (as on the map)" : "NOT armed")}");
                hud.input.Selection.Clear();
            }
            else lines.Add($"INFO  {at} no own unit that moves: the selected-unit case left out");
            hud.Rally.Disarm();

            // 6. armed, Shift + three left clicks on the minimap: a route
            yield return ArmByClick(at);
            var set0 = Pts(); int set0n = fort.RallyPoints.Count; t0 = Time.unscaledTime;
            // review (6 Nov): the marks are counted against the same frame before the clicks (other factories' routes count too), so the fort's three
            // unsent points must show as exactly three marks in place of its sent ones; "3 or more" passed on 1.4 as it was with other routes on the map
            yield return MoveTo(Mini(F)); for (int f = 0; f < 4; f++) yield return null;
            int marks0 = hud.Minimap.RallyMarksDrawn;
            KeyEv(true); for (int f = 0; f < 3; f++) yield return null;
            foreach (var p in new[] { A, B, C }) { yield return Click(Mini(p), false); yield return new WaitForSecondsRealtime(0.15f); }
            yield return MoveTo(Mini(F)); for (int f = 0; f < 4; f++) yield return null;
            var pend = hud.Rally.PendingFor(fort);
            int marks = hud.Minimap.RallyMarksDrawn, wantMarks = marks0 - set0n + 3;
            Check(pend != null && pend.Count == 3 && Pts() == set0 && marks == wantMarks && hud.Minimap.RallyGhostShown,
                  $"{at} armed, Shift, three left clicks on the minimap: unsent {(pend == null ? 0 : pend.Count)} (want 3), rally {Pts()} (want {set0}, not sent yet), rally marks on the minimap {marks} (want {wantMarks} = {marks0} before - the fort's {set0n} sent + its 3 unsent), ghost {(hud.Minimap.RallyGhostShown ? "drawn" : "NOT drawn")}");
            yield return Picture("owner_rally_minimap_route_" + at);
            KeyEv(false); yield return new WaitForSecondsRealtime(0.6f);
            var cuesRoute = RallyCues(t0);
            Check(Points(new[] { A, B, C }, tol) && !hud.Rally.Armed && HasToast(t0, "RALLY ROUTE SET · 3 POINTS") && cuesRoute.Any(c => !c.StartsWith("toast") && c.Contains("rally")),
                  $"{at} Shift let go: rally {Pts()} (want {A} {B} {C}), the toast {(HasToast(t0, "RALLY ROUTE SET · 3 POINTS") ? "RALLY ROUTE SET · 3 POINTS" : "MISSING")}, cues {List(cuesRoute)}");
            yield return MoveTo(Mini(A)); for (int f = 0; f < 4; f++) yield return null;
            yield return Picture("owner_rally_minimap_set_" + at);
            // the map's Shift route of two, the reference for the cues
            yield return ArmByClick(at);
            if (Ground(out var g1, out _) && Ground(out var g2, out _, 1))
            {
                t0 = Time.unscaledTime;
                KeyEv(true); for (int f = 0; f < 3; f++) yield return null;
                yield return Click(g1, false); yield return new WaitForSecondsRealtime(0.15f);
                yield return Click(g2, false); yield return new WaitForSecondsRealtime(0.15f);
                KeyEv(false); yield return new WaitForSecondsRealtime(0.6f);
                var cuesMapRoute = RallyCues(t0);
                Check(fort.RallyPoints.Count == 2 && cuesMapRoute.Select(Kind).ToHashSet().SetEquals(cuesRoute.Select(Kind)),
                      $"{at} the reference, a Shift route of two on the map: rally points {fort.RallyPoints.Count} (want 2), cues {List(cuesMapRoute)} (the minimap's {List(cuesRoute)}; compared without the point count)");
            }
            hud.Rally.Disarm();

            // 7. the tool not armed again: 1.4 as it was
            W.SetRallyPoints(me, fort, new ZWaypoint[0]);
            cam0 = CamPos(); t0 = Time.unscaledTime;
            yield return Click(Mini(A), false); yield return new WaitForSecondsRealtime(0.9f);
            Check(Vector3.Distance(cam0, CamPos()) > 2f && fort.RallyPoints.Count == 0, $"{at} not armed again, left click on the minimap: the camera flew {Vector3.Distance(cam0, CamPos()):0.0} m, rally points {fort.RallyPoints.Count} (want 0)");
            t0 = Time.unscaledTime;
            yield return Click(Mini(C), true); yield return new WaitForSecondsRealtime(0.3f);
            Check(fort.RallyPoints.Count == 0 && RallyCues(t0).Count == 0, $"{at} not armed again, nothing selected, right click on the minimap: rally points {fort.RallyPoints.Count} (want 0), cues {List(RallyCues(t0))}");
        }

        // ------------------------------------------------------------------ steps
        IEnumerator ResetState()
        {
            if (shiftDown) KeyEv(false);
            hud.Rally.Disarm(); if (pw.IsOpen) pw.Close();
            hud.input.Selection.Clear();
            W.SetRallyPoints(me, fort, new ZWaypoint[0]);
            yield return MoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        /// <summary>The fort's build menu opened, its SET RALLY clicked with the check's mouse (OpenZHud's own button rule).</summary>
        IEnumerator ArmByClick(string at)
        {
            hud.Rally.Disarm(); hud.input.Selection.Clear();
            pw.Open(fort);
            yield return new WaitForSecondsRealtime(0.6f);
            var part = pw.LayoutProbe().FirstOrDefault(p => p.name == "SET RALLY");
            if (part.name == null) { Check(false, $"{at} SET RALLY not found in the open menu"); yield break; }
            float t0 = Time.unscaledTime;
            yield return Click(part.r.center, false);
            for (int f = 0; f < 3; f++) yield return null;
            bool good = hud.Rally.Armed && !pw.IsOpen && !hud.LowerPanelsHidden;
            if (!good) Check(false, $"{at} SET RALLY clicked at {part.r.center}: tool {(hud.Rally.Armed ? "armed" : "NOT armed")}, menu {(pw.IsOpen ? "STILL open" : "closed")}, minimap {(hud.LowerPanelsHidden ? "HIDDEN" : "shown")}");
            else ok++;
        }

        /// <summary>A screen point on the ground of the map, outside the HUD, and its Zod pixel (the n-th of a few candidates that work).</summary>
        bool Ground(out Vector2 screen, out Vector2Int px, int n = 0)
        {
            var cam = hud.rts ? hud.rts.GetComponent<Camera>() : Camera.main;
            float[][] c = { new[] { 0.5f, 0.55f }, new[] { 0.42f, 0.5f }, new[] { 0.58f, 0.5f }, new[] { 0.5f, 0.42f }, new[] { 0.36f, 0.6f }, new[] { 0.64f, 0.6f } };
            foreach (var k in c)
            {
                screen = new Vector2(Screen.width * k[0], Screen.height * k[1]);
                if (!cam || hud.IsOverUi(screen) || !game.GroundPoint(cam.ScreenPointToRay(screen), out var gp)) continue;
                px = ZGame.WorldToPx(gp); px.x = Mathf.Clamp(px.x, 0, W.Map.WidthPix - 1); px.y = Mathf.Clamp(px.y, 0, W.Map.HeightPix - 1);
                if (n-- == 0) return true;
            }
            screen = default; px = default; return false;
        }

        // ------------------------------------------------------------------ the minimap picture
        Rect MiniRect()
        {
            var c = new Vector3[4]; hud.Minimap.MapRect.GetWorldCorners(c);                       // the HUD is a screen-space overlay: world = screen pixels
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        /// <summary>The screen point of the minimap picture over Zod pixel p (OpenZMinimap.ScreenToPx the other way round).</summary>
        Vector2 Mini(Vector2Int p)
        {
            var r = MiniRect();
            return new Vector2(r.xMin + r.width * p.x / W.Map.WidthPix, r.yMin + r.height * (1f - (float)p.y / W.Map.HeightPix));
        }

        float TolPx() { var r = MiniRect(); return Mathf.Max(W.Map.WidthPix / Mathf.Max(1f, r.width), W.Map.HeightPix / Mathf.Max(1f, r.height)) + 2f; }

        bool Points(Vector2Int want, float tol) => Points(new[] { want }, tol);
        bool Points(Vector2Int[] want, float tol)
        {
            var l = fort.RallyPoints; if (l.Count != want.Length) return false;
            for (int i = 0; i < want.Length; i++) if (Mathf.Abs(l[i].x - want[i].x) > tol || Mathf.Abs(l[i].y - want[i].y) > tol) return false;
            return true;
        }
        string Pts() => fort.RallyPoints.Count == 0 ? "none" : string.Join(" ", fort.RallyPoints.Select(w => $"({w.x},{w.y})"));
        Vector3 CamPos() => hud.rts ? hud.rts.transform.position : Vector3.zero;

        // ------------------------------------------------------------------ cues (the sound log)
        static HashSet<string> RallyCues(float t0) => new HashSet<string>(OzSoundLog.Since(t0)
            .Where(e => e.key != null && (e.key.Contains("rally") || (e.key == "toast" && e.clip != null && e.clip.StartsWith("RALLY"))))   // D2 check fix: "capture" left out (CombatView's vehicle-capture sounds of the battle, no rally cue)
            .Select(e => e.key == "toast" ? "toast: " + e.clip : e.key));
        static bool HasToast(float t0, string text) => OzSoundLog.Since(t0).Any(e => e.key == "toast" && e.clip == text);
        /// <summary>A cue without the number of points of a route toast (the map's reference route has two points, the minimap's three).</summary>
        static string Kind(string cue) => cue.StartsWith("toast: RALLY ROUTE SET") ? "toast: RALLY ROUTE SET" : cue;
        static string List(HashSet<string> h) => h.Count == 0 ? "none" : string.Join(" + ", h.OrderBy(x => x));

        // ------------------------------------------------------------------ the check's own mouse and keyboard
        void Devices(bool on)
        {
            if (on && mouse == null)
            {
                sysMouse = Mouse.current; sysKeys = Keyboard.current;
                mouse = InputSystem.AddDevice<Mouse>("OzRallyMinimapMouse");
                keys = InputSystem.AddDevice<Keyboard>("OzRallyMinimapKeyboard");
                if (sysMouse != null && sysMouse != mouse) InputSystem.DisableDevice(sysMouse);
                if (sysKeys != null && sysKeys != keys) InputSystem.DisableDevice(sysKeys);
                mouse.MakeCurrent(); keys.MakeCurrent();
            }
            else if (!on && mouse != null)
            {
                if (sysMouse != null) { InputSystem.EnableDevice(sysMouse); sysMouse.MakeCurrent(); }
                if (sysKeys != null) { InputSystem.EnableDevice(sysKeys); sysKeys.MakeCurrent(); }
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
                mouse = null; keys = null; sysMouse = null; sysKeys = null; shiftDown = false;
            }
        }

        void MouseEv(Vector2 p, int buttons)
        {
            if (mouse == null) return;
            mousePos = p;
            var st = new MouseState { position = p };
            if ((buttons & 1) != 0) st = st.WithButton(MouseButton.Left);
            if ((buttons & 2) != 0) st = st.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, st);
        }

        void KeyEv(bool shift)
        {
            if (keys == null) return;
            shiftDown = shift;
            InputSystem.QueueStateEvent(keys, shift ? new KeyboardState(Key.LeftShift) : new KeyboardState());
        }

        IEnumerator MoveTo(Vector2 p) { MouseEv(p, 0); for (int f = 0; f < 3; f++) yield return null; }

        /// <summary>The pointer to p, a press and a release there (left or right), a few frames each, as a hand makes them.</summary>
        IEnumerator Click(Vector2 p, bool right)
        {
            MouseEv(p, 0); for (int f = 0; f < 3; f++) yield return null;
            MouseEv(p, right ? 2 : 1); for (int f = 0; f < 3; f++) yield return null;
            MouseEv(p, 0); for (int f = 0; f < 3; f++) yield return null;
        }

        // ------------------------------------------------------------------ frames for the owner
        IEnumerator Picture(string name)
        {
            if (string.IsNullOrEmpty(Folder)) yield break;
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), tex.EncodeToPNG()); pics.Add(name + ".png (the whole frame)");
                var r = MiniRect(); int pad = 24;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin) - pad, 0, tex.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin) - pad, 0, tex.height - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(r.xMax) + pad, x0 + 1, tex.width), y1 = Mathf.Clamp(Mathf.CeilToInt(r.yMax) + pad, y0 + 1, tex.height);
                int w = x1 - x0, h = y1 - y0;
                var src = tex.GetPixels(x0, y0, w, h);
                var big = new Texture2D(w * 2, h * 2, TextureFormat.RGBA32, false);
                var dst = new Color[w * 2 * h * 2];
                for (int y = 0; y < h * 2; y++) for (int x = 0; x < w * 2; x++) dst[y * w * 2 + x] = src[(y / 2) * w + x / 2];
                big.SetPixels(dst); big.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name + "_minimap_x2.png"), big.EncodeToPNG()); pics.Add(name + "_minimap_x2.png (the minimap cut out, twice the size)");
                Destroy(big);
            }
            catch (System.Exception e) { Check(false, "the owner's frame " + name + " not written: " + e.Message); }   // review (6 Nov): the frames are part of the task, a missing one is a FAIL
            finally { if (tex) Destroy(tex); }
        }
    }
}
#endif
