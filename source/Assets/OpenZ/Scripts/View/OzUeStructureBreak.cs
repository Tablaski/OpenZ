using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// round puffs and plain pale cubes). The Unit editor's two ramp platforms (PLATFORM, TOWER) now COLLAPSE into their own pieces:
    /// <list type="bullet">
    /// <item>the deck and the ramp break into jagged concrete slabs (a jittered fracture grid, shared jagged crack lines, 0.6 / 0.5 m thick), the
    /// side walls and the back wall into wall panels, the kerb into gunmetal lengths with the holo edge line; every top face keeps the
    /// platform's own material and texture coordinates (the joints and the ramp's chevrons line up with the standing body), every broken face
    /// wears the concrete chunk library's fracture material in the platform's cream, plus modelled library chunks with rebar (M) and crumbs (S / XS);</item>
    /// <item>t = 0: the body is swapped for its pieces in place (the cracks open, dust jets out of them, a structure groan); the deck gives
    /// from the middle out, the walls tip outward from the top, the ramp sections slide down from the crest; real physics on the heavy
    /// debris layer (the slabs stack and settle on each other), the crumbs on the light one;</item>
    /// <item>the project's rolling fog dust (OzBlastFog's material: soft billows that fade into the floor): crack jets, a rising core plume,
    /// a base surge rolling out on the first heavy landing, a low lingering cloud; thin grey smoke over the rubble; friction sparks where the
    /// kerb tears and lands;</item>
    /// <item>sound on the events: groan + chunks at the crack, the long collapse at the give, the large crash + collapse on the first heavy
    /// landing, concrete knocks (budgeted) and metal clatter on landings, the settle at the end; a camera kick on that first landing, only
    /// on screen (OpenZFxShake.Kick), larger for the tower;</item>
    /// <item>no scorch and no crater (nothing burned or blew up here; the killing shot leaves its own mark); the rubble keeps the platform's
    /// colours, lies 30 s, then sinks into the floor; CLEAR FIELD, Delete and a new drop on it take it at once.</item>
    /// </list>
    /// What stood on it: the units die with it as before and fall (UnitView); a flag comes down with the deck (FlagView.DeckFall.cs).
    /// The holo obstacle: OzUeStructureBreak.Holo.cs. Switch <see cref="On"/> (on); off = the 1.3 sink, dust and stone burst exactly.
    /// View only: no rule, no sim state, nothing in any level (only the Unit editor makes these objects).
    /// </summary>
    public static partial class OzUeStructureBreak
    {
        public static bool On = true;
        /// <summary>Thicknesses (m): deck slab, ramp slab, wall panel; the crack gap; the fracture grid (tiles) and the jag (share of a cell).</summary>
        public static float DeckT = 0.6f, RampT = 0.5f, WallT = 0.5f, Gap = 0.035f, TopCell = 2.5f, WallCellU = 3f, WallCellV = 3.1f, Jag = 0.07f, Jitter = 0.22f;
        /// <summary>Rubble: seconds it lies, seconds it takes to sink; the most structure pieces alive at once (the oldest collapse sinks early).</summary>
        public static float RestSeconds = 30f, SinkSeconds = 3f;
        public static int MaxLivePieces = 320;
        /// <summary>Camera kick on the first heavy landing (px at 1080, on screen only): tower, platform.</summary>
        public static float KickTowerPx = 14f, KickMidPx = 8f;
        /// <summary>Library chunks per collapse (tower, platform): M with rebar (heavy), S / XS crumbs (light). Halved with WRECKS AND DEBRIS light.</summary>
        public static int ChunksMTower = 14, ChunksMMid = 8, CrumbsTower = 30, CrumbsMid = 18;
        /// <summary>Dust colour (the platform's cream concrete as dust), smoke colour, dust opacity scale.</summary>
        public static Color DustColor = new Color(0.74f, 0.70f, 0.62f), SmokeColor = new Color(0.46f, 0.45f, 0.43f), SparkColor = new Color(1f, 0.78f, 0.35f);
        public static float DustAlphaK = 1f, SmokeSize = 0.7f;

        // ------------------------------------------------------------------ proof
        public static int Collapses, PiecesMade, SlabsMade, WallPanelsMade, KerbsMade, ChunksMade, CrumbsMade, Landings, Knocks, Kicks, Sunk, Swept, Cleared, FogPuffs, OldPath;
        public static float LastBuildMs, MaxBuildMs;
        public static string LastReport = "-";
        static readonly List<OzUeStructureRun> runs = new List<OzUeStructureRun>();
        internal static readonly List<OzUeStructurePiece> live = new List<OzUeStructurePiece>();
        public static int LivePieces { get { live.RemoveAll(p => !p); return live.Count; } }

        internal static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static PhysicsMaterial concrete;
        internal static PhysicsMaterial ConcreteMat() => concrete ??= new PhysicsMaterial("UeStructureConcrete") { bounciness = 0.04f, dynamicFriction = 0.8f, staticFriction = 0.95f, bounceCombine = PhysicsMaterialCombine.Minimum, frictionCombine = PhysicsMaterialCombine.Maximum };
        static bool layersSet;
        internal static int Heavy { get { int l = LayerMask.NameToLayer("DebrisHeavy"); return l >= 0 ? l : BuildingBreaker.HeavyLayer; } }
        internal static int Light => BuildingBreaker.DebrisLayer;
        internal static void Layers()
        {
            if (layersSet) return;
            layersSet = true;
            Physics.IgnoreLayerCollision(Heavy, Light, true);                                                  // as BuildingBreaker: heavy stacks, crumbs meet the world only
        }

        // ------------------------------------------------------------------ the platform
        /// <summary>OzUePlatformView.Fall: the collapse instead of the 1.3 sink. False = not done (switch off, nothing to break): the old path runs.</summary>
        internal static bool Platform(OzUePlatformView v, GameObject body)
        {
            if (!On || !v || v.Obj == null || v.Info == null) { OldPath++; return false; }
            float t0 = Time.realtimeSinceStartup;
            OzUeStructureRun run = null;
            try
            {
                Layers();
                var go = new GameObject("OZ_UeStructureBreak_" + v.Obj.Id);
                run = go.AddComponent<OzUeStructureRun>();
                run.BuildPlatform(v);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{OzUnitEditor.Tag} platform #{v.Obj.Id} collapse: {e.Message} (the old fall plays)");
                if (run) { run.Drop(); Object.Destroy(run.gameObject); }
                OldPath++; return false;
            }
            if (body) body.SetActive(false);
            runs.Add(run); Collapses++;
            LastBuildMs = (Time.realtimeSinceStartup - t0) * 1000f; MaxBuildMs = Mathf.Max(MaxBuildMs, LastBuildMs);
            Cap();
            LastReport = run.Summary;
            Debug.Log($"{OzUnitEditor.Tag} platform #{v.Obj.Id} collapses: {run.Summary}, built in {LastBuildMs:0.0} ms");
            return true;
        }

        /// <summary>Too many pieces alive: the oldest collapse's resting pieces sink now.</summary>
        static void Cap()
        {
            live.RemoveAll(p => !p);
            int over = live.Count - MaxLivePieces;
            for (int i = 0; i < live.Count && over > 0; i++) if (live[i].resting && !live[i].Sinking) { live[i].Sink(0.6f); over--; }
        }

        // ------------------------------------------------------------------ the field's housekeeping
        /// <summary>CLEAR FIELD (OzUePlayground.ClearQuiet): every structure piece and every collapse runner gone at once. Returns the pieces taken.</summary>
        public static int ClearAll()
        {
            int n = 0;
            foreach (var p in live) if (p) { p.gameObject.SetActive(false); Object.Destroy(p.gameObject); n++; }
            live.Clear();
            foreach (var r in runs) if (r) { r.Drop(); Object.Destroy(r.gameObject); }
            runs.Clear();
            HoloClearAll();
            Cleared += n;
            return n;
        }

        /// <summary>OzUePlatformViews.TakeLoose: the structure pieces lying on a deck that is going (test `on`) go with it.</summary>
        internal static int SweepIn(System.Func<Vector3, bool> on)
        {
            int n = 0;
            foreach (var p in live) if (p && on(p.transform.position)) { p.gameObject.SetActive(false); Object.Destroy(p.gameObject); n++; }
            live.RemoveAll(p => !p);
            Swept += n;
            return n;
        }

        /// <summary>A platform or an obstacle is dropped (OzUePlatformViews.Add, OzUeObstacleViews.Add): the rubble in its footprint (world corners
        /// a, b, 0.5 m wider) sinks out quickly, so nothing stands in the new body.</summary>
        internal static int ClearFootprint(Vector3 a, Vector3 b)
        {
            float x0 = Mathf.Min(a.x, b.x) - 0.5f, x1 = Mathf.Max(a.x, b.x) + 0.5f, z0 = Mathf.Min(a.z, b.z) - 0.5f, z1 = Mathf.Max(a.z, b.z) + 0.5f;
            int n = 0;
            foreach (var p in live)
            {
                if (!p || p.Sinking) continue;
                var q = p.transform.position;
                if (q.x < x0 || q.x > x1 || q.z < z0 || q.z > z1) continue;
                p.Sink(0.35f); n++;
            }
            return n;
        }

        internal static void Forget(OzUeStructureRun r) { runs.Remove(r); }

        // ------------------------------------------------------------------ sound
        internal static void Sound(string ev, Vector3 at, float vol, float pitch, int emitter = -1)
        {
            if (AudioDirector.Has(ev)) AudioDirector.Fire(ev, at, emitter, vol, pitch);
            else OzSoundLog.Add("UE", ev, null, "DROP no event", 0f, at);
        }

        static float knockBudget; static int knockFrame;
        /// <summary>Landing knocks share a small budget (3 at once, 7 a second): a shower of pieces is not a wall of noise.</summary>
        internal static bool TryKnock()
        {
            if (knockFrame != Time.frameCount)
            {
                float dt = Time.frameCount - knockFrame > 1000 ? 1f : Time.deltaTime;
                knockFrame = Time.frameCount; knockBudget = Mathf.Min(3f, knockBudget + dt * 7f);
            }
            if (knockBudget < 1f) return false;
            knockBudget -= 1f; return true;
        }

        // ------------------------------------------------------------------ the rolling dust (OzBlastFog's material)
        static ParticleSystem fogPs; static Material fogMat; static bool fogTried;
        static ParticleSystem Fog()
        {
            if (fogPs) return fogPs;
            if (fogTried && !fogMat) return null;
            fogTried = true;
            fogMat = OzBlastFog.NewMaterial("OzUeStructureDust");
            if (!fogMat) return null;
            fogMat.SetFloat("_NoiseBoost", OzBlastFog.UeNoiseBoost);
            fogMat.SetFloat("_GroundFade", OzBlastFog.UeGroundFadeM);
            fogMat.SetFloat("_GroundY", 0f);
            var host = new GameObject("OZ_UeStructureDust");
            fogPs = OzBlastFog.MakeSystem("OZ_UeStructureDust_Puffs", host.transform, fogMat, true, out _);
            return fogPs;
        }

        /// <summary>One fog dust puff (false = the fog shader is missing: the caller's fallback draws it).</summary>
        internal static bool Puff(Vector3 p, Vector3 v, float size, float life, float alpha, Color? tint = null)
        {
            var ps = Fog(); if (!ps) return false;
            var c = tint ?? DustColor;
            var ep = new ParticleSystem.EmitParams
            {
                position = p, velocity = v, startLifetime = life, startSize = size, startColor = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha * DustAlphaK)),
                rotation = Random.Range(0f, 360f), randomSeed = (uint)Random.Range(1, int.MaxValue), applyShapeToPosition = false,
            };
            ps.Emit(ep, 1); FogPuffs++;
            return true;
        }

        /// <summary>A small dust burst (landing, crack): the fog when it is there, else the game's own dust.</summary>
        internal static void DustAt(Vector3 p, float size, int n = 2)
        {
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                float a = Random.Range(0f, 6.283f);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                any |= Puff(p + d * size * 0.25f + Vector3.up * 0.3f, d * Random.Range(0.8f, 1.8f) + Vector3.up * Random.Range(0.3f, 0.9f), size * Random.Range(0.8f, 1.25f), Random.Range(1.6f, 2.6f), 0.5f);
            }
            if (!any) { var fx = OpenZFxPlayer.I; if (fx) fx.Dust(p, Mathf.Clamp(size * 0.3f, 0.3f, 1.4f)); else if (FxManager.I) FxManager.I.Dust(p, 0.6f); }
        }

        internal static void Sparks(Vector3 at, Vector3 dir, int n)
        {
            var fx = OpenZFxPlayer.I; if (!fx) return;
            for (int i = 0; i < n; i++)
            {
                var v = (dir * Random.Range(1.5f, 4f) + Random.insideUnitSphere * 2.2f + Vector3.up * Random.Range(0.5f, 2.5f));
                fx.EmitChannel("leg_spark", at, v, Random.Range(0.3f, 0.65f), Random.Range(0.05f, 0.09f), SparkColor, 1f);
            }
        }

        internal static void Smoke(Vector3 at, float k)
        {
            var fx = OpenZFxPlayer.I; if (!fx) return;
            fx.EmitChannel("leg_smoke", at, new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.7f, 1.3f), Random.Range(-0.25f, 0.25f)), Random.Range(2.2f, 3.4f), SmokeSize * k * Random.Range(0.8f, 1.2f), SmokeColor, 0.55f);
        }
    }

    /// <summary>One collapse: its pieces (built in place, held, released in order), its dust and sound timeline.</summary>
    public sealed class OzUeStructureRun : MonoBehaviour
    {
        internal sealed class Held
        {
            public GameObject go; public Rigidbody rb; public OzUeStructurePiece piece; public Mesh colMesh; public Vector3 colBox; public bool box;
            public float at; public Vector3 vel, spin; public bool done;
        }

        readonly List<Held> held = new List<Held>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Vector3> seams = new List<Vector3>();
        System.Random rnd;
        Vector3 mid, a0, b0; float H, wideM, longM, t; bool tower, crashed, settled, gave; int emitter;
        float plumeUntil, lingerUntil, nextPlume, nextLinger, nextSmoke, smokeUntil;
        Material[] bodyMats; Material fracMat; MaterialPropertyBlock mpb; Color tint, fracTint;
        public string Summary { get; private set; } = "-";

        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        float R(float a, float b) => R(rnd, a, b);

        // ------------------------------------------------------------------ build
        internal void BuildPlatform(OzUePlatformView v)
        {
            var o = v.Obj; var p = v.Info;
            emitter = o.Id; rnd = new System.Random(o.Id * 7919 + 17);
            tower = p.Kind == ZWorld.PlatformHigh;
            H = (float)p.DeckM;
            mid = v.Mid();
            a0 = ZGame.PxToWorld(o.LocX, o.LocY); b0 = ZGame.PxToWorld(o.LocX + o.WidthPix, o.LocY + o.HeightPix);
            float W = ZWorld.PlatformWideTiles, top = ZWorld.PlatformBackRows + ZWorld.PlatformDeckRows, L = p.Rows;
            wideM = W * 2f; longM = L * 2f;
            bodyMats = OzUePlatformViews.Materials();
            float f = o.MaxHealth > 0 ? Mathf.Clamp01((float)o.Health / o.MaxHealth) : 1f, k = Mathf.Lerp(0.62f, 1f, f);   // the body's tint at the death (OzUePlatformView.LateUpdate)
            tint = new Color(k, k, k, 1f);
            ConcreteChunkLib.Load();
            fracMat = ConcreteChunkLib.Fracture ? ConcreteChunkLib.Fracture : bodyMats[0];
            fracTint = Color.Lerp(ConcreteChunkLib.Fracture ? ConcreteChunkLib.LevelFracture : new Color(0.66f, 0.62f, 0.54f), new Color(0.66f, 0.62f, 0.54f), 0.5f) * Mathf.Lerp(0.85f, 1f, f); fracTint.a = 1f;
            mpb = new MaterialPropertyBlock();
            Vector3 P(float across, float along, float y) => OzUePlatformViews.Point(p, across, along, y);
            Vector3 centreLine = P(W * 0.5f, top * 0.5f, 0f);

            // --- the deck and the ramp: one fracture grid over (across, along), the crest an exact grid line
            var us = Cuts(0f, W, TopCellTiles(W), true);
            var vsDeck = Cuts(0f, top, Mathf.Max(2, Mathf.RoundToInt(top / OzUeStructureBreak.TopCell)), true);
            var vsRamp = Cuts(top, L, Mathf.Max(2, Mathf.RoundToInt((L - top) / (OzUeStructureBreak.TopCell * 1.1f))), true);
            var vs = new List<float>(vsDeck); for (int i = 1; i < vsRamp.Count; i++) vs.Add(vsRamp[i]);
            int crestRow = vsDeck.Count - 1;
            var cells = Fracture(us, vs, crestRow, 1f);
            Vector3 rampN = Vector3.Cross(P(W, top, H) - P(0f, top, H), P(0f, L, 0f) - P(0f, top, H)).normalized; if (rampN.y < 0f) rampN = -rampN;
            float slope = Mathf.Acos(Mathf.Clamp01(rampN.y));
            foreach (var c in cells)
            {
                bool ramp = c.centre.y > top + 1e-3f;
                var pts = new List<Vector3>(); var uvs = new List<Vector2>();
                foreach (var q in c.pts)
                {
                    float y = (float)p.HeightAt(q.y);
                    pts.Add(P(q.x, q.y, y));
                    uvs.Add(ramp ? new Vector2(q.x / W, (q.y - top) / 4f) : new Vector2(q.x / 4f, q.y / 4f));   // the body's own UVs (OzUePlatformShape.Shell)
                }
                var depth = ramp ? -rampN : Vector3.down;
                float T = ramp ? OzUeStructureBreak.RampT : OzUeStructureBreak.DeckT;
                var piece = Prism(ramp ? "Ramp" : "Deck", pts, uvs, c.outer, depth, T, ramp ? 1 : 0, true);
                if (piece == null) continue;
                // release: the deck gives from the middle out; the ramp from the crest down
                float dMid = new Vector2((c.centre.x - W * 0.5f) / (W * 0.5f), (c.centre.y - top * 0.5f) / (top * 0.5f)).magnitude;
                float at = ramp ? 0.28f + 0.5f * Mathf.Clamp01((c.centre.y - top) / (L - top)) + R(0f, 0.08f) : 0.16f + 0.22f * Mathf.Clamp01(dMid / 1.4f) + R(0f, 0.06f);
                var inward = centreLine - piece.go.transform.position; inward.y = 0f; inward = inward.sqrMagnitude > 0.01f ? inward.normalized : Vector3.zero;
                var downSlope = (P(W * 0.5f, L, 0f) - P(W * 0.5f, top, H)).normalized;
                piece.at = at;
                piece.vel = ramp ? downSlope * R(0.6f, 1.8f) + Vector3.down * R(0.2f, 0.8f) : inward * R(0.3f, 1.4f) + Vector3.down * R(0.4f, 1.3f);
                piece.spin = new Vector3(R(-1f, 1f), R(-0.3f, 0.3f), R(-1f, 1f)) * R(0.3f, 1.3f);
                OzUeStructureBreak.SlabsMade++;
            }

            // --- the walls: two sides and the back, panels under the slabs, cut by the ramp's slope
            float topLimit(float along) => (float)p.HeightAt(along) - (along > top ? OzUeStructureBreak.RampT / Mathf.Max(0.5f, Mathf.Cos(slope)) : OzUeStructureBreak.DeckT) - 0.03f;
            float wt = OzUeStructureBreak.WallT / 2f;                                                           // in tiles
            for (int side = 0; side < 3; side++)
            {
                bool back = side == 2;
                float uLo = back ? wt : 0f, uHi = back ? W - wt : L;
                float vHi = H - OzUeStructureBreak.DeckT - 0.03f;
                int nu = Mathf.Max(2, Mathf.RoundToInt((uHi - uLo) / OzUeStructureBreak.WallCellU)), nv = Mathf.Max(1, Mathf.RoundToInt(vHi / OzUeStructureBreak.WallCellV));
                var wu = Cuts(uLo, uHi, nu, false); var wv = Cuts(0.01f, vHi, nv, false);
                var wc = Fracture(wu, wv, -1, 0.5f);                                                          // v in metres: the jag measured in tiles x 0.5
                float acrossAt = side == 0 ? 0f : W;
                var outN = back ? (P(1f, 0f, 0f) - P(1f, 1f, 0f)).normalized : side == 0 ? (P(0f, 1f, 0f) - P(1f, 1f, 0f)).normalized : (P(1f, 1f, 0f) - P(0f, 1f, 0f)).normalized;
                foreach (var c in wc)
                {
                    var poly = new List<Vector2>(c.pts); var outer = new List<bool>(c.outer);
                    if (!back)
                    {   // under the slabs: v <= the deck's underside, then the ramp's slope line
                        ClipBelow(poly, outer, q => topLimit(q.x), top, L);
                        if (poly.Count < 3) continue;
                    }
                    var pts = new List<Vector3>(); var uvs = new List<Vector2>();
                    foreach (var q in poly)
                    {
                        var w3 = back ? P(q.x, 0f, q.y) : P(acrossAt, q.x, q.y);
                        pts.Add(w3); uvs.Add(new Vector2(q.x / 4f, q.y / 8f));
                    }
                    float maxY = 0f; foreach (var q in pts) maxY = Mathf.Max(maxY, q.y);
                    if (maxY < 0.3f || Area(pts) < 0.25f) continue;
                    var piece = Prism("Wall", pts, uvs, outer, -outN, OzUeStructureBreak.WallT, 0, false, true);
                    if (piece == null) continue;
                    var cp = piece.go.transform.position;
                    float up01 = Mathf.Clamp01(cp.y / Mathf.Max(1f, H));
                    float along = back ? 0f : (c.centre.x);
                    piece.at = 0.3f + 0.35f * (1f - up01) + (along > top ? 0.25f * (along - top) / (L - top) : 0f) + R(0f, 0.08f);
                    piece.vel = outN * R(0.4f, 1.6f) * (0.4f + up01) + Vector3.down * R(0f, 0.5f);
                    var tipAxis = Vector3.Cross(Vector3.up, outN);                                                  // tips the top outward
                    piece.spin = tipAxis * R(0.5f, 1.3f) * (0.5f + up01) + Random.insideUnitSphere * 0.25f;
                    OzUeStructureBreak.WallPanelsMade++;
                }
            }

            // --- the kerb: gunmetal lengths with the holo edge on top, sides and back
            float kw = OzUePlatformView.KerbWide;
            foreach (var (from, to, across, isBack) in new[] { (0f, L, kw * 0.5f, false), (0f, L, W - kw * 0.5f, false), (kw, W - kw, kw * 0.5f, true) })
            {
                float s = from;
                while (s < to - 0.3f)
                {
                    float len = Mathf.Min(to - s, R(1.4f, 2.2f));                                                  // tiles (2.8 - 4.4 m)
                    if (!isBack && s < top && s + len > top) len = top - s;                                       // break at the crest
                    if (!isBack && to - (s + len) < 0.5f) len = to - s;
                    Vector3 pa, pb;
                    if (isBack) { pa = P(s, across, H); pb = P(s + len, across, H); }
                    else { pa = P(across, s, (float)p.HeightAt(s)); pb = P(across, s + len, (float)p.HeightAt(s + len)); }
                    var piece = Kerb(pa, pb, kw * 2f, OzUePlatformView.KerbM, isBack ? Vector3.zero : (across < W * 0.5f ? (P(0f, 1f, 0f) - P(1f, 1f, 0f)).normalized : (P(1f, 1f, 0f) - P(0f, 1f, 0f)).normalized));
                    if (piece != null)
                    {
                        float along = isBack ? 0f : s + len * 0.5f;
                        piece.at = along > top ? 0.28f + 0.5f * Mathf.Clamp01((along - top) / (L - top)) + 0.04f : 0.3f + R(0f, 0.12f);
                        var outward = isBack ? (P(1f, 0f, 0f) - P(1f, 1f, 0f)).normalized : (across < W * 0.5f ? (P(0f, 1f, 0f) - P(1f, 1f, 0f)).normalized : (P(1f, 1f, 0f) - P(0f, 1f, 0f)).normalized);
                        piece.vel = outward * R(0.8f, 2.4f) + Vector3.up * R(0f, 0.8f);
                        piece.spin = Random.insideUnitSphere * R(1f, 3f);
                        seams.Add((pa + pb) * 0.5f);
                        OzUeStructureBreak.KerbsMade++;
                    }
                    s += len;
                }
            }

            // --- modelled library chunks: M with rebar under the deck (they fall with it), S / XS crumbs out of the cracks
            bool light = OzLightRows.WrecksLight;
            int nM = tower ? OzUeStructureBreak.ChunksMTower : OzUeStructureBreak.ChunksMMid, nS = tower ? OzUeStructureBreak.CrumbsTower : OzUeStructureBreak.CrumbsMid;
            if (light) { nM /= 2; nS /= 2; }
            if (ConcreteChunkLib.Loaded)
            {
                for (int i = 0; i < nM; i++)
                {
                    float ac = R(1.6f, W - 1.6f), al = R(1.6f, top - 0.6f);
                    var at = P(ac, al, H - OzUeStructureBreak.DeckT - 0.9f);
                    var h = LibChunk(rnd.NextDouble() < 0.7 ? "M" : "S", at, true);
                    if (h != null) { h.at = 0.18f + R(0f, 0.3f); h.vel = Vector3.down * R(0.2f, 1f) + Random.insideUnitSphere * 0.8f; h.spin = Random.insideUnitSphere * R(1f, 3f); }
                }
                for (int i = 0; i < nS; i++)
                {
                    bool deck = i % 3 != 2;
                    var at = deck ? P(R(0.6f, W - 0.6f), R(0.6f, top - 0.6f), H - OzUeStructureBreak.DeckT - 0.5f) : P(i % 2 == 0 ? -0.3f : W + 0.3f, R(1f, top), R(1f, H - 1f));
                    var h = LibChunk(rnd.NextDouble() < 0.5 ? "S" : "XS", at, false);
                    if (h != null) { h.at = R(0.05f, 0.9f); h.vel = Random.insideUnitSphere * 1.6f + Vector3.up * R(0f, 1.5f); h.spin = Random.insideUnitSphere * R(2f, 6f); }
                }
            }

            foreach (var h in held) OzUeStructureBreak.live.Add(h.piece);
            OzUeStructureBreak.PiecesMade += held.Count;
            Summary = $"{(tower ? "tower" : "platform")}: {held.Count} pieces (slabs and panels from the deck, the ramp and the walls, kerb lengths, library chunks)";
            plumeUntil = 1.1f; lingerUntil = 6.5f; smokeUntil = 7f;
            Crack();
        }

        /// <summary>A cut list from lo to hi in n cells, each inner cut jittered (fixed = the ends stay).</summary>
        List<float> Cuts(float lo, float hi, int n, bool unused)
        {
            var l = new List<float>();
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(lo, hi, (float)i / n);
                if (i > 0 && i < n) x += (hi - lo) / n * R(-0.18f, 0.18f);
                l.Add(x);
            }
            return l;
        }

        int TopCellTiles(float W) => Mathf.Max(3, Mathf.RoundToInt(W / OzUeStructureBreak.TopCell));

        // ------------------------------------------------------------------ the fracture grid
        internal sealed class Cell { public List<Vector2> pts = new List<Vector2>(); public List<bool> outer = new List<bool>(); public Vector2 centre; }

        /// <summary>A jittered grid over us x vs: shared jagged crack lines between neighbours (straight on the outer edges); a third of the cells
        /// split on a diagonal. crestRow: a row whose cut stays exactly straight (its nodes move along it only). scale: jag / jitter in the
        /// cell's units.</summary>
        List<Cell> Fracture(List<float> us, List<float> vs, int crestRow, float scale)
        {
            int nu = us.Count, nv = vs.Count;
            var node = new Vector2[nu, nv];
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    var q = new Vector2(us[i], vs[j]);
                    bool edgeU = i == 0 || i == nu - 1, edgeV = j == 0 || j == nv - 1 || j == crestRow;
                    float du = (i > 0 && i < nu - 1) ? Mathf.Min(us[i] - us[i - 1], us[i + 1] - us[i]) : 0f;
                    float dv = (j > 0 && j < nv - 1) ? Mathf.Min(vs[j] - vs[j - 1], vs[j + 1] - vs[j]) : 0f;
                    if (!edgeU) q.x += du * R(-OzUeStructureBreak.Jitter, OzUeStructureBreak.Jitter);
                    if (!edgeV) q.y += dv * R(-OzUeStructureBreak.Jitter, OzUeStructureBreak.Jitter);
                    node[i, j] = q;
                }
            var jags = new Dictionary<long, List<Vector2>>();
            List<Vector2> Edge(int i0, int j0, int i1, int j1, bool outerEdge)
            {   // the points between two nodes, from (i0, j0) to (i1, j1); stored once per edge, read backwards by the other side
                long Key(int a, int b, int c, int d) => ((long)(a * 4096 + b) << 24) | (long)(c * 4096 + d);
                bool fwd = i0 * 4096 + j0 < i1 * 4096 + j1;
                long key = fwd ? Key(i0, j0, i1, j1) : Key(i1, j1, i0, j0);
                if (!jags.TryGetValue(key, out var l))
                {
                    l = new List<Vector2>();
                    Vector2 a = fwd ? node[i0, j0] : node[i1, j1], b = fwd ? node[i1, j1] : node[i0, j0];
                    if (!outerEdge)
                    {
                        var d = b - a; var n = new Vector2(-d.y, d.x).normalized; float len = d.magnitude;
                        int m = len > 3.5f * scale ? 3 : 2;
                        for (int k = 1; k <= m; k++)
                        {
                            float tt = (float)k / (m + 1) + R(-0.06f, 0.06f);
                            l.Add(Vector2.Lerp(a, b, tt) + n * len * R(-OzUeStructureBreak.Jag, OzUeStructureBreak.Jag) * 1.2f);
                        }
                    }
                    jags[key] = l;
                }
                if (fwd) return l;
                var r = new List<Vector2>(l); r.Reverse(); return r;
            }
            bool OuterH(int j) => j == 0 || j == nv - 1;
            bool OuterV(int i) => i == 0 || i == nu - 1;
            var cells = new List<Cell>();
            void Add(Cell c, int i0, int j0, int i1, int j1, bool outerEdge)
            {
                c.pts.Add(node[i0, j0]); c.outer.Add(outerEdge);
                foreach (var q in Edge(i0, j0, i1, j1, outerEdge)) { c.pts.Add(q); c.outer.Add(outerEdge); }
            }
            for (int i = 0; i < nu - 1; i++)
                for (int j = 0; j < nv - 1; j++)
                {
                    bool split = rnd.NextDouble() < 0.33;
                    if (!split)
                    {
                        var c = new Cell();
                        Add(c, i, j, i + 1, j, OuterH(j)); Add(c, i + 1, j, i + 1, j + 1, OuterV(i + 1));
                        Add(c, i + 1, j + 1, i, j + 1, OuterH(j + 1)); Add(c, i, j + 1, i, j, OuterV(i));
                        cells.Add(c);
                    }
                    else
                    {
                        var a = new Cell(); var b = new Cell();
                        Add(a, i, j, i + 1, j, OuterH(j)); Add(a, i + 1, j, i + 1, j + 1, OuterV(i + 1)); Add(a, i + 1, j + 1, i, j, false);
                        Add(b, i, j, i + 1, j + 1, false); Add(b, i + 1, j + 1, i, j + 1, OuterH(j + 1)); Add(b, i, j + 1, i, j, OuterV(i));
                        cells.Add(a); cells.Add(b);
                    }
                }
            foreach (var c in cells) { var s = Vector2.zero; foreach (var q in c.pts) s += q; c.centre = s / c.pts.Count; }
            return cells;
        }

        /// <summary>Wall polygons (u = along in tiles, v = height in m): keep v below limit(u) (flat over the deck, then down the ramp's slope;
        /// the limit is linear on each side of `top`, so two half-plane cuts do it).</summary>
        static void ClipBelow(List<Vector2> poly, List<bool> outer, System.Func<Vector2, float> limit, float top, float L)
        {
            float y0 = limit(new Vector2(0f, 0f)), yTop = limit(new Vector2(top + 1e-3f, 0f)), yEnd = limit(new Vector2(L, 0f));
            // half-plane 1: v <= y0 (the deck's underside); half-plane 2: v <= the ramp line through (top, yTop) and (L, yEnd)
            Cut(poly, outer, q => y0 - q.y);
            float k = (yEnd - yTop) / Mathf.Max(1e-3f, L - top);
            Cut(poly, outer, q => (yTop + k * (q.x - top)) - q.y);
        }

        /// <summary>Sutherland-Hodgman against f(q) >= 0; a new edge on the cut is a broken face (not outer).</summary>
        static void Cut(List<Vector2> poly, List<bool> outer, System.Func<Vector2, float> f)
        {
            var np = new List<Vector2>(); var no = new List<bool>();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n]; float fa = f(a), fb = f(b);
                bool ain = fa >= 0f, bin = fb >= 0f;
                if (ain) { np.Add(a); no.Add(outer[i]); }                                                    // a -> b (or a -> the cut point) lies on the old edge
                if (ain != bin)
                {
                    var x = Vector2.Lerp(a, b, fa / (fa - fb));
                    np.Add(x); no.Add(ain ? false : outer[i]);                                                   // leaving: the next edge runs along the cut; entering: x -> b is the old edge
                }
            }
            poly.Clear(); poly.AddRange(np); outer.Clear(); outer.AddRange(no);
        }

        static float Area(List<Vector3> pts)
        {
            var s = Vector3.zero;
            for (int i = 0; i < pts.Count; i++) s += Vector3.Cross(pts[i], pts[(i + 1) % pts.Count]);
            return s.magnitude * 0.5f;
        }

        // ------------------------------------------------------------------ the meshes
        sealed class MB
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int>[] t;
            public MB(int subs) { t = new List<int>[subs]; for (int i = 0; i < subs; i++) t[i] = new List<int>(); }
            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 outward)
            {
                var nn = Vector3.Cross(b - a, c - a); if (nn.sqrMagnitude < 1e-10f) return;
                if (Vector3.Dot(nn, outward) < 0f) { var x = b; b = c; c = x; var y = ub; ub = uc; uc = y; nn = -nn; }   // OzUePlatformShape's rule: Cross(b - a, c - a) faces the viewer
                nn.Normalize(); int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); n.Add(nn); n.Add(nn); n.Add(nn); uv.Add(ua); uv.Add(ub); uv.Add(uc);
                t[sub].Add(i); t[sub].Add(i + 1); t[sub].Add(i + 2);
            }
            public Mesh Make(string name, Vector3 offset)
            {
                for (int i = 0; i < v.Count; i++) v[i] -= offset;
                var m = new Mesh { name = name, subMeshCount = t.Length };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
                for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i);
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>A slab / panel: the top polygon (world), its UVs (the body's own), the outer flag of each edge, extruded `T` along `depth`.
        /// Submeshes: 0 its face (the body's concrete or ramp material), 1 the outer sides (concrete), 2 the broken faces (fracture).</summary>
        Held Prism(string name, List<Vector3> top, List<Vector2> topUv, List<bool> outer, Vector3 depth, float T, int faceMat, bool clampFloor, bool wall = false)
        {
            int n = top.Count; if (n < 3) return null;
            var ct = Vector3.zero; foreach (var q in top) ct += q; ct /= n;
            // the crack gap: every point a little toward the middle
            var tp = new List<Vector3>(n);
            foreach (var q in top) { var d = ct - q; tp.Add(q + (d.sqrMagnitude > 1e-6f ? d.normalized * OzUeStructureBreak.Gap : Vector3.zero)); }
            var bt = new List<Vector3>(n);
            foreach (var q in tp) { var b = q + depth * T; if (clampFloor && b.y < 0.01f) b.y = 0.01f; if (wall && b.y < 0.01f) b.y = 0.01f; bt.Add(b); }
            var cb = Vector3.zero; foreach (var q in bt) cb += q; cb /= n;
            var centre = (ct + cb) * 0.5f;
            var mb = new MB(3);
            var uc = Vector2.zero; foreach (var u in topUv) uc += u; uc /= n;
            Vector2 Plan(Vector3 p, Vector3 e, float k) => new Vector2(Vector3.Dot(p, e), Vector3.Dot(p, depth.sqrMagnitude > 0f ? Vector3.Cross(e, depth).normalized : Vector3.up)) / k;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                mb.Tri(0, ct, tp[i], tp[j], uc, topUv[i], topUv[j], -depth);                                                  // the face
                var e = (tp[j] - tp[i]); var en = e.sqrMagnitude > 1e-8f ? e.normalized : Vector3.right;
                Vector2 b0 = Plan(bt[i], en, 1.6f), b1 = Plan(bt[j], en, 1.6f), b2 = Plan(cb, en, 1.6f);
                mb.Tri(2, cb, bt[i], bt[j], b2, b0, b1, depth);                                                                  // the underside: broken
                var outward = (tp[i] + tp[j]) * 0.5f - ct; outward -= Vector3.Project(outward, depth);
                bool o = outer[i];
                int sub = o ? 1 : 2; float k = o ? 8f : 1.6f;
                Vector2 Side(Vector3 p) => o ? new Vector2(Vector3.Dot(p, en), p.y) / k : new Vector2(Vector3.Dot(p, en), Vector3.Dot(p, depth)) / k;
                mb.Tri(sub, tp[i], tp[j], bt[j], Side(tp[i]), Side(tp[j]), Side(bt[j]), outward);
                mb.Tri(sub, tp[i], bt[j], bt[i], Side(tp[i]), Side(bt[j]), Side(bt[i]), outward);
            }
            var mesh = mb.Make("OZ_UeStructure_" + name, centre); meshes.Add(mesh);
            // the collider: the same prism shrunk to 80 % round its middle (a jagged edge never reaches into the neighbour's)
            var cm = new Mesh { name = "OZ_UeStructure_" + name + "_col" }; meshes.Add(cm);
            var cv = new List<Vector3>(); var ci = new List<int>();
            foreach (var q in tp) cv.Add(Vector3.Lerp(centre, q, 0.8f) - centre);
            foreach (var q in bt) cv.Add(Vector3.Lerp(centre, q, 0.8f) - centre);
            for (int i = 1; i < n - 1; i++) { ci.Add(0); ci.Add(i); ci.Add(i + 1); ci.Add(n); ci.Add(n + i + 1); ci.Add(n + i); }
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; ci.Add(i); ci.Add(j); ci.Add(n + j); ci.Add(i); ci.Add(n + j); ci.Add(n + i); }
            cm.SetVertices(cv); cm.SetTriangles(ci, 0); cm.RecalculateBounds();
            float area = Area(top);
            var go = NewPiece(name, centre, mesh, new[] { bodyMats[faceMat], bodyMats[0], fracMat }, OzUeStructureBreak.Heavy);
            var r = go.GetComponent<MeshRenderer>();
            mpb.Clear(); mpb.SetColor(OzUeStructureBreak.BaseColorId, tint); r.SetPropertyBlock(mpb, 0); r.SetPropertyBlock(mpb, 1);
            mpb.Clear(); mpb.SetColor(OzUeStructureBreak.BaseColorId, fracTint); r.SetPropertyBlock(mpb, 2);
            var h = Hold(go, Mathf.Clamp(area * T * 120f, 25f, 2500f), true, false, false);
            h.colMesh = cm;
            Seam(top);
            return h;
        }

        void Seam(List<Vector3> top) { if (top.Count > 2 && seams.Count < 400) seams.Add(Vector3.Lerp(top[0], top[top.Count / 2], R(0.2f, 0.8f))); }

        /// <summary>A kerb length from a to b (its foot line), wide x tall, with the holo edge line on the outer top edge.</summary>
        Held Kerb(Vector3 a, Vector3 b, float wideM, float tallM, Vector3 outward)
        {
            var dir = b - a; float len = dir.magnitude; if (len < 0.3f) return null;
            dir /= len;
            var side = Vector3.Cross(Vector3.up, dir).normalized; var up = Vector3.Cross(dir, side).normalized; if (up.y < 0f) { up = -up; side = -side; }
            var centre = (a + b) * 0.5f + up * (tallM * 0.5f + 0.01f);
            var mb = new MB(2);
            Vector3 hx = dir * (len * 0.5f - 0.02f), hy = up * (tallM * 0.5f), hz = side * (wideM * 0.5f);
            void Face(Vector3 c, Vector3 u, Vector3 v, Vector3 nrm, int sub)
            {
                Vector3 p0 = c - u - v, p1 = c + u - v, p2 = c + u + v, p3 = c - u + v;
                mb.Tri(sub, p0, p1, p2, Vector2.zero, Vector2.right, Vector2.one, nrm); mb.Tri(sub, p0, p2, p3, Vector2.zero, Vector2.one, Vector2.up, nrm);
            }
            Face(centre + hy, hx, hz, up, 0); Face(centre - hy, hx, hz, -up, 0);
            Face(centre + hz, hx, hy, side, 0); Face(centre - hz, hx, hy, -side, 0);
            Face(centre + hx, hz, hy, dir, 0); Face(centre - hx, hz, hy, -dir, 0);
            // the edge line on the top, on the outer side (6 cm, 1 cm over the top)
            var os = outward.sqrMagnitude > 0.01f && Vector3.Dot(outward, side) < 0f ? -side : side;
            Face(centre + hy + up * 0.01f + os * (wideM * 0.5f - 0.03f), hx, os * 0.03f, up, 1);
            var mesh = mb.Make("OZ_UeStructure_Kerb", centre); meshes.Add(mesh);
            var go = NewPiece("Kerb", centre, mesh, new[] { bodyMats[2], bodyMats[3] }, OzUeStructureBreak.Heavy);
            var h = Hold(go, 60f, false, true, false);
            h.box = true; h.colBox = new Vector3(wideM, tallM, len - 0.05f);                                   // LookRotation(dir, up): x = the side, z = the length
            go.transform.rotation = Quaternion.LookRotation(dir, up);
            // the mesh was built in world axes: undo the turn for the collider only (the box is laid along the GameObject's axes)
            mesh.SetVertices(Rotated(mesh.vertices, Quaternion.Inverse(go.transform.rotation)));
            var ns = mesh.normals; mesh.SetNormals(Rotated(ns, Quaternion.Inverse(go.transform.rotation))); mesh.RecalculateBounds();
            return h;
        }

        static Vector3[] Rotated(Vector3[] v, Quaternion q) { for (int i = 0; i < v.Length; i++) v[i] = q * v[i]; return v; }

        Held LibChunk(string cls, Vector3 at, bool heavy)
        {
            var e = ConcreteChunkLib.Pick(rnd, cls, cls == "XS"); if (e == null || !e.mesh) return null;
            var go = NewPiece("Chunk_" + e.cls, at, e.mesh, e.mats, heavy ? OzUeStructureBreak.Heavy : OzUeStructureBreak.Light);
            go.transform.rotation = ConcreteChunkLib.RandomRotation(rnd);
            go.transform.localScale = ConcreteChunkLib.RandomScale(rnd, e.max_scale);
            var mr = go.GetComponent<MeshRenderer>(); ConcreteChunkLib.ApplyLook(mr, e, rnd);
            go.SetActive(false);                                                                                 // inside the body until its release
            var h = Hold(go, Mathf.Clamp(e.suggested_mass > 0f ? e.suggested_mass : 20f, 1f, 800f), heavy, false, !heavy);
            if (e.col) { h.colMesh = e.col; } else { h.box = true; h.colBox = e.mesh.bounds.size; }
            if (heavy) OzUeStructureBreak.ChunksMade++; else OzUeStructureBreak.CrumbsMade++;
            return h;
        }

        GameObject NewPiece(string name, Vector3 at, Mesh mesh, Material[] mats, int layer)
        {
            var go = new GameObject("OZ_UeStructurePiece_" + name) { layer = layer };
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            return go;
        }

        Held Hold(GameObject go, float mass, bool heavy, bool metal, bool small)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.mass = mass; rb.linearDamping = 0.12f; rb.angularDamping = 0.5f; rb.maxAngularVelocity = 8f;
            rb.maxDepenetrationVelocity = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var pc = go.AddComponent<OzUeStructurePiece>();
            pc.run = this; pc.rb = rb; pc.heavy = heavy; pc.metal = metal; pc.small = small;
            pc.life = OzUeStructureBreak.RestSeconds + R(0f, 4f) - (small ? R(12f, 16f) : 0f);
            var h = new Held { go = go, rb = rb, piece = pc };
            held.Add(h);
            return h;
        }

        // ------------------------------------------------------------------ the timeline
        void Crack()
        {
            var up = Vector3.up * 0.4f;
            int jets = Mathf.Min(seams.Count, tower ? 18 : 12);
            for (int i = 0; i < jets; i++)
            {
                var s = seams[rnd.Next(seams.Count)];
                if (!OzUeStructureBreak.Puff(s + up, new Vector3(R(-1f, 1f), R(1.5f, 3.2f), R(-1f, 1f)), R(1.4f, 2.8f), R(1.4f, 2.2f), 0.55f))
                    OzUeStructureBreak.DustAt(s, 1.5f, 1);
                if (i % 3 == 0) OzUeStructureBreak.Sparks(s + up, Vector3.up, 4);
            }
            OzUeStructureBreak.Sound("dst_structure_groan", mid + Vector3.up * H * 0.6f, 1f, tower ? 0.9f : 1f, emitter);
            OzUeStructureBreak.Sound("dst_rock_chunks", mid + Vector3.up * 2f, 0.9f, 0.9f, emitter);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;                                                                                 // paused: everything holds
            t += dt;
            foreach (var h in held)
            {
                if (h.done || t < h.at) continue;
                h.done = true;
                if (!h.go) continue;
                h.go.SetActive(true);
                Collider c;
                if (!h.box && h.colMesh) { var mc = h.go.AddComponent<MeshCollider>(); mc.convex = true; mc.sharedMesh = h.colMesh; c = mc; }   // added at the release: the cooking cost spreads over the collapse
                else { var bc = h.go.AddComponent<BoxCollider>(); bc.size = h.colBox; c = bc; }
                c.sharedMaterial = OzUeStructureBreak.ConcreteMat();
                h.rb.isKinematic = false; h.rb.interpolation = RigidbodyInterpolation.Interpolate;
                h.rb.linearVelocity = h.vel; h.rb.angularVelocity = h.spin;
                h.piece.released = true;
                if (h.piece.metal && rnd.NextDouble() < 0.5) OzUeStructureBreak.Sparks(h.go.transform.position, h.vel.normalized, 5);   // the kerb tears
            }
            if (!gave && t >= 0.15f)
            {
                gave = true;
                L16Pyramid.Named("dst_l16_pyramid_collapse", "OpenZ/Audio/Sfx/L16/sd_pyramid_collapse_a", mid + Vector3.up * 2f, 1f);   // the long collapse as the deck gives
            }
            if (t < plumeUntil && t >= 0.12f && t >= nextPlume)
            {   // the core plume: dust rising out of the falling body
                nextPlume = t + 0.05f;
                var q = Inside(R(0.2f, 0.9f));
                q.y = R(0.3f, 1f) * H;
                if (!OzUeStructureBreak.Puff(q, new Vector3(R(-0.6f, 0.6f), R(1.4f, 3f), R(-0.6f, 0.6f)), R(3f, 6f) * (tower ? 1.2f : 1f), R(3f, 4.5f), 0.5f))
                    { var fx = FxManager.I; if (fx && rnd.NextDouble() < 0.2) fx.DemolitionDust(q, wideM * 0.6f, longM * 0.3f, 0.5f); }
            }
            if (!crashed && t > 1.7f) Crash(mid, 4f);                                                             // nothing landed hard (a rare low collapse): the crash anyway
            if (t > 1.2f && t < lingerUntil && t >= nextLinger)
            {   // the low lingering cloud
                nextLinger = t + 0.22f;
                var q = Inside(R(0.3f, 1.1f)); q.y = R(0.6f, 1.6f);
                OzUeStructureBreak.Puff(q, new Vector3(R(-0.5f, 0.5f), R(0.05f, 0.3f), R(-0.5f, 0.5f)), R(4f, 7f), R(4f, 6f), R(0.22f, 0.32f));
            }
            if (t > 1.5f && t < smokeUntil && t >= nextSmoke)
            {   // thin grey smoke off the rubble
                nextSmoke = t + R(0.18f, 0.35f);
                var q = Inside(R(0.1f, 0.8f)); q.y = R(0.4f, 1.4f);
                OzUeStructureBreak.Smoke(q, tower ? 1.2f : 1f);
            }
            if (!settled && t > 3.2f) { settled = true; OzUeStructureBreak.Sound("dst_debris_settle", mid + Vector3.up, 0.8f, 1f, emitter); }
            if (t > 12f)
            {   // the pieces live on by themselves; the runner goes when they have
                bool any = false; foreach (var h in held) if (h.go) { any = true; break; }
                if (!any) { OzUeStructureBreak.Forget(this); Destroy(gameObject); }
            }
        }

        /// <summary>A point in the footprint (share of its half sizes).</summary>
        Vector3 Inside(float k)
        {
            float x0 = Mathf.Min(a0.x, b0.x), x1 = Mathf.Max(a0.x, b0.x), z0 = Mathf.Min(a0.z, b0.z), z1 = Mathf.Max(a0.z, b0.z);
            var c = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            return c + new Vector3((x1 - x0) * 0.5f * R(-k, k), 0f, (z1 - z0) * 0.5f * R(-k, k));
        }

        /// <summary>A piece hit something (OzUeStructurePiece): dust and a knock; the first heavy landing is the crash.</summary>
        internal void Landed(OzUeStructurePiece p, Vector3 at, float speed)
        {
            OzUeStructureBreak.Landings++;
            float size = p.small ? 0.9f : p.heavy ? Mathf.Clamp(speed * 0.4f, 1.4f, 3.4f) : 1.8f;
            OzUeStructureBreak.DustAt(at, size, p.small ? 1 : 2);
            if (p.heavy && !p.metal && !crashed && speed > 3f) { Crash(at, speed); return; }
            if (p.metal) OzUeStructureBreak.Sparks(at, Vector3.up, 4);
            if (!OzUeStructureBreak.TryKnock()) return;
            OzUeStructureBreak.Knocks++;
            float vol = Mathf.Clamp(0.2f + speed * 0.03f, 0.2f, p.small ? 0.3f : 0.55f);
            OzUeStructureBreak.Sound(p.metal ? "dst_debris_metal" : "dst_debris_concrete", at, vol, p.small ? 1.15f : R(0.9f, 1.05f), p.GetInstanceID());
        }

        /// <summary>The first heavy landing: the large crash and collapse, the base surge rolling out, the kick (on screen only).</summary>
        void Crash(Vector3 at, float speed)
        {
            crashed = true;
            OzUeStructureBreak.Sound("dst_rock_crash_large", at + Vector3.up, 1f, 1f, emitter);
            OzUeStructureBreak.Sound(tower ? "dst_collapse_large" : "dst_collapse_medium", mid + Vector3.up * 2f, 1f, 1f, emitter);
            float x0 = Mathf.Min(a0.x, b0.x), x1 = Mathf.Max(a0.x, b0.x), z0 = Mathf.Min(a0.z, b0.z), z1 = Mathf.Max(a0.z, b0.z);
            var c = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            int n = tower ? 40 : 28; bool fog = false;
            for (int i = 0; i < n; i++)
            {   // round the footprint's edge, rolling outward
                float a = i * Mathf.PI * 2f / n + R(-0.08f, 0.08f);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float ex = Mathf.Abs(d.x) > 1e-3f ? (x1 - x0) * 0.5f / Mathf.Abs(d.x) : 1e6f, ez = Mathf.Abs(d.z) > 1e-3f ? (z1 - z0) * 0.5f / Mathf.Abs(d.z) : 1e6f;
                var q = c + d * Mathf.Min(ex, ez) * R(0.7f, 1.0f); q.y = R(0.8f, 1.7f);
                fog |= OzUeStructureBreak.Puff(q, d * R(3f, 6f) + Vector3.up * R(0.1f, 0.5f), R(4f, 8f) * (tower ? 1.3f : 1f), R(3.5f, 5.5f), 0.45f);
            }
            for (int i = 0; i < (tower ? 12 : 8); i++)
            {   // and a column over the middle
                var q = Inside(0.6f); q.y = R(1f, H * 0.8f + 1f);
                fog |= OzUeStructureBreak.Puff(q, new Vector3(R(-0.8f, 0.8f), R(1.5f, 3.5f), R(-0.8f, 0.8f)), R(5f, 9f) * (tower ? 1.2f : 1f), R(4f, 6f), 0.4f);
            }
            if (!fog) { var fx = FxManager.I; if (fx) fx.DemolitionDust(c, x1 - x0, z1 - z0, tower ? 1f : 0.8f); }
            var sh = OpenZFxShake.I;
            if (sh != null && sh.Kick(mid + Vector3.up * 2f, tower ? OzUeStructureBreak.KickTowerPx : OzUeStructureBreak.KickMidPx, "ue_structure_collapse")) OzUeStructureBreak.Kicks++;
            for (int i = 0; i < 6; i++) OzUeStructureBreak.Sparks(Inside(0.7f) + Vector3.up * 0.4f, Vector3.up, 3);
        }

        /// <summary>The runner is taken (CLEAR FIELD, a failed build): its meshes go; its pieces are taken by the caller.</summary>
        internal void Drop()
        {
            foreach (var h in held) if (h.go) Destroy(h.go);
            held.Clear();
        }

        void OnDestroy()
        {
            foreach (var m in meshes) if (m) Destroy(m);
            OzUeStructureBreak.Forget(this);
        }
    }

    /// <summary>One structure piece: landings (dust, knocks), rest (kinematic once asleep), the rubble's life and the slow sink.</summary>
    public sealed class OzUeStructurePiece : MonoBehaviour
    {
        internal OzUeStructureRun run; internal Rigidbody rb; internal bool heavy, metal, small, released, resting;
        internal float life;
        float age, lastHit = -9f, sinkT = -1f, sinkLen, sinkDepth; int hits; Vector3 sinkFrom;
        public bool Sinking => sinkT >= 0f;
        public bool Resting => resting;

        void OnCollisionEnter(Collision c)
        {
            if (!released || Sinking) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < 1.8f || age - lastHit < 0.25f || hits >= 4) return;
            lastHit = age; hits++;
            var p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            if (run) run.Landed(this, p, speed);
        }

        void Update()
        {
            float dt = Time.deltaTime; if (dt <= 0f) return;
            age += dt;
            if (Sinking)
            {
                sinkT += dt; float k = Mathf.Clamp01(sinkT / sinkLen);
                transform.position = sinkFrom + Vector3.down * sinkDepth * k * k;
                if (k >= 1f) { OzUeStructureBreak.Sunk++; Destroy(gameObject); }
                return;
            }
            if (released && !resting && age > 1.2f && rb && rb.IsSleeping()) { rb.isKinematic = true; resting = true; }
            if (released && age > 12f && !resting && rb && rb.linearVelocity.sqrMagnitude < 0.05f) { rb.isKinematic = true; resting = true; }   // a slow creep never sleeps
            if (age > life) Sink(OzUeStructureBreak.SinkSeconds);
        }

        /// <summary>Sinks into the floor over `seconds` (its own height), then goes.</summary>
        internal void Sink(float seconds)
        {
            if (Sinking) return;
            if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            var r = GetComponent<Renderer>();
            sinkDepth = r ? r.bounds.size.y + 0.2f : 1.5f; sinkFrom = transform.position; sinkLen = Mathf.Max(0.1f, seconds); sinkT = 0f;
        }

        // the run owns the slab / panel / kerb meshes (OzUeStructureRun.OnDestroy); library meshes are shared: never destroyed here
    }
}
