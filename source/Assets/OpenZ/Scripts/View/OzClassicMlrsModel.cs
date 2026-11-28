using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>The three-tube launcher uses the production MLRS hull, drivetrain, crew and animation.</summary>
    public sealed class OzClassicMlrsModel : MonoBehaviour
    {
        const float RestElevation = 4f;
        const float TubeHeight = 3.11f;
        const float TurretHeight = 2.65f;
        const float TubeMouth = 0.61f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        enum Surface { Armor, Steel, Rim, Dark, Team, Glass, Lamp, Count }
        static Mesh cabMesh, turretMesh, tubeMesh;
        static Material[] materials;
        readonly Transform[] sockets = new Transform[3];
        readonly MaterialPropertyBlock paint = new MaterialPropertyBlock();
        MeshRenderer cab, launcher, tubes;
        ZObject unit;
        Team shown = (Team)(-1);

        public static void Fit(UnitView view, ZObject unit)
        {
            if (!unit.UsesClassicMissileLauncher || view.GetComponent<OzClassicMlrsModel>()) return;
            Transform hull = null, swivel = null;
            foreach (var part in view.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "MLRS_hull") hull = part;
                if (part.name == "MLRSRackYaw") swivel = part;
            }
            if (!hull || !swivel)
            {
                Debug.LogWarning("The classic missile launcher needs a production MLRS model.", view);
                return;
            }

            var model = view.gameObject.AddComponent<OzClassicMlrsModel>();
            model.Build(hull.parent, swivel, unit);
        }

        void Build(Transform hullSpace, Transform swivel, ZObject owner)
        {
            unit = owner;
            EnsureAssets();
            foreach (var part in swivel.GetComponentsInChildren<Transform>(true))
            {
                if (part.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
                if (part.name.Contains("Muzzle_") || part.name == "MLRSRackPitch")
                    part.name = "UnusedClassicMlrsPart";
            }

            foreach (var part in hullSpace.GetComponentsInChildren<Transform>(true))
            {
                if ((part.name.StartsWith("mlrs_Hatch", StringComparison.Ordinal) ||
                    part.name.StartsWith("MLRS_OwnerGlow_Cab", StringComparison.Ordinal)) &&
                    part.TryGetComponent<Renderer>(out var renderer))
                    renderer.enabled = false;
            }

            cab = AddMesh(hullSpace, "Classic MLRS cabin", cabMesh);
            swivel.position = hullSpace.TransformPoint(new Vector3(0f, TurretHeight, 0.62f));
            launcher = AddMesh(swivel, "Classic MLRS turret", turretMesh);
            var rack = Node(swivel, "Classic MLRS tube mount", new Vector3(0f, TubeHeight - TurretHeight, 0f));
            rack.localRotation = Quaternion.AngleAxis(RestElevation, Vector3.right);
            tubes = AddMesh(rack, "Classic MLRS three tubes", tubeMesh);
            for (int i = 0; i < sockets.Length; i++)
            {
                float x = (i - 1) * ZObject.ClassicMissileTubeSpacing;
                sockets[i] = Node(rack, i == 1 ? "Muzzle" : "Classic MLRS outlet", new Vector3(x, 0f, -TubeMouth));
            }
            ApplyPaint();
        }

        static Transform Node(Transform parent, string name, Vector3 at)
        {
            var node = new GameObject(name) { layer = parent.gameObject.layer }.transform;
            node.SetParent(parent, false);
            node.localPosition = at;
            return node;
        }

        static MeshRenderer AddMesh(Transform parent, string name, Mesh mesh)
        {
            var go = Node(parent, name, Vector3.zero).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            return renderer;
        }

        void LateUpdate() => ApplyPaint();

        void ApplyPaint()
        {
            Team color = TeamColors.Shown(unit.Owner);
            if (shown == color) return;
            shown = color;
            paint.SetColor(BaseColor, TeamColors.OriginalPaint(shown));
            cab.SetPropertyBlock(paint, (int)Surface.Team);
            launcher.SetPropertyBlock(paint, (int)Surface.Team);
            tubes.SetPropertyBlock(paint, (int)Surface.Team);
        }

        public Vector3 CenterOutlet => sockets[1].position;
        public Vector3 Outlet(int index) => sockets[index].position;

        static Material MaterialFor(Color32 color, float metal, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Classic MLRS metal" };
            material.SetColor(BaseColor, color);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        static void EnsureAssets()
        {
            if (cabMesh && turretMesh && tubeMesh) return;
            materials = new[]
            {
                MaterialFor(new Color32(163, 158, 144, 255), 0.20f, 0.36f),
                MaterialFor(new Color32(155, 173, 181, 255), 0.43f, 0.44f),
                MaterialFor(new Color32(194, 198, 192, 255), 0.55f, 0.55f),
                MaterialFor(new Color32(43, 46, 48, 255), 0.22f, 0.26f),
                MaterialFor(new Color32(176, 40, 30, 255), 0.14f, 0.38f),
                MaterialFor(new Color32(36, 62, 74, 255), 0.32f, 0.67f),
                MaterialFor(new Color32(224, 165, 83, 255), 0.1f, 0.48f)
            };
            var body = new Geometry();
            body.Box(0, 1.81f, -1.36f, 1.76f, 1.14f, 1.63f, Surface.Armor);
            body.Box(0, 2.39f, -1.36f, 1.86f, 0.12f, 1.66f, Surface.Armor);
            body.Box(0, 2.463f, -1.48f, 1.34f, 0.045f, 1.05f, Surface.Team);
            body.Box(0, 2.505f, -1.48f, 0.24f, 0.045f, 0.06f, Surface.Dark);
            body.Box(0, 2.03f, -2.173f, 1.53f, 0.37f, 0.07f, Surface.Team);
            body.Box(0, 1.44f, -2.168f, 0.63f, 0.17f, 0.055f, Surface.Dark);
            for (int i = -2; i <= 2; i++)
                body.Box(i * 0.105f, 1.44f, -2.201f, 0.028f, 0.099f, 0.015f, Surface.Steel);
            foreach (int side in new[] { -1, 1 })
            {
                body.Box(side * 0.358f, 2.036f, -2.215f, 0.655f, 0.245f, 0.057f, Surface.Glass);
                body.Box(side * 0.901f, 2.02f, -1.71f, 0.07f, 0.249f, 0.56f, Surface.Dark);
                body.Box(side * 0.945f, 2.025f, -1.71f, 0.04f, 0.157f, 0.46f, Surface.Glass);
                body.Box(side * 0.9f, 1.62f, -1.36f, 0.09f, 0.25f, 0.74f, Surface.Dark);
                body.Box(side * 0.96f, 1.61f, -1.36f, 0.05f, 0.19f, 0.64f, Surface.Armor);
                body.Box(side * 0.646f, 1.39f, -2.20f, 0.172f, 0.192f, 0.095f, Surface.Dark);
                body.Box(side * 0.646f, 1.39f, -2.251f, 0.105f, 0.13f, 0.028f, Surface.Lamp);
                foreach (float z in new[] { -0.75f, -1.93f })
                    body.Box(side * 0.771f, 2.463f, z, 0.06f, 0.035f, 0.06f, Surface.Rim);
            }
            cabMesh = body.Mesh("OG Z cabin");

            var turret = new Geometry();
            turret.Box(0, -0.69f, 0f, 1.63f, 0.93f, 1.38f, Surface.Armor);
            turret.Box(0, -0.53f, 0f, 1.73f, 0.12f, 1.46f, Surface.Dark);
            turret.Box(0, 0f, 0f, 2.53f, 0.19f, 1.68f, Surface.Armor);
            var rack = new Geometry();
            for (int i = -1; i <= 1; i++)
            {
                float x = i * ZObject.ClassicMissileTubeSpacing;
                turret.Box(x, 0.137f, -0.38f, 0.34f, 0.24f, 0.39f, Surface.Dark);
                turret.Box(x, 0.113f, 0.59f, 0.34f, 0.19f, 0.39f, Surface.Dark);
                rack.Tube(x);
            }
            foreach (int side in new[] { -1, 1 })
                foreach (float z in new[] { -0.66f, -0.44f, 0.45f, 0.68f })
                    turret.Box(side * 1.185f, 0.104f, z, 0.07f, 0.038f, 0.07f, Surface.Rim);
            turretMesh = turret.Mesh("OG Z turret");
            tubeMesh = rack.Mesh("OG Z three parallel tubes");
        }

        sealed class Geometry
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int>[] indices = new List<int>[(int)Surface.Count];

            public Geometry()
            {
                for (int i = 0; i < indices.Length; i++) indices[i] = new List<int>();
            }

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Surface surface, Vector3 normalA, Vector3 normalB)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                normals.Add(normalA); normals.Add(normalB); normals.Add(normalB); normals.Add(normalA);
                var list = indices[(int)surface];
                Vector3 winding = Vector3.Cross(b - a, c - a);
                if (winding.sqrMagnitude < 1e-12f) winding = Vector3.Cross(c - a, d - a);
                if (Vector3.Dot(winding, normalA) >= 0f)
                    list.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else
                    list.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }

            void Flat(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Surface surface, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, outward) < 0f) n = -n;
                Face(a, b, c, d, surface, n, n);
            }

            public void Box(float x, float y, float z, float width, float height, float length, Surface surface)
            {
                float bevel = Mathf.Min(0.061f, Mathf.Min(width, Mathf.Min(height, length)) * 0.23f);
                var rings = new Vector3[4][];
                var center = new Vector3(x, y, z);
                for (int level = 0; level < rings.Length; level++)
                {
                    float inset = level == 0 || level == 3 ? bevel * 0.7f : 0f;
                    float a = width * 0.5f - inset, b = length * 0.5f - inset;
                    float up = (level < 2 ? -1f : 1f) * (height * 0.5f - (inset == 0f ? bevel : 0f));
                    rings[level] = new[] { At(-a + bevel, -b), At(a - bevel, -b), At(a, -b + bevel), At(a, b - bevel),
                        At(a - bevel, b), At(-a + bevel, b), At(-a, b - bevel), At(-a, -b + bevel) };
                    Vector3 At(float side, float fore) => center + new Vector3(side, up, fore);
                }
                for (int i = 0; i < 8; i++)
                {
                    int j = (i + 1) % 8;
                    for (int k = 0; k < 3; k++)
                    {
                        var a = rings[k][i]; var b = rings[k][j];
                        var c = rings[k + 1][j]; var d = rings[k + 1][i];
                        Flat(a, b, c, d, surface, (a + b + c + d) * 0.25f - center);
                    }
                    Vector3 top = center + Vector3.up * (height * 0.5f);
                    Vector3 bottom = center - Vector3.up * (height * 0.5f);
                    Flat(top, rings[3][i], rings[3][j], top, surface, Vector3.up);
                    Flat(bottom, rings[0][i], rings[0][j], bottom, surface, Vector3.down);
                }
            }

            public void Tube(float x)
            {
                // Each profile step gives its axial position, radius and the finish leading to the next ring.
                var profile = new (float z, float radius, Surface surface)[]
                {
                    (1.94f, 0, Surface.Dark), (1.94f, 0.31f, Surface.Rim), (1.91f, 0.35f, Surface.Steel),
                    (1.78f, 0.35f, Surface.Rim), (1.73f, 0.34f, Surface.Steel),
                    (1.17f, 0.334f, Surface.Rim), (1.15f, 0.353f, Surface.Team), (0.96f, 0.353f, Surface.Rim),
                    (0.935f, 0.334f, Surface.Steel), (-0.38f, 0.334f, Surface.Steel), (-0.415f, 0.37f, Surface.Rim),
                    (-0.455f, 0.37f, Surface.Steel), (-0.555f, 0.37f, Surface.Rim), (-TubeMouth, 0.345f, Surface.Rim),
                    (-TubeMouth, 0.287f, Surface.Dark), (-0.55f, 0.263f, Surface.Dark),
                    (0.13f, 0.26f, Surface.Dark), (0.14f, 0, Surface.Dark)
                };
                const int sides = 40;
                for (int j = 0; j < profile.Length - 1; j++)
                {
                    var a = profile[j]; var b = profile[j + 1];
                    Vector3 Radial(int i)
                    {
                        float angle = i * (2f * Mathf.PI / sides);
                        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    }
                    Vector3 normal(Vector3 r) => (r * (a.z - b.z) + Vector3.forward * (b.radius - a.radius)).normalized;
                    for (int i = 0; i < sides; i++)
                    {
                        var r = Radial(i); var s = Radial(i + 1);
                        Vector3 start = new Vector3(x, 0, a.z), end = new Vector3(x, 0, b.z);
                        Face(start + r * a.radius, start + s * a.radius, end + s * b.radius, end + r * b.radius,
                            a.surface, normal(r), normal(s));
                    }
                }
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name, subMeshCount = indices.Length };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
