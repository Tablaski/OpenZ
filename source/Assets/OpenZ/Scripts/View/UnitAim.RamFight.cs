using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// With the
    /// rules' DV196 (Sim/ZObject.RamFight.cs). View only: it reads the rules, it never writes them.
    /// <list type="bullet">
    /// <item>Replaces the 1.5 big_tanks_fx ram clear (View/UnitAim.RamClear.cs: the turret turned 90 deg aside and the barrels went to their top
    /// stop for the whole ram, and to the top stop already in front of a target) while DV196 is on.</item>
    /// <item>AIM: during a ram the turret follows the gun's target as always (the rules' next enemy in range, never the rammed vehicle: DV188 /
    /// DV196) and its shots are drawn as always.</item>
    /// <item>HOLD: in a ram with no target the turret and the barrels stay where they are (no swing, no return to the front) until the ram is over
    /// (OzRamBarrels: the rammed vehicle's death drawn + 0.6 s); then the normal aim / idle.</item>
    /// <item>CLEAR (DV185 "the barrels never inside the target"): the barrels are lifted only as far as the rammed hull needs: the least
    /// elevation, in <see cref="LiftStepDeg"/> deg steps from the aim's (or the held) elevation, at which no barrel (its recoil node to its muzzle,
    /// <see cref="BarrelRadius"/> m round it, the measure of View/OzBigTanksFxPlayCheck.cs) is within <see cref="GoalMargin"/> m more of a mesh
    /// box of the vehicle it rams, a vehicle it doomed that is still drawn, or (before a ram) an enemy crewed vehicle in the ram's start zone; the
    /// goal is eased with the gun's own slew (hurried as the old clear), and every drawn frame a floor puts the barrels at the least clear
    /// elevation at once when the eased slew lags (<see cref="FloorFrames"/>).</item>
    /// <item>The hull's own pitch is not touched here (nothing raises the hull's nose for the barrels; the hull's ground pitch and its ram lead are
    /// View/OzBigTankPitch.cs and View/OzRamPush.cs, kit mammoth_pitch_n26).</item>
    /// </list>
    /// Switch <see cref="RamFightViewOn"/> (proof only) and the rules' ZWorld.RamFightDeviation; off: the big_tanks_fx clear as before.
    /// </summary>
    public partial class UnitAim
    {
        public static bool RamFightViewOn = true;
        /// <summary>The lift's step (deg), the barrel's radius (m, as the Play check measures), the goal's margin on top (m).</summary>
        public const float LiftStepDeg = 1.5f, BarrelRadius = 0.31f, GoalMargin = 0.15f;
        /// <summary>Proof: frames in a ram that held the turret (no target), frames it aimed at a target, frames the goal was lifted to clear a
        /// hull, frames the floor lifted the drawn barrels, frames a barrel stayed inside at the top stop (want 0).</summary>
        public static int HoldFrames, AimFrames, LiftFrames, FloorFrames, StuckFrames;
        /// <summary>Proof: the largest turn of a held turret from where its hold began (deg; the eased slew's coast only, want a few deg at most).</summary>
        public static float HoldSwingMaxDeg;
        /// <summary>The Play numbers in one line (OzBigTanksFx.Status style).</summary>
        public static string RamFightStatus() => $"DV196 view: ram frames aimed {AimFrames}, held {HoldFrames} (largest turn while held {HoldSwingMaxDeg:0.0} deg), goal lifted {LiftFrames}, floor lifted {FloorFrames}, barrel inside at the top stop {StuckFrames}; rules: looks {(ZGame.I != null && ZGame.I.World != null ? ZGame.I.World.RamFightLooks : 0)}, picks {(ZGame.I != null && ZGame.I.World != null ? ZGame.I.World.RamFightPicks : 0)}, drops {(ZGame.I != null && ZGame.I.World != null ? ZGame.I.World.RamFightDrops : 0)}";
        int rfFrame = -1, rfHoldFrame = -9; float rfHoldYaw;
        readonly List<ZObject> rfSet = new List<ZObject>();                                              // this turret's own (SolveGun: the view, then the floor)
        static readonly List<MeshRenderer> rfBuf = new List<MeshRenderer>();
        sealed class RfBoxes { public readonly List<MeshRenderer> r = new List<MeshRenderer>(); public readonly List<Mesh> m = new List<Mesh>(); public float at = -9f; }
        static readonly Dictionary<int, RfBoxes> rfBoxes = new Dictionary<int, RfBoxes>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RamFightReset() { rfBoxes.Clear(); HoldFrames = AimFrames = LiftFrames = FloorFrames = StuckFrames = 0; HoldSwingMaxDeg = 0f; }

        /// <summary>RamClear's first test (after the doomed lock): DV196 near or in a ram. True = handled (ret: the RamClear result: true = the gun
        /// is not on a target this frame, the goal held).</summary>
        bool RamFightView(ref float gy, ref float gp, out bool ret)
        {
            ret = false;
            if (!RamFightViewOn || !ZWorld.RamFightDeviation || obj == null || obj.Kind != ZKind.Vehicle || obj.Type != OzBigTanks.Mammoth || pitchJ == null || yawJ == null || game == null || game.World == null) return false;
            var st = OzRamBarrels.StateOf(obj);
            if (st == OzRamBarrels.Clear.None) { ramClearSide = 0f; return true; }                  // not near a ram: the normal aim (ret false)
            ramClearSide = 0f;
            var a = obj.AttackObject;
            bool aim = held.Count > 0 || (a != null && !a.Destroyed && a.KillAt < 0 && !obj.RamFireBarred(a));
            if (!aim && st == OzRamBarrels.Clear.Ram)
            {   // no target in the ram: it stays where it is
                gy = yawJ.ang; gp = pitchJ.ang; HoldFrames++;
                if (rfHoldFrame != Time.frameCount - 1) rfHoldYaw = yawJ.ang;
                rfHoldFrame = Time.frameCount;
                HoldSwingMaxDeg = Mathf.Max(HoldSwingMaxDeg, Mathf.Abs(Mathf.DeltaAngle(rfHoldYaw, yawJ.ang)));
            }
            else if (st == OzRamBarrels.Clear.Ram) AimFrames++;
            RamFightSet(st == OzRamBarrels.Clear.Pre);
            rfFrame = Time.frameCount;
            if (rfSet.Count == 0) { ret = !aim && st == OzRamBarrels.Clear.Ram; return true; }
            float g0 = gp;
            gp = ClearPitchAt(gy, gp, GoalMargin);                                                   // where the slew goes
            gp = ClearPitchAt(yawJ.ang, gp, GoalMargin);                                             // and where the barrels are now (the turret on its way)
            Pose(gy, gp, 0f);
            if (Mathf.Abs(Mathf.DeltaAngle(g0, gp)) > 0.01f) LiftFrames++;
            ret = !aim && st == OzRamBarrels.Clear.Ram;
            return true;
        }

        /// <summary>SolveGun, before the drawn pose: the floor (DV185): the drawn barrels never inside a hull of the ram, lifted at once when the
        /// eased slew lags. Only in a frame RamFightView ran for.</summary>
        float RamFightFloor(float ya, float pa)
        {
            if (rfFrame != Time.frameCount || rfSet.Count == 0 || pitchJ == null) return pa;
            float p = ClearPitchAt(ya, pa, 0f);
            if (Mathf.Abs(Mathf.DeltaAngle(pa, p)) > 0.001f) { pitchJ.ang = p; pitchJ.vel = 0f; FloorFrames++; }
            if (BarrelsIn(0f)) StuckFrames++;
            return p;
        }

        /// <summary>The least elevation at or above p (the gun's own stops) at which no barrel is within the radius + margin of a hull of the set.</summary>
        float ClearPitchAt(float yaw, float p, float margin)
        {
            p = ClampPitch(p);
            for (int k = 0; k < 60; k++)
            {
                Pose(yaw, p, 0f);
                if (!BarrelsIn(margin)) return p;
                float np = ClampPitch(p + LiftStepDeg * pitchSign);
                if (Mathf.Abs(np - p) < 0.001f) return p;                                           // the top stop
                p = np;
            }
            return p;
        }

        /// <summary>A barrel (recoil node to muzzle, else the pitch hinge to the muzzle) within BarrelRadius + margin of a mesh box of the set.</summary>
        bool BarrelsIn(float margin)
        {
            float rad = BarrelRadius + margin;
            for (int i = 0; i < muzzles.Count; i++)
            {
                var mz = muzzles[i]; if (!mz.t) continue;
                Vector3 a = mz.recoil ? mz.recoil.position : pitchJ.t.position, b = mz.t.position;
                var d = b - a; float len = d.magnitude; if (len < 1e-4f) continue;
                b += d / len * 0.02f;
                var lo = Vector3.Min(a, b) - Vector3.one * rad; var hi = Vector3.Max(a, b) + Vector3.one * rad;
                for (int s = 0; s < rfSet.Count; s++)
                {
                    var bx = BoxesOf(rfSet[s]); if (bx == null) continue;
                    for (int j = 0; j < bx.r.Count; j++)
                    {
                        var q = bx.r[j]; if (!q || !q.enabled || !q.gameObject.activeInHierarchy) continue;
                        var wb = q.bounds;
                        if (wb.max.x < lo.x || wb.min.x > hi.x || wb.max.y < lo.y || wb.min.y > hi.y || wb.max.z < lo.z || wb.min.z > hi.z) continue;   // far from this barrel
                        var mb = bx.m[j].bounds; var tr = q.transform; var sc = tr.lossyScale;
                        for (int k = 0; k <= 24; k++)
                        {
                            var lp = tr.InverseTransformPoint(Vector3.Lerp(a, b, k / 24f));
                            var dd = Vector3.Scale(Vector3.Max(Vector3.zero, Vector3.Max(mb.min - lp, lp - mb.max)), sc);
                            if (dd.magnitude < rad) return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>The mesh boxes of a vehicle's drawn model (each renderer's own mesh box, turned with it; shadows left out), re-read every 0.5 s.</summary>
        RfBoxes BoxesOf(ZObject o)
        {
            if (o == null || !game.Views.TryGetValue(o.Id, out var v) || !v) return null;
            if (!rfBoxes.TryGetValue(o.Id, out var bx)) { if (rfBoxes.Count > 32) rfBoxes.Clear(); rfBoxes[o.Id] = bx = new RfBoxes(); }
            if (Time.time - bx.at > 0.5f || Time.time < bx.at)
            {
                bx.at = Time.time; bx.r.Clear(); bx.m.Clear();
                v.GetComponentsInChildren(false, rfBuf);
                foreach (var q in rfBuf)
                {
                    if (!q || q.name.ToLowerInvariant().Contains("shadow")) continue;
                    var mf = q.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                    bx.r.Add(q); bx.m.Add(mf.sharedMesh);
                }
                rfBuf.Clear();
            }
            return bx;
        }

        /// <summary>The hulls the barrels keep clear of: the vehicle it rams, every vehicle it doomed still drawn, and before a ram (pre) the enemy
        /// crewed vehicles in the ram's start zone (OzRamBarrels' test).</summary>
        void RamFightSet(bool pre)
        {
            rfSet.Clear();
            var w = game.World; var all = w.Objects;
            if (obj.Ramming) { var t = w.Get(obj.RamTargetId); if (t != null && !t.Destroyed) rfSet.Add(t); }
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o == null || o == obj || o.Destroyed || rfSet.Contains(o)) continue;
                if (o.RamDoomBy == obj.Id || (pre && InStartZone(obj, o))) rfSet.Add(o);
            }
        }
        /// <summary>OzRamBarrels' start-zone test for one vehicle: an enemy crewed vehicle inside the ram's start arc and gap, a little wider (whole numbers).</summary>
        static bool InStartZone(ZObject m, ZObject t)
        {
            if (t.Kind != ZKind.Vehicle || t.Owner == Team.Neutral || t.Owner == m.Owner || t.Drivers.Count == 0 || t.IsShuttle) return false;
            double fx = m.FaceX, fy = m.FaceY, fl = System.Math.Sqrt(fx * fx + fy * fy);
            if (fl < 1e-6) return false;
            long hx = (long)System.Math.Round(fx * 1024.0 / fl), hy = (long)System.Math.Round(fy * 1024.0 / fl);
            long tanMilli = (long)System.Math.Round(System.Math.Tan((ZObject.RamStartArcDeg + OzRamBarrels.PreArcMoreDeg) * System.Math.PI / 180.0) * 1000.0);
            long dx = t.CenterX - m.CenterX, dy = t.CenterY - m.CenterY, rr = ZPathing.Radius(m) + ZPathing.Radius(t) + ZObject.RamStartGapPx + OzRamBarrels.PreGapMorePx;
            if (dx * dx + dy * dy > rr * rr) return false;
            long dot = dx * hx + dy * hy; if (dot <= 0) return false;
            long cross = dx * hy - dy * hx; if (cross < 0) cross = -cross;
            return cross * 1000 <= dot * tanMilli;
        }
    }
}
