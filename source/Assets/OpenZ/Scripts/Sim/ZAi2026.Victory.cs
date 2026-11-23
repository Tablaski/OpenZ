using System;

namespace OpenZ.Sim
{
    internal sealed partial class ZAi2026
    {
        bool TryFinishOpposition(ZObject unit)
        {
            bool skilled = level >= ZBotDifficulty.Skilled;
            ZObject best = null;
            double quickest = 60;
            double firepower = Math.Max(1, ZAiCombat.Firepower(unit));

            // Only units seen at this observation are candidates. The world still decides who has been eliminated.
            if (observedOpposition > 0 && observedOpposition <= (skilled ? 3 : 1))
            {
                foreach (var target in enemies)
                {
                    if (!ZWorld.CountsForElimination(target) || !unit.CanAttackObject(target) || !SafeShot(unit, target)) continue;
                    double distance = ZAiGeometry.Distance(unit, target);
                    bool inRange = unit.WithinAttackRadius(target);
                    double arrival = inRange ? 0 : Math.Max(0, distance - unit.AttackRadius) / Math.Max(1, unit.MoveSpeed);
                    double time = arrival + target.Health / firepower;
                    if (time >= quickest || ZAiCombat.Exchange(unit, target, skilled) * LocalSupport(unit, target) < .85 ||
                        !inRange && !Reachable(unit, target.CenterX, target.CenterY, 6)) continue;
                    best = target;
                    quickest = time;
                }
            }

            if (skilled && unit.HasExplosives())
            {
                foreach (var fort in enemies)
                {
                    if (!fort.IsFort || !unit.CanAttackObject(fort) || !fort.GetCreationMovePoint(out int x, out int y) ||
                        ZAiGeometry.DistanceSquared(unit, fort.CenterX, fort.CenterY) > 360L * 360) continue;

                    double killTime = Math.Max(unit.AttackSpeed, fort.Health / firepower);
                    double incoming = 0;
                    foreach (var guard in enemies)
                    {
                        if (!GuardsGate(guard, fort, x, y)) continue;
                        bool returnsFire = guard.CanAttackObject(unit) && guard.WithinAttackRadius(unit);
                        if (returnsFire) incoming += ZAiCombat.Firepower(guard);
                        if (!unit.CanAttackObject(guard) || !SafeShot(unit, guard) || !unit.WithinAttackRadius(guard)) continue;
                        double suppression = Math.Max(unit.AttackSpeed, guard.Health / firepower);
                        if (suppression >= quickest || returnsFire &&
                            ZAiCombat.Exchange(unit, guard, true) * LocalSupport(unit, guard) < 1.05) continue;
                        best = guard;
                        quickest = suppression;
                    }
                    if (killTime >= quickest || !unit.WithinAttackRadius(fort) || !SafeShot(unit, fort) ||
                        incoming * killTime >= unit.Health * .65) continue;
                    best = fort;
                    quickest = killTime;
                }
            }

            if (best == null) return false;
            if (unit.Waypoints.Count > 0 && unit.Waypoints[0].mode == WpMode.Attack &&
                unit.Waypoints[0].refId == best.Id) return true;
            return Issue(unit, best, WpMode.Attack);
        }
    }
}
