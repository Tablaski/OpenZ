using System.Globalization;
using System.Text;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// no rule. One line per fact, [OpenZ V1.4 laser beam] PASS n/n.
    /// Headless (menu OpenZ/V1.4/Laser Beam: Count (headless)), nothing saved:
    ///  S  the drawn speeds: the laser bolt 1.2 x Zod's 37.5 m/s = 45 m/s x game speed (0.5, 1, 2); grunt, psycho and sniper rounds still 37.5 x;
    ///  H  the laser's head time (shot -> impact frame, also BlastWave's throw) at 4, 8, 12 and 17 m: (d - lead - 0.85) / 45, 1 / 1.2 of before;
    ///  W  the bolt as before (2 bolts 0.085 s apart, 1.7 m long), the sleeve 0.42 m wide (0.5 before);
    ///  C  Zod's client fire clock (View/ZodClientFire.cs), 60 seeds x 30 s at 60 fps: every laser shot comes right after a charge step of
    ///     0.05-0.08 s (one charge per shot), and no other kind (rifle, sniper, toggle, pyro) ever reads Charging; the clock's mean period 0.529 s.
    /// In Play (menu OpenZ/V1.4/Laser Beam: Play Count, a level playing with laser robots firing): the runner's counts, the drawn laser bolts'
    /// mean speed 45 x game speed, charges seen when shots are seen.
    /// </summary>
    public static class OzV14LaserBeamCount
    {
        const string Tag = "[OpenZ V1.4 laser beam] ";
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        static string F(double v, string f = "0.000") => v.ToString(f, CultureInfo.InvariantCulture);

        public const float Mul = 1.2f, Zod = 300f / 8f, Sleeve = 0.42f;

        /// <summary>The headless count; returns the report (first line PASS n/n or FAIL f of n).</summary>
        public static string Run()
        {
            pass = fail = 0; sb = new StringBuilder();
            // S
            Check(Mathf.Abs(WeaponsV2Runner.LaserSpeedMul - Mul) < 1e-6f, "S  LaserSpeedMul " + F(WeaponsV2Runner.LaserSpeedMul) + " (want 1.2)");
            foreach (var gs in new[] { 0.5f, 1f, 2f })
            {
                float laser = WeaponsV2Runner.RoundSpeed("laser", gs);
                Check(Mathf.Abs(laser - Zod * Mul * gs) < 1e-3f, "S  laser bolt at game speed " + F(gs, "0.0") + ": " + F(laser, "0.00") + " m/s (want " + F(Zod * Mul * gs, "0.00") + ")");
                foreach (var t in new[] { "grunt", "psycho", "sniper" })
                {
                    float v = WeaponsV2Runner.RoundSpeed(t, gs);
                    Check(Mathf.Abs(v - Zod * gs) < 1e-3f, "S  " + t + " round at game speed " + F(gs, "0.0") + ": " + F(v, "0.00") + " m/s (Zod's " + F(Zod * gs, "0.00") + ", unchanged)");
                }
            }
            // H
            foreach (var d in new[] { 4f, 8f, 12f, 17f })
            {
                float half = WeaponsV2Logic.LaserBoltLength * 0.5f, lead = Mathf.Min(half + 0.15f, d * 0.5f);
                float want = Mathf.Max(0.02f, (d - lead - half) / (Zod * Mul)), before = Mathf.Max(0.02f, (d - lead - half) / Zod);
                float got = WeaponsV2Runner.HeadTime("laser", d, 1f);
                Check(Mathf.Abs(got - want) < 1e-4f, "H  " + F(d, "0") + " m: impact " + F(got) + " s = " + F(got * 60f, "0.0") + " frames at 60 fps (before " + F(before) + " s = "
                    + F(before * 60f, "0.0") + " frames; " + F((before - got) * 1000f, "0") + " ms sooner)");
            }
            // W
            Check(WeaponsV2Logic.LaserBoltsPerShot == 2 && Mathf.Abs(WeaponsV2Logic.LaserBoltGap - 0.085f) < 1e-6f && Mathf.Abs(WeaponsV2Logic.LaserBoltLength - 1.7f) < 1e-6f,
                "W  double tap as before: " + WeaponsV2Logic.LaserBoltsPerShot + " bolts, " + F(WeaponsV2Logic.LaserBoltGap) + " s apart, " + F(WeaponsV2Logic.LaserBoltLength, "0.00") + " m long");
            Check(Mathf.Abs(WeaponsV2Runner.LaserSleeveWidth - Sleeve) < 1e-6f, "W  sleeve " + F(WeaponsV2Runner.LaserSleeveWidth, "0.00") + " m wide (0.50 before)");
            // C
            int shots = 0, charges = 0, bad = 0, other = 0; double minC = 9, maxC = 0;
            const double dt = 1.0 / 60.0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var c = new ZodClientFire(ZodClientFire.Kind.Laser, seed * 7919 + 17); c.Reset(0);
                bool was = false; double from = 0;
                for (int f = 0; f <= 1800; f++)
                {
                    double now = f * dt; int n = c.Advance(now);
                    if (n > 0) { shots += n; if (!was && n == 1) bad++; else if (was) { double len = now - from; if (len < minC) minC = len; if (len > maxC) maxC = len; } }
                    if (c.Charging && !was) { charges++; from = now; }
                    if (n > 0 && c.Charging) bad++;                                   // a fire frame never leaves the clock in the charge step
                    was = c.Charging;
                }
                foreach (var k in new[] { ZodClientFire.Kind.Rifle, ZodClientFire.Kind.Sniper, ZodClientFire.Kind.Toggle, ZodClientFire.Kind.Pyro })
                {
                    var o = new ZodClientFire(k, seed); o.Reset(0);
                    for (int f = 0; f <= 600; f++) { o.Advance(f * dt); if (o.Charging) other++; }
                }
            }
            Check(shots > 0 && bad == 0 && System.Math.Abs(charges - shots) <= 60, "C  " + shots + " laser shots, " + charges + " charge steps, " + bad + " shots without a charge step just before");
            Check(minC >= 0.05 - dt - 1e-9 && maxC <= 0.08 + dt + 1e-9, "C  charge seen for " + F(minC) + " to " + F(maxC) + " s before the shot (Zod's step 0.050-0.080 s, read at 60 fps: +- one frame)");
            Check(other == 0, "C  Charging read on other kinds (rifle, sniper, toggle, pyro): " + other);
            Check(System.Math.Abs(ZodClientFire.MeanPeriod(ZodClientFire.Kind.Laser) - 0.529) < 0.001, "C  the laser's mean visible shot period " + F(ZodClientFire.MeanPeriod(ZodClientFire.Kind.Laser)) + " s (Zod, unchanged)");
            Info("the sim's laser is untouched: a hit on the roll as Zod's (attack_missile_speed 0), every 0.40 s (attack_speed), 136 px (17 m)");
            return (fail == 0 ? "PASS " + pass + "/" + pass : "FAIL " + fail + " of " + (pass + fail)) + "\n" + sb;
        }

        /// <summary>In Play: the runner's counts since it started.</summary>
        public static string PlayReport()
        {
            var r = WeaponsV2Runner.I; var g = ZGame.I;
            if (!Application.isPlaying || r == null || g == null) return "start a level in Play first and let laser robots fire";
            pass = fail = 0; sb = new StringBuilder();
            float gs = Mathf.Max(0.25f, g.gameSpeed);
            Info("laser shots " + r.LaserShots + ", bolts " + r.LaserBolts + ", charge glows " + r.LaserCharges + ", game speed " + F(gs, "0.00"));
            if (r.DrawnMps.TryGetValue("laser bolt", out var v) && v.y > 0f)
                Check(Mathf.Abs(v.x / v.y - Zod * Mul * gs) < 0.05f && v.z <= Zod * Mul * 2f * 4f, "drawn laser bolts " + F(v.y, "0") + ", mean " + F(v.x / v.y, "0.00") + " m/s (want " + F(Zod * Mul * gs, "0.00") + ")");
            else Check(false, "no laser bolt drawn yet: let laser robots fire, then count again");   // review 6 Nov: nothing counted is not a PASS
            Check(r.LaserShots > 0 && r.LaserCharges > 0, "charge glows seen with the shots: " + r.LaserCharges + " for " + r.LaserShots + " shots (a killing shot and a held shot can come without one)");
            return (fail == 0 ? "PASS " + pass + "/" + pass : "FAIL " + fail + " of " + (pass + fail)) + "\n" + sb;
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/Laser Beam: Count (headless)")]
        static void Menu() => Debug.Log(Tag + Run());

        [UnityEditor.MenuItem("OpenZ/V1.4/Laser Beam: Play Count (a level playing)")]
        static void PlayMenu() => Debug.Log(Tag + PlayReport());
    }
}
