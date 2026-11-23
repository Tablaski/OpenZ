// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: the radar circle's growth. Plain C#, no Unity, never read by the rules (view only).
using System;
using System.Collections.Generic;

namespace OpenZ
{
    /// <summary>
    /// View/OzFogOfWar.Radar.cs
    /// feeds it every frame with the player's standing radars; the reveal map is <see cref="OzFowMask"/>.
    /// <list type="bullet">
    /// <item>Every standing radar of the player's side opens a circle of <see cref="CirclePx"/> Zod px (48 m) round its centre
    /// (<see cref="OzFowMask.StampFractional"/>, a max-blit: explore-once, ground once opened is never covered again).</item>
    /// <item>A radar newly seen (gained with its zone, rebuilt from a ruin, or every radar after <see cref="Reset"/>: the testing panel's
    /// COVER AGAIN) grows its circle to 48 m over <see cref="GrowSeconds"/> of real time with smootherstep easing (<see cref="Ease"/>,
    /// 6t^5 - 15t^4 + 10t^3: starts gently, speeds up, settles gently), stamped every frame with the fractional radius: the edge never moves
    /// in whole-metre steps and nothing snaps, also not at the end. A frame longer than <see cref="MaxStepSeconds"/> (a hitch) moves the
    /// growth by that much only.</item>
    /// <item>It grows from the radar's own building sight (the sight OzFogOfWar.SightPx gives it, 20 m: the fog's stamp round opens that
    /// ground with the zone's flag and buildings, the game's normal reveal of a capture). Where the ground already open round the radar
    /// reaches further all round (<see cref="OpenEdgePx"/>: a regain after the circle was lost half grown), it grows from that edge, so the
    /// visible motion always starts gently and no time passes with nothing to see.</item>
    /// <item>Radars seen with <c>instant</c> (the match start) open the whole circle at once (the start area rule).</item>
    /// <item>A radar not seen in a round (lost, a ruin) forgets its growth, so a regain grows again (open ground simply stays open).</item>
    /// </list>
    /// </summary>
    public sealed class OzFowRadar
    {
        /// <summary>The radar circle: 384 Zod px = 48 m (8 px = 1 m), to the outer end of the soft edge as every sight circle.</summary>
        public const int CirclePx = Sim.ZBattleSight.RadarSightPx;
        /// <summary>The opening's length (s, real time).</summary>
        public const float GrowSeconds = 2.5f;
        /// <summary>The most one frame moves the growth (s): a hitch slows it down, it never jumps.</summary>
        public const float MaxStepSeconds = 0.1f;

        /// <summary>What <see cref="See"/> did with a radar this round.</summary>
        public enum Step { Idle, Started, Growing, Opened, OpenedAtOnce, AlreadyOpen }

        sealed class Growth { public float From, Edge, Age, Radius; public bool Done; public int Round; }
        readonly Dictionary<int, Growth> growths = new Dictionary<int, Growth>();
        readonly List<int> gone = new List<int>();
        int round; float dt;

        /// <summary>Radars whose circle is still growing after the last round.</summary>
        public int Growing { get; private set; }
        /// <summary>Checks: openings started (animated), circles opened at once (the match start), radars whose circle was all open already
        /// (nothing to animate), growths forgotten (lost, a ruin).</summary>
        public int Started, AtOnce, AlreadyOpen, Forgotten;

        /// <summary>Smootherstep, 6t^5 - 15t^4 + 10t^3 (0 at 0, 1 at 1, its first and second derivatives 0 at both ends).</summary>
        public static double Ease(double t)
        {
            if (t <= 0.0) return 0.0;
            if (t >= 1.0) return 1.0;
            return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
        }

        /// <summary>The circle's radius (Zod px) <paramref name="age"/> seconds after the opening began from <paramref name="fromPx"/>.</summary>
        public static float RadiusAt(float fromPx, float age) =>
            age >= GrowSeconds ? CirclePx : (float)(fromPx + (CirclePx - fromPx) * Ease(age / GrowSeconds));

        /// <summary>The start radius (Zod px): the radar's building sight, or the edge of the ground already open round it where that is further.</summary>
        public static float StartPx(int sightPx, float openEdgePx) => Math.Min(Math.Max(sightPx, openEdgePx), CirclePx);

