using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only; the sim, its timing, paths and pick boxes are unchanged.
    /// Cause: the exit grow (View/UnitView.ExitGrow.cs) fitted a new vehicle to the door by its WIDTH only, against door widths of 3.0 m (vehicle
    /// factory) and 3.2 m (fort), and never below 62 % of its size. The gates the game draws are narrower and lower (measured on their meshes,
    /// <see cref="ExitDoors"/>): the vehicle factory's frame is 2.75 m wide and 1.76 m high over its sill, the fort's gate tunnel 2.54 m wide;
    /// every vehicle is taller than 1.76 m (2.5 to 4.3 m), so turrets, launchers and antennas went through the lintel and the wider v2 hulls
    /// through the jambs, v1 alike.
    /// Now, for a vehicle leaving a modelled gate straight along its axis: its drawn box at full size (every renderer, antennas included, measured
    /// once at the bind in the door's own frame: front, rear, each side, top) is fitted to the gate's whole visible profile (the hall's last
    /// part, the frame, the recess / tunnel and the ramp walls: width AND height, each segment with its own air), so it starts just small enough to
    /// pass every part of it; then every frame its largest scale that still keeps the box inside each segment it overlaps (at its drawn place,
    /// lateral offset included) is the ceiling, and it grows up to that ceiling smoothly (the old rate), never shrinking: it grows as its tail
    /// clears the frame and the recess, and is full size once it is out. A model swapped later (a new v2) is measured the same way at run time.
    /// Doors without a modelled gate, a gate left at an angle and the repair bay keep the width fit as before. Switch: <see cref="ExitDoors.Enabled"/>.
    /// </summary>
    public static class ExitDoors
    {
        /// <summary>The switch (off = the width-only door fit as before).</summary>
        public static bool Enabled = true;
        /// <summary>Air kept between the drawn box and each side wall (m) and under each lintel (m), the smallest scale the fit may ask for, and
        /// the grow rate (share of full size per second, the old rate).</summary>
        public static float AirSideM = 0.08f, AirTopM = 0.05f, MinScale = 0.3f, GrowRate = 0.6f;
        /// <summary>No lintel over a segment (open to the sky).</summary>
        public const float Open = 1e6f;

        /// <summary>One slab of a gate along its exit path, in prefab-root metres of the building (+z = out of the door): from z0 to z1 the clear
        /// half width (both sides of the door axis x = 0), the lintel's underside (Open = none) and the floor the units are drawn on there.</summary>
        public struct Seg
        {
            public float z0, z1, half, top, floor;
            public Seg(float z0, float z1, float half, float top, float floor) { this.z0 = z0; this.z1 = z1; this.half = half; this.top = top; this.floor = floor; }
        }

        /// <summary>The vehicle factory's gate (Models/Gates/vehicle_factory_Gate.glb under the prefab's Model node, the shutter slats left out:
        /// they are rolled up while a unit passes): the hall's last 0.6 m (the part the game camera sees through the door), the frame (jambs at
        /// +-1.375 m, lintel 1.905 m), the recess (walls +-1.93 m, then +-2.55 m, open top); outside from z 3.6.</summary>
        public static readonly Seg[] VehicleFactory =
        {
            new Seg(1.80f, 2.40f, 1.25f, 2.25f, 0.13f),
            new Seg(2.40f, 3.05f, 1.375f, 1.905f, 0.148f),
            new Seg(3.05f, 3.40f, 1.93f, Open, 0.13f),
            new Seg(3.40f, 3.60f, 2.549f, Open, 0.125f),
        };

        /// <summary>The fort's gate (Models/Gates/Fort_Gate.glb, the door leaves left out: open while a unit passes): the gate tunnel (+-1.475 m at
        /// its inner mouth, +-1.27 m along it, its lintel 4.105 to 4.42 m over the 1.29 m floor), the outer mouth (+-1.44 m), the ramp's side walls
        /// (+-2.22 m, then +-2.535 m, open top) down to its foot.</summary>
        public static readonly Seg[] Fort =
        {
            new Seg(3.20f, 3.60f, 1.475f, 4.42f, 1.29f),
            new Seg(3.60f, 3.80f, 1.27f, 4.105f, 1.29f),
            new Seg(3.80f, 6.00f, 1.27f, 4.34f, 1.29f),
            new Seg(6.00f, 7.10f, 1.44f, 4.26f, 1.30f),
            new Seg(7.10f, 7.90f, 2.224f, Open, 1.24f),
            new Seg(7.90f, 9.95f, 2.535f, Open, 0.50f),
        };

        /// <summary>
        /// The fit now keeps a hull out of what the game camera can SEE, not out of every part of the gate, measured
        /// roof is 2.36 to 2.47 m, over the hall's last part 2.53 m and more. A hull part over the lintel inside the frame block or the roof slab is
        /// inside solid geometry that no camera above sees: the lintel becomes the roof line (2.36 m over the frame, 2.50 m over the hall). A hull
        /// may reach <see cref="SideGiveM"/> into a door post (the post's top covers it from the game camera; its front face shows the hull's edge
        /// passing close to it); the hall's walls behind the posts take the posts' width. The fort's gate tunnel runs inside the solid wall (its top
        /// 6.3 m and more): its walls and ceiling are seen by no camera; the outer mouth (its face toward the camera, + <see cref="SideGiveM"/>) and
        /// the ramp's open walls stay. Hulls taller than the factory's roof (the MLRS, the mech, the crane) still shrink to pass under it: drawn
        /// bigger they would show through the roof. The hull's drawn place is still kept between the gate's real walls (<see cref="Walls"/>:
        /// centred when it is wider). Off (<see cref="SeenOnly"/> false) = the fit as before.</summary>
        public static bool SeenOnly = true;
        public const float SideGiveM = 0.20f;
        public static readonly Seg[] VehicleFactorySeen =
        {
            new Seg(1.80f, 2.40f, 1.375f + SideGiveM, 2.50f, 0.13f),
            new Seg(2.40f, 3.05f, 1.375f + SideGiveM, 2.36f, 0.148f),
            new Seg(3.05f, 3.40f, 1.93f, Open, 0.13f),
            new Seg(3.40f, 3.60f, 2.549f, Open, 0.125f),
        };
        public static readonly Seg[] FortSeen =
        {
            new Seg(3.20f, 3.60f, Open, Open, 1.29f),
            new Seg(3.60f, 3.80f, Open, Open, 1.29f),
            new Seg(3.80f, 6.00f, Open, Open, 1.29f),
            new Seg(6.00f, 7.10f, 1.44f + SideGiveM, 4.26f, 1.30f),
            new Seg(7.10f, 7.90f, 2.224f, Open, 1.24f),
            new Seg(7.90f, 9.95f, 2.535f, Open, 0.50f),
        };

        /// <summary>The profile of a gate kit's key (GateKitView.Key), null for a door without one: what the camera sees (2 Nov), or every part.</summary>
        public static Seg[] Of(string gateKey) => gateKey == "vehicle_factory" ? (SeenOnly ? VehicleFactorySeen : VehicleFactory) : gateKey == "fort" ? (SeenOnly ? FortSeen : Fort) : null;
        /// <summary>The gate's real walls (the hull's drawn place is kept between them while it is held on the door axis).</summary>
        public static Seg[] Walls(string gateKey) => gateKey == "vehicle_factory" ? VehicleFactory : gateKey == "fort" ? Fort : null;

        /// <summary>Statistics (the Play check): exits fitted by a gate profile, the smallest start scale, frames a ceiling fell under the drawn
        /// scale (the scale is never shrunk: such a frame could poke; the check counts them), exits that reached full size.</summary>
        public static int Fitted, Done, CeilingUnder;
        public static float MinFit = 1f;

        /// <summary>The largest scale (MinScale .. 1) at which the box (full-size reach in metres: front, rear, left, right, top over the pivot)
        /// with its pivot at p (building-local) passes every segment its tail has not left yet; k = metres per building-local unit; whole = every
        /// segment of the path (the bind's start fit).</summary>
        public static float Ceiling(Seg[] segs, Vector3 p, float front, float rear, float left, float right, float top, float k, bool whole)
        {
            if (Fits(segs, p, front, rear, left, right, top, k, 1f, whole)) return 1f;
            float lo = MinScale, hi = 1f;
            if (!Fits(segs, p, front, rear, left, right, top, k, lo, whole)) return lo;
            for (int i = 0; i < 14; i++) { float m = 0.5f * (lo + hi); if (Fits(segs, p, front, rear, left, right, top, k, m, whole)) lo = m; else hi = m; }
            return lo;
        }

        /// <summary>The box at scale s inside the clear width and under the lintel of every segment its tail has not left yet, the ones still ahead
        /// of it included (so it never grows inside the hall and then meets a lower or narrower part), its base at its drawn height or that
        /// segment's floor, the higher. A smaller box always passes where a bigger one does (the search above relies on it).</summary>
        static bool Fits(Seg[] segs, Vector3 p, float front, float rear, float left, float right, float top, float k, float s, bool whole)
        {
            float z0 = p.z - rear * s / k;                                                            // the tail (the front is never a limit: every segment ahead counts)
            float xl = p.x - left * s / k, xr = p.x + right * s / k, air = AirSideM / k, airTop = AirTopM / k;
            foreach (var g in segs)
            {
                if (!whole && z0 >= g.z1) continue;                                                   // its tail is out of this one
                if (xl < -g.half + air || xr > g.half - air) return false;
                float baseY = whole && !ExitFitK102.StartAtDrawnHeight ? g.floor : Mathf.Max(p.y, g.floor);                              // drawn on that floor or above it   // k102: the start fit too: a hull drawn higher than a lintel's floor is fitted from its drawn height
                if (g.top < Open * 0.5f && baseY + top * s / k > g.top - airTop) return false;
            }
            return true;
        }

        public static string Report() => $"ExitDoors: on {Enabled}, seen-only fit {SeenOnly}; exits fitted by a gate profile {Fitted}, smallest start scale {MinFit:0.000}, full size {Done}, frames with the ceiling under the drawn scale {CeilingUnder}";
    }

    public partial class UnitView
    {
        bool exitProfiled; ExitDoors.Seg[] exitSegs, exitWalls; Transform exitGate;
        float exitFront, exitBack, exitLeft, exitRight, exitTop;

        /// <summary>Proof: true while this exit is fitted by a gate profile; the gate's building transform and its profile.</summary>
        public bool ExitProfiled => exitProfiled && exitGrowing;
        public Transform ExitGate => exitGate;
        public ExitDoors.Seg[] ExitSegs => exitSegs;

        /// <summary>ExitDoorFit, first: the building's modelled gate and the hull's real drawn box take over the fit. False = no gate profile here
        /// (the width fit below runs as before).</summary>
        bool ExitFitBind()
        {
            exitProfiled = false; exitSegs = null; exitWalls = null; exitGate = null;
            if (!ExitDoors.Enabled || !exitStraight || exitFrom == null) return false;
            var fvs = FactoryViews.I; var fv = fvs ? fvs.For(exitFrom.Id) : null;
            if (!fv || !fv.Gate || !fv.Building) return false;
            var segs = ExitDoors.Of(fv.Gate.Key); if (segs == null) return false;
            var B = fv.Building;
            var dir = Quaternion.Euler(0f, exitYaw, 0f) * Vector3.forward;
            var dl = B.InverseTransformDirection(dir); dl.y = 0f;
            if (dl.sqrMagnitude < 1e-6f || dl.normalized.z < 0.98f) return false;                   // not straight out of the modelled door
            var right = Vector3.Cross(Vector3.up, dir);
            transform.localScale = exitBase;                                                         // measured at full size, on the door axis
            var c = transform.position;
            bool has = ExitFitK102.Reach(this, exitRs, c, dir, 1f, out float f, out float b, out float l, out float r, out float t);   // k102: the box measure shared with the step, which reads it again every frame as drawn (View/UnitView.ExitFit.K102.cs)
            transform.localScale = exitBase * exitCur;
            if (!has) return false;
            exitFront = f; exitBack = b; exitLeft = l; exitRight = r; exitTop = t;
            exitSegs = segs; exitWalls = ExitDoors.Walls(fv.Gate.Key); exitGate = B; exitProfiled = true;
            exitDir = dir; exitDrawn = c; exitDoor = true;
            float k = Mathf.Max(1e-4f, Mathf.Abs(B.lossyScale.z));
            var p = B.InverseTransformPoint(c); p.x = 0f;                                            // the whole path: on the door axis, each segment's floor
            exitFit = ExitDoors.Ceiling(segs, p, f, b, l, r, t, k, true);
            exitCur = exitFit; transform.localScale = exitBase * exitCur;
            ExitDoors.Fitted++; ExitDoors.MinFit = Mathf.Min(ExitDoors.MinFit, exitFit);
            if (exitFit < 0.999f) { ExitFitted++; ExitFitMin = Mathf.Min(ExitFitMin, exitFit); }
            return true;
        }

        /// <summary>ExitGrowStep for a gate-profiled exit: the ceiling at the place the hull was drawn last frame, approached at the grow rate,
        /// never above it and never shrinking.</summary>
        void ExitFitStep(float dt)
        {
            exitAge += dt;
            float ceil = 1f;
            if (exitFrom != null && !exitFrom.Destroyed && exitAge <= ExitMaxSeconds && exitGate)
            {
                float k = Mathf.Max(1e-4f, Mathf.Abs(exitGate.lossyScale.z));
                ExitFitDrawn();                                                                    // k102: the box as drawn this frame: tilt, bounce, turret, arms, legs (View/UnitView.ExitFit.K102.cs)
                ceil = ExitDoors.Ceiling(exitSegs, exitGate.InverseTransformPoint(exitDrawn), exitFront, exitBack, exitLeft, exitRight, exitTop, k, false);
            }
            if (ceil < exitCur - 0.001f) ExitDoors.CeilingUnder++;
            float next = Mathf.MoveTowards(exitCur, ceil, dt * ExitDoors.GrowRate);
            exitCur = ExitFitNext(exitCur, Mathf.Min(next, ceil), ceil, dt);                       // k102: grown as before, or eased down to a ceiling that fell under the drawn scale (a part would poke through)
            if (exitCur >= 0.999f) { exitCur = 1f; exitGrowing = false; ExitGrowDone++; ExitDoors.Done++; }
            transform.localScale = exitBase * exitCur;
        }
    }
}
