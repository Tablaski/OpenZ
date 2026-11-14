// OpenZ (GPL-3.0) - version 1.2, the blue side's cutscenes: a small posing kit for the two imported robots (EvilZod, the sidekick).
// The models come as T-posed Generic rigs with the usual bone names (mixamorig*) and a few sockets (HeadFront, HatTop, the hand
// sockets, EvilZod's Jaw / JawOpen / JawHingeRight). Nothing here assumes any bone's own axes: every pose is written in WORLD space with
// from-to rotations and a two-bone arm solver (as the commander's fist at the camera, ZodStage.ApplyThrust), from the rest pose that is
// re-set every frame, so a pose is a pure function of its targets (no drift, no pile-up, the same picture every run).
//  - IkArm: shoulder / elbow / hand to a world target with a pole (elbow) direction; the wrist stays straight (the hand keeps its bind
//    rotation on the forearm) unless PalmTo turns it so the palm faces a direction (the palm normal is measured once from the model's own
//    Fist hand pose: the way the fingertips move when the hand closes).
//  - LookAt: the head (and a share of the neck) turns toward a point, clamped; the head's forward is the HeadFront socket at rest.
//  - Lean / Twist / ChestUp on the spine, Squash on the root (the sidekick's EVE-like body talk).
//  - Finger poses: the model's own hand-pose takes (EvilZod@Fist / GripShotgun / GripPump / HandsOpen) sampled once through a Playable
//    graph and blended per hand.
//  - Walk: the model's own Walk take through a Playable graph (loop by hand); the root is moved by the caller at WalkSpeed, measured from
//    the take itself (how far a planted foot travels under the hips in half a cycle), so the feet do not slide.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace OpenZ
{
    public sealed class OzPoseRig
    {
        public Transform Root { get; private set; }
        public Transform Armature, Hips, Spine, Spine1, Spine2, Neck, Head, HeadFront, HatTop;
        public Transform ShoulderL, ShoulderR, ArmL, ArmR, ForeArmL, ForeArmR, HandL, HandR, SocketL, SocketR, FootL, FootR, ToeL, ToeR;
        public Transform UpLegL, UpLegR, LegL, LegR;
        public Animator Anim { get; private set; }
        /// <summary>The model's standing height at rest (m, world) and the rest forward / right axes in the ROOT's frame.</summary>
        public float Height { get; private set; } = 2f;
        public Vector3 FwdLocal { get; private set; } = Vector3.forward;
        public Vector3 RightLocal { get; private set; } = Vector3.right;
        public string Problem { get; private set; } = "";
        public bool Ok => Hips && Head && ArmL && ArmR && ForeArmL && ForeArmR && HandL && HandR;

        readonly List<Transform> bones = new List<Transform>();
        readonly List<Vector3> restPos = new List<Vector3>();
        readonly List<Quaternion> restRot = new List<Quaternion>();
        readonly List<Quaternion> snapRot = new List<Quaternion>();
        readonly Dictionary<string, Quaternion[]> fingerPoses = new Dictionary<string, Quaternion[]>();
        readonly List<Transform> fingersL = new List<Transform>(), fingersR = new List<Transform>();
        Vector3 headFwdLocal = Vector3.forward, palmLocalL = Vector3.down, palmLocalR = Vector3.down; bool palmOk;
        Vector3 footPos0L, footPos0R; Quaternion footRot0L = Quaternion.identity, footRot0R = Quaternion.identity;
        public bool HasLegs => Hips && UpLegL && UpLegR && LegL && LegR && FootL && FootR;
        PlayableGraph walkGraph; AnimationClipPlayable walkPlayable; AnimationClip walkClip; bool walkOn; float walkBlend = 1f, walkBlendDur = 0.35f;   // 1: a new rig has no walk to blend from (0 blended the rest T-pose into its first 0.35 s)
        public float WalkSpeed { get; private set; } = 1f;
        /// <summary>Playback rate of the walk take (the caller moves the root at WalkSpeed x WalkRate, so the feet stay planted).</summary>
        public float WalkRate = 1f;
        public float WalkPhase => walkClip && walkPlayable.IsValid() ? (float)(walkPlayable.GetTime() / walkClip.length) : 0f;
        public float StepPhaseA { get; private set; } = 0.05f;
        public float StepPhaseB { get; private set; } = 0.55f;
        public bool Walking => walkOn;
        public float WalkWeight => walkOn ? walkBlend : 1f - walkBlend;

        // ------------------------------------------------------------------------------------------------ build
        public OzPoseRig(Transform root)
        {
            Root = root;
            Anim = root.GetComponentInChildren<Animator>(true);
            if (!Anim) Anim = root.gameObject.AddComponent<Animator>();
            Anim.updateMode = AnimatorUpdateMode.UnscaledTime; Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; Anim.applyRootMotion = false;
            Armature = Find("Armature"); Hips = Find("mixamorigHips"); Spine = Find("mixamorigSpine"); Spine1 = Find("mixamorigSpine1"); Spine2 = Find("mixamorigSpine2");
            Neck = Find("mixamorigNeck"); Head = Find("mixamorigHead"); HeadFront = Find("HeadFront"); HatTop = Find("HatTop");
            ShoulderL = Find("mixamorigLeftShoulder"); ShoulderR = Find("mixamorigRightShoulder");
            ArmL = Find("mixamorigLeftArm"); ArmR = Find("mixamorigRightArm"); ForeArmL = Find("mixamorigLeftForeArm"); ForeArmR = Find("mixamorigRightForeArm");
            HandL = Find("mixamorigLeftHand"); HandR = Find("mixamorigRightHand"); SocketL = Find("LeftHandSocket"); SocketR = Find("RightHandSocket");
            FootL = Find("mixamorigLeftFoot"); FootR = Find("mixamorigRightFoot"); ToeL = Find("mixamorigLeftToeBase"); ToeR = Find("mixamorigRightToeBase");
            UpLegL = Find("mixamorigLeftUpLeg"); UpLegR = Find("mixamorigRightUpLeg"); LegL = Find("mixamorigLeftLeg"); LegR = Find("mixamorigRightLeg");
            if (!Ok) { Problem = "bones missing (hips / head / arms)"; return; }
            // the feet at rest, in the root's frame (the crouch plants them there)
            if (FootL && FootR) { footPos0L = root.InverseTransformPoint(FootL.position); footPos0R = root.InverseTransformPoint(FootR.position); footRot0L = Quaternion.Inverse(root.rotation) * FootL.rotation; footRot0R = Quaternion.Inverse(root.rotation) * FootR.rotation; }
            var top = Hips ? Hips : root;
            foreach (var t in (Armature ? Armature : root).GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                if (t.name.StartsWith("Shotgun")) continue;                                             // the gun is a prop the stage re-parents (rack / hand): never re-set here
                bones.Add(t); restPos.Add(t.localPosition); restRot.Add(t.localRotation); snapRot.Add(t.localRotation);
            }
            foreach (var t in HandL.GetComponentsInChildren<Transform>(true)) if (t != HandL && t.name.StartsWith("mixamorig")) fingersL.Add(t);
            foreach (var t in HandR.GetComponentsInChildren<Transform>(true)) if (t != HandR && t.name.StartsWith("mixamorig")) fingersR.Add(t);
            // the model's axes at rest: forward = head -> HeadFront on the ground plane; right = up x forward
            Vector3 fwd = HeadFront ? Vector3.ProjectOnPlane(HeadFront.position - Head.position, Vector3.up) : root.forward;
            if (fwd.sqrMagnitude < 1e-6f) fwd = root.forward;
            fwd.Normalize();
            FwdLocal = root.InverseTransformDirection(fwd).normalized; RightLocal = root.InverseTransformDirection(Vector3.Cross(Vector3.up, fwd)).normalized;
            headFwdLocal = Head.InverseTransformDirection(fwd).normalized;
            var b = new Bounds(root.position, Vector3.zero); bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            Height = any ? b.size.y : 2f;
        }

        /// <summary>The model's uniform size on the stage (the redo's EvilZod a little bigger, BS-01 a little smaller): the root's scale and the
        /// measured (renderer) height with it; the stage scales its hand targets by Scale (OzEvilStage.EzHeight / SkHeight). Call right after
        /// the constructor, before LoadWalk.</summary>
        public void SetScale(float s)
        {
            s = Mathf.Clamp(s, 0.5f, 2f);
            Root.localScale = Vector3.one * s; Height *= s; Scale = s;
        }
        public float Scale { get; private set; } = 1f;

        public Transform Find(string name) => Find(Root, name);
        static Transform Find(Transform t, string name)
        {
            if (!t) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var r = Find(t.GetChild(i), name); if (r) return r; }
            return null;
        }

        public Vector3 Fwd => Root.TransformDirection(FwdLocal).normalized;
        public Vector3 Right => Root.TransformDirection(RightLocal).normalized;
        public Vector3 Up => Vector3.up;
        /// <summary>A point in the model's own frame (x = its right, y = up from its feet, z = its forward) to world.</summary>
        public Vector3 Local(float x, float y, float z) => Root.position + Right * x + Vector3.up * y + Fwd * z;

        // ------------------------------------------------------------------------------------------------ the rest pose, every frame
        /// <summary>Every bone back to the bind pose (the frame's pose is then written from scratch).</summary>
        public void ResetToRest()
        {
            for (int i = 0; i < bones.Count; i++) { var t = bones[i]; if (!t) continue; t.localPosition = restPos[i]; t.localRotation = restRot[i]; }
        }
        /// <summary>Remembers this frame's local rotations (the last procedural pose before a walk, blended into the take).</summary>
        public void Snapshot() { for (int i = 0; i < bones.Count; i++) if (bones[i]) snapRot[i] = bones[i].localRotation; }
        /// <summary>k of the current pose, 1-k of the snapshot (rotations only: the walk take's hip slide stays).</summary>
        public void BlendFromSnapshot(float k)
        {
            if (k >= 1f) return;
            for (int i = 0; i < bones.Count; i++) { var t = bones[i]; if (t) t.localRotation = Quaternion.Slerp(snapRot[i], t.localRotation, k); }
        }

        // ------------------------------------------------------------------------------------------------ arms
        /// <summary>Shoulder -> elbow -> hand to `target` (world), the elbow toward `pole` (world direction). w blends from the pose as it is.</summary>
        public void IkArm(bool right, Vector3 target, Vector3 pole, float w = 1f)
        {
            IkChain(right ? ArmR : ArmL, right ? ForeArmR : ForeArmL, right ? HandR : HandL, target, pole, w);
        }
        /// <summary>Shoulder -> elbow -> wrist, or hip -> knee -> ankle: the end joint to `target`, the middle joint toward `pole`.</summary>
        public void IkChain(Transform up, Transform lo, Transform hand, Vector3 target, Vector3 pole, float w = 1f)
        {
            if (!up || !lo || !hand || w <= 0f) return;
            Vector3 A = up.position, B = lo.position, C = hand.position;
            float la = (B - A).magnitude, lb = (C - B).magnitude;
            if (la < 1e-4f || lb < 1e-4f) return;
            Vector3 toT = target - A; float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
            Vector3 dir = toT.sqrMagnitude > 1e-8f ? toT.normalized : Fwd;
            // the elbow: law of cosines on the circle around the shoulder-target line, toward the pole
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f), alpha = Mathf.Acos(cosA);
            Vector3 p = Vector3.ProjectOnPlane(pole, dir); if (p.sqrMagnitude < 1e-6f) p = Vector3.ProjectOnPlane(-Fwd + Vector3.down, dir); p.Normalize();
            Vector3 E = A + (dir * Mathf.Cos(alpha) + p * Mathf.Sin(alpha)) * la;
            Quaternion q1 = Quaternion.FromToRotation(B - A, E - A);
            up.rotation = Quaternion.Slerp(up.rotation, q1 * up.rotation, w);
            Vector3 B2 = lo.position, C2 = hand.position;
            Quaternion q2 = Quaternion.FromToRotation(C2 - B2, A + dir * d - B2);
            lo.rotation = Quaternion.Slerp(lo.rotation, q2 * lo.rotation, w);
        }

        /// <summary>The hand turned about the forearm so that its palm faces `dir` (world); w = share of the turn.</summary>
        public void PalmTo(bool right, Vector3 dir, float w = 1f)
        {
            Transform lo = right ? ForeArmR : ForeArmL, hand = right ? HandR : HandL;
            if (!lo || !hand || !palmOk || w <= 0f) return;
            Vector3 axis = (hand.position - lo.position).normalized;
            Vector3 palm = Vector3.ProjectOnPlane(PalmNormal(right), axis), want = Vector3.ProjectOnPlane(dir, axis);
            if (palm.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f) return;
            float ang = Vector3.SignedAngle(palm, want, axis);
            hand.rotation = Quaternion.AngleAxis(ang * w, axis) * hand.rotation;
        }
        /// <summary>The hand rolled about the forearm by `deg`.</summary>
        public void HandRoll(bool right, float deg)
        {
            Transform lo = right ? ForeArmR : ForeArmL, hand = right ? HandR : HandL;
            if (!lo || !hand) return;
            hand.rotation = Quaternion.AngleAxis(deg, (hand.position - lo.position).normalized) * hand.rotation;
        }
        public Vector3 PalmNormal(bool right) => (right ? HandR : HandL).TransformDirection(right ? palmLocalR : palmLocalL).normalized;
        public Vector3 HandPoint(bool right) => (right ? SocketR : SocketL) ? (right ? SocketR : SocketL).position : (right ? HandR : HandL).position;

        // "wrist and fist looks twisted and no fingers ... hand completely twisted wrong and jitter")
        // IkChain bends the elbow with a from-to rotation, so the forearm turned about whatever axis that gave (the elbow bent sideways off its
        // hinge) and PalmTo then rolled the hand up to 180 deg on a forearm that did not roll: the candy-wrapper wrist, the fist seen edge-on as a
        // lump. IkArmHinge bends the elbow ONLY about its own hinge (measured on the model's takes), aims the arm and swivels it about the
        // shoulder-to-wrist line so the elbow points at the pole (the commander's fist IK, ZodStage.ApplyThrust): the hand keeps its rest
        // turn on the forearm (a straight wrist; the fist and the fingers read as the take made them). TwistSpread shares a needed turn of the
        // hand about the forearm between the forearm and the hand.
        Vector3 hingeLocalL, hingeLocalR; bool hingeOkL, hingeOkR; float swivelSignL = 1f, swivelSignR = 1f;
        public string HingeReport { get; private set; } = "not measured";
        public bool HasHinges => hingeOkL && hingeOkR;

        /// <summary>Each elbow's hinge axis in its forearm's frame, from the model's own takes: the sampled frame where that elbow bends most
        /// (12 deg or more); no bent frame: the axis that bends the rest pose's forearm toward the body's front. Call after the rest pose is
        /// stored (the pose is put back).</summary>
        public void MeasureHinges(string resFolder, string modelName, params string[] takes)
        {
            if (!Anim || !Ok) return;
            ResetToRest();
            FallbackHinge(true); FallbackHinge(false);
            float bestR = 0f, bestL = 0f; string fromR = "fallback (the rest forearm bends forward)", fromL = fromR;
            foreach (var tk in takes)
            {
                AnimationClip clip = null;
                foreach (var c in Resources.LoadAll<AnimationClip>(resFolder + modelName + "@" + tk)) if (c && !c.name.StartsWith("__preview__")) { clip = c; break; }
                if (!clip) continue;
                int n = clip.length > 0.05f ? 16 : 1;
                for (int i = 0; i < n; i++)
                {
                    SampleClip(clip, clip.length * i / n);
                    MeasureOne(true, ref bestR, ref fromR, tk); MeasureOne(false, ref bestL, ref fromL, tk);
                }
                ResetToRest();
            }
            ResetToRest();
            // how far the measured axes are from the forward-bending fallback (a check for the log)
            Vector3 fbR = FallbackAxis(true), fbL = FallbackAxis(false);
            float offR = Vector3.Angle(ForeArmR.rotation * hingeLocalR, fbR), offL = Vector3.Angle(ForeArmL.rotation * hingeLocalL, fbL);
            HingeReport = $"right elbow hinge from {fromR} ({offR:F0} deg from the forward bend), left from {fromL} ({offL:F0} deg)";
        }
        void MeasureOne(bool right, ref float best, ref string from, string take)
        {
            var up = right ? ArmR : ArmL; var lo = right ? ForeArmR : ForeArmL; var hand = right ? HandR : HandL;
            Vector3 u = up.position - lo.position, v = hand.position - lo.position;
            float bend = 180f - Vector3.Angle(u, v);
            if (bend < 12f || bend <= best) return;
            Vector3 n = Vector3.Cross(u, v).normalized;
            if (right) { hingeLocalR = Quaternion.Inverse(lo.rotation) * n; hingeOkR = true; } else { hingeLocalL = Quaternion.Inverse(lo.rotation) * n; hingeOkL = true; }
            best = bend; from = take + " (bent " + bend.ToString("F0") + " deg)";
        }
        Vector3 FallbackAxis(bool right)
        {
            var up = right ? ArmR : ArmL; var lo = right ? ForeArmR : ForeArmL;
            return Vector3.Cross(up.position - lo.position, Fwd).normalized;                    // e2 = n x e1 then points along Fwd: the forearm bends forward
        }
        void FallbackHinge(bool right)
        {
            var lo = right ? ForeArmR : ForeArmL; Vector3 n = FallbackAxis(right);
            if (n.sqrMagnitude < 0.5f) return;
            if (right) { hingeLocalR = Quaternion.Inverse(lo.rotation) * n; hingeOkR = true; } else { hingeLocalL = Quaternion.Inverse(lo.rotation) * n; hingeOkL = true; }
        }

        /// <summary>Shoulder -> elbow -> wrist to `target` (world) with the elbow bent ONLY about its hinge; the elbow toward `pole` (world
        /// direction). The hand keeps its rest turn on the forearm. No hinge measured: IkChain.</summary>
        public void IkArmHinge(bool right, Vector3 target, Vector3 pole, float w = 1f)
        {
            Transform up = right ? ArmR : ArmL, lo = right ? ForeArmR : ForeArmL, hand = right ? HandR : HandL;
            if (!(right ? hingeOkR : hingeOkL)) { IkChain(up, lo, hand, target, pole, w); return; }
            if (!up || !lo || !hand || w <= 0f) return;
            Vector3 A = up.position, B = lo.position, C = hand.position;
            float la = (B - A).magnitude, lb = (C - B).magnitude;
            if (la < 1e-4f || lb < 1e-4f) return;
            Quaternion u0 = up.rotation, f0 = lo.rotation;
            float lat = Mathf.Clamp((target - A).magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
            // 1. the elbow about its hinge to the law-of-cosines angle
            Vector3 n = (lo.rotation * (right ? hingeLocalR : hingeLocalL)).normalized, u = A - B, v = C - B;
            Vector3 e1 = Vector3.ProjectOnPlane(u, n).normalized, e2 = Vector3.Cross(n, e1).normalized;
            float phi = Mathf.Acos(Mathf.Clamp((la * la + lb * lb - lat * lat) / (2f * la * lb), -1f, 1f));
            Vector3 vWant = e1 * Mathf.Cos(phi) + e2 * Mathf.Sin(phi), vNow = Vector3.ProjectOnPlane(v, n);
            Quaternion r1 = vNow.sqrMagnitude > 1e-10f ? Quaternion.AngleAxis(Vector3.SignedAngle(vNow, vWant, n), n) : Quaternion.identity;
            // 2. the shoulder-to-wrist line at the target
            Vector3 C1 = B + r1 * v;
            Quaternion r2 = Quaternion.FromToRotation(C1 - A, target - A);
            // 3. the swivel about that line: the elbow toward the pole. The elbow's side is taken from the hinge (n x line), stable even with
            //    the arm nearly straight; its sign is the bent elbow's own (kept from the last frame it was clear)
            Vector3 ax = (target - A).normalized, n2 = r2 * n, b2 = r2 * (B - A);
            Vector3 eOff = Vector3.ProjectOnPlane(b2, ax), side = Vector3.Cross(n2, ax);
            if (eOff.magnitude > 0.05f * la && side.sqrMagnitude > 1e-8f) { float s = Vector3.Dot(eOff, side) >= 0f ? 1f : -1f; if (right) swivelSignR = s; else swivelSignL = s; }
            side *= right ? swivelSignR : swivelSignL;
            Vector3 p = Vector3.ProjectOnPlane(pole, ax);
            Quaternion r3 = side.sqrMagnitude > 1e-8f && p.sqrMagnitude > 1e-8f ? Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.ProjectOnPlane(side, ax), p, ax), ax) : Quaternion.identity;
            Quaternion rUp = r3 * r2;
            up.rotation = Quaternion.Slerp(u0, rUp * u0, w);
            lo.rotation = Quaternion.Slerp(f0, rUp * r1 * f0, w);
        }

        /// <summary>The hand to `want` (world rotation), the turn about the forearm's axis shared: `forearmShare` of it rolls the forearm
        /// (the hand rides along), the rest turns the hand on it (no candy-wrapper wrist).</summary>
        public void TwistSpread(bool right, Quaternion want, float forearmShare = 0.5f)
        {
            Transform lo = right ? ForeArmR : ForeArmL, hand = right ? HandR : HandL;
            if (!lo || !hand) return;
            Vector3 axis = hand.position - lo.position; if (axis.sqrMagnitude < 1e-8f) { hand.rotation = want; return; }
            axis.Normalize();
            Quaternion delta = want * Quaternion.Inverse(hand.rotation);
            if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
            Vector3 r = new Vector3(delta.x, delta.y, delta.z), pr = Vector3.Project(r, axis);
            var twist = new Quaternion(pr.x, pr.y, pr.z, delta.w);
            float mag = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (mag > 1e-6f) { twist = new Quaternion(twist.x / mag, twist.y / mag, twist.z / mag, twist.w / mag); lo.rotation = Quaternion.Slerp(Quaternion.identity, twist, Mathf.Clamp01(forearmShare)) * lo.rotation; }
            hand.rotation = want;
        }

        // ------------------------------------------------------------------------------------------------ head, spine, root
        /// <summary>Head (and neckShare of the turn on the neck) toward a world point; yaw / pitch clamped against the body's forward.</summary>
        public void LookAt(Vector3 point, float w = 1f, float maxYaw = 65f, float maxPitch = 38f, float neckShare = 0.35f)
        {
            if (!Head || w <= 0f) return;
            Vector3 fwd = Fwd, right = Right;
            Vector3 to = point - Head.position; if (to.sqrMagnitude < 1e-6f) return;
            to.Normalize();
            float yaw = Mathf.Clamp(Vector3.SignedAngle(fwd, Vector3.ProjectOnPlane(to, Vector3.up), Vector3.up), -maxYaw, maxYaw);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(to.y, -1f, 1f)) * Mathf.Rad2Deg, -maxPitch, maxPitch);   // + = chin down
            Quaternion turn = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Quaternion.AngleAxis(yaw, Vector3.up) * right);
            if (Neck && neckShare > 0f) Neck.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Slerp(Quaternion.identity, turn, neckShare), w) * Neck.rotation;
            Vector3 headFwdNow = Head.TransformDirection(headFwdLocal);
            Vector3 wantDir = turn * fwd;
            Quaternion q = Quaternion.FromToRotation(headFwdNow, wantDir);
            Head.rotation = Quaternion.Slerp(Quaternion.identity, q, w) * Head.rotation;
        }
        /// <summary>Extra head angles: nod (chin down, deg), tilt (roll toward its right, deg), turn (yaw, deg).</summary>
        public void HeadAdd(float nodDeg, float tiltDeg, float turnDeg)
        {
            if (!Head) return;
            Head.rotation = Quaternion.AngleAxis(turnDeg, Vector3.up) * Quaternion.AngleAxis(nodDeg, Right) * Quaternion.AngleAxis(tiltDeg, Fwd) * Head.rotation;
        }
        /// <summary>The upper body: leans forward (deg), to its right (deg) and twists toward its right (deg), on the lower spine.</summary>
        public void Lean(float fwdDeg, float sideDeg, float twistDeg)
        {
            var s = Spine1 ? Spine1 : Spine; if (!s) return;
            s.rotation = Quaternion.AngleAxis(twistDeg, Vector3.up) * Quaternion.AngleAxis(fwdDeg, Right) * Quaternion.AngleAxis(sideDeg, Fwd) * s.rotation;
        }
        /// <summary>The chest pushed up / back (deg) on the upper spine (proud), negative = slumped.</summary>
        public void ChestUp(float deg)
        {
            var s = Spine2 ? Spine2 : Spine1; if (!s) return;
            s.rotation = Quaternion.AngleAxis(-deg, Right) * s.rotation;
        }
        /// <summary>The shoulders lifted (deg) (a breath, a shrug).</summary>
        public void Shoulders(float deg)
        {
            if (ShoulderL) ShoulderL.rotation = Quaternion.AngleAxis(-deg, Fwd) * ShoulderL.rotation;
            if (ShoulderR) ShoulderR.rotation = Quaternion.AngleAxis(deg, Fwd) * ShoulderR.rotation;
        }

        /// <summary>A crouch with the feet planted: the hips go down `drop` m and back `back` m, the trunk bows forward `bowDeg` at the hips
        /// (the legs keep their direction and their roots stay where they were), the knees bend toward the feet's rest positions (two-bone
        /// legs, knees forward) and the feet keep their rest orientation flat on the floor. Call right after ResetToRest, before the spine and
        /// the arms (all of those are world-space and sit on top). Nothing moves below 3 mm / 0.3 deg.</summary>
        public void Crouch(float drop, float back, float bowDeg)
        {
            if (!HasLegs) return;
            if (drop < 0.003f && Mathf.Abs(back) < 0.003f && Mathf.Abs(bowDeg) < 0.3f) return;
            Vector3 fwd = Fwd, right = Right;
            Vector3 footL = Root.TransformPoint(footPos0L), footR = Root.TransformPoint(footPos0R);
            Hips.position += Vector3.down * Mathf.Max(0f, drop) - fwd * back;
            if (Mathf.Abs(bowDeg) >= 0.3f)
            {   // the bow: the whole hips bone pitches forward about the body's right axis; the legs are turned back by the same angle and their
                // roots put back where the hips had them, so only the trunk leans
                Vector3 legMid0 = (UpLegL.position + UpLegR.position) * 0.5f;
                Quaternion bow = Quaternion.AngleAxis(bowDeg, right);
                Hips.rotation = bow * Hips.rotation;
                Quaternion unbow = Quaternion.AngleAxis(-bowDeg, right);
                UpLegL.rotation = unbow * UpLegL.rotation; UpLegR.rotation = unbow * UpLegR.rotation;
                Vector3 legMid1 = (UpLegL.position + UpLegR.position) * 0.5f;
                Hips.position += legMid0 - legMid1;
            }
            IkChain(UpLegL, LegL, FootL, footL, fwd + right * 0.15f);
            IkChain(UpLegR, LegR, FootR, footR, fwd - right * 0.15f);
            FootL.rotation = Root.rotation * footRot0L; FootR.rotation = Root.rotation * footRot0R;
        }

        // ------------------------------------------------------------------------------------------------ fingers
        /// <summary>Samples the model's hand-pose takes once (through a Playable graph, so any clip kind works) and measures the palm
        /// normal from the Fist pose. Call after the rest pose is stored; the pose is put back.</summary>
        public void LoadFingerPoses(string resFolder, string modelName, params string[] poseNames)
        {
            if (!Anim) return;
            Vector3 tipOpenL = FingerTip(false), tipOpenR = FingerTip(true);
            foreach (var pn in poseNames)
            {
                AnimationClip clip = null;
                foreach (var c in Resources.LoadAll<AnimationClip>(resFolder + modelName + "@" + pn)) if (c && !c.name.StartsWith("__preview__")) { clip = c; break; }
                if (!clip) { Debug.LogWarning("[EvilZod] hand pose take " + pn + " missing (Resources/" + resFolder + modelName + "@" + pn + ")"); continue; }
                SampleClip(clip, 0f);
                var q = new Quaternion[bones.Count];
                for (int i = 0; i < bones.Count; i++) q[i] = bones[i] ? bones[i].localRotation : Quaternion.identity;
                fingerPoses[pn] = q;
                if (pn == "Fist")
                {   // the palm: where the fingertips go when the hand closes
                    Vector3 dL = FingerTip(false) - tipOpenL, dR = FingerTip(true) - tipOpenR;
                    if (dL.sqrMagnitude > 1e-6f && dR.sqrMagnitude > 1e-6f)
                    {
                        palmLocalL = HandL.InverseTransformDirection(dL.normalized); palmLocalR = HandR.InverseTransformDirection(dR.normalized); palmOk = true;
                    }
                }
                ResetToRest();
            }
        }
        Vector3 FingerTip(bool right)
        {
            var list = right ? fingersR : fingersL; var hand = right ? HandR : HandL;
            if (list.Count == 0) return hand.position;
            Vector3 s = Vector3.zero; int n = 0;
            foreach (var t in list) if (t && t.childCount == 0) { s += t.position; n++; }
            return n > 0 ? s / n : list[list.Count - 1].position;
        }
        public bool HasFingerPose(string pn) => fingerPoses.ContainsKey(pn);
        /// <summary>The fingers of one hand toward a sampled pose (w = 1 fully), on top of whatever the hand does.</summary>
        public void Fingers(bool right, string pose, float w = 1f)
        {
            if (w <= 0f || string.IsNullOrEmpty(pose) || !fingerPoses.TryGetValue(pose, out var q)) return;
            var list = right ? fingersR : fingersL;
            foreach (var t in list)
            {
                int i = bones.IndexOf(t); if (i < 0) continue;
                t.localRotation = Quaternion.Slerp(t.localRotation, q[i], w);
            }
        }

        void SampleClip(AnimationClip clip, float time)
        {
            var g = PlayableGraph.Create("OzPoseSample");
            g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var o = AnimationPlayableOutput.Create(g, "pose", Anim);
            var p = AnimationClipPlayable.Create(g, clip); p.SetApplyFootIK(false); p.SetTime(time);
            o.SetSourcePlayable(p);
            g.Evaluate(0f);
            g.Destroy();
        }

        // ------------------------------------------------------------------------------------------------ the walk take
        /// <summary>Loads the model's Walk take and measures its speed from the feet.</summary>
        public void LoadWalk(string resFolder, string modelName)
        {
            foreach (var c in Resources.LoadAll<AnimationClip>(resFolder + modelName + "@Walk")) if (c && !c.name.StartsWith("__preview__")) { walkClip = c; break; }
            if (!walkClip || !FootL || !FootR || !Hips) { if (!walkClip) Debug.LogWarning("[EvilZod] walk take missing for " + modelName); return; }
            // the planted foot travels backward under the hips by the stride in half a cycle: speed = stride / (length / 2)
            const int N = 24; float minL = 1e9f, maxL = -1e9f, minR = 1e9f, maxR = -1e9f; float lowL = 1e9f, lowR = 1e9f; int iLowL = 0, iLowR = 0;
            for (int i = 0; i < N; i++)
            {
                SampleClip(walkClip, walkClip.length * i / N);
                Vector3 fwd = Fwd;
                float zl = Vector3.Dot(FootL.position - Hips.position, fwd), zr = Vector3.Dot(FootR.position - Hips.position, fwd);
                minL = Mathf.Min(minL, zl); maxL = Mathf.Max(maxL, zl); minR = Mathf.Min(minR, zr); maxR = Mathf.Max(maxR, zr);
                if (FootL.position.y < lowL) { lowL = FootL.position.y; iLowL = i; }
                if (FootR.position.y < lowR) { lowR = FootR.position.y; iLowR = i; }
            }
            ResetToRest();
            float stride = 0.5f * ((maxL - minL) + (maxR - minR));
            WalkSpeed = Mathf.Clamp(stride / Mathf.Max(0.2f, walkClip.length * 0.5f), 0.3f, 3f);
            StepPhaseA = iLowL / (float)N; StepPhaseB = iLowR / (float)N;
            Debug.Log($"[EvilZod] {modelName} walk take {walkClip.length:F2} s, stride {stride:F2} m -> {WalkSpeed:F2} m/s, feet down at {StepPhaseA:F2} / {StepPhaseB:F2}");
        }
        public bool HasWalk => walkClip != null;

        /// <summary>The walk take plays (looping) and blends in over `blend` s from the pose of the last frame.</summary>
        public void StartWalk(float blend = 0.35f)
        {
            if (!walkClip || walkOn) return;
            Snapshot();
            if (!walkGraph.IsValid())
            {
                walkGraph = PlayableGraph.Create("OzWalk");
                walkGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var o = AnimationPlayableOutput.Create(walkGraph, "walk", Anim);
                walkPlayable = AnimationClipPlayable.Create(walkGraph, walkClip); walkPlayable.SetApplyFootIK(false);
                o.SetSourcePlayable(walkPlayable);
            }
            walkPlayable.SetTime(0.0); walkOn = true; walkBlend = 0f; walkBlendDur = Mathf.Max(0.05f, blend);
            if (!walkGraph.IsPlaying()) walkGraph.Play();
        }
        /// <summary>The walk take on at once, in step (no blend from the pose before: a robot that appears walking).</summary>
        public void StartWalkCut() { StartWalk(0.05f); walkBlend = 1f; }
        public void StopWalk(float blend = 0.3f)
        {
            if (!walkOn) return;
            Snapshot(); walkOn = false; walkBlend = 0f; walkBlendDur = Mathf.Max(0.05f, blend);
        }
        /// <summary>Call before the frame's pose is written: advances the take (its own clock) and writes its pose; returns false when the
        /// take is not on (the caller then starts from the rest pose).</summary>
        public bool TickWalk(float dt)
        {
            if (walkBlend < 1f) walkBlend = Mathf.Min(1f, walkBlend + dt / walkBlendDur);
            if (!walkOn || !walkGraph.IsValid()) { if (walkGraph.IsValid() && walkGraph.IsPlaying() && walkBlend >= 1f) walkGraph.Stop(); return false; }
            double t = walkPlayable.GetTime() + dt * Mathf.Max(0.1f, WalkRate);
            if (t > walkClip.length) t -= walkClip.length;
            walkPlayable.SetTime(t);
            walkGraph.Evaluate(0f);
            return true;
        }
        /// <summary>After the frame's pose is written: a walk starting or ending blends with the snapshot.</summary>
        public void FinishWalkBlend() { if (walkBlend < 1f) BlendFromSnapshot(walkBlend); }

        public void Dispose() { if (walkGraph.IsValid()) walkGraph.Destroy(); }
    }
}
