// OpenZ (GPL-3.0) - version 1.4, speed-up 2 (fewer garbage-collector stutters), sound only. The engine loops' LateUpdate made a closure
// object every frame (its sort captured the camera focus) and a new delegate at every reselect. One kept comparison now reads the focus
// the caller sets just before the sort: the same order, the same audible engines.
using System;
using UnityEngine;

namespace OpenZ
{
    public partial class LegacyEngineAudio
    {
        Vector3 gcFocus; Comparison<UnitView> gcByScore;

        /// <summary>The old (a, b) => Score(a, focus).CompareTo(Score(b, focus)), kept once.</summary>
        Comparison<UnitView> ByScore(Vector3 focus) { gcFocus = focus; return gcByScore ??= (a, b) => Score(a, gcFocus).CompareTo(Score(b, gcFocus)); }
    }
}
