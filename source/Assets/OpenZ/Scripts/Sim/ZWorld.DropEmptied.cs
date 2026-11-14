namespace OpenZ.Sim
{
    /// <summary>
    /// Nobody keeps shooting a vehicle that was just emptied.
    /// <para>Zod: when a crew is SHOT OUT (zserver.cpp:1849-1870, ZWorld.UpdateObjectDriverHealth) every unit whose target is the vehicle
    /// disengages and every unit drops its FIRST waypoint when that is an attack on it; an attack that waits further back in a unit's list stays
    /// (DV81 drops those for human teams only). EXIT DRIVER (DV171, ZWorld.ExitDriver) is not Zod's: it emptied the vehicle and dropped its own
    /// order and target, but not the others' attack on it: an enemy jeep or squad whose target or attack order was the tank went on shooting the
    /// empty hull until its order ran out.</para>
    /// <para>With DV176 (<see cref="EmptiedTargetsDeviation"/>, on): in the tick a vehicle is emptied, by EXIT DRIVER or by its crew shot out,
    /// every unit of EVERY team (the computer's too) whose target is that vehicle disengages, and every attack or agro waypoint on it is taken
    /// out of its list, the first and every queued one (a unit whose first waypoint went stops its move, as Zod's loop does). The shot-out path
    /// keeps Zod's own loop for the target and the first waypoint and adds the queued ones of the computer's teams (DV81 took the human
    /// teams' ones already). Rounds already in the air may still land. Nothing picks the null team by itself afterwards
    /// (ZObject.CheckPassiveEngage, CheckAttackTo, ZBot.Command: DV81), and an explicit Ctrl attack order given after the emptying is carried
    /// out as before (DV81). Integers and the world's own object order only: the same on every machine.</para>
    /// <para>What moves in the computer's game: a computer unit attacking a vehicle whose driver the player lets out stops at once instead of
    /// shelling the empty hull; a computer unit with an attack on a vehicle queued behind another order no longer carries it out once that
    /// vehicle's crew was shot out. The parity report's Zod rows switch the rule off (Editor/OpenZParity.cs).</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV176 switch (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off, as DV171 / DV173: kit
        /// u2_followups). Off = as before: EXIT DRIVER leaves the others' targets and orders on the emptied vehicle, a shot-out crew keeps Zod's
        /// clean-up (with DV81's human queued orders).</summary>
        public static bool EmptiedTargetsDeviation = false;

        /// <summary>Checks only (never read by a rule): targets dropped by the EXIT, attack waypoints dropped by the EXIT, queued attack waypoints
        /// of the computer's teams dropped after a shot-out crew (all worlds since the program started).</summary>
        public static int EmptiedTargetsDropped, EmptiedOrdersDropped, EmptiedQueuedDropped;

        /// <summary>DV176: vehicle <paramref name="v"/> was emptied in this tick. exit = EXIT DRIVER (no loop of Zod's runs after it: every target
        /// and every attack / agro waypoint on v, the first included, of every unit of every team); false = its crew shot out (Zod's loop runs
        /// next for the targets and the first waypoints: here the queued ones, every team).</summary>
        void DropAttacksOnEmptied(ZObject v, bool exit)
        {
            if (!EmptiedTargetsDeviation || v == null) return;
            for (int k = 0; k < Objects.Count; k++)
            {
                var o = Objects[k];
                if (o == v) continue;
                o.DropShotsOnEmptied(v, Time);                                                   // u2_followups: a fort gun committed to it and a heavy's loaded second shell at it drop it too (Sim/ZWorld.DropEmptiedShots.cs)
                if (exit && o.AttackObject == v) { o.Disengage(); EmptiedTargetsDropped++; }
                int from = exit ? 0 : 1;
                bool firstGone = false;
                for (int i = o.Waypoints.Count - 1; i >= from; i--)
                {
                    var q = o.Waypoints[i];
                    if ((q.mode != WpMode.Attack && q.mode != WpMode.Agro) || q.refId != v.Id) continue;
                    o.Waypoints.RemoveAt(i);
                    if (i == 0) firstGone = true;
                    if (exit) EmptiedOrdersDropped++; else EmptiedQueuedDropped++;
                }
                if (firstGone) o.StopMove();
            }
        }
    }
}
