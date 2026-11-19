using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit water_fx_1_5: the look of a water splash (view only: the rules never read it). A splash reads as WATER, never as
    /// thin straight streaks: a thick rounded column of soft lit puffs that rises fast and falls back, broken at the top and sides
    /// into clusters of round droplets that arc and fall, a foam patch on the surface that spreads and fades, and mist drifting off.
    /// Every part is drawn by the game's own particle pipeline (OpenZFxPlayer channels): soft feathered edges, a soft fade where it
    /// meets the water or a bank (depth fade), lit by the scene's light (dark at night, warm near a fire), and drawn out along its
    /// path only while it moves fast (the motion smoothing of every view-facing puff), so a slow droplet stays round.
    /// <list type="bullet">
    /// <item><see cref="Jet"/>: the tall column of a blast in the water (the level 18 briefings' bridge charges); the same heights and
    /// times as the column it replaces.</item>
    /// <item><see cref="Drops"/>: what a piece, a wreck, a turret or a body throws up when it comes down on the water
    /// (OzWaterLanding); the same speeds and times as the droplets it replaces, plus a small crown, the foam and a little mist for
    /// the larger pieces.</item>
    /// </list>
    /// The channels are made the first time a splash asks for them, on the scene's own effect player, and go with it.
    /// <see cref="On"/> false = the splashes as before (a test aid for before / after pictures, never saved).
    /// </summary>
    public static class OzWaterSplash
    {
        /// <summary>Test aid, never saved: false = every caller draws its splash as before.</summary>
        public static bool On = true;
        /// <summary>The white of foam and spray before the scene's light falls on it (an albedo, never a glow).</summary>
        public static Color FoamWhite = new Color(0.86f, 0.88f, 0.86f);
        /// <summary>How far the water's own tone is lifted toward the foam white: the column's body, the droplets, the mist, the foam.</summary>
        public static float BodyWhite = 0.62f, DropWhite = 0.72f, MistWhite = 0.82f, FoamPatchWhite = 0.7f;
        /// <summary>Opacity of the column's body, the droplets, the mist and the foam patch.</summary>
        public static float BodyAlpha = 0.85f, DropAlpha = 0.9f, MistAlpha = 0.22f, FoamAlpha = 0.6f;

        public const string Column = "wsp_column", Drop = "wsp_drop", Mist = "wsp_mist", FoamChannel = "l16_foam";

        /// <summary>Checks: splashes drawn by each entry, particles emitted, the effect player the channels were made on.</summary>
        public static int Jets, PieceSplashes, Emitted;
        static OpenZFxPlayer madeOn;

        /// <summary>The three channels (also read by the counted check): every one a soft lit puff, none a stretched streak.</summary>
        public static FxChannelDef[] Defs()
        {
            var column = new FxChannelDef
            {
                name = Column, shape = FxShape.Puff, capacity = 900, queue = 3007, opaqueness = 1f, tint = Color.white, intensity = 1f, lit = 1f, wrap = 0.5f,
                edgeSoft = 0.5f, softFade = 1.0f, sortByDistance = true, noiseAmt = 0.3f, noiseScale = 1.6f, boil = 0.5f, spin = 0.3f, bump = 0.35f, selfShadow = 0.5f,
                gravity = 9.81f, drag = 0.99f, sizeCurve = FxSizeCurve.Lerp, sizeEnd = 1.5f, alphaCurve = FxAlphaCurve.PopFade,
            };
            var drop = new FxChannelDef
            {
                name = Drop, shape = FxShape.Puff, capacity = 1600, queue = 3007, opaqueness = 1f, tint = Color.white, intensity = 1f, lit = 1f, wrap = 0.5f,
                edgeSoft = 0.45f, softFade = 0.4f, noiseAmt = 0.12f, noiseScale = 1.2f, boil = 0.2f, spin = 0f, bump = 0.25f, selfShadow = 0.2f,
                gravity = 9.81f, drag = 0.995f, sizeCurve = FxSizeCurve.Constant, alphaCurve = FxAlphaCurve.PopFade,
            };
            var mist = new FxChannelDef
            {
                name = Mist, shape = FxShape.Puff, capacity = 600, queue = 3008, opaqueness = 1f, tint = Color.white, intensity = 1f, lit = 1f, wrap = 0.6f,
                edgeSoft = 0.6f, softFade = 1.5f, sortByDistance = true, noiseAmt = 0.4f, noiseScale = 1.8f, boil = 0.6f, spin = 0.3f, bump = 0.2f, selfShadow = 0.3f,
                gravity = -0.15f, drag = 0.96f, sizeCurve = FxSizeCurve.BillowGrow, alphaCurve = FxAlphaCurve.SmokeFade,
            };
            return new[] { column, drop, mist };
        }

        static bool Ready(out OpenZFxPlayer z)
        {
            z = OpenZFxPlayer.I;
            if (!On || !z || !z.isActiveAndEnabled) return false;
            if (madeOn == z && z.HasChannel(Column) && z.HasChannel(Drop) && z.HasChannel(Mist)) return true;
            foreach (var d in Defs()) if (!z.UeAddChannel(d)) return false;
            madeOn = z;
            return true;
        }

        /// <summary>The water's own tone lifted toward the foam white (the channels are lit by the scene on top of it).</summary>
        public static Color Tint(Color water, float white) => Color.Lerp(water, FoamWhite, Mathf.Clamp01(white));

        static Vector3 Ring() { float a = Random.value * 6.2831853f; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }

        static void E(OpenZFxPlayer z, string ch, Vector3 p, Vector3 v, float life, float size, Color c, float alpha, float delay)
        {
            if (delay > 0f) z.EmitChannel(ch, p, v, life, size, c, alpha, delay); else z.EmitChannel(ch, p, v, life, size, c, alpha);
            Emitted++;
        }

        /// <summary>A tall column of water thrown up from the surface at `at` (s 1 = 11 to 13 m tall, in the air for about a second and
        /// a half, as the column it replaces). False = not drawn (no effect player, or <see cref="On"/> off): the caller draws its own.</summary>
        public static bool Jet(Vector3 at, float s, float delay, Color water)
        {
            if (!Ready(out var z)) return false;
            float q = Mathf.Sqrt(Mathf.Max(0.05f, s)); int n = Mathf.Max(4, Mathf.RoundToInt(26f * s));
            for (int i = 0; i < n; i++)
            {   // the body: round soft masses in a narrow cone, overlapping into one thick column that rises and falls back
                float tilt = Random.Range(0f, 0.2f), sp = Random.Range(10f, 16.5f) * q; var d = Vector3.up * Mathf.Cos(tilt) + Ring() * Mathf.Sin(tilt);
                E(z, Column, at + Ring() * Random.Range(0f, 0.7f) * q + Vector3.up * 0.3f, d * sp, Random.Range(1.0f, 1.7f), Random.Range(0.9f, 1.5f) * q,
                  Tint(water, BodyWhite + Random.Range(-0.08f, 0.12f)), BodyAlpha, delay);
            }
            for (int i = 0; i < n * 2; i++)
            {   // droplet clusters thrown off the top and the sides: they arc out and fall back
                float tilt = Random.Range(0.05f, 0.55f), sp = Random.Range(6f, 14f) * q; var d = Vector3.up * Mathf.Cos(tilt) + Ring() * Mathf.Sin(tilt);
                var p = at + Ring() * Random.Range(0.2f, 0.9f) * q + Vector3.up * Random.Range(0.5f, 2.0f) * q;
                int cl = 1 + (i % 3);
                for (int k = 0; k < cl; k++)
                    E(z, Drop, p + Random.insideUnitSphere * (0.25f * q), d * sp + Random.insideUnitSphere * (0.6f * q), Random.Range(0.9f, 1.6f), Random.Range(0.10f, 0.26f) * q,
                      Tint(water, DropWhite), DropAlpha, delay);
            }
            for (int i = 0; i < Mathf.Max(2, n / 3); i++)
            {   // mist drifting off the column
                var r = Ring();
                E(z, Mist, at + Vector3.up * Random.Range(1f, 6f) * q + r * Random.Range(0f, 1f) * q, r * Random.Range(0.5f, 1.5f) + Vector3.up * Random.Range(0.3f, 1.2f),
                  Random.Range(1.8f, 2.8f), Random.Range(1.8f, 3.2f) * q, Tint(water, MistWhite), MistAlpha, delay + Random.Range(0.1f, 0.5f));
            }
            FoamPatch(z, at, 2.2f * q, 1.6f + 0.6f * Mathf.Clamp01(s), water, delay);
            Jets++;
            return true;
        }

        /// <summary>A piece coming down on the water at `at` (the drawn surface): count droplets with the speeds and times of the game's
        /// splash recipe (OpenZFxCatalog.M12Splash), round and soft; from s 0.4 a small crown, from s 0.3 the foam patch, from s 0.9 a
        /// little mist. False = not drawn: the caller draws its own.</summary>
        public static bool Drops(Vector3 at, float s, int count, Color water)
        {
            if (!Ready(out var z)) return false;
            var L = OpenZFxCatalog.M12Splash; count = Mathf.Clamp(count, 1, 12);
            float size = Mathf.Clamp(0.10f * Mathf.Sqrt(Mathf.Max(0.01f, s)), 0.07f, 0.22f);
            for (int j = 0; j < count; j++)
            {
                float a = j * L.angleStep, ca = Mathf.Cos(a), sa = Mathf.Sin(a), pr = L.posRadial.At(j);
                float rad = L.radial.At(j) * (L.radialTimesStrength > 0f ? s : 1f);
                E(z, Drop, at + new Vector3(ca * pr, L.posUp.At(j), sa * pr), new Vector3(ca * rad, L.up.At(j), sa * rad), L.life.At(j), size * Random.Range(0.8f, 1.2f),
                  Tint(water, DropWhite), DropAlpha, 0f);
            }
            if (s >= 0.4f)
            {   // the crown's body: a few soft puffs that rise a little and fall back
                int puffs = s < 1f ? 2 : 3;
                for (int j = 0; j < puffs; j++)
                    E(z, Column, at + Ring() * (0.15f * s) + Vector3.up * (0.1f * s), Vector3.up * (1.2f + 1.4f * s) + Ring() * (0.6f * s), 0.55f + 0.1f * j,
                      (0.35f + 0.25f * j) * Mathf.Max(0.4f, s), Tint(water, BodyWhite), BodyAlpha * 0.85f, 0f);
            }
            if (s >= 0.9f)
                E(z, Mist, at + Vector3.up * (0.4f * s), Ring() * 0.4f + Vector3.up * 0.5f, 1.4f, 1.2f * s, Tint(water, MistWhite), MistAlpha, 0.05f);
            if (s >= 0.3f) FoamPatch(z, at, 0.85f * Mathf.Max(0.3f, s), 1.5f + 0.7f * Mathf.Clamp01(s), water, 0f);
            PieceSplashes++;
            return true;
        }

        static void FoamPatch(OpenZFxPlayer z, Vector3 at, float size, float life, Color water, float delay)
        {
            if (!z.HasChannel(FoamChannel)) return;
            E(z, FoamChannel, new Vector3(at.x, at.y + OpenZFxCatalog.RippleLift * 0.6f, at.z), Vector3.zero, life, size, Tint(water, FoamPatchWhite), FoamAlpha, delay);
        }

        public static string Status() =>
            $"[OzWaterSplash] {(On ? "ON" : "OFF (as before)")}: jets {Jets}, piece splashes {PieceSplashes}, particles emitted {Emitted}, channels {(madeOn ? "made" : "not made yet")}";
    }
}
