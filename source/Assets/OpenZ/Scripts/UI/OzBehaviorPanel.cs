using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>Dynamics and Testing content hosted inside the existing right-hand editor panel.</summary>
    public sealed class OzBehaviorPanel : MonoBehaviour
    {
        public static OzBehaviorPanel I { get; private set; }
        public bool IsOpen => OzUeStats.BoxOpen && OzUeStats.LaboratoryTab;
        public bool TestingOpen => OzUeStats.CurrentTab == OzUeStats.Tab.Testing;
        public bool DebugEnabled { get; private set; }
        // All controls now use the ordinary HUD, so they must not bypass its input polling.
        public static bool OwnsPointer => false;
        readonly ZBehaviorProfile[] drafts = new ZBehaviorProfile[2];
        readonly List<OzUeLabInput> inputs = new List<OzUeLabInput>();
        readonly List<OzUeLabSlider> sliders = new List<OzUeLabSlider>();
        readonly List<RectTransform> rows = new List<RectTransform>();
        readonly List<Action> refresh = new List<Action>();
        readonly string[] pages = { "DODGING", "MOVEMENT", "BOOST", "COMBAT" };
        public Team RespawnPointTeam { get; private set; }
        bool builtTesting;
        RectTransform box;
        UeScroll list;
        UeBoxLayout layout;
        OzText footer;
        int family, page, scope;
        bool rebuild = true, dirty, profiles, imports;
        string presetName = "My behavior", status = "Changes apply live to this playground.";
        ZBehaviorProfile P => drafts[family];
        ZWorld World => OzUnitEditor.I != null ? OzUnitEditor.I.World : null;
        string Type => scope == 0 ? "" : (family == 0 ? ZBehaviorSchema.TankTypes : ZBehaviorSchema.RobotTypes)[scope - 1];
        public Rect PanelRect => layout.Box;
        void Awake() { I = this; }
        void Start()
        {
            for (int i = 0; i < 2; i++) { drafts[i] = OzBehaviorLibrary.Draft((ZBehaviorFamily)i).Clone(); World?.SetBehavior(drafts[i]); }
            presetName = P.name;
            OzUeKeys.OnEsc(95, () => { if (!IsOpen) return false; Close(); return true; });
        }
        void Update()
        {
            var ed = OzUnitEditor.I;
            if (!ed || !ed.Hud || !ed.Hud.Root || !OzUnitEditor.Layer(UeLayer.Panels)) return;
            var shell = OzUeStatsRoot.I?.Shell;
            if (shell == null || !shell.Box) return;
            bool visible = !OzUnitEditor.PanelsHidden;
            var next = shell.L;
            if (!box || box.parent != shell.Box || builtTesting != TestingOpen ||
                (next.Box.position - layout.Box.position).sqrMagnitude > .25f || (next.Box.size - layout.Box.size).sqrMagnitude > .25f) rebuild = true;
            layout = next;
            if (IsOpen && rebuild) Build();
            if (box) box.gameObject.SetActive(IsOpen && visible);
            if (!IsOpen || !visible) return;
            list?.Tick(!OzUeLabInput.AnyFocused);
            foreach (var r in rows) if (r) r.gameObject.SetActive(list.Shows(r));
            foreach (var input in inputs.ToArray()) input.Tick();
            foreach (var slider in sliders) slider.Tick();
            foreach (var update in refresh) update();
            footer?.Set(status);
        }
        public void Toggle() => OzUeStats.SetTab(IsOpen && !TestingOpen ? OzUeStats.Tab.Stats : OzUeStats.Tab.Dynamics);
        public void ShowTesting() => OzUeStats.SetTab(OzUeStats.Tab.Testing);
        void Close() { CommitInputs(); OzUeStats.SetTab(OzUeStats.Tab.Stats); }
        void FinishFields() { foreach (var i in inputs.ToArray()) i.Dispose(); }
        internal void CommitInputs() => FinishFields();
        public bool EntryIsClickable => Clickable(OzUeStatsRoot.I?.Shell?.TabButton(OzUeStats.Tab.Dynamics));
        public bool TestingEntryIsClickable => Clickable(OzUeStatsRoot.I?.Shell?.TabButton(OzUeStats.Tab.Testing));
        bool Clickable(HudButton button) => button != null && button.Alive && button.Interactable &&
            OzUnitEditor.I.Hud.IsOverUi(RectTransformUtility.WorldToScreenPoint(null, button.Rt.TransformPoint(button.Rt.rect.center)));
        public void OpenFromButton() { if (!EntryIsClickable) throw new InvalidOperationException("COMBAT DYNAMICS is not clickable."); OzUeStats.SetTab(OzUeStats.Tab.Dynamics); }
        public void ShowPage(ZBehaviorFamily f, int tab)
        { FinishFields(); family = (int)f; page = Mathf.Clamp(tab, 0, 3); scope = 0; presetName = P.name; OzUeStats.SetTab(OzUeStats.Tab.Dynamics); rebuild = true; }
        public ZBehaviorProfile Draft(ZBehaviorFamily f) => drafts[(int)f].Clone();
        public void Edit(ZBehaviorFamily f, ZBehaviorKey key, double value, string type = "")
        {
            var p = drafts[(int)f]; p.Set(type, key, value);
            // A manually tuned combat profile should return to its attack after the blast; the exact reference stays available.
            if (key != ZBehaviorKey.ResumeAttack && !p.Has("", ZBehaviorKey.ResumeAttack)) p.Set("", ZBehaviorKey.ResumeAttack, 1);
            Apply((int)f); dirty = true;
        }
        public void SetMaster(ZBehaviorFamily f, double value)
        { value = Math.Max(0, Math.Min(100, value)); if (drafts[(int)f].master == value) return; drafts[(int)f].master = value; Apply((int)f); dirty = true; }
        void Apply(int f) { World?.SetBehavior(drafts[f]); OzBehaviorLibrary.SaveDraft(drafts[f]); status = "CUSTOM - applied live. Save to use it in matches."; }
        HudButton Button(Transform parent, string name, string label, Rect r, string tip, Action click, int priority = 45)
        {
            var b = OzUeGfx.Button(parent, name, label, r.x, r.y, r.width, r.height, HudButtonStyle.Normal, priority, 11, .05f);
            b.OnClick = _ => Safe(click); Tip(b.Rt, tip, priority); return b;
        }
        void Tip(RectTransform rect, string text, int priority = 45) => OzUeSideTip.Add(rect, text, OzUeSideTip.Place.Side, priority, box);
        OzText Text(Transform parent, string text, float x, float y, float width, float size = 12, Color? colour = null)
            => OzUeGfx.Text(parent, "Label", text, size, colour ?? OzUi.Ink, 0, x, y, OzH.L, width);
        OzUeLabInput Input(Transform parent, string name, Rect r, Func<string> read, Action<string> write, string tip)
        { var i = new OzUeLabInput(parent, name, r, read, s => Safe(() => write(s))); inputs.Add(i); Tip(i.Button.Rt, tip); return i; }
        void Slider(Transform parent, string name, Rect r, double min, double max, Func<double> read, Action<double> write, string tip)
        { var s = new OzUeLabSlider(parent, name, r, min, max, read, write); sliders.Add(s); Tip(s.Button.Rt, tip); }
        void Build()
        {
            FinishFields(); inputs.Clear(); sliders.Clear(); rows.Clear(); refresh.Clear(); list = null;
            if (box) { box.gameObject.SetActive(false); Destroy(box.gameObject); }
            box = OzUeGfx.Node(OzUeStatsRoot.I.Shell.Box, "UE_LaboratoryContent", 0, 0, layout.w, layout.h);
            float w = layout.w;
            builtTesting = TestingOpen;
            footer = Text(box, status, 12, layout.h - 18, w - 24, 10, OzUi.Muted);
            if (TestingOpen) BuildTesting(); else BuildBehavior();
            rebuild = false; OzUeSideTip.Prune();
        }
        void MakeList(float top)
        { list = UeScroll.Make(box, "Parameters", 8, top, layout.w - 16, Mathf.Max(60, layout.h - top - 38), OzUi.GM900, 46); }
        RectTransform Row(string name, float y, float height)
        {
            var row = OzUeGfx.Node(list.Content, name, 0, y, layout.w - 40, height);
            OzUeGfx.Rect(row, "Well", OzUi.GM900, 0, 0, layout.w - 40, height - 3);
            rows.Add(row); return row;
        }
        void BuildBehavior()
        {
            float w = layout.w, inner = w - 24;
            for (int i = 0; i < 2; i++) { int f = i; Button(box, "Family" + i, i == 0 ? "TANKS" : "ROBOTS", new Rect(12 + i * (inner + 6) / 2, 42, (inner - 6) / 2, 28), "Change the edited family; the other family's values are preserved.", () => { family = f; scope = 0; presetName = P.name; profiles = imports = false; rebuild = true; }); }
            var preset = Button(box, "Presets", "", new Rect(12, 76, inner, 26), "Load a reference or named preset into the playground. This does not change the profiles selected for your next match.", () => { profiles = !profiles; imports = false; rebuild = true; });
            refresh.Add(() => preset.Label.Set((dirty ? "CUSTOM: " : "") + P.name + "  v"));
            Input(box, "PresetName", new Rect(12, 107, inner - 78, 26), () => presetName, s => presetName = s, "Name used by SAVE / SAVE AS. Enter commits; Escape cancels. Existing named presets can be updated; built-in references remain protected.");
            Button(box, "Save", "SAVE", new Rect(w - 84, 107, 72, 26), "Save this family, its master position and its manual overrides. Select it in Options > Game for future matches.", () => Save(false));
            string[] actions = { "SAVE AS", "EXPORT", "IMPORT", "FOLDER" };
            Action[] callbacks = { () => Save(true), () => status = "Exported: " + OzBehaviorLibrary.ExportJson(P), () => { imports = !imports; profiles = false; rebuild = true; }, () => { Directory.CreateDirectory(OzBehaviorLibrary.Folder); Application.OpenURL(new Uri(OzBehaviorLibrary.Folder).AbsoluteUri); } };
            string[] tips = { "Save a new independently named copy.", "Export a readable .json combat profile with the master, overrides and reference endpoints.", "Load a local .json or legacy .ozbehavior file; save it to add it to the game's preset list.", "Open the folder used to exchange .json and legacy .ozbehavior preset files. Nothing is uploaded." };
            for (int i = 0; i < 4; i++) { int k = i; Button(box, actions[i], actions[i], new Rect(12 + i * (inner + 4) / 4, 139, (inner - 12) / 4, 24), tips[i], callbacks[k]); }
            Text(box, "OPENZ", 12, 179, 70, 11, OzUi.Amber); Text(box, "ZOD CLASSIC", w - 108, 179, 96, 11, OzUi.Amber);
            var master = Text(box, "", inner / 2 - 20, 179, 84, 12);
            refresh.Add(() => master.Set(P.master.ToString("0.#", CultureInfo.InvariantCulture) + "%"));
            Slider(box, "Master", new Rect(12, 193, inner, 20), 0, 100, () => P.master, n => SetMaster((ZBehaviorFamily)family, Math.Round(n, 1)), "Master: OpenZ at 0%, classic Zod dodge/movement at 100%. Numeric values interpolate; switches change at 50%. Manual fields stay overridden. This is not a success percentage. Normal speed, weapon statistics and damage rules remain separate.");
            Button(box, "Scope", scope == 0 ? "ALL " + (family == 0 ? "TANKS" : "ROBOTS") : DisplayType(Type), new Rect(12, 219, inner * .55f, 26), "Cycle family defaults and per-type exceptions. Per-type overrides win; inherited fields follow the master.", () => { scope = (scope + 1) % ((family == 0 ? ZBehaviorSchema.TankTypes.Length : ZBehaviorSchema.RobotTypes.Length) + 1); rebuild = true; });
            Button(box, "ResetOverrides", "RESET OVERRIDES", new Rect(18 + inner * .55f, 219, inner * .45f - 6, 26), "Release every manual override in this family. Keep the master position.", () => { P.overrides.Clear(); Apply(family); dirty = true; rebuild = true; });
            for (int i = 0; i < 4; i++) { int k = i; Button(box, "Page" + i, pages[i], new Rect(12 + i * (inner + 4) / 4, 251, (inner - 12) / 4, 25), "Edit " + pages[i].ToLowerInvariant() + ". Every field has reference values and detailed help.", () => { page = k; profiles = imports = false; rebuild = true; }); }
            MakeList(283);
            if (profiles || imports) { BuildPresetList(); return; }
            float y = 0;
            foreach (var d in ZBehaviorSchema.All)
            {
                if (d.Group != pages[page]) continue;
                var def = d; var row = Row(d.Key.ToString(), y, 100); float rw = layout.w - 40; string tip = Explain(d.Key);
                var label = Text(row, d.Label, 9, 15, rw - 110, 12); Tip(label.rectTransform, tip);
                var reset = Button(row, "Inherit", "", new Rect(rw - 99, 4, 91, 23), "Release this field's manual override and inherit from the family/master again.", () => { P.Reset(Type, def.Key); Apply(family); });
                refresh.Add(() => reset.Label.Set(P.Has(Type, def.Key) ? "OVERRIDE" : "INHERITED"));
                var refs = Text(row, "OpenZ: " + Reference(d, false) + "  |  Zod: " + Reference(d, true), 9, 39, rw - 18, 10, OzUi.Muted); Tip(refs.rectTransform, tip);
                if (d.Choices != null)
                {
                    var choice = Button(row, "Choice", "", new Rect(9, 57, rw - 18, 28), tip, () => Edit((ZBehaviorFamily)family, def.Key, ((int)P.Get(Type, def.Key) + 1) % def.Choices.Length, Type));
                    refresh.Add(() => choice.Label.Set(def.Format(P.Get(Type, def.Key))));
                }
                else
                {
                    Input(row, "Value", new Rect(9, 57, 150, 28), () => P.Get(Type, def.Key).ToString("G12", CultureInfo.InvariantCulture), s => Edit((ZBehaviorFamily)family, def.Key, OzUeLabInput.Number(s), Type), tip + "\nType any finite value, including scientific notation. Enter or clicking elsewhere applies it; Escape cancels. There is no gameplay slider maximum.");
                    Text(row, d.Unit + "  /  free numeric input", 169, 71, rw - 178, 10, OzUi.Ink2);
                }
                y += 100;
            }
            var actionsRow = Row("Actions", y, 38);
            Button(actionsRow, "Native", "RESTORE OPENZ", new Rect(9, 4, (layout.w - 64) * .65f, 26), "Restore exact native behavior in this family; named presets remain saved.", () => { drafts[family] = OzBehaviorLibrary.Native((ZBehaviorFamily)family); presetName = P.name; dirty = true; Apply(family); rebuild = true; });
            var debug = Button(actionsRow, "Debug", "", new Rect(15 + (layout.w - 64) * .65f, 4, (layout.w - 64) * .35f, 26), "Show the last dodge reason above units. Useful to distinguish a committed dodge, exhausted sprint, blocked path and a forbidden reaction.", () => DebugEnabled = !DebugEnabled);
            refresh.Add(() => debug.Label.Set(DebugEnabled ? "DEBUG ON" : "DEBUG OFF"));
            list.SetContentHeight(y + 38);
        }
        void BuildTesting()
        {
            MakeList(42); var t = World.UnitTesting; float y = 0;
            foreach (Team team in new[] { Team.Red, Team.Blue })
            {
                Team side = team; string word = team == Team.Red ? "RED" : "BLUE";
                var heading = Row(word, y, 30); Text(heading, word + " UNITS", 10, 15, layout.w - 60, 13, team == Team.Red ? OzUi.RedHi : OzUi.BlueHi); y += 30;
                ToggleRow("Immortal " + word.ToLowerInvariant() + " units", () => t.Immortal(side), v => t.Configure(side, v, t.Circular(side)), ref y,
                    "Playground only. Units never enter destruction/removal, lose their drivers or abandon orders because of death. With circular damage OFF, actual HP stay at the reference percentage; a hidden HP pool still counts theoretical deaths. With circular damage ON, real HP decrease and reset at a virtual death.");
                ToggleRow("Circular damage " + word.ToLowerInvariant() + " units", () => t.Circular(side), v => t.Configure(side, t.Immortal(side), v), ref y,
                    "Only has an effect while this team's immortality is enabled. Damage lowers HP normally. A lethal hit records one death and one attacker kill, then restores exactly the reference HP percentage without moving the unit or clearing its orders. Excess damage does not kill several lives at once.");
                var row = Row(word + " HP", y, 87); float rw = layout.w - 40;
                string hpTip = "Reference HP for " + word + ": 1-100% of each unit's own maximum health. Applies immediately to current units and new spawns. Invulnerable units remain at this level. Circular units restart at this level after every virtual death. Changing the percentage resets their current HP and hidden damage pool, but keeps kill/death counts.";
                Text(row, "REFERENCE HP", 10, 16, rw - 120, 12); Input(row, word + "Hp", new Rect(rw - 98, 3, 88, 26), () => t.HpPercent(side).ToString("0.#", CultureInfo.InvariantCulture), s => t.SetHpPercent(side, OzUeLabInput.Number(s)), hpTip);
                Slider(row, word + "HpSlider", new Rect(10, 38, rw - 20, 22), 1, 100, () => t.HpPercent(side), n => { n = Math.Round(n, 1); if (n != t.HpPercent(side)) t.SetHpPercent(side, n); }, hpTip);
                var caption = Text(row, "", 10, 73, rw - 20, 10, OzUi.Muted); refresh.Add(() => caption.Set(t.HpPercent(side).ToString("0.#") + "%  |  applies to existing and future " + word + " units")); Tip(row, hpTip); y += 87;
            }
            ToggleRow("Show damage", () => t.ShowDamage, v => t.ShowDamage = v, ref y, "Floating HP lost above the unit for each landed hit. Invulnerable units display the HP they would have lost, marked as theoretical. Misses display no damage. Driver damage is identified separately.");
            var scores = Row("Scores", y, 94); float sw = layout.w - 40;
            const string scoreTip = "Trophy = kills, skull = deaths. Success above them = kills / (kills + deaths) x 100; no recorded fights = 0%. Immortals count virtual defeats too. Counts accumulate while hidden. Reset counters clears every unit's kills, deaths and success, without changing HP, orders or partial damage pools.";
            var scoreLabel = Text(scores, "Show kill and death counts", 10, 16, sw - 20, 12); Tip(scoreLabel.rectTransform, scoreTip);
            var countsToggle = Button(scores, "ShowCounts", "", new Rect(10, 33, 70, 27), scoreTip, () => t.ShowCounts = !t.ShowCounts);
            refresh.Add(() => { countsToggle.Label.Set(t.ShowCounts ? "ON" : "OFF"); countsToggle.Selected = t.ShowCounts; countsToggle.Refresh(); });
            Button(scores, "ResetCounters", "RESET COUNTERS", new Rect(88, 33, sw - 98, 27), scoreTip, t.ResetCounters);
            Text(scores, "Success = kills / (kills + deaths)", 10, 78, sw - 20, 10, OzUi.Muted); y += 94;
            ToggleRow("Reset automatically on parameters change", () => t.ResetAutomaticallyOnParametersChange, v => t.ResetAutomaticallyOnParametersChange = v, ref y,
                "Reset ALL units' kills, deaths and success whenever a changed effective combat-dynamics value affects at least one living unit fighting in this playground (target/order, dodge, incoming attack or hit in the last 3 seconds). Covers master, fields, presets, import and restoring defaults. Changing a name, saving, reapplying identical values or editing an absent/non-fighting type does not reset. Enabling this option alone does not reset counters. HP and orders remain unchanged.");
            ToggleRow("Respawning", () => t.Respawning, v => t.Respawning = v, ref y,
                "After a real or virtual defeat, reset both opponents to their team points, restore reference HP and crew, clear old projectiles and attack again. Set both points first. Works with or without immortality and cycling damage.");
            foreach (Team side in new[] { Team.Red, Team.Blue })
            {
                Team team = side; var row = Row("RespawnPoint" + team, y, 42);
                var button = Button(row, "SetPoint", "", new Rect(10, 4, layout.w - 60, 30),
                    "Click, then choose a ground point on the field. Right-click or Escape cancels.", () =>
                    {
                        OzUePlace.Stop(); RespawnPointTeam = team; OzUePick.HoldInput(this, true);
                        OzUeToast.Show("CLICK THE " + team.ToString().ToUpperInvariant() + " RESPAWN POINT");
                    });
                refresh.Add(() => button.Label.Set("SET " + team.ToString().ToUpperInvariant() + " RESPAWN POINT" + (t.HasRespawnPoint(team) ? " [SET]" : "")));
                y += 42;
            }
            list.SetContentHeight(y); status = "Testing rules apply only in this playground; combat profiles are unchanged.";
        }
        void ToggleRow(string label, Func<bool> get, Action<bool> set, ref float y, string tip)
        {
            var row = Row(label, y, 58); float rw = layout.w - 40;
            Text(row, label, 10, 18, rw - 95, 12);
            var b = Button(row, "Toggle", "", new Rect(rw - 80, 6, 70, 27), tip, () => set(!get()));
            refresh.Add(() => { b.Label.Set(get() ? "ON" : "OFF"); b.Selected = get(); b.Refresh(); });
            Text(row, "Hover for detailed rules", 10, 43, rw - 20, 10, OzUi.Muted); Tip(row, tip); y += 58;
        }
        void BuildPresetList()
        {
            float y = 0;
            if (profiles)
            {
                foreach (var choice in OzBehaviorLibrary.Choices((ZBehaviorFamily)family))
                {
                    var p = choice; var row = Row(p.name, y, 36);
                    Button(row, "Load", p.name, new Rect(6, 2, layout.w - 52, 29), "Load this preset into the playground. Unsaved edits are replaced; match selection stays unchanged.", () => { drafts[family] = p.Clone(); presetName = p.name; profiles = false; dirty = false; Apply(family); rebuild = true; }); y += 36;
                }
                var del = Row("Delete", y, 40);
                Button(del, "DeletePreset", "DELETE CURRENT SAVED PRESET", new Rect(6, 3, layout.w - 52, 28), "Delete the current named preset; protected built-in references cannot be deleted.", () => { if (!OzBehaviorLibrary.SavedProfiles.Exists(p => p.id == P.id)) throw new InvalidOperationException("Built-in presets are protected."); OzBehaviorLibrary.Delete(P); rebuild = true; }); y += 40;
            }
            else
            {
                var files = Directory.Exists(OzBehaviorLibrary.Folder) ? Array.FindAll(Directory.GetFiles(OzBehaviorLibrary.Folder), f => string.Equals(Path.GetExtension(f), ".json", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(f), ".ozbehavior", StringComparison.OrdinalIgnoreCase)) : Array.Empty<string>();
                if (files.Length == 0) { Text(list.Content, "No files. Use FOLDER to add .json or .ozbehavior presets.", 9, 20, layout.w - 58, 11); y = 50; }
                foreach (string path in files)
                {
                    string file = path; var row = Row(Path.GetFileName(file), y, 36);
                    Button(row, "Import", Path.GetFileName(file), new Rect(6, 2, layout.w - 52, 29), "Import this preset as a draft, then save it to use it in matches.", () => { var p = OzBehaviorLibrary.Import(file); family = (int)p.family; scope = 0; drafts[family] = p; presetName = p.name; imports = false; dirty = true; Apply(family); rebuild = true; }); y += 36;
                }
            }
            list.SetContentHeight(y);
        }
        string Reference(ZBehaviorDef d, bool zod)
        {
            if (Type != "") return d.Format(P.Reference(Type, d.Key, zod));
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var type in family == 0 ? ZBehaviorSchema.TankTypes : ZBehaviorSchema.RobotTypes) { double v = P.Reference(type, d.Key, zod); lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            return lo == hi ? d.Format(lo) : d.Format(lo) + " - " + d.Format(hi) + " by type";
        }
        public string Explain(ZBehaviorKey key)
        {
            var d = ZBehaviorSchema.Def(key); string refs = "";
            foreach (var type in family == 0 ? ZBehaviorSchema.TankTypes : ZBehaviorSchema.RobotTypes)
                if (Type == "" || Type == type) refs += DisplayType(type) + ": OpenZ " + d.Format(P.Reference(type, key, false)) + "; Zod " + d.Format(P.Reference(type, key, true)) + ".\n";
            return d.Label + "\n" + d.Tip + "\n" + refs + "Zod Classic is the pre-#103 dodge/stop-and-fire behavior; Zod Local is idle-only. It does not replace projectile speeds, armor, range, collision, or weapon stats. Manual values override the master; numeric values are not limited by a slider. Physical movement limits affect real positions only when enabled; visual settings affect the model. Zero means instant/unlimited only where the label explicitly says so.";
        }
        void Save(bool copy) { FinishFields(); OzBehaviorLibrary.Save(P, presetName, copy); OzBehaviorLibrary.SaveDraft(P); dirty = false; status = "Saved " + P.name + ". Select it in Options > Game."; }
        void Safe(Action action) { try { action(); } catch (Exception e) { status = e.Message; Debug.LogWarning("[OpenZ Laboratory] " + e.Message); } }
        public static string DisplayType(string type) => type == "medium" ? "MECH" : type == ZMTank.Type ? "MEDIUM TANK" : type.ToUpperInvariant();
        public void FinishRespawnPoint()
        { RespawnPointTeam = Team.Neutral; OzUePick.HoldInput(this, false); }
        void OnDestroy()
        {
            FinishRespawnPoint(); FinishFields(); foreach (var p in drafts) if (p != null) OzBehaviorLibrary.SaveDraft(p);
            PlayerPrefs.Save(); OzUeHooks.SetTyping(this, false);

            if (box) Destroy(box.gameObject);
            if (I == this) I = null;
        }
    }
}
