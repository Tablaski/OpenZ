using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (UI/OzUeControlsRoot.cs; owner 6j "left part should be red / blue / neutral ... keep flag on right side always as
    /// extra", 6k "should be one line and tabs not two lines"; spec E040-E049): the dock, ONE row at every size: the team switch over
    /// x1 / x3 / x5 / x10 / x20, the tabs ROBOTS | VEHICLES | GUNS | SPECIAL, the tab's tiles (the area keeps the width of the widest tab, so the dock
    /// never moves or resizes when the tab changes), EXTRAS (the neutral FLAG and the holo OBSTACLE), PIN. A tile: drag it onto the field
    /// (P4's OzUePlace.BeginDrag), click it to place by clicking the field (OzUePlace.ToggleArm), right-click it to see its stats
    /// (OzUePick.InspectType), its "i" corner opens the unit card (OzUeStats.OpenCard). Under NEUTRAL the robots, the heroes and the
    /// shuttle are OFF (robots are never neutral in Z). PIN off: the dock slides away 1.2 s after the mouse leaves it and comes back at
    /// the bottom edge or its rail. Closed: the rail "UNITS P".
    /// </summary>
    public sealed class OzUeDock
    {
        public const string KeyPin = "oz_ue_pin";
        public const float WakeZone = 36f, HideDelay = 1.2f, Slide = 0.18f, DragUnits = 6f;
        /// <summary>Checks: drags started from a tile, tiles armed, refusals (robots under NEUTRAL), cards opened from a tile.</summary>
        public static int Drags, Arms, Refusals, Cards;

        sealed class Tile
        {
            public string id; public int tab; public bool extra;
            public RectTransform root, art; public HudButton b; public CanvasGroup cg;
            public Image well, backing; public Image[] edge, hoverEdge, armEdge; public RawImage[] dash = new RawImage[4];
            public RawImage pic, glow, corner;
            public HudButton info; public RawImage infoDisc, infoRing; public OzText infoI;
            public RectTransform squad; public Image squadFill; public OzText squadText; public HudButton squadHot;
            public OzText label;
            public float cellW;
            public bool off, offered = true, dragFired, refusedThisPress; public Team team; public string why;
            public bool rescued;                                                                               // 1.2 item 70: a press the HUD saw beside the tile or after a long frame (Rescue)
            public int squadShown = -1; public bool shownHover, shownArmed, shownOff, shownPressed, first = true, wasHover;
            public Image cool; public OzText coolText; public int coolShown = -1; public float coolH = -1f, readyGlowUntil;   // 1.3 item 2: the NUKE tile's cooldown (curtain + seconds)
        }

        OzUnitEditor ed; OpenZHud hud;
        public RectTransform Plate, Rail;
        OzUeSeg count, tabs;
        OzUeTeamSeg team;                                                                                   // 1.5 kit ue_bottom_left_r10 (FAILS A36): the bigger, team-tinted switch (UI/OzUeTeamSeg.cs)
        Image sep; OzText caption;
        HudButton pin; HudButton railBtn; OzText railLabel; OzUeKeycap railKey;
        CanvasGroup plateCg;
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<Tile>[] byTab = { new List<Tile>(), new List<Tile>(), new List<Tile>(), new List<Tile>() };
        Tile flag, obstacle, box, grenades, nuke, platMid, platHigh;                                      // 1.5 ue_boxes: + BOX
        bool pinned = true, wasInside; float leftAt = -1f;
        float slideFrom, slideTo = 0f, slideAt = -10f, slideShown;
        OzUeBottomLayout.Dock lay;
        bool dirtyLook = true; float nextSquad;
        /// <summary>1.2 item 70: a press this far (u) outside a tile's box (on its drawn hover edge) still takes the tile. Rescues =
        /// presses the dock took beside a tile or after a long frame (checks).</summary>
        public const float EdgeGrace = 2f;
        public static int Rescues;
        Vector2 pressFrom; bool rescueHeld; readonly object rescueHolder = new object();

        public bool Pinned => pinned;
        /// <summary>1.5 kit ue_bottom_left_r10 (A36, checks): the team switch.</summary>
        public OzUeTeamSeg TeamSeg => team;
        /// <summary>The plate's rect on the canvas as laid out (for the counted check: one row, the same rect in every tab).</summary>
        public UeBox PlateBox => lay != null ? lay.plate : default;

        public void Build(OzUnitEditor editor, Transform panels)
        {
            ed = editor; hud = editor.Hud;
            pinned = PlayerPrefs.GetInt(KeyPin, 1) == 1;
            Drags = Arms = Refusals = Cards = Rescues = 0;
            OzUePressAt.Hook();                                                                                // 1.2 item 70: where the left button really went down (UI/OzUePressAt.cs)

            Plate = Hud.Node("UE_Dock", panels);
            Hud.Plate(Plate, "sm");
            plateCg = Plate.gameObject.AddComponent<CanvasGroup>();
            hud.AddBlocker(Plate);

            var tw = TeamWords();
            team = new OzUeTeamSeg(Plate, "Team", tw, OzUeBottomLayout.TeamBigText, OzUeBottomLayout.TeamTrack);   // 1.5 kit ue_bottom_left_r10 (A36)
            team.OnPick = k => OzUeControls.SetTeam(k == 0 ? Team.Red : k == 1 ? Team.Blue : Team.Neutral);
            count = new OzUeSeg(Plate, "Count", OzUeBottomLayout.CountWords, OzUeBottomLayout.TeamText, OzUeBottomLayout.TeamTrack);
            count.OnPick = k => OzUeControls.SetCount(OzUeControls.CountSteps[Mathf.Clamp(k, 0, OzUeControls.CountSteps.Length - 1)]);
            count.SetTip(0, OzUeW.Tip("Drop 1 squad or vehicle per click"));
            count.SetTip(1, OzUeW.Tip("Drop 3 squads or vehicles per click"));
            count.SetTip(2, OzUeW.Tip("Drop 5 squads or vehicles per click"));
            count.SetTip(3, OzUeW.Tip("Drop 10 squads or vehicles per click"));
            count.SetTip(4, OzUeW.Tip("Drop 20 squads or vehicles per click"));
            tabs = new OzUeSeg(Plate, "Tabs", OzUeBottomLayout.TabWords, OzUeBottomLayout.TabText, OzUeBottomLayout.TabTrack);
            tabs.OnPick = k => OzUeControls.SetTab(OzUeControls.Tabs[k]);
            tabs.SetTip(0, OzUeW.Tip("Robots", null, "Robot squads: grunt to electro"));
            tabs.SetTip(1, OzUeW.Tip("Vehicles", null, "Jeep to crane"));
            tabs.SetTip(2, OzUeW.Tip("Guns", null, "Gatling, gun, howitzer, missile"));
            tabs.SetTip(3, OzUeW.Tip("Special", null, "The heroes: Alan and Brad on the " + OzUeUi.TeamWordSentence(Team.Red).ToLowerInvariant() + " side, EvilZod on the " + OzUeUi.TeamWordSentence(Team.Blue).ToLowerInvariant() + " side; the shuttle for both", "Six test explosions to drag onto the field"));   // rows 79 / 80

            var lists = new[] { OzUePlayground.Robots, OzUePlayground.Vehicles, OzUePlayground.Guns, OzUePlayground.Special };
            for (int k = 0; k < 4; k++) foreach (var id in lists[k]) byTab[k].Add(MakeTile(id, k, false));
            foreach (var id in OzUeBlasts.Ids) byTab[3].Add(MakeTile(id, 3, true));
            sep = Hud.Img(Plate, "ExtrasLine", OzUi.A(OzUi.Khaki, 0.5f));
            caption = Hud.Label(Plate, "ExtrasCaption", "EXTRAS", 11f, OzUi.Khaki, 0.12f, OzStyle.Plain).FillParent(OzH.C);   // centred in its box
            caption.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);                             // reads bottom to top
            flag = MakeTile(OzUePlayground.Flag, -1, true);
            obstacle = MakeTile(OzUePlayground.Obstacle, -1, true);
            box = MakeTile(OzUePlayground.Box, -1, true);
            grenades = MakeTile(OzUePlayground.Grenades, -1, true);
            nuke = MakeTile(OzUePlayground.Nuke, -1, true);                                               // 1.3 item 2: the NUKE extra
            MakeCooldown(nuke);
            platMid = MakeTile(OzUePlayground.PlatformMid, -1, true);                                     // 1.3: the two ramp platforms
            platHigh = MakeTile(OzUePlayground.PlatformHigh, -1, true);

            pin = HudButton.Make(Plate, "Pin", OzUeBottomLayout.PinWord, new Vector2(41f, OzUeBottomLayout.PinH), HudButtonKind.Small, OzUeBottomLayout.PinText, OzUeBottomLayout.PinTrack, pinned ? HudButtonStyle.Primary : HudButtonStyle.Normal);
            pin.Tooltip = OzUeW.Tip("Pinned: the dock stays.", null, "Off: it hides when the mouse leaves and comes back at the bottom edge.");
            pin.OnClick = c => SetPinned(!pinned);

            Rail = Hud.Node("UE_DockRail", panels);
            Hud.Plate(Rail, "sm");
            hud.AddBlocker(Rail);
            railBtn = OzUeW.Bare(Rail, "Open", 10f, 10f); railBtn.Rt.Fill();
            railBtn.Tooltip = OzUeW.Tip("Show dock", "P");
            railBtn.OnClick = c => OzUeControls.SetDockOpen(true);
            railLabel = Hud.Label(Rail, "Label", OzUeBottomLayout.RailWord, OzUeBottomLayout.RailText, OzUi.Ink2, OzUeBottomLayout.RailTrack, OzStyle.Plain);
            railKey = new OzUeKeycap(Rail, "Key", OzUeBottomLayout.RailKey, 0f);
            Rail.gameObject.SetActive(false);

            slideShown = OzUeControls.DockOpen ? 0f : 1f; slideTo = slideShown;
            OzUeControls.Changed += OnControls;
            OzUeControls.DockChanged += OnDock;
            OzUePlace.Changed += OnPlace;
            OzUeSets.Changed += OnSets;
        }

        public void Destroy()
        {
            OzUePressAt.Unhook();
            if (rescueHeld) { rescueHeld = false; OzUePick.HoldInput(rescueHolder, false); }
            OzUeControls.Changed -= OnControls;
            OzUeControls.DockChanged -= OnDock;
            OzUePlace.Changed -= OnPlace;
            OzUeSets.Changed -= OnSets;
        }

        static string[] TeamWords() => new[] { OzUeUi.TeamWord(Team.Red), OzUeUi.TeamWord(Team.Blue), "NEUTRAL" };

        void OnControls() { dirtyLook = true; }
        void OnPlace() { dirtyLook = true; }
        void OnSets() { nextSquad = 0f; }
        void OnDock()
        {
            slideFrom = slideShown; slideTo = OzUeControls.DockOpen ? 0f : 1f; slideAt = Time.unscaledTime;
            if (!OzUeControls.DockOpen) { leftAt = -1f; wasInside = false; }
        }

        void SetPinned(bool on)
        {
            pinned = on; PlayerPrefs.SetInt(KeyPin, on ? 1 : 0); PlayerPrefs.Save();
            pin.Style = on ? HudButtonStyle.Primary : HudButtonStyle.Normal; pin.Refresh();
            OzUeToast.Show(on ? "DOCK PINNED" : "DOCK HIDES WHEN THE MOUSE LEAVES");
            Debug.Log($"[OpenZ UnitEditor] dock pin {(on ? "on" : "off")}");
            if (on && !OzUeControls.DockOpen) OzUeControls.SetDockOpen(true);
            leftAt = -1f;
        }

        // ------------------------------------------------------------------------------------------------ one tile (E047)
        Tile MakeTile(string id, int tab, bool extra)
        {
            var t = new Tile { id = id, tab = tab, extra = extra };
            t.b = OzUeW.Bare(Plate, "Tile_" + id, 48f, 62f);
            t.root = t.b.Rt;
            t.cg = t.root.gameObject.AddComponent<CanvasGroup>();
            t.art = Hud.Node("Art", t.root);
            t.glow = OzUeW.Raw(t.art, "Glow", null, 0f, 0f, 1f, 1f, OzUi.A(OzUi.Amber, 0.55f)); t.glow.enabled = false;
            t.well = OzUeW.Fill(t.art, "Well", OzUi.A(OzUi.GM950, 0.75f));
            t.edge = Hud.Border(t.art, OzUeArt.WellEdge(Team.Blue));
            for (int k = 0; k < 4; k++) { t.dash[k] = OzUeW.Raw(t.art, "Dash" + k, k < 2 ? OzUeArt.Dash() : OzUeArt.DashV(), 0f, 0f, 1f, 1f, new Color32(150, 155, 143, 140)); t.dash[k].enabled = false; }
            if (extra) { t.backing = Hud.Img(t.art, "Backing", OzUeArt.ExtraWell); }
            t.pic = OzUeW.Raw(t.art, "Picture", null, 0f, 0f, 1f, 1f, Color.white);
            var hov = Hud.Node("Hover", t.art); t.hoverEdge = Hud.Border(hov, OzUi.Amber, 2f); foreach (var e in t.hoverEdge) e.enabled = false;
            var arm = Hud.Node("Armed", t.art); t.armEdge = Hud.Border(arm, OzUi.AmberHi, 2f); foreach (var e in t.armEdge) e.enabled = false;
            t.label = Hud.Label(t.root, "Label", OzUePlayground.ShortName(id), OzUeBottomLayout.TileLabelText, OzUi.A(OzUi.Ink, 0.88f), OzUeBottomLayout.TileLabelTrack, OzStyle.Plain);
            if (!extra)
            {
                bool squadded = OzUePlayground.KindOf(id) == UeKind.Robot || OzUePlayground.KindOf(id) == UeKind.Hero;
                if (OzUePlayground.OpenZOnly(id)) t.corner = OzUeW.Raw(t.art, "OpenZOnly", OzUeArt.Corner(), 0f, 0f, 10f, 10f, OzUi.Amber);
                if (squadded)
                {
                    t.squad = Hud.Node("Squad", t.art);
                    t.squadFill = OzUeW.Fill(t.squad, "Fill", OzUi.A(OzUi.GM950, 0.82f));
                    Hud.Border(t.squad, OzUi.GM400);
                    t.squadText = OzUeW.Centred(t.squad, "N", "", 11f, OzUi.Ink, 0f);
                    t.squadHot = OzUeW.Bare(t.squad, "SquadTip", 15f, 15f); t.squadHot.Rt.Fill(); t.squadHot.Priority = 1;
                    t.squadHot.Tooltip = OzUeW.Tip("Robots per squad");
                    var sb = t.squadHot; var tile = t;
                    sb.OnClick = c => TileClick(tile);
                    sb.OnRightClick = c => TileRight(tile);
                }
                t.info = OzUeW.Bare(t.art, "Info", 16f, 16f); t.info.Priority = 1;
                t.infoDisc = OzUeW.Raw(t.info.Rt, "Disc", OzUeArt.Disc(), 0f, 0f, 16f, 16f, OzUi.A(OzUi.GM950, 0.86f));
                t.infoRing = OzUeW.Raw(t.info.Rt, "Ring", OzUeArt.Ring(), 0f, 0f, 16f, 16f, OzUi.GM400);
                t.infoI = OzUeW.Centred(t.info.Content, "I", "i", 12f, OzUi.Ink, 0f);
                t.info.Tooltip = OzUeW.Tip("Unit card", null, "What it is and all its stats");
                var tt = t;
                t.info.OnClick = c => { if (tt.dragFired) { tt.dragFired = false; return; } Cards++; OzUeStats.OpenCard(tt.id, CurrentTeam(tt)); };   // 1.2 item 70: a press on the "i" that became a drag opens no card
            }
            var self = t;
            t.b.OnClick = c => TileClick(self);
            t.b.OnRightClick = c => TileRight(self);
            tiles.Add(t);
            return t;
        }

        Team CurrentTeam(Tile t) => t.extra ? Team.Neutral : t.id == ZEvilZod.Type ? Team.Blue : OzUeControls.Team;   // DV161: he is always Blue

        void TileClick(Tile t)
        {
            if (t.dragFired) { t.dragFired = false; return; }                                   // that press was a drag (P4 dropped it)
            if (Cooling(t)) { NukeRefuse(); return; }                                           // 1.3 item 2: the NUKE's 30 s still run
            if (t.off || !t.offered) { if (!t.refusedThisPress) Refuse(t); t.refusedThisPress = false; return; }
            Arms++;
            OzUePlace.ToggleArm(t.id, CurrentTeam(t));
        }

        void TileRight(Tile t)
        {
            OzUePick.InspectType(t.id, CurrentTeam(t));
        }

        void Refuse(Tile t)
        {
            Refusals++;
            OzUeToast.Show(!t.offered && !string.IsNullOrEmpty(t.why) ? t.why.ToUpperInvariant() : "ROBOTS ARE NEVER NEUTRAL: PICK RED OR BLUE", true);
            AudioDirector.UI("ui_error");
            OzUePick.InspectType(t.id, CurrentTeam(t));
        }

        // ------------------------------------------------------------------------------------------------ layout
        public void Layout(OzUeBottomLayout.Dock d)
        {
            lay = d;
            Plate.TL(d.plate.x, d.plate.y, d.plate.w, d.plate.h);
            team.Layout(d.teamWell, d.teamCells);
            count.Layout(d.countWell, d.countCells);
            tabs.Layout(d.tabsWell, d.tabCells);
            PlaceTabs(OzUeControls.Team);                                                                     // rows 79 / 80: each tab's tiles from the left, the team button's heroes only
            sep.rectTransform.TL(d.separatorX, d.tileArea.y, 1f, d.tileArea.h);
            // the vertical caption: a box 11 wide and as tall as a tile, the text turned to read bottom to top, centred in it
            var cr = caption.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f); cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(d.caption.h, d.caption.w);
            cr.anchoredPosition = new Vector2(d.caption.x + d.caption.w * 0.5f, -(d.caption.y + d.caption.h * 0.5f));
            PlaceTile(flag, d.flagTile.x, d.flagTile.y, d.flagTile.w, d.tile);
            PlaceTile(obstacle, d.obstacleTile.x, d.obstacleTile.y, d.obstacleTile.w, d.tile);
            PlaceTile(box, d.boxTile.x, d.boxTile.y, d.boxTile.w, d.tile);                                   // 1.5 ue_boxes
            PlaceTile(grenades, d.grenadesTile.x, d.grenadesTile.y, d.grenadesTile.w, d.tile);
            PlaceTile(nuke, d.nukeTile.x, d.nukeTile.y, d.nukeTile.w, d.tile);
            PlaceTile(platMid, d.platMidTile.x, d.platMidTile.y, d.platMidTile.w, d.tile);
            PlaceTile(platHigh, d.platHighTile.x, d.platHighTile.y, d.platHighTile.w, d.tile);
            pin.Rt.TL(d.pin.x, d.pin.y, d.pin.w, d.pin.h);
            Rail.TL(d.rail.x, d.rail.y, d.rail.w, d.rail.h);
            railLabel.At(d.railLabel.x, d.railLabel.h * 0.5f, OzH.L, true, 0f);
            railKey.Rt.TL(d.railKey.x, d.railKey.y, d.railKey.w, d.railKey.h);
            dirtyLook = true;
        }

        // ------------------------------------------------------------------------------------------------ rows 79 / 80: one-side heroes
        Team placedFor; bool placedOnce;
        /// <summary>
        /// one-side hero (EvilZod blue, ALAN &amp; BRAD red: OzUePlayground.ForTeam) is skipped under the other side's button, so the tiles after
        /// it close up (no gap). The area keeps the widest tab's width (the dock never moves). Called by Layout and when the team button
        /// changes; one log line names the SPECIAL tab's tiles when the side changes (the proof in a built exe's log).</summary>
        void PlaceTabs(Team side)
        {
            var d = lay; if (d == null) return;
            var shown = new List<string>();
            for (int k = 0; k < 4; k++)
            {
                float x = d.tileArea.x;
                var list = byTab[k];
                for (int j = 0; j < list.Count; j++)
                {
                    float w = d.tileCells != null && k < d.tileCells.Length && j < d.tileCells[k].Length ? d.tileCells[k][j] : d.tile;
                    PlaceTile(list[j], x, d.tileArea.y, w, d.tile);
                    if (!OzUePlayground.ForTeam(list[j].id, side)) continue;                                  // its place goes to the next tile
                    x += w + d.tileGap;
                    if (k == 3) shown.Add(OzUePlayground.ShortName(list[j].id));
                }
            }
            if (!placedOnce || side != placedFor) Debug.Log("[OpenZ UnitEditor] dock SPECIAL under " + side.ToString().ToUpperInvariant() + ": " + string.Join(", ", shown));
            placedFor = side; placedOnce = true;
        }

        static void PlaceTile(Tile t, float x, float y, float cellW, float T)
        {
            t.cellW = cellW;
            t.root.TL(x, y, cellW, T + OzUeBottomLayout.TileLabelGap + OzUeBottomLayout.TileLabelH);
            t.art.TL((cellW - T) * 0.5f, 0f, T, T);
            t.art.pivot = new Vector2(0.5f, 0.5f); t.art.anchoredPosition += new Vector2(T * 0.5f, -T * 0.5f);   // scale round the middle when pressed
            t.glow.texture = OzUeArt.Glow(T, T, 12f);
            t.glow.rectTransform.TL(-12f, -12f, T + 24f, T + 24f);
            t.pic.rectTransform.TL(3f, 3f, T - 6f, T - 6f);
            if (t.backing) t.backing.rectTransform.TL(3f, 3f, T - 6f, T - 6f);
            t.hoverEdge[0].transform.parent.GetComponent<RectTransform>().TL(-1f, -1f, T + 2f, T + 2f);
            t.armEdge[0].transform.parent.GetComponent<RectTransform>().TL(-1f, -1f, T + 2f, T + 2f);
            // the dashed edge of an OFF tile: four 1 u strips with a 3 on / 3 off pattern
            t.dash[0].rectTransform.TL(0f, 0f, T, 1f); t.dash[1].rectTransform.TL(0f, T - 1f, T, 1f);
            t.dash[2].rectTransform.TL(0f, 0f, 1f, T); t.dash[3].rectTransform.TL(T - 1f, 0f, 1f, T);
            float rep = T / 6f;
            t.dash[0].uvRect = t.dash[1].uvRect = new Rect(0f, 0f, rep, 1f);                     // the pattern along the strip
            t.dash[2].uvRect = t.dash[3].uvRect = new Rect(0f, 0f, 1f, rep);
            if (t.corner) t.corner.rectTransform.TL(T - 11f, 1f, 10f, 10f);
            if (t.info != null) t.info.Rt.TL(3f, 3f, 16f, 16f);
            if (t.cool) { t.coolH = -1f; t.coolShown = -1; }                                                    // 1.3 item 2: the curtain is laid again on its next tick
            t.label.Tracking = OzUeBottomLayout.TileLabelTrack;                                  // centred under the picture; it may reach LabelHang past the cell
            t.label.At(cellW * 0.5f, T + OzUeBottomLayout.TileLabelGap + OzUeBottomLayout.TileLabelH * 0.5f, OzH.C, true, 0f);
            t.squadShown = -1;
            t.first = true;
        }

        // ------------------------------------------------------------------------------------------------ every frame
        public void Tick(bool hidden)
        {
            float now = Time.unscaledTime;
            // the slide (180 ms, unscaled; reduced motion: at once)
            float dur = OzMotion.Dur(Slide);
            float k = dur <= 0f ? 1f : Mathf.Clamp01((now - slideAt) / dur);
            slideShown = Mathf.Lerp(slideFrom, slideTo, k * k * (3f - 2f * k));
            bool open = OzUeControls.DockOpen;
            bool showPlate = !hidden && (open || slideShown < 0.999f);
            Plate.gameObject.SetActiveFast(showPlate);
            Rail.gameObject.SetActiveFast(!hidden && !open);
            if (lay != null && showPlate)
            {
                Plate.anchoredPosition = new Vector2(lay.plate.x, -(lay.plate.y + slideShown * (lay.plate.h + 2f * OzUeBottomLayout.Gap)));
                plateCg.alpha = 1f - slideShown;
                plateCg.blocksRaycasts = open;
            }
            Rescue();                                                                                          // 1.2 item 70: before the tiles read their press (it also lets a held press go)
            OzUeNuke.TickReady();                                                                              // 1.3 item 2: the ready chime, also with the panels hidden
            if (hidden) return;
            TickAutoHide(now);
            team.Tick(); count.Tick(); tabs.Tick();
            if (railBtn != null) railLabel.color = railBtn.Hovered ? (Color)OzUi.Ink : (Color)OzUi.Ink2;
            if (dirtyLook) { dirtyLook = false; ApplyLooks(); }
            if (now >= nextSquad) { nextSquad = now + 0.5f; RefreshSquads(); }
            if (showPlate) foreach (var t in tiles) if (t.root.gameObject.activeSelf) TickTile(t);
        }

        void TickAutoHide(float now)
        {
            if (pinned) { leftAt = -1f; wasInside = OpenZHud.Contains(Plate, Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero); return; }
            var m = Mouse.current; if (m == null) return;
            Vector2 mp = m.position.ReadValue();
            bool dragging = OzUePlace.Dragging;
            if (OzUeControls.DockOpen)
            {   // as the prototype: the 1.2 s start when the mouse LEAVES the dock (a dock woken at the bottom edge stays until then)
                bool inside = OpenZHud.Contains(Plate, mp) || dragging;
                if (inside) { wasInside = true; leftAt = -1f; }
                else if (wasInside) { wasInside = false; leftAt = now; }
                else if (leftAt >= 0f && now - leftAt >= HideDelay) { OzUeControls.SetDockOpen(false); leftAt = -1f; }
            }
            else
            {
                float zonePx = WakeZone * OzUnitEditor.PxPerUnit;
                bool wake = (mp.y >= 0f && mp.y <= zonePx && mp.x >= 0f && mp.x <= Screen.width) || (railBtn != null && railBtn.Hovered);
                if (wake) OzUeControls.SetDockOpen(true);
            }
        }

        void ApplyLooks()
        {
            var t0 = OzUeControls.Team;
            bool neutral = t0 == Team.Neutral;
            // the team switch, words and colours as the player SEES them (YOUR TEAM COLOUR, D16)
            var words = TeamWords();
            for (int k = 0; k < 3; k++) team.SetText(k, words[k]);
            team.SetLook(0, TeamColors.Shown(Team.Red) == Team.Blue ? OzUeSeg.Look.Blue : OzUeSeg.Look.Red);
            team.SetLook(1, TeamColors.Shown(Team.Blue) == Team.Red ? OzUeSeg.Look.Red : OzUeSeg.Look.Blue);
            team.SetLook(2, OzUeSeg.Look.Neutral);
            team.Select(t0 == Team.Red ? 0 : t0 == Team.Blue ? 1 : 2);
            team.SetTip(0, TeamTip(Team.Red));
            team.SetTip(1, TeamTip(Team.Blue));
            team.SetTip(2, OzUeW.Tip("Neutral", "3", "Vehicles and guns drop EMPTY. A robot of either team that touches one climbs in and it joins that team. Robots are never neutral in Z."));
            int c = OzUeControls.Count;
            count.Select(OzUeControls.CountIndex(c));
            int tabIdx = System.Array.IndexOf(OzUeControls.Tabs, OzUeControls.Tab); if (tabIdx < 0) tabIdx = 0;
            tabs.Select(tabIdx);
            tabs.SetDim(0, neutral); tabs.SetDim(3, neutral);
            if (lay != null && (!placedOnce || t0 != placedFor)) PlaceTabs(t0);                                // rows 79 / 80: the team button changed: the SPECIAL tab's tiles close up for that side's heroes
            for (int k = 0; k < 4; k++) foreach (var t in byTab[k]) t.root.gameObject.SetActiveFast(k == tabIdx && OzUePlayground.ForTeam(t.id, t0));   // rows 79 / 80: EvilZod only under BLUE, ALAN & BRAD only under RED
            foreach (var t in tiles) LookTile(t, t0);
        }

        static string TeamTip(Team side)
        {
            string key = TeamColors.Shown(side) == Team.Blue ? "1" : "2";
            return OzUeW.Tip("New units join the " + OzUeUi.TeamWordSentence(side).ToLowerInvariant() + " side", key);
        }

        void LookTile(Tile t, Team team0)
        {
            t.team = t.extra ? Team.Neutral : t.id == ZEvilZod.Type ? Team.Blue : team0;              // DV161: his tile stays Blue under any team button
            bool neutral = t.team == Team.Neutral;
            t.offered = OzUePlayground.Offered(t.id, out t.why);
            t.off = !t.extra && neutral && !OzUePlayground.CanBeNeutral(t.id);
            OzUeArt.Apply(t.pic, t.id, t.extra ? Team.Neutral : t.team);
            var edgeC = neutral ? (t.extra ? (Color)new Color32(214, 212, 196, 179) : OzUeArt.WellEdge(Team.Neutral)) : OzUeArt.WellEdge(t.team);
            foreach (var e in t.edge) e.color = edgeC;
            t.well.color = neutral ? (Color)new Color32(24, 26, 25, 217) : OzUi.A(OzUi.GM950, 0.75f);
            t.label.color = neutral ? (Color)OzUi.Khaki : OzUi.A(OzUi.Ink, 0.88f);
            string name = OzUePlayground.FullName(t.id);
            if (t.id == OzUePlayground.Flag)
                t.b.Tooltip = OzUeW.Tip("Neutral flag (always neutral)", null, "Drag onto the field, or click it and then click the field",
                                        "A robot or vehicle that touches it takes it for its team, and it can be taken back.");
            else if (t.id == OzUePlayground.Grenades)
                t.b.Tooltip = OzUeW.Tip("Grenade crate (always neutral)", null, "Drag onto the field, or click it and then click the field",
                                        "A robot that walks over it picks it up: its squad gets the crate's grenades and the crate is gone",
                                        "Toughs and electrobots never take grenades, nor a squad that still has some", "Shots and blasts can blow it up");
            else if (OzUeBlasts.Is(t.id))                                                                            // 1.5 ue_explosions_6: the six test explosions
                t.b.Tooltip = OzUeW.Tip(OzUeBlasts.TipTitle(t.id), null, "Drag onto the field, or click it and then click the field",
                                        OzUeBlasts.TipLook(t.id), OzUeBlasts.TipDamage(t.id), "Unit editor only: a test of how it looks");
            else if (t.id == OzUePlayground.Nuke)
                t.b.Tooltip = OzUeW.Tip("Nuke strike", null, "Drag onto the field, or click it and then click the field",
                                        "The bomber flies in and drops the nuke on that spot",
                                        "Both teams alike, whoever calls it: everything within " + Mathf.RoundToInt(OzUeNuke.KillRadiusM) + " m is destroyed, everything up to "
                                        + Mathf.RoundToInt(OzUeNuke.DamageRadiusM) + " m is badly damaged",
                                        "One nuke every " + Mathf.RoundToInt(OzUeNuke.CooldownSec) + " s: the tile shows the time left");
            else if (OzUePlayground.IsPlatformId(t.id))
                t.b.Tooltip = OzUeW.Tip(name, null, "Drag onto the field, or click it and then click the field",
                                        "Its ramp faces the view: turn the view to choose the side. " + (t.id == OzUePlayground.PlatformHigh ? "As high as a fort tower gun" : "Half a fort tower's height"),
                                        "Units drive up the ramp; dropped on it they stand on it. A blast stays on its own level",
                                        "Destructible by explosives only (Ctrl + right-click): what stands on it dies with it");
            else if (t.id == OzUePlayground.Box)
                t.b.Tooltip = OzUeW.Tip("Holo box (always neutral)", null, "Drag onto the field, or click it and then click the field",
                                        "Drop it on a box and it goes on top: build towers, walls and castles, up to " + ZWorld.BoxLevels + " high",
                                        "Blocks every shot and every path, like the obstacle. Destructible (10000 HP) by explosives only: Ctrl + right-click it",
                                        "When a box breaks, the boxes above it fall");
            else if (t.id == OzUePlayground.Obstacle)
                t.b.Tooltip = OzUeW.Tip("Holo obstacle (always neutral)", null, "Drag onto the field, or click it and then click the field",
                                        "Blocks every shot and every path: units walk round it", "Destructible (10000 HP) by explosives only: pick Toughs, tanks, mechs, the MLRS or cannons, then Ctrl + right-click it",
                                        "Several can stand side by side, never on units, the flag or each other");
            else if (!t.offered)
                t.b.Tooltip = OzUeW.Tip(name, null, string.IsNullOrEmpty(t.why) ? "Not in play now." : t.why);
            else if (t.off && OzUePlayground.OpenZOnly(t.id))
                t.b.Tooltip = OzUeW.Tip(name, null, "OpenZ only, not in the 1996 Z", "Robots are never neutral in Z: only vehicles and guns can stand empty.", "Pick Red or Blue to place it.");
            else if (t.off)
                t.b.Tooltip = OzUeW.Tip(name, null, "Robots are never neutral in Z: only vehicles and guns can stand empty.", "Pick Red or Blue to place it.");
            else if (neutral)
                t.b.Tooltip = OzUeW.Tip("Empty " + name.ToLowerInvariant() + " (neutral)", null, "Drag onto the field, or click it and then click the field",
                                        "A robot of either team that touches it climbs in: it joins that team", "Units picked + Ctrl + right-click: attack it");
            else if (t.id == ZEvilZod.Type)                                                              // DV161
                t.b.Tooltip = OzUeW.Tip(name, null, "OpenZ only, not in the 1996 Z", "Always joins the " + OzUeUi.TeamWordSentence(Team.Blue).ToLowerInvariant() + " side, whatever the team switch says",
                                        "Drag onto the field, or click it and then click the field", "Shift-click the field: keep placing", "The i corner: unit card");
            else if (OzUePlayground.OpenZOnly(t.id))
                t.b.Tooltip = OzUeW.Tip(name, null, "OpenZ only, not in the 1996 Z", "Drag onto the field, or click it and then click the field",
                                        "Shift-click the field: keep placing", "The i corner: unit card");
            else
                t.b.Tooltip = OzUeW.Tip(name, null, "Drag onto the field, or click it and then click the field", "Shift-click the field: keep placing", "The i corner: unit card");
            t.first = true;
            t.squadShown = -1;
        }

        void RefreshSquads()
        {
            foreach (var t in tiles)
            {
                if (t.squad == null || !t.root.gameObject.activeInHierarchy) continue;
                double v = OzUeSets.InPlay(t.id, UeStat.Squad);
                int n = double.IsNaN(v) || v < 1 ? OzUePlayground.SquadSize(t.id) : (int)System.Math.Round(v);
                if (n == t.squadShown) continue;
                t.squadShown = n;
                t.squadText.Set(OzText.Tab(n.ToString()));
                float w = Mathf.Max(15f, t.squadText.WidthAt(n.ToString()) + 6f + 2f), T = lay != null ? lay.tile : 48f;
                t.squad.TL(T - 3f - w, T - 3f - 15f, w, 15f);
            }
        }

        bool Held(Tile t) => t.b.Pressed || (t.squadHot != null && t.squadHot.Pressed) || (t.info != null && t.info.Pressed);   // its badges are part of it

        /// <summary>
        /// the button under the pointer as it reads it once a frame. Three presses never reached the tile: one on its "i" corner (its own
        /// button), one on the tile's drawn edge (up to EdgeGrace u outside its box), and one made in a long frame, when the pointer was
        /// read only after the pull had already left the tile (OzUePressAt has the point the button really went down at). Such a press
        /// is the tile's: its pull starts the drag, measured from where the button went down, and while it is held the field takes no box
        /// from it.</summary>
        void Rescue()
        {
            var m = Mouse.current; if (m == null || !hud) return;
            if (!m.leftButton.isPressed)
            {
                if (rescueHeld) { rescueHeld = false; OzUePick.HoldInput(rescueHolder, false); }
                foreach (var t in tiles) t.rescued = false;
                return;
            }
            if (!m.leftButton.wasPressedThisFrame) return;
            pressFrom = OzUePressAt.ThisFrame(out var at) ? at : hud.PressPos;
            foreach (var t in tiles) t.rescued = false;
            if (hud.Modal || OzConfirm.Blocks || OzUePlace.Dragging || !OzUeControls.DockOpen) return;
            foreach (var t in tiles) if (Held(t)) return;                                               // the HUD gave it to a tile
            Tile best = null; float bestD = float.MaxValue;
            foreach (var t in tiles)
            {
                if (!t.root.gameObject.activeInHierarchy || !RectTransformUtility.ScreenPointToLocalPointInRectangle(t.root, pressFrom, null, out var lp)) continue;
                var box = t.root.rect; box.xMin -= EdgeGrace; box.yMin -= EdgeGrace; box.xMax += EdgeGrace; box.yMax += EdgeGrace;
                if (!box.Contains(lp)) continue;
                float d = (lp - t.root.rect.center).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            if (best == null) return;
            best.rescued = true; Rescues++;
            if (!hud.PointerOverUi) { rescueHeld = true; OzUePick.HoldInput(rescueHolder, true); }     // the HUD's pointer was already on the field
        }

        void TickTile(Tile t)
        {
            TickCool(t);                                                                                       // 1.3 item 2: the NUKE tile's curtain and seconds
            var m = Mouse.current;
            bool pressed = Held(t) || (t.rescued && m != null && m.leftButton.isPressed);               // 1.2 item 70: the "i" corner too; a press the dock took (Rescue)
            bool hover = (t.b.Hovered || (t.squadHot != null && t.squadHot.Hovered) || (t.info != null && t.info.Hovered)) && !t.off && t.offered;   // its badges are part of it (CSS .tile:hover)
            if (hover && !t.wasHover && !OzUePlace.Dragging) AudioDirector.UI("ui_hover");          // spec 11.4: a tile hover ticks, as the testing panel's tiles
            t.wasHover = hover;
            bool armed = OzUePlace.Armed && OzUePlace.Id == t.id && OzUePlace.Team == CurrentTeam(t);
            if (m != null && m.leftButton.wasPressedThisFrame && pressed) { t.dragFired = false; t.refusedThisPress = false; }
            // a press that travels more than 6 u is a drag: P4 takes it over (the ghost, the drop preview, the drop)
            if (pressed && !t.dragFired && m != null && hud)
            {
                float travel = (m.position.ReadValue() - pressFrom).magnitude / Mathf.Max(0.01f, OzUnitEditor.PxPerUnit);   // 1.2 item 70: from where the button went down
                if (travel > DragUnits)
                {
                    t.dragFired = true;
                    if (t.off || !t.offered) { Refuse(t); t.refusedThisPress = true; }
                    else if (Cooling(t)) { NukeRefuse(); t.refusedThisPress = true; }               // 1.3 item 2: the NUKE's 30 s still run
                    else { Drags++; OzUePlace.BeginDrag(t.id, CurrentTeam(t), pressFrom); }
                }
            }
            if (!pressed && m != null && !m.leftButton.isPressed && !m.leftButton.wasReleasedThisFrame) t.dragFired = false;
            if (!t.first && hover == t.shownHover && armed == t.shownArmed && t.off == t.shownOff && pressed == t.shownPressed) { TickInfo(t); return; }
            t.first = false; t.shownHover = hover; t.shownArmed = armed; t.shownOff = t.off; t.shownPressed = pressed;
            foreach (var e in t.hoverEdge) e.enabled = hover && !armed;
            foreach (var e in t.armEdge) e.enabled = armed;
            t.glow.enabled = armed;
            bool dashed = t.off || !t.offered;
            foreach (var e in t.edge) e.enabled = !dashed;
            foreach (var d in t.dash) d.enabled = dashed;
            t.cg.alpha = dashed ? 0.34f : 1f;
            t.art.localScale = pressed && !dashed ? new Vector3(0.96f, 0.96f, 1f) : Vector3.one;
            TickInfo(t);
        }

        static void TickInfo(Tile t)
        {
            if (t.info == null) return;
            bool h = t.info.Hovered;
            t.infoDisc.color = h ? (Color)OzUi.AmberHi : OzUi.A(OzUi.GM950, 0.86f);
            t.infoRing.color = h ? (Color)OzUi.Amber : (Color)OzUi.GM400;
            t.infoI.color = h ? OzUeW.DarkInk : (Color)OzUi.Ink;
        }

        // ------------------------------------------------------------------------------------------------ 1.3 item 2: the NUKE's cooldown
        /// <summary>Checks: the NUKE tile's curtain share and seconds of the last frame (-1 = none), presses refused while it runs.</summary>
        public static float NukeCurtain = -1f; public static int NukeSecondsShown = -1, NukeRefusals;

        static bool Cooling(Tile t) => t != null && t.id == OzUePlayground.Nuke && !OzUeNuke.Ready;

        static void NukeRefuse()
        {
            NukeRefusals++;
            OzUeToast.Show(OzUeNuke.WaitWords, true);
            AudioDirector.UI("ui_error");
        }

        /// <summary>The curtain (a dark veil over the picture, as tall as the share of the 30 s still to run, shrinking to the top) and the
        /// whole seconds left in its middle; at the end a short amber glow (the ready chime is OzUeNuke.TickReady's).</summary>
        static void MakeCooldown(Tile t)
        {
            t.cool = Hud.Img(t.art, "Cooldown", OzUi.A(OzUi.GM950, 0.78f));
            t.cool.raycastTarget = false;
            t.cool.enabled = false;
            t.coolText = OzUeW.Centred(t.art, "CooldownSeconds", "", 15f, OzUi.Ink, 0f);
            t.coolText.SetActive(false);
        }

        void TickCool(Tile t)
        {
            if (!t.cool) return;
            float share = OzUeNuke.CoolShare, T = lay != null ? lay.tile : 48f;
            bool on = share > 0f;
            float h = on ? Mathf.Max(1f, Mathf.Round((T - 6f) * share)) : 0f;
            if (Mathf.Abs(h - t.coolH) > 0.01f)
            {
                if (on && t.coolH <= 0f) t.readyGlowUntil = 0f;
                if (!on && t.coolH > 0f) t.readyGlowUntil = Time.unscaledTime + 0.6f;                           // it just became ready: a short amber glow
                t.coolH = h;
                t.cool.enabled = on;
                if (on) t.cool.rectTransform.TL(3f, 3f, T - 6f, h);
            }
            int sec = on ? OzUeNuke.SecondsLeft : -1;
            if (sec != t.coolShown)
            {
                t.coolShown = sec;
                t.coolText.SetActive(on);
                if (on) t.coolText.Set(OzText.Tab(sec.ToString()));
            }
            bool armed = OzUePlace.Armed && OzUePlace.Id == t.id;
            if (!armed) t.glow.enabled = Time.unscaledTime < t.readyGlowUntil;
            NukeCurtain = on ? share : -1f; NukeSecondsShown = sec;
        }

        // ------------------------------------------------------------------------------------------------ counted check helpers
        /// <summary>The labels of every tab's tiles (the layout measures them).</summary>
        public static string[][] TileLabels()
        {
            var lists = new[] { OzUePlayground.Robots, OzUePlayground.Vehicles, OzUePlayground.Guns, OzUePlayground.Special };
            var o = new string[4][];
            for (int k = 0; k < 4; k++) { o[k] = new string[lists[k].Length]; for (int j = 0; j < lists[k].Length; j++) o[k][j] = OzUePlayground.ShortName(lists[k][j]); }
            { var sp = new List<string>(o[3]); foreach (var id in OzUeBlasts.Ids) sp.Add(OzUePlayground.ShortName(id)); o[3] = sp.ToArray(); }   // 1.5 ue_explosions_6: + the six test explosions
            return o;
        }
    }
}
