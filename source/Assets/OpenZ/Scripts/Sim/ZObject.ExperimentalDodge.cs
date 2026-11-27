using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    public partial class ZObject
    {
        sealed class EnemyCadence
        {
            public double Last, Period;
            public int Samples, Planned;
        }

        Dictionary<int, EnemyCadence> experimentalCadences;
        int experimentalLastShooter = -1;
        double experimentalNextCheck, experimentalBrakeUntil;
        bool experimentalBrake;
        public int ExperimentalAnticipations { get; private set; }
        public int ExperimentalBrakeDodges { get; private set; }

        bool ExperimentalDodging => Kind == ZKind.Vehicle && Behavior != null &&
            Behavior.On(ZBehaviorKey.ExperimentalDodging);

        internal void ObserveExperimentalShot(ZObject enemy, bool cadenceShot)
        {
            if (!ExperimentalDodging || enemy == null || enemy == this || enemy.AttackSpeed <= 0 ||
                World.AreAllied(Owner, enemy.Owner)) return;
            experimentalLastShooter = enemy.Id;
            if (!cadenceShot) return;
            var history = experimentalCadences ??= new Dictionary<int, EnemyCadence>();
            double now = World.Time, period = Math.Clamp(enemy.AttackSpeed, .3, 12);
            if (!history.TryGetValue(enemy.Id, out var cadence))
            {
                if (history.Count >= 6)
                {
                    int oldest = enemy.Id;
                    double oldestTime = double.PositiveInfinity;
                    foreach (var entry in history)
                        if (entry.Value.Last < oldestTime) { oldest = entry.Key; oldestTime = entry.Value.Last; }
                    history.Remove(oldest);
                }
                cadence = new EnemyCadence { Last = now, Period = period };
                history.Add(enemy.Id, cadence);
            }
            double interval = now - cadence.Last;
            if (interval >= cadence.Period * .55 && interval <= cadence.Period * 1.65)
                cadence.Period += (interval - cadence.Period) * .5;
            else if (interval > cadence.Period * 1.65)
            { cadence.Period = period; cadence.Samples = 0; cadence.Planned = 0; }
            cadence.Last = now;
            cadence.Samples = Math.Min(20, cadence.Samples + 1);
        }

        ZObject ExperimentalFocus(ZObject assigned)
        {
            var latest = World.Get(experimentalLastShooter);
            if (assigned != null && ExperimentalCanShoot(assigned))
            {
                int count = 0;
                foreach (var enemy in World.NearUnits(CenterX, CenterY, 256, World.UnitObjects))
                    if (ExperimentalCanShoot(enemy) && ++count == 2) return assigned;
            }
            return ExperimentalCanShoot(latest) ? latest : assigned;
        }

        bool ExperimentalCanShoot(ZObject enemy) => enemy != null && enemy != this && !enemy.Destroyed &&
            enemy.Owner != Team.Neutral && !World.AreAllied(Owner, enemy.Owner) &&
            enemy.DamageIsMissile && enemy.MissileSpeed > 0 && enemy.CanAttackObject(this) && enemy.WithinAttackRadius(this);

        ZObject ExperimentalAttackTarget()
        {
            int i = BehaviorDodging ? 1 : 0;
            if (Waypoints.Count <= i) return null;
            var order = Waypoints[i];
            return order.mode == WpMode.Attack || order.mode == WpMode.Agro ? World.Get(order.refId) : null;
        }

        void CheckExperimentalAnticipation()
        {
            if (!ExperimentalDodging || experimentalCadences == null || BehaviorDodging || HoldPosition ||
                World.Time < experimentalNextCheck) return;
            experimentalNextCheck = World.Time + .05;
            var enemy = ExperimentalFocus(ExperimentalAttackTarget());
            if (!ExperimentalCanShoot(enemy) || !experimentalCadences.TryGetValue(enemy.Id, out var cadence) ||
                cadence.Planned == cadence.Samples) return;

            // The first intervals are uncertain. Familiar opponents get a narrower prediction.
            double uncertainty = .32 / (1 + (cadence.Samples - 1) * 1.5);
            double sample = DodgeVariation(2, unchecked((uint)(enemy.Id * 131 + cadence.Samples * 151))) - 1;
            double lead = Math.Clamp(cadence.Period * .14, .13, .36);
            double launch = cadence.Last + cadence.Period + uncertainty * sample;
            double until = launch - World.Time;
            if (until > lead || until < -.1) return;
            double distance = Dist(CenterX, CenterY, enemy.CenterX, enemy.CenterY);
            double flight = distance / Math.Max(1, enemy.MissileSpeed);
            double horizon = Math.Min(1.25, Math.Max(.05, until) + flight);
            int tx = (int)Math.Round(CenterX + BehaviorStepDx * horizon);
            int ty = (int)Math.Round(CenterY + BehaviorStepDy * horizon);
            double proximity = distance / Math.Max(1, RangeTo(enemy));
            bool brake = BehaviorActualSpeed >= Math.Max(1, BehaviorBaseMoveSpeed) * 1.45 &&
                sample < (proximity < .55 ? -.86 : -.64);
            cadence.Planned = cadence.Samples;
            if (BehaviorDodge(tx, ty, horizon, ZThreatKind.Shell, enemy.Damage, enemy.Id, false,
                enemy.DamageRadius, anticipateBrake: brake))
            {
                ExperimentalAnticipations++;
                if (experimentalBrake) ExperimentalBrakeDodges++;
            }
        }
    }
}
