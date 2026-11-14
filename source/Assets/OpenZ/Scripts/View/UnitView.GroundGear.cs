using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only; the sim is never written.
    /// Cause: the heavy tank's tracks hang under the same node as its hull and turret (TankSuspension, v1 and v2), so every motion of the
    /// drawn tank moved them too: VehicleMotion's drive wobble on the whole model (pitch up to 3.2 deg, roll up to 2.6 deg), the suspension's
    /// bounce (7.5 cm), roll (2.9 deg), squat and lean and its per-barrel kick, and the shot's hull rock and slide (View/HeavyRecoil.cs). A 3 deg
    /// rock drops the end of a 5.2 m track 14 cm into the ground.
    /// Now the running gear stands on the ground: every frame (UnitView.Sync, around ApplyRecoil) EACH belt is placed on its own (k81d; the
    /// one plane fitted under both belts of k81c let 4-7 cm of a belt into the ground on uneven ground): the ground is sampled under that
    /// belt's bottom (<see cref="TrackGround.Stations"/> stations along its whole length at both edges, at the flat bottom of its mesh
    /// bounds: never above the real belt's bottom), its pitch eases to the line it rests on (the upper hull of that ground through its
    /// middle) and its height is exact every frame: its bottom clears the HIGHEST ground sample under it (ZGame.GroundY: terrain, bridge
    /// decks, craters; read where its points are drawn, no draw offset added), so no sample of it is ever in the ground (between two
    /// samples a sharp crease of the ground may reach a few cm in; over a dip a few cm of air); both belts share the roll their two
    /// heights give. The belts (and any spun wheels, on their side's belt) are put exactly on their frames after everything else moved
    /// the tank this frame: they follow only the ground under them, never the wobble, the bounce or the recoil.
    /// The hull and turret ride on the two belts (their mean height and pitch, the shared roll) with a slight wobble (<see cref="TrackGround.HullFeel"/> of VehicleMotion's drive
    /// wobble, bounce, roll, squat, lean and kick; the shot's rock and slide as they are) and are sprung above the gear: when a rock would take
    /// any corner of the hull's bottom below where it rests on the gear, the hull (with its turret) is raised by that much, so it rocks about
    /// the corner that touches and never sinks into its tracks or the ground. The tank's place and heading are the sim's as before.
    /// v1 and v2 alike (and any type added to <see cref="TrackGround.Types"/> whose tracks are their own renderers under its suspension node).
    /// Switch: <see cref="TrackGround.Enabled"/> (off = the drawing as before).
    /// Note: the v2 model's road wheels are modelled into its hull mesh (inside each belt loop), so they ride with the hull; with the hull
    /// sprung above the gear they only ever lift off the belt's bottom run by the hull's small rock, never through it.
    /// </summary>
    public static class TrackGround
    {
        /// <summary>The switch (the Play test turns it off for its control run and back on).</summary>
        public static bool Enabled = true;
        /// <summary>How much of VehicleMotion's drive wobble, bounce, roll, squat, lean and kick the hull keeps over planted tracks ("slightly":
        /// on a straight run the hull pitches about 0.8 deg and rolls about 1.3 deg (95th percentile; before: 3.1 and 5.0 deg with the tracks)
        /// and rides 4 cm (median) to 8 cm (95th percentile) over its rest on the tracks; measured off the engine, the kit's tools/gear_sim.py).</summary>
        public static float HullFeel = 0.25f;
        /// <summary>How fast the gear's plane takes a new slope (1/s); its height is exact every frame (the lowest point on the ground).</summary>
        public static float TiltRate = 20f;
        /// <summary>A sample this much above or below the ground under the centre is a step (a deck edge, a wall): the centre's ground is used.</summary>
        public static float StepIgnoreM = 0.7f;
        /// <summary>k81d: ground stations along each belt (both edges at each: 2 x Stations samples a belt), the proof's denser stations (three
        /// across each), a jump of the tank (m) that starts the belts afresh (no easing from the old place), and how long a still tank keeps
        /// its last samples (s).</summary>
        public static int Stations = 13, ProofStations = 17;
        public static float SnapM = 3f, StillResampleS = 0.25f;
        /// <summary>The vehicle types whose running gear is planted (both models of each).</summary>
        public static readonly HashSet<string> Types = new HashSet<string> { OzHeavyTank.Type, OzLightTank.Type, OzMediumTank.Type, OzMlrs.Type };   // 1.2 v2 units: the light tank (v1 and v2) too; DV145: the medium tank (its gear beside the suspension, as the light tank v2); 1.2 v2 units: the MLRS (v1 and v2: its belts, road wheels and side plates beside MLRSSuspension)
        /// <summary>Statistics (the Play test): frames planted, the largest hull lift over the gear (m).</summary>
        public static int Frames;
        public static float MaxHullLiftM;
    }

    public partial class UnitView
    {
        // k81d: each belt is placed on its own (left and right). A belt's samples run along its bottom outline (the flat bottom of its mesh
        // bounds over its whole length, at both edges: never above the real belt's bottom, so clearing them clears the belt), its pitch is
        // eased from the line it rests on (the upper hull of the ground under it, through its middle), its height is exact every frame (its
        // bottom clears the HIGHEST ground sample under it), both belts share the roll their two heights give; the hull rides on the two.
        sealed class GearNode { public Transform t; public bool spun; public int side; public Vector3 restPos, scale; public Quaternion restRot; public Matrix4x4 chain; }
        sealed class GearBelt
        {
            public float x0, x1, y0, z0, z1, xc, pitch, h;
            public Vector3[] pts, proof; public float[] g, hz, hg;                                       // hz / hg: BeltRestPitch's upper hull (kept: no garbage every frame)
            public Vector3 pos; public Quaternion rot = Quaternion.identity;
        }
        GearNode[] gearNodes; GearBelt[] gearBelts; bool gearInit, gearMoved, gearValid, gearSnap = true;
        Vector3[] gearHullRoot, gearHullBody; int[] gearHullSide;
        Transform gearBody; Quaternion gearRot = Quaternion.identity; float gearScale = 1f, gearRoll, gearSampledAt = -1f, gearLastYaw;
        Vector3 gearLastP;

        /// <summary>The hull's lift over its planted gear this frame (m; the proof).</summary>
        public float GearHullLiftNow { get; private set; }

        bool GearOn() => TrackGround.Enabled && Obj != null && !Obj.Destroyed && Obj.Kind == ZKind.Vehicle && TrackGround.Types.Contains(Obj.Type) && game != null && motion;

        /// <summary>UnitView.Sync, right before ApplyRecoil (the tank was just placed: the root = its ground frame x the drive wobble): each
        /// belt is set on the ground under it; the root (hull and turret) is put on the two belts with the hull's slight share of the wobble.</summary>
        void GroundGearBefore(float dt)
        {
            gearValid = false;
            if (!GearOn()) { GearRest(); return; }
            if (!gearInit) GearInit();
            if (gearNodes == null || gearBelts == null) return;
            float yawDeg = yaw + modelYaw;
            var yawRot = Quaternion.Euler(0f, yawDeg, 0f);
            var terrain = tilt * yawRot;                                                              // Sync's ground frame (the wobble is on top of it)
            var p0 = transform.position;
            float s = Mathf.Abs(transform.lossyScale.x); if (s < 1e-4f) s = 1f;
            if ((p0 - gearLastP).sqrMagnitude > TrackGround.SnapM * TrackGround.SnapM) gearSnap = true;   // a jump (a spawn, an exit, a teleport): no easing from the old place
            bool same = !gearSnap && (p0 - gearLastP).sqrMagnitude < 1e-6f && Mathf.Abs(Mathf.DeltaAngle(yawDeg, gearLastYaw)) < 0.01f && Time.time - gearSampledAt < TrackGround.StillResampleS;
            if (!same)
            {   // the ground under every sample (a still tank keeps its last samples for a moment: nothing under it moved)
                float gc = game.GroundY(p0.x, p0.z);
                foreach (var b in gearBelts)
                {
                    var lean = gearSnap ? Quaternion.identity : Quaternion.Euler(b.pitch, 0f, gearRoll);   // the belt's turn now: the ground is read where its points are drawn (a flat read is 9 cm off at the ends of a 15 deg slope)
                    for (int i = 0; i < b.pts.Length; i++)
                    {
                        var q = lean * (b.pts[i] * s); var w = p0 + yawRot * new Vector3(q.x, 0f, q.z);
                        float gy = game.GroundY(w.x, w.z); if (Mathf.Abs(gy - gc) > TrackGround.StepIgnoreM) gy = gc;   // a deck edge, a wall: the centre's ground
                        b.g[i] = gy;
                    }
                }
                gearSampledAt = Time.time;
            }
            gearLastP = p0; gearLastYaw = yawDeg;
            float k = gearSnap ? 1f : 1f - Mathf.Exp(-TrackGround.TiltRate * Mathf.Max(0f, dt));
            // each belt's pitch: the line it rests on, eased
            foreach (var b in gearBelts) b.pitch = Mathf.LerpAngle(b.pitch, BeltRestPitch(b, s), k);
            // the shared roll: from the two belts' heights at their own pitch (+x side up = +roll)
            GearBelt lo = gearBelts[0], hi = gearBelts[gearBelts.Length - 1];
            float rollWant = 0f;
            if (gearBelts.Length > 1 && hi.xc - lo.xc > 0.2f)
                rollWant = Mathf.Atan2(BeltHeight(hi, yawRot, hi.pitch, 0f, s) - BeltHeight(lo, yawRot, lo.pitch, 0f, s), (hi.xc - lo.xc) * s) * Mathf.Rad2Deg;
            gearRoll = Mathf.LerpAngle(gearRoll, rollWant, k);
            // each belt's frame: its pitch, the shared roll, its height exact (no sample under the ground; the vehicles' 1 cm draw offset
            // into the ground, yOffset, is not added to a planted belt or to the hull on it: the contact is exact)
            float hSum = 0f, pSum = 0f;
            foreach (var b in gearBelts)
            {
                b.rot = yawRot * Quaternion.Euler(b.pitch, 0f, gearRoll);
                b.h = BeltHeight(b, yawRot, b.pitch, gearRoll, s) + TrackGroundAir.For(Obj.Type);   // k87b, item 146: the heavy's belts ride a 1.5 cm air margin over a crease (View/UnitView.GroundGear.K87b.cs)
                b.pos = new Vector3(p0.x, b.h, p0.z);
                hSum += b.h; pSum += b.pitch;
            }
            gearSnap = false;
            // the hull's ground frame: on the two belts (their mean height and pitch, the shared roll)
            var gRot = yawRot * Quaternion.Euler(pSum / gearBelts.Length, 0f, gearRoll);
            var gPos = new Vector3(p0.x, hSum / gearBelts.Length, p0.z);
            var feel = Quaternion.Inverse(terrain) * transform.rotation;                               // the drive wobble VehicleMotion gave the root
            transform.SetPositionAndRotation(gPos, gRot * Quaternion.SlerpUnclamped(Quaternion.identity, feel, TrackGround.HullFeel));
            motion.ScaleBody(TrackGround.HullFeel);
            gearRot = gRot; gearScale = s; gearValid = true;
        }

        /// <summary>The height of a belt's frame where its lowest sample just touches the ground under it at this pitch and roll (every
        /// sample on or over the ground).</summary>
        static float BeltHeight(GearBelt b, Quaternion yawRot, float pitch, float roll, float s)
        {
            var r = yawRot * Quaternion.Euler(pitch, 0f, roll);
            float h = -1e9f;
            for (int i = 0; i < b.pts.Length; i++) h = Mathf.Max(h, b.g[i] - (r * (b.pts[i] * s)).y);
            return h;
        }

        /// <summary>The pitch a belt rests at (deg, the Euler x angle; the model's front is -z): the upper hull of the ground under it along its
        /// length (the higher of its two edges at each station), the hull's edge under its middle; on a hull corner under the middle, the mean
        /// of the two edges there.</summary>
        static float BeltRestPitch(GearBelt b, float s)
        {
            int n = b.pts.Length / 2;                                                                  // stations: pts[2j], pts[2j + 1] = the two edges at station j
            var hz = b.hz; var hg = b.hg; int m = 0;
            for (int j = 0; j < n; j++)
            {
                float z = b.pts[2 * j].z * s, gz = Mathf.Max(b.g[2 * j], b.g[2 * j + 1]);
                while (m >= 2 && (hz[m - 1] - hz[m - 2]) * (gz - hg[m - 2]) - (hg[m - 1] - hg[m - 2]) * (z - hz[m - 2]) >= 0f) m--;   // keep the upper hull (z ascending)
                hz[m] = z; hg[m] = gz; m++;
            }
            if (m < 2) return 0f;
            float zc = (b.z0 + b.z1) * 0.5f * s, slope = 0f;
            for (int i = 0; i + 1 < m; i++)
            {
                if (zc < hz[i] - 1e-4f || zc > hz[i + 1] + 1e-4f) continue;
                float a = (hg[i + 1] - hg[i]) / Mathf.Max(1e-4f, hz[i + 1] - hz[i]);
                if (Mathf.Abs(zc - hz[i + 1]) <= 1e-4f && i + 2 < m) a = 0.5f * (a + (hg[i + 2] - hg[i + 1]) / Mathf.Max(1e-4f, hz[i + 2] - hz[i + 1]));
                slope = a; break;
            }
            return -Mathf.Atan(slope) * Mathf.Rad2Deg;                                                 // Euler x turns +z down: ground rising toward +z = a negative angle
        }

        /// <summary>UnitView.Sync, right after ApplyRecoil (the shot's rock and slide are on the root): the hull is raised off any bottom
        /// corner that would sink below where it rests on its side's belt, then every gear node is put on its belt's frame.</summary>
        void GroundGearAfter()
        {
            if (!gearValid) { GearHullLiftNow = 0f; return; }
            var up = gearRot * Vector3.up;
            float lift = 0f;
            if (gearHullBody != null && gearBody)
                for (int i = 0; i < gearHullBody.Length; i++)
                {
                    var b = gearBelts[gearHullSide[i]];
                    var W = Matrix4x4.TRS(b.pos, b.rot, Vector3.one * gearScale);
                    lift = Mathf.Max(lift, Vector3.Dot(W.MultiplyPoint3x4(gearHullRoot[i]) - gearBody.TransformPoint(gearHullBody[i]), up));
                }
            if (lift > 1e-5f) transform.position += up * lift;
            GearHullLiftNow = lift; TrackGround.Frames++; if (lift > TrackGround.MaxHullLiftM) TrackGround.MaxHullLiftM = lift;
            foreach (var g in gearNodes)
            {
                if (!g.t) continue;
                var b = gearBelts[g.side];
                var W = Matrix4x4.TRS(b.pos, b.rot, Vector3.one * gearScale);
                var r = g.spun ? g.t.localRotation : g.restRot;                                       // a spun wheel keeps this frame's spin (VehicleMotion.Step)
                var m = W * g.chain * Matrix4x4.TRS(g.restPos, r, g.scale);
                g.t.SetPositionAndRotation(m.GetColumn(3), b.rot * g.chain.rotation * r);
            }
            gearMoved = true;
        }

        /// <summary>Switched off (or not a planted type): the gear back on its rest place under the suspension (as before); the next planted
        /// frame starts from the ground again (no easing from an old place).</summary>
        void GearRest()
        {
            GearHullLiftNow = 0f; gearSnap = true;
            if (!gearMoved || gearNodes == null) return;
            foreach (var g in gearNodes) if (g.t) { g.t.localPosition = g.restPos; if (!g.spun) g.t.localRotation = g.restRot; }
            gearMoved = false;
        }

        /// <summary>The gear's nodes and rest poses, each belt's outline (from its mesh bounds in the root's space at rest: no mesh reading,
        /// the same in the editor and in a build), the side of every node and the hull's bottom corners. Found once.</summary>
        void GearInit()
        {
            gearInit = true;
            if (!motion || !motion.BodyRest(out var bp, out var br)) return;
            gearBody = motion.Body;
            var list = new List<(Transform t, bool spun)>(); motion.GearNodes(list);
            if (list.Count == 0) return;
            var nodes = new List<GearNode>();
            Vector3 lo0 = Vector3.one * 1e9f, hi0 = -Vector3.one * 1e9f, lo1 = Vector3.one * 1e9f, hi1 = -Vector3.one * 1e9f; bool any0 = false, any1 = false;
            var boxes = new List<(Vector3 lo, Vector3 hi)>();
            foreach (var (t, spun) in list)
            {
                var g = new GearNode { t = t, spun = spun, restPos = t.localPosition, restRot = t.localRotation, scale = t.localScale, chain = GearChain(t.parent, bp, br) };
                nodes.Add(g);
                var mf = spun ? null : t.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) { boxes.Add((Vector3.zero, Vector3.zero)); continue; }
                var bb = mf.sharedMesh.bounds; var m = g.chain * Matrix4x4.TRS(g.restPos, g.restRot, g.scale);
                Vector3 lo = Vector3.one * 1e9f, hi = -Vector3.one * 1e9f;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.MultiplyPoint3x4(bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1)));
                    lo = Vector3.Min(lo, c); hi = Vector3.Max(hi, c);
                }
                boxes.Add((lo, hi));
            }
            // the belts: one per side of the vehicle's middle (the mean of the belt boxes' centres); a side's belt boxes are joined
            float mid = 0f; int nb = 0;
            for (int i = 0; i < nodes.Count; i++) if (!nodes[i].spun && boxes[i].hi.x > boxes[i].lo.x) { mid += (boxes[i].lo.x + boxes[i].hi.x) * 0.5f; nb++; }
            if (nb == 0) return;
            mid /= nb;
            for (int i = 0; i < nodes.Count; i++)
            {
                var g = nodes[i];
                float x = boxes[i].hi.x > boxes[i].lo.x ? (boxes[i].lo.x + boxes[i].hi.x) * 0.5f : (g.chain * Matrix4x4.TRS(g.restPos, g.restRot, g.scale)).GetColumn(3).x;
                g.side = x < mid ? 0 : 1;
                if (g.spun || !(boxes[i].hi.x > boxes[i].lo.x)) continue;
                if (g.side == 0) { lo0 = Vector3.Min(lo0, boxes[i].lo); hi0 = Vector3.Max(hi0, boxes[i].hi); any0 = true; }
                else { lo1 = Vector3.Min(lo1, boxes[i].lo); hi1 = Vector3.Max(hi1, boxes[i].hi); any1 = true; }
            }
            var belts = new List<GearBelt>();
            if (any0) belts.Add(MakeBelt(lo0, hi0));
            if (any1) belts.Add(MakeBelt(lo1, hi1));
            if (belts.Count == 0) return;
            if (belts.Count == 1) foreach (var g in nodes) g.side = 0;                                // one belt only: everything rides on it
            // the hull's bottom: the box (suspension space, at rest) of the meshes under the suspension node that are not gear, turret, guns,
            // antennas or wreck pieces; its four bottom corners, each resting on the belt of its side
            Vector3 hl = Vector3.one * 1e9f, hh = -Vector3.one * 1e9f; bool any = false;
            foreach (var mf in gearBody.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || GearSkip(mf.transform, nodes)) continue;
                var m = GearChainFrom(gearBody, mf.transform);
                var bb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.MultiplyPoint3x4(bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1)));
                    hl = Vector3.Min(hl, c); hh = Vector3.Max(hh, c); any = true;
                }
            }
            if (any)
            {
                gearHullBody = new[] { new Vector3(hl.x, hl.y, hl.z), new Vector3(hh.x, hl.y, hl.z), new Vector3(hl.x, hl.y, hh.z), new Vector3(hh.x, hl.y, hh.z) };
                var mb = GearChain(gearBody.parent, bp, br) * Matrix4x4.TRS(bp, br, gearBody.localScale);
                gearHullRoot = new Vector3[4]; gearHullSide = new int[4];
                for (int i = 0; i < 4; i++) { gearHullRoot[i] = mb.MultiplyPoint3x4(gearHullBody[i]); gearHullSide[i] = belts.Count > 1 && gearHullRoot[i].x >= mid ? 1 : 0; }
            }
            gearNodes = nodes.ToArray(); gearBelts = belts.ToArray(); gearSnap = true;
        }

        /// <summary>A belt from its box in the root's space at rest: the planting's samples (stations along its whole length at the box's
        /// bottom, both edges) and the proof's denser ones (three across).</summary>
        static GearBelt MakeBelt(Vector3 lo, Vector3 hi)
        {
            var b = new GearBelt { x0 = lo.x, x1 = hi.x, y0 = lo.y, z0 = lo.z, z1 = hi.z, xc = (lo.x + hi.x) * 0.5f };
            int n = Mathf.Max(3, TrackGround.Stations), np = Mathf.Max(n, TrackGround.ProofStations);
            b.pts = new Vector3[2 * n]; b.g = new float[2 * n]; b.hz = new float[n]; b.hg = new float[n];
            for (int j = 0; j < n; j++)
            {
                float z = Mathf.Lerp(lo.z, hi.z, j / (float)(n - 1));
                b.pts[2 * j] = new Vector3(lo.x, lo.y, z); b.pts[2 * j + 1] = new Vector3(hi.x, lo.y, z);
            }
            b.proof = new Vector3[3 * np];
            for (int j = 0; j < np; j++)
            {
                float z = Mathf.Lerp(lo.z, hi.z, j / (float)(np - 1));
                b.proof[3 * j] = new Vector3(lo.x, lo.y, z); b.proof[3 * j + 1] = new Vector3(b.xc, lo.y, z); b.proof[3 * j + 2] = new Vector3(hi.x, lo.y, z);
            }
            return b;
        }

        /// <summary>From `parent`'s space to the root's (the suspension node at its rest pose; every other node as it stands).</summary>
        Matrix4x4 GearChain(Transform parent, Vector3 bp, Quaternion br)
        {
            var path = new List<Transform>();
            for (var a = parent; a && a != transform; a = a.parent) path.Add(a);
            var m = Matrix4x4.identity;
            for (int i = path.Count - 1; i >= 0; i--)
            {
                var a = path[i]; bool body = a == gearBody;
                m = m * Matrix4x4.TRS(body ? bp : a.localPosition, body ? br : a.localRotation, a.localScale);
            }
            return m;
        }

        /// <summary>From `t`'s space to `top`'s (t under top).</summary>
        static Matrix4x4 GearChainFrom(Transform top, Transform t)
        {
            var m = Matrix4x4.identity;
            for (var a = t; a && a != top; a = a.parent) m = Matrix4x4.TRS(a.localPosition, a.localRotation, a.localScale) * m;
            return m;
        }

        static bool GearSkip(Transform t, List<GearNode> gear)
        {
            for (var a = t; a; a = a.parent)
            {
                foreach (var g in gear) if (g.t == a) return true;
                string n = a.name;
                if (n.Contains("Turret") || n.Contains("Barrel") || n.Contains("Recoil") || n.Contains("Muzzle") || n.Contains("Antenna") || n.Contains("Wreck") || n.StartsWith("frag_")) return true;
                if (n == "TankSuspension" || n == "Suspension" || n == "MLRSSuspension" || n == "Chassis" || n == "BuggyChassis") return false;
            }
            return false;
        }

        /// <summary>Proof (item 146, the Play tests; the k81c signature): this frame's running gear and hull. The first belt's world turn over
        /// its rest turn: its pitch and roll (deg, + = front / right side up); the bottom middle of that belt over the ground under it (m);
        /// the deepest of the proof points of every belt (three across, along its whole bottom) below the ground (m, + = below); the hull's
        /// pitch and roll over the first belt's frame (deg); the hull's lift. False = no planted gear on this view.</summary>
        public bool ProofGear(out float trackPitch, out float trackRoll, out float midHeight, out float sink, out float hullPitch, out float hullRoll, out float lift)
        {
            trackPitch = trackRoll = midHeight = hullPitch = hullRoll = 0f; sink = -9f; lift = GearHullLiftNow;
            if (!ProofBelts(out var pitch, out trackRoll, out sink, out _, out midHeight)) return false;
            trackPitch = pitch[0];
            GearNode first = null;
            foreach (var g in gearNodes) if (g.t && !g.spun && g.side == 0) { first = g; break; }
            if (first == null || !gearBody) return false;
            var F = first.t.rotation * Quaternion.Inverse(first.chain.rotation * first.restRot);
            motion.BodyRest(out _, out var br);
            var H = gearBody.rotation * Quaternion.Inverse(GearChain(gearBody.parent, Vector3.zero, br).rotation * br);
            var rel = Quaternion.Inverse(F) * H;
            hullPitch = -Mathf.Asin(Mathf.Clamp((rel * Vector3.forward).y, -1f, 1f)) * Mathf.Rad2Deg;
            hullRoll = Mathf.Asin(Mathf.Clamp((rel * Vector3.right).y, -1f, 1f)) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>Proof (k81d, menu 14): per belt (0 = the -x side, 1 = the +x side) its drawn pitch (deg, + = front up), the shared roll
        /// (deg, + = right side up), the deepest proof point of all belts below the ground (m, + = below), the largest gap under a belt (the
        /// belt's lowest proof point over the ground: the air under it, m) and the first belt's bottom middle over the ground (m). Read from
        /// the DRAWN belt nodes (their world pose), not from the planting's own numbers.</summary>
        public bool ProofBelts(out float[] pitch, out float roll, out float sink, out float gap, out float midHeight)
        {
            pitch = new float[2]; roll = 0f; sink = -9f; gap = 0f; midHeight = 0f;
            if (Obj == null || game == null || !motion || !TrackGround.Types.Contains(Obj.Type)) return false;
            if (!gearInit) GearInit();
            if (gearNodes == null || gearBelts == null || !gearBody) return false;
            float s = Mathf.Abs(transform.lossyScale.x); if (s < 1e-4f) s = 1f;
            for (int k = 0; k < gearBelts.Length; k++)
            {
                GearNode node = null;
                foreach (var g in gearNodes) if (g.t && !g.spun && g.side == k) { node = g; break; }
                if (node == null) continue;
                var F = node.t.rotation * Quaternion.Inverse(node.chain.rotation * node.restRot);   // the belt's world turn over its rest turn
                var at = node.t.position - F * ((Vector3)(node.chain * Matrix4x4.TRS(node.restPos, node.restRot, node.scale)).GetColumn(3) * s);   // where the belt's frame (the root's origin at rest) is drawn
                pitch[Mathf.Min(k, 1)] = -Mathf.Asin(Mathf.Clamp((F * Vector3.forward).y, -1f, 1f)) * Mathf.Rad2Deg;   // the model's front is -z
                if (k == 0) roll = Mathf.Asin(Mathf.Clamp((F * Vector3.right).y, -1f, 1f)) * Mathf.Rad2Deg;
                var b = gearBelts[k]; float low = 9f;
                foreach (var p in b.proof)
                {
                    var w = at + F * (p * s); float d = game.GroundY(w.x, w.z) - w.y;
                    sink = Mathf.Max(sink, d); low = Mathf.Min(low, -d);
                }
                gap = Mathf.Max(gap, low);
                if (k == 0) { var w = at + F * (new Vector3(b.xc, b.y0, (b.z0 + b.z1) * 0.5f) * s); midHeight = w.y - game.GroundY(w.x, w.z); }
            }
            return true;
        }
    }
}
