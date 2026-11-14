using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 range_rings_option: OPTIONS RANGE RINGS ON SELECTED UNITS = ALWAYS (UI/OzRangeRingsOption.cs). The rings of the player's selected units
    /// burst out as before, then stay at full size and strength while the units are selected (OzRingHold: the ring's age stops at the end of its
    /// hold). While ALWAYS keeps them, the rings are made again, settled (no new burst), when they no longer fit the selection: a member's range
    /// changed (DV162 high ground: RingRadius), a member walked more than twice mergeDistance from its ring's middle (the group spread out), or a
    /// selected unit with a range has no ring (an APC that got its crew, the switch to ALWAYS after the rings had faded: then with the burst).
    /// Looked at 5 times a second (KeepEvery), no allocations.
    /// FADE (the default): none of this runs; the rings are exactly as before.
    /// Hooks in AttackRangeRings.cs: Ring.held, Begin (held = false), LateUpdate (KeepRings when the selection did not change; RingAge for the age).
    /// </summary>
    public partial class AttackRangeRings
    {
        /// <summary>ALWAYS keeps this ring: the option is ALWAYS and the ring is the player's (in the Unit editor's playground every pick is).</summary>
        bool Keep(Ring r) => OzRangeRingsOption.Always && (OzUeHooks.Active || (game && r.team == game.player));

        /// <summary>The age a selection ring is drawn at this frame (OzRingHold; FADE: now - t0, as before).</summary>
        float RingAge(Ring r, float now) => OzRingHold.Age(ref r.t0, ref r.held, now, Keep(r), expandTime + holdTime);

        /// <summary>LateUpdate, the selection unchanged and no box drag: under ALWAYS, make the rings again when they no longer fit it.</summary>
        void KeepRings(List<ZObject> sel)
        {
            if (!OzRangeRingsOption.Always) return;
            float now = Time.unscaledTime;
            if (now < nextKeep && now > nextKeep - 1f) return;                                               // looked at 5 times a second (a clock jump back: at once)
            nextKeep = now + KeepEvery;
            int s = KeptStale(sel);
            if (s == 0) return;
            if (s == 3) { DropAll(); return; }
            Rebuild(sel);
            if (s == 2) Settle();
        }

        /// <summary>0: the rings fit (or FADE, or a ring is still bursting); 1: make them again with the burst (no ring was held); 2: make them
        /// again settled (held rings no longer fit, a member lost its range); 3: no selected unit has a range any more (all emptied): fade them out.</summary>
        int KeptStale(List<ZObject> sel)
        {
            if (!OzRangeRingsOption.Always || !game) return 0;
            int want = 0, have = 0; bool held = false;
            foreach (var o in sel) if (o != null && !o.Destroyed && o.AttackRadius > 0 && (OzUeHooks.Active || o.Owner == game.player)) want++;
            float far = 4f * mergeDistance * mergeDistance;                                                  // (2 x mergeDistance)^2: Rebuild groups within mergeDistance
            bool moved = false;
            foreach (var r in rings)
            {
                if (r.dropAt >= 0f) continue;                                                                // its units left the selection: fading out
                if (!r.held) return 0;                                                                       // still bursting: wait for its hold
                held = true;
                foreach (var o in r.units)
                {
                    if (o == null || o.Destroyed || !curSel.Contains(o.Id)) continue;                        // leaves the ring this frame (LateUpdate)
                    if (!(OzUeHooks.Active || o.Owner == game.player)) continue;                             // counted as want is
                    if (o.AttackRadius <= 0) { moved = true; continue; }                                     // review: lost its range (a vehicle emptied, ZObject.cs:152): Rebuild draws none for it
                    have++;
                    if (o.RingRadius != r.radiusPx) moved = true;
                    else
                    {
                        var p = game.Views.TryGetValue(o.Id, out var v) && v ? v.transform.position : game.CenterWorld(o, false);
                        float dx = p.x - r.center.x, dz = p.z - r.center.z;
                        if (dx * dx + dz * dz > far) moved = true;
                    }
                }
            }
            if (!moved && have == want) return 0;
            if (want == 0) return 3;                                                                         // review: no selected unit has a range any more: the held rings fade out (dropFade)
            return held ? 2 : 1;
        }

        /// <summary>The rings just made by KeepRings start in their hold: full size and strength, no burst.</summary>
        void Settle()
        {
            float now = Time.unscaledTime;
            foreach (var r in rings) { r.t0 = now - (expandTime + holdTime); r.held = true; }
            keptRebuilds++;
        }

        int keptRebuilds; float nextKeep = -1f;
        const float KeepEvery = 0.2f;

        // ------------------------------------------------------------------ the check's hooks (Editor/OzV14RangeRingsCheck.cs)
        /// <summary>Selection rings shown now (not the gun being placed).</summary>
        public int ProofRingCount => rings.Count;
        /// <summary>The lowest and highest alpha of the shown rings whose units are still selected (-1 when there is none).</summary>
        public void ProofRingAlpha(out float min, out float max, out int selected, out int heldN)
        {
            min = -1f; max = -1f; selected = 0; heldN = 0;
            foreach (var r in rings)
            {
                if (r.held) heldN++;
                if (r.dropAt >= 0f) continue;
                selected++;
                if (min < 0f || r.a < min) min = r.a;
                if (r.a > max) max = r.a;
            }
        }
        /// <summary>How often ALWAYS made the rings again settled (KeepRings) since the start.</summary>
        public int ProofKeptRebuilds => keptRebuilds;
        /// <summary>The ring timing: burst, hold, fade, the fade on leaving the selection, the alpha at full strength.</summary>
        public void ProofTiming(out float expand, out float hold, out float fade, out float drop, out float full)
        { expand = expandTime; hold = holdTime; fade = fadeTime; drop = dropFade; full = alpha; }
    }
}
