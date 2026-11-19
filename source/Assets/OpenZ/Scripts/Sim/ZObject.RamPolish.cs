using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV188 "the ram polish" (kit ram_polish, on top of DV182 the Mammoth ram, DV183 the Bizon crush, DV185 the ram duel,
    /// <list type="bullet">
    /// <item>FIRE (both big tanks): from the start of a ram / crush until its target is dead the big tank never fires at THAT target (its gun,
    /// its second barrel, its hatch driver): the target is dropped at once and the next valid enemy in range is engaged in the same tick (Zod's
    /// own passive pick, the ram target left out); none = it holds its fire. A shell already in flight lands as it was fired.</item>
    /// <item>THE DOOMED (both): a vehicle whose fate is sealed (rammed / crushed) attacks nothing any more (CanAttackObject false for every
    /// target) on top of DV182's hold, which already skips its whole fire step.</item>
    /// <item>DRAWN CONTACT (both): the impact is the touch of the DRAWN hulls, not of the spacing circles: the centre distance against the big
    /// tank's drawn front reach plus the target's drawn half extent along the line between them (oriented boxes of the game models, measured
    /// from Resources/OpenZ/VehicleLights/vehicle_lights.json, whole px, the headings as whole numbers x 1024). A target that has never moved
    /// (no heading known) keeps its spacing radius.</item>
    /// <item>PUSH (Mammoth): after the contact the Mammoth drives on <see cref="RamPushPx"/> px along the ram (at most
    /// <see cref="RamPushMaxTicks"/> ticks), shoving the held target (the view moves its drawn hull with the spikes); then the heave tick
    /// (<see cref="RamHeaveTick"/>): the flip, and the kill <see cref="RamFlipTicks"/> ticks later (after the landing).</item>
    /// <item>SWEEP (Mammoth): enemy robots of the Mammoth (never its own team, never neutral) behind the target inside the band the pushed and
    /// rolling hull covers are squashed like a run-over (DV65's death: Squashed, the kill the Mammoth's) at the tick the hull's far edge reaches
    /// them: during the push its far face + the advance; in the roll its far face + the push + <see cref="RamRollPx"/> x the flight's part
    /// (the view's flight is 72 % of the flip). Robots beside the band, in front of the target or in a building are untouched.</item>
    /// </list>
    /// Deterministic: whole numbers only (px, x 1024 headings, ticks), object order, no random draw. Switch <see cref="ZWorld.RamPolishDeviation"/>:
    /// View/ZGame.cs on at every start (off in 'Zod exact'), headless default off; off: not one field of any unit changes.
    /// </summary>
    public partial class ZObject
    {
        /// <summary>The push after the contact (px; 5 px = 0.63 m), its longest time (0.7 s). 1.5 rework 14i: no longer used for the distance (the
        /// lift is <see cref="RamLiftPxOf"/>); the longest lift is <see cref="RamPushMaxTicks"/> (1.5 s).</summary>
        public const int RamPushPx = 5, RamPushMaxTicks = 150;
        /// <summary>The roll's share of the flip (1.5 rework 14i: the view's fall over onto its roof is about 55 % of the flip; was 72 %).</summary>
        public const int RamRollFlightPct = 55;
        /// <summary>
        /// the LIFT. From the drawn contact the Mammoth drives on under the near side of the target (its low wedge front lifts that side while the
        /// target pivots on its far track, then its glacis pushes the lifted hull on over: the view) for the target's drawn length along the ram
        /// + <see cref="RamLiftOverPx"/> (<see cref="RamLiftMinPx"/>..<see cref="RamLiftMaxPx"/>; measured on the drawn wedge in the Unit editor:
        /// the tipping point comes after about that advance): there the target passes its tipping point (the heave). Its speed eases from the
        /// boost down to <see cref="RamTipSpeedPx"/> px/s at the tip (linear in the distance left), then it brakes to a stop at
        /// <see cref="RamStopAccelPx"/> px/s2 (never below its plain speed, then it stands as any unit arriving).</summary>
        public const int RamLiftOverPx = -2, RamLiftMinPx = 16, RamLiftMaxPx = 48, RamTipSpeedPx = 40, RamStopAccelPx = 96;
        /// <summary>1.5 rework 14i: the stop after the tip: (RamTipSpeedPx)^2 / (2 x RamStopAccelPx), rounded up (px).</summary>
        public const int RamStopPx = (RamTipSpeedPx * RamTipSpeedPx + 2 * RamStopAccelPx - 1) / (2 * RamStopAccelPx);

        /// <summary>The drawn hulls (px at 8 px per m), the front reach, the rear reach and the half width. 1.5 rework 14i: measured on the game's
        /// drawn hulls (mesh vertices of the hull and tracks, the turret and the barrels left out, along the rules' heading, Unit editor 10 Nov:
        /// the drawn hulls met). The Mammoth's front is its low wedge where it meets a hull (3.41 m at 0.25-0.5 m height; 3.48 m at the ground).</summary>
        public static void RamDrawnBox(string type, out int front, out int rear, out int halfW)
        {
            switch (type)
            {
                case ZBigTanks.Mammoth: front = 27; rear = 23; halfW = 18; return;   // wedge 3.41 m ahead, 2.87 m behind, 2.25 m half width
                case ZBigTanks.Bizon: front = 28; rear = 26; halfW = 18; return;     // 3.50 / 3.23 / 2.24 m (its front track 3.25 m)
                case "light": front = 12; rear = 15; halfW = 13; return;            // 1.46 / 1.87 / 1.57 m
                case "medium": front = 14; rear = 11; halfW = 11; return;           // 1.67 / 1.33 / 1.33 m
                case ZMTank.Type: front = 17; rear = 18; halfW = 12; return;        // 2.12 / 2.24 / 1.49 m
                case "heavy": front = 19; rear = 19; halfW = 16; return;            // 2.34 / 2.35 / 1.91 m
                case "missile_launcher": front = 18; rear = 18; halfW = 16; return; // 2.19 / 2.19 / 1.97 m
                case "jeep": front = 15; rear = 15; halfW = 11; return;             // 1.76 / 1.76 / 1.34 m
                case "apc": front = 18; rear = 18; halfW = 12; return;              // 2.16 / 2.16 / 1.42 m
                case "crane": front = 19; rear = 19; halfW = 11; return;            // 2.40 / 2.40 / 1.32 m (vehicle_lights, not re-measured)
                default: front = rear = halfW = -1; return;                         // a gun, anything else: its spacing radius
            }
        }

        /// <summary>1.5 rework 14i: the drawn height (px, the turret included, the barrels left out; same probe), for the roll's travel.</summary>
        public static int RamDrawnHeight(string type)
        {
            switch (type)
            {
                case ZBigTanks.Mammoth: return 25; case ZBigTanks.Bizon: return 28; case "light": return 21; case "medium": return 27;
                case ZMTank.Type: return 22; case "heavy": return 21; case "missile_launcher": return 31; case "jeep": return 21;
                case "apc": return 22; case "crane": return 24; default: return 16;
            }
        }

        /// <summary>1.5 rework 14i: the lift for target t along the unit direction (ux, uy) x 1024: its drawn length along the ram +
        /// RamLiftOverPx, clamped (px).</summary>
        public static int RamLiftPxOf(ZObject t, int ux, int uy)
        {
            int w = RamDrawnExtent(t, ux, uy, true) + RamDrawnExtent(t, -ux, -uy, true) + RamLiftOverPx;
            return Math.Max(RamLiftMinPx, Math.Min(RamLiftMaxPx, w));
        }

        /// <summary>The drawn half extent of o along the unit direction (ux, uy) x 1024 (an oriented box; ahead = the side the direction points
        /// to from its centre). No heading known or no box: its spacing radius.</summary>
        public static int RamDrawnExtent(ZObject o, int ux, int uy, bool ahead)
        {
            RamDrawnBox(o.Type, out int fr, out int re, out int hw);
            if (fr < 0 || !o.RamHeading(out int hx, out int hy)) return ZPathing.Radius(o);
            long c = ((long)ux * hx + (long)uy * hy) / 1024, s = ((long)ux * -hy + (long)uy * hx) / 1024;
            int len = (c >= 0) == ahead ? fr : re;
            if (c < 0) c = -c; if (s < 0) s = -s;
            return (int)((c * len + s * hw + 1023) / 1024);
        }

        /// <summary>The drawn gap (px, negative = overlap) between this big tank's front and t, both drawn boxes along the line between them.</summary>
        public int RamDrawnGap(ZObject t)
        {
            long dx = t.CenterX - CenterX, dy = t.CenterY - CenterY, len = RamIsqrt(dx * dx + dy * dy);
            if (len <= 0) return -1;
            int ux = (int)(dx * 1024 / len), uy = (int)(dy * 1024 / len);
            return (int)(len - RamDrawnExtent(this, ux, uy, true) - RamDrawnExtent(t, -ux, -uy, true));
        }

        /// <summary>RamStep's impact test: DV188 the drawn hulls touch, else DV182's spacing contact.</summary>
        bool RamTouch(ZObject t)
        {
            if (!ZWorld.RamPolishDeviation) return RamWithin(this, t, RamContactPx);
            if (RamDrawnGap(t) > 0) return false;
            World.RamDrawnTouches++;
            return true;
        }

        // ------------------------------------------------------------------ fire
        /// <summary>This unit may not fire at o: a doomed vehicle fires at nothing; a big tank not at the target of its ram / crush (from the
        /// start until it is dead).</summary>
        public bool RamFireBarred(ZObject o) =>
            ZWorld.RamPolishDeviation && o != null && Kind == ZKind.Vehicle && (RamDoomBy >= 0 || (ram != null && ram.targetId == o.Id) || o.RamDoomBy == Id);

        /// <summary>ProcessServer, before the passive check: a barred target is dropped and the next one picked in this same tick.</summary>
        void RamFireDrop(double time)
        {
            if (!ZWorld.RamPolishDeviation || AttackObject == null || !RamFireBarred(AttackObject)) { RamFightStep(); return; }   // 1.5 DV196 (kit mammoth_ram_fight_n26c): a Mammoth in a ram keeps a target in range (Sim/ZObject.RamFight.cs)
            Disengage();
            nextPassiveCheck = time;
            World.RamFireDrops++;
            RamFightStep();   // 1.5 DV196 (kit mammoth_ram_fight_n26c): the next enemy in range at once
        }

        // ------------------------------------------------------------------ push (1.5 rework 14i: the lift under the near side, then the stop)
        int rpOn, rpTarget = -1, rpFromX, rpFromY, rpDirX, rpDirY, rpHeld, rpLift; long rpStart;
        /// <summary>The Mammoth pushes now (its ram's target on its wedge: rpOn 1 = the lift, 2 = the stop after the tip).</summary>
        public bool RamPushing => rpOn != 0 && ram != null && ram.targetId == rpTarget;

        /// <summary>The rammed vehicle (set by DV188): the tick the flip starts (-1 = still lifted / not rammed), the push done (px: 1.5 rework
        /// 14i always 0, the hull is never shoved, it pivots on its far track), the roll (px), the ram's direction x 1024 and its centre at the
        /// contact, its drawn half extents ahead / across; 1.5 rework 14i: the planned lift (px) and the Mammoth's advance at the heave (px).</summary>
        public long RamHeaveTick = -1;
        public int RamPushDone, RamRollPx, RamSweepDirX, RamSweepDirY, RamSweepOx, RamSweepOy, RamSweepAhead, RamSweepAcross, RamSweepBy = -1, RamLiftPlan, RamLiftDone;
        public Team RamSweepTeam;

        /// <summary>RamImpact (a Mammoth): the lift starts instead of the end of the ram. False = no lift (switch off, a Bizon).</summary>
        bool RamPushBegin(ZObject t, long now)
        {
            if (!ZWorld.RamPolishDeviation || IsCrusher || ram == null || t == null) return false;
            rpOn = 1; rpTarget = t.Id; rpFromX = CenterX; rpFromY = CenterY; rpDirX = t.RamDirX; rpDirY = t.RamDirY; rpHeld = 0; rpStart = now;
            t.RamDoomTick = now + RamPushMaxTicks + RamFlipTicks + 2;                    // provisional: set again at the heave
            t.RamHeaveTick = -1; t.RamPushDone = 0; t.RamLiftDone = 0;
            int ux = t.RamDirX, uy = t.RamDirY;
            t.RamSweepDirX = ux; t.RamSweepDirY = uy; t.RamSweepOx = t.CenterX; t.RamSweepOy = t.CenterY;
            t.RamSweepAhead = RamDrawnExtent(t, ux, uy, true); t.RamSweepAcross = RamDrawnExtent(t, -uy, ux, true);
            rpLift = t.RamLiftPlan = RamLiftPxOf(t, ux, uy);                              // to the tipping point (the view's lever)
            int along = t.RamSweepAhead + RamDrawnExtent(t, -ux, -uy, true);
            t.RamRollPx = Math.Max(16, Math.Min(64, along + RamDrawnHeight(t.Type)));     // the view's fall: the hull rolls over its far edges onto its roof (its length along the ram + its height)
            t.RamSweepBy = Id; t.RamSweepTeam = Owner;
            World.RamPushes++;
            return true;
        }

        /// <summary>The Mammoth's advance along the ram since the contact (whole px).</summary>
        long RamPushAdvance() => ((long)(CenterX - rpFromX) * rpDirX + (long)(CenterY - rpFromY) * rpDirY) / 1024;

        /// <summary>1.5 rework 14i, RamHurry: the speed cap now (px/s; negative = none): the lift eases from the boost to RamTipSpeedPx at the
        /// tip (linear in the distance left), the stop after it brakes at RamStopAccelPx; the Bizon's drive over a crushed target brakes toward
        /// its end (Sim/ZObject.CrushRollOff.cs). Whole-number distances; Math.Sqrt is exact in IEEE (deterministic).</summary>
        double RamBrakeCap()
        {
            if (rpOn == 1 && rpLift > 0)
            {
                long left = rpLift - RamPushAdvance(); if (left < 0) left = 0;
                return RamTipSpeedPx + (RamBoostPct * MoveSpeed / 100.0 - RamTipSpeedPx) * left / rpLift;
            }
            if (rpOn == 2)
            {
                long left = rpLift + RamStopPx - RamPushAdvance(); if (left < 0) left = 0;
                return Math.Min(RamTipSpeedPx, Math.Sqrt(2.0 * RamStopAccelPx * left));
            }
            return CrushBrakeCap();
        }

        /// <summary>RamStep while it lifts and stops: straight on along the ram, the target held (it pivots in the view); true = it drove this tick.</summary>
        bool RamPushDrive(ZObject t, double dt, long now)
        {
            if (t == null || t.Destroyed || t.KillAt >= 0 || t.RamDoomBy != Id) { RamPushEnd(t, now); return false; }
            long adv = RamPushAdvance();
            if (rpOn == 1 && (adv >= rpLift || now - rpStart >= RamPushMaxTicks)) RamHeave(t, now, adv);   // the tipping point: the fall starts, the Mammoth stops
            if (rpOn == 2 && (adv >= rpLift + RamStopPx || now - rpStart >= RamPushMaxTicks + 40)) { RamPushEnd(t, now); return false; }
            int reach = rpLift + RamStopPx + 2;
            int gx = rpFromX + (int)((long)rpDirX * reach / 1024), gy = rpFromY + (int)((long)rpDirY * reach / 1024);
            int lx = LocX, ly = LocY; double xo = XOver, yo = YOver;
            SetTarget(gx, gy);
            double tile = World.Map.TileWalkSpeed(CenterX, CenterY);
            if (World.Pathing != null) tile = World.Pathing.SafeTileSpeed(this, tile);
            RealMoveSpeed = MoveSpeed * tile * DamagedSpeed() * RunSpeed();
            SetVelocity();
            World.RamPushTicks++;
            if (!ProcessMove(dt, true, out _, out _)) { RamPushEnd(t, now); return true; }
            bool moved = LocX != lx || LocY != ly || XOver != xo || YOver != yo;
            if (!moved && (++rpHeld >= RamHeldTicks || RamChainReady() /* 1.5 DV195: the next target holds it: on to it now */)) RamPushEnd(t, now);
            return true;
        }

        /// <summary>1.5 rework 14i: the tipping point: the flip's fall starts now and the kill RamFlipTicks later (after the landing); the
        /// Mammoth brakes to a stop.</summary>
        void RamHeave(ZObject t, long now, long adv)
        {
            if (rpOn != 1) return;
            rpOn = 2;
            if (t != null && t.RamDoomBy == Id && !t.Destroyed && t.KillAt < 0 && t.RamHeaveTick < 0)
            {
                t.RamHeaveTick = now; t.RamDoomTick = now + RamFlipTicks; t.RamLiftDone = (int)Math.Max(0, adv);
                World.RamHeaves++;
            }
        }

        /// <summary>1.5 rework 14i: RamEnd / RamOrdered / RamDoomStop: a lift still running ends here (its target heaves now, never left
        /// hanging on the wedge); the brake state is cleared.</summary>
        void RamPushClear()
        {
            if (rpOn == 0) return;
            if (rpOn == 1 && World != null) RamHeave(World.Get(rpTarget), World.TickIndex, RamPushAdvance());
            rpOn = 0;
        }

        void RamPushEnd(ZObject t, long now)
        {
            if (rpOn == 1) RamHeave(t, now, RamPushAdvance());                           // cut short (blocked, held): the heave now, never left on the wedge
            rpOn = 0;
            RamEnd("hit", true);
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV188 the ram polish (Sim/ZObject.RamPolish.cs). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool RamPolishDeviation = false;

        public static IDisposable RamPolishOff() => new RamPolishOffHold();
        sealed class RamPolishOffHold : IDisposable
        {
            readonly bool was = RamPolishDeviation;
            public RamPolishOffHold() { RamPolishDeviation = false; }
            public void Dispose() { RamPolishDeviation = was; }
        }

        /// <summary>Statistics (the counted check): drawn touches, fire drops, pushes, push ticks, heaves, robots squashed by a pushed / rolled hull.</summary>
        public int RamDrawnTouches, RamFireDrops, RamPushes, RamPushTicks, RamHeaves, RamSweepKills;

        /// <summary>ZWorld.Tick, after MammothRamTick: the pushed / rolling hulls squash the enemy robots of the Mammoth they reach (object order).</summary>
        void RamSweepTick()
        {
            if (!RamPolishDeviation) return;
            int flight = ZObject.RamFlipTicks * ZObject.RamRollFlightPct / 100;
            for (int i = 0; i < Objects.Count; i++)
            {
                var t = Objects[i];
                if (t.RamSweepBy < 0 || t.RamDoomBy < 0 || t.Destroyed || t.KillAt >= 0) continue;
                long front;
                if (t.RamHeaveTick < 0) front = t.RamSweepAhead + t.RamPushDone;
                else
                {
                    long k = TickIndex - t.RamHeaveTick;
                    if (k > flight + 1) { t.RamSweepBy = -1; continue; }                 // landed: the sweep is over
                    if (k < 0) k = 0; if (k > flight) k = flight;
                    front = t.RamSweepAhead + t.RamPushDone + t.RamRollPx * k / flight;
                }
                RamSweepAt(t, front);
            }
        }

        void RamSweepAt(ZObject t, long front)
        {
            int ux = t.RamSweepDirX, uy = t.RamSweepDirY;
            for (int j = 0; j < Objects.Count; j++)
            {
                var r = Objects[j];
                if (r.Kind != ZKind.Robot || r.Destroyed || r.KillAt >= 0 || r.Health <= 0 || r.Owner == Team.Neutral || r.Owner == t.RamSweepTeam) continue;   // never the Mammoth's own team
                if (r.IsFortDefender || r.PathInBuildingStage || r.FortRushOn || r.IsEvilZod || r.DriverOutOnHull) continue;
                long rx = r.CenterX - t.RamSweepOx, ry = r.CenterY - t.RamSweepOy;
                if (rx > 120 || rx < -120 || ry > 120 || ry < -120) continue;
                long along = (rx * ux + ry * uy) / 1024, lat = (rx * -uy + ry * ux) / 1024; if (lat < 0) lat = -lat;
                int rr = ZPathing.Radius(r);
                if (along <= 0 || along > front + rr || lat > t.RamSweepAcross + rr) continue;   // in front of / beside the band, or not reached yet
                r.Squashed = true; r.RunOverBy = t.RamSweepBy; r.RunOverAt = Time; r.RunOverDirX = ux / 1024f; r.RunOverDirY = uy / 1024f; r.RunOverGlancing = false;
                r.SetHealth(0);
                UpdateObjectHealth(r, t.RamSweepBy);                                     // the Death event, the kill the Mammoth's (as DV65's squash)
                RamSweepKills++;
            }
        }
    }
}
