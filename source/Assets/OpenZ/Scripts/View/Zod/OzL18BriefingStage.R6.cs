// OpenZ (GPL-3.0)
// shot (red r5 video 0:14.0-0:15.7) the camera sinks from 29 m to 7 m beside the middle bridge's smoke pillar (L18Pillar_126, 13-46 m away).
// The pillar is an upright column of puffs, so while the camera sinks along it the column looks the same from frame to frame: a dark blob
// Fix (the cause: smoke this near the lens at the frame edge): while a briefing flies, a stump pillar's puff fades out when it is both NEAR
// the camera and near the EDGE of the picture (a cinematic near-camera fade). A puff in the middle of the picture, or one far away, is drawn
// exactly as before; the pillars still stand over every broken span in the wide shots. View only (no Sim file), deterministic per frame
// (each puff's alpha is set from the pillar colour and this frame's camera, nothing accumulates).
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL18BriefingStage
    {
        /// <summary>red_r6_puff: on / off; a puff fully faded nearer than NearA m, untouched beyond NearB m; the picture's middle (share of
        /// the half frame from the centre) where nothing fades (EdgeFrom) up to where the fade is whole (EdgeTo).</summary>
        public static bool PillarNearFade = true;
        public static float PillarNearA = 25f, PillarNearB = 65f, PillarEdgeFrom = 0.50f, PillarEdgeTo = 0.85f;
        /// <summary>Play checks: puffs faded in the last frame (share over 10 %), the most any puff was faded this flight (0-1), frames it acted.</summary>
        public static int PillarFadedNow, PillarFadeFrames; public static float PillarFadeMax;
        static ParticleSystem.Particle[] pillarBuf;

        void PillarNearTick()
        {
            PillarFadedNow = 0;
            if (!PillarNearFade) return;
            var cam = Camera.main; if (!cam) return;
            if (pillarBuf == null) pillarBuf = new ParticleSystem.Particle[256];
            var cp = cam.transform.position;
            float tanH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            byte baseA = (byte)Mathf.RoundToInt(Mathf.Clamp01(PillarColor.a) * 255f);
            bool acted = false;
            foreach (var sp in spans)
            {
                var ps = sp.pillar; if (!ps) continue;
                int n = ps.GetParticles(pillarBuf); if (n == 0) continue;
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    var p = pillarBuf[i];
                    float k = 1f;                                                                        // 1 = drawn as made
                    float d = Vector3.Distance(cp, p.position);
                    if (d < PillarNearB)
                    {
                        Vector3 vp = cam.WorldToViewportPoint(p.position);
                        float near = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PillarNearA, PillarNearB, d));
                        float edge;
                        if (vp.z <= 0.01f) edge = 1f;
                        else
                        {   // how far out toward the frame edge the puff's nearest rim reaches (its size counted, so a big puff half out of the frame counts as edge)
                            float rad = p.GetCurrentSize(ps) * 0.5f / Mathf.Max(0.01f, vp.z * tanH * 2f);
                            float ex = Mathf.Abs(vp.x - 0.5f) * 2f, ey = Mathf.Abs(vp.y - 0.5f) * 2f;
                            float e = Mathf.Max(ex, ey) - rad * 0.5f;
                            edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PillarEdgeFrom, PillarEdgeTo, e));
                        }
                        k = 1f - near * edge;
                    }
                    byte a = (byte)Mathf.RoundToInt(baseA * k);
                    var c = p.startColor;
                    if (c.a != a) { c.a = a; p.startColor = c; pillarBuf[i] = p; changed = true; }
                    if (k < 0.9f) PillarFadedNow++;
                    if (1f - k > PillarFadeMax) PillarFadeMax = 1f - k;
                }
                if (changed) { ps.SetParticles(pillarBuf, n); acted = true; }
            }
            if (acted && PillarFadedNow > 0) PillarFadeFrames++;
        }

        /// <summary>The red_r6_puff line of the Play checks.</summary>
        public static string PillarNearStatus() => $"pillar near-camera edge fade {(PillarNearFade ? "ON" : "OFF")} (near {PillarNearA:F0}-{PillarNearB:F0} m, edge {PillarEdgeFrom:F2}-{PillarEdgeTo:F2}): faded puffs now {PillarFadedNow}, frames it acted {PillarFadeFrames}, most faded {PillarFadeMax:F2}";
    }
}
