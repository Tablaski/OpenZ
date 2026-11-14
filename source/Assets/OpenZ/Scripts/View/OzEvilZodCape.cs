using System;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// in the world, so it sways with his walk, kicks with his shot and streams out behind the power dash with no animation of its own. The mesh is in the prefab (menu OpenZ/V1.3/EvilZod/1 Build), so the
    /// paint, the drop preview, boarding clones and the fall all carry it; a body the cloak cannot find bones on keeps the rest shape. Data: Resources/OpenZ/HeroSquad/Legs/evilzod_cape.json:
    /// the rest shape in metres on the rig, in his chest's own frame (x his right, y up the spine, z his front), so nothing here depends on how the bones lie in Unity.
    /// Order 83: after his upper body (81) and its effects (82). Nothing of the rules is read: no sim number, no sim random.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(83)]
    public sealed class OzEvilZodCape : MonoBehaviour
    {
        [Serializable] public sealed class CapeJson { public int rows, cols; public float[] rest, pull; public float chestR, hipsR, thighR, armR; }

        public const string DataPath = OzHeroLegs.DataDir + "evilzod_cape", ObjectName = "EvilZod_Cape";
        const string M = "mixamorig";
        /// <summary>Heavy cloth: a slow fall (m/s2), the speed kept per step, steps a second, the most steps in a frame, how far a frame's jump is a teleport (m).</summary>
        public static float Gravity = 11f, Keep = 0.975f, StepsPerSecond = 90f, TeleportM = 6f, GroundLift = 0.08f;
        public static int MaxSteps = 4, Passes = 2;

        static CapeJson data; static bool looked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Forget(); }
        public static void Forget() { data = null; looked = false; }

        public static CapeJson Data
        {
            get
            {
                if (data == null && !looked)
                {
                    looked = true;
                    var ta = Resources.Load<TextAsset>(DataPath);
                    if (ta) { try { data = JsonUtility.FromJson<CapeJson>(ta.text); } catch (Exception e) { Debug.LogWarning("[OpenZ EvilZod] " + DataPath + ": " + e.Message); } }
                    else Debug.LogWarning("[OpenZ EvilZod] Resources/" + DataPath + " is missing");
                    if (data != null && (data.rest == null || data.rest.Length != data.rows * data.cols * 3 || data.pull == null || data.pull.Length != data.rows)) { Debug.LogWarning("[OpenZ EvilZod] the cloak's data does not fit"); data = null; }
                }
                return data;
            }
        }

        // ---------------------------------------------------------------------------- the bones
        sealed class Bones
        {
            public Transform animRoot, hips, spine1, spine2, neck, armL, armR, foreL, foreR, upL, kneeL, upR, kneeR;
            public bool Ok => hips && spine1 && spine2 && neck && armL && armR && foreL && foreR && upL && kneeL && upR && kneeR;
            public static Bones Find(Transform root)
            {
                var all = root.GetComponentsInChildren<Transform>(true);
                Transform F(string n) { foreach (var t in all) if (t.name == n) return t; return null; }
                return new Bones
                {
                    animRoot = root, hips = F(M + "Hips"), spine1 = F(M + "Spine1"), spine2 = F(M + "Spine2"), neck = F(M + "Neck"), armL = F(M + "LeftArm"), armR = F(M + "RightArm"), foreL = F(M + "LeftForeArm"), foreR = F(M + "RightForeArm"),
                    upL = F(M + "LeftUpLeg"), kneeL = F(M + "LeftLeg"), upR = F(M + "RightUpLeg"), kneeR = F(M + "RightLeg"),
                };
            }
            /// <summary>His chest's frame now: the shoulders give x, the spine y; a left-handed frame, so x cross y is his front.</summary>
            public void Frame(out Vector3 o, out Vector3 x, out Vector3 y, out Vector3 z, out float k)
            {
                o = spine2.position; k = Mathf.Max(0.01f, animRoot.lossyScale.y);
                x = (armR.position - armL.position).normalized;
                y = Vector3.ProjectOnPlane(neck.position - spine1.position, x).normalized;
                z = Vector3.Cross(x, y);
            }
        }

        static Vector3 RestAt(CapeJson d, int i, Vector3 o, Vector3 x, Vector3 y, Vector3 z, float k) => o + (x * d.rest[i * 3] + y * d.rest[i * 3 + 1] + z * d.rest[i * 3 + 2]) * k;

        /// <summary>Menu 1: the mesh, the rest shape on the prefab's bind pose: both sides of the cloth (it is one layer, and TeamLit draws front faces only), the outside on the left half of its map, the lining on the right.</summary>
        public static Mesh BakeMesh(Transform capeObject, Transform animRoot)
        {
            var d = Data; if (d == null) return null;
            var b = Bones.Find(animRoot); if (!b.Ok) return null;
            b.Frame(out var o, out var x, out var y, out var z, out float k);
            int n = d.rows * d.cols; var v = new Vector3[n * 2]; var uv = new Vector2[n * 2]; var nr = new Vector3[n * 2]; var w2l = capeObject.worldToLocalMatrix;
            for (int r = 0; r < d.rows; r++)
                for (int c = 0; c < d.cols; c++)
                {
                    int i = r * d.cols + c; v[i] = v[i + n] = w2l.MultiplyPoint3x4(RestAt(d, i, o, x, y, z, k));
                    float s = r / (float)(d.rows - 1), t = c / (float)(d.cols - 1);
                    uv[i] = new Vector2(t * 0.5f, s); uv[i + n] = new Vector2(0.5f + t * 0.5f, s);
                    nr[i] = w2l.MultiplyVector(-z).normalized; nr[i + n] = -nr[i];
                }
            var tri = new int[(d.rows - 1) * (d.cols - 1) * 12]; int q = 0;
            for (int r = 0; r + 1 < d.rows; r++)
                for (int c = 0; c + 1 < d.cols; c++)
                {
                    int a = r * d.cols + c, e = a + 1, f = a + d.cols + 1, g = a + d.cols;       // seen from behind him: left to right, top to bottom, clockwise
                    tri[q++] = a; tri[q++] = e; tri[q++] = f; tri[q++] = a; tri[q++] = f; tri[q++] = g;
                    tri[q++] = a + n; tri[q++] = f + n; tri[q++] = e + n; tri[q++] = a + n; tri[q++] = g + n; tri[q++] = f + n;
                }
            var m = new Mesh { name = "Hero_EvilZod_Cape" }; m.vertices = v; m.normals = nr; m.uv = uv; m.triangles = tri; m.RecalculateBounds();
            return m;
        }

        // ---------------------------------------------------------------------------- the cloth
        Bones bones; CapeJson d; MeshFilter filter; Renderer rend; Mesh mesh; bool ready, fresh = true;
        /// <summary>
        /// The Unit editor's drop preview (View/OzUePlace.cs MakeModel) jumps to the pointer every frame, so the cloth whipped about. A held
        /// cloak keeps its rest shape on his body (no cloth steps); every EvilZod on the field keeps the full cloth.</summary>
        [NonSerialized] public bool Held;
        Vector3[] p, prev, restW, verts, normals, pinWas; float[] restDown, restSide; Vector3 lastO; int n;

        void Start()
        {
            d = Data; filter = GetComponent<MeshFilter>(); rend = GetComponent<Renderer>();
            var an = GetComponentInParent<Animator>();
            if (d == null || !filter || !filter.sharedMesh || !an) return;
            bones = Bones.Find(an.transform); n = d.rows * d.cols;
            if (!bones.Ok || filter.sharedMesh.vertexCount != n * 2) return;
            mesh = Instantiate(filter.sharedMesh); mesh.name = filter.sharedMesh.name + " (live)"; mesh.MarkDynamic(); filter.sharedMesh = mesh;
            p = new Vector3[n]; prev = new Vector3[n]; restW = new Vector3[n]; verts = new Vector3[n * 2]; normals = new Vector3[n * 2]; pinWas = new Vector3[d.cols];
            restDown = new float[n]; restSide = new float[n];
            for (int r = 0; r < d.rows; r++)
                for (int c = 0; c < d.cols; c++)
                {
                    int i = r * d.cols + c;
                    if (r > 0) restDown[i] = Dist(i, i - d.cols);
                    if (c > 0) restSide[i] = Dist(i, i - 1);
                }
            ready = true;
        }

        float Dist(int a, int b) => new Vector3(d.rest[a * 3] - d.rest[b * 3], d.rest[a * 3 + 1] - d.rest[b * 3 + 1], d.rest[a * 3 + 2] - d.rest[b * 3 + 2]).magnitude;

        void LateUpdate()
        {
            if (!ready) return;
            if (rend && !rend.isVisible) { fresh = true; return; }                                // out of sight costs nothing, and it starts at rest
            bones.Frame(out var o, out var x, out var y, out var z, out float k);
            for (int i = 0; i < n; i++) restW[i] = RestAt(d, i, o, x, y, z, k);
            if (Held) { for (int i = 0; i < n; i++) p[i] = prev[i] = restW[i]; fresh = true; Write(); return; }   // the drop preview: still, on his body
            float gs = ZGame.I ? Mathf.Max(0.25f, ZGame.I.gameSpeed) : 1f, dt = Mathf.Min(Time.deltaTime * gs, 0.05f);
            if (fresh || (o - lastO).sqrMagnitude > TeleportM * TeleportM * gs * gs)
            {
                for (int i = 0; i < n; i++) p[i] = prev[i] = restW[i];
                for (int c = 0; c < d.cols; c++) pinWas[c] = restW[c];
                fresh = false;
            }
            lastO = o;
            if (dt > 0f)
            {
                int steps = Mathf.Clamp(Mathf.CeilToInt(dt * StepsPerSecond), 1, MaxSteps); float h = dt / steps;
                for (int s = 1; s <= steps; s++) Step(h, s / (float)steps, k);
                for (int c = 0; c < d.cols; c++) pinWas[c] = restW[c];
            }
            Write();
        }

        void Step(float h, float at, float k)
        {
            Vector3 fall = Vector3.down * (Gravity * h * h); float rate = h * 60f; int R = d.rows, C = d.cols;
            for (int c = 0; c < C; c++) p[c] = prev[c] = Vector3.Lerp(pinWas[c], restW[c], at);
            for (int r = 1; r < R; r++)
            {
                float pull = 1f - Mathf.Pow(1f - d.pull[r], rate);
                for (int c = 0; c < C; c++)
                {
                    int i = r * C + c; var q = p[i];
                    p[i] += (q - prev[i]) * Keep + fall; prev[i] = q;
                    p[i] += (restW[i] - p[i]) * pull;
                }
            }
            for (int pass = 0; pass < Passes; pass++)
            {
                for (int r = 1; r < R; r++)                                                     // each row hangs from the row above: top down, so the fastest dash can not stretch it
                {
                    for (int c = 0; c < C; c++)
                    {
                        int i = r * C + c, u = i - C; float max = restDown[i] * k; var e = p[i] - p[u]; float l = e.magnitude;
                        if (l > max && l > 1e-5f) p[i] = p[u] + e * (max / l);
                    }
                    for (int c = 1; c < C; c++)
                    {
                        int i = r * C + c, w = i - 1; float want = restSide[i] * k; var e = p[i] - p[w]; float l = e.magnitude;
                        if (l < 1e-5f) continue;
                        var fix = e * (0.5f * (l - want) / l); p[i] -= fix; p[w] += fix;
                    }
                }
                for (int i = C; i < n; i++)
                {
                    var q = p[i];
                    Push(ref q, bones.spine1.position, bones.neck.position, d.chestR * k);
                    Push(ref q, bones.hips.position, bones.spine1.position, d.hipsR * k);
                    Push(ref q, bones.upL.position, bones.kneeL.position, d.thighR * k);
                    Push(ref q, bones.upR.position, bones.kneeR.position, d.thighR * k);
                    Push(ref q, bones.armL.position, bones.foreL.position, d.armR * k);          // the right elbow sits back in his holds
                    Push(ref q, bones.armR.position, bones.foreR.position, d.armR * k);
                    p[i] = q;
                }
            }
            if (ZGame.I)
                for (int i = C; i < n; i++)
                {
                    float g = ZGame.I.GroundY(p[i].x, p[i].z) + GroundLift;
                    if (p[i].y < g) p[i].y = g;
                }
        }

        static void Push(ref Vector3 q, Vector3 a, Vector3 b, float r)
        {
            Vector3 ab = b - a; float u = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            Vector3 c = a + ab * u, e = q - c; float l = e.sqrMagnitude;
            if (l < r * r && l > 1e-8f) q = c + e * (r / Mathf.Sqrt(l));
        }

        void Write()
        {
            var w = transform.worldToLocalMatrix; int R = d.rows, C = d.cols;
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                {
                    int i = r * C + c;
                    Vector3 a = p[r * C + Mathf.Min(c + 1, C - 1)] - p[r * C + Mathf.Max(c - 1, 0)], b = p[Mathf.Min(r + 1, R - 1) * C + c] - p[Mathf.Max(r - 1, 0) * C + c];
                    var nm = w.MultiplyVector(Vector3.Cross(a, b)).normalized;
                    verts[i] = verts[i + n] = w.MultiplyPoint3x4(p[i]); normals[i] = nm; normals[i + n] = -nm;
                }
            mesh.vertices = verts; mesh.normals = normals; mesh.RecalculateBounds();
        }

        void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
