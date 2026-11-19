using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV183 "the BIZON CRUSH" (kit bizon_crush, on top of DV181 the Bizon, Sim/ZBigTanks.cs, and DV182 the Mammoth's ram,
    /// Sim/ZObject.MammothRam.cs, whose machinery it shares). Z has no crush; every number is OpenZ's.
    /// <para>WHO and WHEN: the same rules as the Mammoth's ram (a crewed Bizon of any team, the target close in the narrow arc in front of its hull,
    /// the way drivable, the nearest one, a short boost x <see cref="RamBoostPct"/> / 100, the order kept behind it, the cool-down after it).</para>
    /// <para>WHOM: an enemy crewed VEHICLE (any type, the Mammoth and the Bizon too) AND an enemy ground gun / turret (a cannon with its crew that
    /// stands on the ground: never a fort tower gun (<see cref="IsTowerGun"/>), never one on a raised deck or building (StandLevel above the
    /// ground)). Never a robot, a building, the shuttle, our own or a neutral empty one.</para>
    /// <para>HOW it differs: on front contact the target is CRUSHED: it is held at once (no step, no shot, no exit, no entry: the Mammoth's
    /// RamDoomHold) and the Bizon does not stop: it drives on straight over it, toward the spot where the target stands (the two hulls may overlap
    /// while it does: ZPathing.PairExempt through ZWorld.RamPair). When its front is two thirds across the target (the centres
    /// <see cref="CrushKillGap"/>: the Bizon's radius + a third of the target's apart) the target is destroyed with its crew the next tick (the
    /// kill the Bizon's), at the latest <see cref="CrushFallbackTicks"/> after the contact whatever happens (never alive, never left standing).
    /// The Bizon rolls on over the wreck to the target's spot, then its own order goes on (or it stands). The drive ends early when it is blocked
    /// (another unit's room, a wall) or after <see cref="CrushMaxTicks"/>; the target dies anyway. The view: the Bizon's front climbs onto the
    /// target and it rolls over it, the target squashed flat and broken into pieces under its tracks (View/OzBizonCrushView.cs).</para>
    /// <para>The way check for the start is the Mammoth's (to the contact point): something standing right beside or behind the target stops the
    /// roll-over at its own room (it is never driven through; the drive ends held, the target still dies by the 3 s rule) and is the next
    /// candidate after the cool-down.</para>
    /// <para>Deterministic: whole numbers only (squared distances, tick counts), object order, no random draw. Switch
    /// <see cref="ZWorld.BizonCrushDeviation"/>: View/ZGame.cs on at every start (off in 'Zod exact'); headless default off; the parity report and
    /// the counted checks hold it off; it also needs DV181's own switch. Off: not one field of any unit changes.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>The target is destroyed at the latest this long after the contact (4 s; 1.5 rework 14i: was 3 s: with DV187 the drive now
        /// goes on until the Bizon's rear is off the wreck, about 1.5-2.2 s at the crush speed).</summary>
        public const int CrushFallbackTicks = 400;
        /// <summary>The longest drive over the target after the contact (6.5 s: the contact to its centre is about 50 px at x 1.35 its speed),
        /// then its order goes on.</summary>
        public const int CrushMaxTicks = 650;

        bool crushOn; int crushEndX, crushEndY; long crushStartTick;

        /// <summary>DV183: this vehicle was crushed by a Bizon (its view squashes it flat and breaks it under the tracks).</summary>
        public bool RamCrushed;
        /// <summary>DV183: this Bizon is driving over the target it crushed.</summary>
        public bool Crushing => ram != null && crushOn;

        /// <summary>A Bizon (the crush machine) rather than a Mammoth (the ram).</summary>
        public bool IsCrusher => Kind == ZKind.Vehicle && Type == ZBigTanks.Bizon;

        /// <summary>The ram / crush rules are on for this tank's type (DV182 + DV180 for the Mammoth, DV183 + DV181 for the Bizon).</summary>
        bool RamSwitchOn => Type == ZBigTanks.Mammoth ? ZWorld.MammothRamDeviation && ZWorld.MammothDeviation
                          : Type == ZBigTanks.Bizon && ZWorld.BizonCrushDeviation && ZWorld.BizonDeviation;

        /// <summary>The kind of target: a vehicle (both); for the Bizon also a ground gun or turret (not on a tower, a deck or a building).</summary>
        bool RamKindOk(ZObject t) => t.Kind == ZKind.Vehicle || (IsCrusher && CrushGunOk(t));

        /// <summary>A gun / turret standing on the ground: a cannon, not a fort tower gun, on the ground level (no raised deck, no building).</summary>
        static bool CrushGunOk(ZObject t) => t.Kind == ZKind.Cannon && !t.IsTowerGun && !t.IsFortDefender && t.StandLevel == ZWorld.HighLevelGround;

        /// <summary>The centre distance at which the target is crushed: the Bizon's radius + a third of the target's (its front two thirds across).</summary>
        int CrushKillGap(ZObject t) => ZPathing.Radius(this) + ZPathing.Radius(t) / 3;

        /// <summary>RamWayClear: how far the centre's run must be drivable: to the contact point (both tanks; the Bizon's roll-over beyond it is
        /// stopped by any other unit's room, never driven through).</summary>
        long RamReach(long len, ZObject t)
        {
            long r = len - (ZPathing.Radius(this) + ZPathing.Radius(t));
            return r < 0 ? 0 : r;
        }

        /// <summary>RamStep, on front contact (instead of the Mammoth's RamImpact): the target crushed and held, the drive over it begins.</summary>
        void CrushImpact(ZObject t, long now)
        {
            long dx = t.CenterX - CenterX, dy = t.CenterY - CenterY, len = RamIsqrt(dx * dx + dy * dy);
            if (len <= 0) len = 1;
            int ra = ZPathing.Radius(this);
            t.RamDoomBy = Id; t.RamDoomTick = now + CrushFallbackTicks; t.RamHitTick = now; t.RamCrushed = true;
            t.RamDirX = (int)(dx * 1024 / len); t.RamDirY = (int)(dy * 1024 / len);
            t.RamHitX = CenterX + (int)(dx * ra / len); t.RamHitY = CenterY + (int)(dy * ra / len);
            t.RamDoomStop();
            crushOn = true; crushStartTick = now; crushEndX = t.CenterX; crushEndY = t.CenterY; ram.noMoveTicks = 0;
            CrushRollOffEnd(t);                                                          // 1.5 DV187 (kit bizon_crush_climb): the drive goes on past the wreck to the far side (Sim/ZObject.CrushRollOff.cs)
            World.BizonCrushHits++;
            World.Emit(ZEventType.BizonCrush, this, t, t.RamHitX, t.RamHitY, 1f, CrushFallbackTicks, true);   // the contact (the view: the crumple, the climb, the squash, the line)
        }

        /// <summary>RamStep while crushing: straight on over the target to its spot, the boost on; the kill when the front is two thirds across.
        /// True = it drove (or stood held) this tick (the orders wait).</summary>
        bool CrushDrive(ZObject t, double dt, long now)
        {
            if (now - crushStartTick > CrushLimitTicks) { CrushFinish("time"); return false; }   // 1.5 DV187 (kit bizon_crush_climb): + CrushRollOffTicks for the longer drive (Sim/ZObject.CrushRollOff.cs)
            if (t != null && t.RamDoomBy == Id && !t.Destroyed && t.KillAt < 0 && t.RamDoomTick > now + 1)
            {
                if (crushRolled) CrushClearKill(t, now);                                 // 1.5 rework 14i (DV187): destroyed when the Bizon's rear has rolled off it (Sim/ZObject.CrushRollOff.cs)
                else
                {
                    long kx = t.CenterX - CenterX, ky = t.CenterY - CenterY, kg = CrushKillGap(t);
                    if (kx * kx + ky * ky <= kg * kg) { t.RamDoomTick = now + 1; World.BizonCrushOnTime++; }   // squashed under the front: destroyed next tick
                }
            }
            if (CenterX == crushEndX && CenterY == crushEndY) { CrushFinish("over"); return false; }
            int lx = LocX, ly = LocY; double xo = XOver, yo = YOver;
            if (RamChainBlocks()) { CrushFinish("held"); return false; }               // 1.5 DV195 (kit ram_chain_n24_n25): the next target at its room on the way: on to it in this tick (Sim/ZObject.RamChain.cs)
            SetTarget(crushEndX, crushEndY);
            double tile = World.Map.TileWalkSpeed(CenterX, CenterY);
            if (World.Pathing != null) tile = World.Pathing.SafeTileSpeed(this, tile);
            RealMoveSpeed = MoveSpeed * tile * DamagedSpeed() * RunSpeed();
            SetVelocity();
            World.BizonCrushDriveTicks++;
            if (!ProcessMove(dt, true, out _, out _) && !(CenterX == crushEndX && CenterY == crushEndY)) { CrushFinish("blocked"); return true; }
            bool moved = LocX != lx || LocY != ly || XOver != xo || YOver != yo;
            if (!moved) { if (++ram.noMoveTicks >= RamHeldTicks || RamChainReady() /* 1.5 DV195: the next target holds it: on to it now */) CrushFinish("held"); return true; }
            ram.noMoveTicks = 0;
            if (CenterX == crushEndX && CenterY == crushEndY) CrushFinish("over");
            else if (CrushStalled(now)) CrushFinish("held");                            // 1.5 rework 14i (DV187): no progress along the crush (Sim/ZObject.CrushRollOff.cs)
            return true;
        }

        /// <summary>The drive over the target is over: the ram's end with a hit (its waypoint goes, the order goes on, the cool-down).</summary>
        void CrushFinish(string why)
        {
            CrushRollOffFinish();                                                        // 1.5 rework 14i (DV187): a drive that ends before the rear is off: the target dies right after (Sim/ZObject.CrushRollOff.cs)
            crushOn = false;
            World.NoteCrushEnd(why);
            RamEnd("hit", true);
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV183 the BIZON CRUSH (Sim/ZObject.BizonCrush.cs). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool BizonCrushDeviation = false;

        /// <summary>The parity report and the older counted checks: off while this lives.</summary>
        public static IDisposable BizonCrushOff() => new BizonCrushOffHold();
        sealed class BizonCrushOffHold : IDisposable
        {
            readonly bool was = BizonCrushDeviation;
            public BizonCrushOffHold() { BizonCrushDeviation = false; }
            public void Dispose() { BizonCrushDeviation = was; }
        }

        /// <summary>Statistics (the counted check): crush starts, contacts, kills, kills at the front's pass (not the fallback), ticks driven over
        /// a target, and the drive's ends by reason.</summary>
        public int BizonCrushStarts, BizonCrushHits, BizonCrushKills, BizonCrushOnTime, BizonCrushDriveTicks, BizonCrushEndsOver, BizonCrushEndsBlocked,
                   BizonCrushEndsHeld, BizonCrushEndsTime;

        internal void NoteCrushEnd(string why)
        {
            switch (why)
            {
                case "over": BizonCrushEndsOver++; break;
                case "blocked": BizonCrushEndsBlocked++; break;
                case "held": BizonCrushEndsHeld++; break;
                case "time": BizonCrushEndsTime++; break;
            }
        }
    }
}
