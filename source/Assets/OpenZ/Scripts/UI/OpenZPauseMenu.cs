using System;
using System.Collections.Generic;
using System.Reflection;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// Pause menu, options and controls (guide 9.12, mock-up mock_pause_options): Esc (or the MENU button) pauses the game
    /// like Zod's pause (ZGame.paused: the sim clock stops; Time.timeScale 0 freezes the animations; UI runs on unscaled
    /// time) and dims the world through a post volume (saturation -55, exposure -1.2, vignette 0.4) plus a GM_950 35 %
    /// overlay. Left: the hazard-lidded pause panel with the PAUSED stencil and RESUME / OPTIONS / CONTROLS / RESTART LEVEL /
    /// QUIT TO MAIN MENU. Right: OPTIONS (game speed 25-400 % like gmm_options.cpp, music / effects / voice volumes, edge
    /// scroll, health bars, screen shake, reduced motion, UI scale) or CONTROLS (the key list). Settings persist in
    /// PlayerPrefs (oz_*). Esc does not open the menu while a production window, the rally tool or cannon placement owns
    /// it (those close first), and never over the end screen. Fixes Zod quirk 1: Esc no longer quits.
    /// </summary>
    [DefaultExecutionOrder(-44)]
    public partial class OpenZPauseMenu : MonoBehaviour                                     // 1.4: partial, the camera rows (UI/OpenZPauseMenu.CamControls.cs)
    {
        public bool IsOpen { get; private set; }
        OpenZHud hud; ZGame game; Canvas canvas; RectTransform root, pausePanel, sidePanel, optionsRoot, controlsRoot, optMainPage;
        CanvasGroup rootCg; Image dim; OzPost post; float openAt = -10f, closeAt = -10f; bool closing, wasPaused; float prevScale = 1f;
        OzText sub, matchMenuFoot; HudButton bResume, bOptions, bControls, bRestart, bQuit, bReport, bPauseMatch, skirmishSurrender, skirmishMapVote;
        internal const int DuelEscapeHoldSeconds = 2;
        double duelEscapeStarted = -1;
        bool duelEscapeStartedPaused;
        bool showControls;
        readonly List<HudButton> speedBtns = new List<HudButton>();
        public static readonly float[] Speeds = { 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 4f };   // rising; also the steps of the + and - keys (OzSpeedKeys)
        static readonly float[] Scales = OzText.ScalePresets;

        sealed class Slider { public RectTransform track; public Image fill, thumb; public OzText value; public string key, bus; public float v, w; public Action<float> apply; }
        readonly List<Slider> sliders = new List<Slider>(); Slider dragging;
        sealed class Toggle { public HudButton row; public Image knob, track; public OzText state, value; public Func<bool> get; public Action<bool> set; public bool scale, chatter, robots; public string label; public int tab; }
        readonly List<Toggle> toggles = new List<Toggle>();
        int optTab; readonly List<HudButton> optTabBtns = new List<HudButton>();                             // options tabs (OzSettingTips.TabNames)
        int buildKeysTab;
        OzBuildKeyOptions buildKeyOptions;

        /// <summary></summary>
        public HudButton ProofReportButton => bReport;

        public void Build(OpenZHud h)
        {
            hud = h; game = h.game;
            var go = new GameObject("OpenZ_Pause", typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(h.transform, false);
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = h.sortingOrder + 5;
            var cs = go.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080); cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; cs.matchWidthOrHeight = 1f;
            root = Hud.Node("Root", go.transform); root.Fill();
            rootCg = root.gameObject.AddComponent<CanvasGroup>(); rootCg.blocksRaycasts = false;
            h.AddModalBlocker(root);
            var vig = Hud.Kit(root, "Vignette", "Backdrops/bd_vignette", new OzRect(0, 0, 10, 10), null, Image.Type.Simple); vig.rectTransform.Fill(-100, 0, -100, 0); vig.enabled = OzKit.Available;
            dim = Hud.Img(root, "Dim", OzUi.A(OzUi.GM950, 0.35f)); dim.rectTransform.Fill();
            float cx = OzLayout.RefW * 0.5f;

            // ---- pause panel
            pausePanel = Hud.Node("PausePanel", root).Place(new OzRect(OzAnchor.TC, cx + OzLayout.PauseDX, OzLayout.PauseY, OzLayout.PauseW, OzLayout.PauseH));
            if (OzKit.Available) Hud.Shadow(pausePanel, "Panels/pause_panel_mask");
            var pp = Hud.Kit(pausePanel, "Plate", "Panels/pause_panel", new OzRect(0, 0, OzLayout.PauseW, OzLayout.PauseH), null);
            if (!OzKit.Available) pp.color = HudStyle.Slate;
            bool duel = OpenZ.Multiplayer.DuelRuntime.Active;
            bool onlineSkirmish = duel && OpenZ.Multiplayer.DuelRuntime.I.SkirmishRoom;
            var ms = OzKit.GetSprite("Wordmarks/mask_paused");
            if (duel)
                Hud.Label(pausePanel, "MatchMenu", "MATCH MENU", 56, OzUi.Ink, 0.02f).At(OzLayout.PauseWordX, OzLayout.PauseWordY, OzH.L, false, OzLayout.PauseBtnW);
            else if (ms != null)
            {
                float k = OzLayout.PauseWordCap / 118f;                                                      // the stencil mask is cap 118 u at native size
                Hud.Kit(pausePanel, "Paused", "Wordmarks/mask_paused", new OzRect(OzLayout.PauseWordX, OzLayout.PauseWordY, ms.rect.width * 0.5f * k, ms.rect.height * 0.5f * k), OzUi.Ink, Image.Type.Simple);
            }
            else Hud.Label(pausePanel, "Paused", "PAUSED", 77, OzUi.Ink, -0.02f).At(OzLayout.PauseWordX, OzLayout.PauseWordY + 2);
            sub = Hud.Label(pausePanel, "Sub", "", 11, OzUi.Muted, 0.2f, OzStyle.Plain).At(OzLayout.PauseSubX, duel ? 98 : OzLayout.PauseSubY, OzH.L, false, 400);
            int bi = 0;
            HudButton Big(string label, HudButtonStyle st, Action act)
            {
                var b = HudButton.Make(pausePanel, "Btn" + label, label, new Vector2(OzLayout.PauseBtnW, OzLayout.PauseBtnH), HudButtonKind.Large, 16, 0.14f, st, true, OzH.L);
                if (OpenZ.Multiplayer.DuelRuntime.Active)
                    b.Rt.TL(OzLayout.PauseBtnX, 119 + 45 * bi++, OzLayout.PauseBtnW, 41);
                else b.Rt.Place(new OzRect(OzLayout.PauseBtnX, OzLayout.PauseBtnY0 + OzLayout.PauseBtnStep * bi++, OzLayout.PauseBtnW, OzLayout.PauseBtnH));
                b.Priority = 100; b.OnClick = c => act(); return b;
            }
            bResume = Big(duel ? "BACK TO MATCH" : "RESUME", HudButtonStyle.Primary, Close);
            Hud.Keycap(bResume.Content, "Esc", "ESC", OzLayout.PauseBtnW - 10, (OzLayout.PauseBtnH - 14) * 0.5f);
            bOptions = Big("OPTIONS", HudButtonStyle.Normal, () => { if (showControls) OzSfx.Ui("ui_tab_switch"); showControls = false; });
            bControls = Big("CONTROLS", HudButtonStyle.Normal, () => { if (!showControls) OzSfx.Ui("ui_tab_switch"); showControls = true; buildKeyOptions?.Cancel(); });
            if (!onlineSkirmish)
                bRestart = Big("RESTART LEVEL", HudButtonStyle.Normal, () => OzConfirm.Ask("RESTART THE LEVEL?", "THE MATCH STARTS OVER FROM THE BEGINNING.", () => OzScenes.Restart(), id => OzSfx.Ui(id), canvas.sortingOrder + 10));
            if (duel && !onlineSkirmish)
            {
                bRestart.OnClick = c => { Close(); OpenZ.Multiplayer.DuelRuntime.I.RequestRestart(); };
                bRestart.Tooltip = "Ask your opponent to restart this map. Both players must agree.";
                float row = 119 + 45 * bi;
                float halfWidth = (OzLayout.PauseBtnW - 8) * .5f;
                var surrender = Big("SURRENDER", HudButtonStyle.Danger, () => OzConfirm.Ask("SURRENDER THE DUEL?", "YOUR FORT WILL EXPLODE. YOUR OPPONENT WINS.", () => { Close(); OpenZ.Multiplayer.DuelRuntime.I.Surrender(); }, id => OzSfx.Ui(id), canvas.sortingOrder + 10));
                surrender.Rt.TL(OzLayout.PauseBtnX, row, halfWidth, 41);
                bi--;
                bPauseMatch = Big("PAUSE MATCH", HudButtonStyle.Normal, () =>
                {
                    var runtime = OpenZ.Multiplayer.DuelRuntime.I;
                    if (!runtime.CanPauseMatch) return;
                    Close();
                    runtime.RequestPauseMatch();
                });
                bPauseMatch.Rt.TL(OzLayout.PauseBtnX + halfWidth + 8, row, halfWidth, 41);
                bPauseMatch.Tooltip = $"Pause the match now. You can also hold Esc for {DuelEscapeHoldSeconds} seconds.";
                Big(OpenZ.Multiplayer.DuelRuntime.I.IsHost ? "CHANGE MAP" : "ASK TO CHANGE MAP", HudButtonStyle.Normal,
                    () => OpenZ.Multiplayer.DuelRuntime.I.RequestMapChange());
                Big("RETURN TO LOBBY", HudButtonStyle.Normal, () => OpenZ.Multiplayer.DuelRuntime.I.RequestLobbyReturn());
            }
            else if (game.World?.Map.skirmish == true)
            {
                skirmishSurrender = Big("SURRENDER", HudButtonStyle.Danger, () =>
                {
                    if (game.World.SkirmishEliminated(game.player)) return;
                    OzConfirm.Ask("SURRENDER THIS MATCH?", "YOUR FORT WILL BE LOST. YOU CAN WATCH UNTIL THE MATCH ENDS.", () =>
                    {
                        hud.Production?.Close();
                        Close();
                        if (onlineSkirmish) OpenZ.Multiplayer.DuelRuntime.I.Surrender();
                        else game.World.SurrenderSkirmish(game.player);
                    }, id => OzSfx.Ui(id), canvas.sortingOrder + 10);
                });
                if (onlineSkirmish)
                    skirmishMapVote = Big("PROPOSE MAP CHANGE", HudButtonStyle.Normal, () => OpenZ.Multiplayer.DuelRuntime.I.Menus.ShowRoomVote());
                if (onlineSkirmish && OpenZ.Multiplayer.DuelRuntime.I.IsHost)
                    Big("MANAGE PLAYERS", HudButtonStyle.Normal, () => OpenZ.Multiplayer.DuelRuntime.I.Menus.ShowRoomPlayers());
            }
            bQuit = Big("QUIT TO MAIN MENU", HudButtonStyle.Danger, () => OzConfirm.Ask("QUIT TO THE MAIN MENU?", "THIS MATCH ENDS HERE AND ITS PROGRESS IS LOST.", () => { OzSfx.Ui("ui_back"); OzScenes.MainMenu(); }, id => OzSfx.Ui(id), canvas.sortingOrder + 10));
            if (OpenZ.Multiplayer.DuelRuntime.Active)
            {
                bQuit.OnClick = c => OzConfirm.Ask(onlineSkirmish ? "LEAVE THE MATCH?" : "LEAVE THE DUEL?", "YOU WILL DISCONNECT FROM THE OTHER PLAYER.", () => OpenZ.Multiplayer.DuelRuntime.I.Menus.LeaveMatch(), id => OzSfx.Ui(id), canvas.sortingOrder + 10);
            }
            // system browser (OzFeedback, Application.OpenURL); the game stays paused, nothing else changes. The HUD plays ui_click.
            bReport = HudButton.Make(pausePanel, "BtnReportBug", "REPORT A BUG  ↗", new Vector2(OzLayout.PauseBtnW, 36f), HudButtonKind.Small, 11, 0.16f);
            bReport.Rt.Place(new OzRect(OzLayout.PauseBtnX, OpenZ.Multiplayer.DuelRuntime.Active ? 482 : OzLayout.PauseBtnY0 + OzLayout.PauseBtnStep * bi + 4f, OzLayout.PauseBtnW, 36f));
            bReport.Priority = 100; bReport.OnClick = c => OzFeedback.Open("pause menu");
            matchMenuFoot = Hud.Label(pausePanel, "Foot", duel ? "ONLINE MATCH CONTINUES WHILE THIS MENU IS OPEN" : "GAME TIME IS FROZEN WHILE PAUSED", 9, OzUi.Dim, 0.2f, OzStyle.Plain).At(OzLayout.PauseBtnX, OzLayout.PauseFootY);

            // ---- side panel: options / controls
            sidePanel = Hud.Node("SidePanel", root).Place(new OzRect(OzAnchor.TC, cx + OzLayout.OptionsDX, OzLayout.PauseY, OzLayout.PauseW, OzLayout.PauseH));
            if (OzKit.Available) Hud.Shadow(sidePanel, "Panels/panel_lg_mask");
            var op = Hud.Kit(sidePanel, "Plate", "Panels/options_panel", new OzRect(0, 0, OzLayout.PauseW, OzLayout.PauseH), null);
            if (!OzKit.Available) op.color = HudStyle.Slate;
            optionsRoot = Hud.Node("Options", sidePanel); optionsRoot.Fill();
            controlsRoot = Hud.Node("Controls", sidePanel); controlsRoot.Fill();
            BuildOptions();
            if (OpenZ.Multiplayer.DuelRuntime.Active)
                foreach (var speed in speedBtns) { speed.Interactable = false; speed.Tooltip = "Online matches use a shared game speed."; speed.Refresh(); }
            BuildControls();
            go.SetActive(false);
            ApplySaved();
        }

        // ------------------------------------------------------------------ options (guide 9.12)
        void BuildOptions()
        {
            var p = optionsRoot; float x = OzLayout.OptX, w = OzLayout.PauseW - 2 * OzLayout.OptX;
            Hud.Label(p, "Title", "OPTIONS", 16, OzUi.Ink, 0.16f).At(x, 15);
            var mp = optMainPage = Hud.Node("TabMain", p); mp.Fill(0f, -OzLayout.OptTabShift, 0f, OzLayout.OptTabShift);   // options tabs: MAIN's fixed controls sit on their own page under the tab strip
            Hud.Label(mp, "SpeedLabel", "GAME SPEED", 10, OzUi.Muted, 0.22f, OzStyle.Plain).At(x, OzLayout.OptSpeedLabelY);
            float sw = w / Speeds.Length;
            for (int i = 0; i < Speeds.Length; i++)
            {
                float sp = Speeds[i];
                var b = HudButton.Make(mp, "Speed" + i, Mathf.RoundToInt(sp * 100) + "%", new Vector2(sw - 4, OzLayout.OptSpeedH), HudButtonKind.Small, OzLayout.OptRowText, 0.04f);
                b.Rt.Place(new OzRect(x + i * sw, OzLayout.OptSpeedY, sw - 4, OzLayout.OptSpeedH));
                b.Priority = 100; b.OnClick = c => { game.gameSpeed = sp; PlayerPrefs.SetFloat("oz_speed", sp); };
                speedBtns.Add(b);
            }
            AddSlider(mp, 0, 0, "MUSIC", "oz_vol_music", "Music", v => OzAudio.Set("Music", v), OzSettingTips.Music);
            AddSlider(mp, 0, 1, "EFFECTS · BATTLE", "oz_vol_effects", "SFX", v => OzAudio.Set("SFX", v), OzSettingTips.Effects);
            AddSlider(mp, 1, 0, "ANNOUNCER", "oz_vol_voice", "Announcer", v => OzAudio.Set("Announcer", v), OzSettingTips.Announcer);
            AddSlider(mp, 1, 1, "ROBOT VOICES", "oz_vol_robots", "Robots", v => OzAudio.Set("Robots", v), OzSettingTips.Robots);
            AddToggle(p, 0, "EDGE SCROLL", () => PlayerPrefs.GetInt("oz_edge_scroll", 1) == 1, v => { PlayerPrefs.SetInt("oz_edge_scroll", v ? 1 : 0); ApplyEdgeScroll(); });
            AddToggle(p, 1, "HEALTH BARS ON EVERY DAMAGED UNIT", () => OpenZUnitOverlay.AlwaysBars, v => OpenZUnitOverlay.AlwaysBars = v);
            AddToggle(p, 2, "CAMERA SHAKE ON EXPLOSIONS", () => PlayerPrefs.GetInt("oz_shake", 1) == 1, v => PlayerPrefs.SetInt("oz_shake", v ? 1 : 0));
            AddToggle(p, 3, OzSettingTips.CalmLabel, () => OzMotion.Reduced, v => OzMotion.Reduced = v);
            AddToggle(p, 4, "UI SCALE", null, null, true);
            AddToggle(p, 5, "ANNOUNCER CHATTER", null, null, true, true);
            AddToggle(p, 6, OzSettingTips.RobotChatterLabel, null, null, true, true, true);
            AddToggle(p, 7, OzNightRain.Label, () => OzWeather.Enabled, v => OzWeather.Enabled = v);   // NIGHT & RAIN (oz_weather, default on; off = always day, no rain: OzNightRain.cs)
            AddToggle(p, 8, "HIT FLASH ON DAMAGED UNITS", () => OpenZDamageStates.HitFlash, v => OpenZDamageStates.HitFlash = v);
            AddToggle(p, 9, "MOTION BLUR · FAST DEBRIS AND SHELLS", () => UnitLookMotionGate.MotionBlurOn, v => UnitLookMotionGate.MotionBlurOn = v);
            AddToggle(p, 10, OzSettingTips.PathV3Label, () => OzSettingTips.PathV3, v => OzSettingTips.PathV3 = v);
            AddToggle(p, toggles.Count, OzOvertake.Label, () => OzOvertake.On, v => OzOvertake.On = v);     // DV105 FAST UNITS PASS SLOW ONES: oz_overtake, default OFF, from the next match (OzOvertake.cs)
            AddToggle(p, 11, OzSettingTips.MinimalLabel, () => OzSettingTips.MinimalUi, v => OzSettingTips.MinimalUi = v);
            CamControlRows(p);                                                                           // 1.4: MIDDLE MOUSE (was FREE CAMERA, its saved switch carries over), SCROLL SPEED, RIGHT-DRAG SCROLL (UI/OpenZPauseMenu.CamControls.cs)
            AddToggle(p, toggles.Count, NightShadows.Label, null, null, true);
            { var ns = toggles[toggles.Count - 1]; ns.row.OnClick = c => { NightShadows.Step(c.right || c.shift ? -1 : Arrow(ns)); OzSfx.Ui("ui_tab_switch"); }; ns.row.OnRightClick = c => NightShadows.Step(-1); }
            AddToggle(p, toggles.Count, CraterField.Label, () => CraterField.Enabled, v => CraterField.Enabled = v);
            AddToggle(p, toggles.Count, OzQuality.Label, null, null, true);
            { var gq = toggles[toggles.Count - 1]; gq.row.OnClick = c => { OzQuality.Step(c.right || c.shift ? -1 : Arrow(gq)); OzSfx.Ui("ui_tab_switch"); }; gq.row.OnRightClick = c => OzQuality.Step(-1); }
            AddToggle(p, toggles.Count, OzFpsCap.Label, null, null, true);                                  // FRAME RATE LIMIT: 30 / 40 / 60 / UNLIMITED (VSYNC) (oz_fps_cap, default 60, saved and applied at once; 1.3 item 5)
            { var fc = toggles[toggles.Count - 1]; fc.value.At(OzLayout.PauseW - 2 * OzLayout.OptX - 16, OzLayout.OptToggleH * 0.5f, OzH.R, true, OzSettings.ResValueW); fc.row.OnClick = c => { OzFpsCap.Step(c.right || c.shift ? -1 : Arrow(fc)); OzSfx.Ui("ui_tab_switch"); }; fc.row.OnRightClick = c => OzFpsCap.Step(-1); }   // 1.3 item 5: the wide value box, as RESOLUTION
            AddToggle(p, toggles.Count, OzQuality.FpsLabel, null, null, true);
            { var fp = toggles[toggles.Count - 1]; fp.row.OnClick = c => { OzQuality.FpsStep(c.right || c.shift ? -1 : Arrow(fp)); OzSfx.Ui("ui_tab_switch"); }; fp.row.OnRightClick = c => OzQuality.FpsStep(-1); }
            AddToggle(p, toggles.Count, OzSmoothFrames.Label, () => OzSmoothFrames.On, v => OzSmoothFrames.On = v);   // v1.3 item 25: oz_smooth_frames, default OFF, live (View/OzWeakPcOptions.cs)
            AddToggle(p, toggles.Count, OzSunSteps.Label, null, null, true);                               // v1.3 item 25: SUN SHADOW STEPS 4 (default) / 2 (oz_sun_steps, live)
            { var ss = toggles[toggles.Count - 1]; ss.row.OnClick = c => { OzSunSteps.Step(c.right || c.shift ? -1 : Arrow(ss)); OzSfx.Ui("ui_tab_switch"); }; ss.row.OnRightClick = c => OzSunSteps.Step(-1); }
            AddToggle(p, toggles.Count, OzEndingDebris.Label, null, null, true);                           // v1.3 item 25: ENDING DEBRIS FULL (default) / LIGHT (oz_ending_debris, live)
            { var ed = toggles[toggles.Count - 1]; ed.row.OnClick = c => { OzEndingDebris.Step(c.right || c.shift ? -1 : Arrow(ed)); OzSfx.Ui("ui_tab_switch"); }; ed.row.OnRightClick = c => OzEndingDebris.Step(-1); }
            if (OzGfxApi.Available)
            {   // v1.3 item 25: GRAPHICS API DX12 (default) / DX11 (oz_gfx_api, from the next start, Windows only)
                AddToggle(p, toggles.Count, OzGfxApi.Label, null, null, true);
                var ga = toggles[toggles.Count - 1]; ga.row.OnClick = c => { OzGfxApi.Step(c.right || c.shift ? -1 : Arrow(ga)); OzSfx.Ui("ui_tab_switch"); }; ga.row.OnRightClick = c => OzGfxApi.Step(-1);
            }
            AddToggle(p, toggles.Count, OzTouch.Label, null, null, true);                                   // touch and gamepad: AUTO / TOUCH ON / OFF (oz_touch, live; OzTouch.cs)
            { var tg = toggles[toggles.Count - 1]; tg.row.OnClick = c => { OzTouch.StepMode(c.right || c.shift ? -1 : Arrow(tg)); OzSfx.Ui("ui_tab_switch"); }; tg.row.OnRightClick = c => OzTouch.StepMode(-1); }
            AddToggle(p, toggles.Count, OzTrackpadScroll.Label, null, null, true);                          // TRACKPAD SCROLL: ZOOM (default) / MOVE MAP, oz_trackpad_scroll, live (OzTrackpadScroll.cs)
            { var tp = toggles[toggles.Count - 1]; tp.row.OnClick = c => { OzTrackpadScroll.Step(c.right || c.shift ? -1 : Arrow(tp)); OzSfx.Ui("ui_tab_switch"); }; tp.row.OnRightClick = c => OzTrackpadScroll.Step(-1); }
            AddToggle(p, toggles.Count, MechWalk.Label, () => MechWalk.On, v => MechWalk.On = v);
            AddToggle(p, toggles.Count, OzOverlapOption.Label, null, null, true);                          // FRIENDLY UNITS OVERLAP: NO (default) / YES, oz_overlap, live (OzOverlapOption.cs); the cycle form of this menu
            { var ov = toggles[toggles.Count - 1]; ov.row.OnClick = c => { OzOverlapOption.Step(c.right || c.shift ? -1 : Arrow(ov)); OzSfx.Ui("ui_tab_switch"); }; ov.row.OnRightClick = c => OzOverlapOption.Step(-1); }
            AddToggle(p, toggles.Count, OzVehicleSizeOption.Label, null, null, true);                      // VEHICLE SIZE: 60 to 100 %, oz_vehicle_size, default 90 %, live (OzVehicleSizeOption.cs)
            { var vs = toggles[toggles.Count - 1]; vs.row.OnClick = c => { OzVehicleSizeOption.Step(c.right || c.shift ? -1 : Arrow(vs)); OzSfx.Ui("ui_tab_switch"); }; vs.row.OnRightClick = c => OzVehicleSizeOption.Step(-1); }
            AddToggle(p, toggles.Count, OzRobotSizeOption.Label, null, null, true);                        // 1.5 R3.25 ROBOT SIZE: 60 to 100 %, oz_robot_size, default 100 %, live (UI/OzRobotSizeOption.cs); GAME, under VEHICLE SIZE (OzSettingTips)
            { var rz = toggles[toggles.Count - 1]; rz.row.OnClick = c => { OzRobotSizeOption.Step(c.right || c.shift ? -1 : Arrow(rz)); OzSfx.Ui("ui_tab_switch"); }; rz.row.OnRightClick = c => OzRobotSizeOption.Step(-1); }
            AddToggle(p, toggles.Count, OzRangeRingsOption.Label, null, null, true);                       // 1.4 RANGE RINGS ON SELECTED UNITS: FADE (default) / ALWAYS, oz_range_rings, live (UI/OzRangeRingsOption.cs); GAME, under HEALTH BARS (OzSettingTips)
            { var rg = toggles[toggles.Count - 1]; rg.row.OnClick = c => { OzRangeRingsOption.Step(c.right || c.shift ? -1 : Arrow(rg)); OzSfx.Ui("ui_tab_switch"); }; rg.row.OnRightClick = c => OzRangeRingsOption.Step(-1); }
            if (!OpenZ.Multiplayer.DuelRuntime.Active)
                AddToggle(p, toggles.Count, OzTacticalPause.Label, () => OzTacticalPause.On, v => OzTacticalPause.On = v);
            AddToggle(p, toggles.Count, OzForceBarOption.Label, null, null, true);
            { var fb = toggles[toggles.Count - 1]; fb.value.At(w - 16, OzLayout.OptToggleH * 0.5f, OzH.R, true, w - 130); fb.row.OnClick = c => { OzForceBarOption.StepMatch(OpenZ.Multiplayer.DuelRuntime.Active, 1); OzSfx.Ui("ui_tab_switch"); }; fb.row.OnRightClick = c => OzForceBarOption.StepMatch(OpenZ.Multiplayer.DuelRuntime.Active, -1); }
            AddToggle(p, toggles.Count, OzBattleOptions.DamageLabel, () => OzBattleOptions.SoloDamage, v => OzBattleOptions.SoloDamage = v);
            AddToggle(p, toggles.Count, OzBattleOptions.ShakeLabel, () => OzBattleOptions.SoloShake, v => OzBattleOptions.SoloShake = v);
            AddToggle(p, toggles.Count, OzHudLayout.Label, null, null, true);                               // ARRANGE INTERFACE: this menu closes, then the panels can be dragged (OpenZHud.Arrange.cs)
            { var ar = toggles[toggles.Count - 1]; ar.row.Tooltip = OzHudLayout.Tip; ar.row.OnClick = c => { Close(); hud.RequestArrange(); }; ar.row.OnRightClick = null; }
            AddToggle(p, toggles.Count, OzSettings.FullLabel, () => OzSettings.Fullscreen, v => OzSettings.Fullscreen = v);   // FULLSCREEN, directly over RESOLUTION as in the main menu (oz_fullscreen, saved and applied at once)
            AddToggle(p, toggles.Count, OzSettings.ResLabel, null, null, true);                             // RESOLUTION: NATIVE (default) and the lower steps of this display (oz_res_h, OzSettings), saved and applied at once
            { var rs = toggles[toggles.Count - 1]; rs.value.At(OzLayout.PauseW - 2 * OzLayout.OptX - 16, OzLayout.OptToggleH * 0.5f, OzH.R, true, OzSettings.ResValueW); rs.row.OnClick = c => { OzSettings.ResolutionClick(c.right || c.shift ? -1 : 1); OzSfx.Ui("ui_tab_switch"); }; rs.row.OnRightClick = c => OzSettings.ResolutionClick(-1); }   // 1.5 issue 8: the mouse button sets the way (left: one size larger; right or Shift: one smaller), never the place of the click
            AddToggle(p, toggles.Count, OzStrikeGroups.Label, () => OzStrikeGroups.On, v => OzStrikeGroups.On = v);   // DV107 enemy strike groups: oz_strike_groups, default ON, live (ADVANCED tab, first row; OzStrikeGroups.cs)
            AddToggle(p, toggles.Count, OzLavaOption.Label, () => OzLavaOption.On, v => OzLavaOption.On = v);   // DV152 level 7 lava: oz_lava_burns, default ON, read at the next level start (OzLavaOption.cs)
            AddToggle(p, toggles.Count, OzHighGroundOption.Label, () => OzHighGroundOption.On, v => OzHighGroundOption.On = v);   // DV162 high ground shoots farther: oz_high_ground, default ON, read at the next level start (OzHighGroundOption.cs)
            if (OzFogOfWarOption.InOptions)
            {
            AddToggle(p, toggles.Count, OzFogOfWarOption.Label, () => OzFogOfWarOption.On, v => OzFogOfWarOption.On = v);   // 1.4 FOG OF WAR: oz_fog_of_war, default OFF, read at the next level start, view only (UI/OzFogOfWarOption.cs, View/OzFogOfWar.cs)
            AddToggle(p, toggles.Count, OzFogLookOption.Label, null, null, true);                          // 1.4 FOG LOOK: oz_fog_look CLOUD SEA (default) / CLOUD BANKS / MIST LAYERS, live (UI/OzFogLookOption.cs)
            { var fl = toggles[toggles.Count - 1]; fl.row.OnClick = c => { OzFogLookOption.Step(c.right || c.shift ? -1 : Arrow(fl)); OzSfx.Ui("ui_tab_switch"); }; fl.row.OnRightClick = c => OzFogLookOption.Step(-1); }
            }
            if (OzShuttleStart.InOptions)                                                                      // K347: the SHUTTLE switch is the battlefield screen's box now (UI/OpenZMainMenu.ShuttleStart.cs): the old OPTIONS row below is not built
            AddToggle(p, toggles.Count, OzShuttleOption.Label, () => OzShuttleOption.On, v => OzShuttleOption.On = v);   // DV118 the level 16 shuttle: oz_shuttle, default ON, read at the next level start (OzShuttleOption.cs)
            AddToggle(p, toggles.Count, OzShuttleOrdersOption.Label, null, null, true);                    // K256g SHUTTLE ORDERS: LANDABLE SPOTS ONLY (default) / ANYWHERE, oz_shuttle_orders, live (OzShuttleOrdersOption.cs); ADVANCED, under SHUTTLE
            { var so = toggles[toggles.Count - 1]; so.value.At(OzLayout.PauseW - 2 * OzLayout.OptX - 16, OzLayout.OptToggleH * 0.5f, OzH.R, true, OzSettings.ResValueW); so.row.OnClick = c => { OzShuttleOrdersOption.Step(c.right || c.shift ? -1 : Arrow(so)); OzSfx.Ui("ui_tab_switch"); }; so.row.OnRightClick = c => OzShuttleOrdersOption.Step(-1); }
            AddToggle(p, toggles.Count, OzLensEdges.Label, () => OzLensEdges.On, v => OzLensEdges.On = v);   // CHROMATIC ABERRATION · LENS EDGES: oz_chroma, default OFF, live (View/OzLensEdges.cs); its tab and place (GRAPHICS, under MOTION BLUR) come from OzSettingTips
            AddToggle(p, toggles.Count, OzMassLod.Label, () => OzMassLod.On, v => OzMassLod.On = v);         // 1.3 MASS BATTLES · LIGHTER WHEN SLOW: oz_mass_lod, default OFF, live (View/OzMassLod.cs); GRAPHICS, last row (OzSettingTips)
            // 1.3 item 40: lighter rows for a slower processor (View/OzLightRows.cs; every default = today's look, live, saved; GRAPHICS, after MASS BATTLES)
            AddToggle(p, toggles.Count, OzLightRows.LightestLabel, () => OzLightRows.Lightest, v => OzLightRows.Lightest = v);   // a helper: no saved value of its own
            AddToggle(p, toggles.Count, OzLightRows.ShadowsLabel, () => OzLightRows.ShadowsOn, v => OzLightRows.ShadowsOn = v);   // oz_lite_shadows, default ON
            AddToggle(p, toggles.Count, OzLightRows.AoLabel, () => OzLightRows.AoOn, v => OzLightRows.AoOn = v);             // oz_lite_ao, default ON
            AddToggle(p, toggles.Count, OzLightRows.TreesLabel, null, null, true);                                          // oz_lite_trees FULL (default) / LIGHT / LIGHTEST
            { var l40t = toggles[toggles.Count - 1]; l40t.row.OnClick = c => { OzLightRows.TreesStep(c.right || c.shift ? -1 : Arrow(l40t)); OzSfx.Ui("ui_tab_switch"); }; l40t.row.OnRightClick = c => OzLightRows.TreesStep(-1); }
            AddToggle(p, toggles.Count, OzLightRows.WrecksLabel, null, null, true);                                         // oz_lite_wrecks FULL (default) / LIGHT
            { var l40w = toggles[toggles.Count - 1]; l40w.row.OnClick = c => { OzLightRows.WrecksStep(c.right || c.shift ? -1 : Arrow(l40w)); OzSfx.Ui("ui_tab_switch"); }; l40w.row.OnRightClick = c => OzLightRows.WrecksStep(-1); }
            if (OzLowPicture.InOptions)
            {   // 1.5 issue 8: 3D PICTURE SHARP (default) / SOFT / SOFTEST, oz_lite_picture, live, LOW only (View/OzLowPicture.cs); not in the Mac player
                AddToggle(p, toggles.Count, OzLowPicture.Label, null, null, true);
                var l8p = toggles[toggles.Count - 1]; l8p.row.OnClick = c => { OzLowPicture.Step(c.right || c.shift ? -1 : Arrow(l8p)); OzSfx.Ui("ui_tab_switch"); }; l8p.row.OnRightClick = c => OzLowPicture.Step(-1);
            }
            AddBehaviorOptions(p);
            foreach (var t in toggles) if (string.IsNullOrEmpty(t.row.Tooltip)) t.row.Tooltip = OzSettingTips.For(t.label);
            BuildOptTabs(p);                                                                                 // options tabs: the strip, every tab's rows from the top (no row squeezing any more)
            foreach (var t in toggles) OzSettingTips.CentreRow(t.row);                                       // 1.3 item 40 (review): a tab packed below its rows' own layout (GRAPHICS, 17 rows) shows each row's contents in the plate's middle
            foreach (var b in speedBtns) b.Tooltip = OzSettingTips.Speed;
        }

        void AddSlider(RectTransform p, int row, int col, string label, string key, string bus, Action<float> apply, string tip = null)
        {
            float full = OzLayout.PauseW - 2 * OzLayout.OptX, w = (full - OzLayout.OptSliderGap) * 0.5f;
            float x = OzLayout.OptX + col * (w + OzLayout.OptSliderGap), y = OzLayout.OptSliderY0 + OzLayout.OptSliderStep * row;
            int j = row * 2 + col;
            var s = new Slider { key = key, bus = bus, apply = apply, v = PlayerPrefs.GetFloat(key, 1f), w = w };      // 100 % = the audio track's own mix (EFFECTS: 50 % is the mix, 100 % is 6 dB over it, OzAudio.EffectsLin)
            Hud.Label(p, "Label" + j, label, 10, OzUi.Muted, 0.22f, OzStyle.Plain).At(x, y);
            s.value = Hud.Label(p, "Value" + j, "", 11, OzUi.Ink, 0f, OzStyle.Plain).At(x + w, y, OzH.R, false, 60);
            s.track = Hud.Node("Track" + j, p).Place(new OzRect(x, y + 20, w, 8));
            var tr = Hud.Kit(s.track, "Bg", "Bars/bar_track", new OzRect(0, 0, w, 8), null); tr.rectTransform.Fill();
            if (!OzKit.Available) tr.color = OzUi.GM950;
            s.fill = OzKit.Available ? OzKit.FillBar(s.track, "Fill", "amber") : Hud.Img(s.track, "Fill", OzUi.Amber);
            s.fill.rectTransform.Fill(1, 1, 1, 1); s.fill.type = Image.Type.Filled; s.fill.fillMethod = Image.FillMethod.Horizontal;
            s.thumb = Hud.Kit(s.track, "Thumb", "Buttons/slider_thumb", new OzRect(0, -6, 14, 20), OzKit.Available ? (Color?)null : OzUi.Ink, Image.Type.Simple);
            var hot = HudButton.Make(p, "SliderHot" + j, null, new Vector2(w + 8, 30), HudButtonKind.Small);
            hot.Rt.Place(new OzRect(x - 4, y + 10, w + 8, 30)); hot.Bg.enabled = false; hot.Priority = 100; hot.Tooltip = tip;
            if (hot.Hatch) { Destroy(hot.Hatch.gameObject); hot.Hatch = null; }
            sliders.Add(s);
            ShowSlider(s);
        }

        void ShowSlider(Slider s)
        {
            float w = s.w > 0f ? s.w : OzLayout.PauseW - 2 * OzLayout.OptX;
            s.fill.fillAmount = s.v;
            s.thumb.rectTransform.anchoredPosition = new Vector2(w * s.v - 7f, 6f);
            s.value.Set(OzText.Tab(Mathf.RoundToInt(s.v * 100) + "%"));
        }

        void AddToggle(RectTransform p, int j, string label, Func<bool> get, Action<bool> set, bool scale = false, bool chatter = false, bool robots = false)
        {
            float x = OzLayout.OptX, w = OzLayout.PauseW - 2 * OzLayout.OptX, y = OzLayout.OptToggleY0 + OzLayout.OptToggleStep * j;
            var t = new Toggle { get = get, set = set, scale = scale, chatter = chatter, robots = robots, label = label, tab = OzSettingTips.TabOf(label) };
            t.row = HudButton.Make(p, "Toggle" + j, null, new Vector2(w, OzLayout.OptToggleH), HudButtonKind.Small);
            t.row.Rt.Place(new OzRect(x, y, w, OzLayout.OptToggleH)); t.row.Priority = 100;
            Hud.Label(t.row.Content, "Label", label, OzLayout.OptRowText, OzUi.Ink, 0.1f, OzStyle.Plain).At(16, OzLayout.OptToggleH * 0.5f, OzH.L, true, 300);
            if (scale)
            {
                t.value = Hud.Label(t.row.Content, "Value", "", OzLayout.OptRowText, OzUi.Ink2, 0.06f, OzStyle.Plain).At(w - 16, OzLayout.OptToggleH * 0.5f, OzH.R, true, 170);
                if (chatter && robots)
                {
                    t.row.Tooltip = OzSettingTips.RobotChatter;
                    t.row.OnClick = c => { AudioDirector.RobotChatter.Level = (AudioDirector.RobotChatter.Level + (c.right || c.shift || Arrow(t) < 0 ? 2 : 1)) % 3; OzSfx.Ui("ui_tab_switch"); };
                    t.row.OnRightClick = c => AudioDirector.RobotChatter.Level = (AudioDirector.RobotChatter.Level + 2) % 3;
                }
                else if (chatter)
                {
                    t.row.Tooltip = "ANNOUNCER CHATTER|Full: every Rachel line (default). Less: only important lines, longer gaps. Minimal: only critical lines (fort under attack, victory, defeat, territory).|";
                    t.row.OnClick = c => { AudioDirector.Chatter.Level = (AudioDirector.Chatter.Level + (c.right || c.shift || Arrow(t) < 0 ? 2 : 1)) % 3; OzSfx.Ui("ui_tab_switch"); };
                    t.row.OnRightClick = c => AudioDirector.Chatter.Level = (AudioDirector.Chatter.Level + 2) % 3;
                }
                else
                {
                t.row.OnClick = c => { StepScale(c.right || c.shift ? -1 : Arrow(t)); OzSfx.Ui("ui_tab_switch"); };
                t.row.OnRightClick = c => StepScale(-1);
                }
            }
            else
            {
                t.track = Hud.Kit(t.row.Content, "Track", "Buttons/switch_track", new OzRect(w - 16 - 48, (OzLayout.OptToggleH - 18) * 0.5f, 48, 18), null);
                if (!OzKit.Available) t.track.color = OzUi.GM950;
                bool initial = get != null && get();
                t.knob = Hud.Kit(t.row.Content, "Knob", "Buttons/switch_knob", new OzRect(w - 16 - 48 + (initial ? 24 : 2), (OzLayout.OptToggleH - 14) * 0.5f, 22, 14), initial ? (Color)OzUi.Amber : OzUi.GM500, Image.Type.Simple);
                t.state = Hud.Label(t.row.Content, "State", initial ? "ON" : "OFF", 9, OzUi.Amber, 0.2f, OzStyle.Plain).At(w - 16 - 48 - 8, OzLayout.OptToggleH * 0.5f, OzH.R, true, 40);
                t.row.OnClick = c => { bool v = !t.get(); t.set(v); PlayerPrefs.Save(); OzSfx.Ui(v ? "ui_toggle_on" : "ui_toggle_off"); };
            }
            OzForkOptions.Highlight(t.row, label);
            toggles.Add(t);
        }

        // ------------------------------------------------------------------ options tabs (OzSettingTips.TabNames: MAIN / GAME / GRAPHICS / ADVANCED)
        /// <summary>The tab strip under the title. Every row carries the tab of its label (OzSettingTips.TabOf, the main menu's table);
        /// each tab's rows are placed from the top in the table's order and only the open tab's rows are active. MAIN also holds
        /// the game speed row and the four volume sliders (the TabMain page).</summary>
        /// <summary>v1.3: the most rows a tab shows at the roomy spacing (9 end at 580 of the 600 panel); a longer tab is spaced closer.</summary>
        const int RoomyRows = 9;
        void BuildOptTabs(RectTransform p)
        {
            bool duel = OpenZ.Multiplayer.DuelRuntime.Active;
            float x = OzLayout.OptX, w = OzLayout.PauseW - 2 * OzLayout.OptX;
            buildKeysTab = OzSettingTips.TabNames.Length + (duel ? 1 : 0);
            int n = buildKeysTab + 1;
            float unitWidth = (w - OzLayout.OptTabGap * (n - 1)) / n;
            float[] weights = duel ? new[] { .65f, .65f, .9f, .95f, 1.75f, 1.1f } : new[] { .8f, .8f, 1f, 1.1f, 1.3f };
            optTabBtns.Clear();
            float tabX = x;
            for (int i = 0; i < n; i++)
            {
                int id = i;
                float bw = unitWidth * weights[i];
                string label = i == buildKeysTab ? OzBuildKeys.Label : i == MicrophoneTab ? OzForkOptions.AudioDevicesLabel : OzSettingTips.TabNames[i];
                var b = HudButton.Make(p, "Tab" + i, label, new Vector2(bw, OzLayout.OptTabH), HudButtonKind.Small, i == MicrophoneTab && duel ? 9 : 11, 0.06f);
                b.Label.At(bw * .5f, OzLayout.OptTabH * .5f, OzH.C, true, bw - 8f);
                OzForkOptions.Highlight(b, label);
                b.Rt.Place(new OzRect(tabX, OzLayout.OptTabY, bw, OzLayout.OptTabH));
                tabX += bw + OzLayout.OptTabGap;
                b.Priority = 100; b.OnClick = c => { if (optTab != id) OzSfx.Ui("ui_tab_switch"); ShowOptTab(id); };   // the HUD adds ui_click
                optTabBtns.Add(b);
            }
            Hud.Img(p, "TabRule", OzUi.A(OzUi.Amber, 0.35f)).rectTransform.Place(new OzRect(x, OzLayout.OptTabY + OzLayout.OptTabH + 3f, w, 1f));
            for (int tab = 0; tab < n; tab++)
            {
                var rows = toggles.FindAll(t => t.tab == tab);
                rows.Sort((r1, r2) => { int o = OzForkOptions.Compare(r1.label, r2.label); return o != 0 ? o : toggles.IndexOf(r1).CompareTo(toggles.IndexOf(r2)); });
                float top = tab == OzSettingTips.TabMain ? OzLayout.OptToggleY0 + OzLayout.OptTabShift : OzLayout.OptTabRowY0;
                float step = OzLayout.OptToggleStep, rowH = OzLayout.OptToggleH;
                if (rows.Count > RoomyRows) { step = OzLayout.OptToggleStep * (RoomyRows - 1) / (rows.Count - 1); rowH = Mathf.Min(rowH, step - 4f); }   // v1.3: more rows than fit the 600 panel: the tab keeps a 9-row tab's height
                for (int k = 0; k < rows.Count; k++) rows[k].row.Rt.Place(new OzRect(x, top + step * k, w, rowH));
            }
            if (duel) BuildMicrophoneTab();
            buildKeyOptions = new OzBuildKeyOptions(p, OzLayout.PauseW, (page, name, rect, action) =>
            {
                var b = HudButton.Make(page, name, "", rect.Size, HudButtonKind.Small, 11, .02f);
                b.Rt.Place(rect);
                b.Priority = 100;
                b.OnClick = _ => action();
                return b;
            });
            ShowOptTab(Mathf.Clamp(optTab, 0, n - 1));
        }

        /// <summary>Open one tab: its rows and (MAIN) the speed + volume page on, the rest off; the tab's button lights.</summary>
        void ShowOptTab(int tab)
        {
            optTab = tab; dragging = null;
            SetMicrophoneTabVisible(tab == MicrophoneTab);
            buildKeyOptions?.Show(tab == buildKeysTab);
            foreach (var t in toggles) t.row.Rt.gameObject.SetActiveFast(t.tab == tab);
            if (optMainPage) optMainPage.gameObject.SetActiveFast(tab == OzSettingTips.TabMain);
            for (int i = 0; i < optTabBtns.Count; i++) if (optTabBtns[i].Selected != (i == tab)) { optTabBtns[i].Selected = i == tab; optTabBtns[i].Refresh(); }
        }

        /// <summary>Proof hooks: open an options tab (with the OPTIONS page shown), the open tab, the labels of the rows shown now.</summary>
        public void ProofOptTab(int tab) { showControls = false; if (optTabBtns.Count > 0) ShowOptTab(Mathf.Clamp(tab, 0, optTabBtns.Count - 1)); }
        /// <summary>Pause the game and show OPTIONS on one tab (the slow-computer plate opens GRAPHICS this way).</summary>
        public void OpenOptions(int tab) { Open(); if (!IsOpen) return; showControls = false; if (optTabBtns.Count > 0) ShowOptTab(Mathf.Clamp(tab, 0, optTabBtns.Count - 1)); }
        public int ProofOptTabOpen => optTab;
        public string ProofOptTabRows
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                foreach (var t in toggles) if (t.row.Rt.gameObject.activeSelf) { if (sb.Length > 0) sb.Append(" | "); sb.Append(t.label); }
                return sb.ToString();
            }
        }

        /// <summary>The step of a click on a value row: -1 on its left arrow, else +1 (OzSettings.ArrowDir).</summary>
        static int Arrow(Toggle t) { var m = Mouse.current; return m == null ? 1 : OzSettings.ArrowDir(t.value, m.position.ReadValue()); }

        void StepScale(int dir)
        {
            int i = OzSettings.ScaleStep(Scales, OzText.UiScale, dir);                                     // menu_exe_v1: the 109 % default is no preset
            OzText.UiScale = Scales[i]; PlayerPrefs.SetFloat("oz_ui_scale", Scales[i]); PlayerPrefs.Save();
            hud.ApplyUiScale();
        }

        // ------------------------------------------------------------------ controls (Zod key table + OpenZ camera keys)
        // 1.5 kit player_reports_1111 P3: the old fixed key table (Keys, with "SPACE: Go to the last message") is gone: no screen showed it (only the
        // unused OpenZMainMenu.ControlKeys read it, which falls back to its own short list); every controls list draws UI/OzControlsTable.cs (the live keys).

        void BuildControls()
        {
            var p = controlsRoot;
            OzControlsTable.BuildCompact(p, OzLayout.PauseW, OzLayout.PauseH);                              // menu_exe_v1: measured key caps, the live bindings (read-only here)
        }

        // ------------------------------------------------------------------ open / close
        // duel in-game menu
            public void Open()
        {
            if (IsOpen || hud == null || (hud.EndScreen && hud.EndScreen.IsShown)) return;
            if (skirmishSurrender != null)
            {
                skirmishSurrender.Interactable = !game.World.SkirmishEliminated(game.player);
                skirmishSurrender.Refresh();
            }
            if (skirmishMapVote != null)
            {
                var runtime = OpenZ.Multiplayer.DuelRuntime.I;
                skirmishMapVote.Interactable = runtime.RoomCanVote || runtime.RoomVoteMap != null;
                skirmishMapVote.Refresh();
            }
            IsOpen = true; closing = false; openAt = OzMotion.Now;
            wasPaused = game.paused; prevScale = Time.timeScale;
            if (!OpenZ.Multiplayer.DuelRuntime.Active) { game.paused = true; Time.timeScale = 0f; }
            if (hud.Production) hud.Production.Close();
            // The whole in-game HUD (selection panel, minimap,
            // top bar, build bar, toasts, tooltips, unit overlays and labels) goes away while the pause menu is up and comes back once
            // the menu has faded out on resume. A HUD the player hid himself (F10) stays hidden.
            if (!hidHud) { hidHud = hud.hudVisible; if (hidHud) hud.SetVisible(false); }
            post ??= new OzPost("OZ_Pause", -55f, -1.2f, 0.4f, 55);
            canvas.gameObject.SetActive(true);
            optionsRoot.gameObject.SetActiveFast(!showControls); controlsRoot.gameObject.SetActiveFast(showControls);
            if (!showControls && optTab == buildKeysTab) buildKeyOptions?.Refresh();
            string lvl = game.level == "L17" ? "L17 · CAR PARK" : OzCity.IsBridgeGame(game.level) ? OzCity.BridgeGameLabel : game.level == "L16" ? "L16 · JUNGLE" : game.level == "L02" ? "L02 · DESERT" : OzVolcanic.Is(game.level) ? OzVolcanic.OperationLabel : game.level;
            sub.Set(OzText.Tab($"OPERATION {lvl} · {UnitInfo.Clock(game.World.Time)}"));
            RefreshValues();                                                                             // ui_pause / ui_resume: the audio track follows game.paused
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; closing = true; closeAt = OzMotion.Now;
            buildKeyOptions?.Cancel();
            microphoneBinding = false; if (microphonePage && MicrophoneVoice) MicrophoneVoice.Monitoring = false;
            PlayerPrefs.Save();                                                                              // menu_exe_v1: sliders / speed saved when the menu closes
            game.paused = wasPaused;
            Time.timeScale = Multiplayer.DuelRuntime.MatchPlayPaused ? 0f : prevScale > 0f ? prevScale : 1f;
        }

        // duel menu keyboard ownership
        bool CanOpenFromEscape => !(hud.EndScreen && hud.EndScreen.IsShown) && !(hud.Production && hud.Production.IsOpen) &&
            !(hud.Rally && hud.Rally.Armed) && hud.input.Placing == null && !hud.ArrangeOwnsEsc;

        bool DuelEscape(Keyboard keyboard, bool enabled)
        {
            if (!enabled || !Application.isFocused || (!IsOpen && !CanOpenFromEscape))
            {
                duelEscapeStarted = -1;
                return false;
            }
            var key = keyboard.escapeKey;
            if (duelEscapeStarted < 0 && !key.wasPressedThisFrame) return false;
            var runtime = Multiplayer.DuelRuntime.I;
            if (runtime.MatchMenusBlocked || runtime.Menus.IsTyping)
            {
                duelEscapeStarted = -1;
                return false;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (key.wasPressedThisFrame)
            {
                duelEscapeStarted = now;
                duelEscapeStartedPaused = runtime.MatchPaused;
            }
            if (duelEscapeStartedPaused != runtime.MatchPaused || (!key.isPressed && !key.wasReleasedThisFrame))
            {
                duelEscapeStarted = -1;
                return false;
            }
            if (now - duelEscapeStarted >= DuelEscapeHoldSeconds)
            {
                duelEscapeStarted = -1;
                if (runtime.MatchPaused ? runtime.OwnsMatchPause && runtime.Connected : runtime.CanPauseMatch)
                {
                    Close();
                    if (runtime.MatchPaused) runtime.ResumePausedMatch();
                    else runtime.RequestPauseMatch();
                }
                return false;
            }
            if (!key.wasReleasedThisFrame) return false;
            duelEscapeStarted = -1;
            return true;
        }

        void Update()
        {
            if (OzBehaviorOptions.CapturesInput || Multiplayer.DuelRuntime.CapturesInput || hud == null || game == null || game.World == null)
            {
                duelEscapeStarted = -1;
                return;
            }
            var kb = Keyboard.current; var m = Mouse.current;
            bool esc = kb != null && !OzConfirm.Blocks && !OzUeHooks.EscTaken;
            if (OzFpsView.EscTaken) esc = false;                                                     // 1.4 fps_view: Esc leaves the first-person view, never opens this menu (View/OzFpsView.cs)
            if (OzForkOptions.CaptureChatBinding()) esc = false;
            if (IsOpen && !showControls && optTab == buildKeysTab && !OzConfirm.Blocks && buildKeyOptions?.Capture() == true) esc = false;
            if (microphoneBinding) { TickMicrophoneBinding(); esc = false; }
            if (Multiplayer.DuelRuntime.Active && Multiplayer.DuelRuntime.I.Playing) esc = DuelEscape(kb, esc);
            else
            {
                duelEscapeStarted = -1;
                esc = esc && kb.escapeKey.wasPressedThisFrame;
            }
            if (esc)
            {
                if (IsOpen) Close();
                else if (CanOpenFromEscape) Open();
            }
            TickMicrophone();
            ApplyShake();
            if (Time.unscaledTime < audioRetryUntil && Time.unscaledTime > nextAudioRetry) { nextAudioRetry = Time.unscaledTime + 2f; OzAudio.Apply(); }   // the audio table may load after the HUD
            if (IsOpen)
            {
                if (bPauseMatch != null)
                {
                    var runtime = OpenZ.Multiplayer.DuelRuntime.I;
                    if (bPauseMatch.Interactable != runtime.CanPauseMatch)
                    { bPauseMatch.Interactable = runtime.CanPauseMatch; bPauseMatch.Refresh(); }
                    matchMenuFoot.Set(runtime.MatchPaused ? "MATCH PAUSED" : "ONLINE MATCH CONTINUES WHILE THIS MENU IS OPEN");
                }
                float k = OzEase.OutCubic(OzMotion.T(openAt, OzMotion.Dur(OzMotion.Short)));
                rootCg.alpha = k; if (post != null) post.Weight = k;
                pausePanel.localScale = sidePanel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k);
                optionsRoot.gameObject.SetActiveFast(!showControls); controlsRoot.gameObject.SetActiveFast(showControls);
                if (bOptions.Selected != !showControls) { bOptions.Selected = !showControls; bOptions.Refresh(); }
                if (bControls.Selected != showControls) { bControls.Selected = showControls; bControls.Refresh(); }
                if (!OzConfirm.Blocks) DragSliders(m);
                RefreshValues();
            }
            else if (closing)
            {
                float k = OzMotion.T(closeAt, OzMotion.Dur(0.1f, true));
                rootCg.alpha = 1f - k; if (post != null) post.Weight = 1f - k;
                if (k >= 1f) { closing = false; canvas.gameObject.SetActive(false); RestoreHud(); }
            }
        }

        /// <summary>The HUD this menu hid on Open comes back (after the close fade, so it never shows under the fading panel).</summary>
        bool hidHud;
        void RestoreHud() { if (!hidHud) return; hidHud = false; if (hud && !(hud.EndScreen && hud.EndScreen.IsShown)) hud.SetVisible(true); }

        void DragSliders(Mouse m)
        {
            if (m == null) return;
            Vector2 mp = m.position.ReadValue();
            if (m.leftButton.wasPressedThisFrame && optMainPage && optMainPage.gameObject.activeInHierarchy)   // the sliders are on the MAIN tab only, and never under the CONTROLS page
                foreach (var s in sliders)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(s.track, mp, null, out var lp);
                    var r = s.track.rect;
                    if (lp.x > r.xMin - 10 && lp.x < r.xMax + 10 && lp.y > r.yMin - 12 && lp.y < r.yMax + 12) { dragging = s; break; }
                }
            if (dragging != null)
            {
                if (!m.leftButton.isPressed) { PlayerPrefs.SetFloat(dragging.key, dragging.v); OzSoundV3.SliderPreview(dragging.bus == "Music" ? null : dragging.bus); dragging = null; return; }   // sound_v3
                RectTransformUtility.ScreenPointToLocalPointInRectangle(dragging.track, mp, null, out var lp);
                var r = dragging.track.rect;
                float v = Mathf.Clamp01((lp.x - r.xMin) / Mathf.Max(1f, r.width));
                if (!Mathf.Approximately(v, dragging.v)) { if (Mathf.FloorToInt(v * 10) != Mathf.FloorToInt(dragging.v * 10)) OzSfx.Ui("ui_slider_tick"); dragging.v = v; dragging.apply(v); ShowSlider(dragging); }
            }
        }

        int scaleShown = -1;
        void RefreshValues()
        {
            for (int i = 0; i < speedBtns.Count; i++)
            {
                bool on = Mathf.Abs(game.gameSpeed - Speeds[i]) < 0.01f;
                if (speedBtns[i].Selected != on) { speedBtns[i].Selected = on; speedBtns[i].Refresh(); }
            }
            foreach (var t in toggles)
            {
                if (t.label == OzForkOptions.ChatLabel) { t.value.Set(OzForkOptions.ChatName); continue; }
                if (t.label == OzMapReadabilityOptions.BorderLabel) { t.value.Set(OzMapReadabilityOptions.BorderName); continue; }
                if (t.label == OzMapReadabilityOptions.RadarSweepLabel) { t.value.Set(OzText.Tab($"◀  {OzMapReadabilityOptions.RadarSweepName}  ▶")); continue; }
                if (t.label == OzBehaviorLibrary.TanksLabel) { t.value.Set(OzBehaviorLibrary.SelectedName(OpenZ.Sim.ZBehaviorFamily.Tanks)); continue; }
                if (t.label == OzBehaviorLibrary.RobotsLabel) { t.value.Set(OzBehaviorLibrary.SelectedName(OpenZ.Sim.ZBehaviorFamily.Robots)); continue; }
                if (t.label == OzMapReadabilityOptions.FlagLabel) { t.value.Set(OzText.Tab("◀  " + OzMapReadabilityOptions.FlagName + "  ▶")); continue; }
                if (t.label == OzHudLayout.Label) { t.value.Set("DRAG THE PANELS  ▶"); continue; }
                if (t.label == NightShadows.Label) { t.value.Set(OzText.Tab($"◀  {NightShadows.Name}  ▶")); continue; }
                if (t.label == OzTouch.Label) { t.value.Set(OzText.Tab($"◀  {OzTouch.ModeName}  ▶")); continue; }
                if (t.label == OzTrackpadScroll.Label) { t.value.Set(OzText.Tab($"◀  {OzTrackpadScroll.Name}  ▶")); continue; }   // TRACKPAD SCROLL: ZOOM / MOVE MAP
                if (t.label == OzShuttleOrdersOption.Label) { t.value.Set(OzText.Tab($"◀  {OzShuttleOrdersOption.Name}  ▶")); continue; }   // K256g SHUTTLE ORDERS: LANDABLE SPOTS ONLY / ANYWHERE
                if (t.label == OzQuality.Label) { t.value.Set(OzText.Tab($"◀  {OzQuality.Name}  ▶")); continue; }
                if (t.label == OzFogLookOption.Label) { t.value.Set(OzText.Tab($"◀  {OzFogLookOption.Name}  ▶")); continue; }   // 1.4 FOG LOOK
                if (t.label == OzFpsCap.Label) { t.value.Set(OzText.Tab($"◀  {OzFpsCap.Name}  ▶")); continue; }
                if (t.label == OzQuality.FpsLabel) { t.value.Set(OzText.Tab($"◀  {OzQuality.FpsName}  ▶")); continue; }   // 1.3 item 25: OFF / ON / DETAILED
                if (t.label == OzSunSteps.Label) { t.value.Set(OzText.Tab($"◀  {OzSunSteps.Name}  ▶")); continue; }   // v1.3 item 25
                if (t.label == OzEndingDebris.Label) { t.value.Set(OzText.Tab($"◀  {OzEndingDebris.Name}  ▶")); continue; }
                if (t.label == OzGfxApi.Label) { t.value.Set(OzText.Tab($"◀  {OzGfxApi.Name}  ▶")); continue; }
                if (t.label == OzLightRows.TreesLabel) { t.value.Set(OzText.Tab($"◀  {OzLightRows.TreesName}  ▶")); continue; }   // 1.3 item 40
                if (t.label == OzLightRows.WrecksLabel) { t.value.Set(OzText.Tab($"◀  {OzLightRows.WrecksName}  ▶")); continue; }
                if (t.label == OzLowPicture.Label) { t.value.Set(OzText.Tab($"◀  {OzLowPicture.Name}  ▶")); continue; }   // 1.5 issue 8: 3D PICTURE
                if (t.label == OzOverlapOption.Label) { t.value.Set(OzText.Tab($"◀  {OzOverlapOption.Name}  ▶")); continue; }   // FRIENDLY UNITS OVERLAP: NO / YES
                if (t.label == OzVehicleSizeOption.Label) { t.value.Set(OzText.Tab($"◀  {OzVehicleSizeOption.Name}  ▶")); continue; }   // VEHICLE SIZE: 60 to 100 %
                if (t.label == OzRobotSizeOption.Label) { t.value.Set(OzText.Tab($"◀  {OzRobotSizeOption.Name}  ▶")); continue; }   // 1.5 R3.25 ROBOT SIZE: 60 to 100 %
                if (t.label == OzRangeRingsOption.Label) { t.value.Set(OzText.Tab($"◀  {OzRangeRingsOption.Name}  ▶")); continue; }   // 1.4 RANGE RINGS: FADE / ALWAYS
                if (t.label == OzForceBarOption.Label) { t.value.Set(OzText.Tab($"◀  {OzForceBarOption.MatchName(OpenZ.Multiplayer.DuelRuntime.Active)}  ▶")); continue; }
                if (CamRowValue(t)) continue;                                                              // 1.4: MIDDLE MOUSE and SCROLL SPEED (UI/OpenZPauseMenu.CamControls.cs)
                if (t.label == OzSettings.ResLabel) { t.value.Set(OzText.Tab(OzSettings.ResolutionName)); continue; }   // 1.5 issue 8: the value alone, no arrows (the mouse button sets the way: OzSettings.ResolutionClick)
                if (t.chatter && t.robots) { t.value.Set(OzText.Tab($"◀  {AudioDirector.RobotChatter.Names[AudioDirector.RobotChatter.Level]}  ▶")); continue; }
                if (t.chatter) { t.value.Set(OzText.Tab($"◀  {AudioDirector.Chatter.Names[AudioDirector.Chatter.Level]}  ▶")); continue; }
                if (t.scale)
                {
                    int pct = Mathf.RoundToInt(OzText.UiScale * 100);                                        // the string only when the scale changes
                    if (pct != scaleShown) { scaleShown = pct; t.value.Set(OzText.Tab($"◀  {pct}%  ▶")); }
                    continue;
                }
                bool on = t.get();
                t.state.Set(on ? "ON" : "OFF"); t.state.color = on ? (Color)OzUi.Amber : (Color)OzUi.Muted;
                t.knob.color = on ? (Color)OzUi.Amber : (Color)OzUi.GM500;
                t.knob.rectTransform.anchoredPosition = new Vector2(OzLayout.PauseW - 2 * OzLayout.OptX - 16 - 48 + (on ? 24 : 2), t.knob.rectTransform.anchoredPosition.y);
            }
        }

        // ------------------------------------------------------------------ settings applied at start and on change
        void ApplySaved()
        {
            foreach (var s in sliders) if (PlayerPrefs.HasKey(s.key)) s.apply(s.v);
            ApplyEdgeScroll();
            audioRetryUntil = Time.unscaledTime + 12f;
        }
        float audioRetryUntil, nextAudioRetry;

        float edgeDefault = -1f;
        void ApplyEdgeScroll()
        {
            if (!hud.rts) return;
            if (edgeDefault < 0f) edgeDefault = hud.rts.edgePx > 0 ? hud.rts.edgePx : 6f;
            hud.rts.edgePx = PlayerPrefs.GetInt("oz_edge_scroll", 1) == 1 ? edgeDefault : 0f;
        }

        static FieldInfo shakeField; static bool shakeLooked; static readonly object ZeroF = 0f;
        /// <summary>SCREEN SHAKE off: RtsCamera has no switch, so its private shake amount is cleared every frame (this
        /// component runs before the camera).</summary>
        void ApplyShake()
        {
            if (PlayerPrefs.GetInt("oz_shake", 1) == 1 && !OzMotion.Reduced || !hud.rts) return;
            if (!shakeLooked) { shakeLooked = true; shakeField = typeof(RtsCamera).GetField("shake", BindingFlags.NonPublic | BindingFlags.Instance); }
            if (shakeField != null && shakeField.FieldType == typeof(float)) shakeField.SetValue(hud.rts, ZeroF);   // cached box: no garbage per frame
        }

        void OnDestroy() { post?.Destroy(); if (IsOpen) Time.timeScale = prevScale > 0f ? prevScale : 1f; }
    }

    /// <summary>Volume options onto the audio track's AudioDirector buses (by reflection: its bus table is private and the
    /// </summary>
    public static class OzAudio
    {
        static FieldInfo busField; static bool looked; static UnityEngine.Object director; static Type dirType;
        static readonly Dictionary<string, float> baseDb = new Dictionary<string, float>();
        static readonly Dictionary<string, float> wanted = new Dictionary<string, float>();

        /// <summary>EFFECTS slider scale: its middle (50 %) plays the designed mix, its top is EffectsTopDb over it.</summary>
        public const float EffectsTopDb = 6.02f;
        /// <summary>Linear gain of an EFFECTS slider position: 0.5 -> 1, 1 -> 2.</summary>
        public static float EffectsLin(float v) => 2f * Mathf.Clamp01(v);

        /// <summary>Once per install: a position saved on the old scale (100 % = the designed mix) is halved, a missing one starts at 50 %.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureEffectsScale()
        {
            if (PlayerPrefs.GetInt("oz_vol_effects_scale", 1) >= 2) return;
            PlayerPrefs.SetFloat("oz_vol_effects", PlayerPrefs.HasKey("oz_vol_effects") ? 0.5f * PlayerPrefs.GetFloat("oz_vol_effects", 1f) : 0.5f);
            PlayerPrefs.SetInt("oz_vol_effects_scale", 2);
        }

        public static void Set(string bus, float v)
        {
            wanted[bus] = Mathf.Clamp01(v);
            Apply();
        }

        public static void Apply()
        {
            if (!looked)
            {
                looked = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("OpenZ.AudioDirector"); if (t == null) continue;
                    busField = t.GetField("busDb", BindingFlags.NonPublic | BindingFlags.Instance);
                    dirType = t;
                    break;
                }
            }
            if (!director && dirType != null) director = UnityEngine.Object.FindFirstObjectByType(dirType);
            if (busField != null && director && busField.GetValue(director) is Dictionary<string, float> db)
            {
                var loaded = director.GetType().GetProperty("Loaded", BindingFlags.Public | BindingFlags.Instance);
                if (loaded != null && !(bool)loaded.GetValue(director)) return;                              // bus table not read yet: retried later
                foreach (var kv in wanted)
                {
                    foreach (var bus in kv.Key == "SFX" ? new[] { "SFX", "Ambience" } : kv.Key == "Announcer" ? new[] { "Voice/Announcer", "Voice/Commander" } : kv.Key == "Robots" ? new[] { "Voice/Robots" } : new[] { kv.Key })
                    {
                        if (!baseDb.ContainsKey(bus)) baseDb[bus] = db.TryGetValue(bus, out var b0) ? b0 : 0f;
                        db[bus] = baseDb[bus] + (kv.Value <= 0.001f ? -80f : 20f * Mathf.Log10(kv.Value) + (kv.Key == "SFX" ? EffectsTopDb : 0f));
                    }
                }
                return;
            }
            if (CombatAudio.I && wanted.TryGetValue("SFX", out var fx)) CombatAudio.I.volume = 0.8f * EffectsLin(fx) / 0.85f;
        }
    }
}
