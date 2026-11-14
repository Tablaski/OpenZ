using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.4 radar_enemy_bars, DV172 (View/OzRadarBars.cs, View/OzRadarBarsRule.cs): the counted check, headless, on the real level 2 map, nothing
    /// saved: menu OpenZ/V1.4/Radar Enemy Bars: Count (headless, DV172), and off-project on the editor's Mono (the kit's tools/harness: the same
    /// file with OPENZ_HARNESS, the radar truth there is OzStrikeWarning.OwnsRadar's own source cut out of View/OzStrikeWarning.cs). One line per
    /// fact, [OpenZ V1.4 radar bars] PASS n/n. The world: level 2, red the player, blue a CPU that gives no orders.
    ///  R  the radar truth (OzStrikeWarning.OwnsRadar, shared with the minimap's dots and the strike warning): every radar neutral at the start, a
    ///     zone given, a radar ruined, the other side's radar, a second radar;
    ///  D  the rule on real objects, settled: own / enemy robot / enemy gun / neutral empty vehicle / neutral building / rock x radar yes / no x the
    ///     rule on, DV33's switch off, the Unit editor, 'Zod exact', a computer match (a real world with both CPUs): only an enemy without a radar
    ///     with the rule on gets 0, everything else 1;
    ///  P  the unit panel's label (HEALTH · NEEDS RADAR only for an enemy shown without a radar; its length and short form; P4: it switches in
    ///     the same frame as the bar, after the fade out and on the fade in's first drawn frame, at 60 and 25 fps);
    ///  F  the hold and the fade: smootherstep at 0 / 1/4 / 1/2 / 1, monotonic, symmetric; a radar gained / lost counts after 0.6 s of game time
    ///     (nothing moves before), then 0.35 s at 60 and at 25 fps, the middle at half time; per frame never more than its slope allows; a short
    ///     loss half way is not followed; a switch half way turns the fade from where it is; a pause (a 5 s frame, a 0 s frame, NaN; the sim clock
    ///     stopped holds the hold); a new match snaps; at 0 nothing drawn; a contested radar flag (a new owner every 0.2 s, and at random gaps
    ///     under the hold) moves nothing; the log's pace (at most one line a second); the hold at game speed 4 and 0.5;
    ///  G  no garbage: 300 frames of the radar truth + the rule + six bars, 0 bytes; the radar truth's cost a call on level 2; G1 only where the
    ///     thread counter sees a known 4 KB array (round 2: Unity's Boehm Mono returns 0, then G1 is an INFO line, not a PASS);
    ///  S  the Sim never names the rule (source scan of Scripts/Sim; the rules and the computer cannot read it);
    ///  W  (the editor only) the game's own wiring: OzRadarBars.WhyOffNow reads DV33's live switch, the Unit editor, 'Zod exact', the human side;
    ///     BarAlpha / Hides / BarOff read the game's one state; the frame cache; the fade's length (0.35 s, CALM MODE or not); W7 the garbage of
    ///     that path, as G1 (INFO where the counter cannot count; the Play proof's Q2 counts it with the engine's own counter).
    /// </summary>
    public static class OzV14RadarBarsCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red, Foe = Team.Blue;
        const float Fade = OzRadarBarsRule.FadeSeconds;
        const double Hold = OzRadarBarsRule.HoldSec;
        const int None = OzRadarBarsRule.NoChange;

        /// <summary>The radar truth the game asks (the strike warning's own; in the off-Unity harness its source text, cut out of the file).</summary>
#if OPENZ_HARNESS
        static bool OwnsRadar(ZWorld w, Team t) => OzStrikeWarningCut.OwnsRadar(w, t);
        const string TruthName = "OzStrikeWarning.OwnsRadar (its source cut out of View/OzStrikeWarning.cs)";
#else
        static bool OwnsRadar(ZWorld w, Team t) => OzStrikeWarning.OwnsRadar(w, t);
        const string TruthName = "OzStrikeWarning.OwnsRadar";
