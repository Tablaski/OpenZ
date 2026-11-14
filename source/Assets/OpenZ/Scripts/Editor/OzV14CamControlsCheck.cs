using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.4 camera controls (UI/OzCamControls.cs): the counted check. Three menu items, each ends with one line
    /// "[OpenZ v1.4 camera controls] PASS n/n" (or FAIL with the failed rows) in the console and in Report.
    /// Every saved key it touches (oz_scroll_speed, oz_right_drag_scroll, oz_middle_alt, oz_free_camera) is put back as it was.
    ///
    /// 1 Count (no Play, no level): A1-A9 the rows' values, the carry-over of FREE CAMERA, the steps, the tab places and tips, the
    ///   middle-button rule, the right button's gesture (click / drag / not armed / back to the start / threshold), the CONTROLS
    ///   texts. B1 the default = before: a hidden RtsCamera driven through its own per-frame steps (FeelStep, MiddleDrag, WheelInput,
    ///   FlyStep, Place) by one input script, once with every hook of the kit switched off (OzCamControls.HooksOff: the lines as they
    ///   were) and once with the kit at its defaults: every frame's focus and height the same to the bit. B2 SCROLL SPEED 50 / 150 /
    ///   200 / 300 %: the keys' and the middle drag's distance that many times the 100 % one. B3 a drag in a turned view goes along
    ///   the heading only where the free look may hold that turn. B4 the camera's middle-button rule per mode.
    /// 2 Live input (Play, a level after the briefing, no menu open): a test mouse and a test keyboard (Input System devices made
    ///   current; click into the Game view when asked and leave the real mouse and keyboard alone) drive the game's own code:
    ///   R1 right drag with the row OFF: no scroll, the order is given (as before); R2 ON: the map moves, no order; R3 ON, a click:
    ///   the order, no scroll; R4 ON, a right press on the minimap: no scroll, the minimap's order as before; R5 ON, a drag from the
    ///   map that ends on the minimap: no minimap order. M1 TURN VIEW turns; M2 DRAG MAP moves the map; M3 the Alt way without Alt
    ///   moves the map; M4 with Alt it turns; M5 a middle drag in that turned view goes along its heading; M6 a right drag in a
    ///   turned view too. S1-S3 SCROLL SPEED 200 % doubles the arrow key scroll, the screen-edge scroll and the middle drag.
    ///   P1-P5 the pause menu: the GAME tab's rows, the slider (a press on the bar sets it, the row does not step with it; the right
    ///   arrow steps 10 %), the CONTROLS rows follow MIDDLE MOUSE and RIGHT-DRAG SCROLL.
    /// 3 Main menu rows (Play, scene MainMenu): N1 the GAME tab holds the three rows; N2 the slider shows the speed and a drag on it
    ///   sets it; N3 the CONTROLS table's camera and orders rows follow the rows.
    /// </summary>
    public static class OzV14CamControlsCheck
    {
        const string Tag = "[OpenZ v1.4 camera controls]";
        public static string Report = "not run";
        static int ok, bad; static string part = "";
        static readonly StringBuilder fails = new StringBuilder(), notes = new StringBuilder();
        static IEnumerator run; static int waitFrame;

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); }
            notes.Append(pass ? " [ok] " : " [FAIL] ").Append(what).Append(';');
            Debug.Log($"{Tag} {(pass ? "ok  " : "FAIL")} {what}");
        }

        // ------------------------------------------------------------------ the saved keys, put back at the end
        static readonly string[] Keys = { OzCamControls.SpeedPref, OzCamControls.RightPref, OzCamControls.AltPref, OzFreeCam.PrefKey };
        static readonly Dictionary<string, int> savedVal = new Dictionary<string, int>(); static readonly HashSet<string> savedHas = new HashSet<string>();
        static bool prefsHeld;
        static void HoldPrefs()
        {
            savedVal.Clear(); savedHas.Clear();
            foreach (var k in Keys) { if (PlayerPrefs.HasKey(k)) { savedHas.Add(k); savedVal[k] = PlayerPrefs.GetInt(k, 0); } }
            prefsHeld = true;
        }
        static void PutBackPrefs()
        {
            if (!prefsHeld) return;
            prefsHeld = false; OzCamControls.HooksOff = false;
            bool free = !savedHas.Contains(OzFreeCam.PrefKey) || savedVal[OzFreeCam.PrefKey] == 1;
            if (OzFreeCam.On != free) OzFreeCam.On = free;                                                    // the switch's own cache
            foreach (var k in Keys) { if (savedHas.Contains(k)) PlayerPrefs.SetInt(k, savedVal[k]); else PlayerPrefs.DeleteKey(k); }
            PlayerPrefs.Save(); OzCamControls.Reload();
        }
        /// <summary>Every row at its default (as a player who never set them).</summary>
        static void Defaults()
        {
            OzCamControls.HooksOff = false;
            if (!OzFreeCam.On) OzFreeCam.On = true;
            foreach (var k in new[] { OzCamControls.SpeedPref, OzCamControls.RightPref, OzCamControls.AltPref, OzFreeCam.PrefKey }) PlayerPrefs.DeleteKey(k);
            OzCamControls.Reload();
        }

        static void Begin(string what)
        {
            ok = bad = 0; fails.Clear(); notes.Clear(); part = what; Report = "running";
            OzRightDrag.ResetCounters(); HoldPrefs();
        }
        static void Finish()
        {
            PutBackPrefs();
            Report = $"{Tag} {part}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{(bad > 0 ? " FAILED:" + fails : "")}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
        }

        // ================================================================== 1 Count (no Play)
        [MenuItem("OpenZ/V1.4/Camera controls: 1 Count (no Play)")]
        public static void RunCount()
        {
            Begin("count");
            var made = new List<GameObject>();
            try { Values(); Paths(made); }
            catch (System.Exception ex) { Debug.LogException(ex); Check(false, "exception " + ex.Message); }
            finally { foreach (var go in made) if (go) Object.DestroyImmediate(go); Finish(); }
        }

        /// <summary>The ground a middle drag of px pixels moves the view at this camera's height (RtsCamera.MiddleDrag at 100 %).</summary>
        static float DragM(RtsCamera c, float px) => px * 2f * c.height * Mathf.Tan(16f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height) / Mathf.Sin(c.pitch * Mathf.Deg2Rad) * Mathf.Max(0.1f, c.middleDragFactor);

        static bool Bits1(float f) => System.BitConverter.SingleToInt32Bits(f) == System.BitConverter.SingleToInt32Bits(1f);

        static void Values()
        {
            // A1 never set: the defaults
            Defaults();
            Check(OzCamControls.ScrollPercent == 100 && Bits1(OzCamControls.ScrollFactor) && !OzCamControls.RightDrag && OzCamControls.MiddleMode == OzCamControls.Middle.TurnView && OzFreeCam.On,
                  $"A1 never set: SCROLL SPEED {OzCamControls.ScrollPercent} % (factor 1 to the bit: {Bits1(OzCamControls.ScrollFactor)}), RIGHT-DRAG SCROLL {(OzCamControls.RightDrag ? "ON" : "OFF")}, MIDDLE MOUSE {OzCamControls.MiddleName}, FREE CAMERA switch {(OzFreeCam.On ? "on" : "off")}");
            // A2 the saved FREE CAMERA switch carries over
            OzFreeCam.On = false; PlayerPrefs.DeleteKey(OzCamControls.AltPref); OzCamControls.Reload();
            var m0 = OzCamControls.MiddleMode;
            OzFreeCam.On = true; OzCamControls.Reload();
            var m1 = OzCamControls.MiddleMode;
            Check(m0 == OzCamControls.Middle.DragMap && m1 == OzCamControls.Middle.TurnView, $"A2 FREE CAMERA saved off reads {OzCamControls.MiddleNames[(int)m0]}, saved on reads {OzCamControls.MiddleNames[(int)m1]}");
            // A3 the MIDDLE MOUSE values and their keys, the steps round
            string Keys3() => $"oz_free_camera {PlayerPrefs.GetInt(OzFreeCam.PrefKey, -1)} {OzCamControls.AltPref} {PlayerPrefs.GetInt(OzCamControls.AltPref, -1)}";
            OzCamControls.MiddleMode = OzCamControls.Middle.DragMapAltTurns; string kAlt = Keys3(); bool aAlt = OzFreeCam.On && PlayerPrefs.GetInt(OzCamControls.AltPref, 0) == 1;
            OzCamControls.MiddleMode = OzCamControls.Middle.DragMap; string kDrag = Keys3(); bool aDrag = !OzFreeCam.On && PlayerPrefs.GetInt(OzCamControls.AltPref, 1) == 0;
            OzCamControls.MiddleMode = OzCamControls.Middle.TurnView; string kTurn = Keys3(); bool aTurn = OzFreeCam.On && PlayerPrefs.GetInt(OzCamControls.AltPref, 1) == 0;
            var seq = new StringBuilder(); for (int i = 0; i < 3; i++) { OzCamControls.MiddleStep(1); seq.Append(OzCamControls.MiddleName).Append(" > "); }
            OzCamControls.MiddleStep(-1); seq.Append("back: ").Append(OzCamControls.MiddleName);
            Check(aAlt && aDrag && aTurn && seq.ToString() == "DRAG MAP > DRAG MAP, ALT TURNS VIEW > TURN VIEW > back: DRAG MAP, ALT TURNS VIEW",
                  $"A3 MIDDLE MOUSE keys: the Alt way {kAlt}, DRAG MAP {kDrag}, TURN VIEW {kTurn}; steps {seq}");
            OzCamControls.MiddleMode = OzCamControls.Middle.TurnView;
            // A4 SCROLL SPEED: steps, ends, saved values off the steps, the slider's places
            OzCamControls.ScrollPercent = 300; OzCamControls.SpeedStep(1); int top = OzCamControls.ScrollPercent;
            OzCamControls.ScrollPercent = 50; OzCamControls.SpeedStep(-1); int low = OzCamControls.ScrollPercent;
            OzCamControls.SpeedStep(1); int up = OzCamControls.ScrollPercent;
            OzCamControls.SetSpeed(1000, true); int clamp = OzCamControls.ScrollPercent;
            PlayerPrefs.SetInt(OzCamControls.SpeedPref, 55); OzCamControls.Reload(); int odd = OzCamControls.ScrollPercent;
            PlayerPrefs.SetInt(OzCamControls.SpeedPref, 250); OzCamControls.Reload(); int kept = OzCamControls.ScrollPercent;
            int f0 = OzCamControls.SpeedFromFraction(0f), f1 = OzCamControls.SpeedFromFraction(1f), f2 = OzCamControls.SpeedFromFraction(0.2f), f4 = OzCamControls.SpeedFromFraction(0.4f);
            Check(top == 300 && low == 50 && up == 60 && clamp == 300 && odd == 100 && kept == 250 && f0 == 50 && f1 == 300 && f2 == 100 && f4 == 150 && Mathf.Abs(OzCamControls.FractionOf(100) - 0.2f) < 1e-6f,
                  $"A4 SCROLL SPEED steps: 300 +1 = {top}, 50 -1 = {low}, then +1 = {up}; 1000 asked = {clamp}; saved 55 reads {odd}, 250 reads {kept}; the bar 0 / 0.2 / 0.4 / 1 = {f0} / {f2} / {f4} / {f1} %");
            Defaults();
            // A5 the labels and values as agreed
            Check(OzFreeCam.Label == "MIDDLE MOUSE" && OzCamControls.SpeedLabel == "SCROLL SPEED" && OzCamControls.RightLabel == "RIGHT-DRAG SCROLL"
                  && string.Join(" / ", OzCamControls.MiddleNames) == "TURN VIEW / DRAG MAP / DRAG MAP, ALT TURNS VIEW",
                  $"A5 rows {OzFreeCam.Label}, {OzCamControls.SpeedLabel}, {OzCamControls.RightLabel}; MIDDLE MOUSE values {string.Join(" / ", OzCamControls.MiddleNames)}");
            // A6 tab places and tips
            string[] want = { "EDGE SCROLL", OzFreeCam.Label, OzCamControls.SpeedLabel, OzCamControls.RightLabel };
            bool tabs = true; var places = new StringBuilder();
            for (int i = 0; i < want.Length; i++)
            {
                int o = OzSettingTips.TabOrder(want[i]); places.Append(want[i]).Append(' ').Append(o).Append(", ");
                tabs &= OzSettingTips.TabOf(want[i]) == OzSettingTips.TabGame && (i == 0 || o == OzSettingTips.TabOrder(want[i - 1]) + 1);
            }
            bool tips = true;
            for (int i = 1; i < want.Length; i++) { var t = OzSettingTips.For(want[i]); tips &= t != null && t.StartsWith(want[i] + "|") && t.EndsWith("|"); }
            Check(tabs && tips, $"A6 GAME tab, one under the other: {places}tips {(tips ? "all three" : "MISSING")}");
            // A7 the middle-button rule
            int rule = 0, rules = 0;
            foreach (OzCamControls.Middle md in System.Enum.GetValues(typeof(OzCamControls.Middle)))
                for (int b = 0; b < 8; b++)
                {
                    bool altHeld = (b & 1) != 0, ov = (b & 2) != 0, ue = (b & 4) != 0, got = OzCamControls.MiddleTurnsFor(md, altHeld, ov, ue);
                    bool exp = md != OzCamControls.Middle.DragMapAltTurns || altHeld || ov || ue; rules++; if (got == exp) rule++;
                }
            Check(rule == rules && !OzCamControls.MiddleTurnsFor(OzCamControls.Middle.DragMapAltTurns, false, false, false) && OzCamControls.MiddleTurnsFor(OzCamControls.Middle.TurnView, false, false, false),
                  $"A7 the middle button turns: {rule}/{rules} cases (only the Alt way without Alt, the overlay or the Unit editor moves the map instead)");
            // A8 the right button's gesture
            Check(Gesture(true, 0f, 2f) == "click" && Gesture(true, 0f, 11f) == "drag" && Gesture(false, 0f, 100f) == "click" && Gesture(true, 0f, 10f) == "click"
                  && GestureBack() == "drag" && GestureSameFrame() == "click",
                  $"A8 right button, threshold 10 px: 2 px {Gesture(true, 0f, 2f)}, 11 px {Gesture(true, 0f, 11f)}, exactly 10 px {Gesture(true, 0f, 10f)}, 100 px not armed {Gesture(false, 0f, 100f)}, out 40 px and back {GestureBack()}, press and release in one frame {GestureSameFrame()}");
            // A9 the CONTROLS texts
            string fT = OzCamControls.CameraRowText(OzFreeCam.FullRowOff, OzFreeCam.FullRowOn, 0), fD = OzCamControls.CameraRowText(OzFreeCam.FullRowOff, OzFreeCam.FullRowOn, 1), fA = OzCamControls.CameraRowText(OzFreeCam.FullRowOff, OzFreeCam.FullRowOn, 2);
            string cT = OzCamControls.CameraRowText(OzFreeCam.CompactRowOff, OzFreeCam.CompactRowOn, 0), cD = OzCamControls.CameraRowText(OzFreeCam.CompactRowOff, OzFreeCam.CompactRowOn, 1), cA = OzCamControls.CameraRowText(OzFreeCam.CompactRowOff, OzFreeCam.CompactRowOn, 2);
            Check(fT == OzFreeCam.FullRowOn && fD == OzFreeCam.FullRowOff && fA == OzCamControls.FullRowAlt && cT == OzFreeCam.CompactRowOn && cD == OzFreeCam.CompactRowOff && cA == OzCamControls.CompactRowAlt
                  && OzCamControls.OrdersRowText(OzCamControls.FullOrders, false) == OzCamControls.FullOrders && OzCamControls.OrdersRowText(OzCamControls.FullOrders, true) == OzCamControls.FullOrdersOn
                  && OzCamControls.OrdersRowText(OzCamControls.CompactOrders, true) == OzCamControls.CompactOrdersOn && OzCamControls.OrdersRowText("Zoom", true) == null,
                  $"A9 CONTROLS camera row: TURN VIEW \"{fT}\" / \"{cT}\", DRAG MAP \"{fD}\" / \"{cD}\", the Alt way \"{fA}\" / \"{cA}\"; orders row with RIGHT-DRAG SCROLL on \"{OzCamControls.FullOrdersOn}\" / \"{OzCamControls.CompactOrdersOn}\"");
        }

        static string Gesture(bool canStart, float x0, float x1)
        {
            var g = new OzCamControls.RightGesture(); int ev = 0;
            ev |= g.Advance(true, true, false, x0, 0f, canStart, 10f);
            for (int i = 1; i <= 5; i++) ev |= g.Advance(false, true, false, x0 + (x1 - x0) * i / 5f, 0f, canStart, 10f);
            ev |= g.Advance(false, false, true, x1, 0f, canStart, 10f);
            return (ev & OzCamControls.RightGesture.DragReleased) != 0 ? "drag" : (ev & OzCamControls.RightGesture.ClickReleased) != 0 ? "click" : "none";
        }
        static string GestureBack()
        {
            var g = new OzCamControls.RightGesture(); int ev = 0;
            ev |= g.Advance(true, true, false, 0f, 0f, true, 10f);
            ev |= g.Advance(false, true, false, 40f, 0f, true, 10f);
            ev |= g.Advance(false, true, false, 0f, 0f, true, 10f);
            ev |= g.Advance(false, false, true, 0f, 0f, true, 10f);
            return (ev & OzCamControls.RightGesture.DragReleased) != 0 ? "drag" : "click";
        }
        static string GestureSameFrame()
        {
            var g = new OzCamControls.RightGesture();
            int ev = g.Advance(true, false, true, 5f, 5f, true, 10f);
            return (ev & OzCamControls.RightGesture.DragReleased) != 0 ? "drag" : (ev & OzCamControls.RightGesture.ClickReleased) != 0 ? "click" : "none";
        }

        // ------------------------------------------------------------------ the camera's own steps on a hidden camera
        static RtsCamera NewCam(List<GameObject> made)
        {
            var go = new GameObject("OzV14CamControlsCheck") { hideFlags = HideFlags.HideAndDontSave }; made.Add(go);
            var cam = go.AddComponent<RtsCamera>();
            cam.mapSizeMeters = new Vector2(4000f, 4000f);                                                    // no map border is ever reached
            cam.focus = new Vector3(2000f, 0f, -2000f);
            cam.SetHeightImmediate(70f);
            return cam;
        }

        /// <summary>One input script through the camera's own steps at 60 fps: 2.5 s of keys (SHIFT from 0.5 s, released at 2 s), 1 s
        /// diagonal, 1 s middle drag at 600 px/s, 5 wheel notches, a fly-to. Every frame's focus x, z and height, and the distances
        /// the keys and the middle drag made.</summary>
        static List<float> Script(RtsCamera cam, out float keys, out float drag)
        {
            var path = new List<float>(); float dt = 1f / 60f, t = 0f;
            void Frame() { cam.Place(); path.Add(cam.focus.x); path.Add(cam.focus.z); path.Add(cam.height); }
            var k0 = cam.focus;
            for (int i = 0; i < 150; i++) { t += dt; cam.FeelStep(dt, t <= 2f ? Vector2.right : Vector2.zero, t > 0.5f && t <= 2f); Frame(); }
            keys = (cam.focus - k0).magnitude;
            for (int i = 0; i < 60; i++) { cam.FeelStep(dt, new Vector2(-1f, 1f), false); Frame(); }
            cam.FeelStep(dt, Vector2.zero, false);
            var d0 = cam.focus;
            for (int i = 0; i < 60; i++) { cam.MiddleDrag(new Vector2(600f * dt, 250f * dt)); cam.FeelStep(dt, Vector2.zero, false); Frame(); }
            drag = (cam.focus - d0).magnitude;
            for (int i = 0; i < 30; i++) { if (i % 6 == 0) cam.WheelInput(OzTrackpadScroll.PerNotch(), dt); cam.FeelStep(dt, Vector2.zero, false); Frame(); }
            cam.FlyTo(cam.focus + new Vector3(60f, 0f, -40f));
            for (int i = 0; i < 90; i++) { cam.FlyStep(dt); cam.FeelStep(dt, Vector2.zero, false); Frame(); }
            return path;
        }

        static void Paths(List<GameObject> made)
        {
            // B1 the defaults against the lines as they were
            Defaults();
            OzCamControls.HooksOff = true; var before = Script(NewCam(made), out float kb0, out float db0);
            OzCamControls.HooksOff = false; var now = Script(NewCam(made), out float kn0, out float dn0);
            int same = 0; for (int i = 0; i < Mathf.Min(before.Count, now.Count); i++) if (System.BitConverter.SingleToInt32Bits(before[i]) == System.BitConverter.SingleToInt32Bits(now[i])) same++;
            Check(before.Count == now.Count && same == now.Count, $"B1 default = before: {now.Count / 3} frames (keys, SHIFT, diagonal, middle drag, wheel, fly-to), {same}/{now.Count} numbers the same to the bit (keys {kn0:0.###} m, middle drag {dn0:0.###} m)");
            // B2 SCROLL SPEED scales the keys and the middle drag
            var ratios = new StringBuilder(); bool good = true;
            foreach (int pct in new[] { 50, 150, 200, 300 })
            {
                OzCamControls.SetSpeed(pct, false);
                Script(NewCam(made), out float k, out float d);
                float want = pct / 100f, rk = k / kn0, rd = d / dn0;
                good &= Mathf.Abs(rk - want) < want * 1e-3f && Mathf.Abs(rd - want) < want * 1e-3f;
                ratios.Append($"{pct} %: keys x{rk:0.0000}, drag x{rd:0.0000}; ");
            }
            Defaults();
            Check(good, $"B2 SCROLL SPEED: {ratios}(want x0.5 / x1.5 / x2 / x3 within 0.1 %)");
            // B3 a drag in a turned view
            var cam = NewCam(made); cam.yaw = 90f;
            Vector3 turned = cam.ProofDragHeading(Vector3.right);
            OzFreeCam.On = false; Vector3 fixedV = cam.ProofDragHeading(Vector3.right); OzFreeCam.On = true;
            cam.yawHeld = true; Vector3 held = cam.ProofDragHeading(Vector3.right); cam.yawHeld = false;
            OzCamControls.HooksOff = true; Vector3 off = cam.ProofDragHeading(Vector3.right); OzCamControls.HooksOff = false;
            cam.yaw = 0f; Vector3 north = cam.ProofDragHeading(Vector3.right);
            bool b3 = Vector3.Distance(turned, Quaternion.Euler(0f, 90f, 0f) * Vector3.right) < 1e-5f && fixedV == Vector3.right && held == Vector3.right && off == Vector3.right && north == Vector3.right;
            Check(b3, $"B3 a drag to the right in a view turned 90 deg goes {turned} (free look on); unturned with DRAG MAP {fixedV}, a script-held heading {held}, the hooks off {off}, a north-up view {north}");
            // B4 the camera's middle-button rule per mode
            OzCamControls.MiddleMode = OzCamControls.Middle.TurnView; bool t0 = cam.ProofMiddleTurns(false);
            OzCamControls.MiddleMode = OzCamControls.Middle.DragMap; bool t1 = cam.ProofMiddleTurns(false);
            OzCamControls.MiddleMode = OzCamControls.Middle.DragMapAltTurns; bool t2 = cam.ProofMiddleTurns(false), t3 = cam.ProofMiddleTurns(true);
            Defaults();
            Check(t0 && t1 && !t2 && t3, $"B4 the camera asks the middle button to turn: TURN VIEW {t0}, DRAG MAP {t1} (the free look then refuses, as FREE CAMERA off), the Alt way without Alt {t2}, with Alt {t3}");
        }

        // ================================================================== the Play runs
        static void StartRun(string what, IEnumerator e)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} enter Play mode first"); return; }
            if (run != null) { Debug.LogWarning($"{Tag} a run is still going"); return; }
            Begin(what); run = e; waitFrame = 0; EditorApplication.update += Step;
        }

        static void Step()
        {
            if (run == null || !EditorApplication.isPlaying) { if (run != null) { run = null; Check(false, "Play stopped in the middle"); Devices(false); Finish(); } EditorApplication.update -= Step; return; }
            if (Time.frameCount < waitFrame) return;
            bool more;
            try { more = run.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); Check(false, "exception " + ex.Message); more = false; }
            if (!more) { run = null; EditorApplication.update -= Step; Devices(false); Finish(); return; }
            waitFrame = Time.frameCount + (run.Current is int n ? n : 1);
        }

        // ------------------------------------------------------------------ the test devices
        static Mouse tm; static Keyboard tk; static Vector2 tmPos; static int tmButtons; static Key[] tkKeys = new Key[0];
        static void Devices(bool on)
        {
            if (on)
            {
                if (tm == null || !tm.added) tm = InputSystem.AddDevice<Mouse>("OzV14TestMouse");
                if (tk == null || !tk.added) tk = InputSystem.AddDevice<Keyboard>("OzV14TestKeys");
                return;
            }
            if (tm != null && tm.added) InputSystem.RemoveDevice(tm);
            if (tk != null && tk.added) InputSystem.RemoveDevice(tk);
            tm = null; tk = null;
        }
        /// <summary>The test mouse at p with buttons (1 left, 2 right, 4 middle), moved by delta this frame; read from the next frame.</summary>
        static void SetMouse(Vector2 p, int buttons, Vector2 delta)
        {
            var st = new MouseState { position = p, delta = delta };
            if ((buttons & 1) != 0) st = st.WithButton(MouseButton.Left);
            if ((buttons & 2) != 0) st = st.WithButton(MouseButton.Right);
            if ((buttons & 4) != 0) st = st.WithButton(MouseButton.Middle);
            InputSystem.QueueStateEvent(tm, st); tmPos = p; tmButtons = buttons;
        }
        static void KeysDown(params Key[] keys) { tkKeys = keys; InputSystem.QueueStateEvent(tk, new KeyboardState(keys)); }

        /// <summary>A press of a button at a, a drag to b in n frames, the release at b (each step read the frame after).</summary>
        static IEnumerator Drag(int button, Vector2 a, Vector2 b, int n)
        {
            SetMouse(a, 0, Vector2.zero); yield return 1;
            SetMouse(a, button, Vector2.zero); yield return 1;
            Vector2 last = a;
            for (int i = 1; i <= n; i++) { var p = Vector2.Lerp(a, b, i / (float)n); SetMouse(p, button, p - last); last = p; yield return 1; }
            SetMouse(b, 0, Vector2.zero); yield return 1;
            yield return 1;
        }

        // ================================================================== 2 Live input (Play, a level)
        [MenuItem("OpenZ/V1.4/Camera controls: 2 Live input (Play, a level)")]
        public static void RunLive() => StartRun("live input", Live());

        static IEnumerator Live()
        {
            var hud = OpenZHud.I; var g = ZGame.I; var rts = hud ? hud.rts : null; var input = hud ? hud.input : null;
            if (!hud || g == null || g.World == null || !rts || !input) { Check(false, "no level running (start a level, wait for the briefing to end)"); yield break; }
            if (RtsCamera.InputLocked || hud.Modal) { Check(false, "the camera is locked or a menu is open"); yield break; }
            float t0 = Time.realtimeSinceStartup;
            while (!Application.isFocused && Time.realtimeSinceStartup - t0 < 30f) { if (Time.frameCount % 120 == 0) Debug.Log($"{Tag} click into the Game view (then leave the mouse and the keyboard alone)"); yield return 1; }
            if (!Application.isFocused) { Check(false, "the Game view never had the focus"); yield break; }
            Defaults();
            if (OzCheatSpawner.Visible) OzCheatSpawner.I.Show(false);
            rts.FreeLookOff(); rts.follow = null;
            Vector3 focus0 = rts.focus; float h0 = rts.height;
            var sel0 = new List<ZObject>(input.Selection);
            Devices(true);
            Vector2 mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
            if (hud.IsOverUi(mid)) mid.y = Screen.height * 0.65f;
            SetMouse(mid, 0, Vector2.zero); KeysDown(); yield return 2;

            // ---- the unit the orders go to: an own robot leader or vehicle near the view's centre
            ZObject unit = null; float best = float.MaxValue;
            foreach (var o in g.World.Objects)
            {
                if (o.Owner != g.player || o.Destroyed || o.KillAt >= 0 || !o.Selectable || o.Kind == ZKind.Cannon) continue;
                float d = (g.CenterWorld(o) - rts.focus).sqrMagnitude; if (d < best) { best = d; unit = o; }
            }
            if (unit == null) { Check(false, "no own unit to order (R rows need one)"); }
            else
            {
                var at = g.CenterWorld(unit); rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(70f); yield return 2;
                input.Selection.Clear(); input.Selection.Add(unit); yield return 1;
                float sx = at.x < rts.mapSizeMeters.x * 0.5f ? 1f : -1f;                                        // the drags go toward the map's middle (no border in reach)
                Vector2 spot = mid + new Vector2(-100f * sx, -60f);                                              // ground beside the unit (it stands at the view's centre)
                if (hud.IsOverUi(spot) || hud.IsOverUi(spot + new Vector2(200f * sx, 20f))) spot = mid + new Vector2(-100f * sx, 60f);
                // R1 OFF: a right drag is no scroll; its release orders, as before
                OzCamControls.RightDrag = false; OzRightDrag.ResetCounters();
                var f = rts.focus; var mk = input.MarkerPos;
                var e = Drag(2, spot, spot + new Vector2(200f * sx, 0f), 10); while (e.MoveNext()) yield return e.Current;
                Check((rts.focus - f).magnitude < 0.05f && input.MarkerPos != mk && OzRightDrag.SitePassed[OzRightDrag.SiteOrders] == 1 && OzRightDrag.Drags == 0,
                      $"R1 RIGHT-DRAG SCROLL off, a right drag of 200 px: the map moved {(rts.focus - f).magnitude:0.###} m, the order given {input.MarkerPos != mk} (as before)");
                // R2 ON: the map moves with the drag, no order
                OzCamControls.RightDrag = true; OzRightDrag.ResetCounters();
                f = rts.focus; mk = input.MarkerPos;
                e = Drag(2, spot + new Vector2(0f, 20f), spot + new Vector2(200f * sx, 20f), 10); while (e.MoveNext()) yield return e.Current;
                var mv = rts.focus - f;
                Check(mv.magnitude > 0.5f * DragM(rts, 200f) && mv.magnitude < 1.02f * DragM(rts, 200f) && Vector3.Dot(mv.normalized, Vector3.right * sx) > 0.9f && input.MarkerPos == mk && OzRightDrag.SiteSwallowed[OzRightDrag.SiteOrders] == 1 && OzRightDrag.Drags == 1,
                      $"R2 ON, the same drag: the map moved {mv.magnitude:0.##} m along the drag (a middle drag of 200 px: {DragM(rts, 200f):0.##} m) (along the drag {Vector3.Dot(mv.normalized, Vector3.right * sx):0.##}), no order {input.MarkerPos == mk}, drags {OzRightDrag.Drags}");
                // R3 ON: a right click (2 px) orders, no scroll
                OzRightDrag.ResetCounters(); f = rts.focus; mk = input.MarkerPos;
                e = Drag(2, spot + new Vector2(30f, -30f), spot + new Vector2(32f, -30f), 2); while (e.MoveNext()) yield return e.Current;
                Check((rts.focus - f).magnitude < 0.05f && input.MarkerPos != mk && OzRightDrag.SitePassed[OzRightDrag.SiteOrders] == 1 && OzRightDrag.Drags == 0,
                      $"R3 ON, a right click that slid 2 px: the order given {input.MarkerPos != mk}, the map moved {(rts.focus - f).magnitude:0.###} m");
                // R4 / R5 the minimap
                var mm = hud.Minimap;
                if (mm && mm.MapRect)
                {
                    var r = mm.MapRect; Vector2 mmc = RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center));
                    OzRightDrag.ResetCounters(); f = rts.focus;
                    e = Drag(2, mmc, mmc + new Vector2(20f, 10f), 4); while (e.MoveNext()) yield return e.Current;
                    Check(OzRightDrag.Drags == 0 && (rts.focus - f).magnitude < 0.05f && OzRightDrag.SitePassed[OzRightDrag.SiteMinimap] == 1,
                          $"R4 ON, a right press and drag on the minimap: no scroll (drags {OzRightDrag.Drags}), the minimap's order as before ({OzRightDrag.SitePassed[OzRightDrag.SiteMinimap]})");
                    Vector2 from = Vector2.MoveTowards(mmc, mid, Mathf.Max(r.rect.width, r.rect.height) * 0.5f * r.lossyScale.x + 90f);
                    if (hud.IsOverUi(from)) from = mid;
                    OzRightDrag.ResetCounters(); mk = input.MarkerPos;
                    e = Drag(2, from, mmc, 10); while (e.MoveNext()) yield return e.Current;
                    Check(OzRightDrag.Drags == 1 && OzRightDrag.SiteSwallowed[OzRightDrag.SiteMinimap] == 1 && OzRightDrag.SitePassed[OzRightDrag.SiteMinimap] == 0 && input.MarkerPos == mk,
                          $"R5 ON, a drag from the map that ends on the minimap: drags {OzRightDrag.Drags}, the minimap's order {(OzRightDrag.SitePassed[OzRightDrag.SiteMinimap] == 0 ? "not given" : "GIVEN")}, no map order {input.MarkerPos == mk}");
                }
                else { Check(false, "R4 no minimap"); Check(false, "R5 no minimap"); }
                OzCamControls.RightDrag = false;
                input.Selection.Clear();
            }

            // ---- MIDDLE MOUSE (from the middle of the map: no border in reach)
            var centre = new Vector3(rts.mapSizeMeters.x * 0.5f, 0f, -rts.mapSizeMeters.y * 0.5f);
            rts.focus = centre; rts.SetHeightImmediate(70f); rts.FreeLookOff(); yield return 2;
            Vector2 c = mid + new Vector2(-100f, 0f), cr = c + new Vector2(100f, 0f);
            OzCamControls.MiddleMode = OzCamControls.Middle.TurnView;
            var f1 = rts.focus; var e1 = Drag(4, c, cr, 10); while (e1.MoveNext()) yield return e1.Current;
            Check(Mathf.Abs(Mathf.DeltaAngle(rts.yaw, 100f * RtsCamera.FreeLookSpeed)) < 0.5f && (rts.focus - f1).magnitude < 0.05f, $"M1 TURN VIEW, a middle drag of 100 px: turned {rts.yaw:0.##} deg (want {100f * RtsCamera.FreeLookSpeed:0.#}), the map moved {(rts.focus - f1).magnitude:0.###} m");
            rts.FreeLookOff();
            OzCamControls.MiddleMode = OzCamControls.Middle.DragMap; yield return 2;
            f1 = rts.focus; e1 = Drag(4, c, cr, 10); while (e1.MoveNext()) yield return e1.Current;
            var dragDist = (rts.focus - f1).magnitude;
            Check(Mathf.Abs(rts.yaw) < 0.01f && Mathf.Abs(dragDist - DragM(rts, 100f)) < 0.02f * DragM(rts, 100f) && (rts.focus - f1).normalized.x > 0.95f, $"M2 DRAG MAP: turned {rts.yaw:0.##} deg, the map moved {dragDist:0.##} m east (want {DragM(rts, 100f):0.##})");
            OzCamControls.MiddleMode = OzCamControls.Middle.DragMapAltTurns; yield return 2;
            f1 = rts.focus; e1 = Drag(4, c, cr, 10); while (e1.MoveNext()) yield return e1.Current;
            Check(Mathf.Abs(rts.yaw) < 0.01f && Mathf.Abs((rts.focus - f1).magnitude - DragM(rts, 100f)) < 0.02f * DragM(rts, 100f), $"M3 DRAG MAP, ALT TURNS VIEW without Alt: turned {rts.yaw:0.##} deg, the map moved {(rts.focus - f1).magnitude:0.##} m");
            KeysDown(Key.LeftAlt); yield return 2;
            f1 = rts.focus; e1 = Drag(4, c, c + new Vector2(600f, 0f), 20); while (e1.MoveNext()) yield return e1.Current;
            float yawAlt = rts.yaw;
            Check(Mathf.Abs(Mathf.DeltaAngle(yawAlt, 600f * RtsCamera.FreeLookSpeed)) < 0.5f && (rts.focus - f1).magnitude < 0.05f, $"M4 the Alt way with Alt held: turned {yawAlt:0.##} deg (want {600f * RtsCamera.FreeLookSpeed:0.#}), the map moved {(rts.focus - f1).magnitude:0.###} m");
            KeysDown(); yield return 2;
            f1 = rts.focus; e1 = Drag(4, c, cr, 10); while (e1.MoveNext()) yield return e1.Current;
            var along = rts.focus - f1; var turnedRight = Quaternion.Euler(0f, rts.yaw, 0f) * Vector3.right;
            Check(Mathf.Abs(along.magnitude - DragM(rts, 100f)) < 0.02f * DragM(rts, 100f) && Vector3.Angle(along, turnedRight) < 3f && Mathf.Abs(Mathf.DeltaAngle(rts.yaw, yawAlt)) < 0.01f,
                  $"M5 then a middle drag to the right in that turned view: {along.magnitude:0.##} m, {Vector3.Angle(along, turnedRight):0.#} deg off the view's right, the turn kept {rts.yaw:0.##} deg");
            OzCamControls.MiddleMode = OzCamControls.Middle.TurnView; OzCamControls.RightDrag = true; yield return 2;
            f1 = rts.focus; e1 = Drag(2, c, cr, 10); while (e1.MoveNext()) yield return e1.Current;
            along = rts.focus - f1; turnedRight = Quaternion.Euler(0f, rts.yaw, 0f) * Vector3.right;
            Check(along.magnitude > 0.3f * DragM(rts, 100f) && Vector3.Angle(along, turnedRight) < 3f, $"M6 TURN VIEW, a turned view, a right drag to the right: {along.magnitude:0.##} m, {Vector3.Angle(along, turnedRight):0.#} deg off the view's right");
            OzCamControls.RightDrag = false; rts.FreeLookOff(); yield return 2;

            // ---- SCROLL SPEED: the right arrow key, the screen edge (m/s while held, before the hold ramp starts at 0.35 s), the middle
            //      drag (m for the same 100 px) at 100 and 200 %, from the middle of the map
            float[] dist = new float[6];
            for (int run2 = 0; run2 < 2; run2++)
            {
                OzCamControls.SetSpeed(run2 == 0 ? 100 : 200, false);
                for (int way = 0; way < 2; way++)
                {
                    rts.focus = centre; yield return 20;
                    if (way == 0) KeysDown(Key.RightArrow); else SetMouse(new Vector2(1f, mid.y), 0, new Vector2(1f - mid.x, 0f));
                    float p0 = Time.unscaledTime;
                    while (Time.unscaledTime - p0 < 0.08f) yield return 1;                                      // the pan's frame-time mean (4 frames) settles
                    Vector3 a0 = rts.focus; float s0 = Time.unscaledTime;
                    while (Time.unscaledTime - p0 < 0.3f) yield return 1;                                       // before the hold ramp (0.35 s)
                    float held = Time.unscaledTime - s0; Vector3 a1 = rts.focus;
                    if (way == 0) KeysDown(); else SetMouse(mid, 0, new Vector2(mid.x - 1f, 0f));
                    yield return 30;                                                                            // released: the hold ramp resets
                    dist[run2 * 3 + way] = held > 0.001f ? (a1 - a0).magnitude / held : 0f;
                }
                rts.focus = centre; OzCamControls.MiddleMode = OzCamControls.Middle.DragMap; yield return 2;
                f1 = rts.focus; e1 = Drag(4, c, cr, 10); while (e1.MoveNext()) yield return e1.Current; dist[run2 * 3 + 2] = (rts.focus - f1).magnitude;
                OzCamControls.MiddleMode = OzCamControls.Middle.TurnView;
            }
            OzCamControls.SetSpeed(100, false);
            string[] what = { "S1 the right arrow key (m/s)", "S2 the screen edge (m/s)", "S3 a middle drag of 100 px (m)" };
            for (int i = 0; i < 3; i++)
            {
                float ratio = dist[i] > 0.01f ? dist[i + 3] / dist[i] : 0f;
                Check(ratio > (i == 2 ? 1.98f : 1.9f) && ratio < (i == 2 ? 2.02f : 2.1f), $"{what[i]}: {dist[i]:0.##} at 100 %, {dist[i + 3]:0.##} at 200 % (x{ratio:0.###}, want x2)");
            }

            // ---- the pause menu
            var pm = hud.Pause;
            if (pm)
            {
                pm.OpenOptions(OzSettingTips.TabGame); yield return 4;
                string rows = pm.ProofOptTabRows;
                Check(rows.Contains("EDGE SCROLL") && rows.Contains(OzFreeCam.Label) && rows.Contains(OzCamControls.SpeedLabel) && rows.Contains(OzCamControls.RightLabel) && !rows.Contains("FREE CAMERA"),
                      $"P1 pause menu, GAME tab rows: {rows}");
                var sl = OzScrollSpeedSlider.Shown;
                if (sl)
                {
                    Check(sl.ProofValue == OzScrollSpeedSlider.ValueText(100) && Mathf.Abs(sl.ProofFill - 0.2f) < 1e-4f, $"P2 SCROLL SPEED row at the default: \"{sl.ProofValue}\", the bar filled {sl.ProofFill:0.###}");
                    var e2 = Drag(1, sl.ProofPoint(0.6f), sl.ProofPoint(0.6f), 1); while (e2.MoveNext()) yield return e2.Current;
                    int afterBar = OzCamControls.ScrollPercent;
                    var vp = sl.ProofValuePoint(0.97f);
                    e2 = Drag(1, vp, vp, 1); while (e2.MoveNext()) yield return e2.Current;
                    int afterArrow = OzCamControls.ScrollPercent;
                    Check(afterBar == 200 && afterArrow == 210 && PlayerPrefs.GetInt(OzCamControls.SpeedPref, -1) == 210,
                          $"P3 a click on the bar at 60 %: {afterBar} % (the row did not also step), then the right arrow: {afterArrow} %, saved {PlayerPrefs.GetInt(OzCamControls.SpeedPref, -1)}");
                }
                else { Check(false, "P2 no SCROLL SPEED slider shown"); Check(false, "P3 no slider"); }
                OzCamControls.SetSpeed(100, false);
                bool camRows = true, orderRows = true; int nCam = 0, nOrd = 0;
                foreach (OzCamControls.Middle md in System.Enum.GetValues(typeof(OzCamControls.Middle)))
                {
                    OzCamControls.MiddleMode = md;
                    foreach (var r in Resources.FindObjectsOfTypeAll<OzFreeCamRowText>())
                    {
                        if (!r || EditorUtility.IsPersistent(r)) continue;
                        r.Refresh(); nCam++;
                        bool full = r.Text == OzFreeCam.FullRowOn || r.Text == OzFreeCam.FullRowOff || r.Text == OzCamControls.FullRowAlt;
                        camRows &= r.Text == OzCamControls.CameraRowText(full ? OzFreeCam.FullRowOff : OzFreeCam.CompactRowOff, full ? OzFreeCam.FullRowOn : OzFreeCam.CompactRowOn, (int)md);
                    }
                }
                OzCamControls.MiddleMode = OzCamControls.Middle.TurnView;
                foreach (bool on in new[] { true, false })
                {
                    OzCamControls.RightDrag = on;
                    foreach (var r in Resources.FindObjectsOfTypeAll<OzRightDragRowText>())
                    {
                        if (!r || EditorUtility.IsPersistent(r)) continue;
                        r.Refresh(); nOrd++;
                        orderRows &= r.ShowsRightDrag == on && (on ? (r.Text == OzCamControls.FullOrdersOn || r.Text == OzCamControls.CompactOrdersOn) : (r.Text == OzCamControls.FullOrders || r.Text == OzCamControls.CompactOrders));
                    }
                }
                OzCamControls.RightDrag = false;
                Check(camRows && nCam >= 3, $"P4 CONTROLS camera rows follow MIDDLE MOUSE ({nCam} row reads over the three values)");
                Check(orderRows && nOrd >= 2, $"P5 CONTROLS orders rows name the right drag only while RIGHT-DRAG SCROLL is on ({nOrd} row reads)");
                pm.Close(); yield return 20;
            }
            else { for (int i = 1; i <= 5; i++) Check(false, $"P{i} no pause menu"); }

            // ---- put back
            SetMouse(mid, 0, Vector2.zero); KeysDown(); yield return 2;
            Devices(false);
            rts.FreeLookOff(); rts.focus = focus0; rts.SetHeightImmediate(h0);
            input.Selection.Clear(); foreach (var o in sel0) if (!o.Destroyed) input.Selection.Add(o);
        }

        // ================================================================== 3 Main menu rows (Play, MainMenu)
        [MenuItem("OpenZ/V1.4/Camera controls: 3 Main menu rows (Play, MainMenu)")]
        public static void RunMenu() => StartRun("main menu", Menu());

        static IEnumerator Menu()
        {
            var m = Object.FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Check(false, "no main menu in this scene (open MainMenu)"); yield break; }
            Defaults(); yield return 10;
            m.ProofOpenOptions(OzSettingTips.TabGame); yield return 20;
            string rows = m.ProofOptTabRows;
            Check(rows.Contains("EDGE SCROLL") && rows.Contains(OzFreeCam.Label) && rows.Contains(OzCamControls.SpeedLabel) && rows.Contains(OzCamControls.RightLabel) && !rows.Contains("FREE CAMERA"),
                  $"N1 main menu, GAME tab rows: {rows}");
            var sl = OzScrollSpeedSlider.Shown;
            if (sl)
            {
                bool shows = sl.ProofValue == OzScrollSpeedSlider.ValueText(100);
                int got = sl.ProofDragTo(0.8f); yield return 2;
                Check(shows && got == 250 && sl.ProofValue == OzScrollSpeedSlider.ValueText(250) && PlayerPrefs.GetInt(OzCamControls.SpeedPref, -1) == 250,
                      $"N2 the slider showed the default ({shows}); a drag to 80 % of the bar: {got} %, the row reads \"{sl.ProofValue}\", saved {PlayerPrefs.GetInt(OzCamControls.SpeedPref, -1)}");
            }
            else Check(false, "N2 no SCROLL SPEED slider shown");
            m.ProofCloseSide(); yield return 10;
            bool camRows = true; int n = 0;
            foreach (OzCamControls.Middle md in System.Enum.GetValues(typeof(OzCamControls.Middle)))
            {
                OzCamControls.MiddleMode = md;
                foreach (var r in Resources.FindObjectsOfTypeAll<OzFreeCamRowText>())
                {
                    if (!r || EditorUtility.IsPersistent(r)) continue;
                    r.Refresh(); n++;
                    bool full = r.Text == OzFreeCam.FullRowOn || r.Text == OzFreeCam.FullRowOff || r.Text == OzCamControls.FullRowAlt;
                    camRows &= r.Text == OzCamControls.CameraRowText(full ? OzFreeCam.FullRowOff : OzFreeCam.CompactRowOff, full ? OzFreeCam.FullRowOn : OzFreeCam.CompactRowOn, (int)md);
                }
            }
            OzCamControls.RightDrag = true; int nOrd = 0; bool ord = true;
            foreach (var r in Resources.FindObjectsOfTypeAll<OzRightDragRowText>()) { if (!r || EditorUtility.IsPersistent(r)) continue; r.Refresh(); nOrd++; ord &= r.Text == OzCamControls.FullOrdersOn || r.Text == OzCamControls.CompactOrdersOn; }
            OzCamControls.RightDrag = false;
            foreach (var r in Resources.FindObjectsOfTypeAll<OzRightDragRowText>()) { if (!r || EditorUtility.IsPersistent(r)) continue; r.Refresh(); ord &= r.Text == OzCamControls.FullOrders || r.Text == OzCamControls.CompactOrders; }
            Check(camRows && n >= 3 && ord && nOrd >= 1, $"N3 the CONTROLS table: the camera row follows MIDDLE MOUSE ({n} reads), the orders row the right drag ({nOrd} rows)");
        }
    }
}
