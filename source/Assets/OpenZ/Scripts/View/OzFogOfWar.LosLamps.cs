using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// 1.5 los_fog (final pass 14h): the question VehicleLights asks before it draws a vehicle's lamp glows, haze cones and ground pool (Graphics.DrawMesh,
    /// so no renderer HideCovered could hold off): an enemy robot or vehicle the line-of-sight fog hides now. At night a hidden enemy vehicle no longer
    /// shows its headlights in the fog. False whenever the fog is not on screen, for the player's own side and for ground never explored (the 1.4 cloud).
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        public static bool LosHides(ZObject o) => hidingNow && o != null && o.Owner != playerNow && LosCovers(o);

        /// <summary>
        /// fog only (never on ground seen now, scaled by its eased value; the cloud of unexplored ground hides them), so it reads as an artificial
        /// hiding, not a cloud shadow. Depth = how much darker a line's middle is (0.09 = 9 %). Off = exactly the look without them.</summary>
        public static bool LosScanLines = true;
        public static float LosScanDepth = 0.09f;
    }
}
