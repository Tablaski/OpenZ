using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV203 "march grabs" (1.5, kit march_grab_r3; human teams, like DV84). While a robot squad walks the player's plain
    /// move (the attack-move, not a retreat), its leader looks every half second (DV84's beat, ZObject.GrabCertain.cs GrabOnTheWay):
    /// <para>(1) a flag that is not its team's within Zod's own auto_grab_flag_distance (220 px) of ANY member of the squad, or (2) an EMPTY gun
    /// within <see cref="ZWorld.MarchGrabGunPx"/> px of any member. The ONE member nearest such a target (squared whole-pixel distance; ties:
    /// the lower target id, then the lower robot id) that can go (moves, not stunned or holding, sees it, can reach it, nobody closer and idle
    /// takes it, not on a bridge or its approach; a gun: not on a bridge approach, may auto-enter) leaves the squad with DV98's own split
    /// (ZWorld.SquadSplit.cs: a minion simply leaves; a leader leaves the squad to the member on the road nearest the order's goal) and goes alone: a flag with a
    /// MOVE onto it (DV54 runs it) followed by a copy of the squad's remaining player order (it arrives where the squad goes); a gun with
    /// an ENTER (it mans the gun, as an idle robot takes one). The rest of the squad keeps walking its order. A lone robot does the same
    /// without a split.</para>
    /// <para>One robot per target and order: every member of the squad (and the one that left) notes the target for this order (the order's
    /// last player-given leg is the key, like DV84's grabDone mark rides on the order); a target another unit is already heading for
    /// (DV38 GrabClaimed) is left alone. DV84's own grabDone mark still limits empty VEHICLES to one robot per order, unchanged; vehicles
    /// (no robot leader) keep DV84's 80 px for flags. DV130 still holds a flag while the squad is under fire; DV84's watch gives a stalled try
    /// up (the order copy behind it then goes on). Retreats and direct orders never turn off (DV84's gates). The computer is not touched.</para>
    /// Deterministic: sim time, object order, whole numbers, no random draw of its own. Switch: ZWorld.MarchGrabDeviation (the game turns it
    /// on at every start, off in 'Zod exact'; headless default off; the parity report holds it off).
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV203 switch (see ZObject.MarchGrab.cs). The game turns it on at every start; headless default off.</summary>
        public static bool MarchGrabDeviation = false;
        /// <summary>DV203: how far from a member of a marching squad an empty gun is taken (px, 5 tiles; DV84's CloseGrabPx).</summary>
        public const int MarchGrabGunPx = 80;
        /// <summary>DV203 statistics (checks): robots sent to a flag / to a gun out of a march, and of them the ones that left a squad.</summary>
        public static int MarchGrabFlags, MarchGrabGuns, MarchGrabSplits;

        /// <summary>DV203: m leaves the squad led by l (DV98's split; l == m: the leader leaves, the first other member leads).</summary>
        internal void MarchSplit(ZObject l, ZObject m, int gx, int gy)
        {
            if (l == m)
            {   // the member nearest the order's goal (the front of the march) leads on, so nobody walks back to a new leader behind him
                int bi = -1; long bd = long.MaxValue; bool br = false;
                for (int i = 0; i < l.Minions.Count; i++)
                {
                    var x = l.Minions[i];
                    if (x == null || x.Destroyed || x.KillAt >= 0) continue;
                    bool road = OnRoadTile(x);                                             // a member on the road first: his own path then keeps to the road (road speed)
                    long dx = x.CenterX - gx, dy = x.CenterY - gy, d = dx * dx + dy * dy;
                    if (bi < 0 || (road && !br) || (road == br && (d < bd || (d == bd && x.Id < l.Minions[bi].Id)))) { bd = d; bi = i; br = road; }
                }
                if (bi > 0) { var x = l.Minions[bi]; l.Minions.RemoveAt(bi); l.Minions.Insert(0, x); }
            }
            SplitFromSquad(l, m);
        }

        bool OnRoadTile(ZObject x)
        {
            int tx = x.CenterX >> 4, ty = x.CenterY >> 4;
            return Map != null && tx >= 0 && ty >= 0 && tx < Map.width && ty < Map.height && Map.classes[ty * Map.width + tx] == (int)ZClass.Road;
        }

        /// <summary>Turns DV203 off until disposed, then puts back what was there (the parity report's Zod rows and probes).</summary>
        public sealed class MarchGrabHold : IDisposable
        {
            readonly bool was;
            internal MarchGrabHold() { was = MarchGrabDeviation; MarchGrabDeviation = false; }
            public void Dispose() { MarchGrabDeviation = was; }
        }
        public static MarchGrabHold MarchGrabOff() => new MarchGrabHold();
    }

    public partial class ZObject
    {
        List<int> mgSent; ZWaypoint mgOrder = ZWaypoint.Cleared;

        /// <summary>DV203 applies to this unit's look (a robot, the switch on): DV84's own flag look is then left out (this one replaces it).</summary>
        bool MarchGrabOwnsFlags => ZWorld.MarchGrabDeviation && IsRobot;

        /// <summary>The last player-given leg of a waypoint list (the order's key), Cleared = none.</summary>
        static ZWaypoint MgKey(List<ZWaypoint> l)
        {
            for (int i = l.Count - 1; i >= 0; i--) if (l[i].playerGiven) return l[i];
            return ZWaypoint.Cleared;
        }

        bool MgSentFor(ZWaypoint key, int id) => mgSent != null && mgOrder.Equals(key) && mgSent.Contains(id);

        void MgNote(ZWaypoint key, int id)
        {
            if (mgSent == null) mgSent = new List<int>();
            if (!mgOrder.Equals(key)) { mgSent.Clear(); mgOrder = key; }
            if (!mgSent.Contains(id)) mgSent.Add(id);
        }

        static long Sq(ZObject a, ZObject b) { long dx = a.CenterX - b.CenterX, dy = a.CenterY - b.CenterY; return dx * dx + dy * dy; }

        /// <summary>DV203, GrabOnTheWay (DV84) after its gates, on its half-second beat. 0 = nothing sent; 1 = a member left the squad (this
        /// leader walks on); 2 = this unit itself now has the grab waypoint in front.</summary>
        int MarchGrabStep(ZWaypoint wp)
        {
            if (!MarchGrabOwnsFlags) return 0;
            var key = MgKey(Waypoints);
            var team = new List<ZObject> { this };
            foreach (var m in Minions) if (m != null && !m.Destroyed && m.KillAt < 0 && m.World == World) team.Add(m);
            long flagLim = (long)World.S.auto_grab_flag_distance * World.S.auto_grab_flag_distance, gunLim = (long)ZWorld.MarchGrabGunPx * ZWorld.MarchGrabGunPx;
            ZObject bt = null, br = null; long bd = long.MaxValue; bool enter = false;
            bool? flagHeld = null;
            foreach (var f in World.Flags)
            {
                if (World.AreAllied(Owner, f.Owner) || f.Destroyed || MgSentFor(key, f.Id) || GrabClaimed(f)) continue;
                foreach (var m in team)
                {
                    long d = Sq(m, f);
                    if (d > flagLim || !MgBetter(d, f, m, bd, bt, br) || !MgCanGo(m)) continue;
                    if (m.PathUnreachable(f) || !m.CanSee(f) || m.CloserGrabber(f, false)) continue;
                    if (flagHeld == null) flagHeld = AutoFlagWayHeld();                          // DV130: no flag while the squad is under fire
                    if (flagHeld == true) continue;
                    bt = f; br = m; bd = d; enter = false;
                }
            }
            foreach (var g in World.Objects)
            {
                if (g.Kind != ZKind.Cannon || g.Destroyed || g.KillAt >= 0 || !g.CanBeEntered() || g.AutoEnterLocked || MgSentFor(key, g.Id)) continue;
                if (OnBridgeApproach(g) || GrabClaimed(g) || TeammateEntering(g)) continue;
                foreach (var m in team)
                {
                    long d = Sq(m, g);
                    if (d > gunLim || !MgBetter(d, g, m, bd, bt, br) || !MgCanGo(m) || m.NoAutoEnter || m.JustLeftCannon) continue;
                    if (m.PathUnreachable(g) || !m.CanSee(g) || m.CloserGrabber(g, true)) continue;
                    bt = g; br = m; bd = d; enter = true;
                }
            }
            if (bt == null) return 0;
            var order = new List<ZWaypoint>();
            foreach (var q in Waypoints) if (q.playerGiven) order.Add(q);                         // the squad's remaining player order
            var grab = new ZWaypoint { mode = enter ? WpMode.Enter : WpMode.Move, refId = bt.Id, x = bt.CenterX, y = bt.CenterY, attackTo = true };
            foreach (var m in team) m.MgNote(key, bt.Id);                                          // one robot per target and order, for the whole squad
            if (enter) ZWorld.MarchGrabGuns++; else ZWorld.MarchGrabFlags++;
            if (team.Count == 1)                                                                   // a lone robot: the grab in front of its order (as DV84)
            {
                Waypoints.Insert(0, grab); GrabNote(bt); CloneMinionWayPoints(); UpdatedWaypoints = true;
                return 2;
            }
            ZWorld.MarchGrabSplits++;
            if (br == this)                                                                        // the leader is the nearest: he leaves, the first other member leads on
            {
                World.MarchSplit(this, this, wp.x, wp.y);
                foreach (var m in team)
                    if (m != this && m.Leader == null) { m.PathReissue(); m.UpdatedWaypoints = true; break; }   // the new leader plans its own path (as on a leader's death)
                Waypoints.Insert(0, grab); GrabNote(bt); UpdatedWaypoints = true;
                return 2;
            }
            World.MarchSplit(this, br, wp.x, wp.y);                                                            // a member leaves; this leader walks on
            br.Waypoints.Clear(); br.Waypoints.Add(grab);
            if (!enter) br.Waypoints.AddRange(order);                                              // after the flag: the squad's order, to the same place
            br.StopMove(); br.PathReissue(); br.GrabNote(bt); br.UpdatedWaypoints = true;
            return 1;
        }

        /// <summary>DV203: (d, t, m) is nearer than the best so far (ties: the lower target id, then the lower robot id).</summary>
        static bool MgBetter(long d, ZObject t, ZObject m, long bd, ZObject bt, ZObject br) =>
            bt == null || d < bd || (d == bd && (t.Id < bt.Id || (t.Id == bt.Id && m.Id < br.Id)));

        /// <summary>DV203: member m may leave the march (moves, not stunned, not holding position, not fighting, not on a bridge or its approach).</summary>
        bool MgCanGo(ZObject m) => m.CanMove && !m.Stunned && !m.HoldPosition && m.AttackObject == null && m.IsRobot && !OnBridgeApproach(m);
    }
}
