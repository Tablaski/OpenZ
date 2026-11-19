// OpenZ (GPL-3.0) - 1.5 kit fps_fort_button_win (PRIVATE: the owner's first-person test view, never in player text): the order of the fort
// wall SELF DESTRUCT panel's press (View/OzFpsFortButton.cs).
using OpenZ.Sim;

namespace OpenZ
{
    public partial class PlayerInput
    {
        /// <summary>1.5 fps_fort_button_win: the order a plain RTS right-click on enemy fort f's door gives unit u (BuildWaypoint at a point of Zod's door
        /// rectangle, no modifier keys: ENTER_FORT, attackTo off, DV16) through SEND_WAYPOINTS, exactly as Flush sends it, without the voice and the
        /// order marker (as the view's other orders). False when that click would not be ENTER_FORT (an own or neutral fort, a fort that is down, a unit
        /// that cannot enter) or the order is refused. The rules then run the fort's fall and the win exactly as for the click.</summary>
        internal bool FpsEnterFort(ZObject u, ZObject f)
        {
            if (u == null || f == null || W == null || !f.IsFort || f.Destroyed || f.Owner == Team.Neutral || f.Owner == SideOf(u)) return false;
            int x = f.LocX + 80, y = f.LocY + (f.Type == "fort_front" ? 80 : 48);                             // the middle of Zod's door rectangle (OnFortDoor)
            var wp = BuildWaypoint(u, f, x, y, false, false);
            return wp.mode == WpMode.EnterFort && W.SendWaypoints(SideOf(u), u, new[] { wp });
        }
    }
}
