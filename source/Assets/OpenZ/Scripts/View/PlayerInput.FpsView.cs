// OpenZ (GPL-3.0) - version 1.4 kit fps_view: the player input's side of the hidden first-person view (View/OzFpsView.cs). fps_view_fix (d): the
// view's moves are Alt + right-click moves (attackTo off), so an attack order never stops the drive.
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    public partial class PlayerInput
    {
        /// <summary>1.4 FPS VIEW, the hook in Update (after the selection is kept up to date): Alt+F with the view off tries to go in (OzFpsView.TryEnter:
        /// one own unit that can see, else the quiet refusal sound). True while the view is on: the rest of Update (selection, orders, groups, keys) waits,
        /// so the mouse and the keys mean nothing here until the view is left. False always while the combo is not pressed.</summary>
        bool FpsViewStep(Keyboard kb)
        {
            if (OzFpsView.On) return true;
            if (!OzFpsView.Combo(kb) || OzFpsView.LeftFrame == Time.frameCount) return false;                // not on the frame Alt+F left the view
            if (!OzFpsView.TryEnter(this)) return false;
            lDown = dragging = false; PatrolArmed = false;                                                     // a box or a patrol under way when the view went on is dropped
            pending.Clear(); rightDownShift = false;                                                           // fix round 2: a Shift queue not sent yet is dropped too (else Flush sends it on the first frame after the leave)
            return true;
        }

        /// <summary>1.4 FPS VIEW: the move order of an Alt + right-click on open ground at (x, y) (BuildWaypoint without a target, alt: attackTo
        /// off) through SEND_WAYPOINTS, exactly as Flush sends it, without the voice and the order marker (it is sent again while the keys are held:
        /// fix round 3, about one order per 2 tiles of a straight walk). fps_view_fix (d), 7 Nov 2026: the plain right-click was an attack-move
        /// whenever no enemy stood within the unit's attack radius + agro distance at the moment of the order (ZWaypoint.Move(x, y, !NearHostiles)),
        /// and an enemy coming into agro reach then put an ATTACK waypoint in front of the walk (ZObject.CheckAttackTo) that stops the unit dead
        /// until the target dies (StopMove + Engage). Measured on the plain sim (kits/fps_view_fix/tools/harness, drive, L17): an APC with W held
        /// past an enemy 0.00 m in 6 s; with attackTo off 9.50 m and 10 shots (11 standing). W is the player driving: the order always wins, and the
        /// unit shoots on the way as every vehicle (Zod) and the walking-fire robots (DV57) do on a move.</summary>
        internal bool FpsMove(ZObject u, int x, int y)
        {
            if (u == null || W == null || !W.SendWaypoints(SideOf(u), u, new[] { BuildWaypoint(u, null, x, y, false, true) })) return false;
            AudioDirector.FpsWalkOrder(u);                                                                     // fix round 2: its walk is heard, as after a right-click's order marker (View/AudioDirector.FpsView.cs)
            return true;
        }

        /// <summary>1.4 FPS VIEW: the STOP order (X) for the unit.</summary>
        internal bool FpsStop(ZObject u) => u != null && W != null && W.SendStop(SideOf(u), u);

        /// <summary>fps_view_sky (2c): what the view's centre points at: the game's own hover pick (HoverAt: the fog's covered objects, own minions
        /// and bridges skipped as for the mouse) at the middle of the camera's picture; an enemy unit, gun or building in front of the eye within
        /// reachM m, else null (ground, sky, own and neutral objects: the game has no attack-ground order).</summary>
        internal ZObject FpsTargetAtCentre(Camera c, ZObject u, float reachM)
        {
            if (!c || u == null || W == null || !game) return null;
            var h = HoverAt(new Vector2(c.pixelWidth * 0.5f, c.pixelHeight * 0.5f));
            if (h == null || h == u || h.Destroyed || h.Owner == game.player || h.Owner == Team.Neutral) return null;
            if (h.Kind != ZKind.Robot && h.Kind != ZKind.Vehicle && h.Kind != ZKind.Cannon && h.Kind != ZKind.Building) return null;
            var d = game.CenterWorld(h) - c.transform.position;
            if (Vector3.Dot(d, c.transform.forward) <= 0f || d.sqrMagnitude > reachM * reachM) return null;
            return h;
        }

        /// <summary>fps_view_sky (2c): the attack order an RTS right-click on h gives u (BuildWaypoint, no modifier keys) through SEND_WAYPOINTS, as
        /// Flush sends it, without the voice and the order marker (a held button sends it again on a new target); false when that click would
        /// not be an attack (an own, neutral or empty object: move, enter, repair) or the order is refused.</summary>
        internal bool FpsAttack(ZObject u, ZObject h)
        {
            if (u == null || h == null || W == null) return false;
            var wp = BuildWaypoint(u, h, h.CenterX, h.CenterY, false, false);
            return wp.mode == WpMode.Attack && W.SendWaypoints(SideOf(u), u, new[] { wp });
        }
    }
}
