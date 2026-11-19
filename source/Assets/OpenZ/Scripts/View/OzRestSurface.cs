// OpenZ (GPL-3.0)
// View only: nothing here is read by the rules.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit robot_pieces_ground_r11b (A35 fix round): what a dead robot's body or piece really lies on. Round 11 put the bodies down on
    /// ZGame.GroundY (the walk layer's top seen from 200 m up): the Unit editor's platform rails and kerbs, rocks, guns, other wrecks and a
    /// deck's edge are not in it, so a body or piece over them rested on a height that was not under its own points. Now:
    /// <list type="bullet">
    /// <item><see cref="Support"/>: under a point the highest of the walk layer (seen from 1.5 m over the point), GroundY and what stands still
    /// right under it (OzWreckGround.Under: one ray down to the colliders); </item>
    /// <item><see cref="Gap"/>: the drawn points binned in cells (at most <see cref="MaxSide"/> x <see cref="MaxSide"/>, at least <see cref="CellM"/> m),
    /// the lowest point of each cell against the support right under it (a ray), so a slope, a step, a rail or a deck edge under one part of a body
    /// is seen by that part;</item>
    /// <item><see cref="OverEdge"/>: the support under the body's middle is far below the ground's plane through its contact: it is over an edge
    /// (a deck, a ramp's side, a rail): it slides off (UnitView.BodyGround);</item>
    /// <item>the ground's slope under a body (<see cref="Normal"/>), a box face to lie on (<see cref="LieOnFace"/>), a landing point kept inside the
    /// map (<see cref="InsideMap"/>), the loose heads' and cans' seat when they freeze (<see cref="Frozen"/>, <see cref="OzRestWatch"/>), EvilZod's
    /// burst pieces' rest (<see cref="EvilRest"/>); fix round 1: a death-clip body's gun out of its hands (<see cref="OzDroppedGun"/>), the largest
    /// face (<see cref="LieFlat"/>), a frozen head looked at every frame.</item>
    /// </list>
    /// Switch <see cref="On"/> (false = round 11 exactly).
    /// </summary>
    public static class OzRestSurface
    {
        /// <summary>false = exactly as round 11 (GroundY only, no tilt, no edge slide, no seats).</summary>
        public static bool On = true;
        /// <summary>Smallest cell (m), the lowest drawn point this much over its support (m), a drop under the middle that is an edge (m), the steepest
        /// ground a body lies along (deg), cells further than RayBandM over the lowest one (over GroundY) get no ray (m), the most a piece is lifted onto
        /// what is under it (m), a frozen head this far over its support falls again (m).</summary>
        public static float CellM = 0.25f, LiftM = 0.01f, EdgeDropM = 0.3f, MaxTiltDeg = 30f, RayBandM = 1.2f, MaxRaiseM = 0.6f, RefallM = 0.06f;
        public static int MaxSide = 6;
        /// <summary>Counters (Play checks).</summary>
        public static int Measures, Rays, EdgeHits, PiecesSeated, HeadsSeated, HeadsRefell, EvilSeated, PancakesSeated, OffMap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Statics() { On = true; Measures = Rays = EdgeHits = PiecesSeated = HeadsSeated = HeadsRefell = EvilSeated = PancakesSeated = OffMap = 0; cells.Clear(); }

        public static string Status() =>
            $"real surface (A35 r11b): {(On ? "ON" : "OFF")}, measures {Measures} ({Rays} rays), over an edge {EdgeHits}, pop pieces seated {PiecesSeated}, heads / cans seated {HeadsSeated} (fell again {HeadsRefell}, seated again {OzRestWatch.Reseated}), EvilZod pieces seated {EvilSeated}, pancakes seated {PancakesSeated}, off the map {OffMap}";

        // ------------------------------------------------------------------ support
        /// <summary>The highest solid support under p (OzWreckGround.Under: the walk layer from 1.5 m over p, GroundY, what stands still right under p;
        /// `self` and its children never). Nothing under it: GroundY.</summary>
        public static float Support(Vector3 p, Transform self)
        {
            Rays++;
            float s = OzWreckGround.Under(p, self);
            if (s <= float.MinValue) { var g = ZGame.I; s = g ? g.GroundY(p.x, p.z) : 0f; }
            return s;
        }

        static readonly Dictionary<long, int> cells = new Dictionary<long, int>(128);
        static readonly List<int> cellPts = new List<int>(128);
        static readonly List<float> cellGy = new List<float>(128), cellE = new List<float>(128);

        /// <summary>The smallest gap of the points over the support right under them (see the class notes: per cell its point lowest over the ground's
        /// plane, one ray) and its point and support. wallM: a point
        /// more than this UNDER its support is inside a wall (a deck's side, a ramp's foot) and left out, unless every point is (then the plain lowest).
        /// NaN: no point.</summary>
        public static float Gap(List<Vector3> pts, Transform self, float wallM, out Vector3 low, out float lowS)
        {
            low = Vector3.zero; lowS = 0f;
            var g = ZGame.I; if (pts == null || pts.Count == 0 || !g) return float.NaN;
            Measures++;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int i = 0; i < pts.Count; i++) { var p = pts[i]; if (p.x < x0) x0 = p.x; if (p.x > x1) x1 = p.x; if (p.z < z0) z0 = p.z; if (p.z > z1) z1 = p.z; }
            float cell = Mathf.Max(CellM, Mathf.Max(x1 - x0, z1 - z0) / Mathf.Max(1, MaxSide)), inv = 1f / cell;
            // the ground's plane under the points (5 samples): in each cell the point lowest OVER that plane is its candidate (on a slope the point lowest in
            // world height is the cell's downhill one, up to cell x slope off); a step under them: no plane (the lowest in world height)
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, hx = Mathf.Max(0.3f, (x1 - x0) * 0.5f), hz = Mathf.Max(0.3f, (z1 - z0) * 0.5f);
            float gc = g.GroundY(cx, cz), gxp = g.GroundY(cx + hx, cz), gxn = g.GroundY(cx - hx, cz), gzp = g.GroundY(cx, cz + hz), gzn = g.GroundY(cx, cz - hz);
            float sb = (gxp - gxn) / (2f * hx), sc = (gzp - gzn) / (2f * hz);
            bool plane = !(Mathf.Abs((gxp + gxn) * 0.5f - gc) > 0.25f || Mathf.Abs((gzp + gzn) * 0.5f - gc) > 0.25f || Mathf.Abs(sb) > 1f || Mathf.Abs(sc) > 1f);
            if (!plane) sb = sc = 0f;
            cells.Clear(); cellPts.Clear(); cellGy.Clear(); cellE.Clear();
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i]; float e = plane ? p.y - sb * (p.x - cx) - sc * (p.z - cz) : p.y - g.GroundY(p.x, p.z);   // r11b fix round 1: a step or a break under them (no plane): each point over the ground right under it (was its world height: a pancake at the deck / ramp break 2.5 cm into the deck)
                long key = ((long)Mathf.FloorToInt((p.x - x0) * inv) << 20) | (long)(Mathf.FloorToInt((p.z - z0) * inv) & 0xFFFFF);
                if (cells.TryGetValue(key, out int j)) { if (e < cellE[j]) { cellPts[j] = i; cellE[j] = e; } }
                else { cells[key] = cellPts.Count; cellPts.Add(i); cellE.Add(e); }
            }
            float minG = float.MaxValue, minAll = float.MaxValue;                                         // the band's base: the lowest over GroundY that is not deep inside it
            for (int c = 0; c < cellPts.Count; c++) { var p = pts[cellPts[c]]; float d = p.y - g.GroundY(p.x, p.z); cellGy.Add(d); if (d < minAll) minAll = d; if (d >= -wallM && d < minG) minG = d; }
            if (minG == float.MaxValue) minG = minAll;
            float gap = float.MaxValue, any = float.MaxValue, anyS = 0f; Vector3 anyP = Vector3.zero;
            for (int c = 0; c < cellPts.Count; c++)
            {
                if (cellGy[c] > minG + RayBandM) continue;                                               // far over the lowest: no rail or kerb that tall
                var p = pts[cellPts[c]]; float s = Support(p, self), d = p.y - s;
                if (d < any) { any = d; anyP = p; anyS = s; }
                if (d < -wallM) continue;
                if (d < gap) { gap = d; low = p; lowS = s; }
            }
            if (gap == float.MaxValue)
            {
                if (any == float.MaxValue) return float.NaN;
                gap = any; low = anyP; lowS = anyS;
            }
            return gap;
        }

        /// <summary>The middle of the points (their mean).</summary>
        public static Vector3 Mid(List<Vector3> pts)
        {
            var m = Vector3.zero; if (pts == null || pts.Count == 0) return m;
            for (int i = 0; i < pts.Count; i++) m += pts[i];
            return m / pts.Count;
        }

        /// <summary>The ground's normal under (x, z) from four GroundY samples r away; a step or an edge under it (the samples bend), or ground steeper than
        /// MaxTiltDeg: up.</summary>
        public static Vector3 Normal(ZGame g, float x, float z, float r)
        {
            if (!g || !On) return Vector3.up;
            float a = g.GroundY(x - r, z), b = g.GroundY(x + r, z), c = g.GroundY(x, z - r), d = g.GroundY(x, z + r), m = g.GroundY(x, z);
            float bend = 0.2f * r + 0.03f;
            if (Mathf.Abs((a + b) * 0.5f - m) > bend || Mathf.Abs((c + d) * 0.5f - m) > bend) return Vector3.up;
            var n = new Vector3(a - b, 2f * r, c - d).normalized;
            return n.y < Mathf.Cos(MaxTiltDeg * Mathf.Deg2Rad) ? Vector3.up : n;
        }

        public static Vector3 UpAt(Vector3 p) => On && ZGame.I ? Normal(ZGame.I, p.x, p.z, 0.8f) : Vector3.up;

        /// <summary>The body rests (contact `low` on support lowS) with its middle over a surface far under the ground's plane through the contact (n):
        /// it hangs over an edge (a deck, a ramp's side, a rail it lies across). dir = from the contact to the middle (flat).</summary>
        public static bool OverEdge(Vector3 mid, Vector3 low, float lowS, Vector3 n, Transform self, out Vector3 dir)
        {
            dir = mid - low; dir.y = 0f;
            if (!On || dir.sqrMagnitude < 0.01f) return false;                                          // the contact is right under its middle
            float pred = lowS - (n.x * (mid.x - low.x) + n.z * (mid.z - low.z)) / Mathf.Max(0.5f, n.y);
            float s = Support(new Vector3(mid.x, pred + 0.3f, mid.z), self);
            if (pred - s <= EdgeDropM) return false;
            dir.Normalize(); EdgeHits++;
            return true;
        }

        /// <summary>The box face a resting piece lies on (as round 10's RobotPopDeath.FitFace): the most level face whose middle stays over that face's
        /// edge at the tilt it has; turned the smallest way so that face lies along n. e = the box's half sizes (scaled).</summary>
        public static Quaternion LieOnFace(Quaternion q, Vector3 e, Vector3 n)
        {
            Vector3 ax = q * Vector3.right, ay = q * Vector3.up, az = q * Vector3.forward;
            float dx = Vector3.Dot(ax, n), dy = Vector3.Dot(ay, n), dz = Vector3.Dot(az, n);
            float adx = Mathf.Abs(dx), ady = Mathf.Abs(dy), adz = Mathf.Abs(dz);
            int best = -1; float bd = -1f;
            if (e.x * (1f - adx * adx) <= (ady * e.y + adz * e.z) * adx + 1e-5f && adx > bd) { best = 0; bd = adx; }
            if (e.y * (1f - ady * ady) <= (adx * e.x + adz * e.z) * ady + 1e-5f && ady > bd) { best = 1; bd = ady; }
            if (e.z * (1f - adz * adz) <= (adx * e.x + ady * e.y) * adz + 1e-5f && adz > bd) { best = 2; bd = adz; }
            if (best < 0) best = adx >= ady && adx >= adz ? 0 : ady >= adz ? 1 : 2;
            var a = best == 0 ? (dx < 0f ? -ax : ax) : best == 1 ? (dy < 0f ? -ay : ay) : (dz < 0f ? -az : az);
            return Quaternion.FromToRotation(a, n) * q;
        }

        /// <summary>r11b fix round 1: the LARGEST box face lies along n (the axis of the smallest half size turned the smallest way onto n): a long piece
        /// lies down instead of standing on its end (the Play check: EvilZod's leg pieces stood on their ends, 1.49 m of them in the ground).</summary>
        public static Quaternion LieFlat(Quaternion q, Vector3 e, Vector3 n)
        {
            Vector3 a = e.x <= e.y && e.x <= e.z ? q * Vector3.right : e.y <= e.z ? q * Vector3.up : q * Vector3.forward;
            if (Vector3.Dot(a, n) < 0f) a = -a;
            return Quaternion.FromToRotation(a, n) * q;
        }

        // ------------------------------------------------------------------ the map
        /// <summary>p lies inside the map (the world's tiles), margin m in from its sides. No map known: true.</summary>
        public static bool InMap(Vector3 p, float margin)
        {
            var g = ZGame.I; var w = g ? g.World : null; var map = w != null ? w.Map : null;
            if (map == null || map.width <= 0 || map.height <= 0) return true;
            float wx = map.width * 2f, wz = map.height * 2f;                                            // a tile = 16 px = 2 m (ZGame.PxToWorld: px / 8)
            return p.x >= margin && p.x <= wx - margin && -p.z >= margin && -p.z <= wz - margin;
        }

        /// <summary>A thrown body's landing point: kept inside the map (MapMarginM in), shortened along its flight.</summary>
        public static Vector3 InsideMap(Vector3 from, Vector3 to)
        {
            if (!On || InMap(to, MapMarginM)) return to;
            var g = ZGame.I; var map = g && g.World != null ? g.World.Map : null; if (map == null) return to;
            float wx = map.width * 2f, wz = map.height * 2f, m = MapMarginM;
            var c = new Vector3(Mathf.Clamp(to.x, m, Mathf.Max(m, wx - m)), to.y, -Mathf.Clamp(-to.z, m, Mathf.Max(m, wz - m)));
            OffMap++;
            return c;
        }

        public static float MapMarginM = 2f;

        // ------------------------------------------------------------------ the loose head (DV116) and the antics can: their seat when they freeze
        static readonly List<Vector3> buf = new List<Vector3>(512);
        static readonly List<Renderer> rbuf = new List<Renderer>(16);
        static Mesh skinBake;

        /// <summary>AnticCan.Throw: the collider's box also takes in the skins under h as drawn now (the heroes' loose head is a copy of the body's skin
        /// that only the head bones move: its box came from the small rigid pieces on the face alone, and the drawn head lay 0.16-0.26 m in the ground).</summary>
        public static void SkinBox(Transform h, ref Bounds b, ref bool any)
        {
            if (!On || !h) return;
            foreach (var sk in h.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!sk.sharedMesh) continue;
                if (!skinBake) skinBake = new Mesh { name = "OzRestSurface skin box" };
                sk.BakeMesh(skinBake, true); var v = skinBake.vertices;                                     // useScale true: the renderer's own space (its localToWorldMatrix gives the world)
                var m = h.worldToLocalMatrix * sk.transform.localToWorldMatrix;
                for (int i = 0; i < v.Length; i++)
                {
                    var lp = m.MultiplyPoint3x4(v[i]);
                    if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
                }
            }
        }

        /// <summary>The drawn gap of everything drawn under t over the support right under it (NaN: nothing drawn).</summary>
        public static float DrawnGap(Transform t, string type, float wallM)
        {
            if (!t) return float.NaN;
            if (OzDrawnPoints.Collect(t, type, buf, true, rbuf) == 0) return float.NaN;              // a skin with no bake entry: baked now (its lowest vertices)
            return Gap(buf, t, wallM, out _, out _);
        }

        /// <summary>AnticCan, the frame a loose head freezes (kinematic, at rest): its drawn lowest point is put LiftM over the support right under it (the
        /// box collider is the mesh's box x 0.94: the drawn piece lay up to a few cm off it, more on what the rays do not see); off the map it is
        /// disposed; a watch lets it fall again when what it lies on goes.</summary>
        public static void Frozen(AnticCan can)
        {
            if (!On || !can || !can.name.StartsWith("OZ_HeadshotHead_")) return;                      // the loose heads only (the antics' fuel can as before)
            var t = can.transform;
            if (!InMap(t.position, 0f)) { OffMap++; can.Dispose("off the map"); return; }
            float gap = DrawnGap(t, null, float.PositiveInfinity);
            if (!float.IsNaN(gap))
            {
                float dy = Mathf.Clamp(LiftM - gap, -MaxRaiseM, MaxRaiseM);
                if (Mathf.Abs(dy) > 0.002f) { t.position += Vector3.up * dy; HeadsSeated++; }
            }
            if (!t.TryGetComponent<OzRestWatch>(out var w)) w = t.gameObject.AddComponent<OzRestWatch>();
            w.can = can; w.next = 0.3f;
        }

        // ------------------------------------------------------------------ EvilZod's burst pieces (DV161)
        /// <summary>OzEvilZodBurst, a resting body piece (its mesh on t): it turns onto its most level box face along the ground's slope (0.25 s, about
        /// its drawn middle) and lies with its lowest drawn point LiftM over the support right under it (lifted out of the ground or dropped onto it).
        /// Off the map: hidden. false = not handled (round 10's Settle then).</summary>
        public static bool EvilRest(Transform t, Mesh mesh, float dt, ZGame g)
        {
            if (!On || !t || !mesh || !g) return false;
            if (!t.TryGetComponent<OzRestEase>(out var e))
            {
                var pts = OzDrawnPoints.Rigid(mesh); if (pts == null || pts.Length == 0) return false;
                e = t.gameObject.AddComponent<OzRestEase>(); e.enabled = false; e.pts = pts;
                if (!InMap(t.position, 0f)) { e.off = true; OffMap++; t.gameObject.SetActive(false); return true; }
                var lo = pts[0]; var hi = pts[0];
                for (int i = 1; i < pts.Length; i++) { lo = Vector3.Min(lo, pts[i]); hi = Vector3.Max(hi, pts[i]); }
                e.c = (lo + hi) * 0.5f; var s = t.lossyScale;
                var half = Vector3.Scale((hi - lo) * 0.5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                var mid = t.TransformPoint(e.c);
                e.fromQ = t.rotation; e.toQ = LieFlat(t.rotation, half, Normal(g, mid.x, mid.z, 0.5f));   // r11b fix round 1: on its largest face (was the most level one: a leg stood on its end)
            }
            if (e.off) return true;
            if (e.k < 1f)
            {
                var mid = t.TransformPoint(e.c);
                e.k = Mathf.Min(1f, e.k + dt / 0.25f); float u = e.k * e.k * (3f - 2f * e.k);
                t.rotation = Quaternion.Slerp(e.fromQ, e.toQ, u);
                t.position += mid - t.TransformPoint(e.c);                                                 // it turns about its drawn middle
            }
            if (e.k < 1f || (e.next -= dt) <= 0f || e.vy < 0f)
            {
                e.next = 0.4f;
                var m = t.localToWorldMatrix; buf.Clear();
                for (int i = 0; i < e.pts.Length; i++) buf.Add(m.MultiplyPoint3x4(e.pts[i]));
                float gap = Gap(buf, t, 0.45f, out _, out _);
                if (!float.IsNaN(gap))
                {
                    float dy = LiftM - gap;
                    if (dy < -0.05f) { e.vy -= 13f * dt; t.position += Vector3.up * Mathf.Max(e.vy * dt, dy); }   // over a drop: it falls (1 g)
                    else { e.vy = 0f; if (Mathf.Abs(dy) > 0.002f) { t.position += Vector3.up * Mathf.Min(dy, MaxRaiseM); if (e.k >= 1f) EvilSeated++; } }
                }
            }
            return true;
        }

        /// <summary>OzEvilZodBurst, the cape lying (its cloth mesh on a child, flattened): the height of its root so its lowest drawn point lies LiftM over
        /// the support right under it (round 10 / U9h: its box's half height + 2 cm over GroundY at its middle: 2.1-2.3 cm up on flat ground, its
        /// downhill side 0.12-0.23 m into a slope). NaN: no answer (as before then).</summary>
        public static float CapeY(Transform t)
        {
            if (!On || !t) return float.NaN;
            float gap = DrawnGap(t, null, 0.45f);
            if (float.IsNaN(gap)) return float.NaN;
            return t.position.y + Mathf.Min(LiftM - gap, MaxRaiseM);
        }
    }

    /// <summary>1.5 kit robot_pieces_ground_r11b (A35): runs a dying robot's grounding AFTER every pose writer of the frame (the Animator, the heroes'
    /// legs OzHeroLegs at 61, the leg IK at 70, the gestures at 95): round 11 measured in UnitView's own LateUpdate (60), so Alan's and Brad's bodies,
    /// whose hips and legs OzHeroLegs writes after it, were grounded by a pose that was never drawn (0.11-0.30 m up, or sunk 0.9-1.1 m).</summary>
    [DefaultExecutionOrder(32000)]
    public sealed class OzBodyGroundLate : MonoBehaviour
    {
        internal UnitView v;
        void LateUpdate() { if (!v) { enabled = false; return; } v.BodyGroundLate(Time.deltaTime); }
    }

    /// <summary>1.5 kit robot_pieces_ground_r11b (A35): a frozen loose head / can looks at what it lies on every 0.3 s: when that went (a body that
    /// popped away, a piece that sank) it falls again (AnticCan.Unfreeze) and freezes and is seated again where it lands.</summary>
    [DefaultExecutionOrder(32100)]   // r11b fix round 1: after the bodies' grounding (OzBodyGroundLate, 32000) of the same frame
    public sealed class OzRestWatch : MonoBehaviour
    {
        internal AnticCan can; internal float next = 0.3f;
        /// <summary>r11b fix round 1: looks every frame (was every 0.3 s: the Play check saw heads 2.4-8.6 cm over what they lay on in between) and
        /// seats the head again when it is more than ReseatM off LiftM (what it lay on moved down or up a little); over RefallM it falls again.</summary>
        public static float ReseatM = 0.006f;
        public static int Reseated;
        void LateUpdate()
        {
            if (!can) { Destroy(this); return; }
            if ((next -= Time.deltaTime) > 0f) return;
            next = 0f;
            if (!OzRestSurface.On || !can.Frozen) return;
            float gap = OzRestSurface.DrawnGap(transform, null, float.PositiveInfinity);
            if (float.IsNaN(gap)) return;
            if (gap > OzRestSurface.RefallM) { can.Unfreeze(); OzRestSurface.HeadsRefell++; }
            else if (Mathf.Abs(gap - OzRestSurface.LiftM) > ReseatM) { transform.position += Vector3.up * Mathf.Clamp(OzRestSurface.LiftM - gap, -OzRestSurface.MaxRaiseM, OzRestSurface.MaxRaiseM); Reseated++; }
        }
    }

    /// <summary>1.5 kit robot_pieces_ground_r11b fix round 1 (A35, Play look 17 Nov: every death-clip body lay at hip height on its gun planted in the
    /// ground, 0.5-1.1 m in the air): the gun of a robot / hero whose body is drawn by a death clip leaves the hands (UnitView.BodyGround), falls (1 g),
    /// turns onto its largest face along the ground (0.35 s, about its drawn middle) and lies with its lowest drawn point LiftM over the real surface
    /// under it (OzRestSurface.Gap); its landing clatters (every visible action has a sound). It pops away with the body. View only.</summary>
    [DefaultExecutionOrder(32100)]
    public sealed class OzDroppedGun : MonoBehaviour
    {
        internal UnitView body; Vector3[] pts; Vector3 c; Quaternion fromQ, toQ; float vy, k, next; bool landed, gone;
        public static int Dropped, Landed;
        static readonly List<Vector3> buf = new List<Vector3>(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Statics() { Dropped = Landed = 0; OzRestWatch.Reseated = 0; }

        /// <summary>The body's held gun (the first BS01_Weapon_* under it, as WeaponHold finds it) out of the hands: kept where it is drawn now,
        /// under the body's own parent. null: no gun (already gone, or none).</summary>
        internal static OzDroppedGun Drop(UnitView v)
        {
            if (!v || !OzRestSurface.On) return null;
            Transform gun = null;
            foreach (var t in v.GetComponentsInChildren<Transform>(false))
                if (t.name.StartsWith("BS01_Weapon_") && (!t.parent || !t.parent.name.StartsWith("BS01_Weapon_"))) { gun = t; break; }
            if (!gun) return null;
            var w2l = gun.worldToLocalMatrix; var pl = new List<Vector3>(128);
            foreach (var mf in gun.GetComponentsInChildren<MeshFilter>(false))
            {
                if (!mf.sharedMesh || !OzDrawnPoints.Solid(mf.GetComponent<MeshRenderer>())) continue;
                var p = OzDrawnPoints.Rigid(mf.sharedMesh); if (p == null) continue;
                var m = w2l * mf.transform.localToWorldMatrix;
                for (int i = 0; i < p.Length; i++) pl.Add(m.MultiplyPoint3x4(p[i]));
            }
            if (pl.Count == 0) return null;
            gun.SetParent(v.transform.parent, true);
            var d = gun.gameObject.AddComponent<OzDroppedGun>(); d.body = v; d.pts = pl.ToArray();
            Vector3 lo = d.pts[0], hi = d.pts[0];
            for (int i = 1; i < d.pts.Length; i++) { lo = Vector3.Min(lo, d.pts[i]); hi = Vector3.Max(hi, d.pts[i]); }
            d.c = (lo + hi) * 0.5f; var s = gun.lossyScale;
            var half = Vector3.Scale((hi - lo) * 0.5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
            d.fromQ = gun.rotation; d.toQ = OzRestSurface.LieFlat(gun.rotation, half, OzRestSurface.UpAt(gun.TransformPoint(d.c)));
            Dropped++;
            return d;
        }

        void LateUpdate()
        {
            if (gone) return;
            if (!body || body.BgPopping) { gone = true; PopOut.Begin(transform, PopOut.Style.Debris, null, true, false); return; }   // it goes with its body
            float dt = Time.deltaTime; if (dt <= 0f) return;
            if (k < 1f)
            {   // it turns onto its largest face about its drawn middle
                var mid = transform.TransformPoint(c);
                k = Mathf.Min(1f, k + dt / 0.35f); float u = k * k * (3f - 2f * k);
                transform.rotation = Quaternion.Slerp(fromQ, toQ, u);
                transform.position += mid - transform.TransformPoint(c);
            }
            if (landed && k >= 1f && (next -= dt) > 0f) return;
            next = 0.3f;
            var m = transform.localToWorldMatrix; buf.Clear();
            for (int i = 0; i < pts.Length; i++) buf.Add(m.MultiplyPoint3x4(pts[i]));
            float gap = OzRestSurface.Gap(buf, transform, 0.45f, out var low, out _);
            if (float.IsNaN(gap)) return;
            float dy = OzRestSurface.LiftM - gap;
            if (dy < -0.004f)
            {   // over the ground: it falls (1 g), never past its seat
                vy -= 13f * dt; transform.position += Vector3.up * Mathf.Max(vy * dt, dy);
                if (vy * dt <= dy) Land(low);
            }
            else
            {
                if (dy > 0.002f) transform.position += Vector3.up * Mathf.Min(dy, OzRestSurface.MaxRaiseM);
                if (!landed) Land(low);
                vy = 0f;
            }
        }

        void Land(Vector3 at)
        {
            if (landed) return;
            landed = true; Landed++;
            AudioDirector.Fire("dst_debris_metal", at, -1, Mathf.Clamp(0.25f + Mathf.Abs(vy) * 0.06f, 0.25f, 0.6f), Random.Range(0.85f, 1.05f));   // the gun on the ground: a metal clatter
            vy = 0f;
        }
    }

    /// <summary>1.5 kit robot_pieces_ground_r11b (A35): the state of an EvilZod burst piece's rest (data only, OzRestSurface.EvilRest).</summary>
    public sealed class OzRestEase : MonoBehaviour
    {
        internal Vector3[] pts; internal Vector3 c; internal Quaternion fromQ, toQ; internal float k, next, vy; internal bool off;
    }
}
