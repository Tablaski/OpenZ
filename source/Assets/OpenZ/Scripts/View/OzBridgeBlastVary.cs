// OpenZ (GPL-3.0) - version 1.5, kit long_bridge_blast_vary_r6 (14 Nov 2026): the charges of a LONG bridge that dies in normal play looked
// like one blast pasted three times (same size, same facing, same water columns at the same places, same beat). Here each charge of one
// death draws its own turn about the vertical axis, size, moment, side and place; View/OzBridgeDeathExp.cs draws them.
// VIEW ONLY and plain C#: no Sim type, no UnityEngine type. Its numbers come from its OWN generator (xorshift32, seeded from the clock
// and a count of the deaths drawn): never the rules' random stream, never UnityEngine.Random. The level 18 briefing's blasts
// (View/Zod/OzL18BriefingStage.Blast.cs) do not call this file and stay as they are.
namespace OpenZ
{
    public static class OzBridgeBlastVary
    {
        /// <summary>What one charge of a long bridge's death draws for itself.</summary>
        public struct Turn
        {
            /// <summary>Its facing about the vertical axis, 0..360 degrees (the oval of its ball and the way its head leans).</summary>
            public float yawDeg;
            /// <summary>Its size (1 = the briefing's ball), 1 - Size .. 1 + Size.</summary>
            public float size;
            /// <summary>Seconds from this charge to the next one.</summary>
            public float gap;
            /// <summary>+1 or -1: the mirrored twin (which side gets the early, bigger water column and which way the dark head bends).</summary>
            public float side;
            /// <summary>Metres along the span from its even place.</summary>
            public float along;
            /// <summary>Its two water columns: degrees off the straight-across line, distance (x the plain distance, never nearer), size (x), s later.</summary>
            public float w0Turn, w1Turn, w0Far, w1Far, w0Size, w1Size, w0Late, w1Late;
        }

        /// <summary>On / off (off = every charge the same, as before the kit).</summary>
        public static bool On = true;
        /// <summary>No two charges of one death face within this many degrees of each other (up to six charges; more: see Pick).</summary>
        public static float MinYawDeg = 60f;
        /// <summary>Size 1 +- this; the gap between charges +- this many s; a charge's place +- this many m along the span.</summary>
        public static float Size = 0.15f, GapPlay = 0.05f, AlongM = 1.5f;
        /// <summary>The ball's oval (0 = round: long axis x (1 + Oval), short axis x (1 - Oval / 2)); the lean of its rising head (m sideways per m up).</summary>
        public static float Oval = 0.22f, Lean = 0.3f;
        /// <summary>The water columns: up to this many degrees off the straight-across line, up to this much farther out (x), size 1 +- this, up to this many s later.</summary>
        public static float WaterTurnDeg = 35f, WaterFar = 0.35f, WaterSize = 0.15f, WaterLate = 0.10f;
        /// <summary>0 = seeded from the clock (every death different). Another number = a repeatable row of deaths (checks, side-by-side frames).</summary>
        public static int Seed = 0;
        /// <summary>Deaths drawn since the game started (part of the seed, so two deaths in the same clock tick still differ).</summary>
        public static int Deaths;

        static uint state = 2463534242u;
        static float R() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (state & 0xFFFFFF) / 16777216f; }

        static void Reseed()
        {
            Deaths++;
            uint s = Seed != 0 ? (uint)Seed : (uint)System.Diagnostics.Stopwatch.GetTimestamp() ^ ((uint)System.Environment.TickCount * 2246822519u);
            state = (s + (uint)Deaths * 2654435761u) * 747796405u + 2891336453u;
            if (state == 0u) state = 2463534242u;
            R(); R(); R(); R();
        }

