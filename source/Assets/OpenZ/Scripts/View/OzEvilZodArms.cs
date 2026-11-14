using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV161 EvilZod: one arm of his upper body (View/OzEvilZodBody.cs). The two moves of the cutscene rig OzPoseRig, on bones given from outside: the elbow bends only about
    /// its own hinge, the arm swivels so the elbow points at a pole, and a turn of the hand about the forearm is shared between forearm and wrist. OzPoseRig itself is
    /// not used here: its constructor changes the Animator's update and culling modes, which the unit keeps.
    /// </summary>
    public sealed class OzEvilZodArms
    {
        readonly Transform up, lo, hand; readonly Vector3 hingeLocal; float swivelSign = 1f;

        public OzEvilZodArms(Transform up, Transform lo, Transform hand, Vector3 hingeLocal) { this.up = up; this.lo = lo; this.hand = hand; this.hingeLocal = hingeLocal; }

        /// <summary>Shoulder, elbow, wrist to target (world), the elbow bent only about its hinge and turned toward pole (a world direction). The hand keeps its turn on the forearm.</summary>
        public void Reach(Vector3 target, Vector3 pole)
        {
            Vector3 A = up.position, B = lo.position, C = hand.position;
            float la = (B - A).magnitude, lb = (C - B).magnitude;
            if (la < 1e-4f || lb < 1e-4f) return;
            Quaternion u0 = up.rotation, f0 = lo.rotation;
            float lat = Mathf.Clamp((target - A).magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
            Vector3 n = (lo.rotation * hingeLocal).normalized, u = A - B, v = C - B;
            Vector3 e1 = Vector3.ProjectOnPlane(u, n).normalized, e2 = Vector3.Cross(n, e1).normalized;
            float phi = Mathf.Acos(Mathf.Clamp((la * la + lb * lb - lat * lat) / (2f * la * lb), -1f, 1f));
            Vector3 vWant = e1 * Mathf.Cos(phi) + e2 * Mathf.Sin(phi), vNow = Vector3.ProjectOnPlane(v, n);
            Quaternion r1 = vNow.sqrMagnitude > 1e-10f ? Quaternion.AngleAxis(Vector3.SignedAngle(vNow, vWant, n), n) : Quaternion.identity;
            Vector3 C1 = B + r1 * v;
            Quaternion r2 = Quaternion.FromToRotation(C1 - A, target - A);
            Vector3 ax = (target - A).normalized, n2 = r2 * n, b2 = r2 * (B - A);
            Vector3 eOff = Vector3.ProjectOnPlane(b2, ax), side = Vector3.Cross(n2, ax);
            if (eOff.magnitude > 0.05f * la && side.sqrMagnitude > 1e-8f) swivelSign = Vector3.Dot(eOff, side) >= 0f ? 1f : -1f;
            side *= swivelSign;
            Vector3 p = Vector3.ProjectOnPlane(pole, ax);
            Quaternion r3 = side.sqrMagnitude > 1e-8f && p.sqrMagnitude > 1e-8f ? Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.ProjectOnPlane(side, ax), p, ax), ax) : Quaternion.identity;
            Quaternion rUp = r3 * r2;
            up.rotation = rUp * u0;
            lo.rotation = rUp * r1 * f0;
        }

        /// <summary>The hand to want (world), forearmShare of the turn about the forearm rolling the forearm, the rest turning the hand on it.</summary>
        public void TurnHand(Quaternion want, float forearmShare)
        {
            Vector3 axis = hand.position - lo.position;
            if (axis.sqrMagnitude < 1e-8f) { hand.rotation = want; return; }
            axis.Normalize();
            Quaternion delta = want * Quaternion.Inverse(hand.rotation);
            if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
            Vector3 r = new Vector3(delta.x, delta.y, delta.z), pr = Vector3.Project(r, axis);
            var twist = new Quaternion(pr.x, pr.y, pr.z, delta.w);
            float mag = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (mag > 1e-6f)
            {
                twist = new Quaternion(twist.x / mag, twist.y / mag, twist.z / mag, twist.w / mag);
                lo.rotation = Quaternion.Slerp(Quaternion.identity, twist, Mathf.Clamp01(forearmShare)) * lo.rotation;
            }
            hand.rotation = want;
        }
    }
}
