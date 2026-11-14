using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// 1.4 camera controls (UI/OzCamControls.cs), what the RTS camera asks. Hooks in RtsCamera.cs: Update's middle-button line asks
    /// MiddleTurns (the Alt way of MIDDLE MOUSE), the right drag moves the map through MiddleDrag (View/OzRightDrag.cs) and stops
    /// the edge scroll meanwhile as the middle drag does, MiddleDrag and FeelStep's pan take OzCamControls.ScrollFactor (1 at the
    /// default 100 %), and MiddleDrag follows a turned view through DragHeading.
    /// At the defaults (TURN VIEW, 100 %, RIGHT-DRAG SCROLL off) every one of them gives what the lines gave before: MiddleTurns is
    /// true, the factor is 1 (x 1 is exact), the right drag never starts, and DragHeading only turns a drag while the free look may
    /// hold a turned view and no script holds the heading, which before this kit never met a middle drag (the middle button turned
    /// the view then).
    /// </summary>
    public partial class RtsCamera
    {
        /// <summary>The middle button turns the view (when FreeTurn allows it) instead of moving the map: false only with DRAG MAP,
        /// ALT TURNS VIEW and Alt not held (the testing overlay and the Unit editor's playground keep their turn).</summary>
        bool MiddleTurns(Keyboard kb) => OzCamControls.HooksOff
            || OzCamControls.MiddleTurnsFor(OzCamControls.MiddleMode, kb != null && kb.altKey.isPressed, OzCheatSpawner.Visible, OzUeHooks.Active);

        /// <summary>A drag's ground travel along the view's heading when the view is turned (the Alt way's drag, the right drag in a
        /// turned view), as the keys and the screen edge already pan; a fixed or script-held view: unchanged.</summary>
        Vector3 DragHeading(Vector3 v) => !OzCamControls.HooksOff && Mathf.Abs(yaw) > 0.01f && !yawHeld && Testing ? Quaternion.Euler(0f, yaw, 0f) * v : v;

        /// <summary>Checks: the middle-button rule for a keyboard state, as Update asks it.</summary>
        public bool ProofMiddleTurns(bool altHeld) => OzCamControls.HooksOff
            || OzCamControls.MiddleTurnsFor(OzCamControls.MiddleMode, altHeld, OzCheatSpawner.Visible, OzUeHooks.Active);
        /// <summary>Checks: a drag's ground travel as MiddleDrag turns it now.</summary>
        public Vector3 ProofDragHeading(Vector3 v) => DragHeading(v);
    }
}
