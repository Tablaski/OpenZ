namespace OpenZ.Sim
{
    /// <summary>
    /// fort builds its queue (up to 5 entries) in order, and when the queue has run out REPEAT keeps building the last unit, or the
    /// building goes idle. With REPEAT QUEUE switched on for a building, every unit it finishes goes back to the END of its queue, so
    /// the whole list (the unit in production and the queue after it) loops: grunt, light, medium, grunt, light, medium, ...
    /// <list type="bullet">
    /// <item><b>Per building, off by default</b>: <see cref="ZObject.RepeatQueue"/> is false for every building; only the player's toggle in the
    /// build menu (<see cref="ZWorld.SetBuildingRepeatQueue"/>, the owner's command like DV21's REPEAT) sets it. Off = exactly today's queue.</item>
    /// <item><b>With REPEAT</b>: while REPEAT QUEUE is on the queue never runs out, so REPEAT does not matter; a list of one (no queue behind
    /// the unit in production) loops too, which keeps building that unit whatever REPEAT says. REPEAT keeps its own state and takes over
    /// again when REPEAT QUEUE is switched off.</item>
    /// <item><b>Removing</b>: a queue entry removed by the player (a click on its slot, a right-click on its tile) is out of the loop for good.
    /// STOP stops production and clears the queue as today; the switch stays as the player left it.</item>
    /// <item><b>A unit the building cannot build any more</b> (it left the build list of the building's level, e.g. after a capture by a team
    /// with another difficulty bonus): it is skipped and dropped from the loop, as today's queue drops an entry it cannot start, and the
    /// loop goes on with the next entry; when no entry is left that it can build, the building goes idle as today.</item>
    /// <item><b>The unit cap (70)</b>: nothing is skipped; production waits at the cap as today (ZObject.ProcessBuilding) and the loop goes on
    /// in the same order once there is room.</item>
    /// <item><b>A captured building</b> starts with REPEAT QUEUE off (Sim/ZWorld.Rules.cs SetOwnerZod, next to DV21's REPEAT on).</item>
    /// <item><b>The computer</b> never switches it on (ZBot has no call to it; the computer only starts production, it never queues), so a
    /// bot match is the same tick for tick.</item>
    /// </list>
    /// Deterministic: it runs inside ResetProduction, after a unit is built (ZWorld.RulesSideEffects, object order); list operations and
    /// the build-list test only: no random draw, no float, no clock. Switch <see cref="ZWorld.RepeatQueueDeviation"/>: off in the rule file
    /// (headless worlds and the parity's Zod rows keep the rules as they were); View/ZGame.cs turns it on at every start unless 'Zod exact'.
    /// It acts only inside DV21's first-in-first-out branch (ZWorld.FifoQueueDeviation).
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV169 REPEAT QUEUE (off in the rule file; View/ZGame.cs turns it on at every start unless 'Zod exact').</summary>
        public static bool RepeatQueueDeviation = false;
        /// <summary>DV169 statistics (checks, Play log): units the rule sent back to the end of a queue.</summary>
        public int RepeatQueueTurns { get; internal set; }
        /// <summary>DV169 statistics: loop entries skipped and dropped because the building could not build them any more.</summary>
        public int RepeatQueueSkips { get; internal set; }

        /// <summary>DV169 command: the REPEAT QUEUE switch of a factory / fort the team owns (the player's build menu; the computer never
        /// calls it). Refused while the rule is off ('Zod exact') or without DV21's queue.</summary>
        public bool SetBuildingRepeatQueue(Team team, ZObject b, bool on)
        {
            // OpenZ duel command: RepeatQueue
            if (DuelDefersOrders) return DuelSend(team, b, new DuelOrder { Kind = DuelOrderKind.RepeatQueue, Enabled = on });
            if (!RepeatQueueDeviation || !FifoQueueDeviation || !Owns(team, b) || !b.IsProducer) return false;
            b.RepeatQueue = on; Emit(ZEventType.ProductionChanged, b); return true;
        }
    }

    public partial class ZObject
    {
        /// <summary>DV169: the building's REPEAT QUEUE switch (off by default; off again when the building changes owner).</summary>
        public bool RepeatQueue;

        /// <summary>DV169, called first in ResetProduction's DV21 branch (a unit was just built): true = this rule made the step. The finished
        /// unit (CurUnit) goes to the end of the queue, then the front entry is started; an entry the building cannot build any more is
        /// skipped and dropped. False = the rule is off for this building: today's step runs unchanged.</summary>
        bool RepeatQueueStep()
        {
            if (!ZWorld.RepeatQueueDeviation || !RepeatQueue || CurUnit == null) return false;
            Queue.Add(CurUnit);
            World.RepeatQueueTurns++;
            StopBuildingProduction(false);
            while (Queue.Count > 0)
            {
                string next = Queue[0];
                Queue.RemoveAt(0);
                if (ZBuildLists.InList(Type, BuildLevel, next) && World.DuelAllowsUnit(next)) { SetBuildingProduction(next); return true; }
                World.RepeatQueueSkips++;
            }
            return true;
        }
    }
}
