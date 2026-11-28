using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>The three-tube launcher uses the symmetric armored hull and production MLRS drivetrain.</summary>
    public sealed class OzClassicMlrsModel : MonoBehaviour
    {
        const float RestElevation = 4f;
        const float MinimumElevation = -17f, MaximumElevation = 14f;
        const float TurretHeight = 2.24f;
        const float TurretPivotZ = 0.10f;
        const float TubeMouth = 0.61f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        enum Surface { Armor, Steel, Rim, Dark, Team, Glass, Lamp, Count }
        static Mesh cabMesh, turretMesh, trayMesh, tubeMesh;
        static Material[] materials;
        readonly Transform[] sockets = new Transform[3];
        MaterialPropertyBlock paint;
        MeshRenderer cab, launcher, tray, tubes;

        readonly List<Light> lamps = new List<Light>(4);
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly Color LampColor = new Color(1f, 0.76f, 0.29f);
        bool lampsOn;
        Transform rack;
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
            paint = new MaterialPropertyBlock();
            EnsureAssets();
            foreach (var part in swivel.GetComponentsInChildren<Transform>(true))
            {
                if (part.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
                if (part.name.Contains("Muzzle_") || part.name == "MLRSRackPitch")
                    part.name = "UnusedClassicMlrsPart";
            }

            foreach (var part in hullSpace.GetComponentsInChildren<Transform>(true))
            {
                if (!part.TryGetComponent<Renderer>(out var renderer)) continue;
                bool wheel = part.name.StartsWith("MLRS_wheel_", StringComparison.Ordinal);
                renderer.enabled = wheel || part.name.StartsWith("MLRS_RegularTread_", StringComparison.Ordinal);
                if (wheel && int.TryParse(part.name.Substring("MLRS_wheel_".Length), out int index))
                {
                    int slot = index % 5;
                    renderer.enabled = slot != 2;
                    if (slot != 2 && part.parent)
                    {
                        var position = part.parent.localPosition;
                        position.z = (slot < 2 ? slot - 1.5f : slot - 2.5f) * 1.09f;
                        part.parent.localPosition = position;
                    }
                }
            }

            cab = AddMesh(hullSpace, "Classic MLRS cabin", cabMesh);
            foreach (int side in new[] { -1, 1 })
            {
                AddLamp(hullSpace, new Vector3(side * 0.752f, 0.94f, -2.40f), true);
                AddLamp(hullSpace, new Vector3(side * 0.752f, 0.94f, 2.40f), false);
            }
            swivel.position = hullSpace.TransformPoint(new Vector3(0f, TurretHeight, TurretPivotZ));
            launcher = AddMesh(swivel, "Classic MLRS turret", turretMesh);
            rack = Node(swivel, "Classic MLRS tilting tray", Vector3.zero);
            var rest = Quaternion.AngleAxis(RestElevation, Vector3.right);
            rack.localRotation = rest;
            tray = AddMesh(rack, "Classic MLRS tray", trayMesh);
            var cradle = Node(rack, "Classic MLRS tube mount", new Vector3(0f, 0.32f, -0.64f));
            tubes = AddMesh(cradle, "Classic MLRS three tubes", tubeMesh);
            for (int i = 0; i < sockets.Length; i++)
            {
                float x = (i - 1) * ZObject.ClassicMissileTubeSpacing;
                sockets[i] = Node(cradle, i == 1 ? "Muzzle" : "Classic MLRS outlet", new Vector3(x, 0f, -TubeMouth));
            }
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

        void AddLamp(Transform parent, Vector3 position, bool front)
        {
            var node = Node(parent, front ? "Classic MLRS front lamp" : "Classic MLRS rear lamp", position);
            node.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.22f, front ? -1f : 1f));
            var lamp = node.gameObject.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = LampColor;
            lamp.range = front ? 13f : 8f;
            lamp.spotAngle = front ? 51f : 65f;
            lamp.innerSpotAngle = lamp.spotAngle * 0.45f;
            lamp.intensity = front ? 4.0f : 2.6f;
            lamp.enabled = false;
            lamps.Add(lamp);
        }

        void LateUpdate()
        {
            ApplyPaint();
            var target = unit.AttackObject;
            float wanted = RestElevation;
            if (target != null && !target.Destroyed && ZGame.I)
            {
                Vector3 local = rack.parent.InverseTransformPoint(ZGame.I.CenterWorld(target) + Vector3.up * 0.50f);
                Vector3 outlet = rack.parent.InverseTransformPoint(CenterOutlet);
                float distance = new Vector2(local.x - outlet.x, local.z - outlet.z).magnitude;
                wanted = Mathf.Clamp(Mathf.Atan2(local.y - outlet.y, distance) * Mathf.Rad2Deg,
                    MinimumElevation, MaximumElevation);
            }
            rack.localRotation = Quaternion.RotateTowards(rack.localRotation,
                Quaternion.AngleAxis(wanted, Vector3.right), 42f * Time.deltaTime);
            bool on = NightGrade.NightK > 0.35f && unit.Owner != Team.Neutral && !unit.Destroyed;
            if (on == lampsOn) return;
            lampsOn = on;
            foreach (var lamp in lamps) lamp.enabled = on;
            cab.GetPropertyBlock(paint, (int)Surface.Lamp);
            paint.SetColor(EmissionColor, on ? LampColor * 3f : Color.black);
            cab.SetPropertyBlock(paint, (int)Surface.Lamp);
            paint.Clear();
        }

        void ApplyPaint()
        {
            Team color = TeamColors.Shown(unit.Owner);
            if (shown == color) return;
            shown = color;
            paint.SetColor(BaseColor, TeamColors.OriginalPaint(shown));
            cab.SetPropertyBlock(paint, (int)Surface.Team);
            launcher.SetPropertyBlock(paint, (int)Surface.Team);
            tray.SetPropertyBlock(paint, (int)Surface.Team);
            tubes.SetPropertyBlock(paint, (int)Surface.Team);
        }

        public Vector3 AimSalvo(Vector3 target)
        {
            Vector3 line = target - CenterOutlet;
            float run = new Vector2(line.x, line.z).magnitude;
            line.y = Mathf.Max(line.y, run * Mathf.Tan(MinimumElevation * Mathf.Deg2Rad));
            return line;
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

        static Texture2D MetalFinish()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            { name = "MSLR 1996 brushed steel", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cloud = Mathf.PerlinNoise(x * .018f + 23f, y * .018f);
                float pits = Mathf.PerlinNoise(x * .17f, y * .17f + 7f);
                float brush = Mathf.PerlinNoise(x * .029f - y * .033f + 16f, y * .83f);
                uint grit = unchecked((uint)(x * 73856093 ^ y * 19349663));
                grit = (grit ^ (grit >> 13)) * 1274126177u;
                float value = 224f + (cloud - .5f) * 41f + ((grit & 255) / 255f - .5f) * 13f
                    - Mathf.Max(0f, pits - .53f) * 100f + Mathf.Max(0f, brush - .60f) * 100f;
                byte gray = (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
                pixels[y * size + x] = new Color32(gray, gray, gray, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static void EnsureAssets()
        {
            if (cabMesh && turretMesh && trayMesh && tubeMesh) return;
            materials = new[]
            {
                MaterialFor(new Color32(146, 143, 136, 255), 0.39f, 0.31f),
                MaterialFor(new Color32(153, 153, 151, 255), 0.46f, 0.39f),
                MaterialFor(new Color32(171, 168, 158, 255), 0.53f, 0.43f),
                MaterialFor(new Color32(43, 46, 48, 255), 0.22f, 0.26f),
                MaterialFor(new Color32(176, 40, 30, 255), 0.14f, 0.38f),
                MaterialFor(new Color32(36, 62, 74, 255), 0.32f, 0.67f),
                MaterialFor(new Color32(224, 165, 83, 255), 0.1f, 0.48f)
            };
            materials[(int)Surface.Lamp].EnableKeyword("_EMISSION");
            var finish = MetalFinish();
            foreach (var slot in new[] { Surface.Armor, Surface.Steel, Surface.Rim, Surface.Dark, Surface.Team })
                materials[(int)slot].SetTexture("_BaseMap", finish);

            var body = new Geometry();
            body.Box(0, .73f, 0, 2.23f, .83f, 4.44f, Surface.Armor);
            body.Box(0, .42f, 0, 2.12f, .24f, 4.17f, Surface.Dark);
            body.Box(0, 1.31f, .29f, 2.72f, .48f, 3.14f, Surface.Armor);
            body.Box(0, 1.50f, .10f, 2.33f, .16f, 2.20f, Surface.Steel);
            body.Box(0, 1.28f, -1.53f, 1.96f, .28f, 1.23f, Surface.Armor, -27f);
            body.Box(0, 1.57f, -1.16f, 1.67f, .075f, .47f, Surface.Team, -6f);
            body.Box(0, 1.62f, -1.16f, .33f, .055f, .089f, Surface.Dark);
            body.Box(0, .895f, -2.17f, 1.97f, .57f, .32f, Surface.Armor);
            body.Box(0, .92f, -2.345f, .55f, .32f, .041f, Surface.Dark);
            body.Box(0, .94f, 2.10f, 1.95f, .65f, .45f, Surface.Armor);
            for (int i = -2; i <= 2; i++)
                body.Box(i * .105f, .92f, -2.373f, .035f, .26f, .034f, Surface.Rim);

            foreach (int side in new[] { -1, 1 })
            {
                body.Box(side * .457f, 1.428f, -1.68f, .814f, .072f, .61f, Surface.Dark, -27f);
                body.Box(side * .457f, 1.459f, -1.706f, .695f, .072f, .49f, Surface.Glass, -27f);
                body.Box(side * 1.37f, 1.24f, 0f, .57f, .39f, 3.43f, Surface.Armor);
                body.Box(side * 1.37f, 1.28f, -1.87f, .60f, .22f, .76f, Surface.Steel, -28f);
                body.Box(side * 1.37f, 1.28f, 1.87f, .60f, .22f, .76f, Surface.Steel, 25f);
                body.Box(side * 1.661f, 1.18f, 0, .036f, .11f, 3.15f, Surface.Team);
                body.Box(side * 1.676f, 1.35f, .50f, .077f, .245f, .86f, Surface.Steel);
                body.Box(side * 1.719f, 1.35f, .50f, .032f, .187f, .69f, Surface.Dark);
                for (int i = -2; i <= 2; i++)
                    body.Box(side * 1.745f, 1.35f, .50f + i * .113f, .032f, .183f, .047f, Surface.Rim);

                body.Box(side * .76f, .63f, -2.255f, .34f, .27f, .28f, Surface.Steel, -16f);
                foreach (int end in new[] { -1, 1 })
                {
                    body.Box(side * .752f, .94f, end * 2.30f, .269f, .209f, .101f, Surface.Dark);
                    body.Box(side * .752f, .94f, end * 2.359f, .195f, .133f, .032f, Surface.Lamp);
                }

                body.Box(side * .31f, .965f, 2.343f, .46f, .30f, .045f, Surface.Dark);
                for (int i = -1; i <= 1; i++)
                {
                    body.Box(side * .31f, .965f + i * .072f, 2.374f, .41f, .025f, .025f, Surface.Steel);
                    body.Box(side * .31f + i * .107f, .965f, 2.376f, .027f, .242f, .026f, Surface.Steel);
                }
                foreach (float z in new[] { -1.42f, -.74f, -.10f, .58f, 1.32f })
                {
                    body.Box(side * 1.22f, 1.446f, z, .049f, .031f, .049f, Surface.Rim);
                    body.Box(side * 1.53f, 1.446f, z, .044f, .031f, .044f, Surface.Rim);
                }
                foreach (float z in new[] { -1.26f, -1.03f })
                    body.Box(side * .695f, 1.622f, z, .045f, .033f, .045f, Surface.Rim);
            }
            cabMesh = body.Mesh("MSLR 1996 symmetric armored hull");

            var turret = new Geometry();
            turret.RoundBase(-.62f, .15f, 1.14f, Surface.Dark);
            turret.RoundBase(-.51f, .16f, 1.09f, Surface.Rim);
            turret.RoundBase(-.35f, .22f, .97f, Surface.Armor);
            turret.Box(0, -.145f, 0, 1.84f, .28f, .66f, Surface.Dark);
            var platform = new Geometry();
            platform.Box(0, -.035f, .03f, 2.57f, .18f, 1.88f, Surface.Steel);
            var rack = new Geometry();
            for (int i = -1; i <= 1; i++)
            {
                float x = i * ZObject.ClassicMissileTubeSpacing;
                platform.Box(x, 0.047f, -0.39f, 0.40f, 0.086f, 0.30f, Surface.Dark);
                platform.Box(x, 0.047f, 0.49f, 0.40f, 0.086f, 0.30f, Surface.Dark);
                rack.Tube(x);
            }
            foreach (int side in new[] { -1, 1 })
            {
                platform.Box(side * 1.02f, .03f, .08f, .46f, .34f, 1.82f, Surface.Armor);
                platform.Box(side * 1.252f, .03f, .08f, .043f, .193f, 1.35f, Surface.Dark);
                for (int i = -4; i <= 4; i++)
                    platform.Box(side * 1.279f, .03f, .08f + i * .14f, .033f, .181f, .047f, Surface.Rim);
                foreach (float z in new[] { -0.64f, -0.40f, 0.40f, 0.64f })
                    platform.Box(side * 1.185f, 0.049f, z, 0.066f, 0.026f, 0.066f, Surface.Rim);
            }
            turretMesh = turret.Mesh("OG Z turret pedestal");
            trayMesh = platform.Mesh("OG Z tilting tray");
            tubeMesh = rack.Mesh("OG Z three parallel tubes");
        }

        sealed class Geometry
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
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
                Vector3 n = normalA + normalB;
                n = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                Vector2 Project(Vector3 v) => n.y >= n.x && n.y >= n.z ? new Vector2(v.x, v.z) :
                    n.x > n.z ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y);
                uvs.Add(Project(a)); uvs.Add(Project(b)); uvs.Add(Project(c)); uvs.Add(Project(d));
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

            public void Box(float x, float y, float z, float width, float height, float length, Surface surface, float tilt = 0f)
            {
                float bevel = Mathf.Min(0.061f, Mathf.Min(width, Mathf.Min(height, length)) * 0.23f);
                var rings = new Vector3[4][];
                var center = new Vector3(x, y, z);
                var rotation = Quaternion.AngleAxis(tilt, Vector3.right);
                for (int level = 0; level < rings.Length; level++)
                {
                    float inset = level == 0 || level == 3 ? bevel * 0.7f : 0f;
                    float a = width * 0.5f - inset, b = length * 0.5f - inset;
                    float up = (level < 2 ? -1f : 1f) * (height * 0.5f - (inset == 0f ? bevel : 0f));
                    rings[level] = new[] { At(-a + bevel, -b), At(a - bevel, -b), At(a, -b + bevel), At(a, b - bevel),
                        At(a - bevel, b), At(-a + bevel, b), At(-a, b - bevel), At(-a, -b + bevel) };
                    Vector3 At(float side, float fore) => center + rotation * new Vector3(side, up, fore);
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
                    Vector3 top = center + rotation * Vector3.up * (height * 0.5f);
                    Vector3 bottom = center - rotation * Vector3.up * (height * 0.5f);
                    Flat(top, rings[3][i], rings[3][j], top, surface, Vector3.up);
                    Flat(bottom, rings[0][i], rings[0][j], bottom, surface, Vector3.down);
                }
            }

            public void RoundBase(float y, float height, float radius, Surface surface)
            {
                const int sides = 48;
                float low = y - height * 0.5f, high = y + height * 0.5f;
                const float bevel = 0.045f;
                Vector3 At(Vector3 dir, float r, float level) => dir * r + Vector3.up * level;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * 2f * Mathf.PI / sides, b = (i + 1) * 2f * Mathf.PI / sides;
                    var u = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var v = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
                    Face(At(u, radius, low + bevel), At(v, radius, low + bevel),
                        At(v, radius, high - bevel), At(u, radius, high - bevel), surface, u, v);
                    Flat(At(u, radius, high - bevel), At(v, radius, high - bevel),
                        At(v, radius - bevel, high), At(u, radius - bevel, high), surface, u + Vector3.up);
                    Flat(At(u, radius - bevel, high), At(v, radius - bevel, high),
                        Vector3.up * high, Vector3.up * high, surface, Vector3.up);
                    Flat(At(v, radius, low + bevel), At(u, radius, low + bevel),
                        At(u, radius - bevel, low), At(v, radius - bevel, low), surface, u + Vector3.down);
                }
            }

            public void Tube(float x)
            {
                // Each profile step gives its axial position, radius and the finish leading to the next ring.
                var profile = new (float z, float radius, Surface surface)[]
                {
                    (1.969f, 0, Surface.Steel), (1.969f, 0.245f, Surface.Steel), (1.950f, 0.292f, Surface.Rim), (1.940f, 0.315f, Surface.Rim), (1.91f, 0.35f, Surface.Steel),
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
                mesh.SetUVs(0, uvs);
                for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
