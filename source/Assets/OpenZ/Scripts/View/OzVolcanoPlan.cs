// OpenZ (GPL-3.0) - version 1.3, level 7: the clock of the eruptions, kept apart from the drawing so its numbers can be counted with no scene.
// Plain C#, no Unity type, its own random numbers: nothing here reads or writes sim state or the sim's random.
using System;

namespace OpenZ
{
    /// <summary>When the next spit and the next big eruption are due (plan section 5, fix 14): map-wide one spit per 20-40 s and one big per 3-6
    /// minutes, none in the first 30 s, never a rhythm: the gaps ride a wandering mood (an Ornstein-Uhlenbeck walk, as OzLavaPops), so there
    /// are busy minutes and quiet minutes.</summary>
    public sealed class OzVolcanoPlan
    {
        public const float QuietStart = 30f, SpitGapMean = 29f, SpitGapLow = 14f, SpitGapHigh = 58f, BigGapLow = 180f, BigGapHigh = 360f, MoodTau = 70f;
        public const int None = 0, Spit = 1, Big = 2;
        readonly Random rng;
        double mood; bool hasSpare; double spare;
        float now, nextSpit, nextBig;
        public float Mood => (float)mood;

        public OzVolcanoPlan(int seed)
        {
            rng = new Random(seed);
            nextSpit = QuietStart + Next() * 20f;
            nextBig = 60f + Next() * 120f;                                                                      // the first big comes early: a player must see one in the first minutes
        }

        public float Next() => (float)rng.NextDouble();

        /// <summary>A normal random number (polar form), for the mood and the spread of the gaps.</summary>
        public double Gauss()
        {
            if (hasSpare) { hasSpare = false; return spare; }
            double u, v, s;
            do { u = 2.0 * rng.NextDouble() - 1.0; v = 2.0 * rng.NextDouble() - 1.0; s = u * u + v * v; } while (s >= 1.0 || s == 0.0);
            double k = Math.Sqrt(-2.0 * Math.Log(s) / s);
            spare = v * k; hasSpare = true; return u * k;
        }

        /// <summary>One step of the clock. Returns what is due now (the big one first), None for most frames. A due start stays due until
        /// <see cref="Started"/> or <see cref="Retry"/>. dt 0 (a paused game) stops the clock.</summary>
        public int Step(float dt, bool spitFree, bool bigFree)
        {
            if (dt <= 0f) return None;
            now += dt;
            mood += -mood * dt / MoodTau + Math.Sqrt(2.0 * dt / MoodTau) * Gauss();
            if (mood > 2.5) mood = 2.5; else if (mood < -2.5) mood = -2.5;
            if (bigFree && now >= nextBig) return Big;
            if (spitFree && now >= nextSpit) return Spit;
            return None;
        }

        public void Started(int kind)
        {
            if (kind == Big) nextBig = now + Clamp((BigGapLow + Next() * (BigGapHigh - BigGapLow)) * (float)Math.Exp(0.10 * mood), BigGapLow * 0.95f, BigGapHigh * 1.08f);
            else nextSpit = now + Clamp(SpitGapMean * (float)Math.Exp(0.30 * mood + 0.28 * Gauss()), SpitGapLow, SpitGapHigh);
        }

        /// <summary>No volcano fits now (units too close, all busy): ask again in a moment.</summary>
        public void Retry(int kind, float seconds) { if (kind == Big) nextBig = now + seconds; else nextSpit = now + seconds; }

        static float Clamp(float x, float a, float b) => x < a ? a : x > b ? b : x;
    }

    /// <summary>The fountain's strength over its seconds: 3-5 eased surges, never constant, never a repeat. Fixed arrays, no garbage.</summary>
    public struct OzVolcanoSurges
    {
        public const int Max = 5;
        float[] at, half, peak; int n;

        public void Make(OzVolcanoPlan r, float seconds, bool big)
        {
            if (at == null) { at = new float[Max]; half = new float[Max]; peak = new float[Max]; }
            n = big ? 3 + (int)(r.Next() * 3f) : 2;
            if (n > Max) n = Max;
            float slot = seconds / n;
            for (int i = 0; i < n; i++)
            {
                at[i] = (i + 0.25f + 0.5f * r.Next()) * slot;
                half[i] = slot * (0.62f + 0.3f * r.Next());
                peak[i] = i == 0 ? 1f : 0.55f + 0.45f * r.Next();
            }
        }

        /// <summary>0 .. 1 at second t of the fountain: the strongest surge there, on a floor of 0.14 so the mouth never stops.</summary>
        public float At(float t)
        {
            float s = 0.14f;
            for (int i = 0; i < n; i++)
            {
                float d = (t - at[i]) / half[i];
                if (d <= -1f || d >= 1f) continue;
                float c = 0.5f + 0.5f * (float)Math.Cos(d * Math.PI);                                           // eased in and out
                c *= peak[i]; if (c > s) s = c;
            }
            return s;
        }
    }

    /// <summary>A thrown rock's arc: real gravity, an exact landing.</summary>
    public static class OzVolcanoArc
    {
        public const float G = 9.81f;
        /// <summary>From height y0 to height y1 with the top of the arc apex metres over y0: the start speed up and the flight time.</summary>
        public static void Solve(float y0, float y1, float apex, out float vy, out float seconds)
        {
            vy = (float)Math.Sqrt(2.0 * G * Math.Max(apex, 0.1f));
            float fall = Math.Max(y0 + apex - y1, 0.05f);
            seconds = vy / G + (float)Math.Sqrt(2.0 * fall / G);
        }
    }
}
