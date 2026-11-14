// OpenZ (GPL-3.0) - Level 7 briefings: the small effects both cutscene sets share, view only, own random numbers. Everything is made from the game's
// own OpenZ/FxAdditive shapes (no new shader, no shader variant): a soft glow on the lava, a ring, thin smoke, a burst of droplets, and Wade: what shows
// where a leg goes into the lava (a dark crust collar, a hot line, ripples, smoke and steam on each footfall). The soft glows are shape 6, which measures from a world point: Tint and Centre keep that point on the quad, or they draw nothing.
// WRITTEN BY READING ONLY: compiled off-line with the project's code, never run in the engine. Remove this line once it runs.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public static class OzL07CutFx
    {
        public static readonly Color LavaGlow = new Color(2.6f, 0.62f, 0.10f, 1f), LavaRing = new Color(3.4f, 1.15f, 0.22f, 1f);

        /// <summary>A flat quad on the lava (y up), drawn with an FxAdditive shape: 5 = ring, 6 = soft glow. Tint() sets its colour each frame and keeps a glow on its quad.</summary>
        public static MeshRenderer Flat(Transform parent, string name, int layer, float shape, Color tint, uint lightMask = 1u)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name; go.layer = layer;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = DetailKit.FxMat("L07Cut_" + name, shape, 0f, 0f, tint);
            DetailKit.F(r.sharedMaterial, "_UseVertexColor", 0f); DetailKit.F(r.sharedMaterial, "_BandInner", 0.72f); DetailKit.F(r.sharedMaterial, "_BandSides", 0f);
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.renderingLayerMask = lightMask;
            Centre(r);
            return r;
        }

        /// <summary>The colour of a Flat; on a glow also its world centre, so it draws wherever the quad stands now. Call it after moving the quad.</summary>
        public static void Tint(MeshRenderer r, Color c, float gain) { if (!r || !r.sharedMaterial) return; r.sharedMaterial.SetColor("_Color", c * gain); Centre(r); }

        /// <summary>A soft glow (shape 6) follows its quad: centre at the quad, faded out at the quad's nearer edge. A ring (shape 5) is measured on the quad's own uv and needs none of this.</summary>
        public static void Centre(MeshRenderer r)
        {
            if (!r || !r.sharedMaterial || !Mathf.Approximately(r.sharedMaterial.GetFloat("_Shape"), 6f)) return;
            Vector3 s = r.transform.lossyScale;
            GlowAt(r, r.transform.position, Mathf.Min(Mathf.Abs(s.x), Mathf.Abs(s.y)) * 0.5f);
        }

        /// <summary>FxAdditive's soft glow (shape 6) is measured from a world point, not from the quad: without this it draws nothing. radius = where it has faded out; the quad must cover it (scale = 2 x radius).</summary>
        public static void GlowAt(MeshRenderer r, Vector3 world, float radius) { if (!r || !r.sharedMaterial) return; r.sharedMaterial.SetVector("_RingCenter", world); r.sharedMaterial.SetFloat("_RingRadius", radius); }

        /// <summary>The legs-in-lava look of TeamLit (lava_kits LV3, OzLavaWade.Push): the three floats in every slot's block of a TeamLit renderer. heat 0..1, line = the lava's world y, top = the band in m.
        /// TeamPaint.Heat (the melting robot's flat glow over the whole body) stays 0.</summary>
        public static void LegHeat(Renderer r, MaterialPropertyBlock b, float heat, float line, float top)
        {
            if (!r) return;
            int n = Mathf.Max(1, r.sharedMaterials.Length);
            for (int m = 0; m < n; m++) { r.GetPropertyBlock(b, m); b.SetFloat(LegHeatId, heat); b.SetFloat(LegLineId, line); b.SetFloat(LegTopId, top); r.SetPropertyBlock(b, m); }
        }
        static readonly int LegHeatId = Shader.PropertyToID("_OZLegHeat"), LegLineId = Shader.PropertyToID("_OZLegLine"), LegTopId = Shader.PropertyToID("_OZLegTop");

        /// <summary>Thin smoke or steam rising from a place (world space, unscaled time): the commander's cigar recipe at another size. rate = puffs a second.</summary>
        public static ParticleSystem Smoke(Transform parent, string name, int layer, uint lightMask, Vector3 local, float radius, float rate, float size, float life, float rise, Color c)
        {
            var go = new GameObject(name) { layer = layer }; go.transform.SetParent(parent, false); go.transform.localPosition = local;
            var p = go.AddComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = p.main;
            main.duration = 5f; main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = c;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true; main.maxParticles = 120;
            var em = p.emission; em.rateOverTime = rate;
            var sh = p.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius; sh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = p.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f); vel.y = new ParticleSystem.MinMaxCurve(rise * 0.75f, rise * 1.25f); vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            var sz = p.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.4f, 1.3f), new Keyframe(1f, 2.4f)));
            var col = p.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.45f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard; pr.sortMode = ParticleSystemSortMode.Distance;
            pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false; pr.renderingLayerMask = lightMask;
            pr.sharedMaterial = DetailKit.FxMat("L07Cut_" + name, 2f, 1f, 0f, Color.white);
            DetailKit.F(pr.sharedMaterial, "_UseVertexColor", 1f);
            // the puff's edge: without noise shape 2 is a flat disc to 96 % of its radius, and those read as stacked hard discs on the real frames. Eroded by the billow noise and faded from the middle out, age and a stable random from the vertex streams
            DetailKit.F(pr.sharedMaterial, "_NoiseAmt", 0.3f); DetailKit.F(pr.sharedMaterial, "_NoiseScale", 2.2f); DetailKit.F(pr.sharedMaterial, "_Boil", 0.8f); DetailKit.F(pr.sharedMaterial, "_Spin", 0.4f); DetailKit.F(pr.sharedMaterial, "_EdgeSoft", 1f);
            pr.SetActiveVertexStreams(PuffStreams);
            return p;
        }

        static readonly List<ParticleSystemVertexStream> PuffStreams = new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomX };   // as OpenZFxPlayer: uv.z age, uv.w stable random

        /// <summary>A flat quad of darkened crust lying on the lava (FxAdditive gauss, alpha over: it can darken, an additive shape cannot).</summary>
        public static MeshRenderer Crust(Transform parent, string name, int layer, uint lightMask = 1u)
        {
            var r = Flat(parent, name, layer, 6f, CrustColor, lightMask);
            DetailKit.F(r.sharedMaterial, "_Opaqueness", 1f); DetailKit.F(r.sharedMaterial, "_RingAlpha", CrustColor.a);
            if (r.sharedMaterial) r.sharedMaterial.renderQueue = 3003;                                      // under the hot line and the glows of the same spot
            return r;
        }
        public static readonly Color CrustColor = new Color(0.035f, 0.012f, 0.008f, 0.9f);

        /// <summary>What shows where a robot's legs go into the lava. Each leg: the point where the shin crosses the surface (knee and foot bones, so it follows the stride), a dark collar of pushed crust
        /// with a hot line inside it. Each footfall: a ripple running out, a puff of steam and dark smoke at that leg. Tick it after the bones moved. Own numbers, view only.</summary>
        public sealed class Wade
        {
            const int Ripples = 6; public const float RippleSeconds = 0.9f;
            readonly MeshRenderer[] collar = new MeshRenderer[2], line = new MeshRenderer[2], ripple = new MeshRenderer[Ripples]; readonly float[] rippleT = new float[Ripples], relLast = new float[2]; readonly bool[] down = new bool[2];
            readonly Transform[] foot = new Transform[2], knee = new Transform[2]; readonly ParticleSystem smoke; readonly float size; int next; bool shown = true; float puffT; int puffLeg;
            /// <summary>Check: footfalls seen so far.</summary>
            public int Footfalls { get; private set; }

            /// <summary>size = 1 for a soldier's shin (about 0.2 m wide).</summary>
            public Wade(Transform parent, string name, int layer, uint lightMask, float legSize)
            {
                size = legSize;
                for (int i = 0; i < 2; i++) { collar[i] = Crust(parent, name + "Collar" + i, layer, lightMask); line[i] = Flat(parent, name + "Line" + i, layer, 5f, LavaRing, lightMask); }
                for (int i = 0; i < Ripples; i++) { ripple[i] = Flat(parent, name + "Ripple" + i, layer, 5f, LavaRing, lightMask); ripple[i].enabled = false; rippleT[i] = -1f; }
                smoke = Smoke(parent, name + "Smoke", layer, lightMask, Vector3.zero, 0.06f * size, 0f, 0.30f * size, 1.6f, 0.9f, new Color(0.22f, 0.20f, 0.20f, 0.5f));
                smoke.Play();                                                                               // no steady emission: only the Emit calls below
            }

            /// <summary>The foot bones; the knee is each foot's parent bone.</summary>
            public void Bind(Transform footL, Transform footR)
            {
                foot[0] = footL; foot[1] = footR; knee[0] = footL ? footL.parent : null; knee[1] = footR ? footR.parent : null;
            }

            public void Show(bool on)
            {
                if (shown == on) return;
                shown = on;
                for (int i = 0; i < 2; i++) { if (collar[i]) collar[i].enabled = on; if (line[i]) line[i].enabled = on; }
                for (int i = 0; i < Ripples; i++) { if (!on) rippleT[i] = -1f; if (ripple[i]) ripple[i].enabled = false; }
                if (smoke) { if (on) smoke.Play(); else smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            }

            Vector3 Crossing(int s, float y, out bool wet)
            {   // where the shin goes through the sheet: the foot itself when the knee is under too (deep), nothing when the foot is out
                Vector3 f = foot[s].position, k = knee[s] ? knee[s].position : f + Vector3.up * 0.4f;
                wet = f.y < y;
                float t = k.y > f.y ? Mathf.Clamp01((y - f.y) / (k.y - f.y)) : 0f;
                var p = Vector3.Lerp(f, k, t); p.y = y; return p;
            }

            /// <summary>heat 0..1 = how hot the legs are; rootY = the robot's root height (a footfall is the foot's lowest point against it).</summary>
            public void Tick(float dt, float lavaY, float rootY, float heat)
            {
                if (!shown || !foot[0] || !foot[1]) return;
                for (int s = 0; s < 2; s++)
                {
                    var p = Crossing(s, lavaY, out bool wet);
                    collar[s].enabled = line[s].enabled = wet;
                    collar[s].transform.position = p + Vector3.up * 0.015f; collar[s].transform.localScale = Vector3.one * (0.78f * size); Centre(collar[s]);
                    line[s].transform.position = p + Vector3.up * 0.03f; line[s].transform.localScale = Vector3.one * (0.42f * size);
                    Tint(line[s], LavaRing, 0.35f + 0.65f * heat);
                    if (dt <= 0f) continue;
                    float rel = foot[s].position.y - rootY; bool d = rel < relLast[s] - 1e-4f;
                    if (down[s] && !d && wet) Fall(p);
                    down[s] = d; relLast[s] = rel;
                }
                if (dt <= 0f) return;
                for (int i = 0; i < Ripples; i++)
                {
                    if (rippleT[i] < 0f) continue;
                    rippleT[i] += dt; float u = rippleT[i] / RippleSeconds;
                    if (u >= 1f) { rippleT[i] = -1f; ripple[i].enabled = false; continue; }
                    ripple[i].transform.localScale = Vector3.one * ((0.45f + 1.0f * Mathf.Sqrt(u)) * size); Tint(ripple[i], LavaRing, 0.55f * (1f - u) * (1f - u));
                }
                puffT -= dt;
                if (puffT <= 0f)
                {   // thin dark smoke from each leg in turn, thicker as it heats
                    puffT = Mathf.Lerp(0.34f, 0.16f, heat); puffLeg = 1 - puffLeg;
                    var p = Crossing(puffLeg, lavaY, out bool wet);
                    if (wet) smoke.Emit(new ParticleSystem.EmitParams { position = p + Vector3.up * 0.06f, applyShapeToPosition = false }, 1);
                }
            }

            void Fall(Vector3 p)
            {
                Footfalls++;
                int i = next; next = (next + 1) % Ripples;
                ripple[i].transform.position = p + Vector3.up * 0.02f; ripple[i].transform.localScale = Vector3.one * (0.45f * size); ripple[i].enabled = true; rippleT[i] = 0f;
                smoke.Emit(new ParticleSystem.EmitParams { position = p + Vector3.up * 0.1f, applyShapeToPosition = false, startColor = new Color(0.86f, 0.86f, 0.86f, 0.30f) }, 2);    // white steam on every footfall (OzLavaWade's rule)
                smoke.Emit(new ParticleSystem.EmitParams { position = p + Vector3.up * 0.1f, applyShapeToPosition = false }, 2);
            }
        }

        /// <summary>The one lava pop on the spot: an HDR flash, a ring running out, droplets on short arcs. Tick it from the owner's clock.</summary>
        public sealed class Pop
        {
            readonly MeshRenderer flash, ring; readonly float flashGain; readonly List<Transform> drops = new List<Transform>(); readonly List<Vector3> vel = new List<Vector3>(); readonly Light light;
            Vector3 at; float t = -1f; readonly System.Random rng = new System.Random(1907);
            public const float Seconds = 1.1f;
            public int Fired { get; private set; }

            public Pop(Transform parent, int layer, int count, float flashGain)
            {
                this.flashGain = flashGain;
                flash = Flat(parent, "PopFlash", layer, 6f, LavaGlow); ring = Flat(parent, "PopRing", layer, 5f, LavaRing); DetailKit.F(ring.sharedMaterial, "_BandInner", 0.9f);   // a thin running line, not a wide flat band
                var mat = DetailKit.FxMat("L07Cut_PopDrop", 0f, 0f, 0f, LavaRing);
                DetailKit.F(mat, "_UseVertexColor", 0f);
                for (int i = 0; i < count; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = "PopDrop" + i; go.layer = layer;
                    Object.Destroy(go.GetComponent<Collider>()); go.transform.SetParent(parent, false);
                    var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off;
                    go.SetActive(false); drops.Add(go.transform); vel.Add(Vector3.zero);
                }
                var lg = new GameObject("PopLight") { layer = layer }; lg.transform.SetParent(parent, false);
                light = lg.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f, 0.46f, 0.14f); light.range = 7f; light.intensity = 0f; light.shadows = LightShadows.None;
                flash.enabled = ring.enabled = false;
            }

            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            public void Fire(Vector3 world)
            {
                at = world; t = 0f; Fired++;
                flash.enabled = ring.enabled = true;
                flash.transform.position = ring.transform.position = at + Vector3.up * 0.02f;
                light.transform.position = at + Vector3.up * 0.5f;
                for (int i = 0; i < drops.Count; i++)
                {
                    float a = R(0f, Mathf.PI * 2f), s = R(0.5f, 1.5f);
                    vel[i] = new Vector3(Mathf.Cos(a) * s, R(2.2f, 3.8f), Mathf.Sin(a) * s);
                    drops[i].position = at; drops[i].localScale = Vector3.one * R(0.03f, 0.07f); drops[i].gameObject.SetActive(true);
                }
            }

            public void Tick(float dt, Camera view)
            {
                if (t < 0f) return;
                t += dt;
                float u = Mathf.Clamp01(t / Seconds), f = Mathf.Clamp01(1f - t / 0.35f);
                flash.transform.localScale = Vector3.one * (0.5f + 1.1f * Mathf.Sqrt(Mathf.Clamp01(t / 0.3f))); Tint(flash, LavaGlow, flashGain * f);
                Tint(ring, LavaRing, 0.5f * (1f - u)); ring.transform.localScale = Vector3.one * (0.3f + 1.7f * Mathf.Sqrt(u));
                light.intensity = 26f * f * f;
                for (int i = 0; i < drops.Count; i++)
                {
                    if (!drops[i].gameObject.activeSelf) continue;
                    vel[i] += Vector3.down * 7.5f * dt; drops[i].position += vel[i] * dt;
                    if (view) drops[i].rotation = view.transform.rotation;
                    if (drops[i].position.y < at.y) drops[i].gameObject.SetActive(false);
                }
                if (t >= Seconds) { t = -1f; flash.enabled = ring.enabled = false; light.intensity = 0f; foreach (var d in drops) d.gameObject.SetActive(false); }
            }
        }
    }
}
