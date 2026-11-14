// OpenZ (GPL-3.0) - version 1.3, level 7 (LV_CONTRACT.md, kit LV3): hot footprints on the bank.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The prints a robot leaves on the dry bank as it comes out of the lava: one joined mesh of Capacity quads (the oldest is reused), each
    /// quad laid on the ground and carrying its birth on the lava clock in uv1. OpenZ/LavaPrint works out the cooling itself, so the mesh is
    /// written only when a print is made and nothing runs per frame; the renderer is off when every print is cold. One draw at most.
    /// </summary>
    public sealed class OzLavaPrints
    {
        public const int Capacity = 96;
        /// <summary>Seconds a print stays warm; OpenZ/LavaPrint holds the same number.</summary>
        public const float Life = 5f;
        public const string MaterialRes = "OpenZ/Lava/M_L07_Print";

        readonly Vector3[] verts = new Vector3[Capacity * 4];
        readonly Vector2[] uv = new Vector2[Capacity * 4], birth = new Vector2[Capacity * 4];
        readonly Mesh mesh; readonly GameObject go; readonly MeshRenderer mr; readonly ZGame game;
        int next; float lastBirth = -100f;

        public OzLavaPrints(Transform parent, ZGame g)
        {
            game = g;
            var tris = new int[Capacity * 6];
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1; tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
                uv[v] = new Vector2(-1f, -1f); uv[v + 1] = new Vector2(1f, -1f); uv[v + 2] = new Vector2(1f, 1f); uv[v + 3] = new Vector2(-1f, 1f);
                for (int k = 0; k < 4; k++) birth[v + k] = new Vector2(-100f, 0f);
            }
            mesh = new Mesh { name = "OzLavaPrints", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            mesh.vertices = verts; mesh.uv = uv; mesh.uv2 = birth; mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 400f, 4000f));
            go = new GameObject("LAV_Prints"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Resources.Load<Material>(MaterialRes);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.enabled = false;
            if (!mr.sharedMaterial) Debug.LogWarning("[OpenZ] lava: " + MaterialRes + " missing, no footprints");
        }

        /// <summary>One print at foot, pointing along dir, size 1 for a 2.4 m robot. False when it cannot be drawn.</summary>
        public bool Add(Vector3 foot, Vector3 dir, float size, float seed)
        {
            var lava = OzLava.I; if (!lava || !mr.sharedMaterial) return false;
            dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward; dir.Normalize();
            var side = Vector3.Cross(Vector3.up, dir) * (0.20f * size); var along = dir * (0.34f * size);
            var c = new Vector3(foot.x, 0f, foot.z);
            int v = next * 4; next = (next + 1) % Capacity;
            verts[v] = Seat(c - along - side); verts[v + 1] = Seat(c - along + side); verts[v + 2] = Seat(c + along + side); verts[v + 3] = Seat(c + along - side);
            float t = lava.Clock;
            for (int k = 0; k < 4; k++) birth[v + k] = new Vector2(t, seed);
            mesh.SetVertices(verts, 0, verts.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetUVs(1, birth, 0, birth.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            lastBirth = t; mr.enabled = true;
            return true;
        }

        /// <summary>On the ground as it is drawn (the walk surface can lie under it, and a print there would never show), no ray.</summary>
        Vector3 Seat(Vector3 p) => SelectionOverlay.Seat(game, p, 0.025f, false);

        /// <summary>Only turns the one draw off once every print is cold.</summary>
        public void Tick()
        {
            var lava = OzLava.I;
            if (mr.enabled && lava && lava.Clock > lastBirth + Life + 0.5f) mr.enabled = false;
        }

        public void Dispose()
        {
            if (go) Object.Destroy(go);
            if (mesh) Object.Destroy(mesh);
        }
    }
}
