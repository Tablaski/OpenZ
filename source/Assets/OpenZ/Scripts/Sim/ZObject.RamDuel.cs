using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV185 "the RAM DUEL" (kit big_tanks_fx, on DV182 the Mammoth's ram and DV183 the Bizon's crush, which share one
    /// Z has neither; every number is OpenZ's.
    /// <para>MUTUAL RAMS (Mammoth vs Bizon, Mammoth vs Mammoth, Bizon vs Bizon, enemies trying to ram each other): ONE clean result. When a tank
    /// would start a ram (or a crush) on a tank that is already ramming IT, the duel is decided at once: the ram that started FIRST wins (its
    /// start tick); started in the same tick: the LOWER object id wins. The loser's ram is cancelled that very tick (its waypoint goes, its order
    /// goes on from where it stands, its boost ends) and it does not start one on the winner while the winner's ram on it lasts; the winner's ram
    /// goes on as before and, on contact, flips (Mammoth) or crushes (Bizon) the loser. So never both destroyed by the pair's rams, never both
    /// driving into each other and stuck, never a flip and a crush on the same pair.</para>
    /// <para>A RAMMER RAMMED: a Mammoth rammed or crushed while it rams a third vehicle stops at once (DV182's RamDoomStop: its ram ends, its
    /// target is free and untouched unless its spikes had already hit it). A Bizon rammed or crushed while it rolls over a target it has already
    /// crushed: the crushed target is destroyed the next tick (it was under its front: it never lives on under a dead Bizon for the rest of the
    /// 3 s fallback), and the Bizon's crush state is cleared.</para>
    /// <para>Deterministic: tick counts and object ids only, no random draw, no clock. Switch <see cref="ZWorld.RamDuelDeviation"/>: View/ZGame.cs
    /// on at every start (off in 'Zod exact'); headless default off; the parity report and the counted checks hold it off; it only acts while
    /// DV182 or DV183 is on. Off: DV182 / DV183 exactly as before (the first to reach contact in object order wins).</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV185: the tick this tank's current ram started (-1 = none).</summary>
        public long RamStartTick => ram != null ? ram.startTick : -1;

        /// <summary>DV185: t is ramming this tank now (a mutual ram would follow if this one started on t).</summary>
        bool RamDuelMutual(ZObject t) => t != null && t.ram != null && t.ram.targetId == Id;

        /// <summary>DV185: the winner of a mutual ram between this tank (starting now) and t (already ramming it): the earlier start; the same tick:
        /// the lower object id. True = t keeps its ram and this tank does not start.</summary>
        bool RamDuelOtherWins(ZObject t, long now) => t.ram.startTick < now || (t.ram.startTick == now && t.Id < Id);

        /// <summary>RamStep, right before RamBegin: a mutual ram is decided here. False = this tank must not start (the other one won); true = it
        /// starts (no duel, or it won: the other's ram is cancelled now).</summary>
        bool RamDuelStart(ZObject pick, long now)
        {
            if (!ZWorld.RamDuelDeviation || !RamDuelMutual(pick)) return true;
            World.RamDuelDuels++;
            if (RamDuelOtherWins(pick, now)) { World.RamDuelYields++; return false; }
            pick.RamDuelLose();
            World.RamDuelWins++;
            return true;
        }

        /// <summary>DV185: this tank lost a duel: its ram is cancelled at once (its order goes on, the retry wait before it looks again).</summary>
        void RamDuelLose()
        {
            if (ram == null) return;
            if (crushOn) crushOn = false;
            RamEnd("duel", false);
            World.RamDuelCancels++;
        }

        /// <summary>RamPick (DV185): a tank whose ram on this one won (started earlier, or the same tick with a lower id) is never this tank's
        /// candidate while that ram lasts (no start, so no flip-flop between yielding and looking).</summary>
        bool RamDuelBarred(ZObject t, long now) => ZWorld.RamDuelDeviation && RamDuelMutual(t) && RamDuelOtherWins(t, now);

        /// <summary>RamDoomStop (DV185): a Bizon rammed or crushed while it rolls over a target it crushed: that target dies the next tick, the
        /// crush state is cleared.</summary>
        void RamDuelDoomed()
        {
            if (!ZWorld.RamDuelDeviation || ram == null || !crushOn || World == null) return;
            var v = World.Get(ram.targetId);
            if (v != null && v.RamDoomBy == Id && !v.Destroyed && v.KillAt < 0 && v.RamDoomTick > World.TickIndex + 1) { v.RamDoomTick = World.TickIndex + 1; World.RamDuelVictimsFreed++; }
            crushOn = false;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV185 the RAM DUEL (Sim/ZObject.RamDuel.cs). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool RamDuelDeviation = false;

        /// <summary>The parity report and the older counted checks: off while this lives.</summary>
        public static IDisposable RamDuelOff() => new RamDuelOffHold();
        sealed class RamDuelOffHold : IDisposable
        {
            readonly bool was = RamDuelDeviation;
            public RamDuelOffHold() { RamDuelDeviation = false; }
            public void Dispose() { RamDuelDeviation = was; }
        }

        /// <summary>Statistics (the counted check): mutual rams met at a start, starts refused (the other won), wins (the other's ram cancelled),
        /// rams cancelled, crushed targets destroyed early because their Bizon was doomed.</summary>
        public int RamDuelDuels, RamDuelYields, RamDuelWins, RamDuelCancels, RamDuelVictimsFreed;
    }
}
