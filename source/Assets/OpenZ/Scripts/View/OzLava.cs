using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Level 7 "Pyro Technics": the lava, view side (the rule is Sim/ZWorld.Lava.cs, DV152). Boots itself from ZGame on any map with lava.
    ///   surface  one flat mesh over every lava tile of the map (Map.lava), at <see cref="lavaY"/> (or the y of a scene object named
    ///            MRK_lava_level), its outer rim pushed 0.35 m under the bank so no gap shows; vertex colour r = 0 at the edge .. 1 inside,
    ///            g = hot pool; material OpenZ/Lava (Shaders/OpenZLava.shader: drifting crust plates, HDR molten channels for the bloom,
    ///            slow breathing, pops). No shadows cast; it receives the sun's shadows.
    ///   pops     OzLavaPops decides when and where (a wandering rate: busy minutes and quiet minutes, never a rhythm). Every pop is drawn by
    ///            the shader (8 slots, global _OZLavaPops) and has its sound (lava_pop_small / lava_pop_big); a big one on screen also throws
    ///            a little fire puff and a short light flash (FxManager).
    ///   light    one warm point light per lava field (up to MaxLights; brighter over hot pools), flickering softly, day and night: the
    ///            lava lights the ground, the rocks and the units near it.
    ///   robots   every robot's glow follows its sim heat (ZObject.LavaHeat -> TeamPaint.Heat -> TeamLit _OZHeat, strongest at _OZLavaY);
    ///            the sounds of each stage: lava_sizzle_enter (steps in), lava_burn_tick (each second inside), lava_critical (heat 0.75),
    ///            lava_steam_out (steps out), lava_melt (dies in it; the fire death cry plays as for any melt) and the robot's lines
    ///            vo_lava_order / vo_lava_critical / vo_lava_out (the player's own robots, a few seconds apart). The melt itself:
    ///            UnitView.LavaMelt.cs.
    /// View only: nothing here writes sim state. Its own random numbers (OzLavaPops); Unity's Random only for little FX jitter.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public partial class OzLava : MonoBehaviour
    {
        public static OzLava I { get; private set; }
        [Tooltip("lava surface height (m); a scene object named MRK_lava_level overrides it")] public float lavaY = -0.30f;
        [Tooltip("how far the sheet reaches under the bank (m)")] public float edgeGrow = 0.35f;
        public int seed = 1907;
        public bool playSounds = true, verbose;
        public const int MaxLights = 10;
        /// <summary>The lava material (Resources), made once by the editor menu OpenZ/L07 Lava/Make lava material.</summary>
        public const string MaterialRes = "OpenZ/Lava/M_L07_Lava";
        Material convertedMaterial;
        Texture2D convertedField;

        public const string SndPopSmall = "lava_pop_small", SndPopBig = "lava_pop_big", SndEnter = "lava_sizzle_enter", SndTick = "lava_burn_tick",
            SndCritical = "lava_critical", SndOut = "lava_steam_out", SndMelt = "lava_melt", SndBed = "amb_l07_lava_bed",
            VoOrder = "vo_lava_order", VoCritical = "vo_lava_critical", VoOut = "vo_lava_out";
        /// <summary>Seconds between two lava lines of the player's robots (any of the three pools).</summary>
        public const float VoGap = 6f;

        /// <summary>Proof / counted check: pops this session, the time of the last one, the lava tiles drawn.</summary>
        public static int PopsThisSession, TilesDrawn; public static float LastPopAt = -1f;

        ZGame game; ZMapData map; Camera cam;
        OzLavaPops pops; readonly List<OzLavaPops.Pop> fresh = new List<OzLavaPops.Pop>();
        static readonly int LavaYId = Shader.PropertyToID("_OZLavaY");
        readonly List<(Light l, float baseI, float baseR, float phase, int field)> lights = new List<(Light, float, float, float, int)>();
        readonly Dictionary<int, TeamPaint> paints = new Dictionary<int, TeamPaint>();
        readonly HashSet<int> warm = new HashSet<int>(); readonly List<int> cooled = new List<int>();
        readonly Dictionary<int, float> tickAt = new Dictionary<int, float>();
        float lastVo = -100f;
        GameObject sheet;

        void Awake() { I = this; }
        void OnDestroy()
        {
            if (map != null) { Lv4Down(); Lv3Down(); Lv2Down(); Lv1Down(); }
            if (I == this) I = null; if (sheet) Destroy(sheet); foreach (var x in lights) if (x.l) Destroy(x.l.gameObject);
            if (convertedMaterial) Destroy(convertedMaterial);
            if (convertedField) Destroy(convertedField);
            if (AudioDirector.I) for (int i = 0; i < bedCount; i++) AudioDirector.I.StopLoop(BedKey + i);
        }
        const string BedKey = "l07_lava_bed_";
        // the seams of the lava kits (LV_CONTRACT.md): each hook has one body, in its kit's own file; one with no body yet compiles to nothing
        partial void Lv1Boot(); partial void Lv2Boot(); partial void Lv3Boot(); partial void Lv4Boot(); partial void Lv5Boot();
        partial void Lv1Tick(float dt, float t); partial void Lv2Tick(float dt, float t); partial void Lv3Tick(float dt, float t); partial void Lv4Tick(float dt, float t);
        partial void Lv1Down(); partial void Lv2Down(); partial void Lv3Down(); partial void Lv4Down();

        void Start()
        {
            game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!game || game.World == null || !game.World.Map.HasLava) { enabled = false; return; }
            map = game.World.Map; cam = Camera.main;
            var mark = GameObject.Find("MRK_lava_level"); if (mark) lavaY = mark.transform.position.y;
            Shader.SetGlobalFloat(LavaYId, lavaY);
            Lv1Boot();
            BuildSheet();
            var tiles = new List<(int, int, int)>();
            for (int ty = 0; ty < map.height; ty++) for (int tx = 0; tx < map.width; tx++) { int k = map.LavaKind(tx, ty); if (k > 0) tiles.Add((tx, ty, k)); }
            pops = new OzLavaPops(tiles, seed);
            BuildLights();
            StartBeds();
            Lv2Boot(); Lv3Boot(); Lv4Boot(); Lv5Boot();
            Debug.Log($"[OpenZ] lava: {TilesDrawn} tiles at y {lavaY:F2}, {lights.Count} glow lights, rule DV152 {(ZWorld.LavaDeviation ? "ON" : "off")}");
        }

        // ------------------------------------------------------------------ the sheet
        void BuildSheet()
        {
            int W = map.width, H = map.height; TilesDrawn = 0;                                             // (a static: a second level start counts afresh)
            // Generated slopes reach further into their basins than the authored L07 banks.
            float shoreOverlap = OzVolcanic.Is(game.level) ? edgeGrow : .85f;
            var index = new Dictionary<int, int>(); var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            int Corner(int cx, int cy)
            {
                int key = cy * (W + 1) + cx;
                if (index.TryGetValue(key, out int vi)) return vi;
                // the four tiles round this corner: inside (all lava) or edge; the rim is pushed out under the bank
                int lava = 0, hot = 0; Vector2 away = Vector2.zero;
                for (int dy = -1; dy <= 0; dy++)
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        int k = map.LavaKind(cx + dx, cy + dy);
                        var toCorner = new Vector2(-(dx * 2 + 1), -(dy * 2 + 1));                        // tile centre -> corner, tile units
                        if (k > 0) { lava++; away += toCorner; }
                        if (k == 2) hot++;
                    }
                var p = new Vector3(cx * 2f, lavaY, -cy * 2f);
                if (lava < 4 && away.sqrMagnitude > 1e-4f) { away.Normalize(); p.x += away.x * shoreOverlap; p.z -= away.y * shoreOverlap; }
                vi = verts.Count; verts.Add(p); cols.Add(new Color(lava == 4 ? 1f : 0f, hot > 0 ? Mathf.Clamp01(hot / 2f) : 0f, CornerField(cx, cy), 1f));   // b = field id, a = reads the wake
                index[key] = vi; return vi;
            }
            for (int ty = 0; ty < H; ty++)
                for (int tx = 0; tx < W; tx++)
                {
                    if (!map.LavaAt(tx, ty)) continue;
                    int v00 = Corner(tx, ty), v10 = Corner(tx + 1, ty), v01 = Corner(tx, ty + 1), v11 = Corner(tx + 1, ty + 1);
                    tris.Add(v00); tris.Add(v10); tris.Add(v11);                                         // clockwise seen from above (front face up)
                    tris.Add(v00); tris.Add(v11); tris.Add(v01);
                    TilesDrawn++;
                }
            var mesh = new Mesh { name = "OzLavaSheet" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            var n = new Vector3[verts.Count]; for (int i = 0; i < n.Length; i++) n[i] = Vector3.up; mesh.normals = n;
            mesh.RecalculateBounds();
            sheet = new GameObject("LAV_Lava");
            sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = sheet.AddComponent<MeshRenderer>();
            var mat = Resources.Load<Material>(MaterialRes);                                             // made by the editor menu OpenZ/L07 Lava/Make lava material (it keeps the shader in the build)
            if (!mat)
            {
                var sh = Shader.Find("OpenZ/Lava");
                if (!sh) { Debug.LogWarning("[OpenZ] lava: shader OpenZ/Lava missing (run OpenZ/L07 Lava/Make lava material)"); sh = Shader.Find("Universal Render Pipeline/Unlit"); }
                mat = new Material(sh) { name = "M_L07_Lava" };
            }
            mr.sharedMaterial = OzVolcanic.Is(game.level) ? mat : MaterialForGrid(mat);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = true;
        }

        Material MaterialForGrid(Material baseMaterial)
        {
            // Preserve the real lava textures and derive flow and bank distance from this map.
            const int pixelsPerTile = 4;
            int w = map.width * pixelsPerTile, h = map.height * pixelsPerTile;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float tx = (x + .5f) / pixelsPerTile, ty = (h - y - .5f) / pixelsPerTile;
                int ix = (int)tx, iy = (int)ty;
                float bank = 1;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (map.LavaAt(ix + dx, iy + dy)) continue;
                    float a = Mathf.Max(0, Mathf.Abs(tx - (ix + dx + .5f)) - .5f);
                    float b = Mathf.Max(0, Mathf.Abs(ty - (iy + dy + .5f)) - .5f);
                    bank = Mathf.Min(bank, Mathf.Sqrt(a * a + b * b) * 2);
                }
                float flow = Mathf.PerlinNoise(tx * .17f, ty * .17f);
                pixels[y * w + x] = new Color(.31f + flow * .27f, .65f - flow * .21f, bank, .28f);
            }
            convertedField = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
                { name = "Converted lava field", wrapMode = TextureWrapMode.Clamp };
            convertedField.SetPixels32(pixels);
            convertedField.Apply(false, true);
            convertedMaterial = new Material(baseMaterial);
            convertedMaterial.SetTexture("_FieldMap", convertedField);
            convertedMaterial.SetVector("_FieldRect", new Vector4(0, -map.height * 2, .5f / map.width, .5f / map.height));
            if (map.aiMap && Experimental.OzAiTerrainV5.Active && Experimental.OzAiTerrainV4.Active(map.planet))
            {
                convertedMaterial.SetFloat("_OBase", convertedMaterial.GetFloat("_OBase") * .66f);
                convertedMaterial.SetFloat("_OHot", convertedMaterial.GetFloat("_OHot") + .10f);
            }
            return convertedMaterial;
        }

        // ------------------------------------------------------------------ the glow lights (one per lava field)
        // (now in OzLava.Alive.cs: split fields, heat, night)

        // ------------------------------------------------------------------ every frame
        void Update()
        {
            if (game == null || game.World == null) return;
            float t = Time.timeSinceLevelLoad;
            Flicker();
            float dt = game.paused ? 0f : Time.deltaTime;   // every lava clock stops with the game's pause
            Lv1Tick(dt, t);
            Pops(dt, t);
            Robots(t);
            Events();
            Lv2Tick(dt, t); Lv3Tick(dt, t); Lv4Tick(dt, t);
        }

        void Pops(float dt, float t)
        {
            fresh.Clear(); pops.Step(dt, fresh);
            foreach (var p in fresh)
            {
                PopsThisSession++; LastPopAt = t;
                PopStart(p, true);                                                                                  // a dome swells now; its burst has the sound, droplets, ring (OzLava.Alive.cs)
                if (verbose) Debug.Log($"[OpenZ] lava pop {(p.big ? "big" : "small")} at ({p.x:F0}, {p.z:F0}), rate {pops.Rate:F2}/s");
            }
            PopsMore(dt);
        }

        bool OnScreen(Vector3 p)
        {
            if (!cam) cam = Camera.main; if (!cam) return false;
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.z < 160f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        void Robots(float t)
        {
            float dt = game.paused ? 0f : Time.deltaTime;
            var objs = game.World.Objects;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (!o.IsRobot || (o.LavaHeat <= 0 && !warm.Contains(o.Id))) continue;
                if (o.Destroyed || o.KillAt >= 0) continue;                                                         // dying: UnitView.LavaMelt owns its glow now
                var tp = Paint(o.Id);
                float simHeat = (float)o.LavaHeat;
                if (tp) tp.Heat = Mathf.MoveTowards(tp.Heat, simHeat, dt * Mathf.Max(1f, game.gameSpeed) * (simHeat >= tp.Heat ? HeatRise : HeatFall));   // the sim's heat is a staircase, one bite a sim second: eased, so the bites never show
                if (o.LavaHeat > 0 || (tp && tp.Heat > 0f)) warm.Add(o.Id); else { warm.Remove(o.Id); tickAt.Remove(o.Id); }
                if (o.InLava && playSounds)
                {
                    if (!tickAt.TryGetValue(o.Id, out float due)) tickAt[o.Id] = due = t + 1f;
                    if (t >= due) { tickAt[o.Id] = t + 1f; AudioDirector.Fire(SndTick, game.CenterWorld(o), o.Id); }
                }
            }
            cooled.Clear();
            foreach (var id in warm) { var o = game.World.Get(id); if (o == null) cooled.Add(id); }                  // gone (in a vehicle, deleted): forget it
            foreach (var id in cooled) { warm.Remove(id); tickAt.Remove(id); paints.Remove(id); }
        }

        const float HeatRise = (float)(1.0 / (ZWorld.LavaMeltBites * ZWorld.LavaBiteEvery)), HeatFall = (float)(1.0 / ZWorld.LavaCoolSeconds);

        TeamPaint Paint(int id)
        {
            if (paints.TryGetValue(id, out var tp) && tp) return tp;
            tp = game.Views.TryGetValue(id, out var uv) && uv ? uv.GetComponentInChildren<TeamPaint>() : null;
            if (tp) paints[id] = tp;
            return tp;
        }

        void Events()
        {
            var ev = game.World.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                if (e.type == ZEventType.LavaStep)
                {
                    var o = game.World.GetAny(e.objId); if (o == null) continue;
                    var at = new Vector3(e.x / 8f, lavaY, -e.y / 8f);
                    bool mine = o.Owner == game.player && !o.IsMinion;
                    if (e.radius == 1) { Sfx(SndEnter, at, o.Id); if (mine) Vo(VoOrder, at, o.Id); }
                    else if (e.radius == 2) { Sfx(SndCritical, at, o.Id); if (o.Owner == game.player) Vo(VoCritical, at, o.Id); }
                    else if (e.radius == 0) { Sfx(SndOut, at, o.Id); if (mine && e.value > 0.3f) Vo(VoOut, at, o.Id); }
                }
                else if (e.type == ZEventType.Death)
                {
                    var o = game.World.GetAny(e.objId);
                    if (o != null && (o.LavaKilled || o.InLava)) Sfx(SndMelt, new Vector3(e.x / 8f, lavaY, -e.y / 8f), o.Id);   // a robot shot dead in lava melts too (fix 11)
                }
            }
        }

        /// <summary>A robot's line at the order was just said (View/Voice/AudioDirector.LavaLines.cs): the step-in line keeps its gap from it.</summary>
        public static void OrderLineSaid() { if (I) I.lastVo = Time.unscaledTime; }

        void Sfx(string id, Vector3 at, int emitter) { if (playSounds) AudioDirector.Fire(id, at, emitter); }
        void Vo(string id, Vector3 at, int emitter)
        {
            float t = Time.unscaledTime;
            if (!playSounds || t - lastVo < VoGap) return;
            lastVo = t; AudioDirector.Fire(id, at, emitter);
        }
    }
}
