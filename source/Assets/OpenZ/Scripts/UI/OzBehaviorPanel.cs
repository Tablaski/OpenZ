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
        readonly string[] pages = { "FEEL", "DODGING", "MOVEMENT", "BOOST", "COMBAT", "CRIPPLING", "EASY TUNING" };
        readonly Dictionary<string, double> agilityDraft = new Dictionary<string, double>();
        readonly bool[] advanced = new bool[6];
        int healthBand = 1, selectedDirection;
        public bool SimulationDebug { get; private set; }
        public bool RouteDebug { get; private set; }
        static readonly ZBehaviorKey[] essentials = { ZBehaviorKey.MaxDuration, ZBehaviorKey.MaxDistance,
            ZBehaviorKey.BoostDuration, ZBehaviorKey.BoostMaxDistance, ZBehaviorKey.Physical, ZBehaviorKey.TurnFirst,
            ZBehaviorKey.PivotSpeed, ZBehaviorKey.AlignmentTolerance, ZBehaviorKey.TurnRate,
            ZBehaviorKey.TurnAcceleration, ZBehaviorKey.Acceleration, ZBehaviorKey.Braking,
            ZBehaviorKey.DirectView, ZBehaviorKey.Distance, ZBehaviorKey.DodgeSpeed, ZBehaviorKey.BoostSpeed, ZBehaviorKey.Cooldown, ZBehaviorKey.DodgePhysical };
        static bool Primary(ZBehaviorKey k) => Array.IndexOf(essentials, k) >= 0 || k == ZBehaviorKey.Enabled || k == ZBehaviorKey.Boost || k == ZBehaviorKey.CombatManoeuvres || k == ZBehaviorKey.Physical;
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
        { FinishFields(); family = (int)f; page = Mathf.Clamp(tab, 0, 4) + 1; scope = 0; presetName = P.name; OzUeStats.SetTab(OzUeStats.Tab.Dynamics); rebuild = true; }
        internal void ShowEasyTuning(ZBehaviorProfile reference)
        {
            family = 0; page = 6; scope = 0;
            LoadProfile(reference);
            OzUeStats.SetTab(OzUeStats.Tab.Dynamics);
        }
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
            for (int i = 0; i < 2; i++) { int f = i; Button(box, "Family" + i, i == 0 ? "TANKS" : "ROBOTS", new Rect(12 + i * (inner + 6) / 2, 42, (inner - 6) / 2, 28), "Edit tanks or robots independently.", () => { family = f; scope = 0; presetName = P.name; profiles = imports = false; rebuild = true; }); }
            var preset = Button(box, "Presets", "", new Rect(12, 76, inner, 26), "Load a reference or named preset into the playground. This does not change the profiles selected for your next match.", () => { profiles = !profiles; imports = false; rebuild = true; });
            refresh.Add(() => preset.Label.Set((dirty ? "CUSTOM: " : "") + P.name + "  v"));
            Input(box, "PresetName", new Rect(12, 107, inner - 78, 26), () => presetName, s => presetName = s, "Name used by SAVE / SAVE AS. Enter commits; Escape cancels. Existing named presets can be updated; built-in references remain protected.");
            Button(box, "Save", "SAVE", new Rect(w - 84, 107, 72, 26), "Save this family, its master position and its manual overrides. The host selects it under DYNAMICS in the 1 vs 1 lobby.", () => Save(false));
            string[] actions = { "SAVE AS", "EXPORT", "IMPORT", "FOLDER" };
            Action[] callbacks = { () => Save(true), () => status = "Exported: " + OzBehaviorLibrary.ExportJson(P), () => { imports = !imports; profiles = false; rebuild = true; }, () => { Directory.CreateDirectory(OzBehaviorLibrary.Folder); Application.OpenURL(new Uri(OzBehaviorLibrary.Folder).AbsoluteUri); } };
            string[] tips = { "Save a new independently named copy.", "Export a readable .json combat profile with the master, overrides and reference endpoints.", "Load a local .json or legacy .ozbehavior file; save it to add it to the game's preset list.", "Open the folder used to exchange .json and legacy .ozbehavior preset files. Nothing is uploaded." };
            for (int i = 0; i < 4; i++) { int k = i; Button(box, actions[i], actions[i], new Rect(12 + i * (inner + 4) / 4, 139, (inner - 12) / 4, 24), tips[i], callbacks[k]); }
            Button(box, "Scope", scope == 0 ? "ALL " + (family == 0 ? "TANKS" : "ROBOTS") : DisplayType(Type), new Rect(12, 173, inner * .55f, 26), "Choose all units or one type. Type-specific values override family settings.", () => { scope = (scope + 1) % ((family == 0 ? ZBehaviorSchema.TankTypes.Length : ZBehaviorSchema.RobotTypes.Length) + 1); rebuild = true; });
            Button(box, "ResetOverrides", "RESET OVERRIDES", new Rect(18 + inner * .55f, 173, inner * .45f - 6, 26), "Release every manual override in this family.", () =>
            {
                FinishFields();
                agilityDraft.Clear();
                P.overrides = P.id == "zod-classic" ? ZBehaviorProfile.Classic((ZBehaviorFamily)family).overrides : new List<ZBehaviorOverride>();
                Apply(family); dirty = true; rebuild = true;
            });
            for (int i = 0; i < pages.Length; i++) { int k = i; var tab = Button(box, "Page" + i, pages[i], new Rect(12 + i % 4 * (inner + 4) / 4, 205 + i / 4 * 29, (inner - 12) / 4, 25), "Edit " + pages[i].ToLowerInvariant() + ".", () => { FinishFields(); page = k; profiles = imports = false; agilityDraft.Clear(); rebuild = true; }); if (i >= 5) tab.Label.color = OzForkOptions.Accent; }
            MakeList(264);
            if (profiles || imports) { BuildPresetList(); return; }
            if (page == 6) { BuildEasyTuning(); return; }
            float y = 0;
            var intro = Row("Guide", y, 72);
            Text(intro, "OpenZ-VK | " + (page == 0 ? "MOVEMENT AND DODGE FEEL" : pages[page]), 9, 16, layout.w - 58, 12, OzForkOptions.Accent);
            var mode = Text(intro, "", 9, 39, layout.w - 58, 10, OzUi.Muted);
            refresh.Add(() => mode.Set(P.Get(Type, ZBehaviorKey.Physical) >= .5 ?
                P.Get(Type, ZBehaviorKey.TurnFirst) >= .5 ? "BRAKE > PIVOT > ACCELERATE: hull controls real movement" : "PHYSICAL: changes real combat positions" :
                P.Get(Type, ZBehaviorKey.DirectView) >= .5 ? "DIRECT: 2D position + rate-limited hull" : "FOLLOWER: catch-up can distort dodge paths"));
            y += 72;
            if (page == 5)
            {
                var bands = Row("HealthBand", y, 60);
                if (family != 0) { Text(bands, "Crippling applies to tanks. Select TANKS above.", 9, 22, layout.w - 58, 11, OzForkOptions.Accent); list.SetContentHeight(y + 60); return; }
                Text(bands, "GREEN >50%  |  YELLOW >25-50%  |  RED <=25% HP", 9, 14, layout.w - 58, 10);
                for (int i = 0; i < 3; i++) { int b = i; Button(bands, "Band" + i, (i == healthBand ? "[" : "") + ((ZHealthBand)i).ToString().ToUpperInvariant() + (i == healthBand ? "]" : ""), new Rect(9 + i * (layout.w - 58) / 3, 28, (layout.w - 64) / 3, 26), "Edit this health colour. Hidden bands retain their values. Matches the Unit Editor hover bar thresholds.", () => { healthBand = b; rebuild = true; }); }
                y += 60;
            }
            if (page == 0 || page == 2)
            {
                var setup = Row("TurnFirstSetup", y, 44);
                Button(setup, "Apply", "BRAKE > PIVOT > ACCELERATE", new Rect(9, 5, layout.w - 58, 30),
                    "Apply stop-then-turn movement: pivot speed 0%, alignment 5 deg, turn 720 deg/s, acceleration 0.25 s, braking 0.15 s. Keeps dodge settings and type exceptions.",
                    () => { FinishFields(); P.ConfigureTurnFirst(Type); Apply(family); dirty = true; });
                y += 44;
            }
            IEnumerable<ZBehaviorDef> definitions = page == 0 ? Array.ConvertAll(essentials, ZBehaviorSchema.Def) : ZBehaviorSchema.All;
            foreach (var d in definitions)
            {
                if (page != 0 && (d.Group != pages[page] || page != 5 && !Primary(d.Key) && !advanced[page])) continue;
                if (page == 5 && d.Key >= ZBehaviorKey.GreenShake && d.Key <= ZBehaviorKey.RedOilLife &&
                    ((int)d.Key - (int)ZBehaviorKey.GreenShake) / 6 != healthBand) continue;
                var def = d; var row = Row(d.Key.ToString(), y, 100); float rw = layout.w - 40; string tip = Explain(d.Key);
                var label = Text(row, d.Label, 9, 15, rw - 110, 12); Tip(label.rectTransform, tip);
                if (OzForkOptions.IsFork(d.Label)) label.color = OzForkOptions.Accent;
                var reset = Button(row, "Inherit", "", new Rect(rw - 99, 4, 91, 23), "Release this field's manual override and inherit from the family/master again.", () => { P.Reset(Type, def.Key); Apply(family); });
                refresh.Add(() => reset.Label.Set(P.Has(Type, def.Key) ? "OVERRIDE" : "INHERITED"));
                var refs = Text(row, Brief(d.Tip), 9, 39, rw - 18, 10, OzUi.Muted); Tip(refs.rectTransform, tip);
                if (d.Choices != null)
                {
                    var choice = Button(row, "Choice", "", new Rect(9, 57, rw - 18, 28), tip, () => Edit((ZBehaviorFamily)family, def.Key, ((int)P.Get(Type, def.Key) + 1) % def.Choices.Length, Type));
                    refresh.Add(() => choice.Label.Set(def.Format(P.Get(Type, def.Key))));
                }
                else
                {
                    Input(row, "Value", new Rect(9, 57, 150, 28), () => P.Get(Type, def.Key).ToString("G12", CultureInfo.InvariantCulture), s => Edit((ZBehaviorFamily)family, def.Key, OzUeLabInput.Number(s), Type), tip + "\nEnter applies. Escape cancels.");
                    var variance = def.Key switch
                    {
                        ZBehaviorKey.Distance => ZBehaviorKey.DistanceVariance,
                        ZBehaviorKey.DodgeSpeed => ZBehaviorKey.SpeedVariance,
                        ZBehaviorKey.Acceleration => ZBehaviorKey.AccelerationVariance,
                        ZBehaviorKey.BoostSpeed => ZBehaviorKey.BoostVariance,
                        ZBehaviorKey.TurnRate => ZBehaviorKey.PivotVariance,
                        _ => ZBehaviorKey.Count
                    };
                    if (variance != ZBehaviorKey.Count)
                    {
                        Text(row, "VARIANCE", 170, 71, 90, 10, OzForkOptions.Accent);
                        Input(row, "Variance", new Rect(250, 57, rw - 262, 28),
                            () => P.Get(Type, variance).ToString("G12", CultureInfo.InvariantCulture),
                            s => Edit((ZBehaviorFamily)family, variance, OzUeLabInput.Number(s), Type), "Total variance. 0.2 gives a range of −10% to +10%.");
                    }
                    else Text(row, d.Unit + (d.Label.Contains("unlimited") ? " | 0: unlimited" : d.Label.Contains("instant") ? " | 0: instant" : ""), 169, 71, rw - 178, 10, OzUi.Ink2);
                }
                y += 100;
                if (page == 0 && d.Key == ZBehaviorKey.BoostMaxDistance) BuildDirectionCircle(ref y);
            }
            if (page == 1) BuildDirectionCircle(ref y);
            if (page != 0 && page != 5)
            {
                var more = Row("Advanced", y, 38);
                Button(more, "Advanced", advanced[page] ? "ADVANCED - HIDE" : "ADVANCED - SHOW FINE TUNING", new Rect(9, 4, layout.w - 58, 26), "Extra rules and visual fine tuning. Hidden values remain active; saved profiles are preserved.", () => { advanced[page] = !advanced[page]; rebuild = true; });
                y += 38;
            }
            ToggleRow("Debug: combat position + visual lag", () => SimulationDebug, v => SimulationDebug = v, ref y, "Cyan marks the combat position. Violet shows model lag above 5 cm. Display only; combat is unchanged.");
            ToggleRow("Debug: movement target + velocity", () => RouteDebug, v => RouteDebug = v, ref y, "Yellow points to the movement target; green shows commanded velocity before physical steering. Display only; arrows are capped at 6 m.");
            var actionsRow = Row("Actions", y, 38);
            Button(actionsRow, "Native", "RESTORE OPENZ", new Rect(9, 4, (layout.w - 64) * .65f, 26), "Restore exact native behavior in this family; named presets remain saved.", () => { drafts[family] = OzBehaviorLibrary.Native((ZBehaviorFamily)family); presetName = P.name; dirty = true; Apply(family); rebuild = true; });
            var debug = Button(actionsRow, "Debug", "", new Rect(15 + (layout.w - 64) * .65f, 4, (layout.w - 64) * .35f, 26), "Show the last dodge reason above units. Useful to distinguish a committed dodge, exhausted sprint, blocked path and a forbidden reaction.", () => DebugEnabled = !DebugEnabled);
            refresh.Add(() => debug.Label.Set(DebugEnabled ? "DEBUG ON" : "DEBUG OFF"));
            list.SetContentHeight(y + 38);
        }
        void BuildEasyTuning()
        {
            float y = 0, width = layout.w - 40;
            if (family != 0)
            {
                Text(Row("Easy tuning", 0, 48), "Select TANKS to adjust dodge agility.", 9, 22, width - 18, 11, OzForkOptions.Accent);
                list.SetContentHeight(48);
                return;
            }
            foreach (string type in ZBehaviorSchema.TankTypes)
            {
                var row = Row("Agility " + type, y, 44);
                if (!agilityDraft.ContainsKey(type)) agilityDraft[type] = P.Get(type, ZBehaviorKey.Agility);
                Text(row, DisplayType(type), 9, 20, width - 365, 11, OzForkOptions.Accent);
                Input(row, "Agility", new Rect(width - 341, 6, 65, 28),
                    () => agilityDraft[type].ToString("0.#", CultureInfo.InvariantCulture),
                    s => agilityDraft[type] = ZBehaviorSchema.Def(ZBehaviorKey.Agility).Clamp(OzUeLabInput.Number(s)), "Percentage of the light tank's dodge tuning.");
                Text(row, "%", width - 268, 20, 18, 11);
                Button(row, "Reset agility", "RESET TO DEFAULT", new Rect(width - 246, 6, 153, 28),
                    "Restore the Z Classic percentage for this tank.", () => agilityDraft[type] = ZBehaviorProfile.ClassicTankAgility(type));
                Button(row, "Apply agility", "APPLY", new Rect(width - 84, 6, 75, 28),
                    "Apply dodge distance, acceleration, boost and hull pivot for this tank.", () => { P.ApplyTankAgility(type, agilityDraft[type]); Apply(family); dirty = true; });
                y += 44;
            }
            list.SetContentHeight(y);
        }
        void BuildTesting()
        {
            MakeList(42); var t = World.UnitTesting; float y = 0;
            foreach (Team team in new[] { Team.Red, Team.Blue })
            {
                Team side = team; string word = team == Team.Red ? "RED" : "BLUE";
                var heading = Row(word, y, 30); Text(heading, word + " UNITS", 10, 15, layout.w - 60, 13, team == Team.Red ? OzUi.RedHi : OzUi.BlueHi); y += 30;
                ToggleRow("Immortal " + word.ToLowerInvariant() + " units", () => t.Immortal(side), v => t.Configure(side, v, t.Circular(side)), ref y,
                    "Prevent destruction and crew loss. OFF for circular damage keeps HP fixed and counts virtual defeats; ON lowers HP and refills it at defeat.");
                ToggleRow("Circular damage " + word.ToLowerInvariant() + " units", () => t.Circular(side), v => t.Configure(side, t.Immortal(side), v), ref y,
                    "Requires immortality. HP decrease on hits, then refill to Reference HP at defeat. Count one death and attacker kill; keep position and orders.");
                var row = Row(word + " HP", y, 87); float rw = layout.w - 40;
                string hpTip = "Reference HP for " + word + ": 1-100% of each unit's own maximum health. Applies immediately to current units and new spawns. Invulnerable units remain at this level. Circular units restart at this level after every virtual death. Changing the percentage resets their current HP and hidden damage pool, but keeps kill/death counts.";
                Text(row, "REFERENCE HP", 10, 16, rw - 120, 12); Input(row, word + "Hp", new Rect(rw - 98, 3, 88, 26), () => t.HpPercent(side).ToString("0.#", CultureInfo.InvariantCulture), s => t.SetHpPercent(side, OzUeLabInput.Number(s)), hpTip);
                Slider(row, word + "HpSlider", new Rect(10, 38, rw - 20, 22), 1, 100, () => t.HpPercent(side), n => { n = Math.Round(n, 1); if (n != t.HpPercent(side)) t.SetHpPercent(side, n); }, hpTip);
                var caption = Text(row, "", 10, 73, rw - 20, 10, OzUi.Muted); refresh.Add(() => caption.Set(t.HpPercent(side).ToString("0.#") + "%  |  applies to existing and future " + word + " units")); Tip(row, hpTip); y += 87;
            }
            ToggleRow("Show damage", () => t.ShowDamage, v => t.ShowDamage = v, ref y, "Floating HP lost above the unit for each landed hit. Invulnerable units display the HP they would have lost, marked as theoretical. Misses display no damage. Driver damage is identified separately.");
            var scores = Row("Scores", y, 94); float sw = layout.w - 40;
            const string scoreTip = "Trophy: kills. Skull: deaths. Success: kills / total defeats. Immortals count virtual defeats. Reset clears scores only, including hidden scores.";
            var scoreLabel = Text(scores, "Show kill and death counts", 10, 16, sw - 20, 12); Tip(scoreLabel.rectTransform, scoreTip);
            var countsToggle = Button(scores, "ShowCounts", "", new Rect(10, 33, 70, 27), scoreTip, () => t.ShowCounts = !t.ShowCounts);
            refresh.Add(() => { countsToggle.Label.Set(t.ShowCounts ? "ON" : "OFF"); countsToggle.Selected = t.ShowCounts; countsToggle.Refresh(); });
            Button(scores, "ResetCounters", "RESET COUNTERS", new Rect(88, 33, sw - 98, 27), scoreTip, t.ResetCounters);
            Text(scores, "Success = kills / (kills + deaths)", 10, 78, sw - 20, 10, OzUi.Muted); y += 94;
            ToggleRow("Reset automatically on parameters change", () => t.ResetAutomaticallyOnParametersChange, v => t.ResetAutomaticallyOnParametersChange = v, ref y,
                "Clear scores when a dynamics change affects units currently fighting. Saving, renaming and changes to inactive types keep scores. HP and orders stay unchanged.");
            ToggleRow("Respawning", () => t.Respawning, v => t.Respawning = v, ref y,
                "After defeat, return both opponents to their team points, refill HP and crew, clear projectiles and restart combat. Set both points first.");
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
            Text(row, ToggleSummary(label), 10, 43, rw - 20, 10, OzUi.Muted); Tip(row, tip); y += 58;
        }
        void BuildPresetList()
        {
            float y = 0;
            if (profiles)
            {
                foreach (var choice in OzBehaviorLibrary.Choices((ZBehaviorFamily)family))
                {
                    var p = choice; var row = Row(p.name, y, 36);
                    Button(row, "Load", p.name, new Rect(6, 2, layout.w - 52, 29), "Load this preset into the playground. Unsaved edits are replaced; match selection stays unchanged.", () => LoadProfile(p)); y += 36;
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
                    Button(row, "Import", Path.GetFileName(file), new Rect(6, 2, layout.w - 52, 29), "Import this preset as a draft, then save it to use it in matches.", () => { var p = OzBehaviorLibrary.Import(file); LoadProfile(p); scope = 0; dirty = true; }); y += 36;
                }
            }
            list.SetContentHeight(y);
        }
        void LoadProfile(ZBehaviorProfile profile)
        {
            FinishFields();
            agilityDraft.Clear();
            family = (int)profile.family;
            drafts[family] = profile.Clone();
            presetName = profile.name;
            profiles = imports = dirty = false;
            Apply(family);
            rebuild = true;
        }
        static string ToggleSummary(string label)
        {
            if (label.StartsWith("Immortal", StringComparison.Ordinal)) return "Keep units alive after defeat.";
            if (label.StartsWith("Circular", StringComparison.Ordinal)) return "Cycle HP back to full after defeat.";
            if (label.StartsWith("Reset automatically", StringComparison.Ordinal)) return "Clear scores when active combat settings change.";
            if (label == "Respawning") return "Return opponents to their starting points after defeat.";
            if (label.Contains("visual lag")) return "Compare combat position with the displayed model.";
            if (label.Contains("velocity")) return "Show destination and commanded movement arrows.";
            if (label.Contains("damage")) return "Show recent damage above each unit.";
            return "Playground display and testing setting.";
        }
        static string Brief(string tip)
        { int stop = tip.IndexOf(". ", StringComparison.Ordinal); string first = stop < 0 ? tip : tip.Substring(0, stop); return first.Length <= 74 ? first : first.Substring(0, first.LastIndexOf(' ', 70)) + "..."; }
        void BuildDirectionCircle(ref float y)
        {
            float rw = layout.w - 40, cx = rw / 2, radius = Mathf.Min(155, (rw - 90) / 2);
            float cy = 66 + radius, bottom = cy + radius + 27;
            var row = Row("DodgeCircle", y, bottom + 146);
            Text(row, "OpenZ-VK | DODGE DIRECTIONS", 9, 17, rw - 18, 12, OzForkOptions.Accent);
            Text(row, "0 deg: forward | clockwise | 180 deg: back", 9, 38, rw - 18, 10, OzUi.Muted);
            var nativeCircleValues = new ZBehaviorProfile { family = (ZBehaviorFamily)family, master = 100 }.Resolve(Type);
            var circleValues = P.Resolve(Type) ?? nativeCircleValues;
            int circleRevision = World.BehaviorRevision;
            refresh.Add(() => { if (circleRevision != World.BehaviorRevision) { circleRevision = World.BehaviorRevision; circleValues = P.Resolve(Type) ?? nativeCircleValues; } });
            for (int segment = 0; segment < 64; segment++)
            {
                float a = segment * Mathf.PI / 32, c = (segment + 1) * Mathf.PI / 32;
                float x1 = cx + Mathf.Sin(a) * radius, y1 = cy - Mathf.Cos(a) * radius;
                float x2 = cx + Mathf.Sin(c) * radius, y2 = cy - Mathf.Cos(c) * radius;
                float length = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));
                var line = OzUeGfx.Rect(row, "Ring" + segment, OzUi.A(OzForkOptions.Accent, .45f), (x1 + x2 - length) / 2, (y1 + y2) / 2 - .5f, length, 1).rectTransform;
                line.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg);
            }
            for (int i = 0; i < 16; i++)
            {
                int index = i; float angle = i * Mathf.PI / 8;
                float x = cx + Mathf.Sin(angle) * radius, yy = cy - Mathf.Cos(angle) * radius;
                var spoke = OzUeGfx.Rect(row, "Spoke" + i, OzUi.A(OzForkOptions.Accent, .3f), cx, cy, 1, radius - 22).rectTransform;
                spoke.pivot = new Vector2(.5f, 0); spoke.localRotation = Quaternion.Euler(0, 0, -i * 22.5f);
                var button = Button(row, "Direction" + i, "", new Rect(x - Mathf.Min(27, radius * .185f), yy - 16, Mathf.Min(54, radius * .37f), 32),
                    (i * 22.5).ToString("0.#", CultureInfo.InvariantCulture) + " deg clockwise from forward. Select to edit its weight.",
                    () => { FinishFields(); selectedDirection = index; });
                refresh.Add(() => {
                    var v = circleValues;
                    button.Label.Set((index * 22.5).ToString("0.#", CultureInfo.InvariantCulture) + "\n" + (100 * ZObject.BehaviorDirectionProbability(v, index)).ToString("0.#", CultureInfo.InvariantCulture) + "%");
                    button.Label.color = index == selectedDirection ? OzForkOptions.Accent : OzUi.Ink;
                });
            }
            var mode = Button(row, "Mode", "", new Rect(cx - 64, cy - 18, 128, 36),
                "Classic: exact continuous Zod angles. Circle: 16 relative directions. Editing a weight enables the circle.",
                () => Edit((ZBehaviorFamily)family, ZBehaviorKey.Direction, P.Get(Type, ZBehaviorKey.Direction) == 3 ? 0 : 3, Type));
            refresh.Add(() => mode.Label.Set(P.Get(Type, ZBehaviorKey.Direction) == 3 ? "CIRCLE ACTIVE" : P.Get(Type, ZBehaviorKey.Direction) == 0 ? "ZOD CLASSIC" : "USE CIRCLE"));
            var selected = Text(row, "", 9, bottom + 8, rw - 18, 11, OzForkOptions.Accent);
            refresh.Add(() => selected.Set("Direction " + (selectedDirection * 22.5).ToString("0.#", CultureInfo.InvariantCulture) + " deg | weight"));
            Input(row, "DirectionWeight", new Rect(9, bottom + 22, 110, 28),
                () => P.Get(Type, (ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + selectedDirection)).ToString("G12", CultureInfo.InvariantCulture),
                value => { Edit((ZBehaviorFamily)family, (ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + selectedDirection), OzUeLabInput.Number(value), Type); Edit((ZBehaviorFamily)family, ZBehaviorKey.Direction, 3, Type); },
                "Chance = weight / total enabled weights. Zero excludes a direction; all zero prevents dodging. Blocked directions are refused.");
            Button(row, "ResetZod", "RESET ZOD", new Rect(128, bottom + 22, rw - 137, 28),
                "Restore exact Zod angles, reference weights and direction permissions for this scope. Other settings stay unchanged.", () => {
                    FinishFields();
                    for (int i = 0; i < 16; i++) { var key = (ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + i); P.Set(Type, key, ZBehaviorSchema.Def(key).Zod); }
                    P.Set(Type, ZBehaviorKey.Direction, 0);
                    P.Set(Type, ZBehaviorKey.Forward, 1); P.Set(Type, ZBehaviorKey.Backward, 1); P.Set(Type, ZBehaviorKey.Sideways, 1);
                    Apply(family); dirty = true;
                });
            Text(row, "Zod reference: 1,000 native draws grouped into 16 bins.", 9, bottom + 70, rw - 18, 10, OzUi.Muted);
            Text(row, "Classic: world angles. Circle: relative to forward.", 9, bottom + 89, rw - 18, 10, OzUi.Muted);
            Text(row, "Reset restores exact classic angles, without bin rounding.", 9, bottom + 108, rw - 18, 10, OzUi.Muted);
            y += bottom + 146;
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
            var d = ZBehaviorSchema.Def(key);
            return d.Tip + "\nDefaults: OpenZ " + Reference(d, false) + " | Zod " + Reference(d, true) + "." + (d.Unit.Length == 0 ? "" : "\nUnit: " + d.Unit + ".") +
                (d.Label.Contains("0 = unlimited") ? " Zero removes this limit." : d.Label.Contains("0 = instant") ? " Zero applies instantly." : "");
        }
        void Save(bool copy) { FinishFields(); OzBehaviorLibrary.Save(P, presetName, copy); OzBehaviorLibrary.SaveDraft(P); dirty = false; status = "Saved " + P.name + ". Select it under DYNAMICS in the lobby."; }
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
