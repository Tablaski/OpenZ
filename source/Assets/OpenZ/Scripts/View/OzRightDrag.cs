using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// 1.4 RIGHT-DRAG SCROLL (UI/OzCamControls.cs): the right button's drag, read once a frame from the mouse the game reads.
    /// With the row ON, a right press on the map (not over the HUD or a menu, not in the Unit editor, not while the ending holds
    /// the camera) is armed; once the pointer is more than ThresholdPx from where it went down it scrolls: RtsCamera moves the map
    /// by the mouse travel as the middle drag does (View/RtsCamera.cs). Its release is no click: every place that gives an order
    /// or a rally point on the right button's release asks Click(), which is false for that release (PlayerInput's orders and
    /// patrol, the minimap's order, both rally tools), and the shuttle's landing aim hides while it scrolls (View/OzShuttleShadow.cs).
    /// A right press that never travels that far is a click, exactly as before. With the row OFF nothing is armed: Click() is the
    /// button's own release and Scrolling() is false, so every right-button user reads what it read before.
    /// Presses (cancel a placement, a dialog's NO, the testing overlay's drag) act on the press as before; a drag that follows
    /// such a press only scrolls. Order independent: whichever script asks first in a frame reads the mouse for that frame.
    /// </summary>
    public static class OzRightDrag
    {
        /// <summary>The travel (px) that makes a right press a drag, at a 1080 px tall screen; scaled with the screen height, never
        /// under MinPx. 10 px: a click made while the hand moves the mouse a little stays an order.</summary>
        public const float ThresholdPx1080 = 10f, MinPx = 6f;
        public static float ThresholdPx => Mathf.Max(MinPx, ThresholdPx1080 * Screen.height / 1080f);

        /// <summary>The places that read the release (counters per place for the checks).</summary>
        public const int SiteOrders = 0, SitePatrol = 1, SiteMinimap = 2, SiteRally = 3, SiteBuildRally = 4, Sites = 5;
        /// <summary>Checks: drags started, drag releases (no order), clicks; per place, the releases it was told were drags (no order)
        /// and the ones it was told were clicks.</summary>
        public static int Drags, DragReleases, Clicks;
        public static readonly int[] SiteSwallowed = new int[Sites], SitePassed = new int[Sites];

        static OzCamControls.RightGesture g;
        static int frame = -1; static Mouse seen; static int logged;

        /// <summary>The gesture for this frame (once a frame; again when another mouse became the current one).</summary>
        public static void Tick(Mouse m)
        {
            if (frame == Time.frameCount && m == seen) return;
            frame = Time.frameCount; seen = m;
            if (m == null) { g = default; return; }
            var b = m.rightButton;
            bool pressed = b.wasPressedThisFrame;
            Vector2 p = m.position.ReadValue();
            int ev = g.Advance(pressed, b.isPressed, b.wasReleasedThisFrame, p.x, p.y, pressed && CanStart(), ThresholdPx);
            if ((ev & OzCamControls.RightGesture.Started) != 0)
            {
                Drags++;
                if (logged++ < 3) Debug.Log($"[OpenZ Camera] right drag: the map moves (past {ThresholdPx:0.#} px; drags so far {Drags}); its release gives no order");
            }
            if ((ev & OzCamControls.RightGesture.DragReleased) != 0) DragReleases++;
            if ((ev & OzCamControls.RightGesture.ClickReleased) != 0) Clicks++;
        }

        /// <summary>A right press may become a drag now: the row is on and the match's RTS camera takes mouse input here.</summary>
        static bool CanStart()
        {
            if (!OzCamControls.RightDrag || OzCamControls.HooksOff || !Application.isPlaying || OzUeHooks.Active || RtsCamera.InputLocked || OzConfirm.Blocks) return false;
            var hud = OpenZHud.I;
            return hud && hud.rts && hud.rts.isActiveAndEnabled && !hud.Modal && !hud.PointerOverUi;
        }

        /// <summary>The right button drags the map this frame (RtsCamera moves it; the shuttle's aim hides).</summary>
        public static bool Scrolling(Mouse m) { Tick(m); return g.Scrolling && !OzCamControls.HooksOff; }

        /// <summary>The right button came up this frame as a click: the order (or rally point) it gives as before. False for the
        /// release of a drag, and on every frame the button did not come up.</summary>
        public static bool Click(Mouse m, int site)
        {
            if (m == null || !m.rightButton.wasReleasedThisFrame) return false;
            Tick(m);
            bool click = OzCamControls.HooksOff || !g.ReleaseWasDrag;
            if (site >= 0 && site < Sites) { if (click) SitePassed[site]++; else SiteSwallowed[site]++; }
            return click;
        }

        /// <summary>Checks: the counters back to 0.</summary>
        public static void ResetCounters()
        {
            Drags = DragReleases = Clicks = 0; logged = 0;
            for (int i = 0; i < Sites; i++) { SiteSwallowed[i] = 0; SitePassed[i] = 0; }
        }
    }
}
