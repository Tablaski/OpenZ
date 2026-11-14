using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using C = OpenZ.OzUeMushroomCurve;

namespace OpenZ
{
    /// <summary>
    /// projects ported to URP with bloom. The levels' nuke (NukeFx, OpenZNukeMushroom, OpenZ/Nuke) is not changed by a byte: this
    /// file is reached only from OzUeNuke.Impact, the editor's own impact.
    /// <list type="bullet">
    /// <item><b>What it keeps</b>: the levels' blast as it is, NukeFx.Trigger at the same moment: the white flash and the exposure flash,
    /// the flash light, the white-hot heart of the first 2 s, the shockwave ring with its dust wall and shove (the rules' kill wave
    /// stays on that ring), the side explosions and debris, the ground fog rings, the rumble and every sound, the bloom sustain.</item>
    /// <item><b>What it replaces</b>: the levels' mushroom of that blast: the OpenZ/Nuke cap and stem meshes, the fireball riding the cap
    /// and the glow shell around it are hidden (their renderers off; NukeFx runs on unchanged), and the puff cloud is not made
    /// (OpenZNukeMushroom.Enabled is false only while NukeFx.Trigger runs, then put back). The fire light (the ground lit by the
    /// fireball) and the heat shimmer follow this cloud's glow, and the fog hole sits on this cloud's cap.</item>
    /// <item><b>The cloud</b>: a unit sphere (64 x 64, the cap) and an open cylinder (radius 1 at the top, 0.8 at the foot, 32 x 16, the
    /// stem) with OpenZ/UeNukeMushroom (Resources/OpenZ/UnitEditor/OzUeNukeMushroom.shader), on the source's timeline
    /// (View/OzUeMushroom.Curve.cs): rises to H and spreads its cap to 0.75 H in 6.9 s, white-yellow core and orange crust first,
    /// cooling to orange, then thinning into dark wisps; additive, HDR (DisplayMatch 1.63: the source's colours as its screen showed
    /// them, tools/colour_match.py), so the hot parts sit 2-10 x over the URP bloom threshold and glow. Size: the mean cloud (size
    /// 1.05 of the source's 0.9 .. 1.2) spreads its cap to the kill ring (30 m): about 40 m high and 60 m across; each drop point
    /// gives its own size (a hash of the point, no global random draw).</item>
    /// <item><b></b><see cref="On"/>
    /// NUKE draws the levels' own blast with its mushroom exactly as in 1.3 (OzUeNuke calls NukeFx.Trigger itself).</item>
    /// </list>
    /// Checks: View/OzUeMushroomCheck.cs (Play stage, frames at 0 / 1 / 2 / 4 / 7 / 10 s), menu OpenZ/V1.4/Unit Editor.
    /// </summary>
    public sealed class OzUeMushroom : MonoBehaviour
    {
        /// <summary>On (default): the editor's NUKE shows this cloud in place of the levels' mushroom. Off: the levels' blast as in 1.3.</summary>
        public static bool On = true;
        public const string ShaderRes = "OpenZ/UnitEditor/OzUeNukeMushroom", ShaderName = "OpenZ/UeNukeMushroom";
        /// <summary>HDR gain on the cloud's colour: our URP ACES (OpenZ_PostFX) shows the source's colours as its own ACES did (fit over
        /// the whole cloud and life: mean error 7 of 255, tools/colour_match.py). At night it eases like the levels' fireball
        /// (x NukeFx.NightGainMul at NightK 1).</summary>
        public static float DisplayMatch = 2.7f;
        /// <summary>
        /// 28.3 m across at size 1.05), the same proportions and timing; the blast's rules and the ground dust ring do not read it.</summary>
        public const float CloudRingM = 12f * 1.18f;
        /// <summary>Checks only: a fixed size (0.9 .. 1.2) instead of the drop point's own; 0 = the point's own.</summary>
        public static float FixedSize = 0f;
        /// <summary>The parts of the levels' NukeFx this cloud replaces in the editor (renderers off, never destroyed).</summary>
        public static readonly string[] LevelsMushroomParts = { "FX_NukeCap", "FX_NukeStem", "NukeFireball", "NukeGlowShell" };
        /// <summary>The source's colours (hex ffffcc, ff8800, 2a2a2a) made linear as its renderer handed them to its shader.</summary>
        public static readonly Vector4 CoreLinear = new Vector4(1f, 1f, 0.603827f, 1f), EdgeLinear = new Vector4(1f, 0.246201f, 0f, 1f),
            SmokeLinear = new Vector4(0.023153f, 0.023153f, 0.023153f, 1f);
        public const float CapNoise = 0.6f, StemNoise = 0.9f, Distortion = 0.5f;

