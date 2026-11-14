// OpenZ (GPL-3.0) - version 1.4: the FOG OF WAR radar circle's counted rows without Play (View/OzFowRadar.cs, View/OzFowMask.cs). Plain C#:
// the menu OpenZ/V1.4/Fog Radar: Ease Count (headless) runs it in the editor, and the kit's off-Unity harness runs the same file.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR, the radar circle without Play: openings simulated frame by frame exactly as the game runs each frame
    /// (OzFogOfWar.Update: the stamp round every StampEvery frames, which stamps the zone's flag, its linked buildings and the radar's own
    /// building sight once they are the player's; then OzFogOfWar.RadarStep: the radar's round; then the reveal's Ease step of that frame), on
    /// L02's two real radar zones (<see cref="L02"/>), from the frame before the radar is ours.
    /// Measured on two maps: the game's map (the zone's own reveal and the circle together, as on screen) and the circle alone
    /// (<see cref="Circle"/>: a map of its own that gets the circle's stamps and the same reveal steps, from its start disc). The circle's
    /// bars are on the circle alone; the zone's own reveal is the game's normal reveal of a capture (0.8 s a cell, unchanged by this kit), so
    /// the game's map is printed as information, and barred where the zone's ground was open before (rows H16).
    /// Rows H1 - H13, H16 (see <see cref="Run"/>), H15 (information); H14 (the field model against the shader's own field) is in
    /// Editor/OzV14FogRadarCheck.cs.
    /// </summary>
    public static class OzV14FogRadarCount
    {
        /// <summary>A radar's building sight (View/OzFogOfWar.cs SightPx: half its longest side, 4 tiles = 64 px, so 32, + BuildingSightPadPx
        /// 128): 160 px = 20 m.</summary>
        public const int RadarSightPx = 160;
        /// <summary>Level 17's map (110 x 128 tiles of 16 px), the largest: the cost of the whole circle and the field model's comparison.</summary>
        public const int MapW = 110 * 16, MapH = 128 * 16;
        /// <summary>The largest step a frame may make, as the task asks: 2 % of the 48 m circle, 0.96 m, on the frame as it ran (not scaled).</summary>
        public const double StepBarM = 0.02 * OzFowRadar.CirclePx / 8.0;
        /// <summary>The drawn edge: the most the cloud's cover of a texel may change, as a rate (0.1 in a 60 fps frame = 6 a second: no texel
        /// goes from full cloud to clear in under a sixth of a second; a style's bar is <see cref="CoverBarOf"/>: this, or the fog's own reveal
        /// of a radar's capture on that style where that is faster); the deepest speed dip between two faster stretches (a hesitation, m/s);
        /// the most of the drawn opening's growth in its first or its last 10 % of time (linear: 10 %); when it must be at rest (s after the
        /// radar's first round). Set on the fog's edge before its round 4 (one shared 13 m haze band); the kit's harness runs them on the three
        /// round-4 styles (row M2), each with its own sheets' ramps.</summary>
        public const double CoverStepBar = 0.10, ValleyBarMs = 2.0, EaseShareBar = 0.08, RestBarS = OzFowRadar.GrowSeconds + 0.4;
        /// <summary>Row H16b: the ground open all round to 30 m before (a radar lost while its circle was half grown, then regained).</summary>
        public const int HalfOpenPx = 240;

        /// <summary>The fog's own clock's longest step (OzFogOfWar.FowDt, View/OzFogOfWar.Render.cs: the real frame time, at most 0.25 s).</summary>
        public const float FowDtMax = 0.25f;
        /// <summary>The game's reveal step of a frame of <paramref name="dt"/> s (OzFogOfWar.Update: 255 FowDt / RevealSeconds, rounded up, 1 - 255;
        /// FowDt at most <see cref="FowDtMax"/>).</summary>
        public static int RevealStep(float dt, float revealSeconds) => Math.Min(255, Math.Max(1, (int)Math.Ceiling(255f * Math.Min(dt, FowDtMax) / revealSeconds)));

        static readonly Dictionary<string, double> captureRates = new Dictionary<string, double>();
        /// <summary>The fog's own reveal of a radar's capture on a style, as a cover rate in a 60 fps frame (rebase v2, the cover-rate bar):
        /// the radar's building sight (20 m, the circle's start disc) stamped on covered ground, as the stamp round does at every capture or
        /// rebuild of a radar, and revealed by the game's reveal step (RevealSeconds a cell) at 60 fps, measured with the same field model as
        /// the circle (<see cref="FieldMeter.CoverStep"/>); the larger of L02's two radars. The game did this before this kit too (the zone's
        /// flag and buildings only add to it). Worked out once per style.</summary>
        public static double CaptureCoverRate(Edge edge, float revealSeconds)
        {
            string key = edge.Text() + "|" + revealSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            lock (captureRates) if (captureRates.TryGetValue(key, out double known)) return known;
            double worst = 0; const float dt = 1f / 60f; int step = RevealStep(dt, revealSeconds);
            foreach (var z in L02)
            {
                var m = new OzFowMask(z.MapW, z.MapH); var f = new FieldMeter(m.W, m.H, z.Cx, z.Cy, edge);
                f.Sample(m.Shown);                                                      // covered
                m.Stamp(z.Cx, z.Cy, z.SightPx);                                         // OzFogOfWar.StampRound: the radar's building sight
                for (int i = 1; i * dt <= revealSeconds + 0.3f; i++) { m.Ease(step); f.Sample(m.Shown); worst = Math.Max(worst, f.CoverStep); }
            }
            lock (captureRates) captureRates[key] = worst;
            return worst;
        }

        /// <summary>The cover-rate bar of a style (in a 60 fps frame): <see cref="CoverStepBar"/> (0.10), or the fog's own reveal of a radar's
        /// capture on that style (<see cref="CaptureCoverRate"/>) where that is faster. So the circle never changes a texel's cover faster than
        /// 0.10 a 60 fps frame, or, on a look whose cloud edge makes every reveal faster than that (the round-4 sheets are up to 2.9 times
        /// steeper than the shared haze the 0.10 was set on), faster than the game's own reveal of that radar's capture. The other bars of
        /// the drawn opening (never down, one motion, eased, at rest) are unchanged.</summary>
        public static double CoverBarOf(Edge edge, float revealSeconds) => Math.Max(CoverStepBar, CaptureCoverRate(edge, revealSeconds));

        /// <summary>The edge of an opening measured on the explored map: the cells still closed (explored under <see cref="OzFowMask.Revealed"/>)
        /// when it is made, up to 52 m from the radar, sorted by their distance from it; with n of them half open now the opening's half-open
        /// contour lies between the n-th and the (n+1)-th nearest distance (an opening round the radar opens exactly the nearest ones), read
        /// between them from the two cells' explored values (the soft edge is linear in the distance, so this is exact to the byte; without it
        /// a centre on a cell corner, as every L02 building's, reads in steps of up to 0.17 m, because 8 cells share each distance); plus the
        /// soft edge's outer half it is the circle's radius (m), the number the code's radius is compared with. It never reads under the edge
        /// of the ground open round the radar when it is made (<see cref="BaseM"/>, OzFowRadar.OpenEdgePx), so a circle that starts past open
        /// ground is measured from there on its first frame. Ground opened elsewhere in the ring (a unit walking by, the zone's flag and
        /// buildings) only adds to it.</summary>
        public sealed class EdgeMeter
        {
            readonly int[] idx; readonly float[] dist; readonly double baseM;
            /// <summary>The soft edge from the half-open contour to its outer end (m): RampCells x 128 / 255.</summary>
            public static readonly double HalfRampM = OzFowMask.RampCells * OzFowMask.Revealed / 255.0;
            public int Cells => idx.Length;
            /// <summary>The edge of the ground open round the radar when the meter was made (m; OzFowRadar.OpenEdgePx with the same centre).</summary>
            public double BaseM => baseM;

            public EdgeMeter(OzFowMask m, int cx, int cy, int outsidePx)
            {
                double fx = cx / (double)OzFowMask.CellPx, fy = cy / (double)OzFowMask.CellPx, rout = outsidePx / (double)OzFowMask.CellPx;
                var l = new List<KeyValuePair<float, int>>(8192);
                int x0 = Math.Max(0, (int)Math.Floor(fx - rout)), x1 = Math.Min(m.W - 1, (int)Math.Floor(fx + rout));
                int y0 = Math.Max(0, (int)Math.Floor(fy - rout)), y1 = Math.Min(m.H - 1, (int)Math.Floor(fy + rout));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        double dx = x + 0.5 - fx, dy = y + 0.5 - fy, d = Math.Sqrt(dx * dx + dy * dy);
                        int i = y * m.W + x;
                        if (d <= rout && m.Target[i] < OzFowMask.Revealed) l.Add(new KeyValuePair<float, int>((float)d, i));
                    }
                l.Sort((a, b) => a.Key.CompareTo(b.Key));
                idx = new int[l.Count]; dist = new float[l.Count];
                for (int i = 0; i < l.Count; i++) { idx[i] = l[i].Value; dist[i] = l[i].Key; }
                baseM = OzFowRadar.OpenEdgePx(m, cx, cy, outsidePx) / 8.0;
            }

            /// <summary>The circle's radius (m) on this map: at least <see cref="BaseM"/>.</summary>
            public double RadiusM(byte[] map)
            {
                int n = 0;
                for (int i = 0; i < idx.Length; i++) if (map[idx[i]] >= OzFowMask.Revealed) n++;
                if (n == 0) return baseM;
                double c = dist[n - 1];
                if (n < idx.Length)
                {
                    int a = map[idx[n - 1]], b = map[idx[n]]; double g = dist[n] - dist[n - 1];
                    if (a >= OzFowMask.Revealed && a > b && g > 1e-6) c += (a - OzFowMask.Revealed) / (double)(a - b) * g;   // where the value crosses 128 between them
                }
                return Math.Max(baseM, c + HalfRampM);
            }
        }

        /// <summary>
        /// The cloud's edge as Resources/OpenZ/FX/OzFogOfWar.shader's Sheets pass draws it since the fog's round 4 (5 Nov, layered cloud
        /// sheets): four stacked sheets, each fading on its own eased ramp of the signed distance d past the hiding line (m, &gt; 0 covered):
        /// sheet k is drawn only where d &gt; D0, its cover c = smootherstep((d - D0) / max(0.5, D1 - D0)), its density faded in by
        /// smootherstep(c / max(0.05, Thin)) and scaled by AMax; the deck (the third sheet, the shader's k == 2) also closes the covered ground
        /// at any zoom, max(a, AMax smootherstep((d - D1) / 4)). The sheets lie over each other: cover = 1 - the product of (1 - a). The
        /// numbers are the style's own (View/OzFogOfWar.Looks.cs Looks[style].L; the edge mist's AMax times the planet's MistMul).
        /// Left out, as the model before round 4 left out the clouds inside the shared edge haze: the cloud picture's puffs (the shader's
        /// shape threshold, which only shapes the cover inside each sheet's ramp: each sheet's mean cover is taken as its own cover c), the
        /// slow edge wander (only ever opening, at most WanderM 2.5 m, moving with the wind, not with the reveal), the grazing-angle density
        /// (1 at the RTS camera's tilt) and the 1.4 see-through (1 = today; it only scales the cover down).</summary>
        public sealed class Edge
        {
            public struct Sheet
            {
                public double D0, D1, AMax, Thin; public bool Deck;
                public Sheet(double d0, double d1, double amax, double thin, bool deck = false) { D0 = d0; D1 = d1; AMax = amax; Thin = thin; Deck = deck; }
            }
            public readonly string Name; public readonly double K; public readonly Sheet[] Sheets;
            /// <param name="name">The style's name</param><param name="k">OzFogOfWar.EdgeK: the field's logistic scale (m)</param><param name="sheets">Top to bottom</param>
            public Edge(string name, double k, Sheet[] sheets) { Name = name; K = k; Sheets = sheets; }

            static double Sm5u(double t) { t = t < 0 ? 0 : t > 1 ? 1 : t; return t * t * t * (t * (t * 6 - 15) + 10); }

            /// <summary>The cloud's cover at the signed distance d (m past the hiding line).</summary>
            public double Cover(double d)
            {
                double T = 1.0;
                for (int k = 0; k < Sheets.Length; k++)
                {
                    var s = Sheets[k];
                    if (!(d > s.D0)) continue;                                            // the shader: a sheet is live only where dr > L1.x
                    double span = Math.Max(0.5, s.D1 - s.D0), c = Sm5u((d - s.D0) / span);
                    double a = s.AMax * c * Sm5u(c / Math.Max(0.05, s.Thin));
                    if (s.Deck) a = Math.Max(a, s.AMax * Sm5u((d - (s.D0 + span)) / 4.0));
                    T *= 1.0 - Math.Min(1.0, Math.Max(0.0, a));
                }
                return 1.0 - T;
            }

            /// <summary>Where the cover rises (m): from the lowest D0 to the highest D1 (the deck's closing ramp included).</summary>
            public double FromM { get { double v = double.MaxValue; foreach (var s in Sheets) v = Math.Min(v, s.D0); return v; } }
            public double ToM { get { double v = double.MinValue; foreach (var s in Sheets) v = Math.Max(v, s.Deck ? s.D0 + Math.Max(0.5, s.D1 - s.D0) + 4.0 : s.D1); return v; } }
            /// <summary>The steepest rise of the cover a metre of d (on a 1 mm grid).</summary>
            public double MaxSlope()
            {
                double m = 0, prev = Cover(FromM - 1);
                for (double d = FromM - 1 + 0.001; d <= ToM + 1; d += 0.001) { double c = Cover(d); m = Math.Max(m, (c - prev) / 0.001); prev = c; }
                return m;
            }

            public string Text()
            {
                var sb = new System.Text.StringBuilder($"{Name}: K {K:0.00} m, sheets D0 .. D1 m (AMax)");
                foreach (var s in Sheets) sb.Append($" {s.D0:0.#} .. {s.D1:0.#} ({s.AMax:0.##}{(s.Deck ? ", the deck" : "")});");
                return sb.Append($" cover from {FromM:0.#} m to {ToM:0.#} m, steepest {MaxSlope():0.000} a metre").ToString();
            }

            /// <summary>An edge from the text "name:d0,d1,amax,thin[,deck];..." (the kit's harness: tools/prove_kit.py reads View/OzFogOfWar.Looks.cs).</summary>
            public static Edge Parse(string text, double k)
            {
                int c = text.IndexOf(':'); var l = new List<Sheet>();
                foreach (var part in text.Substring(c + 1).Split(';'))
                {
                    var f = part.Split(','); if (f.Length < 4) continue;
                    double P(int i) => double.Parse(f[i], System.Globalization.CultureInfo.InvariantCulture);
                    l.Add(new Sheet(P(0), P(1), P(2), P(3), f.Length > 4 && f[4].Trim() == "deck"));
                }
                return new Edge(text.Substring(0, c), k, l.ToArray());
            }
        }

        /// <summary>
        /// The edge as the cloud draws it, worked out on the processor the way Resources/OpenZ/FX/OzFogOfWar.shader makes it on the GPU from
        /// the shown map (View/OzFogOfWar.Render.cs): FieldDown (the map averaged 2 x 2 onto a 2 m grid, a 16 texel pad round the map, closed
        /// over 20 m outside it), FieldBlurH and FieldBlurV (its 13 bilinear taps of a Gaussian of sigma 4 texels = 8 m, as 25 texel weights,
        /// clamped at the texture's border); FieldDist (round 4: the field read with a cubic B-spline, its texels clamped at the texture's
        /// border, turned into the signed distance d = -K ln(s / (1 - s)) m, the hiding contour at s = 0.5) and the sheets' cover of d
        /// (<see cref="Edge"/>). In a window of 60 m round the radar:
        /// <list type="bullet">
        /// <item><see cref="ContourM"/>: the hiding contour's distance from the radar, the mean of 64 rays (0 while the radar's own spot is
        /// covered); it starts from a point when ground opens from fully covered, so its first steps grow like a square root.</item>
        /// <item><see cref="VisibleM"/>: the cleared cloud as a radius, sqrt(sum of (1 - cover) x 4 m2 / pi), the cover read at every field
        /// texel's middle (2 m, as before round 4; the distance there is the B-spline's, which the 0.5 m distance texture holds): it grows
        /// smoothly also while a hole first forms.</item>
        /// <item><see cref="CoverStep"/>: the largest change of the cover at any of those points since the last sample (a snap shows here).</item>
        /// </list>
        /// The editor menu compares the field with the shader's own (OzFogOfWar.FieldOf, row H14); the Play check with the game's
        /// (OzFogOfWar.FieldTexture, row R12).
        /// </summary>
        public sealed class FieldMeter
        {
            /// <summary>OzFogOfWar.shader's Blur: the weights and texel offsets of its 7 bilinear taps a side.</summary>
            public static readonly double[] BW = { 0.099908, 0.185003, 0.136012, 0.078177, 0.035128, 0.012338, 0.003387 };
            public static readonly double[] BO = { 0.0, 1.476581, 3.445529, 5.414902, 7.384911, 9.355777, 11.327670 };
            /// <summary>The same blur as weights of the texels -12 .. 12.</summary>
            public static readonly double[] Taps = MakeTaps();
            public const int Pad = 16, Rays = 64;
            public const double OutsideM = 20.0, WindowM = 60.0, RayStepM = 0.25;

            readonly int mw, mh, k0, k1, l0, l1, sk0, sk1, sl0, sl1, kMin, kMax, lMin, lMax, nk, nl, snk, pn;
            readonly double cxm, cym;
            readonly Edge edge;
            readonly double[] src, tmp, fld, cov, pxs, pys;
            bool sampled;
            /// <summary>The last sample: the hiding contour (mean, least of the rays), the cleared cloud as a radius (m), the largest cover change.</summary>
            public double ContourM, ContourMinM, VisibleM, CoverStep;

            static double[] MakeTaps()
            {
                var w = new double[25]; w[12] = BW[0];
                for (int i = 1; i < 7; i++)
                {
                    int a = (int)Math.Floor(BO[i]); double f = BO[i] - a;
                    w[12 + a] += BW[i] * (1 - f); w[12 + a + 1] += BW[i] * f;
                    w[12 - a] += BW[i] * (1 - f); w[12 - a - 1] += BW[i] * f;
                }
                return w;
            }

            /// <param name="maskW">The reveal map's cells (m) across</param><param name="maskH">and down</param>
            /// <param name="cx">The radar's centre, Zod px</param><param name="cy"></param>
            /// <param name="e">The cloud's edge of the style on screen (<see cref="Edge"/>)</param>
            public FieldMeter(int maskW, int maskH, int cx, int cy, Edge e)
            {
                mw = maskW; mh = maskH; cxm = cx / 8.0; cym = cy / 8.0; edge = e;
                int fw = (mw + 1) / 2 + 2 * Pad, fh = (mh + 1) / 2 + 2 * Pad;
                kMin = -Pad; kMax = fw - 1 - Pad; lMin = -Pad; lMax = fh - 1 - Pad;                 // the field texture's texels (relative to the map)
                k0 = Clamp((int)Math.Floor((cxm - WindowM - 1) / 2) - 2, kMin, kMax); k1 = Clamp((int)Math.Ceiling((cxm + WindowM - 1) / 2) + 2, kMin, kMax);   // + the B-spline's texels
                l0 = Clamp((int)Math.Floor((cym - WindowM - 1) / 2) - 2, lMin, lMax); l1 = Clamp((int)Math.Ceiling((cym + WindowM - 1) / 2) + 2, lMin, lMax);
                sk0 = Math.Max(kMin, k0 - 12); sk1 = Math.Min(kMax, k1 + 12); sl0 = Math.Max(lMin, l0 - 12); sl1 = Math.Min(lMax, l1 + 12);
                nk = k1 - k0 + 1; nl = l1 - l0 + 1; snk = sk1 - sk0 + 1;
                src = new double[snk * (sl1 - sl0 + 1)]; tmp = new double[nk * (sl1 - sl0 + 1)]; fld = new double[nk * nl];
                var xs = new List<double>(3000); var ys = new List<double>(3000);                  // the field texels' middles in the window (m), as the model before round 4
                for (int l = l0; l <= l1; l++)
                    for (int k = k0; k <= k1; k++)
                    {
                        double px = 2 * k + 1, py = 2 * l + 1, dx = px - cxm, dy = py - cym;
                        if (dx * dx + dy * dy <= WindowM * WindowM) { xs.Add(px); ys.Add(py); }
                    }
                pxs = xs.ToArray(); pys = ys.ToArray(); pn = pxs.Length; cov = new double[pn];
            }

            static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;

            /// <summary>FieldDown at the field texel (k, l) (relative to the map: texel k covers the map's cells 2k and 2k + 1).</summary>
            double Down(byte[] shown, int k, int l)
            {
                int x0 = Clamp(2 * k, 0, mw - 1), x1 = Clamp(2 * k + 1, 0, mw - 1), y0 = Clamp(2 * l, 0, mh - 1), y1 = Clamp(2 * l + 1, 0, mh - 1);
                double v = (shown[y0 * mw + x0] + shown[y0 * mw + x1] + shown[y1 * mw + x0] + shown[y1 * mw + x1]) / (4.0 * 255.0);
                double xm = 2 * k + 1, ym = 2 * l + 1;
                double ox = Math.Max(0.0, Math.Max(-xm, xm - mw)), oy = Math.Max(0.0, Math.Max(-ym, ym - mh));
                if (ox > 0 || oy > 0) v *= 1.0 - Math.Min(1.0, Math.Sqrt(ox * ox + oy * oy) / OutsideM);
                return v;
            }

            /// <summary>Works the field out from <paramref name="shown"/> (OzFowMask.Shown) and measures it.</summary>
            public void Sample(byte[] shown)
            {
                int snl = sl1 - sl0 + 1;
                for (int l = 0; l < snl; l++) for (int k = 0; k < snk; k++) src[l * snk + k] = Down(shown, sk0 + k, sl0 + l);
                for (int l = 0; l < snl; l++)
                    for (int k = 0; k < nk; k++)
                    {
                        double v = 0; int kc = k0 + k;
                        for (int t = -12; t <= 12; t++) v += Taps[t + 12] * src[l * snk + (Clamp(kc + t, kMin, kMax) - sk0)];
                        tmp[l * nk + k] = v;
                    }
                for (int l = 0; l < nl; l++)
                    for (int k = 0; k < nk; k++)
                    {
                        double v = 0; int lc = l0 + l;
                        for (int t = -12; t <= 12; t++) v += Taps[t + 12] * tmp[(Clamp(lc + t, lMin, lMax) - sl0) * nk + k];
                        fld[l * nk + k] = v;
                    }
                double step = 0, a = 0;
                for (int i = 0; i < pn; i++)
                {
                    double c = CoverAt(pxs[i], pys[i]);
                    if (sampled) step = Math.Max(step, Math.Abs(c - cov[i]));
                    cov[i] = c; a += (1.0 - c) * 4.0;                                           // 4 m2 a point
                }
                VisibleM = Math.Sqrt(a / Math.PI);
                CoverStep = sampled ? step : 0.0; sampled = true;
                double sum = 0, least = double.MaxValue;
                for (int r = 0; r < Rays; r++)
                {
                    double ang = r * 2.0 * Math.PI / Rays, ca = Math.Cos(ang), sa = Math.Sin(ang), d = 0;
                    double prev = At(cxm, cym);
                    if (prev >= 0.5)
                    {
                        d = WindowM;
                        double s0 = 0;                                                          // whole metres first, then RayStepM inside the metre that crosses
                        for (double s = 1.0; s <= WindowM + 1e-9; s += 1.0)
                        {
                            if (At(cxm + ca * s, cym + sa * s) < 0.5) break;
                            s0 = s;
                        }
                        prev = s0 > 0 ? At(cxm + ca * s0, cym + sa * s0) : prev;
                        for (double s = s0 + RayStepM; s <= WindowM + 1e-9; s += RayStepM)
                        {
                            double v = At(cxm + ca * s, cym + sa * s);
                            if (v < 0.5) { d = s - RayStepM + (prev - 0.5) / (prev - v) * RayStepM; break; }
                            prev = v;
                        }
                    }
                    sum += d; least = Math.Min(least, d);
                }
                ContourM = sum / Rays; ContourMinM = least;
            }

            /// <summary>The signed distance (m) at a map point, as FieldDist: -K ln(s / (1 - s)) of the B-spline field (s clamped to 1e-5 ..
            /// 1 - 1e-5, d to -60 .. 60 m).</summary>
            public double DistAt(double xm, double ym)
            {
                double s = Math.Min(1 - 1e-5, Math.Max(1e-5, At(xm, ym)));
                return Math.Min(60.0, Math.Max(-60.0, -edge.K * Math.Log(s / (1 - s))));
            }

            /// <summary>The cloud's cover at a map point (<see cref="Edge.Cover"/> of <see cref="DistAt"/>).</summary>
            public double CoverAt(double xm, double ym) => edge.Cover(DistAt(xm, ym));

            /// <summary>FieldDist's cubic B-spline weights of the texels i - 1 .. i + 2 at the fraction f (its w0 .. w3).</summary>
            public static void Weights(double f, out double w0, out double w1, out double w2, out double w3)
            {
                double f2 = f * f, f3 = f2 * f;
                w0 = (1.0 - 3.0 * f + 3.0 * f2 - f3) / 6.0; w1 = (4.0 - 6.0 * f2 + 3.0 * f3) / 6.0; w2 = (1.0 + 3.0 * f + 3.0 * f2 - 3.0 * f3) / 6.0; w3 = f3 / 6.0;
            }

            /// <summary>The field at a map point (m; x east, y south) as FieldDist reads it since round 4: a cubic B-spline of its texels, each
            /// index clamped at the texture's border as the clamp sampler does (inside the window).</summary>
            public double At(double xm, double ym)
            {
                double u = (xm - 1) / 2, v = (ym - 1) / 2;                                     // field texel coordinates relative to the map (a texel's middle: a whole number)
                int i = (int)Math.Floor(u), j = (int)Math.Floor(v);
                Weights(u - i, out double a0, out double a1, out double a2, out double a3);
                Weights(v - j, out double b0, out double b1, out double b2, out double b3);
                if (i - 1 >= Math.Max(k0, kMin) && i + 2 <= Math.Min(k1, kMax) && j - 1 >= Math.Max(l0, lMin) && j + 2 <= Math.Min(l1, lMax))
                {   // inside: the 16 texels straight from the window (no clamp needed)
                    int o = (j - 1 - l0) * nk + (i - 1 - k0);
                    double r0 = a0 * fld[o] + a1 * fld[o + 1] + a2 * fld[o + 2] + a3 * fld[o + 3]; o += nk;
                    double r1 = a0 * fld[o] + a1 * fld[o + 1] + a2 * fld[o + 2] + a3 * fld[o + 3]; o += nk;
                    double r2 = a0 * fld[o] + a1 * fld[o + 1] + a2 * fld[o + 2] + a3 * fld[o + 3]; o += nk;
                    double r3 = a0 * fld[o] + a1 * fld[o + 1] + a2 * fld[o + 2] + a3 * fld[o + 3];
                    return b0 * r0 + b1 * r1 + b2 * r2 + b3 * r3;
                }
                return b0 * Row(i, j - 1, a0, a1, a2, a3) + b1 * Row(i, j, a0, a1, a2, a3) + b2 * Row(i, j + 1, a0, a1, a2, a3) + b3 * Row(i, j + 2, a0, a1, a2, a3);
            }

            double Row(int i, int l, double a0, double a1, double a2, double a3) => a0 * Tex(i - 1, l) + a1 * Tex(i, l) + a2 * Tex(i + 1, l) + a3 * Tex(i + 2, l);

            /// <summary>The field's texel (k, l) relative to the map, clamped at the texture's border (and at the window's, which the window's
            /// samples never reach).</summary>
            public double Tex(int k, int l) => fld[(Clamp(Clamp(l, lMin, lMax), l0, l1) - l0) * nk + (Clamp(Clamp(k, kMin, kMax), k0, k1) - k0)];

            /// <summary>The cover's sample points (the field texels' middles within 60 m).</summary>
            public int Points => pn;

            /// <summary>The last sample's field at the texel (k, l) relative to the map (the field texture's texel k + Pad, l + Pad); NaN outside the window.</summary>
            public double FieldAt(int k, int l) => k < k0 || k > k1 || l < l0 || l > l1 ? double.NaN : fld[(l - l0) * nk + (k - k0)];
            /// <summary>The window's texels (relative to the map), for comparisons.</summary>
            public int K0 => k0; public int K1 => k1; public int L0 => l0; public int L1 => l1;
        }

        /// <summary>
        /// The circle alone (checks only): a reveal map of its own, of the game map's size, that starts with the circle's start disc open and
        /// shown (its start radius, the radar's building sight: that ground is the zone's own reveal on the game's map) and then gets, every
        /// frame, the circle's stamp at the radius the game's circle has then (OzFowMask.StampFractional; a larger radius covers every smaller
        /// one, so the result is the game's own sequence of stamps) and the game's reveal step of that frame. Its explored edge and its drawn
        /// edge are the circle's own motion, with nothing of the flag's or the buildings' ground mixed in.
        /// </summary>
        public sealed class Circle
        {
            public readonly OzFowMask M; public readonly EdgeMeter Edge; public readonly FieldMeter Field; readonly int cx, cy;
            public Circle(int maskW, int maskH, int cx, int cy, float fromPx, Edge edge, bool field = true)
            {
                this.cx = cx; this.cy = cy;
                M = new OzFowMask(maskW * OzFowMask.CellPx, maskH * OzFowMask.CellPx);
                M.StampFractional(cx, cy, fromPx); M.ShowAll();
                Edge = new EdgeMeter(M, cx, cy, 52 * 8);
                if (field) { Field = new FieldMeter(M.W, M.H, cx, cy, edge); Field.Sample(M.Shown); }
            }
            /// <summary>One frame: the circle's radius now (Zod px, as OzFogOfWar.RadarRadiusPx; nothing stamped when under 0), the reveal step.</summary>
            public void Step(float radiusPx, int revealStep) { if (radiusPx > 0f) M.StampFractional(cx, cy, radiusPx); M.Ease(revealStep); Field?.Sample(M.Shown); }
            public double EdgeM => Edge.RadiusM(M.Target);
        }

        /// <summary>A sight the zone's capture gives the player together with its radar (the flag, a linked building): its centre (Zod px) and
        /// its sight (Zod px, OzFogOfWar.SightPx).</summary>
        public struct Sight { public string Name; public int X, Y, Px; public Sight(string n, int x, int y, int px) { Name = n; X = x; Y = y; Px = px; } }

        /// <summary>A radar zone: the map (Zod px), the radar's centre and building sight, the zone's flag and linked buildings.</summary>
        public sealed class Zone { public string Name; public int MapW, MapH, Cx, Cy, SightPx = RadarSightPx; public Sight[] With; }

        /// <summary>L02's two radar zones (Resources/OpenZ/Maps/L02_grid.json, 72 x 90 tiles): centres as ZObject.SetCords (the top-left tile
        /// x 16 + half the size; radar 4 x 3 tiles, vehicle factory 4 x 5, flag 1 x 1), sights as OzFogOfWar.SightPx (flag 128, factory 40 + 128),
        /// the zone's flag and buildings as ZWorld.GetZoneIndex (the first zone holding the top-left). tools/prove_kit.py checks these numbers
        /// against L02_grid.json.</summary>
        public static readonly Zone L02Zone2 = new Zone { Name = "L02 zone 2", MapW = 72 * 16, MapH = 90 * 16, Cx = 656, Cy = 312,
            With = new[] { new Sight("flag", 744, 328, 128), new Sight("vehicle factory", 768, 392, 168) } };
        public static readonly Zone L02Zone7 = new Zone { Name = "L02 zone 7", MapW = 72 * 16, MapH = 90 * 16, Cx = 464, Cy = 1128,
            With = new[] { new Sight("flag", 456, 1080, 128), new Sight("vehicle factory", 384, 1016, 168) } };
        public static readonly Zone[] L02 = { L02Zone2, L02Zone7 };

        /// <summary>How the opening comes: the zone taken on covered ground (a robot on the flag; the Play check: COVER AGAIN while it is not
        /// ours, then the zone given); COVER AGAIN with the zone ours (StampRound(true) stamps its flag, buildings and the radar's building sight
        /// at once, all shown covered); the zone's ground explored before (its flag's, buildings' and the radar's building sight open and
        /// shown); and <see cref="HalfOpenPx"/> open all round before (a regain after the circle was lost half grown).</summary>
        public enum Mode { Gain, CoverAgain, Explored, HalfOpen }

        /// <summary>OzFogOfWar.StampRound for the zone once it is the player's: its flag, its buildings, the radar's building sight (max-blits:
        /// the same map however often).</summary>
        public static void StampZone(OzFowMask m, Zone z) { foreach (var s in z.With) m.Stamp(s.X, s.Y, s.Px); m.Stamp(z.Cx, z.Cy, z.SightPx); }

        /// <summary>One frame of an opening. The first sample is the frame before the radar's first round (Age -1, no circle). Radius, From:
        /// the code's. Edge, Contour, Visible, CoverStep: the circle alone (<see cref="Circle"/>; the first sample: its start disc). Map...: the
        /// game's map (the zone's own reveal and the circle).</summary>
        public struct Sample
        {
            public float T, Dt, Age; public double RadiusM, FromM, EdgeM, ContourM, VisibleM, CoverStep, MapEdgeM, MapContourM, MapVisibleM, MapCoverStep, RadarMs, EaseMs, FieldMs; public int Down;
        }

        /// <summary>An opening simulated frame by frame as the game runs it (see the class), from the frame before the radar's first round.
        /// phase: the frame of the first stamp round after the capture (0 - StampEvery - 1 frames later). hitchAt / hitchSeconds: one long
        /// frame at that time (s). fields false: no field model (the cost runs).</summary>
        public static List<Sample> Simulate(float fps, float seconds, float revealSeconds, int stampEvery, Zone z, Mode mode, Edge edge,
                                            int phase = 0, float hitchAt = -1f, float hitchSeconds = 0f, bool fields = true)
        {
            const int id = 7;
            var m = new OzFowMask(z.MapW, z.MapH); var r = new OzFowRadar();
            if (mode != Mode.Gain) StampZone(m, z);                                     // COVER AGAIN: StampRound(true), the map shown covered
            if (mode == Mode.HalfOpen) m.StampFractional(z.Cx, z.Cy, HalfOpenPx);
            if (mode == Mode.Explored || mode == Mode.HalfOpen) m.ShowAll();
            var meter = new EdgeMeter(m, z.Cx, z.Cy, 52 * 8);                           // the game's map before the radar's first round
            var field = fields ? new FieldMeter(m.W, m.H, z.Cx, z.Cy, edge) : null;
            field?.Sample(m.Shown);
            var list = new List<Sample>(512);
            list.Add(new Sample { T = -1f / fps, Dt = 1f / fps, Age = -1f, RadiusM = -1, FromM = -1, MapEdgeM = meter.RadiusM(m.Target), MapContourM = field?.ContourM ?? 0, MapVisibleM = field?.VisibleM ?? 0 });
            var prev = new byte[m.Target.Length];
            var sw = new Stopwatch(); Circle c = null;
            float t = 0f; bool hitched = false;
            for (int frame = 0; t <= seconds + 1e-4f; frame++)
            {
                float dt = 1f / fps;
                if (frame > 0 && !hitched && hitchAt >= 0f && t >= hitchAt) { dt = hitchSeconds; hitched = true; }
                if (frame > 0) t += dt;
                Buffer.BlockCopy(m.Target, 0, prev, 0, prev.Length);
                if ((frame + phase) % stampEvery == 0) StampZone(m, z);                  // OzFogOfWar.StampRound
                sw.Restart();
                r.Begin(dt); r.See(m, id, z.Cx, z.Cy, z.SightPx, false); r.End();        // OzFogOfWar.RadarStep
                sw.Stop(); double radarMs = sw.Elapsed.TotalMilliseconds;
                int step = RevealStep(dt, revealSeconds);
                sw.Restart(); m.Ease(step); sw.Stop(); double easeMs = sw.Elapsed.TotalMilliseconds;   // OzFogOfWar.Update: the reveal of this frame
                float rad = r.RadiusOf(id);
                if (c == null && rad > 0f)
                {
                    c = new Circle(m.W, m.H, z.Cx, z.Cy, r.FromOf(id), edge, fields);
                    var p0 = list[0]; p0.EdgeM = c.EdgeM; p0.ContourM = c.Field?.ContourM ?? 0; p0.VisibleM = c.Field?.VisibleM ?? 0; list[0] = p0;
                }
                sw.Restart(); c?.Step(rad, step); field?.Sample(m.Shown); sw.Stop();
                int down = 0; for (int i = 0; i < prev.Length; i++) if (m.Target[i] < prev[i]) down++;
                list.Add(new Sample { T = t, Dt = dt, Age = r.AgeOf(id), RadiusM = rad / 8.0, FromM = r.FromOf(id) / 8.0,
                                      EdgeM = c != null ? c.EdgeM : 0, ContourM = c?.Field?.ContourM ?? 0, VisibleM = c?.Field?.VisibleM ?? 0, CoverStep = c?.Field?.CoverStep ?? 0,
                                      MapEdgeM = meter.RadiusM(m.Target), MapContourM = field?.ContourM ?? 0, MapVisibleM = field?.VisibleM ?? 0, MapCoverStep = field?.CoverStep ?? 0,
                                      RadarMs = radarMs, EaseMs = easeMs, FieldMs = sw.Elapsed.TotalMilliseconds, Down = down });
            }
            return list;
        }

        /// <summary>The drawn opening's numbers from frames (time s, frame s, the hiding contour m, the cleared cloud as a radius m, the
        /// largest cover change a frame), the first of them before the opening.</summary>
        public sealed class Drawn
        {
            public double Start = -1, Rest = -1, V0, V1, C0, C1, CoverStep60, CoverStepRaw, VisStep60, VisStepRaw, VisStepAt, Valley, ValleyAt, P10, P90, CP10, CP90, EmergeAt = -1, EmergeM, VMax;
            public bool DownC, DownV; public int Stops;
            public string Why => $"cleared cloud {V0:0.0} -> {V1:0.0} m, hiding contour {C0:0.0} -> {C1:0.0} m";

            public static Drawn Of(IList<double> T, IList<double> Dt, IList<double> C, IList<double> V, IList<double> Cs)
            {
                var d = new Drawn(); int n = T.Count; if (n < 3) return d;
                d.V0 = V[0]; d.V1 = V[n - 1]; d.C0 = C[0]; d.C1 = C[n - 1];
                int first = -1, last = -1;
                for (int i = 1; i < n; i++)
                {
                    double dv = V[i] - V[i - 1], dc = C[i] - C[i - 1];
                    if (dv < -1e-3) d.DownV = true; if (dc < -1e-3) d.DownC = true;
                    if (dv > 0.005) { if (first < 0) first = i; last = i; }
                    double k = (1.0 / 60.0) / Math.Max(1e-4, Dt[i]);                      // the same motion in a 60 fps frame
                    d.CoverStep60 = Math.Max(d.CoverStep60, Cs[i] * k); d.CoverStepRaw = Math.Max(d.CoverStepRaw, Cs[i]);
                    if (dv > d.VisStepRaw) { d.VisStepRaw = dv; d.VisStepAt = T[i]; }
                    d.VisStep60 = Math.Max(d.VisStep60, dv * k);
                    if (d.EmergeAt < 0 && C[i - 1] <= 0 && C[i] > 0) { d.EmergeAt = T[i]; d.EmergeM = C[i]; }
                }
                if (first < 0) return d;
                d.Start = T[first - 1]; d.Rest = T[last];
                // speeds over the moving stretch: a hesitation is a dip with faster motion before and after it; a stop is a fall from over 2 m/s to under a tenth of it in one frame
                var v = new List<double>(); var tv = new List<double>();
                for (int i = first; i <= last; i++) { v.Add((V[i] - V[i - 1]) / Math.Max(1e-4, Dt[i])); tv.Add(T[i]); }
                var right = new double[v.Count]; double rm = double.MinValue;
                for (int i = v.Count - 1; i >= 0; i--) { right[i] = rm; rm = Math.Max(rm, v[i]); }
                double lm = double.MinValue;
                for (int i = 0; i < v.Count; i++)
                {
                    if (i > 0 && i < v.Count - 1) { double dip = Math.Min(lm, right[i]) - v[i]; if (dip > d.Valley) { d.Valley = dip; d.ValleyAt = tv[i]; } }
                    lm = Math.Max(lm, v[i]); d.VMax = Math.Max(d.VMax, v[i]);
                }
                for (int i = Math.Max(2, first + 1); i < n; i++)
                {
                    double a = (V[i - 1] - V[i - 2]) / Math.Max(1e-4, Dt[i - 1]), b = (V[i] - V[i - 1]) / Math.Max(1e-4, Dt[i]);
                    double ac = (C[i - 1] - C[i - 2]) / Math.Max(1e-4, Dt[i - 1]), bc = (C[i] - C[i - 1]) / Math.Max(1e-4, Dt[i]);
                    bool contourThere = C[i - 2] > 0;                                     // the contour's first frame (a hole appearing) is no motion to stop
                    if ((a > 2.0 && b < 0.1 * a) || (contourThere && ac > 2.0 && bc < 0.1 * ac)) d.Stops++;
                }
                d.Start = Math.Max(0.0, d.Start);
                double span = d.Rest - d.Start;
                d.P10 = Share(T, V, d.Start + 0.1 * span, d.V0, d.V1); d.P90 = Share(T, V, d.Start + 0.9 * span, d.V0, d.V1);
                d.CP10 = Share(T, C, d.Start + 0.1 * span, d.C0, d.C1); d.CP90 = Share(T, C, d.Start + 0.9 * span, d.C0, d.C1);
                return d;
            }

            static double Share(IList<double> T, IList<double> X, double at, double x0, double x1)
            {
                if (x1 - x0 < 1e-6) return 1.0;
                for (int i = 1; i < T.Count; i++)
                    if (T[i] >= at) { double f = (at - T[i - 1]) / Math.Max(1e-9, T[i] - T[i - 1]); return (X[i - 1] + f * (X[i] - X[i - 1]) - x0) / (x1 - x0); }
                return 1.0;
            }

            /// <summary>Every bar of the drawn opening, with <paramref name="restBar"/> s after its start at the latest. The cleared cloud's step
            /// is barred on the frame as it ran (2 % of 48 m); the cover change of a texel is a rate (in a 60 fps frame), at most
            /// <paramref name="coverBar"/> (the style's <see cref="CoverBarOf"/>).</summary>
            public bool Ok(double restBar, double coverBar) => !DownC && !DownV && CoverStep60 <= coverBar && VisStepRaw < StepBarM && Valley <= ValleyBarMs && Stops == 0
                                              && P10 <= EaseShareBar && P90 >= 1.0 - EaseShareBar && Rest >= 0 && Rest <= restBar && C1 >= OzFowRadar.CirclePx / 8.0 - 3.0;

            public string Text(double restBar, double coverBar) =>
                $"never down ({(DownC || DownV ? "WENT DOWN" : "contour and cleared cloud")}); the cleared cloud's largest step {VisStepRaw:0.000} m a frame at {VisStepAt:0.00} s (bar {StepBarM:0.00}; "
                + $"{VisStep60:0.000} m in a 60 fps frame); a texel's cover changes at most {CoverStep60:0.000} in a 60 fps frame (a rate, bar {coverBar:0.000}: {(coverBar > CoverStepBar ? "the fog's own reveal of a radar's capture on this look" : "0.10")}; {CoverStepRaw:0.000} in one of these frames); "
                + $"one motion: deepest speed dip {Valley:0.00} m/s{(Valley > 0.05 ? $" at {ValleyAt:0.00} s" : "")} (bar {ValleyBarMs:0.0}), top speed {VMax:0.0} m/s, dead stops {Stops}; "
                + $"eased: {P10 * 100:0.0} % of it in the first 10 % of its time, {(1 - P90) * 100:0.0} % in the last 10 % (bar {EaseShareBar * 100:0} %, linear 10 %; the hiding contour {CP10 * 100:0.0} / {(1 - CP90) * 100:0.0} %); "
                + $"moves {Start:0.00} - {Rest:0.00} s (at rest by {restBar:0.00} s), {Why}{(EmergeAt >= 0 ? $"; the hiding contour appears at {EmergeAt:0.00} s ({EmergeM:0.0} m, a hole forming)" : "")}";
        }

        /// <summary>The drawn opening of samples (from the first, the frame before): the circle alone, or (map) the game's map.</summary>
        public static Drawn DrawnOf(List<Sample> s, bool map = false)
        {
            var T = new List<double>(); var Dt = new List<double>(); var C = new List<double>(); var V = new List<double>(); var Cs = new List<double>();
            foreach (var x in s) { T.Add(x.T); Dt.Add(x.Dt); C.Add(map ? x.MapContourM : x.ContourM); V.Add(map ? x.MapVisibleM : x.VisibleM); Cs.Add(map ? x.MapCoverStep : x.CoverStep); }
            return Drawn.Of(T, Dt, C, V, Cs);
        }

        /// <summary>The numbers of one opening (simulated, or recorded in Play): the code's radius and the circle's explored edge, from its
        /// samples (the first: the frame before; the second: the radar's first round, at its start radius); the circle as drawn and the
        /// game's map as drawn.</summary>
        public sealed class Opening
        {
            public double FirstM, FromM, Reach = -1, LastStep, Step, StepAt, StepAtM, StepDt, Step60, EStep, EGap, EEnd, P10, P90, E10, E90, MapJump;
            public bool DownR, DownE; public int Frames, Down; public Drawn D, Map;

            public static Opening Of(List<Sample> s)
            {
                var o = new Opening(); if (s.Count < 3) return o;
                double to = OzFowRadar.CirclePx / 8.0;
                o.FirstM = s[1].RadiusM; o.FromM = s[1].FromM;
                for (int i = 1; i < s.Count; i++)
                {
                    o.Down += s[i].Down;
                    o.MapJump = Math.Max(o.MapJump, s[i].MapEdgeM - s[i - 1].MapEdgeM);
                    if (i == 1) continue;
                    double dr = s[i].RadiusM - s[i - 1].RadiusM, de = s[i].EdgeM - s[i - 1].EdgeM;
                    if (dr < -1e-5) o.DownR = true; if (de < -1e-5) o.DownE = true;
                    if (dr > o.Step) { o.Step = dr; o.StepAt = s[i].T; o.StepAtM = s[i].RadiusM; o.StepDt = s[i].Dt; }
                    o.Step60 = Math.Max(o.Step60, dr * (1.0 / 60.0) / Math.Max(1e-4, s[i].Dt));
                    o.EStep = Math.Max(o.EStep, de);
                    if (s[i].RadiusM >= o.FromM + 2.5) o.EGap = Math.Max(o.EGap, Math.Abs(s[i].EdgeM - s[i].RadiusM));
                    if (o.Reach < 0) { o.Frames++; if (s[i].RadiusM >= to - 1e-6) { o.Reach = s[i].T; o.LastStep = dr; o.EEnd = s[i].EdgeM; } }   // the code sets 48 m exactly when done
                }
                double span = to - o.FromM;
                o.P10 = Progress(s, 0.1f * OzFowRadar.GrowSeconds, x => (x.RadiusM - o.FromM) / span); o.P90 = Progress(s, 0.9f * OzFowRadar.GrowSeconds, x => (x.RadiusM - o.FromM) / span);
                o.E10 = Progress(s, 0.1f * OzFowRadar.GrowSeconds, x => (x.EdgeM - o.FromM) / span); o.E90 = Progress(s, 0.9f * OzFowRadar.GrowSeconds, x => (x.EdgeM - o.FromM) / span);
                o.D = DrawnOf(s); o.Map = DrawnOf(s, true);
                return o;
            }

            /// <summary>The code's bars: starts at <paramref name="wantFromM"/>, grows only, 48 m at 2.5 s +- <paramref name="reachTol"/>, the raw step
            /// under 2 % of 48 m, eased (at most 3 % by 10 % of the time, at least 97 % by 90 %), no snap at the end.</summary>
            public bool CodeOk(double wantFromM, double reachTol) => Math.Abs(FirstM - wantFromM) < 0.01 && Math.Abs(FromM - wantFromM) < 0.01 && !DownR && Reach > 0
                && Math.Abs(Reach - OzFowRadar.GrowSeconds) <= reachTol && Step < StepBarM && P10 <= 0.03 && P90 >= 0.97 && LastStep < 0.01;
            /// <summary>The circle's explored edge: grows only, the raw step under 2 % of 48 m, within 0.25 m of the code's radius, 47.75 m when
            /// the code reaches 48 m, eased (at most 5 % by 10 % of the time, at least 95 % by 90 %).</summary>
            public bool EdgeOk() => !DownE && EStep < StepBarM && EGap < 0.25 && EEnd >= OzFowRadar.CirclePx / 8.0 - 0.25 && E10 <= 0.05 && E90 >= 0.95;
        }

        /// <summary>The share at the sample whose age first reaches <paramref name="age"/> s.</summary>
        static double Progress(List<Sample> s, float age, Func<Sample, double> f)
        {
            for (int i = 1; i < s.Count; i++) if (s[i].Age >= age - 1e-4f) return f(s[i]);
            return double.NaN;
        }

        /// <summary>The rows. row(id, ok, text); say(text) for the other lines. edge: the cloud's edge of the style the rows measure (<see cref="Edge"/>).
        /// Returns the failed rows.</summary>
        public static int Run(Action<string, bool, string> row, Action<string> say, float revealSeconds, int stampEvery, Edge edge)
        {
            int fails = 0;
            void Row(string id, bool ok, string text) { if (!ok) fails++; row(id, ok, text); }
            double sight = RadarSightPx / 8.0, to = OzFowRadar.CirclePx / 8.0;
            double captureRate = CaptureCoverRate(edge, revealSeconds), coverBar = CoverBarOf(edge, revealSeconds);
            say($"radar circle {to:0} m over {OzFowRadar.GrowSeconds:0.0} s from the radar's building sight ({sight:0} m), reveal {revealSeconds:0.00} s, stamp round every {stampEvery} frames; "
                + $"L02's radar zones: {L02Zone2.Name} (its flag {Dist(L02Zone2, 0):0.0} m away, its vehicle factory {Dist(L02Zone2, 1):0.0} m), {L02Zone7.Name} (flag {Dist(L02Zone7, 0):0.0} m, factory {Dist(L02Zone7, 1):0.0} m); "
                + $"the cloud's edge (blur 8 m, the signed distance of its B-spline, the sheets' ramps): {edge.Text()}; the cover-rate bar on this look {coverBar:0.000} in a 60 fps frame "
                + $"(0.10, or the fog's own reveal of a radar's capture where that is faster: its 20 m building sight on covered ground, {revealSeconds:0.0} s a cell, changes a texel's cover up to {captureRate:0.000} in a 60 fps frame)");

            // H1 the ease itself
            bool mono = true; double last = -1.0;
            for (int i = 0; i <= 10000; i++) { double e = OzFowRadar.Ease(i / 10000.0); if (e < last) mono = false; last = e; }
            double sym = 0.0; for (int i = 0; i <= 100; i++) sym = Math.Max(sym, Math.Abs(OzFowRadar.Ease(i / 100.0) + OzFowRadar.Ease(1.0 - i / 100.0) - 1.0));
            double slope0 = OzFowRadar.Ease(0.001) / 0.001, slope1 = (1.0 - OzFowRadar.Ease(0.999)) / 0.001;
            Row("H1", OzFowRadar.Ease(0) == 0.0 && OzFowRadar.Ease(1) == 1.0 && Math.Abs(OzFowRadar.Ease(0.5) - 0.5) < 1e-12 && mono && sym < 1e-12 && slope0 < 1e-4 && slope1 < 1e-4,
                $"smootherstep: 0 at 0, 1 at 1, 0.5 at 0.5, rising on 10001 steps, symmetric (err {sym:0.0e0}), slope at both ends {slope0:0.0e0} / {slope1:0.0e0} (0 = no snap)");

            // H2 - H4 at 60 fps, H5 at 25 fps: L02's two radar zones, each taken on covered ground (at 60 fps in each of the stamp round's 3
            // phases) and after COVER AGAIN; the zone's flag and factory are the player's with the radar and stamped by the stamp round
            var mapInfo = new List<string>();
            foreach (float fps in new[] { 60f, 25f })
            {
                var cases = new List<(string name, Opening o)>();
                foreach (var z in L02)
                {
                    for (int ph = 0; ph < (fps == 60f ? stampEvery : 1); ph++)
                        cases.Add(($"{z.Name} taken (stamp round {ph} frames after)", Opening.Of(Simulate(fps, 4.0f, revealSeconds, stampEvery, z, Mode.Gain, edge, (stampEvery - ph) % stampEvery))));
                    cases.Add(($"{z.Name} after COVER AGAIN", Opening.Of(Simulate(fps, 4.0f, revealSeconds, stampEvery, z, Mode.CoverAgain, edge))));
                }
                string tag = fps == 60f ? "60 fps" : "25 fps", a = fps == 60f ? "H2" : "H5a", b = fps == 60f ? "H3" : "H5b", c = fps == 60f ? "H4" : "H5c";
                var failA = new List<string>(); var failB = new List<string>(); var failC = new List<string>();
                double f0 = 99, f1 = 0, r0 = 99, r1 = 0, st = 0, stPct = 0, st60 = 0, p10 = 0, p90 = 1, ls = 0, es = 0, gap = 0, eEnd = 99, e10 = 0, e90 = 1, jump = 0; string stCase = "";
                int down = 0; Opening worst = null; string worstName = ""; double mapCover = 0;
                foreach (var (name, o) in cases)
                {
                    if (!o.CodeOk(sight, 1.01 / fps)) failA.Add(name);
                    if (!o.EdgeOk() || o.Down > 0) failB.Add(name);
                    if (!o.D.Ok(RestBarS, coverBar)) failC.Add(name);
                    f0 = Math.Min(f0, o.FirstM); f1 = Math.Max(f1, o.FirstM); r0 = Math.Min(r0, o.Reach); r1 = Math.Max(r1, o.Reach);
                    if (o.Step > st) { st = o.Step; stPct = o.Step / Math.Max(1e-6, o.StepAtM) * 100; stCase = name; }
                    st60 = Math.Max(st60, o.Step60); p10 = Math.Max(p10, o.P10); p90 = Math.Min(p90, o.P90); ls = Math.Max(ls, o.LastStep);
                    es = Math.Max(es, o.EStep); gap = Math.Max(gap, o.EGap); eEnd = Math.Min(eEnd, o.EEnd); e10 = Math.Max(e10, o.E10); e90 = Math.Min(e90, o.E90);
                    down += o.Down; jump = Math.Max(jump, o.MapJump); mapCover = Math.Max(mapCover, o.Map.CoverStep60);
                    bool bad = !o.D.Ok(RestBarS, coverBar);
                    if (worst == null || (bad && worst.D.Ok(RestBarS, coverBar)) || (bad == !worst.D.Ok(RestBarS, coverBar) && o.D.Valley + o.D.P10 * 10 > worst.D.Valley + worst.D.P10 * 10)) { worst = o; worstName = name; }
                    if (fps == 60f) mapInfo.Add($"{name}: {(o.Map.Ok(RestBarS, coverBar) ? "within" : "outside")} the circle's bars (dip {o.Map.Valley:0.0} m/s, {o.Map.P10 * 100:0} % in the first 10 %, the explored edge's largest jump {o.MapJump:0.0} m, a texel's cover up to {o.Map.CoverStep60:0.000} in a 60 fps frame)");
                }
                string Fails(List<string> l) => l.Count == 0 ? "" : $"; FAILED: {string.Join(", ", l)}";
                Row(a, failA.Count == 0, $"{tag}, the code's radius in {cases.Count} openings ({(fps == 60f ? "each zone taken with its first stamp round 0, 1 or 2 frames after, and after COVER AGAIN" : "each zone taken, and after COVER AGAIN")}): "
                    + $"starts at {f0:0.00} - {f1:0.00} m (the radar's building sight {sight:0} m), grows only, 48 m at {r0:0.000} - {r1:0.000} s (2.5 s +- one frame); "
                    + $"largest step {st:0.000} m a frame (bar {StepBarM:0.00} = 2 % of 48 m; {stPct:0.00} % of the radius then; {st60:0.000} m in a 60 fps frame; {stCase}); "
                    + $"eased: at most {p10 * 100:0.00} % of the growth by 10 % of the time, at least {p90 * 100:0.00} % by 90 % (bars 3 / 97, linear 10 / 90); last step at most {ls * 1000:0.000} mm (no snap at the end){Fails(failA)}");
                Row(b, failB.Count == 0, $"{tag}, the circle's edge on the explored map (the circle alone, from its start disc): grows only, largest step {es:0.000} m a frame (bar {StepBarM:0.00}), "
                    + $"within {gap:0.000} m of the code's radius from 2.5 m past its start (bar 0.25), at least {eEnd:0.00} m when the code reaches 48 m, eased {e10 * 100:0.0} % / {e90 * 100:0.0} % (bars 5 / 95); "
                    + $"the game's map: explored cells gone down {down} (explore-once); its explored edge jumps up to {jump:0.0} m in a frame at the capture (the flag's and the factory's ground, the normal reveal of a capture, information){Fails(failB)}");
                Row(c, failC.Count == 0, $"{tag}, the circle as the cloud draws it (the shader's soft edge field of the circle's shown map), {cases.Count - failC.Count} of {cases.Count} within the bars; the closest, {worstName}: "
                    + worst.D.Text(RestBarS, coverBar) + $"; for comparison, the fog's own reveal of the capture on the game's map (the flag's, the factory's and the radar's 20 m, 0.8 s a cell, before this kit too) changes a texel's cover up to {mapCover:0.000} in a 60 fps frame" + Fails(failC));
            }

            // H15 (information): the game's map, the zone's own reveal and the circle together, as on screen
            say("H15 (information) the game's map with the zone's own reveal (its flag 16 m, its factory 21 m and the radar's building sight 20 m open at the capture, 0.8 s a cell, "
                + "the reveal of every capture with the fog on, before this kit too), 60 fps: " + string.Join("; ", mapInfo));

            // H16 the zone's ground open before: the game's map itself is the circle's motion here (60 fps, zone 2)
            foreach (var mode in new[] { Mode.Explored, Mode.HalfOpen })
            {
                var o = Opening.Of(Simulate(60f, 4.0f, revealSeconds, stampEvery, L02Zone2, mode, edge));
                double want = mode == Mode.Explored ? sight : HalfOpenPx / 8.0;
                Row(mode == Mode.Explored ? "H16a" : "H16b", o.CodeOk(want, 1.01 / 60.0) && o.Map.Ok(RestBarS, coverBar) && o.D.Ok(RestBarS, coverBar),
                    $"60 fps, {L02Zone2.Name} taken, {(mode == Mode.Explored ? "its ground explored before (the flag's, the factory's and the radar's building sight open and shown)" : $"open all round to {want:0} m before (a regain after the circle was lost half grown)")}: "
                    + $"the circle from {o.FirstM:0.00} m ({(mode == Mode.Explored ? "its building sight" : "the open ground's edge, so no time passes with nothing to see")}), 48 m at {o.Reach:0.000} s, largest step {o.Step:0.000} m a frame; "
                    + $"the game's map as drawn: " + o.Map.Text(RestBarS, coverBar) + $"; the circle alone: dip {o.D.Valley:0.00} m/s, {o.D.P10 * 100:0.0} / {(1 - o.D.P90) * 100:0.0} %");
            }

            // H6 a hitch: one frame of 1 s at 1.0 s moves the growth by MaxStepSeconds only
            {
                var s = Simulate(60f, 4.0f, revealSeconds, stampEvery, L02Zone2, Mode.Gain, edge, 0, 1.0f, 1.0f, false);
                int k = s.FindIndex(x => x.Dt > 0.5f);
                bool ok = k > 1 && Math.Abs((s[k].Age - s[k - 1].Age) - OzFowRadar.MaxStepSeconds) < 1e-4;
                double jump = k > 1 ? s[k].RadiusM - s[k - 1].RadiusM : -1, plain = k > 1 ? (OzFowRadar.RadiusAt((float)(s[1].FromM * 8), s[k - 1].Age + 1.0f) - OzFowRadar.RadiusAt((float)(s[1].FromM * 8), s[k - 1].Age)) / 8.0 : -1;
                float tEnd = -1f; foreach (var x in s) if (tEnd < 0f && x.RadiusM >= to - 1e-6) tEnd = x.T;
                Row("H6", ok && tEnd > OzFowRadar.GrowSeconds + 0.85f, $"a 1 s hitch at 1.0 s: the growth moves {(k > 1 ? s[k].Age - s[k - 1].Age : -1):0.000} s in that frame ({jump:0.00} m; without the limit it would jump {plain:0.00} m), "
                    + $"48 m at {tEnd:0.00} s of real time (later by the hitch, no jump)");
            }

            // H7 the start: open at once
            {
                var m = new OzFowMask(MapW, MapH); var r = new OzFowRadar(); int cx = 400, cy = 600;
                r.Begin(0f); var st = r.See(m, 1, cx, cy, RadarSightPx, true); r.End(); m.ShowAll();
                int stamps = m.Stamps; r.Begin(1f / 60f); var st2 = r.See(m, 1, cx, cy, RadarSightPx, false); r.End();
                int closed = 0; for (int a = 0; a < 64; a++) { double ang = a * Math.PI / 32; if (m.ShownAtPx(cx + (int)Math.Round(Math.Cos(ang) * 45 * 8), cy + (int)Math.Round(Math.Sin(ang) * 45 * 8)) < OzFowMask.Revealed) closed++; }
                Row("H7", st == OzFowRadar.Step.OpenedAtOnce && st2 == OzFowRadar.Step.Idle && r.RadiusOf(1) == OzFowRadar.CirclePx && r.Growing == 0 && m.Stamps == stamps && closed == 0 && r.AtOnce == 1,
                    $"a radar held at the start: the whole circle at once ({st}, then {st2}, radius {r.RadiusOf(1) / 8:0} m, growing {r.Growing}), shown at 45 m on {64 - closed} of 64 points before the first frame, no stamp after it");
            }

            // H8 lost and regained, H9 a ruin rebuilt, H10 COVER AGAIN
            {
                var z = L02Zone2; var m = new OzFowMask(z.MapW, z.MapH); var r = new OzFowRadar(); int cx = z.Cx, cy = z.Cy;
                int Grow(int frames) { int n = 0; for (int i = 0; i < frames; i++) { r.Begin(1f / 60f); r.See(m, 3, cx, cy, RadarSightPx, false); r.End(); n++; } return n; }
                StampZone(m, z); Grow(1); Grow(160);
                bool open1 = r.RadiusOf(3) == OzFowRadar.CirclePx;
                var before = (byte[])m.Target.Clone();
                r.Begin(1f / 60f); r.End();                                              // lost: not seen this round
                bool forgot = r.RadiusOf(3) < 0f && r.Forgotten == 1;
                int down = 0; for (int i = 0; i < before.Length; i++) if (m.Target[i] < before[i]) down++;
                r.Begin(1f / 60f); var again = r.See(m, 3, cx, cy, RadarSightPx, false); r.End();
                float edgeAgain = r.EdgeOf(3);
                for (int i = 0; i < before.Length; i++) if (m.Target[i] < before[i]) down++;
                r.Begin(1f / 60f); r.End();                                              // lost again; the ground covered meanwhile (COVER AGAIN), regained
                m.Clear(); StampZone(m, z);
                r.Begin(1f / 60f); var again2 = r.See(m, 3, cx, cy, RadarSightPx, false); r.End();
                float r0 = r.RadiusOf(3); Grow(80); float mid = r.RadiusOf(3); Grow(80);
                Row("H8", open1 && forgot && down == 0 && again == OzFowRadar.Step.AlreadyOpen && edgeAgain >= OzFowRadar.CirclePx - 1f && again2 == OzFowRadar.Step.Started && Math.Abs(r0 - RadarSightPx) < 1e-3
                          && mid > r0 && mid < OzFowRadar.CirclePx && r.RadiusOf(3) == OzFowRadar.CirclePx,
                    $"lost, then regained: the growth forgotten ({forgot}), explored cells gone down {down} (explore-once); regained on its open ground: the open ground's edge {edgeAgain / 8:0} m "
                    + $"({again}: all 48 m open, nothing to animate); regained after the ground was covered again: {again2} from {r0 / 8:0.0} m (its building sight), {mid / 8:0.0} m after 80 frames, {r.RadiusOf(3) / 8:0} m after 160");

                for (int i = 0; i < 30; i++) { r.Begin(1f / 60f); r.End(); }              // a ruin: not standing, not seen
                bool ruinNone = r.RadiusOf(3) < 0f;
                var m2 = new OzFowMask(z.MapW, z.MapH);                                  // its ground never opened (a ruin of the zone, never standing as ours)
                r.Begin(1f / 60f); var reb = r.See(m2, 3, cx, cy, RadarSightPx, false); r.End();
                Row("H9", ruinNone && reb == OzFowRadar.Step.Started && r.Growing == 1 && Math.Abs(r.FromOf(3) - RadarSightPx) < 1e-3,
                    $"a ruin (not standing: no circle, {ruinNone}), then rebuilt (standing again) on ground never opened: {reb}, growing {r.Growing}, from {r.FromOf(3) / 8:0.0} m (its building sight)");

                m.Clear(); StampZone(m, z); r.Reset();                                   // COVER AGAIN: the map covered, StampRound(true), the growths forgotten
                var meter = new EdgeMeter(m, cx, cy, 52 * 8);                            // the covered map with the zone stamped, before the radar's round
                r.Begin(1f / 60f); var cov = r.See(m, 3, cx, cy, RadarSightPx, false); r.End();
                double e0 = meter.RadiusM(m.Target); Grow(75); double e1 = meter.RadiusM(m.Target); Grow(80); double e2 = meter.RadiusM(m.Target);
                Row("H10", cov == OzFowRadar.Step.Started && Math.Abs(r.FromOf(3) - RadarSightPx) < 1e-3 && Math.Abs(e0 - sight) < 0.3 && e1 > e0 + 5 && e1 < to - 5 && e2 >= to - 0.25,
                    $"COVER AGAIN: the radar opens again, animated ({cov}, from {r.FromOf(3) / 8:0.0} m, its building sight): the edge {e0:0.00} m (the meter's base: the open ground's edge {meter.BaseM:0.00} m), "
                    + $"{e1:0.0} m after 75 frames, {e2:0.00} m after 155");
            }

            // H11 the fractional stamp equals the unit kernel at a whole radius round a cell middle
            {
                var a = new OzFowMask(512, 512); var b = new OzFowMask(512, 512);
                a.Stamp(8 * 32 + 4, 8 * 32 + 4, 160); b.StampFractional(8 * 32 + 4, 8 * 32 + 4, 160f);
                int diff = 0, worst = 0; for (int i = 0; i < a.Target.Length; i++) { int dd = Math.Abs(a.Target[i] - b.Target[i]); if (dd > 0) diff++; worst = Math.Max(worst, dd); }
                float openEdge = OzFowRadar.OpenEdgePx(a, 8 * 32 + 4, 8 * 32 + 4, OzFowRadar.CirclePx);
                Row("H11", worst <= 1 && Math.Abs(openEdge - 160f) <= 4f, $"the fractional stamp at 20 m round a cell's middle = the units' kernel: {diff} cells differ, by at most {worst} of 255 (rounding); "
                    + $"the open ground's edge of that kernel {openEdge / 8:0.000} m (its stamp radius 20 m)");
            }

            // H12 the edge moves within a cell: 0.1 m more radius raises the edge cells by a few levels, never a whole step
            {
                var m = new OzFowMask(1024, 1024); m.StampFractional(515, 509, 300f);
                var before = (byte[])m.Target.Clone(); long c0 = m.CellsRaised;
                float openEdge = OzFowRadar.OpenEdgePx(m, 515, 509, OzFowRadar.CirclePx);
                m.StampFractional(515, 509, 300.8f);
                int most = 0; for (int i = 0; i < before.Length; i++) most = Math.Max(most, m.Target[i] - before[i]);
                Row("H12", m.CellsRaised > c0 && most <= 10 && Math.Abs(openEdge - 300f) < 0.5f, $"0.1 m more radius: {m.CellsRaised - c0} edge cells go up, by at most {most} of 255 (a whole cell would be up to 85): the edge slides, no metre steps; "
                    + $"a circle grown on from its own open edge ({openEdge / 8:0.000} m, its radius 37.5 m) raises nothing before it is past it");
            }

            // H13 cost: the radar's round every frame of three 60 fps openings (L02 zone 2 taken; each first frame measures the open ground), and
            // one stamp of the whole circle on level 17's map. Barred on the 99th percentile and the median: a single frame far over them is the
            // process put aside by the system (the kit's harness runs at below-normal priority on purpose), printed as the worst; the Play check's
            // R5 bars the worst frame.
            {
                var ms = new List<double>(); var starts = new List<double>(); double sum = 0, emx = 0;
                for (int run = 0; run < 3; run++)
                {
                    var s = Simulate(60f, 3.0f, revealSeconds, stampEvery, L02Zone2, Mode.Gain, edge, 0, -1f, 0f, false);
                    for (int i = 2; i < s.Count; i++) { ms.Add(s[i].RadarMs); sum += s[i].RadarMs; emx = Math.Max(emx, s[i].EaseMs); }
                    starts.Add(s[1].RadarMs);
                }
                ms.Sort(); starts.Sort();
                double p99 = ms[Math.Min(ms.Count - 1, (int)Math.Floor(0.99 * ms.Count))], mx = ms[ms.Count - 1], startMs = starts[1];
                var sw = new Stopwatch(); var full = new List<double>();
                for (int k = 0; k < 21; k++) { var m = new OzFowMask(MapW, MapH); sw.Restart(); m.StampFractional(MapW / 2, MapH / 2, OzFowRadar.CirclePx); sw.Stop(); full.Add(sw.Elapsed.TotalMilliseconds); }
                full.Sort();
                double fieldMs = 0; { var s = Simulate(60f, 0.5f, revealSeconds, stampEvery, L02Zone2, Mode.Gain, edge); for (int i = 2; i < s.Count; i++) fieldMs = Math.Max(fieldMs, s[i].FieldMs); }
                Row("H13", p99 < 0.25 && full[10] < 0.5 && startMs < 0.5, $"cost (this machine, this build): the radar's round during an opening ({ms.Count} frames of 3 openings) mean {sum / Math.Max(1, ms.Count):0.0000} ms, "
                    + $"99 % at or under {p99:0.0000} ms a frame (bar 0.25; the worst single frame {mx:0.0000} ms, information); its first frame (the open ground measured round it) median {startMs:0.0000} ms (bar 0.5); "
                    + $"the whole 48 m circle stamped on a closed map of level 17's size median {full[10]:0.0000} ms, worst {full[20]:0.0000} ms (bar 0.5 on the median); the reveal's Ease that frame worst {emx:0.0000} ms (information); "
                    + $"the checks' own two field models {fieldMs:0.000} ms a frame at most (checks only, never in the game)");
            }
            return fails;
        }

        static double Dist(Zone z, int i) { double dx = z.With[i].X - z.Cx, dy = z.With[i].Y - z.Cy; return Math.Sqrt(dx * dx + dy * dy) / 8.0; }
    }
}
