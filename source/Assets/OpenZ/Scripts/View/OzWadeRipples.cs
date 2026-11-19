// OpenZ (GPL-3.0)
// View only: reads the drawn water and the robot views, writes nothing to the rules.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The water round wading robots, in place of the flat pale rings OpenZWading threw out before (OpenZFxPlayer.Ripple: moving a ring of
    /// radius ~1.95 m every 0.55 s, standing ~0.7 m every 1.2 s, a wake ring of ~1 m every 0.25 s, a ~0.85 m ring on every footfall and a
    /// ~2.8 m ring when it walks in; a robot is about 0.7 m across at its waterline).
    /// <para>ONE mesh and one renderer for every wading robot on screen (one draw call), rebuilt each frame on the water surface, soft edges
    /// from the vertex alpha, in the water's own colours (OzWadeWake.Colours: the level's water material), darker at night:</para>
    /// <list type="bullet">
    /// <item>a soft broken foam collar hugging the legs at the waterline, pushed a little forward while it walks;</item>
    /// <item>standing: two small soft ripples that leave the collar and are gone within about 0.25 m of it;</item>
    /// <item>walking: a small soft ripple round each footfall (0.1 -> 0.4 m) and a short faint V wake (two soft lines from its sides, spreading
    /// 8 deg, at most 1.4 m behind it at walking speed, laid where it walked, fading with its speed and ~1 s after it stops);</item>
    /// <item>walking in: one soft ripple out to about 0.6 m (OpenZWading keeps its droplets and its swim sound).</item>
    /// </list>
    /// Everything scales with the robot size option (OzRobotScale). At full size no ripple is visible further than about 0.65 m from the
    /// robot's middle, the wake about 1.2 m behind it and 1.2 m wide (Status() prints the farthest visible point measured, wake included). Lava: untouched (Feed / Entry / Step return false there: the old rings).
    /// GRAPHICS QUALITY LOW: no V wake, one standing ripple. Off (<see cref="On"/> false) = the old rings (before / after proof).
    /// </summary>
    [DefaultExecutionOrder(85)]   // after OpenZWading (70), which feeds it, and OzWadeWake (80)
    public sealed class OzWadeRipples : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>Collar: the body's radius at the waterline (m at full size; the water shaders' OZ_WADE_RING_R is 0.34), its soft reach
        /// inward / outward (+ ahead while it walks), opacity standing / walking, how far it pushes ahead at walking speed (m).</summary>
        public static float BodyR = 0.30f, CollarIn = 0.09f, CollarOut = 0.16f, CollarMoveOut = 0.08f, CollarA = 0.30f, CollarMoveA = 0.42f, BowPush = 0.12f;
        /// <summary>Standing ripples: reach beyond the collar (m), period (s), opacity.</summary>
        public static float RipReach = 0.36f, RipPeriod = 2.3f, RipA = 0.26f;
        /// <summary>Footfall ripples: start / end radius (m), life (s), opacity.</summary>
        public static float StepR0 = 0.10f, StepR1 = 0.40f, StepLife = 0.75f, StepA = 0.22f;
        /// <summary>Walking in: reach beyond the collar (m), time (s), opacity.</summary>
        public static float EntryReach = 0.38f, EntryS = 1.0f, EntryA = 0.36f;
        /// <summary>V wake: length behind it at walking speed (m), spread (deg), line width near / far (m), opacity, step between samples (m).</summary>
        public static float TailM = 1.4f, SpreadDeg = 8f, LineW0 = 0.10f, LineW1 = 0.22f, LineA = 0.24f, StepM = 0.16f;
        /// <summary>Speed (m/s) under which it counts as standing; the speed that counts as full walking speed in water.</summary>
        public static float StandMps = 0.15f, FullMps = 1.0f;
        /// <summary>The unlit mesh's light: by day / at full night (DetailKit.Night).</summary>
        public static float DayLight = 0.95f, NightLight = 0.32f;
        /// <summary>The most robots drawn at once (the nearest ones are fed first by OpenZWading's own order; the rest keep no ripples).</summary>
        public const int MaxRobots = 64;

        /// <summary>Counted proof: robots fed, frames built, wake samples, footfall ripples, entries, old rings held; the farthest visible
        /// point from a robot's middle (m, divided by its size) and the longest wake measured.</summary>
        public static int Fed, Builds, Samples, StepRipples, Entries, OldRingsHeld, Drawn;
        public static float WidestM, LongestWakeM;

        sealed class W
        {
            public UnitView v; public int fed = -10; public Vector3 p, fwd = Vector3.forward, lastP; public float dt, k = 1f, clock, speed, speedK, wetK, standK, wakeK, odo, lastEmit = -99f, entryT = 99f;
            public bool moving, hasLast;
            public const int MaxS = 16;
            public readonly Vector3[] sP = new Vector3[MaxS], sR = new Vector3[MaxS]; public readonly float[] sO = new float[MaxS], sK = new float[MaxS];
            public int sHead, sCount;
            public const int MaxStep = 4;
            public readonly Vector3[] stepAt = new Vector3[MaxStep]; public readonly float[] stepAge = new float[MaxStep]; public int stepN;
        }

        static OzWadeRipples host;
        static readonly Dictionary<int, W> robots = new Dictionary<int, W>();
        static readonly List<int> gone = new List<int>();
        static readonly List<Vector3> V = new List<Vector3>(8192);
        static readonly List<Color32> C = new List<Color32>(8192);
        static readonly List<int> T = new List<int>(24576);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            host = null; robots.Clear();
            Fed = Builds = Samples = StepRipples = Entries = OldRingsHeld = Drawn = 0; WidestM = LongestWakeM = 0f;
        }

        static bool Low => OzLite.Active && OzLite.Level >= 2;

        static bool Water(Vector3 surf) => On && !OzLava.SurfaceAt(surf, out _);

        static W Of(UnitView v, bool make)
        {
            if (!host)
            {
                if (!make) return null;
                robots.Clear();                                                                              // a new scene: the old entries went with the old host
                var go = new GameObject("OZ_WadeRipples");
                host = go.AddComponent<OzWadeRipples>(); host.Bind();
            }
            int id = v.GetInstanceID();
            if (!robots.TryGetValue(id, out var w))
            {
                if (!make || robots.Count >= MaxRobots) return null;
                w = new W { v = v, clock = (v.Obj != null ? v.Obj.Id * 0.6180339f : 0f) % 1f * RipPeriod };   // own phase per robot, never lockstep
                robots[id] = w;
            }
            return w;
        }

        /// <summary>OpenZWading.Step, every frame for a robot wading on screen (surf = the water surface under its middle). True = this draws
        /// its water (the caller leaves its rings out); false = off, lava, or no room (the caller draws its rings as before).</summary>
        public static bool Feed(UnitView v, Vector3 surf, bool moving, float dt)
        {
            if (!v || !Water(surf)) return false;
            var w = Of(v, true); if (w == null) return false;
            w.fed = Time.frameCount; w.p = surf; w.dt = Mathf.Max(0f, dt); w.moving = moving; w.k = OzRobotScale.Of(v);
            Fed++; OldRingsHeld++;
            return true;
        }

        /// <summary>OpenZWading, walking in: one soft ripple from the collar. True = drawn (the caller throws its droplets without the ring).</summary>
        public static bool Entry(UnitView v, Vector3 surf)
        {
            if (!v || !Water(surf)) return false;
            var w = Of(v, true); if (w == null) return false;
            w.entryT = 0f; w.p = surf; w.fed = Time.frameCount; w.k = OzRobotScale.Of(v); Entries++;
            return true;
        }

        /// <summary>OpenZWading.FootFall: a small soft ripple round the foot. True = drawn (the caller leaves its ring out).</summary>
        public static bool Step(UnitView v, Vector3 at)
        {
            if (!v || !Water(at)) return false;
            var w = Of(v, false); if (w == null) return false;
            int i = w.stepN < W.MaxStep ? w.stepN++ : Oldest(w);
            w.stepAt[i] = at; w.stepAge[i] = 0f; StepRipples++;
            return true;
        }

        static int Oldest(W w) { int o = 0; for (int i = 1; i < W.MaxStep; i++) if (w.stepAge[i] > w.stepAge[o]) o = i; return o; }

        // ------------------------------------------------------------------ the host
        Mesh mesh; MeshRenderer mr;

        void Bind()
        {
            mesh = new Mesh { name = "OzWadeRipples", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = OzWadeWake.Mat();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.enabled = false;
        }

        void OnDestroy() { if (mesh) Destroy(mesh); if (host == this) host = null; }

        void LateUpdate()
        {
            V.Clear(); C.Clear(); T.Clear();
            int f = Time.frameCount; float frameDt = Time.deltaTime;
            lightK = Mathf.Lerp(DayLight, NightLight, Mathf.Clamp01(DetailKit.Night));
            OzWadeWake.Colours();
            cCollar = OzWadeWake.FoamTint; cRip = OzWadeWake.RippleTint; cLine = OzWadeWake.LineTint;
            gone.Clear(); int drawn = 0;
            foreach (var kv in robots)
            {
                var w = kv.Value;
                if (!w.v || !On) { gone.Add(kv.Key); continue; }
                if (f - w.fed <= 1) Advance(w);
                else
                {   // not fed (out of the water, off screen, dead): fade out, then go
                    w.wetK = Mathf.MoveTowards(w.wetK, 0f, frameDt * 3f); w.wakeK = Mathf.MoveTowards(w.wakeK, 0f, frameDt * 3f);
                    if (w.wetK <= 0f) { gone.Add(kv.Key); continue; }
                }
                if (V.Count < 200000) { Draw(w); drawn++; }
            }
            foreach (var id in gone) robots.Remove(id);
            Drawn = drawn;
            mesh.Clear();
            if (T.Count == 0 || !mr.sharedMaterial) { mr.enabled = false; return; }
            mesh.SetVertices(V); mesh.SetColors(C); mesh.SetTriangles(T, 0, true);
            mr.enabled = true; Builds++;
        }

        static void Advance(W w)
        {
            float dt = w.dt;
            if (dt <= 0f) return;                                                                            // paused: the same picture
            w.clock += dt; w.entryT += dt;
            for (int i = 0; i < w.stepN; i++) w.stepAge[i] += dt;
            float inst = 0f;
            if (w.hasLast)
            {
                var d = w.p - w.lastP; d.y = 0f; float m = d.magnitude;
                if (m < 3f) { w.odo += m; inst = m / dt; if (m > 0.002f) w.fwd = Vector3.Slerp(w.fwd, d / m, 1f - Mathf.Exp(-dt * 10f)); }   // more = placed anew: no jump in the wake
                else w.sCount = 0;
            }
            w.lastP = w.p; w.hasLast = true;
            w.speed = Mathf.Lerp(w.speed, Mathf.Min(inst, 6f), 1f - Mathf.Exp(-dt * 5f));
            w.speedK = Mathf.Clamp01((w.speed - StandMps) / Mathf.Max(0.1f, FullMps - StandMps));
            w.wetK = Mathf.MoveTowards(w.wetK, 1f, dt / 0.35f);
            w.standK = Mathf.MoveTowards(w.standK, w.speedK < 0.1f ? 1f : 0f, dt / 0.8f);
            w.wakeK = Mathf.MoveTowards(w.wakeK, w.speedK > 0.05f ? 1f : 0f, dt / (w.speedK > 0.05f ? 0.4f : 1.0f));
            if (!Low && w.speedK > 0.05f && w.odo - w.lastEmit >= StepM)
            {   // one V sample where its middle is now: its side axis, the odometer, its speed
                w.lastEmit = w.odo;
                int i = w.sHead; w.sHead = (w.sHead + 1) % W.MaxS; if (w.sCount < W.MaxS) w.sCount++;
                w.sP[i] = w.p; w.sR[i] = Vector3.Cross(Vector3.up, w.fwd); w.sO[i] = w.odo; w.sK[i] = w.speedK;
                Samples++;
            }
            if (w.wakeK <= 0f) w.sCount = 0;
        }

        // ------------------------------------------------------------------ the mesh
        float lightK; Color cCollar, cRip, cLine;
        Vector3 centre;

        void Add(Vector3 at, Color c, float a, float k)
        {
            a = Mathf.Clamp01(a);
            if (a > 0.05f)
            {
                float r = new Vector2(at.x - centre.x, at.z - centre.z).magnitude / Mathf.Max(0.3f, k);
                if (r > WidestM) WidestM = r;
            }
            var lc = (c * lightK).linear;
            V.Add(at); C.Add(new Color32((byte)(Mathf.Clamp01(lc.r) * 255f), (byte)(Mathf.Clamp01(lc.g) * 255f), (byte)(Mathf.Clamp01(lc.b) * 255f), (byte)(a * 255f)));
        }

        static void Strip(int first, int cols, int rows)
        {
            for (int i = 0; i < cols - 1; i++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = first + i * rows + r, b = a + rows;
                    T.Add(a); T.Add(b); T.Add(a + 1); T.Add(a + 1); T.Add(b); T.Add(b + 1);
                }
        }

        /// <summary>A soft ring round `at`: radius r, half width h (alpha 0 at both edges, a at the middle, broken a little round it).</summary>
        void Ring(Vector3 at, Vector3 fwd, Vector3 rgt, float r, float h, Color c, float a, float k, float ph, int n)
        {
            if (a < 0.01f) return;
            int first = V.Count;
            for (int i = 0; i <= n; i++)
            {
                float th = i * (Mathf.PI * 2f / n), cs = Mathf.Cos(th), sn = Mathf.Sin(th);
                var d = rgt * cs + fwd * sn;
                float br = 0.78f + 0.22f * Mathf.Sin(i * 2.1f + ph * 6.28f) * Mathf.Sin(i * 0.9f - ph * 3.1f);
                Add(at + d * Mathf.Max(0.02f, r - h), c, 0f, k); Add(at + d * r, c, a * br, k); Add(at + d * (r + h), c, 0f, k);
            }
            Strip(first, n + 1, 3);
        }

        void Draw(W w)
        {
            const float Lift = 0.03f;
            float k = Mathf.Clamp(w.k, 0.3f, 1.5f);
            var fwd = w.fwd; fwd.y = 0f; if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; fwd.Normalize();
            var rgt = Vector3.Cross(Vector3.up, fwd);
            centre = w.p + Vector3.up * Lift;
            float body = BodyR * k;
            const int N = 22;
            if (w.wetK > 0.01f)
            {   // the foam collar hugging the legs, pushed ahead a little while it walks
                float A = Mathf.Lerp(CollarA, CollarMoveA, w.speedK) * w.wetK;
                int first = V.Count;
                for (int i = 0; i <= N; i++)
                {
                    float th = i * (Mathf.PI * 2f / N), cs = Mathf.Cos(th), sn = Mathf.Sin(th);
                    var d = rgt * cs + fwd * sn;
                    float front = Mathf.Max(0f, sn);
                    float n = 0.72f + 0.28f * Mathf.Sin(i * 2.3f + w.clock * 1.3f) * Mathf.Sin(i * 0.7f - w.clock * 0.8f);
                    float r = body * (1f + 0.06f * Mathf.Sin(i * 1.7f + w.clock * 0.6f)) + BowPush * k * w.speedK * front * front;
                    float o = (CollarOut + CollarMoveOut * w.speedK * front) * k;
                    Add(centre + d * (r - CollarIn * k), cCollar, 0f, k);
                    Add(centre + d * r, cCollar, A * n, k);
                    Add(centre + d * (r + o * 0.45f), cCollar, A * 0.45f * n * n, k);
                    Add(centre + d * (r + o), cCollar, 0f, k);
                }
                Strip(first, N + 1, 4);
                for (int j = 0; j < (Low ? 1 : 2) && w.standK > 0.01f; j++)
                {   // standing: small ripples leaving the collar
                    float u = Mathf.Repeat(w.clock / RipPeriod + 0.5f * j, 1f);
                    float a = RipA * w.standK * w.wetK * (1f - u) * (1f - u) * Mathf.SmoothStep(0f, 1f, u / 0.12f);
                    Ring(centre, fwd, rgt, body + 0.04f * k + u * RipReach * k, (0.05f + 0.09f * u) * k, cRip, a, k, u + j, N);
                }
                if (w.entryT < EntryS)
                {   // walking in: one soft ripple
                    float u = w.entryT / EntryS;
                    float a = EntryA * w.wetK * (1f - u) * (1f - u) * Mathf.SmoothStep(0f, 1f, u / 0.1f);
                    Ring(centre, fwd, rgt, body + 0.04f * k + u * EntryReach * k, (0.06f + 0.10f * u) * k, cRip, a, k, 0.3f, N);
                }
            }
            for (int i = 0; i < w.stepN; i++)
            {   // a small soft ripple round each footfall
                float u = w.stepAge[i] / StepLife; if (u >= 1f) continue;
                var at = new Vector3(w.stepAt[i].x, centre.y, w.stepAt[i].z);
                float a = StepA * w.wetK * (1f - u) * (1f - u) * Mathf.SmoothStep(0f, 1f, u / 0.15f);
                Ring(at, fwd, rgt, Mathf.Lerp(StepR0, StepR1, u) * k, (0.03f + 0.05f * u) * k, cRip, a, k, u * 0.7f + i, 14);
            }
            if (w.wakeK > 0.01f && w.sCount > 0) Wake(w, k);
        }

        void Wake(W w, float k)
        {   // two soft lines from its sides, laid where it walked, spreading behind it
            float tanS = Mathf.Tan(SpreadDeg * Mathf.Deg2Rad);
            var rgt = Vector3.Cross(Vector3.up, w.fwd);
            for (int side = -1; side <= 1; side += 2)
            {
                int first = V.Count, cols = 0; float longest = 0f;
                for (int j = -1; j < w.sCount; j++)
                {
                    Vector3 b, r; float d, sk;
                    if (j < 0) { b = w.p; r = rgt; d = 0f; sk = w.speedK; }
                    else
                    {
                        int i = (w.sHead - 1 - j + W.MaxS * 2) % W.MaxS;
                        b = w.sP[i]; r = w.sR[i]; d = w.odo - w.sO[i]; sk = w.sK[i];
                        if (d > TailM * k * w.sK[i] + 0.05f) break;
                    }
                    float L = Mathf.Max(0.1f, TailM * k * sk), t = Mathf.Clamp01(d / L);
                    float off = (BodyR + 0.04f) * k + d * tanS, wd = Mathf.Lerp(LineW0, LineW1, t) * k;
                    float a = LineA * sk * w.wakeK * w.wetK * Mathf.SmoothStep(0f, 1f, d / (0.25f * k)) * (1f - Mathf.SmoothStep(0.4f, 1f, t));
                    float n = 0.75f + 0.25f * Mathf.Sin(d * 5.3f + side) * Mathf.Sin(d * 2.1f - w.clock * 0.7f);
                    var c = new Vector3(b.x, centre.y, b.z) + r * (side * off);
                    Add(c - r * (side * wd * 0.5f), cLine, 0f, k); Add(c, cLine, a * n, k); Add(c + r * (side * wd * 0.5f), cLine, 0f, k);
                    if (a > 0.05f && d > longest) longest = d;
                    cols++;
                }
                if (cols > 1) Strip(first, cols, 3);
                if (longest / k > LongestWakeM) LongestWakeM = longest / k;
            }
        }

        public static string Status() =>
            $"wade ripples {(On ? "on" : "off")}: robots now {robots.Count} (drawn {Drawn}), fed {Fed}, builds {Builds}, wake samples {Samples}, footfall ripples {StepRipples}, entries {Entries}, old rings held {OldRingsHeld}; "
            + $"farthest visible point {WidestM:0.00} m from a robot's middle (full size; body {BodyR:0.00} m), longest visible wake {LongestWakeM:0.00} m";
    }
}
