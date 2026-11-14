using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// A player on Discord: units
    /// <para>Zod's guns on a fort's four tower spots (bfort.cpp:534-548) stand INSIDE the fort's blocked tiles (ZWorld.SetMapImpassables:
    /// fort rows 0-8 are impassable, the tower spots (1,0) (7,0) (1,3) (7,3) among them). They are shot from outside, from where a unit stands,
    /// but as a GOAL they are never reachable. Three automatic rules of the game treated them like any other target and failed on them:</para>
    /// <list type="bullet">
    /// <item>the "recently failed to reach" memory (ZObject.Pathing.cs PathUnreachable; ZPathing.PrepareStartPath and ZStuckGuard write it
    /// whenever a chase or a stuck unit had the gun as its goal) took the gun off the attack-move's list (ZObject.CheckAttackTo) and off the
    /// squad help call's list (ZWorld.HelpCall, DV53) for 6 s at a time, even while the unit could hit it from where it stood: it walked on;</item>
    /// <item>the automatic chases (idle agro, the help call) are Zod's AGRO waypoint, a straight drive at the target's centre (ProcessAgroWP:
    /// SetTarget(t.CenterX, t.CenterY)), here a point inside the fort: the unit drives at the fort wall and, out of range, never fires;</item>
    /// <item>an ATTACK on the gun whose planner finds no spot with a clear shot ends at once (ProcessAttackWP, DV23 LosPlanAttack) and was
    /// asked again on the next half-second check.</item>
    /// </list>
    /// <para>DV153, fort tower guns only (<see cref="IsTowerGun"/>): (1) a tower gun the unit can hit from where it stands (its attack range,
    /// Zod's barrier rule and DV23's line: WithinAttackRadius) is never dropped for the "unreachable" memory; (2) an automatic chase of a tower
    /// gun that is not in reach yet is an ATTACK waypoint (Zod's planner with DV28 / DV23: the nearest spot in range with a clear shot), not an
    /// AGRO; (3) an attack on a tower gun with no spot to shoot from anywhere is remembered like an unreachable goal for
    /// ZPathing.UnreachableMemory s; (4) the squad help call on a tower gun (ZWorld.HelpCall, DV53) also reaches a unit that is only stepping
    /// aside (<see cref="TowerHelpAfterStep"/>): its chase goes in behind the step. Counted on 3 Nov: a tower howitzer aims its shell at a
    /// squad's leader or a tank, which steps aside from it (Zod's DodgeMissile; a tank blasts the rock in its way, Zod's
    /// DoAttackImpassableAtCoords) and is still stepping when the shell lands, so the help call passed it by as "not idle"; a squad's robots got
    /// the chase, and the leader's list, copied onto them when its step ended (Zod's squad copy), took it away again; the unit stood under the
    /// shells, or walked off to a flag, and never answered. Units in range already fire by Zod's own rules (CheckPassiveEngage, every 0.5 s; vehicles and DV57's
    /// light robots also while they move); nothing changes for them, nor for ranges, damage, rates, rolls, the player's own orders (a plain
    /// move given near enemies stays Zod's no-fight move) or any other target. Deterministic: sim time, object order, no random draw.</para>
    /// <para>Switch <see cref="ZWorld.TowerAnswerDeviation"/>: View/ZGame.cs turns it on at every start (off in 'Zod exact'); static default
    /// off, so headless worlds, the parity's Zod rows and every older counted check keep the rules as they were.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>A gun on one of a fort's four tower spots: exactly the cannons Zod makes not ejectable (ZWorld.MarkFortTurretCannons at the
        /// map start, ZWorld.PlaceCannon for one placed from the fort's store, DV73's door-tower gun on Hard).</summary>
        public bool IsTowerGun => Kind == ZKind.Cannon && !Ejectable;

        /// <summary>DV153: o is a living enemy fort tower gun this unit can hit from where it stands.</summary>
        internal bool TowerGunInReach(ZObject o) =>
            ZWorld.TowerAnswerDeviation && o != null && o.IsTowerGun && !o.Destroyed && o.KillAt < 0 && o.Owner != Owner && o.Owner != Team.Neutral && WithinAttackRadius(o);

        /// <summary>The automatic target rules' "recently failed to reach" test: PathUnreachable, except (DV153) for a fort tower gun in reach.</summary>
        internal bool SkipUnreachable(ZObject o) => PathUnreachable(o) && !TowerGunInReach(o);

        /// <summary>The waypoint an automatic chase (idle agro, a squad's help call) gives for target t: Zod's AGRO; DV153: an ATTACK for a fort
        /// tower gun that is not in reach yet (Zod's planner drives to a spot in range with a clear shot).</summary>
        internal WpMode ChaseMode(ZObject t)
        {
            if (!ZWorld.TowerAnswerDeviation || t == null || !t.IsTowerGun || WithinAttackRadius(t)) return WpMode.Agro;
            ZWorld.TowerChases++;
            return WpMode.Attack;
        }

        /// <summary>DV153 (ZWorld.HelpCall): a fort tower gun's shot hurt one of this unit's team, and this unit is busy only with automatic steps:
        /// a sidestep from a shell (Zod's DodgeMissile), the server's nudge or the pathing layer's own step aside / back off / regroup / bay (the
        /// moves <see cref="LayerShuffle"/> names), or blasting a rock that stands in its way (Zod's DoAttackImpassableAtCoords; its target a
        /// destroyable blocker). Nothing the player or the computer ordered (no player-given waypoint, no attack-move), no fight with a unit.
        /// True: the help call adds its chase behind those steps, so the unit answers once it has stepped aside (and a squad leader's list, which
        /// its robots copy, then carries the chase to them too).</summary>
        internal bool TowerHelpAfterStep(ZObject a)
        {
            if (!ZWorld.TowerAnswerDeviation || a == null || !a.IsTowerGun) return false;
            if (AttackObject != null && !AttackObject.IsDestroyableImpass) return false;
            foreach (var w in Waypoints)
            {
                if (w.playerGiven || w.attackTo) return false;
                if (w.mode == WpMode.Dodge) continue;
                if (w.mode == WpMode.Move && (w.refId == -1 || (w.refId <= ZPathing.RefStepAside && w.refId >= ZPathing.RefBay))) continue;
                if (w.mode == WpMode.Attack && World?.Get(w.refId) is ZObject r && r.IsDestroyableImpass) continue;
                return false;
            }
            return true;
        }

        /// <summary>DV153 (ProcessAttackWP): no spot with a clear shot at a fort tower gun anywhere: remembered like an unreachable goal, so the
        /// automatic chases do not ask again every half second (the player's own order on it ends as before).</summary>
        void TowerPlanFailed(ZObject t)
        {
            if (!ZWorld.TowerAnswerDeviation || t == null || !t.IsTowerGun) return;
            PS.Remember(t.Id, World.Time + ZPathing.UnreachableMemory);
            ZWorld.TowerPlansFailed++;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV153 switch (code switch; View/ZGame.cs: on, off in 'Zod exact'; headless default off).</summary>
        public static bool TowerAnswerDeviation = false;
        /// <summary>DV153 statistics (the counted check): automatic chases of a tower gun given as an ATTACK, attacks on one with no shot spot,
        /// help-call chases that went in behind a step aside.</summary>
        public static int TowerChases, TowerPlansFailed, TowerStepHelps;

        /// <summary>The parity report and the older counted checks: no DV153 while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable TowerAnswerOff() => new TowerAnswerHold();
        sealed class TowerAnswerHold : IDisposable
        {
            readonly bool was = TowerAnswerDeviation;
            public TowerAnswerHold() { TowerAnswerDeviation = false; }
            public void Dispose() { TowerAnswerDeviation = was; }
        }
    }
}
