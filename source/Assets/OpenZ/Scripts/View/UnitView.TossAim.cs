// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A tank's or a gun's thrown turret (TossTurret, UpdateToss; tower guns too) flew to the rules' debris point with its pivot ending 0.35 m
    /// over the ground, whatever pose it lands in. Lying on its side its lowest vertex was then up to a metre and more in the ground on the
    /// landing frame (the barrel through the ground) and the heavy landing lifted it out in 0.08 s: the turret jumped up out of the ground.
    /// And when the wreck sank away the turret went down 1.25 m only, then was removed while it still stood up to a metre over the ground.
    /// <see cref="TossAimGround"/>: the pose it lands in (the same draw: on its side, turned so it lies on the trees it fells) is picked when
    /// it is thrown instead of 0.6 s before it lands, and the arc's end is moved up or down so its real lowest vertex lies 5 cm in the ground
    /// (OzHeavyLanding.Sink, the heavy landing's own fit) on the landing frame: one smooth arc from the hull to the ground, the same spot (x, z),
    /// flight time and apex, the same ease of the spin into that pose (row 25). Over open water nothing changes (the water rule takes it).
    /// <see cref="TossSinkFull"/>: the landed turret sinks with the hull, on the hull's own curve, its whole height + 15 cm (at least the 1.25 m
    /// of before), so it is under the ground when the wreck is removed. View only. Off = as before this kit.
    /// </summary>
    public partial class UnitView
    {
        public static bool TossAimGround = true, TossSinkFull = true;
        /// <summary>Play check: arcs moved, the last arc's move (m).</summary>
        public static int TossAimed; public static float LastTossAimMove;
        bool tossRestPicked, tossFadeOn; float tossFadeDepth, tossFadeK, tossAimMove;

        /// <summary>Play check: the thrown turret (null before the throw), whether it landed, its flight time (s).</summary>
        public Transform TossedPiece => tossed;
        public bool TossedLanded => tossLanded;
        public float TossFlightSeconds => tossDur;

        /// <summary>TossTurret, once the landing point and the arc are set: the landing pose now, the arc's end at its ground.</summary>
        void TossAimRest()
        {
            tossRestPicked = false; tossAimMove = 0f;
            if (!TossAimGround || !tossed || !OzHeavyLanding.Enabled || !OzHeavyLanding.Turrets) return;
            if (OzWaterLanding.OpenWaterAt(tossTo, out _)) return;
            var rest = Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(100f, 160f));   // lands on its side (UpdateToss's own draw)
            tossEaseRest = OzTreeCrush.TurretPick(tossed, tossTo, rest, Obj);                                       // item 360: on the trees it fells (as UpdateToss)
            tossRestPicked = true;
            float gap = OzHeavyLanding.AimGap(tossed, tossTo, tossEaseRest, out bool ok);
            if (!ok) return;
            LastTossAimMove = tossAimMove = -OzHeavyLanding.Sink - gap;
            tossTo.y += LastTossAimMove; TossAimed++;
        }

        /// <summary>UpdateToss, the landing frame: where its dust and clang go (TossLanded): the landing point before the arc's end was moved, so
        /// the demolition dust (placed from that point up) stays on the ground as before.</summary>
        Vector3 TossDustAt(Vector3 p) => new Vector3(p.x, p.y - tossAimMove, p.z);

        /// <summary>BeginFade: how far the landed turret goes down while the wreck sinks. A turret still in the air (a flipped hull that gives
        /// way, OzGiveWay, can begin its fade before the turret is down) keeps the old 0.5 m/s, so it is never held in the air.</summary>
        void TossFadeBegin()
        {
            tossFadeOn = false; tossFadeK = 0f;
            if (!tossed || !tossLanded) return;
            var tossFadeFrom = tossed.position;
            float top = float.MinValue;
            foreach (var r in tossed.GetComponentsInChildren<Renderer>())
            {
                if (!r || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                top = Mathf.Max(top, r.bounds.max.y);
            }
            float before = 0.5f * WreckFx.FadeSeconds;                                                               // what it went down before this kit
            float ground = TossFadeGround(tossFadeFrom);                                                             // open_fixes_2nov update 1: the lowest ground under its drawn box (UnitView.TossWhole.cs)
            tossFadeDepth = top > float.MinValue ? Mathf.Max(before, top - ground + 0.15f) : before;
            tossFadeOn = true;
        }

        /// <summary>UpdateFade: the turret on the hull's curve (k 0..1), step by step like the old line (a water, lava or landing move of the same
        /// frame is kept, never overwritten); false = off or still in the air (the old 0.5 m/s).</summary>
        bool TossFadeStep(float k)
        {
            if (!TossSinkFull || !tossed || !tossFadeOn) return false;
            tossed.position += Vector3.down * tossFadeDepth * (k - tossFadeK); tossFadeK = k;
            return true;
        }
    }
}
