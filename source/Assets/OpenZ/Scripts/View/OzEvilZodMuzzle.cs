using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only. OzEvilZodFx.Shot hands every shot here, on the frame the pellets leave: the fire at the barrel end of his shotgun, as a heavy
    /// shotgun blast. Before, the flash was one soft round glow 2.4 m across (m12_core), three soft cones and a 3 m warm glow, all born at size 0 and gone in
    /// 0.07-0.1 s: in the game one frame of a pale wash over the gun that reads as the light on the ground, no fire.
    /// Now, from the muzzle along the shot (all sizes x the dash shot's OzEvilZodFx.DashMuzzleK):
    /// <list type="bullet">
    /// <item>the burst: a white-hot core (wv2_glow) and a star of short flame petals around the bore (m12_flame, the launch flash's flame, HDR x15) with two thin
    /// side spikes (wv2_laser's thin hot streak in fire colours), <see cref="StarLife"/> s = 1-2 frames;</item>
    /// <item>the plume: flame tongues along the bore, longest in the middle, white-yellow at the muzzle, orange further out (m12_flame), the far ones die first so the
    /// fire pulls back to the barrel over <see cref="TongueLife"/> s, and three fireball puffs of the game's heat-ramp fire (blast_core: white-hot heart, orange
    /// rim) blown forward; a warm halo (wv2_glow) over it;</item>
    /// <item>sparks thrown forward in a narrow cone (wv2_spark) and slower embers (leg_spark_light);</item>
    /// <item>a smoke puff along the line that drifts on and fades (m12_smoke, the smoke flipbook), one that lingers at the muzzle.</item>
    /// </list>
    /// Untouched: OzEvilZodFx's lights and ground lights (the lit ground), the game's own muzzle (OpenZFxPlayer.Muzzle), the dust under the muzzle, the shake, the
    /// sound, the pellets, the shell. By night the hot parts are <see cref="NightHeat"/> as strong (the night bloom is stronger); with CALM SCREEN the star and
    /// spikes (the strobe) are left out and the rest is softer. Switch <see cref="On"/>: off = round 2's flash, exactly as before.
    /// A file of its own (not a partial of OzEvilZodFx): OzEvilZodFx keeps one added line, so the other EvilZod kits of 3 Nov can change it freely.
    /// </summary>
    public static class OzEvilZodMuzzle
    {
        public static bool On = true;
        /// <summary>Metres (x DashMuzzleK on a dash shot): the core's and the halo's glow sizes, the star's petal width (length = 1.89 x width, m12_flame), the
        /// spikes' width (length = 13 x width, wv2_laser), the plume's tongue width and reach (the far tongue's centre), the fireballs' size.</summary>
        public static float CoreSize = 1.5f, HaloSize = 2.4f, PetalWidth = 0.26f, SpikeWidth = 0.09f, TongueWidth = 0.6f, PlumeReach = 1.55f, FireSize = 0.95f;
        /// <summary>Seconds: the star and spikes, the longest tongue (the muzzle's own; the far ones live 0.7 of it), the fireballs, the halo.</summary>
        public static float StarLife = 0.07f, TongueLife = 0.10f, FireLife = 0.16f, HaloLife = 0.10f;
        /// <summary>Sparks and embers a shot (x DashMuzzleK on a dash shot), the sparks' cone (degrees) and speeds (m/s).</summary>
        public static int SparkCount = 12, EmberCount = 8;
        public static float SparkConeDeg = 18f, SparkMin = 11f, SparkMax = 22f;
        /// <summary>The smoke: puffs along the line, their alpha, their life (s), how fast they drift on (m/s).</summary>
        public static int SmokePuffs = 4;
        public static float SmokeAlpha = 0.5f, SmokeLife = 1.35f, SmokeDrift = 1.6f;
        /// <summary>The hot parts' strength by night (x alpha; 1 = as by day) and with CALM SCREEN.</summary>
        public static float NightHeat = 0.65f, CalmHeat = 0.6f;                                               // U9d polish round 1 (night white-out): 0.8 -> 0.65
        /// <summary>Where a stretched billboard (m12_flame, wv2_laser) lies on its point: true = the point is its FRONT end and it reaches back along -velocity
        /// (what round 2's frames show: its leg_cone, set 1.3 m ahead of the muzzle, reached back to his grip on N+1), so every petal, spike and tongue is
        /// emitted at its front end and covers the span stated here (the plume from the muzzle out, nothing over the barrel); false = the point is its centre.</summary>
        public static bool StretchFromHead = true;
        static float Head => StretchFromHead ? 1f : 0.5f;                                                      // the point's place along the streak, from its back end
        static readonly Color CoreHot = new Color(1f, 0.92f, 0.74f), Petal = new Color(1f, 0.62f, 0.24f), Spike = new Color(1f, 0.72f, 0.36f),
            TongueNear = new Color(1f, 0.8f, 0.48f), TongueMid = new Color(1f, 0.6f, 0.24f), TongueFar = new Color(1f, 0.46f, 0.14f), Halo = new Color(1f, 0.55f, 0.2f),
            SparkHot = new Color(1f, 0.76f, 0.4f), Ember = new Color(1f, 0.55f, 0.18f), Smoke = new Color(0.64f, 0.62f, 0.58f);
        /// <summary>Counters for the Play check: blasts drawn, the last one's time, muzzle and direction, the particles it emitted.</summary>
        public static int Blasts, LastParticles; public static float LastAt = -1f; public static Vector3 LastFrom, LastDir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Blasts = LastParticles = 0; LastAt = -1f; LastFrom = LastDir = Vector3.zero; }

        /// <summary>OzEvilZodFx.Shot: the blast at the muzzle `from`, along the shot `dir`; k = 1, or OzEvilZodFx.DashMuzzleK on a dash shot.</summary>
        public static void Blast(OpenZFxPlayer z, Vector3 from, Vector3 dir, float k)
        {
            if (!z) return;
            var d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            var s = Vector3.Cross(Vector3.up, d); s = s.sqrMagnitude > 1e-4f ? s.normalized : Vector3.right;   // across the bore, level
            var u = Vector3.Cross(d, s).normalized;                                                            // across the bore, up
            bool calm = OzMotion.Reduced;
            float heat = Mathf.Lerp(1f, NightHeat, NightGrade.NightK) * (calm ? CalmHeat : 1f);
            int n = 0;
            Blasts++; LastAt = Time.time; LastFrom = from; LastDir = d;

            // the burst: white-hot core, a star of short petals and two thin spikes across the bore (1-2 frames)
            z.EmitChannel("wv2_glow", from + d * (0.15f * k), Vector3.zero, StarLife, CoreSize * k, CoreHot, heat); n++;
            if (!calm)
            {
                for (int i = 0; i < 4; i++)
                {   // an X in the level plane (seen from the game camera above), petals forward and back at 50 / 130 degrees off the bore
                    float a = (i < 2 ? 50f : 130f) * (i % 2 == 0 ? 1f : -1f) * Mathf.Deg2Rad;
                    Star(z, from, (d * Mathf.Cos(a) + s * Mathf.Sin(a)).normalized, PetalWidth * k, heat); n++;
                }
                for (int i = -1; i <= 1; i += 2) { Star(z, from, (d * Mathf.Cos(55f * Mathf.Deg2Rad) + u * (i * Mathf.Sin(55f * Mathf.Deg2Rad))).normalized, PetalWidth * 0.85f * k, heat); n++; }   // and up / down: seen from the side
                for (int i = -1; i <= 1; i += 2)
                {
                    float w = SpikeWidth * k, len = w * (1.7f / 0.13f);                                            // wv2_laser: length = 13 x width
                    z.EmitChannel("wv2_laser", from + s * (i * (len * Head + 0.1f * k)), s * (i * 0.05f), StarLife * 0.75f, w, Spike, heat); n++;   // 0.1 .. len + 0.1 out to the side
                }
            }
            else OzCalm.Note("evilzod muzzle star");

            // the plume: tongues along the bore (centres at 0.27, 0.61, 1.0 of the reach; the far ones die first), two splayed sideways, one up
            float reach = PlumeReach * k, tw = TongueWidth * k;
            Tongue(z, from + d * (0.27f * reach), d, tw * 0.82f, TongueLife, TongueNear, heat); n++;
            Tongue(z, from + d * (0.61f * reach), d, tw, TongueLife * 0.9f, TongueMid, heat); n++;
            Tongue(z, from + d * reach, d, tw * 0.85f, TongueLife * 0.7f, TongueFar, heat); n++;
            for (int i = -1; i <= 1; i += 2)
            {
                var e = (d + s * (i * 0.25f)).normalized;                                                            // 14 degrees off the bore
                Tongue(z, from + e * (0.48f * reach), e, tw * 0.66f, TongueLife * 0.8f, TongueFar, heat); n++;
            }
            var up = (d + u * 0.21f).normalized;                                                                    // 12 degrees up
            Tongue(z, from + up * (0.45f * reach), up, tw * 0.6f, TongueLife * 0.8f, TongueMid, heat); n++;
            for (int i = 0; i < 3; i++)
            {   // heat-ramp fireballs (the colour comes from the ramp: white-hot heart, orange rim), blown forward, the far ones smaller and shorter
                float at = (0.35f + 0.38f * i) * reach, sz = FireSize * k * (i == 1 ? 1f : 0.82f);
                z.EmitChannel("blast_core", from + d * at, d * (4.5f - 1f * i) * k + Vector3.up * 0.4f, FireLife * (1f - 0.12f * i), sz, Color.white, 1f); n++;
            }
            z.EmitChannel("wv2_glow", from + d * (0.55f * reach), d * 1.5f, HaloLife, HaloSize * k, Halo, 0.5f * heat); n++;

            // sparks forward, embers slower and wider
            float tan = Mathf.Tan(SparkConeDeg * Mathf.Deg2Rad); int sparks = Mathf.RoundToInt(SparkCount * k), embers = Mathf.RoundToInt(EmberCount * k);
            for (int i = 0; i < sparks; i++)
            {
                var r = Random.insideUnitCircle * tan; var v = (d + s * r.x + u * r.y).normalized;
                z.EmitChannel("wv2_spark", from + d * (0.3f * k), v * Random.Range(SparkMin, SparkMax) + Vector3.up * Random.Range(0.3f, 1.6f), Random.Range(0.16f, 0.32f), Random.Range(0.05f, 0.065f), SparkHot, 1f); n++;
            }
            for (int i = 0; i < embers; i++)
            {
                var r = Random.insideUnitCircle * (tan * 2f); var v = (d + s * r.x + u * r.y).normalized;
                z.EmitChannel("leg_spark_light", from + d * (0.5f * k), v * Random.Range(3f, 7f) + Vector3.up * Random.Range(0.6f, 1.6f), Random.Range(0.3f, 0.55f), Random.Range(0.12f, 0.22f), Ember, 1f); n++;
            }

            // smoke: puffs along the line that drift on, grow and fade; one that lingers at the muzzle
            for (int i = 0; i < SmokePuffs; i++)
            {
                var p = from + d * ((0.45f + 0.55f * i) * k) + Vector3.up * 0.05f;
                var v = d * (SmokeDrift * (1f - 0.18f * i)) + Vector3.up * (0.35f + 0.08f * i) + Random.insideUnitSphere * 0.25f;
                z.EmitChannel("m12_smoke", p, v, SmokeLife * Random.Range(0.85f, 1.2f), (0.55f + 0.12f * i) * k, Smoke, SmokeAlpha); n++;
            }
            z.EmitChannel("m12_smoke", from + d * (0.2f * k), Vector3.up * 0.4f + d * 0.4f, SmokeLife * 1.2f, 0.45f * k, Smoke, SmokeAlpha * 0.9f); n++;
            LastParticles = n;
        }

        /// <summary>A star petal: a short flame from 0.05 m off the muzzle out along `e` (m12_flame: a stretched billboard, its axis the velocity, its place on
        /// its point <see cref="StretchFromHead"/>).</summary>
        static void Star(OpenZFxPlayer z, Vector3 from, Vector3 e, float w, float heat)
        {
            float len = w * (0.70f / 0.37f);
            z.EmitChannel("m12_flame", from + e * (len * Head + 0.05f), e * 0.05f, StarLife, w, Petal, heat);
        }

        /// <summary>A flame tongue centred at `at` along `e`, drifting on with the blast (emitted at its front end with <see cref="StretchFromHead"/>).</summary>
        static void Tongue(OpenZFxPlayer z, Vector3 at, Vector3 e, float w, float life, Color c, float heat) =>
            z.EmitChannel("m12_flame", at + e * (w * (0.70f / 0.37f) * (Head - 0.5f)), e * 2.5f, life, w, c, heat);
    }
}
