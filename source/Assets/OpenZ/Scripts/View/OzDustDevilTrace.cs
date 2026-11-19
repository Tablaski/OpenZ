// OpenZ (GPL-3.0)
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 notes N9 (view only): the L02 dust devils (OzDustDevils) leave a trace on sand, as they cross it:
    /// <list type="bullet">
    /// <item><b>Drifting dust behind</b>: low, wide, faint puffs on the levels' lit sand wake dust (OzLevelDust / the channel m12_dust_wake: the
    /// lit smoke flipbook the tanks' dust uses, soft irregular lobes, the sand's own colour, sun-lit and dark at night; never a round unlit ball),
    /// left behind the devil's foot and drifting off with the wind, low (0.2-0.6 m), fading out over 3-6 s. The R11 haze recipe: large and
    /// faint, spread round the path, so it reads as one soft haze, not puffs.</item>
    /// <item><b>Swept marks on the ground</b>: every <see cref="SweepStepM"/> of travel a faint swept disc (long thin streaks along the travel with a
    /// gentle curl: the swept sand a little lighter, the furrows a little darker, a soft broken rim; drawn in world space, so the overlapping
    /// discs of one path make one coherent band) through the game's own ground-print system (TrackMarks, kind 4 of OpenZ/TrackMark: it
    /// multiplies the lit ground, keeps its light and night tint, and is drawn with the scene-depth test, so it never z-fights and never paints
    /// a unit or a rock in front of it), solid a few seconds, then dissolving grain by grain, gone after <see cref="SweepLifeMin"/>-
    /// <see cref="SweepLifeMax"/> s.</item>
    /// </list>
    /// SAND ONLY: both read TrackMarks' ground classes under the devil (MarkStrength: sand / dirt 1, roads and concrete 0.2, rock 0.3, water 0;
    /// DustStrength for the dust) and leave nothing below <see cref="SandMin"/>; never on a bridge deck (TrackMarks' own deck test). The devils'
    /// day / night amount (OzDustDevils.Strength) thins the dust at night. Cost: about 4 dust puffs a second a devil on the existing channel and
    /// one ground disc (at most ~52 quads in the existing ring buffer, the same one draw call) every 1.5 m. Switch <see cref="On"/>.
    /// </summary>
    public static class OzDustDevilTrace
    {
        public static bool On = true;
        /// <summary>Below this ground class strength (TrackMarks MarkStrength / DustStrength) nothing is left (sand / dirt = 1).</summary>
        public static float SandMin = 0.6f;
        /// <summary>Dust: puffs a second, life (s), size (m at t 0.28; the channel grows 0.5 -> 2.3 x), peak opacity, height (m), spread behind (m)
        /// and across (m), drift with the wind (x the wind), speed out behind (m/s).</summary>
        public static float DustRate = 4f, DustLifeMin = 2.5f, DustLifeMax = 4.5f, DustSizeMin = 0.9f, DustSizeMax = 1.4f, DustAlpha = 0.08f,
                            DustYMin = 0.05f, DustYMax = 0.15f, DustBackMin = 4f, DustBackMax = 7f, DustAcross = 1.4f, DustWind = 0.6f, DustBack = 0.35f;   // 1.5 kit devil_like_morning_n9c: was life 3-6 s, size 1.3-2.0, alpha 0.14, height 0.2-0.6 m, 0.6-2.6 m behind the foot
        /// <summary>
        /// No trace dust is ever drawn over the devil. The wake dust
        /// channel (m12_dust_wake, queue 3008) is drawn AFTER the devil's funnel (OzDustDevils: queue 3007, no depth written), so every puff
        /// behind the funnel was painted over it: the pale puffs along the path filled the gaps between the streaks and the funnel read solid,
        /// smooth and strong. Now a puff whose largest size (x PuffGrowMax, + DriftMarginM), seen from the main camera, would touch the funnel's
        /// outline (+ FunnelMarginPx) is not left (counted in NearFunnel); the rest start a few metres behind the foot and lie low on the
        /// ground. KeepOffFunnel false = the screen test off (the numbers above stay).</summary>
        public static bool KeepOffFunnel = true;
        public static float FunnelMarginPx = 8f, PuffGrowMax = 2.3f, DriftMarginM = 1f;
        /// <summary>Swept marks: one every SweepStepM of travel, radius (m), life (s), strength (x the ground's class).</summary>
        public static float SweepStepM = 1.5f, SweepRadius = 2.6f, SweepLifeMin = 10f, SweepLifeMax = 14f, SweepStrength = 1f;   // 1.5 kit dust_devil_trace_r5: strength 0.8 -> 1 (the look's numbers below are then the fresh track's true shares by day)
        /// <summary>1.5 kit dust_devil_trace_r5 (A25 / N9): the swept track's look
        /// (OpenZ/TrackMark kind 4, the global _OZDevilSweep): the swept lane this much lighter than the ground (0.16 = 16 % of its
        /// brightness, about 6 % on screen), its soft scoured edges this much darker (0.12, about 4 % on screen), how streaky the lane is
        /// (0 even .. 1), how far its edges wander (x the mark's radius). Before: fine streaks only, no band (mean tone -0.2 %). Read when a
        /// mark is written: tune in Play, the next mark shows it. TraceLight 0 and TraceDark 0 = no mark seen.</summary>
        public static float TraceLight = 0.16f, TraceDark = 0.12f, TraceStreak = 0.45f, TraceWobble = 0.25f;
        static readonly int SweepLookId = Shader.PropertyToID("_OZDevilSweep");

        /// <summary>Counted proof: dust puffs drawn, swept marks written, marks skipped off sand.</summary>
        public static int DustPuffs, Sweeps, OffSand, NearFunnel;   // NearFunnel: 1.5 kit devil_like_morning_n9c (puffs not left: they would be seen over the devil)

        sealed class Trail { public Vector3 last; public bool seen; public float acc, travel; }
        static readonly Dictionary<int, Trail> trails = new Dictionary<int, Trail>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { trails.Clear(); DustPuffs = Sweeps = OffSand = NearFunnel = 0; }

        /// <summary>OzDustDevils.Update, once a frame a devil after it moved: its foot (on the ground), heading, the dust it carries (0..1), its dust
        /// colour, its seed (its key) and the frame's time.</summary>
        public static void Step(Vector3 foot, Vector3 dir, float carried, Color dust, float seed, float dt)
        {
            if (!On || dt <= 0f) return;
            var tm = TrackMarks.I; var s = tm ? tm.Surface : null;
            if (s == null) return;
            int key = Mathf.RoundToInt(seed * 1000f);
            if (!trails.TryGetValue(key, out var tr)) { tr = new Trail(); trails[key] = tr; }
            if (!tr.seen) { tr.last = foot; tr.seen = true; return; }
            var moved = foot - tr.last; moved.y = 0f; tr.last = foot;
            float step = moved.magnitude;
            if (step > 6f) return;                                                                  // a jump (a new level, a teleport): nothing between
            dir.y = 0f; if (dir.sqrMagnitude < 1e-4f) dir = step > 1e-4f ? moved / step : Vector3.forward; dir.Normalize();
            float night = Mathf.Clamp01(OzDustDevils.Strength);
            // ---- the swept marks
            tr.travel += step;
            if (tr.travel >= SweepStepM)
            {
                tr.travel -= SweepStepM;
                if (tr.travel > SweepStepM) tr.travel = 0f;
                float cls = s.MarkStrength(foot.x, foot.z);
                if (cls >= SandMin && TrackMarks.Active)
                {
                    float k = Mathf.Clamp01((cls - SandMin) / Mathf.Max(0.05f, 1f - SandMin)) * SweepStrength * (0.6f + 0.4f * night);
                    Shader.SetGlobalVector(SweepLookId, new Vector4(TraceLight, TraceDark, Mathf.Clamp01(TraceStreak), TraceWobble));   // 1.5 kit dust_devil_trace_r5: the swept track's look
                    if (tm.DevilSweep(foot, SweepRadius * Random.Range(0.92f, 1.08f), Mathf.Atan2(dir.z, dir.x), k, Random.Range(SweepLifeMin, SweepLifeMax))) Sweeps++;
                }
                else OffSand++;
            }
            // ---- the drifting dust behind
            tr.acc = Mathf.Min(3f, tr.acc + DustRate * dt * Mathf.Clamp01(0.35f + carried));
            if (tr.acc < 1f) return;
            var wind = OzShuttleExhaust.Wind(); wind.y = 0f;
            var side = new Vector3(dir.z, 0f, -dir.x);
            while (tr.acc >= 1f)
            {
                tr.acc -= 1f;
                var at = foot - dir * Random.Range(DustBackMin, DustBackMax) + side * Random.Range(-DustAcross, DustAcross);
                float ds = s.DustStrength(at.x, at.z);
                if (ds < SandMin || s.MarkStrength(at.x, at.z) < SandMin) continue;                   // sand only (no dust off roads, rock, water)
                s.Sample(at.x, at.z, out float gy, out bool deck);
                if (deck) continue;
                at.y = gy + Random.Range(DustYMin, DustYMax);
                float size = Random.Range(DustSizeMin, DustSizeMax);
                if (KeepOffFunnel && OverFunnel(foot, dir, wind, at, size)) { NearFunnel++; continue; }      // 1.5 kit devil_like_morning_n9c: never over the devil
                var vel = wind * DustWind - dir * DustBack + side * Random.Range(-0.25f, 0.25f) + Vector3.up * Random.Range(0f, 0.02f);   // n9c: lies on the ground (was rising 0.03-0.15 m/s)
                float a = DustAlpha * Mathf.Clamp01(ds) * (0.45f + 0.55f * night) * Random.Range(0.75f, 1.1f);
                if (OzLevelDust.Emit(at, vel, size, Random.Range(DustLifeMin, DustLifeMax), a, s.DustColor(at.x, at.z), 1f)) DustPuffs++;
                // no lit channel here: nothing (never the old round unlit ball)
            }
        }

        /// <summary>1.5 kit devil_like_morning_n9c: true when a puff left at `at` (its largest size: size x PuffGrowMax, + DriftMarginM for the
        /// wind) would be seen over the devil from the main camera: its screen circle touches the funnel's outline, taken as the hull of two
        /// circles: the foot (the ground swirl ring) and the leaning, swaying top (the outer veil's top with its bulge). The trace lies along a
        /// line from the foot, so a puff outside the outline at its birth moves further out while it drifts behind the devil. No camera: false.</summary>
        static bool OverFunnel(Vector3 foot, Vector3 dir, Vector3 wind, Vector3 at, float size)
        {
            var cam = Camera.main; if (!cam) return false;
            var lean = -dir * OzDustDevils.LeanBack + wind * OzDustDevils.LeanWind; lean.y = 0f; lean = Vector3.ClampMagnitude(lean, OzDustDevils.LeanMax);
            int o = OzDustDevils.Layers - 1;                                                           // the outer veil: the widest, the tallest
            float footR = Mathf.Max(OzDustDevils.RingR, OzDustDevils.R0[o] * (1f + OzDustDevils.WobbleK));
            float topR = OzDustDevils.R1[o] * (1f + OzDustDevils.WobbleK) + OzDustDevils.SwayM;
            var top = foot + Vector3.up * (OzDustDevils.Height * OzDustDevils.HShare[o]) + lean;
            Vector3 sf = cam.WorldToScreenPoint(foot), st = cam.WorldToScreenPoint(top), sq = cam.WorldToScreenPoint(at);
            if (sq.z <= 0f) return false;                                                              // behind the camera: never seen with it
            if (sf.z <= 0f || st.z <= 0f) return true;                                                 // the camera in the devil: none
            var right = cam.transform.right;
            float rf = Vector2.Distance(cam.WorldToScreenPoint(foot + right * footR), sf), rt = Vector2.Distance(cam.WorldToScreenPoint(top + right * topR), st);
            float rq = Vector2.Distance(cam.WorldToScreenPoint(at + right * (size * PuffGrowMax * 0.5f + DriftMarginM)), sq);
            Vector2 f = sf, ft = (Vector2)st - f, p = sq;
            float k = Mathf.Clamp01(Vector2.Dot(p - f, ft) / Mathf.Max(1e-4f, ft.sqrMagnitude));
            return Vector2.Distance(p, f + ft * k) < Mathf.Lerp(rf, rt, k) + rq + FunnelMarginPx;
        }

        public static string Status() => $"dust devil trace {(On ? "on" : "off")}: dust puffs {DustPuffs}, swept marks {Sweeps}, off sand {OffSand}, kept off the devil {NearFunnel} (screen test {(KeepOffFunnel ? "on" : "off")}), track lighter {TraceLight:F2} / edge darker {TraceDark:F2}";
    }
}
