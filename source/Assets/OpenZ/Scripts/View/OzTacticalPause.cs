// OpenZ (GPL-3.0)
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// View / input side only: no Sim file, no rule, no ZGame edit.
    /// <list type="bullet">
    /// <item>OPTIONS row <see cref="Label"/> (GAME tab, last row), saved in <see cref="PrefKey"/>, default OFF, live. OFF = everything exactly
    /// as before (Space = GO TO THE LAST 5 MESSAGES).</item>
    /// <item>ON: the rebindable key <see cref="OzAction.TacticalPause"/> (default SPACE; no Ctrl / Alt) freezes and unfreezes time. While it
    /// sits on the LAST MESSAGE key, that key freezes and the last-message jump stays on the clickable message toasts (their key cap is
    /// hidden, their tooltip says "Click"). No conflict is shown for the pair, and none at all while the option is off.</item>
    /// <item>Freeze: the time scale eases from where it is to 0 over <see cref="EaseSec"/> s (SmoothStep), so the battle, the effects and
    /// the rule clock (ZGame feeds the scaled frame time into its whole 10 ms ticks, as always) slow down smoothly; at 0 the game's own pause
    /// flag is set: from then on the rules stand exactly as in the pause menu's pause (no tick; every view that waits on the pause waits;
    /// the world sounds hold, the music is muffled). The camera, scrolling, zoom, selection, orders, production, rally points and the
    /// minimap keep working (they run on unscaled time, as in the Unit editor's PAUSE, which uses the same flag and time scale 0). Orders
    /// and production changes go into the world at once and play out when time runs again, exactly as the Unit editor's pause takes them.</item>
    /// <item>Unfreeze: the pause flag off, the time scale eases back to where it was over the same 0.3 s.</item>
    /// <item>Sounds: <see cref="SndFreeze"/> / <see cref="SndUnfreeze"/> (4 takes each from the owner's own slow-mo sounds, the
    /// sound table picks at random, never the same take twice in a row); no ui_pause / ui_resume and no announcer line for these two
    /// flips (AudioDirector asks <see cref="QuietFlip"/>). Opening or closing the pause menu over frozen time plays ui_pause / ui_resume.</item>
    /// <item>Look: a soft dark vignette with a little less colour (a URP post volume like the pause menu's, under it) eases in with the
    /// freeze and out with the unfreeze; the HUD is not touched (it is drawn over the post effects); no text, no PAUSED word on the clock.</item>
    /// <item>Esc opens the pause menu as always; closing it returns to the frozen time. The - / + game speed keys keep working while frozen
    /// (the new speed applies when time runs again). Not in the Unit editor (it has its own PLAY / PAUSE), not before the HUD is up, not in
    /// the ending, not while a menu, a question or the arrange mode is up.</item>
    /// </list>
    /// </summary>
    public sealed partial class OzTacticalPause : MonoBehaviour   // 1.5 review R9 (kit review_r8_r12): partial (View/OzTacticalPause.Review.cs)
    {
        // ------------------------------------------------------------------ the option
        public const string Label = "TACTICAL PAUSE", PrefKey = "oz_tactical_pause_on";
        public const string Tip = "TACTICAL PAUSE|On (default): SPACE freezes time and SPACE again lets it run on. While time stands still you can still move and zoom the camera, select units, give orders and change production. Off: SPACE goes to the last message, as before. The key can be changed in CONTROLS.|";
        static int on = -1;
        /// <summary>The OPTIONS switch (oz_tactical_pause_on, default ON since the owner's 10 Nov late note), live.</summary>
        public static bool On
        {
            get { if (on < 0) on = PlayerPrefs.GetInt(PrefKey, 1) == 1 ? 1 : 0; return on == 1; }
            set
            {
                on = value ? 1 : 0; PlayerPrefs.SetInt(PrefKey, on); PlayerPrefs.Save(); OzBindings.Reload();   // Reload: the CONTROLS caps and conflict marks look again
                Debug.Log($"[OpenZ Options] tactical pause: {(value ? "ON (" + OzBindings.Cap(OzAction.TacticalPause) + " freezes / unfreezes time)" : "OFF (" + OzBindings.Cap(OzAction.LastMessage) + " = the last message, as before)")} ({PrefKey} {on}, saved, live)");
            }
        }

        // ------------------------------------------------------------------ numbers (one place to tune)
        /// <summary>Seconds of the ease into and out of the freeze.</summary>
        public static float EaseSec = 0.3f;
        /// <summary>The vignette volume: intensity, saturation, exposure (the pause menu's: 0.4, -55, -1.2).</summary>
        public static float VignetteIntensity = 0.42f, VignetteSaturation = -30f, VignetteExposure = -0.18f;
        public const string SndFreeze = "ui_time_freeze", SndUnfreeze = "ui_time_unfreeze";
        const int VolumePriority = 54;                                                                   // the pause menu's volume is 55: it wins while it is up

        // ------------------------------------------------------------------ counts (Report)
        public static int Freezes, Unfreezes, FreezeSounds, UnfreezeSounds, Refused, OutsideResumes, Aborts, MenuSounds;
        public static string LastRefused = "-";
        public static float LastFreezeAt = -1f, LastFrozenAt = -1f, LastUnfreezeAt = -1f, LastRunningAt = -1f;

        enum Phase { Running, Freezing, Frozen, Thawing }
        static OzTacticalPause I;
        static int quietUntil = -10;
        Phase phase; float k, baseScale = 1f; OzPost post; bool menuBefore;

        // ------------------------------------------------------------------ reads for the hooks
        /// <summary>The time is frozen by this switch now and nothing else (pause menu, arrange mode, end screen) is in front of it.</summary>
        public static bool Frozen => I && I.phase == Phase.Frozen && !Held(ZGame.I, OpenZHud.I);
        /// <summary>Freezing, frozen or easing back.</summary>
        public static bool Busy => I && I.phase != Phase.Running;
        /// <summary>0 = time runs, 1 = frozen (the ease's own value).</summary>
        public static float Amount => I ? I.k : 0f;
        /// <summary>View/AudioDirector.cs: the pause flag flipped because of this switch (no ui_pause / ui_resume, no announcer line).</summary>
        public static bool QuietFlip => Time.frameCount <= quietUntil;
        /// <summary>UI/OpenZHud.cs: the switch is on and its key is the LAST MESSAGE key: that key freezes, the last message is a click.</summary>
        public static bool TakesLastMessageKey => On && !OzUeHooks.Active && !Multiplayer.DuelRuntime.Active &&
            OzBindings.KeyOf(OzAction.TacticalPause) == OzBindings.KeyOf(OzAction.LastMessage);
        /// <summary>View/OzBindings.cs SharedKey: never a conflict while the switch is off; with LAST MESSAGE it shares the key on purpose.</summary>
        public static bool NoClash(OzAction a, OzAction b)
        {
            bool ta = a == OzAction.TacticalPause, tb = b == OzAction.TacticalPause;
            if (!ta && !tb) return false;
            if (!On || Multiplayer.DuelRuntime.Active) return true;
            return (ta && b == OzAction.LastMessage) || (tb && a == OzAction.LastMessage);
        }
        /// <summary>UI/OpenZHud.cs: the toast tooltip's body and key ("|Click or press SPACE to go there.|SPACE", or "|Click to go there.|"
        /// while the switch has the key).</summary>
        public static string PressTip(string what)
        {
            if (TakesLastMessageKey) return "|Click " + what + "|";
            string cap = OzBindings.Cap(OzAction.LastMessage);
            return "|Click or press " + cap + " " + what + "|" + cap;
        }

        static bool Held(ZGame g, OpenZHud hud) => (hud && hud.Modal) || OzConfirm.Blocks;

        // ------------------------------------------------------------------ install (every scene with a game; the Unit editor's too, idle there)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; on = -1; quietUntil = -10; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (I) return;
            if (!ZGame.I && !FindFirstObjectByType<ZGame>()) return;                                      // game levels (and the Unit editor's playground, where it stays idle)
            I = new GameObject("OzTacticalPause").AddComponent<OzTacticalPause>();
        }

        void OnDestroy()
        {
            if (phase != Phase.Running && Time.timeScale <= 0f) Time.timeScale = 1f;                     // never a frozen clock left behind (a scene change resets it anyway)
            post?.Destroy(); post = null;
            if (I == this) I = null;
        }

        // ------------------------------------------------------------------ every frame
        void Update()
        {
            var g = ZGame.I; var hud = OpenZHud.I;
            if (!g || g.World == null || Multiplayer.DuelRuntime.Active) return;
            bool held = Held(g, hud), menu = hud && hud.Pause && hud.Pause.IsOpen;
            if (menu != menuBefore)
            {   // the pause menu over frozen time: the game's flag does not flip, so the menu's open / close sound is played here
                menuBefore = menu;
                if (phase == Phase.Frozen) { AudioDirector.UI(menu ? "ui_pause" : "ui_resume"); MenuSounds++; }
            }
            if (held) { Look(); return; }                                                                // the menu owns the clock and the flag meanwhile
            if (phase != Phase.Running && (g.World.GameOver || RtsCamera.InputLocked || OzUeHooks.Active)) { Abort(g, "the ending / game over / the Unit editor"); return; }
            if (phase != Phase.Running && !On) Unfreeze(g, "the option was switched off");
            if (On && KeyPressed())
            {
                if (phase == Phase.Running || phase == Phase.Thawing) TryFreeze(g, hud);
                else Unfreeze(g, "the key");
            }
            Step(g, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            Look();
        }

        void LateUpdate()
        {
            if (phase != Phase.Frozen) return;
            var g = ZGame.I; if (!g || Held(g, OpenZHud.I)) return;
            if (!g.paused)
            {   // something else let the game run (a check, a scene tool): follow it, never fight it
                phase = Phase.Running; k = 0f; Time.timeScale = baseScale; OutsideResumes++; LastRunningAt = Time.unscaledTime;
                Debug.Log("[OpenZ] tactical pause: the game was resumed from outside: time runs");
                return;
            }
            if (Time.timeScale != 0f) Time.timeScale = 0f;                                               // the pause menu's close puts back 1 when it found 0
        }

        static bool KeyPressed()
        {
            if (OzUeHooks.Active || OzControlsTable.Capturing || CaptureKit.Enabled) return false;
            var kb = Keyboard.current; if (kb == null) return false;
            if (!OzBindings.Pressed(OzAction.TacticalPause) || kb.ctrlKey.isPressed || kb.altKey.isPressed) return false;
            var es = EventSystem.current; var sel = es ? es.currentSelectedGameObject : null;
            if (sel && (sel.GetComponent<TMPro.TMP_InputField>() || sel.GetComponent<UnityEngine.UI.InputField>())) return false;   // a text field has the keyboard
            return true;
        }

        void TryFreeze(ZGame g, OpenZHud hud)
        {
            string why = null;
            if (!g.isActiveAndEnabled) why = "no running game";
            else if (!hud || !hud.isActiveAndEnabled || !hud.Canvas) why = "the HUD is not up yet (briefing)";
            else if (g.World.GameOver || RtsCamera.InputLocked) why = "the ending";
            else if (phase == Phase.Running && g.paused) why = "the game is paused by something else";
            else if (phase == Phase.Running && Time.timeScale < 0.99f) why = "time is already slowed by something else (time scale " + Time.timeScale.ToString("0.00") + ")";
            if (why != null) { Refused++; LastRefused = why; Debug.Log("[OpenZ] tactical pause: key ignored: " + why); return; }
            if (phase == Phase.Running) baseScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            phase = Phase.Freezing; Freezes++; LastFreezeAt = Time.unscaledTime;
            Play(SndFreeze, "ui_pause"); FreezeSounds++;
            Debug.Log($"[OpenZ] tactical pause: FREEZE (time {baseScale * (1f - Ease(k)):0.00} -> 0 over {EaseSec * (1f - k):0.00} s, speed x{g.gameSpeed:0.##})");
        }

        void Unfreeze(ZGame g, string why)
        {
            if (phase == Phase.Running || phase == Phase.Thawing) return;
            if (phase == Phase.Frozen && g.paused) SetPaused(g, false);
            phase = Phase.Thawing; Unfreezes++; LastUnfreezeAt = Time.unscaledTime;
            Play(SndUnfreeze, "ui_resume"); UnfreezeSounds++;
            Debug.Log($"[OpenZ] tactical pause: UNFREEZE ({why}; time 0 -> {baseScale:0.00} over {EaseSec * k:0.00} s)");
        }

        /// <summary>The ending or the Unit editor came while the time was held: everything back at once, no sound.</summary>
        void Abort(ZGame g, string why)
        {
            if (phase == Phase.Frozen && g.paused) SetPaused(g, false);
            phase = Phase.Running; k = 0f; Time.timeScale = baseScale; Aborts++;
            Debug.Log("[OpenZ] tactical pause: let go at once (" + why + ")");
            Look();
        }

        void Step(ZGame g, float dt)
        {
            float d = EaseSec > 0.001f ? dt / EaseSec : 1f;
            if (phase == Phase.Freezing)
            {
                k = Mathf.Min(1f, k + d);
                if (k >= 1f)
                {
                    phase = Phase.Frozen; LastFrozenAt = Time.unscaledTime;
                    Time.timeScale = 0f; SetPaused(g, true);                                              // the game's own pause from here: the rules stand as in the pause menu
                }
                else Time.timeScale = baseScale * (1f - Ease(k));
            }
            else if (phase == Phase.Thawing)
            {
                k = Mathf.Max(0f, k - d);
                if (k <= 0f) { phase = Phase.Running; Time.timeScale = baseScale; LastRunningAt = Time.unscaledTime; }
                else Time.timeScale = baseScale * (1f - Ease(k));
            }
        }

        static float Ease(float x) => Mathf.SmoothStep(0f, 1f, x);

        static void SetPaused(ZGame g, bool p) { quietUntil = Time.frameCount + 2; g.paused = p; }

        static void Play(string id, string fallback)
        {   // 1.5 review R9 (kit review_r8_r12): the takes on the sound director's own source, the unfreeze a little louder (View/AudioDirector.TimeFreeze.cs: why it went unheard)
            if (OwnSource && AudioDirector.PlayOwnUi(id, id == SndUnfreeze ? UnfreezeLiftDb : 0f)) return;
            AudioDirector.UI(AudioDirector.Has(id) ? id : fallback);
        }

        /// <summary>The vignette follows the ease (in with the freeze, out with the unfreeze).</summary>
        void Look()
        {
            float w = Ease(k);
            if (w <= 0.0001f) { if (post != null) post.Weight = 0f; return; }
            post ??= new OzPost("OZ_TacticalPause", VignetteSaturation, VignetteExposure, VignetteIntensity, VolumePriority);
            post.Weight = w;
        }

        /// <summary>Checks (the Unity pass, no keyboard in the Game view): the key's press, the same path (the option must be on).</summary>
        public static void TestPress()
        {
            var g = ZGame.I;
            if (!I || !g || g.World == null || !On) { Debug.Log("[OpenZ] tactical pause: TestPress ignored (no game, or the option is off)"); return; }
            if (Held(g, OpenZHud.I)) { Debug.Log("[OpenZ] tactical pause: TestPress ignored (a menu is up)"); return; }
            if (I.phase == Phase.Running || I.phase == Phase.Thawing) I.TryFreeze(g, OpenZHud.I); else I.Unfreeze(g, "a check");
        }

        /// <summary>One line for the checks.</summary>
        public static string Report() =>
            $"tactical pause: option {(On ? "ON" : "OFF")}, key {OzBindings.Cap(OzAction.TacticalPause)}{(TakesLastMessageKey ? " (= the last-message key: the toasts are clicks)" : "")}, " +
            $"phase {(I ? I.phase.ToString() : "-")}, amount {Amount:0.00}, time scale {Time.timeScale:0.00}, paused {(ZGame.I && ZGame.I.paused)}; freezes {Freezes} ({FreezeSounds} sounds), " +
            $"unfreezes {Unfreezes} ({UnfreezeSounds} sounds), menu sounds {MenuSounds}, refused {Refused} (last: {LastRefused}), outside resumes {OutsideResumes}, aborts {Aborts}; " +
            $"last freeze {LastFreezeAt:0.00} -> frozen {LastFrozenAt:0.00}, last unfreeze {LastUnfreezeAt:0.00} -> running {LastRunningAt:0.00} (unscaled s)";

        // ------------------------------------------------------------------ the CONTROLS tables (UI/OzControlsTable.cs, via OzFreeCam.ControlsRow)
        /// <summary>The game speed row of both tables, as built; while the switch is on it also shows the TACTICAL PAUSE key in front.</summary>
        public const string SpeedRow = "Game speed: slower / faster", SpeedRowOn = "Tactical pause · game speed";

        /// <summary>OzFreeCam.ControlsRow: the game speed row gets the live switch; every other row is left as it is. Returns the label.</summary>
        public static OzText ControlsRow(OzText label, string text)
        {
            if (label == null || label.rectTransform == null) return label;
            if (text != SpeedRow) return MessageRow(label, text);   // 1.5 kit player_reports_1111 P3: the LAST MESSAGE rows name the tactical pause while it has their key (View/OzTacticalPause.Rows.cs)
            var p = label.rectTransform.parent; if (!p) return label;
            var cap = p.Find("Key_" + OzAction.TacticalPause) as RectTransform; if (!cap) return label;
            int i = cap.GetSiblingIndex();
            var sep = i > 0 ? p.GetChild(i - 1) : null;                                                   // OzControlsTable.Caps draws right to left: the "·" just before the key
            if (sep && !sep.name.StartsWith("Sep")) sep = null;
            var sw = label.rectTransform.gameObject.AddComponent<RowText>();
            sw.Init(label, text, cap.gameObject, sep ? sep.gameObject : null);
            return label;
        }

        /// <summary>The game speed row's key cap and text follow the switch (checked when the table shows and every frame while it is up).</summary>
        [DisallowMultipleComponent]
        public sealed class RowText : MonoBehaviour
        {
            OzText label; string asBuilt; GameObject cap, sep; int shown = -1;
            public bool ShowsTacticalPause => shown == 1;
            internal void Init(OzText l, string text, GameObject c, GameObject s) { label = l; asBuilt = text; cap = c; sep = s; shown = -1; Refresh(); }
            void OnEnable() { shown = -1; Refresh(); }
            void Update() => Refresh();
            public void Refresh()
            {
                if (label == null) return;
                int want = On ? 1 : 0;
                if (want == shown) return;
                shown = want;
                if (cap) cap.SetActive(want == 1);
                if (sep) sep.SetActive(want == 1);
                label.Set(want == 1 ? SpeedRowOn : asBuilt);
            }
        }
    }
}
