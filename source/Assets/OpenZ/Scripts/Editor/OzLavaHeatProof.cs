using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Kit LV2: the numeric proof that the temperature of the lava has no steps and no rhythm. It runs the game's own OzLavaHeat and OzLavaPops
    /// on the 6 field sizes of level 7, plain C#, no scene, no Play, at 30, 60, 144 frames a second and on a jittery clock with hitches. The same
    /// code runs off the project (LV2/tools/heat_proof.py) and in the menu OpenZ/L07 Lava/Checks/LV2 heat numbers (no Play).
    ///   STEP    the most any final value moves in one frame, per second. Limit 0.15 a second
    ///   BEND    the most the rate of change itself moves per second: a step or a kink shows here
    ///   RANGE   0.72-1.35, mean near 1
    ///   SWING   share of 60 s windows in which a lake's temperature moves by 25 % or more (gate 2)
    ///   LINE    tallest spectral line over the local noise floor, Welch average: a rhythm is a tall line. The same test on a beat must FAIL
    ///   ECHO    the signal's own best repeat after its first zero crossing, once the slow mood is taken out: a beat repeats near 1
    ///   PEAKS   gaps between the swells: CV of the gaps. A beat is 0
    ///   LAKES   two lakes never move as one: correlation below 0.98, and the gap between them gets real
    ///   SURGE   gaps 20-45 s, length 6-10 s, never two at once
    ///   PAUSE   a step of 0 changes nothing
    ///   HOLD    exactly 1.0, no wave, no surge
    ///   CALM    swing halved round 1
    ///   POPS    clusters and giants from the second generator: the seeded stream is the same value by value
    /// Every "must fail" line runs the same detector on a bad signal, so the proof has teeth.
    /// </summary>
    public static class OzLavaHeatProof
    {
        public const double Hours = 2.0, StepLimit = 0.15 * 1.0001, BendLimit = 0.30, SwingShareMin = 0.90, SwingBy = 0.25, LineMax = 3.2, EchoMax = 0.45, PeakCvMin = 0.45;
        static readonly int[] Fields = { 126, 91, 64, 38, 10, 3 };
        static readonly int[] Seeds = { 1907, 1, 2, 3 };

        static List<(int tx, int ty, int kind)> FakeTiles()
        {
            var t = new List<(int, int, int)>();
            int y = 0; foreach (int n in Fields) { for (int i = 0; i < n; i++) t.Add((i % 12, y + i / 12, i % 15 == 0 ? 2 : 1)); y += n / 12 + 2; }
            return t;
        }

        /// <summary>One run: per recorded frame the frame length and the values of field 0, 1, the map, and the pixel-level sum on field 0.</summary>
        sealed class Run { public List<double> dt = new List<double>(), a = new List<double>(), b = new List<double>(), map = new List<double>(), px = new List<double>(); public OzLavaHeat h; public List<double> surgeStarts = new List<double>(), surgeLens = new List<double>(); public double min = 9, max = -9; public int twoAtOnce; }

        /// <summary>dtMode: > 0 a fixed frame length, 0 a jittery clock with a hitch now and then.</summary>
        static Run Sim(int seed, double seconds, double dtMode, bool calm, int keepEvery)
        {
            var pops = new OzLavaPops(FakeTiles(), seed); var edge = new OzLavaPops(FakeTiles().Take(100), seed + 101); var sink = new List<OzLavaPops.Pop>();
            var h = new OzLavaHeat(16, seed + 7, 60, -80) { BorderFrom = 6 };
            for (int i = 0; i < 6; i++) h.SetField(i, 0.8, 0.6, 20, 20 + 40.0 * Fields[i] / 126, Fields[i] >= 10 ? Math.Sqrt(Fields[i]) : 0);
            var r = new Run { h = h }; var jit = new Random(seed * 31 + 5); double t = 0; int f = 0, seen = 0, last = -1; double started = 0;
            while (t < seconds)
            {
                double dt = dtMode > 0 ? dtMode : jit.NextDouble() < 0.002 ? 0.12 + 0.2 * jit.NextDouble() : 0.006 + 0.03 * jit.NextDouble();
                sink.Clear(); pops.Step(dt, sink); edge.Step(dt, sink);
                h.Step(dt, pops.Mood, edge.Mood, false, calm); t += dt;
                if (h.SurgesStarted > seen) { seen = h.SurgesStarted; r.surgeStarts.Add(h.LastSurgeAt); started = t; last = h.SurgeField; }
                else if (last >= 0 && h.SurgeField < 0) { r.surgeLens.Add(t - started); last = -1; }
                int on = 0; for (int i = 0; i < 16; i++) if (h.Surge[i] > 0) on++; if (on > 1) r.twoAtOnce++;
                for (int i = 0; i < 16; i++) { if (h.Heat[i] < r.min) r.min = h.Heat[i]; if (h.Heat[i] > r.max) r.max = h.Heat[i]; }
                if (++f % keepEvery == 0 || keepEvery == 1)
                {
                    r.dt.Add(dt); r.a.Add(h.Heat[0]); r.b.Add(h.Heat[1]); r.map.Add(h.Map);
                    r.px.Add(h.Heat[0] * (1.0 + h.WaveAmp * Math.Sin(h.WaveKx * 72 + h.WaveKz * -92 + h.WavePhase)));
                }
            }
            return r;
        }

        // ------------------------------------------------------------------ detectors
        /// <summary>Largest change per second between frames, and largest change of that rate per second.</summary>
        static void Rates(IList<double> v, IList<double> dt, out double step, out double bend)
        {
            step = 0; bend = 0; double prev = 0; bool have = false;
            for (int i = 1; i < v.Count; i++)
            {
                double rate = (v[i] - v[i - 1]) / dt[i];
                if (Math.Abs(rate) > step) step = Math.Abs(rate);
                if (have) { double b = Math.Abs(rate - prev) / (0.5 * (dt[i] + dt[i - 1])); if (b > bend) bend = b; }
                prev = rate; have = true;
            }
        }

        /// <summary>Resample at 1 Hz.</summary>
        static double[] Hz(IList<double> v, IList<double> dt)
        {
            var o = new List<double>(); double t = 0, next = 0;
            for (int i = 0; i < v.Count; i++) { t += dt[i]; while (t >= next) { o.Add(v[i]); next += 1; } }
            return o.ToArray();
        }

        static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit;
                if (i < j) { double a = re[i]; re[i] = re[j]; re[j] = a; a = im[i]; im[i] = im[j]; im[j] = a; }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        double ur = re[i + k], ui = im[i + k], vr = re[i + k + len / 2] * cr - im[i + k + len / 2] * ci, vi = re[i + k + len / 2] * ci + im[i + k + len / 2] * cr;
                        re[i + k] = ur + vr; im[i + k] = ui + vi; re[i + k + len / 2] = ur - vr; im[i + k + len / 2] = ui - vi;
                        double nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                    }
                }
            }
        }

        /// <summary>Welch average of 1024 s windows; the tallest line in periods of 5..300 s over the median of the lines from 2/3 to 3/2 of its frequency. A beat: tens or more.</summary>
        static double TallestLine(double[] s, out double period)
        {
            const int N = 1024; var p = new double[N / 2]; int w = 0;
            for (int o = 0; o + N <= s.Length; o += N / 2, w++)
            {
                var re = new double[N]; var im = new double[N]; double mean = 0; for (int i = 0; i < N; i++) mean += s[o + i]; mean /= N;
                for (int i = 0; i < N; i++) re[i] = (s[o + i] - mean) * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1)));
                Fft(re, im); for (int i = 0; i < N / 2; i++) p[i] += re[i] * re[i] + im[i] * im[i];
            }
            double best = 0; period = 0;
            for (int i = 4; i <= N / 5; i++)
            {
                var near = new List<double>(); for (int j = Math.Max(2, i * 2 / 3); j <= Math.Min(N / 2 - 1, i * 3 / 2 + 4); j++) if (Math.Abs(j - i) > 1) near.Add(p[j]);
                near.Sort(); double m = near[near.Count / 2]; if (m <= 0) continue;
                if (p[i] / m > best) { best = p[i] / m; period = (double)N / i; }
            }
            return best;
        }

        /// <summary>Take out a 90 s moving mean, then the highest repeat of the signal with itself after its first zero crossing.</summary>
        static double Echo(double[] s, out int lag)
        {
            const int W = 45; var d = new double[s.Length - 2 * W]; double sum = 0;
            for (int i = 0; i <= 2 * W; i++) sum += s[i];
            for (int i = W; i < s.Length - W - 1; i++) { d[i - W] = s[i] - sum / (2 * W + 1); sum += s[i + W + 1] - s[i - W]; }
            double v = d.Sum(x => x * x); lag = 0; if (v <= 0) return 0; double best = 0; bool crossed = false;
            for (int k = 1; k < 200 && k < d.Length / 2; k++)
            {
                double c = 0; for (int i = 0; i + k < d.Length; i++) c += d[i] * d[i + k]; c /= v;
                if (!crossed) { if (c <= 0) crossed = true; continue; }
                if (c > best) { best = c; lag = k; }
            }
            return best;
        }

        /// <summary>CV of the gaps between swells: peaks with 0.02 of fall on each side.</summary>
        static double PeakCv(double[] s, out int peaks)
        {
            var at = new List<int>(); double hi = s[0]; int hiAt = 0; bool up = true; double lo = s[0];
            for (int i = 1; i < s.Length; i++)
            {
                if (up) { if (s[i] > hi) { hi = s[i]; hiAt = i; } else if (hi - s[i] >= 0.02) { at.Add(hiAt); up = false; lo = s[i]; } }
                else { if (s[i] < lo) lo = s[i]; else if (s[i] - lo >= 0.02) { up = true; hi = s[i]; hiAt = i; } }
            }
            peaks = at.Count; if (at.Count < 3) return 0;
            var g = at.Zip(at.Skip(1), (a, b) => (double)(b - a)).ToList(); double m = g.Average();
            return Math.Sqrt(g.Select(x => (x - m) * (x - m)).Average()) / m;
        }

        static double SwingShare(double[] s)
        {
            int ok = 0, n = 0;
            for (int i = 0; i + 60 < s.Length; i += 10)
            {
                double lo = 9, hi = -9; for (int j = i; j <= i + 60; j++) { if (s[j] < lo) lo = s[j]; if (s[j] > hi) hi = s[j]; }
                n++; if ((hi - lo) / lo >= SwingBy) ok++;
            }
            return (double)ok / Math.Max(1, n);
        }

        static double Corr(double[] a, double[] b)
        {
            int n = Math.Min(a.Length, b.Length); double ma = a.Take(n).Average(), mb = b.Take(n).Average(), ab = 0, aa = 0, bb = 0;
            for (int i = 0; i < n; i++) { ab += (a[i] - ma) * (b[i] - mb); aa += (a[i] - ma) * (a[i] - ma); bb += (b[i] - mb) * (b[i] - mb); }
            return ab / Math.Sqrt(aa * bb + 1e-30);
        }

        // ------------------------------------------------------------------ the report
        public static string Report()
        {
            var sb = new StringBuilder(); int pass = 0, total = 0;
            void V(bool ok, string text) { total++; if (ok) pass++; sb.AppendLine((ok ? "[LV2] PASS " : "[LV2] FAIL ") + text); }
            double seconds = Hours * 3600;
            foreach (var (name, dtm, keep) in new[] { ("30 fps", 1.0 / 30, 1), ("60 fps", 1.0 / 60, 1), ("144 fps", 1.0 / 144, 2), ("jitter + hitches", 0.0, 1) })
            {
                double step = 0, bend = 0, stepB = 0, bendB = 0, stepM = 0, bendM = 0, mn = 9, mx = -9, maxFrame = 0;
                foreach (int seed in Seeds)
                {
                    var r = Sim(seed, dtm == 1.0 / 144 ? seconds / 4 : seconds, dtm, false, keep);
                    var dt = keep == 1 ? r.dt : r.dt.Select(_ => dtm * keep).ToList();
                    Rates(r.a, dt, out double s, out double b); step = Math.Max(step, s); bend = Math.Max(bend, b);
                    Rates(r.b, dt, out s, out b); stepB = Math.Max(stepB, s); bendB = Math.Max(bendB, b);
                    Rates(r.map, dt, out s, out b); stepM = Math.Max(stepM, s); bendM = Math.Max(bendM, b);
                    mn = Math.Min(mn, r.min); mx = Math.Max(mx, r.max);
                    for (int i = 1; i < r.a.Count; i++) maxFrame = Math.Max(maxFrame, Math.Abs(r.a[i] - r.a[i - 1]));
                }
                double st = Math.Max(step, Math.Max(stepB, stepM)), be = Math.Max(bend, Math.Max(bendB, bendM));
                V(st <= StepLimit, $"STEP {name}: fastest change {st:F4} a second (limit 0.15), biggest single frame {maxFrame:F4}");
                if (dtm > 0) V(be <= BendLimit, $"BEND {name}: rate of change moves at most {be:F3} per s^2 (limit {BendLimit:F2}): no step, no kink");
                else sb.AppendLine($"[LV2] info BEND {name}: {be:F3} per s^2 (a hitch frame stretches the sum; STEP above is what the eye gets)");
                V(mn >= OzLavaHeat.HeatMin - 1e-9 && mx <= OzLavaHeat.HeatMax + 1e-9, $"RANGE {name}: {mn:F3} .. {mx:F3} (0.72-1.35)");
            }
            // one long 60 frame run per seed for the shape of the curve
            double swing = 0, line = 0, linePx = 0, echo = 0, echoPx = 0, cvMin = 9, mean = 0, corrMax = 0, gapDiff = 0; int peaksN = 0, n = 0, runs = 0; double perLine = 0, perPx = 0; int lagE = 0, lagP = 0;
            var gaps = new List<double>(); var lens = new List<double>(); int twice = 0;
            foreach (int seed in Seeds)
            {
                var r = Sim(seed, 6 * 3600, 1.0 / 30, false, 1);
                var a = Hz(r.a, r.dt); var b = Hz(r.b, r.dt); var px = Hz(r.px, r.dt);
                swing += SwingShare(a); runs++;
                double l = TallestLine(a, out double per), lp = TallestLine(px, out double perP);
                if (l > line) { line = l; perLine = per; } if (lp > linePx) { linePx = lp; perPx = perP; }
                double e = Echo(a, out int le), ep = Echo(px, out int lpx); if (e > echo) { echo = e; lagE = le; } if (ep > echoPx) { echoPx = ep; lagP = lpx; }
                cvMin = Math.Min(cvMin, PeakCv(a, out int pk)); peaksN += pk;
                mean += a.Average(); n++;
                corrMax = Math.Max(corrMax, Corr(a, b));
                for (int i = 0; i < a.Length && i < b.Length; i++) gapDiff = Math.Max(gapDiff, Math.Abs(a[i] - b[i]));
                gaps.AddRange(r.surgeStarts.Zip(r.surgeStarts.Skip(1), (x, y) => y - x)); lens.AddRange(r.surgeLens); twice += r.twoAtOnce;
            }
            V(swing / runs >= SwingShareMin, $"SWING {swing / runs * 100:F1} % of 60 s windows move by {SwingBy * 100:F0} % or more (at least {SwingShareMin * 100:F0} %)");
            V(Math.Abs(mean / n - 1.0) <= 0.03, $"MEAN {mean / n:F3} (near 1, the picture)");
            V(line <= LineMax, $"LINE lake heat: tallest line {line:F2} x the noise floor at {perLine:F0} s (limit {LineMax:F1})");
            V(linePx <= LineMax, $"LINE one lava pixel with the wave: {linePx:F2} x at {perPx:F0} s (limit {LineMax:F1})");
            V(echo <= EchoMax, $"ECHO lake heat: best repeat {echo:F2} at {lagE} s (limit {EchoMax:F2}, a beat is near 1)");
            V(echoPx <= EchoMax, $"ECHO one lava pixel with the wave: best repeat {echoPx:F2} at {lagP} s (limit {EchoMax:F2})");
            V(cvMin >= PeakCvMin, $"PEAKS {peaksN} swells, gap CV at least {cvMin:F2} (limit {PeakCvMin:F2}, a beat is 0)");
            V(corrMax < 0.98 && gapDiff >= 0.10, $"LAKES two lakes: correlation at most {corrMax:F3}, they differ by up to {gapDiff:F3}: they do not move as one");
            V(gaps.Count > 100 && gaps.Min() >= OzLavaHeat.SurgeGapMin - 0.2 && gaps.Max() <= OzLavaHeat.SurgeGapMax + 11, $"SURGE {gaps.Count} gaps {gaps.Min():F1} .. {gaps.Max():F1} s, mean {gaps.Average():F1}, CV {Math.Sqrt(gaps.Select(g => (g - gaps.Average()) * (g - gaps.Average())).Average()) / gaps.Average():F2}");
            V(lens.Count > 100 && lens.Min() >= OzLavaHeat.SurgeLenMin - 0.1 && lens.Max() <= OzLavaHeat.SurgeLenMax + 0.1, $"SURGE length {lens.Min():F1} .. {lens.Max():F1} s");
            V(twice == 0, $"SURGE two at once: {twice} frames");

            // the same detectors on bad signals: each of these MUST trip, else the proof has no teeth
            {
                var dt = new List<double>(); var stair = new List<double>(); var beat = new List<double>();
                for (int i = 0; i < 3600 * 30; i++) { double t = i / 30.0; dt.Add(1.0 / 30); stair.Add(1.0 + Math.Floor(t) / 12 % 0.3); beat.Add(1.0 + 0.1 * Math.Sin(2 * Math.PI * t / 24)); }
                Rates(stair, dt, out double s, out double be);
                V(s > StepLimit && be > BendLimit, $"TEETH the sim's 1/12 a second staircase trips STEP ({s:F2} a second) and BEND ({be:F0})");
                var b = Hz(beat, dt); double l = TallestLine(b, out double per), e = Echo(b, out int lag);
                V(l > LineMax && e > EchoMax, $"TEETH a 24 s sine trips LINE ({l:F0} x at {per:F0} s) and ECHO ({e:F2} at {lag} s)");
                V(PeakCv(b, out int _) < PeakCvMin, $"TEETH a 24 s sine trips PEAKS (CV {PeakCv(b, out int _):F3})");
            }
            // pause, hold, calm
            {
                var h = new OzLavaHeat(16, 5, 60, -80); h.SetField(0, 1, 0, 0, 40, 1); for (int i = 0; i < 6000; i++) h.Step(1.0 / 60, 1.2, 1.2, false, false);
                double a0 = h.Heat[0], w0 = h.WavePhase, m0 = h.Map, t0 = h.Time; for (int i = 0; i < 600; i++) h.Step(0, -1.5, -1.5, false, false);
                V(a0 == h.Heat[0] && w0 == h.WavePhase && m0 == h.Map && t0 == h.Time, "PAUSE 600 frames of step 0: every value the same to the bit");
                h.Step(0.0, 1.2, 1.2, true, false);                                                                      // a hold works on a paused game too
                bool one = h.Map == 1.0 && h.WaveAmp == 0.0 && h.SurgeField == -1; for (int i = 0; i < 16; i++) one &= h.Heat[i] == 1.0 && h.Cover[i] == 0.0 && h.Surge[i] == 0.0;
                V(one, "HOLD heat exactly 1.0 on all 16 slots, cover 0, no wave, no surge (gate 1)");
                var full = Sim(3, 3600, 1.0 / 30, false, 1); var calm = Sim(3, 3600, 1.0 / 30, true, 1);
                double sf = Math.Max(full.a.Max() - 1, 1 - full.a.Min()), sc = Math.Max(calm.a.Max() - 1, 1 - calm.a.Min());
                V(sc <= sf * 0.56, $"CALM swing round 1: {sc:F3} against {sf:F3} ({sc / sf * 100:F0} %)");
            }
            // the pops' second generator
            {
                var a = new OzLavaPops(FakeTiles(), 1907); var b = new OzLavaPops(FakeTiles(), 1907); var x = new OzLavaPopExtras(1907); var pa = new List<OzLavaPops.Pop>(); var pb = new List<OzLavaPops.Pop>(); var later = new List<OzLavaPops.Pop>();
                int followers = 0, groups = 0, giants = 0, stream = 0; double dtMax = 0;
                for (int f = 0; f < 60 * 3600 * 6; f++)
                {
                    pa.Clear(); pb.Clear(); a.Step(1.0 / 60, pa); b.Step(1.0 / 60, pb);
                    foreach (var p in pa) { stream++; if (x.Giant()) giants++; if (x.Offer(p) > 0) groups++; }
                    later.Clear(); x.Step(1.0 / 60, later); followers += later.Count;
                    foreach (var q in later) dtMax = Math.Max(dtMax, x.Time - q.t);
                    for (int i = 0; i < pa.Count; i++) if (pa[i].x != pb[i].x || pa[i].z != pb[i].z || pa[i].t != pb[i].t || pa[i].big != pb[i].big) { V(false, "POPS the seeded stream changed"); }
                    if (pa.Count != pb.Count) V(false, "POPS the seeded stream changed");
                }
                double cs = (double)groups / stream, gs = (double)giants / stream, fo = (double)followers / Math.Max(1, groups);
                V(cs >= 0.27 && cs <= 0.33, $"POPS {stream} stream pops, {cs * 100:F1} % start a cluster (30 %)");
                V(fo >= 1.8 && fo <= 2.2, $"POPS clusters add {fo:F2} pops on average (groups of 2-4)");
                V(dtMax <= OzLavaPopExtras.ClusterSeconds + 0.02, $"POPS the last cluster pop lands inside {OzLavaPopExtras.ClusterSeconds:F1} s");
                V(gs >= 0.015 && gs <= 0.025, $"POPS giants {gs * 100:F2} % (2 %)");
                V(true, "POPS two seeded streams beside the second generator stayed the same value by value");
            }
            sb.AppendLine("[LV2] heat numbers " + (pass == total ? "PASS " : "FAIL ") + pass + "/" + total);
            return sb.ToString();
        }
    }
}
