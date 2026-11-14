using System;
using System.Globalization;
using System.Text;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUePlace.cs Words): where the drop preview's words go (the line "GRUNT  x5  (15 robots)" and,
    /// while a tile is armed, the hint under it, each on its dark plate), in canvas units from the canvas bottom. The words keep clear of
    /// every see-through model and its member ring (lo .. hi: the preview's lowest and highest point on the screen) and also of the
    /// squad's range ring (dashed 6 / 5, radius = the unit's range: its polyline on the screen) and that ring's metres label, and of the
    /// blast ring of a unit whose shot bursts (solid INK 30 %, radius = its blast: part of the preview itself, so lo .. hi reach round it
    /// and "under / over the models" is under / over it too). Tried in order, the first that stays inside the free field (over the play
    /// bar and the dock, under the top bar and the arm bar) and touches neither the range ring's line, its label nor the blast ring:
    ///   1 under the models (inside a range ring that is larger than the preview),  2 under the whole preview, its range ring included,
    ///   3 over the models,  4 over the whole preview, its range ring and label included.
    /// None of them clear: under the models, over them when the play bar or the dock is in the way (the rule before this check).
    /// Pure (no engine): the counted check (menu 2) runs SelfCheck, the kit's words model runs this same code off the engine.
    /// </summary>
    public static class OzUeWords
    {
        /// <summary>Canvas u: words to the preview, line plate to hint plate, the clear margin round the plates.</summary>
        public const float Gap = 8f, Between = 2f, Margin = 2f;

        /// <summary>A placement: the line plate's bottom (the hint plate sits Between under it), the words' lowest and highest edge, the
        /// rule taken (1 .. 4 as the class says, 0 = none was clear), over the models or under them, clear of the range ring, its label
        /// and the blast ring, inside the free field.</summary>
        public struct Plan { public float lineBottom, low, top; public int choice; public bool over, clear, fits; }

        public static readonly string[] ChoiceWords =
        {
            "no clear place: the rule before (under the models, over them at the play bar)", "under the models",
            "under the whole preview, its range ring included", "over the models", "over the whole preview, its range ring and its label included",
        };

        /// <summary>The words' place without a blast ring (the other Place with none).</summary>
        public static Plan Place(float lo, float hi, float[] rx, float[] ry, int rn, float lx0, float ly0, float lx1, float ly1,
                                 float cx, float halfW, float ph, bool hint, float floorY, float topY)
            => Place(lo, hi, rx, ry, rn, null, null, 0, lx0, ly0, lx1, ly1, cx, halfW, ph, hint, floorY, topY);

        /// <summary>The words' place. lo / hi: the preview's lowest / highest point (canvas y); rx / ry (rn points, a closed polyline): the
        /// range ring on the canvas (rn 0: no range ring); bx / by (bn points, a closed polyline): the blast ring on the canvas (bn 0:
        /// none); lx0 .. lx1 x ly0 .. ly1: the range ring's metres label (lx1 &lt;= lx0: none); cx: the words' centre x; halfW: half the
        /// wider plate; ph: one plate's height; hint: the second plate under the line; floorY / topY: the free field's lowest and highest
        /// y for the words.</summary>
        public static Plan Place(float lo, float hi, float[] rx, float[] ry, int rn, float[] bx, float[] by, int bn, float lx0, float ly0, float lx1, float ly1,
                                 float cx, float halfW, float ph, bool hint, float floorY, float topY)
        {
            if (rx == null || ry == null) rn = 0;
            rn = Math.Max(0, Math.Min(rn, Math.Min(rx != null ? rx.Length : 0, ry != null ? ry.Length : 0)));
            if (bx == null || by == null) bn = 0;
            bn = Math.Max(0, Math.Min(bn, Math.Min(bx != null ? bx.Length : 0, by != null ? by.Length : 0)));
            for (int k = 0; k < bn; k++) { if (by[k] < lo) lo = by[k]; if (by[k] > hi) hi = by[k]; }          // the blast ring is the preview's own mark
            float rlo = float.MaxValue, rhi = float.MinValue;
            for (int k = 0; k < rn; k++) { if (ry[k] < rlo) rlo = ry[k]; if (ry[k] > rhi) rhi = ry[k]; }
            bool label = lx1 > lx0 && ly1 > ly0;
            float under = hint ? Between + ph : 0f, overUp = hint ? ph + Between : 0f;
            float c1 = lo - Gap - ph;
            float c2 = rn > 1 ? Math.Min(lo, rlo) - Gap - ph : c1;
            float c3 = hi + Gap + overUp;
            float high = hi; if (rn > 1) high = Math.Max(high, rhi); if (label) high = Math.Max(high, ly1);
            float c4 = high + Gap + overUp;
            for (int i = 0; i < 4; i++)
            {
                if (i == 1 && rn <= 1) continue;                                                     // no range ring: 2 and 4 are 1 and 3
                if (i == 3 && rn <= 1 && !label) continue;
                float cand = i == 0 ? c1 : i == 1 ? c2 : i == 2 ? c3 : c4;                          // no array: this runs every frame of a preview
                var p = Make(cand, ph, under, cx, halfW, rx, ry, rn, bx, by, bn, label, lx0, ly0, lx1, ly1, floorY, topY);
                p.choice = i + 1; p.over = i >= 2;
                if (p.fits && p.clear) return p;
            }
            float lb = c1; bool over = false;
            if (lb - under < floorY) { lb = c3; over = true; }                                          // the rule before: over the models at the play bar
            var f = Make(lb, ph, under, cx, halfW, rx, ry, rn, bx, by, bn, label, lx0, ly0, lx1, ly1, floorY, topY);
            f.choice = 0; f.over = over;
            return f;
        }

        static Plan Make(float lb, float ph, float under, float cx, float halfW, float[] rx, float[] ry, int rn, float[] bx, float[] by, int bn, bool label,
                         float lx0, float ly0, float lx1, float ly1, float floorY, float topY)
        {
            var p = new Plan { lineBottom = lb, low = lb - under, top = lb + ph };
            p.fits = p.low >= floorY - 0.01f && p.top <= topY + 0.01f;
            float x0 = cx - halfW - Margin, x1 = cx + halfW + Margin, y0 = p.low - Margin, y1 = p.top + Margin;
            bool hit = SegmentsHit(rx, ry, rn, x0, y0, x1, y1) || SegmentsHit(bx, by, bn, x0, y0, x1, y1);
            if (!hit && label) hit = lx0 < x1 && x0 < lx1 && ly0 < y1 && y0 < ly1;
            p.clear = !hit;
            return p;
        }

        /// <summary>True when any segment of the closed polyline (n points) crosses or lies inside the rectangle x0 .. x1 x y0 .. y1.</summary>
        public static bool SegmentsHit(float[] xs, float[] ys, int n, float x0, float y0, float x1, float y1)
        {
            if (xs == null || ys == null || n <= 0) return false;
            if (n == 1) return xs[0] >= x0 && xs[0] <= x1 && ys[0] >= y0 && ys[0] <= y1;
            for (int k = 0; k < n; k++)
            {
                int j = (k + 1) % n;
                if (SegmentHits(xs[k], ys[k], xs[j], ys[j], x0, y0, x1, y1)) return true;
            }
            return false;
        }

        /// <summary>The segment a .. b crosses or lies inside the rectangle (clipped edge by edge).</summary>
        public static bool SegmentHits(float ax, float ay, float bx, float by, float x0, float y0, float x1, float y1)
        {
            float t0 = 0f, t1 = 1f, dx = bx - ax, dy = by - ay;
            return Clip(-dx, ax - x0, ref t0, ref t1) && Clip(dx, x1 - ax, ref t0, ref t1) && Clip(-dy, ay - y0, ref t0, ref t1) && Clip(dy, y1 - ay, ref t0, ref t1);
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Math.Abs(p) < 1e-9f) return q >= 0f;
            float r = q / p;
            if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }

        /// <summary>An ellipse on the canvas (a range ring seen from the RTS camera), n points.</summary>
        public static int Ellipse(float cx, float cy, float a, float b, int n, float[] xs, float[] ys)
        {
            n = Math.Min(n, Math.Min(xs.Length, ys.Length));
            for (int k = 0; k < n; k++) { double t = k * 2.0 * Math.PI / n; xs[k] = cx + (float)(Math.Cos(t) * a); ys[k] = cy + (float)(Math.Sin(t) * b); }
            return n;
        }

        // ------------------------------------------------------------------------------------------------ counted check
        /// <summary>Menu 2 (and the words model off the engine): the place rule on seven previews drawn from the Play proof's pictures:
        /// a long-range squad (its ring far round the models), a short-range squad (its ring just under the models), a preview near the
        /// play bar, one whose ring is just over the models (its metres label too), a drop with no range ring, one with no clear place
        /// at all (the rule before), and a light tank whose blast ring reaches past its models. One PASS / FAIL line each; returns the
        /// FAIL lines.</summary>
        public static int SelfCheck(StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            int fails = 0;
            var xs = new float[96]; var ys = new float[96];
            const float ph = 22f, halfW = 160f, cx = 957f, floor = 134f, top = 930f;
            // 1 the 1920 x 1080 picture: a grunt x5 (15 m ring 530 x 400 u round the preview), the models 586 .. 856 u
            int n = Ellipse(cx, 640f, 530f, 400f, 96, xs, ys);
            fails += Case(log, "a long-range squad x5 (its ring far round the models)", Place(586f, 856f, xs, ys, n, 937f, 1043f, 977f, 1061f, cx, halfW, ph, true, floor, top), 1);
            // 2 a short-range squad: its ring (130 x 100 u) reaches 40 u under the models: under the ring
            n = Ellipse(cx, 700f, 130f, 100f, 96, xs, ys);
            fails += Case(log, "a short-range squad (its ring 40 u under the models)", Place(640f, 760f, xs, ys, n, 0f, 0f, -1f, -1f, cx, halfW, ph, true, floor, top), 2);
            // 3 near the play bar: no room under the models, the big ring round them: over the models
            n = Ellipse(cx, 235f, 530f, 400f, 96, xs, ys);
            fails += Case(log, "a preview by the play bar (no room under it)", Place(170f, 300f, xs, ys, n, 0f, 0f, -1f, -1f, cx, halfW, ph, true, floor, top), 3);
            // 4 by the play bar with a small ring just over the models and its metres label: over the ring and the label
            n = Ellipse(cx, 230f, 150f, 110f, 96, xs, ys);
            fails += Case(log, "a preview by the play bar, its ring just over the models (and its metres label)", Place(170f, 300f, xs, ys, n, 937f, 343f, 977f, 361f, cx, halfW, ph, true, floor, top), 4);
            // 5 an empty vehicle (no range ring): under the models
            fails += Case(log, "a drop with no range ring (an empty vehicle)", Place(500f, 640f, xs, ys, 0, 0f, 0f, -1f, -1f, cx, halfW, ph, true, floor, top), 1);
            // 6 no clear place (a very small free field): the rule before, and it says so
            n = Ellipse(cx, 220f, 140f, 120f, 96, xs, ys);
            var p6 = Place(150f, 290f, xs, ys, n, 0f, 0f, -1f, -1f, cx, halfW, ph, true, floor, 300f);
            bool ok6 = p6.choice == 0 && p6.over && Math.Abs(p6.lineBottom - (290f + Gap + ph + Between)) < 0.01f;
            fails += Line(log, ok6, "P4 drop words: no clear place (free field 134 .. 300 u, the ring round the models): the rule before, over the models (line plate at "
                          + F(p6.lineBottom) + " u), reported as not clear");
            // 7 a light tank (the 1920 x 1080 picture): its 5 m blast ring (370 x 284 u) round the tank, inside the 15 m range ring: the
            // words under the models AND the blast ring, inside the range ring; without the blast ring they would lie on it
            n = Ellipse(942f, 628f, 545f, 412f, 96, xs, ys);
            var bx = new float[96]; var by = new float[96];
            int bn = Ellipse(960f, 717f, 185f, 142f, 96, bx, by);
            var p7 = Place(660f, 840f, xs, ys, n, bx, by, bn, 0f, 0f, -1f, -1f, cx, halfW, ph, true, floor, top);
            var p7b = Place(660f, 840f, xs, ys, n, 0f, 0f, -1f, -1f, cx, halfW, ph, true, floor, top);
            bool onBlast = SegmentsHit(bx, by, bn, cx - halfW - Margin, p7b.low - Margin, cx + halfW + Margin, p7b.top + Margin);
            bool clear7 = p7.clear && !SegmentsHit(bx, by, bn, cx - halfW - Margin, p7.low - Margin, cx + halfW + Margin, p7.top + Margin);
            fails += Line(log, p7.choice == 1 && p7.fits && clear7 && p7.top <= 575f - Gap + 0.01f && onBlast,
                          "P4 drop words: a light tank (its blast ring round the tank, inside the range ring): " + ChoiceWords[Math.Max(0, Math.Min(4, p7.choice))]
                          + " and its blast ring (rule " + p7.choice + ", want 1), plates " + F(p7.low) + " .. " + F(p7.top) + " u, "
                          + (clear7 ? "clear of the range ring, its label and the blast ring" : "ON A RING") + ", " + (p7.fits ? "inside the free field" : "OUTSIDE the free field")
                          + " (without the blast ring they would lie on it: " + onBlast + ")");
            return fails;
        }

        static int Case(StringBuilder log, string what, Plan p, int want)
        {
            bool ok = p.choice == want && p.clear && p.fits;
            return Line(log, ok, "P4 drop words: " + what + ": " + ChoiceWords[Math.Max(0, Math.Min(4, p.choice))] + " (rule " + p.choice + ", want " + want + "), plates "
                        + F(p.low) + " .. " + F(p.top) + " u, " + (p.clear ? "clear of the range ring and its label" : "ON THE RANGE RING") + ", " + (p.fits ? "inside the free field" : "OUTSIDE the free field"));
        }

        static int Line(StringBuilder log, bool ok, string what) { log.AppendLine((ok ? "PASS " : "FAIL ") + what); return ok ? 0 : 1; }
        static string F(float v) => v.ToString("0", CultureInfo.InvariantCulture);
    }
}
