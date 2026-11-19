using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public partial class UnitView
    {
        double cripplingOilAt;
        Vector3 cripplingRear;
        int oilSegment;
        bool cripplingTrail;

        Quaternion CripplingVisual(bool moving, Vector3 position, float dt)
        {
            if (Obj.Destroyed || game.World.UnitTesting != null && game.World.UnitTesting.Held(Obj) ||
                OzRadarLoss.Covers(Obj))
            { cripplingTrail = false; return Quaternion.identity; }
            double now = game.World.Time;
            bool critical = Obj.ShowsCriticalTankDamage;
            float severity = Obj.CriticalTankDamageSeverity;
            if (critical && moving && dt > 0 && OzBattleOptions.Damage(game.World))
            {
                var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                var rear = position - forward * Mathf.Max(1.2f, Obj.HeightPix / 16f - .45f);
                var mark = rear + Vector3.Cross(Vector3.up, forward) * (Mathf.Lerp(.04f, .19f, severity) * Mathf.Sin(oilSegment * 2.39f + Obj.Id));
                if (!cripplingTrail) { cripplingRear = mark; cripplingOilAt = now; cripplingTrail = true; }
                if (now >= cripplingOilAt && (mark - cripplingRear).sqrMagnitude > .0025f)
                {
                    if ((mark - cripplingRear).sqrMagnitude < 36f)
                    {
                        float width = Mathf.Lerp(.10f, .55f, severity) + (float)Obj.CripplingValue(ZBehaviorKey.GreenOil);
                        double life = 60 + Obj.CripplingValue(ZBehaviorKey.GreenOilLife);
                        var trails = game.GetComponent<OzCripplingTrails>();
                        if (!trails) trails = game.gameObject.AddComponent<OzCripplingTrails>();
                        trails.Add(game, cripplingRear, mark, width, life, 6);
                    }
                    oilSegment++;
                    cripplingRear = mark; cripplingOilAt = now + Mathf.Lerp(.24f, .065f, severity);
                }
            }
            else cripplingTrail = false;
            if (OzMotion.Reduced) return Quaternion.identity;
            float shake = moving ? (float)Obj.CripplingValue(ZBehaviorKey.GreenShake) : 0;
            if (critical && OzBattleOptions.Shake(game.World)) shake += Mathf.Lerp(.4f, 4.2f, severity);
            if (shake <= 0) return Quaternion.identity;
            // Simulation time keeps pause and game-speed controls coherent. No simulation RNG is consumed.
            float phase = (float)(now * 29 + Obj.Id * 2.17);
            return Quaternion.Euler(Mathf.Sin(phase) * shake, 0, Mathf.Sin(phase * 1.37f) * shake);
        }
    }
}
