using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 36: vehicles and their drivers drawn smaller than full size, view only, nothing of the rules changes:
    /// <list type="bullet">
    /// <item>Hidden keys: Ctrl+I = one step smaller, Ctrl+O = one step bigger, 100 to 60 % in fives (<see cref="Steps"/>). In the Unity editor
    /// Ctrl+I is Unity's Edit/Invert Selection and Ctrl+O its File/Open Scene, so there the keys are Ctrl+Alt+I / Ctrl+Alt+O (a plain
    /// Ctrl+I / O shows that hint instead). The exe takes both forms.</item>
    /// <item>On screen: only at a key press, the size shows for 2 s with the menu's click sound, then nothing at any size. The Options row
    /// changes the size silently. While nothing is shown the drawer component is off, so no OnGUI runs for it.</item>
    /// <item>What is drawn smaller: every vehicle view of both teams (jeep, light, medium, mtank, heavy, MLRS, APC, crane; v1 and v2 models;
    /// a wreck while it is still that view), about its root, which is the hull's ground point (UnitView puts the root on the ground), so it
    /// stays on the ground. Everything under the hull follows by itself (turret, barrels, muzzles, wheels, belts, lights, hatches, doors,
    /// ramps, seat / hatch anchors, the APC's benches; torn-off wreck pieces and the flying turret start from the drawn size). Drivers and
    /// crews are posed by BoardingView in world space: they follow through BoardingView.HullGrow (the K386 path that already shrinks them
    /// with a hull growing out of a factory). The shuttle (a ZKind.Vehicle in the rules) is NOT scaled: it is OpenZ's own carrier, not one
    /// of Z's vehicles, and its bay, ramp, flight and crash are staged around its full size. Guns are not vehicles and stay.</item>
    /// <item>What stays: the rules (the sim never reads this class; it is told one number, the size, for the heavy tank's rock rule, Sim/ZObject.HeavyRock.cs), the sim footprint, paths, speeds, hit chances, ranges, the view's unit
    /// spacing radius (measured at the bind, full size), the 1.9 m selection ring (the same ring for every vehicle today) and the health
    /// bar's length (from max health). The pick box, the selection brackets and the health bar's place come from the drawn mesh
    /// (PlayerInput.MeshRect), so they follow the drawn vehicle by themselves.</item>
    /// <item>Every game, the editor and every exe, on every level and in the Unit editor, with no start flag. The size comes from the Options
    /// row VEHICLE SIZE (UI/OzVehicleSizeOption.cs, saved, default 90 %); the hidden keys step that same saved size.</item>
    /// <item>At 100 % nothing is written but the jeep's share (<see cref="JeepShare"/>, 2 Nov): no other transform is touched and every reader of another vehicle gets exactly 1 (<see cref="Of(UnitView)"/>), so the
    /// game draws byte for byte as before. Going back to 100 % writes back the exact size the view had.</item>
    /// </list>
    /// Check: Editor/OzV13VehicleScaleCheck.cs.
    /// </summary>
    [DefaultExecutionOrder(65)]   // after UnitView (60: its exit grow writes the hull's scale in LateUpdate), before BoardingView and FactoryView (70: the crews are posed on the drawn hull)
    public class OzVehicleScaleTest : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.3 item36]";
        /// <summary>The drawn size of the steps (index 0 = full size).</summary>
        public static readonly float[] Steps = { 1f, 0.95f, 0.9f, 0.85f, 0.8f, 0.75f, 0.7f, 0.65f, 0.6f };
        public const float NoteSeconds = 2f;
        /// <summary>The note for a plain Ctrl+I / O in the editor, where those two keys are Unity's.</summary>
        public const string HintText = "VEHICLE SCALE: CTRL+ALT+I SMALLER, CTRL+ALT+O BIGGER";
        /// <summary>The note's gap under the territory tab, in HUD canvas units.</summary>
        public const float LabelGap = 0.5f;

        public static OzVehicleScaleTest I { get; private set; }
        /// <summary>The step now (0 = 100 %).</summary>
        public static int Step { get; private set; }
        public static float Factor => Steps[Step];
        /// <summary>
        /// The jeep is drawn at this share of the vehicle size, on top of it (90 % x 0.92 at the default; the Options row and the hidden keys step it as before). View only: its rules,
        /// footprint, paths and hit size stay; its driver follows the drawn hull (BoardingView.HullGrow reads <see cref="Of(UnitView)"/>).</summary>
        public const float JeepShare = 0.92f;
        /// <summary>The share of the vehicle size a vehicle type is drawn at: the jeep <see cref="JeepShare"/>, every other vehicle 1.</summary>
        public static float Share(string type) => type == "jeep" ? JeepShare : 1f;
        static float Share(UnitView v) => v.Obj != null ? Share(v.Obj.Type) : 1f;
        public static int Percent => Mathf.RoundToInt(Factor * 100f);
        /// <summary>True while at least one drawn vehicle carries a scale other than 1. While false every reader returns 1 at once.</summary>
        public static bool AnyApplied { get; private set; }
        /// <summary>Check counters: scale writes to vehicle roots, key steps taken, click sounds played, the last note shown.</summary>
        public static int Writes, KeySteps, Clicks;
        /// <summary>Check: the writes to a vehicle drawn at a share other than 1 (the jeep); true while a drawn vehicle carries a scale other than
        /// its share at 100 % (the size row is not at 100 %).</summary>
        public static int ShareWrites; public static bool AnyStepApplied { get; private set; }
        public static string LastNote = "";

        sealed class St { public bool known; public Vector3 baseScale, lastSet; public float applied = 1f; }
        static readonly Dictionary<UnitView, St> states = new Dictionary<UnitView, St>();
        static readonly List<UnitView> gone = new List<UnitView>();
        static string note; static float noteUntil = -1f; static GUIStyle noteStyle;
        /// <summary>The OnGUI on its own component, so that it can be off while nothing is shown.</summary>
        sealed class Drawer : MonoBehaviour { void OnGUI() => Draw(); }
        Drawer drawer;

        // ------------------------------------------------------------------ rules of the tool (pure: the check reads them)
        /// <summary>The key rule: -1 = one step smaller (I), +1 = one step bigger (O), 0 = nothing. editor: Alt must be held too (Unity owns
        /// Ctrl+I / Ctrl+O); hint = a plain Ctrl+I / O in the editor (the note names the editor keys).</summary>
        public static int KeyStep(bool ctrl, bool alt, bool iDown, bool oDown, bool editor, out bool hint)
        {
            hint = false;
            if (!ctrl || iDown == oDown) return 0;
            if (editor && !alt) { hint = true; return 0; }
            return iDown ? -1 : 1;
        }

        /// <summary>The step after a key: -1 = smaller (index up), +1 = bigger (index down), held at both ends.</summary>
        public static int StepAfter(int step, int dir) => Mathf.Clamp(step + (dir < 0 ? 1 : dir > 0 ? -1 : 0), 0, Steps.Length - 1);

        /// <summary>The scale a root gets: its own size exactly at 100 %.</summary>
        public static Vector3 Want(Vector3 baseScale, float k) => k == 1f ? baseScale : baseScale * k;

        /// <summary>The text of the label and of the note after a key.</summary>
        public static string LabelText(int percent) => $"VEHICLE SCALE {percent} %";

        /// <summary>What is on screen: 0 = nothing, 2 = the note (2 s after a key).</summary>
        public static int LabelSize(bool noteOn) => noteOn ? 2 : 0;

        /// <summary>Where it goes, in pixels (y down): centred under the territory tab `tab` of a screen sw x sh at UI scale `ui`, `gap` u
        /// under it, as the HUD places its own panels (a canvas RefH / ui units tall, wherever ARRANGE INTERFACE put them), kept on screen.</summary>
        public static Rect Place(OzRect tab, float sw, float sh, float ui, float gap, float w, float h)
        {
            float ch = OzLayout.RefH / ui, cw = sw * ch / sh, p = sh / ch;
            var tl = OzHudLayout.TopLeft(tab, cw, ch);
            float x = (tl.x + tab.w * 0.5f) * p - w * 0.5f, y = (tl.y + tab.h + gap) * p;
            return new Rect(Mathf.Round(Mathf.Clamp(x, 0f, Mathf.Max(0f, sw - w))), Mathf.Round(Mathf.Min(y, Mathf.Max(0f, sh - h))), w, h);
        }

        /// <summary>Drawn smaller by the test: a vehicle's view, not the shuttle's.</summary>
        public static bool Includes(UnitView v) => v && v.Obj != null && v.Obj.Kind == ZKind.Vehicle && !v.Obj.IsShuttle;

        // ------------------------------------------------------------------ readers (1 = no change)
        /// <summary>The drawn size this view carries now (1 = full size; always 1 at 100 %).</summary>
        public static float Of(UnitView v)
        {
            if (!AnyApplied || !v) return 1f;
            return states.TryGetValue(v, out var s) ? s.applied : 1f;
        }

        /// <summary>The same for a component on a vehicle's root (VehicleMotion).</summary>
        public static float Of(Component c)
        {
            if (!AnyApplied || !c) return 1f;
            var v = c.GetComponent<UnitView>();
            return v ? Of(v) : 1f;
        }

        // ------------------------------------------------------------------ life
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; AnyApplied = false; states.Clear(); note = null; noteUntil = -1f; Writes = KeySteps = Clicks = 0; LastNote = ""; ShareWrites = 0; AnyStepApplied = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I) return;
            Step = StepOf(OzVehicleSizeOption.Saved);
            var go = new GameObject("OzVehicleScaleTest"); DontDestroyOnLoad(go);
            I = go.AddComponent<OzVehicleScaleTest>();
            I.drawer = go.AddComponent<Drawer>(); I.drawer.useGUILayout = false; I.drawer.enabled = false;
        }

        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            var kb = Keyboard.current; if (kb == null) return;
            bool iDown = kb.iKey.wasPressedThisFrame, oDown = kb.oKey.wasPressedThisFrame;
            if (!iDown && !oDown) return;
            if (OzControlsTable.Capturing || OzUeHooks.Typing || OzUeHooks.AnyModal) return;   // the rebind screen waits for a key; the Unit editor's text boxes and dialogs
            int dir = KeyStep(kb.ctrlKey.isPressed, kb.altKey.isPressed, iDown, oDown, Application.isEditor, out bool hint);
            if (hint) { Note(HintText); return; }
            if (dir != 0) Press(dir);
        }

        /// <summary>One key step (the check calls it through real key events; this is the same path).</summary>
        public static void Press(int dir)
        {
            int was = Step; Step = StepAfter(Step, dir); KeySteps++;
            OzVehicleSizeOption.Save(Percent);
            Note(LabelText(Percent));
            OzSfx.Ui("ui_click"); Clicks++;
            Debug.Log($"{Tag} vehicle scale {Percent} %{(Step == was ? " (already " + (dir < 0 ? "the smallest step" : "full size") + ")" : "")}");
        }

        /// <summary>The step of a size in percent.</summary>
        public static int StepOf(int percent) { for (int i = 0; i < Steps.Length; i++) if (Mathf.RoundToInt(Steps[i] * 100f) == percent) return i; return 0; }

        /// <summary>The Options row: one step (-1 = smaller, +1 = bigger), live, with no note on screen. The steps stop at the ends.</summary>
        public static void StepQuiet(int dir) { Step = StepAfter(Step, dir); OzVehicleSizeOption.Save(Percent); }

        /// <summary>Check only: puts the step without a key (edit mode: the headless rules run).</summary>
        public static void SetStepForCheck(int step) => Step = Mathf.Clamp(step, 0, Steps.Length - 1);

        static void Note(string s) { note = s; LastNote = s; noteUntil = Time.unscaledTime + NoteSeconds; }

        static bool NoteOn => note != null && Time.unscaledTime <= noteUntil;
        /// <summary>The check reads what is on screen now: the size (<see cref="LabelSize"/>), the text, and whether the drawer runs.</summary>
        public static int ShownSize => LabelSize(NoteOn);
        public static string ShownText => NoteOn ? note : "";
        public static bool DrawerOn => I && I.drawer && I.drawer.enabled;

        void LateUpdate()
        {
            ZWorld.HeavyRockDrawnPct = Percent;                                                       // DV164: what a heavy tank clips goes by its drawn size (Sim/ZObject.HeavyRock.cs)
            bool show = ShownSize != 0; if (drawer && drawer.enabled != show) drawer.enabled = show;
            float k = Factor;
            var g = ZGame.I;
            if (g) foreach (var kv in g.Views) { var v = kv.Value; if (Includes(v) && v.gameObject.activeInHierarchy && !states.ContainsKey(v) && (k != 1f || Share(v) != 1f)) states[v] = new St(); }   // 2 Nov: at 100 % only a jeep is taken in (its share)
            if (states.Count == 0) { AnyApplied = AnyStepApplied = false; return; }                // 100 % and no jeep: nothing is ever written
            bool any = false, step = false; gone.Clear();
            foreach (var kv in states)
            {
                var v = kv.Key; var s = kv.Value;
                if (!v) { gone.Add(v); continue; }
                float sh = Share(v);
                if (!v.gameObject.activeInHierarchy) { if (v.Dying) gone.Add(v); else { if (s.applied != 1f) any = true; if (s.applied != sh) step = true; } continue; }   // a wreck that has gone is forgotten; a view hidden for a moment keeps what it has
                int w = Writes;
                Apply(v.transform, s, sh != 1f ? k * sh : k, v.Dying);                                  // 2 Nov: the jeep at its share of the size
                if (sh != 1f && Writes != w) ShareWrites++;
                if (s.applied != 1f) any = true;
                if (s.applied != sh) step = true;
            }
            foreach (var v in gone) states.Remove(v);
            AnyApplied = any; AnyStepApplied = step;
        }

        /// <summary>The root's scale = the size the view itself gave it x k. A size the view wrote this frame (a hull growing out of a
        /// factory) is taken as its new full size; a dying view's own death effect (PopOut shrinking the wreck from its drawn size) is left
        /// as it is.</summary>
        static void Apply(Transform t, St s, float k, bool dying)
        {
            var cur = t.localScale;
            if (!s.known) { s.known = true; s.applied = 1f; s.lastSet = cur; }
            bool foreign = !Same(cur, s.lastSet);
            if (foreign && dying && s.applied != 1f) { s.lastSet = cur; return; }
            if (s.applied == 1f || foreign) s.baseScale = cur;
            var want = Want(s.baseScale, k);
            if (!Same(want, cur)) { t.localScale = want; Writes++; }
            s.lastSet = want; s.applied = k;
        }

        static bool Same(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

        static void Draw()
        {
            if (ShownSize == 0) return;
            string text = ShownText;
            float ui = Mathf.Clamp(OzText.UiScale, OzText.ScaleMin, OzText.ScaleMax), u = Mathf.Max(0.75f, Screen.height / OzLayout.RefH);
            if (noteStyle == null) { noteStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, padding = new RectOffset(), clipping = TextClipping.Overflow }; noteStyle.normal.textColor = Color.white; }
            noteStyle.fontSize = Mathf.RoundToInt(20f * u);
            var size = noteStyle.CalcSize(new GUIContent(text));
            float w = size.x + 36f * u, h = size.y + 16f * u;
            GUI.Box(Place(OzHudLayout.At(OzPanel.Status, OzLayout.TerrTab), Screen.width, Screen.height, ui, LabelGap, w, h), text, noteStyle);
        }

        // ------------------------------------------------------------------ check helper (Play): a vehicle dropped as the testing panel drops one
        /// <summary>Check only: one vehicle of `tile` (a type, or "light@v2" / "heavy@v1" ...) for `team` near tile (cx, cy), drawn as the
        /// tile says (OzCheatSpawner's drop path). Null when there is no room.</summary>
        public static ZObject CheckSpawn(ZGame g, string tile, Team team, int cx, int cy)
        {
            if (!g || g.World == null) return null;
            string type = tile; int heavyModel = OzHeavyTank.FromTile(ref type); string v2Tile = type; OzV2Units.FromTile(ref type);
            if (!OzCheatSpawner.FreeTile(g.World, cx, cy, 2, false, out int tx, out int ty)) return null;
            var o = g.World.CreateObject("vehicle", type, tx * 16, ty * 16, team);
            if (o == null) return null;
            OzHeavyTank.Pin(o, heavyModel); OzV2Units.PinTile(o, v2Tile);
            g.SpawnUnitView(o);
            return o;
        }
    }
}
