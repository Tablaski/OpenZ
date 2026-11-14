using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// <list type="bullet">
    /// <item>Who: the grunt figure (the BS-01 robot: no RobotV3 type and no OzHeroLook) standing in the top hatch of the light tank (the v2
    /// model the game draws; a testing panel v1 stays as before), and of the medium and heavy tank with their switch. A grunt crew with Alan or
    /// Brad shows this plain grunt figure in the hatch too, so it grows with the row; their own boarding hop figure (OzHeroLook) and EvilZod
    /// do not. Every other robot type, every other vehicle, seat and hatch: as before.</item>
    /// <item>Size: <see cref="HatchGruntRow.size"/> x his size before (through CrewFit, so the boarding hop grows him over its last two keys and
    /// the way out shrinks him back over its first two, as the Electrobot's fit always did; a sniped commander flies at it).</item>
    /// <item>Place: <see cref="HatchGruntRow.fwd"/> toward the turret's front, <see cref="HatchGruntRow.face"/> along his own facing,
    /// <see cref="HatchGruntRow.up"/> up, each x his drawn size (so the shift follows the vehicle size row and the factory roll-out, K386, as
    /// he does). Up goes in before the light tank v2's weapon lift (its need sees it), the sideways shift after it (after its arms-in
    /// centring, which would take a sideways shift back); the shift toward the turret front comes in as he rises (x SmoothStep of the pop),
    /// so he goes up and down through the middle of the opening as before.</item>
    /// <item>The light tank v2's weapon lift cap (LightV2LiftMax) grows with his size, so his rifle still comes up clear of the plate in
    /// every frame it did before (his whole figure is bigger, so is the lift it needs).</item>
    /// <item>The numbers are measured offline on the game's own models (the three v2 tanks with the lid open, the BS-01 grunt with his kit and
    /// rifle posed by his hatch clips): over every turret angle (5 deg), the gun's whole pitch range, its recoil, his kick, a hit's jolt and
    /// front and 7 cm up (at the tank's full size): the gun never meets him there (as today); resting (arms in, his rest look) and shooting
    /// (at any angle) the open lid and the hatch rim come no closer than today; only the short jolt and rise / sink pass the rim deeper.</item>
    /// <item>The medium and the heavy tank have no room for a bigger grunt: resting, his rifle already lies on the gun's mantlet in front of him
    /// and his pack is at the open lid behind him. Their rows (x1.08, moved back along his facing and up) still let the gun go deeper into his
    /// rifle at a few gun angles and put his pack up to 5 cm into the open lid, so they are off (<see cref="HatchGruntMediumHeavy"/> false):
    /// </item>
    /// </list>
    /// Off (<see cref="HatchGruntOn"/> false) = the grunt as before. View only (nothing under Scripts/Sim).
    /// </summary>
    public partial class BoardingView
    {
        public static bool HatchGruntOn = true;
        /// <summary></summary>
        public static bool HatchGruntMediumHeavy = true;

        public readonly struct HatchGruntRow
        {
            /// <summary>x his size before; the shifts in units of his drawn size (x his world scale = metres).</summary>
            public readonly float size, fwd, face, up;
            public HatchGruntRow(float size, float fwd, float face, float up) { this.size = size; this.fwd = fwd; this.face = face; this.up = up; }
        }

        /// <summary>Per tank type: the grunt's size and place in its top hatch (BoardingView.HatchGrunt.cs). A type not listed: as before.</summary>
        public static readonly Dictionary<string, HatchGruntRow> HatchGrunt = new Dictionary<string, HatchGruntRow>
        {
            { "light", new HatchGruntRow(1.07f, 0.0418f, 0f, 0.0976f) },        // x1.07; 3 cm toward the turret front, 7 cm up (the tank at full size)
            { ZMTank.Type, new HatchGruntRow(1.08f, 0f, -0.1382f, 0.0415f) },  // with HatchGruntMediumHeavy: x1.08; 10 cm back along his facing, 3 cm up
            { "heavy", new HatchGruntRow(1.08f, 0f, -0.0896f, 0.0597f) },       // with HatchGruntMediumHeavy: x1.08; 6 cm back along his facing, 4 cm up
        };

        /// <summary>Frames a grunt was placed in a tank's top hatch by this row (the check).</summary>
        public static int HatchGruntFrames;

        /// <summary>The grunt figure: the BS-01 robot (no RobotV3 type) that is not a hero (EvilZod, Alan and Brad carry OzHeroLook).</summary>
        static bool IsHatchGrunt(GameObject go) => go && !go.GetComponent<RobotV3>() && !go.GetComponent<OzHeroLook>();

        static bool HatchGruntOf(Rig r, GameObject go, out HatchGruntRow row)
        {
            row = default;
            return HatchGruntOn && r != null && r.obj != null && r.lid && r.mode == Mode.Roof && HatchGrunt.TryGetValue(r.obj.Type, out row)
                   && (r.obj.Type == OzLightTank.Type ? IsLightV2Hatch(r) : HatchGruntMediumHeavy) && IsHatchGrunt(go);   // the light row: the v2 tank only (the one measured; a testing panel v1 stays as before)
        }

        /// <summary>CrewFit hook: the grunt's size in a tank's top hatch (false: CrewFit as before).</summary>
        static bool HatchGruntFit(Rig r, GameObject go, out float fit)
        {
            fit = 1f;
            if (!HatchGruntOf(r, go, out var row)) return false;
            fit = row.size;
            return true;
        }

        /// <summary>The light tank v2's weapon lift cap factor: the grunt's size in its hatch, 1 for everyone else.</summary>
        static float HatchGruntLift(Rig r) => r != null && HatchGruntOf(r, r.crew, out var row) ? row.size : 1f;

        /// <summary>World shift up for the hatch grunt of drawn size s (zero for everyone else).</summary>
        static Vector3 HatchGruntUp(Rig r, float s) => r != null && HatchGruntOf(r, r.crew, out var row) && row.up != 0f ? r.root.up * (row.up * s) : Vector3.zero;

        /// <summary>World shift sideways (toward the turret's front, along his facing) for the hatch grunt of drawn size s (zero for everyone else).</summary>
        static Vector3 HatchGruntSide(Rig r, float s)
        {
            if (r == null || !HatchGruntOf(r, r.crew, out var row) || (row.fwd == 0f && row.face == 0f)) return Vector3.zero;
            var up = r.root.up;
            var front = Vector3.ProjectOnPlane(r.turret ? -r.turret.forward : -r.root.forward, up);                // vehicles face -z
            var face = Vector3.ProjectOnPlane(r.crewAim * Vector3.back, up);                                          // robots face -z
            front = front.sqrMagnitude > 1e-8f ? front.normalized : Vector3.zero;
            face = face.sqrMagnitude > 1e-8f ? face.normalized : Vector3.zero;
            return (front * (row.fwd * Mathf.SmoothStep(0f, 1f, r.crewPop)) + face * row.face) * s;   // toward the front: comes in as he rises (up / down through the middle of the opening, as before)
        }

        /// <summary>PlaceCommander hooks: before LightV2HatchClear (up) and after it (sideways).</summary>
        void HatchGruntPlaceUp(Rig r, Transform t, ref Vector3 shown, float s)
        {
            var d = HatchGruntUp(r, s);
            if (d == Vector3.zero) { if (r != null && HatchGruntOf(r, r.crew, out _)) HatchGruntFrames++; return; }
            t.position += d; shown += d; HatchGruntFrames++;
        }
        void HatchGruntPlaceSide(Rig r, Transform t, float s) { var d = HatchGruntSide(r, s); if (d != Vector3.zero) t.position += d; }

        /// <summary>Check (Editor/OzHatchGruntCheck.cs): tank `v`'s hatch figure: grunt or not, its size against the size before (crewBase x
        /// crewScale, the hull's share included), the size the row asks for (1 for every other figure), the pop.</summary>
        public static bool HatchGruntSample(UnitView v, out bool grunt, out string type, out float sizeToBefore, out float want, out float pop)
        {
            grunt = false; type = null; sizeToBefore = want = pop = 0f;
            if (!I || !v || !I.rigs.TryGetValue(v, out var r) || !r.crew || !r.lid || r.mode != Mode.Roof) return false;
            grunt = HatchGruntOf(r, r.crew, out var row);
            type = FigureType(r.crew) ?? (r.crew.GetComponent<OzHeroLook>() ? "hero" : "grunt");
            sizeToBefore = Mathf.Abs(r.crew.transform.localScale.x) / Mathf.Max(1e-5f, r.crewBase * r.crewScale * (grunt ? 1f : CrewFit(r, r.crew)));
            want = grunt ? row.size : 1f;
            pop = r.crewPop;
            return true;
        }
    }
}
