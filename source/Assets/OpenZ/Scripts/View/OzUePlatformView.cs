using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// numbers, so the drawn shape and the rules cannot drift:
    ///  * the WALK surface: an invisible MeshCollider on ZGame.WalkLayer (deck, ramp, its walls), the trick of the bridges' WalkDeck. ZGame.GroundY
    ///    and GroundPoint then lift every robot, vehicle, gun, ghost, ring and shot end, and the mouse lands on the deck. Its edges run down the
    ///    middle of the closed rail and wall tiles (the sides of the rules' shape, 1 m in): a robot is drawn up to 2 m off its sim spot, so a robot
    ///    on the deck beside the rail does not drop off it and one on the ground beside the wall does not pop up onto it. So the mouse does not rely on
    ///    it alone: ZGame.GroundPoint asks MouseRay, the rules' RayOnBody: a click on a far rail or kerb counts on the platform, not on the floor behind;
    ///  * the BODY: the fort's cream concrete, 2 m panel joints on the tile grid, a dark chevron ramp so the slope reads, a low gunmetal kerb
    ///    (drawn only, 0.4 m), thin edges in the hologram colour so it belongs to the sandbox set. Casts and takes shadows. It goes darker as
    ///    it loses health.
    /// Destroyed (CombatView asks OzUeHooks.PlatformDied, before the deaths of what stood on it: the platform's Death event comes first):
    /// the walk surface goes at once, what lay on the deck goes with it, the model sinks 1.5 s into the dust as a level 16 pyramid does,
    /// with the game's own stone break, dust and sounds.
    /// </summary>
    public sealed class OzUePlatformView : MonoBehaviour
    {
        public ZObject Obj { get; private set; }
        public ZWorld.PlatformInfo Info { get; private set; }
        /// <summary>The kerb's height, drawn only (m).</summary>
        public const float KerbM = 0.4f;
        internal const float KerbWide = 0.2f; const float EdgeWide = 0.03f;                            // in tiles: 0.4 m and 6 cm
        GameObject body, walk;
        MeshRenderer bodyR;
        MaterialPropertyBlock mpb;
        int lastHealth = int.MinValue;
        float sinkT = -1f, sinkDepth; Vector3 bodyAt;
        Mesh bodyMesh, walkMesh;
        public bool Sinking => sinkT >= 0f;

        internal void Bind(ZObject o, ZWorld.PlatformInfo p)
        {
            Obj = o; Info = p;
            var c = ZGame.PxToWorld(o.LocX + o.WidthPix * 0.5, o.LocY + o.HeightPix * 0.5);
            transform.SetPositionAndRotation(c, Quaternion.identity);
            walk = new GameObject("WalkDeck") { layer = ZGame.WalkLayer >= 0 ? ZGame.WalkLayer : 0 };
            walk.transform.SetParent(transform, false);
            walkMesh = OzUePlatformShape.Walk(p, c);
            walk.AddComponent<MeshCollider>().sharedMesh = walkMesh;
            OzViewScans.AddWalkDeck(walk);                                                       // 1.4 speed-up 4: the walk decks' registry (View/OzViewScans.cs)
            body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            bodyMesh = OzUePlatformShape.Body(p, c, KerbM, KerbWide, EdgeWide);
            body.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            bodyR = body.AddComponent<MeshRenderer>();
            bodyR.sharedMaterials = OzUePlatformViews.Materials();
            bodyR.shadowCastingMode = ShadowCastingMode.On; bodyR.receiveShadows = true;
            mpb = new MaterialPropertyBlock();
            Physics.SyncTransforms();                                                                  // the rays of this very frame see it (a drop, then its units from a saved file)
            ZGame.GroundGridChanged(gameObject);                                                       // 1.4 perf_ground_grid: the new deck's cells baked again
        }

        void LateUpdate()
        {
            if (Obj == null) return;
            if (Sinking)
            {
                sinkT += Time.deltaTime;
                float k = Mathf.Clamp01(sinkT / OzUePlatformViews.SinkSec);
                if (body) body.transform.position = bodyAt + Vector3.down * sinkDepth * k * k + new Vector3(Mathf.Sin(sinkT * 47f), 0f, Mathf.Cos(sinkT * 41f)) * 0.04f * (1f - k);
                if (k >= 1f) Destroy(gameObject);
                return;
            }
            if (Obj.Health == lastHealth || !bodyR) return;
            lastHealth = Obj.Health;
            float f = Obj.MaxHealth > 0 ? Mathf.Clamp01((float)Obj.Health / Obj.MaxHealth) : 1f;
            float t = Mathf.Lerp(0.62f, 1f, f);
            mpb.SetColor(OzUePlatformViews.BaseColorId, new Color(t, t, t, 1f));
            bodyR.SetPropertyBlock(mpb, 0); bodyR.SetPropertyBlock(mpb, 1);                            // the concrete; the kerb and the edges keep their colours
        }

        /// <summary>Destroyed: the walk surface off, what lay on the deck gone, the break. Own Destroy when the model has sunk.</summary>
        internal void Fall()
        {
            if (Sinking) return;
            if (walk) walk.SetActive(false);
            ZGame.GroundGridChanged(gameObject);                                                       // 1.4 perf_ground_grid: the deck has gone
            Physics.SyncTransforms();                                                                  // the deaths of this frame already read the floor
            sinkT = 0f; bodyAt = body ? body.transform.position : transform.position; sinkDepth = (float)Info.DeckM + KerbM + 0.2f;
            OzUePlatformViews.TakeLoose(this);
            var p = Mid();
            var fx = FxManager.I;
            if (fx)
            {
                float w = Info.Wide * 2f, d = Info.Deep * 2f, step = 0.3f;
                for (int i = 0; i < 5; i++)
                {
                    var q = new Vector3(p.x + (i == 0 ? 0f : (i % 2 == 0 ? -1f : 1f) * w * step), 1.2f, p.z + (i == 0 ? 0f : (i < 3 ? -1f : 1f) * d * step));
                    fx.DemolitionDust(q, w * 0.6f, d * 0.4f, i == 0 ? 0.8f : 0.55f);
                }
                fx.Breakup(p + Vector3.up * (float)Info.DeckM, "stone", Mathf.Max(w, d) * 0.4f, Info.Kind == ZWorld.PlatformHigh ? 26 : 18, 1.8f);
            }
            OzUePlatformViews.BreakSound("dst_rock_chunks", p + Vector3.up * 2f, 0.9f, 0.9f);
            OzUePlatformViews.BreakSound("dst_rock_crash_large", p + Vector3.up * 2f, 1f, 1f);
            L16Pyramid.Named("dst_l16_pyramid_collapse", "OpenZ/Audio/Sfx/L16/sd_pyramid_collapse_a", p + Vector3.up * 2f, 1f);
            Debug.Log($"{OzUnitEditor.Tag} platform #{Obj.Id} destroyed: its deck is gone");
        }

        /// <summary>The footprint's middle on the floor.</summary>
        public Vector3 Mid() => ZGame.PxToWorld(Obj.LocX + Obj.WidthPix * 0.5, Obj.LocY + Obj.HeightPix * 0.5);

        void OnDestroy() { if (bodyMesh) Destroy(bodyMesh); if (walkMesh) Destroy(walkMesh); }
    }

    /// <summary>The playground's platform views, and the one-line answers the game's files ask for (each returns the game's own value while
    /// there is no platform: every level).</summary>
    public static class OzUePlatformViews
    {
        /// <summary>The room for the tallest thing that can stand on a deck (m), over the deck: the shuttle's hard top there.</summary>
        public const float TallM = 4.5f;
        /// <summary>Barrel limits while a platform stands: a tank on the high deck firing at the ground 15 m away needs 28 degrees down.</summary>
        public const float PitchDownDeg = -32f, PitchUpDeg = 45f;
        /// <summary>A target this far over its aim point stands on a deck (m).</summary>
        public const float OnDeckM = 1f;
        public const float SinkSec = 1.5f;
        internal static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Dictionary<int, OzUePlatformView> views = new Dictionary<int, OzUePlatformView>();
        static Material[] mats;
        /// <summary>Checks: platforms broken, pieces and dead views taken with them.</summary>
        public static int Fell, LooseTaken;

        public static int Count => views.Count;
        public static OzUePlatformView Of(ZObject o) => o != null && views.TryGetValue(o.Id, out var v) && v ? v : null;
        static ZWorld W => OzUnitEditor.I ? OzUnitEditor.I.World : null;

        internal static OzUePlatformView Add(ZObject o)
        {
            var w = W; var p = w != null && o != null ? w.PlatformOf(o.Id) : null;
            if (p == null) return null;
            var have = Of(o); if (have) return have;
            var go = new GameObject("UE_Platform_" + o.Id);
            var v = go.AddComponent<OzUePlatformView>();
            v.Bind(o, p);
            views[o.Id] = v;
            return v;
        }

        /// <summary>A quiet removal: gone at once, no break. The living on it have gone by then (OzUePlayground.PlatformLeaves); what is dead or
        /// loose on it goes the fall's way (TakeLoose), so no wreck, body, turret or mark stays in the air. The walk surface first: a turret in the
        /// air reads the floor.</summary>
        internal static void Remove(ZObject o)
        {
            if (o == null || !views.TryGetValue(o.Id, out var v)) return;
            views.Remove(o.Id);
            if (!v) return;
            v.gameObject.SetActive(false); Physics.SyncTransforms();
            ZGame.GroundGridChanged(v.gameObject);                                                     // 1.4 perf_ground_grid: the deck has gone
            TakeLoose(v);
            Object.Destroy(v.gameObject);
        }

        internal static void Reset() { views.Clear(); }

        /// <summary>OzUeHooks.PlatformDied (CombatView, on the platform's Death event).</summary>
        internal static bool Died(ZObject o)
        {
            var v = Of(o); if (!v) return false;
            views.Remove(o.Id);
            Fell++;
            v.Fall();
            return true;
        }

        // ---------------------------------------------------------------------------------- the one-line answers
        /// <summary>View/OzShuttleSkyTop.cs Query: the deck's height + the room for what stands on it (float.MinValue off a platform).</summary>
        public static float TopAt(float x, float z)
        {
            if (views.Count == 0) return float.MinValue;
            var w = W; if (w == null) return float.MinValue;
            var px = ZGame.WorldToPx(new Vector3(x, 0f, z));
            if (w.PlatformAtTile(px.x >> 4, px.y >> 4) == 0) return float.MinValue;
            return (float)w.PlatformHeightM(px.x, px.y) + TallM;
        }

        /// <summary>The platform's own height at a point (0 off one): View/OzPlayableShuttle.cs takes it off the rule height, so the ship does
        /// not climb 9 m over a deck for nothing; only the sky top lifts it.</summary>
        public static float UnderM(float x, float z)
        {
            if (views.Count == 0) return 0f;
            var w = W; if (w == null) return 0f;
            var px = ZGame.WorldToPx(new Vector3(x, 0f, z));
            return (float)w.PlatformHeightM(px.x, px.y);
        }

        /// <summary>View/OzTowerAim.cs Aim: a round at a unit on a deck whose scatter lands off the edge is drawn at the unit's height, never
        /// at the wall's foot; every other target: `ground` unchanged.</summary>
        public static Vector3 Aim(ZObject t, Vector3 ground)
        {
            if (views.Count == 0 || t == null || !t.IsUnit) return ground;
            var g = ZGame.I;
            if (!g || !g.Views.TryGetValue(t.Id, out var v) || !v) return ground;
            float y = v.transform.position.y;
            if (y - ground.y > OnDeckM) { ground.y = y; AimsLifted++; }
            return ground;
        }
        /// <summary>Checks: aim points lifted onto a deck.</summary>
        public static int AimsLifted;

        /// <summary>View/UnitAim.cs ClampPitch: the barrel's limits, wider while a platform stands (the Unit editor only).</summary>
        public static float PitchMin(float min) => views.Count > 0 ? Mathf.Min(min, PitchDownDeg) : min;
        public static float PitchMax(float max) => views.Count > 0 ? Mathf.Max(max, PitchUpDeg) : max;

        /// <summary>View/UnitView.Deaths.cs: this unit died with its platform: it comes down (a vehicle: the rigidbody wreck, a gun: the fallen
        /// tower gun's way), it does not stay where the deck was.</summary>
        public static bool Fallen(UnitView v) => v != null && v.Obj != null && v.Obj.World != null && v.Obj.World.PlatformFallen(v.Obj.Id);

        /// <summary>Marks and the drop preview: the height a floor mark is drawn at; 0 with no platform on the field, as before.</summary>
        public static float MarkY(ZGame g, Vector3 p) => views.Count > 0 && g ? g.GroundY(p.x, p.z) : 0f;

        /// <summary>The platform under a screen point (its deck, ramp or walls, by the mouse's ground ray), null = none.</summary>
        public static ZObject Under(Vector2 screen)
        {
            if (views.Count == 0) return null;
            var w = W; var g = ZGame.I; var cam = OzUeDraw.Cam;
            if (w == null || !g || !cam) return null;
            var ray = cam.ScreenPointToRay(screen);
            if (!g.GroundPoint(ray, out var p)) return null;
            var px = ZGame.WorldToPx(p + ray.direction * 0.05f);                                       // a point on a wall's face lies on the footprint's very edge
            var info = w.PlatformOf(w.PlatformAtTile(px.x >> 4, px.y >> 4));
            return info != null ? info.O : null;
        }

        /// <summary>View/ZGame.cs GroundPoint, the camera's rays only (a shot's ray keeps the walk surface, View/HitSurface.cs): `p` is what the ray hit; a ray that
        /// meets a platform's drawn body first, on a rail, a kerb or a wall, ends on the walkable spot nearest that (ZWorld.PlatformInfo.RayOnBody). So everything the
        /// mouse does, drop, preview, hover, pick, move, attack, counts on the platform. Nothing with no platform on the field: every level.</summary>
        public static void MouseRay(Ray ray, ref Vector3 p)
        {
            if (views.Count == 0) return;
            var cam = OzUeDraw.Cam;
            if (!cam || (ray.origin - cam.transform.position).magnitude > cam.nearClipPlane * 2f + 1f) return;
            Vector3 o = ray.origin, d = ray.direction;
            double before = (p - o).magnitude;
            foreach (var v in views.Values)
            {
                if (!v || !v.Info.RayOnBody(o.x * 8.0, -o.z * 8.0, o.y, d.x * 8.0, -d.z * 8.0, d.y, before, OzUePlatformView.KerbM, OzUePlatformView.KerbWide, out double t, out double across, out double along)) continue;
                before = t; p = Point(v.Info, (float)across, (float)along, (float)v.Info.HeightAt(along)); RimRays++;
            }
        }
        /// <summary>Checks: rays the drawn body took from the walk surface or the floor.</summary>
        public static int RimRays;

        /// <summary>A world point on a platform: across 0 .. 10, along 0 (back wall) .. Rows (ramp's foot) tiles.</summary>
        public static Vector3 Point(ZWorld.PlatformInfo p, float across, float along, float y)
        {
            p.Map(across, along, out double px, out double py);
            var v = ZGame.PxToWorld(px, py); v.y = y; return v;
        }

        // ---------------------------------------------------------------------------------- the fall
        /// <summary>Everything dead or loose on the footprint goes with the deck: the dead units' views (not those that die with it now, they
        /// fall), a settled body lying on it from a view that stands beside it (UnitView.BodyOn), a turret thrown onto it from beside it
        /// (UnitView.TossedOffDeck), wreck pieces, shuttle pieces, debris bodies, rubble, scorch marks, burning ground (OpenZPyroFire.GroundGone).
        /// The walk surface must be off already.</summary>
        internal static void TakeLoose(OzUePlatformView v)
        {
            var g = ZGame.I; var w = W; var o = v.Obj;
            if (!g || w == null || o == null) return;
            Vector3 a = ZGame.PxToWorld(o.LocX, o.LocY), b = ZGame.PxToWorld(o.LocX + o.WidthPix, o.LocY + o.HeightPix);
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.z, b.z), z1 = Mathf.Max(a.z, b.z);
            bool On(Vector3 p) => p.x >= x0 && p.x <= x1 && p.z >= z0 && p.z <= z1 && p.y > 0.4f;
            int n = 0;
            foreach (var kv in g.Views)
            {
                var u = kv.Value;
                if (!u || !u.gameObject.activeInHierarchy) continue;
                var x = w.GetAny(kv.Key);
                if (x != null && !x.Destroyed && x.KillAt < 0) continue;
                if (!w.PlatformFallen(kv.Key) && (On(u.transform.position) || u.BodyOn(On))) { u.Hide(); n++; }
                else if (u.TossedOffDeck(On)) n++;
            }
            n += OzUePlayground.SweepDebrisIn(On);
            if (FxManager.I) FxManager.I.ClearRubble(v.Mid(), Mathf.Min(Mathf.Abs(x1 - x0), Mathf.Abs(z1 - z0)) * 0.5f);
            ScorchDecals.ClearOver(a, b);
            OpenZPyroFire.GroundGone(a, b);
            LooseTaken += n;
        }

        internal static void BreakSound(string ev, Vector3 at, float vol, float pitch)
        {
            if (AudioDirector.Has(ev)) AudioDirector.Fire(ev, at, -1, vol, pitch);
            else OzSoundLog.Add("UE", ev, null, "DROP no event", 0f, at);
        }

        // ---------------------------------------------------------------------------------- materials
        /// <summary>The body's materials, in the mesh's submesh order: deck and walls, ramp, kerb, edges.</summary>
        internal static Material[] Materials()
        {
            if (mats != null && mats[0]) { mats[3].SetColor(BaseColorId, OzUeObstacleViews.HoloColor); return mats; }
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            Material M(string name, Texture t, Color c, float smooth)
            {
                var m = new Material(sh) { name = name, hideFlags = HideFlags.DontSave };
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                return m;
            }
            mats = new[]
            {
                M("UePlatform_Concrete", OzUePlatformArt.Concrete(false), Color.white, 0.15f),
                M("UePlatform_Ramp", OzUePlatformArt.Concrete(true), Color.white, 0.15f),
                M("UePlatform_Kerb", null, new Color(0.27f, 0.28f, 0.30f), 0.3f),                      // the fort's pedestal gunmetal (View/TowerMounts.cs pedestalColor)
                M("UePlatform_Edge", null, OzUeObstacleViews.HoloColor, 0.1f),
            };
            return mats;
        }
    }

    public partial class UnitView
    {
        /// <summary>A platform's deck has gone (OzUePlatformViews.TakeLoose): a settled body's bones are kinematic and stay where they came to rest, whatever holds them
        /// up, and the root may stand elsewhere (thrown onto the deck from beside the wall). True = a bone of this settled body lies on it. A body still in flight falls by itself.</summary>
        internal bool BodyOn(System.Func<Vector3, bool> on)
        {
            if (!TryGetComponent<RobotRagdoll>(out var rd) || !rd.Frozen) return false;
            foreach (var rb in GetComponentsInChildren<Rigidbody>()) if (on(rb.transform.position)) return true;
            return false;
        }

        /// <summary>A platform's deck has gone (OzUePlatformViews.TakeLoose) and this dead unit's tossed turret was thrown at it (TossTurret fixes the
        /// landing height at the throw; the turret is no child of the view): lying on the deck it goes with the deck; still in the air it comes down
        /// to the floor from where it is. True = one of those.</summary>
        internal bool TossedOffDeck(System.Func<Vector3, bool> on)
        {
            if (!tossed) return false;
            if (tossLanded)
            {
                if (!on(tossed.position)) return false;
                Destroy(tossed.gameObject); tossed = null;
                return true;
            }
            if (!on(tossTo)) return false;
            tossTo.y = game.GroundY(tossTo.x, tossTo.z) + 0.35f;
            tossFrom = tossed.position;
            tossDur = Mathf.Max(0.1f, tossDur - tossT, Mathf.Sqrt(2f * Mathf.Max(0f, tossFrom.y - tossTo.y) / 9.81f));
            tossApex = 0f; tossT = 0f;
            return true;
        }
    }

    /// <summary>The two meshes of a platform, from the rules' numbers. Local to the footprint's middle `c` (y from the floor).</summary>
    internal static class OzUePlatformShape
    {
        sealed class Build
        {
            public readonly List<Vector3> v = new List<Vector3>(); public readonly List<Vector3> n = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int>[] t;
            public Build(int subs) { t = new List<int>[subs]; for (int i = 0; i < subs; i++) t[i] = new List<int>(); }

            /// <summary>One flat quad a-b-c-d (in order round it), facing `face`; its uv = (x, y) of each corner in texture repeats.</summary>
            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 face, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-8f) normal = Vector3.Cross(c - a, d - a);
                normal.Normalize();
                bool flip = Vector3.Dot(normal, face) < 0f;
                if (flip) normal = -normal;
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
                uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
                // Unity's front face: Cross(b - a, c - a) points at the viewer (as OzUeObstacleViews.Face and RecalculateNormals)
                var tl = t[sub];
                if (flip) { tl.Add(i); tl.Add(i + 2); tl.Add(i + 1); tl.Add(i); tl.Add(i + 3); tl.Add(i + 2); }
                else { tl.Add(i); tl.Add(i + 1); tl.Add(i + 2); tl.Add(i); tl.Add(i + 2); tl.Add(i + 3); }
            }

            public Mesh Make(string name, bool bounds = true)
            {
                var m = new Mesh { name = name, subMeshCount = t.Length };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
                for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i);
                if (bounds) m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>The walk surface's edges, in tiles in from the rules' shape: down the middle of the closed rail.</summary>
        internal const float WalkInset = 0.5f;
        const float TilesPerRepeat = 4f;                                                               // one texture repeat = 8 m = 4 tiles

        static Vector2 Uv(float a, float b) => new Vector2(a / TilesPerRepeat, b / TilesPerRepeat);

        /// <summary>The whole outer shell: deck, ramp, the two sides, the back wall (add = the kerb's height on the walls of the drawn body).</summary>
        static void Shell(Build b, ZWorld.PlatformInfo p, Vector3 c, float add, int subDeck, int subRamp, float inset = 0f)
        {
            float A0 = inset, W = ZWorld.PlatformWideTiles - inset, B0 = inset, top = ZWorld.PlatformBackRows + ZWorld.PlatformDeckRows, L = p.Rows, H = (float)p.DeckM;
            Vector3 P(float across, float along, float y) => OzUePlatformViews.Point(p, across, along, y) - c;
            Vector3 up = Vector3.up, side0 = (P(0f, 1f, 0f) - P(1f, 1f, 0f)).normalized, side1 = -side0, back = (P(1f, 0f, 0f) - P(1f, 1f, 0f)).normalized;
            b.Quad(subDeck, P(A0, B0, H), P(W, B0, H), P(W, top, H), P(A0, top, H), up, Uv(0f, 0f), Uv(W, 0f), Uv(W, top), Uv(0f, top));
            b.Quad(subRamp, P(A0, top, H), P(W, top, H), P(W, L, 0f), P(A0, L, 0f), up, Vector2.zero, Vector2.right, new Vector2(1f, (L - top) / TilesPerRepeat), new Vector2(0f, (L - top) / TilesPerRepeat));   // one chevron across its width
            float yUv(float y) => y / 2f;                                                              // tiles of height
            b.Quad(subDeck, P(A0, B0, 0f), P(A0, top, 0f), P(A0, top, H + add), P(A0, B0, H + add), side0, Uv(0f, 0f), Uv(top, 0f), Uv(top, yUv(H + add)), Uv(0f, yUv(H + add)));
            b.Quad(subDeck, P(W, B0, 0f), P(W, top, 0f), P(W, top, H + add), P(W, B0, H + add), side1, Uv(0f, 0f), Uv(top, 0f), Uv(top, yUv(H + add)), Uv(0f, yUv(H + add)));
            b.Quad(subDeck, P(A0, top, 0f), P(A0, L, 0f), P(A0, L, add), P(A0, top, H + add), side0, Uv(top, 0f), Uv(L, 0f), Uv(L, yUv(add)), Uv(top, yUv(H + add)));
            b.Quad(subDeck, P(W, top, 0f), P(W, L, 0f), P(W, L, add), P(W, top, H + add), side1, Uv(top, 0f), Uv(L, 0f), Uv(L, yUv(add)), Uv(top, yUv(H + add)));
            b.Quad(subDeck, P(A0, B0, 0f), P(W, B0, 0f), P(W, B0, H + add), P(A0, B0, H + add), back, Uv(0f, 0f), Uv(W, 0f), Uv(W, yUv(H + add)), Uv(0f, yUv(H + add)));
        }

        /// <summary>The invisible walk surface: the rules' shape, exactly.</summary>
        internal static Mesh Walk(ZWorld.PlatformInfo p, Vector3 c)
        {
            var b = new Build(1);
            Shell(b, p, c, 0f, 0, 0, WalkInset);
            return b.Make("UePlatformWalk");
        }

        /// <summary>The drawn body: submeshes 0 deck and walls, 1 ramp, 2 kerb, 3 edges.</summary>
        internal static Mesh Body(ZWorld.PlatformInfo p, Vector3 c, float kerb, float kerbWide, float edge)
        {
            var b = new Build(4);
            Shell(b, p, c, kerb, 0, 1);
            float W = ZWorld.PlatformWideTiles, top = ZWorld.PlatformBackRows + ZWorld.PlatformDeckRows, L = p.Rows, H = (float)p.DeckM;
            Vector3 P(float across, float along, float y) => OzUePlatformViews.Point(p, across, along, y) - c;
            float Y(float along) => (float)p.HeightAt(along);
            Vector2 u0 = Vector2.zero;
            void Top(int sub, float a0, float a1, float l0, float l1, float add)
            {
                b.Quad(sub, P(a0, l0, Y(l0) + add), P(a1, l0, Y(l0) + add), P(a1, l1, Y(l1) + add), P(a0, l1, Y(l1) + add), Vector3.up, u0, u0, u0, u0);
            }
            void InnerSide(float a, float l0, float l1, Vector3 face)
            {
                b.Quad(2, P(a, l0, Y(l0)), P(a, l1, Y(l1)), P(a, l1, Y(l1) + kerb), P(a, l0, Y(l0) + kerb), face, u0, u0, u0, u0);
            }
            Vector3 toRight = (P(1f, 1f, 0f) - P(0f, 1f, 0f)).normalized, toFoot = (P(1f, 1f, 0f) - P(1f, 0f, 0f)).normalized;
            foreach (var (l0, l1) in new[] { (0f, top), (top, L) })
            {
                Top(2, 0f, kerbWide, l0, l1, kerb); Top(2, W - kerbWide, W, l0, l1, kerb);
                InnerSide(kerbWide, l0, l1, toRight); InnerSide(W - kerbWide, l0, l1, -toRight);
                Top(3, 0f, edge, l0, l1, kerb + 0.01f); Top(3, W - edge, W, l0, l1, kerb + 0.01f);
            }
            Top(2, kerbWide, W - kerbWide, 0f, kerbWide, kerb);
            b.Quad(2, P(kerbWide, kerbWide, H), P(W - kerbWide, kerbWide, H), P(W - kerbWide, kerbWide, H + kerb), P(kerbWide, kerbWide, H + kerb), toFoot, u0, u0, u0, u0);
            Top(3, edge, W - edge, 0f, edge, kerb + 0.01f);
            b.Quad(3, P(0f, top - 0.02f, H + 0.02f), P(W, top - 0.02f, H + 0.02f), P(W, top + 0.02f, Y(top + 0.02f) + 0.02f), P(0f, top + 0.02f, Y(top + 0.02f) + 0.02f), Vector3.up, u0, u0, u0, u0);   // the crest
            var m = b.Make("UePlatformBody");
            var bo = m.bounds; bo.Expand(new Vector3(1f, (float)p.DeckM * 2f + 2f, 1f)); m.bounds = bo;   // it sinks into the floor
            return m;
        }
    }

    /// <summary>The platform's art, code-made once: concrete (the fort's wall cream, a very subtle grain, 2 m joints on the tile grid), the
    /// ramp's with dark chevrons, and the two dock pictures in the obstacle picture's style.</summary>
    public static class OzUePlatformArt
    {
        static Texture2D concrete, ramp;
        static readonly Texture2D[] pics = new Texture2D[2];

        internal static Texture2D Concrete(bool chevrons)
        {
            var have = chevrons ? ramp : concrete; if (have) return have;
            const int N = 512;                                                                         // 8 m a repeat: a joint every 128 px (2 m, one tile)
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { name = chevrons ? "ue_platform_ramp" : "ue_platform_concrete", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[N * N];
            Color cream = new Color(0.66f, 0.62f, 0.54f), dark = new Color(0.20f, 0.21f, 0.23f), joint = new Color(0.40f, 0.38f, 0.34f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float g = Mathf.PerlinNoise(x * 0.012f + 3.1f, y * 0.012f + 7.7f) * 0.6f + Mathf.PerlinNoise(x * 0.09f, y * 0.09f + 40f) * 0.4f;
                    var c = cream * (0.9f + 0.2f * g);
                    uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663); h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                    if (h % 500u == 0u) c *= 0.82f;                                                    // sparse pores
                    if (chevrons)
                    {   // two bands a repeat, pointing up the ramp
                        float v = (y + Mathf.Abs(x - N * 0.5f) * 0.9f) % (N * 0.5f);
                        if (v < 64f) c = Color.Lerp(c, dark, 0.72f);
                    }
                    if ((chevrons ? x * 10 % N < 15 : x % 128 < 2) || y % 128 < 2) c = Color.Lerp(c, joint, 0.75f);   // the ramp's texture is 10 tiles wide, the deck's 4
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            t.SetPixels(px); t.Apply(true, true);
            if (chevrons) ramp = t; else concrete = t;
            return t;
        }

        // the picture on the obstacle picture's 64 grid: a side view, ground at y 50, the ramp running down to the right
        static readonly Vector2[][] Shapes =
        {
            new[] { new Vector2(10f, 50f), new Vector2(10f, 36f), new Vector2(29f, 36f), new Vector2(54f, 50f) },          // medium
            new[] { new Vector2(10f, 50f), new Vector2(10f, 18f), new Vector2(25f, 18f), new Vector2(56f, 50f) },          // high
        };

        /// <summary>The dock's picture of a platform (128 x 128).</summary>
        public static Texture2D Picture(string id)
        {
            int k = id == OzUePlayground.PlatformHigh ? 1 : 0;
            if (pics[k]) return pics[k];
            const int N = 128, S = 4; const float g = 64f / N;
            var poly = Shapes[k];
            Color well = OzUeArt.ExtraWell, face = new Color32(0xBF, 0xE3, 0xFF, 255), line = OzUeArt.CubeTint;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { name = k == 0 ? "ue_platform_mid" : "ue_platform_high", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                        {
                            float u = (x + (sx + 0.5f) / S) * g, v = (y + (sy + 0.5f) / S) * g;           // 64-grid point, y down
                            Color c = well;
                            if (v > 50f && v < 53f && u > 6f && u < 59f) c = Color.Lerp(c, Color.black, 0.35f);   // its shadow on the ground
                            float d = Inside(poly, u, v);
                            if (d > 0f)
                            {
                                c = Color.Lerp(c, face, 0.14f);
                                if (Mathf.Repeat(v, 6f) < 0.5f) c = Color.Lerp(c, line, 0.3f);              // panel lines
                            }
                            if (Mathf.Abs(d) <= 0.7f) c = line;
                            acc += c;
                        }
                    px[(N - 1 - y) * N + x] = acc / (S * S);
                }
            t.SetPixels(px); t.Apply(true, true);
            pics[k] = t;
            return t;
        }

        /// <summary>Distance to the outline, plus inside, minus outside.</summary>
        static float Inside(Vector2[] poly, float x, float y)
        {
            bool inside = false; float best = 1e9f;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], e = b - a, p = new Vector2(x, y);
                if ((b.y > y) != (a.y > y) && x < (a.x - b.x) * (y - b.y) / (a.y - b.y) + b.x) inside = !inside;
                float s = Mathf.Clamp01(Vector2.Dot(p - a, e) / Mathf.Max(1e-6f, e.sqrMagnitude));
                best = Mathf.Min(best, (p - (a + e * s)).magnitude);
            }
            return inside ? best : -best;
        }
    }
}
