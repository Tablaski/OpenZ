using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV209 "level 17's traffic lights stand at the kerb" (1.5, kit traffic_lights_53, owner addition 53, 12 Nov
    /// <para>The traffic lights of the city levels are drawn at x0.48 of the old size (as tall as the street lamps beside them). Their arm is
    /// shorter now, so on level 17 each pole moves to the kerb along its arm (the heads stay over the same lane, kit l18_street_r3's rule).
    /// Level 17's two lights are DV67 map items (Sim/ZWorld.DetailProps.cs, footprint from <see cref="ZDetailPropsData"/>): their item moves
    /// with the drawn pole, C01 from tile (41, 98) to (43, 98), C02 from (68, 98) to (66, 98). Nothing else: the same hit points, the same rules
    /// (explosives only, no sight block); they block no tile, as at their old tiles (not passable at load), so every path stays as it was.
    /// Level 18's lights are view only (no map item).</para>
    /// <para>Switch <see cref="TrafficLightsKerbDeviation"/>: View/ZGame.cs turns it on at every level start, off in 'Zod exact' (oz_zod_exact = 1,
    /// where DV67 has no props at all); static default off, so headless worlds and every older counted check keep the two items where they were.
    /// Read once, when the world adds its detail props. Data only: no float, no random draw. Parity row: Editor/OpenZParity.V15TrafficLights.cs.</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV209 on: level 17's two traffic-light items on their kerb tiles (ZDetailPropsData). Off = the tiles before 1.5 (41, 98) / (68, 98).</summary>
        public static bool TrafficLightsKerbDeviation = false;
        static readonly string[] TrafficLightUids = { "C01", "C02" };
        static readonly int[][] TrafficLightTilesBefore = { new[] { 41, 98 }, new[] { 68, 98 } };

        /// <summary>The footprint tiles of detail prop k of a set: the data's, or with DV209 off level 17's two lights' tiles before 1.5.</summary>
        static int[] DetailPropTiles(ZDetailPropsData.Set set, int k)
        {
            var t = set.Tiles[k];
            if (TrafficLightsKerbDeviation || !ReferenceEquals(set, ZDetailPropsData.L17)) return t;
            int i = Array.IndexOf(TrafficLightUids, set.Uids[k]);
            return i >= 0 ? TrafficLightTilesBefore[i] : t;
        }

        /// <summary>DV209 on: level 17's two light items block no tile at the kerb, as at their old tiles (not passable at load: 0 tiles blocked),
        /// so every path stays as it was (counted: without this the two kerb tiles would close, 2 tiles newly blocked).</summary>
        static bool DetailPropKeepsPath(ZDetailPropsData.Set set, int k) =>
            TrafficLightsKerbDeviation && ReferenceEquals(set, ZDetailPropsData.L17) && Array.IndexOf(TrafficLightUids, set.Uids[k]) >= 0;

        /// <summary>Holds DV209 off (the probes), puts the value back on Dispose.</summary>
        public static IDisposable TrafficLightsKerbOff() => new TrafficLightsKerbHold(false);
        /// <summary>Holds DV209 on (its parity row), puts the value back on Dispose.</summary>
        public static IDisposable TrafficLightsKerbOn() => new TrafficLightsKerbHold(true);
        sealed class TrafficLightsKerbHold : IDisposable
        {
            readonly bool was = TrafficLightsKerbDeviation;
            public TrafficLightsKerbHold(bool on) { TrafficLightsKerbDeviation = on; }
            public void Dispose() { TrafficLightsKerbDeviation = was; }
        }
    }
}
