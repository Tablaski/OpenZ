using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    ///   dome   a real low dome rises out of the lava over its swell, skin stretching, cracks opening bright from the top (OpenZ/LavaDome)
    ///   ring   at the burst a flash, a ring running outward, then a dark scar that heals (OpenZ/LavaRing, quads lying on the lava)
    /// OzLava decides when; this class only runs the slots and tells OzLava the moment a dome bursts, so sound, droplets and smoke sit on it.
    /// Ages run on the lava's dt, so everything holds still in a pause. Only what is on screen goes into the draw. No garbage per frame.
    /// </summary>
    public sealed class OzLavaDomes
    {
        public const int MaxDomes = 32, MaxRings = 48;
        public const string DomeRes = "OpenZ/Lava/M_L07_Dome", RingRes = "OpenZ/Lava/M_L07_Ring";
        /// <summary>The ring runs this long at a burst, then the scar heals (s).</summary>
        public const float RingSeconds = 1.2f, ScarSeconds = 2.4f;

        struct Dome { public bool on, big, giant; public Vector3 at; public float r, age, swell, seed; }
        struct Ring { public bool on; public Vector3 at; public float r, age, ringLife, scarLife, strength; }

        static readonly int DomeId = Shader.PropertyToID("_OZDome"), RingId = Shader.PropertyToID("_OZRing");

        readonly OzLava lava;
        readonly Material domeMat, ringMat;
        readonly Mesh domeMesh, quad;
        readonly Dome[] domes = new Dome[MaxDomes];
        readonly Ring[] rings = new Ring[MaxRings];
        readonly Matrix4x4[] domeM = new Matrix4x4[MaxDomes], ringM = new Matrix4x4[MaxRings];
        readonly Vector4[] domeD = new Vector4[MaxDomes], ringD = new Vector4[MaxRings];
        readonly MaterialPropertyBlock domeB = new MaterialPropertyBlock(), ringB = new MaterialPropertyBlock();

        /// <summary>Slots in use now; the most ever; the bursts so far; those a full pool turned away.</summary>
        public int LiveDomes { get; private set; }
        public int LiveRings { get; private set; }
        public int MostDomes { get; private set; }
        public int Bursts { get; private set; }
        public int Refused { get; private set; }

        public OzLavaDomes(OzLava lava)
        {
            this.lava = lava;
            domeMat = Resources.Load<Material>(DomeRes); ringMat = Resources.Load<Material>(RingRes);
            if (!domeMat || !ringMat) Debug.LogWarning("[OpenZ] lava: M_L07_Dome or M_L07_Ring missing in Resources/OpenZ/Lava: bubbles drawn without domes and rings");
            // no instancing (a batch run with no graphics) or a material without it: DrawMeshInstanced would throw every frame and stop the kits after this one
            if (!SystemInfo.supportsInstancing || (domeMat && !domeMat.enableInstancing)) domeMat = null;
            if (!SystemInfo.supportsInstancing || (ringMat && !ringMat.enableInstancing)) ringMat = null;
            domeMesh = BuildDome(); quad = BuildQuad();
        }

        public void Destroy()
        {
            if (domeMesh) Object.Destroy(domeMesh);
            if (quad) Object.Destroy(quad);
        }

        /// <summary>A dome starts to swell; false when the pool is full (the caller then bursts it at once).</summary>
        public bool Swell(Vector3 at, float r, float seconds, bool big, bool giant)
        {
            for (int i = 0; i < MaxDomes; i++)
            {
                if (domes[i].on) continue;
                domes[i] = new Dome { on = true, big = big, giant = giant, at = at, r = r, age = 0f, swell = seconds, seed = Random.value };
                return true;
            }
            Refused++; return false;
        }

        /// <summary>A ring on the lava: radius in m, its run in s, then the scar's healing in s (0 = no scar), strength 0..1.</summary>
        public void AddRing(Vector3 at, float r, float ringLife, float scarLife, float strength)
        {
            int oldest = 0; float most = -1f;
            for (int i = 0; i < MaxRings; i++)
            {
                if (!rings[i].on) { oldest = i; most = float.MaxValue; break; }
                float left = rings[i].age / Mathf.Max(0.01f, rings[i].ringLife + rings[i].scarLife);
                if (left > most) { most = left; oldest = i; }
            }
            rings[oldest] = new Ring { on = true, at = at, r = r, age = 0f, ringLife = Mathf.Max(0.05f, ringLife), scarLife = scarLife, strength = strength };
        }

        public void Tick(float dt)
        {
            float heat = lava.HeatNow; int nd = 0, live = 0;
            for (int i = 0; i < MaxDomes; i++)
            {
                if (!domes[i].on) continue;
                domes[i].age += dt;
                if (domes[i].age >= domes[i].swell)
                {
                    domes[i].on = false; Bursts++;
                    lava.DomeBurst(domes[i].at, domes[i].r, domes[i].big, domes[i].giant);
                    continue;
                }
                live++;
                if (!domeMat || !lava.SeenAt(domes[i].at)) continue;
                float u = domes[i].age / domes[i].swell, e = 1f - (1f - u) * (1f - u);      // quick at first, then it stretches
                float wide = domes[i].r * (0.62f + 0.38f * e), high = domes[i].r * (0.14f + 0.62f * e);
                float under = high * Mathf.Max(0f, 1f - u * 5f);                            // the first fifth of the swell it comes up out of the lava, so no dome appears in one frame
                domeM[nd] = Matrix4x4.TRS(domes[i].at + Vector3.down * under, Quaternion.identity, new Vector3(wide, high, wide));
                domeD[nd] = new Vector4(u, domes[i].seed, heat, 0f);
                nd++;
            }
            LiveDomes = live; if (live > MostDomes) MostDomes = live;
            if (nd > 0)
            {
                domeB.SetVectorArray(DomeId, domeD);
                Graphics.DrawMeshInstanced(domeMesh, 0, domeMat, domeM, nd, domeB, ShadowCastingMode.Off, false);
            }
            int nr = 0; live = 0;
            for (int i = 0; i < MaxRings; i++)
            {
                if (!rings[i].on) continue;
                rings[i].age += dt;
                if (rings[i].age >= rings[i].ringLife + rings[i].scarLife) { rings[i].on = false; continue; }
                live++;
                if (!ringMat || !lava.SeenAt(rings[i].at)) continue;
                float s = rings[i].r;
                ringM[nr] = Matrix4x4.TRS(rings[i].at + Vector3.up * 0.015f, Quaternion.identity, new Vector3(s, 1f, s));
                ringD[nr] = new Vector4(rings[i].age, rings[i].ringLife, rings[i].scarLife, rings[i].strength);
                nr++;
            }
            LiveRings = live;
            if (nr > 0)
            {
                ringB.SetVectorArray(RingId, ringD);
                Graphics.DrawMeshInstanced(quad, 0, ringMat, ringM, nr, ringB, ShadowCastingMode.Off, false);
            }
        }

        /// <summary>A hemisphere of radius 1 on y = 0: 14 sides, 4 bands, 56 triangles.</summary>
        static Mesh BuildDome()
        {
            const int Sides = 14, Bands = 4;
            var v = new Vector3[Sides * Bands + 1]; var n = new Vector3[v.Length]; var t = new int[(Sides + (Bands - 1) * Sides * 2) * 3]; int w = 0;
            for (int b = 0; b < Bands; b++)
            {
                float p = b / (float)Bands * Mathf.PI * 0.5f;
                for (int s = 0; s < Sides; s++)
                {
                    float a = s / (float)Sides * Mathf.PI * 2f;
                    v[b * Sides + s] = new Vector3(Mathf.Cos(a) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Sin(a) * Mathf.Cos(p));
                }
            }
            v[Sides * Bands] = Vector3.up;
            for (int i = 0; i < v.Length; i++) n[i] = v[i].normalized;
            for (int b = 0; b < Bands - 1; b++)
                for (int s = 0; s < Sides; s++)
                {
                    int s2 = (s + 1) % Sides, a = b * Sides + s, c = b * Sides + s2, d = (b + 1) * Sides + s, e = (b + 1) * Sides + s2;
                    t[w++] = a; t[w++] = d; t[w++] = c; t[w++] = c; t[w++] = d; t[w++] = e;
                }
            for (int s = 0; s < Sides; s++) { t[w++] = (Bands - 1) * Sides + s; t[w++] = Sides * Bands; t[w++] = (Bands - 1) * Sides + (s + 1) % Sides; }
            var m = new Mesh { name = "OzLavaDome" }; m.vertices = v; m.normals = n; m.triangles = t; m.bounds = new Bounds(Vector3.up * 0.5f, Vector3.one * 2.2f); return m;
        }

        /// <summary>A flat quad of half size 1 lying on y = 0, front face up.</summary>
        static Mesh BuildQuad()
        {
            var m = new Mesh { name = "OzLavaRing" };
            m.vertices = new[] { new Vector3(-1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, -1f) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.uv = new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.bounds = new Bounds(Vector3.zero, new Vector3(2.2f, 0.2f, 2.2f));
            return m;
        }
    }
}
