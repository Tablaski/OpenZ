using System;
using System.Collections.Generic;

namespace OpenZ
{
    /// <summary>
    /// the counted check (Editor/OzL07LavaCount.cs) runs it headless with the same seed and gets the same pops.
    /// The rate wanders: rate(t) = BaseRate * exp(Swing * x(t)), where x is a slow mean-reverting random walk (an Ornstein-Uhlenbeck mood,
    /// time constant MoodSeconds), so there are busy minutes and quiet minutes; inside them the pops come at random (a Poisson stream).
    /// Each pop picks a lava tile (a hot pool tile weighs HotWeight times a crust tile), a spot inside it, and a size: big (BigShare)
    /// or small. View only; its own System.Random (the sim's rand() is never touched).
    /// </summary>
    public sealed class OzLavaPops
    {
        /// <summary>Map-wide pops per second at the middle mood; how far the mood swings (log scale); its time constant (s); hot weight;
        /// share of big pops; sizes (m).</summary>
        public const double BaseRate = 0.55, Swing = 0.85, MoodSeconds = 28.0, MoodLimit = 2.0, HotWeight = 4.0, BigShare = 0.16;
        public const float SmallSize = 0.55f, BigSize = 1.25f;

        public struct Pop { public float x, z, size; public bool big; public double t; }

        readonly Random rng;
        readonly List<(float x, float z)> spots = new List<(float, float)>();
        readonly List<double> cum = new List<double>();
        double total, mood, next, time;

        /// <summary>The lava tiles (tile x, tile y, kind 1 crust / 2 hot) in the OpenZ frame: tile centre = (tx * 2 + 1, -(ty * 2 + 1)).</summary>
        public OzLavaPops(IEnumerable<(int tx, int ty, int kind)> tiles, int seed)
        {
            rng = new Random(seed);
            foreach (var (tx, ty, kind) in tiles)
            {
                spots.Add((tx * 2f + 1f, -(ty * 2f + 1f)));
                total += kind == 2 ? HotWeight : 1.0;
                cum.Add(total);
            }
            next = Draw();
        }

        public int Tiles => spots.Count;
        /// <summary>The pops per second now (the wandering rate).</summary>
        public double Rate => BaseRate * Math.Exp(Swing * mood);
        public double Mood => mood;

        double Gauss() { double u = 1.0 - rng.NextDouble(), v = rng.NextDouble(); return Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * v); }
        double Draw() => -Math.Log(1.0 - rng.NextDouble());   // unit-rate exponential: "work" until the next pop

        /// <summary>Advance dt seconds (any frame length; long frames are split into 0.1 s steps); new pops go into <paramref name="into"/>.</summary>
        public void Step(double dt, List<Pop> into)
        {
            if (spots.Count == 0 || dt <= 0) return;
            while (dt > 1e-9)
            {
                double h = Math.Min(dt, 0.1); dt -= h; time += h;
                mood += -mood * h / MoodSeconds + Math.Sqrt(2.0 * h / MoodSeconds) * Gauss();   // OU, stationary deviation 1
                if (mood > MoodLimit) mood = MoodLimit; else if (mood < -MoodLimit) mood = -MoodLimit;   // never a storm, never a dead minute
                next -= Rate * h;
                while (next <= 0)
                {
                    into.Add(Make());
                    next += Draw();
                }
            }
        }

        Pop Make()
        {
            double r = rng.NextDouble() * total;
            int lo = 0, hi = cum.Count - 1;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (cum[mid] < r) lo = mid + 1; else hi = mid; }
            var (x, z) = spots[lo];
            bool big = rng.NextDouble() < BigShare;
            return new Pop { x = x + (float)(rng.NextDouble() * 1.6 - 0.8), z = z + (float)(rng.NextDouble() * 1.6 - 0.8), big = big, size = big ? BigSize : SmallSize, t = time };
        }
    }

    /// <summary>
    /// Level 7, kit LV2: what comes on top of the seeded stream, from a SECOND generator, so OzLavaPops gives the very same pops as ever (the
    /// counted check Editor/OzL07LavaCount.cs reads that stream). A stream pop may start a cluster: ClusterShare of them bring 1-3 small
    /// followers close by within ClusterSeconds; GiantShare of them are drawn as the rare 2 m giant. Plain C#, no Unity.
    /// </summary>
    public sealed class OzLavaPopExtras
    {
        public const double ClusterShare = 0.30, ClusterSeconds = 1.5, GiantShare = 0.02, ClusterRadius = 1.6;
        const int MaxLater = 32;
        readonly Random rng;
        readonly OzLavaPops.Pop[] later = new OzLavaPops.Pop[MaxLater];
        int count;

        public OzLavaPopExtras(int seed) { rng = new Random(seed ^ 0x51ED27); }
        /// <summary>Seconds this has run (stops with the pause).</summary>
        public double Time { get; private set; }
        public double Next() => rng.NextDouble();
        public bool Giant() => rng.NextDouble() < GiantShare;

        /// <summary>A stream pop has just come: returns how many followers it brings.</summary>
        public int Offer(in OzLavaPops.Pop p)
        {
            if (rng.NextDouble() >= ClusterShare) return 0;
            int n = 1 + rng.Next(3), made = 0;
            for (int i = 0; i < n && count < MaxLater; i++)
            {
                double a = rng.NextDouble() * 2.0 * Math.PI, r = ClusterRadius * Math.Sqrt(rng.NextDouble());
                later[count++] = new OzLavaPops.Pop { x = p.x + (float)(Math.Cos(a) * r), z = p.z + (float)(Math.Sin(a) * r), size = OzLavaPops.SmallSize, big = false, t = Time + ClusterSeconds * rng.NextDouble() };
                made++;
            }
            return made;
        }

        /// <summary>Advance dt seconds; the followers now due go into <paramref name="into"/>.</summary>
        public void Step(double dt, List<OzLavaPops.Pop> into)
        {
            if (dt <= 0) return;
            Time += dt;
            for (int i = 0; i < count;)
            {
                if (later[i].t > Time) { i++; continue; }
                into.Add(later[i]); later[i] = later[--count];
            }
        }
    }
}
