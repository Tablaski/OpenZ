// OpenZ (GPL-3.0)
// under the hold end in time (AnticDirector.FlyoverAll.cs: FlyoverEndLead s before it, the props away as the player gets control).
// The driver of a flight may say it exactly (SecondsToHandBack: the blue runner's clock to its plan's end); without that the plan itself
// answers (the rest of its last shot, which the drivers hand back at), and once the hand-back has begun, the rest of its ease or dip.
// Read only: nothing of the flight changes.
using System;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class ZodFlyover
    {
        /// <summary>The flight's driver: seconds until it calls HandBack (NaN = it does not know). Cleared at every boot.</summary>
        public static Func<float> SecondsToHandBack;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAnticsSignal() { SecondsToHandBack = null; }

        /// <summary>Seconds until this flight hands the camera back: the driver's own word when it gives one, else the rest of the last shot of
        /// the cinematic plan; once the hand-back has begun the rest of its ease / dip (0 when the camera is home). -1 = unknown (an orbit flight
        /// without a plan, or a plan that is over while the driver still speaks: the hold ends them then).</summary>
        public float SecondsToHome
        {
            get
            {
                if (!Active) return 0f;
                if (cineHanding) return handT < 0f ? 0f : handDip ? Mathf.Max(0f, SkipDipOut + SkipDipIn - handT) : Mathf.Max(0f, HandEase - handT);
                if (SecondsToHandBack != null)
                {
                    float s = float.NaN;
                    try { s = SecondsToHandBack(); } catch (Exception) { s = float.NaN; }
                    if (!float.IsNaN(s)) return Mathf.Max(0f, s);
                }
                if (cine == null || cine.Count == 0) return -1f;
                var last = cine[cine.Count - 1];
                float left = last.start + last.dur - cineT;
                return left > 0f ? left : -1f;
            }
        }
    }
}
