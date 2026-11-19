using System;

namespace OpenZ.Sim
{
    public enum ZHealthBand { Green, Yellow, Red }

    public partial class ZObject
    {
        // Match the three existing Unit Editor hover-card colours, including exact boundaries.
        public ZHealthBand HealthBand => MaxHealth <= 0 || (double)Health / MaxHealth > .5 ? ZHealthBand.Green :
            (double)Health / MaxHealth > .25 ? ZHealthBand.Yellow : ZHealthBand.Red;
        public bool CripplingActive => Kind == ZKind.Vehicle && ZBehaviorSchema.IsTank(Type) && Behavior != null && Behavior.On(ZBehaviorKey.Crippling);

        public bool ShowsCriticalTankDamage => !Destroyed && Kind == ZKind.Vehicle &&
            (ZBehaviorSchema.IsTank(Type) || ZBigTanks.Is(Type)) &&
            MaxHealth > 0 && Health > 0 && (long)Health * 10 <= (long)MaxHealth * 3;

        public float CriticalTankDamageSeverity
        {
            get
            {
                if (!ShowsCriticalTankDamage) return 0;
                double remaining = (double)Health * 10 / ((long)MaxHealth * 3);
                return (float)(1 - remaining * remaining * remaining);
            }
        }
        public double CripplingImpactUntil, CripplingImpactFactor = 1, CripplingActualSpeed;
        public bool CripplingHitActive => Behavior != null && Behavior.On(ZBehaviorKey.Impact) && World.Time < CripplingImpactUntil;
        public bool BehaviorUsesActualMotion => Behavior != null && (BehaviorSteersHull || CripplingActive || Behavior.On(ZBehaviorKey.Impact));
        public double CripplingValue(ZBehaviorKey greenKey) => !CripplingActive ? 0 :
            Behavior[(ZBehaviorKey)((int)greenKey + (int)HealthBand * 6)];
        double CripplingAcceleration => CripplingValue(ZBehaviorKey.GreenAcceleration);

        void CripplingHit(int lost, int attackerId)
        {
            var p = Behavior;
            if (lost <= 0 || Destroyed || Kind != ZKind.Vehicle || !ZBehaviorSchema.IsTank(Type) || p == null || !p.On(ZBehaviorKey.Impact)) return;
            var attacker = World.Get(attackerId);
            if (attacker != null && (attacker.Owner == Owner || attacker.Owner == Team.Neutral)) return;
            // Unknown attackers include explosions whose firing unit has already been removed.
            bool heavy = lost >= p[ZBehaviorKey.HeavyImpactMin] && p[ZBehaviorKey.HeavyImpactDuration] > 0;
            if (!heavy && (lost < p[ZBehaviorKey.ImpactMin] || p[ZBehaviorKey.ImpactDuration] <= 0)) return;
            double factor = Math.Min(100, p[heavy ? ZBehaviorKey.HeavyImpactSpeed : ZBehaviorKey.ImpactSpeed]) / 100;
            double until = World.Time + p[heavy ? ZBehaviorKey.HeavyImpactDuration : ZBehaviorKey.ImpactDuration];
            if (CripplingHitActive)
            { factor = Math.Min(factor, CripplingImpactFactor); until = Math.Max(until, CripplingImpactUntil); }
            CripplingImpactFactor = factor; CripplingImpactUntil = until;
        }

        void CripplingCommand(ref double vx, ref double vy)
        {
            double factor = CripplingActive ? Math.Min(100, CripplingValue(ZBehaviorKey.GreenSpeed)) / 100 / Math.Max(.001, DamagedSpeed()) : 1;
            if (CripplingHitActive) factor *= CripplingImpactFactor;
            vx *= factor; vy *= factor;
        }
        void CripplingFreeVelocity(double dt, ref double vx, ref double vy)
        {
            double desired = Math.Sqrt(vx * vx + vy * vy);
            CripplingActualSpeed = CripplingAcceleration <= 0 || desired <= CripplingActualSpeed ? desired :
                Toward(CripplingActualSpeed, desired, Math.Max(1, MoveSpeed) / CripplingAcceleration * dt);
            double factor = desired > 0 ? CripplingActualSpeed / desired : 0;
            vx *= factor; vy *= factor;
        }
        // Select an axis at most 90 degrees from the current hull. Exact side-on ties go forward.
        public static bool NearestHullReverse(double heading, double travel) => Math.Abs(WrapRadians(travel - heading)) > Math.PI / 2 + 1e-10;
    }
}
