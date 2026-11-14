// OpenZ (GPL-3.0) - version 1.2, the blue side's cutscenes: the robots' shadows on the HQ floor (OzEvilStage).
// Owner item 161: in EvilZod's HQ the two robots stood on the dark floor with nothing under them and looked pasted on.
// Why there was no shadow: the stage's key (EzKey, the one light that casts) stands 3.5 m up in front of the robots, so it throws their
// shadows BEHIND them, 1.5-2 m long toward the back wall, mostly hidden by their own bodies from the lens in front, on a floor of about
// 1 % albedo, seen from a low lens that looks over it: nothing of it reads in any shot. The other lights (the rims from behind, the
// face light, the fills, the washes) cast none, and a shadow can only darken the little light that falls on that floor.
// Now each robot (EvilZod, BS-01 and the fleeing lookalikes of the LOSE scene) stands in soft contact shadows drawn on the floor every
// frame after its pose; no light, no shadow map, no camera, no pose and no timing changes:
//  - one under each foot, along the foot (it fades and spreads as the foot lifts: the walk-off, the running clones);
//  - a soft pool under the stance (across both feet);
//  - a soft cast shadow from the feet away from the robot's own key light (its direction from where that light stands this frame, its
//    length from the light's slope), fading out along its length, so it agrees with the key's real shadow.
// Unlit soft-edged quads 1 cm over the floor (over its seams too: no z-fighting), transparent black, the stage layer only: at most
// four small draws per robot. FloorShadowMs / FloorShadowsDrawn report the cost for the checks.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        /// <summary>The floor shadows' darkness at their centres (0..1): under a foot, the stance's pool, the cast shadow.</summary>
        public static float FootShadowA = 1.0f, PoolShadowA = 1.0f, CastShadowA = 0.85f;
        /// <summary>The cast shadow's length: this x the robot's height x the key's horizontal-to-vertical slope (the slope clamped 0.45 .. 1.5).</summary>
        public static float CastShadowLen = 1.2f;
        /// <summary>A foot lifted by this much (x the robot's height) has no contact shadow left.</summary>
        public static float FootFadeLift = 0.12f;
        /// <summary>The soft edge: each shadow quad is this much larger than the outline it darkens (its falloff starts inside it).</summary>
        public static float ShadowSoftness = 1.7f;
        /// <summary>The checks: this stage's floor shadows drawn this frame and the time their placing takes (ms, smoothed). Per stage: the
        /// battlefield screen's card builds a stage of its own beside the director's.</summary>
        public int FloorShadowsDrawn { get; private set; }
        public float FloorShadowMs { get; private set; }
#if UNITY_EDITOR
        /// <summary>Editor proofs only (a before / after pair on one held frame): the floor shadows are not drawn while this is set.</summary>
        public static bool ProofHideFloorShadows;
#endif
        const float ShadowLift = 0.01f;                                                                   // over the floor's top (y 0) and its seams (0.004)

        sealed class Grounding { public Renderer FootL, FootR, Pool, Cast; public float RestL = float.MaxValue, RestR = float.MaxValue; public int Frame; }
        readonly Dictionary<Robot, Grounding> groundings = new Dictionary<Robot, Grounding>();
        readonly List<Robot> groundingsGone = new List<Robot>();
        Transform shadowRoot; Material blobMat, castMat; MaterialPropertyBlock shadowMpb; int shadowFrame; bool shadowWarned;
        static Mesh shadowQuad; static Texture2D blobTex, castTex;
        static readonly int ShadowColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>The checks: what the floor shadows did this frame.</summary>
        public string FloorShadowStatus() => $"floor shadows: {FloorShadowsDrawn} drawn for {groundings.Count} robot(s), {FloorShadowMs:F3} ms";

        /// <summary>Every frame, after the poses and the lights: each robot on the stage gets its floor shadows where its feet are now.</summary>
        void PlaceFloorShadows()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            shadowFrame++;
            int drawn = 0;
            bool show = Live && !LavaWalkOn;                                                              // level 7: no floor, the lava is the floor
