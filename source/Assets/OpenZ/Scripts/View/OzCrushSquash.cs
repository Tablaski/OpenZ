using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// destroy it). Replaces the old whole-model flat scale.
    /// <list type="bullet">
    /// <item>DENTS WHERE THE BIZON PRESSES. The target's own meshes (a runtime copy per part, only this target, only from the first press) are
    /// pushed down only where the Bizon's real underside (its two tracks and its belly between them, OzCrushRide.UnderAt, measured from its
    /// drawn meshes) comes down on them this frame. A grid over the target's footprint (<see cref="CellM"/>) keeps the lowest the underside has
    /// been over each spot (the dent stays: it never springs back), so the pressed band grows front to back with the climb. The press has a
    /// soft edge (<see cref="FalloffSlope"/>: the panels beside a track bend down toward it) and never goes below the hull's own strength
    /// (<see cref="FloorK"/> x its crushed height: the hull keeps about half its height under the tracks).</item>
    /// <item>A WRECK, NOT A SHEET. In a pressed column only the upper part folds (the lower <see cref="KeepLow"/> of the pressed height keeps its
    /// shape: tracks, wheels, the hull's lower walls); the folded layer gets uneven dents (two noise scales, <see cref="DentM"/>) and the walls
    /// bulge out (<see cref="BulgeM"/>), so it keeps its volume and reads as crumpled armour; the normals follow the dents. A few small parts
    /// (antennas, lamps, ladder rails, boxes) are torn off and thrown aside when the track reaches them (<see cref="MaxPiecesOff"/>).</item>
    /// <item>THE GUN NEVER SINKS IN. Each gun barrel bends down at its mantlet and kinks (two thirds of the bend at the mantlet) as far as the
    /// Bizon's underside forces it, never below the ground or the hull under it; where a track then rolls over it, it is flattened to half its
    /// thickness.</item>
    /// <item>THE TURRET FALLS OFF LATER. The turret stays on until the Bizon's underside or front actually reaches it; then it is torn off its
    /// ring (a metal tear, sparks), shoved ahead of the Bizon's front and aside by it, tilts, slides off the hull, falls, lands (a crash and
    /// dust) and lies on its side out of the Bizon's way (<see cref="TurretFall"/>). A target the Bizon never reaches keeps its turret until
    /// the death, which knocks it off the same way.</item>
    /// </list>
    /// R17 / R19 / R20 (14j) stay: OzRamImpactLook.CrushLook gets the crush's progress (char, smoke and fire, grinding chips, the wobble).
    /// The death of a dented target keeps this wreck (OzBizonCrushView.CrushedBreakAt): it burns, smoulders and sinks like every wreck.
    /// Switches: <see cref="Deform"/> (off: nothing dented, the old break at the death), <see cref="TurretFall"/>, <see cref="BarrelBend"/>.
    /// </summary>
    [DefaultExecutionOrder(1020)]
    public class OzCrushSquash : MonoBehaviour
    {
        public static bool Deform = true, TurretFall = true, BarrelBend = true, HoldHeading = true;
        /// <summary>Grid cell (m), the soft edge's slope (m down per m sideways), the hull's strength (pressed height = this x its height, at
        /// least <see cref="HresMin"/> m), the floor the drawn press never passes (x that), the lower share of a pressed column that keeps its
        /// shape, the dents' and the bulge's size (m, x the hull's size), the Bizon's travel over a spot until it has given way (m).</summary>
        public static float CellM = 0.18f, FalloffSlope = 1.2f, HresMin = 0.32f, FloorK = 0.82f, KeepLow = 0.55f, DentM = 0.11f, BulgeM = 0.09f, YieldM = 1.2f;
        public static int MaxCells = 4096, MaxPiecesOff = 3;
        /// <summary>The turret: how far the Bizon's underside must reach into it before it is torn off (m), its sideways shove on the hull and on
        /// the ground (m/s2, top speed m/s: on the hull it slides on ahead of the Bizon and tips off the far edge; on the ground the Bizon's
        /// front pushes it aside out of its way), the gap it keeps ahead of the Bizon's front (m).</summary>
        public static float TurretHitM = 0.05f, ShoveLatAccelHull = 2.5f, ShoveLatMaxHull = 2f, ShoveLatAccel = 12f, ShoveLatMax = 5f, ShoveGap = 0.06f;
        /// <summary>1.5 owner N5 (kit bizon_crush_turret_late): the turret stays on its ring
        /// until the front of the Bizon's TRACKS' bottom run (where its weight bears; not its tall front / front plate) is this far past the
        /// ring's middle (m); while on it is pressed down into the caving roof as far as the Bizon's underside over it needs, at most
        /// <see cref="TurretSinkK"/> x its body height, never so far that a barrel's root goes under the roof, at most TurretSinkRate m/s;
        /// torn under the Bizon it is dragged along (DragShare x its speed) and squeezed out sideways (m/s2, m/s); ahead of the Bizon's front a
        /// shove catches up at most ShoveCatchUp m/s faster than the Bizon (no jump). Off: the v3 rule (its box 5 cm into the underside).</summary>
        public static bool TurretLate = true;
        public static float RingPastM = 0f, TurretSinkK = 0.35f, TurretSinkRate = 2.5f, DragShare = 0.8f, SqueezeAccel = 18f, SqueezeMax = 5f, ShoveCatchUp = 6f;
        /// <summary>1.5 N5, the frames check: how far the last turret was pressed into the roof (m), why it was torn.</summary>
        public static float LastTurretSinkM;
        public static string LastTearWhy = "";

        // statistics (the frames check / proof)
        public static int Begun, PartsDeformed, PartsUnreadable, DeformPasses, TurretBreaks, TurretLandings, TurretRests, BarrelBends, PiecesOff, Crunches;
        public static float LastAmount, LastBarrelDeg, LastTurretTravelM, LastDeformMs, MaxDeformMs, LastPressM, LastBreakAfterTouchS = -1f;
        public static string LastTurretState = "", LastType = "";

        /// <summary>The crush's progress (0 = whole, 1 = every pressed column at its crushed height).</summary>
        public float Amount => amount;
        /// <summary>R19 (OzRamImpactLook): the crush look's state; the pressed hull's mean top now (m above its ground).</summary>
        public OzRamImpactLook.CrushState Look;
        public float HullTopNow => hullTopNow;
        /// <summary>The frames check: the turret's state (on / torn / shoved / falling / landed / resting / none), the barrel's bend (deg).</summary>
        public string TurretState => turretState;
        public float BarrelDeg => barrels.Count > 0 ? barrels[0].drawn : 0f;
        public int DeformedParts => deformedParts;

        /// <summary>UnitView.BeginWreck: this view's hull was dented by a Bizon crush (it stays that wreck).</summary>
        public static bool KeepsWreck(UnitView v) => v && v.TryGetComponent<OzCrushSquash>(out var s) && s.deformedParts > 0;

        // ------------------------------------------------------------------------------------------------ state
        ZGame game; ZObject obj; UnitView view, bizonView; Transform model; OzCrushRide ride;
        Matrix4x4 K, Kinv; float sizeK = 1f, kres = 0.5f, cx, cz; uint seed;
        float yawHold; bool yawHeld, begun;
        // the grid over the footprint (K space: x across, z along the hull at the contact, y up from the target's ground)
        int nx, nz; float gx0, gz0, cell;
        float[] T, Td, Bt, Hres, Hfloor, P, Ps, Uc; bool[] mat; float near = 1e9f, far = -1e9f;
        float amount, hullTopNow = 1.2f, crushSpan = 1f, pressedAt = -1f, sparkAt, crunchAt; bool creaked;

        class Part
        {
            public MeshFilter mf; public Mesh src, inst; public Vector3[] rest, work, nRest; public List<Vector3> nBuf;
            public int i0, i1, j0, j1, barrel = -1; public bool live = true, turretPart;
        }
        readonly List<Part> parts = new List<Part>();
        int deformedParts;

        class Barrel
        {
            public Transform node; public Vector3 axisL; public float len = 1f, rad = 0.1f, want, drawn, maxBend = 80f; public bool started;
            public Vector3 pivotK, axisK, rotK;
        }
        readonly List<Barrel> barrels = new List<Barrel>();

        // small parts that are torn off when the track reaches them
        readonly List<Transform> smalls = new List<Transform>();
        int piecesOff;

        // the turret
        Transform turret; Bounds tLocal; bool turretGone; string turretState = "none";
        Vector3 tPos, tVel, tDir = Vector3.forward, tLat = Vector3.right, tAxis = Vector3.back; float tRollSign = 1f; Quaternion tRot0; float tPitch, tPitchV, tRoll, tRollV, tYaw, tYawV, tSide = 1f, tAirT, tStillT, tRestPitch, tRestRoll, tScrapeAt, tLandAt, tBornAt;
        bool tFell, tRest, tPushed, tLanded; float tPushT = 9f; Vector3 tRestPosL; Quaternion tRestRotL; Vector3 tStart;
        Vector3 tHomeL; bool tHomeSet; float tSink; Vector3[] tPts;                                   // 1.5 N5: its place on the ring, its press, its body points

        // ------------------------------------------------------------------------------------------------ begin
        public void Begin(ZGame g, ZObject o, UnitView v, ZObject b, UnitView bv)
        {
            game = g; obj = o; view = v; bizonView = bv; model = OzBizonCrushView.ModelOf(v);
            if (!v || !model) return;
            seed = RobotDeathRule.Hash(o != null ? o.Id : 0, 77, 383u);
            yawHold = v.transform.eulerAngles.y; yawHeld = true;                                        // 1.5 pass 6a: the heading at the contact
            K = Matrix4x4.TRS(v.transform.position, Quaternion.Euler(0f, yawHold, 0f), Vector3.one); Kinv = K.inverse;
            kres = Strength(o);
            LastType = o != null ? o.Type : "";
            turret = FindTurret(model);
            FindBarrels();
            if (turret) { tLocal = TurretBody(); turretState = "on"; tHomeL = turret.localPosition; tHomeSet = true; tSink = 0f; tPts = TurretPoints(); }   // 1.5 N5
            Collect();
            BuildGrid();
            begun = true; Begun++;
        }

        /// <summary>The ride of the Bizon over this target (OzBizonCrushView.OnEvent): the crush's along-axis per cell, the near / far faces.</summary>
        public void Bind(OzCrushRide r)
        {
            ride = r;
            if (!begun || Uc == null) return;
            near = 1e9f; far = -1e9f;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i; var w = K.MultiplyPoint3x4(new Vector3(gx0 + (i + 0.5f) * cell, 0f, gz0 + (j + 0.5f) * cell));
                    Uc[k] = r.AlongOf(w);
                    if (T[k] > 0.15f) { near = Mathf.Min(near, Uc[k] - 0.5f * cell); far = Mathf.Max(far, Uc[k] + 0.5f * cell); }
                }
        }

        /// <summary>The target's near / far faces along the crush (m from the ride's origin), false when unknown.</summary>
        public bool Faces(out float n, out float f) { n = near; f = far; return begun && near < far; }

        /// <summary>The hull's strength by type (its crushed height = this x its height).</summary>
        static float Strength(ZObject o)
        {
            if (o == null) return 0.5f;
            if (o.Kind == ZKind.Cannon) return 0.42f;
            switch (o.Type)
            {
                case "jeep": return 0.4f;
                case "apc": case "crane": return 0.45f;
                case "light": return 0.48f;
                case "missile_launcher": return 0.46f;
                case "medium": return 0.5f;
                case "heavy": return 0.52f;
                case ZBigTanks.Mammoth: case ZBigTanks.Bizon: return 0.62f;
                default: return 0.5f;                                                                  // the medium tank (mtank) and anything else
            }
        }

        static Transform FindTurret(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (n.Contains("wreck") || n.Contains("Wreck")) continue;
                if (n.Contains("TurretYaw") || n == "MechWaistYaw" || n.Contains("RackYaw")) return t;
            }
            return null;
        }

        static bool Overlay(Renderer r)
        {
            if (!r || !r.enabled || !(r is MeshRenderer)) return true;
            var n = r.name.ToLowerInvariant();
            if (n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring") || n.Contains("marker") || n.Contains("glow") || n.Contains("cone") || n.Contains("decal")) return true;
            return r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450;
        }

        static bool Under(Transform t, Transform parent) => parent && t && (t == parent || t.IsChildOf(parent));

        /// <summary>A transform's renderers' box in its own space (mesh bounds; works for unreadable meshes too).</summary>
        static Bounds LocalBounds(Transform t)
        {
            var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
            var w2l = t.worldToLocalMatrix;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(false))
            {
                var r = mf.GetComponent<Renderer>(); if (Overlay(r) || !mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds; var m = w2l * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return any ? b : new Bounds(Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 1.6f));
        }

        /// <summary>The turret's body box in its own space: its renderers without the barrels, antennas and hatch lids (the shove and the
        /// landing go by it).</summary>
        Bounds TurretBody()
        {
            var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
            var w2l = turret.worldToLocalMatrix;
            foreach (var mf in turret.GetComponentsInChildren<MeshFilter>(false))
            {
                var r = mf.GetComponent<Renderer>(); if (Overlay(r) || !mf.sharedMesh) continue;
                bool skip = false; foreach (var bb in barrels) if (Under(mf.transform, bb.node)) { skip = true; break; }
                var n = mf.name.ToLowerInvariant(); if (skip || n.Contains("antenn") || n.Contains("aerial") || n.Contains("hatch") || n.Contains("lid")) continue;
                var mb = mf.sharedMesh.bounds; var m = w2l * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return any ? b : LocalBounds(turret);
        }

        void FindBarrels()
        {
            var list = new List<Transform>();
            var all = model.GetComponentsInChildren<Transform>(true);
            string[] keys = { "barrelpitch", "barrelspin", "barrel" };
            foreach (var key in keys)
            {
                foreach (var t in all)
                {
                    var n = t.name.ToLowerInvariant();
                    if (n.Contains("wreck") || !n.Contains(key)) continue;
                    if (key == "barrel" && (n.Contains("recoil") && !n.Contains("barrel_"))) continue;
                    list.Add(t);
                }
                if (list.Count > 0) break;
            }
            for (int i = list.Count - 1; i >= 0; i--)
                for (int j = 0; j < list.Count; j++) if (j != i && list[i] != list[j] && list[i].IsChildOf(list[j])) { list.RemoveAt(i); break; }
            foreach (var t in list)
            {
                var b = new Barrel { node = t };
                // its axis: from the pivot to its farthest mesh point (any model's front); its length and thickness
                float far2 = 0f; Vector3 tip = t.position - t.forward; Bounds wb = new Bounds(t.position, Vector3.zero);
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>(false))
                {
                    var r = mf.GetComponent<Renderer>(); if (Overlay(r) || !mf.sharedMesh) continue;
                    var mb = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
                    for (int c = 0; c < 8; c++)
                    {
                        var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                        wb.Encapsulate(p); float d = (p - t.position).sqrMagnitude; if (d > far2) { far2 = d; tip = p; }
                    }
                }
                var ax = wb.center - t.position; if (ax.sqrMagnitude < 0.01f) ax = tip - t.position;   // along the middle of its barrels (a twin gun too)
                if (ax.sqrMagnitude < 0.04f) continue;
                ax.Normalize(); float reachB = 0.3f;
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>(false))
                {
                    var r = mf.GetComponent<Renderer>(); if (Overlay(r) || !mf.sharedMesh) continue;
                    var mb = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
                    for (int c = 0; c < 8; c++) reachB = Mathf.Max(reachB, Vector3.Dot(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))) - t.position, ax));
                }
                b.len = Mathf.Clamp(reachB, 0.3f, 8f);
                b.axisL = t.InverseTransformDirection(ax);
                var e = wb.extents; b.rad = Mathf.Clamp(Mathf.Min(e.x, Mathf.Min(e.y, e.z)) * 0.9f, 0.05f, 0.45f);
                barrels.Add(b);
            }
        }

        void Collect()
        {
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(false))
            {
                var r = mf.GetComponent<Renderer>();
                if (Overlay(r) || !mf.sharedMesh) continue;
                if (mf.transform.name == "OZ_WreckFragments" || (mf.transform.parent && mf.transform.parent.name == "OZ_WreckFragments")) continue;
                int bi = -1; for (int i = 0; i < barrels.Count; i++) if (Under(mf.transform, barrels[i].node)) { bi = i; break; }
                bool tp = bi < 0 && Under(mf.transform, turret);
                var p = new Part { mf = mf, src = mf.sharedMesh, barrel = bi, turretPart = tp };
                if (tp) continue;                                                                         // the turret stays rigid (it is torn off whole)
                if (!mf.sharedMesh.isReadable) { PartsUnreadable++; continue; }
                p.rest = mf.sharedMesh.vertices;
                if (p.rest == null || p.rest.Length == 0) continue;
                parts.Add(p);
                if (bi < 0 && SmallPart(mf.transform, r)) smalls.Add(mf.transform);
            }
        }

        static readonly string[] SmallNames = { "antenna", "aerial", "ladder", "rung", "rail", "lamp", "headlight", "spare", "jerry", "mirror", "toolbox", "tool", "shovel", "hook", "tow", "bracket", "pennant", "exhaust", "box" };
        static bool SmallPart(Transform t, Renderer r)
        {
            var n = t.name.ToLowerInvariant(); bool named = false;
            foreach (var s in SmallNames) if (n.Contains(s)) { named = true; break; }
            if (!named || n.Contains("wheel") || n.Contains("track") || n.Contains("hull") || n.Contains("link")) return false;
            var sz = r.bounds.size; return Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) < 1.6f;
        }

        void BuildGrid()
        {
            // the parts' rest points in K space: their box, then the grid
            var kb = new Bounds(); bool any = false;
            var kpos = new List<Vector3[]>(parts.Count);
            foreach (var p in parts)
            {
                var m = Kinv * p.mf.transform.localToWorldMatrix; var a = new Vector3[p.rest.Length];
                for (int i = 0; i < a.Length; i++) { a[i] = m.MultiplyPoint3x4(p.rest[i]); if (!any) { kb = new Bounds(a[i], Vector3.zero); any = true; } else kb.Encapsulate(a[i]); }
                kpos.Add(a);
            }
            if (!any) { nx = nz = 0; return; }
            cx = kb.center.x; cz = kb.center.z;                                                          // the walls bulge out from the hull's middle
            cell = Mathf.Max(CellM, Mathf.Sqrt(kb.size.x * kb.size.z / MaxCells) * 1.05f);
            gx0 = kb.min.x - 3f * cell; gz0 = kb.min.z - 3f * cell;
            nx = Mathf.CeilToInt((kb.size.x + 6f * cell) / cell); nz = Mathf.CeilToInt((kb.size.z + 6f * cell) / cell);
            int n = nx * nz;
            T = new float[n]; Td = new float[n]; Bt = new float[n]; Hres = new float[n]; Hfloor = new float[n]; P = new float[n]; Ps = new float[n]; Uc = new float[n]; mat = new bool[n];
            float hullTop = 0.5f, ext = 0.5f;
            for (int pi = 0; pi < parts.Count; pi++)
            {
                var p = parts[pi]; var a = kpos[pi];
                int i0 = int.MaxValue, i1 = int.MinValue, j0 = int.MaxValue, j1 = int.MinValue;
                for (int v = 0; v < a.Length; v++)
                {
                    int i = Mathf.Clamp(Mathf.FloorToInt((a[v].x - gx0) / cell), 0, nx - 1), j = Mathf.Clamp(Mathf.FloorToInt((a[v].z - gz0) / cell), 0, nz - 1), k = j * nx + i;
                    float y = Mathf.Max(0f, a[v].y);
                    if (p.barrel >= 0) Bt[k] = Mathf.Max(Bt[k], y); else { T[k] = Mathf.Max(T[k], y); hullTop = Mathf.Max(hullTop, y); ext = Mathf.Max(ext, Mathf.Max(Mathf.Abs(a[v].x), Mathf.Abs(a[v].z))); }
                    i0 = Mathf.Min(i0, i); i1 = Mathf.Max(i1, i); j0 = Mathf.Min(j0, j); j1 = Mathf.Max(j1, j);
                }
                p.i0 = Mathf.Max(0, i0 - 1); p.i1 = Mathf.Min(nx - 1, i1 + 1); p.j0 = Mathf.Max(0, j0 - 1); p.j1 = Mathf.Min(nz - 1, j1 + 1);
            }
            CloseHoles();
            sizeK = Mathf.Clamp(hullTop / 1.5f, 0.6f, 1.6f);
            float bThick = 0.2f; foreach (var b in barrels) bThick = Mathf.Max(bThick, b.rad * 2f);
            float hmin = HresMin * Mathf.Clamp(sizeK, 0.8f, 1.3f);
            for (int k = 0; k < n; k++)
            {
                mat[k] = T[k] > 0.05f || Bt[k] > 0.05f;
                Hres[k] = T[k] > 0.05f ? Mathf.Min(T[k], Mathf.Max(hmin, kres * T[k])) : 0f;
                Hfloor[k] = T[k] > 0.05f ? FloorK * Hres[k] : Bt[k] > 0.05f ? Mathf.Min(Bt[k], 0.45f * bThick) : 0f;
                P[k] = Ps[k] = 1e3f;
            }
            // the top dilated one cell (the hull's outer vertices interpolate against a sane top)
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float t = T[j * nx + i];
                    for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                        {
                            int a2 = i + di, b2 = j + dj; if (a2 < 0 || b2 < 0 || a2 >= nx || b2 >= nz) continue;
                            t = Mathf.Max(t, T[b2 * nx + a2]);
                        }
                    Td[j * nx + i] = t;
                }
            float sumT = 0f, sumH = 0f;
            for (int k = 0; k < n; k++) if (T[k] > 0.2f) { sumT += T[k]; sumH += T[k] - Hres[k]; }
            crushSpan = Mathf.Max(0.05f, sumH);
            hullTopNow = hullTop;
        }

        /// <summary>Holes in the hull's top (the turret ring opening: no hull mesh under the turret) are closed by the roof around them
        /// (a cell with hull in all four directions within 12 cells takes 98 % of the lowest of them): the turret and the Bizon rest on it.</summary>
        void CloseHoles()
        {
            var fill = (float[])T.Clone();
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (T[j * nx + i] > 0.05f) continue;
                    float m = float.MaxValue; int hits = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        int dj = d == 0 ? 0 : d == 1 ? 0 : d == 2 ? 1 : -1, di = d == 0 ? 1 : d == 1 ? -1 : 0;
                        for (int s = 1; s <= 12; s++)
                        {
                            int a = j + dj * s, b = i + di * s; if (a < 0 || b < 0 || a >= nz || b >= nx) break;
                            float v = T[a * nx + b]; if (v > 0.05f) { m = Mathf.Min(m, v); hits++; break; }
                        }
                    }
                    if (hits == 4) fill[j * nx + i] = 0.98f * m;
                }
            T = fill;
        }

        // ------------------------------------------------------------------------------------------------ the support for the ride
        /// <summary>OzCrushRide: the target's support at a world point (m above the target's ground; float.MinValue = no hull there): its whole
        /// top until the Bizon's front track has passed over it, then giving way over <see cref="YieldM"/> to its crushed height.</summary>
        public float SupportAt(Vector3 w, float frontAlong)
        {
            if (!begun || nx == 0) return float.MinValue;
            var q = Kinv.MultiplyPoint3x4(w);
            float fx = (q.x - gx0) / cell - 0.5f, fz = (q.z - gz0) / cell - 0.5f;
            int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
            if (i < -1 || j < -1 || i >= nx || j >= nz) return float.MinValue;
            float ax = fx - i, az = fz - j, s = 0f, top = 0f;
            for (int c = 0; c < 4; c++)
            {
                int ii = i + (c & 1), jj = j + (c >> 1); float wt = ((c & 1) == 0 ? 1f - ax : ax) * ((c >> 1) == 0 ? 1f - az : az);
                if (ii < 0 || jj < 0 || ii >= nx || jj >= nz) continue;
                int k = jj * nx + ii; if (T[k] <= 0.05f) continue;
                float y = Mathf.SmoothStep(0f, 1f, (frontAlong - Uc[k]) / YieldM);
                s += wt * Mathf.Min(Mathf.Lerp(T[k], Hres[k], y), P[k] > 999f ? T[k] : Mathf.Max(P[k], Hres[k]));
                top = Mathf.Max(top, T[k]);
            }
            return top <= 0f ? float.MinValue : s;
        }

        /// <summary>The pressed hull's top at a world point (m above the target's ground; 0 = no hull): the turret slides on it.</summary>
        public float PressedTopAt(Vector3 w)
        {
            if (!begun || nx == 0) return 0f;
            var q = Kinv.MultiplyPoint3x4(w);
            int i = Mathf.FloorToInt((q.x - gx0) / cell), j = Mathf.FloorToInt((q.z - gz0) / cell);
            if (i < 0 || j < 0 || i >= nx || j >= nz) return 0f;
            int k = j * nx + i; return T[k] <= 0.05f ? 0f : Mathf.Min(T[k], Ps[k]);
        }

        // ------------------------------------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            if (!begun || !view || !model) return;
            if (HoldHeading && yawHeld && !view.Dying)
            {   // 1.5 pass 6a: after UnitView's sync: the hull back on its contact heading (tilt kept), so it stays under the Bizon's tracks
                float d = Mathf.DeltaAngle(transform.eulerAngles.y, yawHold);
                if (Mathf.Abs(d) > 0.01f) transform.rotation = Quaternion.AngleAxis(d, Vector3.up) * transform.rotation;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool pressing = ride && ride.Pressing && bizonView && nx > 0;
            float before = amount; bool changed = false, bent = false;
            if (pressing && Deform) changed = PressFromCeiling();
            if (changed) { Soften(); amount = Mathf.Max(amount, Progress()); }
            if (BarrelBend && !turretGone && barrels.Count > 0) bent = UpdateBarrels(dt, pressing);
            if (Deform && (changed || bent)) DeformParts(changed, bent);
            if (TurretFall && turret) UpdateTurret(dt, pressing);
            if (changed) { TearOff(); Crunch(); }
            LastAmount = amount;
            OzRamImpactLook.CrushLook(this, view, obj, bizonView, Mathf.SmoothStep(0f, 1f, amount), amount - before);
        }

        int lastDropK = -1;

        /// <summary>The Bizon's underside over each grid cell this frame (K-space metres above the target's ground; 1e3 = none).</summary>
        float CeilingAt(float x, float z, Matrix4x4 toB, Vector3 ub)
        {
            var q = toB.MultiplyPoint3x4(new Vector3(x, 0f, z));
            float u = ride.UnderAt(q.x, q.z); if (u > 999f) return 1e3f;
            float t = (u - q.y) / ub.y;
            float u2 = ride.UnderAt(q.x + ub.x * t, q.z + ub.z * t);
            if (u2 < 999f) t = (u2 - q.y) / ub.y;
            return t;
        }

        bool PressFromCeiling()
        {
            int dropK = -1; float drop = 0f;
            var bm = ride.ModelNode; if (!bm) return false;
            var toB = bm.worldToLocalMatrix * K; var ub = toB.MultiplyVector(Vector3.up);
            if (ub.y < 0.2f) return false;
            var bc = Kinv.MultiplyPoint3x4(bm.position); float reach = ride.ReachM + 1f, r2 = reach * reach;
            bool any = false;
            for (int j = 0; j < nz; j++)
            {
                float z = gz0 + (j + 0.5f) * cell, dz = z - bc.z; if (dz * dz > r2) continue;
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i; if (!mat[k]) continue;
                    float x = gx0 + (i + 0.5f) * cell, dx = x - bc.x; if (dx * dx + dz * dz > r2) continue;
                    float top = Mathf.Max(T[k], Bt[k]);
                    float c = CeilingAt(x, z, toB, ub); if (c >= top + 0.01f) continue;
                    c = Mathf.Max(c, Hfloor[k]);
                    if (c < P[k] - 0.002f)
                    {
                        float d = Mathf.Min(P[k], top) - c; P[k] = c; any = true;
                        if (d > drop) { drop = d; dropK = k; }
                    }
                }
            }
            if (any) { lastDropK = dropK; LastPressM = Mathf.Max(LastPressM, drop); if (pressedAt < 0f) pressedAt = Time.time; }
            return any;
        }

        /// <summary>The soft edge: each cell no higher than its neighbours' press + the slope x the distance (two chamfer passes over the
        /// material cells), then a two-cell ring around the hull for the interpolation at its edge.</summary>
        void Soften()
        {
            int n = nx * nz; float a = cell * FalloffSlope, b = a * 1.41421f;
            for (int k = 0; k < n; k++) Ps[k] = mat[k] ? P[k] : 1e3f;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i; if (!mat[k]) continue; float v = Ps[k];
                    if (i > 0 && mat[k - 1]) v = Mathf.Min(v, Ps[k - 1] + a);
                    if (j > 0)
                    {
                        if (mat[k - nx]) v = Mathf.Min(v, Ps[k - nx] + a);
                        if (i > 0 && mat[k - nx - 1]) v = Mathf.Min(v, Ps[k - nx - 1] + b);
                        if (i < nx - 1 && mat[k - nx + 1]) v = Mathf.Min(v, Ps[k - nx + 1] + b);
                    }
                    Ps[k] = v;
                }
            for (int j = nz - 1; j >= 0; j--)
                for (int i = nx - 1; i >= 0; i--)
                {
                    int k = j * nx + i; if (!mat[k]) continue; float v = Ps[k];
                    if (i < nx - 1 && mat[k + 1]) v = Mathf.Min(v, Ps[k + 1] + a);
                    if (j < nz - 1)
                    {
                        if (mat[k + nx]) v = Mathf.Min(v, Ps[k + nx] + a);
                        if (i < nx - 1 && mat[k + nx + 1]) v = Mathf.Min(v, Ps[k + nx + 1] + b);
                        if (i > 0 && mat[k + nx - 1]) v = Mathf.Min(v, Ps[k + nx - 1] + b);
                    }
                    Ps[k] = v;
                }
            for (int pass = 0; pass < 2; pass++)
            {
                ring.Clear();
                for (int j = 0; j < nz; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int k = j * nx + i; if (mat[k] || Ps[k] < 999f) continue;
                        float m = 1e3f;
                        for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                            {
                                int a2 = i + di, b2 = j + dj; if (a2 < 0 || b2 < 0 || a2 >= nx || b2 >= nz) continue;
                                m = Mathf.Min(m, Ps[b2 * nx + a2]);
                            }
                        if (m < 999f) ring.Add((k, m));
                    }
                foreach (var (k, m) in ring) Ps[k] = m;
            }
        }
        readonly List<(int, float)> ring = new List<(int, float)>();

        float Sample(float[] g, float x, float z)
        {
            float fx = (x - gx0) / cell - 0.5f, fz = (z - gz0) / cell - 0.5f;
            int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, nx - 2), j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, nz - 2);
            float ax = Mathf.Clamp01(fx - i), az = Mathf.Clamp01(fz - j); int k = j * nx + i;
            return Mathf.Lerp(Mathf.Lerp(g[k], g[k + 1], ax), Mathf.Lerp(g[k + nx], g[k + nx + 1], ax), az);
        }

        float Progress()
        {
            float s = 0f, top = 0f; int c = 0;
            for (int k = 0; k < T.Length; k++)
            {
                if (T[k] <= 0.2f) continue;
                float p = Mathf.Min(T[k], Ps[k]); s += T[k] - p;
                if (T[k] > 0.3f) { top += p; c++; }
            }
            if (c > 0) hullTopNow = top / c;
            return Mathf.Clamp01(s / crushSpan);
        }

        // ------------------------------------------------------------------------------------------------ the dents
        void DeformParts(bool pressChanged, bool bent)
        {
            double t0 = Time.realtimeSinceStartupAsDouble;                                                  // (no allocation per frame)
            for (int pi = 0; pi < parts.Count; pi++)
            {
                var p = parts[pi];
                if (!p.live) continue;
                if (!p.mf) { p.live = false; continue; }
                bool barrel = p.barrel >= 0;
                if (barrel) { if (turretGone || (!bent && !pressChanged)) continue; }
                else { if (!pressChanged || !Touched(p)) continue; }
                if (!p.inst)
                {
                    p.inst = Instantiate(p.src); p.inst.name = p.src.name + "_crushed"; p.inst.MarkDynamic();
                    p.work = new Vector3[p.rest.Length]; p.nRest = p.src.normals; p.nBuf = new List<Vector3>(p.rest.Length);
                    p.mf.sharedMesh = p.inst; deformedParts++; PartsDeformed++;
                }
                var toK = Kinv * p.mf.transform.localToWorldMatrix; var back = toK.inverse;
                var rv = p.rest; var wv = p.work;
                Barrel b = barrel ? barrels[p.barrel] : null;
                for (int v = 0; v < rv.Length; v++)
                {
                    var x = toK.MultiplyPoint3x4(rv[v]);
                    var y = barrel ? BendBarrel(x, b) : MapHull(x);
                    wv[v] = back.MultiplyPoint3x4(y);
                }
                p.inst.SetVertices(wv);
                if (p.nRest != null && p.nRest.Length == rv.Length)
                {
                    p.inst.RecalculateNormals();
                    p.inst.GetNormals(p.nBuf);
                    if (p.nBuf.Count == rv.Length)
                    {
                        for (int v = 0; v < rv.Length; v++)
                        {
                            float w = Mathf.Clamp01((wv[v] - rv[v]).sqrMagnitude * 900f);                 // 0 untouched .. 1 from 3.3 cm moved
                            if (w < 1f) p.nBuf[v] = Vector3.Normalize(Vector3.Lerp(p.nRest[v], p.nBuf[v], w));
                        }
                        p.inst.SetNormals(p.nBuf);
                    }
                }
            }
            DeformPasses++;
            LastDeformMs = (float)((Time.realtimeSinceStartupAsDouble - t0) * 1000.0); MaxDeformMs = Mathf.Max(MaxDeformMs, LastDeformMs);
        }

        bool Touched(Part p)
        {
            for (int j = p.j0; j <= p.j1; j++)
                for (int i = p.i0; i <= p.i1; i++) { int k = j * nx + i; if (Ps[k] < Td[k] - 0.002f) return true; }
            return false;
        }

        /// <summary>One hull point (K space) in the pressed hull: the column's upper part folded down under the press, the lower part kept,
        /// uneven dents, the walls bulged out.</summary>
        Vector3 MapHull(Vector3 x)
        {
            float h = x.y; if (h <= 0.01f) return x;
            float tv = Mathf.Max(Sample(Td, x.x, x.z), h), pv = Mathf.Min(Sample(Ps, x.x, x.z), tv);
            if (pv >= tv - 1e-4f) return x;
            pv = Mathf.Max(0.02f, pv);
            float y0 = KeepLow * pv;
            if (h <= y0) return x;
            float t = (h - y0) / Mathf.Max(1e-4f, tv - y0), nn = (tv - y0) / Mathf.Max(1e-3f, pv - y0);
            float hn = y0 + (pv - y0) * (1f - Mathf.Pow(1f - Mathf.Clamp01(t), nn));
            float a = Mathf.Clamp01(1f - pv / tv);
            float layer = Mathf.SmoothStep(0f, 1f, (hn - y0) / Mathf.Max(1e-3f, pv - y0));
            float dent = DentM * sizeK * a * layer * (0.62f * Mathf.Abs(Noise(x.x * 1.8f, x.z * 1.8f, seed)) + 0.38f * Mathf.Abs(Noise(x.x * 4.6f + 13.1f, x.z * 4.6f - 7.7f, seed ^ 0x5bd1e995u)));
            hn -= dent;
            float bul = BulgeM * sizeK * a * layer;
            float ox = x.x - cx, oz = x.z - cz, ol = Mathf.Sqrt(ox * ox + oz * oz);
            if (ol > 1e-3f) { float wv = 1f + 0.5f * Noise(x.x * 2.3f, x.z * 2.3f, seed + 3u); x.x += ox / ol * bul * wv; x.z += oz / ol * bul * wv; }
            x.z += 0.35f * bul * Noise(x.x * 3.1f, h * 3.1f, seed + 9u);                                 // buckled along: the folded plates are not straight
            x.y = Mathf.Max(0.01f, Mathf.Min(hn, Mathf.Max(0.02f, Sample(Ps, x.x, x.z))));
            return x;
        }

        /// <summary>Smooth value noise in -1..1 (seeded per target).</summary>
        static float Noise(float x, float z, uint s)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z); float fx = x - ix, fz = z - iz;
            fx = fx * fx * (3f - 2f * fx); fz = fz * fz * (3f - 2f * fz);
            float a = H(ix, iz, s), b = H(ix + 1, iz, s), c = H(ix, iz + 1, s), d = H(ix + 1, iz + 1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }
        static float H(int x, int z, uint s)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)z * 0xd8163841u ^ s * 0xcb1ab31fu;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xFFFF) / 32767.5f - 1f;
            }
        }

        // ------------------------------------------------------------------------------------------------ the gun
        /// <summary>The barrels' bend: the smallest bend that fits each barrel under the Bizon's underside now (never more than lays its tip
        /// on the ground or the hull under it), eased; true when a bend changed.</summary>
        bool UpdateBarrels(float dt, bool pressing)
        {
            bool moved = false;
            Matrix4x4 toB = default; Vector3 ub = Vector3.up; bool haveB = false;
            if (pressing && ride.ModelNode) { toB = ride.ModelNode.worldToLocalMatrix * K; ub = toB.MultiplyVector(Vector3.up); haveB = ub.y > 0.2f; }
            foreach (var b in barrels)
            {
                if (!b.node) continue;
                b.pivotK = Kinv.MultiplyPoint3x4(b.node.position);
                b.axisK = Kinv.MultiplyVector(b.node.TransformDirection(b.axisL)).normalized;
                var r = Vector3.Cross(b.axisK, Vector3.up); if (r.sqrMagnitude < 1e-4f) r = Vector3.right; r.Normalize();
                if ((Quaternion.AngleAxis(10f, r) * b.axisK).y > b.axisK.y) r = -r;                            // + bends the tip down
                b.rotK = r;
                if (haveB)
                {
                    b.maxBend = MaxBend(b);
                    float need = b.want;
                    for (float th = b.want; th <= b.maxBend + 0.01f; th += 1f) { need = th; if (Fits(b, th, toB, ub)) break; }
                    b.want = Mathf.Max(b.want, Mathf.Min(need, b.maxBend));
                }
                float was = b.drawn;
                b.drawn = Mathf.MoveTowards(b.drawn, b.want, dt * 240f);
                if (b.drawn > was + 0.05f) moved = true;
                if (!b.started && b.drawn > 1.5f)
                {
                    b.started = true; BarrelBends++;
                    var at = K.MultiplyPoint3x4(b.pivotK + b.axisK * (0.35f * b.len));
                    AudioDirector.Fire("dst_wreck_creak", at, obj != null ? obj.Id : -1, 1f, 0.8f);
                    AudioDirector.Fire("dst_debris_metal", at, obj != null ? obj.Id : -1, 0.7f, 0.75f);
                    OpenZHitSparks.Burst(at, Vector3.up, FxWeapon.Cannon, FxSurface.Metal, 0.6f, false);
                }
                LastBarrelDeg = Mathf.Max(LastBarrelDeg, b.drawn);
            }
            return moved;
        }

        /// <summary>The bend at which the barrel's tip (its underside) meets the ground or the hull under it.</summary>
        float MaxBend(Barrel b)
        {
            for (float th = 2f; th <= 85f; th += 2f)
            {
                var tip = BendPoint(b, b.len, th) - Vector3.up * b.rad;
                var w = K.MultiplyPoint3x4(tip);
                float sup = Mathf.Max(0.02f, PressedTopAt(w));
                if (tip.y < sup) return Mathf.Max(0f, th - 2f);
            }
            return 85f;
        }

        bool Fits(Barrel b, float th, Matrix4x4 toB, Vector3 ub)
        {
            for (int s = 0; s < 4; s++)
            {
                float d = b.len * (s == 0 ? 0.35f : s == 1 ? 0.6f : s == 2 ? 0.85f : 1f);
                var p = BendPoint(b, d, th) + Vector3.up * b.rad;
                if (p.y > CeilingAt(p.x, p.z, toB, ub) + 0.01f) return false;
            }
            return true;
        }

        /// <summary>The share of a barrel's bend at its mantlet (the rest at the kink), the kink's place along it.</summary>
        public static float MantletShare = 0.62f, KinkAt = 0.42f;

        Vector3 BendPoint(Barrel b, float d, float th) => BendBarrel(b.pivotK + b.axisK * d, b, th);

        Vector3 BendBarrel(Vector3 x, Barrel b) => BendBarrel(x, b, b.drawn);

        Vector3 BendBarrel(Vector3 x, Barrel b, float th)
        {
            if (th > 0.01f)
            {
                float s = Vector3.Dot(x - b.pivotK, b.axisK);
                float w0 = Mathf.SmoothStep(0f, 1f, (s - 0.04f * b.len) / (0.18f * b.len));
                if (w0 > 0f)
                {
                    float th0 = th * MantletShare, th1 = th - th0;
                    var q0 = Quaternion.AngleAxis(th0 * w0, b.rotK);
                    x = b.pivotK + q0 * (x - b.pivotK);
                    float sk = KinkAt * b.len, wk = Mathf.SmoothStep(0f, 1f, (s - (sk - 0.1f * b.len)) / (0.2f * b.len));
                    if (wk > 0f) { var kp = b.pivotK + Quaternion.AngleAxis(th0, b.rotK) * (b.axisK * sk); x = kp + Quaternion.AngleAxis(th1 * wk, b.rotK) * (x - kp); }
                }
            }
            if (nx > 0) x.y = Mathf.Min(x.y, Mathf.Max(0.02f, Sample(Ps, x.x, x.z) + 0.01f));                  // flattened where a track rolled over it
            x.y = Mathf.Max(0.015f, x.y);
            return x;
        }

        // ------------------------------------------------------------------------------------------------ small parts torn off
        void TearOff()
        {
            if (piecesOff >= MaxPiecesOff || smalls.Count == 0) return;
            for (int s = smalls.Count - 1; s >= 0 && piecesOff < MaxPiecesOff; s--)
            {
                var t = smalls[s]; if (!t || !t.IsChildOf(transform)) { smalls.RemoveAt(s); continue; }
                var r = t.GetComponent<Renderer>(); if (!r) { smalls.RemoveAt(s); continue; }
                var c = r.bounds.center; var q = Kinv.MultiplyPoint3x4(c);
                int i = Mathf.FloorToInt((q.x - gx0) / cell), j = Mathf.FloorToInt((q.z - gz0) / cell);
                if (i < 0 || j < 0 || i >= nx || j >= nz) continue;
                int k = j * nx + i; if (Ps[k] > Mathf.Max(T[k], r.bounds.max.y - K.m13) - 0.05f) continue;   // the press has not reached it
                foreach (var p in parts) if (p.mf && p.mf.transform == t) { if (p.inst) { p.mf.sharedMesh = p.src; Destroy(p.inst); p.inst = null; deformedParts = Mathf.Max(0, deformedParts - 1); } p.live = false; }
                smalls.RemoveAt(s);
                var from = transform.position + Vector3.up * 0.3f;
                if (WreckPieces.Single(t, from, 0.25f, (obj != null ? obj.Id : 0) * 31 + 7 + piecesOff)) { piecesOff++; PiecesOff++; AudioDirector.Fire("dst_debris_metal", c, obj != null ? obj.Id : -1, 0.55f, Random.Range(1.1f, 1.3f)); }   // torn off: a sharp metal snap
            }
        }

        // ------------------------------------------------------------------------------------------------ crunch sparks and sound
        void Crunch()
        {
            if (lastDropK < 0 || Time.time < sparkAt || !bizonView) return;
            sparkAt = Time.time + 0.16f;
            int i = lastDropK % nx, j = lastDropK / nx;
            var at = K.MultiplyPoint3x4(new Vector3(gx0 + (i + 0.5f) * cell, Mathf.Min(T[lastDropK], P[lastDropK]) + 0.05f, gz0 + (j + 0.5f) * cell));
            var to = bizonView.transform.position - at; to.y = 0f;
            var side = Vector3.Cross(Vector3.up, to.sqrMagnitude > 1e-4f ? to.normalized : Vector3.forward);
            OpenZHitSparks.Burst(at, Vector3.up + side * Random.Range(-0.6f, 0.6f), FxWeapon.Cannon, FxSurface.Metal, 0.55f, false);
            Crunches++;
            if (!creaked) { creaked = true; AudioDirector.Fire("dst_wreck_creak", at, obj != null ? obj.Id : -1, 1f, Random.Range(0.85f, 1f)); }
            if (Time.time >= crunchAt) { crunchAt = Time.time + 0.38f; AudioDirector.Fire("dst_debris_metal", at, obj != null ? obj.Id : -1, 0.5f, Random.Range(0.8f, 1.05f)); }
        }

        // ------------------------------------------------------------------------------------------------ the turret
        void UpdateTurret(float dt, bool pressing)
        {
            if (!turret) return;
            if (!turretGone)
            {
                bool hit = false;
                if (pressing && ride.ModelNode) { hit = TurretLate ? TracksOnRing() : TurretReached(); if (hit && !TurretLate) LastTearWhy = "v3: its box in the Bizon's underside"; }   // 1.5 N5
                if (!hit && view.Dying && view.DeadFor > 0.02f) { hit = true; LastTearWhy = "the blast"; }   // never reached: the blast knocks it off
                if (!hit) { if (TurretLate) PressTurret(dt, pressing); return; }                         // 1.5 N5: on its ring, pressed into the roof
                TearTurret();
            }
            if (tRest)
            {
                turret.SetPositionAndRotation(transform.TransformPoint(tRestPosL), transform.rotation * tRestRotL);
                return;
            }
            int sub = Mathf.Clamp(Mathf.CeilToInt(dt / 0.006f), 1, 8); float h = dt / sub;
            for (int s = 0; s < sub; s++) StepTurret(h);
            Pose(out var pos, out var rot);
            turret.SetPositionAndRotation(pos, rot);
            LastTurretTravelM = Mathf.Max(LastTurretTravelM, Vector3.Distance(new Vector3(tPos.x, 0f, tPos.z), new Vector3(tStart.x, 0f, tStart.z)));
        }

        /// <summary>The turret's top corners and its front / back faces against the Bizon's underside this frame.</summary>
        bool TurretReached()
        {
            var toB = ride.ModelNode.worldToLocalMatrix * K; var ub = toB.MultiplyVector(Vector3.up); if (ub.y < 0.2f) return false;
            var m = Kinv * turret.localToWorldMatrix; var c = tLocal.center; var e = tLocal.extents;
            for (int n = 0; n < 13; n++)
            {
                Vector3 lp;
                if (n < 8) lp = c + Vector3.Scale(e, new Vector3((n & 1) == 0 ? -1 : 1, (n & 2) == 0 ? 0.1f : 1f, (n & 4) == 0 ? -1 : 1));
                else if (n == 8) lp = c + Vector3.up * e.y;
                else lp = c + Vector3.Scale(e, new Vector3(n == 9 ? 1 : n == 10 ? -1 : 0, 0.4f, n == 11 ? 1 : n == 12 ? -1 : 0));
                var p = m.MultiplyPoint3x4(lp);
                if (p.y > CeilingAt(p.x, p.z, toB, ub) + TurretHitM) return true;
            }
            return false;
        }

        /// <summary>1.5 owner N5: the Bizon's TRACKS are on the turret ring: the front of their bottom run (OzCrushRide.RunFrontAlong) is
        /// <see cref="RingPastM"/> past the ring's middle, the ring under the Bizon (not beside it).</summary>
        bool TracksOnRing()
        {
            var c = tLocal.center; var ring = turret.TransformPoint(new Vector3(c.x, tLocal.min.y, c.z));
            if (Mathf.Abs(Vector3.Dot(ring - ride.Origin, ride.Lat)) > ride.HalfWidthM + 0.3f) return false;
            if (ride.AlongOf(ring) > ride.RunFrontAlong - RingPastM) return false;
            LastTearWhy = "the Bizon's tracks on its ring"; return true;
        }

        /// <summary>1.5 owner N5: on its ring with the Bizon's front / belly over it: pressed straight down into the caving roof as far as the
        /// underside over its body needs (+2 cm), never springing back, at most <see cref="TurretSinkK"/> x its body height, never so far that a
        /// barrel's root goes under the (pressed) roof there, at most <see cref="TurretSinkRate"/> m/s. Held there every frame.</summary>
        void PressTurret(float dt, bool pressing)
        {
            if (!tHomeSet || !turret || !turret.parent) return;
            if (pressing && ride && ride.ModelNode && tPts != null && tPts.Length > 0)
            {
                var toB = ride.ModelNode.worldToLocalMatrix * K; var ub = toB.MultiplyVector(Vector3.up);
                if (ub.y >= 0.2f)
                {
                    var m = Kinv * turret.localToWorldMatrix; float need = 0f;
                    for (int i = 0; i < tPts.Length; i++)
                    {
                        var p = m.MultiplyPoint3x4(tPts[i]);
                        float c = CeilingAt(p.x, p.z, toB, ub); if (c > 999f) continue;
                        need = Mathf.Max(need, p.y + tSink - c + 0.02f);                                   // p is drawn pressed by tSink: its own height is p.y + tSink
                    }
                    float cap = TurretSinkK * tLocal.size.y * Mathf.Abs(turret.lossyScale.y);
                    foreach (var b in barrels)
                    {   // the gun stays over the hull: a barrel's root never goes under the (pressed) roof under it
                        if (!b.node) continue;
                        float top = PressedTopAt(b.node.position); if (top <= 0.02f) continue;
                        cap = Mathf.Min(cap, Kinv.MultiplyPoint3x4(b.node.position).y + tSink - b.rad - top - 0.03f);
                    }
                    float goal = Mathf.Clamp(need, 0f, Mathf.Max(0f, cap));
                    if (goal > tSink) { tSink = Mathf.MoveTowards(tSink, goal, TurretSinkRate * dt); LastTurretSinkM = Mathf.Max(LastTurretSinkM, tSink); }
                }
            }
            if (tSink > 1e-4f) turret.localPosition = tHomeL + turret.parent.InverseTransformVector(-K.MultiplyVector(Vector3.up) * tSink);
        }

        /// <summary>1.5 owner N5: up to ~400 points of the turret's body (its own space; barrels, antennas, hatch lids left out): its mesh where
        /// readable, else its parts' box corners (once, at the contact).</summary>
        Vector3[] TurretPoints()
        {
            var l = new List<Vector3>(256); var w2l = turret.worldToLocalMatrix;
            foreach (var mf in turret.GetComponentsInChildren<MeshFilter>(false))
            {
                var r = mf.GetComponent<Renderer>(); if (Overlay(r) || !mf.sharedMesh) continue;
                bool skip = false; foreach (var bb in barrels) if (Under(mf.transform, bb.node)) { skip = true; break; }
                var n = mf.name.ToLowerInvariant(); if (skip || n.Contains("antenn") || n.Contains("aerial") || n.Contains("hatch") || n.Contains("lid")) continue;
                var m = w2l * mf.transform.localToWorldMatrix; var mesh = mf.sharedMesh;
                if (mesh.isReadable && mesh.vertexCount > 0) { var v = mesh.vertices; int step = Mathf.Max(1, v.Length / 64); for (int i = 0; i < v.Length; i += step) l.Add(m.MultiplyPoint3x4(v[i])); }
                else { var mb = mesh.bounds; for (int k = 0; k < 8; k++) l.Add(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)))); }
                if (l.Count > 400) break;
            }
            return l.ToArray();
        }

        /// <summary>1.5 owner N5: torn UNDER the Bizon (its front long past it): dragged along by the tracks and squeezed out sideways from under
        /// them (to the side it sits on), scraping; it then slides off the hull's side and falls. Never thrown ahead of the Bizon's front.</summary>
        void Squeeze(float h, Vector3 dir, Vector3 lat, Vector3 pos)
        {
            float va = Vector3.Dot(tVel, dir), want = Mathf.Max(0f, ride.SpeedAlong) * DragShare;
            if (va < want) tVel += dir * Mathf.Min(want - va, SqueezeAccel * h);
            float vl = Vector3.Dot(tVel, lat) * tSide;
            if (vl < SqueezeMax) tVel += lat * tSide * Mathf.Min(SqueezeAccel * h, SqueezeMax - vl);
            tPushed = true; tPushT = 0f;
            if (turretState == "torn" || turretState == "resting") turretState = "squeezed";
            if (Time.time >= tScrapeAt)
            {
                tScrapeAt = Time.time + 0.32f; var sp = pos + Vector3.up * 0.2f;
                OpenZHitSparks.Burst(sp, lat * tSide + Vector3.up, FxWeapon.MachineGun, FxSurface.Metal, 0.7f, false);
                AudioDirector.Fire("dst_debris_metal", sp, obj != null ? obj.Id : -1, 0.6f, Random.Range(0.85f, 1.1f));
            }
        }

        void TearTurret()
        {
            turretGone = true; TurretBreaks++; turretState = "torn";
            if (pressedAt > 0f) LastBreakAfterTouchS = Time.time - pressedAt;
            tPos = turret.position; tStart = tPos; tRot0 = turret.rotation; tVel = Vector3.up * 0.4f; tBornAt = Time.time;
            var dir = ride ? ride.Dir : transform.forward; var lat = ride ? ride.Lat : transform.right;
            tDir = dir; tLat = lat;
            float l = ride ? Vector3.Dot(tPos - ride.Origin, lat) : 0f;
            uint hsh = RobotDeathRule.Hash(obj != null ? obj.Id : 0, 41, 383u);
            tSide = Mathf.Abs(l) > 0.25f ? Mathf.Sign(l) : ((hsh & 1u) == 0 ? 1f : -1f);
            // it tips over about its gun's line (the gun stays level on the ground, never stuck into it), the top toward the side it is shoved
            tAxis = -dir;
            if (barrels.Count > 0 && barrels[0].node) { var bw = barrels[0].node.TransformDirection(barrels[0].axisL); bw.y = 0f; if (bw.sqrMagnitude > 0.05f) tAxis = bw.normalized; }
            var topGoes = Vector3.Cross(tAxis, Vector3.up); float toSide = Vector3.Dot(topGoes, lat * tSide);
            tRollSign = Mathf.Abs(toSide) > 0.3f ? Mathf.Sign(toSide) : (Vector3.Dot(topGoes, dir) >= 0f ? 1f : -1f);
            tRestRoll = Mathf.Lerp(68f, 96f, ((hsh >> 3) & 255u) / 255f); tRestPitch = Mathf.Lerp(-12f, 14f, ((hsh >> 11) & 255u) / 255f);
            tYawV = tSide * Mathf.Lerp(25f, 60f, ((hsh >> 19) & 255u) / 255f);
            if (!ride || view.Dying) tVel += (dir * 1.2f + lat * tSide * 2.4f);
            var at = turret.position + Vector3.up * 0.2f;
            AudioDirector.Fire("dst_fortgun_break_metal", at, obj != null ? obj.Id : -1, 1f, 1.05f);
            OpenZHitSparks.Burst(at, Vector3.up, FxWeapon.Cannon, FxSurface.Metal, 1f, false);
            if (FxManager.I) FxManager.I.Dust(at, 0.45f);
        }

        /// <summary>The turret's drawn pose: its yaw spin, tilted forward (top toward the shove) and over to its side.</summary>
        void Pose(out Vector3 pos, out Quaternion rot)
        {
            var dir = tDir; var lat = tLat;
            rot = Quaternion.AngleAxis(tYaw, Vector3.up) * Quaternion.AngleAxis(tPitch, lat) * Quaternion.AngleAxis(tRoll * tRollSign, tAxis) * tRot0;
            pos = tPos;
        }

        Vector3 Corner(int n, Quaternion rot, Vector3 pos)
        {
            var c = tLocal.center; var e = tLocal.extents; var ls = turret.lossyScale;
            var lp = c + Vector3.Scale(e, new Vector3((n & 1) == 0 ? -1 : 1, (n & 2) == 0 ? -1 : 1, (n & 4) == 0 ? -1 : 1));
            return pos + rot * Vector3.Scale(lp, ls);
        }

        float SupportUnder(Vector3 w)
        {
            float g = game != null ? game.GroundY(w.x, w.z) : 0f;
            float top = PressedTopAt(w);
            return top > 0.02f ? Mathf.Max(g, K.m13 + top) : g;
        }

        void StepTurret(float h)
        {
            var dir = tDir; var lat = tLat;
            Pose(out var pos, out var rot);
            // the Bizon's front shoves it ahead (and its rounded front aside)
            tPushed = false;
            if (ride && ride.isActiveAndEnabled && ride.Pressing)
            {
                float lo = float.MaxValue, hi = float.MinValue, back = float.MaxValue, lMin = float.MaxValue, lMax = float.MinValue;
                for (int n = 0; n < 8; n++)
                {
                    var c = Corner(n, rot, pos); lo = Mathf.Min(lo, c.y); hi = Mathf.Max(hi, c.y);
                    back = Mathf.Min(back, Vector3.Dot(c - ride.Origin, dir));
                    float l = Vector3.Dot(c - ride.Origin, lat); lMin = Mathf.Min(lMin, l); lMax = Mathf.Max(lMax, l);
                }
                bool inFoot = lMax > -ride.HalfWidthM && lMin < ride.HalfWidthM;
                float front = ride.FrontAlong(lo, hi);
                if (TurretLate && inFoot && front > -1e3f && back < front - 0.5f) Squeeze(h, dir, lat, pos);   // 1.5 N5: torn UNDER the Bizon
                else if (inFoot && front > -1e3f && back < front + ShoveGap)
                {
                    float push = front + ShoveGap - back;
                    if (TurretLate) push = Mathf.Min(push, (Mathf.Max(0f, ride.SpeedAlong) + ShoveCatchUp) * h);   // 1.5 N5: it catches up, never a jump
                    tPos += dir * push;
                    float va = Vector3.Dot(tVel, dir); if (va < ride.SpeedAlong) tVel += dir * (ride.SpeedAlong - va);
                    tPushed = true; tPushT = 0f;
                    if (turretState == "torn" || turretState == "resting") turretState = "shoved";
                    if (Time.time >= tScrapeAt) { tScrapeAt = Time.time + 0.32f; var sp = pos + Vector3.up * 0.2f; OpenZHitSparks.Burst(sp, -dir + Vector3.up, FxWeapon.MachineGun, FxSurface.Metal, 0.7f, false); AudioDirector.Fire(tFell ? "shuttle_skid_scrape" : "dst_debris_metal", sp, obj != null ? obj.Id : -1, 0.6f, Random.Range(0.85f, 1.1f)); }
                }
            }
            // gravity, the support under its corners (the pressed hull or the ground)
            tVel += Vector3.down * 9.81f * h;
            tPos += tVel * h;
            Pose(out pos, out rot);
            float pen = float.MinValue;
            for (int n = 0; n < 8; n++) { var c = Corner(n, rot, pos); float d = SupportUnder(c) - c.y; if (d > pen) pen = d; }
            bool onSup = pen > -0.03f;
            if (pen > 0f)
            {
                tPos.y += pen;
                if (tVel.y < 0f)
                {
                    float imp = -tVel.y;
                    tVel.y = imp > 1.8f ? imp * 0.14f : 0f;
                    if (imp > 1.6f && tAirT > 0.12f) Land(pos, imp);
                }
            }
            if (tPushT < 0.15f)
            {   // shoved (or just now): the Bizon's rounded front pushes it aside too, out of its way
                float vl = Vector3.Dot(tVel, lat) * tSide, lmax = tFell ? ShoveLatMax : ShoveLatMaxHull;
                if (vl < lmax) tVel += lat * tSide * Mathf.Min((tFell ? ShoveLatAccel : ShoveLatAccelHull) * h, lmax - vl);
            }
            tPushT += h;
            var centre = pos + rot * Vector3.Scale(tLocal.center, turret.lossyScale);
            if (!onSup)
            {   // off the hull's edge (or long in the air): it falls and tumbles over
                tAirT += h;
                if (tAirT > 0.08f && (PressedTopAt(centre) <= 0.02f || tAirT > 0.3f)) { tFell = true; if (turretState != "falling") turretState = "falling"; }
            }
            else tAirT = 0f;
            // friction where it rests and has not been shoved for a moment (a shove keeps its sideways speed)
            if (onSup && tPushT > 0.15f)
            {
                var hv = new Vector3(tVel.x, 0f, tVel.z); float sp = hv.magnitude, dec = (tFell ? 11f : 5f) * h;
                hv = sp > dec ? hv * (1f - dec / sp) : Vector3.zero; tVel.x = hv.x; tVel.z = hv.z;
            }
            // its tilt: shoved: tipping forward and over to its side; in the air: tumbling over; down: lying on its side, rocking out
            float wantP, wantR, hz;
            if (!onSup && tFell) { wantP = 38f; wantR = 100f; hz = 2.2f; }
            else if (tFell) { wantP = tRestPitch; wantR = tRestRoll; hz = 2.6f; }
            else
            {   // on the hull: it tilts to rest on the uneven crushed roof (into the dents), tipped forward a little more while shoved
                Slopes(rot, pos, dir, lat, out float sp, out float sr);
                wantP = sp + (tPushed ? 10f : 4f); wantR = sr + (tPushed ? 8f : 3f); hz = 3f;
            }
            Spring(ref tPitch, ref tPitchV, wantP, hz, tFell && onSup ? 0.35f : 0.6f, h);
            Spring(ref tRoll, ref tRollV, wantR, hz, tFell && onSup ? 0.35f : 0.6f, h);
            tYaw += tYawV * h; tYawV = Mathf.MoveTowards(tYawV, 0f, (onSup && !tPushed ? 160f : 20f) * h);
            // at rest: held there (on the wreck's own frame, so it sinks with it at the end)
            bool still = onSup && !tPushed && tVel.sqrMagnitude < 0.02f && Mathf.Abs(tPitchV) < 4f && Mathf.Abs(tRollV) < 4f;
            tStillT = still ? tStillT + h : 0f;
            if (tStillT > 0.45f && Time.time > tBornAt + 0.6f)
            {
                tRest = true; TurretRests++; turretState = "resting";
                Pose(out pos, out rot);
                tRestPosL = transform.InverseTransformPoint(pos); tRestRotL = Quaternion.Inverse(transform.rotation) * rot;
            }
            else if (onSup && tFell && turretState == "falling") { turretState = "landed"; if (!tLanded) { tLanded = true; Land(pos, 3.5f); } }
            LastTurretState = turretState;
        }

        /// <summary>The tilt (deg) that rests the turret's bottom on the supports under its front / back and its two sides.</summary>
        void Slopes(Quaternion rot, Vector3 pos, Vector3 dir, Vector3 lat, out float pitchDeg, out float rollDeg)
        {
            for (int n = 0; n < 8; n++) cs[n] = Corner(n, rot, pos);
            for (int i = 1; i < 8; i++) { var c = cs[i]; int j = i - 1; while (j >= 0 && cs[j].y > c.y) { cs[j + 1] = cs[j]; j--; } cs[j + 1] = c; }   // the four lowest: its bottom (no allocation)
            for (int n = 0; n < 4; n++) { sup4[n] = SupportUnder(cs[n]); al4[n] = Vector3.Dot(cs[n], dir); la4[n] = Vector3.Dot(cs[n], lat) * tSide; }
            float fS = float.MinValue, bS = float.MinValue, pS = float.MinValue, nS = float.MinValue, fa = 0f, ba = 0f, pa = 0f, na = 0f;
            float am = 0.25f * (al4[0] + al4[1] + al4[2] + al4[3]), lm = 0.25f * (la4[0] + la4[1] + la4[2] + la4[3]);
            for (int n = 0; n < 4; n++)
            {
                if (al4[n] >= am) { fS = Mathf.Max(fS, sup4[n]); fa += al4[n]; } else { bS = Mathf.Max(bS, sup4[n]); ba += al4[n]; }
                if (la4[n] >= lm) { pS = Mathf.Max(pS, sup4[n]); pa += la4[n]; } else { nS = Mathf.Max(nS, sup4[n]); na += la4[n]; }
            }
            float len = Mathf.Max(0.3f, 0.5f * (fa - ba)), wid = Mathf.Max(0.3f, 0.5f * (pa - na));
            pitchDeg = fS > float.MinValue && bS > float.MinValue ? Mathf.Clamp(Mathf.Atan2(bS - fS, len) * Mathf.Rad2Deg, -35f, 35f) : 0f;
            rollDeg = pS > float.MinValue && nS > float.MinValue ? Mathf.Clamp(Mathf.Atan2(nS - pS, wid) * Mathf.Rad2Deg, -35f, 35f) : 0f;
        }
        readonly Vector3[] cs = new Vector3[8]; readonly float[] sup4 = new float[4], al4 = new float[4], la4 = new float[4];

        static void Spring(ref float x, ref float v, float goal, float hz, float zeta, float h)
        {
            float w = 2f * Mathf.PI * hz;
            v += (-w * w * (x - goal) - 2f * zeta * w * v) * h; x += v * h;
        }

        void Land(Vector3 at, float imp)
        {
            if (Time.time < tLandAt) return;
            tLandAt = Time.time + 0.3f; TurretLandings++;
            at.y = (game != null ? game.GroundY(at.x, at.z) : at.y) + 0.15f;
            bool big = imp > 3f && tFell;
            AudioDirector.Fire(big ? "dst_turret_crash" : "dst_debris_metal", at, obj != null ? obj.Id : -1, big ? 0.9f : 0.6f, big ? 1f : 0.8f);
            if (FxManager.I) FxManager.I.Dust(at, big ? 0.9f : 0.45f);
            OpenZHitSparks.Burst(at + Vector3.up * 0.2f, Vector3.up, FxWeapon.MachineGun, FxSurface.Metal, big ? 0.9f : 0.5f, false);
            if (big && turretState != "resting") turretState = "landed";
        }

        void OnDestroy()
        {
            foreach (var p in parts)
            {
                if (!p.inst) continue;
                if (p.mf) p.mf.sharedMesh = p.src;
                Destroy(p.inst);
            }
        }
    }
}
