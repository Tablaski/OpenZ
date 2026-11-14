using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// His picture showed the SNIPER; to "the SNIPER leans in, both fists on the box (1 cm off it); seat, gun, others as now" he
    /// wrote "this ok". The seated_gunner clip holds the sniper's fists in front of his chest, 29 cm short of the box's back plate, and his
    /// boots stop a seat move at 4 cm. So for a robot type listed in <see cref="SeatReach"/> on that seat (the sniper on the seat key
    /// "gun_v2": the field gun and the fort tower gun, the same seat drawn x 0.72), after the clip has posed him this frame:
    /// <list type="bullet">
    /// <item>his upper body (the Spine bone, about its own joint) turns <see cref="SeatReachPose.square"/> about the turret's up, the way
    /// that brings his shoulders toward square to the box, then bends <see cref="SeatReachPose.lean"/> forward toward the box;</item>
    /// <item>each arm, two bones, takes the wrist to <see cref="SeatReachPose.wristL"/> / <see cref="SeatReachPose.wristR"/> (CannonTurretYaw's
    /// space: there the fist rests 1 cm off the box's back plate), the elbow bending toward <see cref="SeatReachPose.elbowL"/> /
    /// <see cref="SeatReachPose.elbowR"/>; the hand keeps the clip's own turn.</item>
    /// </list>
    /// The targets are read from the turret every frame, so the pose holds while the turret turns, the barrel pitches (the box does not)
    /// and the turret kicks; a driver-hit jolt moves him and his hands together. Hips, legs, the seat, his size, every other type and every
    /// other seat: as before. View only (nothing under Scripts/Sim). The pose was measured offline with this exact maths on the game's own
    /// It comes in after he has sat down (the boarding hop lands as before) and goes out as he gets up, over <see cref="ReachTime"/> of game
    /// time, eased: no bone turns more than about 2.7 deg a frame at 60 fps (5.3 at 30). A gunner that appears seated (a manned gun of the
    /// level) is in the pose at once. Off: <see cref="SeatsV2"/> false, or his row taken out of <see cref="SeatReach"/>.
    /// </summary>
    public partial class BoardingView
    {
        /// <summary>A seated gunner's reach: degrees of the upper body's turn toward square and of its lean toward the box; each wrist's place
        /// and the point its elbow bends toward, in the seat's parent space (the gun v2: CannonTurretYaw, -z = toward the box). The x signs
        /// name his left (+x) and right; in the game each side's x sign follows the side his shoulder is on.</summary>
        public struct SeatReachPose
        {
            public float lean, square; public Vector3 wristL, wristR, elbowL, elbowR;
            public SeatReachPose(float lean, float square, Vector3 wristL, Vector3 wristR, Vector3 elbowL, Vector3 elbowR)
            { this.lean = lean; this.square = square; this.wristL = wristL; this.wristR = wristR; this.elbowL = elbowL; this.elbowR = elbowR; }
        }

        /// <summary>Seat key (<see cref="SeatKey"/>) -> robot type -> his reach. The sniper on the gun v2: lean 25, turn 20 (his shoulders sit
        /// 17 deg off square in the clip), both fists 1.0 cm off the box's back plate.</summary>
        public static readonly Dictionary<string, Dictionary<string, SeatReachPose>> SeatReach = new Dictionary<string, Dictionary<string, SeatReachPose>>
        {
            ["gun_v2"] = new Dictionary<string, SeatReachPose>
            {
                { "sniper", new SeatReachPose(25f, 20f, new Vector3(0.20f, 1.02f, 0.8666f), new Vector3(-0.20f, 1.02f, 0.8708f),
                                              new Vector3(0.285f, 0.993f, 1.188f), new Vector3(-0.33f, 1.047f, 1.176f)) },
            },
        };

        /// <summary>Seconds of game time the reach takes to come in (after he sat down) and to go out (as he gets up), eased.</summary>
        public static float ReachTime = 0.7f;
        /// <summary>Counters (the checks): frames a figure was posed reaching; frames his bones still held the last pose (the Animator does
        /// not write an off-screen figure) and the clip's pose was put back first.</summary>
        public static int ReachFrames, ReachKeptFrames;

        sealed class ReachState
        {
            public Transform spine, lUp, lLo, lHand, rUp, rLo, rHand; public Transform[] bones; public bool ok;
            public float phase; public int frame = -1;
            public bool cached; public Vector3 tL, tR, eL, eR, up, front;                                   // the last seated frame's targets, in his root's space
            public readonly Quaternion[] clip = new Quaternion[5], wrote = new Quaternion[5]; public bool wroteOk;
        }
        readonly Dictionary<GameObject, ReachState> reachOf = new Dictionary<GameObject, ReachState>();

        static ReachState NewReach(GameObject go, Animator an)
        {
            var s = new ReachState();
            if (an && an.isHuman)
            {
                s.spine = an.GetBoneTransform(HumanBodyBones.Spine);
                s.lUp = an.GetBoneTransform(HumanBodyBones.LeftUpperArm); s.lLo = an.GetBoneTransform(HumanBodyBones.LeftLowerArm); s.lHand = an.GetBoneTransform(HumanBodyBones.LeftHand);
                s.rUp = an.GetBoneTransform(HumanBodyBones.RightUpperArm); s.rLo = an.GetBoneTransform(HumanBodyBones.RightLowerArm); s.rHand = an.GetBoneTransform(HumanBodyBones.RightHand);
            }
            foreach (var x in go.GetComponentsInChildren<Transform>(true))                                   // the BS-01 skeleton's names (as LightV2HatchArms)
            {
                string n = x.name;
                if (!s.spine && n.EndsWith("mixamorigSpine")) s.spine = x;
                else if (!s.lUp && n.EndsWith("mixamorigLeftArm")) s.lUp = x; else if (!s.lLo && n.EndsWith("mixamorigLeftForeArm")) s.lLo = x; else if (!s.lHand && n.EndsWith("mixamorigLeftHand")) s.lHand = x;
                else if (!s.rUp && n.EndsWith("mixamorigRightArm")) s.rUp = x; else if (!s.rLo && n.EndsWith("mixamorigRightForeArm")) s.rLo = x; else if (!s.rHand && n.EndsWith("mixamorigRightHand")) s.rHand = x;
            }
            s.ok = s.spine && s.lUp && s.lLo && s.lHand && s.rUp && s.rLo && s.rHand;
            if (s.ok) s.bones = new[] { s.spine, s.lUp, s.lLo, s.rUp, s.rLo };
            return s;
        }

        /// <summary>Hook, after the figure is placed and its clip asked for: PlaceSeated (seated: on the seat) and Place (a boarding or
        /// leaving figure). Does nothing unless <see cref="SeatReach"/> lists this seat and his type.</summary>
        void GunReach(Rig r, GameObject go, Animator an, Transform hips, bool seated)
        {
            if (!SeatsV2 || r == null || r.mode != Mode.Cannon || r.standGunner || r.seatKey == null || !go || !r.gunnerSeat || !r.gunnerSeat.parent) return;
            if (!SeatReach.TryGetValue(r.seatKey, out var row) || !row.TryGetValue(FigureType(go) ?? "grunt", out var pose)) return;   // no RobotV3 = the BS-01 grunt
            if (!reachOf.TryGetValue(go, out var st))
            {
                st = NewReach(go, an); st.phase = seated ? 1f : 0f;                                          // seen first on the seat: in the pose at once; boarding: after he sits
                reachOf[go] = st;
                if (reachOf.Count > rigs.Count + 32) ReachPurge();
            }
            if (!st.ok) return;
            if (st.frame != Time.frameCount)
            {
                st.frame = Time.frameCount;
                st.phase = Mathf.MoveTowards(st.phase, seated ? 1f : 0f, Time.deltaTime * SimRate / Mathf.Max(0.01f, ReachTime));
            }
            var b = st.bones;
            bool kept = st.wroteOk;                                                                          // nothing posed him since this wrote (off screen; a second call this frame)
            for (int i = 0; kept && i < b.Length; i++) kept = b[i].localRotation == st.wrote[i];
            if (kept) { for (int i = 0; i < b.Length; i++) b[i].localRotation = st.clip[i]; ReachKeptFrames++; }   // start again from the clip's pose
            else for (int i = 0; i < b.Length; i++) st.clip[i] = b[i].localRotation;
            st.wroteOk = false;
            float w = Mathf.SmoothStep(0f, 1f, st.phase);
            var t = go.transform; var yaw = r.gunnerSeat.parent;
            Vector3 up, front;
            if (seated) { up = yaw.up; front = Vector3.ProjectOnPlane(-yaw.forward, up).normalized; }        // the turret's up and its front (-z: the box)
            else if (st.cached) { up = t.TransformDirection(st.up).normalized; front = t.TransformDirection(st.front).normalized; }   // getting up: his own, as on the seat
            else return;
            if (w > 0f)
            {   // 1. the upper body: turned toward square to the box, then bent toward it
                var s = Vector3.ProjectOnPlane(st.rUp.position - st.lUp.position, up);
                var p = Vector3.Cross(up, front); if (Vector3.Dot(p, s) < 0f) p = -p;
                if (pose.square > 0f && s.sqrMagnitude > 1e-10f)
                    st.spine.rotation = Quaternion.AngleAxis((Vector3.Dot(Vector3.Cross(s, p), up) >= 0f ? 1f : -1f) * pose.square * w, up) * st.spine.rotation;
                if (pose.lean > 0f) st.spine.rotation = Quaternion.FromToRotation(up, Vector3.RotateTowards(up, front, pose.lean * w * Mathf.Deg2Rad, 0f)) * st.spine.rotation;
            }
            Vector3 aL, aR, eL, eR;
            if (seated)
            {   // 2. the wrists' places and the elbows' hints on the turret (+ a driver-hit jolt: his hands go with him)
                SeatPose(r, out var sp, out _, out _);
                var off = hips ? hips.position - (sp + SeatPlaceShift(r, go)) : Vector3.zero;
                float sx = yaw.InverseTransformPoint(st.lUp.position).x >= yaw.InverseTransformPoint(st.rUp.position).x ? 1f : -1f;   // the side his left shoulder is on
                aL = yaw.TransformPoint(ReachSide(pose.wristL, sx)) + off; aR = yaw.TransformPoint(ReachSide(pose.wristR, -sx)) + off;
                eL = yaw.TransformPoint(ReachSide(pose.elbowL, sx)) + off; eR = yaw.TransformPoint(ReachSide(pose.elbowR, -sx)) + off;
                st.tL = t.InverseTransformPoint(aL); st.tR = t.InverseTransformPoint(aR); st.eL = t.InverseTransformPoint(eL); st.eR = t.InverseTransformPoint(eR);
                st.up = t.InverseTransformDirection(up); st.front = t.InverseTransformDirection(front); st.cached = true;
            }
            else { aL = t.TransformPoint(st.tL); aR = t.TransformPoint(st.tR); eL = t.TransformPoint(st.eL); eR = t.TransformPoint(st.eR); }
            if (w <= 0f) return;
            ReachArm(st.lUp, st.lLo, st.lHand, Vector3.Lerp(st.lHand.position, aL, w), Vector3.Lerp(st.lLo.position, eL, w), -up);
            ReachArm(st.rUp, st.rLo, st.rHand, Vector3.Lerp(st.rHand.position, aR, w), Vector3.Lerp(st.rLo.position, eR, w), -up);
            for (int i = 0; i < b.Length; i++) st.wrote[i] = b[i].localRotation;
            st.wroteOk = true; ReachFrames++;
        }

        static Vector3 ReachSide(Vector3 v, float sign) => new Vector3(sign * Mathf.Abs(v.x), v.y, v.z);

        /// <summary>Two bones: upper arm a, forearm b, hand c. The wrist (hand c's joint) to target (as near as the arm reaches), the elbow on the side of the
        /// shoulder-target line where point e is (down if e is on that line); c keeps its turn against b.</summary>
        static void ReachArm(Transform a, Transform b, Transform c, Vector3 target, Vector3 e, Vector3 down)
        {
            Vector3 A = a.position, B = b.position, C = c.position;
            float lu = (B - A).magnitude, ll = (C - B).magnitude; var at = target - A; float d = at.magnitude;
            if (lu < 1e-5f || ll < 1e-5f || d < 1e-5f) return;
            var n = at / d;
            d = Mathf.Clamp(d, Mathf.Abs(lu - ll) + 1e-4f, lu + ll - 1e-4f);
            var h = Vector3.ProjectOnPlane(e - A, n);
            if (h.sqrMagnitude < 1e-10f) h = Vector3.ProjectOnPlane(down, n);
            h.Normalize();
            float ca = (lu * lu + d * d - ll * ll) / (2f * lu * d), sa = Mathf.Sqrt(Mathf.Max(0f, 1f - ca * ca));
            var B2 = A + n * (lu * ca) + h * (lu * sa);
            a.rotation = Quaternion.FromToRotation(B - A, B2 - A) * a.rotation;
            Vector3 B1 = b.position, C1 = c.position;
            b.rotation = Quaternion.FromToRotation(C1 - B1, A + n * d - B1) * b.rotation;
        }

        void ReachPurge()
        {
            var gone = new List<GameObject>();
            foreach (var k in reachOf.Keys) if (!k) gone.Add(k);
            foreach (var k in gone) reachOf.Remove(k);
        }
    }
}
