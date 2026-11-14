// OpenZ (GPL-3.0) - version 1.4, speed-up 2 (fewer garbage-collector stutters), view only. A shot the gun held until it was on target
// wrote its wait into a text at every release, and the text was kept only when that shot set a new worst aim error. The text is now
// made only then: the same statistics, the same text where it is kept, nothing drawn changes.
using UnityEngine;

namespace OpenZ
{
    public partial class UnitAim
    {
        /// <summary>Replay's statistics row: Count with the held-shot text made only when Count keeps it (a new worst error).</summary>
        void CountHeld(float err, float waited, bool timeout, float maxWait)
        {
            var st = MyStat();
            string how = err > StatMaxError || err > st.MaxErr ? $"held {waited * 1000f:F0} ms, cap {maxWait * 1000f:F0} ms" : null;
            Count(err, Mathf.Max(0.0001f, waited), timeout, how);
        }
    }
}