        /// <summary>The cloud on screen (checks); null when none.</summary>
        public static OzUeMushroom Current { get; private set; }
        /// <summary>Checks: clouds made, editor blasts that fell back to the levels' look (shader missing), the last blast's hidden
        /// levels' parts (of 4) and whether its puff cloud was left out.</summary>
        public static int Blasts, Fallbacks, LastHidden;
        public static bool LastPuffsSkipped;

        public float Age => age;
        public float Height => height;
        public float Size => size;
        public bool Ended => ended;
        public float GainNow => gainNow;
        public float GlowNow => Glow(age);
        public NukeFx LevelsBlast => nfx;
        public Transform Cap => cap;
        public Transform Stem => stem;
        public Renderer CapRenderer => capR;
        public Renderer StemRenderer => stemR;
        public Material CapMaterial => capMat;

        const float MinScale = 0.001f;
        static Shader shader; static bool warned;
        static Mesh capMesh, stemMesh;
        static readonly int CoreId = Shader.PropertyToID("_ColorCore"), EdgeId = Shader.PropertyToID("_ColorEdge"), SmokeId = Shader.PropertyToID("_ColorSmoke"),
            NoiseScaleId = Shader.PropertyToID("_NoiseScale"), DistortionId = Shader.PropertyToID("_Distortion"), OpacityId = Shader.PropertyToID("_Opacity"),
            ProgressId = Shader.PropertyToID("_Progress"), ShapeId = Shader.PropertyToID("_ShapeId"), TimeId = Shader.PropertyToID("_NukeTime"),
            GainId = Shader.PropertyToID("_Gain"), AtmoHoleId = Shader.PropertyToID("_AtmoHole"), StrengthId = Shader.PropertyToID("_Strength"),
            FadeId = Shader.PropertyToID("_Fade");

        NukeFx nfx; Transform cap, stem; Material capMat, stemMat; MeshRenderer capR, stemR;
        Light fireLight; Material shimmerMat;
        float age, height, size, gainNow; bool ended;

        /// <summary>The Unit editor's blast at the floor point `at` (OzUeNuke.Impact): the levels' NukeFx without its mushroom, plus this
        /// cloud. False (nothing done) when off or the shader is missing: the caller then runs NukeFx.Trigger itself, the levels' look.</summary>
        public static bool Blast(Vector3 at)
        {
            if (!On) return false;
            var sh = LoadShader();
            if (!sh)
            {
                Fallbacks++;
                if (!warned) { warned = true; Debug.LogWarning($"{OzUnitEditor.Tag} NUKE mushroom: the shader {ShaderName} (Resources/{ShaderRes}) is missing or not supported here: the levels' blast and mushroom instead"); }
                return false;
            }
            bool puffs = OpenZNukeMushroom.Enabled; var before = OpenZNukeMushroom.Current;
            NukeFx n;
            OpenZNukeMushroom.Enabled = false;                                                     // NukeFx.Build makes no puff cloud for this one blast
            try { n = NukeFx.Trigger(at); }
            finally { OpenZNukeMushroom.Enabled = puffs; }
            LastPuffsSkipped = OpenZNukeMushroom.Current == before;
            var go = new GameObject("UeNukeMushroom");
            go.AddComponent<OzUeMushroom>().Build(at, n, sh);
            return true;
        }

        static Shader LoadShader()
        {
            if (shader) return shader;
            var s = Resources.Load<Shader>(ShaderRes);
            if (!s) s = Shader.Find(ShaderName);
            if (s && s.isSupported) shader = s;
            return shader;
        }

        /// <summary>The drop point's own size in the source's range 0.9 .. 1.2 (a hash of the point: no draw from any random stream).</summary>
        public static float SizeAt(Vector3 at)
        {
            if (FixedSize > 0f) return Mathf.Clamp(FixedSize, C.SizeMin, C.SizeMax);
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(at.x * 10f) * 73856093u ^ (uint)Mathf.RoundToInt(at.z * 10f) * 19349663u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return C.SizeMin + (C.SizeMax - C.SizeMin) * ((h % 10007u) / 10006f);
            }
        }