        static void Shuffle(float[] a) { for (int i = a.Length - 1; i > 0; i--) { int j = (int)(R() * (i + 1)); if (j > i) j = i; float t = a[i]; a[i] = a[j]; a[j] = t; } }
        static float Wrap(float deg) { deg %= 360f; return deg < 0f ? deg + 360f : deg; }
        /// <summary>The angle between two facings, 0..180 degrees.</summary>
        public static float Apart(float a, float b) { float d = Wrap(a - b); return d > 180f ? 360f - d : d; }

        /// <summary>The draws of the n charges of one death, in the order the charges go off (chargeGap = the plain gap between charges, s).</summary>
        public static Turn[] Pick(int n, float chargeGap)
        {
            Reseed();
            if (n < 1) n = 1;
            var t = new Turn[n]; var yaw = new float[n]; var size = new float[n];
            float a0 = R() * 360f, min = MinYawDeg < 0f ? 0f : MinYawDeg > 110f ? 110f : MinYawDeg;
            if (n * min <= 360f)
            {   // n slots round the full circle, each facing inside the middle of its slot so that no two are nearer than min; dealt out in a shuffled order
                float slot = 360f / n, play = slot - min;
                for (int i = 0; i < n; i++) yaw[i] = Wrap(a0 + slot * i + play * (R() - 0.5f));
                Shuffle(yaw);
            }
            else
            {   // more charges than fit round the circle min apart (no bridge of the game is that long): each one about 137.5 degrees on from the one before
                for (int i = 0; i < n; i++) yaw[i] = Wrap(a0 + 137.5f * i + 30f * (R() - 0.5f));
            }
            for (int i = 0; i < n; i++) size[i] = 1f - Size + 2f * Size * (i + 0.2f + 0.6f * R()) / n;   // one size out of each of n bands: never two the same
            Shuffle(size);
            bool same = true;
            for (int i = 0; i < n; i++)
            {
                t[i].yawDeg = yaw[i]; t[i].size = size[i];
                t[i].gap = chargeGap + GapPlay * (2f * R() - 1f); if (t[i].gap < 0.03f) t[i].gap = 0.03f;
                t[i].side = R() < 0.5f ? -1f : 1f; if (i > 0 && t[i].side != t[0].side) same = false;
                t[i].along = AlongM * (2f * R() - 1f);
                t[i].w0Turn = WaterTurnDeg * (2f * R() - 1f); t[i].w1Turn = WaterTurnDeg * (2f * R() - 1f);
                t[i].w0Far = 1f + WaterFar * R(); t[i].w1Far = 1f + WaterFar * R();
                t[i].w0Size = 1f + WaterSize * (2f * R() - 1f); t[i].w1Size = 1f + WaterSize * (2f * R() - 1f);
                t[i].w0Late = WaterLate * R(); t[i].w1Late = WaterLate * R();
            }
            if (same && n > 1) { int k = (int)(R() * n); if (k >= n) k = n - 1; t[k].side = -t[k].side; }   // never every charge the same twin
            return t;
        }

        /// <summary>One line for the log: the facings, sizes, gaps and twins of a death.</summary>
        public static string Line(Turn[] t)
        {
            var sb = new System.Text.StringBuilder();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sb.Append("facing");
            for (int i = 0; i < t.Length; i++) sb.Append(i == 0 ? " " : " / ").Append(t[i].yawDeg.ToString("0", inv));
            sb.Append(" deg, size");
            for (int i = 0; i < t.Length; i++) sb.Append(i == 0 ? " " : " / ").Append(t[i].size.ToString("0.00", inv));
            sb.Append(", gaps");
            for (int i = 0; i < t.Length - 1; i++) sb.Append(i == 0 ? " " : " / ").Append(t[i].gap.ToString("0.00", inv));
            sb.Append(" s, twin ");
            for (int i = 0; i < t.Length; i++) sb.Append(t[i].side > 0f ? '+' : '-');
            sb.Append(", along");
            for (int i = 0; i < t.Length; i++) sb.Append(i == 0 ? " " : " / ").Append(t[i].along.ToString("+0.0;-0.0", inv));
            sb.Append(" m");
            return sb.ToString();
        }

