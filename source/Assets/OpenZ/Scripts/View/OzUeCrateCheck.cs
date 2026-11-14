#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 73 follow-up (editor only): the counted Play check of the GRENADES crate in the Unit editor, menu OpenZ/V1.2/Unit Editor/74c
    /// Crate Sounds And Delete Check (Editor/OzV12CrateSoundsMenu.cs; Play, the unit editor open). The playground's crate against the
    /// levels' crate, with real mouse and key events (routed to the game whatever window has focus, put back at the end) and the in-Play
    /// sound log (OzSoundLog):
    ///  C1 the drop: the crate's view is the levels' (Z_grenades bound in ZGame.StaticViews with GrenadeCrateV2, no UnitView), square on
    ///     its tile's centre, 3 cm into the ground; the drop's sounds played (the placement sound "placed" and ui_confirm);
    ///  C2 the pointer on it: the field's hover is the crate, its ring and two word lines; a left click shows it in the STATS box (the
    ///     crate's panel) with the click sound; Esc leaves it and the crate stays;
    ///  C3 a red grunt squad picked and sent onto it (right-click): the squad gets the crate's grenades; the lid opens with its sound
    ///     (crate_lid), then the pop with the ammo pickup sound (crate_pickup); the crate's end is quiet as in a level (no crate blast, no
    ///     rock sound, not the old fallback pickup bank); the crate is off the field and its view hidden after the pop;
    ///  C4 a crate clicked, then the Delete key: it is off the field, its view destroyed and out of StaticViews, the box back to the list,
    ///     the removal sound (ui_cancel) and no crate sound;
    ///  C5 a red heavy tank sent at a crate (right-click): the crate bursts as in a level (its pieces, its break sound crate_break), no rock
    ///     sound;
    ///  C6 CLEAR FIELD: no crate view is left (the spent ones too), none in StaticViews.
    /// The field is cleared before and after; the play state, the speed and the STATS box are put back. Hands off the mouse and the keyboard
    /// while it runs (about a minute). Folder (optional, set before Begin): report.txt, sound_log.txt and the pictures go there.
    /// </summary>
    public sealed class OzUeCrateCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ CrateCheck]";
        public static string Report = "not run";
        /// <summary>Optional: a folder for report.txt, sound_log.txt and the pictures (null = the console only).</summary>
        public static string Folder;
        public static bool Running => run;
        static OzUeCrateCheck run;
        int pass, fail;
        readonly StringBuilder lines = new StringBuilder();
        readonly List<string> pictures = new List<string>();
        Vector2 lastPos; bool lBtn, rBtn, hidden0;

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_CrateCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzUeCrateCheck>();
        }

        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { Route(false); if (run == this) run = null; }

        // the editor gives mouse and key events to the game only while a Game view has focus: for the check's events the input goes to
        // the game whatever window has focus (no window focus needed), put back at the end
        bool routed; InputSettings.EditorInputBehaviorInPlayMode route0; InputSettings.BackgroundBehavior back0;
        void Route(bool on)
        {
            var s = InputSystem.settings;
            if (on && !routed) { routed = true; route0 = s.editorInputBehaviorInPlayMode; back0 = s.backgroundBehavior; s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
            else if (!on && routed) { routed = false; s.editorInputBehaviorInPlayMode = route0; s.backgroundBehavior = back0; }
        }

        // ------------------------------------------------------------------------------------------------ helpers
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

        static void Keys(params Key[] k) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); }
        static IEnumerator Tap(Key k) { Keys(k); yield return Frames(3); Keys(); yield return Frames(3); }

        static Vector2 ToScreen(Vector3 world) { var cam = OzUeDraw.Cam; return cam ? (Vector2)cam.WorldToScreenPoint(world) : Vector2.zero; }
        static Vector2 UnitScreen(OzUnitEditor ed, ZObject o)
        {
            if (ed && ed.Input) { var r = ed.Input.ScreenRect(o); if (r.width > 0f) return r.center; }
            return ToScreen(OzUeMarks.Pos(ed.Game, o) + Vector3.up * 0.9f);
        }
        static Vector2 CrateScreen(ZGame g, ZObject o) => ToScreen(OzUeMarks.Pos(g, o) + Vector3.up * 0.4f);   // where the Play proof clicks the crate
        static ZObject Lead(UeDrop d) => d.ok && d.made != null && d.made.Count > 0 ? d.made[0] : null;
        static int SquadGrenades(ZObject o)
        {
            if (o == null) return 0;
            var lead = o.Leader ?? o; int n = lead.Grenades;
            foreach (var m in lead.Minions) if (m != null) n += m.Grenades;
            return n;
        }
        static bool Has(IReadOnlyList<ZObject> l, ZObject o) { if (l == null) return false; for (int i = 0; i < l.Count; i++) if (l[i] == o) return true; return false; }

        /// <summary>The floor point the camera keeps in the middle (as the Play proof's), inside the map.</summary>
        static Vector3 Focus(OzUnitEditor ed)
        {
            Vector3 f = ed && ed.Rts ? ed.Rts.focus : new Vector3(160, 0, -100);
            var map = ed && ed.World != null ? ed.World.Map : null;
            if (map != null) { f.x = Mathf.Clamp(f.x, 30f, map.width * 2f - 30f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 30f), -30f); }
            f.y = 0f; return f;
        }

        static int Count(List<OzSoundLog.Entry> es, string key)
        {
            int n = 0;
            foreach (var e in es) if (e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY", System.StringComparison.Ordinal)) n++;
            return n;
        }
        static float First(List<OzSoundLog.Entry> es, string key)
        {
            foreach (var e in es) if (e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY", System.StringComparison.Ordinal)) return e.t;
            return -1f;
        }
        static readonly string[] Keyed = { "placed", "ui_confirm", "ui_cancel", "crate_lid", "crate_pickup", "crate_break", "pickup", "rock", "explosion" };
        /// <summary>The sound log lines of the crate's keys (and the wrong ones), for the report.</summary>
        static string Lines(List<OzSoundLog.Entry> es)
        {
            var pick = new List<OzSoundLog.Entry>();
            foreach (var e in es) if (System.Array.IndexOf(Keyed, e.key) >= 0) pick.Add(e);
            if (pick.Count == 0) return "      (none of the crate's keys)\n";
            var sb = new StringBuilder();
            foreach (var l in OzSoundLog.Format(pick).Split('\n')) if (l.Trim().Length > 0) sb.Append("      ").AppendLine(l.TrimEnd());
            return sb.ToString();
        }

        IEnumerator Shot(string name)
        {
            if (string.IsNullOrEmpty(Folder)) yield break;
            Directory.CreateDirectory(Folder);
            string b = name + "_" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height, path = Path.Combine(Folder, b + ".png");
            int n = 1; while (File.Exists(path)) path = Path.Combine(Folder, b + "_" + (++n) + ".png");      // never over an older picture
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            pictures.Add(path);
        }

        // ------------------------------------------------------------------------------------------------ the run
        IEnumerator Main()
        {
            Report = "running";
            Route(true);
            float tStart = Time.unscaledTime;
            var ed = OzUnitEditor.I; var g = ed.Game; var W = ed.World;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed;
            var tab0 = OzUeStats.CurrentTab; bool box0 = OzUeStats.BoxOpen;
            hidden0 = OzUnitEditor.PanelsHidden; if (hidden0) OzUnitEditor.SetPanelsHidden(false);           // the STATS box must show (H hid the panels)
            OzUePlace.Stop();
            OzUeControls.SetPlaying(false); OzUeControls.SetSpeed(1f);
            OzUeStats.SetTab(OzUeStats.Tab.Stats);                                                       // the box open on its STATS tab (the crate's panel shows there)
            OzUePlayground.ClearField(); OzUePick.ClearInspect();
            var f = Focus(ed);
            OzUeControls.CentreOn(f);
            Move(ToScreen(f + new Vector3(0f, 0f, -6f)));
            yield return Seconds(1f);
            Info("the battle sound is " + (OzUeControls.SoundOn ? "ON" : "OFF (muted: the sound log still lists every sound, nothing is heard)") + "; the prefab " + OzUeCrateViews.PrefabName + (g.Prefab(OzUeCrateViews.PrefabName) ? " found" : " MISSING")
                 + "; Game view " + UnityEngine.Screen.width + " x " + UnityEngine.Screen.height);

            // ---- C1 the drop (alone in its frame: two ui_confirm in 0.05 s are one sound)
            float t1 = Time.unscaledTime;
            var crate = Lead(OzUePlayground.Drop(OzUePlayground.Grenades, Team.Neutral, f + new Vector3(4f, 0f, 0f), 1));
            yield return Frames(8);
            if (crate == null) { Check(false, "C1 GRENADES drops a crate (refused: the field's centre has no room)"); yield return Finish(tStart, playing0, speed0, tab0, box0); yield break; }
            var go = OzUeCrateViews.Of(crate);
            var cv = go ? go.GetComponent<GrenadeCrateV2>() : null;
            Vector3 c = g.CenterWorld(crate);
            float off = go ? new Vector2(go.transform.position.x - c.x, go.transform.position.z - c.z).magnitude : -1f, dy = go ? go.transform.position.y - c.y : 99f;
            int inBox = crate.Grenades;
            Check(OzUeCrateViews.LevelView(g, crate) && off >= 0f && off < 0.01f && Mathf.Abs(dy + OzUeCrateViews.SinkM) < 0.005f && go.transform.rotation == Quaternion.identity && inBox > 0,
                  "C1 GRENADES drops the crate with the levels' view: " + (go ? go.name : "none") + " in StaticViews " + g.StaticViews.ContainsKey(crate.Id) + ", in the unit views " + g.Views.ContainsKey(crate.Id)
                  + ", GrenadeCrateV2 " + (cv ? (cv.IsV2 ? "v2 (lid)" : "v1") : "none") + ", " + off.ToString("0.000", CultureInfo.InvariantCulture) + " m off its tile's centre, " + dy.ToString("+0.000;-0.000", CultureInfo.InvariantCulture) + " m under the ground (level rule -0.030), " + inBox + " grenades in it");
            int placed = Count(OzSoundLog.Since(t1), PlacementPop.PlacedKey), confirm = Count(OzSoundLog.Since(t1), "ui_confirm");
            Check(placed >= 1 && confirm >= 1, "C1 the drop's sounds played: the placement sound \"" + PlacementPop.PlacedKey + "\" " + placed + "x, ui_confirm " + confirm + "x (the levels have no crate drop: their crates stand from the start)");
            yield return Shot("crate_dropped");

            // ---- C2 the pointer on it, a left click shows it, Esc leaves it
            var cs = CrateScreen(g, crate);
            Move(cs); yield return Frames(8);
            bool hov = OzUePick.Hover == crate; int words = OzUeMarks.CrateWords, rings = OzUeMarks.CrateRings; string top = OzUeMarks.LastCrateTop;
            Check(hov && words == 2 && rings >= 1 && top == OzUeMarks.CrateName + "  " + inBox + " grenades",
                  "C2 the pointer on the crate: the field's hover is the crate (" + hov + "), its ring " + rings + "x, its words " + words + " lines (\"" + top + "\" over \"" + OzUeMarks.CrateHint + "\")");
            int setups0 = OzUeObjectPanel.CrateSetups; float tc = Time.unscaledTime;
            yield return Click(cs); yield return Frames(8);
            bool shown = OzUePick.Kind == UeInspect.Crate && OzUePick.Inspected == crate && OzUePick.InspectedId == OzUePlayground.Grenades;
            string live = OzUeObjectPanel.LastCrateLive ?? "";
            Check(shown && OzUeObjectPanel.CrateSetups > setups0 && live.StartsWith(inBox + " grenades", System.StringComparison.Ordinal),
                  "C2 a left click on it shows it in the STATS box: inspected " + OzUePick.Kind + (OzUePick.Inspected != null ? " #" + OzUePick.Inspected.Id : "") + ", the crate's panel set up " + (OzUeObjectPanel.CrateSetups - setups0) + "x, its live line \"" + live + "\"");
            int clickSound = Count(OzSoundLog.Since(tc), OzUeField.ObjectClickSound);
            Check(clickSound >= 1, "C2 the click that shows the crate has its sound: " + OzUeField.ObjectClickSound + " " + clickSound + "x (as a dock tile's; the flag, the obstacle and empty hardware the same)");
            yield return Shot("crate_picked");
            yield return Tap(Key.Escape); yield return Frames(4);
            Check(OzUePick.Kind == UeInspect.None && OzUePlayground.OnField(OzUePlayground.Grenades) == 1 && OzUeCrateViews.Of(crate),
                  "C2 Esc leaves the crate's panel (inspected " + OzUePick.Kind + "), the crate stays on the field (" + OzUePlayground.OnField(OzUePlayground.Grenades) + ")");
            Move(ToScreen(f + new Vector3(0f, 0f, -6f))); yield return Frames(3);

            // ---- C3 a red grunt squad sent onto it picks it up: the lid, the pop with the pickup sound, a quiet end
            var red = Lead(OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-8f, 0f, 0f), 1));
            yield return Frames(8);
            if (red == null) Check(false, "C3 a red grunt squad drops beside the crate");
            else
            {
                int g0 = SquadGrenades(red), lid0 = GrenadeCrateV2.LidOpens, pops0 = GrenadeCrateV2.Pops, pd0 = GrenadeCrateV2.PickDeaths, b0 = GrenadeCrateV2.Bursts;
                yield return Click(UnitScreen(ed, red)); yield return Frames(4);
                bool picked = Has(OzUePick.Picked, red);
                float t3 = Time.unscaledTime;
                yield return Click(CrateScreen(g, crate), true);
                OzUeControls.SetPlaying(true);
                float w0 = Time.realtimeSinceStartup;
                while (!(crate.Destroyed || crate.KillAt >= 0 || SquadGrenades(red) > g0) && Time.realtimeSinceStartup - w0 < 40f) yield return null;
                yield return Shot("crate_lid_opening");
                yield return Seconds(2f);
                OzUeControls.SetPlaying(false);
                int g1 = SquadGrenades(red), onField = OzUePlayground.OnField(OzUePlayground.Grenades);
                var heard = OzSoundLog.Since(t3);
                float lidAt = First(heard, "crate_lid"), popAt = First(heard, "crate_pickup");
                int lidN = Count(heard, "crate_lid"), popN = Count(heard, "crate_pickup"), rock = Count(heard, "rock"), fallback = Count(heard, "pickup"), blast = Count(heard, "explosion");
                Check(picked && g1 - g0 == inBox && onField == 0,
                      "C3 a red grunt squad picked (" + picked + ") and sent onto the crate (right-click) picks it up: its grenades " + g0 + " -> " + g1 + " (the crate held " + inBox + "), crates on the field " + onField);
                Check(lidN == 1 && popN == 1 && lidAt >= 0f && popAt >= lidAt && popAt - lidAt < 1f && GrenadeCrateV2.LidOpens == lid0 + 1 && GrenadeCrateV2.Pops == pops0 + 1 && GrenadeCrateV2.PickDeaths == pd0 + 1 && GrenadeCrateV2.Bursts == b0,
                      "C3 the crate's sounds as in a level: the lid (crate_lid) " + lidN + "x, then the pop with the ammo pickup sound (crate_pickup) " + popN + "x, " + (lidAt >= 0f && popAt >= 0f ? (popAt - lidAt).ToString("0.00", CultureInfo.InvariantCulture) : "-") + " s after it; the lid opened "
                      + (GrenadeCrateV2.LidOpens - lid0) + "x, the pop " + (GrenadeCrateV2.Pops - pops0) + "x, the collected crate's quiet end " + (GrenadeCrateV2.PickDeaths - pd0) + "x, bursts " + (GrenadeCrateV2.Bursts - b0) + "x");
                Check(rock == 0 && fallback == 0 && blast == 0 && go && !go.activeSelf,
                      "C3 no wrong sound at the pickup: rock " + rock + "x, the old fallback pickup bank " + fallback + "x, a crate blast " + blast + "x; the crate's view hidden after its pop (" + (go ? (go.activeSelf ? "still shown" : "hidden") : "destroyed") + ")");
                lines.Append("      the sound log from the order on (the crate's keys):\n").Append(Lines(heard));
                yield return Shot("crate_collected");
            }

            // ---- C4 a crate clicked, then Delete
            OzUePlayground.ClearField(); OzUePick.ClearInspect(); yield return Frames(4);
            var crate2 = Lead(OzUePlayground.Drop(OzUePlayground.Grenades, Team.Neutral, f + new Vector3(2f, 0f, -2f), 1));
            yield return Frames(8);
            if (crate2 == null) Check(false, "C4 a second crate drops");
            else
            {
                var go2 = OzUeCrateViews.Of(crate2);
                yield return Click(CrateScreen(g, crate2)); yield return Frames(6);
                bool shown2 = OzUePick.Kind == UeInspect.Crate && OzUePick.Inspected == crate2;
                int removed0 = OzUePlayground.Removed, cratesRemoved0 = OzUeCrateViews.Removed; float t4 = Time.unscaledTime;
                yield return Tap(Key.Delete); yield return Frames(6);
                var h4 = OzSoundLog.Since(t4);
                bool offField = OzUePlayground.OnField(OzUePlayground.Grenades) == 0 && W.Get(crate2.Id) == null;
                bool viewGone = !go2 && !g.StaticViews.ContainsKey(crate2.Id) && OzUeCrateViews.Of(crate2) == null && OzUeCrateViews.Removed == cratesRemoved0 + 1;
                Check(shown2 && offField && viewGone && OzUePick.Kind == UeInspect.None && OzUePlayground.Removed == removed0 + 1,
                      "C4 a crate clicked (shown " + shown2 + "), then Delete: off the field (" + offField + "), its view destroyed and out of StaticViews (" + viewGone + "), the box back to the list (" + OzUePick.Kind + "), removed " + (OzUePlayground.Removed - removed0) + " object");
                int crateSounds = Count(h4, "crate_lid") + Count(h4, "crate_pickup") + Count(h4, "crate_break") + Count(h4, "explosion") + Count(h4, "rock");
                Check(Count(h4, "ui_cancel") >= 1 && crateSounds == 0, "C4 the removal's sound ui_cancel " + Count(h4, "ui_cancel") + "x; no crate sound (" + crateSounds + "): Delete takes it off, it is not destroyed");
            }

            // ---- C5 a red heavy tank shoots a crate: the levels' burst
            OzUePlayground.ClearField(); OzUePick.ClearInspect(); yield return Frames(4);
            var crate3 = Lead(OzUePlayground.Drop(OzUePlayground.Grenades, Team.Neutral, f + new Vector3(6f, 0f, 0f), 1));
            var heavy = Lead(OzUePlayground.Drop("heavy", Team.Red, f + new Vector3(-12f, 0f, 0f), 1));
            yield return Frames(8);
            if (crate3 == null || heavy == null) Check(false, "C5 a crate and a red heavy tank drop");
            else
            {
                var go3 = OzUeCrateViews.Of(crate3);
                int bursts0 = GrenadeCrateV2.Bursts, pieces0 = GrenadeCrateV2.PiecesThrown, flown0 = GrenadeCrateV2.GrenadesFlown;
                yield return Click(UnitScreen(ed, heavy)); yield return Frames(4);
                float t5 = Time.unscaledTime;
                yield return Click(CrateScreen(g, crate3), true);
                OzUeControls.SetPlaying(true);
                float w5 = Time.realtimeSinceStartup;
                while (!(crate3.Destroyed || crate3.KillAt >= 0) && Time.realtimeSinceStartup - w5 < 45f) yield return null;
                bool blown = crate3.Destroyed || crate3.KillAt >= 0;
                yield return Seconds(0.4f);
                yield return Shot("crate_burst");
                yield return Seconds(4f);                                                                  // its grenades land and burst
                OzUeControls.SetPlaying(false);
                var h5 = OzSoundLog.Since(t5);
                Check(blown && GrenadeCrateV2.Bursts == bursts0 + 1 && GrenadeCrateV2.PiecesThrown > pieces0 && Count(h5, "crate_break") == 1 && Count(h5, "rock") == 0 && go3 && !go3.activeSelf,
                      "C5 a red heavy tank sent at a crate blows it up as in a level: blown " + blown + ", the burst " + (GrenadeCrateV2.Bursts - bursts0) + "x with " + (GrenadeCrateV2.PiecesThrown - pieces0) + " pieces and "
                      + (GrenadeCrateV2.GrenadesFlown - flown0) + " flying grenades, its break sound (crate_break) " + Count(h5, "crate_break") + "x, rock " + Count(h5, "rock") + "x, blasts " + Count(h5, "explosion") + "x, its view hidden " + (go3 && !go3.activeSelf));
                lines.Append("      the sound log from the order on (the crate's keys):\n").Append(Lines(h5));
            }

            // ---- C6 CLEAR FIELD takes every crate view, the spent ones too
            int held = OzUeCrateViews.Count, swept0 = OzUeCrateViews.Swept;
            OzUePlayground.ClearField(); yield return Frames(4);
            int inStatic = 0;
            foreach (var kv in g.StaticViews) if (kv.Value && kv.Value.GetComponent<GrenadeCrateV2>()) inStatic++;
            Check(held >= 1 && OzUeCrateViews.Count == 0 && inStatic == 0 && OzUeCrateViews.Swept > swept0,
                  "C6 CLEAR FIELD: crate views held before " + held + " (the spent ones hidden), after " + OzUeCrateViews.Count + ", swept " + (OzUeCrateViews.Swept - swept0) + ", crate views in StaticViews " + inStatic);

            yield return Finish(tStart, playing0, speed0, tab0, box0);
        }

        IEnumerator Finish(float tStart, bool playing0, float speed0, OzUeStats.Tab tab0, bool box0)
        {
            OzUePick.ClearInspect();
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            OzUeStats.SetTab(tab0); if (!box0) OzUeStats.SetBox(false);
            if (hidden0) OzUnitEditor.SetPanelsHidden(true);
            yield return Frames(2);
            var sb = new StringBuilder();
            sb.Append(Tag).Append(" COUNTED CHECK 74c ").Append(fail == 0 ? "PASS" : "FAIL").Append(": ").Append(pass).Append(" of ").Append(pass + fail).Append(" checks").AppendLine();
            sb.Append(lines);
            if (pictures.Count > 0) sb.Append("pictures: ").AppendLine(string.Join(", ", pictures));
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder))
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(Path.Combine(Folder, "report.txt"), Report.Replace("\r\n", "\n"));
                    File.WriteAllText(Path.Combine(Folder, "sound_log.txt"), OzSoundLog.Format(OzSoundLog.Since(tStart)).Replace("\r\n", "\n"));
                }
                catch (System.Exception e) { Debug.LogWarning(Tag + " the report could not be written: " + e.Message); }
            }
            if (fail == 0) Debug.Log(Report); else Debug.LogError(Report);
            Destroy(gameObject);
        }
    }
}
#endif
