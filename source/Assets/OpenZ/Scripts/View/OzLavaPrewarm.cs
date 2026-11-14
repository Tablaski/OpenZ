// OpenZ (GPL-3.0) - version 1.3, level 7: fix 24: so the first eruption, dome or steam burst costs no hitch. On the OpenZNukePrewarm pattern, on a map with lava only, at
// most 4 ms a frame through OzStartAhead: PrepareMethod on every lava type (they stay out of the shared HotTypes list), each new material drawn once at sub-pixel size,
// one particle of each lava channel, one silent eruption through every state (OzVolcano.WarmPass). Bombs, chunks and flame meshes are made up front by OzVolcano.Boot.
// The types are named as text: a kit that is not in yet is skipped.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    public sealed class OzLavaPrewarm : MonoBehaviour
    {
        public static float FrameBudgetMs = 4f;
        public static bool Done { get; private set; }
        public static int Jitted, Failed, MaterialsDrawn;

        static readonly string[] Types = { "OzLava", "OzLavaPops", "OzLavaHeat", "OzLavaDomes", "OzLavaAir", "OzLavaWade", "OzLavaWake", "OzLavaPrints", "OzLavaMelt", "OzLavaMeltPlan", "OzVolcano", "OzVolcanoPlan", "OzVolcanoSurges", "OzVolcanoArc", "OzLavaIds", "OzLavaRampTable" };
        static readonly string[] Materials = { "OpenZ/Lava/M_L07_Lava", "OpenZ/Lava/M_L07_Veins", "OpenZ/Lava/M_L07_Dome", "OpenZ/Lava/M_L07_Ring", "OpenZ/Lava/M_L07_Print", OzLavaMelt.MaterialRes, OzVolcano.FlowRes, OzVolcano.BombRes, "OpenZ/FX/M_InfernoFlame" };

        void Start() { Done = false; Jitted = Failed = MaterialsDrawn = 0; OzStartAhead.Run(this, Warm(), "the lava's prewarm"); }

        IEnumerator Warm()
        {
            for (int i = 0; i < 6; i++) yield return null;                                                       // the level's own Start work passes
            var sw = Stopwatch.StartNew(); var methods = new List<MethodInfo>(); var asm = typeof(OzLava).Assembly;
            foreach (var n in Types) Collect(asm.GetType("OpenZ." + n), methods, 0);
            foreach (var m in methods)
            {
                try { RuntimeHelpers.PrepareMethod(m.MethodHandle); m.MethodHandle.GetFunctionPointer(); Jitted++; } catch (Exception) { Failed++; }   // Mono's PrepareMethod is a no-op; GetFunctionPointer really JITs (OpenZNukePrewarm, H11)
                if (sw.Elapsed.TotalMilliseconds >= FrameBudgetMs) { yield return null; sw.Restart(); }
            }
            yield return null;
            yield return DrawWarm();                                                                             // 1.4: a routine of its own, so OzStartAhead hands its frames back (its draws must be drawn in a frame); the same work in the same order
        }

        /// <summary>Each material drawn once at sub-pixel size in two frames, then one silent eruption (as before, moved here unchanged).</summary>
        IEnumerator DrawWarm()
        {
            var cam = Camera.main;
            if (cam)
            {
                var quad = new Mesh { name = "OZ_LavaWarmQuad", vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) }, triangles = new[] { 0, 2, 1, 0, 3, 2 }, normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back } };
                quad.SetUVs(0, new List<Vector4> { Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero }); quad.SetUVs(1, new List<Vector4> { Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero });
                var at = Matrix4x4.TRS(cam.transform.position + cam.transform.forward * (cam.nearClipPlane + 0.5f), cam.transform.rotation, Vector3.one * 1e-4f);
                var one = new[] { at };
                for (int f = 0; f < 2; f++)
                {
                    foreach (var path in Materials)
                    {
                        var m = Resources.Load<Material>(path); if (!m) continue;
                        if (m.enableInstancing) Graphics.DrawMeshInstanced(quad, 0, m, one, 1, null, ShadowCastingMode.Off, false);   // domes, rings, bombs: their real draw is instanced, another shader variant
                        else
                        {
                            var rp = new RenderParams(m) { layer = 0, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = new Bounds(at.GetColumn(3), Vector3.one) };
                            Graphics.RenderMesh(rp, quad, 0, at);
                        }
                        if (f == 0) MaterialsDrawn++;
                    }
                    yield return null;
                }
                Destroy(quad);
            }
            var v = OzVolcano.I; if (v) v.WarmPass();
            yield return null;
            Done = true;
            Debug.Log($"[OpenZ] lava prewarm: {Jitted} methods ({Failed} skipped), {MaterialsDrawn} materials drawn, {(v ? "one silent eruption" : "no volcano")}");
        }

        static void Collect(Type t, List<MethodInfo> into, int depth)
        {
#if ENABLE_IL2CPP
            return;                                                                                      // 1.3 final: an IL2CPP player is compiled ahead (nothing to JIT) and has no GetMethodBody (it threw and stopped the whole prewarm)
#else
            if (t == null || t.ContainsGenericParameters || depth > 2) return;
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var m in t.GetMethods(F))
                if (!m.IsAbstract && !m.ContainsGenericParameters && m.GetMethodBody() != null) into.Add(m);
            foreach (var nt in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) Collect(nt, into, depth + 1);
#endif
        }
    }
}
