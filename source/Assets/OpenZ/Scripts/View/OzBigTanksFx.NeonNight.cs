namespace OpenZ
{
    /// <summary>
    /// View only. The side neon runs of the Mammoth and the Bizon (the dark channel,
    /// the frosted tube and its glow, OzBigTanksFx.Tank.Build) are drawn only while the lamps are on (the lamps' clock, LampsOnAt / LampsOffAt,
    /// the same switch as the searchlight and the street lamps; the Unit editor's DAY | NIGHT switch sets that clock): by day no strip mesh is
    /// drawn at all, at night they glow as before. The switch eases over <see cref="NeonFadeSec"/> (the glow fades, the strip meshes go with
    /// it). <see cref="NeonDayHidden"/> off = shown by day as before.
    /// </summary>
    public sealed partial class OzBigTanksFx
    {
        /// <summary>N19: the neon runs hidden by day (on).</summary>
        public static bool NeonDayHidden = true;
        /// <summary>N19: the neon's fade in at dusk / out at dawn (s of game time).</summary>
        public static float NeonFadeSec = 0.6f;
    }
}
