using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>Child nodes of the editor's UI root, bottom to top (draw order).</summary>
    public enum UeLayer { Field = 0, Panels = 1, Sheets = 2, Popovers = 3, Dialogs = 4, Drag = 5, Tips = 6 }
    /// <summary>A side of the canvas (floating panels report how much of it they cover: OzUeInsets).</summary>
    public enum UeSide { Left = 0, Right = 1, Top = 2, Bottom = 3 }
    /// <summary>What a palette id or a field object is.</summary>
    public enum UeKind { None = 0, Robot = 1, Vehicle = 2, Gun = 3, Hero = 4, Shuttle = 5, Flag = 6, Obstacle = 7, Grenades = 8, Nuke = 9, Platform = 10 }   // 1.3 item 2: Nuke = the NUKE extra (View/OzUeNuke.cs)
    /// <summary>What the stats box shows: nothing picked (list), a type (tile / row), a unit on the field, a flag, an obstacle, empty hardware,
    /// a grenade crate.</summary>
    public enum UeInspect { None = 0, Type = 1, Unit = 2, Flag = 3, Obstacle = 4, EmptyHardware = 5, Crate = 6, Platform = 7 }

    /// <summary>Result of one drop (x1 / x3 / x5).</summary>
    public struct UeDrop
    {
        public bool ok;                 // at least one group was made
        public string why;              // toast text (caps) when refused or partly refused, else null
        public int groups;              // squads / vehicles / guns / shuttles / flags / obstacles made
        public List<ZObject> made;      // their leaders / objects
    }

    /// <summary>
    /// built by the editor menu OpenZ/V1.2/Unit Editor/1 Build Playground Scene, Editor/OzUePlaygroundMenu.cs), serialized on its ZGame
    /// object. The playground is a normal ZWorld on an empty flat map (Resources/OpenZ/Maps/UE_grid.json, 160 x 100 tiles) where the
    /// player orders BOTH teams (View/PlayerInput.BothSides.cs) and the units fight with their own unit AI: no CPU commander (no ZBot at
    /// all), no game end, flags and the holo obstacle only where the player places them (Sim/ZWorld.Playground.cs).
    /// Awake (after ZGame's, before any sim tick): the world's bots cleared, the game end off, the 1 s flag hold on, the calm start
    /// (DV69) held off through OzUeStatics (both teams count as human: it would freeze every self-started action for 20 s), the speed
    /// x1, then the modules' root components (P1 field, P2 controls, P3 stats, P4 shell). Start: the fixed day grade and the main menu's
    /// music playlist instead of the level's music (Q11, this scene's AudioDirector only). The first frame the HUD has its Root, the
    /// editor's UI node OZ_UnitEditor with its seven layers is made on the HUD canvas and every module builds into it (each in its own
    /// try / catch: an exception is logged as an error, the others still build), then UiReady. Every LateUpdate the HUD hides its match
    /// panels and keeps the pixel floor (UI/OpenZHud.UnitEditor.cs). OnDestroy (leaving): every held static restored, every static
    /// event of P1's facades cleared.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class OzUnitEditor : MonoBehaviour
    {
        public const string SceneName = "UE_Playground";
        public const string LevelId = "UE";
        /// <summary>Log prefix of every editor line.</summary>
        public const string Tag = "[OpenZ UnitEditor]";
        /// <summary>Seconds a flag holds after it changed its owner in the playground (spec E111; 0 in every other world).</summary>
        public const double FlagHoldSeconds = 1.0;
        /// <summary>The smallest screen pixels per canvas unit times the UI scale (Q1: 1366 x 768 shows 12 u text at 10 px).</summary>
        public const float PixelFloor = 0.85f;
        /// <summary>The playground's fixed time of day (DayNight's clock): just after the morning warmth fades (0.05), near the noon key. The
        /// scene's sun is built in this look (Editor/OzUePlaygroundMenu.cs) and Awake grades the playground with it (no clock there).</summary>
        public const float DayTime = 0.06f;

        public static OzUnitEditor I { get; private set; }
        public static bool Active => I != null;
        public ZGame Game { get; private set; }
        public ZWorld World => Game != null ? Game.World : null;
        public PlayerInput Input { get; private set; }
        public RtsCamera Rts { get; private set; }
        public OpenZHud Hud => OpenZHud.I;
        public RectTransform UiRoot { get; private set; }
        public static bool UiBuilt => I != null && I.UiRoot != null;
        /// <summary>Raised once, after every module's Build ran.</summary>
        public static event Action UiReady;
        /// <summary>Module builds that threw (the Play proof counts them: must be 0).</summary>
        public static int BuildErrors { get; private set; }

        readonly RectTransform[] layers = new RectTransform[7];
        public static RectTransform Layer(UeLayer layer) => I != null && I.UiRoot != null ? I.layers[(int)layer] : null;

        /// <summary>H: every editor panel and rail hidden (the top bar stays).</summary>
        public static bool PanelsHidden { get; private set; }
        public static void SetPanelsHidden(bool hidden)
        {
            if (PanelsHidden == hidden) return;
            PanelsHidden = hidden;
            Debug.Log($"{Tag} panels {(hidden ? "hidden (H)" : "shown")}");
            PanelsHiddenChanged?.Invoke(hidden);
            OzUeInsets.NotifyChanged();
        }
        public static event Action<bool> PanelsHiddenChanged;

        /// <summary>The HUD canvas size in units (hud.CanvasSize; with the 0.85 pixel floor on short screens).</summary>
        public static Vector2 CanvasSize
        {
            get
            {
                var h = OpenZHud.I;
                if (h && h.Root && h.Root.rect.width > 1f && h.Root.rect.height > 1f) return h.Root.rect.size;
                return CanvasFor(Screen.width, Screen.height, OzText.UiScale);
            }
        }
        /// <summary>Screen pixels per canvas unit.</summary>
        public static float PxPerUnit => Screen.height > 0 ? Screen.height / Mathf.Max(1f, CanvasSize.y) : 1f;

        /// <summary>The editor's canvas for a screen of w x h pixels at a UI scale (pure: the layout checks use it at every size of spec
        /// 2.1). Pixels per unit = max(h / 1080, 0.85) x the UI scale (the HUD's own rule, with the pixel floor).</summary>
        public static Vector2 CanvasFor(float w, float h, float uiScale)
        {
            float s = Mathf.Clamp(uiScale, OzText.ScaleMin, OzText.ScaleMax);
            float k = Mathf.Max(h / OzLayout.RefH, PixelFloor) * s;
            return k > 0f ? new Vector2(w / k, h / k) : new Vector2(OzLayout.RefW, OzLayout.RefH);
        }

        // ------------------------------------------------------------------------------------------------ life cycle
        void Awake()
        {
            if (I && I != this) { Debug.LogError($"{Tag} a second editor root in the scene: this one is removed"); Destroy(this); return; }
            Game = GetComponent<ZGame>(); if (!Game) Game = ZGame.I;
            Input = GetComponent<PlayerInput>(); if (!Input) Input = FindFirstObjectByType<PlayerInput>();
            Rts = FindFirstObjectByType<RtsCamera>();
            if (!Game || Game.World == null)
            {
                Debug.LogError($"{Tag} no ZGame world on the playground's ZGame object: the editor stays off");
                enabled = false;
                return;
            }
            I = this;
            PanelsHidden = false; BuildErrors = 0;
            var w = Game.World;
            int bots = w.Bots.Count;
            w.Bots.Clear();
            w.NoGameEnd = true;
            w.FlagHoldSec = FlagHoldSeconds;                                                  // spec E111
            OzUeStatics.Hold("ZWorld.CalmStartDeviation", () => ZWorld.CalmStartDeviation, v => ZWorld.CalmStartDeviation = v, false);   // DV69 would hold both (human) teams 20 s
            Game.gameSpeed = 1f; Game.paused = false;                                         // Q6: x1
            WreckGroundRest.DeepenPlaygroundGround();                                          // 1.3 item 9: the 1 m ground box 12 m deep, its top kept at 0 (View/WreckGroundRest.cs)
            Debug.Log($"{Tag} playground: level {Game.level}, map {w.Map?.file} {w.Map?.width} x {w.Map?.height} tiles, bots removed {bots}, game end off, flag hold {FlagHoldSeconds:0.0} s, calm start off, input {(Input ? "found" : "MISSING")}, camera {(Rts ? "found" : "MISSING")}");
            if (!Input) Debug.LogError($"{Tag} no PlayerInput in the playground scene: picking and orders are off");
            Add<OzUeField>();                                                                 // P1: picks, inspect, hover, orders' hints, event toasts
            Add<OzUeControlsRoot>();                                                          // P2: dock, play bar, zoom, camera, floor
            Add<OzUeStatsRoot>();                                                             // P3: the STATS box, sets, table, balance, card
            Add<OzUeShellRoot>();                                                             // P4: top bar, dialogs, keys, placing, marks, files
            Add<OzBehaviorPanel>();
        }

        void Add<T>() where T : Component
        {
            try { if (!GetComponent<T>()) gameObject.AddComponent<T>(); }
            catch (Exception e) { BuildErrors++; Debug.LogError($"{Tag} {typeof(T).Name} could not start: {e}"); }
        }

        void Start()
        {
            // the day grade (the playground has no clock): NightGrade's time, its night amount and the shader globals could still hold
            // the last match's night; Start = the playground is the active scene, so the grade's own volume (weight 0 by day) lives in it
            NightGrade.Apply(RenderSettings.sun, DayTime);
            // the editor's UI lives on the HUD canvas: a HUD switched off for recordings (PlayerPrefs oz_hud 0) is made for the playground
            if (!OpenZHud.I && !FindFirstObjectByType<OpenZHud>()) { gameObject.AddComponent<OpenZHud>(); Debug.Log($"{Tag} the HUD was off (oz_hud): made for the editor's UI"); }
            MenuMusicOn();
        }

        void Update()
        {
            if (!musicSet) MenuMusicOn();
            if (UiRoot == null) TryBuildUi();
        }

        bool musicSet;
        /// <summary>Q11: the playground plays the main menu's music playlist. Every scene gets its own AudioDirector, so this switches only
        /// the playground's: its MusicCore (the level's calm / battle playlists) off, its own music voices then play the menu playlist
        /// (hook in View/AudioDirector.cs SwitchMusic: OzUeHooks.MenuMusic). The music option (oz_music) is respected and never written.</summary>
        void MenuMusicOn()
        {
            var ad = AudioDirector.I;
            if (musicSet || !ad || ad.gameObject.scene != gameObject.scene) return;
            musicSet = true;
            ad.musicV3 = false;
            Debug.Log($"{Tag} music: the main menu's playlist (this scene's MusicCore off: no level calm / battle music in the playground)");
        }

        void TryBuildUi()
        {
            var hud = OpenZHud.I;
            if (!hud || !hud.Root) return;
            hud.UeFrame();                                                                    // the pixel floor and the hidden match panels first: the modules lay out on the right canvas
            Canvas.ForceUpdateCanvases();
            UiRoot = OpenZ.Hud.Node("OZ_UnitEditor", hud.Root).Fill(); UiRoot.pivot = Vector2.zero;           // OpenZ.Hud = the HUD kit's builders (Hud here is the HUD component)
            for (int i = 0; i < layers.Length; i++) { var n = OpenZ.Hud.Node("UE_" + (UeLayer)i, UiRoot).Fill(); n.pivot = Vector2.zero; layers[i] = n; }
            Build<OzUeControlsRoot>("bottom controls", c => c.Build(this));
            Build<OzUeStatsRoot>("stats", c => c.Build(this));
            Build<OzUeShellRoot>("shell", c => c.Build(this));
            var cs = CanvasSize;
            Debug.Log($"{Tag} UI built on the HUD canvas {cs.x:0} x {cs.y:0} u ({PxPerUnit:0.###} px per u), {BuildErrors} module error(s)");
            try { UiReady?.Invoke(); }
            catch (Exception e) { BuildErrors++; Debug.LogError($"{Tag} a UiReady listener threw: {e}"); }
        }

        void Build<T>(string what, Action<T> build) where T : Component
        {
            var c = GetComponent<T>();
            if (!c) { BuildErrors++; Debug.LogError($"{Tag} the {what} module ({typeof(T).Name}) is missing: not built"); return; }
            try { build(c); }
            catch (Exception e) { BuildErrors++; Debug.LogError($"{Tag} the {what} module ({typeof(T).Name}) failed to build: {e}"); }
        }

        void LateUpdate()
        {
            var hud = OpenZHud.I;
            if (hud) hud.UeFrame();
        }

        void OnDestroy()
        {
            if (I != this) return;
            OzUeStatics.RestoreAll();
            UiReady = null; PanelsHiddenChanged = null; PanelsHidden = false;
            OzUeInsets.Reset(); OzUePlayground.Reset(); OzUePick.Reset(); OzUeObstacleViews.Reset(); OzUeCrateViews.Reset(); OzUeNuke.Reset(); OzUePlatformViews.Reset(); OzUeHooks.Reset();
            I = null;
            Debug.Log($"{Tag} left the playground: every held static restored");
        }

        // ------------------------------------------------------------------------------------------------ counted checks
        /// <summary>The editor's counted checks in edit mode or Play (menu OpenZ/V1.2/Unit Editor/2 Counted Check, the Play proof): P1's
        /// own (palette, names, formations, the drop rules on paper, insets at the six canvas sizes, team words, the grid file, the
        /// hologram shader and material, the held statics) and every other module's SelfCheck. One "PASS ..." or "FAIL ..." line each;
        /// returns the number of FAIL lines.</summary>
        public static int SelfCheck(StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            int fails = 0;
            fails += Run("P1 playground", log, () => OzUePlayground.SelfCheck(log));
            fails += Run("P1 insets", log, () => OzUeInsets.SelfCheck(log));
            fails += Run("P1 team words", log, () => OzUeUi.SelfCheck(log));
            fails += Run("P1 hologram", log, () => OzUeObstacleViews.SelfCheck(log));
            fails += Run("P2 bottom controls", log, () => OzUeControlsRoot.SelfCheck(log));
            fails += Run("P3 stats", log, () => OzUeStatsRoot.SelfCheck(log));
            fails += Run("P4 shell", log, () => OzUeShellRoot.SelfCheck(log));
            fails += Run("P4 dash walk", log, () => OzUeDash.SelfCheck(log));                              // the world marks' dash walk at every pixel scale (View/OzUeDash.cs)
            fails += Run("P4 drop preview ghost", log, () => OzUeGhost.SelfCheck(log));                    // the preview models' see-through team ghost (View/OzUeGhost.cs)
            fails += Run("P4 drop words", log, () => OzUeWords.SelfCheck(log));                            // the drop's words clear the models and the range ring (View/OzUeWords.cs)
            fails += Run("P2 layout grid", log, () => OzUeLayoutGrid.SelfCheck(log));                      // no panel over another at every design size x UI SCALE step (UI/OzUeLayoutGrid.cs)
            fails += Run("P3 opaque cards", log, () => OzUeSolid.SelfCheck(log));                          // the floating cards' opaque backing (UI/OzUeSolid.cs)
            fails += Run("P4 test round", log, () => OzUePlainChecks.SelfCheck(log));                      // the cap, RESET STATS, the leave question, the words, the loading picture (View/OzUePlainChecks.cs)
            return fails;
        }

        static int Run(string what, StringBuilder log, Func<int> check)
        {
            try { return check(); }
            catch (Exception e) { log.AppendLine($"FAIL {what}: the check threw {e.GetType().Name}: {e.Message}"); return 1; }
        }

        /// <summary>One counted line: "PASS what" or "FAIL what".</summary>
        public static int Line(StringBuilder log, bool ok, string what)
        {
            log.AppendLine((ok ? "PASS " : "FAIL ") + what);
            return ok ? 0 : 1;
        }

        /// <summary>The scene is the playground (the scene name; true while it loads, before any Awake).</summary>
        public static bool IsPlayground(Scene s) => s.IsValid() && s.name == SceneName;
    }
}
