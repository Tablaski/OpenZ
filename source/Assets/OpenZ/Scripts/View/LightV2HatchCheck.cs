#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Editor check (Play) for the light tank v2's hatch driver: every frame, every vertex of every driver figure and of his weapon (as drawn:
    /// a weapon not drawn does not count) is put into the turret's own space and tested against the turret's top surface outside the hatch
    /// opening (a 1 cm height map of the model's turret, hatch plate and roof, made from the model file, next to the check's report). A vertex
    /// under that surface = a point inside the roof. WEAPON: any such point. BODY seen through the top: a point at most 10 cm under the surface
    /// and more than 5 cm outside the opening's edge (his hands deep in the turret under the plate and his forearms going down at the opening's
    /// edge are counted apart, as information). Counts the frames by state (up, jolt, rising / sinking, down), the frames with a point inside,
    /// the deepest point and the smallest weapon clearance while he is up, the frames with his arms in. Started and reported by the editor menu
    /// (Editor/OzLightV2HatchMenu.cs).
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class LightV2HatchCheck : MonoBehaviour
    {
        public static LightV2HatchCheck I { get; private set; }
        public static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/k84e_light_v2_fixes/proof"));
        float x0, z0, cell, wellR; int nx, nz; float[] top; bool ok; string why = "";
        public int frames, upFrames, joltFrames, moveFrames, downFrames, clipFrames, clipUp, clipJolt, clipMove, gunHiddenFrames, gunShownFrames, bodyDeepFrames, bodyEdgeFrames, armsInFrames;
        public float deepest, minClearUp = float.MaxValue; public string deepestAt = "";
        readonly List<BoardingView.LightV2Crew> crews = new List<BoardingView.LightV2Crew>();
        readonly Dictionary<Mesh, Vector3[]> meshVerts = new Dictionary<Mesh, Vector3[]>();
        Mesh baked;
        readonly HashSet<string> types = new HashSet<string>();

        public static void Begin()
        {
            if (I) return;
            var go = new GameObject("LightV2HatchCheck"); DontDestroyOnLoad(go); I = go.AddComponent<LightV2HatchCheck>();
        }

        void Awake()
        {
            string f = Path.Combine(Dir, "turret_top_k84e.json");
            try
            {
                var d = JsonUtility.FromJson<Map>(File.ReadAllText(f));
                x0 = d.x0; z0 = d.z0; cell = d.cell; nx = d.nx; nz = d.nz; wellR = d.wellR; top = d.top; ok = top != null && top.Length == nx * nz;
                if (!ok) why = "bad map " + f;
            }
            catch (System.Exception e) { why = "no map " + f + ": " + e.Message; }
            baked = new Mesh();
            Debug.Log("[LightV2HatchCheck] started " + (ok ? $"map {nx} x {nz} cells of {cell * 100f:0} cm, opening r {wellR:0.000}" : why));
        }

        [System.Serializable] class Map { public float x0 = 0f, z0 = 0f, cell = 0.01f, wellR = 0f; public int nx = 0, nz = 0; public float[] top = null; }   // filled by JsonUtility

        float Top(float x, float z)
        {
            int i = Mathf.RoundToInt((x - x0) / cell), j = Mathf.RoundToInt((z - z0) / cell);
            float best = -99f;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int a = i + di, b = j + dj;
                    if (a < 0 || b < 0 || a >= nx || b >= nz) continue;
                    best = Mathf.Max(best, top[b * nx + a]);
                }
            return best;
        }

        Vector3[] Verts(Mesh m)
        {
            if (!meshVerts.TryGetValue(m, out var v))
            {
                if (m.isReadable) v = m.vertices;
                else { var b = m.bounds; v = new Vector3[8]; for (int k = 0; k < 8; k++) v[k] = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z); }
                meshVerts[m] = v;
            }
            return v;
        }

        void LateUpdate()
        {
            if (!ok) return;
            BoardingView.LightV2Crews(crews);
            foreach (var c in crews)
            {
                if (!c.turret || !c.frame) continue;
                frames++;
                if (!c.crew.activeInHierarchy) { downFrames++; continue; }
                bool up = c.pop >= 0.999f, jolt = up && c.flinch > 0f;
                if (!up) moveFrames++; else if (jolt) joltFrames++; else upFrames++;
                if (c.gunHidden) gunHiddenFrames++; else gunShownFrames++;
                if (c.arms > 0f) armsInFrames++;
                var rb = c.crew.GetComponent<RobotV3>(); if (rb) types.Add(rb.type);
                var hc = c.turret.InverseTransformPoint(c.frame.position);
                float unit = Mathf.Abs(c.turret.lossyScale.y);
                float worst = 0f, clear = float.MaxValue; Vector3 at = Vector3.zero; bool deep = false, edge = false; string what = "";
                void Test(Vector3 world, bool gun)
                {
                    var p = c.turret.InverseTransformPoint(world);
                    float rr = new Vector2(p.x - hc.x, p.z - hc.z).magnitude;
                    if (rr <= wellR) return;
                    float h = Top(p.x, p.z);
                    if (h < -50f) return;
                    float d = (h - p.y) * unit;
                    if (gun) clear = Mathf.Min(clear, -d);
                    if (d <= 0.002f) return;
                    if (!gun && d > 0.10f) { deep = true; return; }
                    if (!gun && (rr - wellR) * unit < 0.05f) { edge = true; return; }                          // at the opening's edge (a shoulder on the rim)
                    if (d > worst) { worst = d; at = p; what = gun ? "weapon" : "body"; }
                }
                foreach (var sk in c.crew.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!sk.enabled || !sk.sharedMesh) continue;
                    sk.BakeMesh(baked, true);
                    var m = Matrix4x4.TRS(sk.transform.position, sk.transform.rotation, Vector3.one);
                    foreach (var v in baked.vertices) Test(m.MultiplyPoint3x4(v), false);
                }
                if (c.weapon)
                    foreach (var mf in c.weapon.GetComponentsInChildren<MeshFilter>())
                    {
                        var mr = mf.GetComponent<Renderer>(); if (!mr || !mr.enabled || !mf.sharedMesh) continue;
                        var m = mf.transform.localToWorldMatrix;
                        foreach (var v in Verts(mf.sharedMesh)) Test(m.MultiplyPoint3x4(v), true);
                    }
                if (up && clear < float.MaxValue) minClearUp = Mathf.Min(minClearUp, clear);
                if (deep) bodyDeepFrames++;
                if (edge) bodyEdgeFrames++;
                if (worst > 0.002f)
                {
                    clipFrames++; if (!up) clipMove++; else if (jolt) clipJolt++; else clipUp++;
                    if (worst > deepest) { deepest = worst; deepestAt = $"{what}, {(up ? jolt ? "jolt" : "up" : "rising/sinking")} at turret ({at.x:0.00}, {at.y:0.00}, {at.z:0.00}) pop {c.pop:0.00}"; }
                }
            }
            if (frames > 0 && Time.frameCount % 300 == 0) Debug.Log("[LightV2HatchCheck] " + Line());
        }

        public string Line() =>
            $"driver frames {frames} (up {upFrames}, jolt {joltFrames}, rising / sinking {moveFrames}, down {downFrames}); frames with a point inside the roof {clipFrames} " +
            $"(up {clipUp}, jolt {clipJolt}, rising / sinking {clipMove}); deepest {deepest * 100f:0.0} cm {deepestAt}; smallest weapon clearance up {(minClearUp < float.MaxValue ? (minClearUp * 100f).ToString("0.0") + " cm" : "-")}; " +
            $"weapon drawn {gunShownFrames} / not drawn {gunHiddenFrames} frames; lifted {BoardingView.LightV2LiftFrames} frames (max {BoardingView.LightV2LiftMaxSeen * 100f:0.0} cm); " +
            $"arms in {armsInFrames} frames, sink held for his arms {BoardingView.LightV2SinkHeldFrames} frames; " +
            $"info: frames with a body point under the plate within 5 cm of the opening's edge {bodyEdgeFrames}, deeper than 10 cm in the turret {bodyDeepFrames}; types {string.Join(",", types)}";

        public string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("light tank v2 hatch driver clip check (" + System.DateTime.Now.ToString("HH:mm:ss") + ")");
            sb.AppendLine(Line());
            sb.AppendLine((clipFrames == 0 && upFrames > 0 ? "PASS" : upFrames == 0 ? "NOT RUN (no driver up)" : "FAIL") + ": 0 frames with a point of his weapon, or of his body seen through the top, inside the roof (frames with the weapon drawn: " + gunShownFrames + ")");
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, "driver_clip_check.txt"), sb.ToString());
            return sb.ToString();
        }

        void OnDestroy() { if (I == this) I = null; if (baked) Destroy(baked); }
    }
}
#endif
