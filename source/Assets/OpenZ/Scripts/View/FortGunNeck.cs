using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>The fort cannon's hydraulic neck: a curved armoured trunk on the turntable with two steel lines, two heavy telescopic rams (a polished stage with a
    /// dark gland, a polished rod) standing in deck collars and driving the head through cups on its underside, and a frame on the head's underside round the
    /// trunk's pass. Modelled (Resources/OpenZ/FortGunNeck/OZ_FortGunNeck.fbx, FortGunYaw mesh space), shared by every fort, shown at every camera.
    /// <list type="bullet">
    /// <item>The trunk is curved about the head's hinge line, so it slides through the frame on the head at every tilt (the frame never moves on it).</item>
    /// <item>Each ram's base pin sits under the ring on the chord of its top pivot's path; the two sections take half the stroke each. Every part keeps scale 1:
    /// only positions and turns change, nothing stretches.</item>
    /// <item>The trunk and the collars wear the gun's own material and atlas (paint A / B, team colour, wreck paint follow); the rams the steel and dark
    /// materials of FortGunView.</item>
    /// <item>No model: an error in the log and no neck.</item>
    /// </list>
    /// Clearances over the whole tilt are checked off the engine by the kit's tools/clear.py on the exported model. View only.</summary>
    public sealed class FortGunNeck
    {
        public const string Prefix = "FortGunNeck_", ModelPath = "OpenZ/FortGunNeck/OZ_FortGunNeck";
        // ---- design, FortGunYaw mesh space (the kit's tools/neck2.py and neck3_model.py, number for number)
        public const float RX = 0.125f, PY = 0.078f, PZ = 0.070f;          // ram top pivot (head space), rams at x +-RX
        public const float BY = -0.069088f, BZ = 0.126132f;                // ram base pin under the ring (yaw space)
        public const float L0 = 0.157434f, G0 = 0.128f, LS = 0.125f, LR = 0.142f;   // pin to pin at rest, base pin to the gland top at rest, stage, rod
        public const float HingeY = 0.045f, HingeZ = -0.25f;               // the head's hinge line (FortGunView.tiltPivot)

        static bool loaded, missing; static Mesh mBase, mCollar, mStage, mRod; static int glandSub = 1;

        readonly Transform yaw, head;
        readonly Transform[] stage = new Transform[2], rod = new Transform[2];
        readonly Renderer[] painted = new Renderer[2];

        public Transform Base { get; private set; }
        public Transform Collar { get; private set; }
        public Transform Stage(int i) => stage[i];
        public Transform Rod(int i) => rod[i];
        public int Renderers { get; private set; }
        /// <summary>Largest pin-to-pin length reached and the smallest rod-in-stage overlap (mesh units): the Play check of "no stretch".</summary>
        public float MaxLength { get; private set; }
        public float MinOverlap { get; private set; } = 1f;

        FortGunNeck(Transform yaw, Transform head) { this.yaw = yaw; this.head = head; }

        static bool Load()
        {
            if (loaded) return !missing;
            loaded = true;
            var go = Resources.Load<GameObject>(ModelPath);
            if (go)
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh; if (!m) continue;
                    if (mf.name == Prefix + "Base") mBase = m; else if (mf.name == Prefix + "Collar") mCollar = m;
                    else if (mf.name == Prefix + "Stage") mStage = m; else if (mf.name == Prefix + "Rod") mRod = m;
                }
            }
            missing = !mBase || !mCollar || !mStage || !mRod;
            if (missing) { Debug.LogError("[OpenZ] FortGunNeck: the model Resources/" + ModelPath + " is missing or incomplete: the fort cannon has no neck"); return false; }
            if (mStage.subMeshCount > 1)                                                         // the gland is the short submesh at the stage's top
                glandSub = mStage.GetSubMesh(0).bounds.size.y < mStage.GetSubMesh(1).bounds.size.y ? 0 : 1;
            return true;
        }

        /// <summary>Builds the neck under `yaw` (and the head's collar under `head`). gunR = the yaw's own renderer (the gun's material and paint).</summary>
        public static FortGunNeck Build(Transform yaw, Transform head, MeshRenderer gunR, Material steel, Material dark)
        {
            if (!yaw || !head || !gunR || !steel || !dark || !Load()) return null;
            var n = new FortGunNeck(yaw, head);
            var gun = gunR.sharedMaterial;
            n.Base = n.Part(yaw, Prefix + "Base", mBase, new[] { gun }, gunR); n.painted[0] = n.Base.GetComponent<Renderer>();
            n.Collar = n.Part(head, Prefix + "Collar", mCollar, new[] { gun }, gunR); n.painted[1] = n.Collar.GetComponent<Renderer>();
            var sm = new Material[Mathf.Max(1, mStage.subMeshCount)];
            for (int i = 0; i < sm.Length; i++) sm[i] = i == glandSub && sm.Length > 1 ? dark : steel;
            var rm = new Material[Mathf.Max(1, mRod.subMeshCount)];
            for (int i = 0; i < rm.Length; i++) rm[i] = steel;
            for (int s = 0; s < 2; s++)
            {
                string k = s == 0 ? "L" : "R";
                n.stage[s] = n.Part(yaw, Prefix + "Stage_" + k, mStage, sm, gunR);
                n.rod[s] = n.Part(yaw, Prefix + "Rod_" + k, mRod, rm, gunR);
            }
            n.Pose();
            return n;
        }

        Transform Part(Transform parent, string name, Mesh mesh, Material[] mats, MeshRenderer like)
        {
            var go = new GameObject(name) { layer = yaw.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = like.receiveShadows;
            r.lightProbeUsage = like.lightProbeUsage; r.reflectionProbeUsage = like.reflectionProbeUsage;
            Renderers++;
            return go.transform;
        }

        /// <summary>Poses the rams from the head's pose (call right after the head is posed).</summary>
        public void Pose()
        {
            if (!yaw || !head) return;
            for (int i = 0; i < 2; i++)
            {
                if (!stage[i] || !rod[i]) continue;
                float sx = i == 0 ? -1f : 1f;
                Vector3 p1 = head.localPosition + head.localRotation * new Vector3(sx * RX, PY, PZ), p0 = new Vector3(sx * RX, BY, BZ);
                Vector3 d = p1 - p0; float L = d.magnitude; if (L < 1e-5f) continue;
                Vector3 up = d / L; float e = (L - L0) * 0.5f;                                          // each section's share of the stroke
                var xa = Vector3.right - up * Vector3.Dot(Vector3.right, up); xa = xa.sqrMagnitude > 1e-8f ? xa.normalized : Vector3.right;
                var rot = Quaternion.LookRotation(Vector3.Cross(xa, up), up);                           // +y up the ram, +x along the pin
                stage[i].localPosition = p0 + up * (G0 + e); stage[i].localRotation = rot;             // the stage mesh's origin is its gland top
                rod[i].localPosition = p1; rod[i].localRotation = rot;                                 // the rod mesh's origin is the top pivot
                MaxLength = Mathf.Max(MaxLength, L); MinOverlap = Mathf.Min(MinOverlap, (G0 + e) - (L - LR));
            }
        }

        /// <summary>The trunk and the head's collar wear exactly what the gun's own renderer wears (its material: war paint A / B, wreck paint; its
        /// property blocks: team colour, band, char), as the head does (FortGunView.SyncHead).</summary>
        public void SyncPaint(MeshRenderer gunR, MaterialPropertyBlock mpb)
        {
            if (!gunR || mpb == null) return;
            foreach (var r in painted)
            {
                if (!r) continue;
                var m = gunR.sharedMaterial;
                if (r.sharedMaterial != m) r.sharedMaterial = m;
                gunR.GetPropertyBlock(mpb, 0); r.SetPropertyBlock(mpb, 0);
            }
        }

        /// <summary>The ram top pivot (yaw space) the design gives for a head tilt in degrees (nose up about the hinge line): the Play check's reference.</summary>
        public static Vector3 PivotAtTilt(float sx, float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            float y = PY - HingeY, z = PZ - HingeZ;
            return new Vector3(sx * RX, HingeY + c * y + s * z, HingeZ - s * y + c * z);
        }
    }
}
