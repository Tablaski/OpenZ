using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// only, no rule changes: the ENTER_FORT order itself is Zod's (PlayerInput.BuildWaypoint, zobject.cpp:2774-2913).
    /// <para>Before: a right-click gave ENTER_FORT only when the pointer was over the fort's picture AND the ground point under it lay on
    /// Zod's small door rectangle (bfort.cpp:517-532, <see cref="OnFortDoor"/>: 2 x 6 tiles of the gate tunnel). At the RTS camera
    /// that ground point lies behind the walls for most of the door's picture, so most clicks on the door were attacks on the fort,
    /// and the ground in front of the gate was a plain move.</para>
    /// <para>Now, with a robot or a vehicle of the player selected (who may enter: the same units as before), a plain right-click
    /// gives ENTER_FORT on:</para>
    /// <list type="bullet">
    /// <item><b>the gate</b> (<see cref="FortPart.Gate"/>): Zod's door rectangle as before; the door's own picture (a box round the door
    /// opening of the fort's door rig, FactoryView.DoorMin / DoorMax: <see cref="GateSideM"/> to each side, <see cref="GateTopM"/> over
    /// it, <see cref="GateInM"/> into the tunnel, <see cref="GateOutM"/> out); and the ground within DV168's reach of the gate's exit
    /// point (ZWorld.FortAutoRushPx, 4 tiles) or in the gate mouth between the exit point and the door. With Ctrl too (as Zod's door).</item>
    /// <item><b>the fort's body</b> (<see cref="FortPart.Body"/>): anywhere else on the fort's own picture (its walls, roof, centre,
    /// ramp). With Ctrl held it stays the attack on the fort it was.</item>
    /// </list>
    /// <para>Unchanged: a click on a unit (a tower gun, the fort's roof defender, an enemy robot or vehicle standing in the gate zone,
    /// own units are not targets) gives that unit's order, as every unit under the pointer beats a building (HoverAt). A click on the
    /// fort's shooting parts (<see cref="FortPart.Shoot"/>: the four tower tops, with or without a gun, and the big gun's turret on the
    /// roof, which is part of the fort) stays the attack on the fort. A flag, a grenade crate, an own APC or another building in the
    /// zone keep their own order. Only a click on bare ground or scenery (a rock, a prop, a tree, a wreck, rubble) in the gate zone is
    /// turned into the fort's gate. Guns alone selected: as before.</para>
    /// <para>The cursor (UI/OpenZCursors.cs) shows ENTER wherever the order is ENTER_FORT (Zod's door showed the move star). Switch
    /// <see cref="FortGateOn"/> (on): off = the door rectangle alone, as before. The Play check: View/OzFortGateCheck.cs.</para>
    /// </summary>
    public partial class PlayerInput
    {
        /// <summary>1.4: the bigger enter-the-fort click (on); off = Zod's door rectangle only.</summary>
        public static bool FortGateOn = true;
        /// <summary>The door picture's box in the door rig's metres: to each side of the opening, over its top, into the tunnel, out of it.</summary>
        public const float GateSideM = 1.5f, GateTopM = 1.5f, GateInM = 3f, GateOutM = 2f;
        /// <summary>The gate mouth on the ground: half its width (px) from the door axis, between the exit point and this far back into the door.</summary>
        public const int GateMouthHalfPx = ZWorld.FortDoorHalf + 16, GateMouthBackPx = 64;
        /// <summary>A tower top is a shooting part within this radius (m) of its pad centre (pad 1.7 m, tower 2.1 m).</summary>
        public const float TowerTopR = 2.3f, WallTopY = 8.76f;
        /// <summary>The big gun's turret box is grown by this (m).</summary>
        public const float TurretGrowM = 0.3f;

        public enum FortPart { None = 0, Gate = 1, Body = 2, Shoot = 3 }

        /// <summary>Play counters: frames the pointer's bare ground / scenery was turned into an enemy fort's gate; right-clicks that gave
        /// ENTER_FORT through the new zone or the body (not Zod's door rectangle).</summary>
        public static int GateHoverFrames, GateClicks;

        int fortPartFrame = -1; ZObject fortPartOf; FortPart fortPart;
        readonly List<ZObject> gateForts = new List<ZObject>(); ZWorld gateFortsOf;
        static readonly Dictionary<int, Renderer[]> turretRs = new Dictionary<int, Renderer[]>();
        static readonly float[] ShootPlanes = { TowerMounts.PadTopY, WallTopY };                      // the tower tops' plate and the wall top under it

        /// <summary>Update, right after HoverAt: the part of an enemy fort under the pointer this frame, and bare ground / scenery in an enemy
        /// fort's gate zone becomes that fort (only with a robot or vehicle of the player selected).</summary>
        ZObject FortGateHover(ZObject h, Vector2 mp)
        {
            fortPartFrame = Time.frameCount; fortPartOf = null; fortPart = FortPart.None;
            if (!FortGateOn || game == null || W == null) return h;
            var c = Cam; if (!c) return h;
            var ray = c.ScreenPointToRay(mp);
            bool ground = game.GroundPoint(ray, out var gp);
            var px = ground ? ZGame.WorldToPx(gp) : new Vector2Int(int.MinValue / 4, int.MinValue / 4);
            if (h != null && h.IsFort && !h.Destroyed && h.Owner != Team.Neutral && h.Owner != game.player)
            {
                if (!FortUnder(h, ray)) { FortBesideFrames++; h = null; }                                // 1.5 N21: only its screen box, not the fort itself, is under the pointer (View/PlayerInput.FortTight.cs)
                else
                {
                    fortPartOf = h; fortPart = PartOf(h, ray, px, ground);
                    return h;
                }
            }
            if (!(h == null || Scenery(h)) || !GateMovers()) return h;
            if (gateFortsOf != W) { gateForts.Clear(); foreach (var o in W.Objects) if (o.IsFort) gateForts.Add(o); gateFortsOf = W; }
            foreach (var f in gateForts)
            {
                if (!f.CanEnterFort(game.player) || OzFogOfWar.Covers(f)) continue;   // 1.4 fog_hover_leak (View/OzFogOfWar.cs Covers): no ENTER cursor or order on a fort the fog hides
                if ((!FortTightOn && ground && GroundGate(f, px.x, px.y)) || DoorBox(f, ray)) { fortPartOf = f; fortPart = FortPart.Gate; GateHoverFrames++; return f; }   // 1.5 N21: the bare ground round the gate is no longer the fort (only its door's own picture)
            }
            return h;
        }

        /// <summary>BuildWaypoint: a right-click on the enemy fort h at ground point (x, y) is an ENTER_FORT (h may be entered by the unit).</summary>
        bool FortEnterClick(ZObject h, int x, int y, bool ctrl)
        {
            if (OnFortDoor(h, x, y)) return true;                                                     // Zod's door rectangle, as before (Ctrl too)
            if (!FortGateOn) return false;
            var part = fortPartFrame == Time.frameCount && fortPartOf == h ? fortPart : GroundGate(h, x, y) ? FortPart.Gate : FortPart.Body;   // no pointer this frame (a check's OrderFor): by the ground point
            bool enter = part == FortPart.Gate || (part == FortPart.Body && !ctrl);
            if (enter) GateClicks++;
            return enter;
        }

        /// <summary>UI/OpenZCursors.cs: the plain right-click at the pointer on enemy fort h this frame would be ENTER_FORT (Ctrl read here).</summary>
        public bool FortEnterAt(ZObject h)
        {
            if (!FortGateOn || h == null || fortPartFrame != Time.frameCount || fortPartOf != h) return false;
            bool ctrl = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
            return fortPart == FortPart.Gate || (fortPart == FortPart.Body && !ctrl);
        }

        /// <summary>The part of enemy fort f under the pointer: Zod's door, a shooting part, the gate zone, else its body.</summary>
        FortPart PartOf(ZObject f, Ray ray, Vector2Int px, bool ground)
        {
            if (ground && OnFortDoor(f, px.x, px.y)) return FortPart.Gate;
            if (ShootPart(f, ray)) return FortPart.Shoot;
            if ((ground && GroundGate(f, px.x, px.y)) || DoorBox(f, ray)) return FortPart.Gate;
            return FortPart.Body;
        }

        /// <summary>A robot or vehicle of the player that can move is selected (the units an ENTER_FORT order is for).</summary>
        bool GateMovers()
        {
            foreach (var s in Selection)
                if (s != null && !s.Destroyed && s.Owner == game.player && (s.Kind == ZKind.Robot || s.Kind == ZKind.Vehicle) && s.CanMove) return true;
            return false;
        }

        /// <summary>Bare scenery a click in the gate zone may pass to the gate: map items but flags and crates, and rubble.</summary>
        static bool Scenery(ZObject h) =>
            (h.Kind == ZKind.MapItem && h.Type != "flag" && h.Type != "grenades" && h.Type != "rockets") || (h.Kind == ZKind.Building && h.Destroyed && !h.IsFort);

        /// <summary>The gate zone on the ground (px): within DV168's reach of the gate's exit point, or in the mouth between it and the door.</summary>
        public static bool GroundGate(ZObject f, int x, int y)
        {
            if (f == null || !f.GetCreationMovePoint(out int ex, out int ey) || !f.GetCreationPoint(out _, out int cy)) return false;
            if (ZWorld.FortAutoRushInReach(f, x, y)) return true;
            long dx = x - ex, dy = y - ey;
            long along = ey > cy ? dy : -dy;                                                          // + = out of the gate (a front fort's gate faces +y)
            return dx <= GateMouthHalfPx && -dx <= GateMouthHalfPx && along <= 0 && along >= -GateMouthBackPx;
        }

        /// <summary>The ray passes through the box round the fort's door opening (its door rig; no rig = no box).</summary>
        static bool DoorBox(ZObject f, Ray ray)
        {
            var fv = FactoryViews.I ? FactoryViews.I.For(f.Id) : null;
            if (!fv) return false;
            Vector3 a = fv.DoorMin, b = fv.DoorMax;
            float side = FortTightOn ? DoorSideTightM : GateSideM, top = FortTightOn ? DoorTopTightM : GateTopM, outM = FortTightOn ? DoorOutTightM : GateOutM;   // 1.5 N21: the door's own picture (View/PlayerInput.FortTight.cs)
            var lo = new Vector3(Mathf.Min(a.x, b.x) - side, Mathf.Min(Mathf.Min(a.y, b.y), 0f) - (FortTightOn ? 0f : 1f), Mathf.Min(a.z, b.z) - GateInM);
            var hi = new Vector3(Mathf.Max(a.x, b.x) + side, Mathf.Max(a.y, b.y) + top, Mathf.Min(a.z, b.z) + outM);
            var t = fv.transform; var o = t.InverseTransformPoint(ray.origin); var d = t.InverseTransformPoint(ray.origin + ray.direction) - o;
            return Slab(o, d, lo, hi);
        }

        /// <summary>A shooting part of fort f is under the pointer: a tower top (a gun standing there is a unit and is picked before this) or the big gun's turret.</summary>
        bool ShootPart(ZObject f, Ray ray)
        {
            if (!game.StaticViews.TryGetValue(f.Id, out var go) || !go) return false;
            var t = go.transform; var o = t.InverseTransformPoint(ray.origin); var d = t.InverseTransformPoint(ray.origin + ray.direction) - o;
            foreach (float y in ShootPlanes)
            {
                if (Mathf.Abs(d.y) < 1e-6f) break;
                float k = (y - o.y) / d.y; if (k <= 0f) continue;
                float x = o.x + d.x * k, z = o.z + d.z * k;
                foreach (var p in TowerMounts.PadLocal) if ((x - p.x) * (x - p.x) + (z - p.z) * (z - p.z) <= TowerTopR * TowerTopR) return true;
            }
            var gun = go.GetComponent<FortGunView>();
            if (!gun || !gun.Bound || gun.Tossed || !gun.YawT) return false;
            if (!turretRs.TryGetValue(f.Id, out var rs) || rs == null || rs.Length == 0 || !rs[0]) turretRs[f.Id] = rs = gun.YawT.GetComponentsInChildren<Renderer>(true);   // a new level's fort may reuse the id
            bool any = false; var bb = new Bounds();
            foreach (var r in rs) { if (!r || !r.enabled || !(r is MeshRenderer)) continue; if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds); }
            if (!any) return false;
            bb.Expand(TurretGrowM * 2f);
            return bb.IntersectRay(ray);
        }

        /// <summary>Ray (o, d) against the box [lo, hi], ahead of o only.</summary>
        static bool Slab(Vector3 o, Vector3 d, Vector3 lo, Vector3 hi)
        {
            float t0 = 0f, t1 = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < 1e-7f) { if (o[i] < lo[i] || o[i] > hi[i]) return false; continue; }
                float a = (lo[i] - o[i]) / d[i], b = (hi[i] - o[i]) / d[i];
                if (a > b) { var s = a; a = b; b = s; }
                t0 = Mathf.Max(t0, a); t1 = Mathf.Min(t1, b);
                if (t0 > t1) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ the Play check's probe (View/OzFortGateCheck.cs)
        public struct FortGateProbeResult
        {
            public ZObject hoverOld, hoverNew; public bool ground; public Vector2Int px;
            public bool oldEnter, newEnter, newEnterCtrl; public FortPart part; public WpMode newMode; public int newRef;
        }

        /// <summary>What a plain right-click at screen point mp gives unit u (selected), before this change (HoverAt + Zod's door rectangle)
        /// and now (the gate zone, the body, the shooting parts); and the same with Ctrl. Nothing is sent.</summary>
        public FortGateProbeResult FortGateProbe(Vector2 mp, ZObject u)
        {
            var r = new FortGateProbeResult();
            r.hoverOld = HoverAt(mp);
            r.ground = game.GroundPoint(Cam.ScreenPointToRay(mp), out var gp); r.px = ZGame.WorldToPx(gp);
            r.oldEnter = r.ground && r.hoverOld != null && (u.Kind == ZKind.Robot || u.Kind == ZKind.Vehicle) && r.hoverOld.CanEnterFort(u.Owner) && OnFortDoor(r.hoverOld, r.px.x, r.px.y);
            r.hoverNew = FortGateHover(r.hoverOld, mp);
            r.part = fortPartOf != null && fortPartOf == r.hoverNew ? fortPart : FortPart.None;
            if (!r.ground) return r;
            int clicks = GateClicks;
            var wp = BuildWaypoint(u, r.hoverNew, r.px.x, r.px.y, false, false);
            r.newMode = wp.mode; r.newRef = wp.refId; r.newEnter = wp.mode == WpMode.EnterFort;
            r.newEnterCtrl = BuildWaypoint(u, r.hoverNew, r.px.x, r.px.y, true, false).mode == WpMode.EnterFort;
            GateClicks = clicks;                                                                      // a probe is not a click
            return r;
        }

        // ------------------------------------------------------------------ DV168's log (Sim/ZWorld.FortAutoRush.cs)
        static ZWorld rushLogOf; static int rushLogged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FortAutoRushLogHook() { ZWorld.FortAutoRushSent -= FortAutoRushLog; ZWorld.FortAutoRushSent += FortAutoRushLog; }

        static void FortAutoRushLog(ZWorld w, ZObject u, ZObject f, int d)
        {
            if (w != rushLogOf) { rushLogOf = w; rushLogged = 0; }
            if (++rushLogged > 12) return;
            Debug.Log($"[OpenZ] fort run from near the gate (DV168): {u.Type} #{u.Id} ({u.Owner}, squad of {1 + u.Minions.Count}) runs into {f.Type} #{f.Id} from {d / 16.0:0.0} tiles of its gate point, t {w.Time:0.0} s (orders so far {w.FortAutoRushOrders})");
        }
    }
}
