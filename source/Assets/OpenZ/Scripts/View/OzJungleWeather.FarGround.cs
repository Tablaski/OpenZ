// OpenZ (GPL-3.0) - Level 16 rain beyond the plateau (testing the free camera):
// a splash or a drip beyond the plateau (the map and its 24 m rim) took the game's ground there, and the level has no collider out there:
// it landed at height 0, under the land beyond the rim or floating over a dip or a pool. Now it lands on what is drawn there
// (OzL16Outskirts.FarGround: the land, the water of its pools, in a low shot the flyover's far water). On the plateau nothing changes.
// Also the Play check's counts of the rain itself: the streak layers against the view, the splashes against their ground.
// Play check: return OpenZ.OzJungleWeather.I.FarSplashCheck(out _, out _);
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzJungleWeather
    {
        /// <summary>Splashes and drips put on the land or the water beyond the plateau, and of them on water.</summary>
        public int SplashesFar { get; private set; }
        public int SplashesFarWater { get; private set; }
        ParticleSystem.Particle[] checkParts;

        /// <summary>The ground a drop at (x, z) lands on: the game's (gy) on the plateau, else what is drawn there; farWater = it lands on
        /// water there (a ring; the level's own water tiles stay the caller's).</summary>
        float SplashGround(float x, float z, float gy, out bool farWater)
        {
            farWater = false;
            if (!OzL16Outskirts.FarGround(x, z, out float y, out bool w)) return gy;
            SplashesFar++;
            if (w) { SplashesFarWater++; farWater = true; }
            return y;
        }

        /// <summary>Play check: every live splash, ring and drip beyond the plateau against the ground drawn under it (FarGround): more than
        /// 0.2 m over or under it = off its ground (floating or sunk).</summary>
        public string FarSplashCheck(out int beyond, out int off)
        {
            beyond = off = 0;
            if (!splashes) return "far splashes: no rain particles";
            int n = splashes.particleCount;
            if (checkParts == null || checkParts.Length < n) checkParts = new ParticleSystem.Particle[Mathf.Max(n, 256)];
            n = splashes.GetParticles(checkParts);
            int plateau = 0; float worst = 0f;
            for (int i = 0; i < n; i++)
            {
                var p = checkParts[i].position;                                                           // world space (the splash system simulates in world)
                if (!OzL16Outskirts.FarGround(p.x, p.z, out float y, out _)) { plateau++; continue; }
                beyond++;
                float dy = p.y - y - 0.045f;                                                              // a ground speck sits 0.05 m up, a ring on water 0.04 m
                if (Mathf.Abs(dy) > Mathf.Abs(worst)) worst = dy;
                if (Mathf.Abs(dy) > 0.2f) off++;
            }
            return $"far splashes: live {n} (on the plateau or nothing drawn beyond it {plateau}; beyond the plateau {beyond}, off their ground by more than 0.2 m {off}, worst {worst:+0.00;-0.00} m) | put beyond the plateau so far {SplashesFar} (on water {SplashesFarWater})";
        }

        /// <summary>Play check: each streak layer's box (as UpdateParticles and Layer put it this frame) against the camera's view between
        /// the layer's near and far depth: the four corners of the view at 10 / 50 / 90 % of the band, each inside the box or inside it moved
        /// by the streaks' own travel while they live (the fall and the wind) = covered; and its live streaks by screen column (16 columns):
        /// a column under a fifth of the median beside one over 0.6 of it = a hard edge (judged when the median is 20 or more).</summary>
        public string StreakCheck(out int uncovered, out int hardEdges)
        {
            uncovered = hardEdges = 0;
            if (!cam || !streaks) return "streaks: not built";
            var ct = cam.transform;
            float h = rts ? rts.height : Mathf.Max(10f, ct.position.y);
            float D = Mathf.Clamp(h / Mathf.Max(0.2f, -ct.forward.y), 15f, 600f);
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), tanH = tanV * cam.aspect;
            Vector2 wd = OzJungleAir.RainWindDir; float slant = slantSpeed * windK;
            var sb = new System.Text.StringBuilder();
            sb.Append($"streaks (rain {rain:F2}, view distance {D:0} m):");
            StreakLayer(sb, "far", streaks, farFrom * D, 1.02f * D, 1f, tanV, tanH, wd, slant, ref uncovered, ref hardEdges);
            StreakLayer(sb, "mid", midStreaks, midFrom * D, farFrom * D, midScale.z, tanV, tanH, wd, slant, ref uncovered, ref hardEdges);
            StreakLayer(sb, "near", nearStreaks, nearFrom * D, midFrom * D, nearScale.z, tanV, tanH, wd, slant, ref uncovered, ref hardEdges);
            return sb.ToString();
        }

        void StreakLayer(System.Text.StringBuilder sb, string name, ParticleSystem ps, float dn, float df, float kz, float tanV, float tanH, Vector2 wd, float slant, ref int uncovered, ref int hardEdges)
        {
            if (!ps || !ps.gameObject.activeInHierarchy) { sb.Append($" {name} off;"); return; }
            var ct = cam.transform; var tr = ps.transform; var shape = ps.shape; Vector3 half = shape.scale * 0.5f;
            var travel = new Vector3(wd.x * slant * kz, -dropSpeed * kz, wd.y * slant * kz) * dropLife;
            int n = 0, ok = 0;
            for (int q = 0; q < 3; q++)
            {
                float z = Mathf.Lerp(dn, df, q == 0 ? 0.1f : q == 1 ? 0.5f : 0.9f);
                for (int c = 0; c < 4; c++)
                {
                    var p = ct.position + ct.forward * z + ct.right * (((c & 1) == 0 ? -1f : 1f) * z * tanH) + ct.up * (((c & 2) == 0 ? -1f : 1f) * z * tanV);
                    bool inside = false;
                    for (int s = 0; s <= 4 && !inside; s++)
                    {
                        var loc = tr.InverseTransformPoint(p - travel * (s / 4f)) - shape.position;
                        inside = Mathf.Abs(loc.x) <= half.x + 0.01f && Mathf.Abs(loc.y) <= half.y + 0.01f && Mathf.Abs(loc.z) <= half.z + 0.01f;
                    }
                    n++; if (inside) ok++;
                }
            }
            uncovered += n - ok;
            int cnt = ps.particleCount;
            if (checkParts == null || checkParts.Length < cnt) checkParts = new ParticleSystem.Particle[Mathf.Max(cnt, 256)];
            cnt = ps.GetParticles(checkParts);
            var cols = new int[16]; int framed = 0;
            for (int i = 0; i < cnt; i++)
            {
                var w = follow ? follow.TransformPoint(checkParts[i].position) : checkParts[i].position;   // the streaks simulate in the camera follower's space
                var v = cam.WorldToViewportPoint(w);
                if (v.z <= 0f || v.x < 0f || v.x >= 1f || v.y < 0f || v.y > 1f) continue;
                cols[Mathf.Min(15, (int)(v.x * 16f))]++; framed++;
            }
            var sorted = (int[])cols.Clone(); System.Array.Sort(sorted);
            float med = 0.5f * (sorted[7] + sorted[8]); int edges = 0;
            if (med >= 20f)
                for (int i = 0; i + 1 < 16; i++)
                {
                    int lo = Mathf.Min(cols[i], cols[i + 1]), hi = Mathf.Max(cols[i], cols[i + 1]);
                    if (lo < 0.2f * med && hi > 0.6f * med) edges++;
                }
            hardEdges += edges;
            sb.Append($" {name} box covers {ok}/{n} view corners, {framed} of {cnt} streaks framed, per column min {sorted[0]} median {med:0} max {sorted[15]}{(med >= 20f ? $", hard edges {edges}" : " (too few to judge)")};");
        }
    }
}
