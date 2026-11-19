using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The rammed
    /// vehicle is TURNED OVER by the Mammoth (view only; the rules hold it, Sim/ZObject.RamPolish.cs: the Mammoth drives on under it for the
    /// lift, the heave at the tipping point, the kill ZObject.RamFlipTicks later). Replaces OzRamPush + OzRamFlip (switch <see cref="On"/>).
    /// <list type="bullet">
    /// <item>The target is a rigid body in the vertical plane along the ram: its drawn outline (the convex hull of its hull + turret mesh
    /// points, barrels left out) rotating about its FAR bottom edge (the far track on the ground), gravity on its centre of mass.</item>
    /// <item>The Mammoth's drawn front (its low wedge: the hull's front reach by height, measured at the contact) drives into it every frame
    /// from the Mammoth's drawn position: the outline is turned exactly as far as it must be so that the wedge and the hull never overlap
    /// (no gap, no float: the near side rests on the wedge while the far track pivots), grinding sparks where they touch. Before the drawn
    /// touch nothing moves (no stop-start, no lunge).</item>
    /// <item>Past the tipping point (its centre of mass beyond the far track) gravity takes it: it falls over the far track onto its side, rolls
    /// over its top edges and lands on its roof (each new corner that meets the ground becomes the pivot, energy lost at each), a heavy landing
    /// (dust, the crash, a camera kick if on screen), a damped rock, then it lies. The death (the blast, the wreck) is drawn after the landing
    /// (VehicleDeathSync: <see cref="DeathWait"/>).</item>
    /// <item>Safety: at the rules' heave it is given the swing it needs to pass the tip if the drawn Mammoth had not turned it far enough.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class OzRamLever : MonoBehaviour
    {
        /// <summary>Switch: false = DV188's push + flip (OzRamPush / OzRamFlip) as before (proof only).</summary>
        public static bool On = true;
        /// <summary>Gravity of the fall (x 9.81 m/s2: the flip reads as a heavy fall at the RTS scale), energy kept when a new corner becomes the
        /// pivot while it rolls on, when it lands on a face (the roof), the substep.</summary>
        public static float GravityK = 5f, RollKeep = 0.85f, LandKeep = 0.3f, FaceKeep = 0.85f, CarryK = 1.6f;
        const float Sub = 1f / 360f;
        // statistics (the Play check / proof)
        public static int Levers, Tips, Kicks, Roofs, Sparks;
        public static float LastTipAt = -1f, LastLandAt = -1f, LastTouchAt = -1f, MaxGapSeen, WedgeDeg;
        /// <summary>Proof: the last lever's outline, centre of mass, pivot, the Mammoth's front and the first pushes (text).</summary>
        public static readonly System.Text.StringBuilder Dbg = new System.Text.StringBuilder();

        static readonly Dictionary<ZObject, OzRamLever> live = new Dictionary<ZObject, OzRamLever>();
        public static bool Holds(ZObject o) => o != null && live.Count > 0 && live.TryGetValue(o, out var l) && l && l.Holding;

        /// <summary>VehicleDeathSync: a lever that has not landed yet holds the drawn death until it has (+ a short beat), at most 1.2 s.</summary>
        public static bool DeathWait(ZObject o, out float wait)
        {
            wait = 0f;
            if (o == null || live.Count == 0 || !live.TryGetValue(o, out var l) || !l || !l.Holding) return false;
            if (l.landedAt >= 0f) { wait = Mathf.Max(0f, l.landedAt + 0.12f - Time.time); return wait > 0.015f; }
            wait = 1.2f; l.deathAsked = true; return true;
        }

        /// <summary>VehicleDeathSync.Later: the time the held death plays (landing + 0.12 s), or the fallback.</summary>
        public static float DueAt(ZObject o, float fallback)
        {
            if (o == null || !live.TryGetValue(o, out var l) || !l) return fallback;
            return l.landedAt >= 0f ? Mathf.Min(fallback, l.landedAt + 0.12f) : fallback;
        }

        public bool Holding { get; private set; }
        public float Angle => theta * Mathf.Rad2Deg;

        ZGame game; ZObject obj, mammoth; UnitView view; Transform mt;
        Vector3 dir, axis, p0, latBase; Quaternion r0; float gy0;
        // the body: its outline in (u, y) at the contact, the centre of mass, the reference point mapping
        Vector2[] poly; Vector2 com; float k2; int pivot; Vector2 pivotAt; float theta, omega;
        // the Mammoth's front: (reach along the ram from its position, height above its ground)
        readonly List<Vector2> mfront = new List<Vector2>(), mraw = new List<Vector2>(), mupper = new List<Vector2>();
        float t, sparkAt, landedAt = -1f, beganAt; bool touched, tipped, kicked, landed, deathAsked, resting; int roofLanded;
        float lastTheta; uint seed; Vector2[] wbuf;
        readonly List<Renderer> shadows = new List<Renderer>();

        public static void Begin(ZGame g, ZObject target, UnitView tv, ZObject m, Vector3 d)
        {
            if (target == null || !tv) return;
            if (live.TryGetValue(target, out var old) && old) return;
            var l = tv.gameObject.AddComponent<OzRamLever>();
            l.Init(g, target, tv, m, d);
            live[target] = l; Levers++;
        }

        void Init(ZGame g, ZObject target, UnitView tv, ZObject m, Vector3 d)
        {
            game = g; obj = target; view = tv; mammoth = m; beganAt = Time.time;
            mt = m != null && g.Views.TryGetValue(m.Id, out var mv) && mv ? mv.transform : null;
            dir = new Vector3(d.x, 0f, d.z); if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward; dir.Normalize();
            axis = Vector3.Cross(Vector3.up, dir).normalized;
            if (Vector3.Dot(Quaternion.AngleAxis(90f, axis) * Vector3.up, dir) < 0f) axis = -axis;   // + turns "up" toward the ram direction
            p0 = transform.position; r0 = transform.rotation; gy0 = Floor(0f);
            latBase = p0 - dir * Vector3.Dot(p0, dir); latBase.y = 0f;
            seed = (uint)((m != null ? m.Id : 0) * 7919 + target.Id * 104729);
            BuildBody();
            if (mt) BuildFront();
            liftEnd = (mt ? Vector3.Dot(mt.position - p0, dir) : 0f) + Mathf.Max(1f, target.RamLiftPlan / 8f);   // where the rules' heave comes (the Mammoth's advance)
            lastMUFrame = mt ? Vector3.Dot(mt.position - p0, dir) : 0f;
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.enabled && r.name.ToLowerInvariant().Contains("shadow")) { r.enabled = false; shadows.Add(r); }   // a blob shadow quad would turn over with it
            Holding = true; lastTheta = 0f;
            Dbg.Clear();
            Dbg.Append("outline (u,y):"); foreach (var q in poly) Dbg.Append(" (" + q.x.ToString("0.00") + "," + q.y.ToString("0.00") + ")");
            Dbg.Append(" | com " + com.ToString("F2") + " pivot " + pivot + " " + pivotAt.ToString("F2") + " k2 " + k2.ToString("0.00") + " | front:");
            foreach (var q in mfront) Dbg.Append(" (" + q.x.ToString("0.00") + "," + q.y.ToString("0.00") + ")");
            Dbg.Append(" | mU at begin " + (mt ? Vector3.Dot(mt.position - p0, dir) : -99f).ToString("0.00") + " ;; ");
        }

        // ------------------------------------------------------------------ geometry
        static bool Skip(Transform t, Transform root, bool hullOnly, out bool barrel)
        {
            barrel = false;
            for (; t && t != root.parent; t = t.parent)
            {
                var n = t.name.ToLowerInvariant();
                if (n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring") || n.Contains("wreck")) return true;
                if (n.Contains("barrel") || n.Contains("gunshoulder") || n.Contains("mantlet") || n.Contains("recoil")) barrel = true;
                if (hullOnly && (n.Contains("turret") || n.Contains("hatch") || n.Contains("antenn") || n.Contains("lamp") || n.Contains("light"))) return true;
            }
            return false;
        }

        static IEnumerable<Vector3> Points(Renderer r)
        {
            Mesh mesh = null; var mf = r.GetComponent<MeshFilter>(); if (mf) mesh = mf.sharedMesh; if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
            var m = r.localToWorldMatrix;
            if (mesh != null && mesh.isReadable && mesh.vertexCount > 0)
            {
                var v = mesh.vertices; int step = Mathf.Max(1, v.Length / 1500);
                for (int i = 0; i < v.Length; i += step) yield return m.MultiplyPoint3x4(v[i]);
            }
            else
            {
                var lb = r.localBounds;
                for (int c = 0; c < 8; c++) yield return m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
            }
        }

        static bool Drawn(Renderer r) => r && r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer) && !(r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450);

        /// <summary>The target's outline along the ram (u from p0, y above the ground), its convex hull, centre of mass and the pivot.</summary>
        void BuildBody()
        {
            var pts = new List<Vector2>();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!Drawn(r) || Skip(r.transform, transform, false, out bool barrel) || barrel) continue;
                foreach (var w in Points(r)) pts.Add(new Vector2(Vector3.Dot(w - p0, dir), Mathf.Max(0f, w.y - gy0)));
            }
            if (pts.Count < 3) { pts.Add(new Vector2(-1.5f, 0f)); pts.Add(new Vector2(1.5f, 0f)); pts.Add(new Vector2(1.5f, 1.5f)); pts.Add(new Vector2(-1.5f, 1.5f)); }
            // the bottom on the ground (tracks / wheels rest there)
            float minY = float.MaxValue; foreach (var p in pts) minY = Mathf.Min(minY, p.y);
            for (int i = 0; i < pts.Count; i++) pts[i] = new Vector2(pts[i].x, pts[i].y - minY);
            poly = Hull(pts);
            // centre of mass: the outline's area centroid, a little low (the hull is heavier than the turret)
            float A = 0f; Vector2 C = Vector2.zero; float minU = float.MaxValue, maxU = float.MinValue, maxY = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Length]; float cr = a.x * b.y - b.x * a.y;
                A += cr; C += (a + b) * cr;
                minU = Mathf.Min(minU, a.x); maxU = Mathf.Max(maxU, a.x); maxY = Mathf.Max(maxY, a.y);
            }
            com = Mathf.Abs(A) > 1e-5f ? C / (3f * A) : new Vector2((minU + maxU) * 0.5f, maxY * 0.4f);
            com.y *= 0.85f;
            float W = maxU - minU, H = maxY;
            // the pivot: the far bottom corner (the far track on the ground)
            pivot = 0; float best = float.MinValue;
            for (int i = 0; i < poly.Length; i++) { var p = poly[i]; float s = p.x - 4f * p.y; if (s > best) { best = s; pivot = i; } }
            pivotAt = poly[pivot];
            k2 = (W * W + H * H) / 12f;                                                     // about the centre; + d^2 per pivot (KAbout)
            theta = 0f; omega = 0f;
        }

        float KAbout(Vector2 pv) => k2 + (com - pv).sqrMagnitude;

        /// <summary>Andrew's monotone chain (counter-clockwise).</summary>
        static Vector2[] Hull(List<Vector2> p)
        {
            p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = h.Count;
                for (int i = 0; i < p.Count; i++)
                {
                    var q = pass == 0 ? p[i] : p[p.Count - 1 - i];
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], q) <= 1e-6f) h.RemoveAt(h.Count - 1);
                    h.Add(q);
                }
                h.RemoveAt(h.Count - 1);
            }
            return h.ToArray();
        }
        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>The Mammoth's drawn front by height (its hull: no turret, barrels, hatch, lamps): the reach along the ram from its position.</summary>
        void BuildFront()
        {
            const float band = 0.15f; var reach = new float[24]; for (int i = 0; i < reach.Length; i++) reach[i] = float.MinValue;
            float mg = game.GroundY(mt.position.x, mt.position.z);
            foreach (var r in mt.GetComponentsInChildren<Renderer>())
            {
                if (!Drawn(r) || Skip(r.transform, mt, true, out bool barrel) || barrel) continue;
                foreach (var w in Points(r))
                {
                    float y = w.y - mg; if (y < -0.05f || y >= band * reach.Length) continue;
                    int b = Mathf.Clamp((int)(y / band), 0, reach.Length - 1);
                    reach[b] = Mathf.Max(reach[b], Vector3.Dot(w - mt.position, dir));
                }
            }
            mfront.Clear();
            for (int i = 0; i < reach.Length; i++) if (reach[i] > float.MinValue) mfront.Add(new Vector2(reach[i], Mathf.Min((i + 0.5f) * band, i == 0 ? 0.08f : 9f)));
            mraw.Clear(); mraw.AddRange(mfront);
            // the wedge the hull rides up: from its foremost low spike straight up and back to the top of its glacis (the spikes between bite in
            // a little), the raw outline above that
            int ti = -1; for (int i = 0; i < mfront.Count; i++) if (mfront[i].y <= 0.7f && (ti < 0 || mfront[i].x > mfront[ti].x)) ti = i;
            if (ti >= 0)
            {
                var tip = mfront[ti]; int bi = -1;
                for (int i = ti + 1; i < mfront.Count; i++) if (mfront[i].x >= tip.x - 2.5f && mfront[i].y <= 2.2f) bi = i;
                if (bi > ti)
                {
                    var top = mfront[bi]; float k = (tip.x - top.x) / Mathf.Max(0.1f, top.y - tip.y);
                    for (int i = 0; i < mfront.Count; i++)
                    {
                        var q = mfront[i];
                        if (q.y <= tip.y) mfront[i] = new Vector2(tip.x + (tip.y - q.y) * k, q.y);   // the blade's edge down to the ground: the hull's lower edge rides up the slope
                        else if (i <= bi) mfront[i] = new Vector2(tip.x - (q.y - tip.y) * k, q.y);
                    }
                    WedgeDeg = Mathf.Atan2(top.y - tip.y, tip.x - top.x) * Mathf.Rad2Deg;
                }
            }
            mupper.Clear(); foreach (var q in mfront) if (q.y >= 0.9f) mupper.Add(q);       // the turn: only the glacis and the top keep out of the hull (the low blade is under it)
        }

        float Floor(float u)
        {
            var w = p0 + dir * u; float y = game.GroundY(w.x, w.z);
            if (OzWaterLanding.OpenWaterAt(new Vector3(w.x, y, w.z), y, out float wy)) y = wy;
            return y;
        }

        // ------------------------------------------------------------------ the body's pose
        static Vector2 Rot(Vector2 v, float th) { float c = Mathf.Cos(th), s = Mathf.Sin(th); return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c); }
        Vector2 World(int i, float th) => pivotAt + Rot(poly[i] - poly[pivot], th);
        Vector2 ComAt(float th) => pivotAt + Rot(com - poly[pivot], th);

        /// <summary>The Mammoth's wedge overlaps the outline at angle th (about the current pivot)?</summary>
        bool Overlap(float th, float mU, out Vector2 at) => Overlap(th, mU, out at, liftPhase >= 3 ? mraw : mfront);
        bool Overlap(float th, float mU, out Vector2 at, List<Vector2> mf)
        {
            at = Vector2.zero;
            int n = poly.Length; if (wbuf == null || wbuf.Length != n) wbuf = new Vector2[n]; var w = wbuf;
            for (int i = 0; i < n; i++) w[i] = World(i, th);
            // a wedge point inside the outline
            for (int j = 0; j < mf.Count; j++)
            {
                var m = new Vector2(mU + mf[j].x, mf[j].y);
                bool inside = true;
                for (int i = 0; i < n && inside; i++) if (Cross(w[i], w[(i + 1) % n], m) < -1e-4f) inside = false;
                if (inside) { at = m; return true; }
            }
            // an outline corner behind the wedge's front at its height
            float top = mf.Count > 0 ? mf[mf.Count - 1].y + 0.08f : 0f;
            for (int i = 0; i < n; i++)
            {
                if (w[i].y > top) continue;
                float reach = FrontAt(w[i].y, mf);
                if (reach > float.MinValue && w[i].x < mU + reach - 1e-3f && w[i].x > mU + reach - 3f) { at = w[i]; return true; }
            }
            return false;
        }

        static float FrontAt(float y, List<Vector2> mf)
        {
            if (mf.Count == 0) return float.MinValue;
            if (y <= mf[0].y) return mf[0].x;
            for (int j = 1; j < mf.Count; j++)
                if (y <= mf[j].y) { float k = (y - mf[j - 1].y) / Mathf.Max(1e-4f, mf[j].y - mf[j - 1].y); return Mathf.Lerp(mf[j - 1].x, mf[j].x, k); }
            return float.MinValue;
        }

        // ------------------------------------------------------------------ per frame
        float lastMU = float.NaN, lastMUFrame, prevNeed = -1f, liftEnd; int dbgN;
        bool settling; float settleGoal, settleV;
        void LateUpdate()
        {
            var w = game != null ? game.World : null;
            if (obj == null || w == null || !view || view.Dying || t > 8f) { End(); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f); t += dt;
            float mU = mt ? Vector3.Dot(mt.position - p0, dir) : -1e4f;
            if (float.IsNaN(lastMU)) lastMU = mU;
            bool heave = obj.RamHeaveTick >= 0 && w.TickIndex >= obj.RamHeaveTick;
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / Sub)); float s = dt / n;
            Vector2 touchAt = Vector2.zero; bool pushedThisFrame = false;
            for (int i = 0; i < n; i++) if (Step(s, Mathf.Lerp(lastMU, mU, (i + 1f) / n), ref touchAt)) pushedThisFrame = true;   // the wedge's drawn move spread over the substeps
            lastMU = mU;
            if (dt > 1e-4f) vDrawn = Mathf.Lerp(vDrawn, Mathf.Max(0f, (mU - lastMUFrame) / dt), 0.3f);
            lastMUFrame = mU;
            if (heave && liftPhase >= 1 && liftPhase <= 2) { liftPhase = 3; Kick(); tipped = true; LastTipAt = Time.time; Dbg.Append("heave before the tip at " + (theta * Mathf.Rad2Deg).ToString("0.0") + " ;; "); }
            Place();
            Fx(pushedThisFrame, touchAt, dt);
            lastTheta = theta;
        }

        /// <summary>The largest swing the wedge can give (rad/s).</summary>
        public static float MaxPushOmega = 3f;
        /// <summary>From this angle on its way over (deg) it settles onto its roof: the resting angle of least height, a damped rock.</summary>
        public static float SettleFromDeg = 150f, SettleHz = 3.2f, SettleDamp = 0.35f;

        /// <summary>1.5 rework 14i: the lift's phases: 0 waiting for the drawn touch, 1 the near side rides up the wedge (the contact), 2 the
        /// Mammoth drives on under it and turns it on to the tipping point by the rules' heave (its advance sets the angle, the wedge never
        /// enters the hull), 3 the fall (gravity, the corners on the ground), then the settle onto its roof.</summary>
        int liftPhase; float uTouch, s1, th1, thTip, sTip, vDrawn;
        /// <summary>The share of the wedge's slope at which the designed turn takes over from the contact; the turn's ease (angle ~ x^k).</summary>
        public static float SlopeShare = 0.8f, TurnEase = 1.6f;

        /// <summary>The smallest angle (about the first pivot) at which the wedge (profile mf) is out of the outline, searched upward from th.</summary>
        float ContactAngle(float th, float mU, List<Vector2> mf, out Vector2 at)
        {
            at = Vector2.zero;
            if (mf.Count == 0 || !Overlap(th, mU, out at, mf)) return th;
            float lo = th, hi = th + 0.6f;
            if (Overlap(hi, mU, out _, mf)) hi = th + 1.4f;
            for (int k = 0; k < 18; k++) { float mid = (lo + hi) * 0.5f; if (Overlap(mid, mU, out _, mf)) lo = mid; else hi = mid; }
            Overlap(lo, mU, out at, mf);
            return hi;
        }

        /// <summary>One substep. True = the wedge pushed (sparks).</summary>
        bool Step(float dt, float mU, ref Vector2 touchAt)
        {
            if (resting) return false;
            if (settling) { Settle(dt); return false; }
            if (liftPhase <= 2)
            {
                Vector2 at = Vector2.zero; float thC = 0f; if (liftPhase <= 1) thC = ContactAngle(0f, mU, mfront, out at);
                if (liftPhase == 0)
                {
                    if (thC <= 1e-5f) return false;
                    liftPhase = 1; uTouch = mU; touched = true; LastTouchAt = Time.time;
                    var c0 = com - poly[pivot]; thTip = Mathf.Atan2(-c0.x, c0.y);              // the centre of mass over the far track
                    sTip = Mathf.Max(0.8f, liftEnd - mU);
                    Dbg.Append("touch mU " + mU.ToString("0.00") + " tip " + (thTip * Mathf.Rad2Deg).ToString("0.0") + " deg after " + sTip.ToString("0.00") + " m ;; ");
                }
                float s = mU - uTouch, goal;
                if (liftPhase == 1)
                {
                    goal = thC;
                    if (thC >= SlopeShare * WedgeDeg * Mathf.Deg2Rad || s >= 0.6f * sTip) { liftPhase = 2; s1 = s; th1 = thC; Dbg.Append("turn from " + (th1 * Mathf.Rad2Deg).ToString("0.0") + " at " + s1.ToString("0.00") + " m ;; "); }
                }
                else
                {
                    float x = Mathf.Clamp01((s - s1) / Mathf.Max(0.3f, sTip - s1));
                    float thU = ContactAngle(0f, mU, mupper, out var atU); if (thU > 1e-5f) at = atU;
                    goal = Mathf.Max(th1 + (thTip - th1) * Mathf.Pow(x, TurnEase), Mathf.Min(thU, thTip));
                }
                goal = Mathf.Max(goal, theta);                                                // never turned back
                float w0 = (goal - theta) / dt; omega = Mathf.Lerp(omega, w0, 0.25f);
                bool pushed = goal > theta + 1e-6f; theta = goal; touchAt = at;
                if (theta >= thTip - 1e-3f)
                {   // the tipping point: the fall, with the turn's own swing
                    liftPhase = 3; tipped = true; Tips++; LastTipAt = Time.time;
                    float dx = Mathf.Max(0.3f, sTip - s1);
                    omega = Mathf.Max(omega, Mathf.Max((thTip - th1) * TurnEase / dx * Mathf.Max(1f, vDrawn), 1.2f));
                    Dbg.Append("tip at " + (mU - uTouch).ToString("0.00") + " m, swing " + omega.ToString("0.00") + " rad/s ;; ");
                }
                return pushed;
            }
            // the fall
            var c = ComAt(theta); float lever = c.x - pivotAt.x;
            omega += 9.81f * GravityK * lever / KAbout(poly[pivot]) * dt;
            theta += omega * dt;
            bool push = false;
            if (mupper.Count > 0 && Overlap(theta, mU, out _, mupper))
            {   // the Mammoth's glacis still shoves it (its low blade is under the hull)
                float need = ContactAngle(theta, mU, mupper, out touchAt);
                if (need > theta) { omega = Mathf.Max(omega, Mathf.Min(MaxPushOmega, (need - theta) / dt)); theta = need; push = true; }
            }
            Ground(dt);
            if (!settling && theta * Mathf.Rad2Deg >= SettleFromDeg) BeginSettle();
            return push;
        }

        /// <summary>The resting angle on its roof: of the angles 150-215 deg the one whose centre of mass is lowest with no corner below the floor
        /// (about the current contact's place), and the lift that rests it there.</summary>
        float RestLift(float th, out float lowestU)
        {
            float need = float.MinValue; lowestU = pivotAt.x;
            for (int i = 0; i < poly.Length; i++)
            {
                var p = World(i, th); float d = (Floor(p.x) - gy0) - p.y;
                if (d > need) { need = d; lowestU = p.x; }
            }
            return need;
        }

        void BeginSettle()
        {
            settling = true; settleV = omega;
            float best = float.MaxValue; settleGoal = Mathf.PI;
            for (float d = 150f; d <= 215f; d += 1f)
            {
                float th = d * Mathf.Deg2Rad; float L = RestLift(th, out _);
                float h = ComAt(th).y + L;
                if (h < best - 1e-4f) { best = h; settleGoal = th; }
            }
        }

        /// <summary>Onto its roof: a damped rock to the resting angle about the contact, its lowest corner kept on the floor; the heavy landing
        /// when it first meets the floor with its roof.</summary>
        void Settle(float dt)
        {
            float w = 2f * Mathf.PI * SettleHz;
            settleV += (w * w * (settleGoal - theta) - 2f * SettleDamp * w * settleV) * dt;
            theta += settleV * dt; omega = settleV;
            float L = RestLift(theta, out float lu);
            if (L > -1e-3f) pivotAt.y += L;                                                // resting on the floor (never in it)
            else if (L < -0.002f) pivotAt.y += Mathf.Max(L, -4f * dt);                       // falls the last bit onto it
            if (!landed && Mathf.Abs(theta - settleGoal) < 0.35f) { Landing(new Vector2(lu, Floor(lu) - gy0), Mathf.Abs(settleV) * 2f, true); }
            if (landed && Mathf.Abs(settleV) < 0.05f && Mathf.Abs(theta - settleGoal) < 0.01f) { theta = settleGoal; resting = true; }
        }

        bool pivotIsFirst = true;

        /// <summary>The corner that meets the ground in the turn's direction makes the body pivot on its foremost ground contact (in the turn's
        /// direction). Rolling on toward its roof it keeps RollKeep of its swing (a face meeting the ground: FaceKeep), at least what carries its
        /// centre of mass over that contact (the ram's shove turns it over onto its roof); on its roof (its centre of mass over the contacts) it
        /// keeps LandKeep (a damped rock) and then it lies.</summary>
        void Ground(float dt)
        {
            int n = poly.Length; int lowest = -1; float pen = -2e-3f;
            for (int i = 0; i < n; i++)
            {
                if (i == pivot) continue;
                var p = World(i, theta); float d = p.y - (Floor(p.x) - gy0);
                if (d < pen) { pen = d; lowest = i; }
            }
            if (lowest < 0) return;
            var lp = World(lowest, theta);
            bool ahead = lp.x > pivotAt.x;
            if (!((omega > 0f && ahead) || (omega < 0f && !ahead))) return;
            float vHit = Mathf.Abs(omega) * (lp - pivotAt).magnitude;
            float lift = -pen; var oldPivot = pivotAt;
            // the contacts after the lift: the foremost one in the turn's direction is the new pivot
            int best = lowest; float bx = lp.x, cMin = float.MaxValue, cMax = float.MinValue; Vector2 bp = lp + new Vector2(0f, lift);
            for (int i = 0; i < n; i++)
            {
                var p = World(i, theta); p.y += lift;
                if (p.y - (Floor(p.x) - gy0) > 0.03f) continue;
                cMin = Mathf.Min(cMin, p.x); cMax = Mathf.Max(cMax, p.x);
                if (omega > 0f ? p.x > bx : p.x < bx) { bx = p.x; best = i; bp = p; }
            }
            if (best == lowest) bp = lp + new Vector2(0f, lift);
            pivot = best; pivotAt = bp; pivotIsFirst = false;
            var c = ComAt(theta);
            bool onFace = cMax - cMin > 0.3f && c.x > cMin + 0.05f && c.x < cMax - 0.05f;
            float deg = Mathf.Abs(theta * Mathf.Rad2Deg);
            bool roof = onFace && deg > 125f;
            if (roof) omega *= LandKeep;
            else if (onFace) omega *= FaceKeep;
            else if (Mathf.Abs(pivotAt.x - oldPivot.x) > 0.25f) omega *= RollKeep;
            if (!roof && omega > 0f && deg < 125f)
            {   // rolling on toward its roof: enough swing to carry its centre of mass over this contact
                var r = c - pivotAt; float dh = r.magnitude - r.y;
                if (r.x < 0f && dh > 0f) omega = Mathf.Max(omega, Mathf.Sqrt(2f * 9.81f * GravityK * dh / KAbout(poly[pivot])) * CarryK);
            }
            if (vHit > 0.5f || roof) Landing(bp, vHit, roof);
            if (roof && Mathf.Abs(omega) < 0.8f) { omega = 0f; resting = true; }
        }

        void Landing(Vector2 at, float speed, bool roof)
        {
            var wp = latBase + dir * (Vector3.Dot(p0, dir) + at.x); wp.y = gy0 + at.y;
            float heavy = Mathf.Clamp01(speed / 3f);
            if (roof && !landed)
            {   // on its roof: the heavy landing
                landed = true; landedAt = Time.time; LastLandAt = landedAt; Roofs++;
                AudioDirector.Fire("dst_turret_crash", wp, obj.Id, Mathf.Lerp(1.0f, 1.3f, heavy), 0.62f);
                if (FxManager.I) { FxManager.I.Dust(new Vector3(wp.x, wp.y + 0.25f, wp.z), 1.3f); FxManager.I.Dust(new Vector3(wp.x, wp.y + 0.25f, wp.z) - dir * 1.2f, 1.0f); }
                var rts = Object.FindFirstObjectByType<RtsCamera>();
                if (rts) rts.Shake(0.6f, wp, "ram_flip_land");
            }
            else if (!landed && tipped && speed > 0.8f)
            {   // onto its side on the way over
                roofLanded++;
                AudioDirector.Fire("dst_turret_crash", wp, obj.Id, Mathf.Lerp(0.6f, 0.95f, heavy), 0.75f);
                if (FxManager.I) FxManager.I.Dust(new Vector3(wp.x, wp.y + 0.2f, wp.z), 0.8f);
            }
        }

        /// <summary>The rules' heave came before the drawn tip: the swing it needs to pass the tip (+15 %).</summary>
        void Kick()
        {
            kicked = true; Kicks++;
            var c = ComAt(theta); var r = c - pivotAt; float dh = r.magnitude - r.y;
            if (dh <= 0f) return;
            float need = Mathf.Sqrt(2f * 9.81f * GravityK * dh / KAbout(poly[pivot])) * 1.15f;
            if (omega < need) omega = need;
        }

        void Place()
        {
            // the body point that was p0's reference: the pivot corner moved from its first place to pivotAt, turned by theta
            var q = Quaternion.AngleAxis(theta * Mathf.Rad2Deg, axis);
            var pv0 = poly[pivot];                                                         // the pivot corner at the contact (u, y)
            var pivotW0 = latBase + dir * (Vector3.Dot(p0, dir) + pv0.x) + Vector3.up * (gy0 + pv0.y);
            var pivotW = latBase + dir * (Vector3.Dot(p0, dir) + pivotAt.x) + Vector3.up * (gy0 + pivotAt.y);
            var lat = p0 - (latBase + dir * Vector3.Dot(p0, dir)); lat.y = 0f;              // p0's own sideways offset from the ram line
            pivotW0 += lat; pivotW += lat;
            transform.SetPositionAndRotation(pivotW + q * (p0 - pivotW0), q * r0);
        }

        void Fx(bool pushed, Vector2 touchAt, float dt)
        {
            if (tipped && view) { var wb14j = view.GetComponent<OzImpactWobble>(); if (wb14j) wb14j.FadeOut(); }   // 1.5 last pass 14j R20: the fall takes over from the rock
            if (!pushed) return;
            var wp = latBase + dir * (Vector3.Dot(p0, dir) + touchAt.x); wp.y = gy0 + Mathf.Max(0.15f, touchAt.y);
            var lat = p0 - (latBase + dir * Vector3.Dot(p0, dir)); lat.y = 0f; wp += lat;
            if (Time.time < sparkAt) return;
            bool first = Sparks == 0 || Time.time - beganAt < 0.05f;
            WreckFx.SparkFountain(wp, first ? 18 : 9, first ? 2f : 1.3f, seed++);
            OpenZHitSparks.Burst(wp, -dir + Vector3.up * 0.4f, FxWeapon.MachineGun, FxSurface.Metal, first ? 1.3f : 0.8f, false);
            sparkAt = Time.time + 0.07f; Sparks++;
            OzRamImpactLook.Contact(obj, view, wp, mt ? mt.position : wp - dir, dir, 45f);   // 1.5 last pass 14j R17 / R20: the target's armour chips with the sparks + its jolt on the contact frame (View/OzRamImpactLook.cs)
        }

        void End()
        {
            Holding = false;
            if (obj != null && live.TryGetValue(obj, out var l) && l == this) live.Remove(obj);
            Destroy(this);
        }

        void OnDestroy()
        {
            Holding = false;
            if (obj != null && live.TryGetValue(obj, out var l) && l == this) live.Remove(obj);
        }
    }
}
