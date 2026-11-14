// OpenZ (GPL-3.0) - version 1.3, level 7 (LV_CONTRACT.md, kit LV3): robots walking in lava. Booted only from OzLava, so only on a map with lava.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A robot in lava, view side only (the rule is Sim/ZWorld.Lava.cs). OpenZWading does nothing here: it wants WAT_ water and water tiles, and
    /// lava is neither. After UnitView has put a robot on the pit bed this lowers it so the lava line stands on its shins (LineShare of its
    /// measured height, so a Tough is right too), eased, ramped across the shore by the real depth, the team colour band lowered with it.
    /// Legs: a view heat of its own (full in HeatUp s, cools in HeatDown s; the sim's own heat follows health), three floats in the body skin's
    /// block, never through TeamPaint.Apply: TeamLit draws the glow climbing from the line, the soot above it and the warm light from below.
    /// Smoke from the legs, steam on each footfall, droplets, a tiny ember, a ring, the crust opening round the foot (OzLavaWake), a thick slow
    /// ring round the legs, and out on the bank a few hot footprints (OzLavaPrints) and drips: for the SmokeCap robots nearest the camera that
    /// are on screen. Rings and discs lift to the lava surface through <see cref="Lift"/>.
    /// Reads the sim, writes nothing to it; own numbers for jitter. Every clock stops with the game's pause. No garbage per frame.
    /// </summary>
    [DefaultExecutionOrder(70)]   // after UnitView (60): UnitView places the robot, this lowers it
    public sealed class OzLavaWade : MonoBehaviour
    {
        public static OzLavaWade I { get; private set; }

        /// <summary>Lava line and glow band as shares of the measured height, the sink's ease, the heat's rise and fall, how long smoke trails after leaving (s).</summary>
        public const float ClimbUp = 8f, ClimbDown = 10f, ClimbShare = 0.12f;   // 1.3 row 17 tail: was 0.42, which lit a long wader to the neck while alive; now the band climbs the legs to the hips and the melt takes it on to the head
        public const float LineShare = 0.16f, BandShare = 0.34f, SinkEase = 0.35f, HeatUp = 1.5f, HeatDown = 8f, Trail = 4.5f, DeadCool = 1f;
        /// <summary>Depth (m) at which the shore ramp starts and how far it runs; how far inside a lake a point counts as lava whatever the walk mesh says.</summary>
        public const float RampFrom = 0.10f, RampDepth = 0.25f, InnerM = 0.9f;
        /// <summary>Robots that get smoke and footfall effects, nearest the camera on screen. Seconds between puffs, between thick rings, a step without foot bones (m).</summary>
        public const int SmokeCap = 24;
        /// <summary>Above this many effect robots: no ring per footfall and thick rings half as often, or they would push the bubbles' rings and scars out of LV2's pool of 48.</summary>
        public const int RingCrowd = 12;
        public const float SmokeEvery = 0.30f, SmokeEveryTrail = 0.55f, RingEvery = 0.8f, StepM = 0.8f, StepGap = 0.22f, FallbackHeight = 2.4f;

        /// <summary>Counted check (OzLavaCheckLV3): robots in lava now, effect robots this frame; the most ever, puffs, footfalls, prints, drips, rings.</summary>
        public int InLavaCount { get; private set; }
        public int NearNow { get; private set; }
        public static int NearMax, SmokePuffs, FootfallsIn, Prints, Drips, Rings;
        public OzLavaWake Wake { get; private set; }
        public OzLavaPrints PrintList { get; private set; }

        sealed class Wader
        {
            public UnitView v; public Transform t, footL, footR; public Renderer[] skins; public int[] slots;   // the skins first, then the body's rigid parts (1.3 row 17 tail)
            public float height, sink, wet, heat, climb, trail, relL, relR, lastStep = -10f, stepDist, smokeT, ringT, feetY, meltLine, meltTop;
            public bool melt, listed, inNow, wasIn, wrote, downL, downR, hasLast; public int printsLeft, dripsLeft, side; public uint rng; public Vector3 lastPos;
        }

        readonly Dictionary<UnitView, Wader> byView = new Dictionary<UnitView, Wader>();
        readonly List<Wader> active = new List<Wader>();
        readonly Wader[] near = new Wader[SmokeCap]; readonly float[] nearKey = new float[SmokeCap];
        static readonly int AccentLoId = Shader.PropertyToID("_AccentLo"), AccentHiId = Shader.PropertyToID("_AccentHi"),
            LegHeatId = Shader.PropertyToID("_OZLegHeat"), LegLineId = Shader.PropertyToID("_OZLegLine"), LegTopId = Shader.PropertyToID("_OZLegTop"),
            HeatInBandId = Shader.PropertyToID("_OZHeatInBand");   // 1.3 row 17 tail: TeamLit keeps the robot's own heat (_OZHeat) inside this band
        MaterialPropertyBlock block;
        ZGame game; ZMapData map; Camera cam; float lavaY;

        void Awake() { I = this; block = new MaterialPropertyBlock(); }
        void OnDestroy() { if (I == this) I = null; Shutdown(); }

        /// <summary>From OzLava.Lv3Boot.</summary>
        public void Setup(ZGame g, ZMapData m, float y, Transform parent)
        {
            game = g; map = m; lavaY = y;
            Wake = new OzLavaWake(m); PrintList = new OzLavaPrints(parent, g);
        }

        /// <summary>From OzLava.Lv3Down, and again harmlessly from OnDestroy: the texture, the mesh and the wake's globals go.</summary>
        public void Shutdown()
        {
            if (Wake != null) { Wake.Dispose(); Wake = null; }
            if (PrintList != null) { PrintList.Dispose(); PrintList = null; }
        }

        /// <summary>Heat of the legs 0..1 of this robot, 0 when it never touched lava.</summary>
        public float LegHeatOf(UnitView v) => v && byView.TryGetValue(v, out var w) ? w.heat : 0f;

        /// <summary>The world height of the top of this robot's glow band as Push last drew it; false when its legs are cold. UnitView.LavaMelt starts its ramp there, so the
        /// band never drops when the robot dies: one that waded long is lit to the chest and the melt goes on from there, one shot dead at once climbs from the shins.</summary>
        public bool BandTop(UnitView v, out float y)
        {
            y = 0f;
            if (!v || !byView.TryGetValue(v, out var w) || w.heat <= 0f) return false;
            float h = w.heat * w.heat * (3f - 2f * w.heat);
            y = w.feetY + LineShare * w.height + BandShare * w.height * (0.4f + 0.6f * h) + ClimbShare * w.height * w.climb;   // Push's line and top, the same sums
            return true;
        }

        /// <summary>UnitView.LavaMelt each frame: a dying robot stays on this list, its ramp from the lava line to this height (m), so the glow climbs from the legs.</summary>
        public void Melting(UnitView v, float line, float top)
        {
            if (!v || v.Obj == null) return;
            if (!byView.TryGetValue(v, out var w)) { w = Bind(v); byView[v] = w; }
            if (!w.listed) { w.listed = true; active.Add(w); }
            w.melt = true; w.meltLine = line; w.meltTop = top;
        }

        /// <summary>The melt is over: no heat and no block value left on the body.</summary>
        public void MeltDone(UnitView v)
        {
            if (!v || !byView.TryGetValue(v, out var w)) return;
            w.melt = false; w.heat = w.climb = 0f; w.wrote = false;
            Floats(w, 0f, 0f, 0f);
        }

        /// <summary>Check: this robot's measured height, how far it is sunk, its lava line's world height.</summary>
        public bool Probe(UnitView v, out float height, out float sink, out float line)
        {
            height = sink = line = 0f;
            if (!v || !byView.TryGetValue(v, out var w)) return false;
            height = w.height; sink = w.sink; line = v.transform.position.y + LineShare * w.height; return true;
        }

        /// <summary>A mark drawn at p (rings, team discs): never under the lava surface; clearance = what the caller adds above it. Nothing on a level with no lava.</summary>
        public static void Lift(ref Vector3 p, float clearance)
        {
            if (OzLava.SurfaceAt(p, out float y) && p.y < y - clearance) p.y = y - clearance;
        }

        // ------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            if (game == null || game.World == null || map == null) return;
            float dt = game.paused ? 0f : Time.deltaTime;
            Gather();
            var fx = OpenZFxPlayer.I; if (fx && !fx.isActiveAndEnabled) fx = null;
            if (!cam) cam = Camera.main;
            var camP = cam ? cam.transform.position : Vector3.zero;
            int inLava = 0, nearN = 0;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var w = active[i];
                if (!Step(w, dt))
                {
                    w.listed = false; active.RemoveAt(i);
                    if (!w.v) byView.Remove(w.v);
                    continue;
                }
                var pos = w.t.position;
                if (w.inNow)
                {
                    inLava++;
                    if (Wake != null && dt > 0f) Wake.Stamp(pos, 1.2f, 0.7f);
                }
                if (fx && (w.inNow || w.trail > 0f || w.printsLeft > 0) && OnScreen(pos))
                    nearN = OzNearestN.Insert(near, nearKey, nearN, w, (pos - camP).sqrMagnitude);
            }
            InLavaCount = inLava; NearNow = nearN; if (nearN > NearMax) NearMax = nearN;
            for (int n = 0; n < nearN; n++) { if (dt > 0f) Effects(near[n], dt, fx); near[n] = null; }
            if (Wake != null) Wake.Tick(dt);
            if (PrintList != null) PrintList.Tick();
        }

        /// <summary>Robots newly on a lava tile join the list. Bones and height are read here, at the first contact, never at first sight.</summary>
        void Gather()
        {
            var views = game.LiveViewValues;
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (!v || !v.isActiveAndEnabled) continue;
                var o = v.Obj; if (o == null || o.Kind != ZKind.Robot) continue;
                var px = ZGame.WorldToPx(v.transform.position);
                if (!map.LavaAt(px.x >> 4, px.y >> 4)) continue;
                if (!byView.TryGetValue(v, out var w)) { w = Bind(v); byView[v] = w; }
                if (!w.listed) { w.listed = true; active.Add(w); }
            }
        }

        Wader Bind(UnitView v)
        {
            var w = new Wader { v = v, t = v.transform };
            foreach (var t in v.GetComponentsInChildren<Transform>(true))
            {   // mixamorigLeftFoot / mixamorig:LeftFoot / LeftFoot; a rig with neither walks on the stride fallback in Feet
                if (!w.footL && t.name.EndsWith("LeftFoot")) w.footL = t;
                if (!w.footR && t.name.EndsWith("RightFoot")) w.footR = t;
            }
            var skins = v.GetComponentsInChildren<SkinnedMeshRenderer>();
            w.skins = WithParts(v, skins);                                                   // 1.3 row 17 tail: the body's rigid parts take the band too
            w.slots = new int[w.skins.Length];
            for (int i = 0; i < w.skins.Length; i++) w.slots[i] = Mathf.Max(1, w.skins[i].sharedMaterials.Length);
            float top = float.NegativeInfinity;
            foreach (var s in skins) if (s.enabled) top = Mathf.Max(top, s.bounds.max.y);   // the height from the skins, as before
            if (float.IsInfinity(top))
                foreach (var r in v.GetComponentsInChildren<Renderer>())
                    if (r.enabled && !(r is ParticleSystemRenderer) && !(r is LineRenderer) && !(r is TrailRenderer)) top = Mathf.Max(top, r.bounds.max.y);
            float h = top - w.t.position.y;
            w.height = float.IsInfinity(h) || float.IsNaN(h) ? FallbackHeight : Mathf.Clamp(h, 1.2f, 4.5f);
            w.rng = ((uint)v.Obj.Id * 2654435761u) | 1u;
            float seed = Rand(w);
            w.ringT = seed * RingEvery; w.smokeT = seed * SmokeEvery;
            return w;
        }

        /// <summary>1.3 row 17 tail: the skins first (the first carries the team band), then every rigid part of this robot's own body that TeamLit draws (BS-01 kit:
        /// helmet, pack, hip plate, the gun; EvilZod's cloak). TeamPaint.Heat writes its glow on them as on the skins, so without the band's soot and line they glowed
        /// whole and pale while the skins glowed from the legs. A figure riding on it keeps its own paint (TeamPaint's rule). Once per robot, at its first contact.</summary>
        static Renderer[] WithParts(UnitView v, SkinnedMeshRenderer[] skins)
        {
            var list = new List<Renderer>(skins);
            var paint = v.GetComponentInChildren<TeamPaint>(true);
            if (paint)
                foreach (var r in v.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.GetComponentInParent<TeamPaint>(true) != paint) continue;
                    foreach (var m in r.sharedMaterials) if (m && m.HasProperty(LegHeatId)) { list.Add(r); break; }
                }
            return list.ToArray();
        }

        /// <summary>One robot: the sink, the heat, the block. False = it is done with lava (or gone): it leaves the list.</summary>
        bool Step(Wader w, float dt)
        {
            var v = w.v;
            if (!v || !v.isActiveAndEnabled || v.Obj == null) return false;
            if (w.melt && v.LavaMelting)
            {   // the melt moves the body and sets the ramp's top; the heat comes up quickly if it was cold, a body thrown in from the bank
                w.inNow = false; w.trail = 0f; w.printsLeft = 0;
                if (dt > 0f) w.heat = Mathf.MoveTowards(w.heat, 1f, dt / 0.35f);
                Floats(w, w.heat * w.heat * (3f - 2f * w.heat), w.meltLine, w.meltTop);
                w.wrote = true;
                return true;
            }
            w.melt = false;
            if (v.Dying || v.Obj.Destroyed)
            {   // UnitView stops placing it (Sync returns on Destroyed): it stays as sunk; any more would run on. The hot line no longer follows it, so a body
                // that falls on the bank would light up whole inside the frozen band: its legs go out quickly (a melting body has its own heat by then)
                w.inNow = false; w.trail = 0f; w.printsLeft = 0;
                if (w.heat <= 0f) return false;
                if (dt > 0f) w.heat = Mathf.MoveTowards(w.heat, 0f, dt / DeadCool);
                Push(w, LineShare * w.height, false);
                return true;
            }
            var t = w.t; var p = t.position;                                                 // UnitView: ground + yOffset
            var px = ZGame.WorldToPx(p);
            bool tile = map.LavaAt(px.x >> 4, px.y >> 4);
            float d = lavaY - p.y, target = 0f;                                              // lava above the feet on the bed; the pit is shallow at the shore
            if (tile) { target = Mathf.Clamp01((d + RampFrom) / RampDepth); if (target < 1f && Inner(p)) target = 1f; }
            float k = dt > 0f ? 1f - Mathf.Exp(-dt * 3f / SinkEase) : 0f;                    // about 95 % in SinkEase
            w.wet += (target - w.wet) * k;
            float L = LineShare * w.height;
            w.sink += (target * Mathf.Max(0f, L - d) - w.sink) * k;
            bool sunk = w.sink > 0.001f;
            if (sunk) t.position = p - Vector3.up * w.sink;
            w.feetY = t.position.y;
            w.inNow = w.wet > 0.5f;
            if (w.inNow) v.AnticResetIdle();                                                 // no idle antic while it stands in lava
            if (dt > 0f)
            {
                w.heat = Mathf.MoveTowards(w.heat, w.inNow ? 1f : 0f, dt / (w.inNow ? HeatUp : HeatDown));
                w.climb = Mathf.MoveTowards(w.climb, w.inNow ? 1f : 0f, dt / (w.inNow ? ClimbUp : ClimbDown));   // the red goes up the leg the longer it stays in, and comes down again after
                w.trail = w.inNow ? Trail : Mathf.Max(0f, w.trail - dt);
            }
            if (sunk || w.heat > 0f || w.wrote) Push(w, L, sunk);
            if (!w.inNow && w.trail <= 0f) w.printsLeft = 0;                                 // prints only in the first steps out
            return tile || sunk || w.wet > 0.005f || w.heat > 0f || w.trail > 0f || w.printsLeft > 0;
        }

        /// <summary>Well inside a lake, where a walk mesh without a pit (a test scene) must not leave a robot standing on the sheet.</summary>
        bool Inner(Vector3 p) => At(p.x + InnerM, p.z) && At(p.x - InnerM, p.z) && At(p.x, p.z + InnerM) && At(p.x, p.z - InnerM);
        bool At(float x, float z) { var px = ZGame.WorldToPx(new Vector3(x, 0f, z)); return map.LavaAt(px.x >> 4, px.y >> 4); }

        /// <summary>After UnitView's band: the band lowered with the body, the three leg floats, all in the one block read.</summary>
        void Push(Wader w, float L, bool sunk)
        {
            float h = w.heat * w.heat * (3f - 2f * w.heat), line = w.feetY + L, top = BandShare * w.height * (0.4f + 0.6f * h) + ClimbShare * w.height * w.climb;
            for (int s = 0; s < w.skins.Length; s++)
            {
                var skin = w.skins[s]; if (!skin) continue;
                for (int m = 0; m < w.slots[s]; m++)
                {
                    skin.GetPropertyBlock(block, m);
                    if (s == 0 && m == 0 && sunk)
                    {
                        float lo = block.GetFloat(AccentLoId), hi = block.GetFloat(AccentHiId);
                        if (hi > lo) { block.SetFloat(AccentLoId, lo - w.sink); block.SetFloat(AccentHiId, hi - w.sink); }
                    }
                    block.SetFloat(LegHeatId, h); block.SetFloat(LegLineId, line); block.SetFloat(LegTopId, top); block.SetFloat(HeatInBandId, 1f);
                    skin.SetPropertyBlock(block, m);
                }
            }
            w.wrote = w.heat > 0f;
        }

        void Floats(Wader w, float h, float line, float top)
        {
            for (int s = 0; s < w.skins.Length; s++)
            {
                var skin = w.skins[s]; if (!skin) continue;
                for (int m = 0; m < w.slots[s]; m++)
                {
                    skin.GetPropertyBlock(block, m);
                    block.SetFloat(LegHeatId, h); block.SetFloat(LegLineId, line); block.SetFloat(LegTopId, top); block.SetFloat(HeatInBandId, 1f);
                    skin.SetPropertyBlock(block, m);
                }
            }
        }

        // ------------------------------------------------------------------ what shows (nearest robots on screen only)
        void Effects(Wader w, float dt, OpenZFxPlayer fx)
        {
            var t = w.t; var pos = t.position; var surf = new Vector3(pos.x, lavaY, pos.z);
            float size = w.height / FallbackHeight;
            var move = w.hasLast ? pos - w.lastPos : Vector3.zero; move.y = 0f; w.lastPos = pos; w.hasLast = true;
            if (w.inNow && !w.wasIn)
            {   // stepping in
                fx.LavaDroplets(surf, 0.55f * size, 8); fx.LavaSteam(surf, 0.7f * size); fx.LavaRing(surf, 1.1f * size, 1.1f); Rings++;
                if (Wake != null) Wake.Stamp(surf, 1.8f, 1f);
            }
            if (!w.inNow && w.wasIn) { w.printsLeft = 4 + (int)(Rand(w) * 3f); w.dripsLeft = 1 + (int)(Rand(w) * 2f); }
            w.wasIn = w.inNow;

            w.smokeT -= dt;
            if (w.smokeT <= 0f && (w.inNow || w.trail > 0f))
            {   // thin dark smoke from each leg in turn, thicker as it heats; a fading trail after it left
                float thick = w.inNow ? Mathf.Lerp(0.45f, 1f, w.heat) : w.trail / Trail;
                w.smokeT = (w.inNow ? SmokeEvery : SmokeEveryTrail) * (0.8f + 0.4f * Rand(w));
                w.side = 1 - w.side;
                var leg = w.side == 0 ? w.footL : w.footR;
                var at = leg ? leg.position : pos + t.right * ((w.side * 2 - 1) * 0.25f * size);
                at.y = Mathf.Max(at.y, w.inNow ? lavaY : pos.y) + 0.25f * size;
                fx.LavaLegSmoke(at, 1.7f * thick * size); SmokePuffs++;
            }
            if (w.inNow)
            {
                w.ringT -= dt;
                if (w.ringT <= 0f) { w.ringT = NearNow > RingCrowd ? RingEvery * 2f : RingEvery; fx.LavaRing(surf, 1.25f * size, 1.6f); Rings++; }
            }
            if (w.v.Obj.IsMoving && (w.inNow || w.printsLeft > 0)) Feet(w, fx, move);
        }

        void Feet(Wader w, OpenZFxPlayer fx, Vector3 move)
        {
            var t = w.t;
            if (w.footL && w.footR)
            {   // a footfall = the foot's lowest point against the root (it stops going down and starts lifting), as OpenZWading
                float rl = w.footL.position.y - t.position.y, rr = w.footR.position.y - t.position.y;
                bool dl = rl < w.relL - 1e-4f, dr = rr < w.relR - 1e-4f;
                if (w.downL && !dl) Fall(w, fx, w.footL.position, move);
                if (w.downR && !dr) Fall(w, fx, w.footR.position, move);
                w.downL = dl; w.downR = dr; w.relL = rl; w.relR = rr;
                return;
            }
            float size = w.height / FallbackHeight;
            w.stepDist += move.magnitude;
            if (w.stepDist < StepM * size) return;
            w.stepDist = 0f; w.side = 1 - w.side;
            var dir = move.sqrMagnitude > 1e-6f ? move.normalized : t.forward;
            Fall(w, fx, t.position + Vector3.Cross(Vector3.up, dir) * ((w.side * 2 - 1) * 0.25f * size), move);
        }

        void Fall(Wader w, OpenZFxPlayer fx, Vector3 foot, Vector3 move)
        {
            float now = Time.time;
            if (now - w.lastStep < StepGap) return;
            w.lastStep = now;
            float size = w.height / FallbackHeight;
            if (w.inNow)
            {
                var at = new Vector3(foot.x, lavaY, foot.z);
                fx.LavaSteam(at, 0.55f * size);                                             // white steam on every footfall
                fx.LavaLegSmoke(at + Vector3.up * 0.3f, 1.6f * size);
                fx.LavaDroplets(at, 0.35f * size, 2 + (int)(Rand(w) * 3f));
                fx.LavaEmber(at, 1);
                if (NearNow <= RingCrowd) { fx.LavaRing(at, 0.7f * size, 0.7f); Rings++; }
                if (Wake != null) Wake.Stamp(at, 1f, 1f);                                    // the crust opens round the foot
                FootfallsIn++;
                return;
            }
            if (w.printsLeft <= 0) return;
            w.printsLeft--;
            if (PrintList != null && PrintList.Add(foot, move.sqrMagnitude > 1e-6f ? move.normalized : w.t.forward, size, Rand(w))) Prints++;
            fx.LavaSteam(foot + Vector3.up * 0.1f, 0.22f * size);
            if (w.dripsLeft > 0) { w.dripsLeft--; fx.LavaDroplets(foot + Vector3.up * 0.3f * size, 0.12f * size, 1); Drips++; }
        }

        bool OnScreen(Vector3 p)
        {
            if (!cam) return false;
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.z < 160f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        /// <summary>Own numbers, never the sim's and never Unity's: 0 .. 1.</summary>
        static float Rand(Wader w)
        {
            uint x = w.rng; x ^= x << 13; x ^= x >> 17; x ^= x << 5; w.rng = x;
            return (x & 0xFFFFFF) / 16777216f;
        }
    }
}
