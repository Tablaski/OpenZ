using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 150, the gate exit fit's leftovers (the Play check at the fort's back gate: a crane's top 0.14 m through the tunnel's low lintel
    /// step, a jeep's tail 0.06 m and a mech's arm 0.03 m through a tunnel wall; "frames with the ceiling under the drawn scale" 46). View only;
    /// the sim, its timing, paths and pick boxes are unchanged. Causes and fixes (View/UnitView.ExitFit.cs keeps the rule):
    ///  * THE BOX WAS THE REST POSE: the fit measured the drawn box once at the bind and kept it, but the drawn vehicle moves inside it on the
    ///    way out: the hull tilts and bounces on the drawn ground (the hall to tunnel floor step lifted the crane's top 0.28 m over 1.3 m of
    ///    travel), turrets and the mech's upper body turn, the mech's arms and legs swing. Now the box is measured again every frame as drawn
    ///    (<see cref="Reach"/>, the bind's own measure) and the ceiling comes from it.
    ///  * THE START FIT stood every hull on each lintel segment's floor; a hull drawn higher in the hall (or rising to it) is fitted from the
    ///    higher of the two (<see cref="StartAtDrawnHeight"/>).
    ///  * NEVER SHRINKING: when the drawn ceiling falls under the drawn scale, the old step kept the scale and the part poked through; now the
    ///    hull eases down to the ceiling (<see cref="ShrinkRate"/> of full size per second, a few % at most in practice) and grows on after it.
    ///  * THE HULL LEFT THE DOOR AXIS INSIDE THE GATE: the hull was held on the door axis only while its exit move lasted; the drawn hull lags
    ///    its sim point, so the exit move ended with the tail still in the tunnel and the hull steered toward the next point (the jeep's tail
    ///    swung into the wall). Now the hull stays on the door axis until its drawn tail has left the gate's last segment
    ///    (<see cref="HoldInGate"/>), and while it is held its drawn place is kept between the walls of every segment it overlaps (the steered
    ///    weave, the view spacing and the no-glide follower's lag can no longer push it sideways into a jamb).
    /// Switches (off = the k93 fit as before, each on its own): <see cref="Drawn"/>, <see cref="StartAtDrawnHeight"/>, <see cref="Shrink"/>,
    /// <see cref="HoldInGate"/>, <see cref="LaneHold"/>.
    /// </summary>
    public static class ExitFitK102
    {
        public static bool Drawn = true, StartAtDrawnHeight = true, Shrink = true, HoldInGate = true, LaneHold = true;
        /// <summary>How fast a hull eases down to a ceiling under its drawn scale (share of full size per second).</summary>
        public static float ShrinkRate = 1.5f;
        /// <summary>Statistics (the Play check): frames a hull eased down to its ceiling, frames its drawn place was kept between the walls, frames
        /// its hull was held on the door axis after the exit move had ended (the tail still in the gate).</summary>
        public static int Shrunk, LaneHolds, HeldFrames;

        /// <summary>The drawn box of `root`'s renderers (each mesh's box through its transform, the rest by their world box; particles, lines and
        /// trails left out, as the Play check reads it), relative to the pivot c, along dir / to its right / up, in full-size metres (the drawn
        /// scale s divided out): front, rear, left, right, top. False = nothing drawn.</summary>
        public static bool Reach(Component root, List<Renderer> rs, Vector3 c, Vector3 dir, float s, out float f, out float b, out float l, out float r, out float t)
        {
            f = b = l = r = t = 0f; bool has = false;
            root.GetComponentsInChildren(rs);
            var right = Vector3.Cross(Vector3.up, dir); float inv = 1f / Mathf.Max(1e-3f, s);
            foreach (var rd in rs)
            {
                if (!rd || !rd.enabled || !rd.gameObject.activeInHierarchy || rd is ParticleSystemRenderer || rd is LineRenderer || rd is TrailRenderer) continue;
                var mf = rd is MeshRenderer ? rd.GetComponent<MeshFilter>() : null;
                Bounds lb; Matrix4x4 m;
                if (mf && mf.sharedMesh) { lb = mf.sharedMesh.bounds; m = rd.transform.localToWorldMatrix; }
                else { lb = rd.bounds; m = Matrix4x4.identity; }                                   // skinned and the rest: their world box
                for (int i = 0; i < 8; i++)
                {
                    var w = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1))) - c;
                    float a = Vector3.Dot(w, dir) * inv, sd = Vector3.Dot(w, right) * inv, up = w.y * inv;
                    f = Mathf.Max(f, a); b = Mathf.Max(b, -a); r = Mathf.Max(r, sd); l = Mathf.Max(l, -sd); t = Mathf.Max(t, up);
                }
                has = true;
            }
            return has;
        }

        public static string Report() => $"ExitFitK102: drawn box {Drawn}, start at the drawn height {StartAtDrawnHeight}, ease down {Shrink} ({ShrinkRate:0.0}/s), held in the gate {HoldInGate}, kept between the walls {LaneHold}; " +
                                         $"frames eased down to a ceiling {Shrunk}, frames kept between the walls {LaneHolds}, frames held on the door axis after the exit move {HeldFrames}";
    }

    public partial class UnitView
    {
        readonly List<Renderer> exitRs = new List<Renderer>();

        /// <summary>ExitFitStep, before the ceiling: the box as drawn this frame (tilt, bounce, turret, arms, legs), full-size metres.</summary>
        void ExitFitDrawn()
        {
            if (!ExitFitK102.Drawn) return;
            if (ExitFitK102.Reach(this, exitRs, transform.position, exitDir, exitCur, out float f, out float b, out float l, out float r, out float t))
            { exitFront = f; exitBack = b; exitLeft = l; exitRight = r; exitTop = t; }
        }

        /// <summary>ExitFitStep: the next drawn scale: grown toward the ceiling as before, or eased down to it when it fell under the drawn scale.</summary>
        float ExitFitNext(float cur, float grown, float ceil, float dt)
        {
            if (!ExitFitK102.Shrink || ceil >= cur) return Mathf.Max(cur, grown);
            ExitFitK102.Shrunk++;
            return Mathf.MoveTowards(cur, ceil, dt * ExitFitK102.ShrinkRate);
        }

        /// <summary>ExitHull: true while a gate-profiled exit still has its drawn tail inside the gate (the hull stays on the door axis).</summary>
        bool ExitTailInGate()
        {
            if (!ExitFitK102.HoldInGate || !exitProfiled || !exitGate || exitSegs == null || exitSegs.Length == 0) return false;
            float k = Mathf.Max(1e-4f, Mathf.Abs(exitGate.lossyScale.z)), s = exitGrowing ? exitCur : 1f;
            float tail = exitGate.InverseTransformPoint(transform.position).z - exitBack * s / k;
            if (tail >= exitSegs[exitSegs.Length - 1].z1) return false;
            ExitFitK102.HeldFrames++;
            return true;
        }

        /// <summary>Sync (vehicles), right after the no-glide follower: while the hull is held on the door axis, its drawn place stays between
        /// the walls of every gate segment its box overlaps (at the drawn scale, with the fit's air); the follower takes the kept place.</summary>
        Vector3 ExitLaneHold(Vector3 p)
        {
            if (!ExitFitK102.LaneHold || !exitStraight || !exitProfiled || !exitGate || exitSegs == null) return p;
            float k = Mathf.Max(1e-4f, Mathf.Abs(exitGate.lossyScale.z)), s = exitGrowing ? exitCur : 1f, air = ExitDoors.AirSideM / k;
            var lp = exitGate.InverseTransformPoint(p);
            float tail = lp.z - exitBack * s / k, head = lp.z + exitFront * s / k, lo = float.NegativeInfinity, hi = float.PositiveInfinity;
            foreach (var g in exitWalls ?? exitSegs)                                                // 2 Nov: the gate's real walls (the fit may use the seen profile)
            {
                if (g.z1 <= tail || g.z0 >= head) continue;
                lo = Mathf.Max(lo, -g.half + air + exitLeft * s / k); hi = Mathf.Min(hi, g.half - air - exitRight * s / k);
            }
            if (float.IsInfinity(lo)) return p;
            float x = lo <= hi ? Mathf.Clamp(lp.x, lo, hi) : 0.5f * (lo + hi);
            if (Mathf.Abs(x - lp.x) < 1e-4f) return p;
            lp.x = x;
            var w = exitGate.TransformPoint(lp);
            p.x = w.x; p.z = w.z;
            if (slip != null) slip.Reset(p.x, p.z, slip.Yaw);
            ExitFitK102.LaneHolds++;
            return p;
        }
    }
}
