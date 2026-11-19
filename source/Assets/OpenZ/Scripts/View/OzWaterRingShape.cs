// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 notes N1 (view only): the foam / ripple ring the water shaders draw round a wreck, a wreck piece or a hull dipping into the water
    /// (Shaders/OpenZWaterContact.hlsl, the contacts OzWaterLanding.PublishContacts and OzVehicleWaterDip.Contacts publish) was the rounded box
    /// of the piece's footprint: from the RTS camera a rounded SQUARE of foam round the wreck. It is now a natural ring that follows the
    /// waterline: a soft ellipse round the footprint (no straight edge anywhere), its radius broken by slow drifting angular noise, a soft
    /// noisy foam edge whose width varies round the outline, two gentle ripples travelling out, and the clearer water over the submerged part
    /// the same organic shape just inside the ring. Wading robots keep their round ring (they always were circles).
    /// <para>The shader reads the global _OZContactBoxy: unset or 0 = the organic ring (the default, nothing has to run), 1 = the old rounded box.
    /// <see cref="Organic"/></para>
    /// </summary>
    public static class OzWaterRingShape
    {
        static readonly int BoxyId = Shader.PropertyToID("_OZContactBoxy");
        static bool organic = true;

        /// <summary>True (default) = the organic waterline ring; false = the old rounded box (proof only).</summary>
        public static bool Organic
        {
            get => organic;
            set { organic = value; Shader.SetGlobalFloat(BoxyId, value ? 0f : 1f); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { organic = true; Shader.SetGlobalFloat(BoxyId, 0f); }   // a Play run in the editor never keeps a proof's box

        public static string Status() => "water contact ring: " + (organic ? "organic waterline (1.5 notes N1)" : "old rounded box (proof)");
    }
}
