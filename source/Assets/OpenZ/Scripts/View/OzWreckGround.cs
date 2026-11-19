// OpenZ (GPL-3.0)
// View only: Scripts/Sim
// is not touched and nothing here is read by the rules (the rules delete a destroyed vehicle at once; its wreck, its pieces and its thrown turret
using System.Collections.Generic;
using System.Diagnostics;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A wreck comes to rest only where something really holds it up; anything else keeps falling under gravity.
    /// <list type="bullet">
    /// <item>SUPPORT (<see cref="Under"/>): under a point, the highest of: the walk layer (terrain, bridge decks, the Unit editor floor and its
    /// platforms' decks, seen from <see cref="AboveM"/> over the point), the level's ground (ZGame.GroundY), a destroyed bridge's standing deck
    /// (OzCraneDeck: the crane repairs from it), and what stands still right under it (a rock, a wall, a building, a resting body; never a body
    /// that still moves, never wreck debris, never water). One ray per point. Resting = its lowest drawn point within <see cref="TolM"/> of that.</item>
    /// <item>HULLS (every vehicle wreck, <see cref="Hulls"/>; OzRammedWreckRest's rule, until now only for rammed or shoved wrecks): the dead
    /// vehicle's hull is measured by <see cref="HullMeasure"/>: its points (<see cref="HullPoints"/>: about <see cref="HullPointBudget"/> real
    /// mesh points over all its solid parts plus each part's 26 outermost points) against the support under EACH of them (a support grid under its
    /// footprint, the points closest to their support, the step cells and what stands under it looked at exactly), so a slope, a deck edge or a
    /// rock under one side is seen. In the
    /// air it falls at 1 g; standing on an edge or a corner it tips over flat (over a drop it slides off the edge and falls on, still turning);
    /// then it lies; it is measured again about twice a second (its support can go; a side it no longer stands on makes it tip). A hull that lies
    /// stable on the ground at its death is left exactly as before. Water, lava, a body PhysX still flies, a ram turn-over or a crush ride that
    /// still places it, the end-of-life sink: their own rules.</item>
    /// <item>FLIPPED HULLS (the DV17 flip, and in the Unit editor every vehicle that falls with its platform): WreckContact froze the body when
    /// it slept or after 6 s wherever it was; now it hands the hull to the same rest (<see cref="FlipFrozen"/>, with the speed it was falling at).</item>
    /// <item>PIECES (<see cref="Pieces"/>): WreckPieces froze a piece (kinematic, colliders off) when it slept, after 8 s or 1.5 s before its end,
    /// wherever it was. Now it stops only when supported (<see cref="PieceMayFreeze"/>); unsupported it is woken and simulated on, and after
    /// <see cref="PieceGraceS"/> more it is brought down (<see cref="OzWreckDrop"/>: straight down at 1 g from the speed it had, turned onto its
    /// nearest face on the way when the fall is longer than <see cref="FlattenFromM"/>, a clank on a hard landing, the water rule at a water
    /// surface). A stopped piece is measured again every <see cref="WatchEveryS"/> (<see cref="PieceWatch"/>): its support gone, it falls. These
    /// pieces are this rule's alone (<see cref="Owns"/>: DebrisAudit's guard no longer wakes them, so two falls never act at once); the sink at its
    /// end, from its life or from the live cap, waits for its landing.</item>
    /// </list>
    /// Switches: <see cref="On"/> (off = exactly as before), <see cref="Hulls"/>, <see cref="Pieces"/>.
    /// </summary>
    public static class OzWreckGround
    {
        public static bool On = true, Hulls = true, Pieces = true;
        /// <summary>Resting = the lowest drawn point within TolM of its support (m); the walk layer is looked for from AboveM over a point (m); an
        /// unsupported piece is simulated on this long before it is brought down (s); a stopped piece is measured again this often (s); the tip
        /// turns at least TipMinRadS rad/s for at most TipMaxS s; a piece is turned onto a face only on a fall longer than FlattenFromM (m).</summary>
        public static float TolM = 0.05f, AboveM = 1.5f, PieceGraceS = 1f, WatchEveryS = 0.5f, TipMinRadS = 0.8f, TipMaxS = 6f, FlattenFromM = 0.15f;
        /// <summary>A hull's points: about this many real mesh points over all its solid parts (each part its share by its vertex count; a part whose
        /// share is under 8: the 8 corners of its own mesh box), plus each sampled part's 26 outermost points (exact for a hull lying on a face, an
        /// edge or a corner).</summary>
        public static int HullPointBudget = 2000;
        /// <summary>The support grid under a hull's footprint (<see cref="HullMeasure"/>): about CellsAim cells (never smaller than CellMinM, never
        /// more than CellsMax); a support step between two cells beside each other is more than SlopeK x the cell + 5 cm (about 24 deg); exact
        /// looks: the RefineN points closest to their estimated support, at most StepRefineMax in the step cells, at most ObstacleMax over what
        /// stands under it (looked for from ObstacleBelowM under its lowest point; small = at most ObstacleSmallM each way; a big one within
        /// ObstacleBandM of its edges; one look per ObstacleCellM part); a point within ContactM of its support stands on it (the tip-over). A
        /// hull that only dropped straight down since its last measure uses it again, less the drop, while it is more than ReuseAboveM over its
        /// support and the measure is at most ReuseS old.</summary>
        public static int CellsAim = 36, CellsMax = 64, RefineN = 10, StepRefineMax = 96, ObstacleMax = 64;
        public static float CellMinM = 0.3f, ContactM = 0.06f, SlopeK = 0.45f, ObstacleBelowM = 1f, ObstacleSmallM = 1.5f, ObstacleBandM = 0.5f, ObstacleCellM = 0.35f, ReuseAboveM = 1.2f, ReuseS = 0.2f;
        /// <summary>Over a drop (the support under its middle lower than the edge it turns on by more than EdgeDropM + EdgeDropK x the distance):
        /// it slides off the edge, gaining EdgeSlideAcc m/s per second (at most EdgeSlideMax m/s).</summary>
        public static float EdgeDropM = 0.3f, EdgeDropK = 0.5f, EdgeSlideAcc = 4f, EdgeSlideMax = 5f;

        /// <summary>Proof counters.</summary>
        public static int HullsWatched, HullFalls, FlipHandovers, PiecesHeldUp, PieceDrops, PieceWatchDrops, PieceWaterHandovers, Landings, Thumps, Flattened,
            Retips, EdgeSlides, EdgeFalls, SpinsStopped, RetiresDeferred, AuditSkips, Measures, Reused;
        public static float MaxHullFallM, MaxPieceDropM, MaxDropSpeed;
        public static string Last = "-";

        static readonly RaycastHit[] hits = new RaycastHit[32];
        static readonly List<Vector3> vbuf = new List<Vector3>(4096);
        static readonly List<Renderer> rbuf = new List<Renderer>(64);
        static int stillMask = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            HullsWatched = HullFalls = FlipHandovers = PiecesHeldUp = PieceDrops = PieceWatchDrops = PieceWaterHandovers = Landings = Thumps = Flattened = 0;
            Retips = EdgeSlides = EdgeFalls = SpinsStopped = RetiresDeferred = AuditSkips = Measures = Reused = 0;
            MaxHullFallM = MaxPieceDropM = MaxDropSpeed = 0f; Last = "-"; stillMask = -1;
            samples.Clear(); outer.Clear(); MeterReset();
        }

        // ------------------------------------------------------------------ the cost meter (the checks: its worst frame, its mean, its rays)
        static long mTicks, mWorst, mTotal, mRaysTotal; static int mFrame = -1, mRays, mRaysWorst, mDepth, mFrom;
        static readonly List<long> mFrames = new List<long>(16384);
        /// <summary>Checks: keep every frame's time of this rule (for <see cref="MeterP99Ms"/>; off in play).</summary>
        public static bool MeterRecord;
        static void Roll()
        {
            int f = Time.frameCount; if (f == mFrame) return;
            if (mTicks > mWorst) mWorst = mTicks;
            if (mRays > mRaysWorst) mRaysWorst = mRays;
            if (MeterRecord && mTicks > 0 && mFrames.Count < 1 << 20) mFrames.Add(mTicks);
            mFrame = f; mTicks = 0; mRays = 0;
        }
        static long T0() { Roll(); return mDepth++ == 0 ? Stopwatch.GetTimestamp() : 0L; }
        static void T1(long t0)
        {
            if (--mDepth > 0) return;
            mDepth = 0;
            if (t0 != 0L) { long d = Stopwatch.GetTimestamp() - t0; mTicks += d; mTotal += d; }
        }
        /// <summary>Checks: the meter from now.</summary>
        public static void MeterReset() { mTicks = mWorst = mTotal = mRaysTotal = 0; mRays = mRaysWorst = 0; mFrame = Time.frameCount; mFrom = Time.frameCount; mDepth = 0; mFrames.Clear(); }
        /// <summary>Checks, since MeterReset: this rule's own time in its worst frame and per frame on average (ms), its rays in its worst frame and
        /// in all.</summary>
        public static float MeterWorstMs { get { Roll(); return (float)(System.Math.Max(mWorst, mTicks) * 1000.0 / Stopwatch.Frequency); } }
        public static float MeterMeanMs => (float)(mTotal * 1000.0 / Stopwatch.Frequency / Mathf.Max(1, Time.frameCount - mFrom));
        public static int MeterWorstRays { get { Roll(); return Mathf.Max(mRaysWorst, mRays); } }
        /// <summary>Checks (MeterRecord on): this rule's time in the frame at the 99th percentile of all frames since MeterReset (ms; a frame it did
        /// nothing in counts as 0: a one-time mesh pick at a type's first death does not decide it).</summary>
        public static float MeterP99Ms
        {
            get
            {
                Roll(); int all = Mathf.Max(1, Time.frameCount - mFrom), k = Mathf.CeilToInt(all * 0.01f);
                if (k > mFrames.Count) return 0f;
                mFrames.Sort();
                return (float)(mFrames[mFrames.Count - k] * 1000.0 / Stopwatch.Frequency);
            }
        }
        public static long MeterRays => mRaysTotal;

        // ------------------------------------------------------------------ hulls (OzRammedWreckRest)
        /// <summary>OzRammedWreckRest: the support-only rest for every vehicle wreck is on.</summary>
        public static bool HullsOn() => On && Hulls;

        /// <summary>OzRammedWreckRest.AtDeath: this dead view is a vehicle hull that rests only on support (every vehicle, not the shuttle: its own
        /// break-up and crash rules).</summary>
        public static bool HullFor(UnitView v) => On && Hulls && v && v.Obj != null && v.Obj.Kind == ZKind.Vehicle && !v.Obj.IsShuttle;

        /// <summary>OzRammedWreckRest, a hull in the support-only rest: left to another rule now (a ram's turn-over or a crush ride still places it,
        /// open water under it: the water rule).</summary>
        public static bool HullWaits(UnitView v, Transform root)
        {
            if (!v || v.Obj == null || !root) return true;
            if (OzMammothRamView.Holds(v.Obj)) return true;
            if (v.GetComponent<OzCrushRide>()) return true;                                                   // a dead crusher's ride eases its model down first
            var rock = v.GetComponent<OzRockBody>(); if (rock && (rock.OnRock || rock.TouchCount > 0)) return true;   // a flipped hull on a rock: the rock rule (it wakes it when its rock goes)
            var g = ZGame.I;
            if (g) { var p = root.position; if (OzWaterLanding.OpenWaterAt(p, g.GroundY(p.x, p.z), out _)) return true; }
            return false;
        }

        /// <summary>OzRammedWreckRest: a hull fell (counted; the gap it fell from).</summary>
        public static void HullFell(UnitView v, float gap)
        {
            HullFalls++; MaxHullFallM = Mathf.Max(MaxHullFallM, gap);
            Last = $"{(v && v.Obj != null ? v.Obj.Type + " #" + v.Obj.Id : "a wreck")}: {gap:0.00} m over its support, falls";
        }

        /// <summary>OzRammedWreckRest.Tip: its slowest turn (rad/s) after t s of tipping, and its longest tip (s); off = the values before.</summary>
        public static float TipMinRad(float old, float t) => On ? Mathf.Max(old, Mathf.Lerp(old, TipMinRadS, t / 0.5f)) : old;   // from its own slow start up to TipMinRadS in half a second
        public static float TipMax(float old) => On ? Mathf.Max(old, TipMaxS) : old;

        /// <summary>OzRammedWreckRest: the next look at a resting hull in about half a second, spread by its id (hulls that came to rest on one frame
        /// are not all measured again on one later frame).</summary>
        public static float RecheckS(int id) => 0.4f + 0.2f * Spread(id);

        /// <summary>WreckContact, the frozen flipped hull: hands it to the support-only rest with the speed it was falling at.</summary>
        public static void FlipFrozen(Component c, float fallSpeed)
        {
            if (!On || !Hulls || !c) return;
            var v = c.GetComponent<UnitView>(); if (!v) return;
            FlipHandovers++;
            OzRammedWreckRest.Ground(v, fallSpeed);
        }

        /// <summary>A part that is no solid hull: fire, flame, smoke, scorch, dust and spark meshes (the hull's measure leaves them out).</summary>
        public static bool FxName(string lower) => lower.Contains("fire") || lower.Contains("flame") || lower.Contains("smoke") || lower.Contains("scorch") || lower.Contains("dust") || lower.Contains("spark");

        // ------------------------------------------------------------------ the hull's points
        static readonly List<Renderer> hpR = new List<Renderer>(128);
        static readonly List<Mesh> hpM = new List<Mesh>(128);
        sealed class Cached { public Vector3[] pts; public Bounds at; }
        static readonly Dictionary<long, Cached> samples = new Dictionary<long, Cached>();
        static readonly Dictionary<int, Cached> outer = new Dictionary<int, Cached>();
        static readonly float[] dLo = new float[13], dHi = new float[13]; static readonly int[] dLoI = new int[13], dHiI = new int[13];

        static bool Readable(Renderer r, Mesh m) => m && m.isReadable && m.vertexCount > 0 && !(r is SkinnedMeshRenderer);

        /// <summary>OzRammedWreckRest.Points: the hull's points in the root's frame (inv = world to root) from its solid parts (solid = its own test).
        /// EVERY solid part (the old list stopped after 2400 points = the first two or three parts), within a budget: a readable part gets its share
        /// of <see cref="HullPointBudget"/> by its vertex count as an even pick of its real points, plus its 26 outermost points; a part whose share
        /// is under 8 (a track link): the 8 corners of its own mesh box (it turns with the part); an unreadable mesh the same; a skinned part or no
        /// mesh: the corners of its drawn box, as before. A Bizon about 2,800 points, a Mammoth about 2,700 (the kit's first round: 25,500 / 21,300).
        /// A mesh's picks are kept (made again when its box changes: a crushed copy).</summary>
        public static void HullPoints(List<Renderer> rs, System.Predicate<Renderer> solid, Matrix4x4 inv, List<Vector3> pts)
        {
            long t0 = T0();
            try
            {
                hpR.Clear(); hpM.Clear(); long total = 0;
                foreach (var r in rs)
                {
                    if (!solid(r)) continue;
                    Mesh m = null; if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh; else if (r.TryGetComponent<MeshFilter>(out var mf)) m = mf.sharedMesh;
                    hpR.Add(r); hpM.Add(m);
                    if (Readable(r, m)) total += m.vertexCount;
                }
                for (int i = 0; i < hpR.Count; i++)
                {
                    var r = hpR[i]; var m = hpM[i]; var to = inv * r.localToWorldMatrix;
                    if (Readable(r, m))
                    {
                        int vc = m.vertexCount, share = total > 0 ? (int)(HullPointBudget * (long)vc / total) : vc;
                        if (share >= 8 || vc <= 8)
                        {
                            foreach (var p in Sample(m, Mathf.Max(share, Mathf.Min(8, vc)))) pts.Add(to.MultiplyPoint3x4(p));
                            if (vc > 8) foreach (var p in Outer(m)) pts.Add(to.MultiplyPoint3x4(p));
                            continue;
                        }
                    }
                    if (m && !(r is SkinnedMeshRenderer))
                    {   // a small part (its share under 8) or an unreadable mesh: its own mesh box in its own frame (it turns with the part)
                        var mb = m.bounds;
                        for (int c = 0; c < 8; c++) pts.Add(to.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, Corner(c))));
                        continue;
                    }
                    var b = r.bounds;
                    for (int c = 0; c < 8; c++) pts.Add(inv.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, Corner(c))));
                }
            }
            finally { T1(t0); }
        }

        /// <summary>An even pick of about n of the mesh's own points (kept per mesh and n).</summary>
        static Vector3[] Sample(Mesh m, int n)
        {
            long key = ((long)m.GetInstanceID() << 22) ^ n;
            if (samples.TryGetValue(key, out var c) && c.at == m.bounds) return c.pts;
            vbuf.Clear(); m.GetVertices(vbuf);
            int step = Mathf.Max(1, Mathf.CeilToInt(vbuf.Count / (float)Mathf.Max(1, n)));
            var a = new Vector3[(vbuf.Count + step - 1) / step];
            for (int i = 0, j = 0; i < vbuf.Count; i += step, j++) a[j] = vbuf[i];
            samples[key] = new Cached { pts = a, at = m.bounds };
            return a;
        }

        /// <summary>The mesh's outermost points along the 26 directions of its own box (its 6 faces, 12 edges, 8 corners): the lowest point of a part
        /// lying on a face, an edge or a corner is one of them (kept per mesh; one pass over its points).</summary>
        static Vector3[] Outer(Mesh m)
        {
            int key = m.GetInstanceID();
            if (outer.TryGetValue(key, out var c) && c.at == m.bounds) return c.pts;
            vbuf.Clear(); m.GetVertices(vbuf);
            for (int k = 0; k < 13; k++) { dLo[k] = float.MaxValue; dHi[k] = float.MinValue; dLoI[k] = dHiI[k] = 0; }
            for (int i = 0; i < vbuf.Count; i++)
            {
                var v = vbuf[i];
                for (int k = 0; k < 13; k++)
                {
                    float d = Dir13(k, v);
                    if (d < dLo[k]) { dLo[k] = d; dLoI[k] = i; }
                    if (d > dHi[k]) { dHi[k] = d; dHiI[k] = i; }
                }
            }
            var set = new List<int>(26);
            for (int k = 0; k < 13; k++) { if (!set.Contains(dLoI[k])) set.Add(dLoI[k]); if (!set.Contains(dHiI[k])) set.Add(dHiI[k]); }
            var a = new Vector3[set.Count]; for (int i = 0; i < a.Length; i++) a[i] = vbuf[set[i]];
            outer[key] = new Cached { pts = a, at = m.bounds };
            return a;
        }

        static float Dir13(int k, Vector3 v)
        {
            switch (k)
            {
                case 0: return v.x;
                case 1: return v.y;
                case 2: return v.z;
                case 3: return v.x + v.y;
                case 4: return v.x - v.y;
                case 5: return v.x + v.z;
                case 6: return v.x - v.z;
                case 7: return v.y + v.z;
                case 8: return v.y - v.z;
                case 9: return v.x + v.y + v.z;
                case 10: return v.x + v.y - v.z;
                case 11: return v.x - v.y + v.z;
                default: return -v.x + v.y + v.z;
            }
        }

        static Vector3 Corner(int c) => new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1);

        // ------------------------------------------------------------------ the hull's measure (one per hull)
        // shared scratch (the main thread measures one hull at a time)
        const int GridCap = 128, Levels = 5, Sub = 3, HitCap = 8, ObCap = 1024, LookCap = 512, PickCap = GridCap * Sub * Sub * Levels;
        static Vector3[] wp = new Vector3[4096]; static float[] est = new float[4096]; static int[] wpCell = new int[4096], seenAt = new int[4096]; static int seenStamp;
        static readonly float[] cLow = new float[GridCap], cTop = new float[GridCap], cSup = new float[GridCap], cGx = new float[GridCap], cGz = new float[GridCap], cLo = new float[GridCap], cHi = new float[GridCap];
        static readonly bool[] cStep = new bool[GridCap]; static readonly int[] cLevN = new int[GridCap], cHitN = new int[GridCap];
        static readonly float[] cLev = new float[GridCap * Levels], cHitY = new float[GridCap * HitCap]; static readonly bool[] cHitStill = new bool[GridCap * HitCap];
        static readonly float[] pkD = new float[PickCap], srtD = new float[PickCap]; static readonly int[] pkI = new int[PickCap], srtI = new int[PickCap];
        static readonly int[] rIdx = new int[64]; static readonly float[] rEst = new float[64];
        static readonly int[] lkI = new int[LookCap]; static readonly float[] lkG = new float[LookCap]; static int lkN; static float lkGap;
        static readonly Collider[] obs = new Collider[32];
        static readonly float[] obY = new float[ObCap]; static readonly int[] obI = new int[ObCap];

        /// <summary>
        /// One hull's measure against the support under it (OzRammedWreckRest keeps one per hull). Its points (world) are put on a grid over its
        /// footprint (about <see cref="CellsAim"/> cells). Under each cell's middle ONE ray from over the cell's highest point keeps every support
        /// there (the walk layer, what stands still, the ground, a standing bridge deck); each point of the cell takes the one that holds IT (the
        /// same rule as <see cref="Under"/>: the walk layer seen from AboveM over the point, what stands still only right under it). A cell whose
        /// points stand over different supports (a rock or a deck top inside it), or whose support is a step away from the cell beside it (more
        /// than a slope: a deck edge, a rock, a wall top), is a step cell; elsewhere the support under every point is estimated from its cell and
        /// the slope to the cells beside it. Exact looks (one ray each): the <see cref="RefineN"/> points closest to their estimated support; in
        /// every step cell, for each third of it each way and each support level around it, the point closest above that level (at most
        /// <see cref="StepRefineMax"/>, the closest first); and what stands under its footprint (<see cref="Obstacles"/>: a rock, a block, a
        /// wall, a platform; a small one all over, a big one near its edges, at most <see cref="ObstacleMax"/>). Gap = the smallest clearance
        /// found (the first round: the 24 lowest points by height only). Contacts = the points within <see cref="ContactM"/> of their support (the
        /// tip-over's edge); its middle = the mean of its points. The same pose on the next frame uses the measure again (the tip and the rest
        /// share it); a hull that only dropped straight down since uses it too, less the drop, while it is more than <see cref="ReuseAboveM"/>
        /// over its support and the measure is under <see cref="ReuseS"/> old (a fall: the support under every point is the same). Rays per
        /// measure, measured offline on the Bizon and Mammoth (README): about 45-55 on flat ground, a slope or rolling ground, 75-150 at a rock, up
        /// to about 190 at a deck edge (the first round's tip alone: 2,000-13,000 for a Bizon on an edge).
        /// </summary>
        public sealed class HullMeasure
        {
            Vector3 atPos; Quaternion atRot; int atFrame = int.MinValue, atCount = -1; float atGap = float.NaN, atTime;
            Vector3 comLocal;
            /// <summary>The mean of its points (world, at the measured pose), the farthest point from it (m), the rays of the last measure.</summary>
            public Vector3 Com { get; private set; }
            public float Reach { get; private set; }
            public int Rays { get; private set; }
            /// <summary>The points it stands on (world, at the measured pose).</summary>
            public readonly List<Vector3> Touch = new List<Vector3>(256);

            /// <summary>The smallest clearance of its points over the support right under each (m; NaN = no point or no support under any).</summary>
            public float Gap(Transform root, List<Vector3> pts) => Gap(root, pts, true);

            float Gap(Transform root, List<Vector3> pts, bool fall)
            {
                if (pts.Count == atCount && atFrame != int.MinValue)
                {
                    Vector3 p = root.position; Quaternion q = root.rotation;
                    if (q.x == atRot.x && q.y == atRot.y && q.z == atRot.z && q.w == atRot.w && p.x == atPos.x && p.z == atPos.z)
                    {
                        float drop = atPos.y - p.y;
                        if (drop == 0f && Time.frameCount - atFrame <= 1) return atGap;                              // the same pose: the tip and the rest share it
                        if (fall && drop > 0f && Time.time - atTime <= ReuseS && atGap - drop > ReuseAboveM) { Reused++; return atGap - drop; }   // straight down since: a fall
                    }
                }
                long t0 = T0();
                try { Measure(root, pts); }
                finally { T1(t0); }
                return atGap;
            }

            /// <summary>OzRammedWreckRest.Tip: the points it stands on and its middle (the current pose); false = none.</summary>
            public bool Contacts(Transform root, List<Vector3> pts, List<Vector3> into, out Vector3 com)
            {
                Gap(root, pts, false);
                into.Clear(); into.AddRange(Touch); com = Com;
                return into.Count > 0;
            }

            /// <summary>It lies stable: its middle over what it stands on (within 3 cm, the tip-over's own rule), or it stands on nothing.</summary>
            public bool Steady(Transform root, List<Vector3> pts)
            {
                float g = Gap(root, pts, false);
                if (float.IsNaN(g) || Touch.Count == 0) return true;
                return Worst(Touch, Com, out _, out _) >= -0.03f;
            }

            /// <summary>OzRammedWreckRest.Tip, turning about `pivot` toward `dir`: over a drop (an edge of a deck, a platform, a rock: the support under
            /// its middle far below the edge) it slides off the edge as it turns, faster and faster; on a slope or flat ground nothing (slide = 0).</summary>
            public void Slide(Transform root, Vector3 com, Vector3 pivot, Vector3 dir, ref float slide, float dt)
            {
                long t0 = T0();
                try
                {
                    float d = new Vector2(com.x - pivot.x, com.z - pivot.z).magnitude;
                    float s = Under(new Vector3(com.x, pivot.y + 0.05f, com.z), root);
                    bool drop = s == float.MinValue || pivot.y - s > EdgeDropM + EdgeDropK * d;
                    if (!drop) { slide = 0f; return; }
                    if (slide <= 0f) EdgeSlides++;
                    slide = Mathf.Min(slide + EdgeSlideAcc * dt, EdgeSlideMax);
                    root.position += new Vector3(dir.x, 0f, dir.z).normalized * (slide * dt);
                }
                finally { T1(t0); }
            }

            /// <summary>OzRammedWreckRest's fall, a hull that went over an edge turning: it turns on about its middle at `spin` rad/s while it is high
            /// enough that no point of it can reach its support this frame (gapLeft = its gap after this frame's fall step); near the support the
            /// turn stops (it lands, then the tip-over lays it flat). Returns the spin left.</summary>
            public float Spin(Transform root, Vector3 axis, float spin, float gapLeft, float vy, float dt)
            {
                if (spin <= 0f) return 0f;
                if (gapLeft <= (spin * Mathf.Max(0.5f, Reach) + vy) * dt * 2f + 0.1f) { SpinsStopped++; return 0f; }
                root.RotateAround(root.TransformPoint(comLocal), axis, spin * Mathf.Rad2Deg * dt);
                return spin;
            }

            void Measure(Transform root, List<Vector3> pts)
            {
                Measures++; long rays0 = mRaysTotal;
                int n = pts.Count; atPos = root.position; atRot = root.rotation; atFrame = Time.frameCount; atTime = Time.time; atCount = n; atGap = float.NaN; Touch.Clear();
                if (n == 0) return;
                if (wp.Length < n) { int cap = Mathf.NextPowerOfTwo(n); wp = new Vector3[cap]; est = new float[cap]; wpCell = new int[cap]; seenAt = new int[cap]; seenStamp = 0; }
                var m = root.localToWorldMatrix; Vector3 sumL = Vector3.zero;
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue, yLo = float.MaxValue, yHi = float.MinValue;
                for (int i = 0; i < n; i++)
                {
                    var l = pts[i]; var w = m.MultiplyPoint3x4(l); wp[i] = w; sumL += l;
                    if (w.x < x0) x0 = w.x;
                    if (w.x > x1) x1 = w.x;
                    if (w.z < z0) z0 = w.z;
                    if (w.z > z1) z1 = w.z;
                    if (w.y < yLo) yLo = w.y;
                    if (w.y > yHi) yHi = w.y;
                }
                comLocal = sumL / n; var com = m.MultiplyPoint3x4(comLocal); Com = com;
                float reach = 0f; for (int i = 0; i < n; i++) reach = Mathf.Max(reach, (wp[i] - com).sqrMagnitude);
                Reach = Mathf.Sqrt(reach);
                // the grid over its footprint; one ray under each cell's middle keeps every support there
                float W = Mathf.Max(x1 - x0, 1e-3f), D = Mathf.Max(z1 - z0, 1e-3f);
                float s = Mathf.Max(CellMinM, Mathf.Sqrt(W * D / Mathf.Max(1, CellsAim)));
                int nx, nz, lim = Mathf.Clamp(CellsMax, 1, GridCap);
                while (true) { nx = (int)(W / s) + 1; nz = (int)(D / s) + 1; if (nx * nz <= lim) break; s *= 1.15f; }
                int nc = nx * nz;
                for (int c = 0; c < nc; c++) { cLow[c] = float.MaxValue; cTop[c] = float.MinValue; cSup[c] = float.MinValue; cStep[c] = false; cLevN[c] = 0; cHitN[c] = 0; }
                for (int i = 0; i < n; i++)
                {
                    var w = wp[i]; int c = Mathf.Clamp((int)((w.x - x0) / s), 0, nx - 1) + Mathf.Clamp((int)((w.z - z0) / s), 0, nz - 1) * nx; wpCell[i] = c;
                    if (w.y < cLow[c]) cLow[c] = w.y;
                    if (w.y > cTop[c]) cTop[c] = w.y;
                }
                for (int c = 0; c < nc; c++)
                {
                    if (cLow[c] == float.MaxValue) continue;
                    Hits(c, x0 + (c % nx + 0.5f) * s, z0 + (c / nx + 0.5f) * s, cLow[c], cTop[c], root);
                    cSup[c] = Lvl(c, cLow[c]);
                    if (cSup[c] > float.MinValue) { cLev[c * Levels] = cSup[c]; cLevN[c] = 1; }
                }
                // a support higher than its lowest point's that some of its points stand over (a rock, a deck top inside the cell): a step cell
                for (int i = 0; i < n; i++)
                {
                    int c = wpCell[i]; if (cSup[c] == float.MinValue) continue;
                    float L = Lvl(c, wp[i].y); if (L > cSup[c] + 0.05f) { cStep[c] = true; AddLevel(c, L); }
                }
                // the slope to the cells beside it; a step there (more than a slope): no slope, its level kept for the exact looks
                float slope = SlopeK * s + 0.05f;
                for (int c = 0; c < nc; c++)
                {
                    cGx[c] = cGz[c] = 0f; cLo[c] = cHi[c] = cSup[c];
                    if (cSup[c] == float.MinValue) continue;
                    int ix = c % nx, iz = c / nx;
                    float l = Side(ix > 0 ? c - 1 : -1, c, slope), r = Side(ix < nx - 1 ? c + 1 : -1, c, slope);
                    float b = Side(iz > 0 ? c - nx : -1, c, slope), f = Side(iz < nz - 1 ? c + nx : -1, c, slope);
                    cGx[c] = Grad(l, cSup[c], r, s); cGz[c] = Grad(b, cSup[c], f, s);
                    Span(c, l); Span(c, r); Span(c, b); Span(c, f);
                }
                // the support under every point of a smooth cell, estimated (the closest ones kept for the exact look); in a step cell, for each third
                // of it each way and each level around it, the point closest above that level
                for (int c = 0; c < nc; c++) if (cStep[c]) for (int t = 0, b0 = c * Sub * Sub * Levels; t < Sub * Sub * Levels; t++) { pkD[b0 + t] = float.MaxValue; pkI[b0 + t] = -1; }
                int rn = 0, rMax = Mathf.Clamp(RefineN, 1, rIdx.Length); float estMin = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var w = wp[i]; int c = wpCell[i]; est[i] = float.MaxValue;
                    if (cSup[c] == float.MinValue) continue;
                    float cx = x0 + (c % nx + 0.5f) * s, cz = z0 + (c / nx + 0.5f) * s;
                    if (cStep[c])
                    {
                        int q = Mathf.Clamp((int)((w.x - cx + 0.5f * s) / s * Sub), 0, Sub - 1) + Sub * Mathf.Clamp((int)((w.z - cz + 0.5f * s) / s * Sub), 0, Sub - 1);
                        int b0 = (c * Sub * Sub + q) * Levels;
                        for (int k = 0; k < cLevN[c]; k++)
                        {
                            float d = w.y - cLev[c * Levels + k];
                            if (d >= -0.1f && d < pkD[b0 + k]) { pkD[b0 + k] = d; pkI[b0 + k] = i; }
                        }
                        continue;
                    }
                    float sp = Mathf.Clamp(cSup[c] + cGx[c] * (w.x - cx) + cGz[c] * (w.z - cz), cLo[c], cHi[c]);
                    float e = w.y - sp; est[i] = e; if (e < estMin) estMin = e;
                    if (rn < rMax) { rIdx[rn] = i; rEst[rn] = e; rn++; }
                    else { int hi = 0; for (int k = 1; k < rn; k++) if (rEst[k] > rEst[hi]) hi = k; if (e < rEst[hi]) { rIdx[hi] = i; rEst[hi] = e; } }
                }
                // the exact looks
                if (++seenStamp == int.MaxValue) { seenStamp = 1; System.Array.Clear(seenAt, 0, seenAt.Length); }
                lkN = 0; lkGap = float.MaxValue;
                for (int j = 0; j < rn; j++) Look(rIdx[j], root);
                int pm = 0;
                for (int c = 0; c < nc; c++)
                {
                    if (!cStep[c]) continue;
                    for (int t = 0, b0 = c * Sub * Sub * Levels; t < Sub * Sub * Levels; t++) if (pkI[b0 + t] >= 0) { srtD[pm] = pkD[b0 + t]; srtI[pm] = pkI[b0 + t]; pm++; }
                }
                if (pm > 1) System.Array.Sort(srtD, srtI, 0, pm);
                for (int j = 0, left = Mathf.Max(0, StepRefineMax); j < pm && left > 0; j++) { int was = lkN; Look(srtI[j], root); if (lkN > was) left--; }
                Obstacles(root, n, x0, x1, z0, z1, yLo, yHi);
                float gap = lkGap;
                if (gap == float.MaxValue) gap = estMin;
                Rays = (int)(mRaysTotal - rays0);
                if (gap == float.MaxValue) return;                                                                // nothing under any point
                atGap = gap;
                // what it stands on: a point within ContactM of its support (estimated in a smooth cell, exact where it was looked at)
                float near = gap + ContactM;
                for (int i = 0; i < n; i++) if (!cStep[wpCell[i]] && est[i] <= near && seenAt[i] != seenStamp) Touch.Add(wp[i]);
                for (int j = 0; j < lkN; j++) if (lkG[j] <= near) Touch.Add(wp[lkI[j]]);
            }

            /// <summary>One exact look at point i (once a measure): its clearance over the support right under it.</summary>
            static void Look(int i, Transform root)
            {
                if (seenAt[i] == seenStamp || lkN >= LookCap) return;
                seenAt[i] = seenStamp;
                float u = Under(wp[i], root), g = u > float.MinValue ? wp[i].y - u : float.MaxValue;
                lkI[lkN] = i; lkG[lkN] = g; lkN++;
                if (g < lkGap) lkGap = g;
            }

            /// <summary>Cell c: every support under (x, z) in one ray from AboveM over its highest point down to 80 m under its lowest (the walk layer,
            /// what stands still: never a body that still moves or wreck debris), the ground and a standing bridge deck there.</summary>
            static void Hits(int c, float x, float z, float yLow, float yTop, Transform self)
            {
                mRays++; mRaysTotal++;
                int b = c * HitCap, k = 0, walk = ZGame.WalkLayer, mask = (walk >= 0 ? 1 << walk : 0) | StillMask();
                if (mask != 0)
                {
                    int n = Physics.RaycastNonAlloc(new Vector3(x, yTop + AboveM, z), Vector3.down, hits, yTop - yLow + AboveM + 80f, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        var col = hits[i].collider; if (!col || (self && col.transform.IsChildOf(self))) continue;
                        bool still = walk < 0 || col.gameObject.layer != walk;
                        if (still) { var a = col.attachedRigidbody; if (a && (!a.isKinematic || a.GetComponent<WreckPieces>())) continue; }
                        Keep(b, ref k, hits[i].point.y, still);
                    }
                }
                var g = ZGame.I;
                if (g)
                {
                    Keep(b, ref k, g.GroundY(x, z), false);
                    var p = new Vector3(x, yLow, z); var d = OzCraneDeck.Under(g, p, 0.3f);
                    if (d != null) { float a = d.Along(p); if (d.Standing(a, d.Across(p))) Keep(b, ref k, d.Y(a), false); }
                }
                cHitN[c] = k;
            }

            static void Keep(int b, ref int k, float y, bool still)
            {   // the HitCap highest kept (the supports near the hull)
                if (k < HitCap) { cHitY[b + k] = y; cHitStill[b + k] = still; k++; return; }
                int lo = 0; for (int j = 1; j < HitCap; j++) if (cHitY[b + j] < cHitY[b + lo]) lo = j;
                if (y > cHitY[b + lo]) { cHitY[b + lo] = y; cHitStill[b + lo] = still; }
            }

            /// <summary>The support of cell c that holds a point at height y (Under's rule: the walk layer and the ground seen from AboveM over it,
            /// what stands still only right under it).</summary>
            static float Lvl(int c, float y)
            {
                float best = float.MinValue; int b = c * HitCap;
                for (int k = 0; k < cHitN[c]; k++) { float h = cHitY[b + k]; if ((cHitStill[b + k] ? h <= y + 0.1f : h <= y + AboveM) && h > best) best = h; }
                return best;
            }

            static void AddLevel(int c, float L)
            {
                if (cLevN[c] >= Levels) return;
                for (int k = 0; k < cLevN[c]; k++) if (Mathf.Abs(cLev[c * Levels + k] - L) <= 0.02f) return;
                cLev[c * Levels + cLevN[c]++] = L;
            }

            static float Side(int nb, int c, float sl)
            {   // the support of the cell beside it; a step there (a deck edge, a rock, a wall top) marks the cell and keeps that level
                if (nb < 0 || cSup[nb] == float.MinValue) return float.MinValue;
                if (Mathf.Abs(cSup[nb] - cSup[c]) <= sl) return cSup[nb];
                cStep[c] = true; AddLevel(c, cSup[nb]);
                return float.MinValue;
            }

            static void Span(int c, float v) { if (v > float.MinValue) { cLo[c] = Mathf.Min(cLo[c], v); cHi[c] = Mathf.Max(cHi[c], v); } }

            static float Grad(float a, float c, float b, float s)
            {
                if (a > float.MinValue && b > float.MinValue) return (b - a) / (2f * s);
                if (b > float.MinValue) return (b - c) / s;
                if (a > float.MinValue) return (c - a) / s;
                return 0f;
            }

            /// <summary>What stands under its footprint (one box query, from <see cref="ObstacleBelowM"/> under its lowest point up to its top; the
            /// walk layer and what stands still): a small one (at most <see cref="ObstacleSmallM"/> each way: a rock, a crate, a post) all over, a big
            /// one (a block, a wall, a platform, a deck) only within <see cref="ObstacleBandM"/> of its edges (its middle is the grid's); the lowest of
            /// its points over each <see cref="ObstacleCellM"/> part of it looked at exactly, the lowest first, at most <see cref="ObstacleMax"/> in all.</summary>
            static void Obstacles(Transform root, int n, float x0, float x1, float z0, float z1, float yLo, float yHi)
            {
                if (ObstacleMax <= 0) return;
                int walk = ZGame.WalkLayer, mask = (walk >= 0 ? 1 << walk : 0) | StillMask();
                if (mask == 0) return;
                var half = new Vector3((x1 - x0) * 0.5f + 0.1f, (yHi - yLo + ObstacleBelowM) * 0.5f + 0.05f, (z1 - z0) * 0.5f + 0.1f);
                var mid = new Vector3((x0 + x1) * 0.5f, (yHi + yLo - ObstacleBelowM) * 0.5f, (z0 + z1) * 0.5f);
                int k = Physics.OverlapBoxNonAlloc(mid, half, obs, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                int left = ObstacleMax;
                for (int o = 0; o < k && left > 0; o++)
                {
                    var col = obs[o]; if (!col || (root && col.transform.IsChildOf(root))) continue;
                    if (walk < 0 || col.gameObject.layer != walk) { var a = col.attachedRigidbody; if (a && (!a.isKinematic || a.GetComponent<WreckPieces>())) continue; }
                    var b = col.bounds;
                    float ax0 = Mathf.Max(b.min.x, x0), ax1 = Mathf.Min(b.max.x, x1), az0 = Mathf.Max(b.min.z, z0), az1 = Mathf.Min(b.max.z, z1);
                    if (ax0 > ax1 || az0 > az1) continue;
                    bool small = b.size.x <= ObstacleSmallM && b.size.z <= ObstacleSmallM;
                    float cs = Mathf.Max(0.05f, ObstacleCellM); int sx, sz;
                    while (true) { sx = (int)((ax1 - ax0) / cs) + 1; sz = (int)((az1 - az0) / cs) + 1; if (sx * sz <= ObCap) break; cs *= 1.25f; }
                    for (int q = 0; q < sx * sz; q++) { obY[q] = float.MaxValue; obI[q] = -1; }
                    int got = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var w = wp[i];
                        if (w.x < ax0 - 0.02f || w.x > ax1 + 0.02f || w.z < az0 - 0.02f || w.z > az1 + 0.02f || w.y < b.min.y - 0.05f) continue;
                        if (!small && Mathf.Min(Mathf.Min(w.x - b.min.x, b.max.x - w.x), Mathf.Min(w.z - b.min.z, b.max.z - w.z)) > ObstacleBandM) continue;
                        int q = Mathf.Clamp((int)((w.x - ax0) / cs), 0, sx - 1) + Mathf.Clamp((int)((w.z - az0) / cs), 0, sz - 1) * sx;
                        if (w.y < obY[q]) { if (obI[q] < 0) got++; obY[q] = w.y; obI[q] = i; }
                    }
                    if (got == 0) continue;
                    int pm = 0; for (int q = 0; q < sx * sz; q++) if (obI[q] >= 0) { srtD[pm] = obY[q]; srtI[pm] = obI[q]; pm++; }
                    if (pm > 1) System.Array.Sort(srtD, srtI, 0, pm);
                    for (int j = 0; j < pm && left > 0; j++) { int was = lkN; Look(srtI[j], root); if (lkN > was) left--; }
                }
            }
        }

        /// <summary>The side on which nothing holds it (the tip-over's rule, as OzRammedWreckRest.Tip): over 16 directions, how far what it stands on
        /// reaches past its middle; the least of them (negative = its middle is that far outside what it stands on), with that direction and point.</summary>
        public static float Worst(List<Vector3> con, Vector3 com, out Vector3 dir, out Vector3 pivot)
        {
            float worst = float.MaxValue; dir = Vector3.right; pivot = con.Count > 0 ? con[0] : com;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); float s = float.MinValue; Vector3 at = pivot;
                for (int i = 0; i < con.Count; i++) { float v = (con[i].x - com.x) * d.x + (con[i].z - com.z) * d.z; if (v > s) { s = v; at = con[i]; } }
                if (s < worst) { worst = s; dir = d; pivot = at; }
            }
            return worst;
        }

        // ------------------------------------------------------------------ support
        /// <summary>The highest solid support under a point (see the class notes); float.MinValue when there is none. `self` and everything under it
        /// is never its own support. One ray from AboveM over the point: the walk layer counts from there, what stands still only right under the
        /// point (the first round cast two rays: the same answer).</summary>
        public static float Under(Vector3 p, Transform self)
        {
            mRays++; mRaysTotal++;
            float best = float.MinValue;
            int walk = ZGame.WalkLayer, mask = (walk >= 0 ? 1 << walk : 0) | StillMask();
            if (mask != 0)
            {
                int n = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + AboveM, p.z), Vector3.down, hits, AboveM + 80f, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var c = hits[i].collider; if (!c || (self && c.transform.IsChildOf(self))) continue;
                    float y = hits[i].point.y;
                    if (walk < 0 || c.gameObject.layer != walk)
                    {   // what stands still right under it (never over it: a roof beside it is no floor)
                        if (y > p.y + 0.1f) continue;
                        var a = c.attachedRigidbody;
                        if (a && (!a.isKinematic || a.GetComponent<WreckPieces>())) continue;                // a body that still moves holds nothing up for long; wreck debris is no floor
                    }
                    if (y > best) best = y;
                }
            }
            var g = ZGame.I;
            if (g)
            {
                float gy = g.GroundY(p.x, p.z); if (gy <= p.y + AboveM && gy > best) best = gy;
                var d = OzCraneDeck.Under(g, p, 0.3f);
                if (d != null) { float a = d.Along(p); if (d.Standing(a, d.Across(p))) { float dy = d.Y(a); if (dy <= p.y + AboveM && dy > best) best = dy; } }
            }
            return best;
        }

        /// <summary>Every layer a resting wreck may lie on besides the walk layer: not the light or heavy debris (they never stay), not Ignore Raycast,
        /// Water or UI.</summary>
        static int StillMask()
        {
            if (stillMask != -1) return stillMask;
            int m = ~0;
            m &= ~(1 << BuildingBreaker.DebrisLayer);
            int heavy = LayerMask.NameToLayer("DebrisHeavy"); m &= ~(1 << (heavy >= 0 ? heavy : BuildingBreaker.HeavyLayer));
            m &= ~((1 << 2) | (1 << 4) | (1 << 5));
            if (ZGame.WalkLayer < 0) return m;                                                                  // not cached until the walk layer is known
            m &= ~(1 << ZGame.WalkLayer);
            return stillMask = m;
        }

        /// <summary>The drawn box of everything solid under t (meshes only: no particles, trails, lines); false = nothing drawn.</summary>
        public static bool Box(Transform t, out Bounds b)
        {
            b = default; bool any = false;
            if (!t) return false;
            rbuf.Clear(); t.GetComponentsInChildren(false, rbuf);
            foreach (var r in rbuf)
            {
                if (!r || !r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450) continue;                         // glows, cones, decals
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            }
            return any;
        }

        /// <summary>The lowest drawn point of everything solid under t (its real mesh points, at most 400 a mesh, plus its 26 outermost points; an
        /// unreadable mesh: its drawn box) and its drawn box; false = nothing drawn.</summary>
        public static bool Lowest(Transform t, out float y, out Bounds b)
        {
            y = 0f;
            if (!Box(t, out b)) return false;
            y = b.max.y;
            foreach (var r in rbuf)
            {
                if (!r || !r.enabled || (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450)) continue;
                if (r is SkinnedMeshRenderer) { y = Mathf.Min(y, r.bounds.min.y); continue; }
                if (!(r is MeshRenderer)) continue;
                var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null;
                if (!m || !m.isReadable || m.vertexCount == 0) { y = Mathf.Min(y, r.bounds.min.y); continue; }
                var w = r.localToWorldMatrix;
                foreach (var p in Sample(m, 400)) { float py = w.MultiplyPoint3x4(p).y; if (py < y) y = py; }
                if (m.vertexCount > 8) foreach (var p in Outer(m)) { float py = w.MultiplyPoint3x4(p).y; if (py < y) y = py; }
            }
            return true;
        }

        /// <summary>The highest support under a drawn box's bottom: its middle, then its footprint (4 points 20 % in from its edges).</summary>
        public static float SupportUnder(Bounds b, Transform self) => SupportUnder(b, b.min.y, self);

        /// <summary>The same at the height y (its real lowest point).</summary>
        public static float SupportUnder(Bounds b, float y, Transform self)
        {
            float best = Under(new Vector3(b.center.x, y, b.center.z), self);
            if (best > float.MinValue && y - best <= TolM) return best;
            for (int k = 0; k < 4; k++)
            {
                var q = new Vector3(Mathf.Lerp(b.min.x, b.max.x, (k & 1) == 0 ? 0.2f : 0.8f), y, Mathf.Lerp(b.min.z, b.max.z, (k & 2) == 0 ? 0.2f : 0.8f));
                float u = Under(q, self); if (u > best) best = u;
            }
            return best;
        }

        /// <summary>A piece lies on its support (its gap to it: m), or it is over open water near the surface (water: the water rule's).</summary>
        public static bool PieceSupported(Transform t, out float gap, out bool water)
        {
            gap = 0f; water = false;
            if (!Lowest(t, out float low, out var b)) return true;                                              // nothing drawn: nothing to rest
            var g = ZGame.I;
            var c = new Vector3(b.center.x, low, b.center.z);
            if (g && OzWaterLanding.OpenWaterAt(c, g.GroundY(c.x, c.z), out float wy) && low <= wy + 0.5f) { water = true; return true; }
            float s = SupportUnder(b, low, t);
            if (s == float.MinValue) return true;                                                               // nothing under it at all: never sent falling for ever
            gap = low - s;
            return gap <= TolM;
        }

        // ------------------------------------------------------------------ pieces (WreckPieces)
        /// <summary>WreckPieces.Update, at the moment it would stop (asleep, its 8 s, or 1.5 s before its end): true = it may stop here. Supported (or
        /// over water, lava, already held): yes. Unsupported: it is woken and simulated on; still unsupported after <see cref="PieceGraceS"/>, it is
        /// brought down (<see cref="OzWreckDrop"/>) from the speed it has and may stop (the drop moves it).</summary>
        public static bool PieceMayFreeze(WreckPieces p, Rigidbody rb, ref float unsupAt)
        {
            if (!On || !Pieces || !p || !rb || rb.isKinematic) return true;
            long t0 = T0();
            try
            {
                var t = p.transform;
                if (OzWaterLanding.InWater(t) || OzLavaMelt.Busy(t)) return true;
                if (PieceSupported(t, out float gap, out bool water) || water) { unsupAt = -1f; return true; }
                if (unsupAt < 0f) { unsupAt = Time.time; PiecesHeldUp++; }
                if (Time.time - unsupAt < PieceGraceS) { if (rb.IsSleeping()) rb.WakeUp(); return false; }
                if (Drop(p, Fall(rb), gap)) PieceDrops++;
                return true;
            }
            finally { T1(t0); }
        }

        /// <summary>WreckPieces.Update, a stopped piece, every <see cref="WatchEveryS"/>: its support gone (a deck, a body it lay on), it falls.
        /// A piece the rock rule holds (it touches a rock) is left to that rule: it wakes it when its rock goes; a piece PhysX simulates again (the
        /// rock rule woke it) and that still moves is PhysX's; one woken and asleep in the air is brought down from the speed it has.</summary>
        public static void PieceWatch(WreckPieces p, ref float at)
        {
            if (!On || !Pieces || !p || p.Fading) return;
            float now = Time.time; if (now < at) return;
            at = now + WatchEveryS * (0.8f + 0.4f * Spread(p.GetInstanceID()));                             // spread over the frames
            long t0 = T0();
            try
            {
                var t = p.transform;
                if (Dropping(t) || OzWaterLanding.InWater(t) || OzLavaMelt.Busy(t)) return;
                var rb = p.GetComponent<Rigidbody>(); if (rb && !rb.isKinematic && !rb.IsSleeping()) return;
                var rock = p.GetComponent<OzRockBody>(); if (rock && (rock.OnRock || rock.TouchCount > 0)) return;
                if (PieceSupported(t, out float gap, out bool water) || water) return;
                if (Drop(p, Fall(rb), gap)) PieceWatchDrops++;
            }
            finally { T1(t0); }
        }

        static float Spread(int id) { unchecked { uint h = (uint)id * 2654435761u; return ((h >> 8) & 1023u) / 1023f; } }

        static float Fall(Rigidbody rb) => rb && !rb.isKinematic ? Mathf.Max(0f, -rb.linearVelocity.y) : 0f;

        /// <summary>WreckPieces.Update, its end-of-life sink may start (after WreckSinkGate let it): false = sink now. A piece still in the air (its
        /// support went since the last watch) is brought down first and sinks after its landing; true while it falls.</summary>
        public static bool SinkWaits(WreckPieces p)
        {
            if (!p) return false;
            if (Dropping(p.transform)) return true;
            if (!On || !Pieces) return false;
            long t0 = T0();
            try
            {
                var t = p.transform;
                if (OzWaterLanding.InWater(t) || OzLavaMelt.Busy(t)) return false;
                var rock = p.GetComponent<OzRockBody>(); if (rock && (rock.OnRock || rock.TouchCount > 0)) return false;
                if (PieceSupported(t, out float gap, out bool water) || water) return false;
                if (!Drop(p, Fall(p.GetComponent<Rigidbody>()), gap)) return false;
                PieceWatchDrops++;
                return true;
            }
            finally { T1(t0); }
        }

        /// <summary>WreckPieces.Retire (the live cap, MaxLive): a piece being brought down sinks after its landing (late = WreckPieces retires it then),
        /// never from the air.</summary>
        public static bool RetireWaits(WreckPieces p, ref bool late)
        {
            if (!p || !Dropping(p.transform)) return false;
            if (!late) RetiresDeferred++;
            late = true;
            return true;
        }

        /// <summary>DebrisAudit's guard: this body is this rule's (a wreck piece it watches, or one it brings down now), so the guard does not wake it
        /// (PhysX's fall and this rule's fall would act on it at once).</summary>
        public static bool Owns(Rigidbody rb)
        {
            if (!rb) return false;
            bool mine = Dropping(rb.transform) || (On && Pieces && rb.GetComponent<WreckPieces>());
            if (mine) AuditSkips++;
            return mine;
        }

        /// <summary>The piece is being brought down now (its end-of-life sink waits for the landing).</summary>
        public static bool Dropping(Transform t) => t && t.TryGetComponent<OzWreckDrop>(out var d) && d && d.enabled;

        static bool Drop(WreckPieces p, float vy, float gap)
        {
            if (!p || Dropping(p.transform)) return false;
            var rb = p.GetComponent<Rigidbody>();
            if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
            foreach (var c in p.GetComponentsInChildren<Collider>()) c.enabled = false;
            p.gameObject.AddComponent<OzWreckDrop>().Begin(p, vy, gap);
            MaxPieceDropM = Mathf.Max(MaxPieceDropM, gap);
            Last = $"wreck piece of #{p.UnitId}: {gap:0.00} m over its support, brought down";
            return true;
        }

        /// <summary>OzWreckDrop's time in the meter.</summary>
        internal static long DropT0() => T0();
        internal static void DropT1(long t0) => T1(t0);

        public static string Status() =>
            $"wreck rest ground {(On ? "on" : "off")} (hulls {(Hulls ? "on" : "off")}, pieces {(Pieces ? "on" : "off")}): hulls watched {HullsWatched}, hull falls {HullFalls} (max {MaxHullFallM:0.00} m), " +
            $"flipped hulls handed over {FlipHandovers}, measures {Measures} (+ {Reused} used again in a straight fall), re-tips at a look {Retips}, edge slides {EdgeSlides} (off an edge into a fall {EdgeFalls}, turns stopped near the support {SpinsStopped}); " +
            $"pieces held up (not stopped in the air) {PiecesHeldUp}, brought down at the stop {PieceDrops}, by the watch {PieceWatchDrops} " +
            $"(max {MaxPieceDropM:0.00} m, fastest {MaxDropSpeed:0.0} m/s), into the water rule {PieceWaterHandovers}, landings {Landings} (clanks {Thumps}), turned onto a face {Flattened}, " +
            $"cap sinks held for a landing {RetiresDeferred}, left out of DebrisAudit's guard {AuditSkips}; cost since the last meter reset: worst frame {MeterWorstMs:0.00} ms, {MeterWorstRays} rays; last: {Last}";
    }

    /// <summary>
    /// 1.5 kit wreck_rest_ground_r6: one wreck piece brought down (View/OzWreckGround.cs). Straight down at 1 g from the speed it had (no glide, no
    /// jump) onto the support under its footprint; a fall longer than OzWreckGround.FlattenFromM turns it onto its nearest face on the way (it lands
    /// lying, no spin); a hard landing on screen: a puff of dust and a metal clank. Over open water the water rule takes it at the surface. Its body
    /// stays kinematic (it was stopped); WreckPieces' sink (at its end or from the live cap) waits for the landing.
    /// </summary>
    [DefaultExecutionOrder(1110)]
    public sealed class OzWreckDrop : MonoBehaviour
    {
        WreckPieces piece; float vy, t, groundT, turnLeft; bool waterTried, flatten, onGround; Quaternion target;

        public void Begin(WreckPieces p, float speed, float gap)
        {
            piece = p; vy = Mathf.Max(0f, speed); t = 0f;
            flatten = gap > OzWreckGround.FlattenFromM;
            if (!flatten) return;
            var tr = transform; Vector3 best = -tr.up; float bd = Vector3.Dot(best, Vector3.down);                  // its nearest face down: of its six box faces the one most down now
            foreach (var ax in new[] { tr.up, tr.right, -tr.right, tr.forward, -tr.forward })
            { float d = Vector3.Dot(ax, Vector3.down); if (d > bd) { bd = d; best = ax; } }
            target = Quaternion.FromToRotation(best, Vector3.down) * tr.rotation;
            turnLeft = Quaternion.Angle(tr.rotation, target);
            if (turnLeft < 1f) flatten = false; else OzWreckGround.Flattened++;
        }

        void LateUpdate()
        {
            long m0 = OzWreckGround.DropT0();
            try { Step(); }
            finally { OzWreckGround.DropT1(m0); }
        }

        void Step()
        {
            if (!piece || piece.Fading) { Destroy(this); return; }                                                 // its sink has it now
            float dt = Time.deltaTime; if (dt <= 0f) return;
            t += dt;
            var tr = transform;
            if (t > 8f || !OzWreckGround.Lowest(tr, out float low, out var b)) { Destroy(this); return; }
            if (flatten && turnLeft > 0.01f)
            {   // the turn onto its face, spread over what is left of the fall (it lands lying)
                float s0 = onGround ? float.MinValue : OzWreckGround.SupportUnder(b, low, tr);
                float left = s0 == float.MinValue ? 0f : Mathf.Max(0f, low - s0);
                float timeLeft = left > 0.01f ? Mathf.Max(0.12f, (-vy + Mathf.Sqrt(vy * vy + 2f * 9.81f * left)) / 9.81f) : 0.12f;
                var c = b.center;
                tr.rotation = Quaternion.RotateTowards(tr.rotation, target, Mathf.Min(turnLeft, turnLeft * dt / timeLeft + 0.01f));
                if (OzWreckGround.Box(tr, out var nb)) tr.position += c - nb.center;                                   // about its middle
                turnLeft = Quaternion.Angle(tr.rotation, target);
                if (!OzWreckGround.Lowest(tr, out low, out b)) { Destroy(this); return; }
            }
            var g = ZGame.I;
            if (!waterTried && g)
            {   // open water under it: the water rule takes it at the surface (splash, rings, its own sinking)
                var cw = new Vector3(b.center.x, low, b.center.z);
                if (OzWaterLanding.OpenWaterAt(cw, g.GroundY(cw.x, cw.z), out float wy) && low <= wy + 0.02f)
                {
                    waterTried = true;
                    if (OzWaterLanding.Enter(tr, "wreck piece", Vector3.down * vy)) { OzWreckGround.PieceWaterHandovers++; Destroy(this); return; }
                }
            }
            float support = OzWreckGround.SupportUnder(b, low, tr);
            float gap = support == float.MinValue ? 0f : low - support;
            if (!onGround)
            {
                if (gap > 1e-3f)
                {   // it falls: 1 g from the speed it had
                    vy += 9.81f * dt;
                    float step = Mathf.Min(gap, vy * dt);
                    tr.position += Vector3.down * step;
                    OzWreckGround.MaxDropSpeed = Mathf.Max(OzWreckGround.MaxDropSpeed, vy);
                    if (step < gap - 1e-3f) return;
                }
                onGround = true; groundT = t; OzWreckGround.Landings++;
                if (vy > 2.5f)
                {   // a hard landing: dust and a metal clank, on screen only
                    var at = new Vector3(b.center.x, support == float.MinValue ? low : support + 0.1f, b.center.z);
                    if (WreckFx.OnScreen(at, 0.1f)) { OzWreckGround.Thumps++; FxManager.I?.Dust(at, Mathf.Clamp(vy / 10f, 0.25f, 0.6f)); AudioDirector.Fire("dst_debris_metal", at, piece.UnitId, Mathf.Clamp(vy / 9f, 0.35f, 0.85f), 1.05f); }
                }
                vy = 0f;
            }
            else if (Mathf.Abs(gap) > 1e-3f) tr.position += Vector3.down * Mathf.Clamp(gap, -3f * dt, 3f * dt);       // on its support while the last of its turn ends
            if (flatten && turnLeft > 0.01f && t - groundT < 0.4f) return;
            Destroy(this);
        }
    }
}
