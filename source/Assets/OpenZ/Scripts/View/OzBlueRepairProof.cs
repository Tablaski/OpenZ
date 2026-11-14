#if UNITY_EDITOR
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
    /// 1.2 items 67 and 68 Play proof (editor only), menu OpenZ/V1.2/Repair: ... (Editor/OzBlueRepairMenu.cs). Start the level in Play first
    /// (PLAY AS BLUE ticked or not: the proof plays the side the human has).
    ///  Trip (item 67, level 16 or 17): the CPU removed, the clock held in the morning, a proof mouse and keyboard. The station: the one whose
    ///  DV62 waiting spot a cannon blocks (the Jungle's north station with its gatling), else the nearest to the human's fort; its zone given
    ///  to the human. Damaged vehicles of the human (a mech beside the gun where the owner's stood, a mech and a crane from the south-west, a
    ///  jeep from the west), one after the other: selected, the R key pressed (the real key path), then watched: T1 the order is UNIT_REPAIR to
    ///  that station, T2 on the pad within 90 game seconds, T3 healed to full, T4 back within 32 px of where it stood (90 s more). Pictures at
    ///  the normal RTS camera (70 m) over the station: the first vehicle at the gun, on the pad, back.
    ///  Lock (item 68), win or loss: the CPU silent, three own units selected and one under the pointer (L0: selected, hovered, its frame
    ///  drawn); then OpenZVictory.DebugFortFalls (the enemy fort for a win, the own fort for a loss) and, for 8 real seconds, every 10 frames a
    ///  left click on an own unit, a box drag over them, a right click on the ground and the R key through the proof devices. Counted: L1
    ///  frames with a selection 0, L2 frames with a hovered unit (input and the overlay's chip) 0, L3 frames with a unit cursor (not the
    ///  arrow) 0, L4 orders the player's units took after the fall (a player-given waypoint) 0, L5 the lock refused the clicks (its counter),
    ///  then L6 the end screen comes up with its three buttons usable (never pressed). Pictures: the pointer on a unit before the fall, after
    ///  the fall with the pointer on the same unit, the end screen.
    /// Counted PASS / FAIL lines and full-screen pictures in <see cref="Folder"/>; the settings and zones it changed are put back (the units it
    /// made stay; a lock run ends the match). Test only.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class OzBlueRepairProof : MonoBehaviour
    {
        public enum Part { Trip, Win, Loss }
        public static string Report = "idle";
        const string KitName = "k67_blue_repair_stuck";
        public const float NormalHeight = 70f;
        Part part;

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
            return System.IO.Path.Combine(Application.persistentDataPath, "blue_repair_proof");
        }

        public static void Run(Part p)
        {
            Report = "running";
            var go = new GameObject("OzBlueRepairProof"); go.AddComponent<OzBlueRepairProof>().part = p;   // no window focus: DevicesOn routes the proof devices to the game (RouteOn)
        }

        ZGame g; ZWorld W; PlayerInput input; RtsCamera rts; OpenZHud hud; Team me;
        readonly StringBuilder sb = new StringBuilder(); int passes, fails, shots; string dir, runTag;
        readonly List<KeyValuePair<ZObject, Team>> zones = new List<KeyValuePair<ZObject, Team>>();
        static Mouse proofMouse, realMouse; static Keyboard proofKb, realKb;

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzBlueRepairProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, runTag + "_" + name + ".png")); shots++;
            Log($"   picture {runTag}_{name}.png (full screen {Screen.width}x{Screen.height}, the RTS camera at {(rts ? rts.height : 0f):0} m)");
            yield return Frames(3);
        }
        static object F(object o, string n)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance); if (f != null) return f.GetValue(o); }
            return null;
        }
        void Cam(Vector3 at) { if (!rts) return; rts.FreeLookOff(); rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(NormalHeight); }
        static float Dist(int ax, int ay, int bx, int by) => Mathf.Sqrt((ax - bx) * (float)(ax - bx) + (ay - by) * (float)(ay - by));
        ZObject ZoneFlag(ZObject b) => W.Flags.FirstOrDefault(f => f.LinkedObjects.Contains(b)) ?? (b.ZoneIndex >= 0 ? W.Flags.FirstOrDefault(f => f.ZoneIndex == b.ZoneIndex) : null);
        void Give(ZObject flag, Team t) { if (flag == null) return; if (!zones.Any(z => z.Key == flag)) zones.Add(new KeyValuePair<ZObject, Team>(flag, flag.Owner)); W.AwardZone(flag, t); }
        ZObject Spawn(bool robot, string type, int px, int py, int hpPercent)
        {
            OzCheatSpawner.FreeTile(W, px >> 4, py >> 4, robot ? 1 : 2, robot, out int tx, out int ty);
            var o = robot ? W.CreateRobotGroup(type, tx * 16, ty * 16, me) : W.CreateObject("vehicle", type, tx * 16, ty * 16, me);
            if (o == null) { Log($"spawn {type} near ({px},{py}) failed"); return null; }
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            if (hpPercent < 100) o.SetHealth(Mathf.Max(1, o.MaxHealth * hpPercent / 100));
            return o;
        }
        Vector2 ScreenOf(ZObject o) { var c = rts ? rts.GetComponent<Camera>() : Camera.main; var p = c.WorldToScreenPoint(g.CenterWorld(o) + Vector3.up * 0.9f); return new Vector2(p.x, p.y); }   // the middle of the figure (the overlay's hover box)

        // the editor routes pointer and keyboard events to the game only while a Game view has focus (and resets devices when the editor
        // loses focus): for the proof's own devices the input goes to the game whatever window has focus, put back after (no window focus)
        static bool routed; static InputSettings.EditorInputBehaviorInPlayMode route0; static InputSettings.BackgroundBehavior back0;
        static void RouteOn()
        {
            if (routed) return; routed = true;
            var s = InputSystem.settings; route0 = s.editorInputBehaviorInPlayMode; back0 = s.backgroundBehavior;
            s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        }
        static void RouteOff()
        {
            if (!routed) return; routed = false;
            var s = InputSystem.settings; s.editorInputBehaviorInPlayMode = route0; s.backgroundBehavior = back0;
        }

        // ------------------------------------------------------------------ proof devices
        void DevicesOn()
        {
            RouteOn();
            realMouse = Mouse.current; proofMouse = InputSystem.AddDevice<Mouse>("OzProofMouse");
            if (realMouse != null && realMouse != proofMouse) InputSystem.DisableDevice(realMouse);
            proofMouse.MakeCurrent();
            realKb = Keyboard.current; proofKb = InputSystem.AddDevice<Keyboard>("OzProofKeyboard"); proofKb.MakeCurrent();
            InputSystem.QueueStateEvent(proofKb, new KeyboardState());
            MouseAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f));
        }
        static void DevicesOff()
        {
            if (realMouse != null) { InputSystem.EnableDevice(realMouse); realMouse.MakeCurrent(); realMouse = null; }
            if (proofMouse != null && proofMouse.added) InputSystem.RemoveDevice(proofMouse);
            proofMouse = null;
            if (proofKb != null && proofKb.added) InputSystem.RemoveDevice(proofKb);
            proofKb = null;
            if (realKb != null) { realKb.MakeCurrent(); realKb = null; }
            RouteOff();
        }
        static void MouseAt(Vector2 p, bool left = false, bool right = false)
        {
            if (proofMouse == null) return;
            var s = new MouseState { position = p };
            if (left) s = s.WithButton(MouseButton.Left, true);
            if (right) s = s.WithButton(MouseButton.Right, true);
            InputSystem.QueueStateEvent(proofMouse, s);
        }
        static IEnumerator Key(Key k)
        {
            if (proofKb == null) yield break;
            InputSystem.QueueStateEvent(proofKb, new KeyboardState(k)); yield return null; yield return null;
            InputSystem.QueueStateEvent(proofKb, new KeyboardState()); yield return null;
        }

        void Start() { StartCoroutine(part == Part.Trip ? Trip() : Lock(part == Part.Win)); }

        bool Setup(string what)
        {
            g = ZGame.I; W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>(); rts = FindFirstObjectByType<RtsCamera>(); hud = OpenZHud.I;
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            if (W == null || !input || !hud) { Log("no level in Play (start level 16 or 17, the kit applied)"); fails++; return false; }
            me = g.player; runTag = (part == Part.Trip ? "k67" : "k68_" + (part == Part.Win ? "win" : "loss")) + "_" + (me == Team.Blue ? "blue" : "red");
            Log($"1.2 {what} proof, {g.level}, human {me}{(me == Team.Blue ? " (PLAY AS BLUE)" : "")}, {Screen.width}x{Screen.height}, world time {W.Time:0.0} s");
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            DevicesOn();
            return true;
        }

        // ------------------------------------------------------------------ item 67: the R key's trip
        IEnumerator Trip()
        {
            if (!Setup("item 67 repair trip")) { Finish(); yield break; }
            yield return WaitFor(() => !ZodBriefing.BriefingActive, 120f); yield return Real(0.5f);   // the start briefing first: ControlAt is a static and may still hold the last level's moment
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 30f);
            int bots = W.Bots.Count; W.Bots.Clear();
            Log($"set-up: CPU removed ({bots}), the clock held, proof mouse and keyboard");
            var stations = W.Objects.Where(o => o.Kind == ZKind.Building && o.Type == "repair" && !o.Destroyed).OrderBy(o => o.Id).ToList();
            if (stations.Count == 0) { Check("a repair station on this level (16 and 17 have them)", false); Finish(); yield break; }
            var fort = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == me && !o.Destroyed);
            System.Func<ZObject, bool> gunBlocked = s => s.GetRepairEntrance(out int qx, out int qy) &&                            // DV62's old waiting spot, 40 px out: a cannon nearer than a mech may stand
                W.Objects.Any(c => c.Kind == ZKind.Cannon && !c.Destroyed && Dist(c.CenterX, c.CenterY, qx, qy + 40) < ZPathing.VehicleRadiusOf("medium") + ZPathing.Radius(c) + 1);
            var st = stations.FirstOrDefault(gunBlocked) ?? stations.OrderBy(s => fort == null ? 0 : Dist(s.CenterX, s.CenterY, fort.CenterX, fort.CenterY)).First();
            st.GetRepairEntrance(out int ex, out int ey);
            Give(ZoneFlag(st), me);
            var guns = W.Objects.Where(o => o.Kind == ZKind.Cannon && !o.Destroyed && Dist(o.CenterX, o.CenterY, ex, ey) < 120).ToList();
            Log($"repair#{st.Id} (owner {st.Owner}), entrance ({ex},{ey}); cannons by the doorway: {(guns.Count == 0 ? "none" : string.Join(", ", guns.Select(c => $"{c.Type}#{c.Id} at ({c.CenterX},{c.CenterY}) {Dist(c.CenterX, c.CenterY, ex, ey + 40):0} px from the old waiting spot")))}");
            var plan = new List<(string kind, int x, int y, string where)>
            {
                ("medium", ex + 40, ey + 56, "beside the gun, where the owner's mech stood"),
                ("medium", ex - 160, ey + 150, "from the south-west"),
                ("crane", ex - 120, ey + 200, "from the south-west"),
                ("jeep", ex - 260, ey - 40, "from the west"),
            };
            int i = 0;
            foreach (var (kind, px, py, where) in plan)
            {
                i++;
                var v = Spawn(false, kind, px, py, 45);
                if (v == null) { Check($"T0.{i} the {kind} {where}", false); continue; }
                int sx = v.CenterX, sy = v.CenterY;
                Cam(g.CenterWorld(st)); yield return Real(0.4f);
                input.Selection.Clear(); input.Selection.Add(v);
                yield return Key(OzBindings.KeyOf(OzAction.RepairVehicles));
                yield return Frames(2);
                bool sent = v.Waypoints.Count > 0 && v.Waypoints[0].mode == WpMode.UnitRepair && v.Waypoints[0].refId == st.Id;
                Check($"T1.{i} {kind}#{v.Id} {where} ({sx},{sy}) {v.Health}/{v.MaxHealth}: the R key gives UNIT_REPAIR to repair#{st.Id}", sent);
                input.Selection.Clear();
                double t0 = W.Time; bool shotGun = false;
                while (!v.Destroyed && !v.OnRepairPad(st.Id) && W.Time - t0 < 90)
                {
                    if (i == 1 && !shotGun && Dist(v.CenterX, v.CenterY, ex, ey + 40) < 40) { shotGun = true; yield return Shot("1_at_the_gun"); }
                    yield return null;
                }
                bool pad = !v.Destroyed && v.OnRepairPad(st.Id);
                Check($"T2.{i} on the pad after {W.Time - t0:0.0} game s (limit 90)", pad);
                if (i == 1 && pad) yield return Shot("2_on_the_pad");
                yield return WaitFor(() => v.Destroyed || v.Health >= v.MaxHealth, 30f);
                Check($"T3.{i} healed to full ({v.Health}/{v.MaxHealth})", !v.Destroyed && v.Health >= v.MaxHealth);
                double t1 = W.Time;
                while (!v.Destroyed && W.Time - t1 < 90 && !(v.Waypoints.Count == 0 && !v.IsMoving)) yield return null;
                float back = Dist(v.CenterX, v.CenterY, sx, sy);
                Check($"T4.{i} back where it stood: {back:0} px from ({sx},{sy}) (limit 32), {W.Time - t1:0.0} game s after the pad", !v.Destroyed && back <= 32f);
                if (i == 1) { Cam(g.CenterWorld(v)); yield return Real(0.5f); yield return Shot("3_back"); }
            }
            Log($"waiting spots moved off a cannon (sim counter): {ZObject.RepairSpotMoves}");
            Finish();
        }

        // ------------------------------------------------------------------ item 68: no world input once the match is decided
        IEnumerator Lock(bool win)
        {
            if (!Setup("item 68 end lock (" + (win ? "win" : "loss") + ")")) { Finish(); yield break; }
            yield return WaitFor(() => !ZodBriefing.BriefingActive, 120f); yield return Real(0.5f);   // the start briefing first: ControlAt is a static and may still hold the last level's moment
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 30f);
            foreach (var b in W.Bots) b.Ignored = true;
            var fort = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == me && !o.Destroyed);
            var mine = W.Objects.Where(o => o.Owner == me && !o.Destroyed && !o.IsMinion && (o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle) && !o.IsFortDefender).OrderBy(o => fort == null ? 0 : Dist(o.CenterX, o.CenterY, fort.CenterX, fort.CenterY)).Take(3).ToList();
            while (mine.Count < 3 && fort != null) { var o = Spawn(true, "grunt", fort.CenterX + 40 * mine.Count, fort.CenterY + 120, 100); if (o == null) break; mine.Add(o); }
            if (mine.Count == 0) { Check("own units on the map", false); Finish(); yield break; }
            var u = mine[0];
            Cam(g.CenterWorld(u)); yield return Real(0.8f);
            input.Selection.Clear(); input.Selection.AddRange(mine);
            var ov = FindFirstObjectByType<OpenZUnitOverlay>();
            var aim = ScreenOf(u);
            MouseAt(aim); yield return Frames(6);
            // own units are never PlayerInput's hover targets (enemies, buildings and objects are); the mouse-over on an own unit is the overlay's (its hover frame)
            Check($"L0 before the fall: {input.Selection.Count} selected; the game's pointer ({hud.Pointer.x:0},{hud.Pointer.y:0}), aimed ({aim.x:0},{aim.y:0}); the overlay's mouse-over {ov?.HoverMember?.Type}#{ov?.HoverMember?.Id} (on {u.Type}#{u.Id}), its chip on #{OpenZUnitOverlay.HoverShownId}; input hover {input.Hover?.Type}#{input.Hover?.Id}; cursor '{hud.Cursors?.Current}'",
                  input.Selection.Count > 0 && (hud.Pointer - aim).sqrMagnitude < 4f && ov != null && ov.HoverMember != null);
            yield return Shot("1_before_the_fall");
            int frames0 = PlayerInput.EndLockFrames, refused0 = PlayerInput.EndLockRefused;
            int n = OpenZVictory.DebugFortFalls(win);
            Log($"DebugFortFalls({win}): {n} fort part(s); decided now: {PlayerInput.MatchDecided(W)} (the lock acts from the next frame's input read: this frame's input was read before the fall)");
            yield return null;
            float t0 = Time.unscaledTime; int frames = 0, selF = 0, hovF = 0, chipF = 0, ovF = 0, curF = 0, clicks = 0; var orders = new HashSet<int>();
            int k = 0;
            while (Time.unscaledTime - t0 < 8f)
            {
                frames++; k++;
                if (input.Selection.Count > 0) selF++;
                if (input.Hover != null) hovF++;
                if (OpenZUnitOverlay.HoverShownId >= 0) chipF++;
                if (ov != null && ov.HoverMember != null) ovF++;
                if (hud.Cursors != null && hud.Cursors.Current != "arrow") curF++;
                foreach (var o in mine) if (!o.Destroyed && o.Waypoints.Any(w => w.playerGiven)) orders.Add(o.Id);
                var at = u.Destroyed ? new Vector2(Screen.width * 0.5f, Screen.height * 0.55f) : ScreenOf(u);
                if (at.x < 140 || at.y < 140 || at.x > Screen.width - 140 || at.y > Screen.height - 140) at = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);   // the ending camera moved away: the middle of the view
                switch (k % 40)
                {
                    case 0: MouseAt(at, true); clicks++; break;
                    case 2: MouseAt(at); break;
                    case 10: MouseAt(at - new Vector2(80, 80), true); clicks++; break;
                    case 13: MouseAt(at + new Vector2(80, 80), true); break;
                    case 15: MouseAt(at + new Vector2(80, 80)); break;
                    case 20: MouseAt(at + new Vector2(120, 0), false, true); break;
                    case 22: MouseAt(at + new Vector2(120, 0)); clicks++; break;
                    case 30: StartCoroutine(Key(OzBindings.KeyOf(OzAction.RepairVehicles))); break;
                    case 35: MouseAt(at); break;
                }
                if (k == 45) yield return Shot("2_after_the_fall_pointer_on_a_unit");
                yield return null;
            }
            Check($"L1 frames with a selection after the fall: {selF} of {frames}", selF == 0);
            Check($"L2 frames with a hovered unit: input {hovF}, the overlay's mouse-over {ovF}, its chip {chipF}", hovF == 0 && ovF == 0 && chipF == 0);
            Check($"L3 frames with a unit cursor (not the arrow): {curF}", curF == 0);
            Check($"L4 orders the player's units took after the fall: {orders.Count}", orders.Count == 0);
            Check($"L5 the lock held {PlayerInput.EndLockFrames - frames0} frames and refused {PlayerInput.EndLockRefused - refused0} of the {clicks} clicks (decided: {PlayerInput.MatchDecided(W)}, game over {W.GameOver}, ending runs {OpenZEnding.Running})",
                  PlayerInput.EndLockFrames - frames0 > 0 && PlayerInput.EndLockRefused - refused0 > 0 && PlayerInput.MatchDecided(W));
            MouseAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return WaitFor(() => OpenZEndScreen.AnyShown, 40f);
            yield return Real(2.5f);
            var es = FindFirstObjectByType<OpenZEndScreen>();
            var buttons = es != null ? F(es, "buttons") as List<HudButton> : null;
            int usable = buttons == null ? 0 : buttons.Count(b => b != null && b.Interactable);
            Check($"L6 the end screen is up ({OpenZEndScreen.AnyShown}) with its buttons usable: {usable} of {(buttons == null ? 0 : buttons.Count)}; still nothing selected ({input.Selection.Count})", OpenZEndScreen.AnyShown && usable == 3 && input.Selection.Count == 0);
            yield return Shot("3_end_screen");
            Finish();
        }

        void Finish()
        {
            if (W != null)
                for (int i = zones.Count - 1; i >= 0; i--)
                    if (zones[i].Key != null && zones[i].Key.Owner != zones[i].Value) W.AwardZone(zones[i].Key, zones[i].Value);
            DevicesOff();
            Log($"result: {passes} PASS, {fails} FAIL, {shots} pictures");
            Report = fails == 0 ? $"PASS ({passes})" : $"FAIL ({fails} of {passes + fails})";
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir ?? Folder(), (runTag ?? "k67") + "_proof.txt"), sb.ToString()); }
            catch (System.Exception ex) { Debug.LogWarning("[OzBlueRepairProof] " + ex.Message); }
            StopAllCoroutines();
            Destroy(gameObject);
        }

        void OnDestroy() { DevicesOff(); }
    }
}
#endif
