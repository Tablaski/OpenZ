using System;

namespace OpenZ.Sim
{
    internal static class ZAiCombat
    {
        internal static double Power(ZObject unit)
        {
            if (!unit.CanAttack()) return 0;
            double range = 1 + Math.Min(450, unit.AttackRadius) / 600d;
            double speed = 1 + Math.Min(60, unit.MoveSpeed) / 180d;
            return Math.Sqrt(Math.Max(0, unit.Health) * Firepower(unit)) * range * speed;
        }

        internal static double Firepower(ZObject unit) => unit.Damage *
            (unit.DamageIsMissile ? 1 : Math.Max(.05, Math.Min(1, unit.DamageChance))) / Math.Max(.20, unit.AttackSpeed);

        internal static double Avoidance(ZObject unit, ZObject attacker)
        {
            var behavior = unit.Behavior;
            if (!attacker.DamageIsMissile || behavior == null || !unit.CanMove || unit.Stunned ||
                !behavior.On(ZBehaviorKey.Enabled) || !behavior.On(ZBehaviorKey.Engaged)) return 0;
            var threat = attacker.BehaviorRocketWeapon ? ZBehaviorKey.ThreatRocket : ZBehaviorKey.ThreatShell;
            if (!behavior.On(threat)) return 0;
            if (attacker.Damage < behavior[ZBehaviorKey.MinDamage] ||
                behavior.On(ZBehaviorKey.VisibleOnly) && !unit.CanSee(attacker)) return 0;
            double flight = ZAiGeometry.Distance(unit, attacker) / Math.Max(1, attacker.MissileSpeed);
            double reaction = behavior[ZBehaviorKey.Reaction] + behavior[ZBehaviorKey.ReactionSpread] * .5;
            double window = Math.Max(0, flight - reaction);
            double speed = Math.Max(unit.MoveSpeed, unit.RealMoveSpeed) * Math.Max(.25, behavior[ZBehaviorKey.DodgeSpeed]);
            if (behavior.On(ZBehaviorKey.Boost))
                speed *= 1 + (behavior[ZBehaviorKey.BoostSpeed] - 1) * behavior[ZBehaviorKey.BoostChance] / 100;
            double clearance = Math.Max(1, attacker.DamageRadius + unit.WidthPix / 2.0);
            double distance = Math.Min(window * speed, behavior[ZBehaviorKey.MaxDistance]);
            double room = Math.Min(1, distance / clearance);
            return Math.Min(.8, behavior[ZBehaviorKey.Chance] / 100 * room);
        }

        internal static double Exchange(ZObject unit, ZObject target, bool detailed)
        {
            if (!target.CanAttack()) return 3;
            double outgoing = Firepower(unit), incoming = Firepower(target);
            if (detailed)
            {
                outgoing *= 1 - Avoidance(target, unit);
                incoming *= 1 - Avoidance(unit, target);
                double reach = Math.Max(-.35, Math.Min(.35, (unit.AttackRadius - target.AttackRadius) / 250d));
                outgoing *= 1 + reach;
                if (unit.CanSnipe && target.CanBeSniped()) outgoing *= 1 + Math.Min(.4, unit.SnipeChance);
            }
            return outgoing * Math.Max(1, unit.Health) / Math.Max(1, incoming * Math.Max(1, target.Health));
        }
    }
}
