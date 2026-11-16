using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public partial class UnitView
    {
        double cripplingSmokeAt, cripplingOilAt;
        Vector3 cripplingRear;
        bool cripplingTrail;

        Quaternion CripplingVisual(bool moving, Vector3 position, float dt)
        {
            if (!Obj.CripplingActive || !moving || dt <= 0 || game.World.UnitTesting != null && game.World.UnitTesting.Held(Obj))
            { cripplingTrail = false; return Quaternion.identity; }
            double now = game.World.Time;
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var rear = position - forward * (Obj.Type == "heavy" ? 1.55f : 1.2f);
            float smoke = (float)Obj.CripplingValue(ZBehaviorKey.GreenSmoke);
            var fx = OpenZFxPlayer.I;
            if (smoke > 0 && now >= cripplingSmokeAt && fx && fx.isActiveAndEnabled && WreckFx.OnScreen(rear, .1f))
            {
                // Cap emission to one puff per drawn frame, even after a pause or a camera cut.
                cripplingSmokeAt = now + 1 / smoke;
                fx.EmitChannel("leg_smoke", rear + Vector3.up * .55f, -forward * .25f + Vector3.up * .65f,
                    1.8f, .65f, new Color(.2f, .22f, .24f), .5f);
            }
            float width = (float)Obj.CripplingValue(ZBehaviorKey.GreenOil);
            double life = Obj.CripplingValue(ZBehaviorKey.GreenOilLife);
            if (width > 0 && life > 0)
            {
                if (!cripplingTrail) { cripplingRear = rear; cripplingOilAt = now; cripplingTrail = true; }
                if (now >= cripplingOilAt && (rear - cripplingRear).sqrMagnitude > .0025f)
                {
                    if ((rear - cripplingRear).sqrMagnitude < 36f)
                    {
                        var trails = game.GetComponent<OzCripplingTrails>();
                        if (!trails) trails = game.gameObject.AddComponent<OzCripplingTrails>();
                        trails.Add(game, cripplingRear, rear, Mathf.Min(2f, width), life);
                    }
                    cripplingRear = rear; cripplingOilAt = now + .08;
                }
            }
            else cripplingTrail = false;
            float shake = (float)Obj.CripplingValue(ZBehaviorKey.GreenShake);
            if (shake <= 0 || OzMotion.Reduced) return Quaternion.identity;
            // Simulation time keeps pause and game-speed controls coherent. No simulation RNG is consumed.
            float phase = (float)(now * 29 + Obj.Id * 2.17);
            return Quaternion.Euler(Mathf.Sin(phase) * shake, 0, Mathf.Sin(phase * 1.37f) * shake);
        }
    }
}
