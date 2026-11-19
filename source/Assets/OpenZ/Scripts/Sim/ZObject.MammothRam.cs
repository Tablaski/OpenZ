using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV182 "the MAMMOTH RAM" (kit mammoth_ram, on top of DV180 the Mammoth, Sim/ZBigTanks.cs). Z has no ram; every number is
    /// OpenZ's. The Mammoth's front carries spikes: when an ENEMY vehicle is close in front of its hull it rams it by itself.
    /// <para>WHO: a crewed Mammoth of any team (player or computer) with its switch on, moving or standing, not stunned, not on HOLD POSITION, not
    /// leaving a building, not in a fort door, not mid way through an entry, repair, crane job, factory exit, lava walk or step aside (its first
    /// waypoint, if any, is a plain MOVE, ATTACK or AGRO one). WHOM: an enemy VEHICLE with its crew inside (never our own, never a neutral empty
    /// one, never a robot, a cannon, a building or the shuttle), not already rammed, not inside a building.</para>
    /// <para>WHEN (every <see cref="RamLookTicks"/> ticks): its hull's heading is known (it has moved at least once) and the vehicle is inside the
    /// narrow arc in front of it (+/- <see cref="RamStartArcDeg"/> degrees of its heading, an integer test: |cross| x 1000 &lt;= dot x
    /// <see cref="RamStartTanMilli"/>) and close: at most <see cref="RamStartGapPx"/> px (2 tiles) between the two hulls (centre distance less both
    /// spacing radii). Several in that arc: the NEAREST (squared centre distance, whole numbers), ties by object order. Its way there must be
    /// drivable: the Mammoth's box swept along the line to the contact point stays on free vehicle tiles, dry, out of the lanes' zones (bridge decks,
    /// one-lane chokepoints; a target standing in one is never rammed), and no other unit (only enemy robots, who are run over as always, DV65) stands within the spacing of that sweep.
    /// Nothing that would need a turn, a reverse or a long run: a target not in front and near is never rammed.</para>
    /// <para>HOW: a MOVE waypoint (refId <see cref="ZWorld.RefRam"/>) is put in FRONT of its list (the player's order stays behind it, untouched)
    /// and the ram drives itself, every tick straight at the target's centre, x <see cref="RamBoostPct"/> / 100 its speed (RunSpeed, only while
    /// the ram lasts). The spacing gate lets the two hulls touch (ZPathing.PairExempt: the Mammoth and its target), every other unit keeps its
    /// room. It does not step aside for shells while it rams.</para>
    /// <para>IMPACT: front contact (at most <see cref="RamContactPx"/> px between the hulls, the target inside +/- <see cref="RamKeepArcDeg"/> degrees
    /// of the heading it moved in this tick, the Mammoth moved forward this tick): the target is RAMMED: it stops, drops its target and orders and
    /// is held (no step, no shot, no exit, no entry) for <see cref="RamFlipTicks"/> ticks (the view lifts it on the spikes and flips it over),
    /// then it is destroyed with its crew (ZWorld.MammothRamTick: the kill is the Mammoth's, veterancy as any kill). Nothing else is hurt by the
    /// ram: a second vehicle beside or behind the target is not touched (only a later valid ram of its own can hit it); a target already rammed is
    /// never rammed again (no double kill); a side or rear contact does nothing special; robots are run over as before (DV65).</para>
    /// <para>END (no hit): the target gone or rammed by another Mammoth, no longer an enemy crewed vehicle, out of the arc it started in (+/-
    /// <see cref="RamKeepArcDeg"/> degrees of the start heading: it never chases round), farther than <see cref="RamKeepGapPx"/> px, the way
    /// blocked (checked every <see cref="RamCheckTicks"/> ticks, a wall in the step, or no progress for <see cref="RamHeldTicks"/> ticks), its own
    /// crew gone, or <see cref="RamMaxTicks"/> ticks. The ram's waypoint goes, the player's order (or the computer's) starts again from here exactly
    /// as it was (a fresh first waypoint: its path from the new spot), or it stands when it had none. A new order (ZWorld.SendWaypoints) ends it
    /// at once. After a hit it waits <see cref="RamCoolTicks"/> ticks before it looks again (no chain hunt), after a ram that ended without a hit
    /// <see cref="RamRetryTicks"/> ticks (no flip-flop between starting and ending).</para>
    /// <para>Deterministic: whole numbers only in every decision (squared distances, integer dot / cross products, the heading as whole
    /// numbers x 1024, tick counts), object order, no random draw, no clock. The one double is the heading's length once per look (Math.Sqrt,
    /// exact in IEEE) and the boost factor handed to Zod's own RunSpeed. Switch <see cref="ZWorld.MammothRamDeviation"/>: View/ZGame.cs on at every
    /// start (off in 'Zod exact'); headless default off; the parity report and the counted checks hold it off; it also needs DV180's own switch.
    /// Off: not one field of any unit changes.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>Look for a ram every this many ticks (0.05 s).</summary>
        public const int RamLookTicks = 5;
        /// <summary>The start arc: +/- this many degrees of the hull's heading (tan 30 deg = 0.577).</summary>
        public const int RamStartArcDeg = 30, RamStartTanMilli = 577;
        /// <summary>The keep / impact arc: +/- this many degrees (tan 40 deg = 0.839): a target that slides a little sideways is still rammed, one
        /// that leaves the front is not chased.</summary>
        public const int RamKeepArcDeg = 40, RamKeepTanMilli = 839;
        /// <summary>The farthest gap between the hulls (px) at which a ram starts: 2 tiles (4 m).</summary>
        public const int RamStartGapPx = 32;
        /// <summary>The gap at which a ram in progress gives up (the target drives away faster than the Mammoth).</summary>
        public const int RamKeepGapPx = 48;
        /// <summary>The hulls touch: at most this many px between them.</summary>
        public const int RamContactPx = 3;
        /// <summary>The speed boost while it rams (percent of its speed): the Mammoth's ram speed (x 6.00 of its 1 m/s = 6 m/s). 1.5 rework 14i
        /// <see cref="RamRampTicks"/>.</summary>
        public const int RamBoostPct = 600;
        /// <summary>
        /// Reached by the same acceleration.</summary>
        public const int CrushBoostPct = 800;
        /// <summary>1.5 rework 14i: the boost builds up from the tank's speed at the start (its plain speed when it was moving, 0 when it stood) to
        /// the full boost over this many ticks (1.3 s), in whole-percent steps every tick, then holds.</summary>
        public const int RamRampTicks = 130;
        /// <summary>This tank's full boost (percent): the Mammoth's ram or the Bizon's crush.</summary>
        int RamFullPct => IsCrusher ? CrushBoostPct : RamBoostPct;
        /// <summary>The longest ram (4.5 s), the longest stand without progress (0.3 s), the re-check of the way (0.1 s).</summary>
        public const int RamMaxTicks = 450, RamHeldTicks = 30, RamCheckTicks = 10;
        /// <summary>After a hit no new ram for 3 s; after a ram without a hit 1 s.</summary>
        public const int RamCoolTicks = 300, RamRetryTicks = 100;
        /// <summary>A rammed vehicle is held this long (0.8 s: from the tipping point it falls over onto its roof in the view, lands, then the
        /// blast), then destroyed. 1.5 rework 14i: was 1.3 s (the flip launched it up in the air "too fast for no reason").</summary>
        public const int RamFlipTicks = 80;

        sealed class RamState
        {
            public int targetId = -1, hx, hy, noMoveTicks, startPct;   // 1.5 rework 14i: startPct = its speed at the start (percent: 100 moving, 0 standing)
            public long startTick, nextCheck;
            public ZWaypoint wp;
        }
        RamState ram;
        long ramNextLook = -1;

        /// <summary>DV182: this Mammoth rams now (its waypoint in front, the boost on).</summary>
        public bool Ramming => ram != null;
        /// <summary>DV182: the vehicle it rams (-1 = none).</summary>
        public int RamTargetId => ram != null ? ram.targetId : -1;
        /// <summary>DV182: this vehicle was rammed: the Mammoth that rammed it (-1 = not rammed), the tick it is destroyed, the ram's direction
        /// (from the Mammoth's centre to this one's, whole numbers x 1024) and the contact point (Zod px).</summary>
        public int RamDoomBy = -1, RamDirX, RamDirY, RamHitX, RamHitY;
        public long RamDoomTick = -1, RamHitTick = -1;
        public bool RamDoomed => RamDoomBy >= 0;

        /// <summary>RunSpeed: the ram's boost (only while it rams). 1.5 rework 14i: it ACCELERATES: from the speed at the start to the full boost
        /// over <see cref="RamRampTicks"/> (whole percent, <see cref="RamSpeedPct"/>), then it holds; a push / crush drive may brake it toward
        /// its end (<see cref="RamBrakeCap"/>, Sim/ZObject.RamPolish.cs), never below its plain speed.</summary>
        bool RamHurry(out double speed)   // 1.5 DV183 (kit bizon_crush): the Mammoth's or the Bizon's own switch
        {
            speed = 1.0;
            if (ram == null || !RamSwitchOn) return false;
            speed = RamSpeedPct / 100.0;
            double cap = RamBrakeCap();                                                  // px/s, negative = no brake now
            if (cap >= 0.0)
            {
                double plain = MoveSpeed * DamagedSpeed();
                if (plain > 0.0) { double f = cap / plain; if (f < 1.0) f = 1.0; if (f < speed) speed = f; }
            }
            return true;
        }

        /// <summary>1.5 rework 14i: the ram's speed now (percent of its plain speed): from its start speed to the full boost over
        /// <see cref="RamRampTicks"/> ticks in whole-percent steps (integer division), then the full boost. 0 = no ram.</summary>
        public int RamSpeedPct
        {
            get
            {
                if (ram == null || World == null) return 0;
                long k = World.TickIndex - ram.startTick; if (k < 0) k = 0;
                int full = RamFullPct;
                if (k >= RamRampTicks) return full;
                return ram.startPct + (int)((full - ram.startPct) * k / RamRampTicks);
            }
        }

        /// <summary>DodgeMissile: no step aside while it rams (before any random draw).</summary>
        bool RamNoDodgeNow() => ram != null && RamSwitchOn;   // 1.5 DV183 (kit bizon_crush)

        /// <summary>ZWorld.SendWaypoints: a new order ends a ram at once (the new list replaces the old one there).</summary>
        internal void RamOrdered()
        {
            if (ram == null) return;
            RamPushClear();                                                              // 1.5 rework 14i: a lift in progress heaves now (Sim/ZObject.RamPolish.cs)
            ram = null; lastWp = ZWaypoint.Cleared; cur = default;                      // the new first waypoint starts as new, also when it equals the old one
            RamPlainSpeed();
            ramNextLook = World != null ? World.TickIndex + RamRetryTicks : -1;
            if (World != null) World.MammothRamEndsOrder++;
        }

        bool RamUnit() => Kind == ZKind.Vehicle && RamSwitchOn && Owner != Team.Neutral /* 1.5 DV183 (kit bizon_crush): the Mammoth (DV180 + DV182) or the Bizon (DV181 + DV183), Sim/ZObject.BizonCrush.cs */
                          && Drivers.Count > 0 && !Destroyed && KillAt < 0 && MoveSpeed > 0 && !RamDoomed && World != null;

        /// <summary>Its own state lets it start a ram (see the class notes, WHO).</summary>
        bool RamMayStart()
        {
            if (Stunned || HoldPosition || PathInBuildingStage || fortRush || FortRunOn || World.GameOver || IsMinion) return false;
            if (DriverOutHeldHere()) return false;
            if (Waypoints.Count == 0) return true;
            var w = Waypoints[0];
            return (w.mode == WpMode.Move || w.mode == WpMode.Attack || w.mode == WpMode.Agro) && w.refId != ZWorld.RefRam && !ZWorld.IsLavaWalk(w) && CanOverwriteWP();
        }

        /// <summary>No ram while the driver of this tank is being let out (EXIT DRIVER's hold, DV171): the hull stands for him.</summary>
        bool DriverOutHeldHere() => World.DriverStillClimbing(this);

        /// <summary>t may be rammed by this Mammoth (see the class notes, WHOM).</summary>
        bool RamTargetOk(ZObject t) =>
            t != null && t != this && RamKindOk(t) /* 1.5 DV183 (kit bizon_crush): the Bizon also takes ground guns */ && !t.Destroyed && t.KillAt < 0 && t.Health > 0 && t.Owner != Team.Neutral && t.Owner != Owner
            && t.Drivers.Count > 0 && !t.RamDoomed && !t.IsShuttle && !t.PathInBuildingStage && t.World == World && !RamWet(t) /* 1.5 DV192 (kit sim_notes11): never in water or lava */;

        /// <summary>The hull's heading as whole numbers (x 1024) from its last move (FaceX / FaceY); false = it has not moved yet (no front known).</summary>
        bool RamHeading(out int hx, out int hy)
        {
            hx = hy = 0;
            double fx = FaceX, fy = FaceY, fl = Math.Sqrt(fx * fx + fy * fy);
            if (fl < 1e-6) return false;
            hx = (int)Math.Round(fx * 1024.0 / fl); hy = (int)Math.Round(fy * 1024.0 / fl);
            return hx != 0 || hy != 0;
        }

        /// <summary>(dx, dy) lies inside +/- the arc of tan milli of the heading (hx, hy): dot &gt; 0 and |cross| x 1000 &lt;= dot x milli. Whole numbers.</summary>
        static bool RamInArc(long dx, long dy, int hx, int hy, int milli)
        {
            long dot = dx * hx + dy * hy;
            if (dot <= 0) return false;
            long cross = dx * hy - dy * hx; if (cross < 0) cross = -cross;
            return cross * 1000 <= dot * milli;
        }

        /// <summary>The gap between the two hulls is at most gap px: the squared centre distance against (both radii + gap) squared.</summary>
        static bool RamWithin(ZObject a, ZObject b, int gap)
        {
            long dx = b.CenterX - a.CenterX, dy = b.CenterY - a.CenterY, r = ZPathing.Radius(a) + ZPathing.Radius(b) + gap;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>The nearest enemy vehicle in the start arc and within the start gap (ties by object order), or null.</summary>
        ZObject RamPick(int hx, int hy)
        {
            ZObject best = null; long bd = long.MaxValue;
            var all = World.Objects;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (!RamTargetOk(t) /* 1.5 DV183 (kit bizon_crush): RamTargetOk holds the kind (RamKindOk) */) continue;
                if (RamDuelBarred(t, World.TickIndex)) continue;                         // 1.5 DV185 (kit big_tanks_fx): a tank whose ram on this one won is never its candidate (Sim/ZObject.RamDuel.cs)
                long dx = t.CenterX - CenterX, dy = t.CenterY - CenterY, d2 = dx * dx + dy * dy;
                if (d2 <= 0 || d2 >= bd || !RamWithin(this, t, RamStartGapPx) || !RamInArc(dx, dy, hx, hy, RamStartTanMilli)) continue;
                best = t; bd = d2;
            }
            return best;
        }

        /// <summary>Whole number square root (floor).</summary>
        static long RamIsqrt(long v)
        {
            if (v <= 0) return 0;
            long r = (long)Math.Sqrt(v);
            while (r * r > v) r--;
            while ((r + 1) * (r + 1) <= v) r++;
            return r;
        }

        /// <summary>The way to t's contact point is drivable (see the class notes, WHEN). Whole numbers only.</summary>
        bool RamWayClear(ZObject t)
        {
            long dx = t.CenterX - CenterX, dy = t.CenterY - CenterY, len = RamIsqrt(dx * dx + dy * dy);
            if (len <= 0) return true;
            long reach = RamReach(len, t);   /* 1.5 DV183 (kit bizon_crush): the Mammoth to the contact point, the Bizon to the crush point */             // the centre's run to the contact point
            if (reach < 0) reach = 0;
            var lanes = World.Pathing != null && World.Pathing.Enabled ? World.Pathing : null;
            if (lanes != null && ChargeLaneZoneAt(lanes, t.CenterX, t.CenterY)) return false;  // a target on a bridge deck or in a one-lane chokepoint is never rammed
            int hw = WidthPix >> 1, hh = HeightPix >> 1;
            for (long s = 0; ; s += 4)
            {
                if (s > reach) s = reach;
                int px = CenterX + (int)(dx * s / len), py = CenterY + (int)(dy * s / len);
                if (!DuelBoxFree(px - hw, py - hh) || !ChargeBoxDry(px - hw, py - hh)) return false;
                if (lanes != null && ChargeLaneZoneAt(lanes, px, py)) return false;
                if (s >= reach) break;
            }
            int ex = CenterX + (int)(dx * reach / len), ey = CenterY + (int)(dy * reach / len);
            var all = World.Objects;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o == this || o == t || !ZPathing.Separable(o) || RamChainOwn(o) /* 1.5 DV195: its own doomed victims are no obstacle */) continue;
                if (o.Kind == ZKind.Robot && o.Owner != Owner && o.Owner != Team.Neutral) continue;   // an enemy robot in the way is run over (DV65)
                long ox = o.CenterX - CenterX, oy = o.CenterY - CenterY, room = ZPathing.MinSpacing(this, o);
                long along = ox * dx + oy * dy;                                          // x len
                if (along <= 0) continue;                                                // beside or behind it: the gate keeps that room as always
                if (along >= reach * len)
                {   // beyond the contact point: it must keep its room from the hull standing there
                    long qx = o.CenterX - ex, qy = o.CenterY - ey;
                    if (qx * qx + qy * qy < room * room) return false;
                    continue;
                }
                long cross = ox * dy - oy * dx; if (cross < 0) cross = -cross;          // x len
                if (cross < room * len) return false;
            }
            return true;
        }

        /// <summary>ProcessServer, before the orders: the ram's look, start, drive, impact and end. True = it drove this tick (the orders wait).</summary>
        bool RamStepOnce(double time, double dt)   // 1.5 DV195 (kit ram_chain_n24_n25): Sim/ZObject.RamChain.cs RamStep calls it (a ram chained without a move drives in the same tick)
        {
            if (ram == null && !ZWorld.MammothRamDeviation && !ZWorld.BizonCrushDeviation) /* 1.5 DV183 (kit bizon_crush) */ return false;
            if (!RamUnit()) { if (ram != null) RamEnd("crew", false); return false; }
            long now = World.TickIndex;
            if (ram == null)
            {
                if (now < ramNextLook) return false;
                ramNextLook = now + RamLookTicks;
                if (!RamMayStart() || !RamHeading(out int hx, out int hy)) return false;
                var pick = RamPick(hx, hy);
                if (pick == null) return false;
                World.MammothRamLooks++;
                if (!RamWayClear(pick)) { World.MammothRamRefusedWay++; return false; }
                if (!RamDuelStart(pick, now)) return false;                              // 1.5 DV185 (kit big_tanks_fx): a mutual ram is decided here: the first started wins, the same tick the lower id (Sim/ZObject.RamDuel.cs)
                RamBegin(pick, hx, hy, now);
            }
            var t = World.Get(ram.targetId);
            if (Waypoints.Count == 0 || !Waypoints[0].Equals(ram.wp)) { RamEnd("order", false); return false; }   // something else took the front (a pathing give-up, a new list)
            if (RamPushing) return RamPushDrive(t, dt, now); /* 1.5 DV188 (kit ram_polish) */ if (crushOn) return CrushDrive(t, dt, now);                                  // 1.5 DV183 (kit bizon_crush): the Bizon drives on over the target it crushed (Sim/ZObject.BizonCrush.cs)
            if (!RamTargetOk(t)) { RamEnd(t != null && t.RamDoomed ? "taken" : "gone", false); return false; }
            long dx = t.CenterX - CenterX, dy = t.CenterY - CenterY;
            if (!RamWithin(this, t, RamKeepGapPx)) { RamEnd("away", false); return false; }
            if (!RamInArc(dx, dy, ram.hx, ram.hy, RamKeepTanMilli)) { RamEnd("arc", false); return false; }
            if (now - ram.startTick > RamMaxTicks) { RamEnd("time", false); return false; }
            if (now >= ram.nextCheck) { ram.nextCheck = now + RamCheckTicks; if (!RamWayClear(t)) { RamEnd("way", false); return false; } }
            // the drive: straight at the target's centre, the boost on (RunSpeed)
            int lx = LocX, ly = LocY; double xo = XOver, yo = YOver;
            SetTarget(t.CenterX, t.CenterY);
            double tile = World.Map.TileWalkSpeed(CenterX, CenterY);
            if (World.Pathing != null) tile = World.Pathing.SafeTileSpeed(this, tile);
            RealMoveSpeed = MoveSpeed * tile * DamagedSpeed() * RunSpeed();
            SetVelocity();
            World.MammothRamBoostTicks++;
            if (!ProcessMove(dt, true, out _, out _) && !(CenterX == t.CenterX && CenterY == t.CenterY)) { RamEnd("blocked", false); return true; }
            bool moved = LocX != lx || LocY != ly || XOver != xo || YOver != yo;
            if (!moved) { if (++ram.noMoveTicks >= RamHeldTicks) RamEnd("held", false); return true; }
            ram.noMoveTicks = 0;
            // the impact: front contact while it drives forward with the target in the arc of the heading it moved in
            if (RamTouch(t) /* 1.5 DV188 (kit ram_polish): the drawn hulls touch */ && RamHeading(out int mx, out int my) && RamInArc(t.CenterX - CenterX, t.CenterY - CenterY, mx, my, RamKeepTanMilli))
            {
                long sx = (long)(LocX - lx) * mx + (long)(LocY - ly) * my;              // whole px moved along the heading (the sub-pixel part left out)
                if (sx >= 0) { if (IsCrusher) CrushImpact(t, now); else RamImpact(t, now); }   // 1.5 DV183 (kit bizon_crush): the Bizon's contact is the crush
            }
            return true;
        }

        void RamBegin(ZObject t, int hx, int hy, long now)
        {
            ram = new RamState { targetId = t.Id, hx = hx, hy = hy, startTick = now, nextCheck = now + RamCheckTicks, startPct = IsMoving ? 100 : 0 };   // 1.5 rework 14i: the boost builds up from here
            ram.wp = new ZWaypoint { mode = WpMode.Move, refId = ZWorld.RefRam, x = t.CenterX, y = t.CenterY };
            Waypoints.Insert(0, ram.wp);
            UpdatedWaypoints = true;
            cur = default; SetTarget(t.CenterX, t.CenterY);
            IsRunning = false;
            if (IsCrusher) World.BizonCrushStarts++; else World.MammothRamStarts++; crushOn = false;   // 1.5 DV183 (kit bizon_crush)
            World.Emit(IsCrusher ? ZEventType.BizonCrush : ZEventType.MammothRam, this, t, t.CenterX, t.CenterY, 0f, IsCrusher ? CrushFallbackTicks : RamFlipTicks, false /* 1.5 DV183 (kit bizon_crush) */);    // the start: the boost (the view: engine rev)
        }

        /// <summary>The ram is over: its waypoint goes, the order behind it starts again from here (a fresh first waypoint), or it stands.</summary>
        void RamEnd(string why, bool hit)
        {
            if (ram == null) return;
            RamPushClear();                                                              // 1.5 rework 14i: a lift in progress heaves now (Sim/ZObject.RamPolish.cs)
            var w = ram.wp; ram = null;
            for (int i = 0; i < Waypoints.Count; i++) if (Waypoints[i].refId == ZWorld.RefRam && Waypoints[i].Equals(w)) { Waypoints.RemoveAt(i); break; }
            lastWp = ZWaypoint.Cleared;                                                  // the order behind it starts as new: its path from here
            cur = default; SetTarget(CenterX, CenterY);
            if (hit && RamChainNow(why)) return;                                       // 1.5 DV195 (kit ram_chain_n24_n25): the next enemy close in front is rammed now: no stop, no cool-down (Sim/ZObject.RamChain.cs)
            StopMove();
            RamPlainSpeed();
            UpdatedWaypoints = true;
            ramNextLook = World.TickIndex + (hit ? RamChainCool(RamCoolTicks) /* 1.5 DV195: RamLookTicks */ : RamRetryTicks);
            World.NoteRamEnd(why);
        }

        /// <summary>The boost ends with the ram: its plain speed back at once (the order's first step is never boosted).</summary>
        void RamPlainSpeed()
        {
            if (World == null || World.Map == null) return;
            double tile = World.Map.TileWalkSpeed(CenterX, CenterY);
            if (World.Pathing != null) tile = World.Pathing.SafeTileSpeed(this, tile);
            RealMoveSpeed = MoveSpeed * tile * DamagedSpeed() * RunSpeed();
        }

        void RamImpact(ZObject t, long now)
        {
            long dx = t.CenterX - CenterX, dy = t.CenterY - CenterY, len = RamIsqrt(dx * dx + dy * dy);
            if (len <= 0) len = 1;
            int ra = ZPathing.Radius(this);
            t.RamDoomBy = Id; t.RamDoomTick = now + RamFlipTicks; t.RamHitTick = now;
            t.RamDirX = (int)(dx * 1024 / len); t.RamDirY = (int)(dy * 1024 / len);
            t.RamHitX = CenterX + (int)(dx * ra / len); t.RamHitY = CenterY + (int)(dy * ra / len);
            t.RamDoomStop();
            World.MammothRamHits++;
            World.Emit(ZEventType.MammothRam, this, t, t.RamHitX, t.RamHitY, 1f, RamFlipTicks, true);     // the impact (the view: crunch, sparks, shake, the flip, the ram line)
            if (!RamPushBegin(t, now)) RamEnd("hit", true);   // 1.5 DV188 (kit ram_polish): Sim/ZObject.RamPolish.cs: the Mammoth pushes on before the heave
        }

        /// <summary>The rammed vehicle: it stops, drops its target and its orders (its ram too, if it is a Mammoth that rammed itself).</summary>
        void RamDoomStop()
        {
            RamDuelDoomed();                                                             // 1.5 DV185 (kit big_tanks_fx): a Bizon doomed while it rolls over a crushed target: that target dies the next tick (Sim/ZObject.RamDuel.cs)
            if (ram != null) { RamPushClear(); /* 1.5 rework 14i */ ram = null; World.NoteRamEnd("rammed"); RamPlainSpeed(); }
            Waypoints.Clear(); lastWp = ZWaypoint.Cleared; cur = default;
            StopMove(); Disengage(); IsRunning = false;
            UpdatedWaypoints = true;
        }

        /// <summary>ProcessServer, before the stun hold: a rammed vehicle takes no step, no shot, no order until it is destroyed.</summary>
        internal bool RamDoomHold()
        {
            if (RamDoomBy < 0) return false;
            if (Waypoints.Count > 0) { Waypoints.Clear(); lastWp = ZWaypoint.Cleared; UpdatedWaypoints = true; }
            StopMove();
            if (AttackObject != null) Disengage();
            return true;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV182 the MAMMOTH RAM (Sim/ZObject.MammothRam.cs). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool MammothRamDeviation = false;
        /// <summary>The ram's own waypoint (a MOVE in front of the order, never the player's).</summary>
        public const int RefRam = -182;

        /// <summary>ZPathing.PairExempt: a ramming Mammoth and its target may touch (the spacing gate lets the spikes reach the hull).</summary>
        public static bool RamPair(ZObject a, ZObject b) => (MammothRamDeviation || BizonCrushDeviation) && a != null /* 1.5 DV183 (kit bizon_crush): also a crushing Bizon and its target */ && b != null && a.Ramming && a.RamTargetId == b.Id
                                                         || RamChainPair(a, b);   // 1.5 DV195 (kit ram_chain_n24_n25): the big tank and the vehicle it doomed, until it is destroyed

        /// <summary>The parity report and the older counted checks: off while this lives.</summary>
        public static IDisposable MammothRamOff() => new MammothRamOffHold();
        sealed class MammothRamOffHold : IDisposable
        {
            readonly bool was = MammothRamDeviation;
            public MammothRamOffHold() { MammothRamDeviation = false; }
            public void Dispose() { MammothRamDeviation = was; }
        }

        /// <summary>Statistics (the counted check, the Play check): looks that found a target, refused for the way, rams started, hits, kills,
        /// ticks driven with the boost, and the ends without a hit by reason.</summary>
        public int MammothRamLooks, MammothRamRefusedWay, MammothRamStarts, MammothRamHits, MammothRamKills, MammothRamBoostTicks, MammothRamEndsOrder;
        public int MammothRamEndsGone, MammothRamEndsTaken, MammothRamEndsAway, MammothRamEndsArc, MammothRamEndsTime, MammothRamEndsWay, MammothRamEndsBlocked,
                   MammothRamEndsHeld, MammothRamEndsCrew, MammothRamEndsFront, MammothRamEndsRammed;

        internal void NoteRamEnd(string why)
        {
            switch (why)
            {
                case "gone": MammothRamEndsGone++; break;
                case "taken": MammothRamEndsTaken++; break;
                case "away": MammothRamEndsAway++; break;
                case "arc": MammothRamEndsArc++; break;
                case "time": MammothRamEndsTime++; break;
                case "way": MammothRamEndsWay++; break;
                case "blocked": MammothRamEndsBlocked++; break;
                case "held": MammothRamEndsHeld++; break;
                case "crew": MammothRamEndsCrew++; break;
                case "order": MammothRamEndsFront++; break;
                case "rammed": MammothRamEndsRammed++; break;
            }
        }

        /// <summary>ZWorld.Tick, after the run-over pass: every rammed vehicle whose hold is over is destroyed with its crew, the kill the
        /// Mammoth's (object order). One already destroyed meanwhile (a shell, a bridge) is left as it died.</summary>
        void MammothRamTick()
        {
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.RamDoomBy < 0 || o.RamDoomTick < 0 || TickIndex < o.RamDoomTick || o.Destroyed || o.KillAt >= 0) continue;
                o.SetHealth(0);
                UpdateObjectHealth(o, o.RamDoomBy);
                if (o.RamCrushed) BizonCrushKills++; else MammothRamKills++;   // 1.5 DV183 (kit bizon_crush)
            }
        }
    }
}
