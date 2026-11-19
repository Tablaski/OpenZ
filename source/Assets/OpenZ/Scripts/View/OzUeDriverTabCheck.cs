#if UNITY_EDITOR && !OZ_RELEASE
// OpenZ (GPL-3.0) - version 1.5 kits ue_exit_button + ue_exit_placement: the Play check (editor only, never in a build).
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
    /// Version 1.5 kits ue_exit_button + ue_exit_placement: the Play check of the Unit editor's EXIT DRIVER button, now in the picked-unit
    /// card of the STATS box under REMOVE (UI/OzUeDriverTab.cs, UI/OzUeInspector.ExitDriver.cs, UI/OzUeExitColumn.cs), run from the menu
    /// OpenZ/V1.5/UE Exit Driver Tab (Editor/OzV15UeDriverTabMenu.cs) while the Unit editor is open in Play (normal mode, not 'Zod exact').
    /// It drops its own units on free ground at the view's centre, picks them by code (PlayerInput.Selection, as a click does), hovers and
    /// clicks the button with real mouse events, presses the bound EXIT CREW key with a real key event, steps UI SCALE through every preset,
    /// then puts back the scale, the box, its tab and the table and removes everything it made.
    /// <list type="bullet">
    /// <item>0 the button is built in the card's head (the inspector of the STATS box); nothing on the play bar.</item>
    /// <item>1 each hatch vehicle RED, picked alone (light, medium, heavy tank, mech, MAMMOTH, BIZON): the button shows, live, the game's
    /// words, the game's tooltip (as the editor's side tip, no HUD tooltip); in its place: right under REMOVE, right edges equal, inside the
    /// head and the box, over no line of the head (name, chip, class line, live line, HP bar), the portrait or the words; the hover: the side
    /// tip LEFT of the box, over neither the dock, the play bar nor the zoom; a frame; the click: the HUD click sound, the vehicle neutral,
    /// its driver out beside it, the button gone and REMOVE back in its old place.</item>
    /// <item>2 a BLUE light tank: the button shows; the EXIT CREW key (E) lets the driver out.</item>
    /// <item>3 an empty NEUTRAL light tank picked: no button.  4 a crewed RED gatling and a RED light tank picked together: no button.</item>
    /// <item>5 every UI SCALE preset with a RED light tank picked: the place rules of row 1 hold (the card at that scale, normal or compact);
    /// the box closed: the button hides with it and no EXIT DRIVER stands anywhere else.</item>
    /// <item>6 the mirror: every frame the button shows exactly when the game's tab shows and the card shows one of those picked tanks.</item>
    /// </list>
    /// Report: proof/play/play_check_report.txt of kit ue_exit_placement; frames ue_exit_driver_card_&lt;type&gt;.png beside it.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class OzUeDriverTabCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.5 UE exit driver card]";
        public static string Report = "not run", Folder;
        public static bool Running;
        /// <summary>The wait after a pick (the game holds the tab back 0.4 s after the click that picked), after the click / key (the jump
        /// takes about 1.5 s), after a UI SCALE step (the box is rebuilt), and the longest wait for the button to be built.</summary>
        public static float ShowWaitSec = 0.7f, ExitWaitSec = 3f, ScaleWaitSec = 0.9f, BuildWaitSec = 5f;

        OpenZHud hud; PlayerInput input; ZWorld W; OzUeDriverTab tab;
        readonly StringBuilder sb = new StringBuilder(); int pass, total;
        readonly List<ZObject> made = new List<ZObject>();
        int startMaxId; bool watching; int frames, mismatch, strays;

        static string Kind(string t) => t == "light" ? "light tank" : t == ZMTank.Type ? "medium tank" : t == "heavy" ? "heavy tank" : t == ZMTank.Mech ? "mech" : t;

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OZ_UeDriverTabCheck").AddComponent<OzUeDriverTabCheck>();
        }

        IEnumerator Start()
        {
            Running = true; Report = "running";
            bool playing0 = OzUeControls.Playing, sound0 = OzUeControls.SoundOn; float speed0 = OzUeControls.Speed;
            bool box0 = OzUeStats.BoxOpen, table0 = OzUeStats.TableOpen; var tab0 = OzUeStats.CurrentTab; float scale0 = OzText.UiScale;
            try
            {
                var ed = OzUnitEditor.I;
                hud = OpenZHud.I; input = ed ? ed.Input : null; W = ed ? ed.World : null;
                if (!ed || !hud || !input || W == null) { Log("0 the Unit editor is open in Play", false, "no OzUnitEditor / HUD / PlayerInput / world"); yield break; }
                OzUeStats.SetTable(false); OzUeStats.SetTab(OzUeStats.Tab.Stats);                            // the card shows in the STATS tab of the open box
                float t0 = Time.unscaledTime;
                while ((tab = OzUeDriverTab.I) == null || tab.Button == null || !tab.Button.Rt)
                {
                    if (Time.unscaledTime - t0 > BuildWaitSec) break;
                    yield return null;
                }
                bool built = tab != null && tab.Button != null && tab.Button.Rt;
                var parent = built ? tab.Button.Rt.parent : null;
                bool inHead = parent && parent.name == "Head" && parent.parent && parent.parent.name == "Inspector";
                int onBar = PlayBarTabs();
                Log("0 the button is built in the picked-unit card (the STATS box's inspector head), nothing on the play bar", built && inHead && onBar == 0 && ZWorld.VehicleExitDeviation,
                    $"button {(built ? "built under " + Path(tab.Button.Rt) : "MISSING")}; in the card's head {inHead}; EXIT DRIVER nodes under the play bar {onBar}; "
                    + $"EXIT DRIVER (DV171) {(ZWorld.VehicleExitDeviation ? "on" : "OFF ('Zod exact': run in the normal mode)")}; builds {OzUeDriverTab.Builds}");
                if (!built || !ZWorld.VehicleExitDeviation) yield break;
                OzUeControls.SetPlaying(true); OzUeControls.SetSpeed(1f); OzUeControls.SetSound(true);
                startMaxId = W.Objects.Count > 0 ? W.Objects.Max(o => o.Id) : 0;
                if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
                input.Selection.Clear();
                watching = true;
                yield return Rows();
                watching = false;
                Log("6 the mirror: the button shows exactly when the game's tab shows and the card shows one of those picked tanks, every frame; never an EXIT DRIVER elsewhere",
                    frames > 0 && mismatch == 0 && strays == 0, $"{frames} frames, {mismatch} different, {strays} frames with an EXIT DRIVER outside the card");
            }
            finally
            {
                watching = false;
                Cleanup();
                if (Mathf.Abs(OzText.UiScale - scale0) > 0.001f && hud) { OzText.UiScale = scale0; hud.ApplyUiScale(); }
                OzUeStats.SetTab(tab0); OzUeStats.SetBox(box0); OzUeStats.SetTable(table0);
                OzUeControls.SetSpeed(speed0); OzUeControls.SetSound(sound0); OzUeControls.SetPlaying(playing0);
                Report = $"{Tag}: {(pass == total && total > 0 ? "PASS" : "FAIL")} {pass}/{total}\n" + sb;
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(System.IO.Path.Combine(Folder, "play_check_report.txt"), Report); } catch { } }
                Debug.Log(Report);
                Running = false; Destroy(gameObject);
            }
        }

        void Log(string what, bool ok, string detail) { total++; if (ok) pass++; sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {what}: {detail}"); }
        void Note(string s) => sb.AppendLine("      " + s);
        static IEnumerator Real(float s) { yield return new WaitForSecondsRealtime(s); }

        void LateUpdate()
        {
            if (!watching || tab == null || !hud) return;
            var b = tab.Button; var ins = OzUeDriverTab.Inspector;
            if (b == null || !b.Rt || ins == null || ins.Head == null) return;
            frames++;
            bool want = ins.Shown && ins.Head.Rt.gameObject.activeInHierarchy && OzUeDriverTab.CardWants(hud);
            if (b.Rt.gameObject.activeSelf != want) mismatch++;
            if (PlayBarTabs() > 0) strays++;
        }

        // ------------------------------------------------------------------ the rows
        IEnumerator Rows()
        {
            var types = new List<string> { "light", ZMTank.Type, "heavy", ZMTank.Mech };
            foreach (var big in new[] { "mammoth", "bizon" })
            {
                if (System.Array.IndexOf(OzUePlayground.Vehicles, big) < 0) { Note($"no {big.ToUpperInvariant()} in this project's Unit editor: its row is skipped"); continue; }
                if (OzUePlayground.Offered(big, out var why)) types.Add(big); else Note($"{big.ToUpperInvariant()} not offered: {why}");
            }
            foreach (var t in types) yield return ClickRow(t);
            yield return KeyRow();
            yield return NeutralRow();
            yield return GunAndTankRow();
            yield return ScaleRow();
        }

        IEnumerator ClickRow(string type)
        {
            string name = Kind(type);
            var v = DropOne(type, Team.Red);
            if (v == null) { Log($"1 {name} RED", false, "no free ground for it at the view's centre"); yield break; }
            yield return Real(0.3f);
            Pick(v);
            yield return Real(ShowWaitSec);
            var b = tab.Button;
            bool shown = b.Rt.gameObject.activeInHierarchy, game = hud.DriverTabShown, live = b.Interactable, liveGame = hud.DriverTabLive;
            var head = OzUeDriverTab.Inspector.Head;
            string tip = head.ExitTip ?? "", want = OzUeDriverTab.SideTip(hud.DriverTabTip);
            bool tipSame = tip == want && tip.Contains("EXIT DRIVER") && b.Tooltip == null;
            string label = b.Label != null ? b.Label.text : "";
            bool placed = PlaceOk(out string where);
            Log($"1 {name} RED, picked alone: the button shows in the card, live, the game's words and tooltip, in its place",
                shown && game && live && liveGame && tipSame && label == OzUeDriverTab.Label && placed,
                $"shown {shown} (the game's tab {game}), live {live} (the game's {liveGame}), label '{label}', side tip the game's {tipSame} ('{Short(tip)}', HUD tooltip {(b.Tooltip == null ? "none" : "SET")}); {where}");
            Vector2 c = Centre(b.Rt);
            int tips0 = OzUeSideTip.Shown;
            MouseAt(c, false); yield return Real(0.7f);                                                     // the side tip comes after 0.35 s
            bool tipOk = TipOk(tips0, out string tipWhere);
            Log($"1 {name} RED: the tooltip opens LEFT of the STATS box, over neither the dock, the play bar nor the zoom", tipOk, tipWhere);
            Shot("ue_exit_driver_card_" + type + ".png"); yield return null; yield return null;
            float t0 = Time.unscaledTime; int clicks0 = OzUeDriverTab.Clicks;
            MouseAt(c, false); yield return null; yield return null;
            MouseAt(c, true); yield return null; yield return null;
            MouseAt(c, false); yield return null; yield return null; yield return null;
            var sounds = OzSoundLog.Since(t0);
            bool click = sounds.Any(e => e.key == OpenZHud.ClickSound || (e.clip ?? "").Contains(OpenZHud.ClickSound));
            yield return Real(ExitWaitSec);
            int outBy = DriversOut(v, Team.Red);
            bool gone = !b.Rt.gameObject.activeSelf;
            var head2 = OzUeDriverTab.Inspector.Head;
            OzUeExitColumn.Lay(head2.Rt.sizeDelta.x, head2.Rt.sizeDelta.y, head2.Rt.sizeDelta.y < 60f, false, 0f, out var r0, out _);
            var rp = head2.Remove.Rt;
            bool removeBack = !head2.Remove.Rt.gameObject.activeSelf || (Mathf.Abs(rp.anchoredPosition.x - r0.x) < 0.5f && Mathf.Abs(-rp.anchoredPosition.y - r0.y) < 0.5f && Mathf.Abs(rp.sizeDelta.y - r0.h) < 0.5f);
            var all = OzSoundLog.Since(t0).Where(e => (e.outcome ?? "").StartsWith("PLAY")).Select(e => e.key ?? e.clip).Distinct().ToList();
            Log($"1 {name} RED: a click on the button lets the driver out",
                OzUeDriverTab.Clicks == clicks0 + 1 && click && v.Owner == Team.Neutral && v.Drivers.Count == 0 && outBy > 0 && gone && removeBack,
                $"clicks {OzUeDriverTab.Clicks - clicks0}, the HUD click sound '{OpenZHud.ClickSound}' {click}, the {name} {v.Owner} with {v.Drivers.Count} driver(s), "
                + $"red robots out beside it {outBy}, the button gone {gone}, REMOVE back in its old place {removeBack}; sounds played after the click: {string.Join(", ", all)}");
            input.Selection.Clear();
            MouseAt(Centre(null), false);
            yield return Real(0.3f);
        }

        IEnumerator KeyRow()
        {
            var v = DropOne("light", Team.Blue);
            if (v == null) { Log("2 light tank BLUE", false, "no free ground for it"); yield break; }
            yield return Real(0.3f);
            Pick(v);
            yield return Real(ShowWaitSec);
            bool shown = tab.Button.Rt.gameObject.activeInHierarchy && tab.Button.Interactable;
            bool placed = PlaceOk(out string where);
            var k = OzBindings.KeyOf(OzAction.ExitCrew);
            int exits0 = W.DriverExits;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); yield return null; yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); yield return null; yield return null;
            yield return Real(ExitWaitSec);
            int outBy = DriversOut(v, Team.Blue), letOut = W.DriverExits - exits0;
            Log($"2 light tank BLUE (both teams are the player's here): the button shows in its place; the key {OzBindings.Cap(OzAction.ExitCrew)} lets the driver out",
                shown && placed && v.Owner == Team.Neutral && (outBy > 0 || letOut > 0),
                $"button shown and live {shown}, in its place {placed}, the tank {v.Owner}, blue robots out beside it {outBy}, drivers let out by the rules {letOut}; {where}");
            input.Selection.Clear();
            yield return Real(0.3f);
        }

        IEnumerator NeutralRow()
        {
            var v = DropOne("light", Team.Neutral);
            if (v == null) { Log("3 light tank NEUTRAL (empty)", false, "no free ground for it"); yield break; }
            yield return Real(0.3f);
            Pick(v);
            yield return Real(ShowWaitSec);
            bool shown = tab.Button.Rt.gameObject.activeSelf;
            Log("3 an empty NEUTRAL light tank picked: no button", !shown && !hud.DriverTabShown, $"button shown {shown}, the game's tab {hud.DriverTabShown}, drivers {v.Drivers.Count}");
            input.Selection.Clear();
            yield return Real(0.2f);
        }

        IEnumerator GunAndTankRow()
        {
            var g = DropOne("gatling", Team.Red); var v = DropOne("light", Team.Red);
            if (g == null || v == null) { Log("4 a gatling gun and a light tank RED", false, "no free ground for them"); yield break; }
            yield return Real(0.3f);
            input.Selection.Clear(); input.Selection.Add(g); input.Selection.Add(v);
            yield return Real(ShowWaitSec);
            bool shown = tab.Button.Rt.gameObject.activeSelf;
            Log("4 a crewed gun and a tank picked together: no EXIT DRIVER button (as in a match: EXIT CREW has the place, its key lets the gun's crew out)",
                !shown && !hud.DriverTabShown, $"button shown {shown}, the game's tab {hud.DriverTabShown}");
            input.Selection.Clear();
            yield return Real(0.2f);
        }

        IEnumerator ScaleRow()
        {
            var v = DropOne("light", Team.Red);
            if (v == null) { Log("5 every UI SCALE step", false, "no free ground for a light tank"); yield break; }
            yield return Real(0.3f);
            Pick(v);
            var bad = new List<string>(); var seen = new List<string>();
            foreach (float s in OzText.ScalePresets)
            {
                OzText.UiScale = s; hud.ApplyUiScale(); yield return Real(ScaleWaitSec);
                if (!input.Selection.Contains(v)) { Pick(v); yield return Real(ShowWaitSec); }
                var b = tab.Button;
                var ins = OzUeDriverTab.Inspector;
                bool shown = b != null && b.Rt && b.Rt.gameObject.activeInHierarchy;
                bool ok = PlaceOk(out string where);
                var cs = OzUnitEditor.CanvasSize;
                string st = $"{s * 100f:0} %: canvas {cs.x:0} x {cs.y:0}, card {(ins != null && ins.Head != null ? ins.Head.Rt.sizeDelta.x.ToString("0.#") + " x " + ins.Head.Rt.sizeDelta.y.ToString("0") : "?")}";
                seen.Add(st);
                if (!shown || !ok) bad.Add(st + (shown ? "" : " NOT SHOWN") + "; " + where);
            }
            Log("5 every UI SCALE step (" + string.Join(" / ", OzText.ScalePresets.Select(x => (x * 100f).ToString("0"))) + " %) at this screen: the button in its place, over nothing",
                bad.Count == 0, bad.Count == 0 ? string.Join("; ", seen) : string.Join(" | ", bad));
            OzUeStats.SetBox(false); yield return Real(0.4f);
            bool hidden = tab.Button == null || !tab.Button.Rt.gameObject.activeInHierarchy; int elsewhere = ActiveExitNodes();
            OzUeStats.SetBox(true); yield return Real(ShowWaitSec);
            bool back = tab.Button != null && tab.Button.Rt.gameObject.activeInHierarchy;
            Log("5 the box closed: the button hides with it and no EXIT DRIVER stands anywhere else; opened again: back", hidden && elsewhere == 0 && back,
                $"hidden {hidden}, EXIT DRIVER buttons shown elsewhere {elsewhere}, back with the box {back}");
            input.Selection.Clear();
            yield return Real(0.2f);
        }

        // ------------------------------------------------------------------ helpers
        void Pick(ZObject v) { input.Selection.Clear(); input.Selection.Add(v); }

        ZObject DropOne(string id, Team team)
        {
            var cam = OzUeCamera.I;
            if (!cam || !cam.FreeCentreGround(out var c)) return null;
            for (int r = 0; r < 7; r++)
                for (int k = 0; k < (r == 0 ? 1 : 8); k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (r * 6f);
                    if (!OzUePlayground.CanDrop(id, team, p, 1, out _)) continue;
                    var d = OzUePlayground.Drop(id, team, p, 1);
                    if (d.ok && d.made != null && d.made.Count > 0) { made.AddRange(d.made); return d.made[0]; }
                }
            return null;
        }

        /// <summary>Robots of team made during the check that stand within 6 tiles of the vehicle (its driver out).</summary>
        int DriversOut(ZObject v, Team team)
        {
            int n = 0;
            foreach (var o in W.Objects)
                if (o.Kind == ZKind.Robot && o.Owner == team && o.Id > startMaxId && !made.Contains(o)
                    && (o.DriverOutFromId == v.Id || (!o.Destroyed && Mathf.Abs(o.CenterX - v.CenterX) < 96 && Mathf.Abs(o.CenterY - v.CenterY) < 96))) n++;
            return n;
        }

        /// <summary>EXIT DRIVER nodes under the play bar's plate (the old tab): must be none.</summary>
        static int PlayBarTabs()
        {
            var panels = OzUnitEditor.Layer(UeLayer.Panels);
            var plate = panels ? panels.Find("UE_PlayBar") : null;
            if (!plate) return 0;
            int n = 0;
            foreach (var t in plate.GetComponentsInChildren<RectTransform>(true)) if (t.name == "UE_DriverExit") n++;
            return n;
        }

        /// <summary>Shown EXIT DRIVER buttons of the editor other than the card's (the HUD's own hidden tab is "DriverExit").</summary>
        int ActiveExitNodes()
        {
            int n = 0; var mine = tab.Button != null ? tab.Button.Rt : null;
            foreach (var t in FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                if (t.name == "UE_DriverExit" && t != mine && t.gameObject.activeInHierarchy) n++;
            return n;
        }

        /// <summary>The button in the card: right under REMOVE (right edges equal, the column's gap), inside the head and the STATS box, over no
        /// line of the head (glyph extents of the name, class line, live line; the HP bar, the OPENZ ONLY chip, the portrait), above the words.</summary>
        bool PlaceOk(out string where)
        {
            var ins = OzUeDriverTab.Inspector; var root = OzUeStatsRoot.I;
            var head = ins != null ? ins.Head : null; var b = tab.Button;
            if (head == null || b == null || !b.Rt || root == null || root.Shell == null) { where = "no card"; return false; }
            UeBox E = CanvasBox(b.Rt), R = CanvasBox(head.Remove.Rt), H = CanvasBox(head.Rt), S = CanvasBox(root.Shell.Box);
            bool compact = head.Rt.sizeDelta.y < 60f;
            float gapWant = compact ? OzUeExitColumn.GapCompact : OzUeExitColumn.Gap, gap = E.y - R.Bottom;
            bool under = head.Remove.Rt.gameObject.activeInHierarchy && Mathf.Abs(E.Right - R.Right) < 0.6f && Mathf.Abs(gap - gapWant) < 0.6f && E.x <= R.x + 0.5f;
            bool inside = E.x >= H.x - 0.5f && E.Right <= H.Right + 0.5f && E.y >= H.y - 0.5f && E.Bottom <= H.Bottom + 0.5f
                          && E.x >= S.x && E.Right <= S.Right && R.y >= H.y - 0.5f;
            var hits = new List<string>();
            void Text(OzText t, bool right, string n)
            {
                if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) return;
                var r = CanvasBox(t.rectTransform);
                float gw = Mathf.Min(t.WidthAt(t.text), r.w);
                var g = right ? new UeBox(r.Right - gw, r.y, gw, r.h) : new UeBox(r.x, r.y, gw, r.h);
                if (g.Overlaps(E)) hits.Add(n + " x EXIT DRIVER"); if (g.Overlaps(R)) hits.Add(n + " x REMOVE");
            }
            void Box(Transform t, string n)
            {
                var rt = t as RectTransform;
                if (!rt || !rt.gameObject.activeInHierarchy) return;
                var gr = rt.GetComponent<UnityEngine.UI.Graphic>(); if (gr && !gr.enabled) return;
                var r = CanvasBox(rt);
                if (r.Overlaps(E)) hits.Add(n + " x EXIT DRIVER"); if (r.Overlaps(R)) hits.Add(n + " x REMOVE");
            }
            Text(head.Name, false, "name"); Text(head.Meta, false, "class line"); Text(head.LiveL, false, "team word"); Text(head.LiveR, true, "HP");
            Text(head.Dim, false, "dim line");
            Box(head.Rt.Find("BarTrack"), "HP bar"); Box(head.Rt.Find("OpenZOnly"), "OPENZ ONLY chip"); Box(head.Pic, "portrait"); Box(head.Back.Rt, "back");
            var words = head.Rt.parent ? head.Rt.parent.Find("Words") as RectTransform : null;
            bool aboveWords = !words || E.Bottom <= CanvasBox(words).y + 0.5f;
            float need = b.Label != null ? b.Label.WidthAt(OzUeDriverTab.Label) + 2f * OzUeExitColumn.MinPadX : 0f;
            bool fits = need <= E.w + 0.5f;
            where = $"EXIT DRIVER {E}, REMOVE {R} (gap {gap:0.#} u, want {gapWant:0}), head {H} ({(compact ? "compact" : "normal")}), box {S}; under REMOVE {under}, inside {inside}, "
                    + $"over nothing {hits.Count == 0}{(hits.Count > 0 ? " (" + string.Join(", ", hits) + ")" : "")}, above the words {aboveWords}, words fit ({need:0.#} u) {fits}; "
                    + $"rows' right edges: name {head.NameRight:0.#}, class {head.MetaRight:0.#}, live {head.LiveRight:0.#} (text from {head.TextX:0.#})";
            return under && inside && hits.Count == 0 && aboveWords && fits;
        }

        /// <summary>The side tip shown for the hovered button: LEFT of the STATS box, over neither the dock, the play bar nor the zoom.</summary>
        bool TipOk(int shown0, out string where)
        {
            var go = GameObject.Find("OzUeTip");
            var root = OzUeStatsRoot.I; var lay = OzUeControlsRoot.I ? OzUeControlsRoot.I.Laid : null;
            if (!go || root == null || root.Shell == null || lay == null) { where = $"side tip shown {(bool)go}, side tips shown since the hover {OzUeSideTip.Shown - shown0}"; return false; }
            var T = CanvasBox((RectTransform)go.transform); var S = CanvasBox(root.Shell.Box);
            bool left = T.Right <= S.x + 0.5f;
            bool clear = !T.Overlaps(lay.dock.plate) && !T.Overlaps(lay.play.plate) && !T.Overlaps(lay.zoom.plate);
            where = $"side tip {T}, box {S}, dock {lay.dock.plate}, play bar {lay.play.plate}, zoom {lay.zoom.plate}: left of the box {left}, over no panel {clear}, "
                    + $"tips shown since the hover {OzUeSideTip.Shown - shown0}";
            return left && clear && OzUeSideTip.Shown > shown0;
        }

        static UeBox CanvasBox(RectTransform rt)
        {
            var w = new Vector3[4]; rt.GetWorldCorners(w);                                               // overlay canvas: world = screen pixels
            float k = OzUnitEditor.PxPerUnit, x0 = w[0].x / k, x1 = w[2].x / k, yTop = (Screen.height - w[1].y) / k, yBot = (Screen.height - w[0].y) / k;
            return new UeBox(x0, yTop, x1 - x0, yBot - yTop);
        }

        static string Path(Transform t) { var s = t.name; for (var p = t.parent; p && s.Length < 200; p = p.parent) s = p.name + "/" + s; return s; }

        static Vector2 Centre(RectTransform rt)
        {
            if (!rt) { var f = OzUeInsets.FreeFieldScreen; return f.center; }
            return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        }

        static void MouseAt(Vector2 p, bool left)
        {
            var st = new MouseState { position = p };
            if (left) st = st.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(Mouse.current, st);
        }

        void Shot(string file)
        {
            if (string.IsNullOrEmpty(Folder)) return;
            try { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Folder, file)); Note("frame " + file); } catch { }
        }

        static string Short(string s) => s.Length <= 90 ? s : s.Substring(0, 90) + "...";

        void Cleanup()
        {
            try { if (input) input.Selection.Clear(); } catch { }
            if (W == null) return;
            var gone = new HashSet<int>();
            foreach (var o in made.ToList())
            {
                try { if (o != null && !o.Destroyed && gone.Add(o.Id)) OzUePlayground.Remove(o); } catch { }
            }
            foreach (var o in W.Objects.ToList())
            {
                try { if (o != null && o.Id > startMaxId && o.Kind == ZKind.Robot && !o.Destroyed && gone.Add(o.Id)) OzUePlayground.Remove(o); } catch { }
            }
            made.Clear();
        }
    }
}
#endif
