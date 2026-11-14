using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUeDraw.cs): the dash pattern of a world mark (dash on, gap off, again) walked along its screen
    /// path one segment at a time, the pattern's place carried from segment to segment. The place is kept inside one period (dash + gap)
    /// and snapped onto a dash's or a gap's end, so every step reaches the next end or the segment's end: a part of L px takes at most
    /// 2 x (L / min(dash, gap) + 2) steps at any pixel scale. The walk it replaces carried an ever growing place through Repeat() in
    /// float and, at a pixel scale that makes the dash a fraction (0.85 px per u on a 1366 x 768 screen), landed a hair before a dash's
    /// end, where its step no longer moved anything: one dashed ring took seconds, a fight with empty vehicles and a hovered unit took
    /// minutes per frame. Pure (no engine): the kit's frame-cost model runs this same code off the engine, the counted check (menu 2)
    /// runs SelfCheck.
    /// </summary>
    public static class OzUeDash
    {
        /// <summary>A pattern place this close to a dash's or a gap's end is at that end (px).</summary>
        public const double Snap = 1e-4;
        /// <summary>Drawn parts shorter than this are left out (px), as before.</summary>
        public const double MinPiece = 0.05;
        /// <summary>Dash and gap lengths under this draw the stroke solid (px), as before.</summary>
        public const double MinDash = 0.01;

        /// <summary>The pattern place `phase` (px into dash + gap) moved on by `px` without drawing (a part of a stroke off the screen).</summary>
        public static double Advance(double phase, double px, double dash, double gap)
        {
            double period = dash + gap;
            if (!(period > 0.0) || double.IsInfinity(period)) return 0.0;
            if (double.IsNaN(phase) || double.IsInfinity(phase)) phase = 0.0;
            if (double.IsNaN(px) || double.IsInfinity(px)) px = 0.0;
            double p = (phase + px) % period;
            if (p < 0.0) p += period;
            return p >= period - Snap ? 0.0 : p;
        }

        /// <summary>The most steps a part of `px` takes (the counted checks and the frame-cost check compare with it).</summary>
        public static int Bound(double px, double dash, double gap)
        {
            if (!(dash > MinDash) || !(gap > MinDash) || !(px > 0.0)) return 1;
            double n = 2.0 * (px / Math.Min(dash, gap) + 2.0);
            return n > int.MaxValue / 4 ? int.MaxValue / 4 : (int)Math.Ceiling(n);
        }

        /// <summary>Walks the part [from, to] px of one segment with the pattern place `phase` (updated) and appends its drawn parts as
        /// (start, end) px pairs to `pieces`. `budget` = the steps still allowed this frame (counted down): when it runs out the rest of
        /// the part is one solid piece, so no stroke can ever hold the frame. Returns the steps taken.</summary>
        public static int Walk(double from, double to, double dash, double gap, ref double phase, List<float> pieces, ref int budget)
        {
            if (pieces == null || !(to - from > Snap)) return 0;
            double period = dash + gap;
            if (!(dash > MinDash) || !(gap > MinDash) || double.IsInfinity(period) || double.IsNaN(from) || double.IsInfinity(to))
            {
                pieces.Add((float)from); pieces.Add((float)to);
                return 1;
            }
            if (!(phase >= 0.0) || phase >= period) phase = Advance(0.0, phase, dash, gap);
            double t = from;
            int steps = 0;
            while (to - t > Snap)
            {
                if (budget <= 0)
                {
                    pieces.Add((float)t); pieces.Add((float)to);                                       // out of steps this frame: the rest solid
                    phase = Advance(phase, to - t, dash, gap);
                    return steps;
                }
                budget--; steps++;
                bool on = phase < dash;
                double run = on ? dash - phase : period - phase;
                if (run <= Snap) { phase = on ? dash : 0.0; continue; }                                 // at an end already: the next part starts
                double step = Math.Min(run, to - t);
                if (on && step > MinPiece) { pieces.Add((float)t); pieces.Add((float)(t + step)); }
                t += step; phase += step;
                if (phase >= period - Snap) phase = 0.0;                                                // a gap ended: the next dash
                else if (on && phase >= dash - Snap) phase = dash;                                      // a dash ended: its gap
            }
            return steps;
        }

        /// <summary>Counted check (menu 2 in edit mode, the Play proof): the walk at the pixel scale (screen px per canvas u) of the canvas
        /// sizes of spec 2.1 (1920 x 1080; 1366 x 768 and 1536 x 730 with the 0.85 floor; 1600 x 900; 2560 x 1440; 2560 x 1600) and of
        /// UI SCALE 160 % on 1366 x 768, 90 % and 135 % on 1920 x 1080, with the marks' dash patterns (6 / 5, 4 / 3, 3 / 3, 3 / 4, 5 / 4 u)
        /// over rings of 24 .. 256 segments and 8 .. 4000 px: every part within its step bound, the drawn length the pattern's share of
        /// the ring (within one dash and the left-out bits), the place always inside one period. One line per pixel scale (8); returns
        /// the number of FAIL lines.</summary>
        public static int SelfCheck(StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            var scales = new (double k, string at)[]
            {
                (1.0, "1920x1080"), (0.85, "1366x768 and 1536x730, the 0.85 pixel floor"), (900.0 / 1080.0, "1600x900"), (1440.0 / 1080.0, "2560x1440"),
                (1600.0 / 1080.0, OzProofWords.Screen1610), (0.85 * 1.6, "1366x768 at UI SCALE 160 %"), (0.9, "1920x1080 at UI SCALE 90 %"), (1.35, "1920x1080 at UI SCALE 135 %"),
            };
            var patterns = new (double dash, double gap)[] { (6, 5), (4, 3), (3, 3), (3, 4), (5, 4) };
            var pieces = new List<float>(256);
            int fails = 0;
            foreach (var s in scales)
            {
                long steps = 0, worst = 0; int parts = 0, over = 0, lengthBad = 0, placeBad = 0;
                float k = (float)s.k;
                foreach (var p in patterns)
                {
                    double dash = (float)p.dash * k, gap = (float)p.gap * k, period = dash + gap;                    // the pixel lengths as the draw code makes them (float)
                    foreach (double ring in new[] { 8.0, 40.0, 157.0, 611.0, 1999.0, 4000.0 })
                        foreach (int segs in new[] { 24, 97, 256 })
                        {
                            double phase = 0.0, seg = ring / segs, drawn = 0.0; int budget = int.MaxValue;
                            for (int i = 0; i < segs; i++)
                            {
                                pieces.Clear();
                                int n = Walk(0.0, seg, dash, gap, ref phase, pieces, ref budget);
                                steps += n; parts++;
                                if (n > Bound(seg, dash, gap)) over++;
                                worst = Math.Max(worst, n);
                                for (int j = 0; j + 1 < pieces.Count; j += 2) drawn += pieces[j + 1] - pieces[j];
                                if (!(phase >= 0.0 && phase < period)) placeBad++;
                            }
                            double want = ring * dash / period;
                            if (Math.Abs(drawn - want) > dash + 2.0 * segs * MinPiece + 1e-3) lengthBad++;            // a dash cut at a segment's end may leave out a bit < MinPiece on each side
                        }
                }
                bool ok = over == 0 && lengthBad == 0 && placeBad == 0;
                if (!ok) fails++;
                log.AppendLine((ok ? "PASS " : "FAIL ") + "P4 dash walk at " + s.k.ToString("0.####", CultureInfo.InvariantCulture) + " px per u (" + s.at + "): " + parts
                    + " segment parts, every one within its step bound (" + over + " over), the drawn share of every ring right (" + lengthBad + " off), the place inside one period ("
                    + placeBad + " outside); most steps of one part " + worst + ", " + steps + " steps in all");
            }
            return fails;
        }
    }
}
