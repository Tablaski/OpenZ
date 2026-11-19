// OpenZ (GPL-3.0) - version 1.5 kit crane_repair_look_r6: what a crane's bridge repair looks like at the game camera (view only).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// VIEW ONLY: the rule is DV206 as it was (where the crane stops, when the bridge is repaired);
    /// this draws the crane's work on a bridge in place of kit crane_deck_sound_r3's stream (<see cref="On"/> false = that stream again).
    /// <list type="bullet">
    /// <item>the stream: glowing orange deck plates (about 1.15 m, were 0.34 m) fly from the tip of the crane's hook on an arc into the span,
    /// each with a glow trail; between them small hot embers on the same path; a glow at the hook; a burst of sparks, a flash and a warm
    /// light where a plate lands. Drawn with the bridge restore's own energy look (Shaders/OzBridgeRestore.shader mode 1, construction
    /// orange, brighter than 1 so the bloom takes it). A plate leaves the hook small and is full size 0.3 of its way out: nothing big is
    /// ever drawn over the crane. The stream runs only while the crane stands and works (the rules' work window) and stops with it.</item>
    /// <item>the spreading: the flag capture's zone wash laid along the deck (the restore's energy fill is that wash; here it runs ONE way):
    /// from the crane's end to the far end, a bright front band, a glowing trail and energy streaks behind it, a light orange tint that
    /// stays; the front moves on row by row as the rules' repair goes on, each move eased out like the capture's sweep; a row flashes when
    /// a plate lands on it. When the rules repair the bridge the fill fades in <see cref="HandOver"/> s while the restore
    /// (OzBridgeRestore) starts: its holographic pieces and its own fill take over, nothing is drawn twice at full strength.</item>
    /// </list>
    /// Cheap: at most <see cref="MaxPlates"/> plates and <see cref="MaxEmbers"/> embers in the air, one quad per <see cref="RowM"/> m of
    /// deck, all pooled; nothing while no crane works on a bridge.
    /// </summary>
    public static class OzCraneRepairLook
    {
        public static bool On = true;
        /// <summary>The stream: a plate every PlateEvery s (its long side PlateLen m), an ember every EmberEvery s (EmberSize m), the arc's
        /// height over the straight line (m, + 0.12 m a metre of distance).</summary>
        public static float PlateEvery = 0.10f, PlateLen = 1.15f, EmberEvery = 0.035f, EmberSize = 0.34f, Arc = 2.6f;
        /// <summary>The fill: one row a RowM m of deck, its tint (the restore's own fill uses 0.15), how fast the front closes on its next
        /// row (1 / s, eased out: 3.5 = 97 % of the way in 1 s, the capture sweep's second), the bright band's width (m), the fade into the
        /// restore (s).</summary>
        public static float RowM = 2.4f, FillTint = 0.5f, FillEase = 3.5f, BandM = 1.1f, HandOver = 0.45f;
        /// <summary>
        /// full-size plates + 1.5 m flashes hid the hook tip for about 2 s): a plate grows to full size only this far (m, seen from above)
        /// from the hook, and a landing that close flashes smaller.</summary>
        public static float HookClearM = 4f;
        public const int MaxPlates = 14, MaxEmbers = 30;
        public static int Jobs, Plates, Embers, Landed, RowsLit, HandOvers, CalledOff;
        public static string Last = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            On = true; PlateEvery = 0.10f; PlateLen = 1.15f; EmberEvery = 0.035f; EmberSize = 0.34f; Arc = 2.6f;
            RowM = 2.4f; FillTint = 0.5f; FillEase = 3.5f; BandM = 1.1f; HandOver = 0.45f;
            Jobs = Plates = Embers = Landed = RowsLit = HandOvers = CalledOff = 0; Last = ""; host = null;
        }

        static OzCraneRepairLookHost host;

        /// <summary>OzCraneLandEndHost.Begin, a crane starts on a bridge. True = this look draws the job (the earlier stream stays out).</summary>
        public static bool Begin(ZGame g, ZObject crane, ZObject b, UnitView v)
        {
            if (!On || !OzCraneLandEnd.Stream || g == null || crane == null || b == null || !b.IsBridge) return false;
            if (OzBridgeRestore.Materials() == null) return false;                                        // the shader is not here: the earlier look
            if (!host) host = new GameObject("OZ_CraneRepairLook").AddComponent<OzCraneRepairLookHost>();
            if (!host.Begin(g, crane, b, v)) return false;
            Jobs++;
            return true;
        }

        /// <summary>OzCraneLandEnd.Streams: the stream works on this bridge (RulesView leaves its fallback weld bursts out).</summary>
        public static bool Streams(int buildingId) => On && host && host.Streaming(buildingId);

        public static string Status() => $"crane repair look {(On ? "on" : "off")}: bridge jobs {Jobs}, plates {Plates} (landed {Landed}), embers {Embers}, deck rows lit {RowsLit}, "
                                         + $"handed over to the restore {HandOvers}, called off {CalledOff}; plate {PlateLen:0.00} m every {PlateEvery:0.00} s, fill tint {FillTint:0.00}; last: {(Last.Length > 0 ? Last : "-")}";
    }

    [DefaultExecutionOrder(62)]   // after UnitView (60) and OzCraneLandEndHost (61): the hook tip read where it is drawn this frame
    public sealed class OzCraneRepairLookHost : MonoBehaviour
    {
        sealed class Fly
        {
            public Transform t; public MeshRenderer r; public Job job; public Vector3 from, to, spin; public Quaternion rot;
            public float t0, len, arc, glowT, flick; public Vector3 size; public int row; public bool busy, plate;
        }
        sealed class Row { public Transform t; public MeshRenderer r; public float hitAt = -100f; public bool lit; }
        sealed class Job
        {
            public ZObject crane, b; public UnitView v; public Transform hook; public bool ended, wasDestroyed, revived, calledOff;
            public float startAt, endAt = -1f, nextPlate, nextEmber, nextGlow, nextLight, front, frontTo, fade = 1f, firstLandAt = -1f, fullAt = -1f;
            public Vector3 centre, far, axis, axisB; public float half, halfW, lane, deckY, step; public int nearSign = 1, reserved;
            public readonly List<Row> rows = new List<Row>();
        }

        readonly List<Job> jobs = new List<Job>();
        readonly List<Fly> flies = new List<Fly>();
        readonly Stack<Row> freeRows = new Stack<Row>();
        MaterialPropertyBlock mpb; ZGame game;
        static Mesh cube, quad;
        static readonly int AlphaId = Shader.PropertyToID("_Alpha"), HitId = Shader.PropertyToID("_Hit"), CutId = Shader.PropertyToID("_Cut"),
            AxisId = Shader.PropertyToID("_Axis"), AxisBId = Shader.PropertyToID("_AxisB"), CentreId = Shader.PropertyToID("_Centre"), GainId = Shader.PropertyToID("_Gain"),
            FrontId = Shader.PropertyToID("_Front"), ClockId = Shader.PropertyToID("_Clock");
        static readonly Color Glow = new Color(1f * 2.4f, 0.5f * 2.4f, 0.12f * 2.4f, 1f), Spark = new Color(1f * 3f, 0.62f * 3f, 0.2f * 3f, 1f), OrangeLight = new Color(1f, 0.55f, 0.18f);

        static Mesh Cube()
        {
            if (cube) return cube;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = go.GetComponent<MeshFilter>().sharedMesh;
            var col = go.GetComponent<Collider>(); if (col) DestroyImmediate(col);
            Destroy(go);
            return cube;
        }

        /// <summary>A flat 1 x 1 m plate in its own x / z, facing up (a row of the fill: no side faces, so two rows never draw a seam twice).</summary>
        static Mesh Quad()
        {
            if (quad) return quad;
            quad = new Mesh { name = "OZ_CraneRepairRow" };
            quad.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) });
            quad.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            quad.RecalculateBounds();
            return quad;
        }

        MeshRenderer NewPart(string name, Mesh mesh, Material m)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        /// <summary>The energy look's values of one renderer. The shader's fill runs from BOTH ends of the length it is given toward its
        /// middle; the deck is handed over as one half of a span twice as long whose middle is the deck's FAR end, so the front (0..1) runs
        /// one way, from the crane's end to the far end. The clock runs backwards for the same reason: the streaks then run with the front.</summary>
        void Props(MeshRenderer r, Job j, float front, float band, float tint, float hit, float gain, float now)
        {
            mpb ??= new MaterialPropertyBlock();
            mpb.Clear();
            mpb.SetFloat(AlphaId, 1f); mpb.SetFloat(HitId, Mathf.Clamp01(hit)); mpb.SetFloat(GainId, Mathf.Clamp01(gain));
            mpb.SetVector(CutId, new Vector4(-10000f, 10000f, -10000f, 10000f));
            mpb.SetVector(AxisId, new Vector4(j.axis.x, j.axis.y, j.axis.z, j.half * 2f)); mpb.SetVector(AxisBId, new Vector4(j.axisB.x, j.axisB.y, j.axisB.z, 0f));
            mpb.SetVector(CentreId, new Vector4(j.far.x, j.far.y, j.far.z, 0f));
            mpb.SetVector(FrontId, new Vector4(front, Mathf.Max(0.02f, OzCraneRepairLook.BandM / Mathf.Max(1f, j.half * 2f)), tint, band));
            mpb.SetFloat(ClockId, -now);
            r.SetPropertyBlock(mpb);
        }

        static Transform Deep(Transform t, string n)
        {
            if (!t) return null;
            if (t.name == n) return t;
            foreach (Transform c in t) { var f = Deep(c, n); if (f) return f; }
            return null;
        }

        internal bool Streaming(int buildingId)
        {
            foreach (var j in jobs) if (!j.ended && j.b != null && j.b.Id == buildingId) return true;
            return false;
        }

        internal bool Begin(ZGame g, ZObject crane, ZObject b, UnitView v)
        {
            game = g;
            foreach (var x in jobs) if (!x.ended && x.crane == crane && x.b == b) return true;          // the same job told twice
            var mats = OzBridgeRestore.Materials(); if (mats == null) return false;
            var j = new Job { crane = crane, b = b, v = v, startAt = Time.time, wasDestroyed = b.Destroyed };
            // the deck: its middle, its long and its across axis, its ends, its width, its height (OzCraneDeck's measure; else the rules' box)
            bool alongX = b.WidthPix >= b.HeightPix;
            j.centre = g.CenterWorld(b, false); j.axis = alongX ? Vector3.right : Vector3.forward; j.axisB = alongX ? Vector3.forward : Vector3.right;
            j.half = (alongX ? b.WidthPix : b.HeightPix) / 16f; j.halfW = (alongX ? b.HeightPix : b.WidthPix) / 16f * 0.9f;
            var d = OzCraneDeck.Get(g, b);
            if (d != null && d.ok)
            {
                j.deckY = d.deckY; j.axis = d.axis; j.axisB = d.axisB;
                j.centre = d.centre + d.axis * ((d.a0 + d.a1) * 0.5f) + d.axisB * d.bMid;
                j.half = Mathf.Max(1f, (d.a1 - d.a0) * 0.5f); j.halfW = Mathf.Max(0.8f, d.halfW * 0.96f);
            }
            else j.deckY = g.CenterWorld(b).y + (g.level == "L02" ? 1.2f : 0.3f);                         // RulesView.Rebuild's deck height
            j.centre.y = j.deckY + 0.06f;
            j.lane = Mathf.Clamp(j.halfW * 0.8f, 0.6f, 4f);
            var cp = g.CenterWorld(crane, false);
            j.nearSign = Vector3.Dot(cp - j.centre, j.axis) >= 0f ? 1 : -1;
            j.far = j.centre - j.axis * (j.nearSign * j.half);
            int n = Mathf.Clamp(Mathf.RoundToInt(j.half * 2f / Mathf.Max(0.8f, OzCraneRepairLook.RowM)), 3, 16);
            j.step = j.half * 2f / n;
            for (int i = 0; i < n; i++)
            {
                var c = freeRows.Count > 0 ? freeRows.Pop() : new Row();
                if (!c.r) { c.r = NewPart("Row", Quad(), mats[1]); c.t = c.r.transform; }
                c.r.sharedMaterial = mats[1];
                c.t.position = j.centre + j.axis * (j.nearSign * (j.half - j.step * (i + 0.5f)));
                c.t.rotation = Quaternion.LookRotation(j.axis, Vector3.up);
                c.t.localScale = new Vector3(j.halfW * 2f, 1f, j.step);
                c.hitAt = -100f; c.lit = false; c.r.enabled = false;
                j.rows.Add(c);
            }
            jobs.Add(j);
            OzCraneRepairLook.Last = $"crane #{crane.Id} on bridge #{b.Id}{(b.Destroyed ? " (destroyed)" : "")}: deck {j.half * 2f:0.0} x {j.halfW * 2f:0.0} m at {j.deckY:0.00} m, {n} rows of {j.step:0.00} m, from its {(j.nearSign > 0 ? "+" : "-")} end";
            Debug.Log("[OpenZ] crane repair look: " + OzCraneRepairLook.Last);
            return true;
        }

        /// <summary>The share of the rules' work window that is over (DV206: CraneWorkFrom .. CraneWorkUntil); Zod's drive in and out: 15 s.</summary>
        float Progress(Job j)
        {
            var W = game != null ? game.World : null; var c = j.crane;
            if (W != null && c != null && c.CraneWorkUntil > 0 && c.CraneWorkFrom >= 0 && c.CraneWorkUntil > c.CraneWorkFrom)
                return Mathf.Clamp01((float)((W.Time - c.CraneWorkFrom) / (c.CraneWorkUntil - c.CraneWorkFrom)));
            return Mathf.Clamp01((Time.time - j.startAt) / 15f);
        }

        bool StillWorking(Job j)
        {
            var c = j.crane;
            if (c == null || c.Destroyed || game == null || game.World == null || game.World.Get(c.Id) != c) return false;
            return c.Waypoints.Count > 0 && c.Waypoints[0].mode == WpMode.CraneRepair && c.Waypoints[0].refId == j.b.Id;
        }

        Vector3 Tip(Job j)
        {
            if (j.v && !j.hook) j.hook = Deep(j.v.transform, "Crane_Hook");
            if (j.hook) return j.hook.TransformPoint(new Vector3(0f, -0.243f, 0f));                       // apc_crane_rebuild_v1 Crane_Hook: lowest y -0.243 (GLB units)
            return j.v ? j.v.transform.position + Vector3.up * 3.2f : game.CenterWorld(j.crane) + Vector3.up * 3.2f;
        }

        int Count(bool plate) { int n = 0; foreach (var p in flies) if (p.busy && p.plate == plate) n++; return n; }

        void Update()
        {
            if (game == null) game = ZGame.I;
            if (jobs.Count == 0 && flies.Count == 0) return;
            float dt = Time.deltaTime, now = Time.time;
            var fx = OpenZFxPlayer.I; var mats = OzBridgeRestore.Materials();
            for (int k = jobs.Count - 1; k >= 0; k--)
            {
                var j = jobs[k];
                bool revivedNow = j.wasDestroyed && j.b != null && !j.b.Destroyed;                        // the rules repaired it: the restore plays now
                if (revivedNow && !j.revived) { j.revived = true; OzCraneRepairLook.HandOvers++; }
                if (!j.ended && !StillWorking(j))                                                         // the job's end, the crane shot dead or sent away
                {
                    j.ended = true; j.endAt = now;
                    if (j.wasDestroyed && !j.revived && Progress(j) < 0.98f) { j.calledOff = true; OzCraneRepairLook.CalledOff++; }
                }
                bool stream = !j.ended && !j.revived && j.crane != null && !j.crane.IsMoving && mats != null;
                float prog = Progress(j); int n = j.rows.Count;
                if (stream)
                {
                    var tip = Tip(j);
                    if (now >= j.nextPlate && Count(true) < OzCraneRepairLook.MaxPlates)
                    {
                        j.nextPlate = now + OzCraneRepairLook.PlateEvery * Random.Range(0.8f, 1.25f);
                        Spawn(j, prog, true, tip, mats[1], now);
                    }
                    for (int e = 0; e < 3 && now >= j.nextEmber && Count(false) < OzCraneRepairLook.MaxEmbers; e++)
                    {
                        j.nextEmber = Mathf.Max(j.nextEmber, now - 0.1f) + OzCraneRepairLook.EmberEvery * Random.Range(0.7f, 1.3f);
                        Spawn(j, prog, false, tip, mats[1], now);
                    }
                    if (fx && now >= j.nextGlow)
                    {   // the source: a glow that breathes at the hook's tip
                        j.nextGlow = now + 0.06f;
                        fx.EmitChannel("wv2_glow", tip, Vector3.zero, 0.16f, Random.Range(0.7f, 1.0f), Glow, 0.7f);
                    }
                    if (fx && now >= j.nextLight && j.firstLandAt >= 0f)
                    {   // a warm light where the work is: the wreck and the deck round the front take the orange
                        j.nextLight = now + 0.3f;
                        var at = j.centre + j.axis * (j.nearSign * (j.half - Mathf.Clamp01(j.front) * j.half * 2f)) + Vector3.up * 1.4f;
                        fx.AddLight(at, OrangeLight, 2.2f, 9f, 0.45f, 2.2f, 0.2f, false);
                    }
                }
                // ---- the fill: the front closes on the row the rules' work has reached, eased out (the capture sweep's feel)
                if (j.revived) j.frontTo = 1.04f;
                else if (!j.ended && j.firstLandAt >= 0f)
                    j.frontTo = prog >= 0.92f ? 1.04f : Mathf.Max(j.frontTo, Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(prog / 0.92f) * n), 1, n) / (float)n);
                j.front = Mathf.Lerp(j.front, j.frontTo, 1f - Mathf.Exp(-dt * Mathf.Max(0.5f, OzCraneRepairLook.FillEase)));
                j.front = Mathf.MoveTowards(j.front, j.frontTo, dt * 0.02f);
                if (j.fullAt < 0f && j.front >= 1.0f) j.fullAt = now;
                bool fadeOut = j.revived || (j.ended && now - j.endAt > (j.wasDestroyed && !j.calledOff ? 2.5f : 0.2f));
                if (fadeOut) j.fade = Mathf.MoveTowards(j.fade, 0f, dt / (j.revived ? Mathf.Max(0.1f, OzCraneRepairLook.HandOver) : 0.5f));
                float fadeIn = j.firstLandAt >= 0f ? Mathf.Clamp01((now - j.firstLandAt) / 0.25f) : 0f;
                float gain = fadeIn * j.fade * (0.94f + 0.06f * Mathf.Sin(now * 4.1f));
                float band = j.fullAt >= 0f ? Mathf.Clamp01(1f - (now - j.fullAt) / 0.4f) : 1f;             // the front band goes out once the deck is covered
                foreach (var c in j.rows)
                {
                    c.r.enabled = gain > 0.004f;
                    if (!c.r.enabled) continue;
                    float hit = 0.5f * Mathf.Exp(-(now - c.hitAt) * 7f);
                    Props(c.r, j, j.front, band, OzCraneRepairLook.FillTint, hit, gain, now);
                }
                bool flying = false; foreach (var p in flies) if (p.busy && p.job == j) { flying = true; break; }
                if (j.ended && ((j.fade <= 0.001f && !flying) || now - j.endAt > 6f))
                {
                    foreach (var p in flies) if (p.busy && p.job == j) { p.busy = false; p.r.enabled = false; p.job = null; }
                    foreach (var c in j.rows) { c.r.enabled = false; freeRows.Push(c); }
                    j.rows.Clear(); jobs.RemoveAt(k);
                }
            }
            StepFlies(fx, now, dt);
        }

        void Spawn(Job j, float prog, bool plate, Vector3 tip, Material m, float now)
        {
            int n = j.rows.Count, target = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(prog / 0.92f) * n), 1, n), row;
            if (plate && j.reserved < target) row = j.reserved++;                                         // the next row of the deck: its first plate
            else
            {
                int top = Mathf.Max(1, j.reserved);
                row = Random.value < 0.7f ? Random.Range(Mathf.Max(0, top - 2), top) : Random.Range(0, top);   // most of the stream goes to the front
            }
            Fly p = null; foreach (var x in flies) if (!x.busy) { p = x; break; }
            if (p == null) { p = new Fly { r = NewPart("Fly", Cube(), m) }; p.t = p.r.transform; flies.Add(p); }
            p.r.sharedMaterial = m;
            p.busy = true; p.plate = plate; p.row = row; p.job = j;
            p.from = tip;
            p.to = j.rows[row].t.position + j.axis * Random.Range(-0.4f, 0.4f) * j.step + j.axisB * Random.Range(-1f, 1f) * j.lane;
            float dist = Vector3.Distance(p.from, p.to);
            p.t0 = now; p.len = Mathf.Clamp(0.5f + 0.018f * dist, 0.55f, 1.05f) * Random.Range(0.92f, 1.08f) * (plate ? 1f : 0.85f);
            p.arc = (OzCraneRepairLook.Arc + 0.12f * dist) * (plate ? 1f : Random.Range(0.6f, 1.1f));
            p.spin = Random.onUnitSphere * Random.Range(160f, 380f); p.rot = Random.rotation; p.flick = Random.value * 6.283f;
            if (plate) { float s = OzCraneRepairLook.PlateLen * Random.Range(0.82f, 1.18f); p.size = new Vector3(s * 0.72f, s * 0.2f, s); OzCraneRepairLook.Plates++; }
            else { float s = OzCraneRepairLook.EmberSize * Random.Range(0.7f, 1.2f); p.size = new Vector3(s, s * 0.6f, s * 1.4f); OzCraneRepairLook.Embers++; }
            p.t.position = p.from; p.t.rotation = p.rot; p.t.localScale = p.size * 0.3f; p.glowT = 0f;
            p.r.enabled = true;
        }

        void StepFlies(OpenZFxPlayer fx, float now, float dt)
        {
            foreach (var p in flies)
            {
                if (!p.busy) continue;
                var j = p.job;
                if (j == null) { p.busy = false; p.r.enabled = false; continue; }
                float k = Mathf.Clamp01((now - p.t0) / Mathf.Max(0.05f, p.len));
                float e = p.plate ? k * k * (3f - 2f * k) : k;                                            // a plate leaves and settles softly, an ember flows
                var mid = (p.from + p.to) * 0.5f + Vector3.up * p.arc;
                var pos = (1f - e) * (1f - e) * p.from + 2f * (1f - e) * e * mid + e * e * p.to;
                // tumbling out of the hook, flat along the deck as it comes down
                p.rot = Quaternion.Euler(p.spin * (dt * (1f - k) * (1f - k))) * p.rot;
                var flat = Quaternion.LookRotation(j.axis, Vector3.up);
                p.t.SetPositionAndRotation(pos, p.plate ? Quaternion.Slerp(p.rot, flat, k * k * k) : p.rot);
                float clear = Mathf.Clamp01(new Vector2(pos.x - p.from.x, pos.z - p.from.z).magnitude / Mathf.Max(0.1f, OzCraneRepairLook.HookClearM));
                float grow = Mathf.Min(Mathf.Clamp01(k / 0.3f), clear);
                p.t.localScale = p.size * Mathf.Lerp(0.3f, 1f, grow);                   // small at the hook: never a block over the crane
                float hit = (p.plate ? 0.7f : 1f) * (0.88f + 0.12f * Mathf.Sin(now * 31f + p.flick));
                Props(p.r, j, 1.04f, 0f, 1f, hit, Mathf.Clamp01(k / 0.1f) * j.fade, now);
                if (p.plate && fx && (p.glowT -= dt) <= 0f && fx.ChannelRoom("wv2_glow"))
                {   // the trail: a glow behind every plate, so the stream reads as a line of light from the hook to the span
                    p.glowT = 0.045f;
                    fx.EmitChannel("wv2_glow", pos, Vector3.zero, 0.3f, p.size.z * 0.95f * Mathf.Lerp(0.5f, 1f, grow), Glow, 0.5f);   // round 6 A: grow (was k / 0.3)
                }
                if (k < 1f) continue;
                p.busy = false; p.r.enabled = false; p.job = null;
                if (p.row < 0 || p.row >= j.rows.Count) continue;
                if (!p.plate)
                {
                    if (fx && Random.value < 0.5f) fx.EmitChannel("wv2_spark", p.to, (Random.insideUnitSphere + Vector3.up) * Random.Range(1.5f, 3f), Random.Range(0.2f, 0.35f), Random.Range(0.06f, 0.08f), Spark, 1f);
                    continue;
                }
                var c = j.rows[p.row];
                if (!c.lit) { c.lit = true; OzCraneRepairLook.RowsLit++; }
                c.hitAt = now; OzCraneRepairLook.Landed++;
                if (j.firstLandAt < 0f) j.firstLandAt = now;
                if (!fx) continue;
                float nearHook = Mathf.Clamp01(new Vector2(p.to.x - p.from.x, p.to.z - p.from.z).magnitude / Mathf.Max(0.1f, OzCraneRepairLook.HookClearM));   // round 6 A: a landing right under the hook flashes small
                fx.EmitChannel("wv2_glow", p.to + Vector3.up * 0.2f, Vector3.zero, 0.22f, 1.5f * Mathf.Lerp(0.35f, 1f, nearHook), Glow, 0.8f);   // the landing's flash
                for (int i = 0; i < 7; i++)
                {
                    var v = (Random.insideUnitSphere + Vector3.up * 1.2f) * Random.Range(2.5f, 5f);
                    fx.EmitChannel("wv2_spark", p.to, v, Random.Range(0.25f, 0.5f), Random.Range(0.07f, 0.11f), Spark, 1f);
                }
            }
        }
    }
}
