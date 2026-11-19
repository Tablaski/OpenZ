using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// 1.5 rule DV178 SPRINT ORDER (kit fps_feel). Zod: a unit moves at its own speed (move_speed x its tile x its damage x Zod's run), whoever
    /// gives the order. OpenZ, while <see cref="ZWorld.SprintOrderDeviation"/> is on (View/ZGame.cs: on at every start, off in 'Zod exact';
    /// headless default off): a MOVE waypoint carrying the sprint mark (<see cref="ZWaypoint.sprint"/>) moves the unit x
    /// <see cref="ZWorld.SprintPct"/> / 100 while it is the unit's first waypoint (ProcessMoveCore, after every other factor of RealMoveSpeed;
    /// the velocity re-scaled the way a tile change re-scales it). The speed is worked out again from move_speed at every step, so the plain
    /// speed is back on the first step of any other waypoint (a new order, a step aside, an attack). A squad's members carry the leader's
    /// waypoints (CloneMinionWayPoints), so they sprint with it.
    /// Only the private first-person view sends the mark (View/PlayerInput.FpsFeel.cs FpsSprint, while Shift is held): no player order, no
    /// computer player, no menu and no counted check of the rules sends one, so every match without that view runs exactly as before.
    /// Integers for the factor (a percentage), no float, no random draw, no Unity, no clock; the world's own object order.
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV178: the first waypoint is a sprint move now (the rule's switch on).</summary>
        public bool SprintNow => ZWorld.SprintOrderDeviation && Waypoints.Count > 0 && Waypoints[0].sprint && Waypoints[0].mode == WpMode.Move;

        /// <summary>ProcessMoveCore, right after every other factor of RealMoveSpeed: the sprint factor while a sprint move is the first waypoint.</summary>
        void SprintPace()
        {
            if (!SprintNow) return;
            RealMoveSpeed = RealMoveSpeed * ZWorld.SprintPct / 100;
            World.SprintTicks++;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV178 SPRINT ORDER (Sim/ZObject.SprintOrder.cs). View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off
        /// (every Zod row, every older counted check and the bot tools as before).</summary>
        public static bool SprintOrderDeviation = false;
        /// <summary>DV178: a sprint move's speed in percent of the unit's own (150 = x 1.5, the usual run of a first-person game; Zod's own robot run
        /// is x 1.8 with stamina).</summary>
        public const int SprintPct = 150;
        /// <summary>DV178 statistics (checks; no rule reads it): unit steps moved with the factor.</summary>
        public int SprintTicks;

        /// <summary>DV178 held off (the parity's Zod rows and probes; a Play session leaves the switch on).</summary>
        public static IDisposable SprintOrderOff() => new SprintOrderHold();
        sealed class SprintOrderHold : IDisposable
        {
            readonly bool was = SprintOrderDeviation;
            public SprintOrderHold() { SprintOrderDeviation = false; }
            public void Dispose() { SprintOrderDeviation = was; }
        }
    }
}
