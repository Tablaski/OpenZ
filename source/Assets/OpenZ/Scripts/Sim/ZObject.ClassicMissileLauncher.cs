using System;

namespace OpenZ.Sim
{
    public sealed partial class ZWorld
    {
        public bool ClassicMissileLauncher { get; set; }
    }

    public partial class ZObject
    {
        public const float ClassicMissileTubeSpacing = 0.85f;
        public const float ClassicMissileMuzzleReach = 0.61f;

        public bool UsesClassicMissileLauncher =>
            Kind == ZKind.Vehicle && Type == "missile_launcher" && World?.ClassicMissileLauncher == true;

        void FireClassicMissileSalvo(ZObject target, int x, int y, double time, bool playerGiven)
        {
            var middle = new ZMissile
            {
                team = Owner, attackerId = Id, targetId = target.Id, playerGiven = playerGiven,
                x = x, y = y, radius = DamageRadius, damage = Damage / 3 + Damage % 3
            };
            double dx = x - CenterX, dy = y - CenterY;
            double length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            // Use the same constant speed as the standard MLRS, measured from the outlets.
            double flightDistance = Math.Max(1, length - ClassicMissileMuzzleReach * 8);
            middle.explodeTime = time + flightDistance / Math.Max(1, MissileSpeed);
            double lane = ClassicMissileTubeSpacing * 8;
            int sideX = (int)Math.Round(-dy * lane / length);
            int sideY = (int)Math.Round(dx * lane / length);

            World.AddMissile(middle);
            for (int side = -1; side <= 1; side += 2)
            {
                World.AddMissile(new ZMissile
                {
                    team = Owner, attackerId = Id, targetId = target.Id, playerGiven = playerGiven,
                    x = x + side * sideX, y = y + side * sideY,
                    radius = DamageRadius, damage = Damage / 3, explodeTime = middle.explodeTime
                });
            }

            World.Emit(ZEventType.MissileFired, this, target, x, y,
                (float)(middle.explodeTime - time), middle.radius);
            World.AlertBehaviorThreat(target, middle, ZThreatKind.Rocket);
        }
    }
}
