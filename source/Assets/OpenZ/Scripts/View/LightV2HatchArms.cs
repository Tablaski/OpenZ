using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 light tank v2 hatch driver (BoardingView.LightV2Hatch.cs drives it every frame from PlaceCommander): after WeaponHold (order 80)
    /// put his weapon on the clip's own arms, (1) the lowest point of that weapon outside the hatch opening is measured (its mesh's box on a
    /// 3 x 3 x 3 lattice, along the vehicle's up, from his root): the lift BoardingView needs next frame to keep it over the plate; (2) with
    /// weight w his arms go IN: each upper arm, then each forearm, turned (shortest arc) to point down, a little in towards his hips
    /// and forward (<see cref="UpperFwd"/> / <see cref="UpperIn"/>, <see cref="LowerFwd"/> / <see cref="LowerIn"/>, degrees from straight
    /// down), so nothing of them passes the plate outside the opening while he rests, rises or sinks (the idle clip holds his hands
    /// 10 to 20 cm outside it); on the way in / out his forearms pass up in front of his chest (<see cref="Arm"/>). (3) The midpoint of his
    /// shoulders is measured for BoardingView, which centres it over the opening while his arms are in. A frame BoardingView did not
    /// drive (he left the hatch: an exit, a corpse): the clip's own arms and his weapon drawn again.
    /// </summary>
    [DefaultExecutionOrder(85)]
    public class LightV2HatchArms : MonoBehaviour
    {
        /// <summary>Degrees: the arms-in directions from straight down (forward, in towards his hips) of the upper arm and the forearm, and
        /// the forearm's elevation over the horizontal half way out (measured offline on the light tank v2's hatch with the BS-01 clips).</summary>
        public const float UpperFwd = 20f, UpperIn = 30f, LowerFwd = 10f, LowerIn = 0f, MidElev = 45f;
        /// <summary>Measured this frame (else BoardingView treats the weapon as not clear); the weapon's lowest point outside the opening
        /// over his root (NaN: none of it outside).</summary>
        public bool measured; public float lowRel = float.NaN, weight = 1f;
        /// <summary>The midpoint of his shoulder joints from his root (the clip's own pose of this frame; BoardingView centres it over the
        /// opening next frame while his arms are in).</summary>
        public bool shoulderOk; public Vector3 shoulderRel;
        Transform lUp, lLo, lHand, rUp, rLo, rHand, hips;
        MeshFilter[] gunMesh; Renderer[] gun;
        int driven = -1; Vector3 rim, up = Vector3.up; float rOpen; bool hidden;

        public void Init(Animator an, Transform weapon)
        {
            gunMesh = weapon ? weapon.GetComponentsInChildren<MeshFilter>(true) : new MeshFilter[0];
            if (an && an.isHuman)
            {
                lUp = an.GetBoneTransform(HumanBodyBones.LeftUpperArm); lLo = an.GetBoneTransform(HumanBodyBones.LeftLowerArm); lHand = an.GetBoneTransform(HumanBodyBones.LeftHand);
                rUp = an.GetBoneTransform(HumanBodyBones.RightUpperArm); rLo = an.GetBoneTransform(HumanBodyBones.RightLowerArm); rHand = an.GetBoneTransform(HumanBodyBones.RightHand);
                hips = an.GetBoneTransform(HumanBodyBones.Hips);
            }
            foreach (var x in GetComponentsInChildren<Transform>(true))
            {
                string n = x.name;
                if (!lUp && n.EndsWith("LeftArm")) lUp = x; else if (!lLo && n.EndsWith("LeftForeArm")) lLo = x; else if (!lHand && n.EndsWith("mixamorigLeftHand")) lHand = x;
                else if (!rUp && n.EndsWith("RightArm")) rUp = x; else if (!rLo && n.EndsWith("RightForeArm")) rLo = x; else if (!rHand && n.EndsWith("mixamorigRightHand")) rHand = x;
                else if (!hips && n.EndsWith("mixamorigHips")) hips = x;
            }
            measured = false; lowRel = float.NaN;
        }

        /// <summary>BoardingView, this frame: the arms-in weight, the hatch rim, the vehicle's up, the opening's radius, his weapon not drawn.</summary>
        public void Drive(float w, Vector3 rimAt, Vector3 upDir, float openR, bool gunHidden, Renderer[] gunR)
        {
            weight = w; rim = rimAt; up = upDir; rOpen = openR; hidden = gunHidden; gun = gunR; driven = Time.frameCount;
        }

        void LateUpdate()
        {
            if (driven != Time.frameCount)
            {   // not in the light tank v2's hatch this frame: his own arms, his weapon drawn
                if (hidden && gun != null) foreach (var g in gun) if (g) g.enabled = true;
                hidden = false; measured = false; shoulderOk = false; return;
            }
            float low = float.MaxValue;
            var root = transform.position;
            shoulderOk = lUp && rUp;
            if (shoulderOk) shoulderRel = Vector3.ProjectOnPlane(0.5f * (lUp.position + rUp.position) - root, up);
            if (gunMesh != null)
                foreach (var mf in gunMesh)
                {
                    if (!mf || !mf.sharedMesh) continue;
                    var b = mf.sharedMesh.bounds; var tr = mf.transform;
                    for (int i = 0; i < 27; i++)
                    {
                        var w = tr.TransformPoint(b.min + Vector3.Scale(b.size, new Vector3(i % 3, (i / 3) % 3, i / 9) * 0.5f));
                        if (Vector3.ProjectOnPlane(w - rim, up).magnitude <= rOpen) continue;
                        low = Mathf.Min(low, Vector3.Dot(w - root, up));
                    }
                }
            lowRel = low < float.MaxValue ? low : float.NaN; measured = true;
            if (weight <= 0f) return;
            var front = Vector3.ProjectOnPlane(-transform.forward, up);                                       // robots face -z
            front = front.sqrMagnitude > 1e-10f ? front.normalized : Vector3.zero;
            var axis = hips ? hips.position : root;
            Arm(lUp, lLo, lHand, front, axis); Arm(rUp, rLo, rHand, front, axis);
        }

        /// <summary>One arm along the path: weight 0 the clip's arm, 0.5 the upper arm in and the forearm up towards his middle
        /// (<see cref="MidElev"/> over the horizontal: his hand in front of his chest, inside the opening), 1 the upper arm and the forearm in.
        /// Going out, his hands rise inside the opening first and then go out over the plate (a straight blend from arms-in to the clip's arms
        /// swept his hands through the plate).</summary>
        void Arm(Transform a, Transform b, Transform c, Vector3 front, Vector3 axis)
        {
            if (!a || !b || !c) return;
            float w1 = Mathf.Min(1f, weight * 2f);
            Turn(a, b, Down(a, front, axis, UpperFwd, UpperIn), w1);
            var inward = Vector3.ProjectOnPlane(axis - b.position, up);
            inward = inward.sqrMagnitude > 1e-10f ? inward.normalized : front;
            Turn(b, c, up * Mathf.Sin(MidElev * Mathf.Deg2Rad) + inward * Mathf.Cos(MidElev * Mathf.Deg2Rad), w1);
            if (weight > 0.5f) Turn(b, c, Down(b, front, axis, LowerFwd, LowerIn), (weight - 0.5f) * 2f);
        }

        /// <summary>Down + tan(in) x from joint j towards the axis + tan(fwd) x front.</summary>
        Vector3 Down(Transform j, Vector3 front, Vector3 axis, float fwdDeg, float inDeg)
        {
            var inward = Vector3.ProjectOnPlane(axis - j.position, up);
            inward = inward.sqrMagnitude > 1e-10f ? inward.normalized : Vector3.zero;
            return -up + inward * Mathf.Tan(inDeg * Mathf.Deg2Rad) + front * Mathf.Tan(fwdDeg * Mathf.Deg2Rad);
        }

        /// <summary>Bone j (its child c gives its direction) turned towards target (shortest arc) x w.</summary>
        static void Turn(Transform j, Transform c, Vector3 target, float w)
        {
            if (w <= 0f) return;
            var cur = c.position - j.position; if (cur.sqrMagnitude < 1e-10f || target.sqrMagnitude < 1e-10f) return;
            j.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, target), w) * j.rotation;
        }
    }
}
