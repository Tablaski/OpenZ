using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// later.
    /// <para>Zod: range is flat, 2D, the same from anywhere (ZObject.WithinAttackRadius, zobject.cpp:4760-4765). The game's own height is drawn only.</para>
    /// <para>The rule: a unit fires <see cref="ZWorld.HighGroundRangePct"/> % as far at a target BELOW it. High = a fort tower gun on any level, or any
    /// robot, vehicle or gun on the deck of a Unit editor platform (Sim/ZWorld.Platforms.cs), medium and high alike. A ramp is not high ground. A target on
    /// the same level, or higher, gets no change: <see cref="StandLevel"/> against <see cref="TargetLevel"/>. The flying shuttle is above everything. A fort's
    /// roof defender counts as high as a tower gun as a target, but gets no longer range himself: the rule names tower guns.</para>
    /// <para>The field <see cref="AttackRadius"/> stays the plain range: every read that means the range to one target goes through
    /// <see cref="RangeTo"/> (or <see cref="HighRange"/> for a second weapon's own range), so nothing else moves. The readers: WithinAttackRadius,
    /// ApproachPlan's "already in reach", the duel's bands (a tank on a deck stands and fires, it does not drive off for the last metres), the steady rules
    /// of the sniper and the Electrobot, the medium tank's "fired at from beyond its own range", the jeep's fear of a crewed gun, the CPU's "really fighting"
    /// (ZBot.FinishGame), the hatch driver's, the mech hand gun's and the crane rifle's own ranges. So the computer's units are under the same rule as
    /// the player's: it is the same test. Left on the plain range on purpose: the walking plans (ApproachPlan's spot, LosPlanAttack,
    /// NearestShotSpot): the spot lies on the ground, and a unit stops the moment WithinAttackRadius says yes; the computer's shuttle (ZBot.CpuShuttle):
    /// a flying ship gets no bonus against it; the fort's own gun and roof defender.</para>
    /// <para>Sight: the rules have no sight distance, only DV23's line, and height does not change it (Sim/ZSight.cs, the shuttle's rule included). The one
    /// reach that plays that part is the agro reach, range + agro_distance (40 px): the table's longest gun gains 20 px, so it never matters on the
    /// default numbers. When a custom range makes the longer range pass it, <see cref="AgroReachTo"/> stretches to the range and no further, so a unit
    /// never chases farther than it did, and a unit walking off a deck does not lose its leash halfway.</para>
    /// <para>The ring: <see cref="RingRadius"/>, the range at something below. View/AttackRangeRings*.cs, OzGunRange, the Unit editor's marks.</para>
    /// <para>Whole pixels: <see cref="ZWorld.HighGroundRange"/> is integer maths, rounded to the nearest pixel. No random draw, no event, no field written,
    /// no frame time. Switch <see cref="ZWorld.HighGroundDeviation"/>: View/ZGame.cs sets it at every level start from OPTIONS "HIGH GROUND SHOOTS FARTHER"
    /// (UI/OzHighGroundOption.cs, oz_high_ground, default on); off in 'Zod exact'; static default off, so headless worlds, the parity's Zod rows and every
    /// older counted check keep the rules as they were. Off = Zod's flat range, tick for tick. Pure C# like the rest of the Sim folder.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>The level this object fires from (ZWorld.HighLevel...): a fort tower gun, a deck, else the ground.</summary>
        public int StandLevel => IsTowerGun ? ZWorld.HighLevelTop : World != null ? World.PlatformDeckLevel(CenterX >> 4, CenterY >> 4) : ZWorld.HighLevelGround;

        /// <summary>The level this object is hit at: as <see cref="StandLevel"/>; the flying shuttle over everything, a roof defender as a tower gun.</summary>
        public int TargetLevel => ShuttleAirborne ? ZWorld.HighLevelAir : IsFortDefender ? ZWorld.HighLevelTop : StandLevel;

        /// <summary>The range `radius` (px) of one of this unit's weapons at t: longer when t stands below it. t null: `radius`.</summary>
        public int HighRange(int radius, ZObject t) => HighRangeFlat(radius, t) + TowerBodyPad(radius, t) + TowerDrawnPad(radius, t);   // 1.5 DV204 (kit fort_gun_target_r3): the range to an enemy fort tower gun is measured to the body of its tower (Sim/ZObject.FortGunAnswer.cs); 1.5 DV208 (kit tower_reach_dv208): to where its tower is drawn (Sim/ZObject.TowerDrawnReach.cs)

        /// <summary>DV162: the range of one weapon at t (to the centre of t).</summary>
        int HighRangeFlat(int radius, ZObject t)
        {
            if (!ZWorld.HighGroundDeviation || t == null) return radius;
            int s = StandLevel;
            return s > ZWorld.HighLevelGround && s > t.TargetLevel ? ZWorld.HighGroundRange(radius) : radius;
        }

        /// <summary>This unit's attack range at t, px.</summary>
        public int RangeTo(ZObject t) => HighRange(AttackRadius, t);

        /// <summary>The range the view draws: at something below it.</summary>
        public int RingRadius => ZWorld.HighGroundDeviation && StandLevel > ZWorld.HighLevelGround ? ZWorld.HighGroundRange(AttackRadius) : AttackRadius;

        /// <summary>Zod's agro reach (range + agro_distance), stretched to the range at t if that is longer.</summary>
        internal int AgroReachTo(ZObject t) => Math.Max(AttackRadius + World.S.agro_distance, RangeTo(t));
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV162 switch: see <see cref="ZObject"/>'s notes above (the game: OPTIONS, on by default, off in 'Zod exact'; headless default off).</summary>
        public static bool HighGroundDeviation = false;
        /// <summary>The range from high ground at a target below, per cent of the plain range.</summary>
        public const int HighGroundRangePct = 110;
        /// <summary>Levels: 0 ground and ramps; 1 a medium deck (4.5 m); 2 a high deck or a fort tower (9.25 m, the same height); 3 the flying shuttle.</summary>
        public const int HighLevelGround = 0, HighLevelMid = 1, HighLevelTop = 2, HighLevelAir = 3;

        /// <summary>A range from high ground, whole pixels, rounded to the nearest.</summary>
        public static int HighGroundRange(int radiusPx) => (radiusPx * HighGroundRangePct + 50) / 100;

        /// <summary>The level of a tile: a platform's deck, else the ground. (0 in every level: the table is null.) The deck only: rails and ramps are not ground to stand on.</summary>
        public int PlatformDeckLevel(int tx, int ty)
        {
            if (platPart == null || PlatformPartAt(tx, ty) != PlatPartDeck) return HighLevelGround;
            var p = PlatformOf(PlatformAtTile(tx, ty));
            return p == null ? HighLevelGround : p.Kind == PlatformHigh ? HighLevelTop : HighLevelMid;
        }

        /// <summary>The Unit editor's preview: a unit dropped at this point (px) would stand high.</summary>
        public bool HighGroundAt(int px, int py) => HighGroundDeviation && PlatformDeckLevel(px >> 4, py >> 4) > HighLevelGround;

        /// <summary>The gun being placed: this spot (tiles) is a fort tower pad.</summary>
        public bool HighGroundPad(int tx, int ty) => HighGroundDeviation && GunSpotOnFortPad(tx, ty);

        /// <summary>The parity report: no DV162 while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable HighGroundOff() => new HighGroundHold();
        sealed class HighGroundHold : IDisposable
        {
            readonly bool was = HighGroundDeviation;
            public HighGroundHold() { HighGroundDeviation = false; }
            public void Dispose() { HighGroundDeviation = was; }
        }
    }
}
