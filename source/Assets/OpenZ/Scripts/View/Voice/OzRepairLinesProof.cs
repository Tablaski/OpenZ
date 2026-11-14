using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.2 items 25 / 25a Play proof (View/Voice/AudioDirector.RepairLines.cs, OzRepairLines.cs), menu OpenZ/V1.2/Voice: 25 Repair Lines
    /// Proof (Editor/OzRepairLinesMenu.cs), on a level with a repair station (16 or 17) in Play. Set-up as the wrench key proof: the CPU
    /// teams removed, the clock held, MINIMAL UI off, TOUCH / GAMEPAD on AUTO, a proof mouse and (for R) a proof keyboard in place of the
    /// real ones, the normal RTS camera (70 m). The station made ours; one damaged vehicle of every repairable type beside it (40 %).
    /// Before every order the proof waits for a quiet robot channel (the line gap), so each line can start.
    ///  P  the 8 repair pools: 15 lines each (the 15 of OzRepairLines.Lines), 15 clips load, on Alan's main channel;
    ///  R  the R key (proof keyboard) on the damaged light tank: UNIT_REPAIR, and the light tank's repair line plays (its own pool, its
    ///     own clips), no move / classic order line;
    ///  W  the wrench key over the command bar (a proof-mouse click, the HUD's own path) on the heavy tank: the heavy tank's repair line;
    ///  M  a right-click on the station (the proof mouse's right button over it) with the mech: UNIT_REPAIR, the mech's repair line;
    ///  X  the APC with a grunt squad, a right-click on the station: the APC (the one sent) says its repair line, the squad says nothing;
    ///  G  the crane's selection line speaks and R comes at once: its repair line does not start over it, it waits for the quiet gap
    ///     and then plays;
    ///  S  R on another damaged light tank while the crane's repair line speaks: no second line (one answer per burst);
    ///  E  the jeep, the missile launcher and the shuttle with R: each says a line of its own pool;
    ///  N  refused: the jeep undamaged + R: no repair line; the station's zone lost + R on the damaged jeep: the refusal blip
    ///     (ui_order_invalid), no repair line; the zone given back;
    ///  O  the switch: unitRepairLines off + R: no repair line; on again;
    ///  L  the lines said in this run: never the same line twice in a row.
    /// Counted PASS / FAIL lines, the sound log of every order, 2 full-screen pictures, in <see cref="Folder"/>. The zones and the settings
    /// it changed are put back at the end (the units it made stay). Test only.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class OzRepairLinesProof : MonoBehaviour
    {
        public static string Report = "idle";
        const string KitName = "k25_repair_lines";
        public const float NormalHeight = 70f;

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
            return System.IO.Path.Combine(Application.persistentDataPath, "repair_lines_proof");
        }

        public static void Run()
        {
            Report = "running";
#if UNITY_EDITOR
            var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) UnityEditor.EditorWindow.FocusWindowIfItsOpen(gv);
