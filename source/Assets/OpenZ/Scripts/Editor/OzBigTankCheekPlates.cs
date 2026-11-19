using System.Collections.Generic;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Mammoth's gun mantlet, between it and each turret cheek, the GLB's turret front has an open slot about 0.15 m wide from the turret
    /// ring up to the mantlet's top (rays measured on the live model: the turret-core fix only made it a black burnt hollow). The prefab
    /// builder (menu OpenZ/1.5/Big Tanks/1, Editor/OzBigTanksSetup.cs) now adds one armour piece per side under the turret's yaw node:
    /// a plate behind the mantlet across the slot and a cheek block beside the mantlet, flush with the cheek face, in the turret's own
    /// paint (its material, UVs inside one paint island of mammoth_head_albedo). The mantlet keeps its own room (it pitches and the
    /// barrels recoil inside it); the pieces turn and fly with the turret, and the renderer's name ends in "_turret" so the cook-off
    /// break hides it with the turret shell (View/OzBigTankWreck.cs). The Bizon has no such slot (measured the same way). No GLB is
    /// changed. Switch <see cref="On"/> (off = the prefab as before).
    /// </summary>
    public static class OzBigTankCheekPlates
    {
        public static bool On = true;
        public const string NodeName = "Mammoth_cheeks_turret";
        public const string MeshPath = "Assets/OpenZ/Resources/OpenZ/BigTanks/Z_mammoth_cheek_plates.asset";
        /// <summary>A paint texel of mammoth_head_albedo (alpha 128 over a 40 px window round it) and the UV per metre of the plates.</summary>
        static readonly Vector2 PaintUv = new Vector2(0.4289f, 0.2304f);
        const float UvPerM = 0.02f;

        /// <summary>The boxes in the turret yaw node's space (x + = the tank's left; the gun points to -z): the plate behind the mantlet, the
        /// block beside it. Mirrored for the right side.</summary>
        static readonly Vector3[] Left =
        {
            new Vector3(0.64f, -0.10f, -1.26f), new Vector3(1.00f, 0.74f, -0.92f),
            new Vector3(0.84f, -0.10f, -1.42f), new Vector3(1.00f, 0.74f, -1.26f),
        };

        public static string Add(Transform model, string type)
        {
            if (!On || type != ZBigTanks.Mammoth || !model) return "";
            Transform yaw = null, turret = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "MammothTurretYaw") yaw = t;
                else if (t.name == "Mammoth_turret") turret = t;
            }
            if (!yaw || !turret) return "NO cheek plates (MammothTurretYaw / Mammoth_turret missing)";
            var tr = turret.GetComponent<MeshRenderer>();
            if (!tr) return "NO cheek plates (Mammoth_turret has no renderer)";
            var boxes = new List<Vector3>(Left);
            for (int i = 0; i < Left.Length; i += 2)
            {
                var lo = Left[i]; var hi = Left[i + 1];
                boxes.Add(new Vector3(-hi.x, lo.y, lo.z)); boxes.Add(new Vector3(-lo.x, hi.y, hi.z));
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool made = !mesh;
            if (made) mesh = new Mesh();
            Build(mesh, boxes);
            mesh.name = "Z_mammoth_cheek_plates";
            if (made) AssetDatabase.CreateAsset(mesh, MeshPath); else EditorUtility.SetDirty(mesh);
            var old = yaw.Find(NodeName); if (old) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(NodeName); go.transform.SetParent(yaw, false); go.layer = turret.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = tr.sharedMaterial;
            mr.shadowCastingMode = tr.shadowCastingMode; mr.receiveShadows = tr.receiveShadows;
            mr.lightProbeUsage = tr.lightProbeUsage; mr.reflectionProbeUsage = tr.reflectionProbeUsage;
            mr.motionVectorGenerationMode = tr.motionVectorGenerationMode; mr.allowOcclusionWhenDynamic = tr.allowOcclusionWhenDynamic;
            return $"cheek plates {NodeName} ({boxes.Count / 2} boxes, {mesh.triangles.Length / 3} tris, material {tr.sharedMaterial.name})";
        }

        static void Build(Mesh m, List<Vector3> bb)
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>(); var ts = new List<int>();
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int b = 0; b + 1 < bb.Count; b += 2)
            {
                var lo = bb[b]; var hi = bb[b + 1]; var c = (lo + hi) * 0.5f; var e = (hi - lo) * 0.5f;
                foreach (var n in dirs)
                {
                    var u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up; var w = Vector3.Cross(n, u);
                    var fc = c + Vector3.Scale(n, e); float eu = Vector3.Scale(u, e).magnitude, ew = Vector3.Scale(w, e).magnitude;
                    var p = new[] { fc - u * eu - w * ew, fc + u * eu - w * ew, fc + u * eu + w * ew, fc - u * eu + w * ew };
                    int i0 = vs.Count;
                    foreach (var q in p)
                    {
                        vs.Add(q); ns.Add(n);
                        uvs.Add(PaintUv + new Vector2(Vector3.Dot(q - c, w), Vector3.Dot(q - c, u)) * UvPerM);   // inside the 40 px paint window
                    }
                    bool flip = Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), n) < 0f;
                    if (!flip) ts.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                    else ts.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
                }
            }
            m.Clear(); m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, uvs); m.SetTriangles(ts, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
        }
    }
}
