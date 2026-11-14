// OpenZ (GPL-3.0) - version 1.4, speed-up 2 (fewer garbage-collector stutters), sound only. Three per-frame lines of the sound director
// made new objects every frame: the duck clean-up (a closure and its delegate), the crane-work and the gatling spin-down loops (a new
// list of keys each). Now one kept delegate and two kept key lists: the same keys in the same order, the same sounds at the same time.
using System;
using System.Collections.Generic;

namespace OpenZ
{
    public partial class AudioDirector
    {
        float gcDuckNow; Predicate<Duck> gcDuckOver;
        readonly List<int> gcCraneKeys = new List<int>(), gcGatKeys = new List<int>();

        /// <summary>StepDucks: the old x => x.until &lt; now, kept once.</summary>
        Predicate<Duck> DuckOver(float now) { gcDuckNow = now; return gcDuckOver ??= x => x.until < gcDuckNow; }

        /// <summary>The keys of d in its own order (what Keys.ToList() gave) in a kept list.</summary>
        static List<int> KeysInto<T>(Dictionary<int, T> d, List<int> buf) { buf.Clear(); buf.AddRange(d.Keys); return buf; }
    }
}
