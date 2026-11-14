using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// 1.2 v2 units (the MLRS v2, owner item 135): the launcher's elevation limits for the model drawn. v1 keeps
    /// SlewFor's missile_launcher limits (-20 .. +57 deg about its rest); the v2 box is hinged at its rear-top edge and its own limits
    /// (OzMlrs.PitchMinV2 / PitchMaxV2) keep the box, with the shot's kick and the launcher kick on top, inside the range the model step
    /// measured clear of the hull, the fenders, the red block, the hinge brackets and the antennas. Everything else of the slew (turn and
    /// pitch speeds, easing, tolerance, kick) is v1's, and k81b's steady solve (no jitter on target switches) serves both: its minimum range
    /// (4 m) is past v2's muzzle reach x 1.25 (2.9 m). Below its stop the launcher still fires (UnitAim.GunError: a barrel at its stop fires,
    /// as in Z); the rockets fly their arc from the tube mouth.
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>Bind: SlewFor's slew with the drawn model's own launcher limits (the MLRS v2); every other unit and model as it was.</summary>
        static Slew ModelSlew(Slew s, ZObject o, UnitView v)
        {
            if (o == null || !v || o.Kind != ZKind.Vehicle || o.Type != OzMlrs.Type || !OzMlrs.BoxModel(v.transform)) return s;   // the first v2 model's box only; the v2 on v1's model keeps v1's limits
            s.pitchMin = OzMlrs.PitchMinV2; s.pitchMax = OzMlrs.PitchMaxV2;
            return s;
        }

        /// <summary>Proof: the elevation limits this gun uses now (deg about its rest).</summary>
        public float PitchMinNow => slew.pitchMin;
        public float PitchMaxNow => slew.pitchMax;
    }
}
