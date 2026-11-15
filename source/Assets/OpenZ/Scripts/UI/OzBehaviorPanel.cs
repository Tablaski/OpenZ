using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>Runtime behavior laboratory. Edits update the playground world and its existing units immediately.</summary>
    public sealed class OzBehaviorPanel : MonoBehaviour
    {
        public static OzBehaviorPanel I { get; private set; }
        public bool IsOpen { get; private set; }
        public static bool OwnsPointer
        {
            get
            {
                if (!I || !I.IsOpen || OzUnitEditor.PanelsHidden || UnityEngine.InputSystem.Mouse.current == null) return false;
                var p = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                return I.panelRect.Contains(new Vector2(p.x / I.scale, (Screen.height - p.y) / I.scale));
            }
        }
        readonly ZBehaviorProfile[] drafts = new ZBehaviorProfile[2];
        readonly Dictionary<string, string> numbers = new Dictionary<string, string>();
        int family, page, scope;
        bool dirty, profiles, imports, debug, clearFocus;
        string presetName = "My behavior", status = "Edits apply immediately to this laboratory. Save a preset to use it in a match.";
        Vector2 scroll, listScroll;
        HudButton button;
        RectTransform blocker;
        Rect panelRect;
        GUIStyle box, text, muted, title, field, btn, selected, tooltip;
        readonly string[] pages = { "DODGING", "MOVEMENT", "BOOST", "COMBAT" };
        readonly Color accent = new Color(.96f, .72f, .3f);
        float scale = 1;
        ZBehaviorProfile P => drafts[family];
        string Type => scope == 0 ? "" : (family == 0 ? ZBehaviorSchema.TankTypes : ZBehaviorSchema.RobotTypes)[scope - 1];
        ZWorld World => OzUnitEditor.I != null ? OzUnitEditor.I.World : null;

        void Awake() { I = this; }
        void Start()
        {
            for (int i = 0; i < 2; i++)
            {
                drafts[i] = OzBehaviorLibrary.Draft((ZBehaviorFamily)i).Clone();
                World?.SetBehavior(drafts[i]);
            }
            presetName = P.name;
            OzUeKeys.OnEsc(95, () => { if (!IsOpen) return false; Toggle(); return true; });
        }
        void Update()
        {
            if (!OzUnitEditor.Active || !OzUnitEditor.I.Hud || !OzUnitEditor.I.Hud.Root) return;
            var hud = OzUnitEditor.I.Hud;
            if (button == null)
            {
                button = HudButton.Make(hud.Root, "UE_Behavior", "BEHAVIOR", new Vector2(150, 34), HudButtonKind.Small, 12);
                button.Tooltip = "BEHAVIOR LABORATORY|Open separate tank and robot behavior menus. Experiment live with dodging, movement, boost and combat; save named presets for Options > Game. The OpenZ and Zod reference values are shown for every field.|";
                button.OnClick = c => Toggle();
                blocker = Hud.Node("UE_BehaviorBlocker", hud.Root);
                hud.AddBlocker(blocker);
            }
            bool visible = !OzUnitEditor.PanelsHidden;
            button.Rt.gameObject.SetActive(visible);
            var cs = OzUnitEditor.CanvasSize;
            button.Rt.TL(cs.x - 168, cs.y - 46, 150, 34);
            scale = Mathf.Clamp(Screen.width / 1280f, .75f, 1.5f);
            float w = Screen.width / scale, h = Screen.height / scale;
            float height = Mathf.Min(530, h - 110);
            panelRect = new Rect(14, h - height - 58, w - 28, height);
            blocker.gameObject.SetActive(IsOpen && visible);
            if (IsOpen && visible)
            {
                blocker.TL(panelRect.x * scale / OzUnitEditor.PxPerUnit, panelRect.y * scale / OzUnitEditor.PxPerUnit,
                    panelRect.width * scale / OzUnitEditor.PxPerUnit, panelRect.height * scale / OzUnitEditor.PxPerUnit);
                OzUeInsets.Set(UeSide.Bottom, "Behavior laboratory", (h - panelRect.y) * scale / OzUnitEditor.PxPerUnit);
            }
            else OzUeInsets.Set(UeSide.Bottom, "Behavior laboratory", 0);
        }
        public void Toggle()
        {
            IsOpen = !IsOpen; profiles = imports = false; clearFocus = true;
            OzUeHooks.SetTyping(this, false);
        }
        public void Edit(ZBehaviorFamily f, ZBehaviorKey key, double value, string type = "")
        {
            drafts[(int)f].Set(type, key, value); Apply((int)f); dirty = true;
        }
        public void SetMaster(ZBehaviorFamily f, double value)
        {
            drafts[(int)f].master = Math.Max(0, Math.Min(100, value)); Apply((int)f); dirty = true; numbers.Clear();
        }
        public ZBehaviorProfile Draft(ZBehaviorFamily f) => drafts[(int)f].Clone();
        public void ShowPage(ZBehaviorFamily f, int tab)
        { family = (int)f; page = Mathf.Clamp(tab, 0, 3); scope = 0; scroll = Vector2.zero; presetName = P.name; numbers.Clear(); IsOpen = true; }
        public string Explain(ZBehaviorKey key) => DetailedTip(ZBehaviorSchema.Def(key));
        void Apply(int f)
        {
            World?.SetBehavior(drafts[f]); OzBehaviorLibrary.SaveDraft(drafts[f]);
        }
        void Changed()
        {
            dirty = true; Apply(family);
            status = "CUSTOM - applied to existing and future " + (family == 0 ? "tanks" : "robots") + ". Match selection is unchanged.";
        }
        void Styles()
        {
            if (box != null) return;
            box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 10, 10) };
            text = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            text.normal.textColor = new Color(.92f, .93f, .89f);
            muted = new GUIStyle(text) { fontSize = 11 }; muted.normal.textColor = new Color(.65f, .74f, .76f);
            title = new GUIStyle(text) { fontSize = 18, fontStyle = FontStyle.Bold }; title.normal.textColor = accent;
            field = new GUIStyle(GUI.skin.textField) { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 4, 3, 3) };
            btn = new GUIStyle(GUI.skin.button) { fontSize = 12, wordWrap = true };
            selected = new GUIStyle(btn); selected.normal.textColor = accent;
            tooltip = new GUIStyle(GUI.skin.box) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(14, 14, 12, 12) };
            tooltip.normal.textColor = Color.white;
        }
        GUIContent C(string label, string tip) => new GUIContent(label, tip);
        bool Button(Rect r, string label, string tip, bool on = false) => GUI.Button(r, C(label, tip), on ? selected : btn);
        void Label(Rect r, string label, string tip = "", bool dim = false) => GUI.Label(r, C(label, tip), dim ? muted : text);
        void OnGUI()
        {
            if (!IsOpen || OzUnitEditor.PanelsHidden || P == null) return;
            Styles(); GUI.depth = -80;
            if (clearFocus) { GUI.FocusControl(null); clearFocus = false; }
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            var r = panelRect;
            GUI.backgroundColor = new Color(.09f, .13f, .16f, .99f); GUI.Box(r, GUIContent.none, box); GUI.backgroundColor = Color.white;
            GUI.BeginGroup(r);
            float w = r.width, h = r.height;
            GUI.Label(new Rect(16, 8, 250, 30), "BEHAVIOR LABORATORY", title);
            if (Button(new Rect(w - 48, 8, 32, 27), "X", "Close this panel. The current laboratory behavior remains active. Escape also closes it.")) Toggle();
            for (int f = 0; f < 2; f++)
            {
                int id = f;
                if (Button(new Rect(260 + f * 130, 8, 124, 28), f == 0 ? "TANK BEHAVIOR" : "ROBOT BEHAVIOR",
                    "Switch to an independent behavior profile. Changes affect this family only; switching menus keeps your edits.", family == f))
                { family = id; scope = 0; scroll = Vector2.zero; numbers.Clear(); presetName = P.name; profiles = imports = false; }
            }
            if (Button(new Rect(16, 44, 248, 28), (dirty ? "CUSTOM - " : "") + P.name + (dirty ? " *" : "") + "  v", "Load a reference or saved preset into the laboratory. This does not select it for future matches.")) { profiles = !profiles; imports = false; }
            GUI.SetNextControlName("Behavior_PresetName");
            presetName = GUI.TextField(new Rect(274, 44, Mathf.Max(100, w - 800), 28), presetName, 80, field);
            GUI.Label(new Rect(274, 44, Mathf.Max(100, w - 800), 28), C("", "Preset name: 1 to 80 characters. SAVE updates your preset; SAVE AS creates a separate named copy."));
            float ax = Mathf.Max(384, w - 516);
            if (Button(new Rect(ax, 44, 75, 28), "SAVE", "Save the current family, master position and manual overrides under this name. Existing saved preset is updated; built-in references are never overwritten.")) Save(false);
            if (Button(new Rect(ax + 81, 44, 82, 28), "SAVE AS", "Create a new independent preset with the entered name. It will appear in this family's Options > Game dropdown.")) Save(true);
            if (Button(new Rect(ax + 169, 44, 72, 28), "EXPORT", "Write this behavior to a .ozbehavior file in UnitEditor/BehaviorPresets. Both reference values and all manual overrides are included.")) Safe(() => status = "Exported: " + OzBehaviorLibrary.Export(P));
            if (Button(new Rect(ax + 247, 44, 72, 28), "IMPORT", "Load a .ozbehavior file from the BehaviorPresets folder into a laboratory draft. Save it to add it to the game dropdown.")) { imports = !imports; profiles = false; }
            if (Button(new Rect(ax + 325, 44, 80, 28), "FOLDER", "Open the local folder used to exchange .ozbehavior preset files. Nothing is uploaded.")) Safe(() => { Directory.CreateDirectory(OzBehaviorLibrary.Folder); Application.OpenURL(new Uri(OzBehaviorLibrary.Folder).AbsoluteUri); });
            if (Button(new Rect(ax + 411, 44, 89, 28), "DELETE", "Delete the current saved preset. Built-in references cannot be deleted. A selected deleted profile falls back to OpenZ Native."))
                Safe(() => { if (!OzBehaviorLibrary.SavedProfiles.Exists(p => p.id == P.id)) throw new InvalidOperationException("Built-in references cannot be deleted."); OzBehaviorLibrary.Delete(P); status = "Saved preset deleted; laboratory draft retained."; });

            const string masterTip = "Master slider: 0% preserves OpenZ's native simulation and presentation exactly when no manual overrides exist. 100% uses Zod Classic, before fenio commit 37af18e: immediate directions, native random dodge, endurance sprint, tanks may interrupt attack, engaged robots still cannot dodge. Numeric values interpolate; checkboxes switch at 50%. Manual overrides remain locked when the master moves. This is not an evasion success percentage and does not change weapon damage, range or projectile speed.";
            Label(new Rect(16, 82, 180, 22), "OPENZ NATIVE", masterTip);
            Label(new Rect(w - 186, 82, 170, 22), "ZOD CLASSIC", masterTip);
            GUI.Label(new Rect(200, 82, w - 400, 22), C("MASTER  " + P.master.ToString("0") + "%  |  " + P.overrides.Count + " manual overrides", masterTip), selected);
            float m = GUI.HorizontalSlider(new Rect(24, 112, w - 48, 20), (float)P.master, 0, 100);
            GUI.Label(new Rect(24, 108, w - 48, 22), C("", masterTip), text);
            if (Math.Abs(m - P.master) > .01) SetMaster((ZBehaviorFamily)family, Math.Round(m));
            var types = family == 0 ? ZBehaviorSchema.TankTypes : ZBehaviorSchema.RobotTypes;
            if (Button(new Rect(16, 140, 185, 27), scope == 0 ? "ALL " + (family == 0 ? "TANKS" : "ROBOTS") : DisplayType(Type), "Scope: click to cycle all-family defaults and per-unit exceptions. Per-unit overrides take precedence over family overrides. Right-click is not required."))
            { scope = (scope + 1) % (types.Length + 1); numbers.Clear(); }
            for (int i = 0; i < pages.Length; i++)
            {
                int id = i;
                if (Button(new Rect(211 + i * 112, 140, 106, 27), pages[i], "Show " + pages[i].ToLowerInvariant() + " parameters. Every row shows the native OpenZ and Zod Classic endpoint values; hover for conditions and exact per-type references.", page == i))
                { page = id; scroll = Vector2.zero; }
            }
            if (Button(new Rect(w - 235, 140, 140, 27), "RESET OVERRIDES", "Remove all manual overrides for this family, including per-type exceptions. The master position remains; every field follows it again."))
            { P.overrides.Clear(); numbers.Clear(); Changed(); }
            if (Button(new Rect(w - 87, 140, 70, 27), debug ? "DEBUG ON" : "DEBUG", "Draw current explosion threats, dodge endpoints and logical positions. Also show live reaction counters. This is a laboratory display only.", debug)) debug = !debug;

            var defs = Array.FindAll(ZBehaviorSchema.All, d => d.Group == pages[page]);
            float col = (w - 52) / 2, rowH = 77;
            Rect area = new Rect(16, 176, w - 32, Mathf.Max(85, h - 248));
            scroll = GUI.BeginScrollView(area, scroll, new Rect(0, 0, w - 54, ((defs.Length + 1) / 2) * rowH));
            for (int i = 0; i < defs.Length; i++) DrawRow(defs[i], new Rect((i % 2) * (col + 12), (i / 2) * rowH, col, rowH - 4));
            GUI.EndScrollView();
            float bottom = h - 64;
            Label(new Rect(16, bottom, w - 32, 22), status, "Laboratory edits update only this world. To use a profile in real matches: save it, return to Options > Game, select it separately for tanks and robots, then start a match.", true);
            int alerts = 0, attempts = 0, refused = 0, replans = 0;
            if (World != null) foreach (var o in World.Objects) if ((family == 1 && o.IsRobot) || (family == 0 && ZBehaviorSchema.IsTank(o.Type)))
            { alerts += o.BehaviorAlerts; attempts += o.BehaviorAttempts; refused += o.BehaviorRefusals; replans += o.BehaviorReplans; }
            Label(new Rect(16, bottom + 25, w - 270, 22), "LIVE  Alerts " + alerts + "   Attempts " + attempts + "   Refusals " + refused + "   Replans " + replans,
                "Alerts include filtered or refused threats. Attempts count planned defensive steps, not successful escapes. Replans change an active step. Hover a unit with DEBUG enabled to read its latest reason.", true);
            if (Button(new Rect(w - 252, bottom + 25, 114, 25), "RESET OPENZ", "Restore this family's exact native behavior in the laboratory. Saved presets and game selection are preserved."))
            { drafts[family] = OzBehaviorLibrary.Native((ZBehaviorFamily)family); presetName = P.name; numbers.Clear(); Changed(); }
            if (Button(new Rect(w - 130, bottom + 25, 114, 25), OzUeControls.Playing ? "PAUSE FIGHT" : "PLAY FIGHT", "Pause or resume the laboratory. Parameters are editable while paused and affect the next simulation tick.")) OzUeControls.TogglePlay();
            if (profiles) DrawProfiles(w, h);
            if (imports) DrawImports(w, h);
            GUI.EndGroup();
            string tip = GUI.tooltip;
            OzUeHooks.SetTyping(this, GUI.GetNameOfFocusedControl().StartsWith("Behavior_") || profiles || imports);
            if (!string.IsNullOrEmpty(tip))
            {
                float tw = Mathf.Min(850, Screen.width / scale - 32);
                float th = tooltip.CalcHeight(new GUIContent(tip), tw);
                float ty = Mathf.Max(5, r.y - th - 8);
                GUI.backgroundColor = new Color(.04f, .07f, .10f, 1);
                GUI.Box(new Rect(16, ty, tw, th), tip, tooltip); GUI.backgroundColor = Color.white;
            }
            GUI.matrix = matrix;
            if (debug) DrawDebug();
        }
        void DrawRow(ZBehaviorDef d, Rect r)
        {
            double value = P.Get(Type, d.Key); bool own = P.Has(Type, d.Key);
            string tip = DetailedTip(d);
            GUI.Box(r, GUIContent.none);
            Label(new Rect(r.x + 8, r.y + 3, r.width - 125, 22), (own ? "* " : "") + d.Label, tip);
            if (Button(new Rect(r.xMax - 110, r.y + 3, 102, 22), own ? "OVERRIDE" : "INHERITED", "Override state. Click to remove this field's manual override and inherit from the family or master slider. Changing any value creates a manual override.", own))
            { P.Reset(Type, d.Key); numbers.Clear(); Changed(); }
            Label(new Rect(r.x + 8, r.y + 26, r.width - 16, 18), "OpenZ: " + Reference(d, false) + "    |    Zod: " + Reference(d, true), tip, true);
            Rect control = new Rect(r.x + 8, r.y + 47, r.width - 16, 22);
            if (d.Choices != null)
            {
                if (Button(control, d.Format(value), tip)) { P.Set(Type, d.Key, ((int)value + 1) % d.Choices.Length); Changed(); }
            }
            else
            {
                var sr = new Rect(control.x, control.y + 3, control.width - 128, 18);
                float v = GUI.HorizontalSlider(sr, (float)value, (float)d.Min, (float)d.Max);
                GUI.Label(sr, C("", tip), text);
                string key = Type + ":" + d.Key;
                if (Math.Abs(v - value) > .00001) { P.Set(Type, d.Key, Math.Round(v, 3)); numbers.Remove(key); Changed(); }
                if (!numbers.TryGetValue(key, out string s)) s = value.ToString("0.###", CultureInfo.InvariantCulture);
                GUI.SetNextControlName("Behavior_" + key);
                string next = GUI.TextField(new Rect(control.xMax - 120, control.y, 79, 22), s, 16, field);
                GUI.Label(new Rect(control.xMax - 120, control.y, 79, 22), C("", tip + " Enter a number using a dot or comma as decimal separator. Invalid text is not applied."));
                if (next != s)
                {
                    numbers[key] = next;
                    if (double.TryParse(next.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && !double.IsNaN(parsed) && !double.IsInfinity(parsed) && parsed >= d.Min && parsed <= d.Max)
                    { P.Set(Type, d.Key, parsed); Changed(); }
                }
                Label(new Rect(control.xMax - 36, control.y, 36, 22), d.Unit, tip, true);
            }
        }
        string Reference(ZBehaviorDef d, bool zod)
        {
            var f = (ZBehaviorFamily)family;
            if (Type != "") return d.Format(P.Reference(Type, d.Key, zod));
            var types = family == 0 ? ZBehaviorSchema.TankTypes : ZBehaviorSchema.RobotTypes;
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (string t in types) { double v = P.Reference(t, d.Key, zod); lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            return lo == hi ? d.Format(lo) : d.Format(lo) + " - " + d.Format(hi) + " (by type)";
        }
        string DetailedTip(ZBehaviorDef d)
        {
            string effect = d.Group == "MOVEMENT" ? "Motion: fields marked visual affect presentation only. Acceleration, braking, reversal and turning constraints affect actual positions only when Simulate physical movement limits is ON. Zero acceleration/braking/turn rate means instantaneous, not disabled." :
                d.Group == "BOOST" ? "Boost: only defensive sprint is changed. Unit statistics still supply maximum endurance. Existing bonuses use the strongest multiplier. Editing never refills endurance." :
                d.Group == "COMBAT" ? "Combat: these rules apply to the selected family/type. Existing weapon eligibility and native attack cooldowns remain. Moving fire is only implemented for grunt, psycho and laser." :
                "Dodging: enabling a situation does not bypass other requirements such as a protected doorway, an immobile/stunned unit, cooldown or stand-and-fire. A reaction is not a guaranteed successful escape. Retreat is a move away from an acquired enemy.";
            string refs = "";
            foreach (string t in family == 0 ? ZBehaviorSchema.TankTypes : ZBehaviorSchema.RobotTypes)
                if (Type == "" || Type == t) refs += DisplayType(t) + ": OpenZ " + d.Format(P.Reference(t, d.Key, false)) + "; Zod " + d.Format(P.Reference(t, d.Key, true)) + ". ";
            return d.Label + "\n" + d.Tip + "\n" + effect + "\nReferences: " + refs + "\nZod means the classic dodge logic before local fork commit 37af18e. Features absent from Zod use their disabled/instant/native equivalent, not invented Zod constants. Manual edits override the master; per-type edits override family values. Use INHERITED/OVERRIDE to release an override.";
        }
        void DrawProfiles(float w, float h)
        {
            Rect r = new Rect(16, 76, Mathf.Min(560, w - 32), Mathf.Min(320, h - 100));
            GUI.Box(r, GUIContent.none, box); var choices = OzBehaviorLibrary.Choices((ZBehaviorFamily)family);
            listScroll = GUI.BeginScrollView(new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 16), listScroll, new Rect(0, 0, r.width - 40, choices.Count * 35));
            for (int i = 0; i < choices.Count; i++)
            {
                var p = choices[i];
                if (Button(new Rect(0, i * 35, r.width - 42, 30), p.name, "Load this preset into the laboratory. Built-ins are read-only references: editing them makes a Custom draft. Unsaved edits are replaced; the saved library and game selection are unchanged."))
                { drafts[family] = p.Clone(); presetName = p.name; dirty = false; profiles = false; numbers.Clear(); Apply(family); status = "Loaded " + p.name + " into the laboratory."; }
            }
            GUI.EndScrollView();
        }
        void DrawImports(float w, float h)
        {
            Rect r = new Rect(16, 76, Mathf.Min(700, w - 32), Mathf.Min(320, h - 100)); GUI.Box(r, GUIContent.none, box);
            string[] files = Directory.Exists(OzBehaviorLibrary.Folder) ? Directory.GetFiles(OzBehaviorLibrary.Folder, "*.ozbehavior") : Array.Empty<string>();
            listScroll = GUI.BeginScrollView(new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 16), listScroll, new Rect(0, 0, r.width - 40, Math.Max(35, files.Length * 35)));
            if (files.Length == 0) Label(new Rect(8, 0, r.width - 50, 50), "No preset files. Use FOLDER and place .ozbehavior files here.", "Import lists local files only. It does not download or upload presets.");
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (Button(new Rect(0, i * 35, r.width - 42, 30), Path.GetFileName(file), "Import " + file + ". The file declares its family; the correct menu opens automatically. Save to add it to the game options."))
                    Safe(() => { var p = OzBehaviorLibrary.Import(file); family = (int)p.family; scope = 0; drafts[family] = p; presetName = p.name; imports = false; numbers.Clear(); Changed(); });
            }
            GUI.EndScrollView();
        }
        void Save(bool copy) => Safe(() => { OzBehaviorLibrary.Save(P, presetName, copy); OzBehaviorLibrary.SaveDraft(P); dirty = false; status = "Saved " + P.name + ". Select it in Options > Game for your next match."; });
        void Safe(Action action) { try { action(); } catch (Exception e) { status = e.Message; Debug.LogWarning("[OpenZ Behavior] " + e.Message); } }
        public static string DisplayType(string type) => type == "medium" ? "MECH" : type == ZMTank.Type ? "MEDIUM TANK" : type.ToUpperInvariant();
        void DrawDebug()
        {
            if (World == null || !Camera.main) return;
            foreach (var o in World.Objects)
            {
                if (o.Destroyed || o.Behavior == null || o.BehaviorThreatUntil < World.Time) continue;
                var p = Camera.main.WorldToScreenPoint(new Vector3(o.CenterX * ZConst.PX_TO_M, 1.5f, -o.CenterY * ZConst.PX_TO_M));
                if (p.z <= 0) continue;
                GUI.Label(new Rect(p.x - 90, Screen.height - p.y - 20, 220, 40), new GUIContent(o.Type + ": " + o.BehaviorDodgeReason, "Latest reason for accepting or refusing a projectile reaction."), text);
                var blast = Camera.main.WorldToScreenPoint(new Vector3(o.BehaviorThreatX * ZConst.PX_TO_M, .3f, -o.BehaviorThreatY * ZConst.PX_TO_M));
                if (blast.z > 0) DrawLine(new Vector2(p.x, Screen.height - p.y), new Vector2(blast.x, Screen.height - blast.y), Color.yellow);
                if (o.BehaviorDodging)
                {
                    var wp = o.Waypoints[0];
                    var end = Camera.main.WorldToScreenPoint(new Vector3(wp.x * ZConst.PX_TO_M, .3f, -wp.y * ZConst.PX_TO_M));
                    if (end.z > 0) DrawLine(new Vector2(p.x, Screen.height - p.y), new Vector2(end.x, Screen.height - end.y), Color.cyan);
                }
            }
        }
        static void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            var matrix = GUI.matrix; var old = GUI.color;
            GUI.color = color; GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y, Vector2.Distance(a, b), 2), Texture2D.whiteTexture);
            GUI.matrix = matrix; GUI.color = old;
        }
        void OnDestroy()
        {
            foreach (var p in drafts) if (p != null) OzBehaviorLibrary.SaveDraft(p);
            PlayerPrefs.Save(); OzUeHooks.SetTyping(this, false); OzUeInsets.Set(UeSide.Bottom, "Behavior laboratory", 0);
            if (button != null && button.Rt) Destroy(button.Rt.gameObject);
            if (blocker) Destroy(blocker.gameObject);
            if (I == this) I = null;
        }
    }
}
