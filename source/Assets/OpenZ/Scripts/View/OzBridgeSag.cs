// OpenZ (GPL-3.0)
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.5 notes N17 (VIEW ONLY: no rule reads it, nothing is written to the sim): the steel bridges (L02 desert, L17 city: every walk deck
    /// ZGame.MakeWalkable built) give under a MAMMOTH or a BIZON, only where the tank is:
    /// <list type="bullet">
    /// <item>a soft bowl of sag under the tank (a smooth bell along the deck, about the tank's length wide), that follows it along the bridge;
    /// full in the free span over the water (desert: between the stone abutments; city: between the piers), a third of it over the abutments /
    /// piers, nothing at the bridge ends, the stone / concrete never moves;</item>
    /// <item>the give is a damped spring (about 1.8 swings a second): the weight coming on presses it down past its rest and it rocks back, a
    /// small rumble while the tank rolls, and when the tank leaves the span springs back up with a damped settle;</item>
    /// <item>whatever stands on that part of the deck follows the deck's height (ZGame.GroundY takes the same dip off the walk deck through
    /// OzBridgeWobble.DipAt), so the tank and the units on the span go down with it; the dark deck underlay goes down with the deepest point;</item>
    /// <item>sounds through the sound table (in view only, takes without a repeat): the steel groans when the weight comes on
    /// (dst_structure_groan), creaks while it rolls (dst_wreck_creak about every 1.2 s) and once as it springs back.</item>
    /// </list>
    /// Sag at rest: Mammoth <see cref="SagMammoth"/>, Bizon <see cref="SagBizon"/> (at most <see cref="MaxSag"/> with the swing). Other vehicles
    /// leave the steel bridges still, as before. L16's plank bridge has its own give for every vehicle (OzBridgeWobble; the big tanks a little
    /// heavier there since this kit), so this script leaves L16 alone. Switch <see cref="On"/> (off = as before; the meshes go back at once).
    /// </summary>
    public sealed class OzBridgeSag : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>The sag at rest under each big tank (m), the most the deck ever goes down with the swing (m) or up past rest (m).</summary>
        public static float SagMammoth = 0.10f, SagBizon = 0.09f, MaxSag = 0.16f, MaxUp = 0.035f;
        /// <summary>The spring: swings a second and damping ratio; the push past rest when the weight comes on and the lift when it goes (x the sag
        /// a second); the bell's half width (1/e) as a share of the tank's drawn half length.</summary>
        public static float WobbleHz = 1.8f, Damping = 0.16f, KickOn = 2.4f, KickOff = 1.3f, SpreadK = 0.9f;
        /// <summary>The rumble while it rolls (m at 3 m/s, swings a second).</summary>
        public static float RumbleM = 0.007f, RumbleHz = 3.2f;
        /// <summary>Over the abutments / piers: this share of the free span's sag; the bridge ends (m) where it fades to nothing.</summary>
        public static float AbutK = 0.33f, EndFadeM = 1.2f;

        /// <summary>Proof: a big tank on a steel deck this frame, how many, the deepest point (m), the last tank type, the sounds sent.</summary>
        public static bool Active => inst && inst.anyLoad;
        public static int Loads, Groans, Creaks; public static float LastPeak, MaxPeak; public static string LastType = "";
        static OzBridgeSag inst;

        sealed class Part { public MeshFilter mf; public Mesh mesh; public Vector3[] basePos, work; public float[] s, w; public Vector3 downLocal; }
        sealed class Load { public int id; public float s, amp, vel, A, W, half, speed, creakAt; public bool here, seen; public Vector3 pos; public string type; }
        sealed class Deck
        {
            public Transform walk, art, under; public Vector3 underBase; public bool alongZ; public float c, mid, halfFull, flatHalf, spanHalf, halfWidth, deckY;
            public readonly List<Part> parts = new List<Part>(); public readonly Dictionary<int, Load> loads = new Dictionary<int, Load>();
            public readonly float[] grid = new float[GridN]; public float g0, gStep, peak; public bool moved, live;
        }
        const int GridN = 128;

        readonly List<Deck> decks = new List<Deck>();
        readonly List<Transform> found = new List<Transform>();
        readonly List<int> gone = new List<int>();
        float nextScan; bool anyLoad; ZGame game;

        // ------------------------------------------------------------------ boot (level scenes: a ZGame is there)
        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Early()
        {
            Loads = Groans = Creaks = 0; LastPeak = MaxPeak = 0f; LastType = "";
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryAttach(); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => TryAttach();
        public static void TryAttach()
        {
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g || FindFirstObjectByType<OzBridgeSag>()) return;
            new GameObject("OzBridgeSag").AddComponent<OzBridgeSag>();
        }

        void OnEnable() { inst = this; }
        void OnDisable() { if (inst == this) inst = null; RestoreAll(); anyLoad = false; }

        // ------------------------------------------------------------------ the decks (every 1.5 s, like OzBridgeWobble)
        void Scan()
        {
            found.Clear();
            if (game.level != "L16") OzViewScans.WalkDecks(found);                                       // L16: the plank bridge's own give (OzBridgeWobble)
            bool same = found.Count == decks.Count;
            if (same) for (int i = 0; i < found.Count; i++) if (decks[i].walk != found[i]) { same = false; break; }
            if (same) return;
            RestoreAll(); decks.Clear();
            foreach (var w in found)
            {
                var mc = w ? w.GetComponent<MeshCollider>() : null; if (!mc) continue;
                var b = mc.bounds;
                var d = new Deck { walk = w, art = w.parent, alongZ = b.size.z >= b.size.x, deckY = b.max.y };
                d.c = d.alongZ ? b.center.z : b.center.x; d.mid = d.alongZ ? b.center.x : b.center.z;
                d.halfFull = d.alongZ ? b.extents.z : b.extents.x; d.halfWidth = d.alongZ ? b.extents.x : b.extents.z;
                float ramp = d.art && (d.art.name.Contains("desert") || game.level == "L02") ? 1.1f : 2f;          // ZGame.MakeWalkable's ramps
                d.flatHalf = Mathf.Max(1f, d.halfFull - ramp);
                d.spanHalf = d.flatHalf * OzBridgeKit.FreeSpan(game.level, d.flatHalf);                                  // the free span: city piers 3.7 of 6 m, desert abutments 1.83 of 4.45 m
                d.g0 = -d.halfFull; d.gStep = 2f * d.halfFull / (GridN - 1);
                if (!d.art) continue;
                foreach (var mf in d.art.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (mf.transform == w || !mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
                    bool still = mf.sharedMesh.name.IndexOf("stone", System.StringComparison.OrdinalIgnoreCase) >= 0
                              || mf.name.IndexOf("stone", System.StringComparison.OrdinalIgnoreCase) >= 0;          // abutments, wing walls, piers: never move
                    if (still) continue;
                    var p = new Part { mf = mf, mesh = mf.mesh };                                                // its own copy: the prefab mesh stays as it is
                    p.mesh.MarkDynamic(); p.basePos = p.mesh.vertices; int n = p.basePos.Length;
                    p.work = new Vector3[n]; p.s = new float[n]; p.w = new float[n];
                    var tr = mf.transform;
                    for (int i = 0; i < n; i++)
                    {
                        var wp = tr.TransformPoint(p.basePos[i]);
                        p.s[i] = (d.alongZ ? wp.z : wp.x) - d.c;
                        p.w[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(d.deckY - 1.9f, d.deckY - 1.0f, wp.y));    // what hangs deep stays (a ruin's rubble in the water)
                    }
                    p.downLocal = tr.InverseTransformVector(Vector3.down);
                    var mb = p.mesh.bounds; mb.Expand(MaxSag * 2f * Mathf.Max(1f, p.downLocal.magnitude)); p.mesh.bounds = mb;   // the culling box once (no RecalculateBounds each frame)
                    d.parts.Add(p);
                }
                var host = d.art.parent;
                d.under = host ? host.Find("underlay_" + d.art.name) : null;
                if (d.under) d.underBase = d.under.position;
                decks.Add(d);
            }
        }

        void RestoreAll() { foreach (var d in decks) Restore(d); }

        void Restore(Deck d)
        {
            if (!d.moved) return;
            foreach (var p in d.parts) if (p.mesh && p.mf && p.mf.sharedMesh == p.mesh) p.mesh.vertices = p.basePos;
            if (d.under) d.under.position = d.underBase;
            for (int i = 0; i < GridN; i++) d.grid[i] = 0f;
            d.moved = false; d.peak = 0f;
        }

        /// <summary>The share of the free span's sag the deck takes at s (0 at the ends, AbutK over the abutments / piers, 1 over the water).</summary>
        float Support(Deck d, float s)
        {
            float a = Mathf.Abs(s);
            float end = Mathf.Clamp01((d.flatHalf - a) / EndFadeM);
            if (end <= 0f) return 0f;
            float span = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(d.spanHalf - 0.4f, d.spanHalf + 0.8f, a));
            return end * (AbutK + (1f - AbutK) * span);
        }

        // ------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            if (!game) { game = ZGame.I; if (!game) return; }
            if (!On) { if (anyLoad || decks.Count > 0) { RestoreAll(); anyLoad = false; } return; }
            if (game.World == null) return;
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1.5f; Scan(); }
            if (decks.Count == 0) { anyLoad = false; return; }
            float dt = game.paused ? 0f : Time.deltaTime;
            foreach (var d in decks) foreach (var L in d.loads.Values) L.here = false;
            var objs = game.World.Objects;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o.Kind != ZKind.Vehicle || o.Destroyed || !ZBigTanks.Is(o.Type)) continue;
                if (!game.Views.TryGetValue(o.Id, out var v) || !v) continue;
                var p = v.transform.position;
                ZObject.RamDrawnBox(o.Type, out int fr, out int re, out _);
                float half = fr > 0 ? (fr + re) / 16f : 3.3f;
                foreach (var d in decks)
                {
                    if (!d.walk || !d.walk.gameObject.activeInHierarchy) continue;
                    float s = (d.alongZ ? p.z : p.x) - d.c, a = (d.alongZ ? p.x : p.z) - d.mid;
                    if (Mathf.Abs(s) > d.flatHalf + half || Mathf.Abs(a) > d.halfWidth + 1.5f) continue;
                    if (!d.loads.TryGetValue(o.Id, out var L)) { L = new Load { id = o.Id, s = s }; d.loads[o.Id] = L; }
                    L.speed = dt > 0f && L.seen ? Mathf.Abs(s - L.s) / dt : 0f;
                    L.s = s; L.here = true; L.half = half; L.type = o.Type; L.pos = p;
                    L.A = o.Type == ZBigTanks.Mammoth ? SagMammoth : SagBizon;
                    L.W = Mathf.Max(1.2f, half * SpreadK);
                    break;
                }
            }
            anyLoad = false; Loads = 0;
            foreach (var d in decks)
            {
                gone.Clear();
                bool live = false;
                foreach (var L in d.loads.Values)
                {
                    float cover = L.here ? Mathf.Clamp01((d.flatHalf + L.half - Mathf.Abs(L.s)) / (2f * L.half)) : 0f;
                    float target = L.A * Support(d, L.s) * Mathf.SmoothStep(0f, 1f, cover);
                    if (L.here && !L.seen) { L.vel += L.A * KickOn; L.seen = true; Groan(L, d); }                 // the weight comes on
                    else if (!L.here && L.seen) { L.vel -= L.A * KickOff; L.seen = false; Creak(L, 0.45f, "it leaves the span"); }   // it goes: the span springs back
                    Spring(L, target, dt);
                    if (L.here) { Loads++; if (dt > 0f && L.speed > 0.4f && Time.time >= L.creakAt) { L.creakAt = Time.time + Random.Range(0.9f, 1.6f); Creak(L, Mathf.Lerp(0.45f, 0.7f, Mathf.Clamp01(L.speed / 4f)), "it rolls on the span"); } }
                    bool still = !L.here && Mathf.Abs(L.amp) < 0.0004f && Mathf.Abs(L.vel) < 0.002f;
                    if (still) gone.Add(L.id); else live = true;
                }
                foreach (var id in gone) d.loads.Remove(id);
                if (!live) { Restore(d); d.live = false; continue; }
                d.live = true; anyLoad = true;
                Field(d);
                Bend(d);
            }
        }

        void Spring(Load L, float target, float dt)
        {
            if (dt <= 0f) return;
            float w = 2f * Mathf.PI * WobbleHz;
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 240f), 1, 8); float h = dt / n;
            for (int k = 0; k < n; k++)
            {
                L.vel += (-w * w * (L.amp - target) - 2f * Damping * w * L.vel) * h;
                L.amp += L.vel * h;
            }
        }

        /// <summary>The deck's sag along its length this frame (the grid DipAt and the meshes read).</summary>
        void Field(Deck d)
        {
            float t = Time.time, peak = 0f;
            for (int i = 0; i < GridN; i++)
            {
                float s = d.g0 + i * d.gStep, sum = 0f, sup = Support(d, s);
                if (sup > 0f)
                    foreach (var L in d.loads.Values)
                    {
                        float amp = L.amp + (L.here ? RumbleM * Mathf.Clamp01(L.speed / 3f) * Mathf.Sin(2f * Mathf.PI * RumbleHz * t + L.id * 1.7f) : 0f);
                        float k = (s - L.s) / L.W;
                        if (k * k < 9f) sum += amp * Mathf.Exp(-k * k);
                    }
                float v = Mathf.Clamp(sum * sup, -MaxUp, MaxSag);
                d.grid[i] = v; if (v > peak) peak = v;
            }
            d.peak = peak; LastPeak = peak; if (peak > MaxPeak) MaxPeak = peak;
            foreach (var L in d.loads.Values) if (L.here) LastType = L.type;
        }

        float Sample(Deck d, float s)
        {
            float f = (s - d.g0) / d.gStep;
            if (f <= 0f || f >= GridN - 1) return 0f;
            int i = (int)f; float u = f - i;
            return d.grid[i] + (d.grid[i + 1] - d.grid[i]) * u;
        }

        void Bend(Deck d)
        {
            for (int pi = d.parts.Count - 1; pi >= 0; pi--)
            {
                var p = d.parts[pi];
                if (!p.mf || !p.mesh || p.mf.sharedMesh != p.mesh || !p.mf.transform.IsChildOf(d.art)) { d.parts.RemoveAt(pi); continue; }   // a broken-off railing piece (OzBridgeRails) flies on its own
                var bp = p.basePos; var wk = p.work;
                for (int i = 0; i < bp.Length; i++)
                {
                    float dip = p.w[i] > 0f ? Sample(d, p.s[i]) * p.w[i] : 0f;
                    wk[i] = bp[i] + p.downLocal * dip;
                }
                p.mesh.vertices = wk;
            }
            if (d.under) d.under.position = d.underBase + Vector3.down * Mathf.Max(0f, d.peak);   // the dark underlay stays inside the sagging deck slab
            d.moved = true;
        }

        void Groan(Load L, Deck d)
        {
            if (!AudioDirector.Has("dst_structure_groan")) return;
            AudioDirector.Fire("dst_structure_groan", L.pos, L.id, L.type == ZBigTanks.Mammoth ? 0.7f : 0.62f, Random.Range(0.78f, 0.9f)); Groans++;
            OzSoundLog.Add("BRIDGE", "dst_structure_groan", null, "SENT World (a big tank's weight comes onto the steel span)", 0.65f, L.pos);
        }

        void Creak(Load L, float vol, string why)
        {
            if (!AudioDirector.Has("dst_wreck_creak")) return;
            AudioDirector.Fire("dst_wreck_creak", L.pos, L.id, vol, Random.Range(0.8f, 0.95f)); Creaks++;
            OzSoundLog.Add("BRIDGE", "dst_wreck_creak", null, "SENT World (" + why + ")", vol, L.pos);
        }

        /// <summary>For OzBridgeWobble.DipAt -> ZGame.GroundY: how far the walk deck under a big tank is down at this point right now (0 elsewhere).</summary>
        public static float DipAt(Collider hit, float x, float z)
        {
            if (!On || inst == null || !inst.anyLoad || hit == null) return 0f;
            var ht = hit.transform;
            var ds = inst.decks;
            for (int i = 0; i < ds.Count; i++)
            {
                var d = ds[i];
                if (d.walk != ht) continue;
                return d.live ? inst.Sample(d, (d.alongZ ? z : x) - d.c) : 0f;
            }
            return 0f;
        }

        public static string Status() =>
            $"bridge sag {(On ? "on" : "off")}: big tanks on a steel span {Loads}, deepest now {LastPeak:0.000} m (max {MaxPeak:0.000} m, {LastType}), groans {Groans}, creaks {Creaks}";
    }
}
