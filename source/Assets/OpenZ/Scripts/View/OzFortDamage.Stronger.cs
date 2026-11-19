using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit fort_damage_stronger (review FAIL 11 / 13: at the RTS camera the fort at 5 % health looked almost like the fort at 100 %). The
    /// wall marks of fort_damage sit on the walls, which the RTS camera sees steeply from above; what reads from up there is the whole fort's paint,
    /// the parapet's top line, the rubble at the foot and what rises above it. So, on top of the kit's own stronger numbers (OzFortDamage.cs:
    /// earlier and more cracks / breaches / parapet breaks / fires, 32 sooted sites, the paint's char up to 0.55, full-strength soot, bigger
    /// breach fires and smoke, bigger rubble):
    /// <list type="bullet">
    /// <item><b>Smoke columns</b> from stage 2 (65 % health) over the breaches: 2 at a time, 3 in the ruin stage, thick and tall (WreckSmoke
    /// x 2.6-4.8), every 0.5-0.22 s.</item>
    /// <item><b>Fires on the broken parapet tops</b> (seen from above): every parapet break keeps a fire burning on the top edge it broke (its own
    /// ignite sound at the break; the fort's fire loop plays while any burns).</item>
    /// <item><b>Broken parapet parts</b>: at every parapet break 4 concrete chunks of the game's chunk library sit jagged on the top edge (half sunk,
    /// popping in), so the parapet's line is broken where it broke; they go with the breach on a heal and pop out when the fort falls.</item>
    /// </list>
    /// View only, the sim untouched. Every mark still eases in (no cut); parts still fall off between the stages.
    /// </summary>
    public partial class OzFortDamage
    {
        /// <summary>Statistics (the Play check / the stage preview's report).</summary>
        public static int ColumnPuffs, TopFires, Jags;
        /// <summary>1.5 pass 4: the smoke columns' flipbook puffs (size m and alpha from 75 % to 0 % health, colour).</summary>
        public static float ColumnSizeMin = 3.5f, ColumnSizeMax = 6f, ColumnAlphaMin = 1f, ColumnAlphaMax = 1f;   // try 2 (tried live): 2.2-4.2 m at 0.45-0.7 drew a faint haze only
        public static Color ColumnColour = new Color(0.12f, 0.115f, 0.11f);

        static Vector3 TopOf(Fort f, Site s) => f.root.TransformPoint(new Vector3(s.p.x, s.top, s.p.z));

        /// <summary>Step, after Effects: the smoke columns and the fires on the broken parapet tops.</summary>
        void Stronger(Fort f)
        {
            if (f.breaches.Count == 0 || !f.root) return;
            float now = Time.time, d = f.d;
            var px = OpenZFxPlayer.I; bool pxOn = px && px.isActiveAndEnabled; var fx = FxManager.I;
            if (f.stage >= 2 && pxOn && now >= f.nextColumn)
            {
                f.nextColumn = now + Mathf.Lerp(0.5f, 0.22f, Mathf.Clamp01((d - Stage2At) / (1f - Stage2At)));
                int n = Mathf.Min(f.breaches.Count, f.stage >= 3 ? 3 : 2);
                for (int i = 0; i < n; i++)
                {
                    var s = f.breaches[(f.columnRR++ & 0x7FFFFFFF) % f.breaches.Count];
                    var at = TopOf(f, s) + Vector3.up * 0.6f + SiteN(f, s) * 0.4f;
                    if (WreckFx.OnScreen(at, 0.1f))
                    {
                        px.WreckSmoke(at, Mathf.Lerp(2.6f, 4.8f, d), true); ColumnPuffs++;
                        // was ever seen; the column is now the game's lit smoke flipbook, 2.2-4.2 m puffs rising ~1.4 m/s for 3-4 s (one per breach a step)
                        px.EmitChannel("m12_smoke", at + new Vector3(Random.Range(-0.4f, 0.4f), 0.3f, Random.Range(-0.4f, 0.4f)),
                                       new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(1.1f, 1.7f), Random.Range(-0.15f, 0.15f)),
                                       Random.Range(3.2f, 4.2f), Mathf.Lerp(ColumnSizeMin, ColumnSizeMax, d) * Random.Range(0.85f, 1.15f), ColumnColour, Mathf.Lerp(ColumnAlphaMin, ColumnAlphaMax, d));
                    }
                }
            }
            if (f.parapets > 0 && fx && now >= f.nextTopFire)
            {
                f.nextTopFire = now + 0.3f;
                for (int k = 0; k < f.parapets; k++)
                {
                    var s = f.breaches[Mathf.Min(k, f.breaches.Count - 1)];
                    var at = TopOf(f, s) + Vector3.up * 0.25f;
                    if (WreckFx.OnScreen(at, 0.1f)) { fx.Burn(at, Mathf.Lerp(1.0f, 1.6f, d)); TopFires++; }
                }
            }
        }

        /// <summary>ParapetBreak: the broken top edge: 4 chunks (M, S) half sunk on the parapet round the break, popping in; and the break's fire
        /// starts (its ignite sound).</summary>
        void ParapetJag(Fort f, Site s, Vector3 top, int k)
        {
            if (!Hushed(f.o.Id)) { OzSoundV3.Ignite($"fdmgtop_{f.o.Id}_{k}", top, 0.5f); SoundsPlayed++; }   // 1.5 ending_fort_ruin: hushed in the ruin sweep
            if (!ConcreteChunkLib.Load() || !f.root) return;
            var n = SiteN(f, s); var t = f.root.TransformDirection(s.t).normalized;
            for (int i = 0; i < 4; i++)
            {
                var e = ConcreteChunkLib.Pick(f.rnd, i < 2 ? "M" : "S", false); if (e == null || !e.mesh) continue;
                var scale = ConcreteChunkLib.RandomScale(f.rnd, e.max_scale) * Rand(f, 0.8f, 1.0f);
                float r = e.Longest * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * 0.5f;
                var at = top + t * Rand(f, -0.9f, 0.9f) + n * Rand(f, -0.15f, 0.25f) - Vector3.up * (r * 0.35f);   // half sunk on the top edge
                var go = new GameObject("OZ_FortParapetJag");
                go.transform.SetParent(transform, true);
                go.transform.SetPositionAndRotation(at, ConcreteChunkLib.RandomRotation(f.rnd));
                go.AddComponent<MeshFilter>().sharedMesh = e.mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = e.mats; ConcreteChunkLib.ApplyLook(mr, e, f.rnd);
                go.transform.localScale = Vector3.zero;
                var pop = go.AddComponent<RimPop>(); pop.to = scale;
                f.rims.Add(new Rim { go = go, site = s });
                Jags++;
            }
        }
    }
}
