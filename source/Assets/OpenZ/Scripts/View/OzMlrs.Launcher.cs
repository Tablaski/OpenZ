namespace OpenZ
{
    /// <summary>
    /// then "mlrs 2 match size of top part to v1 so its not smaller"; then, after a stretched box read far too big;
    /// then "keep resting tilt as v1"): v2's launcher (the box with its 8 rockets) rests at v1's tilt and is drawn
    /// as big as v1's launcher (its rack with its 8 rockets) as the normal RTS camera sees it.
    ///  * The rest tilt: v1's rack is modelled with its tubes 31.92 deg up (the mean long axis of its 8 rockets: 31.06-32.93); v2's box was
    ///    modelled at 11.35 deg. The launcher's pitch joint (MLRSRackPitch) is turned <see cref="RestRaiseV2"/> more at rest, so v2 rests at
    ///    v1's 31.92 deg. The launcher's aim limits (OzMlrs.PitchMinV2 / PitchMaxV2, about the rest) are moved down by the same, so every pose
    ///    the box can take over the hull (the lowest aim, the kicks, the highest) is the one measured clear before.
    ///  * The size: ONE uniform scale of the modelled turret, nothing stretched: the whole turret (MLRSRackYaw: turntable, plinth, red block,
    ///    hinge brackets, antennas, box, rockets) <see cref="ScaleV2"/> times about its turntable's base. Everything on it keeps its modelled
    ///    shape and place on the turret: the round rocket mouths, the muzzles, the tube exits (OpenZFxPlayer.TubeExit), the kicks, the reload
    ///    slide, the box's room inside its brackets.
    /// Measured off the engine on the two models at the same tilt (the RTS camera: 55 deg down, field of view 32, 70 m high, the unit turned every
    /// 15 deg): v2's launcher reads 0.98 / 0.97 / 1.02 of v1's area / length along its hull / width; seen from its front and sides within 5 % of
    /// v1's, seen from behind its box (thinner than v1's rack) reads up to 17 % shorter. It stays well inside the hull's front and back at rest.
    /// The turret stands <see cref="LiftV2M"/> higher: the wider turntable would touch the hull's turret collar (its inner wall 0.82-0.89 m from
    /// the axis); lifted it clears the collar by 1.6 cm and its plinth hides the step from the game camera. Set in the prefab and by the MLRS v2
    /// menu 1 build (Editor/OzMlrsV2Setup.cs).
    /// </summary>
    public static class OzMlrsLauncher
    {
        /// <summary>The turret group's one uniform scale (v2) and the turret's lift (m, prefab metres).</summary>
        public const float ScaleV2 = 1.145f, LiftV2M = 0.05f;
        /// <summary>Item 184: the launcher's rest raised this many degrees over its modelled 11.35 deg (to v1's 31.92 deg).</summary>
        public const float RestRaiseV2 = 20.57f;
    }
}
