// 1.2 item 6, View/OzUnitEditor.cs: module P3's root component (added by OzUnitEditor.Awake, built once the HUD has its Root). It opens
// the stats sets (the OpenZ set is computed in Awake, before any set is applied), builds the STATS box at the right (DESIGN_SPEC E050-E058:
// header with the STATS | BALANCE tabs, TABLE and hide, the set switch OpenZ / ZodEngine / Z-Wiki / Custom, the body that shows the unit
// list, the inspector, the flag / obstacle panel or the balance view, the rail while hidden), the TABLE sheet, the unit card, the hover
// card and the side tooltips, keeps them in step with the field (2-4 refreshes a second, never per-frame searches) and tells the
// camera how much of the right side the box covers (OzUeInsets).
using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>Module P3's root component.</summary>
    public sealed class OzUeStatsRoot : MonoBehaviour
    {
        internal static OzUeStatsRoot I { get; private set; }
        const string Log = "[OpenZ UnitEditor] ";

        OzUnitEditor ed;
        OpenZHud hud;
        bool built;
        RectTransform panelsRoot, sheetsRoot, popsRoot, tipsRoot;
        internal UeBoxShell Shell;
        internal RectTransform PopsRoot => popsRoot;
        internal OzUeTable Table;
        internal OzUeCard Card;
        internal OzUeHoverCard Hover;
        Vector2 builtCanvas; UeBoxLayout builtLayout;
        bool dirtyValues = true, dirtyCounts = true, dirtyView = true, dirtyArt = true;
        float nextRefresh, nextLive;
        /// <summary>Counts for the checks and the proof.</summary>
        public static int Builds, Rebuilds, Resizes, Refreshes;

        void Awake()
        {
            I = this;
            ed = GetComponent<OzUnitEditor>();
            if (ed == null) ed = OzUnitEditor.I;
            OzUeStats.Reset();
            OzUeSets.Open(ed != null ? ed.World : null);
            OzUePlayground.Spawned += OnSpawned;
            OzUePlayground.FieldChanged += OnField;
            OzUeSets.Changed += OnSets;
            OzUePick.Changed += OnPick;
            OzUeStats.StateChanged += OnState;
            OzUeControls.Changed += OnControls;
            OzUnitEditor.PanelsHiddenChanged += OnHidden;
            OzUeInsets.Changed += OnInsets;
        }

        void OnDestroy()
        {
            OzUePlayground.Spawned -= OnSpawned;
            OzUePlayground.FieldChanged -= OnField;
            OzUeSets.Changed -= OnSets;
            OzUePick.Changed -= OnPick;
            OzUeStats.StateChanged -= OnState;
            OzUeControls.Changed -= OnControls;
            OzUnitEditor.PanelsHiddenChanged -= OnHidden;
            OzUeInsets.Changed -= OnInsets;
            OzUeNumberBox.Shutdown();
            UeDrag.Cancel();
            if (Table != null) Table.Dispose();
            OzUeSets.Close();
            OzUeStats.Reset();
            OzUeSideTip.Clear();
            OzUeGfx.Release();
            OzUeInsets.Set(UeSide.Right, "stats", 0f);
            if (I == this) I = null;
        }

        void OnSpawned(ZObject o) { OzUeSets.OnSpawned(o); dirtyCounts = true; }
        void OnField() { dirtyCounts = true; }
        void OnSets() { dirtyValues = true; }
        void OnState() { dirtyView = true; }
        void OnControls() { dirtyArt = true; }
        void OnHidden(bool hidden) { dirtyView = true; }
        void OnInsets() { dirtyView = true; }

        void OnPick()
        {
            // a type inspected from a dock tile / a row opens a hidden box (DESIGN_SPEC 5.3); a field pick on the Balance tab keeps the tab
            if (OzUePick.Kind == UeInspect.Type && !OzUeStats.BoxOpen) OzUeStats.SetBox(true);
            dirtyView = true;
        }

        /// <summary>Builds the box, the table, the card, the hover card and the tooltips on the editor's layers (once the HUD exists).</summary>
        public void Build(OzUnitEditor ed)
        {
            if (built) return;
            if (ed != null) this.ed = ed;
            hud = OpenZHud.I;
            if (hud == null || this.ed == null) { Debug.LogError(Log + "stats: no HUD to build on"); return; }
            panelsRoot = Hud.Node("OzUeStatsPanels", OzUnitEditor.Layer(UeLayer.Panels)); panelsRoot.Fill();
            sheetsRoot = Hud.Node("OzUeStatsSheets", OzUnitEditor.Layer(UeLayer.Sheets)); sheetsRoot.Fill();
            popsRoot = Hud.Node("OzUeStatsPopovers", OzUnitEditor.Layer(UeLayer.Popovers)); popsRoot.Fill();
            tipsRoot = Hud.Node("OzUeStatsTips", OzUnitEditor.Layer(UeLayer.Tips)); tipsRoot.Fill();
            OzUeSideTip.Build(tipsRoot);
            BuildPanels();
            Table = new OzUeTable(); Table.Build(sheetsRoot, hud);
            Card = new OzUeCard(); Card.Build(popsRoot, hud);
            Hover = new OzUeHoverCard(); Hover.Build(panelsRoot);
            OzUeKeys.OnEsc(OzUeKeys.EscCard, () => { if (!OzUeStats.CardOpen) return false; OzUeStats.CloseCard(); return true; });
            OzUeKeys.OnEsc(OzUeKeys.EscTable, () => { if (!OzUeStats.TableOpen) return false; OzUeStats.SetTable(false); OzSfx.Ui("ui_click"); return true; });
            OzUeKeys.OnEsc(OzUeKeys.EscPopover, () => Shell != null && Shell.CloseDropdown());
            OzUeKeys.OnEsc(OzUeKeys.EscBalance, () =>
            {
                if (!OzUeStats.BalanceOpen || !OzUeStats.BoxOpen || OzUnitEditor.PanelsHidden) return false;
                OzUeStats.SetTab(OzUeStats.Tab.Stats); return true;
            });
            OzUeKeys.OnEsc(OzUeKeys.EscInspector, () =>
            {
                var k = OzUePick.Kind;
                if (OzUeStats.BalanceOpen || k == UeInspect.None || k == UeInspect.Flag || k == UeInspect.Obstacle || k == UeInspect.Crate || k == UeInspect.Platform) return false;
                OzUePick.ClearInspect(); return true;
            });
            built = true;
            Builds++;
            dirtyView = dirtyValues = dirtyCounts = dirtyArt = true;
            Sync();
            Debug.Log(Log + "stats box built at " + builtCanvas.x.ToString("0") + " x " + builtCanvas.y.ToString("0") + " u (box " + builtLayout.w.ToString("0.#") + " x " + builtLayout.h.ToString("0.#") + (builtLayout.compact ? ", compact rows" : "") + ")");
        }

        UeBoxLayout CurrentLayout() => UeBoxLayout.For(OzUnitEditor.CanvasSize, OzUeInsets.Get(UeSide.Top), OzUeInsets.Get(UeSide.Bottom));

        void BuildPanels()
        {
            builtCanvas = OzUnitEditor.CanvasSize;
            builtLayout = CurrentLayout();
            if (Shell != null) Shell.Destroy();
            OzUeSideTip.Prune();
            Shell = new UeBoxShell();
            Shell.Build(panelsRoot, hud, builtLayout);
            OzUeSideTip.Prune();
        }

        static bool SameLayout(UeBoxLayout a, UeBoxLayout b) =>
            Mathf.Abs(a.w - b.w) < 0.5f && Mathf.Abs(a.h - b.h) < 0.5f && Mathf.Abs(a.x - b.x) < 0.5f && Mathf.Abs(a.y - b.y) < 0.5f && a.compact == b.compact;

        internal bool Step(int dir) => Shell != null && Shell.StepBalance(dir);

        void Update()
        {
            OzUeSets.TickSave();
            if (!built) return;
            var L = CurrentLayout();
            if (!SameLayout(L, builtLayout))
            {
                if (Shell != null && Shell.CanResize(L))
                {
                    Shell.Resize(L); builtLayout = L; Resizes++;                                // only the height (the dock opened or closed): in place, the scroll and the inputs stay
                }
                else
                {
                    OzUeNumberBox.Blur(true);
                    BuildPanels(); Rebuilds++;
                    dirtyView = dirtyValues = dirtyCounts = dirtyArt = true;
                }
            }
            if (Table != null && Vector2.Distance(OzUnitEditor.CanvasSize, Table.BuiltSize) > 0.5f)
            {
                OzUeNumberBox.Blur(true);
                Table.Rebuild(); Rebuilds++;
                dirtyView = dirtyValues = dirtyCounts = dirtyArt = true;
            }
            if (dirtyView) Sync();
            OzUeNumberBox.Tick();
            UeDrag.Tick();
            float now = OzMotion.Now;
            if ((dirtyValues || dirtyCounts || dirtyArt) && now >= nextRefresh)
            {
                nextRefresh = now + 0.25f;                                                     // at most 4 refreshes a second
                Shell.Refresh(dirtyValues, dirtyCounts, dirtyArt);
                if (Table != null) Table.Refresh(dirtyValues, dirtyCounts, dirtyArt);
                if (Card != null) Card.Refresh();
                dirtyValues = dirtyCounts = dirtyArt = false;
                Refreshes++;
            }
            if (now >= nextLive) { nextLive = now + 0.4f; Shell.TickLive(); }   // the live HP line, 2.5 times a second
            Shell.Tick();
            if (Table != null) Table.Tick();
            if (Card != null) Card.Tick();
            if (Hover != null) Hover.Tick(builtLayout);
            OzUeSideTip.Tick(OzUePlace.Busy || OzUeHooks.AnyModal || OzUnitEditor.PanelsHidden);
        }

        /// <summary>What shows: the box or its rail, the tab, the body's view, the table, the card; the right inset.</summary>
        void Sync()
        {
            dirtyView = false;
            bool hidden = OzUnitEditor.PanelsHidden;
            if (panelsRoot) panelsRoot.gameObject.SetActiveFast(!hidden);
            if (sheetsRoot) sheetsRoot.gameObject.SetActiveFast(!hidden);
            if (popsRoot) popsRoot.gameObject.SetActiveFast(!hidden);
            if (Shell != null) Shell.Sync();
            if (Table != null) Table.Sync();
            if (Card != null) Card.Sync();
            float inset = hidden ? 0f : OzUeStats.BoxOpen ? UeBoxLayout.Gap + builtLayout.w : UeBoxLayout.Gap + UeBoxLayout.RailW;
            OzUeInsets.Set(UeSide.Right, "stats", inset);
            dirtyValues = dirtyCounts = dirtyArt = true;                                     // a view shown again refreshes everything once
        }

        // ------------------------------------------------------------------ checks (edit mode: menu 2 Counted Check; Play: the proof)
        /// <summary>The module's counted checks; returns the number of FAIL lines written to the log (0 = all PASS).</summary>
        public static int SelfCheck(StringBuilder log) => UeStatsChecks.Run(log);
    }

    // ==================================================================== the box shell (E050-E058, E060 picked strip)
    internal sealed class UeBoxShell
    {
        /// <summary>Priorities where the box's parts overlap: rows of a list, the badges and inputs in a row, the fixed parts (heads, feet,
        /// controls) that a row scrolled half out of its list lies under.</summary>
        public const int PRow = 1, PIn = 2, PFix = 3;
        public UeBoxLayout L;
        public RectTransform Box, Rail, Body;
        OpenZHud hud;
        UeSeg tabs, sets; OzUeScope scope;
        HudButton tableBtn, hideBtn, railHot, unpick;
        Image[] railEdge;
        RectTransform strip; OzText stripBlue, stripRed, stripPicked, stripHint;
        internal OzUeStatsList List; internal OzUeInspector Inspector; internal OzUeObjectPanel Objects; internal OzUeBalance Balance;
        string shownKey;
        int pickedBlue = -1, pickedRed = -1;

        public void Build(RectTransform parent, OpenZHud hud, UeBoxLayout L)
        {
            this.hud = hud; this.L = L;
            // ---- E050 the box: an "md" plate with its olive header band (E051)
            Box = OzUeGfx.Node(parent, "OzUeStatsBox", L.x, L.y, L.w, L.h);
            Hud.Plate(Box, "md", L.header);
            hud.AddBlocker(Box);
            float by = (L.header - 28f) * 0.5f;
            // ---- E052 tabs STATS | BALANCE (12 u caps 0.10, padding 0 12)
            var probe = Hud.Label(Box, "Probe", "", 12, OzUi.Ink, 0.10f, OzStyle.Plain);
            float t1 = Mathf.Max(68.5f, probe.WidthAt("STATS") + 24f), t2 = Mathf.Max(88.8f, probe.WidthAt("BALANCE") + 24f);
            UnityEngine.Object.Destroy(probe.gameObject);
            tabs = UeSeg.Make(Box, "Tabs", 12f, by, 28f, new[] { "STATS", "BALANCE" }, new[] { t1, t2 }, 12f, 0.10f, PFix);
            tabs.Hots[0].OnClick = c => OzUeStats.SetTab(OzUeStats.Tab.Stats);
            tabs.Hots[1].OnClick = c => OzUeStats.SetTab(OzUeStats.Tab.Balance);
            OzUeSideTip.Add(tabs.Hots[0].Rt, "Stats: all units, or the picked unit", OzUeSideTip.Place.Side, PFix, Box);
            OzUeSideTip.Add(tabs.Hots[1].Rt, "Balance: one stat for every unit as bars of the four sets (B)", OzUeSideTip.Place.Side, PFix, Box);
            // ---- E053 TABLE, E054 hide
            hideBtn = OzUeGfx.Button(Box, "Hide", ">", L.w - 10f - 28f, by, 28f, 28f, HudButtonStyle.Normal, PFix, 14f, 0f);
            hideBtn.OnClick = c => OzUeStats.SetBox(false);
            OzUeSideTip.Add(hideBtn.Rt, "Hide the stats box (I)", OzUeSideTip.Place.Side, PFix, Box);
            tableBtn = OzUeGfx.Button(Box, "Table", "TABLE", L.w - 10f - 28f - 8f - 62f, by, 62f, 28f, HudButtonStyle.Normal, PFix);
            tableBtn.OnClick = c => OzUeStats.SetTable(!OzUeStats.TableOpen);
            OzUeSideTip.Add(tableBtn.Rt, "Table: every unit, every stat, over the field (T)", OzUeSideTip.Place.Side, PFix, Box);
            // ---- E055 the set switch: four equal cells, names in mixed case
            float sw = L.w - 22f, cw = sw / 4f;
            sets = UeSeg.Make(Box, "Sets", 11f, L.setTop, OzUePlan.CellH(L.compact), new[] { "OpenZ", "ZodEngine", "Z-Wiki", "Custom" }, new[] { cw, cw, cw, cw }, 12f, 0.03f, PFix);   // 24 u (compact 20): the line under it says which stats are used
            for (int i = 0; i < 4; i++)
            {
                var s = OzUeSets.All[i];
                sets.Hots[i].OnClick = c => OzUeSets.Use(s);
                OzUeSideTip.Add(sets.Hots[i].Rt, OzUeSets.LongName(s) + "|The field here uses this set, every unit, both teams|Levels: OpenZ, or your saved Custom stats if you pick Custom on the level screen", OzUeSideTip.Place.Side, PFix, Box);
            }
            scope = OzUeScope.Build(Box, L, PFix);                                                          // "In use here: Custom. Levels: OpenZ, or Custom on the level screen." (UI/OzUeScope.cs)
            // ---- E056 the body: the views swap in place
            Body = OzUeGfx.Node(Box, "Body", 0, L.bodyTop, L.w, L.h - L.bodyTop);
            BuildStrip();
            List = new OzUeStatsList(); List.Build(this);
            Inspector = new OzUeInspector(); Inspector.Build(this);
            Objects = new OzUeObjectPanel(); Objects.Build(this);
            Balance = new OzUeBalance(); Balance.Build(this);
            // ---- E058 the rail while the box is hidden
            var rr = L.Rail;
            Rail = OzUeGfx.Node(parent, "OzUeStatsRail", rr.x, rr.y, rr.width, rr.height);
            Hud.Plate(Rail, "sm");
            hud.AddBlocker(Rail);
            railHot = OzUeGfx.Hot(Rail, "Hot", 0, 0, rr.width, rr.height, 1);
            railHot.OnClick = c => OzUeStats.SetBox(true);
            railEdge = OzUeGfx.Frame(Rail, OzUi.A(OzUi.AmberLo, 0f));
            var lr = Hud.Node("Word", Rail).Anchor(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(rr.width * 0.5f, -(13f + 24.75f)), new Vector2(50f, 15f));
            lr.localEulerAngles = new Vector3(0, 0, 90f);                                     // reads bottom to top, turned round its centre
            Hud.Label(lr, "Text", "STATS", 12, OzUi.Ink2, 0.20f, OzStyle.Plain).FillParent(OzH.C);
            Hud.Keycap(Rail, "Key", "I", rr.width * 0.5f + 10f, 72.5f, 20f);
            OzUeSideTip.Add(Rail, "Show stats (I)", OzUeSideTip.Place.Side, 1, Rail);
            shownKey = null;
        }

        void BuildStrip()
        {
            strip = OzUeGfx.Node(Body, "Picked", 0, 0, L.w, L.strip);
            OzUeGfx.Rect(strip, "Fill", OzUi.A(OzUi.Amber, 0.06f), 0, 0, L.w, L.strip);
            OzUeGfx.HLine(strip, "Top", OzUeGfx.SepLine, 0, 0, L.w);
            float mid = L.strip * 0.5f;
            stripBlue = OzUeGfx.Text(strip, "Blue", "", 12, OzUeUi.TeamText(Team.Blue), 0.10f, 10f, mid, OzH.L, 80f);
            stripRed = OzUeGfx.Text(strip, "Red", "", 12, OzUeUi.TeamText(Team.Red), 0.10f, 60f, mid, OzH.L, 80f);
            stripPicked = OzUeGfx.Text(strip, "Picked", "picked", 12, OzUi.Ink2, 0f, 120f, mid, OzH.L, 60f);
            stripHint = OzUeGfx.Text(strip, "Hint", "", 12, OzUi.Muted, 0f, 170f, mid, OzH.L, 150f);
            OzUeGfx.Swallow(strip, "Swallow", 0, 0, L.w, L.strip, PFix);
            unpick = OzUeGfx.Button(strip, "Unpick", "UNPICK", L.w - 10f - 70f, (L.strip - 28f) * 0.5f, 70f, 28f, HudButtonStyle.Normal, PFix);
            float uw = Mathf.Max(70f, unpick.Label.WidthAt("UNPICK") + 16f);                    // padding 0 8
            unpick.Rt.TL(L.w - 10f - uw, (L.strip - 28f) * 0.5f, uw, 28f);
            unpick.OnClick = c => OzUePick.Unpick();
            OzUeSideTip.Add(unpick.Rt, "Unpick all (Esc)", OzUeSideTip.Place.Side, PFix, Box);
            OzUeSideTip.Add(strip, "Orders for the picked units|Right-click the ground: move there (round obstacles)|Right-click an enemy: attack it|Right-click the flag: take it|" +
                            "Right-click an empty vehicle or gun: a robot climbs in|Ctrl + right-click: forced attack (any unit, empty hardware; the obstacle: explosives only)|Esc: unpick", OzUeSideTip.Place.Side, PFix, Box);
            strip.gameObject.SetActive(false);
        }

        /// <summary>The picked strip shows while units are picked (both teams), over the list and the inspector.</summary>
        public bool StripShown => OzUeStats.CurrentTab == OzUeStats.Tab.Stats && OzUePick.Picked.Count > 0;
        public float StripH => StripShown ? L.strip : 0f;

        void RefreshStrip()
        {
            bool on = StripShown;
            strip.gameObject.SetActiveFast(on);
            if (!on) return;
            int nb = OzUePick.PickedCount(Team.Blue), nr = OzUePick.PickedCount(Team.Red);
            if (nb == pickedBlue && nr == pickedRed) return;
            pickedBlue = nb; pickedRed = nr;
            float x = 10f, mid = L.strip * 0.5f;
            stripBlue.Set(nb > 0 ? nb + " " + OzUeUi.TeamWord(Team.Blue) : ""); stripBlue.color = OzUeUi.TeamText(Team.Blue);
            stripBlue.At(x, mid, OzH.L, true, 90f); if (nb > 0) x += stripBlue.WidthAt(stripBlue.text) + 8f;
            stripRed.Set(nr > 0 ? nr + " " + OzUeUi.TeamWord(Team.Red) : ""); stripRed.color = OzUeUi.TeamText(Team.Red);
            stripRed.At(x, mid, OzH.L, true, 90f); if (nr > 0) x += stripRed.WidthAt(stripRed.text) + 8f;
            stripPicked.At(x, mid, OzH.L, true, 60f); x += stripPicked.WidthAt("picked") + 8f;
            float room = unpick.Rt.anchoredPosition.x - 8f - x;
            string lng = "Right-click: move, attack, take", sh = "Right-click: orders";
            stripHint.Set(!L.narrowList && stripHint.WidthAt(lng) <= room ? lng : sh);
            stripHint.At(x, mid, OzH.L, true, Mathf.Max(20f, room));
        }

        /// <summary>Only the box's height changes (the dock opened or closed while the box is under its 860 u cap): same place, width,
        /// rows and canvas, so the box follows in place (DESIGN_SPEC 2.2: height min(CH - 40 - 24 - insetB, 860), as the prototype's box).</summary>
        public bool CanResize(UeBoxLayout n) =>
            Mathf.Abs(n.w - L.w) < 0.5f && Mathf.Abs(n.x - L.x) < 0.5f && Mathf.Abs(n.y - L.y) < 0.5f && n.compact == L.compact &&
            n.narrowWindow == L.narrowWindow && (n.canvas - L.canvas).sqrMagnitude < 0.25f;

        /// <summary>The box, its body and every view take the new height; the view showing is laid out again at once.</summary>
        public void Resize(UeBoxLayout n)
        {
            L = n;
            Box.TL(L.x, L.y, L.w, L.h);
            Body.TL(0, L.bodyTop, L.w, L.h - L.bodyTop);
            List.Resize(n); Inspector.Resize(n); Objects.Resize(n); Balance.Resize(n);
            shownKey = null;
            ShowView();
        }

        public void Destroy()
        {
            if (Box) UnityEngine.Object.Destroy(Box.gameObject);
            if (Rail) UnityEngine.Object.Destroy(Rail.gameObject);
            CloseDropdown();
            if (Balance != null) Balance.DestroyPopover();
        }

        /// <summary>Box or rail, the tab cells, the TABLE button and the view of the body.</summary>
        public void Sync()
        {
            bool open = OzUeStats.BoxOpen;
            Box.gameObject.SetActiveFast(open);
            Rail.gameObject.SetActiveFast(!open);
            if (!open) CloseDropdown();
            tabs.Paint((int)OzUeStats.CurrentTab, OzUi.AmberHi, OzUi.Amber, OzUeGfx.DarkInk, i => OzUi.Muted);
            tableBtn.Selected = OzUeStats.TableOpen; tableBtn.Refresh();
            PaintSets();
            pickedBlue = pickedRed = -1;
            ShowView();
        }

        void PaintSets()
        {
            var u = OzUeSets.InUse;
            sets.Paint((int)u, OzUeSets.Light(u), OzUeSets.Colour(u), OzUeGfx.DarkInk, i => OzUi.A(OzUeSets.Colour(OzUeSets.All[i]), 0.85f));
            scope?.Paint(u);
        }

        enum View { List, Inspector, Objects, Balance }

        View Which()
        {
            if (OzUeStats.CurrentTab == OzUeStats.Tab.Balance) return View.Balance;
            switch (OzUePick.Kind)
            {
                case UeInspect.Type: case UeInspect.Unit: case UeInspect.EmptyHardware: return OzUePick.InspectedId != null && OzUeLiveStats.Known(OzUePick.InspectedId) ? View.Inspector : View.List;
                case UeInspect.Flag: case UeInspect.Obstacle: case UeInspect.Crate: case UeInspect.Platform: return View.Objects;
            }
            return View.List;
        }

        void ShowView()
        {
            var v = Which();
            RefreshStrip();
            float top = StripH;
            List.Show(v == View.List, top);
            Inspector.Show(v == View.Inspector, top);
            Objects.Show(v == View.Objects, top);
            Balance.Show(v == View.Balance);
            string key = v + "|" + OzUePick.Kind + "|" + OzUePick.InspectedId + "|" + top;
            if (key != shownKey) { shownKey = key; Refresh(true, true, true); }
        }

        public void Refresh(bool values, bool counts, bool art)
        {
            if (!Box || !Box.gameObject.activeInHierarchy) return;
            if (values) PaintSets();
            RefreshStrip();
            if (List.Shown) List.Refresh(values, counts, art);
            if (Inspector.Shown) Inspector.Refresh(values, counts, art);
            if (Objects.Shown) Objects.Refresh();
            if (Balance.Shown) Balance.Refresh(values, counts, art);
        }

        /// <summary>Every frame: hovers, scrolls, drags; the view follows a changed pick count at once.</summary>
        public void Tick()
        {
            if (!Box) return;
            if (Box.gameObject.activeInHierarchy)
            {
                tabs.Tick(); sets.Tick();
                bool strip = StripShown;
                if (strip != strip_) { strip_ = strip; ShowView(); }
                else RefreshStrip();
                if (List.Shown) List.Tick();
                if (Inspector.Shown) Inspector.Tick();
                if (Objects.Shown) Objects.Tick();
                if (Balance.Shown) Balance.Tick();
            }
            else if (Rail && Rail.gameObject.activeInHierarchy)
            {
                var c = railHot.Hovered ? (Color)OzUi.AmberLo : OzUi.A(OzUi.AmberLo, 0f);
                foreach (var e in railEdge) if (e.color != c) e.color = c;
            }
        }
        bool strip_;

        public void TickLive()
        {
            if (Box && Box.gameObject.activeInHierarchy)
            {
                if (Inspector.Shown) Inspector.TickLive();
                if (Objects.Shown) Objects.Refresh();
            }
        }

        public bool StepBalance(int dir) => Balance != null && Balance.Step(dir);
        public bool CloseDropdown() => Balance != null && Balance.CloseList();

        /// <summary>The unit's picture team: the picked unit's, else the dock's (neutral: the grey look).</summary>
        public static Team DockTeam => OzUeControls.Team;

        /// <summary>A row / tile press on a picture starts a drag when it travels (UeDrag).</summary>
        public static void PressPicture(HudButton row, RectTransform pic, string id, Team team)
        {
            var m = Mouse.current;
            if (m == null || !m.leftButton.wasPressedThisFrame || row == null || !row.Pressed) return;
            if (pic != null && !OzUeGfx.Over(pic)) return;
            UeDrag.Arm(id, team);
        }
    }
}
