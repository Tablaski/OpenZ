using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The crushing Bizon's ride up onto, over and off its target (view only; the rules drive it straight on: DV183, with DV187 past the wreck
    /// <para>THE SUPPORT. The Bizon's bottom is three rails measured from its drawn meshes at the contact: the left track, the right track and
    /// the belly between them (the hull's lowest line). Under each rail, every frame, the support is the ground or the target's hull
    /// (OzCrushSquash.SupportAt: its whole top until the front track has rolled onto that spot, then giving way to its crushed height), sampled
    /// across the rail's width. Of every pitch (1 deg, then 0.1 deg), the pose of the lowest centre of mass whose rails clear their supports
    /// is the goal, with the roll that lets both tracks rest (one track on the hull and one on the ground: it leans) and the lift; a small
    /// preference for the last goal's pitch keeps two equal poses from flickering.</para>
    /// <para>THE MOTION. The goal is smoothed (<see cref="GoalTau"/>), then followed by critically damped springs on pitch, roll and lift
    /// (<see cref="FollowHz"/>, faster while the support pushes it up: <see cref="RiseHz"/>; the goal's own speed fed forward, so a steady
    /// climb has no lag), never below the ground. A heavy wobble rides on top: a damped spring (<see cref="WobbleHz"/>, damping
    /// <see cref="WobbleDamp"/>) pushed by the follower's acceleration, so the hull lags, overshoots and settles when it goes up, tips over the
    /// top and comes down. No snap anywhere: no frame-to-frame jitter.</para>
    /// <para>One rigid body: <see cref="OzCrushRideRest"/> puts the Model node back on its rest pose before UnitView syncs the tank (the planted
    /// running gear is placed under the level hull), then this turns and lifts the whole Model node (hull, turret, tracks, wheels, plates; the
    /// neon and lamps follow it in OzBigTanksFx) about the ground point under the Bizon.</para>
    /// <para>The target's death is held (VehicleDeathSync: <see cref="DeathWait"/>) until the Bizon's rear has rolled off the wreck (the landing:
    /// thud and dust), then it explodes <see cref="RollOffBlastS"/> later. OzCrushSquash reads this ride's drawn underside
    /// (<see cref="UnderAt"/>) to dent the target where the Bizon really presses it.</para>
    /// </summary>
    [DefaultExecutionOrder(1001)]
    public class OzCrushRide : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>1.5 kit crush_swamp_fix: the target's death waits until the Bizon has rolled over it (VehicleDeathSync).</summary>
        public static bool HoldDeath = true;
        /// <summary>Unused since 1.5 rework 14i (the rules drive the Bizon's rear off the wreck).</summary>
        public static bool Squeeze = false;
        public static bool SteadyRide = true;
        public const float SquashLead = 0.30f, FlatAtM = 0.6f;
        /// <summary>The steepest pitch and roll (deg).</summary>
        public const float MaxPitch = 26f, MaxRoll = 14f;
        /// <summary>1.5 kit bizon_crush_v3: the goal's smoothing (s), the followers' frequency (Hz) and while the support pushes it up, the
        /// wobble's frequency (Hz), damping and push (x the follower's acceleration; the lift's share), the hysteresis (m of centre-of-mass
        /// height per degree away from the last goal).</summary>
        public static float GoalTau = 0.045f, FollowHz = 3.2f, RiseHz = 5.5f, WobbleHz = 1.55f, WobbleDamp = 0.3f, WobbleK = 0.5f, LiftWobbleK = 0.25f, Hysteresis = 0.002f;
        public static float MaxWobbleDeg = 2.5f, MaxWobbleLift = 0.08f;
        public const float MaxDeathWaitS = 1.5f, RollOffBlastS = 0.3f;
        static readonly Dictionary<int, float> dueByTarget = new Dictionary<int, float>();
        /// <summary>When the held target `targetId` explodes now (Time.time), or `fallback` when no ride holds it.</summary>
        public static float DueAt(int targetId, float fallback) => dueByTarget.Count > 0 && dueByTarget.TryGetValue(targetId, out var t) ? t : fallback;
        public static int RollOffs; public static float LastRollOffToBlast = -1f;

        // statistics (the Play check / proof)
        public static int Rides, Crests, Landings, Thuds, DeathsHeld, Squeezes;
        public static float MaxPitchSeen, MaxLiftSeen, MaxRollSeen, MaxDeathWaitSeen, MaxSqueezeSeen;
        public static float LastRiseGap = -9f, LastTouchToLand = -1f, Pitch, Lift, Roll, GoalPitch;
        /// <summary>The frames check: per frame of the last ride (time, pitch, roll, lift).</summary>
        public static readonly List<Vector4> Trace = new List<Vector4>(1024);

        static readonly Dictionary<int, OzCrushRide> byTarget = new Dictionary<int, OzCrushRide>();

        /// <summary>VehicleDeathSync.Compute: a target the Bizon is crushing is drawn exploding after the Bizon has rolled off it: the seconds to
        /// wait (true), or false (no ride: its own way).</summary>
        public static bool DeathWait(ZGame g, ZObject o, out float wait)
        {
            wait = 0f;
            if (!On || !HoldDeath || o == null || !o.RamCrushed || byTarget.Count == 0 || !byTarget.TryGetValue(o.Id, out var r) || !r || !r.begun) return false;
            if (r.obj == null || r.obj.Destroyed || !r.view || r.view.Dying || !r.targetView || r.targetView.Dying) return false;
            wait = r.PlanDeath();
            return wait > 0.015f;
        }

        // ------------------------------------------------------------------------------------------------ for OzCrushSquash
        /// <summary>The Bizon's Model node (its drawn pose this frame).</summary>
        public Transform ModelNode => begun ? model : null;
        /// <summary>The ride presses: begun and not ending.</summary>
        public bool Pressing => begun && !ending;
        public Vector3 Dir => dir;
        public Vector3 Lat => lat;
        public Vector3 Origin => origin;
        /// <summary>The Bizon's half width (m), the reach of its underside from its Model node (m), its drawn speed along the crush (m/s).</summary>
        public float HalfWidthM => halfW;
        public float ReachM => reach;
        public float SpeedAlong => speedAlong;
        /// <summary>1.5 owner N5 (kit bizon_crush_turret_late): the front of the tracks' bottom run now (their lowest 0.3 m, where the Bizon's
        /// weight bears; not its tall front), m along the crush from the origin, its pitch counted.</summary>
        public float RunFrontAlong => sNow + uFront * Mathf.Cos(pitch * Mathf.Deg2Rad);
        /// <summary>A world point's distance along the crush from the ride's origin (m).</summary>
        public float AlongOf(Vector3 w) => Vector3.Dot(w - origin, dir);

        /// <summary>The Bizon's underside in its Model node's own space at (x, z): the lowest point of its tracks there, or its belly between
        /// them (1e3 = nothing of it there).</summary>
        public float UnderAt(float x, float z)
        {
            int i = Mathf.FloorToInt((x - ux0) / UCell), j = Mathf.FloorToInt((z - uz0) / UCell);
            if (i < 0 || j < 0 || i >= unx || j >= unz) return 1e3f;
            return under[j * unx + i];
        }

        /// <summary>The Bizon's front now (m along the crush from the origin) at world heights lo..hi (the turret it shoves); -1e9 = none there.</summary>
        public float FrontAlong(float lo, float hi)
        {
            float best = -1e9f, cp = Mathf.Cos(pitch * Mathf.Deg2Rad), sp = Mathf.Sin(pitch * Mathf.Deg2Rad);
            float gy = groundNow + lift;
            for (int b = 0; b < frontU.Length; b++)
            {
                if (frontU[b] < -1e8f) continue;
                float y = (b + 0.5f) * FBand, u = frontU[b];
                float yw = gy + u * sp + y * cp, uw = sNow + u * cp - y * sp;
                if (yw < lo - 0.2f || yw > hi + 0.2f) continue;
                best = Mathf.Max(best, uw);
            }
            return best;
        }

        // ------------------------------------------------------------------------------------------------ state
        ZGame game; ZObject obj, target; UnitView view, targetView; Transform model; OzCrushSquash squash;
        Vector3 restPos, dir, lat, origin; Quaternion restRot; float gyB, groundNow, sNow, sPrev, speedAlong;
        float pitch, roll, lift, life, explodeAt = -1f, heldAt = -1f, landedAt = -1f, touchAt = -1f, endT;
        float gP, gR, gL, gPv, gRv, gLv, fP, fPv, fR, fRv, fL, fLv, wP, wPv, wR, wRv, wL, wLv, hz, lastGoalP, crestFrom;
        bool ending, begun, rode, landed, rose, crested, frontDown, frontFalling;
        float near = 4f, far = 6f, halfW = 2.3f, reach = 5f, uFront = 3f, uRear = -3f;
        Vector2 bcom = new Vector2(0f, 1.25f);
        VehicleMotion vmHold; OzCrushRideRest rest;

        class Rail { public Vector2[] pts = new Vector2[0]; public float l0, l1, lc; public float[] prof = new float[0], gprof = new float[0]; public float pu0; public bool ok; }
        readonly Rail railL = new Rail(), railR = new Rail(), railM = new Rail();
        const float PStep = 0.2f, UCell = 0.2f, FBand = 0.25f;
        float[] under = new float[0]; int unx, unz; float ux0, uz0;
        float[] frontU = new float[0];
        static readonly List<Renderer> rs = new List<Renderer>();

        static bool Barrelish(Transform t, Transform root)
        {
            for (; t && t != root; t = t.parent)
            {
                var n = t.name.ToLowerInvariant();
                if (n.Contains("barrel") || n.Contains("gunshoulder") || n.Contains("mantlet") || n.Contains("recoil") || n.Contains("turret") || n.Contains("hatch") || n.Contains("antenn") || n.Contains("exhaust") || n.Contains("wreck")) return true;
            }
            return false;
        }
        static bool Skip(Renderer r)
        {
            if (!r || !r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) return true;
            var n = r.name.ToLowerInvariant();
            return n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring") || n.Contains("wreck") || n.Contains("glow") || n.Contains("cone") || (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450);
        }
        static IEnumerable<Vector3> Points(Renderer r)
        {
            Mesh mesh = null; var mf = r.GetComponent<MeshFilter>(); if (mf) mesh = mf.sharedMesh; if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
            var m = r.localToWorldMatrix;
            if (mesh != null && mesh.isReadable && mesh.vertexCount > 0) { var v = mesh.vertices; int step = Mathf.Max(1, v.Length / 2500); for (int i = 0; i < v.Length; i += step) yield return m.MultiplyPoint3x4(v[i]); }
            else { var lb = r.localBounds; for (int c = 0; c < 8; c++) yield return m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))); }
        }

        // ------------------------------------------------------------------------------------------------ begin
        public void Begin(ZGame g, ZObject o, UnitView v, ZObject t, UnitView tv, OzCrushSquash sq)
        {
            game = g; obj = o; view = v; target = t; targetView = tv; squash = sq; model = OzBizonCrushView.ModelOf(v);
            if (!model) { Destroy(this); return; }
            restPos = model.localPosition; restRot = model.localRotation;
            dir = t != null && (t.RamDirX != 0 || t.RamDirY != 0) ? new Vector3(t.RamDirX, 0f, -t.RamDirY) : (tv ? tv.transform.position - transform.position : transform.forward);
            dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) dir = transform.forward; dir.Normalize();
            lat = Vector3.Cross(Vector3.up, dir);
            origin = transform.position; gyB = g.GroundY(origin.x, origin.z); groundNow = gyB; sNow = sPrev = 0f;
            BuildBizon(v);
            if (sq) sq.Bind(this);                                                                       // the target's cells along this crush
            if (sq && sq.Faces(out float n0, out float f0)) { near = n0; far = f0; }
            else if (tv) { var d = Vector3.Dot(tv.transform.position - origin, dir); float r = OzBizonCrushView.M(ZPathing.Radius(t)); near = d - r; far = d + r; }
            begun = true; Rides++; Trace.Clear();
            if (t != null) byTarget[t.Id] = this;
            rest = GetComponent<OzCrushRideRest>(); if (!rest) rest = gameObject.AddComponent<OzCrushRideRest>();
            rest.ride = this;
            vmHold = v ? v.GetComponent<VehicleMotion>() : null; if (vmHold) vmHold.RideHoldTarget = 1f;
            hz = FollowHz;
        }

        /// <summary>The Bizon's rails (left track, right track, belly), its underside map in its Model node's space, its front by height, its
        /// width and reach, all from its drawn meshes at the contact (the turret, barrels, hatch, antennas and exhaust left out).</summary>
        void BuildBizon(UnitView v)
        {
            rs.Clear(); v.GetComponentsInChildren(false, rs);
            var m2l = model.worldToLocalMatrix;
            var pts = new List<(Vector3 w, bool track)>(8192);
            bool hasTracks = false;
            foreach (var r in rs) { if (Skip(r)) continue; if (IsTrack(r.transform, v.transform)) { hasTracks = true; break; } }
            foreach (var r in rs)
            {
                if (Skip(r) || Barrelish(r.transform, v.transform)) continue;
                bool track = hasTracks && IsTrack(r.transform, v.transform);
                foreach (var w in Points(r)) pts.Add((w, track));
            }
            if (pts.Count == 0) { FallbackRails(); return; }
            // the frame at the contact: along the crush (u), across (l), up from the ground (y)
            float minY = float.MaxValue, trackIn = float.MaxValue, lMax = 0f;
            foreach (var (w, track) in pts) { float y = w.y - gyB; if (track || !hasTracks) minY = Mathf.Min(minY, y); }
            foreach (var (w, track) in pts) { float l = Vector3.Dot(w - origin, lat); lMax = Mathf.Max(lMax, Mathf.Abs(l)); if (track) trackIn = Mathf.Min(trackIn, Mathf.Abs(l)); }
            if (!hasTracks || trackIn > lMax) trackIn = lMax * 0.55f;
            halfW = lMax;
            var binL = new Dictionary<int, float>(); var binR = new Dictionary<int, float>(); var binM = new Dictionary<int, float>();
            float lL0 = 0f, lL1 = -1e9f, lR0 = 1e9f, lR1 = 0f, tf = float.MinValue, tr = float.MaxValue, u0 = float.MaxValue, u1 = float.MinValue;
            lL0 = 1e9f; lR1 = -1e9f;
            var fr = new Dictionary<int, float>();
            foreach (var (w, track) in pts)
            {
                float u = Vector3.Dot(w - origin, dir), l = Vector3.Dot(w - origin, lat), y = w.y - gyB - minY;
                int b = Mathf.FloorToInt(u / PStep);
                int band = Mathf.Max(0, Mathf.FloorToInt((y + minY) / FBand)); fr[band] = fr.TryGetValue(band, out var f) ? Mathf.Max(f, u) : u;
                u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
                if (track || !hasTracks)
                {
                    if (l < 0f) { binL[b] = binL.TryGetValue(b, out var q) ? Mathf.Min(q, y) : y; lL0 = Mathf.Min(lL0, l); lL1 = Mathf.Max(lL1, l); }
                    else { binR[b] = binR.TryGetValue(b, out var q) ? Mathf.Min(q, y) : y; lR0 = Mathf.Min(lR0, l); lR1 = Mathf.Max(lR1, l); }
                    if (y < 0.3f) { tf = Mathf.Max(tf, u); tr = Mathf.Min(tr, u); }
                }
                else if (Mathf.Abs(l) < trackIn) binM[b] = binM.TryGetValue(b, out var q) ? Mathf.Min(q, y) : y;
            }
            // the belly never lower than the tracks at the same place along: its nose and tail ride up the track's front and rear curve (a sloped
            // glacis), and nothing of it reaches beyond the tracks' ends (else the low nose plate, 0.6 m ahead of the tracks, would plough into
            // the target and tear its turret off before the tracks even touch it)
            if (hasTracks)
            {
                var drop = new List<int>();
                foreach (var b in new List<int>(binM.Keys))
                {
                    bool l = binL.TryGetValue(b, out var yl), r = binR.TryGetValue(b, out var yr);
                    if (!l && !r) { drop.Add(b); continue; }
                    binM[b] = Mathf.Max(binM[b], l && r ? Mathf.Min(yl, yr) : l ? yl : yr);
                }
                foreach (var b in drop) binM.Remove(b);
            }
            SetRail(railL, binL, lL0, lL1); SetRail(railR, binR, lR0, lR1); SetRail(railM, binM, -trackIn * 0.9f, trackIn * 0.9f);
            if (!railL.ok || !railR.ok) { FallbackRails(); }
            uFront = tf > float.MinValue ? tf : u1; uRear = tr < float.MaxValue ? tr : u0;
            bcom = new Vector2((uFront + uRear) * 0.5f, 1.25f);
            int nb = 0; foreach (var k in fr.Keys) nb = Mathf.Max(nb, k + 1);
            frontU = new float[Mathf.Clamp(nb, 1, 40)]; for (int i = 0; i < frontU.Length; i++) frontU[i] = fr.TryGetValue(i, out var q) ? q : -1e9f;
            // the underside map in the Model node's space: the lowest track point per cell; between the tracks the belly line
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            var lp = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++) { lp[i] = m2l.MultiplyPoint3x4(pts[i].w); x0 = Mathf.Min(x0, lp[i].x); x1 = Mathf.Max(x1, lp[i].x); z0 = Mathf.Min(z0, lp[i].z); z1 = Mathf.Max(z1, lp[i].z); }
            ux0 = x0 - UCell; uz0 = z0 - UCell; unx = Mathf.CeilToInt((x1 - x0) / UCell) + 3; unz = Mathf.CeilToInt((z1 - z0) / UCell) + 3;
            under = new float[unx * unz]; for (int i = 0; i < under.Length; i++) under[i] = 1e3f;
            var belly = new float[unz]; for (int j = 0; j < unz; j++) belly[j] = 1e3f;
            // the track's inner edge in the Model's space
            float inX = float.MaxValue; for (int i = 0; i < pts.Count; i++) if (pts[i].track) inX = Mathf.Min(inX, Mathf.Abs(lp[i].x));
            if (!hasTracks || inX > 1e8f) inX = Mathf.Max(Mathf.Abs(x0), Mathf.Abs(x1)) * 0.55f;
            for (int i = 0; i < pts.Count; i++)
            {
                int a = Mathf.FloorToInt((lp[i].x - ux0) / UCell), b = Mathf.FloorToInt((lp[i].z - uz0) / UCell);
                if (a < 0 || b < 0 || a >= unx || b >= unz) continue;
                if (pts[i].track || !hasTracks) { int k = b * unx + a; under[k] = Mathf.Min(under[k], lp[i].y); }
                else if (Mathf.Abs(lp[i].x) < inX) belly[b] = Mathf.Min(belly[b], lp[i].y);
            }
            if (hasTracks)
            {   // the same in the Model's space: the belly line raised to the tracks' bottom along them, none beyond their ends
                var trackRow = new float[unz]; for (int j = 0; j < unz; j++) trackRow[j] = 1e3f;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (!pts[i].track) continue; int b = Mathf.FloorToInt((lp[i].z - uz0) / UCell);
                    if (b >= 0 && b < unz) trackRow[b] = Mathf.Min(trackRow[b], lp[i].y);
                }
                for (int b = 0; b < unz; b++) belly[b] = trackRow[b] > 999f ? 1e3f : Mathf.Max(belly[b] > 999f ? trackRow[b] + 0.36f : belly[b], trackRow[b]);
            }
            for (int b = 0; b < unz; b++)
            {
                if (belly[b] > 999f) continue;
                for (int a = 0; a < unx; a++) { float x = ux0 + (a + 0.5f) * UCell; if (Mathf.Abs(x) <= inX + 0.5f * UCell) { int k = b * unx + a; under[k] = Mathf.Min(under[k], belly[b]); } }
            }
            reach = 0f; var mp = model.position;
            for (int i = 0; i < pts.Count; i++) { var d = pts[i].w - mp; d.y = 0f; reach = Mathf.Max(reach, d.magnitude); }
        }

        static bool IsTrack(Transform t, Transform root)
        {
            for (; t && t != root; t = t.parent) { if (t.name.StartsWith("Track_") || t.name.StartsWith("TankWheel")) return true; }
            return false;
        }

        void SetRail(Rail r, Dictionary<int, float> bins, float l0, float l1)
        {
            r.ok = bins.Count >= 2 && l1 >= l0;
            if (!r.ok) return;
            var list = new List<Vector2>(bins.Count);
            foreach (var kv in bins) list.Add(new Vector2((kv.Key + 0.5f) * PStep, kv.Value));
            list.Sort((a, b) => a.x.CompareTo(b.x));
            r.pts = list.ToArray(); r.l0 = l0; r.l1 = l1; r.lc = 0.5f * (l0 + l1);
        }

        void FallbackRails()
        {
            float h = OzBizonCrushView.M(ZPathing.Radius(obj));
            foreach (var r in new[] { railL, railR })
            {
                r.pts = new[] { new Vector2(-h, 0f), new Vector2(0f, 0f), new Vector2(h, 0f) };
                r.l0 = r == railL ? -h * 0.6f : h * 0.3f; r.l1 = r == railL ? -h * 0.3f : h * 0.6f; r.lc = 0.5f * (r.l0 + r.l1); r.ok = true;
            }
            railM.ok = false; uFront = h; uRear = -h; halfW = h * 0.6f; reach = h * 1.2f;
            frontU = new[] { h, h, h, h, h, h, h, h };
            unx = unz = 0; under = new float[0];
        }

        // ------------------------------------------------------------------------------------------------ the support
        void Profiles(float s)
        {
            Profile(railL, s); Profile(railR, s); if (railM.ok) Profile(railM, s);
        }

        void Profile(Rail r, float s)
        {
            float a = r.pts[0].x - 3f, b = r.pts[r.pts.Length - 1].x + 3f;
            int n = Mathf.CeilToInt((b - a) / PStep) + 1;
            if (r.prof.Length != n) { r.prof = new float[n]; r.gprof = new float[n]; }
            r.pu0 = s + a;
            float frontAbs = s + uFront;
            for (int i = 0; i < n; i++)
            {
                float u = r.pu0 + i * PStep;
                var wc = origin + dir * u + lat * r.lc;
                float g = game.GroundY(wc.x, wc.z) - groundNow;
                float sup = g;
                if (squash)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float l = k == 0 ? r.l0 : k == 1 ? r.lc : r.l1;
                        float t = squash.SupportAt(origin + dir * u + lat * l, frontAbs);
                        if (t > float.MinValue) sup = Mathf.Max(sup, t + gyT - groundNow);
                    }
                }
                r.prof[i] = sup; r.gprof[i] = g;
            }
        }
        float gyT => targetView ? targetView.transform.position.y : gyB;

        static float At(float[] p, float u0, float u)
        {
            float f = (u - u0) / PStep; int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, p.Length - 2); float k = Mathf.Clamp01(f - i);
            return Mathf.Lerp(p[i], p[i + 1], k);
        }

        /// <summary>The lift a rail needs at pitch p (deg) with the Bizon at s: no point of it under its support (ground only = g).</summary>
        static float Need(Rail r, float s, float p, bool groundOnly)
        {
            float rad = p * Mathf.Deg2Rad, c = Mathf.Cos(rad), sn = Mathf.Sin(rad), need = -1e3f;
            var prof = groundOnly ? r.gprof : r.prof;
            for (int i = 0; i < r.pts.Length; i++)
            {
                var b = r.pts[i]; float u = s + b.x * c - b.y * sn, y = b.x * sn + b.y * c;
                float d = At(prof, r.pu0, u) - y; if (d > need) need = d;
            }
            return need;
        }

        /// <summary>The lift and roll (sine) that rest the rails on their needs: the tracks share the lift through the roll, the belly holds
        /// the middle.</summary>
        void LiftRoll(float nL, float nR, float nM, out float liftOut, out float sr)
        {
            float lL = railL.lc, lR = railR.lc, g = Mathf.Max(0.5f, lR - lL), smax = Mathf.Sin(MaxRoll * Mathf.Deg2Rad);
            sr = Mathf.Clamp((nR - nL) / g, -smax, smax);
            liftOut = Mathf.Max(nL - lL * sr, nR - lR * sr);
            if (railM.ok && nM > liftOut)
            {
                liftOut = nM;
                float lo = (nR - liftOut) / Mathf.Max(0.1f, lR), hi = (liftOut - nL) / Mathf.Max(0.1f, -lL);
                if (lo <= hi) sr = Mathf.Clamp(sr, lo, hi);
            }
        }

        float Height(float s, float p, out float rl, out float rr)
        {
            float nL = Need(railL, s, p, false), nR = Need(railR, s, p, false), nM = railM.ok ? Need(railM, s, p, false) : -1e3f;
            LiftRoll(nL, nR, nM, out rl, out rr);
            float r = p * Mathf.Deg2Rad;
            return rl + bcom.x * Mathf.Sin(r) + bcom.y * Mathf.Cos(r) + Hysteresis * Mathf.Abs(p - lastGoalP);
        }

        void Solve(float s, out float goalPitch, out float goalRollDeg, out float goalLift)
        {
            float best = float.MaxValue, bp = 0f;
            for (float p = -MaxPitch; p <= MaxPitch + 1e-3f; p += 1f) { float h = Height(s, p, out _, out _); if (h < best - 1e-6f) { best = h; bp = p; } }
            float lo = Mathf.Max(-MaxPitch, bp - 1f), hi = Mathf.Min(MaxPitch, bp + 1f);
            for (float p = lo; p <= hi + 1e-4f; p += 0.1f) { float h = Height(s, p, out _, out _); if (h < best - 1e-6f) { best = h; bp = p; } }
            Height(s, bp, out float lf, out float sr);
            goalPitch = bp; goalRollDeg = Mathf.Asin(Mathf.Clamp(sr, -1f, 1f)) * Mathf.Rad2Deg; goalLift = Mathf.Max(0f, lf);
            lastGoalP = bp;
        }

        // ------------------------------------------------------------------------------------------------ the death hold
        /// <summary>DeathWait: when the held target explodes (s from now): RollOffBlastS after the drawn landing (already landed: from then),
        /// at most MaxDeathWaitS; the same answer for CombatView, AudioDirector and BoardingView.</summary>
        float PlanDeath()
        {
            if (explodeAt > 0f) return Mathf.Max(0f, explodeAt - Time.time);
            heldAt = Time.time;
            explodeAt = landedAt >= 0f ? Mathf.Max(Time.time, landedAt + RollOffBlastS) : Time.time + MaxDeathWaitS;
            if (target != null)
            {
                if (dueByTarget.Count > 8) { var old = new List<int>(); foreach (var kv in dueByTarget) if (kv.Value < Time.time - 20f) old.Add(kv.Key); foreach (var k in old) dueByTarget.Remove(k); }
                dueByTarget[target.Id] = explodeAt;
            }
            DeathsHeld++;
            return Mathf.Max(0f, explodeAt - Time.time);
        }

        /// <summary>The Bizon's rear is off the wreck (the landing), or the ride ends: a held explosion comes RollOffBlastS from now (never later
        /// than planned).</summary>
        void RollOff()
        {
            if (explodeAt < 0f || explodeAt <= Time.time + RollOffBlastS) return;
            explodeAt = Time.time + RollOffBlastS;
            if (target != null) dueByTarget[target.Id] = explodeAt;
            RollOffs++; LastRollOffToBlast = RollOffBlastS;
            if (heldAt > 0f) MaxDeathWaitSeen = Mathf.Max(MaxDeathWaitSeen, explodeAt - heldAt);
        }

        internal void RestForSync()
        {
            if (begun && model) { model.localPosition = restPos; model.localRotation = restRot; }
        }

        // ------------------------------------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            if (!begun) return;
            if (!model || game == null) { Destroy(this); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) { Place(); return; }
            life += dt;
            float s = Vector3.Dot(transform.position - origin, dir); sNow = s;
            speedAlong = Mathf.Lerp(speedAlong, (s - sPrev) / dt, 1f - Mathf.Exp(-dt / 0.08f)); sPrev = s;
            groundNow = game.GroundY(transform.position.x, transform.position.z);
            bool alive = !ending && obj != null && !obj.Destroyed && view && !view.Dying && life < 12f;
            // the drawn touch: R17 chips and R20 jolt (View/OzRamImpactLook.cs)
            float frontU = s + uFront;
            if (!landed && frontU > near && touchAt < 0f && targetView)
            {
                touchAt = Time.time; var cp = origin + dir * near; cp.y = gyB + 0.6f;
                OzRamImpactLook.Contact(target, targetView, cp, transform.position, dir, 18f);
            }
            // the goal: the rigid body at rest on what is under its rails
            float tp = 0f, tr = 0f, tl = 0f;
            Profiles(s);
            if (alive) Solve(s, out tp, out tr, out tl);
            float a = 1f - Mathf.Exp(-dt / GoalTau);
            float oP = gP, oR = gR, oL = gL;
            gP += (tp - gP) * a; gR += (tr - gR) * a; gL += (tl - gL) * a;
            gPv = (gP - oP) / dt; gRv = (gR - oR) / dt; gLv = (gL - oL) / dt;
            if (ending) { gPv = gRv = gLv = 0f; }
            // pushed up by the support (the drawn pose would sink into it): the faster followers
            bool pushed = alive && CurrentNeed(s, fP, fR) > fL + 0.03f;
            hz = Mathf.Lerp(hz, pushed ? RiseHz : FollowHz, 1f - Mathf.Exp(-dt / 0.06f));
            int sub = Mathf.Clamp(Mathf.CeilToInt(dt * 2f * Mathf.PI * Mathf.Max(hz, WobbleHz) / 0.4f), 1, 8); float h = dt / sub;
            for (int i = 0; i < sub; i++)
            {
                float aP = Follow(ref fP, ref fPv, gP, gPv, hz, h), aR = Follow(ref fR, ref fRv, gR, gRv, hz, h), aL = Follow(ref fL, ref fLv, gL, gLv, hz, h);
                Wobble(ref wP, ref wPv, -WobbleK * aP, h); Wobble(ref wR, ref wRv, -WobbleK * aR, h); Wobble(ref wL, ref wLv, -LiftWobbleK * aL, h);
            }
            wP = Mathf.Clamp(wP, -MaxWobbleDeg, MaxWobbleDeg); wR = Mathf.Clamp(wR, -MaxWobbleDeg, MaxWobbleDeg); wL = Mathf.Clamp(wL, -MaxWobbleLift, MaxWobbleLift);
            pitch = Mathf.Clamp(fP + wP, -MaxPitch - 3f, MaxPitch + 3f); roll = Mathf.Clamp(fR + wR, -MaxRoll - 3f, MaxRoll + 3f); lift = Mathf.Max(0f, fL + wL);
            // never below the ground
            float ng = GroundNeed(s, pitch, roll);
            if (lift < ng) { float d = ng - lift; lift = ng; fL += d; if (fLv < 0f) fLv = 0f; }
            if (!rose && (Mathf.Abs(pitch) > 0.3f || lift > 0.03f)) { rose = true; LastRiseGap = near - frontU; }
            if (alive && tl > 0.05f) rode = true;
            // over the top: the nose tips down (a heavy clank), then it lands on the front (a thud)
            if (alive && rode && !crested && gP < -1f && crestFrom > 2f) { crested = true; Crests++; Clank(transform.position + dir * uFront * 0.5f, 0.6f, 0.62f); frontDown = true; }
            crestFrom = Mathf.Max(crestFrom, gP);
            if (frontDown && fPv < -10f) frontFalling = true;
            if (frontDown && frontFalling && fPv > -3f) { frontDown = false; Thud(transform.position + dir * uFront * 0.8f, 0.55f, 0.7f, 0.6f); }
            // the rear off the far face: the landing
            float rearU = s + uRear;
            if (rode && !landed && rearU > far + 0.05f)
            {
                landed = true; landedAt = Time.time; Landings++;
                if (touchAt > 0f) LastTouchToLand = landedAt - touchAt;
                Thud(transform.position + dir * uRear, 0.75f, 0.62f, 0.9f);
                RollOff();
            }
            if (landed && (obj == null || !obj.Crushing)) ending = true;
            if (!alive && !ending) { ending = true; RollOff(); }
            if (!alive) RollOff();
            Pitch = pitch; Lift = lift; Roll = roll; GoalPitch = gP;
            MaxPitchSeen = Mathf.Max(MaxPitchSeen, Mathf.Abs(pitch)); MaxLiftSeen = Mathf.Max(MaxLiftSeen, lift); MaxRollSeen = Mathf.Max(MaxRollSeen, Mathf.Abs(roll));
            if (Trace.Count < 1200) Trace.Add(new Vector4(Time.time, pitch, roll, lift));
            if (ending)
            {
                if (vmHold) vmHold.RideHoldTarget = 0f;
                endT += dt;
                bool settled = Mathf.Abs(pitch) < 0.05f && Mathf.Abs(roll) < 0.05f && lift < 0.005f && Mathf.Abs(fPv) < 0.5f && Mathf.Abs(wPv) < 0.5f;
                if (settled || endT > 3f) { RestoreNow(); Destroy(this); return; }
            }
            Place();
        }

        float CurrentNeed(float s, float p, float rDeg)
        {
            float nL = Need(railL, s, p, false), nR = Need(railR, s, p, false), nM = railM.ok ? Need(railM, s, p, false) : -1e3f;
            float sr = Mathf.Sin(rDeg * Mathf.Deg2Rad);
            return Mathf.Max(nL - railL.lc * sr, Mathf.Max(nR - railR.lc * sr, nM));
        }

        float GroundNeed(float s, float p, float rDeg)
        {
            float nL = Need(railL, s, p, true), nR = Need(railR, s, p, true), nM = railM.ok ? Need(railM, s, p, true) : -1e3f;
            float sr = Mathf.Sin(rDeg * Mathf.Deg2Rad);
            return Mathf.Max(nL - railL.lc * sr, Mathf.Max(nR - railR.lc * sr, nM));
        }

        /// <summary>A critically damped follower of a moving goal (its speed fed forward); returns its acceleration.</summary>
        static float Follow(ref float x, ref float v, float g, float gv, float hz, float h)
        {
            float w = 2f * Mathf.PI * hz, acc = w * w * (g - x) + 2f * w * (gv - v);
            v += acc * h; x += v * h; return acc;
        }

        /// <summary>The heavy wobble: a damped spring pushed by the hull's acceleration.</summary>
        static void Wobble(ref float x, ref float v, float push, float h)
        {
            float w = 2f * Mathf.PI * WobbleHz;
            v += (push - w * w * x - 2f * WobbleDamp * w * v) * h; x += v * h;
        }

        void Thud(Vector3 at, float vol, float pitchK, float dust)
        {
            at.y = game.GroundY(at.x, at.z) + 0.2f;
            AudioDirector.Fire("dst_turret_crash", at, obj != null ? obj.Id : 0, vol, pitchK); Thuds++;
            if (FxManager.I) FxManager.I.Dust(at, dust);
        }

        void Clank(Vector3 at, float vol, float pitchK)
        {
            at.y = game.GroundY(at.x, at.z) + 0.8f;
            AudioDirector.Fire("dst_debris_metal", at, obj != null ? obj.Id : 0, vol, pitchK);
        }

        /// <summary>The whole Model node turned about the ground point under the Bizon (pitch about its cross axis: + lifts the front; roll about
        /// the crush axis: + lifts its right side) and lifted: one rigid body.</summary>
        void Place()
        {
            var parent = model.parent;
            var wp = parent ? parent.TransformPoint(restPos) : restPos;
            var wr = parent ? parent.rotation * restRot : restRot;
            var c = transform.position; c.y = groundNow;
            var q = Quaternion.AngleAxis(pitch, Vector3.Cross(dir, Vector3.up)) * Quaternion.AngleAxis(roll, dir);
            model.SetPositionAndRotation(c + q * (wp - c) + Vector3.up * lift, q * wr);
        }

        /// <summary>Back to the model's own pose at once (a new crush replaces this ride: its Begin must read the rest pose).</summary>
        public void RestoreNow()
        {
            if (model) { model.localPosition = restPos; model.localRotation = restRot; }
            begun = false;
        }

        void OnDestroy()
        {
            RollOff();
            if (vmHold) vmHold.RideHoldTarget = 0f;
            if (begun) RestoreNow();
            if (target != null && byTarget.TryGetValue(target.Id, out var r) && r == this) byTarget.Remove(target.Id);
            if (rest && rest.ride == this) Destroy(rest);
        }
    }

    /// <summary>1.5 kit crush_swamp_fix: puts the crushing Bizon's Model node back on its rest pose before UnitView syncs the tank this frame
    /// (UnitView runs at 60), so the planted running gear is placed under the level hull; OzCrushRide turns the whole node after the sync.</summary>
    [DefaultExecutionOrder(-900)]
    public class OzCrushRideRest : MonoBehaviour
    {
        internal OzCrushRide ride;

        void Update()
        {
            if (!ride) { Destroy(this); return; }
            ride.RestForSync();
        }
    }
}
