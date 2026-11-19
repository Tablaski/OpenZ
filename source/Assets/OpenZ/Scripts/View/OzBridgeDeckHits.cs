using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The view half of
    /// DV210 (the sim half, the aim point on the deck: Sim/ZObject.BridgeDeckAim.cs). View only: the sim's hits, damage, times and random
    /// stream are untouched.
    /// <para>
    /// target's footprint box on the target's surface facing the shooter: the line from the shooter (1.2 m up) through the aim point (1 m up)
    /// against the target's drawn meshes, else against the footprint box (inset 12 %, for a bridge 1.2 m tall). Right for a wall; a bridge is a
    /// flat deck: that line runs about 1 m over the deck and first meets the bridge's near-end posts or the box's near face, so a shell the sim
    /// put on the deck was drawn back at the entrance, toward the shooter.</para>
    /// <para>Now (<see cref="Flat"/>, <see cref="FlatShot"/>) a shell, rocket or grenade at a standing bridge flies to and bursts at the sim's own
    /// point, on the deck (ZGame.GroundY there is the deck's collider), as a shot at open ground does. The MLRS's cosmetic ripple rockets 2-8
    /// (View/MlrsSalvo.cs: a 2.5-4.5 m disc round the real impact, stretched x1.3 along the line of fire) never land nearer to the shooter than
    /// <see cref="MinDeckM"/> past the bridge's near end (<see cref="KeepOnDeck"/>): a rocket that would fall short of that line lands as far past
    /// it as it fell short (x <see cref="ReflectK"/>), so the ripple keeps its spread and its look; its sideways spread is kept (a rocket may still
    /// splash into the water beside the deck). The Tough's, the mech's and the missile cannon's twins land within 0.9-1.1 m of the real impact,
    /// 2 m or more onto the deck: unchanged.</para>
    /// <para>Own units (<see cref="OwnBlastSkips"/>): HitReactions.OnBlast (View/UnitView.Deaths.cs) throws shrapnel chips (metal bits, a spark
    /// burst on the blast's side, the shrapnel sound) at every unit inside a blast's radius and makes a robot flinch, with no team test, though
    /// the sim never hurts the shooter's own team (Sim/ZWorld.cs ProcessMissileDamage: no friendly fire). With the hits on the deck the MLRS's
    /// 10 m splash (its hits 6-11 m from it) still reached the MLRS itself and every own unit at the entrance on most salvos, the Tough's 5 m one
    /// a squad member 4.4 m away: sparks and chips on the attackers from their own shots, the "own legs" look. Now a blast of a shot at a bridge
    /// skips the shooter's own team, the sim's own filter (the one View/MlrsSalvo.Marked.cs QueueMarked uses). Enemy and neutral units in that
    /// splash react as before (the sim hurts them); every blast at anything but a bridge is unchanged (a rule for every blast in the game is
    /// </para>
    /// <para><see cref="On"/> false = the view before this kit (all three parts).</para>
    /// <para>The Play check (menu OpenZ/Checks/V1.5/Bridge aim: hits drawn on bridges (Play), Editor/OpenZParity.V15BridgeAim.cs) reads the
    /// last <see cref="RingSize"/> hits noted here: how far past the near end each one was drawn, how far from its shooter, and (with
    /// <see cref="Probe"/> on: menu 'Bridge aim: clear the drawn hits (Play)') how high over the bridge's DRAWN deck surface straight under it
    /// (<see cref="DeckUnder"/>, the drawn meshes, not the walk collider ZGame.GroundY reads), and every shot at a bridge that ShotLine cut short
    /// (<see cref="CutsSeen"/>: View/ShotLine.cs stops a direct shell or rocket where a bank or deck edge rises LowBankM 1.2 m over its flight).
    /// The probe and the watch run only while the check has them on (off in normal play).</para>
    /// </summary>
    public static class OzBridgeDeckHits
    {
        /// <summary>THE SWITCH (view only): false = every shot at a bridge drawn as before this kit.</summary>
        public static bool On = true;

        /// <summary>The nearest a cosmetic ripple rocket lands past the bridge's near end (m), and how much of a shortfall it is put past that line.</summary>
        public const float MinDeckM = 1.5f, ReflectK = 0.6f;

        /// <summary>A cosmetic rocket put back stays this far short of the bridge's far end (m).</summary>
        public const float FarMarginM = 0.5f;

        /// <summary>The deck probe looks for the drawn deck surface up to this far over the burst (m): a railing's top (about 1 m over the
        /// deck), a lamp arm or a sign gantry over the deck is not the deck.</summary>
        public const float SearchUpM = 0.9f;

        /// <summary>Counts for the checks: blasts drawn on a bridge deck by this rule, ripple rockets put back onto the deck, own units a blast
        /// at a bridge did not chip / flinch.</summary>
        public static int DeckBlasts, RippleMoved, RippleSeen, OwnSkipped;

        /// <summary>The Play check's probe and cut watch (off in normal play): see <see cref="Watch"/>.</summary>
        public static bool Probe { get; private set; }

        /// <summary>One hit noted for the Play check: real = a sim blast (else a cosmetic ripple rocket). deck: 0 = not probed, 1 = measured
        /// (overDeckM = the burst's height over the drawn deck surface straight under it), 2 = the bridge's readable meshes stand there but no
        /// drawn surface covers the point (the burst is beside the drawn deck), 3 = nothing readable to test (judge by eye).</summary>
        public struct Hit { public int bridge, shooter; public bool real, moved; public byte deck; public float depthM, lengthM, acrossM, fromShooterM, overDeckM, at; }

        /// <summary>One shot at a bridge that ShotLine cut short: the cut point's depth past the bridge's near end (m, under 0 = before it).</summary>
        public struct CutShot { public int bridge, shooter; public float depthM, lengthM, fromShooterM, at; }

        public const int RingSize = 128;
        static readonly Hit[] ring = new Hit[RingSize];
        static int ringN;
        static readonly List<CutShot> cuts = new List<CutShot>();

        /// <summary>The hits noted so far (oldest first, at most RingSize).</summary>
        public static Hit[] Recent()
        {
            int n = Mathf.Min(ringN, RingSize); var r = new Hit[n];
            for (int i = 0; i < n; i++) r[i] = ring[(ringN - n + i) % RingSize];
            return r;
        }

        /// <summary>The shots at a bridge ShotLine cut short since the last clear (at most RingSize).</summary>
        public static CutShot[] CutsSeen() => cuts.ToArray();

        public static void Clear() { ringN = 0; DeckBlasts = RippleMoved = RippleSeen = OwnSkipped = 0; cuts.Clear(); }

        /// <summary>The Play check: on = the deck probe on every real blast noted and the watch on ShotLine's cuts (View/ShotLine.cs CutSeen,
        /// the checks' ear). Off = neither (normal play).</summary>
        public static void Watch(bool on)
        {
            Probe = on;
            ShotLine.CutSeen -= OnCut;
            if (on) ShotLine.CutSeen += OnCut;
        }

        /// <summary>True = t is a standing bridge and the view half is on.</summary>
        public static bool Flat(ZObject t) => On && t != null && t.IsBridge && !t.Destroyed;

        static int shotAt = -1, shotBy = -1, shotFrame = -1;

        /// <summary>CombatView MissileFired: true = t is a standing bridge: the shot flies to the sim's point on its deck (no bug #13 surface
        /// pull). Remembers the shot for the cut watch (ShotLine.Cut runs right after it in MissileFired).</summary>
        public static bool FlatShot(ZObject t, ZObject shooter)
        {
            bool f = Flat(t);
            shotAt = f ? t.Id : -1; shotBy = f && shooter != null ? shooter.Id : -1; shotFrame = Time.frameCount;
            return f;
        }

        /// <summary>CombatView MissileExploded: true = b is a standing bridge: the blast bursts at the sim's point on its deck (no bug #13 surface
        /// pull); the hit is noted for the Play check.</summary>
        public static bool OnDeck(ZObject b, ZObject shooter, Vector3 at)
        {
            if (!Flat(b)) return false;
            DeckBlasts++;
            Note(b, shooter, at, true, false);
            return true;
        }

        /// <summary>HitReactions.OnBlast (View/UnitView.Deaths.cs), for unit o inside the blast's radius: true = no shrapnel chips and no flinch
        /// on o: the blast is a shot at a bridge (standing, or felled by this very blast) and o is on its shooter's team, which the sim never
        /// hurts (Sim/ZWorld.cs ProcessMissileDamage; View/MlrsSalvo.Marked.cs QueueMarked uses the same test). A neutral shooter, an enemy or
        /// neutral unit in the splash and every blast at anything but a bridge: false (as before).</summary>
        public static bool OwnBlastSkips(ZEvent e, ZObject attacker, ZObject o)
        {
            if (!On || attacker == null || o == null || attacker.Owner == Team.Neutral || o.Owner != attacker.Owner) return false;
            var g = ZGame.I; var t = g && g.World != null ? g.World.GetAny(e.otherId) : null;
            if (t == null || !t.IsBridge) return false;
            OwnSkipped++;
            return true;
        }

        /// <summary>MlrsSalvo.Queue: cosmetic ripple rocket p of a salvo at target t from a shooter drawn at shooterPos: on a standing bridge
        /// never nearer to the shooter than MinDeckM past the near end (a shooter on the deck: MinDeckM past itself).</summary>
        public static Vector3 KeepOnDeck(ZObject t, Vector3 shooterPos, Vector3 p)
        {
            if (!Flat(t) || !Frame(t, out var e0, out var u, out float len, out _)) return p;
            float ds = Vector3.Dot(Flat0(shooterPos) - e0, u);
            bool fromStart = ds <= len * 0.5f;
            float dShooter = fromStart ? ds : len - ds;                                              // the shooter's depth past its near end (< 0: before it)
            float d = fromStart ? Vector3.Dot(Flat0(p) - e0, u) : len - Vector3.Dot(Flat0(p) - e0, u);
            float min = Mathf.Max(MinDeckM, dShooter + MinDeckM), max = len - FarMarginM;
            RippleSeen++;
            bool moved = false;
            if (d < min && min < max)
            {
                float nd = Mathf.Min(max, min + (min - d) * ReflectK);
                p += u * ((fromStart ? 1f : -1f) * (nd - d));
                RippleMoved++; moved = true;
            }
            Note(t, null, p, false, moved, shooterPos);
            return p;
        }

        static Vector3 Flat0(Vector3 v) { v.y = 0f; return v; }

        /// <summary>The bridge's centre line in the world: e0 = the middle of its north / west end, u = along the deck toward the other end,
        /// len = its length (m), side = across the deck.</summary>
        static bool Frame(ZObject b, out Vector3 e0, out Vector3 u, out float len, out Vector3 side)
        {
            bool vert = b.Type == "bridge_vert";
            int x0 = vert ? b.LocX + (b.WidthPix >> 1) : b.LocX, y0 = vert ? b.LocY : b.LocY + (b.HeightPix >> 1);
            int x1 = vert ? x0 : b.LocX + b.WidthPix, y1 = vert ? b.LocY + b.HeightPix : y0;
            e0 = Flat0(ZGame.PxToWorld(x0, y0)); var e1 = Flat0(ZGame.PxToWorld(x1, y1));
            var d = e1 - e0; len = d.magnitude;
            if (len < 0.5f) { u = Vector3.forward; side = Vector3.right; return false; }
            u = d / len; side = new Vector3(u.z, 0f, -u.x);
            return true;
        }

        /// <summary>The depth of point p past the bridge's end nearer to the shooter at sp (m), that end picked as the sim picks it.</summary>
        static float Depth(Vector3 e0, Vector3 u, float len, Vector3 sp, Vector3 p)
        {
            float ds = Vector3.Dot(Flat0(sp) - e0, u), da = Vector3.Dot(Flat0(p) - e0, u);
            return ds <= len * 0.5f ? da : len - da;
        }

        static void Note(ZObject b, ZObject shooter, Vector3 at, bool real, bool moved, Vector3? shooterPos = null)
        {
            if (!Frame(b, out var e0, out var u, out float len, out var side)) return;
            var g = ZGame.I;
            Vector3 sp = shooterPos ?? (shooter != null && g ? g.CenterWorld(shooter) : at);
            byte deck = 0; float over = float.NaN;
            if (real && Probe) { deck = DeckUnder(b, at, out float dy); if (deck == 1) over = at.y - dy; }
            var h = new Hit
            {
                bridge = b.Id, shooter = shooter != null ? shooter.Id : -1, real = real, moved = moved, deck = deck,
                depthM = Depth(e0, u, len, sp, at), lengthM = len, acrossM = Vector3.Dot(Flat0(at) - e0, side),
                fromShooterM = Vector3.Distance(Flat0(at), Flat0(sp)), overDeckM = over, at = Time.time,
            };
            ring[ringN % RingSize] = h; ringN++;
        }

        /// <summary>ShotLine.CutSeen (only while <see cref="Watch"/> is on): a direct shell or rocket was cut short at p; noted when it is the
        /// shot at a bridge MissileFired drew this frame.</summary>
        static void OnCut(Vector3 p)
        {
            var g = ZGame.I;
            if (shotAt < 0 || shotFrame != Time.frameCount || !g || g.World == null || cuts.Count >= RingSize) return;
            var b = g.World.GetAny(shotAt); var s = shotBy >= 0 ? g.World.GetAny(shotBy) : null;
            if (b == null || !Frame(b, out var e0, out var u, out float len, out _)) return;
            var sp = s != null ? g.CenterWorld(s) : p;
            cuts.Add(new CutShot { bridge = b.Id, shooter = shotBy, depthM = Depth(e0, u, len, sp, p), lengthM = len, fromShooterM = Vector3.Distance(Flat0(p), Flat0(sp)), at = Time.time });
        }

        // ------------------------------------------------------------------ the deck probe (the Play check only)
        sealed class Tris { public Vector3[] v; public int[] t; }
        static readonly Dictionary<Mesh, Tris> meshes = new Dictionary<Mesh, Tris>();
        static readonly List<MeshRenderer> rends = new List<MeshRenderer>(64);

        /// <summary>The highest surface of bridge b's DRAWN meshes straight under (or up to <see cref="SearchUpM"/> over) the burst at `at`, facing
        /// up or down (any winding); walls and the sides of posts do not count. y = its height. 1 = found, 2 = readable meshes of the bridge stand
        /// there but none covers the point (the burst is beside the drawn deck), 3 = nothing to test (no drawn body, or its meshes there have no
        /// CPU copy: judge by eye). The opaque meshes only, as HitSurface (glows, rings, decals and fx left out).</summary>
        static byte DeckUnder(ZObject b, Vector3 at, out float y)
        {
            y = float.NaN;
            var g = ZGame.I; Transform root = null;
            if (g && g.Views.TryGetValue(b.Id, out var v) && v) root = v.transform;
            else if (g && g.StaticViews.TryGetValue(b.Id, out var go) && go && go.activeInHierarchy) root = go.transform;
            if (!root) return 3;
            bool any = false; float top = float.NegativeInfinity, lim = at.y + SearchUpM;
            rends.Clear(); root.GetComponentsInChildren(false, rends);
            foreach (var r in rends)
            {
                if (!r.enabled) continue;
                var mat = r.sharedMaterial; if (!mat || mat.renderQueue >= 2450) continue;
                var bb = r.bounds;
                if (at.x < bb.min.x || at.x > bb.max.x || at.z < bb.min.z || at.z > bb.max.z || bb.min.y > lim) continue;
                var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null; if (!mesh) continue;
                if (!meshes.TryGetValue(mesh, out var d)) { d = mesh.isReadable ? new Tris { v = mesh.vertices, t = mesh.triangles } : null; meshes[mesh] = d; }
                if (d == null) continue;
                any = true;
                var m = r.transform.localToWorldMatrix; var tv = d.v; var tt = d.t;
                for (int i = 0; i + 2 < tt.Length; i += 3)
                {
                    var a = m.MultiplyPoint3x4(tv[tt[i]]); var c1 = m.MultiplyPoint3x4(tv[tt[i + 1]]); var c2 = m.MultiplyPoint3x4(tv[tt[i + 2]]);
                    if (!OverTri(a, c1, c2, at.x, at.z, out float hy) || hy > lim || hy <= top) continue;
                    var n = Vector3.Cross(c1 - a, c2 - a); if (Mathf.Abs(n.y) < 0.3f * n.magnitude) continue;   // walls, the sides of posts
                    top = hy;
                }
            }
            if (!any) return 3;
            if (float.IsNegativeInfinity(top)) return 2;
            y = top; return 1;
        }

        /// <summary>(x, z) inside triangle a b c seen from above: h = the triangle's height there.</summary>
        static bool OverTri(Vector3 a, Vector3 b, Vector3 c, float x, float z, out float h)
        {
            h = 0f;
            float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (den > -1e-9f && den < 1e-9f) return false;
            float l1 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / den, l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / den, l3 = 1f - l1 - l2;
            if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) return false;
            h = l1 * a.y + l2 * b.y + l3 * c.y;
            return true;
        }
    }
}
