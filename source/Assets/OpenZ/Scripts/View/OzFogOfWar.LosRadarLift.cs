// OpenZ (GPL-3.0) - version 1.5 kit r14_r16: MAYHEM's line-of-sight fog lifted over the whole map while a radar of ours stands. View only.
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// While the player's side owns at least one STANDING radar (a radar building of his team, not a ruin: the radar circle's
    /// own test, View/OzFogOfWar.Radar.cs) the line-of-sight fog (View/OzFogOfWar.Los.cs) is off for the whole map: every explored cell counts as seen
    /// (no darkening, no grey veil, no scan lines; the enemy robots and vehicles there shown with their bars, dots and lamps). It fades out over
    /// <see cref="OzFowRadarLift.Seconds"/> when a radar is captured or rebuilt and back in over the same time, the same easing, when the last one is lost
    /// or ruined (<see cref="OzFowRadarLift"/>). The seen-now map keeps running underneath, so the fog comes back exactly as the units see then. Untouched:
    /// the cloud over ground never explored, the radar's own 48 m reveal circle, the rules, the computer. Hooks in View/OzFogOfWar.Los.cs: LosStart
    /// (<see cref="LosLiftStart"/>), LosStep (<see cref="LosLiftStep"/>), LosDecide (<see cref="LosLifted"/>), LosMaterial (<see cref="LosLiftLook"/>),
    /// LosCostText (<see cref="LosLiftText"/>). No shader change: the material's numbers carry it (x = the light kept -> 1, w = the scan lines -> 0, z = the
    /// block skipped when fully lifted).
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        readonly OzFowRadarLift lift = new OzFowRadarLift();

        /// <summary>Checks: this match's lift (null with no fog object).</summary>
        public static OzFowRadarLift RadarLift => I ? I.lift : null;
        /// <summary>Checks only (OpenZ/V1.5/LOS Fog: Radar Lift Check): -1 = the game (a standing radar of ours), 0 = as if none, 1 = as if one.</summary>
        public static int LosLiftForce = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void LiftStatics() { LosLiftForce = -1; }

        /// <summary>The player's side owns a standing radar now (the radar circle's own test).</summary>
        bool LosRadarOurs()
        {
            if (LosLiftForce >= 0) return LosLiftForce == 1;
            var list = W.Objects;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o != null && o.ProvidesRadarTo(player)) return true;
            }
            return false;
        }

        /// <summary>LosStart: the lift as it is at the match start, at once (a radar held from the start: no line-of-sight fog on the first frame).</summary>
        void LosLiftStart()
        {
            lift.Start(LosRadarOurs());
            if (lift.Radar) Debug.Log("[OpenZ FogOfWar] line-of-sight fog: a radar of ours stands at the start: lifted over the whole map (1.5 r14_r16)");
        }

        /// <summary>LosStep, every frame on the fog's clock: toward lifted while a radar of ours stands, back otherwise.</summary>
        void LosLiftStep(float dt)
        {
            int turn = lift.Step(dt, LosRadarOurs());
            if (turn > 0) Debug.Log($"[OpenZ FogOfWar] line-of-sight fog: a radar of ours stands: lifted over the whole map over {OzFowRadarLift.Seconds:0.0} s (from {lift.Value:0.00}; 1.5 r14_r16)");
            else if (turn < 0) Debug.Log($"[OpenZ FogOfWar] line-of-sight fog: no radar of ours stands any more: back over {OzFowRadarLift.Seconds:0.0} s (from {lift.Value:0.00}; 1.5 r14_r16)");
        }

        /// <summary>LosDecide: a unit's seen value with the lift (max(its own, the lift's floor)).</summary>
        byte LosLifted(byte v) => lift.Lift(v);

        /// <summary>LosMaterial: the line-of-sight fog's numbers with the lift: the light kept eased up to 1 (no darkening, no veil), the scan lines eased to 0;
        /// fully lifted: the block switched off (exactly the look without it).</summary>
        Vector4 LosLiftLook(Vector4 v)
        {
            float e = lift.Eased;
            if (e <= 0f) return v;
            v.x += (1f - v.x) * e;
            v.w *= 1f - e;
            if (lift.Full) v.z = 0f;
            return v;
        }

        /// <summary>The cost log's part.</summary>
        static string LosLiftText()
        {
            var f = I; if (!f) return "";
            return $", radar lift {f.lift.Value:0.00} (a radar of ours {(f.lift.Radar ? "stands" : "none")}, gained {f.lift.Gains}, lost {f.lift.Losses})";
        }
    }
}
