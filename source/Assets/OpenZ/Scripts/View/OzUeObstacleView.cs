using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// obstacle of the playground (the rules: Sim/ZWorld.Playground.cs PlaceObstacle, the harbour-wreck rule). A box of the obstacle's sim
    /// footprint (3 x 3 tiles = 6 x 6 m) and 3 m tall, drawn by Shaders/OzUeHolo.shader in four parts: a faint light pool on the floor,
    /// the dark body, the faces turned away (faint), the faces turned to the camera (lit top, front gradient, 1 m grid, neon edges with one
    /// soft glow, corner dots, the slow scan line). A hit (its health went down) flashes the faces for 0.18 s; below 35 % it flickers (8 %
    /// of the frames at 55 %); destroyed (CombatView asks OzUeHooks.ObstacleDied instead of its stone burst) its top outline rises 2.5 m,
    /// grows 25 % and fades in 0.7 s with the game's electric fizzle. No collider: the camera's ground ray and the sim never see it
    /// (the sim has its own impassable tiles). The scan and the flicker stop while the game is paused.
    /// </summary>
    public sealed class OzUeObstacleView : MonoBehaviour
    {
        public ZObject Obj { get; private set; }
        public bool Dying => dieAt >= 0f;
        MeshRenderer[] parts;
        MaterialPropertyBlock mpb;
        int lastHealth; float hitAt = -10f, dieAt = -1f, life;
        Vector3 centre; Vector4 size;

        internal void Bind(ZObject o)
        {
            Obj = o;
            float w = o.WidthPix / 8f, d = o.HeightPix / 8f, h = OzUePlayground.ObstacleHeightM;
            centre = ZGame.PxToWorld(o.LocX + o.WidthPix * 0.5, o.LocY + o.HeightPix * 0.5); centre.y = 0f;
            size = new Vector4(w, h, d, 0f);
            transform.position = centre;
            var box = OzUeObstacleViews.BoxMesh(w, h, d); var pool = OzUeObstacleViews.PoolMesh(0.9f * Mathf.Max(w, d));
            var mats = OzUeObstacleViews.Materials();
            parts = new MeshRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(i == 0 ? "Pool" : i == 1 ? "Body" : i == 2 ? "Hidden" : "Seen");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = i == 0 ? pool : box;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mats != null ? mats[i] : null;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.enabled = mats != null;
                parts[i] = r;
            }
            mpb = new MaterialPropertyBlock();
            lastHealth = o.Health;
            Apply(0f, 1f, 0f);
        }

        void Apply(float hit, float alpha, float rise)
        {
            if (parts == null) return;
            mpb.SetVector(OzUeObstacleViews.BoxSizeId, size); mpb.SetVector(OzUeObstacleViews.BoxCentreId, centre);
            mpb.SetFloat(OzUeObstacleViews.HitId, hit); mpb.SetFloat(OzUeObstacleViews.AlphaId, alpha); mpb.SetFloat(OzUeObstacleViews.RiseId, rise);
            mpb.SetFloat(OzUeObstacleViews.SeedId, Obj != null ? Obj.Id * 0.37f % 1f : 0f);
            foreach (var r in parts) if (r) r.SetPropertyBlock(mpb);
        }

        /// <summary>CombatView's death of this obstacle: the hologram's own break-up (true = handled).</summary>
        internal bool BreakUp(Vector3 at)
        {
            if (Dying) return true;
            dieAt = Time.unscaledTime; life = 0f;
            AudioDirector.Fire("electro_fizzle", centre + Vector3.up * 1.5f, Obj != null ? Obj.Id : -1);
            Debug.Log($"{OzUnitEditor.Tag} obstacle #{(Obj != null ? Obj.Id : -1)} destroyed: the hologram breaks up");
            return true;
        }

        void LateUpdate()
        {
            OzUeObstacleViews.Frame();
            if (Obj == null) return;
            if (Dying)
            {
                life += Time.unscaledDeltaTime;
                float q = Mathf.Clamp01(life / 0.7f);
                Apply(0f, 1f - q, Mathf.Max(q, 0.001f));
                if (q >= 1f) { OzUeObstacleViews.Forget(this); Destroy(gameObject); }
                return;
            }
            float now = Time.unscaledTime;
            if (Obj.Health < lastHealth) hitAt = now;
            lastHealth = Obj.Health;
            float hit = Mathf.Clamp01(1f - (now - hitAt) / 0.18f);
            float low = Obj.MaxHealth > 0 ? (float)Obj.Health / Obj.MaxHealth : 1f;
            float alpha = low < 0.35f && Time.deltaTime > 0f && !OzMotion.Reduced && Random.value < 0.08f ? 0.55f : 1f;
            Apply(hit, alpha, 0f);
        }
    }

    /// <summary>The playground's obstacle views (one per holo obstacle) and the hologram's materials.</summary>
    public static class OzUeObstacleViews
    {
        public const string MaterialPath = "OpenZ/UnitEditor/M_UeHolo", ShaderName = "OpenZ/UeHolo";
        internal static readonly int HoloColorId = Shader.PropertyToID("_HoloColor"), HoloTimeId = Shader.PropertyToID("_HoloTime"), PartId = Shader.PropertyToID("_Part"),
            CullId = Shader.PropertyToID("_Cull"), BoxSizeId = Shader.PropertyToID("_BoxSize"), BoxCentreId = Shader.PropertyToID("_BoxCentre"),
            HitId = Shader.PropertyToID("_Hit"), AlphaId = Shader.PropertyToID("_Alpha"), RiseId = Shader.PropertyToID("_Rise"), SeedId = Shader.PropertyToID("_Seed");
        static readonly Dictionary<int, OzUeObstacleView> views = new Dictionary<int, OzUeObstacleView>();
        static Material[] mats; static bool matsTried;
        static float holoTime; static int frame = -1; static Color lastColor = new Color(-1f, 0f, 0f, 0f);
        static readonly Color HoloPale = new Color32(0xE6, 0xEF, 0xE9, 255);

        /// <summary>The hologram colour now: the floor style's hi colour mixed 42 % toward #E6EFE9 (spec 3.2 HOLO).</summary>
        public static Color HoloColor => Color.Lerp(OzUeBackdrop.HiColor, HoloPale, 0.42f);

        public static OzUeObstacleView Of(ZObject o) => o != null && views.TryGetValue(o.Id, out var v) && v ? v : null;
        public static int Count => views.Count;

        internal static OzUeObstacleView Add(ZObject o)
        {
            if (o == null) return null;
            var have = Of(o); if (have) return have;
            var go = new GameObject("UE_Obstacle_" + o.Id);
            var zod = GameObject.Find("ZodObjects"); if (zod) go.transform.SetParent(zod.transform, false);
            var v = go.AddComponent<OzUeObstacleView>();
            v.Bind(o);
            views[o.Id] = v;
            return v;
        }

        internal static void Remove(ZObject o)
        {
            if (o == null || !views.TryGetValue(o.Id, out var v)) return;
            views.Remove(o.Id);
            if (v) Object.Destroy(v.gameObject);
        }

        internal static void Forget(OzUeObstacleView v) { if (v && v.Obj != null && views.TryGetValue(v.Obj.Id, out var x) && x == v) views.Remove(v.Obj.Id); }

        /// <summary>OzUeHooks.ObstacleDied (CombatView): the hologram breaks up. False = no view (CombatView then plays its own burst).</summary>
        internal static bool Died(ZObject o, Vector3 at) { var v = Of(o); return v && v.BreakUp(at); }

        internal static void Reset()
        {
            views.Clear(); frame = -1; lastColor = new Color(-1f, 0f, 0f, 0f);
        }

        /// <summary>Once a frame (the first view's LateUpdate): the scan clock (game time: it stops while paused; none with reduced motion)
        /// and the hologram colour on the four materials.</summary>
        internal static void Frame()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            var m = Materials(); if (m == null) return;
            if (!OzMotion.Reduced) holoTime += Time.deltaTime;
            var c = HoloColor;
            foreach (var x in m) { x.SetFloat(HoloTimeId, holoTime); if (c != lastColor) x.SetColor(HoloColorId, c); }
            lastColor = c;
        }

        /// <summary>The four part materials (pool, body, hidden faces, seen faces) from the Resources material, drawn in that order.</summary>
        internal static Material[] Materials()
        {
            if (mats != null && mats[0]) return mats;
            if (matsTried && mats == null) return null;
            matsTried = true;
            var src = Resources.Load<Material>(MaterialPath);
            Shader sh = src && src.shader && src.shader.name == ShaderName ? src.shader : Shader.Find(ShaderName);
            if (!sh || !sh.isSupported) { Debug.LogError($"{OzUnitEditor.Tag} the hologram shader {ShaderName} is missing or not supported here: the obstacles are not drawn (their rules still work)"); mats = null; return null; }
            mats = new Material[4];
            for (int i = 0; i < 4; i++)
            {
                var m = src && src.shader == sh ? new Material(src) : new Material(sh);
                m.name = "UeHolo_" + (i == 0 ? "Pool" : i == 1 ? "Body" : i == 2 ? "Hidden" : "Seen");
                m.SetFloat(PartId, i);
                m.SetFloat(CullId, i == 0 ? (float)CullMode.Off : i == 2 ? (float)CullMode.Front : (float)CullMode.Back);
                m.renderQueue = (int)RenderQueue.Transparent + 38 + i;
                mats[i] = m;
            }
            return mats;
        }

        /// <summary>The box: five faces (no floor), each with its own coordinates in metres (uv0) and (width, height, face id) in uv1.</summary>
        internal static Mesh BoxMesh(float w, float h, float d)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv0 = new List<Vector4>(); var uv1 = new List<Vector4>(); var t = new List<int>();
            float x = w * 0.5f, z = d * 0.5f;
            // (centre, right, up, normal, face width, face height, id): right x up = minus the normal (clockwise seen from outside)
            Face(v, n, uv0, uv1, t, new Vector3(x, h * 0.5f, 0f), Vector3.forward * z, Vector3.up * (h * 0.5f), Vector3.right, d, h, 0);
            Face(v, n, uv0, uv1, t, new Vector3(-x, h * 0.5f, 0f), Vector3.back * z, Vector3.up * (h * 0.5f), Vector3.left, d, h, 1);
            Face(v, n, uv0, uv1, t, new Vector3(0f, h * 0.5f, z), Vector3.left * x, Vector3.up * (h * 0.5f), Vector3.forward, w, h, 2);
            Face(v, n, uv0, uv1, t, new Vector3(0f, h * 0.5f, -z), Vector3.right * x, Vector3.up * (h * 0.5f), Vector3.back, w, h, 3);
            Face(v, n, uv0, uv1, t, new Vector3(0f, h, 0f), Vector3.right * x, Vector3.forward * z, Vector3.up, w, d, 4);
            var m = new Mesh { name = "UeHoloBox" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv0); m.SetUVs(1, uv1); m.SetTriangles(t, 0);
            m.bounds = new Bounds(new Vector3(0f, h + 1.5f, 0f), new Vector3(w * 1.3f + 1f, h * 2f + 6f, d * 1.3f + 1f));   // the break-up rises 2.5 m and grows 25 %
            return m;
        }

        /// <summary>The floor pool: one quad of radius r, 2 cm over the floor (face id 5).</summary>
        internal static Mesh PoolMesh(float r)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv0 = new List<Vector4>(); var uv1 = new List<Vector4>(); var t = new List<int>();
            Face(v, n, uv0, uv1, t, new Vector3(0f, 0.02f, 0f), Vector3.right * r, Vector3.forward * r, Vector3.up, 2f * r, 2f * r, 5);
            var m = new Mesh { name = "UeHoloPool" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv0); m.SetUVs(1, uv1); m.SetTriangles(t, 0);
            m.bounds = new Bounds(Vector3.zero, new Vector3(2f * r + 1f, 1f, 2f * r + 1f));
            return m;
        }

        static void Face(List<Vector3> v, List<Vector3> n, List<Vector4> uv0, List<Vector4> uv1, List<int> t, Vector3 c, Vector3 right, Vector3 up, Vector3 normal, float fw, float fh, int id)
        {
            int b = v.Count;
            var corners = new[] { c - right - up, c - right + up, c + right + up, c + right - up };
            var uvs = new[] { new Vector2(0f, 0f), new Vector2(0f, fh), new Vector2(fw, fh), new Vector2(fw, 0f) };
            for (int i = 0; i < 4; i++) { v.Add(corners[i]); n.Add(normal); uv0.Add(new Vector4(uvs[i].x, uvs[i].y, 0f, 0f)); uv1.Add(new Vector4(fw, fh, id, 0f)); }
            t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }

        /// <summary>Counted check: the hologram material loads from Resources with its own shader, the shader is supported, the box mesh has
        /// its five faces with the right sizes.</summary>
        internal static int SelfCheck(StringBuilder log)
        {
            int f = 0;
            var src = Resources.Load<Material>(MaterialPath);
            f += OzUnitEditor.Line(log, src && src.shader && src.shader.name == ShaderName, $"hologram material Resources/{MaterialPath} with shader {ShaderName} ({(src ? (src.shader ? src.shader.name : "no shader") : "missing")})");
            var sh = Shader.Find(ShaderName);
            f += OzUnitEditor.Line(log, sh && sh.isSupported, $"hologram shader {ShaderName} {(sh ? (sh.isSupported ? "supported" : "NOT supported") : "missing")}");
            var m = BoxMesh(6f, 3f, 6f);
            bool ok = m.vertexCount == 20 && m.triangles.Length == 30;
            Object.DestroyImmediate(m);
            f += OzUnitEditor.Line(log, ok, "hologram box: 5 faces (no floor), 20 vertices, 10 triangles");
            return f;
        }
    }
}
