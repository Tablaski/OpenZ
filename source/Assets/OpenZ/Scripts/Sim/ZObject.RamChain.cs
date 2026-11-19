using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// DV182 the Mammoth's ram, DV183 the Bizon's crush, DV185, DV187, DV188 and DV192 / DV193 (kit sim_notes11), whose rules it leaves as they are).
    /// Z has no ram; every number is OpenZ's.
    /// <para>
    /// doomed: RamDoomBy set) until it is destroyed: (1) every order to it is refused (ZWorld.SendWaypoints: a player's, the computer's, a
    /// patrol, a stop, a hold), before this an order was taken and wiped again at its next tick, so between two ticks it had a fresh order;
    /// (2) it is a fixed obstacle for the pathing layer (ZPathing.Static: never pushed apart, never stepped aside, never shoved out of an
    /// overlap), before this the Mammoth's rammed target was pushed 16-25 px (or jumped 46-52 px in one tick) by the spacing push once the
    /// Mammoth's ram had ended; (3) the big tank that doomed it and the doomed vehicle may overlap until it is destroyed
    /// (<see cref="ZWorld.RamPair"/>), so neither is pushed off the other. DV182's hold (no step, no turn, no shot, no exit) is unchanged.
    /// The view holds the doomed hull where it is drawn (View/OzRamStill.cs).</para>
    /// <para>
    /// When a ram ends with a hit (the Mammoth's lift and stop are over,
    /// the Bizon's drive over the crushed target is over), the next enemy that DV182's start rules accept right now (an enemy crewed vehicle or,
    /// for the Bizon, a ground gun: RamTargetOk, incl. DV192's never in water or lava; the nearest one inside +/- RamStartArcDeg of the hull's
    /// heading and within RamStartGapPx; the way clear; DV185's duel) is rammed at once: the ram's waypoint is replaced in the same tick, no stop,
    /// no plain-speed step, no cool-down; the boost builds up again from the speed it has (its plain speed after the brake, as DV182's start
    /// from a move) over RamRampTicks. When the old ram cannot move on this tick, the new one drives in the same tick (no idle tick). A drive
    /// that is held by the next target's room (the Bizon rolling over a target with the next one right behind it, the Mammoth's lift held by
    /// it) ends at once when that next target can be rammed, instead of standing RamHeldTicks first. The big tank's own doomed victims are no
    /// obstacle for the way check (they are destroyed within CrushFallbackTicks). Nothing fits: DV182 as before (the order behind it goes
    /// on), except that the next look is RamLookTicks away instead of RamCoolTicks (no 3 s pause after a hit).</para>
    /// <para>Deterministic: whole numbers only in every decision (DV182's pick, arc and way tests), object order, no random draw, no clock. Switch
    /// <see cref="ZWorld.RamChainDeviation"/>: View/ZGame.cs on at every start (off in 'Zod exact'); headless default off; the parity report's
    /// Zod rows and probes and every older counted check hold it off. Off: every hook returns at its first test, not one field changes.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV195: the tick a chained ram began (-1 = none): RamStep drives the new lunge in that tick when the old ram ended without a move.</summary>
        long ramChainTick = -1;

        /// <summary>ProcessServer: the ram's tick (DV182's RamStepOnce), and with DV195 a ram chained in it without a move this tick drives now.</summary>
        internal bool RamStep(double time, double dt)
        {
            long c0 = ramChainTick;
            bool r = RamStepOnce(time, dt);
            if (r || ram == null || ramChainTick == c0 || World == null || ramChainTick != World.TickIndex) return r;
            World.RamChainSameTick++;
            return RamStepOnce(time, dt);
        }

        /// <summary>RamEnd with a hit, after its waypoint went and before its stop: DV195 the next enemy close in front is rammed now (true = the
        /// new ram began: RamEnd returns at once, no stop, no plain speed, no cool-down). False = nothing fits (RamEnd goes on as before).</summary>
        bool RamChainNow(string why)
        {
            if (!ZWorld.RamChainDeviation || World == null || !RamUnit() || !RamMayStart() || !RamHeading(out int hx, out int hy)) return false;
            long now = World.TickIndex;
            var pick = RamPick(hx, hy);
            if (pick == null) return false;
            if (!RamWayClear(pick)) { World.RamChainRefusedWay++; return false; }
            if (!RamDuelStart(pick, now)) return false;
            int carry = RamChainCarryPct();
            World.NoteRamEnd(why);
            RamBegin(pick, hx, hy, now);                                                 // DV182's start: its waypoint in front of the order
            ram.startPct = carry;                                                        // the boost builds up from the speed it drives at now (no step down to its plain speed)
            ramChainTick = now;
            World.RamChains++;
            return true;
        }

        /// <summary>The speed the ending ram drove at (percent of its plain speed, whole number): its last drive speed (RealMoveSpeed, the boost and
        /// any brake in it) over its plain speed here (RamPlainSpeed's formula); from its plain speed (100) to its full boost; standing: 0.</summary>
        int RamChainCarryPct()
        {
            if (!IsMoving) return 0;
            double tile = World.Map.TileWalkSpeed(CenterX, CenterY);
            if (World.Pathing != null) tile = World.Pathing.SafeTileSpeed(this, tile);
            double plain = MoveSpeed * tile * DamagedSpeed() * RunSpeed();               // the ram is over: RunSpeed has no boost in it now
            if (plain <= 0.0) return 100;
            long pct = (long)(RealMoveSpeed * 100.0 / plain);
            int full = RamFullPct;
            return pct < 100 ? 100 : pct > full ? full : (int)pct;
        }

        /// <summary>RamEnd with a hit and nothing chained: the next look (DV195: RamLookTicks, not DV182's RamCoolTicks: a target that fits a
        /// moment later is rammed then).</summary>
        long RamChainCool(long cool) => ZWorld.RamChainDeviation ? RamLookTicks : cool;

        /// <summary>The next target for a big tank in a ram (DV182's start rules, the ram's own waypoint and its doomed target left out), or null.</summary>
        ZObject RamChainNext()
        {
            if (!ZWorld.RamChainDeviation || World == null || ram == null || !RamUnit() || !RamChainMay() || !RamHeading(out int hx, out int hy)) return null;
            var pick = RamPick(hx, hy);
            if (pick == null || pick.Id == ram.targetId || !RamWayClear(pick)) return null;
            return pick;
        }

        /// <summary>A drive held this tick (the Bizon over its crushed target, the Mammoth's lift): DV195 a next target could be rammed now (the drive
        /// ends at once instead of standing RamHeldTicks).</summary>
        bool RamChainReady()
        {
            if (!ZWorld.RamChainDeviation || RamChainNext() == null) return false;
            World.RamChainEarlyEnds++;
            return true;
        }

        /// <summary>CrushDrive, before its step: DV195 the next target stands at the Bizon's room on its way (the spacing gate would hold it or slide
        /// it round): the drive over the crushed one ends now and the next crush begins in this tick (it rolls on over the first one's wreck).</summary>
        bool RamChainBlocks()
        {
            if (!ZWorld.RamChainDeviation) return false;
            var p = RamChainNext();
            if (p == null) return false;
            long dx = p.CenterX - CenterX, dy = p.CenterY - CenterY, room = ZPathing.MinSpacing(this, p) + 2;
            if (dx * dx + dy * dy > room * room) return false;
            World.RamChainEarlyEnds++;
            return true;
        }

        /// <summary>DV182's RamMayStart for a big tank in a ram: its own state, and the first waypoint behind the ram's is a plain move / attack / agro.</summary>
        bool RamChainMay()
        {
            if (Stunned || HoldPosition || PathInBuildingStage || fortRush || FortRunOn || World.GameOver || IsMinion || DriverOutHeldHere()) return false;
            for (int i = 0; i < Waypoints.Count; i++)
            {
                var w = Waypoints[i];
                if (w.refId == ZWorld.RefRam) continue;
                return (w.mode == WpMode.Move || w.mode == WpMode.Attack || w.mode == WpMode.Agro) && !ZWorld.IsLavaWalk(w);
            }
            return true;
        }

        /// <summary>RamWayClear: DV195 this big tank's own doomed victims are no obstacle (they are destroyed within the fallback).</summary>
        bool RamChainOwn(ZObject o) => ZWorld.RamChainDeviation && o.RamDoomBy == Id && o.RamDoomBy >= 0;
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV195 (Sim/ZObject.RamChain.cs). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool RamChainDeviation = false;

        /// <summary>The parity report's Zod rows and probes and the older counted checks: off while this lives.</summary>
        public static IDisposable RamChainOff() => new RamChainOffHold();
        sealed class RamChainOffHold : IDisposable
        {
            readonly bool was = RamChainDeviation;
            public RamChainOffHold() { RamChainDeviation = false; }
            public void Dispose() { RamChainDeviation = was; }
        }

        /// <summary>DV195 N24: orders to a doomed vehicle or gun are refused (SendWaypoints), counted.</summary>
        internal bool RamStillRefuses(ZObject o)
        {
            if (!RamChainDeviation || o == null || o.RamDoomBy < 0 || o.Destroyed) return false;
            RamStillOrdersRefused++;
            return true;
        }

        /// <summary>DV195 N24, ZPathing.Static: a doomed vehicle or gun never moves (the pathing layer's pushes, step-asides and shoves leave it).</summary>
        public static bool RamStillStatic(ZObject o) => RamChainDeviation && o.RamDoomBy >= 0;

        /// <summary>DV195, RamPair: the big tank that doomed b and b may overlap until b is destroyed (the spacing gate and push leave the pair).</summary>
        public static bool RamChainPair(ZObject a, ZObject b) =>
            RamChainDeviation && a != null && b != null && b.RamDoomBy >= 0 && b.RamDoomBy == a.Id && !b.Destroyed && b.KillAt < 0;

        /// <summary>Statistics (the counted check, the Play check): rams chained at a hit, chained rams that drove in the same tick, held drives ended
        /// early for the next target, chains refused for the way, orders to a doomed unit refused.</summary>
        public int RamChains, RamChainSameTick, RamChainEarlyEnds, RamChainRefusedWay, RamStillOrdersRefused;
    }
}
