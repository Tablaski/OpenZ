// softer flash, for their uses in the game (View/OzExpInGame.cs). One explosion at scale k is the same explosion seen k times as big about
// its origin O: every place k times as far from O, every size x k, and its clock and speeds x sqrt(k), so a thrown piece still falls under
// the real gravity on the same arc as its smoke trail (lengths x k, times x sqrt k). flashK scales its flash (the glow and white-hot heart
// sprites, its lights and its camera kick) when it plays on top of another death's own flash. k = 1 and flashK = 1 (every Unit editor drop,
// the shuttle air burst of kit shuttle_air_burst_44) = exactly as before: every value passes unchanged.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzUeBlastFx
    {
        float k = 1f, sk = 1f, flashK = 1f;
        /// <summary>Every explosion running now (the in-game fog shape reads them: View/OzExpInGame.cs).</summary>
        internal static readonly List<OzUeBlastFx> All = new List<OzUeBlastFx>(16);
        internal int Type => type;
        internal Vector3 Origin => O;
        internal float Scale => k;
        /// <summary>Its own clock (s of the explosion, before the x sqrt k).</summary>
        internal float Clock => t;
        /// <summary>SHOCK's ground wave reaches this far at scale 1 (m).</summary>
        internal static float WaveM => ShockRingM;

        Vector3 Mp(Vector3 p) => k == 1f ? p : O + (p - O) * k;
        float FlashA(string ch, float a) => flashK != 1f && (ch == "ueb_flash" || ch == "ueb_plasma") ? a * flashK : a;
        void EmitL(string ch, Vector3 p, Vector3 v, float life, float size, Color c, float a) => z.EmitChannel(ch, Mp(p), v * sk, life * sk, size * k, c, FlashA(ch, a));
        void EmitL(string ch, Vector3 p, Vector3 v, float life, float size, Color c, float a, float delay) => z.EmitChannel(ch, Mp(p), v * sk, life * sk, size * k, c, FlashA(ch, a), delay * sk);
        void AddL(Vector3 at, Color color, float power, float range, float life, float decayPow = 2.2f, float flicker = 0f, bool whiteHot = false)
            => z.AddLight(Mp(at), color, power * flashK, range * k, life * sk, decayPow, flicker, whiteHot);
        void GroundL(Vector3 flat, float s, bool electro = false, bool blast = false) => z.GroundLight(Mp(flat), s, electro, blast, k);
        void ScorchL(Vector3 at, float s, int priority = 1) => z.Scorch(Mp(at), s * k, priority);
        void UeRingL(Vector3 flat, float radiusM, float life, float delay, Color color) => z.UeRing(Mp(flat), radiusM * k, life * sk, delay * sk, color);
    }
}
