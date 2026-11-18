using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public partial class UnitView
    {
        double cripplingSmokeAt, cripplingOilAt;
        Vector3 cripplingRear;
        int oilSegment;
        bool cripplingTrail;

        Quaternion CripplingVisual(bool moving, Vector3 position, float dt)
        {
            bool damageEffects = Obj.Kind == ZKind.Vehicle && ZBehaviorSchema.IsTank(Obj.Type) && OzBattleOptions.Damage(game.World);
            if ((!damageEffects && (!Obj.CripplingActive || !moving)) || Obj.Destroyed || dt <= 0 ||
                game.World.UnitTesting != null && game.World.UnitTesting.Held(Obj) || OzRadarLoss.Covers(Obj))
            { cripplingTrail = false; return Quaternion.identity; }
            double now = game.World.Time;
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var rear = position - forward * (Obj.Type == "heavy" ? 1.55f : 1.2f);
            float health = Obj.MaxHealth > 0 ? (float)Obj.Health / Obj.MaxHealth : 1;
            float severity = Mathf.Clamp01(1 - 2 * health);
            float smoke = damageEffects ? health <= .5f ? Mathf.Lerp(2, 12, severity) : 0 : (float)Obj.CripplingValue(ZBehaviorKey.GreenSmoke);
            var fx = OpenZFxPlayer.I;
            if (smoke > 0 && now >= cripplingSmokeAt && fx && fx.isActiveAndEnabled && WreckFx.OnScreen(rear, .1f))
            {
                // Cap emission to one puff per drawn frame, even after a pause or a camera cut.
                cripplingSmokeAt = now + 1 / smoke;
                fx.EmitChannel("leg_smoke", rear + Vector3.up * .55f, -forward * .25f + Vector3.up * .65f,
                    damageEffects ? Mathf.Lerp(1.8f, 3.2f, severity) : 1.8f,
                    damageEffects ? Mathf.Lerp(.65f, 1.3f, severity) : .65f,
                    damageEffects ? Color.Lerp(new Color(.34f, .34f, .36f), new Color(.035f, .035f, .04f), severity)
                        : new Color(.2f, .22f, .24f), damageEffects ? Mathf.Lerp(.45f, .85f, severity) : .5f);
            }
            float width = damageEffects ? health <= .25f ? Mathf.Lerp(.15f, .48f, 1 - health * 4) : 0 : (float)Obj.CripplingValue(ZBehaviorKey.GreenOil);
            double life = damageEffects ? 60 : Obj.CripplingValue(ZBehaviorKey.GreenOilLife);
            if (moving && width > 0 && life > 0)
            {
                // Offset the rear marks slightly so a straight pass leaves a broken, irregular spill.
                Vector3 mark = damageEffects ? rear + Vector3.Cross(Vector3.up, forward) * (.12f * Mathf.Sin(oilSegment * 2.39f + Obj.Id)) : rear;
                if (!cripplingTrail) { cripplingRear = mark; cripplingOilAt = now; cripplingTrail = true; }
                if (now >= cripplingOilAt && (mark - cripplingRear).sqrMagnitude > .0025f)
                {
                    if ((mark - cripplingRear).sqrMagnitude < 36f)
                    {
                        var trails = game.GetComponent<OzCripplingTrails>();
                        if (!trails) trails = game.gameObject.AddComponent<OzCripplingTrails>();
                        trails.Add(game, cripplingRear, mark, Mathf.Min(2f, width), life, damageEffects ? 6 : 0);
                    }
                    oilSegment++;
                    cripplingRear = mark; cripplingOilAt = now + (damageEffects ? .22 : .08);
                }
            }
            else cripplingTrail = false;
            float shake = (float)Obj.CripplingValue(ZBehaviorKey.GreenShake);
            if (!moving || shake <= 0 || OzMotion.Reduced) return Quaternion.identity;
            // Simulation time keeps pause and game-speed controls coherent. No simulation RNG is consumed.
            float phase = (float)(now * 29 + Obj.Id * 2.17);
            return Quaternion.Euler(Mathf.Sin(phase) * shake, 0, Mathf.Sin(phase * 1.37f) * shake);
        }
    }
}
