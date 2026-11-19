using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// on the sand by the river's surf). View only.
    /// <para>Cause (measured on L02's river bank, the track meshes' vertices against the walk ground): a planted belt (TrackGround,
    /// UnitView.GroundGear.cs) reads the ground at its stations, but a station more than TrackGround.StepIgnoreM (0.7 m) above or below the
    /// ground under the tank's centre was taken as a bridge deck's edge or a wall and given the centre's height. A river bank is a real slope of
    /// about 1.1 m over 2 m: with the tank's centre down by the water and the end of its belt still up on the sand, that end was put at the
    /// centre's (water) height, so the belt's end sank up to 35 cm into the sand of the bank (light tank; a big tank up to 76 cm at another
    /// steep bank).</para>
    /// <para>Now a station that far off the centre keeps its own ground when the ground between the centre and it is one continuous slope (no
    /// step of more than <see cref="SlopeStepM"/> between probes <see cref="SlopeProbeM"/> apart: a bank, a beach, a dune, a crater rim); a real
    /// jump (a bridge deck's edge, a wall, a cliff) is the centre's ground as before. The belt then rests on the bank (its bottom clears the
    /// highest ground under it, as on every other slope) and the hull rides on it. Every planted vehicle (light, medium, heavy v1 / v2, MLRS,
    /// Bizon, Mammoth). <see cref="On"/> off = as before.</para>
    /// </summary>
    public static class TrackGroundSlope
    {
        /// <summary>N8: a continuous slope keeps its own ground under a belt station (on).</summary>
        public static bool On = true;
        /// <summary>N8: the probes' spacing (m) and the largest step between two probes that is still a slope (m: about 52 deg).</summary>
        public static float SlopeProbeM = 0.35f, SlopeStepM = 0.45f;
        /// <summary>N8 proof: belt stations that kept their own ground on a continuous slope (before: the centre's height).</summary>
        public static int Kept;
    }

    public partial class UnitView
    {
        /// <summary>The ground from the tank's centre (p0, its ground gc) to the station w (x, z) runs as one continuous slope: no step between two
        /// probes SlopeProbeM apart rises or falls more than SlopeStepM (a bank, not a deck edge or a wall).</summary>
        bool GearSlopeRun(Vector3 p0, Vector3 w, float gc)
        {
            if (!TrackGroundSlope.On || game == null) return false;
            float dx = w.x - p0.x, dz = w.z - p0.z, len = Mathf.Sqrt(dx * dx + dz * dz);
            int n = Mathf.Clamp(Mathf.CeilToInt(len / Mathf.Max(0.05f, TrackGroundSlope.SlopeProbeM)), 1, 40);
            float prev = gc;
            for (int k = 1; k <= n; k++)
            {
                float t = (float)k / n;
                float h = game.GroundY(p0.x + dx * t, p0.z + dz * t);
                if (Mathf.Abs(h - prev) > TrackGroundSlope.SlopeStepM) return false;                     // a jump: a deck edge, a wall, a cliff
                prev = h;
            }
            TrackGroundSlope.Kept++;
            return true;
        }
    }
}
