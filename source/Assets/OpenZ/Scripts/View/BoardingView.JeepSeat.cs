using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only (nothing under Scripts/Sim).
    /// <list type="bullet">
    /// <item>Before: the hips bone sat on the DriverSeat anchor, 5 cm in front of the seat cushion's front edge and 15 to 20 cm above the
    /// cushion, the face at the windscreen frame (the bulky types put the head through it at the old size).</item>
    /// <item>Now each robot type's hips sit <see cref="JeepSeatFit.back"/> further back and <see cref="JeepSeatFit.up"/> higher (negative:
    /// lower, down onto the cushion), in the DriverSeat anchor's own space: the shift follows the hull's drawn size (the vehicle size row,
    /// the jeep's share, the factory roll-out: K386) and its rocking suspension. The seat's crewScale gain is <see cref="JeepGrowBack"/>
    /// (the biggest type's), each type draws at its <see cref="JeepSeatFit.fit"/> share of it (never above 1: a fit only ever shrinks over
    /// the last two keys of a boarding, as before). Boarding and leaving: the seat key is the shifted seat, so the hop lands on it (no pop).</item>
    /// <item>The numbers are measured on the game's own models (the jeep, every robot type posed with the seated_driver clip; the grunt
    /// with his kit's helmet): the largest size with no part through the jeep (the boots / shins may stand in the closed tub under the
    /// side walls, the hands hold the wheel), the head at least 3 cm behind the windscreen frame, nothing through the seat back or the
    /// gun at its rest heading over its whole pitch range, the turning gun no closer to the driver than before, the hips on the cushion
    /// (at most 3 cm into it). What stops each type: the windscreen frame (the seated_driver clip leans the upper body forward), the seat
    /// back behind the torso, the boots under the hips (the cushion's front), the gun when it pitches up (the Electrobot's coils).</item>
    /// </list>
    /// Off (<see cref="JeepSeatBack"/> false, set before the level loads, like SeatsV2: the rig's crewScale is taken when the jeep is first
    /// seen) = the driver where and how big he was before this.
    /// </summary>
    public partial class BoardingView
    {
        public static bool JeepSeatBack = true;

        /// <summary>A robot type in the jeep's driver seat: its share of <see cref="JeepGrowBack"/>, its hips' shift back (+z of the seat
        /// anchor, away from the windscreen) and up (+y), in the jeep model's metres.</summary>
        public struct JeepSeatFit
        {
            public float fit, back, up;
            public JeepSeatFit(float fit, float back, float up) { this.fit = fit; this.back = back; this.up = up; }
        }

        /// <summary>The jeep driver's crewScale gain (x 0.62) while <see cref="JeepSeatBack"/> is on: the biggest type's (the grunt's).</summary>
        public const float JeepGrowBack = 1.217f;
        /// <summary>Per robot type (no RobotV3 = the BS-01 grunt): share, back, up. Drawn size against before (the jeep at 90 %, no
        /// share): grunt 1.12, psycho 0.92, tough 0.945, pyro 0.955, sniper 1.015, laser 1.00, Electrobot 1.05 (sizes_2nov: 1.01, 0.74,
        /// 0.75, 0.77, 0.85, 0.81, 0.92); every one with its head at least 3 cm behind the windscreen frame.</summary>
        public static readonly Dictionary<string, JeepSeatFit> JeepSeat = new Dictionary<string, JeepSeatFit>
        {
            { "grunt", new JeepSeatFit(1f, 0.05f, -0.12f) },
            { "psycho", new JeepSeatFit(0.822f, 0.11f, -0.16f) },
            { "tough", new JeepSeatFit(0.844f, 0.10f, -0.10f) },
            { "pyro", new JeepSeatFit(0.853f, 0.10f, -0.14f) },
            { "sniper", new JeepSeatFit(0.907f, 0.07f, -0.16f) },
            { "laser", new JeepSeatFit(0.893f, 0.10f, -0.14f) },
            { "electrobot", new JeepSeatFit(0.76f, 0f, -0.20f) },   // verify 2 Nov: 1.05 (the target; clean to about 1.067, then the gun pitched up meets his coils)
        };

        /// <summary>Check counters: shifted seat poses drawn (seated driver, boarding / leaving figure).</summary>
        public static int JeepSeatShifts;

        /// <summary>The rig is a jeep whose crew sits on its DriverSeat (SeatPose's choice) and the switch is on.</summary>
        static bool JeepDriver(Rig r) =>
            JeepSeatBack && r != null && r.obj != null && r.obj.Type == "jeep" && r.mode == Mode.Seat && r.driverSeat && !(I && I.jeepGunnerSeat && r.gunnerSeat);

        static JeepSeatFit JeepSeatOf(GameObject go) =>
            JeepSeat.TryGetValue(FigureType(go) ?? "grunt", out var k) ? k : new JeepSeatFit(1f / JeepGrowBack, 0f, 0f);   // no RobotV3 = the BS-01 grunt; a type not measured keeps its old size and place

        /// <summary>RigOf, the jeep: its crewScale gain (the old SeatGrow when off).</summary>
        static float JeepGrowNow(Rig r) => JeepDriver(r) ? JeepGrowBack : SeatGrowOf("jeep");

        /// <summary>CrewFit hook: the jeep driver's share of <see cref="JeepGrowBack"/> by his type (false: CrewFit as before).</summary>
        static bool JeepFit(Rig r, GameObject go, out float fit)
        {
            fit = 1f;
            if (!JeepDriver(r)) return false;
            fit = JeepSeatOf(go).fit;
            return true;
        }

        /// <summary>World shift of figure `go`'s hips from the DriverSeat anchor (zero for every other seat or with the switch off).</summary>
        static Vector3 JeepSeatShift(Rig r, GameObject go)
        {
            if (!JeepDriver(r) || !go) return Vector3.zero;
            var k = JeepSeatOf(go);
            if (k.back == 0f && k.up == 0f) return Vector3.zero;
            JeepSeatShifts++;
            return r.driverSeat.TransformVector(new Vector3(0f, k.up, k.back));
        }

        /// <summary>Check (Editor/OzJeepSeatCheck.cs): jeep `v`'s seated driver: his type, his hips in the DriverSeat anchor's space (model
        /// metres) and where they should be, his drawn size against the anchor's and the size the table asks for (crewBase x 0.62 x
        /// JeepGrowBack x fit: the hull's drawn share cancels).</summary>
        public static bool JeepSeatSample(UnitView v, out string type, out Vector3 hipsInSeat, out Vector3 want, out float sizeToSeat, out float wantSize)
        {
            type = null; hipsInSeat = want = Vector3.zero; sizeToSeat = wantSize = 0f;
            if (!I || !v || !I.rigs.TryGetValue(v, out var r) || !r.crew || !r.crewHips || !r.crew.activeInHierarchy || !JeepDriver(r)) return false;
            var k = JeepSeatOf(r.crew);
            type = FigureType(r.crew) ?? "grunt";
            hipsInSeat = r.driverSeat.InverseTransformPoint(r.crewHips.position);
            want = new Vector3(0f, k.up, k.back);
            sizeToSeat = Mathf.Abs(r.crew.transform.lossyScale.x) / Mathf.Max(1e-5f, Mathf.Abs(r.driverSeat.lossyScale.x));
            wantSize = r.crewBase * 0.62f * JeepGrowBack * k.fit;
            return true;
        }
    }
}