#endif
            new GameObject("OzRepairLinesProof").AddComponent<OzRepairLinesProof>();
        }

        // ------------------------------------------------------------------ code-runner entry points
        /// <summary>The 8 pools: lines, clips that load, the channel; "RESULT: PASS" when every pool has the 15 lines with 15 clips.</summary>
        public static string Status()
        {
            var ad = AudioDirector.I;
            if (!ad) return "no AudioDirector (enter Play on a level)";
            var sb = new StringBuilder(); bool ok = true;
            foreach (var t in OzRepairLines.Types)
            {
                string ev = OzRepairLines.EventFor(t); var d = ad.Def(ev);
                if (d == null || d.clips == null || d.loaded == null) { ok = false; sb.AppendLine("- FAIL " + ev + ": not in the sound table"); continue; }
                var lines = new List<string>();
                foreach (var l in d.lines) if (!string.IsNullOrEmpty(l) && !lines.Contains(l)) lines.Add(l);
                bool same = lines.Count == OzRepairLines.Lines.Length && OzRepairLines.Lines.All(lines.Contains);
                bool clipsOk = d.loaded.Count == d.clips.Length && d.loaded.Count == OzRepairLines.Lines.Length && d.loaded.All(c => c && c.name.StartsWith("alan_uv_" + t + "_repair_"));
                bool chan = d.channel == "alan_main" && !d.is3D && d.bus == "Voice/Robots";
                if (!same || !clipsOk || !chan) ok = false;
                sb.AppendLine($"- {ev}: {lines.Count} lines{(same ? "" : " (NOT the 15 repair lines)")}, {d.loaded.Count} of {d.clips.Length} clips load{(clipsOk ? "" : " (FAIL)")}, {d.channel} {d.bus}{(chan ? "" : " (FAIL: must be alan_main, Voice/Robots, 2D)")}");
            }
            sb.AppendLine("- " + OzRepairLines.Summary());
            sb.Insert(0, "[repair_lines] RESULT: " + (ok ? "PASS" : "FAIL") + "\n");
            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        public static bool Running { get; private set; }
        public static string Last { get; private set; } = "";

        /// <summary>Every line of one type's pool (or of all 8 with type null: about 10 minutes) through the game's own path, one row each.</summary>
        public static string PlayLines(string type = null)
        {
            var ad = AudioDirector.I;
            if (!ad) return "no AudioDirector (enter Play on a level)";
            if (Running) return "already running";
            var takes = new List<KeyValuePair<string, string>>();
            foreach (var t in OzRepairLines.Types)
            {
                if (type != null && t != type) continue;
                var d = ad.Def(OzRepairLines.EventFor(t)); if (d == null || d.lines == null) continue;
                var seen = new HashSet<string>();
                foreach (var l in d.lines) if (!string.IsNullOrEmpty(l) && seen.Add(l)) takes.Add(new KeyValuePair<string, string>(d.id, l));
            }
            if (takes.Count == 0) return "no repair pool" + (type != null ? " for " + type : "") + " (types: " + string.Join(" / ", OzRepairLines.Types) + ")";
            var sb = new StringBuilder(); Running = true;
            ad.StartCoroutine(ad.RepairLinesPlayRun(takes, sb, () => { Running = false; Last = sb.ToString(); Debug.Log("[repair_lines] lines done\n" + Last); }));
            return "playing " + takes.Count + " lines (about " + (takes.Count * 5) + " s with the robot line gap); read OzRepairLinesProof.Last when Running is false";
        }

        // ------------------------------------------------------------------ the counted proof
        ZGame g; ZWorld W; PlayerInput input; RtsCamera rts; OpenZHud hud; AudioDirector ad; Team me = Team.Red;
        ZObject st, stFlag, grunts;
        readonly Dictionary<string, ZObject> veh = new Dictionary<string, ZObject>();
        readonly List<string> said = new List<string>();
        readonly StringBuilder sb = new StringBuilder(); int passes, fails, shots; string dir;
        readonly List<KeyValuePair<ZObject, Team>> zones = new List<KeyValuePair<ZObject, Team>>();
        bool kept, minimal0, lines0 = true; int touch0, help0;
        static Mouse proofMouse, realMouse; static Keyboard proofKb, realKb;
        static Vector2 Park => new Vector2(Screen.width * 0.5f, Screen.height * 0.62f);

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzRepairLinesProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }
        IEnumerator Quiet() { yield return WaitFor(() => AudioDirector.RobotChannelFree, 15f); yield return Real(0.2f); }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); shots++;
            Log($"   picture {name}.png (full screen {Screen.width}x{Screen.height}, the RTS camera at {(rts ? rts.height : 0f):0} m)");
            yield return Frames(3);
        }
        void Cam(Vector3 at) { if (!rts) return; rts.FreeLookOff(); rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(NormalHeight); }
        void Select(params ZObject[] os) { input.Selection.Clear(); foreach (var o in os) if (o != null) input.Selection.Add(o); }
        static List<OzSoundLog.Entry> Plays(float t0, float t1) => OzSoundLog.Since(t0).Where(e => e.t <= t1 && e.outcome != null && e.outcome.StartsWith("PLAY")).ToList();
        static bool IsRepairEvent(string k) => k != null && k.StartsWith("vo_order_x_") && k.EndsWith("_repair");
        static bool IsOtherOrderLine(string k) => k != null && k.StartsWith("vo_order") && !IsRepairEvent(k);
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
        static void MouseAt(Vector2 p, MouseButton? held = null)
        {
            if (proofMouse == null) return;
            var s = new MouseState { position = p }; if (held.HasValue) s = s.WithButton(held.Value, true);
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

        /// <summary>The proof mouse over the station (a point where PlayerInput's hover finds it), then a right click there.</summary>
        IEnumerator RightClickStation()
        {
            Cam(g.CenterWorld(st)); yield return Real(0.4f);
            var cam = rts ? rts.GetComponent<Camera>() : Camera.main;
            var c = cam ? (Vector2)cam.WorldToScreenPoint(g.CenterWorld(st)) : Park;
            Vector2 at = c; bool found = false;
            for (int r = 0; r <= 4 && !found; r++)
                for (int k = 0; k < (r == 0 ? 1 : 8) && !found; k++)
                {
                    float a = k * Mathf.PI / 4f; var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 14f;
                    MouseAt(p); yield return Frames(3);
                    if (input.Hover == st) { at = p; found = true; }
                }
            Log($"   the pointer on the station: {(found ? "found" : "NOT found")} at ({at.x:0},{at.y:0}) px (its centre on screen ({c.x:0},{c.y:0}))");
            MouseAt(at, MouseButton.Right); yield return Frames(3);
            MouseAt(at); yield return Frames(2);
        }

        /// <summary>Waits until a repair line started after t0 (at most `max` s), then reports which event / clip / unit said it.</summary>
        IEnumerator HeardRepair(float t0, float max) { yield return WaitFor(() => AudioDirector.RepairLastAt >= t0, max); }
        string LastLine() => $"{AudioDirector.RepairLastEvent} \"{AudioDirector.RepairLastLine}\" clip {AudioDirector.RepairLastClip} by #{AudioDirector.RepairLastUnit}";
        void NoteSaid(float t0) { if (AudioDirector.RepairLastAt >= t0 && AudioDirector.RepairLastLine != null) said.Add(AudioDirector.RepairLastLine); }
        bool SaidBy(string type, ZObject u, float t0) =>
            AudioDirector.RepairLastAt >= t0 && AudioDirector.RepairLastEvent == OzRepairLines.EventFor(type) && AudioDirector.RepairLastUnit == u.Id
            && AudioDirector.RepairLastClip != null && AudioDirector.RepairLastClip.StartsWith("alan_uv_" + type + "_repair_");
        void SoundLog(string tag, float t0, float t1) => Log($"   the sound log of {tag}:\n" + OzSoundLog.Format(OzSoundLog.Since(t0).Where(e => e.t <= t1).ToList()));

        void Start() { StartCoroutine(All()); }

        IEnumerator All()
        {
            Application.targetFrameRate = 60;
            g = ZGame.I; W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>(); rts = FindFirstObjectByType<RtsCamera>(); hud = OpenZHud.I; ad = AudioDirector.I;
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            if (W == null || !input || !hud || !ad) { Log("no level in Play with the HUD and the AudioDirector (start level 16 or 17, the kit applied)"); fails++; Finish(); yield break; }
            me = g.player;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 30f);
            Log($"1.2 items 25 / 25a repair lines proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, world time {W.Time:0.0} s, key {OzBindings.Cap(OzAction.RepairVehicles)}, robot chatter {AudioDirector.RobotChatter.Names[AudioDirector.RobotChatter.Level]}, voices {ad.voices}, unit voices {ad.unitVoices}, repair lines {ad.unitRepairLines} (share {ad.unitRepairShare:0.00}, wait {ad.unitRepairWait:0.0} s)");

            // ---- P: the pools
            var status = Status();
            Log(status.TrimEnd());
            Check("P the 8 repair pools: 15 lines each with 15 clips that load, on Alan's main channel (Status RESULT)", status.StartsWith("[repair_lines] RESULT: PASS"));

            // ---- set-up
            int bots = W.Bots.Count; W.Bots.Clear();
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            kept = true; minimal0 = OzSettingTips.MinimalUi; touch0 = PlayerPrefs.GetInt(OzTouch.Pref, 0); help0 = PlayerPrefs.GetInt(OzTouch.HelpPref, 0); lines0 = ad.unitRepairLines;
            if (minimal0) OzSettingTips.MinimalUi = false;
            if (OzTouch.Setting != OzTouch.Mode.Auto) OzTouch.Setting = OzTouch.Mode.Auto;
            hud.factoryListOpen = false; if (!hud.hudVisible) hud.SetVisible(true); if (hud.Production && hud.Production.IsOpen) hud.Production.Close();
            MouseOn();
            var stations = W.Objects.Where(o => o.Kind == ZKind.Building && o.Type == "repair" && !o.Destroyed).ToList();
            if (stations.Count == 0) { Check("a repair station on this level (levels 16 and 17 have one)", false); Finish(); yield break; }
            st = stations.FirstOrDefault(o => o.Owner == me) ?? stations[0];
            stFlag = ZoneFlag(st);
            if (stFlag == null) { Check($"the zone flag of repair#{st.Id}", false); Finish(); yield break; }
            if (st.Owner != me) Give(stFlag, me);
            yield return Frames(2);
            st.GetRepairEntrance(out int ex, out int ey);
            int etx = ex >> 4, ety = ey >> 4, n = 0;
            foreach (var t in OzRepairLines.Types)
            {
                int dx = (n % 4) * 4 - 6, dy = 8 + (n / 4) * 5; n++;
                ZObject v;
                if (t == ZWorld.ShuttleType)
                {   // the shuttle as the testing overlay drops one (its pad, its pilot, its view: UI/OzCheatShuttle.cs)
                    v = OzCheatShuttle.Spawn(g, hud, me, new Vector2Int((etx + dx) * 16 + 16, (ety + dy) * 16 + 16));
                    if (v != null) v.SetHealth(Mathf.Max(1, v.MaxHealth * 40 / 100));
                    else Log($"spawn shuttle at tile {etx + dx},{ety + dy} failed ({OzCheatShuttle.LastWhy})");
                }
                else v = Spawn(false, t, etx + dx, ety + dy, 40);
                if (v != null) veh[t] = v;
            }
            grunts = Spawn(true, "grunt", etx, ety + 19, 100);
            Log($"set-up: CPU removed {bots}, the clock {(dn ? "held at 0.10" : "(none)")}, MINIMAL UI off (was {minimal0}), TOUCH / GAMEPAD AUTO, a proof mouse; repair#{st.Id} ours ({input.OwnsRepairStation()}); "
                + string.Join(", ", veh.Select(kv => $"{kv.Key}#{kv.Value.Id} {kv.Value.Health}/{kv.Value.MaxHealth}")) + (grunts != null ? $", grunt squad#{grunts.Id}" : ""));
            Check($"the proof's vehicles: {veh.Count} of {OzRepairLines.Types.Length} types made (missing: {string.Join(", ", OzRepairLines.Types.Where(t => !veh.ContainsKey(t)))}) and the grunt squad", veh.Count == OzRepairLines.Types.Length && grunts != null);
            if (!veh.ContainsKey("light") || !veh.ContainsKey("heavy") || !veh.ContainsKey("medium")) { Finish(); yield break; }

            // ---- R: the R key
            var L = veh["light"]; Select(L); Cam(g.CenterWorld(L));
            yield return Quiet();
            float t0 = Time.unscaledTime;
            yield return PressR();
            yield return HeardRepair(t0, 2f);
            bool rSent = OzRepairLines.Sent(L);
            yield return Real(0.3f);
            var pl = Plays(t0, Time.unscaledTime);
            Check($"R the R key on the damaged light tank: UNIT_REPAIR ({rSent}), its repair line plays: {LastLine()}; other order lines {pl.Count(e => IsOtherOrderLine(e.key))}",
                  rSent && SaidBy("light", L, t0) && !pl.Any(e => IsOtherOrderLine(e.key)));
            NoteSaid(t0); SoundLog("the R key", t0, Time.unscaledTime);
            yield return Shot("k25_1_R_key_light_tank_line");

            // ---- W: the wrench key
            var H = veh["heavy"]; Select(H); Cam(g.CenterWorld(H));
            yield return Quiet();
            yield return WaitFor(() => hud.RepairKeyVisible, 2f);
            var kp = hud.RepairKeyButton != null ? (Vector2)Scr(hud.RepairKeyButton.Rt).center : Park;
            MouseAt(kp); yield return Frames(4);
            bool hot = hud.HotButton == hud.RepairKeyButton;
            t0 = Time.unscaledTime;
            MouseAt(kp, MouseButton.Left); yield return Frames(3);
            MouseAt(kp); yield return Frames(2);
            yield return HeardRepair(t0, 2f);
            bool wSent = OzRepairLines.Sent(H);
            MouseAt(Park); yield return Real(0.3f);
            pl = Plays(t0, Time.unscaledTime);
            Check($"W the wrench key clicked (pointed at {hot}) with the damaged heavy tank: UNIT_REPAIR ({wSent}), its repair line plays: {LastLine()}; other order lines {pl.Count(e => IsOtherOrderLine(e.key))}",
                  hot && wSent && SaidBy("heavy", H, t0) && !pl.Any(e => IsOtherOrderLine(e.key)));
            NoteSaid(t0); SoundLog("the wrench key", t0, Time.unscaledTime);

            // ---- M: a right-click on the station
            var Me = veh["medium"]; Select(Me);
            yield return Quiet();
            t0 = Time.unscaledTime;
            yield return RightClickStation();
            yield return HeardRepair(t0, 2f);
            bool mSent = OzRepairLines.Sent(Me);
            yield return Real(0.3f);
            pl = Plays(t0, Time.unscaledTime);
            Check($"M a right-click on the station with the damaged mech: UNIT_REPAIR to repair#{(Me.Waypoints.Count > 0 ? Me.Waypoints[Me.Waypoints.Count - 1].refId : -1)} ({mSent}), its repair line plays: {LastLine()}; other order lines {pl.Count(e => IsOtherOrderLine(e.key))}",
                  mSent && SaidBy("medium", Me, t0) && !pl.Any(e => IsOtherOrderLine(e.key)));
            NoteSaid(t0); SoundLog("the right-click on the station", t0, Time.unscaledTime);
            yield return Shot("k25_2_right_click_station_mech_line");
            MouseAt(Park);

            // ---- X: a mixed selection, the right-click on the station
            if (veh.TryGetValue("apc", out var A) && grunts != null)
            {
                Select(A, grunts);
                yield return Quiet();
                t0 = Time.unscaledTime;
                yield return RightClickStation();
                yield return HeardRepair(t0, 2f);
                yield return Real(0.2f);
                pl = Plays(t0, Time.unscaledTime);
                Check($"X the APC and a grunt squad, a right-click on the station: the APC (sent: {OzRepairLines.Sent(A)}) says its repair line: {LastLine()}; the squad says nothing (other order lines {pl.Count(e => IsOtherOrderLine(e.key))})",
                      OzRepairLines.Sent(A) && SaidBy("apc", A, t0) && !pl.Any(e => IsOtherOrderLine(e.key)));
                NoteSaid(t0); SoundLog("the mixed right-click", t0, Time.unscaledTime);
                MouseAt(Park);
            }
            else Log("X skipped: no APC or grunt squad");

            // ---- G: the crane's selection line speaks, R at once: the repair line waits for the quiet gap, then plays
            // ---- S: R on another damaged vehicle while the crane's repair line speaks: silent (one answer per burst, behind an order line)
            if (veh.TryGetValue("crane", out var C))
            {
                Select(C); Cam(g.CenterWorld(C));
                yield return Quiet();
                string sel = AudioDirector.DebugUnitLine("crane", "select");                     // the crane's selection line, through the game's path
                yield return Frames(2);
                bool speaking = !AudioDirector.RobotChannelFree;
                t0 = Time.unscaledTime;
                yield return PressR();
                yield return Real(0.5f);
                bool notYet = AudioDirector.RepairLastAt < t0;
                yield return HeardRepair(t0, ad.unitRepairWait + 1.5f);
                float after = AudioDirector.RepairLastAt - t0;
                Check($"G the crane's selection line speaks ({speaking}: {sel}), R at once: no repair line over it ({notYet}); the crane's repair line after the quiet gap, {after:0.0} s after the order (wait at most {ad.unitRepairWait:0.0} s): {LastLine()}",
                      speaking && notYet && SaidBy("crane", C, t0) && after <= ad.unitRepairWait + 0.2f);
                NoteSaid(t0); SoundLog("the crane's wait", t0, Time.unscaledTime);
                var L3 = Spawn(false, "light", etx + 9, ety + 9, 40);
                if (L3 != null && AudioDirector.RepairLastAt >= t0)
                {
                    Select(L3);
                    yield return PressR();
                    yield return Real(ad.unitRepairWait + 1f);
                    Check($"S R on another damaged light tank while the crane's repair line speaks: sent ({OzRepairLines.Sent(L3)}), no second repair line for it (the last: {LastLine()})",
                          OzRepairLines.Sent(L3) && AudioDirector.RepairLastUnit != L3.Id);
                }
            }
            else Log("G / S skipped: no crane");

            // ---- E: every other type through R
            foreach (var t in new[] { "jeep", "missile_launcher", "shuttle" })
            {
                if (!veh.TryGetValue(t, out var v)) { Check($"E {t}: no vehicle of that type made", false); continue; }
                if (t == "jeep") continue;                                                     // the jeep speaks in N (after its refusals)
                Select(v); Cam(g.CenterWorld(v));
                yield return Quiet();
                t0 = Time.unscaledTime;
                yield return PressR();
                yield return HeardRepair(t0, 2f);
                Check($"E the R key on the damaged {t}: UNIT_REPAIR / its repair trip ({OzRepairLines.Sent(v)}), its own repair line: {LastLine()}", OzRepairLines.Sent(v) && SaidBy(t, v, t0));
                NoteSaid(t0);
            }

            // ---- N: refused orders say nothing
            if (veh.TryGetValue("jeep", out var J))
            {
                J.SetHealth(J.MaxHealth); Select(J); Cam(g.CenterWorld(J));
                yield return Quiet();
                t0 = Time.unscaledTime;
                yield return PressR();
                yield return Real(ad.unitRepairWait + 1f);
                Check($"N1 the jeep undamaged + R: not sent ({!OzRepairLines.Sent(J)}), no repair line ({Plays(t0, Time.unscaledTime).Count(e => IsRepairEvent(e.key))})",
                      !OzRepairLines.Sent(J) && !Plays(t0, Time.unscaledTime).Any(e => IsRepairEvent(e.key)));
                J.SetHealth(Mathf.Max(1, J.MaxHealth * 40 / 100));
                foreach (var s in stations) if (s.Owner == me) Give(ZoneFlag(s), Team.Neutral);
                yield return Frames(2);
                Select(J); Cam(g.CenterWorld(J));
                yield return Quiet();
                t0 = Time.unscaledTime;
                yield return PressR();
                yield return Real(ad.unitRepairWait + 1f);
                var pn = Plays(t0, Time.unscaledTime);
                Check($"N2 no own station (owns one: {input.OwnsRepairStation()}) + R on the damaged jeep: the refusal blip ({pn.Count(e => e.key == "ui_order_invalid")}), not sent ({!OzRepairLines.Sent(J)}), no repair line ({pn.Count(e => IsRepairEvent(e.key))})",
                      !input.OwnsRepairStation() && pn.Any(e => e.key == "ui_order_invalid") && !OzRepairLines.Sent(J) && !pn.Any(e => IsRepairEvent(e.key)));
                SoundLog("the refused R", t0, Time.unscaledTime);
                Give(stFlag, me);
                yield return Frames(2);
                Select(J);
                yield return Quiet();
                t0 = Time.unscaledTime;
                yield return PressR();
                yield return HeardRepair(t0, 2f);
                Check($"E the station ours again, R on the damaged jeep: its own repair line: {LastLine()}", OzRepairLines.Sent(J) && SaidBy("jeep", J, t0));
                NoteSaid(t0);
            }

            // ---- O: the switch
            var S2 = Spawn(false, "light", etx + 9, ety + 14, 40);                                 // a fresh damaged light tank
            if (S2 != null)
            {
                ad.unitRepairLines = false;
                Select(S2); Cam(g.CenterWorld(S2));
                yield return Quiet();
                t0 = Time.unscaledTime;
                yield return PressR();
                yield return Real(1.5f);
                var po = Plays(t0, Time.unscaledTime);
                Check($"O repair lines switched off (unitRepairLines false) + R on the damaged light tank: sent ({OzRepairLines.Sent(S2)}), no repair line ({po.Count(e => IsRepairEvent(e.key))}); the order answers as before ({string.Join(", ", po.Where(e => e.key != null && e.key.StartsWith("vo_")).Select(e => e.key))})",
                      OzRepairLines.Sent(S2) && !po.Any(e => IsRepairEvent(e.key)));
                ad.unitRepairLines = lines0;
            }

            // ---- L: no line twice in a row
            int twice = 0; for (int i = 1; i < said.Count; i++) if (said[i] == said[i - 1]) twice++;
            Check($"L the {said.Count} repair lines said in this run: never the same line twice in a row ({twice}): {string.Join(" | ", said)}", said.Count >= 6 && twice == 0);
            Log("   " + OzRepairLines.Summary());
            Finish();
        }

        static Rect Scr(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }

        void Finish()
        {
            if (W != null)
                for (int i = zones.Count - 1; i >= 0; i--)
                    if (zones[i].Key != null && zones[i].Key.Owner != zones[i].Value) W.AwardZone(zones[i].Key, zones[i].Value);
            PutBack();
            Log($"1.2 items 25 / 25a result: {passes} PASS, {fails} FAIL, {shots} pictures; the zones put back ({zones.Count}), the units of the proof stay");
            Report = fails == 0 ? $"PASS ({passes})" : $"FAIL ({fails} of {passes + fails})";
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir ?? Folder(), "repair_lines_proof.txt"), sb.ToString()); }
            catch (System.Exception e) { Debug.LogWarning("[OzRepairLinesProof] " + e.Message); }
            StopAllCoroutines();
            Destroy(gameObject);
        }

        void PutBack()
        {
            MouseOff(); KbOff();
            if (ad) ad.unitRepairLines = lines0;
            if (!kept) return;
            kept = false;
            if (OzSettingTips.MinimalUi != minimal0) OzSettingTips.MinimalUi = minimal0;
            if ((int)OzTouch.Setting != Mathf.Clamp(touch0, 0, 2)) OzTouch.Setting = (OzTouch.Mode)Mathf.Clamp(touch0, 0, 2);
            PlayerPrefs.SetInt(OzTouch.HelpPref, help0); PlayerPrefs.Save();
        }

        void OnDestroy() { PutBack(); }
    }
}
