using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// The minimap's click rules (UI/OpenZMinimap.cs) and its cursor (UI/OpenZCursors.cs) ask these answers; the counted check
    /// (Editor/OzV14RallyMinimapCount.cs) counts them against 1.4 as it was and against the map's own rally tool (UI/OpenZRallyPoints.cs).
    /// <list type="bullet">
    /// <item>The rally tool armed (SET RALLY or its key: the menu closes, the tool keeps its factory): a left click on the minimap sets the rally
    /// point (it was already so; now only while the tool is still armed at the release, as on the map); a right click with nothing selected sets it
    /// too (as on the map; before, the minimap asked for the open menu, which is closed while the tool is armed, so nothing happened); with units
    /// selected a right click is their move order (as on the map: PlayerInput's order, the tool stays armed). Shift = one more point of a route,
    /// sent when Shift is let go (OpenZRallyPoints.AddPoint, the same call as the map's). Same sounds, toasts and log line: the same AddPoint / Send.</item>
    /// <item>While armed the minimap draws that factory's route in full (as the open menu's) with the unsent Shift points, a ghost mark under the
    /// pointer and the dotted line to it (the map's ghost flag), and the cursor over it is the map's armed cursor (move).</item>
    /// <item>The tool not armed: every answer is 1.4's as it was (left = fly the camera, right = the selected units' order; the open menu's rally
    /// gesture is unchanged too, though the minimap is hidden while a menu shows: OpenZHud.LowerPanels.cs).</item>
    /// </list>
    /// No rule of the simulation changes: the same ZWorld.SetRallyPoints, no new DV row.
    /// </summary>
    public static class OzRallyMinimap
    {
        /// <summary>What a left press over the map picture starts.</summary>
        public enum Press { Fly, Rally }
        /// <summary>What a right click (the release of a press that did not drag) over the map picture gives.</summary>
        public enum RightAct { None, Order, Rally }

        /// <summary>A left press over the map picture (the HUD took it as a UI press with no button under it): the armed tool's point, else the fly.</summary>
        public static Press LeftPress(bool armed) => armed ? Press.Rally : Press.Fly;

        /// <summary>The left release after a rally press: a point only while the tool is still armed (Esc or the rally key may have dropped it
        /// meanwhile; the map's tool asks Armed at the release too) and no modal screen is up.</summary>
        public static bool LeftRelease(bool rallyPress, bool armed, bool modal) => rallyPress && armed && !modal;

        /// <summary>A right click over the map picture: selected units take it as their order first (the map: PlayerInput's order), else the rally
        /// tool's factory (OpenZRallyPoints.Building: the armed tool's, or the open menu's) gets the point, else nothing.</summary>
        public static RightAct RightClick(int selected, bool hasFactory) => selected > 0 ? RightAct.Order : hasFactory ? RightAct.Rally : RightAct.None;

        /// <summary>The factory whose route the minimap draws in full: the rally tool's (armed: the one it was armed for; else the open menu's).
        /// Without the rally tool, the open menu's (1.4 as it was).</summary>
        public static ZObject Factory(OpenZHud hud)
        {
            if (hud == null) return null;
            if (hud.Rally) return hud.Rally.Building;
            return hud.Production ? hud.Production.Building : null;
        }

        /// <summary>The move cursor over the minimap: units selected (1.4 as it was) or the rally tool armed (the map's armed cursor).</summary>
        public static bool MoveCursor(bool overMap, int selected, bool armed) => overMap && (selected > 0 || armed);

        /// <summary>The ghost mark under the pointer on the minimap: the tool armed and the pointer over the map picture.</summary>
        public static bool Ghost(bool armed, bool overMap) => armed && overMap;
    }
}
