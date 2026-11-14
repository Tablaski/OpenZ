using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV163, level 7, the view's half of the dragons' attack (the rule: Sim/ZWorld.Dragons.cs). The dragon stays a bird of this class. Here it
    /// looks along its own straight line while it soars and, when a robot or a flying shuttle happens to be there on screen after the first
    /// minute, plans a pass like a swoop and asks the rules, once. Nothing in Soar, TryStartSwoop or the routes looks at a robot: the fire point
    /// lies on the line it was already on, so it never turns toward anyone, it only sinks. While a pass runs the drawn bird stays in BS.Swoop on
    /// the rules' path, on the rules' clock, and draws what the rules say (ZEventType.DragonEvent): the Pyro's own jet from its mouth, robots'
    /// shots, embers, its death. The shuttle guards below keep every dragon clear of a hull, in and out of a pass. No other level has a dragon,
    /// and with no dragon and no ship nothing here runs.
    /// </summary>
    public partial class AmbientLife
    {
        const float DgLookEvery = 0.25f, DgAheadMin = 14f, DgAheadMax = 28f, DgSideMax = 7f, DgFireClear = 9.5f, DgFireSpeed = 6f, DgSinkSpeed = 7f, DgT1Min = 2.4f, DgT1Max = 4f, DgT2 = 3.4f, DgScreen = 0.05f;
        const float DgShipPathM = 30f, DgShipSwoopM = 16f, DgFloorOver = 7f, DgShipDropMin = 8f, DgShipDropMax = 18f, DgShipFallM = 8f, DgShipPushMps = 4f, DgShipLandM = 6f, DgUnderHull = 3.5f;
        /// <summary>The shield round a hull (DragonShield): it starts this far off the box, pushes up to this fast close in, and never lets the wings nearer than this; the most a dragon may still fall as it leaves a path.</summary>
        const float DgShieldOut = 9f, DgShieldMps = 14f, DgShieldEdge = 0.4f, DgLeaveVy = -1.5f;
        const float DgNodIn = 0.25f, DgNodPitch = 14f, DgNodSpine2 = 14f, DgNodSpine3 = 18f, DgNodHead = 22f, DgHitEvery = 0.4f;
        /// <summary>The snout's tip in the head bone's own space, measured on LavaDragon.fbx (1 Nov): the 148 vertices weighted only to the head, through its bind pose. They ride the head rigidly, so any pose and any nod keep it on the snout; the bone runs along the snout (+Y), the rig has no jaw.</summary>
        static readonly Vector3 DgMouthHead = new Vector3(0f, 0.31f, -0.02f);

        struct DgShip { public ZObject o; public Transform tr; public Vector3 root; public bool air; public Quaternion rot; }
        readonly List<DgShip> dgShips = new List<DgShip>();
        float dgShipT, dgHitAt = -10f;
        bool anyDragon;

        // ------------------------------------------------------------------ rig
        void DragonRigBones(Bird b)
        {
            anyDragon = true;
            var rig = b.rig;
            b.dgSp2 = Find(rig.model, "spine_2"); b.dgSp3 = Find(rig.model, "spine_3");
            b.dgMouthL = DgMouthHead;
            b.dgMouthF = () => DragonMouth(b);
        }

        /// <summary>The drawn mouth this frame (the head bone rides the clip and the fire pose).</summary>
        Vector3 DragonMouth(Bird b) => b.head && b.rig.shown ? b.head.TransformPoint(b.dgMouthL) : b.pos + Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward * (float)ZWorld.DragonMouthAhead + Vector3.down * (float)ZWorld.DragonMouthBelow;

        /// <summary>PoseBird, after the clip: the neck bends down while the flame is on. The clip rewrites these bones every frame, so nothing builds up.</summary>
        void DragonNod(Bird b, Rig rig)
        {
            float n = b.dgNod;
            if (n <= 0.001f) return;
            var right = rig.root.right;
            if (b.dgSp2) b.dgSp2.rotation = Quaternion.AngleAxis(DgNodSpine2 * n, right) * b.dgSp2.rotation;
            if (b.dgSp3) b.dgSp3.rotation = Quaternion.AngleAxis(DgNodSpine3 * n, right) * b.dgSp3.rotation;
            if (b.head) b.head.rotation = Quaternion.AngleAxis(DgNodHead * n, right) * b.head.rotation;
        }

        // ------------------------------------------------------------------ the clock of the pass
        /// <summary>FollowPath's clock: frame time for every bird; during a pass the rules' time, eased so the 10 ms ticks do not show as a shiver.</summary>
        float DragonClock(Bird b, float dt)
        {
            if (!b.simPass) return b.pt + dt;
            var w = game.World; var p = w.DragonPass;
            if (p == null || p.Serial != b.dgSerial) { b.simPass = false; return b.pt + dt; }
            float want = (float)(w.Time - p.StartAt) + game.TickAlpha * (float)ZConst.TICK, next = b.pt + dt, e = want - next;
            return Mathf.Abs(e) > 0.2f ? want : next + e * Mathf.Min(1f, 8f * dt);
        }

        // ------------------------------------------------------------------ each frame
        void DragonStep(float dt)
        {
            if (!anyDragon) return;
            var w = game.World;
            dgShipT -= dt;
            if (dgShipT <= 0f) { dgShipT = 0.2f; DragonShipsRefresh(w); }
            DragonShipsFollow();
            OzDragonShots.Step();
            for (int i = 0; i < birds.Count; i++)
            {
                var b = birds[i];
                if (!b.dragon) continue;
                DragonScream(b);                                                                          // 1.4 ptero_scream: a pass's scream still due plays when the dragon comes on screen
                b.dgNod = Mathf.MoveTowards(b.dgNod, b.dgFlame ? 1f : 0f, dt / DgNodIn);
                if (b.simPass) b.pitchExtra = DgNodPitch * b.dgNod;
                if (!b.simPass) DragonShield(b, dt);                                                  // a ship can fly into a dragon on a plain path too; a pass of the rules breaks off by the rules;                          // on a path its own guards keep it off: the rules' break-off, DragonSwoopAway
                if (OzDragonProof.Recording) OzDragonProof.NoteGap(DragonHullGap(b));
                b.dgLookT -= dt;
                if (b.dgLookT > 0f) continue;
                b.dgLookT = DgLookEvery;
                if (b.st != BS.Soar || OzDragonProof.NoAsks || !w.DragonMayAsk(b.id)) continue;
                DragonLook(b);
            }
        }

        // ------------------------------------------------------------------ the look: only what lies on its own line
        bool InViewport(Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.x > DgScreen && v.x < 1f - DgScreen && v.y > DgScreen && v.y < 1f - DgScreen;
        }

        void DragonLook(Bird b)
        {
            if (!cam) return;
            var fwd = Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward;
            if (DragonLookShip(b, fwd)) return;
            if (!game.World.DragonMayAskRobot(b.id)) return;                                              // a flying shuttle has the shorter gap: robots wait out their own
            UnitView best = null; float bestSide = DgSideMax, bestAlong = 0f;
            foreach (var v in game.LiveViewValues)
            {
                if (!v || v.Dying || v.Obj == null || !v.gameObject.activeInHierarchy || v.Obj.Kind != ZKind.Robot || !ZWorld.DragonTargetOk(v.Obj)) continue;
                var d = v.transform.position - b.pos;
                float along = d.x * fwd.x + d.z * fwd.z;
                if (along < DgAheadMin || along > DgAheadMax) continue;
                float side = Mathf.Abs(d.x * fwd.z - d.z * fwd.x);
                if (side >= bestSide) continue;
                best = v; bestSide = side; bestAlong = along;
            }
            if (!best) return;
            float fireAt = bestAlong - (float)ZWorld.DragonFireShortM;
            var fire = b.pos + fwd * fireAt; float gy = Surface(fire.x, fire.z); fire.y = gy + 1f;
            OzDragonProof.Encounters++;
            if (!InViewport(best.transform.position + Vector3.up) || !InViewport(fire)) { OzDragonProof.OffScreen++; return; }
            if (!DragonPlanDip(b, fwd, fireAt, gy)) { OzDragonProof.NoPath++; return; }
            DragonAsk(b, best.Obj, gy, bestAlong, bestSide);
        }

        /// <summary>A flying shuttle ahead on its line and under it. The dragon stays at its own height: it is already above the ship.</summary>
        bool DragonLookShip(Bird b, Vector3 fwd)
        {
            for (int i = 0; i < dgShips.Count; i++)
            {
                var s = dgShips[i];
                if (!s.air || !ZWorld.DragonTargetOk(s.o)) continue;
                var mid = s.root + Vector3.up * OzShuttleAim.MidM; var d = mid - b.pos;
                float along = d.x * fwd.x + d.z * fwd.z, side = Mathf.Abs(d.x * fwd.z - d.z * fwd.x), drop = -d.y;
                if (along < DgAheadMin || along > DgAheadMax || side > DgSideMax || drop < DgShipDropMin || drop > DgShipDropMax) continue;
                OzDragonProof.Encounters++;
                if (!InViewport(mid)) { OzDragonProof.OffScreen++; continue; }
                if (!DragonPlanLevel(b, fwd, along - (float)ZWorld.DragonFireShortM)) { OzDragonProof.NoPath++; continue; }
                DragonAsk(b, s.o, Surface(mid.x, mid.z), along, side);
                return true;
            }
            return false;
        }

        /// <summary>The dip: on its own line to the fire height over the point nearest the robot, slow there, out and up. Same shape, same checks as a swoop.</summary>
        bool DragonPlanDip(Bird b, Vector3 fwd, float along, float gy)
        {
            var fire = b.pos + fwd * along;
            b.h0 = b.pos; b.m0 = b.vel;
            b.h1 = new Vector3(fire.x, gy + DgFireClear, fire.z); b.m1 = fwd * DgFireSpeed;
            b.t1 = Mathf.Clamp(Vector3.Distance(b.h0, b.h1) / DgSinkSpeed, DgT1Min, DgT1Max); b.t2 = DgT2;
            bool clear = false;
            for (int k = 0; k < 3 && !clear; k++)
            {
                var d = Quaternion.Euler(0f, k == 0 ? 0f : k == 1 ? -40f : 40f, 0f) * fwd;
                b.h2 = b.h1 + d * 30f + Vector3.up * Mathf.Max(6f, (b.alt - b.h1.y) * 0.5f); b.m2 = d * 8f + Vector3.up * 3.5f;
                clear = PathClear(b, 2f, false);
                for (int s = 1; s <= 4 && clear; s++)
                {
                    var q = b.h2 + d * (8f * s);
                    clear = Surface(q.x, q.z) + b.clear < b.h2.y + 2.2f * (8f * s) / b.cruise;
                }
            }
            return clear && !DragonShipOnPath(b);
        }

        /// <summary>The pass over a ship: level, at the dragon's own height, slower over the fire point.</summary>
        bool DragonPlanLevel(Bird b, Vector3 fwd, float along)
        {
            b.h0 = b.pos; b.m0 = b.vel;
            b.h1 = b.pos + fwd * along; b.m1 = fwd * DgFireSpeed;
            b.t1 = Mathf.Clamp(along / DgSinkSpeed, DgT1Min, DgT1Max); b.t2 = DgT2;
            b.h2 = b.h1 + fwd * 24f + Vector3.up * 2f; b.m2 = fwd * 8f;
            return PathClear(b, 2f, false);
        }

        /// <summary>Asks the rules with the path now in the bird. Accepted: it flies the rules' rounded path from this frame on.</summary>
        int DragonAsk(Bird b, ZObject target, float gy, float along, float side)
        {
            var q = new ZDragonOrder { Dragon = b.id, Target = target.Id, T1 = b.t1, T2 = b.t2, GroundY = gy };
            Vector3[] pts = { b.h0, b.m0, b.h1, b.m1, b.h2, b.m2 };
            for (int i = 0; i < 6; i++) { q.Path[i * 3] = pts[i].x; q.Path[i * 3 + 1] = pts[i].y; q.Path[i * 3 + 2] = pts[i].z; }
            int r = game.World.DragonOrder(q);
            OzDragonProof.NoteAnswer(r);
            if (r != ZWorld.DragonAccepted) return r;
            Vector3 P(int i) => new Vector3((float)q.Path[i * 3], (float)q.Path[i * 3 + 1], (float)q.Path[i * 3 + 2]);
            b.h0 = P(0); b.m0 = P(1); b.h1 = P(2); b.m1 = P(3); b.h2 = P(4); b.m2 = P(5); b.t1 = (float)q.T1; b.t2 = (float)q.T2;
            b.simPass = true; b.dgSerial = game.World.DragonPass.Serial; b.dgFlame = false; b.pt = 0f;
            b.st = BS.Swoop; b.stT = 0f; b.tuckW = 0f; b.flapFromTuck = false;
            OzDragonScream.Start(b.id); DragonScream(b);                                              // the attack's start: 1.4 ptero_scream, one of the owner's three screams at the dragon when it is on screen (AmbientLife.DragonScream.cs); the call and the old scream no longer play here
            var pv = target.IsShuttle ? b.h1 : game.Views.TryGetValue(target.Id, out var tv) && tv ? tv.transform.position : b.h1;
            OzDragonProof.NoteOrder(game.World.Time, b.id, target, along, side, Mathf.DeltaAngle(b.yaw, Mathf.Atan2(b.h1.x - b.h0.x, b.h1.z - b.h0.z) * Mathf.Rad2Deg), cam ? cam.WorldToViewportPoint(pv) : Vector3.zero, cam ? cam.WorldToViewportPoint(b.h1) : Vector3.zero);
            Debug.Log($"[OpenZ Dragon] order t={game.World.Time:F2} tick={game.World.TickIndex} dragon={b.id} target={target.Id} {target.Type} t1={q.T1} t2={q.T2} ground={q.GroundY} path={string.Join(",", q.Path)}");
            return r;
        }

        // ------------------------------------------------------------------ what the rules say
        void DragonSeen(ZEvent e)
        {
            var w = game.World; var k = ZWorld.DragonKindOf(e); int id = ZWorld.DragonOf(e);
            Bird b = null;
            for (int i = 0; i < birds.Count && b == null; i++) if (birds[i].dragon && birds[i].id == id) b = birds[i];
            if (b == null) return;
            var a = w.GetAny(e.objId); var t = w.GetAny(e.otherId);
            OzDragonProof.NoteEvent(k, e);
            switch (k)
            {
                case ZDragonKind.FlameOn:
                    b.dgFlame = true;
                    if (t != null) DragonFlame(b, t, DragonEnd(t, DragonMouth(b)), false);
                    break;
                case ZDragonKind.Bite:
                {
                    var mouth = DragonMouth(b);
                    Vector3 to;
                    if (e.flag && t != null) to = DragonEnd(t, mouth);
                    else if (t != null && OzShuttleAim.Air(t)) to = OzShuttleAim.FlameMiss(mouth, t, OpenZPyroFire.Range);
                    else { to = ZGame.PxToWorld(e.x, e.y); to.y = Surface(to.x, to.z) + 0.15f; }
                    OzDragonProof.NoteBite(Vector3.Angle(to - mouth, Vector3.down), e.flag, mouth.y - Surface(mouth.x, mouth.z), to.y - Surface(to.x, to.z), mouth, to);
                    DragonFlame(b, t, to, e.flag);
                    break;
                }
                case ZDragonKind.Splash:
                    if (t != null && game.Views.TryGetValue(t.Id, out var sv) && sv) OpenZPyroFire.DragonSplash(DragonJet(b), DragonMouth(b), t, sv.AimPos());
                    break;
                case ZDragonKind.FlameOff: b.dgFlame = false; OpenZPyroFire.DragonStop(DragonJet(b)); break;
                case ZDragonKind.Shot: OzDragonShots.Draw(game, e, a, w.DragonPass, b.pos, b.rig.root); break;
                case ZDragonKind.Hit:
                    DragonEmbers(b.pos, 8);
                    if (Time.time - dgHitAt >= DgHitEvery) { dgHitAt = Time.time; Sound(SndDragonHit, b.pos, 0.45f, 300 + b.id); }
                    break;
                case ZDragonKind.End: b.simPass = false; break;
                case ZDragonKind.Down:
                    b.simPass = false; b.dgFlame = false; OpenZPyroFire.DragonStop(DragonJet(b));
                    KillBird(b, b.pos + Vector3.down * 2f, false);
                    break;
                case ZDragonKind.BreakOff:
                    b.simPass = false; b.dgFlame = false; OpenZPyroFire.DragonStop(DragonJet(b));
                    DragonLeavePath(b);                                                                   // the raised floor takes it up
                    Sound(SndDragonCall, b.pos, 0.6f, 100 + b.id);
                    break;
            }
        }

        static int DragonJet(Bird b) => -1000 - b.id;

        /// <summary>Where a flame on this target ends: the robot's aim point, the hull of the flying ship.</summary>
        Vector3 DragonEnd(ZObject t, Vector3 mouth)
        {
            if (OzShuttleAim.Air(t)) return OzShuttleAim.HitAim(t, mouth);
            return game.Views.TryGetValue(t.Id, out var v) && v ? v.AimPos() : game.CenterWorld(t) + Vector3.up * (float)ZWorld.DragonBodyY;
        }

        void DragonFlame(Bird b, ZObject t, Vector3 to, bool hit)
        {
            if (OpenZPyroFire.DragonCue(DragonJet(b), b.dgMouthF, b.head, t, to, hit)) return;
            if (fx) fx.Flame(DragonMouth(b), to);                                                     // the same fallback as a Pyro's when the fire system is off
            CombatAudio.I?.Play("flame", to, 0.75f, 0.9f, DragonJet(b), "flame_loop1");
        }

        /// <summary>The drawn dragon of the running pass, for the robots' guns (UnitAim.Dragon.cs).</summary>
        public bool DragonAimPoint(out Vector3 p)
        {
            for (int i = 0; i < birds.Count; i++) if (birds[i].simPass) { p = birds[i].pos; return true; }
            p = default; return false;
        }

        // ------------------------------------------------------------------ never overlap a shuttle
        void DragonShipsRefresh(ZWorld w)
        {
            dgShips.Clear();
            if (w.ShuttlesMadeCount == 0) return;
            foreach (var o in w.Objects)
            {
                if (!o.IsShuttle || o.Destroyed || o.KillAt >= 0) continue;
                var sv = OzPlayableShuttle.Of(o);
                var root = sv ? sv.transform.position : game.CenterWorld(o);
                dgShips.Add(new DgShip { o = o, tr = sv ? sv.transform : null, root = root, air = o.ShuttleAirborne, rot = sv ? sv.transform.rotation : Quaternion.identity });
            }
        }

        /// <summary>Each frame: the drawn hull where it is now. The list itself is refreshed every 0.2 s.</summary>
        void DragonShipsFollow()
        {
            for (int i = 0; i < dgShips.Count; i++)
            {
                var s = dgShips[i];
                if (!s.tr) continue;
                s.root = s.tr.position; s.rot = s.tr.rotation; dgShips[i] = s;
            }
        }

        float ShipFlatSq(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return dx * dx + dz * dz; }

        /// <summary>Fly: with a flying ship near, at least DgFloorOver m over the top of its hull.</summary>
        float DragonFloor(float floor, Vector3 p)
        {
            for (int i = 0; i < dgShips.Count; i++)
            {
                var s = dgShips[i];
                if (s.air && ShipFlatSq(s.root, p) < DgShipPathM * DgShipPathM) floor = Mathf.Max(floor, s.root.y + OzShuttleAim.HullMax.y + DgFloorOver);
            }
            return floor;
        }

        /// <summary>Fly: a dragon under a flying ship stays under it until the ship has passed: it never climbs through the hull.</summary>
        float DragonCeiling(float want, Vector3 p)
        {
            for (int i = 0; i < dgShips.Count; i++)
            {
                var s = dgShips[i];
                if (s.air && p.y < s.root.y && ShipFlatSq(s.root, p) < DgShipSwoopM * DgShipSwoopM) want = Mathf.Min(want, s.root.y - DgUnderHull);
            }
            return want;
        }

        /// <summary>A flying ship within r m of p (flat distance). Used for swoops: no swoop near one, a plain swoop climbs away from one.</summary>
        bool DragonShipNear(Vector3 p, float r)
        {
            for (int i = 0; i < dgShips.Count; i++) if (dgShips[i].air && ShipFlatSq(dgShips[i].root, p) < r * r) return true;
            return false;
        }

        /// <summary>A dragon leaves its path near a ship: it pulls out of the dive at once. Left at the path's fall (about 10 m/s) it could clear neither the hull nor the ground: Fly changes vy by 4 m/s a second.</summary>
        void DragonLeavePath(Bird b) { EndPath(b); b.vy = Mathf.Max(b.vy, DgLeaveVy); }

        /// <summary>Swoop, each frame: a plain swoop with a flying ship near leaves its path. A pass of the rules breaks off by the rules.</summary>
        bool DragonSwoopAway(Bird b)
        {
            if (b.simPass || !DragonShipNear(b.pos, DgShipSwoopM)) return false;
            DragonLeavePath(b); OzDragonProof.ClimbAways++;
            return true;
        }

        /// <summary>Where p lies off the ship's hull box, in the ship's own axes (zero inside the box).</summary>
        static Vector3 ShipBoxOff(in DgShip s, Vector3 p)
        {
            var l = Quaternion.Inverse(s.rot) * (p - s.root);
            return l - new Vector3(Mathf.Clamp(l.x, OzShuttleAim.HullMin.x, OzShuttleAim.HullMax.x), Mathf.Clamp(l.y, OzShuttleAim.HullMin.y, OzShuttleAim.HullMax.y), Mathf.Clamp(l.z, OzShuttleAim.HullMin.z, OzShuttleAim.HullMax.z));
        }

        /// <summary>The last guard, for a dragon flying free or falling, every frame, flying or landed ship, from any side: inside DgShieldOut m of a hull it is pushed straight off it, harder the closer it gets, and
        /// the wings (half the span) never come nearer than DgShieldEdge m. A ship that flies at a dragon meets the same. The port (tools/overlap_fixed.py) never needs the hard edge; it is the guarantee, not the plan.</summary>
        void DragonShield(Bird b, float dt)
        {
            for (int i = 0; i < dgShips.Count; i++)
            {
                var s = dgShips[i];
                var off = ShipBoxOff(s, b.pos);
                float dist = off.magnitude;
                if (dist >= DgShieldOut) continue;
                var n = dist > 1e-4f ? off / dist : Vector3.up;
                float edge = b.span * 0.5f + DgShieldEdge, k = Mathf.Clamp01((DgShieldOut - dist) / (DgShieldOut - edge)), step = DgShieldMps * k * k * dt;   // squared: a soft start, no kick at the rim
                if (dist + step < edge) { step = edge - dist; OzDragonProof.ShieldHeld++; }
                b.pos += s.rot * n * step;
            }
        }

        /// <summary>No flying ship within DgShipPathM m of the planned path, a dip or a plain swoop.</summary>
        bool DragonShipOnPath(Bird b)
        {
            for (int k = 0; k <= 8; k++)
            {
                float u = k / 8f;
                if (DragonShipNear(Hermite(b.h0, b.m0 * b.t1, b.h1, b.m1 * b.t1, u), DgShipPathM) || DragonShipNear(Hermite(b.h1, b.m1 * b.t2, b.h2, b.m2 * b.t2, u), DgShipPathM)) return true;
            }
            return false;
        }

        /// <summary>Blocked: a landed ship stands in the way like a low building. No ship on the map, no dragon level: false at once.</summary>
        bool DragonShipBlocks(Vector3 p, float grow)
        {
            for (int i = 0; i < dgShips.Count; i++)
            {
                var s = dgShips[i];
                if (s.air) continue;
                float dx = Mathf.Abs(p.x - s.root.x), dz = Mathf.Abs(p.z - s.root.z), half = OzShuttleAim.HullMax.z + grow;
                if (dx < half && dz < half && p.y < s.root.y + DgShipLandM + 1f) return true;
            }
            return false;
        }

        /// <summary>A dead dragon falling over a hull slides off to the side (the shield alone would hold it up there).</summary>
        void DragonFallPush(Bird b, float dt)
        {
            for (int i = 0; i < dgShips.Count; i++)
            {
                var s = dgShips[i];
                if (b.pos.y < s.root.y || ShipFlatSq(s.root, b.pos) >= DgShipFallM * DgShipFallM) continue;
                var away = b.pos - s.root; away.y = 0f; away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.right;
                b.pos += away * (DgShipPushMps * dt);
            }
        }

        /// <summary>The smallest gap (m) from this dragon, wings spread, to any drawn hull box. The Play check's number.</summary>
        float DragonHullGap(Bird b)
        {
            float best = 999f;
            for (int i = 0; i < dgShips.Count; i++) best = Mathf.Min(best, ShipBoxOff(dgShips[i], b.pos).magnitude - b.span * 0.5f);
            return best;
        }

        // ------------------------------------------------------------------ falls
        /// <summary>Fall: a dead dragon that comes down on lava is gone in a splash of it.</summary>
        bool DragonIntoLava(Bird b, Vector3 at)
        {
            var map = game.World.Map;
            if (!map.LavaAt(Mathf.FloorToInt(at.x / 2f), Mathf.FloorToInt(-at.z / 2f))) return false;
            var z = OpenZFxPlayer.I; if (z && z.isActiveAndEnabled) z.LavaHit(at, 0.9f, true);
            Sound(OzLava.SndMelt, at, 0.9f, 400 + b.id);
            OzDragonProof.IntoLava++;
            Gone(b);
            return true;
        }

        // ------------------------------------------------------------------ the Play check's handles
        /// <summary>Dragons alive now.</summary>
        public int ProofAlive() { int n = 0; for (int i = 0; i < birds.Count; i++) if (birds[i].dragon && birds[i].Alive) n++; return n; }
        /// <summary>A dragon in the state: flying plain, on a pass, falling or down, gone.</summary>
        public int ProofCount(string st)
        {
            int n = 0;
            for (int i = 0; i < birds.Count; i++)
            {
                var b = birds[i]; if (!b.dragon) continue;
                if (st == "pass" ? b.simPass : st == "fall" ? b.st == BS.Fall || b.st == BS.Down || b.st == BS.Gone : st == "soar" && b.st == BS.Soar) n++;
            }
            return n;
        }
        /// <summary>The drawn position of the first dragon on a pass, or the first alive.</summary>
        public Vector3 ProofPos() { Bird a = null; foreach (var b in birds) if (b.dragon && b.Alive && (a == null || b.simPass)) a = b; return a != null ? a.pos : Vector3.zero; }

        /// <summary>The check's pass: the first soaring dragon is put on a line to the target, the normal plan is made and the rules asked (with their chance and cooldowns set aside by the
        /// check itself: ZWorld.DragonProofForce). Returns the rules' answer, -1 = no dragon free or no clear path. A shuttle target keeps the dragon at height.</summary>
        public int ProofPass(ZObject target, float approachDeg)
        {
            var w = game.World;
            Bird b = null;
            for (int i = 0; i < birds.Count && b == null; i++) if (birds[i].dragon && birds[i].st == BS.Soar) b = birds[i];
            if (b == null || target == null) return -1;
            var at = game.Views.TryGetValue(target.Id, out var v) && v ? v.transform.position : game.CenterWorld(target);
            for (int k = 0; k < 8; k++)
            {
                float yaw = approachDeg + 45f * k;
                var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                const float along = 20f;
                if (target.IsShuttle)
                {
                    var mid = at + Vector3.up * OzShuttleAim.MidM;
                    b.pos = mid - fwd * along + Vector3.up * 13f; b.yaw = yaw; b.vel = fwd * b.cruise; b.speed = b.cruise; b.vy = 0f;
                    if (DragonPlanLevel(b, fwd, along - (float)ZWorld.DragonFireShortM)) return DragonAsk(b, target, Surface(mid.x, mid.z), along, 0f);
                }
                else
                {
                    float gy = Surface(at.x, at.z);
                    b.pos = at - fwd * along + Vector3.up * (b.alt + gy); b.yaw = yaw; b.vel = fwd * b.cruise; b.speed = b.cruise; b.vy = 0f;
                    if (DragonPlanDip(b, fwd, along - (float)ZWorld.DragonFireShortM, gy)) return DragonAsk(b, target, gy, along, 0f);
                }
            }
            return -1;
        }

        /// <summary>The clip stage's handle (Editor/OzDragonClipStage.cs): the dragon on a pass, its drawn mouth, head bone, neck (spine_3), heading and wingspan, and whether its flame is on.
        /// False = no pass, or the dragon is not drawn.</summary>
        public bool ProofMouth(out Vector3 mouth, out Vector3 head, out Vector3 neck, out Vector3 fwd, out float span, out bool flame)
        {
            mouth = head = neck = fwd = default; span = 0f; flame = false;
            for (int i = 0; i < birds.Count; i++)
            {
                var b = birds[i];
                if (!b.dragon || !b.simPass || !b.head || !b.rig.shown) continue;
                mouth = DragonMouth(b); head = b.head.position; neck = b.dgSp3 ? b.dgSp3.position : b.pos; fwd = Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward;
                span = b.span; flame = b.dgFlame;
                return true;
            }
            return false;
        }

        /// <summary>The check's plain swoop with the start guard taken away: the first soaring dragon, from 34 m out along yaw, down to 2.8 m over the ground at `at`. Swoop's own guard and the shield must do the rest. False = no dragon free.</summary>
        public bool ProofSwoop(Vector3 at, float yaw)
        {
            Bird b = null;
            for (int i = 0; i < birds.Count && b == null; i++) if (birds[i].dragon && birds[i].st == BS.Soar) b = birds[i];
            if (b == null) return false;
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var g = new Vector3(at.x, Surface(at.x, at.z), at.z);
            b.pos = g - fwd * 34f + Vector3.up * b.alt; b.yaw = yaw; b.vel = fwd * b.cruise; b.speed = b.cruise; b.vy = 0f;
            b.h0 = b.pos; b.m0 = b.vel; b.h1 = g + Vector3.up * 2.8f; b.m1 = fwd * 13f;
            b.t1 = Mathf.Clamp(Vector3.Distance(b.h0, b.h1) / 11f, 2.4f, 5.5f); b.t2 = DgT2; b.pt = 0f;
            b.h2 = b.h1 + fwd * 30f + Vector3.up * Mathf.Max(6f, (b.alt - b.h1.y) * 0.5f); b.m2 = fwd * 8f + Vector3.up * 3.5f;
            b.st = BS.Swoop; b.stT = 0f; b.tuckW = 0f; b.flapFromTuck = false;
            return true;
        }

        /// <summary>The critters' own clock: it follows the game's speed up to 2. A check that wants true flight reads it against the rules' time.</summary>
        public float ProofClock() => clock;

        /// <summary>The check: the rules' answer for a target it must refuse (a vehicle, a landed ship, too early). No dragon moves.</summary>
        public int ProofAskOnly(ZObject target)
        {
            var q = new ZDragonOrder { Dragon = 0, Target = target.Id, T1 = 3, T2 = 3, GroundY = 0 };
            return game.World.DragonOrder(q);
        }
    }
}
