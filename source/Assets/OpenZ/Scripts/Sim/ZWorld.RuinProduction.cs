namespace OpenZ.Sim
{
    /// <summary>
    /// DV155 (1.3 item 4): no build clock on a destroyed factory, and nothing comes out of it when it is rebuilt.
    /// <para>What happened (Zod does the same): a destroyed factory or fort stops its production (ProcessKillObject, zobject.cpp:2215-2231)
    /// and a destroyed building other than a fort is rebuilt by itself 600-660 s later (DoAutoRepair, zobject.cpp:4235-4257,
    /// building_auto_repair_time 600 + 0-60 s, zsettings.cpp:278-279). Zod's SetBuildingProduction has no destroyed check, and the
    /// computer player re-sends its pick to every building of its team every order round (ChooseBuildOrders, zbot.cpp:1372-1432),
    /// the ruin included. So the ruin's production started again a few seconds after the blast with its start time then; a ruin is
    /// not processed (ZObject.ProcessServer returns while Destroyed), so the clock ran out unseen, and on the first tick after the
    /// rebuild the unit was finished at once: the factory came back with its last unit. A flag taken while its factory lies in
    /// ruins did the same through the default production (AwardZone -> SetBuildingDefaultProduction).</para>
    /// <para>The rule: a destroyed building takes no production and no queue entry (ZObject.SetBuildingProduction / AddBuildingQueue);
    /// the computer sends no orders for it (ZBot.ChooseBuildOrders: its pick is still made, so every draw stays as before); a rebuilt
    /// factory starts with nothing in progress and an empty queue (AutoRepair). The rebuild itself is Zod's, unchanged, for every
    /// team; the computer gives the rebuilt factory its pick at its next order round, which then takes the full build time.</para>
    /// <para>Switches (code switches, static like FifoQueueDeviation, the same in every world): <see cref="RuinNoBuildDeviation"/>
    /// <see cref="FactoryAutoRebuild"/>
    /// destroyed factory stays a ruin until a crane repairs it;
    /// <see cref="RuinReviveDefault"/> false = a rebuilt factory is idle (default), true = it starts Zod's default unit from that
    /// moment (the full build time).</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV155: a destroyed building takes no production; a rebuilt one starts empty. Off = Zod exact.</summary>
        public static bool RuinNoBuildDeviation = true;
        /// <summary>DV155 owner choice: true = Zod's own rebuild of a destroyed factory after 600-660 s (default); false = it stays
        /// a ruin until a crane repairs it (radars, repair stations and bridges keep Zod's rebuild either way).</summary>
        public static bool FactoryAutoRebuild = true;
        /// <summary>DV155 owner choice: false = a rebuilt factory is idle until its owner picks a unit (default); true = it starts the
        /// first unit of its build list at once (Zod's default production), from the moment it is rebuilt.</summary>
        public static bool RuinReviveDefault = false;

        /// <summary>DV155 statistics (checks): production starts / queue entries refused on a ruin, rebuilt producers emptied, factory
        /// rebuilds switched off (FactoryAutoRebuild false).</summary>
        public static int RuinStartsRefused, RuinRevivedEmpty, RuinRebuildsOff;

        /// <summary>OnKilled, after Zod's rebuild roll (drawn as before, so every later roll stays Zod's): the owner's switch.</summary>
        void RuinRebuildRule(ZObject o)
        {
            if (!RuinNoBuildDeviation || FactoryAutoRebuild || !o.IsProducer || o.IsFort) return;
            o.DoAutoRepair = false; RuinRebuildsOff++;
        }

        /// <summary>AutoRepair, before the health comes back: whatever was left on the ruin goes (nothing in progress, empty queue).
        /// Only a destroyed one: AutoRepair is also the crane's DV61 mend of a damaged building that still stands (ZObject.Buildings.cs,
        /// Stage.ExitBuilding: DoAutoRepair with NextAutoRepairTime 0), and that factory keeps its build and its queue. True = it was a ruin.</summary>
        bool RuinRevives(ZObject o)
        {
            if (!o.Destroyed) return false;
            if (!RuinNoBuildDeviation || !o.IsProducer) return true;
            if (o.Building || o.CurUnit != null || o.Queue.Count > 0) RuinRevivedEmpty++;
            o.StopBuildingProduction();
            return true;
        }

        /// <summary>AutoRepair, after the health came back, a former ruin only: the owner's alternative (RuinReviveDefault) only.</summary>
        void RuinRevived(ZObject o)
        {
            if (!RuinNoBuildDeviation || !RuinReviveDefault || !o.IsProducer || o.Owner == Team.Neutral) return;
            if (o.SetBuildingDefaultProduction()) Emit(ZEventType.ProductionChanged, o);
        }
    }
}
