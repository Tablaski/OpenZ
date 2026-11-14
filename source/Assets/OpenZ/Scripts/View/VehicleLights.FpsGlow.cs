// OpenZ (GPL-3.0)
// Unity chain v8 U1: at night OTHER vehicles' headlight halos beside the
// viewed tank show as big white blobs in the hidden first-person view). VehicleLights' lamp glows and haze cones in that view only:
//  - every glow mesh of another vehicle is sized for the view's camera before it is drawn: its biggest billboard's share of the screen (the
//    shader's own sizing: corner x size x the mount's scale in view space, pulled _ZPull m toward the eye) at most FpsMaxShare (1.5 %); a mount
//    whose halos are bigger is drawn with its cores and lens discs only (a cached copy of the mesh without the wide halo billboards), and one
//    whose cores are still bigger (or within FpsMinDepth m of the eye) is not drawn in that picture;
//  - a haze cone is not drawn while the eye stands in or by it (its mesh box + 0.5 m, or within FpsBeamNearM of the lamp);
//  - nothing is written to a rig, a block or a light: the choice is made each frame inside Apply, so the RTS camera draws every glow exactly
//    as before (OzFpsView.On false: the first line returns the mesh as it is); on the leave the real lights are picked again at once for the
//    RTS focus (FpsLeaveNow, called by OzFpsGlow.Left) and every glow block is sent again;
//  - order: VehicleLights' LateUpdate (80) runs before OzFpsView.PlaceCamera (31900), so the eye read here is the last frame's; every depth is
//    taken that much nearer (FpsStep: the eye's move over the last frame, at most 2 m; 0.5 m on a first frame), so a lamp the eye moves toward
//    is never drawn bigger than the limit in this frame's picture; the cones' "by the eye" distance grows by the same step;
//  - OzFpsGlow.Fixes false (the Play check's owner repro pass only): the meshes as they are, nothing held, no re-pick.
// Counters for the Play check (View/OzFpsViewPlayCheck.Glow.cs): FpsSeen / FpsFull / FpsCores / FpsHeld / FpsBeamsHeld this frame and the
// biggest drawn share FpsMaxDrawn.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public partial class VehicleLights
    {
        /// <summary>The biggest share of the screen one lamp billboard may take in the first-person view (0.015 = 1.5 %).</summary>
        public static float FpsMaxShare = 0.015f;
        /// <summary>A glow mount nearer the eye than this (m, view depth after the shader's pull) is not drawn in the view.</summary>
        public static float FpsMinDepth = 0.6f;
        /// <summary>A haze cone is not drawn while the eye is within this distance of its lamp (m) or inside its box (+ 0.5 m).</summary>
        public static float FpsBeamNearM = 3f;
        /// <summary>Mounts farther than this from the eye are drawn as they are (a 3 m halo at 30 m is under 0.3 % of the screen).</summary>
        public static float FpsFarM = 30f;

        /// <summary>Check counters for the frame FpsFrame: mounts sized, drawn whole, drawn as cores only, not drawn; cones not drawn; the
        /// biggest share of a billboard drawn (0..1) and the biggest share before the limit.</summary>
        public static int FpsFrame = -1, FpsSeen, FpsFull, FpsCores, FpsHeld, FpsBeamsHeld; public static float FpsMaxDrawn, FpsMaxRaw;
        /// <summary>Leaves seen (FpsLeaveNow) and the frame of the last one.</summary>
        public static int FpsLeaves, FpsLeaveFrame = -1;
        /// <summary>The eye's move over the last frame (m): this frame's margin for the one-frame-old eye (see the file's head).</summary>
        public static float FpsStep;
        static Vector3 fpsLastEye; static int fpsLastEyeFrame = -10;

        sealed class GlowElems { public Vector3[] pos; public float[] size; public bool[] core; public Mesh cores; }
        static readonly Dictionary<Mesh, GlowElems> fpsElems = new Dictionary<Mesh, GlowElems>();
        static readonly List<Vector3> fpsV = new List<Vector3>(), fpsN = new List<Vector3>();
        static readonly List<Vector4> fpsU0 = new List<Vector4>(), fpsU1 = new List<Vector4>();
        float fpsPull = -1f;

        static void FpsCount(Camera c)
        {
            if (FpsFrame == Time.frameCount) return;
            FpsFrame = Time.frameCount; FpsSeen = FpsFull = FpsCores = FpsHeld = FpsBeamsHeld = 0; FpsMaxDrawn = FpsMaxRaw = 0f;
            var p = c.transform.position;
            FpsStep = fpsLastEyeFrame == Time.frameCount - 1 ? Mathf.Min(2f, (p - fpsLastEye).magnitude) : 0.5f;
            fpsLastEye = p; fpsLastEyeFrame = Time.frameCount;
        }

        /// <summary>The billboards of a glow mesh (VehicleLightMeshes.GlowMesh: 4 vertices per billboard, uv0 = corner, size, mode; mode 0 = the
        /// wide halo, 1 = the lens disc, 2 = the hot core) and its cores-only copy (null when it has no core), read once per mesh.</summary>
        static GlowElems Elems(Mesh m)
        {
            if (fpsElems.TryGetValue(m, out var e)) return e;
            e = new GlowElems { pos = new Vector3[0], size = new float[0], core = new bool[0] };
            if (m.isReadable)
            {
                fpsV.Clear(); fpsN.Clear(); fpsU0.Clear(); fpsU1.Clear();
                m.GetVertices(fpsV); m.GetNormals(fpsN); m.GetUVs(0, fpsU0); m.GetUVs(1, fpsU1);
                int q = fpsV.Count / 4;
                if (fpsU0.Count == fpsV.Count && q > 0)
                {
                    e.pos = new Vector3[q]; e.size = new float[q]; e.core = new bool[q];
                    var v = new List<Vector3>(); var n = new List<Vector3>(); var u0 = new List<Vector4>(); var u1 = new List<Vector4>(); var t = new List<int>();
                    for (int i = 0; i < q; i++)
                    {
                        int k = i * 4; e.pos[i] = fpsV[k]; e.size[i] = Mathf.Abs(fpsU0[k].z); e.core[i] = fpsU0[k].w > 0.5f;
                        if (!e.core[i]) continue;
                        int o = v.Count;
                        for (int j = 0; j < 4; j++) { v.Add(fpsV[k + j]); n.Add(fpsN.Count == fpsV.Count ? fpsN[k + j] : Vector3.zero); u0.Add(fpsU0[k + j]); u1.Add(fpsU1.Count == fpsV.Count ? fpsU1[k + j] : Vector4.zero); }
                        t.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
                    }
                    if (v.Count > 0)
                    {
                        e.cores = new Mesh { name = m.name + "_fpsCores", hideFlags = HideFlags.DontSave };
                        e.cores.SetVertices(v); e.cores.SetNormals(n); e.cores.SetUVs(0, u0); e.cores.SetUVs(1, u1); e.cores.SetTriangles(t, 0);
                        e.cores.bounds = m.bounds;
                    }
                }
            }
            fpsElems[m] = e;
            return e;
        }

        /// <summary>The biggest share of the screen (0..1) of the billboards of e (cores only or all) drawn at M for camera c; 1 when one is at
        /// the eye. The shader: centre in view space, pulled toward the eye by _ZPull (half for cores), corners +- size x the mount's scale.</summary>
        float Share(GlowElems e, Matrix4x4 M, Camera c, bool coresOnly)
        {
            if (fpsPull < 0f) fpsPull = glowMat && glowMat.HasProperty("_ZPull") ? glowMat.GetFloat("_ZPull") : 0.3f;
            float scale = new Vector3(M.m00, M.m10, M.m20).magnitude, tanV = Mathf.Tan(0.5f * c.fieldOfView * Mathf.Deg2Rad), aspect = Mathf.Max(0.1f, c.aspect);
            var cp = c.transform.position; var fw = c.transform.forward; float best = 0f;
            for (int i = 0; i < e.pos.Length; i++)
            {
                if (coresOnly && !e.core[i]) continue;
                float hs = e.size[i] * scale; var p = M.MultiplyPoint3x4(e.pos[i]);
                float z = Vector3.Dot(p - cp, fw) - fpsPull * (e.core[i] ? 0.5f : 1f) - FpsStep;           // FpsStep: this frame's eye can be that much nearer
                if (z < -hs) continue;                                                                  // behind the eye: not in the picture
                if (z < FpsMinDepth) return 1f;
                float f = hs / (z * tanV), s = f * f / aspect;
                if (s > best) best = s;
            }
            return Mathf.Min(1f, best);
        }

        /// <summary>The mesh Apply draws for this glow mount: m as it is outside the view (and for the viewed unit, which Apply already leaves
        /// out); in the view the whole mesh, its cores only, or null (not drawn in this picture).</summary>
        Mesh FpsGlowMesh(Rig r, Mesh m, Transform mount)
        {
            if (!OzFpsView.On || !OzFpsGlow.Fixes || !m || !mount) return m;
            var c = OzFpsView.I ? OzFpsView.I.Cam : null; if (!c) return m;
            FpsCount(c);
            if ((mount.position - c.transform.position).sqrMagnitude > FpsFarM * FpsFarM) return m;
            FpsSeen++;
            var e = Elems(m); var M = mount.localToWorldMatrix;
            float all = Share(e, M, c, false); FpsMaxRaw = Mathf.Max(FpsMaxRaw, all);
            if (all <= FpsMaxShare) { FpsFull++; FpsMaxDrawn = Mathf.Max(FpsMaxDrawn, all); return m; }
            if (e.cores)
            {
                float core = Share(e, M, c, true);
                if (core <= FpsMaxShare) { FpsCores++; FpsMaxDrawn = Mathf.Max(FpsMaxDrawn, core); return e.cores; }
            }
            FpsHeld++;
            return null;
        }

        /// <summary>A haze cone in the view: not drawn while the eye stands by its lamp or in its box.</summary>
        bool FpsHoldBeam(Rig r)
        {
            if (!OzFpsView.On || !OzFpsGlow.Fixes || !r.BeamMesh || !r.HeadNode) return false;
            var c = OzFpsView.I ? OzFpsView.I.Cam : null; if (!c) return false;
            FpsCount(c);
            var cp = c.transform.position; float nearM = FpsBeamNearM + FpsStep;
            bool near = (r.HeadWorld - cp).sqrMagnitude < nearM * nearM;
            if (!near) { var b = r.BeamMesh.bounds; b.Expand(1f + 2f * FpsStep); near = b.Contains(r.HeadNode.InverseTransformPoint(cp)); }
            if (near) FpsBeamsHeld++;
            return near;
        }

        /// <summary>OzFpsGlow.Left (the view's leave, the RTS camera back on its focus): the real lights and the cones' share picked again now
        /// for the RTS focus (PickRealLights runs every 0.2 s otherwise) and every glow block sent again in the next Apply.</summary>
        public void FpsLeaveNow()
        {
            PickRealLights();
            foreach (var r in rigs) { r.ShownA = new Vector4(-1f, 0f, 0f, 0f); r.FaultShownA = new Vector4(-1f, 0f, 0f, 0f); }
            FpsLeaves++; FpsLeaveFrame = Time.frameCount;
        }

        /// <summary>Play check: every rig's drawn state (glow lit, cone and pool drawn, its real light on / intensity, candidate, effects) by
        /// vehicle id, for the before / after compare of the leave.</summary>
        public void FpsStates(Dictionary<string, string> into)
        {
            foreach (var r in rigs)
            {
                if (r == null || r.Obj == null) continue;
                into["vl " + r.Obj.Id] = $"{r.GlowOn} {r.BeamOn} {r.PoolOn} {(r.Spot ? r.Spot.enabled + " " + r.Spot.intensity.ToString("0.000") : "-")} {r.Candidate} {r.Effects} {r.EffK:0.00}";
            }
        }
    }
}
