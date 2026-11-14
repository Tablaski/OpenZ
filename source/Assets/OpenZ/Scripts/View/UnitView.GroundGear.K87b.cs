namespace OpenZ
{
    /// <summary>
    /// 1.2 item 146, the heavy tank: its planted belts (View/UnitView.GroundGear.cs) rest with their
    /// lowest ground sample exactly on the ground; between two samples a crease of the real ground can still reach a few centimetres into a
    /// belt (the track test, menu 14: 2 frames at 3.3 / 3.8 cm while the heavy v2 drove up a 2.2 deg slope, all others under 1 cm). A small
    /// air margin under the heavy tank's belts takes those in: they ride <see cref="HeavyAirM"/> higher (1.5 cm: about a third of a pixel at
    /// the game's 70 m camera, never seen as floating; the hull rides on the belts as before). Both heavy models (the switch stays equal);
    /// the other types as before. Switch: <see cref="On"/>.
    /// </summary>
    public static class TrackGroundAir
    {
        /// <summary>The switch (off = every belt exactly on its lowest sample, as before).</summary>
        public static bool On = true;
        /// <summary>The heavy tank's air margin under its planted belts (m).</summary>
        public static float HeavyAirM = 0.015f;
        /// <summary>The air under a planted belt of this vehicle type (m).</summary>
        public static float For(string type) => On && type == OzHeavyTank.Type ? HeavyAirM : 0f;
    }
}