#endif

        /// <summary>Level 2, red the player; cpuBlue / cpuRed: that side is a CPU that gives no orders.</summary>
        static ZWorld New(string set, string map, bool cpuBlue = true, bool cpuRed = false)
        {
            var w = ZWorld.Create(set, map, 1);
            w.NoGameEnd = true;
            if (cpuBlue) w.Bots.Add(new ZBot(w, Foe, 102) { Ignored = true });
            if (cpuRed) w.Bots.Add(new ZBot(w, Me, 103) { Ignored = true });
            return w;
        }
        static List<ZObject> Radars(ZWorld w) => w.Objects.Where(o => o.Kind == ZKind.Building && o.Type == "radar").OrderBy(o => o.Id).ToList();
        static ZObject FlagOf(ZWorld w, ZObject b) => w.Flags.FirstOrDefault(f => f.LinkedObjects.Contains(b));
        static void Ruin(ZWorld w, ZObject b) { if (b != null && !b.Destroyed) { b.SetHealth(0); w.UpdateObjectHealth(b); } }

        /// <summary>The objects of the table: (name, object, is it an enemy of red).</summary>
        static List<(string name, ZObject o, bool enemy)> Cast(ZWorld w)
        {
            var c = new List<(string, ZObject, bool)>();
            void Add(string n, ZObject o, bool e) { if (o != null) c.Add((n, o, e)); }
            Add("own grunt", w.Objects.FirstOrDefault(o => o.Kind == ZKind.Robot && o.Owner == Me && !o.IsMinion), false);
            Add("enemy grunt", w.Objects.FirstOrDefault(o => o.Kind == ZKind.Robot && o.Owner == Foe && !o.IsMinion), true);
            Add("enemy gun", w.Objects.FirstOrDefault(o => o.Kind == ZKind.Cannon && o.Owner == Foe), true);
            Add("empty jeep (neutral)", w.Objects.FirstOrDefault(o => o.Kind == ZKind.Vehicle && o.Owner == Team.Neutral), false);
            Add("empty gatling (neutral)", w.Objects.FirstOrDefault(o => o.Kind == ZKind.Cannon && o.Owner == Team.Neutral), false);
            Add("neutral factory", w.Objects.FirstOrDefault(o => o.Kind == ZKind.Building && o.Owner == Team.Neutral && o.IsProducer), false);
            Add("rock (unowned)", w.Objects.FirstOrDefault(o => o.Kind == ZKind.MapItem && o.Type == "rock"), false);
            return c;
        }

        /// <summary>One settled state of the rule (a new match: the end state at once), as the game feeds it.</summary>
        static OzRadarBarsRule Settled(string why, bool owns)
        {
            var r = new OzRadarBarsRule();
            r.Advance(why, owns, Me, 0.016f, Fade, true, 0);
            return r;
        }

        // ------------------------------------------------------------------------------------------------ the whole check
        public static string Run(string settings, string l02, string simDir = null)
        {
            pass = fail = 0; sb = new StringBuilder(); var t0 = DateTime.UtcNow;
            try { R(settings, l02); D(settings, l02); P(); F(); G(settings, l02); S(simDir); W(settings, l02); }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"run time {(DateTime.UtcNow - t0).TotalSeconds:0.00} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.4 radar bars] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------------------ R
        static void R(string set, string map)
        {
            var w = New(set, map); var rs = Radars(w);
            Check(rs.Count == 2 && rs.All(r => r.Owner == Team.Neutral && !r.Destroyed) && !OwnsRadar(w, Me) && !OwnsRadar(w, Foe),
                  $"R1 level 2 starts with {rs.Count} radars, all neutral and standing: no side owns one ({TruthName}), so every match opens with no enemy bars");
            if (rs.Count < 2) return;
            ZObject a = rs[0], b = rs[1]; ZObject fa = FlagOf(w, a), fb = FlagOf(w, b);
            Check(fa != null && fb != null && fa != fb, $"R2 each radar has its own zone flag (radar#{a.Id} flag#{fa?.Id}, radar#{b.Id} flag#{fb?.Id})");
            if (fa == null || fb == null) return;
            w.AwardZone(fa, Me);
            Check(a.Owner == Me && OwnsRadar(w, Me) && !OwnsRadar(w, Foe), "R3 radar#" + a.Id + "'s zone given to red (ZWorld.AwardZone, as a robot at the flag): red owns a radar, blue none");
            Ruin(w, a);
            Check(a.Destroyed && a.Owner == Me && !OwnsRadar(w, Me), "R4 that radar ruined (health 0, ZWorld.UpdateObjectHealth): still red's, a ruin: red owns no radar");
            w.AwardZone(fb, Foe);
            Check(OwnsRadar(w, Foe) && !OwnsRadar(w, Me), "R5 the other radar taken by blue: blue owns one, red none");
            w.AwardZone(fb, Me);
            Check(OwnsRadar(w, Me) && !OwnsRadar(w, Foe), "R6 that radar taken by red while the first is a ruin: red owns one again");
        }

        // ------------------------------------------------------------------------------------------------ D
        static void D(string set, string map)
        {
            var w = New(set, map); var cast = Cast(w);
            Check(cast.Count == 7, $"D0 the table's objects on level 2: {string.Join(", ", cast.Select(c => c.name + " " + c.o.Type + "#" + c.o.Id + " (" + c.o.Owner + ")"))}");
            bool human = w.IsHumanTeam(Me);
            var cpu = New(set, map, true, true); bool cpuHuman = cpu.IsHumanTeam(Me);
            var rules = new (string name, string why)[]
            {
                ("the rule on", OzRadarBarsRule.WhyOff(true, false, false, human)),
                ("DV33's switch off", OzRadarBarsRule.WhyOff(false, false, false, human)),
                ("the Unit editor", OzRadarBarsRule.WhyOff(true, true, false, human)),
                ("'Zod exact'", OzRadarBarsRule.WhyOff(true, false, true, human)),
                ("a computer match (both CPUs)", OzRadarBarsRule.WhyOff(true, false, false, cpuHuman)),
            };
            Check(human && !cpuHuman && rules[0].why == null && rules.Skip(1).All(x => x.why != null),
                  $"D1 a human side in a red-vs-CPU world ({human}), none with both CPUs ({!cpuHuman}): the rule acts only in the first; off reasons: {string.Join("; ", rules.Skip(1).Select(x => x.why))}");
            int k = 1;
            foreach (var (name, why) in rules)
                foreach (bool radar in new[] { false, true })
                {
                    var r = Settled(why, radar);
                    var bad = new List<string>(); var shown = new List<string>();
                    foreach (var c in cast)
                    {
                        bool hide = why == null && !radar && c.enemy;
                        float a = r.Alpha(c.o.Owner); bool h = r.Hides(c.o.Owner);
                        if (a != (hide ? 0f : 1f) || h != hide) bad.Add($"{c.name} {a:0.###}/{h}");
                        shown.Add($"{c.name} {a:0}");
                    }
                    k++;
                    Check(bad.Count == 0, $"D{k} {name}, {(radar ? "a radar owned" : "no radar")}: {string.Join(", ", shown)}{(bad.Count > 0 ? " WRONG: " + string.Join(", ", bad) : "")}");
                }
            var blue = new OzRadarBarsRule(); blue.Advance(null, false, Foe, 0.016f, Fade, true, 0);
            Check(blue.Alpha(Me) == 0f && blue.Alpha(Foe) == 1f && blue.Alpha(Team.Neutral) == 1f,
                  "D" + (++k) + " PLAY AS BLUE (the player blue, no radar): red's bars held back, blue's own and the neutral ones shown");
        }

        // ------------------------------------------------------------------------------------------------ P
        static void P()
        {
            var cases = new (string name, Team owner, bool radar, string want)[]
            {
                ("own unit, no radar", Me, false, "HEALTH"), ("own unit, a radar", Me, true, "HEALTH"),
                ("enemy, no radar", Foe, false, "HEALTH · NEEDS RADAR"), ("enemy, a radar", Foe, true, "HEALTH"),
                ("neutral (empty vehicle), no radar", Team.Neutral, false, "HEALTH"),
            };
            var got = new List<string>(); bool ok = true;
            foreach (var c in cases)
            {
                string l = OzRadarBarsRule.PanelLabel(Settled(null, c.radar).BarOff(c.owner));
                ok &= l == c.want; got.Add(c.name + ": " + l);
            }
            Check(ok, "P1 the unit panel's label: " + string.Join("; ", got));
            string off = OzRadarBarsRule.PanelLabel(Settled(OzRadarBarsRule.WhyOff(false, false, false, true), false).BarOff(Foe));
            Check(off == "HEALTH", "P2 with DV33's switch off an enemy shown without a radar reads " + off);
            const string hero = "HEALTH · SELF-REPAIR 20%/MIN", heroShort = "SELF-REPAIR 20%/MIN";                // UI/OpenZHud.HeroRegen.cs: the label that already fits there, and its short form
            string lab = OzRadarBarsRule.LabelNeedsRadar, labShort = OzRadarBarsRule.LabelNeedsRadarShort;
            Check(lab.Length < hero.Length && labShort.Length < heroShort.Length && lab == OzRadarBarsRule.LabelHealth + " · " + labShort,
                  $"P3 the label ({lab.Length} characters) is shorter than the hero's long label ({hero.Length}) and its short form \"{labShort}\" ({labShort.Length}) shorter than the hero's short one ({heroShort.Length}); "
                  + "the unit panel measures the long one once with the real font against the hero line's own room (HeroRegenLabelMaxW = OzLayout.UnitHpValueRX - 48 - UnitNameX) and takes the short one "
                  + "where it would reach the health value, as the hero line does; the number is hidden with the bar anyway");
            // P4 (round 2): the label goes with the bar, frame by frame (UI/OpenZHud.RadarBars.cs switches it in the frame the bar goes off or comes back,
            // not at the radar change while the bar still fades). The bar is off from factor Hidden on, so a fade out leaves HEALTH up for the part of
            // the 0.35 s above it, and a fade in shows HEALTH from its first drawn frame.
            float pOff = InvEase(OzRadarBarsRule.Hidden);
            var p4 = new List<string>(); bool p4ok = true;
            foreach (float fps in new[] { 60f, 25f })
            {
                var r = Settled(null, true); double now = 0; int lost = -1, gained = -1, offAt = -1, onAt = -1, n = (int)(4f * fps); bool same = true;
                for (int i = 0; i < n; i++)
                {
                    now += 1.0 / fps;
                    int ch = r.Advance(null, i < fps * 0.5f || i >= fps * 2f, Me, 1f / fps, Fade, false, now);
                    bool needs = OzRadarBarsRule.PanelLabel(r.BarOff(Foe)) == OzRadarBarsRule.LabelNeedsRadar, bar = r.Alpha(Foe) > OzRadarBarsRule.Hidden;
                    same &= needs == !bar && OzRadarBarsRule.PanelLabel(r.BarOff(Me)) == OzRadarBarsRule.LabelHealth && OzRadarBarsRule.PanelLabel(r.BarOff(Team.Neutral)) == OzRadarBarsRule.LabelHealth;
                    if (ch == OzRadarBarsRule.RadarLost) lost = i; else if (ch == OzRadarBarsRule.RadarGained) gained = i;
                    if (lost >= 0 && gained < 0 && needs && offAt < 0) offAt = i;
                    if (gained >= 0 && !needs && onAt < 0) onAt = i;
                }
                float tOff = (offAt - lost + 1) / fps, tOn = (onAt - gained + 1) / fps, eOff = Fade * (1f - pOff), eOn = Fade * pOff, slack = 1f / fps + 1e-4f;   // the change's own frame is the fade's first step
                bool ok4 = same && lost >= 0 && gained > lost && offAt > lost && onAt >= gained && Math.Abs(tOff - eOff) <= slack && Math.Abs(tOn - eOn) <= slack;
                p4ok &= ok4;
                p4.Add($"{fps:0} fps: NEEDS RADAR exactly while the enemy bar is off on all {n} frames, HEALTH for own and neutral ({same}); after the loss was followed HEALTH stayed {tOff:0.000} s "
                       + $"while the bar faded out (expected {eOff:0.000} s: the bar is off from factor {OzRadarBarsRule.Hidden} on), after the gain HEALTH came back at {tOn:0.000} s, the bar's first drawn frame (expected {eOn:0.000} s)");
            }
            Check(p4ok, "P4 the label and the bar switch in the same frame: " + string.Join("; ", p4));
        }

        /// <summary>The progress at which smootherstep reaches k (bisection).</summary>
        static float InvEase(float k)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 40; i++) { float m = (lo + hi) * 0.5f; if (OzRadarBarsRule.Ease(m) < k) lo = m; else hi = m; }
            return (lo + hi) * 0.5f;
        }

        // ------------------------------------------------------------------------------------------------ F
        /// <summary>Frames at a fixed rate: K and the change of every frame, the game clock (Now), the first held change (Commit, an index).</summary>
        sealed class FrameRun { public readonly List<float> K = new List<float>(); public readonly List<int> Ch = new List<int>(); public double Now; public int Commit = -1; }

        /// <summary>frames frames at fps: the truth of frame i from owns(i), the game clock advancing speed game seconds a real second.</summary>
        static FrameRun Frames(OzRadarBarsRule r, Func<int, bool> owns, float fps, int frames, double now0 = 0, double speed = 1, FrameRun into = null)
        {
            var run = into ?? new FrameRun { Now = now0 };
            for (int i = 0; i < frames; i++)
            {
                run.Now += speed / fps;
                int ch = r.Advance(null, owns(i), Me, 1f / fps, Fade, false, run.Now);
                run.K.Add(r.K); run.Ch.Add(ch);
                if (run.Commit < 0 && (ch == OzRadarBarsRule.RadarGained || ch == OzRadarBarsRule.RadarLost)) run.Commit = run.K.Count - 1;
            }
            return run;
        }
        /// <summary>The time (s) until K first reaches level, the first sample counted as one frame.</summary>
        static float TimeTo(List<float> ks, float fps, float level, bool up) { for (int i = 0; i < ks.Count; i++) if (up ? ks[i] >= level : ks[i] <= level) return (i + 1) / fps; return float.NaN; }
        static float Bound(float fps) => 1.875f * (1f / fps) / Fade + 1e-4f;
        static bool Steps(List<float> ks, float fps, float start)
        {
            float last = start;
            foreach (var k in ks) { if (Math.Abs(k - last) > Bound(fps)) return false; last = k; }
            return true;
        }

        static void F()
        {
            float e0 = OzRadarBarsRule.Ease(0f), e25 = OzRadarBarsRule.Ease(0.25f), e5 = OzRadarBarsRule.Ease(0.5f), e1 = OzRadarBarsRule.Ease(1f);
            Check(e0 == 0f && e5 == 0.5f && e1 == 1f && Math.Abs(e25 - 0.103515625f) < 1e-7f && OzRadarBarsRule.Ease(-1f) == 0f && OzRadarBarsRule.Ease(2f) == 1f && OzRadarBarsRule.Ease(float.NaN) == 0f,
                  $"F1 smootherstep 6t^5-15t^4+10t^3: 0 -> {e0}, 1/4 -> {e25:0.000000000} (0.103515625), 1/2 -> {e5}, 1 -> {e1}; out of range and NaN clamped");
            bool mono = true, sym = true; float prev = -1f, worst = 0f, slope = 0f;
            for (int i = 0; i <= 10000; i++)
            {
                float t = i / 10000f, e = OzRadarBarsRule.Ease(t);
                if (e < prev) mono = false; prev = e;
                float d = Math.Abs(OzRadarBarsRule.Ease(1f - t) - (1f - e)); if (d > worst) worst = d;
                if (i > 0) slope = Math.Max(slope, (e - OzRadarBarsRule.Ease((i - 1) / 10000f)) * 10000f);
            }
            sym = worst < 1e-6f;
            Check(mono && sym && slope <= 1.877f && OzRadarBarsRule.Ease(0.01f) < 1e-5f && 1f - OzRadarBarsRule.Ease(0.99f) < 1e-5f,
                  $"F2 monotonic over 10001 samples, symmetric (worst {worst:0.0e0}), steepest {slope:0.0000} per unit (15/8 = 1.875 at the middle, measured on float samples), flat at both ends");

            foreach (float fps in new[] { 60f, 25f })
            {
                var r = Settled(null, false);
                Check(r.K == 0f && r.P == 0f, $"F3 {fps:0} fps: the match starts with no radar: K = {r.K} (no bar drawn, no fade at the start)");
                var up = Frames(r, i => true, fps, (int)(fps * (Hold + 0.8)));
                int c = up.Commit;
                bool flat = c > 0 && up.K.Take(c).All(x => x == 0f) && up.Ch.Take(c).All(x => x == None);
                float hold = c / fps;                                                                       // from the frame the truth changed (frame 0) to the frame the rule follows it
                var fin = up.K.Skip(Math.Max(0, c)).ToList();
                float tHalf = TimeTo(fin, fps, 0.5f, true), tEnd = TimeTo(fin, fps, 1f, true), tFirst = TimeTo(fin, fps, OzRadarBarsRule.Hidden + 1e-6f, true);
                bool upMono = true; float worstStep = 0f, last = 0f;
                foreach (var kk in fin) { if (kk < last) upMono = false; worstStep = Math.Max(worstStep, kk - last); last = kk; }
                Check(flat && Math.Abs(hold - Hold) <= 1f / fps + 1e-4f && upMono && Math.Abs(tEnd - Fade) <= 1f / fps + 1e-4f && Math.Abs(tHalf - Fade / 2f) <= 1f / fps + 1e-4f && worstStep <= Bound(fps),
                      $"F4 {fps:0} fps, a radar gained: nothing moves for {hold:0.000} s of game time (the {Hold:0.0} s hold, K 0 and no change on all {c} frames), then the bars fade in over {tEnd:0.000} s (0.35 s within a frame), half at {tHalf:0.000} s, first drawn at {tFirst:0.000} s, monotonic, the largest step {worstStep:0.0000} (slope bound {Bound(fps):0.0000})");
                var down = Frames(r, i => false, fps, (int)(fps * (Hold + 0.8)), up.Now);
                int cd = down.Commit;
                var fout = down.K.Skip(Math.Max(0, cd)).ToList();
                float tGone = TimeTo(fout, fps, 0f, false), holdD = cd / fps;
                bool flatD = cd > 0 && down.K.Take(cd).All(x => x == 1f);
                bool mirror = true; for (int i = 0; i < Math.Min(fin.Count, fout.Count); i++) if (Math.Abs(fout[i] - (1f - fin[i])) > 1e-4f) mirror = false;
                Check(flatD && Math.Abs(holdD - Hold) <= 1f / fps + 1e-4f && Math.Abs(tGone - Fade) <= 1f / fps + 1e-4f && mirror && down.K[down.K.Count - 1] == 0f,
                      $"F5 {fps:0} fps, the radar lost: held for {holdD:0.000} s (K 1 on all {cd} frames), then they fade out over {tGone:0.000} s, the mirror of the fade in, and end at exactly 0 (the bar not drawn)");
            }

            // a short loss half way through the fade in: not followed (the hold); the fade in ends, then a real loss fades out from 1, no jump anywhere
            var m = Settled(null, false); var run = new FrameRun();
            while (run.Commit < 0 && run.K.Count < 600) Frames(m, i => true, 60f, 1, 0, 1, run);
            Frames(m, i => true, 60f, 10, 0, 1, run); float kMid = m.K;
            var back = Frames(m, i => false, 60f, (int)(60 * (Hold + 0.8)), run.Now);
            float kNext = back.K[0], topBefore = back.Commit > 0 ? back.K.Take(back.Commit).Max() : -1f;
            Check(kMid > 0.2f && kMid < 0.9f && kNext > kMid && topBefore == 1f && Math.Abs(back.Commit / 60f - Hold) <= 1f / 60f + 1e-4f && Steps(back.K, 60f, kMid) && back.K[back.K.Count - 1] == 0f,
                  $"F6 the radar lost again half way through the fade in (K {kMid:0.000}): the fade goes on (next frame {kNext:0.000}, up to {topBefore:0.000}), the loss counts after {back.Commit / 60f:0.000} s and fades out from there; every step within the slope bound, no jump");
            var s = Settled(null, true); var sr = new FrameRun();
            while (sr.Commit < 0 && sr.K.Count < 600) Frames(s, i => false, 60f, 1, 0, 1, sr);
            Frames(s, i => false, 60f, 10, 0, 1, sr); float sMid = s.K;
            s.Advance(OzRadarBarsRule.WhyOff(false, false, false, true), false, Me, 1f / 60f, Fade, false, sr.Now + 1 / 60.0); float sNext = s.K;
            Check(sMid > 0.1f && sMid < 0.8f && sNext > sMid && sNext - sMid <= Bound(60f),
                  $"F6b DV33's switch turned off half way through a fade out (K {sMid:0.000}): at once, no hold, and the fade turns from where it is (next frame {sNext:0.000})");

            var p = Settled(null, false); double tp = 0; int guard = 0;
            while (!p.Owns && guard++ < 200) { tp += 1 / 60.0; p.Advance(null, true, Me, 0f, Fade, false, tp); }   // the hold with 0 s frames: P stays where it is
            float p0 = p.P;
            p.Advance(null, true, Me, 5f, Fade, false, tp); float afterLong = p.P;
            p.Advance(null, true, Me, 0f, Fade, false, tp); float afterZero = p.P;
            p.Advance(null, true, Me, float.NaN, Fade, false, tp); float afterNan = p.P;
            p.Advance(null, true, Me, -1f, Fade, false, tp); float afterNeg = p.P;
            Check(p.Owns && p0 == 0f && Math.Abs(afterLong - OzRadarBarsRule.MaxStep / Fade) < 1e-5f && afterZero == afterLong && afterNan == afterLong && afterNeg == afterLong,
                  $"F7 a pause: the first frame after 5 s paused moves the fade only {afterLong:0.0000} (0.1 s worth), a 0 s frame, a NaN and a negative frame do not move it");
            var q = Settled(null, false);
            q.Advance(null, true, Me, 1f / 60f, Fade, false, 10.0);                                          // the radar taken at game time 10.0 s
            for (int i = 0; i < 120; i++) q.Advance(null, true, Me, 1f / 60f, Fade, false, 10.3);           // the pause menu: the sim clock stands at 10.3 s for 2 s of real time
            bool held = !q.Owns && q.Pending && q.K == 0f;
            q.Advance(null, true, Me, 1f / 60f, Fade, false, 10.59); bool notYet = !q.Owns;
            int qc = q.Advance(null, true, Me, 1f / 60f, Fade, false, 10.61);
            Check(held && notYet && qc == OzRadarBarsRule.RadarGained && q.K > 0f && q.K < 0.01f,
                  $"F7b the hold runs on game time: a radar taken just before the pause is not followed during 2 s of pause (the sim clock stopped), at 0.59 s of game time not yet, at 0.61 s it is (K {q.K:0.0000} on that frame, the fade begins)");

            var n = Settled(null, true); var nr = new FrameRun();
            while (nr.Commit < 0 && nr.K.Count < 600) Frames(n, i => false, 60f, 1, 0, 1, nr);
            Frames(n, i => false, 60f, 3, 0, 1, nr); float mid = n.K;
            n.Advance(null, false, Me, 0.016f, Fade, true, nr.Now);
            Check(mid > 0f && mid < 1f && n.K == 0f && n.P == 0f && !n.Pending, $"F8 a new match (fresh) snaps: mid-fade K {mid:0.000} -> {n.K} at once (a match never starts with a fade)");

            var o = Settled(null, false); o.Advance("the Unit editor", false, Me, 1f / 60f, Fade, false, 1 / 60.0);
            float kOff1 = o.K; for (int i = 0; i < 30; i++) o.Advance("the Unit editor", false, Me, 1f / 60f, Fade, false, (i + 2) / 60.0);
            Check(kOff1 > 0f && kOff1 < 0.1f && o.K == 1f && o.Alpha(Foe) == 1f && !o.RuleOn,
                  $"F9 the rule switched off during a match: at once (no hold), the enemy bars fade back in (first frame {kOff1:0.0000}, after 0.5 s {o.K}), no pop either way");
            Check(OzRadarBarsRule.Ease(0f) <= OzRadarBarsRule.Hidden && OzRadarBarsRule.Ease(OzRadarBarsRule.MaxStep / Fade) > OzRadarBarsRule.Hidden,
                  $"F10 at factor 0 the bar is not drawn (Hidden {OzRadarBarsRule.Hidden}); one frame of 0.1 s into a fade it is ({OzRadarBarsRule.Ease(OzRadarBarsRule.MaxStep / Fade):0.000})");

            // a radar flag both sides stand on: Sim/ZWorld.Rules.cs CheckFlagCaptures gives it to the first unit of the other team every 0.2 s
            var c1 = Settled(null, false);
            var con = Frames(c1, i => (i / 12) % 2 == 0, 60f, 312);                                         // 26 owners in 5.2 s (a new one every 12 frames = 0.2 s), the last the enemy's
            bool still = con.K.All(x => x == 0f) && con.Ch.All(x => x == None); int flips1 = c1.Flips;
            var won = Frames(c1, i => true, 60f, (int)(60 * (Hold + 0.6)), con.Now);
            Check(still && flips1 == 26 && Math.Abs(won.Commit / 60f - Hold) <= 1f / 60f + 1e-4f && won.K[won.K.Count - 1] == 1f,
                  $"F11 a contested radar flag, no radar before: its owner changes every 0.2 s for 5.2 s ({flips1} changes of the truth): K stays exactly 0 on all {con.K.Count} frames, no change is followed (no pulse); "
                  + $"the player then holds it: followed after {won.Commit / 60f:0.000} s, the bars fade in");
            var c2 = Settled(null, true);
            var con2 = Frames(c2, i => (i / 12) % 2 == 1, 60f, 312);
            var rnd = new Random(1405); var c3 = Settled(null, false); bool r3 = true; int seg = 0, segs = 0; bool cur = false;
            var con3 = new FrameRun();
            for (int t = 0; t < 40; t++)
            {
                int len = 12 + rnd.Next(0, 23);                                                            // 0.20 .. 0.57 s, every gap under the 0.6 s hold
                cur = !cur; segs++;
                bool v = cur; Frames(c3, i => v, 60f, len, 0, 1, con3); seg += len;
            }
            r3 = con3.K.All(x => x == 0f) && con3.Ch.All(x => x == None);
            Check(con2.K.All(x => x == 1f) && con2.Ch.All(x => x == None) && r3,
                  $"F11b the same with the radar owned before: K stays exactly 1 on all {con2.K.Count} frames; and {segs} owners at random gaps of 0.20 to 0.57 s ({seg / 60f:0.0} s, the truth changed {c3.Flips} times): K stays 0, nothing followed");
            // the log's pace (View/OzRadarBars.cs writes its lines through it): the match's line, then a burst of 30 changes in 1.5 s, then quiet
            var lp = new OzRadarBarsRule.LogPace(1f); var at = new List<float>();
            if (lp.Change(0f, true)) { at.Add(0f); lp.Wrote(0f); }
            for (int i = 1; i <= 30; i++) { float tt = i * 0.05f; if (lp.Change(tt, false)) { at.Add(tt); lp.Wrote(tt); } }
            int heldEnd = lp.Held;
            for (int i = 1; i <= 180; i++) { float tt = 1.5f + i / 60f; if (lp.Quiet(tt)) { at.Add(tt); lp.Wrote(tt); } }
            bool apart = true; for (int i = 1; i < at.Count; i++) if (at[i] - at[i - 1] < 1f - 1e-4f) apart = false;
            Check(at.Count == 3 && apart && heldEnd > 0 && lp.Held == 0 && !lp.Quiet(9f),
                  $"F12 the log's pace: the match's line and a burst of 30 changes in 1.5 s give {at.Count} lines (at {string.Join(", ", at.Select(x => x.ToString("0.00")))} s: at most one a second, "
                  + $"the {heldEnd} changes held at the end of the burst named in one line once the second was over), then nothing more");
            var fast = Frames(Settled(null, false), i => true, 60f, 60, 0, 4);
            var slow = Frames(Settled(null, false), i => true, 60f, 120, 0, 0.5);
            Check(Math.Abs(fast.Commit / 60f - Hold / 4) <= 1f / 60f + 1e-4f && Math.Abs(slow.Commit / 60f - Hold / 0.5) <= 1f / 60f + 1e-4f,
                  $"F13 the hold is game time (three of the sim's 0.2 s capture checks at any speed): at game speed 4 it is {fast.Commit / 60f:0.000} s of real time, at 0.5 {slow.Commit / 60f:0.000} s; the fade after it stays 0.35 s of real time");
        }

        // ------------------------------------------------------------------------------------------------ G
        static readonly Func<long> Allocated = MakeAllocated();
        static Func<long> MakeAllocated()
        {
            var mi = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            return mi == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), mi);
        }
        /// <summary>Round 2: the thread counter sees a known allocation (a 4 KB array made now). Unity's own Boehm Mono (the editor) and IL2CPP
        /// return 0 there (View/OzPerfNumbers.cs:4-9), so a garbage row on them would read 0 bytes whatever the code made: such a row is written
        /// as INFO "not measurable", not as PASS (the Play proof's Q1g / Q2 count it with the engine's own counter instead).</summary>
        static bool Counts(out long seen)
        {
            seen = 0;
            if (Allocated == null) return false;
            long a0 = Allocated(); var probe = new byte[4096]; seen = Allocated() - a0; GC.KeepAlive(probe);
            return seen >= 4096;
        }
        static string NotMeasurable(string row, long seen) => $"{row} not measurable on this runtime: its thread counter (GC.GetAllocatedBytesForCurrentThread{(Allocated == null ? ", missing" : "")}) saw {seen} bytes of a 4 KB array "
                                                             + "(Unity's Boehm Mono returns 0, View/OzPerfNumbers.cs:6): no PASS here; the Play proof's Q1g and Q2 count the garbage with the engine's own \"GC Allocated In Frame\"";

        static void G(string set, string map)
        {
            var w = New(set, map); var cast = Cast(w).Select(c => c.o).ToArray(); var r = new OzRadarBarsRule();
            var fa = FlagOf(w, Radars(w).First()); float sum = 0f; int hides = 0;
            void Frame(int i)
            {
                bool owns = OwnsRadar(w, Me);
                r.Advance(OzRadarBarsRule.WhyOff(true, false, false, w.IsHumanTeam(Me)), owns, Me, 1f / 60f, Fade, i == 0, i / 60.0);
                for (int j = 0; j < cast.Length; j++) { sum += r.Alpha(cast[j].Owner); if (r.Hides(cast[j].Owner)) hides++; }
            }
            for (int i = 0; i < 50; i++) Frame(i);                                                           // warm up (the JIT)
            // the cost of the radar truth: no radar = the whole object list (the worst case, every level's start)
            for (int i = 0; i < 2000; i++) OwnsRadar(w, Me);
            var sw = System.Diagnostics.Stopwatch.StartNew(); int yes = 0;
            for (int i = 0; i < 20000; i++) if (OwnsRadar(w, Me)) yes++;
            double us = sw.Elapsed.TotalMilliseconds * 1000.0 / 20000.0;
            Check(yes == 0 && us < 50.0, $"G2 the radar truth's cost on level 2 ({w.Objects.Count} objects, no radar: the whole list, the worst case): {us:0.00} microseconds a call (bound 50); "
                                         + $"asked once a frame by this rule and once by the strike warning: {2 * us / 16667.0 * 100.0:0.000} % of a 60 fps frame for both");
            if (!Counts(out long seen)) { Info(NotMeasurable("G1", seen)); return; }
            long b0 = Allocated(); for (int i = 1; i <= 300; i++) Frame(i); long noRadar = Allocated() - b0;
            w.AwardZone(fa, Me); Frame(1); Frame(2);
            long b1 = Allocated(); for (int i = 1; i <= 300; i++) Frame(i); long withRadar = Allocated() - b1;
            Check(noRadar == 0 && withRadar == 0, $"G1 300 frames of the radar truth on level 2 ({w.Objects.Count} objects), the rule (with its hold) and {cast.Length} bars a frame: {noRadar} bytes without a radar, {withRadar} with one (checksum {sum:0} / {hides})");
        }

        // ------------------------------------------------------------------------------------------------ S
        static void S(string simDir)
        {
            if (simDir == null || !Directory.Exists(simDir)) { Info("S1 Scripts/Sim not found here: no source scan"); return; }
            var hits = Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories).Where(f => File.ReadAllText(f).Contains("OzRadarBars")).Select(Path.GetFileName).ToList();
            Check(hits.Count == 0, $"S1 no file of Scripts/Sim ({Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories).Length} files) names the rule: the rules and the computer cannot read it{(hits.Count > 0 ? ": " + string.Join(", ", hits) : "")}");
        }

        // ------------------------------------------------------------------------------------------------ W
        static void W(string set, string map)
        {
#if !OPENZ_HARNESS
            var w = New(set, map); var cpu = New(set, map, true, true);
            bool rule0 = OpenZMinimap.RadarRule;
            try
            {
                OpenZMinimap.RadarRule = true;
                string on = OzRadarBars.WhyOffNow(w, Me, false, false), pg = OzRadarBars.WhyOffNow(w, Me, true, false), zx = OzRadarBars.WhyOffNow(w, Me, false, true), cm = OzRadarBars.WhyOffNow(cpu, Me, false, false);
                OpenZMinimap.RadarRule = false;
                string sw = OzRadarBars.WhyOffNow(w, Me, false, false);
                Check(on == null && pg == OzRadarBarsRule.WhyOff(true, true, false, true) && zx == OzRadarBarsRule.WhyOff(true, false, true, true) && cm == OzRadarBarsRule.WhyOff(true, false, false, false) && sw == OzRadarBarsRule.WhyOff(false, false, false, true) && !OzUeHooks.Active,
                      $"W1 the game's wiring (OzRadarBars.WhyOffNow): on in a red-vs-CPU match; off: the playground ({pg}), 'Zod exact' ({zx}), both CPUs ({cm}), OpenZMinimap.RadarRule false = DV33's switch ({sw})");
            }
            finally { OpenZMinimap.RadarRule = rule0; }
            var cast = Cast(w);
            ZObject own = cast.First(c => c.name == "own grunt").o, foe = cast.First(c => c.name == "enemy grunt").o, jeep = cast.First(c => c.name.StartsWith("empty jeep")).o;
            try
            {
                OzRadarBars.Live.Advance(null, false, Me, 0.016f, OzRadarBars.FadeSeconds, true, 0);
                bool hidden = OzRadarBars.BarAlpha(foe) == 0f && OzRadarBars.Hides(foe) && OzRadarBars.BarAlpha(own) == 1f && OzRadarBars.BarAlpha(jeep) == 1f && OzRadarBars.BarAlpha(null) == 1f && !OzRadarBars.Hides(own)
                              && OzRadarBars.BarOff(foe) && !OzRadarBars.BarOff(own) && !OzRadarBars.BarOff(jeep) && !OzRadarBars.BarOff(null);
                OzRadarBars.Live.Advance(null, true, Me, 0.016f, OzRadarBars.FadeSeconds, true, 0);
                bool shown = OzRadarBars.BarAlpha(foe) == 1f && !OzRadarBars.Hides(foe) && !OzRadarBars.BarOff(foe);
                Check(hidden && shown, "W2 OzRadarBars.BarAlpha / Hides / BarOff (what the overlay and the unit panel ask) read the game's one state: enemy 0, hidden and its bar off without a radar, own / empty jeep / none 1 and on; with a radar the enemy 1 and on");
                int e0 = OzRadarBars.EnemyShown, h0 = OzRadarBars.EnemyHidden;
                OzRadarBars.Note(foe, true); OzRadarBars.Note(foe, false); OzRadarBars.Note(own, false); OzRadarBars.Note(jeep, false);
                Check(OzRadarBars.EnemyShown - e0 == 1 && OzRadarBars.EnemyHidden - h0 == 1, "W3 the Play check's counters count enemy marks only");
            }
            finally { OzRadarBars.Live.Reset(); OzRadarBars.EnemyShown = OzRadarBars.EnemyHidden = 0; }
            bool f1 = OzRadarBars.NewFrame(987654), f2 = OzRadarBars.NewFrame(987654), f3 = OzRadarBars.NewFrame(987655);
            OzRadarBars.NewFrame(-1);
            Check(f1 && !f2 && f3, "W4 the frame cache: the first call of a frame works, the second of the same frame returns at once (the overlay and the unit panel share one tick)");
            Check(Math.Abs(OzRadarBars.FadeSeconds - 0.35f) < 1e-6f, $"W5 the fade's length here: {OzRadarBars.FadeSeconds:0.00} s (CALM MODE {(OzMotion.Reduced ? "on" : "off")}: 0.35 s either way, decision 6)");
            if (OzRadarBars.Gained + OzRadarBars.Lost > 0) Info($"W6 counters from an earlier Play session: gained {OzRadarBars.Gained}, lost {OzRadarBars.Lost}, log lines {OzRadarBars.LogLines}");
            // zero garbage of the game's own per-frame path (the wiring + the shared state), as G1; INFO where the thread counter cannot count (round 2)
            if (!Counts(out long seenW)) Info(NotMeasurable("W7", seenW));
            else
            {
                var objs = cast.Select(c => c.o).ToArray(); float sum = 0f;
                void Frame(int i)
                {
                    string why = OzRadarBars.WhyOffNow(w, Me, false, false);
                    OzRadarBars.Live.Advance(why, why == null && OwnsRadar(w, Me), Me, 1f / 60f, OzRadarBars.FadeSeconds, i == 0, i / 60.0);
                    for (int j = 0; j < objs.Length; j++) { sum += OzRadarBars.BarAlpha(objs[j]); if (OzRadarBars.Hides(objs[j])) sum += 0.5f; if (OzRadarBars.BarOff(objs[j])) sum += 0.25f; }
                }
                try
                {
                    for (int i = 0; i < 50; i++) Frame(i);
                    long b0 = Allocated(); for (int i = 1; i <= 300; i++) Frame(i); long bytes = Allocated() - b0;
                    Check(bytes == 0, $"W7 300 frames of the game's own path (WhyOffNow: DV33's switch, the Unit editor, the human side; the radar truth; the one state with its hold; {objs.Length} bars): {bytes} bytes (checksum {sum:0})");
                }
                finally { OzRadarBars.Live.Reset(); }
            }
#else
            Info("W the game's wiring (OpenZMinimap, OzUeHooks, OzMotion): in the editor's menu only");
#endif
        }

        // ------------------------------------------------------------------------------------------------ Quick (the Deviations row)
        /// <summary>The Deviations row's probe on level 2: the radar truth, the table's corners, the hold and the fade, the label.</summary>
        public static string Quick(string settings, string l02)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                var w = New(settings, l02); var cast = Cast(w);
                var foe = cast.First(c => c.name == "enemy grunt").o; var own = cast.First(c => c.name == "own grunt").o; var jeep = cast.First(c => c.name.StartsWith("empty jeep")).o;
                bool owns0 = OwnsRadar(w, Me);
                var r = Settled(null, owns0);
                Check(!owns0 && r.Alpha(foe.Owner) == 0f && r.Alpha(own.Owner) == 1f && r.Alpha(jeep.Owner) == 1f, "no radar on level 2 at the start: the enemy grunt's bar held back, the own grunt's and the empty jeep's shown");
                w.AwardZone(FlagOf(w, Radars(w).First()), Me);
                bool owns1 = OwnsRadar(w, Me); var up = Frames(r, i => owns1, 60f, 90);
                float tIn = up.Commit > 0 ? TimeTo(up.K.Skip(up.Commit).ToList(), 60f, 1f, true) : float.NaN;
                Check(owns1 && Math.Abs(up.Commit / 60f - Hold) <= 1f / 60f + 1e-4f && Math.Abs(tIn - Fade) <= 1f / 60f + 1e-4f && r.Alpha(foe.Owner) == 1f,
                      $"a radar given to red: followed after the {Hold:0.0} s hold ({up.Commit / 60f:0.000} s), the enemy bar fades in over {tIn:0.000} s (smootherstep)");
                var off = Settled(OzRadarBarsRule.WhyOff(true, false, true, true), false);
                Check(off.Alpha(foe.Owner) == 1f && !off.Hides(foe.Owner), "'Zod exact' (as the Zod Engine): every bar shown without a radar");
                Check(OzRadarBarsRule.PanelLabel(Settled(null, false).BarOff(foe.Owner)) == "HEALTH · NEEDS RADAR" && OzRadarBarsRule.PanelLabel(Settled(null, false).BarOff(own.Owner)) == "HEALTH", "the unit panel's label for an enemy without a radar: HEALTH · NEEDS RADAR");
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.4 radar bars quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.4/Radar Enemy Bars: Count (headless, DV172)")]
        static void Menu() => OzV11Kit.RunMenu("radar enemy bars: count (DV172)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText("L02"), Path.Combine(UnityEngine.Application.dataPath, "OpenZ", "Scripts", "Sim")));

        [UnityEditor.MenuItem("OpenZ/V1.4/Radar Enemy Bars: Play Proof (a level playing, about 1.5 minutes)")]
        static void PlayMenu()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.4 radar bars] start a level in Play first (L02 best: two neutral radars), wait for the briefing to end"); return; }
            OzRadarBarsProof.Run();
            UnityEngine.Debug.Log("[OpenZ V1.4 radar bars] Play proof started (about 1.5 minutes, real mouse); its report: " + OzRadarBarsProof.LogPath);
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/Radar Enemy Bars: Status (Play)")]
        static void Status() => UnityEngine.Debug.Log($"[OpenZ V1.4 radar bars] rule {(OzRadarBars.RuleOn ? "ON" : "off (" + OzRadarBars.Why + ")")}, radar owned {OzRadarBars.Owns} (the truth now {OzRadarBars.Raw}, a change waiting {OzRadarBars.Pending}), factor {OzRadarBars.Factor:0.000}, "
                                                      + $"gained {OzRadarBars.Gained}, lost {OzRadarBars.Lost}, the truth changed {OzRadarBars.Flips} times, log lines {OzRadarBars.LogLines}, enemy marks drawn {OzRadarBars.EnemyShown} / held back {OzRadarBars.EnemyHidden}, always-bars marks left out {OzRadarBars.SkippedAlways}; proof: {OzRadarBarsProof.Report}");
#endif
    }
}
