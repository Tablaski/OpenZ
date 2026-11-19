using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit big_tanks_wreck_finer: a Mammoth's or a Bizon's break rattles. Its many small pieces add small metal debris sounds over the first
    /// <see cref="Span"/> s of the break (the game's own dst_debris_metal clips, quieter and a little higher than the big clank, each at a piece
    /// that is flying), then one wreck creak. Staggered by the unit's id (the same break sounds the same), on top of each piece's own first-contact
    /// clanks (WreckPieces, at most 5 a piece). Made by <see cref="OzBigTankWreck.Rattle"/>; removes itself when done. View only.
    /// </summary>
    public sealed class OzBigTankWreckRattle : MonoBehaviour
    {
        /// <summary>Seconds over which the small sounds are spread.</summary>
        public static float Span = 1.9f;
        /// <summary>Proof: small debris sounds asked for.</summary>
        public static int Fired;
        readonly List<Transform> pieces = new List<Transform>();
        float[] at, vol, pitch;
        int next, id;
        float t;
        bool creak;

        /// <summary>6-10 small sounds (one per 8 pieces) at staggered times, volumes 0.30-0.60, pitch 1.05-1.35.</summary>
        public void Begin(List<Transform> made, int unitId)
        {
            pieces.AddRange(made); id = unitId;
            int n = Mathf.Clamp(made.Count / 8, 6, 10);
            at = new float[n]; vol = new float[n]; pitch = new float[n];
            for (int i = 0; i < n; i++)
            {
                at[i] = 0.12f + Span * (i + RobotDeathRule.Unit(id, i, 61u) * 0.8f) / n;
                vol[i] = 0.30f + 0.30f * RobotDeathRule.Unit(id, i, 62u);
                pitch[i] = 1.05f + 0.30f * RobotDeathRule.Unit(id, i, 63u);
            }
        }

        void Update()
        {
            if (at == null) { Destroy(gameObject); return; }
            t += Time.deltaTime;
            while (next < at.Length && t >= at[next])
            {
                AudioDirector.Fire("dst_debris_metal", Pick(next), id, vol[next], pitch[next]);
                Fired++; next++;
            }
            if (!creak && t >= Span * 0.62f) { creak = true; AudioDirector.Fire("dst_wreck_creak", Pick(97), id, 0.55f); Fired++; }
            if (next >= at.Length && creak) Destroy(gameObject);
        }

        Vector3 Pick(int i)
        {
            int n = pieces.Count;
            for (int k = 0; k < n; k++)
            {
                var c = pieces[(int)((RobotDeathRule.Hash(id, i, 64u) + (uint)k) % (uint)n)];
                if (c) return c.position;
            }
            return transform.position;
        }
    }
}
