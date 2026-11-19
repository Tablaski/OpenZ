// OpenZ (GPL-3.0) - version 1.5, round 6 Unity pass B (14 Nov 2026), the lead's task on FAILS A13 after kit wreck_rest_ground_r6's check
// ('Wreck Rest Ground', the pile at night: thrown turrets of Bizon / Mammoth rested 0.38-1.74 m in the air for 30-40 s, as in test exe 14b).
// View only: Scripts/Sim is not touched and nothing here is read by the rules.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>A thrown turret (Zod's turret toss, UnitView.TossedPiece) rests only where something holds it up, like the hulls and pieces of
    /// kit wreck_rest_ground_r6 (View/OzWreckGround.cs: the same measure, <see cref="OzWreckGround.PieceSupported"/>, within
    /// <see cref="OzWreckGround.TolM"/>). A landed turret is looked at every <see cref="LookEveryS"/>; when what it lay on has gone (a hull that
    /// broke up or sank, a pile that moved) it falls straight down at 1 g from rest, turning onto its nearest face during the fall (no spin), and
    /// lands with dust and a clank when the landing is hard. A turret in water or lava is the water / lava rule's. Off with
    /// <see cref="On"/> or with OzWreckGround.On / .Hulls (then exactly test exe 14b).</summary>
    public sealed class OzWreckTurretRest : MonoBehaviour
    {
        public static bool On = true;
        public static float LookEveryS = 0.3f;
        public static int Falls, Landings, Thumps;
        public static float MaxFallM;
        public static int PointFalls;                                                                           // pass C (15 Nov): turrets the box measure called held but whose lowest points all hung over their support
        public static float PointRecheckS = 1.0f;                                                               // pass C: an unmoved turret that passed the point measure is measured again after this (its support may go)

        sealed class Fall { public Transform t; public float vy, turnLeft; public Quaternion target; public bool flatten, points; public int unitId; }
        sealed class Seen { public Vector3 p; public Quaternion r; public float at; }
        readonly Dictionary<Transform, Seen> passed = new Dictionary<Transform, Seen>();

        // pass C (15 Nov, FAILS A13): the pile check (View/OzWreckGroundCheck.cs Probe.Gap) measures a thrown turret by its 32 lowest drawn solid points,
        // each against the support right under it; the box measure (OzWreckGround.PieceSupported: the support under the box middle / 4 footprint
        // A landed turret the box measure calls held is measured that way too; hanging, it comes straight down until its first point touches.
        static readonly List<Renderer> prs = new List<Renderer>();
        static readonly List<Vector3> pvb = new List<Vector3>();
        static readonly Dictionary<Mesh, Vector3[]> pick = new Dictionary<Mesh, Vector3[]>();
        static readonly Vector3[] plow = new Vector3[32];

        static bool PointSolid(Renderer r)
        {
            if (!r || !r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) return false;
            if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450) return false;
            var n = r.name.ToLowerInvariant();
            if (n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring") || n.Contains("marker") || n.Contains("decal") || OzWreckGround.FxName(n)) return false;
            return !(n.Contains("lamp") || n.Contains("beacon") || n.Contains("neon") || n.Contains("glow") || n.Contains("halo") || n.Contains("lens") || n.Contains("bulb") || n.Contains("beam") || n.Contains("cone"));
        }

        static Vector3[] Picked(Mesh m)
        {
            if (pick.TryGetValue(m, out var a)) return a;
            pvb.Clear(); m.GetVertices(pvb); int st = Mathf.Max(1, Mathf.CeilToInt(pvb.Count / 600f));
            var l = new List<Vector3>(); for (int i = 0; i < pvb.Count; i += st) l.Add(pvb[i]);
            a = l.ToArray(); pick[m] = a; return a;
        }

        /// <summary>The smallest gap of its 32 lowest drawn solid points to the support right under each (the pile check's own measure); NaN = nothing
        /// drawn or nothing under it.</summary>
        public static float PointGap(Transform t)
        {
            if (!t) return float.NaN;
            prs.Clear(); t.GetComponentsInChildren(false, prs);
            int n = 0;
            void Add(Vector3 w)
            {
                if (n < plow.Length) { plow[n++] = w; return; }
                int hi = 0; for (int k = 1; k < n; k++) if (plow[k].y > plow[hi].y) hi = k;
                if (w.y < plow[hi].y) plow[hi] = w;
            }
            foreach (var r in prs)
            {
                if (!PointSolid(r)) continue;
                Mesh m = null; if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf)) m = mf.sharedMesh;
                if (m && m.isReadable && m.vertexCount > 0) { var mt = r.localToWorldMatrix; foreach (var p in Picked(m)) Add(mt.MultiplyPoint3x4(p)); }
                else { var b = r.bounds; for (int c = 0; c < 8; c++) Add(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))); }
            }
            float gap = float.MaxValue;
            for (int i = 0; i < n; i++) { float s = OzWreckGround.Under(plow[i], t); if (s > float.MinValue) gap = Mathf.Min(gap, plow[i].y - s); }
            return gap == float.MaxValue ? float.NaN : gap;
        }
        readonly List<Fall> falling = new List<Fall>();
        readonly HashSet<Transform> busy = new HashSet<Transform>();
        float nextLook;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("OzWreckTurretRest");
            DontDestroyOnLoad(go);
            go.AddComponent<OzWreckTurretRest>();
        }

        public static string Status() => $"thrown turrets rest on support {(On && OzWreckGround.On && OzWreckGround.Hulls ? "on" : "off")}: falls {Falls} (max {MaxFallM:0.00} m), landings {Landings}, hard landings heard {Thumps}, point-measure falls {PointFalls}";   // pass C: + point-measure falls

        void LateUpdate()
        {
            if (!On || !OzWreckGround.On || !OzWreckGround.Hulls) { if (falling.Count > 0) { falling.Clear(); busy.Clear(); } return; }
            float now = Time.time, dt = Time.deltaTime;
            if (now >= nextLook && ZGame.I)
            {
                nextLook = now + LookEveryS;
                if (passed.Count > 256) { var gone = new List<Transform>(); foreach (var k in passed.Keys) if (!k) gone.Add(k); foreach (var k in gone) passed.Remove(k); }   // pass C: turrets taken away
                foreach (var v in FindObjectsByType<UnitView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!v || !v.TossedLanded) continue;
                    var t = v.TossedPiece;
                    if (!t || !t.gameObject.activeInHierarchy || busy.Contains(t)) continue;
                    if (OzWaterLanding.InWater(t) || OzLavaMelt.Busy(t)) continue;
                    if (OzWreckGround.PieceSupported(t, out float gap, out bool water) || water)
                    {   // pass C: held by the box measure; its lowest points must touch too (the pile check's measure), else it comes straight down
                        if (water) continue;
                        if (passed.TryGetValue(t, out var sn) && now - sn.at < PointRecheckS && (t.position - sn.p).sqrMagnitude < 1e-4f && Quaternion.Angle(t.rotation, sn.r) < 0.5f) continue;
                        float pg = PointGap(t);
                        if (float.IsNaN(pg) || pg <= OzWreckGround.TolM) { passed[t] = new Seen { p = t.position, r = t.rotation, at = now }; continue; }
                        PointFalls++; passed.Remove(t); Begin(t, pg, v, true); continue;
                    }
                    passed.Remove(t);
                    Begin(t, gap, v, false);
                }
            }
            if (dt <= 0f) return;
            for (int i = falling.Count - 1; i >= 0; i--)
                if (!Step(falling[i], dt)) { busy.Remove(falling[i].t); falling.RemoveAt(i); }
        }

        void Begin(Transform t, float gap, UnitView v, bool points)
        {
            var f = new Fall { t = t, unitId = v.Obj != null ? v.Obj.Id : 0, points = points };   // pass C: points = it lies as it is and comes straight down onto its first point
            f.flatten = !points && gap > OzWreckGround.FlattenFromM;
            if (f.flatten)
            {   // its nearest face down: of its six box faces the one most down now (as OzWreckDrop does for a piece)
                Vector3 best = -t.up; float bd = Vector3.Dot(best, Vector3.down);
                foreach (var ax in new[] { t.up, t.right, -t.right, t.forward, -t.forward })
                { float d = Vector3.Dot(ax, Vector3.down); if (d > bd) { bd = d; best = ax; } }
                f.target = Quaternion.FromToRotation(best, Vector3.down) * t.rotation;
                f.turnLeft = Quaternion.Angle(t.rotation, f.target);
                if (f.turnLeft < 1f) f.flatten = false;
            }
            falling.Add(f); busy.Add(t);
            Falls++; MaxFallM = Mathf.Max(MaxFallM, gap);
        }

        /// <summary>One frame of a turret's fall; false = it lies on its support (or it is gone).</summary>
        bool Step(Fall f, float dt)
        {
            var t = f.t;
            if (!t || !t.gameObject.activeInHierarchy) return false;
            if (f.points)
            {   // pass C: straight down at 1 g until its first low point touches the support under it (the pile check's own measure)
                float pg = PointGap(t);
                if (float.IsNaN(pg) || pg <= 1e-3f) return false;
                f.vy += 9.81f * dt;
                float st = Mathf.Min(pg, f.vy * dt);
                t.position += Vector3.down * st;
                if (st < pg - 1e-3f) return true;
                Landings++; f.vy = 0f; return false;
            }
            if (!OzWreckGround.Lowest(t, out float low, out var b)) return false;
            float support = OzWreckGround.SupportUnder(b, low, t);
            float gap = support == float.MinValue ? 0f : low - support;
            if (f.flatten && f.turnLeft > 0.01f)
            {   // the turn onto its face, spread over what is left of the fall (it lands lying)
                float timeLeft = gap > 0.01f ? Mathf.Max(0.12f, (-f.vy + Mathf.Sqrt(f.vy * f.vy + 2f * 9.81f * gap)) / 9.81f) : 0.12f;
                var c = b.center;
                t.rotation = Quaternion.RotateTowards(t.rotation, f.target, Mathf.Min(f.turnLeft, f.turnLeft * dt / timeLeft + 0.01f));
                if (OzWreckGround.Box(t, out var nb)) t.position += c - nb.center;                                  // about its middle
                f.turnLeft = Quaternion.Angle(t.rotation, f.target);
                if (!OzWreckGround.Lowest(t, out low, out b)) return false;
                support = OzWreckGround.SupportUnder(b, low, t);
                gap = support == float.MinValue ? 0f : low - support;
            }
            if (gap > 1e-3f)
            {   // it falls: 1 g
                f.vy += 9.81f * dt;
                float step = Mathf.Min(gap, f.vy * dt);
                t.position += Vector3.down * step;
                if (step < gap - 1e-3f) return true;
                Landings++;
                if (f.vy > 2.5f)
                {   // a hard landing: dust and a metal clank, on screen only
                    var at = new Vector3(b.center.x, support + 0.1f, b.center.z);
                    if (WreckFx.OnScreen(at, 0.1f)) { Thumps++; FxManager.I?.Dust(at, Mathf.Clamp(f.vy / 10f, 0.25f, 0.6f)); AudioDirector.Fire("dst_debris_metal", at, f.unitId, Mathf.Clamp(f.vy / 9f, 0.35f, 0.85f), 1.05f); }
                }
                f.vy = 0f;
            }
            else if (gap < -1e-3f && f.flatten && f.turnLeft > 0.01f) t.position += Vector3.up * Mathf.Min(-gap, 3f * dt);   // the turn pushed a corner under its support: back up onto it
            return f.flatten && f.turnLeft > 0.01f;
        }
    }
}
