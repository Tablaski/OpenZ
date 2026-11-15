using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 camera controls (OPTIONS, GAME tab, main menu and pause menu; a player of 1.3 found the scroll too slow, wanted to scroll
    /// with the right button and did not want to give up the free camera for a middle-button scroll). Three rows, saved and live:
    ///  - MIDDLE MOUSE (the row of FREE CAMERA, in its place under EDGE SCROLL; label OzFreeCam.Label): TURN VIEW (default, the
    ///    middle button turns and tilts the view, as FREE CAMERA on) / DRAG MAP (the middle button moves the map, the view stays
    ///    fixed, as FREE CAMERA off) / DRAG MAP, ALT TURNS VIEW (the middle button moves the map; held with Alt it turns and tilts
    ///    the view, which then stays turned). Stored in the old switch oz_free_camera (TURN VIEW and the Alt way: 1, DRAG MAP: 0)
    ///    plus oz_middle_alt (1 = the Alt way), so a saved FREE CAMERA carries over: off reads DRAG MAP, on (or never set) TURN VIEW.
    ///  - SCROLL SPEED: 50 to 300 % in steps of 10 (oz_scroll_speed, default 100 % = the speed as before, x1 exactly). It scales the
    ///    keys, the screen edge, the middle drag and the right drag (View/RtsCamera.cs FeelStep and MiddleDrag). A slider with step
    ///    arrows (UI/OzScrollSpeedSlider.cs). Touch, gamepad and the trackpad's MOVE MAP keep their own ground grab.
    ///  - RIGHT-DRAG SCROLL: OFF (default) / ON (oz_right_drag_scroll). ON: a right press on the map that moves more than a few pixels
    ///    moves the map like the middle drag and gives no order on release; a right click without that travel gives the order as
    ///    before (View/OzRightDrag.cs). Also applies in the Unit editor; never starts over the HUD.
    /// View and input only: the sim never reads these.
    /// </summary>
    public static class OzCamControls
    {
        // ------------------------------------------------------------------ SCROLL SPEED
        public const string SpeedLabel = "SCROLL SPEED", SpeedPref = "oz_scroll_speed";
        public const int SpeedMin = 50, SpeedMax = 300, SpeedStepPct = 10, SpeedDefault = 100;
        public const string SpeedTip = "SCROLL SPEED|How fast the map scrolls with the keys, the screen edge and a mouse drag, 50 to 300 %. 100 % (default) is the standard speed; SHIFT and holding still make it faster. Drag the bar or click the arrows.|";

        static int speed = -1;
        /// <summary>The saved scroll speed in percent (a saved value off the steps reads as the default).</summary>
        public static int ScrollPercent
        {
            get { if (speed < 0) speed = SpeedValid(PlayerPrefs.GetInt(SpeedPref, SpeedDefault)); return speed; }
            set => SetSpeed(value, true);
        }
        /// <summary>The value a saved number reads as: on the 10 % steps inside 50..300, else the default.</summary>
        public static int SpeedValid(int p) => p >= SpeedMin && p <= SpeedMax && p % SpeedStepPct == 0 ? p : SpeedDefault;
        /// <summary>A wanted percent put on the nearest step inside the range.</summary>
        public static int SpeedSnap(int p) => Mathf.Clamp(Mathf.RoundToInt(p / (float)SpeedStepPct) * SpeedStepPct, SpeedMin, SpeedMax);
        /// <summary>Set the speed (snapped). save = written to disk now and logged (a slider drag saves once, on release).</summary>
        public static void SetSpeed(int pct, bool save)
        {
            int v = SpeedSnap(pct);
            speed = v; PlayerPrefs.SetInt(SpeedPref, v);
            if (save) { PlayerPrefs.Save(); Debug.Log($"[OpenZ Options] scroll speed: {v} % ({SpeedPref} {v}, saved, live){(v == SpeedDefault ? " (the default)" : "")}"); }
        }
        /// <summary>The camera's multiplier: exactly 1 at the default 100 % (and while the check switch HooksOff is on).</summary>
        public static float ScrollFactor => HooksOff ? 1f : ScrollFactorOf(ScrollPercent);
        public static float ScrollFactorOf(int pct) => pct == SpeedDefault ? 1f : pct / 100f;
        /// <summary>The row's value, as the other percent rows show it.</summary>
        public static string SpeedName => ScrollPercent + " %";
        /// <summary>One step of the row's arrows: -1 slower, +1 faster; the steps stop at both ends.</summary>
        public static void SpeedStep(int dir) { int to = SpeedSnap(ScrollPercent + (dir < 0 ? -SpeedStepPct : SpeedStepPct)); if (to != ScrollPercent) ScrollPercent = to; }
        /// <summary>The slider: a place along the bar (0 = left end, 1 = right end) as a percent on the steps.</summary>
        public static int SpeedFromFraction(float f) => SpeedSnap(Mathf.RoundToInt(SpeedMin + Mathf.Clamp01(f) * (SpeedMax - SpeedMin)));
        /// <summary>The slider: where a percent stands along the bar (0..1).</summary>
        public static float FractionOf(int pct) => Mathf.Clamp01((pct - SpeedMin) / (float)(SpeedMax - SpeedMin));

        // ------------------------------------------------------------------ RIGHT-DRAG SCROLL
        public const string RightLabel = "RIGHT-DRAG SCROLL", RightPref = "oz_right_drag_scroll";
        public const string RightTip = "RIGHT-DRAG SCROLL|On: hold the right mouse button and drag to move the map. A right click without a drag still gives the order (move, attack, capture, enter); a drag never gives one. Off (default): the right button only gives orders.|";
        static int right = -1;
        public static bool RightDrag
        {
            get { if (right < 0) right = PlayerPrefs.GetInt(RightPref, 0) == 1 ? 1 : 0; return right == 1; }
            set
            {
                right = value ? 1 : 0; PlayerPrefs.SetInt(RightPref, right); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] right-drag scroll: {(value ? "ON (right drag moves the map, a right click orders)" : "OFF (the right button only orders)")} ({RightPref} {right}, saved, live)");
            }
        }

        // ------------------------------------------------------------------ MIDDLE MOUSE
        public enum Middle { TurnView = 0, DragMap = 1, DragMapAltTurns = 2 }
        public const string AltPref = "oz_middle_alt";
        public static readonly string[] MiddleNames = { "TURN VIEW", "DRAG MAP", "DRAG MAP, ALT TURNS VIEW" };
        static int alt = -1;
        static bool AltSaved { get { if (alt < 0) alt = PlayerPrefs.GetInt(AltPref, 0) == 1 ? 1 : 0; return alt == 1; } }
        /// <summary>The row as saved: the old FREE CAMERA switch (OzFreeCam.On) and the Alt way.</summary>
        public static Middle MiddleMode
        {
            get => MiddleOf(OzFreeCam.On, AltSaved);
            set
            {
                bool free = value != Middle.DragMap; int a = value == Middle.DragMapAltTurns ? 1 : 0;
                alt = a; PlayerPrefs.SetInt(AltPref, a);
                if (OzFreeCam.On != free) OzFreeCam.On = free;                                                 // saves oz_free_camera (and logs it)
                PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] middle mouse: {MiddleNames[(int)value]} (oz_free_camera {(free ? 1 : 0)}, {AltPref} {a}, saved, live)");
            }
        }
        /// <summary>The mode the two saved values give (pure).</summary>
        public static Middle MiddleOf(bool freeCamera, bool altWay) => !freeCamera ? Middle.DragMap : altWay ? Middle.DragMapAltTurns : Middle.TurnView;
        public static string MiddleName => MiddleNames[(int)MiddleMode];
        /// <summary>One step of the row: +1 TURN VIEW, DRAG MAP, DRAG MAP ALT TURNS VIEW, round again; -1 back.</summary>
        public static void MiddleStep(int dir) => MiddleMode = (Middle)(((int)MiddleMode + (dir < 0 ? 2 : 1)) % 3);
        /// <summary>The middle button turns the view (when the free look allows it) instead of moving the map. Every mode but the Alt
        /// way: yes, as before (FREE CAMERA off still moves the map, as RtsCamera.FreeTurn refuses then). The Alt way: only with Alt
        /// held; the testing overlay and the Unit editor's playground keep their middle-button turn in every mode, as before.</summary>
        public static bool MiddleTurnsFor(Middle mode, bool altHeld, bool overlay, bool unitEditor) => mode != Middle.DragMapAltTurns || altHeld || overlay || unitEditor;

        // ------------------------------------------------------------------ the CONTROLS tables (OzFreeCamRowText, OzRightDragRowText)
        /// <summary>The camera row with the Alt way: the full table (main menu) and the short one (pause menu, help overlay).</summary>
        public const string FullRowAlt = "Zoom \u00B7 middle drag flies \u00B7 Alt + middle drag turns", CompactRowAlt = "Scroll \u00B7 zoom \u00B7 Alt + middle drag turns";
        /// <summary>The camera row's text for a MIDDLE MOUSE mode (0 TURN VIEW, 1 DRAG MAP, 2 the Alt way): offText = the row as built.</summary>
        public static string CameraRowText(string offText, string onText, int mode)
            => mode == (int)Middle.TurnView ? onText : mode == (int)Middle.DragMapAltTurns ? (offText == OzFreeCam.FullRowOff ? FullRowAlt : CompactRowAlt) : offText;
        /// <summary>The orders rows of the CONTROLS tables as built, and what they read while RIGHT-DRAG SCROLL is on.</summary>
        public const string FullOrders = "Move, attack, capture, enter, repair", CompactOrders = "Order: move, attack, capture, enter";
        public const string FullOrdersOn = "Move, attack, capture, enter \u00B7 right drag moves the map", CompactOrdersOn = "Order \u00B7 drag moves the map";
        /// <summary>The text an orders row shows (null: the row is no orders row).</summary>
        public static string OrdersRowText(string asBuilt, bool rightDrag)
            => asBuilt == FullOrders ? (rightDrag ? FullOrdersOn : FullOrders) : asBuilt == CompactOrders ? (rightDrag ? CompactOrdersOn : CompactOrders) : null;

        // ------------------------------------------------------------------ checks
        /// <summary>Check switch only (default off, never saved): every hook of this kit answers as the game did before it (factor 1,
        /// the middle button as FREE CAMERA, no right drag). The counted check compares the camera path with it on and off.</summary>
        public static bool HooksOff;
        /// <summary>Forget the read values (a check changed the saved keys behind them).</summary>
        public static void Reload() { speed = -1; right = -1; alt = -1; }

        /// <summary>The right button's gesture, pure (View/OzRightDrag.cs feeds it the mouse once a frame): a press that may scroll is
        /// armed; it scrolls once the pointer is farther than the threshold from where it went down; its release is then a drag
        /// release (no order), else a click (the order as before). A press that may not scroll stays a click whatever it travels.</summary>
        public struct RightGesture
        {
            public bool Armed, Scrolling, ReleaseWasDrag; public float X0, Y0;
            public const int None = 0, Started = 1, DragReleased = 2, ClickReleased = 4;
            /// <summary>One frame: pressedNow / held / releasedNow of the right button, the pointer, whether a new press may scroll,
            /// the threshold in pixels. Returns the frame's events (flags).</summary>
            public int Advance(bool pressedNow, bool held, bool releasedNow, float x, float y, bool canStart, float threshold)
            {
                int ev = None; ReleaseWasDrag = false;
                if (pressedNow) { Armed = canStart; Scrolling = false; X0 = x; Y0 = y; }
                if (Armed && !Scrolling && (held || releasedNow))
                {
                    float dx = x - X0, dy = y - Y0;
                    if (dx * dx + dy * dy > threshold * threshold) { Scrolling = true; ev |= Started; }
                }
                if (releasedNow) { if (Scrolling) { ReleaseWasDrag = true; ev |= DragReleased; } else ev |= ClickReleased; }
                if (!held) { Armed = false; Scrolling = false; }
                return ev;
            }
        }
    }
}
