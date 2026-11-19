// OpenZ (GPL-3.0)
// View only: nothing here touches the rules.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Every fire of a vehicle / cannon wreck, of a crushed or rammed hull, of a wreck piece, of a burst fuel can and of a death in the air is
    /// tied to what is DRAWN of its source at the moment it is made (view only).
    /// <para>The fault: these fires took their place from numbers kept from an earlier moment: the hull's height measured once at the death
    /// (with its turret, label quads and light cones), a box of the whole model built once for the sticky fire, flame spots picked once in the
    /// world, the ground under a point. When the hull was then dented by a Bizon, lost its turret, was shoved on by a big tank, lay flipped,
    /// broke into pieces or was hidden, the fire stayed where the hull WAS: flames and fire glows in the air.</para>
    /// <para>The rule: the source of a fire is the set of solid parts drawn under its root right now (<see cref="Src"/>: each part's own box
    /// in its own frame; no shadow or label quads, no glows, cones or decals). A fire's foot is put on the top of what is drawn under it
    /// (<see cref="Src.Foot"/>), or over on the nearest part when nothing is drawn under it; a ground flame hugs the hull
    /// (<see cref="GroundOutM"/>) and goes with it; a sticky lick sits within <see cref="LickLiftM"/> m of a part; a fire whose kept place is
    /// far from everything drawn (<see cref="ReachM"/>: it was measured with a turret that now lies metres away) goes home, onto the hull
    /// itself; and a fire whose source shows nothing any more (broken into pieces, hidden; the thrown turret is not the hull) goes out: its
    /// last flames fade in under a second, nothing new is made. No fire is made farther than <see cref="NearM"/> m from its drawn source.</para>
    /// <para>A hull is posed by several scripts in one frame (the rules' place first, then the ram / crush / shove on top of it), so a root
    /// that late scripts pose is read at the pose it was DRAWN with on the last frame (kept here every frame, after all of them).</para>
    /// Hooks (one line each): UnitView.Deaths (the wreck's burn and its flame tongues), OpenZPyroFire (the sticky fire of a wreck or of a dented
    /// hull), OzRamImpactLook (the crushed hull's fires), WreckPieces (a burning piece), OzBigTanksFx.Cans (a fuel can's burst), LegacyMatch
    /// (the death's fire burst). Switch <see cref="On"/> (off = every fire exactly where it was before, for the side-by-side check).
    /// </summary>
    [DefaultExecutionOrder(1500)]   // after every script that poses a hull late (OzRamLunge 998 .. OzCrushSquash 1020)
    public sealed class OzFireSource : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>The rule's number: no fire farther than this from what is drawn of its source (m).</summary>
        public const float NearM = 0.3f;
        /// <summary>A ground flame's place out from the hull's nearest drawn part (m, at most); a deck flame's foot under the deck top (m); a
        /// sticky lick's lift off a part (m) and the gap from which it is brought back to it (m); a hull is "in the air" from this height (m).</summary>
        public static float GroundOutM = 0.2f, DeckSinkM = 0.05f, LickLiftM = 0.09f, LickFarM = 0.15f, AirM = 0.5f;
        /// <summary>A fire is brought to the NEAREST drawn part over at most this far (m): the hull's shape changed under it (a dent, a lost turret,
        /// a part torn off). Farther than that it goes home, onto the part at the hull's root (it never jumps to a part lying metres away). A view
        /// whose biggest drawn part is smaller than <see cref="BodyMinM"/> (a lamp left over) is no source.</summary>
        public static float ReachM = 1.5f, BodyMinM = 0.3f;

        public enum Kind { WreckBurn, Tongue, Sticky, Lick, Small, Crush, Piece, Can, Death }
        public static readonly string[] KindNames = { "wreck burn", "flame tongue", "sticky fire, source gone", "sticky lick", "small damage fire", "crush fire", "piece fire", "can burst", "death burst" };
        const int Kinds = 9;

        /// <summary>Counted since the program started: fires put back on their source (by kind), fires put out with their source (by kind), the
        /// farthest a fire was brought back (m), frames a hull was read at its drawn pose in place of the frame's first one.</summary>
        public static readonly int[] MovedBy = new int[Kinds], OutBy = new int[Kinds];
        public static float MaxMovedM;
        public static int Shifts, Calls;

        public static OzFireSource I { get; private set; }
        static readonly Dictionary<int, Src> srcs = new Dictionary<int, Src>();
        static readonly List<int> gone = new List<int>();
        static readonly List<Renderer> listBuf = new List<Renderer>(64);
        float pruneAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            I = null; srcs.Clear(); notes.Clear(); Record = false; On = true; Shifts = Calls = CentresHome = 0; MaxMovedM = 0f;
            for (int i = 0; i < Kinds; i++) MovedBy[i] = OutBy[i] = 0;
            ResetStats();
        }

        static void Make()
        {
            if (I || !Application.isPlaying) return;
            new GameObject("OzFireSource").AddComponent<OzFireSource>();
        }

        void Awake() { if (I && I != this) { Destroy(gameObject); return; } I = this; }
        void OnDestroy() { if (I == this) { I = null; srcs.Clear(); notes.Clear(); } }

        void LateUpdate()
        {
            int f = Time.frameCount; float now = Time.unscaledTime;
            foreach (var s in srcs.Values)
            {
                if (!s.root || !s.late || now - s.usedAt > 2f) continue;
                s.drawnPos = s.root.position; s.drawnRot = s.root.rotation; s.drawnFrame = f;
            }
            if (Record) Judge();
            if (now < pruneAt) return;
            pruneAt = now + 5f; Prune(false);
        }

        static void Prune(bool hard)
        {
            float now = Time.unscaledTime;
            gone.Clear();
            foreach (var kv in srcs) if (!kv.Value.root || now - kv.Value.usedAt > (hard ? 1f : 6f)) gone.Add(kv.Key);
            foreach (var k in gone) srcs.Remove(k);
        }

        /// <summary>The drawn source under a root. late: other scripts pose this root after the rules placed it (a unit's view); false for a
        /// physics piece.</summary>
        public static Src Of(Transform root, bool late)
        {
            if (!root) return null;
            if (!I) Make();
            int id = root.GetInstanceID();
            if (!srcs.TryGetValue(id, out var s))
            {
                if (srcs.Count > 256) Prune(true);
                s = new Src { root = root, late = late }; srcs[id] = s;
            }
            return s;
        }

        static float Ground(float x, float z) => ZGame.I ? ZGame.I.GroundY(x, z) : 0f;
        static void Out(Kind k) { OutBy[(int)k]++; }
        static void Moved(Kind k, Vector3 from, Vector3 to)
        {
            float d = (to - from).magnitude; if (d < 0.02f) return;
            MovedBy[(int)k]++; if (d > MaxMovedM) MaxMovedM = d;
        }

        // ================================================================================================ the drawn source
        /// <summary>The solid parts drawn under one root, read once per frame: each part's box in its own frame (a skinned part: its world box).</summary>
        public sealed class Src
        {
            public Transform root; public bool late; public float usedAt;
            /// <summary>Parts under this transform are not this source (a view's thrown turret: it flies and lies metres away).</summary>
            public Transform skip;
            internal Vector3 drawnPos; internal Quaternion drawnRot = Quaternion.identity; internal int drawnFrame = -1;
            readonly List<Renderer> parts = new List<Renderer>(24);
            Matrix4x4[] l2w = new Matrix4x4[24], w2l = new Matrix4x4[24]; Bounds[] lb = new Bounds[24];
            Matrix4x4 c = Matrix4x4.identity, cInv = Matrix4x4.identity; bool shifted;
            float listAt = -10f; int builtFrame = -1, n, body = -1, home = -2; Rigidbody rb;
            /// <summary>The lowest and the highest drawn point (world y) and the longest side of the biggest drawn part (m), after <see cref="Build"/>.</summary>
            public float MinY, MaxY, BodySize;
            /// <summary>Something of a hull is drawn (not only a lamp-sized leftover).</summary>
            public bool Hull => n > 0 && BodySize >= BodyMinM;
            public int Count => n;

            void List()
            {
                listAt = Time.unscaledTime; parts.Clear();
                root.GetComponentsInChildren(false, listBuf);
                foreach (var r in listBuf)
                {
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                    var nm = r.name;
                    if (nm.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0 || nm.IndexOf("label", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    parts.Add(r);
                }
                listBuf.Clear();
                root.TryGetComponent(out rb);
            }

            static bool Shown(Renderer r, Transform root)
            {
                if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy || !r.transform.IsChildOf(root)) return false;   // a part that left with a piece is no longer this source
                var m = r.sharedMaterial; return !(m && m.renderQueue >= 2450);                                                             // glows, light cones, decals, rings
            }

            /// <summary>Reads the parts for this frame. False: nothing of the source is drawn.</summary>
            public bool Build()
            {
                if (!root) { n = 0; return false; }
                int f = Time.frameCount; usedAt = Time.unscaledTime;
                if (builtFrame == f) return n > 0;
                builtFrame = f;
                if (Time.unscaledTime - listAt > 0.5f) List();                                              // the list of parts twice a second (a part that left or was hidden is seen every frame: Shown)
                shifted = false;
                if (late && drawnFrame == f - 1 && !(rb && !rb.isKinematic))
                {
                    var p = root.position; var q = root.rotation;
                    if ((p - drawnPos).sqrMagnitude > 1e-6f || Quaternion.Angle(q, drawnRot) > 0.05f)
                    {   // posed again later in the frame by another script: where it was DRAWN on the last frame, not where the frame's first writer left it
                        c = Matrix4x4.TRS(drawnPos, drawnRot * Quaternion.Inverse(q), Vector3.one) * Matrix4x4.Translate(-p); cInv = c.inverse; shifted = true; Shifts++;
                    }
                }
                n = 0; body = -1; home = -2; MinY = float.MaxValue; MaxY = float.MinValue; BodySize = 0f; float best = -1f;
                for (int i = 0; i < parts.Count; i++)
                {
                    var r = parts[i];
                    if (!Shown(r, root) || (skip && r.transform.IsChildOf(skip))) continue;
                    if (n == l2w.Length) { System.Array.Resize(ref l2w, n * 2); System.Array.Resize(ref w2l, n * 2); System.Array.Resize(ref lb, n * 2); }
                    Matrix4x4 m; Bounds b;
                    if (r is SkinnedMeshRenderer) { m = Matrix4x4.identity; b = r.bounds; }
                    else { m = r.transform.localToWorldMatrix; b = r.localBounds; }
                    if (shifted) m = c * m;
                    var e = b.extents; var ctr = b.center;
                    if (e.x + e.y + e.z < 1e-4f) continue;
                    l2w[n] = m; w2l[n] = m.inverse; lb[n] = b;
                    for (int k = 0; k < 8; k++)
                    {
                        float wy = m.MultiplyPoint3x4(ctr + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z)).y;
                        if (wy < MinY) MinY = wy; if (wy > MaxY) MaxY = wy;
                    }
                    var ls = m.lossyScale; float vol = Mathf.Abs(e.x * ls.x * e.y * ls.y * e.z * ls.z);
                    if (vol > best) { best = vol; body = n; BodySize = 2f * Mathf.Max(Mathf.Abs(e.x * ls.x), Mathf.Max(Mathf.Abs(e.y * ls.y), Mathf.Abs(e.z * ls.z))); }
                    n++;
                }
                return n > 0;
            }

            /// <summary>A world point of the frame's first pose at the pose the source is drawn with, and back.</summary>
            public Vector3 ToDrawn(Vector3 w) => shifted ? c.MultiplyPoint3x4(w) : w;
            public Vector3 FromDrawn(Vector3 w) => shifted ? cInv.MultiplyPoint3x4(w) : w;

            static bool Slab(float o, float d, float mn, float mx, ref float t0, ref float t1)
            {
                if (Mathf.Abs(d) < 1e-7f) return o >= mn && o <= mx;
                float a = (mn - o) / d, b = (mx - o) / d; if (a > b) { float s = a; a = b; b = s; }
                if (a > t0) t0 = a; if (b < t1) t1 = b;
                return t0 <= t1;
            }

            /// <summary>The top of what is drawn in the column over (x, z). False: nothing of the source is in that column.</summary>
            public bool Top(float x, float z, out float y)
            {
                y = float.MinValue; bool any = false; float y0 = MaxY + 1f;
                var o = new Vector3(x, y0, z);
                for (int i = 0; i < n; i++)
                {
                    var q = w2l[i].MultiplyPoint3x4(o); var d = w2l[i].MultiplyVector(Vector3.down);
                    var mn = lb[i].min; var mx = lb[i].max; float t0 = 0f, t1 = float.MaxValue;
                    if (!Slab(q.x, d.x, mn.x, mx.x, ref t0, ref t1) || !Slab(q.y, d.y, mn.y, mx.y, ref t0, ref t1) || !Slab(q.z, d.z, mn.z, mx.z, ref t0, ref t1)) continue;
                    float hy = y0 - t0; if (hy > y) y = hy; any = true;
                }
                return any;
            }

            /// <summary>The nearest point of what is drawn (m away; a point inside a part: 0).</summary>
            public float Nearest(Vector3 p, out Vector3 on, out int part)
            {
                float best = float.MaxValue; on = p; part = -1;
                for (int i = 0; i < n; i++)
                {
                    var q = w2l[i].MultiplyPoint3x4(p); var mn = lb[i].min; var mx = lb[i].max;
                    var w = l2w[i].MultiplyPoint3x4(new Vector3(Mathf.Clamp(q.x, mn.x, mx.x), Mathf.Clamp(q.y, mn.y, mx.y), Mathf.Clamp(q.z, mn.z, mx.z)));
                    float d = (w - p).sqrMagnitude;
                    if (d < best) { best = d; on = w; part = i; }
                }
                return part < 0 ? float.MaxValue : Mathf.Sqrt(best);
            }
            public float Nearest(Vector3 p, out Vector3 on) => Nearest(p, out on, out _);

            Vector3 Centre(int i) => l2w[i].MultiplyPoint3x4(lb[i].center);
            /// <summary>The middle of the biggest drawn part (its body).</summary>
            public Vector3 BodyCentre => body >= 0 ? Centre(body) : (root ? root.position : Vector3.zero);
            /// <summary>The middle of the home part (the hull itself).</summary>
            public Vector3 HomeCentre { get { int i = Home(); return i >= 0 ? Centre(i) : BodyCentre; } }

            /// <summary>The part that lies at the root (the hull itself; of several the biggest): where a fire goes when its kept place is far from
            /// everything drawn (the kept place was measured with a turret that now lies metres away).</summary>
            int Home()
            {
                if (home != -2) return home;
                home = -1; if (n == 0 || !root) return home;
                var rp = shifted ? drawnPos : root.position; float best = float.MaxValue, bestVol = -1f;
                for (int i = 0; i < n; i++)
                {
                    var q = w2l[i].MultiplyPoint3x4(rp); var mn = lb[i].min; var mx = lb[i].max;
                    var w = l2w[i].MultiplyPoint3x4(new Vector3(Mathf.Clamp(q.x, mn.x, mx.x), Mathf.Clamp(q.y, mn.y, mx.y), Mathf.Clamp(q.z, mn.z, mx.z)));
                    float d = (w - rp).magnitude; var e = lb[i].extents; var ls = l2w[i].lossyScale; float vol = Mathf.Abs(e.x * ls.x * e.y * ls.y * e.z * ls.z);
                    if (d < best - 0.05f || (d < best + 0.05f && vol > bestVol)) { best = d; bestVol = vol; home = i; }
                }
                return home;
            }

            /// <summary>The nearest point of the home part (m away).</summary>
            public float OnHome(Vector3 p, out Vector3 on)
            {
                on = p; int i = Home(); if (i < 0) return float.MaxValue;
                var q = w2l[i].MultiplyPoint3x4(p); var mn = lb[i].min; var mx = lb[i].max;
                on = l2w[i].MultiplyPoint3x4(new Vector3(Mathf.Clamp(q.x, mn.x, mx.x), Mathf.Clamp(q.y, mn.y, mx.y), Mathf.Clamp(q.z, mn.z, mx.z)));
                return (on - p).magnitude;
            }

            /// <summary>The foot of a fire wanted at `want`: on the top of what is drawn in its column; when nothing is drawn there, over on the
            /// nearest part (a little inside its edge); when that is farther than <see cref="ReachM"/> (sideways), on the home part. False:
            /// nothing is drawn.</summary>
            public bool Foot(Vector3 want, out Vector3 foot)
            {
                foot = want;
                if (n == 0) return false;
                if (Top(want.x, want.z, out float y)) { foot = new Vector3(want.x, y, want.z); return true; }
                Nearest(want, out var on, out int part);
                if (new Vector2(on.x - want.x, on.z - want.z).sqrMagnitude > ReachM * ReachM)
                {   // far from everything drawn: home, on the side it was wanted
                    part = Home(); if (part < 0) return false;
                    OnHome(want, out on);
                }
                var cc = Centre(part); var into = new Vector3(cc.x - on.x, 0f, cc.z - on.z); float il = into.magnitude;
                if (il > 1e-4f) on += into * (Mathf.Min(0.1f, il) / il);
                foot = Top(on.x, on.z, out y) ? new Vector3(on.x, y, on.z) : on;
                return true;
            }

            /// <summary>The smaller half width of the drawn part under a point's footprint (m): how far a fire may spread sideways on it.</summary>
            public float HalfAcross(Vector3 p)
            {
                Nearest(p, out _, out int i); if (i < 0) return 0.7f;
                var e = lb[i].extents; var m = l2w[i]; float a = 0f, b = 0f;
                for (int k = 0; k < 3; k++)
                {
                    var col = m.GetColumn(k); float h = new Vector2(col.x, col.z).magnitude * e[k];
                    if (h > a) { b = a; a = h; } else if (h > b) b = h;
                }
                return b;
            }
        }

        // ================================================================================================ the hooks
        /// <summary>The top of a hull a Bizon dented, at a point (world y; a hull it did not dent, or no hull there: +infinity).</summary>
        static float CrushTop(UnitView v, Vector3 at, Src s)
        {
            if (!v || !v.TryGetComponent<OzCrushSquash>(out var sq) || sq.DeformedParts <= 0) return float.MaxValue;
            float pt = sq.PressedTopAt(at); if (pt <= 0.02f) return float.MaxValue;
            return Mathf.Max(s.MinY + 0.15f, Ground(at.x, at.z) + pt);
        }

        /// <summary>UnitView.Deaths: one beat of a burning vehicle / cannon wreck. at = its smoke and light, fireAt = the foot of its fire puffs,
        /// spread = their sideways spread (m). False: nothing of the hull is drawn, no fire.</summary>
        public static bool WreckBurn(UnitView v, bool flip, ref Vector3 at, ref Vector3 fireAt, ref float spread)
        {
            if (!v) return true;
            Calls++;
            if (!On) { Note(Kind.WreckBurn, fireAt, v.transform); return true; }
            var s = Of(v.transform, true); if (s != null) s.skip = v.TossedPiece;
            if (s == null || !s.Build() || !s.Hull) { Out(Kind.WreckBurn); return false; }
            // a flipped wreck lies on its roof or on a side: its root (the hull's old underside) is beside or over what is drawn, so the middle of its body
            if (!s.Foot(flip ? s.BodyCentre : s.ToDrawn(v.transform.position), out var foot)) { Out(Kind.WreckBurn); return false; }
            var raw = fireAt;
            float top = Mathf.Min(foot.y, CrushTop(v, foot, s));
            fireAt = new Vector3(foot.x, flip ? Mathf.Min(top, s.MinY + 0.9f) : top + 0.1f, foot.z);
            at = new Vector3(foot.x, Mathf.Min(s.MinY + (flip ? 0.7f : 1.2f), top + 0.3f), foot.z);
            spread = Mathf.Clamp(s.HalfAcross(foot) * 0.8f, 0.15f, spread);
            Moved(Kind.WreckBurn, raw, fireAt);
            Note(Kind.WreckBurn, fireAt, v.transform);
            return true;
        }

        /// <summary>UnitView.Deaths: one flame spot of a burning wreck, every frame. onHull: on its deck; else on the ground beside it (keep =
        /// the spot in the hull's frame, taken when it is fresh, so it goes with the hull). False: no flame there now.</summary>
        public static bool Tongue(UnitView v, bool onHull, bool fresh, ref Vector3 keep, ref Vector3 root)
        {
            if (!v) return true;
            Calls++;
            if (!On) { Note(Kind.Tongue, root, v.transform); return true; }
            var s = Of(v.transform, true); if (s != null) s.skip = v.TossedPiece;
            if (s == null || !s.Build() || !s.Hull) { Out(Kind.Tongue); return false; }
            var raw = root;
            if (onHull)
            {
                var w = s.ToDrawn(root);
                if (!s.Foot(w, out var foot)) { Out(Kind.Tongue); return false; }
                float top = Mathf.Min(foot.y, CrushTop(v, foot, s));
                root = new Vector3(foot.x, Mathf.Min(w.y, top - DeckSinkM), foot.z);                       // rooted in the hull: the deck cuts the flame's foot, as before
            }
            else
            {
                if (fresh) keep = v.transform.InverseTransformPoint(root);
                var w = s.ToDrawn(v.transform.TransformPoint(keep));
                if (!OpenZPyroFire.DrySpot(w.x, w.z, out var g)) return false;                              // open water: no flame there
                var p = g + Vector3.up * 0.06f;
                float d = s.Nearest(p, out var on);
                var h = new Vector3(p.x - on.x, 0f, p.z - on.z); float hl = h.magnitude;
                if (hl > ReachM)
                {   // far from everything drawn (its place was picked on the box of a hull whose turret now lies metres away): beside the hull itself
                    d = s.OnHome(p, out on); h = new Vector3(p.x - on.x, 0f, p.z - on.z); hl = h.magnitude;
                }
                if (hl > GroundOutM || d > NearM)
                {   // too far out (the hull moved on, lost a part, was dented): right beside the nearest part, or under its edge
                    var q = hl > GroundOutM ? on + h * (GroundOutM / hl) : new Vector3(on.x, p.y, on.z);
                    if (!OpenZPyroFire.DrySpot(q.x, q.z, out g)) return false;
                    p = g + Vector3.up * 0.06f; d = s.Nearest(p, out on);
                    if (d > NearM)
                    {
                        if (!OpenZPyroFire.DrySpot(on.x, on.z, out g)) return false;
                        p = g + Vector3.up * 0.06f; d = s.Nearest(p, out on);
                    }
                }
                if (d > NearM) { Out(Kind.Tongue); return false; }                                         // the hull is not on the ground here (in the air): no ground flame under it
                root = p;
            }
            Moved(Kind.Tongue, raw, root);
            Note(Kind.Tongue, root, v.transform);
            return true;
        }

        /// <summary>A sticky fire this rule covers: on a vehicle's / cannon's own view, when it is a wreck or a hull a Bizon dented (a living,
        /// whole unit's fire is left as it is).</summary>
        static bool Ours(GameObject go, UnitView v, bool soft)
        {
            if (soft || !v || !go || go != v.gameObject || v.Obj == null || (v.Obj.Kind != ZKind.Vehicle && v.Obj.Kind != ZKind.Cannon)) return false;
            return v.Dying || (v.TryGetComponent<OzCrushSquash>(out var sq) && sq.DeformedParts > 0);
        }

        /// <summary>OpenZPyroFire.StepBurns: true = this sticky fire's hull is a wreck that shows nothing any more (it broke into its pieces or
        /// was hidden): the fire is taken off (its last licks fade out).</summary>
        public static bool Gone(GameObject go, UnitView v, bool structure, Vector3 at)
        {
            if (!Ours(go, v, structure) || !v.Dying) return false;
            Calls++;
            var s = Of(v.transform, true); if (s != null) s.skip = v.TossedPiece;
            if (s != null && s.Build() && s.Hull) return false;                                              // something of the hull is drawn (its thrown turret is not the hull)
            if (!On) { Note(Kind.Sticky, at, v.transform, false, true); return false; }
            Out(Kind.Sticky);
            return true;
        }

        /// <summary>OpenZPyroFire.StepBurns: the middle of the sticky fire of a wreck or of a dented hull (its fire light, its dark smoke and its
        /// heat shimmer start there). It was the middle of the model's box as it was built: in the air between the hull and a turret that lies
        /// metres away. Off every drawn part: the middle of the hull itself.</summary>
        public static void Centre(GameObject go, UnitView v, bool soft, ref Vector3 c)
        {
            if (!On || !Ours(go, v, soft)) return;
            var s = Of(v.transform, true); if (s != null) s.skip = v.TossedPiece;
            if (s == null || !s.Build() || !s.Hull) return;
            if (s.Nearest(s.ToDrawn(c), out _) <= LickFarM) return;                                          // in or on a part: as before
            c = s.HomeCentre; CentresHome++;
        }
        /// <summary>Counted: frames a sticky fire's middle was put on the hull.</summary>
        public static int CentresHome;

        /// <summary>OpenZPyroFire.StepBurns: one sticky lick on a rigid hull (p: world, it rides the hull). False: no lick.</summary>
        public static bool Lick(GameObject go, UnitView v, bool soft, ref Vector3 p)
        {
            if (!Ours(go, v, soft)) return true;
            Calls++;
            var root = v.transform;
            if (!On) { Note(Kind.Lick, root.InverseTransformPoint(p), root, true); return true; }
            var s = Of(root, true); if (s != null) s.skip = v.TossedPiece;
            if (s == null || !s.Build() || !s.Hull) { Out(Kind.Lick); return false; }
            var w = s.ToDrawn(p);
            float d = s.Nearest(w, out var on);
            if (d > ReachM) d = s.OnHome(w, out on);                                                          // far from everything drawn: on the hull itself
            float cap = CrushTop(v, w, s);
            if (d > LickFarM || w.y > cap + LickLiftM)
            {   // the model's box has air here (a dent, a lost turret, a gun's length): onto the nearest drawn part
                var raw = w;
                if (d > LickFarM) w = on + (w - on) * (LickLiftM / d);
                if (w.y > cap + LickLiftM) w.y = cap + LickLiftM;
                Moved(Kind.Lick, raw, w);
                p = s.FromDrawn(w);
            }
            Note(Kind.Lick, root.InverseTransformPoint(p), root, true);
            return true;
        }

        /// <summary>OpenZPyroFire.StepBurns: the one small damage fire of a hull (sc: world, it rides the hull). False: the fire goes out.</summary>
        public static bool Small(GameObject go, UnitView v, bool structure, ref Vector3 sc)
        {
            if (!Ours(go, v, structure)) return true;
            Calls++;
            var root = v.transform;
            if (!On) { Note(Kind.Small, root.InverseTransformPoint(sc), root, true); return true; }
            var s = Of(root, true); if (s != null) s.skip = v.TossedPiece;
            if (s == null || !s.Build() || !s.Hull || !s.Foot(s.ToDrawn(sc), out var foot)) { Out(Kind.Small); return false; }
            var w = s.ToDrawn(sc);
            float top = Mathf.Min(foot.y, CrushTop(v, foot, s));
            var nw = new Vector3(foot.x, Mathf.Min(w.y, top + 0.02f), foot.z);
            if ((nw - w).sqrMagnitude > 1e-4f) { Moved(Kind.Small, w, nw); sc = s.FromDrawn(nw); }
            Note(Kind.Small, root.InverseTransformPoint(sc), root, true);
            return true;
        }

        /// <summary>OzRamImpactLook.CrushLook: a fire site on the hull a Bizon is crushing. False: no fire.</summary>
        public static bool Crush(UnitView v, ref Vector3 site)
        {
            if (!v) return true;
            Calls++;
            if (!On) { Note(Kind.Crush, site, v.transform); return true; }
            var s = Of(v.transform, true); if (s != null) s.skip = v.TossedPiece;
            if (s == null || !s.Build() || !s.Hull) { Out(Kind.Crush); return false; }
            var w = s.ToDrawn(site);
            if (!s.Foot(w, out var foot)) { Out(Kind.Crush); return false; }
            float top = Mathf.Min(foot.y, CrushTop(v, foot, s));
            var nw = new Vector3(foot.x, Mathf.Min(w.y, top), foot.z);
            Moved(Kind.Crush, site, nw);
            site = nw;
            Note(Kind.Crush, site, v.transform);
            return true;
        }

        /// <summary>WreckPieces: one beat of a burning wreck piece. at = where its smoke starts (its fire 0.4 m under that, as before).
        /// False: nothing of the piece is drawn, no fire.</summary>
        public static bool Piece(Transform t, out Vector3 at)
        {
            at = t ? t.position + Vector3.up * 0.3f : Vector3.zero;
            if (!t) return false;
            Calls++;
            if (!On) { Note(Kind.Piece, at + Vector3.down * 0.4f, t); return true; }
            var s = Of(t, false);
            if (s == null || !s.Build()) { Out(Kind.Piece); return false; }
            var p = t.position;
            if (s.Nearest(p, out _) > 0.05f)
            {   // the piece's middle lies in a gap between its parts: the fire sits in the top of its biggest part
                var c = s.BodyCentre; var foot = s.Top(c.x, c.z, out float y) ? new Vector3(c.x, y, c.z) : c;
                Moved(Kind.Piece, p, foot);
                at = foot + Vector3.up * 0.3f;
            }
            Note(Kind.Piece, at + Vector3.down * 0.4f, t);
            return true;
        }

        /// <summary>OzBigTanksFx.Cans: a fuel can bursts into its small fire puff where it landed.</summary>
        public static void CanFire(OpenZFxPlayer fx, Transform can, Vector3 p)
        {
            if (!fx) return;
            Calls++;
            if (!On)
            {
                fx.WreckFire(p + Vector3.up * 0.15f, 0.45f);                                                   // as before: its core 0.85 m over the ground under the can
                Note(Kind.Can, new Vector3(p.x, Ground(p.x, p.z) + 0.85f, p.z), can);
                return;
            }
            var foot = new Vector3(p.x, p.y - 0.1f, p.z);
            fx.WreckFireAt(foot, 0.45f, 0.45f);                                                                // sized to the can and standing on it, wherever it lies
            var core = foot + Vector3.up * (0.85f * 0.45f);
            Moved(Kind.Can, new Vector3(p.x, Ground(p.x, p.z) + 0.85f, p.z), core);
            Note(Kind.Can, core, can);
        }

        /// <summary>LegacyMatch.UnitDeath: the fire burst of a vehicle's / cannon's death. It stood 0.85 m over the GROUND under the death
        /// point: a hull that dies in the air (flipped by a ram) or lies low (dented by a crush) had it under or over itself.</summary>
        public static void DeathFire(OpenZFxPlayer z, ZObject o, Vector3 p, float scale)
        {
            if (!z) return;
            Calls++;
            UnitView v = null; var g = ZGame.I;
            if (g && o != null) g.Views.TryGetValue(o.Id, out v);
            var s = v ? Of(v.transform, true) : null;
            float gy = Ground(p.x, p.z);
            var old = new Vector3(p.x, gy + 0.85f, p.z);
            if (s == null || !s.Build()) { z.WreckFire(p, scale); return; }
            if (!On) { z.WreckFire(p, scale); Note(Kind.Death, old, v.transform); return; }
            float hk = Mathf.Clamp((s.MaxY - s.MinY) / 1.1f, 0.45f, 1f);                                     // the core (0.85 m up) must lie in the hull: a hull under 1.1 m gets a lower burst
            var c = s.Top(p.x, p.z, out _) ? p : s.BodyCentre;
            bool same = hk >= 1f && Mathf.Abs(s.MinY - gy) < 0.25f && c == p;
            if (same) { z.WreckFire(p, scale); Note(Kind.Death, old, v.transform); return; }                 // a hull of 1.1 m or more standing on the ground: exactly as before
            var foot = new Vector3(c.x, s.MinY, c.z);
            z.WreckFireAt(foot, scale, hk);
            var core = foot + Vector3.up * (0.85f * hk);
            Moved(Kind.Death, old, core);
            Note(Kind.Death, core, v.transform);
        }

        // ================================================================================================ the counted check's notes
        /// <summary>The check (View/OzFireSourceCheck.cs): while true every fire made through the hooks is noted and, at the end of its frame
        /// (after every script has posed the hulls), measured against what is drawn of its source.</summary>
        public static bool Record;
        struct NoteRow { public Kind kind; public Vector3 p; public Transform src; public int id; public bool local, gone; }
        static readonly List<NoteRow> notes = new List<NoteRow>(256);
        /// <summary>Since <see cref="ResetStats"/>: fires noted, fires farther than <see cref="NearM"/> from their drawn source, the farthest
        /// (m, by kind; 99 = the source shows nothing); the distinct fires (kind + source) that were too far; a few of them in words.</summary>
        public static readonly int[] Seen = new int[Kinds], Far = new int[Kinds];
        public static readonly float[] Worst = new float[Kinds];
        public static readonly HashSet<long> FarFires = new HashSet<long>();
        public static readonly List<string> FarRows = new List<string>();
        static readonly List<Renderer> judgeBuf = new List<Renderer>(64);

        public static void ResetStats()
        {
            for (int i = 0; i < Kinds; i++) { Seen[i] = Far[i] = 0; Worst[i] = 0f; }
            FarFires.Clear(); FarRows.Clear(); notes.Clear();
        }

        static void Note(Kind k, Vector3 p, Transform src, bool local = false, bool gone = false)
        {
            if (!Record) return;
            if (!I) Make();
            notes.Add(new NoteRow { kind = k, p = p, src = src, id = src ? src.GetInstanceID() : 0, local = local, gone = gone });
        }

        /// <summary>The distance from a world point to the solid parts drawn under a root right now (its own reading of the scene, not the
        /// cached one the hooks used). 99: nothing is drawn.</summary>
        public static float Measure(Transform root, Vector3 w)
        {
            if (!root) return 99f;
            float best = 99f * 99f;
            root.GetComponentsInChildren(false, judgeBuf);
            foreach (var r in judgeBuf)
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!r.enabled || r.forceRenderingOff) continue;
                var nm = r.name.ToLowerInvariant(); if (nm.Contains("shadow") || nm.Contains("label")) continue;
                if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450) continue;
                Vector3 on;
                if (r is SkinnedMeshRenderer) on = r.bounds.ClosestPoint(w);
                else
                {
                    var b = r.localBounds; if (b.extents.x + b.extents.y + b.extents.z < 1e-4f) continue;
                    var q = r.transform.InverseTransformPoint(w); var mn = b.min; var mx = b.max;
                    on = r.transform.TransformPoint(new Vector3(Mathf.Clamp(q.x, mn.x, mx.x), Mathf.Clamp(q.y, mn.y, mx.y), Mathf.Clamp(q.z, mn.z, mx.z)));
                }
                float d = (on - w).sqrMagnitude; if (d < best) best = d;
            }
            judgeBuf.Clear();
            return Mathf.Sqrt(best);
        }

        void Judge()
        {
            for (int i = 0; i < notes.Count; i++)
            {
                var nr = notes[i]; int k = (int)nr.kind;
                float d = nr.gone || !nr.src ? 99f : Measure(nr.src, nr.local ? nr.src.TransformPoint(nr.p) : nr.p);
                Seen[k]++;
                if (d > Worst[k]) Worst[k] = d;
                if (d <= NearM + 0.005f) continue;
                Far[k]++;
                if (FarFires.Add(((long)k << 32) ^ (uint)nr.id) && FarRows.Count < 60)
                    FarRows.Add($"{KindNames[k]} of {(nr.src ? nr.src.name : "(destroyed)")}: {(d >= 98f ? "its source shows nothing" : d.ToString("0.00") + " m from its source")} at {Time.time:0.0} s");
            }
            notes.Clear();
        }

        public static string Status()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"fire source tie {(On ? "on" : "off")}: calls {Calls}, hulls read at their drawn pose {Shifts} frames, farthest fire brought back {MaxMovedM:0.00} m, sticky fire middles put on the hull {CentresHome} frames; put back / put out by kind:");
            for (int i = 0; i < Kinds; i++) if (MovedBy[i] + OutBy[i] > 0) sb.Append($" {KindNames[i]} {MovedBy[i]} / {OutBy[i]};");
            return sb.ToString();
        }
    }
}
