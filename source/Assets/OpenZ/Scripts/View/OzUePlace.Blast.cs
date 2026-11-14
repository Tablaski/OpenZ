using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUePlace.cs Words): the drop preview's blast ring for the words' place. A unit whose shot bursts
    /// (Blast over 0.6 m, as the light tank's 5 m) shows a thin solid ring of that radius round the drop's centre (INK 30 %,
    /// View/OzUePlace.cs Preview); the drop's words keep clear of it as of the range ring (View/OzUeWords.cs: it counts as part of the
    /// preview, so "under the models" is under it too).
    /// </summary>
    internal sealed partial class OzUePlaceDriver
    {
        readonly float[] blastX = new float[RingSamples], blastY = new float[RingSamples];

        /// <summary>Checks (the Play proof): the blast ring of the last frame's preview (m round WordsCentre; 0 = none drawn).</summary>
        internal static float WordsBlastM;

        /// <summary>The blast ring on the canvas (blastM round the centre on the floor), the points in front of the camera.</summary>
        int BlastPolyline(Camera cam, OpenZHud hud, Vector3 centre, float blastM)
        {
            if (!(blastM > 0.1f) || !cam || !hud) return 0;
            int n = 0;
            for (int k = 0; k < RingSamples; k++)
            {
                float a = k * (2f * Mathf.PI / RingSamples);
                var s = cam.WorldToScreenPoint(new Vector3(centre.x + Mathf.Cos(a) * blastM, centre.y, centre.z + Mathf.Sin(a) * blastM));
                if (s.z <= cam.nearClipPlane) continue;
                var c = hud.ScreenToCanvas(s);
                blastX[n] = c.x; blastY[n] = c.y; n++;
            }
            return n;
        }
    }
}
