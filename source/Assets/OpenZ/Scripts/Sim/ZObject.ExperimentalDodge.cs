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

        public int ExperimentalFeints { get; private set; }
        public int ExperimentalGearChanges { get; private set; }
        public int ExperimentalSeparations { get; private set; }

        double ExperimentalDuelGap(ZObject enemy)
        {
            double minimum = ZPathing.MinSpacing(this, enemy) + 8;
            double requested = Behavior[ZBehaviorKey.ExperimentalDuelGap] / ZConst.PX_TO_M;
            return requested <= 0 ? 0 : Math.Max(minimum, Math.Min(requested, RangeTo(enemy) - 12));
        }

        bool TryExperimentalFeint(ZDuelState d, ZObject enemy, bool agro)
        {
            double chance = Behavior[ZBehaviorKey.ExperimentalFeintChance];
            if (!d.active || chance <= 0 || !BehaviorHeadingSet) return false;
            double now = World.Time;
            if (d.feintEnd <= now)
            {
                if (now < d.nextFeint) return false;
                double expectedShot = double.PositiveInfinity;
                if (experimentalCadences != null && experimentalCadences.TryGetValue(enemy.Id, out var cadence))
                {
                    expectedShot = cadence.Last + cadence.Period;
                    if (expectedShot > now + .43 && expectedShot < now + cadence.Period)
                    {
                        d.nextFeint = expectedShot - .38;
                        return false;
                    }
                }
                var rng = World.DuelRandom;
                d.nextFeint = now + .85 + rng.Next(51) * .01;
                if (rng.Next(10000) >= chance * 100) return false;
                d.feintAxis = BehaviorHeading;
                d.feintTurn = (.12 + rng.Next(36) * .01) * d.sign;
                double nominalSwitch = now + .23 + rng.Next(9) * .01;
                d.feintSwitch = expectedShot >= now && expectedShot <= now + .43 ?
                    Math.Clamp(expectedShot + .05, now + .19, now + .40) : nominalSwitch;
                d.feintEnd = d.feintSwitch + .44 + rng.Next(21) * .01;
                d.feintCounted = false;
            }

            bool forward = now < d.feintSwitch;
            double travel = d.feintAxis + (forward ? d.feintTurn : -d.feintTurn + Math.PI);
            double ux = Math.Cos(travel), uy = Math.Sin(travel);
            if (!DuelHeadingOk(enemy, ux, uy, forward ? 8 : 14, agro, out int x, out int y, forward ? 1 : -1) ||
                DodgeRadar && RadarApproach(x, y, enemy) > .55)
            {
                d.feintEnd = now;
                return false;
            }

            d.experimentalGear = forward ? 1 : -1;
            if (forward && !d.feintCounted) { d.feintCounted = true; ExperimentalFeints++; }
            SetTarget(x, y);
            SetVelocity();
            d.nextAim = Math.Min(now + .08, forward ? d.feintSwitch : d.feintEnd);
            return true;
        }

        static double ExperimentalCurvePulse(double phase) =>
            Math.Sin(phase) * .72 + Math.Sin(phase * 2) * .28;



        int aimOpponent = -1;
        double aimObservedAt, aimVx, aimVy, aimRotation;

        void ObserveExperimentalMotion(ZObject target)
        {
            if (Behavior[ZBehaviorKey.ExperimentalAimPrediction] <= 0 || target.Kind != ZKind.Vehicle) return;
            double now = World.Time;
            double vx = target.BehaviorStepDx, vy = target.BehaviorStepDy;
            if (aimOpponent != target.Id)
            {
                aimOpponent = target.Id;
                aimObservedAt = now;
                aimVx = vx; aimVy = vy; aimRotation = 0;
                return;
            }
            double interval = now - aimObservedAt;
            if (interval < .08) return;
            double motion = vx * vx + vy * vy;
            double previous = aimVx * aimVx + aimVy * aimVy;
            double delta = motion > 1 && previous > 1 ?
                WrapRadians(Math.Atan2(vy, vx) - Math.Atan2(aimVy, aimVx)) : 0;
            // A sudden reversal is evidence of a new course, not a continuous half-circle.
            double rate = Math.Abs(delta) < Math.PI / 2 ? Math.Clamp(delta / interval, -1.8, 1.8) : 0;
            aimRotation = aimRotation * .45 + rate * .55;
            aimVx = vx; aimVy = vy; aimObservedAt = now;
        }

        bool TryExperimentalAim(ZObject target, int projectileSpeed, out int tx, out int ty)
        {
            ObserveExperimentalMotion(target);
            double vx = aimVx, vy = aimVy;
            tx = target.CenterX; ty = target.CenterY;
            if (vx * vx + vy * vy < 1) return false;

            double certainty = Behavior[ZBehaviorKey.ExperimentalAimPrediction] / 100;
            double sample = DodgeVariation(1, unchecked((uint)(target.Id * 193 + World.TickIndex * 149))) - .5;
            double rate = sample < certainty ? aimRotation : 0;
            double flight = Math.Min(1.6, Dist(CenterX, CenterY, tx, ty) / projectileSpeed);
            for (int i = 0; i < 3; i++)
            {
                double turn = rate * flight;
                double along = Math.Abs(rate) < .01 ? flight : Math.Sin(turn) / rate;
                double across = Math.Abs(rate) < .01 ? 0 : (1 - Math.Cos(turn)) / rate;
                tx = (int)Math.Round(target.CenterX + vx * along - vy * across);
                ty = (int)Math.Round(target.CenterY + vy * along + vx * across);
                flight = Math.Min(1.6, Dist(CenterX, CenterY, tx, ty) / projectileSpeed);
            }
            return true;
        }

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
            cadence.Samples++;
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
                World.Time < experimentalNextCheck || duel != null && duel.feintEnd > World.Time) return;
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
