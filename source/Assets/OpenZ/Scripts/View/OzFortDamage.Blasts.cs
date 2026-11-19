// OpenZ (GPL-3.0)
using System.Collections;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 notes N3 (view only, no sim read beyond what fort_damage reads): wherever the fort's damage breaks parts off (a new breach, a
    /// parapet break, the chips of a new crack) small LOCAL explosions go off at that spot, staggered over about 0.3-0.8 s, so the parts are
    /// blown off instead of just falling. Four kinds, never the same kind twice in a row at one spot:
    /// <list type="bullet">
    /// <item>0 FLASH + DEBRIS PUFF: a white-hot core flash, a short warm light, a puff of concrete dust and a few sparks; exp_small.</item>
    /// <item>1 FIRE BURST: 4-5 small fireballs out of the wall, a dark smoke puff rising after them, a flickering orange light;
    /// exp_medium (soft) + dst_fire_ignite.</item>
    /// <item>2 SPARKS + SMOKE: a spray of streak sparks and glow sparks falling, grey smoke, a small core flash; ngt_lamp_spark +
    /// dst_fortgun_break_metal (the torn steel in the wall).</item>
    /// <item>3 DUST BURST: a burst of concrete dust out of and along the wall (no flame), fine chips; dst_rock_crumble_medium +
    /// dst_crater_clods.</item>
    /// </list>
    /// All particles are the game's own lit flipbook channels (m12_core, m12_fire, m12_smoke, m12_dust, m12_spark, leg_spark) of
    /// OpenZFxPlayer, the light its AddLight. Sounds go through the game's sound table (AudioDirector: every event is in-view only and picks
    /// its takes without a repeat), so off screen nothing plays; drawn only on screen. The ruin sweep at the ending (hushed) leaves them out
    /// (the blast covers it). Sizes: a breach 1, a parapet break 1.1, a crack's chips 0.6. Switch <see cref="LocalBlastsOn"/> (off = 1.5 as
    /// before). Counters for the Play check: <see cref="LocalBlastsFired"/>, <see cref="LocalBlastKinds"/>, <see cref="LocalBlastSounds"/>.
    /// </summary>
    public partial class OzFortDamage
    {
        public static bool LocalBlastsOn = true;
        /// <summary>All the local blasts' sizes (1 = as tuned), their sound level (x the event's own level).</summary>
        public static float LocalBlastSizeK = 1f, LocalBlastVolK = 1f;
        /// <summary>Counted: blasts fired (on screen or not), drawn (on screen), per kind, sounds sent.</summary>
        public static int LocalBlastsFired, LocalBlastsDrawn, LocalBlastSounds;
        public static readonly int[] LocalBlastKinds = new int[4];
        public static readonly string[] LocalBlastNames = { "flash + debris puff", "fire burst", "sparks + smoke", "dust burst" };

        static readonly Color BlastCore = new Color(1f, 0.9f, 0.68f), BlastFire = new Color(1f, 0.56f, 0.2f), BlastSpark = new Color(1f, 0.72f, 0.36f),
            BlastSmokeDark = new Color(0.30f, 0.285f, 0.27f), BlastSmokeGrey = new Color(0.52f, 0.5f, 0.47f), BlastDust = new Color(0.66f, 0.63f, 0.58f),
            BlastLightWarm = new Color(1f, 0.66f, 0.36f), BlastLightFire = new Color(1f, 0.5f, 0.2f);

        int blastKindNext = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBlastCounters() { LocalBlastsFired = LocalBlastsDrawn = LocalBlastSounds = 0; for (int i = 0; i < 4; i++) LocalBlastKinds[i] = 0; }

        /// <summary>The hooks (AddBreach, AddCrack, ParapetBreak): count blasts round `at` on site s (top = on the parapet's top edge).</summary>
        void LocalBlasts(Fort f, Site s, Vector3 at, int count, bool top)
        {
            if (!LocalBlastsOn || !isActiveAndEnabled || f == null || s == null || f.o == null || !f.root || Hushed(f.o.Id)) return;
            var n = SiteN(f, s); var t = f.root.TransformDirection(s.t); t.y = 0f; t = t.sqrMagnitude > 1e-4f ? t.normalized : Vector3.Cross(Vector3.up, n).normalized;
            float k = (top ? 1.1f : count <= 1 ? 0.6f : 1f) * LocalBlastSizeK;
            StartCoroutine(BlastRun(f, at, n, t, Mathf.Clamp(count, 1, 4), k, top));
        }

        IEnumerator BlastRun(Fort f, Vector3 at, Vector3 n, Vector3 t, int count, float k, bool top)
        {
            // the times: the first with the parts breaking out, the rest staggered, the whole run 0.3-0.8 s (one blast: one alone)
            var due = new float[count];
            for (int i = 1; i < count; i++) due[i] = due[i - 1] + Random.Range(0.12f, 0.32f);
            if (count > 1)
            {
                float span = due[count - 1], want = Mathf.Clamp(span, 0.3f, 0.8f);
                if (span > 1e-3f) for (int i = 1; i < count; i++) due[i] *= want / span;
            }
            if (blastKindNext < 0) blastKindNext = Random.Range(0, 4);
            int kind = count <= 1 && !top ? (Random.value < 0.5f ? 3 : 2) : blastKindNext;   // a crack's chips: a dust burst or sparks
            float clock = 0f;
            for (int i = 0; i < count; i++)
            {
                while (clock < due[i]) { yield return null; clock += Time.deltaTime; }
                if (f == null || f.dead || !f.root || (f.o != null && Hushed(f.o.Id))) yield break;
                Vector3 p = i == 0 ? at + n * 0.1f
                    : at + t * (Random.Range(-0.75f, 0.75f) * (top ? 1.3f : 1f)) + Vector3.up * (top ? Random.Range(-0.15f, 0.25f) : Random.Range(-0.45f, 0.45f)) + n * Random.Range(0.08f, 0.22f);
                LocalBlast(kind, p, n, k * (i == 0 ? 1f : Random.Range(0.75f, 0.95f)), f.o != null ? f.o.Id : -1);
                kind = (kind + 1 + Random.Range(0, 3)) & 3;                                                  // the next one another kind
            }
            blastKindNext = (kind + 1) & 3;
        }

        /// <summary>One local explosion of the given kind at p on the wall (n: out of the wall), size k.</summary>
        void LocalBlast(int kind, Vector3 p, Vector3 n, float k, int emitter)
        {
            kind &= 3; LocalBlastsFired++; LocalBlastKinds[kind]++;
            var px = OpenZFxPlayer.I;
            bool draw = px && px.isActiveAndEnabled && WreckFx.OnScreen(p, 0.15f);
            float vk = LocalBlastVolK;
            switch (kind)
            {
                case 0:   // flash + debris puff
                    if (draw)
                    {
                        px.EmitChannel("m12_core", p + n * 0.12f, n * 0.3f, 0.15f, 1.5f * k, BlastCore, 1f);
                        px.AddLight(p + n * 0.6f, BlastLightWarm, 5f * k, 7f * k, 0.22f, 2.2f, 0f, true);
                        for (int i = 0; i < 4; i++)
                            px.EmitChannel("m12_dust", p + Random.insideUnitSphere * 0.25f * k, n * Random.Range(0.9f, 1.8f) + Vector3.up * Random.Range(0.3f, 0.9f) + Random.insideUnitSphere * 0.4f,
                                           Random.Range(1.2f, 1.7f), Random.Range(0.9f, 1.4f) * k, BlastDust, 0.55f);
                        for (int i = 0; i < 6; i++)
                            px.EmitChannel("m12_spark", p, n * Random.Range(2f, 4f) + Vector3.up * Random.Range(1f, 3f) + Random.insideUnitSphere * 1.2f,
                                           Random.Range(0.35f, 0.6f), 0.55f * 0.07f * k, BlastFire, 1f);
                    }
                    BlastSound("exp_small", p, emitter, 0.42f * vk, Random.Range(1.05f, 1.2f));
                    break;
                case 1:   // fire burst
                    if (draw)
                    {
                        int m = 4 + Random.Range(0, 2);
                        for (int i = 0; i < m; i++)
                        {
                            var o = Random.insideUnitSphere * 0.3f * k;
                            px.EmitChannel("m12_fire", p + o + n * 0.15f, n * Random.Range(1.2f, 2.2f) + Vector3.up * Random.Range(0.8f, 1.6f) + o * 2f,
                                           Random.Range(0.38f, 0.55f), Random.Range(1.0f, 1.5f) * k, BlastFire, 1f);
                        }
                        px.EmitChannel("m12_core", p + n * 0.2f, n * 0.3f, 0.12f, 0.9f * k, BlastCore, 1f);
                        for (int i = 0; i < 4; i++)
                            px.EmitChannel("m12_smoke", p + n * 0.4f + Random.insideUnitSphere * 0.3f * k, n * Random.Range(0.5f, 1.0f) + Vector3.up * Random.Range(1.0f, 1.8f),
                                           Random.Range(1.6f, 2.3f), Random.Range(1.1f, 1.6f) * k, BlastSmokeDark, 0.6f, Random.Range(0.08f, 0.25f));
                        px.AddLight(p + n * 0.8f, BlastLightFire, 6f * k, 8f * k, 0.45f, 1.6f, 0.35f, true);
                    }
                    BlastSound("exp_medium", p, emitter, 0.32f * vk, Random.Range(1.08f, 1.22f));
                    BlastSound("dst_fire_ignite", p, emitter, 0.55f * vk, Random.Range(0.92f, 1.08f));
                    break;
                case 2:   // sparks + smoke
                    if (draw)
                    {
                        for (int i = 0; i < 14; i++)
                            px.EmitChannel("m12_spark", p + Random.insideUnitSphere * 0.1f, n * Random.Range(2.2f, 4.5f) + Vector3.up * Random.Range(1.2f, 3.4f) + Random.insideUnitSphere * 1.6f,
                                           Random.Range(0.4f, 0.8f), 0.55f * 0.07f * k, BlastFire, 1f);
                        for (int i = 0; i < 10; i++)
                            px.EmitChannel("leg_spark", p + Random.insideUnitSphere * 0.15f, n * Random.Range(1.2f, 3.2f) + Vector3.up * Random.Range(0.4f, 2.2f) + Random.insideUnitSphere * 0.8f,
                                           Random.Range(0.35f, 0.7f), Random.Range(0.04f, 0.08f) * k, BlastSpark, 1f);
                        px.EmitChannel("m12_core", p + n * 0.1f, Vector3.zero, 0.1f, 0.6f * k, BlastCore, 1f);
                        for (int i = 0; i < 5; i++)
                            px.EmitChannel("m12_smoke", p + n * 0.3f + Random.insideUnitSphere * 0.35f * k, n * Random.Range(0.4f, 0.9f) + Vector3.up * Random.Range(0.6f, 1.3f),
                                           Random.Range(1.8f, 2.5f), Random.Range(0.9f, 1.4f) * k, BlastSmokeGrey, 0.55f, Random.Range(0.02f, 0.18f));
                        px.AddLight(p + n * 0.5f, BlastSpark, 3.2f * k, 5f * k, 0.18f, 2.4f, 0.6f, true);
                    }
                    BlastSound("ngt_lamp_spark", p, emitter, 0.8f * vk, Random.Range(0.9f, 1.12f));
                    BlastSound("dst_fortgun_break_metal", p, emitter, 0.38f * vk, Random.Range(1.0f, 1.15f));
                    break;
                default:  // dust burst
                    if (draw)
                    {
                        var along = Vector3.Cross(Vector3.up, n); if (along.sqrMagnitude < 1e-4f) along = Vector3.right; along.Normalize();
                        for (int i = 0; i < 8; i++)
                        {
                            float sd = Random.Range(-1f, 1f);
                            px.EmitChannel("m12_dust", p + along * (sd * 0.35f * k) + Random.insideUnitSphere * 0.2f * k,
                                           n * Random.Range(0.8f, 2.0f) + along * (sd * Random.Range(0.6f, 1.4f)) + Vector3.up * Random.Range(-0.2f, 0.7f),
                                           Random.Range(1.6f, 2.4f), Random.Range(1.1f, 1.8f) * k, BlastDust, 0.6f);
                        }
                        for (int i = 0; i < 6; i++)
                            px.EmitChannel("leg_ember", p + Random.insideUnitSphere * 0.2f, n * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(1f, 2.5f) + Random.insideUnitSphere,
                                           Random.Range(0.5f, 0.9f), Random.Range(0.03f, 0.05f) * k, BlastDust * 0.7f, 0.9f);
                    }
                    BlastSound("dst_rock_crumble_medium", p, emitter, 0.45f * vk, Random.Range(1.0f, 1.12f));
                    BlastSound("dst_crater_clods", p, emitter, 0.55f * vk, Random.Range(0.95f, 1.1f));
                    break;
            }
            if (draw) LocalBlastsDrawn++;
        }

        /// <summary>One sound through the table (in-view only, takes without a repeat); never in the ruin sweep (Play's own hush).</summary>
        void BlastSound(string id, Vector3 at, int emitter, float vol, float pitch)
        {
            if (!AudioDirector.Has(id) || Hushed(emitter)) return;
            AudioDirector.Fire(id, at, emitter, vol, pitch); SoundsPlayed++; LocalBlastSounds++;
        }

        public static string LocalBlastReport() =>
            $"fort local blasts {(LocalBlastsOn ? "on" : "off")}: fired {LocalBlastsFired}, drawn {LocalBlastsDrawn}, sounds {LocalBlastSounds}; kinds " +
            $"{LocalBlastNames[0]} {LocalBlastKinds[0]}, {LocalBlastNames[1]} {LocalBlastKinds[1]}, {LocalBlastNames[2]} {LocalBlastKinds[2]}, {LocalBlastNames[3]} {LocalBlastKinds[3]}";
    }
}