        void Build(Vector3 at, NukeFx n, Shader sh)
        {
            nfx = n;
            size = SizeAt(at);
            height = C.HeightFor(size, CloudRingM);
            transform.position = at + Vector3.up * (C.Lift * C.MetresPerUnit(height, size));
            cap = Part("UeMushroomCap", CapMesh(), sh, 0, CapNoise, out capMat, out capR);
            stem = Part("UeMushroomStem", StemMesh(), sh, 1, StemNoise, out stemMat, out stemR);
            LastHidden = HideLevelsMushroom(n);
            Apply(0f);
            Current = this; Blasts++;
            Debug.Log($"{OzUnitEditor.Tag} NUKE mushroom: size x{size:0.00}, {height:0.0} m high, cap {height * C.CapWidth:0.0} m wide at its widest (radius), "
                      + $"life {C.Life:0.0} s; the levels' cap, stem, fireball and glow shell hidden ({LastHidden} of {LevelsMushroomParts.Length}), their puff cloud {(LastPuffsSkipped ? "not made" : "MADE")}");
        }

        Transform Part(string name, Mesh mesh, Shader sh, int shape, float noise, out Material mat, out MeshRenderer r)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mat = new Material(sh) { name = "UeNukeMushroom_" + name + " (runtime)" };
            mat.SetVector(CoreId, CoreLinear); mat.SetVector(EdgeId, EdgeLinear); mat.SetVector(SmokeId, SmokeLinear);   // LINEAR (SetVector, never SetColor)
            mat.SetFloat(NoiseScaleId, noise); mat.SetFloat(DistortionId, Distortion); mat.SetFloat(ShapeId, shape);
            r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return go.transform;
        }

        /// <summary>Hides the levels' mushroom parts of this blast (renderers off) and finds its fire light and shimmer. Returns the parts hidden.</summary>
        int HideLevelsMushroom(NukeFx n)
        {
            if (!n) return 0;
            int k = 0;
            foreach (var name in LevelsMushroomParts)
            {
                var t = n.transform.Find(name); var r = t ? t.GetComponent<Renderer>() : null;
                if (r) { r.enabled = false; k++; }
            }
            var fl = n.transform.Find("NukeFireLight"); fireLight = fl ? fl.GetComponent<Light>() : null;
            var sm = n.transform.Find("NukeShimmer"); var sr = sm ? sm.GetComponent<Renderer>() : null; shimmerMat = sr ? sr.sharedMaterial : null;   // NukeFx's own runtime copy
            return k;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (ended) { if (!nfx || age > C.Life + 6f) Destroy(gameObject); return; }
            if (age >= C.Life) { End(); return; }
            Apply(age);
        }

        /// <summary>Places and feeds the cloud for its age t (the source's timeline, OzUeMushroomCurve).</summary>
        void Apply(float t)
        {
            float h = height, rise = C.Rise(t, h), r = Mathf.Max(MinScale, C.CapRadius(t, h)), w = Mathf.Max(MinScale, C.StemRadius(t, h));
            cap.localPosition = new Vector3(0f, rise, 0f);
            cap.localScale = new Vector3(r, Mathf.Max(MinScale, r * C.Flatten(t)), r);
            cap.localRotation = Quaternion.Euler(0f, -C.CapTurn(t) * Mathf.Rad2Deg, 0f);              // the source's turn about its right-handed y, seen the same way round
            bool stemOn = C.StemShown(t);
            if (stem.gameObject.activeSelf != stemOn) stem.gameObject.SetActive(stemOn);
            stem.localPosition = new Vector3(0f, rise * 0.5f, 0f);
            stem.localScale = new Vector3(w, C.StemHeight(t, h), w);
            float p = C.Progress(t);
            gainNow = DisplayMatch * Mathf.Lerp(1f, NukeFx.NightGainMul, NightGrade.NightK);
            Set(capMat, C.CapTime(t), p, C.Opacity(t), 1f);
            Set(stemMat, C.StemTime(t), p, C.StemOpacity(t), C.StemFade(t));
        }

        void Set(Material m, float time, float p, float op, float fade)
        {
            m.SetFloat(TimeId, time); m.SetFloat(ProgressId, p); m.SetFloat(OpacityId, op); m.SetFloat(GainId, gainNow); m.SetFloat(FadeId, fade);
        }

        /// <summary>How brightly the cloud glows at age t, 1 .. 0: the shader's cooling (1 - smoothstep(0.5, 1, p)) squared, times its
        /// opacity against the base. The levels' fire light and heat shimmer of this blast follow it.</summary>
        public static float Glow(float t)
        {
            float p = C.Progress(t), d = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, p));
            return Mathf.Clamp01(d * d * Mathf.Clamp01(C.Opacity(t) / C.BaseOpacity));
        }

        void LateUpdate()
        {
            if (!nfx) return;                                                                         // the levels' blast ended: its OnDestroy cleared the fog hole
            float k = ended ? 0f : Glow(age);
            if (fireLight) { fireLight.intensity *= k; fireLight.enabled = fireLight.intensity > 0.05f; }   // NukeFx sets it afresh in every Update
            if (shimmerMat) shimmerMat.SetFloat(StrengthId, shimmerMat.GetFloat(StrengthId) * k);
            var hole = Vector4.zero;
            if (!ended && cap)
            {
                float r = cap.lossyScale.x;                                                           // the unit sphere's radius in metres
                if (r > 0.5f) { var c = cap.position; hole = new Vector4(c.x, c.y, c.z, r * 0.9f); }
            }
            Shader.SetGlobalVector(AtmoHoleId, hole);                                                 // the fog stops at this cloud's front (NukeFx set it on its hidden cap)
        }

        void End()
        {
            ended = true;
            if (capR) capR.enabled = false;
            if (stemR) stemR.enabled = false;
            if (nfx) Shader.SetGlobalVector(AtmoHoleId, Vector4.zero);
            Debug.Log($"{OzUnitEditor.Tag} NUKE mushroom faded out after {age:0.0} s");
            if (!nfx) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (!ended && nfx && NukeFx.Current == nfx) Shader.SetGlobalVector(AtmoHoleId, Vector4.zero);
            if (capMat) Destroy(capMat);
            if (stemMat) Destroy(stemMat);
        }

        // ------------------------------------------------------------------------------------------------ the meshes (built once)
        static Mesh CapMesh() { if (!capMesh) capMesh = Sphere(160, 120); return capMesh; }
        static Mesh StemMesh() { if (!stemMesh) stemMesh = Cylinder(1f, 0.8f, 1f, 96, 48); return stemMesh; }

        /// <summary>The source's unit sphere (width and height segments as given), z turned over into Unity's axes.</summary>
        static Mesh Sphere(int ws, int hs)
        {
            var v = new List<Vector3>((ws + 1) * (hs + 1)); var n = new List<Vector3>(v.Capacity); var tri = new List<int>(ws * hs * 6);
            for (int iy = 0; iy <= hs; iy++)
            {
                float th = (float)iy / hs * Mathf.PI;
                for (int ix = 0; ix <= ws; ix++)
                {
                    float phi = (float)ix / ws * Mathf.PI * 2f;
                    var p = new Vector3(-Mathf.Cos(phi) * Mathf.Sin(th), Mathf.Cos(th), -Mathf.Sin(phi) * Mathf.Sin(th));
                    v.Add(p); n.Add(p.normalized);
                }
            }
            for (int iy = 0; iy < hs; iy++)
                for (int ix = 0; ix < ws; ix++)
                {
                    int a = iy * (ws + 1) + ix + 1, b = iy * (ws + 1) + ix, c = (iy + 1) * (ws + 1) + ix, d = (iy + 1) * (ws + 1) + ix + 1;
                    if (iy != 0) { tri.Add(a); tri.Add(b); tri.Add(d); }
                    if (iy != hs - 1) { tri.Add(b); tri.Add(c); tri.Add(d); }
                }
            return Make("UeNukeMushroomCap", v, n, tri, 4f);
        }

        /// <summary>The source's open cylinder (top radius, foot radius, height, radial and height segments), z turned over.</summary>
        static Mesh Cylinder(float top, float foot, float h, int rs, int hs)
        {
            var v = new List<Vector3>((rs + 1) * (hs + 1)); var n = new List<Vector3>(v.Capacity); var tri = new List<int>(rs * hs * 6);
            float slope = (foot - top) / h;
            for (int iy = 0; iy <= hs; iy++)
            {
                float k = (float)iy / hs, r = k * (foot - top) + top;
                for (int ix = 0; ix <= rs; ix++)
                {
                    float th = (float)ix / rs * Mathf.PI * 2f, s = Mathf.Sin(th), c = Mathf.Cos(th);
                    v.Add(new Vector3(r * s, -k * h + h * 0.5f, -r * c));
                    n.Add(new Vector3(s, slope, -c).normalized);
                }
            }
            for (int ix = 0; ix < rs; ix++)
                for (int iy = 0; iy < hs; iy++)
                {
                    int a = iy * (rs + 1) + ix, b = (iy + 1) * (rs + 1) + ix, c = (iy + 1) * (rs + 1) + ix + 1, d = iy * (rs + 1) + ix + 1;
                    tri.Add(a); tri.Add(b); tri.Add(d); tri.Add(b); tri.Add(c); tri.Add(d);
                }
            return Make("UeNukeMushroomStem", v, n, tri, 3f);
        }

        static Mesh Make(string name, List<Vector3> v, List<Vector3> n, List<int> tri, float boundsSize)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * boundsSize);                            // the shader pushes vertices out (up to ~0.45): never culled early
            return m;
        }
    }
}
