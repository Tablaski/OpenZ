using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 los_fog (final pass 14h): the crew figures this view draws for a vehicle (the jeep's driver, a hatch commander, a seated gunner, a
    /// robot boarding or climbing out) live under OZ_BoardingCrew, not under the vehicle's own view, so the fog's HideCovered (View/OzFogOfWar.Render.cs)
    /// adds them to the renderers it holds off for a hidden vehicle: a jeep hidden in the line-of-sight fog no longer leaves its driver standing there.
    /// </summary>
    public partial class BoardingView
    {
        static readonly List<Renderer> fogTmp = new List<Renderer>();

        /// <summary>Appends the renderers of vehicle view v's crew figure and of every robot boarding it or leaving it now.</summary>
        public static void AddCrewRenderers(UnitView v, List<Renderer> into)
        {
            if (!I || !v || into == null) return;
            if (I.rigs.TryGetValue(v, out var r) && r.crew) { r.crew.GetComponentsInChildren(true, fogTmp); into.AddRange(fogTmp); }
            for (int i = 0; i < I.ghosts.Count; i++)
            {
                var g = I.ghosts[i];
                if (g == null || !g.go || g.rig == null || g.rig.view != v) continue;
                g.go.GetComponentsInChildren(true, fogTmp); into.AddRange(fogTmp);
            }
            fogTmp.Clear();
        }
    }
}