        /// <summary>The counted check (no drawing): `deaths` deaths of 3, 4, 5 and 6 charges, from the clock and from a fixed seed; every pair of
        /// facings at least MinYawDeg apart (a hundredth of a degree of rounding allowed), the facings spread over the whole circle, sizes
        /// inside 1 +- Size and never two alike, gaps positive, never every charge the same twin, no water column nearer than the plain one,
        /// and no two deaths in a row the same. The generator's state and counts are put back.</summary>
        public static string Check(int deaths = 2000)
        {
            uint keepState = state; int keepDeaths = Deaths, keepSeed = Seed;
            int bad = 0, total = 0; float minApart = 999f, minSize = 9f, maxSize = 0f, minGap = 9f, maxGap = 0f, minSizeStep = 9f; var bins = new int[12];
            string last = "", why = "";
            for (int pass = 0; pass < 2; pass++)
            {
                Seed = pass == 0 ? 0 : 77;
                for (int n = 3; n <= 6; n++)
                    for (int d = 0; d < deaths; d++)
                    {
                        var t = Pick(n, 0.12f); total++; bool ok = true, same = true;
                        for (int i = 0; i < n; i++)
                        {
                            bins[(int)(t[i].yawDeg / 30f) % 12]++;
                            if (t[i].size < minSize) minSize = t[i].size;
                            if (t[i].size > maxSize) maxSize = t[i].size;
                            if (t[i].size < 1f - Size - 1e-4f || t[i].size > 1f + Size + 1e-4f) { ok = false; why = "size out of range"; }
                            if (i < n - 1)
                            {
                                if (t[i].gap < minGap) minGap = t[i].gap;
                                if (t[i].gap > maxGap) maxGap = t[i].gap;
                                if (t[i].gap <= 0f) { ok = false; why = "gap not positive"; }
                            }
                            if (t[i].side != t[0].side) same = false;
                            if (t[i].w0Far < 1f || t[i].w1Far < 1f) { ok = false; why = "a water column nearer than the plain one"; }
                            for (int j = i + 1; j < n; j++)
                            {
                                float a = Apart(t[i].yawDeg, t[j].yawDeg);
                                if (a < minApart) minApart = a;
                                if (a < MinYawDeg - 0.01f) { ok = false; why = "two facings too near"; }
                                float ds = t[i].size > t[j].size ? t[i].size - t[j].size : t[j].size - t[i].size;
                                if (ds < minSizeStep) minSizeStep = ds;
                                if (ds < 0.4f * 2f * Size / n - 1e-4f) { ok = false; why = "two sizes alike"; }
                            }
                        }
                        if (same) { ok = false; why = "every charge the same twin"; }
                        string line = Line(t);
                        if (line == last) { ok = false; why = "two deaths in a row the same"; }
                        last = line;
                        if (!ok) bad++;
                    }
            }
            int lo = int.MaxValue, hi = 0;
            for (int i = 0; i < 12; i++) { if (bins[i] < lo) lo = bins[i]; if (bins[i] > hi) hi = bins[i]; }
            bool spread = lo > 0 && hi < lo * 1.25f;
            state = keepState; Deaths = keepDeaths; Seed = keepSeed;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return string.Format(inv, "[OpenZ FX] long bridge blast vary check: {0} ({1} deaths of 3-6 charges, {2} bad{3}; nearest two facings {4:0.00} deg (rule {5:0}), facings per 30 deg sector {6}..{7}, sizes {8:0.000}..{9:0.000}, nearest two sizes {10:0.000}, gaps {11:0.000}..{12:0.000} s)",
                bad == 0 && spread ? "PASS" : "FAIL", total, bad, bad > 0 ? " (" + why + ")" : "", minApart, MinYawDeg, lo, hi, minSize, maxSize, minSizeStep, minGap, maxGap);
        }
    }
}
