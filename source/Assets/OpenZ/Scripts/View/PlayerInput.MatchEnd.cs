using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// the orders, only from the sim's game over on (the next end check, 0.22 s after the fort's fall) and only with the end freeze (DV10)
    /// on; selection, the hover frames and chips and the unit cursors stayed (the world was meant to stay clickable during the cheering).
    /// Now from the moment the match is decided (the fort's fall: OpenZEnding runs from the TeamEliminated event; or the sim's game over
    /// when one side lost its last unit) until the scene is left, the world takes no input from the player: the selection is dropped, no
    /// click or box selects, no order is given (mouse, R, Q / X / H, groups, select-all, next robot / vehicle / gun), a placement or an
    /// armed patrol is cancelled, nothing is hovered (no frame, no chip, the plain arrow). Both sides alike, the win and the loss. The end
    /// screen's buttons and keys are its own (UI/OpenZEndScreen.cs) and work as before; the camera keeps OpenZEnding's own lock. The Unit
    /// editor's playground never ends (no lock there); the gate battery's test stages are left out as for the camera lock.
    /// </summary>
    public partial class PlayerInput
    {
        /// <summary>The match is decided: the world takes no selection, order or hover from the player.</summary>
        public static bool MatchDecided(ZWorld w) => w != null && !w.NoGameEnd && (w.GameOver || OpenZEnding.Running) && !GateBattery.Running;
        /// <summary>Proof counters (Play): frames the lock held, units it dropped from the selection, clicks it refused.</summary>
        public static int EndLockFrames, EndLockDropped, EndLockRefused;

        /// <summary>Update, before anything reads the pointer: true = the match is decided, the frame's world input is over.</summary>
        bool EndLockStep(Mouse mouse, Vector2 mp)
        {
            if (!MatchDecided(W)) return false;
            EndLockFrames++;
            if (Selection.Count > 0) { EndLockDropped += Selection.Count; Selection.Clear(); }
            Hover = null;
            if (Placing != null) CancelPlace();
            pending.Clear(); PatrolArmed = false; lDown = dragging = false;
            if (InView(mp) && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasReleasedThisFrame)) EndLockRefused++;
            return true;
        }
    }
}
