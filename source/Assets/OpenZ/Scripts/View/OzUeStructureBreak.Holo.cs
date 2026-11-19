using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// 1.5 ue_structure_destruction: the holo obstacle's death. Before: its top outline rose 2.5 m and faded in 0.7 s. Now the hologram SHATTERS
    /// into its own panels: every face splits along its 1 m grid into panels of 1 x 1 to 2 x 2 m, drawn by the obstacle's own hologram shader
    /// (OpenZ/UeHolo, the "top" look: fill, 1 m grid, neon edges with their glow, corner dots, the scan; both sides), with a white-hot hit flash
    /// that decays, glitch flicker, then physics (thrown out of the box, tumbling, bouncing on the floor; holograms pass through each other);
    /// they lie dimming 1.5-3.5 s and dissolve one by one with a last blip. With it: a burst of holo-coloured sparks and glows from the edges,
    /// a flash of holo light on the floor, the projector's floor pool pulsing out and fading, a shower of light motes rising; the glass
    /// shatter and spark crackle with the hologram's fizzle, soft glass tinks on landings, a last soft fizzle as the last panels go.
    /// No dust, no scorch, no rubble that stays: a hologram is light (the floor stays clean for the next drop).
    /// </summary>
    public static partial class OzUeStructureBreak
    {
        /// <summary>Panels: launch speed (m/s, out and up), how long they lie, the dissolve (s), the hit flash (s).</summary>
        public static float HoloOutMin = 2.2f, HoloOutMax = 5.5f, HoloUpMin = 1.5f, HoloUpMax = 4.2f, HoloLieMin = 1.5f, HoloLieMax = 3.5f, HoloDissolve = 0.55f, HoloFlash = 0.3f;
        public static int HoloShatters, HoloPanels, HoloTinks;
        static readonly List<OzUeHoloShatter> holos = new List<OzUeHoloShatter>();

        /// <summary>OzUeObstacleView.BreakUp: the shatter (true = done; the view hides its box). centre = the box's floor middle, size = (w, h, d).</summary>
        internal static bool Obstacle(ZObject o, Vector3 centre, Vector4 size)
        {
            if (!On || o == null) return false;
            var mats = OzUeObstacleViews.Materials(); if (mats == null) return false;
            try
            {
                Layers();
                var go = new GameObject("OZ_UeHoloShatter_" + o.Id);
                var s = go.AddComponent<OzUeHoloShatter>();
                s.Build(o.Id, centre, size, mats);
                holos.Add(s); HoloShatters++;
                Debug.Log($"{OzUnitEditor.Tag} obstacle #{o.Id} shatters: {s.Count} hologram panels");
                return true;
            }
            catch (System.Exception e) { Debug.LogError($"{OzUnitEditor.Tag} obstacle #{o.Id} shatter: {e.Message} (the old fade plays)"); return false; }
        }

        static void HoloClearAll()
        {
            foreach (var h in holos) if (h) Object.Destroy(h.gameObject);
            holos.Clear();
        }

        internal static void HoloForget(OzUeHoloShatter s) => holos.Remove(s);
    }

    /// <summary>One shattered hologram: its panels and its pool pulse.</summary>
    public sealed class OzUeHoloShatter : MonoBehaviour
    {
        sealed class Panel
        {
            public GameObject go; public Renderer r; public Rigidbody rb; public Vector2 size; public float lie, seed, landedAt = -1f; public bool blipped, tinked;
        }

        static Material seen, pool;
        readonly List<Panel> panels = new List<Panel>();
        readonly List<Mesh> meshes = new List<Mesh>();
        MaterialPropertyBlock mpb;
        GameObject poolGo; Renderer poolR; Vector3 centre; Vector4 size; float t, holoTime; int id; bool lastFizzle;
        public int Count => panels.Count;

        static float R(float a, float b) => Random.Range(a, b);

        internal void Build(int objId, Vector3 c, Vector4 sz, Material[] src)
        {
            id = objId; centre = c; size = sz; mpb = new MaterialPropertyBlock();
            if (!seen) { seen = new Material(src[3]) { name = "UeHolo_Shard" }; seen.SetFloat(OzUeObstacleViews.CullId, (float)CullMode.Off); }   // both sides: a tumbling panel
            if (!pool) { pool = new Material(src[0]) { name = "UeHolo_ShardPool" }; }
            float w = sz.x, h = sz.y, d = sz.z;
            // the box's five faces as BoxMesh lays them: (centre, right, up, normal, width, height)
            var faces = new (Vector3 c, Vector3 r, Vector3 u, Vector3 n, float fw, float fh)[]
            {
                (new Vector3(w * 0.5f, h * 0.5f, 0f), Vector3.forward, Vector3.up, Vector3.right, d, h),
                (new Vector3(-w * 0.5f, h * 0.5f, 0f), Vector3.back, Vector3.up, Vector3.left, d, h),
                (new Vector3(0f, h * 0.5f, d * 0.5f), Vector3.left, Vector3.up, Vector3.forward, w, h),
                (new Vector3(0f, h * 0.5f, -d * 0.5f), Vector3.right, Vector3.up, Vector3.back, w, h),
                (new Vector3(0f, h, 0f), Vector3.right, Vector3.forward, Vector3.up, w, d),
            };
            var core = c + Vector3.up * h * 0.4f;
            foreach (var f in faces)
            {
                int nx = Mathf.Max(1, Mathf.RoundToInt(f.fw)), ny = Mathf.Max(1, Mathf.RoundToInt(f.fh));
                float cw = f.fw / nx, ch = f.fh / ny;
                var used = new bool[nx, ny];
                for (int iy = 0; iy < ny; iy++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        if (used[ix, iy]) continue;
                        int sx = 1, sy = 1;
                        float pick = Random.value;
                        if (pick < 0.3f && ix + 1 < nx && !used[ix + 1, iy]) sx = 2;
                        else if (pick < 0.55f && iy + 1 < ny && !used[ix, iy + 1]) sy = 2;
                        else if (pick < 0.7f && ix + 1 < nx && iy + 1 < ny && !used[ix + 1, iy] && !used[ix, iy + 1] && !used[ix + 1, iy + 1]) { sx = 2; sy = 2; }
                        for (int a = 0; a < sx; a++) for (int b = 0; b < sy; b++) used[ix + a, iy + b] = true;
                        float pw = sx * cw, ph = sy * ch;
                        var local = f.c - f.r * (f.fw * 0.5f) - f.u * (f.fh * 0.5f) + f.r * (ix * cw + pw * 0.5f) + f.u * (iy * ch + ph * 0.5f);
                        MakePanel(c + local, f.r, f.u, f.n, pw, ph, core);
                    }
            }
            // the projector's floor pool: a big quad, its radius pulsing out from the box's size
            poolGo = new GameObject("Pool"); poolGo.transform.SetParent(transform, false); poolGo.transform.position = c;
            var pm = OzUeObstacleViews.PoolMesh(0.9f * Mathf.Max(w, d) * 2.4f); meshes.Add(pm);
            poolGo.AddComponent<MeshFilter>().sharedMesh = pm;
            poolR = poolGo.AddComponent<MeshRenderer>(); poolR.sharedMaterial = pool; poolR.shadowCastingMode = ShadowCastingMode.Off; poolR.receiveShadows = false;
            // the burst: sparks and glows off the edges, light motes, the flash of light, the sounds
            var holo = OzUeObstacleViews.HoloColor; var hot = new Color(holo.r * 3f, holo.g * 3f, holo.b * 3f, 1f);
            var fx = OpenZFxPlayer.I;
            if (fx)
            {
                for (int i = 0; i < 44; i++)
                {
                    var e = c + new Vector3(R(-w, w) * 0.5f, R(0f, h), R(-d, d) * 0.5f);
                    var v = (e - core).normalized * R(2f, 6f) + Vector3.up * R(0.5f, 3f);
                    fx.EmitChannel(i % 3 == 0 ? "leg_spark" : "wv2_spark", e, v, R(0.25f, 0.6f), R(0.04f, 0.08f), i % 4 == 0 ? Color.white : hot, 1f);
                }
                for (int i = 0; i < 10; i++) fx.EmitChannel("wv2_glow", c + new Vector3(R(-w, w) * 0.5f, R(0.3f, h), R(-d, d) * 0.5f), Random.insideUnitSphere * 1.2f, R(0.15f, 0.3f), R(0.6f, 1.2f), holo, 0.9f);
                for (int i = 0; i < 24; i++) fx.EmitChannel("wv2_glow", c + new Vector3(R(-w, w) * 0.45f, R(0.2f, h), R(-d, d) * 0.45f), new Vector3(R(-0.3f, 0.3f), R(0.8f, 2.2f), R(-0.3f, 0.3f)), R(0.9f, 1.8f), R(0.12f, 0.25f), holo, 0.8f);
                fx.AddLight(c + Vector3.up * 1.6f, holo, 7f, 15f, 0.45f, 2.2f, 0.25f, true);
            }
            var au = CombatAudio.I;
            if (AudioDirector.Has("dst_glass")) AudioDirector.Fire("dst_glass", c + Vector3.up * 1.5f, id, 0.7f, 1.15f);
            if (au) au.PlayPitched("ue_holo_crackle", c + Vector3.up * 1.5f, 0.5f, 0f, id, 1f, "sd_lamp_spark_a", "sd_lamp_spark_b", "sd_lamp_spark_c");
        }

        void MakePanel(Vector3 at, Vector3 right, Vector3 up, Vector3 normal, float pw, float ph, Vector3 core)
        {
            var go = new GameObject("OZ_UeHoloPanel") { layer = OzUeStructureBreak.Light };
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(normal, up));                       // local x = the panel's right (or its mirror), y = up, z = the normal
            var m = new Mesh { name = "OZ_UeHoloPanel" }; meshes.Add(m);
            float x = pw * 0.5f, y = ph * 0.5f;
            m.SetVertices(new List<Vector3> { new Vector3(-x, -y, 0f), new Vector3(-x, y, 0f), new Vector3(x, y, 0f), new Vector3(x, -y, 0f) });
            m.SetNormals(new List<Vector3> { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward });
            m.SetUVs(0, new List<Vector4> { new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, ph, 0f, 0f), new Vector4(pw, ph, 0f, 0f), new Vector4(pw, 0f, 0f, 0f) });
            var u1 = new Vector4(pw, ph, 4f, 0f);                                                                 // face id 4: the hologram's lit-top look
            m.SetUVs(1, new List<Vector4> { u1, u1, u1, u1 });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            m.bounds = new Bounds(Vector3.zero, new Vector3(pw + 0.5f, ph + 0.5f, 0.5f));
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = seen;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(pw, ph, 0.06f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 2f * pw * ph; rb.linearDamping = 0.35f; rb.angularDamping = 0.4f; rb.maxAngularVelocity = 10f;
            rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var outDir = (at - core); outDir.y *= 0.4f; outDir = outDir.sqrMagnitude > 1e-4f ? outDir.normalized : normal;
            rb.linearVelocity = outDir * R(OzUeStructureBreak.HoloOutMin, OzUeStructureBreak.HoloOutMax) + Vector3.up * R(OzUeStructureBreak.HoloUpMin, OzUeStructureBreak.HoloUpMax);
            rb.angularVelocity = Random.insideUnitSphere * R(2f, 7f);
            go.AddComponent<OzUeHoloTink>().owner = this;
            panels.Add(new Panel { go = go, r = r, rb = rb, size = new Vector2(pw, ph), lie = R(OzUeStructureBreak.HoloLieMin, OzUeStructureBreak.HoloLieMax), seed = Random.value });
            OzUeStructureBreak.HoloPanels++;
        }

        internal void Tink(GameObject go, Vector3 at, float speed)
        {
            foreach (var p in panels)
            {
                if (p.go != go) continue;
                if (p.landedAt < 0f) p.landedAt = t;
                if (p.tinked || speed < 1.5f) return;
                p.tinked = true;
                if (!OzUeStructureBreak.TryKnock()) return;
                OzUeStructureBreak.HoloTinks++;
                var au = CombatAudio.I;
                if (au) au.PlayPitched("ue_holo_tink", at, Mathf.Clamp(0.12f + speed * 0.03f, 0.12f, 0.28f), 0f, go.GetInstanceID(), R(1.1f, 1.35f), "sd_lamp_glass_a", "sd_lamp_glass_b");
                var fx = OpenZFxPlayer.I;
                if (fx) for (int i = 0; i < 3; i++) fx.EmitChannel("wv2_spark", at, Random.insideUnitSphere * 1.5f + Vector3.up, R(0.15f, 0.3f), 0.04f, OzUeObstacleViews.HoloColor * 2f, 1f);
                return;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime; if (dt <= 0f) return;                                                    // paused: frozen
            t += dt; if (!OzMotion.Reduced) holoTime += dt;
            var holo = OzUeObstacleViews.HoloColor;
            seen.SetColor(OzUeObstacleViews.HoloColorId, holo); seen.SetFloat(OzUeObstacleViews.HoloTimeId, holoTime);
            pool.SetColor(OzUeObstacleViews.HoloColorId, holo);
            // the pool: grows out from the box and fades in 0.6 s
            if (poolR)
            {
                float k = Mathf.Clamp01(t / 0.6f);
                if (k >= 1f) { Destroy(poolGo); poolR = null; }
                else
                {
                    mpb.Clear();
                    float grow = 1f + 1.4f * (1f - (1f - k) * (1f - k));
                    mpb.SetVector(OzUeObstacleViews.BoxSizeId, new Vector4(size.x * grow, size.y, size.z * grow, 0f)); mpb.SetVector(OzUeObstacleViews.BoxCentreId, centre);
                    mpb.SetFloat(OzUeObstacleViews.AlphaId, (1f - k) * 2.2f > 1f ? 1f : (1f - k) * 2.2f); mpb.SetFloat(OzUeObstacleViews.RiseId, 0f); mpb.SetFloat(OzUeObstacleViews.HitId, 0f);
                    poolR.SetPropertyBlock(mpb);
                }
            }
            int alive = 0;
            foreach (var p in panels)
            {
                if (!p.go) continue;
                if (p.landedAt < 0f && t > 2.5f) p.landedAt = t;                                                  // never touched down (a far bounce): it lies from now
                float alpha = 1f, hit = Mathf.Clamp01(1f - t / OzUeStructureBreak.HoloFlash);
                if (!OzMotion.Reduced && Random.value < (t < 1f ? 0.1f : 0.05f)) alpha = R(0.25f, 0.6f);              // glitch flicker
                if (p.landedAt >= 0f)
                {
                    float lying = t - p.landedAt;
                    alpha *= Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(lying / p.lie));
                    if (lying > p.lie)
                    {
                        float q = (lying - p.lie) / OzUeStructureBreak.HoloDissolve;
                        if (!p.blipped) { p.blipped = true; var fx = OpenZFxPlayer.I; if (fx) fx.EmitChannel("wv2_glow", p.go.transform.position, Vector3.up * 0.6f, 0.25f, Mathf.Max(p.size.x, p.size.y) * 0.6f, holo, 0.8f); }
                        hit = Mathf.Max(hit, Mathf.Clamp01(1f - q * 3f) * 0.6f);                                  // a last blip
                        alpha *= Mathf.Clamp01(1f - q);
                        if (q >= 1f) { Destroy(p.go); continue; }
                    }
                }
                alive++;
                mpb.Clear();
                mpb.SetVector(OzUeObstacleViews.BoxSizeId, new Vector4(p.size.x, 0f, p.size.y, 0f)); mpb.SetVector(OzUeObstacleViews.BoxCentreId, p.go.transform.position);
                mpb.SetFloat(OzUeObstacleViews.HitId, hit); mpb.SetFloat(OzUeObstacleViews.AlphaId, alpha); mpb.SetFloat(OzUeObstacleViews.RiseId, 0f);
                mpb.SetFloat(OzUeObstacleViews.SeedId, p.seed);
                p.r.SetPropertyBlock(mpb);
            }
            if (!lastFizzle && t > 1f && alive <= panels.Count / 4) { lastFizzle = true; if (AudioDirector.Has("electro_fizzle")) AudioDirector.Fire("electro_fizzle", centre + Vector3.up, id, 0.4f, 1.2f); }
            if (alive == 0 && !poolR) { OzUeStructureBreak.HoloForget(this); Destroy(gameObject); }
        }

        void OnDestroy() { foreach (var m in meshes) if (m) Destroy(m); OzUeStructureBreak.HoloForget(this); }
    }

    /// <summary>A hologram panel's contacts go to its shatter (the landing time, a soft tink).</summary>
    public sealed class OzUeHoloTink : MonoBehaviour
    {
        internal OzUeHoloShatter owner;
        void OnCollisionEnter(Collision c) { if (owner) owner.Tink(gameObject, c.contactCount > 0 ? c.GetContact(0).point : transform.position, c.relativeVelocity.magnitude); }
    }
}
