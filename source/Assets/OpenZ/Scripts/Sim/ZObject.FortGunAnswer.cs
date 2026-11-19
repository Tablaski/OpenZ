using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// blue fort's tower with a missile cannon on it; the medium tank does not fire at it, drives on, then fires at the sniper on the fort's roof,
    /// which is farther; "he must shoot tower turrets if in range, not ignore them and get destroyed").
    /// <para>Cause (counted, kit README): the medium tank's gun reaches 120 px (the light tank's gun), the tower's missile cannon 144 px and,
    /// standing on the tower, 158 px (DV162 high ground). Driving past at 130-160 px the tank is inside the gun's reach and outside its own:
    /// it cannot hit the gun from where it is. Its order was an attack-move, but on the player's order the calm-move rule (b49,
    /// ZObject.CalmMayFight) lets a unit pick only an enemy it can hit from its way, so the gun, a short turn-in away, is never picked; the roof
    /// defender, whose rule spot is the fort's edge and so nearer, is (within 120 px) and gets the shells. On a plain move a unit never turns
    /// off its way at all (Zod), and a unit that already fires at something keeps it while it stays in range (Zod's CheckPassiveEngage).</para>
    /// <para>The rule (every half second, on CheckPassiveEngage's beat): an enemy fort TOWER gun (<see cref="IsTowerGun"/>: missile cannon,
    /// howitzer, gun or gatling on one of a fort's four tower spots) that is shooting at this unit, or at a unit of its side while this unit
    /// stands in the gun's own reach, and that this unit can damage (CanAttackObject), comes first:</para>
    /// <list type="bullet">
    /// <item>a vehicle or robot that is idle, on a move order (plain or attack-move, the player's or the computer's, a flag run), on an automatic
    /// chase (AGRO) or on an attack it picked on the way (not a target the player or the computer named) gets an ATTACK waypoint on the gun in
    /// front of that order (behind a sidestep in progress): Zod's attack planner with DV28 / DV23 takes it to the nearest spot in range with a
    /// clear shot, it fires until the gun is destroyed (the calm-move rule does not end this attack when the gun is out of range: that is the
    /// point), and then its order goes on where it was;</item>
    /// <item>a unit holding position, stunned, on a SPRINT order (the player chose to run) or on an order of its own kind (enter, repair, pick up,
    /// fort, factory exit, a named attack) does not turn: when the gun is in its reach it fires at it before any other target;</item>
    /// <item>a gun it cannot reach a shot spot for is remembered like an unreachable goal (ZPathing.UnreachableMemory s), no ask every beat.</item>
    /// </list>
    /// <para>Not changed: ranges, damage, reloads, rolls, the guns' own choice, the fort's own gun and roof defender (not tower guns), ground guns,
    /// any other target. Both sides, every level. Deterministic: sim time, object order (nearest gun by whole-pixel squared distance, ties the lower
    /// id), no random draw. Switch <see cref="ZWorld.FortGunAnswerDeviation"/>: View/ZGame.cs turns it on at every start, off in 'Zod exact' (Zod
    /// has no such rule); static default off, so headless worlds, the parity's Zod rows and every older counted check keep the rules as they
    /// were.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>
        /// A weapon's range at an enemy fort TOWER gun (<see cref="IsTowerGun"/>) is
        /// measured to the tower's body, a disc of <see cref="ZWorld.FortTowerBodyPx"/> round the gun's centre (the tower drum under the gun
        /// reaches 3 m = 24 px out to the fort's outer wall: TowerMounts.PadLocal +-6.99 / -6.56 m in a 20 x 19.8 m fort), not to the gun's
        /// centre (Zod: points_within_distance centre to centre, zobject.cpp:4760-4765), as DV26 measures a building's range to its walls.
        /// Added in <see cref="HighRange"/>, so every reader of a range at one target gets it the same: WithinAttackRadius (when it fires),
        /// ApproachPlan's "already in reach", the duel's bands, the sniper's and the Electrobot's steady rules, the CPU's "really fighting",
        /// the second weapons (the mech's hand gun, the hatch driver, the crane rifle). Every attacker: vehicles, robots, guns. Not changed:
        /// the tower gun's own range, damage, rolls, the walking plans' spots (they stop as soon as WithinAttackRadius says yes), the view's
        /// rings. Whole pixels, no random draw. 0 when the switch is off, for a weaponless unit or any other target.</summary>
        internal int TowerBodyPad(int radius, ZObject t) =>
            ZWorld.FortGunAnswerDeviation && radius > 0 && t != null && t != this && t.IsTowerGun ? ZWorld.FortTowerBodyPx : 0;

        /// <summary>DV204: the fort tower gun this unit's own answer waypoint is on (-1: none).</summary>
        int fgaGun = -1;

        /// <summary>DV204 (ProcessAttackWP): this attack is the unit's answer to fort tower gun t: the calm-move rule does not end it out of range.</summary>
        internal bool FortGunAnswering(ZObject t) => ZWorld.FortGunAnswerDeviation && t != null && t.Id == fgaGun && t.IsTowerGun;

        /// <summary>DV204 (ProcessAttackWP): no spot with a clear shot at the gun this unit answers: remembered like an unreachable goal (DV153 does
        /// the same for every tower gun while its switch is on).</summary>
        void FortGunPlanFailed(ZObject t)
        {
            if (!FortGunAnswering(t) || ZWorld.TowerAnswerDeviation) return;
            PS.Remember(t.Id, World.Time + ZPathing.UnreachableMemory);
        }

        /// <summary>DV204: the nearest living enemy fort tower gun that shoots at this unit, or at a unit of its side while this unit is in the gun's
        /// own reach, and that this unit can damage. null: none.</summary>
        ZObject FortGunOnUs()
        {
            ZObject best = null; long bd = long.MaxValue;
            foreach (var g in World.NearUnits(CenterX, CenterY, ZWorld.FortGunLookPx + ZWorld.TowerDrawnLookPad, World.Objects))   // 1.5 DV208 round 6 (kit tower_reach_fair_r6): the look square covers a tower gun's reach both ways (Sim/ZObject.TowerDrawnReach.cs)
            {
                if (g.Kind != ZKind.Cannon || !g.IsTowerGun || g.Destroyed || g.KillAt >= 0 || g.Owner == Owner || g.Owner == Team.Neutral) continue;
                var v = g.AttackObject;
                if (v == null || v.Owner != Owner || v.Destroyed) continue;
                if (v != this && !Within(g.CenterX, g.CenterY, CenterX, CenterY, g.RangeTo(this))) continue;
                if (!CanAttackObject(g)) continue;
                long dx = g.CenterX - CenterX, dy = g.CenterY - CenterY, d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }

        /// <summary>
        /// but an enemy fort tower gun's body is in its reach: it fires at that gun before any other target it picks itself (the roof defender,
        /// units), no turn. Not over a target the player named, not by a walking robot that does not fire on the move (Zod). true = engaged.</summary>
        bool FortGunInReachFirst()
        {
            if (AttackObject != null && AttackObject.IsTowerGun && !AttackObject.Destroyed && WithinAttackRadius(AttackObject)) return false;   // already on one: Zod keeps it
            if (IsRobot && IsMoving && !FiresOnMove) return false;
            int k = 0; while (k < Waypoints.Count && Waypoints[k].mode == WpMode.Dodge) k++;
            if (k < Waypoints.Count && Waypoints[k].mode == WpMode.Attack && Waypoints[k].playerGiven) return false;   // a target the player named
            ZObject best = null; long bd = long.MaxValue;
            foreach (var g in World.NearUnits(CenterX, CenterY, ZWorld.FortGunLookPx + ZWorld.TowerDrawnLookPad, World.Objects))   // 1.5 DV208 round 6 (kit tower_reach_fair_r6): the look square covers a tower gun's reach both ways (Sim/ZObject.TowerDrawnReach.cs)
            {
                if (g.Kind != ZKind.Cannon || !g.IsTowerGun || g.Destroyed || g.KillAt >= 0 || g.Owner == Owner || g.Owner == Team.Neutral) continue;
                if (!CanAttackObject(g) || !WithinAttackRadius(g)) continue;
                long dx = g.CenterX - CenterX, dy = g.CenterY - CenterY, d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = g; }
            }
            if (best == null) return false;
            if (AttackObject != best) { Engage(best); ZWorld.FortGunEngages++; }
            return true;
        }

        /// <summary>DV204: this unit may turn off what it is doing for the gun (waypoint k = its first one after a sidestep).</summary>
        bool FortGunMayTurn(int k)
        {
            if (HoldPosition || CrocWary || !CanMove) return false;
            if (k >= Waypoints.Count) return true;                                     // idle (or only stepping aside)
            var w = Waypoints[k];
            switch (w.mode)
            {
                case WpMode.Move: return !w.sprint;                                    // a move, a flag run; a sprint is the player's run
                case WpMode.Agro: return true;                                         // an automatic chase of something else
                case WpMode.Attack: return !w.playerGiven;                             // an attack picked on the way, never a named target
                default: return false;                                                 // enter, repair, pick up, fort, factory exit
            }
        }

        /// <summary>DV204, CheckPassiveEngageCore on its half-second beat (before Zod's own pick): true = this beat is done (the unit answers a
        /// fort tower gun).</summary>
        bool FortGunAnswer(bool engageOnly)
        {
            if (!ZWorld.FortGunAnswerDeviation || Kind == ZKind.Cannon || IsFortDefender || IsShuttle || IsEvilZod || World == null) return false;
            var g = FortGunOnUs();
            if (g == null) return FortGunInReachFirst();
            if (AttackObject != null && AttackObject.IsTowerGun && !WithinAttackRadius(AttackObject)) Disengage();   // a tower gun it no longer reaches
            bool reach = WithinAttackRadius(g), fire = reach && !(IsRobot && IsMoving && !FiresOnMove);   // a walking robot does not fire (Zod)
            if (fire && AttackObject != g && !(AttackObject != null && AttackObject.IsTowerGun)) { Engage(g); ZWorld.FortGunEngages++; }   // before any other target
            int k = 0; while (k < Waypoints.Count && Waypoints[k].mode == WpMode.Dodge) k++;
            if (k < Waypoints.Count && Waypoints[k].mode == WpMode.Attack && World.Get(Waypoints[k].refId) is ZObject on && on.IsTowerGun && !on.Destroyed && on.KillAt < 0)
                return true;                                                           // already on a fort tower gun (its own answer or an order)
            if (engageOnly || !FortGunMayTurn(k)) return fire;
            if (!reach && PathUnreachable(g)) return false;                            // no shot spot found lately
            Waypoints.Insert(k, new ZWaypoint { mode = WpMode.Attack, refId = g.Id, x = g.CenterX, y = g.CenterY });
            fgaGun = g.Id;
            ZWorld.FortGunAnswers++;
            return true;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV204 switch (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off).</summary>
        public static bool FortGunAnswerDeviation = false;
        /// <summary>DV204: how far (px, square half side) a unit looks for a fort tower gun on it: the longest tower gun's reach (howitzer
        /// 200 px, 220 from the tower) and a margin.</summary>
        public const int FortGunLookPx = 256;
        /// <summary>DV204 body reach: the radius (px) of a fort tower's body round its gun's centre (3 m, 1.5 tiles).</summary>
        public const int FortTowerBodyPx = 24;
        /// <summary>DV204 statistics (the counted check): answer waypoints given, guns engaged without a turn.</summary>
        public static int FortGunAnswers, FortGunEngages;

        /// <summary>The parity report and the older counted checks: no DV204 while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable FortGunAnswerOff() => new FortGunAnswerHold();
        sealed class FortGunAnswerHold : IDisposable
        {
            readonly bool was = FortGunAnswerDeviation;
            public FortGunAnswerHold() { FortGunAnswerDeviation = false; }
            public void Dispose() { FortGunAnswerDeviation = was; }
        }
    }
}