#if UNITY_EDITOR
            if (ProofHideFloorShadows) show = false;
#endif
            if (show)
            {
                if (ez != null && ez.Ok) drawn += Ground(ez, ezOnStage, EzHeight, key);
                if (sk != null && sk.Ok) drawn += Ground(sk, skOnStage, SkHeight, skKey && skKey.enabled ? skKey : key);
                foreach (var u in runners) if (u.on && !u.done && u.R.Rig != null) drawn += Ground(u.R, true, 1.45f * u.R.Rig.Scale, key);
            }
            groundingsGone.Clear();
            foreach (var kv in groundings)
            {
                if (kv.Value.Frame == shadowFrame) continue;
                if (kv.Key.Go == null) groundingsGone.Add(kv.Key);                                        // a lookalike that ran off: its shadows go with it
                else ShowGrounding(kv.Value, false);                                                      // off the stage / not shown this frame
            }
            foreach (var r in groundingsGone) { DestroyGrounding(groundings[r]); groundings.Remove(r); }
            FloorShadowsDrawn = drawn;
            float ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000f / System.Diagnostics.Stopwatch.Frequency;
            FloorShadowMs = FloorShadowMs <= 0f ? ms : Mathf.Lerp(FloorShadowMs, ms, 0.05f);
        }

        /// <summary>One robot's floor shadows this frame (stage frame: the floor's top is y 0). Returns how many are drawn.</summary>
        int Ground(Robot r, bool onStage, float height, Light keyLight)
        {
            if (!onStage || r.Go == null || !r.Go.activeInHierarchy || r.Rig == null) return 0;
            if (!groundings.TryGetValue(r, out var g)) { g = NewGrounding(r.Name); groundings[r] = g; }
            g.Frame = shadowFrame;
            if (!g.Pool) return 0;                                                                        // the shadow material is missing (warned once)
            var rig = r.Rig;
            float H = Mathf.Max(0.3f, height), soft = Mathf.Max(1f, ShadowSoftness);
            Vector3 fwd = Flat(transform.InverseTransformDirection(rig.Fwd), Vector3.forward);
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 c; Vector3 across = right; float wide, deep, fadeL = 0f, fadeR = 0f;
            int n = 0;
            if (rig.FootL && rig.FootR)
            {
                FootSole(rig.FootL, rig.ToeL, fwd, H, out var cL, out var dL, out var lenL, out var yL);
                FootSole(rig.FootR, rig.ToeR, fwd, H, out var cR, out var dR, out var lenR, out var yR);
                g.RestL = Mathf.Min(g.RestL, yL); g.RestR = Mathf.Min(g.RestR, yR);                      // the ankle's height when the foot stands
                float fadeAt = Mathf.Max(0.02f, FootFadeLift * H);
                fadeL = Mathf.Clamp01((yL - g.RestL) / fadeAt); fadeR = Mathf.Clamp01((yR - g.RestR) / fadeAt);
                n += SetShadow(g.FootL, cL, dL, 0.55f * lenL * soft * (1f + 0.6f * fadeL), lenL * soft * (1f + 0.4f * fadeL), FootShadowA * (1f - fadeL));
                n += SetShadow(g.FootR, cR, dR, 0.55f * lenR * soft * (1f + 0.6f * fadeR), lenR * soft * (1f + 0.4f * fadeR), FootShadowA * (1f - fadeR));
                c = (cL + cR) * 0.5f;
                Vector3 lr = cR - cL; lr.y = 0f; float apart = lr.magnitude;
                if (apart > 0.03f * H) across = lr / apart;
                float sole = 0.5f * (lenL + lenR);
                wide = Mathf.Max((apart + sole) * soft, 0.34f * H); deep = Mathf.Max(1.1f * sole * soft, 0.26f * H);
            }
            else
            {   // no leg bones: one pool under the root
                c = transform.InverseTransformPoint(r.Root.position); c.y = 0f;
                wide = 0.34f * H; deep = 0.26f * H;
                ShowOne(g.FootL, false); ShowOne(g.FootR, false);
            }
            n += SetShadow(g.Pool, c, new Vector3(-across.z, 0f, across.x), wide, deep, PoolShadowA * (1f - 0.25f * (fadeL + fadeR)));
            // the cast shadow: away from the robot's key light, as long as that light's slope makes it
            Vector3 lp = keyLight ? transform.InverseTransformPoint(keyLight.transform.position) : c + new Vector3(1.85f, 3.5f, 3.3f);
            Vector3 away = c - lp; away.y = 0f; float dh = away.magnitude;
            away = dh > 0.05f ? away / dh : new Vector3(-0.45f, 0f, -0.9f).normalized;
            float slope = Mathf.Clamp(dh / Mathf.Max(0.3f, lp.y - 0.5f * H), 0.45f, 1.5f);
            float len = Mathf.Max(0.2f * H, CastShadowLen * H * slope);
            n += SetShadow(g.Cast, c + away * (0.5f * len - 0.1f * H), away, Mathf.Max(0.36f * H, 0.8f * wide), len, CastShadowA * (1f - 0.25f * (fadeL + fadeR)));
            return n;
        }

        /// <summary>A foot's sole on the floor (stage frame): its centre and direction from the ankle and the toe base, its length, and the
        /// ankle's height (the lift).</summary>
        void FootSole(Transform ankle, Transform toe, Vector3 fwd, float H, out Vector3 centre, out Vector3 along, out float len, out float ankleY)
        {
            Vector3 a = transform.InverseTransformPoint(ankle.position); ankleY = a.y; a.y = 0f;
            Vector3 d = Vector3.zero;
            if (toe) { Vector3 t = transform.InverseTransformPoint(toe.position); t.y = 0f; d = t - a; }
            float l0 = d.magnitude;
            if (l0 < 0.03f * H) { along = fwd; l0 = 0.08f * H; } else along = d / l0;
            Vector3 heel = a - along * (0.35f * l0), tip = a + along * (1.6f * l0);                   // the sole: a little behind the ankle to past the toe base
            centre = (heel + tip) * 0.5f; len = 1.95f * l0;
        }

        static Vector3 Flat(Vector3 v, Vector3 fallback) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : fallback; }

        /// <summary>One shadow quad: centre on the floor, its length along `along`, its width across, its darkness. Returns 1 when drawn.</summary>
        int SetShadow(Renderer rd, Vector3 centre, Vector3 along, float width, float length, float alpha)
        {
            if (!rd) return 0;
            alpha = Mathf.Clamp01(alpha);
            if (alpha < 0.004f || width < 1e-3f || length < 1e-3f) { ShowOne(rd, false); return 0; }
            var t = rd.transform;
            t.localPosition = new Vector3(centre.x, ShadowLift, centre.z);
            t.localRotation = Quaternion.LookRotation(Flat(along, Vector3.forward), Vector3.up);
            t.localScale = new Vector3(width, 1f, length);
            shadowMpb.SetColor(ShadowColorId, new Color(0f, 0f, 0f, alpha));
            rd.SetPropertyBlock(shadowMpb);
            ShowOne(rd, true);
            return 1;
        }

        static void ShowOne(Renderer rd, bool on) { if (rd && rd.enabled != on) rd.enabled = on; }
        static void ShowGrounding(Grounding g, bool on) { ShowOne(g.FootL, on); ShowOne(g.FootR, on); ShowOne(g.Pool, on); ShowOne(g.Cast, on); }
        static void DestroyGrounding(Grounding g)
        {
            foreach (var rd in new[] { g.FootL, g.FootR, g.Pool, g.Cast }) if (rd) Destroy(rd.gameObject);
        }

        Grounding NewGrounding(string name)
        {
            var g = new Grounding();
            if (!EnsureShadowKit()) return g;
            g.Pool = ShadowQuadOf(name + "_FloorPool", blobMat);
            g.FootL = ShadowQuadOf(name + "_FootShadowL", blobMat);
            g.FootR = ShadowQuadOf(name + "_FootShadowR", blobMat);
            g.Cast = ShadowQuadOf(name + "_CastShadow", castMat);
            return g;
        }

        Renderer ShadowQuadOf(string name, Material m)
        {
            var go = new GameObject(name) { layer = Layer };
            go.transform.SetParent(shadowRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = shadowQuad;
            var rd = go.AddComponent<MeshRenderer>();
            rd.sharedMaterial = m; rd.shadowCastingMode = ShadowCastingMode.Off; rd.receiveShadows = false;
            rd.lightProbeUsage = LightProbeUsage.Off; rd.reflectionProbeUsage = ReflectionProbeUsage.Off; rd.renderingLayerMask = LightMask;
            rd.allowOcclusionWhenDynamic = false; rd.enabled = false;
            return rd;
        }

        /// <summary>The shared pieces: the root under the stage, the flat quad, the two soft pictures, the two materials (the studio's
        /// transparent unlit template: its shader variants ship).</summary>
        bool EnsureShadowKit()
        {
            if (!shadowRoot) { var go = new GameObject("FloorShadows") { layer = Layer }; go.transform.SetParent(transform, false); shadowRoot = go.transform; }
            if (shadowMpb == null) shadowMpb = new MaterialPropertyBlock();
            if (!shadowQuad) shadowQuad = MakeShadowQuad();
            if (!blobTex) blobTex = MakeShadowTex("FloorShadowBlob", false);
            if (!castTex) castTex = MakeShadowTex("FloorShadowCast", true);
            if (!blobMat) blobMat = ShadowMat(blobTex);
            if (!castMat) castMat = ShadowMat(castTex);
            if (blobMat && castMat) return true;
            if (!shadowWarned) { shadowWarned = true; Debug.LogWarning("[EvilZod] floor shadows: Resources/" + ZodStage.MatPath + "ZodStage_Smoke missing: no floor shadows"); }
            return false;
        }

        static Material ShadowMat(Texture2D tex)
        {
            var t = Resources.Load<Material>(ZodStage.MatPath + "ZodStage_Smoke");                        // transparent (alpha blend), unlit, no soft particles
            if (!t) return null;
            var m = new Material(t) { hideFlags = HideFlags.DontSave, name = "EvilStage_FloorShadow" };
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(0f, 0f, 0f, 1f));
            m.SetFloat("_Cull", 0f);                                                                      // both faces (a flat quad seen from any side)
            m.renderQueue = 2990;                                                                         // before the stage's smoke
            madeMats.Add(m);
            return m;
        }

        /// <summary>A flat quad on the floor plane (x across, z along, uv v = 0 at -z), white vertex colours (the particle shader reads them).</summary>
        static Mesh MakeShadowQuad()
        {
            var m = new Mesh { name = "FloorShadowQuad", hideFlags = HideFlags.DontSave };
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The soft pictures (white, the darkness in alpha, 0 at every edge): a round falloff (feet, pool), or the cast shadow's
        /// (soft sides, rising over the first 18 % from the feet end v = 0, fading out to the far end).</summary>
        static Texture2D MakeShadowTex(string name, bool cast)
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4, hideFlags = HideFlags.DontSave };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N, a;
                    if (!cast) { float w = v * 2f - 1f, r2 = u * u + w * w; a = r2 >= 1f ? 0f : (1f - r2) * (1f - r2); }
                    else
                    {
                        float side = Mathf.Max(0f, 1f - u * u); side *= side;
                        a = side * Mathf.SmoothStep(0f, 1f, v / 0.18f) * Mathf.Pow(Mathf.Max(0f, 1f - v), 1.6f);
                    }
                    px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px); tex.Apply(true, true);
            return tex;
        }
    }
}
