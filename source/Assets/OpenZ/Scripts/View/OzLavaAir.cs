using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// Level 7: the air over the lava, near the camera only, all following the lava's temperature. Embers rising off the lava (40-80 live; the
    /// other embers on this level, OzVolcanicAir's, drift round the camera and are a different set), thin low wisps of smoke, a rare taller pale
    /// at most 6 quads of the game's own OpenZ/HeatDistort in its plume mode (the opaque texture is already on at every preset), each square to the camera
    /// with its foot on the lava, eased in and out over its life, on a lake of its own while a free lake is in view. LIGHTEST: half the particles, no hot air.
    /// Emits nothing it cannot be seen: a spot is drawn at random and tried against the view, as OzLava.OnScreen does it. No garbage per frame.
    /// </summary>
    public sealed class OzLavaAir
    {
        public const float EmberRate = 13f, WispRate = 2.6f, TallRate = 0.14f;
        public const int MaxShimmers = 6;
        /// <summary>Hot air: width and height (m), each a random pick; life (s), eased in over its first third and out over its last; the gap before the next one (s);
        /// how far apart two stand (m); the waver's cell (m), so a wide one boils as finely as a small one.</summary>
        public const float HazeWideMin = 9f, HazeWideMax = 16f, HazeTallMin = 5f, HazeTallMax = 9f, HazeLifeMin = 5f, HazeLifeMax = 9f,
            HazeGapMin = 0.8f, HazeGapMax = 3f, HazeApart = 8f, HazeCellM = 0.8f;
        /// <summary>Offset strength (OpenZ/HeatDistort _Strength, a share of the screen height) at the start camera's distance: 70 m up at pitch 55 is 85.5 m to the ground.
        /// </summary>
        public static float HazeStrength = 0.006f;
        public const float HazeRefDistance = 85.5f, HazeFarLeast = 0.6f, HazeNearMost = 1.5f;
        /// <summary></summary>
        public static bool ShimmerOn = true;
        /// <summary>Check only (OzLavaHazeCheck, the frame cost and the frames): a new one as soon as a slot is free, no gap.</summary>
        public static bool NoGapForCheck;

        const int Tries = 12;                                                      // the view holds 7-17 % of the lava spots at 70 m (counted on the real map): 12 tries find one 60-90 % of the time, 5 only 30-60 %
        readonly OzLava lava;
        const int HazeTries = 24, LakeSamples = 32;                                // spots of one lake tried for new hot air; spots per lake tried against the view, spread along it
        readonly Vector3[] spots, hot;
        readonly int[] spotLake, lakeIds, freeLakes, busyLakes;                    // each spot's lake (OzLava field id); the lakes that have spots; scratch for a pick
        readonly int[][] lakeSpots, lakeSample;                                    // per lake: its spots, a spread few of them
        float emberDue, wispDue, tallDue, shimmerDue;

        sealed class Shimmer { public Transform t; public Renderer r; public MaterialPropertyBlock b; public float age, life; public Vector3 at; public int lake = -1; }
        static readonly int AlphaId = Shader.PropertyToID("_Alpha"), StrengthId = Shader.PropertyToID("_Strength"), NoiseId = Shader.PropertyToID("_NoiseScale");
        readonly List<Shimmer> shimmers = new List<Shimmer>();
        Mesh quad; Material shimmerMat; GameObject root;

        /// <summary>Everything emitted so far (the check reads it).</summary>
        public int Embers { get; private set; }
        public int Wisps { get; private set; }
        public int Tall { get; private set; }
        public int ShimmersUp { get; private set; }
        public int HazeStarted { get; private set; }
        /// <summary>Check, read only: the spots and each one's lake; hot air slot i now (false = the slot is free).</summary>
        public int SpotCount => spots.Length;
        public Vector3 SpotAt(int i) => spots[i];
        public int LakeOfSpot(int i) => spotLake[i];
        public int HazeSlots => shimmers.Count;
        public bool HazeProbe(int i, out Vector3 foot, out float age, out float life, out Vector3 size, out int lake, out float strength, out Quaternion turn, out bool drawn)
        {
            foot = size = default; age = life = strength = 0f; lake = -1; turn = Quaternion.identity; drawn = false;
            if (i < 0 || i >= shimmers.Count || shimmers[i].life <= 0f) return false;
            var s = shimmers[i];
            foot = s.t.position; age = s.age; life = s.life; size = s.t.localScale; lake = s.lake; strength = s.b.GetFloat(StrengthId); turn = s.t.rotation; drawn = s.r.enabled;
            return true;
        }

        public OzLavaAir(OzLava lava, Vector3[] spots, Vector3[] hot)
        {
            this.lava = lava; this.spots = spots; this.hot = hot;
            spotLake = new int[spots.Length]; int lakes = 0, n = 0;
            for (int i = 0; i < spots.Length; i++)
            {   // each spot's lake as OzLava.GatherFields found it: a map lava tile by its field, any other spot by the drawn grid
                int id = lava.FieldOfTile(Mathf.FloorToInt(spots[i].x / 2f), Mathf.FloorToInt(-spots[i].z / 2f));
                if (id < 0) id = lava.FieldAt(spots[i].x, spots[i].z);
                spotLake[i] = id; if (id >= lakes) lakes = id + 1;
            }
            var count = new int[lakes];
            for (int i = 0; i < spots.Length; i++) if (spotLake[i] >= 0) count[spotLake[i]]++;
            lakeSpots = new int[lakes][]; lakeSample = new int[lakes][];
            for (int id = 0; id < lakes; id++) { lakeSpots[id] = new int[count[id]]; if (count[id] > 0) n++; count[id] = 0; }
            for (int i = 0; i < spots.Length; i++) { int id = spotLake[i]; if (id >= 0) lakeSpots[id][count[id]++] = i; }
            lakeIds = new int[n]; freeLakes = new int[n]; busyLakes = new int[n]; n = 0;
            for (int id = 0; id < lakes; id++)
            {
                var all = lakeSpots[id]; int m = Mathf.Min(LakeSamples, all.Length); var some = new int[m];
                for (int k = 0; k < m; k++) some[k] = all[(int)((k + 0.5f) * all.Length / m)];
                lakeSample[id] = some; if (m > 0) lakeIds[n++] = id;
            }
        }

        public void Destroy()
        {
            if (root) Object.Destroy(root);
            if (quad) Object.Destroy(quad);
            if (shimmerMat) Object.Destroy(shimmerMat);
        }

        bool Pick(Vector3[] from, out Vector3 at)
        {
            at = default;
            if (from.Length == 0) return false;
            for (int i = 0; i < Tries; i++)
            {
                at = from[Random.Range(0, from.Length)];
                if (lava.NearAt(at)) return true;
            }
            return false;
        }

        public void Tick(float dt, float heat, bool lite)
        {
            var fx = OpenZFxPlayer.I; if (!fx || dt <= 0f) { Shimmers(dt, lite); return; }
            float k = heat * heat;                                                 // 0.52 .. 1.82: cool minutes are visibly emptier
            emberDue += EmberRate * Mathf.Clamp(k, 0.7f, 1.5f) * dt;
            wispDue += WispRate * k * dt;
            tallDue += TallRate * k * dt;
            while (emberDue >= 1f)
            {
                emberDue -= 1f;
                if (Pick(spots, out var at)) { at.x += Random.Range(-0.9f, 0.9f); at.z += Random.Range(-0.9f, 0.9f); fx.LavaEmber(at, 1); Embers++; }
            }
            while (wispDue >= 1f)
            {
                wispDue -= 1f;
                if (Pick(spots, out var at)) { at.x += Random.Range(-0.9f, 0.9f); at.z += Random.Range(-0.9f, 0.9f); fx.LavaSmoke(at + Vector3.up * 0.1f, 0.55f); Wisps++; }
            }
            while (tallDue >= 1f)
            {
                tallDue -= 1f;
                if (Pick(hot, out var at)) { fx.LavaSteam(at + Vector3.up * 0.15f, 1.5f); Tall++; }
            }
            Shimmers(dt, lite);
        }

        void Shimmers(float dt, bool lite)
        {
            bool on = ShimmerOn && !lite;
            if (on && !shimmerMat) Build();
            if (!shimmerMat) return;
            shimmerDue -= dt;
            var cam = Camera.main; int up = 0; Shimmer free = null;
            for (int i = 0; i < shimmers.Count; i++)
            {   // a live one lives out its life, in view or not: it eases out, it never stops short
                var s = shimmers[i];
                if (s.life > 0f && (s.age += dt) >= s.life) { s.life = 0f; s.lake = -1; s.r.enabled = false; }
                if (s.life > 0f) up++; else if (free == null) free = s;
            }
            if (on && dt > 0f && free != null && (shimmerDue <= 0f || NoGapForCheck))
            {
                if (PickHaze(out var at, out int lake))
                {   // a new one: its size, its life and the gap after it each a random pick, so it keeps no rhythm
                    shimmerDue = Random.Range(HazeGapMin, HazeGapMax);
                    free.at = at + Vector3.up * 0.1f; free.lake = lake; free.age = 0f; free.life = Random.Range(HazeLifeMin, HazeLifeMax);
                    float wide = Random.Range(HazeWideMin, HazeWideMax);
                    free.t.localScale = new Vector3(wide, Random.Range(HazeTallMin, HazeTallMax), 1f);
                    free.b.SetFloat(NoiseId, wide / HazeCellM);
                    up++; HazeStarted++;
                }
                else shimmerDue = 0.25f;                                               // no lava in view: look again shortly
            }
            for (int i = 0; i < shimmers.Count; i++)
            {
                var s = shimmers[i]; if (s.life <= 0f) continue;
                s.r.enabled = dt > 0f;                                                 // OpenZ/HeatDistort boils on _Time: in a pause it hides instead of moving on
                float d = cam ? Vector3.Distance(cam.transform.position, s.at) : HazeRefDistance;
                s.b.SetFloat(AlphaId, Ease(s.age / s.life));
                s.b.SetFloat(StrengthId, HazeStrength * Mathf.Clamp(HazeRefDistance / Mathf.Max(d, 1f), HazeFarLeast, HazeNearMost));
                s.r.SetPropertyBlock(s.b);
                s.t.position = s.at;
                if (cam) s.t.rotation = cam.transform.rotation;                        // square to the camera at any pitch; the foot stays on the lava
            }
            ShimmersUp = up;
        }

        /// <summary>Hot air's weight over its life, u = 0 .. 1: up over the first third, full, down over the last third, SmoothStep at both ends (not a sine).</summary>
        public static float Ease(float u) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) * 3f));

        /// <summary>A spot in view for new hot air: on a lake no hot air stands on while such a lake is in view, else on any lake in view; never within HazeApart of another.</summary>
        bool PickHaze(out Vector3 at, out int lake)
        {
            int nf = 0, nb = 0;
            for (int i = 0; i < lakeIds.Length; i++)
            {
                int id = lakeIds[i];
                if (!LakeInView(id)) continue;
                if (LakeBusy(id)) busyLakes[nb++] = id; else freeLakes[nf++] = id;
            }
            return nf > 0 ? OnLake(freeLakes, nf, out at, out lake) : OnLake(busyLakes, nb, out at, out lake);
        }

        bool LakeInView(int id) { var s = lakeSample[id]; for (int k = 0; k < s.Length; k++) if (lava.NearAt(spots[s[k]])) return true; return false; }
        bool LakeBusy(int id) { for (int i = 0; i < shimmers.Count; i++) if (shimmers[i].life > 0f && shimmers[i].lake == id) return true; return false; }

        bool Crowded(Vector3 p)
        {
            for (int i = 0; i < shimmers.Count; i++)
            {
                var s = shimmers[i]; if (s.life <= 0f) continue;
                float dx = s.at.x - p.x, dz = s.at.z - p.z; if (dx * dx + dz * dz < HazeApart * HazeApart) return true;
            }
            return false;
        }

        /// <summary>One of the n lakes at random, a spot of it in view and clear of the others; a lake that gives none is dropped and another tried.</summary>
        bool OnLake(int[] ids, int n, out Vector3 at, out int lake)
        {
            at = default; lake = -1;
            while (n > 0)
            {
                int j = Random.Range(0, n), id = ids[j]; var mine = lakeSpots[id];
                for (int i = 0; i < HazeTries; i++)
                {
                    var p = spots[mine[Random.Range(0, mine.Length)]];
                    if (lava.NearAt(p) && !Crowded(p)) { at = p; lake = id; return true; }
                }
                ids[j] = ids[--n];
            }
            return false;
        }

        void Build()
        {
            var hd = Resources.Load<Material>("OpenZ/FX/M_HeatDistort");
            if (!hd) { ShimmerOn = false; return; }
            shimmerMat = new Material(hd) { name = "LavaShimmer (runtime)" };
            shimmerMat.SetFloat("_Mode", 2f); shimmerMat.SetFloat("_Inner", 0.1f); shimmerMat.SetFloat("_NoiseScale", 8f); shimmerMat.SetFloat("_Speed", 1.4f); shimmerMat.SetFloat("_Strength", HazeStrength);
            shimmerDue = Random.Range(HazeGapMin, HazeGapMax);                         // the first one comes after a gap too
            quad = new Mesh { name = "OzLavaShimmer" };
            quad.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f) };
            quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            root = new GameObject("LAV_Shimmer"); root.transform.SetParent(lava.transform, false);
            for (int i = 0; i < MaxShimmers; i++)
            {
                var go = new GameObject("LAV_Shimmer_" + i); go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = shimmerMat; r.enabled = false;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                shimmers.Add(new Shimmer { t = go.transform, r = r, b = new MaterialPropertyBlock() });
            }
        }
    }
}
