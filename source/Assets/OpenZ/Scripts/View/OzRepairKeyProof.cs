using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 10 Play proof (UI/OpenZHud.RepairKey.cs: the wrench key over the command bar), menu OpenZ/V1.2/HUD: 10 Wrench Key Proof
    /// (Editor/OzRepairKeyMenu.cs), on a level with a repair station (16 or 17) in Play. Set-up: the CPU teams removed, the clock held in
    /// the morning, MINIMAL UI off, TOUCH · GAMEPAD on AUTO, a proof mouse in place of the real one, the normal RTS camera (70 m, no free
    /// look) on the units. Near the station: a light tank at 40 % (A), a light tank at full health (B) and a grunt squad (G) of the player.
    ///  N  no own repair station (its zone neutral): A selected, the key stays hidden;
    ///  S  the station ours: the key pops in over the bar's R key (12 u above the bar, its middle over R's), on no other panel, with the
    ///     wrench glyph and the touch REPAIR key's tooltip; pointed at, its tooltip shows;
    ///  U  B alone, B + G: hidden; B + G + A: shown;
    ///  L  the factory list open: the key beside the list or past its far edge, never on it; the list closed: back over R;
    ///  M  MINIMAL UI on: gone with the bar; off: back.  H  the HUD hidden: gone; shown: it pops in again.
    ///  P  the fort's build menu (the lower panels off): gone; closed: back.
    ///  T  the touch layout (TOUCH ON): hidden while the touch keys (with their own REPAIR key) show; the mouse back on AUTO: back;
    ///  C  a click on it through the HUD's own pointer path: A gets the R order (UNIT_REPAIR to the station), the key pops out, the repair
    ///     order cue plays (no refusal blip, no attack cue); A drives onto the pad, is repaired, drives back; the key stays hidden;
    ///  R  B damaged: the R key (a proof keyboard) gives B the same order to the same station, with the same cue;
    ///  X  A damaged again (shown), then the station's zone lost: the key goes.
    /// Counted PASS / FAIL lines and full-screen pictures in <see cref="Folder"/>; the zones and the settings it changed are put back at the
    /// end (the units it made stay). Test only.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class OzRepairKeyProof : MonoBehaviour
    {
        public static string Report = "idle";
        const string KitName = "k10_wrench_button";
        /// <summary>The normal RTS camera height of a level start (RtsCamera.height).</summary>
        public const float NormalHeight = 70f;

        /// <summary>
        /// player's data folder.</summary>
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = System.IO.Path.Combine(d.FullName, "work", "staging");
                if (!System.IO.Directory.Exists(staging)) continue;
                foreach (var stage in System.IO.Directory.GetDirectories(staging))
                {
                    var k = System.IO.Path.Combine(stage, KitName);
                    if (System.IO.Directory.Exists(k)) return System.IO.Path.Combine(k, "proof");
                }
            }
            return System.IO.Path.Combine(Application.persistentDataPath, "repair_key_proof");
        }

        public static void Run()
        {
            Report = "running";
#if UNITY_EDITOR
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) UnityEditor.EditorWindow.FocusWindowIfItsOpen(gv);
#endif
            new GameObject("OzRepairKeyProof").AddComponent<OzRepairKeyProof>();
        }

        ZGame g; ZWorld W; PlayerInput input; RtsCamera rts; OpenZHud hud; Team me = Team.Red;
        ZObject st, stFlag, A, B, G, fort;
        RectTransform cmdBar, unitPanel, factoryPanel; HudButton keyR;
        readonly StringBuilder sb = new StringBuilder(); int passes, fails, shots; string dir;
        readonly List<KeyValuePair<ZObject, Team>> zones = new List<KeyValuePair<ZObject, Team>>();   // the flags whose zone the proof gave away, with the owner before
        bool kept, minimal0; int touch0, help0;
        bool tripWatch; int tripWanted;
        static Mouse proofMouse, realMouse; static Keyboard proofKb, realKb;
        static Vector2 Park => new Vector2(Screen.width * 0.5f, Screen.height * 0.62f);

        // ------------------------------------------------------------------ helpers
        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzRepairKeyProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); shots++;
            Log($"   picture {name}.png (full screen {Screen.width}x{Screen.height}, the RTS camera at {(rts ? rts.height : 0f):0} m)");
            yield return Frames(3);
        }
        static object F(object o, string n)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance); if (f != null) return f.GetValue(o); }
            return null;
        }
        static Rect Scr(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
        static bool Drawn(RectTransform rt) => rt && rt.gameObject.activeInHierarchy;
        float K => hud && hud.Canvas ? Mathf.Max(0.01f, hud.Canvas.scaleFactor) : 1f;
        void Cam(Vector3 at) { if (!rts) return; rts.FreeLookOff(); rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(NormalHeight); }
        void Select(params ZObject[] os) { input.Selection.Clear(); foreach (var o in os) if (o != null) input.Selection.Add(o); }
        static int Played(float t0, string key) { int n = 0; foreach (var e in OzSoundLog.Since(t0)) if (e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY")) n++; return n; }
        string Key() => $"wanted {hud.RepairKeyWanted}, on screen {hud.RepairKeyVisible}, spot {hud.RepairKeySpot}, own station {hud.RepairStationOwned}, to send {input.RepairCandidates()}, pops {OpenZHud.RepairKeyPops}";
        ZObject ZoneFlag(ZObject b) => W.Flags.FirstOrDefault(f => f.LinkedObjects.Contains(b)) ?? (b.ZoneIndex >= 0 ? W.Flags.FirstOrDefault(f => f.ZoneIndex == b.ZoneIndex) : null);
        void Give(ZObject flag, Team t)
        {
            if (flag == null) return;
            if (!zones.Any(z => z.Key == flag)) zones.Add(new KeyValuePair<ZObject, Team>(flag, flag.Owner));
            W.AwardZone(flag, t);
        }
        ZObject Spawn(bool robot, string type, int tx, int ty, int hpPercent)
        {
            OzCheatSpawner.FreeTile(W, tx, ty, robot ? 1 : 2, robot, out tx, out ty);
            var o = robot ? W.CreateRobotGroup(type, tx * 16, ty * 16, me) : W.CreateObject("vehicle", type, tx * 16, ty * 16, me);
            if (o == null) { Log($"spawn {type} at tile {tx},{ty} failed"); return null; }
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            if (hpPercent < 100) o.SetHealth(Mathf.Max(1, o.MaxHealth * hpPercent / 100));
            return o;
        }

        // ------------------------------------------------------------------ proof devices
        void MouseOn()
        {
            realMouse = Mouse.current;
            proofMouse = InputSystem.AddDevice<Mouse>("OzProofMouse");
            if (realMouse != null && realMouse != proofMouse) InputSystem.DisableDevice(realMouse);
            proofMouse.MakeCurrent();
            MouseAt(Park);
        }
        static void MouseOff()
        {
            if (realMouse != null) { InputSystem.EnableDevice(realMouse); realMouse.MakeCurrent(); realMouse = null; }
            if (proofMouse != null && proofMouse.added) InputSystem.RemoveDevice(proofMouse);
            proofMouse = null;
        }
        static void MouseAt(Vector2 p, bool down = false)
        {
            if (proofMouse == null) return;
            var s = new MouseState { position = p }; if (down) s = s.WithButton(MouseButton.Left, true);
            InputSystem.QueueStateEvent(proofMouse, s);
        }
        static IEnumerator PressR()
        {
            realKb = Keyboard.current; proofKb = InputSystem.AddDevice<Keyboard>("OzProofKeyboard"); proofKb.MakeCurrent();
            InputSystem.QueueStateEvent(proofKb, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(proofKb, new KeyboardState(OzBindings.KeyOf(OzAction.RepairVehicles))); yield return null; yield return null;
            InputSystem.QueueStateEvent(proofKb, new KeyboardState()); yield return null;
            KbOff();
        }
        static void KbOff()
        {
            if (proofKb != null) { if (proofKb.added) InputSystem.RemoveDevice(proofKb); proofKb = null; }
            if (realKb != null) { realKb.MakeCurrent(); realKb = null; }
        }

        // ------------------------------------------------------------------ run
        void Start() { StartCoroutine(All()); }

        /// <summary>C: every frame of A's repair trip, the key must not be wanted (A is on its way, nothing else is selected).</summary>
        void Update() { if (tripWatch && hud && hud.RepairKeyWanted) tripWanted++; }

        /// <summary>Where the key stands (screen rectangles of the live HUD): off the bar and every lower panel; over R, 12 u above the bar
        /// (spot 0); at the same height, 8 u right of the factory list (spot 1); 12 u past the list's far edge (spot 2).</summary>
        void Placement(string tag, string what, params int[] spots)
        {
            var pod = Scr(hud.RepairKeyRect); var bar = Scr(cmdBar); var key = Scr(hud.RepairKeyButton.Rt);
            float gap = (pod.yMin - bar.yMax) / K, dx = keyR != null ? (key.center.x - Scr(keyR.Rt).center.x) / K : 999f;
            var others = new List<KeyValuePair<string, RectTransform>>
            {
                new KeyValuePair<string, RectTransform>("the unit panel", unitPanel),
                new KeyValuePair<string, RectTransform>("the minimap", hud.Minimap ? hud.Minimap.Panel : null),
                new KeyValuePair<string, RectTransform>("the EXIT CREW tab", hud.ExitButtonRect),
                new KeyValuePair<string, RectTransform>("the factory list", factoryPanel),
                new KeyValuePair<string, RectTransform>("the touch keys", hud.TouchKeysRect),
            };
            var hit = others.Where(o => Drawn(o.Value) && Scr(o.Value).Overlaps(pod)).Select(o => o.Key).ToList();
            int spot = hud.RepairKeySpot; bool ok = spots.Contains(spot) && !pod.Overlaps(bar) && hit.Count == 0 && hud.RepairKeyVisible;
            string at;
            if (spot == 0) { ok &= Mathf.Abs(gap - OpenZHud.RepairPodGap) <= 0.75f && Mathf.Abs(dx) <= 0.75f; at = $"over the R key ({dx:0.0} u off its middle), {gap:0.0} u above the bar"; }
            else if (spot == 1 && Drawn(factoryPanel))
            {
                var lr = Scr(factoryPanel); float side = (pod.xMin - lr.xMax) / K, bottoms = (pod.yMin - lr.yMin) / K;
                ok &= Mathf.Abs(gap - OpenZHud.RepairPodGap) <= 0.75f && side >= OpenZHud.RepairBesideGap - 1.5f && side <= OpenZHud.RepairBesideGap + 1.5f;
                at = $"beside the factory list ({side:0.0} u right of it, bottoms {bottoms:0.0} u apart), {gap:0.0} u above the bar";
            }
            else if (spot == 2 && Drawn(factoryPanel)) { var lr = Scr(factoryPanel); float over = (pod.yMin - lr.yMax) / K; ok &= over >= OpenZHud.RepairPodGap - 1.5f; at = $"past the factory list's top ({over:0.0} u above it)"; }
            else at = $"spot {spot}";
            Log($"   {tag}: the key's plate at screen ({pod.xMin:0},{pod.yMin:0})-({pod.xMax:0},{pod.yMax:0}) px, canvas {hud.RepairKeyCanvasRect}, the bar ({bar.xMin:0},{bar.yMin:0})-({bar.xMax:0},{bar.yMax:0}), k {K:0.000}; on: {(hit.Count == 0 ? "nothing" : string.Join(", ", hit))}");
            Check($"{tag} {what}: the key stands {at}, on no panel{(hit.Count > 0 ? " (on " + string.Join(", ", hit) + ")" : "")}", ok);
        }

        IEnumerator All()
        {
            Application.targetFrameRate = 60;
            g = ZGame.I; W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>(); rts = FindFirstObjectByType<RtsCamera>(); hud = OpenZHud.I;
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            if (W == null || !input || !hud || hud.RepairKeyRect == null) { Log("no level in Play with the HUD and its wrench key (start level 16 or 17, the kit applied)"); fails++; Finish(); yield break; }
            me = g.player;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 30f);   // the HUD entrance is over
            cmdBar = F(hud, "cmdBar") as RectTransform; unitPanel = hud.UnitPanelRect; factoryPanel = F(hud, "factoryPanel") as RectTransform; keyR = F(hud, "bR") as HudButton;
            Log($"1.2 item 10 wrench key proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, interface scale {OzText.UiScale:0.00} (k {K:0.000}), world time {W.Time:0.0} s, key {OzBindings.Cap(OzAction.RepairVehicles)}");

            // ---- set-up
            int bots = W.Bots.Count; W.Bots.Clear();
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            kept = true; minimal0 = OzSettingTips.MinimalUi; touch0 = PlayerPrefs.GetInt(OzTouch.Pref, 0); help0 = PlayerPrefs.GetInt(OzTouch.HelpPref, 0);
            if (minimal0) OzSettingTips.MinimalUi = false;
            if (OzTouch.Setting != OzTouch.Mode.Auto) OzTouch.Setting = OzTouch.Mode.Auto;
            hud.factoryListOpen = false; if (!hud.hudVisible) hud.SetVisible(true); if (hud.Production && hud.Production.IsOpen) hud.Production.Close();
            MouseOn();
            Log($"set-up: CPU removed {bots}, the clock {(dn ? "held at 0.10" : "(none)")}, MINIMAL UI off (was {minimal0}), TOUCH · GAMEPAD AUTO (was {touch0}), a proof mouse in place of the real one");
            var stations = W.Objects.Where(o => o.Kind == ZKind.Building && o.Type == "repair" && !o.Destroyed).ToList();
            if (stations.Count == 0) { Check("a repair station on this level (levels 16 and 17 have one)", false); Finish(); yield break; }
            st = stations.FirstOrDefault(o => o.Owner == me) ?? stations[0];
            stFlag = ZoneFlag(st);
            if (stFlag == null) { Check($"the zone flag of repair#{st.Id}", false); Finish(); yield break; }
            fort = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == me && !o.Destroyed && o.IsProducer);

            // ---- N: no own repair station
            foreach (var s in stations) if (s.Owner == me) Give(ZoneFlag(s), Team.Neutral);
            yield return Frames(2);
            Check($"N0 the player owns no repair station now ({stations.Count} on the level; repair#{st.Id}, its zone's flag#{stFlag.Id} {stFlag.Owner})", !input.OwnsRepairStation());
            st.GetRepairEntrance(out int ex, out int ey);
            int etx = ex >> 4, ety = ey >> 4;
            A = Spawn(false, "light", etx - 3, ety + 8, 40);
            B = Spawn(false, "light", etx + 3, ety + 8, 100);
            G = Spawn(true, "grunt", etx, ety + 12, 100);
            if (A == null || B == null || G == null) { Check("the proof's units (A, B, G)", false); Finish(); yield break; }
            int aX = A.CenterX, aY = A.CenterY;
            Log($"units: A light#{A.Id} at ({A.CenterX},{A.CenterY}) {A.Health}/{A.MaxHealth}; B light#{B.Id} at ({B.CenterX},{B.CenterY}) {B.Health}/{B.MaxHealth}; G grunt squad#{G.Id} x{1 + G.Minions.Count}; repair#{st.Id} entrance ({ex},{ey})");
            Cam(g.CenterWorld(A));
            Select(A);
            yield return Real(0.6f);
            Check($"N1 no own repair station: the damaged tank A selected, the key stays hidden ({Key()})", input.RepairCandidates() == 1 && !hud.RepairKeyWanted && !Drawn(hud.RepairKeyRect));
            yield return Shot("k10_1_no_station_hidden");

            // ---- S: the station ours
            int pops0 = OpenZHud.RepairKeyPops;
            Give(stFlag, me);
            float tS = Time.unscaledTime;
            yield return WaitFor(() => hud.RepairKeyVisible, 1.5f);
            float inS = Time.unscaledTime - tS;
            Check($"S1 the station ours (flag#{stFlag.Id}): the key pops in, fully in after {inS:0.00} s ({Key()})", hud.RepairKeyVisible && inS <= 0.6f && OpenZHud.RepairKeyPops == pops0 + 1);
            Placement("S2", "A selected, the factory list closed", 0);
            var glyph = hud.RepairKeyGlyph; string sprite = glyph && glyph.sprite ? glyph.sprite.name : "(none)";
            var touchTip = hud.TouchKey(4) != null ? hud.TouchKey(4).Tooltip : null;
            Check($"S3 its look and words: the glyph '{sprite}', the tooltip '{hud.RepairKeyButton.Tooltip}' = the touch REPAIR key's ({touchTip == hud.RepairKeyButton.Tooltip})",
                  sprite.Contains(OpenZHud.RepairKeyIcon) && hud.RepairKeyButton.Tooltip == OpenZHud.RepairKeyTip() && (touchTip == null || touchTip == hud.RepairKeyButton.Tooltip));
            yield return Shot("k10_2_shown_damaged_tank_and_station");
            var kc = Scr(hud.RepairKeyButton.Rt).center;
            MouseAt(kc); yield return Real(0.8f);
            var tip = F(hud, "tipPanel") as RectTransform;
            Check($"S4 the pointer on the key: it is the hovered button ({hud.HotButton == hud.RepairKeyButton}) and its tooltip shows ({Drawn(tip)}); the selection kept ({input.Selection.Contains(A)})", hud.HotButton == hud.RepairKeyButton && Drawn(tip) && input.Selection.Contains(A));
            yield return Shot("k10_3_tooltip");
            MouseAt(Park); yield return Frames(4);

            // ---- U: undamaged / mixed selections
            Select(B); yield return Real(0.35f);
            Check($"U1 only the undamaged tank B selected: hidden ({Key()})", !hud.RepairKeyWanted && !Drawn(hud.RepairKeyRect));
            yield return Shot("k10_4_undamaged_tank_hidden");
            Select(B, G); yield return Real(0.3f);
            Check($"U2 B and the grunt squad: hidden ({Key()})", !hud.RepairKeyWanted);
            Select(B, G, A); yield return Real(0.4f);
            Check($"U3 B, the squad and the damaged A: shown ({Key()})", hud.RepairKeyVisible && input.RepairCandidates() == 1);

            // ---- L: the factory list
            Select(A);
            hud.factoryListOpen = true; yield return Real(0.7f);
            Check($"L0 the factory list is open ({Drawn(factoryPanel)})", Drawn(factoryPanel));
            Placement("L1", "the factory list open", 1, 2);
            yield return Shot("k10_5_factory_list_open");
            hud.factoryListOpen = false; yield return Real(0.4f);
            Placement("L2", "the factory list closed again", 0);

            // ---- M / H / P: MINIMAL UI, the hidden HUD, the build menu
            OzSettingTips.MinimalUi = true; yield return Real(0.5f);
            Check($"M1 MINIMAL UI on: gone with the command bar (bar drawn {Drawn(cmdBar)}; {Key()})", !Drawn(cmdBar) && !hud.RepairKeyVisible);
            OzSettingTips.MinimalUi = false; yield return Real(0.5f);
            Check($"M2 MINIMAL UI off: back ({Key()})", Drawn(cmdBar) && hud.RepairKeyVisible);
            hud.SetVisible(false); yield return Real(0.3f);
            Check($"H1 the HUD hidden: not wanted, not drawn ({Key()})", !hud.RepairKeyWanted && !hud.RepairKeyVisible);
            int popsH = OpenZHud.RepairKeyPops;
            hud.SetVisible(true); yield return Real(0.45f);
            Check($"H2 the HUD shown again: the key pops in again ({Key()})", hud.RepairKeyVisible && OpenZHud.RepairKeyPops == popsH + 1);
            if (fort != null)
            {
                hud.Production.Open(fort); yield return Real(0.35f);
                Check($"P1 the fort's build menu open: the lower panels are off and the key with them (lower panels off {hud.LowerPanelsHidden}; {Key()}; A still selected {input.Selection.Contains(A)})",
                      hud.Production.IsOpen && hud.LowerPanelsHidden && !hud.RepairKeyVisible);
                hud.Production.Close(); yield return Real(0.35f);
                Check($"P2 the build menu closed: back ({Key()})", !hud.LowerPanelsHidden && hud.RepairKeyVisible);
            }
            else Log("P skipped: no own fort with a build menu");

            // ---- T: the touch layout
            PlayerPrefs.SetInt(OzTouch.HelpPref, 1);                                                    // the first-time help card would cover the map
            OzTouch.Setting = OzTouch.Mode.On;
            for (int i = 0; i < 4; i++) { MouseAt(Park + new Vector2(8f * i, 0f)); yield return Frames(2); }
            yield return WaitFor(() => OzTouch.Driving && hud.TouchKeysShown, 3f);
            yield return Real(0.3f);
            bool touchOn = OzTouch.Driving && hud.TouchKeysShown;
            Check($"T1 the touch layout (TOUCH ON): its keys show (with their REPAIR key, usable {(hud.TouchKey(4) != null && hud.TouchKey(4).Interactable)}), the wrench key is hidden ({Key()})", touchOn && !hud.RepairKeyWanted && !hud.RepairKeyVisible);
            if (touchOn) yield return Shot("k10_6_touch_layout_hidden");
            OzTouch.Setting = OzTouch.Mode.Auto;
            for (int i = 0; i < 8; i++) { MouseAt(Park + new Vector2(6f * i, 4f * i)); yield return Frames(2); }
            yield return WaitFor(() => !OzTouch.Driving, 3f);
            if (proofMouse != null) proofMouse.MakeCurrent();
            MouseAt(Park); yield return Real(0.45f);
            Check($"T2 the mouse back (AUTO): the wrench key is back ({Key()})", !OzTouch.Driving && hud.RepairKeyVisible);

            // ---- C: the click, through the HUD's own pointer path
            Select(A); Cam(g.CenterWorld(A));
            yield return WaitFor(() => hud.RepairKeyVisible, 1.5f);
            int clicks0 = OpenZHud.RepairKeyClicks;
            var kp = Scr(hud.RepairKeyButton.Rt).center;
            MouseAt(kp); yield return Frames(4);
            bool hot = hud.HotButton == hud.RepairKeyButton;
            float tC = Time.unscaledTime;
            MouseAt(kp, true); yield return Frames(3);
            MouseAt(kp, false); yield return Frames(2);
            bool sent = A.Waypoints.Count > 0 && A.Waypoints[0].mode == WpMode.UnitRepair;
            int refA = sent ? A.Waypoints[0].refId : -1, wxA = sent ? A.Waypoints[0].x : 0, wyA = sent ? A.Waypoints[0].y : 0;
            Check($"C1 a click on the key (pointed at {hot}, clicks {OpenZHud.RepairKeyClicks - clicks0}): A got the R order, UNIT_REPAIR to repair#{refA} at ({wxA},{wyA}); A still selected {input.Selection.Contains(A)}",
                  hot && OpenZHud.RepairKeyClicks == clicks0 + 1 && sent && refA == st.Id && input.Selection.Contains(A));
            yield return Real(0.3f);
            Check($"C2 A on its way: the key popped out ({Key()})", !hud.RepairKeyWanted && !hud.RepairKeyVisible);
            MouseAt(Park);
            yield return Real(0.6f);
            var heard = OzSoundLog.Since(tC - 0.01f).Where(e => e.t <= tC + 0.9f).ToList();
            Log("   the sound log of the click (0.9 s):\n" + OzSoundLog.Format(heard));
            int rep = Played(tC, "ui_order_repair"), inv = Played(tC, "ui_order_invalid"), att = Played(tC, "ui_order_attack");
            Check($"C3 the click's sound: the repair order cue {rep} (the HUD's own click gives way to it in the same frame: one sound per action), no refusal blip ({inv}), no attack cue ({att})", rep >= 1 && inv == 0 && att == 0);
            tripWatch = true; tripWanted = 0;
            Cam(Vector3.Lerp(g.CenterWorld(A), g.CenterWorld(st), 0.5f));
            yield return Real(0.8f);
            yield return Shot("k10_7_clicked_driving_to_station");
            yield return WaitFor(() => A.Destroyed || A.OnRepairPad(st.Id), 45f);
            Check($"C4 A drove onto the station's pad (world time {W.Time:0.0} s)", !A.Destroyed && A.OnRepairPad(st.Id));
            Cam(g.CenterWorld(st)); yield return Real(0.6f);
            yield return Shot("k10_8_on_the_pad");
            yield return WaitFor(() => A.Destroyed || (!A.OnRepairPad(st.Id) && A.Health == A.MaxHealth), 30f);
            yield return WaitFor(() => A.Destroyed || (A.Waypoints.Count == 0 && !A.IsMoving), 45f);
            tripWatch = false;
            float back = Mathf.Sqrt((A.CenterX - aX) * (float)(A.CenterX - aX) + (A.CenterY - aY) * (float)(A.CenterY - aY));
            Check($"C5 repaired ({A.Health}/{A.MaxHealth}) and back where it stood ({back:0} px from its start: the R order's way back)", !A.Destroyed && A.Health == A.MaxHealth && back <= 32f);
            Check($"C6 the key stayed hidden the whole trip ({tripWanted} frames wanted while A was away) and stays hidden with A repaired ({Key()})", tripWanted == 0 && !hud.RepairKeyWanted);
            Cam(g.CenterWorld(A)); yield return Real(0.5f);
            yield return Shot("k10_9_back_repaired_hidden");

            // ---- R: the R key gives the same order
            B.SetHealth(B.MaxHealth / 2); Select(B); Cam(g.CenterWorld(B));
            yield return WaitFor(() => hud.RepairKeyVisible, 1.5f);
            Check($"R0 B damaged and selected: the key shows ({Key()})", hud.RepairKeyVisible);
            float tR = Time.unscaledTime;
            yield return PressR();
            yield return Frames(2);
            bool rSent = B.Waypoints.Count > 0 && B.Waypoints[0].mode == WpMode.UnitRepair;
            Check($"R1 the R key on B gives the order the key's click gave A: UNIT_REPAIR to repair#{(rSent ? B.Waypoints[0].refId : -1)} at ({(rSent ? B.Waypoints[0].x : 0)},{(rSent ? B.Waypoints[0].y : 0)}) (the click: repair#{refA} at ({wxA},{wyA}))",
                  rSent && B.Waypoints[0].refId == refA && B.Waypoints[0].x == wxA && B.Waypoints[0].y == wyA);
            yield return Real(0.4f);
            Check($"R2 the R key's sound: the repair order cue {Played(tR, "ui_order_repair")} (as the click's); the wrench key popped out ({Key()})", Played(tR, "ui_order_repair") >= 1 && !hud.RepairKeyWanted);

            // ---- X: the station lost while the key shows
            A.SetHealth(Mathf.Max(1, A.MaxHealth * 40 / 100)); Select(A); Cam(g.CenterWorld(A));
            yield return WaitFor(() => hud.RepairKeyVisible, 1.5f);
            Check($"X0 A damaged again and selected: shown ({Key()})", hud.RepairKeyVisible);
            Give(stFlag, me == Team.Blue ? Team.Red : Team.Blue);
            float tX = Time.unscaledTime;
            yield return WaitFor(() => !hud.RepairKeyWanted, 1.5f);
            float goneIn = Time.unscaledTime - tX;
            Check($"X1 the station's zone lost: the key goes after {goneIn:0.00} s ({Key()})", !hud.RepairKeyWanted && goneIn <= 0.6f);
            yield return Real(0.4f);
            yield return Shot("k10_10_station_lost_hidden");
            Finish();
        }

        void Finish()
        {
            tripWatch = false;
            if (W != null)
                for (int i = zones.Count - 1; i >= 0; i--)
                    if (zones[i].Key != null && zones[i].Key.Owner != zones[i].Value) W.AwardZone(zones[i].Key, zones[i].Value);
            PutBack();
            Log($"1.2 item 10 result: {passes} PASS, {fails} FAIL, {shots} pictures; the zones put back ({zones.Count}), the units of the proof stay");
            Report = fails == 0 ? $"PASS ({passes})" : $"FAIL ({fails} of {passes + fails})";
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir ?? Folder(), "repair_key_proof.txt"), sb.ToString()); }
            catch (System.Exception ex) { Debug.LogWarning("[OzRepairKeyProof] " + ex.Message); }
            StopAllCoroutines();
            Destroy(gameObject);
        }

        void PutBack()
        {
            MouseOff(); KbOff();
            if (!kept) return;
            kept = false;
            if (OzSettingTips.MinimalUi != minimal0) OzSettingTips.MinimalUi = minimal0;
            if ((int)OzTouch.Setting != Mathf.Clamp(touch0, 0, 2)) OzTouch.Setting = (OzTouch.Mode)Mathf.Clamp(touch0, 0, 2);
            PlayerPrefs.SetInt(OzTouch.HelpPref, help0); PlayerPrefs.Save();
        }

        void OnDestroy() { PutBack(); }
    }
}
