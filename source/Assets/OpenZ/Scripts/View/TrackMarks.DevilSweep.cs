// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 notes N9: a dust devil's swept mark in the ground-print system (OzDustDevilTrace). A disc of radius r as a lattice of
    /// <see cref="SweepCells"/> x <see cref="SweepCells"/> quads on the DRAWN ground (every vertex on the rendered terrain + the prints' lift, like a
    /// footprint lattice, so the disc lies over the dunes instead of cutting them; the cells wholly outside the disc are left out), in the same ring
    /// buffer, the same material and the same single draw call as every print. Kind 4 (OpenZ/TrackMark DevilSweep): TEXCOORD0 = the disc's own
    /// coordinates (-1..1); the devil's heading rides in the print's seed (kind 4 + heading / 2 pi x 0.9), the swept streaks are drawn in the shader in WORLD
    /// space along that heading, so the overlapping discs of one path make one coherent band (no criss-cross). Life / dissolve and the
    /// scene-depth test are the prints' own (no z-fighting, never over a unit or a rock); never on a bridge deck.
    /// </summary>
    public partial class TrackMarks
    {
        /// <summary>Quads across a swept disc (at a 2.6 m radius: 0.65 m cells).</summary>
        public const int SweepCells = 8;
        /// <summary>The swept marks written (proof).</summary>
        public static int SweepsWritten;
        readonly Vector3[] sweepGrid = new Vector3[(SweepCells + 1) * (SweepCells + 1)];

        /// <summary>One swept disc at centre (radius m, the devil's heading rad in xz (atan2 z, x), strength 0..1, life s). False: not written (not ready, a deck).</summary>
        public bool DevilSweep(Vector3 centre, float radius, float heading, float strength, float life)
        {
            if (!ready || Surface == null || radius < 0.2f || strength < 0.01f) return false;
            var c0 = centre; if (Height(ref c0)) return false;                                                // never on a deck
            const int N = SweepCells; int row = N + 1;
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                {
                    var p = centre + new Vector3((2f * i / N - 1f) * radius, 0f, (2f * j / N - 1f) * radius);
                    OnGround(ref p); sweepGrid[j * row + i] = p;
                }
            byte sb = (byte)Mathf.RoundToInt(Mathf.Clamp01(strength) * 255f);
            float h01 = Mathf.Repeat(heading / (2f * Mathf.PI), 1f);
            float ks = 4f + Mathf.Min(0.8999f, h01 * 0.9f);                                                   // kind 4 + the heading (the streaks' direction; the dissolve's seed too)
            int n = 0;
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    float u0 = 2f * i / N - 1f, u1 = 2f * (i + 1) / N - 1f, v0 = 2f * j / N - 1f, v1 = 2f * (j + 1) / N - 1f;
                    float nu = Mathf.Clamp(0f, u0, u1), nv = Mathf.Clamp(0f, v0, v1);                           // the cell's point nearest the centre
                    if (nu * nu + nv * nv > 1.02f) continue;                                                  // wholly outside the disc
                    int g = j * row + i;
                    Quad(sweepGrid[g], sweepGrid[g + 1], sweepGrid[g + row + 1], sweepGrid[g + row],
                         new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), clock, clock, sb, sb, life, ks);
                    n++;
                }
            SweepsWritten++;
            return n > 0;
        }
    }
}