        /// <summary>The edge of the ground already open round the Zod px point (cx, cy) (Zod px): the largest radius whose
        /// <see cref="OzFowMask.StampFractional"/> would raise no cell of Target (a cell not fully open limits it to its distance plus its
        /// own value's share of the soft edge); <paramref name="maxPx"/> when everything up to there is open.</summary>
        public static float OpenEdgePx(OzFowMask m, int cx, int cy, int maxPx)
        {
            double fx = cx / (double)OzFowMask.CellPx, fy = cy / (double)OzFowMask.CellPx, best = maxPx / (double)OzFowMask.CellPx;
            int x0 = Math.Max(0, (int)Math.Floor(fx - best)), x1 = Math.Min(m.W - 1, (int)Math.Floor(fx + best));
            int y0 = Math.Max(0, (int)Math.Floor(fy - best)), y1 = Math.Min(m.H - 1, (int)Math.Floor(fy + best));
            var t = m.Target;
            for (int y = y0; y <= y1; y++)
            {
                double dy = y + 0.5 - fy; if (Math.Abs(dy) >= best) continue;
                int row = y * m.W;
                for (int x = x0; x <= x1; x++)
                {
                    byte v = t[row + x]; if (v == 255) continue;
                    double dx = x + 0.5 - fx, e = Math.Sqrt(dx * dx + dy * dy) + OzFowMask.RampCells * v / 255.0;
                    if (e < best) best = e;
                }
            }
            return (float)(best * OzFowMask.CellPx);
        }

        /// <summary>Starts a round (one a frame): <paramref name="frameSeconds"/> is the frame's real time.</summary>
        public void Begin(float frameSeconds)
        {
            round++; Growing = 0;
            dt = frameSeconds > 0f ? Math.Min(frameSeconds, MaxStepSeconds) : 0f;
        }

        /// <summary>A standing radar of the player's side this round (<paramref name="sightPx"/>: its building sight): stamps its circle while
        /// it grows (and once when it is first seen).</summary>
        public Step See(OzFowMask m, int id, int cx, int cy, int sightPx, bool instant)
        {
            Step s = Step.Idle;
            if (!growths.TryGetValue(id, out var g))
            {
                g = new Growth();
                growths[id] = g;
                if (instant) { g.Edge = g.From = CirclePx; g.Age = GrowSeconds; g.Radius = CirclePx; g.Done = true; AtOnce++; m.StampFractional(cx, cy, CirclePx); s = Step.OpenedAtOnce; }
                else
                {
                    g.Edge = OpenEdgePx(m, cx, cy, CirclePx);
                    g.From = StartPx(sightPx, g.Edge);
                    if (g.From >= CirclePx - 1f) { g.Age = GrowSeconds; g.Radius = CirclePx; g.Done = true; AlreadyOpen++; s = Step.AlreadyOpen; }   // all open (within a px: the soft edge's byte rounding): nothing to grow
                    else { g.Radius = g.From; m.StampFractional(cx, cy, g.Radius); Growing++; Started++; s = Step.Started; }
                }
            }
            else if (!g.Done)
            {
                g.Age += dt;
                g.Radius = RadiusAt(g.From, g.Age);
                m.StampFractional(cx, cy, g.Radius);
                if (g.Age >= GrowSeconds) { g.Done = true; s = Step.Opened; }
                else { Growing++; s = Step.Growing; }
            }
            g.Round = round;
            return s;
        }

        /// <summary>Ends a round: radars not seen in it (lost, a ruin) forget their growth.</summary>
        public void End()
        {
            if (growths.Count == 0) return;
            gone.Clear();
            foreach (var kv in growths) if (kv.Value.Round != round) gone.Add(kv.Key);
            for (int i = 0; i < gone.Count; i++) growths.Remove(gone[i]);
            Forgotten += gone.Count;
        }

        /// <summary>Every growth forgotten: the radars still ours open their circle again, animated (the testing panel's COVER AGAIN).</summary>
        public void Reset() { growths.Clear(); Growing = 0; }

        /// <summary>Checks: the circle radius of a radar now (Zod px), -1 when it has none (not ours, a ruin).</summary>
        public float RadiusOf(int id) => growths.TryGetValue(id, out var g) ? g.Radius : -1f;
        /// <summary>Checks: the seconds a radar's circle has grown, -1 when it has none.</summary>
        public float AgeOf(int id) => growths.TryGetValue(id, out var g) ? g.Age : -1f;
        /// <summary>Checks: the radius the growth began from (Zod px), -1 when none.</summary>
        public float FromOf(int id) => growths.TryGetValue(id, out var g) ? g.From : -1f;
        /// <summary>Checks: the open ground's edge measured when the growth began (<see cref="OpenEdgePx"/>, Zod px), -1 when none.</summary>
        public float EdgeOf(int id) => growths.TryGetValue(id, out var g) ? g.Edge : -1f;
        /// <summary>Checks: radars with a circle (growing or open).</summary>
        public int Count => growths.Count;
    }
}
