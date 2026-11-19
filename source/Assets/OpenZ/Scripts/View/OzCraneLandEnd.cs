// OpenZ (GPL-3.0) - version 1.5 kit crane_deck_sound_r3: a crane repairing a bridge from its land end (view only).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// The rule is DV206 (Sim/ZObject.CraneLandEnd.cs: the crane stops at the bridge's
    /// nearest reachable dry end and works there). This is what is SEEN and HEARD while a crane works (view only, nothing written to the rules):
    /// <list type="bullet">
    /// <item>sound: every crane job (bridges and buildings) plays the owner's construction track (AudioDirector.CraneConstruct.cs) from the
    /// moment the job starts (fade in 0.3 s) until it ends or is called off (fade out 0.5 s: the job's end, the crane destroyed or sent away),
    /// in place of the old repair-work noise: the repeating wrench / repair clips (veh_crane_work, AudioDirector), the job-start wrench
    /// (sound_v3 "crane", RulesView) and the weld crackle of the crane's work (bld_repair_weld from CraneArmRig and RulesView's fallback welds;
    /// <see cref="WeldQuiet"/> false = the crackle back on top); the arm's own movement sounds (slew, hoist, luff, clank, servo) stay;</item>
    /// <item>bridges: while the crane stands at its stop (its arm slews to the bridge as before, CraneArmRig), a stream of small orange hologram pieces (the bridge restore's own hologram material, Shaders/OzBridgeRestore.shader)
    /// flies from the tip of its hook on short arcs into the span, a faint glow trail behind each; each piece lights a cell of the deck where
    /// the bridge will stand, from the crane's end to the far end as the repair goes on (the rules' own work window, DV206 / DV61), so the
    /// orange hologram deck spreads over the gap; when the rules repair the bridge the existing restore animation (OzBridgeRestore) plays and
    /// the lit cells fade into it.</item>
    /// </list>
    /// Cheap: at most <see cref="MaxFlying"/> pieces in the air and one cell plate per 1.3 m of deck per side, pooled; nothing while no crane works.
    /// </summary>
    public static class OzCraneLandEnd
    {
        public static bool On = true, Stream = true, Sound = true, WeldQuiet = true;
        public static float SpawnEvery = 0.11f, FlightMin = 0.55f, FlightMax = 0.75f, PieceSize = 0.34f, Arc = 2.2f, CellM = 1.3f;
        public const int MaxFlying = 16;
        public static int Jobs, BridgeJobs, Pieces, Landed, CellsLit, Sounds, SoundStops, WeldsQuiet;
        /// <summary>The owner's construction track is the crane's work sound (his clip is in the build): the old work noise stays out.</summary>
        public static bool OwnsWorkSound => On && Sound && AudioDirector.I && AudioDirector.I.CraneConstructReady;
        /// <summary>CraneArmRig.Weld / RulesView's fallback welds: true = no weld crackle (the sparks still fly; his track is the work sound).</summary>
        public static bool QuietWeld() { if (!WeldQuiet || !OwnsWorkSound) return false; WeldsQuiet++; return true; }
        public static string Last = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            On = Stream = Sound = WeldQuiet = true; SpawnEvery = 0.11f; FlightMin = 0.55f; FlightMax = 0.75f; PieceSize = 0.34f; Arc = 2.2f; CellM = 1.3f;
            Jobs = BridgeJobs = Pieces = Landed = CellsLit = Sounds = SoundStops = WeldsQuiet = 0; Last = ""; host = null;
        }

        static OzCraneLandEndHost host;
        static OzCraneLandEndHost Host()
        {
            if (host) return host;
            host = new GameObject("OZ_CraneLandEnd").AddComponent<OzCraneLandEndHost>();
            return host;
        }

        /// <summary>RulesView.Handle(ZEventType.CraneAnim): on = crane starts working on `building`, off = the job ended.</summary>
        public static void Job(ZGame g, ZObject crane, ZObject building, bool on)
        {
            if (!On || g == null || building == null) return;
            if (on) { if (crane != null) { Host().Begin(g, crane, building); Jobs++; } }
            else if (host) host.End(building.Id, crane != null ? crane.Id : -1);
        }

        /// <summary>RulesView's fallback weld bursts (the arm out of reach): none on a bridge the orange stream works on (DV206: the crane stands
        /// at the land end, the bursts would spark in the middle of the gap over the water); in 'Zod exact' (the crane on the deck) as before.</summary>
        public static bool Streams(int buildingId) => On && Stream && ZWorld.CraneLandEndDeviation && ((host && host.Streaming(buildingId)) || OzCraneRepairLook.Streams(buildingId));   // 1.5 kit crane_repair_look_r6 (A8): the new stream counts too

        public static string Status() => $"crane land end {(On ? "on" : "off")} (DV206 {(ZWorld.CraneLandEndDeviation ? "ON" : "off")}): jobs {Jobs} (bridges {BridgeJobs}), "
                                         + $"pieces {Pieces} (landed {Landed}), deck cells lit {CellsLit}, construction sound starts {Sounds} / stops {SoundStops}, weld crackles left out {WeldsQuiet}; last: {(Last.Length > 0 ? Last : "-")}";
    }

    /// <summary>
    /// announcer line ann_bridge_repaired kept, the robot's 'Bridge' line (vo_building_events) dropped for a bridge Rachel was asked to announce;
    /// when Rachel is not asked (nothing of the player's near, off screen) the robot's line plays as before, so it is still said once).
    /// <see cref="KeepRachel"/> false = the robot's line kept and Rachel's dropped (the one switch for the lead). <see cref="On"/> false = both
    /// lines as before. Other buildings unchanged.</summary>
    public static class OzBridgeSaidOnce
    {
        public static bool On = true, KeepRachel = true;
        public static int RobotDropped, RachelDropped, RachelSaid, ClusterDropped;
        static int rachelFor = -1;
        // round 5 (14 Nov, A1b caveat): two crane repairs 3.4 s apart gave Rachel's line and then the robot's 'Bridge' line = heard as the double
        // the owner named; one repair cluster = ONE 'bridge repaired': a bridge repaired within ClusterGap s (real time) of the last spoken one gets no line
        public static float ClusterGap = 12f;
        static float lastSaidAt = -100f;
        static bool InCluster => Time.unscaledTime - lastSaidAt < ClusterGap;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { On = true; KeepRachel = true; RobotDropped = RachelDropped = RachelSaid = ClusterDropped = 0; rachelFor = -1; ClusterGap = 12f; lastSaidAt = -100f; }

        /// <summary>AudioDirector.OnRevived, before Rachel's ann_bridge_repaired for this repaired bridge: true = she is asked to say it.</summary>
        public static bool RachelSays(ZObject a)
        {
            rachelFor = -1;
            if (a == null || !a.IsBridge || !On) return true;
            if (InCluster) { ClusterDropped++; OzSoundLog.Add("dir", "ann_bridge_repaired", null, "bridge repaired: same repair cluster, already said (Rachel's line left out)"); return false; }   // round 5 A1b: one line per cluster
            if (KeepRachel) return true;
            RachelDropped++; OzSoundLog.Add("dir", "ann_bridge_repaired", null, "bridge repaired: said once, by the robot (Rachel's line left out)");
            return false;
        }

        /// <summary>AudioDirector.OnRevived, right after Rachel was asked: `queued` = the announcer took the line (AnnounceImpl; it drops a line
        /// on its own rules: ANNOUNCER Minimal, the same line a moment ago, a full queue). Only a line she really says takes the robot's turn,
        /// so 'bridge repaired' is never said zero times.</summary>
        public static void RachelQueued(ZObject a, bool queued)
        {
            if (a == null || !a.IsBridge || !On || !KeepRachel) return;
            if (queued) { rachelFor = a.Id; RachelSaid++; lastSaidAt = Time.unscaledTime; }   // round 5 A1b: the cluster starts at her line
            else OzSoundLog.Add("dir", "ann_bridge_repaired", null, "bridge repaired: Rachel's line not taken by the announcer: the robot says it");
        }
        /// <summary>AudioDirector.OnRevived, at the robot's 'Bridge' / 'repaired' line (vo_building_events) of every revived building; `wants` =
        /// the line's own condition as before. True = it is said.</summary>
        public static bool RobotSays(ZObject a, bool wants)
        {
            bool rachel = a != null && KeepRachel && rachelFor == a.Id; rachelFor = -1;                 // read once per revive: never a stale bridge id
            if (!wants || a == null || !a.IsBridge || !On) return wants;
            if (!rachel && InCluster) { ClusterDropped++; OzSoundLog.Add("dir", "vo_building_events", null, "bridge repaired: same repair cluster, already said (the robot's 'Bridge' line left out)"); return false; }   // round 5 A1b
            if (!rachel) { lastSaidAt = Time.unscaledTime; return true; }                                  // round 5 A1b: the robot's line starts a cluster too
            RobotDropped++; OzSoundLog.Add("dir", "vo_building_events", null, "bridge repaired: said once, by Rachel (the robot's 'Bridge' line left out)");
            return false;
        }
    }

    [DefaultExecutionOrder(61)]   // after UnitView (60, Update) places the hull: the hook tip read where it is drawn this frame
    public class OzCraneLandEndHost : MonoBehaviour
    {
        sealed class Piece { public Transform t; public MeshRenderer r; public Vector3 from, to, spin; public float t0, len; public int cell; public bool busy; public float glowT; }
        sealed class Cell { public Transform t; public MeshRenderer r; public float litAt = -1f, hitAt = -1f; }
        sealed class JobState
        {
            public ZObject crane, b; public UnitView v; public bool bridge, ended, soundOn; public string key;
            public float endAt = -1f, nextSpawn, startAt, fade = 1f;
            public bool wasDestroyed, deckOk; public int nearSign = 1, reserved;
            public Vector3 centre, axis, axisB; public float half, halfW, deckY;
            public readonly List<Cell> cells = new List<Cell>();
            public Transform hook;
        }

        readonly List<JobState> jobs = new List<JobState>();
        readonly List<Piece> pieces = new List<Piece>();
        readonly Stack<Cell> freeCells = new Stack<Cell>();
        MaterialPropertyBlock mpb; static Mesh cube;
        ZGame game;
        static readonly int AlphaId = Shader.PropertyToID("_Alpha"), HitId = Shader.PropertyToID("_Hit"), CutId = Shader.PropertyToID("_Cut"),
            AxisId = Shader.PropertyToID("_Axis"), AxisBId = Shader.PropertyToID("_AxisB"), CentreId = Shader.PropertyToID("_Centre"), GainId = Shader.PropertyToID("_Gain");
        static readonly Color Glow = new Color(1f * 2.2f, 0.5f * 2.2f, 0.12f * 2.2f, 1f), Spark = new Color(1f * 3f, 0.62f * 3f, 0.2f * 3f, 1f);

        static Mesh Cube()
        {
            if (cube) return cube;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = go.GetComponent<MeshFilter>().sharedMesh;
            var col = go.GetComponent<Collider>(); if (col) DestroyImmediate(col);
            Destroy(go);
            return cube;
        }

        MeshRenderer NewPart(string name, Material m)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Cube();
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        void Props(MeshRenderer r, JobState j, float alpha, float hit)
        {
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(AlphaId, Mathf.Clamp01(alpha)); mpb.SetFloat(HitId, Mathf.Clamp01(hit)); mpb.SetFloat(GainId, 1f);
            mpb.SetVector(CutId, new Vector4(-10000f, 10000f, -10000f, 10000f));
            mpb.SetVector(AxisId, new Vector4(j.axis.x, j.axis.y, j.axis.z, j.half)); mpb.SetVector(AxisBId, new Vector4(j.axisB.x, j.axisB.y, j.axisB.z, 0f));
            mpb.SetVector(CentreId, new Vector4(j.centre.x, j.deckY, j.centre.z, 0f));
            r.SetPropertyBlock(mpb);
        }

        static Transform Deep(Transform t, string n)
        {
            if (!t) return null;
            if (t.name == n) return t;
            foreach (Transform c in t) { var f = Deep(c, n); if (f) return f; }
            return null;
        }

        public void Begin(ZGame g, ZObject crane, ZObject b)
        {
            game = g;
            foreach (var x in jobs) if (!x.ended && x.crane == crane && x.b == b) return;   // the same job told twice
            var j = new JobState { crane = crane, b = b, bridge = b.IsBridge, key = "cranework:" + crane.Id, startAt = Time.time, wasDestroyed = b.Destroyed };
            g.Views.TryGetValue(crane.Id, out j.v);
            if (j.bridge) { if (!OzCraneRepairLook.Begin(g, crane, b, j.v)) Deck(g, j); OzCraneLandEnd.BridgeJobs++; }   // 1.5 kit crane_repair_look_r6 (A8): the big plates + the spreading fill (View/OzCraneRepairLook.cs) draw the job; the earlier small stream only when that look is off
            if (OzCraneLandEnd.Sound && AudioDirector.I && AudioDirector.I.CraneConstructStart(j.key, g.CenterWorld(crane), j.v ? j.v.transform : null) == 1) { j.soundOn = true; OzCraneLandEnd.Sounds++; }
            jobs.Add(j);
            OzCraneLandEnd.Last = $"crane #{crane.Id} on {b.Type} #{b.Id}{(b.Destroyed ? " (destroyed)" : "")} at ({crane.CenterX >> 4},{crane.CenterY >> 4}), sound {(j.soundOn ? "on" : "off")}";
        }

        public bool Streaming(int buildingId)
        {
            foreach (var j in jobs) if (!j.ended && j.bridge && j.b != null && j.b.Id == buildingId && j.cells.Count > 0) return true;
            return false;
        }

        public void End(int buildingId, int craneId)
        {
            foreach (var j in jobs) if (!j.ended && j.b.Id == buildingId && (craneId < 0 || j.crane.Id == craneId)) Stop(j);
        }

        void Stop(JobState j)
        {
            if (j.ended) return;
            j.ended = true; j.endAt = Time.time;
            if (j.soundOn && AudioDirector.I) { AudioDirector.I.CraneConstructStop(j.key); OzCraneLandEnd.SoundStops++; }
            j.soundOn = false;
        }

        void Deck(ZGame g, JobState j)
        {
            var b = j.b; bool alongX = b.WidthPix >= b.HeightPix;
            j.centre = g.CenterWorld(b, false); j.axis = alongX ? Vector3.right : Vector3.forward; j.axisB = alongX ? Vector3.forward : Vector3.right;
            j.half = (alongX ? b.WidthPix : b.HeightPix) / 16f; j.halfW = Mathf.Min(2.0f, (alongX ? b.HeightPix : b.WidthPix) / 16f * 0.6f);
            var d = OzCraneDeck.Get(g, b);
            if (d != null && d.ok) { j.deckY = d.deckY; j.centre = d.centre + d.axisB * d.bMid; j.axis = d.axis; j.axisB = d.axisB; j.half = Mathf.Max(1f, (d.a1 - d.a0) * 0.5f); j.halfW = Mathf.Clamp(d.halfW * 0.8f, 0.8f, 2.2f); j.deckOk = true; }
            else j.deckY = g.CenterWorld(b).y + (g.level == "L02" ? 1.2f : 0.3f);   // RulesView.Rebuild's deck height
            var cp = g.CenterWorld(j.crane, false);
            j.nearSign = Vector3.Dot(cp - j.centre, j.axis) >= 0f ? 1 : -1;
            // the deck cells from the crane's end to the far end, two across
            int n = Mathf.Max(3, Mathf.RoundToInt(j.half * 2f / OzCraneLandEnd.CellM));
            float step = j.half * 2f / n; var mats = OzBridgeRestore.Materials(); if (mats == null) return;
            for (int i = 0; i < n; i++)
                for (int s = 0; s < 2; s++)
                {
                    var c = freeCells.Count > 0 ? freeCells.Pop() : new Cell();
                    if (!c.r) { c.r = NewPart("Cell", mats[0]); c.t = c.r.transform; }
                    float a = j.nearSign * (j.half - step * (i + 0.5f)), x = (s == 0 ? -0.5f : 0.5f) * j.halfW;
                    c.t.position = j.centre + j.axis * a + j.axisB * x + Vector3.up * (j.deckY + 0.04f - j.centre.y);
                    c.t.rotation = Quaternion.LookRotation(j.axis, Vector3.up);
                    c.t.localScale = new Vector3(j.halfW * 0.92f, 0.06f, step * 0.92f);
                    c.litAt = c.hitAt = -1f; c.r.enabled = false;
                    j.cells.Add(c);
                }
        }

        float Progress(JobState j)
        {
            var W = game != null ? game.World : null; var c = j.crane;
            if (W != null && c != null && c.CraneWorkUntil > 0 && c.CraneWorkFrom >= 0 && c.CraneWorkUntil > c.CraneWorkFrom)
                return Mathf.Clamp01((float)((W.Time - c.CraneWorkFrom) / (c.CraneWorkUntil - c.CraneWorkFrom)));
            return Mathf.Clamp01((Time.time - j.startAt) / 15f);                                     // Zod's drive in and out: no work window
        }

        bool StillWorking(JobState j)
        {
            var c = j.crane;
            if (c == null || c.Destroyed || game == null || game.World == null || game.World.Get(c.Id) != c) return false;
            return c.Waypoints.Count > 0 && c.Waypoints[0].mode == WpMode.CraneRepair && c.Waypoints[0].refId == j.b.Id;
        }

        Vector3 Tip(JobState j)
        {
            if (j.v && !j.hook) j.hook = Deep(j.v.transform, "Crane_Hook");
            if (j.hook) return j.hook.TransformPoint(new Vector3(0f, -0.243f, 0f));                   // apc_crane_rebuild_v1 Crane_Hook: lowest y -0.243 (GLB units)
            return j.v ? j.v.transform.position + Vector3.up * 3.2f : game.CenterWorld(j.crane) + Vector3.up * 3.2f;
        }

        void Update()
        {
            if (game == null) game = ZGame.I;
            float dt = Time.deltaTime, now = Time.time;
            var fx = OpenZFxPlayer.I; var mats = OzBridgeRestore.Materials();
            for (int k = jobs.Count - 1; k >= 0; k--)
            {
                var j = jobs[k];
                if (!j.ended && !StillWorking(j)) Stop(j);                                               // a crane shot dead never sends its CraneAnim off; a call-off
                bool parked = !j.ended && j.crane != null && !j.crane.IsMoving;
                // ---- the orange stream into the span
                if (j.bridge && OzCraneLandEnd.Stream && mats != null && j.cells.Count > 0)
                {
                    bool revived = j.wasDestroyed && j.b != null && !j.b.Destroyed;                      // the rules repaired it: the restore plays now
                    if (parked && !revived && now >= j.nextSpawn && CountFlying() < OzCraneLandEnd.MaxFlying)
                    {
                        j.nextSpawn = now + OzCraneLandEnd.SpawnEvery * Random.Range(0.8f, 1.25f);
                        Spawn(j, Progress(j), mats[0]);
                    }
                    bool fadeOut = revived || (j.ended && now - j.endAt > (j.wasDestroyed ? 2.5f : 0.2f));
                    if (fadeOut) j.fade = Mathf.MoveTowards(j.fade, 0f, dt / (revived ? 0.35f : 0.5f));
                    foreach (var c in j.cells)
                    {
                        if (c.litAt < 0f) continue;
                        float up = Mathf.Clamp01((now - c.litAt) / 0.2f), hit = c.hitAt >= 0f ? Mathf.Clamp01(1f - (now - c.hitAt) / 0.35f) : 0f;
                        float a = (0.42f + 0.1f * Mathf.Sin(now * 5.3f + c.t.position.x * 1.7f + c.t.position.z)) * up * j.fade;
                        c.r.enabled = a > 0.004f; if (c.r.enabled) Props(c.r, j, a, hit * 0.8f);
                    }
                }
                bool flying = false; foreach (var p in pieces) if (p.busy && Owner(p) == j) { flying = true; break; }
                bool gone = j.ended && ((j.fade <= 0.001f && !flying) || now - j.endAt > 6f || j.cells.Count == 0);
                if (gone)
                {
                    foreach (var c in j.cells) { c.r.enabled = false; c.litAt = -1f; freeCells.Push(c); }
                    j.cells.Clear(); jobs.RemoveAt(k);
                }
            }
            StepPieces(fx, now);
        }

        readonly Dictionary<Piece, JobState> owner = new Dictionary<Piece, JobState>();
        JobState Owner(Piece p) => owner.TryGetValue(p, out var j) ? j : null;
        int CountFlying() { int n = 0; foreach (var p in pieces) if (p.busy) n++; return n; }

        void Spawn(JobState j, float prog, Material m)
        {
            int total = j.cells.Count, target = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(prog / 0.92f) * total), 1, total), cell;
            if (j.reserved < target) cell = j.reserved++;
            else cell = Random.Range(0, Mathf.Max(1, j.reserved));
            Piece p = null; foreach (var x in pieces) if (!x.busy) { p = x; break; }
            if (p == null) { p = new Piece { r = NewPart("Piece", m) }; p.t = p.r.transform; pieces.Add(p); }
            p.busy = true; p.cell = cell; owner[p] = j;
            p.from = Tip(j); var c = j.cells[cell].t.position;
            p.to = c + j.axis * Random.Range(-0.35f, 0.35f) + j.axisB * Random.Range(-0.3f, 0.3f);
            p.t0 = Time.time; p.len = Random.Range(OzCraneLandEnd.FlightMin, OzCraneLandEnd.FlightMax);
            p.spin = Random.onUnitSphere * Random.Range(180f, 420f); p.t.rotation = Random.rotation;
            float s = OzCraneLandEnd.PieceSize * Random.Range(0.75f, 1.2f); p.t.localScale = new Vector3(s, s * 0.45f, s * 1.3f);
            p.t.position = p.from; p.r.enabled = true; p.glowT = 0f;
            OzCraneLandEnd.Pieces++;
        }

        void StepPieces(OpenZFxPlayer fx, float now)
        {
            foreach (var p in pieces)
            {
                if (!p.busy) continue;
                var j = Owner(p);
                float k = Mathf.Clamp01((now - p.t0) / Mathf.Max(0.05f, p.len)), e = k * k * (3f - 2f * k);
                var mid = (p.from + p.to) * 0.5f + Vector3.up * (OzCraneLandEnd.Arc + 0.15f * Vector3.Distance(p.from, p.to));
                var pos = (1f - e) * (1f - e) * p.from + 2f * (1f - e) * e * mid + e * e * p.to;   // a short eased arc
                p.t.position = pos; p.t.rotation = Quaternion.Euler(p.spin * Time.deltaTime) * p.t.rotation;
                if (j != null) Props(p.r, j, 0.85f * (1f - 0.3f * k) * j.fade, 0.55f);
                if (fx && (p.glowT -= Time.deltaTime) <= 0f) { p.glowT = 0.035f; fx.EmitChannel("wv2_glow", pos, Vector3.zero, 0.3f, 0.2f, Glow, 0.8f); }
                if (k < 1f) continue;
                p.busy = false; p.r.enabled = false; owner.Remove(p);
                if (j == null || p.cell < 0 || p.cell >= j.cells.Count) continue;
                var c = j.cells[p.cell];
                if (c.litAt < 0f) { c.litAt = now; OzCraneLandEnd.CellsLit++; }
                c.hitAt = now; OzCraneLandEnd.Landed++;
                if (fx) for (int i = 0; i < 3; i++)
                    {
                        var v = (Random.insideUnitSphere + Vector3.up * 1.2f) * Random.Range(1.5f, 3.2f);
                        fx.EmitChannel("wv2_spark", p.to, v, Random.Range(0.18f, 0.35f), Random.Range(0.03f, 0.05f), Spark, 1f);
                    }
            }
        }

        void OnDestroy()
        {
            foreach (var j in jobs) if (j.soundOn && AudioDirector.I) AudioDirector.I.CraneConstructStop(j.key);
        }
    }
}
