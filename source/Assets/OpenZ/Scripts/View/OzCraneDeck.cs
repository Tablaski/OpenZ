// OpenZ (GPL-3.0) - version 1.5 kit factory_restore_crane_r3: a crane repairing a destroyed bridge stands ON its standing deck (view only).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The rules send a repairing crane from the bridge's end to its MIDDLE and back (Zod's GetCraneCenter, brobot / bbridge.cpp). A destroyed
    /// 3D bridge has no walk deck (ZGame.MakeWalkable builds one only for the intact and the damaged model), so the drawn crane took the river
    /// bed's height: it sank into the standing stump, and in the middle the deck is gone (level 18's long bridges: 10 m of fallen deck).
    /// VIEW ONLY (no rule, no timing, the crane's place in the rules unchanged):
    /// <list type="bullet">
    /// <item>height: over a destroyed bridge's STANDING deck (measured once per bridge on the destroyed model: up faces at the intact deck's
    /// height, bin by bin from each end) the crane's five ground samples read the intact bridge's own walk surface (its deck and end ramps,
    /// the WalkDeck of the intact model), so it drives up the ramp and stands on the deck;</item>
    /// <item>place: the drawn crane stops on the deck with its front <see cref="EdgeClear"/> m before the broken edge (the end it came from)
    /// and works from there (its arm reaches over the gap), kept whole on the deck across; while the rules' crane drives on to the middle and
    /// back it waits there (no wheel turn), then follows it out. A sudden end of the hold (the bridge repaired meanwhile) eases back at
    /// 3 m/s, never a jump.</item>
    /// </list>
    /// Only cranes (no other unit stands on a destroyed bridge: robots wade through the river as before).
    /// </summary>
    public static class OzCraneDeck
    {
        public static bool On = true;
        /// <summary>m kept between the crane's front and the broken edge of the standing deck.</summary>
        public static float EdgeClear = 0.6f;
        /// <summary>A 0.5 m bin of the destroyed model counts as standing deck from this share of up faces at deck height.</summary>
        public static float MinCover = 0.45f;
        /// <summary>m/s the drawn crane catches up with the rules' place after a hold ends at once.</summary>
        public static float Relax = 3f;

        public static int Measured, HeldFrames, DeckFrames, Holds;
        public static string Last = "";

        public sealed class Deck
        {
            public int id; public bool ok; public string why = "";
            public Vector3 centre, axis, axisB; public float bMid, halfW;
            public float a0, a1, stumpA, stumpB, deckY;          // the deck's ends (m along from the centre), standing deck from each end (m), deck height
            public float[] sa = new float[4], sy = new float[4]; // the walk surface along the axis: ramp start, deck start, deck end, ramp end
            public float Y(float a)
            {
                if (a <= sa[0]) return sy[0];
                for (int i = 1; i < 4; i++) if (a <= sa[i]) return Mathf.Lerp(sy[i - 1], sy[i], Mathf.InverseLerp(sa[i - 1], sa[i], a));
                return sy[3];
            }
            /// <summary>This point (along, across from the deck's middle line) lies on the standing deck or on the ramp up to it.</summary>
            public bool Standing(float a, float b)
            {
                if (!ok || Mathf.Abs(b - bMid) > halfW + 0.05f) return false;
                return (stumpA > 0.05f && a >= sa[0] && a <= a0 + stumpA) || (stumpB > 0.05f && a <= sa[3] && a >= a1 - stumpB);
            }
            public float Along(Vector3 w) => Vector3.Dot(w - centre, axis);
            public float Across(Vector3 w) => Vector3.Dot(w - centre, axisB);
        }

        static readonly Dictionary<int, Deck> decks = new Dictionary<int, Deck>();
        static readonly Dictionary<int, int> craneEnd = new Dictionary<int, int>();          // crane -> the end it works from (-1 = a0, +1 = a1)
        static readonly Dictionary<int, Vector2> craneSize = new Dictionary<int, Vector2>();  // crane -> half length, half width (m)
        static readonly Dictionary<int, Vector3> craneOff = new Dictionary<int, Vector3>();   // crane -> drawn minus rules' place (m)
        static readonly Dictionary<int, float> craneT = new Dictionary<int, float>();
        static readonly List<ZObject> bridges = new List<ZObject>();
        static ZWorld world; static bool scanned; static RulesView rv;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            On = true; EdgeClear = 0.6f; MinCover = 0.45f; Relax = 3f;
            Measured = HeldFrames = DeckFrames = Holds = 0; Last = "";
            decks.Clear(); craneEnd.Clear(); craneSize.Clear(); craneOff.Clear(); craneT.Clear(); bridges.Clear(); world = null; scanned = false; rv = null;
        }

        static void Scan(ZGame g)
        {
            if (world == g.World && scanned) return;
            world = g.World; scanned = true; bridges.Clear(); decks.Clear(); craneEnd.Clear(); craneOff.Clear(); craneT.Clear();
            foreach (var o in g.World.Objects) if (o.IsBridge) bridges.Add(o);
            rv = g.GetComponent<RulesView>(); if (!rv) rv = Object.FindFirstObjectByType<RulesView>();
        }

        /// <summary>The destroyed bridge whose deck region (its ramps and `margin` m round it) holds this point, measured; null if none.</summary>
        public static Deck Under(ZGame g, Vector3 p, float margin)
        {
            if (g == null || g.World == null) return null;
            Scan(g);
            foreach (var b in bridges)
            {
                if (!b.Destroyed) continue;
                var c = g.CenterWorld(b, false);
                float hx = b.WidthPix / 16f + margin + 3f, hz = b.HeightPix / 16f + margin + 3f;   // px / 8 = m; half = / 16; + the ramps
                if (Mathf.Abs(p.x - c.x) > hx || Mathf.Abs(p.z - c.z) > hz) continue;
                var d = Get(g, b);
                if (d == null || !d.ok) continue;
                float a = d.Along(p), bb = d.Across(p);
                if (a < d.sa[0] - margin || a > d.sa[3] + margin || Mathf.Abs(bb - d.bMid) > d.halfW + margin) continue;
                return d;
            }
            return null;
        }

        /// <summary>The bridge's measured deck (once per bridge; the destroyed model never changes).</summary>
        public static Deck Get(ZGame g, ZObject b)
        {
            if (decks.TryGetValue(b.Id, out var d)) return d;
            d = Measure(g, b); decks[b.Id] = d; Measured++;
            Last = $"bridge #{b.Id} {b.Type}: {(d.ok ? $"deck {d.deckY:0.00} m, ends {d.a0:0.0} / {d.a1:0.0} m, half width {d.halfW:0.0} m, standing deck {d.stumpA:0.0} m + {d.stumpB:0.0} m" : "not measured: " + d.why)}";
            Debug.Log("[OpenZ] crane deck: " + Last);
            return d;
        }

        static Deck Measure(ZGame g, ZObject b)
        {
            var d = new Deck { id = b.Id };
            var v = rv ? rv.BridgeModels(b) : null;
            var intact = v != null && v.Length > 0 ? v[0] : null; var wreck = v != null && v.Length > 2 ? v[2] : null;
            if (!intact || !wreck || intact == wreck) { d.why = "no intact / destroyed model pair"; return d; }
            d.centre = g.CenterWorld(b, false); d.centre.y = 0f;
            bool alongX = b.WidthPix >= b.HeightPix;
            d.axis = alongX ? Vector3.right : Vector3.forward; d.axisB = alongX ? Vector3.forward : Vector3.right;
            // the intact bridge's walk surface (ZGame.MakeWalkable: a flat deck at the most common top height + a ramp at each end)
            var wd = intact.transform.Find("WalkDeck"); var mc = wd ? wd.GetComponent<MeshCollider>() : null;
            if (mc && mc.sharedMesh && mc.sharedMesh.vertexCount == 8)
            {
                var M = wd.localToWorldMatrix; var pts = new List<(float a, float y, float b)>();
                foreach (var p in mc.sharedMesh.vertices) { var w = M.MultiplyPoint3x4(p); pts.Add((d.Along(w), w.y, d.Across(w))); }
                pts.Sort((x, y) => x.a.CompareTo(y.a));
                float bmin = float.MaxValue, bmax = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    var p0 = pts[i * 2]; var p1 = pts[i * 2 + 1];
                    d.sa[i] = (p0.a + p1.a) * 0.5f; d.sy[i] = (p0.y + p1.y) * 0.5f;
                    bmin = Mathf.Min(bmin, Mathf.Min(p0.b, p1.b)); bmax = Mathf.Max(bmax, Mathf.Max(p0.b, p1.b));
                }
                d.a0 = d.sa[1]; d.a1 = d.sa[2]; d.deckY = (d.sy[1] + d.sy[2]) * 0.5f; d.bMid = (bmin + bmax) * 0.5f; d.halfW = (bmax - bmin) * 0.5f;
            }
            else if (!FromMesh(g, intact, d)) { d.why = "the intact model has no walk deck and no readable deck faces"; return d; }
            // the standing deck of the destroyed model: up faces at the deck height, 0.5 m bins from each end
            int nb = Mathf.Max(1, Mathf.CeilToInt((d.a1 - d.a0) / 0.5f));
            var cover = new float[nb]; bool readable = false;
            foreach (var mf in wreck.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>(); var m = mf.sharedMesh;
                if (!r || !m || !m.isReadable) continue;                                                // its renderers may be hidden for a moment (the restore's wreck light): the model counts
                readable = true;
                var M = mf.transform.localToWorldMatrix; var vs = m.vertices;
                var w = new Vector3[vs.Length]; for (int i = 0; i < vs.Length; i++) w[i] = M.MultiplyPoint3x4(vs[i]);
                bool flip = M.determinant < 0f;
                for (int sm = 0; sm < m.subMeshCount; sm++)
                {
                    if (m.GetTopology(sm) != MeshTopology.Triangles) continue;
                    var t = m.GetTriangles(sm);
                    for (int k = 0; k + 2 < t.Length; k += 3)
                    {
                        var A = w[t[k]]; var B = w[t[k + (flip ? 2 : 1)]]; var C = w[t[k + (flip ? 1 : 2)]];
                        var n = Vector3.Cross(B - A, C - A); float area2 = n.magnitude; if (area2 < 1e-6f) continue;
                        float up = Mathf.Abs(n.y) / area2; if (up < 0.8f) continue;                      // flat faces (either winding: some deck plates are two-sided)
                        float yc = (A.y + B.y + C.y) / 3f; if (yc < d.deckY - 0.3f || yc > d.deckY + 0.3f) continue;
                        float bc = (d.Across(A) + d.Across(B) + d.Across(C)) / 3f; if (Mathf.Abs(bc - d.bMid) > d.halfW) continue;
                        float la = d.Along(A), lb = d.Along(B), lc = d.Along(C);
                        float lo = Mathf.Min(la, Mathf.Min(lb, lc)), hi = Mathf.Max(la, Mathf.Max(lb, lc)), area = area2 * 0.5f;
                        int i0 = Mathf.Clamp(Mathf.FloorToInt((lo - d.a0) / 0.5f), 0, nb - 1), i1 = Mathf.Clamp(Mathf.FloorToInt((hi - d.a0) / 0.5f), 0, nb - 1);
                        for (int i = i0; i <= i1; i++)
                        {
                            float s0 = d.a0 + i * 0.5f, s1 = s0 + 0.5f;
                            float share = hi - lo < 1e-4f ? 1f : Mathf.Max(0f, Mathf.Min(hi, s1) - Mathf.Max(lo, s0)) / (hi - lo);
                            cover[i] += area * share;
                        }
                    }
                }
            }
            if (!readable) { d.why = "the destroyed model's meshes are not readable"; d.ok = true; return d; }   // no standing deck known: the crane stays at the deck's end
            float full = 0.5f * 2f * Mathf.Max(0.5f, d.halfW);
            int ia = 0; while (ia < nb && (cover[ia] / full >= MinCover || (ia + 1 < nb && cover[ia + 1] / full >= MinCover && ia > 0))) ia++;
            int ib = 0; while (ib < nb && (cover[nb - 1 - ib] / full >= MinCover || (ib + 1 < nb && cover[nb - 2 - ib] / full >= MinCover && ib > 0))) ib++;
            d.stumpA = Mathf.Min(ia * 0.5f, d.a1 - d.a0); d.stumpB = Mathf.Min(ib * 0.5f, d.a1 - d.a0 - d.stumpA);
            d.ok = true;
            return d;
        }

        /// <summary>No WalkDeck on the intact model (it never stood in this game): the same surface from its mesh (the most common up-face
        /// height, its extent, ramps of ZGame.MakeWalkable's length down to the terrain).</summary>
        static bool FromMesh(ZGame g, GameObject intact, Deck d)
        {
            var hist = new Dictionary<int, float>(); var faces = new List<(float y, float a0, float a1, float b0, float b1)>();
            foreach (var mf in intact.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (!m || !m.isReadable) continue;
                var M = mf.transform.localToWorldMatrix; var vs = m.vertices; var w = new Vector3[vs.Length];
                for (int i = 0; i < vs.Length; i++) w[i] = M.MultiplyPoint3x4(vs[i]);
                for (int sm = 0; sm < m.subMeshCount; sm++)
                {
                    if (m.GetTopology(sm) != MeshTopology.Triangles) continue;
                    var t = m.GetTriangles(sm);
                    for (int k = 0; k + 2 < t.Length; k += 3)
                    {
                        var A = w[t[k]]; var B = w[t[k + 1]]; var C = w[t[k + 2]];
                        var n = Vector3.Cross(B - A, C - A); float a2 = n.magnitude; if (a2 < 1e-6f || Mathf.Abs(n.y) / a2 < 0.85f) continue;
                        float yc = (A.y + B.y + C.y) / 3f; int key = Mathf.RoundToInt(yc * 20f);
                        hist[key] = (hist.TryGetValue(key, out var h) ? h : 0f) + a2 * 0.5f;
                        float la = d.Along(A), lb = d.Along(B), lc = d.Along(C), xa = d.Across(A), xb = d.Across(B), xc = d.Across(C);
                        faces.Add((yc, Mathf.Min(la, Mathf.Min(lb, lc)), Mathf.Max(la, Mathf.Max(lb, lc)), Mathf.Min(xa, Mathf.Min(xb, xc)), Mathf.Max(xa, Mathf.Max(xb, xc))));
                    }
                }
            }
            if (hist.Count == 0) return false;
            int best = 0; float bestA = -1f; foreach (var kv in hist) if (kv.Value > bestA) { bestA = kv.Value; best = kv.Key; }
            d.deckY = best / 20f;
            float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
            foreach (var f in faces) if (Mathf.Abs(f.y - d.deckY) < 0.12f) { a0 = Mathf.Min(a0, f.a0); a1 = Mathf.Max(a1, f.a1); b0 = Mathf.Min(b0, f.b0); b1 = Mathf.Max(b1, f.b1); }
            if (a1 <= a0) return false;
            float ramp = g.level == "L02" ? 1.1f : 2f;
            d.a0 = a0; d.a1 = a1; d.bMid = (b0 + b1) * 0.5f; d.halfW = (b1 - b0) * 0.5f;
            Vector3 At(float a) => d.centre + d.axis * a + d.axisB * d.bMid;
            d.sa[0] = a0 - ramp; d.sa[1] = a0; d.sa[2] = a1; d.sa[3] = a1 + ramp;
            var r0 = At(d.sa[0]); var r3 = At(d.sa[3]);
            d.sy[0] = g.TerrainY(r0.x, r0.z); d.sy[1] = d.sy[2] = d.deckY; d.sy[3] = g.TerrainY(r3.x, r3.z);
            return true;
        }

        static Vector2 Size(UnitView v, ZObject u)
        {
            if (craneSize.TryGetValue(u.Id, out var s)) return s;
            float fl = 0f, fw = 0f; var f = v.transform.forward; f.y = 0f; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward; var rt = Vector3.Cross(Vector3.up, f);
            foreach (var r in v.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var lb = mf.sharedMesh.bounds; var M = r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = M.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))) - v.transform.position;
                    fl = Mathf.Max(fl, Mathf.Abs(Vector3.Dot(c, f))); fw = Mathf.Max(fw, Mathf.Abs(Vector3.Dot(c, rt)));
                }
            }
            s = new Vector2(Mathf.Clamp(Mathf.Max(fl, fw), 1.5f, 4.5f), Mathf.Clamp(Mathf.Min(fl, fw), 1f, 2.2f));   // longer side = its length (whatever way the model was built)
            craneSize[u.Id] = s;
            return s;
        }

        /// <summary>UnitView.Sync, right after the rules' place: a crane on (or driving onto) a destroyed bridge is drawn on its standing deck.
        /// True = it is held at the deck's broken edge this frame (it stands: no wheel turn, no dust).</summary>
        public static bool Hold(UnitView v, ZObject u, ref Vector3 p)
        {
            if (!On || v == null || u == null || u.Type != "crane" || u.Destroyed) return false;
            var g = ZGame.I; if (g == null || g.World == null) return false;
            var sim = p; bool held = false;
            var d = Under(g, p, 4f);
            float a = d != null ? d.Along(p) : 0f, bb = d != null ? d.Across(p) : 0f;
            bool job = d != null && u.Waypoints.Count > 0 && u.Waypoints[0].mode == WpMode.CraneRepair && u.Waypoints[0].refId == d.id;
            if (d != null && !job && !(a >= d.a0 - 0.5f && a <= d.a1 + 0.5f && Mathf.Abs(bb - d.bMid) <= d.halfW)) d = null;   // only its repair job (or a crane already on it): a crane passing the bridge's mouth is never pulled
            if (d != null)
            {
                var hs = Size(v, u);
                if (!craneEnd.TryGetValue(u.Id, out int end)) { end = a < (d.a0 + d.a1) * 0.5f ? -1 : 1; craneEnd[u.Id] = end; Holds++; }
                float lim = end < 0 ? d.a0 + d.stumpA - hs.x - EdgeClear : d.a1 - d.stumpB + hs.x + EdgeClear;
                if (end < 0 ? a > lim : a < lim) { p += d.axis * (lim - a); a = lim; held = true; }
                // whole on the deck across (eased in along the ramp)
                float wl = Mathf.Max(0f, d.halfW - hs.y - 0.15f), rel = bb - d.bMid;
                float into = end < 0 ? Mathf.InverseLerp(d.sa[0], d.a0, a) : Mathf.InverseLerp(d.sa[3], d.a1, a);
                if (Mathf.Abs(rel) > wl && into > 0f) p += d.axisB * ((Mathf.Clamp(rel, -wl, wl) - rel) * Mathf.SmoothStep(0f, 1f, into));
            }
            else craneEnd.Remove(u.Id);
            // the drawn place eases back to the rules' one after a hold that ended at once (never a jump)
            var off = p - sim; off.y = 0f;
            float dt = Time.deltaTime;
            if (craneOff.TryGetValue(u.Id, out var was) && !held && off.sqrMagnitude < was.sqrMagnitude)
            {
                var eased = Vector3.MoveTowards(was, off, Relax * Mathf.Max(dt, 0f));
                p = sim + eased; off = eased;
            }
            if (off.sqrMagnitude > 1e-6f) craneOff[u.Id] = off; else craneOff.Remove(u.Id);
            if (held) HeldFrames++;
            return held;
        }

        /// <summary>UnitView.Sync, the vehicle's five ground samples: over a destroyed bridge's standing deck (or the ramp up to it) a crane reads
        /// the deck, not the river bed under it.</summary>
        public static void Samples(UnitView v, Vector3 p, Vector3 fwd, Vector3 right, ref float hc, ref float hf, ref float hb, ref float hr, ref float hl)
        {
            if (!On || v == null || v.Obj == null || v.Obj.Type != "crane") return;
            var g = ZGame.I; var d = Under(g, p, 4f); if (d == null) return;
            if (S(d, p, ref hc)) DeckFrames++;
            S(d, p + fwd * 1.5f, ref hf); S(d, p - fwd * 1.5f, ref hb); S(d, p + right * 1.1f, ref hr); S(d, p - right * 1.1f, ref hl);
        }

        static bool S(Deck d, Vector3 w, ref float h)
        {
            float a = d.Along(w), b = d.Across(w);
            if (!d.Standing(a, b)) return false;
            h = Mathf.Max(h, d.Y(a));
            return true;
        }

        public static string Status() => $"crane deck {(On ? "on" : "off")}: bridges measured {Measured}, holds {Holds}, frames held {HeldFrames}, frames on a standing deck {DeckFrames}; last: {(Last.Length > 0 ? Last : "-")}";
    }
}
