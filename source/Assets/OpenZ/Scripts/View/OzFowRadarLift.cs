// OpenZ (GPL-3.0) - version 1.5 kit r14_r16: MAYHEM's line-of-sight fog lifted over the whole map while a radar of ours stands. Plain C#, no Unity,
// never read by the rules (view only).
using System;

namespace OpenZ
{
    /// <summary>
    /// One number for the whole map, <see cref="Value"/>: 0 = the line-of-sight fog as it is (View/OzFogOfWar.Los.cs), 1 =
    /// lifted (every explored cell counts as seen now: no darkening, no veil, no scan lines, the enemy units there shown). It moves toward 1 while the
    /// player's side owns at least one standing radar and toward 0 when it owns none, linear in time over <see cref="Seconds"/> both ways (the same easing
    /// in and out: the look takes its smootherstep, <see cref="Eased"/>; a change half way turns back from where it is, no jump). The units' hiding reads
    /// <see cref="Lift"/>: a cell's seen value is max(its own, <see cref="Floor"/>), so the units on explored ground show when the lift passes half way
    /// (128 of 255) and hide again only under 77 (the line-of-sight fog's own gap: no blink). The cloud over ground never explored and the radar's own
    /// reveal circle (View/OzFowRadar.cs) are not touched.
    /// </summary>
    public sealed class OzFowRadarLift
    {
        /// <summary>Seconds the lift takes, out and back.</summary>
        public const float Seconds = 1.0f;
        /// <summary>0 .. 1, linear in time (see the class notes).</summary>
        public float Value { get; private set; }
        /// <summary>A standing radar of ours as the last step saw it.</summary>
        public bool Radar { get; private set; }
        /// <summary>Checks: radars gained (none -> some) and lost (some -> none) since the start.</summary>
        public int Gains, Losses;

        /// <summary>The match start: at once, no fade on the first frame.</summary>
        public void Start(bool radar) { Radar = radar; Value = radar ? 1f : 0f; Gains = Losses = 0; }

        /// <summary>A frame of dt seconds; radar = the player's side owns a standing radar now. Returns 1 when one was gained in this step, -1 when the last
        /// was lost, 0 otherwise.</summary>
        public int Step(float dt, bool radar)
        {
            int turn = 0;
            if (radar != Radar) { Radar = radar; if (radar) { Gains++; turn = 1; } else { Losses++; turn = -1; } }
            float goal = radar ? 1f : 0f;
            if (Value != goal && dt > 0f)
            {
                float d = dt / Seconds;
                Value = goal > Value ? Math.Min(goal, Value + d) : Math.Max(goal, Value - d);
            }
            return turn;
        }

        /// <summary>The look's share (smootherstep of <see cref="Value"/>): the darkening, the veil and the scan lines are times (1 - this).</summary>
        public float Eased { get { float x = Value; return x <= 0f ? 0f : x >= 1f ? 1f : x * x * x * (x * (x * 6f - 15f) + 10f); } }
        /// <summary>The seen value (0 .. 255) every explored cell has at least now, for the units' hiding.</summary>
        public byte Floor => (byte)Math.Min(255, (int)(Value * 255f + 0.5f));
        /// <summary>A cell's seen value with the lift: max(its own, <see cref="Floor"/>).</summary>
        public byte Lift(byte v) { byte f = Floor; return v > f ? v : f; }
        /// <summary>Fully lifted: the line-of-sight fog's pass block can be skipped.</summary>
        public bool Full => Value >= 1f;
    }
}
