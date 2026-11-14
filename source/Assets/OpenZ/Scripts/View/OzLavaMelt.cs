// OpenZ (GPL-3.0)
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>What becomes of the object when it has gone under. Hide: its renderers go off and its own code removes it later. Keep: a piece its caller keeps or
    /// pools: it lies under the sheet at full size, heat off. Off: SetActive(false). Destroy: destroyed here.</summary>
    public enum OzMeltEnd { Hide = 0, Keep = 1, Off = 2, Destroy = 3 }

    /// <summary>
    /// Level 7: anything that lands on lava melts into it, view only. Every debris path hands its piece here at the frame it touches:
    ///   OzWaterLanding   wreck pieces, tank and gun pieces, building parts, shuttle pieces, fort gun pieces, rock chunks, wreck hulls, thrown turrets, robot parts
    ///   FxManager        the rubble chips (stone, metal, wood): pooled, drawn instanced, handled by RubbleStep / RubbleDraw
    ///   DetailKit.Chunks, RollingWheel, OzFuelCan, AnticCan (robot heads, gun cans), OzShuttleCrewFall
    /// A robot's own body keeps UnitView.LavaMelt.
    ///   look      TeamLit parts take the legs' heat through their blocks (_OZHeat, _OZLegHeat, _OZLegLine, _OZLegTop): redder, then the one ramp climbing from the lava line, with bloom.
    ///             A part on any other shader gets a second draw of its mesh with OpenZ/LavaMelt, the same recipe on top of its own look. Nothing is swapped, so nothing pops.
    ///   sinking   OzLavaMeltPlan: it heats, softens and goes down until its top is under the sheet. The sheet writes depth, so it leaves by going under; it never scales away.
    ///             How far is read at the touch from every corner of what it draws, in the pose and at the scale it will have at the end, tilt and flatten counted (EndHeights).
    ///   around it smoke, embers, steam, bubbles (OzLava.PopAt) and rings, on screen only, inside budgets. lava_sizzle_enter at the touch, lava_melt so it ends about when the piece is gone.
    ///   cost      fixed slots, block writes in turn when many melt, no allocation per frame. Past the slots a piece takes the old quick sink of OzWaterLanding (counted: Overflow).
    /// Only exists where OzLava boots, so <see cref="Live"/> is one plain static bool on every other level. Writes no sim state. UnityEngine.Random for FX jitter only.
    /// </summary>
    [DefaultExecutionOrder(240)]
    public sealed class OzLavaMelt : MonoBehaviour
    {
        /// <summary>Plain bool, false on every level with no lava: the one test the shared paths pay.</summary>
        public static bool Live;
        public static OzLavaMelt I => me;
        static OzLavaMelt me;

        public const string MaterialRes = "OpenZ/Lava/M_L07_Melt";
        public const int MaxPieces = 96, MaxPiecesLite = 48, RubbleBatch = 256;
        /// <summary></summary>
        public static float PeakHeat = 0.55f;
        /// <summary>Top of the glow ramp (_OZLegHeat, _MeltT): the temperature of the band from the lava line. The yellow is this: lower it and the band goes from yellow to red. Starting value.</summary>
        public static float PeakGlow = 1f;
        /// <summary>Budgets: puffs a frame, smoke puffs and bubbles a second, one melt sound per gap, Light pulses a second.</summary>
        public static int PuffsPerFrame = 5;
        public static float SmokePerSecond = 26f, BubblesPerSecond = 7f, MeltSoundGap = 0.35f, LightsPerSecond = 0.7f;
        /// <summary>The rubble chips share these: one sizzle and one gloop per gap, so many at once are one sound; small rings a second. A real bone can reach this far past its joint (m).</summary>
        public static float ChipSoundGap = 0.8f, ChipRingsPerSecond = 6f, BonePad = 0.25f;

        /// <summary>Counted by the checks.</summary>
        public static int Taken, Gone, Overflow, Cut, MostLive, Rubble, RubbleUnder, MostRubble, Smokes, Bubbles, Rings, Sizzles, MeltSounds, Lights, HullsTaken, ChipSounds, ChipBubbles, ChipRings, PopsHeld;
        public static float WorstMs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Live = false; me = null; ClearCounts(); }

        public static void ClearCounts()
        {
            Taken = Gone = Overflow = Cut = MostLive = Rubble = RubbleUnder = MostRubble = Smokes = Bubbles = Rings = Sizzles = MeltSounds = Lights = HullsTaken = ChipSounds = ChipBubbles = ChipRings = PopsHeld = 0; WorstMs = 0f;
        }

        sealed class Melt
        {
            public bool alive, seen, soundDone, ringDone, slump, thenDestroy;
            public Transform t; public GameObject go; public UnitView view; public Renderer[] rs;
            public int id, cls, up, hotN, overN;
            public OzMeltEnd end;
            public Renderer[] hot = new Renderer[8]; public int[] slots = new int[8], bare = new int[8]; public bool[] wide = new bool[8];   // bare: a bit for each slot with no block of its own at the touch. wide: it had a renderer block
            public Mesh[] oMesh = new Mesh[4]; public Transform[] oT = new Transform[4]; public int[] oSub = new int[4], oLayer = new int[4];
            public float age, dur, lavaY, top0, endHi, fullHi, under, height, foot, x0, z0, cx, cz, dip, nextSmoke, nextPop, soundAt, puff;
            public System.Action then;
            public Vector3 scale0, last; public Quaternion rot0, rot1;
        }

        sealed class Batch { public Mesh mesh; public int n; public readonly Matrix4x4[] mats = new Matrix4x4[RubbleBatch]; public readonly float[] t = new float[RubbleBatch], top = new float[RubbleBatch]; public MaterialPropertyBlock block; }

        static readonly int HeatId = Shader.PropertyToID("_OZHeat"), LegHeatId = Shader.PropertyToID("_OZLegHeat"), LegLineId = Shader.PropertyToID("_OZLegLine"), LegTopId = Shader.PropertyToID("_OZLegTop"),
            MeltTId = Shader.PropertyToID("_MeltT"), MeltTopId = Shader.PropertyToID("_MeltTop");

        readonly Melt[] melts = new Melt[MaxPieces];
        readonly HashSet<int> busy = new HashSet<int>();
        readonly Batch[] batches = new Batch[6];
        readonly List<Material> mats = new List<Material>(8);
        MaterialPropertyBlock block, soloBlock;
        Material instMat, soloMat;
        ZGame game; OzLava lava; float lavaY0;
        int live, high, rubbleLive, puffsLeft;
        bool lite, calm; float liteDue, smokeBudget, popBudget, lightBudget, chipRingBudget, nextMeltSound, nextChipSound;
        Quaternion reachInv; Vector3 reachPos, reachEnd; float reachRx, reachRy, reachRz;

        public int LiveNow => live;

        /// <summary>From OzLava's boot (OzLava.Melt.cs).</summary>
        public void Setup(ZGame g, OzLava l, float y)
        {
            game = g; lava = l; lavaY0 = y; me = this;
            block = new MaterialPropertyBlock(); soloBlock = new MaterialPropertyBlock();
            for (int i = 0; i < melts.Length; i++) melts[i] = new Melt();
            var src = Resources.Load<Material>(MaterialRes);
            var sh = src ? src.shader : Shader.Find("OpenZ/LavaMelt");
            if (sh)
            {
                instMat = new Material(sh) { name = "OZ_LavaMeltInst", enableInstancing = true, hideFlags = HideFlags.DontSave };
                soloMat = new Material(sh) { name = "OZ_LavaMeltSolo", enableInstancing = false, hideFlags = HideFlags.DontSave };
            }
            else Debug.LogWarning("[OpenZ] lava melt: shader OpenZ/LavaMelt missing, parts off TeamLit melt without their glow");
            Live = true;
            Debug.Log($"[OpenZ] lava melt: on, {MaxPieces} slots, glow draw {(sh ? "ready" : "MISSING")}");
        }

        void OnDestroy()
        {
            if (me == this) { Live = false; me = null; ClearCounts(); }                                     // the counts end with the level: L07 then L02 in one Play session starts clean
            if (instMat) Destroy(instMat); if (soloMat) Destroy(soloMat);
        }

        // ------------------------------------------------------------------ the way in
        /// <summary>This object is melting now (the wreck gate waits for it; the caller knows to leave it alone).</summary>
        public static bool Busy(Transform t) => Live && t && me.busy.Contains(t.GetInstanceID());

        /// <summary>A slot is free now.</summary>
        public static bool HasRoom => Live && me.live < (me.lite ? MaxPiecesLite : MaxPieces);

        /// <summary>PopOut was asked to scale a melting piece away. It waits for the melt: then `done` runs and the piece is destroyed if that was asked. True = held here.</summary>
        public static bool PopWaits(Transform t, System.Action done, bool destroy)
        {
            if (!Busy(t)) return false;
            int id = t.GetInstanceID(); var all = me.melts;
            for (int i = 0; i < me.high; i++)
            {
                var m = all[i]; if (!m.alive || m.id != id) continue;
                if (m.then == null) m.then = done;
                m.thenDestroy |= destroy; PopsHeld++;
                return true;
            }
            return false;
        }

        /// <summary>A piece has reached the lava: from here this class moves it. rs = its renderers, b = its world box, y = the lava. False: not taken (no slot, nothing drawn): the caller's own way.</summary>
        public static bool Take(Transform t, Renderer[] rs, Bounds b, float y, float speed, OzMeltEnd end, string kind) => Live && t && me.Begin(t, rs, b, y, speed, end, kind);

        /// <summary>The same for a caller that has no box to hand.</summary>
        public static bool Take(Transform t, float speed, OzMeltEnd end, string kind)
        {
            if (!Live || !t) return false;
            var rs = t.GetComponentsInChildren<Renderer>();
            bool any = false; var b = new Bounds(t.position, Vector3.zero);
            for (int i = 0; i < rs.Length; i++) { var r = rs[i]; if (!Draws(r, t)) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (!any || !OzLava.SurfaceAt(b.center, out float y)) { if (!OzLava.SurfaceAt(t.position, out y)) return false; }
            return me.Begin(t, rs, b, y, speed, end, kind);
        }

        static bool Draws(Renderer r, Transform root)
        {
            if (!r || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.gameObject.activeInHierarchy || !r.transform.IsChildOf(root)) return false;
            var m = r.sharedMaterial; if (m && m.renderQueue >= 2450) return false;                       // light cones, glows, decals
            var n = r.name; return n.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("label", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        bool Begin(Transform t, Renderer[] rs, Bounds b, float y, float speed, OzMeltEnd end, string kind)
        {
            int id = t.GetInstanceID();
            if (busy.Contains(id)) return true;
            if (live >= (lite ? MaxPiecesLite : MaxPieces)) { Overflow++; return false; }
            Melt m = null; int at = 0;
            for (int i = 0; i < melts.Length; i++) if (!melts[i].alive) { m = melts[i]; at = i; break; }
            if (m == null) { Overflow++; return false; }
            m.hotN = m.overN = 0;
            if (rs != null) for (int i = 0; i < rs.Length; i++) if (Draws(rs[i], t)) Collect(m, rs[i]);
            if (m.hotN == 0 && m.overN == 0) return false;

            bool hull = kind == "wreck hull";
            float side = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            m.cls = hull ? OzLavaMeltPlan.Hull : (int)OzWaterLanding.SizeOf(side);
            uint h = RobotDeathRule.Hash(id, 27, 52u) | 1u;
            m.dur = OzLavaMeltPlan.Seconds(m.cls, h); float u1 = WreckFx.Rnd(ref h), u2 = WreckFx.Rnd(ref h);
            m.t = t; m.go = t.gameObject; m.id = id; m.rs = rs; m.end = end; m.age = 0f; m.lavaY = y; m.seen = true; m.soundDone = m.ringDone = false;
            var p = t.position; m.x0 = p.x; m.z0 = p.z; m.cx = b.center.x; m.cz = b.center.z; m.last = p;
            if (!OzLava.SurfaceAt(b.center, out _)) { m.cx = p.x; m.cz = p.z; }                              // its middle hangs over the bank: the rings and bubbles stay on the lava, at its pivot
            m.then = null; m.thenDestroy = false;
            m.height = Mathf.Max(0.15f, b.size.y); m.foot = Mathf.Clamp(0.5f * Mathf.Max(b.size.x, b.size.z), 0.15f, 3f);
            m.under = 0.12f + 0.12f * m.height;
            m.dip = hull ? 0f : Mathf.Min(0.3f, 0.04f * speed);
            m.scale0 = t.localScale; m.rot0 = m.rot1 = t.rotation; m.slump = !hull;
            float tilt = (hull ? Mathf.Lerp(4f, 8f, u1) : Mathf.Lerp(8f, 16f, u1));                          // it goes in a little on one side, never straight down like a lift
            m.rot1 = Quaternion.AngleAxis(tilt, Quaternion.Euler(0f, u2 * 360f, 0f) * Vector3.right) * m.rot0;
            var r0 = t.rotation; float ax = Mathf.Abs((r0 * Vector3.right).y), ay = Mathf.Abs((r0 * Vector3.up).y), az = Mathf.Abs((r0 * Vector3.forward).y);
            m.up = ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;
            EndHeights(m, rs, b);
            m.top0 = p.y + m.endHi - y;                                                                     // from here, not from the box of the touch: the first frame does not move it
            if (m.top0 < -m.under) m.under = -m.top0;                                                       // already that deep: it stays where it is, it never rises
            m.nextSmoke = 0.1f; m.nextPop = 0.25f + u1 * 0.3f; m.puff = u2 * 6.28f;
            m.soundAt = Mathf.Max(0.25f, m.dur - 3f);                                                       // the gloop runs 2.5-3.4 s: it ends about when the piece is gone
            m.view = null;
            if (t.TryGetComponent<Rigidbody>(out var rb))
            {
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;
            }
            var cols = t.GetComponentsInChildren<Collider>(); for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
            if (hull) { HullsTaken++; if (t.TryGetComponent<UnitView>(out var uv)) { m.view = uv; uv.LavaHullMelts(); } }
            m.alive = true; busy.Add(id); live++; Taken++; if (at >= high) high = at + 1; if (live > MostLive) MostLive = live;

            var surf = new Vector3(m.cx, y, m.cz);
            if (lava && lava.playSounds && AudioDirector.Has(OzLava.SndEnter)) { AudioDirector.Fire(OzLava.SndEnter, surf, -1, m.cls == OzLavaMeltPlan.Small ? 0.6f : 1f); Sizzles++; }
            if (m.cls >= OzLavaMeltPlan.Medium && !lite && !calm && lightBudget >= 1f && FxManager.I)
            {
                lightBudget -= 1f; Lights++;
                FxManager.I.AddLight(surf + Vector3.up * 0.6f, new Color(1f, 0.34f, 0.08f), m.cls == OzLavaMeltPlan.Medium ? 3f : 6f, m.cls == OzLavaMeltPlan.Medium ? 5f : 9f, m.dur * 0.8f, 0.35f);
            }
            return true;
        }

        /// <summary>How high its highest point stands over the pivot at the end: in the pose rot1, at the end scale (endHi) and at its own scale (fullHi). Every corner of every mesh it draws, in that
        /// pose: a box read at the touch leaves out the tilt, which lifts one end of a long piece, and the flatten, which is on one local axis. At the landing only.</summary>
        void EndHeights(Melt m, Renderer[] rs, Bounds b)
        {
            var t = m.t;
            reachInv = Quaternion.Inverse(m.rot0); reachPos = t.position;
            reachRx = (m.rot1 * Vector3.right).y; reachRy = (m.rot1 * Vector3.up).y; reachRz = (m.rot1 * Vector3.forward).y;
            float sy = m.slump ? 1f - OzLavaMeltPlan.Flatten : 1f, sx = m.slump ? 1f + OzLavaMeltPlan.Widen : 1f;
            reachEnd = new Vector3(m.up == 0 ? sy : sx, m.up == 1 ? sy : sx, m.up == 2 ? sy : sx);
            m.endHi = m.fullHi = float.MinValue;
            if (rs != null)
                for (int i = 0; i < rs.Length; i++)
                {
                    var r = rs[i]; if (!Draws(r, t)) continue;
                    if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
                    {
                        var mb = mf.sharedMesh.bounds; var M = mf.transform.localToWorldMatrix;
                        for (int k = 0; k < 8; k++) Reach(m, M.MultiplyPoint3x4(Corner(mb, k)), 0f);
                        continue;
                    }
                    var rb = r.bounds;
                    for (int k = 0; k < 8; k++) Reach(m, Corner(rb, k), 0f);
                    if (r is SkinnedMeshRenderer skin)                                                     // a body: its box lags its pose, its bones do not
                    {
                        var bones = skin.bones;
                        for (int k = 0; k < bones.Length; k++) if (bones[k]) Reach(m, bones[k].position, BonePad);
                    }
                }
            if (m.endHi == float.MinValue) for (int k = 0; k < 8; k++) Reach(m, Corner(b, k), 0f);
        }

        static Vector3 Corner(Bounds b, int k) => b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));

        void Reach(Melt m, Vector3 world, float pad)
        {
            var v = reachInv * (world - reachPos);
            float end = OzLavaMeltPlan.PointY(reachRx, reachRy, reachRz, v.x * reachEnd.x, v.y * reachEnd.y, v.z * reachEnd.z) + pad, full = OzLavaMeltPlan.PointY(reachRx, reachRy, reachRz, v.x, v.y, v.z) + pad;
            if (end > m.endHi) m.endHi = end;
            if (full > m.fullHi) m.fullHi = full;
        }

        void Collect(Melt m, Renderer r)
        {
            r.GetSharedMaterials(mats);
            bool team = false;
            for (int i = 0; i < mats.Count; i++) if (mats[i] && mats[i].HasProperty(LegHeatId)) { team = true; break; }
            if (team)
            {
                if (m.hotN == m.hot.Length) { int n = m.hotN * 2; System.Array.Resize(ref m.hot, n); System.Array.Resize(ref m.slots, n); System.Array.Resize(ref m.bare, n); System.Array.Resize(ref m.wide, n); }
                r.GetPropertyBlock(block); m.wide[m.hotN] = !block.isEmpty;
                int bare = 0;
                for (int i = 0; i < mats.Count && i < 32; i++) { r.GetPropertyBlock(block, i); if (block.isEmpty) bare |= 1 << i; }
                m.hot[m.hotN] = r; m.slots[m.hotN] = mats.Count; m.bare[m.hotN] = bare; m.hotN++;
            }
            if (!r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) return;                       // a skin off TeamLit has no second draw
            int subs = mf.sharedMesh.subMeshCount;
            for (int i = 0; i < mats.Count && i < subs; i++)
            {
                if (mats[i] && mats[i].HasProperty(LegHeatId)) continue;
                if (m.overN == m.oMesh.Length)
                {
                    int n = m.overN * 2;
                    System.Array.Resize(ref m.oMesh, n); System.Array.Resize(ref m.oT, n); System.Array.Resize(ref m.oSub, n); System.Array.Resize(ref m.oLayer, n);
                }
                m.oMesh[m.overN] = mf.sharedMesh; m.oT[m.overN] = r.transform; m.oSub[m.overN] = i; m.oLayer[m.overN] = r.gameObject.layer; m.overN++;
            }
        }

        // ------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            if (!game) return;
            long t0 = Stopwatch.GetTimestamp();
            float dt = game.paused ? 0f : Mathf.Min(Time.deltaTime, 0.05f);
            if (Time.unscaledTime >= liteDue) { liteDue = Time.unscaledTime + 1f; lite = OzLavaFx.Lite; calm = OzLavaFx.Calm; }
            smokeBudget = Mathf.Min(smokeBudget + SmokePerSecond * dt, 8f); popBudget = Mathf.Min(popBudget + BubblesPerSecond * dt, 3f); lightBudget = Mathf.Min(lightBudget + LightsPerSecond * dt, 2f); chipRingBudget = Mathf.Min(chipRingBudget + ChipRingsPerSecond * dt, 3f);
            puffsLeft = PuffsPerFrame;
            int frame = Time.frameCount;
            bool many = live > 24;
            for (int i = 0; i < high; i++)
            {
                var m = melts[i]; if (m.alive) Step(m, i, dt, frame, many);
            }
            while (high > 0 && !melts[high - 1].alive) high--;
            DrawRubble();
            float ms = (Stopwatch.GetTimestamp() - t0) * 1000f / Stopwatch.Frequency;
            if (ms > WorstMs) WorstMs = ms;
        }

        void Step(Melt m, int i, float dt, int frame, bool many)
        {
            if (!m.t || !m.go.activeInHierarchy) { Close(m, true); return; }                                 // its owner took it away
            if (m.end == OzMeltEnd.Keep && (m.t.position - m.last).sqrMagnitude > 0.25f) { Close(m, true, true); return; }   // a pooled piece thrown again
            m.age += dt;
            float k = OzLavaMeltPlan.Clamp01(m.age / m.dur), soft = m.slump ? OzLavaMeltPlan.Soft(k) : 0f, sy = 1f - OzLavaMeltPlan.Flatten * soft, sx = 1f + OzLavaMeltPlan.Widen * soft;
            float y = m.lavaY + OzLavaMeltPlan.TopOver(m.top0, m.under, k) - m.endHi;
            if (m.dip > 0f) y -= m.dip * Mathf.Sin(Mathf.PI * Mathf.Min(1f, m.age / 0.7f));                  // the weight of its landing: it dips and comes back up a little, no jump
            var p = new Vector3(m.x0, y, m.z0);
            m.t.SetPositionAndRotation(p, Quaternion.Slerp(m.rot0, m.rot1, OzLavaMeltPlan.Sink(k)));
            m.last = p;
            if (m.slump) m.t.localScale = new Vector3(m.scale0.x * (m.up == 0 ? sy : sx), m.scale0.y * (m.up == 1 ? sy : sx), m.scale0.z * (m.up == 2 ? sy : sx));
            if (((frame + i) & 7) == 0) m.seen = lava && lava.SeenAt(new Vector3(m.cx, m.lavaY, m.cz));
            float heat = OzLavaMeltPlan.Heat(k), top = Mathf.Max(0.12f, m.height * OzLavaMeltPlan.Band(k) * 1.05f);
            if (m.seen)
            {
                if (!many || ((frame + i) & 1) == 0 || k >= 1f) Write(m, heat * PeakHeat, heat * PeakGlow, top);
                if (m.overN > 0) Solo(m, heat * PeakGlow, top);
                if (dt > 0f) Around(m, k, dt);
            }
            if (k >= 1f) Finish(m);
        }

        /// <summary>The heat goes into the blocks the part already has. A renderer block (the _BaseColor of a robot plate, a concrete chip, a building piece) goes on serving every slot it serves: a slot with no block
        /// of its own gets none then, so nothing can hide that colour. With no renderer block each slot takes a block of its own, as the legs do; Cold takes those away.</summary>
        void Write(Melt m, float red, float heat, float top)
        {
            for (int i = 0; i < m.hotN; i++)
            {
                var r = m.hot[i]; if (!r) continue;
                bool wide = m.wide[i];
                if (wide) { r.GetPropertyBlock(block); Put(red, heat, m.lavaY, top); r.SetPropertyBlock(block); }
                int n = m.slots[i];
                for (int s = 0; s < n; s++)
                {
                    if (wide && Bare(m.bare[i], s)) continue;
                    r.GetPropertyBlock(block, s);
                    Put(red, heat, m.lavaY, top);
                    r.SetPropertyBlock(block, s);
                }
            }
        }

        void Put(float red, float heat, float line, float top) { block.SetFloat(HeatId, red); block.SetFloat(LegHeatId, heat); block.SetFloat(LegLineId, line); block.SetFloat(LegTopId, top); }

        static bool Bare(int mask, int slot) => slot < 32 && ((mask >> slot) & 1) != 0;

        /// <summary>The melt leaves no trace on a part that lives on: a slot block it made goes, and what it wrote into a block that was there goes back to 0, the shader's own values.
        /// Only a block that holds its heat is touched, so a part with a new owner keeps all that owner gave it.</summary>
        void Cold(Melt m)
        {
            for (int i = 0; i < m.hotN; i++)
            {
                var r = m.hot[i]; if (!r) continue;
                r.GetPropertyBlock(block);
                if (block.HasFloat(HeatId)) { Put(0f, 0f, 0f, 0f); r.SetPropertyBlock(block); }
                r.GetSharedMaterials(mats);
                for (int s = 0; s < mats.Count; s++)
                {
                    r.GetPropertyBlock(block, s);
                    if (!block.HasFloat(HeatId)) continue;
                    if (Bare(m.bare[i], s)) r.SetPropertyBlock(null, s);
                    else { Put(0f, 0f, 0f, 0f); r.SetPropertyBlock(block, s); }
                }
            }
        }

        void Solo(Melt m, float heat, float top)
        {
            if (!soloMat) return;
            soloBlock.SetFloat(MeltTId, heat); soloBlock.SetFloat(MeltTopId, top);
            for (int i = 0; i < m.overN; i++)
            {
                var t = m.oT[i]; if (!t) continue;
                Graphics.DrawMesh(m.oMesh[i], t.localToWorldMatrix, soloMat, m.oLayer[i], null, m.oSub[i], soloBlock, ShadowCastingMode.Off, false);
            }
        }

        /// <summary>Smoke, embers, steam, bubbles, the second ring, the melt sound. On screen only (the caller), inside the budgets.</summary>
        void Around(Melt m, float k, float dt)
        {
            var fx = OpenZFxPlayer.I; bool live2 = fx && fx.isActiveAndEnabled;
            if (!m.soundDone && m.age >= m.soundAt && Time.time >= nextMeltSound)
            {
                m.soundDone = true; nextMeltSound = Time.time + MeltSoundGap;
                if (lava && lava.playSounds && AudioDirector.Has(OzLava.SndMelt)) { AudioDirector.Fire(OzLava.SndMelt, new Vector3(m.cx, m.lavaY, m.cz), -1, m.cls == OzLavaMeltPlan.Small ? 0.45f : m.cls == OzLavaMeltPlan.Medium ? 0.7f : 1f); MeltSounds++; }
            }
            if (!live2 || k >= 0.97f) return;
            m.puff += 2.399f;
            var rim = new Vector3(m.cx + Mathf.Cos(m.puff) * m.foot * 0.8f, m.lavaY, m.cz + Mathf.Sin(m.puff) * m.foot * 0.8f);
            if (!OzLava.SurfaceAt(rim, out _)) rim = new Vector3(m.cx, m.lavaY, m.cz);
            if ((m.nextSmoke -= dt) <= 0f && smokeBudget >= 1f && puffsLeft > 0)
            {
                m.nextSmoke = m.cls == OzLavaMeltPlan.Small ? 0.5f : m.cls == OzLavaMeltPlan.Medium ? 0.38f : m.cls == OzLavaMeltPlan.Large ? 0.28f : 0.2f;
                smokeBudget -= 1f; puffsLeft--; Smokes++;
                float s = m.cls == OzLavaMeltPlan.Small ? 0.4f : m.cls == OzLavaMeltPlan.Medium ? 0.65f : m.cls == OzLavaMeltPlan.Large ? 0.95f : 1.3f;
                if (m.cls == OzLavaMeltPlan.Small) fx.LavaSmoke(rim + Vector3.up * 0.1f, s); else fx.LavaLegSmoke(rim + Vector3.up * 0.15f, s);
                if (Random.value < 0.3f) fx.LavaEmber(rim, 1);
                if (Random.value < 0.25f) fx.LavaSteam(rim, s * 0.8f);
            }
            if ((m.nextPop -= dt) <= 0f && popBudget >= 1f)
            {
                m.nextPop = m.cls == OzLavaMeltPlan.Small ? 0.9f : m.cls == OzLavaMeltPlan.Medium ? 0.7f : m.cls == OzLavaMeltPlan.Large ? 0.5f : 0.36f;
                popBudget -= 1f; Bubbles++;
                if (lava) lava.PopAt(new Vector3(m.cx + Mathf.Sin(m.puff) * (m.foot + 0.15f), m.lavaY, m.cz + Mathf.Cos(m.puff) * (m.foot + 0.15f)), false);
            }
            if (!m.ringDone && k >= 0.5f)
            {
                m.ringDone = true; Rings++;
                fx.LavaRing(new Vector3(m.cx, m.lavaY, m.cz), m.foot * 1.5f + 0.4f, 1.3f);
            }
        }

        /// <summary>Its top went under: the last ring, smoke and bubble, then it is gone. It is under the sheet by now: switching it off shows nothing.</summary>
        void Finish(Melt m)
        {
            var at = new Vector3(m.cx, m.lavaY, m.cz);
            Gone++;
            var fx = OpenZFxPlayer.I;
            if (m.seen && fx && fx.isActiveAndEnabled)
            {
                Rings++; fx.LavaRing(at, m.foot * 2f + 0.5f, 1.6f);
                int n = m.cls + 1; for (int i = 0; i < n; i++) { fx.LavaSmoke(at + Vector3.up * (0.15f + i * 0.4f), 0.6f + 0.25f * i); Smokes++; }
                fx.LavaSteam(at, 0.5f + 0.2f * m.cls); fx.LavaEmber(at, 1 + m.cls);
            }
            if (m.seen && lava) { lava.PopAt(at, m.cls >= OzLavaMeltPlan.Large); Bubbles++; }
            FireOut(m.t);
            var view = m.view;
            switch (m.end)
            {
                case OzMeltEnd.Hide:
                    if (m.rs != null) for (int i = 0; i < m.rs.Length; i++) { var r = m.rs[i]; if (r && r.transform.IsChildOf(m.t)) r.enabled = false; }
                    Close(m, false); break;
                case OzMeltEnd.Keep:
                    Close(m, false); break;
                case OzMeltEnd.Off:
                    Close(m, false); break;
                default:
                    var go = m.go; Close(m, false); if (go) Destroy(go); break;
            }
            if (view) view.LavaHullGone();
        }

        /// <summary>Its own fire and smoke end with it, nothing burns on over empty lava. Only here: in water a shuttle or fort piece burns on, as before.</summary>
        static void FireOut(Transform t)
        {
            if (!t) return;
            if (t.TryGetComponent<WreckPieces>(out var wp)) wp.OzDouse();
            if (t.TryGetComponent<OzShuttleBreakup>(out var sb)) sb.OzDouse();
            if (t.TryGetComponent<OzFortGunBreakup>(out var fg)) fg.OzDouse();
            if (t.TryGetComponent<BasePropFlamingDebris>(out var fd)) fd.enabled = false;
        }

        /// <summary>The record ends. cut = its owner took it mid-melt. A piece that lives on leaves cold and at its own scale; a Keep piece stays under the sheet, its top as deep as the melt left it.
        /// thrown = its owner threw it again: it leaves cold, and its new place and scale are its owner's. A pop-out that waited (PopWaits) goes through now.</summary>
        void Close(Melt m, bool cut, bool thrown = false)
        {
            if (cut) Cut++;
            var t = m.t;
            if (t && (cut || m.end == OzMeltEnd.Keep || m.end == OzMeltEnd.Off))
            {
                Cold(m);
                if (m.slump && !thrown) t.localScale = m.scale0;
                if (!cut) { var p = t.position; p.y = m.lavaY - m.under - m.fullHi; t.position = p; }
            }
            if (t && !cut && m.end == OzMeltEnd.Off) t.gameObject.SetActive(false);
            var then = m.then; var doomed = m.thenDestroy ? m.go : null;
            busy.Remove(m.id); m.alive = false; m.t = null; m.go = null; m.view = null; m.rs = null; m.then = null; live--;
            for (int i = 0; i < m.hotN; i++) m.hot[i] = null;
            for (int i = 0; i < m.overN; i++) { m.oT[i] = null; m.oMesh[i] = null; }
            m.hotN = m.overN = 0;
            if (doomed) Destroy(doomed);
            then?.Invoke();
        }

        // ------------------------------------------------------------------ the rubble chips of FxManager
        /// <summary>One rubble slot, each frame on a lava map. size = its radius in m. True = it is melting: FxManager leaves it alone. melt 0 = not yet, then its share of the way.</summary>
        public static bool RubbleStep(ref float melt, ref Vector3 pos, ref Vector3 vel, ref float life, float age, float size, float dt)
        {
            if (melt <= 0f)
            {
                if (vel.y >= 0f || !OzLava.SurfaceAt(pos, out float ly) || pos.y - size > ly) return false;
                if (me.game.GroundY(pos.x, pos.z) > ly - 0.02f) return false;                                // a bank: its own bounce
                float sp = vel.magnitude;
                melt = 0.001f; vel = Vector3.zero; life = age + OzLavaMeltPlan.RubbleSeconds(size) + 0.7f; Rubble++; me.rubbleLive++; if (me.rubbleLive > MostRubble) MostRubble = me.rubbleLive;
                OzWaterLanding.Splash(new Vector3(pos.x, ly, pos.z), OzWaterSize.Small, "rubble chip", sp);
                me.ChipIn(new Vector3(pos.x, ly, pos.z));
                return true;
            }
            if (melt >= 1f) return true;                                                                     // under the sheet: its slot ends by its own life
            float d = OzLavaMeltPlan.RubbleSeconds(size);
            float was = melt;
            melt = Mathf.Min(1f, melt + dt / d);
            if (was < 0.5f && melt >= 0.5f) me.ChipBubble(pos);
            pos.y -= (size * 2.1f + 0.14f) / d * dt;
            if (melt >= 1f) { RubbleUnder++; me.rubbleLive--; }
            else if (me.smokeBudget >= 4f && Random.value < 0.04f)
            {
                var fx = OpenZFxPlayer.I;
                if (fx && fx.isActiveAndEnabled && me.lava && me.lava.SeenAt(pos)) { me.smokeBudget -= 1f; Smokes++; fx.LavaSmoke(pos + Vector3.up * 0.15f, 0.3f); if (Random.value < 0.5f) { fx.LavaEmber(pos, 1); me.popBudget -= 0.3f; } }
            }
            return true;
        }

        /// <summary>A chip at the touch: its sizzle and gloop, shared by all chips (one of each per ChipSoundGap, quiet: many at once are one sound), and a small ring inside its budget.</summary>
        void ChipIn(Vector3 at)
        {
            float now = Time.time;
            if (now >= nextChipSound && lava && lava.playSounds)
            {
                nextChipSound = now + ChipSoundGap;
                if (AudioDirector.Has(OzLava.SndEnter)) { AudioDirector.Fire(OzLava.SndEnter, at, -1, 0.35f); Sizzles++; ChipSounds++; }
                if (AudioDirector.Has(OzLava.SndMelt)) { AudioDirector.Fire(OzLava.SndMelt, at, -1, 0.3f); MeltSounds++; ChipSounds++; }
            }
            var fx = OpenZFxPlayer.I;
            if (chipRingBudget >= 1f && fx && fx.isActiveAndEnabled && lava && lava.SeenAt(at)) { chipRingBudget -= 1f; ChipRings++; Rings++; fx.LavaRing(at, 0.45f, 1.1f); }
        }

        /// <summary>A chip half way down: one bubble inside the budget.</summary>
        void ChipBubble(Vector3 p)
        {
            if (popBudget < 1f || !lava || !OzLava.SurfaceAt(p, out float ly) || !lava.SeenAt(p)) return;
            popBudget -= 1f; ChipBubbles++; Bubbles++;
            lava.PopAt(new Vector3(p.x, ly, p.z), false);
        }

        /// <summary>A small bubble beside a melting robot (UnitView.LavaMelt): on screen only, inside the budget. False = not now.</summary>
        public static bool RobotBubble(Vector3 p)
        {
            if (!Live || me.popBudget < 1f || !me.lava || !me.lava.SeenAt(p)) return false;
            me.popBudget -= 1f; Bubbles++;
            me.lava.PopAt(p, false);
            return true;
        }

        /// <summary>Its glow, drawn over FxManager's own draw of the same chip with the same matrix.</summary>
        public static void RubbleDraw(Mesh mesh, ref Matrix4x4 m, float melt, float size)
        {
            if (!Live || melt >= 1f || !me.instMat) return;
            Batch b = null;
            var all = me.batches;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) { all[i] = b = new Batch { mesh = mesh, block = new MaterialPropertyBlock() }; break; }
                if (ReferenceEquals(all[i].mesh, mesh)) { b = all[i]; break; }
            }
            if (b == null || b.n >= RubbleBatch) return;
            b.mats[b.n] = m; b.t[b.n] = OzLavaMeltPlan.Smooth(0f, 0.4f, melt) * PeakGlow; b.top[b.n] = Mathf.Max(0.1f, size * 2.2f * OzLavaMeltPlan.Smooth(0f, 0.5f, melt)); b.n++;
        }

        void DrawRubble()
        {
            for (int i = 0; i < batches.Length; i++)
            {
                var b = batches[i]; if (b == null || b.n == 0) continue;
                b.block.SetFloatArray(MeltTId, b.t); b.block.SetFloatArray(MeltTopId, b.top);
                Graphics.DrawMeshInstanced(b.mesh, 0, instMat, b.mats, b.n, b.block, ShadowCastingMode.Off, false);
                b.n = 0;
            }
        }

        public static string Status() => $"melt: live {(me ? me.live : 0)} (most {MostLive}), taken {Taken}, gone {Gone}, cut {Cut}, hulls {HullsTaken}, overflow {Overflow}; rubble {Rubble}, under {RubbleUnder}, most {MostRubble}, chip sounds {ChipSounds}, chip bubbles {ChipBubbles}, chip rings {ChipRings}; smoke {Smokes}, bubbles {Bubbles}, rings {Rings}, lights {Lights}; sizzles {Sizzles}, melt sounds {MeltSounds}; pops held {PopsHeld}; worst {WorstMs:F2} ms";
    }
}
