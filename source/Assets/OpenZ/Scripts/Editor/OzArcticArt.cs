// OpenZ-VK (GPL-3.0). Original, reusable winter art generated with Unity primitives.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenZ.EditorTools
{
    internal static class OzArcticArt
    {
        internal const string Folder = "Packages/org.openz.aimaps/Art/Arctic/";
        internal const float BridgeWidth = 7.6f, BridgeLength = 18f;
        static readonly string[] BridgeStages = { "intact", "damaged", "destroyed", "chunks" };

        internal static Material Mat(string name) => Load<Material>(name + ".mat");
        internal static GameObject Bridge(string key) => Load<GameObject>("Z_" + key + ".prefab");
        internal static GameObject[] BridgeKit() => BridgeStages.Select(state => Bridge("bridge_arctic_" + state)).ToArray();

        static T Load<T>(string file) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Folder + file);
            if (!asset) throw new InvalidOperationException("Missing Arctic kit asset " + file);
            return asset;
        }

        [MenuItem("OpenZ/AI Maps/Rebuild original Arctic art")]
        public static void Generate()
        {
            Directory.CreateDirectory("../aimaps/Art/Arctic");
            AssetDatabase.Refresh();
            var detail = Texture("SnowGrain", false);
            var strata = Texture("SlateStrata", true);

            var snow = new Material(Shader.Find("OpenZ/Sand")) { name = "Wind-packed snow" };
            snow.SetTexture("_DetailTex", detail);
            snow.SetTexture("_DetailNrm", Texture2D.grayTexture);
            snow.SetFloat("_DetailOn", 1);
            snow.SetFloat("_DetailNormalStr", 0);
            snow.SetFloat("_Smoothness", .065f);
            snow.SetVector("_DetailTiles", new Vector4(2.4f, 7.3f, 0, 0));
            snow.SetVector("_DetailStr", new Vector4(.52f, .65f, .53f, .07f));
            snow.SetVector("_DetailFade", new Vector4(105, 250, .25f, 0));
            Save(snow, "Snow.mat");

            var slate = Surface("Blue slate and snow caps", new Color(.34f, .39f, .49f));
            slate.SetTexture("_BaseMap", strata);
            slate.SetFloat("_Triplanar", 1);
            slate.SetFloat("_TriScale", .22f);
            slate.SetFloat("_TopBlend", .94f);
            slate.SetFloat("_TriHeight", 8.5f);
            slate.SetFloat("_TopMin", .51f);
            slate.SetFloat("_TopMax", .79f);
            slate.SetColor("_TopColor", new Color(.84f, .90f, .96f));
            Save(slate, "Slate.mat");
            var boulder = new Material(Mat("Slate")) { name = "Faceted snow-capped boulders" };
            boulder.SetFloat("_FlatShade", 1);
            Save(boulder, "Boulder.mat");
            var shoal = Surface("Submerged slate", new Color(.31f, .50f, .64f));
            shoal.SetTexture("_BaseMap", strata);
            Save(shoal, "Shoal.mat");

            var template = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/OpenZ/Materials/Env/WaterV4/M_Water_v3_backup.mat");
            var water = new Material(template) { name = "Arctic meltwater" };
            water.SetColor("_ShallowColor", new Color(.38f, .68f, .82f));
            water.SetColor("_MidColor", new Color(.14f, .39f, .62f));
            water.SetColor("_DeepColor", new Color(.045f, .18f, .35f));
            water.SetColor("_FoamColor", new Color(.72f, .87f, .94f, .55f));
            water.SetFloat("_MidDepth", .19f);
            water.SetFloat("_DeepDepth", .62f);
            water.SetFloat("_RippleStrengthA", .27f);
            water.SetFloat("_RippleStrengthB", .16f);
            water.SetFloat("_CausticsStrength", .27f);
            Save(water, "Meltwater.mat");

            var plate = Surface("Brushed galvanized plate", new Color(.56f, .67f, .73f), .46f);
            Save(Texture("DeckGrain", true), "DeckGrain.asset");
            plate.SetTexture("_BaseMap", Load<Texture2D>("DeckGrain.asset"));
            Save(plate, "Galvanized.mat");
            Save(Surface("Dark coated steel", new Color(.17f, .23f, .28f), .58f), "Steel.mat");
            Save(Surface("Winter safety paint", new Color(.86f, .43f, .11f)), "SafetyPaint.mat");
            Save(Surface("Windblown frost", new Color(.88f, .95f, .99f)), "Frost.mat");
            var flakes = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Snow flurries" };
            flakes.SetColor("_BaseColor", new Color(.86f, .94f, 1f));
            Save(flakes, "Flakes.mat");

            for (int i = 0; i < 4; i++)
            {
                Save(RockMesh(i, 3.0f + i * .45f), "Pillar" + i + ".asset");
                Save(RockMesh(i, 1.13f + i * .22f), "Broken" + i + ".asset");
            }
            Save(RockMesh(4, .62f, .43f), "Fragment.asset");
            foreach (var state in BridgeStages) BuildBridge(state);
            AssetDatabase.SaveAssets();
        }

        internal static void Ensure()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Z_bridge_arctic_chunks.prefab")
                || !AssetDatabase.LoadAssetAtPath<Material>(Folder + "Boulder.mat")) Generate();
        }

        static Material Surface(string name, Color color, float metal = 0)
        {
            var material = new Material(Shader.Find("OpenZ/TeamLit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .16f + .38f * metal);
            material.SetFloat("_Metallic", metal);
            return material;
        }

        static void Save<T>(T fresh, string file) where T : Object
        {
            var target = Folder + file;
            var existing = AssetDatabase.LoadAssetAtPath<T>(target);
            if (!existing) AssetDatabase.CreateAsset(fresh, target);
            else
            {
                EditorUtility.CopySerialized(fresh, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(fresh);
            }
        }

        static Texture2D Texture(string name, bool slate)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, !slate)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float low = Periodic(u, v, 7), grit = Periodic(u, v, 32);
                if (name == "DeckGrain")
                {
                    float brushed = Periodic(u, v, 33);
                    float wear = Periodic(u, v, 4);
                    float a = .71f + brushed * .15f + wear * .12f;
                    pixels[y * size + x] = new Color(a, a, a, 1f);
                }
                else if (slate)
                {
                    float band = .5f + .5f * Mathf.Sin((v * 12 + low * .48f) * Mathf.PI * 2);
                    float a = .46f + band * .27f + low * .18f + grit * .10f;
                    pixels[y * size + x] = new Color(a, a * 1.02f, a * 1.04f, 1);
                }
                else pixels[y * size + x] = new Color(.28f + grit * .43f, .32f + low * .35f,
                    .35f + grit * .18f + low * .18f, Mathf.Max(0, grit - .72f) * 1.6f);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static float Periodic(float u, float v, float scale)
        {
            float N(float a, float b) => Mathf.PerlinNoise(91 + a * scale, 31 + b * scale);
            return Mathf.Lerp(Mathf.Lerp(N(u, v), N(u - 1, v), u),
                Mathf.Lerp(N(u, v - 1), N(u - 1, v - 1), u), v);
        }

        static Mesh RockMesh(int seed, float height, float radius = .95f)
        {
            const int sides = 7, rings = 4;
            var vertices = new Vector3[sides * rings + 1];
            var fractions = new[] { 0f, .37f, .77f, 1f };
            var widths = new[] { .75f, 1f, .82f, .56f };
            var rng = new System.Random(103 + seed * 17);
            for (int ring = 0; ring < rings; ring++)
            for (int i = 0; i < sides; i++)
            {
                float angle = (i + .17f * (ring % 2)) * Mathf.PI * 2 / sides;
                float scatter = (float)rng.NextDouble() * .24f + .80f;
                float r = widths[ring] * radius * scatter;
                vertices[ring * sides + i] = new Vector3(Mathf.Sin(angle) * r + ring * .057f,
                    fractions[ring] * height * (ring == 0 ? 1 : scatter), Mathf.Cos(angle) * r);
            }
            vertices[vertices.Length - 1] = Vector3.up * height * .98f;
            var triangles = new System.Collections.Generic.List<int>();
            for (int ring = 0; ring < rings - 1; ring++)
            for (int i = 0; i < sides; i++)
            {
                int a = ring * sides + i, b = ring * sides + (i + 1) % sides;
                triangles.AddRange(new[] { a, b, a + sides, b, b + sides, a + sides });
            }
            for (int i = 0; i < sides; i++)
                triangles.AddRange(new[] { (rings - 1) * sides + i, (rings - 1) * sides + (i + 1) % sides, vertices.Length - 1 });
            var mesh = new Mesh { name = "Fractured slate " + seed, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static int DressRocks(Transform objects)
        {
            var slate = Mat("Boulder");
            var pillars = Enumerable.Range(0, 4).Select(i => Load<Mesh>("Pillar" + i + ".asset")).ToArray();
            var broken = Enumerable.Range(0, 4).Select(i => Load<Mesh>("Broken" + i + ".asset")).ToArray();
            var fragments = new[] { Load<Mesh>("Fragment.asset") };
            int count = 0;
            foreach (Transform t in objects)
            {
                if (!t.name.StartsWith("rock_", StringComparison.Ordinal)) continue;
                int hash = Mathf.Abs((int)t.position.x * 73 ^ (int)t.position.z * 143);
                int variant = hash % pillars.Length;
                t.rotation = Quaternion.Euler(0, hash % 360, 0);
                var stages = t.gameObject.AddComponent<RockPillar>();
                stages.pillar = Part(t, "Pillar", pillars[variant], slate, true);
                stages.damaged = Part(t, "Damaged", broken[variant], slate, false);
                stages.chunks = fragments;
                stages.chunkMaterial = slate;
                count++;
            }
            return count;
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material material, bool active)
        {
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(parent, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            child.GetComponent<MeshRenderer>().sharedMaterial = material;
            child.SetActive(active);
            return child;
        }

        static void BuildBridge(string state)
        {
            bool destroyed = state == "destroyed", damaged = state == "damaged";
            var root = new GameObject("Z_bridge_arctic_" + state);
            var plate = Mat("Galvanized");
            var steel = Mat("Steel");
            var safety = Mat("SafetyPaint");
            var frost = Mat("Frost");
            if (state == "chunks")
            {
                for (int i = 0; i < 7; i++)
                    Box(root.transform, "Twisted steel", Vector3.zero, new Vector3(.25f + i * .07f, .12f, .31f + i * .13f), Quaternion.identity, i % 3 == 0 ? safety : steel);
            }
            else
            {
                for (int i = -4; i <= 4; i++)
                {
                    if (destroyed && Mathf.Abs(i) < 3) continue;
                    float z = i * 1.8f;
                    Box(root.transform, "Ribbed deck plate", new Vector3(0, .72f, z), new Vector3(6.8f, .16f, 1.75f), Quaternion.identity, plate);
                    foreach (int side in new[] { -1, 1 })
                    {
                        float x = side * 3.65f;
                        Box(root.transform, "Closed box beam", new Vector3(x, .48f, z), new Vector3(.30f, .52f, 1.8f), Quaternion.identity, steel);
                        Box(root.transform, "Rail upright", new Vector3(x, 1.22f, z - .77f), new Vector3(.11f, .9f, .11f), Quaternion.identity, safety);
                        if (!damaged || i != 0)
                            Box(root.transform, "Safety rail", new Vector3(x, 1.65f, z), new Vector3(.1f, .085f, 1.8f), Quaternion.identity, steel);
                        Box(root.transform, "Windward frost", new Vector3(side * 3.17f, .812f, z + .17f), new Vector3(.41f, .017f, .64f + .12f * ((i + 4) % 3)), Quaternion.identity, frost);
                    }
                    if (i % 2 == 0)
                        Box(root.transform, "Seam lip", new Vector3(0, .812f, z + .81f), new Vector3(5.6f, .022f, .08f), Quaternion.identity, steel);
                }
                foreach (int end in new[] { -1, 1 })
                {
                    var pitch = Quaternion.Euler(end * 22, 0, 0);
                    Box(root.transform, "Approach ramp", new Vector3(0, .39f, end * 8.1f), new Vector3(6.48f, .16f, 1.9f), pitch, steel);
                    Box(root.transform, "Bank abutment", new Vector3(0, -.09f, end * 7.15f),
                        new Vector3(6.95f, 1.32f, 1.03f), Quaternion.identity, Mat("Slate"));
                    if (destroyed)
                        foreach (int side in new[] { -1, 1 })
                            Box(root.transform, "Bent girder end", new Vector3(side * 2.83f, .28f, end * 4.08f),
                                new Vector3(.2f, .31f, 1.25f), Quaternion.Euler(end * 22, side * 14, 0), steel);
                }
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + root.name + ".prefab");
            Object.DestroyImmediate(root);
            if (!prefab) throw new IOException("Could not save Arctic " + state + " bridge prefab");
        }

        static void Box(Transform parent, string name, Vector3 position, Vector3 size, Quaternion rotation, Material material)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(bar.GetComponent<BoxCollider>());
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.SetLocalPositionAndRotation(position, rotation);
            bar.transform.localScale = size;
            bar.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        internal static void Snowfall(Transform parent, int width, int height)
        {
            var go = new GameObject("Windblown flurries");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(width, 27, -height);
            var snow = go.AddComponent<ParticleSystem>();
            var main = snow.main;
            main.duration = 18;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8, 15);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .13f);
            main.maxParticles = 2200;
            var shape = snow.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2 * width + 12, 12, 2 * height + 12);
            var emission = snow.emission;
            emission.rateOverTime = 135;
            var velocity = snow.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(.5f, 2.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(-2.3f, -.87f);
            velocity.z = new ParticleSystem.MinMaxCurve(.2f, .8f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Mat("Flakes");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
