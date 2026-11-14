using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 DV174 (Sim/ZEvilZod.Face.cs), fix round 1, EvilZod only. UnitAim.AdjustBodyYaw turns a standing robot's drawn body onto its target, but while the legs
    /// still show a walk (walkShown: the drawn stride has to fall under 0.15 m/s first, about 0.2-0.3 s after the rules stop him) it keeps the walk line unless the
    /// robot is "firing", and EvilZod's shots never mark his UnitAim as firing (his EvilShot is no Hitscan: it never goes through HoldShot or OnFire). The rules,
    /// meanwhile, turn him onto the target and fire 0-24 ticks after the pick, so right after a stop his drawn gun trailed the rules' shot and OzEvilZodFx's gate held
    /// the volley, up to its cap. Here, called first in EvilZodYaw (after AdjustBodyYaw, before the dash's own turn), his drawn body turns from last frame's yaw toward
    /// the target at the rules' own turn speed (<see cref="EvilFaceTurnDegPerSec"/>, the robots' 720 deg/s, per game second) when
    /// <list type="bullet">
    /// <item>the rules stand him with a live attack target while the legs still walk (the walk tail), or</item>
    /// <item>a volley of his is held by the gate for his drawn gun (OzEvilZodFx.ShotHeldTarget), whatever else he does.</item>
    /// </list>
    /// It only ever leaves the body nearer the target than this frame's other turns did (a standing body's eased turn is kept when it is ahead). Not in the dash or
    /// its brake (the dash line), not while dying. Every other unit leaves at the first line; nothing here is read by the rules.
    /// </summary>
    public partial class UnitView
    {
        /// <summary>DV174 fix round 1: his drawn body's turn onto the target, deg per game second (the rules' ZEvilFace.TurnCentiPerTick, the robots' Slew of UnitAim).</summary>
        public static float EvilFaceTurnDegPerSec = ZEvilFace.TurnCentiPerTick;

        void EvilFaceYaw(float dt)
        {
            if (Obj == null || !Obj.IsEvilZod || Obj.EvilDashing || evilBrake || Dying || dt <= 0f || !game) return;
            var t = OzEvilZodFx.ShotHeldTarget(this);                                              // a volley held for his drawn gun (a kill shot's target may be dead already)
            if (t == null && !Obj.IsMoving && walkShown && Obj.AttackObject != null && !Obj.AttackObject.Destroyed) t = Obj.AttackObject;   // the walk tail: the rules stand him and aim
            if (t == null) return;
            var d = game.CenterWorld(t, false) - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.04f) return;
            float aim = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;                                     // as AimYaw
            float mine = Mathf.MoveTowardsAngle(prevYaw, aim, EvilFaceTurnDegPerSec * Mathf.Max(0.25f, game.gameSpeed) * dt);
            if (Mathf.Abs(Mathf.DeltaAngle(mine, aim)) < Mathf.Abs(Mathf.DeltaAngle(yaw, aim))) yaw = mine;
        }
    }
}
