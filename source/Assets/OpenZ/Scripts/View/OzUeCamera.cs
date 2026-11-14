using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs; owner 6k; spec 9.6, E030-E036, E214, E215): the Unit editor's camera on top of the game's RTS
    /// camera. The RTS camera keeps its own controls (W A S D, arrows, the wheel's zoom, the middle button's free look, which the playground
    /// switches on); this adds what the prototype had:
    ///  - the view's centre is the middle of the FREE field (left of the stats box, above the bottom band), not the window's middle: when
    ///    a panel opens or closes the floor point there glides (180 ms) to the new middle;
    ///  - the middle button turns and tilts round that point (tilt held at 40 .. 66 degrees, the prototype's range);
    ///  - the wheel zooms TO THE CURSOR (the floor point under it stays under it; the zoom buttons zoom about the free field's middle);
    ///  - (run 11, owner item 227) the RIGHT button never moves the view:
    ///    every right press and release is a click for the editor (OzUeField, OzUePlace, PlayerInput), however far the mouse slid; the
    ///    view moves with W A S D, the arrows, the screen edge, the wheel, the middle button, the compass and the zoom buttons;
    ///  - Fit (Home), CentreOn, the compass's glide home, a double-click on empty floor centres the view there.
    /// Every move is the RTS camera's own focus shifted after its Update and placed again (RtsCamera.Place), so its limits stay.
    /// Runs at -30: after the HUD (-45, PointerOverUi of this frame), before PlayerInput (0) and the RTS camera (0); the anchors are applied
    /// in LateUpdate, after the RTS camera moved.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class OzUeCamera : MonoBehaviour
    {
        public static OzUeCamera I { get; private set; }
        /// <summary>The tilt range of the free look in the playground (spec 9.6; the game's free look allows 25 .. 89).</summary>
        public const float TiltMin = 40f, TiltMax = 66f;
        /// <summary>Right-button travel (canvas units) after which the press is a pan, not an order (spec 9.6).</summary>
        public const float DragUnits = 4f;
        /// <summary>The free field's middle glides this long to a new place (spec 9.6).</summary>
        public const float CentreGlide = 0.18f;
        /// <summary>Checks: right-drag pans, wheel zooms to the cursor, fits, centre glides, compass glides since the editor opened.</summary>
        public static int Pans, CursorZooms, Fits, Glides, Homes;

        RtsCamera rts; Camera cam; ZGame game;
        float startHeight = 70f;
        // what keeps a floor point under a screen point this frame (the highest wins: grab, turn, zoom, glide, centre move)
        enum Hold { None, Glide, Zoom, Turn, Grab }
        Hold hold; Vector3 holdWorld; Vector2 holdScreen;
        // the right-drag pan
        bool dragging; Vector3 grabWorld = Vector3.zero;                                                            // run 11: no right-drag pan any more (dragging stays false)
        // the zoom anchor (wheel: the cursor; buttons: the free field's middle)
        bool zoomOn, zoomMine; Vector3 zoomWorld; Vector2 zoomScreen; float targetBefore;
        Vector2 mouseBefore; Vector3 groundBefore; bool groundBeforeOk, overUiBefore;
        // the turn pivot (middle button held, the compass's glide home)
        bool turnOn; Vector3 turnWorld;
        // a glide of the free field's middle to a floor point (Fit, CentreOn, double-click)
        bool glideOn; Vector3 glideNow, glideTo;
        // the free field moved (a panel opened / closed): its old middle's floor point glides to the new middle
        bool moveOn; Vector3 moveWorld; Vector2 moveFrom, moveTo; float moveAt; Vector2 lastCentre = new Vector2(-1f, -1f);
        bool startPlaced;
        // the double-click on empty floor
        float clickAt = -10f, lastClickAt = -10f; Vector2 clickPos, lastClickPos; bool clickArmed;

        public void Init(OzUnitEditor ed)
        {
            I = this;
            rts = ed.Rts; game = ed.Game;
            cam = rts ? rts.GetComponent<Camera>() : Camera.main;
            if (rts) startHeight = rts.height;
            OzUeStatics.Hold("RtsCamera.FreeTiltMin", () => RtsCamera.FreeTiltMin, v => RtsCamera.FreeTiltMin = v, TiltMin);
            OzUeStatics.Hold("RtsCamera.FreeTiltMax", () => RtsCamera.FreeTiltMax, v => RtsCamera.FreeTiltMax = v, TiltMax);
            Pans = CursorZooms = Fits = Glides = Homes = 0;
            Debug.Log($"[OpenZ UnitEditor] camera: free look on (middle button), tilt {TiltMin:0}..{TiltMax:0} deg, start height {startHeight:0} m, {(rts ? "RTS camera found" : "NO RTS camera")}");
        }

        void OnDestroy() { if (I == this) I = null; }

        /// <summary>The camera's height at the start (the 100 % of the zoom read-out, spec E032).</summary>
        public float StartHeight => startHeight;
        /// <summary>The view is turned or tilted (the compass shows it, E036).</summary>
        public bool Turned => rts && rts.UeTurned;
        /// <summary>The view's heading (degrees, 0 = north up, clockwise).</summary>
        public float Yaw => rts ? rts.yaw : 0f;
        public Camera Cam => cam;

        // ------------------------------------------------------------------------------------------------ input (before PlayerInput)
        void Update()
        {
            if (!rts || !cam) return;
            var m = Mouse.current; var hud = OpenZHud.I;
            bool overUi = hud && hud.PointerOverUi;
            bool modal = OzUeHooks.AnyModal || OzConfirm.Blocks;
            Vector2 mp = m != null ? m.position.ReadValue() : Vector2.zero;
            mouseBefore = mp; overUiBefore = overUi;
            groundBeforeOk = Ground(mp, out groundBefore);
            targetBefore = rts.UeTargetHeight;
            zoomMine = false;

            if (m != null)
            {
                dragging = false;
                if (OzUeHooks.RightDragged) OzUeHooks.RightDragged = false;

                // the middle button turns round the free field's middle (the RTS camera turns, LateUpdate keeps that point there)
                if (m.middleButton.wasPressedThisFrame) { turnOn = FreeCentreGround(out turnWorld); glideOn = false; }
                if (!m.middleButton.isPressed && !rts.UeGliding) turnOn = false;

                TickDoubleClick(m, mp, overUi || modal);
            }
        }

        void TickDoubleClick(Mouse m, Vector2 mp, bool blocked)
        {
            float now = Time.unscaledTime;
            if (m.leftButton.wasPressedThisFrame)
            {
                clickArmed = !blocked && OzUePick.Hover == null && !OzUePlace.Busy;
                clickAt = now; clickPos = mp;
            }
            if (m.leftButton.wasReleasedThisFrame && clickArmed)
            {
                clickArmed = false;
                float k = Mathf.Max(0.01f, OzUnitEditor.PxPerUnit);
                bool click = now - clickAt < 0.35f && (mp - clickPos).magnitude / k < 6f;
                if (!click) { lastClickAt = -10f; return; }
                if (now - lastClickAt < 0.4f && (mp - lastClickPos).magnitude / k < 10f && OzUePick.Hover == null && Ground(mp, out var g))
                {
                    lastClickAt = -10f;
                    CentreOn(g);
                    return;
                }
                lastClickAt = now; lastClickPos = mp;
            }
        }

        // ------------------------------------------------------------------------------------------------ after the RTS camera moved
        void LateUpdate()
        {
            if (!rts || !cam) return;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            var ff = OzUeInsets.FreeFieldScreen;
            Vector2 centre = ff.width > 1f && ff.height > 1f ? ff.center : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // the player moves the view himself (arrows, W A S D, the screen edge): the glides give way
            if (UserPans()) { glideOn = false; moveOn = false; }

            // the wheel zoomed (the RTS camera's target height changed and not by a button of the editor): zoom to the cursor
            if (!zoomMine && Mathf.Abs(rts.UeTargetHeight - targetBefore) > 1e-3f)
            {
                if (!overUiBefore && groundBeforeOk) { zoomOn = true; zoomWorld = groundBefore; zoomScreen = mouseBefore; CursorZooms++; }
                else zoomOn = false;
            }
            if (zoomOn && Mathf.Abs(rts.height - rts.UeTargetHeight) < 0.01f) zoomOn = false;

            // the start view (spec 9.6): once the panels have reported their insets, the floor's centre stands in the free field's middle
            if (!startPlaced && OzUnitEditor.UiBuilt)
            {
                startPlaced = true; lastCentre = centre;
                Keep(new Vector3(rts.mapSizeMeters.x * 0.5f, 0f, -rts.mapSizeMeters.y * 0.5f), centre);
                Debug.Log($"[OpenZ UnitEditor] camera: start view, the floor's centre in the free field's middle ({centre.x:0}, {centre.y:0} px)");
            }
            // the free field changed (a panel opened or closed, H): its old middle's floor point glides to the new middle
            if (lastCentre.x < 0f) lastCentre = centre;
            if ((centre - lastCentre).sqrMagnitude > 0.25f)
            {
                Vector2 from = moveOn ? Vector2.Lerp(moveFrom, moveTo, Ease((now - moveAt) / CentreGlide)) : lastCentre;
                if (Ground(from, out var w)) { moveOn = true; moveWorld = w; moveFrom = from; moveTo = centre; moveAt = now; }
                lastCentre = centre;
            }
            if (moveOn && now - moveAt >= CentreGlide) moveOn = false;

            // pick the one hold of this frame
            hold = Hold.None;
            if (dragging) { hold = Hold.Grab; holdWorld = grabWorld; holdScreen = mouseBefore; }
            else if (turnOn && (rts.FreeLook || rts.UeGliding)) { hold = Hold.Turn; holdWorld = turnWorld; holdScreen = centre; }
            else if (zoomOn) { hold = Hold.Zoom; holdWorld = zoomWorld; holdScreen = zoomScreen; }
            else if (glideOn)
            {
                float k = 1f - Mathf.Exp(-dt / 0.055f);                                                 // about 180 ms to settle (the prototype's glide)
                glideNow = Vector3.Lerp(glideNow, glideTo, k);
                if ((glideNow - glideTo).sqrMagnitude < 0.0004f) { glideNow = glideTo; glideOn = false; }
                hold = Hold.Glide; holdWorld = glideNow; holdScreen = centre;
            }
            else if (moveOn) { hold = Hold.Glide; holdWorld = moveWorld; holdScreen = Vector2.Lerp(moveFrom, moveTo, Ease((now - moveAt) / CentreGlide)); }
            if (hold == Hold.Grab || hold == Hold.Turn) { zoomOn = false; glideOn = false; moveOn = false; }   // a hand on the view: the older holds end
            else if (hold == Hold.Zoom) { glideOn = false; moveOn = false; }
            if (hold != Hold.None) Keep(holdWorld, holdScreen);
        }

        /// <summary>The player pans the view himself this frame: the arrows, the bound scroll keys, the mouse at the screen edge.</summary>
        bool UserPans()
        {
            var kb = Keyboard.current; var m = Mouse.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed) return true;
                if (!(kb.ctrlKey.isPressed || kb.altKey.isPressed) && (OzBindings.Held(OzAction.ScrollLeft) || OzBindings.Held(OzAction.ScrollRight)
                    || OzBindings.Held(OzAction.ScrollUp) || OzBindings.Held(OzAction.ScrollDown))) return true;
            }
            if (m != null && rts.edgePx > 0f && Application.isFocused)
            {
                var p = m.position.ReadValue(); float e = rts.edgePx;
                if (p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height && (p.x < e || p.x > Screen.width - e || p.y < e || p.y > Screen.height - e)) return true;
            }
            return false;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>Shift the RTS camera's focus so the floor point w is under the screen point s (pixels, bottom-left), then place it.</summary>
        void Keep(Vector3 w, Vector2 s)
        {
            for (int i = 0; i < 2; i++)
            {
                if (!Ground(s, out var g)) return;
                var d = new Vector3(w.x - g.x, 0f, w.z - g.z);
                if (d.sqrMagnitude < 1e-8f) return;
                rts.focus += d;
                rts.Place();
            }
        }

        /// <summary>The floor point (y 0) under a screen point (pixels, bottom-left).</summary>
        public bool Ground(Vector2 screen, out Vector3 p)
        {
            p = default;
            if (!cam) return false;
            var r = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (r.direction.y > -1e-4f) return false;
            float t = -r.origin.y / r.direction.y;
            p = r.origin + r.direction * t; p.y = 0f;
            return true;
        }

        /// <summary>The floor point at the free field's middle.</summary>
        public bool FreeCentreGround(out Vector3 p)
        {
            var ff = OzUeInsets.FreeFieldScreen;
            var c = ff.width > 1f && ff.height > 1f ? ff.center : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return Ground(c, out p);
        }

        // ------------------------------------------------------------------------------------------------ the editor's camera actions
        /// <summary>The free field's middle glides to this floor point.</summary>
        public void CentreOn(Vector3 worldPoint)
        {
            if (!rts) return;
            if (!FreeCentreGround(out glideNow)) glideNow = new Vector3(rts.focus.x, 0f, rts.focus.z);
            glideTo = new Vector3(worldPoint.x, 0f, worldPoint.z);
            glideOn = true; zoomOn = false; moveOn = false; rts.UeStopFly();
            Glides++;
            Debug.Log($"[OpenZ UnitEditor] camera: centre glides to ({glideTo.x:0.0}, {glideTo.z:0.0})");
        }

        /// <summary>E034 / Home: every unit, flag and obstacle framed with 15 % margin inside the free field (no units = the start view),
        /// measured along the (turned) view, as the prototype's fit().</summary>
        public void Fit()
        {
            if (!rts || !cam || game == null) return;
            Fits++;
            var pts = new List<Vector3>();
            foreach (var o in OzUePlayground.Placed()) if (o != null) pts.Add(game.CenterWorld(o, false));
            if (pts.Count == 0)
            {
                rts.ZoomTo(startHeight); zoomMine = true; zoomOn = false;
                CentreOn(new Vector3(rts.mapSizeMeters.x * 0.5f, 0f, -rts.mapSizeMeters.y * 0.5f));
                Debug.Log("[OpenZ UnitEditor] camera: fit, nothing on the field: the start view");
                return;
            }
            var right = cam.transform.right; right.y = 0f; right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            var fwd = Vector3.Cross(right, Vector3.up);
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var p in pts)
            {
                float a = Vector3.Dot(p, right), b = Vector3.Dot(p, fwd);
                x0 = Mathf.Min(x0, a); x1 = Mathf.Max(x1, a); z0 = Mathf.Min(z0, b); z1 = Mathf.Max(z1, b);
            }
            float bw = Mathf.Max(24f, x1 - x0) * 1.3f, bd = Mathf.Max(16f, z1 - z0) * 1.3f;
            var ff = OzUeInsets.FreeFieldScreen;
            float sw = ff.width > 1f ? ff.width : Screen.width, sh = ff.height > 1f ? ff.height : Screen.height;
            float tilt = rts.Tilt * Mathf.Deg2Rad;
            float q = Mathf.Min(sw / bw, sh / (bd * Mathf.Max(0.2f, Mathf.Sin(tilt))));             // pixels per metre that frame the box
            float F = Screen.height * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float h = F / Mathf.Max(q, 1e-3f) * Mathf.Sin(rts.pitch * Mathf.Deg2Rad);                // RtsCamera: distance = height / sin(pitch)
            rts.ZoomTo(h); zoomMine = true; zoomOn = false;
            var c = right * ((x0 + x1) * 0.5f) + fwd * ((z0 + z1) * 0.5f);
            CentreOn(c);
            Debug.Log($"[OpenZ UnitEditor] camera: fit {pts.Count} object(s), box {bw:0} x {bd:0} m along the view, height {Mathf.Clamp(h, rts.minHeight, rts.maxHeight):0} m");
        }

        /// <summary>E033 / E035: one wheel notch out (dir -1) or in (+1) about the free field's middle.</summary>
        public void ZoomStep(int dir)
        {
            if (!rts || dir == 0) return;
            if (FreeCentreGround(out zoomWorld))
            {
                var ff = OzUeInsets.FreeFieldScreen;
                zoomScreen = ff.width > 1f && ff.height > 1f ? ff.center : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                zoomOn = true;
            }
            rts.ZoomBy(Mathf.Pow(1f - rts.wheelStep, Mathf.Sign(dir)));
            zoomMine = true; glideOn = false;
        }

        /// <summary>E036: back to north and the fixed tilt (the free look's 0.6 s glide), round the free field's middle.</summary>
        public void CompassHome()
        {
            if (!rts) return;
            turnOn = FreeCentreGround(out turnWorld);
            rts.UeGlideHome();
            Homes++;
            Debug.Log("[OpenZ UnitEditor] camera: compass, back to north");
        }

        /// <summary>E032: the zoom against the start (start height / height x 100).</summary>
        public float ZoomPercent => rts && rts.height > 0.01f ? startHeight / rts.height * 100f : 100f;

        /// <summary>E031: canvas units per metre on the ground at the free field's bottom row (sideways).</summary>
        public float UnitsPerMetreAtBottom()
        {
            var ff = OzUeInsets.FreeFieldScreen;
            float y = (ff.height > 1f ? ff.yMin : 0f) + 2f, x = ff.width > 1f ? ff.center.x : Screen.width * 0.5f;
            if (!Ground(new Vector2(x - 50f, y), out var a) || !Ground(new Vector2(x + 50f, y), out var b)) return 0f;
            float m = Vector3.Distance(a, b);
            return m > 1e-4f ? 100f / m / Mathf.Max(0.01f, OzUnitEditor.PxPerUnit) : 0f;
        }
    }
}
