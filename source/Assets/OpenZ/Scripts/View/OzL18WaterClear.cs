// OpenZ (GPL-3.0)
// Level 18 only: publishes the static pieces that stand in the water (the two stumps at the map edge, the half-sunk barge, the seven containers)
// as their footprint at the waterline to the water shader (_OZClearBoxA / _OZClearBoxB / _OZClearCount, Shaders/OpenZWaterContact.hlsl
// OzWaterClearBand): M_Water_L18 (_ObjectClearBand > 0) draws a soft clearer band round each of them. Wading robots, wrecks and pieces are
// already published by OpenZWading / OzWaterLanding (water_depth_soft) and get the same band from those lists. Every other level: nothing
// (the count stays 0). View only: reads the scene, never the rules.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public sealed class OzL18WaterClear : MonoBehaviour
    {
        /// <summary>On / off (off = count 0), the water surface height of level 18 (m), seconds between two scans (a destroyed container leaves).</summary>
        public static bool On = true; public static float WaterY = -0.8f, Rescan = 1.0f;
        /// <summary>The scene roots scanned under Environment (each child = one piece): the stumps, the dressing's pieces, the containers.</summary>
        public static readonly string[] Roots = { "L18_RiverStumps", "L18_Dressing/Pieces", "L18_Containers" };
        public const int Max = 24;
        /// <summary>Play checks: pieces published at the last scan, their names.</summary>
        public static int Count; public static string Names = "";

        static readonly int AId = Shader.PropertyToID("_OZClearBoxA"), BId = Shader.PropertyToID("_OZClearBoxB"), NId = Shader.PropertyToID("_OZClearCount");
        static readonly Vector4[] a = new Vector4[Max], b = new Vector4[Max];
        static OzL18WaterClear me;
        float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            Shader.SetGlobalFloat(NId, 0f);
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
            Loaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void Loaded(Scene s, LoadSceneMode mode)
        {
            if (!s.IsValid() || !s.name.StartsWith(OzCity.BridgeGameScene)) return;
            if (me) return;
            var go = new GameObject("OzL18WaterClear") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(go, s);
            me = go.AddComponent<OzL18WaterClear>();
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Mathf.Max(0.2f, Rescan);
            Scan();
        }

        void OnDestroy()
        {
            if (me == this) me = null;
            Count = 0; Shader.SetGlobalFloat(NId, 0f);
        }

        void Scan()
        {
            int n = 0; var names = new List<string>();
            var env = GameObject.Find("Environment");
            if (On && env)
                foreach (var rn in Roots)
                {
                    var root = env.transform.Find(rn); if (!root) continue;
                    for (int i = 0; i < root.childCount && n < Max; i++)
                    {
                        var host = root.GetChild(i);
                        if (!host.gameObject.activeInHierarchy) continue;
                        if (!Footprint(host, out var c, out var axis, out var half)) continue;
                        a[n] = new Vector4(c.x, c.z, half.x, half.y);
                        b[n] = new Vector4(axis.x, axis.y, WaterY, 0f);
                        names.Add(host.name); n++;
                    }
                }
            Count = n; Names = string.Join(", ", names);
            if (n > 0) { Shader.SetGlobalVectorArray(AId, a); Shader.SetGlobalVectorArray(BId, b); }
            Shader.SetGlobalFloat(NId, n);
        }

        /// <summary>A piece's footprint where it meets the water: the box of its meshes in its own axes (x along its local X, y along its local Z, in
        /// metres), its world centre, its local X in world x / z. False = it does not reach through the water surface.</summary>
        static bool Footprint(Transform host, out Vector3 centre, out Vector2 axis, out Vector2 half)
        {
            centre = Vector3.zero; axis = Vector2.right; half = Vector2.zero;
            Vector3 lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo; float yLo = float.MaxValue, yHi = float.MinValue; bool any = false;
            foreach (var mf in host.GetComponentsInChildren<MeshFilter>(false))
            {
                var m = mf.sharedMesh; var r = mf.GetComponent<Renderer>();
                if (!m || !r || !r.enabled) continue;
                var bb = m.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var cl = bb.center + Vector3.Scale(bb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var w = mf.transform.TransformPoint(cl);
                    var l = host.InverseTransformPoint(w);
                    lo = Vector3.Min(lo, l); hi = Vector3.Max(hi, l); yLo = Mathf.Min(yLo, w.y); yHi = Mathf.Max(yHi, w.y); any = true;
                }
            }
            if (!any || yLo > WaterY + 0.05f || yHi < WaterY - 0.05f) return false;
            var s = host.lossyScale;
            half = new Vector2((hi.x - lo.x) * 0.5f * Mathf.Abs(s.x), (hi.z - lo.z) * 0.5f * Mathf.Abs(s.z));
            centre = host.TransformPoint((lo + hi) * 0.5f);
            var ax = host.right; ax.y = 0f;
            if (ax.sqrMagnitude < 1e-6f) ax = Vector3.right;
            ax.Normalize(); axis = new Vector2(ax.x, ax.z);
            return half.x > 0.05f && half.y > 0.05f;
        }
    }
}
