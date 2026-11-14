// OpenZ (GPL-3.0)
// THE ALIVE LAYER UNDER A LEVEL-START FLYOVER, and what it answers.
// The first pass (AnticDirector.FlyoverAll.cs) drives every standing robot into the phone or the rocket-fuel drink. Measured on the bones
// every frame (OzBlueFlyoverProof.Checks3.cs) two things were still wrong:
//  * on the drive's first frame only the squad leaders began: every squad member was refused by a busy rule of the held simulation and
//    came 1-3 s later, in the very shot the camera opens on (the drive's own "everyone covered" could not see it: a refused robot costs no
//    budget, and the restart gap is only measured on a robot that restarts). Under the hold the simulation does not tick, so an order, a
//    sim velocity, a drawn step (the view's own spacing nudge), a selection or an enemy standing in the distance is no reason to keep a
//    robot out of its antic: UnitView.AnticBusyReason stands those rules down while the drive runs (dead, hit, knocked, a throw, a grab, a
//    stance, the fort defender and an ending still count); the drive counts every refusal by its reason and notes every robot's first
//    start (FlyoverRefusedBy, FlyoverLatestFirstStart, FlyoverLateStarts; the proof prints them);
//  * nearly every robot kept one pose for about a second at some point of the flight (the antic clips hold still where a listener or a
//    drinker would; a robot the drive could not start stands in the calm idle, a one-pixel motion at the flyover's distances).
// This layer runs after every other bone writer (execution order 96: after UnitView 60, RobotLegIK 70, WeaponHold 80 and the gestures 95)
// on every antic-capable robot whose body is drawn while the drive owns the hold:
//  * in an antic: a small living motion on top of the clip, the head turning a few degrees and the chest breathing, slow, each robot on
//    its own phase, so no pose is held frame for frame;
//  * without one (refused: a stance, a hit, the hero squad's grace): a visible look-around, the head sweeping some twenty degrees and the
//    torso shifting its weight from side to side, so the robot is never still.
// World-space deltas on the head, the upper chest and the spine, applied only while the robot's skin is visible (a culled Animator writes
// no bones, and a delta there would add up frame after frame); the weight eases in at the hold and out in the drive's ending, before the
// hand-back, so the player gets control with the clips' own poses. View only. Play check: AnticDirector.FlyoverAliveStatus().
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class AnticDirector
    {
        /// <summary>The alive layer under the drive (false = the clips alone).</summary>
        public static bool FlyoverAlive = true;
        /// <summary>In an antic: the head's turn and nod (deg) and the chest's breathing lean (deg); without one: the look-around's sweep and nod (deg) and the weight shift of the spine (deg).</summary>
        public static float AliveHeadDeg = 3f, AliveHeadNodDeg = 1.5f, AliveChestDeg = 1f, AliveLookDeg = 22f, AliveLookNodDeg = 3f, AliveShiftDeg = 2.5f;
        /// <summary>The weight's ease in (s) at the hold and ease out (s) in the drive's ending.</summary>
        public static float AliveEaseIn = 0.4f, AliveEaseOut = 0.3f;
        /// <summary>Counts (the Play proof): frames the layer wrote, robots a frame (max), robots without an antic a frame (max), the weight now.</summary>
        public static int AliveFrames, AliveRobotsMax, AliveFallbackMax;
        public static float AliveWeight;

        /// <summary>A hold begins (and every boot): the layer's counts start anew.</summary>
        static void AliveReset() { AliveFrames = AliveRobotsMax = AliveFallbackMax = 0; AliveWeight = 0f; }

        /// <summary>FlyoverBegin: the layer on the director's object (made once a level, it goes with the level).</summary>
        void AliveEnsure()
        {
            if (!FlyoverAlive) return;
            var l = GetComponent<FlyoverAliveLayer>(); if (!l) l = gameObject.AddComponent<FlyoverAliveLayer>();
            l.enabled = true;
        }

        /// <summary>Play check: one line.</summary>
        public static string FlyoverAliveStatus() =>
            $"alive layer {(FlyoverAlive ? "on" : "OFF")}: weight {AliveWeight:F2}, wrote {AliveFrames} frames, at most {AliveRobotsMax} robots a frame ({AliveFallbackMax} of them without an antic); "
            + $"head {AliveHeadDeg:F1} / {AliveHeadNodDeg:F1} deg, chest {AliveChestDeg:F1} deg in an antic; look-around {AliveLookDeg:F0} / {AliveLookNodDeg:F0} deg, weight shift {AliveShiftDeg:F1} deg without one";
    }

    /// <summary>The alive layer (AnticDirector.FlyoverAlive.cs): after every other bone writer of the frame.</summary>
    [DefaultExecutionOrder(96)]
    public sealed class FlyoverAliveLayer : MonoBehaviour
    {
        sealed class Bones { public Transform head, chest, spine; public SkinnedMeshRenderer skin; public float seed; }
        readonly Dictionary<int, Bones> bones = new Dictionary<int, Bones>();
        float weight;

        void LateUpdate()
        {
            if (!AnticDirector.FlyoverDriving) { weight = 0f; AnticDirector.AliveWeight = 0f; return; }    // no hold: the clips' own poses
            bool on = AnticDirector.FlyoverAlive && !AnticDirector.FlyoverEnding;
            float dt = Time.deltaTime;
            weight = Mathf.MoveTowards(weight, on ? 1f : 0f, dt / Mathf.Max(0.05f, on ? AnticDirector.AliveEaseIn : AnticDirector.AliveEaseOut));
            AnticDirector.AliveWeight = weight;
            if (weight <= 0f) return;
            var g = ZGame.I; if (!g || g.Views == null) return;
            float t = Time.time; int robots = 0, fallback = 0;
            foreach (var v in g.LiveViewValues)
            {
                if (!v || !v.isActiveAndEnabled || v.Obj == null || v.Obj.Kind != ZKind.Robot || v.Obj.Destroyed || v.Dying || !v.CanAntic) continue;
                var b = Of(v); if (b == null || !b.skin || !b.skin.isVisible) continue;              // not drawn = not written (the Animator writes no bones there either)
                bool antic = v.AnticActive;
                float ph = b.seed;
                var front = v.AnticFront; var right = Vector3.Cross(Vector3.up, front); if (right.sqrMagnitude < 1e-6f) right = Vector3.right; right.Normalize();
                float yaw, nod, lean, shift;
                if (antic)
                {   // on top of the clip: a few degrees, two slow waves that never line up, the chest breathing
                    yaw = AnticDirector.AliveHeadDeg * (0.7f * Mathf.Sin(t * 2.17f + ph) + 0.3f * Mathf.Sin(t * 0.83f + 2f * ph));
                    nod = AnticDirector.AliveHeadNodDeg * Mathf.Sin(t * 1.53f + 1.3f * ph);
                    lean = AnticDirector.AliveChestDeg * Mathf.Sin(t * 1.76f + 0.7f * ph);
                    shift = 0f;
                }
                else
                {   // no antic (refused): a look-around and a weight shift the camera can see
                    yaw = AnticDirector.AliveLookDeg * (0.7f * Mathf.Sin(t * 1.85f + ph) + 0.3f * Mathf.Sin(t * 0.71f + 2f * ph));
                    nod = AnticDirector.AliveLookNodDeg * Mathf.Sin(t * 1.31f + 1.3f * ph);
                    lean = AnticDirector.AliveChestDeg * 1.5f * Mathf.Sin(t * 1.76f + 0.7f * ph);
                    shift = AnticDirector.AliveShiftDeg * Mathf.Sin(t * 1.65f + 0.4f * ph);
                    fallback++;
                }
                if (b.spine && shift != 0f) b.spine.rotation = Quaternion.AngleAxis(shift * weight, front) * b.spine.rotation;
                if (b.chest) b.chest.rotation = Quaternion.AngleAxis(lean * weight, right) * b.chest.rotation;
                if (b.head) b.head.rotation = Quaternion.AngleAxis(yaw * weight, Vector3.up) * Quaternion.AngleAxis(nod * weight, right) * b.head.rotation;
                robots++;
            }
            if (robots > 0)
            {
                AnticDirector.AliveFrames++;
                AnticDirector.AliveRobotsMax = Mathf.Max(AnticDirector.AliveRobotsMax, robots);
                AnticDirector.AliveFallbackMax = Mathf.Max(AnticDirector.AliveFallbackMax, fallback);
            }
        }

        Bones Of(UnitView v)
        {
            int id = v.Obj.Id;
            if (bones.TryGetValue(id, out var b)) return b;
            var an = v.GetComponentInChildren<Animator>();
            if (!an || !an.isHuman || !an.isInitialized) return null;                                 // not kept: asked again next frame
            b = new Bones { head = an.GetBoneTransform(HumanBodyBones.Head), spine = an.GetBoneTransform(HumanBodyBones.Spine), skin = v.GetComponentInChildren<SkinnedMeshRenderer>() };
            b.chest = an.GetBoneTransform(HumanBodyBones.UpperChest); if (!b.chest) b.chest = an.GetBoneTransform(HumanBodyBones.Chest);
            b.seed = ((id * 0.6180339f) % 1f) * 6.2831853f;
            if (!b.head && !b.chest) return null;
            bones[id] = b; return b;
        }
    }
}
