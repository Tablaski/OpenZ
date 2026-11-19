// OpenZ (GPL-3.0) - version 1.5 kit fps_feel: the sprint move of the private first-person view (View/OzFpsView.cs, View/OzFpsFeel.cs).
using OpenZ.Sim;

namespace OpenZ
{
    public partial class PlayerInput
    {
        /// <summary>1.5 fps_feel: the view's move (FpsMove: an Alt + right-click move on open ground, through SEND_WAYPOINTS) with the sprint mark
        /// (ZWaypoint.sprint, rule DV178 in Sim/ZObject.SprintOrder.cs: the unit moves x ZWorld.SprintPct / 100 while this order is its first
        /// waypoint). Sent only by the first-person view while Shift is held (OzFpsView.Walk asks OzFpsFeel.Sprint); no other order of the game
        /// carries the mark. Its walk is heard as FpsMove's is.</summary>
        internal bool FpsSprint(ZObject u, int x, int y)
        {
            if (u == null || W == null) return false;
            var wp = BuildWaypoint(u, null, x, y, false, true);
            wp.sprint = true;
            if (!W.SendWaypoints(SideOf(u), u, new[] { wp })) return false;
            AudioDirector.FpsWalkOrder(u);
            return true;
        }
    }
}
