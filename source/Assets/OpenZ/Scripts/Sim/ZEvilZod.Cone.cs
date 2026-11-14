using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// DV161's shot hit its one target. Now the same shot also hits every other
    /// enemy robot inside the cone its pellets fly in, each by its own distance:
    /// <list type="bullet">
    /// <item>The cone: from his centre along the line to the aimed target's centre, the half angle of the view's pellet cone (OzEvilZodFx.ConeDeg 9 deg, a dash shot
    /// DashConeDeg 6 deg; as tan x 1000: <see cref="ZEvilCone.TanPermille"/>, <see cref="ZEvilCone.DashTanPermille"/>). A robot is in it when it stands ahead of him and
    /// its body (the traffic rules' radius) reaches the cone: its distance from the aim line at most its distance along the line x tan + its radius. Whole px, longs.</item>
    /// <item>The reach: his shot range and line rules, as for any target of his (WithinAttackRadius: range, Zod's barriers, DV23's line), and no farther than
    /// <see cref="ZEvilCone.PastPx"/> behind the aimed target's distance: the view's pellets fly at most 8 m past their aim point.</item>
    /// <item>The damage: the single-target rule at that robot's own distance (ZEvilZod.Hit, the range falloff: any robot within 64 px dies, 900 HP at his range).
    /// A dash shot's full number stays on the target it dashed at; the cone takes its distance rule.</item>
    /// <item>Who: enemy robots on the ground only (not the aimed target, not his team, not a neutral, not one still getting out of a shuttle or an APC: CanAttackObject).
    /// Vehicles, guns and buildings take nothing more than today. A shot at a shuttle in the air has no cone.</item>
    /// <item>Each hit: its HP, the hit time, an <see cref="ZEventType.EvilConeHit"/> for the view, then the death relay with him as the killer (the kill credit,
    /// DV12) and the squad's help call (DV53), as the mech's hand gun does (ZObject.MechHandGun.cs). Object order; no random draw anywhere.</item>
    /// </list>
    /// Switch <see cref="ZWorld.EvilConeDeviation"/>: on by default (the game never sets it; it does something only where an EvilZod fires, and DV161 has him only
    /// outside 'Zod exact'); off = his shot is DV161's single hit, bit for bit.
    /// </summary>
    public static class ZEvilCone
    {
        /// <summary>The cone's half angle as tan x 1000: the view's pellet cone, OzEvilZodFx.ConeDeg 9 deg (tan 0.1584) and DashConeDeg 6 deg (tan 0.1051).</summary>
        public const int TanPermille = 158, DashTanPermille = 105;
        /// <summary>How far past the aimed target's distance the cone reaches (px): the view's longest pellet flight past its aim point, 3 + 5 m = 8 m (OzEvilZodFx.Spread).</summary>
        public const int PastPx = 64;

        /// <summary>The cone test, whole px: a = (ax, ay) from his centre to the aimed target's centre, b = (bx, by) to the robot's centre, r its body radius, the half
        /// angle as tan x 1000. True when b lies ahead (a . b &gt; 0) and its distance from the aim line is at most (its distance along the line) x tan + r.</summary>
        public static bool InCone(long ax, long ay, long bx, long by, int r, int tanPermille)
        {
            long aa = ax * ax + ay * ay;
            if (aa <= 0) return false;
            long along = ax * bx + ay * by;
            if (along <= 0) return false;
            long cross = Math.Abs(ax * by - ay * bx);
            long over = cross * 1000 - along * tanPermille;                                     // |a| x (1000 x lateral - tan x along), compared with |a| x 1000 x r
            if (over <= 0) return true;
            long room = (long)r * 1000;
            return over * over <= room * room * aa;
        }
    }

    public partial class ZObject
    {
        /// <summary>DV167, this EvilZod: shots that hit robots besides their target, the robots they hit, the robots they killed.</summary>
        public int EvilConeShots, EvilConeHits, EvilConeKills;

        /// <summary>EvilFire, right after the aimed hit and its event: every other enemy robot in the cone of the shot takes the shot at its own distance.</summary>
        void EvilConeFire(ZObject t, int d, bool dash, double time)
        {
            if (!ZWorld.EvilConeDeviation || t == null || t.ShuttleAirborne) return;
            long ax = t.CenterX - CenterX, ay = t.CenterY - CenterY;
            if (ax == 0 && ay == 0) return;
            int tan = dash ? ZEvilCone.DashTanPermille : ZEvilCone.TanPermille, reach = d + ZEvilCone.PastPx;
            List<ZObject> hit = null;
            foreach (var o in World.UnitObjects)                                                    // object order; the hits are made after the walk (a death relay may end a team)
            {
                if (o == t || o == this || o.Kind != ZKind.Robot || o.Destroyed || o.KillAt >= 0 || o.Owner == Team.Neutral || o.ShuttleAirborne) continue;
                if (!ZEvilCone.InCone(ax, ay, o.CenterX - CenterX, o.CenterY - CenterY, ZPathing.Radius(o), tan)) continue;
                if (!CanAttackObject(o) || (int)DistanceFrom(o) > reach || !WithinAttackRadius(o)) continue;
                (hit ??= new List<ZObject>()).Add(o);
            }
            if (hit == null) return;
            EvilConeShots++; World.EvilConeShots++;
            foreach (var o in hit)
            {
                if (o.Destroyed) continue;
                int od = (int)DistanceFrom(o), hp0 = o.Health;
                o.DamageHealth(ZEvilZod.Hit(o, od, AttackRadius, false));
                o.LastEnemyHit = time;
                bool killed = o.Destroyed;
                EvilConeHits++; World.EvilConeHits++;
                if (killed) { EvilConeKills++; World.EvilConeKills++; }
                World.Emit(ZEventType.EvilConeHit, this, o, o.CenterX, o.CenterY, hp0 - o.Health, od, dash, killed);
                World.UpdateObjectHealth(o, Id);                                                    // the death relay, he the killer (DV12's kill credit)
                if (o.Health < hp0) World.HelpCall(o, this);                                        // DV53
            }
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV167 switch (Sim/ZEvilZod.Cone.cs): on by default, the game never sets it. Off: EvilZod's shot hits its one target (DV161 as it was).</summary>
        public static bool EvilConeDeviation = true;
        /// <summary>DV167 statistics of this world (checks): shots with a cone hit, robots hit by a cone, robots a cone killed.</summary>
        public int EvilConeShots, EvilConeHits, EvilConeKills;
    }
}
