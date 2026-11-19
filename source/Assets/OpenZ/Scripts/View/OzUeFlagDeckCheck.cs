#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// the counted Play check, menu OpenZ/V1.5/Unit Editor: Flag on Platforms Check (Editor/OzV15UeFlagAnywhereMenu.cs; Play, the unit editor open,
    /// about a minute, hands off the mouse and the keyboard). Pictures and report.txt go to <see cref="Folder"/>.
    ///  F1 the drop preview: a flag is accepted (the ghost green) on both decks and on both ramps, upper and lower half; the crate and the obstacle
    ///     are still refused there with their words;
    ///  F2 dropped there: on the deck or the ramp (never a rail), the pole's foot on the surface (not in the air, not under it), upright, the cloth
    ///     over the kerb; the pick box over the flag picks it; its capture ring drapes on the platform;
    ///  F3 a red tank sent onto the tower's flag takes it; a blue robot on the floor right beside the wall does not;
    ///  F4 a red robot sent up the medium ramp takes the flag on the ramp;
    ///  F5 save and load: the flags on the deck and on the ramp again, owners kept;
    ///  F6 the tower destroyed: the flag stays in the rules, its pole comes down to the floor;
    ///  F7 Delete of a platform takes its flag off the field too (quietly), CLEAR FIELD leaves no flag.
    /// </summary>
    public sealed class OzUeFlagDeckCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ FlagDeckCheck]";
        public static string Report = "not run";
        public static string Folder;
        public static bool Running => run;
        static OzUeFlagDeckCheck run;
        int pass, fail;
        readonly StringBuilder lines = new StringBuilder();
        readonly List<string> pictures = new List<string>();
        Vector2 lastPos; bool lBtn, rBtn, hidden0;
        OzUnitEditor ed; ZGame g; ZWorld w;

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_FlagDeckCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzUeFlagDeckCheck>();
        }

        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { Route(false); if (run == this) run = null; }

        bool routed; InputSettings.EditorInputBehaviorInPlayMode route0; InputSettings.BackgroundBehavior back0;
        void Route(bool on)
        {
            var s = InputSystem.settings;
            if (on && !routed) { routed = true; route0 = s.editorInputBehaviorInPlayMode; back0 = s.backgroundBehavior; s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
            else if (!on && routed) { routed = false; s.editorInputBehaviorInPlayMode = route0; s.backgroundBehavior = back0; }
        }

        // ------------------------------------------------------------------------------------------------ helpers (View/OzUePlatformCheck.cs's)
        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        void Info(string what) { lines.Append("INFO  ").AppendLine(what); }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Seconds(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        void Move(Vector2 p)
        {
            var m = Mouse.current; if (m == null) return;
            var st = new MouseState { position = p, delta = p - lastPos };
            if (lBtn) st = st.WithButton(MouseButton.Left);
            if (rBtn) st = st.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(m, st); lastPos = p;
        }

        IEnumerator Click(Vector2 p, bool right = false)
        {
            Move(p); yield return Frames(2);
            if (right) rBtn = true; else lBtn = true;
            Move(p); yield return Frames(2);
            if (right) rBtn = false; else lBtn = false;
            Move(p); yield return Frames(3);
        }

        static Vector2 ToScreen(Vector3 world) { var cam = OzUeDraw.Cam; return cam ? (Vector2)cam.WorldToScreenPoint(world) : Vector2.zero; }
        static ZObject Lead(UeDrop d) => d.ok && d.made != null && d.made.Count > 0 ? d.made[0] : null;
        Vector3 Deck(ZWorld.PlatformInfo p, float across, float along) => OzUePlatformViews.Point(p, across, along, (float)p.HeightAt(along));

        IEnumerator Shot(string name)
        {
            if (string.IsNullOrEmpty(Folder)) yield break;
            Directory.CreateDirectory(Folder);
            string b = name + "_" + Screen.width + "x" + Screen.height, path = Path.Combine(Folder, b + ".png");
            int n = 1; while (File.Exists(path)) path = Path.Combine(Folder, b + "_" + (++n) + ".png");      // never over an older picture
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            pictures.Add(path);
        }

        static Vector3 Focus(OzUnitEditor e)
        {
            Vector3 f = e && e.Rts ? e.Rts.focus : new Vector3(160, 0, -100);
            var map = e && e.World != null ? e.World.Map : null;
            if (map != null) { f.x = Mathf.Clamp(f.x, 70f, map.width * 2f - 70f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 60f), -60f); }
            f.y = 0f; return f;
        }

        /// <summary>The flag's drawn root (FlagView: OZ_Flags/Flag_id), null = no view.</summary>
        static Transform FlagRoot(ZObject f) { var go = f != null ? GameObject.Find("Flag_" + f.Id) : null; return go ? go.transform : null; }

        bool InFlags(ZObject f) { foreach (var x in w.Flags) if (x == f) return true; return false; }

        /// <summary>F2 for one flag: on the platform's walkable part, the pole's foot on the surface, upright, the cloth over the kerb.</summary>
        void Stands(ZObject f, string where)
        {
            var r = FlagRoot(f);
            byte part = f != null ? w.PlatformPartAt(f.CenterX >> 4, f.CenterY >> 4) : (byte)0;
            bool walkable = part == ZWorld.PlatPartDeck || part == ZWorld.PlatPartRampHigh || part == ZWorld.PlatPartRampLow;
            float rule = f != null ? (float)w.PlatformHeightM(f.CenterX, f.CenterY) : -9f;
            float foot = r ? r.position.y : -9f, ground = r ? g.GroundY(r.position.x, r.position.z) : -9f;
            float tilt = r ? Vector3.Angle(r.up, Vector3.up) : 90f;
            float clothLow = float.MaxValue;
            if (r) { var cl = r.Find("Pivot/Cloth"); var cr = cl ? cl.GetComponent<Renderer>() : null; if (cr) clothLow = cr.bounds.min.y; }
            Check(f != null && r && walkable && rule > 0.1f && Mathf.Abs(foot - (rule - 0.05f)) < 0.03f && Mathf.Abs(ground - rule) < 0.1f && tilt < 0.5f
                  && clothLow > rule + OzUePlatformView.KerbM,
                  "F2 a flag on the " + where + ": part " + part + ", the pole's foot " + foot.ToString("0.00") + " m (the platform " + rule.ToString("0.00") + " m, the walk surface "
                  + ground.ToString("0.00") + " m), tilt " + tilt.ToString("0.0") + " deg, the cloth's lowest point " + (clothLow < 1e6f ? clothLow.ToString("0.00") : "none") + " m");
        }

        // ------------------------------------------------------------------------------------------------ the run
        IEnumerator Main()
        {
            Report = "running"; Route(true);
            ed = OzUnitEditor.I; g = ed.Game; w = ed.World;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed;
            hidden0 = OzUnitEditor.PanelsHidden; if (hidden0) OzUnitEditor.SetPanelsHidden(false);
            OzUePlace.Stop(); OzUePlayground.ClearField(); OzUePick.ClearInspect();
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            var c = Focus(ed);
            OzUeControls.CentreOn(c);
            yield return Seconds(1f);
            Info("Game view " + Screen.width + " x " + Screen.height + ", camera turn " + OzUePlayground.PlatformDirNow);

            var mid = Lead(OzUePlayground.Drop(OzUePlayground.PlatformMid, Team.Neutral, c + new Vector3(-30f, 0f, 0f), 1));
            var high = Lead(OzUePlayground.Drop(OzUePlayground.PlatformHigh, Team.Neutral, c + new Vector3(30f, 0f, 0f), 1));
            yield return Frames(4);
            if (mid == null || high == null) { Check(false, "F1 both platforms drop on the free field"); yield return Finish(playing0, speed0); yield break; }
            var mi = w.PlatformOf(mid.Id); var hi = w.PlatformOf(high.Id);

            // F1 the drop preview
            var spots = new[] { ("medium deck", Deck(mi, 5f, 5f)), ("medium ramp, upper half", Deck(mi, 5f, mi.Rows - mi.RampRows * 0.75f)), ("medium ramp, lower half", Deck(mi, 5f, mi.Rows - mi.RampRows * 0.25f)),
                                ("tower deck", Deck(hi, 5f, 5f)), ("tower ramp, upper half", Deck(hi, 5f, hi.Rows - hi.RampRows * 0.75f)), ("tower ramp, lower half", Deck(hi, 5f, hi.Rows - hi.RampRows * 0.25f)) };
            foreach (var (name, at) in spots)
            {
                bool can = OzUePlayground.CanDrop(OzUePlayground.Flag, Team.Neutral, at, 1, out string why);
                Check(can && why == null, "F1 the flag's preview on the " + name + ": " + (can ? "green" : "refused, " + why));
            }
            foreach (var id in new[] { OzUePlayground.Grenades, OzUePlayground.Obstacle })
            {
                bool can = OzUePlayground.CanDrop(id, Team.Neutral, Deck(hi, 5f, 5f), 1, out string why);
                Check(!can && why == "NO ROOM: NOT ON A PLATFORM", "F1 " + id + " on the tower's deck stays refused: " + why);
            }
            OzUeControls.CentreOn(Deck(hi, 5f, 5f)); yield return Seconds(0.4f);
            OzUePlace.ToggleArm(OzUePlayground.Flag, Team.Neutral); yield return Frames(3);
            Move(ToScreen(Deck(hi, 5f, 5f))); yield return Frames(4);
            yield return Shot("ghost_flag_on_tower_deck");
            {
                foreach (var (name, at) in new[] { ("tower deck", Deck(hi, 5f, 5f)), ("tower deck, near the rail", Deck(hi, 1.5f, 3f)), ("tower ramp, upper half", Deck(hi, 5f, hi.Rows - hi.RampRows * 0.75f)) })
                {
                    var sp = ToScreen(at);
                    if (sp.x < 4f || sp.y < 4f || sp.x > Screen.width - 4f || sp.y > Screen.height - 4f) { Info("G1 " + name + " off the screen, not checked"); continue; }
                    int g0 = OzUePlaceDriver.FlagGhosts;
                    Move(sp); yield return Frames(4);
                    var pc = OzUePlaceDriver.LastCentre; var ppx = ZGame.WorldToPx(pc); float want = (float)w.PlatformHeightM(ppx.x, ppx.y);
                    float off = (ToScreen(pc) - sp).magnitude;
                    Check(want > 0.5f && Mathf.Abs(pc.y - want) < 0.03f && off < 6f && OzUePlaceDriver.FlagGhosts > g0 && Mathf.Abs(OzUePlaceDriver.LastFlagFootY - pc.y) < 0.01f && OzUeMarks.PlatformWords == 0,
                        "G1 the armed flag's preview on the " + name + ": at " + pc.y.ToString("0.00") + " m (the rules' " + want.ToString("0.00") + " m), " + off.ToString("0.0") + " px from the pointer, ghost " + (OzUePlaceDriver.FlagGhosts > g0 ? "drawn" : "MISSING") + ", platform marks " + OzUeMarks.PlatformWords);
                }
                Move(ToScreen(Deck(hi, 5f, 5f))); yield return Frames(4);
                yield return Shot("ghost_flag_height_on_tower_deck");
            }
            OzUePlace.Stop(); yield return Frames(2);

            // F2 dropped on the tower's deck, on the medium ramp
            int rings0 = FlagView.DeckRings;
            var fTop = Lead(OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, Deck(hi, 1.5f, 4f), 1));                // the deck's first open column, one rail away from the floor
            var fRamp = Lead(OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, Deck(mi, 5f, mi.Rows - mi.RampRows * 0.5f), 1));
            yield return Seconds(0.6f);
            Stands(fTop, "tower's deck"); Stands(fRamp, "medium ramp");
            Check(FlagView.DeckRings - rings0 == 2, "F2 both capture rings drape on their platform (" + (FlagView.DeckRings - rings0) + " of 2)");
            var rt = FlagRoot(fTop);
            var picked = rt ? OzUeField.HoverProbe(ToScreen(rt.position + Vector3.up * 2f), false) : null;
            Check(picked == fTop, "F2 the pointer over the tower's flag points at the flag (" + (picked != null ? picked.Type : "nothing") + ")");
            yield return Shot("flag_on_tower_deck");
            OzUeControls.CentreOn(Deck(mi, 5f, mi.Rows - mi.RampRows * 0.5f)); yield return Seconds(0.4f);
            yield return Shot("flag_on_medium_ramp");

            // F3 a red tank onto the tower's flag; a blue robot beside the wall does not take it
            if (fTop != null)
            {
                var beside = Lead(OzUePlayground.Drop("grunt", Team.Blue, OzUePlatformViews.Point(hi, -0.5f, 4f, 0f), 1));   // the floor right outside that rail
                if (beside != null) beside.Damage = 0;
                foreach (var m in beside != null ? beside.Minions : new List<ZObject>()) if (m != null) m.Damage = 0;
                yield return Seconds(1.5f);
                Check(fTop.Owner == Team.Neutral && beside != null && w.PlatformAtTile(beside.CenterX >> 4, beside.CenterY >> 4) == 0,
                      "F3 a blue robot on the floor beside the tower's wall does not take the flag on the deck (owner " + fTop.Owner + ")");
                var tank = Lead(OzUePlayground.Drop("light", Team.Red, OzUePlatformViews.Point(hi, 5f, hi.Rows + 4f, 0f), 1));
                if (tank != null) tank.Damage = 0;
                yield return Frames(4);
                OzUeControls.CentreOn(Deck(hi, 5f, 9f)); yield return Seconds(0.3f);
                OzUePick.Pick(new[] { tank }, false); yield return Frames(3);
                yield return Click(ToScreen(rt ? rt.position : Deck(hi, 1.5f, 4f)), true);
                yield return Frames(4);
                string how3 = "the right-click";
                if (tank != null && tank.Waypoints.Count == 0)                                              // 1.5 pass 4: as F4 (injected clicks do not land in some editor sessions)
                {
                    w.SendWaypoints(Team.Red, tank, new[] { ZWaypoint.Move(fTop.CenterX, fTop.CenterY) });
                    how3 = "the rules' order (the injected click did not land)";
                }
                OzUeControls.SetSpeed(2f);
                float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 40f && fTop.Owner != Team.Red) yield return null;
                OzUeControls.SetSpeed(1f);
                Check(fTop.Owner == Team.Red, "F3 a red tank sent up the tower's ramp takes the flag on its deck (owner " + fTop.Owner + ", " + (Time.realtimeSinceStartup - t1).ToString("0.0") + " s; sent by " + how3 + ")");
                yield return Seconds(1.2f);
                yield return Shot("tower_flag_taken");
                if (tank != null) OzUePlayground.Remove(tank);
                if (beside != null) OzUePlayground.Remove(beside);
                yield return Frames(2);
            }

            // F4 a red robot up the medium ramp takes the flag on the ramp
            if (fRamp != null)
            {
                var gr = Lead(OzUePlayground.Drop("grunt", Team.Red, OzUePlatformViews.Point(mi, 5f, mi.Rows + 6f, 0f), 1));
                if (gr != null) gr.Damage = 0;
                yield return Frames(4);
                OzUePick.Pick(new[] { gr }, false); yield return Frames(3);
                var rr = FlagRoot(fRamp);
                yield return Click(ToScreen(rr ? rr.position : Deck(mi, 5f, mi.Rows - mi.RampRows * 0.5f)), true);
                yield return Frames(4);
                string how = "the right-click";
                if (gr != null && gr.Waypoints.Count == 0)                                                  // 1.5 pass 4: injected clicks do not land in some editor sessions (0 waypoints):
                {                                                                                            // the same order through the rules (a move onto the flag), as the click gives
                    w.SendWaypoints(Team.Red, gr, new[] { ZWaypoint.Move(fRamp.CenterX, fRamp.CenterY) });
                    how = "the rules' order (the injected click did not land)";
                }
                OzUeControls.SetSpeed(2f);
                float t2 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t2 < 40f && fRamp.Owner != Team.Red) yield return null;
                OzUeControls.SetSpeed(1f);
                Check(fRamp.Owner == Team.Red, "F4 a red robot sent up the medium ramp takes the flag on the ramp (owner " + fRamp.Owner + "; sent by " + how + ")");
                if (gr != null) OzUePlayground.Remove(gr);
                yield return Frames(2);
            }

            // F5 save and load
            {
                var tree = OzUePlayground.FieldToTree();
                int n = OzUePlayground.FieldFromTree(tree, out string err);
                yield return Seconds(0.6f);
                ZObject top2 = null, ramp2 = null;
                var ps = w.Platforms;
                foreach (var f in w.Flags)
                {
                    byte part = w.PlatformPartAt(f.CenterX >> 4, f.CenterY >> 4);
                    if (part == ZWorld.PlatPartDeck) top2 = f; else if (part == ZWorld.PlatPartRampHigh || part == ZWorld.PlatPartRampLow) ramp2 = f;
                }
                Check(err == null && ps.Count == 2 && w.Flags.Count == 2 && top2 != null && ramp2 != null && top2.Owner == Team.Red && ramp2.Owner == Team.Red,
                      "F5 loaded (" + n + " objects): two platforms, " + w.Flags.Count + " flags, one on a deck and one on a ramp, both red");
                if (top2 != null) Stands(top2, "deck after the load");
                if (ramp2 != null) Stands(ramp2, "ramp after the load");
                fTop = top2;
            }

            // F6 the tower destroyed: the flag comes down
            ZWorld.PlatformInfo hi2 = null;
            foreach (var p in w.Platforms) if (p.Kind == ZWorld.PlatformHigh) hi2 = p;
            if (hi2 != null && fTop != null && hi2.O.Contains(fTop.CenterX, fTop.CenterY))
            {
                int drops0 = FlagView.FloorDrops;
                OzUeControls.CentreOn(Deck(hi2, 5f, 5f)); yield return Seconds(0.3f);
                hi2.O.SetHealth(0); w.UpdateObjectHealth(hi2.O);
                yield return Seconds(2.5f);
                var r = FlagRoot(fTop); float y = r ? r.position.y : 99f, floor = r ? g.GroundY(r.position.x, r.position.z) : -99f;
                Check(InFlags(fTop) && !fTop.Destroyed && r && Mathf.Abs(y - (floor - 0.05f)) < 0.1f && y < 1f && FlagView.FloorDrops - drops0 == 1 && fTop.Owner == Team.Red,
                      "F6 the tower destroyed: the flag stays (owner " + fTop.Owner + "), its pole's foot at " + y.ToString("0.00") + " m, the floor " + floor.ToString("0.00") + " m");
                yield return Shot("tower_fallen_flag_on_floor");
            }
            else Check(false, "F6 the tower and its flag are there for the fall");

            // F7 Delete of a platform takes its flag; CLEAR FIELD leaves none
            ZWorld.PlatformInfo mi2 = null;
            foreach (var p in w.Platforms) if (p.Kind == ZWorld.PlatformMid) mi2 = p;
            ZObject onMid = null;
            if (mi2 != null) foreach (var f in w.Flags) if (mi2.O.Contains(f.CenterX, f.CenterY)) onMid = f;
            if (mi2 != null && onMid != null)
            {
                OzUePlayground.Remove(mi2.O); yield return Frames(4);
                Check(!InFlags(onMid) && FlagRoot(onMid) == null && w.Platforms.Count == 0, "F7 Delete of the medium platform takes the flag on its ramp off the field too");
            }
            else Check(false, "F7 the medium platform and its flag are there for the Delete");
            OzUePlayground.ClearField(); yield return Frames(4);
            Check(w.Flags.Count == 0, "F7 CLEAR FIELD: no flag left (" + w.Flags.Count + ")");
            yield return Finish(playing0, speed0);
        }

        IEnumerator Finish(bool playing0, float speed0)
        {
            OzUePlace.Stop(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            if (hidden0) OzUnitEditor.SetPanelsHidden(true);
            yield return Frames(2);
            var sb = new StringBuilder();
            sb.Append(Tag).Append(' ').Append(fail == 0 ? "PASS" : "FAIL").Append(": ").Append(pass).Append(" of ").Append(pass + fail).Append(" checks").AppendLine();
            sb.Append(lines);
            if (pictures.Count > 0) sb.Append("pictures: ").AppendLine(string.Join(", ", pictures));
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder))
            {
                try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), Report.Replace("\r\n", "\n")); }
                catch (System.Exception e) { Debug.LogWarning(Tag + " the report could not be written: " + e.Message); }
            }
            if (fail == 0) Debug.Log(Report); else Debug.LogError(Report);
            Destroy(gameObject);
        }
    }
}
#endif
